# ADR-002: Multi-tenancy via shared schema + ProgramId

**Status:** Accepted
**Date:** 2026-09-07

## Context

The legacy system creates a new database for every year's tournament. That
means no cross-year reporting, redeployment for every new event, and
duplicated schema maintenance. Quizware needs to support running many
programs (events, seasons) side by side without any of that.

## Decision

Use a **shared database schema** across all programs. Every tenant-scoped
entity carries a `ProgramId` column (see `ITenantScoped` in
`Quizware.Domain.Common`) and EF Core applies a **global query filter** on
`ProgramId` so a query can never accidentally cross tenant boundaries.

**The tenant id comes from the JWT claim, never the route.** A request's
`programId` route segment is validated against the token's `program_id`
claim (see `P3-07`, the program-scope filter) and rejected with 403 before
any database access if they disagree. The route value is never trusted as
the source of truth for which program's data to touch.

## Alternatives rejected

**Database per program** — today's approach. Rejected because it requires a
new database (and redeployment) for every event, makes year-on-year
reporting impossible without cross-database queries, and multiplies schema
migration effort by the number of programs that have ever existed.

## Consequences

- Creating a new program is one `INSERT`, not a new database.
- A single leaked or forged `ProgramId` is the only realistic cross-tenant
  attack surface, so `P4` (database phase) includes a dedicated tenant
  isolation test, and `P15-03` includes a tenant-isolation penetration test
  before go-live.
- Every new entity added anywhere in the system must implement
  `ITenantScoped` (or be deliberately shared/global, like `Topic`/`Tag`/
  `MediaAsset` with a nullable `ProgramId`) — this is a standing review
  checklist item, not a one-time decision.
