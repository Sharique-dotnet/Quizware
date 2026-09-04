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

**Last updated:** 2026-09-04 · **Session:** S-2026-09-04-01 · **Saved by:** Claude Opus 5 (Claude Code)

---

## 1. What this project is

`[FACT]` A rebuild of a quiz-tournament system. **QuizApp-9AMM** (ASP.NET MVC 4,
in production) is being replaced by **QuizApp** — ASP.NET Core Web API on .NET 10
with SQL Server, an Angular 22 front end later. **QuickBuzz**, the existing buzzer
app, becomes an optional module that the system must work without.

`[FACT]` The core problem being solved: the legacy system hardcodes the tournament.
A new database per event; turn order as `QuestionNumber % 3` in 30+ places; the
running order of question types baked into 108 views as redirect chains. The new
design turns all of it into configuration data.

**Naming trap:** `QuizApp` (new), `QuizApp-9AMM` (legacy MVC 4), and `QuickBuzz`
(legacy buzzer) are three different systems. Check `PROJECT.md` §Domain glossary
before editing, or you will change the wrong one.

## 2. Current objective

`[FACT]` As of the last checkpoint, the project sits at **Phase 0 — requirements
confirmation** (`docs/new-system/06-Development-Roadmap.md` §6.1). Design
documents 01–06 are complete. **No application code has been written**:
`QuizApp/QuizApp.slnx` contains exactly `<Solution />`.

`[FACT]` The most recent session (S-2026-09-04-01) did not advance the product. It
built the AI context-preservation system you are reading now, because the user
works across two Claude accounts, Codex, Cursor, and other tools and was losing
project context on every switch.

So there are two threads:
- **Product:** confirm the Phase 0 assumptions, then scaffold the solution (T-001, T-002).
- **Tooling:** the context system is built but has never been exercised on a second save (T-003).

## 3. State of play

| Area | State |
|---|---|
| Design docs 01–06 | `[FACT]` Complete, ~5,500 lines in `docs/new-system/` |
| Legacy analyses | `[FACT]` Complete, ~1,600 lines in `docs/` |
| Phase 0 confirmation | `[FACT]` **Not done.** ~18 assumptions await the user (Q-001) |
| New solution | `[FACT]` Empty. `QuizApp/QuizApp.slnx` is `<Solution />` |
| Domain / API / DB code | `[FACT]` None written |
| Context system | `[FACT]` Built and populated this session. Untested on a merge (T-003) |
| Git | `[FACT]` Outer repo on `master`, **no commits yet**. Nothing committed this session |

## 4. Next actions

1. **Ask the user Q-001** — the Phase 0 assumptions. `docs/new-system/06-Development-Roadmap.md`
   §6.5 lists each with the design's assumed answer, so this is a confirm-or-correct
   pass, not an interview. Phase 1 encodes these into the domain model; getting
   them wrong is expensive later.
2. **Ask the user Q-002** — should `context/` be committed, and should the outer
   repo get its first commit? The context system is worthless across machines and
   accounts if it is not committed.
3. **Then T-002** — scaffold the four projects per
   `docs/new-system/02-Architecture-Proposal.md` §2.4. Run `dotnet --list-sdks`
   first (V-002 — the .NET 10 SDK is unverified on this machine).

Full queue: `TASKS.md`.

## 5. Constraints you must respect

- `[DECIDED]` **Architecture is settled at design level** — modular monolith, Clean
  Architecture, shared schema with `ProgramId` multi-tenancy, Table-Per-Type
  questions, configuration-driven tournament, event-sourced scoring, EF Core 10
  code-first, SignalR. All twelve decisions with their rejected alternatives are in
  `docs/new-system/02-Architecture-Proposal.md` §2.16. Do not re-open one without
  reading why the alternative was rejected.
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
- `[ASSUMED]` **Commit only when asked** (V-005). Confirm before the first commit.

Reasoning for the context-system decisions: `DECISIONS.md` D-001 – D-008.

## 6. Files in play

| Path | Note |
|---|---|
| `docs/new-system/06-Development-Roadmap.md` §6.5 | The ~18 open assumptions — the immediate blocker |
| `docs/new-system/02-Architecture-Proposal.md` §2.4, §2.16 | Target project structure and the decision table |
| `docs/new-system/04-Database-Schema.md` | 1,956 lines — the schema, needed from Phase 4 |
| `QuizApp/QuizApp.slnx` | `<Solution />` — where the new code will start |
| `context/_meta/SPEC.md` | The save/resume procedure. Read before checkpointing |
| `AGENTS.md`, `CLAUDE.md`, `.cursor/rules/` | Tool adapters, all pointing at SPEC |

`[FACT]` `QuizApp-9AMM/` and `QuickBuzz/` are **nested git repositories** — they
have their own `.git` and their own history. The outer repo owns the new work.

## 7. Open questions

- **Q-001** `[OPEN]` **BLOCKING** — which of the ~18 Phase 0 assumptions do you
  confirm? Blocks Phase 1 and therefore T-002. Widest blast radius: shared vs
  per-program question bank; disqualified score zeroed vs kept-and-excluded;
  whether a team may play more than one match per stage; whether negative totals
  are allowed.
- **Q-002** `[OPEN]` — commit `context/`, and give the outer repo its first commit?
- **Q-003** `[OPEN]` — which other AI tools need adapters? Claude, Cursor, and the
  `AGENTS.md` convention are covered. `[UNVERIFIED]` whether the user's Codex reads
  a root `AGENTS.md` (V-001).

Verification queue (V-001 – V-005) in `TASKS.md` — five things currently believed
but unchecked, including whether the .NET 10 SDK is even installed here.

## 8. Do not retry

- **L-001** Large quoted heredocs (`cat > f <<'EOF'`) fail in the Bash tool on this
  machine — `unexpected EOF while looking for matching '` partway through the body.
  Use the Write tool for anything over ~50 lines.
- **L-002** A context-saving subagent invoked bare **cannot see the conversation**
  and will invent a plausible one. Always go through `/save-context`, which
  extracts the brief in the main session first.
- **L-003** Do not summarize `docs/new-system/` into `context/`. It creates a second
  source of truth that drifts, and a summary of a specification is a worse
  specification. Point with a path *and* a section number.

Full detail: `LESSONS.md`.

## 9. Environment and commands

`[FACT]` Windows 10 Pro 10.0.19045 · PowerShell 5.1 primary, Git Bash available ·
context root `C:\Sharique\Projects\Personal\QuizApp` · branch `master`, no commits.

```bash
git status --short          # outer repo only; nested repos report separately
dotnet --list-sdks          # V-002 — verify .NET 10 before scaffolding
```

`[FACT]` No build or test command exists yet — there is no code. Record the real
commands in `PROJECT.md` §Commands the first time they are run.

## 10. Where to read more

| File | For |
|---|---|
| `TASKS.md` | The work queue, open questions, verification queue |
| `DECISIONS.md` | Why things are the way they are; rejected alternatives |
| `LESSONS.md` | What already failed — **read before proposing an approach** |
| `PROJECT.md` | Stack, repo map, glossary, environment, conventions |
| `HISTORY.md` | The timeline of checkpoints |
| `sessions/2026-09-04-01-context-system-bootstrap.md` | Full detail of the last session |
| `_meta/SPEC.md` | How to save and resume context |
| `README.md` | The workflow, for humans |

**To checkpoint your own session:** `/save-context` in Claude Code, or in any other
tool: *"Save the context per `context/_meta/SPEC.md`."*
