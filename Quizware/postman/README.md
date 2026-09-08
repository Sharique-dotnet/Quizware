# Quizware Postman collection

`Quizware.postman_collection.json` covers every implemented endpoint through
**Phase 7 (Tournament configuration)**. Endpoints that are still `501`
stubs — Matches, Live match engine, Scores, Standings, Qualification
commit, Buzzer, Display, Reports (Phase 8 onward) — are intentionally not
included; there's nothing real to test yet.

All variables (`baseUrl`, tokens, ids) are **collection variables**, saved by
each request's test script for the next request to use — no separate
Postman environment file is needed.

## Setup

1. Have the API running locally: from `Quizware/src/Quizware.Api`,
   ```bash
   dotnet run --no-launch-profile
   ```
   (defaults to `http://localhost:5299`; if yours differs, edit the
   `baseUrl` collection variable after import.)
2. In Postman: **Import** → select `Quizware.postman_collection.json`.
3. The seeded admin credentials (`adminEmail` / `adminPassword` variables)
   default to `admin@quizapp.local` / `ChangeMe!123` — the values
   `AdminUserSeeder` creates on a fresh dev database. Update them if your
   database already has a different admin password.

## Run order

Run folders top to bottom; within a folder, run requests top to bottom.
Each request that creates something saves its id for later requests — do
not skip requests inside a folder, or a later request will use an empty
variable.

1. **00 Health** — confirms the API is up before anything else.
2. **01 Auth → Login** — must run first; every other request needs
   `{{accessToken}}`. `Me` / `Refresh` / `Change Password` can run any time
   after.
3. **02 Programs** — `Create Program` sets `{{programId}}`. **`Select
   Program` must run before any folder below this one** — it mints
   `{{scopedAccessToken}}`, which every program-scoped request (04 onward)
   uses. The scoped token expires in 15 minutes; re-run *Select Program* if
   later requests start returning 401.
4. **03 Admin** — user invite/roles/reset/deactivate. *Assign Roles* needs
   `{{programId}}` from the folder above, so this folder must run after
   **02 Programs**, not before.
5. **04 Teams** — the two Excel-import requests need a `.xlsx` file
   attached manually (Postman can't ship a binary fixture inside a JSON
   collection export). Column layout is documented in each request's
   description.
6. **05 Topics & Tags** — `Create Topic` / `Create Tag` set `{{topicId}}` /
   `{{tagId}}`, referenced by the Questions and Rules folders below.
7. **06 Media** — attach an image/audio/video file manually before sending;
   sets `{{mediaAssetId}}` (only needed if you extend the collection with an
   Audio-Visual question, not used by the requests included here).
8. **07 Questions** — `Create MCQ Question` sets `{{mcqQuestionId}}`;
   `Update Question` creates a new version and sets `{{mcqQuestionIdV2}}`
   (the one that gets approved/retired). The MCQ import-validate request
   needs a `.xlsx` file attached manually.
9. **08 Stages** — creates a League and a Final stage, adds segments to
   League only, and deliberately validates both *before* and *after* adding
   segments so you can see `STAGE_HAS_NO_SEGMENTS` fire and clear. Reorders
   segments with one locked segment to confirm it keeps its index, then
   reorders stages, then runs the program-wide readiness check (which
   should still report a blocker for the empty Final stage).
10. **09 Rules** — `Reset Scoring Defaults` and `Reset Tie-Break Defaults`
    seed realistic starting data before the matching `Upsert` requests are
    exercised. `Preview Selection` reports `canSatisfy: false` until you've
    approved at least one MCQ question in **07 Questions**.

## Notes

- Every request's test script asserts the expected status code, so you can
  run the whole collection via **Collection Runner** (or `newman run
  postman/Quizware.postman_collection.json`) and get a clean pass/fail
  summary instead of reading responses by hand.
- `{{$randomInt}}` in a few request bodies (program code, team code, invited
  user email) avoids unique-constraint collisions on repeated runs — you can
  re-run the whole collection against the same database without cleaning up
  first, except for the two Excel-import requests (they need a file
  re-attached each run, since Postman doesn't persist the attachment in the
  exported JSON).
- `Delete Stage` in folder 08 only works on a stage with zero matches — that
  is why the Final stage (never given a match) is the one deleted, not
  League.
