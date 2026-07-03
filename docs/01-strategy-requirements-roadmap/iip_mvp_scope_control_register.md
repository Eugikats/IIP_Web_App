# IIP MVP Scope Control Register

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | MVP Scope Control Register |
| Version | 1.0 |
| Status | Draft |
| Audience | Product owners, engineering leads, delivery managers, pilot sponsors, QA leads |
| Date | 2026-06-30 |


## 1. Purpose

This register protects the IIP MVP from scope creep. It defines what must be built for the first credible pilot, what is explicitly out of scope, what belongs in Phase 2, and what must not be built too early.


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


## 2. MVP Definition

The MVP is a motor claims and document intelligence workflow for insurer staff. It should prove that IIP can ingest claim context, organize documents, assist staff with AI-supported review, maintain auditability, and support pilot reporting without pretending to replace existing insurer systems.

## 3. In Scope for MVP

| Scope Item | Included Capability | Acceptance Signal |
| --- | --- | --- |
| Core staff shell | Persistent navigation, top bar, tenant/workspace context, global search pattern, role-aware menu | Users can access role-appropriate modules and see current environment. |
| Command Center | Operational metrics, quick links, SLA/document risk indicators, provider status | Users can quickly identify claims needing action. |
| Claims Inbox | Dense table, filters, search, row preview/detail, assignment and escalation states | Claims can be found, opened, reviewed, and routed. |
| Motor claim review | Claim summary, customer/policy context, timeline, documents, AI-assisted review | A claims officer can review a motor claim end-to-end. |
| Document intelligence | Upload, classify, extract, validate, show confidence, identify missing documents | Document completeness can be assessed and reviewed. |
| Channels context | WhatsApp/email conversation context for claim intake and follow-up | Staff can understand customer communication history. |
| AI Copilot | Summaries, extraction assistance, missing-document support, draft updates, risk signals | AI outputs are reviewable, source-grounded, and audited. |
| Approvals | Human review and approval gates for consequential actions | Claims decisions remain human-controlled. |
| Audit and compliance | Audit events for claim, document, AI, approval, and export actions | An auditor can reconstruct who did what and why. |
| Provider health | Health checks, configured/not configured/error states | No silent failed dependencies or fake success states. |
| Pilot reporting | Basic operational and pilot KPIs | Pilot team can measure usage and value. |



## 4. Explicitly Out of Scope for MVP

| Out-of-Scope Item | Reason | Possible Future Phase |
| --- | --- | --- |
| Autonomous claim approval or rejection | Regulated and consequential decision requiring human accountability | Not planned as autonomous functionality. |
| Policy administration replacement | IIP is not a core insurance system | Never as MVP; only integrations where needed. |
| Payment release | High financial risk and controlled by insurer systems | Future integration only with human approval and controls. |
| Full underwriting engine | MVP is motor claims-focused | Phase 2/3 underwriting support module. |
| Full fraud determination engine | AI may flag signals but must not accuse or decide fraud | Future paid add-on with strong governance. |
| Customer login portal | Customers use channels first; staff app is internal | Future self-service portal if required. |
| Advanced BI/Fabric analytics | MVP needs operational metrics first | Phase 2 reporting and analytics expansion. |
| Full offline platform | Can design resilient channel buffering, not full offline operations | Future resilience roadmap. |
| Multi-country regulatory automation | Requires jurisdiction-specific validation | Future localization. |



## 5. Phase 2 Candidate Scope

| Candidate | Description | Dependency |
| --- | --- | --- |
| Customer acquisition module | Lead capture, quote assistance, conversion tracking | Commercial pilot demand and channel integration maturity. |
| Early warning module | Signals for claims trends, provider patterns, operational bottlenecks | Sufficient data volume and validated signal definitions. |
| Fraud signal add-on | Pattern-based signal detection, not final fraud decision | Governance, legal review, and evidence quality. |
| Underwriting support | Risk summaries, document review, policy Q&A, decision support | Underwriting SME requirements and policy integration. |
| Advanced reports | Power BI/Fabric integration and executive scorecards | Data model stabilization. |
| Claims system write-back | Structured export or integration into client claims platform | Client system API availability and approval. |



## 6. Build-Too-Early Guardrails

| Guardrail | Instruction |
| --- | --- |
| Do not optimize for every insurance line first | Motor claims must work before adding medical, life, property, or marine workflows. |
| Do not make AI autonomous | Keep regulated decisions human-owned. |
| Do not hide missing integrations | Use Not configured states and setup guidance. |
| Do not build fake production screens | Use empty states or real API errors instead of mock data outside demo/dev. |
| Do not overbuild analytics before workflow | Workflow completeness and auditability are higher priority than dashboards. |
| Do not create frontend-only permissions | Authorization must be enforced server-side. |



## 7. Open Questions

| ID | Open Question | Decision Needed |
| --- | --- | --- |
| OQ-MVP-001 | Which motor claim document checklist is final for pilot? | Client/Claims SME sign-off. |
| OQ-MVP-002 | Should Sales & Leads be MVP-light or Phase 2? | Pilot sponsor decision. |
| OQ-MVP-003 | Is live WhatsApp required for MVP or can pilot use controlled channel simulation first? | Integration and pilot decision. |
| OQ-MVP-004 | Which pilot KPIs are mandatory for success measurement? | Leadership and client agreement. |

## 8. MVP Boundary Rules by Role

| Role | Boundary Rule |
| --- | --- |
| Product Owner | May prioritize and clarify scope but must use change control for expansion. |
| Engineering Lead | Must refuse unapproved build-too-early features that weaken MVP delivery. |
| AI Agent/Coding Agent | Must follow AGENTS.md guardrails and avoid adding fake workflows or autonomous decisions. |
| QA Lead | Must test against MVP acceptance criteria, not aspirational future scope. |
| Pilot Sponsor | May request changes through change control; pilot-critical requests must be justified. |

## 9. MVP Scope Decision Questions

| Question | Use to Decide |
| --- | --- |
| Does this help a motor claim move through intake, document review, human decision support, audit, or reporting? | If yes, likely MVP candidate. |
| Does this require full core-system replacement or payment control? | If yes, out of MVP scope. |
| Can this be configured instead of custom-built? | If yes, treat as configuration requirement. |
| Can the pilot succeed without it? | If yes, consider Phase 2 or backlog. |
| Does this introduce legal, security, or AI governance risk? | If yes, require review before inclusion. |

## 10. MVP Scope Sign-Off Checklist

| Item | Status |
| --- | --- |
| MVP modules confirmed | To be completed by Product Owner. |
| Out-of-scope items reviewed with stakeholders | To be completed before implementation sprint planning. |
| Phase 2 items captured in backlog | To be completed by Delivery Lead. |
| Open questions assigned owners | To be completed before UAT planning. |
| Pilot-critical integrations confirmed | To be completed with client IT. |
