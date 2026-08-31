---
Product: Insurance Intelligence Platform (IIP)
Document Name: Functional Test Cases & Acceptance Criteria Pack
Version: 1.0
Status: Draft
Audience: QA, Product, Engineering, Claims Operations, Client UAT Users
Date: 2026-06-30
---


---

> **Authority note — 2026-08-05:** This pack is a historical scenario catalogue. The normative Release 1 obligations and measurable gates are `../01-strategy-requirements-roadmap/iip_release_1_requirements_baseline.md` and `../01-strategy-requirements-roadmap/iip_mvp_acceptance_criteria_register.md`. Retain useful scenarios, but do not treat broad module coverage or generic quality language below as acceptance without requirement IDs and current evidence.

# July 2026 Update: Functional Tests for Chat-First Command Center

## Functional Testing Decision

The Command Center must now be tested as a **chat-first operational command workspace**. The primary user journey begins with the central command chat, not with a dashboard grid.

## Additional Functional Test Cases

| Test ID | Module | Scenario | Preconditions | Steps | Expected Result | Priority | Severity |
|---|---|---|---|---|---|---|---|
| FT-CMDCHAT-001 | Command Center | User opens chat-first Command Center. | Authenticated user with Command Center access. | Open Command Center. | Central chat workspace dominates page; daily briefing, metric pills, composer, and evidence rail are visible or show safe states. | High | Major |
| FT-CMDCHAT-002 | Command Center | User asks daily priority question. | Backend metrics and claims endpoints available. | Enter `What needs my attention today?`. | System returns prioritized operational answer with structured blocks and evidence/source context. | High | Major |
| FT-CMDCHAT-003 | Command Center | User clicks metric pill. | Metric pill visible. | Click `SLA breaches` or `Pending approvals`. | Conversation shows filtered result block or navigates to correct filtered workspace with context preserved. | High | Major |
| FT-CMDCHAT-004 | Command Center | SLA breach query. | SLA data exists. | Enter `Show SLA breaches`. | Response shows SLA summary, linked claims, backend timestamps, and right rail evidence. | High | Major |
| FT-CMDCHAT-005 | Command Center | Missing document query. | Claim/document checklist configured. | Enter `Which motor claims are missing police reports?`. | Response shows only authorized matching claims and missing-document evidence. | High | Major |
| FT-CMDCHAT-006 | Command Center | Provider not configured. | Required provider is disabled/missing. | Ask a query requiring provider. | Chat response shows `Not configured`; no simulated result appears. | High | Critical |
| FT-CMDCHAT-007 | Command Center | Unauthorized operational query. | User lacks required permission. | Ask for restricted claim/audit data. | Response denies or limits safely; no restricted data appears. | High | Critical |
| FT-CMDCHAT-008 | Command Center | Draft customer update from chat. | User can draft but not auto-send sensitive messages. | Ask to prepare missing-document customer updates. | Draft action proposal appears; send requires human approval. | High | Critical |
| FT-CMDCHAT-009 | Command Center | No approved source for policy answer. | Knowledge source missing. | Ask a coverage/policy question. | System refuses/escalates with no-source explanation. | High | Critical |
| FT-CMDCHAT-010 | Command Center | Audit command session. | Command query completed. | Open audit/agent run record. | Audit shows user, command, tools, evidence IDs, output block types, correlation ID, and proposed actions. | High | Critical |

## Acceptance Criteria

| ID | Acceptance Criterion |
|---|---|
| AC-CMDCHAT-001 | The Command Center must visually prioritize the central command-chat workspace over static dashboard cards. |
| AC-CMDCHAT-002 | Metric pills must be clickable and must map to real backend filters or command queries. |
| AC-CMDCHAT-003 | Every AI-assisted operational answer must show evidence, confidence/uncertainty, and human-review status where relevant. |
| AC-CMDCHAT-004 | The right rail must update based on the current command focus or selected embedded result. |
| AC-CMDCHAT-005 | The UI must not display fake numbers, fake records, hardcoded insurer data, or fake provider success in deployed environments. |
| AC-CMDCHAT-006 | The Command Center must pass keyboard, responsive, empty, loading, error, unauthorized, and Not configured state testing. |

## Phase 1-4 Functional Test Alignment

| Phase | Functional Test Focus |
|---|---|
| Phase 1 | Layout, prompt chips, composer, response block rendering, evidence rail shell. |
| Phase 2 | Real API-backed briefing, metrics, claims, SLA, documents, approvals, provider health. |
| Phase 3 | Agent workflows, RAG answers, action drafts, approval handoff, audit events. |
| Phase 4 | Full E2E tests, security/tenant tests, accessibility, visual QA, production release validation. |

# Functional Test Cases & Acceptance Criteria Pack

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

This document converts the FSD into execution-ready functional test cases for screens, modules, roles, workflows, states, and user actions. It is intended for internal QA, client UAT preparation, and coding-agent acceptance checks.

## Testing Scope

| Area | In MVP Functional Scope | Notes |
|---|---|---|
| App shell | Yes | Left navigation, top bar, workspace context, role display, global search entry points. |
| Command Center | Yes | Metrics, operational trend cards, quick links, AI interaction areas, provider states. |
| Claims Inbox | Yes | Dense table, filters, search, row preview, assignment and escalation states. |
| Case Review | Yes | Summary, customer and policy context, document status, timeline, human actions. |
| Document Review | Yes | Upload, classification, extraction, confidence, missing-document detection, human review. |
| Channels | Yes | Overview, inbound channel visibility, handoff to claims/customers/leads. |
| Customers | Yes | Customer overview and interaction history from supported channels. |
| Sales & Leads | Limited MVP | Lead overview and routing only unless confirmed for pilot. |
| Knowledge Base | Limited MVP | Approved content used for grounded AI support. |
| Approvals | MVP workflow | Human approval gates for operational actions. |
| Compliance & Audit | Yes | Audit event visibility and filtering. |
| Admin Settings | MVP configuration | Provider health, roles, workspace settings, Not configured states. |

## Test ID Convention

Use the format `FT-<MODULE>-<NUMBER>`, for example `FT-CLAIMS-001`.

## Functional Test Matrix

| Test ID | Module | Scenario | Preconditions | Steps | Expected Result | Priority | Severity |
|---|---|---|---|---|---|---|---|
| FT-SHELL-001 | App Shell | User opens staff app with valid tenant and workspace context. | User is authenticated or in approved demo mode. | Open app, inspect left nav, top bar, workspace, role, alerts. | Shell loads, context is visible, no broken nav, role-aware items are visible. | High | Major |
| FT-SHELL-002 | App Shell | Unauthorized user attempts to access restricted route. | User lacks role permission. | Navigate to restricted module. | User sees unauthorized state; no data is leaked. | High | Critical |
| FT-SHELL-003 | App Shell | Provider is not configured. | Provider config missing. | Open affected module. | UI shows Not configured state and admin-oriented next step. | High | Major |
| FT-SEARCH-001 | Global Search | Assigned-claims user searches a term matching assigned and unassigned cases. | Same-workspace records exist for multiple assignees. | Submit the shared term from the shell. | Only cases the current user may open appear; returned counts do not reveal restricted matches. | High | Critical |
| FT-SEARCH-002 | Global Search | User searches case, document, task and approval references. | Authorized related objects exist. | Search exact and partial references. | Results group by type, explain the match, rank exact identity first and link to a real authorized route; approval results require approval permission. | High | Critical |
| FT-SEARCH-003 | Global Search | User searches restricted or cross-tenant identifiers. | Restricted records exist. | Submit exact identifiers. | UI reports no authorized results without existence, count, snippet or sensitive-metadata leakage; raw query is absent from audit. | High | Critical |
| FT-SEARCH-004 | Global Search | Search is used with keyboard, failure and return navigation. | Search route available. | Focus with Ctrl/Cmd+K, submit, inject API failure, open a result, then go back. | Focus/submit/status are accessible; safe error is shown; query and result context return without stale authority. | High | Major |
| FT-CMD-001 | Command Center | Operational metrics load. | Backend available. | Open Command Center. | Open claims, new claims, incomplete claims, SLA risk, pending approvals, and AI metrics display or empty state. | High | Major |
| FT-CMD-002 | Command Center | Metrics API fails. | Simulate failed API call. | Open Command Center. | Error state appears; no fake fallback numbers appear. | High | Major |
| FT-CMD-003 | Command Center | Quick link opens claims queue. | Claims data exists or empty state exists. | Click claims quick link. | Correct filtered view opens with context preserved. | Medium | Minor |
| FT-CLAIMS-001 | Claims Inbox | Claims list loads. | Claims endpoint configured. | Open Claims Inbox. | Table shows claim ID, customer, status, SLA, risk, owner, next action. | High | Major |
| FT-CLAIMS-002 | Claims Inbox | Filter by SLA at risk. | Claims include SLA values. | Apply SLA filter. | Only matching claims show; filter chip appears; clear filter works. | High | Major |
| FT-CLAIMS-003 | Claims Inbox | Search by customer, claim ID, or policy number. | Indexed data exists. | Search each supported field. | Correct results appear; no cross-tenant data appears. | High | Critical |
| FT-CLAIMS-004 | Claims Inbox | Open row preview drawer. | At least one claim exists. | Click row. | Drawer opens with summary, documents, timeline, and action links. | High | Major |
| FT-CASE-001 | Case Review | Case summary displays grounded information. | Claim exists. | Open case review. | Summary, customer, policy, incident, docs, and timeline display. | High | Major |
| FT-CASE-002 | Case Review | Human records decision-related action. | User has permission. | Add note, assign, escalate, or request document. | Action is saved and audit event is created. | High | Critical |
| FT-CASE-003 | Case Review | User without approval permission tries approval action. | Role lacks permission. | Attempt action. | Action is disabled or server rejects with safe message. | High | Critical |
| FT-DOC-001 | Documents | Upload claim document. | Upload enabled. | Upload allowed file type. | Document appears with processing status and audit event. | High | Major |
| FT-DOC-002 | Documents | Unsupported file type. | Upload enabled. | Upload unsupported type. | Validation error appears; file is not processed. | Medium | Major |
| FT-DOC-003 | Documents | Extraction confidence shown. | Document processed. | Open extracted fields. | Fields, confidence, and source document are visible. | High | Major |
| FT-DOC-004 | Documents | Missing required document detected. | Claim checklist configured. | Open document checklist. | Missing documents are clearly marked with next action. | High | Major |
| FT-DOC-005 | Documents | Human corrects extracted field. | User has review permission. | Edit extracted value and save. | Correction saved, original value retained in audit trail. | High | Critical |
| FT-CHANNEL-001 | Channels | Channel overview loads. | Endpoint configured. | Open Channels workspace. | Inbound volumes, unresolved messages, and Not configured states work. | Medium | Major |
| FT-CUST-001 | Customers | Customer overview opens from claim. | Customer linked to claim. | Click customer from claim. | Customer record opens with linked claims and interactions. | Medium | Major |
| FT-LEADS-001 | Sales & Leads | Lead overview loads. | Endpoint configured. | Open Sales & Leads. | Leads render or empty state appears; no fake data. | Medium | Minor |
| FT-KB-001 | Knowledge Base | Approved article can be viewed. | Approved knowledge exists. | Open knowledge item. | Content displays with approval metadata. | Medium | Major |
| FT-AUDIT-001 | Audit | Audit log filters by claim. | Audit events exist. | Filter by claim ID. | Relevant events appear with actor, time, action, and correlation ID. | High | Critical |
| FT-ADMIN-001 | Admin | Provider health visible. | Admin role. | Open Support & Health / Admin provider health. | Providers show healthy, degraded, failed, or Not configured. | High | Major |

## Role-Based Functional Acceptance

| Role | Must Be Able To | Must Not Be Able To |
|---|---|---|
| Admin | Configure workspace, users, providers, and roles. | Bypass audit logging or expose secrets. |
| Claims Officer | Review claims, documents, notes, requests, and assignments. | Final approve restricted actions without permission. |
| Claims Supervisor | Escalate, assign, review queues, and approve configured actions. | Access tenant admin secrets. |
| Underwriter | Review underwriting support information where configured. | Bind policies automatically through AI. |
| Compliance Officer | Review audit, evidence, consent, and exception events. | Modify core operational data unless granted. |
| Customer Service Agent | View customer interactions and draft updates. | Settle claims or make regulated decisions. |
| Executive | View reports and trends. | Edit claim evidence or operational records. |
| Otic Admin / Support | Support approved environments under agreed access rules. | Access client data without approved support process. |

## Acceptance Criteria

- All Critical and High priority MVP functional tests pass before pilot UAT.
- Medium priority tests have documented pass, defer, or workaround status.
- Every failed critical test has an owner, fix target, and retest result.
- Every module has loading, empty, error, unauthorized, and Not configured coverage.
- No client-facing or staff-facing screen relies on fake production data.

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

### WhatsApp operational acceptance journey

Run WA-AC-001–010 from `docs/03-design-architecture/iip_whatsapp_thread_outbound_delivery_spec.md` with authorized channel staff and a separate unauthorized role. Include unmatched inbound manual-link review, case-linked thread reopen, in-window reviewed reply, out-of-window approved template, provider failure, unknown acknowledgement, duplicate callback and late delivered/read callback. Record exact environment, provider/business number, template/language, participant role, permission evidence, timestamps, audit references, accessibility/low-bandwidth observations and named approvers. Provider acceptance must never be scored as delivered.
