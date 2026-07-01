import { ChangeDetectionStrategy, Component, computed, effect, inject, InjectionToken, OnInit, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalDatePipe } from '../../core/pipes/local-date.pipe';
import { DIALOG_REF_TOKEN } from '../../models/dialog.model';
import { SprintTaskDetailService } from '../../services/sprint-task-detail.service';
import { SprintBoardService } from '../../services/sprint-board.service';
import { MembersService } from '../../services/members.service';
import { SprintTaskApiState, SprintTaskApiType, WorkItemApiPriority } from '../../core/enums/system.enum';
import { SPRINT_TASK_STATE_BADGE, SPRINT_TASK_STATE_LABEL, SPRINT_TASK_STATE_OPTIONS } from '../../core/constants/system.constant';
import type { UpdateSprintTaskPayload } from '../../models/sprint-task-detail.model';

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

const STATE_CHANGE_RE = /^State changed from '(\w+)' to '(\w+)'\.$/;
const ASSIGNED_RE     = /^Assigned to '([^']+)'\.$/;
const UNASSIGNED_RE   = /^Unassigned\.$/;

export interface StateTimelineEntry {
  state: SprintTaskApiState;
  authorName?: string;
  createdAt?: string;
}

export interface AssigneeTimelineEntry {
  assigneeName: string | null;
  authorName?: string;
  createdAt?: string;
}

@Component({
  selector: 'app-sprint-task-detail-dialog',
  standalone: true,
  imports: [FormsModule, NgClass, LocalDatePipe],
  templateUrl: './sprint-task-detail-dialog.component.html',
  styleUrl: './sprint-task-detail-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SprintTaskDetailDialogComponent implements OnInit {
  private readonly dialogRef = inject(DIALOG_REF_TOKEN);
  readonly detailSvc          = inject(SprintTaskDetailService);
  readonly board              = inject(SprintBoardService);
  readonly members            = inject(MembersService);
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

  /** Horizontal state progression, oldest first. Parsed from history messages; falls back to the current state when the task never transitioned. */
  readonly stateTimeline = computed<StateTimelineEntry[]>(() => {
    const history = [...this.detailSvc.history()].reverse();
    const entries: StateTimelineEntry[] = [];

    for (const h of history) {
      const m = STATE_CHANGE_RE.exec(h.message);
      if (!m) continue;
      if (entries.length === 0) entries.push({ state: STATE_NAME_TO_API[m[1]] });
      entries.push({ state: STATE_NAME_TO_API[m[2]], authorName: h.authorName, createdAt: h.createdAt });
    }

    if (entries.length === 0) {
      const t = this.task();
      if (t) entries.push({ state: t.state });
    }
    return entries;
  });

  /** Horizontal assignee progression, oldest first. Parsed from history messages. */
  readonly assigneeTimeline = computed<AssigneeTimelineEntry[]>(() => {
    const history = [...this.detailSvc.history()].reverse();
    const entries: AssigneeTimelineEntry[] = [];

    for (const h of history) {
      if (h.message.startsWith('Work item created')) {
        const m = ASSIGNED_RE.exec(h.message) ?? /Assigned to '([^']+)'\./.exec(h.message);
        entries.push({ assigneeName: m ? m[1] : null });
        continue;
      }
      const assigned = ASSIGNED_RE.exec(h.message);
      if (assigned) {
        entries.push({ assigneeName: assigned[1], authorName: h.authorName, createdAt: h.createdAt });
        continue;
      }
      if (UNASSIGNED_RE.test(h.message))
        entries.push({ assigneeName: null, authorName: h.authorName, createdAt: h.createdAt });
    }

    if (entries.length === 0) {
      const t = this.task();
      entries.push({ assigneeName: t?.assignedToName ?? null });
    }
    return entries;
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

      this._seeded.set(true);
      this._snapshot.set(JSON.stringify(this.payload()));
    });
  }

  ngOnInit(): void {
    this.detailSvc.loadDetail(this.data.taskId);
  }

  /** Saves all edited fields (state and assignee are saved separately, immediately). */
  save(): void {
    if (!this.canSave()) return;
    this.detailSvc.updateTask(this.data.taskId, this.payload()).subscribe({
      next: () => this._snapshot.set(JSON.stringify(this.payload())),
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
