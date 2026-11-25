# Security review — Phase 15 (`P15-02`, `P15-03`)

**Date:** 2026-11-25 · **Scope:** `Quizware/` (the new API, the buzzer module
and the BuzzerAgent) · **Method:** OWASP Top 10 (2021) walk-through of the
code, a dependency scan, a secret scan of the whole git history, and an
automated tenant-isolation penetration test.

**Result:** no critical findings. Every finding below rated Medium or higher
is fixed on this branch, and each fix has a test or a CI check guarding it.
What is left is a set of recommendations for deployment.

## Findings and fixes

| # | Severity | Finding | Fix | Guarded by |
|---|---|---|---|---|
| S-1 | **High** | **Cross-program read.** `ProgramScopeMiddleware` only checked a route value named `programId`. `ProgramsController` names the program `{id}`, so a token for program A could read program B's details, dashboard, formats and settings, and run its go-live validation. | The middleware now enforces whatever route value an endpoint names with `[ProgramRoute]`. `ProgramsController` declares `{id}`. An unscoped SuperAdmin token is still let through, because program management across programs is that role's job. | `TenantIsolationTests` |
| S-2 | **High** | **Cross-program write.** The qualification, tie-break and selection rule upserts accepted another program's stage id in the request body and saved the rule into the caller's program. | `RuleStages.EnsureOwnedAsync` rejects (400) any stage id that is not the program's own, in all three upserts. | `TenantIsolationTests` (forged bodies) |
| S-3 | Medium | **Vulnerable dependency.** `Microsoft.AspNetCore.WebUtilities` up to 2.3.13, which comes in through the ASP.NET Core 2.3 Identity packages, carries GHSA-6527-9fp8-r4x6. A fresh restore of `master` failed the NuGet audit. | Pinned to 2.3.14; the 2.3 packages were bumped to 2.3.14. | NuGet audit: any advisory fails the `/warnaserror` build |
| S-4 | Medium | **No brute-force protection.** Sign-in never counted failures and was not rate-limited (OWASP A07). | Five wrong passwords lock an account for 15 minutes. A locked account gets the same response as an unknown one. Sign-in and refresh are limited to 10 per minute per client address (`Security:AuthRequestsPerMinute`), returning 429 `RATE_LIMITED`. | `SecurityHardeningTests` |
| S-5 | Medium | **Placeholder secrets could reach production.** `appsettings.json` ships `CHANGE_ME` values, and nothing stopped the API from running with them (A05/A02). | Outside Development and Testing, the API refuses to start if the signing key is a placeholder or shorter than 32 bytes, if the issuer, audience or connection string is missing or a placeholder, or if a buzzer agent key shorter than 16 characters is set. | `SecurityHardeningTests` |
| S-6 | Low | **No security headers or HSTS** (A05). | Every response carries `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Cross-Origin-Resource-Policy: same-origin` and a deny-all `Content-Security-Policy`. The CSP is left off the Development-only documentation pages. HSTS applies outside Development, and Kestrel's `Server` header is off. | `SecurityHardeningTests` |
| S-7 | Low | **A crash on bad input.** `PUT /questions/{format}/{id}` returned 500 for a body missing its option or item lists, and for malformed JSON. | Missing lists are treated as empty, so the validators answer 400. Malformed JSON is also a 400. | `TenantIsolationTests` (every route is probed with `{}`) |
| S-8 | Info | **Secret-scan false positives.** gitleaks flags four Postman collection *variable names* (for example `matchMcqQuestionId1`) as generic API keys. | Reviewed and listed by fingerprint in `.gitleaksignore`. | CI job `secret-scan` (gitleaks v8.24.3, full history) |

## OWASP Top 10 (2021) walk-through

| Risk | Status | Evidence |
|---|---|---|
| **A01 Broken access control** | ✅ after S-1/S-2 | Every route is covered by role policies (ADR-009). Tenancy is enforced three ways: the program comes from the token (ADR-002), the scope middleware refuses a mismatched route, and EF query filters apply. `TenantIsolationTests` reads every routed endpoint from the running app and forges another program's ids into each one: 217 requests over 157 routes. It also forges foreign ids into 11 request bodies. None succeeds, none crashes, none echoes the other program's data, and that program's data is unchanged afterwards. Hub `JoinMatch` and the display API refuse foreign matches (`LiveHubTests`, `DisplayEndpointTests`). |
| **A02 Cryptographic failures** | ✅ | Passwords use ASP.NET Core Identity's PBKDF2 hasher. Access tokens are HMAC-SHA256 JWTs that last 15 minutes, with the issuer, audience, lifetime and key all validated. Refresh tokens are stored only as hashes and rotated on every use. The agent key is compared in constant time. HTTPS redirection and HSTS are on. The signing key length is enforced at start-up (S-5). |
| **A03 Injection** | ✅ | All data access goes through EF Core with parameters; the code has no `FromSqlRaw`, `ExecuteSqlRaw` or `SqlQueryRaw`. CSV exports defuse formula injection. Excel exports write values, never formulas. PDF text is escaped. |
| **A04 Insecure design** | ✅ | A buzzer can never write scores (an architecture test enforces this). Answers take idempotency keys. Live events go through a transactional outbox. Every irreversible action needs ProgramAdmin, and every override is audited. |
| **A05 Security misconfiguration** | ✅ after S-5/S-6 | Swagger and Scalar are mapped only in Development. A 500 never returns exception details. Placeholder secrets stop start-up. Security headers are set. There is no CORS policy, so the API is same-origin only (see the recommendations). |
| **A06 Vulnerable components** | ✅ after S-3 | The NuGet audit treats every advisory, at any severity, as a build error under `/warnaserror`. `dotnet list package --vulnerable --include-transitive` is clean. |
| **A07 Identification and authentication failures** | ✅ after S-4 | Account lockout, sign-in rate limiting and generic failure messages. Display tokens are read-only and short-lived. The buzzer agent authenticates with its own key, never a user token (Q-005). |
| **A08 Software and data integrity failures** | ✅ | Uploads are checked by file signature (magic bytes), not by name (`MediaValidation`). No polymorphic deserialization of client types is used. A malformed body is a 400 (S-7). |
| **A09 Security logging and monitoring failures** | ✅ | An interceptor writes every insert, update and delete to `AuditLog`, with old and new values and the actor. Auditors can query it with filters (P13-05). Logs are structured (Serilog) and carry correlation ids. Request bodies, and so passwords, are not logged, and EF sensitive-data logging is off. |
| **A10 Server-side request forgery** | ✅ | The API makes no outbound HTTP calls. Only the BuzzerAgent calls out, and only to the API base address it is configured with. |

## Recommendations (deployment, not code defects)

- **`AllowedHosts`** is `*`. On the venue server, set it to the server's own
  host name(s).
- **CORS:** none is configured, which means same-origin only. When the Angular
  front end is served from a different origin, add an explicit list of allowed
  origins; never use a wildcard.
- **TLS:** use the venue's internal certificate (ADR-008), and keep HTTPS
  redirection on.
- **Secrets** (`Jwt:SigningKey`, `ConnectionStrings:Default`,
  `Buzzer:Agent:ApiKey`) belong in environment variables or user secrets on
  the venue server, never in `appsettings.json`.
- **The rate limit is per client address.** If the API ever sits behind a
  reverse proxy, configure forwarded headers, or every client will share one
  limit.

## How to repeat this review

```bash
cd Quizware
dotnet build Quizware.slnx -c Release /warnaserror        # NuGet audit (A06)
dotnet list Quizware.slnx package --vulnerable --include-transitive
dotnet test tests/Quizware.Api.IntegrationTests --filter "FullyQualifiedName~TenantIsolation|FullyQualifiedName~SecurityHardening"
cd .. && docker run --rm -v "$PWD:/repo" ghcr.io/gitleaks/gitleaks:v8.24.3 git /repo --redact --no-banner
```
