# Backup and restore — runbook and drill (`P15-05`, NFR-A5)

**Drill date:** 2026-11-25, SQL Server 2022 · **Drill script:**
`Quizware/tools/drills/backup_restore_drill.py`

## What is backed up, and when

| Backup | When | Where | Kind |
|---|---|---|---|
| **Automatic pre-match** (NFR-A5) | Every match start, just before the start is saved | SQL Server's default backup folder, or `Backup:Directory` on the SQL Server host | Full, `COPY_ONLY`, `CHECKSUM`. Named `<db>-prematch-<match no>-<match id>-<UTC time>.bak` |
| **Venue routine** (recommended) | Before doors open, at each break, and after the final | The same folder, copied off the server | Full, `CHECKSUM` |

The pre-match backup is copy-only, so it never breaks a backup chain the
venue's DBA keeps. **If it fails, the match starts anyway.** The outcome is
recorded in the `MatchStarted` timeline entry as `preMatchBackup`
(`succeeded`, `file`, `error`) and logged as a warning. Check it in the
match's timeline or match report before a match that matters. Set
`Backup:BeforeMatchStart=false` to turn it off.

## Take a backup by hand

```sql
BACKUP DATABASE [Quizware] TO DISK = N'D:\QuizwareBackups\Quizware-before-final.bak'
  WITH INIT, CHECKSUM, COPY_ONLY;
RESTORE VERIFYONLY FROM DISK = N'D:\QuizwareBackups\Quizware-before-final.bak' WITH CHECKSUM;
```

## Restore

1. **Stop the API**, so nothing writes while you restore.
2. Restore the backup over the database (or under a new name; see step 5):

   ```sql
   RESTORE FILELISTONLY FROM DISK = N'D:\QuizwareBackups\Quizware-before-final.bak';  -- logical file names
   RESTORE DATABASE [Quizware] FROM DISK = N'D:\QuizwareBackups\Quizware-before-final.bak'
     WITH MOVE N'Quizware' TO N'D:\Data\Quizware.mdf', MOVE N'Quizware_log' TO N'D:\Data\Quizware_log.ldf',
          CHECKSUM, REPLACE;
   ```

3. **Start the API.** It checks the migrations on start-up, and is ready in
   about 5 s.
4. **Verify before going live again:**
   - `GET /health/ready` returns 200.
   - The live match's state (`GET /matches/{id}/live/state`) shows the
     expected question count and scores.
   - Its timeline and score ledger agree: each `AnswerRecorded` has a score
     event.
   - The match report (`/reports/matches/{id}`) reads as expected.
5. To look at an older point without replacing the live database, restore
   under a new name, for example `Quizware_PreMatch`, and point a second API
   instance or a SQL client at it.

Anything done after the backup was taken is not in the restore. Re-enter it
from the match timeline or the operator's notes, or replay the match from
the pre-match backup.

## Drill results (2026-11-25)

The drill ran on a scratch database through these steps: build a live
match; play four answers; take a full backup; play two more answers; drop
the database; restore; compare.

| Check | Result |
|---|---|
| Automatic pre-match backup taken when the match started | ✅ `QuizwareBackupDrill-prematch-1-…bak` |
| Backup verified (`RESTORE VERIFYONLY … WITH CHECKSUM`) | ✅ |
| Restore time | **0.5 s** (small drill database) |
| Row counts of all 59 tables after the restore equal those at backup time | ✅ 482 rows, no table differs |
| Live state after the restore equals the state at backup time | ✅ 4 questions served, scores 10/10/20, ledger total 40 |
| Match report after the restore is identical to the one at backup time | ✅ |
| The match carries on and finishes on the restored database | ✅ |
| The pre-match backup, restored as a second database, holds the match ready to play | ✅ match state `Draft`, 0 answers |

Repeat the drill:

```bash
cd Quizware && dotnet build Quizware.slnx -c Release
SA_PASSWORD='<sa password>' python3 tools/drills/backup_restore_drill.py --sql-container quizware-sql
```
