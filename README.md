# NFlow

A full-stack project management platform built with .NET 10 and Angular 19. Covers the complete software development lifecycle — backlog, sprints, kanban boards, repositories, team management, and analytics — deployable on-premises via Docker.

> Formerly *Nexus Kaban*. The repository, solution and project folders keep the `DASHBOARD` name.

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | .NET 10 · C# 14 · Clean Architecture · DDD · CQRS · MediatR · FluentValidation |
| Frontend | Angular 19 · Signals · Standalone Components · RxJS |
| Database | SQL Server (EF Core 10) · Redis |
| Messaging | RabbitMQ |
| Observability | OpenTelemetry · Application Insights |
| Infrastructure | Docker · Docker Compose · Azure (AKS / App Service) |

## Features

- **Backlog** — create, rank, and promote work items to sprints; bulk operations; infinite scroll
- **Sprint Planning** — sprint lifecycle (create → activate → close), capacity planning, workload view
- **Kanban Board** — customisable columns, drag-and-drop cards, WIP limits
- **Smart Board** — freeform card board independent of sprints
- **Work Items** — rich detail panel, state transitions, labels, components, versions, discussions
- **Repositories** — link Git repositories, manage metadata, browse linked pipelines
- **Members & Roles** — invite members, custom roles with granular permission sets, organisation hierarchy
- **Search** — global cross-entity search
- **Analytics** — sprint velocity, burndown, cycle time
- **My Work** — personal dashboard aggregating assigned items across projects
- **NHub integration** — optional chat/meeting add-on via shared JWT SSO (see [NHub](../HUB))

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker Engine + Compose v2)
- .NET 10 SDK — only required for local development without Docker

## Quick Start

```bash
# 1. Copy and fill environment variables
cp .env.example .env

# 2. Start backing services + the API and web containers (profile "app")
docker compose --profile app up -d --build

# 3. Open the app
open http://localhost:4201
```

Default seed account: `admin@dashboard.local` (organization alias set at sign-in).

### Service Endpoints (local)

| Service | URL |
|---|---|
| Web (Angular, container) | http://localhost:4201 |
| API (container) | http://localhost:55432 |
| SQL Server | localhost:1433 |
| Redis | localhost:6379 |
| RabbitMQ UI | http://localhost:15672 |
| DBGate (DB browser) | http://localhost:8047 |
| Mailpit (email preview) | http://localhost:8025 |

## Local Development (without Docker)

```bash
# Backend
cd DASHBOARD
dotnet restore
dotnet run

# Frontend (separate terminal)
cd DASHBOARD.VIEW
npm install
ng serve
```

Native dev runs the API at `http://localhost:5152` and the Angular dev server at `http://localhost:4200`. All endpoints are versioned under `/api/v1`.

> On Windows, Hyper-V/WinNAT can reserve port ranges that include `55432`. If the API container fails with "ports are not available", run `net stop winnat` then `net start winnat` in an elevated shell.

## Project Structure

```
DASHBOARD/                  # ASP.NET Core 10 backend
├── Domain/                 # Entities, enums, domain interfaces
├── Application/            # CQRS commands/queries, validators, DTOs
│   ├── Auth/
│   ├── Backlog/
│   ├── Sprints/
│   ├── SprintTasks/
│   ├── WorkItems/
│   ├── Repositories/
│   ├── Members/
│   ├── SmartBoard/
│   └── ...
├── Infrastructure/         # EF Core, Identity, messaging, services
├── Controllers/            # REST controllers (one per feature)
└── Middleware/

DASHBOARD.VIEW/             # Angular 19 frontend
├── src/app/
│   ├── pages/              # Route-level smart components
│   ├── components/         # Shared display components
│   ├── services/           # Feature services
│   ├── models/             # TypeScript interfaces
│   └── guards/             # Route guards
```

## Environment Variables

See `.env.example` for the full list. Key variables:

| Variable | Description |
|---|---|
| `MSSQL_SA_PASSWORD` | SQL Server SA password |
| `JWT_SECRET` | JWT signing secret — **must match** `HUB/.env` if NHub is enabled |
| `INTERNAL_API_TOKEN` | Service-to-service token for NHub → NFlow calls |
| `HUB_CHAT_INTERNAL_TOKEN` | Service-to-service token for NFlow → NHub calls |
| `RABBITMQ_USER / PASS` | RabbitMQ credentials |

## Database Migrations

EF Core migrations run automatically on startup. To add a new migration manually:

```bash
cd DASHBOARD
dotnet ef migrations add <MigrationName>
```

## NHub Integration

NFlow acts as the identity provider for NHub. Both services share a JWT secret (`JWT_SECRET`) and a pair of internal service tokens. NHub is optional — NFlow runs fully without it.

See the [NHub repository](../HUB) for setup instructions.

## License

Private repository — all rights reserved.
