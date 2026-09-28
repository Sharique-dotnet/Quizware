# DECISIONS

Append-only. Entries are never deleted or renumbered — a decision that turns out
wrong is marked `SUPERSEDED` and keeps its reasoning, so the next agent does not
re-propose it. Format: `_meta/SPEC.md` §6.3.

**Index:** D-001 · D-002 · D-003 · D-004 · D-005 · D-006 · D-007 · D-008 · D-009 ·
D-010 · D-011 · D-012 · D-013 · D-014 · D-015 · D-016 · D-017 · D-018 · D-019 ·
D-020 · D-021 · D-022 · D-023 · D-024 · D-025 · D-026 · D-027 · D-028 · D-029 ·
D-030 · D-031 · D-032 · D-033 · D-034 · D-035 · D-036 · D-037 · D-038 · D-039 ·
D-040 · D-041 · D-042 · D-043 · D-044

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

### D-025 · `OptionOrderJson` is computed eagerly at reservation time but not persisted onto `MatchQuestion` until Phase 9's `Activate()`
- **Status:** ACTIVE
- **Added:** 2026-09-10 (S-2026-09-10-01)
- **Decision:** `QuestionSelector.SelectAndReserveAsync` computes the shuffled
  option order (seeded from the same `SelectionRequest.RandomSeed`) and returns
  it on `SelectedQuestion.OptionOrderJson`, but does **not** write it into
  `MatchQuestion.OptionOrderJson` at reservation time. That column is only ever
  set by the domain method `MatchQuestion.Activate(...)`, per its own doc
  comment, which fires when a question is actually served live — Phase 9
  territory.
- **Why:** Reservation (Phase 8) and serving (Phase 9) are different points in
  the entity's lifecycle in the existing domain model. Writing the same value
  at both points would create two sources of truth; reusing the same seed at
  `Activate()` time reproduces the identical order without needing to persist
  it early.
- **Alternatives rejected:** Add a new `MatchQuestion.ReserveWithOptionOrder(...)`
  factory that writes `OptionOrderJson` immediately at reservation — rejected as
  unnecessary scope creep into Phase 9's own entity-lifecycle design; also risks
  the two-sources-of-truth problem above if a later `Activate()` call recomputed
  a different order (e.g. seed handling drifted).
- **Consequences:** Phase 9's match-start/serve handler must reuse the exact
  same seed (carried on `MatchQuestion`/`SelectionRequest`, needs confirming
  when Phase 9 is scoped) to reproduce the identical shuffle P8-06 requires for
  dispute resolution — flag this as a Phase 9 prerequisite check.
- **Confidence:** [DECIDED]

### D-026 · Tag filtering (`QuestionSelectionRule.TagFilterJson`) is left unimplemented in the Phase 8 selector
- **Status:** ACTIVE
- **Added:** 2026-09-10 (S-2026-09-10-01)
- **Decision:** `QuestionSelector`'s pool-building (P8-01) implements format,
  language, topic-filter, owner-scope, and approved-only filters, but not tag
  filtering, even though `TagFilterJson` already exists as a column and P8-01's
  acceptance text lists "tag filter" among the pool-building criteria.
- **Why:** Verified by grep — there is no `QuestionTag` join entity anywhere in
  the schema, no `Tag` reference on `Question.cs`, and `Tag.cs` has no
  back-reference to questions. Implementing tag filtering would require a schema
  migration (a new many-to-many join table), which is outside Phase 8's stated
  scope. This gap traces back to Phase 1 (P1-06), whose own notes record that
  `QuestionTag` was deliberately not modeled, left for "an EF many-to-many
  mapping in Phase 4" — that mapping was never actually added in Phase 4 either.
- **Alternatives rejected:** Inventing an ad-hoc `Question.TagIds` string column
  or an on-the-fly join to unblock this one filter — rejected as scope creep
  that still needs a migration and would create a throwaway shape likely to be
  redone properly later.
- **Consequences:** A future phase (or an explicit user request) needs a
  `QuestionTag` join table plus migration before tag filtering can work. Every
  other P8-01 filter (topic, owner-scope, approved-only, format) is implemented
  and tested.
- **Confidence:** [DECIDED]

### D-027 · `QuestionSelectionRule.TopicFilterJson` wired up end-to-end this session
- **Status:** ACTIVE
- **Added:** 2026-09-10 (S-2026-09-10-01)
- **Decision:** `TopicFilterJson` (an existing column since Phase 7 that
  `QuestionSelectionRule.Update()` never actually set — a dead column) is now
  an optional parameter on `Update()`, mapped through `SelectionRuleAppDto`,
  `RuleMappings.ToDto()`, `UpsertSelectionRulesCommand`, the API's
  `SelectionRuleDto` contract, and `RulesController`.
- **Why:** P8-01 explicitly requires topic filtering to work in the selector;
  without a write path, there would be nothing real for the selector to read.
  This completes Phase 7's own contract rather than adding new scope.
- **Alternatives rejected:** None seriously considered — this is a small,
  necessary fix to unblock a stated Phase 8 requirement, not a design choice
  with real alternatives.
- **Consequences:** None beyond the mapping now being complete; no new
  migration needed (column already existed).
- **Confidence:** [DECIDED]

### D-028 · `QuestionSelectionRule.Specificity` + `IRuleService.ResolveSelectionRuleAsync` — nullable, unlike the scoring-rule resolver
- **Status:** ACTIVE
- **Added:** 2026-09-10 (S-2026-09-10-01)
- **Decision:** Added `QuestionSelectionRule.Specificity` (segment beats stage
  beats program — same pattern as `ScoringRule.Specificity`) and
  `IRuleService.ResolveSelectionRuleAsync(programId, formatCode, stageId,
  segmentTemplateId, ct) -> QuestionSelectionRule?`. Unlike
  `ResolveScoringRuleAsync` (which throws `ScoringRuleNotFoundException` when
  nothing matches), this resolver returns `null` when no rule is configured.
- **Why:** Reuses the exact specificity-resolution pattern Phase 7's
  `RuleService.ResolveScoringRuleAsync` already established, rather than
  duplicating the logic in the new selector. Made nullable (not throwing)
  because a selection rule is genuinely optional — when absent, `QuestionSelector`
  falls back to `QuestionSelectionRule.Create()`'s own defaults (every
  difficulty, `NeverInProgram`, no topic spread, widen-then-fail), so a missing
  rule is not an error condition the way a missing scoring rule is (scoring
  cannot proceed without a rule; selection can, via defaults).
- **Alternatives rejected:** Making it throw like `ResolveScoringRuleAsync` for
  API symmetry — rejected because it would force every caller to catch an
  exception just to fall back to defaults, when a nullable return expresses the
  same thing more directly.
- **Consequences:** Any future caller of `ResolveSelectionRuleAsync` must
  explicitly handle the `null` case (apply defaults), unlike calls to
  `ResolveScoringRuleAsync`.
- **Confidence:** [DECIDED]

### D-029 · Cross-match reservation locking — any non-`Released` `MatchQuestion` row excludes that question from every other match's draw
- **Status:** SUPERSEDED by D-044
- **Added:** 2026-09-10 (S-2026-09-10-01) · **Updated:** 2026-09-28 (S-2026-09-28-01)
- **Decision:** A question that is `Reserved`/`Active`/`Answered`/`Skipped`
  (i.e. any `MatchQuestion.State != Released`) in **any** match's
  `MatchQuestion` row is excluded from every other draw's pool — a separate
  exclusion set (`lockedSet`, computed from `_db.MatchQuestions.Where(mq =>
  mq.State != Released)`) applied **in addition to** the
  `QuestionUsageHistory`-based repeat-policy exclusion, with an explicit
  carve-out (`IsOwnMatchReservation`) so a match drawing for its own second
  segment doesn't lock itself out of its own already-reserved question.
- **Why:** P8-07's acceptance criterion "a reserved question is unavailable to
  other matches" cannot be satisfied by `QuestionUsageHistory` alone, because
  (per that entity's own doc comment) it is written once a question is
  actually **used**, not merely reserved — without a separate lock, two
  different matches could both reserve the same question before either serves
  it.
- **Alternatives rejected:** Writing a `QuestionUsageHistory` row at reservation
  time instead of at actual use — rejected because it would conflate "reserved"
  with "used" for repeat-policy purposes (e.g. `NeverInMatch` checks), which are
  semantically different questions.
- **Consequences:** Phase 9's abandon-match flow must call
  `ReleaseReservationsAsync` (P8-10) to free locked questions back to the pool,
  or they remain permanently locked out of every future draw.
- **Confidence:** [DECIDED]

### D-030 · `DifficultyMixJson` format convention — a flat percentage map, invented this session, no prior convention existed
- **Status:** ACTIVE
- **Added:** 2026-09-10 (S-2026-09-10-01)
- **Decision:** `DifficultyMixJson` (a column that has existed since Phase 7
  but was never actually written with real content anywhere) is a flat JSON
  object mapping `DifficultyLevel` enum names to integer percentages, e.g.
  `{"Easy":60,"Hard":40}`, summing to ~100. Parsed case-insensitively;
  unrecognized keys or non-positive values are dropped; an absent/empty map
  means "no mix constraint, draw purely by weight."
- **Why:** Grepped the codebase and confirmed no existing convention — the
  column was referenced only in migrations/snapshots/DTOs, never actually
  serialized anywhere, so this was invented from scratch to unblock P8-03.
  Chose percentages over raw counts so the same rule works regardless of
  `QuestionCount` per draw.
- **Alternatives rejected:** Raw per-difficulty counts — rejected because a
  fixed-count map would need to change every time `QuestionCount` changes,
  whereas a percentage map is stable across different draw sizes for the same
  rule.
- **Consequences:** `[ASSUMED]`, not confirmed against any design doc or with
  the user — flag to the user before the Angular rule-configuration screens
  are built against this shape. See Q-006.
- **Confidence:** [ASSUMED]

### D-031 · `IQuestionSelector`'s write methods never call `SaveChangesAsync` themselves — transaction boundary belongs to the caller
- **Status:** ACTIVE
- **Added:** 2026-09-10 (S-2026-09-10-01)
- **Decision:** `SelectAndReserveAsync` adds `MatchQuestion` rows to the
  tracked `IAppDbContext`, and `ReleaseReservationsAsync` mutates existing
  ones, but neither calls `SaveChangesAsync`.
- **Why:** Matches Phase 9's own stated acceptance criterion (P9-02: "Start is
  one transaction; a failure reserves nothing") — Phase 9's future match-start
  handler will call the selector once per segment, then commit everything in
  one `SaveChangesAsync`. If the selector owned its own transaction, that
  all-or-nothing guarantee would be impossible. Also mirrors how `RuleService`
  (Phase 7) never calls `SaveChangesAsync` either — a read/resolve service —
  now extended as the convention for the selector's write path too.
- **Alternatives rejected:** Selector commits its own reservation immediately —
  rejected because it would make a multi-segment match start partially
  succeed on failure, violating P9-02 before Phase 9 even starts.
- **Consequences:** Every caller of `SelectAndReserveAsync`/
  `ReleaseReservationsAsync` (Phase 8's own tests included) must call
  `SaveChangesAsync` itself, or nothing persists — noted explicitly so Phase 9
  doesn't rediscover this by a failing test.
- **Confidence:** [DECIDED]

### D-032 · Seeded weighted draw uses a stable `OrderBy(q => q.Id)` sort before consulting the PRNG, to make "same seed -> identical draw" actually true
- **Status:** ACTIVE
- **Added:** 2026-09-10 (S-2026-09-10-01)
- **Decision:** The seeded PRNG is constructed as `new
  Random(unchecked((int)(seed ^ (seed >> 32))))` from `SelectionRequest`'s
  `long RandomSeed`, and every candidate pool is deterministically sorted
  (`OrderBy(q => q.Id)`) before being consulted by the weighted draw.
- **Why:** P8-04's acceptance criterion is "same seed -> identical draw,
  asserted in a test." EF Core does not guarantee query result ordering
  without an explicit `OrderBy` — without a stable sort first, two runs with
  the same seed could still disagree purely from nondeterministic SQL/SQLite
  row-return order, not from the PRNG itself.
- **Alternatives rejected:** Relying on natural DB return order — rejected as
  fragile and provider-dependent; verified via a new test
  (`SameSeed_ProducesIdenticalDraw`) that calls `PreviewAsync` twice with the
  same `SelectionRequest` and asserts identical `QuestionId` sequences.
- **Consequences:** Any future change to pool-building must preserve the
  `OrderBy(q => q.Id)` stable sort, or determinism silently breaks without a
  visible error.
- **Confidence:** [DECIDED]

### D-033 · `PreviewSelectionQuery` rewritten to delegate entirely into `IQuestionSelector.PreviewAsync`, superseding the Phase 7 stub
- **Status:** ACTIVE
- **Added:** 2026-09-10 (S-2026-09-10-01)
- **Decision:** The Phase-7-era `PreviewSelectionQuery` handler (which only
  reported raw pool/eligible counts by difficulty, ignoring repeat policy,
  topic filter, and mix) is rewritten to call the real
  `IQuestionSelector.PreviewAsync`, guaranteeing the preview endpoint can never
  disagree with what a real draw would produce. `SegmentTemplateId` was added
  as a new optional field on both `PreviewSelectionQuery` and the
  `SelectionPreviewRequest` API contract (previously missing — the old stub
  only took `StageId`, but rule resolution needs segment-level specificity
  too, per D-028).
- **Why:** The old stub's own doc comment said explicitly it existed only "so
  P7 configuration screens can sanity-check a rule against the current bank
  size before Phase 8 exists" — i.e. it was always meant to be superseded once
  the real selector existed, not a design this session is overriding.
- **Alternatives rejected:** Keeping the old stub alongside the new real preview
  path under a different route — rejected as needless duplication; nothing
  depended on the stub's specific (incomplete) output shape being preserved.
- **Consequences:** None negative; `SelectionPreviewRequest` callers (Postman
  collection, future Angular screens) now need to supply `SegmentTemplateId`
  for segment-specific rule resolution to work correctly.
- **Confidence:** [DECIDED]

### D-034 · Answer reversal is ProgramAdmin/SuperAdmin only — Operators may not reverse answers
- **Status:** ACTIVE
- **Added:** 2026-09-28 (S-2026-09-28-01)
- **Decision:** `POST /matches/{id}/live/answers/{aid}/reverse` is authorized
  with `Policies.CanAdjustScore` (ProgramAdmin/SuperAdmin). `[FACT]`
  `Quizware/src/Quizware.Api/Controllers/v1/LiveMatchController.cs:121`.
  `docs/new-system/05-API-Design.md` (reverse route role) and
  `docs/Implementation-Plan.md` row P9-10 were corrected to say ProgramAdmin.
- **Why:** D-013 already binds reversal, disqualification, and score
  adjustment to ProgramAdmin. The API design doc and the plan's P9-10 row had
  not caught up with D-013 and still suggested Operators could reverse; the
  open question "may Operators reverse answers?" was answered by applying the
  existing binding decision, not by a new product choice.
- **Alternatives rejected:** *Operator may reverse (as the stale docs read)* —
  contradicts D-013's explicit narrowing of reversal authority.
- **Consequences:** Operator UIs must not offer reversal. Any test or Postman
  request reversing an answer must authenticate as ProgramAdmin/SuperAdmin.
- **Confidence:** [DECIDED]

### D-035 · Turn rotation is per segment; Buzzer and RapidFire have no turn holder
- **Status:** ACTIVE
- **Added:** 2026-09-28 (S-2026-09-28-01)
- **Decision:** The turn holder for a served question is the active
  participant at index `segment.ServedQuestionCount` (mod count) over active
  participants ordered by `TurnOrder` (`Application/Gameplay/TurnRotation.cs`).
  Formats whose handler reports `AnyTeamMayAnswer` (Buzzer, RapidFire — BR-2.4)
  have no turn holder (null).
- **Why:** Business rule BR-2.2 in `docs/Implementation-Plan.md` says rotation
  restarts per segment. The first Phase 9 implementation rotated across the
  whole match, which drifted from the rule as soon as a segment had a question
  count not divisible by the team count.
- **Alternatives rejected:** *Match-wide rotation* — the original
  implementation; contradicts BR-2.2.
- **Consequences:** Any new format must declare `AnyTeamMayAnswer` correctly on
  its handler, or it will silently get (or lose) a turn holder.
- **Confidence:** [DECIDED]

### D-036 · One `IQuestionFormatHandler` per question format, discovered by assembly scan
- **Status:** ACTIVE
- **Added:** 2026-09-28 (S-2026-09-28-01)
- **Decision:** Live rendering and answer evaluation live in one handler class
  per format under `Quizware/src/Quizware.Application/Gameplay/Formats/` (Mcq,
  Buzzer, Card, Choice, Passing, TieBreaker, AudioVisual, RapidFire, Sequence,
  VisualRapidFire), with an `OptionFormatHandler` base for option-based formats
  and an `AcceptedAnswers` helper. Handlers are discovered by assembly scan and
  resolved via `QuestionFormatHandlers.For(format)`. The previous switch-based
  `LiveQuestionFormats.cs` was deleted.
- **Why:** Plan task P9-07 calls for a handler per format; one class per format
  keeps each format's rules closed to changes in the others and lets a new
  format be added without touching a central switch.
- **Alternatives rejected:** *Single switch-based `LiveQuestionFormats.cs`* —
  what shipped in PR #1; replaced because it violated P9-07 and concentrated all
  ten formats' rules in one file.
- **Consequences:** Adding a format = add a handler class; no registration
  edit needed. Format-level flags (`AnyTeamMayAnswer`, `TopicChoice`, shuffle)
  live on the handler.
- **Confidence:** [DECIDED]

### D-037 · Passing (BR-2.5): seat-order direction, min-of-two pass limit, reveal-if-all-pass
- **Status:** ACTIVE
- **Added:** 2026-09-28 (S-2026-09-28-01)
- **Decision:** A pass goes to the next active participant by seat in the
  question's `PassDirection` (Clockwise/Anticlockwise). The limit is
  `min(question.MaxPassCount, segmentTemplate.MaxPassCount)`. If every team
  passes and `RevealAnswerIfAllPass` is set, the answer is revealed and the
  question skipped. An answer after a pass must send `PassNumber` = number of
  passes so far; the outcome resolves to `PassedCorrect` (scoring context key
  `AfterPass`). `OperatorChoice` falls back to clockwise because
  `PassQuestionRequest` has no target-participant field.
- **Why:** BR-2.5 in the plan. The first Phase 9 cut ignored direction and the
  question-level limit. The OperatorChoice fallback exists only because the
  frozen contract lacks a target field (Q-007).
- **Alternatives rejected:** *Invent a target field without approval* — the
  contract is treated as frozen; changing it is the user's call (Q-007).
- **Consequences:** Clients must send `PassNumber` on post-pass answers.
  OperatorChoice is not truly supported until Q-007 is answered.
- **Confidence:** [DECIDED] (the OperatorChoice fallback part is [ASSUMED] —
  confirm via Q-007)

### D-038 · Choice round board is built from each question's TopicChoice label
- **Status:** ACTIVE
- **Added:** 2026-09-28 (S-2026-09-28-01)
- **Decision:** The Choice-round topic board groups reserved questions by the
  handler's `TopicChoice` label (from the question's TopicChoice field); the
  displayed topic name prefers that label. When an exclusive topic is played,
  other questions with the same label are released.
- **Why:** Plan's Choice-round rule: teams pick a topic; an exclusive topic is
  consumed once played. Grouping by the label (not only `TopicId`) matches how
  Choice questions are authored.
- **Alternatives rejected:** none recorded in the brief.
- **Consequences:** Choice questions without a label fall back to the topic
  name. Released questions return to the pool via the normal release path.
- **Confidence:** [DECIDED]

### D-039 · Sudden-death segments close as soon as one team leads after equal turns
- **Status:** ACTIVE
- **Added:** 2026-09-28 (S-2026-09-28-01)
- **Decision:** A segment marked sudden-death (`MatchSegment.MakeSuddenDeath`)
  closes as soon as one team leads after all teams have had equal turns
  (`Application/Gameplay/SuddenDeath.TryCloseAsync`). Marking is domain-only for
  now; no API sets it.
- **Why:** BR-5.6. Wiring the flag from configuration belongs with Phase 11's
  tie-break matches, which are the only intended user of sudden death.
- **Alternatives rejected:** *Add an API flag in Phase 9* — premature; the
  tie-break setup that should drive it doesn't exist yet.
- **Consequences:** Phase 11 must call `MakeSuddenDeath` when building a
  tie-break match whose `TieBreakRule` asks for sudden death. `[ASSUMED]` that
  Phase 11 is where this gets driven from — confirm when implementing Phase 11
  (V-010).
- **Confidence:** [DECIDED]

### D-040 · Match auto-seeding endpoint with Random / Rank / Snake modes
- **Status:** ACTIVE
- **Added:** 2026-09-28 (S-2026-09-28-01)
- **Decision:** `POST /programs/{pid}/matches/auto-seed {stageId, seedingMode}`
  groups unplaced Registered/Active teams — or the stage's committed
  `StageQualifications`, if any exist — into Draft matches, using the pure
  domain function `Domain/Tournament/MatchSeeding.Group(rankedTeams, min, max,
  mode, Random)`.
- **Why:** Plan gap item (auto-seed) left open after PR #1. Using committed
  qualifications when present lets later stages seed from earlier results.
- **Alternatives rejected:** none recorded in the brief.
- **Consequences:** Seeding logic is unit-testable in Domain; teams already
  placed in a match are skipped.
- **Confidence:** [DECIDED]

### D-041 · Transactional outbox via Application port `IMatchNotifications`
- **Status:** ACTIVE
- **Added:** 2026-09-28 (S-2026-09-28-01)
- **Decision:** Application defines `IMatchNotifications.Publish(eventType,
  programId, matchId, payload)`; Infrastructure implements it as
  `Outbox/OutboxMatchNotifications.cs`, which writes `OutboxMessage` rows into
  the same `AppDbContext` so they commit in the same transaction as the match
  change. Events emitted: `MatchStateChanged` (start), `AnswerRecorded`,
  `ParticipantRemoved`, `MatchCompleted`. Delivery (dispatcher, SignalR) is
  later work (Phase 12).
- **Why:** ADR-006 (SignalR + outbox): notifications must not be lost or sent
  for rolled-back changes. Named `IMatchNotifications`, not
  `INotificationPublisher`, to avoid clashing with MediatR's type of that name.
- **Alternatives rejected:** *`INotificationPublisher`* — name collision with
  MediatR. *Publish directly to SignalR* — not transactional.
- **Consequences:** Outbox rows accumulate until a dispatcher exists (T-024).
- **Confidence:** [DECIDED]

### D-042 · Scoring is an event-sourced ScoreEvent ledger with transactional read models
- **Status:** ACTIVE
- **Added:** 2026-09-28 (S-2026-09-28-01)
- **Decision:** `IScoringEngine` (`Application/Scoring/`) writes `ScoreEvent`
  rows and updates `TeamMatchScore`/`TeamStageScore` in the same transaction.
  Reversal writes compensating state and marks the original `IsReversed`.
  `RecalculateMatch`/`RecalculateStage` rebuild the read models from the ledger.
  Tie-break matches count toward stage totals only if
  `TieBreakRule.ScoreCountsTowardStage`. Manual adjustment and recalculation are
  ProgramAdmin-only (D-013).
- **Why:** Architecture decision (event-sourced scoring, ADR-004) plus D-011's
  requirement that tie-break points don't leak into standings by default.
  Rebuild-from-ledger makes read models disposable and auditable.
- **Alternatives rejected:** *Mutable score totals only* — rejected at design
  level (ADR-004).
- **Consequences:** Standings read stored totals, not the ledger; any code that
  writes a `ScoreEvent` must go through `IScoringEngine` or totals drift.
- **Confidence:** [DECIDED]

### D-043 · Tie-break criteria service with five criteria and a reported deciding criterion
- **Status:** ACTIVE
- **Added:** 2026-09-28 (S-2026-09-28-01)
- **Decision:** `Application/Qualification/TieBreakCriteriaService`
  (`ITieBreakCriteriaService`) orders tied teams by configured criteria:
  `TotalScore`, `FewerIncorrect`, `MoreCorrectAtHighDifficulty` (alias
  `HigherDifficultyCorrect`), `FasterAverageBuzzTime`, `HeadToHead`, and reports
  which criterion decided. Built on the Phase 1 pure evaluator
  (`TieBreakCriteriaEvaluator`).
- **Why:** D-011's phase 1 (non-playing criteria) of tie-breaking; the alias
  exists because both names appear in docs/config.
- **Alternatives rejected:** none recorded in the brief.
- **Consequences:** Phase 11 qualification uses this before falling back to a
  tie-break match.
- **Confidence:** [DECIDED]

### D-044 · Question selector locked set: Reserved/Active in any match, or non-released in the same match
- **Status:** ACTIVE
- **Added:** 2026-09-28 (S-2026-09-28-01)
- **Decision:** A question is excluded from a draw if it is `Reserved` or
  `Active` in **any** match, or in **any non-Released** state in the **same**
  match. Supersedes D-029.
- **What changed the answer:** D-029 locked every non-Released state across all
  matches and carved out the drawing match's own reservations
  (`IsOwnMatchReservation`). `[FACT]` (brief) Phase 9 hit same-match duplicate
  draws and re-draws of already-answered questions; `[UNVERIFIED]` that the
  own-match carve-out was the specific cause — check the diff of
  `Application/Selection/QuestionSelector.cs` in this session's commits. Answered/Skipped questions in *other* matches are governed
  by the repeat policy via `QuestionUsageHistory`, not by the lock.
- **Why:** Keeps D-029's goal (a reserved question is unavailable to other
  matches, P8-07) while fixing the duplicate/re-draw bug; regression tests
  added to `SelectionEngineTests`.
- **Alternatives rejected:** *D-029 as written* — produced the bug above.
- **Consequences:** Abandoned matches must still release reservations (P8-10),
  or questions stay locked.
- **Confidence:** [DECIDED]
