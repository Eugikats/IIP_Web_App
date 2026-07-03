---
Product: Insurance Intelligence Platform (IIP)
Document Name: Security Test Checklist / Security Validation Plan
Version: 1.0
Status: Draft
Audience: Security, Engineering, QA, Compliance, Client IT
Date: 2026-06-30
---

# Security Test Checklist / Security Validation Plan

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

This document converts the Security & Compliance Document into concrete release security checks for demo, staging, UAT, pilot, and production readiness.

## Security Test Categories

| Category | Objective |
|---|---|
| Identity and authentication | Confirm users are authenticated through approved identity mechanisms. |
| Authorization and RBAC | Confirm permissions are enforced server-side, not only in the browser. |
| Tenant and workspace isolation | Confirm data is isolated by tenant and workspace. |
| Secrets and configuration | Confirm secrets are stored in Key Vault or approved secure stores, not source code. |
| Data protection | Confirm sensitive data is protected in transit, at rest, and in logs. |
| AI safety | Confirm AI cannot perform prohibited decisions or expose sensitive data. |
| Auditability | Confirm security-relevant and decision-relevant actions are logged. |
| Integration security | Confirm external connectors validate signatures, scopes, and failure modes. |

## Security Checklist

| Check ID | Area | Security Check | Method | Expected Result | Priority | Severity |
|---|---|---|---|---|---|---|
| SEC-AUTH-001 | Authentication | Unauthenticated user cannot access protected app routes. | Manual and automated route test. | Redirect or unauthorized state. | High | Critical |
| SEC-AUTH-002 | Authentication | API rejects requests without valid auth or approved local demo guard. | API test. | 401 or approved dev-only response. | High | Critical |
| SEC-AUTH-003 | Authentication | Demo headers are not accepted in staging or production unless explicitly approved for demo environment. | Config review and API test. | Request rejected outside approved environment. | High | Critical |
| SEC-RBAC-001 | Authorization | Server enforces role permissions for every restricted action. | API tests by role. | Unauthorized action returns 403. | High | Critical |
| SEC-RBAC-002 | Authorization | Hidden frontend buttons are not the only control. | Direct API call test. | Direct unauthorized call fails. | High | Critical |
| SEC-TEN-001 | Tenant isolation | User from tenant A cannot read tenant B claim. | API test with tenant IDs. | 403 or 404 with no data leak. | High | Critical |
| SEC-TEN-002 | Workspace isolation | Workspace claims user cannot access other workspace data without permission. | API test. | Access denied or filtered. | High | Critical |
| SEC-SEC-001 | Secrets | No secrets are committed to repo. | Secret scan. | No high-confidence secrets. | High | Critical |
| SEC-SEC-002 | Secrets | Production secrets come from Key Vault or managed secret store. | Config inspection. | No plaintext production secrets. | High | Critical |
| SEC-DATA-001 | Data protection | Logs do not contain raw customer documents, full tokens, or unnecessary PII. | Log inspection. | Redacted or minimal values only. | High | Critical |
| SEC-DATA-002 | Data protection | Sensitive files are stored with controlled access and auditability. | Azure config review. | Private storage, least privilege, audit enabled. | High | Critical |
| SEC-AI-001 | AI safety | AI refuses or escalates final claims decision requests. | Red-team prompt test. | Safe response with human-review boundary. | High | Critical |
| SEC-AI-002 | AI safety | AI does not reveal data from another tenant or unrelated claim. | Prompt and tool test. | No data leakage. | High | Critical |
| SEC-INT-001 | Integration security | Webhook endpoints validate provider authenticity. | Integration test. | Invalid signature rejected. | High | Critical |
| SEC-INT-002 | Integration security | Microsoft Graph scopes are least-privilege and approved. | Entra app review. | No broad unapproved scopes. | High | Major |
| SEC-AUD-001 | Auditability | Security-relevant actions produce audit events. | Action and log test. | Actor, time, tenant, action, correlation ID captured. | High | Critical |
| SEC-ERR-001 | Error handling | API errors do not reveal stack traces or secrets. | Error path test. | Safe error messages. | High | Major |

## Release Security Gates

| Gate | Entry Criteria | Exit Criteria |
|---|---|---|
| Demo gate | Local/demo environment only. | No visible secrets, no unsafe AI claims decisions, demo data marked as demo. |
| Staging gate | Auth, tenant isolation, logging, provider health configured. | Critical security checks pass. |
| UAT gate | Client users and roles configured. | Role and tenant access checks pass for UAT users. |
| Pilot gate | Production-like config, audit, support, backup, and monitoring ready. | All critical security checks pass and exceptions are approved. |
| Production gate | Legal/compliance sign-off complete. | All critical and high issues closed or formally accepted. |

## Security Exception Handling

| Field | Required |
|---|---|
| Exception ID | Unique identifier. |
| Description | What is being accepted or deferred. |
| Risk | Business and security impact. |
| Environment | Demo, staging, UAT, pilot, or production. |
| Compensating Control | Temporary control or mitigation. |
| Owner | Person accountable. |
| Expiry Date | Date by which exception must be fixed or renewed. |
| Approval | Security, product, and client approval where required. |

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
