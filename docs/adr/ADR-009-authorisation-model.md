# ADR-009: Seven roles; ProgramAdmin holds all oversight authority

**Status:** Accepted
**Date:** 2026-09-07

## Context

The legacy system's authorisation is informal and inconsistent. Early design
work for Quizware considered adding a dedicated `Judge` role to hold
oversight actions (disqualification, answer reversal, score adjustment,
tie-break resolution) separately from day-to-day operation. On reflection,
splitting oversight into its own role added a role that would sit idle most
of the time and created ambiguity about who has final authority in the
room.

## Decision

Seven roles: `SuperAdmin`, `ProgramAdmin`, `QuestionAuthor`, `Operator`,
`Scorer`, `Display`, `Auditor`. **There is no `Judge` role.** `ProgramAdmin`
(and `SuperAdmin`) hold **sole authority** over the four consolidated
oversight actions:

1. Disqualifying a participant
2. Reversing/undoing an answer
3. Making a manual score adjustment
4. Resolving a tie manually

`Operator` and `Scorer` can run the match and record answers, but get `403`
on all four of the above — proven by integration tests in `P9`, `P10`, and
`P11`. Authorisation policies (`CanManageProgram`, `CanOperateMatch`,
`CanDisqualify`, `CanResolveTie`, etc. — `05-API-Design.md` §5.9) are seeded
in `P3-06`.

## Alternatives rejected

**A separate `Judge` role** holding the four oversight actions independently
of `ProgramAdmin`. Rejected because, in practice, one person runs each event
and is accountable for both configuring the program and making the calls
that matter — separating those into two roles would either sit the `Judge`
role idle at most events (nobody to assign it to) or force the `ProgramAdmin`
to also hold `Judge`, at which point the extra role adds complexity with no
separation-of-duty benefit actually realised.

## Consequences

- Exactly 7 roles are seeded (`P3-05`) — verified as exactly 7, not
  "at least 7", so an accidental extra role addition is caught.
- If a future event genuinely needs a separate judging authority distinct
  from the program administrator (e.g. an external adjudication panel),
  that is a new ADR superseding this one, informed by that actual
  requirement — not spun up speculatively now.
