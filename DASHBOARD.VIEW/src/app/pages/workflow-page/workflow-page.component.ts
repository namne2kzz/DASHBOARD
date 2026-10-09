import { Component, HostListener, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { WorkflowService } from '../../services/workflow.service';
import type { BoardColumn, NewColumnForm, SprintTaskStateKey } from '../../models/workflow.model';
import { SPRINT_TASK_STATE_OPTIONS } from '../../core/constants/system.constant';
import { AddColumnModalComponent } from '../../components/add-column-modal/add-column-modal.component';

/** Human-readable labels for each sprint-task state key. */
const STATE_KEY_LABELS: Record<SprintTaskStateKey, string> = {
  'open':        'Open',
  'todo':        'To Do',
  'in-progress': 'In Progress',
  'in-review':   'In Review',
  'verified':    'Verified',
  'running':     'Running',
  'done':        'Done',
  'passed':      'Passed',
  'failed':      'Failed',
  'closed':      'Closed',
};

@Component({
  selector: 'app-workflow-page',
  imports: [FormsModule, AddColumnModalComponent],
  templateUrl: './workflow-page.component.html',
  styleUrl: './workflow-page.component.css',
})
export class WorkflowPageComponent {
  readonly board        = inject(WorkflowService);
  readonly stateOptions = SPRINT_TASK_STATE_OPTIONS;

  // ── Section menu ──────────────────────────────────────────────────────────
  readonly sectionMenuOpen = signal(false);

  // ── Add column modal ──────────────────────────────────────────────────────
  readonly addModalOpen = signal(false);

  // ── Inline column edit ────────────────────────────────────────────────────
  readonly editColId    = signal<string | null>(null);
  readonly editColName  = signal('');
  readonly editColState = signal<SprintTaskStateKey>('open');

  @HostListener('document:click')
  onDocumentClick(): void {
    this.closeColumnEdit();
    this.sectionMenuOpen.set(false);
  }

  /** Toggles the section-level "..." menu. @param e Mouse event (stops propagation). */
  openSectionMenu(e: MouseEvent): void {
    e.stopPropagation();
    this.sectionMenuOpen.update(v => !v);
  }

  /** Opens the add-column modal. */
  startAddColumn(): void {
    this.sectionMenuOpen.set(false);
    this.addModalOpen.set(true);
  }

  /** Receives the form data from the modal and delegates to the service. @param form Validated form data. */
  onColumnSaved(form: NewColumnForm): void {
    this.board.createColumn(form.name, form.mappedState, form.wipLimit, form.wipMode, form.agingLimitDays);
    this.addModalOpen.set(false);
  }

  /** Opens inline edit for a column row. @param col The column to edit. @param e Mouse event (stops propagation). */
  openColumnEdit(col: BoardColumn, e: MouseEvent): void {
    e.stopPropagation();
    if (this.editColId() === col.id) { this.closeColumnEdit(); return; }
    this.editColId.set(col.id);
    this.editColName.set(col.name);
    this.editColState.set(col.mappedState);
  }

  /** Saves name + state mapping then closes the edit row. @param columnId The column being saved. */
  saveColumnEdit(columnId: string): void {
    const name = this.editColName().trim();
    if (!name) return;
    this.board.renameAndRemap(columnId, name, this.editColState());
    this.closeColumnEdit();
  }

  /** Closes the inline edit row without saving. */
  closeColumnEdit(): void { this.editColId.set(null); }

  /** Prevents document:click from closing the edit row when clicking inside it. @param e Mouse event. @param columnId The row's column id. */
  stopIfEditing(e: MouseEvent, columnId: string): void {
    if (this.editColId() === columnId) e.stopPropagation();
  }

  /** @param state Sprint-task state key. @returns Display label for the state badge. */
  stateLabelFor(state: SprintTaskStateKey): string {
    return STATE_KEY_LABELS[state] ?? state;
  }
}
