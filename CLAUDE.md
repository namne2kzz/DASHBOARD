# Full-Stack .NET + Angular + DB — Claude Instructions

## Stack

| Layer | Tech |
|-------|------|
| Backend | .NET 10, C# 14, Clean Architecture, DDD, CQRS, MediatR, FluentValidation |
| Frontend | Angular v19, Signals, Standalone Components, RxJS |
| Database | SQL Server + PostgreSQL (EF Core 10), Redis (StackExchange.Redis) |
| Cloud | Azure: AKS, App Service, Service Bus, Key Vault, Application Insights |
| DevOps | Azure DevOps, GitHub Actions, Docker, Kubernetes |

---

## Project Structure

```
DASHBOARD/                                      # .NET Backend (ASP.NET Core 10)
├── Domain/
│   ├── Common/                                 # Base entity, aggregate root, domain events
│   ├── Entities/                               # Domain entities
│   ├── Enums/                                  # Domain enums
│   └── Interfaces/                             # Domain repository/service contracts
├── Application/
│   ├── Common/
│   │   ├── Behaviors/                          # MediatR pipeline behaviors (validation, logging, perf)
│   │   ├── Exceptions/                         # Domain/application exception types
│   │   ├── Interfaces/                         # Application service contracts
│   │   └── Models/                             # Shared models (PagedList, Result<T>, etc.)
│   ├── Contracts/                              # Shared request/response contracts
│   ├── Auth/
│   │   ├── Commands/ (Login · Logout · RefreshToken)
│   │   ├── Queries/  (GetCurrentUser)
│   │   └── DTOs/
│   ├── Backlog/
│   │   ├── Commands/ (CreateBacklogItem · UpdateBacklogItem · DeleteBacklogItem · RankBacklogItem · PromoteToSprint)
│   │   ├── Queries/  (ListBacklogItems)
│   │   └── DTOs/
│   ├── Capacity/
│   │   ├── Commands/ (UpsertCapacityMember · RemoveCapacityMember · AddDayOff · RemoveDayOff)
│   │   ├── Queries/  (GetCapacity)
│   │   └── DTOs/
│   ├── CustomRoles/
│   │   ├── Commands/ (CreateCustomRole · UpdateCustomRole · DeleteCustomRole · CloneCustomRole)
│   │   ├── Queries/  (ListCustomRoles)
│   │   └── DTOs/
│   ├── Discussions/
│   │   ├── Commands/ (AddDiscussion · UpdateDiscussion · DeleteDiscussion)
│   │   ├── Queries/  (ListDiscussions)
│   │   └── DTOs/
│   ├── Invitations/
│   │   ├── Commands/ (CreateInvitation · AcceptInvitation)
│   │   └── DTOs/
│   ├── Members/
│   │   ├── Commands/ (AddMember · UpdateMemberRole · RemoveMember)
│   │   ├── Queries/  (ListMembers)
│   │   └── DTOs/
│   ├── Repositories/
│   │   ├── Commands/ (CreateRepository · UpdateRepository · ArchiveRepository · AddMetadata · DeleteMetadata)
│   │   ├── Queries/  (ListRepositories · GetRepository · ListMetadata)
│   │   └── DTOs/
│   ├── SmartBoard/
│   │   ├── Commands/ (CreateColumn · UpdateColumn · ReorderColumns · CreateCard · MoveCard · DeleteCard)
│   │   ├── Queries/  (GetBoard · GetCards)
│   │   └── DTOs/
│   ├── Sprints/
│   │   ├── Commands/ (CreateSprint · UpdateSprint · DeleteSprint · ActivateSprint · CloseSprint)
│   │   ├── Queries/  (ListSprints · GetSprintSummary)
│   │   └── DTOs/
│   ├── SprintTasks/
│   │   ├── Commands/ (CreateSprintTask · ChangeSprintTaskState · LogWork)
│   │   ├── Queries/  (ListSprintTasks)
│   │   └── DTOs/
│   ├── Users/
│   │   ├── Commands/ (UpdateProfile · ChangePassword)
│   │   ├── Queries/  (GetUser · ListUsers)
│   │   └── DTOs/
│   ├── Wiki/
│   │   ├── Commands/ (CreateWikiPage · UpdateWikiPage · MoveWikiPage · DeleteWikiPage)
│   │   ├── Queries/  (ListWikiPages · GetWikiPage)
│   │   └── DTOs/
│   └── WorkItems/
│       ├── Commands/ (CreateWorkItem · UpdateWorkItem · DeleteWorkItem · ChangeWorkItemState)
│       ├── Queries/  (ListWorkItems · GetWorkItem)
│       └── DTOs/
├── Infrastructure/
│   ├── Auth/                                   # JWT, token handling
│   ├── Email/
│   │   └── Templates/                          # Email templates
│   ├── Identity/                               # ASP.NET Identity integration
│   ├── Messaging/
│   │   └── Consumers/                          # Azure Service Bus consumers
│   ├── Persistence/
│   │   └── Configurations/                     # EF Core entity configurations
│   ├── Services/                               # Infrastructure service implementations
│   └── Settings/                              # Strongly-typed settings (IOptions<T>)
├── Controllers/                                # REST endpoints — one controller per feature
│   ├── Auth/Requests/
│   ├── Backlog/Requests/
│   ├── CustomRoles/Requests/
│   ├── Members/Requests/
│   ├── Repositories/Requests/
│   ├── SmartBoard/
│   ├── Sprints/Requests/
│   ├── Users/Requests/
│   ├── Wiki/
│   └── WorkItems/Requests/
├── Core/
│   └── Constants/                              # App-wide constants
├── Middleware/                                 # Custom ASP.NET middleware
└── Properties/

DASHBOARD.VIEW/src/app/                         # Angular v19 Frontend
├── components/                                 # Shared display components
│   ├── kanban-board/
│   ├── kanban-column/
│   ├── task-card/
│   ├── wiki-editor/
│   ├── wiki-rich-editor/
│   └── work-item-detail-panel/
├── guards/                                     # Route guards
├── interceptors/                               # HTTP interceptors
├── layout/                                     # App shell layout
├── models/                                     # Interfaces & types (*.model.ts)
├── pages/                                      # Route-level smart components (lazy-loaded)
│   ├── analytics-page/
│   ├── backlog-management-page/
│   ├── boards-page/
│   ├── login-page/
│   ├── overview-page/
│   ├── pipelines-page/
│   ├── repos-page/
│   ├── smart-board-page/
│   ├── sprint-planning-page/
│   ├── wiki-page/
│   └── wiki-spec-page/
├── resources/
├── services/                                   # Feature services
└── utils/                                      # Utility functions
```

---

## Hard Constraints

### Clean Architecture — layer dependencies (never violate)
| Layer | May depend on |
|-------|--------------|
| Domain | Nothing |
| Application | Domain only |
| Infrastructure | Application + Domain |
| WebApi | Application only |

### File conventions
- Angular component = 4 files always: `.ts` / `.html` / `.scss` / `.spec.ts` — never inline template
- Angular interfaces/types → `DASHBOARD.VIEW/src/app/models/*.model.ts` — never inside component or service files
- .NET request records → `DASHBOARD/Controllers/{Feature}/Requests/` — never inside Controller file
- .NET response DTOs → `DASHBOARD/Application/{Feature}/DTOs/` — never inside Controller file

### Code documentation — every public method, no exceptions
```csharp
/// <summary>One-line description.</summary>
/// <param name="ct">Cancellation token.</param>
/// <returns>OrderDto if found; null otherwise.</returns>
```
```typescript
/** One-line description. @param id Entity id. @returns Observable or null. */
```

---

## Automatic Workflow

Khi nhận bất kỳ yêu cầu code nào: **tự động** đọc `C:\DEV\DASHBOARD\.claude\agents\auto.md` và follow toàn bộ flow trong đó (detect agent/skill theo keyword → hiện list detected → generate → post-gen review) — không hỏi "Use auto agent? (yes/no)" trước nữa.

Chỉ bỏ qua flow này khi: yêu cầu rõ ràng không phải code (hỏi đáp, giải thích, đọc file...), hoặc user nói thẳng muốn code "thuần", không cần attach agent/skill.

---

## Skills Reference

Detailed patterns and code templates in `C:\DEV\DASHBOARD\.claude\skills\`:

| Area | Skills |
|------|--------|
| Backend | `generate-dotnet` · `clean-architecture` · `ddd-cqrs` · `unit-testing` · `testcontainers` · `aspire-orchestration` · `opentelemetry` · `resilience-patterns` · `snapshot-testing` · `api-versioning` |
| Frontend | `generate-angular` · `angular-signals` · `angular-rxjs` · `unit-testing-angular` |
| Database | `efcore-sqlserver` · `efcore-postgresql` · `redis-cache` · `migrations` · `query-optimization` |

Agents: `auto` · `dotnet-coder` · `angular-coder` · `reviewer` · `architect` · `db-optimizer` · `security-auditor` · `build-error-resolver` · `ui-tester`

Workflows (slash commands): `/build-feature` · `/fix-bug` · `/pr-review` · `/deploy-to-azure` · `/tdd` · `/security-scan` · `/health-check` · `/self-test`

Trước khi sinh code .NET/Angular, đọc thêm `C:\DEV\DASHBOARD\.claude\memory\mistakes.md` (lỗi thường gặp cần tránh) và `C:\DEV\DASHBOARD\.claude\memory\patterns.md` (pattern nên dùng).

---

## Business Documentation (`.claude/business/`)

Sau khi thêm/sửa **business logic** của 1 feature (rule mới, workflow mới, permission đổi...), PHẢI update lại file business document tương ứng trong `C:\DEV\DASHBOARD\.claude\business\`.

- Đọc `.claude/business/RULES.md` để biết quy tắc document (cấu trúc Update Log + Business Doc, ngôn ngữ, khi nào cần update).
- Mỗi feature có 1 file `{feature}.dod.md` (vd `backlog.dod.md`, `sprints.dod.md`...).
- Thay đổi ảnh hưởng domain tổng thể → update thêm `.claude/business/domain-business.md`.
- Refactor kỹ thuật thuần (không đổi business) → không cần update.

---

## UI Self-Test (`.claude/self-test/`)

Test UI thật bằng Playwright MCP (Microsoft Edge) thay cho test tay: `/self-test` hoặc agent `ui-tester`.

- Quy tắc ghi/đọc: `.claude/self-test/RULES.md` — **đọc trước khi chạy hoặc ghi**.
- Môi trường + danh sách module + ưu tiên: `.claude/self-test/test-plan.md`.
- Module đang code dở → khai vào `.claude/self-test/wip-features.md` để self-test skip.
- Kết quả từng module: `.claude/self-test/modules/{feature}.test.md` · bug: `bugs.md` · cải tiến: `improvements.md`.
- Credential: `.claude/self-test.local.json` (gitignored, mẫu ở `.claude/self-test.local.example.json`).
- App chạy **native, không qua Docker**, **không có `/health`**: API `dotnet run` (`:5152`), UI `npm start` (`:4200`). Không tự bật app, không tự chạy migration/seed.
- **Không sửa code app lúc test** — chỉ ghi bug/improvement. Không tự promote ticket lên Nexus.

---

## Implementation Plan (`.claude/plans/`)

Plan chi tiết cho feature lớn **trước khi code** (schema, API contract, thứ tự implement, migration). 1 feature = 1 file `{feature-slug}.md`.

Phân biệt: `plans/` = sẽ làm gì · `business/` = nghiệp vụ phải đúng thế nào · `self-test/` = đã kiểm chứng chưa.

---

## Kiến thức bổ sung (`.claude/docs/`)

Tài liệu nền do **user cung cấp** (domain knowledge, glossary, quy ước nghiệp vụ, spec, ADR, hợp đồng API bên thứ ba). Claude **đọc** khi cần, **không tự ghi** vào đây trừ khi user yêu cầu rõ ràng. Xem `.claude/docs/README.md`.
