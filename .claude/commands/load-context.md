---
description: Load context/ and report the project state — run this first in a new session or after switching accounts or tools
argument-hint: "[optional: what you plan to work on]"
allowed-tools: Read, Glob, Grep, Bash
---

# Load the saved context

Bring yourself up to speed on this project from `context/` alone. Assume you have
no chat history — because you do not.

User's stated intent for this session (may be empty): **$ARGUMENTS**

---

## Step 1 — Read

1. `context/CURRENT.md` — the entry point. Read all of it.
2. `context/TASKS.md` — the work queue, open questions, and verification queue.
3. `context/DECISIONS.md` — the constraints you must respect. At minimum, every
   `ACTIVE` entry.
4. `context/LESSONS.md` — **before you propose any approach.** This is the list of
   things that already failed.
5. `context/PROJECT.md` — only if you are unfamiliar with the codebase.

## Step 2 — Reconcile with the repository

The context describes the repo as of the last checkpoint. Verify it still holds:

```bash
git status --short
git log --oneline -10
```

Check that the paths in `CURRENT.md` §6 "Files in play" still exist. **Where they
disagree, the repository is right and the context is stale** — say so explicitly
rather than working from a stale picture.

## Step 3 — Report

Give the user a short orientation, in this shape:

```
Loaded S-2026-09-04-02 (saved 2026-09-04 by Claude Opus 5).

Project:   <one line>
Objective: <the current goal>
State:     <where the work actually stands>
Next:      <the next action from CURRENT.md §4>

Needs you: Q-005 — <question> (blocks T-014)
Stale:     <anything where the repo contradicts the context, or "nothing">
Unverified: <any [ASSUMED]/[UNVERIFIED] item that your intended work depends on>
```

## Step 4 — Before you act

- Anything tagged `[ASSUMED]` or `[UNVERIFIED]` is **not established.** Verify it,
  or state plainly that you are proceeding without verifying.
- Surface every `[OPEN]` question before it can block you.
- Do not ask the user to re-explain anything `context/` already answers.
- Respect `ACTIVE` decisions. If you think one is wrong, say why and propose a
  superseding decision — do not quietly work around it.
