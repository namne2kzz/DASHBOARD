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
    // Spies are shared across specs — reset so call counts do not leak between them.
    mockDialogRef.close.calls.reset();
    mockBacklog.addItem.calls.reset();

    await TestBed.configureTestingModule({
      imports: [AddBacklogItemDialogComponent],
      providers: [
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

  it('canSubmit is true for an epic once the title has content', () => {
    // An epic sits at the top of the hierarchy, so no parent is required.
    component.type.set('epic');
    component.title.set('My epic');
    expect(component.canSubmit()).toBeTrue();
  });

  it('canSubmit stays false for a non-epic until a parent is picked', () => {
    component.type.set('user-story');
    component.title.set('My story');
    expect(component.canSubmit()).toBeFalse();

    component.parentId.set('feature-1');
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

  it('submit calls addItem and closes dialog when title and parent are set', () => {
    component.type.set('user-story');
    component.title.set('Test story');
    component.parentId.set('feature-1');

    component.submit();

    expect(mockBacklog.addItem).toHaveBeenCalledWith('user-story', 'Test story', 'feature-1');
    expect(mockDialogRef.close).toHaveBeenCalled();
  });

  it('submit does nothing for a non-epic without a parent', () => {
    component.type.set('user-story');
    component.title.set('Orphan story');
    component.submit();
    expect(mockBacklog.addItem).not.toHaveBeenCalled();
  });
});
