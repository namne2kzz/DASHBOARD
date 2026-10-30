import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Redirects unauthenticated users to sign-in, preserving the intended URL.
 *
 * When the URL already names a tenant, it sends them to that tenant's sign-in page so an expired
 * session resumes at the password step instead of making them name their organization again.
 */
export const authGuard: CanActivateFn = (route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated()) return true;

  // Walk up: the alias is a parameter of the shell route, not of the child this guard runs on.
  const alias = route.pathFromRoot
    .map(r => r.paramMap.get('orgAlias'))
    .find((a): a is string => !!a);

  return router.createUrlTree(alias ? [alias, 'login'] : ['/login'],
    { queryParams: { returnUrl: state.url } });
};
