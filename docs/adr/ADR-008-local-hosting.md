# ADR-008: Local, on-premises hosting

**Status:** Accepted
**Date:** 2026-09-07

## Context

Quizware runs a live, on-stage event in a single hall, for a single evening
at a time. Venue internet connectivity at such events is often unreliable,
and the show cannot pause because a cloud link drops.

## Decision

The API, database, and SignalR hubs run **locally on a venue server** — a
laptop, mini-PC, or on-prem box on the same local network as the hall — with
**no cloud dependency assumed**. The operator console and display screens
talk to it over the LAN. The buzzer agent, if present, pushes outbound to
the local API over LAN HTTPS rather than the API reaching into the operator
PC (see ADR-005 and the "why the agent still pushes outward" note in
`02-Architecture-Proposal.md` §2.14) — this keeps the design robust to
unpredictable local network topology (VLANs, client isolation) without
needing port-forwarding or a fixed network layout.

## Alternatives rejected

**Cloud hosting.** Rejected as the default because a live event cannot
tolerate an internet outage mid-match, and there is no requirement here for
remote access, multi-region availability, or elastic scaling that would
justify accepting that risk. A cloud deployment remains possible later if
requirements change (e.g. multiple simultaneous venues needing central
coordination), but that is not today's problem.

## Consequences

- `P3-15` (Docker Compose for local SQL Server) and the venue-hardware
  inventory (deferred from Phase 0, `P0-06`) are direct products of this
  decision.
- Backup and restore (`P15-05`) is a local, manual/scripted concern, not a
  managed cloud service's responsibility — this must be drilled, not
  assumed, before the first live event (`P15-07`).
- If the project ever needs cross-venue coordination or remote monitoring,
  that is a new ADR superseding this one, not a quiet architecture drift.
