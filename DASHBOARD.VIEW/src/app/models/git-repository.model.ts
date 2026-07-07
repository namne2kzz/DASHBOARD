/** Non-secret summary of one configured GitHub connection for a project. Never carries a token. */
export interface GitRepositoryConnectionSummary {
  repoUrl: string;
  ownerLogin: string;
  repoName: string;
  isPrimary: boolean;
}

/** One branch on the connected GitHub repository. */
export interface GitBranch {
  name: string;
  isDefault: boolean;
  lastCommitSha: string;
  lastCommitAt: string;
  isStale: boolean;
}

/** One recent commit on the connected GitHub repository. */
export interface GitCommit {
  sha: string;
  shortSha: string;
  branch: string;
  message: string;
  authorName: string;
  authorEmail: string;
  date: string;
  linkedWorkItemId: string | null;
}

/** Review/merge status of a pull request. */
export type GitPullRequestStatus = 'open' | 'closed' | 'merged';

/** One pull request on the connected GitHub repository. */
export interface GitPullRequest {
  number: number;
  title: string;
  sourceBranch: string;
  targetBranch: string;
  status: GitPullRequestStatus;
  reviewDecision: string;
  authorLogin: string;
  reviewers: string[];
  linkedWorkItemId: string | null;
  mergedAt: string | null;
}

/** Remaining GitHub API quota for the token backing the current connection. */
export interface GitRateLimit {
  limit: number;
  remaining: number;
  resetAt: string;
}

/** Full snapshot returned by the overview endpoint for one project's active (or selected) GitHub connection. */
export interface GitRepositoryOverview {
  hasConnection: boolean;
  repoUrl: string | null;
  fullName: string | null;
  defaultBranch: string | null;
  branches: GitBranch[];
  commits: GitCommit[];
  pullRequests: GitPullRequest[];
  rateLimit: GitRateLimit | null;
  status: string | null;
  lastSyncError: string | null;
}
