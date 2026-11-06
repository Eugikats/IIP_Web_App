# Build loop summary

Branch `build/claims-core` (worktree `../iip-build`), based on `e8b2b42`. Written for the owner, who was not present during the build.

## What is done and what is blocked

**All 37 build items are done and committed**, one commit per item (`feat(item-N): ...`). Each was finished only after its gates passed (see "How it was verified" below).

| Items | What it gave you |
|---|---|
| 1-2 | Mock providers so everything runs locally with no credentials; known defects fixed |
| 3-8 | Claim types, required documents and claim questions per product, operating settings, setup from a policy wording, preview as the customer sees it |
| 9-16 | Privacy notice and consent, photo-or-file per slot, evidence originals with hashes, photos to PDF, camera scanner, automatic recognising and renaming, extracted data into the claim, authenticity checks |
| 17-18 | Background pipeline with retries, review score and recommendation (advice only, a person decides) |
| 19-25 | Claimant-facing status wording and timeline, officer status change, customer notifications, staff "send claim link", slow-connection upload, claims pack, pack emailed automatically |
| 26-29 | Customer memory, then multi-tenant WhatsApp, email and claimant portal/channels (each company has its own numbers, mailboxes, hostnames and wording) |
| 30-35 | Self-serve sign-up and password sign-in, token metering, plans with limits and payments, Academy for real learners, retention/legal hold/erasure, indexed lookups for scale |
| 36 | Design direction across the portal; phone-first rail; no-permission states; visual suite brought up to date |
| 37 | Final pass: every suite run, plus a two-company end-to-end journey |

**The original Item 38 push succeeded.** The 2026-11-02 verification and security cleanup commits still need a follow-up push after GitHub authentication is restored; see "Item 38" below.

**Built differently from the brief, on purpose** (all in DECISIONS.md): the background pipeline uses a durable database queue in every environment rather than a Service Bus consumer (D-024); the WhatsApp assistant queue was made durable in item 27; no automatic monthly charge exists because mobile money cannot be charged unattended (D-039).

## Decisions a human must review, most important first

1. **Payments (D-039).** The MTN MoMo, Airtel Money and card adapters are written to each operator's published API and tested only against stand-ins. They have never run against a real account. Prices and allowances (Free, Agent, Team, Enterprise) are placeholders for the business to set; they can be replaced with `INSURANCEINTELLIGENCE_PLAN_CATALOGUE` without a release. Existing workspaces are grandfathered on "Standard" with no new limits.
2. **Erasure and retention (D-041).** Nothing is deleted by default; a retention period is an explicit choice per record kind, and the periods and legal basis are for the insurer's legal team to confirm. Erasing a person removes what identifies them (name, phone, email, what they wrote, documents and stored files, chats, consents, portal access) but keeps the claim record itself for audit and reinsurance. A legal hold on a claim blocks both retention and erasure for it. The deletion record holds counts, who and why, never the person's details. Not built: erasure of a learner's Academy records, whole-workspace erasure on closing an account, and export of a person's data. Erasure does not reach copies outside the platform (a provider's message copy, a pack already sent to a reinsurer, backups, soft-deleted files for 7 days); run a restore drill and make sure a restore replays erasures.
3. **Self-serve sign-up and platform sign-in (D-037).** Anyone can open a workspace by email, but sign-up is on by default only in developer runtimes; a deployed environment must set `INSURANCEINTELLIGENCE_SELF_SERVE_SIGNUP=true`. Platform tokens are HS256, 8 hours, with no refresh and no revocation, signed with a deployment secret (`INSURANCEINTELLIGENCE_LOCAL_AUTH_SIGNING_KEY`). The sign-up rate limit counts by connection address, so behind a proxy the real address must be forwarded. Five wrong passwords pause an account for 15 minutes. The terms page is draft wording marked "to be confirmed by legal".
4. **Multi-tenant channels (D-034, D-035, D-036).** Each company brings its own WhatsApp number, mailbox and (optionally) hostname. Credentials are stored encrypted per company and never shown again. Custom hostnames need DNS and TLS you control; nothing here issues certificates. There is no database uniqueness rule on link names, hostnames or code redemption (a data migration for someone who can see the real data).
5. **AI is advisory everywhere.** The review score (item 18), authenticity checks (item 16) and recommendations never change a claim by themselves; every status change, approval and message to a customer is a person's action and is audited. The platform preamble that states this is not editable by a company.
6. **Metering (D-038).** Token use is recorded per call with a cost estimate from `INSURANCEINTELLIGENCE_AI_PRICING`; the estimate is only as good as the price you enter.
7. **Scale (D-042).** Everyday lookups are indexed. Dashboards, reports, global search and the brief still read a whole workspace; move them to queries or a search index before a workspace reaches tens of thousands of claims.
8. **The visual suite (D-043, closes D-008).** It failed at baseline for three reasons; two were real robustness gaps (fixed in the product) and one was expectations about screens redesigned earlier (updated on purpose, each change listed). Muted text was darkened slightly to meet AA contrast. No test was deleted or skipped.
9. **A phone starts with the narrow icon rail** and the rail opens on keyboard focus only (D-043). Both fix real phone problems found by the suite.

## How it was verified (final pass, item 37)

| Check | Result |
|---|---|
| `dotnet build InsuranceIntelligence.sln` | 0 warnings, 0 errors |
| `dotnet test` | Domain 1178 passed, Api 1738 passed, 0 failed |
| `pnpm lint` | clean |
| `pnpm test` (academy catalogue, runtime data, provider status, role routes, permission parity, UI release gates, trust-truth, authority-permissions, scan-math) | all pass |
| `pnpm test:runtime-recovery` | passes (one assertion needed the claim error state to carry its own title, fixed in the product) |
| `pnpm test:deployed-dom` | 3 passed, 3 skipped by the spec's own conditions |
| `pnpm test:claims-core` (full journey, includes the two-company journey and the item 36 responsive/permission block) | 110 passed |
| Visual suite, 3 projects (desktop 1440, tablet 1024, mobile 390) | 150 passed, 51 skipped by their own conditions (project or deployed-profile only), 0 failed |

The same figures are in PROGRESS.md under "Item 37".

### Verification refresh (2026-11-02)

Reran the full claims-core journey from a clean API and portal using `CI=1`, `IIP_E2E_API_PORT=5270`, and `IIP_E2E_PORTAL_PORT=3205`, which prevents Playwright from reusing an already-running local server. Result: **110 passed, 0 failed** in 15.4 minutes. The police-letter sample was classified as a police abstract, mock extraction returned the expected policy reference, and the review/correction flow passed. A first run that permitted server reuse produced 109 passes and one card-payment navigation failure; the isolated card test and the clean full rerun both passed. No application code changed during this verification refresh.

The deployed IIP sign-in was also checked with the requested Microsoft work account. Authentication completed, but the app displayed **Workspace access unavailable** because the account could not be resolved to an IIP workspace. No production claim or document was created; deployed workflow and real-provider extraction remain unverified until workspace access is provisioned.

### Remote branch integrity review (2026-11-02)

The organisation branch pointed to `a792fda`, which added a large obfuscated JavaScript payload to `web/portal/postcss.config.js` alongside `.gitignore` edits. The payload was not executed. The branch was rebased onto that remote tip, and commit `a6222f6` restores the plain PostCSS configuration. `node --check web/portal/postcss.config.js` and `pnpm lint` pass on the cleaned branch. The suspicious commit remains in branch history; a repository maintainer should establish why it was added and whether any related credentials or automation need review. The branch tip contains the clean configuration.

## Full manual walkthrough

Start everything on mock providers: `scripts/run-local-mock.sh` (prints "API is up"), then open http://localhost:3000. Nothing needs a credential.

1. **Sign up** at `/signup`: company, name, email, a password of 12+ characters, tick the terms. Read the confirmation link from the mock outbox (`/api/v1/dev/mock-outbox`, scope `iip-platform` / `identity`), open it, then sign in at `/signin`. You land in your workspace as administrator with Motor, Medical and Property as drafts.
2. **Set up a product** (Admin, Products): add claim types, required documents and claim questions per claim type. Or upload a policy wording and accept the proposed configuration item by item. Use "Preview as customer" to see what a claimant will see.
3. **Invite a colleague** (Admin, Users and roles). Open the link from the outbox in a private window and join. Try withdrawing an invitation.
4. **File a claim** (Claims, New claim) or send the customer a claim link ("Send claim link"). As the customer: read the privacy notice and agree, take a photo or choose a file per slot (on a phone-sized window the camera input is offered), watch each file become "Being checked" and then "Received". Files are recognised and renamed; extracted values are offered to the officer to accept.
5. **Review the claim**: review score and the reasons behind it (advice, you respond Agree or Disagree), authenticity checks, the timeline, the claim details. Change status with the status menu (the customer gets a notification in their own wording). Under More actions: draft a customer update, request supervisor review, export the claims pack, record the claim resolution.
6. **Claims pack**: download it, or switch on the automatic email in Admin, Claims pack email.
7. **WhatsApp and email** (Admin, WhatsApp / Email inbox / Connections): in mock mode, send a message through the mock inbound endpoints (see item 27 and 28 in HOW_TO_TEST.md); the assistant opens or updates claims, always telling the customer a person will decide.
8. **Plans and payment** (Admin, Billing): see usage meters and your plan; choose a bigger plan, pay with the pretend card page or the pretend mobile-money operator (`/pay/mock`). Cancel and resume. Fill the allowance (set a low claims limit) and see staff get a clear limit message while the customer is told a person will be in touch.
9. **Academy** at `/learn`: sign in by email link, take a course, submit written work, earn a certificate and verify it at `/learn/certificate/<number>`. As an administrator, Admin, Academy: issue one-time access codes and mark written work.
10. **Data protection** (Admin, Data protection): set retention periods, place and release a legal hold, preview then erase one person by phone/email/policy number, read the deletion record.
11. **Two companies**: sign up a second company in a private window. Confirm neither sees the other's claims, even for the same phone and policy number, and that erasing in one leaves the other untouched.
12. **Phone**: resize to 390px and walk through `/signin`, `/signup`, `/learn`, the claimant pages and Admin pages. There is no sideways scroll. The sidebar is the narrow rail and opens on one tap.

## What only real credentials can prove

Nothing below has been run against a real service. Each needs its first run with real access:

- **Azure OpenAI quality**: real prompts, grounding quality, refusal behaviour, token and cost accuracy against `INSURANCEINTELLIGENCE_AI_PRICING`, the length limits of model output on real documents.
- **Azure Document Intelligence**: real extraction accuracy on Ugandan police abstracts, IDs, repair estimates, handwriting and low-quality phone photos.
- **WhatsApp**: Meta approval of message templates, the 24-hour window, real webhook signature verification, media download, one number per company.
- **Email sending**: SMTP and Microsoft Graph sending with attachments (the claims pack), delivery/spam behaviour, size limits, and IMAP reading for the inbox feature against a real mailbox.
- **Payment providers**: MTN MoMo, Airtel Money and the card gateway adapters have never run against a real account. Needs sandbox then live credentials, per-market status shapes, callback reachability and the amount/currency match under real conditions.
- **Service Bus** (if you want it instead of the database queue): a namespace, the consumer, dead-letter handling.
- **Real IMAP, Meta signatures, DNS and TLS** for customers' own hostnames and mailboxes.
- **Customer-managed key first deployment** (`infra/mainTemplate.bicep`): Key Vault, key rotation, what happens if access to the key is lost.
- **Backups and soft delete**: confirm the blob soft-delete window (7 days), SQL point-in-time restore, and that an erased person does not come back from a restore (restore procedure must replay erasures).
- **Entra sign-in alongside platform sign-in** in a real tenant.

## Item 38: push to the organisation's repository

The original push of `build/claims-core` to the `org` remote succeeded on 2026-10-12. The follow-up push of the 2026-11-02 verification and security cleanup commits was refused because the stored GitHub credential is invalid. `gh` is not installed; SSH has no authorized key. Remote is at `f901ab2`, and local is three commits ahead. After restoring GitHub authentication, run `git push org build/claims-core` from `../iip-build`. This is a normal fast-forward push; no force or merge is required. `origin` remains unchanged.
