export const StorageKeys = {
  // ── Authentication ────────────────────────────────────────────
  // Tokens and session data managed by AuthService / TokenService.
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
  wikiPages:        'ado.wiki.pages',
  reposIntegration: 'ado.github.integration.v1',
  pipelines:        'ado.mock.pipelines',
} as const;
