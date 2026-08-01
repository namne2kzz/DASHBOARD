import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { WorkItemMetadataService } from '../../services/work-item-metadata.service';
import { MetadataService } from '../../services/metadata.service';
import { PrivilegeService } from '../../core/services/privilege.service';
import { ToastService } from '../../core/components/toast/toast.service';

/** A catalog group (one metadata key) with its selectable values. */
interface MetadataGroup {
  key: string;
  label: string;
  values: { id: string; value: string }[];
}

/**
 * Assigns repository metadata catalog values (Labels, Components, versions, …) to a work item.
 * Reuses the existing metadata catalog as the option source — one generic control for every key.
 */
@Component({
  selector: 'app-work-item-metadata-editor',
  imports: [FormsModule],
  templateUrl: './work-item-metadata-editor.component.html',
  styleUrl: './work-item-metadata-editor.component.css',
})
export class WorkItemMetadataEditorComponent {
  /** The work item whose metadata is edited. */
  readonly taskId = input.required<string>();
  /** Emitted after a successful save with the item's current "Labels" values, so the board card chips stay in sync. */
  readonly changed = output<string[]>();

  private readonly api       = inject(WorkItemMetadataService);
  private readonly metadata  = inject(MetadataService);
  private readonly privilege = inject(PrivilegeService);
  private readonly toast     = inject(ToastService);

  /** IDs of currently-assigned catalog values. */
  readonly selectedIds = signal<ReadonlySet<string>>(new Set());
  readonly loading     = signal(false);
  readonly saving      = signal(false);

  /** Key whose add-search popover is currently open, or null. */
  readonly addingKey = signal<string | null>(null);
  /** The add-search term. */
  readonly addSearch = signal('');

  /** Whether the current user may change assignments. */
  readonly canEdit = computed(() => this.privilege.canEditWorkItem());

  /** Catalog values grouped by key (excludes the member-only Team Role key; hides empty keys). */
  readonly groups = computed<MetadataGroup[]>(() => {
    const byKey = new Map<string, MetadataGroup>();
    for (const m of this.metadata.items()) {
      if (m.key === 'RepoRole') continue;
      let group = byKey.get(m.key);
      if (!group) { group = { key: m.key, label: m.displayName, values: [] }; byKey.set(m.key, group); }
      group.values.push({ id: m.id, value: m.value });
    }
    return [...byKey.values()].sort((a, b) => a.label.localeCompare(b.label));
  });

  /** True when no catalog values exist at all. */
  readonly isEmpty = computed(() => this.groups().length === 0);

  constructor() {
    // Reload assignments whenever the target task changes.
    effect(() => {
      const id = this.taskId();
      if (id) this._load(id);
    });
  }

  /** @param id A catalog value ID. @returns True when it is currently assigned. */
  isSelected(id: string): boolean {
    return this.selectedIds().has(id);
  }

  /** @param group A catalog group. @returns Its currently-assigned values. */
  selectedValues(group: MetadataGroup): { id: string; value: string }[] {
    return group.values.filter(v => this.isSelected(v.id));
  }

  /**
   * Unselected values in a group matching the current add-search term.
   * @param group The catalog group being added to.
   */
  addMatches(group: MetadataGroup): { id: string; value: string }[] {
    const q = this.addSearch().trim().toLowerCase();
    return group.values.filter(v => !this.isSelected(v.id) && (!q || v.value.toLowerCase().includes(q)));
  }

  /** Opens (or closes) the add-search popover for a key. @param key The metadata key. */
  toggleAdd(key: string): void {
    this.addSearch.set('');
    this.addingKey.update(cur => (cur === key ? null : key));
  }

  /** Closes the add-search popover. */
  closeAdd(): void {
    this.addingKey.set(null);
    this.addSearch.set('');
  }

  /** Adds a value to the work item and clears the search (popover stays open for more). @param id Catalog value ID. */
  add(id: string): void {
    if (this.isSelected(id)) return;
    this.toggle(id);
    this.addSearch.set('');
  }

  /**
   * Toggles a catalog value on the work item and persists the new full set.
   * @param id The catalog value ID to toggle.
   */
  toggle(id: string): void {
    if (!this.canEdit() || this.saving()) return;

    const previous = this.selectedIds();
    const next = new Set(previous);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    this.selectedIds.set(next);

    this.saving.set(true);
    this.api.set(this.taskId(), [...next]).subscribe({
      next: () => {
        this.saving.set(false);
        this.changed.emit(this._labelValues(next));
      },
      error: () => {
        this.selectedIds.set(previous); // revert optimistic change
        this.saving.set(false);
        this.toast.error('Failed to update labels.');
      },
    });
  }

  /** Resolves the "Labels" values among the given selection, for board card chip sync. */
  private _labelValues(ids: ReadonlySet<string>): string[] {
    return this.metadata.items()
      .filter(m => m.key === 'Labels' && ids.has(m.id))
      .map(m => m.value)
      .sort();
  }

  private _load(taskId: string): void {
    this.loading.set(true);
    this.api.get(taskId).subscribe({
      next: items => {
        this.selectedIds.set(new Set(items.map(i => i.metadataId)));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
