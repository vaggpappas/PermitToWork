# Permit To Work — Domain Model & Scope (v0.2 — DRAFT FOR REVIEW)

> Status: proposal. Everything marked **❓** is an open question for Vagelis.
> Nothing is implemented yet — we agree this first, then scaffold.

## Decisions taken (2026-08-09)

| # | Decision |
|---|---|
| D1 | Backend = 4 projects: `Domain` / `Application` / `Infrastructure` / `Api` (+ `Tests`) |
| D2 | Auth = ASP.NET Core Identity + JWT bearer |
| D3 | Delivery = Phase 1 Employees & Teams end-to-end, then Phase 2 Permits |
| D4 | An employee may belong to **multiple teams** simultaneously → `TeamMembership` stays a first-class entity with history |
| D5 | Contractors **can log in** → every query touching employees/teams/permits must be company-scoped for contractor users (see §3.1) |
| D6 | No profile photos in v1 |
| D7 | UI language = English |
| D8 | `CLAUDE.md` keeps the design principles + test conventions; build commands rewritten for this solution |

---

## 1. What the app is

A web application where an employee registers, completes a profile, is assigned to one or
more **work teams**, and — through those assignments — participates in **Permits To Work**
(PTW): the formal authorisation document required before hazardous work starts on an
industrial site.

Delivery is in two phases:

| Phase | Contents | Why first |
|---|---|---|
| **1** | Identity/auth, Employee profiles, Teams, Org hierarchy | The permit module references all of it. Building permits first means constantly stubbing employees. |
| **2** | Permit lifecycle, resources, approvals, audit | The headline feature. Built on a foundation that already works end-to-end. |

---

## 2. Bounded contexts

```
┌──────────────────┐   ┌────────────────────┐   ┌──────────────────┐
│  Identity        │   │  Organization      │   │  Permitting      │
│  (Phase 1)       │   │  (Phase 1)         │   │  (Phase 2)       │
│                  │   │                    │   │                  │
│  ApplicationUser │──▶│  Employee          │──▶│  Permit          │
│  Role            │   │  Team              │   │  PermitWorker    │
│  RefreshToken    │   │  TeamMembership    │   │  PermitResource  │
│                  │   │  Certification     │   │  PermitEvent     │
│                  │   │  Facility/Building │   │  Approval        │
└──────────────────┘   └────────────────────┘   └──────────────────┘
```

Separate contexts, one database, one `DbContext`, schema-separated
(`identity.`, `org.`, `ptw.`). Simple to run, still clearly modular.

---

## 3. Identity context

`ApplicationUser : IdentityUser<Guid>` — credentials only. No profile data here.
The profile lives on `Employee`; `Employee.UserId` is the link (1:1, optional on the
Employee side so an admin can pre-create employee records before they register).

### Application roles (global, coarse)

| Role | Can |
|---|---|
| `Administrator` | Everything. Manage users, employees, teams, org structure, lookups. |
| `SafetyOfficer` | Approve / reject / suspend permits across the site. Read all employees. |
| `Supervisor` | Manage their own team's members, create and submit permits for their team. |
| `Employee` | Read own profile, edit a subset of it, see permits they're assigned to. |

**Important design point:** *Permit Issuer*, *Permit Receiver* and *Creator* (fields ⑫⑬⑭
in your mockup) are **not** global roles — they are per-permit assignments. A person can be
the receiver on one permit and uninvolved in another. So they are properties of the
`Permit` aggregate, guarded by policies like `CanIssuePermits` which is granted by the
global role. Modelling them as Identity roles would be wrong and would make
authorisation checks impossible to write correctly.

### 3.1 Contractor access (D5)

Contractors log in, so authorization has **two dimensions**: role (what you may do) and
company scope (whose data you may see). An `Administrator` or `SafetyOfficer` from the
plant owner sees everything; a `Supervisor` at Acme Maintenance sees only Acme employees,
Acme teams, and permits Acme is party to.

This must be enforced in one place, not sprinkled through controllers. Plan: a
`ICurrentUser` abstraction exposing `UserId`, `EmployeeId`, `CompanyId`, `IsInternal`,
and an EF Core **global query filter** on company-scoped entities driven by it. That way
"forgot to filter by company" is a class of bug that cannot occur — an errors-out-of-
existence design rather than a check repeated in 30 handlers.

Roles therefore become: `Administrator`, `SafetyOfficer` (internal only),
`Supervisor` (internal or contractor), `Employee` (internal or contractor).

---

## 4. Organization context

### 4.1 Employee (aggregate root)

| Field | Type | Notes |
|---|---|---|
| `Id` | Guid | |
| `EmployeeNumber` | `EmployeeNumber` (VO) | Unique, e.g. `EMP-00142`. Business identity. |
| `FullName` | `PersonName` (VO) | First, Last. |
| `DateOfBirth` | DateOnly? | |
| `Contact` | `ContactInfo` (VO) | Email, PhoneNumber. Email unique. |
| `Trade` | `Trade` (lookup) | The craft: "Pipe Fitter", "Welder Gr.3", "Electrician". Gates what work they may do. |
| `JobTitle` | string | The org-chart title: "Senior Maintenance Engineer". Free text — titles are endless and not rule-bearing. |
| `ManagerId` | Guid? (self-FK → Employee) | "Under whom is working". Null for the top of the chain. |
| `Address` | `Address` (VO) | Street, City, PostalCode, Country. |
| `CompanyId` | FK → `Company` | Employer. Plant owner or contractor. |
| `HireDate` | DateOnly | |
| `Status` | `EmploymentStatus` enum | `Active`, `Suspended`, `Terminated`. Not a bool — per MIRO. |
| `UserId` | Guid? | Link to Identity account, null until they register. |
| `Certifications` | `Certification[]` | Owned collection, see below. |

No profile photo in v1 (D6).

**Age is not stored** — it is computed from `DateOfBirth`. Storing it would guarantee a
wrong value the day after it is written. Same reasoning applies to certification validity.

`Trade` and `JobTitle` are deliberately two fields: the trade is rule-bearing (a Hot Work
permit needs a certified welder), the title is descriptive. Collapsing them into one string
would mean parsing text to make a safety decision.

`ManagerId` is a self-reference on Employee rather than a separate "reporting line" table,
because an employee has exactly one manager at a time and we don't need its history.

### 4.2 Certification (owned entity of Employee)

Drives the `CERT. EXPIRY` column and the "Hot work certified" note in your mockup, and
later gates whether a worker may be added to a Hot Work permit.

`Id`, `Type` (lookup: Hot Work, Confined Space Entry, Working at Height, LOTO, First Aid…),
`IssuedBy`, `IssuedOn`, `ExpiresOn`, `DocumentUrl?`

Derived: `IsValidOn(date)`.

### 4.3 Team

| Field | Notes |
|---|---|
| `Id`, `Name`, `Code` | Code unique, e.g. `MECH-A`. |
| `Description` | |
| `LeaderId` | FK → Employee. A team has exactly one leader. |
| `FacilityId` | The team's home facility. |
| `IsActive` | |
| `Members` | via `TeamMembership` |

### 4.4 TeamMembership (join entity, not a plain many-to-many)

`EmployeeId`, `TeamId`, `JoinedOn`, `LeftOn?`, `RoleInTeam` (enum: `Member`, `Deputy`, `Leader`)

Modelled as an entity rather than a skip-navigation because membership has a history and
attributes. `LeftOn == null` means currently active — and we expose
`Team.ActiveMembers` so callers never have to know that rule.

Confirmed by D4: multiple concurrent memberships are allowed.

### 4.5 Org hierarchy — CONFIRMED

Three real entities, each a row in the database:

```
Facility          a site / place on the map          "Refinery North"
   └─< Building   a unit or area (many per facility) "Distillation Unit 3"
        └─< Location   a specific space inside it    "Room 2.14", "East garage", "Pump closet"
```

`Company` is a **separate axis**, not part of this tree. It answers "who employs this
person / who performs this work", which is independent of "where". A contractor works
across many facilities; a facility hosts many contractors.

Each level: `Id`, `Code`, `Name`, `Description?`, `IsActive`, plus the parent FK.
A permit points at a `Location` — and therefore knows its Building and Facility
transitively, so those are never stored twice on the permit.

### 4.6 Lookups (admin-managed reference data)

`Trade`, `CertificationType`, `Company`, `PermitType`, `TaskGroup`, `PpeItem`,
`EquipmentType`, `Chemical`. All simple `Id / Code / Name / IsActive` tables so you never
hard-code a dropdown.

---

## 5. Permitting context (Phase 2 — sketch only, we design it properly later)

Recorded now only so Phase 1 doesn't paint us into a corner.

- `Permit` aggregate root: `PermitNumber` (PTW-2026-0001), `Type`, `TaskGroup`,
  `WorkPackage/Project`, `ValidFrom`/`ValidTo` (a `DateTimeRange` VO — start+end date/time
  as one concept, so "end before start" is unrepresentable), `Status`, `WorkDescription`,
  `Location`, `BuildingId`, `FacilityId`, `Notes`.
- Roles on the permit: `CreatedById`, `IssuerId`, `ReceiverId`. (The mockup is indicative
  only — screens will be designed properly in Phase 2, not copied from it.)
- Resources: `PermitWorker`, `PermitEquipment`, `PermitChemical`, `PermitPpe`,
  `PermitDocument`.
- `PermitStatus`: `Draft → PendingApproval → Approved → Active → (OnHold ⇄ Active) →
  (Suspended) → Closed`, plus `Rejected`, `Expired`, `Cancelled`.
  Implemented as an explicit state machine on the aggregate — transitions are the only way
  to change status, and illegal transitions throw a domain exception. This is the single
  most important design decision of the project and where the DDD marks are.
- `PermitEvent`: immutable append-only audit trail (who, what, when, why).

---

## 6. Non-functional / assignment checklist

Mapped to `Final-Project-v10.pdf`:

| Requirement | How |
|---|---|
| Domain model (DDD) | Section 3–5 above; entities, value objects, aggregates, domain events |
| DB created from model | EF Core code-first + migrations |
| Layered architecture | `Domain` / `Application` (services) / `Infrastructure` (repositories, EF) / `Api` (controllers) |
| Repository + Service + Controller | Yes, all three, explicit interfaces |
| REST API + Angular | ASP.NET Core Web API + Angular SPA |
| Authentication / Authorization | ASP.NET Core Identity + JWT; Angular interceptor + route guards + role-based UI |
| Swagger | Swashbuckle with bearer auth configured |
| Unit tests | xUnit, behaviour-focused, `Subject_Action_When_Condition` naming |
| Integration tests | Postman collection committed to the repo |
| DB in Docker | `docker-compose.yml` with SQL Server 2022 |
| README build/deploy | Written last, verified by following it from a clean clone |

---

## 7. Proposed repo layout

```
PermitToWork/
├─ PermitToWork.sln
├─ docker-compose.yml
├─ README.md
├─ CLAUDE.md
├─ docs/
├─ src/
│  ├─ PermitToWork.Domain/
│  ├─ PermitToWork.Application/
│  ├─ PermitToWork.Infrastructure/
│  └─ PermitToWork.Api/
├─ tests/
│  └─ PermitToWork.Tests/
├─ client/                 # Angular workspace
└─ postman/
```

The existing `PermitToWork/PermitToWork.csproj` console stub gets deleted — it's the
default Rider scaffold and nothing depends on it.

---

## 8. Open questions

None blocking. All of Q1–Q7 answered as of 2026-08-09.

Deferred until Phase 2 design:

- Which permit types the site actually issues (Hot Work, Confined Space, Electrical,
  Working at Height, Excavation, Lifting…) and which certifications each one requires.
- Whether approval needs one signature or several.

## 9. Tooling constraint

The assistant's sandbox has **no .NET SDK** and the NuGet/npm registries are blocked, so
it cannot run `dotnet new`, `dotnet build`, `dotnet test`, `ng serve` or `npm install`.
All project files are written by hand; **Vagelis runs build/restore/test in Rider** and
reports failures back. This is workable but means compile errors surface a round-trip
later than usual — so the code must be written carefully rather than iterated into
correctness.
