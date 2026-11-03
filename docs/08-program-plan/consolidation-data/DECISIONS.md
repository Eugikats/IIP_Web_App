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
