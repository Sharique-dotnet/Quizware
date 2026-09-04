# DECISIONS

Append-only. Entries are never deleted or renumbered — a decision that turns out
wrong is marked `SUPERSEDED` and keeps its reasoning, so the next agent does not
re-propose it. Format: `_meta/SPEC.md` §6.3.

**Index:** D-001 · D-002 · D-003 · D-004 · D-005 · D-006 · D-007 · D-008

---

## Where the system-architecture decisions live

The twelve architecture decisions for QuizApp — modular monolith, Clean
Architecture, shared schema with `ProgramId`, Table-Per-Type questions,
configuration-driven tournament, event-sourced scoring, buzzer behind a port,
agent-pushes-to-API, EF Core 10 code-first, SignalR, `OrderIndex` segment
ordering, tie-break as an ordinary match — are recorded **with their rejected
alternatives** in `docs/new-system/02-Architecture-Proposal.md` §2.16.

They are not duplicated here. See **D-001** for why.

`[FACT]` Those decisions are design-stage and awaiting Phase 0 confirmation
(`docs/new-system/06-Development-Roadmap.md` §6.1 marks Phase 0 as the current
position). The unconfirmed points are tracked as Q-001 in `TASKS.md`.

---

### D-001 · `context/` points to design docs, it does not duplicate them
- **Status:** ACTIVE
- **Added:** 2026-09-04
- **Decision:** Architecture and requirements that are already written durably in
  `docs/` are referenced from `context/` by path and section, never copied into it.
- **Why:** `context/` exists to preserve what is at risk of being lost — the
  contents of a chat window. `docs/new-system/` is ~5,500 lines already committed
  to the repository and is in no danger. Copying it would create a second source
  of truth that silently drifts, and would blow the `CURRENT.md` size budget with
  information the next agent can read at its source.
- **Alternatives rejected:** *Summarize the design docs into `PROJECT.md`* — creates
  drift, and a summary of a specification is a worse specification. *Ignore the
  docs entirely* — the next agent would not know they exist.
- **Consequences:** `PROJECT.md` and `DECISIONS.md` stay short. Every pointer must
  carry a file path **and** a section number, because unanchored pointers rot.
- **Confidence:** [DECIDED]

### D-002 · Context is plain Markdown in the repository, never tool state
- **Status:** ACTIVE
- **Added:** 2026-09-04
- **Decision:** All AI-session continuity lives in `context/` as Markdown plus one
  small JSON file, committed to the repository.
- **Why:** The user moves between two Claude accounts, Codex, Cursor, and other
  tools mid-project. Every proprietary store — chat history, per-tool memory,
  account-scoped state — is invisible from at least one of those. Markdown in the
  repo is the only medium all of them can read and write today.
- **Alternatives rejected:** *Per-tool memory features* — do not cross accounts, let
  alone vendors. *A database or an MCP memory server* — adds a runtime dependency
  and a process the next tool may not have running; the failure mode is silent.
  *A single large context file* — cannot express "read this first, that on demand".
- **Consequences:** No automatic capture. A checkpoint is an explicit act, so the
  system is only as good as the discipline of invoking it. Accepted deliberately —
  see D-006.
- **Confidence:** [DECIDED]

### D-003 · `CURRENT.md` is the single entry point, and is size-capped
- **Status:** ACTIVE
- **Added:** 2026-09-04
- **Decision:** `CURRENT.md` is the one file an arriving agent must read, and it is
  capped at 400 lines. Everything else is read on demand.
- **Why:** The next agent arrives with no history and a finite budget. One
  self-sufficient file it can read in full beats six files it will skim. The cap
  is what keeps that promise true on a project with fifty sessions behind it — an
  entry point that grows without bound stops being read.
- **Alternatives rejected:** *No cap* — every append-only context system dies this
  way. *A separate tiny `START-HERE.md`* — one more indirection before the agent
  learns anything; the orientation is now the header of `CURRENT.md` instead.
- **Consequences:** Requires the demotion rule (SPEC §4.5): nothing may leave
  `CURRENT.md` until its content exists in a longer-lived file. Compaction is
  therefore a real step of every save, not an afterthought.
- **Confidence:** [DECIDED]

### D-004 · Every assertion carries a confidence tag
- **Status:** ACTIVE
- **Added:** 2026-09-04
- **Decision:** Five tags — `[FACT]`, `[DECIDED]`, `[ASSUMED]`, `[UNVERIFIED]`,
  `[OPEN]` — and an untagged assertion is a defect.
- **Why:** The failure mode that makes handover context dangerous is not omission,
  it is *confident staleness*: an assumption written in the same voice as a
  verified fact, which the next agent then builds on. Tags make the distinction
  mechanical instead of a matter of tone. This mirrors the convention the design
  docs already use — they separate confirmed findings from recommendations and
  list assumptions explicitly (`docs/new-system/README.md`).
- **Alternatives rejected:** *Prose hedging* ("probably", "I think") — invisible to
  a skim and lost in summarization. *Separate files per confidence level* —
  fragments each topic across files.
- **Consequences:** `[ASSUMED]` and `[UNVERIFIED]` items need somewhere to be
  chased, hence the Verification Queue in `TASKS.md`. Promoting a tag to `[FACT]`
  requires citing the evidence on the line.
- **Confidence:** [DECIDED]

### D-005 · Contradictions supersede; nothing is ever overwritten
- **Status:** ACTIVE
- **Added:** 2026-09-04
- **Decision:** When new information contradicts an existing entry, the old entry
  keeps its full text and gains `SUPERSEDED by D-###`. The new entry records what
  changed the answer.
- **Why:** A reversed decision is more useful than a forgotten one. Without the
  record, the next agent proposes the rejected option again, and the argument is
  re-run from zero. The *reason it was reversed* is the highest-value sentence in
  the whole file.
- **Alternatives rejected:** *Overwrite in place* — loses exactly the information
  that prevents repeated work. *Git history as the archive* — real, but no agent
  will `git log -p` a context file before answering; it must be visible in the
  file being read.
- **Consequences:** `DECISIONS.md` grows monotonically. Acceptable: it is read on
  demand, not on every session, and the index at the top keeps it navigable.
- **Confidence:** [DECIDED]

### D-006 · One procedure file; tool configs are thin adapters
- **Status:** ACTIVE
- **Added:** 2026-09-04
- **Decision:** The save/resume procedure lives once in `context/_meta/SPEC.md`.
  `.claude/agents/context-keeper.md`, `.claude/commands/*`, `AGENTS.md` and
  `.cursor/rules/context-keeper.mdc` are thin pointers to it, and the spec wins
  where they disagree.
- **Why:** Four copies of a procedure become four different procedures within a
  month, and the context files would then be written to inconsistent formats by
  different tools — precisely the fragmentation the system exists to remove. A
  tool with no adapter at all still works: point it at `SPEC.md`.
- **Alternatives rejected:** *Full procedure in each tool config* — guaranteed drift.
  *A script that generates the context* — cannot read a conversation; the
  extraction step is inherently a model's job.
- **Consequences:** Adding a new AI tool costs one short adapter file. Changing the
  procedure means editing `SPEC.md` only.
- **Confidence:** [DECIDED]

### D-007 · The main assistant extracts; the subagent merges
- **Status:** ACTIVE
- **Added:** 2026-09-04
- **Decision:** `/save-context` runs in the main session, extracts a
  `## CONVERSATION BRIEF`, and passes it to the `context-keeper` subagent, which
  reads the existing files and merges. The subagent must declare it and fall back
  to repository evidence if the brief is missing.
- **Why:** A subagent is spawned in a fresh process and **cannot see the parent
  conversation**. A "context-saving subagent" invoked bare would therefore
  hallucinate a plausible session — the worst possible outcome for a file whose
  entire purpose is to be trusted by the next agent. Splitting the roles puts each
  step where the necessary information actually is.
- **Alternatives rejected:** *Subagent alone* — cannot see the conversation.
  *Main agent alone* — works, and remains the documented fallback, but spends the
  main context window on file merging.
- **Consequences:** The quality of a save depends on the brief. `/save-context`
  therefore specifies the twelve extraction categories rather than saying
  "summarize". A bare `context-keeper` invocation produces an honest, thin,
  `[UNVERIFIED]` reconstruction instead of a confident fake.
- **Confidence:** [DECIDED]

### D-008 · A no-change save writes nothing at all
- **Status:** ACTIVE
- **Added:** 2026-09-04
- **Decision:** If a save finds no material change, no file is written — not even a
  timestamp — and the agent says so.
- **Why:** The user asked for idempotence. More concretely: if every invocation
  appended a history block, `HISTORY.md` would fill with empty entries and stop
  being a usable timeline, and `git log` on `context/` would stop showing when
  things actually changed. Silence is the correct output.
- **Alternatives rejected:** *Always bump `last_save`* — makes "when did this last
  really change?" unanswerable. *Always write a session file* — same, and inflates
  `sessions/`.
- **Consequences:** "Material" needs a definition: a new, updated, or superseded
  entry, or a status change. Reformatting is not material. Stated in SPEC §4.6.
- **Confidence:** [DECIDED]
