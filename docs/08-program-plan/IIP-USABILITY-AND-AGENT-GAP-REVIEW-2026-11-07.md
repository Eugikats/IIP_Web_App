# IIP sign-in, agent and channel usability review

**Review date:** 2026-11-07  
**Repository:** `Otic-Group/IIP_Web_App` working copy (`agenticIip-v2`)  
**Reviewed checkout:** `plan/consolidation-2026-11-03` at `d3500235fce2fcce56a2568ba51922f4b627b158`  
**Purpose:** Record evidence-backed causes of current sign-in friction and adjacent customer/agent workflow gaps, then define a complete, verifiable repair path.

## Executive finding

The live login reaches Microsoft successfully, returns to the IIP portal, and then fails while resolving the signed-in identity to an IIP workspace. The browser showed **Workspace access unavailable** after the redirect. Read-only Azure Monitor data now confirms 29 failed `/api/v1/me` operations in seven days with the API warning **No single active membership** for the same signed-in identity. The API also reports `authMode=Entra`, `environment=Demo`, and `status=healthy`.

The failure stage is proven: Microsoft authentication succeeds, then IIP returns 403 because it cannot resolve one effective active workspace membership. The exact data/configuration reason is **not yet proven**: Azure SQL access from this machine is blocked by its firewall, and the warning does not distinguish no active membership, multiple distinct workspace memberships, or an inactive organisation. Live configuration also confirms self-service organisation sign-up is closed, so an identity with no membership has no self-service recovery path. A duplicate-row fix exists in commit `4818bb11` on `org/main` and the live warning matches that newer diagnostic path, but the deployed commit SHA is unavailable. The portal turns different `/api/v1/me` failures into the same generic message, concealing actionable detail from users.

There are separate agent usability gaps: the web assistant accepts text only; WhatsApp image/document messages are stored or routed for claim handling but are not passed to the customer assistant as visual evidence; and the social-channel plan documents incomplete TikTok inbound and live provider validation. These gaps can make a customer feel unable to complete a task even after sign-in succeeds.

## Evidence and limitations

### Repository state

- The active checkout is `plan/consolidation-2026-11-03`, not `main`.
- The working tree contains unrelated edits and untracked workspace/acquisition work. They were not modified during this review.
- Remotes are `org` (`Otic-Group/IIP_Web_App`) and `origin` (`OticGroupAz/agenticIip-v2`). GitHub MCP access was rechecked on 2026-11-07 and still returns an organization SAML SSO 403; the read-only `org` Git remote remains available. No GitHub write, merge, push, Azure deployment, or production data change was performed.
- A fresh read-only `git ls-remote` confirms GitHub `org/main` at `49f0a1271b9d18938ee4c59d5aee33dfa19451ca`. Relative to it, the active branch has 19 commits not on `org/main`, while `org/main` has 192 commits not on the active branch; their merge base is `b21105179bbe8a76982af1deace0baa8e1ebd013`. This is a substantial integration/reconciliation task, not a simple fast-forward. Recheck before integration.
- The live `/health` response was rechecked on 2026-11-07 and reports healthy Demo/Entra. It does not include a build SHA, so it cannot prove which source commit is running.
- The live `/health/ready` response reports `environment=Demo`, `hostEnvironment=Production`, `storageProvider=SqlServer`, Azure Blob/OpenAI/Document Intelligence/AI Search/Service Bus, `seededBusinessData=true`, and `status=ready`. Email, generic communication, and notification providers report `Disabled`. This describes the Demo runtime and its seeded data; it is not production-customer data. The generic `communicationProvider` is not the WhatsApp Cloud API provider: WhatsApp has a separate settings-backed runtime resolver and health route. That route requires `ConfigureIntegrations`; the unauthenticated check returned 401, so WhatsApp's actual live connection state remains unverified.
- A live browser sign-in previously reproduced the post-Microsoft workspace-access failure. The displayed error does not reveal the underlying HTTP status or SQL membership state.
- Read-only Azure Monitor evidence collected on 2026-11-07 confirms the failure stage. In the IIP API's Application Insights component, there were 29 distinct `GET /api/v1/me` operations over the prior seven days that returned the membership-resolver warning `No single active membership for user ...`; the same authenticated object ID recurred, and the tenant, workspace and organisation hints were all absent. This proves Microsoft authentication reached the API and that IIP refused to create a workspace context. It rules out a purely browser/MSAL sign-in failure for these attempts.
- A read-only Entra directory lookup confirmed the recurring object ID maps to the email address the user supplied for IIP sign-in. The report intentionally omits both identifiers. The directory response did not expose `accountEnabled`, so account-enabled state was not used as evidence.
- The live warning text matches the newer membership diagnostic present on `org/main`. It is consistent with the duplicate-row fix being in the running API, but health telemetry still has no build SHA, so the exact deployed commit is not proven. Other `/api/v1/me` calls returned 200; a separate 503 was on the Google file-connection picker and is unrelated to sign-in.
- Read-only Azure App Service settings show `INSURANCEINTELLIGENCE_AUTH_MODE=Entra` and no `INSURANCEINTELLIGENCE_MULTI_TENANT` setting. The API code defaults this flag to `false`; the configured Key Vault had no secret names containing `MULTI` or `TENANT`. This confirms the live API's self-service organisation-signup path is closed under the deployed configuration. A user without a workspace therefore needs an administrator to provision access; enabling multi-tenant mode is not an appropriate sign-in workaround.
- The attempted read-only Azure SQL query was blocked by the SQL server firewall from this machine, and the configured SQL Entra administrator is a different identity from the current Azure CLI account. No firewall rule or database state was changed. The membership row count and organisation status therefore remain unverified.

### What remains unverified

- Whether the affected Microsoft `oid` has zero active IIP memberships, multiple distinct `(TenantId, WorkspaceId)` memberships without a selector, or a membership under an organisation that is not `Active`. Those cases all resolve to a null effective membership and the live warning does not distinguish them.
- Whether the live API contains exactly commit `4818bb11` or a later equivalent build. The warning text is evidence for the newer diagnostic path, but no deployment SHA is exposed.
- Whether the production database has applied and enforces the modeled unique membership index. The reviewed source model declares a unique `(TenantId, WorkspaceId, UserId)` index, but this review did not inspect Azure SQL or migration history.
- Whether a specific live insurer has approved and configured each social provider. Local code and a plan are not proof of a live connection.
- Whether the affected account's organisation-membership records agree with its runtime `UserTenantMemberships` and role assignments. `org/main` has backend tests for ambiguous memberships, but the portal has no normal first-sign-in organisation picker.

## Sign-in diagnosis

### Current request path

1. `web/portal/lib/entraAuth.ts` initializes MSAL, handles the redirect, and acquires an API token.
2. `web/portal/lib/runtimeContext.tsx` calls `GET /api/v1/me`.
3. API middleware reads the authenticated stable user identifier (`oid`), takes tenant/workspace claims and the requested-workspace header only as hints, and asks `GetEffectiveMembershipAsync` for an active IIP membership.
4. `RuntimeContextEndpoints.GetMe` returns 401 if unauthenticated, 403 if authenticated but no membership context was resolved, and a runtime context if resolution succeeds.
5. `web/portal/components/iip/AppShell.tsx` displays the same “could not be resolved to an IIP workspace” copy whenever runtime context is missing, regardless of the actual error.

### Confirmed causes and change history

| Finding | Evidence | Origin / significance |
|---|---|---|
| Fail-closed membership lookup can block an otherwise successful Microsoft sign-in | `GetMe` requires `tenantContext.HasCurrent`; middleware resolves a membership before setting context. | Introduced by `40f5aa18` (Gate 1 / ADR-0005 DB-authoritative membership resolver). This is an intentional tenant-security rule. It must not be “fixed” by trusting token tenant, role, or browser-provided scope. An eligible staff account still needs an active IIP membership. |
| Same-workspace duplicate membership rows can look ambiguous on this branch | `EfCorePorts.cs` counts returned rows; `memberships.Length != 1` returns null. | Fixed in `4818bb11` by deduplicating `(TenantId, WorkspaceId)` before checking ambiguity and adding safe diagnostic logging. The fix is present on `org/main` but absent from this checkout. It protects legacy/unconstrained data; the EF model also declares a unique triple index, whose live enforcement was not verified. |
| Even the newer membership warning is not a complete diagnosis | `4818bb11` logs “No single active membership” with user ID and tenant/workspace hints, without a correlation ID or a count/reason breakdown. | If that code is deployed, it can identify that membership resolution refused the request, but cannot by itself distinguish zero memberships from multiple distinct workspaces or stale/invalid hints. Add a safe refusal reason code and correlation ID, and retain the distinction in the client UI. |
| The portal hides actionable failure reasons | `runtimeContext.tsx` retains a `UiServiceError`, but `AppShell.tsx` ignores its status/message and renders one generic explanation. | Makes a missing membership, multiple memberships, expired/wrong-audience token, 401, 403, and API outage look alike. Add a safe, actionable reason plus correlation/reference ID; do not expose raw provider errors, token claims, or internal exception details. |
| Account selection can silently reuse the first cached Microsoft account | `entraAuth.ts` selects redirect result, then active account, then `getAllAccounts()[0]`. | Can pick a stale/wrong cached account in a shared browser. Add an explicit account chooser when multiple cached accounts exist and a clear “switch Microsoft account” action; never silently bind the user to an arbitrary cached identity. |
| A stale saved workspace/organisation can cause a 403 loop in older portal code | `org/main` `runtimeContext.tsx` retries `/api/v1/me` after clearing remembered workspace/organisation overrides when the first response is 403. | This recovery is present on `org/main`, not the reviewed checkout. It handles stale local selection only; a truly missing membership or ambiguous membership still needs admin provisioning/selection. The AppShell's visible message remains generic on `org/main`. |
| A person who belongs to multiple organisations cannot choose one on first sign-in | API middleware accepts `X-Organisation-Id` as a hint and the authenticated `/api/v1/me/organisations` endpoint lists the person's organisations. `org/main` portal only calls that endpoint to detect an empty list and redirect to `/welcome`; it does not render a picker when the list has entries. `config.ts` stores an active organisation override, but `scopedHeaders()` never sends `X-Organisation-Id`. The ordinary Settings workspace switcher only exists after `/api/v1/me` succeeds. | Added with multi-organisation support (`d3d729c5`), but portal selection is incomplete: a multi-membership lookup fails closed, with no selector and no client header to disambiguate. Add a pre-shell chooser that obtains the organisation list using the signed-in identity, sends its chosen ID as a hint, and lets the API revalidate membership before loading the workspace. Never choose the first organisation silently. |
| First-time setup is a dead end when single-tenant mode is active | On `org/main`, `AppShell` sends a signed-in identity with no listed organisation memberships to `/welcome`; `POST /api/v1/organisations` is rejected by `SignUpClosed` unless `MultiTenant=true` (or Development auth). | Confirmed in the live API App Service settings on 2026-11-07: `INSURANCEINTELLIGENCE_MULTI_TENANT` is absent and the API defaults it to false; the configured Key Vault has no matching tenant/multi-tenant secret names. A no-membership user cannot self-provision and must be added through the existing-workspace admin path. Do not turn multi-tenant on just to bypass this without completing its data, isolation, key-ring, and migration gates. |
| The recovery check and the runtime membership store are different models | `/api/v1/me/organisations` calls `OrganisationService.ListMineAsync` (organization memberships/links), while `/api/v1/me` resolves `UserTenantMemberships` and role assignments. | Listing an organisation does not itself prove the user has an active runtime membership. Provisioning/cutover must keep both records consistent. If the organisation list is empty, `AppShell` assumes a new user; with sign-up closed this becomes `/welcome` and then a 403. If the list is non-empty but runtime membership is absent, the user remains on the generic access error. |
| Live custom-host and redirect configuration may confuse the sign-in journey | Prior live browser/config inspection showed the custom login redirecting to the Azure App Service host; deployment docs list the App Service callback and legacy app registrations. | Confirm exact live redirect/authority registration before changing it. Register and consistently use the intended public origin, and document the canonical app registration cutover. This is a UX/config issue, not yet the proven cause of the workspace lookup failure. |

### Safe repair sequence

1. Use the confirmed App Insights warning and its operation ID, then have an authorized database operator run a read-only count of active membership scopes and check organisation status for the affected stable object ID. The local query attempt was blocked by the SQL firewall; do not widen the firewall as a shortcut. Record only the safe reason code and correlation ID, never tokens or customer data.
2. Compare live API/portal settings and deployment SHA with the approved source commit. `/health` needs a non-secret build identifier to make this verifiable.
3. Ensure the affected staff identity has exactly the intended active workspace membership and role assignments through the approved provisioning path. Do not infer business authorization from the Microsoft tenant or role claim.
4. Carry the deduplication fix into the release branch and verify the deployed database constraint/migration. Keep ambiguity across genuinely different workspaces fail-closed until a deliberate workspace selection flow is available; implement the missing pre-shell organisation chooser and send its selection as `X-Organisation-Id` for server revalidation.
5. Improve the access error screen to distinguish unauthenticated, membership missing, workspace selection required, and service unavailable, each with a safe next action and support reference.
6. Add multi-account selection and ensure the configured public redirect URI matches the actual public host.

For the authorized database operator, this read-only query returns only membership scopes, duplicate-row counts and organisation status. Replace the placeholder locally with the affected Entra object ID; do not paste the ID or returned tenant details into tickets or chat:

```sql
DECLARE @UserId nvarchar(240) = N'<affected Entra object ID>';

SELECT
    m.TenantId,
    m.WorkspaceId,
    COUNT_BIG(*) AS ActiveMembershipRows,
    o.Status AS OrganisationStatus
FROM dbo.UserTenantMemberships AS m
LEFT JOIN dbo.Organisations AS o
    ON o.OrganisationId = m.TenantId
WHERE m.UserId = @UserId
  AND m.IsActive = 1
GROUP BY m.TenantId, m.WorkspaceId, o.Status
ORDER BY m.TenantId, m.WorkspaceId;
```

Interpretation: no rows means no active direct membership; multiple distinct tenant/workspace rows mean the current no-hint request is ambiguous; an organisation status other than `Active` is also refused. One active scope with `Active` status should resolve under the current code, so investigate live build/configuration and the correlated operation if the query shows that result.

**Sign-in acceptance:** the intended account completes Microsoft sign-in; `/api/v1/me` returns its server-resolved workspace/role; a user without membership receives a clear invitation/provisioning path; a multi-workspace user can choose only from server-authorized memberships; duplicate legacy rows for one workspace do not lock the user out; wrong audience/outage is distinguishable; no client header or token role grants tenant access.

## Agent and customer workflow gaps

| Workflow | Current evidence | Gap and required direction |
|---|---|---|
| Ask the web assistant about a claim using an attached image | `AssistantBar.tsx` is text-input only; `AIConversations.tsx` and `ChatPrimitives.tsx` pass a string prompt and render a text composer. | No image/file attach flow for the assistant. Add authorized attachment upload, malware/content validation, secure evidence storage, OCR/vision extraction with field provenance, and a human correction step. Make unsupported formats and processing failures recoverable. |
| Send a WhatsApp image/document and get agent help | `WhatsAppCloudApiInboundProvider` maps image/document content; `CustomerAssistantService` routes these to `HandleMediaMessageAsync` and deliberately does not send the file to the language model. Claim document ingestion separately processes/attaches media where it can match a claim. | Receipt/claim attachment handling is not the same as image understanding. Link media to the correct customer/claim, then provide extracted facts with citations to the assistant only after permission and evidence checks. Unknown match stays in a visible manual-review queue. |
| Non-text messages on other social channels | Meta and X webhook code records non-text as unsupported; the plan says TikTok inbound is not implemented. | Implement only after provider contracts/permissions are confirmed. Show exact capability per account; unsupported media must offer a secure upload alternative and appear in staff review rather than silently disappearing. |
| Cross-channel recognition | `IIP-OMNICHANNEL-CHANNEL-EXPANSION-PLAN.md` requires verified identity links; channel identities are stored separately. | A same name/handle/phone is not enough to merge people. Complete a verified, auditable link/correct/unlink workflow; maintain channel-specific consent and prevent cross-tenant context leakage. |
| Telegram / Meta / X / TikTok in one Inbox | The expansion plan records local implementation slices but also missing live proof, migration application, delivery receipts/media support, and TikTok inbound contract. | Do not equate local endpoints or outbound code with end-to-end availability. Each account needs provider verification, webhook health, inbound event/thread mapping, safe reply, consent/window checks, retries/idempotency, status reconciliation, database migration, and staging evidence. TikTok inbound and account-specific proof remain explicit gaps. |
| Outbound reminders and follow-ups | The expansion plan says durable follow-up task storage exists, but scheduling tool/worker, approved-template configuration and delivery/recovery are not wired. | A stored reminder will not send itself. Finish scheduling, worker, policy rechecks at send time, duplicate prevention, cancellation on opt-out/new response, provider-template/window constraints, and operator visibility for overdue/failed tasks. |
| Languages | The expansion plan catalogs text languages and says voice adapters currently cover Luganda and Kiswahili; it requires native-speaker/insurance review. | Tenant language settings alone do not validate translation quality. Test supported languages on policy terms, negation, amounts, dates, and extraction; preserve original source and require human review where quality is not validated. |

## Claims document, fraud and export review

These findings are from `org/main` source, not a live claim submission. The live readiness response lists Azure Document Intelligence and Azure Blob as configured providers, but I did not upload a real or synthetic test pack in this review.

| Capability | Evidence in code | Current state / remaining validation |
|---|---|---|
| Claim document images and PDFs | `DocumentEndpoints.UploadDocuments` accepts multipart files, stores originals and hashes, and submits to `DocumentPipeline`. The document classifier, extraction provider, recognizer, validation rules, and claim-slot checklist process the stored file. | **Claim-specific uploads exist.** This is separate from assistant chat, which has no attachment composer, and from WhatsApp images, which are not fed to the agent as visual evidence. Validate each enabled image/PDF format, large and rotated scans, duplicate pages, and a complete product-specific claim pack against ground truth. |
| Extraction quality and human correction | `DocumentProcessingRunner` persists classification, extracted fields, validation warnings and recognition; `DocumentMapper` exposes allowed corrections and blocks approval until processing/classification/blocking checks pass. `DocumentPipeline` can process inline or queue a durable job; jobs can retry and dead-letter. `FieldProposalService` proposes extracted claim fields for human review. | Workflow and human review controls are present. Field quality, source bounding boxes, end-to-end processing time, worker retry recovery and proposal-to-claim linkage remain unverified against sample documents and current Azure runtime. Extraction approval only confirms a person accepted the extracted fields; it is not a claim decision. |
| Fraud/risk signal coverage | `FraudRuleEngine` computes deterministic signals for shared phone/email across claims, claim velocity, one policy linked to multiple claimant names, low document completeness, early claim after policy inception, and high sum insured. Defaults include 3 claims within 30 days, 14 days from policy inception, and UGX 100,000,000 sum insured; tenant rules can override supported thresholds. `FraudQueryService` reads tenant-scoped claims/policies and joins triage/assignment/outcome dispositions. | **Advisory rules are linked and auditable**, not an AI fraud verdict. The default velocity rule is not “four claims in three days”; the limits are configurable. Repeated patterns not represented in structured claims/policy fields, cross-insurer external data, provider/garage/repair-network links, image manipulation, and coordinated networks need additional data/rules and validation. Review thresholds with Ugandan insurer investigators before use. |
| Claim assessment | `ClaimAssessmentService` combines fixed fraud rules, image authenticity observations, case-review narrative signals, and missing/unreadable documents into a sourced recommendation; stale inputs are detected and a human can agree/disagree with an audited reason. | The decision boundary is correctly advisory. A claim summary or recommendation must not be presented as approval/repudiation or as a confirmed fraud finding. Validate visible citations and stale-assessment refresh behavior in the actual claims UI. |
| Claim export pack | `ClaimsPackEndpoints.Download` builds a ZIP with summary PDF, customer documents renamed by type, originals as received, and a SHA-256 manifest; export requires permission, tenant-scoped claim lookup and audit. Separate JSON audit export and email/share flows exist. | Source path is present. The user-requested live export still needs an end-to-end run confirming content, hashes, filenames, permission denial, audit record and large-pack delivery. Do not call it live-verified based on the endpoint existing. |

The priority for extraction testing is to pair each synthetic claim pack with a ground-truth JSON and verify field value, document type, page/region evidence, confidence, validation warning, missing document, correction/approval state and final export manifest. Include consistent, incomplete, contradictory, duplicate, rotated/blurred and low-resolution variants for motor, life, health, property and third-party injury. Keep a reviewer as the authority for document correction, claim outcome and fraud disposition.

## Branch reconciliation for reported customer-facing issues

The fixes for the other two issues previously reported are in the authoritative `org/main` history, not in the active planning checkout:

| Issue | Earlier cause in this checkout | Fix found on `org/main` | Remaining proof |
|---|---|---|---|
| Bank-assurance assistant says it lacks bank/service details after switching modes | The reviewed code initializes `KatoBrand.Current` once from deployment configuration. `ToolListProducts` and system-prompt instructions read this static brand/catalog, so changing a workspace/demo mode does not itself change the model's approved bank description or offered products. | `3f15a6e0` (2026-11-06) registers workspace brand services, wires a workspace-scoped assistant brand provider to tenant settings and published products, injects approved organisation descriptions, and changes the prompt to say when product details are not configured. Workspace-specific brand support predates it (`e8bffc8d`), but this commit closes the assistant prompt/catalog gap. | Confirm the target workspace has its bank description and published products configured, deploy an artifact containing this commit, and test actual questions against those records. No live build SHA or target-workspace configuration was available here. |
| WhatsApp contact not visible/current in Inbox or Acquisition; message may route against stale owner | The reviewed code creates or refreshes prospects inside normal assistant handling and catches projection errors so the customer still gets a response. Queue timing can also leave a saved inbound message to be processed after the WhatsApp number has changed workspace owner. | `24b0a477` (2026-11-06) refreshes prospects before brand lookup/agent gating, includes existing policyholders, resolves names from customer records, triggers triage, records refresh failures in audit, and checks current number ownership before the queued assistant responds. The Inbox reader also joins message contacts to customer/prospect names by normalized phone. | Confirm this artifact is deployed, then test both a never-seen number and an existing policyholder against the correct WABA/workspace. Prospect refresh remains best effort with audit-only failure handling; a durable retry/reconciliation queue is still a reliability improvement. |

`org/main` at the freshly confirmed revision (`49f0a127`) contains these fixes, as well as the duplicate-membership fix `4818bb11`. They are not in `plan/consolidation-2026-11-03` at `d3500235`. Local `main` is a separate worktree at `495db1ad`, a direct ancestor and four commits behind `org/main`. The live health endpoint does not publish a source revision, so none of these code commits can be called live until deployment metadata or runtime behavior proves it.

## Existing work that should remain the source of truth

- [IIP omnichannel channel expansion plan](IIP-OMNICHANNEL-CHANNEL-EXPANSION-PLAN.md) contains the detailed provider status, review/autopilot dispatch guard, cross-channel identity, notifications, languages, document-generation prompts, and browser acceptance criteria. This review does not replace those requirements.
- [Canonical Entra cutover record](../05-deployment-release-enablement/iip_canonical_entra_cutover_2026-07-16.md) states that the live dev portal still used legacy application registrations at the time it was written, SQL migration/access was blocked, and cutover required confirming the launch user's active IIP membership.
- [Entra provisioning runbook](../05-deployment-release-enablement/iip_gate1_azure_entra_provisioning_runbook.md) defines the intended fail-closed membership rule and live `/api/v1/me` verification.
- `org/main:docs/05-deployment-release-enablement/IIP_NEXT_STEPS_DOMAIN_MULTITENANT_PRICING.md`, written 2026-10-09, records the earlier single-workspace/sign-up-closed state. This review checked the live API settings on 2026-11-07: auth mode is Entra, the multi-tenant flag is absent, code defaults it to false, and no matching Key Vault secret names were found. The live API therefore has sign-up closed. Its health endpoint still omits a build SHA and the flag, so deployment provenance remains unknown.
- `org/main`'s `MembershipLookupTests` cover single membership, duplicate same-workspace rows, distinct-workspace ambiguity, and inactive memberships. Portal `sign-in.spec.ts` and `stand-in-microsoft.ts` simulate Microsoft and stub `/api/v1/me`; the no-membership scenario always stubs `/api/v1/me/organisations` as `[]`, so it routes to `/welcome`. Those tests do not exercise live Microsoft, an existing multi-organisation account, a closed sign-up response, or the actual Azure membership database.
- The current health response reports `Demo` with host environment `Production`. This is a demo runtime behind production hosting, not proof that production customer data or all capabilities are active.

## Prioritized closeout checklist

### P0: Let authorized people enter

- [ ] Read-only diagnose the live failed `/api/v1/me` request by correlation ID and membership resolution result.
- [ ] Verify live build SHA, Entra app IDs/audience/redirect origin, API configuration, and DB migration/index state.
- [ ] Apply the existing duplicate-row fix to the release branch if absent; provision the correct active workspace membership using the approved admin path.
- [ ] Replace the generic access error with safe reason-specific instructions and a support reference.
- [ ] Add a first-sign-in organisation chooser for users with multiple memberships; verify zero-membership onboarding behaves correctly for the deployed multi-tenant setting.
- [ ] Verify sign-in in the actual public browser with the intended identity and a negative case without membership.

### P1: Complete agent-assisted document intake

- [ ] Add attachment support to the web assistant and a secure path from inbound channel media to claim evidence.
- [ ] Extract image/document fields with source-page/region provenance and confidence; let a human correct fields.
- [ ] Never treat extraction or fraud signals as final coverage/fraud decisions; route uncertainty to a human.
- [ ] Verify Inbox media is not silently discarded and unsupported media has a visible next step.

### P2: Finish truthful channel availability

- [ ] Complete TikTok inbound only after first-party webhook/event/signature contract verification.
- [ ] Prove each enabled channel with a real account in staging: inbound text/media, Inbox identity/threading, consent, human reply, delivery result, retry/duplicate behavior, and health UI.
- [ ] Apply and verify migrations before enabling protected runtime routes.
- [ ] Complete durable follow-up execution and operational recovery.
- [ ] Finish verified cross-channel identity linking and configurable notification recipient/destination controls from the existing plan.
- [ ] Reconcile language UI availability with validated model/provider support and actual runtime behavior.

## Delivery status

This review is source/runtime analysis only. No product source code was changed, no tests were run, no Azure resource or database was changed, and nothing was committed or deployed. Read-only App Insights evidence now proves the failure is IIP membership resolution after Microsoft authentication; the exact membership/organisation row state and deployed SHA remain unverified because direct SQL access is blocked by the firewall. Azure CLI login and App Service/SQL authorization are separate from the browser login; deployment should not be used as a substitute for diagnosing the membership failure.
