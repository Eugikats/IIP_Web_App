# IIP Academy build ledger

Updated at the end of every iteration. Newest facts win. Keep it short.

## Decisions made (owner asleep, recommended choices)

- Course 1 "AI & Agentic AI for Insurance" (id `ai-agentic-insurance`) takes its lessons, quizzes, assessments and final task
  from `academy-content/`. It replaces the older shorter content of the same course. The other existing courses keep their
  content, reshaped into lessons (idea, in IIP, worked example) with their quiz as the course check.
- Course 2 "Claims Communication & Service Recovery" is listed "Coming soon" with its map as the outline (no lessons yet).
- Course content lives on the server as JSON built by a script from the supplied markdown and the existing TS content. The
  browser gets lessons and questions without answers. The server grades and issues certificates.
- Academy data lives in one table `AcademyRecords` (kind, owner, payload) in the Academy platform scope.
- The Academy operator is an administrator of the platform's own workspace (config `Academy:OperatorTenantId`, default the
  deployment's tenant). Operators enrol people, mark final tasks, create groups and events, moderate, set the Discord link.
- Final applied task: learner submits text and the ten-box canvas; an operator marks it against the rubric (pass 60 of 100);
  the certificate follows a pass. Until marked it shows "Submitted, waiting for review".
- Lessons have no recordings yet (video production not started in the content register): the player area says so honestly and
  the lesson text carries the lesson.
- Superseded by the 2026-10-17 pricing/payment work below: prices are USD 85 by default. Without a payment key, enrolment
  remains open and free with an explicit notice. With a key, priced courses require server-verified payment.
- Public sign-in beyond Microsoft work/school accounts needs External ID (owner action). Noted on the sign-in page.

## Phase status

| Phase | State |
|---|---|
| 0 Content pipeline | done, committed |
| 1 Frontend foundation | done, committed |
| 2 Read screens on real content | done, committed |
| 3 Learner backend | done, committed (42 Academy backend tests pass) |
| 4 Learner screens live | built and browser-tested locally; live deploy is the single release below |
| 5 Community | built and browser-tested locally |
| 6 Zenith | built and browser-tested locally (no language model offline: explanations say so) |
| 7 Cut-over | done in code: sidebar link, old routes redirect, old screens and workspace price settings removed |
| Deploy and live verification | done 2026-10-16: API, migration AddAcademyRecords and portal live; visitor pages checked in a real browser at 1280, 768 and 390; signed-in API checked with a real Microsoft token; a Zenith explanation checked against the live model |

## Done log

- 2026-10-16: content pipeline (10 courses, 116 lessons, Course 1 exact from the supplied files).
- 2026-10-16: new Academy frontend from the design (dashboard, courses, course, lesson, quiz, final task, certificates and public
  verification, profile, progress, settings, notifications, community, Zenith, Academy team screen).
- 2026-10-16: server-side grading, attempts, certificates, community with consent and reports, Zenith grounded in lessons with
  daily and monthly limits.
- 2026-10-16: cut-over: sidebar and website link to /academy, /app/academy redirects, legacy sources moved to
  web/portal/scripts/academy-source (the content builder reads them).
- Browser tests (desktop 1280, tablet 768, phone 390): tests/academy/{journey,community,zenith,navigation}.spec.ts.

## Next

Nothing is queued. Owner to open https://app.insuranceintelligenceplatform.com/academy while signed in and walk through enrol,
a unit quiz, the profile and settings, the community and Zenith in the browser (the signed-in screens were verified locally in
a browser and live through the API, but not live in a browser because that needs a real Microsoft sign-in with two-factor).

Live notes: the deployed API rejected every anonymous call, so the public catalogue, certificate check and who-am-I are now
exempt in RuntimeHeaderGuard (tests pin that the rest of the Academy API still needs sign-in). The portal needed a manual
`az webapp restart` after deploy before it served the new build. The admin account (OticGroup@...) was enrolled in Course 1
once while testing Zenith.

## Questions for the owner

Answered 2026-10-17: Discord link is set in the admin screen (built). Hero photo is AI generated (no rights question). Payments:
Flutterwave, same approach as oticlms. Seven design-only courses: left out. Zenith: 10 a day, 1,000,000 tokens a month.
Price: USD 85 per course.

Open, see IIP_ACADEMY_PRICING_PAYMENTS_ASSESSMENT_PLAN.md: the other eight courses (short checks only, or full 10/15 banks);
card plus mobile money with a shilling rate; same or separate Flutterwave account; is 85 dollars tax inclusive; rotate the keys
found in oticlms/FLUTTERWAVE_INTEGRATION.md. Who the Academy operators are besides platform administrators.

## 2026-10-17 additions (local, not deployed)

USD 85 pricing, Flutterwave payments behind IPaymentGateway (stand-in for local tests), order records, reconcile worker, Payments
tab, Zenith lock during assessments and the final task, Zenith limits 10 a day and 1,000,000 tokens, editable settings.
Browser tests: tests/academy/payments.spec.ts (start the API with scripts/local/run-offline-payments.sh). Needs the owner: the
Flutterwave keys and a go for the deploy. See IIP_ACADEMY_PRICING_PAYMENTS_ASSESSMENT_PLAN.md.

## Not verified / known gaps

- Live Flutterwave checkout, account collection permissions and dashboard webhook settings need owner verification with test keys.
- This iteration is local only. No deployment, cloud settings, migrations, push or merge was performed.

## 2026-10-17 Flutterwave v3 adapter verification

Classification: provider request/response shapes are the current supported v3 contract; server verification, reference/currency/
amount matching, owner-only confirmation, operator authorization and single enrolment are invariants. v4 is a different provider
contract, not an implicit fallback. The one-cent underpayment allowance was legacy behavior that contradicted the expected-amount
rule; it is removed rather than preserved as a supported contract.

Official documentation checked on 2026-10-17:

- [Standard hosted checkout](https://developer.flutterwave.com/docs/flutterwave-standard-1): POST `/v3/payments` with `tx_ref`,
  amount, currency, redirect URL, required customer email and optional customer name/customizations. The existing body matches.
  Successful responses use HTTP 200 with outer `status: success` and `data.link`. Fixed checkout to require that envelope and handle invalid
  JSON/field types with a safe `gateway_error`. Only absolute HTTPS checkout URLs are accepted.
- [Payment methods](https://developer.flutterwave.com/docs/payment-methods): `card` and `mobilemoneyuganda` are the correct
  Standard options. Uganda mobile money uses UGX. The dashboard must allow per-payment options (disable the dashboard's
  "Enable Dashboard Payment Options" override); the owner must confirm the account supports the intended methods/currencies.
- [Verify by ID](https://developer.flutterwave.com/docs/transaction-verification) and
  [verify by reference](https://developer.flutterwave.com/reference/verify-transaction-with-tx_ref): both use outer `status`,
  `data.status`, numeric `data.id`/`data.amount`, `data.tx_ref`, and `data.currency`. Both routes are covered by fake HTTP tests.
  Non-200 responses, error envelopes, invalid fields and malformed JSON now produce no usable verification and no enrolment. Failed transaction
  status is retained only inside a valid success envelope. Removed the one-cent underpayment allowance: amount must be at
  least the order amount. Overpayment remains accepted, as in the existing contract and Flutterwave's verification guidance.
- [v3 webhooks](https://developer.flutterwave.com/docs/webhooks) still specify `verif-hash` containing the configured shared
  secret. Kept this v3 scheme with `CryptographicOperations.FixedTimeEquals`; unset/blank secrets reject all webhooks.
  [v4 webhooks](https://developer.flutterwave.com/v4.0/docs/webhooks) specify base64 HMAC-SHA256 over the raw body in
  `flutterwave-signature`. These are not interchangeable. No v4 header fallback was added to this v3 Standard integration.
  A future v4 migration must change authentication, webhook payload fields and verification together. v4's later generic
  examples conflict with its HMAC section; do not copy their direct shared-secret comparison for `flutterwave-signature`.
- Standard redirects use `status`, `tx_ref`, `transaction_id`; cancelled/failed checkout can omit transaction ID. The current
  callback reads these names and server verification falls back to reference when no numeric ID is available. Redirect status
  never authorizes enrolment.

No new migration, course content, price defaults, limits, provider registration, tenant scope or operator permissions changed.
No real credentials were read or used. Adapter tests use explicit nonfunctional test placeholders.

Files changed: `src/InsuranceIntelligence.Api/Academy/Payments/{PaymentGateway,AcademyPaymentService}.cs`,
`tests/InsuranceIntelligence.Api.Tests/{FlutterwaveGatewayTests,AcademyPaymentTests}.cs`,
`web/portal/playwright.academy.config.ts`, `web/portal/tests/academy/{journey,community,zenith,navigation,payments}.spec.ts`,
and this ledger. No legacy payment path or new provider fallback was added. Invalid provider responses fail closed;
the underpayment rule is stricter. No new control-plane fields, storage, tenant access, auth grants, AI actions or human
decision permissions were added. This addresses the Academy plan's Part B provider-verification and pre-deploy work,
not its live-provider/deployment gate. Product UI and the course content are unchanged; only browser coverage changed.

### Validation

- `dotnet test tests/InsuranceIntelligence.Api.Tests --filter FullyQualifiedName~FlutterwaveGatewayTests --nologo -v minimal`:
  initial run passed, 39 tests. Four additional HTTP 201/204 cases bring the final adapter suite to 43 cases, all included in
  the final full backend run. Includes request serialization, ID/reference verification, failed/not-found/non-success HTTP responses,
  outer status, malformed JSON and wrong field types, HTTPS links, missing secret, signature mismatches, and a structural check
  that the comparison calls the constant-time framework primitive (not a timing benchmark).
- `dotnet test InsuranceIntelligence.sln --nologo -v minimal`: final run passed, 1,547 API and 722 domain tests, none skipped
  (earlier run before the four additional HTTP cases: 1,543 API and 722 domain tests).
  Existing warnings include NU1903 for System.Security.Cryptography.Xml 8.0.4, CS0108 and AcademyZenith xUnit analyzer warnings.
- In `web/portal`, `pnpm.cmd lint`: passed with existing image-element warnings and the Next lint deprecation notice.
- `npx.cmd tsc --noEmit -p .`: initial run failed because starting Next regenerated `.next-dev` route files during the check;
  rerun after startup passed. No TypeScript source fix was required.
- Final `pnpm.cmd lint` and `npx.cmd tsc --noEmit -p .` reruns passed after the browser-test updates.
- Additional `pnpm.cmd build`: first attempt correctly refused missing runtime-profile/auth configuration. Retried as a local
  production build with the deployment script's public Entra/Demo build settings (command below): passed, 70 static pages
  generated, route compilation and build traces completed. Existing image-element warnings remain.
- Browser matrix: passed, 51 checks across 15 separate spec/project invocations; none skipped.
  Each invocation uses `npx.cmd playwright test --config playwright.academy.config.ts tests/academy/<spec>.spec.ts --project=<project>`.
  Projects: phone (390), desktop (1280), tablet (768). Specs: journey, community, zenith, navigation, payments.
  Start `bash scripts/local/run-offline.sh` for the first four and `bash scripts/local/run-offline-payments.sh` for payments.
  Restart the API before every spec/project pair to reset the one in-memory learner. The portal is started with the exact local
  environment in `playwright.academy.config.ts`. Previously skipped phone/tablet flows now run; phone navigation also runs
  because the current shell has a permanent rail rather than the old drawer assumed by its skip.
  An initial phone Zenith run failed because its floating button is deliberately hidden below 860px. The test now uses bottom
  navigation on phone/tablet and the floating panel on desktop. The phone rerun passed. No product UI changed.

| Spec | Phone 390 | Desktop 1280 | Tablet 768 |
|---|---|---|---|
| journey | 11 passed | 11 passed | 11 passed |
| community | 1 passed | 1 passed | 1 passed |
| zenith | 1 passed | 1 passed | 1 passed |
| navigation | 1 passed | 1 passed | 1 passed |
| payments | 3 passed | 3 passed | 3 passed |

Exact browser invocations, from `web/portal` (fresh API for each line):

```powershell
npx.cmd playwright test --config playwright.academy.config.ts tests/academy/journey.spec.ts --project=phone
npx.cmd playwright test --config playwright.academy.config.ts tests/academy/community.spec.ts --project=phone
npx.cmd playwright test --config playwright.academy.config.ts tests/academy/zenith.spec.ts --project=phone
npx.cmd playwright test --config playwright.academy.config.ts tests/academy/navigation.spec.ts --project=phone
npx.cmd playwright test --config playwright.academy.config.ts tests/academy/payments.spec.ts --project=phone
npx.cmd playwright test --config playwright.academy.config.ts tests/academy/journey.spec.ts --project=desktop
npx.cmd playwright test --config playwright.academy.config.ts tests/academy/community.spec.ts --project=desktop
npx.cmd playwright test --config playwright.academy.config.ts tests/academy/zenith.spec.ts --project=desktop
npx.cmd playwright test --config playwright.academy.config.ts tests/academy/navigation.spec.ts --project=desktop
npx.cmd playwright test --config playwright.academy.config.ts tests/academy/payments.spec.ts --project=desktop
npx.cmd playwright test --config playwright.academy.config.ts tests/academy/journey.spec.ts --project=tablet
npx.cmd playwright test --config playwright.academy.config.ts tests/academy/community.spec.ts --project=tablet
npx.cmd playwright test --config playwright.academy.config.ts tests/academy/zenith.spec.ts --project=tablet
npx.cmd playwright test --config playwright.academy.config.ts tests/academy/navigation.spec.ts --project=tablet
npx.cmd playwright test --config playwright.academy.config.ts tests/academy/payments.spec.ts --project=tablet
```

Local process handling: ports 3000/3100/5167 had no listeners at the initial check. Started the portal with the config's local
environment using `pnpm.cmd dev`. On Windows, used `C:\Program Files\Git\bin\bash.exe` for the API scripts. Stopped each API
process started by this verification before starting the next. A proposed bulk process script was rejected by automatic
approval review and did not run; individual owned-process sessions were used instead.
All local API/portal processes started for this verification were stopped afterward; ports 3000/3100/5167 were checked clear.
`git diff --check` passed. No product UI was changed, so the visual-change craft checklist is not applicable to this diff;
the existing UI flows were exercised in real Chromium browsers at all three widths and the failed mobile screenshot was inspected.

Additional local production-build invocation, from `web/portal` in PowerShell. These are public build parameters, not secrets;
the values match `scripts/local/deploy-dev-portal.sh` with the requested public domains. This does not deploy or modify Azure:

```powershell
$env:NEXT_PUBLIC_RUNTIME_PROFILE='Demo'
$env:NEXT_PUBLIC_AUTH_MODE='Entra'
$env:NEXT_PUBLIC_API_BASE_URL='https://api.insuranceintelligenceplatform.com'
$env:NEXT_PUBLIC_PRESENTATION_MODE='true'
$env:NEXT_PUBLIC_ENTRA_TENANT_ID='dcb0b132-fb9e-475c-a68e-b0671222e8d8'
$env:NEXT_PUBLIC_ENTRA_CLIENT_ID='3b3522e4-13fd-4bd9-8f08-a1cedf428d0d'
$env:NEXT_PUBLIC_ENTRA_PORTAL_CLIENT_ID='3b3522e4-13fd-4bd9-8f08-a1cedf428d0d'
$env:NEXT_PUBLIC_ENTRA_API_SCOPE='api://d55fe46b-1fba-4c5b-81e1-4ee1918ecf73/access_as_user'
$env:NEXT_PUBLIC_ENTRA_REDIRECT_URI='https://app.insuranceintelligenceplatform.com/app'
pnpm.cmd build
```

### Prepared deployment, NOT executed

This release adds no migration. The migration endpoint call below is a dev post-deploy pending-migration check and should apply
nothing. It is forbidden in protected runtime profiles; do not bypass that guard. These commands are instructions only and
must not be run until the owner explicitly says to deploy in plain words.

Pre-deploy checklist:

- [ ] Owner gives explicit deployment approval for this commit and confirms the dev subscription/resource group/apps below.
- [ ] Local backend, lint, TypeScript and all 15 spec/project combinations pass. Review the existing dependency vulnerability
  warnings separately before treating this as a production candidate.
- [ ] Owner rotates the previously reported exposed credentials in the other project. Do not open its integration file.
- [ ] Owner puts Flutterwave test credentials in `kv-iip-dev-zcxolwov`, under `Flutterwave--SecretKey` and
  `Flutterwave--SecretHash`. API settings `Flutterwave__SecretKey`/`Flutterwave__SecretHash` use Key Vault references, never
  plaintext. Do not print settings values, read vault secret values or enable shell tracing.
- [ ] Owner confirms USD cards and Uganda mobile money (UGX), the Academy exchange rate, and the dashboard payment-options
  override. Set `Academy__PortalBaseUrl` to `https://app.insuranceintelligenceplatform.com`. No fake provider in the deployment.
- [ ] Decide whether the shared account webhook can point to IIP without disrupting oticlms. If used, register
  `https://api.insuranceintelligenceplatform.com/api/v1/academy/payments/webhook` with the v3 hash; otherwise use confirm and
  reconciliation. Do not replace another app's webhook without owner agreement.
- [ ] Verify the existing `AcademyRecords` migration is applied. Investigate any unexpected pending migration rather than
  applying it as part of this release. Retain the previous known-good API and portal release for rollback.

From the repository root in Git Bash, after approval only:

```bash
bash scripts/local/deploy-dev-api.sh
```

Then in PowerShell, using the authorized admin's existing Azure CLI session. The access token remains in memory, is not
printed and is cleared after use. Do not use transcript/debug logging for this command:

```powershell
$ErrorActionPreference = 'Stop'
$migrationAccess = az account get-access-token --resource api://d55fe46b-1fba-4c5b-81e1-4ee1918ecf73 --query accessToken -o tsv
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($migrationAccess)) { throw 'Admin authentication failed' }
try {
    Invoke-RestMethod -Method Post -Uri 'https://api.insuranceintelligenceplatform.com/api/v1/admin/db-migrate' -Headers @{ Authorization = ('Bearer ' + $migrationAccess) }
} finally {
    $migrationAccess = $null
}
```

Then from the repository root in Git Bash:

```bash
IIP_PRESENTATION_MODE=true IIP_API_BASE_URL=https://api.insuranceintelligenceplatform.com IIP_ENTRA_REDIRECT_URI=https://app.insuranceintelligenceplatform.com/app bash scripts/local/deploy-dev-portal.sh
az webapp restart --subscription 5f5765ae-985f-42e4-a443-8e2447be33ca --resource-group rg-iip-dev --name app-portal-dev-h5girbnzrayqe --output none
curl --fail --silent --show-error https://api.insuranceintelligenceplatform.com/health/ready
```

Post-deploy owner checks: sign in with Microsoft/MFA; test USD card and UGX mobile money using Flutterwave test mode; verify
the stored reference/currency/amount, single enrolment after repeated confirm/webhook, and reconciliation after closing the
payment tab. Check the Payments tab and manual refund/revocation record, assessment lock in a second tab, Zenith limits and
phone/tablet layouts. With no key configured, verify the free-enrolment notice. Record results before any live-key switch.
If release checks fail, stop payment activation and redeploy the retained known-good API/portal pair with owner approval;
no schema rollback is needed for this release. Do not mark this staging-validated or production-ready from local checks.
