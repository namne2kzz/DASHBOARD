import { ChangeDetectionStrategy, Component, computed, inject, InjectionToken, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DIALOG_REF_TOKEN } from '../../models/dialog.model';
import { BacklogDocumentsDialogData } from '../../models/backlog.model';

@Component({
  selector: 'app-backlog-documents-dialog',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './backlog-documents-dialog.component.html',
  styleUrl: './backlog-documents-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BacklogDocumentsDialogComponent {
  private readonly dialogRef = inject(DIALOG_REF_TOKEN);
  private readonly data      = inject('DIALOG_DATA' as unknown as InjectionToken<BacklogDocumentsDialogData>);

  readonly docs       = signal<string[]>([...this.data.documents]);
  readonly newDoc     = signal('');
  readonly editIndex  = signal<number | null>(null);
  readonly editValue  = signal('');

  readonly isEmpty   = computed(() => this.docs().length === 0);
  readonly canAddNew = computed(() => this.newDoc().trim().length > 0);

  /** Appends the new document title to the list and clears the input. */
  addDoc(): void {
    const val = this.newDoc().trim();
    if (!val) return;
    this.docs.update(list => [...list, val]);
    this.newDoc.set('');
  }

  /**
   * Begins inline editing of an existing document.
   * @param index Zero-based position of the document to edit.
   */
  startEdit(index: number): void {
    this.editIndex.set(index);
    this.editValue.set(this.docs()[index]);
  }

  /** Saves the edited value back to the list, ignoring empty input. */
  confirmEdit(): void {
    const idx = this.editIndex();
    if (idx === null) return;
    const val = this.editValue().trim();
    if (val) this.docs.update(list => list.map((d, i) => i === idx ? val : d));
    this.cancelEdit();
  }

  /** Cancels inline editing without saving. */
  cancelEdit(): void {
    this.editIndex.set(null);
    this.editValue.set('');
  }

  /**
   * Removes a document from the list.
   * @param index Zero-based position of the document to remove.
   */
  deleteDoc(index: number): void {
    if (this.editIndex() === index) this.cancelEdit();
    this.docs.update(list => list.filter((_, i) => i !== index));
  }

  /** Returns the updated document list to the caller and closes the dialog. */
  save(): void {
    this.dialogRef.close(this.docs());
  }

  /** Closes the dialog without returning data. */
  cancel(): void {
    this.dialogRef.close();
  }
}
