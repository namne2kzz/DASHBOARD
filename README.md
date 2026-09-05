# DASHBOARD

A full-stack project management platform built with .NET 10 and Angular 19. Covers the complete software development lifecycle — backlog, sprints, kanban boards, repositories, wikis, team management, and analytics — deployable on-premises via Docker.

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
- **Wiki** — hierarchical pages with a rich-text editor
- **Members & Roles** — invite members, custom roles with granular permission sets, organisation hierarchy
- **Search** — global cross-entity search
- **Analytics** — sprint velocity, burndown, cycle time
- **My Work** — personal dashboard aggregating assigned items across projects
- **HUB integration** — optional chat/meeting add-on via shared JWT SSO (see [HUB](../HUB))

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker Engine + Compose v2)
- .NET 10 SDK — only required for local development without Docker

## Quick Start

```bash
# 1. Copy and fill environment variables
cp .env.example .env

# 2. Start all services
docker compose up -d --build

# 3. Open the app
open http://localhost:4200
```

Default seed credentials: `admin@system.local` / `Admin@123`

### Service Endpoints (local)

| Service | URL |
|---|---|
| Web (Angular) | http://localhost:4200 |
| API | http://localhost:5080 |
| SQL Server | localhost:1433 |
| Redis | localhost:6379 |
| RabbitMQ UI | http://localhost:15672 |
| DBGate (DB browser) | http://localhost:8090 |
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

The API defaults to `https://localhost:7xxx`; the Angular dev server proxies `/api` accordingly.

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
│   ├── Wiki/
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
| `JWT_SECRET` | JWT signing secret — **must match** `HUB/.env` if HUB is enabled |
| `INTERNAL_API_TOKEN` | Service-to-service token for HUB → DASHBOARD calls |
| `HUB_CHAT_INTERNAL_TOKEN` | Service-to-service token for DASHBOARD → HUB calls |
| `RABBITMQ_USER / PASS` | RabbitMQ credentials |

## Database Migrations

EF Core migrations run automatically on startup. To add a new migration manually:

```bash
cd DASHBOARD
dotnet ef migrations add <MigrationName>
```

## HUB Integration

DASHBOARD acts as the identity provider for HUB. Both services share a JWT secret (`JWT_SECRET`) and a pair of internal service tokens. HUB is optional — DASHBOARD runs fully without it.

See the [HUB repository](../HUB) for setup instructions.

## License

Private repository — all rights reserved.
