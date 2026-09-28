# Quizware

Quizware is a quiz-tournament management system for inter-school quiz
competitions. It runs the whole event: registering teams, building a question
bank, planning stages and matches, operating each match live, scoring, and
working out standings, qualification and tie-breaks.

It replaces **QuizApp-9AMM**, an ASP.NET MVC 4 application that has run the
competition in production for years. The rebuild is an ASP.NET Core Web API on
.NET 10 with SQL Server. An Angular 22 front end will follow as a separate
track.

> **Status:** back end in active development. Phases 0–10 of the
> [implementation plan](docs/Implementation-Plan.md) are done, which covers
> configuration, the question bank, the live match engine, and scoring and
> standings. **Phase 11 (qualification and tie-breaking) is next.**

---

## Contents

- [Why a rebuild](#why-a-rebuild)
- [What the system does](#what-the-system-does)
- [How a tournament is modelled](#how-a-tournament-is-modelled)
- [Architecture](#architecture)
- [Tech stack](#tech-stack)
- [Repository layout](#repository-layout)
- [Getting started](#getting-started)
- [Testing](#testing)
- [API overview](#api-overview)
- [Roles and permissions](#roles-and-permissions)
- [Roadmap and status](#roadmap-and-status)
- [Documentation](#documentation)
- [Working on this repository](#working-on-this-repository)
- [License](#license)

---

## Why a rebuild

The legacy system works, but the tournament is baked into its code:

- **A new database for every event.** Nothing carries over from one year to the
  next, and you can't compare results across years.
- **Turn order written as `QuestionNumber % 3`** in more than 30 places, so it
  only works with exactly three teams per match.
- **The running order of question types is hard-coded** as redirect chains
  across 108 views. Changing the order of rounds means editing code.
- **Tie-breaks are a separate `TieBreaker` table and set of screens** that have
  no connection to the qualification logic.

Quizware turns all of this into **configuration data**. The number of teams per
match, the rounds in a stage and their order, how points are awarded, how
questions are chosen, who qualifies and how ties are broken are all set per
program. None of them are in the code.

The old buzzer app, **QuickBuzz**, becomes an optional module. The system has to
run a complete tournament **without any buzzer hardware**, with the operator
recording answers manually.

## What the system does

| Area | Capabilities |
|---|---|
| **Programs** | One program per event (e.g. one year's competition), holding all of its data. Clone a program's configuration into next year's. Program settings, enabled question formats, a dashboard. |
| **Users and access** | JWT login with refresh tokens, program-scoped tokens, read-only display tokens, 7 roles (see [Roles](#roles-and-permissions)). |
| **Teams** | Register and edit teams, change status, team images, history, and bulk import from Excel. |
| **Question bank** | 10 question formats (below), topics and tags, media upload, versioned edits, approval and retirement, usage and coverage reports, duplicate detection, and MCQ import from Excel. |
| **Tournament configuration** | Stages (League, Semi-Final, Final…), segment templates (the rounds in each match, with locked or reorderable positions), and scoring, selection, qualification and tie-break rules. There is also a readiness check that reports what still blocks a stage or program from running. |
| **Question selection** | A seeded, reproducible draw: a difficulty mix, topic spread and repeat policy per stage, favouring less-used questions, with a fallback ladder when the pool is too small. Questions are reserved per match so two matches never draw the same one. |
| **Match setup** | Create matches manually or auto-seed them (random, by rank, or snake order). Assign seats and turn order, add, remove or reorder segments, and run a ready check before start. |
| **Live match engine** | Start, pause, resume, end or abandon a match. Open, close or skip segments, serve, reveal or skip questions, record answers (safe to retry via an `Idempotency-Key`), pass questions, run the Choice round's topic picks, handle sudden death, and disqualify or reinstate a team. A full event timeline is kept. The live state is read from the database every time, so a restarted console or API resumes exactly where it stopped. |
| **Scoring** | Every point comes from a scoring rule and is written to an **append-only score ledger**. Match and stage totals update in the same transaction. Reversals and manual adjustments are audited ledger entries, and totals can be rebuilt from the ledger at any time. |
| **Standings** | Overall, per-stage and per-team standings, plus a tie-break criteria service (total score, fewer incorrect, more correct at high difficulty, faster average buzz time, head-to-head). |
| **Notifications** | Match start, answers, disqualifications and match end are written to a **transactional outbox**. They will be delivered live over SignalR in Phase 12. |

**Question formats:** MCQ, Buzzer, Passing, Card, Choice, Rapid Fire, Visual
Rapid Fire, Sequence, Audio-Visual and Tie-Breaker. Each has its own handler
for how it is presented, answered and scored.

**Game rules the engine enforces**, all driven by configuration:

- **Turns:** turn order rotates within each segment. In Buzzer and Rapid Fire
  rounds, any team may answer.
- **Passing:** a question passes clockwise or anticlockwise by seat, up to the
  question's pass limit. If every team passes, the answer can be revealed.
- **Choice round:** a team picks a topic from a board, and an exclusive topic is
  removed once it has been played.
- **Sudden death:** the segment ends as soon as one team leads after an equal
  number of turns.
- **Disqualification:** a disqualified team is removed without inventing answers
  for it. Turn order closes up around it and the match carries on.

## How a tournament is modelled

```
Program (the event)
 ├── Teams, Topics, Tags, Media, Questions (the question bank)
 ├── Rules: scoring · question selection · qualification · tie-break
 └── Stages (League → Semi-Final → Final …)
      ├── Segment templates (the rounds: format, question count, locked or reorderable)
      └── Matches
           ├── Participants (team, seat, turn order)
           └── Segments (copied from the templates)
                └── Match questions → Answer records → Score events
```

The development seeder recreates the legacy 18-team tournament purely as
configuration data. That proves the model can express the tournament the old
system hard-coded.

## Architecture

A **modular monolith** built with **Clean Architecture**. The main decisions
are recorded as ADRs in [`docs/adr/`](docs/adr):

| Decision | Summary |
|---|---|
| [ADR-001](docs/adr/ADR-001-modular-monolith.md) | Modular monolith: one deployable, with enforced module boundaries. |
| [ADR-002](docs/adr/ADR-002-multi-tenancy.md) | Shared schema, with every row owned by a `ProgramId`. Enforced by EF Core query filters and request middleware. |
| [ADR-003](docs/adr/ADR-003-question-storage-tpt.md) | Questions stored as Table-Per-Type: one table per format. |
| [ADR-004](docs/adr/ADR-004-event-sourced-scoring.md) | Scoring is event-sourced: a score-event ledger, with the totals derived from it. |
| [ADR-005](docs/adr/ADR-005-buzzer-port-null-default.md) | The buzzer sits behind a port and defaults to "no buzzer". |
| [ADR-006](docs/adr/ADR-006-signalr-with-outbox.md) | Live updates go through SignalR, fed by a transactional outbox. |
| [ADR-007](docs/adr/ADR-007-tie-break-as-ordinary-match.md) | A tie-break is an ordinary match run by the same engine. |
| [ADR-008](docs/adr/ADR-008-local-hosting.md) | Hosted on premises at the venue, with no cloud dependency. |
| [ADR-009](docs/adr/ADR-009-authorisation-model.md) | Role- and policy-based authorisation, scoped to a program. |
| [ADR-010](docs/adr/ADR-010-module-boundaries.md) | Module boundaries and dependency rules. |

**Layers**, with dependencies pointing inwards (checked by the architecture tests):

| Project | Responsibility |
|---|---|
| `Quizware.Domain` | Entities, value objects and business rules. No framework dependencies. |
| `Quizware.Application` | Use cases as MediatR commands and queries, FluentValidation, the match engine, scoring, question selection, and ports such as `IMatchNotifications`. Cannot reference Infrastructure. |
| `Quizware.Infrastructure` | EF Core `AppDbContext`, migrations, ASP.NET Identity, JWT, the outbox, seeders and file storage. |
| `Quizware.Api` | Controllers, versioned request/response contracts (`/api/v1`), middleware for correlation ids, program scope and errors, and the OpenAPI document. |
| `Quizware.Modules.Buzzer` | The optional QuickBuzz integration module. |
| `tools/Quizware.BuzzerAgent` | An on-site agent that bridges the buzzer hardware to the API (Phase 14). |

## Tech stack

- **.NET 10** / ASP.NET Core Web API, C#
- **EF Core 10** with SQL Server (code-first migrations)
- **ASP.NET Core Identity** with JWT bearer tokens
- **MediatR**, **FluentValidation**
- **Serilog** structured logging, health checks at `/health/live` and `/health/ready`
- **Swashbuckle** for the OpenAPI document, **Scalar** for the interactive API reference
- **ClosedXML** for Excel import
- **NSwag**-generated TypeScript client for the future Angular app
- **xUnit** and FluentAssertions. The integration tests run against SQLite through `WebApplicationFactory`.
- **GitHub Actions** CI (build and test on every push and pull request to `master`)

## Repository layout

| Path | What it is |
|---|---|
| [`Quizware/`](Quizware) | **The new system.** Solution `Quizware.slnx`. |
| `Quizware/src/` | Domain, Application, Infrastructure, Api and Modules.Buzzer projects. |
| `Quizware/tests/` | Domain, Application, Architecture and API integration tests. |
| `Quizware/tools/Quizware.BuzzerAgent/` | On-site buzzer bridge (Phase 14). |
| `Quizware/clients/typescript/` | Generated TypeScript API client. |
| `Quizware/docs/openapi.v1.json` | The generated OpenAPI document. |
| [`Quizware/postman/`](Quizware/postman) | Postman collection covering every implemented endpoint, and how to run it. |
| `Quizware/docker-compose.yml` | Local SQL Server 2022 for development. |
| [`docs/new-system/`](docs/new-system) | Design documents 01–06, meant to be read in order. |
| [`docs/Implementation-Plan.md`](docs/Implementation-Plan.md) | The phase-by-phase build plan, with task ids and acceptance criteria. |
| [`docs/adr/`](docs/adr) | Architecture decision records. |
| `docs/*-Technical-Analysis.md` | Line-by-line audits of the two legacy systems. |
| [`context/`](context) | Shared project memory that lets work continue across AI tools and sessions. Start at `context/CURRENT.md`. |

> **Naming trap:** `Quizware` (the new system), `QuizApp-9AMM` (the legacy MVC 4
> app) and `QuickBuzz` (the legacy buzzer app) are three different systems. The
> two legacy apps are separate git repositories that may sit next to
> `Quizware/` on a developer's machine. They are not part of this repository's
> history.

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server: LocalDB on Windows, or the Docker container below
- Optional: Docker, Node.js (to run the Postman collection with `newman`)

### 1. Start a database

Either use SQL Server LocalDB (`(localdb)\MSSQLLocalDB`) or start the bundled
container:

```bash
cd Quizware
docker compose up -d        # SQL Server 2022 on localhost:1433
```

The container's development `sa` password is in `docker-compose.yml`.

### 2. Configure the API

`appsettings.json` only contains `CHANGE_ME` placeholders. Put your local values
in `src/Quizware.Api/appsettings.Development.json`, which git ignores, or in
environment variables:

| Setting | Environment variable |
|---|---|
| `ConnectionStrings:Default` | `ConnectionStrings__Default` |
| `Jwt:Issuer` | `Jwt__Issuer` |
| `Jwt:Audience` | `Jwt__Audience` |
| `Jwt:SigningKey` (at least 32 bytes) | `Jwt__SigningKey` |

For example, for LocalDB:

```json
{
  "ConnectionStrings": { "Default": "Server=(localdb)\\MSSQLLocalDB;Database=Quizware-Dev;Trusted_Connection=True;TrustServerCertificate=True" },
  "Jwt": { "Issuer": "quizware-local", "Audience": "quizware-local", "SigningKey": "<a random string of 32+ characters>" }
}
```

### 3. Run it

```bash
cd Quizware/src/Quizware.Api
dotnet run                   # launch profile: http://localhost:5155
```

On startup the API:

- applies all EF Core migrations,
- seeds the roles,
- outside Production only, seeds a SuperAdmin account and the demo 18-team
  tournament.

The seeded admin's email and password are in
`AdminUserSeeder.DefaultEmail` / `DefaultPassword`
(`src/Quizware.Infrastructure/`). **Change that password for any real use.**

In Development you get:

- the interactive API reference (Scalar) at `/scalar`
- the OpenAPI document at `/swagger/v1/swagger.json`
- health checks at `/health/live` and `/health/ready`

## Testing

```bash
cd Quizware
dotnet build Quizware.slnx -c Release /warnaserror
dotnet test  Quizware.slnx -c Release --no-build
```

The suite covers domain rules, application logic, architecture rules (layer
dependencies) and end-to-end API behaviour. The integration tests use SQLite
in-memory, so they need no database.

The **Postman collection** exercises the API against a running instance with a
real database, from login through a complete live match to standings:

```bash
cd Quizware
npx newman run postman/Quizware.postman_collection.json
```

The collection expects the API at `http://localhost:5299`: run with
`ASPNETCORE_URLS=http://localhost:5299`, or edit the `baseUrl` variable. The
three requests that upload Excel or media files need a file attached by hand,
so an unattended run reports those 5 as failures. See
[`Quizware/postman/README.md`](Quizware/postman/README.md).

To check that the model and migrations are in sync:

```bash
cd Quizware
dotnet ef migrations has-pending-model-changes \
  --project src/Quizware.Infrastructure --startup-project src/Quizware.Api
```

## API overview

All endpoints are under `/api/v1`. Most are scoped to a program:
`/api/v1/programs/{programId}/…`. The full contract is in
[`docs/new-system/05-API-Design.md`](docs/new-system/05-API-Design.md) and the
generated `Quizware/docs/openapi.v1.json`.

| Area | Base route |
|---|---|
| Auth | `/auth/login`, `/auth/refresh`, `/auth/logout`, `/auth/me`, `/auth/change-password`, `/auth/select-program`, `/auth/display-token` |
| Programs | `/programs`, `…/formats`, `…/settings`, `…/clone`, `…/dashboard`, `…/validate` |
| Admin | `/admin/users` |
| Teams | `/programs/{id}/teams` (including Excel import) |
| Topics and tags | `/programs/{id}/topics`, `/programs/{id}/tags` |
| Questions | `/programs/{id}/questions` (one create route per format, approve, retire, import, usage, coverage, duplicates, media) |
| Stages | `/programs/{id}/stages` (segment templates, reorder, validate, standings) |
| Rules | `/programs/{id}/rules/scoring`, `…/selection` (with preview), `…/qualification`, `…/tie-break` |
| Matches | `/programs/{id}/matches` (participants, segments, ready check, auto-seed) |
| Live match | `/matches/{id}/live/…` (state, start, pause, segments, questions, answers, pass, topics, disqualify, end, abandon, timeline) |
| Scores | `/matches/{id}/scores` (events, adjust, recalculate) |
| Standings | `/programs/{id}/standings/overall`, `…/stages/{stageId}`, `…/teams/{teamId}` |

Endpoints for later phases already exist as `501 Not Implemented` stubs so the
contract is fixed up front: qualification, buzzer, display, reports, match
preflight, and live snapshot/restore.

## Roles and permissions

| Role | Can |
|---|---|
| **SuperAdmin** | Everything, across all programs. |
| **ProgramAdmin** | Manage a program's configuration and matches. Also the only role that can reverse an answer, disqualify a team, adjust or recalculate scores, and resolve tie-breaks manually. |
| **QuestionAuthor** | Write and maintain questions. |
| **Operator** | Run live matches: segments, questions, answers and passes. |
| **Scorer** | Record answers. |
| **Display** | Read-only live view, using a display token that cannot write. |
| **Auditor** | Read-only access to timelines, score ledgers and reports. |

## Roadmap and status

| Phase | Scope | Status |
|---|---|---|
| 0–2 | Requirements, domain model and business rules, architecture decisions | ✅ Done |
| 3 | Project skeleton, cross-cutting concerns | ✅ Done |
| 4 | Database schema and migrations | ✅ Done |
| 5 | API contract (OpenAPI first) | ✅ Done |
| 6 | Configuration modules: programs, users, teams, topics, media, question bank | ✅ Done |
| 7 | Tournament configuration: stages, segments, rules | ✅ Done |
| 8 | Question selection engine | ✅ Done |
| 9 | Match engine | ✅ Done |
| 10 | Scoring and standings | ✅ Done |
| 11 | Qualification and tie-breaking | ⏭️ Next |
| 12 | Live push (SignalR) and display API | Planned |
| 13 | Reporting and exports | Planned |
| 14 | QuickBuzz integration module | Planned (deliberately last) |
| 15 | Hardening | Planned |
| 16 | Data migration from QuizApp-9AMM | Optional |
| 17 | Angular 22 front end | Separate track |

The detailed tasks and acceptance criteria for each phase are in
[`docs/Implementation-Plan.md`](docs/Implementation-Plan.md). The current
working state is in [`context/CURRENT.md`](context/CURRENT.md).

## Documentation

Read the design documents in order:

1. [Analysis findings](docs/new-system/01-Analysis-Findings.md): what the legacy systems do and where they break.
2. [Architecture proposal](docs/new-system/02-Architecture-Proposal.md): the target design and the alternatives that were rejected.
3. [PRD](docs/new-system/03-PRD-API.md): product requirements and business rules.
4. [Database schema](docs/new-system/04-Database-Schema.md)
5. [API design](docs/new-system/05-API-Design.md)
6. [Development roadmap](docs/new-system/06-Development-Roadmap.md)

The legacy audits are
[QuizApp-9AMM](docs/QuizApp-9AMM-Technical-Analysis.md) and
[QuickBuzz](docs/QuickBuzz-Technical-Analysis.md).

## Working on this repository

- **Read [`context/CURRENT.md`](context/CURRENT.md) first.** `context/` is the
  project's shared memory across sessions and tools (Claude, Codex, Cursor…).
  It holds decisions and their reasoning, lessons from approaches that failed,
  the task queue and open questions. The rules for AI assistants are in
  [`CLAUDE.md`](CLAUDE.md) and [`AGENTS.md`](AGENTS.md).
- Work phase by phase from the implementation plan. Each phase ends with the
  build and all tests green.
- Commit messages are a single meaningful line. API and Angular changes go in
  separate commits.
- Design documents separate confirmed findings from recommendations. Keep that
  distinction when editing them.

## License

[MIT](LICENSE.txt)
