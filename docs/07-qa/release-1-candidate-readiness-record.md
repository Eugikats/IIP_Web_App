# Release 1 Candidate Readiness Record

| Field | Value |
|---|---|
| Version | 1.1 |
| Date | 2026-09-02 |
| Status | Evidence-backed snapshot; production-candidate **NO** |
| Owner | Product / Engineering (Release) |
| Authority | `AGENTS.md`, `docs/08-program-plan/IIP-CONSOLIDATED-ROADMAP.md`, `docs/07-qa/rf10-release-foundation-go-no-go-evidence.md` |

This document consolidates the release-candidate commit, current implementation branch, infra-contract gap registry (including the requested `/api/infra-contracts/known-gaps` contract status), and approved motor scope. It does not replace owning requirement, scope, or go/no-go documents; it links to them with actual repository values.

---

## 1. Release candidate commit and branch

| Field | Value |
|---|---|
| **Current git branch** | `harry/rf-invalidate-downstream-after-corrections` |
| **Branch tip (HEAD)** | `e84e36cdfdcc15f6260aece802549860bc8eacc1` |
| **Branch tip date** | 2026-08-05 16:32:22 +0300 |
| **Branch tip message** | `docs: organize repository navigation` |
| **Remote tracking** | `origin/harry/rf-invalidate-downstream-after-corrections` — local branch **ahead by 2 commits** at record time |
| **RF-10 evidence pack commit** | `1f3308126f78280905df81fd84e15f6d9c1a0fdb` — `docs(rf-10): publish honest Release Foundation go/no-go evidence` (2026-08-04) |
| **RF-09 harness tip (at RF-10 publish)** | `7afa7c8` — `feat(rf-09): expand UAT and latency evidence harnesses` |
| **Merged `main` tip (remote)** | `ef846ae` — Merge pull request #24 (RF-07 merge governance) |

### Readiness verdict (unchanged from RF-10)

```text
RELEASE 1 DEV-GATE: NO
PRODUCTION-CANDIDATE: NO
MANAGED APPLICATION PACKAGE READY: NO
PARTNER CENTER PRIVATE PLAN READY: NO
```

**Interpretation:** The branch tip above is the current working release-foundation line, not an approved production-candidate freeze. RF-10 (commit `1f33081`) remains the authoritative go/no-go pack until re-issued with live Azure, Entra, UAT, and latency evidence.

---

## 2. Current route context

### 2.1 Git / delivery route

| Route | Value |
|---|---|
| Active implementation branch | `harry/rf-invalidate-downstream-after-corrections` |
| Parent program | Release Foundation Blocker Remediation (`IIP-RF-001`) |
| Merge governance | `docs/05-deployment-release-enablement/iip_merge_governance.md` (RF-07 compensating control) |
| Dev API host (last recorded smoke) | `https://app-api-dev-h5girbnzrayqe.azurewebsites.net` |
| Dev portal host (last recorded smoke) | `https://app-portal-dev-h5girbnzrayqe.azurewebsites.net` |

### 2.2 Requested API route: `/api/infra-contracts/known-gaps`

| Field | Value |
|---|---|
| **Implementation status** | **Not implemented** — no route, handler, test, or OpenAPI entry exists in the repository (verified 2026-08-14 by full-repo search for `infra-contracts`, `known-gaps`, `KnownGaps`, `InfraContract`). |
| **Registration location (if added)** | Expected in `src/InsuranceIntelligence.Api/Program.cs` alongside other diagnostic/readiness routes. |
| **Authoritative gap registry (today)** | This document (section 3) plus owning sources listed in section 3.4. |

### 2.3 Nearest implemented runtime routes (provider / infra posture)

These routes expose **live configuration health**, not a structured known-gaps contract with owners and acceptance evidence:

| Route | Handler | Permission filter | Response shape (summary) |
|---|---|---|---|
| `GET /api/v1/system/capabilities` | `SystemCapabilityEndpoints.GetCapabilities` | `ViewCommandCenter` | `SystemCapabilityStatusDto`: booleans are true only for live provider modes (for example AzureOpenAI, AzureDocumentIntelligence, Graph, AfricasTalking, IncomingWebhook, WhatsAppCloudApi). `NotConfigured`/`Disabled`/`Mock` remain inactive; plus `Environment`, `AuthMode`. |
| `GET /api/v1/provider-status` | `ProviderStatusEndpoints.GetProviderStatus` | `ViewCommandCenter` | Same as `BuildProviderHealth` — `ProviderHealthResponse(overall, items[])` with per-provider `Status` (`healthy` / `missing` / `not_configured` / …) and `Detail`. |
| `GET /api/v1/admin/provider-health` | `AdminEndpoints.GetProviderHealth` | `ManageTenantSettings` | Identical provider health payload as above. |
| `GET /health/ready` | `HealthEndpoints.Ready` | None | Readiness probe (SQL, config, provider startup validation). |

Provider health items currently emitted: `database`, `blobStorage`, `documentIntelligence`, `aiSearch`, `azureOpenAI`, `serviceBus`, `graphMail`, `communications`, `notifications` (+ `mockProviders` in Development auth mode only).

### 2.4 Release 1 staff portal routes (canonical)

Source: `docs/03-design-architecture/iip_information_architecture_navigation_and_screen_inventory.md` §6.

| Screen ID | Route | Release 1 role |
|---|---|---|
| IA-SCR-001 | `/app` | Supporting entry (Command Center) |
| IA-SCR-002 | `/app/work-items` | Required (My Work) |
| IA-SCR-003–006 | `/app/claims`, `/app/claims/new`, `/app/claims/[id]`, `/app/claims/[id]/extraction` | Primary motor workflow |
| IA-SCR-007 | `/app/channels` | Primary (WhatsApp mandatory) |
| IA-SCR-008–010 | `/app/customers`, `/app/customers/[id]`, `/app/documents` | Supporting |
| IA-SCR-011+ | Communications, knowledge, flows, audit, integrations, admin | Mixed supporting / conditional |

Compatibility aliases: `/app/workflows` → My Work; `/app/support` → `/app/integrations`; `/app/settings` → `/app/admin`.

---

## 3. Known gaps registry

Because `/api/infra-contracts/known-gaps` is absent, the table below is the **documentation authority** for infra, contract, and release gaps at this record date. Severity uses release impact, not engineering convenience.

**Severity key:** `Critical` = invariant or production-candidate blocker · `High` = staging/pilot blocker · `Medium` = credible pilot gap or contract debt · `Low` = deferred follow-up with honest `Not configured` / local-only state.

### 3.1 Release-foundation human-owned blockers (RF-10)

Source: `docs/07-qa/rf10-release-foundation-go-no-go-evidence.md` § Human-owned blockers.

| Gap ID | Description | Owner | Severity | Acceptance evidence required | Current evidence |
|---|---|---|---|---|---|
| RF-06-DEPLOY | Deployed agent-workflow execution worker with Application Insights / Log Analytics proof of one bounded Release 1 execution | Azure operators | **Critical** | Telemetry capture from approved env showing one real bounded R1 workflow execution | Code on branch (`AgentWorkflowExecutionWorker`, contract tests); **no deployed telemetry** |
| RF-08-SQL | Authorized Azure SQL migration window + applied-history / rollback record | DB / platform owners | **High** | Maintenance-window log, applied migration history, rollback procedure record | Not recorded |
| RF-08-DOCINT | Azure AI Document Intelligence region / capacity / residency proof | Azure / capacity owners | **High** | Confirmed deployment, region, quota; residency sign-off where required | Subscription capability blocks OpenAI/DI family on Sponsored sub (roadmap Phase 3.1) |
| RF-08-LIVE-DOC | Live upload → extraction → correction → re-evaluation on real providers | Staging operators | **Critical** | End-to-end protected-staging run with zero simulation | `PEND-T0-02` open; dev providers honestly `Not configured` |
| RF-09-UAT-AUTH | Authenticated multi-role Entra UAT tokens and retained matrix evidence | Identity / UAT leads | **High** | Role matrix + token-backed UAT runs per `docs/07-qa/uat-script.md` | `artifacts/rf09/uat-smoke-blocked.json`: authenticated `/me` **BLOCKED** (no token) |
| RF-09-PERF | Representative staging p95/p99 vs documented thresholds (no relaxation) | Perf / platform | **High** | AC-PERF-01 pass on representative authenticated load near deployment region | **Fail** recorded: dev claims list p95 1,277–1,336 ms vs ≤800 ms threshold (`iip_mvp_acceptance_criteria_register.md`) |
| RF-07-RESID | Native GitHub rulesets / required checks | Org admins | **Medium** | Branch protection with required CI jobs | Compensating merge governance doc only; private-repo plan limits native enforcement |
| RF-X-PAYG | Sponsored subscription / PAYG for non-dev latency and provider capability | Commercial / Azure billing | **High** | Capable subscription for Standard SKU, Azure OpenAI, staging/prod | PAYG deferred per roadmap Phase 3.4 decision (2026-07-07) |

### 3.2 Information-architecture and API contract gaps

Source: `docs/03-design-architecture/iip_information_architecture_navigation_and_screen_inventory.md` § Known contract gaps; screen inventory notes.

| Gap ID | Description | Owner | Severity | Acceptance evidence required | Current evidence |
|---|---|---|---|---|---|
| IA-GAP-01 | Policy and distribution endpoint groups rely on authentication without the same fine-grained permission filters as core routes | Architecture / API | **Medium** | Route inventory + permission matrix tests for `/api/v1` policy/distribution groups | Documented; route-wide proof pending (`AC-AUTH-01`) |
| IA-GAP-02 | Route-level visibility does not replace action-level rules on mixed-capability screens | Product / UX / Engineering | **Medium** | Action-level authorization tests on mixed screens | Documented |
| IA-GAP-03 | Target-insurer role labels and landing priorities unvalidated | Product / Insurance | **Medium** | Practitioner validation per IA exit gate | No participant study |
| IA-SCR-014 | `/app/policy` (Policy Guidance) mounted with fixture-shaped content; live contract gap | Engineering | **Low** | Live knowledge/policy contract or explicit `Not configured` | Fixture content flagged in IA inventory |
| IA-RECENT-WORK | Command Center recent-work / advanced search gaps | Engineering | **Medium** | Protected multi-role staging + AC-SEARCH-01 | Local API/portal tests pass; staging validation open |

### 3.3 Pending engineering / provider prove items (selected)

Source: `docs/research/iip-pending-work-register.md`.

| Gap ID | Description | Owner | Severity | Acceptance evidence required | Current evidence |
|---|---|---|---|---|---|
| PEND-T0-01 | Live Azure staging with real providers | Ops / Gate 3 | **Critical** | Protected staging env with configured providers | **OPEN / BLOCKED** |
| PEND-T0-02 | One real document extraction → human review e2e | Ops / QA | **Critical** | AC-DOC / G2 evidence on real provider | **OPEN / BLOCKED** on PEND-T0-01 |
| PEND-WA-01 | Live Meta WhatsApp Cloud API webhook + media download prove | Ops / Gate 3 | **High** | AC-WA-01 protected-staging + UAT journey | **OPEN / BLOCKED** on credentials |
| PEND-WA-03 | Outbound WhatsApp sending (beyond inbound path) | Infrastructure | **High** | Reviewed outbound send + delivery reconciliation per R1-WA-* | Inbound code exists; outbound **Not configured** |
| PEND-SMS-01 | Live Africa's Talking sandbox/prod send prove | Ops / Gate 3 | **Medium** | Staging SMS config + live send proof | **OPEN / BLOCKED** on credentials |

### 3.4 Owning documents (do not duplicate closure here)

| Topic | Owning document |
|---|---|
| Go/no-go verdict and decision matrix | `docs/07-qa/rf10-release-foundation-go-no-go-evidence.md` |
| Acceptance thresholds and gate status | `docs/01-strategy-requirements-roadmap/iip_mvp_acceptance_criteria_register.md` |
| Open decisions and gate summary | `docs/06-decisions/open-questions-tracker.md` → `iip_assumptions_constraints_open_questions_register.md` |
| Deferred prove backlog | `docs/research/iip-pending-work-register.md` |
| Release Foundation ticket map | `docs/08-program-plan/wayfinder/release-foundation-blockers/MAP.md` |
| UAT execution scripts | `docs/07-qa/uat-script.md` |
| Blocked UAT smoke artifact | `artifacts/rf09/uat-smoke-blocked.json` |

---

## 4. Approved motor scope

Motor scope is **proposed in baseline documents** and **selected in scope registers**; six-function stakeholder approval remains pending (G0).

### 4.1 Canonical definition

| Field | Value |
|---|---|
| **Release 1 wedge** | Non-injury motor comprehensive **own-damage** evidence review and approval preparation |
| **Primary authority** | `docs/01-strategy-requirements-roadmap/iip_product_definition_and_validation_charter.md` (§ Product frame) |
| **Normative requirement** | `R1-BUS-001` in `docs/01-strategy-requirements-roadmap/iip_release_1_requirements_baseline.md` |
| **Scope register** | `docs/01-strategy-requirements-roadmap/iip_mvp_scope_control_register.md` v2.1 |
| **Desk research** | `docs/research/uganda-release-1-claims-workflow-desk-research.md` |

### 4.2 Included claim types (Release 1)

| Claim type | Decision | Notes |
|---|---|---|
| Motor comprehensive own accidental damage | **Primary** | Core wedge; structured FNOL, evidence review, human approval preparation |
| Windscreen/glass and minor vehicle-part damage | **Primary test cases** | Low-complexity variants within same workflow |
| Motor third-party property damage | **Secondary** | Narrow extension; multi-party evidence |
| Motor bodily injury / fatality / COMESA | **Exception route / excluded** | Route out of happy path |
| Medical, group life/death, workers' comp, fire/agriculture/marine/engineering | **Explicitly excluded** | Later releases |

**Code-backed governed path (single path, claim-type discriminator):** Domain `ClaimRoutingPolicy` / `ClaimAuthorityPolicy` and Application `IClaimGovernedPathService` classify claim types onto one FNOL → intake → assignment → review → approval → communication → evidence-pack export path. Own-damage and third-party property use the happy path with standard human gates. Theft/total-loss and injury/fatality are exception routes requiring designated-human escalation before ReadyForExport; contested liability, fraud signals, settlement/payment, and repudiation likewise force designated-human escalation. AI/system/integration actors cannot finalize those consequential actions. Evidence-pack export requires `ReadyForExport`, a human actor, tenant scope, and audit reason.

### 4.3 Explicit exclusions (invariant)

From scope register and charter:

- Autonomous consequential insurance outcomes (AI must not approve/reject/repudiate/settle)
- Core policy/claim/payment system of record replacement
- Customer self-service portal as Release 1 dependency
- Sales/leads, full underwriting, fraud intelligence, billing as Release 1 requirements
- Bodily injury, fatality, medical, life, workers' compensation, contested-liability specialist workflows

### 4.4 Mandatory Release 1 capabilities within motor scope

| Capability | Requirement IDs | Status (summary) |
|---|---|---|
| Server-authorized staff entry | R1-FUN-001, R1-SEC-* | Implemented; protected multi-role validation pending |
| Structured motor FNOL + work queue | R1-FUN-002–004 | Implemented locally |
| Document intake + real-provider extraction | R1-FUN-005–006 | Conditional on provider; dev **Not configured** |
| Human review, correction, invalidation | R1-FUN-007, R1-FUN-009 | RF-05 remediation on branch |
| WhatsApp intake + reviewed follow-up | R1-WA-001–004 | Source complete locally; live provider blocked |
| Human approval + controlled communication | R1-FUN-010–014 | Implemented locally; live send provider-blocked |
| Audit / evidence export | R1-FUN-015 | Implemented locally |

### 4.5 Approval status

| Approval | Status |
|---|---|
| Product, Insurance, Operations, Security/Privacy, Architecture, Delivery (G0) | **Pending** — baseline and scope registers remain **Proposed** |
| Target-insurer configuration (rules, templates, languages, deployment) | **Conditional** — unconfigured capability stays `Not configured` |

### 4.6 UX release-gate evidence (2026-09-02)

The Release 1 staff UX remediation is source-complete and locally verified on `harry/ux-r1-release-gates`. The portal gates cover responsive workflows, role preview without authority escalation, evidence currentness, recovery and unknown-result handling, automated axe checks, keyboard/focus behavior, 200% text, 320/390/768/1024/1440 CSS-pixel layouts, and protected-profile DOM assertions. A live Computer Use walkthrough passed the running portal's API-offline recovery and compact-drawer paths and produced one extraction-recovery fix. Runtime-backed role preview, populated evidence and provider actions were unavailable and remain open for protected UAT. Exact local results are recorded in `docs/07-qa/uat-script.md`.

This evidence does **not** change the production-candidate verdict. CI and maintainer review are pending, and local fixtures/emulation do not replace protected authenticated multi-role UAT, live provider and low-bandwidth evidence, practicing Ugandan insurance-staff usability, physical-device or assistive-technology testing, staging validation, or the named go/no-go approvals.

---

## 5. Record maintenance

Update this document when:

1. A release-candidate commit is frozen (new HEAD SHA, branch, and verdict).
2. `/api/infra-contracts/known-gaps` is implemented — move section 3 to reference live API response and retain this doc as snapshot or redirect to OpenAPI.
3. RF-10 is re-issued with new evidence.
4. Motor scope changes through approved scope control (`iip_mvp_scope_control_register.md`).

**Last verified:** 2026-09-02 — Release 1 UX local gates added; production-candidate verdict and RF-10 authority unchanged.
