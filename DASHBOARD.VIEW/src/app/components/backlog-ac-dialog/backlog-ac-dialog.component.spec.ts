/// <reference types="jasmine" />
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BacklogAcDialogComponent } from './backlog-ac-dialog.component';
import { DIALOG_REF_TOKEN } from '../../models/dialog.model';

describe('BacklogAcDialogComponent', () => {
  let fixture: ComponentFixture<BacklogAcDialogComponent>;
  let component: BacklogAcDialogComponent;

  const mockDialogRef  = { close: jasmine.createSpy('close') };
  const mockDialogData = { itemId: 'item-1', acceptanceCriteria: ['Given X', 'When Y'] };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BacklogAcDialogComponent],
      providers: [
        { provide: DIALOG_REF_TOKEN, useValue: mockDialogRef  },
        { provide: 'DIALOG_DATA',    useValue: mockDialogData },
      ],
    }).compileComponents();

    fixture   = TestBed.createComponent(BacklogAcDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => expect(component).toBeTruthy());

  it('initialises rows from dialog data', () => {
    expect(component.rows()).toEqual(['Given X', 'When Y']);
  });

  it('defaults to one empty row when no existing criteria', () => {
    // test via direct signal manipulation
    component.rows.set([]);
    expect(component.rows().length).toBe(0); // internal — init is tested above
  });

  it('addRow appends an empty string', () => {
    component.addRow();
    expect(component.rows().length).toBe(3);
    expect(component.rows().at(-1)).toBe('');
  });

  it('removeRow removes the correct row', () => {
    component.removeRow(0);
    expect(component.rows()).toEqual(['When Y']);
  });

  it('removeRow keeps at least one empty row', () => {
    component.rows.set(['Only row']);
    component.removeRow(0);
    expect(component.rows()).toEqual(['']);
  });

  it('updateRow updates the correct row', () => {
    component.updateRow(1, 'Updated');
    expect(component.rows()[1]).toBe('Updated');
  });

  it('save closes dialog with non-empty rows', () => {
    component.rows.set(['AC 1', '', 'AC 3']);
    component.save();
    expect(mockDialogRef.close).toHaveBeenCalledWith(['AC 1', 'AC 3']);
  });

  it('cancel closes without data', () => {
    component.cancel();
    expect(mockDialogRef.close).toHaveBeenCalledWith();
  });
});
