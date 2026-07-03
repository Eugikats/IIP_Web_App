---
Product: Insurance Intelligence Platform (IIP)
Document Name: Compliance Evidence Register
Version: 1.0
Status: Draft
Audience: Compliance, Security, QA, Audit, Client Governance
Date: 2026-06-30
---

# Compliance Evidence Register

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

This register identifies evidence needed to prove that IIP controls are operating as intended. It supports security review, compliance review, client pilot governance, and release readiness.

## Evidence Register

| Evidence ID | Control Area | Evidence Required | Source | Frequency | Owner | Status |
|---|---|---|---|---|---|---|
| CE-AUTH-001 | Authentication | Authentication configuration and sign-in test evidence. | Entra ID / app config / screenshots. | Per release. | Security | Open |
| CE-RBAC-001 | Authorization | Role permission test results. | RBAC test suite. | Per release. | QA / Security | Open |
| CE-TEN-001 | Tenant isolation | Cross-tenant access test results. | API test results. | Per release. | QA / Engineering | Open |
| CE-AUD-001 | Audit trail | Sample audit logs for claim actions, document corrections, AI runs, approvals. | Database / logs. | Per release. | Engineering | Open |
| CE-AI-001 | AI safety | AI red-team results and failure remediation. | AI evaluation log. | Per model/prompt release. | AI Lead | Open |
| CE-DOC-001 | Document processing | Extraction review and correction audit samples. | Document module / audit logs. | Per release. | QA / Claims Ops | Open |
| CE-SEC-001 | Secrets | Secret scan and Key Vault configuration evidence. | CI logs / Azure config. | Per release. | DevOps | Open |
| CE-LOG-001 | Logging | Application Insights and Log Analytics dashboards. | Azure Monitor. | Per release. | DevOps | Open |
| CE-BACK-001 | Backup | Backup configuration and restore test evidence. | Azure SQL / storage reports. | Requires Confirmation. | DevOps | Open |
| CE-INC-001 | Incident response | Incident response contact list and runbook approval. | Support docs. | Per pilot/release. | Support Lead | Open |
| CE-CONS-001 | Consent / communication | Customer consent or notice records where required. | Channel logs / CRM / configuration. | Requires Confirmation. | Product / Legal | Open |
| CE-RET-001 | Retention | Data retention policy and implementation evidence. | Config / policy docs. | Requires Confirmation. | Legal / Engineering | Open |
| CE-DEP-001 | Deployment | Deployment logs, release notes, and post-deployment validation. | CI/CD / release docs. | Per release. | Release Manager | Open |

## Evidence Quality Rules

- Evidence must include date, environment, owner, and source system where practical.
- Screenshots alone are weaker than logs or exported results; use both where useful.
- Sensitive evidence must be redacted before client sharing unless approved.
- Evidence should not expose secrets, tokens, private keys, or unnecessary customer PII.
- Evidence should be retained according to the final agreed retention policy.

## Compliance Review Cadence

| Review | Purpose | Suggested Timing |
|---|---|---|
| Pre-UAT review | Confirm controls before client testing. | Before UAT start. |
| Pilot go-live review | Confirm critical evidence before live pilot. | 1 to 3 days before pilot launch. |
| 30-day review | Confirm control operation during pilot. | 30 days after launch. |
| Pilot closeout review | Confirm lessons, exceptions, evidence gaps. | Pilot closeout. |

## Evidence Gap Template

| Field | Required |
|---|---|
| Gap ID | Yes |
| Missing Evidence | Yes |
| Related Control | Yes |
| Risk | Yes |
| Owner | Yes |
| Target Date | Yes |
| Interim Mitigation | Yes |

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
