# Pilot Sign-Off Form

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Pilot Sign-Off Form |
| Version | 1.0 |
| Status | Draft |
| Audience | Client sponsor, operational owner, IT/security owner, Otic delivery lead, QA lead |
| Date | 2026-06-30 |

This document is part of the Insurance Intelligence Platform (IIP) documentation set. IIP is an Azure-native, human-reviewed insurance operations platform for insurers, brokers, TPAs, HMOs, bancassurance partners, and microinsurance providers. The current MVP direction is motor claims and document intelligence, with WhatsApp-first customer interaction feeding internal insurer workspaces for claims, channels, customers, documents, approvals, audit, and reporting. AI assists with classification, extraction, summarization, triage, routing, drafting, and evidence preparation. Humans retain authority for regulated and consequential decisions such as claim approval, rejection, settlement, fraud determination, payment release, policy binding, and final underwriting decisions.


## 1. Sign-Off Purpose

This form records formal approval at key pilot checkpoints. It should be used for setup approval, UAT approval, training approval, go-live approval, pilot closeout, and phase transition.

## 2. Sign-Off Type

- [ ] Setup / configuration sign-off
- [ ] UAT sign-off
- [ ] Training sign-off
- [ ] Go-live sign-off
- [ ] Pilot closeout sign-off
- [ ] Phase 2 / rollout decision sign-off


## 3. Sign-Off Summary

| Field | Value |
| --- | --- |
| Client name |  |
| Pilot phase |  |
| Environment |  |
| Date |  |
| Sign-off requested by |  |
| Decision | Approve / Approve with Conditions / Reject / Defer |
| Conditions or exceptions |  |


## 4. Readiness Checklist

| Readiness Area | Status | Evidence / Notes |
| --- | --- | --- |
| Pilot scope approved | Complete / Incomplete |  |
| Configuration workbook approved | Complete / Incomplete |  |
| Users and roles configured | Complete / Incomplete |  |
| UAT completed | Complete / Incomplete |  |
| Critical defects resolved | Complete / Incomplete / Accepted |  |
| High defects resolved or accepted | Complete / Incomplete / Accepted |  |
| Training completed | Complete / Incomplete |  |
| Support path confirmed | Complete / Incomplete |  |
| Security checks completed | Complete / Incomplete |  |
| AI human-review guardrails validated | Complete / Incomplete |  |
| Audit evidence available | Complete / Incomplete |  |


## 5. Conditions and Exceptions

| Condition / Exception | Owner | Target Date | Accepted By | Notes |
|---|---|---|---|---|
|  |  |  |  |  |
|  |  |  |  |  |

## 6. Approval Statements

By signing, the approver confirms that the relevant pilot checkpoint has been reviewed and is approved according to the stated decision and any listed conditions. Approval does not authorize AI to perform regulated insurance decisions autonomously.

## Sign-Off

| Role | Name | Decision | Date | Notes |
|---|---|---|---|---|
| Client Executive Sponsor |  | Approve / Reject / Defer |  |  |
| Client Operational Owner |  | Approve / Reject / Defer |  |  |
| Client IT / Security Owner |  | Approve / Reject / Defer |  |  |
| Otic Delivery Lead |  | Approve / Reject / Defer |  |  |
| Otic Product Owner |  | Approve / Reject / Defer |  |  |

## Open Questions and Required Confirmations

- Confirm the first pilot insurer, pilot sponsor, and operational owner.
- Confirm pilot duration, target launch date, and daily/weekly support cadence.
- Confirm which claim types are included in the first pilot beyond motor claims, if any.
- Confirm client-specific document checklist, SLA thresholds, branch structure, and approval workflow.
- Confirm integration readiness for WhatsApp Business API, Microsoft Graph/email, policy lookup, claims system access, and document storage.
- Confirm legal, compliance, privacy, and data residency requirements before production use.
