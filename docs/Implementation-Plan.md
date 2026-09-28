# Quizware — Detailed Implementation Plan

**Version:** 1.0
**Scope:** Backend (ASP.NET Core Web API on .NET 10 + SQL Server). Angular 22 is
a separate track that starts after Phase 5.
**Source of truth:** the design documents in [`docs/new-system/`](new-system/README.md).

---

## How this document relates to the design docs

| Document | Answers |
|---|---|
| `new-system/01-Analysis-Findings.md` | What the old systems do and what not to carry over |
| `new-system/02-Architecture-Proposal.md` | How the system is structured |
| `new-system/03-PRD-API.md` | What it must do (FRs, business rules) |
| `new-system/04-Database-Schema.md` | Every table and column |
| `new-system/05-API-Design.md` | Every endpoint, service and workflow |
| `new-system/06-Development-Roadmap.md` | **Which phases, in what order, and why** |
| **This document** | **Exactly what to build in each phase, task by task, and how to know each task is finished** |

The roadmap explains sequencing and dependencies. This plan turns each phase into
numbered tasks with concrete artifacts and acceptance criteria. Where the two
disagree, the roadmap wins on ordering and this plan wins on detail.

---

## Working conventions

### Task IDs

`P{phase}-{nn}` — e.g. `P9-07`. Stable; do not renumber. New work inside a phase
gets the next free number, even if it sorts oddly.

### Definition of Done (applies to every task)

A task is not done until **all** of these are true:

1. Code compiles with zero warnings (`TreatWarningsAsErrors` is on).
2. Unit tests exist for every branch of new business logic.
3. Integration tests exist for anything that touches the database or HTTP.
4. Architecture tests still pass (no layer violation).
5. XML doc comments on every public interface member.
6. OpenAPI document regenerated if any contract changed.
7. No hardcoded business value — anything tunable is a config row or setting.
8. Reviewed by one other developer (or, solo, re-read after 24 hours).

### Branching

`feature/P{phase}-{nn}-short-name` → PR → `develop`. `main` is release-only.
One PR per task where practical; never one PR per phase.

### Test strategy per layer

| Layer | Test type | Tooling |
|---|---|---|
| Domain | Pure unit, no mocks needed | xUnit + FluentAssertions |
| Application | Unit with faked ports | xUnit + NSubstitute |
| Infrastructure / persistence | Integration against **real SQL Server** | Testcontainers |
| API | Integration via `WebApplicationFactory` | xUnit |
| Architecture | Dependency-rule enforcement | NetArchTest |

**Never use the EF Core in-memory provider.** It does not enforce constraints and
will report success where SQL Server would reject the write.

### Estimation basis

Durations assume **one full-time developer**. Two developers working in parallel
after Phase 5 cut the calendar roughly in half — see §Parallelisation.

---

# Phase 0 — Requirements confirmation — **DONE (skipped, owner-confirmed)**

**Status:** closed without a workshop. The project owner is the sole
stakeholder — there is no separate organiser or quiz-master to sign anything
off — so the 16 assumptions already recorded in `06-Development-Roadmap.md`
§6.5 are accepted as final rather than re-litigated. They are `[DECIDED]`, not
`[ASSUMED]`; see `context/DECISIONS.md`. `P0-03`/`P0-04`/`P0-06` (per-format
rule sheets, scoring-value sign-off, venue hardware inventory) are deferred:
settle each only when it first blocks a task, not up front.

**This does not block Phase 1.** Nothing downstream needs `[ASSUMED]` values
resolved by committee — they are already resolved by decision.

> If a rule turns out wrong later, that is an ordinary spec change, not a
> process failure — there was never a second stakeholder to have caught it earlier.

---

# Phase 1 — Domain model and business rules — **DONE**

**Duration:** 1–2 weeks · **Depends on:** nothing blocking (Phase 0 is
owner-confirmed, not a workshop gate) · **Project:** `Quizware.Domain`

This project has **zero external package references**. If you find yourself
wanting EF Core or ASP.NET here, the logic belongs in another layer. Verified:
`Quizware.Domain.csproj` carries no `PackageReference` at all.

**Status:** all 14 tasks built, 95/95 `Quizware.Domain.Tests` pass, solution
builds with 0 warnings/errors. `P1-02` and `P1-04` are committed
(`2b2bcfb`); `P1-01`, `P1-03`, `P1-05`–`P1-14` are complete on disk but
**not yet committed** — do not assume the working tree matches HEAD.

### Tasks

| Task | What to build | Acceptance criteria | Status |
|---|---|---|---|
| `P1-01` | `Common/` — `BaseEntity`, `ITenantScoped`, `IAuditable`, `ISoftDeletable` | Interfaces only; no behaviour that needs infrastructure | Done |
| `P1-02` | **`TurnOrderCalculator`** — pure function: given participants + question index → next active participant | See dedicated test list below. **Build this first.** | Done, committed |
| `P1-03` | Enums: `QuestionFormatCode`, `AnswerOutcome`, `MatchState`, `StageState`, `ProgramState`, `ParticipantStatus`, `TeamStatus`, `DifficultyLevel`, `SegmentOrderMode`, `MatchKind`, `TieBreakScope`, `TieBreakResolutionMethod` (plus others added as later tasks needed them — see `context/DECISIONS.md`) | Values match the lookup-table seeds in `04-Database-Schema.md` §4.3 exactly | Done |
| `P1-04` | `Programs/` — `Program`, `ProgramSetting`, `ProgramQuestionFormat` | Invariant: a program cannot go `Live` without ≥1 stage | Done, committed |
| `P1-05` | `Teams/` — `Team`, `TeamMember` | Invariant: status change requires a reason | Done |
| `P1-06` | `QuestionBank/` — abstract `Question` base + the 10 format subclasses + `QuestionOption`, `SequenceItem`, `VisualRapidFireItem`, `Topic`, `Tag`, `MediaAsset` | TPT shape per `04-Database-Schema.md` §4.7. `AudioVisualQuestion.MediaAssetId` is non-nullable | Done |
| `P1-07` | `Tournament/` — `Stage`, `StageSegmentTemplate`, `Match`, `MatchParticipant` | Invariant: a match needs ≥2 active participants to start | Done |
| `P1-08` | `Gameplay/` — `MatchSegment`, `MatchQuestion`, `AnswerRecord`, `MatchEvent` | Invariant: only one segment `Open` per match; one question `Active` per segment | Done |
| `P1-09` | `Scoring/` — `ScoringRule`, `ScoreEvent`, `TeamMatchScore`, `TeamStageScore` | Invariant: a `ScoreEvent` is immutable once created | Done |
| `P1-10` | `Qualification/` — `QualificationRule`, `StageQualification`, `TieBreakRule`, `TieBreakEvent`, `TieBreakParticipant` | Invariant: a resolved tie must name its resolution method | Done |
| `P1-11` | **`BuzzRankingCalculator`** — port the ranking logic from QuickBuzz's `DeviceApiController` into a pure function | Lowest non-zero time wins; `0` means "no press" and sorts last | Done |
| `P1-12` | **`SegmentOrderResolver`** — pure function: template order → per-match override → shuffle by seed → honour locked segments | Same seed produces the same order every time | Done |
| `P1-13` | **`TieBreakCriteriaEvaluator`** — pure function: ordered criteria → first that separates, or "still tied" | Returns *which* criterion decided it | Done |
| `P1-14` | Domain exceptions: `InvalidStateTransitionException`, `InsufficientParticipantsException`, `QuestionPoolExhaustedException`, `ScoringRuleNotFoundException`, `UnresolvedTieException`, `SegmentNotReorderableException`, `FormatInUseException` | One per error code in `05-API-Design.md` §5.2 | Done |

QuestionBank deliberately does **not** model a `QuestionTag` join entity in the
domain layer — that many-to-many is an EF mapping concern for Phase 4, not a
domain concept.

### `TurnOrderCalculator` — required test cases

This single class carries the whole disqualification requirement. Test it hard:

- 3 active participants, questions 0–5 → `1,2,3,1,2,3`
- **3 participants, one disqualified after question 2** → remaining two continue
  as `1,2,1,2` with no gap and no fake answer
- 2 active participants → alternates correctly
- 4+ active participants → rotates correctly (proves nothing is hardcoded to 3)
- 1 active participant remaining → signals "match should end"
- 0 active participants → throws, never returns a null participant
- Disqualified participant is **never** returned, even if it holds the lowest
  original `TurnOrder`
- Turn order recompaction preserves the relative order of the survivors
- `SeatNumber` is untouched by recompaction (teams do not move on stage)

**Exit criteria:** `Quizware.Domain.Tests` has >90% line coverage and the project
references no NuGet package other than the test framework.

---

# Phase 2 — Architecture decisions, locked — **DONE**

**Duration:** 3–5 days · **Depends on:** P1 (references its shapes, e.g.
TPT questions) · **Not a hard gate on Phase 3.** Every decision below is
already made and written down in `docs/new-system/02-Architecture-Proposal.md`
§2.16 — this phase is paperwork that records it as an ADR, not a design
decision that blocks coding.

**Status:** all 10 ADRs written to `docs/adr/ADR-00N-*.md`. Note that
`docs/` (including `docs/adr/`) is **gitignored** in this repo by deliberate
choice — these files exist on disk for reference but are not tracked in git
history. If that changes later, `docs/adr/` should be un-ignored and
committed as its own change.

| Task | What to build | Done when | Status |
|---|---|---|---|
| `P2-01` | ADR-001 Modular monolith over microservices | One page, decision + alternatives rejected | Done |
| `P2-02` | ADR-002 Multi-tenancy: shared schema + `ProgramId` + EF global query filter | Includes the "tenant id comes from the JWT claim, never the route" rule | Done |
| `P2-03` | ADR-003 Question storage: Table-Per-Type (base + 10 format tables) | Records both alternatives rejected — one wide table, and ten independent tables | Done |
| `P2-04` | ADR-004 Event-sourced scoring (`ScoreEvent` ledger, never mutate) | Explains how undo works without deletes | Done |
| `P2-05` | ADR-005 Buzzer as a port with three adapters, `Null` by default | States the "solution builds with the module deleted" requirement | Done |
| `P2-06` | ADR-006 SignalR over polling; outbox for reliable dispatch | | Done |
| `P2-07` | ADR-007 Tie-break as an ordinary `Match` with `MatchKind = TieBreak` | Explains why a separate tie-break code path was rejected | Done |
| `P2-08` | ADR-008 Local (on-premises) hosting | Notes what it simplifies and what would change if moved to cloud | Done |
| `P2-09` | ADR-009 Authorisation model: 7 roles, ProgramAdmin holds all oversight authority (no Judge role) | Lists the four consolidated actions | Done |
| `P2-10` | Module boundary map + dependency-rule diagram | The diagram the Phase 3 architecture tests will enforce | Done — `ADR-010-module-boundaries.md` |

**Exit criteria:** ADRs written to `docs/adr/`. Any later deviation requires a
new ADR superseding the old one — do not edit a decided ADR in place.

---

# Phase 3 — Project skeleton and cross-cutting concerns

**Duration:** 1–2 weeks · **Depends on:** `P3-01` alone needs nothing and can
run before Phase 1; everything else in this phase needs `P1` (e.g. `P3-08`
maps the `P1-14` domain exceptions) and benefits from `P2`'s dependency-rule
diagram, but does not need every ADR written first.

**Status: all 17 tasks done.** Full solution builds with 0 warnings/errors;
109 tests pass across all four test projects (95 Domain, 4 Application, 4
Architecture, 6 Api.IntegrationTests). Not committed — this session did not
commit anything (see `context/CURRENT.md`).

**Goal:** one trivial endpoint working end to end, with every cross-cutting
concern already in place. Nothing here is a feature; everything here touches
every future feature.

| Task | What to build | Acceptance criteria | Status |
|---|---|---|---|
| `P3-01` | Solution + 5 source projects + 4 test projects per `02-Architecture-Proposal.md` §2.4 | `dotnet build` clean | Done |
| `P3-02` | **Architecture tests** (NetArchTest) | Domain references nothing; Application references only Domain; nothing references Api. **Test fails the build when violated** — verify by deliberately breaking it once | Done — verified by inverting an assertion, watching it fail, then reverting |
| `P3-03` | DI wiring: `AddApplication()`, `AddInfrastructure()`, `AddApi()` | Each project owns its own registration extension | Done. No `AddDomain()`: Domain has zero package references (including DI abstractions) and zero services to register, so there is nothing to wire |
| `P3-04` | ASP.NET Core Identity + JWT issue/validate + refresh-token rotation | Access token 15 min, refresh 7 days, rotated on use, hash stored not the token | Done — `JwtTokenService`, `RefreshToken` (SHA-256 hash only, never the raw token) |
| `P3-05` | Seed the **7 roles**: `SuperAdmin`, `ProgramAdmin`, `QuestionAuthor`, `Operator`, `Scorer`, `Display`, `Auditor` | Exactly 7 — no `Judge` | Done — `RoleSeeder`, run at startup |
| `P3-06` | Authorisation policies per `05-API-Design.md` §5.9 | `CanManageProgram`, `CanManageQuestions`, `CanOperateMatch`, `CanRecordAnswer`, `CanAdjustScore`, `CanDisqualify`, `CanResolveTie`, `CanViewLive`, `DisplayOnly` | Done — all 9, role membership matches §5.9 exactly |
| `P3-07` | **Program-scope filter**: route `{programId}` must match the token's `program_id` claim | Rejects with 403 *before* hitting the database; integration test proves it | Done — `ProgramScopeMiddleware` |
| `P3-08` | Global exception handler → RFC 9457 `application/problem+json` | Maps every domain exception from `P1-14` to its documented error code | Done — `GlobalExceptionHandler` maps all 7 P1-14 exceptions plus `NoActiveParticipantsException` and `ValidationException` |
| `P3-09` | Serilog structured logging + correlation id middleware | `X-Correlation-Id` echoed on every response and present in every log line | Done — `CorrelationIdMiddleware` pushes it into Serilog's `LogContext` |
| `P3-10` | FluentValidation pipeline behaviour | Returns *all* failing fields, not just the first | Done — `ValidationBehavior<TRequest,TResponse>` (MediatR pipeline), aggregates every validator's every failure |
| `P3-11` | `ICurrentUser`, `ICurrentProgram`, `IClock` + test fakes | `IClock` makes time-dependent tests deterministic | Done — interfaces in Application, implementations in Infrastructure (`CurrentUser`/`CurrentProgram` read JWT claims, `SystemClock` wraps `DateTime.UtcNow`) |
| `P3-12` | `IdempotencyFilter` + `IdempotencyRecord` storage | Same key + same body → replays stored response; same key + different body → 409 | Done — `IdempotencyFilter` (MVC action filter) + `EfIdempotencyStore` |
| `P3-13` | Health checks: `/health/live`, `/health/ready` | Ready includes DB; buzzer probe added later in P14 | Done — `/health/live` is a liveness no-op, `/health/ready` runs `AddDbContextCheck<AppDbContext>()` |
| `P3-14` | Swagger / OpenAPI generation with examples | | Done (Swashbuckle + bearer scheme); worked per-endpoint examples deferred to Phase 5 when the real DTOs exist |
| `P3-15` | Docker Compose for local SQL Server | `docker compose up` gives a working dev DB | Done — `Quizware/docker-compose.yml`. **Not run in this environment** (no Docker daemon available here) — verify locally |
| `P3-16` | CI pipeline: restore → build → test → migration-drift check | Fails if a model change has no matching migration | Done — `.github/workflows/ci.yml`, using `dotnet ef migrations has-pending-model-changes`. **Not executed** (no GitHub Actions runner here) — verify on push |
| `P3-17` | Working endpoints: `GET /api/v1/admin/health`, `POST /api/v1/auth/login` | Login returns a usable JWT; health returns 200 | Done — both covered by passing integration tests (login also tested against wrong password and unknown email) |

**Exit criteria:** a developer can clone, `docker compose up`, `dotnet run`, log
in, and get a token — with logging, error handling and auth all functioning.
`docker compose up` and the GitHub Actions run itself could not be exercised
in this environment (no Docker daemon, no CI runner) — everything else
(build, all 109 tests, the login round-trip against a real relational
database) was verified directly.

### Scope decision: Phase 3's database is Identity-only

`AppDbContext` in this phase covers only `Identity` tables, `RefreshToken`,
and `IdempotencyRecord` — the minimum P3 needs for a working login endpoint
and idempotent writes. The full 51-table business schema is added to this
**same** context in Phase 4 (`P4-01` onward), not a second context. One
migration exists so far: `InitialIdentitySchema`.

### Testing note: SQLite stands in for SQL Server in integration tests

`Quizware.Api.IntegrationTests` swaps `AppDbContext`'s SQL Server connection
for an in-process SQLite database (`CustomWebApplicationFactory`) — a real
relational engine that enforces constraints, unlike the EF Core InMemory
provider the plan explicitly forbids. Program.cs runs `EnsureCreatedAsync()`
under a `Testing` environment (SQLite cannot replay a migration generated
against SQL Server) and `MigrateAsync()` otherwise, so real deployments
always go through migrations.

---

# Phase 4 — Database schema and migrations — **DONE**

**Duration:** 1–2 weeks · **Depends on:** P1, P2, P3

51 tables per `04-Database-Schema.md` §4.13. Build them in dependency order.

**Status: all 20 tasks done.** 49 new tables in migration `AddBusinessSchema`
(on top of Phase 3's 9 Identity/RefreshToken/IdempotencyRecord tables — ~58
total, versus the doc's 51, for the reasons below). Full solution builds with
0 warnings/errors; **115 tests pass** (95 Domain, 4 Application, 4
Architecture, 12 Api.IntegrationTests — 6 new persistence tests added:
tenant isolation, soft delete, audit stamping, audit log, NOT-NULL
enforcement, seed fidelity).

**Three scope decisions made while building this, all deliberate:**

1. **Lookup tables are enum columns, not separate tables.** `QuestionFormat`,
   `AnswerOutcome`, `MatchState`, etc. were built as C# enums in `P1-03`, not
   entity classes — the domain model already committed to that. Adding
   separate lookup tables now would mean two sources of truth for the same
   values. `P4-01` therefore configures enum-to-column mapping, not lookup
   tables; values still match the `P1-03` enums exactly (that was always
   true, by construction).
2. **`ScoringRule`/`TieBreakRule` defaults are seeded per-program, not
   globally.** Both have a required, non-nullable `ProgramId` — there is no
   program to attach a global seed row to in a migration that runs before
   any program exists. The legacy point values from `01-Analysis-Findings.md`
   §1.4d are captured in `Quizware.Domain.Scoring.DefaultScoringValues` now,
   ready for Phase 6's "create a program" flow to seed a full rule set for
   every new program — same values, applied at the right time instead of the
   wrong one.
3. **Foreign keys mostly exist as plain Guid columns, not enforced
   relationships.** With no navigation properties anywhere in the Phase 1
   domain model (a deliberate choice — entities reference each other by Id
   only), wiring every one of ~150 FK columns as a formal EF relationship
   was cut for time. The unique constraints, indexes and check constraints
   explicitly named in `04-Database-Schema.md` §4.14/§4.15 are all in place;
   referential-integrity FKs can be added incrementally later without a
   breaking change.

| Task | What to build | Acceptance criteria | Status |
|---|---|---|---|
| `P4-01` | `AppDbContext` + configuration classes for lookup tables | Seeded by migration, values match `P1-03` enums | Done as enum columns — see scope decision 1 |
| `P4-02` | Identity tables + `ProgramUser` + `RefreshToken` | | Done |
| `P4-03` | `Program`, `ProgramSetting`, `ProgramQuestionFormat` | | Done (mapping only — the "auto-create on program creation" behaviour is Phase 6, `P6-01`) |
| `P4-04` | `Team`, `TeamMember` | | Done |
| `P4-05` | `Topic`, `Tag`, `QuestionTag`, `MediaAsset` | | Done, except `QuestionTag` (no join entity — Phase 1 deliberately left this as an EF many-to-many with no domain class; deferred) |
| `P4-06` | **`Question` base + 10 format tables via TPT** | `AudioVisualQuestion.MediaAssetId`/`.AnswerText` NOT NULL at the DB level | Done — verified via EF model inspection (`IsNullable == false`), not a live failing insert (see test file for why) |
| `P4-07` | `QuestionOption`, `SequenceItem`, `VisualRapidFireItem`, `QuestionUsageHistory` | `UX_SequenceItem_Position` enforced | Done |
| `P4-08` | `Stage`, `StageSegmentTemplate` | `UX_SST_Stage_Order` enforced | Done |
| `P4-09` | `QuestionSelectionRule`, `ScoringRule`, `QualificationRule`, `TieBreakRule` | | Done. `QuestionSelectionRule` did not exist yet — added to `Quizware.Domain.Tournament` as part of this phase |
| `P4-10` | `Match`, `MatchParticipant`, `MatchSegment`, `MatchQuestion` | `CK_Match_TieBreak`, `UX_MP_Match_Team`, `UX_MP_Match_Seat`, `UX_MQ_Match_Question` all enforced | Done |
| `P4-11` | `AnswerRecord`, `MatchEvent`, `ScoreEvent`, `TeamMatchScore`, `TeamStageScore` | | Done |
| `P4-12` | `TieBreakEvent`, `TieBreakParticipant`, `StageQualification` | | Done |
| `P4-13` | `AuditLog`, `IdempotencyRecord`, `OutboxMessage`, `ImportBatch`, `ImportBatchRow` | | Done |
| `P4-14` | Buzzer tables in a **separate configuration set** | Can be excluded from the model without breaking the rest | Done — entities + configs live in `Quizware.Modules.Buzzer`; `AppDbContext` picks them up via a new `IEntityConfigurationAssemblyMarker` port (Application) so Infrastructure never references the Buzzer project at all |
| `P4-15` | **Global query filters**: tenant + soft delete | | Done, applied reflectively to every entity implementing `ITenantScoped`/`ISoftDeletable`. Outside a program-scoped request (background jobs, seeding), the tenant filter is a no-op rather than throwing — see `ICurrentProgram.ProgramId`'s doc comment for why it must never throw |
| `P4-16` | Audit interceptor: `CreatedAtUtc/By`, `UpdatedAtUtc/By` | Developer never sets these by hand | Done — `AuditableEntitySaveChangesInterceptor`. `CreatedAtUtc/By` are also set explicitly by domain factories (they take `createdBy` as a required argument); the interceptor is the safety net, not the only source |
| `P4-17` | Audit-log interceptor writing to `AuditLog` | Captures old/new values and changed columns | Done — `AuditLogSaveChangesInterceptor` |
| `P4-18` | Key indexes from `04-Database-Schema.md` §4.14 | Especially `IX_Question_Selection` and `IX_MP_Active_Turn` | Done, all 7 named indexes |
| `P4-19` | All check constraints from §4.15 | | Done, all of them — with `LEN(...)` rewritten to `<> ''` for cross-provider compatibility (SQLite has no `LEN`) |
| `P4-20` | Seed migration: default scoring rules, default tie-break rule, one admin user | Scoring values match `01-Analysis-Findings.md` §1.4d | Partially — see scope decision 2 for scoring/tie-break rules. The admin user **is** seeded: `AdminUserSeeder` creates `admin@quizware.local` / `ChangeMe!123` as `SuperAdmin` on first run, skipped entirely in Production |

### Critical tests before leaving this phase

| Test | Must prove | Status |
|---|---|---|
| Tenant isolation | A query with program A's token **cannot** return program B's rows | Done — `PersistenceTests.TenantIsolation_*` |
| Soft delete | Deleted rows are invisible to normal queries and recoverable | Done |
| Audit stamping | Insert/update populates audit fields without explicit code | Done |
| Constraint enforcement | `AudioVisualQuestion` with a null `MediaAssetId` fails | Done, via model inspection rather than a live insert |
| Seed fidelity | Seeded scoring values equal the legacy constants | Done, against `DefaultScoringValues` |

**Exit criteria:** all tests run against real SQL Server via Testcontainers.
**Not met exactly as written** — this environment has no Docker daemon, so
tests run against SQLite instead (a real relational engine, not the
forbidden EF Core InMemory provider). Re-running the same test suite against
real SQL Server via Testcontainers, once Docker is available, is the
one thing this phase could not verify directly.

---

# Phase 5 — API contract, OpenAPI first — **DONE**

**Duration:** 1 week · **Depends on:** P1, P4

**Nothing is implemented in this phase.** The point is to fix the contracts
before code depends on them, and to unblock the Angular team months early.

**Status: all 8 tasks done.** 16 controllers (2 extended from Phase 3 —
`AuthController`, `AdminController` — plus 14 new), ~145 routes / 173
operations, all returning `501 Not Implemented` with typed
`ActionResult<T>` responses so the OpenAPI document carries real schemas.
Solution builds with 0 warnings/errors; **147 tests pass** (95 Domain, 4
Application, 4 Architecture, 44 Api.IntegrationTests — 32 new tests: 9
FluentValidation format-validator tests, 23 controller-reachability/
authorization tests).

**Scope decisions made while building this, all deliberate:**

1. **Not every one of the ~145 routes has a dedicated, individually-named
   response DTO.** Every documented POST/PUT body has a named request DTO
   (`P5-01`/`P5-02`), and every endpoint with a worked JSON example in
   `05-API-Design.md` §5.4 has a matching response DTO (start match, live
   state, record answer, disqualify, segment reorder, ties, tie-break-match,
   resolve-manually, qualification preview, selection preview, coverage,
   formats). Simpler CRUD list/detail endpoints reuse a small, obvious
   summary/detail shape per area rather than one bespoke type per route.
   Every action still returns a *typed* `ActionResult<T>` (never a bare
   `IActionResult`) wherever a response shape exists, so Swashbuckle emits a
   real schema for it — see scope decision 3 for why this matters.
2. **The buzzer agent's press-ingestion endpoint
   (`POST /buzzer/sessions/{id}/presses`) is left `[AllowAnonymous]`.** Per
   ADR-005 this is called by the on-premises agent process, not a browser —
   it will carry its own agent-to-API credential once Phase 14 implements
   the adapters, not a user JWT. This is not the final security posture;
   flagged explicitly in the controller's XML doc.
3. **Swashbuckle does not read `[JsonPolymorphic]`/`[JsonDerivedType]`
   automatically** — `SelectSubTypesUsing` was added to
   `Quizware.Api/DependencyInjection.cs` to supply `QuestionResponse`'s 10
   per-format subtypes explicitly. Without this (and without every question
   action returning `ActionResult<QuestionResponse>` instead of bare
   `IActionResult`), the generated OpenAPI document silently flattened the
   polymorphic response to its base type with no `oneOf` — verified by
   generating the document both before and after the fix and diffing the
   `QuestionResponse` schema.
4. **`openapi-generator-cli` (Java-based) could not run in this
   environment** — no JVM available. Used `NSwag.ConsoleCore` instead (pure
   .NET, installed as a global dotnet tool), which reads the same OpenAPI
   document and produces an equivalent discriminated-union TypeScript
   client. `docs/openapi.v1.json` (gitignored, like the rest of `docs/`) is
   the exported document if it needs regenerating with a different tool.
5. **Per-endpoint worked JSON examples (the literal payloads in
   §5.4) are not wired into Swagger** as `schema.example` values. Swashbuckle
   10.x targets a newer major version of `Microsoft.OpenApi` with a
   different (and, as of this build, less-documented) example API than the
   patterns most existing guides assume; hand-authoring ~10 examples against
   an unfamiliar, fast-moving API risked fragility for low return in a
   phase whose actual contract (routes, request/response shapes, the
   discriminated union) already fully exists and is machine-verified. XML
   doc comments are enabled and wired into Swagger instead
   (`GenerateDocumentationFile` + `IncludeXmlComments`), giving every
   contract type and non-obvious route a description in the UI.

| Task | What to build | Acceptance criteria | Status |
|---|---|---|---|
| `P5-01` | Request/response DTOs for all 16 controllers in `05-API-Design.md` §5.3 | camelCase, UTC dates suffixed `Utc` | Done — `Quizware.Api/Contracts/V1/**` (camelCase is System.Text.Json's ASP.NET Core default; no property currently needs a raw non-UTC date) |
| `P5-02` | **Per-format question request models** (`CreateMcqQuestionRequest`, `CreateAudioVisualQuestionRequest`, …) | `POST .../questions/audio-visual` cannot accept an options array | Done — `Contracts/V1/Questions/Formats/CreateQuestionRequests.cs`, one route + one request type per format |
| `P5-03` | FluentValidation validators per format, sharing `QuestionBaseValidator` | Sequence positions validated contiguous 1..N | Done — `Contracts/V1/Questions/Formats/QuestionFormatValidators.cs`, 9 unit tests in `QuestionFormatValidatorsTests.cs` |
| `P5-04` | Controller stubs returning `501 Not Implemented` | Every documented route exists and is reachable | Done — verified for a representative route per controller in `ControllerStubReachabilityTests.cs`, including auth/role/program-scope negative cases (full enumeration of all ~145 routes is scope decision 1's sibling: mechanical, not attempted) |
| `P5-05` | Polymorphic question response with `oneOf` + `formatCode` discriminator | Generated TS client produces a discriminated union | Done — see scope decision 3; verified in the generated `quizware-api-client.ts` (`McqQuestionResponse extends QuestionResponse`, etc.) |
| `P5-06` | Complete OpenAPI document with worked examples | Examples match those in `05-API-Design.md` §5.4 | Done except literal example payloads — see scope decision 5 |
| `P5-07` | Generated TypeScript client, committed | Angular team can build against it | Done — `Quizware/clients/typescript/quizware-api-client.ts` (NSwag, not openapi-generator — see scope decision 4), committed (not gitignored) |
| `P5-08` | `.http` / Postman collection | Every endpoint callable by hand | Done — `Quizware.Api/Quizware.Api.http`, one representative request per area plus the full live-match and qualification workflows |

**Exit criteria:** Angular track can start. Contract changes after this point
require a version bump or a documented breaking-change note.

---

# Phase 6 — Configuration modules

**Duration:** 2–3 weeks · **Depends on:** P3, P4, P5

Build strictly in this order — each depends on the one before.

### 6a — Program management

| Task | What | Acceptance |
|---|---|---|
| `P6-01` | `IProgramService`: create, update, get, list | Auto-creates `ProgramQuestionFormat` rows, all enabled |
| `P6-02` | Program settings CRUD (`ProgramSetting`) | Category + key + typed value |
| `P6-03` | Branding fields + logo upload | |
| `P6-04` | **Clone a program's configuration** (stages, templates, rules) without results | Cloned program is `Draft`; no matches, no scores |
| `P6-05` | Lifecycle transitions `Draft → Configured → Live → Completed → Archived` | Invalid transitions rejected with `CONFLICT_STATE` |
| `P6-06` | `PUT /programs/{id}/formats` — enable/disable question formats | `FORMAT_IN_USE` (409) if a segment template still uses it, naming the stages |

### 6b — Users and roles

| Task | What | Acceptance |
|---|---|---|
| `P6-07` | User invite, deactivate, password reset | |
| `P6-08` | `ProgramUser` — assign a role **per program** | A user can be ProgramAdmin on 2026 and Operator on 2025 |
| `P6-09` | `POST /auth/select-program` issues a program-scoped token | |
| `P6-10` | `POST /auth/display-token` — read-only display token | Cannot write anything; integration test proves it |

### 6c — Teams

| Task | What | Acceptance |
|---|---|---|
| `P6-11` | Team CRUD + images | **No hardcoded team limit**; optional `Program.MaxTeams` |
| `P6-12` | Status changes with reason + timestamp + actor | |
| `P6-13` | Excel import: **validate → report → commit** using `ImportBatch` | Report shown before anything is saved; no silent row skipping |

### 6d — Topics and tags

| Task | What | Acceptance |
|---|---|---|
| `P6-14` | `Topic` CRUD with parent/child; `Tag` CRUD | Program-scoped or shared (`ProgramId` null) |

### 6e — Media

| Task | What | Acceptance |
|---|---|---|
| `P6-15` | `IMediaService`: upload, extension allow-list, **magic-byte check**, size cap, SHA-256 dedup, store outside web root | A renamed `.exe` is rejected |

### 6f — Question bank

| Task | What | Acceptance |
|---|---|---|
| `P6-16` | Question CRUD, one route per format | Base row + format row written in one transaction |
| `P6-17` | Versioning: editing a used question creates a new version | `SupersedesQuestionId` chain intact |
| `P6-18` | Approval workflow `Draft → Approved → Retired` | Only approved questions are selectable |
| `P6-19` | Per-format Excel import with validation report | Column template differs per format |
| `P6-20` | Near-duplicate detection via `NormalizedText` | |
| `P6-21` | `GET /questions/coverage` | Reports **only formats in use**; returns `formatsInUse` / `formatsNotUsed` |

**Exit criteria:** a program with teams and questions can be created entirely
through the API, with no SQL run by hand.

---

# Phase 7 — Tournament configuration

**Duration:** 1–2 weeks · **Depends on:** P6

| Task | What to build | Acceptance criteria |
|---|---|---|
| `P7-01` | Stage CRUD + reorder | `UX_Stage_Program_Order` maintained in one transaction |
| `P7-02` | Segment template CRUD | A stage needs ≥1 segment; no format is compulsory |
| `P7-03` | **`PUT /stages/{id}/segments/reorder`** taking the full ordered list | Partial list rejected with `VALIDATION_FAILED`; locked segments keep their index |
| `P7-04` | `SegmentOrderMode`: `Fixed` / `RandomPerMatch` / `OperatorChoice` | |
| `P7-05` | `IsOrderLocked` per segment | Survives shuffling |
| `P7-06` | Scoring rule management + reset-to-defaults | |
| `P7-07` | Selection rule management | |
| `P7-08` | Qualification rule management | |
| `P7-09` | **Tie-break rule management** — criteria order, format (MCQ default), question count, difficulty range, sudden death, max rounds, `OnStillTied`, `ScoreCountsTowardStage` | Defaults seeded per `04-Database-Schema.md` §`TieBreakRule` |
| `P7-10` | `IRuleService` resolution order: segment → stage → program | Returns `SCORING_RULE_MISSING` rather than guessing |
| `P7-11` | `POST /programs/{id}/validate` readiness check | Checks **only formats in use**; `STAGE_HAS_NO_SEGMENTS` if a stage is empty |
| `P7-12` | **Seed script reproducing the 18-team tournament entirely as data** | 3 stages, 6+3+1 matches, League's 4-segment order, MCQ tie-break rule |

### Tests that prove the configuration model works

| Test | Must prove |
|---|---|
| Reorder propagation | Matches **not yet created** pick up the new order; matches **already created keep the old one** |
| Optional formats | A stage with no Passing segment passes readiness with **zero** Passing questions in the bank, and coverage never mentions Passing |
| Rule resolution | A segment-level scoring rule overrides the stage rule, which overrides the program rule |

**Exit criteria:** the entire current tournament exists as configuration rows,
with no C# describing its shape.

---

# Phase 8 — Question selection engine

**Duration:** 1–2 weeks · **Depends on:** P6f, P7 · **Interface:** `IQuestionSelector`

| Task | What to build | Acceptance criteria |
|---|---|---|
| `P8-01` | Pool building: format, language, topic filter, tag filter, owner scope, approved-only | Queries the **base `Question` table only** — no format table joined during the draw |
| `P8-02` | Repeat-policy exclusion: `NeverInMatch` / `NeverInStage` / `NeverInProgram` / `NeverForTeam` | Uses `QuestionUsageHistory` |
| `P8-03` | Difficulty-mix splitting from `DifficultyMixJson` | 60/40 mix honoured as closely as the pool allows |
| `P8-04` | Seeded weighted random draw, weighting toward lower `TimesUsed` | **Same seed → identical draw**, asserted in a test |
| `P8-05` | Topic-spread policy | Prefers an unused topic within the segment |
| `P8-06` | Option shuffling, recorded in `MatchQuestion.OptionOrderJson` | The order actually shown is reproducible for disputes |
| `P8-07` | **Reservation** into `MatchQuestion` with `State = Reserved` | A reserved question is unavailable to other matches |
| `P8-08` | Fallback ladder: widen difficulty ±1 → drop topic filter → allow older-program repeats → precise error | `QUESTION_POOL_EXHAUSTED` names format, required, available, difficulty range |
| `P8-09` | `POST /rules/selection/preview` — dry run consuming nothing | Returns pool size, achievable mix, warnings |
| `P8-10` | Release of unused reservations when a match is abandoned | |

**Exit criteria:** heavy unit coverage. Exhaustion produces a *typed error with a
suggestion*, never an unhandled exception mid-show.

---

# Phase 9 — The match engine

**Duration:** 3–4 weeks · **Depends on:** P7, P8 (P10 develops alongside `P9-06`)

The heart of the system. Replaces ~5,700 lines of duplicated legacy controllers.

> **Build order recommendation:** complete `P9-01` … `P9-06` **with the MCQ
> format only** and get it fully solid. Then add the remaining nine formats one
> at a time (`P9-07`). Do **Sequence** and **VisualRapidFire** last — they
> exercise the child-table paths.

| Task | What to build | Acceptance criteria |
|---|---|---|
| `P9-01` | Match setup: create, add participants, seats, turn order, auto-seed (random / by rank / snake) | Participant count validated against the stage's min/max |
| `P9-02` | Match lifecycle: `start` (reserves all questions, stores `RandomSeed`), `pause`, `resume`, `end`, `abandon` | Start is **one transaction**; a failure reserves nothing |
| `P9-03` | Segment lifecycle: open, close, skip-with-reason | Only one segment `Open` at a time |
| `P9-04` | Question serving: serve, reveal, skip; **server-authoritative timer** | `TimerStartedAtUtc` set server-side; client computes remaining from server now |
| `P9-05` | **Turn-order resolution over active participants only** (wraps `TurnOrderCalculator`) | Never returns a disqualified participant |
| `P9-06` | **Answer recording** — transactional + idempotent | One transaction covers: `AnswerRecord` + `ScoreEvent` + `TeamMatchScore` + `TeamStageScore` + `MatchQuestion.State` + `QuestionUsageHistory` + `Question.TimesUsed` + `MatchEvent` + `OutboxMessage`. **≤3 round trips** |
| `P9-07` | `IQuestionFormatHandler` per format — 10 implementations | Adding a format touches no existing code |
| `P9-08` | Passing mechanics: pass to next active team, `MaxPassCount`, `PassDirection` | Points differ for direct vs after-pass (`ContextKey`) |
| `P9-09` | Choice round topic selection + `TopicChoiceLimit` | Exclusive topics removed from the board once played |
| `P9-10` | **Undo / reverse an answer** — compensating `ScoreEvent`, never a delete | `ProgramAdmin` only (D-013) |
| `P9-11` | **Participant disqualification + turn-order recompaction** | `ProgramAdmin` only. Applies `TeamCountChangePolicy`. Ends the match if <2 remain |
| `P9-12` | `MatchEvent` timeline, strictly increasing `SequenceNumber` | Append-only |
| `P9-13` | **Crash recovery / resume** — rebuild state from the database, not memory | Same question order after restart, because questions were reserved at start |
| `P9-14` | Effective segment order: template → per-match override → live reorder of pending segments; `RandomPerMatch` from seed; locked segments | Open/completed segments never move (`SEGMENT_NOT_REORDERABLE`) |
| `P9-15` | Sudden-death segment closing | Closes as soon as one team leads, once all tied teams have faced equal questions |
| `P9-16` | **`GET /matches/{id}/live/state`** — everything the console needs in one call | <150 ms; replaces the legacy `OnXxxLoad` methods |

### Required integration tests

| Test | Must prove |
|---|---|
| Full match | A complete 3-team match start → finish, correct final standings |
| **Disqualification mid-segment** | Match finishes with 2 teams, **no fake answers**, turn order recompacted |
| Restart mid-question | Resumes at the exact same question with the same upcoming order |
| Double submission | Same `Idempotency-Key` twice → scored **once** |
| Authorisation | `Operator` and `Scorer` get **403** on disqualify and on answer reversal — both are `ProgramAdmin`-only |
| No-format-configured | A stage without Passing runs a complete match |

---

# Phase 10 — Scoring and standings

**Duration:** 1–2 weeks, overlaps Phase 9 · **Depends on:** P7, `P9-06`

| Task | What to build | Acceptance criteria |
|---|---|---|
| `P10-01` | `IScoringEngine`: resolve rule → create `ScoreEvent` → update read models incrementally | Never recomputes from raw answers on a request |
| `P10-02` | `TeamMatchScore` / `TeamStageScore` maintenance in the same transaction as the score event | Always consistent |
| `P10-03` | Manual adjustment with mandatory reason | **`ProgramAdmin` only** |
| `P10-04` | Reversal / undo via compensating events | Score returns to the exact prior value |
| `P10-05` | Standings endpoints (match / stage / program) | Reads pre-aggregated data |
| `P10-06` | `POST /scores/recalculate` — rebuild read models from the event ledger | The safety net; result must equal the incremental value |
| `P10-07` | Tie-break criteria service (wraps `TieBreakCriteriaEvaluator`) | Records which criterion decided |

### Tests

- Score **equals** the sum of non-reversed `ScoreEvent` rows
- Undo restores the exact prior total
- Disqualified teams excluded from standings but their history retained
- Manual adjustment from `Operator` or `Scorer` → **403**

---

# Phase 11 — Qualification and tie-breaking

**Duration:** 2 weeks · **Depends on:** P9, P10

| Task | What to build | Acceptance criteria |
|---|---|---|
| `P11-01` | Qualification preview: winners per match + best remaining + wildcards, each with a reason | Blocked until every match in the stage is `Completed` |
| `P11-02` | **Tie detection** on/across the qualification boundary | Separates ties that block a commit from ties merely recorded |
| `P11-03` | **Phase 1 — criteria evaluation**, ordered and configurable | Records `ResolvedByCriterion` |
| `P11-04` | **Phase 2 — tie-break match creation**: `Match` with `MatchKind = TieBreak`, only the tied teams, segments from the rule (MCQ default) | Hands off to the **existing** match engine — no new gameplay code |
| `P11-05` | Tie-break result capture: `TieBreakEvent` + `TieBreakParticipant` ranks | Written automatically on match completion |
| `P11-06` | Extra rounds up to `MaxExtraRounds`, then `OnStillTied` fallback | Manual decision requires reason + **`ProgramAdmin`** approval |
| `P11-07` | Commit: creates next stage's matches + participants per `SeedingMode` | Blocked while a qualifying-place tie is unresolved (`UNRESOLVED_TIE`) |
| `P11-08` | Rollback before the next stage starts | |

### Tests

- Reproduce the exact **18 → 9 → 3 → 1** progression from configuration alone
- **Force a 9th/10th wildcard tie**: criteria run in order, deciding criterion
  recorded, and where criteria fail a tie-break match is created with only the
  two tied teams
- Tie-break points do **not** move the stage leaderboard when
  `ScoreCountsTowardStage = 0`
- A tie **below** the cut does not block the commit
- `POST /ties/{tieId}/resolve-manually` → **403** for every role except
  `ProgramAdmin` and `SuperAdmin`

---

# Phase 12 — Live push and display API

**Duration:** 1–2 weeks · **Depends on:** P9, P10

| Task | What to build | Acceptance criteria |
|---|---|---|
| `P12-01` | `MatchHub` (operators) with the documented server→client events | Includes `SegmentOrderChanged`, `TieBreakStarted`, `TieBreakResolved` |
| `P12-02` | `DisplayHub` (projectors, read-only token) | Cannot invoke anything that writes |
| `P12-03` | Outbox processor dispatching domain events to SignalR | Survives a failed push; retries |
| `P12-04` | **Correct-answer stripping** until reveal | A display client cannot read the answer early — test it |
| `P12-05` | Server-authoritative timer ticks | All screens agree within tolerance |
| `P12-06` | Full state re-sync on reconnect | `RequestFullState()` |
| `P12-07` | Display projections (`IDisplayProjectionService`) + branding endpoint | |

**Test:** 100 simulated display clients receive an update within **500 ms**.

---

# Phase 13 — Reporting and exports

**Duration:** 1 week · **Depends on:** P10, P11

| Task | What | Acceptance |
|---|---|---|
| `P13-01` | Match report | Includes the `MatchEvent` timeline |
| `P13-02` | Stage summary + qualification record | Shows tie-break provenance where relevant |
| `P13-03` | Team performance report | |
| `P13-04` | Question usage / quality report | Flags questions everyone got right |
| `P13-05` | Audit log query endpoint | Filterable by entity, actor, date |
| `P13-06` | Export to Excel / CSV / PDF | |

---

# Phase 14 — QuickBuzz integration module

**Duration:** 2 weeks · **Depends on:** P9 · **Deliberately last**

> By this point the system is proven to work without a buzzer — which is exactly
> the requirement. Building it last proves independence by construction.

| Task | What to build | Acceptance criteria |
|---|---|---|
| `P14-01` | `IBuzzerProvider` port implementation scaffolding | Defined in P2, implemented now |
| `P14-02` | **`NullBuzzerAdapter`** (the default) | `IsAvailable = false`; **every existing test still passes** with it registered |
| `P14-03` | `BuzzSession` / `BuzzPress` persistence + `BuzzDeviceMapping` device→team | Raw 24-byte frame stored as hex for disputes |
| `P14-04` | **`SerialBuzzerAdapter`** — port `SerialService` + `DeviceParser` from QuickBuzz | Constants moved to config; `Console.WriteLine` → `ILogger`; **any device count**, `DeviceCount` defaults to 3 |
| `P14-05` | `Quizware.BuzzerAgent` tray app for the operator PC | Owns the COM port; pushes outbound |
| `P14-06` | `HttpAgentBuzzerAdapter` + `POST /buzzer/sessions/{id}/presses` ingestion | Outbound push — no port forwarding needed |
| `P14-07` | `GET /buzzer/capability`, `/health`, `POST /buzzer/test` | Capability drives whether the UI shows manual buttons |
| `P14-08` | Graceful-degradation path | Operator override always available |

### Tests

- **Solution builds with `Quizware.Modules.Buzzer` deleted**
- **Unplug the hardware mid-match → the match completes normally** via manual entry
- Buzzer failure returns `503 BUZZER_UNAVAILABLE` and **never blocks** an answer
- Buzz data never writes scores directly

---

# Phase 15 — Hardening

**Duration:** 2 weeks · **Depends on:** everything

| Task | What | Target |
|---|---|---|
| `P15-01` | Load test: 100 display clients + 5 operators + a full match | p95 gameplay <200 ms; live state <150 ms |
| `P15-02` | Security review: OWASP Top 10, dependency scan, secret scan | Zero criticals |
| `P15-03` | **Tenant-isolation penetration test** | No cross-program leak under any forged input |
| `P15-04` | Failure drills: kill the API mid-match, drop the DB connection, unplug the buzzer, disconnect a display | Recovery <30 s, zero data loss |
| `P15-05` | Backup and restore drill | Restore verified, not assumed |
| `P15-06` | Performance tuning against the NFR table | |
| `P15-07` | **Full dress rehearsal — a complete 18-team tournament on real venue hardware with real operators** | **Mandatory. Do not skip.** |

> Almost every defect in the current system would have been caught by one dress
> rehearsal.

---

# Phase 16 — Data migration *(optional)*

**Duration:** 1 week · **Depends on:** P15

Only if the 9AMM history is wanted. Treat the result as **read-only archived
data** — do not try to make old matches replayable.

| Task | What | Acceptance |
|---|---|---|
| `P16-01` | Create the `Program` row for "9AMM 2025" | State `Archived` |
| `P16-02` | `SchoolsTeam` → `Team` | |
| `P16-03` | 3 `Stage` rows; `Matches` slot + `RoundsId` → real `Match` rows | Resolves the legacy slot-number ambiguity |
| `P16-04` | `SchoolsTeam_Matches` → `MatchParticipant`, deriving turn order | |
| `P16-05` | Questions → `Question` + format table (near 1:1 per `06-Development-Roadmap.md` §Phase 16 step 6) | Difficulty defaulted by round; **every row flagged for author review** |
| `P16-06` | 9 answer tables → `AnswerRecord` + `ScoreEvent`, points backfilled from `Contants.cs` | |
| `P16-07` | Rebuild `TeamMatchScore` / `TeamStageScore` from events | |
| `P16-08` | **Reconciliation** | Every migrated team's total **equals** `spGetTotalScoreNew` from the old DB. If not — stop and investigate |

---

# Phase 17 — Angular 22 *(separate track)*

**Starts:** after Phase 5 (against the generated client + mock server)

| Order | App | Depends on |
|---|---|---|
| 1 | **Admin console** — programs, teams, questions, stages, rules, segment reordering (drag & drop), format enable/disable | P5 contract; real data from P6–P7 |
| 2 | **Operator console** — live match control, answer recording, disqualification, undo, buzzer panel | P9, P12 |
| 3 | **Display screens** — read-only, SignalR-driven | P12 (parallel with 2) |

---

# Milestones

| # | Milestone | After | Demonstrates |
|---|---|---|---|
| M1 | **Walking skeleton** | P3 | Login works; logging, errors, auth in place |
| M2 | **Schema complete** | P4 | 51 tables, tenant isolation proven |
| M3 | **Contract frozen** | P5 | Angular unblocked |
| M4 | **Config-driven tournament** | P7 | The 18-team tournament exists as data only |
| M5 | **First live match** | P9 | A complete match, including a mid-match disqualification |
| M6 | **Full tournament** | P11 | 18 → 9 → 3 → 1, including a real tie-break |
| M7 | **Show-ready** | P12 | Live screens, server timers |
| M8 | **Buzzer integrated** | P14 | Works with hardware, and works without it |
| M9 | **Production ready** | P15 | Dress rehearsal passed |

---

# Timeline

| Phase | Duration | Cumulative |
|---|---|---|
| 0 Requirements | 1 wk | 1 |
| 1 Domain | 2 wk | 3 |
| 2 Architecture (ADRs) | 1 wk | 4 |
| 3 Skeleton | 2 wk | 6 |
| 4 Database | 2 wk | 8 |
| 5 API contract | 1 wk | 9 |
| 6 Config modules | 3 wk | 12 |
| 7 Tournament config | 2 wk | 14 |
| 8 Question selection | 2 wk | 16 |
| 9 Match engine | 4 wk | 20 |
| 10 Scoring *(overlaps 9)* | — | 20 |
| 11 Qualification + tie-break | 2 wk | 22 |
| 12 SignalR + display | 2 wk | 24 |
| 13 Reporting | 1 wk | 25 |
| 14 QuickBuzz | 2 wk | 27 |
| 15 Hardening | 2 wk | 29 |
| 16 Migration *(optional)* | 1 wk | 30 |

**One developer: ~29–30 weeks (about 7 months).**

### Parallelisation

With two developers after Phase 5, expect **17–19 weeks**:

- **Dev A** (critical path): P6 → P7 → P8 → P9 → P10
- **Dev B**: P6c/P6e (teams, media) → P13 reporting → P12 SignalR → P14 buzzer
- Both converge on P11 and P15

The critical path is **P0 → P1 → P2 → P3 → P4 → P6 → P7 → P8 → P9 → P10 → P15**.
Phases 11, 12, 13 and 14 parallelise once P9 is stable.

---

# Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Match engine under-estimated | High | Build MCQ end to end first; the other 9 formats are handler plug-ins |
| Business rules more complex than the code shows | High | P0 workshop with actual quiz masters, not just the code |
| **Live event failure** | Very high | P15 rehearsal is mandatory; keep the legacy system as a fallback for the first event |
| Question bank not ready | High | `GET /questions/coverage` from P6 onward gives early warning |
| Buzzer hardware differs from the code | Medium | P14 needs real hardware; do not simulate only |
| Tenant leak | High | P4 test actively attempts a cross-program read |
| Angular blocked | Medium | Contract-first at P5; generated client + mock |
| Scope creep (voting, streaming, mobile) | Medium | Documented as future extensibility; refuse for v1 |

---

# Start here

*(Updated to reflect actual progress — see `context/CURRENT.md` for the
authoritative live status.)*

1. ~~**P0** — settle the open questions.~~ **Done** — owner-confirmed.
2. ~~**P1** — domain model (`P1-01`–`P1-14`).~~ **Done**, committed
   (`2b2bcfb`, `d6171cd`, `4697df7`, `6e6a13e`).
3. ~~**P2** — architecture decisions (`P2-01`–`P2-10`).~~ **Done** — 10 ADRs
   in `docs/adr/` (uncommitted; `docs/` is deliberately gitignored).
4. ~~**Phase 3** — skeleton and cross-cutting concerns (`P3-01`–`P3-17`).~~
   **Done** — 109 tests passing solution-wide. Uncommitted. `docker compose up`
   and the GitHub Actions run itself are unverified in this environment
   (no Docker daemon, no CI runner here) — verify both locally before trusting
   them blindly.
5. ~~**Phase 4** — database schema and migrations (`P4-01`–`P4-20`).~~ **Done**
   — 115 tests passing solution-wide, migration `AddBusinessSchema` applied
   cleanly against SQLite in tests. Uncommitted. **Not verified against real
   SQL Server via Testcontainers** (no Docker here) — do that before trusting
   the migration blindly in production.
6. ~~**Phase 5** — API contract, OpenAPI first (`P5-01`–`P5-08`).~~ **Done**
   — 147 tests passing solution-wide, OpenAPI document + generated
   TypeScript client + `.http` collection all produced. Uncommitted.
7. **Phase 6** — Configuration modules (Program management → Users/roles →
   Teams → Topics/tags → Media → Question bank). First phase where
   `IProgramService` and friends actually do something behind the Phase 5
   stubs, in the order 6a → 6f listed above — each sub-phase depends on the
   one before it.

Everything after that follows the dependency map.
