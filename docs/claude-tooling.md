# Claude Code agents and skills for this project

This project uses Claude Code's project-scoped extension points to encode
project-specific workflows: **subagents** (delegated, single-purpose Claude
sessions with their own tools/prompt) and **skills/slash commands**
(procedures the main session follows itself when invoked with `/name`). Both
live under `.claude/` at the repository root and are checked into git, so
they're available to anyone working on this repo with Claude Code.

This doc exists because `docs/` is entirely gitignored (see
`context/DECISIONS.md` D-016) — `.claude/agents/` and `.claude/commands/`
themselves are tracked in git and don't need this file to *exist*, but
nothing else explains *why* each one exists or what problem in this specific
codebase it addresses. Update this file whenever an agent or command here is
added, renamed, or retired.

## Agents (`.claude/agents/*.md`)

Invoked via the `Agent` tool (`subagent_type: "<name>"`), or automatically
when a task matches their `description`.

| Agent | What it does | Why this project needs it |
|---|---|---|
| `context-keeper` | Checkpoints the current conversation into `context/` so any AI tool/account can resume without chat history. | This repo's own continuity mechanism (`CLAUDE.md`'s "Read the context first" convention) depends on it; invoked by `/save-context`. |
| `domain-rule-guardian` | Checks pending changes under `Quizware/src/` against the binding decisions in `context/DECISIONS.md` (e.g. D-009 Table-Per-Type questions, D-019 MediatR-vs-controller routing, D-023 two-phase reindex). | The architecture is deliberately settled at design level (`context/CURRENT.md` §5) and several of its rules aren't compiler-enforced — a violation compiles fine and only shows up as a design regression later. |
| `ef-migration-reviewer` | Diffs a new EF Core migration against `docs/new-system/04-Database-Schema.md`. | The schema doc is gitignored and nothing else keeps it in sync with real migrations; drift here means the doc silently stops being trustworthy. |
| `api-contract-auditor` | Checks controller/contract changes against the frozen route and shape definitions in `docs/new-system/05-API-Design.md`. | That contract is explicitly frozen so the future Angular client can be generated from it (see `docs/new-system/05-API-Design.md`'s own framing) — an undocumented rename breaks codegen without breaking the .NET build. |
| `buzzer-integration-tester` | Exercises the buzzer press-ingestion flow (`POST /sessions/{id}/presses`) against a live API using a mock payload. | The real Buzzer Agent needs on-premises RS485 hardware (`docs/adr/ADR-005-buzzer-port-null-default.md`) that isn't available in this dev environment, so this is the only practical way to test that path pre-Phase-14. |
| `postman-regression-runner` | Runs `Quizware/postman/Quizware.postman_collection.json` via `newman` against a live instance and reports pass/fail per folder, respecting the documented run order. | This exact process already found a real backend bug once (`context/LESSONS.md` L-010) that a unit test missed — worth repeating deliberately, not just once by accident. |

## Skills / slash commands (`.claude/commands/*.md`)

Invoked as `/name [arguments]` in a Claude Code session.

| Command | What it does | Why this project needs it |
|---|---|---|
| `/load-context` | Reads `context/` and reports project state, reconciled against the repo. | Entry point for every new session per `CLAUDE.md`. |
| `/save-context` | Extracts a brief from the current conversation and hands it to `context-keeper` to merge into `context/`. | Checkpoint mechanism used before switching tools/accounts or before compaction. |
| `/explain-tournament-config` | Translates a stage/segment/rule configuration (from the API or LocalDB) into a plain-language description. | The tournament engine is fully data-driven by design (`docs/new-system/02-Architecture-Proposal.md`, replacing the legacy system's hardcoded turn order and views) — a misconfigured tournament is a data bug, and data bugs are far easier to catch by reading a sentence than by reading rule rows. |
| `/sync-schema-doc` | Generates an EF Core migration and updates `docs/new-system/04-Database-Schema.md` in the same pass. | Same drift risk as `ef-migration-reviewer`, but proactive — do the update at generation time instead of catching it after the fact. |
| `/sync-postman` | Diffs the live `swagger.json` against `Quizware/postman/Quizware.postman_collection.json` and adds missing requests to the correct folder. | Keeps the manual process that built the original 80-request collection (`context/CURRENT.md`) repeatable per phase instead of ad hoc. |

## Conventions these follow

- **Report, don't fix.** The three review/audit agents (`domain-rule-guardian`,
  `ef-migration-reviewer`, `api-contract-auditor`) and the two test-runner
  agents (`buzzer-integration-tester`, `postman-regression-runner`) never edit
  source themselves — they report findings back to the calling session, which
  decides what to do with them. This matches how `/code-review` in this
  environment works generally, and avoids an agent applying a "fix" that
  contradicts a deliberate design decision it didn't know about.
- **Never start a stray `dotnet run`.** Several of these need a live API
  instance; all of them check for one and ask rather than starting their own,
  because a background `dotnet run` left running is exactly what caused the
  file-lock problems recorded in `context/LESSONS.md` V-009 and hit again
  during the QuizApp→Quizware rename.
- **`context/` and `docs/` are read as ground truth, never edited by review
  agents.** Only the two `sync-*` commands write to `docs/`, and only the
  specific section their migration/endpoint touched.
