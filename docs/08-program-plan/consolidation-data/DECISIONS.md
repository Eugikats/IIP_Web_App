# Consolidation decisions

One entry per judgement call made while executing `IIP-CONSOLIDATION-MERGE-PLAN-2026-11-03.md`: what was decided, why, and the alternative. Claude reviews this file after each phase.

## Decisions already made by the owner (2026-11-03)

- D-001 Two editions in one repository: `main` is the SaaS edition, `managed-app` is the Azure Managed Application edition, cut from `main` after it is complete.
- D-002 Repository: `Otic-Group/IIP_Web_App`. `main` is created from the live lineage `4818bb1`. `OticGroupAz/agenticIip-v2` is not reused because its `main` carries the hidden-code injection.
- D-003 Direct pushes are allowed; pull requests only if a push is refused. No force-push, no deployment, no change to Azure resources.
- D-004 Defaults in plan sections 3.1 and 4 apply (module placement and source of truth), with the capability-matrix rule for the policy wording, sign-in, billing and Academy overlaps.
- D-005 The agent has no database access. The owner supplies `live_db_history.json` from the read-only diagnose endpoint (plan section 6).

## Decisions made during execution

### D-006 Owner security prerequisite confirmed

On 2026-11-03 the owner confirmed in this chat: "The listed checks and credential rotation are complete on this machine". This satisfies the plan's prerequisite to continue here. The agent did not independently certify the scans or rotation. Alternative: move execution to a clean environment.

### D-007 Independent clone with locally available Git objects

Execution uses `C:/work/iip-consolidation-verified`, outside all existing IIP checkouts. An ordinary remote clone failed with a connection reset, and a retry stalled and was cancelled. The successful remote clone used the two existing object stores as `--reference-if-able` sources with `--dissociate`. GitHub supplied the refs; `git fetch --all` passed. The new clone has no `.git/objects/info/alternates`, so it is independent. No existing checkout, branch or tag was changed. Alternative: repeatedly download the same objects over the failing connection.

### D-008 Bootstrap the guard before installing it on the baseline

Phase 0 asks to run the guard on the live baseline before copying it there, but the baseline has no guard. The reviewed plan-branch script was copied outside the checkout and run against the baseline first, then installed from the plan branch. Both plan and baseline checks passed. The baseline PostCSS Git blob is exactly 81 bytes. Alternative: install the script before checking, which reverses the listed step order.

### D-009 Preserve the plan and evidence beside phase reports

In addition to the explicitly listed plan, data and guard paths, Phase 0 copies the handover and branch/live audit from the plan branch. They are the instructions and evidence named by the handover. No application files are imported from the plan branch. Alternative: keep these two documents reachable only through the plan branch.

### D-010 Repair baseline build warnings without weakening checks

The first Release build of the live baseline succeeded with 17 warnings and 0 errors, which fails the plan's zero-warning gate. The XML package pin moves from 8.0.4 to Microsoft's patched 8.0.5. Sources: https://github.com/dotnet/runtime/security/advisories/GHSA-7hqp-7c5m-4vq6 and https://github.com/dotnet/runtime/security/advisories/GHSA-vrv5-2jg9-4mmr. Both identify 8.0.5 as patched. The other warnings are addressed with an explicit null check in the existing outcome path, an explicit `new` modifier on the existing authentication scheme constant, equivalent xUnit collection assertions and awaited tasks in seven document tests. No test, assertion or expected outcome is removed. These are current supported contracts; the null branch already returns when no reason exists. Alternative: suppress warnings or advance main with a red gate, both rejected.

The copied migration inventory's trailing blank line was removed to pass `git diff --cached --check`; no migration entry or migration source changed.

### D-011 Consolidation authorization overrides the older PR-only process

The owner's handover explicitly permits direct phase pushes and fast-forward main updates after gates pass. That task-specific authorization supersedes the older repository's PR-only process for this consolidation. All three CI jobs and the broader phase checks remain in force; nothing is deployed. A refused push triggers the plan's stop-and-report rule.

### D-012 Phase 0 validates the tests present in the live baseline

The live baseline has 735 Domain and 1,741 API tests. It does not contain `playwright.claims-core.config.ts`, the `test:claims-core` package script, or the Item 37 journey; those are scheduled for Phase C. Phase 0 runs the baseline's existing suites and records the absent future suites as not yet applicable, never as passed. The tests present at baseline are retained and their counts must not fall. Phase C and final gates must include the imported tests and journeys. Alternative: silently claim future suites ran, or port test-dependent features out of the prescribed order, both rejected.

### D-013 Push reviewable phase work without moving main

The phase branch and baseline tag may be pushed while local browser checks are still running, so the unchanged CI jobs can also validate the phase. The phase report remains explicitly IN PROGRESS. Main is not created or advanced until the applicable phase gate is green. This follows the handover's distinction between pushing a phase branch and advancing main.

### D-014 Correct workspace browser checks exposed by the full matrix

The Nile Life renewal test selected the insurer's organisation in the browser, then queried the default tenant and expected the bank-only events tab. It now verifies that the tab stays hidden for an insurer and reads policy-date event provenance from the API using Nile Life's own tenant and workspace. This preserves both the tenant-scope assertion and the insurer/bank UI boundary. A tablet sidebar test was also blocked by Next.js's development badge covering the profile menu; `devIndicators: false` removes that developer-only badge while leaving Next's error overlay enabled. Evidence: the focused desktop renewal suite passed 2/2; the focused tablet renewal/demo-customer scenarios passed 3/3; the tablet sidebar test passed 1/1 after disabling the badge. Alternative: expose a bank-only tab to insurers, query another tenant's events, or force the click through the overlay; all would misrepresent the intended product behavior or test environment.

### D-015 Keep main frozen while stacking remaining consolidation phases

The section 8 global gate is still red, so `main` stays at `80155dc`. To continue the owner's request without publishing a phase that has not passed its required gate, B2 will be based on the reviewable B1 phase branch, and B3 and C will stack in order above it. The combined branch will be eligible for one fast-forward to `main` only after the complete section 8 gate passes. This changes the phase-branch ancestry from the plan's per-phase `main` base but does not relax any test, migration, security, or release gate. The alternative is to stop all merge work until unrelated visual/deployed checks are repaired, which would prevent the requested remaining phases from progressing while `main` remains equally protected.

### D-016 Skip two v2 commits whose behavior is already present or unsafe to port as a unit

`76bf49b` is not cherry-picked. The live baseline already omits the demo banner and status bar, while that v2 commit also removes sign-up recovery and the MFA gate from `AppShell`; those protections remain in the baseline. `cfb3bb2` is not cherry-picked as a whole. The baseline already creates a WhatsApp prospect on first contact, captures profile and stated names, and includes tests for those behaviors (`FirstContact_WithAProfileName...`, `FirstContact_WithNoProfileName...`, and `ANameThePersonStates...`). Porting the old v2 commit wholesale would remove newer tenant-aware provider, identity verification, usage metering, opt-out, and message-recovery behavior from the same files. The larger B2 snapshot will be reconciled against these current contracts, and existing tests remain. Alternatives: take either upstream commit wholesale, which would regress current supported behavior, or skip the newly contributed social work without review.

### D-017 Make the approved Brief and Assistant destinations real

The B2 visual gate showed `/app` rendering the Assistant even though `DESIGN.md` makes the Brief the workspace landing and places the full Assistant at `/app/assistant`. `/app` now renders Brief, the rail labels it Brief, and the persistent assistant prompt carries questions to `/app/assistant`. The former browser assertions also named stale controls and greeting copy; they now assert the current accessible labels and verify the same workflow behavior. Connector list responses with a missing or malformed `connectors` field now show the existing safe error state instead of crashing Knowledge Manager. A blank claims-table header is now labelled Actions. Phase B2 visual evidence: 155 passed, 49 deployment-only skips, 0 failed across desktop, tablet and mobile. Alternative: preserve route mismatch and loosen the tests without implementing the approved navigation, rejected.

### D-018 Pair the SaaS API with its matching Functions source

The deployed Functions revision `1872a14` is not a compatible complete worker for API `4818bb1` in SaaS mode. The core agent-workflow message contract is stable, but the old worker lacks multi-tenant mailbox suppression, workspace iteration, legal-hold-aware retention, case follow-through, and current notification registrations/types. Release API and Functions from the same source revision; for API `4818bb1`, the Functions tree's last source-changing commit is `0d8af21`, and that tree is identical through `80155dc`. Evidence and exact checks are in `PHASE-B3-REPORT.md`. Alternative: continue pairing the old worker based only on the unchanged workflow envelope, which would leave tenant-scope and legal-hold behavior unsafe.

### D-019 Port claims-core item 1 without replacing newer live safety behavior

Item 1 adds deterministic local Mock providers, API test support and claim fixtures. The live `AdminEndpoints.cs` retains its current fraud-rule update and audit behavior, while the incoming provider-health changes are reapplied so developer-only mocks report `mock`, never `healthy`, and a missing Teams adapter remains `not_configured`. The live `LocalMockDocumentStorageProvider.cs` is retained because its path validation is stricter than the incoming prefix-only check. The health test asserts `mock`, never `healthy`, and retains the existing safe-response assertions. The incoming email-health scope and provider-health portal mapping are retained with the new tests. The four `docs/build-loop` files are deferred to the final consolidated docs commit as instructed; the committed `src/.../test-results/...` image was excluded as generated test output. Alternatives: take the incoming conflict sides wholesale, which would weaken local storage path validation and drop current fraud-rule updates, or import generated test output into source control.

### D-020 Port claims-core item 2 as a document-processing reconciliation

Item 2's shared `DocumentProcessingRunner` is used for staff upload/retry and claimant upload/retry so failures leave a durable, retryable document and one processing path. The live document contract is retained alongside the incoming behavior: `ProcessingCapability` remains available for `Not configured` responses, `ClassifierDisagrees` is added as a separate staff signal, and checklist field-completeness details remain while `CountsTowardsCompleteness` determines whether a stored non-failed file satisfies its slot. The live API role map is the base; item 2's least-privilege CustomerService grant is applied, while the separate unused `UserRole` enum is removed after repository search showed no code consumer and the tests confirm `PlatformRole` is the sole role type. Build-loop notes are deferred to the final docs commit and generated `test-results` files are excluded. Alternatives: take the old branch files wholesale, which would drop live processing-capability and field-detail response fields, or keep the old manual processing path and leave staff/claimant retry behavior inconsistent.

### D-021 Keep tenant onboarding out of the insurer Admin role

The item 2 known-defect test expected `PlatformRole.Admin` and `PublisherSupport` to hold `ManageTenantOnboarding`. The live code and approved tenant boundary describe this as the publisher's cross-tenant onboarding queue, reserved for `PublisherSupport`; tenant Admin explicitly excludes it. The test now asserts only `PublisherSupport`, while retaining the same authorization coverage and test count. Alternatives: grant every insurer Admin cross-tenant onboarding access to satisfy the incoming test, which conflicts with the platform/tenant boundary and the existing live behavior.

### D-022 Render claimant completeness from the server's received counts

The item 2 browser journey exposed a misleading claimant progress count: a failed file remained visible for retry but the portal counted it as submitted. The claim response now exposes `RequiredDocumentsReceived` and `RequiredDocumentsTotal` from the backend checklist, and each slot exposes `Received` separately from `Uploaded`. The claimant page uses those server-owned values for the progress count and missing list, while retaining the failed file, its status and retry control. This keeps the file available for recovery without saying it satisfied the requirement. Alternatives: count every stored file as received, or recompute the count from portal rows and risk diverging from the backend rule.

### D-023 Merge product claim types without removing policy wording facts

Item 3 adds claim-type configuration, matching, a human-editable product surface and typed claim intake. The live product aggregate already carries human-confirmed policy-wording facts with source citations, so the merge retains both `WordingFacts` and `ClaimTypes` through the domain model, API DTOs, portal types and product mapping. The assistant also keeps the live customer-requested follow-up tool while gaining its product claim-type lookup. The four retired `docs/build-loop` snapshots and generated API `test-results` fixtures are excluded. `next-env.d.ts` is restored because its only incoming change was generated route typing. Alternatives: choosing either product snapshot wholesale would drop one capability, or committing local upload artifacts as feature data.

### D-024 Keep one claim document rule set across API and background processing

Item 4 supplies product-version and claim-type document slots to both the claimant portal and background follow-through. The live API also returns extracted-field completeness details, and item 2 distinguishes stored uploads from documents that satisfy completeness. The shared Application provider now preserves those field checks and item 4's resolved slots; API compatibility naming delegates to that provider. The portal reports `Uploaded` and `Received` separately, with backend slot limits and accepted formats. Wording-fact validation and claim-type/document-matrix validation are both retained when a product is edited. Alternatives: using the older static checklist would omit per-product slots in the API, while using the incoming provider unchanged would lose field-level extraction details.

### D-025 Add typed claim details while retaining live summary and evidence workflows

Item 5 adds product-defined claim fields to onboarding, claimant intake, the FNOL form, the claim API and staff review. The product aggregate and API continue to carry source-cited `WordingFacts` and the existing `ClaimTypes`/document matrix. The live claims summary, reported facts, export, and file-connection state remain on Claim Review; the new `ClaimDetailsCard` is a separate section. Generated storage fixtures and `docs/build-loop` snapshots are excluded, and generated `next-env.d.ts` changes are not retained. Alternatives: taking the incoming Claim Review wholesale would remove live file-connection/evidence and reported-facts UI; taking live product code wholesale would omit the new typed fields.

### D-026 Reconcile absent document processing and retain the Ugandan document name

The five-item gate exposed competing expectations for a missing processor. The supported item 2 behavior is explicit: persist the file, mark it failed/retryable with a safe warning, return HTTP 503 and `ProcessingCapability: DocumentProcessing`, then allow staff to retry when processing is available. The older direct endpoint test is updated to assert that behavior rather than leave a claimant upload appearing to be “Being checked” indefinitely. The built-in term remains “Police abstract,” used in Uganda; the incoming generic “Police report” test expectation is updated. Both contracts and the complete backend gate are rerun before this repair is committed.

### D-027 Port workspace operating settings and defer the database migration

Item 6 adds tenant-scoped settings for the IRA publish rule, reminder timing, SLA warning lead, seat cap and claims allowance. `ProductCommandService` keeps both the source-validating policy wording repository and the new tenant settings repository; the Admin surface keeps the existing integration, template, notification and policy cards alongside Operating settings. The new settings entity, EF repository, DbSet and API remain in scope, but this item's migration and the shared model snapshot are deferred to section 6 because the baseline has 42 migrations and this branch's migration history must be reconciled before adding any schema change. Generated test files and `docs/build-loop` snapshots are excluded. Alternatives: copying the claims-core migration now could fork or rewrite live schema history, while omitting the repository/API would make the UI unsaved.

### D-028 Keep onboarding wording setup and product wording review as distinct workflows

The live `ProductWordingExtractionService` is used by the existing onboarding screen to prepare an initial product: it reads only the selected workspace wording chunks, requires exact source quotes, saves an audited proposal, and lets a person select/edit facts before creating a draft product. Claims-core item 7 reads wording for an already-created product, validates quoted clauses, supports per-item accept/edit/reject, and applies accepted facts through `ProductCommandService` as a new draft version that still requires the existing second-person publish approval. Neither is a strict superset of the other because they own different product lifecycle steps. Keep the live onboarding routes and screen; port item 7 as an additive attached-product review flow, retaining its citation checks and human gates. Knowledge ingestion stores heading text in chunk metadata, so item 7 supplies those labels as reading context while validating citations against chunk body text only. The source citation opens the selected document and its extracted chunks in the Data Hub. Its persistence migration remains deferred to section 6. Evidence: live `ProductWordingExtractionTests` and `wording-setup.spec.ts`; incoming `WordingDraftApiTests`, `WordingExtractionTests`, and the item 7 browser journey. Alternatives: replacing onboarding with the item 7 UI would require a product to exist before setup, while dropping item 7 would leave no per-item decisions or apply-to-existing-product workflow.

### D-029 Preview customer flows from the saved product while retaining live claimant feedback

Item 8 exposes the product's current draft or published claim type, customer-visible questions and document slots through a tenant-scoped, ManageProducts-only read endpoint. The preview reuses the same claimant form and document card as the public claim pages, uses sample values, and cannot create a claim or upload documents. During the ClaimAccessStatus conflict, keep the live claimant status copy and preserve the existing required-document completion summary and alert semantics in the shared card; the incoming component extraction alone had dropped both. Exclude `docs/build-loop` snapshots and generated storage fixtures. Evidence: `CustomerPreviewApiTests` passed 10/10; the claims-core browser suite passed 25/25 including preview and 390px overflow; portal lint and production build passed. Alternatives: taking the incoming ClaimAccessStatus wholesale would discard live claimant status copy and document progress feedback, while keeping a separate document card would allow preview and claimant uploads to drift.

### D-030 Require and preserve versioned claimant privacy consent before uploads

Item 9 adds a workspace-owned privacy notice and version, a claimant agreement or withdrawal history stored through the existing JSON consent repository, an upload-time server check, and a staff-visible consent status. Keep all live outbound channel consent-purpose constants when adding the separate `claim_data_processing` purpose. The shared claimant document card retains item 8's preview behavior and item 7's live document progress and accessible error feedback, while disabling upload and retry controls until the current notice is agreed to. A notice text change advances its version and requires agreement again; withdrawal stops new portal uploads while keeping previously submitted evidence. No relational column migration is required because `TenantSettings` and `ConsentRecord` use JSON payload persistence. Evidence: targeted API suite 25/25, domain suite 5/5, privacy browser suite 5/5, portal lint passed with pre-existing image and Next.js warnings, production build passed. Alternatives: reusing marketing/channel consent would conflate distinct purposes, and client-only upload gating would be bypassable.

### D-031 Offer camera capture without changing consent or slot rules

Item 10 adds a camera capture control and a separate file chooser on every claimant document slot. Camera capture is offered only when the slot allows images; PDF-only slots remain chooser-only, multi-file slots allow repeated single-photo capture or batch selection, and existing limits remain enforced. Keep the privacy-consent lock, retry/remove behavior, required-document summary, live status and accessible upload errors from prior items in the shared card. Generated storage fixtures and build-loop snapshots are excluded. Evidence: browser suite for items 8, 9 and 10 passed 11/11, including phone-width preview, consent withdrawal, image capture, multiple images and PDF-only behavior; portal production build passed with the existing image warnings. Alternatives: replacing the chooser with camera capture would strand desktop and PDF users, while using a second customer-only component would create drift.

### D-032 Keep the live Academy implementation and omit the duplicate learner application

Item 33's capability was compared with the Academy already on this SaaS baseline. The live Academy already has learner identity and sign-in, course enrolment and paid access, progress, quizzes and assessments, human marking of final work, certificates, and the learner and admin surfaces under `/academy`. Item 33 adds a second learner store, content catalogue, payment path, certificate path and `/learn` application, so it is not a strict superset and would split Academy records and routes. Keep the live Academy and its tests. The existing operator marking flow covers the required human decision. One-time insurer-issued sponsor codes were not found in the live implementation; they remain an explicit follow-up to add through the existing Academy store if insurer-sponsored access is enabled. Alternatives: importing item 33 wholesale would create parallel learner identity and course state, while claiming sponsor codes exist would be inaccurate.

### D-033 Batch the final Phase C port and keep the report tied to verified results

Items 34-37 were combined into one reviewable local change to shorten the remaining turnaround after the prior item-by-item commits. The API/domain/UI slices remain separately testable and the item 34/36/37 browser journey has its own recorded result. The legacy `docs/build-loop/SUMMARY.md` was excluded because it directs users to the omitted `/learn` flow and reports source-branch capabilities as if they were present in this target. Alternatives: four more commits would extend turnaround without changing the resulting code, while copying the summary unchanged would leave misleading setup instructions.

### D-034 Keep Academy public routes narrow in the runtime access guard

The final authorization review found the live Academy's public course and certificate routes and exact learner session route were blocked by the Entra runtime header guard, despite their own public or learner-session controls and the existing regression test contract. Exempt only those paths, retain authentication for other Academy endpoints, and remove the obsolete `/api/v1/learn` exemption because the duplicate learner API was not merged. Alternatives: exempting all of `/api/v1/academy` would bypass staff authorization, while keeping the old `/api/v1/learn` exception would expose a dead route as public.

### D-035 Add an approved WhatsApp quick reply and close discovered UI/security gaps

The existing named-template send flow can include an optional `Learn more` quick-reply button when the 24-hour free-form window is closed. It sends a Meta quick-reply component with the configured template and maps the returned button reply into the existing inbound conversation path. The insurer must first have Meta approve a template containing the matching quick-reply button; this change does not submit or approve templates and does not implement spreadsheet campaign ingestion, QR acquisition, bulk approval, or automated campaign dispatch. The button is optional and is not sent unless selected. During validation, role selection could be overwritten by first-run browser hydration; persist a deliberate selection immediately and keep signed-in/server authority independent. The accessibility audit measured the My Work action label at 4.41:1 on its pale-blue panel; use the neutral text token. Pin patched crypto and packaging dependencies and stop ignoring filenames used by the repository security guard. These are integration closeout changes, not changes to the Phase C data model or migration history.

### D-036 Preserve current contracts in the final verification fixes

Register the knowledge ingestion sink used by wording upload and restore the guarded WhatsApp outbound adapter with its rate limiter. Use each brand's offered covers, recognize manager handoff requests, and avoid creating sales prospects for claims/service handoffs. Reconcile fixtures with first-contact prospect creation, explicit auto-reply mode, token-owned claimant scope and tenant-specific SMS secrets. Keep all tests. Visual selectors now use the current Command Center labels, compact navigation and responsive claim links. The unsupported-answer fixture exercises safe rendering, not a live model's groundedness. The claimant progress assertion checks the current required-document count. Add the visible human-authority reminder to the Assistant. Full backend tests passed 3,645; the saved final visual report records 159 passed, 51 skipped, zero failed and zero flaky.

### D-037 Remove executable injection remnants without rewriting history

The closeout working tree restores the ordinary PostCSS configuration. Final inspection also found an extensionless tracked `web/portal/init`, introduced in `03ddd368`, with an obfuscated payload hidden after whitespace. Retain the path as an inert retirement note, remove all package-command references, and include this extensionless path in the injection guard. Existing commits preserve the incident evidence; no branch history is rewritten. The guard had omitted extensionless scripts. No injected file is executed to inspect it. The alternative of shipping the remaining start/Storybook invocation was rejected. This source cleanup does not establish machine or credential integrity; the owner's earlier security confirmation is not new evidence about this file.

### D-038 Honor the owner's instruction not to repeat journeys

On 2026-11-06 the owner explicitly instructed not to rerun the journey and to commit and push now. Use the existing results and publish the closeout branch. The full claims journey's prior result remains 102 passed and one stale-copy failure; its assertion correction is committed but not rerun. The final visual matrix is confirmed green from its saved report. Do not represent the unrerun deployed-DOM failures or remaining external checks as passed. Phase D remains deferred by the owner. Main advancement is distinct from publishing the phase branch under D-013; the remaining Section 8 exception is not silently inferred from a request to stop repeating journeys.
