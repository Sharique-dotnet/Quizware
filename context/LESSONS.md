# LESSONS

Approaches that failed, bugs, and environment traps. **Read this before proposing
an approach** — it is the list of things that already cost someone time.
Format: `_meta/SPEC.md` §6.5.

**Last updated:** 2026-09-08 (S-2026-09-08-01)

---

### L-001 · Large quoted heredocs fail in the Bash tool on this machine
- **Added:** 2026-09-04
- **Tried:** Writing a ~330-line Markdown file with
  `cat > file <<'EOF' … EOF` through the Bash tool (Git Bash on Windows).
- **Result:** `bash: -c: line 131: unexpected EOF while looking for matching '` —
  the shell lost the heredoc partway through the body. The file was not created.
  Small heredocs and ordinary commands work fine; the failure appears with long
  multi-line content.
- **Root cause:** `[UNVERIFIED]` Most likely line-ending or length handling in the
  Windows Git Bash invocation path rather than the Markdown content itself — the
  same text written via the Write tool succeeded unchanged, which rules out a
  stray delimiter in the body.
- **Instead:** Use the Write tool for any file over roughly 50 lines. Keep Bash for
  commands, short appends, and inspection.
- **Still true?** Until someone verifies the root cause and confirms a fix. If a
  large heredoc succeeds, note it here rather than deleting this entry.

### L-002 · A "context-saving subagent" cannot see the conversation it is saving
- **Added:** 2026-09-04
- **Tried:** The obvious design — one subagent, invoked bare, that reads the chat
  and writes `context/`.
- **Result:** Would fail silently and badly. A subagent is spawned in a fresh
  process with no access to the parent conversation, so asked to "save the
  context" with nothing but a repo, a capable model produces a *plausible*
  session record. For a file whose entire value is that the next agent trusts it,
  a confident fabrication is the worst possible output.
- **Root cause:** `[FACT]` Subagents start cold; only the main assistant holds the
  conversation.
- **Instead:** Split the roles (D-007). The main session extracts a
  `## CONVERSATION BRIEF`; the subagent merges it into the files. When the brief
  is missing, the subagent must say so and fall back to repository evidence with
  everything tagged `[UNVERIFIED]` — an honest thin record, never a confident
  invented one.
- **Still true?** Yes, unless a platform starts passing conversation history to
  subagents. Applies to every AI tool with a delegation feature, not just Claude.

### L-003 · Duplicating design docs into context is a trap
- **Added:** 2026-09-04
- **Tried:** Considered summarizing `docs/new-system/` (~5,500 lines, six
  documents) into `context/PROJECT.md` so an arriving agent would have everything
  in one place.
- **Result:** Rejected before implementation. Two problems: the summary becomes a
  second source of truth that drifts from the documents the moment either is
  edited, and a summary of a specification is a worse specification — the detail
  removed is exactly the detail that makes it usable.
- **Root cause:** `[FACT]` Design docs are already committed to the repo and are
  not at risk of loss. `context/` exists to preserve what only lives in a chat
  window.
- **Instead:** Point with a path **and** a section number (D-001). Reserve
  `context/` for conversation-only knowledge: reasoning, assumptions, dead ends,
  preferences, and current state.
- **Still true?** Yes while the docs stay in the repo. If design detail ever moves
  somewhere an agent cannot read, that content must be pulled into `context/`.

### L-004 · A conversation brief's claims about git/commit state can be stale by save time
- **Added:** 2026-09-04 (S-2026-09-04-03)
- **Tried:** Trusted a brief's claim at face value — "all of this session's work
  is uncommitted, only an earlier task's commit exists" — before checking
  `git log` directly.
- **Result:** Would have written `TASKS.md`/`CURRENT.md` with a false "nothing is
  committed" state. `git log` showed the opposite: six commits existed beyond
  the one the brief named, covering exactly the work described as uncommitted,
  each authored directly by the user. The user evidently ran the commits outside
  the visible conversation (consistent with the "AI proposes a message, user
  runs `git commit`" workflow in `CLAUDE.md`'s Instructions section), and the
  brief simply hadn't caught up.
- **Root cause:** `[FACT]` A brief describes the conversation as the reporting
  agent last saw it. Git state can change through action the agent doesn't
  witness (the user committing directly, a separate terminal, another tool).
  Commit/staging state is exactly the kind of claim that is cheap to verify and
  expensive to get wrong, because everything downstream (`TASKS.md`'s "commit
  this" tasks, `CURRENT.md`'s "not yet committed" warnings) inherits the error.
- **Instead:** Always run `git status --short`, `git log --oneline -15`, and
  `git diff --stat` before writing any claim about what is or isn't committed —
  per the context-keeper's own Step 0/§4.0b instructions — rather than
  transcribing the brief's account of it. This applies even when the brief
  sounds confident and specific.
- **Still true?** Yes, structurally — this is not specific to this project, it
  follows from the subagent/brief split (D-007, L-002) itself.
- **Recurrence (2026-09-08, S-2026-09-08-01):** happened again, same shape. The
  brief driving that session's save claimed "nothing was committed this
  session... all of 6b through 6f is uncommitted." `git log` showed four
  commits (`717b96f`, `ae94bca`, `41e45bd`, `1d86810`, covering sub-phases
  6a–6d) already existed, dated the same day, authored directly by the user —
  only 6e and 6f were actually uncommitted. The brief was an honest snapshot of
  what the reporting agent last saw; the user had committed in between,
  outside that visibility. Confirms this is a structural risk that will recur
  every time, not a one-off — treat "is X committed?" as always requiring a
  fresh `git log`/`git status` check, never a transcription, no matter how
  specific or recent the brief's account sounds.

### L-005 · Swashbuckle does not auto-detect `[JsonPolymorphic]`/`[JsonDerivedType]`
- **Added:** 2026-09-07 (S-2026-09-07-01)
- **Tried:** Decorated `QuestionResponse` with `[JsonPolymorphic]` and its 10
  subtypes with `[JsonDerivedType]`, expecting Swashbuckle 10.2.3 to generate a
  `oneOf`/discriminator OpenAPI schema automatically, with controller actions
  returning bare `IActionResult`.
- **Result:** The generated `openapi.v1.json` had no usable schema for
  `QuestionResponse` in most responses — Swashbuckle could not infer anything
  from an `IActionResult` return type, and did not read the JSON attributes on
  its own.
- **Root cause:** `[FACT]` Swashbuckle 10.2.3 targets a newer major
  `Microsoft.OpenApi` version and, in this configuration, needs response types
  stated explicitly (`ActionResult<T>`) plus explicit subtype registration —
  attribute-based polymorphism detection is not automatic here.
- **Instead:** Type every action `ActionResult<TResponse>` (not `IActionResult`)
  wherever a response DTO exists, and call
  `options.SelectSubTypesUsing(...)` explicitly listing the union's subtypes,
  alongside `UseOneOfForPolymorphism()` and `SelectDiscriminatorNameUsing(...)`.
  See D-017.
- **Still true?** `[UNVERIFIED]` whether a future Swashbuckle version fixes this
  automatically — re-check this lesson before removing the explicit wiring on a
  package upgrade.

### L-006 · `openapi-generator-cli` needs a JVM this environment doesn't have
- **Added:** 2026-09-07 (S-2026-09-07-01)
- **Tried:** Running `openapi-generator-cli generate` (the Java-based tool) to
  produce the TypeScript client from `docs/openapi.v1.json`.
- **Result:** Could not run — no JVM installed in this environment.
- **Root cause:** `[FACT]` `openapi-generator-cli` requires a Java runtime;
  none is present on this machine.
- **Instead:** Used `NSwag.ConsoleCore` (`nswag openapi2tsclient`), a pure-.NET
  global dotnet tool — produced an equivalent discriminated-union TypeScript
  client with no extra runtime dependency. See D-018.
- **Still true?** Yes, unless a JVM is installed on this machine later.

### L-007 · A DB-only uniqueness/state constraint without a handler pre-check surfaces as an unhandled 500, not a clean 4xx
- **Added:** 2026-09-08 (S-2026-09-08-01)
- **Tried (implicitly, before this session's fixes):** Relying on a unique index
  or check constraint alone to enforce a business rule, with no equivalent
  check in the MediatR handler before the `SaveChangesAsync` that could violate
  it.
- **Result:** Hit this pattern three separate times in one session. (1) Phase
  6c: `CreateTeamCommandHandler` originally had no pre-check for `Team.Code`
  uniqueness — fixed proactively this time, but only because the pattern had
  already bitten once. (2) Phase 6d: neither `CreateTag` nor `CreateTopic`
  pre-checked name uniqueness before this session — a duplicate name hit the
  DB's unique-index violation raw and surfaced as an unhandled `500`, not a
  clean `409`; found by a test, not live. (3) Phase 6f: `Question.Approve`
  throws its own `InvalidOperationException` (by design, pinned by an existing
  Domain test) when the question isn't `Draft` — but `InvalidOperationException`
  isn't one of the types `GlobalExceptionHandler` maps, so approving an
  already-approved question would 500 instead of returning a clean 409.
- **Root cause:** `[FACT]` EF Core surfaces constraint violations as
  `DbUpdateException`, and domain methods sometimes throw framework exception
  types (`InvalidOperationException`) rather than a mapped domain exception —
  neither is caught by `GlobalExceptionHandler`'s explicit type map, so both
  bubble up as an unhandled `500`.
- **Instead:** Whenever adding a new unique index or check constraint, add the
  matching pre-check in the handler that could violate it (an explicit
  pre-query, not "let the DB reject it") — or, for a domain method that throws
  a generic exception type, check the precondition in the handler *before*
  calling the domain method and throw a type `GlobalExceptionHandler` already
  maps (e.g. `InvalidStateTransitionException`) instead.
- **Still true?** Yes — this is a standing implementation checklist item for
  every future constraint/domain-exception addition, not something specific to
  Phase 6.

### L-008 · `Question.Approve`'s `InvalidOperationException` needed a handler-level guard, not a `GlobalExceptionHandler` change
- **Added:** 2026-09-08 (S-2026-09-08-01)
- **Tried:** Calling `question.Approve(approvedBy)` directly from
  `ApproveQuestionCommandHandler` and letting whatever it threw propagate.
- **Result:** Approving an already-approved (non-Draft) question threw the
  domain method's own `InvalidOperationException`, which
  `GlobalExceptionHandler` does not map — would surface as a `500`, not the
  expected `409`.
- **Root cause:** `[FACT]` `Question.Approve` is pinned by an existing Domain
  test to throw `InvalidOperationException` specifically — changing the
  domain method's exception type would break that test and reach outside this
  phase's scope.
- **Instead:** Added the state check (`question.Status != QuestionStatus.Draft`)
  in the handler *before* calling `Approve`, throwing
  `InvalidStateTransitionException` (already mapped to 409) instead. Domain
  method and its test untouched.
- **Still true?** Yes, as a pattern: when a pinned domain method's exception
  type can't be changed, guard its precondition one layer up in the handler
  rather than widening `GlobalExceptionHandler`'s map to catch a generic
  framework exception type (which would then also swallow *unexpected*
  `InvalidOperationException`s elsewhere as if they were routine 409s).

### L-009 · `ProgramSetting("Teams","MaxTeams")` is not a second team-cap mechanism
- **Added:** 2026-09-08 (S-2026-09-08-01)
- **Tried:** Nothing wrong was actually done here — flagging this pre-emptively
  because the settings-bag key appears in Phase 6a's own test fixtures and
  could easily be mistaken for a real, competing design by a future agent
  skimming old test code.
- **Result (if mistaken):** A future session could wire team-cap enforcement
  against the wrong mechanism, or add a second enforcement path, causing the
  two to disagree.
- **Root cause:** `[FACT]` `Program.MaxTeams` is a typed column on `Program`,
  added in Phase 4 (unused until Phase 6c wired `SetMaxTeams` and
  `CreateTeamCommandHandler`'s enforcement). The `ProgramSetting("Teams",
  "MaxTeams")` key was only ever an incidental example value used in Phase 6a's
  own test fixtures — never a competing design, never read by any handler.
- **Instead:** Treat `Program.MaxTeams` as the sole real mechanism. If a future
  session finds the settings-bag key in old test code, that does not indicate
  a second intended mechanism exists or should be built.
- **Still true?** Yes, unless a future decision deliberately introduces a
  second, settings-bag-driven override — which would need its own `D-###`
  entry, not a silent revival of the old test-fixture value.
