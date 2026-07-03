# IIP Product Risk Register

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Product Risk Register |
| Version | 1.0 |
| Status | Draft |
| Audience | Product owners, delivery managers, risk leads, architects, QA leads, pilot sponsors |
| Date | 2026-06-30 |


## 1. Purpose

This register tracks product, technical, compliance, AI, delivery, deployment, integration, and pilot risks for IIP. It is intended to be reviewed throughout roadmap planning, implementation, UAT, pilot launch, and pilot closeout.


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


## 2. Risk Scale

| Level | Probability | Impact |
| --- | --- | --- |
| Low | Unlikely or limited exposure | Minor schedule, user, or operational effect. |
| Medium | Possible under normal delivery conditions | Could affect scope, timeline, client confidence, or quality. |
| High | Likely or serious exposure | Could block release, pilot, security acceptance, or client adoption. |



## 3. Risk Register

| Risk ID | Risk | Category | Probability | Impact | Mitigation | Owner |
| --- | --- | --- | --- | --- | --- | --- |
| R-001 | MVP scope expands beyond motor claims before core workflow is stable | Product | Medium | High | Use MVP Scope Control Register and change control gates. | Product Owner |
| R-002 | Client expects IIP to replace core insurance systems | Commercial/Product | Medium | High | Position IIP as intelligence and operations layer, not core replacement. | Leadership/Commercial |
| R-003 | AI outputs are treated as final decisions by users | AI/Compliance | Medium | High | Use visible human review gates, disclaimers, audit logs, and permission controls. | AI Governance Lead |
| R-004 | Insufficient document samples reduce extraction quality | Document Intelligence | High | High | Collect representative samples and build document test pack before pilot. | Document Lead/Client SME |
| R-005 | Live integration dependencies delay pilot | Integration | Medium | High | Use phased integration: configured/not configured states, controlled import, read-only lookup first. | Integration Lead |
| R-006 | Tenant isolation or RBAC defects expose data incorrectly | Security | Low | High | Server-side auth tests, tenant boundary tests, audit checks, security validation. | Security Lead |
| R-007 | Production uses mock/demo data accidentally | Quality | Medium | High | Environment gates, no mock production policy, release readiness checklist. | Engineering Lead |
| R-008 | Pilot users are not trained on human-in-loop model | Adoption | Medium | Medium | Role-based training and user guide reinforcement. | Delivery Lead |
| R-009 | Claims SMEs are unavailable for workflow validation | Delivery | Medium | Medium | Schedule SME review gates and pre-agree decisions. | Project Manager |
| R-010 | Azure cost or capacity assumptions are not validated | Deployment | Medium | Medium | Create environment setup and cost monitoring plan. | Cloud Lead |
| R-011 | WhatsApp/channel message handling loses context | Integration/Operations | Medium | High | Use idempotency, message correlation, retries, and audit events. | Integration Lead |
| R-012 | Pilot success metrics are not agreed early | Pilot | Medium | High | Create Pilot Success Measurement Plan before launch. | Product/Client Sponsor |



## 4. Active Controls

| Control | Risk Reduced |
| --- | --- |
| Human-in-the-loop approval model | R-003 |
| MVP Scope Control Register | R-001 |
| Provider health and Not configured states | R-005, R-007 |
| Security & Compliance Document plus QA/Test Plan | R-006 |
| Document test pack and extraction review workflow | R-004 |
| Pilot success measurement plan | R-012 |



## 5. Open Questions

| ID | Open Question | Risk Impact |
| --- | --- | --- |
| OQ-RISK-001 | Who owns risk review cadence during implementation? | Risks may not be actively managed. |
| OQ-RISK-002 | Which risk tool or tracker will be used? | Risk evidence may be scattered. |
| OQ-RISK-003 | What is the pilot client tolerance for manual workaround during integration setup? | Impacts launch approach. |

## 6. Risk Response Options

| Response | When to Use |
| --- | --- |
| Avoid | When risk violates human-in-loop, security, compliance, or client trust principles. |
| Mitigate | When risk can be reduced with controls, tests, training, or phased rollout. |
| Transfer | When responsibility belongs to client IT, integration provider, or cloud provider. |
| Accept | When risk is low, visible, and has a workaround. |
| Escalate | When risk may block pilot, client confidence, security approval, or release. |

## 7. Risk Review Agenda Template

| Agenda Item | Description |
| --- | --- |
| New risks | Capture new risks from development, QA, client, or support discussions. |
| Changed risks | Update probability, impact, owner, or mitigation status. |
| Top blockers | Review risks that can block UAT, pilot, or release. |
| Mitigation actions | Assign actions and dates. |
| Escalations | Identify risks requiring leadership or client decision. |

## 8. Risk Indicators to Monitor

| Indicator | Possible Risk |
| --- | --- |
| Repeated document extraction errors | Document sample quality or model mismatch. |
| Many Not configured providers near UAT | Integration readiness risk. |
| High number of change requests | Scope creep or stakeholder misalignment. |
| User confusion about AI recommendations | Training or UX governance risk. |
| Frequent auth/tenant test failures | Security readiness risk. |
| No baseline KPI data | Pilot value measurement risk. |
