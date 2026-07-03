# Post-Deployment Validation Checklist

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Post-Deployment Validation Checklist |
| Version | 1.0 |
| Status | Draft |
| Audience | QA, DevOps, Engineering, Support, Product, Client Success |
| Date | 2026-06-30 |


## 1. Purpose

This checklist validates that a newly deployed IIP environment or release is operationally ready after deployment. It must be run after significant deployments, environment setup, pilot launch, hotfixes, and production changes.

This document is part of the deployment, release, and client enablement documentation set for Insurance Intelligence Platform (IIP). IIP is an Azure-first, human-reviewed insurance operations platform for claims, documents, channels, customer operations, approvals, auditability, and AI-assisted decision support. Customers interact through external channels such as WhatsApp, email, web forms, SMS, and broker/call-center routes; internal insurer staff use the IIP staff workspace. The MVP is centered on motor claims and document intelligence, with AI assisting through summarization, extraction, classification, routing, and evidence preparation while humans retain authority over consequential insurance decisions.

The expected technical foundation is Next.js/React/TypeScript/Tailwind on the frontend, .NET 8/ASP.NET Core on the backend, Azure SQL, Azure Blob Storage, Azure Key Vault, Azure AI Search, Azure OpenAI, Azure AI Document Intelligence, Azure Service Bus/Event Grid, Azure Functions or Durable Functions, Application Insights/Log Analytics, Microsoft Entra ID/MSAL, and Microsoft Graph where configured. Deployment options include an Otic-hosted SaaS path and a customer-tenant or Azure Managed Application path. Production and pilot environments must not rely on fake endpoints or mock production data; unconfigured providers must show clear Not configured states.

## 2. Validation Summary

| Validation Area | Owner | Status |
|---|---|---|
| Application availability | DevOps |  |
| Authentication and RBAC | Security / QA |  |
| Tenant/workspace context | Backend / QA |  |
| Database and migrations | Backend |  |
| Documents and Blob Storage | Backend / QA |  |
| AI provider behavior | AI Owner / QA |  |
| Document Intelligence | AI/Document Lead |  |
| Integrations | Integration Lead |  |
| Audit logging | QA / Compliance |  |
| Monitoring and alerts | DevOps / Support |  |

## 3. Health Validation

| Check | Expected Result | Pass/Fail |
|---|---|---|
| `/health` | Returns healthy or expected degraded state |  |
| `/health/live` | Returns live |  |
| `/health/ready` | Returns ready when dependencies are available |  |
| Provider health page | Shows configured, degraded, or Not configured accurately |  |
| Frontend loads | App shell and navigation render |  |

## 4. Security and Access Validation

| Check | Expected Result | Pass/Fail |
|---|---|---|
| Login works for authorized user | User lands in correct workspace |  |
| Unauthorized user blocked | No access to protected routes/APIs |  |
| Role restrictions enforced | Role cannot access disallowed module/action |  |
| Tenant boundary enforced | User cannot access another tenant/workspace |  |
| Secrets not exposed | No secrets in UI, logs, console, or responses |  |

## 5. Workflow Validation

| Check | Expected Result | Pass/Fail |
|---|---|---|
| Command Center loads | Metrics/cards show real, empty, or error state |  |
| Claims list loads | Table/search/filter states work |  |
| Claim detail opens | Summary, documents, timeline, and audit context visible |  |
| Document upload works | File stored and linked to claim |  |
| Document review works | Extraction/low-confidence behavior visible or Not configured |  |
| AI claim summary works | AI assists only and preserves human review |  |
| Audit trail logs action | Actor/action/time/context recorded |  |

## 6. Integration Validation

| Integration | Expected Result | Pass/Fail |
|---|---|---|
| WhatsApp/channel intake | Configured, testable, or explicitly Not configured |  |
| Microsoft Graph/email | Configured, testable, or explicitly Not configured |  |
| Azure Blob Storage | Upload/download authorized only |  |
| Azure SQL | Reads/writes successful and tenant-scoped |  |
| Azure AI Search | Search works or Not configured shown |  |
| Azure OpenAI | AI call works or Not configured shown |  |
| Document Intelligence | Extraction works or Not configured shown |  |

## 7. Release Closure

| Closure Item | Complete |
|---|---|
| Validation evidence saved |  |
| Release notes published |  |
| Known issues updated |  |
| Support team notified |  |
| Client notified where applicable |  |
| Monitoring dashboard reviewed |  |
| Rollback window closed or extended |  |


## Assumptions and Open Questions

| Type | Item | Action Required |
|---|---|---|
| Assumption | The pilot MVP is focused on motor claims, document intelligence, internal staff workflows, and human-reviewed AI support. | Confirm during pilot kickoff. |
| Assumption | Azure is the preferred hosting foundation, with SaaS and Managed Application deployment paths both preserved until commercial packaging is finalized. | Confirm per client. |
| Open Question | Final Azure region, private networking requirements, and customer data residency constraints may vary by client. | Confirm with the insurer and implementation team. |
| Open Question | Exact client support contacts, escalation contacts, and approval signatories are not yet fixed. | Capture in the Client Configuration Workbook or pilot kickoff pack. |
| Requires Confirmation | Any pricing, SLA, or regulatory commitment must be confirmed by business, legal, security, and delivery leads before client use. | Review before external distribution. |

