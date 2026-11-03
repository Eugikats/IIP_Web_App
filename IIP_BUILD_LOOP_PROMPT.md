# IIP build loop

You are building out the IIP (Insurance Intelligence Platform) in a closed loop. You work through every item below, in order, one at a time: define done, build it, prove it works, commit it, record it, and move straight to the next. You do not stop, pause, summarise early or ask for confirmation until every item is DONE or BLOCKED. There is no time limit and no scope limit. Build every item completely, not a minimal version.

The audit of the system is `IIP_AUDIT.md` in the repo root. Read all of it before you start. Item codes (A3, B7, C4...) refer to it.

## What IIP must do when you finish

1. A company signs itself up and configures its products: claim types, the documents each claim type needs (each marked as a document or evidence), and the fields to capture. AI reads its policy wordings and drafts this for a person to confirm.
2. A customer reports a claim on WhatsApp, by email or on the web, and gets a claim link. On a cheap Android phone they upload each document into its own slot, as a file, a photo, or a camera scan. Document photos become clean PDFs. Evidence photos stay as untouched originals.
3. Every file is recognised and renamed (`PoliceLetter_CLM-0042.pdf`, not `IMG_2031.jpg`), its data is extracted into the claim, it is checked for authenticity, and a team of specialist agents works the claim in the background the moment a document lands.
4. The insurer gets a decision-ready claim ahead of time: a claims pack (summary PDF with an advisory recommendation, renamed documents, evidence originals, zipped), emailed automatically. The customer is told every time the status changes.
5. Many insurers run on one deployment, each with their own WhatsApp number, mailbox and claimant portal, fully isolated.
6. Plans, usage metering and payments (card and mobile money) work, including a self-serve plan.
7. IIP Academy works for real learners, end to end, including payment.
8. Data protection is complete: consent, retention, deletion and erasure.
9. The portal uses the design direction in `DESIGN.md`.

The only thing not in this loop is the conversational onboarding agent and setup wizard (A7). It is built after this loop, on top of everything here.

## Setup (first run only)

If `docs/build-loop/BUILD_PLAN.md` does not exist:

1. Do NOT touch the current working tree or its branch. It has uncommitted work that must stay as it is. Work only in a separate git worktree:
   `git fetch origin && git worktree add ../iip-build -b build/claims-core origin/main`
   (if `../iip-build` already exists, use it; if `origin/main` does not exist, branch from the default branch and record it). Copy `IIP_AUDIT.md` and this prompt file into `../iip-build/`. From here on, run every command and edit every file inside `../iip-build` only.
2. Prove the toolchain works in the worktree: `dotnet build InsuranceIntelligence.sln -c Release`, `cd web/portal && pnpm install && pnpm lint`, and that Playwright can launch Chromium. Install anything missing.
3. Create `docs/build-loop/` containing:
   - `BUILD_PLAN.md`: every item below as a checklist with status `TODO`, `IN PROGRESS`, `DONE` or `BLOCKED`.
   - `PROGRESS.md`: one entry per finished item (what changed, files, tests added, how it was verified, commit hash).
   - `DECISIONS.md`: every judgement call you made on your own, the option you picked and why.
   - `HOW_TO_TEST.md`: plain steps a non-developer can follow to test each finished item.
4. Commit: `chore(build-loop): set up build plan`.

## The loop

Repeat until no item is `TODO` or `IN PROGRESS`:

1. Make sure you are in `../iip-build` on `build/claims-core`. Read `BUILD_PLAN.md`, `PROGRESS.md`, `DECISIONS.md`. Use the task list to track the current item.
2. If an item is `IN PROGRESS` from an interrupted run, check `git status`. If the work is half done and checks fail, `git reset --hard HEAD && git clean -fd` and redo the item.
3. Take the first `TODO` item. Mark it `IN PROGRESS`.
4. Write its acceptance criteria under the item in `BUILD_PLAN.md`, starting from the description below, before writing code. Make every criterion testable.
5. Read the relevant code first and reuse what exists. The audit names the files.
6. Build it with tests: unit tests for logic, API tests for endpoints, UI tests for screens, and extend the end-to-end spec.
7. Run every check in the definition of done. Fix and re-run until green. Make up to 5 serious attempts.
8. When green: update `PROGRESS.md` and `HOW_TO_TEST.md`, mark `DONE`, commit `feat(<item code>): <what>`, push `build/claims-core`.
9. If still failing after 5 attempts: `git reset --hard` to the last commit, mark `BLOCKED` with the exact error and what you tried, commit the plan, continue. Mark dependent items BLOCKED with the reason only if they truly cannot be built without it; otherwise build them.
10. Never stop to ask. Choose the safest reasonable option, record it in `DECISIONS.md`, keep going.

## Definition of done (every item)

- `dotnet build InsuranceIntelligence.sln -c Release`: 0 errors, no new warnings
- `dotnet test InsuranceIntelligence.sln -c Release`: everything passes (baseline 1764)
- `cd web/portal && pnpm lint` passes and every `pnpm check:*` script passes
- The existing Playwright suites still pass
- `web/portal/tests/claims-core.e2e.spec.ts` passes, including this item's steps
- New tests cover the new behaviour
- `HOW_TO_TEST.md` has steps for this item

## Rules

- Never commit to `main`, never merge, never force-push, never open a pull request. All work goes on `build/claims-core` in `../iip-build`. Never edit, stash, reset or clean the original working tree.
- When your context gets long, rely on the state files, not memory: everything you need to continue must be in `BUILD_PLAN.md`, `PROGRESS.md` and `DECISIONS.md`.
- Never use real Azure, Meta, Graph, SMS, payment or other live credentials. Never deploy. Never touch `rg-iip-dev` or any live resource. Everything external is built against the provider interfaces, with a real adapter driven by configuration and a Mock adapter for local runs and tests. Override the Development Service Bus setting with env vars as the audit describes.
- Never delete, skip or weaken a test to make it pass. If a test is wrong because behaviour changed on purpose, change it and say why in `DECISIONS.md`.
- Tenant isolation is absolute. New records follow the `PersistedScopedJson` pattern through the scoped repository base; blob keys are prefixed `{tenantId}/{workspaceId}/`. Every new data type gets a test proving another tenant cannot read or change it.
- Humans stay at the decision. Do not loosen `ApprovalDecisionGuard` or any human-review contract. AI output is advisory and labelled.
- Migrations are additive. Existing claims, products and documents keep working. The hard-coded Motor/Medical rules remain the fallback for claims with no configured product.
- Every new endpoint has a permission (`PermissionEndpointFilter`), an audit event, and lives in its own `*Endpoints.cs` file, not `Program.cs`.
- Check each new library's licence and size. Prefer MIT/Apache. Record every library and licence in `DECISIONS.md`, and flag revenue-based or commercial terms.
- Customer-facing pages work at 390px on a throttled connection. Lazy-load heavy code.
- Copy shown to customers is plain and friendly. Legal text you write is marked "to be confirmed by legal" in `DECISIONS.md`.

## The end-to-end oracle

Create `web/portal/tests/claims-core.e2e.spec.ts` in item 1 and extend it with every item. It starts the API (Mock providers, seed data) and the portal. When the loop ends it walks the entire journey across two tenants:

company signs up → configures a product from a policy wording (AI draft confirmed) with a claim type, a document slot, an evidence slot and fields → customer reports a claim on WhatsApp (mock) and receives a link → at 390px gives consent, fills the fields, scans a document, uploads an evidence photo → files are converted, classified, renamed, extracted and checked → agents produce a recommendation → completeness hits 100% → pack is emailed (mock) with the expected file names → officer changes status → customer is notified → a second tenant cannot see any of it → the company's usage and plan show correctly → a learner pays (mock), completes a course and verifies their certificate.

Commit small fixtures under `web/portal/tests/fixtures/claims-core/`: a police letter photo, a logbook PDF, a damage photo with EXIF, a sample policy wording.

## Items, in order

1. **Mock providers for local runs.** Add `Mock` for `INSURANCEINTELLIGENCE_AI_PROVIDER` and `INSURANCEINTELLIGENCE_DOCUMENT_PROVIDER` (and any other provider that lacks one), reusing `LocalTestDoubles`. Deterministic: documents classified from a marker in fixture content, plausible extracted fields with confidences, a fixed clearly-labelled AI summary, mock tool calls for Kato. Impossible to enable outside Development (respect `ALLOW_PRODUCTION_MOCKS` and runtime guards; test it). Provider health shows "Mock" or "Not configured", never "Operational" for an unconfigured provider. Add `scripts/run-local-mock.sh`. Create the e2e spec.

2. **Fix known defects.** Permission parity: fix the 17 mismatches between `web/portal/lib/roles.ts` and `RolePermissionMap.cs`; the server is the authority except where a non-admin role holds an admin-level permission (for example `CustomerService` with `ManageProducts` or `ManageTenantOnboarding`), which you remove from the server; record each decision. Settle `UserRole` vs `PlatformRole` into one. Classifier: whole-token filename matching (fix "accident_report.pdf" → `CustomerId`). Failed processing marks the document `ProcessingFailed` with a customer retry, never a permanent "Being checked". One completeness rule (B8): a required slot counts when the customer uploaded into it; classifier disagreement raises a staff flag. Fix the agent registry labels and count.

3. **Claim types per product (A3).** `ClaimType` (key, name, description, active, order) under the product version; admin UI to add, edit, reorder, deactivate; seeded types for every demo product; dropdown in staff and claimant claim creation and in Kato; `ClaimTypeClassifier` becomes a suggestion; migrate existing free-text values.

4. **Required documents per claim type (A4).** `ProductDocumentRequirement` gains `ClaimTypeKeys` (empty = all), `Kind` (`Document` or `Evidence`), `AllowsMultiplePages`, `MaxFiles`, `AcceptedFormats`. Resolution by product + claim type; admin UI per claim type; sensible defaults for each kind.

5. **Claim fields per claim type (A5).** `ClaimFieldDefinition` (key, label, type text/number/date/select/phone/currency/yes-no, required, options, help text, order, customer-visible or staff-only). Admin field builder. One dynamic form renderer for staff and claimant. Values in `ClaimCase.FieldValues`. Seed definitions mirroring `MotorFnolDetails` and `MedicalFnolDetails`; existing claims read through a fallback. Summary, pack and APIs use field values.

6. **Product settings and labels (A2, B2).** IRA publish gate as a per-tenant setting. Claimant page uses the tenant's `CustomerLabel` instead of the hard-coded `Label()`. SLA and reminder timings, seat limits and other hard-coded tenant values listed in the audit become tenant settings with today's values as defaults.

7. **Policy wording to draft configuration (A6).** On uploading a wording to a product, AI reads it and drafts covered items, exclusions, claim types, required documents and fields, each with a citation to the clause and page. A review screen lets the admin accept, edit or reject each item, writing accepted items into the product. Nothing applies without confirmation.

8. **Preview as customer (A8).** "Preview as customer" on a product shows the claimant pages for a chosen claim type with sample data, saving nothing.

9. **Privacy notice and consent (H2).** Before the first upload the claimant sees a short notice (what, why, who, how long, how to withdraw) and gives consent, stored as a `ConsentRecord` with the notice version. Notice text is a tenant setting with a default. Withdrawal is possible from the claim page.

10. **Photo or file per slot (B3).** Each slot offers "Take photo" (`capture="environment"`) and "Choose file"; multi-file where allowed.

11. **Evidence originals (B6).** Evidence uploads keep the original bytes and EXIF untouched; store a SHA-256; generate a separate server-side preview; HEIC kept with a JPEG preview. One client compressor, used only for document slots.

12. **Document photos to PDF (B5).** Document-slot images become a PDF server-side; multi-page slots ("Add another page") become one ordered PDF; originals kept alongside.

13. **Camera document scanner (B4).** "Scan document" on document slots: in-browser camera, edge detection, adjustable corners, perspective correction, contrast clean-up, then into the PDF flow. Lazy-loaded only when tapped. Automatic fallback to "Take photo" when the camera, library or device fails. Unit tests for the image maths on fixtures; e2e for scan and fallback.

14. **Recognise and rename every file (B7).** Vision-model classification against the tenant's full document catalogue including custom types, returning type, confidence and reason (mock locally). The customer's slot wins; a confident disagreement raises a staff flag. Unslotted uploads go into the matching slot when confident, otherwise to staff review. Display name `<DocumentLabelNoSpaces>_<ClaimRef>[_<n>].<ext>`; `OriginalFileName` kept; staff see both.

15. **Extracted data into the claim (C2).** Per-document-type extraction schemas (configurable per document type) using Document Intelligence and/or the model; proposed values for claim fields with source document, page and confidence; officers accept or correct; a person-confirmed value is never overwritten.

16. **Authenticity checks (E2).** For evidence and document images: EXIF presence and consistency (capture date vs incident date, device, GPS if present), signs of editing, and duplicate or near-duplicate detection across all claims in the tenant using perceptual hashes. Advisory flags with reasons, a human disposition, audit trail.

17. **Background specialist agents (E3).** Upload returns as soon as the file is safely stored. A durable `document-processing` pipeline (Service Bus consumer in Azure, durable local equivalent in Mock mode) runs in parallel per document: convert, classify and rename, extract, authenticity, completeness; then re-runs fraud signals, claims summary and recommendation for the claim. Replace the in-process queues that lose work with durable ones. Retries, dead-lettering and visibility in the UI.

18. **Fraud scoring and recommendation (E2).** A fraud score combining the rule engine signals with model-assisted signals, explained, advisory, behind the existing human disposition. A recommendation agent that drafts a recommendation (for example "ready to approve", "request X", "refer for investigation") with reasons and citations, clearly advisory.

19. **Claimant status wording and timeline (B9).** Every `CaseStatus` mapped to customer wording from tenant templates with defaults; timeline driven by the product's stages.

20. **Officer status change (C6).** One audited "Change status" action with a domain transition table, required reason and permission; every status reachable through a defined path; resolve and approval rules unchanged.

21. **Customer notifications on status change (C7).** Status events send the mapped message on the tenant's channels; admin-approved templates may auto-send, free text still needs approval; WhatsApp added to the same send path as Email and SMS, including Meta template messages outside the 24-hour window.

22. **Staff "Send claim link" (B1).** From the claim page, send the claimant a link to that claim by the tenant's channels; respects contact consent; audited.

23. **Slow connections (B10).** Upload progress, automatic retry with backoff, resumable (chunked) uploads, slot state kept across reloads, size targets for document images. Playwright run with network throttling at 390px.

24. **Claims pack (C4).** Zip containing `00_ClaimSummary_<ClaimRef>.pdf` (claim, customer, policy, product and claim type, field values, document checklist with status, timeline, AI summary, open questions, risk signals, fraud score, authenticity flags and recommendation, all AI parts labelled advisory), `01_Documents/` renamed PDFs, `02_Evidence/` originals, `manifest.json` (original name, new name, type, SHA-256, upload time). Download from the claim page; permission `ExportEvidencePack`; audited. Keep the JSON audit pack.

25. **Pack emailed automatically (C5).** Tenant settings for recipients and trigger (completeness 100%, a chosen status, or manual). Large packs go as a secure expiring download link. "Send pack now" for staff. Audited.

26. **Customer memory (D3).** Existing customers get known details prefilled and are offered documents already on file instead of being asked again, across channels.

27. **Multi-tenant WhatsApp (A9, D1).** Webhook routes by `PhoneNumberId` to the right tenant; per-tenant WhatsApp configuration at runtime; Kato branding and catalogue per tenant from the tenant's configured products instead of `MicroinsuranceCatalog`; WhatsApp media intake lands in the right claim slot.

28. **Multi-tenant email (A9, D2).** Timers iterate every tenant with a connected mailbox; per-tenant Graph ingestion; an IMAP option for insurers without Microsoft 365; attachments classified into slots.

29. **Multi-tenant claimant portal and channels.** Tenant resolved from the link or hostname, not build-time env; per-tenant base URL and CORS; per-tenant SMS webhook routing; per-tenant branding on claimant pages.

30. **Self-serve company sign-up (A1).** A public sign-up page: company and admin details, email verification, tenant and workspace created automatically, admin bootstrapped, sensible starter products. Support a non-Entra sign-in for companies without Microsoft 365 (email and password with verification, or an external identity provider) alongside Entra; staff invitations by email. No IIP operator needed.

31. **Token metering (E4).** Record prompt and completion tokens on every AI call into `AgentActionLog` per tenant; documents and storage metered too; cost model reads the configured deployment rather than fixed gpt-4o prices.

32. **Plans, limits and payments (G).** Per-tenant plan and subscription state (Free, Agent ~$20/month, Team, Enterprise) with claim and usage allowances enforced and clear upgrade prompts; prices in UGX and USD. Payment provider interface with card and mobile money adapters (MTN MoMo, Airtel Money, plus a card gateway), webhooks, reconciliation, receipts and failed-payment handling; Mock locally. Billing page shows plan, usage and invoices.

33. **Academy for real learners (F).** Public learner area outside the staff app with its own sign-in by email link; learner accounts; enrolment by payment (through item 32's providers) or by admin-issued access code; server-side progress; certificates with a unique code and public verification page; optional video per module; exercise marking (auto-marked where answers are objective, staff-marked queue otherwise).

34. **Retention, deletion and erasure (H1).** Retention classes for cases, documents, blobs, communications and conversation turns with tenant settings; deletion jobs that remove blobs; full data-subject erasure across cases, documents and blobs with evidence of deletion; legal hold. Confirm storage and SQL encryption in Bicep and add customer-managed key support as an option.

35. **Scale.** Replace full-scan `ListAsync` calls (magic-link requests, follow-through, billing) with indexed lookups by policy number, contact and status; add the needed lookup columns and indexes.

36. **Design direction (`DESIGN.md`).** Apply the design in `DESIGN.md` across the staff portal and claimant pages: accent, icon rail, border-led layout and six-state coverage, without breaking existing tests (update visual baselines on purpose and record it).

37. **Final pass.** Run the full e2e journey across two tenants, every check, and every Playwright suite. Fix anything broken.

## When every item is DONE or BLOCKED

Write `docs/build-loop/SUMMARY.md`: what is done, what is blocked and why, the decisions a human must review (most important first), the full manual test walkthrough, and a checklist of what can only be proven with real credentials in a deployed environment (Azure OpenAI quality, Document Intelligence, WhatsApp templates, email sending, payment providers). Commit and push the branch. Do not merge and do not open a pull request.
