# Postman collection

`PermitToWork.postman_collection.json` — 32 requests in 7 folders, each with assertions.
Requests share state through collection variables, so **run them in order**.

## Before you start

1. Database up: `docker compose up -d sqlserver` (wait for healthy: `docker compose ps`)
2. Schema applied: `dotnet ef database update -p src/PermitToWork.Infrastructure -s src/PermitToWork.Api`
3. API running: `dotnet run --project src/PermitToWork.Api`
4. **Turn off SSL verification in Postman** — Settings → General → *SSL certificate
   verification* → off. The dev certificate is self-signed and every request fails
   without this.

## Importing

Postman → **Import** → drag in `PermitToWork.postman_collection.json`.

The `baseUrl` variable defaults to `https://localhost:7188`. If your API prints a
different port on startup, edit it on the collection's **Variables** tab.

## Running

**All at once:** right-click the collection → **Run collection** → *Run Permit To Work API*.
Every request should pass. That is the fastest way to prove the whole backend works.

**One at a time:** start with `2. Authentication → Login as administrator`. Its test script
stores the bearer token in a collection variable, and the collection is configured to send
that token on every other request — so you never paste a token by hand.

## What each folder proves

| Folder | Shows |
|---|---|
| 1. Smoke test | API is up; protected endpoints reject anonymous callers with 401 |
| 2. Authentication | Login issues a JWT; a wrong password gives the same message as an unknown email |
| 3. Reference data | Lookup endpoints, and captures the ids the next folder needs |
| 4. Employees | Create / read / search / update, 409 on a duplicate badge number, 400 on a malformed email |
| 5. Business rules | Domain invariants surfacing as 422 — illegal status transitions, self-management, a certification expiring before it was issued |
| 6. Registration | Registration claims an administrator-created record and never invents one |
| 7. Company scoping | A contractor's token sees only her own company's employees, and cannot create records |

## Re-running

Folders 4–7 create data, so a second full run fails on `Create an employee` with a 409 —
correctly, since `ACME-0042` now exists. To start clean:

```bash
docker compose down -v && docker compose up -d sqlserver
dotnet ef database update -p src/PermitToWork.Infrastructure -s src/PermitToWork.Api
```

Or change `employeeNumber` and `email` in *Create an employee* to something new.
