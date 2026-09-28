# Quizware Postman collection

`Quizware.postman_collection.json` covers every implemented endpoint through
**Phase 10 (match engine, scoring and standings)**. Endpoints that are still
`501` stubs — admin lookups, match preflight, live snapshot/restore, Qualification,
Buzzer, Display, Reports — are intentionally not included; there's nothing
real to test yet.

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
   default to `admin@quizapp.local` / `KeepMeUpdated@123` — the values
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
11. **10 Matches** — the `Setup —` requests first capture the admin's user
    id (the disqualify request needs it), create teams B–E, shrink the
    League MCQ template to 2 questions, add a Passing template, and create
    and approve 3 MCQ questions and 1 Passing question, so the match below
    can be started. Then it runs the whole match-setup API: create, list,
    get, update, add three participants, order them, add/remove segments,
    reset to template, reorder, and **Mark Match Ready** (asserts no
    blockers). It also creates and deletes a scratch match, and auto-seeds
    teams D and E into a second match, which is abandoned in the next folder.
12. **11 Live Match** — plays the match end to end: start, state, open a
    segment, preview/serve/get a question, pause/resume, record an answer
    (with an `Idempotency-Key` header), reverse it and re-record it,
    reveal and skip a question, close a segment and skip another, then pass
    a Passing question and answer it with `passNumber: 1`, disqualify and
    reinstate a team, read the timeline, end the match, and abandon the
    auto-seeded match. Three requests **expect a 409** on purpose: passing
    an MCQ question, selecting a topic outside a Choice segment, and
    reordering segments live (no endpoint can yet set the stage's
    `AllowSegmentReorderDuringMatch` flag).
13. **12 Scores** — match scores, the score-event ledger (the reversed
    answer is still there, marked `isReversed`), a +5 adjustment, and a
    recalculation that must leave the totals unchanged.
14. **13 Standings** — overall, stage (both routes) and team standings for
    the completed match.

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
- An unattended `newman` run therefore reports exactly **5 failures**: the
  team-import validate/commit pair, media upload, and the MCQ-import
  validate/commit pair. Everything else should pass.
- `Create Passing Question` sends `passDirection` as a number (`1` =
  Clockwise). The API has no string-enum JSON converter, so `"Clockwise"`
  is rejected with a 400.
- `Delete Stage` in folder 08 only works on a stage with zero matches — that
  is why the Final stage (never given a match) is the one deleted, not
  League.
