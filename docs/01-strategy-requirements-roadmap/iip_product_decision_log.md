# IIP Product Decision Log

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Product Decision Log |
| Version | 1.0 |
| Status | Draft |
| Audience | Leadership, product owners, architects, delivery managers, engineering leads |
| Date | 2026-06-30 |


## 1. Purpose

This decision log records key product decisions that should guide future planning, implementation, QA, deployment, and client communication. It is intended to reduce repeated debate and prevent coding agents or implementation teams from reintroducing superseded assumptions.


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


## 2. Decision Log

| Decision ID | Decision | Status | Rationale | Implementation Impact |
| --- | --- | --- | --- | --- |
| DEC-001 | Use Insurance Intelligence Platform (IIP) as the official product name | Confirmed | The name is clearer, broader, and more client-ready. | Use IIP consistently across documents, UI, repositories, proposals, and architecture references. |
| DEC-002 | Treat ACIP/Agentic naming as superseded legacy context | Confirmed | Older drafts used ACIP, but the official name is IIP. | Do not introduce ACIP in new documents unless explaining legacy references. |
| DEC-003 | MVP focuses on motor claims and document intelligence | Confirmed | A claims/document MVP is practical, demonstrable, and high-value. | Prioritize Claims, Documents, Channels context, Audit, and Command Center. |
| DEC-004 | Customers do not log into the internal staff application | Confirmed | IIP staff app is for insurer/Otic users; customers use external channels. | Design customer workflows through WhatsApp/email/web/SMS and internal staff views. |
| DEC-005 | AI assists, humans decide | Confirmed | Insurance decisions are consequential and require accountability. | Every AI output must be reviewable, auditable, and non-binding. |
| DEC-006 | Azure-first architecture | Confirmed | Enterprise insurers require security, identity, data, and operational trust. | Use Azure SQL, Blob, Key Vault, AI Search, Azure OpenAI, Document Intelligence, Service Bus, App Insights, Entra ID. |
| DEC-007 | Support both Managed Application and SaaS paths | Confirmed direction | Enterprise customers may need customer-tenant deployment while demos/pilots may need SaaS. | Keep deployment abstraction and tenant isolation clean. |
| DEC-008 | No mock data in staging/production | Confirmed | Trustworthy implementation requires truthful states. | Use real data, empty states, error states, or Not configured states. |
| DEC-009 | Role-aware navigation must not be frontend-only security | Confirmed | Browser-owned permission checks are insufficient. | Backend authorization and audit logging must enforce permissions. |
| DEC-010 | Start with controlled integrations | Confirmed direction | Integration risk can derail MVP if uncontrolled. | Prioritize WhatsApp/email/Graph, Blob, AI, SQL, and optional read-only policy lookup. |



## 3. Decision Categories

| Category | Key Decisions |
| --- | --- |
| Product identity | DEC-001, DEC-002 |
| MVP scope | DEC-003, DEC-004 |
| AI governance | DEC-005 |
| Architecture/deployment | DEC-006, DEC-007 |
| Engineering guardrails | DEC-008, DEC-009 |
| Integration sequencing | DEC-010 |



## 4. Decision Review Triggers

A decision should be reviewed only when one of the following occurs:

- Pilot client requirements materially change the MVP scope.
- Regulatory/legal review requires a change in AI or audit behavior.
- Client IT requires a different deployment model.
- A confirmed integration is unavailable or unsafe for MVP.
- Commercial packaging changes the module sequence.

## 5. Open Questions

| ID | Open Question | Affected Decisions |
| --- | --- | --- |
| OQ-DEC-001 | Which deployment path will the first client pilot use? | DEC-007 |
| OQ-DEC-002 | Which integrations are mandatory for pilot launch rather than Phase 2? | DEC-010 |
| OQ-DEC-003 | What support model will be offered during the pilot? | DEC-007, DEC-010 |

## 6. Decision Record Template

| Field | Guidance |
| --- | --- |
| Decision ID | Sequential identifier, e.g., DEC-011. |
| Decision Title | Short decision label. |
| Decision Statement | Clear statement of what has been decided. |
| Options Considered | List alternatives considered. |
| Reason for Decision | Explain business, technical, compliance, or delivery rationale. |
| Decision Owner | Person or group accountable for decision. |
| Date | Date decision was made. |
| Review Trigger | When decision should be revisited. |
| Implementation Impact | What must change in documents, backlog, code, tests, or communications. |

## 7. Additional Decisions to Capture Next

| Decision Needed | Why It Matters |
| --- | --- |
| Final pilot client | Determines configuration, governance, and commercial terms. |
| Final deployment model | Determines architecture, security review, Azure setup, and cost. |
| Final document checklist | Determines document intelligence configuration and test pack. |
| Final role list | Determines RBAC matrix and training materials. |
| Final support model | Determines pilot confidence and escalation flow. |
| Final success KPIs | Determines pilot value proof. |

## 8. Decision Governance

| Practice | Rule |
| --- | --- |
| Document before build | Major decisions should be captured before coding agents implement them. |
| Update linked artifacts | A decision may require updates to PRD, FSD, API spec, backlog, QA plan, or training material. |
| Separate decision from opinion | Only confirmed decisions enter the log; suggestions become open questions. |
| Use review triggers | Do not reopen decisions unless a trigger is met. |
