# HISTORY

Append-only timeline. One block per checkpoint that produced a material change.
Newest last. Format: `_meta/SPEC.md` §6.6.

---

## S-2026-09-04-01 · context-system-bootstrap · Claude Opus 5 (Claude Code)
- **Focus:** Designed and built the cross-AI context preservation system, and
  bootstrapped `context/` with the real state of the QuizApp project.
- **Changed:** `context/` (created), `.claude/agents/`, `.claude/commands/`,
  `.cursor/rules/`, `AGENTS.md`, `CLAUDE.md` (all created — no existing AI
  conventions were present in the repo).
- **Added:** D-001 – D-008, T-001 – T-004, Q-001 – Q-003, V-001 – V-005,
  L-001 – L-003.
- **Superseded:** none — first session.
- **Detail:** `sessions/2026-09-04-01-context-system-bootstrap.md`

## S-2026-09-04-02 · tpt-roles-scaffolding · Claude Sonnet 5 (Claude Code)
- **Focus:** Merged a conversation brief covering two phases of work: further
  design-doc iteration (Table-Per-Type questions, configurable segment order,
  tie-break as an ordinary match, optional question formats, Judge role removed)
  and the first real .NET solution scaffolding (10 projects, correct references,
  builds clean). Corrected `CURRENT.md`'s stale "no application code written"
  claim and reconciled a `git log` discrepancy the brief did not anticipate (one
  commit already exists).
- **Changed:** `CURRENT.md` (rewritten), `DECISIONS.md` (D-009–D-014 appended),
  `TASKS.md` (T-002/T-003/T-004 closed, T-005/T-006/T-007 added, Q-001/Q-002
  updated, Q-004 added, V-002/V-004 resolved), `PROJECT.md` (repo map, tech stack,
  environment, commands, conventions updated), `HISTORY.md` (this block).
- **Added:** D-009, D-010, D-011, D-012, D-013, D-014, T-005, T-006, T-007, Q-004.
- **Superseded:** none — all new information extended or closed existing entries
  rather than contradicting them.
- **Detail:** `sessions/2026-09-04-02-tpt-roles-scaffolding.md`

## S-2026-09-04-03 · phase0-owner-confirmed-phase1-domain · Claude Sonnet 5 (Claude Code)
- **Focus:** Merged a brief covering two things: closing Phase 0 as
  owner-confirmed (the user is QuizApp's sole stakeholder, so no external
  workshop applies), and the full implementation of Phase 1's domain model
  (P1-01–P1-14, 89 files, 95 passing tests). Verification against the
  repository found the brief's commit-state claim was stale — `git log` showed
  the domain model and prior scaffolding already committed across six commits
  the brief did not account for — and corrected it rather than transcribing it.
- **Changed:** `CURRENT.md` (rewritten), `TASKS.md` (T-001/T-007 closed,
  T-005/T-006 kept, T-008/T-009/T-010 added, Q-001/Q-004 answered, V-005
  resolved to `[DECIDED]`), `DECISIONS.md` (D-015 appended), `LESSONS.md`
  (L-004 added), `PROJECT.md` (repo map, environment, conventions updated),
  `HISTORY.md` (this block).
- **Added:** D-015, T-008, T-009, T-010, L-004.
- **Superseded:** none directly, but corrected a stale claim from the source
  brief (commit state) rather than propagating it — see L-004 and the session
  file's "Corrections made to the brief's account".
- **Detail:** `sessions/2026-09-04-03-phase0-owner-confirmed-phase1-domain.md`

## S-2026-09-07-01 · phases-2-3-4-5-catchup · Claude Sonnet 5 (Claude Code)
- **Focus:** Merged a brief covering only Phase 5 (API contract, OpenAPI
  first), but found `CURRENT.md` was stale by three additional, undocumented
  phases — Phase 2 (ADRs), Phase 3 (skeleton/cross-cutting), and Phase 4
  (schema/migrations) had all been completed and mostly committed since the
  last save, with no context checkpoint in between. Reconstructed those three
  phases from `docs/Implementation-Plan.md`'s own inline "Status: done" text
  and `git log`/`git show`, tagged `[FACT]` (verified against the repo, not
  invented), then merged in Phase 5 from the brief. Verified the brief's test
  count and file counts directly rather than transcribing them (95+4+4+44=147
  tests passed, 16 controllers present, TS client 19,419 lines).
- **Changed:** `CURRENT.md` (rewritten), `DECISIONS.md` (D-016–D-018 appended),
  `TASKS.md` (T-005/T-008/T-009 closed with resolution notes, T-011/T-012/T-013
  added closed, T-006 kept, T-014 added, Q-005 added, V-006/V-007 added),
  `LESSONS.md` (L-005, L-006 added), `HISTORY.md` (this block).
- **Added:** D-016, D-017, D-018, T-011, T-012, T-013, T-014, Q-005, L-005,
  L-006, V-006, V-007.
- **Superseded:** none directly — T-005/T-008/T-009 closed with their original
  text preserved and a resolution note added, not overwritten.
- **Detail:** `sessions/2026-09-07-01-phases-2-3-4-5-catchup.md`

## S-2026-09-08-01 · phase6-configuration-modules · Claude Sonnet 5 (Claude Code)
- **Focus:** Implemented all six sub-phases of Phase 6 — Configuration
  modules (6a Programs, 6b Users/roles, 6c Teams, 6d Topics/tags, 6e Media,
  6f Question bank) — per direct per-phase user instructions, plan-first
  except 6e/6f where the user explicitly waived planning. Re-ran the full
  suite at session end and live-verified against real LocalDB.
- **Changed:** `CURRENT.md` (rewritten), `DECISIONS.md` (D-019–D-022
  appended), `TASKS.md` (T-015/T-016/T-017 added, V-008 added), `LESSONS.md`
  (L-004 updated with a recurrence, L-007/L-008/L-009 added), `HISTORY.md`
  (this block).
- **Added:** D-019, D-020, D-021, D-022, T-015, T-016, T-017, L-007, L-008,
  L-009, V-008.
- **Superseded:** none directly — L-004 updated in place per SPEC §4.4
  rather than duplicated.
- **Correction:** the conversation brief claimed 6b–6f were all uncommitted;
  `git log` showed 6a–6d (`717b96f`, `ae94bca`, `41e45bd`, `1d86810`) were
  already committed by the user out-of-band. Only 6e+6f are genuinely
  uncommitted. Same root cause as the original L-004 entry.
- **Detail:** `sessions/2026-09-08-01-phase6-configuration-modules.md`

## S-2026-09-08-02 · phase7-tournament-configuration · Claude Sonnet 5 (Claude Code)
- **Focus:** Merged a brief covering two pieces of work done immediately
  after S-2026-09-08-01: Phase 7 — Tournament configuration, delivered in
  full (all 12 tasks) and committed as `3dbc6f2`; and a Postman collection
  covering everything through Phase 7, validated by actually running it
  against a live API with `newman`, which surfaced and led to fixing two
  real bugs (a collection-ordering bug, and a genuine backend bug in 3 of 4
  rule-upsert handlers — same class as L-007, recorded as L-010). Corrected
  the brief's claim that Phase 7 and Phase 6e/6f were still uncommitted —
  `git log` showed both already committed by the user out-of-band, the third
  recurrence of the pattern in L-004.
- **Changed:** `CURRENT.md` (§2–§10 rewritten), `DECISIONS.md` (D-023, D-024
  appended), `TASKS.md` (T-015, T-016 closed; T-018, T-019 added; V-009
  added), `LESSONS.md` (L-004 third recurrence appended, L-010 added),
  `HISTORY.md` (this block).
- **Added:** D-023, D-024, T-018, T-019, V-009, L-010.
- **Superseded:** none — all new information extended or closed existing
  entries; T-015/T-016 closed on the same premise they were opened under
  (commit decision resolved by action, Phase 7 implemented), not
  contradicted.
- **Detail:** `sessions/2026-09-08-02-phase7-tournament-configuration.md`
