import { Component } from '@angular/core';
import { KanbanBoardComponent } from '../../components/kanban-board/kanban-board.component';

@Component({
  selector: 'app-boards-page',
  imports: [KanbanBoardComponent],
  template: '<app-kanban-board />',
})
export class BoardsPageComponent {}
