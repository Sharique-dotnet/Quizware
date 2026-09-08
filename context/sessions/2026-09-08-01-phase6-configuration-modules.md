# S-2026-09-08-01 · phase6-configuration-modules

## Metadata
- **Date:** 2026-09-08
- **Tool/model:** Claude Sonnet 5 (Claude Code), context-keeper subagent
- **Source:** `## CONVERSATION BRIEF` handed to the subagent, covering Phase 6
  sub-phases 6a through 6f in full technical detail. The brief's claim that
  "nothing from 6b–6f was committed" was checked against the repository and
  found stale for 6a–6d — see Correction below.

## What we set out to do

Implement Phase 6 — Configuration modules — of `docs/Implementation-Plan.md`,
sub-phase by sub-phase, per direct user instruction each time: "start Phase
6a...", "start Phase 6b...", "start Phase 6c...", "start Phase 6d...", "start
Phase 6e add 6f... Don't plan, start implementing." Each sub-phase after 6a
followed the plan-then-implement workflow from `CLAUDE.md` (What/Why/Where/
Affects, phased, one-line commit message per phase) except 6e/6f, where the
user explicitly waived planning.

## What happened

### 6a — Program management (P6-01–P6-06)
Introduced `IAppDbContext` (`Application/Abstractions/IAppDbContext.cs`) as
the single persistence port every Application-layer MediatR handler uses —
this is the architectural foundation the rest of Phase 6 builds on. Real
`ProgramsController` handlers replaced the Phase 5 501-stubs, backed by
`Application/Programs/` commands and queries. `Program.UpdateDetails` added
to the domain entity. Live-verified via `.http` requests against a running
`dotnet run` instance and LocalDB. Found and fixed a real bug during live
testing: `UpdateFormats` used case-sensitive `Enum.Parse<QuestionFormatCode>`
against a client-supplied string, and the `.http` file's own example used
uppercase `"MCQ"` — would have thrown an unhandled 500. Fixed to
`Enum.TryParse(..., ignoreCase: true, ...)` with a clean `ValidationProblem`
on failure. Committed `717b96f`.

### 6b — Users and roles (P6-07–P6-10)
The user asked for this as "Phase 6b with Program management" but the plan
document names 6b as "Users and roles" — proceeded on the corrected basis
since the user did not object. `AuthController`/`AdminController` extended
*directly* (no MediatR) because their core entities — `AppUser`, `AppRole`,
`ProgramUser` — are Infrastructure-only Identity types, not Domain types on
`IAppDbContext`. This is the first instance of what became D-019. Delivered:
logout, `me`, select-program, display-token, change-password on
`AuthController`; invite/list/assign-roles/deactivate/reset-password on
`AdminController`. `IJwtTokenService.GenerateAccessToken` gained optional
`programId`/`expiresIn` parameters to support display tokens and
select-program flows. Committed `ae94bca`.

### 6c — Teams (P6-11–P6-13)
Team CRUD, status changes (with mandatory reason), and Excel import
(validate → report → commit) via `Application/Teams/` (`CreateTeam`,
`UpdateTeam`, `SetTeamImages`, `ChangeTeamStatus`, `DeleteTeam`,
`GetTeamById`, `ListTeams`, `GetTeamHistory`) plus a new
`Infrastructure/Imports/TeamExcelParser.cs` (ClosedXML 0.105.1 — the first
Excel library added to the codebase; format-only parsing, no business rules).
`Team.UpdateDetails`/`SetImages`/`Delete` added to the domain entity.
Import logic (`ImportBatch`/`ImportBatchRow`) lives directly in
`TeamsController` rather than Application, since those types are
Infrastructure-only — second instance of D-019.

Found a real, pre-existing schema bug while writing a test that inserted an
active `MatchParticipant`: the `CK_MP_Removal` check constraint literal was
`"Status = 1 OR (...)"`, assuming `ParticipantStatus.Active == 1`, but the
enum has no explicit values so `Active == 0` — the constraint would have
rejected every legitimate active-participant insert. Fixed the literal to
`Status = 0` in `TournamentConfigurations.cs` and generated/applied migration
`FixMatchParticipantRemovalCheckConstraint`.

Also identified and resolved an ambiguity from Phase 6a's own test fixtures:
a `ProgramSetting("Teams","MaxTeams")` key used as example test data could be
mistaken for a second team-cap mechanism. Confirmed `Program.MaxTeams` (a
typed column, unused since Phase 4) as the real one and added
`Program.SetMaxTeams` to wire it up — recorded as L-009 so this isn't
re-litigated later. Committed `41e45bd`.

### 6d — Topics and tags (P6-14)
Topic (parent/child hierarchy, cycle-checked via
`TopicVisibility.EnsureParentIsValidAsync`, 100-hop defensive bound) and Tag
CRUD, shared-vs-per-program scoping. `Topic`/`Tag` gained `UpdateDetails`/
`Delete`. `TopicConfiguration` gained a real self-referencing FK
(`ParentTopicId`, `DeleteBehavior.Restrict`); `TagConfiguration` gained a
unique index. Migration `AddTopicParentForeignKeyAndTagUniqueIndex`.

Found via a failing integration test (`Create_DuplicateNameInSameScope_
ReturnsConflict`, actual 500 vs expected 409): none of the four handlers
(Create/Update × Topic/Tag) pre-checked name uniqueness before insert, so the
DB's own unique-index violation surfaced as a raw unhandled exception. Fixed
by adding explicit `AnyAsync` pre-checks in all four handlers, throwing the
already-mapped `InvalidStateTransitionException`. This is the first instance
of what became L-007. Committed `1d86810`.

### 6e — Media (P6-15) and 6f — Question bank (P6-16–P6-21)
Delivered together per explicit instruction ("start Phase 6e add 6f...
Don't plan, start implementing" — the only sub-phase where the usual
plan-first workflow was waived).

**6e:** `IFileStorage` port (Application/Abstractions), `LocalFileStorage`
implementation (Infrastructure/Media), magic-byte + extension + size
validation (`MediaValidation.cs` — jpg/jpeg/png/gif/mp3/wav/mp4/webm
allow-list, 25 MB cap; these limits are this implementation's own numbers,
not sourced from any design doc — recorded as D-021, `[ASSUMED]`, confirm
with the user before treating as fixed), SHA-256-based deduplication (same
bytes uploaded twice return the same `MediaAsset` with `WasDeduplicated =
true`, verified in `MediaEndpointTests.Upload_SameBytesTwice_Deduplicates`).
A renamed-executable-as-.jpg upload is correctly rejected by the magic-byte
check (`Upload_RenamedExeAsJpg_IsRejected`), not just by extension.

**6f:** All 10 question formats (MCQ, Buzzer, Passing, Card, Choice,
RapidFire, TieBreaker, Sequence, AudioVisual, VisualRapidFire) got real
Create commands under `Application/QuestionBank/Commands/`, sharing
`QuestionCommon.cs` helpers (`EnsureTopicAndTagsExistAsync`,
`ResolveReplacementTargetAsync`, `ApplyVersioning`). A deliberate design
decision (D-020): there is no separate Update endpoint — `PUT
{formatCode}/{id}` deserializes into the same per-format Create request and
calls the same Create command with an optional `ReplacesQuestionId`, so
create and update can never validate differently; the superseded question is
soft-deleted if unused or retired if used (FR-3.10). `Question` (TPT base)
gained `LinkSupersedes`, `Delete`, and text normalization for future
duplicate detection. All format subclasses' `Create()` factories were
extended with previously-hardcoded fields now made configurable (e.g. McqQuestion
gained `allowMultipleCorrect`, `shuffleOptions`, `negativeMarkingEnabled`).
Reads go through a new parallel `QuestionDto` hierarchy
(`Application/QuestionBank/Dtos/`) — Application cannot reference Api's
`QuestionResponse` types — mapped to API contracts by the new
`QuestionResponseMapper.cs` via a switch expression on DTO runtime type.
Excel import (validate → commit) was deliberately scoped to MCQ only
(D-022); the other 9 formats' import templates are explicitly deferred, not
forgotten, via `Infrastructure/Imports/McqQuestionExcelParser.cs`.

Found and fixed three more contract bugs while wiring the controller's
per-format `ToCommand` mappers (Phase 5 stubs had never been exercised
against the real domain factories): `CreateChoiceQuestionRequest` was
missing the domain-required `TopicLabel` field entirely;
`CreateRapidFireQuestionRequest` declared a bogus `Options` list (RapidFire
has no options table at all) and was missing `IsHostRead`/`AnswerText`;
`CreateTieBreakerQuestionRequest` had no field to select which of the
domain's three answer modes to use. Fixed all three request contracts and
their matching response contracts (`RapidFireQuestionResponse`/
`TieBreakerQuestionResponse` no longer incorrectly inherit
`OptionBasedQuestionResponse`).

Found and fixed an unmapped-exception bug in `ApproveQuestionCommandHandler`:
`Question.Approve()` throws a plain `InvalidOperationException` when the
question isn't in Draft status — not one of `GlobalExceptionHandler`'s
mapped types, so re-approving an already-approved question would 500. A
pinned Domain test (`QuestionTests.cs:57`) specifically expects
`InvalidOperationException` from the domain method itself, so that method
was left untouched; instead the handler now checks status *before* calling
`Approve()` and throws the already-mapped `InvalidStateTransitionException`.
Recorded as L-008 — second instance of the L-007 pattern, but this time the
fix has to live in the handler rather than as a DB-level pre-check, since the
constraint is a domain invariant, not a unique index.

Migration `AddQuestionDifficultyCheckConstraint` added
(`CK_Question_Difficulty` on `QuestionConfigurations.cs`). A stray upload
artifact from live media-upload testing appeared at
`src/QuizApp.Api/App_Data/media/<guid>.png` in `git status`; removed the file
and added `**/App_Data/` to the root `.gitignore` so this class of artifact
never gets committed by accident.

6e and 6f were left **uncommitted** at end of session (the user's "Don't
plan, start implementing" instruction covered implementation, not a request
to commit — standing policy V-005 still applies).

### Verification
Re-ran the full suite at session end: `dotnet build` → 0 warnings/0 errors;
`dotnet test` → 224 passed, 0 failed (95 Domain, 17 Application, 4
Architecture, 108 Api.IntegrationTests — up from 147 at the Phase 5
checkpoint). Live-verified via `dotnet run` (port 5299) against real LocalDB
(`QuizApp-Dev`) using the seeded admin account for several of the
sub-phases, not just automated tests.

## Correction to the conversation brief (L-004 recurrence)

The brief that drove this checkpoint claimed nothing from 6b–6f was
committed. `git log` showed this was wrong for 6a–6d: `717b96f` (6a),
`ae94bca` (6b), `41e45bd` (6c), `1d86810` (6d) all exist, dated 2026-09-08,
authored directly by `Sharique` — consistent with the standing "AI proposes,
user runs `git commit`" workflow (V-005) having happened outside the brief's
own visibility into the conversation. Only 6e (Media) and 6f (Question bank)
were confirmed actually uncommitted via `git status --short` (52 files,
+6259/-56 lines). This is the same root-cause pattern as the original L-004
entry — a brief's commit-state claim was stale — so L-004 in `LESSONS.md` was
updated rather than a new lesson created.

## What's next

Phase 7 — Tournament configuration (Stage CRUD, segment templates, segment
reordering, scoring/selection/qualification rule management, tie-break rule
management, program readiness validation) — see `TASKS.md` T-016. Before
that, the user should be asked whether to commit 6e+6f (T-015), and
`docs/Implementation-Plan.md`'s own stale "Start here" section should
eventually be re-synced (T-017, low priority).
