export const StorageKeys = {
  // ── Authentication ────────────────────────────────────────────
  // Session data managed by AuthService / TokenService. Only the non-secret user profile is
  // persisted; the access/refresh tokens now live in HttpOnly cookies and are never written to
  // storage. The token keys are retained only so tests can assert they stay empty.
  accessToken:  'auth.access-token',
  refreshToken: 'auth.refresh-token',
  userProfile:  'auth.user',

  // ── Repository context ────────────────────────────────────────
  // Persists the last selected repository across sessions.
  selectedRepoId: 'board.repoId',

  // ── Board / feature state ─────────────────────────────────────
  // Persisted UI state for each feature page.
  sprintPlanning:    'task-dashboard.sprint-planning.v1',
  backlogManagement: 'task-dashboard.backlog-management.v1',
  workflow:          'task-dashboard.workflow.v1',

  // ── UI preferences ───────────────────────────────────────────
  // User-selected UI settings persisted across sessions.
  theme:      'nxs.ui.theme',
  dateFormat: 'nxs.ui.date-format',
  timezone:   'nxs.ui.timezone',

  // ── Mock / dev data ───────────────────────────────────────────
  // Keys used by mock services to seed and persist fake data.
  reposIntegration: 'ado.github.integration.v1',
  pipelines:        'ado.mock.pipelines',
} as const;
