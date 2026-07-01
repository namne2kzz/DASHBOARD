import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AddBacklogItemDialogComponent } from './add-backlog-item-dialog.component';
import { DIALOG_REF_TOKEN } from '../../models/dialog.model';
import { BacklogManagementService } from '../../services/backlog-management.service';
import { signal } from '@angular/core';

describe('AddBacklogItemDialogComponent', () => {
  let fixture: ComponentFixture<AddBacklogItemDialogComponent>;
  let component: AddBacklogItemDialogComponent;

  const mockDialogRef = { close: jasmine.createSpy('close') };
  const mockBacklog = {
    epics:    signal([]),
    features: signal([]),
    addItem:  jasmine.createSpy('addItem'),
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AddBacklogItemDialogComponent],
      providers: [
        { provide: DIALOG_REF_TOKEN,          useValue: mockBacklog },
        { provide: BacklogManagementService,  useValue: mockBacklog },
        { provide: DIALOG_REF_TOKEN,          useValue: mockDialogRef },
      ],
    }).compileComponents();

    fixture   = TestBed.createComponent(AddBacklogItemDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('canSubmit is false when title is empty', () => {
    component.title.set('');
    expect(component.canSubmit()).toBeFalse();
  });

  it('canSubmit is true when title has content', () => {
    component.title.set('My story');
    expect(component.canSubmit()).toBeTrue();
  });

  it('cancel closes the dialog without calling addItem', () => {
    component.cancel();
    expect(mockDialogRef.close).toHaveBeenCalled();
    expect(mockBacklog.addItem).not.toHaveBeenCalled();
  });

  it('submit does nothing when title is empty', () => {
    component.title.set('');
    component.submit();
    expect(mockBacklog.addItem).not.toHaveBeenCalled();
  });

  it('submit calls addItem and closes dialog when title is set', () => {
    component.title.set('Test story');
    component.type.set('user-story');
    component.submit();
    expect(mockBacklog.addItem).toHaveBeenCalledWith('user-story', 'Test story', jasmine.anything());
    expect(mockDialogRef.close).toHaveBeenCalled();
  });
});
