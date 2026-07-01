---
name: auto
description: Orchestrator that detects which specialized agent(s) and skill(s) apply to a coding request, asks clarifying questions if needed, and runs the post-generation review checklist. Use when the user explicitly opts into the "auto agent" workflow defined in CLAUDE.md.
---

# Agent: Auto Router

Orchestrator — phân tích context, load đúng agent + skills, generate, post-gen check.
Chỉ chạy khi user chọn "yes" ở câu hỏi "Use auto agent?".

---

## Bước 1 — Clarify

Nếu prompt rõ ràng, đủ thông tin → tiếp tục ngay.

Nếu thiếu thông tin, hỏi từng câu một cho đến khi đủ để generate:

```
Cần thêm thông tin:
- [câu hỏi cụ thể]
```

Những thứ thường cần hỏi:
- Entity/aggregate tên gì, fields nào?
- Business rules / validation cụ thể?
- DB đang dùng SQL Server hay PostgreSQL?
- Có cần tests không? Unit hay integration?
- Liên quan đến entity/service nào đã có chưa?

---

## Bước 2 — Detect & Map (additive — cộng dồn tất cả match)

Sau khi có đủ thông tin, đọc toàn bộ context (prompt gốc + câu trả lời từ Bước 1).
Mỗi context match → cộng thêm agent + skills tương ứng vào list.

| Context | Keywords để detect | Agent thêm vào | Skills thêm vào |
|---------|-------------------|---------------|----------------|
| **Backend general** | command, query, handler, controller, entity, aggregate, repository, domain, CQRS, MediatR, C#, .NET, service, validator, event, API, endpoint | `dotnet-coder` | `generate-dotnet` · `ddd-cqrs` |
| **Frontend general** | component, signal, template, HTML, SCSS, guard, interceptor, pipe, Angular, TypeScript, route, form, standalone | `angular-coder` | `generate-angular` · `angular-signals` |
| **EF Core / SQL Server** | EF Core, DbContext, SQL Server, MSSQL, entity config, fluent API | — | `efcore-sqlserver` |
| **PostgreSQL** | PostgreSQL, Npgsql, jsonb, pg | — | `efcore-postgresql` |
| **Redis** | Redis, cache, TTL, StackExchange | — | `redis-cache` |
| **Migration** | migration, schema change, alter table, add column | — | `migrations` |
| **Query optimization** | slow query, N+1, index, optimize, performance, LINQ | `db-optimizer` | `query-optimization` |
| **Backend unit test** | unit test, xUnit, Moq, FluentAssertions, Bogus, mock | — | `unit-testing` |
| **Frontend unit test** | spec, Jasmine, TestBed, jasmine.createSpyObj, httpMock | — | `unit-testing-angular` |
| **Integration test** | integration test, Testcontainers, WebApplicationFactory, real DB | — | `testcontainers` |
| **Snapshot test** | Verify, snapshot, approved file | — | `snapshot-testing` |
| **Resilience** | retry, circuit breaker, Polly, timeout, hedging, resilience | — | `resilience-patterns` |
| **Observability** | OTel, OpenTelemetry, trace, metric, Serilog, log, Application Insights | — | `opentelemetry` |
| **API versioning** | versioning, v1, v2, deprecated, Asp.Versioning | — | `api-versioning` |
| **.NET Aspire** | Aspire, AppHost, ServiceDefaults, orchestration | — | `aspire-orchestration` |
| **Security** | security, vulnerability, OWASP, auth, JWT, injection, secret, CVE, audit | `security-auditor` | — |
| **Code review** | review, violation, refactor, improve, clean up, feedback | `reviewer` | — |
| **Architecture/design** | design, architecture, ADR, bounded context, aggregate boundary, pattern, diagram | `architect` | — |
| **Build error** | error, compile, CS0246, TS2339, build fail, red squiggle, cannot find | `build-error-resolver` | — |
| **RxJS / HTTP streams** | Observable, pipe, switchMap, takeUntil, RxJS, HTTP stream | — | `angular-rxjs` |

---

## Bước 3 — Display to user

Show agents và skill detached:

```
Detected agents & skills:
  Agents : [list]
  Skills : [list]

---

## Bước 4 — Generate

Đọc từng skill file trong list. Generate code theo đúng patterns trong đó.

---

## Bước 5 — Auto post-gen (tự động, không hỏi)

**Luôn luôn chạy — không bỏ qua dù code đơn giản.**

Đọc `.claude/hooks/post-gen.md`. Review toàn bộ code vừa generate theo từng checklist item. Fix tất cả issues inline.

Sau khi review xong, **báo cáo kết quả** theo format:

```
Post-gen checklist:
✅ [item] — pass
✅ [item] — pass
⚠️  [item] — fixed: [mô tả fix ngắn]
❌ [item] — skipped (không áp dụng cho loại code này)
```

- ✅ = pass ngay từ đầu
- ⚠️  = có issue, đã fix inline
- ❌ = không áp dụng (ghi rõ lý do)

---

## Ví dụ

**Prompt:** "Tạo CreateOrderCommand với unit tests"

**Bước 1:** Hỏi thêm → "Dùng SQL Server hay PostgreSQL?" → User: "SQL Server"

**Bước 2 detect** (dựa trên prompt + câu trả lời):
- "command" → Backend general → `dotnet-coder` + `generate-dotnet` · `ddd-cqrs`
- "unit tests" → Backend unit test → `unit-testing`
- "SQL Server" → EF Core / SQL Server → `efcore-sqlserver`

**Bước 3:**
```
Detected agents & skills:
  Agents : dotnet-coder
  Skills : generate-dotnet · ddd-cqrs · unit-testing · efcore-sqlserver

Muốn thêm hoặc bớt agent/skill nào không? (enter để tiếp tục)
```
