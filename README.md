# ShiftLedger — API

Backend for **ShiftLedger**, a workforce management system (tasks, time tracking, payroll).
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
| Validation | FluentValidation |
| Database | MySQL 8 via EF Core + Pomelo provider *(wired in phase P1)* |
| API docs | Swagger / OpenAPI (Swashbuckle) at `/swagger` |
| Tests | xUnit · FluentAssertions · NSubstitute |

**Deliberate version pins:** MediatR is held at **12.4.1** and FluentAssertions at **7.x** —
these are the last releases under a free (Apache) licence; later major versions require a
paid commercial licence.

## Project layout

```
ShiftLedger.slnx
src/
  Domain/            Entities, enums, value objects — pure C#, no framework refs
  Application/       CQRS commands/queries + handlers, DTOs, validators, interfaces
  Infrastructure/    EF Core, persistence, external services (implements Application interfaces)
  Api/               Controllers, DI wiring, middleware, Swagger — thin, delegates to MediatR
tests/
  Application.UnitTests/    Domain + handler unit tests (no DB)
  Api.IntegrationTests/     Endpoint → handler → DB tests (from phase P1)
```

**Dependency rule:** dependencies point inward. `Domain` depends on nothing; `Api` depends on
everything. Each layer registers its own services via an `AddApplication()` / `AddInfrastructure()`
extension method (see `DependencyInjection.cs` in each layer).

## Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- MySQL 8 (local install or Docker) — **required from phase P1**, not for the current scaffold
- EF Core tools: `dotnet tool install --global dotnet-ef`

## Getting started

```bash
# restore + build
dotnet build ShiftLedger.slnx

# run the API (Swagger UI at http://localhost:5184/swagger)
dotnet run --project src/Api --launch-profile http

# run all tests (unit)
dotnet test ShiftLedger.slnx
```

**Integration tests** run against a real MySQL using a dedicated `*_test` schema (created/dropped
per run). Provide the connection via an env var pointing at that schema, e.g.:

```bash
ConnectionStrings__Default="server=localhost;port=3306;database=shiftledger_test;user=root;password=***" \
  dotnet test tests/Api.IntegrationTests
```

### Configuration & secrets

The database connection string and JWT signing key are **never committed**. Locally they are
supplied via .NET user-secrets or `appsettings.Development.json` (git-ignored values); in other
environments via environment variables. The connection string key is `ConnectionStrings:Default`.

## Build status

Currently at **phase P0 — scaffold**: solution builds, API boots, `/swagger` loads, both test
projects are green. Subsequent phases (persistence, auth, tasks, time, payroll, …) follow
`plans/Implementation_Plan.md` in the workspace.
