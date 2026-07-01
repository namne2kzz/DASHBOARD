import { CommonModule } from '@angular/common';
import { Component, computed, ElementRef, HostListener, inject, OnInit, QueryList, signal, ViewChildren } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgClass } from '@angular/common';
import { SprintPlanningService } from '../../services/sprint-planning.service';
import { MembersService } from '../../services/members.service';
import { DialogService } from '../../core/components/dialog/dialog.service';
import { CreateSprintDialogComponent } from '../../components/create-sprint-dialog/create-sprint-dialog.component';
import { AddSprintTaskDialogComponent } from '../../components/add-sprint-task-dialog/add-sprint-task-dialog.component';
import { AddCapacityMemberDialogComponent } from '../../components/add-capacity-member-dialog/add-capacity-member-dialog.component';
import type { SprintTask } from '../../models/sprint-planning.model';
import { SPRINT_TASK_STATE_OPTIONS } from '../../core/constants/system.constant';

@Component({
  selector: 'app-sprint-planning-page',
  imports: [CommonModule, FormsModule, NgClass],
  templateUrl: './sprint-planning-page.component.html',
  styleUrl: './sprint-planning-page.component.css',
})
export class SprintPlanningPageComponent implements OnInit {
  readonly planning          = inject(SprintPlanningService);
  readonly taskStateOptions  = SPRINT_TASK_STATE_OPTIONS;

  /** Reloads sprint detail on every navigation to this page to pick up state changes made on the Board. */
  ngOnInit(): void { this.planning.refresh(); }
  readonly members        = inject(MembersService);
  private readonly dialog = inject(DialogService);

  // ── Sprint setup edit mode ───────────────────────────────────
  readonly setupMenuOpen = signal(false);
  readonly editMode      = signal(false);
  readonly editName      = signal('');
  readonly editStart     = signal('');
  readonly editEnd       = signal('');

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

  @HostListener('document:click')
  onDocumentClick(): void {
    this.setupMenuOpen.set(false);
    this.closeReassign();
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

  /** Whether the selected sprint can be edited (future sprints only). */
  canEditSelectedSprint(): boolean {
    const s = this.planning.selectedSprint();
    return !!s && this.planning.sprintStatus(s) === 'future';
  }
}
