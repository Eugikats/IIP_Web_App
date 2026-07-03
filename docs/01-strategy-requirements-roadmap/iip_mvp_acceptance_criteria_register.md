# IIP MVP Acceptance Criteria Register

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | MVP Acceptance Criteria Register |
| Version | 1.0 |
| Status | Draft |
| Audience | Product owners, QA leads, engineering leads, delivery managers, pilot sponsors |
| Date | 2026-06-30 |


## 1. Purpose

This register defines what must be true before the IIP MVP can be considered complete enough for demo, UAT, pilot launch, or production hardening. It focuses on functional completion, responsible AI, security, data, integrations, quality, and pilot readiness.


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


## 2. Acceptance Levels

| Level | Meaning |
| --- | --- |
| Demo Ready | Suitable for controlled internal or client demo with clearly identified limitations. |
| UAT Ready | Suitable for client users to validate planned workflows in a non-production environment. |
| Pilot Ready | Suitable for limited real pilot use with support, monitoring, security checks, and rollback plan. |
| Production Candidate | Suitable for broader client rollout after pilot evidence and hardening. |



## 3. MVP Acceptance Criteria

| Criteria ID | Acceptance Criterion | Required Level | Evidence |
| --- | --- | --- | --- |
| AC-001 | Official product naming is consistently Insurance Intelligence Platform (IIP). | Demo Ready | UI/documentation review. |
| AC-002 | Users can authenticate or use approved local dev/demo identity pattern with tenant/workspace context. | Demo Ready | Auth test evidence. |
| AC-003 | Role-aware navigation and protected actions are enforced server-side. | UAT Ready | RBAC test evidence. |
| AC-004 | Health and provider status endpoints work and show Not configured when services are missing. | Demo Ready | Provider health evidence. |
| AC-005 | Claims can be created, listed, searched, filtered, opened, and reviewed. | UAT Ready | Functional test evidence. |
| AC-006 | Motor claim detail shows customer/policy context, timeline, status, assignment, and documents. | UAT Ready | Case review test evidence. |
| AC-007 | Documents can be uploaded, classified, extracted, reviewed, and corrected by humans. | UAT Ready | Document intelligence test evidence. |
| AC-008 | Missing or incomplete document states are visible and actionable. | UAT Ready | Document checklist tests. |
| AC-009 | AI summaries and drafts are clearly assistive and reviewable. | Pilot Ready | AI evaluation and audit evidence. |
| AC-010 | AI cannot approve/reject claims, bind policies, release payment, or accuse fraud. | Pilot Ready | AI red-team test evidence. |
| AC-011 | Approval gates are enforced for consequential actions. | Pilot Ready | Approval workflow tests. |
| AC-012 | Audit records are captured for user actions, AI outputs, document review, approvals, and exports. | Pilot Ready | Audit query evidence. |
| AC-013 | Command Center shows real operational state, not fake production data. | Demo Ready | Dashboard and data-state review. |
| AC-014 | Core integrations show configured, Not configured, degraded, or error states. | UAT Ready | Integration state tests. |
| AC-015 | Security checks pass for tenant isolation, RBAC, secrets, audit, and sensitive data handling. | Pilot Ready | Security validation plan evidence. |
| AC-016 | UAT plan, pilot charter, support model, and release readiness checklist are complete. | Pilot Ready | Signed artifacts. |



## 4. Non-Acceptance Conditions

| Condition | Reason |
| --- | --- |
| AI can make final claim decisions | Violates human-in-loop governance. |
| Mock data appears in staging/production as if real | Damages trust and pilot validity. |
| Frontend hides actions but backend allows them | Security weakness. |
| Provider failure is invisible to users or support | Operational risk. |
| Documents are extracted without confidence or human correction path | Quality and audit risk. |
| No audit trail for important actions | Compliance failure. |
| Pilot success metrics are undefined | Cannot prove value. |



## 5. Open Questions

| ID | Open Question | Acceptance Impact |
| --- | --- | --- |
| OQ-AC-001 | Which criteria must be met for the first external demo versus UAT? | Sets release gate threshold. |
| OQ-AC-002 | Who signs off each acceptance level? | Defines approval process. |
| OQ-AC-003 | What minimum document extraction accuracy/confidence is acceptable for pilot? | Defines document quality threshold. |
| OQ-AC-004 | What pilot usage volume is expected? | Defines performance readiness. |

## 6. Acceptance Evidence Checklist

| Evidence Type | Examples |
| --- | --- |
| Functional test evidence | Screenshots, test results, test IDs, pass/fail logs. |
| API test evidence | Endpoint tests, auth failure tests, validation tests, tenant boundary tests. |
| Security evidence | RBAC checks, secret handling review, audit tests, provider health review. |
| AI safety evidence | Red-team cases, citation checks, prohibited action tests. |
| Document intelligence evidence | Sample document results, confidence review, correction audit logs. |
| UAT evidence | Client tester sign-off, issue log, known issues, accepted limitations. |
| Deployment evidence | Post-deployment validation, monitoring screenshots, rollback plan. |

## 7. Acceptance Sign-Off Matrix

| Acceptance Level | Recommended Sign-Off |
| --- | --- |
| Demo Ready | Product Owner and Engineering Lead |
| UAT Ready | Product Owner, QA Lead, Engineering Lead |
| Pilot Ready | Product Owner, QA Lead, Security Lead, Delivery Lead, Client Sponsor |
| Production Candidate | Leadership, Security/Compliance, Client Sponsor, Support Owner |

## 8. Sample Acceptance Review Agenda

| Agenda Item | Purpose |
| --- | --- |
| Review completed acceptance criteria | Confirm evidence exists. |
| Review failed or partial criteria | Decide fix, defer, or accepted limitation. |
| Review open questions | Assign owners and deadlines. |
| Review known risks | Confirm mitigations. |
| Make readiness decision | Approve, conditionally approve, or block. |
