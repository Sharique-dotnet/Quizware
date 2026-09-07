# CURRENT CONTEXT — QuizApp

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

**Last updated:** 2026-09-04 · **Session:** S-2026-09-04-03 · **Saved by:** Claude Sonnet 5 (Claude Code)

---

## 1. What this project is

`[FACT]` A rebuild of a quiz-tournament system. **QuizApp-9AMM** (ASP.NET MVC 4,
in production) is being replaced by **QuizApp** — ASP.NET Core Web API on .NET 10
with SQL Server, an Angular 22 front end later. **QuickBuzz**, the existing buzzer
app, becomes an optional module that the system must work without.

`[FACT]` The core problem being solved: the legacy system hardcodes the tournament.
A new database per event; turn order as `QuestionNumber % 3` in 30+ places; the
running order of question types baked into 108 views as redirect chains; one
`TieBreaker` table/screens completely disconnected from qualification logic. The
new design turns all of it into configuration data.

**Naming trap:** `QuizApp` (new), `QuizApp-9AMM` (legacy MVC 4), and `QuickBuzz`
(legacy buzzer) are three different systems. Check `PROJECT.md` §Domain glossary
before editing, or you will change the wrong one.

## 2. Current objective

`[FACT]` The design docs (`docs/new-system/01`–`06` + README, ~6,000 lines) and
`docs/Implementation-Plan.md` (658 lines, 185 tasks across 18 phases) are
complete and stable. `[FACT]` The .NET solution is scaffolded (10 projects,
builds clean) **and Phase 1 — the domain model — is now fully implemented**:
all of P1-01 through P1-14 exist under `QuizApp/src/QuizApp.Domain/`, 95 tests
pass under `QuizApp/tests/QuizApp.Domain.Tests/`, and the whole solution builds
with 0 warnings/0 errors. **This corrects the previous checkpoint's claim that
no domain model existed** — that was true as of S-2026-09-04-02, it is not true
now. Full breakdown: `TASKS.md` T-010 (Closed).

`[DECIDED]` Phase 0 is resolved: the user is QuizApp's sole owner, so Phase 0 is
treated as **owner-confirmed** rather than run as an external stakeholder
workshop — see D-015. All 16 assumptions in
`docs/new-system/06-Development-Roadmap.md` §6.5 are accepted as `[DECIDED]`.
**Known loose end:** `docs/Implementation-Plan.md`'s Phase 0 section (lines
73–88) still reads with its original six-task workshop wording on disk — an
edit to replace it with a short "SKIPPED — owner-confirmed" note did not
persist, `[FACT]` verified this session. The decision stands regardless (see
D-015's Consequences); the doc text just needs re-editing to match. See T-008.

What remains before further feature work:
- **Sequencing (T-009):** the roadmap's recommended order is Domain (Phase 1) →
  ADRs (Phase 2) → Skeleton (Phase 3). In practice this project has done
  Skeleton first, then Domain, with ADRs (Phase 2) still not started at all.
  Whoever resumes should decide whether to backfill Phase 2 now or continue
  Phase 3's remaining cross-cutting concerns (T-005) directly on top of the
  domain model that now exists.
- **Commit state:** as of this session, everything through Phase 1 is
  committed except one line in `CLAUDE.md` (see §3 Git row below). Do not
  assume otherwise without checking `git log` yourself — a prior session's
  brief claimed the opposite and was stale by the time this checkpoint ran
  (L-004).

## 3. State of play

| Area | State |
|---|---|
| Design docs 01–06 + README | `[FACT]` Complete, ~6,000 lines in `docs/new-system/` |
| `docs/Implementation-Plan.md` | `[FACT]` Complete, 658 lines, 185 tasks, 18 phases, 9 milestones. Phase 0 section text not yet re-synced with D-015 (T-008) |
| Legacy analyses | `[FACT]` Complete, ~1,600 lines in `docs/` |
| Phase 0 confirmation | `[DECIDED]` Owner-confirmed, not a workshop (D-015). See T-001 (Closed), Q-001 (Answered) |
| New solution (`QuizApp/`) | `[FACT]` Scaffolded: 10 projects, correct references, correct subfolders, builds clean |
| Domain model (Phase 1) | `[FACT]` **Complete** — P1-01 through P1-14, 89 `.cs` files, 95 passing tests. See T-010 (Closed) |
| Phase 2 (ADRs) | `[FACT]` Not started |
| Phase 3 (skeleton cross-cutting) | `[FACT]` Partial — no DI wiring beyond defaults, no Identity/JWT, no exception-handling middleware, no Serilog, no FluentValidation pipeline, no health checks beyond template defaults, no CI. See T-005 |
| Context system | `[FACT]` Built S-2026-09-04-01; this is its third real merge (T-003 exercised it first) |
| Git | `[FACT]` Outer repo on `master`, 7 commits (`bf8cb06` → `6e6a13e`, full list in `PROJECT.md` §Environment). Only `CLAUDE.md` has an uncommitted one-line addition. Corrects a stale prior claim — see L-004 |

## 4. Next actions

1. **Resolve T-009** — decide whether to backfill Phase 2 (ADRs) now that
   Phase 1 (domain model) is done, or continue Phase 3's remaining
   cross-cutting concerns (T-005: DI wiring, Identity/JWT, global exception
   handler, Serilog, FluentValidation pipeline, health checks, CI) directly on
   top of the domain model. Judgment call for whoever resumes.
2. **T-008** — re-edit `docs/Implementation-Plan.md`'s Phase 0 section (lines
   73–88) to reflect D-015 ("SKIPPED — owner-confirmed"); the previous attempt
   at this edit did not persist. Verify by re-reading the file after writing.
3. **Ask the user whether to commit `CLAUDE.md`'s one uncommitted line** (the
   new "always refer to `context/`" Instructions bullet) — everything else
   through Phase 1 is already committed.
4. **T-006** (not urgent, Phase 14) — decide whether `QuizApp.BuzzerAgent`
   references `QuizApp.Modules.Buzzer` to reuse serial frame-parsing code, or
   reimplements it standalone.

Full queue: `TASKS.md`.

## 5. Constraints you must respect

- `[DECIDED]` **Architecture is settled at design level** — modular monolith, Clean
  Architecture, shared schema with `ProgramId` multi-tenancy, Table-Per-Type
  questions (D-009), configuration-driven tournament with configurable segment
  order (D-010), event-sourced scoring, tie-break as an ordinary Match through the
  existing engine (D-011), EF Core 10 code-first, SignalR. Full list with rejected
  alternatives: `docs/new-system/02-Architecture-Proposal.md` §2.16, plus D-009 –
  D-014 in `DECISIONS.md` for the ones refined this session. Do not re-open one
  without reading why the alternative was rejected.
- `[DECIDED]` **Questions are Table-Per-Type** (D-009): one shared `Question`
  table plus one child table per format, because formats have genuinely different
  NOT-NULL requirements a shared table cannot express. One route/endpoint per
  format (e.g. `POST /questions/mcq`), not one generic endpoint.
- `[DECIDED]` **7 roles, no Judge** (D-013): SuperAdmin, ProgramAdmin,
  QuestionAuthor, Operator, Scorer, Display, Auditor. ProgramAdmin/SuperAdmin hold
  sole authority over disqualification, answer reversal, score adjustment, and
  tie-break resolution.
- `[DECIDED]` **On-premises hosting, no cloud dependency** (D-014). Buzzer device
  count is configurable, default 3.
- `[DECIDED]` **The buzzer must be deletable.** `QuizApp.Modules.Buzzer` sits behind
  an `IBuzzerProvider` port with a `Null` default. Every match must complete with
  the module removed entirely. The buzzer identifies who answered first; it never
  writes scores.
- `[DECIDED]` **No fake answers, ever.** Disqualifying a team recompacts `TurnOrder`
  and the match continues. This is the central defect being fixed.
- `[FACT]` **Design docs separate confirmed findings from recommendations**, and
  list assumptions explicitly. Preserve that distinction when editing them.
- `[FACT]` **Context is plain Markdown in the repo, never tool-specific state**
  (D-002) — the user switches between accounts and vendors mid-project.
- `[DECIDED]` **Commit only when asked** (V-005, now written into `CLAUDE.md`'s
  Instructions section directly). The assistant proposes plans in phases with a
  commit message per phase; the user runs `git commit`.
- `[DECIDED]` **Phase 0 is owner-confirmed, not a workshop** (D-015) — the user
  is QuizApp's sole owner/stakeholder; all 16 assumptions in
  `docs/new-system/06-Development-Roadmap.md` §6.5 are accepted.
- `[FACT]` **Domain exceptions are introduced incrementally**, when the entity
  that needs them is built, not stubbed out preemptively — established pattern
  through Phase 1, see `PROJECT.md` §Conventions.

Reasoning for all decisions: `DECISIONS.md` D-001 – D-015.

## 6. Files in play

| Path | Note |
|---|---|
| `QuizApp/QuizApp.slnx` and `QuizApp/{src,tests,tools}/` | The scaffolded solution — 10 projects, all committed |
| `QuizApp/src/QuizApp.Domain/` | Phase 1 domain model — complete, committed (T-010) |
| `QuizApp/tests/QuizApp.Domain.Tests/` | 95 xUnit + FluentAssertions tests, all passing |
| `docs/Implementation-Plan.md` lines 73–88 | Phase 0 section — still stale text, needs re-sync with D-015 (T-008) |
| `docs/new-system/06-Development-Roadmap.md` §6.5 | The 16 Phase 0 assumptions, now `[DECIDED]` via D-015 |
| `docs/new-system/02-Architecture-Proposal.md` §2.4, §2.16 | Target project structure and the decision table |
| `docs/new-system/04-Database-Schema.md` | 1,956 lines — TPT question tables, `TieBreakRule`, `ProgramQuestionFormat`, `SegmentOrderMode` all live here |
| `context/_meta/SPEC.md` | The save/resume procedure. Read before checkpointing |
| `AGENTS.md`, `CLAUDE.md`, `.cursor/rules/` | Tool adapters, all pointing at SPEC. `CLAUDE.md` also carries user process Instructions (commit workflow, plan format) — one line uncommitted |

`[FACT]` `QuizApp-9AMM/` and `QuickBuzz/` are **nested git repositories** — they
have their own `.git` and their own history. The outer repo owns the new work.

## 7. Open questions

- **Q-003** `[OPEN]` — which other AI tools need adapters beyond Claude, Cursor,
  and `AGENTS.md`? `[UNVERIFIED]` whether the user's Codex reads a root
  `AGENTS.md` (V-001).

Q-001 (Phase 0 assumptions) is **ANSWERED** via D-015: owner-confirmed, all 16
rows in §6.5 accepted. Q-002 (commit `context/`?) is **ANSWERED**: yes, done
(`bf8cb06`). Q-004 (commit the scaffolding?) is **ANSWERED**: yes, it was —
`git log` shows `893c4a7` (see `PROJECT.md` §Environment for the full commit
list).

Verification queue in `TASKS.md`: V-001 (Codex/AGENTS.md), V-003 (SQL Server
availability). V-002 and V-004 are resolved — see `TASKS.md` Closed.

## 8. Do not retry

- **L-001** Large quoted heredocs (`cat > f <<'EOF'`) fail in the Bash tool on this
  machine partway through the body. Use the Write tool for anything over ~50 lines.
- **L-002** A context-saving subagent invoked bare **cannot see the conversation**
  and will invent a plausible one. Always go through `/save-context`.
- **L-003** Do not summarize `docs/new-system/` into `context/`. Point with a path
  *and* a section number.
- **L-004** A conversation brief's claims about commit/staging state can be
  stale by the time a checkpoint runs — the user may commit directly outside
  the visible conversation. Always verify with `git log`/`git status` before
  writing anything about what is or isn't committed.

Full detail: `LESSONS.md`.

## 9. Environment and commands

`[FACT]` Windows 10 Pro 10.0.19045 · PowerShell 5.1 primary, Git Bash available ·
context root `C:\Sharique\Projects\Personal\QuizApp` · branch `master`, 7 commits.
`[FACT]` .NET SDKs 8.0.421 and 10.0.400 installed at `C:\Program Files\dotnet\sdk`.

```bash
git status --short                 # outer repo only; nested repos report separately
cd QuizApp && dotnet build          # 0 warnings, 0 errors as of this session
dotnet sln QuizApp.slnx list        # list the 10 projects
dotnet test QuizApp/QuizApp.slnx --filter "FullyQualifiedName~Domain"  # 95 passed
```

Note: the solution root is `QuizApp\QuizApp\` (repo root and solution folder share
the last path segment) — easy to fumble in `cd`.

`[FACT]` `QuizApp.Domain.Tests` has 95 passing xUnit + FluentAssertions tests.
Other test projects (`Application.Tests`, `Api.IntegrationTests`,
`Architecture.Tests`) still have only a `.csproj`, no `.cs` files. No EF Core
context or migrations exist — that is Phase 4.

## 10. Where to read more

| File | For |
|---|---|
| `TASKS.md` | The work queue, open questions, verification queue |
| `DECISIONS.md` | Why things are the way they are; rejected alternatives |
| `LESSONS.md` | What already failed — **read before proposing an approach** |
| `PROJECT.md` | Stack, repo map, glossary, environment, conventions |
| `HISTORY.md` | The timeline of checkpoints |
| `sessions/2026-09-04-03-phase0-owner-confirmed-phase1-domain.md` | Full detail of this session (Phase 0 decision + Phase 1 domain model) |
| `sessions/2026-09-04-02-tpt-roles-scaffolding.md` | Full detail of the design-doc edits + scaffolding session |
| `sessions/2026-09-04-01-context-system-bootstrap.md` | Full detail of the context system's own bootstrap |
| `_meta/SPEC.md` | How to save and resume context |
| `README.md` | The workflow, for humans |

**To checkpoint your own session:** `/save-context` in Claude Code, or in any other
tool: *"Save the context per `context/_meta/SPEC.md`."*
