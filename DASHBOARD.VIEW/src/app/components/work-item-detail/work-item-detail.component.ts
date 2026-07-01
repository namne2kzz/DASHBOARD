import { NgClass, NgComponentOutlet } from '@angular/common';
import { LocalDatePipe } from '../../core/pipes/local-date.pipe';
import { Component, computed, effect, inject, signal, Type } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, FormBuilder, FormsModule, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { merge } from 'rxjs';
import { Router } from '@angular/router';
import { WorkItemApiState } from '../../core/enums/system.enum';
import { PrivilegeService } from '../../core/services/privilege.service';
import {
  AssigneeTimelineEntry,
  StateTimelineEntry,
  WorkItemPriority,
  WorkItemType,
} from '../../models/work-item.model';
import {
  PRIORITY_LABELS,
  STATE_BADGE_CLASSES,
  STATE_LABELS,
  WORK_ITEM_TYPE_COLOR,
  WORK_ITEM_TYPE_LABELS,
} from '../../core/constants/system.constant';
import { MembersService } from '../../services/members.service';
import { ResourceService } from '../../services/resource.service';
import { TaskBoardService } from '../../services/task-board.service';
import { WikiService } from '../../services/wiki.service';
import {
  stateToStatus,
  WORK_ITEM_TYPE_STATES,
} from '../../utils/work-item-mapper.util';
import { parseWikiPageIdFromLink } from '../../utils/wiki-link.util';
import { BugFieldsComponent } from './type-fields/bug-fields/bug-fields.component';
import { ImprovementFieldsComponent } from './type-fields/improvement-fields/improvement-fields.component';
import { TaskFieldsComponent } from './type-fields/task-fields/task-fields.component';
import { TestPlanFieldsComponent } from './type-fields/test-plan-fields/test-plan-fields.component';
import { UserStoryFieldsComponent } from './type-fields/user-story-fields/user-story-fields.component';

type ActiveTab = 'details' | 'discussion' | 'history';

function remainingWorkValidator(ctrl: AbstractControl): ValidationErrors | null {
  const og  = (ctrl.parent?.get('originalEstimate')?.value as number | null) ?? null;
  const rem = ctrl.value as number | null;
  if (og !== null && rem !== null && rem > og) return { exceedsOriginal: true };
  return null;
}

@Component({
  selector: 'app-work-item-detail',
  imports: [ReactiveFormsModule, FormsModule, NgClass, NgComponentOutlet, LocalDatePipe],
  templateUrl: './work-item-detail.component.html',
  styleUrl: './work-item-detail.component.css',
})
export class WorkItemDetailComponent {
  private readonly fb        = inject(FormBuilder);
  private readonly router    = inject(Router);
  readonly board     = inject(TaskBoardService);
  readonly members   = inject(MembersService);
  readonly wiki      = inject(WikiService);
  readonly resource  = inject(ResourceService);
  readonly privilege = inject(PrivilegeService);

  readonly workItemTypeLabels  = WORK_ITEM_TYPE_LABELS;
  readonly workItemTypeColors  = WORK_ITEM_TYPE_COLOR;
  readonly workItemTypeOptions = (Object.keys(WORK_ITEM_TYPE_LABELS) as WorkItemType[]).map(id => ({
    id,
    label: WORK_ITEM_TYPE_LABELS[id],
  }));
  readonly priorityOptions = (Object.keys(PRIORITY_LABELS) as WorkItemPriority[]).map(id => ({
    id,
    label: PRIORITY_LABELS[id],
  }));
  readonly sprintOptions = ['May 2026', 'June 2026', 'July 2026'];

  private readonly typeFieldsRegistry: Record<WorkItemType, Type<unknown>> = {
    'user-story':  UserStoryFieldsComponent,
    'bug':         BugFieldsComponent,
    'task':        TaskFieldsComponent,
    'improvement': ImprovementFieldsComponent,
    'test-plan':   TestPlanFieldsComponent,
  };

  /** @returns The component class to render for the currently selected work item type. */
  readonly currentTypeFieldsComponent = computed(() => this.typeFieldsRegistry[this.currentType()]);

  /** @returns The list of valid API states for the currently selected work item type. */
  readonly currentTypeStates = computed(() => WORK_ITEM_TYPE_STATES[this.currentType()]);

  readonly activeTab    = signal<ActiveTab>('details');
  readonly commentDraft = signal('');

  readonly discussionEntries  = computed(() => [...this.board.discussions()].reverse());
  readonly historyEntries     = computed(() => this.board.history());
  readonly stateBadgeClasses  = STATE_BADGE_CLASSES;
  readonly stateLabels        = STATE_LABELS;

  /** Ordered list of states extracted from history entries that record state transitions. */
  readonly stateTimeline = computed((): StateTimelineEntry[] => {
    const regex = /State changed from '(\w+)' to '(\w+)'\./;
    const entries: StateTimelineEntry[] = [];
    const allHistory = this.historyEntries(); // newest-first from API

    // Oldest entry is chronologically the creation event
    const creation = allHistory.length > 0 ? allHistory[allHistory.length - 1] : undefined;

    for (const entry of [...allHistory].reverse()) {
      const match = regex.exec(entry.message);
      if (!match) continue;

      const fromVal = WorkItemApiState[match[1] as keyof typeof WorkItemApiState];
      const toVal   = WorkItemApiState[match[2] as keyof typeof WorkItemApiState];

      if (entries.length === 0 && fromVal !== undefined) {
        entries.push({ state: fromVal, authorName: creation?.authorName, createdAt: creation?.createdAt });
      }
      if (toVal !== undefined) entries.push({ state: toVal, authorName: entry.authorName, createdAt: entry.createdAt });
    }

    return entries;
  });

  /** Ordered list of assignee changes extracted from history entries. */
  readonly assigneeTimeline = computed((): AssigneeTimelineEntry[] => {
    const assignedRx   = /Assigned to '([^']+)'\./;
    const unassignedRx = /\bUnassigned\./;
    const allHistory   = this.historyEntries(); // newest-first from API
    const result: AssigneeTimelineEntry[] = [];

    // Whether any entry explicitly records an assignment change (new backend format)
    const hasAssignmentEvent = allHistory.some(
      h => assignedRx.test(h.message) || unassignedRx.test(h.message),
    );

    for (const entry of [...allHistory].reverse()) {
      if (result.length === 0 && entry.message.includes('Work item created')) {
        const m = assignedRx.exec(entry.message);
        if (m) {
          // New backend format: assignee name embedded in creation message
          result.push({ assigneeName: m[1], authorName: entry.authorName, createdAt: entry.createdAt });
        } else if (!hasAssignmentEvent) {
          // Old backend format with no subsequent assignment events:
          // infer initial assignee from the current task (no changes = current === initial)
          const task = this.board.dialogTask();
          const name = task?.assignedToId ? this.members.displayName(task.assignedToId) : null;
          result.push({ assigneeName: name, authorName: entry.authorName, createdAt: entry.createdAt });
        }
        // Old format + has subsequent events: skip creation entry, timeline starts from first change
        continue;
      }
      const assignedMatch = assignedRx.exec(entry.message);
      if (assignedMatch) {
        result.push({ assigneeName: assignedMatch[1], authorName: entry.authorName, createdAt: entry.createdAt });
        continue;
      }
      if (unassignedRx.test(entry.message)) {
        result.push({ assigneeName: null, authorName: entry.authorName, createdAt: entry.createdAt });
      }
    }

    return result;
  });

  readonly form = this.fb.nonNullable.group({
    // ── Common ──────────────────────────────────────────────
    title:            ['', [Validators.required, Validators.maxLength(200)]],
    description:      ['', Validators.maxLength(2000)],
    priority:         this.fb.nonNullable.control<WorkItemPriority>('medium', Validators.required),
    state:            this.fb.nonNullable.control<WorkItemApiState>(WorkItemApiState.New, Validators.required),
    workItemType:     this.fb.nonNullable.control<WorkItemType>('task', Validators.required),
    sprint:           ['May 2026'],
    assignedToId:     [''],
    implementInBuild: [''],
    fixedInVersion:   [''],
    wikiLinksText:    [''],
    // ── UserStory ────────────────────────────────────────────
    acceptanceCriteria: ['', Validators.maxLength(4000)],
    storyPoints:        this.fb.control<number | null>(null),
    // ── Bug ──────────────────────────────────────────────────
    stepsToReproduce: ['', Validators.maxLength(4000)],
    environment:      [''],
    rootCause:        ['', Validators.maxLength(2000)],
    solution:         ['', Validators.maxLength(2000)],
    impaction:        ['', Validators.maxLength(2000)],
    // ── Bug + Task ────────────────────────────────────────────
    unitTest:     ['', Validators.maxLength(2000)],
    designReview: ['', Validators.maxLength(2000)],
    // ── Task ──────────────────────────────────────────────────
    originalEstimate: this.fb.control<number | null>(null, Validators.min(0)),
    remainingWork:    this.fb.control<number | null>(null, [Validators.min(0), remainingWorkValidator]),
    completedWork:    this.fb.control<number | null>({ value: null, disabled: true }),
    // ── TestPlan ──────────────────────────────────────────────
    automated:     this.fb.control<boolean | null>(null),
    testStepsText: ['', Validators.maxLength(8000)],
  });

  constructor() {
    effect(() => {
      const mode = this.board.dialogMode();
      if (mode !== null) this.activeTab.set('details');

      const task = this.board.dialogTask();
      if (mode === 'edit' && task) {
        this.form.patchValue({
          title:            task.title,
          description:      task.description,
          priority:         task.priority,
          state:            task.state,
          workItemType:     task.workItemType,
          sprint:           task.sprint,
          assignedToId:     task.assignedToId ?? '',
          implementInBuild: task.implementInBuild ?? '',
          fixedInVersion:   task.fixedInVersion  ?? '',
          wikiLinksText:    task.wikiLinks.join('\n'),
          acceptanceCriteria: task.acceptanceCriteria ?? '',
          storyPoints:        task.storyPoints ?? null,
          stepsToReproduce:   task.stepsToReproduce ?? '',
          environment:        task.environment  ?? '',
          rootCause:          task.rootCause    ?? '',
          solution:           task.solution     ?? '',
          impaction:          task.impaction    ?? '',
          unitTest:           task.unitTest     ?? '',
          designReview:       task.designReview ?? '',
          originalEstimate:   task.originalEstimate ?? null,
          remainingWork:      task.remainingWork    ?? null,
          completedWork:      task.completedWork    ?? null,
          automated:          task.automated ?? null,
          testStepsText:      task.testSteps?.join('\n') ?? '',
        });
      } else if (mode === 'create') {
        const type = this.board.createInitialType();
        this.form.reset({
          title: '', description: '', priority: 'medium',
          state:        WORK_ITEM_TYPE_STATES[type][0],
          workItemType: type,
          sprint:           'May 2026',
          assignedToId:     this.members.currentUserId() ?? '',
          implementInBuild: '', fixedInVersion: '', wikiLinksText: '',
          acceptanceCriteria: '', storyPoints: null,
          stepsToReproduce: '', environment: '', rootCause: '', solution: '', impaction: '',
          unitTest: '', designReview: '',
          originalEstimate: null, remainingWork: null, completedWork: null,
          automated: null, testStepsText: '',
        });
      }
    });

    // Auto-compute completedWork = originalEstimate - remainingWork (clamped to 0)
    // and re-run remainingWork validators when originalEstimate changes.
    merge(
      this.form.controls.originalEstimate.valueChanges,
      this.form.controls.remainingWork.valueChanges,
    ).pipe(takeUntilDestroyed()).subscribe(() => {
      const og  = this.form.controls.originalEstimate.value;
      const rem = this.form.controls.remainingWork.value;
      const completed = og !== null && rem !== null ? Math.max(0, og - rem) : null;
      this.form.controls.completedWork.setValue(completed, { emitEvent: false });
      this.form.controls.remainingWork.updateValueAndValidity({ emitEvent: false });
    });
  }

  /** @returns true when the dialog should be rendered. */
  visible(): boolean { return this.board.dialogMode() !== null; }

  /** @returns true when editing an existing work item. */
  isEdit(): boolean  { return this.board.dialogMode() === 'edit'; }

  /** Reactive signal tracking the selected work item type — drives NgComponentOutlet re-render. */
  readonly currentType = toSignal(
    this.form.controls.workItemType.valueChanges,
    { initialValue: this.form.controls.workItemType.value },
  );

  /** @returns NgClass object for the type icon badge. */
  typeIconClasses(): Record<string, boolean> {
    const t = this.currentType();
    return {
      'bg-sky-500/10 text-sky-400 ring-sky-500/35':          t === 'user-story',
      'bg-rose-500/10 text-rose-400 ring-rose-500/35':       t === 'bug',
      'bg-amber-400/10 text-amber-300 ring-amber-400/35':    t === 'task',
      'bg-orange-500/10 text-orange-400 ring-orange-400/35': t === 'improvement',
      'bg-violet-500/10 text-violet-400 ring-violet-500/35': t === 'test-plan',
    };
  }

  readonly savedWikiLinks = computed(() => this.board.dialogTask()?.wikiLinks ?? []);

  /** @param link Wiki link string. @returns Human-readable label. */
  wikiLinkLabel(link: string): string {
    const id = parseWikiPageIdFromLink(link);
    if (id) {
      const page = this.wiki.getPage(id);
      return page ? `${page.title} (${id})` : id;
    }
    return link;
  }

  /** @param link Wiki link to navigate to. */
  openWikiLink(link: string): void {
    const pageId = parseWikiPageIdFromLink(link);
    if (!pageId) return;
    this.board.closeDialog();
    void this.router.navigate(['/wiki', pageId]);
  }

  /** Posts the current comment draft to the work item discussion and clears the draft. */
  postComment(): void {
    const id = this.board.dialogTaskId();
    if (!id) return;
    const text = this.commentDraft().trim();
    if (!text) return;
    this.board.addDiscussion(id, text);
    this.commentDraft.set('');
  }

  /** Validates the form and creates or updates the work item via the board service. */
  save(): void {
    const allowed = this.isEdit() ? this.privilege.canEditWorkItem() : this.privilege.canCreateWorkItem();
    if (!allowed) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v  = this.form.getRawValue();
    const id = this.isEdit() ? (this.board.dialogTaskId() ?? undefined) : undefined;

    this.board.upsertTask({
      id,
      customId:     this.isEdit() ? (this.board.dialogTask()?.customId ?? '') : '',
      title:        v.title,
      description:  v.description,
      priority:     v.priority,
      state:        v.state,
      status:       stateToStatus(v.state),
      workItemType: v.workItemType,
      sprint:       v.sprint,
      assignedToId: v.assignedToId || null,
      implementInBuild: v.implementInBuild || null,
      fixedInVersion:   v.fixedInVersion   || null,
      wikiLinks: v.wikiLinksText.split('\n').map(l => l.trim()).filter(Boolean),
      acceptanceCriteria: v.acceptanceCriteria || null,
      storyPoints:        v.storyPoints ?? null,
      stepsToReproduce:   v.stepsToReproduce || null,
      environment:        v.environment  || null,
      rootCause:          v.rootCause    || null,
      solution:           v.solution     || null,
      impaction:          v.impaction    || null,
      unitTest:           v.unitTest     || null,
      designReview:       v.designReview || null,
      originalEstimate:   v.originalEstimate ?? null,
      remainingWork:      v.remainingWork    ?? null,
      completedWork:      v.completedWork    ?? null,
      automated:          v.automated ?? null,
      testSteps: v.testStepsText
        ? v.testStepsText.split('\n').map(s => s.trim()).filter(Boolean)
        : null,
    });
  }

  /** Closes the dialog without saving. */
  cancel(): void { this.board.closeDialog(); }

  /** Soft-deletes the current work item and closes the dialog. */
  delete(): void {
    if (!this.privilege.canDeleteWorkItem()) return;
    const id = this.board.dialogTaskId();
    if (!id) return;
    this.board.deleteTask(id);
    this.board.closeDialog();
  }
}
