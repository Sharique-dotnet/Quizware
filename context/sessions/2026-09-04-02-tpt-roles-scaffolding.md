# S-2026-09-04-02 · TPT questions, tie-break, role consolidation, solution scaffolding

**Date:** 2026-09-04 · **Tool/model:** Claude Sonnet 5 (Claude Code), context-keeper subagent
**Source:** `## CONVERSATION BRIEF` provided by the main session, cross-checked against the repository. Where the brief's account of pre-session history conflicts with what the repo shows, this file notes it; where the brief describes work not directly visible on disk (the sequence of document edits), it is recorded as `[UNVERIFIED]` narrative and the *resulting* file content is recorded as `[FACT]`.

---

## What we set out to do

Continue iterating on the six `docs/new-system/` design docs, then begin scaffolding the actual .NET solution once the docs stabilized.

## What happened

### Part 1 — design doc iteration

Per the brief, this conversation made five substantive rounds of edits to `docs/new-system/01`–`06` and `README.md`:

1. Question storage changed from a single unified `Question` table to **Table-Per-Type**: one shared `Question` base table (identity/lifecycle/classification) plus one child table per format (e.g. `AudioVisualQuestion` with NOT-NULL `MediaAssetId`/`AnswerText` — a guarantee a shared table cannot express). Six of ten formats share `QuestionOption`; `Sequence` and `VisualRapidFire` get their own item tables (`SequenceItem`, `VisualRapidFireItem`). Propagated across all 6 docs + README (schema, PRD, one-route-per-format API design, architecture diagrams, roadmap Phase 9 and Phase 16 mapping).
2. **Configurable question-type order**: `StageSegmentTemplate.OrderIndex` as source of truth, three levels (stage template default, per-match override, live reorder of pending segments), `SegmentOrderMode` enum (`Fixed` default / `RandomPerMatch` from the match's stored RNG seed / `OperatorChoice`), plus `IsOrderLocked` per segment. New finding 1.4h added to the analysis doc: legacy hardcodes this as `window.location.href` redirect chains across 108 Razor views.
3. **Tie-break for the League wildcard boundary**: two phases — Phase 1 ordered non-playing criteria (total score, fewer incorrect, harder questions answered, faster buzz time, head-to-head); Phase 2 (still tied) creates an ordinary `Match` with `MatchKind = TieBreak` for just the tied teams, driven by a new `TieBreakRule` table (format defaults to MCQ, fully configurable to any of the 10 formats, plus question count/difficulty/sudden-death/max-rounds/fallback). The tie-break match runs through the **existing match engine** — no separate gameplay code. New tables: `TieBreakRule`, `TieBreakEvent`, `TieBreakParticipant`. `ScoreCountsTowardStage` defaults false. Finding 1.4i added: legacy `TieBreaker` table/screens exist but are disconnected from qualification; `LeagueRoundScore` resolves ties by row order today.
4. **Question formats confirmed optional by construction**, with one real gap fixed: `FR-1.5` readiness validation previously read as if it required questions in every format; tightened to only check formats actually configured. Added `ProgramQuestionFormat` (per-program enable/disable, UI convenience only, guarded by a `FORMAT_IN_USE` error). Documented three distinct senses of "optional" that must not be conflated: not-configured (no segment row), skippable-on-the-night (`IsOptional=1`, still drawn), disabled-program-wide (`ProgramQuestionFormat.IsEnabled=0`).
5. **Judge role removed entirely**, arrived at incrementally: disqualification approval, answer-reversal approval, score-adjustment approval, and tie-break manual resolution were each narrowed step by step to `ProgramAdmin`-only (previously various combinations of Operator/Judge/ProgramAdmin), ending with an explicit instruction to drop the Judge role from the system altogether. Applied across PRD actors/permission matrix/business rules, API design role columns and the `CanDisqualify`/`CanAdjustScore`/`CanResolveTie` policy table in §5.9 (added a previously-missing `CanResolveTie` row), database schema seeded `AppRole` list and column comments, roadmap open-questions rows 8/8a/6b and Phase 9–11 test deliverables, architecture doc (one unrelated "a remote judge" wording changed to "a remote reviewer"), and README. System now has exactly **7 roles**: SuperAdmin, ProgramAdmin, QuestionAuthor, Operator, Scorer, Display, Auditor.

Also resolved via a user screenshot of the roadmap's §6.5 open-questions table: open question 8 (disqualification approver = ProgramAdmin), open question 11 (API hosting = **local/on-premises venue server, no cloud dependency assumed**; the buzzer agent still pushes outward to the API even though everything is local, since the operator PC's exact network position isn't guaranteed), open question 12 (buzzer device count = **configurable, default 3**, today's hardware count, not a hard limit).

`[FACT]` verified directly in the current repository (2026-09-04, this session):
- `docs/new-system/06-Development-Roadmap.md:296-299,586-590` and `docs/new-system/05-API-Design.md:1401-1405` confirm the Judge role is gone and `ProgramAdmin`/`SuperAdmin` hold sole authority over disqualification, answer reversal, score adjustment, and tie-break resolution.
- `docs/new-system/04-Database-Schema.md` contains 44 occurrences of `SegmentOrderMode|OrderIndex|TieBreakRule|ProgramQuestionFormat|DeviceCount|AudioVisualQuestion` — the TPT/tie-break/order/format-toggle/device-count schema elements described above are present.
- `docs/new-system/02-Architecture-Proposal.md` contains on-premises/local-venue-server hosting language.
- Current line counts: 01=436, 02=781, 03=689, 04=1956, 05=1409, 06=623, README=113 (6,007 total) — higher than the prior session's "~5,500 lines" figure, consistent with this session's additions.

Finally, the user asked for a phased implementation plan synthesizing all six docs. Written to `docs/Implementation-Plan.md` (658 lines, `[FACT]` verified via `wc -l`) — **not** under `new-system/`. 18 phases (0–17, mirroring the roadmap's numbering), 185 numbered tasks with stable IDs (`P9-06` etc.), each with acceptance criteria. Includes a Definition-of-Done checklist, branch naming `feature/P{phase}-{nn}-short-name`, a per-layer test strategy that **explicitly bans the EF Core in-memory provider** (does not enforce constraints), a 9-case test list for `TurnOrderCalculator` (the class replacing the legacy `QuestionNumber % 3` bug), 9 milestones (M1 walking skeleton → M9 production ready), a two-scenario timeline (single dev ~29–30 weeks; two devs in parallel after Phase 5, ~17–19 weeks, with an explicit critical path and split), and a risk table.

### Part 2 — .NET solution scaffolding (verified directly on disk this session)

`QuizApp/QuizApp.slnx` went from the literal `<Solution />` to a populated 10-project solution. `[FACT]`, confirmed by reading the file directly:

```xml
<Solution>
  <Folder Name="/src/">   Api, Application, Domain, Infrastructure, Modules.Buzzer
  <Folder Name="/tests/"> Api.IntegrationTests, Application.Tests, Architecture.Tests, Domain.Tests
  <Folder Name="/tools/"> BuzzerAgent
</Solution>
```

All 10 projects target `net10.0` (`[FACT]`, grepped every `.csproj`). Project references (`[FACT]`, grepped every `.csproj`) exactly match the architecture doc's dependency rule:
- `QuizApp.Domain` — no project references.
- `QuizApp.Application` → Domain.
- `QuizApp.Infrastructure` → Application, Domain.
- `QuizApp.Modules.Buzzer` → Application, Domain.
- `QuizApp.Api` → Domain, Application, Infrastructure, Modules.Buzzer.
- `QuizApp.BuzzerAgent` (tool) → **no project references at all** — open decision, see below.
- Test projects reference their corresponding src project; `QuizApp.Architecture.Tests` has no `.cs` files yet (csproj only) and per the brief will reference all 5 src projects when the NetArchTest dependency-rule tests are written (Phase 9 per the roadmap's numbering, not yet done).

Subfolder layout created inside each project per the architecture doc §2.4, each populated with a `.gitkeep` (34 total, `[FACT]` — 34 `.gitkeep` files appear in `git status`): `Domain/{Common,Programs,Teams,QuestionBank,Tournament,Gameplay,Scoring,Qualification,Buzzer,Enums}`, `Application/{Abstractions,Programs,Teams,QuestionBank,Tournament,Gameplay,Scoring,Qualification,Reporting,Common}`, `Infrastructure/{Persistence,Repositories,Identity,Files,Caching,Outbox}`, `Modules.Buzzer/{Abstractions,Serial,Agent,Manual}`, `Api/{Controllers/v1,Hubs,Middleware,Filters}`.

`dotnet new` template placeholders were deleted: `Class1.cs` (4 classlibs), `WeatherForecast.cs` + `WeatherForecastController.cs` (Api), `UnitTest1.cs` (4 test projects). `[FACT]` confirmed — `find src tools tests -name "*.cs"` this session returns only `src/QuizApp.Api/Program.cs` and `tools/QuizApp.BuzzerAgent/Program.cs` as hand-written files, plus generated `obj/` artifacts from a build.

`dotnet build` was reported as 0 warnings/0 errors across all 10 projects; `[FACT]` consistent with `obj/Debug/net10.0/*AssemblyInfo.cs` build artifacts present for every project. `.NET 10 SDK 10.0.400` confirmed installed this session (`dotnet --list-sdks` → `8.0.421` and `10.0.400`, both under `C:\Program Files\dotnet\sdk`) — this resolves the prior session's V-002.

**Open decision, explicitly flagged and not resolved:** should `QuizApp.BuzzerAgent` reference `QuizApp.Modules.Buzzer` to reuse serial frame-parsing (`DeviceParser`/`SerialService`, ported from legacy QuickBuzz per roadmap task `P14-04`), or reimplement standalone since it deploys as a separate process? Currently no reference either way. This is Phase 14 work; deferred, not urgent, but should not be assumed either way.

## A correction to the prior session's account, found via `git log`

`[FACT]` — the outer repo is **no longer commit-less**. `git log --oneline` shows one commit, `bf8cb06 "Initial commit: project scaffolding, docs, and AI context system"`, authored by `Sharique <60686493+Sharique-dotnet@users.noreply.github.com>` (with `Co-Authored-By: Claude Sonnet 5`). Its tree contains `QuizApp/QuizApp.slnx` as a single line (`<Solution />`) — meaning **this commit predates the Part 2 scaffolding** but **postdates** all of Part 1 (the commit tree already contains `docs/Implementation-Plan.md` and the fully-edited design docs, plus the entire `context/` bootstrap from S-2026-09-04-01). So at some point between Part 1 and Part 2 of this conversation, someone committed everything accumulated so far. The prior session's Q-002 ("should context/ be committed, and should the outer repo get its first commit?") is therefore **answered: yes**, `[FACT]` — though the brief handed to this save did not mention a commit happening, and no `/save-context` ran between Part 1 and the commit, so the merge into `context/` you are reading now is the first time this commit is recorded. Whoever made it did not go through the documented `/save-context` flow — noted, not treated as a problem, since I1 only requires the record to be honest, not that every commit be preceded by a checkpoint.

Current `git status` (`[FACT]`, checked this session): the Part 2 scaffolding (all `src/`, `tests/`, `tools/` files under `QuizApp/`, plus the modified `QuizApp.slnx`) is **staged** (`git add` has run) but **not committed**. Nothing here commits it — that is a decision for the user (see Q-004 below).

## Decisions made this session

Recorded in `DECISIONS.md` as D-009 through D-014:
- D-009 Table-Per-Type question storage
- D-010 Configurable, three-level question-type ordering
- D-011 Tie-break runs as an ordinary Match through the existing engine
- D-012 Question formats are optional at three independent levels
- D-013 Judge role removed; ProgramAdmin/SuperAdmin sole authority over oversight actions
- D-014 On-premises/local hosting, no cloud dependency assumed; buzzer device count configurable, default 3

These supersede nothing in `DECISIONS.md` directly — the file previously only *pointed* at `02-Architecture-Proposal.md` §2.16 for "the twelve architecture decisions" without itemizing them, and TPT/OrderIndex/tie-break-as-match were already named in that pointer text (written by S-2026-09-04-01, evidently after the docs already reflected earlier passes of this same design work). D-009–D-011 give those three their own entries with reasoning and rejected alternatives, which the pointer text did not carry. D-012–D-014 are new subjects not previously named anywhere in `context/`.

## Changes to the repo (this save)

Writes made by this context-keeper run, all under `context/`:
- `CURRENT.md` — rewritten: corrected the stale "no application code written" claim, new objective/state/next-actions, corrected git status, new open questions.
- `DECISIONS.md` — appended D-009–D-014.
- `TASKS.md` — T-002 marked DONE with a residual sub-task for what Phase 3 still lacks; T-001 updated to reflect partial resolution; new T-005 (finish Phase 3 cross-cutting concerns), T-006 (decide BuzzerAgent reference), T-007 (commit the staged scaffolding); Q-001 updated (partially answered); new Q-004 (commit the staged files?); V-002 moved to Closed (verified).
- `PROJECT.md` — repo map updated to show the real `QuizApp/` tree instead of "only QuizApp.slnx"; Tech stack / Environment status lines updated (.NET 10 SDK now `[FACT]` installed); Commands section given the real `dotnet build`/`dotnet sln`/reference commands.
- `HISTORY.md` — appended this session's block.
- `sessions/2026-09-04-02-tpt-roles-scaffolding.md` — this file.
- `_meta/state.json` — counters bumped, `last_session_id` updated.

No file outside `context/` was written by this save.

## Problems hit

None specific to this save. The main interpretive difficulty was reconciling the brief's account of "no commits yet" against `git log` showing one commit already made — resolved by trusting the repository (SPEC §5.6) and recording the discrepancy rather than either silently overriding the brief or silently trusting it.

## What is left

- The staged scaffolding files are not committed (Q-004).
- Phase 3's cross-cutting concerns (DI wiring, Identity/JWT, global exception handler, Serilog, FluentValidation pipeline, health checks, CI) are not implemented — only the project/reference skeleton exists (T-005).
- Phase 1 (domain model) and Phase 2 (ADRs) have not been started, even though the roadmap's own recommended order is Domain → ADRs → Skeleton; the user scaffolded the skeleton first. Not a problem, just a note for whoever resumes.
- The `QuizApp.BuzzerAgent` ↔ `QuizApp.Modules.Buzzer` reference question is open (Phase 14, not urgent).
- Real-world stakeholder sign-off on the roadmap's original ~18 Phase 0 assumptions has still not happened — several of the highest-impact ones now have AI-assisted answers baked directly into the docs (tie-break format, hosting, device count, roles), narrowing what is left, but this is still one person iterating with an AI, not a confirmed customer sign-off (see Q-001).

## Verbatim details worth keeping

`.csproj` `ProjectReference` grep output and `dotnet --list-sdks` output, both reproduced in "Part 2" above, are the exact evidence for the dependency graph and the SDK-installed fact — reproduce these commands rather than re-deriving the graph from prose if it needs re-checking later.
