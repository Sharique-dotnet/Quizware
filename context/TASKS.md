# TASKS

Work items (`T-###`), open questions (`Q-###`), and things that need checking
before they can be relied on. Done items stay — they are the record of what was
already tried. Format: `_meta/SPEC.md` §6.4.

**Last updated:** 2026-09-04 (S-2026-09-04-03)

---

## Active work

- **T-005** `TODO` · Implement Phase 3's cross-cutting concerns
  - Why: T-002 delivered only the project/reference skeleton. Roadmap Phase 3 also
    calls for DI wiring beyond framework defaults, Identity/JWT, a global exception
    handler, Serilog, a FluentValidation pipeline, health checks beyond template
    defaults, and CI. None of these exist yet.
  - Where: `QuizApp/src/QuizApp.Api/`, `QuizApp/src/QuizApp.Infrastructure/`,
    `docs/Implementation-Plan.md` (Phase 3 task IDs)
  - Blocked by: nothing technical; sequencing question below (see note).
  - Note: the roadmap/plan's recommended order is Domain (Phase 1) → ADRs
    (Phase 2) → Skeleton (Phase 3), but the user scaffolded the skeleton (T-002)
    before either Phase 1 or Phase 2 was started. Whoever resumes should decide
    whether to backfill Phase 1/2 first or continue Phase 3 on the skeleton as-is.

- **T-006** `TODO` · Decide whether `QuizApp.BuzzerAgent` references `QuizApp.Modules.Buzzer`
  - Why: The agent needs the serial frame-parsing logic (`DeviceParser`/
    `SerialService`, ported from legacy QuickBuzz per roadmap task `P14-04`).
    Currently it has zero project references — an explicit gap, not an oversight.
  - Where: `QuizApp/tools/QuizApp.BuzzerAgent/QuizApp.BuzzerAgent.csproj`
  - Blocked by: nothing urgent — this is Phase 14 work per
    `docs/Implementation-Plan.md`. Flagged now so it isn't assumed silently later.

- **T-008** `TODO` · Re-sync `docs/Implementation-Plan.md` Phase 0 section with D-015
  - Why: D-015 records Phase 0 as owner-confirmed/skipped, but `[FACT]` verified
    this session that the file on disk (`docs/Implementation-Plan.md` lines
    73–88) still carries the original six-task workshop wording
    (`P0-01`–`P0-06`, "Duration: 1 week"). An edit rewriting this into a short
    "SKIPPED — owner-confirmed" note was reportedly made earlier but is not
    present now — either it was never saved, or it reverted through some
    out-of-band change. Not investigated further this session; not worth the
    time versus just re-applying the edit.
  - Where: `docs/Implementation-Plan.md` lines 73–88
  - Blocked by: nothing — purely needs someone to make the edit and this time
    verify it persisted (re-read the file after writing, not just after the
    tool call reports success).

- **T-009** `TODO` · Decide Phase 1 vs Phase 2 vs Phase 3 sequencing going forward
  - Why: Phase 1 (domain model) is now fully implemented (P1-01–P1-14, see T-010
    Closed). Phase 2 (ADRs) has not been started. Phase 3 (skeleton) was already
    partially done before Phase 1 even started (T-002/T-005) — so the roadmap's
    recommended order (Domain → ADRs → Skeleton) has now been done out of
    sequence in two different ways in two different sessions. Whoever resumes
    should pick: backfill Phase 2 (ADRs) before continuing Phase 3's remaining
    cross-cutting concerns, or proceed with Phase 3 (T-005) directly since the
    domain model it would sit on top of now exists.
  - Where: `docs/Implementation-Plan.md` Phases 2 and 3.
  - Blocked by: nothing technical — a judgment call for whoever resumes.

## Open questions

- **Q-001** `ANSWERED` (S-2026-09-04-03) · Which of the ~18 Phase 0 assumptions do
  you confirm?
  - `[DECIDED]` via D-015: the user is QuizApp's sole owner/stakeholder, so Phase 0
    is treated as owner-confirmed rather than run as an external workshop. All 16
    assumptions in `docs/new-system/06-Development-Roadmap.md` §6.5 are accepted
    as-is and are now `[DECIDED]`, not `[ASSUMED]`. Domain-model work (Phase 1) is
    no longer blocked on this — see T-010 (Closed), which shows Phase 1 has since
    been fully implemented against these assumptions.
  - Residual detail worth keeping visible: rows that previously read as the
    widest blast-radius sub-points — shared vs per-program question bank,
    disqualified-team score handling, one-match-per-stage-or-not, negative
    totals — are covered by the same owner-confirmed acceptance. If a genuine
    second stakeholder is ever brought onto the project (e.g. a co-organiser),
    these rows are the ones worth re-confirming with them specifically, since
    "owner-confirmed" resolved *who* signs off, not independent verification
    against real event operations. See D-015's Consequences for the caveat that
    the Implementation-Plan.md doc text itself hasn't caught up with this
    decision yet (T-008).

- **Q-002** `ANSWERED` · Should `context/` be committed, and should the outer repo get
  its first commit?
  - `[FACT]` Yes — done. See T-004. `git log` shows `bf8cb06` includes `context/`
    in full, made sometime between this session's Part 1 (design doc edits) and
    Part 2 (solution scaffolding) — the scaffolding itself is staged but not yet
    in that commit (see Q-004).

- **Q-003** `OPEN` · Which other AI tools need adapters?
  - Blocks: nothing.
  - `[FACT]` The user named two Claude accounts, Codex, Cursor, "and potentially
    others". Adapters exist for Claude Code (`.claude/`), Cursor
    (`.cursor/rules/`), and the `AGENTS.md` convention, which Codex and several
    other agents read. `[UNVERIFIED]` whether the user's Codex setup reads
    `AGENTS.md` at the repository root. Anything without an adapter still works if
    pointed at `context/_meta/SPEC.md` — the cost of a missing adapter is one
    sentence of prompting, not a failure.

- **Q-004** `ANSWERED` (S-2026-09-04-03) · Commit the staged solution scaffolding
  now, or leave it staged?
  - `[FACT]` It was committed. `git log` as of this session shows the scaffolding
    landed as `893c4a7` and the domain model that followed landed as three
    further commits (`d6171cd`, `4697df7`, `6e6a13e`) plus `2b2bcfb` for the
    turn-order calculator. See T-007/T-010 (Closed). Only `CLAUDE.md` has an
    uncommitted one-line addition as of this session (the new "always refer to
    `context/`" Instructions bullet).

## Verification queue

Things currently tagged `[ASSUMED]` or `[UNVERIFIED]` that will mislead someone if
they stay unchecked.

- **V-001** · `[UNVERIFIED]` Codex reads a root `AGENTS.md` in the user's setup.
  - Check: open this repo in Codex and ask "what should you read first?" If it
    does not mention `context/CURRENT.md`, the adapter needs a different filename.
  - Matters for: Q-003, and for the cross-tool guarantee generally.

- **V-003** · `[UNVERIFIED]` A SQL Server instance is available for development.
  - Check: ask the user, or look for a connection string once Phase 3 creates
    `appsettings.Development.json`.
  - Matters for: Phase 4 (schema and migrations).

- **V-005** · Resolved as `[DECIDED]` (S-2026-09-04-03) · Commits are made only
  when the user explicitly asks; the AI proposes a plan and a commit message but
  does not run `git commit` itself.
  - `[FACT]` `CLAUDE.md` now has an explicit "Instructions" section (added by the
    user directly, verified present on disk) stating this in writing: "Never
    commit the change on your own until asked," plus "always provide plans in
    phases, after each phase meaningful single line commit message," separate
    commit messages for API vs Angular changes, and a What/Why/Where/Affects
    format for every plan. This is no longer an inferred convention — it is
    written policy in the repo.
  - Note (S-2026-09-04-03): despite this, `git log` shows several commits
    (`893c4a7`, `471a60a`, `2b2bcfb`, `d6171cd`, `4697df7`, `6e6a13e`) made during
    or around this and prior sessions' work, all authored directly by the user
    (`Sharique`) — consistent with the policy (the AI proposed messages, the user
    ran the commits), not a contradiction. A prior brief for S-2026-09-04-0x
    claimed the Phase 1 domain-model work was still uncommitted at session end;
    that claim was **stale** by the time this checkpoint ran — verified `[FACT]`
    against `git log` this session. Lesson recorded as L-004.

## Closed

- **T-010** `DONE` · Implement Phase 1 — the domain model (P1-01 through P1-14)
  - Delivered in S-2026-09-04-03, verified `[FACT]` against the repository this
    session (89 `.cs` files under `QuizApp/src/QuizApp.Domain/`, 23 under
    `QuizApp/tests/QuizApp.Domain.Tests/`, `dotnet test` on the Domain test
    project → 95 passed, 0 failed; `dotnet build` on the full solution → 0
    warnings/0 errors). All committed as `d6171cd`, `4697df7`, `6e6a13e` on top
    of `2b2bcfb` (P1-02, from an earlier session).
  - Common/ (P1-01): `BaseEntity`, `ITenantScoped`, `IAuditable`,
    `ISoftDeletable`. Enums/ (P1-03): all plan-listed enums plus several more
    added on demand while building later tasks (`QuestionPoolScope`,
    `QuestionStatus`, `QuestionOwnerScope`, `PassDirection`, `CardRevealMode`,
    `SequenceItemKind`, `MediaKind`, `TieBreakAnswerMode`, `StageType`,
    `TeamCountChangePolicy`, `TopicSelectionMode`, `MatchSegmentState`,
    `MatchQuestionState`, `AnswerSource`, `ScoreEventType`, `SeedingMode`,
    `OnStillTiedPolicy`, `TieBreakEventState`, `QualificationReason`).
    `QuestionFormatCode` integer values match the seed ids in
    `docs/new-system/04-Database-Schema.md` §4.3, `[FACT]` verified this session
    — id 4 is an intentional gap, documented in a comment on the enum itself.
  - Teams/ (P1-05): `Team` (status changes require a reason via `ChangeStatus`),
    `TeamMember`. QuestionBank/ (P1-06): `Question` abstract base (TPT, per
    D-009) plus 10 sealed subclasses, `QuestionOption`, `SequenceItem`,
    `VisualRapidFireItem`, `Topic`, `Tag`, `MediaAsset`. `AudioVisualQuestion`
    enforces non-null `MediaAssetId` and non-empty `AnswerText` in its factory
    method, per D-009's original NOT-NULL reasoning. `QuestionTag` was
    deliberately **not** modeled here — left for an EF many-to-many mapping in
    Phase 4, since P1-06's acceptance criteria didn't require a join entity.
  - Tournament/ (P1-07): `Stage`, `StageSegmentTemplate`, `Match`,
    `MatchParticipant` (implements `ITurnOrderParticipant`, integrates with
    `TurnOrderCalculator` from P1-02). `Match.Start(int activeParticipantCount)`
    enforces >=2 active participants via the new `InsufficientParticipantsException`.
  - Gameplay/ (P1-08): `MatchSegment` (`Open()` enforces one-open-segment-per-match
    by taking the sibling list as a parameter; `Reorder()` throws
    `SegmentNotReorderableException` if locked or not Pending), `MatchQuestion`
    (`Activate()` enforces one-active-question-per-segment the same way),
    `AnswerRecord` (`MarkReversed`), `MatchEvent` (append-only,
    `NextSequenceNumber` enforces strictly-increasing sequence numbers).
  - Scoring/ (P1-09): `ScoringRule`, `ScoreEvent` (immutable once created —
    `Reverse()` creates a **new**, opposite-signed event rather than mutating,
    marks the original `IsReversed = true`, throws if reversed twice),
    `TeamMatchScore` (`ApplyAnswer`/`ApplyAdjustment`, the incremental read
    model), `TeamStageScore`.
  - Qualification/ (P1-10): `QualificationRule`, `StageQualification`,
    `TieBreakRule` (`Criteria` stored as `IReadOnlyList<string>`, not raw JSON —
    JSON serialization is an EF-layer concern deferred to Phase 4),
    `TieBreakEvent` (`Resolve()` enforces "a resolved tie must name its
    resolution method," and additionally requires non-blank `Notes` when
    `ResolutionMethod` is Manual, matching a CHECK constraint in the schema doc),
    `TieBreakParticipant`.
  - Buzzer/ (P1-11): `BuzzRankingCalculator` — pure function ported from
    QuickBuzz's `DeviceApiController` ranking logic; lowest non-zero press time
    wins, `0` means "no press" and sorts last.
  - Tournament/SegmentOrderResolver.cs (P1-12): pure function — `Fixed` returns
    template order; `RandomPerMatch` shuffles unlocked segments deterministically
    via a stored `System.Random(seed)` while locked segments keep their template
    position; same seed always reproduces the same order (tested).
  - Qualification/TieBreakCriteriaEvaluator.cs (P1-13): pure function taking
    pre-normalized criterion values (caller ensures higher-is-always-better per
    criterion) and an ordered criterion-name list; narrows the tied group
    criterion-by-criterion and reports which criterion decided it, or still-tied.
  - Common/Exceptions/ (P1-14): all 7 domain exceptions exist —
    `InvalidStateTransitionException`, `InsufficientParticipantsException`,
    `QuestionPoolExhaustedException`, `ScoringRuleNotFoundException`,
    `UnresolvedTieException`, `SegmentNotReorderableException`,
    `FormatInUseException` — plus `NoActiveParticipantsException` from P1-02.
    Deliberate pattern across the whole phase: exceptions were introduced
    incrementally, when the entity that needed them was built, rather than all
    stubbed out upfront under P1-14.
  - Test project: `QuizApp.Domain.Tests` uses xUnit + FluentAssertions 6.12.2
    (added as a package reference this session, `[FACT]` verified present in
    the committed `.csproj`). `QuizApp.Domain.csproj` itself has **zero** NuGet
    package references, `[FACT]` verified — the domain layer stays dependency-free.
  - Full detail: `sessions/2026-09-04-03-phase0-owner-confirmed-phase1-domain.md`.

- **T-007** `DONE` · Commit the solution scaffolding
  - `[FACT]` Corrected this session: the scaffolding was committed as `893c4a7`
    ("Add initial project structure with API, application, domain, and
    infrastructure layers"), which already existed in `git log` before this
    checkpoint ran. The prior open state (staged-not-committed) reflected an
    earlier point in the project's history that had already been resolved by
    the time of this save; this task is closed rather than left open on stale
    information. See Q-004.

- **T-001** `DONE` (via D-015) · Confirm the Phase 0 requirement assumptions
  - Resolved in S-2026-09-04-03: not through a stakeholder workshop (the
    original plan for this task) but through an explicit decision that the user
    is QuizApp's sole owner and there is no separate stakeholder to run a
    workshop with. See D-015. The Implementation-Plan.md doc itself has not yet
    been re-edited to reflect this — see T-008.

- **T-000** `DONE` · Build the cross-AI context preservation system
  - Delivered in S-2026-09-04-01: `context/` structure, `_meta/SPEC.md` as the
    single procedure, the `context-keeper` subagent, `/save-context` and
    `/load-context`, and adapters for Claude, Cursor, and the `AGENTS.md`
    convention. Detail: `sessions/2026-09-04-01-context-system-bootstrap.md`.

- **T-002** `DONE` · Scaffold the QuizApp solution skeleton
  - Delivered in S-2026-09-04-02: `QuizApp/QuizApp.slnx` now lists 10 projects
    (`QuizApp.Domain`, `.Application`, `.Infrastructure`, `.Modules.Buzzer`,
    `.Api`, plus 4 test projects and the `QuizApp.BuzzerAgent` tool), all
    targeting `net10.0`, wired with project references exactly matching
    `docs/new-system/02-Architecture-Proposal.md` §2.4's dependency rule
    (`[FACT]`, verified by grepping every `.csproj`). Subfolder layout created per
    §2.4 with `.gitkeep` placeholders (34 folders). Template placeholder files
    (`Class1.cs`, `WeatherForecast*.cs`, `UnitTest1.cs`) deleted. `dotnet build`
    reported 0 warnings/0 errors; build artifacts for all 10 projects confirmed
    present on disk this session. Full detail:
    `sessions/2026-09-04-02-tpt-roles-scaffolding.md`.
  - Residual work is **not** part of this task — see T-005 (Phase 3 cross-cutting
    concerns) and T-006 (BuzzerAgent reference decision), which are genuinely new
    work, not loose ends of T-002.
  - Note: subsequently committed as `893c4a7` — see T-007 (Closed).

- **T-003** `DONE` · Exercise the context system on a real working session
  - This save (S-2026-09-04-02) is the exercise: a real merge against existing
    `CURRENT.md`/`DECISIONS.md`/`TASKS.md`, a stale-claim correction (§2 of
    `CURRENT.md`), six new decisions appended without disturbing D-001–D-008, and
    a `git log` discrepancy found and reconciled rather than trusted blindly. The
    merge/supersede/compaction paths described in `_meta/SPEC.md` §4.4 worked as
    specified.

- **T-004** `DONE` · Decide whether `context/` is committed to git
  - `[FACT]` Resolved by action, not by an explicit conversation: `git log` shows
    one commit, `bf8cb06 "Initial commit: project scaffolding, docs, and AI
    context system"`, whose tree includes all of `context/`. Answers Q-002.
    Nobody reported making this commit through `/save-context`, so record it as an
    out-of-band action rather than a documented decision with reasoning.

- **V-002** · `[FACT]` The .NET 10 SDK is installed on this machine.
  - Verified S-2026-09-04-02: `dotnet --list-sdks` → `8.0.421` and `10.0.400`,
    both under `C:\Program Files\dotnet\sdk`. Used to build all 10 QuizApp
    projects (T-002) with 0 warnings/0 errors.

- **V-004** · Resolved as D-014 · Deployment is to an on-premises venue server
  with no cloud dependency, `DeviceCount` configurable with a default of 3.
  - Was `[ASSUMED]`; promoted to `[DECIDED]` in S-2026-09-04-02 via the user
    confirming open questions 11 and 12 in
    `docs/new-system/06-Development-Roadmap.md` §6.5. See D-014.
