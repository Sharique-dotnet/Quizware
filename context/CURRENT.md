# CURRENT CONTEXT — Quizware

> **AI agents: read this file first.** It is the working state of this project as
> of the last checkpoint, written so you can continue without any chat history.
> Full resume procedure: `context/_meta/SPEC.md` §5.
>
> **Confidence tags** — `[FACT]` verified · `[DECIDED]` binding · `[ASSUMED]`
> unconfirmed, adopted to make progress · `[UNVERIFIED]` believed but unchecked ·
> `[OPEN]` unanswered. **Anything `[ASSUMED]` or `[UNVERIFIED]` is not
> established** — verify it, or say plainly that you are proceeding without.
> Where this file and the repository disagree, **the repository is right** and
> this file is stale; say so.

**Last updated:** 2026-09-10 · **Session:** S-2026-09-10-01 · **Saved by:** Claude Sonnet 5 (Claude Code)

---

## 1. What this project is

`[FACT]` A rebuild of a quiz-tournament system. **QuizApp-9AMM** (ASP.NET MVC 4,
in production) is being replaced by **Quizware** — ASP.NET Core Web API on .NET 10
with SQL Server, an Angular 22 front end later. **QuickBuzz**, the existing buzzer
app, becomes an optional module that the system must work without.

`[FACT]` The core problem being solved: the legacy system hardcodes the tournament.
A new database per event; turn order as `QuestionNumber % 3` in 30+ places; the
running order of question types baked into 108 views as redirect chains; one
`TieBreaker` table/screens completely disconnected from qualification logic. The
new design turns all of it into configuration data.

**Naming trap:** `Quizware` (new), `QuizApp-9AMM` (legacy MVC 4), and `QuickBuzz`
(legacy buzzer) are three different systems. Check `PROJECT.md` §Domain glossary
before editing, or you will change the wrong one. **Also note (corrected this
session):** the outer repo/context root is named `QuizApp`
(`C:\Sharique\Projects\Personal\QuizApp\`) even though the new system inside it
is `Quizware/` — don't confuse the repo root's name with the legacy
`QuizApp-9AMM/` subfolder; they are unrelated.

## 2. Current objective

`[FACT]` **Phase 8 — Question selection engine (`IQuestionSelector`) — is now
fully done, all 10 tasks (P8-01–P8-10), and committed** as `31d2f22` ("Add
question selection engine: pool building, repeat-policy exclusion, seeded
weighted draw with difficulty mix and topic spread, MatchQuestion reservation,
and the widen-then-fail fallback ladder"). `[FACT]` verified via `git log` —
`31d2f22` is HEAD.

`[FACT]` **Phase 7 and the Postman collection + 3 rule-handler bugfixes (T-018,
previously staged) are also now committed** — landed as `d2b16cc` ("Add Postman
collection, README, and robust upsert handling"), plus a chain of rename
commits (`bcf4c70`–`859813c`) that renamed every project to the final
`Quizware` naming, added Scalar for OpenAPI docs, and added README/LICENSE —
all committed by the user out-of-band before this session started. **This is
the fourth recurrence of the L-004 pattern** (user commits proposed work
without narrating it mid-conversation) — see `LESSONS.md` L-004.

**Delivered this session (S-2026-09-10-01):**
- **Phase 8 — Question selection engine**, all 10 tasks, `31d2f22`: pool
  building (format/language/topic-filter/owner-scope/approved-only — **no tag
  filter**, D-026), repeat-policy exclusion via `QuestionUsageHistory`,
  difficulty-mix splitting from `DifficultyMixJson` (a format invented this
  session, D-030 — flag before Angular consumes it, Q-006), a seeded weighted
  draw favoring lower `TimesUsed` with a deterministic stable sort (D-032),
  topic-spread policy, option-order shuffling computed at reservation but not
  persisted until Phase 9's `Activate()` (D-025), reservation into
  `MatchQuestion` with **cross-match locking** so a reserved-but-not-yet-used
  question can't be drawn by another match (D-029), the widen→drop-topic→
  allow-older-repeats→fail fallback ladder with a typed
  `QuestionPoolExhaustedException`, `POST /rules/selection/preview` rewritten
  to delegate to the real selector instead of the old Phase-7 stub (D-033),
  and `ReleaseReservationsAsync` for abandoned matches.
- Also wired up `QuestionSelectionRule.TopicFilterJson` (a dead column since
  Phase 7 — D-027) and added `IRuleService.ResolveSelectionRuleAsync`
  (nullable, unlike `ResolveScoringRuleAsync` — D-028).
- New files: `Quizware/src/Quizware.Application/Selection/{SelectionModels,
  IQuestionSelector,QuestionSelector}.cs`,
  `Quizware/tests/Quizware.Api.IntegrationTests/SelectionEngineTests.cs` (7
  new tests). `MatchesController.cs` deliberately left untouched — still all
  `501` stubs, Phase 9 territory.
- No new EF migration — confirmed via `dotnet ef migrations
  has-pending-model-changes`.
- Full test suite independently re-run: **245/245 passing** (17 Application,
  95 Domain, 4 Architecture, 129 Api.IntegrationTests), up from 238 — closes
  V-009. `dotnet build` → 0 warnings/errors.
- Live-verified end-to-end against real LocalDB (`Quizware-Dev`): logged in
  as the seeded admin, created + approved a real MCQ question via the live
  API, then `POST /rules/selection/preview` returned
  `{"poolSize":1,"eligibleAfterFilters":1,"eligibleAfterRepeatPolicy":1,
  "canSatisfy":true,"difficultyMixAchievable":{"Medium":1}}` — confirms the
  full pipeline works against real SQL Server LocalDB, not just SQLite tests.
- **Correction to prior checkpoint's `CURRENT.md`:** the repo root was stated
  as `C:\Sharique\Projects\Personal\Quizware\` — actually
  `C:\Sharique\Projects\Personal\QuizApp\`, with the solution nested at
  `Quizware/` underneath. Fixed here and in `PROJECT.md`.

**What's next: Phase 9 — the match engine.** Both stated prerequisites (Phase
7, Phase 8) are now done. Re-read `docs/Implementation-Plan.md`'s Phase 9
section fresh — do not assume its text matches this file. See T-020.

## 3. State of play

| Area | State |
|---|---|
| Phases 0–7 | `[FACT]` DONE, all committed (chain ending `3dbc6f2`, then Postman+bugfixes `d2b16cc`, then rename/Scalar/README commits through `859813c`). |
| Phase 8 (all P8-01–P8-10) | `[FACT]` DONE, **committed** `31d2f22`. |
| Test suite | `[FACT]` 245/245 passing, independently re-run this session — closes V-009. |
| Build | `[FACT]` 0 warnings, 0 errors, independently re-run this session. |
| Migrations | `[FACT]` No new migration needed for Phase 8 (`TopicFilterJson` already existed, just previously unwired). Live LocalDB verification (creating/approving a real question) is strong indirect evidence all 5 migrations are applied — see V-008 (still formally `[UNVERIFIED]`, not a direct `__EFMigrationsHistory` query). |
| Git (outer repo) | `[FACT]` Branch `master`, HEAD `31d2f22`, working tree clean at session start and end. |
| Context system | `[FACT]` This is its 7th real merge. |

## 4. Next actions

1. **Start Phase 9 — the match engine.** Re-read `docs/Implementation-Plan.md`'s
   Phase 9 section fresh. See T-020. Note D-025 (Phase 9's `Activate()` must
   reuse the Phase 8 reservation's seed to reproduce `OptionOrderJson`
   identically) and D-031 (the selector's write methods don't call
   `SaveChangesAsync` — Phase 9's handler must, once per segment, matching
   P9-02's one-transaction criterion).
2. **T-006** (Phase 14, not urgent) — decide whether `Quizware.BuzzerAgent`
   references `Quizware.Modules.Buzzer` to reuse serial frame-parsing code, or
   reimplements it standalone.
3. **Q-006** (new, not blocking) — confirm whether `DifficultyMixJson`'s
   invented percentage-map convention (D-030) is right before Angular
   rule-configuration screens are built against it.
4. Optional, low priority: `docs/Implementation-Plan.md`'s own "Start here"
   section is stale (T-017); Postman collection doesn't yet cover Phase 8's
   `POST /rules/selection/preview` endpoint.
5. **V-008** still open if a direct check is wanted: query `__EFMigrationsHistory`
   in `Quizware-Dev`, or run `dotnet ef database update` as a no-op check.

Full queue: `TASKS.md`.

## 5. Constraints you must respect

- `[DECIDED]` **Architecture is settled at design level** — modular monolith, Clean
  Architecture, shared schema with `ProgramId` multi-tenancy, Table-Per-Type
  questions (D-009), configuration-driven tournament with configurable segment
  order (D-010), event-sourced scoring, tie-break as an ordinary Match through the
  existing engine (D-011), EF Core 10 code-first, SignalR. 10 ADRs in `docs/adr/`.
  Full list with rejected alternatives: `docs/new-system/02-Architecture-Proposal.md`
  §2.16, plus D-009 – D-033 in `DECISIONS.md`. Do not re-open one without reading
  why the alternative was rejected.
- `[DECIDED]` **Questions are Table-Per-Type** (D-009), one route per format.
  Reads now go through a parallel `QuestionDto` hierarchy in
  `Application/QuestionBank/Dtos/` (Application cannot reference Api's
  `QuestionResponse` types), mapped to the API contract by
  `QuestionResponseMapper.cs`.
- `[DECIDED]` **Routing convention for what uses MediatR** (D-019): if the
  phase's core entities are Domain types exposed on `IAppDbContext` (Team,
  Topic, Tag, Question, Program), use MediatR/Application. If they are
  Infrastructure-only types, business logic goes directly in the controller —
  Application cannot reference Infrastructure at all (enforced by
  `Architecture.Tests`).
- `[DECIDED]` **Tag filtering on questions is not implementable yet** (D-026):
  no `QuestionTag` join entity exists anywhere in the schema. Phase 8's
  selector implements every other P8-01 pool filter but not tag filter — needs
  a schema migration first, out of scope until explicitly requested.
- `[DECIDED]` **`DifficultyMixJson` is a flat percentage map** (D-030), e.g.
  `{"Easy":60,"Hard":40}`, invented this session — no prior convention
  existed. `[ASSUMED]`, not confirmed with the user — see Q-006.
- `[DECIDED]` **`IQuestionSelector`'s write methods never call
  `SaveChangesAsync`** (D-031) — the caller (Phase 9's match-start handler)
  owns the transaction boundary, matching P9-02.
- `[DECIDED]` **`MatchQuestion.OptionOrderJson` is only set at `Activate()`
  time (Phase 9), not at Phase 8 reservation** (D-025) — the selector returns
  the computed order on its DTO instead, for the future caller to reuse via
  the same seed.
- `[DECIDED]` **Cross-match reservation locking** (D-029): any
  `MatchQuestion` row with `State != Released`, in any match, excludes that
  question from every other match's draw — not just `QuestionUsageHistory`.
- `[DECIDED]` **`IRuleService.ResolveSelectionRuleAsync` returns `null` when no
  rule matches** (D-028), unlike `ResolveScoringRuleAsync` which throws — a
  selection rule is optional, scoring is not.
- `[DECIDED]` **Question editing has no separate Update endpoint** (D-020):
  `PUT {formatCode}/{id}` reuses the Create command with an optional
  `ReplacesQuestionId`.
- `[DECIDED]` **Phase 6f Excel import is MCQ-only** (D-022); other formats
  deferred, not forgotten.
- `[ASSUMED]` **Media validation limits are this implementation's own numbers**
  (D-021), not documented anywhere — confirm with the user before treating as
  fixed requirements.
- `[DECIDED]` **7 roles, no Judge** (D-013), **9 authorization policies**.
  Display tokens carry only `Roles.Display`.
- `[DECIDED]` **On-premises hosting, no cloud dependency** (D-014).
- `[DECIDED]` **The buzzer must be deletable** — `IBuzzerProvider` port, `Null`
  default (ADR-005).
- `[DECIDED]` **No fake answers, ever.**
- `[DECIDED]` **`docs/` is entirely gitignored** (D-016) — "not in `git log`"
  does not mean "doesn't exist" for anything under `docs/`.
- `[DECIDED]` **Swashbuckle 10.x needs explicit polymorphic-schema wiring**
  (D-017); **NSwag, not openapi-generator-cli** (D-018).
- `[DECIDED]` **Commit only when asked** (V-005). **As of this session, the
  working tree is clean — all Phase 8 work (and the earlier staged Postman/
  bugfix work) is committed**, out-of-band by the user (fourth recurrence of
  L-004).
- `[FACT]` **A DB-only uniqueness/state constraint without a handler pre-check
  surfaces as an unhandled 500, not a clean 4xx** (L-007/L-010) — any upsert
  handler must look up existing rows by every column a unique index covers,
  not just `Id`.
- `[FACT]` **`Program.MaxTeams` (typed column) is the real team-cap mechanism**
  — not the `ProgramSetting("Teams","MaxTeams")` key (L-009).
- `[DECIDED]` **Reordering a unique-`OrderIndex` list needs a two-phase reindex**
  (D-023). **Locked segments keep their slot during a bulk segment reorder**
  (D-024).

Reasoning for all decisions: `DECISIONS.md` D-001 – D-033.

## 6. Files in play

| Path | Note |
|---|---|
| `Quizware/src/Quizware.Application/Selection/{SelectionModels,IQuestionSelector,QuestionSelector}.cs` | New this session — the Phase 8 question selection engine |
| `Quizware/tests/Quizware.Api.IntegrationTests/SelectionEngineTests.cs` | New this session — 7 tests |
| `Quizware/src/Quizware.Domain/Tournament/QuestionSelectionRule.cs` | `Specificity` added; `Update()` gained `topicFilterJson` param (D-027) |
| `Quizware/src/Quizware.Application/Rules/Services/{IRuleService,RuleService}.cs` | `ResolveSelectionRuleAsync` added (D-028) |
| `Quizware/src/Quizware.Application/Rules/Queries/PreviewSelection.cs` | Rewritten to delegate to `IQuestionSelector.PreviewAsync` (D-033); `SegmentTemplateId` added |
| `Quizware/src/Quizware.Application/Rules/{Dtos/RuleDtos.cs,Dtos/RuleMappings.cs,Commands/UpsertSelectionRules.cs}` | `TopicFilterJson` threaded through |
| `Quizware/src/Quizware.Application/DependencyInjection.cs` | Registers `IQuestionSelector -> QuestionSelector` |
| `Quizware/src/Quizware.Api/Contracts/V1/Rules/RuleContracts.cs`, `Controllers/v1/RulesController.cs` | `SelectionRuleDto.TopicFilterJson`, `SelectionPreviewRequest.SegmentTemplateId` |
| `Quizware/src/Quizware.Api/Controllers/v1/MatchesController.cs` | Still 501 stubs — deliberately untouched, Phase 9 |
| `Quizware/postman/{Quizware.postman_collection.json,README.md}` | 80 requests, Phase 0–7 coverage, committed `d2b16cc` |
| `context/_meta/SPEC.md` | The save/resume procedure |

`[FACT]` `QuizApp-9AMM/` and `QuickBuzz/` are **nested git repositories**.

## 7. Open questions

- **Q-003** `[OPEN]` — which other AI tools need adapters beyond Claude, Cursor,
  and `AGENTS.md`? `[UNVERIFIED]` whether Codex reads a root `AGENTS.md` (V-001).
- **Q-005** `[OPEN]` — `POST /buzzer/sessions/{id}/presses` is `[AllowAnonymous]`,
  not the final security posture; Phase 14 needs a real auth scheme for the
  buzzer agent.
- **Q-006** `[OPEN]` (new) — is `DifficultyMixJson`'s invented percentage-map
  convention (D-030) right for the future Angular rule-configuration screens,
  or should it be raw per-difficulty counts? Not blocking, but confirm before
  Angular work reaches those screens.

Q-001, Q-002, Q-004 remain **ANSWERED** — see `TASKS.md` Closed.

Verification queue in `TASKS.md`: V-001 (Codex/AGENTS.md), V-006 (Docker/CI
unverified), V-007 (Testcontainers-vs-SQL-Server), V-008 (migrations formally
unverified as *applied* to LocalDB, though this session's live question
create/approve is strong indirect evidence they are). V-009 **resolved** this
session (245/245 tests independently re-run).

## 8. Do not retry

- **L-001** through **L-003** — see `LESSONS.md` (heredoc failures, bare
  subagent invocation, doc duplication).
- **L-004** (four recurrences now) — a conversation brief's/prior checkpoint's
  claim about commit state is reliably stale by save time; the user commits
  proposed work out-of-band without narrating it. **This session's fourth
  recurrence was caught two ways:** `git log` at session start (found `d2b16cc`
  and the rename chain already committed), and the system-provided `gitStatus`
  reminder at save time (independently confirmed `31d2f22` was already HEAD,
  no extra `git log` call needed to catch it). Always check one of these
  before writing any commit-state claim.
- **L-005** — Swashbuckle needs explicit polymorphic-schema wiring, doesn't
  auto-detect `[JsonPolymorphic]`/`[JsonDerivedType]`.
- **L-006** — `openapi-generator-cli` needs a JVM this environment doesn't have;
  use NSwag instead.
- **L-007**/**L-010** — a uniqueness or state constraint enforced only at the
  DB level, without a matching handler pre-check keyed on the **full** unique
  index (not just `Id`), surfaces as an unhandled 500. Checked and confirmed
  clean for the Phase 8 selector's own writes this session (no new unique
  constraints introduced).
- **L-008** — `Question.Approve`'s `InvalidOperationException` isn't mapped by
  `GlobalExceptionHandler`; guard the precondition in the handler instead.
- **L-009** — don't mistake `ProgramSetting("Teams","MaxTeams")` for a second
  team-cap mechanism; `Program.MaxTeams` is the real one.

Full detail: `LESSONS.md`.

## 9. Environment and commands

`[FACT]` Windows 10 Pro 10.0.19045 · PowerShell primary, Git Bash tool also
available · **context root `C:\Sharique\Projects\Personal\QuizApp`** (corrected
this session — was wrongly stated as `...\Personal\Quizware` in the prior
checkpoint) · branch `master`, HEAD `31d2f22`. `[FACT]` .NET 10 SDK. LocalDB
instance `(localdb)\MSSQLLocalDB`, database `Quizware-Dev`. Seeded admin:
`admin@quizapp.local` (password location known from prior sessions, not
repeated here — credential value, not to be recorded per SPEC §4.1).

```bash
cd Quizware && dotnet build Quizware.slnx                                    # 0 warnings, 0 errors
dotnet test Quizware.slnx --no-build                                          # 245/245 passing
dotnet ef migrations has-pending-model-changes --project src/Quizware.Infrastructure --startup-project src/Quizware.Api   # confirms no migration needed
cd src/Quizware.Api && dotnet run --no-build --urls http://localhost:5299     # live LocalDB verification
netstat -ano | grep ':5299' | grep LISTENING | awk '{print $5}'               # find the dev-server PID to kill it cleanly afterward
taskkill //PID <pid> //F
```

## 10. Where to read more

| File | For |
|---|---|
| `TASKS.md` | The work queue, open questions, verification queue |
| `DECISIONS.md` | Why things are the way they are; rejected alternatives |
| `LESSONS.md` | What already failed — **read before proposing an approach** |
| `PROJECT.md` | Stack, repo map, glossary, environment, conventions |
| `HISTORY.md` | The timeline of checkpoints |
| `sessions/2026-09-10-01-phase8-question-selection-engine.md` | Full detail of this session (Phase 8) |
| `sessions/2026-09-08-02-phase7-tournament-configuration.md` | Phase 7 + Postman collection detail |
| `sessions/2026-09-08-01-phase6-configuration-modules.md` | Phase 6 (6a–6f) detail |
| `sessions/2026-09-07-01-phases-2-3-4-5-catchup.md` | Phases 2–5 detail |
| `_meta/SPEC.md` | How to save and resume context |
| `README.md` | The workflow, for humans |

**To checkpoint your own session:** `/save-context` in Claude Code, or in any
other tool: *"Save the context per `context/_meta/SPEC.md`."*
