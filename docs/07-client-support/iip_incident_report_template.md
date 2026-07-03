# Incident Report Template

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Incident Report Template |
| Version | 1.0 |
| Status | Draft |
| Audience | Otic support, engineering, security, client IT, client sponsor |
| Date | 2026-06-30 |


## Purpose

Provide a standard incident report template for outages, data issues, AI errors, failed integrations, security concerns, root cause, impact, response, and preventive actions.


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

## 3. Incident Summary Template

| Field | Value |
|---|---|
| Incident ID | IIP-INC-YYYY-NNN |
| Incident Title | TBD |
| Severity | Sev 1 / Sev 2 / Sev 3 / Sev 4 |
| Environment | Demo / Staging / UAT / Pilot / Production |
| Tenant / Workspace | TBD |
| Start Time | TBD |
| Detection Time | TBD |
| Restoration Time | TBD |
| Closure Time | TBD |
| Incident Commander | TBD |
| Client Contact | TBD |
| Otic Technical Owner | TBD |

## 4. Impact Assessment

| Area | Details |
|---|---|
| Users affected | Number and roles. |
| Modules affected | Claims, Documents, Channels, Admin, AI, integrations, etc. |
| Customer impact | Whether external customer communication or intake was affected. |
| Data impact | Data loss, duplication, exposure, inconsistency, or none confirmed. |
| AI impact | Whether AI produced unsafe, unsupported, or incorrect output. |
| SLA impact | Claims or support SLA risk. |
| Workaround used | Manual process, temporary disablement, queue replay, rollback, etc. |

## 5. Timeline

| Time | Event | Owner | Evidence / Notes |
|---|---|---|---|
| HH:MM | Incident detected | TBD | Alert, user report, log, health check. |
| HH:MM | Triage started | TBD | Initial checks. |
| HH:MM | Mitigation applied | TBD | Workaround or fix. |
| HH:MM | Service restored | TBD | Validation evidence. |
| HH:MM | Client notified | TBD | Communication record. |

## 6. Root Cause Analysis

| Question | Response |
|---|---|
| What happened? | TBD |
| Why did it happen? | TBD |
| Why was it not prevented? | TBD |
| Why was it not detected earlier? | TBD |
| What controls must change? | TBD |

## 7. Corrective and Preventive Actions

| Action ID | Action | Owner | Due Date | Priority | Status |
|---|---|---|---|---|---|
| CAPA-001 | TBD | TBD | TBD | High | Open |

## 8. Evidence to Attach

- Health check output.
- Provider health snapshot.
- Relevant logs with correlation IDs.
- Queue/dead-letter status if applicable.
- Deployment/release record if applicable.
- Screenshots or user reports.
- Audit events.
- Communication messages.

## 9. Incident Closure Criteria

- Service restored or workaround accepted.
- Client notified according to severity.
- Root cause recorded or explicitly marked as unresolved.
- Corrective actions assigned.
- Known issues and improvement backlog updated.
- Security/compliance evidence captured if relevant.



## Open Questions and Required Confirmations

| ID | Open Question | Owner | Impact if Unresolved |
|---|---|---|---|
| OQ-01 | What are the contracted response and resolution targets for pilot and production support? | Otic + Client Sponsor | Support expectations may be unclear. |
| OQ-02 | Which support channels are approved for the client: email, Teams, ticketing portal, WhatsApp group, phone, or all? | Otic + Client IT | Issues may be raised outside controlled channels. |
| OQ-03 | Who are named client contacts for L1 business triage, IT triage, security escalation, and executive escalation? | Client Sponsor | Escalation may be delayed. |
| OQ-04 | Which environment is in scope for support: demo, staging, UAT, pilot, production, or all? | Product Owner | Non-production issues may be incorrectly treated as production incidents. |
| OQ-05 | What is the approved maintenance window and required notice period? | Client IT + Otic Ops | Release timing and downtime communication may be disputed. |
| OQ-06 | Which third-party providers must be included in support coordination: WhatsApp BSP, Microsoft 365 tenant admins, Azure subscription owner, core insurance vendor? | Client IT + Otic Ops | Dependency resolution may be slow. |
