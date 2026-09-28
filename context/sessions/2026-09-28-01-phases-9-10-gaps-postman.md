# S-2026-09-28-01 · phases-9-10-gaps-postman

**Date:** 2026-09-28 · **Tool/model:** Claude Opus 5.5 (Claude Code, cloud
container) · **Source:** `## CONVERSATION BRIEF` handed to the context-keeper
subagent, cross-checked against the repository (`git log`, `git status`, file
listings, targeted greps) on 2026-09-28.

---

## What we set out to do

Continue implementing `docs/Implementation-Plan.md` phase by phase. This
session covered Phase 9 (match engine) and Phase 10 (scoring and standings).
The user's last request: close the Phase 9 gaps against the plan (auto-seed,
per-format handlers, pass direction, Choice topics, sudden death, outbox),
settle whether Operators may reverse answers, refresh Postman and `context/`
(two phases out of date), and tell the user what to do next.

## What happened

1. Phase 9a–9f was implemented and merged into master via PR #1, which the
   user opened from the GitHub UI (merge `3f945cf`). The user then updated
   `CLAUDE.md` on master (`177a353`: no commit trailers, phased plans).
2. The branch picked up master (`be81545`) and Phase 10 was built: scoring
   engine (`2431cb2`), score endpoints with ProgramAdmin-only adjust and
   ledger recalculation (`738e947`), tie-break criteria service (`d6cdc1f`),
   standings (`3aaea4e`); master was merged in again (`10888f1`).
3. Gap-closing pass on Phase 9: reversal restricted to ProgramAdmin
   (`e609f0f`), per-segment turn rotation (`37be8bb`), one handler per format
   (`8868378`), passing rules (`d09195a`), Choice board (`d713288`), sudden
   death (`d09cfa4`), auto-seed (`a64da74`), outbox (`ce330a5`), Phase 9
   required tests (`5d580be`).
4. Postman collection refresh (`774264c`): folders 10–13, 156 requests,
   199 assertions, `adminPassword` fixed.
5. First run of Phases 9–10 against a real SQL Server 2022 (Docker in the
   cloud container): all migrations applied; newman ran the full collection
   with only the 5 documented file-attachment failures.
6. Commits were made by the assistant under the user's explicit instruction
   for this work ("test the changes, when all tests pass commit, then move to
   the next phase"). Branch pushed; 15 commits ahead of `origin/master`; no PR
   open for them.

Keeper cross-check (2026-09-28): `[FACT]` `git status` clean, branch
`claude/workflows-project-status-bsgu4d` in sync with its remote, HEAD
`774264c`, `git rev-list --count origin/master..HEAD` = 15. `[FACT]`
`LiveMatchController.cs:121` uses `Policies.CanAdjustScore` for reversal.
`[FACT]` `Gameplay/Formats/` holds the ten handlers plus base/helper;
`Infrastructure/Outbox/OutboxMatchNotifications.cs` exists; latest migration
is `20260908084206_AddQuestionDifficultyCheckConstraint`.

## Decisions made

- D-034 reversal ProgramAdmin-only (applies D-013; docs corrected).
- D-035 per-segment turn rotation; no turn holder for Buzzer/RapidFire.
- D-036 one `IQuestionFormatHandler` per format.
- D-037 passing direction, limits, reveal-if-all-pass, OperatorChoice fallback.
- D-038 Choice board from TopicChoice labels; exclusive topics consumed.
- D-039 sudden death closes when one team leads after equal turns.
- D-040 auto-seed endpoint with Random/Rank/Snake.
- D-041 transactional outbox via `IMatchNotifications`.
- D-042 event-sourced scoring ledger with transactional read models.
- D-043 tie-break criteria service.
- D-044 selector locked-set rule — **supersedes D-029**.

## Changes to the repo (paths under `Quizware/`)

- **Application:** `Gameplay/` (MatchSetup, MatchEventLog, LiveStateBuilder,
  MatchCompletion, TurnRotation, LiveRules, TopicPicks, ScoringResolver,
  AnswerResultBuilder, SuddenDeath, `Commands/*` incl. AutoSeedMatches,
  ReverseAnswer, PassQuestion, SelectTopic, Disqualify/Reinstate),
  `Gameplay/Formats/*`, `Scoring/*` (IScoringEngine, ScoringEngine,
  MatchScoreboard, Standings, AdjustScore, RecalculateScores, queries),
  `Qualification/TieBreakCriteriaService`, `Abstractions/IMatchNotifications`,
  `Selection/QuestionSelector` fix, `DependencyInjection`. Deleted:
  `LiveQuestionFormats.cs`.
- **Infrastructure:** `Outbox/OutboxMatchNotifications.cs` + DI. No new EF
  migration in Phases 9–10 (`has-pending-model-changes` clean).
- **Domain:** Match (TouchSetup, UpdateDetails, MarkReady, Delete,
  ReviseResult, Abandon refuses terminal), MatchParticipant, MatchSegment
  (Renumber, AdjustPlannedQuestionCount, MakeSuddenDeath), MatchQuestion
  (SelectTopic, Reopen…), TeamMatchScore/TeamStageScore (Rebuild etc.),
  `StageSegmentTemplate.ConfigurePlay`, `Stage.ConfigureMatchPlay`,
  `Tournament/MatchSeeding.cs`.
- **Api:** MatchesController (all but Preflight), LiveMatchController (all but
  Snapshot/Restore), ScoresController, StandingsController,
  `GET stages/{id}/standings`, `Contracts/V1/LiveMatch/LiveMapper.cs`.
- **Docs (gitignored, D-016):** `docs/new-system/05-API-Design.md` reverse
  route role → ProgramAdmin; `docs/Implementation-Plan.md` P9-10 row.
- **Tests:** MatchTestHarness, MatchesEndpointTests, LiveMatchLifecycleTests,
  LiveSegmentsAndServingTests, LiveAnswersAndScoringTests,
  LiveDisqualificationTests, LiveQuestionFormatsTests, LivePassingTests,
  LiveChoiceRoundTests, LiveSuddenDeathTests, MatchOutboxTests,
  Phase9RequiredTests, ScoringEngineTests, ScoresEndpointTests,
  TieBreakCriteriaServiceTests, StandingsEndpointTests, SelectionEngineTests
  regressions, plus domain tests. 421/421 passing.
- **Postman:** `postman/Quizware.postman_collection.json` + `README.md`.

## Problems hit

- json.dump reformatted the whole Postman collection (L-011).
- `pkill -f 'Quizware.Api'` killed the calling shell (L-012).
- newman piped into `head` left a stale JSON report (L-013).
- Live reorder 409 because nothing can enable
  `AllowSegmentReorderDuringMatch` via API (L-014).
- Enum-typed contract fields need numbers (L-015).
- Match-setup gotchas: Draft initial state, template needed per segment
  format, 200 on stage-segment add, no default Incorrect rules for AV and
  Sequence (L-016).
- FluentValidation `ValidationException` clash (L-017).

## What is left

- Phase 11 (T-023) — next.
- Outbox delivery (T-024), remaining 501 stubs (T-025), live-contract gaps
  (T-026), default AV/Sequence Incorrect rules (T-027, Q-010).
- Keeper observation: option shuffle may not be persisted at serve (T-028).
- Questions for the user: Q-007 (pass target field), Q-008 (play-settings
  endpoints), Q-009 (enum converter), Q-010 (AV/Sequence rules), Q-011 (PR for
  the 15 commits), Q-006 (unchanged).

## Verbatim details worth keeping

```
dotnet build Quizware.slnx -c Release /warnaserror   -> 0 warnings, 0 errors
dotnet test                                          -> 421/421 (17 App, 131 Domain, 4 Arch, 269 Api.IntegrationTests)
newman (real SQL Server 2022)                        -> 5 failures: team import validate/commit, media upload, MCQ import validate/commit
```

- Auto-seed request: `POST /programs/{pid}/matches/auto-seed {stageId, seedingMode: Random|Rank|Snake}`.
- Reversal route: `POST /matches/{id}/live/answers/{aid}/reverse` — `Policies.CanAdjustScore`.
- Answer after a pass: send `PassNumber` = number of passes; outcome `PassedCorrect`, context key `AfterPass`.
- Numeric enum example: `PassDirection` 1 = Clockwise.
- Alias for the name clash:
  `using ValidationException = Quizware.Application.Common.Exceptions.ValidationException;`
