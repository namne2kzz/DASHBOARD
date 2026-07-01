import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';
import { repoContextGuard, rootRedirectGuard } from './guards/repo-context.guard';
import { membersPrivilegeGuard, globalAdminGuard } from './guards/privilege.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () =>
      import('./pages/login-page/login-page.component').then(m => m.LoginPageComponent),
  },
  {
    path: '',
    loadComponent: () =>
      import('./layout/shell-layout.component').then(m => m.ShellLayoutComponent),
    canActivateChild: [authGuard],
    children: [
      {
        path: '',
        pathMatch: 'full',
        canActivate: [rootRedirectGuard],
        children: [],
      },
      // ── Global routes — auth only, no repo context ─────────────
      {
        path: 'profile',
        loadComponent: () =>
          import('./pages/profile-page/profile-page.component').then(m => m.ProfilePageComponent),
      },
      {
        path: 'settings',
        children: [
          { path: '', pathMatch: 'full', redirectTo: 'general' },
          {
            path: 'general',
            loadComponent: () =>
              import('./pages/settings-general-page/settings-general-page.component').then(m => m.SettingsGeneralPageComponent),
          },
          {
            path: 'members',
            canActivate: [membersPrivilegeGuard],
            loadComponent: () =>
              import('./pages/settings-members-page/settings-members-page.component').then(m => m.SettingsMembersPageComponent),
          },
          {
            path: 'metadata',
            canActivate: [membersPrivilegeGuard],
            loadComponent: () =>
              import('./pages/settings-metadata-page/settings-metadata-page.component').then(m => m.SettingsMetadataPageComponent),
          },
          {
            path: 'users',
            canActivate: [globalAdminGuard],
            loadComponent: () =>
              import('./pages/settings-users-page/settings-users-page.component').then(m => m.SettingsUsersPageComponent),
          },
        ],
      },
      // ── Repo-scoped routes — requires repoContextGuard ─────────
      {
        path: ':repoCode',
        canActivate: [repoContextGuard],
        children: [
          { path: '', pathMatch: 'full', redirectTo: 'boards' },
          {
            path: 'boards',
            loadComponent: () =>
              import('./pages/boards-page/boards-page.component').then(m => m.BoardsPageComponent),
          },
          {
            path: 'overview',
            loadComponent: () =>
              import('./pages/overview-page/overview-page.component').then(m => m.OverviewPageComponent),
          },
          {
            path: 'analytics',
            loadComponent: () =>
              import('./pages/analytics-page/analytics-page.component').then(m => m.AnalyticsPageComponent),
          },
          {
            path: 'workflow',
            loadComponent: () =>
              import('./pages/workflow-page/workflow-page.component').then(m => m.WorkflowPageComponent),
          },
          {
            path: 'backlog',
            loadComponent: () =>
              import('./pages/backlog-management-page/backlog-management-page.component').then(m => m.BacklogManagementPageComponent),
          },
          {
            path: 'sprint-planning',
            loadComponent: () =>
              import('./pages/sprint-planning-page/sprint-planning-page.component').then(m => m.SprintPlanningPageComponent),
          },
          {
            path: 'wiki',
            loadComponent: () =>
              import('./pages/wiki-page/wiki-page.component').then(m => m.WikiPageComponent),
          },
          {
            path: 'repos',
            loadComponent: () =>
              import('./pages/repos-page/repos-page.component').then(m => m.ReposPageComponent),
          },
          {
            path: 'pipelines',
            loadComponent: () =>
              import('./pages/pipelines-page/pipelines-page.component').then(m => m.PipelinesPageComponent),
          },
        ],
      },
    ],
  },
];
