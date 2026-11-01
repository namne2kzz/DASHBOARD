import { CdkDrag, CdkDragDrop, CdkDropList, DragDropModule } from '@angular/cdk/drag-drop';
import { Component, computed, input, output, signal } from '@angular/core';
import type { BoardItem } from '../../models/boards.model';
import type { WipMode } from '../../models/workflow.model';
import { TaskCardComponent } from '../task-card/task-card.component';

@Component({
  selector: 'app-kanban-column',
  imports: [DragDropModule, TaskCardComponent],
  templateUrl: './kanban-column.component.html',
  styleUrl: './kanban-column.component.css',
})
export class KanbanColumnComponent {
  readonly title          = input.required<string>();
  readonly listId         = input.required<string>();
  readonly tasks          = input.required<BoardItem[]>();
  readonly wipLimit       = input<number>(0);
  readonly wipMode        = input<WipMode>('soft');
  readonly wipExceeded    = input(false);
  readonly agingLimitDays = input<number>(0);
  readonly enterPredicate = input<(drag: CdkDrag, drop: CdkDropList) => boolean>(() => true);
  /** When false the cards render normally but cannot be picked up. */
  readonly canMove        = input(true);
  readonly dropped        = output<CdkDragDrop<BoardItem[]>>();
  readonly opened         = output<BoardItem>();

  /** Raised when a card in this column is picked up, so the board can tell every column what is in flight. */
  readonly dragStarted = output<BoardItem>();

  /** Raised when the drag finishes, whether or not it ended in a drop. */
  readonly dragEnded = output<void>();

  /**
   * Pointer x, in viewport coordinates, while a card from this column is being dragged.
   *
   * The board turns this into sideways auto-scroll: dragging towards either edge brings the
   * columns beyond it into view, so a card can reach a column the viewport never showed.
   */
  readonly dragMoved = output<number>();

  /**
   * Whether this column's state is one the dragged card's type can occupy at all.
   *
   * Supplied by the board, which owns the column-to-state mapping. Defaults to true so a
   * column with no drag in progress is never shown as refusing.
   */
  readonly acceptsDraggedType = input(true);

  /** True while a card is being dragged over this column and the column will accept it. */
  readonly isDropTarget = signal(false);

  /**
   * True while the pointer is inside this column's bounds.
   *
   * Tracked separately from {@link isDropTarget} because the CDK does not raise
   * cdkDropListEntered for a list whose enterPredicate refuses — so a column that cannot
   * take the card would otherwise have no way to know the card is over it, and would refuse
   * the drop without ever saying why.
   */
  readonly pointerOver = signal(false);

  /** True while a card is being dragged anywhere on the board. */
  readonly dragInProgress = input(false);

  /**
   * Whether a card dragged here would be refused, and for which of the two reasons.
   *
   * They are different problems and must not be reported with the same words: a full column
   * will accept the card once something leaves, whereas a column whose state the card's type
   * cannot occupy will never accept it.
   */
  readonly refusal = computed<'none' | 'wip' | 'type'>(() => {
    if (!this.acceptsDraggedType()) return 'type';
    if (this.wipMode() === 'hard' && this.wipLimit() > 0 && this.tasks().length >= this.wipLimit()) return 'wip';
    return 'none';
  });

  /** True when a drop here would be refused for any reason. */
  readonly blocksDrop = computed(() => this.refusal() !== 'none');

  /** @param event CDK drag-drop event forwarded from the drop list. */
  onDrop(event: CdkDragDrop<BoardItem[]>): void {
    this.isDropTarget.set(false);
    this.pointerOver.set(false);
    this.dropped.emit(event);
  }
}
