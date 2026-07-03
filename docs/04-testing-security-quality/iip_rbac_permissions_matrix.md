# RBAC & Permissions Matrix

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | RBAC & Permissions Matrix |
| Version | 1.0 |
| Status | Draft |
| Audience | Product owners, security leads, backend engineers, frontend engineers, QA leads, coding agents |
| Date | 2026-06-30 |


## 1. Purpose

This document defines exact permissions for Admin, Claims Officer, Claims Supervisor, Underwriter, Compliance Officer, Customer Service Agent, Executive, Otic Admin, and Support roles.


This document uses the existing IIP project context and prior documentation pack: Master Product Context, PRD, FSD, Technical Architecture Document, System Integration Strategy, UI/UX Handoff, API Specification, Data Model, Security & Compliance Document, QA/Test Plan, Deployment Plan, User/Admin Guide, Implementation Roadmap, and Strategy/Requirements/Roadmap Pack.

Confirmed project baseline:
- Official product name: Insurance Intelligence Platform (IIP).
- IIP is an Azure-native, human-reviewed insurance operations platform, not a replacement for core insurance systems.
- MVP focus: motor claims and document intelligence for a mid-to-large insurer pilot.
- Customer engagement is WhatsApp-first and channel-led; customers do not log into the internal staff application.
- Internal modules include Command Center, Channels, Customers, Sales & Leads, Claims, Documents, Knowledge Base, Approvals, Compliance & Audit, Reports & Analytics, Integrations, Admin Settings, and Support & Health.
- The internal UI is a role-aware staff workspace with persistent navigation, top bar, global search, operational states, and contextual Copilot panel.
- The platform uses Next.js, React, TypeScript, and Tailwind on the frontend and .NET 8 / ASP.NET Core on the backend.
- Azure services include Azure SQL, Blob Storage, Key Vault, Azure AI Search, Azure OpenAI, Azure AI Document Intelligence, Service Bus/Event Grid, Functions/Durable Functions, API Management, Application Insights/Log Analytics, Entra ID/MSAL, and optional Managed Application packaging.
- AI assists with extraction, summarization, classification, routing, drafting, risk signals, and evidence preparation; humans make regulated or consequential decisions.
- Staging and production must not use fake operational data. Empty states, error states, and Not configured states must be real and explicit.


## RBAC Principles

- Backend authorization is the enforcement point.
- Frontend role-aware navigation is not sufficient for security.
- Least privilege should be the default.
- All privileged actions must be audited.
- Support and Otic Admin access must be controlled, justified, and traceable.

## Permission Matrix

| Module / Capability | Admin | Claims Officer | Claims Supervisor | Underwriter | Compliance Officer | Customer Service Agent | Executive | Otic Admin | Support Engineer |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Command Center | View | View | View | View | View | View | View | View | View diagnostics |
| Claims | Configure/View All | Create/Edit Assigned | Review/Assign/Approve escalations | View coverage context | View compliance context | View customer-facing status | View metrics only | Tenant admin support | Support read as authorized |
| Documents | Configure checklist/View | Upload/Review assigned | Review exceptions | View policy-related docs | View audit/legal docs | View customer submitted docs | View metrics | Support config | Support troubleshooting |
| Channels | Configure/View | View claim-related | View team queues | View as needed | View audit context | Draft/send if permitted | Metrics only | Support config | Troubleshoot provider |
| Approvals | Configure workflows | Submit for approval | Approve/reject within authority | Recommend underwriting review | Review compliance escalations | No final approval | View metrics | Support config | Support troubleshoot |
| AI Outputs | Configure policy | Request/use with review | Review team outputs | Use coverage assistant | Review AI audit | Use draft assistant | View aggregate metrics | Support prompt/tool config as authorized | Troubleshoot only |
| Audit | Full tenant admin view | View own/assigned case audit | View team audit | View underwriting-related audit | Full compliance audit view | View customer comms audit | View reports | Support audit access | Support diagnostics |
| Admin Settings | Full client admin | No | Limited team config | No | Compliance settings as assigned | No | No | Full platform admin | Limited support |
| Integrations | Configure if admin | No | View status | No | View status | View channel status | View status | Configure/support | Troubleshoot |

## Action Classes

| Action Class | Examples | Authorization Rule |
| --- | --- | --- |
| Read | View claim, document, customer, report | Requires module permission and tenant/workspace scope. |
| Create | Create claim, upload document, create task | Requires role permission and audit. |
| Update | Edit claim field, correct extraction, assign task | Requires role permission, entity scope, audit. |
| Approve | Approve exception, review escalation | Requires explicit approval permission and reason. |
| Export | Generate claim pack, export report | Requires export permission and audit. |
| Configure | Provider config, checklist config, role setup | Admin/support role only; audit required. |

## Restricted Actions

| Action | Who May Perform | Notes |
| --- | --- | --- |
| Final claim approval/rejection | Authorized human claims role only | AI cannot perform. |
| Fraud determination or accusation | Authorized investigation/compliance process only | AI may flag signals only. |
| Provider credential update | Admin/Otic Admin as authorized | Secrets in Key Vault only. |
| Cross-tenant support access | Otic Admin/Support with explicit authorization | Must be audited. |
| Customer message send | Customer Service/Claims role as configured | AI can draft only. |

## Acceptance Criteria

| ID | Acceptance Criterion | Evidence |
| --- | --- | --- |
| AC-RBAC-001 | Backend denies unauthorized API access regardless of frontend navigation. | API security tests. |
| AC-RBAC-002 | All privileged actions create audit events. | Audit review. |
| AC-RBAC-003 | Role matrix is reflected in UI visibility and action availability. | Functional QA. |
| AC-RBAC-004 | Support/Otic Admin access is auditable. | Support access test. |

## Open Questions and Required Confirmations

| ID | Open Question / Confirmation Needed | Impact |
| --- | --- | --- |
| OQ-RBAC-001 | What exact authority limits apply to Claims Supervisor and Underwriter roles? | Affects approval rules. |
| OQ-RBAC-002 | Will client define custom roles in MVP? | Affects admin complexity. |
| OQ-RBAC-003 | Which users can send customer messages during pilot? | Affects Channels permissions. |
