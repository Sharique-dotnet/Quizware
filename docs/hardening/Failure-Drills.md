# Failure drills — Phase 15 (`P15-04`)

**Date:** 2026-11-25 · **Target:** recovery under 30 s and zero data loss
(NFR-A1, NFR-A2), a buzzer failure never stops a match (NFR-A3), and a display
dropping never affects gameplay (NFR-A4).

## How they were run

| Drill | Where | How |
|---|---|---|
| Kill the API mid-match | Real process on SQL Server 2022 | `Quizware/tools/drills/failure_drills.py`: `kill -9`, then a restart |
| Kill the API while an answer is in flight | Real process on SQL Server 2022 | The same script: the answer is sent, the API is `kill -9`ed 20 ms later |
| Database stalls (server or network hangs) | SQL Server 2022 in Docker | The same script: `docker pause`, then `docker unpause` |
| Database goes away | SQL Server 2022 in Docker | The same script: `docker stop`, then `docker start` |
| Restart mid-match (regression guard, runs in CI) | In-process, SQLite file | `FailureDrillTests.RestartingTheApiMidMatch_…` |
| Disconnect a display | In-process, SignalR | `FailureDrillTests.ADisplayThatDrops_…` |
| Unplug the buzzer | In-process, simulated serial port | `BuzzerDegradationTests` (Phase 14) |

After every drill the script checks that the match is consistent. Each
recorded answer must have its score event, and the scoreboard must equal the
score ledger. After the last drill the match is played to the end.

```bash
cd Quizware && dotnet build Quizware.slnx -c Release
python3 tools/drills/failure_drills.py \
  --connection 'Server=localhost,1433;Database=QuizwareDrill;User Id=sa;Password=<sa password>;TrustServerCertificate=True' \
  --sql-container quizware-sql
```

## Results (SQL Server 2022, 2026-11-25)

| Drill | Data lost | Recovery | What the operator saw | Result |
|---|---|---|---|---|
| `kill -9` between answers | none: the state after restart matched the state before, field for field | API ready in **4.8 s** | — | ✅ |
| `kill -9` with an answer in flight | none: the answer was not committed, so it was simply recorded again; the scoreboard matched the ledger | **4.9 s** | The request dropped | ✅ |
| Database paused, then resumed | none | **0.2 s** after resuming | **503 `DATABASE_UNAVAILABLE`** after 35 s, with "Nothing was saved. Retry…" | ✅ |
| Database stopped, then started | none | **5.4 s** after starting | **503 `DATABASE_UNAVAILABLE`** after 14.5 s; `/health/ready` reported 503 | ✅ |
| Match played to the end after all four | — | — | — | ✅ |
| In-process restart (CI) | none: identical state; live events queued before the restart were delivered after it | well under 30 s | — | ✅ |
| Display disconnects and rejoins | gameplay unaffected; the rejoined display gets the full state and the live feed | — | — | ✅ |
| Buzzer unplugged mid-match | none: the match finishes on manual entry | — | 503 `BUZZER_UNAVAILABLE`; manual buttons | ✅ |

**Why nothing is lost:** each action is one `SaveChanges`, a single
transaction, so a crash leaves it either wholly saved or not saved at all. The
answer, its score event, the read models, the timeline entry and the outbox
event are always written together. Live events sit in the outbox until they
are delivered, so a restart delivers whatever was still waiting.

## Fixed during the drills

- **A database outage came back as a 500** ("An unexpected error occurred"),
  which told the operator nothing. Connection, network and timeout failures
  are now recognised (`DatabaseFailures`) and returned as **503
  `DATABASE_UNAVAILABLE`**, with `Retry-After: 5` and "Nothing was saved.
  Retry the action once the database is back". Ordinary database errors, such
  as a duplicate key, are not reclassified.

## Notes for the venue

- **A stalled database takes about 35 s to report**, which is SQL Server's
  default 30 s command timeout plus connecting. Adding `Command Timeout=10`
  to the venue connection string makes gameplay fail fast during a stall.
  It also caps long reports and exports at 10 s, so keep the default if those
  matter more.
- **Restarting the API takes about 5 s** on this hardware, start-up migration
  check included. Consoles and displays reconnect and re-sync with
  `RequestFullState`.
- **NFR-A5, an automatic database backup before each match starts, is not
  implemented.** The backup and restore runbook covers backups taken by the
  operator; see `Backup-Restore.md`.
