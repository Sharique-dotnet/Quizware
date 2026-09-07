# LESSONS

Approaches that failed, bugs, and environment traps. **Read this before proposing
an approach** — it is the list of things that already cost someone time.
Format: `_meta/SPEC.md` §6.5.

**Last updated:** 2026-09-07 (S-2026-09-07-01)

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
