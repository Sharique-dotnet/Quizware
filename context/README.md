# Cross-AI Context Preservation

`context/` is this project's memory. It exists so you can switch between AI
assistants — two Claude accounts, Codex, Cursor, Copilot, Gemini, whatever comes
next — and pick the work up cold, without re-explaining the project, the
decisions, or what's half-done.

The full procedure is `_meta/SPEC.md`. This file is the short version.

## Starting a session

Point any AI assistant at `context/CURRENT.md` and tell it to read that first.

| Tool | How |
|---|---|
| Claude Code | `/load-context` |
| Anything else | "Read `context/CURRENT.md` and orient me on this project." |

It will tell you the project, the current objective, what's decided, what state
things are in, what to do next, and any question it needs you to answer first.
`AGENTS.md` at the repo root and `.cursor/rules/context-keeper.mdc` already tell
Codex/Cursor-style tools to do this automatically.

## Checkpointing a session

Whenever you want to save progress — before switching tools, before a long chat
gets compacted, or just when you've made real progress:

| Tool | How |
|---|---|
| Claude Code | `/save-context` |
| Anything else | "Save the context per `context/_meta/SPEC.md`." |

It reads the conversation, reads the existing `context/` files, merges in what's
new (never overwrites — contradictions get superseded, with the old entry kept),
and reports back what changed. If nothing material happened, it changes nothing
and says so.

## What's in here

| File | What it's for |
|---|---|
| **`CURRENT.md`** | Start here. The working state, self-contained, capped at 400 lines. |
| `PROJECT.md` | The slow-moving stuff: purpose, architecture, stack, glossary, conventions. |
| `DECISIONS.md` | Every decision, with *why*, and what was rejected. Append-only. |
| `TASKS.md` | The work queue, open questions, and a list of things that are still just assumptions. |
| `LESSONS.md` | What already failed. Read before trying something that sounds obvious. |
| `HISTORY.md` | One line per checkpoint — the timeline. |
| `sessions/` | The full detail behind each `HISTORY.md` line. |
| `_meta/SPEC.md` | The procedure itself. Read this if you're building the equivalent for another tool. |
| `_meta/state.json` | ID counters. Don't edit by hand. |

## Why it looks like this

- **One entry point, size-capped.** `CURRENT.md` is the only file a new agent must
  read, and it stays under 400 lines on purpose — an entry point that grows
  forever stops getting read. Everything else is there when needed, not by default.
- **Confidence tags on every claim.** `[FACT]` · `[DECIDED]` · `[ASSUMED]` ·
  `[UNVERIFIED]` · `[OPEN]`. The dangerous failure mode isn't a gap, it's a
  confident guess written in the same voice as a verified fact. Tags make the
  difference visible at a glance.
- **Nothing is ever deleted.** A reversed decision is marked superseded and kept,
  with the reasoning intact — that's what stops the next agent re-proposing
  something already tried and rejected.
- **Plain Markdown and JSON, nothing proprietary.** No tool's memory feature, no
  database, no chat-history API. Every assistant can read and write these.
- **One procedure, thin adapters.** The save/resume logic lives once, in
  `_meta/SPEC.md`. `CLAUDE.md`, `AGENTS.md`, and `.cursor/rules/` are pointers to
  it, not copies — so there's one place to fix if the process needs to change.

See `DECISIONS.md` D-001 through D-008 for the full reasoning behind each of these.

## Adding support for another tool

Most agentic coding tools (Codex, Aider, and others) already read a root
`AGENTS.md`, which is included here — no extra work needed. For a tool that
doesn't, add a short adapter file in its own convention that says, in essence:
"read `context/CURRENT.md` first; to checkpoint, follow `context/_meta/SPEC.md`."
See `.claude/agents/context-keeper.md` or `.cursor/rules/context-keeper.mdc` as
examples of what "thin" should look like — a page, not a copy of the spec.

## Worked example

This project's own `context/` is a live, non-trivial example: `CURRENT.md` down
to `sessions/2026-09-04-01-context-system-bootstrap.md` show the format actually
in use, including a superseded-decision case in `DECISIONS.md`.
