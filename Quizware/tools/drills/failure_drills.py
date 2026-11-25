#!/usr/bin/env python3
"""P15-04 failure drills against a real API process and SQL Server.

Builds a live match through the public API, then:

  1. kill -9 the API between answers, restart it, and check nothing was lost;
  2. kill -9 the API while an answer is in flight: the answer is either fully
     recorded or not at all;
  3. pause the database container mid-match (the network or server stalls),
     then resume it;
  4. stop and restart the database container mid-match.

After each drill it measures recovery time (target < 30 s, NFR-A2) and checks
the match is consistent: every recorded answer has its score event and its
timeline entry, and the scoreboard agrees with the score ledger (NFR-A1).

Standard library only. Usage (from the Quizware folder, after a Release build):

  python3 tools/drills/failure_drills.py \\
      --connection 'Server=localhost,1433;Database=QuizwareDrill;User Id=sa;Password=...;TrustServerCertificate=True' \\
      --sql-container quizware-sql

The API runs in Development (the seeded admin account is used to sign in).
"""

import argparse
import json
import os
import signal
import subprocess
import sys
import tempfile
import threading
import time
import urllib.error
import urllib.request
import uuid

QUESTIONS = 12


def parse(raw):
    """JSON when it is JSON (health checks answer in plain text)."""
    if not raw:
        return None
    try:
        return json.loads(raw)
    except ValueError:
        return raw.decode(errors="replace")


class Api:
    def __init__(self, base_url):
        self.base_url = base_url.rstrip("/")
        self.token = None

    def call(self, method, path, body=None, timeout=60, token=None):
        data = json.dumps(body).encode() if body is not None else None
        request = urllib.request.Request(self.base_url + path, data=data, method=method)
        request.add_header("Content-Type", "application/json")
        if token or self.token:
            request.add_header("Authorization", "Bearer " + (token or self.token))
        started = time.monotonic()
        try:
            with urllib.request.urlopen(request, timeout=timeout) as response:
                return response.status, parse(response.read()), time.monotonic() - started
        except urllib.error.HTTPError as error:
            return error.code, parse(error.read()), time.monotonic() - started
        except (urllib.error.URLError, ConnectionError, TimeoutError, OSError) as error:
            return None, str(error), time.monotonic() - started

    def ok(self, method, path, body=None):
        status, payload, _ = self.call(method, path, body)
        if status is None or status >= 300:
            raise RuntimeError(f"{method} {path} -> {status}: {payload}")
        return payload


class ApiProcess:
    """Runs the built API (Release) as a child process that can be killed hard."""

    def __init__(self, args):
        self.args = args
        self.process = None

    def start(self):
        env = dict(os.environ)
        env.update({
            "ASPNETCORE_ENVIRONMENT": "Development",
            "ASPNETCORE_URLS": self.args.base_url,
            "ConnectionStrings__Default": self.args.connection,
            "Jwt__Issuer": "quizware-drill",
            "Jwt__Audience": "quizware-drill",
            "Jwt__SigningKey": "drill-only-signing-key-that-is-at-least-32-bytes",
            "Security__AuthRequestsPerMinute": "1000",
        })
        log = open(self.args.log, "ab")
        self.process = subprocess.Popen(
            [self.args.dotnet, self.args.api_dll], cwd=os.path.dirname(self.args.api_dll), env=env, stdout=log, stderr=log)
        return time.monotonic()

    def kill(self):
        if self.process and self.process.poll() is None:
            os.kill(self.process.pid, signal.SIGKILL)
            self.process.wait()


def wait_until(predicate, limit=120):
    started = time.monotonic()
    while time.monotonic() - started < limit:
        if predicate():
            return time.monotonic() - started
        time.sleep(0.25)
    raise RuntimeError("timed out waiting for recovery")


def ready(api):
    status, _, _ = api.call("GET", "/health/ready", timeout=5)
    return status == 200


def build_match(api):
    """Program, approved questions, a stage, three teams, a started match."""
    api.token = api.ok("POST", "/api/v1/auth/login", {"email": "admin@quizapp.local", "password": "KeepMeUpdated@123"})["accessToken"]
    program = api.ok("POST", "/api/v1/programs", {"code": "DRILL-" + uuid.uuid4().hex[:8], "name": "Failure drill", "description": None})
    api.token = api.ok("POST", "/api/v1/auth/select-program", {"programId": program["id"]})["accessToken"]
    p = f"/api/v1/programs/{program['id']}"
    api.ok("POST", f"{p}/rules/scoring/reset-defaults")
    for i in range(QUESTIONS):
        question = api.ok("POST", f"{p}/questions/mcq", {
            "formatCode": "Mcq", "questionText": f"Drill question {i} {uuid.uuid4().hex[:6]}", "difficultyLevelId": 2,
            "language": "en", "options": [
                {"text": "Right", "isCorrect": True, "displayOrder": 1, "mediaAssetId": None},
                {"text": "Wrong", "isCorrect": False, "displayOrder": 2, "mediaAssetId": None}],
            "allowMultipleCorrect": False, "shuffleOptions": False, "negativeMarkingEnabled": False})
        api.ok("POST", f"{p}/questions/{question['id']}/approve", {"notes": "Failure drill"})
    stage = api.ok("POST", f"{p}/stages", {"name": "Drill stage", "orderIndex": 0})
    api.ok("POST", f"{p}/stages/{stage['id']}/segments", {"formatCode": "Mcq", "questionCount": QUESTIONS})
    teams = [api.ok("POST", f"{p}/teams", {"code": f"D{i}{uuid.uuid4().hex[:5]}", "schoolName": f"Drill School {i}",
                                         "displayName": f"Drill Team {i}", "memberNames": ["A"]})["id"] for i in range(3)]
    match = api.ok("POST", f"{p}/matches", {"stageId": stage["id"], "name": "Drill match", "matchNumber": 1})
    for seat, team in enumerate(teams, start=1):
        api.ok("POST", f"{p}/matches/{match['id']}/participants", {"teamId": team, "seatNumber": seat})
    live = f"/api/v1/matches/{match['id']}/live"
    api.ok("POST", f"{live}/start")
    segments = api.ok("GET", f"{p}/matches/{match['id']}/segments")["segments"]
    api.ok("POST", f"{live}/segments/{segments[0]['id']}/open")
    return {"live": live, "segment": segments[0]["id"], "match": match["id"]}


def serve(api, m):
    return api.ok("POST", f"{m['live']}/questions/serve", {"segmentId": m["segment"]})


def answer_body(api, m, question):
    state = api.ok("GET", f"{m['live']}/state")
    return {"matchQuestionId": question["matchQuestionId"], "matchParticipantId": state["activeParticipant"]["participantId"],
            "outcome": "Correct", "selectedOptionId": None, "selectedOptionIds": None, "freeTextAnswer": None,
            "passNumber": 0, "answerSource": "Operator", "buzzPressId": None, "responseTimeMs": None}


def play_one(api, m):
    question = serve(api, m)
    api.ok("POST", f"{m['live']}/answers", answer_body(api, m, question))


def snapshot(api, m):
    state = api.ok("GET", f"{m['live']}/state")
    timeline = api.ok("GET", f"{m['live']}/timeline")["events"]
    ledger = api.ok("GET", f"/api/v1/matches/{m['match']}/scores/events")["events"]
    return {
        "served": state["progress"]["questionsServed"],
        "scores": sorted(p["score"] for p in state["participants"]),
        "answers_in_timeline": sum(1 for e in timeline if e["eventType"] == "AnswerRecorded"),
        "score_events": len(ledger),
        "ledger_total": sum(e["points"] for e in ledger),
    }


def consistent(snap):
    """Every answer has its score event, and the scoreboard equals the ledger."""
    return snap["answers_in_timeline"] == snap["score_events"] and sum(snap["scores"]) == snap["ledger_total"]


def report(name, ok, **facts):
    print(json.dumps({"drill": name, "passed": ok, **facts}))
    return ok


def drill_kill_between_answers(api, proc, m):
    play_one(api, m)
    before = snapshot(api, m)
    proc.kill()
    restarted = proc.start()
    recovered = wait_until(lambda: ready(api))
    after = snapshot(api, m)
    play_one(api, m)
    lost = before != after
    return report("kill -9 between answers", not lost and consistent(after) and recovered < 30,
                  recovery_seconds=round(time.monotonic() - restarted, 1), before=before, after=after)


def drill_kill_mid_answer(api, proc, m):
    question = serve(api, m)
    body = answer_body(api, m, question)
    before = snapshot(api, m)
    result = {}
    sender = threading.Thread(target=lambda: result.update(r=api.call("POST", f"{m['live']}/answers", body, timeout=10)))
    sender.start()
    time.sleep(0.02)
    proc.kill()
    sender.join()
    restarted = proc.start()
    wait_until(lambda: ready(api))
    after = snapshot(api, m)
    recorded = after["answers_in_timeline"] - before["answers_in_timeline"]
    if recorded == 0:
        # Not committed: the operator simply records it again.
        api.ok("POST", f"{m['live']}/answers", answer_body(api, m, question))
    final = snapshot(api, m)
    return report("kill -9 while an answer is in flight", recorded in (0, 1) and consistent(after) and consistent(final),
                  answer_committed=bool(recorded), in_flight_response=result.get("r", (None,))[0],
                  recovery_seconds=round(time.monotonic() - restarted, 1), after=final)


def drill_database(api, m, args, action):
    play_one(api, m)
    before = snapshot(api, m)
    question = serve(api, m)
    body = answer_body(api, m, question)
    subprocess.run(["docker", action[0], args.sql_container], check=True, capture_output=True)
    outage_status, payload, outage_seconds = api.call("POST", f"{m['live']}/answers", body, timeout=90)
    health_status, _, _ = api.call("GET", "/health/ready", timeout=30)
    resumed = time.monotonic()
    subprocess.run(["docker", action[1], args.sql_container], check=True, capture_output=True)
    recovered = wait_until(lambda: api.call("GET", f"{m['live']}/state", timeout=10)[0] == 200)
    after = snapshot(api, m)
    if after["answers_in_timeline"] == before["answers_in_timeline"]:
        api.ok("POST", f"{m['live']}/answers", body)
    final = snapshot(api, m)
    error_code = payload.get("errorCode") if isinstance(payload, dict) else None
    return report(f"database {action[0]} / {action[1]}",
                  outage_status is not None and outage_status >= 500 and consistent(after) and consistent(final) and recovered < 30,
                  answer_during_outage=outage_status, error_code=error_code, answered_after_seconds=round(outage_seconds, 1),
                  health_during_outage=health_status, recovery_seconds=round(time.monotonic() - resumed, 1), after=final)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    here = os.path.dirname(os.path.abspath(__file__))
    parser.add_argument("--connection", required=True, help="SQL Server connection string for a scratch database")
    parser.add_argument("--sql-container", default="quizware-sql")
    parser.add_argument("--base-url", default="http://localhost:5390")
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--api-dll", default=os.path.normpath(os.path.join(
        here, "..", "..", "src", "Quizware.Api", "bin", "Release", "net10.0", "Quizware.Api.dll")))
    parser.add_argument("--log", default=os.path.join(tempfile.gettempdir(), "quizware-failure-drills-api.log"))
    args = parser.parse_args()

    api = Api(args.base_url)
    proc = ApiProcess(args)
    proc.start()
    try:
        wait_until(lambda: ready(api), limit=180)
        m = build_match(api)
        results = [
            drill_kill_between_answers(api, proc, m),
            drill_kill_mid_answer(api, proc, m),
            drill_database(api, m, args, ("pause", "unpause")),
            drill_database(api, m, args, ("stop", "start")),
        ]
        status = api.ok("POST", f"{m['live']}/end")
        report("match completes after every drill", status is not None and consistent(snapshot(api, m)))
    finally:
        proc.kill()
    sys.exit(0 if all(results) else 1)


if __name__ == "__main__":
    main()
