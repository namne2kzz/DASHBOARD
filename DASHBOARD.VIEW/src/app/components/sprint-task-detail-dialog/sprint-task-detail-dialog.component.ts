import { ChangeDetectionStrategy, Component, computed, effect, inject, InjectionToken, OnInit, signal } from '@angular/core';
import { NgClass, NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { LocalDatePipe } from '../../core/pipes/local-date.pipe';
import { DateTimeService } from '../../core/services/date-time.service';
import { DIALOG_REF_TOKEN } from '../../models/dialog.model';
import { DialogService } from '../../core/components/dialog/dialog.service';
import { SprintTaskDetailService } from '../../services/sprint-task-detail.service';
import { SprintBoardService } from '../../services/sprint-board.service';
import { MembersService } from '../../services/members.service';
import { SprintTaskApiState, SprintTaskApiType, WorkItemApiPriority } from '../../core/enums/system.enum';
import { SPRINT_TASK_STATE_BADGE, SPRINT_TASK_STATE_LABEL, SPRINT_TASK_STATE_OPTIONS } from '../../core/constants/system.constant';
import type { UpdateSprintTaskPayload } from '../../models/sprint-task-detail.model';
import type { WorkItemPickerApiDto } from '../../models/sprint-planning-api.model';

export interface SprintTaskDetailDialogData {
  taskId: string;
}

const TYPE_ICON_CLASS: Record<SprintTaskApiType, string> = {
  [SprintTaskApiType.UserStory]: 'text-sky-400 ring-sky-500/35 bg-sky-500/10',
  [SprintTaskApiType.Task]:      'text-amber-300 ring-amber-400/35 bg-amber-400/10',
  [SprintTaskApiType.Bug]:       'text-rose-400 ring-rose-500/35 bg-rose-500/10',
  [SprintTaskApiType.TestPlan]:  'text-violet-400 ring-violet-500/35 bg-violet-500/10',
};

/** Maps the C# enum member name embedded in history messages (e.g. "InReview") back to its numeric value. */
const STATE_NAME_TO_API: Record<string, SprintTaskApiState> = {
  New:      SprintTaskApiState.New,
  Backlog:  SprintTaskApiState.Backlog,
  Todo:     SprintTaskApiState.Todo,
  Active:   SprintTaskApiState.Active,
  InReview: SprintTaskApiState.InReview,
  Done:     SprintTaskApiState.Done,
};

// Field values can legitimately contain newlines (multi-line Description, etc.) — the `s` flag
// lets '.' span them so the whole message still matches instead of falling through to 'other'.
const STATE_CHANGE_RE   = /^State changed from '(\w+)' to '(\w+)'\.$/;
const ASSIGNED_RE       = /^Assigned to '([^']+)'\.$/;
const ASSIGNED_IN_RE    = /Assigned to '([^']+)'\./;
const UNASSIGNED_RE     = /^Unassigned\.$/;
const FIELD_CHANGED_RE  = /^(.+?) changed from '?(.*?)'? to '?(.*?)'?\.$/s;
const FIELD_UPDATED_RE  = /^(.+?) updated\.$/s;
const FIELD_SET_RE      = /^([^:]+): '?(.*?)'?\.$/s;
const FIELD_ADDED_RE    = /^(.+?) added\.$/s;
const CREATED_RE        = /^Created this (.+?) work item\.$/;
/** Legacy pre-per-field creation message ("Work item created." / "Work item created. Assigned to 'X'."). */
const CREATED_LEGACY_RE = /^Work item created\.?/;

type HistoryEntryKind =
  | 'created' | 'state' | 'assign' | 'unassign'
  | 'field-change' | 'field-update' | 'field-set' | 'field-added' | 'other';

/** One row of the unified activity timeline — a history entry enriched with a display kind, read as one sentence per row. */
export interface HistoryTimelineEntry {
  id: string;
  kind: HistoryEntryKind;
  toState?: SprintTaskApiState;
  assigneeName?: string;
  assigneeAvatar?: string;
  fieldLabel?: string;
  oldValue?: string;
  newValue?: string;
  typeLabel?: string;
  message: string;
  authorName: string;
  authorAvatar?: string;
  createdAt: string;
}

/** One calendar-day section of the activity timeline. */
export interface HistoryDayGroup {
  dayLabel: string;
  entries: HistoryTimelineEntry[];
}

/** One node of the "at a glance" state or assignee progress strip. */
export interface ProgressNode {
  label: string;
  badgeClass: string;
  date: string;
}

@Component({
  selector: 'app-sprint-task-detail-dialog',
  standalone: true,
  imports: [FormsModule, NgClass, NgTemplateOutlet, LocalDatePipe],
  templateUrl: './sprint-task-detail-dialog.component.html',
  styleUrl: './sprint-task-detail-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SprintTaskDetailDialogComponent implements OnInit {
  private readonly dialogRef = inject(DIALOG_REF_TOKEN);
  private readonly dialogSvc = inject(DialogService);
  readonly detailSvc          = inject(SprintTaskDetailService);
  readonly board              = inject(SprintBoardService);
  readonly members            = inject(MembersService);
  readonly dateTime           = inject(DateTimeService);
  readonly data               = inject('DIALOG_DATA' as unknown as InjectionToken<SprintTaskDetailDialogData>);

  /** Exposed so the template @switch can compare against enum members. */
  readonly sprintTaskApiType = SprintTaskApiType;
  readonly stateOptions      = SPRINT_TASK_STATE_OPTIONS;
  readonly stateLabel        = SPRINT_TASK_STATE_LABEL;
  readonly stateBadgeClass   = SPRINT_TASK_STATE_BADGE;

  readonly priorities = [
    { value: WorkItemApiPriority.Low,      label: 'Low' },
    { value: WorkItemApiPriority.Medium,   label: 'Medium' },
    { value: WorkItemApiPriority.High,     label: 'High' },
    { value: WorkItemApiPriority.Critical, label: 'Critical' },
  ];

  readonly activeTab    = signal<'details' | 'discussion' | 'history'>('details');
  readonly commentDraft = signal('');

  readonly assigneeMenuOpen = signal(false);
  readonly assigneeSearch   = signal('');

  readonly filteredAssigneeMembers = computed(() => {
    const q = this.assigneeSearch().trim().toLowerCase();
    const members = this.board.capacityMembers();
    return q ? members.filter(m => m.userName.toLowerCase().includes(q)) : members;
  });

  readonly task = computed(() => this.detailSvc.detail());

  readonly typeIconClass = computed(() => {
    const t = this.task();
    return t ? TYPE_ICON_CLASS[t.type] : '';
  });

  /**
   * Unified activity timeline, newest first — each history entry becomes one row read as a
   * sentence ("{author} did X"), with the author's avatar as the single rail marker per row.
   */
  readonly historyTimeline = computed<HistoryTimelineEntry[]>(() => {
    const members = this.board.capacityMembers();
    const avatarFor = (name: string) => members.find(m => m.userName === name)?.userAvatar;

    return this.detailSvc.history().map((h): HistoryTimelineEntry => {
      const base = { id: h.id, message: h.message, authorName: h.authorName, authorAvatar: h.authorAvatar, createdAt: h.createdAt };

      const created = CREATED_RE.exec(h.message);
      if (created) {
        return { ...base, kind: 'created', typeLabel: created[1] };
      }
      if (CREATED_LEGACY_RE.test(h.message)) {
        const assignedIn = ASSIGNED_IN_RE.exec(h.message);
        return { ...base, kind: 'created', assigneeName: assignedIn?.[1], assigneeAvatar: assignedIn ? avatarFor(assignedIn[1]) : undefined };
      }
      const stateMatch = STATE_CHANGE_RE.exec(h.message);
      if (stateMatch) {
        return { ...base, kind: 'state', toState: STATE_NAME_TO_API[stateMatch[2]] };
      }
      const assigned = ASSIGNED_RE.exec(h.message);
      if (assigned) {
        return { ...base, kind: 'assign', assigneeName: assigned[1], assigneeAvatar: avatarFor(assigned[1]) };
      }
      if (UNASSIGNED_RE.test(h.message)) {
        return { ...base, kind: 'unassign' };
      }
      // Order matters: "changed"/"updated"/"added" are more specific than the generic "Field: value." pattern.
      const fieldChanged = FIELD_CHANGED_RE.exec(h.message);
      if (fieldChanged) {
        return { ...base, kind: 'field-change', fieldLabel: fieldChanged[1], oldValue: fieldChanged[2], newValue: fieldChanged[3] };
      }
      const fieldUpdated = FIELD_UPDATED_RE.exec(h.message);
      if (fieldUpdated) {
        return { ...base, kind: 'field-update', fieldLabel: fieldUpdated[1] };
      }
      const fieldAdded = FIELD_ADDED_RE.exec(h.message);
      if (fieldAdded) {
        return { ...base, kind: 'field-added', fieldLabel: fieldAdded[1] };
      }
      const fieldSet = FIELD_SET_RE.exec(h.message);
      if (fieldSet) {
        return { ...base, kind: 'field-set', fieldLabel: fieldSet[1], newValue: fieldSet[2] };
      }
      return { ...base, kind: 'other' };
    });
  });

  /** {@link historyTimeline}, bucketed into calendar-day sections ("Today", "Yesterday", or a date) for display. */
  readonly historyGroups = computed<HistoryDayGroup[]>(() => {
    const groups: HistoryDayGroup[] = [];
    for (const entry of this.historyTimeline()) {
      const dayLabel = this.dateTime.relativeDay(entry.createdAt);
      const lastGroup = groups.at(-1);
      if (lastGroup?.dayLabel === dayLabel) lastGroup.entries.push(entry);
      else groups.push({ dayLabel, entries: [entry] });
    }
    return groups;
  });

  /** "At a glance" state progression — oldest first, one node per transition. */
  readonly stateProgress = computed<ProgressNode[]>(() => {
    const nodes: ProgressNode[] = [];
    for (const e of [...this.historyTimeline()].reverse()) {
      if (e.kind === 'created') {
        nodes.push({ label: this.stateLabel[SprintTaskApiState.New], badgeClass: this.stateBadgeClass[SprintTaskApiState.New], date: e.createdAt });
      } else if (e.kind === 'state' && e.toState !== undefined) {
        nodes.push({ label: this.stateLabel[e.toState], badgeClass: this.stateBadgeClass[e.toState], date: e.createdAt });
      }
    }
    if (nodes.length === 0) {
      const t = this.task();
      if (t) nodes.push({ label: this.stateLabel[t.state], badgeClass: this.stateBadgeClass[t.state], date: '' });
    }
    return nodes;
  });

  /** "At a glance" assignee progression — oldest first, one node per (re)assignment. */
  readonly assigneeProgress = computed<ProgressNode[]>(() => {
    const nodes: ProgressNode[] = [];
    for (const e of [...this.historyTimeline()].reverse()) {
      if (e.kind === 'created' || e.kind === 'assign') {
        nodes.push({
          label: e.assigneeName ?? 'Unassigned',
          badgeClass: e.assigneeName ? (e.assigneeAvatar ?? 'bg-sky-600') : 'border-2 border-dashed border-slate-600 bg-transparent',
          date: e.createdAt,
        });
      } else if (e.kind === 'unassign') {
        nodes.push({ label: 'Unassigned', badgeClass: 'border-2 border-dashed border-slate-600 bg-transparent', date: e.createdAt });
      }
    }
    if (nodes.length === 0) {
      const t = this.task();
      nodes.push({
        label: t?.assignedToName ?? 'Unassigned',
        badgeClass: t?.assignedToName ? 'bg-sky-600' : 'border-2 border-dashed border-slate-600 bg-transparent',
        date: '',
      });
    }
    return nodes;
  });

  readonly isBug      = computed(() => this.task()?.type === SprintTaskApiType.Bug);
  readonly isTestPlan = computed(() => this.task()?.type === SprintTaskApiType.TestPlan);
  readonly isUserStory = computed(() => this.task()?.type === SprintTaskApiType.UserStory);

  /** Task and Bug share unit-test / design-review notes. */
  readonly showDevNotes = computed(() => {
    const t = this.task()?.type;
    return t === SprintTaskApiType.Bug || t === SprintTaskApiType.Task;
  });

  /** Original estimate applies to Task and Bug only. */
  readonly showEstimate = computed(() => !this.isTestPlan() && !this.isUserStory());

  // ── Editable fields, seeded once from the loaded task ──────────────────────
  readonly title            = signal('');
  readonly description      = signal('');
  readonly priority         = signal(WorkItemApiPriority.Medium);
  readonly storyPoints      = signal(0);
  readonly originalEstimate = signal(0);
  readonly stepsToReproduce = signal('');
  readonly environment      = signal('');
  readonly rootCause        = signal('');
  readonly solution         = signal('');
  readonly impaction        = signal('');
  readonly unitTest         = signal('');
  readonly designReview     = signal('');
  readonly testSteps        = signal<string[]>(['']);
  readonly automated        = signal(false);

  // ── Parent (User Story) picker ───────────────────────────────────────────
  readonly parentSearchTerm   = signal('');
  readonly parentResults      = signal<WorkItemPickerApiDto[]>([]);
  readonly parentSearching    = signal(false);
  readonly parentDropdownRect = signal<{ top: number; left: number; width: number } | null>(null);
  readonly selectedParent     = signal<WorkItemPickerApiDto | null>(null);

  private readonly parentSearch$ = new Subject<string>();

  private readonly _seeded   = signal(false);
  private readonly _snapshot = signal('');

  readonly payload = computed<UpdateSprintTaskPayload>(() => ({
    title:            this.title(),
    description:      this.description(),
    priority:         this.priority(),
    assignedToId:     this.task()?.assignedToId ?? null,
    storyPoints:      this.storyPoints(),
    originalEstimate: this.originalEstimate(),
    stepsToReproduce: this.isBug() ? (this.stepsToReproduce().trim() || null) : null,
    environment:      this.isBug() ? (this.environment().trim() || null)      : null,
    rootCause:        this.isBug() ? (this.rootCause().trim() || null)        : null,
    solution:         this.isBug() ? (this.solution().trim() || null)         : null,
    impaction:        this.isBug() ? (this.impaction().trim() || null)        : null,
    unitTest:         this.showDevNotes() ? (this.unitTest().trim() || null)     : null,
    designReview:     this.showDevNotes() ? (this.designReview().trim() || null) : null,
    testSteps:        this.isTestPlan() ? this.testSteps().map(s => s.trim()).filter(Boolean) : null,
    automated:        this.isTestPlan() ? this.automated() : null,
    parentId:         this.selectedParent()?.id ?? null,
  }));

  readonly isDirty = computed(() => JSON.stringify(this.payload()) !== this._snapshot());
  readonly canSave = computed(() => this.isDirty() && this.title().trim().length > 0);

  constructor() {
    // Seed editable signals exactly once, when the task first loads.
    effect(() => {
      const t = this.task();
      if (!t || this._seeded()) return;

      this.title.set(t.title);
      this.description.set(t.description);
      this.priority.set(t.priority);
      this.storyPoints.set(t.storyPoints);
      this.originalEstimate.set(t.originalEstimate);
      this.stepsToReproduce.set(t.stepsToReproduce ?? '');
      this.environment.set(t.environment ?? '');
      this.rootCause.set(t.rootCause ?? '');
      this.solution.set(t.solution ?? '');
      this.impaction.set(t.impaction ?? '');
      this.unitTest.set(t.unitTest ?? '');
      this.designReview.set(t.designReview ?? '');
      this.testSteps.set(t.testSteps && t.testSteps.length > 0 ? [...t.testSteps] : ['']);
      this.automated.set(t.automated ?? false);
      this.selectedParent.set(
        t.parentId ? { id: t.parentId, workItemNumber: t.parentWorkItemNumber ?? '', title: t.parentTitle ?? '' } : null,
      );

      this._seeded.set(true);
      this._snapshot.set(JSON.stringify(this.payload()));
    });

    this.parentSearch$.pipe(
      debounceTime(500),
      distinctUntilChanged(),
      switchMap(term => {
        if (term.length < 2) {
          this.parentResults.set([]);
          this.parentSearching.set(false);
          return [];
        }
        return this.board.searchParentStories(term);
      }),
      takeUntilDestroyed(),
    ).subscribe({
      next:  results => { this.parentResults.set(results); this.parentSearching.set(false); },
      error: ()      => { this.parentResults.set([]); this.parentSearching.set(false); },
    });
  }

  ngOnInit(): void {
    this.detailSvc.loadDetail(this.data.taskId);
  }

  /** Saves all edited fields (state and assignee are saved separately, immediately). */
  save(): void {
    if (!this.canSave()) return;
    const parent = this.selectedParent();
    this.detailSvc.updateTask(this.data.taskId, this.payload(), parent ? { workItemNumber: parent.workItemNumber, title: parent.title } : null).subscribe({
      next: () => this._snapshot.set(JSON.stringify(this.payload())),
    });
  }

  /**
   * Feeds a new search term into the debounced parent-story search and positions the floating dropdown.
   * @param term Raw input value.
   * @param el The input element from the DOM event.
   */
  onParentSearch(term: string, el: HTMLInputElement): void {
    this.parentSearchTerm.set(term);
    const trimmed = term.trim();
    if (trimmed.length >= 2) {
      this.parentSearching.set(true);
      const r = el.getBoundingClientRect();
      this.parentDropdownRect.set({ top: r.bottom + 4, left: r.left, width: r.width });
    } else {
      this.parentResults.set([]);
      this.parentSearching.set(false);
      this.parentDropdownRect.set(null);
    }
    this.parentSearch$.next(trimmed);
  }

  /** Selects a User Story as the parent and closes the dropdown. @param item The picked story. */
  selectParent(item: WorkItemPickerApiDto): void {
    this.selectedParent.set(item);
    this.parentResults.set([]);
    this.parentSearchTerm.set('');
    this.parentDropdownRect.set(null);
  }

  /** Clears the selected parent so a new search can be started. */
  clearParent(): void {
    this.selectedParent.set(null);
    this.parentSearchTerm.set('');
  }

  /** Closes this dialog and opens the currently-saved parent User Story in its place. */
  openParent(): void {
    const t = this.task();
    if (!t?.parentId) return;
    this.dialogRef.close();
    this.dialogSvc.open(SprintTaskDetailDialogComponent, {
      title: t.parentWorkItemNumber ?? 'Parent',
      width: '44rem',
      data:  { taskId: t.parentId },
    });
  }

  /** Appends a blank test step row. */
  addTestStep(): void {
    this.testSteps.update(steps => [...steps, '']);
  }

  /** Removes the test step at the given index. @param index Row to remove. */
  removeTestStep(index: number): void {
    this.testSteps.update(steps => steps.filter((_, i) => i !== index));
  }

  /** Updates the text of a test step row. @param index Row index. @param value New step text. */
  updateTestStep(index: number, value: string): void {
    this.testSteps.update(steps => steps.map((s, i) => (i === index ? value : s)));
  }

  /** Changes the task's state immediately. @param value Selected state value from the dropdown. */
  onStateChange(value: string): void {
    const t = this.task();
    if (!t?.sprintId) return;
    this.detailSvc.changeState(this.data.taskId, t.sprintId, +value);
  }

  /** Opens or closes the searchable assignee picker. */
  toggleAssigneeMenu(): void {
    this.assigneeMenuOpen.update(v => !v);
    this.assigneeSearch.set('');
  }

  /** Reassigns the task immediately and closes the picker. @param userId Selected user ID, or null to unassign. */
  selectAssignee(userId: string | null): void {
    const t = this.task();
    this.assigneeMenuOpen.set(false);
    if (!t?.sprintId) return;
    this.detailSvc.changeAssignee(this.data.taskId, t.sprintId, userId);
  }

  /** Posts the drafted comment and clears the input. */
  postComment(): void {
    if (!this.commentDraft().trim()) return;
    this.detailSvc.postComment(this.data.taskId, this.commentDraft());
    this.commentDraft.set('');
  }

  /** Closes the dialog. */
  close(): void {
    this.dialogRef.close();
  }
}
