# One-Page Product Brief

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | One-Page Product Brief |
| Version | 1.0 |
| Status | Draft |
| Audience | Executives, client sponsors, sales, partners, quick sharing |
| Date | 2026-06-30 |


## Product Summary

**Insurance Intelligence Platform (IIP)** is an Azure-native, AI-assisted insurance operations platform that helps insurers manage customer channels, claims, documents, approvals, knowledge, audit evidence, and operational visibility from one role-aware workspace.

IIP is not a replacement for the insurer's core policy or claims system. It acts as an intelligent operational layer above existing systems.

## Core Promise

AI prepares the work. Insurance professionals make the decisions.

## First MVP Focus

The recommended MVP is **Motor Claims & Document Intelligence** for a mid-to-large insurer pilot.

| MVP area | Description |
|---|---|
| Motor claim intake | Capture and structure FNOL from WhatsApp or supported channels |
| Claims inbox | Search, filter, assign, review, and escalate claims |
| Document intelligence | Upload, classify, extract, validate, and review claim documents |
| AI summaries | Prepare source-grounded case summaries for staff review |
| Missing-document detection | Identify incomplete files and draft customer updates |
| Command Center | Track open claims, incomplete claims, SLA risk, approvals, escalations, and backlog |
| Audit trail | Log AI outputs, human review, workflow actions, and evidence |

## Target Users

- Claims officers
- Claims supervisors
- Customer service agents
- Compliance and audit users
- Underwriters, where underwriting support is enabled
- Operations managers
- Executives
- Admins and support teams

## Core Modules

| Module | Purpose |
|---|---|
| Command Center | Operational visibility and executive control |
| Channels | WhatsApp/email/web/customer communication intake |
| Customers | Customer timeline and interaction context |
| Sales & Leads | Acquisition, follow-up, and quote handoff |
| Claims | Claims queue, review, assignment, escalation |
| Documents | OCR, classification, extraction, completeness review |
| Knowledge Base | Approved policy and process knowledge with citations |
| Approvals | Human decision workflow and escalation |
| Compliance & Audit | Evidence, logging, traceability, and review |
| Admin | Tenant, workspace, roles, templates, SLAs, integrations |

## Responsible AI Boundary

IIP may summarize, extract, classify, compare, flag, recommend, draft, and route. It must not approve or reject claims, bind policies, set final premiums, accuse customers of fraud, promise settlement, release payments, or make final regulated decisions.

## Architecture Snapshot

| Layer | Direction |
|---|---|
| Frontend | Next.js, React, TypeScript, Tailwind |
| Backend | .NET 8 / ASP.NET Core |
| Data | Azure SQL, Blob Storage |
| AI | Azure OpenAI, Azure AI Search, Azure AI Document Intelligence |
| Integration | WhatsApp, Microsoft Graph/email, policy/claims systems, exports |
| Security | Entra ID/MSAL, Key Vault, server-side authorization, audit logs |
| Deployment | Azure-first, with enterprise Managed Application path and SaaS option |

## Business Value

- Faster digital customer intake.
- Shorter document review cycles.
- Reduced missing-document delays.
- Better SLA and backlog visibility.
- Improved auditability and governance.
- Safer, human-reviewed AI use in insurance workflows.
- Stronger executive visibility into operational risk and service performance.

## Recommended Next Step

Run a controlled motor claims and document intelligence pilot with confirmed scope, users, workflows, document checklists, success metrics, and governance.

## Open Questions and Required Confirmations

| Item | Status | Notes |
|---|---|---|
| First pilot insurer | Requires Confirmation | ICEA-like mid-to-large insurer is the working assumption, but the named pilot client must be confirmed. |
| Commercial currency | Requires Confirmation | Pricing can be expressed in USD for investor/client modeling, but local contract currency may vary. |
| Final pilot duration | Requires Confirmation | An 8 to 12 week pilot is assumed unless client procurement or integration timelines require adjustment. |
| Production deployment model | Requires Confirmation | Azure Managed Application is preferred for enterprise clients; Otic-hosted SaaS may be used for demos or smaller clients. |
| Regulatory sign-off requirements | Requires Confirmation | Local insurance, privacy, outsourcing, and client legal requirements must be validated before production. |

