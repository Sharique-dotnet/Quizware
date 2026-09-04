---
description: Checkpoint this conversation into context/ so any AI assistant, account, or platform can resume it
argument-hint: "[optional focus note, e.g. 'just the scoring decisions']"
allowed-tools: Read, Write, Edit, Glob, Grep, Bash, Agent
---

# Save the current context

Checkpoint this conversation into `context/` so another AI — a different Claude
account, Codex, Cursor, or anything else — can pick the work up cold.

Focus note from the user (may be empty): **$ARGUMENTS**

---

## Why you, and not the subagent alone

**You have the conversation. A subagent does not.** A freshly spawned agent
cannot see this chat, so the extraction step can only be done here. Your job is to
do that extraction and hand it over.

## Step 1 — Read the procedure

Read `context/_meta/SPEC.md`, sections §2 (tags), §4.1 (what to extract), and
§4.4 (merge rules). If `context/` does not exist yet, you are bootstrapping — the
spec's §6 templates define the structure.

## Step 2 — Extract the brief

Go back over this entire conversation and write a `## CONVERSATION BRIEF` covering
SPEC §4.1 items 1–12: objective, requirements and constraints, decisions **with
their reasoning**, changes made (with paths), current status, failed approaches
and why they failed, bugs and surprises, open questions, assumptions, user
preferences and conventions, environment, and commands.

Rules:

- Reasoning matters more than outcome. "Chose X over Y because Z" survives; "chose
  X" gets re-litigated.
- Include anything the user stated in passing — offhand constraints are the ones
  most often lost across tools.
- Exclude conversational noise, retries, and fixed typos.
- Never include secrets. Note that a credential exists and where it lives, not its
  value.
- If `$ARGUMENTS` names a focus, weight the brief toward it — but never drop
  material decisions or blockers just because they fall outside that focus.

## Step 3 — Delegate the merge

Call the `context-keeper` agent with `run_in_background: false`, passing your
brief:

```
Save the current context per context/_meta/SPEC.md.

## CONVERSATION BRIEF
<your extraction from Step 2>
```

The subagent reads the existing `context/`, merges rather than overwrites,
allocates IDs, and writes the files. Its final report is not shown to the user —
**relay it**.

If the `context-keeper` agent is unavailable, do the merge yourself by following
SPEC §4.3–§4.7 directly. The result must be identical; the agent is an
optimization, not a requirement.

## Step 4 — Report

Relay what changed in 8 lines or fewer: added IDs, updated sections, anything
superseded, files touched, and — most importantly — any question that now needs
the user's answer.

If nothing material changed, say so and confirm that no files were written.
