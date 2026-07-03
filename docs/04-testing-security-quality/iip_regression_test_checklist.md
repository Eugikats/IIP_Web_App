---
Product: Insurance Intelligence Platform (IIP)
Document Name: Regression Test Checklist
Version: 1.0
Status: Draft
Audience: QA, Engineering, Release Managers, Product
Date: 2026-06-30
---

# Regression Test Checklist

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

This checklist ensures future releases do not break core IIP claims, channels, customers, leads, auth, APIs, AI, documents, audit, and admin workflows.

## Regression Checklist

| Check ID | Area | Regression Check | Required Every Release | Status |
|---|---|---|---|---|
| REG-SHELL-001 | App Shell | Navigation, top bar, workspace, role, and search render correctly. | Yes | Open |
| REG-AUTH-001 | Auth | Valid user can access allowed modules. | Yes | Open |
| REG-AUTH-002 | Auth | Unauthorized user cannot access restricted modules. | Yes | Open |
| REG-TEN-001 | Tenancy | Tenant/workspace headers and identity context are enforced. | Yes | Open |
| REG-CMD-001 | Command Center | Metrics render, fail safely, and do not use fake data. | Yes | Open |
| REG-CLAIMS-001 | Claims | Claims list, filters, search, and preview drawer work. | Yes | Open |
| REG-CASE-001 | Case Review | Case details, timeline, documents, notes, and actions work. | Yes | Open |
| REG-DOC-001 | Documents | Upload, processing status, extraction display, correction, and review work. | Yes | Open |
| REG-DOC-002 | Documents | Missing document and mismatch flags work. | Yes | Open |
| REG-AI-001 | AI | AI summaries are grounded and human-reviewed. | Yes | Open |
| REG-AI-002 | AI | AI refuses prohibited final decisions. | Yes | Open |
| REG-AUD-001 | Audit | State changes create audit events. | Yes | Open |
| REG-CHANNEL-001 | Channels | Channel overview works or shows Not configured. | Yes | Open |
| REG-CUST-001 | Customers | Customer overview and linked claims/interactions work. | Yes | Open |
| REG-LEADS-001 | Sales & Leads | Overview works or shows empty/Not configured. | Yes | Open |
| REG-ADMIN-001 | Admin | Provider health and config states render. | Yes | Open |
| REG-API-001 | API | Smoke tests pass for health, provider health, cases, and configured v1 overview endpoints. | Yes | Open |
| REG-SEC-001 | Security | Critical RBAC and tenant-boundary tests pass. | Yes | Open |
| REG-DEP-001 | Deployment | Post-deployment validation passes. | Yes | Open |

## Regression Categories

| Category | When Required |
|---|---|
| Smoke regression | Every deployment. |
| Critical path regression | Every release candidate. |
| Full functional regression | Before pilot go-live and major releases. |
| Security regression | Every auth, RBAC, tenant, AI tool, integration, or API change. |
| Document regression | Every Document Intelligence prompt/model/schema change. |
| AI regression | Every agent prompt, tool, model, retrieval, or policy change. |

## Acceptance Criteria

- All smoke and critical-path regression checks pass before release.
- Failed regression checks are classified by severity and owned.
- No Critical regression failure is released without formal exception approval.
- AI and document regressions are retested after every prompt/model/schema change.
- Regression evidence is attached to the release record.

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
