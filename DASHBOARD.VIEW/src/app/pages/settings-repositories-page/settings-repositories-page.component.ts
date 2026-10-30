import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DialogService } from '../../core/components/dialog/dialog.service';
import { RepositoryAdminService } from '../../services/repository-admin.service';
import { RepositoryContextService } from '../../services/repository-context.service';
import { ToastService } from '../../core/components/toast/toast.service';
import { LoadErrorComponent } from '../../components/load-error/load-error.component';
import { LocalDatePipe } from '../../core/pipes/local-date.pipe';
import { UserSelectComponent, UserOption } from '../../components/user-select/user-select.component';
import { NewRepoDialogComponent } from '../../components/new-repo-dialog/new-repo-dialog.component';
import { RepositoryApiDto } from '../../models/repository.model';

/** Which slice of the repository list is on screen. */
type RepoFilter = 'active' | 'archived' | 'all';

/**
 * Settings → Repositories: the administrator's view of every repository in the organization,
 * archived ones included.
 *
 * Distinct from the repo switcher in the sidebar, which only ever shows what the signed-in user
 * can work in. This screen exists to run the set: rename, hand over the Project Manager role, and
 * archive or restore.
 */
@Component({
  selector: 'app-settings-repositories-page',
  standalone: true,
  imports: [FormsModule, LoadErrorComponent, LocalDatePipe, UserSelectComponent],
  templateUrl: './settings-repositories-page.component.html',
  styleUrls: ['./settings-repositories-page.component.css'],
})
export class SettingsRepositoriesPageComponent implements OnInit {
  protected readonly svc     = inject(RepositoryAdminService);
  private  readonly repoCtx  = inject(RepositoryContextService);
  private  readonly toast    = inject(ToastService);
  private  readonly dialog   = inject(DialogService);

  readonly searchQuery  = signal('');
  readonly activeFilter = signal<RepoFilter>('active');

  // ── Edit dialog state ─────────────────────────────────────────
  readonly editing          = signal<RepositoryApiDto | null>(null);
  readonly formName         = signal('');
  readonly formDescription  = signal('');
  readonly formLead         = signal<string | null>(null);
  readonly memberOptions    = signal<UserOption[]>([]);
  readonly membersLoading   = signal(false);
  readonly saving           = signal(false);

  // ── Archive confirmation ──────────────────────────────────────
  /** Repository awaiting archive confirmation, or null when no prompt is open. */
  readonly archiving = signal<RepositoryApiDto | null>(null);

  /** What the user has typed into the confirmation box. */
  readonly archiveConfirmation = signal('');

  /** True while the archive request is in flight. */
  readonly archivePending = signal(false);

  /**
   * True once the typed text matches the repository's code.
   *
   * Typing the code rather than a fixed word like "archive" is what makes this a real check: the
   * same word for every repository becomes muscle memory, whereas the code can only be produced
   * by reading which repository the prompt is actually about.
   */
  readonly canArchive = computed(() => {
    const repo = this.archiving();
    if (!repo || this.archivePending()) return false;
    return this.archiveConfirmation().trim().toUpperCase() === repo.code.toUpperCase();
  });

  readonly filteredRepos = computed(() => {
    const q      = this.searchQuery().toLowerCase().trim();
    const filter = this.activeFilter();

    return this.svc.repositories().filter(r => {
      const matchSearch = !q
        || r.name.toLowerCase().includes(q)
        || r.code.toLowerCase().includes(q);
      const matchFilter =
        filter === 'active'   ? !r.isArchived :
        filter === 'archived' ?  r.isArchived :
        true;
      return matchSearch && matchFilter;
    });
  });

  readonly counts = computed(() => {
    const all = this.svc.repositories();
    return {
      all:      all.length,
      active:   all.filter(r => !r.isArchived).length,
      archived: all.filter(r =>  r.isArchived).length,
    };
  });

  /** True once the edit form differs from the repository it was opened on. */
  readonly isDirty = computed(() => {
    const repo = this.editing();
    if (!repo) return false;
    return this.formName().trim() !== repo.name
      || this.formDescription().trim() !== (repo.description ?? '')
      || this.formLead() !== repo.leadId;
  });

  readonly canSave = computed(() =>
    !!this.formName().trim() && this.isDirty() && !this.saving());

  /** Loads the full list, archived entries included. */
  ngOnInit(): void {
    this.svc.load();
  }

  /** Re-queries the server so the search also reaches repositories not yet loaded. */
  onSearch(): void {
    this.svc.load(this.searchQuery());
  }

  // ── Create ────────────────────────────────────────────────────

  /** Opens the shared new-repository dialog and folds the result into the list. */
  async openCreate(): Promise<void> {
    const ref = this.dialog.open<NewRepoDialogComponent, unknown, RepositoryApiDto>(
      NewRepoDialogComponent,
      { title: 'New Repository', width: '30rem' },
    );

    const repo = await ref.closed;
    if (!repo) return;

    this.svc.repositories.update(list => [...list, repo]);
    // The sidebar switcher keeps its own list, so it has to be told about the new repository.
    this.repoCtx.load();
    this.toast.success(`Repository "${repo.name}" created.`);
  }

  // ── Edit ──────────────────────────────────────────────────────

  /**
   * Opens the edit form for one repository and loads its members for the Project Manager picker.
   * @param repo The repository to edit.
   */
  openEdit(repo: RepositoryApiDto): void {
    this.editing.set(repo);
    this.formName.set(repo.name);
    this.formDescription.set(repo.description ?? '');
    this.formLead.set(repo.leadId);

    this.memberOptions.set([]);
    this.membersLoading.set(true);
    this.svc.members(repo.id).subscribe({
      next: members => {
        this.memberOptions.set(members.map(m => ({
          id:          m.userId,
          name:        m.userName,
          email:       m.userEmail,
          avatarClass: m.avatarClass,
        })));
        this.membersLoading.set(false);
      },
      error: () => {
        this.membersLoading.set(false);
        this.toast.error('Could not load this repository\'s members.');
      },
    });
  }

  /** Closes the edit form, discarding anything typed. */
  closeEdit(): void {
    this.editing.set(null);
    this.saving.set(false);
  }

  /** Saves the edit form. */
  save(): void {
    const repo = this.editing();
    if (!repo || !this.canSave()) return;

    this.saving.set(true);

    // Only sent when it actually changed, so saving a rename never disturbs the role.
    const leadId = this.formLead() !== repo.leadId
      ? this.formLead() ?? undefined
      : undefined;

    this.svc.update(repo.id, {
      name:        this.formName().trim(),
      description: this.formDescription().trim(),
      leadId,
    }).subscribe({
      next: () => {
        // The sidebar shows repository names, so a rename has to reach it too.
        this.repoCtx.load();
        this.toast.success('Repository updated.');
        // Reload rather than patch: a handover changes the Project Manager on the server, and the
        // name it should now display is not something this screen can work out on its own.
        this.svc.load(this.searchQuery());
        this.closeEdit();
      },
      error: () => {
        this.saving.set(false);
        this.toast.error('Could not save the changes.');
      },
    });
  }

  // ── Archive / restore ─────────────────────────────────────────

  /** Opens the archive prompt. @param repo The repository to archive. */
  confirmArchive(repo: RepositoryApiDto): void {
    this.archiving.set(repo);
    this.archiveConfirmation.set('');
  }

  /** Dismisses the archive prompt without archiving. */
  cancelArchive(): void {
    this.archiving.set(null);
    this.archiveConfirmation.set('');
    this.archivePending.set(false);
  }

  /** Archives the repository awaiting confirmation. */
  archive(): void {
    const repo = this.archiving();
    if (!repo || !this.canArchive()) return;

    this.archivePending.set(true);

    this.svc.archive(repo.id).subscribe({
      next: () => {
        this.cancelArchive();
        // It must leave the sidebar switcher as well, not just this list.
        this.repoCtx.load();
        this.toast.success(`"${repo.name}" archived.`);
      },
      error: () => {
        this.archivePending.set(false);
        this.toast.error('Could not archive the repository.');
      },
    });
  }

  /** Restores an archived repository. @param repo The repository to restore. */
  unarchive(repo: RepositoryApiDto): void {
    this.svc.unarchive(repo.id).subscribe({
      next: () => {
        this.repoCtx.load();
        this.toast.success(`"${repo.name}" restored.`);
      },
      error: () => this.toast.error('Could not restore the repository.'),
    });
  }
}
