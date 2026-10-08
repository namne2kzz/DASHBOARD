import { Component, inject, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RepositoryContextService } from '../../services/repository-context.service';
import { SystemUsersService } from '../../services/system-users.service';
import { AuthService } from '../../services/auth.service';
import { DIALOG_REF_TOKEN } from '../../models/dialog.model';
import { ToastService } from '../../core/components/toast/toast.service';
import { UserPickerItem } from '../../models/user.model';

interface DropdownRect { top: number; left: number; width: number; }

@Component({
  selector: 'app-new-repo-dialog',
  standalone: true,
  imports: [FormsModule, NgClass],
  templateUrl: './new-repo-dialog.component.html',
  styleUrl: './new-repo-dialog.component.scss',
})
export class NewRepoDialogComponent {
  private readonly repoCtx   = inject(RepositoryContextService);
  readonly usersSvc  = inject(SystemUsersService);
  private readonly dialogRef = inject(DIALOG_REF_TOKEN);
  private readonly toast     = inject(ToastService);
  private readonly auth      = inject(AuthService);

  /** The organization this repository will belong to (the caller's org). Read-only. */
  readonly orgAlias = this.auth.currentUser()?.orgAlias ?? '';

  // ── Form fields ───────────────────────────────────────────────
  name        = '';
  code        = '';
  description = '';

  // ── Code availability ─────────────────────────────────────────
  readonly codeStatus = signal<'idle' | 'checking' | 'available' | 'taken'>('idle');

  // ── Scrum Master picker ───────────────────────────────────────
  readonly searchTerm    = signal('');
  readonly searchResults = signal<UserPickerItem[]>([]);
  readonly selectedUser  = signal<UserPickerItem | null>(null);
  readonly searching     = signal(false);
  readonly dropdownRect  = signal<DropdownRect | null>(null);

  // ── Submission state ──────────────────────────────────────────
  readonly submitting = signal(false);

  private readonly search$ = new Subject<string>();

  constructor() {
    this.search$.pipe(
      debounceTime(400),
      distinctUntilChanged(),
      switchMap(term => {
        if (term.length < 2) {
          this.searchResults.set([]);
          this.searching.set(false);
          return [];
        }
        return this.usersSvc.searchAllUsers(term);
      }),
      takeUntilDestroyed(),
    ).subscribe({
      next:  results => { this.searchResults.set(results); this.searching.set(false); },
      error: ()      => { this.searchResults.set([]);      this.searching.set(false); },
    });
  }

  /** Transforms the code field to  and strips non-alphanumeric chars on each keystroke. */
  onCodeInput(): void {
    this.code = this.code.toUpperCase().replace(/[^A-Z0-9]/g, '');
    this.codeStatus.set('idle');
  }

  /** Validates code availability against the API when the field loses focus. */
  onCodeBlur(): void {
    const trimmed = this.code.trim();
    if (!trimmed) { this.codeStatus.set('idle'); return; }
    this.codeStatus.set('checking');
    this.repoCtx.checkCode(trimmed).subscribe({
      next:  available => this.codeStatus.set(available ? 'available' : 'taken'),
      error: ()        => this.codeStatus.set('idle'),
    });
  }

  /**
   * Feeds a new search term into the debounced stream and snapshots the input's viewport rect for the fixed dropdown.
   * @param term Raw input value.
   * @param el The input element (passed directly from the event so no ViewChild race condition).
   */
  onUserSearch(term: string, el: HTMLInputElement): void {
    this.searchTerm.set(term);
    this.selectedUser.set(null);
    const trimmed = term.trim();
    if (trimmed.length >= 2) {
      this.searching.set(true);
      const r = el.getBoundingClientRect();
      this.dropdownRect.set({ top: r.bottom + 4, left: r.left, width: r.width });
    } else {
      this.searchResults.set([]);
      this.searching.set(false);
      this.dropdownRect.set(null);
    }
    this.search$.next(trimmed);
  }

  /** Selects a user from the autocomplete results. @param user Picked user. */
  selectUser(user: UserPickerItem): void {
    this.selectedUser.set(user);
    this.searchTerm.set('');
    this.searchResults.set([]);
    this.dropdownRect.set(null);
  }

  /** Clears the selected Scrum Master so a new search can be started. */
  clearUser(): void {
    this.selectedUser.set(null);
    this.searchTerm.set('');
    this.searchResults.set([]);
    this.dropdownRect.set(null);
  }

  /** Extracts two-letter initials from a full name. @param name Full display name. @returns Uppercase initials. */
  initials(name: string): string {
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (!parts.length) return '?';
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }

  /** Submits the form to create the repository. */
  submit(): void {
    const user = this.selectedUser();
    if (!this.name.trim() || !this.code.trim() || !user || this.codeStatus() === 'taken') return;
    this.submitting.set(true);
    this.repoCtx.create({
      name:          this.name.trim(),
      code:          this.code.trim(),
      description:   this.description.trim(),
      scrumMasterId: user.userId,
    }).subscribe({
      next:  repo => { this.submitting.set(false); this.dialogRef.close(repo); },
      error: err  => {
        this.submitting.set(false);
        this.toast.error(err?.error?.detail ?? err?.error?.title ?? 'Failed to create repository.');
      },
    });
  }

  /** Closes the dialog without saving. */
  cancel(): void {
    this.dialogRef.close();
  }

}
