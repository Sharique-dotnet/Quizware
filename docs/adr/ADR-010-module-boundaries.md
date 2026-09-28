# ADR-010: Module boundary map and dependency rule

**Status:** Accepted
**Date:** 2026-09-07

## Context

`P3-02` needs a concrete, testable dependency rule to enforce with
NetArchTest — not just a diagram in a design doc. This ADR is that rule,
made explicit as the thing the architecture tests check, per
`02-Architecture-Proposal.md` §2.3–§2.5.

## Decision

### Project dependency rule ("arrows point inward")

```
        Quizware.Api
       /      |      \
      /       |       \
Infrastructure |  Modules.Buzzer (optional)
      \       |       /
       \      |      /
      Quizware.Application
             |
      Quizware.Domain   (depends on nothing)
```

- **`Quizware.Domain`** depends on nothing — no other project, no external
  NuGet package beyond the test framework in its test project. Verified this
  session: `Quizware.Domain.csproj` carries zero `PackageReference` entries.
- **`Quizware.Application`** depends only on `Quizware.Domain`.
- **`Quizware.Infrastructure`**, **`Quizware.Api`**, and
  **`Quizware.Modules.Buzzer`** depend on `Application` and `Domain`.
- **Nothing depends on `Quizware.Api`.**
- `Quizware.Modules.Buzzer` references only `Application`'s ports
  (`IBuzzerProvider`), never `Infrastructure` directly — this is what makes
  "delete the module and the solution still builds" (ADR-005) actually true.

`P3-02`'s architecture tests enforce this mechanically and must be verified
by deliberately breaking the rule once (e.g. a `Domain → Infrastructure`
reference) and confirming the test fails the build.

### Module boundary rule (within `Application`)

A module may call another module **only through its public
Application-layer interface** (e.g. `IQuestionSelector`, `IScoringEngine`).
No module reaches into another module's persistence directly. The eleven
bounded contexts and who talks to whom:

| # | Module | Owns | Talks to |
|---|---|---|---|
| 1 | Identity & Access | Users, roles, permissions, tokens | Everything |
| 2 | Program Management | Programs, settings, branding, scoring rules | All |
| 3 | Team Management | Teams, members, registration, status | Tournament |
| 4 | Question Bank | Questions, options, topics, tags, media, import | Gameplay |
| 5 | Tournament | Stages, matches, participants, turn order | Gameplay, Qualification |
| 6 | Gameplay Engine | Live match flow, segments, serving, answers | Question Bank, Scoring, Buzzer |
| 7 | Scoring | Scoring rules, score events, standings, undo | Gameplay |
| 8 | Qualification | Advancement and tie-breaking | Tournament, Scoring, Gameplay |
| 9 | Display | Read-only projections | Gameplay, Scoring |
| 10 | Reporting | Standings, exports, event summary | Scoring, Tournament |
| 11 | Buzzer Integration *(optional)* | Sessions, presses, device mapping | Gameplay (through a port only) |

## Alternatives rejected

No serious alternative was considered here beyond what ADR-001 already
rejected (microservices) — this ADR exists to make the already-chosen
internal structure enforceable, not to re-open the structural decision.

## Consequences

- `Quizware.Architecture.Tests` is not optional scaffolding — it is the thing
  that keeps this document true over time. A PR that violates the dependency
  rule fails CI, not a code review comment.
- Domain folders map 1:1 to the modules above (`Programs/`, `Teams/`,
  `QuestionBank/`, `Tournament/`, `Gameplay/`, `Scoring/`, `Qualification/`,
  `Buzzer/`) — this mapping is already realised in
  `Quizware/src/Quizware.Domain/` as of Phase 1.
