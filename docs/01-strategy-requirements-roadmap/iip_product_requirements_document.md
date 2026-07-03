# Insurance Intelligence Platform (IIP) - Product Requirements Document

**Document type:** Product Requirements Document (PRD)  
**Product name:** Insurance Intelligence Platform  
**Acronym:** IIP  
**Prepared for:** Otic Group / IIP Product, Engineering, Design, Cloud, Security, AI, and Delivery Teams  
**Date:** 30 June 2026  
**Version:** 1.0 - Consolidated MVP PRD  
**Status:** Working product baseline for MVP planning and implementation  
**Classification:** Internal - Product, Engineering, Architecture, Cloud, Security, AI, Leadership, and Delivery Reference

---

## Document Control

| Field | Value |
|---|---|
| Owner | Product / Delivery Lead |
| Primary audience | Product, engineering, design, AI, architecture, security, cloud, sales, leadership |
| Source baseline | IIP Master Product Context, DeepSeek findings, Antigravity/codebase findings, UI/UX notes, meeting notes, lifecycle checklist |
| Current decision | Use **Insurance Intelligence Platform (IIP)** as official product name |
| Superseded name | ACIP / Agentic Insurance Intelligence Platform |
| MVP focus | Motor Claims and Document Intelligence for an insurance company pilot |
| Decision model | AI assists. Humans make regulated insurance decisions. |

---

## 1. Executive Summary


---

# July 2026 Update: Chat-First Command Center Requirement

## Product Decision

The Command Center is no longer a dashboard-first page with chat added as a secondary feature. The approved direction is a **chat-first operational command workspace**.

The center of the page must be the primary AI conversation surface, similar in interaction priority to ChatGPT, while still preserving insurer-grade operational controls, evidence, role-aware actions, and human accountability.

## Updated Product Requirement

> **The Command Center is not a dashboard with chat. The Command Center is a chat-first operational command workspace where metrics, claims, evidence, actions, and reports appear as structured blocks inside the conversation.**

## Required Experience

The Command Center must include:

- a large central command-chat workspace occupying the primary page area;
- a sticky command composer for operational questions and work requests;
- an initial daily operational briefing message;
- metric pills embedded inside the conversation flow;
- structured result blocks for claims, SLA risk, approvals, documents, customer updates, reports, and provider health;
- suggested prompts for common operational questions;
- action proposal blocks for human-reviewed next steps;
- a right-side Context & Evidence Rail showing sources, confidence, selected records, and human-review warnings;
- no fake metrics, hardcoded insurer data, simulated success states, or dashboard-only placeholders in deployed environments.

## Example User Queries

```text
What needs my attention today?
Show claims likely to breach SLA.
Which motor claims are missing police reports?
Summarize today's new FNOL submissions.
Prepare a management summary for today's claims operations.
Show pending customer updates requiring approval.
```

## Updated Requirement IDs

| ID | Requirement | Priority |
|---|---|---:|
| PRD-CMD-CHAT-001 | The Command Center must make the central chat workspace the dominant interaction surface. | P0 |
| PRD-CMD-CHAT-002 | Operational metrics must be available as compact metric pills inside or above the command conversation, not only as dashboard cards. | P0 |
| PRD-CMD-CHAT-003 | Agent responses must support structured UI blocks such as metric pills, claims tables, SLA summaries, evidence lists, and action proposals. | P0 |
| PRD-CMD-CHAT-004 | Every operational answer must be tenant-scoped, role-aware, evidence-linked, and auditable where applicable. | P0 |
| PRD-CMD-CHAT-005 | The right panel must act as a Context & Evidence Rail, not as a duplicate chat thread. | P0 |
| PRD-CMD-CHAT-006 | The Command Center must never present AI output as a final human decision. | P0 |
| PRD-CMD-CHAT-007 | Not configured, empty, unauthorized, degraded, and error states must appear as explicit chat/system blocks. | P0 |

## Phase 1-4 Delivery Alignment

| Phase | Product Outcome |
|---|---|
| Phase 1 | Build the chat-first Command Center shell, left navigation, top bar, central command thread, composer, and evidence rail. |
| Phase 2 | Connect daily briefing, metrics, claims attention, SLA risk, approvals, provider health, and evidence blocks to real APIs. |
| Phase 3 | Add agentic workflows: filtering, summarization, customer-update drafting, approval-task preparation, report drafting, and evidence-aware answers. |
| Phase 4 | Harden UX, security, QA, accessibility, audit logging, no-fake-data enforcement, and release validation. |
The **Insurance Intelligence Platform (IIP)** is an Azure-native, human-reviewed insurance operations platform for African insurers and insurance-adjacent institutions. It is designed to help insurers acquire customers, process claims, review documents, support underwriting, detect risk signals, manage customer communications, and maintain auditable workflows through AI-assisted, source-grounded, role-aware workspaces.

IIP is not a generic chatbot and must not be positioned as an autonomous insurance decision-maker. Its purpose is to act as an intelligent operational layer above existing insurer workflows and systems. It receives fragmented inputs from WhatsApp, email, broker submissions, call center notes, internal uploads, and documents; turns them into structured cases; prepares evidence-linked summaries; detects missing information; routes work; drafts customer-safe communications; and supports human decision-making.

The central product principle is:

> **AI prepares insurance work. Humans make regulated insurance decisions.**

The first MVP should focus on **Motor Claims and Document Intelligence** for a mid-to-large insurance company. The MVP must support WhatsApp/email/channel-based First Notice of Loss (FNOL), claim intake, document upload, OCR and field extraction, document classification, missing-document detection, AI-generated claim summaries, advisory fraud-signal flags, human review, audit logging, and claim pack export.

This PRD defines the product purpose, MVP requirements, user groups, feature scope, exclusions, success metrics, risks, assumptions, and open questions required to guide implementation.

---

## 2. Product Identity and Positioning

### 2.1 Official Product Name

| Item | Decision |
|---|---|
| Official product name | **Insurance Intelligence Platform** |
| Acronym | **IIP** |
| Superseded name | ACIP / Agentic Insurance Intelligence Platform |
| Internal code/repo name | IIP |
| Public-facing name | Insurance Intelligence Platform |
| Company attribution | Use "Insurance Intelligence Platform by Otic" only where attribution is required |

All new PRD, FSD, TSD, UI/UX, pitch, roadmap, and implementation documents should use **Insurance Intelligence Platform (IIP)**. Older references to ACIP should be labeled as superseded naming references.

### 2.2 Product Category

IIP is a governed insurance operations intelligence platform. It combines:

- Insurance workflow management
- AI-assisted document intelligence
- AI-assisted claims preparation
- Channel and customer interaction intelligence
- Source-grounded policy knowledge retrieval
- Human review and approval workflows
- Audit trails and compliance evidence
- Azure-native enterprise deployment

### 2.3 Strategic Positioning

IIP should be positioned as:

> **An Africa-first, Azure-native insurance intelligence platform that helps insurers acquire customers, process claims, review documents, support underwriting, and manage customer workflows through human-reviewed AI, auditable evidence, and configurable insurance workflows.**

### 2.4 Safe Enterprise Messaging

Use:

- Human-reviewed insurance intelligence
- AI-assisted claims operations
- Controlled insurance automation
- Source-grounded policy intelligence
- Agentic workflow orchestration
- Regulator-ready workflow records
- Auditable decision support
- Role-aware insurance operations workspace

Avoid:

- Fully autonomous claims approval
- Autonomous underwriting
- AI claims decisioning
- Automatic policy binding
- AI fraud accusation
- Unreviewed premium pricing
- Unrestricted customer advice
- Automatic settlement release

---

## 3. Product Vision

### 3.1 Vision Statement

IIP should become the intelligent insurance operations layer that helps African insurers move from fragmented, manual, document-heavy workflows to structured, auditable, AI-assisted operations while preserving human accountability for regulated decisions.

### 3.2 North Star Outcome

The product should help an insurance company quickly answer:

- Which claims need attention?
- Which claims are missing documents?
- Which customer conversations need staff action?
- Which cases are close to SLA breach?
- Which claims have AI-prepared summaries ready for review?
- Which documents have low-confidence extraction results?
- Which actions require human approval?
- Which policy documents or SOPs did AI use?
- What is the audit trail for a claim, customer interaction, or AI output?
- What is the operational impact on productivity, service speed, and customer trust?

### 3.3 Core Product Promise

| Current State | Target State with IIP |
|---|---|
| Manual document processing | AI-assisted document classification, OCR, extraction, and review |
| Slow claims intake | Guided FNOL and structured claims registration |
| Paper-heavy workflows | Digital evidence packs and case files |
| Fragmented policy knowledge | Source-grounded policy and SOP retrieval |
| Repetitive customer support | AI-assisted support drafts and escalation |
| Weak customer education | Controlled insurance explanations through approved channels |
| Poor claim visibility | Status, SLA, missing-document, and assignment tracking |
| Informal-market friction | WhatsApp-first engagement and guided onboarding |
| Inconsistent staff responses | Standardized knowledge-backed guidance |
| Weak audit trail | Human-reviewed, regulator-ready logs |

---

## 4. Business Objectives

### 4.1 MVP Business Objectives

The MVP must prove that IIP can improve insurer operations without removing human control. Specifically, it must:

1. Reduce claims intake friction.
2. Reduce manual document review workload.
3. Improve missing-document visibility.
4. Generate structured, human-reviewable claim summaries.
5. Improve customer response speed through guided channels.
6. Improve claims officer productivity.
7. Improve audit traceability of AI-assisted work.
8. Demonstrate safe, human-controlled use of AI in insurance.
9. Provide an enterprise-ready Azure architecture path.
10. Create a credible foundation for future modules such as customer acquisition, fraud intelligence, underwriting support, and early warning.

### 4.2 Broader Product Objectives

IIP should eventually help insurers:

- Increase qualified insurance leads.
- Improve WhatsApp-native customer acquisition.
- Improve customer education and policy understanding.
- Improve renewal and retention outreach.
- Reduce call center and repetitive support burden.
- Standardize internal knowledge access.
- Improve compliance and evidence readiness.
- Scale across insurers, branches, countries, and product lines.

---

## 5. Target Market and Customer Segments

### 5.1 Primary Market

The first market is Uganda, with an Africa-configurable product model.

The platform should be built for African market realities:

- Low insurance penetration
- Trust-sensitive claims experiences
- Document-heavy insurer operations
- WhatsApp and mobile-first customer behavior
- Variable connectivity
- Branch and broker-driven operations
- Multilingual customer needs
- Increasing regulatory and audit expectations

### 5.2 Target Institutions

| Institution Type | Primary Use Case |
|---|---|
| General insurance companies | Motor, property, marine, engineering claims and underwriting support |
| Life insurers | Beneficiary support, life claims, policy servicing |
| Health insurers / HMOs | Medical claims, provider invoice review, member support |
| Microinsurance providers | Simple claims, low-cost onboarding, local-language education |
| Brokers | Client submissions, renewals, claims follow-up, document packs |
| TPAs / HMOs | Health claims, provider document review, customer support |
| Bancassurance partners | Customer education, embedded lead qualification, renewals |
| Reinsurers | Later phase: bordereaux, treaty summaries, claims reporting |

### 5.3 First Pilot Customer

The recommended first pilot customer is a **mid-to-large insurance company**, preferably a general insurer with meaningful motor claims volume.

Reasons:

- Claims volume is sufficient to demonstrate ROI.
- Motor claims provide a clear document intelligence use case.
- Insurer leadership has direct interest in operational efficiency, customer trust, and auditability.
- Internal users are clearly defined.
- The product can later expand to brokers, bancassurance partners, TPAs, HMOs, and microinsurance providers.

---

## 6. User Personas

### 6.1 Internal Insurance Users

| Persona | Primary Needs |
|---|---|
| Claims Officer | Claim summaries, document checklists, extraction review, evidence view, customer update drafts |
| Claims Supervisor | Escalations, SLA visibility, approvals, workload management, exception oversight |
| Underwriter | Submission summaries, missing information, guideline retrieval, risk notes, referral flags |
| Customer Service Agent | Customer guidance, policy Q&A, claim status drafts, escalation support |
| Compliance Officer | Audit logs, AI output review, decision trails, regulator-ready evidence packs |
| Insurer Admin | Users, roles, workflows, claim types, document templates, integrations, tenant configuration |
| Executive / Operations Manager | Claims turnaround, productivity, SLA performance, cost reduction, adoption metrics |
| IT / Cloud Admin | Identity, access, integrations, monitoring, security, deployment health |
| Knowledge Manager | Upload, approve, archive, test, and maintain policy/SOP/FAQ knowledge |
| Finance / Payments Officer | Later phase: approved payment workflows, reconciliation, settlement visibility |

### 6.2 External Users and Channels

Customers should not access the internal IIP workspace in V1. Instead, customer and broker interactions should feed internal workspaces through approved channels.

| External Actor | Expected Interaction |
|---|---|
| Policyholder / Customer | WhatsApp/web/email claim initiation, document upload, status request, missing-document response |
| Prospective Customer | Product inquiry, quote request, lead qualification through WhatsApp/web/call center |
| Broker / Agent | Submission support, document upload, customer follow-up, renewal tracking |
| Beneficiary | Life claim guidance in later phases |
| Health Member | Medical claim guidance in later phases |
| Garage / Assessor / Hospital | Later phase: evidence/document submission and status updates |

### 6.3 Platform-Side Users

| Persona | Primary Needs |
|---|---|
| Otic Platform Admin | Tenant support, deployment monitoring, configuration assistance, incident response |
| Implementation Consultant | Setup, workflow configuration, pilot training, document template mapping |
| Support Engineer | Provider health, API failures, logs, queues, deployment health |

---

## 7. MVP Scope

### 7.1 MVP Name

**IIP Motor Claims and Document Intelligence MVP**

### 7.2 MVP Purpose

The MVP exists to help an insurer receive, structure, review, and export motor claim files faster using guided intake, insurance document intelligence, AI-assisted summaries, human review, audit logging, and channel-based customer submission.

### 7.3 MVP Claim Type

The selected MVP claim type is **Motor Claim**.

Motor claims are suitable because they are familiar, document-heavy, easy to demo, operationally common, and suitable for demonstrating OCR, missing-document checks, claim summaries, and fraud-signal detection.

### 7.4 In-Scope MVP Modules

| Module | MVP Scope |
|---|---|
| Tenant setup | Configure insurer tenant, users, roles, product lines, claim types, document requirements |
| Authentication and RBAC | Microsoft Entra ID staff login, role-aware access, MFA-ready design |
| Command Center | Role-based operational overview, AI-first workspace, priority cards, workload summary |
| Channels | Internal unified inbox for WhatsApp, webchat, SMS, email, broker, call center, and website interactions |
| Customers | Internal Customer 360 profile, not customer portal login |
| Sales & Leads | Internal acquisition workspace fed by external channels |
| Claims Inbox / Work Queue | Claim queue, filters, statuses, SLA, assignment, missing-document visibility |
| FNOL / New Claim Intake | Capture first notice of loss and create structured claim intake record |
| Document Upload | Upload and store claim forms, reports, photos, permits, IDs, quotations, schedules |
| Document Classification | Identify document type and route to correct extraction/review flow |
| OCR and Field Extraction | Extract key fields from supported motor claim documents |
| Missing-Document Checklist | Identify required documents by claim type and flag gaps |
| Claim Summary Generation | Generate structured, evidence-linked claim summary for human review |
| Fraud-Signal Flags | Surface suspicious patterns as advisory signals only |
| Human Review Cockpit | Review, correct, approve fields, request missing documents, escalate, export |
| Audit Trail | Log AI actions, document actions, user actions, edits, approvals, escalations, exports |
| Claim Pack Export | Export reviewed claim data and evidence as PDF/CSV/JSON/API payload where supported |
| Basic Policy Q&A | Retrieve and explain approved policy clauses for internal staff with source grounding |
| Support and Health | Provider health, queues, failures, service availability, configuration state |

### 7.5 Explicitly Out of Scope for MVP

Do not build these into the first MVP unless explicitly approved:

- Autonomous claim approval
- Autonomous claim rejection
- Automatic settlement
- Payment release
- Final premium calculation
- Full underwriting pricing engine
- Automatic policy binding
- National ID integration
- Vehicle registry integration
- Medical provider integration
- Full payment collection
- Full voice/TTS support
- All regional languages
- All insurance product lines
- Complex reinsurance reporting
- Advanced Microsoft Fabric analytics
- Cross-country regulatory automation
- Full offline claims processing
- Fully transactable Microsoft Marketplace billing
- Customer portal login as a primary V1 flow

---

## 8. Core User Journeys

### 8.1 Motor Claim FNOL Journey

```text
Customer reports motor accident via WhatsApp, email, web, broker, or call center
↓
IIP captures FNOL information
↓
IIP creates claim intake record
↓
Customer or staff uploads claim documents
↓
Document Intelligence classifies documents
↓
OCR/extraction captures key fields
↓
System checks required document completeness
↓
AI generates claim summary
↓
Fraud Signal module flags inconsistencies or suspicious patterns
↓
Claims officer reviews evidence
↓
Supervisor approves sensitive workflow actions where required
↓
Customer-safe update is drafted
↓
Human approves communication/action
↓
Audit trail is stored
↓
Claim pack is exported or pushed to downstream system
```

### 8.2 Missing Document Request Journey

```text
Document checker identifies missing required item
↓
AI drafts customer-safe missing-document message
↓
Claims officer reviews and approves message
↓
Message is sent through configured channel
↓
Customer uploads missing document
↓
System rechecks completeness
↓
Claim status updates
↓
Audit trail records request and response
```

### 8.3 Human Approval Journey

```text
AI prepares summary, recommendation, draft, or flag
↓
System determines whether action is sensitive or consequential
↓
Approval task is created
↓
Authorized human reviewer reviews evidence
↓
Reviewer approves, rejects, edits, overrides, or escalates
↓
Decision is logged
↓
Customer-safe output or workflow update is completed
```

### 8.4 Policy Q&A Journey

```text
Staff asks policy or claims procedure question
↓
System identifies tenant, product, policy, and role context
↓
Approved knowledge sources are retrieved
↓
AI drafts answer using retrieved sources only
↓
Answer includes citations/evidence references
↓
If ambiguous, system escalates or labels uncertainty
↓
Query and output are logged
```

### 8.5 Channel-to-Lead Journey

```text
Customer sends product inquiry through WhatsApp, webchat, email, broker, call center, or website form
↓
IIP creates or updates channel conversation
↓
AI classifies intent and confidence
↓
System routes to Sales & Leads workspace
↓
Staff reviews lead, product interest, missing information, and next action
↓
Human follows up, qualifies, or converts lead
↓
Audit and interaction history are stored
```

---

## 9. Functional Requirements

### 9.1 Requirement Priority Definitions

| Priority | Meaning |
|---|---|
| P0 | Mandatory for MVP launch and safe operation |
| P1 | Important for MVP pilot success |
| P2 | Useful but can move to Phase 2 |
| P3 | Future roadmap |

### 9.2 Platform and Tenant Requirements

| ID | Requirement | Priority |
|---|---|---|
| PRD-PLAT-001 | The platform must support insurer tenant setup. | P0 |
| PRD-PLAT-002 | Every claim, customer, document, lead, conversation, task, audit event, and AI run must carry tenant context. | P0 |
| PRD-PLAT-003 | The platform must support workspace/subtenant segmentation where required by branch, region, product line, or business unit. | P1 |
| PRD-PLAT-004 | The platform must support role-aware navigation and permissioned actions. | P0 |
| PRD-PLAT-005 | The platform must support environment labels for development, staging, pilot, and production. | P1 |

### 9.3 Authentication and Access Requirements

| ID | Requirement | Priority |
|---|---|---|
| PRD-AUTH-001 | Staff users must authenticate through Microsoft Entra ID or an approved enterprise identity provider. | P0 |
| PRD-AUTH-002 | The platform must support role-based access control for insurer staff and platform-side users. | P0 |
| PRD-AUTH-003 | Sensitive actions must be authorized server-side, not only hidden in the frontend. | P0 |
| PRD-AUTH-004 | The platform must support least-privilege access by default. | P0 |
| PRD-AUTH-005 | Privileged admin and support access must be auditable. | P0 |

### 9.4 Command Center Requirements

| ID | Requirement | Priority |
|---|---|---|
| PRD-CMD-001 | The Command Center must provide a role-based operational overview. | P0 |
| PRD-CMD-002 | It must show open claims, SLA risk, missing documents, pending review, customer update backlog, and AI activity. | P0 |
| PRD-CMD-003 | It must support chat-first queries for operational questions. | P1 |
| PRD-CMD-004 | It must display recommended next actions without overwhelming the user. | P1 |
| PRD-CMD-005 | It must expose loading, empty, error, unauthorized, and not-configured states. | P0 |

### 9.5 Channels Requirements

| ID | Requirement | Priority |
|---|---|---|
| PRD-CHAN-001 | The platform must provide an internal unified Channel Inbox. | P0 |
| PRD-CHAN-002 | Channel Inbox must support WhatsApp, webchat, email, SMS, broker, call center, and website form sources where configured. | P1 |
| PRD-CHAN-003 | Channel conversations must show customer, channel, intent, AI confidence, suggested routing, status, and assigned officer. | P0 |
| PRD-CHAN-004 | Channel health must show connected, degraded, disconnected, and not-configured states. | P1 |
| PRD-CHAN-005 | Channel events must be traceable to downstream work items such as claim, lead, task, or customer record. | P0 |

### 9.6 Customers Requirements

| ID | Requirement | Priority |
|---|---|---|
| PRD-CUST-001 | The platform must provide an internal Customer 360 workspace. | P0 |
| PRD-CUST-002 | Customers must not log into the internal app in V1. | P0 |
| PRD-CUST-003 | Customer 360 must show identity, contact, policies, claims, interactions, documents, consent, payments, and opportunities where available. | P1 |
| PRD-CUST-004 | Customer records must support consent status. | P0 |
| PRD-CUST-005 | Sensitive customer data must be masked where appropriate. | P0 |

### 9.7 Sales and Leads Requirements

| ID | Requirement | Priority |
|---|---|---|
| PRD-LEAD-001 | The platform must provide an internal Sales & Leads workspace. | P1 |
| PRD-LEAD-002 | Leads must be created or updated from WhatsApp, webchat, broker, call center, email, website, agent, and bancassurance channels where configured. | P1 |
| PRD-LEAD-003 | Leads must show source, product interest, status, AI confidence, missing information, next action, assigned owner, and quote value where available. | P1 |
| PRD-LEAD-004 | AI may recommend next actions but must not bind policies or provide final regulated advice. | P0 |

### 9.8 Claims Requirements

| ID | Requirement | Priority |
|---|---|---|
| PRD-CLM-001 | The platform must support motor claim intake. | P0 |
| PRD-CLM-002 | Claims Inbox must support filtering by status, risk, SLA, channel, assigned officer, and completeness. | P0 |
| PRD-CLM-003 | Claims must have statuses such as New, Intake Started, Documents Pending, Documents Uploaded, AI Processing, Needs Review, Missing Information, Fraud Signal Detected, Escalated, Returned to Customer, Exported, and Closed. | P0 |
| PRD-CLM-004 | Claims must support assignment and reassignment. | P0 |
| PRD-CLM-005 | Claims must support escalation. | P0 |
| PRD-CLM-006 | Claims must show SLA status and breach risk. | P0 |
| PRD-CLM-007 | Claims must support customer-safe update drafting and human review before sending. | P1 |
| PRD-CLM-008 | Claims must support claim pack export. | P0 |

### 9.9 Document Intelligence Requirements

| ID | Requirement | Priority |
|---|---|---|
| PRD-DOC-001 | The platform must support document upload and storage. | P0 |
| PRD-DOC-002 | The platform must classify supported motor claim document types. | P0 |
| PRD-DOC-003 | The platform must perform OCR and field extraction. | P0 |
| PRD-DOC-004 | Extracted fields must show confidence scores where available. | P0 |
| PRD-DOC-005 | Low-confidence fields must be routed to human review. | P0 |
| PRD-DOC-006 | The platform must detect missing required documents. | P0 |
| PRD-DOC-007 | The platform must support human correction of extracted fields. | P0 |
| PRD-DOC-008 | The platform must preserve source document references for extracted fields. | P0 |
| PRD-DOC-009 | The platform should detect unreadable, blurry, incomplete, expired, duplicate, or mismatched documents where possible. | P1 |

### 9.10 AI Requirements

| ID | Requirement | Priority |
|---|---|---|
| PRD-AI-001 | AI must produce claim summaries for human review. | P0 |
| PRD-AI-002 | AI outputs affecting claims, policies, customers, or regulated workflows must be source-grounded where possible. | P0 |
| PRD-AI-003 | AI must show uncertainty or confidence where available. | P0 |
| PRD-AI-004 | AI must not fabricate policy terms, exclusions, claim obligations, dates, limits, amounts, or regulatory obligations. | P0 |
| PRD-AI-005 | AI must not approve, reject, value, settle, or accuse. | P0 |
| PRD-AI-006 | AI tools must be scoped by tenant, user role, active workflow, and approved tool registry. | P0 |
| PRD-AI-007 | Every AI action must be logged with enough metadata to support review. | P0 |

### 9.11 Audit and Compliance Requirements

| ID | Requirement | Priority |
|---|---|---|
| PRD-AUD-001 | The platform must log user actions, AI actions, document events, workflow events, approvals, overrides, escalations, and exports. | P0 |
| PRD-AUD-002 | Audit logs must include actor, timestamp, tenant, workspace, case, action type, and relevant evidence references. | P0 |
| PRD-AUD-003 | Human overrides of AI outputs must be logged. | P0 |
| PRD-AUD-004 | The platform must support regulator-ready audit export in later versions. | P1 |
| PRD-AUD-005 | The platform must support claims register export or reporting. | P1 |

### 9.12 Admin and Configuration Requirements

| ID | Requirement | Priority |
|---|---|---|
| PRD-ADM-001 | Admins must configure users and roles. | P0 |
| PRD-ADM-002 | Admins must configure claim types and document checklists. | P0 |
| PRD-ADM-003 | Admins must configure channels and integrations. | P1 |
| PRD-ADM-004 | Admins must configure retention and consent policies in later phases. | P2 |
| PRD-ADM-005 | Admins must configure AI provider modes and model settings where permitted. | P1 |

---

## 10. Non-Functional Requirements

### 10.1 Security

| ID | Requirement | Priority |
|---|---|---|
| PRD-NFR-SEC-001 | All sensitive endpoints must enforce server-side authorization. | P0 |
| PRD-NFR-SEC-002 | Secrets must be stored in Azure Key Vault or equivalent secure secret storage. | P0 |
| PRD-NFR-SEC-003 | Customer data must be tenant-isolated. | P0 |
| PRD-NFR-SEC-004 | Sensitive data must be encrypted in transit and at rest. | P0 |
| PRD-NFR-SEC-005 | Privileged access must be logged. | P0 |
| PRD-NFR-SEC-006 | Production must not use mock auth bypasses or fake endpoints. | P0 |

### 10.2 Reliability and Resilience

| ID | Requirement | Priority |
|---|---|---|
| PRD-NFR-REL-001 | The system must show clear degraded and not-configured states for unavailable providers. | P0 |
| PRD-NFR-REL-002 | Background processing should use queues or durable workflows where appropriate. | P1 |
| PRD-NFR-REL-003 | Channel events should support retry and failure visibility. | P1 |
| PRD-NFR-REL-004 | Hybrid channel buffering should be specified for clients with connectivity concerns. | P2 |

### 10.3 Performance

| ID | Requirement | Priority |
|---|---|---|
| PRD-NFR-PERF-001 | Common dashboard and list views should load quickly under pilot volumes. | P0 |
| PRD-NFR-PERF-002 | Document processing may run asynchronously and must show progress/status. | P0 |
| PRD-NFR-PERF-003 | Search, filters, and pagination must be supported for high-volume lists. | P0 |

### 10.4 Observability

| ID | Requirement | Priority |
|---|---|---|
| PRD-NFR-OBS-001 | The platform must capture API, workflow, queue, integration, document AI, and AI provider telemetry. | P0 |
| PRD-NFR-OBS-002 | Support & Health screen must expose provider health and key failures. | P1 |
| PRD-NFR-OBS-003 | Application Insights and Log Analytics should be used for monitoring. | P0 |

### 10.5 Accessibility and Usability

| ID | Requirement | Priority |
|---|---|---|
| PRD-NFR-UX-001 | The UI should be keyboard-friendly and accessible. | P1 |
| PRD-NFR-UX-002 | Empty, loading, error, unauthorized, and not-configured states must be designed. | P0 |
| PRD-NFR-UX-003 | The UI must distinguish AI suggestion, staff decision, system automation, customer action, and admin configuration. | P0 |
| PRD-NFR-UX-004 | Information density should support insurance operations without becoming cluttered. | P1 |

---

## 11. User Experience Requirements

### 11.1 Three-Panel Architecture

IIP should use a consistent three-panel enterprise workspace:

```text
Left Navigation
+
Main Chat / Workflow Workspace
+
Right Contextual Copilot Panel
```

### 11.2 Core Layout

| Area | Requirement |
|---|---|
| Left navigation | Persistent, role-aware, collapsible navigation across modules |
| Top bar | Workspace/environment, global search, notifications, user/role, support access |
| Main workspace | Chat-first interaction and structured workflow content |
| Right Copilot panel | Contextual summaries, next actions, evidence, confidence, risks, recommendations |
| Status bar / health | Connectivity, last sync, active workflows, provider state, queue depth where useful |

### 11.3 Core Navigation

MVP and near-term navigation should include:

1. Command Center
2. Channels
3. Customers
4. Sales & Leads
5. Claims
6. Documents
7. Knowledge Base
8. Approvals
9. Compliance & Audit
10. Reports & Analytics
11. Integrations
12. Admin Settings
13. Support & Health

### 11.4 UX Principles

IIP should feel intelligent but controlled. Every major page should include some combination of:

- AI summary card
- Recommended next action
- Smart checklist
- Risk or issue flags
- Confidence indicators
- Source evidence
- Timeline of events
- Human approval status
- Action buttons connected to workflow
- Contextual assistant panel

### 11.5 V1 Screen Priority

| Priority | Screen |
|---|---|
| 1 | Login / tenant selection |
| 2 | Command Center |
| 3 | Channels Inbox |
| 4 | Sales & Leads Inbox |
| 5 | Customer 360 |
| 6 | Claims Inbox / Work Queue |
| 7 | FNOL / New Claim Intake |
| 8 | Case Review |
| 9 | Document Extraction Review |
| 10 | Audit Trail |
| 11 | Admin Settings |
| 12 | Support & Health |

---

## 12. Data Requirements

### 12.1 Canonical Entities

The MVP should define or refine the following entities:

| Entity | Purpose |
|---|---|
| Tenant | Insurer or customer organization context |
| Workspace | Branch, region, product line, business unit, or subtenant context |
| User | Internal staff or platform-side user |
| Role | Permission group |
| Customer | Customer/policyholder/prospect identity record |
| Policy | Policy metadata and coverage context |
| Claim | Claim case record |
| ClaimDocument | Uploaded document linked to a claim |
| DocumentExtraction | Extracted fields and confidence scores |
| ChannelConversation | External channel interaction record |
| Lead | Acquisition opportunity or quote interest record |
| Task | Work item assigned to user/team |
| Approval | Human review and approval record |
| AuditEvent | Immutable event log |
| IntegrationEvent | External system or channel event |
| AIRun | AI prompt/tool/model/output metadata |
| KnowledgeSource | Approved policy/SOP/FAQ/document source |

### 12.2 Data Principles

- Every record must be tenant-aware.
- Every sensitive record must support auditability.
- AI outputs must reference evidence where relevant.
- Raw documents, extracted fields, reviewed fields, and final exports must be distinguishable.
- Customer-facing content must be separated from internal notes, risk flags, and fraud signals.
- Production data must not be used in demos unless authorized and governed.

### 12.3 Motor Claim Data Fields

| Category | Example Fields |
|---|---|
| Claim identity | Claim reference, policy number, status, assigned officer, SLA status |
| Customer | Name, phone, email, ID, address, consent status |
| Incident | Date, time, location, description, third parties, police reference |
| Vehicle | Registration number, make, model, owner, driver, permit details |
| Documents | Document type, upload date, confidence, status, source file |
| Financial | Repair estimate, invoice amount, deductible/excess, reserve suggestion |
| Review | Reviewer, decision notes, approvals, escalations, overrides |
| Audit | Created by, updated by, AI runs, evidence references, export events |

---

## 13. AI and Human Governance

### 13.1 AI May Do

AI may:

- Summarize claims
- Classify documents
- Extract fields
- Flag missing information
- Identify possible inconsistencies
- Draft customer-safe messages
- Route work
- Retrieve policy/SOP knowledge
- Suggest next actions
- Prepare review notes
- Generate evidence packs

### 13.2 AI Must Not Do

AI must not:

- Approve a claim
- Reject a claim
- Repudiate a claim
- Approve settlement
- Release payment
- Bind a policy
- Set final premium
- Make final underwriting acceptance or rejection
- Accuse a customer of fraud
- Make complaint final response
- Submit regulatory filings without human approval
- Provide unsupported policy or legal interpretation

### 13.3 Required AI Output Rules

For sensitive outputs, AI must:

- Use approved tenant knowledge or case evidence where applicable.
- Show source references or evidence IDs where applicable.
- Show uncertainty or confidence where available.
- Label fraud outputs as signals, not conclusions.
- Escalate missing, conflicting, expired, or low-confidence evidence.
- Log prompt version, retrieved evidence, tool calls, model/provider metadata, output, confidence, and user action.

---

## 14. Integrations

### 14.1 MVP Integration Scope

| Integration | Purpose | Direction | MVP Priority |
|---|---|---|---|
| WhatsApp Business API | Customer claims initiation, acquisition, missing-document requests | Two-way | Critical |
| Microsoft Graph / Outlook / Exchange | Email ingestion, customer communication, shared mailbox processing | Read/write with approval | High |
| Azure Blob Storage | Store claim documents and evidence | Read/write | Critical |
| Azure SQL | Store structured platform data | Read/write | Critical |
| Azure AI Document Intelligence | OCR, classification, extraction | Processing | Critical |
| Azure AI Search | Policy/document retrieval and RAG | Query | Critical |
| Core policy admin system | Policy validation and customer/policy lookup | Prefer read-only | Phase 1/2 |
| Claims management system | Sync or export claim packs | Controlled write/export | Phase 2 |
| Power BI / Fabric | Analytics and reporting | Read/reporting | Phase 2 |
| SMS gateway | Fallback notifications | Write | Later |
| Mobile money | Premium/payment/payout workflows | Controlled write | Later unless pilot requires |
| IRA/regulatory systems | Claims/complaints reporting | Export/API | Later |

### 14.2 Required MVP API Endpoints

The implementation baseline identifies these required endpoint groups for V1 internal workspaces:

```text
GET /api/v1/channels/overview
GET /api/v1/customers/overview
GET /api/v1/sales-leads/overview
```

Additional claims, document, audit, and admin endpoints should be defined in the FSD/API specification.

### 14.3 Integration Principles

- Use read-only integration where possible.
- Do not give AI direct generic database access.
- Avoid `query_database(sql)` style agent tools.
- Use scoped integration tools and APIs.
- Use human-approved write-back for sensitive updates.
- Support file/CSV/API fallback where core-system APIs are unavailable.
- Log every integration event.

---

## 15. Deployment and Architecture Direction

### 15.1 Recommended Deployment Direction

For enterprise insurer pilots, the preferred direction is:

> **Azure Managed Application or private Azure deployment, with a lighter Otic-hosted SaaS path available for fast demos or smaller pilots.**

### 15.2 Deployment Options

| Option | Best For | Notes |
|---|---|---|
| Otic-hosted SaaS | Fast demo, pilot, small insurer, broker | Faster onboarding but must prove tenant isolation |
| Enterprise private deployment | Large insurer, bank, HMO, regulated institution | Stronger trust and isolation |
| Azure Managed Application | Marketplace/private offer path | Preferred long-term enterprise packaging |
| Hybrid control plane/data plane | Mature production model | Otic controls product lifecycle; insurer controls/isolates data plane |
| Local/offline mode | Future special deployment | Not MVP unless funded |

### 15.3 Hybrid Cloud Mirror / Channel Resilience

The MVP should not promise full offline operations. It should document a **resilient channel buffer** for connectivity-sensitive clients:

- Acknowledge inbound channel events quickly.
- Queue messages safely.
- Retry failed events with backoff.
- Expose queue health and failed events.
- Allow recovery after temporary outage.
- Treat full offline claims processing as a future phase.

### 15.4 Azure Service Direction

| Platform Need | Azure Service |
|---|---|
| Frontend hosting | Azure App Service / Static Web Apps / Container Apps |
| Backend API | .NET 8 on App Service / Container Apps |
| Workflow orchestration | Azure Functions / Durable Functions |
| Database | Azure SQL |
| Document storage | Azure Blob Storage |
| Messaging | Azure Service Bus / Event Grid |
| Search/RAG | Azure AI Search |
| Document extraction | Azure AI Document Intelligence |
| AI reasoning | Azure OpenAI |
| Secrets | Azure Key Vault |
| Identity | Microsoft Entra ID |
| Monitoring | Application Insights, Log Analytics |
| Security monitoring | Microsoft Sentinel / Defender for Cloud |
| API governance | Azure API Management |

### 15.5 Region Strategy

Recommended starting region: **South Africa North**, subject to service/model availability and legal review.

Fallback: **West Europe**, subject to data residency/legal review.

Engineering must confirm Azure OpenAI model availability, Document Intelligence availability, AI Search availability, quotas, latency, cost, and data processing behavior before production commitments.

---

## 16. Compliance and Privacy Requirements

### 16.1 Compliance Position

IIP must be designed as a decision-support and workflow intelligence system, not as a system that replaces qualified insurance staff in regulated decisions.

### 16.2 MVP-Critical Controls

| Control | Requirement |
|---|---|
| Human decision ownership | Every regulated claim/underwriting/payment action must have a human owner |
| Audit logging | AI and human actions must be logged |
| Consent | Customer data collection must be purpose-aware and logged |
| Tenant isolation | Insurer data must be isolated |
| Role-based access | Staff actions must match role permissions |
| Evidence-linked outputs | AI outputs must reference documents/evidence where relevant |
| Claims SLA visibility | Claims workflows must show SLA status |
| Claims register readiness | Claims data should be exportable in structured form |
| Data minimization | Only collect required information |
| Cross-border review | Legal review required before final region/model decision |

### 16.3 Legal Review Items

The following require legal/compliance confirmation:

- Uganda IRA expectations for AI-assisted claims workflows
- Whether regulatory sandbox approval is required or beneficial
- Data residency and cross-border processing implications
- Azure OpenAI region/model data handling
- Required consent language for WhatsApp claims initiation
- Required claims register format and fields
- Complaint handling and retention obligations
- Data subject access/correction/deletion processes
- Breach notification requirements

This PRD is not legal advice.

---

## 17. Success Metrics

### 17.1 MVP Success Metrics

| Metric | Target Direction |
|---|---|
| Claim intake time | Reduce |
| Missing-document follow-ups | Reduce |
| Manual document review time | Reduce |
| Claim summary preparation time | Reduce |
| Customer first-response time | Improve |
| Claims officer productivity | Improve |
| Claim evidence completeness | Improve |
| Audit traceability | Improve |
| Human review completion rate | Improve |
| SLA visibility | Improve |
| AI output correction rate | Track and reduce over time |
| User satisfaction | Positive pilot feedback |

### 17.2 Product Quality Metrics

| Metric | Target Direction |
|---|---|
| Document extraction confidence | Track by document type and field |
| Low-confidence routing | All low-confidence sensitive fields routed to review |
| AI hallucination incidents | Zero tolerance for unsupported policy/customer claims |
| Audit completeness | All material actions logged |
| Endpoint reliability | Track API failures and provider health |
| Error recovery | Failed jobs visible and retryable |

### 17.3 Business Metrics

| Metric | Target Direction |
|---|---|
| Claims handling cost | Reduce |
| Claims turnaround | Improve |
| Call center/query volume | Reduce for repetitive requests |
| Lead response speed | Improve |
| Quote/lead conversion | Improve in later phases |
| Renewal risk visibility | Improve in later phases |
| Fraud/inconsistency detection | Improve advisory signal coverage |

---

## 18. Release Phasing

### 18.1 MVP / Phase 1

Focus: Motor Claims and Document Intelligence.

Includes:

- Tenant setup
- Entra ID authentication
- RBAC
- Command Center
- Channels Inbox
- Customer 360
- Sales & Leads
- Claims Inbox / Work Queue
- FNOL/New Claim Intake
- Document upload
- OCR/classification/extraction
- Missing-document checklist
- Claim summary
- Fraud-signal flags
- Human review cockpit
- Audit logs
- Claim pack export
- Basic policy Q&A
- Support & Health

### 18.2 Phase 2

Focus: broader insurance operations.

Potential modules:

- Underwriting Support / Proposal Parsing
- Early Warning / Retention Intelligence
- Advanced Fraud Signal module
- Power BI/Fabric analytics
- Policy admin integration
- Claims system integration
- Advanced approval workflows
- Customer-safe claim tracker
- Complaint management
- Retention and renewal workflows

### 18.3 Phase 3

Focus: commercial scale and advanced modules.

Potential modules:

- Azure Managed Application / Marketplace packaging
- Advanced customer acquisition journeys
- Broker portal or broker workspace
- Mobile money/payment workflows
- Voice/call agent
- Multilingual expansion
- Reinsurance reporting
- Agriculture/microinsurance modules
- Cross-country regulatory configuration
- Advanced AI evaluation and quality monitoring

---

## 19. Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Product name confusion from ACIP/IIP history | Documentation and branding inconsistency | Use IIP everywhere; label ACIP as superseded |
| Scope creep beyond motor claims MVP | Delays and unclear implementation | Separate MVP from roadmap modules |
| AI perceived as replacing claims officers | Customer/regulator/staff resistance | Use human-reviewed messaging and approval gates |
| Unsupported AI answers | Compliance and trust risk | RAG/source grounding, citations, uncertainty, human review |
| Poor document samples | Weak extraction and demo quality | Build synthetic/redacted motor claim test packs |
| Core system integration delays | MVP blocked by client IT | Support file/API/manual import fallback |
| WhatsApp provider uncertainty | Channel implementation delay | Decide Meta direct/Twilio/Infobip early |
| Azure region/model unavailability | Deployment delay or cost increase | Confirm quotas and fallback region early |
| Data residency uncertainty | Legal/compliance risk | Legal review before production commitments |
| Production mocks | Trust and quality risk | No mock data/fake endpoints in staging/production |
| Offline expectations misunderstood | Overpromising | Position MVP as resilient channel buffering, not full offline platform |

---

## 20. Assumptions

1. The official product name is **Insurance Intelligence Platform (IIP)**.
2. The first MVP customer is a mid-to-large insurance company.
3. The first MVP claim type is motor claims.
4. Customers do not log into the internal IIP app in V1.
5. External customer interactions enter through WhatsApp, email, web, broker, call center, or other configured channels.
6. The platform is Azure-first.
7. The main backend is .NET 8.
8. The frontend is Next.js / React / TypeScript / Tailwind.
9. Human-in-the-loop governance is non-negotiable.
10. Full offline mode is not MVP.
11. Azure Managed Application/private deployment is preferred for enterprise insurer pilots, but Otic-hosted SaaS may be used for faster demos.
12. Legal review is required before production decisions on data residency and AI processing regions.

---

## 21. Open Questions

| ID | Question | Owner |
|---|---|---|
| OQ-001 | Which insurer is the confirmed first pilot customer? | Sales / Leadership |
| OQ-002 | Which WhatsApp provider will be used: Meta direct, Twilio, Infobip, or another provider? | Engineering |
| OQ-003 | Is Azure Managed Application required for the first pilot or only later? | Cloud Architect / Leadership |
| OQ-004 | Which Azure region and model deployments are approved and available? | Cloud Architect |
| OQ-005 | Which real policy/claims systems must be integrated first? | Pilot Customer / Engineering |
| OQ-006 | What is the legally approved data residency posture? | Legal / Compliance |
| OQ-007 | What exact claims register format is required? | Compliance / Claims SME |
| OQ-008 | What are the pilot volume assumptions for claims and documents? | Product / Pilot Customer |
| OQ-009 | What pricing model will be used for pilot and post-pilot? | Sales / Finance |
| OQ-010 | What current UI screens are already implemented and production-ready? | Engineering |
| OQ-011 | What document templates and synthetic test packs are available? | Product / Engineering |
| OQ-012 | What languages must be supported in MVP? | Product / Pilot Customer |

---

## 22. Acceptance Criteria for MVP PRD Completion

This PRD is ready to hand off into FSD and implementation planning when:

- Product name is confirmed as Insurance Intelligence Platform (IIP).
- MVP is confirmed as Motor Claims and Document Intelligence.
- First pilot customer type is confirmed.
- Customer/internal user boundary is confirmed.
- Human-in-the-loop rules are accepted by product, engineering, and leadership.
- MVP modules are agreed.
- Out-of-scope items are accepted.
- Core Azure architecture direction is accepted.
- Legal review items are acknowledged.
- Open questions are assigned to owners.

---

## 23. Immediate Next Actions

| # | Action | Owner |
|---|---|---|
| 1 | Confirm IIP as official product name across all documents and repos. | Leadership |
| 2 | Update README, AGENTS.md, prompts, UI labels, and backlog to use IIP. | Product / Engineering |
| 3 | Convert this PRD into an FSD with screen-level and workflow-level specifications. | Product |
| 4 | Create Motor Claims Workflow Specification. | Product / Claims SME |
| 5 | Create Document Intelligence Specification and extraction matrix. | Product / AI Engineering |
| 6 | Create Data Model and Canonical Entity Specification. | Engineering / Architecture |
| 7 | Create API Specification / OpenAPI plan. | Backend Engineering |
| 8 | Create AI Governance and Human Review Policy. | AI / Compliance |
| 9 | Confirm WhatsApp provider and channel architecture. | Engineering |
| 10 | Confirm Azure region, quotas, and service availability. | Cloud Architect |
| 11 | Prepare legal review brief for Uganda privacy, IRA, and cross-border processing. | Legal / Compliance |
| 12 | Audit current UI/codebase against the target three-panel architecture. | Frontend Engineering |
| 13 | Create synthetic/redacted motor claim document packs. | Product / Engineering |
| 14 | Produce implementation backlog from this PRD and the upcoming FSD. | Product / Delivery |

---

## Appendix A - MVP Motor Claim Document Checklist

| Document | Required for MVP | Purpose |
|---|---|---|
| Claim form | Yes | Captures claim/FNOL details |
| Police report | Yes | Validates accident report, date, place, parties |
| National ID | Yes | Identity verification |
| Driving permit | Yes | Driver eligibility and expiry check |
| Vehicle photos | Yes | Damage evidence |
| Repair quotation | Yes | Estimated repair cost |
| Policy schedule | Yes | Policy number, insured, coverage, limits |
| Assessor report | Recommended | Damage assessment and recommendation |
| Receipt/invoice | Optional | Payment or repair evidence |
| Vehicle logbook | Optional | Ownership validation |
| Witness statement | Optional | Supporting evidence |
| Medical report | Optional | Injury-related extension |

---

## Appendix B - Document Intelligence Test Cases

The MVP document test pack should include:

- Blurry image
- Cropped document
- Expired driving permit
- Mismatched claimant name
- Mismatched incident date
- Duplicate police report
- Inflated repair quotation
- Missing stamp/signature
- Wrong document type uploaded
- Low-confidence OCR
- Redacted real document
- Fully synthetic demo document

---

## Appendix C - Key Product Guardrail

The following guardrail must be repeated in implementation prompts, PRD/FSD/TSD, UI copy, QA tests, and AI agent policies:

> IIP does not approve claims, reject claims, bind policies, set premiums, accuse customers of fraud, release payments, or make final underwriting decisions. IIP prepares the work; humans make the regulated decisions.
