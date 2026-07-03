# Data Migration / Seed Data Plan

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Data Migration / Seed Data Plan |
| Version | 1.0 |
| Status | Draft |
| Audience | Data Lead, Backend Engineering, QA, Client IT, Product, Support |
| Date | 2026-06-30 |


## 1. Purpose

This plan defines how IIP data is prepared, loaded, validated, and governed for dev, staging, UAT, pilot, and production. It covers seed data, client test data, users, roles, policy references, claims, documents, templates, and migration safety.

This document is part of the deployment, release, and client enablement documentation set for Insurance Intelligence Platform (IIP). IIP is an Azure-first, human-reviewed insurance operations platform for claims, documents, channels, customer operations, approvals, auditability, and AI-assisted decision support. Customers interact through external channels such as WhatsApp, email, web forms, SMS, and broker/call-center routes; internal insurer staff use the IIP staff workspace. The MVP is centered on motor claims and document intelligence, with AI assisting through summarization, extraction, classification, routing, and evidence preparation while humans retain authority over consequential insurance decisions.

The expected technical foundation is Next.js/React/TypeScript/Tailwind on the frontend, .NET 8/ASP.NET Core on the backend, Azure SQL, Azure Blob Storage, Azure Key Vault, Azure AI Search, Azure OpenAI, Azure AI Document Intelligence, Azure Service Bus/Event Grid, Azure Functions or Durable Functions, Application Insights/Log Analytics, Microsoft Entra ID/MSAL, and Microsoft Graph where configured. Deployment options include an Otic-hosted SaaS path and a customer-tenant or Azure Managed Application path. Production and pilot environments must not rely on fake endpoints or mock production data; unconfigured providers must show clear Not configured states.

## 2. Data Categories

| Data Category | Purpose | Environment Use |
|---|---|---|
| Synthetic seed data | Developer/testing data with no real client data | Local, Dev, Demo |
| Client-approved test data | UAT and pilot simulation | UAT/Staging as approved |
| Masked/anonymized data | Testing with reduced privacy risk | Staging/UAT if approved |
| Production data | Live operational data | Pilot/Production only |
| Configuration data | Roles, workflows, SLAs, templates, settings | All environments as needed |

## 3. Seed Data Scope

| Entity | MVP Need | Notes |
|---|---|---|
| Tenants and workspaces | Required | `tenant-demo` and `workspace-claims` may be used only in demo/dev contexts. |
| Users and roles | Required | Admin, Claims Officer, Supervisor, Compliance, Customer Service, Executive. |
| Customers | Required for workflow testing | Synthetic or client-approved test customers. |
| Policies | Required where policy context is tested | Read-only/reference initially where core integration is not active. |
| Claims | Required | Motor claim examples across statuses and SLA states. |
| Documents | Required | Claim form, police report, ID, driving permit, photos, repair quote, policy schedule, assessor report where applicable. |
| Audit events | Required | Generated through actions, not manually faked for production. |
| Knowledge items | Optional | Approved policy/process knowledge for RAG tests. |

## 4. Migration Process

| Step | Action | Owner | Evidence |
|---|---|---|---|
| 1 | Confirm data scope and source | Product/Data Lead | Data scope sheet |
| 2 | Classify data sensitivity | Security/Data Lead | Data classification notes |
| 3 | Prepare mapping | Backend/Data Lead | Mapping sheet |
| 4 | Clean and validate data | Data Lead | Validation log |
| 5 | Load into target environment | Backend/DevOps | Import log |
| 6 | Run reconciliation | QA/Data Lead | Reconciliation report |
| 7 | Run smoke tests | QA | Test evidence |
| 8 | Obtain sign-off | Product/Client | Sign-off record |

## 5. Data Validation Rules

- Every claim must belong to exactly one tenant and workspace.
- Every document must be linked to a claim or approved knowledge process.
- Every user must have explicit role assignment and workspace access.
- Document extraction outputs must preserve original document reference and confidence metadata.
- AI outputs must link to evidence where available and must not overwrite human decisions.
- Audit events must capture actor, action, timestamp, tenant, workspace, target entity, and correlation ID where applicable.

## 6. Migration Risk Controls

| Risk | Control |
|---|---|
| Production data copied to unsafe environment | Use synthetic/masked data unless formally approved. |
| Incorrect tenant mapping | Validate tenant/workspace IDs before import. |
| Missing documents | Reconcile expected vs imported document count. |
| Broken policy references | Flag unresolved policy numbers as incomplete context. |
| Duplicate records | Use source IDs and idempotency keys where available. |
| Failed import | Preserve import logs and rollback/cleanup script. |

## 7. Load Checklist

| Check | Complete |
|---|---|
| Source data approved |  |
| Data classification complete |  |
| Mapping reviewed |  |
| Import script tested in lower environment |  |
| Backup/snapshot taken where applicable |  |
| Import completed |  |
| Reconciliation completed |  |
| UAT smoke tests passed |  |
| Sign-off obtained |  |


## Assumptions and Open Questions

| Type | Item | Action Required |
|---|---|---|
| Assumption | The pilot MVP is focused on motor claims, document intelligence, internal staff workflows, and human-reviewed AI support. | Confirm during pilot kickoff. |
| Assumption | Azure is the preferred hosting foundation, with SaaS and Managed Application deployment paths both preserved until commercial packaging is finalized. | Confirm per client. |
| Open Question | Final Azure region, private networking requirements, and customer data residency constraints may vary by client. | Confirm with the insurer and implementation team. |
| Open Question | Exact client support contacts, escalation contacts, and approval signatories are not yet fixed. | Capture in the Client Configuration Workbook or pilot kickoff pack. |
| Requires Confirmation | Any pricing, SLA, or regulatory commitment must be confirmed by business, legal, security, and delivery leads before client use. | Review before external distribution. |

