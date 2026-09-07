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
