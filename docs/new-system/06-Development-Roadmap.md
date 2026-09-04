# 6. Recommended Development Sequence

You asked whether this order is right:

> Requirements → PRD → Domain/Business Rules → Architecture → Database Schema →
> API Contract → Project Structure → Core Implementation → Testing →
> QuickBuzz Integration → Angular

**It is close, but I recommend three changes:**

1. **Move Architecture before the Database Schema.** The schema is a consequence
   of the architecture (multi-tenant strategy, modular boundaries), not the other
   way round. Designing tables first tends to lock in the wrong boundaries.
2. **Move Project Structure and a "walking skeleton" much earlier** — right after
   architecture, before you write any real feature. You want authentication,
   migrations, logging, error handling and CI working end to end on day one, with
   one trivial endpoint. Otherwise these cross-cutting pieces get bolted on later
   and never fit properly.
3. **Do not treat Testing as a phase.** It is not a step after implementation; it
   runs alongside every phase. Keep one explicit hardening phase at the end for
   load, security and rehearsal testing, but write unit tests as you go.

---

## 6.1 The adjusted sequence

```
Phase 0  Requirements confirmation        ← you are here (docs 1–5 done)
Phase 1  Domain model + business rules
Phase 2  Architecture decisions (locked)
Phase 3  Project skeleton + cross-cutting concerns   ← moved earlier
Phase 4  Database schema + migrations
Phase 5  API contract (OpenAPI first)
Phase 6  Configuration modules (Program, Team, Question bank)
Phase 7  Tournament configuration module
Phase 8  Question selection engine
Phase 9  THE MATCH ENGINE  ← the heart; everything before this feeds it
Phase 10 Scoring + standings
Phase 11 Qualification engine
Phase 12 Live push (SignalR) + display API
Phase 13 Reporting + exports
Phase 14 QuickBuzz integration module     ← last, because it is optional
Phase 15 Hardening: load, security, rehearsal
Phase 16 Data migration (optional)
Phase 17 Angular 22 front end
```

Testing runs continuously through Phases 1–14.

---

## 6.2 Phase detail

### Phase 0 — Requirements confirmation *(1 week)*

**What:** review documents 1–5 with the organisers and decide the open questions.

**Deliverables**
- Signed-off PRD
- Answers to the open questions in section 6.5
- An agreed list of the 10 question formats and their exact rules
- Agreed default scoring values

**Depends on:** the analysis you now have.
**Why first:** every later phase turns these answers into structure. Changing a
business rule here costs a conversation; changing it in Phase 9 costs a rewrite.

---

### Phase 1 — Domain model and business rules *(1–2 weeks)*

**What:** write the `QuizApp.Domain` project. Entities, enums, value objects
and pure business logic — **no database, no framework, no packages**.

**Deliverables**
- All domain entities (`Program`, `Team`, `Question`, `Stage`, `Match`,
  `MatchParticipant`, `MatchSegment`, `MatchQuestion`, `AnswerRecord`,
  `ScoreEvent`, …)
- Enums and lookup constants
- `TurnOrderCalculator` — a pure function, fully unit-tested
- `BuzzRankingCalculator` — ported from QuickBuzz, now testable without hardware
- Domain invariants (a match needs 2+ active teams; a completed match is
  immutable; …)
- **Unit tests for every rule** — this project is 100% testable with zero setup

**Depends on:** Phase 0.
**Why here:** the domain is the only thing that must be exactly right. It is also
the cheapest place to be wrong, because nothing depends on it yet.

**Do this first inside the phase:** the turn-order calculator, because it is the
single rule that the whole disqualification requirement rests on.

---

### Phase 2 — Architecture decisions, locked *(3–5 days)*

**What:** confirm and record the decisions from document 2 as ADRs (short
Architecture Decision Records, one page each).

**Deliverables**
- ADRs for: modular monolith, multi-tenancy model, unified question table,
  event-sourced scoring, buzzer port + adapters, SignalR
- Module boundary map
- A dependency-rule diagram that the architecture tests will enforce

**Depends on:** Phase 1 (the domain shows you where the real boundaries are).
**Why before the schema:** the multi-tenant decision determines whether every
table gets a `ProgramId`. Deciding this after the schema means redoing the schema.

---

### Phase 3 — Project skeleton and cross-cutting concerns *(1–2 weeks)*

**What:** create the solution and make one trivial endpoint work end to end, with
every cross-cutting concern already in place.

**Deliverables**
- The 5 projects plus test projects, with correct references
- **Architecture tests** that fail the build on a layer violation
- Dependency injection wiring
- ASP.NET Core Identity, JWT issuing and validation, refresh-token rotation
- Global exception handler producing RFC 9457 responses
- Serilog structured logging with correlation ids
- FluentValidation pipeline
- `ICurrentUser`, `ICurrentProgram`, `IClock` abstractions
- Health-check endpoints
- Swagger / OpenAPI generation
- Docker Compose for SQL Server locally
- CI pipeline: build, test, migration check
- **One working endpoint:** `GET /api/v1/admin/health` and `POST /auth/login`

**Depends on:** Phase 2.
**Why moved earlier:** these concerns touch every controller. Adding auth or
error handling after 40 endpoints exist means editing 40 endpoints.

---

### Phase 4 — Database schema and migrations *(1–2 weeks)*

**What:** turn document 4 into EF Core configurations and the first migration.

**Deliverables**
- `AppDbContext` with all `IEntityTypeConfiguration` classes
- Global query filters for tenant + soft delete
- Audit interceptor (sets `CreatedBy`/`UpdatedAt` automatically)
- Audit-log interceptor writing to `AuditLog`
- Initial migration
- Seed migration: lookups, question formats, default scoring rules, an admin user
- All indexes and check constraints from document 4
- **Integration tests** using a real SQL Server container (Testcontainers), not
  an in-memory provider — the in-memory provider does not enforce constraints and
  will lie to you

**Depends on:** Phases 1, 2, 3.

**Critical checks before moving on**
- Global query filter cannot be bypassed (write a test that tries)
- Soft delete works everywhere
- Audit fields are populated automatically
- Seed data matches the current system's scoring values

---

### Phase 5 — API contract, OpenAPI first *(1 week)*

**What:** define every endpoint's request and response shape **before**
implementing them.

**Deliverables**
- All DTOs and validators
- Controller stubs returning `501 Not Implemented`
- A complete OpenAPI document with examples
- A generated TypeScript client, checked in — the Angular team can start
  building against a mock immediately
- Postman / `.http` collection

**Depends on:** Phases 1, 4.
**Why here:** it unblocks the front-end work months early, and it forces you to
find awkward contracts before they are baked into code.

---

### Phase 6 — Configuration modules *(2–3 weeks)*

**What:** the CRUD foundations. In dependency order:

| Order | Module | Depends on |
|---|---|---|
| 6a | Program management (+ settings, branding, clone) | nothing |
| 6b | User and role management, program scoping — seed the 7 roles (`SuperAdmin`, `ProgramAdmin`, `QuestionAuthor`, `Operator`, `Scorer`, `Display`, `Auditor`) | 6a |
| 6c | Team management (+ Excel import with validation report) | 6a |
| 6d | Topics and tags | 6a |
| 6e | Media assets (upload, validation, dedup) | 6a |
| 6f | Question bank (+ Excel import, coverage report) | 6d, 6e |

**Deliverables:** working CRUD, imports, validation, tests.

**Depends on:** Phases 3, 4, 5.
**Why in this order:** you cannot create a team without a program, or a question
without a topic and media store.

---

### Phase 7 — Tournament configuration *(1–2 weeks)*

**What:** stages, segment templates, and the three rule tables.

**Deliverables**
- Stage CRUD and reordering
- Segment template CRUD and reordering
- Scoring rule management + reset-to-defaults
- Selection rule management
- Qualification rule management
- **Tie-break rule management** — criteria order, tie-break format (MCQ default),
  question count, difficulty, sudden death, max rounds, fallback action
- **Segment reordering**: `PUT .../segments/reorder` taking the full ordered list,
  plus the stage's `SegmentOrderMode` and per-segment order lock
- **Per-program format enable/disable** (`ProgramQuestionFormat`), with the
  `FORMAT_IN_USE` guard preventing a format being disabled while a segment
  template still uses it
- Rule resolution service (segment → stage → program)
- `POST /programs/{id}/validate` — the readiness check
- **A seed script that reproduces the current 18-team tournament entirely as
  data**, including the League's 4-segment running order and the MCQ tie-break
  rule. This is the proof that the configuration model works.

**Tests to write here:**
- Reorder a stage's segments and confirm (a) matches not yet created pick up the
  new order, and (b) matches already created keep the old one. That second half
  is the rule that protects a live tournament from an accidental edit.
- **Configure a stage with no Passing segment and confirm the whole pipeline is
  clean:** readiness validation passes with zero Passing questions in the bank,
  the coverage report does not mention Passing, and a full match runs without it.
  This is the test that proves no format is compulsory.

**Depends on:** Phase 6.

---

### Phase 8 — Question selection engine *(1–2 weeks)*

**What:** `IQuestionSelector`.

**Deliverables**
- Pool building with all filters
- Repeat-policy exclusion
- Difficulty-mix splitting
- Seeded weighted random draw
- Topic-spread policy
- Reservation into `MatchQuestion`
- Fallback ladder + precise exhaustion errors
- `POST /rules/selection/preview` (dry run)
- `GET /questions/coverage`
- **Heavy unit tests:** same seed → same draw; no repeats; mix respected;
  exhaustion produces the right error, not an exception

**Depends on:** Phases 6f, 7.
**Why before the match engine:** the engine calls this at match start. Building it
separately keeps it testable in isolation.

---

### Phase 9 — The match engine *(3–4 weeks — the biggest phase)*

**What:** `IMatchEngine`. This is the heart of the system and replaces ~5,700
lines of duplicated controller code.

**Sub-steps, in order**

| # | Step |
|---|---|
| 9a | Match setup: create, add participants, seats, turn order, auto-seed |
| 9b | Match lifecycle: start (reserve questions), pause, resume, end, abandon |
| 9c | Segment lifecycle: open, close, skip |
| 9d | Question serving: serve, reveal, skip, server-side timer |
| 9e | **Turn order resolution over active participants only** |
| 9f | Answer recording (transactional + idempotent) |
| 9g | Format handlers — one `IQuestionFormatHandler` per format |
| 9h | Passing mechanics (pass to the next active team, pass counts) |
| 9i | Topic selection for the Choice round |
| 9j | Undo / reverse an answer |
| 9k | **Participant disqualification and turn-order recompaction** |
| 9l | Match events timeline |
| 9m | Crash recovery / resume |
| 9n | Effective segment order: template → per-match override → live reorder of pending segments; `RandomPerMatch` shuffling from the match seed; locked segments |
| 9o | Sudden-death segment closing (needed by tie-breaks, built here because it is a gameplay rule) |

**Deliverables**
- One engine covering all 10 formats
- `GET /matches/{id}/live/state` returning everything in one call
- Full integration tests, including:
  - a complete 3-team match from start to finish
  - a match where a team is disqualified mid-segment and finishes with 2
  - a match where the API is restarted mid-question and resumes correctly
  - the same request sent twice with one idempotency key scores once
  - **authorization:** `Operator` and `Scorer` are rejected with `403` on
    disqualify and on answer reversal — those two actions are `ProgramAdmin`
    only, now that the Judge role has been removed

**Depends on:** Phases 7, 8, and Scoring (10) is developed alongside 9f.

**Recommendation:** build 9a–9f with the MCQ format only, get it completely
solid, then add the other nine formats one at a time. Each is now a small,
self-contained unit of work: one format table, one request model and validator,
one `IQuestionFormatHandler`. Because the engine only ever talks to
`Question.Id`, adding a format touches no existing code. Budget roughly a day
each, and do the two structurally unusual ones — **Sequence** (ordered items with
partial credit) and **VisualRapidFire** (a set of images with per-image answers)
— last, since they exercise the child-table paths.

---

### Phase 10 — Scoring and standings *(1–2 weeks, overlaps Phase 9)*

**Deliverables**
- `IScoringEngine`: rule resolution, score events, incremental read-model updates
- Manual adjustment with reason and approval
- Reversal / undo
- `TeamMatchScore` and `TeamStageScore` maintenance
- Standings endpoints
- Recalculate-from-events endpoint (the safety net)
- Tie-break service with ordered criteria
- **Tests:** score = sum of events; undo restores exactly; disqualified teams
  excluded from standings but their history retained; a manual adjustment from
  `Operator` or `Scorer` is rejected with `403` — only `ProgramAdmin` (or
  `SuperAdmin`) may approve one

**Depends on:** Phases 7 (scoring rules), 9f.

---

### Phase 11 — Qualification and tie-breaking *(2 weeks)*

**Deliverables**
- Preview: winners, best remaining, wildcards, reasons
- **Tie detection** on and across the qualification boundary, separating ties
  that block a commit from ties that are merely recorded
- **Phase 1 criteria evaluation** — ordered, configurable, recording which
  criterion broke the tie
- **Phase 2 tie-break match creation** — builds a `Match` with
  `MatchKind = TieBreak` containing only the tied teams, from the tie-break rule
  (MCQ by default), then hands it to the existing match engine
- Tie-break result capture: `TieBreakEvent` + `TieBreakParticipant` ranks
- Extra rounds up to the limit, then the configured fallback (manual decision
  with reason and ProgramAdmin approval, coin toss, or shared slot)
- Commit blocked while a qualifying-place tie is unresolved
- Rollback before the next stage starts

**Tests**
- Reproduce the exact 18 → 9 → 3 → 1 progression from configuration alone
- **Force a tie on the 9th/10th wildcard boundary** and confirm: criteria run in
  order, the deciding criterion is recorded, and where criteria fail a tie-break
  match is created with only the two tied teams
- Confirm the tie-break's points do not move the stage leaderboard when
  `ScoreCountsTowardStage = 0`
- Confirm a tie below the cut does not block the commit
- Confirm `POST /ties/{tieId}/resolve-manually` is rejected with `403` for
  every role except `ProgramAdmin` and `SuperAdmin`

**Depends on:** Phases 9 (the engine runs the tie-break match), 10.

**Note on effort:** this grew from 1 week to 2 because tie-breaking is now a real
feature rather than a flag. The tie-break match itself costs almost nothing —
it reuses the engine — but detection, criteria evaluation, the fallback ladder
and the audit trail are genuine work.

---

### Phase 12 — Live push and display API *(1–2 weeks)*

**Deliverables**
- `MatchHub` and `DisplayHub`
- Outbox processor pushing domain events to SignalR
- Display token issuing and the read-only display policy
- Correct-answer stripping until reveal
- Server-authoritative timer ticks
- Full state re-sync on reconnect
- **Test:** 100 simulated display clients receive an update within 500 ms

**Depends on:** Phases 9, 10.

---

### Phase 13 — Reporting and exports *(1 week)*

**Deliverables:** match report, stage summary, team report, question usage, audit
log, and Excel/CSV/PDF export.

**Depends on:** Phases 10, 11.

---

### Phase 14 — QuickBuzz integration module *(2 weeks)*

**Deliberately last.** By this point the whole system has been proven to work
without it — which is exactly the requirement.

**Sub-steps**

| # | Step |
|---|---|
| 14a | `IBuzzerProvider` port (already defined in Phase 2; implement now) |
| 14b | `NullBuzzerAdapter` and verify every test still passes with it |
| 14c | Buzz session and press persistence, device→team mapping |
| 14d | `SerialBuzzerAdapter` — port `SerialService` and `DeviceParser` from QuickBuzz, move constants to configuration, replace `Console.WriteLine` with `ILogger`, support any device count |
| 14e | `QuizApp.BuzzerAgent` — the small tray app for the operator PC |
| 14f | `HttpAgentBuzzerAdapter` + the push ingestion endpoint |
| 14g | Buzzer endpoints, health check and the hardware test screen |
| 14h | Fallback and degradation testing |

**Deliverables**
- The module builds, and **the solution also builds with the module removed**
- Buzz presses persisted with raw frames
- Operator override always available
- **Test:** unplug the hardware mid-match — the match completes normally

**Depends on:** Phase 9 (needs a working match to attach to).

**Why last:** if you build it early it becomes load-bearing by accident. Building
it last proves the independence requirement by construction.

---

### Phase 15 — Hardening *(2 weeks)*

**Deliverables**
- Load test: 100 display clients, 5 operators, a full match
- Security review: OWASP top 10, dependency scan, secret scan, penetration test
  of tenant isolation
- Failure testing: kill the API mid-match, kill the database connection, unplug
  the buzzer, disconnect a display
- Backup and restore drill
- Performance tuning against the NFR targets
- **A full rehearsal:** run a complete 18-team tournament end to end with real
  operators, on the real venue hardware

**Depends on:** everything.

**Do not skip the rehearsal.** Almost every problem this system has today would
have been found by one dress rehearsal.

---

### Phase 16 — Data migration *(1 week, optional)*

Only if you want the 9AMM history in the new system.

**Steps**
1. Create a `Program` row for "9AMM 2025".
2. Migrate `SchoolsTeam` → `Team`.
3. Create `Stage` rows for the 3 rounds.
4. Convert `Matches` slot numbers + `RoundsId` into real `Match` rows.
5. Migrate `SchoolsTeam_Matches` → `MatchParticipant`, deriving turn order from
   the old ordering.
6. Migrate the questions. Each old table maps to a `Question` base row plus one
   row in its own format table — a near one-to-one mapping, which makes this the
   easiest part of the migration:
   - `MCQ` → `Question` + `McqQuestion`; `MCQ_Option` → `QuestionOption`
   - `Buzzer` → `Question` + `BuzzerQuestion`; `Buzzer_Option` → `QuestionOption`
   - `Card`, `Choice`, `Passing`, `TieBreaker` → likewise
     (`Choice.TopicName` → `ChoiceQuestion.TopicLabel`)
   - `Sequence` → `Question` + `SequenceQuestion`;
     `Sequence_Option` → `SequenceItem` (`RightSequenceNo` → `CorrectPosition`,
     `WrongSequenceNo` → `DisplayOrder`)
   - `Audio_Visual` → `Question` + `AudioVisualQuestion`, with the file path
     string resolved into a `MediaAsset` row
   - `RapidFire` → `Question` + `RapidFireQuestion` with `IsHostRead = 1`
     (this table was never read by the old system, so treat it as low-confidence
     data)
   - `VisualRapidFire` → group the per-image rows into one
     `VisualRapidFireQuestion` per set, with each image becoming a
     `VisualRapidFireItem`
   - Difficulty is defaulted by round: level 2 for round 1, 3 for round 2,
     4 for the final. Flag every migrated question for author review.
7. Migrate the 9 answer tables → `AnswerRecord` + `ScoreEvent`, backfilling
   points from `Contants.cs`.
8. Rebuild `TeamMatchScore` and `TeamStageScore` from the events.
9. **Reconcile:** every migrated team's total must match
   `spGetTotalScoreNew` from the old database exactly. If it does not, stop and
   investigate.

**Recommendation:** treat this as **read-only historical data**, marked
`Archived`. Do not try to make old matches replayable.

---

### Phase 17 — Angular 22 *(separate track)*

Three applications, built in this order:

| Order | App | Why this order |
|---|---|---|
| 1 | **Admin console** | Needed to create data for the others |
| 2 | **Operator console** | The critical live app |
| 3 | **Display screens** | Read-only; can be developed in parallel with 2 |

The Angular team can start at the end of **Phase 5**, using the generated
TypeScript client against a mock server. They do not need to wait for Phase 9.

---

## 6.3 Dependency map

```
Phase 0  Requirements
   │
Phase 1  Domain ──────────────────────────────┐
   │                                          │
Phase 2  Architecture                         │
   │                                          │
Phase 3  Skeleton + cross-cutting             │
   │                                          │
Phase 4  Database ◄───────────────────────────┘
   │
Phase 5  API contract ──────────────► Phase 17 Angular can start here
   │
Phase 6  Config modules (Program → Users → Teams → Topics → Media → Questions)
   │
Phase 7  Tournament configuration
   │
   ├──► Phase 8  Question selection
   │        │
   └────────┴──► Phase 9  MATCH ENGINE ◄──► Phase 10 Scoring
                     │                          (built together)
                     ├──► Phase 11 Qualification
                     ├──► Phase 12 SignalR + Display
                     ├──► Phase 13 Reporting
                     └──► Phase 14 QuickBuzz (optional, last)
                              │
                        Phase 15 Hardening
                              │
                        Phase 16 Migration (optional)
```

**The critical path is:** 0 → 1 → 2 → 3 → 4 → 6 → 7 → 8 → 9 → 10 → 15.
Phases 11, 12, 13 and 14 can be parallelised across developers once Phase 9
is stable.

---

## 6.4 Timeline estimate

| Phase | Duration | Cumulative |
|---|---|---|
| 0 Requirements | 1 week | 1 |
| 1 Domain | 2 weeks | 3 |
| 2 Architecture | 1 week | 4 |
| 3 Skeleton | 2 weeks | 6 |
| 4 Database | 2 weeks | 8 |
| 5 API contract | 1 week | 9 |
| 6 Config modules | 3 weeks | 12 |
| 7 Tournament config | 2 weeks | 14 |
| 8 Question selection | 2 weeks | 16 |
| 9 Match engine | 4 weeks | 20 |
| 10 Scoring *(overlaps 9)* | — | 20 |
| 11 Qualification + tie-breaking | 2 weeks | 22 |
| 12 SignalR + Display | 2 weeks | 24 |
| 13 Reporting | 1 week | 25 |
| 14 QuickBuzz | 2 weeks | 27 |
| 15 Hardening | 2 weeks | 29 |
| 16 Migration *(optional)* | 1 week | 30 |

**Backend: roughly 29–30 weeks (about 7 months) for one developer.**
With two developers working in parallel after Phase 5, expect **17–19 weeks**.

Angular adds 10–14 weeks, largely overlapping from Phase 5 onward.

---

## 6.5 Open questions to settle in Phase 0

These are the points where I had to make an assumption. Confirm them before
Phase 1.

| # | Question | My assumption |
|---|---|---|
| 1 | Should the question bank be shared across years, or separate per program? | Support both — `OwnerScope` on `Question` |
| 2 | Are Rapid Fire questions ever stored, or always read from paper? | Support both — the `IsHostRead` flag |
| 3 | Should a disqualified team's score be zeroed or kept and excluded? | Kept and excluded (`ExcludeFromStandings`) |
| 4 | When a team is removed mid-segment, keep the planned question count or rebalance? | Configurable; default `Rebalance` |
| 5 | Can a team play more than one match per stage? | No, by default |
| 6 | Should tie-break questions come from the same bank or a reserved set? | Same bank, drawn by the normal selector with the repeat policy applied; format defaults to MCQ and is configurable |
| 6a | Should tie-break points count toward the stage score? | No by default — the tie-break decides the order, not the points |
| 6b | What happens if a tie-break is still level after the maximum rounds? | Manual decision by a ProgramAdmin with a recorded reason (configurable to coin toss or a shared slot) |
| 6c | Should the running order of question types ever be randomised per match? | Supported (`RandomPerMatch`), but `Fixed` is the default |
| 7 | Are negative total scores allowed? | Yes, configurable per program |
| 8 | Who may approve a disqualification? | ProgramAdmin |
| 8a | Should there be a separate Judge role for oversight actions (disqualification, answer reversal, score adjustment, tie-break resolution)? | No — the Judge role has been dropped entirely. `ProgramAdmin` (or `SuperAdmin`) holds sole authority over all four; the system now has 7 roles: `SuperAdmin`, `ProgramAdmin`, `QuestionAuthor`, `Operator`, `Scorer`, `Display`, `Auditor` |
| 9 | Should audience/online voting be in scope later? | Out of scope for v1, designed for |
| 10 | Is the 9AMM history worth migrating? | Optional (Phase 16) |
| 10a | Which question formats will the first program actually use? | All ten are supported; configure only the ones you want |
| 11 | Where will the API be hosted — venue server or cloud? | Local (on-premises venue server, no cloud dependency assumed) |
| 12 | How many buzzer devices are needed at most? | Configurable, with a default of 3 (today's count); the system supports any number |

---

## 6.6 Risks and how to manage them

| Risk | Impact | Mitigation |
|---|---|---|
| The match engine is under-estimated | High | Build MCQ end to end first; treat the other 9 formats as handler plug-ins |
| Business rules turn out to be more complex than the code shows | High | Phase 0 workshop with the actual quiz masters, not just the code |
| Live event failure | Very high | Phase 15 rehearsal is mandatory; keep the old system available as a fallback for the first event |
| Question bank not ready in time | High | `GET /questions/coverage` from Phase 6 onward gives early warning |
| Buzzer hardware behaves differently than the code suggests | Medium | Phase 14 needs real hardware access; do not simulate only |
| Multi-tenant leak | High | Write a test in Phase 4 that actively tries to read another program's data |
| Angular blocked waiting for the API | Medium | Contract-first in Phase 5; generated client + mock server |
| Scope creep into audience voting, streaming, mobile | Medium | Documented as future extensibility; refuse for v1 |

---

## 6.7 What to build first, in one paragraph

Start with the **domain project and the turn-order calculator** — it is one small
class, it needs no database, and it is the exact rule that today's system gets
wrong. Then build the **project skeleton with authentication, logging and error
handling working end to end**, because those touch everything. Then the
**database and migrations**, then the **OpenAPI contract** so the Angular team can
start. Then the configuration modules bottom-up (program → teams → questions →
stages → rules), then the **question selector**, and only then the **match
engine** — which by that point is mostly wiring together pieces that are already
tested. Leave **QuickBuzz for last**, deliberately, because that is the cleanest
possible proof that the quiz system does not depend on it.
