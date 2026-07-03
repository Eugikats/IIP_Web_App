# Client Support Operating Model

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Client Support Operating Model |
| Version | 1.0 |
| Status | Draft |
| Audience | Client sponsor, support manager, operations lead, Otic leadership, implementation team |
| Date | 2026-06-30 |


## Purpose

Define L1/L2/L3 support, Otic/client responsibilities, support tooling, reporting cadence, support roles, and escalation governance.


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

## 3. Operating Model Overview

| Layer | Owned By | Primary Role |
|---|---|---|
| L0 - Self-help | Client users + documentation | Use User/Admin Guide, FAQ, known issues, and training materials. |
| L1 - Business super user | Client | Answer workflow questions, confirm business process, collect evidence, triage user issues. |
| L2 - Product support | Otic support / implementation | Reproduce issues, check configuration, validate workflows, coordinate fixes. |
| L3 - Engineering/cloud | Otic engineering / cloud operations | Resolve code, database, infrastructure, AI, and integration defects. |
| Governance | Client sponsor + Otic lead | Decide priorities, scope changes, escalation, and release timing. |

## 4. RACI Matrix

| Activity | Client User | Client Super User | Client IT | Otic Support | Otic Engineering | Otic Product |
|---|---|---|---|---|---|---|
| Raise issue | R | A | C | C | I | I |
| First-line triage | C | R/A | C | C | I | I |
| Role/access confirmation | C | R | A | C | I | I |
| Configuration issue | C | C | C | R | C | A |
| Code defect fix | I | C | I | R | A | C |
| AI quality issue | C | C | I | R | C | A |
| Security incident | I | C | R | R | C | A |
| Release approval | I | C | C | C | R | A |

## 5. Support Tooling

| Tool / Register | Purpose | Owner |
|---|---|---|
| Issue log | Track support issues and status. | Support Lead |
| Known issues register | Publish known limitations and workarounds. | Product Owner |
| Incident report | Document major incidents and corrective actions. | Incident Commander |
| Release notes | Communicate fixes and changes. | Release Manager |
| Feedback log | Capture improvement requests. | Customer Success / Product |
| AI quality review log | Track AI output issues and model/prompt improvements. | AI Owner |

## 6. Reporting Cadence

| Report / Meeting | Cadence | Audience | Content |
|---|---|---|---|
| Daily go-live triage | Daily during launch week | Pilot users, support, product | Open blockers, critical issues, next actions. |
| Weekly support report | Weekly during pilot | Client sponsor, Otic lead | Issue volume, severity, aging, trends, risks. |
| Monthly service review | Monthly after production | Client sponsor, operations, Otic | SLA, adoption, incidents, improvements, roadmap. |
| Incident review | After Sev 1/major Sev 2 | Incident stakeholders | Root cause, impact, corrective actions. |

## 7. Operating Model Acceptance Criteria

- L1/L2/L3 ownership is agreed.
- Support tooling and registers are active.
- Reporting cadence is scheduled.
- Escalation path is published.
- Support model is reviewed at pilot closeout before production transition.



## Open Questions and Required Confirmations

| ID | Open Question | Owner | Impact if Unresolved |
|---|---|---|---|
| OQ-01 | What are the contracted response and resolution targets for pilot and production support? | Otic + Client Sponsor | Support expectations may be unclear. |
| OQ-02 | Which support channels are approved for the client: email, Teams, ticketing portal, WhatsApp group, phone, or all? | Otic + Client IT | Issues may be raised outside controlled channels. |
| OQ-03 | Who are named client contacts for L1 business triage, IT triage, security escalation, and executive escalation? | Client Sponsor | Escalation may be delayed. |
| OQ-04 | Which environment is in scope for support: demo, staging, UAT, pilot, production, or all? | Product Owner | Non-production issues may be incorrectly treated as production incidents. |
| OQ-05 | What is the approved maintenance window and required notice period? | Client IT + Otic Ops | Release timing and downtime communication may be disputed. |
| OQ-06 | Which third-party providers must be included in support coordination: WhatsApp BSP, Microsoft 365 tenant admins, Azure subscription owner, core insurance vendor? | Client IT + Otic Ops | Dependency resolution may be slow. |
