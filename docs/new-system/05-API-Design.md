# 5. API Design

Base URL: `https://api.quizapp.local/api/v1`
Format: JSON, `camelCase`
Auth: `Authorization: Bearer <jwt>`

---

## 5.1 General rules

| Rule | Detail |
|---|---|
| Versioning | Path-based: `/api/v1/...` |
| IDs | GUIDs, sent as strings |
| Dates | UTC ISO-8601, property names end in `Utc` |
| Paging | `?page=1&pageSize=50` (max 200); response wraps in `PagedResult<T>` |
| Sorting | `?sort=name,-createdAtUtc` (minus = descending) |
| Filtering | Explicit named query parameters, never a free-form filter string |
| Errors | RFC 9457 `application/problem+json` |
| Concurrency | `ETag` on GET, `If-Match` required on PUT/PATCH |
| Idempotency | `Idempotency-Key` header required on all gameplay POSTs |
| Correlation | `X-Correlation-Id` echoed on every response |
| Program scope | Taken from the JWT `program_id` claim; path `{programId}` must match it |

---

## 5.2 Standard envelopes

### Paged result

```json
{
  "items": [ ... ],
  "page": 1,
  "pageSize": 50,
  "totalCount": 137,
  "totalPages": 3
}
```

### Error (RFC 9457)

```json
{
  "type": "https://quizapp/errors/question-pool-exhausted",
  "title": "Not enough questions available",
  "status": 409,
  "detail": "Segment 'MCQ' needs 6 questions at difficulty 2-3 but only 4 are available.",
  "instance": "/api/v1/matches/8f2.../live/start",
  "errorCode": "QUESTION_POOL_EXHAUSTED",
  "correlationId": "d41c...",
  "extensions": {
    "format": "MCQ",
    "required": 6,
    "available": 4,
    "difficultyRange": [2, 3],
    "suggestion": "Add 2 more MCQ questions at difficulty 2 or 3, or widen the selection rule."
  }
}
```

### Validation error

```json
{
  "type": "https://quizapp/errors/validation",
  "title": "One or more validation errors occurred",
  "status": 400,
  "errorCode": "VALIDATION_FAILED",
  "errors": {
    "difficultyLevelId": ["Must be between 1 and 5."],
    "options": ["At least 2 options are required.",
                "Exactly one option must be marked correct."]
  }
}
```

### Error codes

| Code | Status | Meaning |
|---|---|---|
| `VALIDATION_FAILED` | 400 | Request body failed validation |
| `UNAUTHENTICATED` | 401 | Missing or expired token |
| `FORBIDDEN` | 403 | Role or program scope does not allow this |
| `NOT_FOUND` | 404 | Entity does not exist in this program |
| `CONFLICT_STATE` | 409 | Action not allowed in the current state |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` did not match |
| `QUESTION_POOL_EXHAUSTED` | 409 | Not enough questions for the rule |
| `INSUFFICIENT_PARTICIPANTS` | 409 | Fewer than 2 active teams |
| `SCORING_RULE_MISSING` | 409 | No rule for this format + outcome |
| `IDEMPOTENCY_MISMATCH` | 409 | Same key, different body |
| `UNRESOLVED_TIE` | 409 | A tie affecting a qualifying place is still open |
| `TIE_BREAK_NOT_APPLICABLE` | 409 | The tie was already resolved, or does not affect qualification |
| `SEGMENT_NOT_REORDERABLE` | 409 | Segment already opened, locked, or the stage forbids live reordering |
| `FORMAT_IN_USE` | 409 | Cannot disable a format still used by a segment template |
| `STAGE_HAS_NO_SEGMENTS` | 409 | A stage needs at least one segment to start |
| `BUZZER_UNAVAILABLE` | 503 | Provider offline — **never blocks gameplay** |
| `RATE_LIMITED` | 429 | Too many requests |

---

## 5.3 Controllers and endpoints

### `AuthController` — `/api/v1/auth`

| Method | Route | Role | Purpose |
|---|---|---|---|
| POST | `/login` | anon | Email + password → access + refresh token |
| POST | `/refresh` | anon | Rotate the refresh token |
| POST | `/logout` | any | Revoke the refresh token |
| GET | `/me` | any | Current user, roles and accessible programs |
| POST | `/select-program` | any | Issue a token scoped to one program |
| POST | `/display-token` | ProgramAdmin | Mint a read-only display token |
| POST | `/change-password` | any | |

### `ProgramsController` — `/api/v1/programs`

| Method | Route | Role | Purpose |
|---|---|---|---|
| GET | `/` | Super, ProgAdmin | List accessible programs |
| GET | `/{id}` | ProgAdmin+ | Program detail |
| POST | `/` | SuperAdmin | Create a program |
| PUT | `/{id}` | ProgAdmin | Update |
| POST | `/{id}/clone` | ProgAdmin | Copy config from another program |
| GET | `/{id}/formats` | ProgAdmin | Which question formats this program uses |
| PUT | `/{id}/formats` | ProgAdmin | **Enable/disable formats** — omit or disable the ones you are not using |
| GET | `/{id}/settings` | ProgAdmin | All settings |
| PUT | `/{id}/settings` | ProgAdmin | Bulk update settings |
| POST | `/{id}/validate` | ProgAdmin | **Readiness check before going live** |
| POST | `/{id}/activate` | ProgAdmin | `Configured → Live` |
| POST | `/{id}/complete` | ProgAdmin | `Live → Completed` |
| POST | `/{id}/archive` | ProgAdmin | |
| GET | `/{id}/dashboard` | ProgAdmin | Counts, progress, warnings |

### `TeamsController` — `/api/v1/programs/{programId}/teams`

| Method | Route | Role | Purpose |
|---|---|---|---|
| GET | `/` | any | List, filter by status, paged |
| GET | `/{id}` | any | Detail with members |
| POST | `/` | ProgAdmin | Create |
| PUT | `/{id}` | ProgAdmin | Update |
| DELETE | `/{id}` | ProgAdmin | Soft delete (blocked if already played) |
| POST | `/{id}/status` | ProgAdmin | Change status with reason |
| POST | `/{id}/images` | ProgAdmin | Upload score / selection images |
| POST | `/import/validate` | ProgAdmin | Upload Excel → **validation report only** |
| POST | `/import/{batchId}/commit` | ProgAdmin | Commit the validated batch |
| GET | `/{id}/history` | any | Matches played, scores, status changes |

### `QuestionsController` — `/api/v1/programs/{programId}/questions`

| Method | Route | Role | Purpose |
|---|---|---|---|
| GET | `/` | Author+ | Filter by format, difficulty, topic, tag, status, text. Queries the **base** table — returns a summary row per question |
| GET | `/{id}` | Author+ | Full detail, polymorphic by format (options, sequence items or media) |
| POST | `/{formatCode}` | Author+ | **Create — one route per format**, strongly typed (`/mcq`, `/buzzer`, `/audio-visual`, `/sequence`, …) |
| PUT | `/{formatCode}/{id}` | Author+ | Update; creates a version if already used |
| DELETE | `/{id}` | ProgAdmin | Soft delete (base row; format row and children cascade) |
| POST | `/{id}/approve` | ProgAdmin | `Draft → Approved` |
| POST | `/{id}/retire` | ProgAdmin | Stop using it |
| POST | `/import/{formatCode}/validate` | Author+ | Excel upload → validation report, using that format's column template |
| POST | `/import/{batchId}/commit` | Author+ | Commit |
| GET | `/coverage` | ProgAdmin | **Do we have enough questions?** per format/difficulty |
| GET | `/{id}/usage` | Author+ | Where it has been used |
| POST | `/media` | Author+ | Upload an audio/image/video asset |
| GET | `/duplicates` | Author+ | Near-duplicate report |

### `TopicsController`, `TagsController`

Standard CRUD under `/api/v1/programs/{programId}/topics` and `/tags`.

### `StagesController` — `/api/v1/programs/{programId}/stages`

| Method | Route | Role | Purpose |
|---|---|---|---|
| GET | `/` | any | Ordered stage list |
| GET | `/{id}` | any | Stage with segment templates and rules |
| POST | `/` | ProgAdmin | Create |
| PUT | `/{id}` | ProgAdmin | Update |
| DELETE | `/{id}` | ProgAdmin | Soft delete (blocked if matches exist) |
| PUT | `/reorder` | ProgAdmin | Reorder stages |
| GET | `/{id}/segments` | any | Segment templates |
| POST | `/{id}/segments` | ProgAdmin | Add a segment |
| PUT | `/{id}/segments/{segId}` | ProgAdmin | Update |
| DELETE | `/{id}/segments/{segId}` | ProgAdmin | Remove |
| PUT | `/{id}/segments/reorder` | ProgAdmin | **Reorder the question types** — send the ordered list of segment ids |
| PUT | `/{id}/segment-order-mode` | ProgAdmin | `Fixed` / `RandomPerMatch` / `OperatorChoice` |
| GET | `/{id}/standings` | any | Stage leaderboard |
| POST | `/{id}/validate` | ProgAdmin | Check the stage is runnable |

**Worked example — a year without Passing, Card or Choice.**

Configuring the League stage is four `POST`s. There is no step that requires the
other six formats, and nothing to "switch off" unless you want them hidden:

```
POST /programs/{p}/stages/{league}/segments   { format: "MCQ",         questionCount: 6 }
POST /programs/{p}/stages/{league}/segments   { format: "AudioVisual", questionCount: 3 }
POST /programs/{p}/stages/{league}/segments   { format: "Sequence",    questionCount: 2 }
POST /programs/{p}/stages/{league}/segments   { format: "Buzzer",      questionCount: 5 }

GET  /programs/{p}/stages/{league}
→ 4 segments, in that order. The match plays exactly these four.
   Passing, Card, Choice, RapidFire, VisualRapidFire: not configured,
   therefore never drawn, never scored, never validated against.
```

To add Passing back next year, add one row. To reorder, send the ordered list.
Neither needs a deployment.

### `RulesController` — `/api/v1/programs/{programId}/rules`

| Method | Route | Role | Purpose |
|---|---|---|---|
| GET | `/scoring` | ProgAdmin | All scoring rules |
| PUT | `/scoring` | ProgAdmin | Bulk upsert |
| POST | `/scoring/reset-defaults` | ProgAdmin | Restore the seeded values |
| GET | `/selection` | ProgAdmin | All selection rules |
| PUT | `/selection` | ProgAdmin | Bulk upsert |
| POST | `/selection/preview` | ProgAdmin | **Dry-run a draw without consuming questions** |
| GET | `/qualification` | ProgAdmin | All qualification rules |
| PUT | `/qualification` | ProgAdmin | Bulk upsert |
| GET | `/tie-break` | ProgAdmin | All tie-break rules |
| PUT | `/tie-break` | ProgAdmin | Bulk upsert (criteria order, format, counts, fallback) |
| POST | `/tie-break/reset-defaults` | ProgAdmin | Restore the seeded MCQ defaults |

### `MatchesController` — `/api/v1/programs/{programId}/matches`

| Method | Route | Role | Purpose |
|---|---|---|---|
| GET | `/` | any | Filter by stage and state |
| GET | `/{id}` | any | Detail with participants |
| POST | `/` | ProgAdmin | Create a match in a stage |
| PUT | `/{id}` | ProgAdmin | Update (only while `Draft`/`Ready`) |
| DELETE | `/{id}` | ProgAdmin | Soft delete |
| POST | `/{id}/participants` | ProgAdmin | Add a team |
| DELETE | `/{id}/participants/{pid}` | ProgAdmin | Remove before start |
| PUT | `/{id}/participants/order` | ProgAdmin | Set seats and turn order |
| GET | `/{id}/segments` | ProgAdmin, Operator | This match's effective segment order |
| PUT | `/{id}/segments/reorder` | ProgAdmin, Operator | **Override the question-type order for this match** |
| POST | `/{id}/segments` | ProgAdmin | Add a segment to this match only |
| DELETE | `/{id}/segments/{segId}` | ProgAdmin | Remove a segment from this match only |
| POST | `/{id}/segments/reset-to-template` | ProgAdmin | Discard the override |
| POST | `/{id}/ready` | ProgAdmin, Operator | `Draft → Ready` after pre-flight checks |
| POST | `/auto-seed` | ProgAdmin | Generate matches for a stage automatically |
| GET | `/{id}/preflight` | Operator | Questions ready? Buzzer healthy? Displays online? |

### `LiveMatchController` — `/api/v1/matches/{matchId}/live` (the engine)

| Method | Route | Role | Purpose |
|---|---|---|---|
| GET | `/state` | Operator, Display | **One call returns everything the screen needs** |
| POST | `/start` | Operator | Reserve questions, store seed, `Ready → InProgress` |
| POST | `/pause` | Operator | |
| POST | `/resume` | Operator | |
| POST | `/segments/{segId}/open` | Operator | Open the next segment |
| POST | `/segments/{segId}/close` | Operator | Close it |
| POST | `/segments/{segId}/skip` | Operator | Skip with a reason |
| PUT | `/segments/reorder` | Operator | **Reorder pending segments mid-match** (only when the stage allows) |
| GET | `/segments/next-options` | Operator | Under `OperatorChoice`, the pending segments to pick from |
| GET | `/next-question` | Operator | Peek at the next question and target team |
| POST | `/questions/serve` | Operator | Show the question, start the server timer |
| GET | `/questions/{mqId}` | Operator | Full question with options (correct answer included) |
| POST | `/questions/{mqId}/reveal` | Operator | Reveal the answer to displays |
| POST | `/questions/{mqId}/skip` | Operator | Skip this question |
| POST | `/answers` | Operator, Scorer | **Record an answer (idempotent)** |
| POST | `/answers/{id}/reverse` | Operator, ProgAdmin | Undo with a reason |
| POST | `/pass` | Operator | Pass the question to the next active team |
| POST | `/topics/select` | Operator | Choice round — the team picks a topic |
| GET | `/topics/available` | Operator, Display | Remaining topics and the limit |
| POST | `/participants/{pid}/disqualify` | ProgAdmin | **Remove a team; the match continues** |
| POST | `/participants/{pid}/reinstate` | ProgAdmin | Undo a removal |
| POST | `/end` | Operator | Compute standings, `→ Completed` |
| POST | `/abandon` | ProgAdmin | With a reason |
| GET | `/timeline` | Operator, Auditor | The `MatchEvent` list |
| POST | `/snapshot` | ProgAdmin | Save a recovery point |
| POST | `/restore/{snapshotId}` | ProgAdmin | Restore it |

### `ScoresController` — `/api/v1/matches/{matchId}/scores`

| Method | Route | Role | Purpose |
|---|---|---|---|
| GET | `/` | any | Live scores per team, with breakdown |
| GET | `/events` | Operator, Auditor | The score-event ledger |
| POST | `/adjust` | ProgAdmin | Manual adjustment with reason |
| POST | `/recalculate` | ProgAdmin | Rebuild `TeamMatchScore` from events |

### `StandingsController` — `/api/v1/programs/{programId}/standings`

| Method | Route | Purpose |
|---|---|---|
| GET | `/overall` | Whole-program leaderboard |
| GET | `/stages/{stageId}` | Stage leaderboard with tie-break detail |
| GET | `/teams/{teamId}` | One team's full record |

### `QualificationController` — `/api/v1/programs/{programId}/qualification`

| Method | Route | Role | Purpose |
|---|---|---|---|
| GET | `/stages/{stageId}/preview` | ProgAdmin | **Who advances and why — before committing** |
| POST | `/stages/{stageId}/wildcards` | ProgAdmin | Set manual wildcards |
| POST | `/stages/{stageId}/commit` | ProgAdmin | Create the next stage's matches |
| POST | `/stages/{stageId}/rollback` | ProgAdmin | Undo, only before that stage starts |
| GET | `/stages/{stageId}/ties` | ProgAdmin | All ties detected, resolved and unresolved |
| POST | `/stages/{stageId}/ties/detect` | ProgAdmin | Re-run detection and apply the criteria phase |
| GET | `/ties/{tieId}` | ProgAdmin | One tie: who is tied, criteria tried, current state |
| POST | `/ties/{tieId}/tie-break-match` | ProgAdmin | **Create the tie-break match** from the rule |
| POST | `/ties/{tieId}/resolve-manually` | ProgAdmin | Record a manual decision with a reason |
| POST | `/ties/{tieId}/abandon` | ProgAdmin | Cancel a tie-break that is no longer needed |

### `BuzzerController` — `/api/v1/buzzer` *(optional module)*

**Every endpoint here returns `503 BUZZER_UNAVAILABLE` when the module is off.
Nothing else in the system is affected.**

| Method | Route | Role | Purpose |
|---|---|---|---|
| GET | `/capability` | Operator, Display | `{ available, provider, deviceCount }` |
| GET | `/health` | Operator | Provider health check |
| GET | `/devices` | Operator | Discovered devices / COM ports |
| PUT | `/matches/{matchId}/mappings` | ProgAdmin | Map devices to participants |
| POST | `/matches/{matchId}/sessions` | Operator | Arm a buzz session |
| GET | `/sessions/{id}` | Operator, Display | Session state + ranked presses |
| POST | `/sessions/{id}/presses` | **Agent** | **Agent pushes results here** |
| POST | `/sessions/{id}/collect` | Operator | Poll the provider now |
| POST | `/sessions/{id}/reset` | Operator | Reset the devices |
| POST | `/test` | Operator | Read all buttons — the rehearsal screen |

### `DisplayController` — `/api/v1/display` *(read-only, display token)*

| Method | Route | Purpose |
|---|---|---|
| GET | `/matches/{matchId}/state` | Screen state; **correct answers omitted until revealed** |
| GET | `/matches/{matchId}/scores` | Live scoreboard |
| GET | `/programs/{programId}/standings` | Standings screen |
| GET | `/programs/{programId}/branding` | Logo, colours, fonts |

### `ReportsController` — `/api/v1/programs/{programId}/reports`

| Method | Route | Purpose |
|---|---|---|
| GET | `/matches/{matchId}` | Full match report |
| GET | `/stages/{stageId}` | Stage summary |
| GET | `/teams/{teamId}` | Team performance |
| GET | `/questions/usage` | Which questions were used and how they performed |
| GET | `/audit` | Filtered audit log |
| GET | `/export/{reportType}?format=xlsx\|csv\|pdf` | Export |

### `AdminController` — `/api/v1/admin`

| Method | Route | Role | Purpose |
|---|---|---|---|
| GET | `/users` | Super, ProgAdmin | List users |
| POST | `/users` | Super, ProgAdmin | Invite |
| PUT | `/users/{id}/roles` | Super, ProgAdmin | Assign roles for a program |
| POST | `/users/{id}/deactivate` | Super, ProgAdmin | |
| GET | `/lookups` | any | All lookup tables in one call, cacheable |
| GET | `/health` | anon | Liveness |
| GET | `/health/ready` | anon | Readiness including DB and buzzer |

---

## 5.4 Key request and response models

### `POST /matches/{id}/live/start`

Request: *(empty)*
Headers: `Idempotency-Key: <guid>`

Response `200`:

```json
{
  "matchId": "8f2b...",
  "state": "InProgress",
  "randomSeed": 748291043,
  "startedAtUtc": "2026-03-14T09:02:11.421Z",
  "participants": [
    { "participantId": "a1...", "teamId": "t1...", "teamName": "الحمد",
      "seatNumber": 1, "turnOrder": 1, "status": "Active",
      "scoreImageUrl": "/media/AlHamd.png", "buzzDeviceId": 1 },
    { "participantId": "a2...", "teamId": "t2...", "teamName": "مدنی(ب)",
      "seatNumber": 2, "turnOrder": 2, "status": "Active",
      "scoreImageUrl": "/media/MadniBoys.png", "buzzDeviceId": 2 },
    { "participantId": "a3...", "teamId": "t3...", "teamName": "رفیع الدین(گ)",
      "seatNumber": 3, "turnOrder": 3, "status": "Active",
      "scoreImageUrl": "/media/RafiuddinGirls.png", "buzzDeviceId": 3 }
  ],
  "segments": [
    { "segmentId": "s1...", "format": "MCQ", "orderIndex": 1,
      "plannedQuestionCount": 6, "state": "Pending" },
    { "segmentId": "s2...", "format": "AudioVisual", "orderIndex": 2,
      "plannedQuestionCount": 3, "state": "Pending" },
    { "segmentId": "s3...", "format": "Sequence", "orderIndex": 3,
      "plannedQuestionCount": 2, "state": "Pending" },
    { "segmentId": "s4...", "format": "Buzzer", "orderIndex": 4,
      "plannedQuestionCount": 5, "state": "Pending" }
  ],
  "questionsReserved": 16,
  "buzzerAvailable": true
}
```

### `GET /matches/{id}/live/state`

**One call gives the operator console everything.** This replaces the current
`OnXxxLoad` methods that each ran 3 stored procedures.

```json
{
  "matchId": "8f2b...",
  "matchName": "League Match 1",
  "stageName": "League",
  "state": "InProgress",
  "isPaused": false,
  "currentSegment": {
    "segmentId": "s1...",
    "format": "MCQ",
    "displayName": "Multiple Choice",
    "orderIndex": 1,
    "state": "Open",
    "plannedQuestionCount": 6,
    "servedQuestionCount": 2,
    "topicSelectionMode": "None"
  },
  "currentQuestion": {
    "matchQuestionId": "mq3...",
    "orderIndex": 3,
    "state": "Active",
    "format": "MCQ",
    "questionText": "...",
    "difficultyLevel": 2,
    "topicName": "Literature",
    "mediaUrl": null,
    "options": [
      { "optionId": "o1...", "text": "...", "displayOrder": 1 },
      { "optionId": "o2...", "text": "...", "displayOrder": 2 },
      { "optionId": "o3...", "text": "...", "displayOrder": 3 },
      { "optionId": "o4...", "text": "...", "displayOrder": 4 }
    ],
    "correctOptionId": "o2...",
    "timeLimitSeconds": 30,
    "timerStartedAtUtc": "2026-03-14T09:07:44.100Z",
    "serverNowUtc": "2026-03-14T09:07:52.310Z",
    "remainingSeconds": 21.7
  },
  "activeParticipant": {
    "participantId": "a2...", "teamId": "t2...", "teamName": "مدنی(ب)",
    "seatNumber": 2, "turnOrder": 2
  },
  "participants": [
    { "participantId": "a1...", "teamName": "الحمد", "seatNumber": 1,
      "turnOrder": 1, "status": "Active", "score": 30 },
    { "participantId": "a2...", "teamName": "مدنی(ب)", "seatNumber": 2,
      "turnOrder": 2, "status": "Active", "score": 20 },
    { "participantId": "a3...", "teamName": "رفیع الدین(گ)", "seatNumber": 3,
      "turnOrder": 3, "status": "Active", "score": 40 }
  ],
  "progress": { "questionsServed": 3, "questionsTotal": 16,
                "segmentsCompleted": 0, "segmentsTotal": 4 },
  "buzzer": { "available": true, "sessionId": null, "state": "Idle" },
  "canUndo": true,
  "lastAnswerId": "ans7..."
}
```

**Note on `activeParticipant`:** it is computed from **active participants only**.
If a team is disqualified, this value shifts correctly with no code change.

### `POST /matches/{id}/live/answers`

```json
{
  "matchQuestionId": "mq3...",
  "matchParticipantId": "a2...",
  "outcome": "Correct",
  "selectedOptionId": "o2...",
  "selectedOptionIds": null,
  "freeTextAnswer": null,
  "passNumber": 0,
  "answerSource": "Operator",
  "buzzPressId": null,
  "responseTimeMs": 8210
}
```

Headers: `Idempotency-Key: 4f3e...`

Response `200`:

```json
{
  "answerRecordId": "ans8...",
  "outcome": "Correct",
  "pointsAwarded": 10,
  "scoringRuleId": "sr12...",
  "teamScore": 30,
  "scores": [
    { "teamId": "t1...", "score": 30, "rank": 2 },
    { "teamId": "t2...", "score": 30, "rank": 2 },
    { "teamId": "t3...", "score": 40, "rank": 1 }
  ],
  "nextQuestion": {
    "matchQuestionId": "mq4...",
    "activeParticipantId": "a3...",
    "activeTeamName": "رفیع الدین(گ)"
  },
  "segmentComplete": false,
  "matchComplete": false
}
```

**For Sequence questions,** `selectedOptionIds` carries the order the team gave:
`["o3...", "o1...", "o4...", "o2..."]`, and the engine compares it against each
option's `CorrectSequenceNumber`.

### `POST /matches/{id}/live/participants/{pid}/disqualify`

**This is the endpoint that solves your problem.**

```json
{
  "reason": "Used a mobile phone during the Sequence segment",
  "approvedByUserId": "u9...",
  "excludeFromStandings": true
}
```

Response `200`:

```json
{
  "participantId": "a2...",
  "teamName": "مدنی(ب)",
  "status": "Disqualified",
  "removedAtUtc": "2026-03-14T09:21:03.008Z",
  "remainingActiveParticipants": [
    { "participantId": "a1...", "teamName": "الحمد",
      "seatNumber": 1, "turnOrder": 1, "score": 30 },
    { "participantId": "a3...", "teamName": "رفیع الدین(گ)",
      "seatNumber": 3, "turnOrder": 2, "score": 40 }
  ],
  "matchCanContinue": true,
  "turnOrderRecalculated": true,
  "currentSegmentAdjustment": {
    "policy": "Rebalance",
    "plannedQuestionCountBefore": 6,
    "plannedQuestionCountAfter": 4,
    "message": "Segment rebalanced for 2 active teams."
  },
  "nextActiveParticipantId": "a3..."
}
```

Note `turnOrder` for team 3 changed from 3 to 2 while `seatNumber` stayed at 3 —
the team does not physically move on stage, but the rotation closes up. **The
match continues with 2 teams and no fake answers.**

If only one team would remain, the response is instead:

```json
{
  "matchCanContinue": false,
  "matchCompleted": true,
  "winnerTeamId": "t3...",
  "reason": "Only one active participant remains."
}
```

### `PUT /programs/{id}/stages/{stageId}/segments/reorder`

Changing the order of question types. The whole order is sent at once, so the
result is unambiguous and the server rewrites all indexes in one transaction —
this is what a drag-and-drop list produces.

```json
{
  "orderedSegmentTemplateIds": [
    "seg-buzzer-id",
    "seg-mcq-id",
    "seg-sequence-id",
    "seg-audiovisual-id"
  ]
}
```

Response `200`:

```json
{
  "stageId": "st1...",
  "segmentOrderMode": "Fixed",
  "segments": [
    { "id": "seg-buzzer-id",      "orderIndex": 1, "format": "Buzzer",      "questionCount": 5, "isOrderLocked": false },
    { "id": "seg-mcq-id",         "orderIndex": 2, "format": "MCQ",         "questionCount": 6, "isOrderLocked": false },
    { "id": "seg-sequence-id",    "orderIndex": 3, "format": "Sequence",    "questionCount": 2, "isOrderLocked": false },
    { "id": "seg-audiovisual-id", "orderIndex": 4, "format": "AudioVisual", "questionCount": 3, "isOrderLocked": false }
  ],
  "affectedMatches": {
    "notYetCreated": "will use the new order",
    "alreadyCreated": 0,
    "inProgress": 0
  }
}
```

**Validation:** the list must contain every segment of the stage exactly once.
A partial list is rejected with `VALIDATION_FAILED` rather than being guessed at.
Locked segments must keep their existing index.

**Matches already created keep the order they were given** — `affectedMatches`
says so explicitly, so an admin editing the template mid-tournament can see that
live matches are untouched.

### `PUT /matches/{matchId}/segments/reorder`

The same shape, overriding the order for one match only. During a live match it
accepts only `Pending` segments:

```json
{
  "orderedSegmentIds": ["seg-3", "seg-4", "seg-2"],
  "reason": "Audio desk not ready — moving Audio-Visual later"
}
```

Response `409 CONFLICT_STATE` if a listed segment is already `Open` or
`Completed`, or if `Stage.AllowSegmentReorderDuringMatch = 0`, with
`extensions.suggestion` naming which segment blocked it.

### `GET /programs/{id}/qualification/stages/{stageId}/ties`

```json
{
  "stageId": "st1...",
  "ties": [
    {
      "tieBreakEventId": "tb1...",
      "state": "AwaitingPlay",
      "contestedRank": 9,
      "contestedSlots": 1,
      "affectsQualification": true,
      "teams": [
        { "teamId": "t2...",  "teamName": "مدنی(ب)",    "enteringScore": 110 },
        { "teamId": "t14...", "teamName": "مومن گرلز", "enteringScore": 110 }
      ],
      "criteriaApplied": [
        { "criterion": "TotalScore",                "separated": false, "values": [110, 110] },
        { "criterion": "FewerIncorrect",            "separated": false, "values": [4, 4] },
        { "criterion": "MoreCorrectAtHighDifficulty","separated": false, "values": [2, 2] },
        { "criterion": "FasterAverageBuzzTime",     "separated": false, "reason": "No buzzer data in this stage" },
        { "criterion": "HeadToHead",                "separated": false, "reason": "Teams never met" }
      ],
      "rule": {
        "tieBreakFormat": "MCQ",
        "questionCount": 3,
        "difficultyRange": [3, 5],
        "suddenDeath": false,
        "maxExtraRounds": 3,
        "scoreCountsTowardStage": false,
        "onStillTied": "ManualDecision"
      },
      "availableActions": ["CreateTieBreakMatch", "ResolveManually"]
    }
  ],
  "blocksCommit": true
}
```

### `POST /programs/{id}/qualification/ties/{tieId}/tie-break-match`

Creates the tie-break match. The body is optional — omit it to use the stage's
configured rule, or override any field for this one tie.

```json
{
  "formatCodeOverride": "Buzzer",
  "questionCountOverride": 5,
  "suddenDeathOverride": true,
  "reason": "Both teams requested a buzzer decider"
}
```

Response `201`:

```json
{
  "tieBreakEventId": "tb1...",
  "matchId": "m-tb-1...",
  "matchKind": "TieBreak",
  "stageId": "st1...",
  "matchNumber": 7,
  "name": "League — Tie-break for 9th place",
  "state": "Ready",
  "participants": [
    { "teamId": "t2...",  "teamName": "مدنی(ب)",    "seatNumber": 1, "turnOrder": 1 },
    { "teamId": "t14...", "teamName": "مومن گرلز", "seatNumber": 2, "turnOrder": 2 }
  ],
  "segments": [
    { "segmentId": "s-tb-1", "format": "MCQ", "orderIndex": 1,
      "plannedQuestionCount": 3, "isSuddenDeath": false }
  ],
  "questionsReserved": 3,
  "scoreCountsTowardStage": false,
  "nextStep": "Run this match on the operator console, then re-run the qualification preview."
}
```

From here it is **an ordinary match**: the operator opens
`/matches/{matchId}/live/state` and plays it exactly like any other. No separate
tie-break screen or endpoint exists.

When the match completes, the engine writes the `TieBreakEvent` resolution and
the per-team ranks automatically, and the qualification preview becomes
committable.

### `POST /programs/{id}/qualification/ties/{tieId}/resolve-manually`

```json
{
  "rankedTeamIds": ["t2...", "t14..."],
  "method": "ManualDecision",
  "reason": "Momin Girls withdrew from the tie-break; Madni (B) advances.",
  "approvedByUserId": "u9..."
}
```

Rejected with `403` unless the caller is a ProgramAdmin, and with
`VALIDATION_FAILED` if `reason` is empty — a manual override must always be
explainable after the event.

### `GET /programs/{id}/qualification/stages/{stageId}/preview`

```json
{
  "fromStage": { "id": "st1...", "name": "League", "orderIndex": 1 },
  "toStage":   { "id": "st2...", "name": "Semi-Final", "orderIndex": 2 },
  "rule": { "winnersPerMatch": 1, "bestRemainingAcrossStage": 3,
            "manualWildcardSlots": 0, "totalSlots": 9 },
  "allMatchesComplete": true,
  "qualifiers": [
    { "teamId": "t3...", "teamName": "رفیع الدین(گ)", "sourceMatch": "League 1",
      "stageScore": 145, "stageRank": 1, "reason": "MatchWinner" },
    { "teamId": "t7...", "teamName": "سراج العلوم(گ)", "sourceMatch": "League 2",
      "stageScore": 138, "stageRank": 2, "reason": "MatchWinner" },
    { "teamId": "t2...", "teamName": "مدنی(ب)",  "sourceMatch": "League 1",
      "stageScore": 120, "stageRank": 7, "reason": "BestRemaining" }
  ],
  "eliminated": [
    { "teamId": "t9...", "teamName": "ملّت", "stageScore": 95, "stageRank": 12 }
  ],
  "unresolvedTies": [
    { "rank": 9,
      "teams": [
        { "teamId": "t11...", "teamName": "النور",  "stageScore": 110 },
        { "teamId": "t14...", "teamName": "مومن گرلز", "stageScore": 110 }
      ],
      "criteriaApplied": ["TotalScore", "FewerIncorrect"],
      "stillTied": true,
      "resolutionOptions": ["TieBreakerMatch", "ManualDecision"] }
  ],
  "canCommit": false,
  "blockedReason": "1 unresolved tie on the qualification boundary."
}
```

### `POST /programs/{id}/rules/selection/preview`

Lets an admin test a selection rule **without consuming any questions**.

Request:

```json
{ "stageId": "st2...", "formatCode": "MCQ", "questionCount": 6 }
```

Response:

```json
{
  "poolSize": 84,
  "eligibleAfterFilters": 41,
  "eligibleAfterRepeatPolicy": 33,
  "difficultyMixRequested": { "2": 60, "3": 40 },
  "difficultyMixAchievable": { "2": 4, "3": 2 },
  "canSatisfy": true,
  "sample": [
    { "questionId": "q1...", "difficulty": 2, "topic": "Literature",
      "preview": "..." }
  ],
  "warnings": ["Only 9 level-3 MCQ questions remain for this program."]
}
```

### `GET /programs/{id}/questions/coverage`

Answers the question every organiser actually cares about: *do we have enough
questions to run the event?*

**It reports only on formats the program actually uses.** Requirements are
derived from the segment templates that exist, so a format you have not
configured is never listed, never counted and never a blocker.

```json
{
  "readyToRun": false,
  "formatsInUse": ["MCQ", "AudioVisual", "Sequence", "Buzzer"],
  "formatsNotUsed": ["Passing", "Card", "Choice", "RapidFire",
                     "VisualRapidFire", "TieBreaker"],
  "byStage": [
    { "stageName": "League", "matches": 6,
      "requirements": [
        { "format": "MCQ", "difficultyRange": [1,2],
          "requiredTotal": 36, "available": 84, "status": "Ok" },
        { "format": "Sequence", "difficultyRange": [1,2],
          "requiredTotal": 12, "available": 9,
          "status": "Short", "shortfall": 3 }
      ] }
  ],
  "blockers": [
    "Stage 'League' needs 3 more Sequence questions at difficulty 1-2."
  ]
}
```

Note what is **not** in `blockers`: nothing about Passing, Card or Choice. The
program does not use them, so no questions are required and readiness is not
affected. `formatsNotUsed` is informational only — it is there so an organiser
can spot a format they *meant* to include.

### `PUT /programs/{id}/formats`

Declare which question types this program uses. Optional — a new program starts
with all formats enabled, so ignoring this endpoint gives you every format
available.

```json
{
  "formats": [
    { "formatCode": "MCQ",             "isEnabled": true,  "displayOrder": 1 },
    { "formatCode": "AudioVisual",     "isEnabled": true,  "displayOrder": 2 },
    { "formatCode": "Sequence",        "isEnabled": true,  "displayOrder": 3 },
    { "formatCode": "Buzzer",          "isEnabled": true,  "displayOrder": 4 },
    { "formatCode": "Passing",         "isEnabled": false, "disabledReason": "Not running Passing this year" },
    { "formatCode": "Card",            "isEnabled": false },
    { "formatCode": "Choice",          "isEnabled": false },
    { "formatCode": "RapidFire",       "isEnabled": false },
    { "formatCode": "VisualRapidFire", "isEnabled": false },
    { "formatCode": "TieBreaker",      "isEnabled": true,  "displayOrder": 5 }
  ]
}
```

Disabled formats vanish from the segment-template picker, the question-bank
filters and the import templates. Existing questions are untouched.

If a disabled format is still referenced by a segment template, the request is
rejected — the disable is never silently destructive:

```json
{
  "type": "https://quizapp/errors/format-in-use",
  "title": "Question format is still in use",
  "status": 409,
  "errorCode": "FORMAT_IN_USE",
  "detail": "Passing cannot be disabled: it is used by 1 stage.",
  "extensions": {
    "formatCode": "Passing",
    "usedBy": [
      { "stageId": "st2...", "stageName": "Semi-Final", "segmentTemplateId": "seg9..." }
    ],
    "suggestion": "Remove the Passing segment from 'Semi-Final' first, then disable the format."
  }
}
```

---

## 5.5 Application services

| Service | Responsibility |
|---|---|
| `IProgramService` | Program CRUD, cloning, activation, readiness validation |
| `ITeamService` | Team CRUD, status changes, import |
| `IQuestionService` | Question CRUD, versioning, approval, duplicate detection |
| `IQuestionImportService` | Excel parse → validate → report → commit |
| `IMediaService` | Upload, validate magic bytes, deduplicate, store |
| `IStageConfigurationService` | Stages, segment templates, reordering, validation |
| `IRuleService` | Scoring, selection and qualification rules; resolution order |
| `IMatchSetupService` | Create matches, assign participants, seed automatically |
| **`IMatchEngine`** | **The core.** Start, open/close segments, serve, answer, pass, skip, undo, end |
| **`IQuestionSelector`** | Build the pool, apply difficulty mix, weighted draw, reserve |
| **`IScoringEngine`** | Resolve the rule, create score events, update read models |
| `ITurnOrderService` | Compute the next active participant; recompact after a removal |
| `IQualificationEvaluator` | Preview, resolve ties, commit, roll back |
| `ITieBreakService` | Detect ties, apply the ordered criteria, build and resolve tie-break matches, record `TieBreakEvent` |
| `ISegmentOrderService` | Resolve the effective segment order (template → per-match → live reorder), apply shuffling from the match seed, enforce locked segments |
| `IStandingsService` | Match, stage and program standings |
| `IMatchRecoveryService` | Snapshot, restore, replay the event timeline |
| `IDisplayProjectionService` | Build the read-only display payloads |
| `IReportingService` | Reports and exports |
| `IAuditService` | Read the audit log |
| **`IBuzzerProvider`** | **The optional port.** Arm, collect, reset, health |
| `IBuzzerSessionService` | Persist sessions and presses; rank; map devices to teams |
| `INotificationPublisher` | Push to SignalR via the outbox |
| `IIdempotencyService` | Store and replay idempotent responses |
| `ICurrentUser`, `ICurrentProgram`, `IClock` | Ambient context, all injectable and fake-able in tests |

---

## 5.6 Important workflows

### W1 — Starting a match (question selection happens here)

```
POST /matches/{id}/live/start
  │
  ├─ 1. Check state = Ready
  ├─ 2. Check active participants >= stage.MinTeamsPerMatch
  ├─ 3. Generate and store RandomSeed
  ├─ 4. For each StageSegmentTemplate in order:
  │        ├─ create a MatchSegment (state = Pending)
  │        ├─ resolve the QuestionSelectionRule
  │        │     (segment → stage → program, most specific wins)
  │        ├─ IQuestionSelector.SelectAsync(rule, count, seed, exclusions)
  │        │     ├─ build the pool  (format, language, topic, owner scope, approved)
  │        │     ├─ subtract used questions per RepeatPolicy
  │        │     ├─ split into difficulty buckets by DifficultyMixJson
  │        │     ├─ weighted random draw (lower TimesUsed = higher weight)
  │        │     ├─ apply the topic-spread policy
  │        │     └─ if short → widen difficulty → drop topics → precise error
  │        ├─ create MatchQuestion rows (state = Reserved) with the shuffled
  │        │     option order stored in OptionOrderJson
  │        └─ assign TargetParticipantId by turn order (turn-based formats only)
  ├─ 5. Set state = InProgress, StartedAtUtc
  ├─ 6. Write MatchEvent "MatchStarted"
  └─ 7. Publish to SignalR (via the outbox)

  ALL OF THIS IS ONE DATABASE TRANSACTION.
```

### W2 — Recording an answer

```
POST /matches/{id}/live/answers   (Idempotency-Key required)
  │
  ├─ 1. Idempotency check → if the key exists, replay the stored response
  ├─ 2. Validate: match InProgress, segment Open, question Active,
  │              participant belongs to this match and is Active
  ├─ 3. BEGIN TRANSACTION
  │      ├─ insert AnswerRecord
  │      ├─ resolve the ScoringRule (segment → stage → program)
  │      │      └─ if none found → 409 SCORING_RULE_MISSING (never guess)
  │      ├─ insert ScoreEvent
  │      ├─ update TeamMatchScore  (incremental, not a recalculation)
  │      ├─ update TeamStageScore
  │      ├─ set MatchQuestion.State = Answered
  │      ├─ insert QuestionUsageHistory
  │      ├─ increment Question.TimesUsed
  │      ├─ insert MatchEvent "AnswerRecorded"
  │      ├─ insert OutboxMessage
  │      └─ store the idempotency record
  │     COMMIT
  ├─ 4. Compute the next active participant from ACTIVE participants only
  ├─ 5. If the segment is finished → mark it Completed
  ├─ 6. If all segments are finished → the match may be ended
  └─ 7. Return the new state; the outbox pushes SignalR

  Total: 1 transaction, ~3 round trips.
  (Today: up to 12 round trips, 3 stored-procedure calls with 8 subqueries each.)
```

### W3 — Disqualifying a team mid-match

```
POST /matches/{id}/live/participants/{pid}/disqualify
  │
  ├─ 1. Check the caller is a ProgramAdmin, and a reason was given
  ├─ 2. BEGIN TRANSACTION
  │      ├─ participant.Status = Disqualified, record reason/when/who/segment
  │      ├─ recompact TurnOrder over the remaining ACTIVE participants
  │      ├─ if ExcludeFromStandings → flag their score events
  │      ├─ release any MatchQuestion still Reserved and targeted at them
  │      ├─ apply the stage's TeamCountChangePolicy to the open segment:
  │      │     KeepPlanned    → nothing changes
  │      │     Rebalance      → recompute PlannedQuestionCount for N teams
  │      │     TruncateSegment→ close the segment now
  │      ├─ if only 1 active participant remains → complete the match,
  │      │     declare that team the winner
  │      ├─ insert MatchEvent "ParticipantDisqualified"
  │      └─ insert OutboxMessage
  │     COMMIT
  └─ 3. Push the new state to every screen

  The match continues normally. No fake answer is ever needed.
```

### W4 — Buzzer question, with graceful degradation

```
Operator opens a Buzzer segment
  │
  ├─ GET /buzzer/capability
  │     ├─ available = false ──► UI shows manual buttons.
  │     │                        Operator clicks the winning team.
  │     │                        POST /answers with answerSource = "Operator".
  │     │                        ✔ The match runs exactly the same.
  │     │
  │     └─ available = true
  │           ├─ POST /buzzer/matches/{id}/sessions   → devices armed, timer starts
  │           ├─ Agent polls the RS485 bus and pushes presses:
  │           │     POST /buzzer/sessions/{sid}/presses
  │           ├─ Server maps DeviceId → TeamId via BuzzDeviceMapping,
  │           │     ranks by ElapsedMs, persists every press
  │           ├─ SignalR broadcasts the ranked list to all screens
  │           ├─ Operator confirms (and may always override)
  │           └─ POST /answers with answerSource = "Buzzer", buzzPressId = ...
  │
  └─ If the agent times out or errors:
        the session is marked Failed, a warning is shown,
        and the UI falls back to the manual buttons.
        ✔ Gameplay is never blocked.
```

### W5 — Stage qualification

```
1. All matches in the stage reach Completed
2. GET /qualification/stages/{id}/preview
     ├─ winners  = top N ACTIVE participants per match, by TeamMatchScore
     ├─ others   = every remaining active team, ranked by TeamStageScore
     ├─ wildcards= the top M of "others"
     └─ detect ties ON OR ACROSS the qualification boundary
          (a tie wholly below the cut is recorded but does not block)

3. FOR EACH tie that affects a qualifying place:

     PHASE 1 — criteria (instant, no stage time)
       for each criterion in TieBreakRule.CriteriaJson, in order:
           TotalScore → FewerIncorrect → MoreCorrectAtHighDifficulty
                     → FasterAverageBuzzTime → HeadToHead
           if it separates the teams:
               record ResolvedByCriterion, state = Resolved
               break
       ↓ still tied

     PHASE 2 — play  (only if PlayTieBreakSegment = 1)
       POST /qualification/ties/{tieId}/tie-break-match
         ├─ create Match with MatchKind = TieBreak, only the tied teams
         ├─ build segments from the rule (MCQ by default)
         ├─ reserve questions via the normal selector
         │     (repeat policy honoured — no question they have already seen)
         └─ state = Ready

       Operator plays it on the NORMAL console:
         /matches/{matchId}/live/*   ← same engine, same endpoints

       On match completion the engine:
         ├─ ranks the tied teams by their tie-break score
         ├─ writes TieBreakParticipant rows (points + ResultRank)
         ├─ if SuddenDeath, closes as soon as one team leads
         ├─ if still level and RoundsPlayed < MaxExtraRounds → another round
         ├─ if still level at the limit → apply OnStillTied
         │     ManualDecision → POST /ties/{tieId}/resolve-manually
         │     CoinToss       → recorded with the same audit trail
         │     ShareTheSlot   → both qualify, if the rules allow
         └─ TieBreakEvent.state = Resolved

       Tie-break points do NOT touch TeamStageScore
       unless ScoreCountsTowardStage = 1.

4. POST /qualification/stages/{id}/commit
     ├─ blocked while any qualification-affecting tie is unresolved
     ├─ insert StageQualification rows (IsCommitted = 1, with the reason
     │     and TieBreakEventId where a tie decided the place)
     ├─ create the next stage's Match rows per the seeding mode
     ├─ create MatchParticipant rows with seats and turn order
     └─ set the next stage's state to Ready

5. Rollback is possible until that stage starts.
```

### W6 — Crash recovery

```
API restarts mid-match
  │
  ├─ Client calls GET /matches/{id}/live/state
  ├─ Server reads from the database, not from memory:
  │     ├─ Match.StateId          = InProgress
  │     ├─ Match.CurrentSegmentId → the open MatchSegment
  │     ├─ MatchQuestion where State = Active → the current question
  │     ├─ MatchQuestion where State = Reserved → what comes next, unchanged
  │     ├─ TeamMatchScore → current scores, already aggregated
  │     └─ MatchParticipant where Status = Active → current turn order
  └─ The show resumes at exactly the same point.

  Because questions were RESERVED at start (not chosen on demand), the
  question order after a restart is identical.
```

---

## 5.7 SignalR hubs

### `/hubs/match` — operators, scorers, judges

| Server → client | Payload |
|---|---|
| `MatchStateChanged` | Full live state |
| `QuestionServed` | Question + active participant + timer start |
| `AnswerRecorded` | Answer + points + updated scores |
| `AnswerReversed` | Which answer, updated scores |
| `ScoresUpdated` | Score list |
| `ParticipantRemoved` | Participant + new turn order |
| `SegmentChanged` | Segment opened or closed |
| `SegmentOrderChanged` | New running order for the pending segments |
| `TieBreakStarted` | A tie-break match has begun, and for which place |
| `TieBreakResolved` | Who won the tie and by what method |
| `BuzzResultsReady` | Ranked press list |
| `TimerTick` | Server-authoritative remaining seconds |
| `MatchCompleted` | Final standings |

| Client → server | Purpose |
|---|---|
| `JoinMatch(matchId)` | Subscribe to a match group |
| `LeaveMatch(matchId)` | |
| `RequestFullState()` | Re-sync after a reconnect |

### `/hubs/display` — projector screens (read-only token)

Same events, but **`correctOptionId` and `answerText` are stripped until the
question is revealed.** A display client cannot invoke anything that writes.

---

## 5.8 Validation and error handling

### Validation layers

| Layer | What it checks | Example |
|---|---|---|
| Model binding | Types, required fields | `difficultyLevelId` must be a number |
| FluentValidation | Field rules, cross-field rules | 2–8 options; exactly one correct |
| Application | Business rules needing the database | Team already in another match this stage |
| Domain | Invariants that must never break | A match cannot start with fewer than 2 active teams |
| Database | Last line of defence | Unique and check constraints |

### Question request models — one per format

Because each format has its own table with its own fields, each format also gets
its own request model. This means the API rejects a malformed question at the
contract level, and the generated Angular client is strongly typed per format.

All of them inherit the shared base:

```csharp
public abstract record CreateQuestionRequestBase
{
    public required string FormatCode          { get; init; }
    public string?         QuestionText        { get; init; }
    public string?         Explanation         { get; init; }
    public required byte   DifficultyLevelId   { get; init; }  // 1..5
    public Guid?           TopicId             { get; init; }
    public string          Language            { get; init; } = "ur";
    public int?            TimeLimitSeconds    { get; init; }
    public string?         Source              { get; init; }
    public List<Guid>      TagIds              { get; init; } = [];
}

// Six option-based formats share an options list
public sealed record CreateMcqQuestionRequest : CreateQuestionRequestBase
{
    public required List<OptionDto> Options       { get; init; }
    public bool AllowMultipleCorrect              { get; init; }
    public bool ShuffleOptions                    { get; init; } = true;
    public bool NegativeMarkingEnabled            { get; init; }
}

public sealed record CreateBuzzerQuestionRequest : CreateQuestionRequestBase
{
    public required List<OptionDto> Options       { get; init; }
    public int  BuzzWindowSeconds                 { get; init; } = 30;
    public bool LockoutOnWrongAnswer              { get; init; } = true;
    public bool AllowStealAfterWrong              { get; init; } = true;
    public int? StealWindowSeconds                { get; init; }
}

public sealed record CreatePassingQuestionRequest : CreateQuestionRequestBase
{
    public required List<OptionDto> Options       { get; init; }
    public int  MaxPassCount                      { get; init; } = 2;
    public PassDirection PassDirection            { get; init; } = PassDirection.Clockwise;
    public bool RevealAnswerIfAllPass             { get; init; } = true;
}

// Sequence has items, not options — a different shape entirely
public sealed record CreateSequenceQuestionRequest : CreateQuestionRequestBase
{
    public required List<SequenceItemDto> Items   { get; init; }
    public bool PartialCreditEnabled              { get; init; }
    public int? PointsPerCorrectPosition          { get; init; }
    public SequenceItemKind ItemKind              { get; init; } = SequenceItemKind.Text;
}

// Audio-Visual has no options at all — media and an answer are REQUIRED
public sealed record CreateAudioVisualQuestionRequest : CreateQuestionRequestBase
{
    public required Guid   MediaAssetId           { get; init; }
    public required MediaKind MediaKind           { get; init; }
    public required string AnswerText             { get; init; }
    public List<string>    AcceptableAnswers      { get; init; } = [];
    public int?  PlaybackStartSeconds             { get; init; }
    public int?  PlaybackDurationSeconds          { get; init; }
    public bool  AutoPlay                         { get; init; }
    public bool  ReplayAllowed                    { get; init; } = true;
    public Guid? RevealMediaAssetId               { get; init; }
}

// Visual Rapid Fire is a SET of images, each with its own answer
public sealed record CreateVisualRapidFireQuestionRequest : CreateQuestionRequestBase
{
    public required List<VrfItemDto> Items        { get; init; }
    public int? RevealSecondsPerImage             { get; init; }
    public int? GridColumns                       { get; init; }
    public bool ScorePerImage                     { get; init; } = true;
}

public sealed record OptionDto(string Text, bool IsCorrect, int DisplayOrder, Guid? MediaAssetId);
public sealed record SequenceItemDto(string? Text, Guid? MediaAssetId, int CorrectPosition, int DisplayOrder);
public sealed record VrfItemDto(Guid MediaAssetId, string AnswerText, List<string> AcceptableAnswers, int DisplayOrder);
```

**Routing:** `POST /programs/{programId}/questions/{formatCode}` binds to the
matching request type, so `POST .../questions/audio-visual` cannot accept an
options array and `POST .../questions/mcq` cannot accept a media id. Compare
this with today's system, where any field can be sent to any endpoint.

### Validator examples

Each format gets a focused validator instead of one validator full of `When`
branches:

```csharp
public sealed class CreateMcqQuestionValidator
    : AbstractValidator<CreateMcqQuestionRequest>
{
    public CreateMcqQuestionValidator()
    {
        Include(new QuestionBaseValidator());              // shared rules, once

        RuleFor(x => x.QuestionText).NotEmpty().MaximumLength(4000);

        RuleFor(x => x.Options)
            .Must(o => o.Count is >= 2 and <= 8)
            .WithMessage("Between 2 and 8 options are required.");

        RuleFor(x => x.Options)
            .Must(o => o.Count(p => p.IsCorrect) == 1)
            .WithMessage("Exactly one option must be marked correct.")
            .Unless(x => x.AllowMultipleCorrect);

        RuleForEach(x => x.Options)
            .ChildRules(o => o.RuleFor(p => p.Text).NotEmpty().MaximumLength(1000));
    }
}

public sealed class CreateSequenceQuestionValidator
    : AbstractValidator<CreateSequenceQuestionRequest>
{
    public CreateSequenceQuestionValidator()
    {
        Include(new QuestionBaseValidator());

        RuleFor(x => x.Items).Must(i => i.Count >= 2)
            .WithMessage("A sequence needs at least 2 items.");

        RuleFor(x => x.Items)
            .Must(BeContiguousFromOne)
            .WithMessage("Correct positions must be 1..N with no gaps or duplicates.");

        RuleFor(x => x.PointsPerCorrectPosition)
            .NotNull().When(x => x.PartialCreditEnabled)
            .WithMessage("Partial credit requires a points value per position.");
    }

    private static bool BeContiguousFromOne(List<SequenceItemDto> items) =>
        items.Select(i => i.CorrectPosition).OrderBy(p => p)
             .SequenceEqual(Enumerable.Range(1, items.Count));
}

public sealed class CreateAudioVisualQuestionValidator
    : AbstractValidator<CreateAudioVisualQuestionRequest>
{
    public CreateAudioVisualQuestionValidator()
    {
        Include(new QuestionBaseValidator());

        // Required by the contract AND by a NOT NULL column — belt and braces
        RuleFor(x => x.MediaAssetId).NotEmpty();
        RuleFor(x => x.AnswerText).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.PlaybackDurationSeconds).GreaterThan(0)
            .When(x => x.PlaybackDurationSeconds.HasValue);
    }
}

// The shared rules live in exactly one place
public sealed class QuestionBaseValidator
    : AbstractValidator<CreateQuestionRequestBase>
{
    public QuestionBaseValidator()
    {
        RuleFor(x => x.DifficultyLevelId).InclusiveBetween((byte)1, (byte)5);
        RuleFor(x => x.Language).NotEmpty().MaximumLength(10);
        RuleFor(x => x.TimeLimitSeconds).GreaterThan(0)
            .When(x => x.TimeLimitSeconds.HasValue);
    }
}
```

**Reading a question back** uses a polymorphic response — the OpenAPI document
declares a `oneOf` with `formatCode` as the discriminator, so the Angular client
gets a proper discriminated union:

```json
{
  "id": "q1...",
  "formatCode": "AudioVisual",
  "difficultyLevel": 3,
  "topicName": "Poetry",
  "questionText": "Identify the poet from this recitation.",
  "audioVisual": {
    "mediaUrl": "/media/av/clip-014.mp3",
    "mediaKind": "Audio",
    "answerText": "مرزا غالب",
    "playbackStartSeconds": 12,
    "playbackDurationSeconds": 25,
    "replayAllowed": true
  }
}
```

### Global exception handling

One `IExceptionHandler` maps exception types to problem responses:

| Exception | Status | Error code |
|---|---|---|
| `ValidationException` | 400 | `VALIDATION_FAILED` |
| `NotFoundException` | 404 | `NOT_FOUND` |
| `ForbiddenException` | 403 | `FORBIDDEN` |
| `InvalidStateTransitionException` | 409 | `CONFLICT_STATE` |
| `DbUpdateConcurrencyException` | 409 | `CONCURRENCY_CONFLICT` |
| `QuestionPoolExhaustedException` | 409 | `QUESTION_POOL_EXHAUSTED` |
| `ScoringRuleNotFoundException` | 409 | `SCORING_RULE_MISSING` |
| `UnresolvedTieException` | 409 | `UNRESOLVED_TIE` |
| `SegmentNotReorderableException` | 409 | `SEGMENT_NOT_REORDERABLE` |
| `FormatInUseException` | 409 | `FORMAT_IN_USE` |
| `BuzzerUnavailableException` | 503 | `BUZZER_UNAVAILABLE` |
| anything else | 500 | `INTERNAL_ERROR` (details logged, not returned) |

### Live-match specific rules

1. **Never return 500 during a live match if it can be avoided.** Every expected
   failure has a specific code and a suggested action.
2. **Buzzer failures are always 503 and always non-blocking** — the client falls
   back to manual entry.
3. **Question pool exhaustion is caught at match start, not mid-show.**
4. **Every gameplay error names the recovery action** in
   `extensions.suggestion`.
5. **Concurrency conflicts return the current state** so the client can re-render
   instead of guessing.

---

## 5.9 Security implementation summary

```csharp
// Every gameplay endpoint carries an explicit policy — never left anonymous
[Authorize(Policy = Policies.CanOperateMatch)]
[HttpPost("answers")]
[ServiceFilter(typeof(IdempotencyFilter))]
public async Task<ActionResult<RecordAnswerResponse>> RecordAnswer(
    Guid matchId, [FromBody] RecordAnswerRequest request, CancellationToken ct)
```

| Policy | Roles |
|---|---|
| `CanManageProgram` | SuperAdmin, ProgramAdmin |
| `CanManageQuestions` | SuperAdmin, ProgramAdmin, QuestionAuthor |
| `CanOperateMatch` | SuperAdmin, ProgramAdmin, Operator |
| `CanRecordAnswer` | SuperAdmin, ProgramAdmin, Operator, Scorer |
| `CanAdjustScore` | SuperAdmin, ProgramAdmin |
| `CanDisqualify` | SuperAdmin, ProgramAdmin |
| `CanResolveTie` | SuperAdmin, ProgramAdmin |
| `CanViewLive` | all authenticated, including Display |
| `DisplayOnly` | Display token — read endpoints only |

Answer reversal (`POST /answers/{id}/reverse`) needs no separate policy — it is
covered by `CanOperateMatch`, whose membership (SuperAdmin, ProgramAdmin,
Operator) already matches that endpoint's allowed roles exactly.

There is no `Judge` role and no policy grants it anything: disqualification,
score adjustment, tie-break resolution and answer reversal are all
`ProgramAdmin` (or `SuperAdmin`) only.

Every request additionally passes a **program-scope filter**: the `{programId}`
in the route must match the `program_id` claim in the token, or the request is
rejected with 403 before it ever reaches the database.
