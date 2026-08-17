# Postman collections

Two collections. Both share state through collection variables, so **run each in order**.

| File | Covers |
|---|---|
| `PermitToWork.postman_collection.json` | Phase 1 — auth, employees, company scoping. 32 requests, 7 folders. |
| `PermitToWork.Permits.postman_collection.json` | Phase 2 — the permit lifecycle, the certification rule, approval panels, expiry. 9 folders. |

They are independent: the permits collection creates its own people, so it does not need
the Phase 1 one to have run first.

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

## What the permits collection proves

| Folder | Shows |
|---|---|
| 1–3 | Sign in, capture reference ids, create a certified welder and an uncertified labourer |
| 4 | A permit is numbered `HW-2026-0001` by the server, starts as Draft, and cannot be submitted with nobody on it |
| **5** | **The certification rule.** The labourer is refused by name, naming the missing certificate; the welder is accepted. The same person is later accepted onto a Cold Work permit, because the rule is per permit type |
| 6 | The facility panel is copied onto the permit at submission; the crew freezes while people sign; one decisive signature activates it; the issuer is whoever signed last |
| 7 | Crews reopen once work is live; suspend, resume, close — and closure is the creator's alone |
| 8 | Approval panels, and that seating somebody twice is a 409 |
| **9** | **Expiry.** A backdated permit is activated, the sweep expires it with a null actor, and running the sweep again reports zero |

Folder 5 and folder 9 are the two worth reading. The first is where Phase 1 and Phase 2
meet; the second is what stops an abandoned permit reading as live work for ever.

## What each Phase 1 folder proves

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
