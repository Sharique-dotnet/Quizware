# Session S-2026-09-08-02 · phase7-tournament-configuration

**Saved by:** Claude Sonnet 5 (Claude Code) · **Date:** 2026-09-08

Picked up immediately after S-2026-09-08-01 (Phase 6 configuration modules).
Two pieces of work, in order: Phase 7 (Tournament configuration), then a
Postman collection covering everything through Phase 7 — validated by
actually running it against a live API with `newman`, which surfaced and led
to fixing two real bugs.

---

## Part 1 — Phase 7: Tournament configuration (P7-01 through P7-12)

User instruction: "start Phase 7 and complete all tasks at once, provide
single line commit message at the end. Don't plan, start implementing." —
plan-first workflow explicitly waived (same pattern as Phase 6e/6f in the
prior session).

Committed as `3dbc6f2` — "Stage/segment CRUD with reorder, scoring/
selection/qualification/tie-break rule management, IRuleService resolution,
program readiness validation, and the 18-team seed script". `[FACT]`
verified via `git show --stat 3dbc6f2`: 43 files, +2209/-37 lines.

### P7-01 — Stage CRUD + reorder
`StagesController.cs` rewritten from 501 stubs to real MediatR-backed
handlers. New `Application/Tournament/` module:
- `Dtos/StageDtos.cs`, `Dtos/StageMappings.cs`
- `Commands/{CreateStage,UpdateStage,DeleteStage,ReorderStages}.cs`
- `Queries/{ListStages,GetStageById,ValidateStage}.cs`

### P7-02 — Segment template CRUD
`Commands/{CreateSegmentTemplate,UpdateSegmentTemplate,DeleteSegmentTemplate}.cs`.

### P7-03 — `PUT /stages/{id}/segments/reorder`
`Commands/ReorderSegmentTemplates.cs` — requires the full ordered list
(rejects a partial list with 400 `VALIDATION_FAILED`); locked segments keep
their original `OrderIndex` regardless of where they appear in the request.
Algorithm (see D-024): walk target slots 0..n-1; if the segment originally
in slot i is `IsOrderLocked`, it stays; otherwise the next unlocked id from
the request's queue fills the slot.

### P7-04 — `SegmentOrderMode`
Enum already existed from Phase 1 (Fixed/RandomPerMatch/OperatorChoice).
Wired a new `Stage.SetSegmentOrderMode()` mutator + `Commands/SetSegmentOrderMode.cs`.

### P7-05 — `IsOrderLocked` per segment
Already a `StageSegmentTemplate` column from Phase 4; the P7-03 reorder
algorithm is what makes it actually "survive shuffling" for the first time.

### P7-06 — Scoring rule management + reset-to-defaults
New `Application/Rules/` module:
- `Commands/{UpsertScoringRules,ResetScoringDefaults}.cs`
- `Queries/GetRules.cs` (all four `Get*RulesQuery` handlers)
- `Dtos/RuleDtos.cs`, `Dtos/RuleMappings.cs`

`ResetScoringDefaults` wires up `Domain.Scoring.DefaultScoringValues.All` (18
legacy point values, existed since Phase 4 but unused until now) — soft-
deletes existing program-wide rules first, then reseeds.

### P7-07 — Selection rule management
`Commands/UpsertSelectionRules.cs` — operates on the existing
`QuestionSelectionRule` domain entity (Phase 1); the contract DTO calls it
"SelectionRule".

### P7-08 — Qualification rule management
`Commands/UpsertQualificationRules.cs`.

### P7-09 — Tie-break rule management
`Commands/{UpsertTieBreakRules,ResetTieBreakDefaults}.cs`. New
`Domain/Qualification/DefaultTieBreakValues.cs` (mirrors
`DefaultScoringValues`'s pattern) — 3 recommended seed rows from
`docs/new-system/04-Database-Schema.md` §TieBreakRule (League/
StageQualification/MCQ/3 questions/no sudden death; Semi-Final/
MatchRanking/MCQ/3/no; Final/FinalPlacement/MCQ/5/yes).

### P7-10 — `IRuleService` resolution order
New `Application/Rules/Services/{IRuleService,RuleService}.cs` — resolves
the most specific `ScoringRule` (segment beats stage beats program, via the
existing `ScoringRule.Specificity` computed property from Phase 4), throws
`ScoringRuleNotFoundException` (→ 409 `SCORING_RULE_MISSING`) rather than
guessing. Registered in `Application/DependencyInjection.cs` via
`services.AddScoped<IRuleService, RuleService>()`.

### P7-11 — `POST /programs/{id}/validate` readiness check
Upgraded `Application/Programs/Queries/ValidateProgram.cs` (existed since
Phase 6a as a stage-count-only check) to the full per-stage
`STAGE_HAS_NO_SEGMENTS` check the prior checkpoint's `CURRENT.md` had
explicitly flagged as deferred to Phase 7. Also added the single-stage
version via `Tournament/Queries/ValidateStage.cs`, wired to
`POST /stages/{id}/validate`.

### P7-12 — Seed script for the 18-team tournament
New `Infrastructure/Persistence/TournamentSeeder.cs` — creates a "Demo
18-Team Tournament" program (Code `DEMO-18`), 18 teams, 3 stages (League/
Semi-Final/Final), League's 4-segment order (Mcq 5, AudioVisual 3, Buzzer 4,
RapidFire 5 questions), program-wide scoring defaults, 2 qualification rules
(League→Semi-Final, Semi-Final→Final), and one MCQ tie-break rule for
League's StageQualification. Wired into `Program.cs` right after
`AdminUserSeeder.SeedAsync`, same `!IsProduction` guard, idempotent by
`Program.Code`. Deliberately does NOT create `Match` rows with participants
or real gameplay data — scoped to configuration only. Live-verified: 3
stages, 18 teams, 18 scoring rules confirmed after a fresh `dotnet run`.

### Domain changes
Added mutators that Phase 1–6 never needed:
`Stage.{Rename,Reorder,SetSegmentOrderMode,Delete}`,
`StageSegmentTemplate.{Update,Delete}`, `ScoringRule.{UpdatePoints,Delete}`,
`QualificationRule.Update`, `TieBreakRule.{Update,Delete}`,
`QuestionSelectionRule.Update`. All four rule entities were effectively
immutable-after-Create before this session.

### Contract change
`Api/Contracts/V1/Stages/StageContracts.cs`'s `CreateStageRequest` gained
`string StageType = "League"` — the Phase 5 stub had omitted it entirely
even though `Stage.Create` requires a `StageType`; genuine contract gap fix,
given a default so existing callers don't break.

### Real bug found via reorder testing — see D-023
`ReorderStagesCommandHandler` and `ReorderSegmentTemplatesCommandHandler`
initially wrote final `OrderIndex` values directly, which transiently
violated the unique `(ProgramId, OrderIndex)` / `(StageId, OrderIndex)`
indexes mid-batch (EF issues per-row UPDATE statements; both SQL Server and
the SQLite test provider check unique indexes per-statement, not deferred to
end-of-transaction). Fixed with a two-phase reindex: write a temporary
offset (`100_000 + i`) in one `SaveChangesAsync`, then the real values in a
second `SaveChangesAsync`. Caught via a failing integration test before this
ever reached a human tester.

### Testing
Added `tests/QuizApp.Api.IntegrationTests/{StagesEndpointTests.cs,
RulesEndpointTests.cs}` (22 new tests). Updated
`ControllerStubReachabilityTests.cs` to remove the two routes (`GET /stages`,
`GET /rules/scoring`) no longer 501 stubs. No new EF migration needed —
confirmed via `dotnet ef migrations has-pending-model-changes` (all changes
were behavior-only: new mutator/Delete methods using existing
`IAuditable`/`ISoftDeletable` columns).

### Verification (as reported by the brief; not independently re-run this
checkpoint — see V-009)
`dotnet build` → 0 warnings/errors; `dotnet test` → 224 → 237 (22 new
Stages/Rules tests, exact count reconfirmed in Part 2). Live end-to-end
verification against real LocalDB (`QuizApp-Dev`) via `dotnet run` on port
5299: created a program, created a stage, hit `POST /validate` before/after
adding a segment (false→true), hit `ResetScoringDefaults`/
`ResetTieBreakDefaults`, confirmed `TournamentSeeder` output.

---

## Part 2 — Postman collection

User asked: "Create a postman collection to test and run the development
till now. Keep it inside project folder and tracked. Also tell me the order
for testing."

Delivered `QuizApp/postman/QuizApp.postman_collection.json` (Postman
Collection v2.1 schema) and `QuizApp/postman/README.md`. Scope: every
implemented endpoint through Phase 7 — explicitly excludes Phase 8+
(Matches, Live match engine, Scores, Standings, Qualification commit,
Buzzer, Display, Reports), still 501 stubs.

Structure: 10 folders in dependency order — `00 Health`, `01 Auth`,
`02 Programs`, `03 Admin`, `04 Teams`, `05 Topics & Tags`, `06 Media`,
`07 Questions`, `08 Stages`, `09 Rules` — 80 requests total. Uses collection
variables (not a separate environment file) — every request that creates
something has a `pm.test` script that asserts the expected status code and
saves the relevant id/token into `pm.collectionVariables` for the next
request. Three requests (Teams Excel import-validate, Media upload,
Questions MCQ import-validate) need a real file attached manually since
Postman's JSON export can't embed binary fixtures — documented in both the
request `description` fields and the README.

### Validated by actually running it
Installed `newman` via `npx --yes newman` (not previously used in this
environment) and ran the collection folder-by-folder against a live
`dotnet run` instance on port 5299 (skipping the three file-upload
requests). This surfaced two real problems, both fixed and reverified:

1. **Collection-ordering bug** (first Newman run: 22/23 passed): original
   folder order was `00 Health, 01 Auth, 02 Admin, 03 Programs` — but
   `02 Admin`'s "Assign Roles" request needs `{{programId}}`, which only
   exists after `03 Programs`'s "Create Program" runs. Fixed by physically
   swapping folder order to `02 Programs, 03 Admin` and updating the
   README's run-order section.

2. **Backend bug** (second Newman run, folders 00/01/02/03/05/08/09: 57/58
   passed) — same class as L-007, recorded as L-010: `PUT /rules/scoring`
   returned 500 when called right after `POST /rules/scoring/reset-defaults`
   with a body creating a "new" (`Id: Guid.Empty`) rule whose natural key
   (FormatCode=Mcq, Outcome=Correct, ContextKey=null) already matched one of
   the 18 rows `ResetScoringDefaults` had just seeded — violating the DB's
   unique `(ProgramId, StageId, SegmentTemplateId, FormatCode, Outcome,
   ContextKey)` index, unhandled, surfacing as a raw 500.

   Fixed `UpsertScoringRulesCommandHandler` to check for an existing rule by
   natural key (not just by Id) before inserting, updating it instead if
   found. Proactively audited the other three rule-upsert handlers and found
   the identical latent bug in `UpsertQualificationRulesCommandHandler`
   (unique `(ProgramId, FromStageId)`) and `UpsertTieBreakRulesCommandHandler`
   (unique `(ProgramId, StageId, Scope)`) — fixed both the same way.
   `UpsertSelectionRulesCommandHandler` was NOT touched — its underlying
   index (`QuestionSelectionRuleConfiguration`) is non-unique.

   Added a regression test:
   `RulesEndpointTests.UpsertScoring_SameNaturalKeyAsExistingDefault_UpdatesInsteadOf500`.

   Rebuilt (had to `taskkill` a leftover `QuizApp.Api.exe` from an earlier
   `dotnet run` first — locked DLLs blocked the rebuild), reran the full
   test suite (238 passed, 0 failed — 95 Domain, 17 Application, 4
   Architecture, 122 Api.IntegrationTests, up from 237/121), restarted
   `dotnet run` and reran the same Newman folder sequence: 58/58 requests,
   62/62 assertions, all green. **`[UNVERIFIED]` this checkpoint** — not
   independently re-run; see V-009.

---

## Git state as of this checkpoint (verified `[FACT]` via `git log`/`git status`)

Both Phase 7 (`3dbc6f2`) and Phase 6e/6f (`783bc1c`) are **committed** —
contradicting the brief handed to this save, which claimed both were still
uncommitted (see L-004's third recurrence). Only staged-not-committed as of
this checkpoint:
- `QuizApp/postman/QuizApp.postman_collection.json` (new)
- `QuizApp/postman/README.md` (new)
- `QuizApp/src/QuizApp.Application/Rules/Commands/UpsertScoringRules.cs` (modified)
- `QuizApp/src/QuizApp.Application/Rules/Commands/UpsertQualificationRules.cs` (modified)
- `QuizApp/src/QuizApp.Application/Rules/Commands/UpsertTieBreakRules.cs` (modified)
- `QuizApp/tests/QuizApp.Api.IntegrationTests/RulesEndpointTests.cs` (modified — regression test)

Offered commit message (not run — standing policy, V-005): "Add Postman
collection covering all Phase 0-7 endpoints, and fix three rule-upsert
handlers that 500'd on a natural-key collision".

## What's next
1. Ask the user whether to commit the staged Postman + bugfix work (T-018).
2. Phase 8 — Question selection engine (`IQuestionSelector`) — both stated
   prerequisites (P6f, P7) are now done (T-019).
3. Low priority: `docs/Implementation-Plan.md`'s "Start here" section is
   still stale (T-017, unresolved, not newly discovered this session).
4. V-009 — re-run `dotnet build`/`dotnet test` once the locked
   `QuizApp.Api.exe` process (and Visual Studio, if still holding a lock) are
   stopped, to independently confirm the 238-passed claim.
