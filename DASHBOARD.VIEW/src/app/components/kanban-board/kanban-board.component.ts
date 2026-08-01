import { CdkDragDrop, DragDropModule, type CdkDrag, type CdkDropList } from '@angular/cdk/drag-drop';
import { NgClass } from '@angular/common';
import { computed, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import type { BoardItem } from '../../models/boards.model';
import type { BoardColumn, WipMode } from '../../models/workflow.model';
import { SprintBoardService, STATE_KEY_TO_API, type QueryRow } from '../../services/sprint-board.service';
import { WorkflowService } from '../../services/workflow.service';
import { PrivilegeService } from '../../core/services/privilege.service';
import { DialogService } from '../../core/components/dialog/dialog.service';
import { KanbanColumnComponent } from '../kanban-column/kanban-column.component';
import { CreateWorkItemDialogComponent } from '../create-workitem-dialog/create-workitem-dialog.component';
import { SprintTaskDetailDialogComponent } from '../sprint-task-detail-dialog/sprint-task-detail-dialog.component';
import { SprintTaskApiType } from '../../core/enums/system.enum';

@Component({
  selector: 'app-kanban-board',
  imports: [DragDropModule, FormsModule, NgClass, KanbanColumnComponent],
  templateUrl: './kanban-board.component.html',
  styleUrl: './kanban-board.component.css',
})
export class KanbanBoardComponent {
  readonly board      = inject(SprintBoardService);
  readonly workflow = inject(WorkflowService);
  readonly privilege  = inject(PrivilegeService);
  private readonly dialog = inject(DialogService);

  /** Exposed so the template @switch can compare against enum members. */
  readonly sprintTaskApiType = SprintTaskApiType;

  readonly searchExpanded      = signal(false);
  readonly queryPanelOpen      = signal(false);
  readonly newWorkItemMenuOpen = signal(false);

  /** Dynamic query rows live on the board service so filtering can consume them. */
  get queryRows() { return this.board.queryRows; }

  readonly newWorkItemTypes = [
    { value: SprintTaskApiType.Task,     label: 'Task' },
    { value: SprintTaskApiType.Bug,      label: 'Bug' },
    { value: SprintTaskApiType.TestPlan, label: 'Test Plan' },
  ];

  readonly newWorkItemIconClass: Record<SprintTaskApiType, string> = {
    [SprintTaskApiType.UserStory]: 'text-sky-400 ring-sky-500/35 bg-sky-500/10',
    [SprintTaskApiType.Task]:      'text-amber-300 ring-amber-400/35 bg-amber-400/10',
    [SprintTaskApiType.Bug]:       'text-rose-400 ring-rose-500/35 bg-rose-500/10',
    [SprintTaskApiType.TestPlan]:  'text-violet-400 ring-violet-500/35 bg-violet-500/10',
  };

  readonly criteriaOptions = [
    { value: 'title',      label: 'Title' },
    { value: 'type',       label: 'Type' },
    { value: 'priority',   label: 'Priority' },
    { value: 'state',      label: 'State' },
    { value: 'assignedTo', label: 'Assigned To' },
    { value: 'remaining',  label: 'Remaining Work' },
    { value: 'labels',     label: 'Labels' },
  ];

  readonly operationOptions = [
    { value: 'equals',       label: 'Equals' },
    { value: 'not-equals',   label: 'Not Equals' },
    { value: 'contains',     label: 'Contains' },
    { value: 'not-contains', label: 'Not Contains' },
    { value: 'starts-with',  label: 'Starts With' },
    { value: 'is-empty',     label: 'Is Empty' },
    { value: 'is-not-empty', label: 'Is Not Empty' },
  ];

  /**
   * Map of column.id → filtered BoardItems for that column.
   * Recalculates only when columns or filtered task list changes.
   */
  readonly colItemsMap = computed(() => {
    const cols  = this.workflow.columns();
    const items = this.board.filteredItems();
    const map   = new Map<string, BoardItem[]>();
    for (const col of cols) {
      const apiState = STATE_KEY_TO_API[col.mappedState];
      map.set(col.id, items.filter(i => i.apiState === apiState));
    }
    return map;
  });

  /** Map of column.id → WIP exceeded status and mode. Recalculates when columns or items change. */
  readonly wipStatusMap = computed(() => {
    const cols   = this.workflow.columns();
    const map    = this.colItemsMap();
    const result = new Map<string, { exceeded: boolean; mode: WipMode; limit: number }>();
    for (const col of cols) {
      const count    = (map.get(col.id) ?? []).length;
      const exceeded = col.wipLimit > 0 && count >= col.wipLimit;
      result.set(col.id, { exceeded, mode: col.wipMode, limit: col.wipLimit });
    }
    return result;
  });

  private readonly _predicateCache = new Map<string, (d: CdkDrag, l: CdkDropList) => boolean>();

  /**
   * Returns a stable CDK enter-predicate for the given column.
   * Hard-stop columns at their limit return false to block drops.
   * @param colId Column to evaluate.
   * @returns Predicate function for CdkDropList.
   */
  getEnterPredicate(colId: string): (drag: CdkDrag, drop: CdkDropList) => boolean {
    if (!this._predicateCache.has(colId)) {
      this._predicateCache.set(colId, () => {
        const wip = this.wipStatusMap().get(colId);
        return !(wip?.exceeded && wip.mode === 'hard');
      });
    }
    return this._predicateCache.get(colId)!;
  }

  /** @returns Count of query rows with a selected criteria. */
  get activeQueryCount(): number {
    return this.board.queryRows().filter(r => r.criteria).length;
  }

  /** Appends a blank query row. */
  addQueryRow(): void {
    this.board.queryRows.update(rows => [...rows, { logicalOperator: 'and', criteria: '', operation: 'equals', value: '' }]);
  }

  /** Removes the row at the given index, keeping at least one blank row. @param index Row to remove. */
  removeQueryRow(index: number): void {
    this.board.queryRows.update(rows => {
      const next = rows.filter((_, i) => i !== index);
      return next.length ? next : [{ logicalOperator: 'and', criteria: '', operation: 'equals', value: '' }];
    });
  }

  /**
   * Immutably updates one field on a query row.
   * @param index Row index.
   * @param field Field key.
   * @param value New value.
   */
  updateQueryRow(index: number, field: keyof QueryRow, value: string): void {
    this.board.queryRows.update(rows =>
      rows.map((row, i) => (i === index ? { ...row, [field]: value } : row)),
    );
  }

  /** Clears all query rows. */
  clearQuery(): void {
    this.board.clearQuery();
  }

  /**
   * Handles card drop events from columns.
   * Uses cdkDragData to identify the item and the target column's mappedState.
   * @param event CDK drag-drop event.
   * @param targetCol Destination column config from WorkflowService.
   */
  onDrop(event: CdkDragDrop<BoardItem[]>, targetCol: BoardColumn): void {
    if (!this.privilege.canEditWorkItem()) return;
    if (event.previousContainer === event.container) return;
    const wip = this.wipStatusMap().get(targetCol.id);
    if (wip?.exceeded && wip.mode === 'hard') return;
    const item = event.item.data as BoardItem;
    this.board.moveItemToState(item, targetCol.mappedState);
  }

  /** Opens the create-work-item dialog pre-set to the chosen type and closes the hover menu. @param type Work item type selected from the menu. */
  openCreateWorkItem(type: SprintTaskApiType): void {
    this.newWorkItemMenuOpen.set(false);
    this.dialog.open(CreateWorkItemDialogComponent, {
      title: 'New work item',
      width: '40rem',
      data:  { type },
    });
  }

  /** Opens the task detail dialog for a clicked card. @param item The board item to show detail for. */
  openDetail(item: BoardItem): void {
    this.dialog.open(SprintTaskDetailDialogComponent, {
      title: item.workItemNumber,
      width: '44rem',
      data:  { taskId: item.id },
    });
  }
}
