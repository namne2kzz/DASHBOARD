import { CommonModule } from '@angular/common';
import { Component, computed, effect, ElementRef, HostListener, inject, OnInit, QueryList, signal, ViewChildren } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgClass } from '@angular/common';
import { Router } from '@angular/router';
import { InfiniteScrollDirective } from '../../directives/infinite-scroll.directive';
import { FlipDropDirective } from '../../directives/flip-drop.directive';
import { SprintPlanningService } from '../../services/sprint-planning.service';
import { MembersService } from '../../services/members.service';
import { DialogService } from '../../core/components/dialog/dialog.service';
import { CreateSprintDialogComponent } from '../../components/create-sprint-dialog/create-sprint-dialog.component';
import { AddSprintTaskDialogComponent } from '../../components/add-sprint-task-dialog/add-sprint-task-dialog.component';
import { AddCapacityMemberDialogComponent } from '../../components/add-capacity-member-dialog/add-capacity-member-dialog.component';
import { SprintStoryDetailDialogComponent } from '../../components/sprint-story-detail-dialog/sprint-story-detail-dialog.component';
import type { SprintTask } from '../../models/sprint-planning.model';
import { SPRINT_TASK_STATE_BADGE, SPRINT_TASK_STATE_LABEL, SPRINT_TASK_STATE_OPTIONS } from '../../core/constants/system.constant';
import { getStateCategory, SprintTaskApiState, StateCategory } from '../../core/enums/system.enum';
import { ConfirmService } from '../../core/components/confirm/confirm.service';
import { RepositoryContextService } from '../../services/repository-context.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../core/components/toast/toast.service';

@Component({
  selector: 'app-sprint-planning-page',
  imports: [CommonModule, FormsModule, NgClass, InfiniteScrollDirective, FlipDropDirective],
  templateUrl: './sprint-planning-page.component.html',
  styleUrl: './sprint-planning-page.component.css',
})
export class SprintPlanningPageComponent implements OnInit {
  readonly planning          = inject(SprintPlanningService);
  readonly taskStateOptions  = SPRINT_TASK_STATE_OPTIONS;
  readonly stateBadge        = SPRINT_TASK_STATE_BADGE;
  readonly stateLabel        = SPRINT_TASK_STATE_LABEL;

  /** Number of capacity members whose workload exceeds their capacity — the only load state that gets colour. */
  readonly overloadedCount   = computed(() => this.planning.memberLoads().filter(m => m.state === 'overloaded').length);
  private readonly router    = inject(Router);
  private readonly repoCtx   = inject(RepositoryContextService);
  private readonly auth      = inject(AuthService);

  private readonly _stateToApi: Record<SprintTask['state'], SprintTaskApiState> = {
    'open':        SprintTaskApiState.Open,
    'todo':        SprintTaskApiState.ToDo,
    'in-progress': SprintTaskApiState.InProgress,
    'in-review':   SprintTaskApiState.InReview,
    'verified':    SprintTaskApiState.Verified,
    'running':     SprintTaskApiState.Running,
    'done':        SprintTaskApiState.Done,
    'passed':      SprintTaskApiState.Passed,
    'failed':      SprintTaskApiState.Failed,
    'closed':      SprintTaskApiState.Closed,
  };

  /** Maps a string state to its numeric SprintTaskApiState for use with SPRINT_TASK_STATE_BADGE. @param state String state. */
  stateApi(state: SprintTask['state']): SprintTaskApiState { return this._stateToApi[state]; }

  /** Returns true when a task is not in a terminal/done category. @param state String state key. */
  isOpenState(state: SprintTask['state']): boolean {
    return getStateCategory(this._stateToApi[state]) !== StateCategory.Done;
  }

  /** Reloads sprint detail on every navigation to this page to pick up state changes made on the Board. */
  ngOnInit(): void { this.planning.refresh(); }

  /**
   * Navigates to the work-item detail page for a given work item number.
   * @param workItemNumber Formatted number (e.g. "DASH-22").
   */
  navigateToItem(workItemNumber: string): void {
    const orgAlias = this.auth.currentUser()?.orgAlias;
    const repoCode = this.repoCtx.selectedRepo()?.code;
    if (!orgAlias || !repoCode) return;
    this.router.navigate(['/', orgAlias, repoCode, 'boards', workItemNumber]);
  }
  readonly members         = inject(MembersService);
  private readonly dialog  = inject(DialogService);
  private readonly confirm = inject(ConfirmService);
  private readonly toast   = inject(ToastService);

  // ── Sprint setup edit mode ───────────────────────────────────
  readonly setupMenuOpen = signal(false);
  readonly editMode      = signal(false);
  readonly editName      = signal('');
  readonly editStart     = signal('');
  readonly editEnd       = signal('');

  // ── Day-off add form ─────────────────────────────────────────
  readonly showDayOffForm        = signal(false);
  readonly dayOffDate            = signal('');
  readonly dayOffHours           = signal(8);
  readonly dayOffReason          = signal('');
  readonly dayOffUserId          = signal<string | null>(null);
  readonly showDayOffMemberPicker = signal(false);

  readonly canAddDayOff = computed(() =>
    this.dayOffDate().length > 0 && this.dayOffHours() > 0,
  );

  // ── Reassign picker ──────────────────────────────────────────
  @ViewChildren('reassignPanel') private reassignPanelRefs!: QueryList<ElementRef<HTMLElement>>;
  readonly reassignOpenTaskId = signal<string | null>(null);
  readonly reassignSearch     = signal('');
  readonly reassignRect       = signal<{ top: number; left: number; width: number } | null>(null);
  private _reassignEl: HTMLElement | null = null;

  readonly filteredReassignMembers = computed(() => {
    const q = this.reassignSearch().toLowerCase();
    return this.planning.memberLoads().filter(m =>
      !q || m.name.toLowerCase().includes(q),
    );
  });

  /** How many rows each list renders initially and per "load more" step. */
  private static readonly PAGE_SIZE = 25;

  // ── Sprint stories filter ─────────────────────────────────────
  readonly storySearch      = signal('');
  readonly storyStateFilter = signal<SprintTask['state'] | ''>('');
  private readonly _storyVisible = signal(SprintPlanningPageComponent.PAGE_SIZE);

  readonly filteredSprintStories = computed(() => {
    const q     = this.storySearch().trim().toLowerCase();
    const state = this.storyStateFilter();
    return this.planning.sprintStories().filter(s =>
      (!q || s.title.toLowerCase().includes(q) || s.workItemNumber.toLowerCase().includes(q)) &&
      (!state || s.state === state),
    );
  });

  /** The rendered slice of {@link filteredSprintStories}, capped for a lighter first paint. */
  readonly pagedSprintStories = computed(() => this.filteredSprintStories().slice(0, this._storyVisible()));

  /** True when more stories exist beyond the currently rendered slice. */
  readonly hasMoreStories = computed(() => this._storyVisible() < this.filteredSprintStories().length);

  /** Renders the next batch of sprint stories. */
  loadMoreStories(): void {
    this._storyVisible.update(c => c + SprintPlanningPageComponent.PAGE_SIZE);
  }

  // ── Task workload filter ──────────────────────────────────────
  readonly taskSearch         = signal('');
  readonly taskStateFilter    = signal<SprintTask['state'] | ''>('');
  readonly taskAssigneeFilter = signal('');
  private readonly _taskVisible = signal(SprintPlanningPageComponent.PAGE_SIZE);

  readonly filteredSprintTaskRows = computed(() => {
    const q        = this.taskSearch().trim().toLowerCase();
    const state    = this.taskStateFilter();
    const assignee = this.taskAssigneeFilter();
    return this.planning.sprintTaskRows().filter(t => {
      if (q && !t.title.toLowerCase().includes(q) && !t.workItemNumber.toLowerCase().includes(q)) return false;
      if (state && t.state !== state) return false;
      if (assignee === 'unassigned' && t.assignedToId !== null) return false;
      if (assignee && assignee !== 'unassigned' && t.assignedToId !== assignee) return false;
      return true;
    });
  });

  /** The rendered slice of {@link filteredSprintTaskRows}, capped for a lighter first paint. */
  readonly pagedSprintTaskRows = computed(() => this.filteredSprintTaskRows().slice(0, this._taskVisible()));

  /** True when more task rows exist beyond the currently rendered slice. */
  readonly hasMoreTasks = computed(() => this._taskVisible() < this.filteredSprintTaskRows().length);

  /** Renders the next batch of task-workload rows. */
  loadMoreTasks(): void {
    this._taskVisible.update(c => c + SprintPlanningPageComponent.PAGE_SIZE);
  }

  constructor() {
    // Reset each list back to its first page whenever its filters or the selected sprint change,
    // so a narrowed then re-widened result starts from the top instead of a stale large slice.
    effect(() => {
      this.storySearch(); this.storyStateFilter(); this.planning.selectedSprint()?.id;
      this._storyVisible.set(SprintPlanningPageComponent.PAGE_SIZE);
    });
    effect(() => {
      this.taskSearch(); this.taskStateFilter(); this.taskAssigneeFilter(); this.planning.selectedSprint()?.id;
      this._taskVisible.set(SprintPlanningPageComponent.PAGE_SIZE);
    });
  }

  @HostListener('document:click')
  onDocumentClick(): void {
    this.setupMenuOpen.set(false);
    this.closeReassign();
    this.showDayOffMemberPicker.set(false);
  }

  /** Recalculates dropdown position on scroll so it stays anchored to the trigger. */
  @HostListener('window:scroll', ['$event'])
  onWindowScroll(): void {
    if (this._reassignEl) {
      this._updateReassignRect();
      requestAnimationFrame(() => this._fitReassignRectToViewport());
    }
  }

  /** Opens reassign picker for a task. @param taskId The task to reassign. @param el The trigger element. */
  openReassign(taskId: string, el: HTMLElement, event: MouseEvent): void {
    event.stopPropagation();
    if (this.reassignOpenTaskId() === taskId) { this.closeReassign(); return; }
    this._reassignEl = el;
    this._updateReassignRect();
    this.reassignSearch.set('');
    this.reassignOpenTaskId.set(taskId);
    requestAnimationFrame(() => this._fitReassignRectToViewport());
  }

  private _updateReassignRect(): void {
    const r = this._reassignEl!.getBoundingClientRect();
    const w = Math.max(r.width, 180);
    this.reassignRect.set({ top: r.bottom + 4, left: r.right - w, width: w });
  }

  /** Flips the reassign panel above the trigger when it wouldn't fully fit below, like a native context menu. */
  private _fitReassignRectToViewport(): void {
    const panelEl = this.reassignPanelRefs?.first?.nativeElement;
    const triggerEl = this._reassignEl;
    const current = this.reassignRect();
    if (!panelEl || !triggerEl || !current) return;

    const margin = 8;
    const triggerRect = triggerEl.getBoundingClientRect();
    const panelHeight = panelEl.getBoundingClientRect().height;

    let top = triggerRect.bottom + 4;
    const fitsBelow = top + panelHeight <= window.innerHeight - margin;
    if (!fitsBelow) {
      const aboveTop = triggerRect.top - panelHeight - 4;
      top = aboveTop >= margin ? aboveTop : Math.max(margin, window.innerHeight - panelHeight - margin);
    }

    let left = current.left;
    if (left + current.width > window.innerWidth - margin) left = window.innerWidth - current.width - margin;
    if (left < margin) left = margin;

    if (top !== current.top || left !== current.left) {
      this.reassignRect.set({ ...current, top, left });
    }
  }

  /** Assigns the task and closes the picker. @param taskId The task. @param userId New assignee or empty to unassign. */
  doReassign(taskId: string, userId: string, event: MouseEvent): void {
    event.stopPropagation();
    this.planning.reassignTask(taskId, userId);
    this.closeReassign();
  }

  /** Closes the reassign picker. */
  closeReassign(): void {
    this.reassignOpenTaskId.set(null);
    this.reassignRect.set(null);
    this.reassignSearch.set('');
    this._reassignEl = null;
  }

  /** Opens the add capacity member dialog. */
  openAddCapacityMember(): void {
    this.dialog.open(AddCapacityMemberDialogComponent, { title: 'Add member to sprint', width: '28rem' });
  }

  /** Opens the create sprint dialog. */
  openNewSprint(): void {
    this.dialog.open(CreateSprintDialogComponent, { title: 'New sprint', width: '28rem' });
  }

  /** Opens the story detail dialog. @param story The sprint story. */
  openStoryDetail(story: SprintTask): void {
    const subTasks = this.planning.sprintTaskRows().filter(t => t.parentId === story.id);
    this.dialog.open(SprintStoryDetailDialogComponent, {
      title: story.workItemNumber,
      width: '52rem',
      data:  { story, subTasks },
    });
  }

  /** Returns the number of sub-tasks belonging to a story. @param storyId Story ID. */
  storySubTaskCount(storyId: string): number {
    return this.planning.sprintTaskRows().filter(t => t.parentId === storyId).length;
  }

  /** Opens the add-task dialog for a sprint story. @param story The parent story. */
  openAddTask(story: SprintTask): void {
    this.dialog.open(AddSprintTaskDialogComponent, {
      title: 'Add task', width: '32rem',
      data:  { storyId: story.id, storyTitle: story.title },
    });
  }

  /** Returns the parent story title for a task row. @param parentId The parent sprint task ID. */
  parentStoryTitle(parentId: string | null): string {
    if (!parentId) return '—';
    return this.planning.sprintStories().find(s => s.id === parentId)?.title ?? '—';
  }

  /** Enters edit mode for the selected sprint. */
  startEdit(): void {
    const s = this.planning.selectedSprint();
    if (!s) return;
    this.editName.set(s.name);
    this.editStart.set(s.startDate);
    this.editEnd.set(s.endDate);
    this.editMode.set(true);
    this.setupMenuOpen.set(false);
  }

  /** Saves the edited sprint. */
  saveEdit(): void {
    this.planning.updateSprint(this.editName(), this.editStart(), this.editEnd());
    this.editMode.set(false);
  }

  /** Cancels edit without saving. */
  cancelEdit(): void { this.editMode.set(false); }

  /** Whether the selected sprint can be edited (future and active sprints). */
  canEditSelectedSprint(): boolean {
    const s = this.planning.selectedSprint();
    return !!s && this.planning.sprintStatus(s) !== 'past';
  }

  /** Whether the selected sprint can be deleted (future and past, not active). */
  canDeleteSelectedSprint(): boolean {
    const s = this.planning.selectedSprint();
    return !!s && this.planning.sprintStatus(s) !== 'active';
  }

  /** Deletes the selected sprint after confirmation. */
  async deleteSprint(): Promise<void> {
    const s = this.planning.selectedSprint();
    if (!s) return;
    const ok = await this.confirm.ask(
      `Delete sprint "${s.name}"?`,
      'All tasks and capacity data will be permanently removed. This cannot be undone.',
      'danger',
    );
    if (!ok) return;
    this.setupMenuOpen.set(false);
    this.planning.deleteSprint(s.id);
  }

  /** Removes a capacity member from the sprint after confirmation. @param userId Member's user ID. */
  async removeCapacityMember(userId: string): Promise<void> {
    const capacityMemberId = this.planning.getCapacityMemberId(userId);
    if (!capacityMemberId) return;
    const ok = await this.confirm.ask(
      'Remove member from sprint?',
      'Their capacity and day-off entries for this sprint will be deleted.',
      'warning',
    );
    if (!ok) return;
    this.planning.removeCapacityMember(capacityMemberId);
  }

  /** Opens the day-off add form. */
  openDayOffForm(): void {
    this.dayOffDate.set('');
    this.dayOffHours.set(8);
    this.dayOffReason.set('');
    this.dayOffUserId.set(null);
    this.showDayOffForm.set(true);
  }

  /** Submits the new day-off entry. */
  submitDayOff(): void {
    if (!this.canAddDayOff()) return;
    this.planning.addDayOff(
      this.dayOffDate(),
      this.dayOffHours(),
      this.dayOffReason(),
      this.dayOffUserId() ?? undefined,
    );
    this.showDayOffForm.set(false);
  }

  /** Cancels the day-off add form. */
  cancelDayOff(): void { this.showDayOffForm.set(false); }

  // ── Sprint lifecycle ──────────────────────────────────────────

  /** Whether the selected sprint can be activated (status = Planning). */
  canActivateSelectedSprint(): boolean {
    return this.planning.selectedSprint()?.status === 'Planning';
  }

  /** Whether the selected sprint can be closed (status = Active). */
  canCloseSelectedSprint(): boolean {
    return this.planning.selectedSprint()?.status === 'Active';
  }

  /** Activates the selected sprint after confirmation. */
  async activateSprint(): Promise<void> {
    const s = this.planning.selectedSprint();
    if (!s) return;
    const ok = await this.confirm.ask(
      `Activate sprint "${s.name}"?`,
      'This will mark the sprint as Active. Only one sprint can be active at a time.',
      'info',
    );
    if (!ok) return;
    this.setupMenuOpen.set(false);
    this.planning.activateSprint(s.id).subscribe({
      next:  () => this.toast.success('Sprint activated.'),
      error: (err) => this.toast.error(err?.error?.error ?? 'Failed to activate sprint.'),
    });
  }

  /** Closes the selected sprint. Shows a warning if incomplete items remain and prompts for confirmation. */
  async closeSprint(): Promise<void> {
    const s = this.planning.selectedSprint();
    if (!s) return;
    this.setupMenuOpen.set(false);
    this.planning.closeSprint(s.id).subscribe({
      next: async (result) => {
        if (!result) {
          // HTTP 204 — closed cleanly
          this.toast.success('Sprint closed.');
          return;
        }
        if (!result.closed && result.warning) {
          // Server returned a warning — prompt user to confirm force close
          const force = await this.confirm.ask(
            'Incomplete items remaining',
            result.warning,
            'warning',
          );
          if (!force) return;
          this.planning.closeSprint(s.id, true).subscribe({
            next: () => this.toast.success('Sprint closed.'),
            error: (err) => this.toast.error(err?.error?.error ?? 'Failed to close sprint.'),
          });
        }
      },
      error: (err) => this.toast.error(err?.error?.error ?? 'Failed to close sprint.'),
    });
  }
}
