# ADR-003: Question storage as Table-Per-Type

**Status:** Accepted
**Date:** 2026-09-07

## Context

Quizware supports ten distinct question formats (MCQ, Audio-Visual, Sequence,
Buzzer, Passing, Card, Choice, Rapid Fire, Visual Rapid Fire, Tie-Breaker).
Each format has genuinely different required fields — for example
`AudioVisualQuestion` requires a non-null `MediaAssetId` and `AnswerText`,
while `SequenceQuestion` requires a `SequenceLength` and has no options at
all. All ten formats also share the same identity, classification and
lifecycle columns (program, difficulty, topic, language, approval status,
usage count).

## Decision

Use **Table-Per-Type (TPT)**: one shared `Question` base table holding the
15 or so columns every format has in common, plus one table per format
holding only that format's columns, joined 1:1 on a shared primary key
(`QuestionId`). In the domain model this is `Question` (abstract) with ten
sealed subclasses (`McqQuestion`, `AudioVisualQuestion`, etc.) — see
`Quizware.Domain.QuestionBank`.

Everything else in the system (`MatchQuestion`, `QuestionUsageHistory`,
`QuestionSelectionRule`, `AnswerRecord`) references `Question.Id` only, and
never needs to know which format table is involved — this is what keeps the
match engine and question selector single, generic engines rather than one
per format.

## Alternatives rejected

**(a) Ten independent tables**, each repeating the ~15 shared columns.
Rejected because it reproduces the 75–80% duplication already found in the
legacy system's per-format tables, and any change to a shared concern (e.g.
adding an approval workflow) would need to be made in ten places.

**(b) One wide table** with every format's columns as nullable. Rejected
because it cannot express real constraints — `AudioVisualQuestion.MediaAssetId`
being `NOT NULL` is a database guarantee that a single shared table cannot
enforce, since the same column would need to be nullable for every other
format.

## Consequences

- Ten format-specific EF Core entity configurations are needed in
  `Quizware.Infrastructure` (Phase 4), one route per format at the API layer
  (`POST /questions/mcq`, `POST /questions/audio-visual`, …), and one request
  DTO per format (Phase 5) — this is the direct cost of the format-specific
  guarantees TPT buys.
- Adding an eleventh format later is one new table, one new subclass, and one
  new handler — not a schema-wide change.
