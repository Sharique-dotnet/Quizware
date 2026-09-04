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
