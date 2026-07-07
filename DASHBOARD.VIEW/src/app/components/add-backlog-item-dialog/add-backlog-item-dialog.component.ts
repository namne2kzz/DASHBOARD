import { ChangeDetectionStrategy, Component, computed, effect, ElementRef, HostListener, inject, signal, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DIALOG_REF_TOKEN } from '../../models/dialog.model';
import { BacklogManagementService } from '../../services/backlog-management.service';
import type { BacklogItem, BacklogLevel } from '../../models/backlog.model';

@Component({
  selector: 'app-add-backlog-item-dialog',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './add-backlog-item-dialog.component.html',
  styleUrl: './add-backlog-item-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AddBacklogItemDialogComponent {
  private readonly dialogRef = inject(DIALOG_REF_TOKEN);
  private readonly backlog   = inject(BacklogManagementService);

  @ViewChild('searchInput') private searchInputRef?: ElementRef<HTMLInputElement>;

  readonly types: BacklogLevel[] = ['epic', 'feature', 'user-story'];

  readonly type     = signal<BacklogLevel>('user-story');
  readonly title    = signal('');
  readonly parentId = signal<string | null>(null);

  // ── Searchable parent picker ─────────────────────────────────
  readonly parentDropdownOpen = signal(false);
  readonly parentSearch       = signal('');

  readonly parentOptions = computed<BacklogItem[]>(() => {
    switch (this.type()) {
      case 'feature':    return this.backlog.epics();
      case 'user-story': return this.backlog.features();
      default:           return [];
    }
  });

  readonly filteredParentOptions = computed(() => {
    const q = this.parentSearch().toLowerCase();
    return this.parentOptions().filter(o => o.title.toLowerCase().includes(q));
  });

  readonly selectedParentTitle = computed(() => {
    const id = this.parentId();
    if (!id) return 'Unassigned (optional)';
    return this.parentOptions().find(o => o.id === id)?.title ?? 'Unassigned (optional)';
  });

  readonly parentLabel = computed(() =>
    this.type() === 'feature' ? 'Parent Epic' : 'Parent Feature',
  );

  readonly parentRequired = computed(() => this.type() !== 'epic');

  readonly canSubmit = computed(() =>
    this.title().trim().length > 0 &&
    (!this.parentRequired() || this.parentId() !== null),
  );

  @HostListener('document:click')
  onDocumentClick(): void { this.parentDropdownOpen.set(false); }

  constructor() {
    // Reset parent selection when type changes — default to null (unassigned)
    effect(() => {
      this.parentOptions(); // track dependency
      this.parentId.set(null);
      this.parentSearch.set('');
      this.parentDropdownOpen.set(false);
    });
  }

  /** Opens the parent dropdown and auto-focuses the search input. */
  openDropdown(): void {
    this.parentDropdownOpen.set(true);
    setTimeout(() => this.searchInputRef?.nativeElement.focus(), 0);
  }

  /** Selects a parent item and closes the dropdown. @param id The selected item ID or null to clear. */
  selectParent(id: string | null): void {
    this.parentId.set(id);
    this.parentSearch.set('');
    this.parentDropdownOpen.set(false);
  }

  /** @param level Backlog level. @returns Display label. */
  typeLabel(level: BacklogLevel): string {
    switch (level) {
      case 'epic':       return 'Epic';
      case 'feature':    return 'Feature';
      case 'user-story': return 'User Story';
    }
  }

  /** Creates the backlog item and closes the dialog. */
  submit(): void {
    if (!this.canSubmit()) return;
    this.backlog.addItem(this.type(), this.title().trim(), this.parentId());
    this.dialogRef.close();
  }

  /** Closes the dialog without creating an item. */
  cancel(): void {
    this.dialogRef.close();
  }
}
