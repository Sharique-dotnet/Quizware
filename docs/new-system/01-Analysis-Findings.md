# 1. Analysis of the Existing Systems

This is the starting point. Everything here comes from reading the actual code of
`QuizApp-9AMM` and `QuickBuzz`. Where I give an opinion, it is clearly marked as
**Recommendation**. Everything else is a **confirmed finding** with the file and
line where I found it.

---

## 1.1 What the two systems are

### QuizApp-9AMM (the main quiz software)

| Item | Value |
|---|---|
| Technology | ASP.NET **MVC 4** on **.NET Framework 4.x** (very old) |
| Data access | **Entity Framework 5** with an **EDMX** designer file (database-first) |
| Database | SQL Server LocalDB, database `BRFQuizDb_9AMM_Final` |
| UI | Razor views + jQuery 3.7 + Bootstrap 3 (108 `.cshtml` files) |
| Tables | 32 tables, 3 stored procedures |
| Controllers | 16 controllers, about 9,100 lines of C# |
| Purpose | Runs a live inter-school Urdu quiz competition on stage |

### QuickBuzz (the buzzer system)

| Item | Value |
|---|---|
| Technology | ASP.NET Core MVC on **.NET 10** (modern) |
| Purpose | Talks to physical buzzer hardware over a **serial / RS485 port** |
| Hardware | 3 devices, each with 5 buttons (A–E), 24-byte frames, 38400 baud |
| Database | **None.** It stores nothing. |
| Size | 1 service (`SerialService`), 1 parser, 1 API controller, 4 views |

---

## 1.2 The most important finding

> **QuickBuzz and QuizApp-9AMM are not connected in any way.**

I searched the whole QuizApp-9AMM codebase for any reference to QuickBuzz. The only
match is a **picture file**:

```
Views/SecondRound/MatchOneMCQ.cshtml:64
<img src="~/Content/BRFSoftware/images/Rounds/QuickBuzz.png">
```

There is no HTTP call, no shared database, no shared code, no shared config.

**What this means in practice:** during the live event an operator watches the
QuickBuzz screen in one window, sees which team pressed first, and then
**manually clicks** the result in QuizApp-9AMM in another window.

**Why this is good news:** because nothing is coupled today, making QuickBuzz an
*optional plug-in* in the new system is not a migration problem. It is a clean
start.

---

## 1.3 How the tournament actually works today (confirmed)

18 teams. 3 rounds.

| Round | Matches | Teams per match | Question formats used |
|---|---|---|---|
| Round 1 (League) | 6 | 3 | MCQ, Audio-Visual, Sequence, Buzzer |
| Round 2 (Semi-Final) | 3 | 3 | MCQ, Audio-Visual, Rapid Fire, Passing, Card, Choice |
| Round 3 (Final) | 1 | 3 | Sequence, Audio-Visual, Visual Rapid Fire, Rapid Fire, Passing, Card, Buzzer, Choice |

Confirmed from the seed file `9AMM_AfterProgram.sql`:

- `Rounds` has exactly 3 rows, named "1", "2", "3"
- `Matches` has exactly 6 rows, named "1" to "6"
- `SchoolsTeam` has exactly 18 rows

### A key structural problem

The `Matches` table is **not a real match**. It is only a *slot number* (1 to 6)
that every round re-uses:

- Round 1 uses slots 1–6
- Round 2 uses slots 1–3
- Round 3 uses slot 1

So "Match 1" means three different real-world matches depending on the round.
Every single query has to carry **both** `RoundsId` and `MatchesId` together.
A real match entity does not exist anywhere in the system.

---

## 1.4 Business rules that are hardcoded

All of these live in C# code, not in the database or a settings file.

### (a) Teams per match is fixed at 3

Found in every gameplay controller, more than 30 times:

```csharp
// FirstRoundController.cs:42
activeTeam = MCQs.QuestionNumber % 3;
```

Turn order is decided by `QuestionNumber % 3`. This is exactly why the system
**cannot continue a match after a team is removed** — the modulo keeps pointing
at the missing team, so the operator has to enter a fake wrong answer just to
move the game forward. This is the problem you described.

The response object also has fixed properties: `SchoolsTeamIdOne`,
`TeamNameTwo`, `TeamScoreThree`. There is no room for a 4th team, and no way to
drop to 2.

### (b) Matches per round is fixed

```csharp
// MatchesController.cs:136
switch (matchesVM.RoundsId) {
    case 1: maxMatchesAllowed = 6; break;
    case 2: maxMatchesAllowed = 3; break;
    case 3: maxMatchesAllowed = 1; break;
}
```

The same rule is copy-pasted again in the Excel upload method at line 63.

### (c) Team count is fixed at 18

```csharp
// HomeController.cs
const int maxTeams = 18;
```

### (d) Every scoring value is a C# constant

`SupportedFile/Contants.cs` (note the spelling mistake in the class name):

| Format | Correct | Wrong | Not answered |
|---|---|---|---|
| MCQ | +10 | 0 | – |
| Audio-Visual | +10 | 0 | – |
| Sequence | +20 | 0 | – |
| Buzzer | +20 | −15 | −15 |
| Rapid Fire | +5 | −5 | – |
| Passing (direct) | +15 | −10 | – |
| Passing (after a pass) | +10 | −10 | – |
| Card | +5 | −5 | – |
| Visual Rapid Fire | +5 | −5 | – |
| Choice | +15 | −15 | – |

Also hardcoded: `Choice_SemiFinal_Topic_Limit = 6` and
`Choice_Final_Topic_Limit = 9`.

Changing any score means editing C#, rebuilding and redeploying.

### (e) Qualification rules do not exist in code at all

There is **no code** that decides who moves from Round 1 to Round 2. The
`LeagueRoundScore` action only sorts teams by score and shows a list. A human
reads that screen and manually enters the Round 2 matches on the `SetMatches`
page. The rule "winners plus the next highest scorers" exists only in people's
heads.

### (f) Questions are pre-assigned to a match and a fixed position

Every question table has `RoundsId`, `MatchesId` and `QuestionNumber`, and
questions are served in strict order:

```csharp
db.MCQs.Where(m => m.MatchesId == x && m.RoundsId == y && m.StatusId == 0)
       .OrderBy(m => m.QuestionNumber).Take(1).FirstOrDefault();
```

Question sets must therefore be prepared and assigned by hand before the event.
There is no difficulty level, no topic, no category, and no randomisation.

### (g) Which question types a round plays is fixed in code

Each round's controller hardcodes the formats it supports. `FirstRoundController`
implements MCQ, Audio-Visual, Sequence and Buzzer — and nothing else.
`SecondRoundController` implements a different six. There is no configuration
that says "Round 1 plays these formats"; the answer is which `#region` blocks
somebody wrote.

**Consequence:** removing Passing from a year's event means the Passing code,
views and tables are still present and must be worked around; adding Rapid Fire
to Round 1 means copying ~350 lines from `SecondRoundController` into
`FirstRoundController` plus a new set of views.

### (h) The order of question types is hardcoded in the Razor views

The sequence of segments in a match — MCQ, then Audio-Visual, then Sequence, then
Buzzer — is not stored anywhere. It is a **literal redirect written into each
view**:

```javascript
// Views/FirstRound/MatchOneMCQ.cshtml:153
window.location.href = "@Url.Action("MatchOneAudioVisual", "FirstRound")";
```

Each screen hardcodes the name of the screen that follows it. The same jump is
repeated at line 629 of the same file, and again in every one of the 108 views.

**Consequence:** to swap Buzzer and Sequence, or to insert Rapid Fire into
Round 1, someone must edit the redirect in every affected view, for every match,
in every round — then rebuild and redeploy. There is no way to change the order
from a screen, and no way to make Round 2 Match 1 differ from Round 2 Match 2.

### (i) There is no tie-breaker at the qualification boundary

The `TieBreaker` table and its screens exist, but they are **completely
disconnected from qualification**. I searched `ScoreController`,
`MatchesController` and `LandingController` — none of them reference
`TieBreaker` at all. `MatchTieBreaker.cshtml` is a standalone screen an operator
navigates to manually.

More seriously, as noted in section 1.9, `PostTieBreakerAnswer` only marks the
question as used — **it never records which team won**. So even when a
tie-breaker is played, the result exists only in the audience's memory.

`LeagueRoundScore` sorts teams by score with
`.OrderByDescending(t => t.Score)` and assigns ranks by list position. Two teams
on equal points therefore receive **different ranks purely by their database
order** — an arbitrary tie-break that nobody decided and that is not recorded.
With 3 wildcard places decided on score alone, a tie on the 9th/10th boundary is
resolved today by accident.

---

## 1.5 Duplication (the biggest maintenance cost)

**Nine near-identical question table families**, each with its own options and
answers tables:

```
MCQ             / MCQ_Option        / MCQ_Answers
Buzzer          / Buzzer_Option     / Buzzer_Answers
Card            / Card_Option       / Card_Answers
Choice          / Choice_Option     / Choice_Answers
Passing         / Passing_Option    / Passing_Answers
Sequence        / Sequence_Option   / Sequence_Answers
Audio_Visual                        / Audio_Visual_Answers
RapidFire                           / RapidFire_Answers
VisualRapidFire                     / VisualRapidFire_Answers
TieBreaker      / TieBreakerOption   (no answers table at all)
```

They all have the same shape:
`Id, RoundsId, MatchesId, Question, QuestionNumber, StatusId`.

**Three near-identical gameplay controllers:**

| Controller | Lines |
|---|---|
| `FirstRoundController` | 1,324 |
| `SecondRoundController` | 1,930 |
| `FinalRoundController` | 2,474 |

The Audio-Visual code appears in all three, almost character for character.
Passing appears twice. Choice appears twice. Buzzer appears twice.

**The "CALCULATE MARKS AND TEAMS" block is copy-pasted more than 40 times.** It
is the same ~50 lines each time, and each copy runs the `spGetTotalScoreNew`
stored procedure **once per team** — so 3 database round trips on every click,
each with 8 subqueries inside.

**Nine CRUD controllers** (`MCQController`, `BuzzerController`, `CardController`,
`ChoiceController`, `PassingController`, `SequenceController`,
`AudioVisualController`, `VisualRapidFireController`, `TieBreakerController`) are
the same five methods with different type names: `Index`, `GetXList`,
`UploadExcel`, `AddXQuestions`, `Edit`.

**Estimate: roughly 75–80% of the C# in this project is duplicated code.**

---

## 1.6 Unused and orphaned items (confirmed by search)

| Item | Status |
|---|---|
| `QuizCategory` table + model | **Never referenced by any controller.** Dead. |
| `Rounds_QuizCategory` table + model | **Never referenced.** Dead. |
| `RapidFire` questions table | **Never read.** Only `RapidFire_Answers` is written — rapid-fire questions are read aloud by the host from paper. |
| `spGetTotalScore` stored procedure | Replaced by `spGetTotalScoreNew`. Never called. |
| `spGetTotalScore_Result` model | Never used. |
| `Passing_Answers.PassingNumber` | Column is `NOT NULL` but **never set** in code. |
| `Sequence_Option.WrongSequenceNo` | Stored but never used in scoring. |
| `OnTieBreakerLoad` | Entire method is commented out. |
| `Card_Answers`, `RapidFire_Answers`, `VisualRapidFire_Answers`, `Choice_Answers` | Hold `MatchId` / `RoundId` as loose ints with **no foreign key**, so an answer cannot be traced back to the question that produced it. |
| `TieBreakerOption.TieBreakerId` | Declared `NOT NULL` but has **no foreign key constraint**. |

---

## 1.7 Other confirmed defects

1. **No authentication or authorisation at all.** No `[Authorize]` anywhere, no
   login page, no user table. Anyone who can reach the URL can post answers and
   change scores.
2. **`[ValidateInput(false)]`** on the gameplay controllers turns off the
   built-in XSS protection.
3. **`ValidateAntiForgeryToken` is missing** on every POST.
4. **No audit fields anywhere.** No `CreatedAt`, `CreatedBy`, `UpdatedAt`. If a
   score is wrong there is no way to find out who changed it or when.
5. **No soft delete and no undo.** `spReset` uses `TRUNCATE TABLE`, wiping every
   answer permanently with no backup.
6. **The DbContext is a field, not injected** (`BRFQuizEntities db = new
   BRFQuizEntities();`) and is never disposed.
7. **Null-reference risk everywhere.** `GetMatchOneMultipleChoice` does
   `mcq.MCQs` straight after `FirstOrDefault()` with no null check — if the
   questions run out mid-show, the screen crashes.
8. **`LandingController` crashes with fewer than 18 teams** — it does
   `schoolTeamMatches[17]` with no bounds check.
9. **`Buzzer_Answers.Answer` is `INT`** (1 = right, 2 = wrong, 3 = no answer)
   while every other answers table uses `BIT`. Inconsistent.
10. **Transactions are unreliable.** Several methods call `SaveChanges()` more
    than once inside a `TransactionScope` and only sometimes call
    `tS.Complete()` — so a partial write can be committed.
11. **Scores are recalculated from scratch on every request** by a stored
    procedure with 8 subqueries, run once per team, on every click.
12. **A new database is created every year**, so past results cannot be
    compared and the code must be redeployed per event.

---

## 1.8 QuickBuzz findings

**What is good and worth keeping:**

- The serial protocol handling is solid: frame alignment on the `0x02` start
  byte, device-ID validation, end-byte validation (`0xFD` / `0xFE`), 3 retries,
  and the required 150 ms RS485 bus delay.
- The ranking logic is correct: lowest press time wins, `0` means "no press" and
  sorts last.
- It already targets .NET 10, so the code carries over easily.

**Problems:**

- `SerialService` is a **singleton with one global lock**, and `PollAll()` takes
  up to ~450 ms plus retries. Every caller blocks behind it.
- **Device IDs are hardcoded:** `int[] ids = { 1, 2, 3 };` — again fixed at 3
  teams.
- **Baud rate, frame length and timeouts are hardcoded** constants.
- `appsettings.json` contains **no application settings at all**.
- **Nothing is persisted.** Buzz times vanish on page refresh, so there is no
  evidence if a team disputes a result.
- The `/api/device/buzzer` and `/api/device/quickbuzz` endpoints are
  near-duplicates.
- Uses `Console.WriteLine` instead of `ILogger`.
- **No mapping from device ID to team.** A human has to remember that device 2
  is the team on the left.
- The 30-second timer is browser JavaScript only and is unrelated to the
  hardware timestamps.

---

## 1.9 What is genuinely missing (functionality)

1. **Users, login and roles.**
2. **Automatic qualification** from one round to the next.
3. **Team disqualification or substitution** during a live match.
4. **Undo or correct a mistake.** Once an answer is posted it cannot be
   reversed.
5. **A real tie-breaker flow.** Tie-breaker questions can be added, but
   `PostTieBreakerAnswer` only marks the question as used — **it never records
   who won**. Nothing connects it to qualification, so a tie for the last
   wildcard place is currently settled by database row order (section 1.4i).
6. **Configurable segment order.** Which question type follows which is a
   hardcoded redirect in each view (section 1.4h).
7. **Question bank management:** difficulty, topic, tags, language, reuse
   history, duplicate detection.
8. **Match state.** There is no `NotStarted / InProgress / Paused / Completed`.
   The system infers "finished" from `StatusId != 0` on questions.
9. **Live display push.** Screens poll with jQuery AJAX; there is no SignalR or
   WebSocket.
10. **Server-side timers.** All timing is browser-side only, so screens disagree.
11. **Reports and exports:** final standings, per-team report, event summary.
12. **Media management.** Audio and visual files are plain strings pointing at a
    folder path — no upload validation, no size limit, no cleanup.
13. **Multi-language question content** (the content is Urdu, the UI is
    English).
14. **Any tests at all.** There is no test project in the solution.

---

## 1.10 What to carry forward, and what to drop

| Carry forward | Why |
|---|---|
| The 10 question formats and their mechanics | Proven on stage; the audience knows them |
| The scoring values | Tuned from real events — but move them into the database |
| Excel bulk import | Genuinely useful for organisers |
| Team score / selection images | Needed for the stage display |
| QuickBuzz serial protocol code | It works and is hardware-correct |
| QuickBuzz ranking algorithm | Correct |

| Drop | Why |
|---|---|
| Ten *independent* question tables that repeat the same 15 columns | Keep a table per format for the fields that genuinely differ, but move the shared identity/lifecycle columns into one `Question` base table (Table-Per-Type) |
| Six byte-identical `*_Option` tables | Replace with one `QuestionOption` table — they had no field differences at all |
| One controller per round | Replace with one gameplay engine driven by configuration |
| `QuestionNumber % 3` turn logic | Replace with an explicit turn-order table |
| Segment order as a `window.location.href` in each view | Replace with `StageSegmentTemplate.OrderIndex`, reorderable from a screen and overridable per match |
| A fixed set of question formats baked into each round's controller | Replace with opt-in segments — a format is played only if configured, so dropping Passing for a year needs no code and no questions |
| Tie on the wildcard boundary settled by database row order | Replace with configurable ordered tie-break criteria, then a real tie-break match whose result is recorded |
| `Matches` as a shared slot lookup | Replace with a real `Match` entity that belongs to a stage |
| `StatusId` as an untyped `int` | Replace with proper enums and lifecycle states |
| `QuizCategory`, `Rounds_QuizCategory`, `RapidFire` questions, `spGetTotalScore` | Dead code |
| `TRUNCATE`-based reset | Replace with soft delete plus a proper "reset" action that is itself audited |
| A new database every year | Replace with one database plus a `Program` (tenant) column |
| EDMX / EF5 / .NET Framework | Replace with EF Core 10 on .NET 10 |
| Razor + jQuery UI | Replace with a REST API plus Angular 22 |

---

## 1.11 Improvements I recommend (beyond your list)

These are **Recommendations**, not findings.

1. **Event-sourced scoring.** Store every scoring action as an immutable
   `ScoreEvent` row; the current score is the sum of its events. This gives you
   free undo, a full audit trail and instant dispute resolution.
2. **Idempotency keys on every gameplay POST.** On a live stage, a double-click
   or a flaky Wi-Fi retry must never award points twice.
3. **Offline-tolerant operator console.** Venue Wi-Fi fails eventually. Queue
   actions locally and sync when the connection returns.
4. **A rehearsal / dry-run mode** that plays a whole tournament with fake data so
   the crew can practise before the real event.
5. **Question reuse history across programs**, so the same question is not asked
   to the same school two years running.
6. **Server-authoritative timers** pushed over SignalR so every screen agrees.
7. **A separate read-only Display API** with its own token. Projector machines
   should never be able to write anything.
8. **Snapshot and restore of a whole match**, so a mid-show crash is recoverable
   in seconds.
9. **Per-program branding** (logo, colours, fonts) stored as data.
10. **Excel import with a validation report** shown before committing, instead of
    silently skipping bad rows the way the current code does.
