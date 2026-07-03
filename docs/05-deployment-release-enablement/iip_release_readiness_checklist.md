---
Product: Insurance Intelligence Platform (IIP)
Document Name: Release Readiness Checklist
Version: 1.0
Status: Draft
Audience: Release Managers, Product, Engineering, QA, Security, Support, Client Stakeholders
Date: 2026-06-30
---

# Release Readiness Checklist

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

This checklist provides a final go/no-go structure before demo, staging, UAT, pilot, or production release.

## Release Metadata Template

| Field | Value |
|---|---|
| Release Name | Requires Confirmation |
| Version | Requires Confirmation |
| Environment | Demo / Staging / UAT / Pilot / Production |
| Release Owner | Requires Confirmation |
| Deployment Date | Requires Confirmation |
| Rollback Owner | Requires Confirmation |
| Client Approval Needed | Yes / No / Requires Confirmation |

## Go/No-Go Checklist

| Check ID | Area | Readiness Check | Required for Demo | Required for UAT | Required for Pilot/Production | Status |
|---|---|---|---|---|---|---|
| RR-REQ-001 | Requirements | Release scope mapped to approved requirements. | Yes | Yes | Yes | Open |
| RR-REQ-002 | Requirements | Out-of-scope items are not accidentally enabled. | Yes | Yes | Yes | Open |
| RR-FUNC-001 | Functional QA | Critical functional tests pass. | Yes | Yes | Yes | Open |
| RR-FUNC-002 | Functional QA | Empty, error, unauthorized, and Not configured states verified. | Yes | Yes | Yes | Open |
| RR-API-001 | API | API smoke tests pass. | Yes | Yes | Yes | Open |
| RR-API-002 | API | Tenant and workspace boundary tests pass. | Yes | Yes | Yes | Open |
| RR-SEC-001 | Security | Critical security checks pass. | Yes | Yes | Yes | Open |
| RR-SEC-002 | Security | Secrets scan complete. | Yes | Yes | Yes | Open |
| RR-AI-001 | AI Safety | Critical red-team tests pass. | Yes | Yes | Yes | Open |
| RR-AI-002 | AI Safety | Human decision boundary visible in UI and enforced in workflows. | Yes | Yes | Yes | Open |
| RR-DOC-001 | Document Intelligence | Document processing tests pass for MVP document types. | No | Yes | Yes | Open |
| RR-DATA-001 | Data | Required seed/configuration data loaded safely. | Yes | Yes | Yes | Open |
| RR-OBS-001 | Observability | Logs, metrics, health checks, and alerts are active. | No | Yes | Yes | Open |
| RR-SUP-001 | Support | Support and escalation contacts confirmed. | No | Yes | Yes | Open |
| RR-REL-001 | Release | Rollback plan is documented. | Yes | Yes | Yes | Open |
| RR-CLIENT-001 | Client | Client UAT or pilot sign-off received where needed. | No | Yes | Yes | Open |

## Release Decision Template

| Decision | Meaning |
|---|---|
| Go | Release can proceed. |
| Conditional Go | Release can proceed with documented accepted exceptions. |
| No-Go | Release is blocked until listed issues are resolved. |

## Required Sign-Offs

| Role | Required For | Sign-Off |
|---|---|---|
| Product Owner | Scope and acceptance. | Requires Confirmation |
| Engineering Lead | Build and deployment readiness. | Requires Confirmation |
| QA Lead | Test completion. | Requires Confirmation |
| Security/Compliance Lead | Security and governance gates. | Requires Confirmation |
| Support Lead | Support readiness. | Requires Confirmation |
| Client Representative | UAT / pilot / production readiness. | Requires Confirmation |

## Accepted Exception Template

| Field | Required |
|---|---|
| Exception ID | Yes |
| Description | Yes |
| Impact | Yes |
| Severity | Yes |
| Owner | Yes |
| Mitigation | Yes |
| Expiry / Fix Target | Yes |
| Approver | Yes |

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
