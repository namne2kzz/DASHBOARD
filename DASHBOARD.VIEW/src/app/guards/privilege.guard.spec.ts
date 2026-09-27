import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, UrlTree, provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { globalAdminGuard, membersPrivilegeGuard } from './privilege.guard';
import { PrivilegeService } from '../core/services/privilege.service';
import { RepositoryContextService } from '../services/repository-context.service';
import { RepositoryApiDto } from '../models/repository.model';

/**
 * Unit tests for the two guards that hide privileged routes.
 *
 * These are a convenience, not a security boundary — the API refuses the request regardless, which
 * the integration tests cover. What they protect against is a worse user experience: opening a
 * settings page that then fails every call with 403, or landing on a blank screen because the
 * fallback redirect had nowhere to send the user.
 *
 * That fallback is the interesting part. Both guards redirect to the selected repository's board,
 * and both have to cope with there being no selected repository yet — which is the normal state
 * during a cold start, when the repository list has not finished loading.
 */
describe('privilege guards', () => {
  let canManageMembers: ReturnType<typeof signal<boolean>>;
  let isGlobalAdmin: ReturnType<typeof signal<boolean>>;
  let selectedRepo: ReturnType<typeof signal<RepositoryApiDto | null>>;
  let router: Router;

  const repository = { id: 'repo-1', code: 'DASH' } as RepositoryApiDto;

  function configure(options: {
    canManageMembers?: boolean;
    isGlobalAdmin?: boolean;
    repo?: RepositoryApiDto | null;
  }): void {
    canManageMembers = signal(options.canManageMembers ?? false);
    isGlobalAdmin    = signal(options.isGlobalAdmin ?? false);
    selectedRepo     = signal(options.repo ?? null);

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: PrivilegeService,          useValue: { canManageMembers, isGlobalAdmin } },
        { provide: RepositoryContextService,  useValue: { selectedRepo } },
      ],
    });

    router = TestBed.inject(Router);
  }

  function activate(
    guard: typeof membersPrivilegeGuard,
  ): boolean | UrlTree {
    return TestBed.runInInjectionContext(
      () => guard({} as ActivatedRouteSnapshot, {} as never),
    ) as boolean | UrlTree;
  }

  function path(result: boolean | UrlTree): string {
    return router.serializeUrl(result as UrlTree);
  }

  // ── membersPrivilegeGuard ───────────────────────────────────────────────

  describe('membersPrivilegeGuard', () => {
    it('allows a user who can manage members', () => {
      configure({ canManageMembers: true, repo: repository });

      expect(activate(membersPrivilegeGuard)).toBeTrue();
    });

    it('sends a user without the privilege to the board', () => {
      configure({ canManageMembers: false, repo: repository });

      expect(path(activate(membersPrivilegeGuard))).toBe('/DASH/boards');
    });

    it('falls back to the root when no repository is selected yet', () => {
      // During a cold start the repository list is still loading, so there is no code to build a
      // board URL from. Redirecting to `/DASH/boards` with an undefined code would produce
      // `/undefined/boards`, a route that does not exist.
      configure({ canManageMembers: false, repo: null });

      expect(path(activate(membersPrivilegeGuard))).toBe('/');
    });

    it('does not consult the repository at all when the privilege is held', () => {
      // The happy path must not depend on the repository list having loaded, or a privileged user
      // would be bounced off their own settings page purely because of timing.
      configure({ canManageMembers: true, repo: null });

      expect(activate(membersPrivilegeGuard)).toBeTrue();
    });

    it('is re-evaluated when the privilege changes', () => {
      configure({ canManageMembers: false, repo: repository });
      expect(activate(membersPrivilegeGuard)).toBeInstanceOf(UrlTree);

      // The member list finishes loading and the user turns out to hold the role after all.
      canManageMembers.set(true);

      expect(activate(membersPrivilegeGuard)).toBeTrue();
    });
  });

  // ── globalAdminGuard ────────────────────────────────────────────────────

  describe('globalAdminGuard', () => {
    it('allows a global admin', () => {
      configure({ isGlobalAdmin: true, repo: repository });

      expect(activate(globalAdminGuard)).toBeTrue();
    });

    it('sends everyone else to the board', () => {
      configure({ isGlobalAdmin: false, repo: repository });

      expect(path(activate(globalAdminGuard))).toBe('/DASH/boards');
    });

    it('falls back to the root when no repository is selected yet', () => {
      configure({ isGlobalAdmin: false, repo: null });

      expect(path(activate(globalAdminGuard))).toBe('/');
    });

    it('does not accept ManageMembers as a substitute for global admin', () => {
      // These are separate rights: managing the members of one repository says nothing about
      // administering the whole system.
      configure({ isGlobalAdmin: false, canManageMembers: true, repo: repository });

      expect(activate(globalAdminGuard)).toBeInstanceOf(UrlTree);
    });
  });

  // ── The two guards are independent ──────────────────────────────────────

  describe('the relationship between the two', () => {
    it('lets a global admin through the members guard as well', () => {
      // PrivilegeService.can() already folds the global-admin flag into every permission, so a
      // global admin reports canManageMembers as true. This pins that arrangement: an admin must
      // never be locked out of a repository settings page.
      configure({ isGlobalAdmin: true, canManageMembers: true, repo: repository });

      expect(activate(membersPrivilegeGuard)).toBeTrue();
      expect(activate(globalAdminGuard)).toBeTrue();
    });
  });
});
