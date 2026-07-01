/// <reference types="jasmine" />
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BacklogDocumentsDialogComponent } from './backlog-documents-dialog.component';
import { DIALOG_REF_TOKEN } from '../../models/dialog.model';

describe('BacklogDocumentsDialogComponent', () => {
  let fixture: ComponentFixture<BacklogDocumentsDialogComponent>;
  let component: BacklogDocumentsDialogComponent;

  const mockDialogRef  = { close: jasmine.createSpy('close') };
  const mockDialogData = { itemId: 'item-1', documents: ['Doc A', 'Doc B'] };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BacklogDocumentsDialogComponent],
      providers: [
        { provide: DIALOG_REF_TOKEN, useValue: mockDialogRef  },
        { provide: 'DIALOG_DATA',    useValue: mockDialogData },
      ],
    }).compileComponents();

    fixture   = TestBed.createComponent(BacklogDocumentsDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('initialises docs from dialog data', () => {
    expect(component.docs()).toEqual(['Doc A', 'Doc B']);
  });

  it('addDoc appends a new document', () => {
    component.newDoc.set('New link');
    component.addDoc();
    expect(component.docs()).toContain('New link');
    expect(component.newDoc()).toBe('');
  });

  it('addDoc ignores empty input', () => {
    component.newDoc.set('   ');
    component.addDoc();
    expect(component.docs().length).toBe(2);
  });

  it('deleteDoc removes the correct document', () => {
    component.deleteDoc(0);
    expect(component.docs()).toEqual(['Doc B']);
  });

  it('startEdit sets editIndex and editValue', () => {
    component.startEdit(1);
    expect(component.editIndex()).toBe(1);
    expect(component.editValue()).toBe('Doc B');
  });

  it('confirmEdit updates the document', () => {
    component.startEdit(0);
    component.editValue.set('Updated Doc');
    component.confirmEdit();
    expect(component.docs()[0]).toBe('Updated Doc');
    expect(component.editIndex()).toBeNull();
  });

  it('save closes dialog with the current docs list', () => {
    component.save();
    expect(mockDialogRef.close).toHaveBeenCalledWith(component.docs());
  });

  it('cancel closes dialog without data', () => {
    component.cancel();
    expect(mockDialogRef.close).toHaveBeenCalledWith();
  });
});
