# Client FAQ / Troubleshooting Guide

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Client FAQ / Troubleshooting Guide |
| Version | 1.0 |
| Status | Draft |
| Audience | Claims users, supervisors, admins, compliance users, client IT, support teams |
| Date | 2026-06-30 |


## Purpose

Help client users resolve common questions around login, claims, documents, AI outputs, channels, approvals, audit logs, and support escalation.


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

## 3. Login and Access FAQ

| Question | Answer / Troubleshooting Steps |
|---|---|
| I cannot log in. | Confirm approved environment URL, browser, Entra ID account, network access, and whether the user has been provisioned. Escalate if still blocked. |
| I can log in but cannot see a module. | This is likely role-based access. Confirm the user's role and workspace assignment with the client admin. |
| I see Unauthorized. | Capture screenshot, environment, user, role, and attempted action. Escalate to support. |
| I see Not configured. | A provider or integration is not configured for that tenant/environment. Report the provider name and screen. |

## 4. Claims FAQ

| Question | Answer / Troubleshooting Steps |
|---|---|
| A claim is missing from the inbox. | Check filters, assigned officer, status, product line, search terms, and date range. Escalate with claim ID if still missing. |
| A claim cannot move to the next stage. | Check required documents, mandatory fields, approvals, role permission, and validation errors. |
| The SLA badge looks wrong. | Confirm SLA configuration, incident date, claim created date, working-day rules, and escalation rules. |
| Can AI approve a claim? | No. AI supports review, but human users make final claims decisions. |

## 5. Documents FAQ

| Question | Answer / Troubleshooting Steps |
|---|---|
| Upload failed. | Check file type, size, network, and document category. Retry once; then escalate with timestamp and claim ID. |
| Extraction is wrong. | Use human review to correct the field, then log the extraction issue for improvement. |
| Document is marked low confidence. | Review manually. Low confidence means the system is asking for human verification. |
| Required document is missing. | Follow the missing-document workflow and draft customer update if appropriate. |

## 6. AI Output FAQ

| Question | Answer / Troubleshooting Steps |
|---|---|
| AI answer has no citation. | Do not rely on it for decision support. Ask for cited answer or escalate as AI quality issue. |
| AI gave a recommendation that seems unsafe. | Do not act on it. Escalate as AI quality/safety issue. |
| AI flagged possible fraud. | Treat it as a signal requiring review, not an accusation or final determination. |
| AI summary missed key evidence. | Correct manually and log an AI quality review item. |

## 7. Channels FAQ

| Question | Answer / Troubleshooting Steps |
|---|---|
| WhatsApp message did not appear. | Check channel provider status, message timestamp, queue status, and duplicate/idempotency handling. |
| Customer update draft is wrong. | Edit before sending. AI-generated customer messages require human review. |
| Channel is unavailable. | Use approved manual fallback and report the provider status. |

## 8. When to Escalate

Escalate when the issue blocks claims operations, affects many users, suggests data exposure, produces unsafe AI behavior, prevents customer intake, or cannot be resolved using this guide.



## Open Questions and Required Confirmations

| ID | Open Question | Owner | Impact if Unresolved |
|---|---|---|---|
| OQ-01 | What are the contracted response and resolution targets for pilot and production support? | Otic + Client Sponsor | Support expectations may be unclear. |
| OQ-02 | Which support channels are approved for the client: email, Teams, ticketing portal, WhatsApp group, phone, or all? | Otic + Client IT | Issues may be raised outside controlled channels. |
| OQ-03 | Who are named client contacts for L1 business triage, IT triage, security escalation, and executive escalation? | Client Sponsor | Escalation may be delayed. |
| OQ-04 | Which environment is in scope for support: demo, staging, UAT, pilot, production, or all? | Product Owner | Non-production issues may be incorrectly treated as production incidents. |
| OQ-05 | What is the approved maintenance window and required notice period? | Client IT + Otic Ops | Release timing and downtime communication may be disputed. |
| OQ-06 | Which third-party providers must be included in support coordination: WhatsApp BSP, Microsoft 365 tenant admins, Azure subscription owner, core insurance vendor? | Client IT + Otic Ops | Dependency resolution may be slow. |
