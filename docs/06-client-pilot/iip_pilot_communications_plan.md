# Pilot Communications Plan

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Pilot Communications Plan |
| Version | 1.0 |
| Status | Draft |
| Audience | Client sponsor, communications owner, operational owner, support, Otic delivery |
| Date | 2026-06-30 |

This document is part of the Insurance Intelligence Platform (IIP) documentation set. IIP is an Azure-native, human-reviewed insurance operations platform for insurers, brokers, TPAs, HMOs, bancassurance partners, and microinsurance providers. The current MVP direction is motor claims and document intelligence, with WhatsApp-first customer interaction feeding internal insurer workspaces for claims, channels, customers, documents, approvals, audit, and reporting. AI assists with classification, extraction, summarization, triage, routing, drafting, and evidence preparation. Humans retain authority for regulated and consequential decisions such as claim approval, rejection, settlement, fraud determination, payment release, policy binding, and final underwriting decisions.


## 1. Communications Purpose

The communications plan defines how pilot stakeholders are informed before, during, and after the IIP pilot. It covers leadership updates, user notices, training communications, go-live notices, issue communications, planned maintenance, and closeout communications.

## 2. Stakeholder Groups

| Stakeholder Group | Information Needed | Channel | Frequency |
| --- | --- | --- | --- |
| Executive sponsor and leadership | Pilot progress, risks, KPI results, decisions | Email / steering meeting | Weekly or milestone-based |
| Claims users | Training, workflow changes, support path, known issues | Email / Teams / meeting | Before go-live and as needed |
| Supervisors and managers | Adoption, backlog, SLA, escalations, user issues | Meeting / status report | Weekly |
| IT/security | Access, integrations, incidents, maintenance, provider health | Email / technical check-in | As needed |
| Compliance/audit | Audit evidence, AI safety, data handling, incident updates | Meeting / report | Weekly or milestone-based |
| Otic delivery/support | Actions, issues, risks, client decisions | Internal channel / status report | Daily/weekly |


## 3. Communication Templates

### 3.1 Pilot Kickoff Notice

Subject: IIP Pilot Kickoff - Insurance Intelligence Platform

Message:

The IIP pilot is starting for the agreed motor claims and document intelligence workflow. The pilot will help test claim intake, document review, AI-assisted summaries, human review, audit trails, and operational dashboards. AI will assist users but will not make final claim decisions.

### 3.2 Training Invitation

Subject: IIP Pilot Training Session

Message:

You are invited to a role-based IIP training session. The session will cover your pilot workflow, how to use AI assistance safely, how to review documents, and how to raise support issues during the pilot.

### 3.3 Go-Live Notice

Subject: IIP Pilot Go-Live Notice

Message:

The IIP pilot is now live for the agreed pilot users and workflow. Please use the support channel for any issues and remember that all claim decisions remain under human authority.

### 3.4 Issue Communication Notice

Subject: IIP Pilot Issue Update

Message:

An issue has been logged affecting: [area]. Current impact: [impact]. Workaround: [workaround]. Owner: [owner]. Next update: [time/date].

### 3.5 Pilot Closeout Notice

Subject: IIP Pilot Closeout and Next Steps

Message:

The IIP pilot review is complete. The closeout report summarizes outcomes, KPI results, lessons learned, open issues, and the recommended next step.

## 4. Communication Rules

- [ ] Do not communicate that AI approves, rejects, settles, prices, or pays claims.
- [ ] Use clear language that AI assists and humans decide.
- [ ] Communicate known issues honestly with workaround and owner.
- [ ] Use agreed client channels for official communications.
- [ ] Escalate security, privacy, or AI safety concerns immediately.
- [ ] Keep leadership updates decision-oriented and evidence-based.


## 5. Communications Calendar Template

| Date | Audience | Message Type | Owner | Status |
| --- | --- | --- | --- | --- |
|  | All pilot stakeholders | Kickoff notice |  |  |
|  | Pilot users | Training invitation |  |  |
|  | Leadership | Weekly status report |  |  |
|  | Pilot users | Go-live notice |  |  |
|  | Governance team | Closeout report notice |  |  |

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

