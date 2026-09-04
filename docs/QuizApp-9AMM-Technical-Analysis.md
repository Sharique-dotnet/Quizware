# QuizApp‑9AMM — Comprehensive Technical Analysis & Audit Report

**Audited artifact:** `C:\Sharique\Projects\Personal\QuizApp\QuizApp-9AMM`
**Audit date:** 2026‑09‑02
**Auditor role:** Senior Full‑Stack Engineer / Software Architect / Technical Auditor
**Method:** Full source read of every `.cs`, `.cshtml`, `.config`, `.sql`, `.csproj`/`.sln` in the solution (vendor `packages/`, `Scripts/tinymce`, bootstrap/font‑awesome distributions excluded). Every "unused" claim was verified by whole‑codebase reference counting, not by filename inspection.

> **Scope note.** A sibling folder `C:\Sharique\Projects\Personal\QuizApp\QuickBuzz` contains a separate **.NET 10** project (`QuickBuzz.Web`, references `System.IO.Ports` → physical buzzer hardware). It is **not** part of `QuizApp.sln` and is out of scope for this report, but it is clearly the intended successor/companion for the buzzer hardware integration that `QuizApp-9AMM` currently lacks. See §3.

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Current Architecture](#2-current-architecture)
3. [What Is Missing](#3-what-is-missing)
4. [What Should Be Improved](#4-what-should-be-improved)
5. [Unused / Unused‑But‑Created Models](#5-unused--unused-but-created-models)
6. [Orphaned / Dead Code](#6-orphaned--dead-code)
7. [Frontend ↔ Backend Integration Gaps](#7-frontend--backend-integration-gaps)
8. [API Analysis](#8-api-analysis)
9. [Database Analysis](#9-database-analysis)
10. [Security Findings](#10-security-findings)
11. [Testing Gaps](#11-testing-gaps)
12. [Performance & Scalability Findings](#12-performance--scalability-findings)
13. [Technical Debt](#13-technical-debt)
14. [Recommended Refactoring](#14-recommended-refactoring)
15. [Prioritized Action Plan](#15-prioritized-action-plan)
16. [Priority Matrix](#16-priority-matrix)
17. [Implementation Roadmap](#17-implementation-roadmap)

---

## 1. Executive Summary

### What this application is

`QuizApp-9AMM` is a **live stage‑show quiz control system** for the "Bazm‑e‑Rekhta" inter‑school Urdu quiz competition. It is not a public web product. It is an operator console: a human presenter drives the entire show from a keyboard, one screen per round per match, while a projector shows the same page. There is no player‑facing client and no concurrency — one operator, one browser, one match at a time.

The tournament structure is hard‑wired into the code: **18 teams → Round 1 (6 matches × 3 teams) → Round 2 (3 matches × 3 teams) → Round 3 / Final (1 match × 3 teams)**, with sub‑rounds MCQ, Audio‑Visual, Sequence, Buzzer, Rapid Fire, Visual Rapid Fire, Passing, Card ("QuickBuzz"), Choice, and a Tie‑Breaker.

### Overall assessment

| Dimension | Rating | Comment |
|---|---|---|
| **Functional completeness** | 🟠 Mostly works, notable holes | Rapid Fire has no question bank wired in; Tie‑Breaker records no score; several routes throw at runtime |
| **Architecture** | 🔴 Poor | No service layer, no repository, no DI. Controllers are the entire application. 2 controllers are 1,900–2,500 lines. |
| **Maintainability** | 🔴 Poor | ~41,000 lines of Razor across 105 views, most of it copy‑paste. 24 view actions in one controller that all `return View()`. |
| **Security** | 🔴 Critical gaps | **Zero authentication, zero authorization, zero CSRF validation.** Scoring is decided client‑side. Unrestricted file upload. |
| **Data integrity** | 🟠 Fragile | Three silent‑rollback bugs; no unique constraints; no indexes beyond PKs; one FK missing entirely |
| **Performance** | 🟠 Acceptable at current scale | Severe N+1 patterns, but the dataset is ~18 teams / a few hundred questions, so it holds |
| **Testing** | 🔴 None | **Zero test projects, zero test files, zero assertions in the entire solution** |
| **Production readiness** | 🔴 Not ready | LocalDB connection string committed, `debug="true"`, no logging, no health check, no CI/CD, no secrets management |

### The five findings that matter most

1. **Scoring is graded in the browser and trusted by the server (P0).** Every gameplay endpoint receives a boolean `Answer` computed by JavaScript (`if (correctOption == lockValue) lockValueStatus = true;`) and awards marks from it without ever re‑checking `OptionStatus` in the database. Additionally, the **correct answer is shipped to the client before the team answers** (`CorrectOption` in the `GetMatchOne*` responses). Any person who can reach the app can forge a score with a single `curl`, and anyone with DevTools open on the operator machine can read the answer key live. For a competition where schools compete for a prize, this is a result‑integrity failure, not merely a coding style issue.

2. **No authentication or authorization at all (P0).** `Web.config` declares Forms auth pointing at `~/Account/Login`, but **no `AccountController` exists** and **no action anywhere carries `[Authorize]`**. All 90+ endpoints — including score mutation and the Excel bulk‑import endpoints — are anonymous. 17 views emit `@Html.AntiForgeryToken()`; **zero controllers validate it**.

3. **Unrestricted file upload to a web‑served directory (P0).** `AudioVisualController.AddAudioVisualQuestions` and `VisualRapidFireController.AddVisualRapidFireQuestions` accept any `HttpPostedFileBase` and `SaveAs()` it into `~/Content/BRFSoftware/images/AV` / `.../VisualRapidFire` with no extension allow‑list, no content‑type check, no size cap beyond `maxRequestLength=1 GB`. Under classic IIS this is a straight path to remote code execution.

4. **Three confirmed silent‑rollback bugs that lose data mid‑show (P0/P1).** `TransactionScope` is opened but `Complete()` is never reached on specific branches — the buzzer "nobody pressed" path (`FirstRoundController.cs:1052`, `FinalRoundController.cs:1874`) and the passing "all three passed" path (`FinalRoundController.cs:1237`, `SecondRoundController.cs:915`). The question is never marked played, so **the show loops on the same question**. Separately, `SecondRoundController.cs:463` has an **empty `catch (Exception EX) { }`** that silently discards a failed Audio‑Visual score write.

5. **The Rapid Fire round has no question source (P1).** The `RapidFire` table and entity exist and are fully mapped, but `db.RapidFires` is referenced **zero times** in the entire codebase. There is no `RapidFireController`, no question‑management UI, no `GetRapidFireQuestion` endpoint. `OnRapidFireLoad` returns only team scores; `PostMatchOneRapidFireAnswer` just adds ±5 marks with no question binding. The round is effectively "presenter reads from paper, clicks a key."

### Quick wins (< 1 day each, high value)

- Delete 3 orphaned `.cshtml` copies under `Content/BRFSoftware/images/Score/` (source disclosure).
- Fix the 3 missing `tS.Complete()` calls and the empty `catch`.
- Add `[Authorize]` + a single hard‑coded operator login, or an IP/host restriction.
- Add an upload extension allow‑list.
- Fix the 3 `Views/Matches/*.cshtml` pages that throw `HttpException` on every request.
- Remove `Selection.cshtml`'s broken `MatchOne` link.

---

## 2. Current Architecture

### 2.1 Technology stack (as built)

| Layer | Technology | Version | Notes |
|---|---|---|---|
| Runtime | .NET Framework | **4.6.1** | `QuizApp.csproj` → `<TargetFrameworkVersion>v4.6.1</TargetFrameworkVersion>`. Out of Microsoft support. |
| Web framework | ASP.NET **MVC 4** | 4.0.20710.0 | 2012‑era. `packages.config`. |
| Also registered | ASP.NET **Web API 2 (v4.0)** | 4.0.20710.0 | `WebApiConfig.Register` is called in `Global.asax.cs`, route `api/{controller}/{id}` mapped — **but zero `ApiController` classes exist**. Entirely vestigial. |
| ORM | **Entity Framework 5.0**, Database‑First EDMX | 5.0.0 | `Models/QuizApp.edmx` (155 KB) + T4 templates. `OnModelCreating` throws `UnintentionalCodeFirstException`. |
| Database | SQL Server **LocalDB** | `(localdb)\MSSQLLocalDB`, catalog `BRFQuizDb_9AMM_Final` | Connection string committed in `Web.config` |
| DB project | SSDT `.sqlproj` | — | 33 table scripts + 3 stored procedures |
| View engine | Razor (WebPages 2.0) | — | 105 `.cshtml`, ~40,900 lines |
| Front‑end | jQuery (5 versions!), **AngularJS 1.x**, Bootstrap 3 + Bootstrap 5, Select2, toastr, TinyMCE 4.7, SweetAlert2 | mixed | No build step, no bundler for app code |
| Excel I/O | **EPPlus 6.0.8** | NonCommercial licence | `SupportedFile/ExcelToDataTable.cs` |
| Hosting | IIS Express, `http://localhost:2028/` | — | `Staging` solution configuration exists but has no transform |

### 2.2 Physical structure

```
QuizApp.sln
├── QuizApp                    (ASP.NET MVC 4 web app — the whole application)
│   ├── App_Start/             BundleConfig, FilterConfig, RouteConfig, WebApiConfig
│   ├── Controllers/           16 controllers, 9,481 LOC
│   ├── Models/                33 EF entities + DbContext (auto-generated from EDMX)
│   ├── VM/                    15 files of view-models / DTOs (hand-written)
│   ├── SupportedFile/         Contants.cs (scoring constants), ExcelToDataTable.cs
│   ├── Views/                 105 .cshtml, 40,881 LOC
│   └── Content/, Scripts/, fonts/
├── QuizApp.Database           (SSDT project — 33 tables, 3 stored procedures)
└── 9AMM_AfterProgram.sql      (334 KB post-event schema + data dump, loose at repo root)
```

There is **no** `.Services`, `.Domain`, `.Infrastructure`, `.Tests`, or `.Common` project. The solution has exactly two projects.

### 2.3 Component map and communication

```
┌──────────────────────────────────────────────────────────────────┐
│  OPERATOR BROWSER (single machine, projector-mirrored)           │
│                                                                  │
│  Admin pages                    Gameplay pages                   │
│  ─────────────                  ──────────────                   │
│  AngularJS 1.x controller       Inline jQuery, ~1,000 lines each │
│  $http.get(GetXxxQuestionList)  Hand-rolled boolean state machine│
│  dirPagination table            (q, o, l, a, r, s flags)         │
│  Excel upload <form>            $(document).keypress → $.ajax    │
│                                 HARDCODED  var matchId = N;      │
│                                            var roundId  = N;     │
└───────────────┬──────────────────────────────────────────────────┘
                │ HTTP (form-encoded POST / GET, JSON responses)
                │ No auth header, no CSRF token, no API versioning
┌───────────────▼──────────────────────────────────────────────────┐
│  ASP.NET MVC 4 CONTROLLERS  (business logic lives HERE, only)    │
│                                                                  │
│  Content controllers          Gameplay controllers               │
│  ──────────────────           ─────────────────────              │
│  MCQ, Buzzer, Card,           FirstRound   (1,324 LOC)           │
│  Choice, Passing,             SecondRound  (1,930 LOC)           │
│  Sequence, AudioVisual,       FinalRound   (2,474 LOC)           │
│  VisualRapidFire,             TieBreaker   (514 LOC)             │
│  TieBreaker, Home,            Score, Landing, Matches            │
│  Matches                                                         │
│                                                                  │
│  field:  BRFQuizEntities db = new BRFQuizEntities();  ← per      │
│          controller instance, NEVER disposed                     │
└───────────────┬──────────────────────────────────────────────────┘
                │ EF 5 LINQ  +  114× raw `exec spGetTotalScoreNew`
┌───────────────▼──────────────────────────────────────────────────┐
│  SQL SERVER LocalDB — BRFQuizDb_9AMM_Final                       │
│  33 tables · 3 stored procedures · PKs+FKs only, no other indexes│
└──────────────────────────────────────────────────────────────────┘
```

**External integrations: none.** No email, no SMS, no payment, no auth provider, no cloud storage, no message queue, no SignalR/WebSocket. The only "integration" is Excel file import via EPPlus and local disk writes for media files.

### 2.4 The domain model in one paragraph

`SchoolsTeam` (18 rows) is joined to `Round` (3) and `Match` (6) through `SchoolsTeam_Matches`. Each question type has a triplet: a **question** table (`MCQ`, `Buzzer`, `Card`, `Choice`, `Passing`, `Sequence`, `Audio_Visual`, `VisualRapidFire`, `RapidFire`, `TieBreaker`), an **option** table (`MCQ_Option`, …, `TieBreakerOption`) and an **answers** table (`MCQ_Answers`, …). Question rows carry `StatusId` (0 = unplayed, 1 = played) which is the entire game‑progress state machine. Scores are never stored as a total — they are re‑aggregated on every request by `spGetTotalScoreNew`, which sums `Marks` across nine answer tables.

### 2.5 Architecture verdict

| Criterion | Verdict | Evidence |
|---|---|---|
| **Clean** | ❌ No | Controllers do HTTP handling, validation, business rules, EF queries, raw SQL, file I/O, and Excel parsing in the same method. No layer boundary exists anywhere. |
| **Maintainable** | ❌ No | Changing the buzzer scoring rule requires editing 2 controllers; changing the buzzer *screen* requires editing 9 near‑identical `.cshtml` files of ~52 KB each. |
| **Scalable** | ⚠️ N/A by design | Single‑operator app. But `sessionState mode="InProc"`, per‑request `new BRFQuizEntities()`, and 114 synchronous stored‑proc calls per page would prevent any multi‑user or multi‑venue deployment. |
| **Production‑ready** | ❌ No | See §10 and §14. |

**However** — one fair point in the design's defence: the constraint set (one operator, offline venue, LocalDB, no network) genuinely does not require most of what "production‑grade" normally implies. The report distinguishes throughout between *required for correctness/integrity* and *architectural best practice*.

---

## 3. What Is Missing

Each item is classified as:
- **[REQUIRED]** — the existing code/schema implies it and the app is incorrect or incomplete without it
- **[RECOMMENDED]** — strongly advised; the app works but is fragile/unsafe without it
- **[OPTIONAL]** — nice‑to‑have

---

### 3.1 Authentication & authorization — **[REQUIRED] · P0**

**Why needed.** `Web.config` line 33 already declares `<authentication mode="Forms"><forms loginUrl="~/Account/Login" timeout="2880" /></authentication>` and a full `<membership>` / `<roleManager>` / `<profile>` provider stack pointing at a connection string named `DefaultConnection`. The design *intends* authentication.

**Evidence of absence.**
- `Controllers/` contains no `AccountController.cs`; `Views/` contains no `Account/` folder → `~/Account/Login` is a 404.
- `grep -c '\[Authorize' Controllers/*.cs` → **0**
- The `DefaultConnection` connection string referenced by all three providers **does not exist** in `<connectionStrings>` → the providers would throw if ever invoked.
- `Scripts/loginModal.js` exists (1,811 bytes) and is referenced by **zero** views — a leftover from the original template.

**Impact.** Anyone on the venue LAN can open `/Score/FormOne` and mutate scores, or `/MCQ/UploadExcel` and replace the entire question bank mid‑show.

**Recommended implementation.**
```
1. Add AccountController with Login (GET/POST) + LogOff, using FormsAuthentication
   with a single operator credential stored as a PBKDF2 hash in Web.config appSettings
   (or a 2-row Users table). Do NOT use the legacy SqlMembershipProvider stack — delete it.
2. Register a global filter:  filters.Add(new AuthorizeAttribute());  in FilterConfig.
3. Whitelist only the public display routes with [AllowAnonymous]:
   Landing/*, Score/Round*Score, Score/LeagueRoundScore.
4. Delete the unused <membership>/<roleManager>/<profile> blocks from Web.config.
```

---

### 3.2 Server‑side answer verification — **[REQUIRED] · P0**

**Why needed.** The server currently has no idea whether an answer was correct; it is told.

**Evidence.**
- `Views/FirstRound/MatchOneMCQ.cshtml` (and 40+ sibling views):
  ```javascript
  var lockValueStatus;
  if (correctOption == lockValue) { lockValueStatus = true;  ... }
  else                            { lockValueStatus = false; ... }
  $.ajax({ url: '.../PostMatchOneMCQAnswer',
           data: { MCQ_OptionId: selctedOptionId, Answer: lockValueStatus, ... } });
  ```
- `Controllers/FirstRoundController.cs:195`:
  ```csharp
  if (mcqAnswer.Answer) { mcq_answer.Marks = Contants.MCQ_Marks; }
  else                  { mcq_answer.Marks = 0; }
  ```
  `MCQ_Option.OptionStatus` — the actual truth in the database — is **never read** in this method.
- The same pattern holds for `PostMatchOneAVAnswer`, `PostMatchOneSequenceAnswer`, `PostMatchOneBuzzerAnswer`, `PostMatchOnePassingAnswer`, `PostMatchOneCardAnswer`, `PostChoiceAnswer`, `PostMatchOneRapidFireAnswer`, `PostMatchOneVisualRapidFireAnswer`.

**Recommended implementation.**
```csharp
// Replace client-supplied Answer with a server-side lookup:
var option = db.MCQ_Option.Find(mcqAnswer.MCQ_OptionId);
if (option == null) return HttpNotFound();
bool isCorrect = option.OptionStatus;          // ← authority
mcq_answer.Marks = isCorrect ? Contants.MCQ_Marks : 0;
mcq_answer.Answer = isCorrect;
```
And **stop shipping `CorrectOption` to the client** in `GetMatchOne*` — the operator screen only needs it to colour the option green *after* the lock, which can be returned by the `Post*Answer` response instead.

---

### 3.3 Rapid Fire question bank wiring — **[REQUIRED] · P1**

**Evidence.** `db.RapidFires` reference count across all 16 controllers: **0**. The `RapidFire` table (`QuizApp.Database/dbo/Tables/RapidFire.sql`) has `Question`, `Answer`, `QuestionNumber`, `RoundsId`, `MatchesId` — it is designed to hold questions — but nothing reads it. `SecondRoundController.OnRapidFireLoad` (line 563) returns team scores only; there is no `GetRapidFireQuestion` endpoint anywhere.

**Impact.** Rapid Fire questions are not in the system. The presenter reads from paper; the app only counts ±5. This also means `RapidFire_Answers` rows cannot be traced back to a question — no audit trail, no dispute resolution.

**Recommended implementation.** Add `RapidFireController` mirroring `AudioVisualController` (Index + `GetRapidFireQuestionList` + `UploadExcel` + `AddRapidFireQuestions` + `Edit`), plus `GetMatchRapidFire(QuestionParameterVM)` in `SecondRoundController`/`FinalRoundController`, and add `RapidFireId` (nullable FK) to `RapidFire_Answers`.

---

### 3.4 Tie‑Breaker scoring & result persistence — **[REQUIRED] · P1**

**Evidence.**
- There is **no `TieBreaker_Answers` table** — the DB project has `TieBreaker.sql` and `TieBreakerOption.sql` only.
- `VM/TieBreakerVM.cs → TieBreakerAnswer` has `{ TieBreakerOptionId, Answer, QuestionNumber, Skip }` — **no `SchoolsTeamId`**. The winning team is not even identifiable.
- `TieBreakerController.PostTieBreakerAnswer` (line 486) only does `tieBreaker.Status = 1;` and echoes the DTO back. No marks, no team, no outcome.

**Impact.** A tie cannot be broken by the system. The outcome exists only in the operator's head.

**Recommended implementation.** Create `TieBreaker_Answers (TieBreaker_AnswersId, TieBreakerId FK, TieBreakerOptionId FK, SchoolsTeamId FK, Answer BIT, Marks INT, MatchId, RoundId, StatusId)`; add `SchoolsTeamId` + `MatchesId` + `RoundsId` to `TieBreakerAnswer`; persist and surface the winner on the score screens.

---

### 3.5 Delete operations for every content type — **[REQUIRED] · P1**

**Evidence.** Across all 16 controllers there is **not a single `Delete` action**. The full action inventory (§8) contains only `Index`, `Get*List`, `UploadExcel`, `Add*Questions`, `Edit`. Once a bad question is imported, the only removal path is SSMS.

**Recommended implementation.** Add `[HttpPost] Delete(int id)` to each content controller (soft‑delete via `StatusId = 2` is safer than hard delete because answer rows FK into option rows), plus a confirm dialog in each `Index.cshtml`.

---

### 3.6 Missing `Edit` for Audio‑Visual and Visual Rapid Fire — **[REQUIRED] · P2**

**Evidence.** `Views/VisualRapidFire/Index.cshtml:20` calls
`window.location.href = "@Url.Action("Edit", "VisualRapidFire")?id=" + VisualRapidFireId;`
but `VisualRapidFireController` has **no `Edit` action** and there is no `Views/VisualRapidFire/Edit.cshtml`. Same gap for `AudioVisualController` (`Views/AudioVisual/` has `Index` + `AddAudioVisualQuestions` only, while every other content module has `Edit.cshtml`).

**Impact.** Clicking the pencil icon produces a 404. The pattern is present in all 7 other modules, so this is an unfinished feature, not a deliberate omission.

---

### 3.7 Show‑reset endpoint — **[REQUIRED] · P1**

**Evidence.** `spReset` exists (`QuizApp.Database/dbo/Stored Procedures/spReset.sql`) and is mapped on the context (`BRFQuizEntities.spReset()`), but **`spReset` is referenced 0 times** in controllers or views.

**Impact.** Between a rehearsal and the live show — or after an aborted match — the operator must run the stored procedure by hand in SSMS. Under stage pressure this is a real operational risk. Note also that `spReset` `TRUNCATE`s **all nine** answer tables globally — it cannot reset a single match.

**Recommended implementation.** Add an authenticated `Admin/Reset` page with (a) full reset → `spReset`, and (b) a new `spResetMatch @RoundsId, @MatchesId` for per‑match reset, both behind a typed confirmation.

---

### 3.8 Undo / correction of a submitted answer — **[REQUIRED] · P1**

**Evidence.** Every `Post*Answer` action is an unconditional `db.<X>_Answers.Add(...)`. There is no update path, no delete path, no idempotency key. A double‑press of the operator key inserts a second scoring row; `spGetTotalScoreNew` sums both.

**Impact.** In a live show, mis‑keys are inevitable. The only compensating control is `ScoreController.FormOne/FormTwo/FormThree`, which adds a hardcoded **+5** to the *last* answer row — an ad‑hoc patch, not a correction facility (see §6.4).

**Recommended implementation.** Add `Post*Answer` idempotency (reject if a row already exists for that `OptionId` + `SchoolsTeamId`), and an authenticated "Adjust score" screen writing an explicit `ScoreAdjustment` row with a reason, rather than silently mutating an answer row.

---

### 3.9 Logging, monitoring, health check — **[RECOMMENDED] · P1**

**Evidence.** Zero `log4net`/`NLog`/`Serilog`/`Trace.Write`/`EventLog` references. One `try/catch` in 9,481 lines of controller code, and it is **empty** (`SecondRoundController.cs:463`). `FilterConfig` registers only `HandleErrorAttribute`, and `<customErrors>` is not configured, so `Views/Shared/Error.cshtml` never renders in local mode — users see the yellow screen of death with a full stack trace.

**Recommended implementation.** Add Serilog (rolling file to `App_Data/logs`), a global `IExceptionFilter` that logs and returns `Error.cshtml`, `<customErrors mode="RemoteOnly">`, and a `/health` endpoint that pings the DB.

---

### 3.10 Automated tests — **[RECOMMENDED] · P1**

**Evidence.** `QuizApp.sln` contains exactly two projects (`QuizApp`, `QuizApp.Database`). No `*.Tests` project, no `[TestMethod]`/`[Fact]`/`[Test]` attribute, no `Moq`/`xunit`/`NUnit`/`MSTest` package in `packages.config`. See §11.

---

### 3.11 Concurrency / turn control — **[RECOMMENDED] · P2**

**Evidence.** Game progress is `StatusId` on the question row, read as `.Where(StatusId == 0).OrderBy(QuestionNumber).Take(1)` and written without any optimistic concurrency token (`rowversion` absent from every table). Two browser tabs on the same match will both grab question N and both score it.

**Impact.** Low today (one operator), but it is the reason the app cannot be extended to a second control station or a separate projector client.

---

### 3.12 Real‑time projector sync — **[OPTIONAL] · P3**

Currently the projector is a mirror of the operator's screen. SignalR would allow a separate audience view (no answer key leakage, better layout). This is also what the sibling **QuickBuzz** .NET 10 project appears to be heading toward with `System.IO.Ports` for physical buzzers.

### 3.13 Other missing items — **[OPTIONAL] · P3**

- Question preview / dry‑run mode before going live
- Export of final results (PDF/Excel) — EPPlus is already referenced and could write as well as read
- Audit log of every operator keypress (invaluable for dispute resolution)
- Localization resource files (Urdu strings are hardcoded in `.cshtml`)
- `README.md` / operator runbook — the repo has **no documentation at all**

---

## 4. What Should Be Improved

Ranked by leverage.

### 4.1 Collapse the 24+29 duplicated view actions into 2 parameterized actions

`FirstRoundController` has 24 actions (`MatchOneMCQ` … `MatchSixBuzzer`) that are byte‑identical: `public ActionResult MatchXxxYyy() { return View(); }` (lines 1193–1320). `SecondRoundController` has 17 more (lines 1789–1924); `FinalRoundController` has 9 (lines 2429–2469). Each is paired with a `.cshtml` that differs from its siblings **only in `var matchId = N;` and the `Url.Action` target of the next screen** — verified by diffing `MatchOneMCQ.cshtml` against `MatchTwoMCQ.cshtml`: 71 differing lines out of ~950, and the substantive difference is one integer.

**Improvement:**
```csharp
// Replace 24 actions with 1:
public ActionResult Play(int round, int match, string stage)
{
    if (!db.SchoolsTeam_Matches.Any(x => x.RoundsId == round && x.MatchesId == match))
        return HttpNotFound();
    return View(stage, new PlayContextVM { RoundsId = round, MatchesId = match });
}
// Route: Play/{round}/{match}/{stage}
```
and replace 24 `.cshtml` files (≈ 700 KB) with 4 (`MCQ`, `AudioVisual`, `Sequence`, `Buzzer`) that read `@Model.MatchesId`. **Estimated deletion: ~28,000 lines of Razor.**

### 4.2 Extract the "load teams + scores" block into one method

The 60‑line `#region CALCULATE MARKS AND TEAMS` block — the LINQ join over `SchoolsTeam_Matches`/`SchoolsTeams` followed by a `switch(i)` that calls `spGetTotalScoreNew` three times — appears **114 times**:

| File | `spGetTotalScoreNew` call sites |
|---|---|
| `FinalRoundController.cs` | 51 |
| `SecondRoundController.cs` | 36 |
| `FirstRoundController.cs` | 24 |
| `ScoreController.cs` | 4 |
| **Total** | **115** (114 in‑game + 1) |

**Improvement:** one `PlayingTeamsVM BuildPlayingTeams(int roundsId, int matchesId)` in a `ScoreService`. Removes ~4,000 lines from the controllers and makes the three‑call‑per‑request N+1 fixable in one place (see §12.1).

### 4.3 Introduce a service layer + repository (or at minimum, DI)

Every controller opens with a raw field:
```csharp
BRFQuizEntities db = new BRFQuizEntities();
```
No `IDisposable` handling — **`protected override void Dispose(bool)` appears 0 times** in the codebase, so contexts are collected non‑deterministically and connections held longer than necessary. There is no seam for testing.

**Improvement (staged):** (a) add the `Dispose` override to every controller — 10 minutes, immediate benefit; (b) introduce a constructor‑injected `IQuizContext` via a lightweight DI container; (c) move scoring and progression rules into `IScoringService` / `IRoundProgressionService`.

### 4.4 Replace the boolean state machine in the views

Every gameplay view carries `var q, o, l, a, r, s;` — six untyped booleans acting as a show‑phase state machine, mutated in a 300‑line `$(document).keypress` switch. There are no comments explaining the flags.

**Improvement:** a small explicit state object (`{ phase: 'AwaitingQuestion' | 'ShowingOptions' | 'Locked' | 'Revealed' }`) in one shared `quiz-runner.js`, included by all gameplay views.

### 4.5 Normalize the answer tables

Five answer tables (`Card_Answers`, `RapidFire_Answers`, `VisualRapidFire_Answers`, `Choice_Answers`) carry `MatchId`/`RoundId` **as denormalized `INT NOT NULL` columns with no FK**, while the other four (`MCQ_Answers`, `Audio_Visual_Answers`, `Buzzer_Answers`, `Passing_Answers`, `Sequence_Answers`) derive match/round by joining up to the question table. Naming is also inconsistent: `MatchId`/`RoundId` (answers) vs `MatchesId`/`RoundsId` (everywhere else).

**Improvement:** pick one shape. Recommended: every `*_Answers` table gets `MatchesId`, `RoundsId`, **and** a nullable FK to its question — this both fixes the RapidFire audit gap (§3.3) and lets `spGetTotalScoreNew` drop its five JOINs.

### 4.6 Consolidate front‑end dependencies

`Scripts/` ships **five jQuery versions** (1.8.1, 1.9.1, 1.12.4, 3.7.1 + slim) and `Content/BRFSoftware/js/` ships a sixth (3.1.0). `_Layout.cshtml` loads jQuery 3.7.1 via bundle while `_Notification.cshtml` loads jQuery **1.12.4 again** — two jQuery instances on the same page whenever a toast is shown. Bootstrap 3.4.1 and Bootstrap 5.3.3 are both present. AngularJS 1.x is used for admin lists while gameplay is raw jQuery.

Confirmed‑unused vendor libraries (reference count 0 in all views and `BundleConfig`):

| Library | Size | Refs |
|---|---|---|
| `knockout-2.1.0.js` + `.debug.js` | 212 KB | **0** |
| `datatables.js` + `.min.js` | 487 KB | **0** |
| `loginModal.js` | 1.8 KB | **0** |
| `BannerCarousel.js` | 823 B | **0** |
| `backtotop.js` | 559 B | **0** |
| `materialcard.js` | 281 B | **0** |
| `main.js` | 63 B | **0** |

Plus the `knockoutjs`, `Modernizr`, `Microsoft.AspNet.WebApi.*`, `Microsoft.Net.Http`, `Microsoft.AspNet.Providers.*` NuGet packages, none of which back live code.

### 4.7 Fix the `[ValidateInput(false)]` blanket

11 of 16 controllers carry `[ValidateInput(false)]` at class level (`AudioVisual`, `Buzzer`, `Card`, `Choice`, `FinalRound`, `FirstRound`, `MCQ`, `Passing`, `Sequence`, `TieBreaker`, `VisualRapidFire`). It is needed for exactly one reason: TinyMCE posts HTML into the `Questions` field. Applying it at class level disables request validation for **every** parameter of **every** action in those controllers, including the gameplay POST endpoints that accept only integers and booleans.

**Improvement:** move to property level — `[AllowHtml] public string Questions { get; set; }` on the VM — and delete all 11 class attributes.

### 4.8 Standardize API response shapes

`GetChoiceQuestionByTopic` returns `Json(new { error = "..." })` on the not‑found path but a `ChoiceAnswerParameterVM` on success — the client cannot distinguish them without sniffing fields. `PostTieBreakerAnswer` returns `Json(new { success = false, message })` on one path and `Json(tbAnswer)` on another. Everything else returns a bare VM with HTTP 200 regardless of outcome.

**Improvement:** a single envelope `{ ok: bool, data: T, error: string }` and correct status codes (400/404/409).

---

## 5. Unused / Unused‑But‑Created Models

Methodology: for each type, I counted `\b<TypeName>\b` occurrences across `Controllers/`, `Views/`, `VM/`, `SupportedFile/` (excluding the `Models/` declaration site itself), then cross‑checked `db.<DbSet>` usage in controllers. Both numbers are reported so loose name collisions (e.g. "Buzzer" appearing in a view title) can be discounted.

### 5.1 EF entities (`QuizApp/Models/`)

| # | Model | File | Purpose | Referenced by | `db.<Set>` uses | In migrations/DB? | Frontend? | Status | Recommendation |
|---|---|---|---|---|---|---|---|---|---|
| 1 | `Audio_Visual` | `Models/Audio_Visual.cs` | AV question | `AudioVisualController`, First/Second/FinalRound | 21 | ✅ `Audio_Visual.sql` | ✅ | **Used** | Keep; add `Edit`+`Delete` (§3.5–3.6) |
| 2 | `Audio_Visual_Answers` | `Models/Audio_Visual_Answers.cs` | AV score row | First/Second/FinalRound, `ScoreController` | 6 | ✅ | ⚠️ indirect | **Used** | Keep; add `MatchesId`/`RoundsId` (§4.5) |
| 3 | `Buzzer` | `Models/Buzzer.cs` | Buzzer question | `BuzzerController`, FirstRound, FinalRound | 20 | ✅ | ✅ | **Used** | Keep |
| 4 | `Buzzer_Answers` | `Models/Buzzer_Answers.cs` | Buzzer score row | FirstRound, FinalRound | 2 | ✅ | ⚠️ indirect | **Used** | Keep. `Answer` is `int` (1/2/3) not `bool` — inconsistent with all siblings; consider an enum |
| 5 | `Buzzer_Option` | `Models/Buzzer_Option.cs` | Buzzer option | `BuzzerController`, FirstRound, FinalRound | 12 | ✅ | ✅ | **Used** | Keep |
| 6 | `Card` | `Models/Card.cs` | QuickBuzz question | `CardController`, SecondRound, FinalRound | 16 | ✅ | ✅ | **Used** | Keep. UI label is "QuickBuzz", entity is "Card" — rename one (§13.4) |
| 7 | `Card_Answers` | `Models/Card_Answers.cs` | Card score row | SecondRound, FinalRound | 2 | ✅ | ⚠️ indirect | **Used** | Keep. Has `MatchId`/`RoundId` but **no link to `Card_Option`** — cannot audit which question was answered |
| 8 | `Card_Option` | `Models/Card_Option.cs` | Card option | `CardController`, SecondRound, FinalRound | 10 | ✅ | ✅ | **Partially used** | Options are stored and displayed but `Card_Answers` never references them. Either add `Card_OptionId` FK or drop the table |
| 9 | `Choice` | `Models/Choice.cs` | Topic‑choice question | `ChoiceController`, SecondRound, FinalRound | 25 | ✅ | ✅ | **Used** | Keep. Newest, best‑structured module |
| 10 | `Choice_Answers` | `Models/Choice_Answers.cs` | Choice score row | SecondRound, FinalRound | 2 | ✅ | ⚠️ indirect | **Used** | Keep |
| 11 | `Choice_Option` | `Models/Choice_Option.cs` | Choice option | `ChoiceController`, SecondRound, FinalRound | 12 | ✅ | ✅ | **Used** | Keep |
| 12 | `Match` | `Models/Match.cs` | Match lookup | 34 files | 33 | ✅ `Matches.sql` | ✅ | **Used** | Keep. Entity `Match` ↔ table `Matches` ↔ DbSet `Matches` — pluralization is inconsistent but harmless |
| 13 | `MCQ` | `Models/MCQ.cs` | MCQ question | `MCQController`, FirstRound, SecondRound | 18 | ✅ | ✅ | **Used** | Keep. Column is `MCQs` (plural) for a single question text — rename to `Question` for consistency with all 8 siblings |
| 14 | `MCQ_Answers` | `Models/MCQ_Answers.cs` | MCQ score row | FirstRound, SecondRound, `ScoreController` | 8 | ✅ | ⚠️ indirect | **Used** | Keep |
| 15 | `MCQ_Option` | `Models/MCQ_Option.cs` | MCQ option | `MCQController`, FirstRound, SecondRound | 14 | ✅ | ✅ | **Used** | Keep. Also appears in `TieBreakerController` lines 254–355 — but **only inside commented‑out code** (§6.2) |
| 16 | `Passing` | `Models/Passing.cs` | Passing question | `PassingController`, SecondRound, FinalRound | 18 | ✅ | ✅ | **Used (UI hidden)** | Keep, but the nav link is commented out in `_Layout.cshtml:60` — see §7.4 |
| 17 | `Passing_Answers` | `Models/Passing_Answers.cs` | Passing score row | SecondRound, FinalRound | 4 | ✅ | ⚠️ indirect | **Partially used** | `PassingNumber INT NOT NULL` is **never assigned** by either controller → always 0. Either populate it from `PassStatus` or drop the column |
| 18 | `Passing_Option` | `Models/Passing_Option.cs` | Passing option | `PassingController`, SecondRound, FinalRound | 15 | ✅ | ✅ | **Used** | Keep |
| 19 | **`QuizCategory`** | `Models/QuizCategory.cs` | Quiz category lookup | **none** | **0** | ✅ `QuizCategory.sql` | ❌ | 🔴 **UNUSED / ORPHANED** | **Delete.** Zero references anywhere in the solution. No controller, no view, no seed. Remove entity, DbSet, EDMX mapping, and table |
| 20 | **`RapidFire`** | `Models/RapidFire.cs` | Rapid‑fire question | Views only (as a *label*, e.g. `MatchOneRapidFire.cshtml`) | **0** | ✅ `RapidFire.sql` | ❌ | 🔴 **ORPHANED (schema live, code dead)** | **Wire it up (§3.3)** — do not delete. The table is the intended question bank; the feature was never finished |
| 21 | `RapidFire_Answers` | `Models/RapidFire_Answers.cs` | Rapid‑fire score row | SecondRound, FinalRound | 2 | ✅ | ⚠️ indirect | **Used** | Keep, but add `RapidFireId` FK once §3.3 lands |
| 22 | `Round` | `Models/Round.cs` | Round lookup | 31 files | 33 | ✅ `Rounds.sql` | ✅ | **Used** | Keep |
| 23 | **`Rounds_QuizCategory`** | `Models/Rounds_QuizCategory.cs` | Round↔Category join | **none** | **0** | ✅ | ❌ | 🔴 **UNUSED / ORPHANED** | **Delete** together with `QuizCategory` (#19). Junction table for a feature that was never built |
| 24 | `SchoolsTeam` | `Models/SchoolsTeam.cs` | Team | `HomeController`, `Home/Index.cshtml`, + navigation from every answer table | 50 | ✅ | ✅ | **Used** | Keep. `StatusId` is written as `0` on import and **never read** — candidate for removal |
| 25 | `SchoolsTeam_Matches` | `Models/SchoolsTeam_Matches.cs` | Team↔Round↔Match | 7 controllers | 55 | ✅ | ✅ | **Used** | Keep. **Missing unique constraint** on `(SchoolsTeamId, RoundsId, MatchesId)` (§9.4) |
| 26 | `Sequence` | `Models/Sequence.cs` | Sequence question | `SequenceController`, FirstRound, FinalRound | 18 | ✅ | ✅ | **Used** | Keep |
| 27 | `Sequence_Answers` | `Models/Sequence_Answers.cs` | Sequence score row | FirstRound, FinalRound | 2 | ✅ | ⚠️ indirect | **Used** | Keep |
| 28 | `Sequence_Option` | `Models/Sequence_Option.cs` | Sequence option + ordering | `SequenceController`, FirstRound, FinalRound | 11 | ✅ | ✅ | **Used** | Keep. Only option table with `WrongSequenceNo`/`RightSequenceNo` — correct for the domain |
| 29 | **`spGetTotalScore_Result`** | `Models/spGetTotalScore_Result.cs` | Result of legacy score SP | **none** | n/a | SP exists | ❌ | 🔴 **DEPRECATED / UNUSED** | **Delete** with `spGetTotalScore` SP. Superseded by `spGetTotalScoreNew` + `VM/ScoreVM.cs → ScoreVMTotal` |
| 30 | `TieBreaker` | `Models/TieBreaker.cs` | Tie‑breaker question | `TieBreakerController`, `MatchTieBreaker.cshtml` | 10 | ✅ | ✅ | **Partially used** | Questions are managed and displayed, but the **result is never recorded** (§3.4). Column is `Status` not `StatusId` — inconsistent with all 32 siblings |
| 31 | `TieBreakerOption` | `Models/TieBreakerOption.cs` | Tie‑breaker option | `TieBreakerController` | 9 | ✅ | ⚠️ indirect | **Partially used** | ⚠️ Navigation property `TieBreaker` exists on the entity, but **`TieBreakerOption.sql` declares NO foreign key** to `TieBreaker` — orphan options are insertable (§9.3) |
| 32 | `VisualRapidFire` | `Models/VisualRapidFire.cs` | Visual rapid‑fire | `VisualRapidFireController`, FinalRound | 9 | ✅ | ✅ | **Used** | Keep; `Edit` action missing (§3.6) |
| 33 | `VisualRapidFire_Answers` | `Models/VisualRapidFire_Answers.cs` | VRF score row | FinalRound only | 1 | ✅ | ⚠️ indirect | **Used (narrow)** | Keep. Written by `FinalRoundController` only — VRF exists in the final round alone |

### 5.2 View‑models / DTOs (`QuizApp/VM/`)

| VM class | File | Purpose | Used by | Status | Recommendation |
|---|---|---|---|---|---|
| `AudioVisualVM` | `AudioVisualVM.cs` | — | **nothing** | 🔴 **Empty class, unused** | Delete (`public class AudioVisualVM { }` — zero members) |
| `AVAnswer` | `AudioVisualVM.cs` | AV answer DTO | First/Second/FinalRound | Used | Keep. Field `Skip` declared but the AV post handlers never read it |
| `LandingVM` | `LandingVM.cs` | — | **nothing** | 🔴 **Empty class, unused** | Delete |
| `RoundOneTeam` | `LandingVM.cs` | 18 team images | `LandingController.RoundOneTeam` | Used | ⚠️ **Refactor** — 18 hardcoded `string TeamOne…TeamEighteen` properties assigned by fixed index (`schoolTeamMatches[0..17]`). **Throws `ArgumentOutOfRangeException` if fewer than 18 teams are set up**, with no guard (contrast: `RoundTwoTeam`/`RoundThreeTeam` *do* guard with `if (Count == 0)`). Replace with `List<string>` |
| `RoundTwoTeam` | `LandingVM.cs` | 9 team images | `LandingController.RoundTwoTeam` | Used | Same refactor; guards only `Count == 0`, not `Count < 9` |
| `RoundThreeTeam` | `LandingVM.cs` | 3 team images | `LandingController.RoundThreeTeam` | Used | Same refactor |
| `RapidFireVM` | `RapidFireVM.cs` | — | **nothing** | 🔴 **Empty class, unused** | Delete |
| `VisualRapidFireVM` | `VisualRapidFireVM.cs` | — | **nothing** | 🔴 **Empty class, unused** | Delete |
| `VisualRapidFireAnswerVM` | `VisualRapidFireVM.cs` | VRF answer DTO | `FinalRoundController` | Used | Keep. Field `Marks` is client‑supplied and **ignored** server‑side — remove it, it invites tampering |
| `ScoreVM` | `ScoreVM.cs` | 8 per‑round subtotals | **nothing** | 🔴 **Unused** | Delete — matches the shape of the deprecated `spGetTotalScore`, superseded by `ScoreVMTotal` |
| `ScoreVMTotal` | `ScoreVM.cs` | `{ TotalMarks }` | 115 call sites | Used | Keep |
| `MatchesVM` | `MatchesVM.cs` | Set‑matches form | `MatchesController.SetMatches` | Used | Keep |
| `MatchListVM` | `MatchesVM.cs` | Match list row | `MatchesController.MatchList` | **Partially used** | `TeamName` property is **never populated** by the projection at `MatchesController.cs:406` — the view shows `SchoolName` only. Either populate or delete the property |
| `MCQVM` / `EditMCQVM` / `MCQOptions` / `MCQAnswer` | `MCQVM.cs` | MCQ CRUD + play | `MCQController`, First/SecondRound | Used | ⚠️ `MCQVM` and `EditMCQVM` are **identical** except `EditMCQVM` adds `MCQId`. Merge with a nullable Id |
| `BuzzerVM` / `EditBuzzerVM` / `BuzzerOptions` / `BuzzerAnswer` | `BuzzerVM.cs` | Buzzer | `BuzzerController`, First/FinalRound | Used | Same duplicate‑VM issue |
| `CardVM` / `EditCardVM` / `CardOptions` / `CardAnswer` | `CardVM.cs` | Card | `CardController`, Second/FinalRound | Used | Same. `CardAnswer.Card_OptionId` is **commented out** — see §5.1 #8 |
| `PassingVM` / `EditPassingVM` / `PassingOptions` / `PassingAnswer` | `PassingVM.cs` | Passing | `PassingController`, Second/FinalRound | Used | Same |
| `SequenceVM` / `EditSequenceVM` / `SequenceOptions` / `SequenceAnswer` | `SequenceVM.cs` | Sequence | `SequenceController`, First/FinalRound | Used | Same |
| `ChoiceVM` / `EditChoiceVM` / `ChoiceOptions` / `ChoiceAnswer` | `ChoiceVM.cs` | Choice | `ChoiceController`, Second/FinalRound | Used | Same |
| `TieBreakerVM` / `EditTieBreakerVM` / `TieBreakerOptions` / `TieBreakerAnswer` | `TieBreakerVM.cs` | Tie‑breaker | `TieBreakerController` | Used | Same. `TieBreakerAnswer` missing `SchoolsTeamId` (§3.4) |
| `ChoiceAnswerParameterVM` | `ChoiceVM.cs` | Choice question payload | Second/FinalRound | Used | Keep |
| **`ChoiceTopicVM`** | `ChoiceVM.cs` | `{ TopicName, IsPlayed }` | **nothing** | 🔴 **Unused** | Delete — both controllers build an **anonymous type** `new { TopicName, StatusId }` instead |
| **`ChoiceStateVM`** | `ChoiceVM.cs` | Choice round state | **nothing** | 🔴 **Unused** | Delete — designed as the proper state container for the Choice round; superseded by `PlayingTeamsVM.ChoiceTopics` (a `dynamic`) |
| `QuestionParameterVM` | `QuestionAnswerVM.cs` | `{ MatchesId, RoundsId, QuestionNumber }` | ~25 endpoints | Used | Keep. `QuestionNumber` is posted by the client but **read by no handler** — remove |
| `MultipleChoiceAnswerParameterVM` | `QuestionAnswerVM.cs` | MCQ payload | First/SecondRound | Used | ⚠️ Exposes `CorrectOption` to the client (§10.2) |
| `AudioVisualAnswerParameterVM` | `QuestionAnswerVM.cs` | AV payload | First/Second/FinalRound | Used | Ships the plain‑text `Answer` to the client before reveal |
| `VisualRapidFireAnswerParameterVM` | `QuestionAnswerVM.cs` | VRF payload | FinalRound | Used | Keep |
| `SequenceAnswerParameterVM` | `QuestionAnswerVM.cs` | Sequence payload | First/FinalRound | Used | Exposes `RightOption` list |
| `BuzzerAnswerParameterVM` | `QuestionAnswerVM.cs` | Buzzer payload | First/FinalRound | Used | Exposes `CorrectOption` |
| `CardAnswerParameterVM` | `QuestionAnswerVM.cs` | Card payload | Second/FinalRound | Used | Exposes `CorrectOption` |
| `PassingAnswerParameterVM` | `QuestionAnswerVM.cs` | Passing payload | Second/FinalRound | Used | Exposes `CorrectOption`. **Missing `QuestionNo`/`QuestionCount`** that all five siblings have — inconsistent |
| `TieBreakerAnswerParameterVM` | `QuestionAnswerVM.cs` | Tie‑breaker payload | `TieBreakerController` | Used | `QuestionCount` commented out |
| `TeamsVM` | `TeamsVM.cs` | Team + score | 5 controllers | Used | Keep |
| `PlayingTeamsVM` | `TeamsVM.cs` | 3 teams + active team | All gameplay controllers | Used | ⚠️ **15 flattened properties** (`SchoolsTeamIdOne/Two/Three`, `TeamNameOne/Two/Three`, …) instead of `List<TeamsVM>`. Also carries `public dynamic ChoiceTopics` — a `dynamic` in a DTO defeats compile‑time checking. Refactor to a list + typed topics |
| `LeagueTeamsVM` | `TeamsVM.cs` | `TeamsVM` + `Rank` | `ScoreController.LeagueRoundScore` | Used | ⚠️ **Duplicate of `TeamsVM`** with one extra `int Rank`. Merge — add `Rank` to `TeamsVM` and delete |
| `OptionList` | `SupportedFile/Contants.cs` | `{ StatusId, Status }` | `Contants.GetStatusList()` → 18 views | Used | Keep, but move out of `Contants.cs` — a DTO does not belong in a constants file |

### 5.3 Summary of model problems

| Problem | Instances |
|---|---|
| **Completely unused (delete)** | `QuizCategory`, `Rounds_QuizCategory`, `spGetTotalScore_Result`, `ScoreVM`, `ChoiceTopicVM`, `ChoiceStateVM`, `AudioVisualVM`, `LandingVM`, `RapidFireVM`, `VisualRapidFireVM` — **10 types** |
| **Orphaned (schema exists, code dead)** | `RapidFire` — wire up, don't delete |
| **Duplicate/overlapping** | 7 × (`XxxVM` vs `EditXxxVM`) differ only by an Id; `TeamsVM` vs `LeagueTeamsVM` differ only by `Rank`; 6 × `XxxOptions` are byte‑identical `{ OptionId, Options, Status }`; 6 × `XxxAnswerParameterVM` are near‑identical |
| **Unnecessary fields** | `Passing_Answers.PassingNumber` (never set), `VisualRapidFireAnswerVM.Marks` (ignored), `QuestionParameterVM.QuestionNumber` (ignored), `MatchListVM.TeamName` (never projected), `SchoolsTeam.StatusId` (never read), `AVAnswer.Skip` (never read) |
| **Missing relationships** | `Card_Answers` → no option/question FK; `RapidFire_Answers` → no question FK; `VisualRapidFire_Answers` → no question FK; `TieBreakerOption` → **no DB‑level FK to `TieBreaker`** despite a navigation property |
| **Incorrect relationships** | `Audio_Visual_Answers.Audio_VisualId` has **no FK constraint** in `Audio_Visual_Answers.sql` even though the column exists and is used for joins in `spGetTotalScoreNew` |
| **Naming inconsistency** | `MCQ.MCQs` vs `Question` (8 siblings); `TieBreaker.Status` vs `StatusId` (32 siblings); `Card_Answers.MatchId` vs `MatchesId`; `Match` entity ↔ `Matches` table; `Contants` (sic) |
| **DTO/entity separation problems** | `AudioVisualController.AddAudioVisualQuestions` and `VisualRapidFireController.AddVisualRapidFireQuestions` **bind the EF entity directly from the request** (`ActionResult AddAudioVisualQuestions(Audio_Visual audio_visual, …)`) — mass‑assignment exposure (§10.6). All other modules correctly bind a VM |
| **Circular dependencies** | None found at type level. EF navigation cycles (`Match ↔ MCQ ↔ MCQ_Option ↔ MCQ_Answers ↔ SchoolsTeam ↔ SchoolsTeam_Matches ↔ Match`) exist but are normal for EF and are not serialized (controllers always project to VMs first) |

---

## 6. Orphaned / Dead Code

### 6.1 Routes that reference controllers that do not exist — **confirmed broken**

`Views/Matches/FirstRound.cshtml`, `SecondRound.cshtml`, `FinalRound.cshtml` each link to five non‑existent controllers:

| Link | Target controller | Exists? |
|---|---|---|
| `@Url.Action("Index", "Events")` | `EventsController` | ❌ |
| `@Url.Action("Index", "Schools")` | `SchoolsController` | ❌ |
| `@Url.Action("Index", "Writers")` | `WritersController` | ❌ |
| `@Url.Action("Index", "ContactUs")` | `ContactUsController` | ❌ |
| `@Url.Action("Index", "AboutUs")` | `AboutUsController` | ❌ |

These are leftovers from a different Bazm‑e‑Rekhta website template. `Url.Action` returns `null` for an unresolvable action, so these render as `<a href="">` — silently broken links.

### 6.2 The three `Views/Matches/*.cshtml` pages throw at runtime — **confirmed defect**

All three define `@section TopNavigations { … }`. Their layout is `~/Views/Shared/_Layout.cshtml`, which renders only `AngularScripts` and `scripts`. Razor throws `HttpException: The following sections have been defined but have not been rendered: "TopNavigations"`.

**Therefore `/Matches/FirstRound`, `/Matches/SecondRound`, and `/Matches/FinalRound` return HTTP 500 on every request** — and all three actions exist in `MatchesController` (lines 18, 23, 28). They are reachable from the nav inside those very pages, and from `Selection.cshtml`. **Fix: delete the three views and the three actions, or add `@RenderSection("TopNavigations", required: false)` to `_Layout.cshtml`.**

### 6.3 Broken match‑selection link

`Views/Matches/Selection.cshtml:47`:
```html
<div id="R1M1"><a href="@Url.Action("MatchOne", "FirstRound")">…M1.png…</a></div>
```
`FirstRoundController` has no `MatchOne` action (it has `MatchOneMCQ`). Every other cell on the page correctly targets `MatchTwoMCQ`, `MatchThreeMCQ`, etc. **Round 1 / Match 1 is unreachable from the selection screen.** One‑character‑class fix: `"MatchOneMCQ"`.

### 6.4 `ScoreController.FormOne / FormTwo / FormThree` — undocumented score hack

```csharp
[HttpPost] public ActionResult FormOne(int SchoolsTeamId) {
    int marks = db.MCQ_Answers.Where(...).ToList().Select(x => x.Marks).LastOrDefault();
    Int32 total = marks + 5;                    // ← magic +5, not from Contants
    ...
}
```
- `FormOne` mutates the team's **last** `MCQ_Answers` row, adding 5. `FormTwo` is a **byte‑for‑byte copy** of `FormOne` that redirects to `RoundTwoScore` instead. `FormThree` does the same on `Audio_Visual_Answers`.
- `FormOne` is `[HttpPost]`; **`FormTwo` and `FormThree` are not** → reachable by GET, i.e. any `<img src>` or prefetch mutates the score.
- `mc` is dereferenced without a null check → `NullReferenceException` if the team has no answer row yet.
- No `[Authorize]`, no CSRF, no audit.
- The `+5` corresponds to no constant in `Contants.cs`. Its business meaning is undocumented.

Referenced from `Views/Score/RoundOneScore.cshtml` / `RoundTwoScore.cshtml` / `RoundThreeScore.cshtml`.

**Recommendation:** replace with the audited score‑adjustment facility from §3.8. If the +5 encodes a real rule ("bonus for X"), name it in `Contants` and document it.

### 6.5 Orphaned duplicate views in a public directory

```
Content/BRFSoftware/images/Score/RoundOneScore.cshtml    (5,866 B)
Content/BRFSoftware/images/Score/RoundTwoScore.cshtml     (5,960 B)
Content/BRFSoftware/images/Score/RoundThreeScore.cshtml   (5,866 B)
```
These are stale copies of `Views/Score/*.cshtml` (which are 9–12 KB, i.e. the real ones have moved on). They are `<Content Include=…>` in `QuizApp.csproj`, so they **are deployed**. The `BlockViewHandler` that prevents `.cshtml` from being served applies only to `Views/Web.config` — it does not cover `Content/`. Depending on IIS handler mapping this is either a 404 or **raw Razor source disclosure**.

**Recommendation:** delete all three files and their `.csproj` entries. (Worth verifying with a request to `/Content/BRFSoftware/images/Score/RoundOneScore.cshtml` on the deployed instance.)

### 6.6 Commented‑out code

**132 lines** of commented‑out executable code in controllers:

| File | Lines | Nature |
|---|---|---|
| `TieBreakerController.cs` | **34** | Lines 254–355 are a wholesale copy of `MCQController`'s Edit logic (`editMCQVM`, `db.MCQ_Option`, `mcq.MCQs`) left commented in place. Proof the file was created by copy‑paste |
| `FinalRoundController.cs` | 27 | Assorted |
| `SecondRoundController.cs` | 27 | Assorted |
| `MatchesController.cs` | 8 | Incl. the abandoned `return View(db.SchoolsTeam_Matches.ToList());` at line 401 |
| `VisualRapidFireController.cs` | 8 | **The entire duplicate‑question validation block (lines 497–503) is commented out** — so VRF Excel import accepts duplicates while every other importer rejects them (§7.6) |
| `ScoreController.cs` | 5 | Dead `//schoolTeamsMatches.MatchesId == questionParameterVM.MatchesId` filters, repeated 3× |
| Others | 23 | |

### 6.7 Dead/unused front‑end assets

See table in §4.6 — `knockout` (212 KB), `datatables` (487 KB), `loginModal.js`, `BannerCarousel.js`, `backtotop.js`, `materialcard.js`, `main.js`: **zero references**. Plus five redundant jQuery copies and an unused Bootstrap major version.

### 6.8 Dead Web API infrastructure

`Global.asax.cs` calls `WebApiConfig.Register(GlobalConfiguration.Configuration)` and `WebApiConfig` maps `api/{controller}/{id}`. **There is not a single `ApiController` in the solution.** Four NuGet packages (`Microsoft.AspNet.WebApi`, `.Client`, `.Core`, `.WebHost`) plus `Microsoft.Net.Http` exist solely to support this. Remove all of it, or — better — use it properly when the endpoints are refactored (§14.3).

### 6.9 Dead database objects

| Object | Status | Evidence |
|---|---|---|
| `spGetTotalScore` | 🔴 Dead | Superseded by `spGetTotalScoreNew`. Referenced 0 times. Also **functionally wrong now** — it omits Card, VisualRapidFire and Choice totals |
| `spReset` | 🟠 Live but uncallable from the app | Mapped on the context, invoked 0 times (§3.7) |
| `QuizCategory`, `Rounds_QuizCategory` tables | 🔴 Dead | 0 references |
| `9AMM_AfterProgram.sql` (334 KB) | 🟠 Stray artifact | A post‑event `USE [BRFQuizDb_9AMM_Final]` schema+data dump dated 14‑12‑2025, sitting loose at the repo root outside both projects. Contains real competition data. Move to a `db/snapshots/` folder or remove from source control |

---

## 7. Frontend ↔ Backend Integration Gaps

### 7.1 Backend endpoints with **no** front‑end consumer

| Endpoint | File:line | Note |
|---|---|---|
| `FinalRound.MatchOnePassing` (view action) | `FinalRoundController.cs:2449` | `Views/FinalRound/MatchOnePassing.cshtml` exists (52 KB) but **no `Url.Action` anywhere links to it**. Reachable only by typing the URL |
| `FinalRound.MatchOneRapidFire` | `FinalRoundController.cs:2439` | Same — view exists (14.5 KB), zero inbound links |
| `SecondRound.MatchOnePassing` / `MatchTwoPassing` / `MatchThreePassing` | `SecondRoundController.cs:1846, 1881, 1916` | Three 53 KB views, zero inbound links |
| `SecondRound.MatchOneCard` / `MatchTwoCard` / `MatchThreeCard` | `SecondRoundController.cs:1853, 1888, 1924` | Linked once each — from the *Choice* screens only; not from `Selection.cshtml` |
| `Matches.FirstRound` / `SecondRound` / `FinalRound` | `MatchesController.cs:18, 23, 28` | Linked, but the views **throw** (§6.2) |
| `Matches.Selection` | `MatchesController.cs:222` | Linked 53× from gameplay views, but **not from `_Layout.cshtml`'s main nav** — the operator must know the keyboard shortcut |
| `Home.UploadExcel` (GET) | `HomeController.cs:21` | Declared without `[HttpPost]`; the form posts to it, but it is also GET‑reachable |
| `FinalRound.PostMatchImageStatusUpdate` | `FinalRoundController.cs:772` | Linked from exactly one view; VRF‑specific |

**Root cause:** the Passing round appears to have been **removed from the running order** (its nav link is commented out in `_Layout.cshtml:60` — `@*<li><a href="@Url.Action("Index", "Passing")">Passing</a></li>*@`) while the entire back end, the 6 gameplay views (≈ 320 KB) and the `PassingController` CRUD remain. This is the single largest block of orphaned functionality in the project.

### 7.2 Front‑end calls with **no** matching backend endpoint

| View:line | Call | Problem |
|---|---|---|
| `Views/Matches/Selection.cshtml:47` | `Url.Action("MatchOne", "FirstRound")` | No such action → dead link, R1M1 unreachable |
| `Views/VisualRapidFire/Index.cshtml:20` | `Url.Action("Edit", "VisualRapidFire")` | No such action → 404 on the edit pencil |
| `Views/Matches/{First,Second,Final}Round.cshtml` | 5 × `Url.Action("Index", "<Events\|Schools\|Writers\|ContactUs\|AboutUs>")` | No such controllers → `href=""` |

### 7.3 Data contract mismatches

- **`MCQ.MCQs` → `Question`.** `MCQController.GetMCQQuestionList` aliases `Question = mcqList.MCQs` so the Angular view can bind `{{mcq.Question}}`. Every other module's entity already has a `Question` column. The alias is a papering‑over of the naming inconsistency (§5.3).
- **`Audio_Visual` returns the answer text** in `GetAudioVisualQuestionList` and in `GetMatchOneAV` — the plain answer reaches the browser before the reveal.
- **`PlayingTeamsVM.ChoiceTopics` is `dynamic`.** The Choice views bind `response.ChoiceTopics[i].TopicName` / `.StatusId` against an anonymous type. Any rename in the controller breaks the view silently at runtime.

### 7.4 Features present in the UI but not backed end‑to‑end

| UI element | Backend state |
|---|---|
| "Passing" nav link | Commented out in `_Layout.cshtml`, full backend alive |
| VRF "Edit" pencil | 404 |
| AV "Edit" | No pencil rendered and no action — the only module with neither |
| `MatchListVM.TeamName` column | Property exists, projection never sets it, view never shows it |
| Tie‑Breaker "winner" | No UI, no data, no endpoint |

### 7.5 Hardcoded match/round IDs in the view layer

Every gameplay `.cshtml` begins with `var matchId = N; var roundId = M;`. The match identity — a **data** concern — lives in **41 separate view files**. Adding a 7th first‑round match means creating 4 new controller actions and 4 new 30–52 KB views. See §4.1.

### 7.6 Inconsistent import validation between modules

| Importer | Validates Round/Match exist? | Rejects duplicate `(Round, Match, QuestionNumber)`? |
|---|---|---|
| `AudioVisualController.UploadExcel` | ✅ | ✅ |
| `MCQ` / `Buzzer` / `Card` / `Choice` / `Passing` / `Sequence` | ✅ | ✅ |
| **`VisualRapidFireController.UploadExcel`** | ❌ **commented out** (lines 497–503) | ❌ **commented out** |
| `HomeController.UploadExcel` (teams) | n/a | ❌ — no duplicate‑team check at all, only a `maxTeams = 18` cap |
| `MatchesController.UploadSetMatchesExcel` | ✅ | ✅ |

---

## 8. API Analysis

Route table: a single convention route, `{controller}/{action}/{id}` (`RouteConfig.cs`). Plus the vestigial `api/{controller}/{id}` (§6.8). **No attribute routing, no HTTP‑verb constraints on 90 % of actions, no versioning.**

Legend — **Auth**: all rows are 🔴 Anonymous unless stated. **CSRF**: all rows are 🔴 Unvalidated.

### 8.1 Content management (admin) endpoints

| Endpoint | Verb | Purpose | Consumer | Backend | FE use | Validation | Errors | Issues |
|---|---|---|---|---|---|---|---|---|
| `GET /Home/Index` | GET | Team list | `Home/Index.cshtml` | ✅ | ✅ | — | none | Loads all teams eagerly |
| `POST /Home/UploadExcel` | GET+POST | Bulk‑import teams | `Home/Index.cshtml` | ✅ | ✅ | Cap 18; **no dup check** | `TempData` only | **No `[HttpPost]`** → GET‑callable. `SaveChanges()` **inside the loop** (18 round‑trips). No transaction — a mid‑file failure leaves partial data. `row["SchoolName"].ToString()` throws if the column is absent |
| `GET /{Module}/Index` × 8 | GET | Question list page | nav | ✅ | ✅ | — | none | — |
| `GET /{Module}/Get{X}QuestionList` × 8 | GET | JSON question list | AngularJS `$http.get` | ✅ | ✅ | — | none | **N+1**: `db.MCQs.ToList()` then one option query per row (§12.2). Returns **`OptionStatus` — the answer key — to any anonymous caller** |
| `POST /{Module}/UploadExcel` × 8 | GET+POST | Bulk import | module `Index.cshtml` | ✅ | ✅ | Varies (§7.6) | `TempData` | **No `[HttpPost]`**. `SaveChanges()` per row. `Convert.ToInt32(row[...])` throws `FormatException` on malformed cells → yellow screen |
| `GET/POST /{Module}/Add{X}Questions` × 9 | GET/POST | Create question | module page | ✅ | ✅ | `[Required]` on VM | `ModelState` | `AudioVisual` + `VisualRapidFire` bind the **EF entity** directly (§10.6) |
| `GET/POST /{Module}/Edit` × 7 | GET/POST | Update question | list page | ✅ | ✅ | `[Required]` | `ModelState` | `id = 0` default → `Find(0)` returns null → NRE. **Missing for `AudioVisual` and `VisualRapidFire`** (§3.6) |
| — | — | **Delete** | — | ❌ **absent** | — | — | — | **No delete anywhere** (§3.5) |

### 8.2 Match & scoring endpoints

| Endpoint | Verb | Purpose | Consumer | Backend | FE use | Issues |
|---|---|---|---|---|---|---|
| `GET /Matches/SetMatches` | GET | Assign teams→match | nav | ✅ | ✅ | — |
| `POST /Matches/SetMatches` | POST | Persist assignment | `SetMatches.cshtml` | ✅ | ✅ | `foreach (var item in matchesVM.SchoolsTeamId)` → **NRE if no team selected** ( `[Required]` on a `List<int>` does not reject an empty list). `return RedirectToAction` **inside** the `using (TransactionScope)` before `Complete()` on two branches |
| `POST /Matches/UploadSetMatchesExcel` | GET+POST | Bulk assign | `SetMatches.cshtml` | ✅ | ✅ | No `[HttpPost]`; `SaveChanges()` per row |
| `GET /Matches/MatchList` | GET | List assignments | nav | ✅ | ✅ | 4‑table join, no paging |
| `GET /Matches/Selection` | GET | Match picker | 53 views | ✅ | ✅ | Not in main nav; R1M1 link broken (§6.3) |
| `GET /Matches/{First,Second,Final}Round` | GET | — | nav | ⚠️ | ⚠️ | **HTTP 500** (§6.2) |
| `POST /Score/FormOne` | POST | +5 to last MCQ answer | `RoundOneScore.cshtml` | ✅ | ✅ | Magic +5; NRE risk; no auth (§6.4) |
| `GET /Score/FormTwo` | **GET** | +5 to last MCQ answer | `RoundTwoScore.cshtml` | ✅ | ✅ | **State‑mutating GET** |
| `GET /Score/FormThree` | **GET** | +5 to last AV answer | `RoundThreeScore.cshtml` | ✅ | ✅ | **State‑mutating GET** |
| `GET /Score/RoundOneScore` | GET | R1 scoreboard | keyboard `0` | ✅ | ✅ | 18 SP calls per page load. `.First()` → **throws if the SP returns no row** |
| `GET /Score/RoundTwoScore` / `RoundThreeScore` | GET | R2/R3 scoreboard | keyboard `9`/`8` | ✅ | ✅ | Same; three near‑identical 45‑line methods |
| `GET /Score/LeagueRoundScore` | GET | Ranked league table | keyboard `7` | ✅ | ✅ | Only method that uses `FirstOrDefault()` + null‑coalescing — the correct pattern the other three should copy. Computes `sortedTeams` and **never uses it** (dead variable) |
| `GET /Landing/{Index,ThankYou}` | GET | Idle screens | keyboard | ✅ | ✅ | — |
| `GET /Landing/RoundOneTeam` | GET | 18‑team splash | keyboard `1` | ✅ | ✅ | **`IndexOutOfRangeException` if < 18 teams** — no guard (§5.2) |
| `GET /Landing/RoundTwoTeam` / `RoundThreeTeam` | GET | Splash | keyboard `2`/`3` | ✅ | ✅ | Guards `Count == 0` only, not `Count < 9` / `< 3` |

### 8.3 Gameplay endpoints (the live‑show API)

All are `JsonResult`, all accept form‑encoded POST (a few GET), all anonymous, none CSRF‑validated, none verb‑constrained.

| Pattern | Instances | Purpose | Consumer | Issues |
|---|---|---|---|---|
| `On{X}Load(QuestionParameterVM)` | 17 | Return 3 teams + live scores + whether a next question exists | Every gameplay view on `$(document).ready` | 3 stored‑proc calls each; 60 lines duplicated 114× (§4.2) |
| `GetMatch{X}(QuestionParameterVM)` | 14 | Return the next unplayed question + options | Operator presses `Q` | 🔴 **Returns `CorrectOption` / `Answer` / `RightOption` to the client** (§10.2). 🔴 **Unguarded null**: `mcq.MCQs` dereferenced right after `FirstOrDefault()` — NRE when the bank is exhausted |
| `PostMatch{X}Answer(...)` | 17 | Persist score, mark question played, return updated scores | Operator presses `A` | 🔴 **Trusts the client's `Answer` boolean** (§3.2). 🔴 Missing `tS.Complete()` on 3 branches (§10.7). No idempotency |
| `GetChoiceTopics(int, int)` | 2 | Topic grid | Choice topic screen | Only endpoints with **explicit scalar parameters** rather than a VM — inconsistent |
| `GetChoiceQuestionByTopic(string, int, int)` | 2 | Question for a chosen topic | Choice question screen | Returns `{ error: "..." }` with HTTP 200 on the failure path — shape mismatch |
| `PostMatchImageStatusUpdate(...)` | 1 | Mark a VRF image shown | `MatchOneVisualRapidFire.cshtml` | FinalRound only |
| `GetTieBreakerQuestion(...)` / `PostTieBreakerAnswer(...)` | 2 | Tie‑breaker | `MatchTieBreaker.cshtml` | `SingleOrDefault(QuestionNumber == n)` → **throws `InvalidOperationException` on duplicate question numbers** (no unique constraint exists). Records no score (§3.4) |

### 8.4 Cross‑cutting API problems

| # | Problem | Evidence |
|---|---|---|
| 1 | **Duplicate endpoints across round controllers** | `OnAudioVisualLoad` exists in FirstRound (:329), SecondRound (:318) and FinalRound (:305) with near‑identical bodies. Same for `GetMatchOneAV`, `PostMatchOneAVAnswer`, `OnSequenceLoad`, `OnBuzzerLoad`, `OnPassingLoad`, `OnCardLoad`, `OnChoiceLoad`, `GetChoiceTopics`, `GetChoiceQuestionByTopic`, `PostChoiceAnswer`, `OnRapidFireLoad`, `PostMatchOneRapidFireAnswer` — **~13 endpoint families duplicated 2–3×** |
| 2 | **Misleading names** | `GetMatchOneMultipleChoice`, `PostMatchOneMCQAnswer` etc. say "MatchOne" but serve all six matches (the match comes from the payload). `SecondRoundController` has both `GetMatchCard` and `PostMatchCardAnswer` (no "One") while everything else keeps "One" — inconsistent |
| 3 | **No verb constraints** | Only 9 actions in the whole app carry `[HttpPost]`. Every `Post*Answer` gameplay endpoint accepts GET |
| 4 | **No status codes** | Every path returns HTTP 200. Client error handling is `error: function () { alert("error occured"); }` |
| 5 | **No rate limiting / replay protection** | A loop of `curl -d "SchoolsTeamId=3&Answer=true&..." /FirstRound/PostMatchOneMCQAnswer` adds unbounded score |

---

## 9. Database Analysis

### 9.1 Schema inventory

33 tables + 3 stored procedures in `QuizApp.Database/dbo/`. Grouped:

| Group | Tables |
|---|---|
| Reference | `Rounds`, `Matches`, `SchoolsTeam`, `QuizCategory` 🔴 |
| Junction | `SchoolsTeam_Matches`, `Rounds_QuizCategory` 🔴 |
| Questions (9) | `MCQ`, `Buzzer`, `Card`, `Choice`, `Passing`, `Sequence`, `Audio_Visual`, `VisualRapidFire`, `RapidFire` 🟠, `TieBreaker` |
| Options (7) | `MCQ_Option`, `Buzzer_Option`, `Card_Option`, `Choice_Option`, `Passing_Option`, `Sequence_Option`, `TieBreakerOption` |
| Answers (9) | `MCQ_Answers`, `Buzzer_Answers`, `Card_Answers`, `Choice_Answers`, `Passing_Answers`, `Sequence_Answers`, `Audio_Visual_Answers`, `VisualRapidFire_Answers`, `RapidFire_Answers` |

🔴 = orphaned · 🟠 = schema live, code dead

### 9.2 Indexes — **the single biggest structural gap**

**Every table has exactly one index: its clustered primary key.** There is not one `CREATE INDEX`, `CREATE NONCLUSTERED INDEX`, or `UNIQUE` constraint in the entire `QuizApp.Database` project.

Every hot query filters on non‑indexed columns:

```sql
-- executed on every question advance, 9 question types × 3 rounds:
WHERE MatchesId = @m AND RoundsId = @r AND StatusId = 0 ORDER BY QuestionNumber

-- executed 3× per page load, 114 call sites:
WHERE SchoolsTeamId = @t AND MatchId = @m AND RoundId = @r
```

**Recommended indexes (18 total):**
```sql
-- One per question table (9):
CREATE NONCLUSTERED INDEX IX_MCQ_Match_Round_Status
    ON dbo.MCQ (MatchesId, RoundsId, StatusId) INCLUDE (QuestionNumber);
-- …identical shape for Buzzer, Card, Choice, Passing, Sequence,
--   Audio_Visual, VisualRapidFire, RapidFire

-- One per answers table (9):
CREATE NONCLUSTERED INDEX IX_MCQ_Answers_Team
    ON dbo.MCQ_Answers (SchoolsTeamId) INCLUDE (MCQ_OptionId, Marks);
CREATE NONCLUSTERED INDEX IX_Card_Answers_Team_Match_Round
    ON dbo.Card_Answers (SchoolsTeamId, MatchId, RoundId) INCLUDE (Marks);
-- …etc.

-- Junction:
CREATE NONCLUSTERED INDEX IX_SchoolsTeam_Matches_Round_Match
    ON dbo.SchoolsTeam_Matches (RoundsId, MatchesId) INCLUDE (SchoolsTeamId);
```
At 18 teams and a few hundred questions the table scans are invisible. They are still the right fix, and they are ~30 minutes of work.

### 9.3 Missing / incorrect foreign keys

| Table | Column | FK present? | Consequence |
|---|---|---|---|
| **`TieBreakerOption`** | `TieBreakerId INT NOT NULL` | 🔴 **NO** | `TieBreakerOption.sql` declares only the PK. The EF entity has a `virtual TieBreaker TieBreaker` navigation property, so the EDMX *believes* the relationship exists. Orphan options are insertable and `TieBreaker` rows are deletable while options survive |
| **`Audio_Visual_Answers`** | `Audio_VisualId INT NULL` | 🔴 **NO** | Column exists and is joined in `spGetTotalScoreNew`, but no constraint. Orphan score rows possible |
| `Card_Answers` | `MatchId`, `RoundId` | 🔴 NO | Denormalized ints, no FK to `Matches`/`Rounds` |
| `RapidFire_Answers` | `MatchId`, `RoundId` | 🔴 NO | Same |
| `VisualRapidFire_Answers` | `MatchId`, `RoundId` | 🔴 NO | Same |
| `Choice_Answers` | `MatchId`, `RoundId` | 🔴 NO | Same |
| `Card_Answers` | — | 🔴 no question link at all | Cannot tell which Card question a score row belongs to |
| `RapidFire_Answers` | — | 🔴 no question link | Same |
| `VisualRapidFire_Answers` | — | 🔴 no question link | Same |

Where FKs *do* exist they are correct and consistently named (`FkChild_Column`).

### 9.4 Missing unique constraints

| Should be unique | Currently | Consequence |
|---|---|---|
| `(RoundsId, MatchesId, QuestionNumber)` on all 9 question tables | not enforced | Duplicate question numbers. Both `Add*Questions` and `UploadExcel` do a `SELECT`‑then‑`INSERT` check — a TOCTOU race, and VRF's check is commented out entirely |
| `TieBreaker.QuestionNumber` | not enforced | `TieBreakerController.cs:495` uses `SingleOrDefault(...)` → **`InvalidOperationException`** on a duplicate |
| `(SchoolsTeamId, RoundsId, MatchesId)` on `SchoolsTeam_Matches` | not enforced | A team can be assigned to the same match twice; the app checks in C# instead (`MatchesController.cs:367`) |
| `(SchoolName, TeamName)` on `SchoolsTeam` | not enforced | `HomeController.UploadExcel` has **no duplicate check** — re‑running the import creates 18 more teams (up to the cap) |
| Exactly one `OptionStatus = 1` per question | not enforced | Nothing prevents 0 or 4 correct answers; `GetMatch*` would then set `CorrectOption` to the last true option or leave it 0 |

### 9.5 Data‑type and column issues

| Issue | Detail |
|---|---|
| **`NVARCHAR(MAX)` overuse** | `SchoolName`, `TeamName`, `ScoreImage`, `SelectionImage`, `RoundsName`, `MatchesName`, `FileType`, `Audio_Visuals`, `Visuals`, every `Options` column, every `Question` column. Filenames and 20‑char names do not need MAX. MAX columns are stored off‑row (LOB) and **cannot be indexed**. `Choice` is the exception and does it right: `TopicName NVARCHAR(100)` |
| **`StatusId INT` used as a boolean/enum with no constraint** | 0 = unplayed, 1 = played (inferred; never documented). No `CHECK`, no lookup table. `Contants.GetStatusList()` generates 1–4 for a UI dropdown, hinting at 4 intended states that the code never uses |
| **`Buzzer_Answers.Answer INT`** vs `BIT` everywhere else | 1 = right, 2 = wrong, 3 = not answered. Undocumented magic numbers; should be a `CHECK` constraint or lookup |
| **`Marks` nullable in 2 of 9 answer tables** | `Buzzer_Answers.Marks INT NULL`, `Passing_Answers.Marks INT NULL`; the other seven are `NOT NULL`. `spGetTotalScoreNew` wraps everything in `ISNULL(...,0)` so it works, but the inconsistency is a latent trap |
| **No timestamps** | Not one table has `CreatedAt`/`UpdatedAt`/`CreatedBy`. **There is no way to reconstruct when a score was awarded** — fatal for dispute resolution in a competition |
| **No `rowversion`** | No optimistic concurrency anywhere (§3.11) |
| **No soft‑delete column** | With no delete endpoints this is moot today, but §3.5 will need it |

### 9.6 Normalization

The 9 × (question / option / answer) triplet structure is **denormalized by design** — nine near‑identical trios instead of one polymorphic `Question`/`Option`/`Answer` set with a `QuestionTypeId`. Given a live‑show app with fixed round types, this is a defensible trade (each round has genuinely different mechanics: `Sequence_Option` needs ordering, `Buzzer_Answers` needs a tri‑state, `Passing_Answers` needs a pass counter). But it is the direct cause of:

- `spGetTotalScoreNew` being a 9‑branch UNION‑of‑subqueries
- every gameplay controller being a 9× copy of the same shape
- the four different answer‑table shapes documented in §4.5

**Recommendation:** do **not** attempt full polymorphic normalization — the migration cost far exceeds the benefit for this domain. Instead, standardize the answer tables (§4.5) so the nine variants at least share one shape.

### 9.7 Stored procedures

| SP | Status | Issues |
|---|---|---|
| `spGetTotalScoreNew` | ✅ Live, called 114× | Correct (sums all 9 answer types). But it is a **scalar‑returning proc executed once per team per page** — 3 executions per gameplay request, 18 per Round‑1 scoreboard. Should be a set‑returning proc taking a match, or a `SUM` over an indexed view |
| `spGetTotalScore` | 🔴 Dead | Also **wrong** — omits Card, VRF and Choice. Delete before someone calls it |
| `spReset` | 🟠 Uncallable from the app | `TRUNCATE`s all 9 answer tables and resets all `StatusId` — **global, not per‑match**. No transaction wrapper, no confirmation, no logging. `TRUNCATE` will fail if any FK references the table (it does not today, but will after §9.3) |

### 9.8 Migrations & seed data

- **There is no migration framework.** EF 5 Database‑First with EDMX means schema changes are made in SSMS/SSDT and then "Update Model from Database". There is no `Migrations/` folder, no `__MigrationHistory`, no versioning of schema changes.
- **There is no seed script.** `Rounds` (3 rows) and `Matches` (6 rows) are lookup tables the entire app depends on, and nothing in source control populates them. A fresh clone produces a working build against an empty, non‑functional database.
- `9AMM_AfterProgram.sql` (334 KB) is a full post‑event dump — schema + real competition data — sitting loose at the repo root. It is not referenced by either project.

**Recommendation:** add `QuizApp.Database/Scripts/PostDeployment/Seed.sql` (idempotent `MERGE` for `Rounds` and `Matches`), register it in the `.sqlproj`, and move `9AMM_AfterProgram.sql` under `db/snapshots/` (or out of source control).

### 9.9 Queries that degrade at scale

| Query | Location | Today | At 10× |
|---|---|---|---|
| `exec spGetTotalScoreNew` × 3 per request | 114 sites | ~5 ms | Linear in answer rows × 9 tables, unindexed |
| `db.MCQs.ToList()` then per‑row option query | `Get*QuestionList` × 8 | 1 + N | N=500 → 501 queries |
| `db.Audio_Visual.ToList()` before projection | `AudioVisualController.cs:24` | full table into memory | Full table scan + full materialization |
| `SaveChanges()` inside the import loop | 10 importers | 1 round trip per row | 1,000‑row file = 1,000 transactions |
| `.Where(...).ToList().Select(x => x.Marks).LastOrDefault()` | `ScoreController.FormOne/Two/Three` | materializes all rows to take one | — |
| `db.MCQs.Where(...).ToList().Count` | `GetMatchOneMultipleChoice:127` | materializes rows to count them | Should be `.Count()` |

---

## 10. Security Findings

Severity: 🔴 Critical · 🟠 High · 🟡 Medium · 🔵 Low. OWASP Top 10 (2021) mapping included.

### 🔴 SEC‑01 — Complete absence of authentication and authorization (A01, A07)

`grep -c '\[Authorize' Controllers/*.cs` → **0**. `Web.config` points Forms auth at `~/Account/Login`, which does not exist. All ~90 endpoints are anonymous, including `Score/FormOne`, every `Post*Answer`, and every `UploadExcel`.
**Fix:** §3.1. **Effort: 1 day.**

### 🔴 SEC‑02 — Answer key disclosed to the client before the answer is given (A04)

`GetMatchOneMultipleChoice`, `GetMatchOneBuzzer`, `GetMatchOneCard`, `GetMatchOnePassing`, `GetChoiceQuestionByTopic`, `GetTieBreakerQuestion` all return `CorrectOption`. `GetMatchOneAV` returns the plain‑text `Answer`. `GetMatchOneSequence` returns `RightOption`. `Get{X}QuestionList` returns `OptionStatus` for **every question in the bank** to any anonymous caller.
**Impact:** on a shared/venue network, an unauthenticated request to `/MCQ/GetMCQQuestionList` dumps the entire answer key.
**Fix:** strip correctness fields from all pre‑answer payloads; return correctness only in the `Post*Answer` response. Add `[Authorize]` to the list endpoints. **Effort: 1 day.**

### 🔴 SEC‑03 — Client‑side scoring trusted by the server (A04, A08)

Detailed in §3.2. Any `POST` with `Answer=true` awards marks.
**Fix:** §3.2. **Effort: 2 days across 17 endpoints.**

### 🔴 SEC‑04 — Unrestricted file upload into a web‑served directory (A03, A05)

```csharp
// AudioVisualController.cs:127 and VisualRapidFireController.cs:114
string fileName = Path.GetFileName(AVFile.FileName);
string filePath = Path.Combine(Server.MapPath(Contants.strAudioVisual_File), fileName);
AVFile.SaveAs(filePath);       // → ~/Content/BRFSoftware/images/AV/<anything>
```
No extension allow‑list, no MIME check, no magic‑byte check, no size limit (`maxRequestLength="1048576"` KB = **1 GB**; `maxAllowedContentLength` = 40 MB, so IIS caps it at 40 MB), no filename randomization, no `<handlers>` restriction on that folder. `Path.GetFileName` does block `../` traversal — that part is correct — but nothing blocks `shell.aspx`.
**Impact:** upload `evil.aspx` → request `/Content/BRFSoftware/images/AV/evil.aspx` → **RCE as the app pool identity**, unauthenticated.
**Fix:**
```csharp
var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".mp3", ".mp4", ".webm" };
var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
if (!allowed.Contains(ext)) { ModelState.AddModelError("", "Unsupported file type."); … }
var safeName = Guid.NewGuid().ToString("N") + ext;
```
plus a `web.config` in the upload folder with `<handlers><clear/><add name="StaticFile" …/></handlers>` and `<add key="..." value="..."/>` request filtering. **Effort: 0.5 day.**

### 🔴 SEC‑05 — CSRF unprotected on every state‑changing endpoint (A01)

`ValidateAntiForgeryToken` appears **0 times** in controllers. `@Html.AntiForgeryToken()` appears **17 times** in views — tokens are issued and then ignored. Combined with SEC‑01 (no auth) and the GET‑accessible mutators (SEC‑06), any page the operator visits can silently rewrite scores.
**Fix:** add `[ValidateAntiForgeryToken]` to every POST action and `@Html.AntiForgeryToken()` to every form/AJAX header. **Effort: 0.5 day.**

### 🟠 SEC‑06 — State‑mutating operations reachable by GET (A01, A04)

| Endpoint | Effect |
|---|---|
| `Score/FormTwo`, `Score/FormThree` | +5 marks |
| All 10 `UploadExcel` actions | bulk insert |
| All 17 `Post*Answer` actions | insert score row, mark question played |
| `TieBreaker/PostTieBreakerAnswer` | mark question played |

Only 9 actions carry `[HttpPost]`.
**Fix:** `[HttpPost]` on every mutator. **Effort: 2 hours.**

### 🟠 SEC‑07 — Stored XSS via question content (A03)

11 controllers carry class‑level `[ValidateInput(false)]`. TinyMCE‑authored HTML is stored raw and rendered with `$("#Question").html(question)` and `@Html.Raw`‑equivalent Angular bindings. `Views/Web.config` additionally sets `validateRequest="false"`.
**Impact:** an operator (or an anonymous caller, per SEC‑01) can inject `<script>` into a question that executes on the projector screen and in the admin list.
**Fix:** replace class‑level `[ValidateInput(false)]` with property‑level `[AllowHtml]` (§4.7) **and** sanitize with `HtmlSanitizer` on write. **Effort: 1 day.**

### 🟠 SEC‑08 — Sensitive configuration in source control (A05)

`Web.config` commits the full connection string (`data source=(localdb)\MSSQLLocalDB;initial catalog=BRFQuizDb_9AMM_Final;integrated security=True;encrypt=False;trustservercertificate=True`). `encrypt=False` and `trustservercertificate=True` are both insecure defaults for any non‑LocalDB target. There is no `Web.Staging.config` despite a `Staging` solution configuration existing. `Web.Release.config` only strips `debug`.
**Fix:** move the connection string to a `configSource`d file excluded from source control, or use `aspnet_regiis -pe`. Set `encrypt=True` for any real SQL Server. **Effort: 0.5 day.**

### 🟠 SEC‑09 — Full stack traces exposed to the client (A05)

`<compilation debug="true">` is committed, and there is **no `<customErrors>` element at all** — so the default is `RemoteOnly`, which is safe for remote clients but shows full traces on localhost (which is the venue machine). `Views/Shared/Error.cshtml` exists but only renders when `HandleErrorAttribute` fires with custom errors on.
**Fix:** add `<customErrors mode="On" defaultRedirect="~/Error" />` and ensure `Web.Release.config` sets `debug="false"` (it already removes the attribute, which defaults to false — acceptable, but be explicit). **Effort: 1 hour.**

### 🔴 SEC‑10 — Silent data loss from unreached `TransactionScope.Complete()` (integrity)

Three confirmed sites where `using (TransactionScope tS = ...)` is entered and `Complete()` is unreachable on a branch → **implicit rollback on `Dispose`**:

| Site | Branch | Effect |
|---|---|---|
| `FirstRoundController.cs:1052` `PostMatchOneBuzzerAnswer` | `BuzzerPress == false` | `buzzer.StatusId = 1` rolls back → **the show loops on the same buzzer question forever** |
| `FinalRoundController.cs:1874` `PostMatchOneBuzzerAnswer` | same | same |
| `FinalRoundController.cs:1237` / `SecondRoundController.cs:915` `PostMatchOnePassingAnswer` | `PassStatus == 3` (all three teams passed) | `passingAllPass.StatusId = 1` rolls back → same loop |

Additionally, `MatchesController.SetMatches` has two `return RedirectToAction(...)` statements **inside** the `using (TransactionScope)` before `Complete()` — harmless today because nothing has been written at that point, but a landmine.
**Fix:** move `tS.Complete()` to the end of the `using` block on every path, or drop `TransactionScope` entirely for these single‑`SaveChanges()` operations (EF already wraps `SaveChanges` in a transaction). **Effort: 2 hours.**

### 🟠 SEC‑11 — Exceptions silently swallowed (A09)

`SecondRoundController.cs:463`:
```csharp
catch (Exception EX)
{
}
```
The only `catch` in 9,481 lines, and it discards a failed Audio‑Visual score write. The operator sees a normal screen; the team silently loses 10 marks. `EX` is also an unused variable (compiler warning CS0168).
**Fix:** log and re‑throw, or return a failure envelope the client surfaces. **Effort: 1 hour.**

### 🟡 SEC‑12 — Mass assignment / over‑posting (A08)

`AddAudioVisualQuestions(Audio_Visual audio_visual, HttpPostedFileBase AVFile)` and `AddVisualRapidFireQuestions(VisualRapidFire visualRapidFire, …)` bind **the EF entity itself** from the request body. A crafted POST can set `Audio_VisualId`, `StatusId`, or any navigation FK. Every other module correctly binds a VM.
**Fix:** introduce `AudioVisualVM` (the file already exists — as an empty class, §5.2) and `VisualRapidFireVM`. **Effort: 2 hours.**

### 🟡 SEC‑13 — Source disclosure via stray `.cshtml` in `Content/` (A05)

§6.5. Three Razor files deployed outside the `Views/` `BlockViewHandler` guard.
**Fix:** delete. **Effort: 5 minutes.**

### 🟡 SEC‑14 — Unbounded request size (A05)

`maxRequestLength="1048576"` (1 GB) in `<httpRuntime>`. IIS's `maxAllowedContentLength="41943040"` (40 MB) is the effective cap, but the mismatch means the ASP.NET‑level guard is inert. A 40 MB upload per request, unauthenticated, is a trivial disk‑fill / DoS vector.
**Fix:** align both to a realistic ceiling (e.g. 20 MB) and enforce per‑file size in code. **Effort: 30 minutes.**

### 🔵 SEC‑15 — Mixed‑content / third‑party font CDN over HTTP

`Views/Matches/Selection.cshtml`, `Views/Landing/Index.cshtml` and others load
`<link href="http://fonts.googleapis.com/css?family=Roboto+Condensed" …>` over **plain HTTP**.
**Fix:** `https://`, or self‑host (the venue may be offline anyway — a missing font is a live‑show risk in itself). **Effort: 15 minutes.**

### 🔵 SEC‑16 — Outdated framework and libraries

.NET Framework 4.6.1 (out of support), ASP.NET MVC 4 (2012), EF 5 (2012), AngularJS 1.x (EOL Jan 2022), Bootstrap 3.4.1 (EOL), jQuery 1.8.1/1.9.1/1.12.4 (known XSS CVEs, shipped though 3.7.1 is the one bundled), TinyMCE 4.7.0 (2017). No dependency scanning.
**Fix:** long‑term migration (§17 Phase 5). Short term, delete the unshipped old jQuery copies.

### 🔵 SEC‑17 — Missing security headers

No `X-Content-Type-Options`, `X-Frame-Options`, `Content-Security-Policy`, `Referrer-Policy`, `Strict-Transport-Security`. No HTTPS enforcement (`IISUrl` is `http://localhost:2028/`).
**Fix:** add `<httpProtocol><customHeaders>` to `Web.config`. **Effort: 1 hour.**

### ✅ What is done correctly

- **No SQL injection.** All 115 raw SQL calls use the identical parameterized form `db.Database.SqlQuery<ScoreVMTotal>("exec spGetTotalScoreNew @SchoolsTeamId,@MatchesId,@RoundsId", p1, p2, p3)`. Zero string concatenation or interpolation into SQL anywhere.
- **No path traversal in uploads.** `Path.GetFileName()` strips directory components before `Path.Combine`.
- **No hardcoded API keys, passwords, or tokens** anywhere in the codebase.
- **No dangerous deserialization**, no `BinaryFormatter`, no `eval`.
- **Correct use of `TransactionScope`** on the majority of write paths (the 3 exceptions are SEC‑10).

---

## 11. Testing Gaps

### 11.1 Current state

**There is no test code in this solution.**

| Check | Result |
|---|---|
| Test projects in `QuizApp.sln` | **0** (solution contains `QuizApp` + `QuizApp.Database` only) |
| Files matching `*Test*.cs` / `*Spec*.cs` | **0** |
| `[TestMethod]` / `[Fact]` / `[Test]` attributes | **0** |
| Test framework packages in `packages.config` | **0** (no MSTest, xUnit, NUnit) |
| Mocking libraries | **0** (no Moq, NSubstitute, FakeItEasy) |
| E2E / UI automation | **0** (no Selenium, Playwright, Cypress) |
| Test data builders / fixtures | **0** |
| CI configuration | **0** (no `.github/`, `azure-pipelines.yml`, `.gitlab-ci.yml`) |
| Code coverage tooling | **0** |

Coverage is therefore **0 %** across 9,481 controller LOC and 40,881 view LOC.

### 11.2 Why this is worse than usual here

This is a **live, one‑shot, unrepeatable event**. A scoring defect discovered mid‑show cannot be hot‑fixed and cannot be rolled back — the audience and the competing schools have already seen the score. The three silent‑rollback bugs (SEC‑10) are exactly the class of defect a single unit test would have caught, and they sit on the least‑exercised branches (nobody pressed the buzzer / all three teams passed) — i.e. precisely the paths that only appear under real competition pressure.

### 11.3 Critical functionality with zero coverage

Ranked by "cost of being wrong during a live final":

| # | Behaviour | Where | Risk if wrong |
|---|---|---|---|
| 1 | Mark calculation for all 9 round types | `Contants` + 17 `Post*Answer` methods | **Wrong winner** |
| 2 | `spGetTotalScoreNew` aggregation across 9 answer tables | SP | **Wrong winner** |
| 3 | Question progression (`StatusId 0→1`, `OrderBy(QuestionNumber).Take(1)`) | 17 methods | Show loops / skips (this bug is live — SEC‑10) |
| 4 | Active‑team rotation (`QuestionNumber % 3`) | 17 methods | Wrong team is asked. **Note the inconsistency:** `FirstRoundController` maps `% 3 == 0 → team 3` via the `default:` label, while `FinalRoundController.OnChoiceLoad:2085` first does `if (activeTeam == 0) activeTeam = 3;`. Same intent, two implementations — untested |
| 5 | Buzzer tri‑state marking (+20 / −15 / −15) | `PostMatchOneBuzzerAnswer` | Wrong score; **contains a live bug** |
| 6 | Passing pass‑chain marking (+15 / +10 / −10 / all‑pass) | `PostMatchOnePassingAnswer` | Wrong score; **contains a live bug** |
| 7 | Choice topic limits (6 semi‑final / 9 final) | `Contants` + 4 sites | Round ends early or never ends |
| 8 | Excel import validation & skip logic | 10 `UploadExcel` methods | Corrupt question bank |
| 9 | Team/match setup limits (18 teams; 6/3/1 matches; 3 teams per match) | `HomeController`, `MatchesController` | Unrunnable tournament |
| 10 | `RoundOneTeam` 18‑element indexing | `LandingController.cs:112` | **Unhandled exception on the landing screen** if setup is incomplete |
| 11 | Null handling when the question bank is exhausted | every `GetMatch*` | NRE on the projector |

### 11.4 Untested error scenarios and edge cases

- Question bank empty for a match → `mcq.MCQs` NRE (`GetMatchOneMultipleChoice:126`)
- `spGetTotalScoreNew` returns no rows → `.First()` throws (`ScoreController` ×3, all gameplay `On*Load`)
- Fewer than 18 / 9 / 3 teams assigned → `IndexOutOfRangeException` (`LandingController`)
- `Edit(int id = 0)` with the default → `Find(0)` null → NRE (7 controllers)
- Excel column missing → `row["SchoolName"]` `ArgumentException`
- Excel cell non‑numeric → `Convert.ToInt32` `FormatException`
- Duplicate `TieBreaker.QuestionNumber` → `SingleOrDefault` `InvalidOperationException`
- Double keypress → duplicate score row (no idempotency)
- Empty team selection on `SetMatches` → `foreach` over null `List<int>` NRE
- Zero or multiple `OptionStatus = 1` per question → silent mis‑scoring

### 11.5 Recommended test strategy

Do **not** aim for "80 % coverage". Aim for **the scoring path, exhaustively**.

**Phase A — pure logic (no DB, ~1 day, highest value)**
Extract mark calculation into `IScoringService` and table‑test every branch:
```csharp
[Theory]
[InlineData(BuzzerOutcome.Right,     true,  20)]
[InlineData(BuzzerOutcome.Wrong,     true, -15)]
[InlineData(BuzzerOutcome.NoAnswer,  true, -15)]
[InlineData(BuzzerOutcome.Right,     false,  0)]   // no buzzer press → no row
public void BuzzerMarks(BuzzerOutcome o, bool pressed, int expected) => …
```
Also cover: `QuestionNumber % 3` team rotation, Choice topic limits, Passing pass‑chain.

**Phase B — integration against LocalDB (~3 days)**
`EffortSqlServer`/Respawn or a per‑test transaction. Cover: full question progression for one match end‑to‑end; `spGetTotalScoreNew` correctness against hand‑computed totals; the three SEC‑10 rollback branches; `spReset` idempotency.

**Phase C — controller tests (~2 days)**
Requires DI (§4.3). Assert that `Post*Answer` **ignores** a client‑supplied `Answer` once SEC‑03 is fixed; assert `[Authorize]` is enforced; assert upload rejection of `.aspx`.

**Phase D — smoke E2E (~2 days)**
Playwright script that drives one complete Round‑1 match by keypress and asserts the final scoreboard. This is the test that would give real confidence before an event.

**Mocking strategy:** avoid mocking `DbContext` (EF 5 makes this painful and the mocks lie). Use a real LocalDB with a transaction rollback per test. Mock only `IFileStorage` (the upload abstraction to be introduced) and `IClock`.

---

## 12. Performance & Scalability Findings

Context: current load is **one operator, one browser, ~18 teams, a few hundred questions**. Almost nothing here is a *present* problem. The distinction below is deliberate.

### 12.1 🟠 `spGetTotalScoreNew` executed three times per request, 114 call sites — *matters now, mildly*

Every `On*Load` and every `Post*Answer` runs the aggregation proc once per team, sequentially, synchronously. Each execution scans nine unindexed answer tables.

- **Per gameplay request:** 3 executions ≈ 9 × 3 = 27 table scans
- **Per Round‑1 scoreboard (`ScoreController.RoundOneScore`):** 18 teams → **18 executions ≈ 162 table scans**, all in a `foreach`

**Why it matters now:** the scoreboard is displayed on the projector between rounds; a visible delay is a production value issue.
**Fix:** rewrite as `spGetMatchScores @RoundsId, @MatchesId` returning one row per team; call once. Add the §9.2 indexes. **~40× fewer round trips.**

### 12.2 🟠 Classic N+1 in all 8 `Get{X}QuestionList` endpoints — *matters now*

```csharp
// MCQController.cs:26
var mcq = (from mcqList in db.MCQs.ToList()          // ← 1 query, full materialization
           select new {
               …,
               OptionsList = (from o in db.MCQ_Option // ← 1 query PER ROW
                              where o.MCQId == mcqList.MCQId
                              select …).ToList()
           }).ToList();
```
The `.ToList()` on `db.MCQs` forces client evaluation, so the option query cannot be translated or joined. 200 questions → **201 round trips**.
**Fix:**
```csharp
var mcq = db.MCQs
    .Select(m => new { m.MCQId, Question = m.MCQs, m.RoundsId, m.MatchesId,
                       m.QuestionNumber, m.StatusId,
                       OptionsList = m.MCQ_Option.Select(o => new { o.Options, o.OptionStatus }) })
    .ToList();     // one query, one join
```
Same fix in `AudioVisualController.cs:24` (which does `db.Audio_Visual.ToList()` before projecting — pure waste).

### 12.3 🟠 `SaveChanges()` inside every import loop — *matters now*

All 10 `UploadExcel` methods call `db.SaveChanges()` once per row. A 200‑row file = **200 transactions**, plus the per‑row validation `SELECT`s (`db.Matches.Any(...)`, `db.Rounds.Any(...)`, `db.Audio_Visual.Any(...)`) → ~800 round trips for a 200‑row import.
Worse, there is **no transaction around the loop** — a `FormatException` on row 150 leaves 149 rows committed and no rollback.
**Fix:** validate the whole file first, `AddRange`, single `SaveChanges()` inside one `TransactionScope`, `db.Configuration.AutoDetectChangesEnabled = false` during the batch.

### 12.4 🟡 Payload size on gameplay views — *matters now for load time*

`Views/FirstRound/MatchOneBuzzer.cshtml` is **52,772 bytes** of inline HTML+CSS+JS, none of it cached (it is a Razor view, not a static asset). Nine such buzzer views exist. Every screen transition is a full page load re‑downloading ~50 KB of near‑identical script.
**Fix:** §4.1 + extract the shared JS to a cacheable `quiz-runner.js`.

### 12.5 🟡 `DbContext` never disposed — *matters at any scale*

`BRFQuizEntities db = new BRFQuizEntities();` as a field in all 16 controllers, and `protected override void Dispose(bool)` appears **0 times**. Connections return to the pool only when the finalizer runs.
**Fix:** add the override (10 minutes, 16 files).

### 12.6 🟡 Repeated computations — *minor*

- `.ToList().Count` instead of `.Count()` (`GetMatchOneMultipleChoice:127` and siblings) — materializes rows to count them
- `.ToList().Select(x => x.Marks).LastOrDefault()` (`ScoreController.FormOne/Two/Three`) — materializes all rows to read one field
- `ScoreController.LeagueRoundScore` computes `sortedTeams` (a full sort with rank assignment) and then **never uses it** — it returns `sortedTeamsScore` instead. Pure waste, and a bug‑shaped one: the two lists rank differently (`sortedTeams` includes zero‑score teams)
- `Contants.GetStatusList()` rebuilds a 4‑item list on every call, 18 call sites — should be a `static readonly` array

### 12.7 🔵 No caching anywhere — *will matter if multi‑user*

`Rounds` (3 rows) and `Matches` (6 rows) are re‑queried on every `Add*Questions` GET, every `Edit` GET, and every `SetMatches` GET — `db.Rounds` and `db.Matches` are each hit **33 times** across controllers, always with `.ToList()` for a `SelectList`. These are immutable lookups.
**Fix:** `MemoryCache` or a static readonly snapshot loaded at `Application_Start`.

### 12.8 🔵 Front‑end rendering — *minor*

- Two jQuery instances loaded on any page that shows a toast (`_Layout` bundle 3.7.1 + `_Notification` 1.12.4)
- AngularJS `dirPagination` renders 5 rows per page client‑side from a fully materialized list — fine at 200 questions, poor at 5,000
- `resizeText()` runs a synchronous measure/resize loop per question render
- No lazy loading of AV media

### 12.9 Scalability ceiling

| Constraint | Blocks |
|---|---|
| `sessionState mode="InProc"` | Web farm / multiple instances |
| No optimistic concurrency (`rowversion` absent) | A second operator console |
| Hardcoded `matchId` in views | More than 6/3/1 matches |
| 18 hardcoded properties in `RoundOneTeam` | More than 18 teams |
| `spReset` truncates globally | Concurrent events on one database |
| LocalDB | Any deployment off the operator's machine |

**None of these matter for the app as scoped.** They are listed so that a decision to extend the app (a second venue, a second control station, a public leaderboard) is taken with eyes open.

---

## 13. Technical Debt

### 13.1 Duplication — the dominant debt

| Duplicated unit | Copies | Approx. lines |
|---|---|---|
| "Load 3 teams + call `spGetTotalScoreNew` ×3" region | **114** | ~6,800 |
| `return View()` gameplay actions | 50 | ~200 |
| Gameplay `.cshtml` differing only in `matchId` | 41 | **~28,000** |
| `On{X}Load` / `GetMatch{X}` / `Post{X}Answer` families across 3 round controllers | ~13 families × 2–3 | ~3,500 |
| `XxxController` CRUD (Index / GetList / UploadExcel / Add / Edit) | 8 near‑identical controllers | ~2,700 |
| `XxxVM` vs `EditXxxVM` | 7 pairs | ~350 |
| `XxxOptions` DTOs (`{OptionId, Options, Status}`) | 6 identical | ~40 |
| `XxxAnswerParameterVM` DTOs | 6 near‑identical | ~120 |
| `ScoreController.RoundOne/Two/ThreeScore` | 3 identical but for `int round` | ~135 |
| `ScoreController.FormOne/FormTwo` | 2 byte‑identical | ~26 |

**Conservative estimate: ~60 % of the ~50,000‑line codebase is copy‑paste.**

### 13.2 SOLID violations

| Principle | Violation |
|---|---|
| **S**ingle Responsibility | `FinalRoundController` (2,474 lines) handles HTTP routing, request validation, 8 different round rule‑sets, EF querying, raw ADO.NET, and view‑model assembly |
| **O**pen/Closed | Adding a 10th round type means editing `Contants`, the EDMX, `spGetTotalScoreNew`, 3 round controllers, and creating N views. Nothing is extensible |
| **L**iskov | Not applicable — **there is not a single interface or abstract class in the project** |
| **I**nterface Segregation | Not applicable — no interfaces |
| **D**ependency Inversion | Every controller depends on the concrete `BRFQuizEntities`, constructed with `new` in a field initializer. **Zero abstractions, zero injection points** |

### 13.3 DRY violations

Beyond §13.1: the same 60‑line team‑loading block, the same 4‑case `switch(i)` option unpacker (in **every** `GetMatch*`, 14 copies), the same `if (correctOption == lockValue)` JS grader in 41 views, the same Excel‑upload skeleton in 10 methods.

### 13.4 Naming

| Issue | Detail |
|---|---|
| `Contants.cs` | **Misspelling of "Constants"** — in the class name, file name, and 40+ call sites |
| `BazmeRekhta.Controllers` | `HomeController.cs:12` declares a **different namespace** from every other controller (`QuizApp.Controllers`) |
| `Card` ↔ "QuickBuzz" | The entity, table, controller and views say `Card`; the UI label in `_Layout.cshtml:57` says "QuickBuzz"; the sibling .NET 10 project is named `QuickBuzz` |
| `instantNoticeToStudentListModule` | The AngularJS module name in **`MCQ/Index.cshtml`** (and siblings) — copied verbatim from a *school notice board* application. Nothing to do with quizzes |
| `MatchOne*` endpoints serving all matches | §8.4 #2 |
| `MCQ.MCQs` | A single question stored in a plural column |
| `TieBreaker.Status` | The only `Status` among 32 `StatusId` |
| `selctedOptionId` | Typo, 41 views |
| `Card_Answers.MatchId` vs `MatchesId` | Two conventions in one schema |
| `spGetTotalScoreNew` | "New" in a permanent name; the "old" one is dead |

### 13.5 Tight coupling

- Views know the database's match IDs (`var matchId = 1;`)
- Views know the scoring rules (they decide correctness)
- Controllers know the raw SQL text of a stored procedure, 114 times
- `_Layout.cshtml` hardcodes the full admin nav; adding a module means editing the layout
- `Contants.strAudioVisual_File = "../Content/BRFSoftware/images/AV"` — a **relative** path used with `Server.MapPath`, coupling the storage location to the URL structure

### 13.6 Over‑ and under‑engineering

**Over‑engineered:** Web API 2 registered with no API controllers; `TransactionScope` (a distributed‑transaction primitive) wrapped around single `SaveChanges()` calls; Forms auth + Membership + Role + Profile providers configured for an app with no users; a `Staging` build configuration with no transform.

**Under‑engineered:** no service layer; no DI; no logging; no tests; no error handling; no input abstraction; no migrations; no seed data; no documentation.

### 13.7 Debt inventory summary

| Category | Items | Rough remediation |
|---|---|---|
| Duplicated view files | 41 | 5 days |
| Duplicated controller regions | 114 | 4 days |
| Dead code (types, files, assets, SPs) | ~25 | 1 day |
| Missing tests | everything | 8 days |
| Missing auth/authz | all endpoints | 1 day |
| Security fixes (SEC‑01…17) | 17 | 6 days |
| Naming / consistency | ~12 classes of issue | 2 days |
| DB indexes + constraints | ~30 objects | 1 day |
| Framework modernization | whole stack | 20+ days |

---

## 14. Recommended Refactoring

Ordered so that each step is independently shippable and each unlocks the next.

### R1 — Add a `Dispose` override to all 16 controllers *(30 min, zero risk)*
```csharp
protected override void Dispose(bool disposing)
{
    if (disposing) db.Dispose();
    base.Dispose(disposing);
}
```

### R2 — Extract `Contants` → `Constants`, `ScoringRules`, `MediaPaths` *(2 h)*
Fix the spelling, split the concerns, move `OptionList` out, make `GetStatusList()` a `static readonly` array. Mechanical rename across ~40 sites.

### R3 — Extract `IScoringService` *(2 days) — the highest‑leverage change*
```csharp
public interface IScoringService
{
    int MarksFor(QuestionType type, AnswerOutcome outcome, PassStatus? pass = null);
    PlayingTeamsVM BuildPlayingTeams(int roundsId, int matchesId);
    int TotalFor(int schoolsTeamId, int matchesId, int roundsId);
}
```
`BuildPlayingTeams` alone replaces **114 copies** of the same 60‑line block and is the single place to later batch the stored‑proc call (§12.1). `MarksFor` is pure and immediately unit‑testable (§11.5 Phase A). This also creates the seam that makes R4 and all of §11 possible.

### R4 — Introduce DI and `IQuizContext` *(1 day)*
Constructor‑inject the context. Any lightweight container works (Unity, Autofac, or a hand‑rolled `IDependencyResolver` — MVC 4 supports all three).

### R5 — Fix the correctness bugs *(1 day)*
The three `tS.Complete()` sites (SEC‑10), the empty `catch` (SEC‑11), `Selection.cshtml`'s `MatchOne` link (§6.3), the three throwing `Views/Matches/*.cshtml` (§6.2), the `LeagueRoundScore` dead variable, the null guards in every `GetMatch*` and `ScoreController`.

### R6 — Move grading to the server *(2 days)*
§3.2 across 17 endpoints. Strip `CorrectOption` / `Answer` / `RightOption` from all pre‑answer payloads. Depends on R3.

### R7 — Add authentication and CSRF *(1.5 days)*
§3.1 + SEC‑05 + SEC‑06 (`[HttpPost]` on all mutators).

### R8 — Harden file upload *(0.5 day)*
SEC‑04. Introduce `IFileStorage` so the path coupling in §13.5 goes away and uploads become mockable.

### R9 — Delete dead code *(1 day)*
The 10 unused types (§5.3), `spGetTotalScore` + `spGetTotalScore_Result`, `QuizCategory` + `Rounds_QuizCategory`, the 3 stray `Content/.../Score/*.cshtml`, the 7 unused JS libraries, 5 redundant jQuery copies, the Web API packages, the Membership/Role/Profile config blocks, the 132 commented‑out lines. **Removes ~700 KB and ~1,500 lines with zero behaviour change.**

### R10 — Collapse the 41 gameplay views into 4 *(5 days) — the biggest win**
§4.1. One `Play(round, match, stage)` action + 4 parameterized views + one shared `quiz-runner.js` carrying the state machine. **Deletes ~28,000 lines of Razor.** Depends on R3 (so the views stop computing scores) and R6 (so they stop grading).

### R11 — Standardize the answer tables and add indexes/constraints *(2 days)*
§4.5, §9.2, §9.3, §9.4. Add `MatchesId`/`RoundsId`/question FK to all nine `*_Answers` tables; add the 18 indexes; add the unique constraints; add the missing `TieBreakerOption` and `Audio_Visual_Answers` FKs. Simplifies `spGetTotalScoreNew` and enables R12.

### R12 — Batch the score aggregation *(1 day)*
Replace `spGetTotalScoreNew` (per team) with `spGetMatchScores` (per match). Depends on R3 + R11.

### R13 — Complete the missing features *(4 days)*
Rapid Fire question bank (§3.3), Tie‑Breaker scoring (§3.4), Delete actions (§3.5), AV/VRF Edit (§3.6), reset endpoint (§3.7), score‑adjustment facility replacing `FormOne/Two/Three` (§3.8, §6.4).

### R14 — Add tests *(8 days, staged alongside R3–R13)*
§11.5 Phases A→D.

### R15 — Logging, error handling, config, deployment *(3 days)*
§3.9, SEC‑08, SEC‑09, SEC‑14, SEC‑17, seed script, CI pipeline.

---

## 15. Prioritized Action Plan

### P0 — Before the next live event (must not ship without)

1. Fix the three unreachable `tS.Complete()` calls — the show currently loops on the buzzer/all‑pass paths *(SEC‑10)*
2. Fix the empty `catch` swallowing AV scores *(SEC‑11)*
3. Add authentication + `[Authorize]` *(SEC‑01)*
4. Add `[HttpPost]` to all 27 mutating actions *(SEC‑06)*
5. Add the upload extension allow‑list *(SEC‑04)*
6. Move grading server‑side and stop shipping the answer key *(SEC‑02, SEC‑03)*
7. Fix `Selection.cshtml`'s broken R1M1 link — Round 1 Match 1 is unreachable *(§6.3)*
8. Fix or delete the three `Views/Matches/*.cshtml` that return HTTP 500 *(§6.2)*
9. Add null guards to every `GetMatch*` and to `ScoreController`'s `.First()` calls *(§11.4)*
10. Add a guard to `LandingController.RoundOneTeam` for fewer than 18 teams *(§5.2)*

### P1 — Next sprint

11. `[ValidateAntiForgeryToken]` everywhere *(SEC‑05)*
12. Wire up the Rapid Fire question bank *(§3.3)*
13. Persist Tie‑Breaker results *(§3.4)*
14. Add an authenticated reset endpoint *(§3.7)*
15. Extract `IScoringService` *(R3)*
16. Add `Dispose` overrides *(R1)*
17. Add the 18 indexes and the missing FKs/unique constraints *(R11 partial)*
18. Sanitize question HTML; replace class‑level `[ValidateInput(false)]` *(SEC‑07)*
19. Add logging + global exception filter + `<customErrors>` *(§3.9, SEC‑09)*
20. Add Phase‑A unit tests for all scoring rules *(§11.5)*
21. Delete the three stray `Content/.../Score/*.cshtml` *(SEC‑13)*

### P2 — Next quarter

22. Collapse the 41 gameplay views into 4 *(R10)*
23. Introduce DI *(R4)*
24. Delete all dead code and unused libraries *(R9)*
25. Add Delete actions and the missing Edit actions *(§3.5, §3.6)*
26. Replace `FormOne/Two/Three` with an audited adjustment facility *(§6.4)*
27. Standardize the answer tables *(R11)*
28. Batch score aggregation *(R12)*
29. Fix N+1 in the list endpoints and the import loops *(§12.2, §12.3)*
30. Phase‑B/C tests *(§11.5)*
31. Connection string out of source control; `Web.Staging.config` *(SEC‑08)*
32. Security headers; HTTPS *(SEC‑17)*
33. Seed script + CI pipeline *(§9.8, R15)*

### P3 — Backlog

34. Merge the duplicate VMs *(§5.3)*
35. Fix the naming inconsistencies *(§13.4)*
36. Consolidate the front‑end dependency stack *(§4.6)*
37. Phase‑D E2E smoke test *(§11.5)*
38. Add timestamps/audit columns *(§9.5)*
39. Real‑time projector sync / SignalR; converge with the `QuickBuzz` .NET 10 project *(§3.12)*
40. Migrate to .NET 8+ / ASP.NET Core, EF Core *(SEC‑16)*

---

## 16. Priority Matrix

| # | Issue | Category | Evidence | Impact | Effort | Priority | Recommended Action |
|---|---|---|---|---|---|---|---|
| 1 | `TransactionScope.Complete()` unreachable on 3 branches → silent rollback | Correctness | `FirstRoundController.cs:1052`, `FinalRoundController.cs:1874`, `FinalRoundController.cs:1237`, `SecondRoundController.cs:915` | Show loops forever on a question; score lost | 2 h | **P0** | Move `tS.Complete()` to the end of every `using` block |
| 2 | Scoring decided in the browser, trusted by the server | Security / Integrity | `MatchOneMCQ.cshtml` (`if (correctOption == lockValue)`) → `FirstRoundController.cs:195` (`if (mcqAnswer.Answer)`); 17 endpoints | Any POST forges the winner | 2 d | **P0** | Re‑derive correctness from `Option.OptionStatus` server‑side |
| 3 | Zero authentication / authorization | Security (A01/A07) | `[Authorize]` count = 0; `~/Account/Login` 404s; `Web.config:33` | Anyone on the LAN mutates scores & question bank | 1 d | **P0** | `AccountController` + global `AuthorizeAttribute` + `[AllowAnonymous]` on display routes |
| 4 | Unrestricted file upload to a served directory | Security (A03/A05) | `AudioVisualController.cs:127`, `VisualRapidFireController.cs:114` | Unauthenticated RCE via `.aspx` | 4 h | **P0** | Extension allow‑list + GUID filename + handler lockdown on the folder |
| 5 | Answer key returned to the client pre‑answer | Security (A04) | `CorrectOption` in 6 `GetMatch*`; `OptionStatus` in 8 `Get*QuestionList` | Answer key readable by anyone | 1 d | **P0** | Strip correctness from pre‑answer payloads; return it in the Post response |
| 6 | 27 state‑mutating actions reachable by GET | Security (A01) | Only 9 `[HttpPost]` in the solution; `Score/FormTwo`, `FormThree`, all `UploadExcel` | Drive‑by score mutation | 2 h | **P0** | Add `[HttpPost]` to every mutator |
| 7 | Empty `catch` swallows AV score failures | Correctness | `SecondRoundController.cs:463` | Team silently loses 10 marks | 1 h | **P0** | Log + surface the failure |
| 8 | `Views/Matches/{First,Second,Final}Round.cshtml` throw HTTP 500 | Correctness | `@section TopNavigations` defined; `_Layout.cshtml` renders only `AngularScripts`/`scripts` | 3 linked pages are dead | 30 m | **P0** | Delete the views + actions, or add `@RenderSection("TopNavigations", required:false)` |
| 9 | Round 1 / Match 1 unreachable | Correctness | `Selection.cshtml:47` → `Url.Action("MatchOne","FirstRound")`; no such action | Operator cannot start M1 from the picker | 5 m | **P0** | Change to `"MatchOneMCQ"` |
| 10 | Unguarded nulls in every `GetMatch*` and `ScoreController` | Reliability | `mcq.MCQs` after `FirstOrDefault()`; `.First()` on `SqlQuery` ×3 | Yellow screen on the projector | 4 h | **P0** | Null checks + graceful "round complete" response |
| 11 | `RoundOneTeam` indexes `[0..17]` with no guard | Reliability | `LandingController.cs:112–130` | `IndexOutOfRangeException` if setup incomplete | 1 h | **P0** | Guard `Count < 18`; replace 18 properties with a `List<string>` |
| 12 | No CSRF validation (tokens issued, never checked) | Security (A01) | `ValidateAntiForgeryToken` = 0; `@Html.AntiForgeryToken()` × 17 | Cross‑site score mutation | 4 h | **P1** | `[ValidateAntiForgeryToken]` on all POSTs |
| 13 | Rapid Fire has no question bank wired in | Missing feature | `db.RapidFires` referenced 0×; no `RapidFireController` | Round is unmanaged; no audit trail | 2 d | **P1** | Add `RapidFireController` + `GetMatchRapidFire` + `RapidFireId` FK |
| 14 | Tie‑Breaker records no result | Missing feature | No `TieBreaker_Answers` table; `TieBreakerAnswer` has no `SchoolsTeamId`; `PostTieBreakerAnswer:486` | Ties cannot be resolved by the system | 1 d | **P1** | Create the answers table; add team/match/round; persist and display |
| 15 | No reset endpoint (`spReset` uncallable) | Missing feature | `spReset` referenced 0× | Manual SSMS work under show pressure | 4 h | **P1** | Authenticated `Admin/Reset` + per‑match `spResetMatch` |
| 16 | 114 copies of the team+score block | Maintainability | `spGetTotalScoreNew` × 51/36/24/4 by file | Any scoring change is a 114‑site edit | 2 d | **P1** | Extract `IScoringService.BuildPlayingTeams` |
| 17 | Zero indexes beyond PKs | Performance / DB | No `CREATE INDEX` in `QuizApp.Database` | Table scans on every question advance | 1 d | **P1** | Add the 18 indexes in §9.2 |
| 18 | `TieBreakerOption` has no FK to `TieBreaker` | Data integrity | `TieBreakerOption.sql` — PK only, yet the entity has a nav property | Orphan options; EDMX/DB divergence | 1 h | **P1** | Add the FK constraint |
| 19 | No unique constraint on `(Round, Match, QuestionNumber)` | Data integrity | No `UNIQUE` in any table script; `SingleOrDefault` at `TieBreakerController.cs:495` | Duplicate questions; `InvalidOperationException` | 4 h | **P1** | Add unique constraints across the 9 question tables |
| 20 | Class‑level `[ValidateInput(false)]` × 11 + stored HTML | Security (A03) | 11 controllers; `Views/Web.config` `validateRequest="false"` | Stored XSS on the projector | 1 d | **P1** | Property‑level `[AllowHtml]` + HtmlSanitizer |
| 21 | `DbContext` never disposed | Resource leak | `Dispose` override count = 0 | Connection‑pool pressure | 30 m | **P1** | Add the override to 16 controllers |
| 22 | No logging, one empty `catch` in 9,481 LOC | Observability (A09) | No Serilog/NLog/log4net; no `<customErrors>` | Post‑mortem impossible | 1 d | **P1** | Serilog + global `IExceptionFilter` + `<customErrors>` |
| 23 | Zero tests | Quality | 0 test projects, 0 test attributes, 0 frameworks | Scoring defects reach a live, unrepeatable event | 8 d | **P1** | §11.5 Phases A→D |
| 24 | Stray `.cshtml` deployed under `Content/` | Security (A05) | 3 files in `Content/BRFSoftware/images/Score/`, all `<Content Include>` in the csproj | Source disclosure | 5 m | **P1** | Delete files + csproj entries |
| 25 | 41 gameplay views differing only in `matchId` | Maintainability | `MatchOneMCQ` vs `MatchTwoMCQ`: 71/950 lines differ | ~28,000 lines of copy‑paste; every UI fix × 41 | 5 d | **P2** | `Play(round, match, stage)` + 4 parameterized views |
| 26 | 50 `return View()` actions | Maintainability | `FirstRoundController.cs:1193–1320` (24), `SecondRound` (17), `FinalRound` (9) | Same | (in #25) | **P2** | Collapse with #25 |
| 27 | 10 completely unused types | Dead code | `QuizCategory`, `Rounds_QuizCategory`, `spGetTotalScore_Result`, `ScoreVM`, `ChoiceTopicVM`, `ChoiceStateVM`, + 4 empty VM classes — all 0 refs | Cognitive load; false schema | 4 h | **P2** | Delete types, DbSets, EDMX entries, tables |
| 28 | Dead `spGetTotalScore` (and it is *wrong*) | Dead code / risk | 0 refs; omits Card/VRF/Choice totals | Someone will call it | 30 m | **P2** | Drop the procedure |
| 29 | 700 KB of unused JS + 5 redundant jQuery copies | Dead code | knockout, datatables, loginModal, BannerCarousel, backtotop, materialcard, main.js — all 0 refs | Payload, confusion, CVE surface | 2 h | **P2** | Delete; consolidate on one jQuery |
| 30 | Web API registered with zero `ApiController`s | Dead code | `WebApiConfig.Register` called; no `ApiController` in solution | 5 dead packages | 1 h | **P2** | Remove, or adopt properly during the endpoint refactor |
| 31 | N+1 in all 8 `Get{X}QuestionList` | Performance | `db.MCQs.ToList()` then per‑row option query | 201 round trips at 200 questions | 4 h | **P2** | Server‑side projection with navigation properties |
| 32 | `SaveChanges()` per row in 10 importers, no transaction | Performance / Integrity | All `UploadExcel` methods | 200 transactions; partial commit on failure | 1 d | **P2** | Validate → `AddRange` → one `SaveChanges` in one transaction |
| 33 | No Delete anywhere; AV/VRF Edit missing | Missing feature | 0 `Delete` actions; `VisualRapidFire/Index.cshtml:20` → 404 | Bad data can only be removed in SSMS | 2 d | **P2** | Add soft‑delete + the two Edit actions |
| 34 | `Score/FormOne/Two/Three` magic +5 hack | Design / Security | `ScoreController.cs:16–50`; `FormTwo`≡`FormOne`; two are GET | Unaudited, unauthenticated score mutation | 1 d | **P2** | Replace with an audited `ScoreAdjustment` facility |
| 35 | Answer tables have 4 different shapes | Design / DB | `MatchId/RoundId` on 4 tables, derived on 5; 3 have no question link | Cannot audit; forces 9‑branch SP | 2 d | **P2** | Standardize: `MatchesId`, `RoundsId`, question FK on all 9 |
| 36 | `spGetTotalScoreNew` called per team | Performance | 18 executions per Round‑1 scoreboard | Visible projector delay | 1 d | **P2** | `spGetMatchScores` returning one row per team |
| 37 | Connection string + `debug="true"` in source control | Security (A05) | `Web.config`; `encrypt=False`, `trustservercertificate=True` | Config leakage; insecure transport | 4 h | **P2** | `configSource` + encryption + `Web.Staging.config` |
| 38 | No security headers, no HTTPS | Security (A05) | No `<customHeaders>`; `IISUrl=http://…` | Clickjacking, MIME sniffing | 1 h | **P2** | Add headers; enable HTTPS |
| 39 | No seed data for `Rounds`/`Matches` | DevOps | No post‑deployment script in the `.sqlproj` | A fresh clone yields a non‑functional DB | 2 h | **P2** | Idempotent `MERGE` post‑deployment script |
| 40 | No CI/CD, no build validation | DevOps | No pipeline files | Broken code reaches the venue | 1 d | **P2** | GitHub Actions / Azure Pipelines: build + test + SSDT publish |
| 41 | 7 duplicate `XxxVM`/`EditXxxVM` pairs; `TeamsVM`/`LeagueTeamsVM` | Maintainability | §5.2 | Double edits | 1 d | **P3** | Merge with nullable Id / add `Rank` |
| 42 | Naming: `Contants`, `BazmeRekhta.Controllers`, `instantNoticeToStudentListModule`, `MCQ.MCQs`, `TieBreaker.Status`, `selctedOptionId` | Readability | §13.4 | Confusion for new maintainers | 2 d | **P3** | Systematic rename |
| 43 | `PlayingTeamsVM` has 15 flattened properties + a `dynamic` | Design | `VM/TeamsVM.cs` | Runtime‑only errors in views | 1 d | **P3** | `List<TeamsVM>` + typed `ChoiceTopics` |
| 44 | Mixed‑content HTTP font CDN | Security (low) | `Selection.cshtml`, `Landing/Index.cshtml` | Mixed content; offline‑venue failure | 15 m | **P3** | HTTPS or self‑host |
| 45 | No timestamps / audit columns on any table | Auditability | No `CreatedAt` in 33 tables | Disputes cannot be reconstructed | 1 d | **P3** | Add `CreatedAtUtc` to all `*_Answers` |
| 46 | `9AMM_AfterProgram.sql` (334 KB) loose at repo root | Hygiene | Not referenced by either project; contains real event data | Repo bloat; stale data confusion | 15 m | **P3** | Move to `db/snapshots/` or remove from VCS |
| 47 | .NET 4.6.1 / MVC 4 / EF 5 / AngularJS 1.x — all EOL | Modernization | `csproj`, `packages.config` | No security patches | 20+ d | **P3** | Plan migration to .NET 8+/ASP.NET Core; converge with `QuickBuzz` |
| 48 | 132 lines of commented‑out code | Hygiene | `TieBreakerController.cs:254–355` (MCQ leftovers); VRF validation at `:497–503` | Misleading; hides a real behaviour gap | 2 h | **P3** | Delete; restore the VRF duplicate check |
| 49 | No optimistic concurrency (`rowversion` absent) | Scalability | No concurrency token on any table | Blocks a second operator console | 1 d | **P3** | Add `rowversion` when multi‑station is required |
| 50 | No documentation (`README`, runbook, ERD) | Knowledge | Repo has no `.md` files | Bus factor of 1 | 2 d | **P3** | README, operator runbook, keyboard‑shortcut reference, ERD |

---

## 17. Implementation Roadmap

Sequenced by dependency and business impact. Effort assumes one experienced .NET developer.

### Phase 0 — Emergency hardening (3–4 days) · *before the next live event*

> Goal: the app cannot lose a score, cannot loop, cannot be tampered with, and does not crash on the projector.

- Matrix items **1, 7, 8, 9, 10, 11** — correctness and crash fixes
- Matrix items **3, 4, 6** — auth, upload, verb constraints
- Matrix items **2, 5** — server‑side grading, stop leaking the answer key
- **Exit criterion:** a full dry‑run of one Round‑1 match, one Round‑2 match and the Final, including the "nobody buzzed" and "all three passed" paths, completes without a loop, a crash, or a lost score.

**Dependency note:** item 2 (server‑side grading) touches 17 endpoints and 41 views. If Phase 0 must be shorter, ship items 1, 7, 8, 9, 10, 11, 3, 4, 6 (≈ 1.5 days) and defer item 2/5 to Phase 1 — but *only* if the venue network is genuinely isolated.

### Phase 1 — Integrity and safety net (2 weeks)

> Goal: correctness is provable, failures are visible, and the missing rounds work.

- **Week 1:** `IScoringService` extraction (item 16) → unlocks everything downstream. Phase‑A unit tests for every scoring rule (item 23). CSRF (12). `Dispose` (21). Logging + exception filter + `<customErrors>` (22).
- **Week 2:** Rapid Fire wiring (13). Tie‑Breaker persistence (14). Reset endpoint (15). DB indexes, missing FKs, unique constraints (17, 18, 19). XSS sanitization (20). Delete the stray `Content/` views (24).
- **Exit criterion:** every mark‑calculation branch has a passing test; a deliberately corrupted question bank produces a logged error rather than a yellow screen.

### Phase 2 — The great collapse (3 weeks)

> Goal: cut the codebase roughly in half without changing behaviour.

- **Week 3:** DI (item 23/R4). Delete all dead code — types, SPs, JS, packages, commented code (27, 28, 29, 30, 48).
- **Weeks 4–5:** Collapse 41 gameplay views → 4 parameterized views + one shared `quiz-runner.js`; collapse 50 view actions → 1 `Play(round, match, stage)` (25, 26). This is the highest‑risk refactor in the plan and *must* follow Phase 1's test suite.
- **Exit criterion:** ~28,000 lines deleted; the Phase‑1 test suite and a manual dry‑run both still pass.

### Phase 3 — Data model and performance (2 weeks)

- Standardize the nine answer tables (35); batch score aggregation into `spGetMatchScores` (36); fix the N+1 list endpoints (31) and the import loops (32).
- Add Delete + the missing Edit actions (33); replace `FormOne/Two/Three` with an audited adjustment facility (34).
- Phase‑B/C integration and controller tests.
- **Exit criterion:** the Round‑1 scoreboard renders in a single query; a 500‑row Excel import is one transaction.

### Phase 4 — Production discipline (1 week)

- Secrets out of source control; `Web.Staging.config` (37). Security headers + HTTPS (38). Seed script (39). CI/CD with build + test + SSDT publish (40). Health check. Phase‑D E2E smoke test.
- README, operator runbook, keyboard‑shortcut reference, ERD (50).
- **Exit criterion:** a clean clone builds, seeds, tests and deploys with no manual steps.

### Phase 5 — Modernization (ongoing, 4–6 weeks)

- Merge the duplicate VMs (41); systematic renaming (42); `PlayingTeamsVM` refactor (43); audit columns (45); concurrency tokens (49).
- Evaluate migration to **.NET 8+ / ASP.NET Core / EF Core**, converging with the sibling **`QuickBuzz`** .NET 10 project — which already targets the modern stack and carries the physical‑buzzer (`System.IO.Ports`) integration this app lacks. The natural end state is one .NET 8+/10 solution where `QuizApp` becomes the show‑control web app and `QuickBuzz` supplies hardware input, joined by SignalR for real‑time projector sync (item 39/§3.12).

---

### Closing assessment

`QuizApp-9AMM` **works** — it has demonstrably run a real 18‑team tournament, and the `9AMM_AfterProgram.sql` dump proves it. That deserves to be said plainly before the criticism lands.

What it is not is *safe to run again unchanged*. Three of its bugs (the two `TransactionScope` rollback paths and the empty `catch`) will silently corrupt a live show, and they sit on branches that only fire under real competition conditions. Its scoring is decided by JavaScript that anyone can edit. Its answer key is served to anonymous callers. And there is not one test to tell you when any of that breaks.

The good news is that the fixes are cheap relative to the risk: **Phase 0 is three to four days and removes every catastrophic failure mode.** Everything after that is about making the next ten years of this application affordable rather than making the next show survivable.

---

*Report generated 2026‑09‑02. All findings verified against source; file paths and line numbers refer to the working tree at the time of audit. Confirmed defects (reproducible from the code) are distinguished throughout from architectural recommendations (matters of engineering judgement).*
