import { ChangeDetectionStrategy, Component, computed, effect, inject, OnInit, signal } from '@angular/core';
import { NgClass, NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { LocalDatePipe } from '../../core/pipes/local-date.pipe';
import { AutoResizeDirective } from '../../directives/auto-resize.directive';
import { WorkItemMetadataEditorComponent } from '../../components/work-item-metadata-editor/work-item-metadata-editor.component';
import { UserSelectComponent, UserOption } from '../../components/user-select/user-select.component';
import { DateTimeService } from '../../core/services/date-time.service';
import { SprintTaskDetailService } from '../../services/sprint-task-detail.service';
import { SprintBoardService } from '../../services/sprint-board.service';
import { MembersService } from '../../services/members.service';
import { SprintTaskApiState, SprintTaskApiType, WorkItemApiPriority } from '../../core/enums/system.enum';
import { SPRINT_TASK_STATE_BADGE, SPRINT_TASK_STATE_LABEL, SPRINT_TASK_STATE_OPTIONS } from '../../core/constants/system.constant';
import type { UpdateSprintTaskPayload } from '../../models/sprint-task-detail.model';
import type { WorkItemPickerApiDto } from '../../models/sprint-planning-api.model';

// ── Type icon styling ────────────────────────────────────────────────────────
const TYPE_ICON_CLASS: Record<SprintTaskApiType, string> = {
  [SprintTaskApiType.UserStory]: 'text-type-story ring-slate-800 bg-slate-800',
  [SprintTaskApiType.Task]:      'text-type-task ring-slate-800 bg-slate-800',
  [SprintTaskApiType.Bug]:       'text-type-bug ring-slate-800 bg-slate-800',
  [SprintTaskApiType.TestPlan]:  'text-type-test ring-slate-800 bg-slate-800',
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

const STATE_CHANGE_RE   = /^State changed from '(\w+)' to '(\w+)'\.$/;
const ASSIGNED_RE       = /^Assigned to '([^']+)'\.$/;
const ASSIGNED_IN_RE    = /Assigned to '([^']+)'\./;
const UNASSIGNED_RE     = /^Unassigned\.$/;
const FIELD_CHANGED_RE  = /^(.+?) changed from '?(.*?)'? to '?(.*?)'?\.$/s;
const FIELD_UPDATED_RE  = /^(.+?) updated\.$/s;
const FIELD_SET_RE      = /^([^:]+): '?(.*?)'?\.$/s;
const FIELD_ADDED_RE    = /^(.+?) added\.$/s;
const CREATED_RE        = /^Created this (.+?) work item\.$/;
const CREATED_LEGACY_RE = /^Work item created\.?/;

type HistoryEntryKind =
  | 'created' | 'state' | 'assign' | 'unassign'
  | 'field-change' | 'field-update' | 'field-set' | 'field-added' | 'other';

/** One row of the unified activity timeline. */
interface HistoryTimelineEntry {
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
interface HistoryDayGroup {
  dayLabel: string;
  entries: HistoryTimelineEntry[];
}

/** One node of the "at a glance" state / assignee progress strip. */
interface ProgressNode {
  label: string;
  badgeClass: string;
  date: string;
}

/**
 * Full-page work-item detail view, mounted at `/{orgAlias}/{repoCode}/boards/{itemKey}`.
 * Provides the same editing capabilities as the former dialog, but the kanban sidebar stays
 * visible because this page renders in the shell's existing router-outlet.
 */
@Component({
  selector: 'app-work-item-detail-page',
  standalone: true,
  imports: [FormsModule, NgClass, NgTemplateOutlet, LocalDatePipe, AutoResizeDirective, WorkItemMetadataEditorComponent, UserSelectComponent],
  templateUrl: './work-item-detail-page.component.html',
  styleUrl: './work-item-detail-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'min-h-0 flex-1 flex flex-col overflow-auto' },
})
export class WorkItemDetailPageComponent implements OnInit {
  private readonly route    = inject(ActivatedRoute);
  private readonly router   = inject(Router);
  readonly detailSvc        = inject(SprintTaskDetailService);
  readonly board            = inject(SprintBoardService);
  readonly members          = inject(MembersService);
  readonly dateTime         = inject(DateTimeService);

  // ── Exposed constants for the template ──────────────────────────────────────
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

  // ── Route param ─────────────────────────────────────────────────────────────
  /** Work item number from the URL — e.g. "DASH-22". */
  readonly itemKey = this.route.snapshot.paramMap.get('itemKey') ?? '';

  // ── Resolution state ─────────────────────────────────────────────────────────
  /**
   * UUID of the resolved work item — null until resolved via the by-key API.
   */
  readonly taskId    = signal<string | null>(null);
  /** True while we are waiting for the key→UUID resolution to complete. */
  readonly resolving = signal(true);
  /** True when the key could not be resolved (task does not exist or access denied). */
  readonly notFound  = signal(false);

  // ── Convenience computed (non-nullable alias used in the template) ──────────
  /** Resolved UUID. Non-empty string once `resolving` is false and `notFound` is false. */
  readonly resolvedTaskId = computed(() => this.taskId() ?? '');

  // ── Tabs / comment draft ─────────────────────────────────────────────────────
  readonly activeTab    = signal<'details' | 'discussion' | 'history'>('details');
  readonly commentDraft = signal('');

  // ── Assignee picker ──────────────────────────────────────────────────────────

  /** Capacity members mapped to the normalised UserOption shape for <app-user-select>. */
  readonly assigneeOptions = computed<UserOption[]>(() =>
    this.board.capacityMembers().map(m => ({ id: m.userId, name: m.userName, avatarClass: m.userAvatar })),
  );

  // ── Task derived computeds ───────────────────────────────────────────────────
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
    const members   = this.board.capacityMembers();
    const avatarFor = (name: string) => members.find(m => m.userName === name)?.userAvatar;

    return this.detailSvc.history().map((h): HistoryTimelineEntry => {
      const base = { id: h.id, message: h.message, authorName: h.authorName, authorAvatar: h.authorAvatar, createdAt: h.createdAt };

      const created = CREATED_RE.exec(h.message);
      if (created) return { ...base, kind: 'created', typeLabel: created[1] };

      if (CREATED_LEGACY_RE.test(h.message)) {
        const assignedIn = ASSIGNED_IN_RE.exec(h.message);
        return { ...base, kind: 'created', assigneeName: assignedIn?.[1], assigneeAvatar: assignedIn ? avatarFor(assignedIn[1]) : undefined };
      }
      const stateMatch = STATE_CHANGE_RE.exec(h.message);
      if (stateMatch) return { ...base, kind: 'state', toState: STATE_NAME_TO_API[stateMatch[2]] };

      const assigned = ASSIGNED_RE.exec(h.message);
      if (assigned) return { ...base, kind: 'assign', assigneeName: assigned[1], assigneeAvatar: avatarFor(assigned[1]) };

      if (UNASSIGNED_RE.test(h.message)) return { ...base, kind: 'unassign' };

      const fieldChanged = FIELD_CHANGED_RE.exec(h.message);
      if (fieldChanged) return { ...base, kind: 'field-change', fieldLabel: fieldChanged[1], oldValue: fieldChanged[2], newValue: fieldChanged[3] };

      const fieldUpdated = FIELD_UPDATED_RE.exec(h.message);
      if (fieldUpdated) return { ...base, kind: 'field-update', fieldLabel: fieldUpdated[1] };

      const fieldAdded = FIELD_ADDED_RE.exec(h.message);
      if (fieldAdded) return { ...base, kind: 'field-added', fieldLabel: fieldAdded[1] };

      const fieldSet = FIELD_SET_RE.exec(h.message);
      if (fieldSet) return { ...base, kind: 'field-set', fieldLabel: fieldSet[1], newValue: fieldSet[2] };

      return { ...base, kind: 'other' };
    });
  });

  /** {@link historyTimeline}, bucketed into calendar-day sections for display. */
  readonly historyGroups = computed<HistoryDayGroup[]>(() => {
    const groups: HistoryDayGroup[] = [];
    for (const entry of this.historyTimeline()) {
      const dayLabel   = this.dateTime.relativeDay(entry.createdAt);
      const lastGroup  = groups.at(-1);
      if (lastGroup?.dayLabel === dayLabel) lastGroup.entries.push(entry);
      else groups.push({ dayLabel, entries: [entry] });
    }
    return groups;
  });

  /** "At a glance" state progression — oldest first. */
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

  /** "At a glance" assignee progression — oldest first. */
  readonly assigneeProgress = computed<ProgressNode[]>(() => {
    const nodes: ProgressNode[] = [];
    for (const e of [...this.historyTimeline()].reverse()) {
      if (e.kind === 'created' || e.kind === 'assign') {
        nodes.push({
          label:      e.assigneeName ?? 'Unassigned',
          badgeClass: e.assigneeName ? (e.assigneeAvatar ?? 'bg-sky-600') : 'border-2 border-dashed border-slate-600 bg-transparent',
          date:       e.createdAt,
        });
      } else if (e.kind === 'unassign') {
        nodes.push({ label: 'Unassigned', badgeClass: 'border-2 border-dashed border-slate-600 bg-transparent', date: e.createdAt });
      }
    }
    if (nodes.length === 0) {
      const t = this.task();
      nodes.push({
        label:      t?.assignedToName ?? 'Unassigned',
        badgeClass: t?.assignedToName ? 'bg-sky-600' : 'border-2 border-dashed border-slate-600 bg-transparent',
        date:       '',
      });
    }
    return nodes;
  });

  readonly isBug       = computed(() => this.task()?.type === SprintTaskApiType.Bug);
  readonly isTestPlan  = computed(() => this.task()?.type === SprintTaskApiType.TestPlan);
  readonly isUserStory = computed(() => this.task()?.type === SprintTaskApiType.UserStory);

  /** Percentage of estimated work that has been completed (estimate − remaining), clamped 0–100. */
  readonly workPct = computed(() => {
    const est = this.originalEstimate();
    if (est <= 0) return 0;
    return Math.min(100, Math.round((this.completedWorkDisplay() / est) * 100));
  });

  /** Task and Bug share unit-test / design-review notes. */
  readonly showDevNotes = computed(() => {
    const t = this.task()?.type;
    return t === SprintTaskApiType.Bug || t === SprintTaskApiType.Task;
  });

  /** Original estimate applies to Task and Bug only. */
  readonly showEstimate = computed(() => !this.isTestPlan() && !this.isUserStory());

  // ── Editable fields ──────────────────────────────────────────────────────────
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
  readonly testSteps              = signal<string[]>(['']);
  readonly automated              = signal(false);
  readonly acceptanceCriteria = signal<string[]>([]);  // one item per row (mirrors backlog AC)
  readonly documents              = signal<string[]>([]);
  readonly editingDocIndex        = signal<number | null>(null);
  readonly remainingWork          = signal(0);    // editable for Task/Bug

  /** Completed work hours, derived as max(0, estimate − remaining). */
  readonly completedWorkDisplay = computed(() =>
    Math.max(0, this.originalEstimate() - this.remainingWork()),
  );

  // ── Parent (User Story) picker ───────────────────────────────────────────────
  readonly parentSearchTerm   = signal('');
  readonly parentResults      = signal<WorkItemPickerApiDto[]>([]);
  readonly parentSearching    = signal(false);
  readonly parentDropdownRect = signal<{ top: number; left: number; width: number } | null>(null);
  readonly selectedParent     = signal<WorkItemPickerApiDto | null>(null);

  private readonly parentSearch$ = new Subject<string>();

  private readonly _seeded   = signal(false);
  private readonly _snapshot = signal('');

  readonly payload = computed<UpdateSprintTaskPayload>(() => {
    const isUS  = this.isUserStory();
    const acItems = isUS ? this.acceptanceCriteria().map(l => l.trim()).filter(Boolean) : [];
    return {
      title:              this.title(),
      description:        this.description(),
      priority:           this.priority(),
      assignedToId:       this.task()?.assignedToId ?? null,
      storyPoints:        this.storyPoints(),
      originalEstimate:   this.originalEstimate(),
      stepsToReproduce:   this.isBug() ? (this.stepsToReproduce().trim() || null) : null,
      environment:        this.isBug() ? (this.environment().trim() || null)      : null,
      rootCause:          this.isBug() ? (this.rootCause().trim() || null)        : null,
      solution:           this.isBug() ? (this.solution().trim() || null)         : null,
      impaction:          this.isBug() ? (this.impaction().trim() || null)        : null,
      unitTest:           this.showDevNotes() ? (this.unitTest().trim() || null)     : null,
      designReview:       this.showDevNotes() ? (this.designReview().trim() || null) : null,
      testSteps:          this.isTestPlan() ? this.testSteps().map(s => s.trim()).filter(Boolean) : null,
      automated:          this.isTestPlan() ? this.automated() : null,
      parentId:           this.selectedParent()?.id ?? null,
      acceptanceCriteria: isUS && acItems.length > 0 ? JSON.stringify(acItems) : null,
      documents:          isUS ? this.documents().filter(Boolean) : null,
      remainingWork:      this.showEstimate() ? this.remainingWork() : 0,
    };
  });

  readonly isDirty = computed(() => JSON.stringify(this.payload()) !== this._snapshot());
  readonly canSave = computed(() => this.isDirty() && this.title().trim().length > 0);

  constructor() {
    // Resolve itemKey → taskId via the backend by-key endpoint.
    // This works for tasks in any sprint (not just the active board sprint).
    this.detailSvc.resolveKey(this.itemKey)
      .pipe(takeUntilDestroyed())
      .subscribe({
        next: (taskId) => {
          this.taskId.set(taskId);
          this.detailSvc.loadDetail(taskId);
          this.resolving.set(false);
        },
        error: () => {
          this.resolving.set(false);
          this.notFound.set(true);
        },
      });

    // Seed editable signals exactly once when the task first loads.
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
      this.remainingWork.set(t.remainingWork);

      // Acceptance criteria: stored as a JSON array string or plain text — parse into array.
      const acRaw = t.acceptanceCriteria ?? '';
      let acArr: string[] = [];
      try {
        const parsed = JSON.parse(acRaw);
        if (Array.isArray(parsed)) acArr = (parsed as string[]).filter(Boolean);
      } catch { acArr = acRaw ? acRaw.split('\n').filter(l => l.trim()) : []; }
      this.acceptanceCriteria.set(acArr);
      this.documents.set(t.documents && t.documents.length > 0 ? [...t.documents] : []);

      this.selectedParent.set(
        t.parentId ? { id: t.parentId, workItemNumber: t.parentWorkItemNumber ?? '', title: t.parentTitle ?? '' } : null,
      );
      this._seeded.set(true);
      this._snapshot.set(JSON.stringify(this.payload()));
    }, { allowSignalWrites: true });

    // Clamp remainingWork whenever originalEstimate shrinks below it.
    effect(() => {
      const est = this.originalEstimate();
      if (this.remainingWork() > est) this.remainingWork.set(est);
    }, { allowSignalWrites: true });

    // Parent-story search: debounced, switches to cancel in-flight requests.
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
    // Board items may already be loaded (common: user clicked a card from the board).
    // The effect above handles both the "already loaded" and "loading" cases.
  }

  // ── Persistence ──────────────────────────────────────────────────────────────

  /** Saves all edited fields; state and assignee are saved separately, immediately. */
  save(): void {
    const id = this.taskId();
    if (!this.canSave() || !id) return;
    const parent = this.selectedParent();
    this.detailSvc.updateTask(id, this.payload(), parent ? { workItemNumber: parent.workItemNumber, title: parent.title } : null).subscribe({
      next: () => this._snapshot.set(JSON.stringify(this.payload())),
    });
  }

  /** Changes the task's state immediately. @param value Selected state API value. */
  onStateChange(value: string): void {
    const id = this.taskId();
    const t  = this.task();
    if (!id || !t?.sprintId) return;
    this.detailSvc.changeState(id, t.sprintId, +value);
  }

  // ── Assignee ─────────────────────────────────────────────────────────────────

  /** Reassigns the task immediately. Called from <app-user-select> (selected) output. @param userId New assignee, or null to unassign. */
  selectAssignee(userId: string | null): void {
    const id = this.taskId();
    const t  = this.task();
    if (!id || !t?.sprintId) return;
    this.detailSvc.changeAssignee(id, t.sprintId, userId);
  }

  // ── Discussion ───────────────────────────────────────────────────────────────

  /** Posts the drafted comment and clears the input. */
  postComment(): void {
    const id = this.taskId();
    if (!id || !this.commentDraft().trim()) return;
    this.detailSvc.postComment(id, this.commentDraft());
    this.commentDraft.set('');
  }

  // ── Test steps ───────────────────────────────────────────────────────────────

  /** Appends a blank test step row. */
  addTestStep(): void { this.testSteps.update(steps => [...steps, '']); }

  /** Removes the test step at the given index. @param index Row to remove. */
  removeTestStep(index: number): void {
    this.testSteps.update(steps => steps.filter((_, i) => i !== index));
  }

  /** Updates the text of a test step row. @param index Row index. @param value New step text. */
  updateTestStep(index: number, value: string): void {
    this.testSteps.update(steps => steps.map((s, i) => (i === index ? value : s)));
  }

  // ── Documents (UserStory only) ────────────────────────────────────────────────

  /**
   * Sets remaining work, clamped to [0, currentEstimate].
   * Called from the Remaining input so it can never exceed the estimate.
   * @param value Raw numeric input value.
   */
  setRemainingWork(value: number): void {
    this.remainingWork.set(Math.min(Math.max(0, value), this.originalEstimate()));
  }

  /** Appends a blank document row and opens it in edit mode. */
  addDocument(): void {
    this.documents.update(d => [...d, '']);
    const newIdx = this.documents().length - 1;
    this.editingDocIndex.set(newIdx);
    setTimeout(() => document.getElementById(`doc-edit-${newIdx}`)?.focus(), 0);
  }

  /** Removes the document at the given index. @param index Row to remove. */
  removeDocument(index: number): void {
    this.documents.update(d => d.filter((_, i) => i !== index));
    if (this.editingDocIndex() === index) this.editingDocIndex.set(null);
  }

  /** Updates a document URL at the given index. @param index Row index. @param value New URL or label. */
  updateDocument(index: number, value: string): void {
    this.documents.update(d => d.map((v, i) => (i === index ? value : v)));
  }

  /** Opens the document at the given index in inline edit mode. @param index Row to edit. */
  editDoc(index: number): void {
    this.editingDocIndex.set(index);
    setTimeout(() => document.getElementById(`doc-edit-${index}`)?.focus(), 0);
  }

  /** Commits the current document edit and returns to view mode. */
  commitDocEdit(): void { this.editingDocIndex.set(null); }

  /**
   * Returns a short human-readable label for a document value.
   * @param value URL or document name.
   * @returns Filename extracted from URL, or raw value if not a URL.
   */
  docLabel(value: string): string {
    try {
      const u = new URL(value);
      const parts = u.pathname.split('/').filter(Boolean);
      const last  = parts[parts.length - 1];
      return last ? decodeURIComponent(last) : u.hostname;
    } catch { return value; }
  }

  // ── Acceptance Criteria helpers (UserStory only) ─────────────────────────────

  /** Appends a blank acceptance-criteria row. */
  addAcItem(): void { this.acceptanceCriteria.update(rows => [...rows, '']); }

  /** Removes the AC row at the given index. @param index Row to remove. */
  removeAcItem(index: number): void {
    this.acceptanceCriteria.update(rows => rows.filter((_, i) => i !== index));
  }

  /** Updates the AC row at the given index. @param index Row index. @param value New criterion text. */
  updateAcItem(index: number, value: string): void {
    this.acceptanceCriteria.update(rows => rows.map((v, i) => (i === index ? value : v)));
  }

  // ── Parent picker ────────────────────────────────────────────────────────────

  /**
   * Feeds a new search term into the debounced parent-story search and positions the dropdown.
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

  /** Selects a User Story as the parent. @param item The picked story. */
  selectParent(item: WorkItemPickerApiDto): void {
    this.selectedParent.set(item);
    this.parentResults.set([]);
    this.parentSearchTerm.set('');
    this.parentDropdownRect.set(null);
  }

  /** Clears the selected parent. */
  clearParent(): void {
    this.selectedParent.set(null);
    this.parentSearchTerm.set('');
  }

  /**
   * Navigates to the parent User Story's detail page.
   * Uses the parent's workItemNumber to build the sibling route.
   */
  openParent(): void {
    const t = this.task();
    if (!t?.parentWorkItemNumber) return;
    this.router.navigate(['..', t.parentWorkItemNumber], { relativeTo: this.route });
  }

  // ── Navigation ───────────────────────────────────────────────────────────────

  /** Returns to the board. */
  goBack(): void {
    this.router.navigate(['..'], { relativeTo: this.route });
  }
}
