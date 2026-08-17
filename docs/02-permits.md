# Phase 2 — Permit To Work

Agreed 2026-08-17. This is the module the application exists for.

---

## 1. Decisions

| # | Decision |
|---|---|
| P1 | **Certifications are a hard block.** A permit refuses a worker who does not hold every certification its type requires, valid across the whole permit window. Enforced in the aggregate, so no endpoint can bypass it. |
| P2 | **A facility approval panel.** Each facility has a standing list of approvers, configured by an administrator, with some marked *decisive*. A permit activates when every approver has signed, or immediately when one decisive approver signs. |
| P2a | **"Issuer" is derived, not stored.** It is whoever's approval completed the permit — a fact the approvals already record. A stored `IssuerId` would be a second copy that goes stale. |
| P2b | **The receiver** is the person accountable for the work on site. Named on the permit; performs no approval step. |
| P2c | **Only the creator closes.** They are the one who knows the job is done, and it makes closure attributable. |
| P2d | **Rejection is terminal.** A refused permit is rewritten from scratch, so what was refused stays on the record as refused. |
| P3 | Resources in v1: **workers, equipment, documents**. |
| P4 | Number format `HW-2026-0001` — permit type code, year, per-type sequence, from the same atomic counter table as badge numbers and team codes. |
| P5 | Documents are built **last**, because file upload is a feature in its own right. Everything else works without them. |

---

## 2. Lifecycle

```
  Draft ──submit──► Pending ──all sign, or one decisive signs──► Active ──close──► Closed
                       │
                       └──any approver refuses──► Rejected
```

- **submit** — the creator sends a complete draft to the facility's panel. The panel is
  copied onto the permit at that moment.
- **approve** — an approver signs. The permit activates on the last outstanding signature,
  or immediately if that approver is decisive.
- **reject** — any approver refuses, with a reason. Terminal.
- **close** — the creator declares the work done.

Every transition is a method on the aggregate. There is no status setter. Illegal moves
throw a `DomainException`, which the API returns as 422.

### Rules worth naming

1. **Nobody approves their own paperwork.** If the creator sits on the facility panel,
   their seat is skipped for their own permits. If that leaves nobody, submission is
   refused with an explanation rather than a permit that can never be approved.
2. **Only the creator may close.**
3. **A draft cannot be submitted with no workers.** An authorisation for nobody is not an
   authorisation.
4. **Content is frozen once submitted.** Description, dates, location and receiver are
   editable in Draft only — otherwise the thing approved is not the thing performed.
5. **Crew is frozen while Pending, and reopens once Active.** People are approving a
   specific crew, so swapping it underneath them would make their signature meaningless.
   Once work is live, crews genuinely do change — shifts turn over, people go sick.

---

## 3. The certification rule

The reason Phase 1's certification model exists.

Each `PermitType` lists the certifications it requires — Hot Work requires a Hot Work
certificate, Confined Space Entry requires its own. When a permit is created, that list is
**copied onto the permit** along with the certification names.

That copy is deliberate, and it is not the same thing as storing a derived value. It is a
record of what the rules *were* when this permit was raised. If somebody adds a requirement
to Hot Work next March, permits issued last week do not retroactively become invalid — and
an investigator reading a two-year-old permit sees the policy that actually applied to it.

Adding a worker then checks, in the aggregate:

```
for each required certification:
    the employee must hold one valid on ValidFrom AND on ValidTo
```

Both ends, because a certificate expiring halfway through a three-day permit is exactly the
case a single-date check misses.

---

## 4. Model

```
Permit (aggregate root)
├─ PermitNumber          value object, HW-2026-0001
├─ PermitTypeId, CategoryId
├─ Project?              "Unit 3 Turnaround"
├─ WorkDescription
├─ LocationId            → Building → Facility, transitively
├─ Notes?
├─ Validity              DateTimeRange value object (start + end as one concept)
├─ Status                PermitStatus
├─ CreatedById / IssuerId / ReceiverId       employee ids
├─ RequiredCertifications[]   snapshot of policy at issue: (typeId, name)
├─ Workers[]             PermitWorker
├─ Equipment[]           PermitEquipment
├─ Documents[]           PermitDocument      (built last)
└─ Events[]              PermitEvent — append-only audit trail
```

`PermitEvent` is written by the aggregate itself on every transition, so the audit trail
cannot be forgotten by a caller. Who did what, when, and why for the ones that carry a
reason.

### Lookups

| Table | Contents |
|---|---|
| `PermitType` | HW Hot Work, CS Confined Space, WH Working at Height, EL Electrical, CW Cold Work |
| `PermitTypeCertification` | which certifications each type requires |
| `Category` | Maintenance, Inspection, Construction, Cleaning |

---

## 5. Who may do what

| Action | Roles |
|---|---|
| Create, edit draft, submit, cancel | Administrator, Supervisor, Responsible |
| Approve, reject | Administrator, SafetyOfficer — the *issuer* |
| Accept | **The named receiver only**, whatever their role |
| Suspend, resume, close | Administrator, SafetyOfficer |
| Read | Anyone, within their company scope |

Company scoping works as it does for teams: a contractor sees a permit if one of their own
people is its creator, issuer, receiver or a worker on it.

---

## 6. Build order

1. Domain — value objects, aggregate, state machine, certification rule ← **this step**
2. EF configuration and migration
3. Repository, service, controllers
4. Unit tests for the state machine and the certification rule
5. Angular — permit list, permit form, the lifecycle action buttons
6. Documents — upload, download, size and type limits
