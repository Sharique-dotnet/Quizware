# CURRENT CONTEXT — QuizApp

> **AI agents: read this file first.** It is the working state of this project as
> of the last checkpoint, written so you can continue without any chat history.
> Full resume procedure: `context/_meta/SPEC.md` §5.
>
> **Confidence tags** — `[FACT]` verified · `[DECIDED]` binding · `[ASSUMED]`
> unconfirmed, adopted to make progress · `[UNVERIFIED]` believed but unchecked ·
> `[OPEN]` unanswered. **Anything `[ASSUMED]` or `[UNVERIFIED]` is not
> established** — verify it, or say plainly that you are proceeding without.
> Where this file and the repository disagree, **the repository is right** and
> this file is stale; say so.

**Last updated:** 2026-09-07 · **Session:** S-2026-09-07-01 · **Saved by:** Claude Sonnet 5 (Claude Code)

---

## 1. What this project is

`[FACT]` A rebuild of a quiz-tournament system. **QuizApp-9AMM** (ASP.NET MVC 4,
in production) is being replaced by **QuizApp** — ASP.NET Core Web API on .NET 10
with SQL Server, an Angular 22 front end later. **QuickBuzz**, the existing buzzer
app, becomes an optional module that the system must work without.

`[FACT]` The core problem being solved: the legacy system hardcodes the tournament.
A new database per event; turn order as `QuestionNumber % 3` in 30+ places; the
running order of question types baked into 108 views as redirect chains; one
`TieBreaker` table/screens completely disconnected from qualification logic. The
new design turns all of it into configuration data.

**Naming trap:** `QuizApp` (new), `QuizApp-9AMM` (legacy MVC 4), and `QuickBuzz`
(legacy buzzer) are three different systems. Check `PROJECT.md` §Domain glossary
before editing, or you will change the wrong one.

## 2. Current objective

`[FACT]` **Phases 0–5 of `docs/Implementation-Plan.md`'s 18 phases are all now
marked DONE on disk**, verified this session by reading the plan file directly
(it carries its own inline "Status: all N tasks done" notes per phase) and by
re-running the full test suite: `dotnet build` → 0 warnings/errors,
`dotnet test` → **147 passed, 0 failed** (95 Domain, 4 Application, 4
Architecture, 44 Api.IntegrationTests).

**This corrects the previous checkpoint (S-2026-09-04-03), which only knew about
Phase 1.** Phases 2 (ADRs), 3 (skeleton/cross-cutting), 4 (schema/migrations) all
happened in the gap between that checkpoint and this one, without a context save
in between — reconstructed this session from `docs/Implementation-Plan.md`'s own
status text and `git log`, not from a conversation brief (the brief handed to
this session only covered Phase 5). Full phase-by-phase breakdown: `TASKS.md`
T-011 through T-013 (Closed).

- **Phase 2 — ADRs, DONE.** 10 ADRs in `docs/adr/` (ADR-001 through ADR-010,
  covering modular monolith, multi-tenancy, TPT questions, event-sourced scoring,
  buzzer-port-null-default, SignalR+outbox, tie-break-as-match, local hosting,
  authorisation model, module boundaries). **Not committed** — see §3 Git row.
- **Phase 3 — skeleton/cross-cutting, DONE.** Identity+JWT, 7 roles/9 policies,
  program-scope middleware, global exception handler (RFC 9457), Serilog +
  correlation IDs, FluentValidation pipeline, idempotency filter, health checks,
  Swagger, Docker Compose, CI workflow. **Committed** as `4452949`.
- **Phase 4 — schema/migrations, DONE.** ~58 tables (49 new + Phase 3's 9
  Identity/token tables) via migration `AddBusinessSchema`, global query filters
  (tenant + soft delete), audit + audit-log interceptors, buzzer tables kept in a
  separate assembly picked up via `IEntityConfigurationAssemblyMarker` so
  Infrastructure never references the buzzer module. **Committed** as `c624f51`.
- **Phase 5 — API contract (OpenAPI first), DONE this session per the brief.**
  16 controllers under `Controllers/v1/` (all stub `501`), per-format request DTOs
  and a `[JsonPolymorphic]`/`[JsonDerivedType]` discriminated-union
  `QuestionResponse` hierarchy, FluentValidation validators, generated TypeScript
  client (~19,400 lines, verified), `.http` collection. **Staged but not
  committed** — verified `[FACT]` via `git status --short` this session (20+
  new/modified files all show as staged `A`/`M`, no commit on top of `c624f51`).

`[DECIDED]` Phase 0 remains resolved as owner-confirmed (D-015) — and this time
the doc text re-sync **did persist**: `docs/Implementation-Plan.md` line 73 now
reads `**DONE (skipped, owner-confirmed)**`, closing the loose end T-008 flagged
last checkpoint.

**What's next: Phase 6 — Configuration modules** (Program management → Users/
roles → Teams → Topics/tags → Media → Question bank), per the plan's "Start
here" section (line ~800). This is the first phase where the Phase 5 stub
controllers get real MediatR command/query handlers behind them in
`QuizApp.Application`.

## 3. State of play

| Area | State |
|---|---|
| Design docs 01–06 + README, legacy analyses, `docs/adr/` (10 ADRs) | `[FACT]` Complete on disk. **All of `docs/` is gitignored** (`.gitignore` line `docs/`) — none of it is tracked by git, deliberately (see D-016). Do not assume "not in `git log`" means "doesn't exist." |
| `docs/Implementation-Plan.md` | `[FACT]` 823 lines on disk (grew from 658), Phases 0–5 marked DONE inline, "Start here" section names Phase 6 next |
| Phase 0 (owner confirmation) | `[DECIDED]` D-015, doc text now in sync (T-008 closed) |
| Phase 1 (domain model) | `[FACT]` Complete, 95 passing tests — unchanged since S-2026-09-04-03 |
| Phase 2 (ADRs) | `[FACT]` Complete, 10 files in `docs/adr/` — **uncommitted by construction** (`docs/` gitignored), not a pending-commit gap |
| Phase 3 (skeleton/cross-cutting) | `[FACT]` Complete, **committed** `4452949`. Identity/JWT, 7 roles, 9 policies, exception handling, Serilog, FluentValidation pipeline, idempotency, health checks, Docker Compose, CI — `docker compose up` and the GitHub Actions run itself are `[UNVERIFIED]` in this dev environment (no Docker daemon, no CI runner here per the plan doc's own text) |
| Phase 4 (schema/migrations) | `[FACT]` Complete, **committed** `c624f51`. Migration `AddBusinessSchema` verified against SQLite in tests, **not yet verified against real SQL Server via Testcontainers** (`[UNVERIFIED]`, no Docker here) |
| Phase 5 (API contract) | `[FACT]` Complete per this session's brief, cross-checked: 16 controllers present, TS client 19,419 lines, full suite 147/147 passing, 0 build warnings. **Staged, not committed** |
| Test suite | `[FACT]` 147 passed / 0 failed, re-run this session (`dotnet test QuizApp.slnx`) |
| Context system | `[FACT]` Built S-2026-09-04-01; this is its 4th real merge, and the first one that had to reconstruct multiple un-saved phases from repo evidence rather than a brief |
| Git (outer repo) | `[FACT]` Branch `master`, 12 commits (`bf8cb06` → `c624f51`, full list `PROJECT.md` §Environment). Phase 5's ~20 files are staged but uncommitted. **Nothing should be assumed committed without checking `git log`/`git status` yourself** — this checkpoint itself only exists because the prior one's phase count was stale (see L-004, and the new finding in §8) |

## 4. Next actions

1. **Start Phase 6 — Configuration modules.** Per the plan: Program management →
   Users/roles → Teams → Topics/tags → Media → Question bank, each sub-phase
   depending on the one before it. This is where `QuizApp.Application` gets real
   MediatR handlers behind the Phase 5 stub controllers for the first time.
2. **Ask the user whether to commit Phase 5's staged work** before starting
   Phase 6 — nothing has been committed since `c624f51` (Phase 4). The commit
   message on offer from this session's brief: "Add API contract:
   request/response DTOs, question format validators, 16 controller stubs, and
   generated TypeScript client". Per standing policy, do not commit without
   being asked.
3. **T-006** (Phase 14, not urgent) — decide whether `QuizApp.BuzzerAgent`
   references `QuizApp.Modules.Buzzer` to reuse serial frame-parsing code, or
   reimplements it standalone.
4. **Verify when possible, not urgent now:** `docker compose up` (Phase 3),
   the GitHub Actions CI run (Phase 3), and the migration against real SQL
   Server via Testcontainers (Phase 4) — all three are `[UNVERIFIED]` because
   this dev environment has no Docker daemon and no CI runner.

Full queue: `TASKS.md`.

## 5. Constraints you must respect

- `[DECIDED]` **Architecture is settled at design level** — modular monolith, Clean
  Architecture, shared schema with `ProgramId` multi-tenancy, Table-Per-Type
  questions (D-009), configuration-driven tournament with configurable segment
  order (D-010), event-sourced scoring, tie-break as an ordinary Match through the
  existing engine (D-011), EF Core 10 code-first, SignalR. Now also formalised as
  10 ADRs in `docs/adr/`. Full list with rejected alternatives:
  `docs/new-system/02-Architecture-Proposal.md` §2.16, plus D-009 – D-018 in
  `DECISIONS.md`. Do not re-open one without reading why the alternative was
  rejected.
- `[DECIDED]` **Questions are Table-Per-Type** (D-009): one shared `Question`
  table plus one child table per format. One route/endpoint per format
  (`POST /questions/mcq`, etc.), not one generic endpoint — now implemented as
  10 stub controller actions in `QuestionsController` plus a discriminated-union
  `QuestionResponse` for reads (D-017).
- `[DECIDED]` **7 roles, no Judge** (D-013), **9 authorization policies**
  (`Policies.cs`) — now actually seeded/enforced as of Phase 3, not just
  designed. ProgramAdmin/SuperAdmin hold sole authority over disqualification,
  answer reversal, score adjustment, tie-break resolution.
- `[DECIDED]` **On-premises hosting, no cloud dependency** (D-014). Buzzer device
  count configurable, default 3.
- `[DECIDED]` **The buzzer must be deletable.** `QuizApp.Modules.Buzzer` sits
  behind an `IBuzzerProvider` port with a `Null` default; its EF entity
  configurations live in a separate assembly Infrastructure never references
  directly (Phase 4's `IEntityConfigurationAssemblyMarker`, D-009/ADR-005). Every
  match must complete with the buzzer module removed entirely.
- `[DECIDED]` **No fake answers, ever.** Disqualifying a team recompacts
  `TurnOrder` and the match continues.
- `[DECIDED]` **`docs/` is entirely gitignored** (D-016) — design docs, the
  Implementation Plan, and the ADRs all live on disk but are **not** tracked by
  the outer git repo. Do not read "not in `git log`" as "doesn't exist" for
  anything under `docs/`.
- `[DECIDED]` **Swashbuckle 10.x needs explicit help to emit polymorphic OpenAPI
  schemas** (D-017): controller actions must return `ActionResult<TResponse>`
  (never bare `IActionResult`) wherever a response DTO exists, and
  `SelectSubTypesUsing` must explicitly enumerate `QuestionResponse`'s 10
  subtypes — `[JsonPolymorphic]`/`[JsonDerivedType]` alone is not picked up.
  Apply this pattern to any future polymorphic response type.
- `[DECIDED]` **NSwag, not openapi-generator-cli, generates the TypeScript
  client** (D-018) — this environment has no JVM, and NSwag is pure .NET.
- `[FACT]` **Design docs separate confirmed findings from recommendations**;
  preserve that distinction when editing them.
- `[FACT]` **Context is plain Markdown in the repo, never tool-specific state**
  (D-002).
- `[DECIDED]` **Commit only when asked** (V-005). The assistant proposes plans in
  phases with a commit message per phase; the user runs `git commit`. **As of
  this session, everything from Phase 5 is staged and uncommitted** — the last
  actual commit is `c624f51` (Phase 4).
- `[DECIDED]` **Phase 0 is owner-confirmed, not a workshop** (D-015).
- `[FACT]` **Domain exceptions are introduced incrementally**, when the entity
  that needs them is built.

Reasoning for all decisions: `DECISIONS.md` D-001 – D-018.

## 6. Files in play

| Path | Note |
|---|---|
| `docs/Implementation-Plan.md` | 823 lines, gitignored, Phases 0–5 marked DONE, "Start here" names Phase 6 next |
| `docs/adr/ADR-001..010-*.md` | Phase 2 deliverable, gitignored, uncommitted by construction |
| `QuizApp/src/QuizApp.Domain/` | Phase 1, unchanged since last checkpoint |
| `QuizApp/src/QuizApp.Infrastructure/Identity/`, `Persistence/` | Phase 3/4: Identity, JWT, `AppDbContext`, 49-table `AddBusinessSchema` migration, interceptors, global filters |
| `QuizApp/src/QuizApp.Modules.Buzzer/Manual/` | Phase 4: buzzer entities + configs, separate assembly, picked up via `IEntityConfigurationAssemblyMarker` |
| `QuizApp/src/QuizApp.Api/Contracts/V1/**` | Phase 5: request/response DTOs, one folder per area; `Questions/QuestionResponses.cs` has the discriminated union; `Questions/Formats/` has the 10 create-request records + validators |
| `QuizApp/src/QuizApp.Api/Controllers/v1/*.cs` | Phase 5: 16 controllers, all actions `StatusCode(501)` typed as `ActionResult<TResponse>` |
| `QuizApp/src/QuizApp.Api/DependencyInjection.cs` | FluentValidation registration, Swagger `UseOneOfForPolymorphism`/`SelectDiscriminatorNameUsing`/`SelectSubTypesUsing` wiring |
| `QuizApp/clients/typescript/quizapp-api-client.ts` | Generated via NSwag, ~19,400 lines, committed to the repo (not gitignored — this is a build artifact the team wants tracked) |
| `QuizApp/src/QuizApp.Api/QuizApp.Api.http` | Hand-written `.http` collection covering all 16 areas + a full login-to-qualification workflow |
| `QuizApp/tests/QuizApp.Api.IntegrationTests/` | 44 tests: persistence (Phase 4), auth/health (Phase 3), validator + controller-stub-reachability (Phase 5) |
| `context/_meta/SPEC.md` | The save/resume procedure |
| `AGENTS.md`, `CLAUDE.md`, `.cursor/rules/` | Tool adapters |

`[FACT]` `QuizApp-9AMM/` and `QuickBuzz/` are **nested git repositories**.

## 7. Open questions

- **Q-003** `[OPEN]` — which other AI tools need adapters beyond Claude, Cursor,
  and `AGENTS.md`? `[UNVERIFIED]` whether Codex reads a root `AGENTS.md` (V-001).
- **Q-005** `[OPEN]` — `POST /buzzer/sessions/{id}/presses` was implemented as
  `[AllowAnonymous]` in Phase 5 because agent-to-API authentication is explicitly
  a Phase 14 concern (per ADR-005) — flagged by the session that built it as
  **not the final security posture**. Whoever does Phase 14 needs to replace
  this with a real auth scheme for the buzzer agent before it ships.

Q-001, Q-002, Q-004 remain **ANSWERED** — see `TASKS.md` Closed.

Verification queue in `TASKS.md`: V-001 (Codex/AGENTS.md), V-003 (SQL Server
availability — now more concrete, see V-006), V-006 (Docker/CI unverified),
V-007 (Testcontainers-vs-SQL-Server for Phase 4's migration).

## 8. Do not retry

- **L-001** through **L-004** — see `LESSONS.md` (heredoc failures, bare
  subagent invocation, doc duplication, stale git-state claims in briefs).
- **L-005** (new) — Swashbuckle does not read `[JsonPolymorphic]`/
  `[JsonDerivedType]` automatically, nor infer schemas from bare `IActionResult`.
- **L-006** (new) — `openapi-generator-cli` needs a JVM; this environment has
  none. Use NSwag (`NSwag.ConsoleCore`, pure .NET) instead.
- **New finding this session (not yet a numbered lesson, folded into process):**
  a checkpoint can go stale by *multiple entire phases*, not just one commit's
  worth of drift, if `/save-context` isn't run for several work sessions in a
  row. `docs/Implementation-Plan.md`'s own inline "Status: done" markers per
  phase turned out to be the fastest, most reliable way to reconstruct what
  happened in the gap — check that file's phase headers first when a brief's
  scope looks narrower than the repo's actual progress.

Full detail: `LESSONS.md`.

## 9. Environment and commands

`[FACT]` Windows 10 Pro 10.0.19045 · PowerShell 5.1 primary, Git Bash available ·
context root `C:\Sharique\Projects\Personal\QuizApp` · branch `master`, 12 commits.
`[FACT]` .NET SDKs 8.0.421 and 10.0.400 installed.

```bash
git status --short                 # outer repo only; nested repos report separately
cd QuizApp && dotnet build QuizApp.slnx     # 0 warnings, 0 errors, verified this session
dotnet test QuizApp.slnx --no-build         # 147 passed, 0 failed, verified this session
```

`[FACT]` No Docker daemon and no CI runner in this dev environment — Phase 3's
`docker compose up`/CI workflow and Phase 4's Testcontainers-vs-SQL-Server run
are `[UNVERIFIED]`, per the plan doc's own text (not just this session's guess).

## 10. Where to read more

| File | For |
|---|---|
| `TASKS.md` | The work queue, open questions, verification queue |
| `DECISIONS.md` | Why things are the way they are; rejected alternatives |
| `LESSONS.md` | What already failed — **read before proposing an approach** |
| `PROJECT.md` | Stack, repo map, glossary, environment, conventions |
| `HISTORY.md` | The timeline of checkpoints |
| `sessions/2026-09-07-01-phases-2-3-4-5-catchup.md` | Full detail of this session |
| `sessions/2026-09-04-03-phase0-owner-confirmed-phase1-domain.md` | Phase 0/1 detail |
| `_meta/SPEC.md` | How to save and resume context |
| `README.md` | The workflow, for humans |

**To checkpoint your own session:** `/save-context` in Claude Code, or in any
other tool: *"Save the context per `context/_meta/SPEC.md`."*
