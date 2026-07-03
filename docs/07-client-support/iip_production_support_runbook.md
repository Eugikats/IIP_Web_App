# Production Support Runbook

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Production Support Runbook |
| Version | 1.0 |
| Status | Draft |
| Audience | Support, DevOps, Engineering, Security, Client IT, Customer Success |
| Date | 2026-06-30 |


## 1. Purpose

This runbook defines how the support and engineering teams monitor, diagnose, escalate, and restore IIP once it is running in pilot or production.

This document is part of the deployment, release, and client enablement documentation set for Insurance Intelligence Platform (IIP). IIP is an Azure-first, human-reviewed insurance operations platform for claims, documents, channels, customer operations, approvals, auditability, and AI-assisted decision support. Customers interact through external channels such as WhatsApp, email, web forms, SMS, and broker/call-center routes; internal insurer staff use the IIP staff workspace. The MVP is centered on motor claims and document intelligence, with AI assisting through summarization, extraction, classification, routing, and evidence preparation while humans retain authority over consequential insurance decisions.

The expected technical foundation is Next.js/React/TypeScript/Tailwind on the frontend, .NET 8/ASP.NET Core on the backend, Azure SQL, Azure Blob Storage, Azure Key Vault, Azure AI Search, Azure OpenAI, Azure AI Document Intelligence, Azure Service Bus/Event Grid, Azure Functions or Durable Functions, Application Insights/Log Analytics, Microsoft Entra ID/MSAL, and Microsoft Graph where configured. Deployment options include an Otic-hosted SaaS path and a customer-tenant or Azure Managed Application path. Production and pilot environments must not rely on fake endpoints or mock production data; unconfigured providers must show clear Not configured states.

## 2. Support Principles

| Principle | Operational Rule |
|---|---|
| Protect client operations | Prioritize claim intake, document review, customer updates, and audit availability. |
| Protect data | Treat access, tenant boundaries, documents, and audit logs as high-sensitivity areas. |
| Preserve evidence | Avoid deleting logs, messages, documents, or audit records during troubleshooting. |
| Be transparent | Communicate issue status, impact, workaround, and next update time. |
| Escalate early | Security, data exposure, auth failures, or AI safety issues require immediate escalation. |

## 3. First 15 Minutes Triage

| Step | Action | Tool / Source |
|---|---|---|
| 1 | Confirm issue report and affected tenant/workspace/user/module | Support ticket or client contact |
| 2 | Check health endpoints `/health`, `/health/live`, `/health/ready` | API / monitoring |
| 3 | Check provider health admin page | IIP admin/provider health |
| 4 | Check recent deployments or configuration changes | CI/CD and change log |
| 5 | Check Application Insights failures, latency, exceptions | App Insights / Log Analytics |
| 6 | Check queues and retries | Service Bus/Event Grid |
| 7 | Confirm whether issue is user-specific, tenant-specific, provider-specific, or platform-wide | Logs and replication |

## 4. Common Incident Paths

| Symptom | Likely Area | First Checks |
|---|---|---|
| Users cannot log in | Entra ID/MSAL/auth config | App registration, redirect URI, token validation, roles |
| API returns unauthorized | RBAC/tenant headers | User role, token claims, tenant/workspace mapping |
| Claims not appearing | API/database/integration | Case endpoints, filters, DB records, integration event status |
| Documents fail to process | Blob/Document Intelligence/queue | Blob access, model config, queue failures, file type/size |
| AI answers are unavailable | Azure OpenAI/config/tool registry | Provider health, deployment name, throttling, Not configured state |
| Search returns no results | Azure AI Search/indexing | Index schema, indexer status, knowledge approval |
| WhatsApp messages delayed | Channel provider/queue | Webhook delivery, retries, idempotency, channel config |
| Dashboard metrics wrong | Aggregation/API/data | Metric query, tenant filters, cache, job status |

## 5. Severity Model

| Severity | Description | Examples | Response Target |
|---|---|---|---|
| Sev 1 | Critical outage or data/security risk | System unavailable, data exposure, auth bypass | Immediate escalation |
| Sev 2 | Major workflow degraded | Claims intake blocked, document processing down | Same business day urgent handling |
| Sev 3 | Limited defect or workaround exists | One report wrong, one integration delayed | Normal support queue |
| Sev 4 | Question, enhancement, minor issue | Usability question, training request | Planned response |

## 6. Operational Checks

- Review failed requests, exceptions, and dependency failures.
- Check correlation IDs from user reports.
- Verify tenant and workspace filters in logs and database queries.
- Check Service Bus dead-letter queues.
- Check AI provider errors and throttling responses.
- Check Blob access permissions and private container policies.
- Check database CPU, DTU/vCore, blocking queries, and failed migrations.
- Check front-end build and static asset delivery.
- Confirm whether client configuration changed recently.

## 7. Escalation Matrix

| Area | Escalate To | Trigger |
|---|---|---|
| Authentication / RBAC | Security Lead + Backend Lead | Login failures, role leakage, unauthorized access |
| Data / Database | Backend Lead + DBA/DevOps | Migration failures, data inconsistency, performance issue |
| AI Safety | AI Owner + Product + Compliance | Unsafe output, hallucination, autonomous decision risk |
| Documents | AI/Document Lead + Backend Lead | OCR/extraction failure, missing evidence, invalid confidence behavior |
| Channels | Integration Lead | WhatsApp/email/webhook failure |
| Client Communications | Client Success Lead | Client impact, downtime notices, governance updates |

## 8. Support Closure Checklist

| Check | Complete |
|---|---|
| Root cause identified or workaround documented |  |
| Client impact described |  |
| Logs/correlation IDs captured |  |
| Data integrity confirmed |  |
| AI safety reviewed if applicable |  |
| Client update sent |  |
| Known issue or improvement backlog updated |  |
| Incident report created for Sev 1/2 |  |


## Assumptions and Open Questions

| Type | Item | Action Required |
|---|---|---|
| Assumption | The pilot MVP is focused on motor claims, document intelligence, internal staff workflows, and human-reviewed AI support. | Confirm during pilot kickoff. |
| Assumption | Azure is the preferred hosting foundation, with SaaS and Managed Application deployment paths both preserved until commercial packaging is finalized. | Confirm per client. |
| Open Question | Final Azure region, private networking requirements, and customer data residency constraints may vary by client. | Confirm with the insurer and implementation team. |
| Open Question | Exact client support contacts, escalation contacts, and approval signatories are not yet fixed. | Capture in the Client Configuration Workbook or pilot kickoff pack. |
| Requires Confirmation | Any pricing, SLA, or regulatory commitment must be confirmed by business, legal, security, and delivery leads before client use. | Review before external distribution. |

