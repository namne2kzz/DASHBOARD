import { computed, DestroyRef, effect, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EMPTY, Observable } from 'rxjs';
import { tap } from 'rxjs/operators';
import { RepositoryContextService } from './repository-context.service';
import { SprintSelectionService } from './sprint-selection.service';
import { PrivilegeService } from '../core/services/privilege.service';
import { ToastService } from '../core/components/toast/toast.service';
import { environment } from '../../environments/environment';
import {
  CapacityMemberApiDto,
  SprintApiDto,
  SprintDetailApiDto,
  SprintTaskApiDto,
  SprintTaskApiType,
} from '../models/sprint-planning-api.model';
import { SPRINT_TASK_STATE_OPTIONS } from '../core/constants/system.constant';
import { LoadState, MemberLoad, Sprint, SprintTask } from '../models/sprint-planning.model';

@Injectable({ providedIn: 'root' })
export class SprintPlanningService {
  private readonly http            = inject(HttpClient);
  private readonly repoCtx         = inject(RepositoryContextService);
  private readonly sprintSelection = inject(SprintSelectionService);
  private readonly privilege       = inject(PrivilegeService);
  private readonly toast           = inject(ToastService);
  private readonly destroyRef      = inject(DestroyRef);

  private readonly _detail   = signal<SprintDetailApiDto | null>(null);
  private readonly _loading  = signal(false);
  private readonly _error        = signal<string | null>(null);

  readonly canManageSprints  = computed(() => this.privilege.canManageSprints());
  readonly canManageCapacity = computed(() => this.privilege.canManageCapacity());
  readonly canEditWorkItem   = computed(() => this.privilege.canEditWorkItem());

  /** True while either the shared sprint list or this page's own sprint detail is loading. */
  readonly loading = computed(() => this._loading() || this.sprintSelection.loading());
  readonly error   = this._error.asReadonly();

  readonly sprints = computed<Sprint[]>(() =>
    this.sprintSelection.sprints().map(s => ({
      id: s.id, name: s.name,
      startDate: s.startDate, endDate: s.endDate, isActive: s.isActive,
      hubChannelId:  s.hubChannelId  ?? null,
      hubChannelUrl: s.hubChannelUrl ?? null,
    })),
  );

  readonly selectedSprintId = this.sprintSelection.selectedSprintId;

  readonly selectedSprint = computed<Sprint | null>(() =>
    this.sprints().find(s => s.id === this.selectedSprintId()) ?? this.sprints()[0] ?? null,
  );

  readonly capacityMembers = computed(() => this._detail()?.capacityMembers ?? []);
  readonly daysOff         = computed(() => this._detail()?.daysOff ?? []);
  readonly personalDaysOff = computed(() => this.daysOff().filter(d => d.userId !== null));
  readonly teamDaysOff     = computed(() => this.daysOff().filter(d => d.userId === null));
  readonly workingDays     = computed(() => this._detail()?.workingDays ?? 0);

  readonly effectiveWorkingDays = computed(() => {
    const detail = this._detail();
    if (!detail) return 0;
    const teamOffDates = new Set(
      detail.daysOff.filter(d => d.userId === null).map(d => d.date),
    );
    return this.generateWorkingDates(detail.startDate, detail.endDate)
      .filter(d => !teamOffDates.has(d)).length;
  });

  readonly memberLoads = computed<MemberLoad[]>(() => {
    const loads   = this._detail()?.memberLoads ?? [];
    const members = this._detail()?.capacityMembers ?? [];
    return loads.map(l => {
      const cap = members.find(m => m.userId === l.userId);
      return {
        userId:              l.userId,
        name:                l.userName,
        role:                cap?.role ?? 'Developer',
        hoursPerDay:         l.hoursPerDay,
        overtimeHoursPerDay: l.overtimeHoursPerDay,
        personalDaysOff:     l.personalDaysOffHours,
        capacity:            l.capacity,
        workload:            l.workload,
        loadPercent:         l.loadPercent,
        state:               l.loadState,
      };
    });
  });

  readonly totalCapacity   = computed(() => this.memberLoads().reduce((s, m) => s + m.capacity, 0));
  readonly totalWorkload   = computed(() => this.memberLoads().reduce((s, m) => s + m.workload, 0));
  readonly teamLoadPercent = computed(() => this.percent(this.totalWorkload(), this.totalCapacity()));
  readonly teamLoadState   = computed(() => this.loadState(this.teamLoadPercent()));

  readonly sprintStories = computed<SprintTask[]>(() =>
    this.flatTasks().filter(t => t.type === 'user-story'),
  );

  readonly sprintTaskRows = computed<SprintTask[]>(() =>
    this.flatTasks().filter(t => t.type !== 'user-story'),
  );

  constructor() {
    effect(() => {
      const repoId = this.repoCtx.selectedRepoId();
      this._error.set(null);
      if (!repoId) { this._detail.set(null); return; }
    });

    // Load sprint detail whenever the shared sprint selection changes.
    effect(() => {
      const repoId   = this.repoCtx.selectedRepoId();
      const sprintId = this.sprintSelection.selectedSprintId();
      this._detail.set(null);
      if (!repoId || !sprintId) return;
      this.loadDetail(repoId, sprintId);
    });
  }

  /** Creates a new sprint (inactive). Caller must subscribe and handle error.
   * @param name Sprint name.
   * @param startDate ISO date.
   * @param endDate ISO date.
   * @param createHubChannel When true, a HUB Chat channel is created and linked to the sprint.
   * @returns Observable that emits the created sprint and adds it to the selection list. */
  createSprint(name: string, startDate: string, endDate: string, createHubChannel = false): Observable<SprintApiDto> {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return EMPTY;
    return this.http
      .post<SprintApiDto>(this.sprintsUrl(repoId), { name, startDate, endDate, createHubChannel })
      .pipe(tap(sprint => this.sprintSelection.addSprint(sprint)));
  }

  /** Deletes a sprint and removes it from the selection list. @param sprintId Sprint to delete. */
  deleteSprint(sprintId: string): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;
    this.http
      .delete<void>(`${this.sprintsUrl(repoId)}/${sprintId}`)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  () => this.sprintSelection.removeSprint(sprintId),
        error: (err) => this.toast.error(err?.error?.error ?? 'Failed to delete sprint.'),
      });
  }

  /** Selects a sprint — its detail loads automatically via the shared selection effect. @param sprintId The sprint to select. */
  selectSprint(sprintId: string): void {
    this.sprintSelection.selectSprint(sprintId);
  }

  /** Returns the date-based status of a sprint. @param sprint The sprint to evaluate. */
  sprintStatus(sprint: Sprint): 'past' | 'active' | 'future' {
    const today = new Date().toISOString().slice(0, 10);
    if (sprint.endDate < today)   return 'past';
    if (sprint.startDate <= today) return 'active';
    return 'future';
  }

  /** Updates sprint name and dates. Allowed for future and active sprints; blocked for past. @param name Sprint name. @param startDate ISO date. @param endDate ISO date. */
  updateSprint(name: string, startDate: string, endDate: string): void {
    const repoId   = this.repoCtx.selectedRepoId();
    const sprintId = this.selectedSprintId();
    if (!repoId || !sprintId || !name.trim() || !startDate || !endDate || startDate >= endDate) return;

    this.http
      .put(`${this.sprintsUrl(repoId)}/${sprintId}`, { name: name.trim(), startDate, endDate })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.sprintSelection.patchSprint(sprintId, { name: name.trim(), startDate, endDate });
          this.reloadDetail();
        },
        error: () => this.toast.error('Failed to update sprint. Check dates do not overlap existing sprints.'),
      });
  }

  /** @deprecated Use updateSprint instead. */
  updateSprintDates(startDate: string, endDate: string): void {
    const sprint = this.selectedSprint();
    if (sprint) this.updateSprint(sprint.name, startDate, endDate);
  }

  /** Updates regular daily hours for a capacity member. @param userId Member user ID. @param value New hours value. */
  updateHours(userId: string, value: number): void {
    const member = this.capacityMembers().find(m => m.userId === userId);
    if (!member) return;
    this.upsertCapacity(member, member.hoursPerDay, value, member.overtimeHoursPerDay);
  }

  /** Updates overtime daily hours for a capacity member. @param userId Member user ID. @param value New overtime hours. */
  updateOvertime(userId: string, value: number): void {
    const member = this.capacityMembers().find(m => m.userId === userId);
    if (!member) return;
    this.upsertCapacity(member, member.hoursPerDay, member.hoursPerDay, value);
  }

  /** Adds a new member to the sprint capacity configuration. @param userId Repo member user ID. @param role Team-role (discipline) value. @param hoursPerDay Daily working hours. @param overtime Daily overtime hours. */
  addCapacityMember(userId: string, role: string, hoursPerDay: number, overtime: number): void {
    const repoId   = this.repoCtx.selectedRepoId();
    const sprintId = this.selectedSprintId();
    if (!repoId || !sprintId) return;

    this.http
      .put(
        `${this.sprintsUrl(repoId)}/${sprintId}/capacity/members/${userId}`,
        null,
        { params: { role: role, hoursPerDay: hoursPerDay.toString(), overtimeHoursPerDay: overtime.toString() } },
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  () => this.reloadDetail(),
        error: () => this.toast.error('Failed to add capacity member.'),
      });
  }

  /** Adds 1h overtime for a member (capped at 6h). @param userId Member user ID. */
  addOneHourOvertime(userId: string): void {
    const member = this.capacityMembers().find(m => m.userId === userId);
    if (!member) return;
    const newOt = Math.min(6, member.overtimeHoursPerDay + 1);
    this.upsertCapacity(member, member.hoursPerDay, member.hoursPerDay, newOt);
  }

  /** Creates a Task sub-task under a sprint story. @param parentStoryId Parent UserStory SprintTask ID. @param title Task title. @param description Optional description. @param priority Priority (0=Low…3=Critical). @param originalEstimate Estimated hours. @param assignedToId Optional assignee. */
  createSubTask(parentStoryId: string, title: string, description: string, priority: number, originalEstimate: number, assignedToId: string | null): void {
    const repoId   = this.repoCtx.selectedRepoId();
    const sprintId = this.selectedSprintId();
    if (!repoId || !sprintId || !title.trim()) return;

    const params: Record<string, string> = {
      type:             '1', // SprintTaskType.Task
      title:            title.trim(),
      description:      description,
      priority:         priority.toString(),
      parentId:         parentStoryId,
      originalEstimate: originalEstimate.toString(),
    };
    if (assignedToId) params['assignedToId'] = assignedToId;

    this.http
      .post(`${this.tasksUrl(repoId, sprintId)}`, null, { params })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  () => this.reloadDetail(),
        error: () => this.toast.error('Failed to create task.'),
      });
  }

  /** Removes a sprint story and restores its backlog item to Ready. @param taskId Sprint task ID. */
  descopeStory(taskId: string): void {
    const repoId   = this.repoCtx.selectedRepoId();
    const sprintId = this.selectedSprintId();
    if (!repoId || !sprintId) return;

    this.http
      .post(`${this.tasksUrl(repoId, sprintId)}/${taskId}/descope`, {})
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.reloadDetail(),
        error: () => this.toast.error('Failed to de-scope story.'),
      });
  }

  /** Updates remaining work hours on a task. @param taskId Sprint task ID. @param hours New remaining hours. */
  updateRemainingWork(taskId: string, hours: number): void {
    const repoId   = this.repoCtx.selectedRepoId();
    const sprintId = this.selectedSprintId();
    if (!repoId || !sprintId) return;

    this.http
      .patch(`${this.tasksUrl(repoId, sprintId)}/${taskId}/remaining`, null,
        { params: { hours: hours.toString() } })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  () => this.reloadDetail(),
        error: () => this.toast.error('Failed to update remaining work.'),
      });
  }

  /** Reassigns a task to a different team member. @param taskId Sprint task ID. @param userId New assignee user ID. */
  reassignTask(taskId: string, userId: string): void {
    const repoId   = this.repoCtx.selectedRepoId();
    const sprintId = this.selectedSprintId();
    if (!repoId || !sprintId) return;

    this.http
      .patch(`${this.tasksUrl(repoId, sprintId)}/${taskId}/assign`, null,
        { params: { assignedToId: userId } })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  () => this.reloadDetail(),
        error: () => this.toast.error('Failed to reassign task.'),
      });
  }

  /** Changes the state of a sprint task. @param taskId Sprint task ID. @param state New state string key. */
  changeTaskState(taskId: string, state: string): void {
    const repoId   = this.repoCtx.selectedRepoId();
    const sprintId = this.selectedSprintId();
    if (!repoId || !sprintId) return;

    const opt = SPRINT_TASK_STATE_OPTIONS.find(o => o.value === state);
    if (!opt) return;

    this.http
      .patch(`${this.tasksUrl(repoId, sprintId)}/${taskId}/state`, null,
        { params: { newState: opt.api.toString() } })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  () => this.reloadDetail(),
        error: () => this.toast.error('Failed to change task state.'),
      });
  }

  /** Removes a capacity member row from the sprint. @param capacityMemberId The capacity row ID (CapacityMember.Id, not userId). */
  removeCapacityMember(capacityMemberId: string): void {
    const repoId   = this.repoCtx.selectedRepoId();
    const sprintId = this.selectedSprintId();
    if (!repoId || !sprintId) return;
    this.http
      .delete(`${this.sprintsUrl(repoId)}/${sprintId}/capacity/members/${capacityMemberId}`)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  () => this.reloadDetail(),
        error: (err) => this.toast.error(err?.error?.error ?? 'Failed to remove capacity member.'),
      });
  }

  /** Adds a day-off entry to the sprint. @param date ISO date string. @param hours Hours to deduct. @param reason Short description. @param userId Optional member; omit for team-wide day off. */
  addDayOff(date: string, hours: number, reason: string, userId?: string): void {
    const repoId   = this.repoCtx.selectedRepoId();
    const sprintId = this.selectedSprintId();
    if (!repoId || !sprintId) return;
    const params: Record<string, string> = { date, hours: hours.toString(), reason };
    if (userId) params['userId'] = userId;
    this.http
      .post(`${this.sprintsUrl(repoId)}/${sprintId}/capacity/daysoff`, null, { params })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  () => this.reloadDetail(),
        error: () => this.toast.error('Failed to add day off.'),
      });
  }

  /** Removes a day-off entry from the sprint. @param dayOffId The DayOff row ID. */
  removeDayOff(dayOffId: string): void {
    const repoId   = this.repoCtx.selectedRepoId();
    const sprintId = this.selectedSprintId();
    if (!repoId || !sprintId) return;
    this.http
      .delete(`${this.sprintsUrl(repoId)}/${sprintId}/capacity/daysoff/${dayOffId}`)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  () => this.reloadDetail(),
        error: (err) => this.toast.error(err?.error?.error ?? 'Failed to remove day off.'),
      });
  }

  /** Returns the capacity row ID for a member (needed for remove). @param userId Member user ID. @returns CapacityMember.Id or null if not in sprint. */
  getCapacityMemberId(userId: string): string | null {
    return this.capacityMembers().find(m => m.userId === userId)?.id ?? null;
  }

  /** Returns the display name of a user by ID. @param userId User ID or null. @returns Name or 'Unassigned'. */
  userName(userId: string | null): string {
    if (!userId) return 'Unassigned';
    return this.memberLoads().find(m => m.userId === userId)?.name ?? 'Unknown';
  }

  /** Neutral bar; only an overloaded row gets colour (Nexus UI). @param state Load state. @returns Tailwind class for the progress bar. */
  progressBarClass(state: LoadState): string {
    switch (state) {
      case 'safe':       return 'bg-slate-300';
      case 'warning':    return 'bg-slate-300';
      case 'overloaded': return 'bg-rose-500';
    }
  }

  /** @param percent Raw load percent. @returns Load state label. */
  loadState(percent: number): LoadState {
    if (percent > 120) return 'overloaded';
    if (percent > 100) return 'warning';
    return 'safe';
  }

  /** @param value Raw percent. @returns Value capped at 140. */
  cappedPercent(value: number): number {
    return Math.min(140, Math.max(0, value));
  }

  // ── Private ───────────────────────────────────────────────────────────────

  private loadDetail(repoId: string, sprintId: string): void {
    this._loading.set(true);
    this.http.get<SprintDetailApiDto>(`${this.sprintsUrl(repoId)}/${sprintId}/detail`)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  d => { this._detail.set(d); this._loading.set(false); },
        error: () => { this._error.set('Failed to load sprint detail.'); this._loading.set(false); },
      });
  }

  /** Reloads the current sprint detail — call when navigating to this page to pick up external state changes. */
  refresh(): void {
    const repoId   = this.repoCtx.selectedRepoId();
    const sprintId = this.selectedSprintId();
    if (repoId && sprintId) this.loadDetail(repoId, sprintId);
  }

  private reloadDetail(): void { this.refresh(); }

  private upsertCapacity(member: CapacityMemberApiDto, _old: number, hoursPerDay: number, overtime: number): void {
    const repoId   = this.repoCtx.selectedRepoId();
    const sprintId = this.selectedSprintId();
    if (!repoId || !sprintId) return;

    this.http
      .put(
        `${this.sprintsUrl(repoId)}/${sprintId}/capacity/members/${member.userId}`,
        null,
        { params: { role: member.role.toString(), hoursPerDay: hoursPerDay.toString(), overtimeHoursPerDay: overtime.toString() } },
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  () => this.reloadDetail(),
        error: () => this.toast.error('Failed to update capacity.'),
      });
  }

  private flatTasks(): SprintTask[] {
    const flatten = (tasks: SprintTaskApiDto[]): SprintTask[] =>
      tasks.flatMap(t => [this.mapTask(t), ...flatten(t.subTasks)]);
    return flatten(this._detail()?.tasks ?? []);
  }

  private mapTask(t: SprintTaskApiDto): SprintTask {
    return {
      id:               t.id,
      parentId:         t.parentId,
      type:             t.type === SprintTaskApiType.UserStory ? 'user-story'
                      : t.type === SprintTaskApiType.Bug      ? 'bug'
                      : t.type === SprintTaskApiType.TestPlan ? 'test-plan'
                      : 'task',
      workItemNumber:   t.workItemNumber,
      title:            t.title,
      description:      t.description,
      priority:         t.priority,
      assignedToId:     t.assignedToId,
      assignedToName:   t.assignedToName,
      state:            (['new', 'backlog', 'todo', 'active', 'in-review', 'done'] as const)[t.state],
      storyPoints:        t.storyPoints,
      originalEstimate:   t.originalEstimate,
      remainingWork:      t.remainingWork,
      completedWork:      t.completedWork,
      acceptanceCriteria: t.acceptanceCriteria ?? null,
      documents:          t.documents ?? [],
    };
  }

  private generateWorkingDates(start: string, end: string): string[] {
    const dates: string[] = [];
    const e = new Date(end + 'T00:00:00');
    for (const d = new Date(start + 'T00:00:00'); d <= e; d.setDate(d.getDate() + 1)) {
      if (d.getDay() !== 0 && d.getDay() !== 6)
        dates.push(d.toISOString().slice(0, 10));
    }
    return dates;
  }

  private percent(workload: number, capacity: number): number {
    if (capacity === 0) return workload > 0 ? 999 : 0;
    return Math.round((workload / capacity) * 100);
  }

  private sprintsUrl(repoId: string): string {
    return `${environment.apiBaseUrl}/repositories/${repoId}/sprints`;
  }

  private tasksUrl(repoId: string, sprintId: string): string {
    return `${environment.apiBaseUrl}/repositories/${repoId}/sprints/${sprintId}/tasks`;
  }
}
