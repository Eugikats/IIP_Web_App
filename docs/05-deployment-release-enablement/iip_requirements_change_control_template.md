# IIP Requirements Change Control Template

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Requirements Change Control Template |
| Version | 1.0 |
| Status | Draft |
| Audience | Product owners, delivery managers, client stakeholders, engineering leads, QA leads |
| Date | 2026-06-30 |


## 1. Purpose

This template defines how new requirements, scope changes, workflow changes, integration changes, and client requests should be captured, assessed, approved, deferred, or rejected during IIP delivery.


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


## 2. Change Control Principles

- Protect the motor claims and document intelligence MVP.
- Do not allow unapproved changes to enter development.
- Separate pilot-critical changes from future roadmap items.
- Record user value, risk, effort, dependencies, and release impact.
- Preserve human-in-the-loop, security, audit, and tenant isolation requirements.

## 3. Change Request Form

| Field | Description |
| --- | --- |
| Change Request ID | Unique ID, e.g., CR-001. |
| Submitted By | Name, role, organization. |
| Date Submitted | Date of request. |
| Module / Workflow | Claims, Documents, Channels, AI, Reports, Admin, Integrations, etc. |
| Requested Change | Clear description of what should change. |
| Business Reason | Why this is needed. |
| User Impact | Who benefits or is affected. |
| Priority Requested | Critical, High, Medium, Low. |
| Pilot Critical? | Yes/No. If yes, explain why pilot cannot continue without it. |
| Security / Compliance Impact | Does this affect data, privacy, audit, decisions, approvals, or access? |
| AI Governance Impact | Does this affect AI prompts, tools, outputs, decisions, evidence, or review? |
| Integration Impact | Systems, channels, APIs, data mappings affected. |
| Estimated Effort | Small, Medium, Large, Requires discovery. |
| Recommended Disposition | Approve, defer, reject, split, or requires discovery. |
| Decision Owner | Person/group authorized to decide. |
| Target Release | MVP, Pilot, Phase 2, backlog. |
| Final Decision | Approved, rejected, deferred, withdrawn. |
| Decision Date | Date of final decision. |



## 4. Assessment Checklist

| Question | Answer |
| --- | --- |
| Does the change support the MVP outcome? | Yes/No/Requires Confirmation |
| Does it introduce autonomous AI decisioning? | Yes/No - must be No unless explicitly redesigned under governance. |
| Does it affect tenant isolation or RBAC? | Yes/No/Requires Security Review |
| Does it need new data entities or migrations? | Yes/No/Requires Architecture Review |
| Does it require new integrations? | Yes/No/Requires Integration Review |
| Does it affect QA coverage or release gates? | Yes/No/Requires QA Update |
| Can it be configured instead of custom-built? | Yes/No/Requires Product Review |



## 5. Approval Matrix

| Change Type | Recommended Approver |
| --- | --- |
| Minor wording/UI copy/configuration | Product Owner |
| MVP scope change | Product Owner plus Delivery Lead |
| Architecture or integration change | Solution Architect plus Engineering Lead |
| Security, compliance, or data access change | Security/Compliance Lead |
| AI behavior or tool permission change | AI Governance Lead |
| Commercial/client commitment change | Leadership/Commercial Owner |



## 6. Open Questions

| ID | Open Question | Decision Needed |
| --- | --- | --- |
| OQ-CR-001 | Who is the final change authority during the pilot? | Name decision owner. |
| OQ-CR-002 | What tool will store approved and rejected change requests? | Select Planner, Azure DevOps, Jira, or another tracker. |
| OQ-CR-003 | What SLA applies to change request review? | Define review cadence. |

## 7. Change Decision Outcomes

| Outcome | Meaning | Next Step |
| --- | --- | --- |
| Approved for MVP | Change is necessary for MVP or pilot success. | Add to backlog with owner, release, acceptance criteria, and tests. |
| Approved for Phase 2 | Change is valuable but not MVP-critical. | Add to roadmap with priority. |
| Requires Discovery | Impact or effort is unclear. | Assign analysis task before decision. |
| Rejected | Change conflicts with scope, security, strategy, or feasibility. | Record rationale. |
| Split | Part can be done now; rest moves later. | Create separate backlog items. |

## 8. Impact Analysis Template

| Impact Area | Questions to Answer |
| --- | --- |
| Product | Does this change the MVP promise or module boundaries? |
| Workflow | Does this change user steps, approvals, status, or SLA logic? |
| Data | Does this need schema changes or new retention requirements? |
| API | Does this require new endpoints, payloads, or permissions? |
| Security | Does this expose data, change roles, or affect secrets? |
| AI | Does this change prompts, tools, citations, confidence, or human review? |
| QA | What new tests are required? |
| Release | Can this fit current release without threatening readiness? |

## 9. Change Log Table

| CR ID | Date | Request | Decision | Target Release | Owner | Status |
| --- | --- | --- | --- | --- | --- | --- |
| CR-001 | TBD | TBD | TBD | TBD | TBD | Open |
| CR-002 | TBD | TBD | TBD | TBD | TBD | Open |
| CR-003 | TBD | TBD | TBD | TBD | TBD | Open |
