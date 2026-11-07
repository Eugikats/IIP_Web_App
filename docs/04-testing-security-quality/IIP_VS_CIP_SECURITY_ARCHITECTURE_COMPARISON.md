# IIP and CIP security architecture compared

Status: Review, not a contract. Last reviewed 2026-11-07.
Owner: platform engineering. Audience: the product owner and engineers deciding what to carry across between the two products.

## What this is, and what it is not

CIP (the credit intelligence platform, repository `CIP_Web_App`, read at commit `1a4be27`) and IIP (this repository, `main`) are sister SaaS products that share a lot of design. IIP's multi-factor sign-in was written to follow CIP's, and the sign-in page now follows CIP's look.

This is a static reading of both codebases plus two live header checks against the IIP dev deployment. It is not a penetration test, and it does not cover either product's infrastructure (network rules, Key Vault configuration, Azure SQL settings). Anything marked "not found" means a search of the code did not find it, not that it is proven absent. Where a document and the code disagree, the code was taken as the truth.

## In one paragraph

The two products have the same shape: Microsoft Entra sign-in, a server-side membership store that decides which organisation a person may act in, role and permission checks on the server, per-organisation records, MFA, rate limits, and AI that prepares work while a person decides. CIP is ahead in four places: a database-level tenant filter, a tamper-evident audit chain, browser security headers, and a built-in security audit command. IIP is ahead in three: a permission model finer than roles, per-organisation provider secrets encrypted with Data Protection and never returned to a browser, and a written human-decision boundary that is enforced in code. Both products rely on application code, not the database, to keep organisations apart.

## Side by side

| Area | CIP | IIP | Reading |
|---|---|---|---|
| Identity provider | Entra ID for work accounts, plus Entra External ID (CIAM) for Google and email | Same two, plus the platform's own password sign-in | Equivalent. IIP adds a third path |
| Token validation | One bearer scheme with a merged key set from both directories. Issuer must be a Microsoft or `ciamlogin.com` host with a GUID tenant. Audience list checked | Separate schemes per directory, routed by the token's issuer. Work-account issuer must be `login.microsoftonline.com/{guid}/v2.0`. Audience list checked | Equivalent. IIP's separate schemes are easier to reason about; CIP's merged set is simpler to configure |
| Who a request acts as | Membership row keyed by Entra object id and tenant id. Token claims are only a lookup key | Membership store keyed by object id. Token tenant and workspace claims are hints, never authority (ADR-0005) | Equivalent |
| Several memberships, no choice made | Picks the most recently used active membership, unless the request names one by header | Refuses (403) unless the request names one by header. The portal had no way to choose, which locked you out | CIP is friendlier; IIP is stricter. See "Membership choice" below |
| Header that names the organisation | `X-CIP-Organization-Id`, honoured only if the person has that membership | `X-Organisation-Id` and `X-Requested-Workspace-Id`, re-validated against real membership, fail closed | Equivalent |
| Roles and permissions | Role per membership row, many named roles and about 20 named policies, `RequireRole` | Roles plus a permission set loaded from the store per request, checked by endpoint filters | IIP is finer grained |
| Tenant isolation in data access | EF Core global query filter on the current tenant for about 197 filtered entities. A forgotten `Where` still cannot cross tenants | Every repository call carries an explicit tenant and workspace scope (about 290 uses in the infrastructure layer). Tests fail if a new table could mix organisations. No global filter | CIP has the stronger safety net. IIP relies on every query remembering the scope |
| Database-level isolation (row-level security) | Not found | Not present, and the product's own security statement says so | Equal gap. Neither has a second lock in the database |
| MFA | Platform MFA: TOTP, passkey (WebAuthn), email code, secret protector, enforcement middleware, per-user challenge rate limit | Ported from CIP's design: TOTP, passkey, email and SMS codes, enforcement middleware, `mfa` rate limit | Equivalent. IIP adds SMS |
| Rate limiting | Token bucket per user or host (configurable), plus named limits for the chat widget, invitations, registration and MFA | Named fixed-window limits for sign-up, join, sign-in, password flows, MFA, webhooks, marketplace, academy. No general per-user limit | CIP has a general limit; IIP limits the sensitive doors only. See finding 3 |
| Browser security headers (portal) | HSTS, nosniff, Referrer-Policy, Permissions-Policy, frame-ancestors, with a separate rule for its embeddable widget | None. The live portal sent no security headers when checked | Gap in IIP. See finding 1 |
| API security headers | Not found in the API itself; HTTPS redirection is on | nosniff, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, a closed CSP on every response. No HSTS, no HTTPS redirection in code (Azure terminates TLS) | IIP's API is ahead. Both lack API-level HSTS |
| CORS | One named policy for the web app origin plus localhost, with credentials | `AddCors` and `UseCors` are present; the policy was not reviewed | Not compared. Worth a look when headers are fixed |
| Secrets and connections | Key Vault references accepted for provider settings; a guard rejects plain values that are not Key Vault references. MFA secrets have a protector | Per-organisation connection secrets stored as one blob encrypted through ASP.NET Core Data Protection, validated against the real provider before save, write-only from the browser | IIP's model is the stronger one for a multi-tenant SaaS: one organisation's provider keys never sit in shared configuration |
| Audit trail | Append-only log where each event stores the previous event's hash and its own SHA-256, so editing or deleting history is detectable | Audit events are recorded and queryable. No hash chain, previous-hash or tamper-evidence field was found | CIP is ahead. See finding 4 |
| Human decision boundary | Documented: agents recommend, authorised humans decide; no silent write-back | Documented and tested: AI never approves, denies, settles, binds, or sends regulated messages without a person; prompts and internal notes are not exposed | Equivalent. IIP's is the more detailed |
| Security self-check | `security-audit` and `compliance-audit` commands that scan every controller for `[Authorize]`, every privileged write for a role, PII in outbound types, redacted views and cross-tenant reads, with a generated report | Tests for specific rules (endpoint parity, tenant mixing). No single command that produces an audit report | CIP is ahead. See finding 5 |
| Platform status gates | Suspended and deleted organisations are blocked; suspended and removed users are blocked; invited users limited to basic routes | Suspended, cancelled and closed organisations are blocked; subscription state drives access | Equivalent |
| Developer-only shortcuts | Test auth handler in tests | Browser-supplied identity headers exist for local development and are rejected in every deployed profile by a guard, with three independent blocks against in-memory storage outside developer machines | IIP's guards are more explicit |

## Membership choice: what went wrong on 2026-11-07

Your account belongs to more than one organisation or workspace (the real one and the demonstration worlds, for example). IIP's API looks at the signed-in person's memberships and, if there is more than one and the request does not say which, returns 403 rather than guess. That is the safe default, and the live log said exactly this: "No single active membership for user ... (tenant hint none, workspace hint none, organisation header none)".

The portal remembered a choice only in the browser's local storage, set when you switched demonstrations. A new browser or an incognito window has no remembered choice, so the first call to `/api/v1/me` was refused and the portal showed "Workspace access unavailable". There was no screen to choose from.

CIP handles the same situation on the server: with no header, it takes the most recently used active membership. That is convenient, and it is also safe, because the person really does belong to every candidate.

What was done: IIP keeps the stricter API and now asks the person. When `/api/v1/me` is refused, the portal fetches `/api/v1/me/organisations`. With one place it goes straight in; with several it shows "Choose an organisation" and remembers the pick. The API still re-checks the choice against real membership on every request. A test covers it (`tests/signin/sign-in.spec.ts`, "belongs to several organisations").

Option not taken: copy CIP and let the API pick the most recently used membership. It needs a last-used timestamp on IIP's membership rows (a migration) and it silently drops a person into an organisation they may not expect, which matters when one of them is a bank's demonstration world. It can be added later if the picker proves tiresome.

## Findings on IIP, in the order they are worth fixing

1. **The portal sends no browser security headers.** Checked live on 2026-11-07: `https://app.insuranceintelligenceplatform.com/login` returned no `Strict-Transport-Security`, `X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy` or clickjacking protection. `web/portal/next.config.ts` defines no `headers()`. CIP sets all of these. Suggested: add the same set, with `frame-ancestors 'none'` (IIP does not appear to embed its own pages), then run the sign-in suite, since a Content-Security-Policy that is too tight breaks the Microsoft redirect. Medium severity, low effort.
2. **The rate limiter is registered twice in the request pipeline.** `src/InsuranceIntelligence.Api/Program.cs` calls `app.UseRateLimiter()` at lines 961 and 963, either side of `UseAuthorization()`. Each request to a limited endpoint is probably counted twice, so a limit of 20 sign-ins a minute behaves like 10. This is a reading of the code, not a measured result; confirm with a test that sends N requests to a limited endpoint before changing it. Low severity, trivial fix once confirmed.
3. **No general per-user or per-address limit.** Only the named doors are limited. A signed-in client can call ordinary endpoints without a ceiling. The earlier performance work found the API pinned at 100% CPU on a one-core plan, so this is also a stability matter. CIP's token bucket is a workable pattern. Medium severity.
4. **The audit log is not tamper-evident.** CIP chains each event to the previous one with a SHA-256 hash. IIP's records are append-only by convention, and nothing would reveal an edited or deleted row. For insurers and the audit story, this is the most valuable thing to borrow. Needs a migration and a verification job. Medium to high value, medium effort.
5. **No single security audit report.** CIP can produce a pass, fail or skipped report for authorisation coverage, role escalation, PII in outbound types and cross-tenant reads. IIP has targeted tests but nothing a reviewer can run and attach to a security questionnaire. Medium value.
6. **No global tenant filter and no row-level security.** IIP's isolation depends on every query carrying its scope and on tests catching new tables. That has held, and the product's own statement is honest about it. A second lock in the database is the long-term answer for both products; neither has one.
7. **No HSTS from the API.** Azure terminates TLS, so this is lower risk, but neither the API code nor the live response sends `Strict-Transport-Security`. Pair with finding 1.

## Things worth knowing about CIP

- `apps/web/next.config.ts` in CIP at commit `1a4be27` contains unresolved merge markers (`<<<<<<< Updated upstream`, `>>>>>>> Stashed changes`) in the committed file, inside the security headers block. A build of that file should fail. Worth fixing there before the next CIP deploy; it was not changed from here.
- CIP's token issuer check accepts any GUID tenant on a Microsoft login host. So does IIP's. That is correct for a multi-tenant product, because the real control is the membership lookup that follows, and both products do that lookup.
- CIP's own security audit notes one skipped check (the content guard test needed a policy package in the database). Its report is generated, not continuous.

## What does not need changing

- The shape of sign-in, MFA and the membership store, which match.
- The decision to keep the API strict about ambiguous memberships.
- Encrypting per-organisation connection secrets with Data Protection rather than keeping them in configuration or Key Vault references.
- The human-decision boundary and the rule that no model output is shown as a final decision.

## Manual items this review cannot do

- Registering the Entra External ID application so "Continue with Google" and "Sign in with email" work. The portal needs `NEXT_PUBLIC_EXTERNAL_ID_AUTHORITY`, `NEXT_PUBLIC_EXTERNAL_ID_CLIENT_ID` and `NEXT_PUBLIC_EXTERNAL_ID_API_SCOPE`, and the API needs `INSURANCEINTELLIGENCE_EXTERNAL_ID_AUTHORITY` and `INSURANCEINTELLIGENCE_EXTERNAL_ID_CLIENT_ID` (and optionally `INSURANCEINTELLIGENCE_EXTERNAL_ID_AUDIENCE`) so it accepts those tokens alongside work-account tokens.
- Reviewing Azure network rules, SQL firewall, Key Vault access and diagnostic settings for both products.
