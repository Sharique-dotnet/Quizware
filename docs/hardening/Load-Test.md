# Load test and performance tuning — Phase 15 (`P15-01`, `P15-06`)

**Date:** 2026-11-25 · **Test:** `LoadTests` in
`Quizware/tests/Quizware.Api.IntegrationTests` · **Query guard:** `QueryBudgetTests`

## Scenario (`P15-01`)

- **5 operators**, each with an Operator token, play **one full match each, at
  the same time**: 3 teams, 15 MCQ questions, then end. Each question is a
  serve, a live-state read and an answer.
- **100 display clients** are connected to `/hubs/display` and watching match 1
  (NFR-SC2). After each answer, the operator waits until all 100 have received
  `AnswerRecorded`.
- Every gameplay request, every live-state read and every push to the displays
  is timed. Each match's first question warms the pipeline up and is not
  counted.

Run it against real SQL Server, which is when the NFR targets are enforced:

```bash
cd Quizware
QUIZWARE_TEST_SQLSERVER='Server=localhost,1433;User Id=sa;Password=<sa password>;TrustServerCertificate=True' \
  dotnet test tests/Quizware.Api.IntegrationTests --filter "FullyQualifiedName~LoadTests" --logger "console;verbosity=detailed"
```

Each run creates a throwaway database and drops it afterwards. Without the
variable, the test runs on the default SQLite file database. That is the CI
setting, and there it is only a smoke test with targets relaxed fivefold:
SQLite allows one writer at a time, so with five operators and the dispatcher
writing at once its tail latencies measure the lock, not the code.

## Results — SQL Server 2022

One container held the API, SQL Server, all 100 SignalR clients and the test
runner, so these numbers are pessimistic for a dedicated venue server. The
range covers six runs after tuning.

| NFR | Target | p95 measured | Status |
|---|---|---|---|
| NFR-P1 gameplay requests (serve, answer, end) | < 200 ms | 141–183 ms (p50 72–80 ms) | ✅ |
| NFR-P2 `GET /live/state` | < 150 ms | 56–75 ms (p50 37–42 ms) | ✅ |
| NFR-P3 push to all 100 displays | < 500 ms | 307–365 ms | ✅ |
| NFR-SC2 100 display clients on one match | 100 | 100 | ✅ |
| NFR-P5 standings from pre-aggregated data | — | `TeamStageScore` read model (Phase 10) | ✅ |
| **NFR-P4 recording an answer in ≤ 3 database round trips** | ≤ 3 | **17 reads + 1 write batch** | ❌ **not met** — see below |
| **NFR-SC3 stateless behind a load balancer** | — | single instance assumed | ⚠️ **not met** — see below |

## Tuning done (`P15-06`)

Before tuning, the first SQL Server runs had live-state p95 between 64 and
164 ms (one run over target), gameplay p95 between 169 and 180 ms, and push
p95 between 355 and 423 ms.

1. **The outbox dispatcher built two state snapshots for every event.** It now
   builds each match's snapshots once per dispatch pass. Every event in a pass
   was committed before the pass began, so the first snapshot already includes
   them all: screens receive exactly what they did before, at a fraction of
   the database reads.
2. **All matches' events were pushed one after another**, so one busy match
   delayed every other match's screens. Matches are now pushed side by side
   (`Live:DispatchConcurrency`, default 4), each in its own scope. Within a
   match, delivery stays strictly in order and still stops at a failed or
   retry-waiting event, as `LiveDispatchTests` checks.
3. **Live state made 12 round trips.** Participants, team names and scores
   now come from a single joined query, bringing it to 10. Live state is read
   by every console and built for every push, so this saving is multiplied
   across both.

After tuning, live-state p95 fell to 56–75 ms. `QueryBudgetTests` now fails if
any of these requests gains a round trip: live state 10, serve 11, answer 17.

## Not met, and why

- **NFR-P4 (≤ 3 round trips per answer).** Recording an answer reads the
  match, question, segment, participants, prior answers, the bank question,
  scoring rules (twice), both score read models and the event sequence, saves
  in one batch, then reads again to build the response. Getting to 3 means
  rebuilding the answer pipeline around one preloaded aggregate and returning
  the response from memory. That is a redesign, too large to make safely in a
  hardening pass. The latency target the NFR exists to protect (NFR-P1) is met
  with room to spare, so this is recorded as an accepted gap with a
  follow-up, not a fix.
- **NFR-SC3 (stateless, behind a load balancer).** The design assumes one API
  instance on the venue server (ADR-008). Several pieces of state live in
  process: the outbox dispatcher (exactly one should run), SignalR groups (no
  backplane), the buzzer agent's last heartbeat, and the sign-in rate limiter.
  Running more than one instance would need a SignalR backplane (Redis), a
  lock or leader election for the dispatcher, and a shared store for the
  heartbeat and rate limiter.
