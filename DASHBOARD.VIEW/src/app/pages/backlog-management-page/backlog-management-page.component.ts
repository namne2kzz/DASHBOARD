import { DragDropModule } from '@angular/cdk/drag-drop';
import { CommonModule } from '@angular/common';
import { Component, computed, HostListener, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BacklogManagementService } from '../../services/backlog-management.service';
import { DialogService } from '../../core/components/dialog/dialog.service';
import { AddBacklogItemDialogComponent } from '../../components/add-backlog-item-dialog/add-backlog-item-dialog.component';
import { BacklogItemDetailDialogComponent } from '../../components/backlog-item-detail-dialog/backlog-item-detail-dialog.component';
import type { BacklogItem, BacklogState } from '../../models/backlog.model';

@Component({
  selector: 'app-backlog-management-page',
  imports: [CommonModule, FormsModule, DragDropModule],
  templateUrl: './backlog-management-page.component.html',
  styleUrl: './backlog-management-page.component.css',
})
export class BacklogManagementPageComponent implements OnInit {
  readonly backlog        = inject(BacklogManagementService);
  private readonly dialog = inject(DialogService);

  // ── Kebab menu ──────────────────────────────────────────────
  readonly openMenuId      = signal<string | null>(null);

  // ── Inline title edit ────────────────────────────────────────
  readonly editingId    = signal<string | null>(null);
  readonly editTitle    = signal('');

  // ── Delete confirmation ──────────────────────────────────────
  readonly pendingDeleteId = signal<string | null>(null);

  // ── Promote to sprint ────────────────────────────────────────
  readonly promotingItemId    = signal<string | null>(null);
  readonly selectedSprintId   = signal<string>('');

  // ── Portfolio hierarchy (collapsible) ─────────────────────────
  readonly expandedEpicIds = signal<ReadonlySet<string>>(new Set());

  // ── Epic picker (searchable) ─────────────────────────────────
  readonly epicPickerOpen   = signal(false);
  readonly epicSearch       = signal('');
  readonly filteredEpics    = computed(() => {
    const q = this.epicSearch().toLowerCase();
    return this.backlog.epics().filter(e => e.title.toLowerCase().includes(q));
  });
  readonly selectedEpicTitle = computed(() => {
    const id = this.backlog.selectedEpicId();
    if (!id) return 'All Epics';
    return this.backlog.epics().find(e => e.id === id)?.title ?? 'Epic';
  });

  /** Closes menus when user clicks anywhere outside. */
  @HostListener('document:click')
  onDocumentClick(): void {
    this.openMenuId.set(null);
    this.epicPickerOpen.set(false);
  }

  /** Selects or clears the epic filter. @param epicId Epic ID or null to clear. */
  selectEpicFilter(epicId: string | null): void {
    this.backlog.setSelectedEpic(epicId);
    this.epicSearch.set('');
    this.epicPickerOpen.set(false);
  }

  ngOnInit(): void {
    this.backlog.reset();
  }

  // ── Portfolio hierarchy ────────────────────────────────────────

  /** @param epicId The epic whose expanded state to check. @returns True when its features are shown. */
  isEpicExpanded(epicId: string): boolean {
    return this.expandedEpicIds().has(epicId);
  }

  /** Expands or collapses the feature list under an epic. @param epicId The epic to toggle. */
  toggleEpicExpand(epicId: string): void {
    this.expandedEpicIds.update(current => {
      const next = new Set(current);
      if (next.has(epicId)) next.delete(epicId);
      else next.add(epicId);
      return next;
    });
  }

  // ── Kebab menu ───────────────────────────────────────────────

  /**
   * Toggles the kebab menu for a specific item.
   * @param id The item ID whose menu to toggle.
   */
  toggleMenu(id: string): void {
    this.openMenuId.update(cur => cur === id ? null : id);
  }

  /**
   * Returns the state options available for transition (excludes current and Committed).
   * @param item The backlog item.
   * @returns Allowed target states.
   */
  stateOptions(item: BacklogItem): BacklogState[] {
    if (item.state === 'committed') return [];
    return (['new', 'refining', 'ready'] as BacklogState[]).filter(s => s !== item.state);
  }

  /**
   * Human-readable label for a BacklogState.
   * @param state The state to label.
   * @returns Capitalised label.
   */
  stateLabel(state: BacklogState): string {
    switch (state) {
      case 'new':       return 'New';
      case 'refining':  return 'Refining';
      case 'ready':     return 'Ready';
      case 'committed': return 'Committed';
    }
  }

  // ── Inline title edit ─────────────────────────────────────────

  /**
   * Begins inline title editing for an item.
   * @param item The item to edit.
   */
  startEdit(item: BacklogItem): void {
    this.openMenuId.set(null);
    this.editingId.set(item.id);
    this.editTitle.set(item.title);
  }

  /** Saves the edited title and exits editing mode. @param itemId The item being edited. */
  confirmEdit(itemId: string): void {
    const title = this.editTitle().trim();
    if (title) this.backlog.updateTitle(itemId, title);
    this.cancelEdit();
  }

  /** Cancels inline editing without saving. */
  cancelEdit(): void {
    this.editingId.set(null);
    this.editTitle.set('');
  }

  // ── State change ──────────────────────────────────────────────

  /**
   * Transitions the item to a new refinement state.
   * @param itemId The target item.
   * @param state The target state.
   */
  setItemState(itemId: string, state: BacklogState): void {
    this.openMenuId.set(null);
    this.backlog.updateState(itemId, state);
  }

  // ── Delete ────────────────────────────────────────────────────

  /**
   * Requests delete confirmation via the inline strip at the bottom of the card.
   * @param item The item to delete.
   */
  requestDelete(item: BacklogItem): void {
    this.openMenuId.set(null);
    this.pendingDeleteId.set(item.id);
  }

  /** Confirms and executes the pending delete. */
  confirmDelete(): void {
    const id = this.pendingDeleteId();
    if (!id) return;
    this.backlog.deleteItem(id);
    this.pendingDeleteId.set(null);
  }

  /** Cancels the pending delete. */
  cancelDelete(): void {
    this.pendingDeleteId.set(null);
  }

  // ── Dialogs ───────────────────────────────────────────────────

  /** Opens the add-backlog-item dialog. */
  openAddDialog(): void {
    this.dialog.open(AddBacklogItemDialogComponent, {
      title: 'Add backlog item',
      width: '28rem',
    });
  }

  /**
   * Opens the full detail dialog for a user story (title, state, estimate, iteration,
   * documents, acceptance criteria). No-op for Epics/Features or while the row is
   * mid inline-edit.
   * @param item The backlog item to view/edit.
   */
  openDetail(item: BacklogItem): void {
    if (item.type !== 'user-story' || this.editingId() === item.id) return;
    this.dialog.open(BacklogItemDetailDialogComponent, {
      title: 'User story details',
      width: '40rem',
      data: { item },
    });
  }

  /** Opens the promote panel for the given item, pre-selecting the active sprint. @param itemId The item to promote. */
  openPromote(itemId: string): void {
    const active = this.backlog.sprints().find(s => s.isActive);
    this.selectedSprintId.set(active?.id ?? this.backlog.sprints()[0]?.id ?? '');
    this.promotingItemId.set(itemId);
  }

  /** Confirms the promote action. */
  confirmPromote(): void {
    const itemId   = this.promotingItemId();
    const sprintId = this.selectedSprintId();
    if (!itemId || !sprintId) return;
    this.backlog.promoteToSprint(itemId, sprintId);
    this.cancelPromote();
  }

  /** Cancels the promote panel. */
  cancelPromote(): void {
    this.promotingItemId.set(null);
    this.selectedSprintId.set('');
  }
}
