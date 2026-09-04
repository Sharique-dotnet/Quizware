# 3. Product Requirements Document — Backend API

**Product:** QuizApp API
**Version:** 1.0
**Scope:** ASP.NET Core Web API on .NET 10 + SQL Server. Angular 22 is a later
phase and is out of scope here, except where it shapes API requirements.

---

## 3.1 Goals

### Primary goals

| # | Goal | How we know it worked |
|---|---|---|
| G1 | Run many quiz programs in **one database** | A second program can be created without any deployment |
| G2 | Make the tournament shape **configurable** | A 4-team, 5-stage tournament can be set up through the API alone |
| G3 | Let a match **continue after a team is removed** | A 3-team match finishes correctly with 2 teams and no fake answers |
| G4 | Serve questions **dynamically** from a bank | No question is pre-assigned to a match or a position |
| G5 | Make QuickBuzz **optional** | With the buzzer module removed, every match still completes |
| G6 | Remove the duplication | Shared question columns defined once, one options table, one gameplay engine |
| G7 | Add **security and audit** | Every write is authenticated and attributable to a user |
| G8 | Make the system **recoverable** | A mid-match restart resumes at the same question |

### Non-goals for version 1

- The Angular front end (separate phase)
- Public/audience voting
- Live streaming or broadcast overlays
- Mobile apps
- Migrating the historical 9AMM data (optional, see the roadmap)

---

## 3.2 Actors and roles

| Role | Who they are | What they can do |
|---|---|---|
| **SuperAdmin** | Platform owner | Everything, across all programs. Creates programs and program admins. |
| **ProgramAdmin** | Event organiser | Full control of **their own** program: teams, questions, stages, matches, rules, users. |
| **QuestionAuthor** | Content writer | Create and edit questions in the bank. Cannot see live match data. |
| **QuizMaster / Operator** | Person driving the show | Runs a live match: start, serve question, record answer, undo, pause, end. |
| **Scorer** | Assistant | Record answers and request corrections. Cannot change tournament setup. |
| **Display** | The projector machine | **Read-only.** Sees the live match state and standings. Cannot write anything. |
| **Auditor** | Reviewer, after the event | Read-only access to everything including the full audit log. |

### Permission matrix (summary)

| Capability | Super | ProgAdmin | Author | Operator | Scorer | Display | Auditor |
|---|:--:|:--:|:--:|:--:|:--:|:--:|:--:|
| Create program | ✓ | | | | | | |
| Manage program settings | ✓ | ✓ | | | | | |
| Manage teams | ✓ | ✓ | | | | | |
| Manage question bank | ✓ | ✓ | ✓ | | | | |
| Configure stages / rules | ✓ | ✓ | | | | | |
| Create / seed matches | ✓ | ✓ | | | | | |
| Start / end a match | ✓ | ✓ | | ✓ | | | |
| Serve a question | ✓ | ✓ | | ✓ | | | |
| Record an answer | ✓ | ✓ | | ✓ | ✓ | | |
| Undo an answer | ✓ | ✓ | | ✓ | | | |
| Manual score adjustment | ✓ | ✓ | | | | | |
| Disqualify a team | ✓ | ✓ | | | | | |
| Commit qualification | ✓ | ✓ | | | | | |
| View live match | ✓ | ✓ | | ✓ | ✓ | ✓ | ✓ |
| View audit log | ✓ | ✓ | | | | | ✓ |

---

## 3.3 Core features

### F1 — Program (tenant) management
Create a program, set its dates, branding, language, default scoring rules and
selection rules. **Clone an existing program** so next year's event starts from
last year's configuration. Lifecycle: `Draft → Configured → Live → Completed →
Archived`.

### F2 — Team management
Register teams (school name, display name, short code, images, optional member
list). Bulk import from Excel with a **validation preview** before committing.
Team status: `Registered`, `Active`, `Withdrawn`, `Disqualified`.

### F3 — Question bank
One unified bank supporting all 10 formats. Each question has format, text,
options, difficulty (1–5), topic, tags, language, optional media, and usage
history. Bulk Excel import per format. Duplicate detection on import. Questions
can be **program-owned** or in a **shared organisation library**.

### F4 — Tournament configuration
Define stages in order. For each stage: number of matches, teams per match,
**which question formats run and in what order**, how many questions each, the
scoring rules that apply, the question selection rules, the tie-break rule, and
the qualification rule that decides who advances.

The order of question types is a first-class, editable list: reorder it by drag
and drop, override it for an individual match, optionally shuffle it per match,
or let the operator choose the next segment live.

**Which types are used is equally free.** No format is compulsory — a stage plays
exactly the segments you configure. Dropping Passing from a year's event means
not creating a Passing segment; no Passing questions are needed and nothing else
changes. A program can also switch formats off entirely so they disappear from
the admin screens.

### F5 — Match setup
Create matches inside a stage. Assign participants either manually, from a
qualification result, or by automatic seeding (random, by rank, or snake).
Assign seat numbers and turn order.

### F6 — Live gameplay engine
The core. One engine that drives every format:
`start match → open segment → serve question → record answer → advance turn →
close segment → next segment → end match`. Supports pause, resume, skip, undo
and mid-match participant removal.

### F7 — Scoring
Rules are data. Every point change is an immutable `ScoreEvent`. Standings are
maintained incrementally. Manual adjustments are allowed but must carry a reason
and ProgramAdmin approval.

### F8 — Qualification
Evaluate a stage against its rule and produce a **preview** of who advances and
why. An admin reviews and then commits. Committing creates the participants of
the next stage.

### F9 — Tie-breaking
Configurable, ordered tie-break criteria applied first (total score, fewer
incorrect, harder questions answered, faster buzz, head-to-head). If teams are
still level, the system creates a **real tie-break match** containing only the
tied teams — MCQ by default, any format if configured — runs it through the
normal match engine, and records who won and why.

This covers the case that matters most: a tie for the **last wildcard place** in
the League. Today that is decided by database row order and recorded nowhere.

### F10 — Live display feed
Read-only endpoints plus a SignalR hub giving the projector screens the current
question, the active team, live scores, the timer and buzz results.

### F11 — Buzzer integration (optional)
Arm, collect and reset a buzz session; map devices to teams; persist press
times. Always degrades to manual entry.

### F12 — Reporting and export
Match report, stage standings, program standings, per-team performance,
question-usage report, full audit log. Export to Excel, CSV and PDF.

---

## 3.4 Functional requirements

### FR-1 Programs

| ID | Requirement |
|---|---|
| FR-1.1 | The system shall support an unlimited number of programs in one database. |
| FR-1.2 | Every tenant-owned record shall carry a `ProgramId`. |
| FR-1.3 | A user's accessible programs shall be determined by their token, not by request parameters. |
| FR-1.4 | The system shall allow cloning a program's configuration (stages, rules, templates) without its results. |
| FR-1.5 | A program shall not move to `Live` until it has at least one stage, one match and enough questions to satisfy **the selection rules of the formats it actually uses**. Formats the program does not use shall not be checked. |
| FR-1.6 | Programs shall be archivable, never hard-deleted. |

### FR-2 Teams

| ID | Requirement |
|---|---|
| FR-2.1 | Teams shall be created individually or by Excel import. |
| FR-2.2 | Excel import shall return a row-by-row validation report **before** any data is saved. |
| FR-2.3 | There shall be **no hardcoded limit** on the number of teams. A per-program maximum may be configured. |
| FR-2.4 | Team short code shall be unique within a program. |
| FR-2.5 | A team's status change shall be recorded with a reason, a timestamp and the acting user. |
| FR-2.6 | A team already used in a completed match shall not be deletable, only withdrawn. |

### FR-3 Question bank

| ID | Requirement |
|---|---|
| FR-3.1 | Each question format shall have its own table holding the fields specific to that format, sharing a `Question` base table for the columns common to all formats (identity, difficulty, topic, language, status, usage). |
| FR-3.1a | A format table shall use the base question's id as its own primary key, so the relationship is one-to-one. |
| FR-3.1b | All other parts of the system (matches, answers, rules, usage history) shall reference the base `Question` only, and shall not branch on format at the data-access level. |
| FR-3.2 | A question shall **not** reference any stage, match or question number. |
| FR-3.3 | Every question shall have a difficulty level from 1 to 5. |
| FR-3.4 | Every question shall have exactly one topic and any number of tags. |
| FR-3.5 | The six option-based formats (MCQ, Buzzer, Passing, Card, Choice, TieBreaker) shall support 2 to 8 options in `QuestionOption`, with exactly one marked correct. |
| FR-3.6 | Sequence questions shall store their items in `SequenceItem`, each with a correct position forming a contiguous 1..N with no gaps or duplicates. |
| FR-3.7 | Media-based formats shall reference a `MediaAsset` through a **non-nullable** column on their own format table, and shall reject files failing extension, magic-byte or size validation. |
| FR-3.7a | Visual Rapid Fire questions shall store a set of images in `VisualRapidFireItem`, each with its own answer text. |
| FR-3.8 | The system shall record `TimesUsed` and `LastUsedAtUtc` on every question. |
| FR-3.9 | Import shall flag near-duplicate question text within the same program. |
| FR-3.9a | The question bank shall never require questions for a format the program does not use. Coverage and readiness reports shall list only formats that appear in a segment template. |
| FR-3.10 | A question used in a live match shall not be editable; a new version shall be created instead. |

### FR-4 Tournament configuration

| ID | Requirement |
|---|---|
| FR-4.1 | Stages shall be ordered and unlimited in number. |
| FR-4.2 | Teams per match shall be configurable per stage (minimum 2, no fixed maximum). |
| FR-4.3 | Matches per stage shall be configurable. |
| FR-4.4 | A stage shall define an ordered list of segments; each segment names a format and a question count. |
| FR-4.4-a | **No question format shall be compulsory.** A stage plays exactly the segments configured for it and nothing else. Omitting a format from a stage shall require no questions, no scoring rules and no selection rules for that format. |
| FR-4.4-b | A stage shall require at least one segment before it may start; there shall be no other minimum, and no format the system insists on. |
| FR-4.4-c | A program shall be able to enable or disable each question format, so unused formats are hidden from admin screens, question-bank filters and import templates. |
| FR-4.4-d | Disabling a format shall not delete existing questions, alter matches already created, or affect any other program. |
| FR-4.4-e | A format that is referenced by an existing segment template shall not be disableable until those templates are removed; the error shall name the stages using it. |
| FR-4.4a | **The order of question types shall be editable through the API** by supplying a reordered list of segment ids, with no code change and no deployment. |
| FR-4.4b | The order shall be overridable for an individual match, so two matches in the same stage may run different orders. |
| FR-4.4c | A stage shall support a `SegmentOrderMode` of `Fixed`, `RandomPerMatch` (shuffled from the match's stored seed, so it stays reproducible) or `OperatorChoice` (the operator picks the next segment live). |
| FR-4.4d | A segment may be marked order-locked so it stays in place even when the rest are shuffled — for example, always ending on Buzzer. |
| FR-4.4e | Where the stage permits it, the operator shall be able to reorder segments that are still `Pending` during a live match; segments already opened or completed shall not move. |
| FR-4.5 | Scoring rules shall be defined per program and may be overridden per stage or per segment. |
| FR-4.6 | Selection rules shall be defined per stage and format. |
| FR-4.7 | Tie-break rules shall be defined per program and may be overridden per stage. |
| FR-4.8 | Configuration shall be validated before the stage is allowed to start, including that segment order indexes are unique and contiguous. |

### FR-5 Match setup

| ID | Requirement |
|---|---|
| FR-5.1 | Participants shall be assignable manually, from qualification results, or by automatic seeding. |
| FR-5.2 | The system shall reject a match whose participant count is outside the stage's configured range. |
| FR-5.3 | The same team shall not appear twice in one match. |
| FR-5.4 | Every participant shall have a distinct `SeatNumber` and `TurnOrder` within the match. |
| FR-5.5 | Match state shall be one of `Draft, Ready, InProgress, Paused, Completed, Abandoned`. |

### FR-6 Live gameplay

| ID | Requirement |
|---|---|
| FR-6.1 | Starting a match shall reserve all its questions up front and store the RNG seed. |
| FR-6.2 | `GET /matches/{id}/live/state` shall return everything a client needs to render the screen, in one call. |
| FR-6.3 | The next answering team shall be computed from **active participants only**. |
| FR-6.4 | Recording an answer shall be a single atomic transaction covering the answer, the score event and the standings update. |
| FR-6.5 | Every gameplay POST shall require an `Idempotency-Key`; a repeat of the same key shall return the original result without acting twice. |
| FR-6.6 | The operator shall be able to skip a question; the skip shall be recorded, not silently ignored. |
| FR-6.7 | The operator shall be able to undo the last answer; the undo shall create a compensating score event, never a delete. |
| FR-6.8 | Removing a participant mid-match shall recalculate turn order and continue the match. |
| FR-6.9 | The system shall **never require a fake or wrong answer** to progress. |
| FR-6.10 | If the server restarts mid-match, the same state, question order and scores shall be restored. |
| FR-6.11 | Segment timers shall be server-authoritative and pushed to clients. |
| FR-6.12 | A match shall not be completable while any segment is still open, unless it is explicitly abandoned. |

### FR-7 Scoring

| ID | Requirement |
|---|---|
| FR-7.1 | Points shall come from `ScoringRule` rows, never from constants in code. |
| FR-7.2 | A rule shall be keyed by format + outcome + optional context (for example Passing "after a pass"). |
| FR-7.3 | Every point change shall create an immutable `ScoreEvent`. |
| FR-7.4 | A team's score shall equal the sum of its non-reversed score events. |
| FR-7.5 | Manual adjustments shall require a reason and ProgramAdmin approval. |
| FR-7.6 | Negative totals shall be allowed unless the program disables them. |
| FR-7.7 | Score events of a disqualified team shall be retained but excluded from standings. |

### FR-8 Qualification

| ID | Requirement |
|---|---|
| FR-8.1 | Rules shall express: N winners per match, plus M best remaining across the stage, plus optional manual wildcards. |
| FR-8.2 | The system shall provide a **preview** listing every qualifying team with the reason (`MatchWinner`, `BestRemaining`, `Manual`, `TieBreakWin`, `CriteriaTieBreak`). |
| FR-8.3 | Ties on the qualification boundary shall first be resolved by the configured ordered criteria, and the criterion that broke the tie shall be recorded. |
| FR-8.3a | Where the criteria cannot separate the teams, the system shall create a **tie-break match** containing only the tied teams, built from the stage's tie-break rule. |
| FR-8.3b | The tie-break question format shall be configurable and shall **default to MCQ**. |
| FR-8.3c | The tie-break match shall run through the normal match engine, and its result shall be recorded as a `TieBreakEvent` with a rank for each tied team. |
| FR-8.3d | Whether tie-break points count toward the stage score shall be configurable, and shall default to **not counting**. |
| FR-8.3e | Where the teams are still level after the configured maximum extra rounds, the system shall apply the configured fallback (manual decision, coin toss, or sharing the slot). A manual decision shall require a reason and ProgramAdmin approval. |
| FR-8.3f | Qualification shall not be committable while any tie affecting a qualifying place is unresolved. |
| FR-8.4 | Committing shall create the next stage's matches and participants, and shall be reversible until that stage starts. |
| FR-8.5 | Qualification shall be blocked until every match in the stage is `Completed`. |

### FR-9 Buzzer (optional module)

| ID | Requirement |
|---|---|
| FR-9.1 | The API shall start and every match shall complete with the buzzer module absent or disabled. |
| FR-9.2 | `GET /buzzer/capability` shall report availability so the client can choose its UI. |
| FR-9.3 | Buzz presses shall be persisted with device id, mapped team, button, elapsed milliseconds and rank. |
| FR-9.4 | Any number of devices shall be supported; the expected device count shall be configurable per program, defaulting to 3. |
| FR-9.5 | The operator shall always be able to override the hardware result. |
| FR-9.6 | A buzzer failure shall never block or fail an answer submission. |
| FR-9.7 | Buzz data shall never write scores directly; it only identifies who answers. |

### FR-10 Display

| ID | Requirement |
|---|---|
| FR-10.1 | Display endpoints shall be read-only and use a scoped display token. |
| FR-10.2 | Correct answers shall not be exposed to display clients until the answer is revealed. |
| FR-10.3 | Updates shall be pushed by SignalR, not polled. |
| FR-10.4 | On reconnect, a display client shall receive a full state snapshot. |

### FR-11 Audit

| ID | Requirement |
|---|---|
| FR-11.1 | Every create, update and delete shall record who and when. |
| FR-11.2 | Gameplay actions shall additionally write a `MatchEvent` row forming a replayable timeline. |
| FR-11.3 | Audit records shall be immutable and never deleted. |
| FR-11.4 | Deletes shall be soft deletes throughout. |

---

## 3.5 Business rules

### BR-1 Tournament structure

- **BR-1.1** A program has one or more stages, each with a unique order number.
- **BR-1.2** A stage has one or more matches.
- **BR-1.3** A match has at least 2 participants at start time.
- **BR-1.4** A team may appear in at most one match per stage.
- **BR-1.5** A stage cannot start until the previous stage is complete, unless it
  is explicitly marked as independent.
- **BR-1.6** Stage segment templates define the play order; the operator cannot
  skip a segment without recording a reason.
- **BR-1.7** A match may appear in a stage as a regular match or as a tie-break
  match. A tie-break match belongs to the stage whose tie it resolves.
- **BR-1.8** **No question format is mandatory.** What a match plays is defined
  solely by its stage's segment templates. A format with no segment template is
  never drawn, never scored and never validated against.
- **BR-1.9** A stage must have at least one segment. There is no other minimum.
- **BR-1.10** Three distinct kinds of "optional" exist and must not be confused:
  *not configured* (no segment template — the format is simply not used),
  *skippable* (`IsOptional = 1` — planned, but the operator may skip it on the
  night with a recorded reason), and *disabled program-wide*
  (`ProgramQuestionFormat.IsEnabled = 0` — hidden from the admin UI).

### BR-1a Order of question types

- **BR-1a.1** The order of question types in a match is `OrderIndex` on the
  stage's segment templates. It is data, editable at any time before the match
  starts.
- **BR-1a.2** Order indexes within a stage must be unique and contiguous from 1.
  Reordering rewrites them all in one transaction.
- **BR-1a.3** When a match is created, the segment order is copied from the
  template into `MatchSegment`. Changing the template afterwards does not alter
  matches already created — so a live tournament cannot be disturbed by an edit.
- **BR-1a.4** Under `SegmentOrderMode = RandomPerMatch`, the order is shuffled
  using the match's stored `RandomSeed`, so it is different per match yet
  reproducible and auditable.
- **BR-1a.5** Segments marked `IsOrderLocked` keep their position under
  shuffling and cannot be moved by a live reorder.
- **BR-1a.6** During a live match, only `Pending` segments may be reordered, and
  only when `Stage.AllowSegmentReorderDuringMatch = 1`. A segment that is `Open`,
  `Completed` or `Skipped` never moves.
- **BR-1a.7** Every reorder writes a `MatchEvent`, so the running order actually
  played can always be reconstructed.

### BR-2 Turn order

- **BR-2.1** Turn order is calculated only over `Active` participants.
- **BR-2.2** `nextTeamIndex = questionIndexInSegment % activeParticipantCount`.
- **BR-2.3** When a participant is removed, turn order is recompacted immediately
  and the current question index is preserved.
- **BR-2.4** Formats where any team may answer (Buzzer, Rapid Fire) ignore turn
  order.
- **BR-2.5** Passing follows turn order for the first attempt and then passes
  clockwise through the remaining active teams.

### BR-3 Scoring

- **BR-3.1** Points are looked up as
  `(ProgramId, Format, Outcome, ContextKey) → Points`, with fallback from
  segment → stage → program → system default.
- **BR-3.2** Recognised outcomes: `Correct`, `Incorrect`, `NoAnswer`, `Passed`,
  `PassedCorrect`, `Skipped`, `TimedOut`, `Penalty`, `Bonus`.
- **BR-3.3** A score event is never updated. A correction adds a reversing event
  plus a new event.
- **BR-3.4** Match score = sum of the team's score events in that match.
- **BR-3.5** Stage score = sum across the team's matches in that stage.
- **BR-3.6** Default values carried over from the current system are seeded but
  fully editable.

### BR-4 Qualification (the current 18-team example, now expressed as data)

```
Stage "League": 18 teams, 6 matches of 3
  Rule: WinnersPerMatch = 1        → 6 teams
        BestRemainingAcrossStage = 3 → 3 teams
        Total advancing = 9

Stage "Semi-Final": 9 teams, 3 matches of 3
  Rule: WinnersPerMatch = 1        → 3 teams
        BestRemainingAcrossStage = 0
        Total advancing = 3

Stage "Final": 3 teams, 1 match
  Rule: WinnersPerMatch = 1        → 1 champion
```

- **BR-4.1** "Winner" is the highest-scoring **active** participant of a match.
- **BR-4.2** "Best remaining" ranks all non-winning active teams across the whole
  stage by stage score.
- **BR-4.3** Disqualified teams cannot qualify by any route.
- **BR-4.4** A manual wildcard requires a reason and ProgramAdmin approval.
- **BR-4.5** Where teams are level on the qualification boundary, BR-5 applies
  before any place is awarded. Qualification cannot be committed while such a tie
  is unresolved.
- **BR-4.6** A tie that does not affect a qualifying place (for example 14th and
  15th when only 9 advance) is recorded but does not block the commit.

### BR-5 Tie-breaking

A tie is settled in two phases: **criteria first, then play.**

#### Phase 1 — non-playing criteria (ordered, configurable)

Applied in the order listed in `TieBreakRule.CriteriaJson`. The first criterion
that separates the teams wins, and **which criterion decided it is recorded**.

1. Higher total score
2. Fewer incorrect answers
3. More correct answers on higher-difficulty questions
4. Faster average buzz time, if buzzer data exists
5. Head-to-head result, if the teams met

Criteria are cheap, instant, and need no stage time. An organiser may reorder or
remove any of them.

#### Phase 2 — a played tie-break

If the criteria cannot separate the teams and `PlayTieBreakSegment = 1`:

- **BR-5.1** The system creates a **tie-break match** (`MatchKind = TieBreak`)
  containing only the tied teams.
- **BR-5.2** Its segments are built from the tie-break rule: format, question
  count, difficulty range and time limit.
- **BR-5.3** The format **defaults to MCQ** — fast, unambiguous, needing no media
  or buzzer hardware — and is configurable to any of the ten formats.
- **BR-5.4** Questions are drawn by the normal selection engine, honouring the
  repeat policy, so a tie-break never reuses a question the teams have seen.
- **BR-5.5** The match runs through the **normal match engine**. There is no
  separate tie-break gameplay code.
- **BR-5.6** Under `SuddenDeath = 1`, the segment closes as soon as one team
  leads, provided every tied team has faced the same number of questions.
- **BR-5.7** Tie-break points do **not** count toward the stage score unless
  `ScoreCountsTowardStage = 1`. The default keeps the League table honest — the
  tie-break decides the order, not the points.
- **BR-5.8** If teams are still level, another round is played, up to
  `MaxExtraRounds`.
- **BR-5.9** After the maximum rounds, `OnStillTied` applies: `ManualDecision`
  (ProgramAdmin, with a recorded reason), `CoinToss` (recorded), or `ShareTheSlot`
  (where the rules allow joint qualification).
- **BR-5.10** The whole episode is recorded as a `TieBreakEvent` with a
  `TieBreakParticipant` row per team, giving a defensible record of who was tied,
  on what, and how it ended.

#### Worked example — the League wildcard tie

```
League complete. 9 places: 6 match winners + 3 best remaining.
Ranking the non-winners by stage score:

  7th  Al-Noor          128  → wildcard 1
  8th  Millat           119  → wildcard 2
  9th  Madni (B)        110  ┐
 10th  Momin Girls      110  ┘ tied for the last wildcard place

Phase 1 — criteria:
  TotalScore ............. equal (110 = 110)
  FewerIncorrect ......... equal (4 = 4)
  MoreCorrectAtHighDiff .. equal (2 = 2)
  FasterAverageBuzzTime .. no buzzer data in the League stage
  HeadToHead ............. they never met (different matches)
  → still tied

Phase 2 — play:
  Tie-break match created: Madni (B) vs Momin Girls
  Format MCQ, 3 questions, difficulty 3-5, 20s each
  Result: Madni (B) 2 correct, Momin Girls 1 correct

  Madni (B) qualifies    → StageQualification.Reason = TieBreakWin
  Momin Girls eliminated → recorded on the same TieBreakEvent

Neither team's stage score changes (ScoreCountsTowardStage = 0).
```

### BR-6 Question selection

- **BR-6.1** A question is drawn only from the pool defined by the stage's
  selection rule.
- **BR-6.2** Default repeat policy: a question is never repeated within a
  program.
- **BR-6.3** Difficulty mix is respected as closely as the pool allows.
- **BR-6.4** All of a match's questions are reserved at match start; a reserved
  question is unavailable to other matches.
- **BR-6.5** Abandoning a match releases its unused reservations.
- **BR-6.6** If the pool cannot satisfy the rule, the system widens difficulty
  by one level, then drops the topic filter, then returns a precise error stating
  the format, difficulty and count that are missing.
- **BR-6.7** The RNG seed is stored on the match so the draw is reproducible.

### BR-7 Participant removal

- **BR-7.1** Removal requires a reason and a ProgramAdmin.
- **BR-7.2** A match continues while at least 2 active participants remain.
- **BR-7.3** With only 1 active participant left, the match completes immediately
  and that team is the winner.
- **BR-7.4** The removed team's score events are retained and marked excluded.
- **BR-7.5** Removal is recorded as a `MatchEvent` and pushed to all screens.
- **BR-7.6** Substitution (removing one team and adding another) is permitted
  only before the match starts.

### BR-8 Live-match integrity

- **BR-8.1** Only one segment may be open per match at a time.
- **BR-8.2** Only one question may be `Active` per segment at a time.
- **BR-8.3** An answer can only be recorded against the currently active question.
- **BR-8.4** Every team's answer must be recorded before the next question is
  served, unless the format allows only one answerer.
- **BR-8.5** A completed match cannot be reopened; a correction is a manual score
  adjustment.

---

## 3.6 Tournament / round / match flow

```
1. SETUP  (ProgramAdmin)
   Create program → add teams → load question bank
   → define stages → define segment templates → define scoring rules
   → define selection rules → define qualification rules
   → validate configuration                     ── program becomes "Configured"

2. STAGE SEEDING  (ProgramAdmin)
   Stage 1: assign teams to matches (manual / random / seeded)
   Later stages: created automatically by committing qualification

3. MATCH PREPARATION  (Operator)
   Open match → confirm participants and seats → assign buzz devices (optional)
   → run pre-flight check (questions available? buzzer healthy? displays connected?)
   ── match becomes "Ready"

4. LIVE MATCH  (Operator)
   START MATCH
     ├─ questions reserved, RNG seed stored, SignalR notifies all screens
     │
     └─ FOR EACH SEGMENT in the template:
          OPEN SEGMENT
            └─ FOR EACH QUESTION in the segment:
                 ├─ determine the answering team (active participants only)
                 ├─ [if Buzzer format] arm devices, collect presses, rank
                 ├─ SERVE QUESTION      → pushed to display; timer starts
                 ├─ RECORD ANSWER       → outcome + points, atomic
                 │    (or SKIP, or PASS to the next team)
                 ├─ REVEAL ANSWER       → correct answer pushed to display
                 └─ scores updated and pushed
          CLOSE SEGMENT
     │
     ├─ at any point: PAUSE / RESUME / UNDO LAST / DISQUALIFY PARTICIPANT
     │
     END MATCH
       ├─ compute final match standings
       ├─ apply tie-break criteria if needed
       └─ match becomes "Completed"

5. STAGE COMPLETION  (ProgramAdmin)
   All matches complete → run qualification preview
     │
     ├─ no boundary tie ──────────────────────────────► COMMIT
     │
     └─ boundary tie detected (e.g. 9th and 10th level on 110)
          ├─ Phase 1: apply ordered criteria
          │     └─ separated? → record the deciding criterion ──► COMMIT
          │
          └─ Phase 2: still tied
                ├─ create a TIE-BREAK MATCH with only the tied teams
                │     (MCQ by default, format configurable)
                ├─ operator runs it on the normal console
                ├─ result recorded on the TieBreakEvent
                └─ still level after MaxExtraRounds?
                      → manual decision / coin toss / share the slot
                                                       ──► COMMIT
   COMMIT → next stage's matches and participants are created

6. PROGRAM COMPLETION
   Final stage complete → champion declared
   → reports generated → program archived
```

---

## 3.7 Admin capabilities

| Area | Capabilities |
|---|---|
| **Program** | Create, clone, configure, set branding, set language, validate, start, archive |
| **Users** | Invite, assign roles, scope to programs, deactivate, reset password |
| **Teams** | Add, edit, import, upload images, change status, view history |
| **Questions** | Add, edit, version, import, tag, set difficulty, upload media, retire, view usage |
| **Structure** | Choose which question formats this program uses at all; manage stages, segment templates, formats and question counts; **reorder question types by drag and drop**; set the stage order mode (fixed / random per match / operator choice); lock a segment's position |
| **Rules** | Scoring rules, selection rules, qualification rules, **tie-break rules (criteria order, tie-break format defaulting to MCQ, question count, difficulty, sudden death, fallback)** |
| **Matches** | Create, seed, assign participants and seats, **override the segment order for this match**, reorder, reset |
| **Live control** | Start, pause, resume, skip, undo, override, disqualify, **reorder pending segments (where the stage allows)**, end, abandon |
| **Scores** | View, adjust manually with reason, approve adjustments, recalculate |
| **Qualification** | Preview, adjust wildcards, **see and resolve boundary ties, launch a tie-break match, record a manual decision**, commit, roll back before the stage starts |
| **Buzzer** | Configure provider, map devices to teams, health check, test, disable |
| **Recovery** | Snapshot a match, restore a match, replay the event timeline |
| **Reporting** | Standings, match report, team report, question usage, audit log, exports |

---

## 3.8 Non-functional requirements

### Performance

| ID | Requirement |
|---|---|
| NFR-P1 | 95th percentile response time for gameplay endpoints under 200 ms. |
| NFR-P2 | `GET /matches/{id}/live/state` under 150 ms. |
| NFR-P3 | SignalR update delivered to all display clients within 500 ms. |
| NFR-P4 | Recording an answer must not exceed 3 database round trips. |
| NFR-P5 | Standings must be read from pre-aggregated data, never recomputed from raw answers on request. |

### Availability and reliability

| ID | Requirement |
|---|---|
| NFR-A1 | Zero data loss on an unexpected restart mid-match. |
| NFR-A2 | Recovery to the exact prior state within 30 seconds of restart. |
| NFR-A3 | A buzzer hardware failure must not stop a match. |
| NFR-A4 | A display client disconnection must not affect gameplay. |
| NFR-A5 | Automated database backup before each match starts. |

### Security

| ID | Requirement |
|---|---|
| NFR-S1 | All traffic over HTTPS/TLS 1.2 or higher. |
| NFR-S2 | Passwords hashed with the ASP.NET Core Identity default (PBKDF2 or better). |
| NFR-S3 | Access tokens expire in 15 minutes; refresh tokens are rotated on use. |
| NFR-S4 | No endpoint is anonymous except `/auth/login` and `/health`. |
| NFR-S5 | Cross-program data access is impossible even with a forged request id. |
| NFR-S6 | All uploads validated by extension, magic bytes and size, and stored outside the web root. |
| NFR-S7 | No secret is ever committed to source control. |
| NFR-S8 | Login is rate-limited to 5 attempts per minute per IP. |

### Maintainability

| ID | Requirement |
|---|---|
| NFR-M1 | No business rule may be a hardcoded constant. |
| NFR-M2 | Unit test coverage above 80% in Domain and Application. |
| NFR-M3 | Every endpoint documented in OpenAPI with examples. |
| NFR-M4 | Layer dependencies enforced by automated architecture tests. |
| NFR-M5 | Adding a new question format must require only a new format table, a lookup row and a format handler — no change to any existing table, and no change to the match engine, scoring or selection code. |

### Usability (API-level)

| ID | Requirement |
|---|---|
| NFR-U1 | Errors follow RFC 9457 with a machine-readable code and a human message. |
| NFR-U2 | Validation errors list every failing field, not just the first. |
| NFR-U3 | Every list endpoint supports paging, sorting and filtering consistently. |
| NFR-U4 | Dates are always UTC ISO-8601 with a `Utc` suffix in the property name. |

### Scalability

| ID | Requirement |
|---|---|
| NFR-SC1 | Support at least 50 programs, 1,000 teams and 100,000 questions without redesign. |
| NFR-SC2 | Support 100 concurrent display clients on one match. |
| NFR-SC3 | The API must be stateless so it can run behind a load balancer. |

### Observability

| ID | Requirement |
|---|---|
| NFR-O1 | Structured logging (Serilog) with a correlation id per request. |
| NFR-O2 | `/health/live` and `/health/ready` endpoints, including a buzzer health probe when enabled. |
| NFR-O3 | Gameplay actions logged at Information level with match and user context. |
| NFR-O4 | Metrics for request duration, error rate and SignalR connection count. |

---

## 3.9 Future extensibility

Explicitly designed for, not built yet:

| Idea | Why it is already possible |
|---|---|
| New question formats | Add one handler class, one lookup row and one new table; nothing existing is altered |
| Any tournament shape (knockout, league table, Swiss) | Stages and rules are data |
| Online qualifying rounds | Same question bank, a new "self-serve" match type |
| Audience voting | New module consuming the same match state |
| Live-stream score overlay | Another read-only Display API consumer |
| Mobile scorer app | Same REST API |
| Multi-language UI and content | `Language` already on questions; add resource files |
| AI-assisted question generation | Writes into the same question bank |
| Cross-year analytics | All programs already live in one database |
| Sponsor / advertisement slots | Add to the segment template as a non-scoring segment type |
