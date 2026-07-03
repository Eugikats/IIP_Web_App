# IIP Assumptions, Constraints & Open Questions Register

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Assumptions, Constraints & Open Questions Register |
| Version | 1.0 |
| Status | Draft |
| Audience | Product owners, architects, delivery managers, client stakeholders, QA leads |
| Date | 2026-06-30 |


## 1. Purpose

This register consolidates assumptions, constraints, unresolved questions, required confirmations, and dependency risks for IIP planning and delivery.


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


## 2. Assumptions Register

| Assumption ID | Assumption | Why It Matters | Validation Needed |
| --- | --- | --- | --- |
| A-001 | The first MVP remains focused on motor claims and document intelligence. | Controls implementation scope. | Confirm with pilot sponsor. |
| A-002 | Customers interact primarily through external channels rather than internal app login. | Defines UX and auth scope. | Confirm with client service model. |
| A-003 | IIP will run on Azure-first architecture. | Guides infrastructure and security. | Confirm client Azure requirements and region. |
| A-004 | Pilot can begin with limited or read-only core-system integration if needed. | Reduces integration risk. | Confirm client IT acceptance. |
| A-005 | Human reviewers remain accountable for final insurance decisions. | Controls AI governance. | Confirm compliance/legal acceptance. |
| A-006 | Document samples will be available for extraction testing. | Required for document intelligence quality. | Collect sample pack. |
| A-007 | Role-aware navigation maps to real user roles. | Required for RBAC and training. | Confirm role list with client. |



## 3. Constraints Register

| Constraint ID | Constraint | Impact |
| --- | --- | --- |
| C-001 | No autonomous claims approval, rejection, payment, pricing, or fraud accusation. | AI features must remain assistive and reviewable. |
| C-002 | No mock data in staging or production. | Demo/dev data must not leak into real environments. |
| C-003 | Server-side authorization is required. | Frontend role hiding is not sufficient. |
| C-004 | Auditability is mandatory. | Important actions must generate traceable records. |
| C-005 | Integration availability may depend on client systems. | Roadmap must support staged integrations. |
| C-006 | Data residency and security expectations may vary by client. | Deployment plan must remain configurable. |



## 4. Open Questions Register

| Open Question ID | Question | Category | Needed By | Owner |
| --- | --- | --- | --- | --- |
| OQ-001 | Who is the confirmed first pilot insurer? | Commercial | Pilot planning | Leadership |
| OQ-002 | Which deployment model will be used for the pilot? | Deployment | Environment setup | Architecture |
| OQ-003 | Which motor claim categories and documents are in pilot scope? | Product/Claims | MVP build | Claims SME |
| OQ-004 | Which integrations are mandatory for pilot launch? | Integration | Roadmap execution | Integration Lead |
| OQ-005 | What are final pilot success metrics? | Pilot | Pilot launch | Product/Client Sponsor |
| OQ-006 | What are support hours and escalation contacts during pilot? | Support | Pilot launch | Delivery Lead |
| OQ-007 | Which regulatory or client security questionnaire must be completed? | Security | Pre-production | Security Lead |
| OQ-008 | What data retention period applies to pilot documents, claims, conversations, and audit logs? | Compliance | Architecture hardening | Compliance Lead |
| OQ-009 | Which final user roles will be configured for pilot? | RBAC | UAT | Client Admin/Product |
| OQ-010 | Will pricing be pilot-only, subscription, usage-based, or private offer? | Commercial | Proposal | Leadership |



## 5. Review Cadence

| Cadence | Purpose |
| --- | --- |
| Weekly during implementation | Close delivery blockers and validate assumptions. |
| Before UAT | Resolve scope, roles, data, and integration questions. |
| Before pilot launch | Confirm support model, success metrics, security gates, and environment readiness. |
| Pilot closeout | Convert unresolved items into roadmap improvements. |

## 6. Dependency Register

| Dependency ID | Dependency | Type | Impact if Delayed | Mitigation |
| --- | --- | --- | --- | --- |
| D-001 | Pilot client decision | Commercial | Configuration, data, and UAT cannot finalize. | Use generic MVP config until confirmed. |
| D-002 | Motor claim document samples | Product/Data | Document extraction quality cannot be validated. | Create synthetic samples and request real anonymized examples. |
| D-003 | Azure environment access | Deployment | Implementation and testing may stall. | Start with local/dev environment and environment setup guide. |
| D-004 | Integration credentials | Integration | Channel and core-system tests may be blocked. | Use Not configured state and controlled stubs in dev only. |
| D-005 | Client role list | RBAC | Permissions and training cannot finalize. | Use default roles and validate in UAT. |

## 7. Open Question Closure Rules

| Rule | Description |
| --- | --- |
| Assign an owner | Every open question needs an accountable owner. |
| Assign a needed-by date | Questions without dates tend to remain unresolved. |
| Classify impact | Mark whether it affects scope, design, build, QA, release, pilot, or commercial. |
| Convert to decision | When answered, add outcome to the Product Decision Log if material. |
| Update affected docs | Closed questions may require PRD, FSD, roadmap, backlog, or QA updates. |

## 8. Register Review Checklist

| Question | Expected Action |
| --- | --- |
| Are any assumptions now invalid? | Update the register and affected documents. |
| Are any open questions blocking the next milestone? | Escalate. |
| Are any dependencies late? | Create mitigation plan. |
| Are any constraints being violated? | Stop work and correct course. |
