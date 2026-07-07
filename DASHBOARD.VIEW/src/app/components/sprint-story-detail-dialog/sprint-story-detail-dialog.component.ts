import { ChangeDetectionStrategy, Component, computed, HostListener, inject, InjectionToken, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DIALOG_REF_TOKEN } from '../../models/dialog.model';
import { SprintStoryDetailDialogData, SprintTask } from '../../models/sprint-planning.model';
import { SprintPlanningService } from '../../services/sprint-planning.service';
import { MembersService } from '../../services/members.service';
import { DialogService } from '../../core/components/dialog/dialog.service';
import { AddSprintTaskDialogComponent } from '../add-sprint-task-dialog/add-sprint-task-dialog.component';
import { SPRINT_TASK_STATE_BADGE, SPRINT_TASK_STATE_LABEL } from '../../core/constants/system.constant';
import { SprintTaskApiState } from '../../core/enums/system.enum';

@Component({
  selector: 'app-sprint-story-detail-dialog',
  standalone: true,
  imports: [NgClass, FormsModule],
  templateUrl: './sprint-story-detail-dialog.component.html',
  styleUrl: './sprint-story-detail-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SprintStoryDetailDialogComponent {
  private readonly dialogRef = inject(DIALOG_REF_TOKEN);
  readonly planning          = inject(SprintPlanningService);
  readonly members           = inject(MembersService);
  private readonly dialogSvc = inject(DialogService);

  readonly data = inject('DIALOG_DATA' as unknown as InjectionToken<SprintStoryDetailDialogData>);

  readonly priorityLabel: Record<number, string> = { 0: 'Low', 1: 'Medium', 2: 'High', 3: 'Critical' };

  readonly priorityClass: Record<number, string> = {
    0: 'bg-slate-700/60 text-slate-400 ring-1 ring-slate-600',
    1: 'bg-amber-500/15 text-amber-300 ring-1 ring-amber-500/40',
    2: 'bg-orange-500/15 text-orange-300 ring-1 ring-orange-500/40',
    3: 'bg-red-500/15 text-red-400 ring-1 ring-red-500/40',
  };

  readonly stateBadge = SPRINT_TASK_STATE_BADGE;
  readonly stateLabel = SPRINT_TASK_STATE_LABEL;

  private readonly _stateToApi: Record<SprintTask['state'], SprintTaskApiState> = {
    'new':       SprintTaskApiState.New,
    'backlog':   SprintTaskApiState.Backlog,
    'todo':      SprintTaskApiState.Todo,
    'active':    SprintTaskApiState.Active,
    'in-review': SprintTaskApiState.InReview,
    'done':      SprintTaskApiState.Done,
  };

  /** Maps string state to its numeric API enum for badge lookup. @param state String state key. */
  stateApi(state: SprintTask['state']): SprintTaskApiState { return this._stateToApi[state]; }

  readonly subTaskTypeClass: Record<string, string> = {
    'task':      'text-amber-300 ring-amber-400/35 bg-amber-400/10',
    'bug':       'text-rose-400 ring-rose-500/35 bg-rose-500/10',
    'test-plan': 'text-violet-400 ring-violet-500/35 bg-violet-500/10',
  };

  // ── Story assignee picker ────────────────────────────────────────────────────
  readonly showAssigneePicker = signal(false);
  readonly assigneeSearch     = signal('');
  readonly assigneeId         = signal<string | null>(this.data.story.assignedToId);

  readonly filteredMembers = computed(() => {
    const q = this.assigneeSearch().toLowerCase();
    return this.members.members().filter(m => !q || m.userName.toLowerCase().includes(q));
  });

  /** Toggles the story assignee picker. @param e Mouse event. */
  toggleAssigneePicker(e: MouseEvent): void {
    e.stopPropagation();
    this.openSubTaskPickerId.set(null);
    this.assigneeSearch.set('');
    this.showAssigneePicker.update(v => !v);
  }

  /** Selects an assignee for the story. @param userId User ID or null to unassign. */
  selectAssignee(userId: string | null): void {
    this.assigneeId.set(userId);
    this.showAssigneePicker.set(false);
    this.planning.reassignTask(this.data.story.id, userId ?? '');
  }

  // ── Sub-task assignee pickers ────────────────────────────────────────────────
  readonly openSubTaskPickerId  = signal<string | null>(null);
  readonly subTaskSearch        = signal('');
  /** Optimistic assignee map: taskId → userId | null */
  readonly subTaskAssignees     = signal<Record<string, string | null>>(
    Object.fromEntries(this.data.subTasks.map(t => [t.id, t.assignedToId])),
  );

  readonly filteredSubTaskMembers = computed(() => {
    const q = this.subTaskSearch().toLowerCase();
    return this.members.members().filter(m => !q || m.userName.toLowerCase().includes(q));
  });

  /** Toggles the assignee picker for a specific sub-task. @param taskId Sub-task ID. @param e Mouse event. */
  toggleSubTaskPicker(taskId: string, e: MouseEvent): void {
    e.stopPropagation();
    this.showAssigneePicker.set(false);
    this.subTaskSearch.set('');
    this.openSubTaskPickerId.update(id => id === taskId ? null : taskId);
  }

  /** Selects an assignee for a sub-task. @param taskId Sub-task ID. @param userId User ID or null to unassign. */
  selectSubTaskAssignee(taskId: string, userId: string | null): void {
    this.subTaskAssignees.update(m => ({ ...m, [taskId]: userId }));
    this.openSubTaskPickerId.set(null);
    this.planning.reassignTask(taskId, userId ?? '');
  }

  @HostListener('document:click')
  onDocumentClick(): void {
    this.showAssigneePicker.set(false);
    this.openSubTaskPickerId.set(null);
  }

  /** Opens the add-task dialog for this story. */
  openAddTask(): void {
    const story = this.data.story;
    this.dialogRef.close();
    this.dialogSvc.open(AddSprintTaskDialogComponent, {
      title: 'Add task', width: '32rem',
      data:  { storyId: story.id, storyTitle: story.title },
    });
  }

  /** De-scopes this story and closes the dialog. */
  descopeStory(): void {
    if (!confirm(`De-scope "${this.data.story.title}"? It will be moved back to the backlog.`)) return;
    this.planning.descopeStory(this.data.story.id);
    this.dialogRef.close();
  }

  /** Changes a sub-task's state. @param taskId Task ID. @param event Change event. */
  changeSubTaskState(taskId: string, event: Event): void {
    this.planning.changeTaskState(taskId, (event.target as HTMLSelectElement).value as SprintTask['state']);
  }

  /** Closes the dialog. */
  close(): void { this.dialogRef.close(); }
}
