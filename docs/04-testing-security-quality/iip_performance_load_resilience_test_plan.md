---
Product: Insurance Intelligence Platform (IIP)
Document Name: Performance, Load & Resilience Test Plan
Version: 1.0
Status: Draft
Audience: Engineering, DevOps, QA, Support, Client IT
Date: 2026-06-30
---

# Performance, Load & Resilience Test Plan

## Common IIP Context Used

The Insurance Intelligence Platform (IIP) is an Azure-native, human-reviewed insurance operations platform for African insurers and related insurance organizations. It acts as an intelligent operational layer across staff, customers, core insurance systems, documents, channels, knowledge sources, and AI agents.

The current MVP direction is Motor Claims & Document Intelligence for a mid-to-large insurer pilot. Customers do not log into the internal staff app. Customer interactions arrive through WhatsApp-first channels and other supported channels, then feed internal insurer workspaces.

Confirmed implementation direction:

- Frontend: Next.js, React, TypeScript, Tailwind CSS.
- Backend: .NET 8 / ASP.NET Core, EF Core, Azure SQL.
- Azure services: Azure SQL, Blob Storage, Key Vault, Azure AI Search, Azure OpenAI, Azure AI Document Intelligence, Service Bus or Event Grid, Functions or Durable Functions, API Management, Application Insights, and Log Analytics.
- Identity: Microsoft Entra ID / MSAL / OAuth 2.0.
- Primary internal modules: Command Center, Claims, Documents, Channels, Customers, Sales & Leads, Knowledge Base, Approvals, Compliance & Audit, Reports & Analytics, Integrations, Admin Settings, Support & Health.
- Governance rule: AI prepares insurance work; humans make consequential decisions.

Non-negotiable AI boundary:

- AI may summarize, classify, extract, flag, draft, route, and recommend.
- AI must not approve or reject claims, bind policies, set final premiums, accuse customers of fraud, release payments, promise settlement, or make final regulated decisions.

## Runtime authorization boundary

Authenticated performance tests require a real bearer token for the target runtime. Tenant, workspace, role, and user authority must remain server-resolved from the validated token and membership store.

Rules:

- Do not send `X-Tenant-Id`, `X-Workspace-Id`, `X-User-Id`, `x-demo-role`, or other browser-owned authority headers.
- Generate a unique `X-Correlation-ID` for traceability without treating it as identity.
- Public readiness checks may run without a token; protected claims/document/AI scenarios must fail closed when a token is absent.
- Remote load execution requires explicit operator approval and must use synthetic or approved masked records only.
The repository owner approved and executed a bounded internal-dev readiness run on 2026-07-13: two runs of 50 requests at concurrency 5 against `GET /health/ready`. This approval does not extend to staging, production, authenticated routes, mutation routes, provider load, stress, or soak testing. See `docs/07-qa/live-performance-test-approval-2026-07-13.md`.

Later on 2026-07-13, after Entra token acquisition and server-resolved membership passed live UAT, the approved envelope was extended to one bounded authenticated `GET /api/v1/claims` run at 50 requests and concurrency 5. All requests succeeded, but client p95 was `1,277 ms` and Application Insights server p95 was `961.8373 ms`, both above the provisional `800 ms` target. This is execution evidence, not a staging or pilot performance pass.

A follow-up optimization reduced observed SQL calls from about seven to exactly four per claims request and deployed successfully to dev after `376/376` backend tests. The immediate warm repeat remained error-free but measured client claims p95 `1,336 ms`; sampled server p95 was `321.0767 ms` with a `5,525.1093 ms` outlier. The latency gate therefore remains failed despite lower database round trips and a better server median.

The executable smoke harness currently covers `/health/ready`, `/api/v1/claims`, and optional `/api/v1/claims/{id}`. Provider, document, queue, webhook, and audit-load scenarios remain environment/provider gated.

## Purpose

This plan defines how IIP should be tested for load, responsiveness, queue behavior, AI throttling, document processing volume, integration retries, WhatsApp resilience, and database performance.

## Performance Goals

Final targets must be confirmed with the pilot client. The values below are implementation defaults for planning and should be adjusted during UAT and pilot readiness.

| Area | Initial Target | Status |
|---|---:|---|
| App shell first usable load | Under 3 seconds on normal broadband | Assumption |
| Claims list API p95 | Under 800 ms for MVP-sized pilot data | Assumption |
| Case detail API p95 | Under 1.2 seconds | Assumption |
| Document upload acknowledgement | Under 3 seconds excluding processing | Assumption |
| Document processing completion | Under 5 minutes for standard documents | Assumption |
| AI summary response | Under 20 seconds where provider is healthy | Assumption |
| Health endpoint | Under 300 ms | Assumption |

## Executable Smoke Harness

`tools/qa/Invoke-IipPerformanceSmoke.mjs` is the dependency-free pre-pilot smoke runner. It reports p50/p95/p99, error rate, request rate, status counts, and threshold pass/fail per scenario.

Validate the runner without contacting Azure:

```powershell
node tools/qa/Invoke-IipPerformanceSmoke.mjs --self-test --requests 20 --concurrency 5
```

Run against an approved remote runtime only after authorization:

```powershell
$env:IIP_UAT_ACCESS_TOKEN = "<real bearer token>"
node tools/qa/Invoke-IipPerformanceSmoke.mjs --base-url "https://<approved-api-host>" --allow-remote --include-authenticated --claim-id "<synthetic-claim-id>" --requests 50 --concurrency 5
```

The token is read only from `IIP_UAT_ACCESS_TOKEN` and is never printed or written to the report. The runner refuses non-loopback targets unless `--allow-remote` is supplied.

Passing the built-in self-test proves the harness and threshold calculations work. It is not evidence that the dev, staging, or customer runtime meets performance targets.

## Load Test Scenarios

| Test ID | Scenario | Load Shape | Expected Result | Priority | Severity |
|---|---|---|---|---|---|
| PERF-API-001 | Claims list browsing. | 25 concurrent users. | Stable p95, no errors above threshold. | High | Major |
| PERF-API-002 | Case detail review. | 25 concurrent users. | Stable p95 and no data leakage. | High | Major |
| PERF-DOC-001 | Document upload burst. | 100 uploads over 15 minutes. | All files acknowledged and queued safely. | High | Major |
| PERF-DOC-002 | Document processing queue. | Mixed document types. | Queue drains; failed jobs retry or dead-letter with visibility. | High | Major |
| PERF-AI-001 | AI summary burst. | 20 concurrent AI requests. | Throttling handled; safe retry/degraded messages. | High | Major |
| PERF-CHAN-001 | WhatsApp webhook burst. | 200 inbound events. | Idempotent processing; no duplicate cases/messages. | High | Critical |
| PERF-DB-001 | Filtered claims query. | MVP data volume plus growth seed. | Indexes support common filters and searches. | High | Major |
| PERF-AUD-001 | Audit logging under load. | Concurrent state changes. | Audit events written without blocking core workflow excessively. | High | Critical |

## Resilience Test Scenarios

| Test ID | Failure Mode | Expected Behavior | Priority | Severity |
|---|---|---|---|---|
| RES-DB-001 | Database temporarily unavailable. | API returns safe error; health degraded; no fake success. | High | Critical |
| RES-BLOB-001 | Blob storage unavailable. | Upload fails safely or queues retry depending design; user sees actionable error. | High | Major |
| RES-AI-001 | Azure OpenAI throttling. | Backoff/retry or degraded message; no fabricated answer. | High | Major |
| RES-DI-001 | Document Intelligence service unavailable. | Document state remains queued/failed with retry visibility. | High | Major |
| RES-QUEUE-001 | Queue backlog grows. | Monitoring alert triggers and backlog visible. | High | Major |
| RES-WA-001 | WhatsApp webhook duplicate event. | Event is idempotent; no duplicate claim or message. | High | Critical |
| RES-GRAPH-001 | Microsoft Graph token/scope failure. | Provider health degraded and clear Not configured/auth error. | Medium | Major |

## Metrics to Capture

| Metric | Source | Use |
|---|---|---|
| API latency p50/p95/p99 | Application Insights | User experience and regression detection. |
| API error rate | Application Insights | Stability gate. |
| Queue depth and age | Azure Monitor | Backlog and outage detection. |
| Document processing duration | App logs / metrics | OCR/extraction performance. |
| AI request latency and failures | App logs / provider metrics | Throttling and user experience. |
| Database query duration | SQL metrics | Index and query tuning. |
| Webhook duplicate count | Integration logs | Idempotency validation. |

## Exit Criteria

- No Critical resilience test fails before pilot go-live.
- Known performance exceptions are documented with owner and mitigation.
- Monitoring dashboards show API, queue, AI, document processing, database, and provider health metrics.
- Load tests do not create duplicate claims, duplicate documents, or cross-tenant data exposure.

## Quality Gates Used Across This Pack

| Gate | Required Outcome | Evidence |
|---|---|---|
| G1 - Requirements coverage | MVP modules, workflows, roles, APIs, data entities, AI actions, and document flows have mapped tests. | Traceability matrix, test cases, review sign-off. |
| G2 - Tenant and role boundary | Users cannot access another tenant, workspace, or unauthorized action. | RBAC tests, API tests, audit logs. |
| G3 - Human decision control | AI never performs final claims, underwriting, fraud, premium, settlement, or payment decisions. | AI red-team results, UI checks, audit evidence. |
| G4 - Evidence and auditability | Critical actions are logged with actor, tenant, workspace, timestamp, source, and correlation ID. | Audit event records, log samples. |
| G5 - No fake production behavior | Staging and production do not silently use fake data, fake endpoints, or hidden auth bypasses. | Config review, environment checks, release checklist. |
| G6 - Failure transparency | Loading, empty, error, unauthorized, and Not configured states are visible and useful. | Functional test results, screenshots. |

## Open Questions

| ID | Open Question | Owner | Impact if Unresolved |
|---|---|---|---|
| OQ-01 | Which insurer will be the first signed pilot customer and what exact internal processes will be included? | Product / Commercial | Final test scripts, UAT users, and success thresholds may change. |
| OQ-02 | Which claims/core system will be integrated first, and will MVP require read-only lookup, export, or direct write-back? | Product / Engineering / Client IT | Impacts API, integration, resilience, and regression testing. |
| OQ-03 | Which Azure deployment path is selected first: customer-tenant Managed Application or Otic-hosted SaaS pilot? | Leadership / Engineering | Impacts release gates, security validation, support access, and incident response. |
| OQ-04 | Which WhatsApp Business provider will be used for the first pilot? | Product / Integrations | Impacts webhook tests, retry tests, idempotency checks, and channel support runbooks. |
| OQ-05 | What client-specific SLAs, roles, branches, claim categories, and document checklists must be configured? | Client Operations | Impacts functional tests, RBAC tests, document intelligence tests, and acceptance criteria. |
| OQ-06 | What final regulatory, legal, data protection, and breach notification requirements must be signed off for the first pilot? | Legal / Compliance | Impacts compliance evidence, security validation, and incident response steps. |
