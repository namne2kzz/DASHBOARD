import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Redirects the bare root `/` to the authenticated user's organization home `/{orgAlias}`.
 * Sends unauthenticated users to `/login`.
 */
export const orgHomeRedirectGuard: CanActivateFn = () => {
  const auth   = inject(AuthService);
  const router = inject(Router);

  const alias = auth.currentUser()?.orgAlias;
  return router.parseUrl(alias ? `/${alias}` : '/login');
};

/**
 * Validates the `:orgAlias` URL segment against the authenticated user's organization.
 * A user only ever belongs to one org, so a mismatched alias is redirected to their own.
 * Leaves the authentication redirect to the shell's child auth guard.
 */
export const orgContextGuard: CanActivateFn = route => {
  const auth   = inject(AuthService);
  const router = inject(Router);

  const user = auth.currentUser();
  if (!user?.orgAlias) return true; // not authenticated (or legacy session) — authGuard handles it

  const requested = (route.paramMap.get('orgAlias') ?? '').toLowerCase();
  const mine      = user.orgAlias.toLowerCase();
  return requested === mine ? true : router.parseUrl(`/${user.orgAlias}`);
};
