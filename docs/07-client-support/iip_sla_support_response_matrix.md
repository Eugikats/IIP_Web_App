# SLA / Support Response Matrix

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | SLA / Support Response Matrix |
| Version | 1.0 |
| Status | Draft |
| Audience | Client sponsor, operations leads, support team, service managers |
| Date | 2026-06-30 |


## Purpose

Define severity levels, examples, response expectations, resolution guidance, escalation rules, and communication cadence for IIP support.


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

## Severity Classification

| Severity | Name | Definition | Examples | Initial Response Target | Communication Cadence |
|---|---|---|---|---|---|
| Sev 1 | Critical outage | Production or pilot operation is unavailable or exposes a serious security/data risk. | Users cannot access IIP; tenant data exposure suspected; claims intake unavailable; production database unavailable. | Requires Confirmation: target to be agreed in support contract. Suggested pilot target: 1 hour. | Every 30-60 minutes until stabilized. |
| Sev 2 | Major impairment | Critical workflow is degraded with no practical workaround. | Document extraction failing for all claims; WhatsApp intake stuck; approvals cannot be completed. | Requires Confirmation: suggested pilot target: 4 business hours. | At least daily or at agreed milestones. |
| Sev 3 | Standard issue | Feature issue with workaround or limited user impact. | One user has role issue; specific document type extracts poorly; report missing field. | Requires Confirmation: suggested pilot target: 1 business day. | Every 2-3 business days or on status change. |
| Sev 4 | Question/request | How-to, enhancement request, cosmetic issue, documentation request. | FAQ question; label change; new report request; configuration clarification. | Requires Confirmation: suggested pilot target: 2 business days. | Weekly or as agreed. |

## 3. Resolution Guidance

| Severity | Restoration Target | Resolution Target | Notes |
|---|---|---|---|
| Sev 1 | Requires Confirmation. Suggested pilot target: same business day or emergency workaround. | Requires Confirmation. | Restoration may involve rollback, disabling a failing provider, queue replay, or manual workaround. |
| Sev 2 | Requires Confirmation. Suggested target: 1-2 business days. | Requires Confirmation. | Priority fix or workaround needed where key workflow is blocked. |
| Sev 3 | Requires Confirmation. Suggested target: next planned release or agreed patch. | Requires Confirmation. | Include in backlog and communicate expected release. |
| Sev 4 | Requires Confirmation. Suggested target: backlog review or documentation response. | Requires Confirmation. | Enhancement requests follow change control. |

## 4. SLA Clock Rules

| Rule | Description |
|---|---|
| Business hours | Requires Confirmation: define client support timezone, likely Africa/Kampala for local pilot operations. |
| Start time | SLA clock starts when issue is received in an approved support channel with enough information to triage. |
| Pause conditions | SLA may pause when awaiting client evidence, third-party provider response, access approval, or security approval. |
| Reclassification | Severity may be upgraded or downgraded based on business impact and workaround availability. |
| Closure | Issue closes when fix/workaround is accepted, or when no client response is received after agreed follow-up period. |

## 5. Escalation Rules

| Trigger | Escalation |
|---|---|
| Sev 1 opened | Notify Otic support lead, engineering lead, client sponsor, and client IT immediately. |
| Sev 2 unresolved beyond target | Escalate to product owner and implementation lead. |
| Security or data exposure suspected | Escalate to security owner and incident response process. |
| AI output safety concern | Escalate to AI governance owner and temporarily disable risky workflow if needed. |
| Repeated issue trend | Add to known issues register and improvement backlog. |

## 6. Client Impact Examples

| Impact | Likely Severity |
|---|---|
| Entire pilot cannot operate | Sev 1 |
| Claims officers cannot review any claims | Sev 1 or Sev 2 depending workaround. |
| Document extraction fails for one document type | Sev 2 or Sev 3 depending volume and workaround. |
| One user's role is wrong | Sev 3 unless senior approver is blocked during go-live. |
| Request for a new dashboard metric | Sev 4 / change request. |

## 7. SLA Acceptance Criteria

- Severity definitions are approved by client sponsor and Otic lead.
- Support operating hours are confirmed.
- Response targets and resolution targets are contractually confirmed.
- Escalation contacts are named.
- SLA reporting cadence is agreed.



## Open Questions and Required Confirmations

| ID | Open Question | Owner | Impact if Unresolved |
|---|---|---|---|
| OQ-01 | What are the contracted response and resolution targets for pilot and production support? | Otic + Client Sponsor | Support expectations may be unclear. |
| OQ-02 | Which support channels are approved for the client: email, Teams, ticketing portal, WhatsApp group, phone, or all? | Otic + Client IT | Issues may be raised outside controlled channels. |
| OQ-03 | Who are named client contacts for L1 business triage, IT triage, security escalation, and executive escalation? | Client Sponsor | Escalation may be delayed. |
| OQ-04 | Which environment is in scope for support: demo, staging, UAT, pilot, production, or all? | Product Owner | Non-production issues may be incorrectly treated as production incidents. |
| OQ-05 | What is the approved maintenance window and required notice period? | Client IT + Otic Ops | Release timing and downtime communication may be disputed. |
| OQ-06 | Which third-party providers must be included in support coordination: WhatsApp BSP, Microsoft 365 tenant admins, Azure subscription owner, core insurance vendor? | Client IT + Otic Ops | Dependency resolution may be slow. |
