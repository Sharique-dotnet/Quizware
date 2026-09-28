# ADR-007: A tie-break is an ordinary Match

**Status:** Accepted
**Date:** 2026-09-07

## Context

Today's tie-breaker exists as a separate, disconnected `TieBreaker`
table and its own screens, unconnected to the normal qualification logic —
it is exercised once a year, at the single most contested moment of the
event, by a code path nobody has run since the last tie.

## Decision

A tie-break is created as an **ordinary `Match`** with `MatchKind =
TieBreak`, containing only the tied teams, with its segments built from the
relevant `TieBreakRule` (MCQ by default, any of the ten formats
configurable). It runs through the **same match engine**, on the **same**
operator console, with the **same** scoring and audit trail as any other
match. `TieBreakEvent`/`TieBreakParticipant` record that a tie occurred and
how it was resolved; `ScoreCountsTowardStage` on the rule decides whether
the tie-break's own points affect the stage leaderboard or only settle
ordering. No separate tie-break gameplay code exists anywhere in the system.

## Alternatives rejected

**A special tie-break mode** — a second, dedicated gameplay code path
distinct from the normal match engine. Rejected because a second code path
that is only ever exercised once a year, under time pressure, at the moment
of greatest scrutiny, is exactly the kind of code most likely to be broken
and least likely to have been tested recently.

## Consequences

- `P11-04` ("Phase 2 — tie-break match creation") explicitly hands off to
  the existing match engine rather than writing new gameplay code — this
  is a hard constraint on that task, not an implementation detail left open.
- Every match-engine feature (undo, disqualification, crash recovery, live
  push) automatically works for tie-breaks too, because it is the same code.
- Criteria-based resolution (`TieBreakCriteriaEvaluator`) is tried first and
  is cheap; only when criteria fail to separate the teams does a tie-break
  match get created at all.
