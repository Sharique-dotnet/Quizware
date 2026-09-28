# ADR-004: Event-sourced scoring

**Status:** Accepted
**Date:** 2026-09-07

## Context

The legacy system recomputes a team's score from scratch on every request via
an 8-subquery stored procedure, has no way to undo a scoring mistake other
than manually editing a total, and keeps no record of why a score changed.
Disputes cannot be resolved after the fact because there is no history.

## Decision

Score every point as an immutable **`ScoreEvent`** row (an append-only
ledger): `Answer`, `ManualAdjust`, `Reversal`, `Penalty`, or `Bonus`, each
with the points, the reason (for manual events), and — for a reversal — a
`ReversesScoreEventId` pointing back at the event it undoes.

**Rows are never updated and never deleted.** An undo does not mutate or
remove the original row; it inserts a new row with the opposite points. See
`Quizware.Domain.Scoring.ScoreEvent.Reverse()`, which marks the original
`IsReversed = true` and returns a brand-new event rather than changing the
original's `Points` in place.

`TeamMatchScore` and `TeamStageScore` are fast **read models**, updated
incrementally in the same transaction as each `ScoreEvent` — never
recomputed from raw answers on a request — with `POST /scores/recalculate`
as a safety net that rebuilds them from the event ledger and must produce
the same total.

## Alternatives rejected

**A mutable score column**, incremented and decremented in place. Rejected
because it provides no undo (once decremented, the previous value is gone),
no audit trail (no record of who changed what or why), and no way to
recover from a corrupted total except recomputing from raw answers — which
is itself expensive under the legacy approach.

## Consequences

- Every scoring code path (match engine, manual adjustment, tie-break
  scoring) must write through `ScoreEvent`, never touch a team's total
  directly.
- Storage grows with every scoring action, but this is a small, bounded cost
  for a large audit and correctness benefit — never something to "clean up"
  by deleting old events.
- `P10-06`'s recalculation endpoint is the load-bearing safety net that makes
  event sourcing trustworthy rather than merely audited; it must be tested
  to produce identical results to the incremental read model.
