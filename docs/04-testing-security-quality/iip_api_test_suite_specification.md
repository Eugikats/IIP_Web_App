---
Product: Insurance Intelligence Platform (IIP)
Document Name: API Test Suite Specification
Version: 1.0
Status: Draft
Audience: Backend Engineering, QA Automation, Security, Integration Teams
Date: 2026-06-30
---

# API Test Suite Specification

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

Known local/demo request headers:

| Header | Known Demo Value | Purpose |
|---|---|---|
| X-Tenant-Id | tenant-demo | Tenant context. |
| X-Workspace-Id | workspace-claims | Workspace context. |
| x-demo-role | Admin | Demo role context only. |
| x-demo-user-id | local-admin | Demo user context only. |
| X-User-Id | local-admin | User context. |
| X-Correlation-ID | UUID | Traceability across logs and requests. |

Known initial endpoints include health checks, admin provider health, and cases APIs. Additional overview endpoints discussed include `/api/v1/channels/overview`, `/api/v1/customers/overview`, and `/api/v1/sales-leads/overview`.

## Purpose

This document defines API-level tests for IIP endpoints, authentication, tenant headers, validation, pagination, errors, audit behavior, role permissions, and integration boundaries.

## API Test Principles

- Test APIs directly, not only through the browser.
- Never assume frontend hiding equals authorization.
- Every state-changing request must have audit expectations.
- Every tenant-scoped request must validate tenant and workspace boundaries.
- No staging or production API may silently return fake data.

## API Test Data Approach

| Data Set | Purpose | Notes |
|---|---|---|
| Demo tenant | Local/demo validation. | Clearly marked and isolated. |
| Empty tenant | Empty-state behavior. | Confirms no fake fallback. |
| Multi-tenant pair | Tenant isolation tests. | Tenant A and Tenant B with similar record IDs. |
| Role matrix users | RBAC tests. | Admin, Claims Officer, Supervisor, Compliance, Executive. |
| Invalid inputs | Validation tests. | Bad IDs, missing headers, bad dates, unsupported files. |
| Integration samples | Connector tests. | WhatsApp, Graph, document callbacks, provider health. |

## Core API Test Matrix

| Test ID | Endpoint / Area | Scenario | Request Focus | Expected Result | Priority | Severity |
|---|---|---|---|---|---|---|
| API-HEALTH-001 | GET /health | Liveness check. | No auth where allowed. | 200 and simple health payload. | High | Major |
| API-HEALTH-002 | GET /health/ready | Readiness check. | Dependencies configured. | Ready only when required dependencies are available. | High | Major |
| API-HEALTH-003 | GET /api/admin/provider-health | Provider health check. | Admin auth and tenant context. | Provider status includes healthy, degraded, failed, or Not configured. | High | Major |
| API-CASES-001 | POST /api/cases | Create case with valid payload. | Tenant, workspace, user, correlation headers. | Case created; audit event created. | High | Critical |
| API-CASES-002 | POST /api/cases | Missing required tenant header. | No X-Tenant-Id. | 400 or 401/403, no case created. | High | Critical |
| API-CASES-003 | GET /api/cases | List cases by tenant/workspace. | Tenant and workspace headers. | Only scoped cases returned. | High | Critical |
| API-CASES-004 | GET /api/cases/{caseId} | Tenant A requests Tenant B case. | Cross-tenant ID. | 403 or 404, no data leakage. | High | Critical |
| API-CASES-005 | GET /api/cases | Pagination and filters. | Query params. | Stable pagination, correct filter results. | Medium | Major |
| API-CHANNEL-001 | GET /api/v1/channels/overview | Channel overview loads. | Tenant/workspace context. | Valid overview or empty state payload. | Medium | Major |
| API-CUST-001 | GET /api/v1/customers/overview | Customer overview loads. | Tenant/workspace context. | Valid overview or empty state payload. | Medium | Major |
| API-LEADS-001 | GET /api/v1/sales-leads/overview | Sales leads overview loads. | Tenant/workspace context. | Valid overview or empty state payload. | Medium | Minor |
| API-RBAC-001 | Restricted action | Unauthorized role calls endpoint directly. | Role context. | 403 and audit/security log where appropriate. | High | Critical |
| API-VAL-001 | Any create/update | Invalid JSON. | Malformed body. | Safe 400 response, no stack trace. | High | Major |
| API-VAL-002 | Any create/update | Oversized payload. | Limit exceed. | Request rejected safely. | High | Major |
| API-AUD-001 | State-changing endpoint | Audit event generated. | Valid action. | Audit has actor, tenant, workspace, correlation ID, action, timestamp. | High | Critical |
| API-ERR-001 | Dependency failure | Database or provider unavailable. | Simulated failure. | Safe error, no fake data, provider health degraded. | High | Major |

## Standard API Assertions

| Assertion | Applies To |
|---|---|
| Response status matches expected outcome. | All tests. |
| Response payload follows contract. | All success responses. |
| Error payload is safe and actionable. | All failure responses. |
| Tenant and workspace scoping is enforced. | All tenant-scoped APIs. |
| Role authorization is enforced server-side. | All restricted APIs. |
| Audit event is created where required. | All state-changing or sensitive actions. |
| Correlation ID is propagated to logs. | All APIs. |
| No fake fallback data appears. | All APIs in staging and production. |

## Recommended Automation Layers

| Layer | Tooling Assumption | Purpose |
|---|---|---|
| Unit tests | .NET test framework | Validate services, validators, permission checks. |
| Integration tests | Test database or containerized dependencies | Validate APIs with real persistence boundaries. |
| Contract tests | OpenAPI or typed clients | Validate response shapes and client compatibility. |
| Security API tests | Automated API runner | Validate auth, RBAC, tenant boundaries. |
| Smoke tests | CI/CD and post-deployment | Validate health, key endpoints, and provider states. |

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
