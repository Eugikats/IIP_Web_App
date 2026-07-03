# Change Control Process

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Change Control Process |
| Version | 1.0 |
| Status | Draft |
| Audience | Product, Engineering, QA, Security, Support, Client Success, Client Governance |
| Date | 2026-06-30 |


## 1. Purpose

The Change Control Process defines how IIP changes are requested, assessed, prioritized, approved, implemented, tested, released, and communicated. It covers bugs, feature requests, workflow changes, configuration changes, hotfixes, security fixes, integration changes, and AI behavior changes.

This document is part of the deployment, release, and client enablement documentation set for Insurance Intelligence Platform (IIP). IIP is an Azure-first, human-reviewed insurance operations platform for claims, documents, channels, customer operations, approvals, auditability, and AI-assisted decision support. Customers interact through external channels such as WhatsApp, email, web forms, SMS, and broker/call-center routes; internal insurer staff use the IIP staff workspace. The MVP is centered on motor claims and document intelligence, with AI assisting through summarization, extraction, classification, routing, and evidence preparation while humans retain authority over consequential insurance decisions.

The expected technical foundation is Next.js/React/TypeScript/Tailwind on the frontend, .NET 8/ASP.NET Core on the backend, Azure SQL, Azure Blob Storage, Azure Key Vault, Azure AI Search, Azure OpenAI, Azure AI Document Intelligence, Azure Service Bus/Event Grid, Azure Functions or Durable Functions, Application Insights/Log Analytics, Microsoft Entra ID/MSAL, and Microsoft Graph where configured. Deployment options include an Otic-hosted SaaS path and a customer-tenant or Azure Managed Application path. Production and pilot environments must not rely on fake endpoints or mock production data; unconfigured providers must show clear Not configured states.

## 2. Change Categories

| Category | Description | Example |
|---|---|---|
| Product feature | New or improved product capability | New claims filter or dashboard metric |
| Workflow change | Change to operational process | Supervisor approval required before export |
| Configuration change | Tenant-specific setting | SLA threshold or document checklist |
| Integration change | Provider/API/webhook change | WhatsApp provider update |
| Data change | Migration, mapping, import, cleanup | Load policy references |
| Security change | Auth, RBAC, secret, vulnerability fix | Role permission update |
| AI behavior change | Prompt, tool registry, RAG, confidence rule | Modify AI summary output format |
| Hotfix | Urgent defect fix | Claims upload failure fix |

## 3. Change Request Template

| Field | Value |
|---|---|
| Change ID |  |
| Requestor |  |
| Date Requested |  |
| Tenant/Workspace |  |
| Change Category |  |
| Description |  |
| Business Reason |  |
| Impacted Users/Roles |  |
| Impacted Modules |  |
| Urgency | Low / Medium / High / Critical |
| Risk | Low / Medium / High |
| Required Date |  |
| Attachments/Evidence |  |

## 4. Impact Assessment

| Assessment Area | Questions |
|---|---|
| Product | Does this align with MVP scope or roadmap? |
| Engineering | What code/API/data changes are required? |
| Security | Does it affect auth, roles, tenant isolation, or secrets? |
| Compliance | Does it affect auditability, approvals, data retention, or regulated decisions? |
| AI Safety | Does it change AI outputs, tool use, grounding, or human review? |
| Client Operations | Will users require training or communication? |
| Release | Can it wait for planned release or is it a hotfix? |

## 5. Approval Matrix

| Change Type | Required Approval |
|---|---|
| Low-risk configuration | Product Owner + Client Admin |
| Feature change | Product Owner + Engineering Lead + QA |
| Security change | Security Lead + Engineering Lead |
| AI behavior change | Product Owner + AI Owner + QA/Compliance |
| Integration change | Integration Lead + Client IT where applicable |
| Production hotfix | Engineering Lead + QA Lead + Support Lead |
| Client contractual/SLA change | Commercial/Legal + Client Sponsor |

## 6. Change Lifecycle

| Step | Status | Output |
|---|---|---|
| Submit | New | Change request logged |
| Triage | Under Review | Priority and category assigned |
| Assess | Impact Assessment | Impact/risk/effort documented |
| Approve/Reject/Defer | Decision | Decision record |
| Implement | In Progress | Code/config/docs updated |
| Test | QA/UAT | Evidence and results |
| Release | Scheduled/Released | Release notes and deployment evidence |
| Close | Closed | Outcome and lessons captured |

## 7. Emergency Change Rules

Emergency changes are allowed for critical outages, security incidents, production blockers, and high-impact data/integration failures. Emergency changes still require post-change documentation, validation, and retrospective review.

## 8. Change Closure Checklist

| Check | Complete |
|---|---|
| Change request updated |  |
| Test evidence attached |  |
| Security/AI review completed if applicable |  |
| Release notes updated |  |
| Client notified if applicable |  |
| Documentation updated |  |
| Known issues/improvement backlog updated |  |


## Assumptions and Open Questions

| Type | Item | Action Required |
|---|---|---|
| Assumption | The pilot MVP is focused on motor claims, document intelligence, internal staff workflows, and human-reviewed AI support. | Confirm during pilot kickoff. |
| Assumption | Azure is the preferred hosting foundation, with SaaS and Managed Application deployment paths both preserved until commercial packaging is finalized. | Confirm per client. |
| Open Question | Final Azure region, private networking requirements, and customer data residency constraints may vary by client. | Confirm with the insurer and implementation team. |
| Open Question | Exact client support contacts, escalation contacts, and approval signatories are not yet fixed. | Capture in the Client Configuration Workbook or pilot kickoff pack. |
| Requires Confirmation | Any pricing, SLA, or regulatory commitment must be confirmed by business, legal, security, and delivery leads before client use. | Review before external distribution. |

