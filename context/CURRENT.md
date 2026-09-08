# CURRENT CONTEXT — Quizware

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

**Last updated:** 2026-09-08 · **Session:** S-2026-09-08-02 · **Saved by:** Claude Sonnet 5 (Claude Code)

---

## 1. What this project is

`[FACT]` A rebuild of a quiz-tournament system. **QuizApp-9AMM** (ASP.NET MVC 4,
in production) is being replaced by **Quizware** — ASP.NET Core Web API on .NET 10
with SQL Server, an Angular 22 front end later. **QuickBuzz**, the existing buzzer
app, becomes an optional module that the system must work without.

`[FACT]` The core problem being solved: the legacy system hardcodes the tournament.
A new database per event; turn order as `QuestionNumber % 3` in 30+ places; the
running order of question types baked into 108 views as redirect chains; one
`TieBreaker` table/screens completely disconnected from qualification logic. The
new design turns all of it into configuration data.

**Naming trap:** `Quizware` (new), `QuizApp-9AMM` (legacy MVC 4), and `QuickBuzz`
(legacy buzzer) are three different systems. Check `PROJECT.md` §Domain glossary
before editing, or you will change the wrong one.

## 2. Current objective

`[FACT]` **Phase 7 — Tournament configuration — is now fully done, all 12
tasks (P7-01 through P7-12), and committed** as `3dbc6f2` ("Stage/segment CRUD
with reorder, scoring/selection/qualification/tie-break rule management,
IRuleService resolution, program readiness validation, and the 18-team seed
script"). `[FACT]` verified via `git show --stat 3dbc6f2` (43 files,
+2209/-37 lines).

`[FACT]` **Phase 6 (all of 6a–6f) is now fully committed too** — 6e (Media)
and 6f (Question bank), left uncommitted at the end of the prior checkpoint
(S-2026-09-08-01), landed as `783bc1c` ("Media upload with validation/
deduplication, and question bank CRUD with versioning, approval, import,
coverage, and duplicate detection") sometime between that checkpoint and this
one. **Phases 0–5 remain DONE**, unchanged. Full phase-by-phase history:
`TASKS.md` Closed section, `sessions/2026-09-07-01-*.md`,
`sessions/2026-09-08-01-*.md`.

**Delivered this session (S-2026-09-08-02), in order:**
- **Phase 7 — Tournament configuration** (all 12 tasks, `3dbc6f2`): Stage CRUD
  + reorder; segment-template CRUD + reorder (locked segments keep their slot
  — see D-024); `SegmentOrderMode` mutator; scoring/selection/qualification/
  tie-break rule upsert + two reset-to-defaults commands (wires up
  `DefaultScoringValues`, unused since Phase 4, and a new
  `DefaultTieBreakValues`); `IRuleService`/`RuleService` (specificity-based
  resolution — segment beats stage beats program); the richer
  `STAGE_HAS_NO_SEGMENTS` program/stage readiness check (this closes the gap
  the prior checkpoint had explicitly flagged as deferred to Phase 7); and
  `TournamentSeeder.cs` (an 18-team "Demo 18-Team Tournament" seed, wired into
  `Program.cs` behind the same `!IsProduction` guard as `AdminUserSeeder`,
  live-verified: 3 stages / 18 teams / 18 scoring rules present after a fresh
  `dotnet run`). New Domain mutators on entities that were create-only through
  Phase 1–6 (`Stage`, `StageSegmentTemplate`, `ScoringRule`,
  `QualificationRule`, `TieBreakRule`, `QuestionSelectionRule` — full list in
  `TASKS.md` T-016). Real bug found and fixed: reorder handlers needed a
  two-phase reindex to avoid transiently violating a unique `OrderIndex` index
  — see D-023. 22 new tests (`StagesEndpointTests.cs`, `RulesEndpointTests.cs`).
  No new EF migration needed.
- **Postman collection** (`Quizware/postman/`, **staged, not yet committed** —
  see §3): 80 requests across 10 folders covering every implemented endpoint
  through Phase 7 (Phase 8+ — Matches, Live engine, Scores, Standings,
  Qualification, Buzzer, Display, Reports — deliberately excluded, still 501
  stubs). Validated by actually running it with `newman` against a live
  `dotnet run`, not just written — this surfaced and led to fixing two real
  bugs (see below).
- **Two bugs found and fixed while validating the Postman collection:**
  (1) a collection-ordering bug (Admin folder ran before Programs, but
  Admin's Assign-Roles request needs `{{programId}}`) — fixed by reordering
  the folders. (2) A genuine backend bug, same class as L-007: `PUT
  /rules/scoring` 500'd when a "new" rule's natural key already matched a
  row `ResetScoringDefaults` had just seeded, because the handler only
  looked up existing rows by `Id`, not by natural key. Fixed in all three
  affected handlers (Scoring/Qualification/TieBreak — Selection's index is
  non-unique, no bug there) — see L-010 and D-023/D-024's sibling reasoning.
  Added a regression test. **These fixes are staged but not committed** —
  see §3/T-018.

**Important correction to the session brief (third recurrence of L-004):**
the brief driving this save claimed *neither* Phase 7 nor Phase 6e/6f had
been committed — only one-line commit messages were ever "offered." `git log`
verified `[FACT]` both already exist as real commits (`783bc1c`, `3dbc6f2`),
authored directly by `Sharique`, consistent with the standing "AI proposes,
user runs `git commit`" workflow (V-005) having happened outside this save's
visibility. Only the Postman collection and the three rule-handler bugfixes
(work that came *after* the Phase 7 commit message was offered) are actually
still uncommitted. See L-004's third entry — treat every "offered but not
run" claim in a future brief as needing independent `git log` verification,
not just the most recent one.

**What's next: Phase 8 — Question selection engine (`IQuestionSelector`).**
Both of its stated prerequisites (P6f question bank, P7 rule management) are
now done. Re-read `docs/Implementation-Plan.md`'s Phase 8 section fresh — do
not assume its text matches this file.

## 3. State of play

| Area | State |
|---|---|
| Phases 0–5 | `[FACT]` DONE, unchanged since S-2026-09-07-01. |
| Phase 6 (all of 6a–6f) | `[FACT]` DONE, **fully committed**: 6a `717b96f`, 6b `ae94bca`, 6c `41e45bd`, 6d `1d86810`, 6e+6f `783bc1c`. |
| Phase 7 (all of P7-01–P7-12) | `[FACT]` DONE, **committed** `3dbc6f2`. |
| Postman collection (`Quizware/postman/`) | `[FACT]` DONE, **staged, not committed**. |
| 3 rule-handler bugfixes + regression test | `[FACT]` DONE, **staged, not committed** (same diff group as the Postman work — found while validating it). |
| Test suite | `[UNVERIFIED]` this session — brief claims 238 passed/0 failed (95 Domain, 17 Application, 4 Architecture, 122 Api.IntegrationTests) after the Phase 7 + bugfix work; **not independently re-run this checkpoint** — a `dotnet build` attempt failed on file locks from a running `Quizware.Api.exe` (PID 5752) and Visual Studio. See V-009. |
| Build | Same caveat as above — see V-009. |
| Migrations | `[FACT]` Still 5 total on disk (unchanged from prior checkpoint) — Phase 7 needed no new migration, confirmed via `dotnet ef migrations has-pending-model-changes`. Whether all 5 are actually **applied** to LocalDB remains `[UNVERIFIED]` — see V-008 (unchanged). |
| Git (outer repo) | `[FACT]` Branch `master`, HEAD `3dbc6f2`, 19 commits total. Staged-not-committed: `Quizware/postman/**` (new) and 4 modified files (3 rule-handler fixes + `RulesEndpointTests.cs`). |
| Context system | `[FACT]` This is its 6th real merge. |

## 4. Next actions

1. **Ask the user whether to commit the Postman collection + rule-handler
   bugfixes** — see T-018. A single commit is reasonable here (the bugfixes
   were found while validating the collection, not a cleanly separable unit
   the way 6e/6f were). Offered message: "Add Postman collection covering all
   Phase 0-7 endpoints, and fix three rule-upsert handlers that 500'd on a
   natural-key collision".
2. **Start Phase 8 — Question selection engine** (`IQuestionSelector`).
   Re-read `docs/Implementation-Plan.md`'s Phase 8 section fresh. See T-019.
3. **T-006** (Phase 14, not urgent) — decide whether `Quizware.BuzzerAgent`
   references `Quizware.Modules.Buzzer` to reuse serial frame-parsing code, or
   reimplements it standalone.
4. **Verify when possible, not urgent:** V-006 (Docker/CI), V-007
   (Testcontainers vs SQL Server), V-008 (migrations actually applied to
   LocalDB, not just generated), V-009 (new — re-run `dotnet build`/
   `dotnet test` once the locked `Quizware.Api.exe` process is stopped, to
   independently confirm the 238-passed claim).
5. Optional, low priority: `docs/Implementation-Plan.md`'s own "Start here"
   section (line ~794) is now two phases stale (still names Phase 6/7 text
   that predates this session). See T-017.

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
- `[DECIDED]` **Commit only when asked** (V-005). **As of this session, only the
  Postman collection + 3 rule-handler bugfixes are uncommitted; Phase 6
  (all of 6a–6f) and Phase 7 are fully committed** (see §2's correction —
  third recurrence of L-004).
- `[FACT]` **A DB-only uniqueness/state constraint without a handler pre-check
  surfaces as an unhandled 500, not a clean 4xx.** Hit 3 times in the prior
  session (Team.Code, Topic/Tag name, `Question.Approve`'s
  `InvalidOperationException`, L-007), and **recurred a fourth/fifth/sixth
  time this session** in 3 of the 4 rule-upsert handlers (Scoring/
  Qualification/TieBreak) — this time the handler *did* pre-check by `Id`,
  but not by the entity's full natural key, so a "create" whose natural key
  already existed (e.g. right after `ResetScoringDefaults`) still 500'd. See
  L-010: any upsert handler must look up existing rows by every column a
  unique index covers, not just `Id`.
- `[FACT]` **`Program.MaxTeams` (typed column) is the real team-cap mechanism**;
  the `ProgramSetting("Teams","MaxTeams")` key seen in Phase 6a's own test
  fixtures was only ever an incidental example value, not a second intended
  mechanism — see L-009 if you find that key in old test code and wonder.
- `[DECIDED]` **Reordering a unique-`OrderIndex` list needs a two-phase reindex**
  (D-023) — write a temporary offset first, then final values in a second
  `SaveChangesAsync`, or a unique-index violation can occur mid-batch
  depending on EF's per-row update order. **Locked segments keep their slot
  during a bulk segment reorder** (D-024) — `IsOrderLocked` segments are
  excluded from repositioning, not validated-and-rejected if the caller's
  order would have moved them.

Reasoning for all decisions: `DECISIONS.md` D-001 – D-024.

## 6. Files in play

| Path | Note |
|---|---|
| `Quizware/src/Quizware.Application/Abstractions/IAppDbContext.cs` | New in 6a — the port MediatR handlers use for Domain-typed entities |
| `Quizware/src/Quizware.Application/{Programs,Teams,Topics,Tags,Media,QuestionBank,Tournament,Rules}/**` | Command/query handlers, one folder per area — `Tournament/` and `Rules/` are new this session (Phase 7) |
| `Quizware/src/Quizware.Application/Rules/Services/{IRuleService,RuleService}.cs` | New this session — specificity-based scoring-rule resolution (segment > stage > program) |
| `Quizware/src/Quizware.Api/Controllers/v1/{ProgramsController,AuthController,AdminController,TeamsController,TopicsController,TagsController,QuestionsController,StagesController,RulesController}.cs` | Real handlers now, not 501 stubs — `StagesController`/`RulesController` rewritten this session |
| `Quizware/src/Quizware.Infrastructure/Persistence/TournamentSeeder.cs` | New this session — 18-team demo tournament seed, wired into `Program.cs` |
| `Quizware/src/Quizware.Domain/Tournament/Stage.cs`, `StageSegmentTemplate.cs`, `Domain/Scoring/ScoringRule.cs`, `Domain/Qualification/{QualificationRule,TieBreakRule,DefaultTieBreakValues}.cs`, `Domain/Tournament/QuestionSelectionRule.cs` | New mutators this session (`Rename`/`Reorder`/`Update`/`Delete`/etc.) on entities that were create-only through Phase 1–6 |
| `Quizware/postman/{Quizware.postman_collection.json,README.md}` | New this session — 80 requests, 10 folders, Phase 0–7 coverage. **Staged, not committed.** |
| `Quizware/src/Quizware.Application/Rules/Commands/{UpsertScoringRules,UpsertQualificationRules,UpsertTieBreakRules}.cs` | Bugfixed this session (natural-key lookup, L-010). **Staged, not committed.** |
| `Quizware/src/Quizware.Infrastructure/Identity/{IJwtTokenService,JwtTokenService}.cs` | 6b: optional `programId`/`expiresIn` params for display/select-program tokens |
| `Quizware/src/Quizware.Infrastructure/Imports/{TeamExcelParser,McqQuestionExcelParser}.cs` | Excel import parsers (format-only parsing; ClosedXML 0.105.1) |
| `Quizware/src/Quizware.Infrastructure/Media/{LocalFileStorage,MediaStorageOptions}.cs` | 6e, committed `783bc1c` |
| `Quizware/src/Quizware.Domain/QuestionBank/*.cs` | All 10 question subclasses' `Create()` factories gained optional params (6f) |
| `Quizware/src/Quizware.Api/Contracts/V1/{Admin,Teams,Topics,Questions,Stages}/**` | Rewritten/extended contracts; `StageContracts.cs`'s `CreateStageRequest` gained `StageType` this session (Phase 5 stub had omitted it) |
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
unverified), V-007 (Testcontainers-vs-SQL-Server), V-008 (migrations actually
applied to LocalDB, not just present on disk), V-009 (new — this session's
238-passed test claim not independently re-run, build was file-locked by a
running dev-server process).

## 8. Do not retry

- **L-001** through **L-006** — see `LESSONS.md` (heredoc failures, bare subagent
  invocation, doc duplication, stale git-state claims in briefs, Swashbuckle
  polymorphism gap, openapi-generator-cli needs a JVM).
- **L-004** (updated this session, third recurrence) — a conversation brief's
  claim about commit state was stale *again*: this session's brief said
  neither Phase 6e/6f nor Phase 7 was committed; `git log` showed both already
  committed by the user out-of-band (`783bc1c`, `3dbc6f2`). Same root cause
  each time. Always run `git log`/`git status` yourself before writing any
  commit-state claim, regardless of how specific or confident the brief
  sounds — this is now a 3-for-3 pattern, treat it as near-certain to recur.
- **L-007** — a uniqueness or state constraint enforced only at the DB level
  (unique index, check constraint) but never pre-checked in the handler
  surfaces as an unhandled 500 instead of a clean 4xx. Always add the
  matching pre-check, or map the exception type in `GlobalExceptionHandler`.
- **L-008** — `Question.Approve`/domain-thrown `InvalidOperationException`
  isn't one of `GlobalExceptionHandler`'s mapped types; check state *before*
  calling a domain method that throws a generic exception type, and throw a
  mapped domain exception instead.
- **L-009** — don't mistake `ProgramSetting("Teams","MaxTeams")` (seen in
  Phase 6a's own test fixtures) for a second real team-cap mechanism; the typed
  `Program.MaxTeams` column, wired in Phase 6c, is the actual one.
- **L-010** (new) — L-007's pattern recurred in 3 of 4 rule-upsert handlers:
  each pre-checked existing rows by `Id` only, not by the full natural key a
  unique index covers, so a "create" whose natural key already existed (e.g.
  right after a reset-to-defaults) still 500'd. Any upsert handler must look
  up by every column the unique index covers, not just `Id` — found by
  end-to-end (Newman) testing, not a unit test.

Full detail: `LESSONS.md`.

## 9. Environment and commands

`[FACT]` Windows 10 Pro 10.0.19045 · PowerShell 5.1 primary, Git Bash available ·
context root `C:\Sharique\Projects\Personal\Quizware` · branch `master`, 19 commits.
`[FACT]` .NET SDKs 8.0.421 and 10.0.400 installed. `[FACT]` LocalDB instance
`(localdb)\MSSQLLocalDB`, database `Quizware-Dev`, used for live manual
verification via `dotnet run` (port 5299) and curl against the seeded admin
(`admin@quizapp.local` / `ChangeMe!123` — credential location only, per policy).
`[FACT]` `newman` (Postman's CLI runner) is now usable in this environment via
`npx --yes newman` — not previously used/verified here.

```bash
git status --short                          # outer repo only
cd Quizware && dotnet build Quizware.slnx      # last independently verified 2026-09-08 S-2026-09-08-01; this session's build attempt failed on file locks — see V-009
dotnet test Quizware.slnx --no-build          # brief claims 238 passed, 0 failed — [UNVERIFIED] this session, see V-009
dotnet ef migrations list --project src/Quizware.Infrastructure --startup-project src/Quizware.Api
npx --yes newman run Quizware/postman/Quizware.postman_collection.json --folder "00 Health"   # etc. per folder — see Quizware/postman/README.md for run order
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
| `sessions/2026-09-08-02-phase7-tournament-configuration.md` | Full detail of this session (Phase 7 + Postman collection) |
| `sessions/2026-09-08-01-phase6-configuration-modules.md` | Phase 6 (6a–6f) detail |
| `sessions/2026-09-07-01-phases-2-3-4-5-catchup.md` | Phases 2–5 detail |
| `_meta/SPEC.md` | How to save and resume context |
| `README.md` | The workflow, for humans |

**To checkpoint your own session:** `/save-context` in Claude Code, or in any
other tool: *"Save the context per `context/_meta/SPEC.md`."*
