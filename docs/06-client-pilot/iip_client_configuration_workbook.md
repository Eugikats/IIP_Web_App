# Client Configuration Workbook

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Client Configuration Workbook |
| Version | 1.0 |
| Status | Draft |
| Audience | Client operational owner, claims leadership, IT/security, admins, Otic implementation team |
| Date | 2026-06-30 |

This document is part of the Insurance Intelligence Platform (IIP) documentation set. IIP is an Azure-native, human-reviewed insurance operations platform for insurers, brokers, TPAs, HMOs, bancassurance partners, and microinsurance providers. The current MVP direction is motor claims and document intelligence, with WhatsApp-first customer interaction feeding internal insurer workspaces for claims, channels, customers, documents, approvals, audit, and reporting. AI assists with classification, extraction, summarization, triage, routing, drafting, and evidence preparation. Humans retain authority for regulated and consequential decisions such as claim approval, rejection, settlement, fraud determination, payment release, policy binding, and final underwriting decisions.


## 1. How to Use This Workbook

This workbook captures the client-specific information required to configure IIP for a pilot. It should be completed jointly by the client operational team, IT/security team, claims team, compliance team, and Otic implementation team.

## 2. Client and Environment Details

| Field | Client Response | Notes |
| --- | --- | --- |
| Client legal name |  | Requires Confirmation |
| Pilot business unit / department |  | Requires Confirmation |
| Pilot branches / regions |  | Requires Confirmation |
| Executive sponsor |  | Requires Confirmation |
| Operational owner |  | Requires Confirmation |
| IT/security owner |  | Requires Confirmation |
| Compliance/audit owner |  | Requires Confirmation |
| Pilot environment type | Staging / UAT / Pilot / Production | Requires Confirmation |
| Preferred Azure region |  | Requires Confirmation |


## 3. Claim Type Configuration

| Configuration Item | Client Value | Notes |
| --- | --- | --- |
| Pilot claim type | Motor claim | Confirmed MVP direction unless client changes scope |
| Subtypes |  | Accident, theft, windscreen, third-party, etc. |
| Claim number format |  | Requires Confirmation |
| Policy number format |  | Requires Confirmation |
| Priority/risk categories |  | Requires Confirmation |
| Assignment rules |  | Requires Confirmation |
| Approval levels |  | Requires Confirmation |
| Escalation triggers |  | Requires Confirmation |


## 4. Document Checklist Configuration

| Document Type | Required? | Who Provides It? | AI Extraction Needed? | Human Review Required? | Notes |
| --- | --- | --- | --- | --- | --- |
| Claim form | Yes / No |  | Yes / No | Yes | Requires Confirmation |
| Police report | Yes / No |  | Yes / No | Yes | Requires Confirmation |
| National ID | Yes / No |  | Yes / No | Yes | Requires Confirmation |
| Driving permit | Yes / No |  | Yes / No | Yes | Requires Confirmation |
| Vehicle photos | Yes / No |  | Yes / No | Yes | Requires Confirmation |
| Repair quotation | Yes / No |  | Yes / No | Yes | Requires Confirmation |
| Policy schedule | Yes / No |  | Yes / No | Yes | Requires Confirmation |
| Assessor report | Yes / No |  | Yes / No | Yes | Requires Confirmation |
| Other |  |  |  |  | Requires Confirmation |


## 5. SLA and Escalation Configuration

| Workflow Stage | SLA Target | Warning Threshold | Breach Threshold | Escalation Owner |
| --- | --- | --- | --- | --- |
| FNOL captured |  |  |  |  |
| Documents requested |  |  |  |  |
| Documents received |  |  |  |  |
| Document review completed |  |  |  |  |
| Claim review completed |  |  |  |  |
| Approval decision prepared |  |  |  |  |
| Customer update sent |  |  |  |  |


## 6. User and Role Configuration

| User Name | Email | Role | Branch/Team | Access Notes |
| --- | --- | --- | --- | --- |
|  |  | Admin |  |  |
|  |  | Claims Officer |  |  |
|  |  | Claims Supervisor |  |  |
|  |  | Compliance Officer |  |  |
|  |  | Customer Service Agent |  |  |
|  |  | Executive Viewer |  |  |


## 7. Channel and Template Configuration

| Area | Required Configuration | Client Input |
| --- | --- | --- |
| WhatsApp Business API | Number, provider, webhook access, approved templates |  |
| Email / Microsoft Graph | Mailbox, permissions, folder rules, consent model |  |
| SMS | Provider and use cases, if in scope |  |
| Customer update templates | Acknowledgement, missing documents, status update, escalation |  |
| Languages | English plus local language needs, if any |  |


## 8. Integration Configuration

| Integration | Pilot Status | Owner | Notes |
| --- | --- | --- | --- |
| Policy administration system | Enabled / Not configured / Manual | Client IT / Otic |  |
| Claims management system | Enabled / Not configured / Manual | Client IT / Otic |  |
| WhatsApp Business API | Enabled / Not configured | Client IT / Otic |  |
| Microsoft Graph / Outlook | Enabled / Not configured | Client IT / Otic |  |
| Blob/document storage | Enabled | Otic / Azure |  |
| Azure AI Document Intelligence | Enabled / Not configured | Otic / Azure |  |
| Azure AI Search | Enabled / Not configured | Otic / Azure |  |

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

