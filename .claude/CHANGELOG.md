# Changelog

## [2.0.0] — 2026-05-17

### Added
- `CLAUDE.md` — master full-stack instructions (BE + FE + DB)
- `.claude/skills/` — reusable Claude Code skill files:
  - `backend/` — .NET, Clean Architecture, DDD/CQRS
  - `frontend/` — Angular v20+ (Signals, standalone, RxJS)
  - `database/` — SQL Server, PostgreSQL, Redis, migrations, query optimization
- `.claude/agents/` — specialized agent personas
- `.claude/workflows/` — end-to-end automation workflows
- `.claude/hooks/` — pre/post generation validation hooks
- `.claude/memory/` — persistent context (global, patterns, mistakes)
- `prompts/system/` — expert full-stack system prompt
- `prompts/tasks/` — task-specific prompt templates (migrated from `configs/prompts/`)
- `tools/scripts/` — PowerShell setup and quality scripts
- `tools/pipelines/` — Azure DevOps + GitHub Actions CI/CD configs
- `tools/evals/` — AI output evaluation criteria
- `docs/architecture.md` — system architecture overview
- `docs/decisions/adr-template.md` — Architecture Decision Record template
- `docs/runbooks/` — deployment and incident runbooks
- Angular v20+ templates (standalone, Signals-based)
- Angular v20+ TypeScript examples

### Changed
- Restructured from flat `configs/` to `prompts/` hierarchy
- Moved `scripts/` → `tools/scripts/`
- `.claude/instructions/system-prompt.md` → `CLAUDE.md` (root) + `prompts/system/`

### Removed
- `configs/` directory (content migrated to `prompts/tasks/`)
- `mcp-servers/` directory (was empty)
- `scripts/` directory (content migrated to `tools/scripts/`)
- `.claude/instructions/` (content in `CLAUDE.md`)
- `.claude/context/` (replaced by `.claude/memory/`)

---

## [1.0.0] — 2024-01-01
- Initial .NET/Azure developer template
- Basic prompt templates for architecture, code-review, performance, security, testing
- Clean Architecture project structure
- Azure infrastructure templates (Terraform, Bicep, K8s)
