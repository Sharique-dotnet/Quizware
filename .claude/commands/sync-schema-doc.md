---
description: Generate an EF Core migration and update docs/new-system/04-Database-Schema.md in the same pass, so the schema doc doesn't go stale
argument-hint: "<migration name, e.g. AddStageSegmentTemplate>"
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
---

# Sync migration and schema doc

Generate an EF Core migration for the current model changes, then update
`docs/new-system/04-Database-Schema.md` to match — in the same pass, so the
two never drift apart. `docs/` is gitignored (`context/DECISIONS.md` D-016),
so nothing else forces this; it only happens if you do it deliberately.

Migration name: **$ARGUMENTS**

---

## Step 1 — Check for pending model changes

```bash
cd Quizware
dotnet ef migrations has-pending-model-changes --project src/Quizware.Infrastructure --startup-project src/Quizware.Api
```

If there are none, say so and stop — do not generate an empty migration.

## Step 2 — Generate the migration

```bash
dotnet ef migrations add $ARGUMENTS --project src/Quizware.Infrastructure --startup-project src/Quizware.Api
```

Read the generated `Up()`/`Down()` in full.

## Step 3 — Update the schema doc

Read `docs/new-system/04-Database-Schema.md` and edit the affected table(s)
to match exactly what the migration does: new/changed columns, types,
nullability, indexes, and foreign keys. Preserve the doc's existing structure
and level of detail — add to it, don't rewrite sections that didn't change.

Cross-check against `docs/adr/ADR-002-multi-tenancy.md` (new tenant-scoped
tables need `ProgramId`) and `docs/adr/ADR-003-question-storage-tpt.md`
(question tables stay Table-Per-Type) — a schema change that violates either
is worth flagging to the user before writing the doc update, not after.

## Step 4 — Build and verify

```bash
dotnet build Quizware.slnx
```

Confirm it's still green. Do not apply the migration to LocalDB yourself
unless explicitly asked — `context/CURRENT.md` V-008 already flags that
"migration generated" and "migration applied" are tracked as separate,
independently-verified facts in this project.

## Step 5 — Report

State the migration name, which tables/columns changed, and exactly which
section of `04-Database-Schema.md` was updated. If the migration's actual
effect differs from what you expected going in, say so.
