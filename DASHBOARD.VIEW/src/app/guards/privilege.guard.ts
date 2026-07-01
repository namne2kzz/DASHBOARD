import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { PrivilegeService } from '../core/services/privilege.service';
import { RepositoryContextService } from '../services/repository-context.service';

/** Blocks navigation to routes that require member-management privileges. */
export const membersPrivilegeGuard: CanActivateFn = () => {
  const privilege = inject(PrivilegeService);
  const repoCtx   = inject(RepositoryContextService);
  const router    = inject(Router);

  if (privilege.canManageMembers()) return true;

  const code = repoCtx.selectedRepo()?.code;
  return router.createUrlTree(code ? ['/', code, 'boards'] : ['/']);
};

/** Blocks navigation to routes that require global-admin access. */
export const globalAdminGuard: CanActivateFn = () => {
  const privilege = inject(PrivilegeService);
  const repoCtx   = inject(RepositoryContextService);
  const router    = inject(Router);

  if (privilege.isGlobalAdmin()) return true;

  const code = repoCtx.selectedRepo()?.code;
  return router.createUrlTree(code ? ['/', code, 'boards'] : ['/']);
};
