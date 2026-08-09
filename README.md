# Permit To Work (PTW)

A full-stack web application for managing employees, work teams, and **Permits To Work** —
the formal authorisation required before hazardous work begins on an industrial site.

Final project for **Coding Factory 10**, Athens University of Economics and Business.

> **Status: in development.** Phase 1 (identity, employees, teams) is being built first;
> the permit module follows. This README is filled in as each piece lands and is verified
> from a clean clone before submission.

---

## Tech stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core 10 Web API (C#) |
| Data access | Entity Framework Core 10, code-first migrations |
| Database | SQL Server 2022 (Docker) |
| Auth | ASP.NET Core Identity + JWT bearer tokens |
| API docs | Swagger / OpenAPI (Swashbuckle) |
| Frontend | Angular SPA |
| Tests | xUnit + FluentAssertions + NSubstitute; Postman collection for the API |

## Architecture

Clean, inward-pointing layers:

```
src/
├─ PermitToWork.Domain          entities, value objects, domain rules — zero dependencies
├─ PermitToWork.Application     use-case services, DTOs, repository interfaces
├─ PermitToWork.Infrastructure  EF Core, repository implementations, Identity
└─ PermitToWork.Api             controllers, auth, Swagger, DI composition root
tests/
└─ PermitToWork.Tests
client/                         Angular workspace
docs/                           design documents
postman/                        API integration test collection
```

`Application` defines repository interfaces; `Infrastructure` implements them. Controllers
never touch a `DbContext`.

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [Node.js 20+](https://nodejs.org/) and npm
- `dotnet-ef` CLI: `dotnet tool install --global dotnet-ef`

## Running locally

**1. Start SQL Server**

```bash
docker compose up -d sqlserver
```

Wait for the container to report healthy: `docker compose ps`.

**2. Apply migrations**

```bash
dotnet ef database update -p src/PermitToWork.Infrastructure -s src/PermitToWork.Api
```

**3. Run the API**

```bash
dotnet run --project src/PermitToWork.Api
```

Swagger UI: <https://localhost:7188/swagger>
Health check: <https://localhost:7188/api/health>

**4. Run the frontend**

```bash
cd client
npm install
npm start
```

App: <http://localhost:4200>

## Tests

```bash
dotnet test PermitToWork.sln
```

## Configuration

`src/PermitToWork.Api/appsettings.json` holds non-secret defaults. The SA password and JWT
signing key are development-only values and are overridden in any real deployment via
environment variables or user secrets:

```bash
dotnet user-secrets set "Jwt:SigningKey" "<a long random string>" -p src/PermitToWork.Api
```

## Documentation

- [`docs/01-domain-and-scope.md`](docs/01-domain-and-scope.md) — domain model, bounded
  contexts, decisions and open questions.
