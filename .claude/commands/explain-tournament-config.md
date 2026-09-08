---
description: Translate a stage/segment/rule configuration payload or DB rows into plain language, since the tournament engine is fully data-driven
argument-hint: "[a program/stage id, a JSON payload, or a question like 'what does stage 2 look like']"
allowed-tools: Read, Glob, Grep, Bash
---

# Explain a tournament configuration

Turn a tournament's stage/segment/scoring/qualification/tie-break
configuration into a plain-language description a non-developer (QA, the
tournament operator) can verify against what they intended, without reading
JSON or SQL.

Input from the user (may be empty — if so, ask which program/stage, or offer
to explain the seeded demo tournament): **$ARGUMENTS**

---

## Why this exists

`docs/new-system/02-Architecture-Proposal.md` and
`docs/new-system/01-Analysis-Findings.md` establish that the legacy system
hardcoded the tournament (turn order as `QuestionNumber % 3`, running order
baked into 108 views, a disconnected `TieBreaker` table) — see
`context/CURRENT.md` §1. The new system turns all of that into configuration
data instead: `Stage`, `StageSegmentTemplate`, `ScoringRule`,
`QualificationRule`, `TieBreakRule`, `QuestionSelectionRule`. That flexibility
means a misconfigured tournament is a data bug, not a code bug — and data bugs
are much easier to catch by reading a plain-language description than by
reading rows.

## Step 1 — Get the data

Either query the live API (`GET /programs/{id}/stages`,
`/rules/scoring`, `/rules/qualification`, `/rules/tiebreak`, etc. — see
`docs/new-system/05-API-Design.md` for exact routes) against a running
instance, or query LocalDB directly:

```bash
sqlcmd -S "(localdb)\MSSQLLocalDB" -d "Quizware-Dev" -Q "<query>" -C
```

If the user gave you a raw JSON payload instead, work from that directly.

## Step 2 — Resolve specificity

Read `Quizware/src/Quizware.Application/Rules/Services/RuleService.cs` (or
its interface) if resolving *which* rule actually applies matters — resolution
is segment beats stage beats program (`context/DECISIONS.md`, Phase 7 work).
Don't just list every rule row; say which one wins for a given segment.

## Step 3 — Explain

Produce a short narrative per stage, in order:

- Stage name, type, and how many teams/segments it has.
- Each segment in its actual play order (`OrderIndex`, respecting
  `IsOrderLocked` segments — see D-024), what question format it uses, and
  which scoring/selection rule resolves for it.
- How qualification/tie-break works for that stage, in plain terms ("top 2
  advance; ties broken by total correct answers, then by earliest completion
  time" — not the raw rule JSON).

Flag anything that looks like a likely misconfiguration (e.g. a segment with
no resolvable scoring rule, a qualification rule that can never be satisfied)
as a question back to the user, not a silent assumption.
