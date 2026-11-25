# Dress rehearsal checklist — Phase 15 (`P15-07`)

> **Status: not yet run.** P15-07 is a complete 18-team tournament on the real
> venue hardware, run by the real operators. It cannot be done from a
> development environment. Everything else in Phase 15 has been verified in
> software, and this checklist is what the rehearsal must prove on the day.
> **Mandatory. Do not skip.**

## Before the day

- [ ] **Venue server:** Release build of `Quizware.Api` on the venue machine,
  with SQL Server installed locally (ADR-008).
- [ ] **Configuration from secrets, not `appsettings.json`:**
  `Jwt:SigningKey` (≥ 32 bytes), `Jwt:Issuer`, `Jwt:Audience`,
  `ConnectionStrings:Default`, and `Buzzer:Agent:ApiKey` (≥ 16 characters).
  The API refuses to start on the placeholders, which is a good first check.
- [ ] **`AllowedHosts`** set to the server's host name, and HTTPS with the
  venue's internal certificate (see `Security-Review.md`).
- [ ] **Connection string:** consider `Command Timeout=10`, so a database
  stall fails fast at the console (see `Failure-Drills.md`).
- [ ] **Backups:** `Backup:BeforeMatchStart=true`. Check that the SQL Server
  service account can write to the backup folder.
- [ ] **Buzzer: confirm the serial command bytes before anything else.**
  `Buzzer:Serial:Commands` (read `0x47`, arm `0x53`, reset `0x52`) are
  *assumed*. Check them against QuickBuzz's `SerialService` and correct the
  configuration.
- [ ] **BuzzerAgent** installed on the operator PC (console or Windows
  service), its `Agent:ApiKey` matching the server's, with `Agent:ApiBaseUrl`
  pointing at the venue server and the COM port set.
- [ ] **Tournament configured as data:** 18 teams, League → Semi-Final →
  Final, segment templates, scoring, selection, qualification and tie-break
  rules. Run `POST /programs/{id}/validate` and get no blockers.
- [ ] **Question bank:** coverage report (`/questions/coverage`) shows enough
  approved questions for every format in use, for every match.

## On the day — run the whole tournament

- [ ] **Two or more operator consoles and every projector screen** connected.
  Screens use display tokens.
- [ ] **Buzzer rehearsal screen** (`POST /buzzer/test`): every device answers,
  and every button's timer moves.
- [ ] **Map devices to teams** for each match (`PUT /buzzer/matches/{id}/mappings`).
- [ ] **Play every League match.** For each one, check that:
  - the pre-match backup succeeded: look at `preMatchBackup` in the match timeline;
  - the screens follow every question, answer and reveal, and **no screen
    ever shows an answer before the reveal**;
  - buzzer presses rank correctly, and the operator can override them.
- [ ] **Qualification:** preview → detect ties → **play a real tie-break
  match** → commit. The Semi-Final is seeded correctly.
- [ ] **Semi-Final and Final**, including at least one segment reordered live
  and at least one disqualification with a reason.
- [ ] **Reports:** match, stage and team reports, and the audit log, exported
  to Excel, CSV and PDF.

## Failure drills on the real hardware

Repeat each of these during a live match:

- [ ] **Unplug the buzzer cable mid-question.** The console shows the manual
  buttons, the answer is recorded by hand, and the match carries on.
- [ ] **Close a projector's browser and reopen it.** It rejoins and shows the
  current question and scores.
- [ ] **Restart the API service mid-match.** Back within 30 s, with nothing
  lost; the consoles re-sync.
- [ ] **Stop and start the SQL Server service** between questions. The
  consoles show `DATABASE_UNAVAILABLE` (503), then the match continues.
- [ ] **Restore drill:** restore the morning's backup to a second database
  and spot-check it (see `Backup-Restore.md`).

## Sign-off

| Area | Signed off by | Notes |
|---|---|---|
| Tournament played end to end | | |
| Displays never revealed an answer early | | |
| Buzzer (command bytes confirmed, rankings right) | | |
| Failure drills on real hardware | | |
| Backup restored and checked | | |
