import { ChangeDetectionStrategy, Component, computed, inject, InjectionToken, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DIALOG_REF_TOKEN } from '../../models/dialog.model';
import { BacklogItemDetailDialogData, BacklogState, TshirtSize } from '../../models/backlog.model';
import { BacklogManagementService } from '../../services/backlog-management.service';

@Component({
  selector: 'app-backlog-item-detail-dialog',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './backlog-item-detail-dialog.component.html',
  styleUrl: './backlog-item-detail-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BacklogItemDetailDialogComponent {
  private readonly dialogRef = inject(DIALOG_REF_TOKEN);
  private readonly data      = inject('DIALOG_DATA' as unknown as InjectionToken<BacklogItemDetailDialogData>);
  readonly backlog = inject(BacklogManagementService);

  private readonly itemId = this.data.item.id;

  readonly title       = signal(this.data.item.title);
  readonly state       = signal<BacklogState>(this.data.item.state);
  readonly sprintId    = signal(this.data.item.sprintId ?? '');
  readonly storyPoints = signal(this.data.item.storyPoints);
  readonly tshirtSize  = signal(this.data.item.tshirtSize);
  readonly documents   = signal<string[]>(
    this.data.item.documents.length ? [...this.data.item.documents] : [''],
  );
  readonly acRows      = signal<string[]>(
    this.data.item.acceptanceCriteria.length ? [...this.data.item.acceptanceCriteria] : [''],
  );

  readonly isCommitted = computed(() => this.data.item.state === 'committed');
  readonly parentTitle = computed(() => this.backlog.parentTitle(this.data.item));
  readonly childCount  = computed(() => this.backlog.childCount(this.itemId));

  /** States selectable from this dialog — Committed can only be reached via the Promote flow. */
  readonly stateOptions: BacklogState[] = ['new', 'refining', 'ready'];

  readonly canSave = computed(() => this.title().trim().length > 0);

  /**
   * Human-readable label for a BacklogState.
   * @param state The state to label.
   * @returns Capitalised label.
   */
  stateLabel(state: BacklogState): string {
    switch (state) {
      case 'new':       return 'New';
      case 'refining':  return 'Refining';
      case 'ready':     return 'Ready';
      case 'committed': return 'Committed';
    }
  }

  asTshirtSize(value: string): TshirtSize {
    return value as TshirtSize;
  }

  /** Appends a new empty document row. */
  addDoc(): void {
    this.documents.update(list => [...list, '']);
  }

  /**
   * Removes a document row. Keeps at least one row.
   * @param index Zero-based position to remove.
   */
  removeDoc(index: number): void {
    if (this.documents().length <= 1) {
      this.documents.set(['']);
      return;
    }
    this.documents.update(list => list.filter((_, i) => i !== index));
  }

  /**
   * Updates the text of a document row.
   * @param index Zero-based row index.
   * @param value New text value.
   */
  updateDoc(index: number, value: string): void {
    this.documents.update(list => list.map((d, i) => i === index ? value : d));
  }

  /** Appends a new empty acceptance-criteria row. */
  addAcRow(): void {
    this.acRows.update(list => [...list, '']);
  }

  /**
   * Removes an acceptance-criteria row. Keeps at least one row.
   * @param index Zero-based row index to remove.
   */
  removeAcRow(index: number): void {
    if (this.acRows().length <= 1) {
      this.acRows.set(['']);
      return;
    }
    this.acRows.update(list => list.filter((_, i) => i !== index));
  }

  /**
   * Updates the text of an acceptance-criteria row.
   * @param index Zero-based row index.
   * @param value New text value.
   */
  updateAcRow(index: number, value: string): void {
    this.acRows.update(list => list.map((r, i) => i === index ? value : r));
  }

  /**
   * Saves edited fields and closes the dialog. Committed items go through the granular
   * title/documents/acceptance-criteria endpoints only — the general update endpoint rejects
   * any write once an item is Committed (state, sprint and estimate are locked in that case).
   */
  save(): void {
    if (!this.canSave()) return;

    const acceptanceCriteria = this.acRows().filter(r => r.trim());
    const documents          = this.documents().filter(d => d.trim());

    if (this.isCommitted()) {
      this.backlog.updateTitle(this.itemId, this.title().trim());
      this.backlog.updateDocuments(this.itemId, documents);
      this.backlog.updateAcceptanceCriteria(this.itemId, acceptanceCriteria);
    } else {
      this.backlog.saveItemDetails(this.itemId, {
        title:              this.title().trim(),
        state:              this.state(),
        sprintId:           this.sprintId() || null,
        storyPoints:        this.storyPoints(),
        tshirtSize:         this.tshirtSize(),
        documents,
        acceptanceCriteria,
      });
    }

    this.dialogRef.close();
  }

  /** Closes the dialog without saving. */
  cancel(): void {
    this.dialogRef.close();
  }
}
