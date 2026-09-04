# TASKS

Work items (`T-###`), open questions (`Q-###`), and things that need checking
before they can be relied on. Done items stay — they are the record of what was
already tried. Format: `_meta/SPEC.md` §6.4.

**Last updated:** 2026-09-04 (S-2026-09-04-02)

---

## Active work

- **T-001** `IN-PROGRESS` · Confirm the Phase 0 requirement assumptions
  - Why: `docs/new-system/06-Development-Roadmap.md` §6.1 places the project at
    Phase 0, and §6.5 lists ~18 points where the design had to assume an answer.
    Phase 1 (domain model) should not start until these are settled — the domain
    model encodes them.
  - Where: `docs/new-system/06-Development-Roadmap.md` §6.5
  - Update (S-2026-09-04-02): several of the widest-impact points are now answered
    directly in the docs rather than left as assumptions — tie-break format
    default (D-011), disqualification approver and Judge-role removal (D-013),
    API hosting (D-014), buzzer device count (D-014). Still genuinely open: shared
    vs per-program question bank, whether a disqualified team's score is zeroed or
    kept-and-excluded, whether a team may play more than one match per stage,
    whether negative totals are allowed (roadmap §6.5 records "Yes, configurable"
    for negative totals per row 7 — `[UNVERIFIED]` whether that specific row is a
    genuine per-program-owner decision or another AI-assisted default; check
    against the doc directly). None of this has real-world stakeholder sign-off —
    it is still one person iterating with an AI. See Q-001.
  - Blocked by: Q-001 (needs the user for final sign-off; several sub-points no
    longer block on it)

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

- **T-007** `TODO` · Commit the staged solution scaffolding
  - Why: `git status` shows the entire `QuizApp/src|tests|tools` tree and the
    updated `QuizApp.slnx` as staged (`git add` has run) but not committed. Only
    one commit exists in the outer repo (`bf8cb06`), made before this scaffolding
    existed.
  - Where: outer repo root
  - Blocked by: nothing technical — needs the user's go-ahead per the "commit only
    when asked" convention (see `PROJECT.md` §Conventions).

## Open questions

- **Q-001** `OPEN` · Which of the ~18 Phase 0 assumptions do you confirm?
  - Blocks: further Phase 1 domain-model work that depends on the still-open
    sub-points below. Does **not** block T-002 any longer — the user scaffolded
    the solution structure without waiting on this.
  - The full table with the design's assumed answer for each is at
    `docs/new-system/06-Development-Roadmap.md` §6.5. `[UNVERIFIED→partially
    resolved]` as of S-2026-09-04-02: open question 8 (disqualification approver),
    8a (Judge role), 11 (hosting), and 12 (buzzer device count) now have answers
    written directly into the docs (D-011, D-013, D-014) rather than left as
    assumptions. Still genuinely open, widest blast radius on the domain model:
    shared vs per-program question bank (§6.5 Q1); whether a disqualified team's
    score is zeroed or kept-and-excluded (Q3); whether a team may play more than
    one match per stage (Q5). Row 7 (negative totals) reads "Yes, configurable per
    program" in the current doc — `[UNVERIFIED]` whether this reflects genuine
    stakeholder sign-off or another AI-assisted default; check the doc directly
    before treating it as confirmed.
  - `[OPEN]` No real-world stakeholder sign-off has happened on any of these — this
    remains one person iterating with an AI, per the brief for S-2026-09-04-02.

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

- **Q-004** `OPEN` · Commit the staged solution scaffolding now, or leave it staged?
  - Blocks: T-007.
  - `[FACT]` `git status` shows all of `QuizApp/src|tests|tools` plus the modified
    `QuizApp.slnx` staged but not committed, as of S-2026-09-04-02.
  - Recommendation: commit it — per D-002/D-006's own logic, uncommitted work does
    not survive a machine or account switch any better than uncommitted context
    does.

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

- **V-005** · `[ASSUMED]` Commits are made only when the user explicitly asks.
  - Check: ask before the first commit.
  - Matters for: every session. Cheap to confirm, annoying to get wrong.
  - Note (S-2026-09-04-02): the one commit in the repo (`bf8cb06`) was not
    reported as user-requested in this session's brief, so it was presumably made
    directly by the user outside the AI's view — consistent with this assumption,
    not a contradiction of it, but the AI side of any future session should keep
    asking rather than assuming a precedent now exists for it to commit unasked.

## Closed

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
  - Note: these files are `git add`-staged but **not committed** — see T-007.

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
