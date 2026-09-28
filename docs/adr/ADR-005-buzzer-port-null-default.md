# ADR-005: Buzzer behind a port, with a Null adapter as the default

**Status:** Accepted
**Date:** 2026-09-07

## Context

QuickBuzz (the legacy buzzer app) is a separate, optional piece of hardware
and software. Not every program will have buzzer hardware, and the core
gameplay — MCQ, Sequence, Passing, and every other format — must work
identically whether or not a buzzer is present. Coupling the match engine
directly to buzzer hardware would make the whole system depend on hardware
that most events may not have.

## Decision

Define an **`IBuzzerProvider` port** in `Quizware.Domain`/`Quizware.Application`
with three adapters in `Quizware.Modules.Buzzer`:

- `NullBuzzerAdapter` — the **default**. `IsAvailable = false`; every
  existing test must still pass with it registered.
- `SerialBuzzerAdapter` — talks to the RS485 hardware directly (ported from
  QuickBuzz's `SerialService`/`DeviceParser`).
- `HttpAgentBuzzerAdapter` — receives pushed results from
  `Quizware.BuzzerAgent`, a small tray app on the operator PC that owns the
  COM port and pushes outbound over LAN HTTPS (no port forwarding needed).

**The solution must build and every test must pass with
`Quizware.Modules.Buzzer` deleted entirely.** The buzzer identifies who
answered first; it never writes scores directly — an operator or the match
engine always translates a buzz result into a normal `AnswerRecord` and
`ScoreEvent`.

## Alternatives rejected

**A direct dependency** from the match engine (or `Quizware.Api`) to buzzer
hardware or its client library. Rejected because it breaks the "must work
without it" requirement outright — any program without buzzer hardware,
or any developer machine without the hardware attached, would be unable to
build or run the core system.

## Consequences

- `P14` (QuickBuzz integration) is deliberately built **last** in the
  roadmap, specifically to prove the rest of the system was never dependent
  on it.
- Buzzer failure must degrade gracefully: `503 BUZZER_UNAVAILABLE` and a
  manual-entry fallback, never a blocked or crashed match.
- Any new gameplay feature must be designed to work with `IsAvailable =
  false` from day one, not have buzzer support bolted on as an afterthought.
