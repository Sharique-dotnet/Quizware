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

**Last updated:** 2026-09-04 · **Session:** S-2026-09-04-02 · **Saved by:** Claude Sonnet 5 (Claude Code)

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

`[FACT]` The design docs (`docs/new-system/01`–`06` + README, ~6,000 lines) and a
new `docs/Implementation-Plan.md` (658 lines, 185 tasks across 18 phases) are
complete and stable — no known pending edits. `[FACT]` The **actual .NET solution
has been scaffolded**: `QuizApp/QuizApp.slnx` now lists 10 projects with the
project references, subfolder layout, and dependency rule specified in
`docs/new-system/02-Architecture-Proposal.md` §2.4, and it builds clean (0
warnings, 0 errors). **This corrects the previous checkpoint's claim that no
application code existed** — that was true as of S-2026-09-04-01, it is not true
now.

Two things remain before real feature work (Phase 1 onward) can proceed
confidently:
- **Product:** several Phase 0 assumptions are still open (T-001/Q-001) — though
  four of the highest-impact ones (disqualification approver, Judge role, hosting,
  buzzer device count) are now answered directly in the docs, not just assumed.
- **Process:** the roadmap's own recommended order is Domain (Phase 1) → ADRs
  (Phase 2) → Skeleton (Phase 3), but the skeleton was built first, with Phase 1
  and 2 not yet started. Not a problem, just worth knowing before assuming Phase 1
  is done.

## 3. State of play

| Area | State |
|---|---|
| Design docs 01–06 + README | `[FACT]` Complete, ~6,000 lines in `docs/new-system/` |
| `docs/Implementation-Plan.md` | `[FACT]` Complete, 658 lines, 185 tasks, 18 phases, 9 milestones |
| Legacy analyses | `[FACT]` Complete, ~1,600 lines in `docs/` |
| Phase 0 confirmation | `[FACT]` Partially done — see T-001/Q-001. No real-world stakeholder sign-off yet; still one person iterating with an AI |
| New solution (`QuizApp/`) | `[FACT]` Scaffolded: 10 projects, correct references, correct subfolders, template files removed, builds clean. **No feature code yet** — no domain model, no DI wiring beyond defaults, no Identity/JWT, no EF Core context/migrations, no CI |
| Domain / API / DB code | `[FACT]` None written — only `Program.cs` entry points exist (Api, BuzzerAgent) |
| Context system | `[FACT]` Built S-2026-09-04-01, exercised for the first real merge this session (T-003 now DONE) |
| Git | `[FACT]` Outer repo on `master`, **one commit** (`bf8cb06`). The `QuizApp/` scaffolding is `git add`-staged on top of it but **not committed** (T-007/Q-004) |

## 4. Next actions

1. **Decide T-007/Q-004** — commit the staged `QuizApp/` scaffolding, or leave it
   staged? Recommendation: commit it, for the same reason `context/` itself is
   committed (D-002/D-006) — uncommitted work does not survive a machine switch.
2. **Resolve the sequencing question in T-005** — either backfill Phase 1 (domain
   model) and Phase 2 (ADRs) before continuing, or proceed with Phase 3's
   remaining cross-cutting concerns (DI wiring, Identity/JWT, global exception
   handler, Serilog, FluentValidation pipeline, health checks, CI) on top of the
   skeleton as-is. This is a judgment call for whoever resumes, not something to
   assume silently.
3. **Ask the user Q-001's residual sub-points** — shared vs per-program question
   bank, disqualified-team score handling, one-match-per-stage-per-team or not.
   `docs/new-system/06-Development-Roadmap.md` §6.5 has the full table.
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
- `[ASSUMED]` **Commit only when asked** (V-005) — still the working assumption,
  though one commit exists that this session's brief did not report as
  user-requested (presumed done by the user directly, not a contradiction).

Reasoning for all decisions: `DECISIONS.md` D-001 – D-014.

## 6. Files in play

| Path | Note |
|---|---|
| `QuizApp/QuizApp.slnx` and `QuizApp/{src,tests,tools}/` | The scaffolded solution — 10 projects, staged not committed |
| `docs/new-system/06-Development-Roadmap.md` §6.5 | Remaining open Phase 0 assumptions |
| `docs/new-system/02-Architecture-Proposal.md` §2.4, §2.16 | Target project structure and the decision table |
| `docs/new-system/04-Database-Schema.md` | 1,956 lines — TPT question tables, `TieBreakRule`, `ProgramQuestionFormat`, `SegmentOrderMode` all live here |
| `docs/Implementation-Plan.md` | 658 lines — the phased task list with acceptance criteria; Phase 3 tasks are what's left of T-005 |
| `context/_meta/SPEC.md` | The save/resume procedure. Read before checkpointing |
| `AGENTS.md`, `CLAUDE.md`, `.cursor/rules/` | Tool adapters, all pointing at SPEC |

`[FACT]` `QuizApp-9AMM/` and `QuickBuzz/` are **nested git repositories** — they
have their own `.git` and their own history. The outer repo owns the new work.

## 7. Open questions

- **Q-001** `[OPEN]` — remaining Phase 0 assumptions: shared vs per-program
  question bank; disqualified-team score zeroed vs kept-and-excluded; one match
  per stage per team or more. `docs/new-system/06-Development-Roadmap.md` §6.5.
  No longer blocks T-002 (scaffolding proceeded without it); does block firming up
  the domain model.
- **Q-004** `[OPEN]` — commit the staged `QuizApp/` scaffolding now? See T-007.
- **Q-003** `[OPEN]` — which other AI tools need adapters beyond Claude, Cursor,
  and `AGENTS.md`? `[UNVERIFIED]` whether the user's Codex reads a root
  `AGENTS.md` (V-001).

Q-002 (commit `context/`?) is **ANSWERED**: yes, done (`bf8cb06`).

Verification queue in `TASKS.md`: V-001 (Codex/AGENTS.md), V-003 (SQL Server
availability). V-002 and V-004 are resolved — see `TASKS.md` Closed.

## 8. Do not retry

- **L-001** Large quoted heredocs (`cat > f <<'EOF'`) fail in the Bash tool on this
  machine partway through the body. Use the Write tool for anything over ~50 lines.
- **L-002** A context-saving subagent invoked bare **cannot see the conversation**
  and will invent a plausible one. Always go through `/save-context`.
- **L-003** Do not summarize `docs/new-system/` into `context/`. Point with a path
  *and* a section number.

Full detail: `LESSONS.md`.

## 9. Environment and commands

`[FACT]` Windows 10 Pro 10.0.19045 · PowerShell 5.1 primary, Git Bash available ·
context root `C:\Sharique\Projects\Personal\QuizApp` · branch `master`, 1 commit.
`[FACT]` .NET SDKs 8.0.421 and 10.0.400 installed at `C:\Program Files\dotnet\sdk`.

```bash
git status --short                 # outer repo only; nested repos report separately
cd QuizApp && dotnet build          # 0 warnings, 0 errors as of this session
dotnet sln QuizApp.slnx list        # list the 10 projects
```

Note: the solution root is `QuizApp\QuizApp\` (repo root and solution folder share
the last path segment) — easy to fumble in `cd`.

`[UNVERIFIED]` No tests exist yet (test projects have a `.csproj` but no `.cs`
files). No EF Core context or migrations exist — that is Phase 4.

## 10. Where to read more

| File | For |
|---|---|
| `TASKS.md` | The work queue, open questions, verification queue |
| `DECISIONS.md` | Why things are the way they are; rejected alternatives |
| `LESSONS.md` | What already failed — **read before proposing an approach** |
| `PROJECT.md` | Stack, repo map, glossary, environment, conventions |
| `HISTORY.md` | The timeline of checkpoints |
| `sessions/2026-09-04-02-tpt-roles-scaffolding.md` | Full detail of this session (design edits + scaffolding) |
| `sessions/2026-09-04-01-context-system-bootstrap.md` | Full detail of the context system's own bootstrap |
| `_meta/SPEC.md` | How to save and resume context |
| `README.md` | The workflow, for humans |

**To checkpoint your own session:** `/save-context` in Claude Code, or in any other
tool: *"Save the context per `context/_meta/SPEC.md`."*
