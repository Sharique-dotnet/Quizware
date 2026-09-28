# S-2026-09-10-01 · phase8-question-selection-engine

**Date:** 2026-09-10 · **Tool/model:** Claude Sonnet 5 (Claude Code) ·
**Source:** `## CONVERSATION BRIEF` handed to the context-keeper subagent.

---

## What we set out to do

Continue the Quizware rebuild. The user's instruction for this session:
"Analyze the current status of the code and then start Phase 8 and complete
all tasks at once, provide single line commit message at the end. Don't
plan, start implementing" — i.e. implement Phase 8 (Question selection
engine) in full, skipping the project's normal plan-first workflow per
explicit user waiver, ending with a proposed one-line commit message (not an
actual `git commit` — that stays the user's own action per standing policy).

## What happened

1. Verified starting state: the repo root had been renamed —
   `C:\Sharique\Projects\Personal\QuizApp\` is the actual outer repo, with the
   ASP.NET Core solution nested at `Quizware/` (not the flat `Quizware/` root
   the prior checkpoint's `CURRENT.md`/`PROJECT.md` assumed). `git log` at
   session start showed a clean tree, with Phase 7 + the Postman collection +
   the 3 rule-handler bugfixes (T-018 from the prior checkpoint) already
   committed out-of-band by the user as `d2b16cc` ("Add Postman collection,
   README, and robust upsert handling"), plus a chain of rename commits
   (`bcf4c70`–`859813c`) that renamed every project to the final `Quizware`
   naming, added Scalar for OpenAPI docs, and added README/LICENSE.
2. Read `docs/Implementation-Plan.md` lines 498–517 for Phase 8's exact
   scope (P8-01 through P8-10, depends on P6f + P7, interface
   `IQuestionSelector`).
3. Implemented the full engine (see Decisions and Changes below), without
   the usual plan-first pause, per the explicit user waiver.
4. Verified: `dotnet build` (0 warnings/errors), `dotnet test` (245/245
   passing, up from 238), `dotnet ef migrations has-pending-model-changes`
   (none needed), and a live end-to-end run against real SQL Server LocalDB
   (`Quizware-Dev`) — logged in as the seeded admin, created and approved a
   real MCQ question, called the new preview endpoint, got back a correct
   response.
5. At session end, `git status --short` showed the Phase 8 changes as
   uncommitted (per standing policy — never commit without being asked). A
   system-provided `gitStatus` reminder injected after the final reply then
   showed these had since been committed by the user out-of-band as
   `31d2f22`, using the exact one-line message the assistant had proposed —
   the fourth recurrence of the L-004 pattern.

## Decisions made (see `DECISIONS.md` D-025–D-033 for full reasoning)

- **D-025** — `OptionOrderJson` computed eagerly at `SelectAndReserveAsync`
  time and returned on the DTO, but not persisted onto `MatchQuestion` until
  Phase 9's `Activate()` (the domain's existing, documented place for it).
- **D-026** — Tag filtering (`TagFilterJson`) left unimplemented: no
  `QuestionTag` join entity exists anywhere in the schema; would need a
  migration, out of Phase 8's scope.
- **D-027** — `QuestionSelectionRule.TopicFilterJson` (a dead column since
  Phase 7) wired up end-to-end: `Update()`, `SelectionRuleAppDto`,
  `RuleMappings.ToDto()`, `UpsertSelectionRulesCommand`, the API contract,
  `RulesController`.
- **D-028** — `QuestionSelectionRule.Specificity` +
  `IRuleService.ResolveSelectionRuleAsync` (nullable — unlike
  `ResolveScoringRuleAsync`, which throws — because a selection rule is
  optional).
- **D-029** — Cross-match reservation locking: any `MatchQuestion` row with
  `State != Released`, in any match, excludes that question from every other
  match's draw pool, on top of `QuestionUsageHistory`-based repeat-policy
  exclusion.
- **D-030** — `DifficultyMixJson` format convention invented this session: a
  flat percentage map (e.g. `{"Easy":60,"Hard":40}`), `[ASSUMED]`, no prior
  convention or design doc backed it. See Q-006.
- **D-031** — `IQuestionSelector`'s write methods (`SelectAndReserveAsync`,
  `ReleaseReservationsAsync`) never call `SaveChangesAsync` — the caller
  (Phase 9's future match-start handler) owns the transaction boundary,
  matching P9-02's "Start is one transaction" criterion.
- **D-032** — Seeded PRNG uses `new
  Random(unchecked((int)(seed ^ (seed >> 32))))`, with every candidate pool
  deterministically sorted (`OrderBy(q => q.Id)`) before the weighted draw,
  so "same seed → identical draw" (P8-04) is actually guaranteed regardless
  of EF Core/SQL row-return order.
- **D-033** — `PreviewSelectionQuery` rewritten to delegate entirely into
  `IQuestionSelector.PreviewAsync`, superseding the Phase-7-era stub (whose
  own doc comment said it existed only until Phase 8's real selector
  existed). `SegmentTemplateId` added to both the query and the API contract.

## Changes to the repo

All under `Quizware/`, paths relative to
`C:\Sharique\Projects\Personal\QuizApp\Quizware\`.

**New files:**
- `src/Quizware.Application/Selection/SelectionModels.cs` — `SelectionRequest`,
  `SelectedQuestion`, `SelectionResult` records.
- `src/Quizware.Application/Selection/IQuestionSelector.cs` — interface:
  `PreviewAsync`, `SelectAndReserveAsync`, `ReleaseReservationsAsync`.
- `src/Quizware.Application/Selection/QuestionSelector.cs` — the full engine.
- `tests/Quizware.Api.IntegrationTests/SelectionEngineTests.cs` — 7 new tests.

**Modified files:**
- `src/Quizware.Domain/Tournament/QuestionSelectionRule.cs` — `Specificity`
  computed property; `Update()` gained an optional `topicFilterJson` param.
- `src/Quizware.Application/Rules/Dtos/RuleDtos.cs` — `TopicFilterJson` added
  to `SelectionRuleAppDto`.
- `src/Quizware.Application/Rules/Dtos/RuleMappings.cs` — maps
  `TopicFilterJson`.
- `src/Quizware.Application/Rules/Commands/UpsertSelectionRules.cs` — passes
  `dto.TopicFilterJson` through.
- `src/Quizware.Application/Rules/Services/{IRuleService,RuleService}.cs` —
  `ResolveSelectionRuleAsync` added.
- `src/Quizware.Application/Rules/Queries/PreviewSelection.cs` — rewritten to
  delegate to `IQuestionSelector.PreviewAsync`; gained `SegmentTemplateId`.
- `src/Quizware.Application/DependencyInjection.cs` — registered
  `IQuestionSelector -> QuestionSelector`.
- `src/Quizware.Api/Contracts/V1/Rules/RuleContracts.cs` — `SelectionRuleDto`
  gained `TopicFilterJson`; `SelectionPreviewRequest` gained
  `SegmentTemplateId`.
- `src/Quizware.Api/Controllers/v1/RulesController.cs` — mapping updates.

**Not touched (deliberately, Phase 9 territory):** `MatchesController.cs`
remains entirely `501 NotImplemented` stubs.

**No new EF Core migration** — confirmed via `dotnet ef migrations
has-pending-model-changes --project src/Quizware.Infrastructure
--startup-project src/Quizware.Api`.

## Problems hit

- Initial test-file compile failure in `SelectionEngineTests.cs`: used
  `LoginRequest`/`TokenResponse` (from `AuthController.cs`) without importing
  `Quizware.Api.Controllers.v1` — fixed by adding the using; not a design
  bug, a missed import when copying sibling test files' bodies.
- No FK constraints are configured in EF for `MatchQuestion.MatchId`/
  `QuestionId` or `QuestionUsageHistory.MatchId`/`StageId`/`TeamId` (only
  indexes declared, verified by reading `GameplayConfigurations.cs`) — this
  simplified test setup, since arbitrary `Guid.NewGuid()` values could be
  used for `MatchId`/`StageId` without seeding a full Stage→Match chain.
- The question-creation API contract differs from what a naive read of the
  domain model would suggest: needs `formatCode` in the body (redundant with
  the route) and `difficultyLevelId` (an integer, not the enum name string),
  and option objects use `text`/`displayOrder`, not `optionText`. Discovered
  by trial-and-error against the live API, then confirmed against the
  already-existing Postman collection's "Create MCQ Question" request.

## What is left

- Phase 9 — the match engine — is next (T-020). Prerequisites (Phase 7,
  Phase 8) are both done.
- Q-006 (new): confirm the `DifficultyMixJson` percentage-map convention
  (D-030) with the user, or against a design doc, before Angular's
  rule-configuration screens are built against it.
- T-006 (Phase 14, not urgent): `Quizware.BuzzerAgent` reference decision.
- Postman collection does not yet include Phase 8's
  `POST /rules/selection/preview` endpoint — not done this session, not
  blocking.

## Verbatim details worth keeping

Commands used/confirmed this session:
```bash
cd Quizware && dotnet build Quizware.slnx                                    # 0 warnings, 0 errors
dotnet test Quizware.slnx --no-build                                          # 245/245 passing
dotnet ef migrations has-pending-model-changes --project src/Quizware.Infrastructure --startup-project src/Quizware.Api   # confirmed no migration needed
cd src/Quizware.Api && dotnet run --no-build --urls http://localhost:5299     # live LocalDB verification
netstat -ano | grep ':5299' | grep LISTENING | awk '{print $5}'               # find the dev-server PID to kill it cleanly afterward
taskkill //PID <pid> //F
```

Live-verification response (`POST /rules/selection/preview`, real LocalDB,
one approved MCQ question in the pool):
```json
{"poolSize":1,"eligibleAfterFilters":1,"eligibleAfterRepeatPolicy":1,
 "canSatisfy":true,"difficultyMixAchievable":{"Medium":1}}
```

Proposed (and, per the post-reply `gitStatus` reminder, since actually run by
the user) one-line commit message:
"Add question selection engine: pool building, repeat-policy exclusion,
seeded weighted draw with difficulty mix and topic spread, MatchQuestion
reservation, and the widen-then-fail fallback ladder" — landed as `31d2f22`.
