# DECISIONS

Append-only. Entries are never deleted or renumbered — a decision that turns out
wrong is marked `SUPERSEDED` and keeps its reasoning, so the next agent does not
re-propose it. Format: `_meta/SPEC.md` §6.3.

**Index:** D-001 · D-002 · D-003 · D-004 · D-005 · D-006 · D-007 · D-008 · D-009 ·
D-010 · D-011 · D-012 · D-013 · D-014 · D-015 · D-016 · D-017 · D-018 · D-019 ·
D-020 · D-021 · D-022 · D-023 · D-024

---

## Where the system-architecture decisions live

The twelve architecture decisions for Quizware — modular monolith, Clean
Architecture, shared schema with `ProgramId`, Table-Per-Type questions,
configuration-driven tournament, event-sourced scoring, buzzer behind a port,
agent-pushes-to-API, EF Core 10 code-first, SignalR, `OrderIndex` segment
ordering, tie-break as an ordinary match — are recorded **with their rejected
alternatives** in `docs/new-system/02-Architecture-Proposal.md` §2.16.

They are not duplicated here. See **D-001** for why.

`[FACT]` Those decisions are design-stage; several of the highest-impact points
that used to be Phase 0 open questions now have documented answers baked directly
into the design docs as of S-2026-09-04-02 — see **D-009** through **D-014** below
for the reasoning behind Table-Per-Type questions, configurable segment order, and
tie-break-as-an-ordinary-match specifically (three of the twelve §2.16 items,
given their own entries here because their rejected alternatives are worth
keeping close to the reasoning). Real-world stakeholder sign-off on the roadmap's
remaining Phase 0 assumptions has still not happened — see Q-001 in `TASKS.md`.

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

### D-009 · Questions are Table-Per-Type, not one shared table
- **Status:** ACTIVE
- **Added:** 2026-09-04
- **Decision:** One shared `Question` base table carries identity, lifecycle, and
  classification columns common to all ten formats. Each format (MCQ, Buzzer,
  Passing, Card, Choice, Sequence, AudioVisual, RapidFire, VisualRapidFire,
  TieBreaker) gets its own child table holding only that format's fields. Six of
  the ten formats share one `QuestionOption` child table since their options are
  byte-identical in shape; `Sequence` and `VisualRapidFire` get their own item
  tables (`SequenceItem`, `VisualRapidFireItem`) because their items genuinely
  differ. Full schema: `docs/new-system/04-Database-Schema.md`.
- **Why:** The user pushed back on an earlier single-table design: different
  question formats have genuinely different required fields, and a shared table
  cannot express that difference as a constraint. Concrete example:
  `AudioVisualQuestion.MediaAssetId` and `.AnswerText` need to be NOT NULL — a
  guarantee impossible to state in a table that also holds MCQ or Sequence rows,
  where those columns must be nullable.
- **Alternatives rejected:** *Single `Question` table with nullable format-specific
  columns* — the original design; rejected because NOT NULL constraints that only
  apply to one format become unenforceable, pushing validation into application
  code where it can be forgotten. *A JSON/EAV column per format* — considered
  implicitly rejected by choosing typed child tables; not discussed in detail in
  the brief, so not recorded as weighed.
- **Consequences:** Every layer that touches questions needs one route/endpoint
  per format (e.g. `POST /questions/mcq` vs `POST /questions/audio-visual`) rather
  than one generic endpoint — this is now baked into `05-API-Design.md`, the PRD's
  functional requirements, and `Implementation-Plan.md` Phase 9's sub-tasks and
  Phase 16's legacy migration mapping.
- **Confidence:** [DECIDED]

### D-010 · Question-type order is configurable at three levels, with a fixed default
- **Status:** ACTIVE
- **Added:** 2026-09-04
- **Decision:** `StageSegmentTemplate.OrderIndex` is the single source of truth for
  the order question formats are played within a stage. Three levels can override
  it: the stage template default, a per-match override, and a live reorder of
  pending (not-yet-played) segments during a match. `SegmentOrderMode` has three
  values: `Fixed` (default — respects `OrderIndex` as configured), `RandomPerMatch`
  (shuffled from the match's stored RNG seed, so it is reproducible), and
  `OperatorChoice` (the operator picks live). `IsOrderLocked` can pin a segment so
  it is excluded from live reordering.
- **Why:** User's own words: "the Order of Question type should also be easily
  configurable." Finding 1.4h in `01-Analysis-Findings.md` documents why this
  matters: the legacy system hardcodes the running order as a literal
  `window.location.href` redirect chain repeated across 108 Razor views (e.g.
  `MatchOneMCQ.cshtml:153` jumps to `MatchOneAudioVisual`) — changing the order for
  one event means editing view code.
- **Alternatives rejected:** *A single fixed order per program, no per-match
  override* — does not cover the operator wanting to reorder live if, say, an
  AV asset fails to load; the three-level design was chosen specifically to cover
  that case without inventing new gameplay code.
- **Consequences:** Match-start logic must resolve `OrderIndex` through all three
  levels and respect `IsOrderLocked`; the match's RNG seed must be persisted
  before segment order is derived, or `RandomPerMatch` cannot be reproduced for
  audit/replay.
- **Confidence:** [DECIDED]

### D-011 · Tie-break runs as an ordinary Match through the existing engine
- **Status:** ACTIVE
- **Added:** 2026-09-04
- **Decision:** Two-phase tie-break at the League wildcard boundary. Phase 1:
  ordered, configurable, non-playing criteria (total score, fewer incorrect
  answers, harder questions answered, faster buzz time, head-to-head). Phase 2 (if
  still tied): create an ordinary `Match` with `MatchKind = TieBreak` containing
  only the tied teams, configured by a new `TieBreakRule` table — format defaults
  to MCQ but is configurable to any of the 10 formats, plus question count,
  difficulty, sudden-death flag, max rounds, and a fallback rule if still
  unresolved. New tables: `TieBreakRule`, `TieBreakEvent`, `TieBreakParticipant`.
  `ScoreCountsTowardStage` defaults to `false` — a tie-break match decides
  qualification order, not points.
- **Why:** User's own words: "if there is a tie in scores... there should be tie
  breaker round and mostly it should be MCQ type but it should also be
  configurable." Running it through the existing match engine — rather than
  writing separate tie-break gameplay code — was a deliberate simplification: a
  `Match` already knows how to run any of the 10 formats, so a tie-break is just a
  `Match` with a different `MatchKind` and a smaller participant list. Finding
  1.4i in the analysis doc documents the legacy gap this replaces: a `TieBreaker`
  table/screens exist but are completely disconnected from qualification, and
  `LeagueRoundScore` currently resolves ties by database row order.
- **Alternatives rejected:** *Dedicated tie-break gameplay module* — rejected as
  unnecessary duplication once it was clear the existing match engine already
  supports arbitrary formats; a second code path for "matches that happen to be
  tie-breaks" would double the surface area to test and diverge over time.
- **Consequences:** Reporting and scoring code must be able to tell a tie-break
  match apart from a stage match (`MatchKind`) and must respect
  `ScoreCountsTowardStage = false` so tie-break points do not leak into league
  standings.
- **Confidence:** [DECIDED]

### D-012 · Question formats are optional at three independent levels
- **Status:** ACTIVE
- **Added:** 2026-09-04
- **Decision:** "Optional" has three distinct, non-conflatable meanings, all
  supported: (1) **not-configured** — no `StageSegmentTemplate` row for that
  format; the common case, already true by construction since a stage only plays
  the segments it is configured with. (2) **skippable-on-the-night** —
  `StageSegmentTemplate.IsOptional = 1`; the segment is still drawn but can be
  skipped during play. (3) **disabled-program-wide** — new
  `ProgramQuestionFormat.IsEnabled = 0`; a per-program admin-screen toggle that
  only hides a format from configuration UI, guarded by a `FORMAT_IN_USE` error if
  a segment template still references a format someone tries to disable.
- **Why:** User's words: "does all question types compulsory? they should be
  optional... if we don't want passing so we will not configure it or leave it
  blank." Verifying this surfaced one real gap rather than a design flaw: PRD
  `FR-1.5` readiness validation read as if a stage needed "enough questions to
  satisfy every selection rule," which could be misread as requiring all ten
  formats to have question-bank content regardless of use. Tightened to check
  only formats actually referenced by that stage's segment templates.
- **Alternatives rejected:** none recorded — this was confirmation-plus-one-fix,
  not a alternatives-weighing decision.
- **Consequences:** Any future code or documentation that says a question format
  is "optional" must say which of the three senses it means; the ambiguity is
  exactly what caused the `FR-1.5` near-miss.
- **Confidence:** [DECIDED]

### D-013 · Judge role removed; ProgramAdmin/SuperAdmin hold sole authority over oversight actions
- **Status:** ACTIVE
- **Added:** 2026-09-04
- **Decision:** The system has exactly **7 roles**: `SuperAdmin`, `ProgramAdmin`,
  `QuestionAuthor`, `Operator`, `Scorer`, `Display`, `Auditor`. There is no `Judge`
  role. `ProgramAdmin` (or `SuperAdmin`) holds sole approval authority over
  disqualification, answer reversal, manual score adjustment, and tie-break manual
  resolution.
- **Why:** Arrived at incrementally across four separate user requests in one
  conversation, each narrowing authority further: disqualification approval
  narrowed to ProgramAdmin-only (was Judge-or-ProgramAdmin); answer-reversal and
  score-adjustment approval narrowed to ProgramAdmin-only (were
  Operator-or-Judge and Judge-or-ProgramAdmin respectively); tie-break manual
  resolution narrowed to ProgramAdmin-only (was ProgramAdmin-or-Judge); then an
  explicit final instruction, "Drop Judge from the system entirely." The pattern
  across all four steps was the same: a role that existed for oversight
  duplicated authority ProgramAdmin already had, without a distinct
  responsibility of its own.
- **Alternatives rejected:** *Keep Judge for a subset of actions* (the state after
  steps 1–3, before step 4) — rejected in the final step because a role with a
  shrinking, inconsistent set of powers is confusing to configure and audit; full
  removal is simpler than a partial role.
- **Consequences:** Every place a role list, permission matrix, or policy table
  existed had to be swept: PRD actors table and permission matrix, API design
  endpoint role columns and the `CanDisqualify`/`CanAdjustScore`/`CanResolveTie`
  policy table in `05-API-Design.md` §5.9 (a `CanResolveTie` row was missing and
  got added while doing this sweep), the seeded `AppRole` list and column comments
  in `04-Database-Schema.md`, roadmap open-questions rows 8/8a/6b and the
  authorization test additions to Phase 9–11 deliverables, one stray unrelated
  "a remote judge" wording in the architecture doc reworded to "a remote
  reviewer" to avoid confusion, and the README. `[FACT]` Verified present in the
  current repo: `docs/new-system/06-Development-Roadmap.md:296-299,586-590` and
  `docs/new-system/05-API-Design.md:1401-1405`.
- **Confidence:** [DECIDED]

### D-014 · Local/on-premises hosting assumed; buzzer device count configurable, default 3
- **Status:** ACTIVE
- **Added:** 2026-09-04
- **Decision:** API hosting is a **local/on-premises venue server** — no cloud
  dependency assumed. The buzzer agent still pushes outward to the API even
  though everything is local, because the operator PC's exact network position
  relative to the server is not guaranteed. Separately, the number of buzzer
  devices is **configurable, defaulting to 3** (today's hardware count) — added
  as a `DeviceCount` config setting in the architecture doc's buzzer JSON config
  example and in the `BuzzDeviceMapping` discussion. The default is explicitly a
  seed value, not a hard limit.
- **Why:** Both answered via a user screenshot of `06-Development-Roadmap.md` §6.5
  showing partially-visible answers to its open-questions table: open question 11
  (hosting) and open question 12 (device count). The "configurable with a sensible
  default" shape mirrors the same pattern the user applied to the tie-break
  format (defaults to MCQ, but configurable) — this appears to be a consistent
  house preference, not a one-off (see `PROJECT.md` §Conventions).
- **Alternatives rejected:** *Cloud-hosted API* — not chosen; the on-premises
  assumption changed the architecture doc's physical topology diagram and prose.
  *Hardcoded device count of 3* — rejected in favor of a configurable value with 3
  as the default, consistent with the house style.
- **Consequences:** No SignalR scale-out design is needed for a multi-server
  farm (single on-prem server); the buzzer agent's HTTP client must handle the
  server being reachable but not co-located, so it cannot assume localhost.
  `V-004` (previously `[ASSUMED]`, unconfirmed) is now `[DECIDED]` — see
  `TASKS.md` Closed section.
- **Confidence:** [DECIDED]

### D-015 · Phase 0 is owner-confirmed, not run as a stakeholder workshop
- **Status:** ACTIVE
- **Added:** 2026-09-04 (S-2026-09-04-03)
- **Decision:** `docs/Implementation-Plan.md` Phase 0 ("Requirements confirmation")
  is treated as **skipped-by-substitution**: the six tasks that assumed a
  workshop with organisers/quiz-masters (`P0-01`–`P0-06`) do not apply, because
  the user is the sole owner and stakeholder of Quizware — there is no separate
  organiser or quiz-master to convene. The 16 assumptions listed in
  `docs/new-system/06-Development-Roadmap.md` §6.5 are accepted as-is and are
  now to be treated as **`[DECIDED]`**, not `[ASSUMED]`. This answers `Q-001` in
  `TASKS.md`.
- **Why:** A workshop-style confirmation process assumes a party distinct from
  the person building the system, whose sign-off is worth waiting for. That
  party does not exist here — the user reviewing the docs one-on-one with an AI
  *is* the sign-off, since there is nobody else with standing to disagree.
  Re-running "confirm with the organisers" as a literal task would block Phase 1
  indefinitely on a meeting that cannot happen.
- **Alternatives rejected:** *Leave Phase 0 as an unstarted blocking phase* —
  rejected because it would stall Phase 1 (domain model) forever waiting on a
  workshop the project structurally cannot hold. *Silently treat the
  assumptions as decided without recording why* — rejected because a future
  session (or a future actual stakeholder, e.g. a co-organiser brought on
  later) needs to see that these were accepted by the owner, not independently
  verified against real event operations.
- **Consequences:** All 16 rows in §6.5 move from `[ASSUMED]` to `[DECIDED]`
  outcome-wise, though the residual genuinely-open sub-points tracked in
  `TASKS.md` T-001/Q-001 (shared vs per-program question bank, disqualified-team
  score handling, one-match-per-stage-or-not) still need an explicit answer —
  "owner-confirmed" resolves *who* signs off, not the content of every still-
  blank row. **Known inconsistency, flagged for follow-up:** the edit that was
  meant to rewrite `docs/Implementation-Plan.md`'s Phase 0 section into a short
  "SKIPPED — owner-confirmed" note did not persist — verified `[FACT]` this
  session (S-2026-09-04-03) that the file on disk still reads with the original
  six-task workshop wording (`P0-01`–`P0-06`, lines 73–88). The decision recorded
  here is the one that stands (repository content that merely restates an
  already-superseded plan does not un-decide anything), but the doc text is
  stale relative to it and should be re-edited to match. See `TASKS.md` for the
  follow-up task.
- **Confidence:** [DECIDED]

### D-016 · `docs/` is entirely gitignored; design docs and ADRs live on disk but outside git
- **Status:** ACTIVE
- **Added:** 2026-09-07 (S-2026-09-07-01)
- **Decision:** The whole `docs/` directory (design docs 01–06, both legacy
  technical analyses, `Implementation-Plan.md`, and the Phase 2 ADRs in
  `docs/adr/`) is listed in `.gitignore` and is **not tracked by the outer git
  repository** — verified `[FACT]`: `.gitignore` contains a bare `docs/` line,
  and `git show --stat` on commit `0df2bd2` ("Update docs: schema, API, roadmap,
  and process clarified") shows every file under `docs/` being removed from the
  git index in the same commit that added the `docs/` ignore line.
- **Why:** `[UNVERIFIED]` — no brief or conversation record explains the
  reasoning; this was made out-of-band (the user edits `docs/` directly across
  sessions and evidently decided it should not be version-controlled the same
  way code is). Recorded here as a `[FACT]` about repository behavior, not as a
  reasoned decision this context system was party to.
- **Alternatives rejected:** not known — see above.
- **Consequences:** `git log`/`git show` will never show design-doc or ADR
  changes. Anyone reconstructing project history from git alone will think
  Phase 2 (ADRs) never happened; always check `docs/adr/` on disk directly, not
  just git history, before concluding a phase's paperwork doesn't exist. This
  also means `docs/` content can change or vanish without any commit trail —
  treat its current on-disk state as the only source of truth, with no git-based
  diff/blame available to recover an earlier version.
- **Confidence:** [DECIDED] (the behavior is `[FACT]`; the reasoning behind it is
  `[UNVERIFIED]` — ask the user if it matters later)

### D-017 · Swashbuckle 10.x needs explicit wiring to emit polymorphic OpenAPI schemas
- **Status:** ACTIVE
- **Added:** 2026-09-07 (S-2026-09-07-01)
- **Decision:** Every controller action that returns a response DTO must be
  typed `ActionResult<TResponse>` (never bare `IActionResult`), and the
  `QuestionResponse` discriminated union's 10 subtypes are explicitly registered
  via `options.SelectSubTypesUsing(...)` in Swagger setup, alongside
  `UseOneOfForPolymorphism()` and `SelectDiscriminatorNameUsing(_ => "formatCode")`.
- **Why:** Swashbuckle 10.2.3 (targeting a newer major `Microsoft.OpenApi`) does
  not read `[JsonPolymorphic]`/`[JsonDerivedType]` attributes automatically, and
  cannot infer any response schema at all from a bare `IActionResult` return
  type. Verified by diffing the generated `openapi.v1.json` before/after the fix:
  before, `QuestionResponse` had no schema in most responses; after,
  `GET /questions/{id}` correctly shows `oneOf` referencing all 10 per-format
  schemas with `discriminator.propertyName: "formatCode"`.
- **Alternatives rejected:** *Rely on the JSON attributes alone* — silently
  produces an empty/wrong schema with no build-time warning; discovered only by
  inspecting the generated OpenAPI document directly. *Switch OpenAPI generator
  entirely* — not pursued; the fix was cheap once the gap was found.
- **Consequences:** Any future polymorphic response type needs the same two-part
  treatment (typed `ActionResult<T>` + explicit `SelectSubTypesUsing`). A stub
  action returning `StatusCode(501)` still satisfies `ActionResult<TResponse>`
  via its implicit conversion, so this cost nothing functionally in Phase 5.
- **Confidence:** [DECIDED]

### D-018 · NSwag, not openapi-generator-cli, generates the TypeScript client
- **Status:** ACTIVE
- **Added:** 2026-09-07 (S-2026-09-07-01)
- **Decision:** `Quizware/clients/typescript/quizapp-api-client.ts` is generated
  with `NSwag.ConsoleCore` (`nswag openapi2tsclient`), a pure-.NET global dotnet
  tool, not the Java-based `openapi-generator-cli`.
- **Why:** `openapi-generator-cli` could not run in this environment — no JVM
  installed. NSwag needed no additional runtime and produced an equivalent
  discriminated-union TypeScript client (`McqQuestionResponse extends
  QuestionResponse`, etc., confirmed present in the generated file).
- **Alternatives rejected:** *Install a JVM just for this tool* — an unnecessary
  environment dependency when a pure-.NET alternative exists and produces
  compatible output.
- **Consequences:** Any future regeneration of the TypeScript client from
  `docs/openapi.v1.json` should use the same NSwag command, not
  openapi-generator-cli, unless a JVM becomes available and there's a concrete
  reason to switch.
- **Confidence:** [DECIDED]

### D-019 · MediatR/Application only for Domain-typed entities; Infrastructure-only entities go straight from controller to `AppDbContext`
- **Status:** ACTIVE
- **Added:** 2026-09-08 (S-2026-09-08-01)
- **Decision:** When a phase's core entities are Domain types exposed on
  `IAppDbContext` (`Program`, `Team`, `Topic`, `Tag`, `Question`), business
  logic goes through MediatR command/query handlers in `Quizware.Application`.
  When the entities are Infrastructure-only types — ASP.NET Core Identity's
  `AppUser`/`AppRole`/`ProgramUser` (Phase 6b), or `ImportBatch`/
  `ImportBatchRow` (Phase 6c/6f's Excel import) — business logic is written
  directly in the controller action, injecting the concrete `AppDbContext`.
- **Why:** `Architecture.Tests` enforces that `Quizware.Application` may only
  reference `Quizware.Domain`, never `Quizware.Infrastructure`. Identity types and
  import-batch types are deliberately Infrastructure-only (not modeled in
  Domain), so a MediatR handler for them is structurally impossible without
  breaking that rule. This mirrors a pattern already present before this
  session: `AuthController.Login`/`Refresh`, built in Phase 3 before MediatR
  existed in this codebase at all.
- **Alternatives rejected:** *Model Identity/import-batch state as Domain types
  just so they can go through MediatR* — rejected; would blur the boundary
  between "business domain" and "framework/infrastructure concern" that the
  Clean Architecture split exists to keep, for entities (ASP.NET Identity
  tables, transient import staging rows) that are not really domain concepts.
- **Consequences:** A future phase must check which side of this split its
  entities fall on *before* choosing an implementation shape — do not assume
  every controller gets a MediatR handler. Team import (6c) and MCQ import (6f)
  both still reuse the Domain-side `CreateTeamCommand`/`CreateMcqQuestionCommand`
  via `ISender` for the actual per-row entity creation, so business rules
  cannot drift between the single-create and bulk-import paths even though the
  import bookkeeping itself bypasses MediatR.
- **Confidence:** [DECIDED]

### D-020 · Question editing has no separate Update handler — Create is reused with an optional `ReplacesQuestionId`
- **Status:** ACTIVE
- **Added:** 2026-09-08 (S-2026-09-08-01)
- **Decision:** All 10 `CreateXxxQuestionCommand` records gained an optional
  `Guid? ReplacesQuestionId = null`. `PUT /questions/{formatCode}/{id}`
  deserializes its body into the same per-format Create request shape and
  invokes the same Create command with `ReplacesQuestionId` set. There is no
  separate "Update" command or handler for any of the 10 formats.
  `QuestionCommon.ResolveReplacementTargetAsync` + `.ApplyVersioning` do the
  shared work: verify the old question exists and matches format, link
  `SupersedesQuestionId` + increment `Version`, then soft-delete the old row if
  `TimesUsed == 0` or retire it if used.
- **Why:** PRD FR-3.10: "a question used in a live match shall not be editable;
  a new version shall be created instead." Routing both Create and Update
  through one code path guarantees they validate identically — a question
  created via `POST` and one created via `PUT` (as a new version) can never
  diverge in what's accepted, since it is literally the same handler.
- **Alternatives rejected:** *A separate `UpdateXxxQuestionCommand` per format*
  — rejected as the more conventional REST shape, but it would double the
  handler count (20 instead of 10) and create an ongoing risk of the two paths'
  validation rules drifting apart over time — exactly the failure mode FR-3.10's
  versioning requirement exists to prevent.
- **Consequences:** Any future change to a format's validation rules only needs
  to touch one handler per format. Live-verified both branches this session:
  updating an unused question makes the old id 404 after the update; updating a
  used question (forced via `RecordUsage()` in a test DB scope) leaves the old
  id returning 200 with `Status = Retired`.
- **Confidence:** [DECIDED]

### D-021 · Media validation limits (extensions, size cap, magic bytes) are this implementation's own numbers, not sourced from any doc
- **Status:** ACTIVE
- **Added:** 2026-09-08 (S-2026-09-08-01)
- **Decision:** `MediaValidation.cs` allows exactly these extensions —
  jpg/jpeg/png/gif/mp3/wav/mp4/webm — mapped to `MediaKind`, checks
  magic-byte signatures per extension (PNG `89 50 4E 47`, JPEG `FF D8 FF`, MP4
  `ftyp` at offset 4, etc.), and caps upload size at 25 MB.
- **Why:** P6-15's acceptance criterion ("extension allow-list, magic-byte
  check, size cap") names the *mechanisms* but not the specific list, byte
  signatures, or numeric cap — none of `docs/new-system/**` specifies them.
  These values were chosen as deliberately conservative defaults to make the
  acceptance criterion concretely testable, not transcribed from a
  requirement.
- **Alternatives rejected:** none weighed explicitly — this was "pick a
  reasonable default to unblock the acceptance test," not a considered
  trade-off between named alternatives.
- **Consequences:** Treat the extension list, the 25 MB cap, and the magic-byte
  table as `[ASSUMED]` product requirements until the user confirms them. If a
  real event needs a larger media file (e.g. a longer AV clip) or a format not
  on this list, the cap/list needs to be revisited, not treated as a fixed
  constraint copied from a spec.
- **Confidence:** [ASSUMED] — confirm with the user before relying on the exact
  numbers.

### D-022 · Phase 6f's Excel import is scoped to MCQ only; the other 9 formats are deferred
- **Status:** ACTIVE
- **Added:** 2026-09-08 (S-2026-09-08-01)
- **Decision:** `P6-19` ("per-format Excel import with validation report") is
  delivered only for MCQ (`McqQuestionExcelParser.cs`, column template
  `QuestionText, DifficultyLevelId, Language, Option1..4, Option1..4Correct` —
  also not documented anywhere, invented for this implementation). The other 9
  formats' import parsers do not exist yet. Everything else in Phase 6f
  (Create, versioning, approval, delete-guard, coverage, duplicates) **was**
  delivered for all 10 formats, since those operations turned out to be
  format-agnostic once the shared plumbing (`QuestionCommon.cs`,
  `QuestionMapper.cs`) existed.
- **Why:** Explicitly borrowed the project's own documented strategy from
  Phase 9's roadmap entry — "build MCQ fully first, then add the remaining nine
  formats one at a time" — cited directly to the user as the justification for
  this scoping, rather than an unstated shortcut. Each additional format's
  import parser is mechanical repetition of the same pattern (a column
  template + a call into that format's `CreateXxxQuestionCommand`), not a design
  problem, so it was deferred rather than built speculatively.
- **Alternatives rejected:** *Build all 10 formats' import parsers now* —
  rejected as unnecessary upfront work; nothing about the remaining 9 is
  expected to be harder than MCQ's, so there's no design risk in deferring them.
- **Consequences:** A future session adding format N's import support should
  follow `McqQuestionExcelParser.cs`'s exact pattern (Infrastructure-only
  parser class + `TeamsController`/`QuestionsController`'s validate/report/
  commit controller code, reusing that format's `CreateXxxQuestionCommand` via
  `ISender` for the commit step) rather than inventing a new import mechanism.
- **Confidence:** [DECIDED]

### D-023 · Reordering a unique-OrderIndex list needs a two-phase reindex, never direct final values in one pass
- **Status:** ACTIVE
- **Added:** 2026-09-08 (S-2026-09-08-02)
- **Decision:** ReorderStagesCommandHandler and ReorderSegmentTemplatesCommandHandler
  write a temporary offset value (100_000 + i) to every row's OrderIndex in one
  SaveChangesAsync, then the real final values in a second SaveChangesAsync,
  rather than writing final values directly in one pass.
- **Why:** Stage.OrderIndex (unique per ProgramId) and StageSegmentTemplate.OrderIndex
  (unique per StageId) are both enforced by a unique DB index. EF Core issues one
  UPDATE per changed row in whatever order the change tracker picks, so reordering
  [A,B,C] to [B,C,A] can produce a row briefly holding another row's about-to-be-
  vacated index mid-batch. Both SQL Server and the SQLite test provider check
  unique indexes per-statement, not deferred to end-of-transaction, so a direct
  single-pass write throws a constraint violation on some permutations depending
  on tracked-row order - caught by a failing integration test before reaching a
  human tester.
- **Alternatives rejected:** Defer constraint checking to end-of-transaction - not
  supported by SQLite (the test provider) as configured; would be a bigger, riskier
  change than a two-phase write. Sort the update order to avoid collisions -
  fragile and permutation-dependent; the temporary-offset approach is correct for
  every permutation unconditionally.
- **Consequences:** Any future "reorder a unique-ordered list" feature should reuse
  this same two-phase-write pattern, not a single-pass write of final values.
- **Confidence:** [DECIDED]

### D-024 · Segment-template reorder: a locked segment keeps its original slot; unlocked segments fill in around it from the caller's requested order
- **Status:** ACTIVE
- **Added:** 2026-09-08 (S-2026-09-08-02)
- **Decision:** ReorderSegmentTemplatesCommandHandler requires the full ordered
  list of segment ids (rejects a partial list with 400 VALIDATION_FAILED).
  Algorithm: walk target slots 0..n-1; if the segment that originally occupied
  slot i has IsOrderLocked = true, it stays in slot i regardless of where the
  caller's request places it; otherwise the next unlocked id from the request's
  queue (in the order the caller supplied) fills slot i.
- **Why:** IsOrderLocked (a StageSegmentTemplate column since Phase 4) needs the
  property implied by its name - "excluded from reordering" - to actually hold at
  the one place segments are ever reordered in bulk. A locked segment silently
  moving because the caller listed it in a different position would defeat the
  column's purpose.
- **Alternatives rejected:** Reject the whole request if a locked segment's
  requested position differs from its current one - considered but not chosen;
  picked the more forgiving "locked segments are simply excluded from
  repositioning" semantics instead, since it lets a caller resubmit the full
  current order (including locked segments in their current slots) without
  needing to compute which slots are locked itself.
- **Consequences:** Any UI building a drag-and-drop reorder for segment templates
  must either disable dragging locked segments, or accept that dragging one has no
  effect on the persisted order (silently ignored, not rejected) - worth a UX note
  when the Angular front end gets built.
- **Confidence:** [DECIDED]
