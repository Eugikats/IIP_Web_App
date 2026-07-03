# Issue Log Template

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Issue Log Template |
| Version | 1.0 |
| Status | Draft |
| Audience | Pilot team, support team, client super users, product owner |
| Date | 2026-06-30 |


## Purpose

Provide a structured issue log for tracking bugs, questions, support requests, configuration issues, ownership, severity, and status.


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

## 3. Issue Log Table

| Issue ID | Date Raised | Raised By | Environment | Module | Title | Description | Severity | Priority | Owner | Status | Target Date | Resolution / Next Action |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| IIP-ISS-001 | YYYY-MM-DD | Name | Pilot | Claims | Example issue | Describe issue and impact. | Sev 3 | Medium | TBD | Open | TBD | Capture next action. |

## 4. Recommended Status Values

| Status | Meaning |
|---|---|
| New | Received but not triaged. |
| Triage | Being clarified, reproduced, or classified. |
| In Progress | Owner is actively working on it. |
| Awaiting Client | Waiting for evidence, approval, or client action. |
| Awaiting Third Party | Waiting for Microsoft, WhatsApp provider, Azure owner, or core system vendor. |
| Ready for Retest | Fix or workaround is available. |
| Closed | Accepted and resolved. |
| Deferred | Accepted but moved to future release or change request. |

## 5. Required Triage Checklist

- Environment identified.
- Module identified.
- User role identified.
- Tenant/workspace context captured.
- Steps to reproduce captured.
- Business impact captured.
- Severity assigned.
- Owner assigned.
- Next update time agreed for Sev 1/2.

## 6. Issue Categories

| Category | Examples |
|---|---|
| Access/RBAC | Login failure, wrong role, unauthorized screen, missing menu. |
| Claims workflow | Claim creation failure, assignment issue, approval block, status error. |
| Documents | Upload failure, extraction failure, incorrect classification, missing checklist. |
| AI output | Unsupported answer, missing citation, unsafe suggestion, low confidence. |
| Integration | WhatsApp, email, Graph, Blob, Azure AI, policy/claims lookup. |
| UI/UX | Layout issue, confusing label, table density, empty/error state issue. |
| Reporting | Wrong metric, missing trend, export issue. |
| Configuration | SLA, document checklist, roles, branches, templates. |

## 7. Closure Criteria

- Issue has a documented outcome.
- Fix/workaround is tested by owner.
- Client requester or super user accepts closure where required.
- Related known issue, risk, or improvement item is updated.
- Audit/security implications are recorded if relevant.



## Open Questions and Required Confirmations

| ID | Open Question | Owner | Impact if Unresolved |
|---|---|---|---|
| OQ-01 | What are the contracted response and resolution targets for pilot and production support? | Otic + Client Sponsor | Support expectations may be unclear. |
| OQ-02 | Which support channels are approved for the client: email, Teams, ticketing portal, WhatsApp group, phone, or all? | Otic + Client IT | Issues may be raised outside controlled channels. |
| OQ-03 | Who are named client contacts for L1 business triage, IT triage, security escalation, and executive escalation? | Client Sponsor | Escalation may be delayed. |
| OQ-04 | Which environment is in scope for support: demo, staging, UAT, pilot, production, or all? | Product Owner | Non-production issues may be incorrectly treated as production incidents. |
| OQ-05 | What is the approved maintenance window and required notice period? | Client IT + Otic Ops | Release timing and downtime communication may be disputed. |
| OQ-06 | Which third-party providers must be included in support coordination: WhatsApp BSP, Microsoft 365 tenant admins, Azure subscription owner, core insurance vendor? | Client IT + Otic Ops | Dependency resolution may be slow. |
