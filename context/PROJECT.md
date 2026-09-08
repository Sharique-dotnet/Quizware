# PROJECT — Quizware

Slow-changing orientation. Read `CURRENT.md` first; come here when you need the
stack, the map, or the conventions.

**Last updated:** 2026-09-08 (S-2026-09-08-02)

---

## Purpose

Rebuild the quiz-tournament system currently running as **QuizApp-9AMM** (ASP.NET
MVC 4) as a modern **ASP.NET Core Web API on .NET 10** with **SQL Server**, ready
for an **Angular 22** front end later. The legacy buzzer application
**QuickBuzz** is absorbed as an *optional* module.

The driving problem, from `docs/new-system/README.md`: the current system
hardcodes the tournament structure — a new database per event, `QuestionNumber % 3`
for turn order in 30+ places, and the running order of question types baked into
108 views as redirect chains. The new system makes all of that configuration data.

## Scope

**In scope (v1):** program/tenant configuration, teams, question bank across ten
formats, configurable tournament structure, dynamic question selection, the match
engine, event-sourced scoring, qualification and tie-breaking, live push, reporting,
optional buzzer integration.

**Out of scope (v1):** audience/online voting (designed for, not built). Data
migration from 9AMM is optional, Phase 16. Angular front end is a separate track,
Phase 17.

## Domain glossary — read this before editing anything

Three systems share confusingly similar names. Getting this wrong means editing
the wrong codebase.

| Name | What it is |
|---|---|
| **Quizware** | The **new** system. Solution `Quizware/Quizware.slnx`. `[FACT]` 10 projects exist: `src/Quizware.{Domain,Application,Infrastructure,Modules.Buzzer,Api}`, `tests/Quizware.{Domain,Application}.Tests`, `tests/Quizware.Api.IntegrationTests`, `tests/Quizware.Architecture.Tests`, `tools/Quizware.BuzzerAgent`. |
| **QuizApp-9AMM** | The **existing** ASP.NET MVC 4 system being replaced. Read-only reference. |
| **QuickBuzz** | The **existing** buzzer application, to be absorbed as the optional `Quizware.Modules.Buzzer`. |

Domain terms: a **Program** is the tenant root (one event/year). A **Stage** holds
**Matches**; each match has **MatchParticipants** with a fixed `SeatNumber` and a
recalculated `TurnOrder`. A **StageSegmentTemplate** defines which question format
is played and in what order. Scoring is **event-sourced** via `ScoreEvent`.

## Architecture

`[DECIDED]` Modular monolith, Clean Architecture, four projects plus an optional
buzzer module. Shared schema with `ProgramId` multi-tenancy enforced by an EF Core
global query filter driven by a JWT claim.

**Do not restate the architecture here.** It is fully specified in
`docs/new-system/02-Architecture-Proposal.md` — §2.4 has the project structure,
§2.16 has the twelve key decisions with their rejected alternatives. See D-001 for
why this file points rather than duplicates.

## Repository map

```
C:\Sharique\Projects\Personal\Quizware\      <- context root (outer git repo)
├── context/                 AI-session continuity. Start at CURRENT.md.
├── docs/
│   ├── new-system/          Design docs 01-06 + README, read in numbered order (~6,000 lines)
│   ├── Implementation-Plan.md              Phased task list, 658 lines, 185 tasks (P0-P17)
│   ├── QuizApp-9AMM-Technical-Analysis.md   Legacy audit (1,492 lines)
│   └── QuickBuzz-Technical-Analysis.md      Legacy audit (123 lines)
├── Quizware/                 NEW system. `[FACT]` Solution scaffolded, 10 projects,
│                            Phases 0–7 of 18 all DONE (see `CURRENT.md` §2):
│   ├── Quizware.slnx
│   ├── src/Quizware.Domain/            no project/NuGet references. Phase 1 domain
│   │                                  model complete: Common/, Enums/, Teams/,
│   │                                  QuestionBank/, Tournament/, Gameplay/,
│   │                                  Scoring/, Qualification/, Buzzer/. Phase 7
│   │                                  added mutators (Rename/Reorder/Update/Delete)
│   │                                  to several entities that were create-only
│   │                                  before (Stage, StageSegmentTemplate,
│   │                                  ScoringRule, QualificationRule, TieBreakRule,
│   │                                  QuestionSelectionRule) plus
│   │                                  Qualification/DefaultTieBreakValues.cs
│   ├── src/Quizware.Application/       -> Domain. Phase 3: Authorization/ (Policies,
│   │                                  Roles), Common/Behaviors/ValidationBehavior,
│   │                                  Abstractions/ (IClock, ICurrentUser,
│   │                                  ICurrentProgram, IAppDbContext [Phase 6a]).
│   │                                  Phase 6: Programs/, Teams/, Topics/, Tags/,
│   │                                  Media/, QuestionBank/ (one folder per area,
│   │                                  MediatR commands/queries — see D-019 for
│   │                                  when this pattern applies vs. controller-direct).
│   │                                  Phase 7: Tournament/ (Stage/segment CRUD +
│   │                                  reorder), Rules/ (scoring/selection/
│   │                                  qualification/tie-break upsert + reset-
│   │                                  defaults + IRuleService resolution)
│   ├── src/Quizware.Infrastructure/    -> Application, Domain. Phase 3: Identity/
│   │                                  (JWT, AppUser/AppRole, RefreshToken),
│   │                                  Idempotency/. Phase 4: Persistence/
│   │                                  (AppDbContext, ~58-table EF model,
│   │                                  Configurations/, Interceptors/), Auditing/,
│   │                                  Outbox/, Imports/ (Phase 6c/6f Excel import
│   │                                  parsers). Phase 6e: Media/ (LocalFileStorage).
│   │                                  Phase 7: Persistence/TournamentSeeder.cs
│   │                                  (18-team demo tournament, dev-only seed)
│   ├── src/Quizware.Modules.Buzzer/    -> Application, Domain (optional module).
│   │                                  Phase 4: Manual/ (BuzzSession, BuzzPress,
│   │                                  BuzzDeviceMapping) + own EF configs, kept out
│   │                                  of Infrastructure's direct references
│   ├── src/Quizware.Api/               -> all four above. Phase 3: Middleware/,
│   │                                  Filters/. Phase 5: Contracts/V1/ (DTOs per
│   │                                  area, discriminated-union QuestionResponse),
│   │                                  Controllers/v1/ (16 controllers). Phases 6–7
│   │                                  turned most of those from 501 stubs into
│   │                                  real handlers — only Phase 8+ areas (Matches,
│   │                                  Live engine, Scores, Standings, Qualification
│   │                                  commit, Buzzer, Display, Reports) remain stubs.
│   ├── tests/Quizware.Domain.Tests/    xUnit + FluentAssertions; 95 tests
│   ├── tests/Quizware.Application.Tests/  17 tests (Phase 6/7 growth from 4)
│   ├── tests/Quizware.Architecture.Tests/ 4 tests (NetArchTest dependency rules)
│   ├── tests/Quizware.Api.IntegrationTests/ 122 tests `[UNVERIFIED]` this checkpoint,
│   │                                  see `CURRENT.md` V-009 (persistence,
│   │                                  auth/health, validators, controller-stub
│   │                                  reachability, Teams/Topics/Tags/Media/
│   │                                  QuestionBank/Stages/Rules endpoint tests)
│   ├── clients/typescript/quizapp-api-client.ts  generated via NSwag (D-018),
│   │                                  ~19,400 lines, committed (not regenerated
│   │                                  since Phase 5 — stale relative to Phase 6/7
│   │                                  endpoints if the Angular front end needs it)
│   ├── postman/Quizware.postman_collection.json + README.md  Phase 7 checkpoint:
│   │                                  80 requests, 10 folders, covers every
│   │                                  implemented endpoint through Phase 7;
│   │                                  staged, not committed as of this checkpoint
│   └── tools/Quizware.BuzzerAgent/     console tray app; no project refs yet (T-006)
├── QuizApp-9AMM/            LEGACY MVC 4. Nested git repo.
├── QuickBuzz/               LEGACY buzzer. Nested git repo.
├── AGENTS.md                Cross-tool AI instructions
├── CLAUDE.md                Claude Code entry point
└── .claude/, .cursor/       Tool adapters for the context system
```

`[UNVERIFIED]` as of S-2026-09-08-02 (see `CURRENT.md` V-009 — not
independently re-run this checkpoint, build attempt was file-locked by a
running dev-server process): `dotnet build` reportedly 0 warnings/errors,
`dotnet test` reportedly **238 passed, 0 failed** across all four test
projects (95 Domain, 17 Application, 4 Architecture, 122
Api.IntegrationTests). Phases 0–7 of the 18-phase plan are all marked DONE.
See `CURRENT.md` §2–3 for the phase-by-phase breakdown and what's committed
vs. staged vs. gitignored-by-construction (D-016).

**Nested git repositories.** `QuizApp-9AMM/` and `QuickBuzz/` each contain their
own `.git`. The outer repository at the path above is the context root and owns
the new work. `[FACT]` verified by `find -type d -name .git`.

## Tech stack

| Layer | Choice | Status |
|---|---|---|
| Runtime | .NET 10 | `[DECIDED]`; `[FACT]` SDK 10.0.400 installed, solution scaffolded and builds clean |
| API | ASP.NET Core Web API | `[DECIDED]` |
| Data | SQL Server, EF Core 10, code-first migrations | `[DECIDED]` (EDMX rejected) |
| Live updates | SignalR | `[DECIDED]` (AJAX polling rejected) |
| Front end | Angular 22 | `[DECIDED]`, separate track, Phase 17 |
| Legacy | ASP.NET MVC 4, EDMX | being replaced |

## Environment

- `[FACT]` Windows 10 Pro 10.0.19045, PowerShell 5.1 primary; Git Bash available.
- `[FACT]` Context root: `C:\Sharique\Projects\Personal\Quizware`.
- `[FACT]` Outer repo is on branch `master`. As of S-2026-09-08-02, `git log`
  shows 19 commits, most recent: `3dbc6f2` (Phase 7: Stage/segment CRUD +
  reorder, scoring/selection/qualification/tie-break rule management,
  IRuleService, program readiness validation, 18-team seed script) ·
  `783bc1c` (Phase 6e/6f: media upload + question bank CRUD) · `1d86810`
  (6d: Topics/Tags) · `41e45bd` (6c: Teams) · `ae94bca` (6b: Users/roles) ·
  `717b96f` (6a: Programs) · `8294c27` (Phase 5: API contract) · `c624f51`
  (Phase 4: full business schema) · `4452949` (Phase 3: Identity/JWT/DI) ·
  `0df2bd2` (docs updated, `docs/` moved to fully gitignored — D-016) ·
  `6e6a13e`/`4697df7`/`d6171cd`/`2b2bcfb` (Phase 1 domain model) · `471a60a`
  (.gitignore + `CLAUDE.md` commit-practice instructions) · `893c4a7`
  (solution scaffolding) · `bf8cb06` (initial commit). `main` is the intended
  base branch for PRs but does not exist. **As of this checkpoint, only the
  Postman collection (`Quizware/postman/`) and 3 rule-handler bugfixes +
  their regression test are staged but not committed** — see `TASKS.md`
  T-018. Everything else through Phase 7 is committed (see `CURRENT.md` §2's
  correction — a brief's "still uncommitted" claim has now gone stale by
  save time three separate times, L-004). Phase 2's 10 ADRs in `docs/adr/`
  are on disk but will never show in `git log` (`docs/` is gitignored,
  D-016) — this is expected, not a pending-commit gap.
- `[FACT]` .NET 10 SDK **10.0.400** installed locally, alongside 8.0.421, both
  under `C:\Program Files\dotnet\sdk`. Verified via `dotnet --list-sdks`.
- `[UNVERIFIED]` SQL Server instance available for development. Check: connection
  string in a future `appsettings.Development.json`.
- `[DECIDED]` Deployment target is an on-premises venue server, no cloud
  dependency (D-014). Buzzer device count is configurable, default 3 (D-014).

## Commands

`[FACT]` Verified working from `Quizware/` (the solution root — note the same
last-path-segment name as the repo root, `Quizware\Quizware\`, is easy to fumble in
`cd`):

```bash
dotnet sln Quizware.slnx list       # list the 10 projects
dotnet build Quizware.slnx          # 0 warnings, 0 errors last independently verified S-2026-09-08-01
dotnet test Quizware.slnx --no-build  # 238 passed, 0 failed reported S-2026-09-08-02, [UNVERIFIED] this checkpoint (V-009)
dotnet list <project> reference    # verify a project's dependency edges
npx --yes newman run Quizware/postman/Quizware.postman_collection.json  # requires `dotnet run` already active; see postman/README.md for folder order
```

`[FACT]` EF Core migrations exist: `InitialIdentitySchema` (Phase 3) and
`AddBusinessSchema` (Phase 4, ~58 tables total). Integration tests run against
an in-process SQLite database, not real SQL Server (no Docker daemon in this
environment) — see `TASKS.md` V-006/V-007 for what that leaves unverified.

```bash
git status --short          # outer repo; nested repos report separately
dotnet --list-sdks          # confirmed: 8.0.421 and 10.0.400
```

## Conventions and user preferences

- `[FACT]` The user works across multiple AI assistants and accounts — two Claude
  accounts, Codex, Cursor, and others — and switches between them mid-project.
  Anything durable must be plain Markdown in the repository, never tool-specific
  state. This is the founding constraint of `context/` (D-002).
- `[FACT]` Design documents deliberately separate **confirmed findings** from
  **recommendations**, and assumptions are listed explicitly rather than hidden
  (`docs/new-system/README.md`, `06-Development-Roadmap.md` §6.5). The context
  system mirrors this with its confidence tags — see D-004.
- `[FACT]` The user asked for existing conventions to be inspected before adding
  structure, rather than a new structure being imposed. Applies to future work too.
- `[DECIDED]` Commits are made only when the user asks (V-005, promoted from
  `[ASSUMED]` in S-2026-09-04-03). `[FACT]` `CLAUDE.md` now has an explicit
  "Instructions" section, written by the user directly: never commit unasked;
  always present plans in phases, each followed by a single-line commit
  message; separate commit messages for API vs Angular changes; every plan
  states What/Why/Where/Affects; and — added this session — always read
  `context/CURRENT.md` first and keep the rest of `context/` in mind for every
  decision during the session, not only at the start. This assistant proposes
  commit messages; the user runs `git commit`. See `TASKS.md` V-005 for the
  git-log evidence this pattern is actually being followed, and L-004 for a
  related pitfall (a brief's claim about commit state can be stale).
- `[FACT]` The user makes incremental, narrow requests rather than specifying
  everything up front (e.g. narrowing the Judge role's authority across four
  separate messages before asking for full removal, D-013). Expect more of this
  style; it is not a sign a decision is unstable, just how it arrives.
- `[FACT]` The user reads output carefully enough to catch inconsistencies an
  agent introduced (e.g. a stray leftover role checkmark in an intermediate
  pass). Thoroughness matters more than speed to this user.
- `[FACT]` **"Configurable" means "configurable with a sensible default,"** not
  "configurable with no default." Observed twice: tie-break format defaults to
  MCQ but is fully configurable (D-011); buzzer device count defaults to 3 but is
  configurable (D-014). Apply this pattern when a future request says
  "configurable" without specifying a default.
- `[FACT]` Domain exceptions were introduced incrementally during Phase 1 — added
  when the entity that needed them was built (e.g. `InsufficientParticipantsException`
  appeared with `Match.Start`, `SegmentNotReorderableException` with
  `MatchSegment.Reorder`) rather than all stubbed out upfront under the
  P1-14 "exceptions" task. Worked well: every exception in the codebase has a
  real caller from the moment it exists. Treat this as the default pattern for
  future phases too, not just something that happened to occur this once.
- `[DECIDED]` **All of `docs/` is gitignored** — design docs, the Implementation
  Plan, and the ADRs are never tracked by the outer git repo (D-016). This was
  made out-of-band; the reasoning isn't recorded, only the behavior. Treat the
  on-disk content of `docs/` as authoritative regardless of what `git log`
  shows — it will show nothing for `docs/`, ever.
- `[DECIDED]` **Polymorphic API response types need explicit Swagger wiring**
  (D-017): `ActionResult<TResponse>` return types plus explicit
  `SelectSubTypesUsing`, because Swashbuckle 10.x does not read
  `[JsonPolymorphic]`/`[JsonDerivedType]` on its own (L-005).
- `[UNVERIFIED]` Large, cross-referenced documentation edits were made via
  targeted scripts (read file, assert exact old text present, replace, write)
  rather than an editor's diff-style edit tool, because the design docs are large
  (400-2000 lines) and cross-referenced. Worked well for that session; not
  established as a standing preference to reach for by default.

## External systems

- **Buzzer hardware** — serial/COM devices, today owned by QuickBuzz. New design
  puts them behind an `IBuzzerProvider` port with `Null` (default), `HttpAgent`
  (recommended) and `Serial` adapters. The system must complete every match with
  the buzzer module deleted entirely. `[DECIDED]` Device count is configurable via
  a `DeviceCount` setting, default 3 — today's hardware count, not a hard limit
  (D-014).
- `[DECIDED]` No cloud dependency — hosting is a local/on-premises venue server
  (D-014). The buzzer agent still pushes outward to the API over HTTP even though
  everything is local, since the operator PC's exact network position relative to
  the server is not guaranteed.
