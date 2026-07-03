# UAT Plan - User Acceptance Testing

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | UAT Plan - User Acceptance Testing |
| Version | 1.0 |
| Status | Draft |
| Audience | Client Testers, Product, QA, Delivery, Client Success, Engineering |
| Date | 2026-06-30 |


## 1. Purpose

The UAT Plan defines how insurer users validate that IIP is ready for pilot or production use. UAT confirms that workflows, roles, screens, documents, AI assistance, audit logs, and integrations operate acceptably for the client context.

This document is part of the deployment, release, and client enablement documentation set for Insurance Intelligence Platform (IIP). IIP is an Azure-first, human-reviewed insurance operations platform for claims, documents, channels, customer operations, approvals, auditability, and AI-assisted decision support. Customers interact through external channels such as WhatsApp, email, web forms, SMS, and broker/call-center routes; internal insurer staff use the IIP staff workspace. The MVP is centered on motor claims and document intelligence, with AI assisting through summarization, extraction, classification, routing, and evidence preparation while humans retain authority over consequential insurance decisions.

The expected technical foundation is Next.js/React/TypeScript/Tailwind on the frontend, .NET 8/ASP.NET Core on the backend, Azure SQL, Azure Blob Storage, Azure Key Vault, Azure AI Search, Azure OpenAI, Azure AI Document Intelligence, Azure Service Bus/Event Grid, Azure Functions or Durable Functions, Application Insights/Log Analytics, Microsoft Entra ID/MSAL, and Microsoft Graph where configured. Deployment options include an Otic-hosted SaaS path and a customer-tenant or Azure Managed Application path. Production and pilot environments must not rely on fake endpoints or mock production data; unconfigured providers must show clear Not configured states.

## 2. UAT Objectives

| Objective | Description |
|---|---|
| Workflow fit | Confirm IIP supports the agreed motor claims workflow. |
| Role fit | Confirm each role sees and does only what is appropriate. |
| Document fit | Confirm required documents can be uploaded, reviewed, extracted, and flagged. |
| AI safety | Confirm AI outputs are helpful, grounded, and human-reviewed. |
| Operational readiness | Confirm support, audit, reporting, and escalation states are usable. |

## 3. UAT Entry Criteria

| Criterion | Required Evidence |
|---|---|
| UAT environment available | URL and access confirmation |
| Users provisioned | UAT user list with roles |
| Test data loaded | Seed data and sample claims loaded |
| Core APIs healthy | Health checks and provider health reviewed |
| Known limitations shared | Known Issues Register provided |
| UAT scripts approved | Test scenarios shared with users |

## 4. UAT Roles

| Role | UAT Responsibility |
|---|---|
| Claims Officer | Test claim intake, document review, task updates, customer update drafting. |
| Claims Supervisor | Test review, escalation, approvals, dashboards, workload visibility. |
| Underwriter | Test policy context and underwriting support if in scope. |
| Compliance Officer | Test audit trail, evidence, export controls, and AI governance visibility. |
| Admin | Test users, roles, workspace settings, provider health, configuration. |
| Customer Service Agent | Test channels/customer update workflows where in scope. |

## 5. Core UAT Scenarios

| ID | Scenario | Expected Result | Priority |
|---|---|---|---|
| UAT-001 | User logs in and lands in correct workspace/role view | Correct navigation, workspace, role labels, and permissions | High |
| UAT-002 | Create or receive a motor claim | Claim record appears with correct status and audit event | High |
| UAT-003 | Upload required claim documents | Documents are stored, listed, and linked to claim | High |
| UAT-004 | Run document intelligence review | Fields are extracted, low confidence is flagged, human review required | High |
| UAT-005 | View AI claim summary | Summary is grounded, cites available evidence, and avoids final decision | High |
| UAT-006 | Escalate claim for supervisor review | Task/escalation is visible to supervisor | High |
| UAT-007 | Draft customer update | Human can review/edit before sending | Medium |
| UAT-008 | Export or view claim pack | Authorized users can access; unauthorized users cannot | High |
| UAT-009 | View audit trail | Key actions are logged with user/time/context | High |
| UAT-010 | Provider not configured state | User sees clear Not configured state, not fake data | High |

## 6. Defect Severity During UAT

| Severity | Meaning | Example |
|---|---|---|
| Critical | Blocks pilot or creates security/data risk | Unauthorized access, claim loss, data exposure |
| High | Blocks important workflow | Cannot upload required documents |
| Medium | Workaround exists but workflow is degraded | Dashboard metric delayed |
| Low | Cosmetic or minor usability issue | Label wording or spacing issue |

## 7. UAT Exit Criteria

| Criterion | Required Outcome |
|---|---|
| Critical defects | Zero open |
| High defects | Zero open or accepted workaround signed off |
| UAT scenarios | Agreed critical scenarios passed |
| Role validation | Admin, officer, supervisor, compliance roles validated |
| AI safety | No unresolved human-review or unsafe-output issue |
| Client sign-off | UAT sign-off completed |

## 8. UAT Sign-Off Table

| Area | Decision | Signatory | Date | Notes |
|---|---|---|---|---|
| Claims workflow | Approved / Not Approved |  |  |  |
| Document workflow | Approved / Not Approved |  |  |  |
| AI assistance | Approved / Not Approved |  |  |  |
| Role permissions | Approved / Not Approved |  |  |  |
| Reporting/audit | Approved / Not Approved |  |  |  |
| Pilot readiness | Approved / Not Approved |  |  |  |


## Assumptions and Open Questions

| Type | Item | Action Required |
|---|---|---|
| Assumption | The pilot MVP is focused on motor claims, document intelligence, internal staff workflows, and human-reviewed AI support. | Confirm during pilot kickoff. |
| Assumption | Azure is the preferred hosting foundation, with SaaS and Managed Application deployment paths both preserved until commercial packaging is finalized. | Confirm per client. |
| Open Question | Final Azure region, private networking requirements, and customer data residency constraints may vary by client. | Confirm with the insurer and implementation team. |
| Open Question | Exact client support contacts, escalation contacts, and approval signatories are not yet fixed. | Capture in the Client Configuration Workbook or pilot kickoff pack. |
| Requires Confirmation | Any pricing, SLA, or regulatory commitment must be confirmed by business, legal, security, and delivery leads before client use. | Review before external distribution. |

