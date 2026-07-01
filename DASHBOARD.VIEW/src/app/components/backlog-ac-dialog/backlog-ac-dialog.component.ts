import { ChangeDetectionStrategy, Component, computed, inject, InjectionToken, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DIALOG_REF_TOKEN } from '../../models/dialog.model';
import { BacklogAcDialogData } from '../../models/backlog.model';

@Component({
  selector: 'app-backlog-ac-dialog',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './backlog-ac-dialog.component.html',
  styleUrl: './backlog-ac-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BacklogAcDialogComponent {
  private readonly dialogRef = inject(DIALOG_REF_TOKEN);
  private readonly data      = inject('DIALOG_DATA' as unknown as InjectionToken<BacklogAcDialogData>);

  /** Working copy of criteria rows. Default: one empty row when no existing criteria. */
  readonly rows = signal<string[]>(
    this.data.acceptanceCriteria.length ? [...this.data.acceptanceCriteria] : [''],
  );

  readonly canSave = computed(() => this.rows().some(r => r.trim().length > 0));

  /** Appends a new empty row. */
  addRow(): void {
    this.rows.update(list => [...list, '']);
  }

  /**
   * Removes the row at the given index. Keeps at least one row.
   * @param index Zero-based row index to remove.
   */
  removeRow(index: number): void {
    if (this.rows().length <= 1) {
      this.rows.set(['']);
      return;
    }
    this.rows.update(list => list.filter((_, i) => i !== index));
  }

  /**
   * Updates the text of a specific row.
   * @param index Zero-based row index.
   * @param value New text value.
   */
  updateRow(index: number, value: string): void {
    this.rows.update(list => list.map((r, i) => i === index ? value : r));
  }

  /** Returns the non-empty criteria list to the caller and closes the dialog. */
  save(): void {
    this.dialogRef.close(this.rows().filter(r => r.trim()));
  }

  /** Closes the dialog without returning data. */
  cancel(): void {
    this.dialogRef.close();
  }
}
