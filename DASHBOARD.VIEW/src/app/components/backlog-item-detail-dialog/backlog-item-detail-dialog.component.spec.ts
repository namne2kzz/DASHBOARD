/// <reference types="jasmine" />
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { BacklogItemDetailDialogComponent } from './backlog-item-detail-dialog.component';
import { DIALOG_REF_TOKEN } from '../../models/dialog.model';
import { BacklogManagementService } from '../../services/backlog-management.service';
import { BacklogItem } from '../../models/backlog.model';

describe('BacklogItemDetailDialogComponent', () => {
  let fixture: ComponentFixture<BacklogItemDetailDialogComponent>;
  let component: BacklogItemDetailDialogComponent;

  const item: BacklogItem = {
    id: 'story-1',
    type: 'user-story',
    title: 'Invite an external user by email',
    parentId: 'feature-1',
    rank: 1,
    sprintId: null,
    sprintName: null,
    state: 'ready',
    storyPoints: 5,
    tshirtSize: null,
    acceptanceCriteria: ['Given X, When Y, Then Z'],
    documents: ['Spec.docx'],
  };

  const mockDialogRef = { close: jasmine.createSpy('close') };
  const mockBacklog = {
    sprints:        signal([]),
    estimateMode:   signal<'fibonacci' | 'tshirt'>('fibonacci'),
    fibonacciPoints: [1, 2, 3, 5, 8, 13],
    tshirtSizes:     ['XS', 'S', 'M', 'L', 'XL'],
    parentTitle:     jasmine.createSpy('parentTitle').and.returnValue('Invitations'),
    childCount:      jasmine.createSpy('childCount').and.returnValue(0),
    saveItemDetails: jasmine.createSpy('saveItemDetails'),
    updateTitle:     jasmine.createSpy('updateTitle'),
    updateDocuments: jasmine.createSpy('updateDocuments'),
    updateAcceptanceCriteria: jasmine.createSpy('updateAcceptanceCriteria'),
  };

  beforeEach(async () => {
    // Spies are shared across specs in this block — reset so call counts do not leak.
    mockDialogRef.close.calls.reset();
    mockBacklog.saveItemDetails.calls.reset();
    mockBacklog.updateTitle.calls.reset();
    mockBacklog.updateDocuments.calls.reset();
    mockBacklog.updateAcceptanceCriteria.calls.reset();

    await TestBed.configureTestingModule({
      imports: [BacklogItemDetailDialogComponent],
      providers: [
        { provide: DIALOG_REF_TOKEN,         useValue: mockDialogRef },
        { provide: BacklogManagementService, useValue: mockBacklog },
        { provide: 'DIALOG_DATA',            useValue: { item } },
      ],
    }).compileComponents();

    fixture   = TestBed.createComponent(BacklogItemDetailDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => expect(component).toBeTruthy());

  it('initialises fields from dialog data', () => {
    expect(component.title()).toBe(item.title);
    expect(component.state()).toBe('ready');
    expect(component.storyPoints()).toBe(5);
    expect(component.documents()).toEqual(['Spec.docx']);
    expect(component.acRows()).toEqual(['Given X, When Y, Then Z']);
  });

  it('canSave is false when title is cleared', () => {
    component.title.set('   ');
    expect(component.canSave()).toBeFalse();
  });

  it('addDoc appends an empty row', () => {
    component.addDoc();
    expect(component.documents()).toEqual(['Spec.docx', '']);
  });

  it('updateDoc updates the correct row', () => {
    component.addDoc();
    component.updateDoc(1, 'New doc');
    expect(component.documents()).toEqual(['Spec.docx', 'New doc']);
  });

  it('removeDoc drops the row entirely', () => {
    // Unlike removeAcRow, the document list is allowed to be empty.
    component.removeDoc(0);
    expect(component.documents()).toEqual([]);
  });

  it('addAcRow appends an empty row', () => {
    component.addAcRow();
    expect(component.acRows().length).toBe(2);
    expect(component.acRows().at(-1)).toBe('');
  });

  it('removeAcRow keeps at least one row', () => {
    component.removeAcRow(0);
    expect(component.acRows()).toEqual(['']);
  });

  it('save sends all edited fields and closes the dialog', () => {
    component.title.set('Updated title');
    component.storyPoints.set(8);
    component.save();

    expect(mockBacklog.saveItemDetails).toHaveBeenCalledWith('story-1', {
      title:              'Updated title',
      state:              'ready',
      sprintId:           null,
      storyPoints:        8,
      tshirtSize:         null,
      documents:          ['Spec.docx'],
      acceptanceCriteria: ['Given X, When Y, Then Z'],
    });
    expect(mockDialogRef.close).toHaveBeenCalled();
  });

  it('save does nothing when title is empty', () => {
    component.title.set('');
    component.save();
    expect(mockBacklog.saveItemDetails).not.toHaveBeenCalled();
  });

  it('cancel closes without saving', () => {
    component.cancel();
    expect(mockDialogRef.close).toHaveBeenCalled();
    expect(mockBacklog.saveItemDetails).not.toHaveBeenCalled();
  });
});

describe('BacklogItemDetailDialogComponent — Committed item', () => {
  // The backend rejects the general update endpoint once an item is Committed
  // (see UpdateBacklogItemCommandHandler) — Save must route through the granular
  // title/documents/acceptance-criteria endpoints instead for these items.
  let fixture: ComponentFixture<BacklogItemDetailDialogComponent>;
  let component: BacklogItemDetailDialogComponent;

  const committedItem: BacklogItem = {
    id: 'story-2',
    type: 'user-story',
    title: 'Already promoted story',
    parentId: 'feature-1',
    rank: 1,
    sprintId: 'sprint-1',
    sprintName: 'Sprint 1',
    state: 'committed',
    storyPoints: 8,
    tshirtSize: null,
    acceptanceCriteria: ['Given A, When B, Then C'],
    documents: ['Doc.docx'],
  };

  const mockDialogRef = { close: jasmine.createSpy('close') };
  const mockBacklog = {
    sprints:        signal([]),
    estimateMode:   signal<'fibonacci' | 'tshirt'>('fibonacci'),
    fibonacciPoints: [1, 2, 3, 5, 8, 13],
    tshirtSizes:     ['XS', 'S', 'M', 'L', 'XL'],
    parentTitle:     jasmine.createSpy('parentTitle').and.returnValue('Invitations'),
    childCount:      jasmine.createSpy('childCount').and.returnValue(0),
    saveItemDetails: jasmine.createSpy('saveItemDetails'),
    updateTitle:     jasmine.createSpy('updateTitle'),
    updateDocuments: jasmine.createSpy('updateDocuments'),
    updateAcceptanceCriteria: jasmine.createSpy('updateAcceptanceCriteria'),
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BacklogItemDetailDialogComponent],
      providers: [
        { provide: DIALOG_REF_TOKEN,         useValue: mockDialogRef },
        { provide: BacklogManagementService, useValue: mockBacklog },
        { provide: 'DIALOG_DATA',            useValue: { item: committedItem } },
      ],
    }).compileComponents();

    fixture   = TestBed.createComponent(BacklogItemDetailDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('isCommitted is true', () => expect(component.isCommitted()).toBeTrue());

  it('save uses the granular endpoints instead of saveItemDetails', () => {
    component.title.set('Fixed typo in title');
    component.save();

    expect(mockBacklog.updateTitle).toHaveBeenCalledWith('story-2', 'Fixed typo in title');
    expect(mockBacklog.updateDocuments).toHaveBeenCalledWith('story-2', ['Doc.docx']);
    expect(mockBacklog.updateAcceptanceCriteria).toHaveBeenCalledWith('story-2', ['Given A, When B, Then C']);
    expect(mockBacklog.saveItemDetails).not.toHaveBeenCalled();
    expect(mockDialogRef.close).toHaveBeenCalled();
  });
});
