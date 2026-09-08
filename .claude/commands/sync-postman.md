---
description: Regenerate/update the Postman collection from the live swagger.json after implementing a new phase's endpoints
argument-hint: "[optional: which folder/area changed, e.g. 'Stages']"
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
---

# Sync the Postman collection to the live API

Update `Quizware/postman/Quizware.postman_collection.json` so it covers
every implemented endpoint, replacing the manual process used when the
collection was first built (`context/CURRENT.md`'s Postman collection entry —
80 requests across 10 folders, validated by actually running it with
`newman`, not just written).

Area that changed, if known (may be empty — sync everything if so):
**$ARGUMENTS**

---

## Step 1 — Confirm a live instance

```bash
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5299/swagger/v1/swagger.json
```

If nothing is listening, ask the user to start `dotnet run` first — do not
start it yourself unless explicitly asked (`context/LESSONS.md` V-009: stray
background `dotnet run` processes have caused file-lock problems in this repo
more than once).

## Step 2 — Diff against the live contract

```bash
curl -s http://localhost:5299/swagger/v1/swagger.json -o /tmp/live-swagger.json
```

Compare its paths against the existing collection's requests. New routes not
yet in the collection, or routes the collection has but swagger no longer
serves (renamed/removed), are what you're looking for.

## Step 3 — Update the collection

Add new requests to the correct existing folder (see
`Quizware/postman/README.md` for the folder scheme and run order — folder
order matters, e.g. Programs must run before Admin because Admin's
Assign-Roles request needs `{{programId}}`). Match the existing requests'
style: use collection variables (`{{baseUrl}}`, `{{programId}}`, etc.), not
hardcoded values. Still-implemented-as-501-stub endpoints stay excluded, per
the collection's documented scope.

## Step 4 — Validate

```bash
cd Quizware
npx --yes newman run postman/Quizware.postman_collection.json
```

Fix any failure that's a collection bug (bad variable, wrong order). If a
failure looks like a real backend bug, report it — don't silently work around
it in the collection.

## Step 5 — Report

State what was added/removed/changed, and the newman pass/fail summary.
