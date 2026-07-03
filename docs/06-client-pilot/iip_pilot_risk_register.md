# Pilot Risk Register

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Pilot Risk Register |
| Version | 1.0 |
| Status | Draft |
| Audience | Pilot governance team, delivery lead, client sponsor, operational owner, risk/compliance |
| Date | 2026-06-30 |

This document is part of the Insurance Intelligence Platform (IIP) documentation set. IIP is an Azure-native, human-reviewed insurance operations platform for insurers, brokers, TPAs, HMOs, bancassurance partners, and microinsurance providers. The current MVP direction is motor claims and document intelligence, with WhatsApp-first customer interaction feeding internal insurer workspaces for claims, channels, customers, documents, approvals, audit, and reporting. AI assists with classification, extraction, summarization, triage, routing, drafting, and evidence preparation. Humans retain authority for regulated and consequential decisions such as claim approval, rejection, settlement, fraud determination, payment release, policy binding, and final underwriting decisions.


## 1. Register Purpose

The Pilot Risk Register tracks risks that could affect pilot delivery, adoption, security, compliance, operational value, or client confidence. It should be reviewed weekly during the pilot.

## 2. Risk Scoring

| Score | Probability | Impact |
| --- | --- | --- |
| 1 | Rare | Low effect; minor inconvenience |
| 2 | Unlikely | Small delay or minor workaround |
| 3 | Possible | Material delay, adoption issue, or support burden |
| 4 | Likely | Major pilot disruption or stakeholder concern |
| 5 | Almost certain | Critical disruption, security/compliance exposure, or failed pilot outcome |


## 3. Risk Register Template

| Risk ID | Risk | Probability | Impact | Rating | Mitigation | Owner | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| R-001 | Client users are not available for UAT or training | 3 | 4 | 12 | Book sessions early, nominate backups, track attendance | Client Operational Owner | Open |
| R-002 | Integration access is delayed or unavailable | 3 | 4 | 12 | Use approved manual import or Not configured state; do not fake production data | Client IT / Otic Engineering | Open |
| R-003 | Document samples are low quality or incomplete | 3 | 3 | 9 | Define document guidance and human review correction workflow | Claims Supervisor | Open |
| R-004 | Users over-trust AI outputs | 2 | 5 | 10 | Training, UI labels, refusal behavior, human approval gates | Otic Product / Client Lead | Open |
| R-005 | Security or tenant isolation issue appears during pilot | 2 | 5 | 10 | Security validation, RBAC tests, incident response path | Otic Engineering / Client IT | Open |
| R-006 | Pilot success metrics are not baselined | 3 | 3 | 9 | Approve success measurement plan before go-live | Client Sponsor | Open |
| R-007 | Scope expands beyond MVP without change control | 3 | 4 | 12 | Use change control template and scope control register | Otic Delivery Lead | Open |
| R-008 | Support issues are not triaged quickly | 3 | 3 | 9 | Use issue log, severity matrix, and weekly support review | Otic Support Lead | Open |


## 4. Weekly Risk Review Questions

- [ ] Which risks increased this week?
- [ ] Which risks now require sponsor intervention?
- [ ] Which mitigations are overdue?
- [ ] Which risks should be converted into issues?
- [ ] Which risks affect go-live or closeout decision?
- [ ] Which risks affect AI safety, privacy, or regulated decision boundaries?

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

