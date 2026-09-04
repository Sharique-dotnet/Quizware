# PROJECT — QuizApp

Slow-changing orientation. Read `CURRENT.md` first; come here when you need the
stack, the map, or the conventions.

**Last updated:** 2026-09-04 (S-2026-09-04-01)

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
| **QuizApp** | The **new** system. Solution `QuizApp/QuizApp.slnx`. Projects will be `QuizApp.Domain`, `.Application`, `.Infrastructure`, `.Api`. |
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
│   ├── new-system/          Design docs 01-06, read in numbered order (~5.5k lines)
│   ├── QuizApp-9AMM-Technical-Analysis.md   Legacy audit (1,492 lines)
│   └── QuickBuzz-Technical-Analysis.md      Legacy audit (123 lines)
├── QuizApp/                 NEW system. Currently only QuizApp.slnx: "<Solution />"
├── QuizApp-9AMM/            LEGACY MVC 4. Nested git repo.
├── QuickBuzz/               LEGACY buzzer. Nested git repo.
├── AGENTS.md                Cross-tool AI instructions
├── CLAUDE.md                Claude Code entry point
└── .claude/, .cursor/       Tool adapters for the context system
```

**Nested git repositories.** `QuizApp-9AMM/` and `QuickBuzz/` each contain their
own `.git`. The outer repository at the path above is the context root and owns
the new work. `[FACT]` verified by `find -type d -name .git`.

## Tech stack

| Layer | Choice | Status |
|---|---|---|
| Runtime | .NET 10 | `[DECIDED]` per design docs, no code yet |
| API | ASP.NET Core Web API | `[DECIDED]` |
| Data | SQL Server, EF Core 10, code-first migrations | `[DECIDED]` (EDMX rejected) |
| Live updates | SignalR | `[DECIDED]` (AJAX polling rejected) |
| Front end | Angular 22 | `[DECIDED]`, separate track, Phase 17 |
| Legacy | ASP.NET MVC 4, EDMX | being replaced |

## Environment

- `[FACT]` Windows 10 Pro 10.0.19045, PowerShell 5.1 primary; Git Bash available.
- `[FACT]` Context root: `C:\Sharique\Projects\Personal\QuizApp`.
- `[FACT]` Outer repo is on branch `master` with **no commits yet**; `main` is the
  intended base branch for PRs.
- `[UNVERIFIED]` .NET 10 SDK installed locally. Check: `dotnet --list-sdks`.
- `[UNVERIFIED]` SQL Server instance available for development. Check: connection
  string in a future `appsettings.Development.json`.
- `[ASSUMED]` Deployment target is an on-premises venue server, no cloud
  dependency (design docs §6.5 Q11 records this as an assumption, not a
  confirmation). Confirm: ask the user.

## Commands

No build yet — `QuizApp.slnx` is an empty solution.

```bash
git status --short          # outer repo; nested repos report separately
dotnet --list-sdks          # verify the .NET 10 SDK before Phase 3
```

Once the skeleton exists (Phase 3), expected: `dotnet build`, `dotnet test`,
`dotnet ef migrations add <Name> -p src/QuizApp.Infrastructure -s src/QuizApp.Api`.
`[UNVERIFIED]` — record the real commands here the first time they are run.

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
- `[ASSUMED]` Commits are made only when the user asks. Confirm: ask before the
  first commit.

## External systems

- **Buzzer hardware** — serial/COM devices, today owned by QuickBuzz. New design
  puts them behind an `IBuzzerProvider` port with `Null` (default), `HttpAgent`
  (recommended) and `Serial` adapters. The system must complete every match with
  the buzzer module deleted entirely.
- `[UNVERIFIED]` No other external services identified. The design assumes no
  cloud dependency.
