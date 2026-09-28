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

**Last updated:** 2026-09-28 · **Session:** S-2026-09-28-01 · **Saved by:** Claude Opus 5.5 (Claude Code)

---

## 1. What this project is

`[FACT]` A rebuild of a quiz-tournament system. **QuizApp-9AMM** (ASP.NET MVC 4,
in production) is being replaced by **Quizware** — ASP.NET Core Web API on .NET 10
with SQL Server, an Angular 22 front end later. **QuickBuzz**, the existing buzzer
app, becomes an optional module that the system must work without.

`[FACT]` The core problem: the legacy system hardcodes the tournament (a database
per event, turn order as `QuestionNumber % 3` in 30+ places, question-type order
baked into 108 views, a `TieBreaker` table disconnected from qualification). The
new design turns all of it into configuration data.

**Naming trap:** `Quizware` (new, in `Quizware/`), `QuizApp-9AMM` (legacy MVC 4),
and `QuickBuzz` (legacy buzzer) are three different systems. The outer repo root
folder is named differently per machine (`C:\Sharique\Projects\Personal\QuizApp`
on the user's Windows PC, `/home/user/Quizware` in the cloud container) — neither
is the legacy `QuizApp-9AMM/`. See `PROJECT.md` §Domain glossary.

## 2. Current objective

`[FACT]` (user stated) Implement `docs/Implementation-Plan.md` phase by phase.
**Phases 0–10 are done. Phase 11 — qualification and tie-breaking — is next**
(T-023). Re-read the plan's Phase 11 section (~line 586) fresh; do not assume it
matches this file.

**Delivered in S-2026-09-28-01** (cloud Claude Code session, Linux container):
- **Phase 9 — match engine.** 9a–9f merged to master via PR #1 (merge
  `3f945cf`, opened by the user). Then a gap-closing pass on the branch:
  reversal is ProgramAdmin-only (D-034), per-segment turn rotation with no turn
  holder in Buzzer/RapidFire (D-035), one `IQuestionFormatHandler` per format
  (D-036), passing direction/limits/reveal-if-all-pass (D-037), Choice-round
  topic board (D-038), sudden death (D-039), match auto-seed (D-040),
  transactional outbox (D-041), the Phase 9 required tests, and a selector
  lock fix (D-044, supersedes D-029).
- **Phase 10 — scoring and standings.** ScoreEvent ledger with transactional
  read models, reversal, recalculation (D-042); ProgramAdmin-only manual adjust;
  tie-break criteria service (D-043); overall/stage/team standings.
- **Postman** refreshed: folders 10 Matches, 11 Live Match, 12 Scores,
  13 Standings; 156 requests, 199 assertions; `adminPassword` fixed (T-022).
- **First run against real SQL Server** (2022, Docker): all migrations apply;
  newman passes except the 5 documented file-attachment requests.

The user's last request: close the Phase 9 gaps, settle Operator reversal,
refresh Postman and `context/`, and "tell me what to do". All done — the items
needing the user are in §7.

## 3. State of play

| Area | State |
|---|---|
| Phases 0–8 | `[FACT]` DONE, on master. |
| Phase 9 (incl. gap items) | `[FACT]` DONE. 9a–9f on master (`3f945cf`); gap work on the branch. |
| Phase 10 | `[FACT]` DONE, on the branch (`2431cb2`, `738e947`, `d6cdc1f`, `3aaea4e`). |
| Phase 11 | `[FACT]` Not started. `QualificationController` is all 501 stubs. |
| Build | `[FACT]` `dotnet build Quizware.slnx -c Release /warnaserror` → 0 warnings/errors (2026-09-28, brief). |
| Tests | `[FACT]` 421/421: 17 Application, 131 Domain, 4 Architecture, 269 Api.IntegrationTests (2026-09-28, brief; not re-run by the keeper — V-012). |
| Migrations | `[FACT]` None added in Phases 9–10; latest `20260908084206_AddQuestionDifficultyCheckConstraint`; `has-pending-model-changes` clean. Applied cleanly to SQL Server 2022 (V-003 resolved). |
| Postman | `[FACT]` 156 requests / 199 assertions; expect exactly 5 failures (team import validate/commit, media upload, MCQ import validate/commit — need manual file attachments). |
| Still 501 | `[FACT]` admin lookups, match preflight, live snapshot/restore, Qualification, Buzzer, Display, Reports (grep of `StatusCode(501` 2026-09-28). |
| Git | `[FACT]` Branch `claude/workflows-project-status-bsgu4d`, HEAD `774264c`, clean, pushed, **15 commits ahead of `origin/master`, no PR open** (Q-011). |
| Outbox | `[FACT]` Rows written transactionally; nothing delivers them yet (T-024, Phase 12). |

## 4. Next actions

1. **Ask the user the §7 questions** — especially Q-011 (PR for the 15
   commits?) and Q-007/Q-008 (contract/endpoint additions), since they decide
   what Phase 11 can rely on.
2. **Start Phase 11** (T-023): present a phased plan first (What/Why/Where/
   Impact, one-line commit per phase, per `CLAUDE.md`). Use
   `ITieBreakCriteriaService` (D-043) before creating a tie-break match through
   the existing engine (D-011); call `MatchSegment.MakeSuddenDeath` from the
   `TieBreakRule` (D-039, V-010); honour `ScoreCountsTowardStage` (D-042).
3. **T-028** (quick check): the shuffled option order from reservation may never
   be persisted — `ServeQuestion.cs:71` activates with `"[]"`. Confirm and fix if
   real (D-025).
4. Lower priority: T-027 (after Q-010), T-026 live-contract gaps, T-025 other
   501 stubs, T-017 plan-doc status markers, T-006 (Phase 14).

Full queue: `TASKS.md`.

## 5. Constraints you must respect

**Workflow (user stated, `CLAUDE.md`):**
- `[FACT]` Never commit unless asked. Exception granted for Phases 9–10 only
  ("test, commit when all tests pass, move to the next phase") — do not assume it
  carries over; ask.
- `[FACT]` Commit messages: single line, **no `Co-Authored-By:` or
  `Claude-Session:` trailers**. Separate commits for API and Angular changes.
- `[FACT]` Plans in phases, each with What/Why/Where/What-it-affects and a
  one-line commit message.
- `[FACT]` Work only on branch `claude/workflows-project-status-bsgu4d`; do not
  create another branch; no PRs unless asked.
- `[FACT]` Read `context/` first and keep it in mind throughout.

**Architecture and domain (full reasoning in `DECISIONS.md`):**
- `[DECIDED]` Modular monolith, Clean Architecture, shared schema with
  `ProgramId` tenancy, TPT questions (D-009), configurable segment order
  (D-010), event-sourced scoring (D-042), tie-break as an ordinary Match
  (D-011), EF Core 10 code-first, SignalR + outbox (D-041). ADRs in `docs/adr/`.
- `[DECIDED]` 7 roles, no Judge; reversal, disqualification, score adjustment,
  recalculation, and tie-break manual resolution are ProgramAdmin/SuperAdmin
  only (D-013, D-034).
- `[DECIDED]` MediatR for Domain-typed entities; Infrastructure-only entities
  handled in controllers (D-019). Application never references Infrastructure.
- `[DECIDED]` Selector write methods never call `SaveChangesAsync` — the caller
  owns the transaction (D-031). Selector lock rule: D-044.
- `[DECIDED]` Every live format has its own `IQuestionFormatHandler` (D-036);
  set `AnyTeamMayAnswer` correctly (D-035).
- `[DECIDED]` Any `ScoreEvent` must go through `IScoringEngine` (D-042).
- `[DECIDED]` Tag filtering not implemented — needs a `QuestionTag` join table
  first (D-026). `DifficultyMixJson` is a percentage map (D-030, `[ASSUMED]`,
  Q-006).
- `[DECIDED]` `docs/` is gitignored (D-016) — doc edits never appear in git.
- `[DECIDED]` On-premises hosting (D-014); buzzer must be deletable (ADR-005);
  no fake answers, ever.
- `[FACT]` Any upsert against a unique index must look up by every indexed
  column, or it 500s (L-007/L-010). Guard domain `InvalidOperationException`s in
  the handler (L-008).
- `[FACT]` Enum-typed contract fields need numeric values (L-015, Q-009).

## 6. Files in play

| Path (under `Quizware/`) | Note |
|---|---|
| `src/Quizware.Application/Gameplay/` | Match engine: setup, rotation, live state, sudden death, `Commands/*` |
| `src/Quizware.Application/Gameplay/Formats/` | One handler per format + `OptionFormatHandler`, `AcceptedAnswers`, `QuestionFormatHandlers.For` |
| `src/Quizware.Application/Gameplay/Commands/ServeQuestion.cs` | Line 71 — T-028 option-order check |
| `src/Quizware.Application/Scoring/` | `IScoringEngine`, standings, adjust, recalculate |
| `src/Quizware.Application/Qualification/TieBreakCriteriaService.cs` | Phase 11 builds on this |
| `src/Quizware.Application/Selection/QuestionSelector.cs` | Lock rule D-044 |
| `src/Quizware.Application/Abstractions/IMatchNotifications.cs` | Outbox port |
| `src/Quizware.Infrastructure/Outbox/OutboxMatchNotifications.cs` | Outbox writer |
| `src/Quizware.Domain/Tournament/MatchSeeding.cs` | Auto-seed grouping |
| `src/Quizware.Api/Controllers/v1/{Matches,LiveMatch,Scores,Standings,Stages}Controller.cs` | Phase 9–10 API |
| `src/Quizware.Api/Controllers/v1/QualificationController.cs` | Phase 11 target (501 stubs) |
| `src/Quizware.Api/Contracts/V1/LiveMatch/LiveMapper.cs` | Live contract mapping |
| `src/Quizware.Infrastructure/Identity/AdminUserSeeder.cs` | Seeded admin email/password constants (value not recorded here) |
| `tests/Quizware.Api.IntegrationTests/MatchTestHarness.cs` | Fixture builder for match tests |
| `postman/Quizware.postman_collection.json`, `postman/README.md` | Hand-formatted — edit as text (L-011) |
| `docs/Implementation-Plan.md` (repo root) | Phase 11 at ~line 586; gitignored |

`[FACT]` `QuizApp-9AMM/` and `QuickBuzz/` are nested git repositories.

## 7. Open questions (blocking marked)

- **Q-007** `[OPEN]` — add a target-participant field to `PassQuestionRequest` so
  OperatorChoice passing works? Until then it falls back to clockwise (D-037,
  `[ASSUMED]` acceptable — V-011).
- **Q-008** `[OPEN]` — add API endpoints for segment-template play settings
  (`MaxPassCount`, topic selection) and stage match-play settings
  (`AllowSegmentReorderDuringMatch` …)? Which phase? Today they are domain-only,
  so live segment reorder is unreachable via API (L-014).
- **Q-009** `[OPEN]` — register `JsonStringEnumConverter` globally (breaking
  numeric callers) or keep numeric enums? Settle before TS client regen/Angular.
- **Q-010** `[OPEN]` **blocks T-027** — add default Incorrect scoring rules for
  AudioVisual and Sequence (and what values)? Without them, matches with those
  segments cannot be marked Ready on defaults.
- **Q-011** `[OPEN]` — open a PR for the 15 unmerged commits (`2431cb2` …
  `774264c`)? Only if the user asks.
- **Q-006** `[OPEN]` — is the `DifficultyMixJson` percentage map right for
  Angular? Unchanged, not blocking yet.
- **Q-003** `[OPEN]` — other AI tools needing adapters (V-001). **Q-005**
  `[OPEN]` — real auth for buzzer presses (Phase 14).

Verification queue (`TASKS.md`): V-001, V-006 (docker compose / CI not run),
V-007 (tests not run on SQL Server via Testcontainers), V-008 (LocalDB
specifically), V-010, V-011, V-012.

## 8. Do not retry

- **L-011** — don't `json.load`/`json.dump` the Postman collection; edit as text.
- **L-012** — `pkill -f 'Quizware.Api'` kills your own shell; kill by PID.
- **L-013** — don't pipe newman into `head` when exporting JSON.
- **L-014** — live segment reorder 409s: nothing can enable it via API yet.
- **L-015** — enum contract fields reject strings; send numbers.
- **L-016** — match setup: `Draft` initial state; segment needs a template of
  its format; stage-segment add returns 200; AV/Sequence lack Incorrect rules.
- **L-017** — alias the app's `ValidationException` against FluentValidation's.
- **L-004** — verify commit state with `git log`/`git status`, never trust a
  brief. **L-007/L-010** — upsert pre-checks on the full unique key.
- L-001–L-003, L-005, L-006, L-008, L-009 — see `LESSONS.md`.

## 9. Environment and commands

`[FACT]` Two environments (details: `PROJECT.md` §Environment):
- **User's Windows 10 PC** — PowerShell/Git Bash, root
  `C:\Sharique\Projects\Personal\QuizApp`, LocalDB `(localdb)\MSSQLLocalDB`,
  DB `Quizware-Dev`. Still valid for the user.
- **Cloud Claude Code container** (this session) — Linux, root
  `/home/user/Quizware`, .NET SDK at `$HOME/.dotnet`. Docker daemon started
  manually for SQL Server 2022 (`quizware-sql`, port 1433, `Quizware-Dev`); the
  container, dockerd, and the API were all stopped/removed at session end. SA
  password and JWT key were ad-hoc env values — not recorded. GitHub push works.

```bash
export PATH=$HOME/.dotnet:$HOME/.dotnet/tools:$PATH DOTNET_ROOT=$HOME/.dotnet DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet build Quizware/Quizware.slnx -c Release /warnaserror        # 0 warnings/errors
dotnet test Quizware/Quizware.slnx --no-build -c Release            # 421/421
dotnet ef migrations has-pending-model-changes --project src/Quizware.Infrastructure --startup-project src/Quizware.Api
# API vs SQL Server: env ConnectionStrings__Default, Jwt__Issuer, Jwt__Audience, Jwt__SigningKey,
#   ASPNETCORE_URLS=http://localhost:5299, ASPNETCORE_ENVIRONMENT=Development
cd Quizware && npx --yes newman run postman/Quizware.postman_collection.json   # expect exactly 5 failures
```

Seeded admin credentials: `AdminUserSeeder.DefaultEmail` / `DefaultPassword`
constants — read them there; never copy values into `context/`.

## 10. Where to read more

| File | For |
|---|---|
| `TASKS.md` | Work queue (T-023 next), open questions, verification queue |
| `DECISIONS.md` | D-001 – D-044 with reasoning; D-029 superseded by D-044 |
| `LESSONS.md` | What already failed — **read before proposing an approach** |
| `PROJECT.md` | Stack, repo map, glossary, both environments, conventions |
| `HISTORY.md` | Timeline of checkpoints |
| `sessions/2026-09-28-01-phases-9-10-gaps-postman.md` | This session: Phases 9–10, gaps, Postman, SQL Server run |
| `sessions/2026-09-10-01-phase8-question-selection-engine.md` | Phase 8 |
| `sessions/2026-09-08-02-phase7-tournament-configuration.md` | Phase 7 + first Postman collection |
| `_meta/SPEC.md` | How to save and resume context |

**To checkpoint your own session:** `/save-context` in Claude Code, or in any
other tool: *"Save the context per `context/_meta/SPEC.md`."*
