import { Component } from '@angular/core';
import { KanbanBoardComponent } from '../../components/kanban-board/kanban-board.component';

@Component({
  selector: 'app-boards-page',
  imports: [KanbanBoardComponent],
  template: '<app-kanban-board />',
  // Propagate the shell's flex-col context down so kanban-board's flex-1 fills height
  host: { class: 'min-h-0 flex-1 flex flex-col' },
})
export class BoardsPageComponent {}
