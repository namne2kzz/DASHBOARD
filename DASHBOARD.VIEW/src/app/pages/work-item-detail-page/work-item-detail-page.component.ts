import { ChangeDetectionStrategy, Component, computed, effect, inject, OnInit, signal } from '@angular/core';
import { NgClass, NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, map, switchMap, tap } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { LocalDatePipe } from '../../core/pipes/local-date.pipe';
import { AutoResizeDirective } from '../../directives/auto-resize.directive';
import { WorkItemMetadataEditorComponent } from '../../components/work-item-metadata-editor/work-item-metadata-editor.component';
import { UserSelectComponent, UserOption } from '../../components/user-select/user-select.component';
import { DateTimeService } from '../../core/services/date-time.service';
import { SprintTaskDetailService } from '../../services/sprint-task-detail.service';
import { SprintBoardService } from '../../services/sprint-board.service';
import { MembersService } from '../../services/members.service';
import { RepositoryContextService } from '../../services/repository-context.service';
import { PrivilegeService } from '../../core/services/privilege.service';
import { AttachmentService } from '../../services/attachment.service';
import { WorkItemLinkService } from '../../services/work-item-link.service';
import { ConfirmService } from '../../core/components/confirm/confirm.service';
import { renderMarkdown } from '../../utils/markdown.util';
import type { AttachmentDto } from '../../models/attachment.model';
import type { WorkItemLinkDto, WorkItemLinkType } from '../../models/work-item-link.model';
import { LINK_TYPE_OPTIONS } from '../../models/work-item-link.model';
import { SprintTaskApiState, SprintTaskApiType, WorkItemApiPriority } from '../../core/enums/system.enum';
import { SPRINT_TASK_STATE_BADGE, SPRINT_TASK_STATE_LABEL, SPRINT_TASK_STATE_OPTIONS } from '../../core/constants/system.constant';
import type { UpdateSprintTaskPayload } from '../../models/sprint-task-detail.model';
import type { WorkItemPickerApiDto } from '../../models/sprint-planning-api.model';

// ── Type icon styling ────────────────────────────────────────────────────────
const TYPE_DOT_CLASS: Record<SprintTaskApiType, string> = {
  [SprintTaskApiType.UserStory]: 'nx-type--story',
  [SprintTaskApiType.Task]:      'nx-type--task',
  [SprintTaskApiType.Bug]:       'nx-type--bug',
  [SprintTaskApiType.TestPlan]:  'nx-type--test',
};

const TYPE_ICON_CLASS: Record<SprintTaskApiType, string> = {
  [SprintTaskApiType.UserStory]: 'text-type-story ring-slate-800 bg-slate-800',
  [SprintTaskApiType.Task]:      'text-type-task ring-slate-800 bg-slate-800',
  [SprintTaskApiType.Bug]:       'text-type-bug ring-slate-800 bg-slate-800',
  [SprintTaskApiType.TestPlan]:  'text-type-test ring-slate-800 bg-slate-800',
};

/** Maps the C# enum member name embedded in history messages (e.g. "InReview") back to its numeric value. */
const STATE_NAME_TO_API: Record<string, SprintTaskApiState> = {
  Open:       SprintTaskApiState.Open,
  ToDo:       SprintTaskApiState.ToDo,
  InProgress: SprintTaskApiState.InProgress,
  InReview:   SprintTaskApiState.InReview,
  Verified:   SprintTaskApiState.Verified,
  Running:    SprintTaskApiState.Running,
  Done:       SprintTaskApiState.Done,
  Passed:     SprintTaskApiState.Passed,
  Failed:     SprintTaskApiState.Failed,
  Closed:     SprintTaskApiState.Closed,
  // Legacy names (may appear in old history records)
  New:        SprintTaskApiState.Open,
  Backlog:    SprintTaskApiState.ToDo,
  Todo:       SprintTaskApiState.ToDo,
  Active:     SprintTaskApiState.InProgress,
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
  private readonly attachments = inject(AttachmentService);
  private readonly links       = inject(WorkItemLinkService);
  private readonly confirm     = inject(ConfirmService);
  private readonly repoCtx     = inject(RepositoryContextService);
  private readonly privilege   = inject(PrivilegeService);

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
  /** The current item key from the route; updated reactively on navigation between linked items. */
  readonly itemKey = signal(this.route.snapshot.paramMap.get('itemKey') ?? '');

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
  readonly activeTab    = signal<'details' | 'discussion' | 'attachments' | 'history'>('details');
  readonly commentDraft = signal('');

  // ── Attachments tab state ──────────────────────────────────────────────────────
  readonly attachmentList      = signal<AttachmentDto[]>([]);
  readonly attachmentUploading = signal(false);
  readonly attachmentError     = signal<string | null>(null);

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

  /** Maps a work item type to its nx-type dot modifier class. @param type The work item type. */
  typeDot(type: SprintTaskApiType): string {
    return TYPE_DOT_CLASS[type] ?? 'nx-type--task';
  }

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
        nodes.push({ label: this.stateLabel[SprintTaskApiState.Open], badgeClass: this.stateBadgeClass[SprintTaskApiState.Open], date: e.createdAt });
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
  /** True while a pasted/dropped image is uploading. */
  readonly imageUploading   = signal(false);
  /** Rendered Markdown preview of the description (Angular sanitizes the [innerHTML]). */
  readonly renderedDescription = computed(() => renderMarkdown(this.description()));
  /** True when the description contains Markdown worth previewing (image, heading, list, code, link, bold). */
  readonly descriptionHasMarkdown = computed(() =>
    /!\[|\]\(|^#{1,6}\s|^[-*]\s|^\d+\.\s|```|`[^`]+`|\*\*|__/m.test(this.description()),
  );
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

  private readonly _seededId = signal<string | null>(null);
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
  readonly canSave = computed(() =>
    this.canEditWorkItem() && this.isDirty() && this.title().trim().length > 0);

  // Permission gates, mirroring what the handlers enforce: field edits and state changes are
  // EditWorkItem, reassignment is its own AssignWorkItem. Comments and attachments are
  // deliberately absent — the server gates those on membership and authorship, not on a role.

  /** Whether the user may edit this work item's fields or change its state. */
  readonly canEditWorkItem = this.privilege.canEditWorkItem;

  /** Whether the user may change who this work item is assigned to. */
  readonly canAssignWorkItem = this.privilege.canAssignWorkItem;

  constructor() {
    // React to itemKey changes so navigating between linked items (same route, different param)
    // reloads the view instead of reusing the stale component state.
    this.route.paramMap
      .pipe(
        map(p => p.get('itemKey') ?? ''),
        distinctUntilChanged(),
        tap(key => {
          // Reset per-item state before resolving the new key.
          this.itemKey.set(key);
          this.resolving.set(true);
          this.notFound.set(false);
          this.linkList.set([]);
          this.attachmentList.set([]);
        }),
        switchMap(key => this.detailSvc.resolveKey(key)),
        takeUntilDestroyed(),
      )
      .subscribe({
        next: (taskId) => {
          this.taskId.set(taskId);
          this.detailSvc.loadDetail(taskId);
          this.detailSvc.loadMentionCandidates(taskId);
          this.loadLinks();
          this.resolving.set(false);
        },
        error: () => {
          this.resolving.set(false);
          this.notFound.set(true);
        },
      });

    // Seed editable signals whenever a different task's detail loads (re-seeds on navigation
    // between linked items, not just the first load).
    effect(() => {
      const t = this.task();
      if (!t || this._seededId() === t.id) return;

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
      this._seededId.set(t.id);
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
    if (!id || !t?.sprintId || !this.canEditWorkItem()) return;
    this.detailSvc.changeState(id, t.sprintId, +value);
  }

  // ── Assignee ─────────────────────────────────────────────────────────────────

  /** Reassigns the task immediately. Called from <app-user-select> (selected) output. @param userId New assignee, or null to unassign. */
  selectAssignee(userId: string | null): void {
    const id = this.taskId();
    const t  = this.task();
    if (!id || !t?.sprintId || !this.canAssignWorkItem()) return;
    this.detailSvc.changeAssignee(id, t.sprintId, userId);
  }

  // ── Discussion ───────────────────────────────────────────────────────────────

  /** Whether the @mention picker is currently open. */
  readonly mentionOpen = signal(false);
  /** Current text typed after the active "@" (used to filter candidates). */
  readonly mentionQuery = signal('');
  /** Caret index of the "@" that triggered the open picker. */
  private mentionAnchor = -1;
  /** Users picked from the mention dropdown, keyed by display name → userId, for token resolution on post. */
  private readonly pickedMentions = new Map<string, string>();

  /** Candidates filtered by the current @query, capped at 8. */
  readonly mentionMatches = computed(() => {
    const q = this.mentionQuery().toLowerCase();
    return this.detailSvc.mentionCandidates()
      .filter(c => c.name.toLowerCase().includes(q))
      .slice(0, 8);
  });

  /**
   * Re-evaluates the mention picker whenever the comment text or caret changes.
   * Opens when the caret sits in an "@word" token, closes otherwise.
   * @param value Current textarea value.
   * @param caret Current caret position.
   */
  onCommentInput(value: string, caret: number): void {
    this.commentDraft.set(value);

    const uptoCaret = value.slice(0, caret);
    const at = uptoCaret.lastIndexOf('@');
    // Open only when "@" starts a token (preceded by start/space) and no space follows it yet.
    if (at >= 0 && (at === 0 || /\s/.test(uptoCaret[at - 1])) && !/\s/.test(uptoCaret.slice(at + 1))) {
      this.mentionAnchor = at;
      this.mentionQuery.set(uptoCaret.slice(at + 1));
      this.mentionOpen.set(true);
    } else {
      this.mentionOpen.set(false);
    }
  }

  /**
   * Inserts the chosen mention as a plain, readable "@Name " and remembers its userId
   * so it can be resolved to a token when the comment is posted.
   * @param c The selected candidate.
   */
  selectMention(c: { userId: string; name: string }): void {
    const text   = this.commentDraft();
    const anchor = this.mentionAnchor;
    if (anchor < 0) return;

    const before = text.slice(0, anchor);
    const after  = text.slice(anchor + 1 + this.mentionQuery().length);
    this.pickedMentions.set(c.name, c.userId);
    this.commentDraft.set(`${before}@${c.name}${after} `);
    this.mentionOpen.set(false);
    this.mentionAnchor = -1;
  }

  /**
   * Replaces each plain "@Name" the user picked with a `@[Name](userId)` token the backend parses.
   * Longest names first, so "@Anna Lee" is matched before "@Anna".
   * @param text The draft body with plain @Name mentions.
   * @returns The body with resolvable mention tokens.
   */
  private encodeMentions(text: string): string {
    let out = text;
    for (const name of [...this.pickedMentions.keys()].sort((a, b) => b.length - a.length)) {
      const id = this.pickedMentions.get(name)!;
      out = out.split(`@${name}`).join(`@[${name}](${id})`);
    }
    return out;
  }

  /**
   * Renders a stored comment body for display: turns `@[Name](id)` tokens into a highlighted "@Name".
   * @param body Raw comment body.
   * @returns Sanitised HTML string.
   */
  renderComment(body: string): string {
    // Replace mention tokens with a highlight placeholder BEFORE markdown so the parens/brackets
    // in @[Name](id) are not interpreted as a markdown link. Angular sanitizes the final [innerHTML].
    const withMentions = body.replace(
      /@\[([^\]]+)\]\([0-9a-fA-F-]{36}\)/g,
      (_m, name) => `<span class="mention">@${name}</span>`,
    );
    return renderMarkdown(withMentions);
  }

  /** Posts the drafted comment and clears the input. */
  postComment(): void {
    const id = this.taskId();
    if (!id || !this.commentDraft().trim()) return;
    this.detailSvc.postComment(id, this.encodeMentions(this.commentDraft()));
    this.commentDraft.set('');
    this.pickedMentions.clear();
    this.mentionOpen.set(false);
  }

  // ── Inline image paste / drop ─────────────────────────────────────────────────

  /**
   * Handles pasting an image into an editor: uploads it and inserts a Markdown image at the caret.
   * @param e The clipboard event.
   * @param box The textarea the paste happened in.
   * @param needsEdit True for the description, which is part of the work item and so requires
   *   EditWorkItem; false for a comment, which the server gates on membership alone.
   */
  onEditorPaste(e: ClipboardEvent, box: HTMLTextAreaElement, needsEdit = false): void {
    if (needsEdit && !this.canEditWorkItem()) return;
    const file = this._imageFromItems(e.clipboardData?.items);
    if (!file) return;
    e.preventDefault();
    void this._uploadAndInsert(file, box);
  }

  /**
   * Handles dropping an image file onto an editor.
   * @param e The drag event.
   * @param box The textarea the drop happened on.
   * @param needsEdit True for the description; false for a comment. See {@link onEditorPaste}.
   */
  onEditorDrop(e: DragEvent, box: HTMLTextAreaElement, needsEdit = false): void {
    if (needsEdit && !this.canEditWorkItem()) return;
    const file = this._imageFromFiles(e.dataTransfer?.files);
    if (!file) return;
    e.preventDefault();
    void this._uploadAndInsert(file, box);
  }

  /** Finds the first image file among clipboard items. */
  private _imageFromItems(items?: DataTransferItemList): File | null {
    if (!items) return null;
    for (const item of Array.from(items))
      if (item.kind === 'file' && item.type.startsWith('image/'))
        return item.getAsFile();
    return null;
  }

  /** Finds the first image file among dropped files. */
  private _imageFromFiles(files?: FileList): File | null {
    if (!files) return null;
    for (const f of Array.from(files))
      if (f.type.startsWith('image/')) return f;
    return null;
  }

  /** Uploads an image then inserts `![](url)` at the textarea caret, updating the bound signal. */
  private async _uploadAndInsert(file: File, box: HTMLTextAreaElement): Promise<void> {
    const repoId = this.repoCtx.selectedRepoId();
    const taskId = this.taskId();
    if (!repoId || !taskId) return;

    this.imageUploading.set(true);
    try {
      const url     = await this.attachments.uploadInlineImage(repoId, taskId, file);
      const snippet = `![image](${url})`;
      const start   = box.selectionStart ?? box.value.length;
      const end     = box.selectionEnd ?? box.value.length;
      const next    = box.value.slice(0, start) + snippet + box.value.slice(end);
      // Route the update to whichever field this textarea is bound to.
      if (box.placeholder.startsWith('Describe')) this.description.set(next);
      else this.commentDraft.set(next);
    } catch {
      // Upload failed — keep the text as-is.
    } finally {
      this.imageUploading.set(false);
    }
  }

  // ── Attachments tab ─────────────────────────────────────────────────────────

  /** Switches to the Attachments tab and loads the list. */
  openAttachments(): void {
    this.activeTab.set('attachments');
    this._loadAttachments();
  }

  /** Loads the current work item's attachments into the signal. */
  private _loadAttachments(): void {
    const repoId = this.repoCtx.selectedRepoId();
    const taskId = this.taskId();
    if (!repoId || !taskId) return;
    this.attachments.list(repoId, taskId)
      .then(list => this.attachmentList.set(list))
      .catch(() => { /* non-critical */ });
  }

  /** Handles a file chosen via the file input. @param e The change event. */
  onAttachmentPick(e: Event): void {
    const input = e.target as HTMLInputElement;
    const file  = input.files?.[0];
    if (file) void this._uploadAttachment(file);
    input.value = '';
  }

  /** Handles a file dropped onto the drop zone. @param e The drag event. */
  onAttachmentDrop(e: DragEvent): void {
    e.preventDefault();
    const file = e.dataTransfer?.files?.[0];
    if (file) void this._uploadAttachment(file);
  }

  /** Uploads a file attachment and prepends it to the list. */
  private async _uploadAttachment(file: File): Promise<void> {
    const repoId = this.repoCtx.selectedRepoId();
    const taskId = this.taskId();
    if (!repoId || !taskId) return;

    this.attachmentError.set(null);
    this.attachmentUploading.set(true);
    try {
      const created = await this.attachments.upload(repoId, taskId, file);
      this.attachmentList.update(list => [created, ...list]);
    } catch (err) {
      this.attachmentError.set(err instanceof Error ? err.message : 'Upload failed.');
    } finally {
      this.attachmentUploading.set(false);
    }
  }

  /** Confirms then deletes an attachment. @param a The attachment to remove. */
  async deleteAttachment(a: AttachmentDto): Promise<void> {
    const repoId = this.repoCtx.selectedRepoId();
    const taskId = this.taskId();
    if (!repoId || !taskId) return;

    const ok = await this.confirm.ask('Delete attachment?', `"${a.fileName}" will be removed.`, 'danger');
    if (!ok) return;

    try {
      await this.attachments.delete(repoId, taskId, a.id);
      this.attachmentList.update(list => list.filter(x => x.id !== a.id));
    } catch {
      this.attachmentError.set('Could not delete the attachment.');
    }
  }

  /** Formats a byte count as a human-readable size. @param bytes Size in bytes. */
  formatBytes(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  // ── Work item links ─────────────────────────────────────────────────────────

  readonly linkList          = signal<WorkItemLinkDto[]>([]);
  readonly linkDialogOpen    = signal(false);
  readonly linkTypeOptions   = LINK_TYPE_OPTIONS;
  readonly linkDraftType     = signal<WorkItemLinkType>('Related');
  readonly linkDraftDesc     = signal('');
  readonly linkSearchTerm    = signal('');
  readonly linkSearchResults = signal<WorkItemPickerApiDto[]>([]);
  readonly linkSelectedTarget = signal<WorkItemPickerApiDto | null>(null);
  readonly linkSaving        = signal(false);
  readonly linkError         = signal<string | null>(null);

  /** Links grouped by their display label for sectioned rendering. */
  readonly linksByLabel = computed(() => {
    const groups = new Map<string, WorkItemLinkDto[]>();
    for (const l of this.linkList()) {
      const arr = groups.get(l.label) ?? [];
      arr.push(l);
      groups.set(l.label, arr);
    }
    return [...groups.entries()].map(([label, items]) => ({ label, items }));
  });

  /** Loads the current work item's links. */
  loadLinks(): void {
    const repoId = this.repoCtx.selectedRepoId();
    const taskId = this.taskId();
    if (!repoId || !taskId) return;
    this.links.list(repoId, taskId)
      .then(list => this.linkList.set(list))
      .catch(() => { /* non-critical */ });
  }

  /** Opens the add-link dialog with a fresh draft. */
  openLinkDialog(): void {
    this.linkDraftType.set('Related');
    this.linkDraftDesc.set('');
    this.linkSearchTerm.set('');
    this.linkSearchResults.set([]);
    this.linkSelectedTarget.set(null);
    this.linkError.set(null);
    this.linkDialogOpen.set(true);
  }

  /** Closes the add-link dialog. */
  closeLinkDialog(): void { this.linkDialogOpen.set(false); }

  /** Searches for a link target by title/key. @param term Search text. */
  searchLinkTargets(term: string): void {
    this.linkSearchTerm.set(term);
    this.linkSelectedTarget.set(null);
    const repoId = this.repoCtx.selectedRepoId();
    const taskId = this.taskId();
    if (!repoId || !taskId || !term.trim()) { this.linkSearchResults.set([]); return; }
    this.links.searchTargets(repoId, taskId, term.trim())
      .then(r => this.linkSearchResults.set(r))
      .catch(() => this.linkSearchResults.set([]));
  }

  /** Picks a target from the search results. @param t The chosen item. */
  selectLinkTarget(t: WorkItemPickerApiDto): void {
    this.linkSelectedTarget.set(t);
    this.linkSearchTerm.set(`${t.workItemNumber} ${t.title}`);
    this.linkSearchResults.set([]);
  }

  /** Creates the link from the current draft. */
  async saveLink(): Promise<void> {
    const repoId = this.repoCtx.selectedRepoId();
    const taskId = this.taskId();
    const target = this.linkSelectedTarget();
    if (!repoId || !taskId || !target) { this.linkError.set('Pick a work item to link.'); return; }

    this.linkSaving.set(true);
    this.linkError.set(null);
    try {
      const created = await this.links.create(
        repoId, taskId, target.id, this.linkDraftType(),
        this.linkDraftDesc().trim() || null);
      this.linkList.update(list => [...list, created]);
      this.linkDialogOpen.set(false);
    } catch (err: unknown) {
      const e = err as { error?: { error?: string } };
      this.linkError.set(e?.error?.error ?? 'Could not create the link.');
    } finally {
      this.linkSaving.set(false);
    }
  }

  /** Confirms then removes a link. @param l The link to delete. */
  async deleteLink(l: WorkItemLinkDto): Promise<void> {
    const repoId = this.repoCtx.selectedRepoId();
    const taskId = this.taskId();
    if (!repoId || !taskId) return;
    const ok = await this.confirm.ask('Remove link?', `The link to ${l.targetKey} will be removed.`, 'danger');
    if (!ok) return;
    try {
      await this.links.delete(repoId, taskId, l.id);
      this.linkList.update(list => list.filter(x => x.id !== l.id));
    } catch { /* non-critical */ }
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

  /** Navigates to a linked work item by its key (e.g. "DASH-36"). @param key The target work item key. */
  openLinkTarget(key: string): void {
    this.router.navigate(['..', key], { relativeTo: this.route });
  }

  // ── Navigation ───────────────────────────────────────────────────────────────

  /** Returns to the board. */
  goBack(): void {
    this.router.navigate(['..'], { relativeTo: this.route });
  }
}
