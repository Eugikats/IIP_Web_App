# Support & Escalation Guide

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Support & Escalation Guide |
| Version | 1.0 |
| Status | Draft |
| Audience | Client sponsor, client IT, claims operations, Otic support, implementation team |
| Date | 2026-06-30 |


## Purpose

Define support channels, severity levels, response expectations, escalation paths, responsibilities, and the pilot support model for IIP.


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

## 3. Support Channels

| Channel | Use For | Rules |
|---|---|---|
| Primary ticket channel | All operational incidents, bugs, access issues, configuration requests, and support tracking. | Every issue must receive a ticket/reference number. |
| Email support mailbox | Formal support requests, evidence attachments, release communications, support summaries. | Avoid sending secrets, tokens, passwords, or raw sensitive customer data. |
| Teams/meeting channel | Daily pilot coordination, triage calls, stakeholder updates. | Decisions must be copied into the issue log or decision log. |
| Phone/escalation contact | Sev 1 and urgent Sev 2 escalation. | Use only for urgent issues; create ticket afterward. |

## 4. Escalation Path

| Level | Owner | Trigger | Responsibilities |
|---|---|---|---|
| L1 | Client super user / business support | User question, workflow confusion, first-line triage. | Confirm issue, collect screenshots, determine role/module, check known issues. |
| L2 | Otic support / implementation lead | Product issue, configuration issue, API/integration issue. | Reproduce, check logs, validate tenant/workspace, coordinate fix or workaround. |
| L3 | Engineering / cloud operations | Code defect, infrastructure issue, database issue, AI/integration failure. | Diagnose root cause, patch, rollback, queue recovery, database fix where approved. |
| Executive escalation | Client sponsor + Otic lead | Sev 1, blocked pilot milestone, business risk. | Agree business decision, communication, and priority. |

## 5. Client Responsibilities

| Responsibility | Detail |
|---|---|
| Named contacts | Provide business, IT, security, and executive escalation contacts. |
| Evidence | Provide screenshots, steps to reproduce, claim/document IDs, timestamps, and affected users. |
| User management | Confirm users, roles, workspace access, and business approval for role changes. |
| Third-party access | Coordinate Microsoft 365, Azure subscription, WhatsApp provider, and core system owners. |
| Final decisions | Maintain human approval over claims, underwriting, fraud, settlement, payment, and regulated actions. |

## 6. Otic Responsibilities

| Responsibility | Detail |
|---|---|
| Triage | Classify, prioritize, and assign issues. |
| Diagnosis | Check health endpoints, provider health, logs, queues, audit trails, and integration status. |
| Workarounds | Provide safe workaround where possible. |
| Fix coordination | Coordinate engineering fixes, retesting, release notes, and deployment. |
| Communication | Provide status updates according to severity and agreed cadence. |

## 7. Required Issue Intake Fields

| Field | Required | Notes |
|---|---|---|
| Issue title | Yes | Short description. |
| Environment | Yes | Demo, staging, UAT, pilot, production. |
| Tenant/workspace | Yes | Do not include secrets. |
| User/role | Yes | Affected user role, not password. |
| Module | Yes | Claims, Documents, Channels, Admin, etc. |
| Claim/document/reference ID | Where applicable | Use internal IDs, avoid unnecessary personal data. |
| Steps to reproduce | Yes | Numbered steps preferred. |
| Screenshot/video | Where useful | Redact sensitive details where possible. |
| Business impact | Yes | Users affected, workflow blocked, SLA risk. |

## 8. Pilot Support Rhythm

| Meeting | Cadence | Purpose |
|---|---|---|
| Daily triage during launch week | Daily | Review critical issues, blockers, configuration changes. |
| Weekly pilot support review | Weekly | Review issue trends, adoption, unresolved risks, upcoming releases. |
| Steering escalation | As needed | Resolve scope, timeline, commercial, or executive blockers. |
| Closeout support review | End of pilot | Summarize issues, resolution trends, readiness for next phase. |

## 9. Support Acceptance Criteria

- Support channels and named contacts are confirmed.
- Severity model is agreed with the client.
- Issue log is active and maintained.
- Known issues register is shared with client stakeholders.
- Escalation path is tested before pilot go-live.
- Support handover checklist is complete before production transition.



## Open Questions and Required Confirmations

| ID | Open Question | Owner | Impact if Unresolved |
|---|---|---|---|
| OQ-01 | What are the contracted response and resolution targets for pilot and production support? | Otic + Client Sponsor | Support expectations may be unclear. |
| OQ-02 | Which support channels are approved for the client: email, Teams, ticketing portal, WhatsApp group, phone, or all? | Otic + Client IT | Issues may be raised outside controlled channels. |
| OQ-03 | Who are named client contacts for L1 business triage, IT triage, security escalation, and executive escalation? | Client Sponsor | Escalation may be delayed. |
| OQ-04 | Which environment is in scope for support: demo, staging, UAT, pilot, production, or all? | Product Owner | Non-production issues may be incorrectly treated as production incidents. |
| OQ-05 | What is the approved maintenance window and required notice period? | Client IT + Otic Ops | Release timing and downtime communication may be disputed. |
| OQ-06 | Which third-party providers must be included in support coordination: WhatsApp BSP, Microsoft 365 tenant admins, Azure subscription owner, core insurance vendor? | Client IT + Otic Ops | Dependency resolution may be slow. |
