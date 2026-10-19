<div align="center">

# ⚡ NFlow

**A full-stack, self-hostable project management platform**
— backlog → sprints → boards → ship, all in one place.

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-14-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![Angular](https://img.shields.io/badge/Angular-19-DD0031?logo=angular&logoColor=white)](https://angular.dev/)
[![SQL Server](https://img.shields.io/badge/SQL%20Server-EF%20Core%2010-CC2927?logo=microsoftsqlserver&logoColor=white)](https://learn.microsoft.com/ef/core/)
[![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker&logoColor=white)](https://www.docker.com/)
[![License](https://img.shields.io/badge/license-Private-red)](#-license)

</div>

> NFlow covers the complete software development lifecycle — backlog, sprints, kanban boards, repositories, team management and analytics — and deploys on-premises with a single `docker compose` command.
>
> _Formerly **Nexus Kaban**. The repository, solution and project folders keep the `DASHBOARD` name._

---

## 🧱 Tech Stack

| Layer | Technology |
|---|---|
| 🖥️ **Backend** | .NET 10 · C# 14 · Clean Architecture · DDD · CQRS · MediatR · FluentValidation |
| 🎨 **Frontend** | Angular 19 · Signals · Standalone Components · RxJS |
| 🗄️ **Data** | SQL Server (EF Core 10) · Redis |
| 📨 **Messaging** | RabbitMQ |
| 🔭 **Observability** | OpenTelemetry · Application Insights |
| ☁️ **Infrastructure** | Docker · Docker Compose · Azure (AKS / App Service) |

---

## ✨ Features

| | |
|---|---|
| 📋 **Backlog** | Create, rank and promote work items to sprints · bulk ops · infinite scroll |
| 🏃 **Sprint Planning** | Lifecycle (create → activate → close) · capacity planning · workload view |
| 🗂️ **Kanban Board** | Customisable columns · drag-and-drop cards · WIP limits |
| 🧩 **Smart Board** | Freeform card board independent of sprints |
| 📝 **Work Items** | Rich detail panel · state transitions · labels · attachments · typed links · discussions |
| 📦 **Repositories** | Link Git repos · manage metadata · browse linked pipelines |
| 👥 **Members & Roles** | Invite members · custom roles with granular permissions · org hierarchy |
| 🔍 **Search** | Global cross-entity search |
| 📊 **Analytics** | Sprint velocity · burndown · cycle time |
| 🎯 **My Work** | Personal dashboard aggregating assigned items across projects |
| 🔔 **Notifications** | Real-time in-app push (SSE) with per-type preferences |
| 💬 **NHub** | Optional chat/meeting add-on via shared JWT SSO → [NHub](../HUB) |

---

## 🚀 Quick Start

> **Prerequisites** — [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker Engine + Compose v2). The .NET 10 SDK is only needed for native development.

```bash
# 1 · Copy and fill environment variables
cp .env.example .env

# 2 · Start backing services + API and web (profile "app")
docker compose --profile app up -d --build

# 3 · Open the app
open http://localhost:4201
```

🔑 Default seed account: **`admin@dashboard.local`** _(organization alias set at sign-in)_.

### 🌐 Service Endpoints (local)

| Service | URL |
|---|---|
| Web — Angular (container) | http://localhost:4201 |
| API (container) | http://localhost:55432 |
| SQL Server | `localhost:1433` |
| Redis | `localhost:6379` |
| RabbitMQ UI | http://localhost:15672 |
| DBGate (DB browser) | http://localhost:8047 |
| Mailpit (email preview) | http://localhost:8025 |

---

## 🛠️ Local Development (without Docker)

```bash
# Backend  →  http://localhost:5152
cd DASHBOARD
dotnet restore
dotnet run

# Frontend →  http://localhost:4200   (separate terminal)
cd DASHBOARD.VIEW
npm install
ng serve
```

All endpoints are versioned under **`/api/v1`**.

> 💡 **Windows port conflict** — Hyper-V / WinNAT can reserve ranges that include `55432`. If the API container reports _"ports are not available"_, run `net stop winnat` then `net start winnat` in an elevated shell.

---

## 🏗️ Architecture

Clean Architecture with strict inward-pointing dependencies — the domain knows nothing of the outside world.

```
┌─────────────────────────────────────────────────────────┐
│  Angular 19 SPA  ──HTTP /api/v1──▶  ASP.NET Core 10 API   │
└─────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        ▼                     ▼                     ▼
   Controllers          Application            Infrastructure
   (REST, 1/feature)    (CQRS · MediatR)       (EF Core · Identity
        │                     │                 · Messaging · Redis)
        └──────────┬──────────┘                        │
                   ▼                                    ▼
                Domain  ◀── depends on nothing      SQL Server · RabbitMQ
           (Entities · Events)
```

### 📁 Project Structure

```
DASHBOARD/                  # ASP.NET Core 10 backend
├── Domain/                 # Entities, enums, domain interfaces  (no dependencies)
├── Application/            # CQRS commands/queries, validators, DTOs
│   ├── Auth/ · Backlog/ · Sprints/ · SprintTasks/ · WorkItems/
│   └── Repositories/ · Members/ · SmartBoard/ · ...
├── Infrastructure/         # EF Core, Identity, messaging, services
├── Controllers/            # REST controllers (one per feature)
└── Middleware/

DASHBOARD.VIEW/             # Angular 19 frontend
└── src/app/
    ├── pages/              # Route-level smart components (lazy-loaded)
    ├── components/         # Shared display components
    ├── services/           # Feature services
    ├── models/             # TypeScript interfaces
    └── guards/             # Route guards
```

---

## ⚙️ Configuration

See `.env.example` for the full list. Key variables:

| Variable | Description |
|---|---|
| `MSSQL_SA_PASSWORD` | SQL Server SA password |
| `JWT_SECRET` | JWT signing secret — **must match** `HUB/.env` if NHub is enabled |
| `INTERNAL_API_TOKEN` | Service-to-service token for NHub → NFlow calls |
| `HUB_CHAT_INTERNAL_TOKEN` | Service-to-service token for NFlow → NHub calls |
| `RABBITMQ_USER` / `RABBITMQ_PASS` | RabbitMQ credentials |

### 🗃️ Database Migrations

EF Core migrations run **automatically on startup**. To add one manually:

```bash
cd DASHBOARD
dotnet ef migrations add <MigrationName>
```

---

## 💬 NHub Integration

NFlow acts as the **identity provider** for NHub. Both services share a JWT secret (`JWT_SECRET`) and a pair of internal service tokens. NHub is fully optional — NFlow runs standalone without it.

➡️ See the [NHub repository](../HUB) for setup.

---

## 📄 License

Private repository — **all rights reserved**.
