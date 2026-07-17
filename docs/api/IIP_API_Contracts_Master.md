# IIP API Contracts Master Specification

**Product:** Insurance Intelligence Platform (IIP)
**Staff app:** IIP Operations
**Document type:** API contracts, endpoint→UI→permission map, DTO/provider/error rules, contract governance
**Path:** `docs/api/IIP_API_Contracts_Master.md`
**Status:** Reconciled to the live codebase (Gate 0)
**Last updated:** 2026-07-06

> **How this document was reconciled.** This master spec adopts the framework of the
> team's draft API contract specification, but every concrete detail has been
> **verified against the real backend** (`src/InsuranceIntelligence.Api/Program.cs`,
> the `Permission` enum, and the generated `docs/api/openapi.json`). Where the draft
> conflicted with the implementation, the implementation wins and the delta is recorded
> in §3. A contract map that does not match the code is worse than none.

---

## 1. Purpose

Single source of truth for IIP API contract behaviour and frontend/backend traceability:
contract principles, the real endpoint→UI→permission map, response/error/DTO rules,
provider-state and `Not configured` behaviour, sensitive-data rules, the human-reviewed
AI boundary, contract-test requirements, and change governance. It exists to stop any
agent from guessing contracts, inventing data, bypassing authorization, exposing sensitive
data, or building screens not backed by real APIs.

## 2. Contract philosophy

IIP is a human-reviewed insurance intelligence platform: **AI prepares work; humans make
regulated decisions.** Contracts must support real operational data (no fake dashboards),
server-side authorization (never browser-owned), tenant/workspace isolation, evidence-first
AI outputs, honest provider states, safe error handling, auditability, and machine-checkable
traceability via OpenAPI.

## 3. Reconciliation deltas vs the draft spec (authoritative decisions)

| # | Area | Draft said | Reality (this doc) | Decision |
|---|------|-----------|--------------------|----------|
| 1 | Route prefix | `/api/…` canonical | **`/api/v1/…`** on all ~90 routes | `/api/v1` ratified in **ADR-0004**. |
| 2 | Response envelope | `ApiResponse<T>`/`PagedApiResponse<T>`/`ApiErrorResponse` | **Bare DTOs/arrays**; errors via ASP.NET `ProblemDetails`/`ValidationProblem` | Document reality now; a wrapping envelope is a **future ADR**, not a silent change. |
| 3 | Permissions | dotted strings (`claims.read`) | **PascalCase `Permission` enum** (`ViewAllClaims`, `ViewCommandCenter`, …) | Keep the enum (wired into `RolePermissionMap` + endpoint filters + tests); map the doc to it. |
| 4 | Endpoints | several non-existent (`/api/me`, `/command-center/briefing`, `/claims/sla-risks`, `/approvals/queue`, `/documents/upload`, `/reports/overview`, `/admin/settings`, `/api/search`) | real routes in §7 | Use §7; the generated `openapi.json` is the machine truth. |
| 5 | Repo paths | `docs/ui/…`, `docs/security/…`, `web/portal/src/lib/…` | those `docs/ui`/`docs/security` paths were never created; portal has no `src/` (`web/portal/lib/…`) | Use real paths. |

## 4. Route versioning

`/api/v1/…` is canonical for all business endpoints (see **ADR-0004**). `/health`,
`/health/live`, `/health/ready` are intentionally un-versioned infrastructure probes.
A future `/api/v2` requires a new ADR (deprecation window, client migration, dual export).

## 5. Runtime & environment contract

| Runtime profile | Real providers | Test doubles | Synthetic data | Banner |
|---|---|---|---|---|
| `LocalTest` / `Development` | partial | allowed | allowed (local) | Local Development |
| `Demo` | **required** | forbidden | seeded into isolated `tenant-demo` | Synthetic demo data |
| `Staging` / `Production` / `ManagedApplicationReviewer` | **required** | forbidden | **forbidden** (boot refused) | none (Production) |

Enforced at startup by `AzureProviderStartupValidator`. Forbidden in deployed profiles:
`Mock, Simulated, Placeholder, InMemory, SQLite, DevelopmentAuth, SeededDemoBusinessData
(outside Demo), FallbackToMock`. Demo/test diagnostic endpoints
(`debug-teams-notification`, `email/test-ingestion`) are **only mapped in Development/LocalTest**.

## 6. Global contracts

### 6.1 Request context (resolved server-side)
Authenticated identity (Entra JWT outside local), internal user identity, tenant context,
workspace context (where applicable), permission set, correlation id. Browser-supplied
`X-Tenant-Id` / `X-Workspace-Id` / `x-demo-role` / `x-demo-user-id` / `X-User-Id` are a
**Development-only** affordance; `RuntimeHeaderGuard` rejects them outside Development.

### 6.2 Responses (reality)
Endpoints return **bare DTOs / arrays** (e.g. `GET /api/v1/documents` → `DocumentDto[]`).
There is currently **no `{data, meta}` envelope**. Errors use ASP.NET Core `ProblemDetails`
(`ValidationProblem` for 400s; status codes for 401/403/404/409/500). Adopting a uniform
envelope is a tracked future ADR (delta §3.2).

### 6.3 Safe-error rule
Error responses must never include stack traces, connection strings, tokens/keys/SAS,
raw provider exception payloads, hidden prompts/system messages, model/tool traces, or
another tenant's data.

## 7. Endpoint → permission map (verified, 90 routes)

Generated from `Program.cs`. "_service-layer enforced_" means authorization is enforced in
the Application service (throws `PermissionDeniedException`, handler returns 403) rather than
by an endpoint filter — both are valid. The 5 filter-less routes are intentional: public
health probes, the self-scoped `/me`, and the payload-validated Graph webhook.

| Method | Route | Permission |
|---|---|---|
<!-- BEGIN GENERATED ROUTE TABLE (regenerate via the script in §12) -->
| GET | `/health` | _public probe_ |
| GET | `/health/live` | _public probe_ |
| GET | `/health/ready` | _public probe_ |
| GET | `/api/v1/me` | _self / auth-only_ |
| GET | `/api/v1/client-portal/claims/{caseId:guid}` | ViewClientPortal |
| POST | `/api/v1/client-portal/claims/{caseId:guid}/documents` | ViewClientPortal |
| POST | `/api/v1/cases` | CreateClaim |
| GET | `/api/v1/cases` | ViewAssignedClaims / ViewAllClaims |
| GET | `/api/v1/cases/{caseId:guid}` | ViewAssignedClaims / ViewAllClaims |
| GET | `/api/v1/claims` | ViewAssignedClaims / ViewAllClaims |
| GET | `/api/v1/claims/attention` | ViewAssignedClaims / ViewAllClaims |
| GET | `/api/v1/claims/daily-summary` | ViewCommandCenter |
| GET | `/api/v1/claims/{claimId:guid}` | ViewAssignedClaims / ViewAllClaims |
| GET | `/api/v1/claims/{claimId:guid}/draft-context` | ViewAiOutputs |
| POST | `/api/v1/cases/{caseId:guid}/documents` | UploadDocument |
| GET | `/api/v1/cases/{caseId:guid}/documents` | ViewAssignedClaims / ViewAllClaims |
| GET | `/api/v1/cases/{caseId:guid}/document-checklist` | ViewAssignedClaims / ViewAllClaims |
| GET | `/api/v1/cases/{caseId:guid}/documents/{documentId:guid}/content` | ViewAssignedClaims / ViewAllClaims |
| POST | `/api/v1/cases/{caseId:guid}/documents/{documentId:guid}/reclassify` | UploadDocument |
| POST | `/api/v1/cases/{caseId:guid}/documents/{documentId:guid}/approve-extraction` | UploadDocument |
| POST | `/api/v1/cases/{caseId:guid}/documents/{documentId:guid}/correct-field` | UploadDocument |
| POST | `/api/v1/knowledge-documents` | ManageKnowledgeBase |
| GET | `/api/v1/knowledge-documents` | ManageKnowledgeBase |
| GET | `/api/v1/knowledge-documents/{knowledgeDocId:guid}` | ManageKnowledgeBase |
| POST | `/api/v1/knowledge-documents/{knowledgeDocId:guid}/approve` | ManageKnowledgeBase |
| POST | `/api/v1/knowledge-documents/{knowledgeDocId:guid}/archive` | ManageKnowledgeBase |
| POST | `/api/v1/knowledge-documents/reindex` | ManageKnowledgeBase |
| POST | `/api/v1/assistant/policy-questions` | ViewAiOutputs |
| GET | `/api/v1/assistant/actions` | ViewAiOutputs |
| GET | `/api/v1/assistant/actions/{caseId:guid}` | ViewAiOutputs |
| GET | `/api/v1/command-center/metrics` | ViewCommandCenter |
| POST | `/api/v1/command-center/query` | ViewCommandCenter |
| GET | `/api/v1/provider-status` | ViewCommandCenter |
| GET | `/api/v1/system/capabilities` | ViewCommandCenter |
| GET | `/api/v1/cases/{caseId:guid}/timeline` | ViewAssignedClaims / ViewAllClaims |
| GET | `/api/v1/cases/{caseId:guid}/recommended-actions` | ViewAssignedClaims / ViewAllClaims |
| POST | `/api/v1/cases/{caseId:guid}/workflow/evaluate` | ApproveSettlementWorkflowStep |
| GET | `/api/v1/workflow/tasks` | ViewAssignedClaims / ViewAllClaims |
| GET | `/api/v1/workflow/tasks/{taskId:guid}` | ViewAssignedClaims / ViewAllClaims |
| POST | `/api/v1/workflow/tasks/{taskId:guid}/complete` | ViewAssignedClaims / ViewAllClaims |
| POST | `/api/v1/workflow/tasks/{taskId:guid}/assign` | ViewAssignedClaims / ViewAllClaims |
| GET | `/api/v1/approvals` | ApproveSettlementWorkflowStep |
| GET | `/api/v1/approvals/{approvalId:guid}` | ApproveSettlementWorkflowStep |
| POST | `/api/v1/cases/{caseId:guid}/approvals` | ViewAssignedClaims / ViewAllClaims |
| POST | `/api/v1/approvals/{approvalId:guid}/approve` | ApproveSettlementWorkflowStep |
| POST | `/api/v1/approvals/{approvalId:guid}/reject` | ApproveSettlementWorkflowStep |
| POST | `/api/v1/approvals/{approvalId:guid}/override` | OverrideAiRecommendation |
| POST | `/api/v1/approvals/{approvalId:guid}/escalate` | ApproveSettlementWorkflowStep |
| POST | `/api/v1/cases/{caseId:guid}/customer-updates/draft` | ManageCustomerCommunications |
| GET | `/api/v1/cases/{caseId:guid}/customer-updates` | ViewAssignedClaims |
| POST | `/api/v1/customer-updates/{updateId:guid}/approve` | ManageCustomerCommunications |
| POST | `/api/v1/customer-updates/{updateId:guid}/reject` | ManageCustomerCommunications |
| POST | `/api/v1/customer-updates/{updateId:guid}/send` | SendCustomerUpdate |
| GET | `/api/v1/reports/operations-summary` | ViewCommandCenter |
| GET | `/api/v1/admin/tenant-configuration` | ManageTenantSettings |
| PUT | `/api/v1/admin/tenant-configuration` | ManageTenantSettings |
| GET | `/api/v1/admin/provider-modes` | ManageTenantSettings |
| GET | `/api/v1/admin/provider-health` | ManageTenantSettings |
| GET | `/api/v1/admin/roles` | ManageTenantSettings |
| GET | `/api/v1/admin/users` | ManageTenantSettings |
| POST | `/api/v1/admin/users` | ManageTenantSettings |
| PUT | `/api/v1/admin/users/{userId}` | ManageTenantSettings |
| PUT | `/api/v1/admin/users/{userId}/role` | ManageTenantSettings |
| POST | `/api/v1/admin/debug-teams-notification` | ManageTenantSettings |
| GET | `/api/v1/channels/overview` | ViewChannels |
| GET | `/api/v1/customers` | ViewCustomers |
| GET | `/api/v1/customers/{customerId}` | ViewCustomers |
| GET | `/api/v1/sales-leads` | ViewSalesLeads |
| GET | `/api/v1/sales-leads/{leadId}` | ViewSalesLeads |
| POST | `/api/v1/sales-leads` | ManageSalesLeads |
| POST | `/api/v1/onboarding` | ManageTenantOnboarding |
| GET | `/api/v1/onboarding` | ManageTenantOnboarding |
| GET | `/api/v1/onboarding/{onboardingId}` | ManageTenantOnboarding |
| GET | `/api/v1/onboarding/{onboardingId}/consent-url` | ManageTenantOnboarding |
| GET | `/onboarding/consent-callback` | _admin-consent redirect: state-validated_ |
| GET | `/api/v1/audit/events` | ViewAuditLog |
| GET | `/api/v1/fraud/signals` | ViewFraudSignals |
| GET | `/api/v1/underwriting/reviews` | ViewUnderwriting |
| GET | `/api/v1/admin/agents` | ManageAgents |
| POST | `/api/v1/admin/agents/{agentKey}/kill-switch` | ManageAgents |
| GET | `/api/v1/admin/billing` | ViewBilling |
| GET | `/api/v1/documents` | ViewAllClaims |
| GET | `/api/v1/compliance/audit-events` | ViewAuditLog |
| GET | `/api/v1/compliance/cases/{caseId:guid}/audit-pack` | ViewAuditLog |
| GET | `/api/v1/integrations/email/health` | ConfigureIntegrations |
| GET | `/api/v1/integrations/email/mailboxes` | ConfigureIntegrations |
| POST | `/api/v1/integrations/email/mailboxes` | ConfigureIntegrations |
| POST | `/api/v1/integrations/email/test-ingestion` | ConfigureIntegrations |
| POST | `/api/v1/integrations/email/process-message` | ConfigureIntegrations |
| GET | `/api/v1/integrations/email/messages` | ConfigureIntegrations |
| GET | `/api/v1/integrations/email/messages/{messageId:guid}` | ConfigureIntegrations |
| POST | `/api/v1/integrations/email/graph/webhook` | _webhook: payload-validated_ |
| POST | `/api/v1/communications/send-email` | SendCustomerUpdate |
| POST | `/api/v1/communications/send-sms` | SendCustomerUpdate |
| POST | `/api/v1/communications/send-teams-notification` | ConfigureIntegrations |
| GET | `/api/v1/communications/history` | ViewAssignedClaims / ViewAllClaims |
| GET | `/api/v1/cases/{caseId:guid}/communications` | ViewAssignedClaims / ViewAllClaims |
| GET | `/api/v1/agent-workflows` | ViewAgentWorkflows |
| POST | `/api/v1/agent-workflows` | ManageAgentWorkflows |
| GET | `/api/v1/agent-workflows/{workflowKey}` | ViewAgentWorkflows |
| POST | `/api/v1/agent-workflows/{workflowKey}/publish` | ManageAgentWorkflows |
| POST | `/api/v1/agent-workflows/{workflowKey}/runs` | RunAgentWorkflows |
| GET | `/api/v1/agent-workflow-runs` | ViewAgentWorkflows |
| GET | `/api/v1/agent-workflow-runs/{runId:guid}` | ViewAgentWorkflows |
| POST | `/api/v1/agent-workflow-runs/{runId:guid}/nodes/{nodeKey}/complete` | ManageAgentWorkflows |
| GET | `/api/v1/agent-work-items` | ReviewAgentWorkItems |
| GET | `/api/v1/agent-work-items/{workItemId:guid}` | ReviewAgentWorkItems |
| POST | `/api/v1/agent-work-items/{workItemId:guid}/decision` | ReviewAgentWorkItems |
<!-- END GENERATED ROUTE TABLE -->

## 8. Screen → API map (IIP Operations `/app/*`)

Real portal routes (`web/portal/lovable-pages/*` in the `components/iip/AppShell` shell).
Reconciliation hooks live in `web/portal/lib/lovable-parity/data/*`.

| Screen | UI route | Primary endpoint(s) | Permission | Status |
|---|---|---|---|---|
| Command Center | `/app` | `GET /api/v1/command-center/metrics`, `POST /api/v1/command-center/query` | `ViewCommandCenter` | real chat-first structured workspace; honest provider/empty states |
| Claims Inbox | `/app/claims` | `GET /api/v1/claims`, `/claims/attention` | `ViewAssignedClaims`/`ViewAllClaims` | real |
| Claim detail / review | `/app/claims/[id]` | `GET /api/v1/claims/{id}`, `/cases/{id}/timeline`, `/documents`, `/approvals` | as per route | real governed claim workspace |
| Documents | `/app/documents` | `GET /api/v1/documents` | `ViewAllClaims` | real |
| Document extraction review | `/app/claims/[id]/extraction` | `GET /api/v1/cases/{id}/documents`, tenant-scoped content, correct-field, approve-extraction | `ViewAssignedClaims`/`ViewAllClaims`, `UploadDocument` for changes | real source preview, field verification, audit reason, and human approval |
| Review & Approvals | `/app/work-items` | `GET /api/v1/agent-work-items`, decision endpoint; `GET /api/v1/approvals`, approve/reject/escalate/override/execute endpoints | `ReviewAgentWorkItems`, `ApproveSettlementWorkflowStep`, `OverrideAiRecommendation` | one human review workspace; prepared artifacts and consequential approvals remain explicitly separated |
| Channels | `/app/channels` | `GET /api/v1/channels/overview` | `ViewChannels` | real read; write-actions honest-gated (no ingestion source yet) |
| Customers / Customer 360 | `/app/customers`, `/app/customers/[id]` | `GET /api/v1/customers`, `/customers/{id}` | `ViewCustomers` | real |
| Acquisition / Sales-Leads | `/app/acquisition` | `GET /api/v1/sales-leads/{leadId}` | `ViewSalesLeads` | honest-empty; manual create pending (B3) |
| Communications ledger | `/app/communications` | `GET /api/v1/communications` | `ManageCustomerCommunications` | read-only operational ledger; governed claim/channel workflows own actions |
| Audit | `/app/audit` | `GET /api/v1/audit/events` | `ViewAuditLog` | real |
| Operations Intelligence | `/app/executive` | `GET /api/v1/reports/operations-summary` | `ViewCommandCenter` | real management operations aggregates; unavailable financial measures are omitted |
| Risk & Fraud Signals | `/app/fraud` | `GET /api/v1/fraud/signals` | `ViewFraudSignals` | advisory signals only; no autonomous accusation |
| Underwriting Intelligence | `/app/underwriting` | `GET /api/v1/underwriting/reviews` | `ViewUnderwriting` | advisory/read-only until governed submission actions exist |
| Knowledge Hub | `/app/knowledge` | `GET /api/v1/knowledge-documents` | `ManageKnowledgeBase` | real read surface; write controls remain hidden until sensitivity and governance reason are persisted |
| Policy Intelligence | `/app/policy` | `POST /api/v1/assistant/policy-questions` | `ViewAiOutputs` | grounded Q&A pending Gate 3 providers (honest-gated) |
| Integrations & Health | `/app/integrations` | `GET /api/v1/admin/provider-health`, `/api/v1/integrations/email/health`, `/api/v1/system/capabilities` | `ManageTenantSettings`/`ConfigureIntegrations` | one real provider/capability control plane; no fake connection tests, queue metrics, or support tickets |
| Admin console | `/app/admin` | tenant configuration, users, agents, onboarding, billing endpoints | per admin route | role-filtered platform administration launchpad |
| Admin · Agents | `/app/admin/agents` | `GET /api/v1/admin/agents`, kill-switch `POST` | `ManageAgents` | real (persisted kill switch) |
| Admin · Billing | `/app/admin/billing` | `GET /api/v1/admin/billing` | `ViewBilling` | real usage meters |
| Admin · Tenant policy | `/app/admin/tenant-policy` | tenant agent capability policy endpoints | `ManageTenantSettings` | real structured policy bounded by the platform ceiling |
| Admin · Tenant onboarding | `/app/admin/onboarding` | `/api/v1/onboarding`, consent-url endpoint | `ManageTenantSettings` | real Entra/subscription onboarding boundary |
| Automation Studio | `/app/flows`, `/app/flows/new`, `/app/flows/{key}` | `GET/POST /api/v1/agent-workflows`, publish/start endpoints | View/Manage/RunAgentWorkflows | real definition catalog and durable control plane; production execution worker not yet enabled |
| Agent Run Debugger | `/app/runs/{id}` | `GET /api/v1/agent-workflow-runs/{id}` | ViewAgentWorkflows | bounded run trace and evidence references; raw prompts/OCR are intentionally excluded |

Compatibility routes are not separate product surfaces: `/app/workflows` opens the approval tab inside Review & Approvals, `/app/support` redirects to Integrations & Health, and `/app/settings` redirects to the Admin console. They are retained only to avoid breaking existing bookmarks.

> **Out of scope (PRD):** a customer login portal. Per `iip_product_requirements_document.md`
> ("not customer portal login") and `iip_mvp_scope_control_register.md` ("Customer login portal …
> Future self-service portal if required"), policyholders interact via **channels**, not a login.
> The `client-portal` API endpoints exist for channel-assisted claim view / document upload
> (link-based), not a standalone portal app.

## 9. Provider-state contract

States: `configured | not_configured | healthy | degraded | unavailable | error`. Provider-backed
capabilities that are unconfigured must return an honest `Not configured` state — never a fabricated
success. `GET /api/v1/admin/provider-health` and `/api/v1/system/capabilities` expose normalized state.

## 10. Permission reference (real enum)

`ViewCommandCenter, CreateClaim, UploadDocument, ViewAllClaims, ViewAssignedClaims,
ApproveSettlementWorkflowStep, ViewAuditLog, ManageKnowledgeBase, ConfigureIntegrations,
ViewAiOutputs, OverrideAiRecommendation, ManageCustomerCommunications, SendCustomerUpdate,
ManageTenantSettings, ViewClientPortal, ViewChannels, ViewCustomers, ViewSalesLeads,
ViewFraudSignals, ViewUnderwriting, ManageAgents, ViewBilling`
(source: `src/InsuranceIntelligence.Domain/Administration/Permission.cs`; role grants in
`RolePermissionMap.cs`).

## 11. Sensitive data, tenant isolation & human-review

- **Never returned to the UI:** tokens/keys/secrets/connection/SAS, raw prompts/system
  messages, model/tool traces, provider exception payloads, other tenants' data, internal
  fraud/reserve/legal notes unless the role and surface are authorized.
- **Tenant/workspace:** every tenant-scoped query filters server-side; the browser is not a
  trusted authority in deployed runtimes; cross-tenant/workspace access tests exist.
- **AI boundary:** AI may summarize/extract/classify/draft/recommend/route/surface-risk.
  AI must never approve/reject claims, bind policies, release payments, set final premium,
  accuse fraud, or make final underwriting/legal decisions. Use `risk_signal_detected`, never
  `fraud_confirmed`.

## 12. Generated artifacts & regeneration

- `docs/api/openapi.json` — generated from the running API (Swashbuckle). Swagger UI +
  `/swagger/v1/swagger.json` are exposed **Development/LocalTest only**.
- Regenerate the spec: run the API in Development (SqlServer LocalDB), then
  `curl http://127.0.0.1:<port>/swagger/v1/swagger.json -o docs/api/openapi.json`.
- Regenerate the §7 route table: parse `Program.cs` for `app.Map*` + `PermissionEndpointFilter`
  (script used to produce this table lives in the Gate 0 exit report).

## 13. Contract-test & CI requirements (target)

Required checks (Gate 0 target; not all wired into CI yet — see exit report): OpenAPI generates;
every protected endpoint has a permission filter or service-layer check; every `/app/*` route maps
to a real endpoint or a known honest state; empty results render empty (no fake fallback); missing
provider returns `Not configured`; unknown Command Center block renders safely; state-changing
actions write audit events; cross-tenant access fails; sensitive fields excluded; deployed profiles
reject mock/demo providers (enforced today by `AzureProviderStartupValidator`).

## 14. Change governance

Any change to a route, method, request/response DTO, enum, permission, auth/tenant/workspace
rule, provider dependency, error shape, sensitive-data exposure, or Command Center block type
requires updating this document and regenerating `openapi.json`. Architectural/breaking changes
require an ADR (e.g. the response-envelope adoption in §3.2, a future `/api/v2`).

## 15. Change log

| Date | Change |
|---|---|
| 2026-07-06 | Initial master spec, reconciled to the live codebase for Gate 0 (real `/api/v1` routes, real `Permission` enum, bare-DTO reality, verified 90-endpoint permission map, committed `openapi.json`). |
