# SaaS Tenant Provisioning Guide

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | SaaS Tenant Provisioning Guide |
| Version | 1.0 |
| Status | Draft |
| Audience | Engineering, DevOps, Support, Client Success, Product Operations |
| Date | 2026-06-30 |


## 1. Purpose

This guide defines how to provision an Otic-hosted SaaS tenant for IIP, including tenant records, workspaces, roles, configuration, limits, integrations, monitoring, and support readiness.

This document is part of the deployment, release, and client enablement documentation set for Insurance Intelligence Platform (IIP). IIP is an Azure-first, human-reviewed insurance operations platform for claims, documents, channels, customer operations, approvals, auditability, and AI-assisted decision support. Customers interact through external channels such as WhatsApp, email, web forms, SMS, and broker/call-center routes; internal insurer staff use the IIP staff workspace. The MVP is centered on motor claims and document intelligence, with AI assisting through summarization, extraction, classification, routing, and evidence preparation while humans retain authority over consequential insurance decisions.

The expected technical foundation is Next.js/React/TypeScript/Tailwind on the frontend, .NET 8/ASP.NET Core on the backend, Azure SQL, Azure Blob Storage, Azure Key Vault, Azure AI Search, Azure OpenAI, Azure AI Document Intelligence, Azure Service Bus/Event Grid, Azure Functions or Durable Functions, Application Insights/Log Analytics, Microsoft Entra ID/MSAL, and Microsoft Graph where configured. Deployment options include an Otic-hosted SaaS path and a customer-tenant or Azure Managed Application path. Production and pilot environments must not rely on fake endpoints or mock production data; unconfigured providers must show clear Not configured states.

## 2. Provisioning Principles

| Principle | Rule |
|---|---|
| Tenant isolation | Every request must be scoped by tenant and workspace on the backend. |
| Least privilege | Users receive only the roles needed for their pilot or production responsibilities. |
| Config before use | Unconfigured providers must show Not configured rather than fake success. |
| Audit everything | Tenant creation, role assignment, integration setup, and support access must be audited. |
| Safe defaults | Disable risky capabilities until configured and tested. |

## 3. Tenant Provisioning Workflow

| Step | Action | Owner | Evidence |
|---|---|---|---|
| 1 | Create tenant record | Platform Admin | Tenant ID |
| 2 | Create initial workspace | Platform Admin | Workspace ID |
| 3 | Configure admin user(s) | Platform Admin / Client Admin | Admin access confirmed |
| 4 | Assign roles | Client Admin | RBAC matrix approval |
| 5 | Configure modules | Product / Client Success | Module config checklist |
| 6 | Configure integrations | Integration Lead | Provider health status |
| 7 | Load seed data | Data Lead | Seed data log |
| 8 | Run smoke tests | QA | Test evidence |
| 9 | Enable pilot users | Client Admin | User list |
| 10 | Handover to support | Support Lead | Support handover checklist |

## 4. Tenant Data Model Setup

| Entity | Required? | Notes |
|---|---|---|
| Tenant | Required | Legal/client organization boundary. |
| Workspace | Required | Operational area, e.g., claims workspace. |
| Users | Required | Staff users only; customers do not log into internal app. |
| Roles | Required | Admin, Claims Officer, Supervisor, Underwriter, Compliance, Customer Service, Executive, Support. |
| Claim Types | Required for MVP | Motor claims initially. |
| Document Templates | Required for document intelligence | Police report, claim form, ID, driving permit, vehicle photos, repair quotation, policy schedule, assessor report where applicable. |
| SLA Rules | Required | Client-specific. |
| Channels | Optional/configured | WhatsApp, email, web forms, SMS depending on pilot scope. |
| Knowledge Sources | Optional/configured | Approved policy/process/FAQ content. |

## 5. Default Configuration Checklist

| Configuration | Default Recommendation | Client-Specific? |
|---|---|---|
| MVP module | Motor Claims & Document Intelligence | Yes |
| AI decision authority | Assist only, human approves | No |
| Audit logging | Enabled | No |
| Exports | Restricted to authorized roles | Yes |
| Provider status | Not configured until credentials are provided | Yes |
| Tenant headers | Required server-side | No |
| Demo seed data | Allowed only in dev/demo, not production | Yes |

## 6. SaaS Operational Limits

| Limit Area | Example Limit | Requires Confirmation |
|---|---|---|
| Users | Pilot user count | Yes |
| Monthly documents | Pilot volume | Yes |
| AI calls | Usage budget/limits | Yes |
| Storage | Document retention and volume | Yes |
| Channel messages | WhatsApp/email volume | Yes |
| Support access | Support user count and access window | Yes |

## 7. Tenant Activation Checklist

- Tenant exists and is active.
- Workspace exists and is active.
- Client admin user can log in.
- Roles are assigned and tested.
- Health endpoints pass.
- Provider health shows configured or Not configured correctly.
- Claims, documents, audit, and dashboard smoke tests pass.
- Support contacts are configured.
- Monitoring tags include tenant/workspace where appropriate.
- Client success handover is complete.


## Assumptions and Open Questions

| Type | Item | Action Required |
|---|---|---|
| Assumption | The pilot MVP is focused on motor claims, document intelligence, internal staff workflows, and human-reviewed AI support. | Confirm during pilot kickoff. |
| Assumption | Azure is the preferred hosting foundation, with SaaS and Managed Application deployment paths both preserved until commercial packaging is finalized. | Confirm per client. |
| Open Question | Final Azure region, private networking requirements, and customer data residency constraints may vary by client. | Confirm with the insurer and implementation team. |
| Open Question | Exact client support contacts, escalation contacts, and approval signatories are not yet fixed. | Capture in the Client Configuration Workbook or pilot kickoff pack. |
| Requires Confirmation | Any pricing, SLA, or regulatory commitment must be confirmed by business, legal, security, and delivery leads before client use. | Review before external distribution. |

