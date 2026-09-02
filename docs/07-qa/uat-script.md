# IIP UAT Script

**Status:** Phase 8 draft execution script  
**Date:** 2026-07-08  
**Scope:** Insurance Intelligence Platform (IIP) Operations UAT for dev/UAT candidate builds.

Critical journey definitions and stable IDs come from `../03-design-architecture/iip_release_1_task_flow_exception_state_catalog.md`; role context comes from `../03-design-architecture/iip_release_1_role_journey_maps.md`. This script records execution evidence and must not redefine their business or state rules.

## 1. Operating Rules

- Test only with `LocalTest` synthetic records or approved masked insurer records.
- Do not treat seeded, mock, placeholder, or fixture data as production readiness evidence.
- Empty data must remain empty. Missing providers must show `Not configured`.
- All consequential insurance decisions remain human-reviewed.
- Tenant, workspace, role, and permission authority must come from server-resolved runtime context, not browser headers, route parameters, or client state.
- Protected runtimes must not expose fake provider success, fake dashboards, DevelopmentAuth, in-memory stores, SQLite, or fallback-to-mock behavior.

## 2. Current Known Blockers

| Blocker | Impact | Required resolution |
|---|---|---|
| Entra consent and authenticated `/api/v1/me` smoke are resolved for the current dev administrator; broader role UAT is still open. | One Admin identity is proven end to end, but the full role matrix and customer-tenant onboarding flow are not yet evidenced. | Provision approved UAT identities through the membership/onboarding authority and execute the route matrix without browser-owned authority headers. |
| Azure OpenAI, Document Intelligence, Graph/email, and related provider credentials may be absent in dev. | AI, extraction, email, and send paths may correctly render `Not configured`. | Configure real providers or leave visible `Not configured` states; do not enable fake success paths. |
| Staging/PAYG resource capability remains gated by subscription/provider availability. | Staging-grade UAT and pilot-readiness sign-off cannot be claimed from the current dev-only environment. | Re-run this script in the approved staging or reviewer environment after capability is available. |
| Production residency decision values are not yet approved. | Production/reviewer deployment cannot claim a jurisdiction, compliant region, DR posture, or model-processing boundary. | Approve the country/geography, primary and secondary region allowlist, residency mode, storage replication, Azure OpenAI deployment type, and non-secret approval reference; then verify the compliance posture reports `Compliant`. |

## 3. Roles Under Test

| Role key | User-facing role | Landing route | Primary UAT focus |
|---|---|---|---|
| `Admin` | Insurer Admin / Workspace Administrator | `/app` | Tenant configuration, role visibility, provider state, auditability. |
| `ITAdmin` | IT / Security Admin | `/app/integrations` | Identity, integrations, health, monitoring, and admin access. |
| `ClaimsOfficer` | Claims Officer | `/app/claims` | Claims, documents, missing items, draft updates, and evidence. |
| `ClaimsSupervisor` | Claims Supervisor | `/app/work-items` | Prepared-work review, escalations, workflow approvals, SLA risk, and team visibility. |
| `CustomerService` | Customer Service Agent | `/app/channels` | Channels, customer conversations, communications, and safe drafts. |
| `Compliance` | Compliance Officer | `/app/audit` | Audit logs, approvals, AI outputs, overrides, and evidence lineage. |
| `KnowledgeManager` | Knowledge Manager | `/app/knowledge` | Approved knowledge, document visibility, and policy retrieval readiness. |
| `PublisherSupport` | Publisher Platform Administrator | `/app/integrations` | Deployment health, provider state, onboarding, and tenant support. |

## 4. Route Access Matrix

Expected route access follows the live portal role config. Backend authorization must still be verified independently.

| Route | Admin | ITAdmin | ClaimsOfficer | ClaimsSupervisor | CustomerService | Compliance | KnowledgeManager | PublisherSupport |
|---|---|---|---|---|---|---|---|---|
| `/` login | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes |
| `/app` Command Center | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes |
| `/app/claims` | Yes | No | Yes | Yes | No | No | No | No |
| `/app/claims/new` | Yes | No | Yes | Yes | No | No | No | No |
| `/app/claims/{id}` | Yes | No | Yes | Yes | No | No | No | No |
| `/app/channels` | Yes | No | Yes | No | Yes | No | No | No |
| `/app/customers` | Yes | No | Yes | Yes | Yes | No | No | No |
| `/app/documents` | Yes | No | Yes | No | No | No | Yes | No |
| `/app/communications` | Yes | No | Yes | Yes | Yes | Yes | No | No |
| `/app/acquisition` | Yes | No | No | No | Yes | No | No | No |
| `/app/policy` | Yes | No | Yes | Yes | No | Yes | Yes | No |
| `/app/fraud` | Yes | No | No | Yes | No | No | No | No |
| `/app/underwriting` | Yes | No | No | No | No | No | No | No |
| `/app/work-items` | Yes | Yes | Yes | Yes | Yes | Yes | No | No |
| `/app/flows` | Yes | Yes | Yes | Yes | Yes | Yes | No | No |
| `/app/runs/{id}` | Yes | Yes | Yes | Yes | Yes | Yes | No | No |
| `/app/knowledge` | Yes | No | Yes | No | Yes | No | Yes | No |
| `/app/executive` | Yes | No | No | Yes | No | Yes | No | Yes |
| `/app/integrations` | Yes | Yes | No | No | No | No | No | Yes |
| `/app/audit` | Yes | Yes | No | No | No | Yes | No | Yes |
| `/app/admin` | Yes | Yes | No | No | No | No | No | Yes |
| `/app/admin/agents` | Yes | No | No | No | No | No | No | Yes |
| `/app/admin/billing` | Yes | No | No | No | No | No | No | No |
| `/app/admin/tenant-policy` | Yes | No | No | No | No | No | No | No |
| `/app/admin/onboarding` | Yes | No | No | No | No | No | No | Yes |

## 5. Motor Claim Scenario Pack

Load these as synthetic or approved masked records before execution. Each scenario must include tenant/workspace scope, audit-ready identifiers, and source evidence references.

| Scenario ID | Scenario | Required evidence |
|---|---|---|
| `MOTOR-001` | Clean motor claim | Policy, claim, complete document set, normal SLA state. |
| `MOTOR-002` | Missing documents | Claim plus required-document gaps. |
| `MOTOR-003` | Expired permit | Claim plus expired permit or license evidence. |
| `MOTOR-004` | Field mismatch | Policy/claim/document mismatch requiring human review. |
| `MOTOR-005` | High-value repair quote | Quote above configured threshold requiring supervisor review. |
| `MOTOR-006` | Duplicate invoice | Two invoice records with duplicate reference or hash. |
| `MOTOR-007` | Blurry ID | Low-confidence extraction requiring correction/review. |
| `MOTOR-008` | Risk/fraud signal | Non-accusatory risk indicators routed for human review. |
| `MOTOR-009` | Escalated claim | Workflow escalation and approval queue entry. |
| `MOTOR-010` | Complaint-linked claim | Customer complaint history and compliance visibility. |
| `MOTOR-011` | Assessor report revision | Authorized minimum referral pack plus acknowledged report and a later superseding revision. |
| `MOTOR-012` | Evidence eligibility matrix | Current, superseded, unverified, contradicted, stale, unauthorized and unsupported-language evidence references. |

## 6. Functional Scripts

| ID | Role | Steps | Pass criteria |
|---|---|---|---|
| `UAT-AUTH-001` | All roles | Sign in with the configured organization account, complete MFA, open `/api/v1/me`. | User lands on the role landing route; `/api/v1/me` returns server-resolved tenant, workspace, user, role, and environment source. |
| `UAT-AUTH-002` | All roles | Attempt to access one denied route from the matrix. | UI blocks navigation or redirects safely; backend route denies data/action access if called directly. |
| `UAT-AUTH-003` | ClaimsOfficer | Open `MOTOR-002`, begin supported draft/review work, expire the session or revoke the relevant permission, then reauthenticate. | `FLW-R1-AUTH-01`: committed work survives; restricted content/action is removed; authority and current evidence are re-resolved; prior submit is not replayed. |
| `UAT-CC-001` | ClaimsSupervisor | Open `/app`, ask for claims at SLA risk. | Command Center returns structured blocks, scoped claims only, and evidence/context rail updates without fabricated metrics. |
| `UAT-CLAIMS-001` | ClaimsOfficer | Open `/app/claims`, filter claims, open `MOTOR-002`. | Queue and detail show claim facts, missing documents, SLA state, and no cross-tenant records. |
| `UAT-FNOL-001` | ClaimsOfficer | Open `/app/claims/new`, submit a valid FNOL draft if enabled. | Created claim is tenant/workspace scoped, audit event is written, and missing fields fail with safe validation. |
| `UAT-DOCS-001` | ClaimsOfficer | Open a claim extraction review for `MOTOR-007`, correct one extracted field. | Correction is persisted, audit event is written, and downstream completeness is marked for re-evaluation. |
| `UAT-DOCS-002` | ClaimsOfficer | Review `MOTOR-012` and exercise each applicable evidence condition. | `FLW-R1-EVIDENCE-01`: provenance/version remains visible; ineligible evidence reaches the documented owned path and cannot support a consequential action. |
| `UAT-ASSESSOR-001` | ClaimsOfficer | For `MOTOR-011`, prepare the minimum assessor referral; exercise the configured manual baseline and, where enabled, Graph lost-response reconciliation; record exact-version acknowledgement; receive a late/duplicate/matched report; then receive a superseding revision. | `FLW-R1-ASSESSOR-01`: unapproved disclosure/configuration is blocked; export, staff attestation, Graph acceptance/unknown, acknowledgement, receipt, review and claim outcome stay distinct; retry follows reconciliation; exact pack/report versions remain traceable; prior report is superseded rather than erased; no core write-back occurs. Scope accepted under DEC-018; runtime remains blocked until policy/contracts/environment exist. |
| `UAT-ASSESSORSEC-001` | ClaimsOfficer, ITAdmin, Security/Privacy observer | Exercise `THR-R1-ASR-01`–`08`: stale/spoofed/look-alike/changed assessor, wrong/excessive/cross-tenant pack, forwarding and expired token/link, foreign mailbox/message/attachment/case/blob IDs, hostile attachment corpus and scanner outage, false/manual/provider acknowledgement, duplicate/reordered/late return, and audit reconstruction. | `AC-ASR-01`: zero unauthorized release, quarantine bypass, bearer evidence URL/SAS or cross-tenant disclosure; sender/token never grants authority; material change invalidates preflight; scanner outage holds evidence; remote copies are never claimed revoked; every distinct state is safely auditable. Blocked until implementation and protected controls exist. |
| `UAT-ASSESSORETH-001` | ClaimsOfficer, CustomerService/Complaints, authorized independent reviewer, Privacy/Accessibility observer | For `MOTOR-011`, compare a claim-question-bound minimum pack with an excessive whole-file pack; provide accessible claimant notice and exercise language/access need plus a narrow later-notice exception; dispute an incorrect/limited assessor report and its proposed reliance through mediated intake; correct context, obtain independent reconsideration/another assessment where required, and inspect downstream/data-use inventory. | `AC-ETH-01`: every released item has approved necessity and excessive disclosure is blocked; notice is useful and distinct from consent; request is acknowledged/owned without an internal-app account; exact report becomes `Contested` and cannot support new action until reasoned reconsideration; correction/remedy propagates without erasing history; zero unapproved training, ranking, retrieval-authority or scoring reuse; actual burden/fairness measures and residual risk are recorded. Blocked until policy, contracts, implementation, representative identities/content and claimant/practitioner validation exist. |
| `UAT-WF-001` | ClaimsSupervisor | Open `/app/work-items`, switch to **Consequential approvals**, then approve or escalate the `MOTOR-005` workflow step. | Prepared work and consequential approvals are visibly separated; human decision guard is visible; action records actor, reason, timestamp, and before/after workflow state. |
| `UAT-WF-002` | ClaimsSupervisor | Attempt to decide or execute a mismatched, expired, stale-evidence, unauthorized, or already-decided approval; also exercise an alternate workflow path. | `FLW-R1-DECISION-01`: every attempt fails safely or returns/escalates; no system/AI/alternate path bypasses the exact authorized-human gate; committed prior state remains. |
| `UAT-COMMS-001` | CustomerService | Open `/app/channels`, select a claim conversation, draft a customer update. | Draft is customer-safe, source-grounded, not sent automatically, and pending approval appears where required. |
| `UAT-COMMS-002` | CustomerService | Exercise missing preflight, provider timeout after possible acceptance, duplicate/out-of-order callback, and retry. | `FLW-R1-COMMS-01`: draft is preserved; no unsafe send occurs; unknown remains distinct; reconciliation precedes retry; delivery status is monotonic and auditable. |
| `UAT-COMPLIANCE-001` | Compliance | Open `/app/audit`, inspect `MOTOR-009` and export an audit pack if configured. | Audit view includes AI/human actions, evidence references, overrides, actor IDs, and safe export status. |
| `UAT-COMPLIANCE-002` | Compliance | Attempt a consequential approval/override while the target separation-of-duty policy is unapproved or denies that combination. | `AC-SOD-01`: unresolved `PERM-GAP-01` blocks a least-privilege/independent-review pass; once configured, server denial and safe audit evidence match the approved policy. |
| `UAT-KNOW-001` | KnowledgeManager | Open `/app/knowledge`, review approved and unapproved knowledge records. | Only approved knowledge is retrievable by AI; unapproved/sensitive records are not used as authoritative answers. |
| `UAT-INT-001` | ITAdmin | Open `/app/integrations`. | The unified Integrations & Health control plane shows real configured state or `Not configured`; no fake green status, connection-test success, queue depth, or support ticket is shown. |
| `UAT-ADMIN-001` | Admin | Open `/app/admin`, update a non-destructive tenant setting in the UAT tenant. | Change is permission-checked, audit-logged, and cannot widen platform ceilings or bypass human review. |
| `UAT-AGENT-001` | Admin | Open `/app/admin/tenant-policy`, narrow one allowed AI capability. | Policy validator accepts narrowing changes and rejects any attempt to exceed platform ceilings. |
| `UAT-SUPPORT-001` | PublisherSupport | Open `/app/integrations` and inspect deployment/provider status. | The tenant-scoped health surface exposes no secrets, raw provider errors, or cross-tenant data. |
| `UAT-UX-001` | ClaimsOfficer, ClaimsSupervisor, CustomerService, Compliance, Admin as applicable | Exercise one permitted and one denied/stale representative from each interaction family: case, queue, document, evidence, approval, communication, export, conversation and safety control. Inspect preflight, submit, pending and outcome. | `AC-PRE-01`/`AC-ACT-01`: exact purpose, target, effect, prerequisites, authority, reversibility and audit consequence are visible where applicable; no preselected consequential outcome, duplicate activation, premature success, or hidden denied effect occurs. |
| `UAT-UX-002` | Applicable internal roles | Inject concurrent change, duplicate submit, offline interruption, session expiry, provider `Not configured`, timeout-before/after-possible-effect and partial failure into representative mutable/external actions. | `AC-CON-01`/`AC-REC-01`: safe input and committed work survive; conflict/unknown remain explicit; authority is re-resolved; reconciliation precedes retry; focus reaches the owned recovery region; no duplicate effect occurs. |
| `UAT-A11Y-001` | Applicable internal roles using keyboard and touch | Complete the critical interaction matrix at desktop and narrow layout using keyboard only, then physical touch with reduced motion enabled; open/close every modal primitive. | `AC-A11Y-02`/`04`/`06`: logical visible focus; correct modal entry/containment/return; no keyboard trap; required 44-pixel targets and WCAG floor pass; no nonessential motion; every action remains understandable. |
| `UAT-A11Y-002` | Applicable internal roles using NVDA/Chrome plus one additional supported screen-reader/browser | Complete critical default/loading/partial/validation/permission/provider/conflict/success/recovery states at 200% text, 400%/320 CSS-pixel reflow and Windows forced colors. | `AC-A11Y-01`/`03`/`05`: names, roles, states, instructions, errors, progress and outcomes are announced once with useful context; focus remains appropriate; contrast/non-color/forced-colors and reflow pass with zero critical/serious barrier. |

## 7. API Smoke Checklist

Run these before UAT and after each deployment.

| Check | Expected |
|---|---|
| `GET /health/ready` | `200 OK` only when required runtime dependencies are ready for the selected profile. |
| Unauthenticated `GET /api/v1/me` | `401 Unauthorized`. |
| Authenticated `GET /api/v1/me` | Resolved tenant/workspace/user/roles from membership context. |
| Authenticated `GET /api/v1/compliance/posture` as Compliance/Admin | Tenant/workspace-scoped retention, consent, SLA, data-residency, and provider posture aggregates; no raw customer records, admin emails, callback payloads, or cross-tenant data. |
| Spoofed tenant/workspace/user browser headers in authenticated calls | Ignored where server-resolved context exists. |
| Protected runtime startup with mock/simulated/provider fallback config | Fails closed. |
| Teams notification worker receives a malformed envelope or provider does not confirm delivery | Invocation fails for Service Bus retry/dead-letter handling; it is not logged or acknowledged as delivered. |
| Published Functions binding metadata | Queue is `%ServiceBus:ApprovalNotificationsQueue%`; connection prefix is `ServiceBusConnection`; deployed settings provide `ServiceBus__ApprovalNotificationsQueue` and `ServiceBusConnection__fullyQualifiedNamespace`. |

### 7.1 Live Smoke Runner

Use the repo-owned smoke runner for repeatable dev/UAT probes:

```powershell
# Public readiness, unauthenticated denial, and portal route check only.
powershell -ExecutionPolicy Bypass -File tools\qa\Invoke-IipLiveUatSmoke.ps1 -SkipAzToken

# Full authenticated gate after tenant admin consent/sign-in is complete.
powershell -ExecutionPolicy Bypass -File tools\qa\Invoke-IipLiveUatSmoke.ps1 -RequireAuthenticated

# Role-specific authenticated evidence with JSON output.
powershell -ExecutionPolicy Bypass -File tools\qa\Invoke-IipLiveUatSmoke.ps1 `
  -RequireAuthenticated `
  -ExpectedRole ClaimsSupervisor `
  -ExpectedMeSource MembershipStore `
  -IncludeClaimsList `
  -IncludeProviderStatus `
  -IncludeCompliancePosture `
  -DeniedApiPath "/api/v1/admin/users" `
  -OutputPath "artifacts/rf09/claims-supervisor-uat.json"
```

The runner accepts `-ApiBaseUrl`, `-PortalBaseUrl`, `-Scope`, `-AccessToken`, `-ExpectedRole`, `-ExpectedTenantId`, `-ExpectedWorkspaceId`, `-ExpectedMeSource`, `-DeniedApiPath`, `-IncludeClaimsList`, `-IncludeProviderStatus`, `-IncludeCompliancePosture`, `-TimeoutSeconds`, and `-OutputPath` overrides. It does not persist tokens. `-RequireAuthenticated` fails if `az account get-access-token --scope <scope>` cannot obtain a user token or if authenticated `/api/v1/me` does not return `200`.

### 7.2 Admin Consent URL Helper

Use the non-mutating helper to produce the portal app admin-consent URL for the current dev/UAT tenant:

```powershell
powershell -ExecutionPolicy Bypass -File tools\qa\New-IipAdminConsentUrl.ps1
```

Override `-TenantId`, `-PortalClientId`, `-PortalBaseUrl`, `-RedirectUri`, and `-Scope` when testing another tenant or deployment. The helper only prints URLs and next commands; it does not create, update, or delete Entra or Azure resources.

Important: portal admin consent grants consent for the portal SPA/client application. The smoke runner's default token probe uses Azure CLI as a separate public client, so Azure CLI token acquisition can still require separate tenant consent even after browser sign-in is unblocked. If a valid user access token is obtained through another approved route, pass it to the smoke runner with `-AccessToken`.

### 7.1 Performance Smoke Checklist

| Check | Expected |
|---|---|
| `node tools/qa/Invoke-IipPerformanceSmoke.mjs --self-test --requests 20 --concurrency 5` | Harness passes readiness, claims-list, and claim-detail loopback scenarios with zero errors. |
| Remote target without `--allow-remote` | Runner refuses before sending traffic. |
| Protected scenario without `IIP_UAT_ACCESS_TOKEN` | Runner fails closed before sending protected requests. |
| `node tools/qa/Invoke-IipPerformanceSmoke.mjs --self-test --include-provider-health --include-compliance --claim-id <id>` | Harness also proves representative authenticated provider-status and compliance-posture scenarios. |
| Approved dev/staging run with a real token and synthetic claim ID | Health p95 < 300 ms, claims list p95 < 800 ms, claim detail p95 < 1.2 s, zero unexpected HTTP responses, no cross-tenant data. |

The self-test validates the harness only. Remote load execution requires explicit approval and does not become pilot evidence until run against the approved environment with a real token and synthetic records.

## 8. Go / No-Go Criteria

| Area | Go criteria |
|---|---|
| Backend | `dotnet restore`, `dotnet build`, and `dotnet test` pass for `InsuranceIntelligence.sln`. |
| Portal | `pnpm lint`, `pnpm test`, and `pnpm build` pass in `web/portal`. |
| Visual/UAT | Role walkthroughs pass for all required routes with no P0/P1 defects. |
| Security | Unauthorized routes and direct API calls deny access; no tenant spoofing; no secret exposure. |
| Providers | Real providers work or show `Not configured`; no fake success. |
| Audit | Workflow, AI, provider, approval, communication, export, and admin actions write audit events. |
| Human review | No AI path finalizes consequential insurance decisions. |
| Data | Empty states are honest; synthetic test pack is clearly marked; no fabricated operational metrics. |

## 9. Execution Record

| Date | Build/deployment | Executor | Result | Notes |
|---|---|---|---|---|
| 2026-07-08 | Local portal QA guard scripts | Codex | Partial pass | `pnpm check:runtime-data`, `pnpm check:ui-release-gates`, and `pnpm check:role-routes` passed after updating stale guard scripts to the current `/app/*` codebase layout. |
| 2026-07-08 | Portal lint/build | Codex | Pass with warning | `pnpm lint` and `pnpm build` passed. Both report the existing sidebar logo `<img>` warning and the `next lint` deprecation notice. |
| 2026-07-08 | Deployed-profile DOM guard | Codex | Pass with skips | `pnpm test:deployed-dom` passed `3`, skipped `3`. Skips are for the customer portal route, which is not currently mapped in the Next `/app/*` route tree. |
| 2026-07-08 | Backend solution tests | Codex | Pass | `dotnet test InsuranceIntelligence.sln` passed `354` tests: `100` Domain and `254` API. |
| 2026-07-09 | Configured visual suite, Desktop Chromium | Codex | Pass with skips | `pnpm exec playwright test -c tests/visual/playwright.config.ts --project=desktop-chromium` passed `24`, skipped `9` deployed-profile checks in Development mode. Reconciled Gates 0.2, 1, and 5-12 to current `/app/*` routes and removed fake-success Admin/Integrations residue. |
| 2026-07-09 | Configured visual suite, Tablet Chromium | Codex | Pass with skips | `pnpm exec playwright test -c tests/visual/playwright.config.ts --project=tablet-chromium` passed `23`, skipped `10` checks in Development mode after aligning Command Center assertions with compact provider-status behavior below desktop width. |
| 2026-07-09 | Configured visual suite, Mobile Chromium | Codex | Pass with skips | `pnpm exec playwright test -c tests/visual/playwright.config.ts --project=mobile-chromium` passed `23`, skipped `10` checks in Development mode after fixing the governed-action dialog footer for mobile viewport reachability and aligning mobile Command Center assertions. |
| 2026-07-09 | Configured visual suite, all projects | Codex | Pass with skips | `pnpm test:visual` passed `73`, skipped `29` deployed-profile checks in Development mode. Added tenant-onboarding visual coverage for Entra tenant versus Azure subscription separation, removed obsolete root-level legacy visual specs and stale snapshot folders, and confirmed removed-route references are absent from `tests/visual`. |
| 2026-07-09 | Protected-runtime visual checks | Codex | Pass | With `IIP_VISUAL_RUNTIME_PROFILE=Staging` and `IIP_VISUAL_AUTH_MODE=Entra`, `pnpm exec playwright test -c tests/visual/playwright.config.ts --grep "@deployed"` passed `9/9` on Desktop, `9/9` on Tablet, and `9/9` on Mobile. Local note: generated `.next` had to be cleared between protected-project runs after stale manifest `readlink` errors in the OneDrive checkout. |
| 2026-07-09 | Deployed-profile DOM guard refresh | Codex | Pass with skips | `pnpm test:deployed-dom` passed `3`, skipped `3`. Staff shell and mobile staff protected-profile checks pass; skips remain for the unmapped customer route. |
| 2026-07-09 | Portal lint/build after responsive visual cleanup | Codex | Pass with warning | `pnpm lint` passed with the existing sidebar-logo `<img>` warning and `next lint` deprecation notice. `pnpm build` passed after retry; the first attempt compiled/generated pages but failed during a Windows/OneDrive `.next/standalone` copy operation. |
| 2026-07-09 | Dev live smoke runner, unauthenticated | Codex | Pass with auth blocked | `tools/qa/Invoke-IipLiveUatSmoke.ps1 -SkipAzToken` passed API readiness (`200`), unauthenticated `/api/v1/me` denial (`401`), and portal `/app` route (`200`). Authenticated `/me` was intentionally marked blocked because no token was supplied. |
| 2026-07-09 | Dev live smoke runner, authenticated-required | Codex | Blocked | `tools/qa/Invoke-IipLiveUatSmoke.ps1 -RequireAuthenticated` passed readiness, unauthenticated denial, and portal route, then failed at token acquisition with `AADSTS65001 consent_required` for the configured API scope. |
| 2026-07-09 | Admin consent URL helper | Codex | Ready for operator action | Added `tools/qa/New-IipAdminConsentUrl.ps1` to print the tenant-specific portal admin-consent URL and post-consent smoke command without mutating Azure resources. |
| 2026-07-10 | Compliance posture local gate | Codex | Pass locally | Added `GET /api/v1/compliance/posture` and Audit page posture cards for retention, consent, SLA, data residency, and provider posture. Focused API tests passed `18/18`; portal `tsc --noEmit` passed; portal `pnpm build` passed after clearing stale generated `.next`. |
| 2026-07-10 | Notification resilience local gate | Codex | Pass locally | Invalid/unconfirmed Teams notification deliveries persist `Failed` and fail into Service Bus retry/dead-letter handling; confirmed deliveries persist `Delivered`. Deployable hosts no longer activate the simulated `GraphTeams` stub or fall back to a mock Teams provider, and the stub source was removed. Focused tests passed `13/13`; full solution build passed with `0` errors and existing obsolete LocalTest guard warnings; full backend tests passed `364/364`. Live Service Bus/webhook delivery remains provider-gated. |
| 2026-07-10 | Functions hosting/binding local gate | Codex | Ready, not deployed | Focused tests passed `17/17`, including protected-runtime fail-closed coverage; Bicep compile and Release Functions publish passed; generated metadata uses the configured queue and identity connection; Azure what-if against `rg-iip-dev` succeeded with the expected Function App and three RBAC creates and no deletes. Full backend tests passed `369/369`. Read-only inspection returned no existing Function App, so live deployment, SQL contained-user setup, artifact deployment, and Service Bus/provider smoke remain open. |
| 2026-07-10 | Notification test-double removal | Codex | Pass locally | Removed deployable mock Teams providers and in-memory Teams notification persistence. Mock notification mode now resolves to `Disabled`; real persistence remains EF/Azure SQL. Affected suites passed `62/62`. |
| 2026-07-10 | Integration-provider test-double isolation | Codex | Pass locally | Synthetic email ingestion, attachment, communication-send, and webhook providers now exist only in `InsuranceIntelligence.LocalTestDoubles`. Deployable mock/unconfigured modes resolve to disabled providers; focused Phase 6/7 tests passed `62/62`. |
| 2026-07-10 | Deployable persistence zero-simulation gate | Codex | Pass locally | Deployable Infrastructure now accepts only SQL-backed repository composition, does not register in-memory support repositories, and no longer contains or invokes `DemoDataSeeder`. Release Functions publish passed without warnings; solution build passed with `0` warnings and `0` errors; backend tests passed `368/368` (`102` Domain, `266` API). |
| 2026-07-10 | Portal provider truth and responsive-shell gate | Codex | Pass locally | Added the canonical `pnpm test` gate and behavioral provider-status coverage; `Not configured`, degraded, unavailable, and unknown states fail closed while parity fixtures are disabled in production builds. Fixed mobile sidebar clipping and persisted-state hydration mismatch. `pnpm test`, `tsc --noEmit`, `pnpm lint`, clean-cache `pnpm build`, focused mobile Playwright, in-app browser desktop/mobile QA, and the full visual matrix passed (`74` passed, `31` expected Development skips). |
| 2026-07-10 | Performance smoke harness | Codex | Harness pass; live run open | Added a fail-closed Node performance runner with no demo authority headers. Loopback self-test passed readiness, authenticated claims list, and claim detail at 20 requests / concurrency 5 with zero errors; p95 was 97/27/40 ms against 300/800/1200 ms harness targets. Remote execution was not attempted and remains gated by explicit approval, Entra consent/token availability, synthetic records, and staging/provider capability. |
| 2026-07-10 | Application Insights host wiring | Codex | Pass live for API and Functions host telemetry | Wired API and isolated Functions telemetry to the IaC-provided Application Insights connection, retained Information-level safe operational events, and removed the legacy instrumentation-key setting from engineering Bicep. Dev action group `ag-iip-dev-h5girbnzrayqe`, API 5xx, Functions 5xx, and Service Bus dead-letter alerts are live with `cloud@oticgroup.net` as the enabled common-schema operations receiver; Azure reported the email test notification as succeeded on 2026-07-13. Authenticated business-operation telemetry remains part of authenticated UAT. |
| 2026-07-13 | Portal tenant admin consent | Codex / tenant administrator | Pass | Microsoft Graph shows an `AllPrincipals` delegated permission grant from the `iip-portal-stg` service principal to the `iip-api-stg` service principal for `access_as_user`. Azure CLI token acquisition still returns `AADSTS65001` because Microsoft Azure CLI is a separate client and was not granted this permission. |
| 2026-07-13 | API live telemetry proof | Codex | Pass for API request telemetry | API deployment `d8b503af-ed29-445e-9649-2d89e4a6c0a2` completed `RuntimeSuccessful`. Application Insights ingested uniquely marked `200`, `401`, and `404` requests with durations, operation IDs, and API cloud role. After removing `APPINSIGHTS_INSTRUMENTATIONKEY`, readiness remained `200` and a second marked request was ingested through `APPLICATIONINSIGHTS_CONNECTION_STRING` alone. The post-deployment query window contained `23` requests and `0` exceptions. Authenticated operational-event traces remain open. |
| 2026-07-13 | Functions runtime and live telemetry proof | Codex | Pass for host and trigger readiness | `func-iip-dev-h5girbnzrayqe` is deployed with managed identity, least-privilege SQL roles (`db_datareader`, `db_datawriter`), Service Bus Data Receiver, Storage Blob Data Owner, and the Functions 5xx alert. Corrected deployment `f5556fd2-4d60-4327-bc24-2703139f4723` loads enabled function `TeamsNotificationDispatch`; host HTTP is `200`. Post-fix Application Insights evidence shows two host starts, two function-discovery events, zero indexing errors, and zero exceptions. The queue is active with zero active/scheduled/dead-letter messages; delivery was not exercised because notifications remain `Disabled`. |
| 2026-07-13 | Authenticated live UAT | Codex / tenant administrator | Core identity smoke passed | Azure CLI delegated consent completed for the API scope. The current user's `oid` was added idempotently to the existing dev `dcb0b132-fb9e-475c-a68e-b0671222e8d8` / `workspace-operations` Admin scope without replacing the existing principal. `Invoke-IipLiveUatSmoke.ps1 -RequireAuthenticated` passed readiness `200`, anonymous `/api/v1/me` denial `401`, portal `/app` `200`, and authenticated `/api/v1/me` `200` with server-resolved context. Full role/provider scenarios remain open. |
| 2026-07-13 | Production data-residency technical control | Codex | Technical gate pass; approval open | Production/reviewer startup now fails on missing decision metadata or an Azure region outside the approved list. Compliance posture separates tenant address from Azure residency; IaC defaults storage to `Standard_LRS` and requires explicit replication choice. Focused backend tests passed `56/56`; Bicep compiled with the existing nullable-module warning. No jurisdiction was selected and no production resources were created. |
| 2026-07-13 | Approved live dev performance smoke | Repository owner / Codex | Approval resolved; latency gate still failed | Readiness and authenticated claims runs completed with zero errors. A request-scoped permission optimization reduced observed SQL dependencies from about seven to exactly four per claims request; full backend tests passed `376/376`; dev deployment `b0b8d3d3-1bf4-4c22-976a-466d17516c41` succeeded. The warm repeat still measured client claims p95 `1,336 ms` against `800 ms`; sampled server p95 was `321.0767 ms` with a `5,525.1093 ms` outlier. Evidence is in the approval document and three authenticated JSON reports. Staging/pilot sign-off remains open. |
| 2026-08-04 | RF-09 smoke runner evidence refresh | GPT-5.4 | Partial pass | `Invoke-IipLiveUatSmoke.ps1 -SkipAzToken -TimeoutSeconds 20 -OutputPath artifacts/rf09/uat-smoke-blocked.json` wrote machine-readable evidence for readiness `200`, anonymous `/me` denial `401`, portal `/app` route `200`, and an honest authenticated block when no token was supplied. `Invoke-IipPerformanceSmoke.mjs --self-test --include-provider-health --include-compliance --output artifacts/rf09/perf-self-test.json` passed all representative self-test scenarios. Full authenticated role-matrix and staging/provider runs remain open. |
| 2026-08-07 | Release 1 journey/flow/interaction specification validator | Codex | Pass locally | `validate-release1-journey-docs.mjs` passed: 12 governed artifacts, 80 requirements/RTM rows, 10 flows, 20 critical interactions with purpose/authority, surface/control, and action-recovery coverage; 20 independently changing regions; 10 required recovery states; 7 coordinated surfaces; 22 acceptance criteria; 20 UAT IDs; 18 stable state codes; 13 current permissions; 30 current audit actions; 9 permission/audit, 8 surface/control, and 8 recovery parity gaps. |
| 2026-08-08 | Release 1 interaction accessibility diagnostic and runtime-parity reconciliation | Codex | Diagnostic completed; remediation required | Desktop and mobile-emulated Chromium local fixture runs completed 6/6 diagnostic tests. Found failed governed-dialog focus entry/containment/return, critical accessible-name/label violations, serious contrast violations, many sub-44px targets, and incomplete reduced-motion/status behavior. No manual screen reader, physical touch, forced-colors, zoom/reflow, live provider, protected identity, or staging evidence; see `release-1-interaction-accessibility-and-runtime-parity-verification-2026-08-08.md`. |
| 2026-08-10 | Interaction contracts to requirements/acceptance/permission/audit traceability | Codex | Source validator passed; runtime evidence unchanged | Added 11 atomic baseline requirements, 10 measurable gates, 4 UAT scenarios, and one 20-row interaction RTM. Validator checks 91 requirements/RTM rows, 20/20 interactions, 34 acceptance criteria, 24 UAT IDs, 14 current permissions, 39 current audit actions, and 10 permission/audit gaps. This is source traceability, not remediation, accessibility conformance, protected UAT, or practitioner validation. |
| 2026-08-07 | Release 1 journey/flow affected runtime gates | Codex | Pass locally; protected blocked | Selected backend invariants/contracts passed `135/135`; portal `pnpm test`, lint, and production build passed; focused desktop Claims/Documents/Approvals browser tests passed `5`, skipped `3` local-profile `@deployed` assertions. See `release-1-journey-flow-validation-2026-08-07.md`; protected multi-role/provider/UAT and practitioner evidence remain open. |
| 2026-09-02 | Release 1 UX release-gate closure | Codex | Source-complete; pass locally | Portal unit/guard, lint, no-emit TypeScript and production build passed. Full responsive Playwright passed `149` and skipped `49` profile-inapplicable checks; focused release gates passed `63` and skipped `18`; deployed-profile DOM passed `3` and skipped `3` documented unmapped/customer or viewport-inapplicable checks. Backend Release build passed with `0` warnings/errors and `649/649` tests; both authoritative Bicep files compiled with only the existing CLI-version warning. CI, protected authenticated role-matrix UAT, live providers, practicing Ugandan insurance-staff usability, physical-device/assistive-technology checks, staging and production-candidate approval remain open. |
| 2026-09-02 | Live Computer Use Release 1 walkthrough | Codex | Partial pass; runtime-backed paths open | Interacted with the running portal across Command Center, Claims, claim review, extraction, My Work, Conversations and compact navigation. API-offline states stayed bounded, preserved filters/safe selections, showed `Not configured`/unverified authority, disabled Command Center send, and exposed retry/return routes. Drawer focus entered Close, Escape closed it, and focus returned to Open navigation. The walkthrough found and fixed raw extraction `Failed to fetch` copy; live retest showed safe connection guidance plus Try again. Role preview, populated current/historical evidence, provider-backed work and authenticated actions could not be exercised because no approved local test host/runtime was available; those remain automated-fixture or protected-UAT gates. |
