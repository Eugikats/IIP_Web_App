# Maintenance Window & Downtime Notice Template

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Maintenance Window & Downtime Notice Template |
| Version | 1.0 |
| Status | Draft |
| Audience | Client sponsor, client users, client IT, Otic release manager, support team |
| Date | 2026-06-30 |


## Purpose

Provide standard client communication for planned maintenance, downtime, releases, degraded service, and restoration notices.


## 1. Context and Operating Principles

IIP is an Azure-native, human-reviewed insurance operations platform for insurers, brokers, TPAs, HMOs, bancassurance partners, and microinsurance providers. It supports insurance staff with customer/channel intake, claims operations, document intelligence, knowledge retrieval, workflow routing, approvals, auditability, and operational intelligence.

Core operating principles for support:

- IIP supports insurance work; humans make consequential decisions.
- AI may summarize, classify, extract, flag, draft, recommend, route, and prepare evidence.
- AI must not approve or reject claims, bind policies, set final premiums, accuse customers of fraud, release payments, or make final regulated decisions.
- Customers do not log into the internal staff app; customer interactions arrive through channels such as WhatsApp, web chat, email, SMS, broker, call center, and website forms.
- Production and pilot environments must not rely on fake endpoints or mock production data.
- Tenant, workspace, role, audit, and correlation context must be preserved for every operational action.
- Not configured providers must show a clear `Not configured` state rather than silent failure.



## 2. Support Scope

The Client Support Pack covers support after demo, during pilot, and during production transition. It assumes IIP has at minimum the core staff app, Command Center, Claims, Documents, Channels, Customers, Sales & Leads, Approvals, Audit, Admin, API health, provider health, and the agreed Azure-first implementation foundation.

### In Scope

| Area | Included Support |
|---|---|
| Access and roles | Login, workspace access, role visibility, unauthorized states, user role mapping. |
| Claims workflow | Claim creation, claim inbox, review flow, assignment, escalation, approval routing, status changes. |
| Documents | Upload, classification, extraction, missing-document detection, confidence review, document viewer. |
| AI support | AI summaries, RAG answers, citations, low-confidence handling, prompt safety, human-review workflow. |
| Channels | WhatsApp/channel intake, message queue status, customer update drafts, failed channel notifications. |
| Integrations | Microsoft Graph/email, Blob Storage, Azure AI services, policy/claims lookup where configured. |
| Operations | Health checks, provider health, logs, audit events, queues, environment status, release issues. |
| Client enablement | FAQs, troubleshooting, issue triage, maintenance notices, known issue communication. |

### Out of Scope Unless Separately Agreed

| Area | Treatment |
|---|---|
| Final insurance decisions | Client insurer remains accountable for claims, underwriting, pricing, fraud, and payment decisions. |
| Core system replacement | IIP sits above core systems and should not be treated as the policy or claims system of record unless explicitly designed. |
| Regulatory legal advice | IIP support can provide system evidence and operational logs, not legal opinions. |
| Client network/device support | Client IT remains responsible for local devices, internet connectivity, browser policies, and endpoint controls. |
| Third-party provider outages | IIP support coordinates diagnosis and mitigation but cannot guarantee third-party recovery times. |

## 3. Planned Maintenance Notice Template

**Subject:** Planned IIP Maintenance - [Environment] - [Date/Time]

| Field | Detail |
|---|---|
| Environment | Pilot / Production / UAT / Staging |
| Maintenance Window | Start: [date/time/timezone] - End: [date/time/timezone] |
| Expected Impact | [No downtime / brief downtime / degraded service / specific modules affected] |
| Reason | [Release, database maintenance, Azure maintenance, integration update, security patch] |
| Affected Users | [Roles / departments / all users] |
| Required User Action | [Save work, avoid uploads, use manual fallback, none] |
| Support Contact | [Approved support channel] |

Message:

IIP will undergo planned maintenance during the window above. During this period, users may experience [impact]. Please avoid [specific actions] during the window. A restoration notice will be sent once maintenance is complete.

## 4. Degraded Service Notice Template

**Subject:** IIP Service Degradation - [Module/Environment]

| Field | Detail |
|---|---|
| Issue Detected | [date/time/timezone] |
| Affected Area | [Claims / Documents / AI / Channels / Login / Integrations] |
| Current Impact | [Description] |
| Workaround | [Description or None available] |
| Next Update | [date/time/timezone] |
| Support Reference | [Issue/incident ID] |

Message:

We are investigating degraded service affecting [area]. Users may experience [impact]. Please use [workaround] where applicable. The support team will provide the next update by [time].

## 5. Restoration Notice Template

**Subject:** IIP Service Restored - [Module/Environment]

| Field | Detail |
|---|---|
| Restored At | [date/time/timezone] |
| Affected Area | [Module] |
| Summary | [Brief description] |
| User Action | [Refresh, retry failed action, report remaining issues] |
| Follow-up | [Incident report / post-incident review / none] |

Message:

Service has been restored for [area]. Users may resume normal operations. Please report any remaining issues through the approved support channel and include the support reference [ID].

## 6. Release Notice Template

**Subject:** IIP Release [Version] - [Environment] - [Date]

| Section | Content |
|---|---|
| Release version | [Version] |
| Environment | [Environment] |
| Release date | [Date] |
| New features | [List] |
| Fixes | [List] |
| Known issues | [List or link] |
| User impact | [What users should expect] |
| Required action | [Training, refresh browser, retest scenario, none] |

## 7. Communication Approval Checklist

- Message reviewed by release/support owner.
- Client sponsor approval obtained where required.
- Timezone stated clearly.
- Impact and workaround are clear.
- No secrets or sensitive customer data included.
- Support contact included.
- Follow-up timing stated for degraded service or incidents.



## Open Questions and Required Confirmations

| ID | Open Question | Owner | Impact if Unresolved |
|---|---|---|---|
| OQ-01 | What are the contracted response and resolution targets for pilot and production support? | Otic + Client Sponsor | Support expectations may be unclear. |
| OQ-02 | Which support channels are approved for the client: email, Teams, ticketing portal, WhatsApp group, phone, or all? | Otic + Client IT | Issues may be raised outside controlled channels. |
| OQ-03 | Who are named client contacts for L1 business triage, IT triage, security escalation, and executive escalation? | Client Sponsor | Escalation may be delayed. |
| OQ-04 | Which environment is in scope for support: demo, staging, UAT, pilot, production, or all? | Product Owner | Non-production issues may be incorrectly treated as production incidents. |
| OQ-05 | What is the approved maintenance window and required notice period? | Client IT + Otic Ops | Release timing and downtime communication may be disputed. |
| OQ-06 | Which third-party providers must be included in support coordination: WhatsApp BSP, Microsoft 365 tenant admins, Azure subscription owner, core insurance vendor? | Client IT + Otic Ops | Dependency resolution may be slow. |
