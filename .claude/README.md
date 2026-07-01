# Full-Stack .NET + Angular + DB — Claude Code Template

A production-ready Claude Code template: .NET 10 · Angular v20+ · SQL Server + PostgreSQL + Redis · Azure.

---

## Quick Start

1. **Read `CLAUDE.md`** — master instructions, auto-loaded by Claude Code
2. **Configure `.claude/config.json`** — project name, paths, stack versions
3. **Update `.claude/memory/global.md`** — fill in project context, sprint, links
4. **Use skills/agents/workflows** — see `CLAUDE.md` for full reference list

---

## Template Structure

```
.claude/
├── config.json              ← Stack config, model settings
├── memory/
│   ├── global.md            ← Project context (update per project)
│   ├── patterns.md          ← Established code patterns
│   └── mistakes.md          ← Anti-patterns to avoid
├── skills/
│   ├── backend/             ← .NET: generate, clean-arch, ddd-cqrs, testcontainers, otel...
│   ├── frontend/            ← Angular: generate, signals, rxjs, unit-testing
│   └── database/            ← SQL Server, PostgreSQL, Redis, migrations
├── agents/                  ← dotnet-coder, angular-coder, reviewer, architect, db-optimizer,
│                               security-auditor, build-error-resolver
├── workflows/               ← build-feature, fix-bug, code-review, deploy-to-azure,
│                               tdd, security-scan, health-check
├── hooks/
│   ├── post-gen.md          ← Post-generation review checklist
│   ├── pre-gen.md           ← Pre-generation context checklist
│   ├── validation.md        ← Output validation criteria
│   └── scripts/             ← Actual hook scripts (post-write.ps1, pre-write.ps1)
└── settings.json            ← Claude Code hooks config (auto-format, block secrets)
```

```
docs/
├── architecture.md          ← System architecture overview
├── testing-strategy.md      ← Unit / integration / snapshot / E2E strategy
├── observability.md         ← OTel setup, Serilog, Application Insights
├── decisions/               ← Architecture Decision Records (ADR)
└── runbooks/                ← Deployment & incident response

prompts/
├── system/expert-fullstack.md  ← Paste into Claude Custom Instructions
└── tasks/                      ← architecture.md, code-review.md, performance.md, security.md, testing.md

tools/
├── scripts/                 ← PowerShell: init-project.ps1, setup-db.ps1, check-quality.ps1
└── pipelines/               ← Azure Pipelines + GitHub Actions CI/CD

templates/
├── dotnet/                  ← C#: CommandHandler, Controller, DomainEntity, UnitTests
├── angular/                 ← TS/HTML/SCSS/spec + Signal store + Interceptor + Builder
└── infrastructure/          ← K8s deployment YAML
```

---

## Setup — Tích hợp vào project của bạn

**Copy 2 thứ vào root solution (nơi chạy `claude`):**

```
YourSolution/
├── CLAUDE.md        ← copy từ template
├── .claude/         ← copy từ template (skills, agents, hooks, settings...)
├── src/             ← .NET source
├── frontend/        ← Angular source
└── YourApp.sln
```

**Sau khi copy, update 2 file:**

| File | Update gì |
|------|-----------|
| `CLAUDE.md` | Đổi project structure cho đúng namespace/path thực tế |
| `.claude/memory/global.md` | Điền project context, team, sprint, external links |

> Template docs (`README.md`, `CHANGELOG.md`, `ROADMAP.md`, `templates/`, `docs/`) — giữ trong folder template để tham khảo, không cần copy sang project.

---

## Daily Usage

### Generate + Auto-Review (1 prompt)

```
[agent] [yêu cầu của bạn]

After generating, verify against .claude/hooks/post-gen.md. Fix all failures inline. Show final code only.
```

**Backend example:**
```
[dotnet-coder] Generate CreateOrderCommand:
- Entity: Order (CustomerId, Note, Items)
- Deps: IOrderRepository, ICustomerRepository, IUnitOfWork
- Validation: CustomerId required, Items not empty
- Return: Result<Guid> — include unit tests

After generating, verify against .claude/hooks/post-gen.md. Fix all failures inline. Show final code only.
```

**Frontend example:**
```
[angular-coder] Generate OrderListPage:
- Load orders from OrderService (GET /api/orders)
- Show loading / error / empty / loaded states
- Click row → navigate to /orders/:id
- Separate .ts / .html / .scss / .spec.ts files

After generating, verify against .claude/hooks/post-gen.md. Fix all failures inline. Show final code only.
```

### Full-Stack Feature (end-to-end)

```
/workflow build-feature [mô tả feature]
```

Workflow gồm 6 bước: architect → DB schema → backend CQRS → API controller → Angular → review.

---

## MCP (Model Context Protocol)

Claude kết nối trực tiếp với DB, filesystem, GitHub — không cần paste code/schema vào chat.

### Setup

1. `.claude/mcp.json` đã có sẵn với placeholder values — chỉ cần replace
2. File bị gitignore — credentials không bao giờ commit
3. Xóa server nào không dùng để tránh lỗi connect khi khởi động
4. Restart Claude Code sau khi save

> Mất file: copy lại từ `.claude/mcp.json.example` rồi điền values

### Placeholders cần replace

| Placeholder | Thay bằng |
|---|---|
| `C:/DEV/YOUR_PROJECT_FOLDER` | Root folder project thực tế |
| `ghp_REPLACE_WITH_YOUR_GITHUB_TOKEN` | GitHub PAT — `github.com/settings/tokens` → `repo + issues + pull_requests` |
| `YOUR_PG_USER / PASSWORD / DATABASE` | PostgreSQL credentials — dùng readonly user |
| `YOUR_MSSQL_USER / PASSWORD / DATABASE` | SQL Server credentials — Azure SQL: thêm `MSSQL_ENCRYPT=true` |
| `redis://localhost:6379` | Redis URL — có auth: `redis://:PWD@host:6379` |
| `playwright` | Không cần config — chạy `npx playwright install` một lần |
| `YOUR_ORG_NAME` + `AZURE_DEVOPS_PAT` | Azure DevOps org + PAT từ User Settings |
| `YourApp.sln` path (roslyn) | Đường dẫn đến `.sln` file thực tế |

### Available servers

| Server | Dùng cho |
|--------|---------|
| `filesystem` | Claude đọc/ghi file trực tiếp, không cần paste code |
| `github` | Xem issues/PRs, tạo PR không cần rời terminal |
| `postgres` / `sqlserver` | Claude query DB thực — debug, verify migration |
| `redis` | Inspect cache keys, debug TTL |
| `playwright` | Browser automation — test Angular UI |
| `azure-devops` | Xem pipeline runs, work items |
| `roslyn` | Semantic .NET code analysis — find symbol, circular deps (~10x token savings vs filesystem) |

---

## System Prompt Setup

Copy nội dung `prompts/system/expert-fullstack.md` vào Claude's **Custom Instructions** để có expert-level responses trong mọi conversation.
