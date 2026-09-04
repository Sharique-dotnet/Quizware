# QuizApp — New System Design

Design documents for rebuilding `QuizApp-9AMM` (and absorbing `QuickBuzz`) as an
**ASP.NET Core Web API on .NET 10** with **SQL Server**, ready for an
**Angular 22** front end later.

All conclusions are based on reading the actual source of both existing projects.
Confirmed findings are separated from recommendations throughout.

### A note on names

The new system and the old one share the name **QuizApp**, so these documents use
a consistent convention:

| Name | Means |
|---|---|
| **QuizApp** | The **new** system described in these documents — solution `QuizApp.sln`, projects `QuizApp.Domain`, `QuizApp.Application`, `QuizApp.Infrastructure`, `QuizApp.Api` |
| **QuizApp-9AMM** | The **existing** ASP.NET MVC 4 system being replaced |
| **QuickBuzz** | The existing buzzer application, absorbed as the optional `QuizApp.Modules.Buzzer` |

Wherever a finding describes current behaviour, it says **QuizApp-9AMM**.

---

## Read in this order

| # | Document | What it answers |
|---|---|---|
| 1 | [Analysis & Findings](01-Analysis-Findings.md) | What the two systems do today, what is broken, what is dead code, what to keep and what to drop |
| 2 | [Architecture Proposal](02-Architecture-Proposal.md) | How the new system is structured; modules; multi-tenancy; how QuickBuzz becomes optional |
| 3 | [PRD — API](03-PRD-API.md) | Goals, roles, features, functional requirements, business rules, tournament flow, non-functional requirements |
| 4 | [Database Schema](04-Database-Schema.md) | Every table, column, key, constraint and index, with the purpose of each |
| 5 | [API Design](05-API-Design.md) | Controllers, endpoints, request/response models, services, workflows, validation and errors |
| 6 | [Development Roadmap](06-Development-Roadmap.md) | Exactly what to build, in what order, and why |

Earlier line-by-line audits of the existing code are in the parent folder:
`../QuizApp-9AMM-Technical-Analysis.md` and `../QuickBuzz-Technical-Analysis.md`.

---

## The eight requirements, and how they are solved

| Your requirement | The fix |
|---|---|
| **1. One database for many programs** | A `Program` table is the tenant root. Every table carries `ProgramId`, enforced by an EF Core global query filter driven by the JWT claim. Creating next year's event is one `INSERT` plus a config clone — no new database, no redeployment. |
| **2. Dynamic teams; continue after a disqualification** | `MatchParticipant` stores `SeatNumber` (fixed) and `TurnOrder` (recalculated). Turn selection reads active participants only: `active[index % active.Count]`. Disqualifying a team recompacts the order and the match carries on. **No fake answers, ever.** This replaces `QuestionNumber % 3`, which is hardcoded in 30+ places today. |
| **3. Configurable tournament structure** | `Stage`, `StageSegmentTemplate`, `QualificationRule` and `ScoringRule` are all data. The current 18 → 9 → 3 → 1 tournament becomes ~15 configuration rows. Any number of stages, matches, teams per match and formats. |
| **4. Dynamic question selection** | Questions have no match, round or question-number columns. Each format keeps **its own table** for its own fields, over a shared `Question` base carrying `Format`, `DifficultyLevel` (1–5), `Topic`, `Tags` and `Language`. `QuestionSelectionRule` drives a seeded weighted random draw with difficulty balancing, repeat prevention, topic spread and a fallback ladder — and the draw reads the base table only, so per-format tables cost it nothing. Questions are reserved at match start so a crash cannot change the order. |
| **5. Optional QuickBuzz** | An `IBuzzerProvider` port with three adapters — `Null` (default), `HttpAgent` (recommended) and `Serial`. Selected by configuration. **The entire buzzer project can be deleted and the solution still builds and every match still completes.** The buzzer only identifies who answers first; it never writes scores. |
| **6. Configurable order of question types** | The running order is `StageSegmentTemplate.OrderIndex` — one column, reordered by sending the full ordered list (what a drag-and-drop list produces). Overridable per match, optionally shuffled per match from the stored seed, and reorderable live for segments still pending. Today this order is a hardcoded `window.location.href` in each of 108 views. |
| **7. Tie-break for the wildcard boundary** | Two phases. First, ordered configurable criteria (total score, fewer incorrect, harder questions, faster buzz, head-to-head) — instant, and the deciding criterion is recorded. If still level, the system creates a **real tie-break match** with only the tied teams, **MCQ by default and any format if configured**, run by the same match engine. Result recorded on a `TieBreakEvent`; tie-break points do not move the stage table unless you say so. |
| **8. Question types are optional** | A stage plays exactly the segments you configure. Not doing Passing this year? Do not create a Passing segment — no questions to author, no rules to set, no code to change. Readiness checks and coverage reports only ever look at formats you actually use. A program can also switch formats off entirely so they vanish from the admin screens. |

---

## The headline numbers

| | Today | New design |
|---|---|---|
| Databases per year | 1 new each time | 1 forever |
| Question tables | 10 independent, repeating the same 15 columns | 10 format tables + **1 shared base** (Table-Per-Type) |
| Option tables | 7 | **1**, plus 2 genuinely different item tables |
| Answer tables | 9 | **1** |
| Gameplay controllers | 3 (5,728 lines) | **1 engine** |
| Duplicated C# | ~75–80% | near zero |
| DB round trips per answer | up to 12 | **≤ 3** |
| Hardcoded business rules | 8 major ones | **0** |
| Order of question types | A redirect hardcoded in 108 views | One `OrderIndex` column, editable at 3 levels |
| Tie on the last wildcard place | Settled by database row order, recorded nowhere | Configurable criteria, then a real tie-break match, fully recorded |
| Question types per round | Fixed by which `#region` blocks exist in that round's controller | Opt-in: configure the ones you want, omit the rest |
| Teams per match | fixed at 3 | any number, changeable mid-match |
| Authentication | none | JWT + 7 roles + program scoping |
| Audit trail | none | every write, plus a replayable match timeline |
| Undo | impossible | built in (event-sourced scoring) |
| Crash recovery | start over | resumes at the exact question |
| Tests | none | 80%+ on domain and application |

---

## What I found that you may not have known

- **QuickBuzz is not connected to QuizApp-9AMM at all.** The only reference is a PNG
  image file. A human currently bridges the two systems by eye.
- **The qualification rule exists nowhere in code.** Round 1 → Round 2
  advancement is done entirely by a person reading a screen.
- **The `Matches` table is not a match.** It is a slot number 1–6 that every round
  re-uses, which is why every query needs `RoundsId` *and* `MatchesId`.
- **`PostTieBreakerAnswer` never records who won.** It only marks the question as
  used.
- **`QuizCategory`, `Rounds_QuizCategory`, the `RapidFire` questions table and
  `spGetTotalScore` are completely dead** — never referenced anywhere.
- **The order of question types is a hardcoded redirect.**
  `MatchOneMCQ.cshtml:153` literally jumps to `MatchOneAudioVisual`. Changing the
  running order means editing views and redeploying.
- **`TieBreaker` is not connected to qualification at all.** No controller
  references it, and `LeagueRoundScore` ranks equal-scoring teams by their
  database row order — so a tie for the last wildcard place is currently decided
  by accident.
- **`Passing_Answers.PassingNumber` is `NOT NULL` but never set** by any code
  path.
- **Four answer tables have no foreign key to their question**, so those answers
  cannot be traced back to what was asked.
- **There is no authentication anywhere.** Not one `[Authorize]` attribute exists.
- **`spReset` uses `TRUNCATE TABLE`** — the reset is unrecoverable.

---

## Suggested next step

Work through **Phase 0** of the [roadmap](06-Development-Roadmap.md): confirm the
PRD with the organisers and answer the 16 open questions in section 6.5. Then
start Phase 1 with the domain model — specifically the turn-order calculator,
which is the single class that fixes the disqualification problem.
