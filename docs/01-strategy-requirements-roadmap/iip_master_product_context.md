# Insurance Intelligence Platform (IIP) - Master Product Context

**Document type:** Master Product Context / Source-of-Truth Baseline  
**Product name:** Insurance Intelligence Platform  
**Acronym:** IIP  
**Prepared for:** Otic Group / IIP product and engineering teams  
**Date:** 30 June 2026  
**Status:** Consolidated working baseline for PRD, FSD, TSD, UI/UX, architecture, and delivery planning  
**Classification:** Internal - Product, Engineering, Architecture, Cloud, Security, AI, Leadership, and Delivery Reference

---

## 1. Executive Summary

The **Insurance Intelligence Platform (IIP)** is an Azure-native, human-reviewed insurance operations platform for African insurers, brokers, TPAs, HMOs, bancassurance partners, and microinsurance providers. It is designed to help insurance institutions acquire customers, process claims, review documents, support underwriting, detect risk signals, manage customer interactions, and maintain auditable insurance workflows through AI-assisted, source-grounded, role-aware workspaces.

IIP should not be positioned as a generic chatbot or as an autonomous claims decision system. Its value is that it acts as an intelligent operational layer over existing insurer processes and systems. It receives fragmented customer, broker, email, WhatsApp, document, and internal workflow inputs, converts them into structured cases, prepares evidence-linked summaries, detects missing information, routes work, drafts customer-safe communications, and gives staff better decision support.

The core principle is:

> **AI prepares insurance work. Humans make regulated insurance decisions.**

The first MVP should focus on **Motor Claims and Document Intelligence** for a mid-to-large insurance company. The MVP should support WhatsApp/email/channel-based FNOL, document upload, OCR/extraction, missing-document detection, claim summary generation, fraud-signal flagging, human review, audit logging, and claim pack export.

The official name for all future documents should be **Insurance Intelligence Platform (IIP)**. Earlier references to **ACIP - Agentic Insurance Intelligence Platform** should be treated as superseded naming references, not the current official product name.

---

## 2. Source Coverage and Truncation Notes

This master context merges information from multiple source streams, including product summaries, implementation notes, UI/UX blueprints, market benchmark reports, meeting transcripts, DeepSeek findings, Antigravity/codebase findings, and development lifecycle guidance.

### 2.1 Source Handling Rules Used

1. **Latest explicit user instruction wins.** The product name is now **Insurance Intelligence Platform (IIP)**, not ACIP.
2. **Codebase-aware findings outrank generic research where implementation state is concerned.** Antigravity findings are treated as stronger for codebase and current UI/module implementation details.
3. **Human-in-the-loop governance is non-negotiable.** Any source implying autonomous regulated decisions has been normalized to human-reviewed decision support.
4. **MVP scope is separated from roadmap ambition.** Claims and document intelligence form the first MVP; customer acquisition, fraud intelligence, underwriting, early warning, and advanced analytics remain modules in the broader roadmap.
5. **Unconfirmed items are marked as assumptions, open questions, or decisions requiring confirmation.**

### 2.2 Truncation Statement

Some uploaded source previews were visibly truncated by the environment during inspection. No major product decision has been intentionally removed. Where source detail was too broad for this master context, it has been consolidated into canonical direction, contradiction registers, missing-information logs, and roadmap notes. Future PRD/FSD/TSD documents should expand relevant sections rather than treat this master context as the final detailed specification.

---

## 3. Canonical Product Identity

| Item | Canonical Direction |
|---|---|
| Official product name | **Insurance Intelligence Platform** |
| Acronym | **IIP** |
| Superseded name | ACIP / Agentic Insurance Intelligence Platform |
| Internal repo/code name | IIP |
| Public-facing name | Insurance Intelligence Platform |
| Suggested branded phrasing | Insurance Intelligence Platform by Otic, only where company attribution is needed |
| Product category | Azure-native insurance operations intelligence platform |
| Strategic positioning | Human-reviewed AI operations layer for insurance workflows |
| Primary market | Uganda first, Africa-configurable |
| First MVP wedge | Motor Claims and Document Intelligence |

### 3.1 Naming Decision

All future PRD, FSD, TSD, UI/UX, implementation, pitch, and roadmap materials should use:

> **Insurance Intelligence Platform (IIP)**

Earlier references to ACIP should not be deleted from historical notes, but should be labeled as superseded.

### 3.2 Product Definition

IIP is an Azure-native, human-reviewed insurance operations platform that helps insurers convert fragmented insurance inputs into structured, auditable, AI-assisted workflows. It supports customer acquisition, claims processing, document intelligence, policy knowledge retrieval, underwriting support, fraud-signal detection, early warning/retention, customer communication, approvals, audit trails, and operational reporting.

IIP sits above, beside, or in front of existing insurance systems. It is not meant to replace core policy administration, claims management, finance, ERP, CRM, or regulatory systems on day one.

---

## 4. North Star Positioning

### 4.1 Final Product Positioning

**IIP is an Africa-first, Azure-native insurance intelligence platform that helps insurers acquire customers, process claims, review documents, support underwriting, and manage customer workflows through human-reviewed AI agents, auditable evidence, and configurable insurance workflows.**

### 4.2 Safer Enterprise Positioning

Use language such as:

- Human-reviewed insurance intelligence
- AI-assisted claims operations
- Controlled insurance automation
- Source-grounded policy intelligence
- Agentic workflow orchestration
- Regulator-ready workflow records
- Auditable decision support
- Role-aware insurance operations workspace

Avoid language such as:

- Fully autonomous claims approval
- Autonomous underwriting
- AI claims decisioning
- Automatic policy binding
- AI fraud accusation
- Unreviewed premium pricing
- Unrestricted customer advice
- Automatic settlement release

### 4.3 Core Product Promise

IIP helps insurers move from fragmented, manual, document-heavy workflows to structured, evidence-linked, human-reviewed workflows.

| Current State | Target State with IIP |
|---|---|
| Manual document processing | AI-assisted document classification, OCR, extraction, and review |
| Slow claims intake | Guided FNOL and structured claims registration |
| Paper-heavy workflows | Digital evidence packs and case files |
| Fragmented policy knowledge | Source-grounded policy and SOP retrieval |
| Repetitive customer support | AI-assisted support drafts and escalation |
| Weak customer education | Controlled, multilingual insurance explanations |
| Poor claim visibility | Status, SLA, missing-document, and assignment tracking |
| Informal-market friction | WhatsApp-first engagement and guided onboarding |
| Inconsistent staff responses | Standardized knowledge-backed guidance |
| Weak audit trail | Human-reviewed, regulator-ready logs |

---

## 5. Strategic Market Context

### 5.1 Uganda-First Logic

Uganda is a suitable first market because the insurance opportunity is clear: low insurance penetration, document-heavy insurer operations, customer trust challenges, manual claims handling, limited customer education, and growing digital/mobile engagement. WhatsApp and mobile-first journeys are highly relevant to customer acquisition and claims initiation.

### 5.2 Africa-Configurable Logic

IIP should be Uganda-first but not Uganda-hardcoded. The product should eventually support:

- Country-specific regulatory configuration
- Currency configuration
- Language configuration
- Document template configuration
- Product-line configuration
- Insurer-specific workflow rules
- Claims threshold configuration
- Approval hierarchy configuration
- Retention policy configuration
- Customer-channel configuration

### 5.3 Main Business Problems IIP Addresses

| Problem | Business Impact | IIP Response |
|---|---|---|
| Low insurance penetration | Low policy uptake, weak renewal retention, poor trust | WhatsApp journeys, customer education, guided onboarding |
| Slow claims processing | Complaints, customer frustration, high support load | FNOL, document intelligence, missing-document checks, summaries |
| High operational cost | Staff workload, rework, high expense ratio | Automation of intake, extraction, summarization, routing |
| Internal knowledge fragmentation | Inconsistent service and slow decisions | Policy/SOP/claims knowledge retrieval |
| Weak customer touchpoint intelligence | Missed leads and poor digital conversion | Channels, Sales & Leads, customer-safe journeys |
| Weak document intelligence | Errors, leakage, fraud exposure, slow review | OCR, classification, validation, confidence scoring |
| Compliance and audit pressure | Regulatory risk and poor accountability | Audit trails, source citations, human approval, RBAC |

---

## 6. Business Objectives

### 6.1 Customer Acquisition Objectives

IIP should help insurers:

- Increase qualified insurance leads
- Simplify onboarding
- Improve customer education
- Support WhatsApp-native acquisition
- Support local-language engagement
- Guide prospects to suitable product information
- Hand off to human agents, brokers, or sales staff for final advisory or sales action

### 6.2 Claims Processing Objectives

IIP should help insurers:

- Reduce claims intake time
- Improve document completeness
- Generate claim checklists
- Reduce missing-document follow-ups
- Generate claim summaries
- Detect duplicate or suspicious submissions as advisory signals
- Improve customer claim-status visibility
- Route claims to human reviewers

### 6.3 Internal Operations Objectives

IIP should:

- Reduce staff document-search time
- Improve policy reference accuracy
- Standardize internal responses
- Assist claims officers
- Assist underwriters
- Improve staff onboarding
- Create reusable institutional knowledge

### 6.4 Customer Experience Objectives

IIP should:

- Provide guided support through familiar channels
- Support low-bandwidth digital interaction
- Support WhatsApp, web, email, broker, and future mobile channels
- Provide clear claim requirements
- Escalate sensitive issues to humans
- Improve customer trust through transparency

### 6.5 Governance Objectives

IIP must:

- Maintain audit logs
- Enforce human approval for regulated decisions
- Protect PII and sensitive data
- Support role-based access
- Provide source-backed AI outputs
- Prevent unsupported policy/claim assertions
- Support insurer-specific compliance rules

---

## 7. Target Institutions and Users

### 7.1 Target Institutions

| Institution | Primary Use Case |
|---|---|
| General insurers | Motor, property, marine, engineering, claims and underwriting support |
| Life insurers | Beneficiary support, life claims, policy servicing |
| Health insurers | Medical claims, provider invoice review, member support |
| Microinsurance providers | Simple claims, local-language education, low-cost onboarding |
| Brokers | Client submissions, renewals, claims follow-up, document packs |
| TPAs / HMOs | Health claims, provider document review, customer support |
| Bancassurance partners | Embedded customer education, lead qualification, renewals |
| Reinsurers | Later-phase bordereaux, treaty summaries, claims reporting |

### 7.2 First Pilot Customer

The first pilot customer should be a **mid-to-large insurance company**, preferably with meaningful motor claims volume and digital transformation appetite.

Reasons:

- Enough claims volume to prove ROI
- Direct operational pain in claims and document handling
- Stronger budget ownership than smaller intermediaries
- Better pilot evidence for the wider market
- Clear internal users across claims, operations, compliance, customer service, and IT

### 7.3 Internal User Personas

| Persona | Main Needs |
|---|---|
| Claims Officer | Claim summaries, document checklists, extraction review, customer updates |
| Claims Supervisor | Escalations, SLA view, approvals, workload management |
| Underwriter | Submission summaries, risk indicators, missing information |
| Customer Service Agent | Policy Q&A, customer guidance, escalation support |
| Compliance Officer | Audit logs, decision trails, regulator-ready evidence |
| Insurer Admin | Users, roles, workflows, integrations, tenant setup |
| Executive | Productivity, claims turnaround, cost reduction, adoption metrics |
| IT / Cloud Admin | Security, integrations, monitoring, deployment health |
| Otic Support Engineer | Tenant support, deployment monitoring, incident response |

### 7.4 External User Personas

External users interact through controlled channels rather than the internal staff app.

| Persona | Main Needs |
|---|---|
| Policyholder | Claim initiation, status, policy explanation, document guidance |
| Prospective Customer | Product education and onboarding guidance |
| Broker / Agent | Submission support, client summaries, renewal tracking |
| Beneficiary | Life claim guidance and document requirements |
| Health Member | Medical claim guidance and status update |
| Garage / Assessor / Provider | Document submission and claim-related evidence exchange, where configured |

---

## 8. Product Scope and Module Strategy

### 8.1 MVP Scope

The first release should be:

> **IIP Motor Claims and Document Intelligence MVP**

It should include:

- Tenant setup
- Internal staff login with Microsoft Entra ID
- Role-based access control
- Command Center
- Channels Inbox
- Sales & Leads Inbox
- Customer 360 internal workspace
- Claims intake dashboard
- WhatsApp/email/channel-based FNOL
- Document upload
- OCR and document classification
- Field extraction
- Missing-document checklist
- Claim summary generation
- Fraud-signal flags
- Human review cockpit
- Audit trail
- Claim pack export as PDF/CSV/JSON or controlled API payload
- Basic policy document Q&A
- Basic usage and operations dashboard
- Admin settings and integration configuration basics

### 8.2 Explicitly Out of Scope for MVP

Do not build these first:

- Autonomous claim approval
- Autonomous claim rejection
- Automatic settlement
- Final premium calculation
- Full underwriting pricing engine
- Automatic policy binding
- National ID integration unless confirmed
- Vehicle registry integration unless confirmed
- Medical provider integration unless confirmed
- Payment release automation
- Full voice/TTS support
- All regional languages
- All insurance product lines
- Complex reinsurance reporting
- Advanced Microsoft Fabric analytics
- Cross-country regulatory automation
- Full offline/local version

### 8.3 Broader Commercial Modules

| Module | Timing | Purpose |
|---|---|---|
| Claims Processing | MVP | Claims intake, document review, summary, human review |
| Document Intelligence | MVP | OCR, classification, extraction, validation, evidence links |
| Channels | MVP | Unified internal inbox for WhatsApp, email, SMS, broker, call center, web |
| Customers | MVP | Internal Customer 360 from normalized records |
| Sales & Leads | MVP | Customer acquisition work created from external channels |
| Fraud Signal | Phase 1/2 | Advisory fraud/inconsistency detection |
| Customer Acquisition | Phase 1/2 | Guided lead capture, product education, routing |
| Underwriting Support | Phase 2 | Submission parsing, risk summaries, missing information |
| Early Warning / Retention | Phase 2 | Churn risk, premium non-payment, proactive retention |
| Policy Servicing | Phase 2 | Renewals, endorsements, policy questions, servicing workflows |
| Advanced Analytics | Phase 2/3 | Power BI/Fabric executive insights |
| Voice / Call Agent | Future | AI-assisted calls, summaries, escalation |
| Regulatory Reporting | Future | Claims/complaints/regulatory exports |

---

## 9. MVP Motor Claims Workflow

### 9.1 Why Motor Claims First

Motor claims should be first because they are familiar, high-volume, document-heavy, and easier to demonstrate than more sensitive life or health workflows. They offer clear opportunities for document intelligence, missing-document detection, inconsistency checks, SLA tracking, fraud signals, and customer communication.

### 9.2 MVP Motor Claim Document Checklist

| Document | MVP Status | Purpose |
|---|---|---|
| Claim form | Required | Captures FNOL and incident details |
| Police report | Required | Accident verification and incident details |
| National ID | Required | Identity verification |
| Driving permit | Required | Driver eligibility and expiry check |
| Vehicle photos | Required | Damage evidence |
| Repair quotation | Required | Estimated repair cost |
| Policy schedule | Required | Policy number, coverage, limits |
| Assessor report | Strongly recommended | Assessment and damage validation |
| Receipt / invoice | Optional | Repair/payment evidence |
| Vehicle logbook | Optional | Ownership validation |
| Witness statement | Optional | Supporting evidence |
| Medical report | Optional | Injury-related extension |

### 9.3 Motor Claim Lifecycle

```text
Customer reports accident through WhatsApp, email, web, broker, or call center
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

### 9.4 Human Approval Points

Human approval is required for:

- Coverage determination
- Claim approval
- Claim rejection
- Settlement amount
- Fraud determination
- Payment release
- Final customer decision letter
- Any adverse customer outcome
- Any high-value or escalated claim action

### 9.5 Fraud-Signal Opportunities

The Fraud Signal module should be advisory only. It may flag:

- Duplicate claims for the same vehicle/date
- Mismatched claimant names
- Mismatched incident dates
- Expired driving permits
- Altered or duplicated police reports
- Inflated repair quotations
- Inconsistent damage photos and quotation line items
- Repeated claimants or providers
- Missing supporting evidence

It must not accuse a customer of fraud. It must label outputs as signals and require human investigation.

---

## 10. AI Agent and Intelligence Model

### 10.1 Agent Design Principle

IIP should use multiple specialized AI-assisted modules. These agents should not only answer chat messages; they should support workflows, generate structured outputs, and create auditable work products.

Every agent must operate within:

- Tenant boundaries
- Workspace boundaries
- User permissions
- Active workflow state
- Approved tool registry
- Approved knowledge sources
- Human approval rules

### 10.2 MVP Agent Inventory

| Agent / Capability | Purpose | Key Restriction |
|---|---|---|
| Orchestrator Agent | Routes requests, coordinates workflow context | Must not bypass human review |
| FNOL Intake Agent | Guides claim intake and creates structured intake records | Must not promise settlement |
| Document Intelligence Agent | Classifies documents, extracts fields, validates quality | Must not treat low-confidence extraction as fact |
| Missing Information Agent | Checks claim against required document checklist | Must not close claims automatically |
| Claims Summary Agent | Produces human-reviewable claim summary | Must not approve, reject, value, or settle claims |
| Fraud Signal Agent | Flags suspicious patterns for investigation | Must not accuse customer of fraud |
| Policy Intelligence Agent | Retrieves policy clauses and explains coverage/exclusions | Must not make binding legal interpretation |
| Customer Update Agent | Drafts customer-safe updates | Must require approval where sensitive |
| Multilingual Agent | Simplifies/translates customer guidance | Must preserve legal meaning and escalate uncertainty |
| Review and Audit Agent | Captures review trail and decision evidence | Must not alter or hide logs |

### 10.3 Future Agents

| Agent | Future Scope |
|---|---|
| Underwriting Support Agent | Submission summaries, risk factors, missing information, draft questions |
| Policy Servicing Agent | Renewals, endorsements, premium/status questions |
| Broker Assistant Agent | Broker submissions, renewal reminders, client summaries |
| Reinsurance Reporting Agent | Bordereaux, treaty summaries, recovery packs |
| Customer Acquisition Agent | Product education, lead qualification, onboarding support |
| Early Warning Agent | Retention, churn risk, premium non-payment, relationship manager prompts |
| Voice / Call Agent | Calls, transcripts, summaries, escalations, consent logs |

### 10.4 AI Grounding Rules

Any AI output that affects a customer, claim, policy, payment, complaint, underwriting workflow, compliance workflow, or broker workflow must:

- Retrieve from approved tenant knowledge first
- Apply tenant and workspace filters
- Cite source documents, sections, or evidence IDs where applicable
- Show confidence or uncertainty
- Escalate missing, conflicting, expired, or low-confidence evidence
- Log prompt version, retrieved evidence IDs, tool calls, model/provider metadata, output, confidence, and user action
- Never fabricate policy terms, exclusions, amounts, limits, dates, claim obligations, or regulatory obligations

Policy coverage questions must never be answered from model memory alone.

---

## 11. UI/UX Product Direction

### 11.1 Core Experience Vision

IIP should feel like a living insurance intelligence system, not a traditional insurance portal with a chatbot bolted on. Intelligence should be embedded into every screen, workflow, status, notification, case file, document view, and dashboard.

### 11.2 Three-Panel Architecture

The approved UX direction is a three-panel enterprise workspace:

```text
Persistent Left Navigation
↓
Main Chat / Work Workspace
↓
Right Contextual Copilot Panel
```

| Area | Purpose |
|---|---|
| Left Navigation | Role-aware navigation across Command Center, Channels, Customers, Sales & Leads, Claims, Documents, Knowledge, Approvals, Audit, Analytics, Integrations, Admin |
| Main Workspace | Chat-first and workflow-first operating area, with structured cards, tables, forms, and case views injected into the conversation/workspace |
| Right Copilot Panel | Contextual insight, evidence, risks, next actions, document preview, confidence, suggested actions |
| Top Bar | Workspace selector, environment, global search, alerts, user profile, role |
| Status Bar | Connection state, last sync, active workflows, queue count, provider health |

### 11.3 V1 Navigation Areas

| Area | Purpose |
|---|---|
| Command Center | Role-based operational overview and AI-first entry point |
| Channels | Unified internal inbox for WhatsApp, webchat, email, SMS, broker, call center |
| Customers | Internal Customer 360 workspace |
| Sales & Leads | Acquisition and lead pipeline fed by external channels |
| Claims | Claims inbox, FNOL, case review, document workflows |
| Documents | Upload, extraction review, validation, classification |
| Knowledge Base | Approved policy/SOP/claims knowledge |
| Approvals | Human review queue |
| Compliance & Audit | Audit logs, AI actions, human overrides, exports |
| Reports & Analytics | Operational performance, SLA, AI quality, cost/ROI |
| Integrations | Microsoft Graph, WhatsApp, APIs, storage, future systems |
| Admin Settings | Tenant, roles, workflows, document templates, AI/provider settings |
| Support & Health | Provider health, queues, errors, deployment health |

### 11.4 Internal vs Customer-Facing Clarification

Customers should not log into the internal IIP app in V1. The **Channels**, **Customers**, and **Sales & Leads** modules are internal insurer workspaces fed by external channels such as WhatsApp, WebChat, SMS, email, broker, and call center.

A future customer-safe portal can exist later, but it should not be confused with the internal Customer 360, Channels, or Claims operations workspace.

### 11.5 Priority Screens

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

## 12. Architecture and Technical Stack

### 12.1 Default Technical Stack

| Layer | Technology / Service | Purpose |
|---|---|---|
| Frontend | Next.js, React, TypeScript, Tailwind CSS | Main insurer workspace |
| Backend | ASP.NET Core Web API / .NET 8 | Platform APIs and business logic |
| Workflow | Azure Functions / Durable Functions | Long-running processes, document workflows, escalations |
| Database | Azure SQL | Tenants, claims, customers, workflows, audit metadata |
| Local dev persistence | SQLite or mock persistence where appropriate | Developer mode only |
| Storage | Azure Blob Storage | Claim documents, evidence files, exports |
| Messaging | Azure Service Bus / Event Grid | Async processing, retries, notifications |
| Search / RAG | Azure AI Search | Policy, SOP, claim, and document retrieval |
| AI | Azure OpenAI through provider interfaces | Summaries, explanations, drafting, classification support |
| Document extraction | Azure AI Document Intelligence | OCR, document classification, extraction |
| Identity | Microsoft Entra ID | Staff authentication and RBAC |
| API governance | Azure API Management | Secure external/internal APIs |
| Secrets | Azure Key Vault | Secrets, certificates, connection strings |
| Observability | Azure Monitor, Application Insights, Log Analytics | Monitoring, tracing, performance, errors |
| Security operations | Microsoft Sentinel / Defender for Cloud | SIEM and security posture |
| IaC | Bicep / Terraform | Repeatable Azure deployment |
| CI/CD | GitHub Actions / Azure DevOps | Build, test, deploy |

### 12.2 Required V1 Backend Endpoints

The current implementation direction expects at least:

```text
GET /api/v1/channels/overview
GET /api/v1/customers/overview
GET /api/v1/sales-leads/overview
```

Additional likely endpoints for MVP include:

```text
GET /api/v1/command-center/summary
GET /api/v1/claims
POST /api/v1/claims
GET /api/v1/claims/{claimId}
POST /api/v1/claims/{claimId}/documents
GET /api/v1/claims/{claimId}/documents
POST /api/v1/documents/{documentId}/classify
POST /api/v1/documents/{documentId}/extract
POST /api/v1/claims/{claimId}/summary
GET /api/v1/audit/events
GET /api/v1/admin/provider-health
```

### 12.3 Production Data Rule

No fake endpoints, mock data, or simulated production responses should be used in staging or production. Empty API results should render empty states; failed API calls should render error states.

---

## 13. Data and Canonical Model

### 13.1 Core Entities

IIP should normalize incoming data into a canonical model rather than allowing every insurer integration to create custom chaos.

Initial canonical entities:

```text
Tenant
Workspace
User
Role
Customer
Policy
Claim
Document
Interaction
Task
WorkflowCase
ConsentRecord
AuditEvent
IntegrationEvent
WriteBackRequest
Lead
ChannelConversation
KnowledgeSource
AIRun
ApprovalTask
```

### 13.2 Core Tables / Collections

Likely database structures include:

```text
Tenants
Workspaces
Users
Roles
Customers
Policies
Claims
Documents
Interactions
Tasks
WorkflowCases
ConsentRecords
AuditEvents
IntegrationEvents
WriteBackRequests
ConnectorConfigs
FieldMappings
SalesLeads
ChannelConversations
KnowledgeSources
AIRuns
ApprovalTasks
```

### 13.3 Tenant and Workspace Model

Every case, document, communication, integration event, search item, audit event, and usage record must carry tenant and workspace identifiers.

Potential workspace dimensions:

- Business unit
- Branch
- Region
- Product line
- Brand
- Broker network
- Country, where relevant

---

## 14. Integrations Strategy

### 14.1 MVP Integrations

| Integration | Purpose | Direction | MVP Priority |
|---|---|---|---|
| WhatsApp Business API | Customer claims initiation, acquisition, missing-document requests | Two-way | Critical |
| Microsoft Graph / Outlook / Exchange | Email ingestion, shared mailbox, customer communication | Read/write depending on approval | High |
| Azure Blob Storage | Claim documents and evidence | Read/write | Critical |
| Azure SQL | Claims, customers, policies, tasks, audit events | Read/write | Critical |
| Azure AI Document Intelligence | OCR, classification, extraction | Processing service | Critical |
| Azure AI Search | Policy/document retrieval and RAG | Read/query | Critical |

### 14.2 Phase 2 Integrations

| Integration | Purpose |
|---|---|
| Core policy administration system | Policy validation and customer/policy lookup |
| Claims management system | Claim sync or export |
| Power BI / Microsoft Fabric | Analytics and executive reporting |
| SFTP / secure file exchange | Batch ingestion and fallback |
| SMS gateway | Fallback notifications |
| SharePoint / OneDrive | Knowledge and document import/export |
| Teams | Alerts and internal notifications |

### 14.3 Later Integrations

| Integration | Purpose |
|---|---|
| Mobile money | Premium/payout-related workflows, if approved |
| National ID systems | Identity verification, subject to legal approval |
| Vehicle registry | Motor ownership/vehicle verification |
| Medical provider systems | Health claims, provider invoice review |
| IRA/regulatory systems | Claims/complaints/regulatory exports |
| Core banking | Bancassurance integrations |

### 14.4 Integration Principle

The MVP should avoid deep custom integration into every insurer core system. Instead, it should support:

- API ingestion where available
- Microsoft Graph ingestion
- Secure file upload
- CSV/Excel import
- SFTP or batch imports
- Read-only policy lookup
- Controlled exports
- Human-approved write-back

---

## 15. Deployment Model

### 15.1 Recommended Direction

The preferred enterprise direction is:

> **Azure Managed Application / private Azure deployment for serious insurer pilots, with a possible Otic-hosted SaaS path for faster demos or smaller clients.**

### 15.2 Deployment Options

| Option | Best For | Notes |
|---|---|---|
| Otic-hosted SaaS | Fast demos, small insurers, brokers, lower-cost pilots | Faster onboarding but stronger tenant isolation controls needed |
| Azure Managed Application / customer tenant deployment | Large insurers, banks, HMOs, regulated institutions | Better customer trust and data control |
| Hybrid control plane / data plane | Long-term scalable model | Otic controls billing/support/versioning; insurer data plane holds sensitive data |
| Cloud Mirror / resilient buffer | Connectivity-sensitive channels | Useful for WhatsApp/webhook resilience |
| Full offline/local deployment | Future variant | Not MVP unless explicitly funded |

### 15.3 Hybrid Cloud Mirror / Resilient Channel Buffer

The platform should address client concerns around internet outages, especially for WhatsApp/customer channels. The MVP should frame this as resilient channel buffering, not full offline platform capability.

Recommended behavior:

- Webhook events are acknowledged quickly
- Messages are queued safely
- Failed events retry with exponential backoff
- Local or edge buffering may be used where required
- Full offline claims processing remains a future phase

### 15.4 Environment Strategy

| Environment | Purpose |
|---|---|
| Local Dev | Developer productivity, mock providers allowed only locally |
| Dev | Shared development validation |
| Staging | Production-like validation, no fake endpoints |
| Pilot Production | Customer pilot deployment |
| Enterprise Production | Customer tenant / Managed Application deployment |

---

## 16. Security, Privacy, Compliance, and Governance

### 16.1 Security Principles

IIP will handle insurance data, claims evidence, customer communications, policy records, underwriting information, broker records, and operational records. Security must be embedded in product experience, backend architecture, agent design, workflows, logs, and admin configuration.

Required principles:

- Microsoft Entra ID authentication
- MFA-ready staff access
- Role-based access control
- Attribute-aware access where needed
- Server-side authorization for every sensitive endpoint
- Tenant and workspace isolation
- Least privilege by default
- Audit logging for all sensitive actions
- Secrets in Key Vault
- Encryption in transit and at rest
- No AI access to unrestricted production databases
- No generic `query_database(sql)` tool

### 16.2 Compliance Requirements

| Requirement | Product Implication | MVP Critical |
|---|---|---|
| Human ownership of regulated decisions | Approval workflows and clear decision ownership | Yes |
| Claims audit trail | Every AI and human action must be logged | Yes |
| Claims SLA tracking | SLA risk and breach indicators | Yes |
| Claims register support | Claims data should be exportable in regulator-ready formats | Yes |
| Consent capture | Customer data processing must be purpose-aware and logged | Yes |
| Data minimization | Collect only required information | Yes |
| Role-based access | Users see only permitted records/actions | Yes |
| Tenant isolation | No cross-insurer data exposure | Yes |
| Source grounding | Policy answers cite approved documents | Yes |
| Cross-border processing review | Legal review required for region/model processing | Yes |
| Retention policy | Configurable retention and deletion rules | Phase 2 |
| Data subject requests | Access/correction/deletion workflow | Phase 2 |
| Complaint handling | Complaint workflows and audit trail | Phase 2 unless pilot requires |

### 16.3 Legal Review Required

The following require legal or compliance confirmation:

- Whether IRA approval or sandbox participation is required
- Whether processing insurance PII in South Africa North or West Europe is acceptable
- Whether Azure OpenAI processing counts as cross-border processing
- Required consent language for WhatsApp claims initiation
- Required claims register format
- Complaint escalation and retention requirements
- AI explainability requirements for insurer governance

---

## 17. Pricing and Packaging

### 17.1 Recommended Pricing Structure

Use simple modular pricing first.

| Component | Description |
|---|---|
| Base platform fee | Command Center, authentication, audit trail, admin settings, core workspace |
| Seat bundle | Defined number of insurer staff seats |
| Claims Processing module | Claims intake, work queue, document review, summaries, audit trail |
| Customer Acquisition module | Sales & Leads, Channels, WhatsApp lead capture, product guidance, routing |
| Fraud Signal module | Advisory fraud/inconsistency detection |
| Early Warning module | Retention/churn/SLA/customer-risk intelligence |
| Underwriting Support module | Submission parsing, risk summaries, missing information, underwriter notes |
| Usage charges | Documents processed, AI tokens, WhatsApp conversations, storage |
| Implementation fee | Onboarding, configuration, templates, training, integration setup |
| Enterprise private deployment premium | Customer-tenant / Managed Application deployments |

### 17.2 Pilot Pricing Direction

For the first pilot, avoid overly complex pricing:

```text
Pilot fee
+ implementation/onboarding fee
+ included claims/documents volume
+ pass-through or capped AI/document/WhatsApp usage
```

### 17.3 Charge Separately

- Fraud Signal module
- Early Warning/Retention module
- Underwriting Support module
- Custom integrations
- Private deployment
- Premium support
- Advanced analytics/Power BI/Fabric
- Voice/call agent
- Country-specific regulatory automation

---

## 18. Roadmap

### Phase 0 - Consolidation and Readiness

- Confirm product name as Insurance Intelligence Platform (IIP)
- Finalize master product context
- Produce PRD, FSD, TSD, and roadmap
- Lock first MVP workflow
- Confirm first pilot customer and real systems
- Confirm deployment model
- Confirm legal review requirements

### Phase 1 - MVP Foundation

- Tenant setup
- Entra ID login
- RBAC
- Command Center
- Channels Inbox
- Sales & Leads Inbox
- Customer 360
- Claims Inbox
- FNOL
- Document upload
- Document classification/extraction
- Missing-document checklist
- Claim summary
- Human review
- Audit trail
- Export

### Phase 2 - Product Expansion

- Fraud Signal module
- Customer Acquisition module
- Underwriting Support module
- Early Warning/Retention module
- Policy servicing
- Power BI/Fabric reporting
- SFTP/file integrations
- Claims/policy system integrations

### Phase 3 - Enterprise and Marketplace

- Azure Managed Application package
- Private deployment pattern
- Marketplace readiness
- Advanced security posture
- Customer-managed keys
- Private endpoints
- Regulatory reporting exports
- Advanced analytics

### Phase 4 - Advanced Intelligence

- Voice/call agent
- Advanced multilingual support
- Reinsurance workflows
- Agriculture/microinsurance workflows
- More insurance product lines
- Cross-country regulatory configuration

---

## 19. Contradictions Register

| ID | Topic | Conflict | Resolution |
|---|---|---|---|
| C1 | Product name | ACIP vs IIP | Use Insurance Intelligence Platform (IIP); ACIP is superseded |
| C2 | Target user | Brokers/TPAs vs insurers | First pilot targets an insurance company; brokers/TPAs remain adjacent/future users |
| C3 | MVP scope | Claims-only vs broad five-module platform | MVP = Motor Claims and Document Intelligence; roadmap includes acquisition, fraud, early warning, underwriting |
| C4 | AI autonomy | Some language suggests automated decisions | Strict human-in-the-loop rule across all documents |
| C5 | Deployment | SaaS vs Managed Application | Enterprise insurer pilot should prefer Managed Application/private Azure path; SaaS remains lighter option |
| C6 | Offline mode | Full offline vs resilient channel buffer | MVP should document resilient queue/buffer, not full offline platform |
| C7 | Underwriting timing | Client asks mention underwriting | Basic proposal parsing may be considered; full Underwriting Copilot is Phase 2 |
| C8 | Early warning timing | Mentioned as important module | Phase 2 commercial module, not core claims MVP |
| C9 | Customer portal | Some docs mention client portal | Internal app is not customer-facing in V1; external flows use WhatsApp/web channels |
| C10 | Current UI | Standard dashboard vs chat-first architecture | Move toward three-panel command-center architecture |

---

## 20. Missing Information Register

| ID | Missing Item | Recommended Answer | Confidence | Who Should Confirm | Priority |
|---|---|---|---|---|---|
| M1 | Official product name | Insurance Intelligence Platform (IIP) | High | Leadership | Critical |
| M2 | First pilot customer | Mid-to-large insurance company | High | Sales/Leadership | Critical |
| M3 | MVP claim type | Motor claim | High | Product/Claims SME | Critical |
| M4 | Deployment model | Azure Managed Application/private deployment for enterprise pilot; SaaS option for fast demos | Medium-High | Cloud Architect/Leadership | High |
| M5 | WhatsApp provider | Meta direct, Twilio, Infobip, or other provider not yet confirmed | Unknown | Engineering | High |
| M6 | Azure region | South Africa North preferred; West Europe fallback | Medium | Engineering/Legal | High |
| M7 | Pricing | Modular pricing with base + modules + usage | Medium | Sales/Finance | High |
| M8 | Legal/data residency | Needs formal legal review | Medium | Legal/Compliance | High |
| M9 | Real insurer systems | Policy/claims system names not yet confirmed | Low | Pilot customer/Engineering | High |
| M10 | Current codebase state | Partially known: Channels, Customers, Sales & Leads implementation direction exists | Medium | Engineering | Medium |
| M11 | Document samples | Need synthetic/redacted motor claim packs | High | Product/Engineering | High |
| M12 | Marketplace timeline | Not confirmed whether needed for first pilot | Medium | Leadership/Cloud Architect | Medium |

---

## 21. Immediate Next Actions

| # | Action | Owner |
|---|---|---|
| 1 | Confirm Insurance Intelligence Platform (IIP) as the official name across all docs and repos | Leadership |
| 2 | Add naming note: Earlier drafts referred to ACIP; current official name is IIP | Product |
| 3 | Update PRD, FSD, TSD, README, UI labels, and implementation prompts to use IIP | Product/Engineering |
| 4 | Lock Motor Claims as the MVP workflow | Product/Claims SME |
| 5 | Create the Motor Claims document checklist and field extraction matrix | Product/Engineering |
| 6 | Confirm first pilot customer and their actual systems | Sales/Leadership |
| 7 | Confirm WhatsApp provider: Meta direct, Twilio, Infobip, or other | Engineering |
| 8 | Confirm Azure region/service availability and quotas | Cloud Architect |
| 9 | Draft legal review brief for Uganda data/privacy/IRA concerns | Legal/Compliance |
| 10 | Convert Channels, Customers, and Sales & Leads into formal FSD modules | Product |
| 11 | Draft the Hybrid Cloud Mirror / resilient channel buffer architecture | Architecture |
| 12 | Prepare the PRD using this canonical direction | Product |
| 13 | Prepare the FSD using this canonical direction | Product/Engineering |
| 14 | Prepare the TSD / Architecture Document using this canonical direction | Architecture |

---

## 22. PRD/FSD/TSD Handoff Guidance

This master context should now be used to generate the following formal documents:

1. **PRD - Product Requirements Document**
   - Product vision
   - Users and personas
   - MVP scope
   - Business objectives
   - Success metrics
   - Roadmap
   - Risks and assumptions

2. **FSD - Functional Specification Document**
   - Modules
   - Screens
   - User roles
   - Functional requirements
   - Workflows
   - Business rules
   - Acceptance criteria

3. **TSD - Technical Specification Document**
   - Azure architecture
   - Services
   - Data model
   - APIs
   - Integrations
   - Security
   - Observability
   - Deployment model

4. **AI Governance and Human Review Policy**
   - AI boundaries
   - Approval rules
   - Prompt/source logging
   - Evidence and confidence requirements
   - Prohibited AI actions

5. **UI/UX Implementation Handoff**
   - Three-panel architecture
   - Screens
   - Components
   - Empty/error/loading states
   - Role-aware UI behavior

---

## 23. Final Canonical Statement

> **Insurance Intelligence Platform (IIP)** is an Azure-native, human-reviewed insurance operations platform that helps insurers acquire customers, process claims, review documents, support underwriting, detect risk signals, manage customer interactions, and maintain auditable insurance workflows through AI-assisted, source-grounded, role-aware workspaces. IIP does not replace insurers' core systems or human decision-makers. It prepares the work, links evidence, recommends next actions, and keeps the process auditable while humans remain accountable for regulated insurance decisions.

