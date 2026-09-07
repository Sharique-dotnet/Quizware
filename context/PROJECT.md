# PROJECT — QuizApp

Slow-changing orientation. Read `CURRENT.md` first; come here when you need the
stack, the map, or the conventions.

**Last updated:** 2026-09-07 (S-2026-09-07-01)

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
| **QuizApp** | The **new** system. Solution `QuizApp/QuizApp.slnx`. `[FACT]` 10 projects exist: `src/QuizApp.{Domain,Application,Infrastructure,Modules.Buzzer,Api}`, `tests/QuizApp.{Domain,Application}.Tests`, `tests/QuizApp.Api.IntegrationTests`, `tests/QuizApp.Architecture.Tests`, `tools/QuizApp.BuzzerAgent`. |
| **QuizApp-9AMM** | The **existing** ASP.NET MVC 4 system being replaced. Read-only reference. |
| **QuickBuzz** | The **existing** buzzer application, to be absorbed as the optional `QuizApp.Modules.Buzzer`. |

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
C:\Sharique\Projects\Personal\QuizApp\      <- context root (outer git repo)
├── context/                 AI-session continuity. Start at CURRENT.md.
├── docs/
│   ├── new-system/          Design docs 01-06 + README, read in numbered order (~6,000 lines)
│   ├── Implementation-Plan.md              Phased task list, 658 lines, 185 tasks (P0-P17)
│   ├── QuizApp-9AMM-Technical-Analysis.md   Legacy audit (1,492 lines)
│   └── QuickBuzz-Technical-Analysis.md      Legacy audit (123 lines)
├── QuizApp/                 NEW system. `[FACT]` Solution scaffolded, 10 projects,
│                            Phases 0–5 of 18 all DONE (see `CURRENT.md` §2):
│   ├── QuizApp.slnx
│   ├── src/QuizApp.Domain/            no project/NuGet references. Phase 1 domain
│   │                                  model complete: Common/, Enums/, Teams/,
│   │                                  QuestionBank/, Tournament/, Gameplay/,
│   │                                  Scoring/, Qualification/, Buzzer/
│   ├── src/QuizApp.Application/       -> Domain. Phase 3: Authorization/ (Policies,
│   │                                  Roles), Common/Behaviors/ValidationBehavior,
│   │                                  Abstractions/ (IClock, ICurrentUser,
│   │                                  ICurrentProgram)
│   ├── src/QuizApp.Infrastructure/    -> Application, Domain. Phase 3: Identity/
│   │                                  (JWT, AppUser/AppRole, RefreshToken),
│   │                                  Idempotency/. Phase 4: Persistence/
│   │                                  (AppDbContext, ~58-table EF model,
│   │                                  Configurations/, Interceptors/), Auditing/,
│   │                                  Outbox/, Imports/
│   ├── src/QuizApp.Modules.Buzzer/    -> Application, Domain (optional module).
│   │                                  Phase 4: Manual/ (BuzzSession, BuzzPress,
│   │                                  BuzzDeviceMapping) + own EF configs, kept out
│   │                                  of Infrastructure's direct references
│   ├── src/QuizApp.Api/               -> all four above. Phase 3: Middleware/,
│   │                                  Filters/. Phase 5: Contracts/V1/ (DTOs per
│   │                                  area, discriminated-union QuestionResponse),
│   │                                  Controllers/v1/ (16 controllers, all 501 stubs)
│   ├── tests/QuizApp.Domain.Tests/    xUnit + FluentAssertions; 95 tests
│   ├── tests/QuizApp.Application.Tests/  4 tests
│   ├── tests/QuizApp.Architecture.Tests/ 4 tests (NetArchTest dependency rules)
│   ├── tests/QuizApp.Api.IntegrationTests/ 44 tests (persistence, auth/health,
│   │                                  validators, controller-stub reachability)
│   ├── clients/typescript/quizapp-api-client.ts  generated via NSwag (D-018),
│   │                                  ~19,400 lines, committed
│   └── tools/QuizApp.BuzzerAgent/     console tray app; no project refs yet (T-006)
├── QuizApp-9AMM/            LEGACY MVC 4. Nested git repo.
├── QuickBuzz/               LEGACY buzzer. Nested git repo.
├── AGENTS.md                Cross-tool AI instructions
├── CLAUDE.md                Claude Code entry point
└── .claude/, .cursor/       Tool adapters for the context system
```

`[FACT]` As of S-2026-09-07-01: `dotnet build` → 0 warnings/errors, `dotnet test`
→ **147 passed, 0 failed** across all four test projects. Phases 0–5 of the
18-phase plan are all marked DONE in `docs/Implementation-Plan.md`. See
`CURRENT.md` §2–3 for the phase-by-phase breakdown and what's committed vs.
staged vs. gitignored-by-construction (D-016).

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
- `[FACT]` Context root: `C:\Sharique\Projects\Personal\QuizApp`.
- `[FACT]` Outer repo is on branch `master`. As of S-2026-09-07-01, `git log`
  shows 12 commits: `bf8cb06` (initial commit, docs + context system) ·
  `893c4a7` (solution scaffolding) · `471a60a` (.gitignore + `CLAUDE.md`
  commit-practice instructions) · `2b2bcfb` (P1-02, `TurnOrderCalculator`) ·
  `d6171cd` (Common base interfaces + remaining enums) · `4697df7` (Common
  interfaces, enums, `Program` aggregate) · `6e6a13e` (full domain model) ·
  `0df2bd2` (docs updated, and `docs/` moved from tracked to fully gitignored —
  see D-016) · `4452949` (Phase 3: Identity, JWT, DI, logging, tests) ·
  `c624f51` (Phase 4: full business schema, global query filters, audit
  interceptors, pluggable buzzer persistence). `main` is the intended base
  branch for PRs but does not exist. **Phase 5's ~20 files (contracts,
  controllers, TS client) are staged but not committed** as of this session —
  see `TASKS.md` T-013. Phase 2's 10 ADRs in `docs/adr/` are on disk but will
  never show in `git log` (`docs/` is gitignored, D-016) — this is expected,
  not a pending-commit gap.
- `[FACT]` .NET 10 SDK **10.0.400** installed locally, alongside 8.0.421, both
  under `C:\Program Files\dotnet\sdk`. Verified via `dotnet --list-sdks`.
- `[UNVERIFIED]` SQL Server instance available for development. Check: connection
  string in a future `appsettings.Development.json`.
- `[DECIDED]` Deployment target is an on-premises venue server, no cloud
  dependency (D-014). Buzzer device count is configurable, default 3 (D-014).

## Commands

`[FACT]` Verified working from `QuizApp/` (the solution root — note the same
last-path-segment name as the repo root, `QuizApp\QuizApp\`, is easy to fumble in
`cd`):

```bash
dotnet sln QuizApp.slnx list       # list the 10 projects
dotnet build QuizApp.slnx          # 0 warnings, 0 errors as of S-2026-09-07-01
dotnet test QuizApp.slnx --no-build  # 147 passed, 0 failed as of S-2026-09-07-01
dotnet list <project> reference    # verify a project's dependency edges
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
