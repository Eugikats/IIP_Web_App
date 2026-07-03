---
Product: Insurance Intelligence Platform (IIP)
Document Name: Incident Response & Breach Handling Runbook
Version: 1.0
Status: Draft
Audience: Security, Support, Engineering, Compliance, Leadership
Date: 2026-06-30
---

# Incident Response & Breach Handling Runbook

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

This runbook defines response steps for outages, data leaks, authentication issues, AI errors, failed integrations, suspicious access, and potential security incidents.

## Incident Severity Levels

| Severity | Description | Examples | Target Response |
|---|---|---|---|
| Sev 1 | Critical business or security incident. | Data breach, cross-tenant exposure, production outage, unauthorized access. | Immediate response. |
| Sev 2 | Major degradation or high-risk defect. | Claims workflow unavailable, document pipeline down, AI unsafe output risk. | Urgent response. |
| Sev 3 | Moderate issue with workaround. | Provider degraded, isolated feature failure, non-critical integration issue. | Same business day or agreed SLA. |
| Sev 4 | Minor issue or question. | UI defect, training question, low-priority configuration issue. | Standard support cadence. |

## Initial Response Checklist

| Step | Action | Owner |
|---|---|---|
| 1 | Create incident record and assign severity. | Support Lead |
| 2 | Capture time, environment, tenant, workspace, user reports, and correlation IDs. | Support / Engineering |
| 3 | Stabilize system and stop further harm. | Engineering / Security |
| 4 | Preserve logs and evidence. | Security / DevOps |
| 5 | Notify internal stakeholders. | Incident Commander |
| 6 | Determine client notification requirement. | Leadership / Legal / Compliance |
| 7 | Implement workaround or rollback if needed. | Engineering / Release Manager |
| 8 | Track actions and communications. | Incident Commander |

## Incident Playbooks

| Incident Type | Immediate Actions | Key Evidence | Escalation |
|---|---|---|---|
| Data leakage or cross-tenant exposure | Disable affected route/API if needed, preserve logs, block access, assess scope. | API logs, audit events, correlation IDs, affected records. | Security, Legal, Leadership, Client. |
| Auth or RBAC failure | Revoke access, disable role/action, inspect recent access. | Auth logs, role config, API responses. | Security, Engineering, Client Admin. |
| AI unsafe output | Disable or restrict affected agent/tool, preserve prompt/output, review harm. | AI interaction log, source context, user action. | AI Lead, Compliance, Product. |
| Document pipeline outage | Pause new processing if needed, queue documents, notify users of delay. | Queue metrics, provider health, failed job logs. | Engineering, Support, Client Ops. |
| WhatsApp/channel outage | Confirm provider status, queue/retry messages, communicate degraded service. | Webhook logs, queue state, provider status. | Integration Lead, Support, Client Ops. |
| Database issue | Stop risky writes if needed, inspect health, evaluate restore/rollback. | SQL metrics, logs, backup state. | DevOps, Engineering Lead. |
| Production deployment failure | Roll back or hotfix, freeze further changes. | Deployment logs, release artifact, migration logs. | Release Manager, Engineering Lead. |

## Breach Handling Notes

Final legal and regulatory notification requirements must be confirmed by qualified legal/compliance advisors for the pilot jurisdiction and client contract. Until confirmed, treat potential personal data exposure as high severity and preserve evidence.

## Communications Template

| Field | Content |
|---|---|
| Incident ID | Requires Confirmation |
| Severity | Sev 1 / Sev 2 / Sev 3 / Sev 4 |
| Summary | What happened in plain language. |
| Impact | Who and what is affected. |
| Current Status | Investigating / Mitigating / Monitoring / Resolved. |
| Workaround | Available workaround if any. |
| Next Update | Time of next update. |
| Contact | Support/escalation contact. |

## Post-Incident Review

| Review Area | Question |
|---|---|
| Cause | What happened and why? |
| Detection | How was it detected? |
| Response | What worked and what was delayed? |
| Impact | Which tenants, users, records, workflows, or integrations were affected? |
| Remediation | What fixes are required? |
| Prevention | What monitoring, tests, guardrails, or process changes must be added? |

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
