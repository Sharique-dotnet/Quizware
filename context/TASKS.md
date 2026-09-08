# TASKS

Work items (`T-###`), open questions (`Q-###`), and things that need checking
before they can be relied on. Done items stay — they are the record of what was
already tried. Format: `_meta/SPEC.md` §6.4.

**Last updated:** 2026-09-08 (S-2026-09-08-02)

---

## Active work

- **T-006** `TODO` · Decide whether `QuizApp.BuzzerAgent` references `QuizApp.Modules.Buzzer`
  - Why: The agent needs the serial frame-parsing logic (`DeviceParser`/
    `SerialService`, ported from legacy QuickBuzz per roadmap task `P14-04`).
    Currently it has zero project references — an explicit gap, not an oversight.
  - Where: `QuizApp/tools/QuizApp.BuzzerAgent/QuizApp.BuzzerAgent.csproj`
  - Blocked by: nothing urgent — this is Phase 14 work per
    `docs/Implementation-Plan.md`. Flagged now so it isn't assumed silently later.

- **T-018** `TODO` · Ask the user whether to commit the Postman collection and
  the three rule-handler bugfixes
  - Why: `git status` confirms these are the only uncommitted work as of this
    checkpoint — Phase 6e/6f (`783bc1c`) and Phase 7 (`3dbc6f2`) are already
    committed (see T-015/T-016, both closed, and L-004's third recurrence).
  - Where: `QuizApp/postman/QuizApp.postman_collection.json`,
    `QuizApp/postman/README.md` (both new), plus modifications to
    `Application/Rules/Commands/{UpsertScoringRules,UpsertQualificationRules,
    UpsertTieBreakRules}.cs` and `tests/QuizApp.Api.IntegrationTests/RulesEndpointTests.cs`
    (the new regression test). All staged (`git add`'d) but not committed.
  - Blocked by: standing policy (V-005) — never commit without being asked.
    Offered message: "Add Postman collection covering all Phase 0-7 endpoints,
    and fix three rule-upsert handlers that 500'd on a natural-key collision".
    A single commit is reasonable here — the bugfixes were found *while*
    building/validating the Postman collection, so they aren't a cleanly
    separable unit of work the way Phase 6e vs 6f were.

- **T-019** `TODO` · Implement Phase 8 — Question selection engine (`IQuestionSelector`)
  - Why: Phase 7 (Tournament configuration) is now fully done, including
    `IRuleService` (P7-10) which Phase 8's selector will need to resolve
    scoring rules. Per `docs/Implementation-Plan.md`'s dependency map, Phase 8
    depends on P6f (question bank, done) and P7 (rule management, done) — both
    prerequisites are now satisfied.
  - Where: `docs/Implementation-Plan.md` Phase 8 section — **re-read it fresh**,
    do not assume its text matches `CURRENT.md`.
  - Blocked by: T-018 (commit decision) is not a hard blocker, but should be
    resolved first per standing workflow (same pattern as T-015→T-016).

- **T-017** `TODO` · Re-sync `docs/Implementation-Plan.md`'s "Start here" section
  (~line 794) with actual progress
  - Why: It still names Phase 6 as next and describes Phases 3–5 as
    "Uncommitted" — both stale as of this session (Phase 6 is done; Phases 3–5
    have been committed since S-2026-09-07-01... `[UNVERIFIED]`, re-check before
    editing). Same class of drift as T-008 (Phase 0 doc text) — low priority,
    doesn't block code work, but misleads anyone reading the plan doc cold.
  - Where: `docs/Implementation-Plan.md` lines ~794–823.
  - Blocked by: nothing; low priority, cosmetic-but-misleading.

## Open questions

- **Q-001** `ANSWERED` (S-2026-09-04-03) · Which of the ~18 Phase 0 assumptions do
  you confirm?
  - `[DECIDED]` via D-015: the user is QuizApp's sole owner/stakeholder, so Phase 0
    is treated as owner-confirmed rather than run as an external workshop. All 16
    assumptions in `docs/new-system/06-Development-Roadmap.md` §6.5 are accepted
    as-is and are now `[DECIDED]`, not `[ASSUMED]`. Domain-model work (Phase 1) is
    no longer blocked on this — see T-010 (Closed), which shows Phase 1 has since
    been fully implemented against these assumptions.
  - Residual detail worth keeping visible: rows that previously read as the
    widest blast-radius sub-points — shared vs per-program question bank,
    disqualified-team score handling, one-match-per-stage-or-not, negative
    totals — are covered by the same owner-confirmed acceptance. If a genuine
    second stakeholder is ever brought onto the project (e.g. a co-organiser),
    these rows are the ones worth re-confirming with them specifically, since
    "owner-confirmed" resolved *who* signs off, not independent verification
    against real event operations. See D-015's Consequences for the caveat that
    the Implementation-Plan.md doc text itself hasn't caught up with this
    decision yet (T-008).

- **Q-002** `ANSWERED` · Should `context/` be committed, and should the outer repo get
  its first commit?
  - `[FACT]` Yes — done. See T-004. `git log` shows `bf8cb06` includes `context/`
    in full, made sometime between this session's Part 1 (design doc edits) and
    Part 2 (solution scaffolding) — the scaffolding itself is staged but not yet
    in that commit (see Q-004).

- **Q-003** `OPEN` · Which other AI tools need adapters?
  - Blocks: nothing.
  - `[FACT]` The user named two Claude accounts, Codex, Cursor, "and potentially
    others". Adapters exist for Claude Code (`.claude/`), Cursor
    (`.cursor/rules/`), and the `AGENTS.md` convention, which Codex and several
    other agents read. `[UNVERIFIED]` whether the user's Codex setup reads
    `AGENTS.md` at the repository root. Anything without an adapter still works if
    pointed at `context/_meta/SPEC.md` — the cost of a missing adapter is one
    sentence of prompting, not a failure.

- **Q-004** `ANSWERED` (S-2026-09-04-03) · Commit the staged solution scaffolding
  now, or leave it staged?
  - `[FACT]` It was committed. `git log` as of this session shows the scaffolding
    landed as `893c4a7` and the domain model that followed landed as three
    further commits (`d6171cd`, `4697df7`, `6e6a13e`) plus `2b2bcfb` for the
    turn-order calculator. See T-007/T-010 (Closed). Only `CLAUDE.md` has an
    uncommitted one-line addition as of this session (the new "always refer to
    `context/`" Instructions bullet).

- **Q-005** `OPEN` (added S-2026-09-07-01) · `POST /buzzer/sessions/{id}/presses`
  is `[AllowAnonymous]` in Phase 5 because agent-to-API auth is explicitly a
  Phase 14 concern (ADR-005). Not the final security posture — needs a real
  auth scheme for the buzzer agent before Phase 14 ships. Blocks: Phase 14
  hardening, not current work.

## Verification queue

Things currently tagged `[ASSUMED]` or `[UNVERIFIED]` that will mislead someone if
they stay unchecked.

- **V-001** · `[UNVERIFIED]` Codex reads a root `AGENTS.md` in the user's setup.
  - Check: open this repo in Codex and ask "what should you read first?" If it
    does not mention `context/CURRENT.md`, the adapter needs a different filename.
  - Matters for: Q-003, and for the cross-tool guarantee generally.

- **V-003** · `[UNVERIFIED]` A SQL Server instance is available for development.
  - Check: ask the user, or look for a connection string once Phase 3 creates
    `appsettings.Development.json`.
  - Matters for: Phase 4 (schema and migrations). Superseded in practice by
    V-006/V-007 below — Phase 4 shipped against SQLite instead, real SQL Server
    still unverified.

- **V-006** (added S-2026-09-07-01) · `[UNVERIFIED]` `docker compose up`
  (`QuizApp/docker-compose.yml`) and the GitHub Actions CI workflow
  (`.github/workflows/ci.yml`) both from Phase 3 — neither has been run in this
  dev environment (no Docker daemon, no CI runner). This is stated directly in
  `docs/Implementation-Plan.md`'s own Phase 3 exit-criteria text, not just this
  session's guess.
  - Check: run `docker compose up` locally once Docker is available; push to
    trigger the Actions workflow once a remote exists.
  - Matters for: trusting the dev-DB and CI setup before relying on them.

- **V-007** (added S-2026-09-07-01) · `[UNVERIFIED]` The Phase 4 migration
  `AddBusinessSchema` has only been verified against SQLite
  (`CustomWebApplicationFactory` in integration tests), not real SQL Server via
  Testcontainers as the plan's exit criteria call for — no Docker daemon here.
  - Check: run the same test suite against SQL Server via Testcontainers once
    Docker is available.
  - Matters for: trusting the migration in production without surprises from
    SQL-Server-specific behavior SQLite doesn't share.

- **V-008** (added S-2026-09-08-01) · `[UNVERIFIED]` The three migrations
  generated this session (`FixMatchParticipantRemovalCheckConstraint`,
  `AddTopicParentForeignKeyAndTagUniqueIndex`, `AddQuestionDifficultyCheckConstraint`)
  were reportedly applied to the LocalDB (`(localdb)\MSSQLLocalDB`, database
  `QuizApp-Dev`) and live-verified during the session that produced them — this
  checkpoint confirmed `[FACT]` that all 5 migrations are present on disk and
  recognized by `dotnet ef migrations list` against the configured connection,
  but did not independently query the LocalDB to confirm they were actually
  applied (not just generated).
  - Check: `dotnet ef database update` (should be a no-op if already applied)
    or query `__EFMigrationsHistory` in `QuizApp-Dev` directly.
  - Matters for: trusting the dev DB schema matches the code before Phase 7
    work assumes the fixed `CK_MP_Removal` constraint or the new FK/indexes.

- **V-009** (added S-2026-09-08-02) · `[UNVERIFIED]` This checkpoint's claim
  that Phase 7 landed with a clean `dotnet build`/`dotnet test` (238
  passed/0 failed) was **not independently re-verified this session** — a
  `dotnet build` attempted during the save itself failed with `MSB3027`/
  `MSB3021` file-lock errors (`QuizApp.Api.exe` PID 5752 running, plus
  Visual Studio holding some of the same DLLs). Killing a live dev-server
  process to force a clean rebuild was judged out of scope for a
  context-only checkpoint. The claim is plausible (git history shows a
  full, cleanly-organized commit with 22 new tests, and the diff content of
  the 3 rule-handler fixes read as complete and self-consistent) but rests
  on the brief's account, not this session's own run.
  - Check: stop the running `QuizApp.Api.exe` (and close Visual Studio if it
    also holds a lock), then `dotnet build QuizApp.slnx` and
    `dotnet test QuizApp.slnx --no-build` from `QuizApp/`.
  - Matters for: trusting that Phase 7's 22 new tests and the 3 rule-handler
    bugfixes actually pass before starting Phase 8 on top of them.

- **V-005** · Resolved as `[DECIDED]` (S-2026-09-04-03) · Commits are made only
  when the user explicitly asks; the AI proposes a plan and a commit message but
  does not run `git commit` itself.
  - `[FACT]` `CLAUDE.md` now has an explicit "Instructions" section (added by the
    user directly, verified present on disk) stating this in writing: "Never
    commit the change on your own until asked," plus "always provide plans in
    phases, after each phase meaningful single line commit message," separate
    commit messages for API vs Angular changes, and a What/Why/Where/Affects
    format for every plan. This is no longer an inferred convention — it is
    written policy in the repo.
  - Note (S-2026-09-04-03): despite this, `git log` shows several commits
    (`893c4a7`, `471a60a`, `2b2bcfb`, `d6171cd`, `4697df7`, `6e6a13e`) made during
    or around this and prior sessions' work, all authored directly by the user
    (`Sharique`) — consistent with the policy (the AI proposed messages, the user
    ran the commits), not a contradiction. A prior brief for S-2026-09-04-0x
    claimed the Phase 1 domain-model work was still uncommitted at session end;
    that claim was **stale** by the time this checkpoint ran — verified `[FACT]`
    against `git log` this session. Lesson recorded as L-004.

## Closed

- **T-016** `DONE` · Implement Phase 7 — Tournament configuration (P7-01–P7-12)
  - Delivered in S-2026-09-08-02, committed as `3dbc6f2` ("Stage/segment CRUD
    with reorder, scoring/selection/qualification/tie-break rule management,
    IRuleService resolution, program readiness validation, and the 18-team
    seed script") — `[FACT]` verified via `git show --stat 3dbc6f2` (43 files,
    +2209/-37 lines).
  - Stage CRUD + reorder, segment-template CRUD + reorder (with locked
    segments keeping their slot — see D-024), `SegmentOrderMode` mutator,
    scoring/selection/qualification/tie-break rule upsert + two reset-to-
    defaults commands, `IRuleService`/`RuleService` (specificity-based
    resolution: segment beats stage beats program), the richer
    `STAGE_HAS_NO_SEGMENTS` program/stage readiness check, and
    `TournamentSeeder.cs` (18-team demo tournament, wired into `Program.cs`
    behind the same `!IsProduction` guard as `AdminUserSeeder`).
  - Real pre-existing bug found and fixed: reorder handlers needed a
    two-phase reindex to avoid transiently violating a unique `OrderIndex`
    index — see D-023.
  - New Domain mutators added to entities that had been create-only through
    Phase 1–6: `Stage.{Rename,Reorder,SetSegmentOrderMode,Delete}`,
    `StageSegmentTemplate.{Update,Delete}`, `ScoringRule.{UpdatePoints,Delete}`,
    `QualificationRule.Update`, `TieBreakRule.{Update,Delete}`,
    `QuestionSelectionRule.Update`.
  - New `Domain/Qualification/DefaultTieBreakValues.cs` (3 seed rows, mirrors
    `DefaultScoringValues`'s pattern from Phase 4).
  - No new EF migration needed — confirmed via
    `dotnet ef migrations has-pending-model-changes`.
  - Testing: 22 new tests (`StagesEndpointTests.cs`, `RulesEndpointTests.cs`);
    `[UNVERIFIED]` this checkpoint — see V-009 — the brief's own claimed count
    (238 passed / 0 failed) was not independently re-run this session due to
    a locked build (running `QuizApp.Api.exe` + Visual Studio holding DLLs).
  - Full detail: `sessions/2026-09-08-02-phase7-tournament-configuration.md`.

- **T-015** `DONE` (superseded its own premise) · Ask the user whether to
  commit Phase 6e (Media) and 6f (Question bank)
  - `[FACT]` Resolved by action, not by an explicit conversation turn this
    context system witnessed: `git log` (checked S-2026-09-08-02) shows both
    landed as `783bc1c` ("Media upload with validation/deduplication, and
    question bank CRUD with versioning, approval, import, coverage, and
    duplicate detection") — the user ran the commit out-of-band, consistent
    with V-005's standing workflow. This is the third recurrence of the
    pattern in L-004 (a brief's "still uncommitted" claim being stale by save
    time) — see L-004's third entry.

- **T-014-PRIOR-NOTE:** T-011, T-012, T-013 below were reconstructed in
  S-2026-09-07-01 from `docs/Implementation-Plan.md`'s own inline phase-status
  text and `git log`/`git show`, **not** from a conversation brief (the brief
  handed to that session only covered Phase 5). See that session's file for the
  reconstruction method.

- **T-013** `DONE` · Implement Phase 5 — API contract, OpenAPI first (P5-01–P5-08)
  - Delivered per this session's (S-2026-09-07-01) conversation brief; verified
    `[FACT]` against the repo: 16 controllers under
    `QuizApp/src/QuizApp.Api/Controllers/v1/`, `dotnet build` → 0 warnings/errors,
    `dotnet test` → 147 passed/0 failed (95 Domain, 4 Application, 4
    Architecture, 44 Api.IntegrationTests), TS client
    `QuizApp/clients/typescript/quizapp-api-client.ts` → 19,419 lines (brief
    claimed ~19,400 — matches).
  - Contracts/V1/ (P5-01–P5-04): per-area request/response DTOs; the 10 per-format
    question create-request records in `Contracts/V1/Questions/Formats/`; the
    `[JsonPolymorphic]`/`[JsonDerivedType]` discriminated-union `QuestionResponse`
    in `Contracts/V1/Questions/QuestionResponses.cs` (P5-05); FluentValidation
    validators per format in `QuestionFormatValidators.cs`.
  - 16 controllers (P5-06): 14 new + AuthController/AdminController extended, all
    actions `StatusCode(501)` typed `ActionResult<TResponse>` — see D-017 for why
    the typing is load-bearing for Swagger, not cosmetic.
  - TypeScript client (P5-07) via NSwag, not openapi-generator-cli — see D-018,
    L-006 (no JVM in this environment).
  - `.http` collection (P5-08): `QuizApp/src/QuizApp.Api/QuizApp.Api.http`,
    covers all 16 areas plus a full login-to-qualification workflow.
  - Five scope decisions recorded inline in `docs/Implementation-Plan.md`'s
    Phase 5 section: (1) not every route has a bespoke response DTO — only
    documented request bodies and worked-JSON-example endpoints do; (2)
    `POST /buzzer/sessions/{id}/presses` is `[AllowAnonymous]`, not final — see
    Q-005; (3) the Swashbuckle polymorphism gap, D-017; (4) NSwag over
    openapi-generator-cli, D-018; (5) per-endpoint literal JSON examples from
    the API design doc are not wired into Swagger via `schema.example` —
    judged not worth the fragility risk against Swashbuckle 10.x's example API,
    XML doc comments substitute instead.
  - **Staged but not committed** as of this checkpoint — offered commit message:
    "Add API contract: request/response DTOs, question format validators, 16
    controller stubs, and generated TypeScript client".
  - Full detail: `sessions/2026-09-07-01-phases-2-3-4-5-catchup.md`.

- **T-012** `DONE` · Implement Phase 4 — database schema and migrations (P4-01–P4-20)
  - Reconstructed `[FACT]` from `docs/Implementation-Plan.md`'s own "Status: all
    20 tasks done" text and `git show --stat c624f51` ("Add full business
    schema, global query filters, audit interceptors, and pluggable buzzer
    persistence") — **committed**, unlike Phase 5.
  - ~58 tables total (49 new in migration `AddBusinessSchema`, on top of Phase
    3's 9 Identity/token tables) vs. the design doc's nominal 51 — reconciled by
    three scope decisions recorded in the plan doc: (1) lookup values are C#
    enum columns, not separate tables, matching Phase 1's enums; (2)
    `ScoringRule`/`TieBreakRule` defaults seed per-program (Phase 6's "create a
    program" flow), not globally, since no program exists yet at migration time
    — legacy values captured meanwhile in
    `QuizApp.Domain.Scoring.DefaultScoringValues`; (3) most FKs are plain `Guid`
    columns without formal EF relationships (Phase 1 has no navigation
    properties by design) — named indexes/unique/check constraints from the
    schema doc are all in place regardless.
  - Global query filters (tenant + soft delete) applied reflectively to every
    `ITenantScoped`/`ISoftDeletable` entity; tenant filter is a no-op (never
    throws) outside a program-scoped request, e.g. background jobs/seeding.
    Audit + audit-log interceptors (`AuditableEntitySaveChangesInterceptor`,
    `AuditLogSaveChangesInterceptor`). Buzzer tables live in a separate assembly
    picked up via a new `IEntityConfigurationAssemblyMarker` port in
    Application, so Infrastructure never references the buzzer module directly.
  - Admin user seeded (`admin@quizapp.local` / `ChangeMe!123`, `SuperAdmin`,
    skipped in Production) via `AdminUserSeeder`.
  - Tests: 115 passing at the time (95 Domain, 4 Application, 4 Architecture, 12
    Api.IntegrationTests — 6 new persistence tests: tenant isolation, soft
    delete, audit stamping, audit log, NOT-NULL enforcement, seed fidelity), run
    against SQLite in `CustomWebApplicationFactory`, **not** real SQL Server via
    Testcontainers (no Docker here) — see V-007.
  - Full detail: `sessions/2026-09-07-01-phases-2-3-4-5-catchup.md`.

- **T-011** `DONE` · Implement Phases 2 and 3 (ADRs; skeleton and cross-cutting concerns)
  - Reconstructed `[FACT]` from `docs/Implementation-Plan.md`'s own "Status:
    DONE" text and `git show --stat 4452949` ("Add core infra: Identity, JWT,
    DI, logging, tests") for Phase 3 — **committed**. Phase 2's 10 ADRs exist on
    disk in `docs/adr/` but are **uncommitted by construction** since all of
    `docs/` is gitignored — see D-016 — not a pending-commit gap the way Phase
    5's staged files are.
  - Phase 2 (ADRs, P2-01–P2-10): `docs/adr/ADR-001` through `ADR-010` — modular
    monolith, multi-tenancy, TPT questions, event-sourced scoring,
    buzzer-port-null-default, SignalR+outbox, tie-break-as-ordinary-match, local
    hosting, authorisation model, module boundaries. This resolves the
    "Phase 2 not started" state the prior checkpoint (S-2026-09-04-03) recorded
    — superseded, not contradicted (nothing about Phase 1 or earlier changes).
  - Phase 3 (skeleton, all 17 tasks): DI wiring (`AddApplication`/
    `AddInfrastructure`/`AddApi`), ASP.NET Core Identity + JWT (15 min
    access/7 day refresh, rotated on use, hash stored not the raw token), 7
    roles seeded via `RoleSeeder`, 9 authorization policies matching
    `05-API-Design.md` §5.9 exactly, `ProgramScopeMiddleware` (rejects
    `{programId}`-vs-JWT-claim mismatches with 403 before hitting the DB),
    `GlobalExceptionHandler` (RFC 9457 `application/problem+json`, maps all 7
    P1-14 domain exceptions plus `NoActiveParticipantsException` and
    `ValidationException`), Serilog + `CorrelationIdMiddleware`,
    `ValidationBehavior<TRequest,TResponse>` MediatR pipeline (aggregates every
    validator's every failure, not just the first), `ICurrentUser`/
    `ICurrentProgram`/`IClock` + test fakes, `IdempotencyFilter` +
    `EfIdempotencyStore`, `/health/live` and `/health/ready` (DB-backed),
    Swagger + bearer scheme, Docker Compose for local SQL Server (unverified
    here, no Docker daemon — see V-006), GitHub Actions CI (unverified here, no
    runner — see V-006). Working endpoints: `GET /api/v1/admin/health`,
    `POST /api/v1/auth/login`.
  - Scope decision recorded in the plan doc: Phase 3's `AppDbContext` covers
    only Identity/RefreshToken/IdempotencyRecord tables (migration
    `InitialIdentitySchema`) — the full 51-table business schema is added to
    this *same* context in Phase 4, not a second context.
  - Testing note recorded in the plan doc: `QuizApp.Api.IntegrationTests` swaps
    `AppDbContext`'s SQL Server connection for an in-process SQLite database (a
    real relational engine enforcing constraints, not the forbidden EF Core
    InMemory provider) — `EnsureCreatedAsync()` under `Testing`,
    `MigrateAsync()` otherwise.
  - Tests at the time: 109 passing (95 Domain, 4 Application, 4 Architecture, 6
    Api.IntegrationTests).
  - This closes out **T-005** (Phase 3 cross-cutting concerns — now fully
    delivered, superseding its earlier `TODO` status) and **T-009** (the
    Phase 1/2/3 sequencing question — resolved in practice as Domain → Skeleton
    → ADRs → Schema → Contract, not the roadmap's nominal Domain → ADRs →
    Skeleton order, but every phase did get done). Neither entry's original text
    is deleted — this note supersedes their open status per SPEC §4.4.
  - Full detail: `sessions/2026-09-07-01-phases-2-3-4-5-catchup.md`.

- **T-009** `DONE` · Decide Phase 1 vs Phase 2 vs Phase 3 sequencing going forward
  - Original text (S-2026-09-04-03): Phase 1 (domain model) is now fully
    implemented (P1-01–P1-14, see T-010 Closed). Phase 2 (ADRs) has not been
    started. Phase 3 (skeleton) was already partially done before Phase 1 even
    started (T-002/T-005) — so the roadmap's recommended order (Domain → ADRs
    → Skeleton) has now been done out of sequence in two different ways in two
    different sessions. Whoever resumes should pick: backfill Phase 2 (ADRs)
    before continuing Phase 3's remaining cross-cutting concerns, or proceed
    with Phase 3 (T-005) directly since the domain model it would sit on top of
    now exists.
  - Resolved (S-2026-09-07-01): both were done, in the order Domain (1) →
    Skeleton (3) → ADRs (2) → Schema (4) → Contract (5) — not the roadmap's
    nominal order, but every phase got delivered and nothing was blocked by the
    reordering. See T-011 (Closed) for the full Phase 2/3 detail.

- **T-005** `DONE` · Implement Phase 3's cross-cutting concerns
  - Original text (S-2026-09-04-03): T-002 delivered only the project/reference
    skeleton. Roadmap Phase 3 also calls for DI wiring beyond framework
    defaults, Identity/JWT, a global exception handler, Serilog, a
    FluentValidation pipeline, health checks beyond template defaults, and CI.
    None of these existed yet at that time.
  - Resolved (S-2026-09-07-01): all 17 Phase 3 tasks delivered and committed as
    `4452949`. See T-011 (Closed) for the full breakdown.

- **T-008** `DONE` · Re-sync `docs/Implementation-Plan.md` Phase 0 section with D-015
  - `[FACT]` Verified this session (S-2026-09-07-01): line 73 of
    `docs/Implementation-Plan.md` now reads
    `# Phase 0 — Requirements confirmation — **DONE (skipped, owner-confirmed)**`.
    The earlier attempt's edit that "didn't persist" (per S-2026-09-04-03) has
    since been re-applied and this time verified present on disk.

- **T-010** `DONE` · Implement Phase 1 — the domain model (P1-01 through P1-14)
  - Delivered in S-2026-09-04-03, verified `[FACT]` against the repository this
    session (89 `.cs` files under `QuizApp/src/QuizApp.Domain/`, 23 under
    `QuizApp/tests/QuizApp.Domain.Tests/`, `dotnet test` on the Domain test
    project → 95 passed, 0 failed; `dotnet build` on the full solution → 0
    warnings/0 errors). All committed as `d6171cd`, `4697df7`, `6e6a13e` on top
    of `2b2bcfb` (P1-02, from an earlier session).
  - Common/ (P1-01): `BaseEntity`, `ITenantScoped`, `IAuditable`,
    `ISoftDeletable`. Enums/ (P1-03): all plan-listed enums plus several more
    added on demand while building later tasks (`QuestionPoolScope`,
    `QuestionStatus`, `QuestionOwnerScope`, `PassDirection`, `CardRevealMode`,
    `SequenceItemKind`, `MediaKind`, `TieBreakAnswerMode`, `StageType`,
    `TeamCountChangePolicy`, `TopicSelectionMode`, `MatchSegmentState`,
    `MatchQuestionState`, `AnswerSource`, `ScoreEventType`, `SeedingMode`,
    `OnStillTiedPolicy`, `TieBreakEventState`, `QualificationReason`).
    `QuestionFormatCode` integer values match the seed ids in
    `docs/new-system/04-Database-Schema.md` §4.3, `[FACT]` verified this session
    — id 4 is an intentional gap, documented in a comment on the enum itself.
  - Teams/ (P1-05): `Team` (status changes require a reason via `ChangeStatus`),
    `TeamMember`. QuestionBank/ (P1-06): `Question` abstract base (TPT, per
    D-009) plus 10 sealed subclasses, `QuestionOption`, `SequenceItem`,
    `VisualRapidFireItem`, `Topic`, `Tag`, `MediaAsset`. `AudioVisualQuestion`
    enforces non-null `MediaAssetId` and non-empty `AnswerText` in its factory
    method, per D-009's original NOT-NULL reasoning. `QuestionTag` was
    deliberately **not** modeled here — left for an EF many-to-many mapping in
    Phase 4, since P1-06's acceptance criteria didn't require a join entity.
  - Tournament/ (P1-07): `Stage`, `StageSegmentTemplate`, `Match`,
    `MatchParticipant` (implements `ITurnOrderParticipant`, integrates with
    `TurnOrderCalculator` from P1-02). `Match.Start(int activeParticipantCount)`
    enforces >=2 active participants via the new `InsufficientParticipantsException`.
  - Gameplay/ (P1-08): `MatchSegment` (`Open()` enforces one-open-segment-per-match
    by taking the sibling list as a parameter; `Reorder()` throws
    `SegmentNotReorderableException` if locked or not Pending), `MatchQuestion`
    (`Activate()` enforces one-active-question-per-segment the same way),
    `AnswerRecord` (`MarkReversed`), `MatchEvent` (append-only,
    `NextSequenceNumber` enforces strictly-increasing sequence numbers).
  - Scoring/ (P1-09): `ScoringRule`, `ScoreEvent` (immutable once created —
    `Reverse()` creates a **new**, opposite-signed event rather than mutating,
    marks the original `IsReversed = true`, throws if reversed twice),
    `TeamMatchScore` (`ApplyAnswer`/`ApplyAdjustment`, the incremental read
    model), `TeamStageScore`.
  - Qualification/ (P1-10): `QualificationRule`, `StageQualification`,
    `TieBreakRule` (`Criteria` stored as `IReadOnlyList<string>`, not raw JSON —
    JSON serialization is an EF-layer concern deferred to Phase 4),
    `TieBreakEvent` (`Resolve()` enforces "a resolved tie must name its
    resolution method," and additionally requires non-blank `Notes` when
    `ResolutionMethod` is Manual, matching a CHECK constraint in the schema doc),
    `TieBreakParticipant`.
  - Buzzer/ (P1-11): `BuzzRankingCalculator` — pure function ported from
    QuickBuzz's `DeviceApiController` ranking logic; lowest non-zero press time
    wins, `0` means "no press" and sorts last.
  - Tournament/SegmentOrderResolver.cs (P1-12): pure function — `Fixed` returns
    template order; `RandomPerMatch` shuffles unlocked segments deterministically
    via a stored `System.Random(seed)` while locked segments keep their template
    position; same seed always reproduces the same order (tested).
  - Qualification/TieBreakCriteriaEvaluator.cs (P1-13): pure function taking
    pre-normalized criterion values (caller ensures higher-is-always-better per
    criterion) and an ordered criterion-name list; narrows the tied group
    criterion-by-criterion and reports which criterion decided it, or still-tied.
  - Common/Exceptions/ (P1-14): all 7 domain exceptions exist —
    `InvalidStateTransitionException`, `InsufficientParticipantsException`,
    `QuestionPoolExhaustedException`, `ScoringRuleNotFoundException`,
    `UnresolvedTieException`, `SegmentNotReorderableException`,
    `FormatInUseException` — plus `NoActiveParticipantsException` from P1-02.
    Deliberate pattern across the whole phase: exceptions were introduced
    incrementally, when the entity that needed them was built, rather than all
    stubbed out upfront under P1-14.
  - Test project: `QuizApp.Domain.Tests` uses xUnit + FluentAssertions 6.12.2
    (added as a package reference this session, `[FACT]` verified present in
    the committed `.csproj`). `QuizApp.Domain.csproj` itself has **zero** NuGet
    package references, `[FACT]` verified — the domain layer stays dependency-free.
  - Full detail: `sessions/2026-09-04-03-phase0-owner-confirmed-phase1-domain.md`.

- **T-007** `DONE` · Commit the solution scaffolding
  - `[FACT]` Corrected this session: the scaffolding was committed as `893c4a7`
    ("Add initial project structure with API, application, domain, and
    infrastructure layers"), which already existed in `git log` before this
    checkpoint ran. The prior open state (staged-not-committed) reflected an
    earlier point in the project's history that had already been resolved by
    the time of this save; this task is closed rather than left open on stale
    information. See Q-004.

- **T-001** `DONE` (via D-015) · Confirm the Phase 0 requirement assumptions
  - Resolved in S-2026-09-04-03: not through a stakeholder workshop (the
    original plan for this task) but through an explicit decision that the user
    is QuizApp's sole owner and there is no separate stakeholder to run a
    workshop with. See D-015. The Implementation-Plan.md doc itself has not yet
    been re-edited to reflect this — see T-008.

- **T-000** `DONE` · Build the cross-AI context preservation system
  - Delivered in S-2026-09-04-01: `context/` structure, `_meta/SPEC.md` as the
    single procedure, the `context-keeper` subagent, `/save-context` and
    `/load-context`, and adapters for Claude, Cursor, and the `AGENTS.md`
    convention. Detail: `sessions/2026-09-04-01-context-system-bootstrap.md`.

- **T-002** `DONE` · Scaffold the QuizApp solution skeleton
  - Delivered in S-2026-09-04-02: `QuizApp/QuizApp.slnx` now lists 10 projects
    (`QuizApp.Domain`, `.Application`, `.Infrastructure`, `.Modules.Buzzer`,
    `.Api`, plus 4 test projects and the `QuizApp.BuzzerAgent` tool), all
    targeting `net10.0`, wired with project references exactly matching
    `docs/new-system/02-Architecture-Proposal.md` §2.4's dependency rule
    (`[FACT]`, verified by grepping every `.csproj`). Subfolder layout created per
    §2.4 with `.gitkeep` placeholders (34 folders). Template placeholder files
    (`Class1.cs`, `WeatherForecast*.cs`, `UnitTest1.cs`) deleted. `dotnet build`
    reported 0 warnings/0 errors; build artifacts for all 10 projects confirmed
    present on disk this session. Full detail:
    `sessions/2026-09-04-02-tpt-roles-scaffolding.md`.
  - Residual work is **not** part of this task — see T-005 (Phase 3 cross-cutting
    concerns) and T-006 (BuzzerAgent reference decision), which are genuinely new
    work, not loose ends of T-002.
  - Note: subsequently committed as `893c4a7` — see T-007 (Closed).

- **T-003** `DONE` · Exercise the context system on a real working session
  - This save (S-2026-09-04-02) is the exercise: a real merge against existing
    `CURRENT.md`/`DECISIONS.md`/`TASKS.md`, a stale-claim correction (§2 of
    `CURRENT.md`), six new decisions appended without disturbing D-001–D-008, and
    a `git log` discrepancy found and reconciled rather than trusted blindly. The
    merge/supersede/compaction paths described in `_meta/SPEC.md` §4.4 worked as
    specified.

- **T-004** `DONE` · Decide whether `context/` is committed to git
  - `[FACT]` Resolved by action, not by an explicit conversation: `git log` shows
    one commit, `bf8cb06 "Initial commit: project scaffolding, docs, and AI
    context system"`, whose tree includes all of `context/`. Answers Q-002.
    Nobody reported making this commit through `/save-context`, so record it as an
    out-of-band action rather than a documented decision with reasoning.

- **V-002** · `[FACT]` The .NET 10 SDK is installed on this machine.
  - Verified S-2026-09-04-02: `dotnet --list-sdks` → `8.0.421` and `10.0.400`,
    both under `C:\Program Files\dotnet\sdk`. Used to build all 10 QuizApp
    projects (T-002) with 0 warnings/0 errors.

- **V-004** · Resolved as D-014 · Deployment is to an on-premises venue server
  with no cloud dependency, `DeviceCount` configurable with a default of 3.
  - Was `[ASSUMED]`; promoted to `[DECIDED]` in S-2026-09-04-02 via the user
    confirming open questions 11 and 12 in
    `docs/new-system/06-Development-Roadmap.md` §6.5. See D-014.
