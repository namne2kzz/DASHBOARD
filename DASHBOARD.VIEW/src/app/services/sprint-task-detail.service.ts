import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { forkJoin, Observable, tap } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { environment } from '../../environments/environment';
import { RepositoryContextService } from './repository-context.service';
import { SprintBoardService, PRIORITY_MAP } from './sprint-board.service';
import type { SprintTaskApiDto, SprintTaskApiState } from '../models/sprint-planning-api.model';
import type { DiscussionApiDto, HistoryApiDto, UpdateSprintTaskPayload } from '../models/sprint-task-detail.model';

const API = `${environment.apiBaseUrl}/repositories`;

/** Loads and mutates a single sprint task's detail, discussion thread, and history — for the task detail dialog. */
@Injectable({ providedIn: 'root' })
export class SprintTaskDetailService {
  private readonly http       = inject(HttpClient);
  private readonly repoCtx    = inject(RepositoryContextService);
  private readonly board      = inject(SprintBoardService);
  private readonly destroyRef = inject(DestroyRef);

  readonly detail      = signal<SprintTaskApiDto | null>(null);
  readonly discussions = signal<DiscussionApiDto[]>([]);
  readonly history     = signal<HistoryApiDto[]>([]);
  readonly loading     = signal(false);
  readonly error       = signal<string | null>(null);

  /** Loads detail, discussions, and history for a task in parallel. @param taskId The sprint task to load. */
  loadDetail(taskId: string): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;

    this.loading.set(true);
    this.error.set(null);

    forkJoin({
      detail:      this.http.get<SprintTaskApiDto>(`${API}/${repoId}/sprint-tasks/${taskId}`),
      discussions: this.http.get<DiscussionApiDto[]>(`${API}/${repoId}/sprint-tasks/${taskId}/discussions`),
      history:     this.http.get<HistoryApiDto[]>(`${API}/${repoId}/sprint-tasks/${taskId}/history`),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ detail, discussions, history }) => {
          this.detail.set(detail);
          this.discussions.set(discussions);
          this.history.set(history);
          this.loading.set(false);
        },
        error: () => { this.error.set('Failed to load task detail'); this.loading.set(false); },
      });
  }

  /** Posts a new comment and prepends it to the discussion list. @param taskId The sprint task. @param body Comment text. */
  postComment(taskId: string, body: string): void {
    const repoId = this.repoCtx.selectedRepoId();
    const trimmed = body.trim();
    if (!repoId || !trimmed) return;

    this.http
      .post<DiscussionApiDto>(`${API}/${repoId}/sprint-tasks/${taskId}/discussions`, JSON.stringify(trimmed), {
        headers: { 'Content-Type': 'application/json' },
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: dto => this.discussions.update(list => [...list, dto]),
        error: () => this.error.set('Failed to post comment'),
      });
  }

  /**
   * Updates the task's editable fields (everything except state and assignee).
   * Caller subscribes to know when the save completes, so it can reset its dirty-tracking snapshot.
   * @param taskId The sprint task to update.
   * @param payload The new field values.
   * @returns Observable that updates the local `detail` signal on success.
   */
  updateTask(taskId: string, payload: UpdateSprintTaskPayload): Observable<void> {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) throw new Error('No repository selected.');

    return this.http.put<void>(`${API}/${repoId}/sprint-tasks/${taskId}`, payload).pipe(
      tap(() => {
        this.detail.update(d => d ? { ...d, ...payload } : d);
        // Only title/priority show on the board card — keep it in sync to avoid a stale mismatch.
        this.board.patchItem(taskId, { title: payload.title, priority: PRIORITY_MAP[payload.priority] });
      }),
    );
  }

  /** Changes the task's state and reloads history. @param taskId The sprint task. @param sprintId The task's sprint. @param newState Target state. */
  changeState(taskId: string, sprintId: string, newState: SprintTaskApiState): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;

    this.http
      .patch(`${API}/${repoId}/sprints/${sprintId}/tasks/${taskId}/state?newState=${newState}`, null)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          const nowIso = new Date().toISOString();
          this.detail.update(d => d ? { ...d, state: newState } : d);
          this.board.patchItem(taskId, { apiState: newState, stateChangedAt: nowIso });
          this.reloadHistory(taskId);
        },
        error: () => this.error.set('Failed to change state'),
      });
  }

  /** Reassigns the task and reloads history. @param taskId The sprint task. @param sprintId The task's sprint. @param userId New assignee, or null to unassign. */
  changeAssignee(taskId: string, sprintId: string, userId: string | null): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;

    let params = new HttpParams();
    if (userId) params = params.set('assignedToId', userId);

    this.http
      .patch(`${API}/${repoId}/sprints/${sprintId}/tasks/${taskId}/assign`, null, { params })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          const member           = userId ? this.board.capacityMembers().find(m => m.userId === userId) : undefined;
          const assignedToName   = member?.userName   ?? null;
          const assignedToAvatar = member?.userAvatar ?? null;

          this.detail.update(d => d ? { ...d, assignedToId: userId, assignedToName, assignedToAvatar } : d);
          // Clear name/avatar too — the card falls back to these when assignedToId is null,
          // so leaving the old name in place would still show the previous assignee.
          this.board.patchItem(taskId, { assignedToId: userId, assignedToName, assignedToAvatar });
          this.reloadHistory(taskId);
        },
        error: () => this.error.set('Failed to reassign task'),
      });
  }

  private reloadHistory(taskId: string): void {
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId) return;
    this.http.get<HistoryApiDto[]>(`${API}/${repoId}/sprint-tasks/${taskId}/history`)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: history => this.history.set(history), error: () => {} });
  }
}
