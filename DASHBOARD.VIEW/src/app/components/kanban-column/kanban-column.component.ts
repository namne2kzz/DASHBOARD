import { CdkDrag, CdkDragDrop, CdkDropList, DragDropModule } from '@angular/cdk/drag-drop';
import { Component, input, output } from '@angular/core';
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
  readonly dropped        = output<CdkDragDrop<BoardItem[]>>();
  readonly opened         = output<BoardItem>();

  /** @param event CDK drag-drop event forwarded from the drop list. */
  onDrop(event: CdkDragDrop<BoardItem[]>): void {
    this.dropped.emit(event);
  }
}
