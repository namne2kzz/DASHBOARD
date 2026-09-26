# Implementation Plan: GitHub Repo Integration (Config-Based, Per-Project)

Status: **CONFIRMED — config-based architecture (v2), all open decisions resolved in §7. Ready for implementation, starting with Phase 1.**

Scope: Wire the mocked `:repoCode/repos` tab to real GitHub data, where **each Project (`Repository` entity) can link to one or more external GitHub repos**. Connection config (URL + PAT) lives in **server-side app configuration**, keyed by `Repository.Code`, edited directly by whoever manages the deployment/config — **not** through a web UI or API, so the secret token is never transmitted over HTTP at all.

> **v2 change log**: superseded the earlier v1 design (DB-backed `GitConnection` entity + admin CRUD screen + Data Protection encryption). The user decided mid-review that entering credentials through a web form/API is an unnecessary transmission risk for a secret that only needs to exist on the server. v2 removes all of that: no entity, no migration, no CRUD commands, no Settings sub-nav, no controller for connection management. Everything below is new; nothing from the v1 plan should be built.

---

## 0. Terminology disambiguation (read this first)

| Term in this plan | What it actually means | Existing code |
|---|---|---|
| **Project** | Scrum project / work-item container | `Domain/Entities/Repository.cs` (`Code` field, e.g. `"DASH"`), `Application/Repositories/*` |
| **Git connection (config entry)** | One `{RepoUrl, Token, ...}` record in server config, associated with a project by `Repository.Code` | **NEW** — lives in `appsettings*.json` / environment / Key Vault, never in the database |
| **Repos tab** | Per-project UI tab showing GitHub activity (commits/branches/PRs) | `DASHBOARD.VIEW/src/app/pages/repos-page/*`, currently backed by `services/repos-mock.service.ts` |

All new backend code uses the name **`GitConnection`** for a config entry (never "Repository"); all new Angular types prefix with `Git` (`GitBranch`, `GitCommit`, …), reusing shapes already validated in `repos-mock.service.ts`.

**There is no admin UI and no database table for connections in this design.** An admin (or whoever deploys the app) edits config directly — locally via `appsettings.Development.json` (already gitignored per this repo's history — see the commit "Remove appsettings.Development.json from tracking (contains dev credentials)", so this is consistent with existing practice, not a new risk), and in Azure via environment variables / Azure App Configuration / Key Vault-backed configuration (§2).

---

## 1. Configuration Model

### 1.1 Shape

Keyed by `Repository.Code`. Each code maps to a **list** of connections (so one project can link multiple git repos, e.g. a frontend repo and a backend repo):

```json
// appsettings.Development.json (gitignored) or appsettings.json base + environment overrides in Azure
{
  "GitConnections": {
    "DASH": [
      {
        "RepoUrl": "https://github.com/namne2kzz/DASHBOARD.git",
        "Token": "ghp_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
        "IsPrimary": true
      },
      {
        "RepoUrl": "https://github.com/namne2kzz/DASHBOARD.VIEW.git",
        "Token": "ghp_yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy"
      }
    ],
    "OTHERPROJ": [
      { "RepoUrl": "https://github.com/contoso/other-repo.git", "Token": "ghp_zzzz...", "IsPrimary": true }
    ]
  }
}
```

`RepoUrl` format confirmed: full HTTPS clone URL, `.git` suffix accepted, e.g. `https://github.com/namne2kzz/DASHBOARD.git`.

### 1.2 Strongly-typed options — `Infrastructure/Settings/GitConnectionsOptions.cs`

```csharp
namespace DASHBOARD.Infrastructure.Settings;

/// <summary>Per-project GitHub connection list, keyed by <c>Repository.Code</c>. Bound from the "GitConnections" config section — never stored in the database, never accepted from the frontend.</summary>
public sealed class GitConnectionsOptions : Dictionary<string, List<GitConnectionEntry>>
{
    public const string SectionName = "GitConnections";
}

/// <summary>One configured link between a project and an external GitHub repository.</summary>
public sealed class GitConnectionEntry
{
    public string RepoUrl { get; set; } = default!;
    public string Token { get; set; } = default!;
    public string? DefaultBranchOverride { get; set; }
    public bool IsPrimary { get; set; }

    /// <summary>Optional — set only if a GitHub webhook is registered for this repo (Phase 3). Used to verify the <c>X-Hub-Signature-256</c> header on incoming webhook deliveries.</summary>
    public string? WebhookSecret { get; set; }
}
```

Registration in `Infrastructure/DependencyInjection.cs`:
```csharp
services.Configure<GitConnectionsOptions>(configuration.GetSection(GitConnectionsOptions.SectionName));
```

Consumed via `IOptionsMonitor<GitConnectionsOptions>` (not `IOptions<T>`) wherever it's read, so config edits (e.g. a token rotation dropped into `appsettings.json` or an App Service environment variable) take effect **without an app restart** — `IOptionsMonitor` picks up `reloadOnChange` automatically for file-based providers.

### 1.3 Startup validation (fail fast, don't silently misconfigure)

Add an `IValidateOptions<GitConnectionsOptions>` (registered as `services.AddSingleton<IValidateOptions<GitConnectionsOptions>, GitConnectionsOptionsValidator>()`) that runs at startup and checks, per code:
- `RepoUrl` matches `^https://github\.com/[\w.-]+/[\w.-]+(\.git)?/?$`
- `Token` non-empty
- At most one entry has `IsPrimary = true` per code (if none is marked primary, the first entry in the list is treated as primary — document this fallback in the handler, no need to fail startup over it)
- No duplicate `RepoUrl` within the same code's list

Fails fast on app startup with a clear error rather than surfacing a confusing 500 later when a project's Repos tab is opened.

### 1.4 What is explicitly **not** built

- No `GitConnection` database entity, no EF Core migration, no table.
- No Create/Update/Delete/SetPrimary/Test commands — there is nothing to mutate at runtime; config changes are a deployment/ops action, not an application feature.
- No admin Settings UI page, no new route, no new guard, no new `Permission` enum value. (The earlier v1 plan's `settings/repo-setting` page, `GitConnectionsController`, and `git-connections.service.ts` are all dropped.)
- No encryption-at-rest code in the app (no Data Protection API usage here) — secrecy is handled entirely by standard config-provider practice: gitignored dev file locally, environment variables/Key Vault-backed configuration in Azure (§2 covers the production story). The token never enters the database and never crosses the `DASHBOARD.VIEW` → API boundary in either direction.

---

## 2. Credential Security (revised)

- **Local/dev**: `appsettings.Development.json` — already gitignored in this repo (precedent already set). Never commit real tokens; use a `appsettings.Development.json.example` with placeholder values if a template is wanted (optional, not required by this plan).
- **Azure/production**: prefer layering configuration so the token itself never lives in a checked-in file:
  - Simplest: App Service **Application Settings** (environment variables), which ASP.NET Core's configuration system reads automatically and which override `appsettings.json` — Azure encrypts these at rest and access is controlled by Azure RBAC on the App Service resource, not by this codebase.
  - Stronger (recommended before wider production rollout, flagged as a Phase 4/hardening item, not blocking Phase 1): add the **Azure Key Vault configuration provider** (`Microsoft.Extensions.Configuration.AzureKeyVault`) so `GitConnections:DASH:0:Token` etc. can be sourced from Key Vault secrets instead of plain environment variables, with no code changes to the options classes above (the config provider is transparent to `IOptionsMonitor<GitConnectionsOptions>`).
- The token **never appears in any DTO returned to the frontend**. Any read query that surfaces connection info to the UI (§3) must only return non-secret fields (`RepoUrl` minus credentials, `OwnerLogin`, `RepoName`, `IsPrimary`) — never `Token`.
- Logging: never log `GitConnectionEntry.Token` or full `RepoUrl` query strings if a token were ever accidentally embedded in a URL (it won't be, per the format above, but worth a code-review note for whoever implements this).

---

## 3. Backend Read Surface

Single feature folder: **`Application/GitRepositories/`** (no `Application/GitConnections/` folder — there's nothing to command, only to query).

### 3.1 Queries

| Query | File | Input | Output | Privilege |
|---|---|---|---|---|
| `ListGitRepositoryConnectionsQuery` | `Application/GitRepositories/Queries/ListGitRepositoryConnections/ListGitRepositoryConnectionsQuery.cs` | `RepositoryId` | `IReadOnlyList<GitRepositoryConnectionSummaryDto>` | Project member (`IsMemberOfAsync`) |
| `GetGitRepositoryOverviewQuery` | `Application/GitRepositories/Queries/GetGitRepositoryOverview/GetGitRepositoryOverviewQuery.cs` | `RepositoryId, RepoUrl?` (null = use the entry with `IsPrimary` true, or the first entry if none marked primary) | `GitRepositoryOverviewDto` | Project member (`IsMemberOfAsync`) |

Both handlers resolve `Repository.Code` from `RepositoryId` (existing `Application/Repositories` lookup pattern), then look up `GitConnectionsOptions` via `IOptionsMonitor<GitConnectionsOptions>.CurrentValue.TryGetValue(code, out var entries)`. If the code has no entries → `ListGitRepositoryConnectionsQuery` returns an empty list; `GetGitRepositoryOverviewQuery` returns `GitRepositoryOverviewDto` with `HasConnection = false` (frontend renders the existing "not connected" empty state — unchanged from today's mock behavior, confirmed).

No privilege beyond plain project membership is needed for either query — neither ever returns a token, so there's nothing sensitive to gate beyond "can this user see this project at all," same bar as boards/backlog.

### 3.2 DTOs — `Application/GitRepositories/DTOs/`

Field names intentionally mirror `repos-mock.service.ts` types so the Angular swap is close to a rename-only change:

```csharp
GitRepositoryConnectionSummaryDto(string RepoUrl, string OwnerLogin, string RepoName, bool IsPrimary)

GitRepositoryOverviewDto(
    bool HasConnection, string? RepoUrl, string? FullName, string? DefaultBranch,
    IReadOnlyList<GitBranchDto> Branches, IReadOnlyList<GitCommitDto> Commits,
    IReadOnlyList<GitPullRequestDto> PullRequests, GitRateLimitDto? RateLimit,
    string? Status, string? LastSyncError)

GitBranchDto(string Name, bool IsDefault, string LastCommitSha, DateTime LastCommitAt, bool IsStale)

GitCommitDto(string Sha, string ShortSha, string Branch, string Message,
    string AuthorName, string AuthorEmail, DateTime Date, string? LinkedWorkItemId)

GitPullRequestDto(int Number, string Title, string SourceBranch, string TargetBranch,
    string Status, string ReviewDecision, string AuthorLogin, IReadOnlyList<string> Reviewers,
    string? LinkedWorkItemId, DateTime? MergedAt)

GitRateLimitDto(int Limit, int Remaining, DateTime ResetAt)
```

`LinkedWorkItemId` resolution: regex-match commit messages / PR titles against the project's `Code` prefix (e.g. `DASH-12`), falling back to a bare `#12` pattern — implemented as a small helper in the query handler, kept out of the GitHub-facing service.

### 3.3 Controller

`Controllers/GitRepositories/GitRepositoriesController.cs` — route `api/repositories/{repoId:guid}/git-repositories`, `[Authorize]`:

| Verb | Route | Handler | Response |
|---|---|---|---|
| GET | `` | `ListGitRepositoryConnectionsQuery` | 200, `GitRepositoryConnectionSummaryDto[]` |
| GET | `overview?repoUrl={url?}` | `GetGitRepositoryOverviewQuery` | 200, `GitRepositoryOverviewDto` (always 200, even when `HasConnection=false`) |

---

## 4. GitHub Integration Service

Unchanged in substance from the v1 plan — this part doesn't depend on where the token comes from, only that it arrives as a plaintext string for the duration of one request.

### 4.1 NuGet dependency

Add to `DASHBOARD/DASHBOARD.csproj`: `<PackageReference Include="Octokit" Version="[latest stable]" />` (no `Directory.Packages.props` in this repo — pin directly).

### 4.2 Interface — `Application/Common/Interfaces/IGitHubIntegrationService.cs`

```csharp
Task<GitHubValidationResultDto> ValidateAsync(string ownerLogin, string repoName, string token, CancellationToken ct);
Task<IReadOnlyList<GitBranchDto>> ListBranchesAsync(string ownerLogin, string repoName, string token, CancellationToken ct);
Task<IReadOnlyList<GitCommitDto>> ListRecentCommitsAsync(string ownerLogin, string repoName, string token, string? branch, int take, CancellationToken ct);
Task<IReadOnlyList<GitPullRequestDto>> ListPullRequestsAsync(string ownerLogin, string repoName, string token, CancellationToken ct);
Task<GitRateLimitDto> GetRateLimitAsync(string token, CancellationToken ct);
```

### 4.3 Implementation — `Infrastructure/Services/GitHub/OctokitGitHubIntegrationService.cs`

- Fresh `Octokit.GitHubClient` per call, `Credentials = new Credentials(token)` — token is read from `GitConnectionEntry.Token` by the query handler and passed in for the duration of one request only, never persisted beyond that scope, never logged.
- Error mapping: `NotFoundException` → repo not found/token lacks access; `AuthorizationException` → token rejected; `RateLimitExceededException` → rate limited + reset time. Translate into `Application/Common/Exceptions/` types (`GitProviderUnavailableException`, `GitCredentialInvalidException`, `GitRateLimitExceededException`) caught by the existing exception-handling middleware.
- `ListRecentCommitsAsync`: cap to a recent window (e.g. last 30 commits per branch) to bound Octokit paging cost.

### 4.4 Sync strategy (unchanged: on-demand + short cache)

- `GetGitRepositoryOverviewQuery` live-calls GitHub through `IGitHubIntegrationService` on every request, wrapped in a read-through cache keyed `git:overview:{repositoryId}:{repoUrl}` with TTL ~60s, to stay well under GitHub's 5000 req/hr limit when multiple users open the tab.
  > **Implementation note:** the handler depends only on the `IDistributedCache` abstraction, never on Redis directly. It is now backed by **real Redis** — `Microsoft.Extensions.Caching.StackExchangeRedis` + `services.AddStackExchangeRedisCache(...)` reading `ConnectionStrings:Redis` (`Infrastructure/DependencyInjection.cs`). Local dev runs Redis via the `redis` service in `docker-compose.yml` (`redis:7-alpine`, port `6379`, healthcheck `redis-cli ping`) — start it with `docker compose up -d redis`. `appsettings.json` sets `ConnectionStrings:Redis` to `localhost:6379` for native `dotnet run`; the containerized `api` service in `docker-compose.yml` overrides it to `redis:6379` (Docker service-name DNS) via environment variable. An `AddDistributedMemoryCache()` in-process fallback is kept **commented out** right above the Redis registration for easy revert if Redis is ever unavailable/removed — no handler code changes needed either way since everything depends on `IDistributedCache` only.
- No background job, no webhook in Phase 1–2. Webhooks remain an optional future Phase 3 (§6) — if ever built, the webhook secret would also live in `GitConnectionEntry` (a new `WebhookSecret` field), never in the database.

---

## 5. Frontend

Much smaller than the v1 plan — no settings page, no admin service, no new guard/route beyond what already exists.

### 5.1 Models — `DASHBOARD.VIEW/src/app/models/git-repository.model.ts` (replaces the interfaces currently inline in `repos-mock.service.ts`)

```ts
export interface GitRepositoryConnectionSummary { repoUrl: string; ownerLogin: string; repoName: string; isPrimary: boolean; }
export interface GitBranch { name: string; isDefault: boolean; lastCommitSha: string; lastCommitAt: string; isStale: boolean; }
export interface GitCommit { sha: string; shortSha: string; branch: string; message: string; authorName: string; authorEmail: string; date: string; linkedWorkItemId: string | null; }
export interface GitPullRequest { number: number; title: string; sourceBranch: string; targetBranch: string; status: 'open' | 'closed' | 'merged'; reviewDecision: string; authorLogin: string; reviewers: string[]; linkedWorkItemId: string | null; mergedAt: string | null; }
export interface GitRateLimit { limit: number; remaining: number; resetAt: string; }
export interface GitRepositoryOverview { hasConnection: boolean; repoUrl: string | null; fullName: string | null; defaultBranch: string | null; branches: GitBranch[]; commits: GitCommit[]; pullRequests: GitPullRequest[]; rateLimit: GitRateLimit | null; status: string | null; lastSyncError: string | null; }
```

### 5.2 New service — `DASHBOARD.VIEW/src/app/services/git-repository.service.ts` (**replaces `repos-mock.service.ts`**, deleted entirely, no feature flag — confirmed)

- Fetches `api/repositories/{repoId}/git-repositories` (connection list, only used if a project has 2+ repos — drives a tab/dropdown selector) and `api/repositories/{repoId}/git-repositories/overview?repoUrl={url}`.
- Reactive on `RepositoryContextService.selectedRepo()` via `effect()`, same pattern as other project-scoped services in this codebase.
- Signals: `connections`, `overview`, `loading`, `error`, `hasConnection = computed(() => this.overview()?.hasConnection ?? false)`.

### 5.3 `repos-page.component.ts` changes

- Replace `ReposMockService` injection with `GitRepositoryService`.
- If `connections().length > 1`, render a simple tab/dropdown to switch which repo's overview is shown (calls `overview` reload with the selected `repoUrl`); if exactly 1, skip the selector entirely.
- States: loading (existing skeleton), **not connected** (`hasConnection === false`) — same empty-state copy/UI as today's mock ("no git repository connected for this project yet"), just now driven by the real `hasConnection` flag instead of mock data — confirmed no change in this UX, error/invalid/rate-limited banner using `overview.status`/`lastSyncError`, connected (render branches/commits/PRs/rate-limit as today).

### 5.4 Nothing else

No new route, no new guard, no new nav item, no new Settings page. `app.routes.ts`, `guards/privilege.guard.ts`, and the Settings nav component are untouched by this feature.

---

## 6. Phased Delivery Plan

| Phase | Scope | Ships | Explicit non-goals |
|---|---|---|---|
| **Phase 1 — Config + read surface** | `GitConnectionsOptions`/`GitConnectionEntry` + startup validator, `Application/GitRepositories/*` queries (masked connection list + overview with `HasConnection` flag but **no live GitHub call yet** — return `Status = "PendingValidation"` and empty collections when connected-but-not-yet-fetched), `Controllers/GitRepositories/*`, example `appsettings.Development.json` entries (local only, gitignored). | Backend can tell, per project, whether/which GitHub repo(s) are configured — no Octokit calls yet. | No commits/branches/PRs data yet; Repos tab still not cut over. |
| **Phase 2 — Live Repos tab** | `IGitHubIntegrationService` + `OctokitGitHubIntegrationService` (§4), `IDistributedCache` read-through cache backed by real Redis (see §4.4 implementation note), `git-repository.service.ts`, `repos-page.component.ts` cutover, **deletion of `repos-mock.service.ts`** (confirmed, no flag). | Repos tab shows real, live GitHub data scoped per project, sourced entirely from server config. | No webhooks — data freshness bounded by ~60s cache. |
| **Phase 3 — Real-time** | GitHub webhook receiver + HMAC validation (`GitConnectionEntry.WebhookSecret`, added to the config model) + MassTransit consumer + proactive cache invalidation. | Near-real-time updates without manual refresh, on top of the existing 60s-cache fallback. | No mirror tables — invalidation only, next request re-pulls live from GitHub. Admin must still register the webhook URL on the GitHub repo side manually (Settings → Webhooks → Add webhook) — this plan doesn't automate that GitHub-side step. |

### 6.1 Phase 3 detail

- **Route**: `POST api/webhooks/github/{repositoryId:guid}` — the exact URL an admin pastes into GitHub's "Add webhook" form for a given project's repo, so the receiver already knows which project the event is for without needing to parse it out of the payload.
- **Signature verification**: GitHub signs the raw request body with HMAC-SHA256 using the webhook secret, sent as `X-Hub-Signature-256: sha256=<hex>`. The controller must read the **raw body bytes** (before any JSON model binding) to recompute and compare the HMAC — reject with 401 on mismatch or on a repo with no `WebhookSecret` configured.
- **Which connection's secret to use**: resolve `Repository.Code` from `{repositoryId}` (same DB lookup as the existing queries), then match the webhook payload's `repository.full_name` (`owner/repo`) against the configured entries for that code to find the right `WebhookSecret` — a project with 2 linked repos will register 2 separate webhooks (one per GitHub repo), each posting to the same `{repositoryId}` route, disambiguated by `full_name` in the payload.
- **Flow after verification**: publish a `GitSyncRequestedEvent(RepositoryId, RepoUrl)` on the existing MassTransit/RabbitMQ bus (already wired in `Infrastructure/DependencyInjection.cs`) rather than invalidating the cache inline in the controller — keeps the webhook endpoint fast (ack GitHub's delivery quickly; GitHub retries/disables webhooks that are slow or error) and reuses the project's existing messaging infra. A new `GitSyncConsumer` handles the event by removing the matching `git:overview:{repositoryId}:{repoUrl}` key from `IDistributedCache`, so the next Repos-tab load/poll gets a cache miss and re-pulls fresh data from GitHub instead of waiting out the 60s TTL.
- **Events worth acting on**: `push` (commits/branches changed) and `pull_request` (opened/closed/merged/synchronize) — the controller can inspect the `X-GitHub-Event` header and ignore/204 anything else without even publishing an event.
- **Always 200/204 to GitHub** on a verified-but-uninteresting event, and 401 only on signature failure — GitHub disables a webhook after repeated non-2xx responses, so the controller must not 500 on, e.g., an event type it doesn't recognize.
| **Phase 4 — Production hardening (before Azure deploy)** | Azure Key Vault configuration provider wired in so `GitConnections:*:Token` values can be sourced from Key Vault secrets instead of plain App Service environment variables; add to `deploy-to-azure` pre-deployment checklist. | Production-grade secret sourcing. | — |

---

## 7. Decisions Log

1. **Cardinality** — confirmed: one project code → many connections (e.g. FE + BE repo), stored as a JSON array per code.
2. **Auth mode** — confirmed: GitHub Personal Access Token only.
3. **Credential storage** — **changed from v1**: no database, no app-level encryption code. Token lives in server-side configuration only (gitignored dev file locally; environment variables/Key Vault in Azure). Reasoning: eliminates the actual risk the user flagged — a secret should never be typed into a web form or transmitted over the API, so remove that surface entirely rather than encrypt it after the fact.
4. **Sync strategy** — confirmed: on-demand live pull via Octokit + 60s cache, backed by real Redis (see §4.4). `docker-compose.yml`'s `redis` service was already scaffolded (commented out) before this feature; it's now active. An in-memory `IDistributedCache` fallback is kept commented out in `Infrastructure/DependencyInjection.cs` in case Redis is ever unavailable.
5. **Permission scope** — **changed from v1**: there is no admin CRUD surface to gate anymore. Viewing the Repos tab (already-configured data, never includes tokens) is open to any project member, same as boards/backlog. *Editing* the connection list is now an ops/deployment action (whoever has access to the server's configuration or deployment pipeline), outside the application's own permission system entirely.
6. **Route/label naming** — moot in v2 (no new route/page). API route is `api/repositories/{repoId}/git-repositories` (+ `/overview`).
7. **Rollout safety** — confirmed: `repos-mock.service.ts` deleted entirely in Phase 2, no feature flag. Not-yet-configured projects show the same "not connected" empty state as today.
8. **GitHub URL format** — confirmed: full HTTPS clone URL, e.g. `https://github.com/namne2kzz/DASHBOARD.git`.

---

## 8. File Manifest (v2 — supersedes the v1 manifest)

**Backend**
```
DASHBOARD/Infrastructure/Settings/GitConnectionsOptions.cs           (GitConnectionsOptions + GitConnectionEntry)
DASHBOARD/Infrastructure/Settings/GitConnectionsOptionsValidator.cs  (IValidateOptions<GitConnectionsOptions>)
DASHBOARD/Application/Common/Interfaces/IGitHubIntegrationService.cs
DASHBOARD/Application/GitRepositories/Queries/ListGitRepositoryConnections/ListGitRepositoryConnectionsQuery(.cs, Handler.cs)
DASHBOARD/Application/GitRepositories/Queries/GetGitRepositoryOverview/GetGitRepositoryOverviewQuery(.cs, Handler.cs)
DASHBOARD/Application/GitRepositories/DTOs/GitRepositoryConnectionSummaryDto.cs
DASHBOARD/Application/GitRepositories/DTOs/GitRepositoryOverviewDto.cs
DASHBOARD/Application/GitRepositories/DTOs/GitBranchDto.cs
DASHBOARD/Application/GitRepositories/DTOs/GitCommitDto.cs
DASHBOARD/Application/GitRepositories/DTOs/GitPullRequestDto.cs
DASHBOARD/Application/GitRepositories/DTOs/GitRateLimitDto.cs
DASHBOARD/Controllers/GitRepositories/GitRepositoriesController.cs
DASHBOARD/Infrastructure/Services/GitHub/OctokitGitHubIntegrationService.cs
(Phase 3) DASHBOARD/Controllers/Webhooks/GitHubWebhookController.cs
(Phase 3) DASHBOARD/Infrastructure/Messaging/Consumers/GitSyncConsumer.cs
```
Edits to existing files: `DASHBOARD/DASHBOARD.csproj` (Octokit package), `DASHBOARD/Infrastructure/DependencyInjection.cs` (register `GitConnectionsOptions` + validator + `IGitHubIntegrationService`), `DASHBOARD/appsettings.Development.json` (gitignored — add real local `GitConnections` values, not committed).

**No migration, no new Domain entity, no new Domain enums, no new Commands, no `GitConnectionsController`, no Data Protection wiring.**

**Frontend**
```
DASHBOARD.VIEW/src/app/models/git-repository.model.ts
DASHBOARD.VIEW/src/app/services/git-repository.service.ts
```
Edits to existing files: `DASHBOARD.VIEW/src/app/pages/repos-page/repos-page.component.ts` (+ `.html`) (cutover to real service, optional repo-selector when 2+ connections). Deletion: `DASHBOARD.VIEW/src/app/services/repos-mock.service.ts` (Phase 2).

**No new route, no new guard, no new Settings page, no new nav item.**

---

## 9. Business Documentation follow-up

Per `CLAUDE.md`, once this feature ships, update `.claude/business/` per `RULES.md`: a new/extended doc noting the new **config-based** per-project GitHub connection model (keyed by `Repository.Code`, no in-app credential management) so future readers don't assume there's an admin UI for this.
