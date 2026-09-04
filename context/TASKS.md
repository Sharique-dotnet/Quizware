# TASKS

Work items (`T-###`), open questions (`Q-###`), and things that need checking
before they can be relied on. Done items stay — they are the record of what was
already tried. Format: `_meta/SPEC.md` §6.4.

**Last updated:** 2026-09-04 (S-2026-09-04-01)

---

## Active work

- **T-001** `TODO` · Confirm the Phase 0 requirement assumptions
  - Why: `docs/new-system/06-Development-Roadmap.md` §6.1 places the project at
    Phase 0, and §6.5 lists ~18 points where the design had to assume an answer.
    Phase 1 (domain model) should not start until these are settled — the domain
    model encodes them.
  - Where: `docs/new-system/06-Development-Roadmap.md` §6.5
  - Blocked by: Q-001 (needs the user)

- **T-002** `TODO` · Scaffold the QuizApp solution skeleton
  - Why: `QuizApp/QuizApp.slnx` is `<Solution />` — an empty file. Nothing can be
    built until the four projects exist. Roadmap Phase 3.
  - Where: `QuizApp/`, target structure in
    `docs/new-system/02-Architecture-Proposal.md` §2.4
  - Blocked by: T-001, and V-002 below (is the .NET 10 SDK actually installed?)

- **T-003** `TODO` · Exercise the context system on a real working session
  - Why: The system was designed and bootstrapped in one session (S-2026-09-04-01).
    Its merge, supersede, and compaction paths have been specified but never run
    against a second save. The first real `/save-context` on top of existing files
    is the actual test.
  - Where: `context/`, `.claude/commands/save-context.md`
  - Blocked by: nothing — this happens naturally on the next checkpoint.

- **T-004** `TODO` · Decide whether `context/` is committed to git
  - Why: The outer repo has no commits yet and an empty `.gitignore`. The context
    system is worthless across machines and accounts if it is not committed, but
    that is the user's call, not an assumption to make silently.
  - Where: `.gitignore`, outer repo
  - Blocked by: Q-002 (needs the user)

## Open questions

- **Q-001** `OPEN` · Which of the ~18 Phase 0 assumptions do you confirm?
  - Blocks: T-001, and therefore T-002 and all of Phase 1.
  - The full table with the design's assumed answer for each is at
    `docs/new-system/06-Development-Roadmap.md` §6.5. The ones with the widest
    blast radius on the domain model: shared vs per-program question bank (§6.5
    Q1); whether a disqualified team's score is zeroed or kept-and-excluded (Q3);
    whether a team may play more than one match per stage (Q5); whether negative
    totals are allowed (Q7).
  - `[OPEN]` Not asked in this session — the session was about the context system,
    not the design. Raised here so the next agent surfaces it rather than assuming.

- **Q-002** `OPEN` · Should `context/` be committed, and should the outer repo get
  its first commit?
  - Blocks: T-004.
  - `[FACT]` The outer repo is on `master` with no commits (`git log` reports "your
    current branch 'master' does not have any commits yet"). `[FACT]` `main` is the
    configured base branch for pull requests, which does not exist yet either.
  - Recommendation: commit `context/`. It is the whole point of the system that it
    travels with the repository to another machine or account.

- **Q-003** `OPEN` · Which other AI tools need adapters?
  - Blocks: nothing.
  - `[FACT]` The user named two Claude accounts, Codex, Cursor, "and potentially
    others". Adapters exist for Claude Code (`.claude/`), Cursor
    (`.cursor/rules/`), and the `AGENTS.md` convention, which Codex and several
    other agents read. `[UNVERIFIED]` whether the user's Codex setup reads
    `AGENTS.md` at the repository root. Anything without an adapter still works if
    pointed at `context/_meta/SPEC.md` — the cost of a missing adapter is one
    sentence of prompting, not a failure.

## Verification queue

Things currently tagged `[ASSUMED]` or `[UNVERIFIED]` that will mislead someone if
they stay unchecked.

- **V-001** · `[UNVERIFIED]` Codex reads a root `AGENTS.md` in the user's setup.
  - Check: open this repo in Codex and ask "what should you read first?" If it
    does not mention `context/CURRENT.md`, the adapter needs a different filename.
  - Matters for: Q-003, and for the cross-tool guarantee generally.

- **V-002** · `[UNVERIFIED]` The .NET 10 SDK is installed on this machine.
  - Check: `dotnet --list-sdks`
  - Matters for: T-002. The whole design targets .NET 10.

- **V-003** · `[UNVERIFIED]` A SQL Server instance is available for development.
  - Check: ask the user, or look for a connection string once Phase 3 creates
    `appsettings.Development.json`.
  - Matters for: Phase 4 (schema and migrations).

- **V-004** · `[ASSUMED]` Deployment is to an on-premises venue server with no
  cloud dependency.
  - Check: ask the user. The design records this as an assumption, not a
    confirmation (`docs/new-system/06-Development-Roadmap.md` §6.5 Q11).
  - Matters for: hosting, SignalR scale-out, and the buzzer agent's network path.

- **V-005** · `[ASSUMED]` Commits are made only when the user explicitly asks.
  - Check: ask before the first commit.
  - Matters for: every session. Cheap to confirm, annoying to get wrong.

## Closed

- **T-000** `DONE` · Build the cross-AI context preservation system
  - Delivered in S-2026-09-04-01: `context/` structure, `_meta/SPEC.md` as the
    single procedure, the `context-keeper` subagent, `/save-context` and
    `/load-context`, and adapters for Claude, Cursor, and the `AGENTS.md`
    convention. Detail: `sessions/2026-09-04-01-context-system-bootstrap.md`.
