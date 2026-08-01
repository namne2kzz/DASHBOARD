import { Component, computed, effect, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { RepositoryContextService } from '../services/repository-context.service';
import { PrivilegeService } from '../core/services/privilege.service';
import { DialogService } from '../core/components/dialog/dialog.service';
import { LoadingOverlayComponent } from '../core/components/loading/loading-overlay.component';
import { DialogComponent } from '../core/components/dialog/dialog.component';
import { NewRepoDialogComponent } from '../components/new-repo-dialog/new-repo-dialog.component';
import { RepositoryApiDto } from '../models/repository.model';

@Component({
  selector: 'app-shell-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, LoadingOverlayComponent, DialogComponent],
  templateUrl: './shell-layout.component.html',
  styleUrl: './shell-layout.component.css',
})
export class ShellLayoutComponent {
  readonly collapsed        = signal(false);
  readonly repoDropdownOpen = signal(false);
  readonly settingsOpen     = signal(false);
  readonly auth      = inject(AuthService);
  readonly repoCtx   = inject(RepositoryContextService);
  readonly privilege = inject(PrivilegeService);
  private readonly dialog = inject(DialogService);
  private readonly router = inject(Router);

  private readonly currentUrl = toSignal(
    this.router.events.pipe(
      filter(e => e instanceof NavigationEnd),
      map(e => (e as NavigationEnd).urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  /** True when the current route is a global page (profile/settings) that doesn't require repo access. */
  readonly isGlobalRoute = computed(() => {
    const url = this.currentUrl() ?? '';
    return url.startsWith('/profile') || url.startsWith('/settings') || url.startsWith('/my-work');
  });

  constructor() {
    effect(() => {
      if ((this.currentUrl() ?? '').includes('/settings')) {
        this.settingsOpen.set(true);
      }
    });
  }

  /** Toggle the Settings sub-nav group open/closed. */
  toggleSettings(): void {
    this.settingsOpen.update(v => !v);
  }

  toggleSidebar(): void {
    this.collapsed.update(v => !v);
  }

  /** @returns Router link segments for a repo-scoped page. @param page Sub-route path e.g. 'boards' or 'settings/general'. */
  repoLink(page: string): string[] {
    const code = this.repoCtx.selectedRepo()?.code;
    return code ? ['/', code, ...page.split('/')] : ['/'];
  }

  /** Selects a repository, preserves the current sub-route, and navigates. @param id Repository ID. */
  selectRepo(id: string): void {
    const repo = this.repoCtx.repositories().find(r => r.id === id);
    if (!repo) return;
    this.repoCtx.select(id);
    this.repoDropdownOpen.set(false);
    const segments = this.router.url.split('/').filter(Boolean);
    const subSegments = segments.length >= 2 ? segments.slice(1) : ['boards'];
    void this.router.navigate(['/', repo.code, ...subSegments]);
  }

  /**
   * Opens the New Repository dialog. Only reachable when the user is a global admin.
   * On success, selects and navigates to the newly created repository.
   */
  openNewRepo(): void {
    this.repoDropdownOpen.set(false);
    const ref = this.dialog.open<NewRepoDialogComponent, unknown, RepositoryApiDto>(
      NewRepoDialogComponent,
      { title: 'New Repository', width: '30rem' },
    );
    void ref.closed.then(repo => {
      if (repo) {
        this.selectRepo(repo.id);
      }
    });
  }

  logout(): void {
    this.auth.logout();
    void this.router.navigate(['/login']);
  }
}
