# ADR-006: SignalR for live updates, with an outbox for reliable dispatch

**Status:** Accepted
**Date:** 2026-09-07

## Context

Display screens, operator consoles, and (indirectly) the buzzer agent all
need to see match state change in near-real-time — a question revealed, a
score updated, a segment closed. The legacy system polls via AJAX, which is
both wasteful (constant requests whether or not anything changed) and
laggy (bounded by the poll interval).

## Decision

Use **SignalR** for live push: a `MatchHub` for operators (read/write) and a
`DisplayHub` for projectors (read-only token, cannot invoke anything that
writes). Domain events are written to an **`OutboxMessage`** table in the
same transaction as the state change that produced them, and a background
outbox processor dispatches them to SignalR — so a transient SignalR failure
does not lose the event, and dispatch survives a restart (it retries from
the outbox rather than from memory).

## Alternatives rejected

**AJAX polling** — today's approach. Rejected as both wasteful (load on the
server and network for no-op polls) and laggy (updates only as fresh as the
last poll interval), neither of which suits a live, on-stage event where an
operator or a display screen needs to react within a few hundred
milliseconds of a state change.

## Consequences

- Every state-changing operation that display screens or operators care
  about must write an `OutboxMessage` in the same transaction, not simply
  call SignalR directly — direct calls have no delivery guarantee across a
  crash or a dropped connection.
- `P12-06` (full state re-sync on reconnect) exists because push delivery is
  best-effort even with an outbox — a client must always be able to ask for
  a full snapshot rather than trust it has seen every incremental event.
- Performance target: 100 simulated display clients receive an update within
  500 ms (`P12` test).
