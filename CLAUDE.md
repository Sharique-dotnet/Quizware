# CLAUDE.md

## Read the context first

This repository carries its own AI memory in `context/`, so work survives
switching between Claude accounts, Codex, Cursor, and other tools.

**Before doing anything else in a new session, read `context/CURRENT.md`.** It
states what this project is, the current objective, what has been decided, where
the work stands, and what to do next — without any chat history.

Or run `/load-context`, which reads it and reconciles it against the repository.

Then as needed:

- `context/TASKS.md` — work queue, open questions, verification queue
- `context/DECISIONS.md` — binding constraints and their reasoning
- `context/LESSONS.md` — **read before proposing an approach**; what already failed
- `context/PROJECT.md` — stack, repo map, conventions
- `context/HISTORY.md` and `context/sessions/` — the timeline and full session detail

Confidence tags: `[FACT]` verified · `[DECIDED]` binding · `[ASSUMED]` unconfirmed
· `[UNVERIFIED]` unchecked · `[OPEN]` unanswered. Anything `[ASSUMED]` or
`[UNVERIFIED]` is not established — verify it or say plainly that you did not.
Where the repository and the context disagree, **the repository is right** and the
context is stale.

## Save the context when checkpointing

Run **`/save-context`** when the user asks to save or checkpoint, before switching
tools or accounts, or before a long conversation is compacted. It extracts the
brief from this conversation and hands it to the `context-keeper` agent, which
merges it into `context/`.

The `context-keeper` agent can also be invoked directly — but it runs in a fresh
process and **cannot see this conversation**, so it must be given a
`## CONVERSATION BRIEF`. Prefer `/save-context`, which does that for you.

Full procedure and file formats: `context/_meta/SPEC.md`. Cross-tool rules:
`AGENTS.md`.

## Repository layout

| Path | What it is |
|---|---|
| `docs/new-system/` | Design docs for the new system, numbered 01–06, read in order |
| `docs/*-Technical-Analysis.md` | Line-by-line audits of the two legacy systems |
| `QuizApp/` | The **new** system (ASP.NET Core). Currently an empty solution |
| `QuizApp-9AMM/` | The **legacy** ASP.NET MVC 4 system being replaced. Nested git repo |
| `QuickBuzz/` | The **legacy** buzzer app, to become an optional module. Nested git repo |
| `context/` | AI-session continuity. Start at `CURRENT.md` |

**Naming trap:** `QuizApp`, `QuizApp-9AMM` and `QuickBuzz` are three different
systems with similar names. See `context/PROJECT.md` §Domain glossary before
editing, or you will change the wrong one.

## Conventions

- `QuizApp-9AMM/` and `QuickBuzz/` are nested git repositories with their own
  history. Commits for the new work belong to the outer repository.
- Design documents separate confirmed findings from recommendations. Preserve that
  distinction when editing them.
  

## Instructions
- Never commit the change on your own until asked.
- Always provide plans in phases, after each phase meaningful single line commit message.
- Separate commit messages for API and Angular changes.
- In Plan, always mention What you are changing/adding, Why you are doing this, Where you will add/delete/change things, What it will affect.
