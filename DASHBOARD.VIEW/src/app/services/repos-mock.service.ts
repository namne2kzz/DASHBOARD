import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { TaskBoardService } from './task-board.service';
import { StorageKeys } from '../core/constants/storage-keys.constant';

export type GitHubConnectionStatus = 'disconnected' | 'connected' | 'revoked';
export type GitHubSyncMode = 'initial' | 'delta';
export type PullRequestStatus = 'open' | 'closed' | 'merged';
export type ReviewDecision = 'pending' | 'approved' | 'changes_requested';
export type WebhookEventType = 'push' | 'pull_request' | 'pull_request_review';

export interface GitHubIntegration {
  installationId: number | null;
  accountLogin: string | null;
  accountType: 'Organization' | 'User' | null;
  status: GitHubConnectionStatus;
  connectedAt: string | null;
  disconnectedAt: string | null;
  tokenExpiresAt: string | null;
}

export interface GitHubRepository {
  id: number;
  name: string;
  fullName: string;
  visibility: 'public' | 'private';
  defaultBranch: string;
  selected: boolean;
  syncStatus: 'not_linked' | 'queued' | 'syncing' | 'synced' | 'failed';
  lastSyncedAt: string | null;
}

export interface GitHubCommit {
  id: string;
  sha: string;
  shortSha: string;
  repoId: number;
  branch: string;
  message: string;
  author: string;
  authorEmail: string;
  date: string;
  linkedWorkItemId: string | null;
}

export interface GitHubBranch {
  repoId: number;
  name: string;
  isDefault: boolean;
  lastCommit: string;
  lastCommitAt: string;
  isStale: boolean;
}

export interface GitHubPullRequest {
  id: string;
  number: number;
  repoId: number;
  title: string;
  source: string;
  target: string;
  status: PullRequestStatus;
  reviewDecision: ReviewDecision;
  author: string;
  reviewers: string[];
  linkedWorkItemId: string | null;
  mergedAt: string | null;
}

export interface GitHubWebhookEvent {
  deliveryId: string;
  type: WebhookEventType;
  action: string;
  status: 'queued' | 'processed' | 'ignored';
  receivedAt: string;
}

export interface GitHubRateLimit {
  limit: number;
  remaining: number;
  resetAt: string;
  backoffUntil: string | null;
}

interface ReposState {
  integration: GitHubIntegration;
  repositories: GitHubRepository[];
  commits: GitHubCommit[];
  branches: GitHubBranch[];
  pullRequests: GitHubPullRequest[];
  webhookEvents: GitHubWebhookEvent[];
  rateLimit: GitHubRateLimit;
  lastSyncMode: GitHubSyncMode | null;
}

const DISCONNECTED_INTEGRATION: GitHubIntegration = {
  installationId: null,
  accountLogin: null,
  accountType: null,
  status: 'disconnected',
  connectedAt: null,
  disconnectedAt: null,
  tokenExpiresAt: null,
};

const EMPTY_STATE: ReposState = {
  integration: DISCONNECTED_INTEGRATION,
  repositories: [],
  commits: [],
  branches: [],
  pullRequests: [],
  webhookEvents: [],
  rateLimit: {
    limit: 5000,
    remaining: 5000,
    resetAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    backoffUntil: null,
  },
  lastSyncMode: null,
};

const DEMO_REPOSITORIES: GitHubRepository[] = [
  {
    id: 801,
    name: 'dashboard',
    fullName: 'contoso/dashboard',
    visibility: 'private',
    defaultBranch: 'main',
    selected: true,
    syncStatus: 'synced',
    lastSyncedAt: '2026-05-15T06:40:00Z',
  },
  {
    id: 802,
    name: 'integration-worker',
    fullName: 'contoso/integration-worker',
    visibility: 'private',
    defaultBranch: 'main',
    selected: true,
    syncStatus: 'synced',
    lastSyncedAt: '2026-05-15T06:38:00Z',
  },
  {
    id: 803,
    name: 'design-system',
    fullName: 'contoso/design-system',
    visibility: 'public',
    defaultBranch: 'main',
    selected: false,
    syncStatus: 'not_linked',
    lastSyncedAt: null,
  },
];

const DEMO_BRANCHES: GitHubBranch[] = [
  {
    repoId: 801,
    name: 'main',
    isDefault: true,
    lastCommit: '9f61aa2',
    lastCommitAt: '2026-05-15T06:34:00Z',
    isStale: false,
  },
  {
    repoId: 801,
    name: 'feature/GH-456-repo-sync',
    isDefault: false,
    lastCommit: '6d4be19',
    lastCommitAt: '2026-05-15T05:50:00Z',
    isStale: false,
  },
  {
    repoId: 801,
    name: 'release/1.0',
    isDefault: false,
    lastCommit: 'c31e9af',
    lastCommitAt: '2026-03-20T09:15:00Z',
    isStale: true,
  },
  {
    repoId: 802,
    name: 'main',
    isDefault: true,
    lastCommit: 'a18cc43',
    lastCommitAt: '2026-05-15T06:20:00Z',
    isStale: false,
  },
];

const DEMO_COMMITS: GitHubCommit[] = [
  {
    id: '9f61aa2e',
    sha: '9f61aa2e3b1f4d8c91a0a55d3d412ce8ff115201',
    shortSha: '9f61aa2',
    repoId: 801,
    branch: 'main',
    message: 'fixed #2: persist board drag order after reload',
    author: 'Mai Tran',
    authorEmail: 'mai@contoso.test',
    date: '2026-05-15T06:34:00Z',
    linkedWorkItemId: '2',
  },
  {
    id: '6d4be19a',
    sha: '6d4be19a01ab42ae9bc74f1bcf837effdc448ca9',
    shortSha: '6d4be19',
    repoId: 801,
    branch: 'feature/GH-456-repo-sync',
    message: 'feat: resolve bug #6 with GitHub webhook queue',
    author: 'Alex Nguyen',
    authorEmail: 'alex@contoso.test',
    date: '2026-05-15T05:50:00Z',
    linkedWorkItemId: '6',
  },
  {
    id: 'a18cc43b',
    sha: 'a18cc43b78fa4a4d9334129a7d8a0ab9550a1092',
    shortSha: 'a18cc43',
    repoId: 802,
    branch: 'main',
    message: 'chore: add idempotency check for delivery ids',
    author: 'Dev Bot',
    authorEmail: 'bot@contoso.test',
    date: '2026-05-15T06:20:00Z',
    linkedWorkItemId: null,
  },
];

const DEMO_PULL_REQUESTS: GitHubPullRequest[] = [
  {
    id: 'pr-241',
    number: 241,
    repoId: 801,
    title: 'GH-456 Import repositories and branches',
    source: 'feature/GH-456-repo-sync',
    target: 'main',
    status: 'open',
    reviewDecision: 'approved',
    author: 'Alex Nguyen',
    reviewers: ['Mai Tran', 'Dev Bot'],
    linkedWorkItemId: '6',
    mergedAt: null,
  },
  {
    id: 'pr-238',
    number: 238,
    repoId: 801,
    title: 'Fixed #2 board persistence regression',
    source: 'bugfix/drag-order',
    target: 'main',
    status: 'merged',
    reviewDecision: 'approved',
    author: 'Mai Tran',
    reviewers: ['Alex Nguyen'],
    linkedWorkItemId: '2',
    mergedAt: '2026-05-15T06:35:00Z',
  },
];

function freshConnectedState(): ReposState {
  const now = new Date();
  return {
    integration: {
      installationId: 11734590,
      accountLogin: 'contoso',
      accountType: 'Organization',
      status: 'connected',
      connectedAt: now.toISOString(),
      disconnectedAt: null,
      tokenExpiresAt: new Date(now.getTime() + 55 * 60 * 1000).toISOString(),
    },
    repositories: DEMO_REPOSITORIES,
    branches: DEMO_BRANCHES,
    commits: DEMO_COMMITS,
    pullRequests: DEMO_PULL_REQUESTS,
    webhookEvents: [
      {
        deliveryId: 'gh-delivery-238-merged',
        type: 'pull_request',
        action: 'closed.merged',
        status: 'processed',
        receivedAt: '2026-05-15T06:35:05Z',
      },
      {
        deliveryId: 'gh-delivery-801-push',
        type: 'push',
        action: 'refs/heads/main',
        status: 'processed',
        receivedAt: '2026-05-15T06:34:20Z',
      },
    ],
    rateLimit: {
      limit: 5000,
      remaining: 4862,
      resetAt: new Date(now.getTime() + 48 * 60 * 1000).toISOString(),
      backoffUntil: null,
    },
    lastSyncMode: 'initial',
  };
}

@Injectable({ providedIn: 'root' })
export class ReposMockService {
  private readonly board = inject(TaskBoardService);
  private readonly state = signal<ReposState>(EMPTY_STATE);

  readonly integration = computed(() => this.state().integration);
  readonly repositories = computed(() => this.visibleWhenConnected(this.state().repositories));
  readonly linkedRepositories = computed(() =>
    this.repositories().filter((repo) => repo.selected && repo.syncStatus === 'synced'),
  );
  readonly commits = computed(() => this.visibleWhenConnected(this.state().commits));
  readonly branches = computed(() => this.visibleWhenConnected(this.state().branches));
  readonly pullRequests = computed(() => this.visibleWhenConnected(this.state().pullRequests));
  readonly webhookEvents = computed(() => this.visibleWhenConnected(this.state().webhookEvents));
  readonly rateLimit = computed(() => this.state().rateLimit);
  readonly lastSyncMode = computed(() => this.state().lastSyncMode);
  readonly isConnected = computed(() => this.integration().status === 'connected');
  readonly queueDepth = computed(
    () => this.webhookEvents().filter((event) => event.status === 'queued').length,
  );
  readonly syncSummary = computed(() => ({
    repos: this.linkedRepositories().length,
    branches: this.branches().length,
    commits: this.commits().length,
    pullRequests: this.pullRequests().length,
  }));

  constructor() {
    this.load();
    effect(() => {
      const state = this.state();
      localStorage.setItem(StorageKeys.reposIntegration, JSON.stringify(state));
    });
  }

  connectGitHubApp(): void {
    this.state.set(freshConnectedState());
    this.applyMergedPullRequestAutomation('pr-238');
  }

  disconnectFromGitHub(): void {
    this.state.set({
      ...EMPTY_STATE,
      integration: {
        ...DISCONNECTED_INTEGRATION,
        status: 'revoked',
        disconnectedAt: new Date().toISOString(),
      },
    });
  }

  runDeltaSync(): void {
    if (!this.isConnected()) {
      return;
    }
    this.consumeRateLimit(18);
    this.state.update((state) => ({
      ...state,
      repositories: state.repositories.map((repo) =>
        repo.selected ? { ...repo, lastSyncedAt: new Date().toISOString() } : repo,
      ),
      lastSyncMode: 'delta',
    }));
  }

  processWebhookQueue(): void {
    if (!this.isConnected()) {
      return;
    }
    const deliveryId = 'gh-delivery-241-review-approved';
    if (this.state().webhookEvents.some((event) => event.deliveryId === deliveryId)) {
      return;
    }

    this.consumeRateLimit(6);
    this.state.update((state) => ({
      ...state,
      pullRequests: state.pullRequests.map((pr) =>
        pr.id === 'pr-241' ? { ...pr, reviewDecision: 'approved' } : pr,
      ),
      webhookEvents: [
        {
          deliveryId,
          type: 'pull_request_review',
          action: 'submitted.approved',
          status: 'processed',
          receivedAt: new Date().toISOString(),
        },
        ...state.webhookEvents,
      ],
      lastSyncMode: 'delta',
    }));
  }

  simulateMergeAutomation(): void {
    if (!this.isConnected()) {
      return;
    }
    const mergedAt = new Date().toISOString();
    this.consumeRateLimit(8);
    this.state.update((state) => ({
      ...state,
      pullRequests: state.pullRequests.map((pr) =>
        pr.id === 'pr-241' ? { ...pr, status: 'merged', mergedAt } : pr,
      ),
      webhookEvents: [
        {
          deliveryId: 'gh-delivery-241-merged',
          type: 'pull_request',
          action: 'closed.merged',
          status: 'processed',
          receivedAt: mergedAt,
        },
        ...state.webhookEvents.filter((event) => event.deliveryId !== 'gh-delivery-241-merged'),
      ],
      lastSyncMode: 'delta',
    }));
    this.applyMergedPullRequestAutomation('pr-241');
  }

  private visibleWhenConnected<T>(items: T[]): T[] {
    return this.isConnected() ? items : [];
  }

  private applyMergedPullRequestAutomation(prId: string): void {
    const pr = this.state().pullRequests.find((item) => item.id === prId);
    if (!pr?.linkedWorkItemId || pr.status !== 'merged') {
      return;
    }
    this.board.transitionWorkItemFromGitHub(
      pr.linkedWorkItemId,
      `GitHub PR #${pr.number} was merged`,
    );
  }

  private consumeRateLimit(count: number): void {
    this.state.update((state) => {
      const remaining = Math.max(0, state.rateLimit.remaining - count);
      return {
        ...state,
        rateLimit: {
          ...state.rateLimit,
          remaining,
          backoffUntil:
            remaining < 100 ? new Date(Date.now() + 10 * 60 * 1000).toISOString() : null,
        },
      };
    });
  }

  private load(): void {
    const raw = localStorage.getItem(StorageKeys.reposIntegration);
    if (!raw) {
      this.state.set(EMPTY_STATE);
      return;
    }
    try {
      const parsed = JSON.parse(raw) as ReposState;
      this.state.set({
        ...EMPTY_STATE,
        ...parsed,
        integration: { ...DISCONNECTED_INTEGRATION, ...parsed.integration },
        rateLimit: { ...EMPTY_STATE.rateLimit, ...parsed.rateLimit },
      });
    } catch {
      this.state.set(EMPTY_STATE);
    }
  }
}
