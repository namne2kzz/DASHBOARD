import { ComponentFixture, TestBed } from '@angular/core/testing';
import { KanbanColumnComponent } from './kanban-column.component';
import type { BoardItem } from '../../models/boards.model';

describe('KanbanColumnComponent', () => {
  let fixture: ComponentFixture<KanbanColumnComponent>;
  let component: KanbanColumnComponent;

  function card(id: string): BoardItem {
    return {
      id,
      workItemNumber: `DASH-${id}`,
      title: `Item ${id}`,
      type: 'task',
      priority: 'medium',
      labels: [],
      assignedToId: null,
      assignedToName: null,
      stateChangedAt: null,
    } as unknown as BoardItem;
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [KanbanColumnComponent] }).compileComponents();
    fixture = TestBed.createComponent(KanbanColumnComponent);
    component = fixture.componentInstance;

    fixture.componentRef.setInput('title', 'In Progress');
    fixture.componentRef.setInput('listId', 'col-1');
    fixture.componentRef.setInput('tasks', []);
  });

  describe('refusing a drop', () => {
    it('accepts by default', () => {
      fixture.detectChanges();
      expect(component.refusal()).toBe('none');
      expect(component.blocksDrop()).toBeFalse();
    });

    it('refuses a type that cannot occupy this column state, however empty the column is', () => {
      // A test plan has no "In Review" — no amount of free capacity makes this drop legal.
      fixture.componentRef.setInput('acceptsDraggedType', false);
      fixture.componentRef.setInput('tasks', []);
      fixture.detectChanges();

      expect(component.refusal()).toBe('type');
    });

    it('refuses a hard-stop column that has reached its limit', () => {
      fixture.componentRef.setInput('wipMode', 'hard');
      fixture.componentRef.setInput('wipLimit', 2);
      fixture.componentRef.setInput('tasks', [card('1'), card('2')]);
      fixture.detectChanges();

      expect(component.refusal()).toBe('wip');
    });

    it('still accepts into a soft column over its limit, which only warns', () => {
      fixture.componentRef.setInput('wipMode', 'soft');
      fixture.componentRef.setInput('wipLimit', 1);
      fixture.componentRef.setInput('tasks', [card('1'), card('2')]);
      fixture.detectChanges();

      expect(component.blocksDrop()).toBeFalse();
    });

    it('reports the type rule first, since capacity cannot explain that refusal', () => {
      // Both rules fail at once; the message must be the one that stays true tomorrow.
      fixture.componentRef.setInput('acceptsDraggedType', false);
      fixture.componentRef.setInput('wipMode', 'hard');
      fixture.componentRef.setInput('wipLimit', 1);
      fixture.componentRef.setInput('tasks', [card('1')]);
      fixture.detectChanges();

      expect(component.refusal()).toBe('type');
    });

    it('ignores a WIP limit of zero, which means unlimited', () => {
      fixture.componentRef.setInput('wipMode', 'hard');
      fixture.componentRef.setInput('wipLimit', 0);
      fixture.componentRef.setInput('tasks', [card('1'), card('2'), card('3')]);
      fixture.detectChanges();

      expect(component.blocksDrop()).toBeFalse();
    });
  });

  it('clears the drop-target highlight once the card lands', () => {
    fixture.detectChanges();
    component.isDropTarget.set(true);

    component.onDrop({ previousIndex: 0, currentIndex: 0 } as never);

    expect(component.isDropTarget()).toBeFalse();
  });
});
