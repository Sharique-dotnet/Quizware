#!/usr/bin/env python3
"""P15-05 backup and restore drill against SQL Server — restore verified, not assumed.

  1. Build a live match (starting it takes the automatic pre-match backup, NFR-A5).
  2. Play part of it, take a full backup, and record the state, the match report
     and the row count of every table.
  3. Play on, then lose the database (DROP DATABASE).
  4. Restore the backup and compare: same state, same report, same row counts.
     Start the API on it and finish the match.
  5. Restore the automatic pre-match backup as a second database and check it
     holds the match as it was ready to play: not started, no answers.

Standard library only; runs sqlcmd inside the SQL Server container. Usage (from
the Quizware folder, after a Release build):

  SA_PASSWORD=... python3 tools/drills/backup_restore_drill.py --sql-container quizware-sql
"""

import argparse
import json
import os
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import failure_drills as drills  # noqa: E402

DATABASE = "QuizwareBackupDrill"
PREMATCH = DATABASE + "_PreMatch"
BACKUP_FILE = "/var/opt/mssql/data/QuizwareBackupDrill-manual.bak"


class Sql:
    def __init__(self, container, password):
        self.container = container
        self.password = password

    def run(self, query, database="master"):
        result = subprocess.run(
            ["docker", "exec", self.container, "/opt/mssql-tools18/bin/sqlcmd", "-S", "localhost", "-U", "sa", "-P", self.password,
             "-C", "-b", "-h", "-1", "-W", "-s", "|", "-d", database, "-Q", "SET NOCOUNT ON; " + query],
            capture_output=True, text=True)
        if result.returncode != 0:
            raise RuntimeError(f"sqlcmd failed: {result.stdout} {result.stderr}")
        return [line for line in result.stdout.splitlines() if line.strip()]

    def drop(self, database):
        self.run(f"IF DB_ID(N'{database}') IS NOT NULL BEGIN ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; "
                 f"DROP DATABASE [{database}]; END")

    def row_counts(self, database):
        rows = self.run("SELECT t.name, SUM(p.rows) FROM sys.tables t JOIN sys.partitions p ON p.object_id = t.object_id "
                        "AND p.index_id IN (0, 1) GROUP BY t.name ORDER BY t.name", database)
        return {name: int(count) for name, count in (row.split("|") for row in rows)}

    def restore(self, database, file):
        files = [row.split("|")[:2] for row in self.run(f"RESTORE FILELISTONLY FROM DISK = N'{file}'")]
        moves = ", ".join(
            f"MOVE N'{logical}' TO N'/var/opt/mssql/data/{database}{'_log.ldf' if physical.lower().endswith('.ldf') else '.mdf'}'"
            for logical, physical in files)
        started = time.monotonic()
        self.run(f"RESTORE DATABASE [{database}] FROM DISK = N'{file}' WITH {moves}, CHECKSUM, REPLACE")
        return time.monotonic() - started


def report(api, m):
    """The match report, serialised so two copies compare exactly."""
    body = api.ok("GET", f"/api/v1/programs/{m['program']}/reports/matches/{m['match']}")
    return json.dumps(body, sort_keys=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    here = os.path.dirname(os.path.abspath(__file__))
    parser.add_argument("--sql-container", default="quizware-sql")
    parser.add_argument("--sa-password", default=os.environ.get("SA_PASSWORD"))
    parser.add_argument("--base-url", default="http://localhost:5391")
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--api-dll", default=os.path.normpath(os.path.join(
        here, "..", "..", "src", "Quizware.Api", "bin", "Release", "net10.0", "Quizware.Api.dll")))
    parser.add_argument("--log", default=os.path.join(drills.tempfile.gettempdir(), "quizware-backup-drill-api.log"))
    args = parser.parse_args()
    if not args.sa_password:
        parser.error("set --sa-password or SA_PASSWORD")

    sql = Sql(args.sql_container, args.sa_password)
    sql.drop(DATABASE)
    sql.drop(PREMATCH)
    args.connection = f"Server=localhost,1433;Database={DATABASE};User Id=sa;Password={args.sa_password};TrustServerCertificate=True"
    api = drills.Api(args.base_url)
    proc = drills.ApiProcess(args)
    results = []
    try:
        proc.start()
        drills.wait_until(lambda: drills.ready(api), limit=180)
        m = drills.build_match(api)
        started = next(e for e in api.ok("GET", f"{m['live']}/timeline")["events"] if e["eventType"] == "MatchStarted")
        prematch = json.loads(started["detail"])["preMatchBackup"]
        results.append(drills.report("automatic pre-match backup taken (NFR-A5)", bool(prematch and prematch["succeeded"]),
                                     backup=prematch))

        for _ in range(4):
            drills.play_one(api, m)
        state_at_backup = drills.snapshot(api, m)
        report_at_backup = report(api, m)
        proc.kill()  # quiesce: nothing writes between the counts and the backup
        counts_at_backup = sql.row_counts(DATABASE)
        sql.run(f"BACKUP DATABASE [{DATABASE}] TO DISK = N'{BACKUP_FILE}' WITH INIT, CHECKSUM, COPY_ONLY")
        sql.run(f"RESTORE VERIFYONLY FROM DISK = N'{BACKUP_FILE}' WITH CHECKSUM")

        proc.start()
        drills.wait_until(lambda: drills.ready(api))
        for _ in range(2):
            drills.play_one(api, m)
        proc.kill()
        sql.drop(DATABASE)  # the disaster

        restore_seconds = sql.restore(DATABASE, BACKUP_FILE)
        counts_after = sql.row_counts(DATABASE)
        proc.start()
        drills.wait_until(lambda: drills.ready(api))
        state_after = drills.snapshot(api, m)
        report_after = report(api, m)
        results.append(drills.report(
            "restore from the manual backup", counts_after == counts_at_backup and state_after == state_at_backup and report_after == report_at_backup,
            restore_seconds=round(restore_seconds, 1), tables=len(counts_after), rows=sum(counts_after.values()),
            state=state_after, differing_tables=[t for t in counts_at_backup if counts_at_backup[t] != counts_after.get(t)]))

        drills.play_one(api, m)
        api.ok("POST", f"{m['live']}/end")
        results.append(drills.report("match finishes on the restored database", drills.consistent(drills.snapshot(api, m))))

        sql.restore(PREMATCH, prematch["file"] if prematch["file"].startswith("/") else "/var/opt/mssql/data/" + prematch["file"])
        match_row = sql.run(f"SELECT State, (SELECT COUNT(*) FROM AnswerRecords WHERE MatchId = '{m['match']}') "
                            f"FROM Matches WHERE Id = '{m['match']}'", PREMATCH)[0].split("|")
        results.append(drills.report("pre-match backup restores the match as it was ready to play",
                                     int(match_row[0]) in (1, 2) and int(match_row[1]) == 0,
                                     match_state=int(match_row[0]), answers=int(match_row[1])))
    finally:
        proc.kill()
        sql.drop(PREMATCH)
    sys.exit(0 if all(results) else 1)


if __name__ == "__main__":
    main()
