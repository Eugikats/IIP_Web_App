# Known Issues Register

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Known Issues Register |
| Version | 1.0 |
| Status | Draft |
| Audience | Client users, super users, support team, product owner, implementation team |
| Date | 2026-06-30 |


## Purpose

Track known limitations, workarounds, owners, expected fix date, release target, client impact, and status.


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

## 3. Known Issues Register

| Known Issue ID | Date Added | Module | Description | Impact | Workaround | Owner | Target Release | Expected Fix Date | Status |
|---|---|---|---|---|---|---|---|---|---|
| KI-001 | YYYY-MM-DD | Documents | Example extraction limitation. | Medium | Manual review field. | TBD | TBD | TBD | Open |

## 4. Status Values

| Status | Meaning |
|---|---|
| Open | Known and not yet resolved. |
| Workaround Available | Safe workaround exists. |
| In Development | Fix is being built. |
| Ready for Release | Fix is complete and awaiting deployment. |
| Resolved | Fix released and accepted. |
| Deferred | Accepted limitation for current pilot phase. |

## 5. Known Issue Categories

| Category | Examples |
|---|---|
| Functional limitation | Missing feature, incomplete workflow, role-specific gap. |
| Data limitation | Missing seed data, incomplete policy lookup, unclear mappings. |
| AI limitation | Weak output quality, citation limitations, low confidence. |
| Document limitation | Document type not trained, poor OCR, low-quality scans. |
| Integration limitation | Provider not configured, API unavailable, read-only connection. |
| UX limitation | Confusing workflow, layout issue, unclear empty state. |

## 6. Communication Rules

- Known issues that affect pilot operations must be reviewed in weekly pilot status meetings.
- Workarounds must be clear, safe, and approved by the client business owner where they affect claims handling.
- AI safety or data exposure issues must not remain as informal known issues; they must follow incident and security handling.
- Resolved known issues should reference release notes or deployment validation evidence.

## 7. Acceptance Criteria

- Each known issue has an owner.
- Each high-impact known issue has a workaround or target release.
- Client-facing issues are communicated in plain language.
- Known issues are reviewed before UAT, pilot go-live, and production transition.



## Open Questions and Required Confirmations

| ID | Open Question | Owner | Impact if Unresolved |
|---|---|---|---|
| OQ-01 | What are the contracted response and resolution targets for pilot and production support? | Otic + Client Sponsor | Support expectations may be unclear. |
| OQ-02 | Which support channels are approved for the client: email, Teams, ticketing portal, WhatsApp group, phone, or all? | Otic + Client IT | Issues may be raised outside controlled channels. |
| OQ-03 | Who are named client contacts for L1 business triage, IT triage, security escalation, and executive escalation? | Client Sponsor | Escalation may be delayed. |
| OQ-04 | Which environment is in scope for support: demo, staging, UAT, pilot, production, or all? | Product Owner | Non-production issues may be incorrectly treated as production incidents. |
| OQ-05 | What is the approved maintenance window and required notice period? | Client IT + Otic Ops | Release timing and downtime communication may be disputed. |
| OQ-06 | Which third-party providers must be included in support coordination: WhatsApp BSP, Microsoft 365 tenant admins, Azure subscription owner, core insurance vendor? | Client IT + Otic Ops | Dependency resolution may be slow. |
