import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map, take } from 'rxjs';
import { RepositoryContextService } from '../services/repository-context.service';

/**
 * Reads `:repoCode` from the URL and selects the matching repository.
 * Redirects to the first accessible repo when the code is not found.
 * Redirects to `/` (no-access overlay) when the user has no repos at all.
 */
export const repoContextGuard: CanActivateFn = route => {
  const repoCtx = inject(RepositoryContextService);
  const router  = inject(Router);
  const code    = (route.paramMap.get('repoCode') ?? '').toLowerCase();

  const resolve = () => {
    const repos = repoCtx.repositories();
    if (!repos.length) return router.parseUrl('/');

    const repo = repos.find(r => r.code.toLowerCase() === code);
    if (repo) { repoCtx.select(repo.id); return true as const; }

    return router.parseUrl(`/${repos[0].code}/boards`);
  };

  if (repoCtx.repositories().length > 0) return resolve();
  return repoCtx.ready$.pipe(take(1), map(() => resolve()));
};

/**
 * Redirects `/` to `/{selectedRepoCode}/boards` once the repository list is ready.
 * Stays on `/` (no-access overlay) when the user has no accessible repositories.
 */
export const rootRedirectGuard: CanActivateFn = () => {
  const repoCtx = inject(RepositoryContextService);
  const router  = inject(Router);

  const redirect = () => {
    const code = repoCtx.selectedRepo()?.code ?? repoCtx.repositories()[0]?.code;
    return code ? router.parseUrl(`/${code}/boards`) : true;
  };

  if (repoCtx.repositories().length > 0) return redirect();
  return repoCtx.ready$.pipe(take(1), map(() => redirect()));
};
