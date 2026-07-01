import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { MetadataService } from '../../services/metadata.service';
import { PrivilegeService } from '../../core/services/privilege.service';
import { RepositoryContextService } from '../../services/repository-context.service';
import { DateTimeService } from '../../core/services/date-time.service';
import { MetadataDto, CreateMetadataPayload, UpdateMetadataPayload } from '../../models/metadata.model';

type ActivePanel = 'create' | 'edit' | null;

/** A group of metadata entries sharing the same key. */
interface MetadataGroup {
  key:         string;
  displayName: string;
  items:       MetadataDto[];
}

/** Settings page to manage the repository metadata catalog (global + repo entries). */
@Component({
  selector: 'app-settings-metadata-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './settings-metadata-page.component.html',
  styleUrls: ['./settings-metadata-page.component.scss'],
})
export class SettingsMetadataPageComponent {
  protected readonly metadataService = inject(MetadataService);
  protected readonly privilege       = inject(PrivilegeService);
  protected readonly repoCtx         = inject(RepositoryContextService);
  protected readonly dt              = inject(DateTimeService);

  /** Entries grouped by metadata key for display. */
  readonly groups = computed<MetadataGroup[]>(() => {
    const byKey = new Map<string, MetadataGroup>();
    for (const m of this.metadataService.items()) {
      let g = byKey.get(m.key);
      if (!g) { g = { key: m.key, displayName: m.displayName, items: [] }; byKey.set(m.key, g); }
      g.items.push(m);
    }
    return [...byKey.values()].sort((a, b) => a.displayName.localeCompare(b.displayName));
  });

  // ── Modal state ───────────────────────────────────────────────
  readonly activePanel = signal<ActivePanel>(null);
  readonly editing     = signal<MetadataDto | null>(null);

  // ── Form ──────────────────────────────────────────────────────
  readonly formKey      = signal('');
  readonly formValue    = signal('');
  readonly formIsGlobal = signal(false);

  /** True when the current user may create/edit/delete the given entry. @param entry Target entry. @returns Whether the action is allowed. */
  canManage(entry: MetadataDto): boolean {
    return entry.isGlobal ? this.privilege.isGlobalAdmin() : this.privilege.canManageMembers();
  }

  /** True when the user can create any metadata (repo-level at minimum). */
  readonly canCreate = computed(() => this.privilege.canManageMembers() || this.privilege.isGlobalAdmin());

  /** Opens the create panel with defaults. */
  openCreate(): void {
    if (!this.canCreate()) return;
    this.editing.set(null);
    this.formKey.set(this.metadataService.keys()[0]?.key ?? '');
    this.formValue.set('');
    this.formIsGlobal.set(false);
    this.activePanel.set('create');
  }

  /** Opens the edit panel for an entry (value only). @param entry Entry to edit. */
  openEdit(entry: MetadataDto): void {
    if (!this.canManage(entry)) return;
    this.editing.set(entry);
    this.formKey.set(entry.key);
    this.formValue.set(entry.value);
    this.formIsGlobal.set(entry.isGlobal);
    this.activePanel.set('edit');
  }

  /** Closes the active panel. */
  closePanel(): void { this.activePanel.set(null); }

  /** Submits the create/edit form. No-op when value is blank or no repo is selected. */
  submit(): void {
    const repoId = this.repoCtx.selectedRepoId();
    const value  = this.formValue().trim();
    if (!repoId || !value) return;

    const editing = this.editing();
    let obs$: Observable<unknown>;
    if (editing) {
      const payload: UpdateMetadataPayload = { value };
      obs$ = this.metadataService.update(repoId, editing.id, payload);
    } else {
      if (!this.formKey()) return;
      const payload: CreateMetadataPayload = {
        key:      this.formKey(),
        value,
        isGlobal: this.privilege.isGlobalAdmin() && this.formIsGlobal(),
      };
      obs$ = this.metadataService.create(repoId, payload);
    }
    obs$.subscribe({ next: () => this.closePanel() });
  }

  /** Soft-deletes an entry after confirmation. @param entry Entry to delete. */
  remove(entry: MetadataDto): void {
    if (!this.canManage(entry)) return;
    const repoId = this.repoCtx.selectedRepoId();
    if (!repoId || !confirm(`Delete "${entry.value}" from ${entry.displayName}?`)) return;
    this.metadataService.delete(repoId, entry.id).subscribe();
  }
}
