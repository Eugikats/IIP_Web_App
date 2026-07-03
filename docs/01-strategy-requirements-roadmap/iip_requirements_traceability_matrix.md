# IIP Requirements Traceability Matrix

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Requirements Traceability Matrix |
| Version | 1.0 |
| Status | Draft |
| Audience | Product owners, business analysts, QA leads, solution architects, engineering leads |
| Date | 2026-06-30 |


## 1. Purpose

This matrix links IIP requirements to modules, APIs, data entities, UI screens, test coverage, and release gates. It is a working control document for preventing gaps between strategy, requirements, design, architecture, implementation, QA, and release readiness.


This document uses the existing IIP project context and prior documentation pack: Master Product Context, PRD, FSD, Technical Architecture Document, System Integration Strategy, UI/UX Handoff, API Specification, Data Model, Security & Compliance Document, QA/Test Plan, Deployment Plan, User/Admin Guide, and Implementation Roadmap.

Confirmed project baseline:
- Official product name: Insurance Intelligence Platform (IIP).
- IIP is an Azure-native, human-reviewed insurance operations platform, not a replacement for core insurance systems.
- MVP focus: motor claims and document intelligence for a mid-to-large insurer pilot.
- Customer engagement is WhatsApp-first and channel-led; customers do not log into the internal staff application.
- Internal modules include Command Center, Channels, Customers, Sales & Leads, Claims, Documents, Knowledge Base, Approvals, Compliance & Audit, Reports & Analytics, Integrations, Admin Settings, and Support & Health.
- The internal UI is a role-aware staff workspace with persistent navigation, a top bar, global search, operational states, and a contextual Copilot panel.
- The platform uses Next.js/React/TypeScript/Tailwind on the frontend and .NET 8/Azure services on the backend.
- AI assists with extraction, summarization, classification, routing, drafting, risk signals, and evidence preparation; humans make regulated or consequential decisions.


## 2. Traceability Method

Each requirement should be traceable through the full implementation chain:

Requirement -> Functional module -> API/API contract -> Data entity -> UI screen -> Test case -> Release gate.

Where a detail is not yet confirmed, it is marked as **Requires Confirmation** or **Open Question**.

## 3. Requirements Traceability Matrix

| Requirement ID | Requirement / Capability | FSD Module | API / Backend Coverage | Data Entity | UI Coverage | Test Coverage | Release Gate |
| --- | --- | --- | --- | --- | --- | --- | --- |
| REQ-001 | Staff authentication and role-aware access | Admin, Claims, Channels, Documents, Approvals, Audit | Entra/MSAL, tenant headers, RBAC middleware | User, Role, Tenant, Workspace | Login, Top Bar, Navigation, Admin Settings | Auth, RBAC, tenant boundary tests | Release Gate 1 |
| REQ-002 | Command Center operational visibility | Command Center | GET /api/admin/provider-health, module overview APIs | Claim, Task, Approval, AuditEvent | Command Center | Dashboard state tests, empty/error tests | Release Gate 2 |
| REQ-003 | Motor claim intake from channels | Channels, Claims | GET /api/v1/channels/overview, POST /api/cases | ChannelConversation, Claim, Customer | Channels, Claim Create | FNOL workflow tests | Release Gate 2 |
| REQ-004 | Claims inbox and review | Claims | GET /api/cases, GET /api/cases/{caseId} | Claim, Policy, Customer, Task | Claims Inbox, Case Review | Claims table, filter, detail tests | Release Gate 2 |
| REQ-005 | Document upload and classification | Documents | Document upload/extraction endpoints - Requires Confirmation | Document, DocumentExtraction | Documents, Case Documents | OCR/classification tests | Release Gate 2 |
| REQ-006 | Document completeness checks | Documents, Claims | Document checklist endpoints - Requires Confirmation | DocumentChecklist, Claim, Document | Document Review, Claim Detail | Missing document tests | Release Gate 2 |
| REQ-007 | AI summary and recommendations | Copilot, Claims, Documents | AI interaction/agent run endpoints - Requires Confirmation | AIInteraction, AgentRun, AuditEvent | Copilot Panel, Claim Detail | AI safety/evidence tests | Release Gate 3 |
| REQ-008 | Human approval gates | Approvals | Approval endpoints - Requires Confirmation | Approval, Task, AuditEvent | Approvals, Claim Review | Approval workflow tests | Release Gate 3 |
| REQ-009 | Customer update drafting | Channels, Claims | Message draft endpoints - Requires Confirmation | ChannelConversation, Customer, AuditEvent | Channel Thread, Copilot | Draft review and send approval tests | Release Gate 3 |
| REQ-010 | Auditability | Compliance & Audit | Audit query endpoints - Requires Confirmation | AuditEvent, AIInteraction, Approval | Audit Log | Audit completeness tests | Release Gate 3 |
| REQ-011 | Provider health and Not configured states | Support & Health, Admin | GET /health, /health/live, /health/ready, /api/admin/provider-health | IntegrationEvent, ProviderStatus | Support & Health | Provider state tests | Release Gate 1 |
| REQ-012 | Reports and analytics | Reports | Reports endpoints - Requires Confirmation | Claim, DocumentExtraction, AuditEvent | Reports | Metric accuracy tests | Release Gate 4 |



## 4. Release Gate Definitions

| Gate | Meaning | Minimum Evidence |
| --- | --- | --- |
| Release Gate 1 | Foundation ready | Authentication, tenant context, health checks, provider health, base navigation, environment readiness. |
| Release Gate 2 | Core MVP workflows ready | Claims intake, claims inbox, document handling, customer/channel context, empty/error states. |
| Release Gate 3 | Governed AI and approval ready | AI outputs are auditable, human review gates work, approvals are enforced, audit logs are queryable. |
| Release Gate 4 | Pilot reporting and operational readiness | Reports, support states, monitoring, pilot metrics, UAT evidence, release readiness checklist. |



## 5. Coverage Summary

| Coverage Area | Current State | Action Needed |
| --- | --- | --- |
| Product requirements | Core MVP requirements are defined across PRD/FSD | Maintain traceability as implementation evolves. |
| API coverage | Initial APIs exist for health, provider health, cases, and selected overview endpoints | Confirm final endpoints for documents, approvals, AI, audit, reports, and messages. |
| Data model coverage | Core entities exist conceptually | Confirm physical database schema and EF Core migration naming. |
| UI coverage | Core staff app modules are defined | Map every screen state to test cases and role permissions. |
| QA coverage | QA/Test Plan exists | Create execution-level test cases and link each test ID to requirements. |



## 6. Open Questions

| ID | Open Question | Impact |
| --- | --- | --- |
| OQ-RTM-001 | What final endpoint names will be used for document upload, classification, extraction, and review? | Blocks final API test coverage. |
| OQ-RTM-002 | What final endpoint names will be used for AI AgentRun and AIInteraction storage? | Blocks AI audit traceability. |
| OQ-RTM-003 | Will pilot require integration with a live policy admin or claims system? | Affects release gate scope and test data. |
| OQ-RTM-004 | What test management tool will hold final execution status? | Affects evidence collection. |

## 7. Traceability Maintenance Rules

| Rule | Description |
| --- | --- |
| One requirement, many links | A requirement may connect to multiple modules, APIs, screens, entities, and tests. |
| No orphan API | Every API should support a requirement or platform control. |
| No orphan screen | Every screen should support a workflow, decision, configuration, or operational control. |
| No hidden decision | Every regulated or consequential action needs a human reviewer and audit event. |
| No untested release gate | Every release gate must have test evidence before sign-off. |

## 8. Additional Traceability Rows for Later Expansion

| Requirement ID | Requirement / Capability | Likely Phase | Traceability Note |
| --- | --- | --- | --- |
| REQ-013 | Client configuration management | MVP/Pilot | Maps to Admin Settings, configuration data, UAT setup, and pilot configuration workbook. |
| REQ-014 | Knowledge Base and policy Q&A | Phase 1/2 | Maps to AI/RAG Knowledge Architecture and citation tests. |
| REQ-015 | Claims export pack | MVP/Pilot | Maps to Claim, Document, AuditEvent, Approval, export workflow, and audit tests. |
| REQ-016 | Fraud signal add-on | Phase 2 | Must be limited to signal/risk flagging and never final accusation. |
| REQ-017 | Early warning analytics | Phase 2 | Requires sufficient operational data and validated metrics. |
| REQ-018 | Underwriting support | Phase 2/3 | Requires underwriting-specific requirements and policy integration. |

## 9. RTM Review Checklist

| Review Area | Checklist |
| --- | --- |
| Requirements | Every PRD requirement has an ID and status. |
| Functional design | Every requirement maps to an FSD module or is marked deferred. |
| API design | Every interactive workflow has an API path or Requires Confirmation. |
| Data design | Every saved object maps to a data entity. |
| QA | Every MVP requirement maps to at least one test case or planned test case. |
| Release | Every MVP requirement maps to a release gate. |
