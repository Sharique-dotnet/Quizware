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

**Last updated:** 2026-09-08 · **Session:** S-2026-09-08-01 · **Saved by:** Claude Sonnet 5 (Claude Code)

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

`[FACT]` **Phase 6 — Configuration modules — is now fully done, all six
sub-phases (6a–6f, tasks P6-01 through P6-21).** Verified this session by
re-running the full suite (`dotnet build` → 0 warnings/errors; `dotnet test` →
**224 passed, 0 failed**: 95 Domain, 17 Application, 4 Architecture, 108
Api.IntegrationTests) and by spot-checking file paths named in the session brief
directly against the repo (all present as described).

**Phases 0–5 remain DONE from the prior checkpoint** (S-2026-09-07-01) —
unchanged this session. Full phase-by-phase history: `TASKS.md` Closed section
(T-010 through T-013), `sessions/2026-09-07-01-*.md`.

**Sub-phases delivered this session, in order:**
- **6a — Program management** (`P6-01`–`P6-06`): `IAppDbContext` port
  introduced; real `ProgramsController` handlers via MediatR (`Application/Programs/`).
  Committed `717b96f`.
- **6b — Users and roles** (`P6-07`–`P6-10`): `AuthController`/`AdminController`
  extended directly (no MediatR — see D-019). Login/refresh/logout/me/
  select-program/display-token/change-password; user invite/list/assign-roles/
  deactivate/reset-password. Committed `ae94bca`.
- **6c — Teams** (`P6-11`–`P6-13`): Team CRUD, status changes, Excel import
  (validate → report → commit) via `Application/Teams/` + `TeamExcelParser`
  (ClosedXML 0.105.1). Migration `FixMatchParticipantRemovalCheckConstraint`
  (real pre-existing bug, see §8). Committed `41e45bd`.
- **6d — Topics and tags** (`P6-14`): Topic (parent/child, cycle-checked) and Tag
  CRUD, shared-vs-per-program scoping. Migration
  `AddTopicParentForeignKeyAndTagUniqueIndex`. Committed `1d86810`.
- **6e — Media** (`P6-15`): `IFileStorage`/`LocalFileStorage`, magic-byte +
  extension + size validation, SHA-256 dedup. **Uncommitted** — see §3.
- **6f — Question bank** (`P6-16`–`P6-21`): all 10 formats have Create, format-
  agnostic Update/versioning/Approve/Retire/Delete/Coverage/Duplicates; Excel
  import scoped to MCQ only (deliberate, D-022). Migration
  `AddQuestionDifficultyCheckConstraint`. **Uncommitted** — see §3.

**Important correction to the session brief (recurrence of L-004):** the brief
that drove this save claimed *nothing from 6b–6f was committed*. `git log`
verified `[FACT]` this is wrong for 6a–6d: four commits exist, dated 2026-09-08,
authored directly by `Sharique` — `717b96f` (6a), `ae94bca` (6b), `41e45bd` (6c),
`1d86810` (6d) — consistent with the standing "AI proposes, user runs `git
commit`" workflow (V-005) having happened outside this save's visibility. Only
**6e (Media) and 6f (Question bank) are actually uncommitted**, confirmed via
`git status --short` (52 files, +6259/-56 lines: `Application/Media/**`,
`Application/QuestionBank/**`, `Infrastructure/Media/**`,
`Infrastructure/Imports/McqQuestionExcelParser.cs`, `QuestionsController.cs`,
`Contracts/V1/Questions/**`, the `AddQuestionDifficultyCheckConstraint`
migration, `.gitignore` `**/App_Data/` line, two new test files). See L-004
(updated) for this pattern recurring.

**What's next: Phase 7 — Tournament configuration** (Stage CRUD, segment
templates, segment reordering, scoring/selection/qualification rule management,
tie-break rule management, program readiness validation). Per
`docs/Implementation-Plan.md` line 817 ("Start here" section) — **not yet
re-edited to name Phase 7**, since the brief for this session said
`Implementation-Plan.md` itself was not updated; treat its "Start here" text as
one phase stale, `CURRENT.md` is authoritative.

## 3. State of play

| Area | State |
|---|---|
| Phases 0–5 | `[FACT]` DONE, unchanged since S-2026-09-07-01. See that checkpoint / `TASKS.md` Closed for detail. |
| Phase 6a (Programs) | `[FACT]` DONE, **committed** `717b96f` |
| Phase 6b (Users/roles) | `[FACT]` DONE, **committed** `ae94bca` |
| Phase 6c (Teams) | `[FACT]` DONE, **committed** `41e45bd` |
| Phase 6d (Topics/tags) | `[FACT]` DONE, **committed** `1d86810` |
| Phase 6e (Media) | `[FACT]` DONE, **uncommitted** (working tree) |
| Phase 6f (Question bank) | `[FACT]` DONE, **uncommitted** (working tree, same diff as 6e) |
| Test suite | `[FACT]` 224 passed / 0 failed, re-run this session: 95 Domain, 17 Application, 4 Architecture, 108 Api.IntegrationTests |
| Build | `[FACT]` 0 warnings, 0 errors, re-run this session |
| Migrations | `[FACT]` 5 total on disk: `InitialIdentitySchema`, `AddBusinessSchema` (both pre-existing), `FixMatchParticipantRemovalCheckConstraint` (6c), `AddTopicParentForeignKeyAndTagUniqueIndex` (6d), `AddQuestionDifficultyCheckConstraint` (6f, uncommitted). `dotnet ef migrations list` against the configured connection lists all 5, confirming the tooling sees them `[FACT]`. Whether they were actually **applied** to the LocalDB (`QuizApp-Dev`) is `[UNVERIFIED]` this session — brief claims yes, live-verified during the session's own work, but not independently re-checked here (see V-008). |
| Git (outer repo) | `[FACT]` Branch `master`, HEAD `1d86810`, 17 commits total (`bf8cb06` → `1d86810`). 6e+6f uncommitted in the working tree — nothing staged. |
| Context system | `[FACT]` This is its 5th real merge. |

## 4. Next actions

1. **Ask the user whether to commit Phase 6e+6f's uncommitted work** before
   starting Phase 7 — see T-015. Two logical commits per the session's own
   grouping: Media (6e) and Question bank (6f) touch mostly disjoint files
   (`Contracts/V1/Questions/**` and `QuestionsController.cs` are shared, so a
   clean single-purpose split may not be possible — decide when asked).
2. **Start Phase 7 — Tournament configuration** (Stage CRUD, segment templates,
   reordering, scoring/selection/qualification/tie-break rule management,
   program readiness validation). Re-read `docs/Implementation-Plan.md`'s Phase
   7 section fresh — do not assume its text matches this file. This is also
   where the richer `STAGE_HAS_NO_SEGMENTS` coverage check (deferred in 6a's
   `ValidateProgram` and 6f's coverage query) would get built out for real.
   See T-016.
3. **T-006** (Phase 14, not urgent) — decide whether `QuizApp.BuzzerAgent`
   references `QuizApp.Modules.Buzzer` to reuse serial frame-parsing code, or
   reimplements it standalone.
4. **Verify when possible, not urgent:** V-006 (Docker/CI), V-007 (Testcontainers
   vs SQL Server), V-008 (new — did the 6c/6d/6f migrations actually get applied
   to LocalDB, not just generated).
5. Optional, low priority: `docs/Implementation-Plan.md`'s own "Start here"
   section (line ~794) still says Phase 6 is next and describes Phases 3–5 as
   "Uncommitted" — both now stale. See T-017.

Full queue: `TASKS.md`.

## 5. Constraints you must respect

- `[DECIDED]` **Architecture is settled at design level** — modular monolith, Clean
  Architecture, shared schema with `ProgramId` multi-tenancy, Table-Per-Type
  questions (D-009), configuration-driven tournament with configurable segment
  order (D-010), event-sourced scoring, tie-break as an ordinary Match through the
  existing engine (D-011), EF Core 10 code-first, SignalR. 10 ADRs in `docs/adr/`.
  Full list with rejected alternatives: `docs/new-system/02-Architecture-Proposal.md`
  §2.16, plus D-009 – D-022 in `DECISIONS.md`. Do not re-open one without reading
  why the alternative was rejected.
- `[DECIDED]` **Questions are Table-Per-Type** (D-009), one route per format.
  Reads now go through a parallel `QuestionDto` hierarchy in
  `Application/QuestionBank/Dtos/` (Application cannot reference Api's
  `QuestionResponse` types), mapped to the API contract by
  `QuestionResponseMapper.cs`.
- `[DECIDED]` **New this session — routing convention for what uses MediatR**
  (D-019): if the phase's core entities are Domain types exposed on
  `IAppDbContext` (Team, Topic, Tag, Question, Program), use MediatR/Application.
  If they are Infrastructure-only types (Identity's `AppUser`/`AppRole`/
  `ProgramUser`; `ImportBatch`/`ImportBatchRow`), business logic goes directly in
  the controller injecting the concrete `AppDbContext` — Application cannot
  reference Infrastructure types at all (enforced by `Architecture.Tests`).
  Established/repeated three times (6b, 6c's import, 6f's import); treat as
  binding for future phases, not a one-off.
- `[DECIDED]` **New this session — question editing has no separate Update
  endpoint** (D-020): `PUT {formatCode}/{id}` deserializes into the same
  per-format Create request and calls the same Create command with an optional
  `ReplacesQuestionId`, so create and update can never validate differently. Old
  question is soft-deleted if unused, retired if used (FR-3.10).
- `[DECIDED]` **New this session — Phase 6f Excel import is MCQ-only** (D-022);
  the other 9 formats' import templates are explicitly deferred, not forgotten.
- `[ASSUMED]` **Media validation limits are this implementation's own numbers,
  not documented anywhere** (D-021): extension allow-list (jpg/jpeg/png/gif/
  mp3/wav/mp4/webm), 25 MB size cap, specific magic-byte signatures. Confirm
  with the user before treating these as fixed product requirements.
- `[DECIDED]` **7 roles, no Judge** (D-013), **9 authorization policies**. Display
  tokens carry only `Roles.Display` regardless of the minting admin's own roles —
  this is what makes "cannot write anything" true (every write policy is
  `RequireRole` over roles that never include Display), live-verified this session.
- `[DECIDED]` **On-premises hosting, no cloud dependency** (D-014).
- `[DECIDED]` **The buzzer must be deletable** — `IBuzzerProvider` port, `Null`
  default (ADR-005).
- `[DECIDED]` **No fake answers, ever.**
- `[DECIDED]` **`docs/` is entirely gitignored** (D-016) — do not read "not in
  `git log`" as "doesn't exist" for anything under `docs/`.
- `[DECIDED]` **Swashbuckle 10.x needs explicit polymorphic-schema wiring**
  (D-017); **NSwag, not openapi-generator-cli** (D-018).
- `[DECIDED]` **Commit only when asked** (V-005). **As of this session, 6e+6f are
  uncommitted; 6a–6d already are** (see §2's correction).
- `[FACT]` **A DB-only uniqueness/state constraint without a handler pre-check
  surfaces as an unhandled 500, not a clean 4xx.** Hit 3 times this session
  (Team.Code, Topic/Tag name, `Question.Approve`'s `InvalidOperationException`)
  — see L-007. Always add the matching pre-check, or map the exception type in
  `GlobalExceptionHandler`, whenever adding a new unique index or check
  constraint.
- `[FACT]` **`Program.MaxTeams` (typed column) is the real team-cap mechanism**;
  the `ProgramSetting("Teams","MaxTeams")` key seen in Phase 6a's own test
  fixtures was only ever an incidental example value, not a second intended
  mechanism — see L-009 if you find that key in old test code and wonder.

Reasoning for all decisions: `DECISIONS.md` D-001 – D-022.

## 6. Files in play

| Path | Note |
|---|---|
| `QuizApp/src/QuizApp.Application/Abstractions/IAppDbContext.cs` | New in 6a — the port MediatR handlers use for Domain-typed entities |
| `QuizApp/src/QuizApp.Application/{Programs,Teams,Topics,Tags,Media,QuestionBank}/**` | Phase 6 command/query handlers, one folder per area |
| `QuizApp/src/QuizApp.Api/Controllers/v1/{ProgramsController,AuthController,AdminController,TeamsController,TopicsController,TagsController,QuestionsController}.cs` | Real handlers now, not 501 stubs |
| `QuizApp/src/QuizApp.Infrastructure/Identity/{IJwtTokenService,JwtTokenService}.cs` | 6b: optional `programId`/`expiresIn` params for display/select-program tokens |
| `QuizApp/src/QuizApp.Infrastructure/Imports/{TeamExcelParser,McqQuestionExcelParser}.cs` | Excel import parsers (format-only parsing; ClosedXML 0.105.1) |
| `QuizApp/src/QuizApp.Infrastructure/Media/{LocalFileStorage,MediaStorageOptions}.cs` | 6e, **uncommitted** |
| `QuizApp/src/QuizApp.Infrastructure/Persistence/Configurations/{TournamentConfigurations,QuestionConfigurations}.cs` | Bug fixes: `CK_MP_Removal` (6c), Topic parent FK + Tag unique index (6d), `CK_Question_Difficulty` (6f, uncommitted) |
| `QuizApp/src/QuizApp.Infrastructure/Persistence/Migrations/2026090*` | 3 new migrations this session — see §3 table for names/commit status |
| `QuizApp/src/QuizApp.Domain/QuestionBank/*.cs` | All 10 question subclasses' `Create()` factories gained optional params (6f) |
| `QuizApp/src/QuizApp.Api/Contracts/V1/{Admin,Teams,Topics,Questions}/**` | Rewritten/extended contracts; several were unusable Phase 5 stubs (mismatched field names) fixed this session |
| `context/_meta/SPEC.md` | The save/resume procedure |

`[FACT]` `QuizApp-9AMM/` and `QuickBuzz/` are **nested git repositories**.

## 7. Open questions

- **Q-003** `[OPEN]` — which other AI tools need adapters beyond Claude, Cursor,
  and `AGENTS.md`? `[UNVERIFIED]` whether Codex reads a root `AGENTS.md` (V-001).
- **Q-005** `[OPEN]` — `POST /buzzer/sessions/{id}/presses` is `[AllowAnonymous]`,
  not the final security posture; Phase 14 needs a real auth scheme for the
  buzzer agent.

Q-001, Q-002, Q-004 remain **ANSWERED** — see `TASKS.md` Closed.

Verification queue in `TASKS.md`: V-001 (Codex/AGENTS.md), V-006 (Docker/CI
unverified), V-007 (Testcontainers-vs-SQL-Server), V-008 (new — migrations
generated this session actually applied to LocalDB, not just present on disk).

## 8. Do not retry

- **L-001** through **L-006** — see `LESSONS.md` (heredoc failures, bare subagent
  invocation, doc duplication, stale git-state claims in briefs, Swashbuckle
  polymorphism gap, openapi-generator-cli needs a JVM).
- **L-004** (updated this session) — a conversation brief's claim about commit
  state recurred as stale *again*: this session's brief said all of 6b–6f was
  uncommitted; `git log` showed 6a–6d already committed by the user out-of-band.
  Same root cause as the original entry. Always run `git log`/`git status`
  yourself before writing any commit-state claim, regardless of how specific or
  confident the brief sounds.
- **L-007** (new) — a uniqueness or state constraint enforced only at the DB
  level (unique index, check constraint) but never pre-checked in the handler
  surfaces as an unhandled 500 instead of a clean 4xx. Hit 3 times this session.
  Always add the matching pre-check, or map the exception type in
  `GlobalExceptionHandler`.
- **L-008** (new) — `Question.Approve`/domain-thrown `InvalidOperationException`
  isn't one of `GlobalExceptionHandler`'s mapped types; check state *before*
  calling a domain method that throws a generic exception type, and throw a
  mapped domain exception instead.
- **L-009** (new) — don't mistake `ProgramSetting("Teams","MaxTeams")` (seen in
  Phase 6a's own test fixtures) for a second real team-cap mechanism; the typed
  `Program.MaxTeams` column, wired in Phase 6c, is the actual one.

Full detail: `LESSONS.md`.

## 9. Environment and commands

`[FACT]` Windows 10 Pro 10.0.19045 · PowerShell 5.1 primary, Git Bash available ·
context root `C:\Sharique\Projects\Personal\QuizApp` · branch `master`, 17 commits.
`[FACT]` .NET SDKs 8.0.421 and 10.0.400 installed. `[FACT]` LocalDB instance
`(localdb)\MSSQLLocalDB`, database `QuizApp-Dev`, used for live manual
verification via `dotnet run` (port 5299) and curl against the seeded admin
(`admin@quizapp.local` / `ChangeMe!123` — credential location only, per policy).

```bash
git status --short                          # outer repo only
cd QuizApp && dotnet build QuizApp.slnx     # 0 warnings, 0 errors, verified this session
dotnet test QuizApp.slnx --no-build         # 224 passed, 0 failed, verified this session
dotnet ef migrations list --project src/QuizApp.Infrastructure --startup-project src/QuizApp.Api
```

`[FACT]` No Docker daemon and no CI runner in this dev environment — see V-006/V-007.

## 10. Where to read more

| File | For |
|---|---|
| `TASKS.md` | The work queue, open questions, verification queue |
| `DECISIONS.md` | Why things are the way they are; rejected alternatives |
| `LESSONS.md` | What already failed — **read before proposing an approach** |
| `PROJECT.md` | Stack, repo map, glossary, environment, conventions |
| `HISTORY.md` | The timeline of checkpoints |
| `sessions/2026-09-08-01-phase6-configuration-modules.md` | Full detail of this session (6a–6f) |
| `sessions/2026-09-07-01-phases-2-3-4-5-catchup.md` | Phases 2–5 detail |
| `_meta/SPEC.md` | How to save and resume context |
| `README.md` | The workflow, for humans |

**To checkpoint your own session:** `/save-context` in Claude Code, or in any
other tool: *"Save the context per `context/_meta/SPEC.md`."*
