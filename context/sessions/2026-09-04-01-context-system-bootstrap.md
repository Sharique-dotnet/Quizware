# S-2026-09-04-01 · Context system bootstrap

| | |
|---|---|
| **Date** | 2026-09-04 |
| **Tool** | Claude Code (Claude Opus 5), Windows 10, PowerShell + Git Bash |
| **Source** | Live conversation — the assistant that did the work also wrote this record |
| **Context root** | `C:\Sharique\Projects\Personal\QuizApp` |

---

## What we set out to do

Build a reusable context-preservation subagent. The user works across two Claude
accounts, Codex, Cursor, and other tools, and loses the working context of a
project every time they switch — re-explaining the project, the decisions made,
the current progress, and the pending work by hand.

Requirement in one line: **any** AI assistant, on any platform or account, should
be able to read a `context/` directory at the repository root and continue the work
without the original chat history.

## What happened

### Repository inspection

Done first, because the user asked for existing conventions to be matched rather
than a new structure imposed. Findings:

- `[FACT]` No `.claude/` directory, no agents, no `CLAUDE.md`, no `AGENTS.md`, no
  `.cursorrules` anywhere in the repository. There was no existing convention to
  match, so this session establishes one.
- `[FACT]` The user has global Claude skills at `~/.claude/skills/` (`graphify`
  and others, mostly symlinks into `~/.agents/skills/`). Frontmatter convention
  observed there: `name` and `description`. The new agent and commands follow it.
- `[FACT]` The outer repo is on branch `master` with **no commits yet**, and an
  empty `.gitignore`.
- `[FACT]` `QuizApp-9AMM/` and `QuickBuzz/` are **nested git repositories** with
  their own `.git` directories. This shaped the context-root rule in SPEC §1.1 —
  the root resolves to the *outermost* repo, so one `context/` serves all three
  sub-projects instead of three competing ones.
- `[FACT]` `QuizApp/QuizApp.slnx` contains exactly `<Solution />` — the new system
  has no code yet.
- `[FACT]` `docs/new-system/` holds six design documents totalling ~5,500 lines,
  plus two legacy technical analyses (~1,600 lines).

### Design

The architecture that emerged, and the reasoning, are recorded as D-001 through
D-008 in `DECISIONS.md`. The three that most shaped the result:

1. **Single procedure, thin adapters (D-006).** `context/_meta/SPEC.md` holds the
   whole save/resume procedure. Every tool config points at it and defers to it.
   Four copies of a procedure become four different procedures within a month, and
   the files would then be written in inconsistent formats by different tools —
   the exact fragmentation the system exists to remove.

2. **The main assistant extracts, the subagent merges (D-007).** See L-002. A
   subagent spawned fresh cannot see the parent conversation, so a bare
   "context-saving subagent" would invent a plausible session. `/save-context`
   runs in the main session where the conversation actually is, extracts a
   structured brief, and hands it to `context-keeper` for the merge.

3. **Point, do not duplicate (D-001, L-003).** `docs/new-system/` is already
   durable. `context/` covers only what lives nowhere but a chat window.

### Delivered

| Path | Purpose |
|---|---|
| `context/_meta/SPEC.md` | The procedure. Single source of truth, ~7 invariants, save §4, resume §5, templates §6 |
| `context/CURRENT.md` | Entry point — the file a new agent reads first |
| `context/PROJECT.md` | Stable orientation: purpose, glossary, repo map, stack, environment, conventions |
| `context/DECISIONS.md` | D-001 – D-008 with reasoning and rejected alternatives |
| `context/TASKS.md` | T-###, Q-###, and a V-### verification queue |
| `context/LESSONS.md` | L-001 – L-003 |
| `context/HISTORY.md` | The timeline |
| `context/sessions/` | This file |
| `context/README.md` | Human-facing workflow guide |
| `.claude/agents/context-keeper.md` | The subagent |
| `.claude/commands/save-context.md` | `/save-context` — extract, delegate, report |
| `.claude/commands/load-context.md` | `/load-context` — read, reconcile, orient |
| `AGENTS.md` | Cross-tool entry point (Codex and others) |
| `.cursor/rules/context-keeper.mdc` | Cursor adapter, `alwaysApply: true` |
| `CLAUDE.md` | Claude Code entry point |

## Problems hit

- **Heredoc failure.** Writing `SPEC.md` via `cat > file <<'EOF'` in the Bash tool
  died with `line 131: unexpected EOF while looking for matching '`. Switched to
  the Write tool; identical content succeeded. Recorded as L-001 because it will
  recur for any agent working in this repo on Windows.
- **The subagent visibility problem.** Recognized during design rather than after
  a failure. Recorded as L-002 and D-007 because it is the one flaw that would
  have made the whole system untrustworthy while appearing to work.

## What is left

- Q-001 — the ~18 Phase 0 design assumptions still need the user's confirmation.
  Not raised in this session, which was about the context system rather than the
  design. It is the item most likely to block real work.
- Q-002 — whether `context/` gets committed, and whether the outer repo gets its
  first commit. Nothing was committed in this session.
- V-001 — whether the user's Codex reads a root `AGENTS.md`. Untested.
- T-003 — the merge, supersede, and compaction paths are specified but have never
  run against existing files. The next `/save-context` is the first real test.

## Verbatim details worth keeping

```
$ git log --oneline -10
fatal: your current branch 'master' does not have any commits yet

$ cat QuizApp/QuizApp.slnx
<Solution />

# heredoc failure, L-001
/usr/bin/bash: -c: line 131: unexpected EOF while looking for matching `'
```

Line counts at bootstrap, for future drift checks:
`01-Analysis-Findings.md` 436 · `02-Architecture-Proposal.md` 781 ·
`03-PRD-API.md` 689 · `04-Database-Schema.md` 1956 · `05-API-Design.md` 1409 ·
`06-Development-Roadmap.md` 623 · `QuizApp-9AMM-Technical-Analysis.md` 1492 ·
`QuickBuzz-Technical-Analysis.md` 123.
