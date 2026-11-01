import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideToastr } from 'ngx-toastr';
import { signal } from '@angular/core';
import { WorkItemDetailPageComponent } from './work-item-detail-page.component';
import { PrivilegeService } from '../../core/services/privilege.service';
import { SprintTaskDetailService } from '../../services/sprint-task-detail.service';

describe('WorkItemDetailPageComponent', () => {
  let component: WorkItemDetailPageComponent;
  let fixture: ComponentFixture<WorkItemDetailPageComponent>;
  let canEditWorkItem: ReturnType<typeof signal<boolean>>;
  let canAssignWorkItem: ReturnType<typeof signal<boolean>>;
  let detail: jasmine.SpyObj<SprintTaskDetailService>;

  beforeEach(async () => {
    canEditWorkItem   = signal(true);
    canAssignWorkItem = signal(true);

    await TestBed.configureTestingModule({
      imports: [WorkItemDetailPageComponent],
      providers: [
        provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
        provideNoopAnimations(), provideToastr(),
        { provide: PrivilegeService, useValue: { canEditWorkItem, canAssignWorkItem } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(WorkItemDetailPageComponent);
    component = fixture.componentInstance;
    detail = TestBed.inject(SprintTaskDetailService) as jasmine.SpyObj<SprintTaskDetailService>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('permission gating', () => {
    /** Puts a task in place so the state/assignee handlers get past their own null checks. */
    function withTask(): void {
      spyOn(component, 'taskId').and.returnValue('task-1');
      spyOn(component, 'task').and.returnValue({ id: 'task-1', sprintId: 'sprint-1' } as never);
    }

    it('will not change state without EditWorkItem', () => {
      withTask();
      const changeState = spyOn(detail, 'changeState');
      canEditWorkItem.set(false);

      component.onStateChange('3');

      expect(changeState).not.toHaveBeenCalled();
    });

    it('will not reassign without AssignWorkItem', () => {
      withTask();
      const changeAssignee = spyOn(detail, 'changeAssignee');
      canAssignWorkItem.set(false);

      component.selectAssignee('u2');

      expect(changeAssignee).not.toHaveBeenCalled();
    });

    it('keeps reassignment available to a role that may assign but not edit', () => {
      // AssignWorkItem is its own permission on the server, so holding only it must still work.
      withTask();
      const changeAssignee = spyOn(detail, 'changeAssignee');
      canEditWorkItem.set(false);

      component.selectAssignee('u2');

      expect(changeAssignee).toHaveBeenCalled();
    });

    it('never lets Save become available without EditWorkItem', () => {
      component.title.set('A real title');
      canEditWorkItem.set(false);

      expect(component.canSave()).toBeFalse();
    });
  });
});
