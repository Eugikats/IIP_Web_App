# Support Handover Checklist

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Support Handover Checklist |
| Version | 1.0 |
| Status | Draft |
| Audience | Implementation team, support team, engineering, operations, client sponsor |
| Date | 2026-06-30 |


## Purpose

Ensure the implementation team hands over properly to the support team, including environments, contacts, runbooks, known issues, access, and monitoring.


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

## 3. Handover Checklist

| Area | Checklist Item | Owner | Status | Evidence / Link |
|---|---|---|---|---|
| Scope | Supported modules confirmed. | Implementation Lead | TBD | TBD |
| Environment | Demo/staging/UAT/pilot/prod URLs documented. | Cloud/Ops | TBD | TBD |
| Access | Support users and roles provisioned. | Client IT + Otic Ops | TBD | TBD |
| Contacts | Client and Otic escalation contacts documented. | Project Manager | TBD | TBD |
| Runbooks | Production support runbook shared. | Support Lead | TBD | TBD |
| Known issues | Known issues register reviewed. | Product Owner | TBD | TBD |
| Monitoring | Health checks and provider health reviewed. | Cloud/Ops | TBD | TBD |
| Logs | Log access and correlation ID process confirmed. | Engineering | TBD | TBD |
| Integrations | Integration owners and provider contacts confirmed. | Integration Lead | TBD | TBD |
| AI governance | AI issue escalation path confirmed. | AI Owner | TBD | TBD |
| Security | Incident response contact path confirmed. | Security Owner | TBD | TBD |
| Release | Current release version and release notes shared. | Release Manager | TBD | TBD |
| UAT | UAT outcomes and unresolved defects shared. | QA Lead | TBD | TBD |
| Client training | Training completed or scheduled. | Enablement Lead | TBD | TBD |

## 4. Handover Meeting Agenda

1. Confirm supported scope and current environment.
2. Review pilot/client context.
3. Review architecture and integrations at a support level.
4. Review known issues and workarounds.
5. Review severity model and escalation path.
6. Review monitoring, logs, provider health, and queue diagnostics.
7. Review release process and maintenance windows.
8. Confirm open questions and next actions.

## 5. Handover Acceptance Criteria

- Support team can access approved tools and environments.
- Support team understands modules, roles, and escalation model.
- Known issues are documented and client-facing where needed.
- Runbooks and incident templates are available.
- Release/version state is known.
- Client contacts and Otic contacts are confirmed.



## Open Questions and Required Confirmations

| ID | Open Question | Owner | Impact if Unresolved |
|---|---|---|---|
| OQ-01 | What are the contracted response and resolution targets for pilot and production support? | Otic + Client Sponsor | Support expectations may be unclear. |
| OQ-02 | Which support channels are approved for the client: email, Teams, ticketing portal, WhatsApp group, phone, or all? | Otic + Client IT | Issues may be raised outside controlled channels. |
| OQ-03 | Who are named client contacts for L1 business triage, IT triage, security escalation, and executive escalation? | Client Sponsor | Escalation may be delayed. |
| OQ-04 | Which environment is in scope for support: demo, staging, UAT, pilot, production, or all? | Product Owner | Non-production issues may be incorrectly treated as production incidents. |
| OQ-05 | What is the approved maintenance window and required notice period? | Client IT + Otic Ops | Release timing and downtime communication may be disputed. |
| OQ-06 | Which third-party providers must be included in support coordination: WhatsApp BSP, Microsoft 365 tenant admins, Azure subscription owner, core insurance vendor? | Client IT + Otic Ops | Dependency resolution may be slow. |
