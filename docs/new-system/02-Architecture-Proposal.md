# 2. Solution and Architecture Proposal

Target stack: **ASP.NET Core Web API on .NET 10**, **SQL Server**, **Angular 22**
(later). This document covers the backend and database only, but every decision
here keeps the future Angular app in mind.

---

## 2.1 The one-line summary

> Build a **modular monolith** using **Clean Architecture**, with a **single
> multi-program database**, a **configuration-driven tournament engine**, and
> **QuickBuzz as an optional plug-in behind an interface**.

---

## 2.2 Why a modular monolith and not microservices

| Reason | Explanation |
|---|---|
| The whole system runs in one hall for one evening | There is no need for independent scaling or independent deployment |
| One database is a requirement | Microservices would fight this |
| A small team maintains it | Microservices multiply the operations work |
| Everything is transactional | A "record answer" action must update score, question status and turn order together — much easier in one process |
| Latency matters on stage | An in-process call is faster and more reliable than a network hop |

A modular monolith gives you almost all the benefits (clear boundaries, testable
modules, ability to split later) with none of the cost. If a module ever needs to
become its own service, the boundaries are already drawn.

---

## 2.3 High-level architecture

```
                        ┌───────────────────────────────────────────┐
                        │        Angular 22 (later phase)            │
                        │  Admin Console │ Operator Console │ Display│
                        └───────────────┬───────────────────────────┘
                                        │ HTTPS (REST) + SignalR
                        ┌───────────────▼───────────────────────────┐
                        │        QuizApp.Api                         │
                        │  Controllers · SignalR Hubs · Filters      │
                        │  AuthN/AuthZ · Validation · Error handling │
                        └───────────────┬───────────────────────────┘
                                        │
                        ┌───────────────▼───────────────────────────┐
                        │      QuizApp.Application                   │
                        │  Use cases · Services · DTOs · Ports       │
                        │  (IBuzzerProvider, IQuestionSelector, ...) │
                        └───────────────┬───────────────────────────┘
                                        │
                        ┌───────────────▼───────────────────────────┐
                        │        QuizApp.Domain                      │
                        │  Entities · Enums · Domain rules           │
                        │  NO dependency on anything else            │
                        └───────────────▲───────────────────────────┘
                                        │ implements
        ┌───────────────────────────────┼──────────────────────────────┐
        │                               │                              │
┌───────▼─────────┐   ┌─────────────────▼──────────┐   ┌───────────────▼────────┐
│ Infrastructure  │   │ Modules.Buzzer (optional)  │   │  Infrastructure.Files  │
│ EF Core · SQL   │   │ Serial adapter             │   │  Media storage         │
│ Repositories    │   │ HTTP agent adapter         │   │                        │
│ Outbox · Cache  │   │ Null adapter (default)     │   │                        │
└─────────────────┘   └────────────────────────────┘   └────────────────────────┘
```

**The dependency rule:** arrows point inward. `Domain` depends on nothing.
`Application` depends only on `Domain`. `Infrastructure` and `Api` depend on
`Application` and `Domain`. Nothing depends on `Api`.

---

## 2.4 Solution / project structure

```
QuizApp.sln
│
├── src/
│   ├── QuizApp.Domain/                      (no external packages)
│   │   ├── Common/                          BaseEntity, ITenantScoped, IAuditable
│   │   ├── Programs/                        Program, ProgramSetting, Branding
│   │   ├── Teams/                           Team, TeamMember, TeamStatus
│   │   ├── QuestionBank/                    Question, QuestionOption, Topic, Tag, MediaAsset
│   │   ├── Tournament/                      Stage, Match, MatchParticipant, TurnOrder
│   │   ├── Gameplay/                        MatchSegment, MatchQuestion, AnswerRecord
│   │   ├── Scoring/                         ScoringRule, ScoreEvent, TeamStanding
│   │   ├── Qualification/                   QualificationRule, StageQualification
│   │   ├── Buzzer/                          BuzzSession, BuzzPress, BuzzDeviceMapping
│   │   └── Enums/                           QuestionFormat, AnswerOutcome, MatchState...
│   │
│   ├── QuizApp.Application/
│   │   ├── Abstractions/                    IAppDbContext, ICurrentUser, IClock,
│   │   │                                    IBuzzerProvider, IQuestionSelector,
│   │   │                                    IQualificationEvaluator, IScoringEngine
│   │   ├── Programs/                        Commands, Queries, DTOs, Validators
│   │   ├── Teams/
│   │   ├── QuestionBank/
│   │   ├── Tournament/
│   │   ├── Gameplay/                        The match engine use cases
│   │   ├── Scoring/
│   │   ├── Qualification/
│   │   ├── Reporting/
│   │   └── Common/                          Result<T>, PagedList<T>, Errors
│   │
│   ├── QuizApp.Infrastructure/
│   │   ├── Persistence/                     AppDbContext, configurations, migrations
│   │   ├── Repositories/
│   │   ├── Identity/                        JWT, users, roles
│   │   ├── Files/                           Media storage
│   │   ├── Caching/
│   │   └── Outbox/                          Reliable event dispatch
│   │
│   ├── QuizApp.Modules.Buzzer/              OPTIONAL — can be removed entirely
│   │   ├── Abstractions/                    (references only Application ports)
│   │   ├── Serial/                          SerialBuzzerAdapter  (from QuickBuzz)
│   │   ├── Agent/                           HttpAgentBuzzerAdapter
│   │   ├── Manual/                          NullBuzzerAdapter    (DEFAULT)
│   │   └── BuzzerModuleExtensions.cs        AddBuzzerModule(configuration)
│   │
│   └── QuizApp.Api/
│       ├── Controllers/v1/
│       ├── Hubs/                            MatchHub, DisplayHub
│       ├── Middleware/                      Exception handler, tenant resolver
│       ├── Filters/                         Idempotency, audit
│       └── Program.cs
│
├── tools/
│   └── QuizApp.BuzzerAgent/                 Small console/tray app for the operator PC
│                                            (owns the COM port, pushes to the API)
└── tests/
    ├── QuizApp.Domain.Tests/
    ├── QuizApp.Application.Tests/
    ├── QuizApp.Api.IntegrationTests/
    └── QuizApp.Architecture.Tests/          Enforces the dependency rule
```

---

## 2.5 Core modules (bounded contexts)

| # | Module | Owns | Talks to |
|---|---|---|---|
| 1 | **Identity & Access** | Users, roles, permissions, tokens | Everything |
| 2 | **Program Management** | Programs (tenants), settings, branding, scoring rules | All |
| 3 | **Team Management** | Teams, members, registration, images, status | Tournament |
| 4 | **Question Bank** | Questions, options, topics, tags, difficulty, media, import | Gameplay |
| 5 | **Tournament** | Stages, matches, participants, turn order, brackets | Gameplay, Qualification |
| 6 | **Gameplay Engine** | Live match flow, segments, serving questions, recording answers | Question Bank, Scoring, Buzzer |
| 7 | **Scoring** | Scoring rules, score events, standings, undo | Gameplay |
| 8 | **Qualification** | Who advances from one stage to the next, and tie-breaking | Tournament, Scoring, Gameplay |
| 9 | **Display** | Read-only projections for the projector screens | Gameplay, Scoring |
| 10 | **Reporting** | Standings, exports, event summary | Scoring, Tournament |
| 11 | **Buzzer Integration** *(optional)* | Buzz sessions, presses, device mapping | Gameplay (through a port) |

**Rule between modules:** a module may only call another module through its
public Application-layer interface. No module reaches into another module's
tables directly. This is enforced by an architecture test.

---

## 2.6 Multi-program (multi-tenant) strategy

**Chosen approach: one database, shared schema, a `ProgramId` column on every
tenant-owned table.**

Why this and not database-per-program (today's approach) or schema-per-program:

| Approach | Verdict |
|---|---|
| Database per program (current) | ✗ Cannot compare years, needs redeployment, backup nightmare, connection-string juggling |
| Schema per program | ✗ Migrations must run N times, queries across programs become impossible |
| **Shared schema + `ProgramId`** | ✓ One migration, cross-program reporting, one backup, simple |

### How it is enforced

1. Every tenant table has `ProgramId UNIQUEIDENTIFIER NOT NULL` with an FK to
   `Program`.
2. Every entity implements `ITenantScoped`.
3. EF Core applies a **global query filter** so a developer physically cannot
   forget the `WHERE ProgramId = ...`:

```csharp
modelBuilder.Entity<Match>()
    .HasQueryFilter(m => m.ProgramId == _tenant.CurrentProgramId
                      && !m.IsDeleted);
```

4. On `SaveChanges`, `ProgramId` is stamped automatically on new rows.
5. The current program comes from the **JWT claim**, not from a query string, so
   a user cannot switch tenants by editing the URL.
6. Every unique index includes `ProgramId` — e.g. team codes are unique *within*
   a program, not globally.

### Shared vs program-owned data

The question bank is the interesting case. A question can be:

- **Program-owned** — created for this event only
- **Organisation-owned (shared library)** — reusable across every year

So `Question` has `OwnerScope` (`Program` or `Organisation`) and a nullable
`ProgramId`. The question selector can be told which pools to draw from.

---

## 2.7 Configuration-driven tournament engine

This is the heart of the redesign. Nothing about the tournament shape is in code.

```
Program  "9AMM Quiz 2026"
  │
  ├── Stage 1  "League"       order=1  advanceMode=Automatic
  │     ├── QualificationRule: 1 winner per match + 3 best runners-up
  │     ├── SegmentTemplate: MCQ(6) → AudioVisual(3) → Sequence(2) → Buzzer(5)
  │     ├── Match 1  [Team A, Team B, Team C]
  │     ├── Match 2  [Team D, Team E, Team F]
  │     └── ... 6 matches
  │
  ├── Stage 2  "Semi-Final"   order=2
  │     ├── QualificationRule: 1 winner per match
  │     ├── SegmentTemplate: MCQ(6) → AV(3) → RapidFire(1) → Passing(3) → Card(3) → Choice(6)
  │     └── 3 matches
  │
  └── Stage 3  "Final"        order=3
        ├── QualificationRule: 1 champion
        └── SegmentTemplate: Sequence(2) → AV(3) → VisualRapidFire(1) → ...
```

**Everything in that tree is data**, stored in tables:

- How many stages → rows in `Stage`
- How many matches per stage → rows in `Match`
- How many teams per match → rows in `MatchParticipant` (2, 3, 4 — any number)
- **Which formats are played at all** → rows in `StageSegmentTemplate`
  (no row = that format is never used; **no format is compulsory**)
- **In what order they are played** → `StageSegmentTemplate.OrderIndex`
- How points are awarded → rows in `ScoringRule`
- Who advances → rows in `QualificationRule`
- **How a tie is settled** → rows in `TieBreakRule`
- Which question to serve next → rows in `QuestionSelectionRule`

To run a completely different tournament next year, an admin fills in forms.
**No code changes, no redeployment.**

---

## 2.8 Dynamic teams and disqualification

The `QuestionNumber % 3` problem is solved by making turn order explicit data.

### `MatchParticipant`

| Column | Meaning |
|---|---|
| `SeatNumber` | Physical position on stage (1, 2, 3, …) — never changes |
| `TurnOrder` | Position in the answering rotation — **recalculated** when a team leaves |
| `Status` | `Active`, `Disqualified`, `Withdrawn`, `Substituted` |
| `RemovedAtSegmentId` | Where in the match they were removed |
| `RemovalReason` | Free text, for the audit trail |

### What happens when the admin disqualifies a team mid-match

1. `POST /matches/{matchId}/participants/{teamId}/disqualify`
2. The participant's `Status` becomes `Disqualified`.
3. `TurnOrder` is recalculated for the remaining **active** participants only.
4. A `MatchEvent` row records the removal (who, when, why).
5. The current segment's remaining question count is recalculated using the
   configured `TeamCountChangePolicy`:
   - `KeepPlanned` — keep the same number of questions, just skip the removed
     team's turns
   - `Rebalance` — recompute so every remaining team gets an equal number
   - `TruncateSegment` — end the segment now
6. The removed team's existing score events are **kept but marked excluded from
   standings** (so history is not lost).
7. SignalR pushes the new match state to every screen.

**The turn-order calculation becomes:**

```csharp
var active = participants.Where(p => p.Status == Active)
                         .OrderBy(p => p.TurnOrder).ToList();
var next = active[questionIndexInSegment % active.Count];
```

The team count is read from the data at that instant, so 3 → 2 works, and so
does 3 → 4.

**No fake answers are ever required.**

---

## 2.9 Dynamic question selection

Questions are **no longer tied to a match or a position**. They live in one bank
with attributes, and the engine draws them at run time.

### Question attributes

`Format`, `DifficultyLevel` (1–5), `TopicId`, `Tags`, `Language`,
`EstimatedSeconds`, `MediaAssetId`, `OwnerScope`, `TimesUsed`, `LastUsedAt`.

### Selection rule (stored per stage + format)

```json
{
  "stage": "Semi-Final",
  "format": "MCQ",
  "questionCount": 6,
  "difficultyMix": { "2": 60, "3": 40 },
  "topicFilter": { "include": ["Literature","History"], "exclude": [] },
  "language": "ur",
  "repeatPolicy": "NeverInProgram",
  "topicSpread": "OneQuestionPerTopicIfPossible",
  "shuffleOptions": true
}
```

### The algorithm

1. **Build the pool** — all questions matching format, language, topic filter,
   and owner scope.
2. **Remove used questions** based on `repeatPolicy`:
   - `NeverInMatch` — exclude anything already used in this match
   - `NeverInStage` — exclude anything used in this stage
   - `NeverInProgram` *(default)* — exclude anything used anywhere in this program
   - `NeverForTeam` — additionally exclude anything this team saw in a past program
3. **Split by difficulty** according to `difficultyMix` (e.g. 60% level 2,
   40% level 3), so the match is balanced, not accidentally all-hard.
4. **Weighted random draw** inside each difficulty bucket. Weight favours
   questions with a lower `TimesUsed`, so the bank is used evenly.
5. **Topic spread** — if `OneQuestionPerTopicIfPossible`, prefer a question from
   a topic not yet used in this segment.
6. **Reserve, do not just pick.** The drawn questions are written to
   `MatchQuestion` with status `Reserved` **before** they are shown. If the
   server restarts mid-match, the same questions come back in the same order.
7. **Store the RNG seed** on the match. The whole draw is reproducible, which
   matters if a result is challenged.
8. **Fallback ladder** when the pool runs dry:
   `widen difficulty ±1` → `drop the topic filter` → `allow repeats from older
   programs` → `return a clear error naming exactly what is missing`.

Never crash mid-show because the questions ran out — the current system does
exactly that.

### Suggested difficulty policy

| Stage | Levels drawn |
|---|---|
| League / Round 1 | 1 and 2 |
| Semi-Final | 2 and 3 |
| Final | 3, 4 and 5 |
| Tie-breaker | 4 and 5, always fresh |

All of this is data, so it can be changed per program.

---

## 2.10 Optional question types

**No question format is mandatory.** A stage plays exactly the segments
configured for it. Dropping a format from a year's event means not creating a
segment for it — there is no flag to unset, no placeholder row, and no questions
to author.

```
Stage "League"  (this year)          Stage "League"  (last year)
  ├── MCQ            × 6               ├── MCQ            × 6
  ├── AudioVisual    × 3               ├── AudioVisual    × 3
  ├── Sequence       × 2               ├── Sequence       × 2
  └── Buzzer         × 5               ├── Buzzer         × 5
                                       └── Passing        × 3   ← simply not
  Passing / Card / Choice /                                        created this year
  RapidFire / VisualRapidFire:
  not configured → never played
```

This falls out of the design rather than being a feature bolted on: the engine
iterates the segment rows that exist. Nothing enumerates the ten formats and asks
"where is Passing?".

**What it means downstream**

| Concern | Behaviour for an unused format |
|---|---|
| Question bank | No questions needed. Authors are never nagged for them. |
| Coverage report | The format is not listed and cannot be a blocker |
| Readiness validation | Not checked — only configured formats are validated |
| Scoring rules | Seeded rows exist but are never looked up; harmless |
| Selection rules | None needed |
| Match engine | Never reached; no branch exists for it |
| Admin UI | Hidden entirely if disabled via `ProgramQuestionFormat` |

**The three meanings of "optional"** — these are genuinely different and the
docs keep them apart:

| Meaning | Mechanism | Typical use |
|---|---|---|
| **Not used this year** | No `StageSegmentTemplate` row | "We are not doing Passing in 2026" |
| **Planned but skippable** | `StageSegmentTemplate.IsOptional = 1` | "Run Rapid Fire only if we are ahead of schedule" |
| **Hidden program-wide** | `ProgramQuestionFormat.IsEnabled = 0` | "Nobody should be able to pick Buzzer — we have no hardware" |

The first needs no configuration at all. The third is a convenience so unused
formats do not clutter the admin screens, and it is guarded: a format still
referenced by a segment template cannot be disabled, and the error names the
stages using it.

**The only floor:** a stage needs at least one segment. There is no minimum set
of formats, and no format the system insists on.

---

## 2.11 Configurable order of question types

The order in which question formats are played is a single integer column,
`StageSegmentTemplate.OrderIndex`. Changing it is a `PUT` with the reordered list
of segment ids — the natural output of a drag-and-drop list in the admin console.

In the current system this order is a hardcoded `window.location.href` in each of
108 Razor views (finding 1.4g), so changing it means editing and redeploying.

**Three levels, most specific winning:**

```
Stage template          StageSegmentTemplate.OrderIndex
   │                    the default for every match in the stage
   │  copied at match creation
   ▼
Per match               MatchSegment.OrderIndex
   │                    editable per match — Semi-Final 1 can differ from 2
   │  editable while Pending
   ▼
Live                    operator reorders pending segments mid-match
                        (only when Stage.AllowSegmentReorderDuringMatch = 1)
```

**Three ordering modes**, set per stage:

| Mode | Behaviour | When to use |
|---|---|---|
| `Fixed` | Every match plays the template order | The normal case |
| `RandomPerMatch` | Shuffled from the match's stored `RandomSeed` | Stops later teams learning the running order from earlier matches — and stays reproducible because the seed is stored |
| `OperatorChoice` | The operator picks the next segment live | Flexible show running |

A segment can be marked `IsOrderLocked` so it holds its position even when the
rest are shuffled — for example, always finishing on Buzzer for the drama.

**Two safety rules that matter on stage:**

1. A match takes its order **at creation**. Editing the stage template later
   never disturbs a match that is already created or running.
2. Only `Pending` segments can be reordered live. A segment that is open or
   finished never moves, so scores and question reservations stay coherent.

Every reorder writes a `MatchEvent`, so the order actually played can always be
reconstructed afterwards.

---

## 2.12 Tie-break architecture

Ties are settled in two phases, and **the second phase reuses the match engine
rather than adding a parallel one.**

```
   Qualification preview detects a tie
   (e.g. 9th and 10th level on 110, one wildcard place left)
                    │
      ┌─────────────▼──────────────┐
      │  PHASE 1 — criteria        │   instant, no stage time
      │  ordered, configurable:    │
      │   TotalScore               │
      │   FewerIncorrect           │
      │   MoreCorrectAtHighDiff    │
      │   FasterAverageBuzzTime    │
      │   HeadToHead               │
      └─────────────┬──────────────┘
             separated? ──yes──► record which criterion decided it ──► done
                    │ no
      ┌─────────────▼──────────────────────────────┐
      │  PHASE 2 — play a tie-break                │
      │  Create Match(MatchKind = TieBreak)        │
      │   • only the tied teams                    │
      │   • format from TieBreakRule — MCQ default │
      │   • questions from the normal selector     │
      │   • runs on THE SAME match engine          │
      └─────────────┬──────────────────────────────┘
                    │
             still level after MaxExtraRounds?
                    │
             ManualDecision / CoinToss / ShareTheSlot
                    │
                    ▼
             TieBreakEvent.state = Resolved
             StageQualification records the reason + TieBreakEventId
```

**Why a tie-break is modelled as an ordinary match**

| Alternative | Problem |
|---|---|
| A special tie-break mode inside the engine | A second code path that is exercised once a year — the least-tested code running at the most contested moment |
| A separate tie-break screen (today's approach) | It is what already exists, and it records nothing |
| **A `Match` with `MatchKind = TieBreak`** | Reuses serving, scoring, undo, display, audit and recovery for free |

The only genuinely new gameplay rule is **sudden death**
(`MatchSegment.IsSuddenDeath`): close the segment as soon as one team leads,
provided every tied team has faced the same number of questions.

**Why MCQ is the default format.** It is fast, unambiguous, needs no media file
and no buzzer hardware, and works with two teams as readily as four — the right
default when a tie-break is unplanned and the audience is waiting.
`TieBreakRule.TieBreakFormatId` accepts any of the ten formats, so an organiser
can switch the final's decider to Buzzer from a dropdown.

**Scoring integrity.** `ScoreCountsTowardStage` defaults to `false`: the
tie-break decides the *order*, not the *points*. Without this, a team could
finish the League on more points than a team that beat them outright.

---

## 2.13 API architecture

- **REST over HTTPS**, versioned as `/api/v1/...`
- **JSON** in and out, `camelCase`
- **JWT bearer tokens**, short-lived access token + refresh token
- **SignalR** for anything live (score changes, question shown, timer, buzz
  results)
- **RFC 9457 `application/problem+json`** for every error
- **Idempotency-Key header** required on all gameplay POSTs
- **ETag / If-Match** on updates to avoid two operators overwriting each other
- **OpenAPI** document generated automatically; Angular clients generated from it
- **Rate limiting** per user and per IP
- **Pagination** on every list endpoint (`page`, `pageSize`, max 200)

### Endpoint groups

```
/api/v1/auth/*                     login, refresh, logout
/api/v1/programs/*                 program CRUD, settings, branding, clone
/api/v1/programs/{id}/teams/*      team management
/api/v1/programs/{id}/questions/*  question bank, import, media
/api/v1/programs/{id}/stages/*     stage + segment template config
/api/v1/programs/{id}/matches/*    match setup, participants
/api/v1/matches/{id}/live/*        THE GAMEPLAY ENGINE
/api/v1/matches/{id}/scores/*      scores, undo, adjustments
/api/v1/programs/{id}/standings    leaderboards
/api/v1/programs/{id}/qualification preview + commit advancement
/api/v1/buzzer/*                   OPTIONAL module
/api/v1/display/*                  read-only, separate token
/hubs/match                        SignalR — operators
/hubs/display                      SignalR — projector screens
```

---

## 2.14 Integration architecture for the optional QuickBuzz

### The rule

> **QuizApp must run perfectly with QuickBuzz completely absent.**

### How that is guaranteed

The Application layer defines a **port**:

```csharp
public interface IBuzzerProvider
{
    bool IsAvailable { get; }
    Task<BuzzerHealth> CheckHealthAsync(CancellationToken ct);
    Task<BuzzSessionHandle> ArmAsync(BuzzArmRequest request, CancellationToken ct);
    Task<IReadOnlyList<BuzzPressResult>> CollectAsync(Guid sessionId, CancellationToken ct);
    Task ResetAsync(Guid sessionId, CancellationToken ct);
}
```

Three **adapters** implement it:

| Adapter | When it is used | Notes |
|---|---|---|
| `NullBuzzerAdapter` | **Default.** No hardware configured. | `IsAvailable = false`. The UI simply shows manual buttons. |
| `HttpAgentBuzzerAdapter` | **Recommended for production.** | Talks to a small `BuzzerAgent` running on the operator's PC, which owns the COM port. |
| `SerialBuzzerAdapter` | API itself runs on the machine with the RS485 cable | Direct `System.IO.Ports`, ported from QuickBuzz's `SerialService`. |

Selected purely by configuration:

```json
"Buzzer": {
  "Provider": "HttpAgent",        // None | Serial | HttpAgent
  "DeviceCount": 3,                // configurable; 3 is today's default, any number is supported
  "Agent": { "BaseUrl": "http://localhost:5299", "TimeoutMs": 3000 },
  "Serial": { "PortName": "COM3", "BaudRate": 38400, "FrameLength": 24,
              "ReadTimeoutMs": 1500, "RetryCount": 3, "BusDelayMs": 150 }
}
```

**`DeviceCount` is a default, not a limit.** It seeds how many
`BuzzDeviceMapping` rows a new program starts with and how many device tiles the
operator console shows before any are mapped. Adding a 4th or 5th device is a
configuration change, not a code change — `PollAll()` iterates whatever devices
are mapped, not a hardcoded array of three.

`Provider: "None"` removes the module entirely. **You can delete the whole
`QuizApp.Modules.Buzzer` project and the solution still builds and runs.**

### Recommended physical topology

**Hosting decision:** the API runs **locally, on a venue server** on the same
network as the hall (a laptop, mini-PC, or on-prem box) — not in the cloud, and
with no cloud dependency assumed. This keeps the whole show working even if the
venue's internet connection is unreliable, which matters far more than remote
access for a one-evening event.

```
   Operator PC (in the hall)              Venue server (local network)
┌──────────────────────────────┐            ┌────────────────────┐
│  Angular Operator Console    │            │  QuizApp.Api       │
│              │               │  LAN HTTPS │                    │
│              └───────────────┼───────────►│  Gameplay engine   │
│                              │            │  Scoring, SignalR  │
│                              │            │  SQL Server (local)│
│  BuzzerAgent (tray app)      │            └────────▲───────────┘
│   ├─ owns COM3 / RS485       │   outbound LAN HTTPS │
│   ├─ polls devices           │───────────────────────┘
│   └─ pushes buzz results ────┼──► POST /api/v1/buzzer/sessions/{id}/presses
│                              │
│  ┌─────────┬─────────┬──────────────┐
│  │Device 1 │Device 2 │Device 3 ... N│  RS485 bus, 38400 baud
│  └─────────┴─────────┴──────────────┘
└──────────────────────────────┘
```

**Why the agent still pushes outward, even on a local network:** the operator
PC's network position (which Wi-Fi access point, which VLAN, whether it sits
behind client isolation) is not guaranteed. An outbound push from the agent to
the API works regardless of that topology and needs no port forwarding or
firewall rule on either side — so it stays the right pattern whether the API is
local or ever moved to the cloud later.

**What "assume local" simplifies:** no cloud failover path, no WAN latency
budget, and TLS can use a private/internal certificate rather than a public one.
If a future program needs remote access (a second venue, a remote reviewer),
that is a configuration change to the API's binding — the architecture does not
need to change.

### The graceful-degradation flow

```
Operator opens a Buzzer segment
        │
        ▼
  GET /matches/{id}/live/buzzer/capability
        │
   ┌────┴────┐
   │available│  no ──► UI shows: "Which team buzzed first?" [Team A][Team B][Team C]
   └────┬────┘         Operator clicks. Recorded with source = Manual.
        │ yes
        ▼
  POST .../buzzer/arm   → devices reset and armed, server timer starts
        ▼
  Agent detects presses, pushes them
        ▼
  SignalR broadcasts ranked presses to all screens
        ▼
  Operator confirms the winner (always able to override)
        ▼
  POST .../answers   with buzzSource = Hardware, buzzSessionId = ...
```

The important part: **the answer-recording endpoint is identical either way.**
The buzzer only supplies *who answers first*; it never awards points and never
touches scores. If it fails halfway through a match, the operator falls back to
manual clicks for that one question and carries on.

### What we keep from QuickBuzz

- `DeviceParser` — the 24-byte frame parser, unchanged
- `SerialService` — becomes `SerialBuzzerAdapter`, with the hardcoded values
  moved to configuration and `Console.WriteLine` replaced with `ILogger`
- The ranking algorithm — moved into the Domain layer as a pure, testable
  function, so it can be unit-tested without hardware

### What we add

- `BuzzDeviceMapping` — maps `DeviceId` to a `MatchParticipant`, so the system
  knows device 2 is Team B
- `BuzzSession` and `BuzzPress` tables — buzz times are **persisted**, giving an
  evidence trail for disputes
- A health-check endpoint the operator can run before the show starts
- Support for **any number of devices**; the count is configurable with a default of 3 (today's hardware), not hardcoded

---

## 2.15 Cross-cutting concerns

### Security

| Concern | Approach |
|---|---|
| Authentication | ASP.NET Core Identity + JWT (access 15 min, refresh 7 days) |
| Authorisation | Role + permission policies; every endpoint has an explicit policy |
| Tenant isolation | `ProgramId` from the token claim, enforced by EF global query filter |
| Display machines | A separate scoped, read-only token that cannot write |
| Input validation | FluentValidation on every request DTO; reject unknown properties |
| XSS | Never turn off validation; sanitise rich text on write; Angular escapes on render |
| SQL injection | EF Core parameterised queries only; **no raw SQL string building** |
| Secrets | User Secrets in development, environment variables or Key Vault in production |
| Transport | HTTPS enforced, HSTS, TLS 1.2+ |
| Headers | CSP, X-Content-Type-Options, Referrer-Policy |
| Rate limiting | Fixed window per user; stricter on `/auth/login` |
| Audit | Every write records `CreatedBy` / `UpdatedBy`; gameplay writes an event row |
| File upload | Extension allow-list, magic-byte check, size cap, virus scan hook, store outside the web root |

### Scalability

The realistic load is small (a few dozen users, one hall), but the design should
not fall over:

- **Scores are pre-aggregated** in `TeamMatchScore` and updated incrementally on
  each score event — no more "8 subqueries per team per click".
- **Read models for the display screens** are cached in memory and invalidated by
  domain events.
- **SignalR replaces polling**, cutting request volume by roughly 90%.
- The API is **stateless**, so it can be scaled out; SignalR uses a backplane
  only if more than one instance is ever needed.
- Async all the way down; no blocking calls on request threads. (The serial port
  is the one blocking resource, and it lives in the separate agent process.)
- Indexes designed for the actual query patterns (see the schema document).

### Maintainability

- A `Question` base table plus one small table per format, instead of ten tables
  each repeating the same 15 columns → the shared code and columns exist once.
- One gameplay engine instead of three round controllers.
- Strategy pattern per question format, so adding a new format means adding one
  class and one lookup row — not a new table, controller and set of views.
- All magic numbers become configuration rows.
- Everything is dependency-injected and therefore testable.
- Architecture tests fail the build if a layer dependency is violated.

### Extensibility

Designed in from the start:

- **New question format** → one `IQuestionFormatHandler`, one lookup row, and one
  small migration adding its table (only the columns that format actually needs —
  nothing else in the system changes, because everything references
  `Question.Id`)
- **New tournament shape** → data only
- **New running order of question types** → data only, reorderable from a screen
- **Dropping or adding a question type for a year** → add or remove one segment row
- **New tie-break policy** → data only (criteria order, format, counts, fallback)
- **New scoring model** → data only
- **New qualification rule** → data, or one `IQualificationStrategy` class
- **New buzzer hardware** → one `IBuzzerProvider` implementation
- **New output (mobile, TV overlay)** → another API consumer
- **Public/audience voting, online qualifying rounds, live streaming overlays**
  are all natural additions because the API is already the single source of truth

---

## 2.16 Key architecture decisions summary

| # | Decision | Main alternative rejected |
|---|---|---|
| 1 | Modular monolith | Microservices — unnecessary complexity for one hall |
| 2 | Clean Architecture with 4 projects | Single project — becomes unmaintainable, as today |
| 3 | Shared schema + `ProgramId` | Database per program — today's pain |
| 4 | Table-Per-Type questions: a `Question` base table + one table per format | (a) Ten independent tables — reproduces today's duplication of the shared columns; (b) one wide table — every format-specific column would have to be nullable and unenforceable |
| 5 | Configuration-driven tournament | Hardcoded rounds — today's pain |
| 6 | Event-sourced scoring | Mutable score column — no undo, no audit |
| 7 | Buzzer behind a port with a Null default | Direct dependency — breaks the "must work without it" rule |
| 8 | Agent pushes to API | API polls the agent — impossible through venue NAT |
| 9 | EF Core 10, code-first migrations | EDMX — obsolete and unmaintainable |
| 10 | SignalR for live updates | AJAX polling — today's approach, wasteful and laggy |
| 11 | Segment order is a single `OrderIndex` column, editable at stage, match and live level | A hardcoded redirect chain in the views — today's pain (finding 1.4g) |
| 11a | Question types are opt-in: a format is played only if a segment exists for it | A fixed set of formats every event must run — today's implicit assumption |
| 12 | A tie-break is an ordinary `Match` with `MatchKind = TieBreak`, run by the same engine | A special tie-break mode — a second code path exercised once a year, at the most contested moment |
