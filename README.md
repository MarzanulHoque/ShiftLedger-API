# ShiftLedger — API

Backend for **ShiftLedger**, a bike service shop management system: service jobs (task
management), billing, and reporting for a shop owner and their mechanics.
ASP.NET Core Web API on **.NET 10**, built with **Clean Architecture + CQRS (MediatR)**.

> The full product specification (scope, data model, business rules, build plan) lives in the
> `docs/` and `plans/` folders of the parent workspace, outside this repository.

---

## Tech stack

| Concern | Choice |
|---|---|
| Runtime | .NET 10 (LTS) |
| API style | Controllers (not Minimal APIs) |
| Architecture | Clean Architecture + CQRS via MediatR |
| Database | MySQL 8 via EF Core + Pomelo provider |
| Validation | FluentValidation |
| Real-time | SignalR (`/hubs/notifications`) |
| Reports | QuestPDF (PDF) · ClosedXML (Excel) |
| API docs | Swagger / OpenAPI (Swashbuckle) at `/swagger` |
| Tests | xUnit · FluentAssertions |

**Deliberate version pins:** MediatR is held at **12.4.1** and FluentAssertions at **7.x** —
these are the last releases under a free (Apache) licence; later major versions require a
paid commercial licence. QuestPDF uses its free Community license (set in `ReportExporter`).

## Project layout

```
ShiftLedger.slnx
src/
  Domain/            Entities, enums — pure C#, no framework refs
  Application/       CQRS commands/queries + handlers, DTOs, validators, interfaces
                        Jobs/  Bills/  Dashboards/  Reports/  Notifications/  Users/  Departments/
                        EmployeeProfiles/  PayRates/  Auth/   (Employee/Pay — parked for v2)
  Infrastructure/    EF Core, persistence, JWT, QuestPDF/ClosedXML export (implements Application interfaces)
  Api/               Controllers, DI wiring, middleware, SignalR hub, Swagger — thin, delegates to MediatR
tests/
  Application.UnitTests/    Domain + handler unit tests (no DB)
  Api.IntegrationTests/     Endpoint → handler → DB tests, real MySQL via Testcontainers-style fixture
```

**Dependency rule:** dependencies point inward. `Domain` depends on nothing; `Api` depends on
everything. Each layer registers its own services via an `AddApplication()` / `AddInfrastructure()`
extension method (see `DependencyInjection.cs` in each layer).

## Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- MySQL 8 (local install or Docker)
- EF Core tools: `dotnet tool install --global dotnet-ef`

## Getting started

```bash
# restore + build
dotnet build ShiftLedger.slnx

# apply migrations (creates the schema)
dotnet ef database update --project src/Infrastructure --startup-project src/Api

# run the API (Swagger UI at http://localhost:5184/swagger)
dotnet run --project src/Api --launch-profile http

# run all tests (unit — no DB needed)
dotnet test tests/Application.UnitTests
```

On first run, a **bootstrap Admin** is seeded from `BootstrapAdmin:Email`/`BootstrapAdmin:Password`
(config) if no users exist yet. In `Development`, fixed **demo accounts** are also seeded
(see `Infrastructure/Persistence/DbSeeder.cs` for the exact emails/passwords — dev-only, rotate
before any non-dev environment).

**Integration tests** run against a real MySQL using a dedicated `*_test` schema (dropped and
re-migrated per run — never point this at a database you care about). Provide the connection via
an env var:

```bash
ConnectionStrings__Default="server=localhost;port=3306;database=shiftledger_test;user=root;password=***" \
  dotnet test tests/Api.IntegrationTests
```

### Configuration & secrets

The database connection string and JWT signing key are **never committed**. Locally they are
supplied via `appsettings.Development.json` (git-ignored — copy `appsettings.Development.json.example`
and fill in real values) or .NET user-secrets; in other environments via environment variables.
The connection string key is `ConnectionStrings:Default`.

## API surface (v1)

All endpoints are versioned under `/api/v1`. Roles: **Admin** (shop owner) and **Employee**
(mechanic). List endpoints are paginated (`?page=&pageSize=`, default 20 / max 100) and return
`{ items, page, pageSize, totalCount }`.

| Area | Endpoints |
|---|---|
| Auth | `POST /auth/login`, `/auth/refresh`, `/auth/forgot-password`, `/auth/reset-password` |
| Users & org | `GET/POST /users`, `/departments` (Admin) |
| Service jobs | `GET/POST /jobs`, `PATCH /jobs/{id}/status`, `PATCH /jobs/{id}/assign`, `/jobs/{id}/comments`, `/jobs/{id}/history` |
| Billing | `GET/POST /jobs/{id}/bill`, `/bills/{id}/line-items`, `PATCH /bills/{id}/pay`, `GET /bills` |
| Dashboards | `GET /dashboard/admin`, `GET /dashboard/me` |
| Reports | `GET /reports/{type}?format=json\|pdf\|excel` — Jobs, Revenue, UnpaidBills, BillingHistory, MechanicProductivity |
| Notifications | `GET /notifications`, `PATCH /notifications/{id}/read` — real-time via `/hubs/notifications` (SignalR, JWT-authenticated) |

Full contract (request/response shapes) is authoritative in Swagger at `/swagger`.

## Build status

**Backend feature-complete for v1** (Task management + Billing + Reporting). Phases P0–P6 done;
currently in **P7 — hardening** (pagination ✅, rule-ID test coverage ✅, security pass ✅; this
fresh-clone check is the last item). Employee management/payroll and parts inventory are
**deferred to v2** — see `docs/01_MVP_Scope.md` in the parent workspace. The React frontend
(`ShiftLedger-Client`) is built after the backend, per `plans/Implementation_Plan.md`.
