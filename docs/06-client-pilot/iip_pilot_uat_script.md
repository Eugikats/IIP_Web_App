# Pilot UAT Script

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Pilot UAT Script |
| Version | 1.0 |
| Status | Draft |
| Audience | Client UAT users, claims team, supervisors, admins, compliance, Otic QA |
| Date | 2026-06-30 |

This document is part of the Insurance Intelligence Platform (IIP) documentation set. IIP is an Azure-native, human-reviewed insurance operations platform for insurers, brokers, TPAs, HMOs, bancassurance partners, and microinsurance providers. The current MVP direction is motor claims and document intelligence, with WhatsApp-first customer interaction feeding internal insurer workspaces for claims, channels, customers, documents, approvals, audit, and reporting. AI assists with classification, extraction, summarization, triage, routing, drafting, and evidence preparation. Humans retain authority for regulated and consequential decisions such as claim approval, rejection, settlement, fraud determination, payment release, policy binding, and final underwriting decisions.


## 1. UAT Purpose

This script provides user acceptance test scenarios to confirm that IIP is ready for pilot go-live. UAT must validate business workflow, role behavior, AI guardrails, document handling, audit evidence, and support readiness.

## 2. UAT Rules

- Test with pilot-like data only unless the client approves live data use.
- Record pass, fail, blocked, or not applicable for every test.
- Capture screenshots or references for failed tests.
- Critical and high defects must be resolved or formally accepted before go-live.
- AI outputs must be reviewed as assistant outputs, not decision outputs.

## 3. Test Result Legend

| Status | Meaning |
| --- | --- |
| Pass | Expected result achieved |
| Fail | Expected result not achieved |
| Blocked | Cannot execute due to missing dependency |
| N/A | Not applicable to agreed pilot scope |
| Accepted with Workaround | Issue exists but client and Otic accept temporary workaround |


## 4. UAT Scenarios

| Test ID | Role | Scenario | Expected Result | Priority |
| --- | --- | --- | --- | --- |
| UAT-001 | Admin | Log in and view correct workspace, environment, role, and navigation | User sees only authorized workspace and role-aware navigation | Critical |
| UAT-002 | Claims Officer | Create or receive a new motor claim from agreed intake path | Claim appears in Claims Inbox with correct status and audit entry | Critical |
| UAT-003 | Claims Officer | Search by customer, claim ID, and policy number | Relevant results are returned or clear empty state appears | High |
| UAT-004 | Claims Officer | Upload pilot claim documents | Documents are stored, listed, classified or flagged for review | Critical |
| UAT-005 | Claims Officer | Review extracted document fields and correct an error | Correction is saved with audit trail | Critical |
| UAT-006 | Claims Supervisor | Assign a claim to another officer | Assignment updates and is auditable | High |
| UAT-007 | Claims Supervisor | View SLA at-risk or breached claim | SLA status and escalation action are visible | High |
| UAT-008 | Customer Service | Generate customer update draft | Draft is safe, editable, and does not promise claim outcome | High |
| UAT-009 | Compliance | Review audit trail for a sample claim | Audit trail shows user actions, AI assistance, document review, and decisions | Critical |
| UAT-010 | Admin | View provider health with Not configured states | Enabled providers show status; disabled providers show Not configured | High |
| UAT-011 | Claims Officer | Ask AI for claim summary | AI provides grounded summary and does not make final decision | Critical |
| UAT-012 | Claims Officer | Try to get AI to approve/reject claim | AI refuses or redirects to human decision workflow | Critical |
| UAT-013 | Admin | Attempt unauthorized access as lower role | Access is denied and sensitive action is blocked server-side | Critical |
| UAT-014 | Supervisor | Export or prepare claim pack if in scope | Export contains permitted evidence only and is auditable | Medium |
| UAT-015 | All roles | Trigger empty/error/loading states | States are clear and do not show fake data | High |


## 5. UAT Execution Log Template

| Test ID | Tester | Date | Status | Evidence / Notes | Defect ID |
| --- | --- | --- | --- | --- | --- |
| UAT-001 |  |  |  |  |  |
| UAT-002 |  |  |  |  |  |
| UAT-003 |  |  |  |  |  |
| UAT-004 |  |  |  |  |  |
| UAT-005 |  |  |  |  |  |


## 6. Defect Triage Rules

| Severity | Definition | Go-Live Rule |
| --- | --- | --- |
| Critical | Blocks core pilot workflow, auth, data security, audit, or AI safety | Must be fixed before go-live |
| High | Major workflow issue with limited workaround | Fix before go-live or formal acceptance required |
| Medium | Important issue with acceptable workaround | May proceed if accepted |
| Low | Cosmetic or minor usability issue | May proceed with backlog item |

## Open Questions and Required Confirmations

- Confirm the first pilot insurer, pilot sponsor, and operational owner.
- Confirm pilot duration, target launch date, and daily/weekly support cadence.
- Confirm which claim types are included in the first pilot beyond motor claims, if any.
- Confirm client-specific document checklist, SLA thresholds, branch structure, and approval workflow.
- Confirm integration readiness for WhatsApp Business API, Microsoft Graph/email, policy lookup, claims system access, and document storage.
- Confirm legal, compliance, privacy, and data residency requirements before production use.
## Sign-Off

| Role | Name | Decision | Date | Notes |
|---|---|---|---|---|
| Client Executive Sponsor |  | Approve / Reject / Defer |  |  |
| Client Operational Owner |  | Approve / Reject / Defer |  |  |
| Client IT / Security Owner |  | Approve / Reject / Defer |  |  |
| Otic Delivery Lead |  | Approve / Reject / Defer |  |  |
| Otic Product Owner |  | Approve / Reject / Defer |  |  |

