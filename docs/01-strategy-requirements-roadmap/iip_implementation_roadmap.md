# Insurance Intelligence Platform (IIP) - Implementation Roadmap

> **⚠️ For execution ORDER and current status, defer to [`../08-program-plan/IIP-CONSOLIDATED-ROADMAP.md`](../08-program-plan/IIP-CONSOLIDATED-ROADMAP.md)** — the single authoritative sequence (2026-07-07). This document remains the source for strategic scope, requirements, and phase *detail*; the consolidated roadmap governs *what order to build/deploy in* and *what is already done*.

**Document type:** Implementation Roadmap / Delivery Plan  
**Product name:** Insurance Intelligence Platform  
**Acronym:** IIP  
**Prepared for:** Otic Group / IIP Product, Engineering, Cloud, Security, QA, Delivery, and Leadership Teams  
**Date:** 30 June 2026  
**Version:** 1.0 - Consolidated MVP Implementation Roadmap  
**Status:** Working implementation baseline for MVP delivery and pilot readiness  
**Classification:** Internal - Product, Engineering, Cloud, Security, QA, Delivery, Support, and Leadership Reference

---

## Document Control

| Field | Value |
|---|---|
| Owner | Product / Engineering / Delivery Lead |
| Primary audience | Product, engineering, UI/UX, AI, cloud, security, QA, delivery, leadership, implementation agents |
| Source baseline | IIP Master Product Context, PRD, FSD, Technical Architecture, System Integration Strategy, UI/UX Handoff, API Specification, Data Model, Security and Compliance Document, QA/Test Plan, Deployment Plan, User/Admin Guide, chat decisions, Antigravity/codebase findings |
| Current product name | **Insurance Intelligence Platform (IIP)** |
| Superseded name | ACIP / Agentic Insurance Intelligence Platform |
| MVP focus | Motor Claims and Document Intelligence for an insurance company pilot |
| Product posture | Azure-first, human-reviewed, evidence-linked, audit-ready, tenant-aware, insurance-operations-focused |
| AI decision rule | AI assists. Humans make regulated insurance decisions. |

---

## 1. Executive Summary

The **Insurance Intelligence Platform (IIP)** implementation roadmap defines the practical build sequence for taking IIP from consolidated product documentation into a usable pilot-ready insurance operations platform.

IIP is an Azure-native, human-reviewed insurance operations platform for insurers, brokers, TPAs, HMOs, bancassurance partners, and microinsurance providers. It helps insurance institutions acquire customers, process claims, review documents, support underwriting, detect risk signals, manage customer interactions, and maintain auditable insurance workflows through AI-assisted, source-grounded, role-aware workspaces.

The agreed first MVP wedge is:

> **Motor Claims and Document Intelligence for a mid-to-large insurance company pilot.**

The implementation must focus on building a credible, secure, demonstrable, and extensible platform - not trying to build every future module at once. The MVP must prove that IIP can capture claim inputs, process documents, prepare structured evidence, support claims officers, generate missing-document checklists, draft customer-safe updates, preserve auditability, and keep final insurance decisions under human control.

The recommended delivery approach is:

```text
Documentation baseline and decision lock
↓
MVP foundation and secure app shell
↓
Core operational workspaces
↓
Claims and document intelligence workflow
↓
AI, RAG, and human review controls
↓
Integrations and channel resilience
↓
Security, compliance, QA, and deployment hardening
↓
Pilot launch and controlled learning
↓
Phase 2 commercial modules
```

This roadmap is designed for an implementation AI agent such as Antigravity/Codex as well as human engineers. It breaks the work into phases, workstreams, epics, dependencies, release gates, and risks.

---

## 2. Roadmap Principles

IIP implementation must follow these principles across every phase.

| Principle | Implementation Meaning |
|---|---|
| **IIP name is canonical** | Use Insurance Intelligence Platform and IIP across UI, docs, routes, prompts, READMEs, and handoffs. Treat ACIP as superseded. |
| **Claims-first MVP** | Build Motor Claims and Document Intelligence first. Other modules must not derail the MVP. |
| **Human-in-the-loop is mandatory** | AI must not approve claims, reject claims, bind policies, set premiums, accuse fraud, or release payments. |
| **No mock production** | Local mode may use mocked providers. Staging and production must use real provider paths or explicit not-configured states. |
| **Azure-first** | Use Microsoft Entra ID, Azure SQL, Blob Storage, AI Search, Document Intelligence, OpenAI, Key Vault, App Insights, Service Bus/Functions where applicable. |
| **Tenant/workspace-aware by default** | Every data record, API request, search query, document, audit event, and AI run must be tenant/workspace scoped. |
| **Evidence-linked AI** | AI answers and summaries must cite retrieved evidence or mark uncertainty. |
| **Operational UI first** | The UI must feel like an insurance command center, not a generic chatbot. |
| **Integration-safe architecture** | Start with Graph/email, WhatsApp, uploads, exports, and read-only core policy lookup before deep system write-back. |
| **Compliance by design** | Audit, consent, retention, RBAC, and approval boundaries must be built into workflows from the beginning. |

---

## 3. Current Baseline

The documentation pack now defines the core product, functional, technical, security, testing, deployment, and usage expectations.

| Existing Artifact | Roadmap Use |
|---|---|
| Master Product Context | Source-of-truth baseline and naming/MVP decisions |
| PRD | Product goals, users, requirements, success metrics, exclusions |
| FSD | Module behavior, screens, roles, workflows, states, business rules |
| Technical Architecture Document | Azure services, component architecture, data flows, security patterns |
| System Integration Strategy | Integration priorities, connectors, fallback paths, channel strategy |
| UI/UX Handoff | Three-panel architecture, navigation, screens, states, implementation guidance |
| API Specification | Endpoint conventions, tenant headers, models, error behavior, audit rules |
| Data Model Document | Canonical entities, relationships, tenant/workspace identifiers, tables |
| Security and Compliance Document | Identity, tenant isolation, privacy, Uganda compliance, audit, AI controls |
| QA/Test Plan | Functional, AI, document, integration, security, UI, and release testing |
| Deployment Plan | Environment strategy, CI/CD, Azure provisioning, release gates, rollback |
| User/Admin Guide | Operational usage and administration behavior |

### 3.1 Canonical MVP Scope

The MVP should include:

- Microsoft Entra ID staff authentication.
- Tenant/workspace context.
- Role-aware app shell.
- Command Center.
- Channels Inbox.
- Sales & Leads Inbox.
- Customer 360 internal workspace.
- Claims Inbox / Work Queue.
- FNOL / New Claim Intake.
- Case Review.
- Document upload and evidence storage.
- Azure AI Document Intelligence adapter.
- Document classification and extraction review.
- Missing-document checklist.
- AI claim summary.
- Advisory fraud signals.
- Policy/knowledge retrieval over approved sources.
- Human review and approval queues.
- Customer update drafting.
- Audit logs.
- Basic reports and operational metrics.
- Admin settings for roles, workflows, integrations, providers, and health.
- Local developer mode with mocked provider adapters.
- Staging/production with real providers or explicit not-configured states.

### 3.2 Explicitly Out of Scope for MVP

The MVP must not attempt to deliver:

- Autonomous claim approval or rejection.
- Automatic settlement or payment release.
- Final underwriting decisioning.
- Full underwriting pricing engine.
- Automatic policy binding.
- Direct production database access by AI agents.
- Full regulatory automation.
- Full voice/call-center agent.
- Full mobile customer app.
- Full marketplace billing automation.
- Full country-by-country regulatory automation.
- All insurance product lines.
- Full offline platform operation.

---

## 4. Roadmap Overview

The roadmap is organized into delivery phases. Timelines are indicative and should be adjusted based on team size, codebase state, customer dependencies, and Azure readiness.

| Phase | Name | Target Outcome | Indicative Timing |
|---|---|---|---|
| 0 | Decision Lock and Delivery Setup | Align naming, scope, backlog, repos, environments, and delivery rules | Week 0-1 |
| 1 | Platform Foundation | Secure tenant-aware app foundation, auth, data model, API conventions, audit base | Weeks 1-2 |
| 2 | Core Workspaces and Shell | Build/refine Command Center, Channels, Sales & Leads, Customers, Claims shell | Weeks 2-4 |
| 3 | Claims Workflow MVP | Build Motor Claims FNOL, Work Queue, Case Review, statuses, tasks, approvals | Weeks 4-6 |
| 4 | Document Intelligence and AI Review | Build upload, storage, OCR/extraction, checklists, summaries, source grounding | Weeks 5-7 |
| 5 | Integrations and Resilience | WhatsApp, Microsoft Graph/email, exports, provider health, resilient queues | Weeks 6-8 |
| 6 | Security, Compliance, and Observability | RBAC, audit, logging, monitoring, compliance views, deployment readiness | Weeks 7-9 |
| 7 | QA, UAT, and Pilot Readiness | Test completion, demo data, training, go/no-go gates, release candidate | Weeks 9-10 |
| 8 | Pilot Launch and Stabilization | Controlled insurer pilot, live feedback, support, metrics, refinement | Weeks 11-14 |
| 9 | Phase 2 Expansion | Underwriting, early warning, fraud module, analytics, Marketplace path | Post-pilot |

---

## 5. Phase 0 - Decision Lock and Delivery Setup

### 5.1 Objective

Lock the implementation baseline so engineering agents and human teams do not build against conflicting assumptions.

### 5.2 Key Decisions to Lock

| Decision | Canonical Answer | Owner |
|---|---|---|
| Product name | Insurance Intelligence Platform (IIP) | Leadership/Product |
| Superseded name | ACIP is historical only | Product |
| MVP wedge | Motor Claims and Document Intelligence | Product/Claims SME |
| First buyer | Mid-to-large insurance company | Sales/Leadership |
| First deployment path | Azure-first; enterprise private/Managed Application path preferred, SaaS option for fast pilot | Cloud/Leadership |
| AI autonomy boundary | AI assists; humans decide | Product/Security/Compliance |
| Customer access | Customers do not log into internal app in V1; customer interactions flow through channels | Product/UX |
| Core UI pattern | Three-panel command center with left nav, main workspace, right Copilot | UX/Frontend |

### 5.3 Phase 0 Epics

| Epic | Description | Exit Criteria |
|---|---|---|
| Naming cleanup | Replace ACIP references in active docs, prompts, and UI-facing material | Active docs use Insurance Intelligence Platform (IIP) |
| Backlog creation | Convert PRD/FSD/API/Data Model into implementation epics and tasks | Backlog is organized by phase and workstream |
| Repo guardrails | Update AGENTS.md/CODEX.md/README rules for agents | Coding agents have clear rules |
| Environment plan | Confirm local/dev/staging/pilot production strategy | Environment matrix approved |
| Pilot assumptions | Record pilot customer assumptions and unresolved items | Open questions register exists |

### 5.4 Deliverables

- Updated README/AGENTS.md naming and architecture guardrails.
- MVP implementation backlog.
- Phase-based delivery board.
- Decision register.
- Open questions register.
- Pilot assumptions list.

---

## 6. Phase 1 - Platform Foundation

### 6.1 Objective

Build or stabilize the secure technical foundation needed for every IIP module.

### 6.2 Workstreams

| Workstream | Deliverables |
|---|---|
| Frontend foundation | Next.js app shell, layout system, routes, error/loading/empty states, feature flags |
| Backend foundation | .NET 8 API structure, controllers, services, DTOs, validation, error handling |
| Identity | Microsoft Entra ID/MSAL integration, role claims, session handling |
| Tenant/workspace | Server-side tenant/workspace enforcement and request context handling |
| Data | Azure SQL schema foundation and EF Core migrations |
| Audit | AuditEvent base pipeline for user, system, AI, document, and integration actions |
| Configuration | Provider mode, environment variables, Key Vault alignment, Not Configured states |
| Observability | Health endpoints, Application Insights, correlation IDs, structured logs |

### 6.3 Required API Foundations

The API must support:

- Health checks: `/health`, `/health/live`, `/health/ready`.
- Provider health: `/api/admin/provider-health` or versioned equivalent.
- Versioned endpoints under `/api/v1` where practical.
- Tenant/workspace request context.
- Correlation ID per request.
- Standard error response format.
- Pagination for lists.
- Server-side authorization.
- Audit events for sensitive operations.

### 6.4 Tenant and Header Requirements

Development/demo requests may include tenant and workspace headers, but production cannot trust browser-owned permissions. Server-side authorization must remain authoritative.

| Context Item | Purpose |
|---|---|
| Tenant ID | Isolates insurer data |
| Workspace ID | Isolates branch/product/unit/context |
| User ID | Links actions to staff identity |
| Role claims | Controls allowed actions |
| Correlation ID | Traces request and workflow execution |

### 6.5 Phase 1 Exit Criteria

- User can log into the staff app.
- Backend can enforce tenant/workspace context.
- Health and provider status are visible.
- Database migrations run cleanly.
- Audit events can be recorded.
- Local development can run with mocked providers.
- Staging does not rely on fake endpoints unless explicitly flagged as not configured.

---

## 7. Phase 2 - Core Workspaces and Shell

### 7.1 Objective

Deliver the core operational workspaces that make IIP feel like an insurance command center.

### 7.2 Target UI Structure

The app must use a consistent three-panel enterprise layout:

```text
Top Bar: workspace, environment, global search, alerts, user/role
Left Navigation: Command Center, Channels, Sales & Leads, Customers, Claims, Documents, Knowledge, Approvals, Audit, Reports, Integrations, Admin, Support
Main Workspace: chat-first workflow or operational content area
Right Copilot Panel: context, next actions, evidence, risk, confidence, suggestions
Status Area: connection, sync, active agents, queues, provider health
```

### 7.3 Core Workspace Build Order

| Order | Workspace | Purpose |
|---|---|---|
| 1 | Command Center | Role-aware operational overview and AI-first landing workspace |
| 2 | Channels | Unified internal inbox for WhatsApp, webchat, email, SMS, broker, and call-center interactions |
| 3 | Sales & Leads | Customer acquisition work fed by external channels |
| 4 | Customers | Internal Customer 360 for policies, claims, interactions, consent, documents, opportunities |
| 5 | Claims | Claims work queue, FNOL, active claims, case review |
| 6 | Documents | Upload, extraction review, validation, classification, source evidence |
| 7 | Approvals | Human review queue for consequential actions |
| 8 | Audit and Compliance | Audit logs, AI actions, human overrides, exports |
| 9 | Admin Settings | Tenant, roles, workflows, providers, integrations, templates |
| 10 | Support and Health | Provider health, queue backlog, sync failures, deployment status |

### 7.4 Antigravity/Codebase-Aware Modules

The codebase-aware findings define these V1 internal workspaces:

| Module | Required Endpoint | Notes |
|---|---|---|
| Channels | `GET /api/v1/channels/overview` | Internal unified inbox; customers do not log into the app |
| Customers | `GET /api/v1/customers/overview` | Internal Customer 360; includes identity, policies, claims, interactions, consent |
| Sales & Leads | `GET /api/v1/sales-leads/overview` | Internal lead inbox fed by WhatsApp, broker, email, call center, and web sources |

### 7.5 Phase 2 Exit Criteria

- Main navigation is aligned to IIP modules.
- Command Center shows role-aware metrics and next actions.
- Channels, Customers, and Sales & Leads render real API states.
- Loading, empty, error, unauthorized, and Not Configured states exist.
- Copilot panel is context-aware even if AI functionality is initially limited.
- Role-aware navigation exists without relying on frontend-only authorization.

---

## 8. Phase 3 - Claims Workflow MVP

### 8.1 Objective

Build the complete Motor Claims operational workflow from FNOL to human-reviewed case pack.

### 8.2 Core Claims Screens

| Screen | Description |
|---|---|
| Claims Inbox / Work Queue | Dense list of claims with status, SLA, completeness, risk, assignment, next action |
| FNOL / New Claim Intake | Guided claim creation from customer/channel/broker/staff input |
| Case Review | Central claim workspace with summary, documents, policy details, timeline, tasks, decisions, audit |
| Document Checker | Document list, extraction review, confidence, missing items, validation issues |
| Human Review / Approvals | Human-owned approval or escalation for sensitive actions |
| Customer Update Draft | AI-assisted customer-safe response draft requiring human approval where sensitive |

### 8.3 Motor Claim Status Model

| Status | Meaning |
|---|---|
| New | Claim has been created but not yet reviewed |
| Intake Started | FNOL is being captured |
| Documents Pending | Required documents are missing |
| Documents Uploaded | Customer/staff submitted documents |
| AI Processing | Classification, extraction, summary, or checklist running |
| Needs Review | AI-prepared case is ready for human review |
| Missing Information | Required details/documents remain incomplete |
| Fraud Signal Detected | Advisory signal requires human investigation |
| Escalated | Case requires supervisor/specialist review |
| Approved for Next Step by Human | Human has approved movement to next workflow step |
| Returned to Customer | Customer-safe request/update sent |
| Exported | Reviewed case pack exported or sent downstream |
| Closed | Workflow is complete |

### 8.4 Claims Workflow Epics

| Epic | Description | Exit Criteria |
|---|---|---|
| FNOL capture | Capture policy/customer/incident/channel data | Claims can be created with required fields |
| Work queue | List, filter, search, assign, escalate claims | Officers can manage workload |
| Case review | View claim details, documents, timeline, tasks, audit | Case file is operationally usable |
| SLA tracking | Track acknowledgement, review, assessment, and escalation timing | SLA risk/breach indicators visible |
| Missing documents | Generate and manage required document checklist | Missing items can be requested |
| Human approvals | Route sensitive actions to supervisor/reviewer | Consequential actions require approval |
| Customer updates | Draft customer-safe updates and document requests | Messages are human-reviewed as needed |
| Export | Generate claim/evidence pack | Reviewed claim can be exported |

### 8.5 Phase 3 Exit Criteria

- Staff can create and manage a Motor Claim.
- Claims can be assigned and escalated.
- Missing documents can be identified and requested.
- Case Review is the central operational workspace.
- No AI output can finalize a claim outcome.
- All sensitive actions create audit events.

---

## 9. Phase 4 - Document Intelligence and AI Review

### 9.1 Objective

Build the intelligent document workflow that proves the MVP value.

### 9.2 Document Types for MVP

| Document Type | MVP Role |
|---|---|
| Claim form | FNOL and claimant data extraction |
| Police report | Accident date, location, parties, officer, report reference |
| National ID | Identity extraction and matching |
| Driving permit | Driver eligibility and expiry validation |
| Vehicle photos | Damage evidence and quality indicators |
| Repair quotation | Garage details, costs, line items, total amount |
| Policy schedule | Coverage, policy number, limits, insured party |
| Assessor report | Assessment summary and repair/loss estimate |

### 9.3 Document Intelligence Epics

| Epic | Description | Exit Criteria |
|---|---|---|
| Upload and storage | Upload documents into tenant-scoped Blob Storage | Original files stored securely |
| Classification | Identify document type | Document types assigned with confidence |
| OCR/extraction | Extract fields using Azure AI Document Intelligence | Extracted fields visible for review |
| Confidence scoring | Show confidence at field/document level | Low-confidence fields routed to review |
| Validation rules | Compare dates, names, IDs, policy numbers, amounts | Mismatches flagged |
| Missing document checklist | Compare claim type against required documents | Required/missing list generated |
| Review interface | Human can approve, edit, reject extracted fields | Human correction captured |
| Evidence linking | Link extracted fields to source documents/pages | Evidence trace visible |

### 9.4 AI Agent Epics

| Agent/Capability | MVP Role | Guardrail |
|---|---|---|
| Claims Summary Agent | Summarizes claim, parties, documents, missing items, key dates, next steps | Must not approve/reject/settle |
| Missing Information Agent | Checks requirements against submitted data | Must not close claim automatically |
| Fraud Signal Agent | Flags duplicate, inconsistent, suspicious patterns | Must not accuse customer of fraud |
| Policy Intelligence Agent | Retrieves policy clauses and explains coverage context | Must not make binding legal interpretation |
| Customer Update Agent | Drafts customer-safe updates | Sensitive messages require human approval |
| Orchestrator | Routes tasks and preserves workflow context | Must not bypass permissions or approval gates |

### 9.5 Phase 4 Exit Criteria

- Documents can be uploaded, classified, extracted, reviewed, and audited.
- AI summaries include uncertainty and evidence where available.
- Low-confidence outputs route to human review.
- Fraud signals are advisory only.
- Policy answers are source-grounded and tenant-scoped.
- Prompt/tool/model metadata is logged for AI runs.

---

## 10. Phase 5 - Integrations and Resilience

### 10.1 Objective

Connect IIP to priority channels and enterprise systems without over-integrating too early.

### 10.2 MVP Integration Order

| Order | Integration | MVP Goal |
|---|---|---|
| 1 | File upload / Blob Storage | Enable document evidence workflow |
| 2 | Microsoft Graph / Outlook | Ingest claim emails and attachments from shared mailbox |
| 3 | WhatsApp Business API | Enable customer FNOL, document requests, missing-document follow-up |
| 4 | Export API / PDF/CSV/JSON | Export reviewed claim pack or structured data |
| 5 | Read-only policy lookup | Validate policy/customer context where customer systems are available |
| 6 | Provider health | Show integration health and Not Configured states |

### 10.3 Hybrid Channel Resilience

Client concerns around outages should be addressed through a **resilient channel buffer**, not an overstated full offline platform.

Required behavior:

- Webhooks should acknowledge quickly where safe.
- Incoming events should be queued durably.
- Failed events should retry with exponential backoff.
- Provider downtime should show degraded/not-configured health.
- No customer message should be silently lost.
- Local/offline edge buffering may be future/private-deployment scope.

### 10.4 Phase 5 Exit Criteria

- Microsoft Graph/email ingestion works in configured environment.
- WhatsApp provider decision is made or Not Configured state is implemented.
- Documents and messages create traceable IntegrationEvent records.
- Export works for reviewed claim packs.
- Provider health is visible in Support & Health/Admin.
- Integration failures are observable and retryable.

---

## 11. Phase 6 - Security, Compliance, and Observability

### 11.1 Objective

Harden the system for pilot use with insurance data.

### 11.2 Security Epics

| Epic | Description | Exit Criteria |
|---|---|---|
| Entra ID authentication | Staff login and token validation | Unauthorized users blocked |
| RBAC | Role-based actions and navigation | Users only see permitted actions |
| Server-side authorization | Backend enforces access | No frontend-only security |
| Tenant isolation | Query/data enforcement by tenant/workspace | Cross-tenant access blocked |
| Key Vault | Secrets managed securely | No secrets in repo/env leaks |
| PII protection | Masking, minimization, audit-sensitive access | Sensitive data access controlled |
| Audit trail | User/system/AI/document/integration actions logged | Logs complete and exportable |
| AI governance | Prompt/tool/model logs, evidence IDs, confidence | AI runs traceable |
| Incident monitoring | App Insights, logs, alerts | Operational failures visible |

### 11.3 Compliance Epics

| Epic | Description | Exit Criteria |
|---|---|---|
| Consent capture | Record customer consent for channel interactions | Consent events logged |
| Claims register support | Track required claims register fields | Export possible |
| SLA tracking | Acknowledgement/review/settlement timing | SLA risk/breach visible |
| Complaint readiness | Route complaint-related interactions | Complaint classification exists |
| Legal review register | Document unresolved regulatory issues | Legal questions tracked |
| Data residency review | Confirm Azure region/legal acceptability | Region decision approved |

### 11.4 Phase 6 Exit Criteria

- Security controls pass QA/security review.
- Audit logs capture sensitive actions.
- RBAC and tenant isolation are tested.
- AI cannot bypass human approval gates.
- Provider health and telemetry are visible.
- Legal/compliance open questions are documented.

---

## 12. Phase 7 - QA, UAT, and Pilot Readiness

### 12.1 Objective

Prepare the system for controlled pilot deployment.

### 12.2 Testing Scope

| Test Area | Required Coverage |
|---|---|
| Functional testing | All MVP modules and workflows |
| UI state testing | Loading, empty, error, unauthorized, not configured, degraded |
| API testing | Auth, tenant context, validation, pagination, errors, audit |
| Document testing | OCR/extraction, classification, low confidence, mismatches |
| AI testing | Grounding, source citations, refusal boundaries, no autonomous decisions |
| Integration testing | Graph, WhatsApp, Blob, export, provider health |
| Security testing | RBAC, tenant isolation, secret handling, audit trails |
| Performance testing | Queue load, document processing, list views, search |
| UAT | Claims officer, supervisor, admin, compliance reviewer scenarios |
| Deployment testing | CI/CD, rollback, health checks, staging parity |

### 12.3 Pilot Demo Dataset

Prepare synthetic/redacted motor claim packages:

- Standard clean claim.
- Missing police report.
- Expired driving permit.
- Mismatched names.
- Mismatched dates.
- High-value repair quotation.
- Duplicate invoice.
- Blurry ID.
- Potential fraud signal.
- Escalated claim.
- Complaint-like customer interaction.

### 12.4 Release Candidate Exit Criteria

- All critical and high-severity defects resolved or accepted by leadership.
- No known tenant isolation defects.
- No known AI decision-boundary violations.
- No production mock endpoints.
- All priority workflows tested end-to-end.
- User/Admin Guide aligns with actual behavior.
- Deployment Plan and rollback steps tested.
- Go/no-go checklist signed off.

---

## 13. Phase 8 - Pilot Launch and Stabilization

### 13.1 Objective

Run a controlled pilot with a target insurer and prove operational value.

### 13.2 Pilot Scope

| Area | Pilot Scope |
|---|---|
| Customer segment | Insurance company |
| Business line | Motor claims |
| Users | Claims officers, supervisors, admins, compliance reviewer, support |
| Channels | Email/Graph, file upload, optional WhatsApp sandbox or pilot number |
| Documents | Motor claim documents and evidence pack |
| AI | Summary, checklist, extraction review, policy Q&A, advisory fraud signal |
| Decisions | Human-reviewed only |
| Reporting | Claims volume, processing status, SLA, missing documents, AI quality, audit |

### 13.3 Pilot Metrics

| Metric | Target Direction |
|---|---|
| Claim intake time | Reduce |
| Missing-document follow-ups | Reduce |
| Manual document review time | Reduce |
| Claim summary preparation time | Reduce |
| First customer response time | Improve |
| Claims officer productivity | Improve |
| Evidence completeness | Improve |
| SLA visibility | Improve |
| Audit traceability | Improve |
| User confidence | Improve |
| AI decision-boundary violations | Zero |
| Regulatory/security incidents | Zero |

### 13.4 Pilot Support Model

- Daily monitoring during initial launch period.
- Clear support escalation path.
- Issue log with severity and owner.
- Weekly pilot steering review.
- User feedback capture.
- AI output review sessions.
- Integration failure monitoring.
- Pilot retrospective and phase 2 recommendation.

### 13.5 Pilot Exit Criteria

- Target workflows completed successfully.
- Pilot users can operate the core system without engineering support for routine tasks.
- AI outputs are useful and safely governed.
- Claims evidence packs are auditable.
- Measurable operational improvement is shown.
- Leadership can decide whether to proceed to paid rollout, private deployment, or expanded module build.

---

## 14. Phase 9 - Post-Pilot Expansion Roadmap

### 14.1 Expansion Modules

| Module | Description | Timing |
|---|---|---|
| Fraud Intelligence Module | Dedicated fraud/inconsistency scoring, external reference checks, case investigation workbench | Phase 2 |
| Underwriting Support Module | Proposal parsing, risk summaries, missing information, guideline retrieval, underwriter note drafting | Phase 2 |
| Early Warning / Retention Module | Churn risk, payment behavior, renewal risk, proactive customer nudges | Phase 2 |
| Customer Acquisition Module | Product education, quote guidance, lead qualification, WhatsApp journeys, broker handoff | Phase 2 |
| Advanced Analytics | Power BI/Fabric dashboards, ROI, claims trends, AI quality, cost reporting | Phase 2-3 |
| Broker Workspace | Broker submission support, renewal tracking, client summaries | Phase 3 |
| Policy Servicing | Endorsements, renewals, certificates, customer-safe policy support | Phase 3 |
| Marketplace Packaging | Azure Marketplace SaaS/Managed Application packaging, plans, metering | Phase 3 |
| Voice/Call Agent | Speech, call summaries, contact-center augmentation | Future |
| Multi-country Configuration | Country, currency, document, language, and regulatory templates | Future |

### 14.2 Underwriting Copilot Scope

Start with basic underwriting support, not autonomous underwriting.

Phase 2 capabilities:

- Upload proposal/submission documents.
- Extract applicant and risk details.
- Identify missing information.
- Retrieve underwriting guidelines.
- Generate draft underwriter notes.
- Flag referral conditions.
- Prepare broker/customer questions.

Guardrails:

- Must not bind policy.
- Must not approve/reject risk.
- Must not set final premium.
- Must require human underwriter approval.

### 14.3 Fraud Module Scope

The Fraud Intelligence module should be positioned as a paid add-on or advanced module.

Capabilities:

- Duplicate claim detection.
- Duplicate invoice detection.
- Mismatched names/dates/IDs.
- Repeated claimant/provider patterns.
- Inflated quotation indicators.
- Document inconsistency detection.
- Human fraud investigator workflow.

Guardrails:

- Must not accuse a customer of fraud.
- Must label outputs as signals.
- Must provide evidence for each flag.
- Must allow false-positive review.

### 14.4 Early Warning Scope

The Early Warning module should focus on retention and operational risk.

Capabilities:

- Detect customers at risk of lapse.
- Identify missed premium patterns.
- Flag high-risk accounts.
- Recommend retention outreach.
- Trigger human-reviewed communication.
- Support customer and portfolio dashboards.

Guardrails:

- Must not make unfair customer classifications.
- Must not make adverse policy decisions automatically.
- Must preserve auditability and customer communication controls.

---

## 15. Workstream Roadmaps

### 15.1 Product and Delivery Workstream

| Phase | Product/Delivery Tasks |
|---|---|
| 0 | Lock naming, MVP, first buyer, claim type, scope boundaries |
| 1 | Convert PRD/FSD into backlog and acceptance criteria |
| 2 | Validate UI module behavior with stakeholders |
| 3 | Define claims workflow rules and status transitions |
| 4 | Define document templates and extraction fields |
| 5 | Confirm integration provider priorities |
| 6 | Coordinate legal/security review |
| 7 | Run UAT scripts and pilot readiness review |
| 8 | Run pilot steering and feedback sessions |
| 9 | Prioritize Phase 2 modules |

### 15.2 Frontend Workstream

| Phase | Frontend Tasks |
|---|---|
| 1 | App shell, routing, auth states, tenant/workspace display, base components |
| 2 | Command Center, Channels, Customers, Sales & Leads |
| 3 | Claims Inbox, FNOL, Case Review, Approvals |
| 4 | Document review, extraction fields, confidence, evidence viewer |
| 5 | Provider health, integration states, channel status |
| 6 | Audit views, admin settings, RBAC-aware UI |
| 7 | UAT fixes, accessibility, responsiveness, visual QA |
| 8 | Pilot feedback refinements |

### 15.3 Backend Workstream

| Phase | Backend Tasks |
|---|---|
| 1 | .NET API foundation, EF Core migrations, tenant context, audit service |
| 2 | Overview endpoints for Command Center, Channels, Customers, Sales & Leads |
| 3 | Claims, FNOL, tasks, assignments, approvals, statuses |
| 4 | Documents, extraction results, checklists, AI run records |
| 5 | Graph/email, WhatsApp, export, integration events, retry queues |
| 6 | Authorization hardening, audit export, provider health, monitoring |
| 7 | Test coverage and performance improvements |
| 8 | Pilot fixes and telemetry-driven optimization |

### 15.4 AI and Document Intelligence Workstream

| Phase | AI/Document Tasks |
|---|---|
| 1 | Provider abstraction and mock/local mode |
| 3 | Claim summary prompt contracts and AI run logging |
| 4 | Document Intelligence adapter, extraction schema, confidence handling |
| 4 | RAG over approved policy/knowledge sources |
| 5 | Customer-safe message drafting with approval controls |
| 6 | AI governance tests and prompt/tool registry |
| 7 | AI quality evaluation and red-team testing |
| 8 | Human feedback loop and output tuning |

### 15.5 Cloud/DevOps Workstream

| Phase | Cloud/DevOps Tasks |
|---|---|
| 1 | Azure resource baseline and Key Vault pattern |
| 1 | CI/CD for frontend/backend/workers/IaC |
| 5 | Service Bus/Event Grid/Functions setup for async workflows |
| 6 | App Insights, Log Analytics, alerts, dashboards |
| 7 | Staging/pilot deployment validation and rollback rehearsal |
| 8 | Pilot production support and operational monitoring |
| 9 | Managed Application / Marketplace path |

### 15.6 Security and Compliance Workstream

| Phase | Security/Compliance Tasks |
|---|---|
| 1 | Auth, RBAC, tenant isolation requirements |
| 3 | Human approval and decision-boundary checks |
| 4 | AI audit metadata and source grounding rules |
| 5 | Integration permission review |
| 6 | Security testing, audit logs, privacy controls |
| 7 | Legal review checklist and release gate |
| 8 | Pilot monitoring and incident response readiness |

### 15.7 QA Workstream

| Phase | QA Tasks |
|---|---|
| 1 | Test plan setup and baseline smoke tests |
| 2 | UI state testing for core workspaces |
| 3 | Claims workflow end-to-end tests |
| 4 | Document extraction and AI behavior tests |
| 5 | Integration and resilience tests |
| 6 | Security/compliance tests |
| 7 | UAT, regression, release candidate certification |
| 8 | Pilot defect triage and feedback validation |

---

## 16. MVP Backlog Structure

### 16.1 Epic List

| Epic ID | Epic | MVP Priority |
|---|---|---|
| E01 | App shell, auth, tenant/workspace context | Critical |
| E02 | Role-aware navigation and UI states | Critical |
| E03 | Command Center | Critical |
| E04 | Channels Inbox | High |
| E05 | Sales & Leads Inbox | High |
| E06 | Customer 360 | High |
| E07 | Claims Work Queue | Critical |
| E08 | FNOL / New Claim Intake | Critical |
| E09 | Case Review Workspace | Critical |
| E10 | Document Upload and Storage | Critical |
| E11 | Document Classification and Extraction | Critical |
| E12 | Missing Document Checklist | Critical |
| E13 | AI Claim Summary | Critical |
| E14 | Policy Q&A / Knowledge Retrieval | High |
| E15 | Human Review and Approvals | Critical |
| E16 | Customer Update Drafting | High |
| E17 | Audit Trail and Compliance Logs | Critical |
| E18 | Microsoft Graph / Email Ingestion | High |
| E19 | WhatsApp Channel Integration | High |
| E20 | Export and Evidence Pack | High |
| E21 | Admin Settings | High |
| E22 | Support and Provider Health | High |
| E23 | QA and Release Certification | Critical |
| E24 | Deployment and Monitoring | Critical |

### 16.2 Acceptance Criteria Pattern

Every epic should include:

- User story.
- Roles allowed.
- Data dependencies.
- API endpoints.
- UI states.
- Audit events.
- Error handling.
- Security/RBAC requirement.
- Tenant/workspace enforcement.
- AI guardrail if applicable.
- Test cases.
- Definition of done.

---

## 17. Dependencies and Critical Path

### 17.1 Critical Dependencies

| Dependency | Blocks |
|---|---|
| Product name lock | Documentation, UI labels, repo instructions |
| Tenant/workspace model | API, data model, security, all modules |
| Auth/RBAC foundation | All staff workflows |
| Audit foundation | AI, claims, documents, approvals, integrations |
| Document storage | Document Intelligence workflow |
| Claims status model | Work Queue, Case Review, approvals, reporting |
| Provider configuration | AI, Document Intelligence, Graph, WhatsApp |
| Synthetic documents | Document testing, demos, AI evaluation |
| Azure region/quota confirmation | Deployment and AI service readiness |
| Legal/data residency review | Production pilot go/no-go |
| First pilot customer systems | Integration depth and demo realism |

### 17.2 Critical Path

```text
Decision lock
↓
Tenant/auth/audit foundation
↓
Claims data model and APIs
↓
Claims work queue and case review
↓
Document upload/storage
↓
Document extraction/review
↓
AI summary/checklist
↓
Human approval/audit
↓
Integration health/export
↓
QA/security/UAT
↓
Pilot launch
```

---

## 18. Decision Gates

| Gate | When | Required Decision |
|---|---|---|
| Gate 0 | Before implementation | Name, MVP, first workflow, deployment assumption locked |
| Gate 1 | After foundation | Auth, tenant/workspace, audit, environment baseline accepted |
| Gate 2 | After core UI | Command Center and core workspaces accepted |
| Gate 3 | After claims workflow | FNOL to Case Review workflow accepted |
| Gate 4 | After document AI | Extraction, checklist, summary, and human review accepted |
| Gate 5 | Before staging | Integrations, provider health, no mock production confirmed |
| Gate 6 | Before pilot | Security, QA, UAT, legal risks, deployment checklist accepted |
| Gate 7 | After pilot | Continue, pivot, expand, or package decision |

---

## 19. Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Scope creep into all insurance modules | Delays MVP | Keep Motor Claims as MVP; move others to Phase 2 |
| AI autonomy language creates compliance concern | Regulatory/client trust risk | Use human-reviewed decision support wording everywhere |
| Core insurer systems unavailable | Integration delay | Use secure upload, Graph/email, CSV/export, read-only adapters first |
| Azure AI service/quota unavailable in preferred region | Deployment delay | Confirm early; define fallback region and legal review |
| WhatsApp provider not confirmed | Channel delay | Build provider abstraction and Not Configured state |
| Poor document samples | Weak AI demo/testing | Build synthetic/redacted Motor Claim test pack early |
| Frontend-only RBAC | Security failure | Enforce authorization server-side |
| Mock data leaks into staging/production | False demo confidence | Add release gate preventing mock providers in staging/prod |
| Overbuilding Marketplace before pilot | Delays product learning | Defer full Marketplace billing unless required |
| Legal questions unresolved | Pilot blocked | Maintain legal review checklist and decision log |
| UI becomes dashboard-only | Weak product differentiation | Enforce three-panel, chat-first command center pattern |

---

## 20. 30-60-90 Day Implementation View

### 20.1 First 30 Days

| Outcome | Key Work |
|---|---|
| Foundation ready | Auth, tenant context, API foundation, audit, app shell |
| Core docs aligned | Naming, roadmap, backlog, AGENTS.md, PRD/FSD alignment |
| Core UI started | Command Center, Channels, Customers, Sales & Leads |
| Claims model started | Claim statuses, FNOL, Work Queue APIs |
| Azure path confirmed | Region, services, quotas, Key Vault, monitoring plan |

### 20.2 Days 31-60

| Outcome | Key Work |
|---|---|
| Claims workflow usable | FNOL, Work Queue, Case Review, assignment, escalation |
| Document workflow usable | Upload, storage, extraction, validation, review |
| AI assistance introduced | Claim summary, checklist, policy retrieval, customer draft |
| Integration baseline | Graph/email, export, provider health, WhatsApp decision |
| Security hardening | RBAC, tenant tests, audit review, AI guardrail tests |

### 20.3 Days 61-90

| Outcome | Key Work |
|---|---|
| Pilot release candidate | End-to-end tests, UAT, deployment, rollback, monitoring |
| Demo/pilot content ready | Synthetic motor claim packs, demo script, user/admin guide |
| Pilot launch | Controlled insurer pilot, support, metrics, feedback |
| Phase 2 prioritization | Underwriting, fraud, early warning, acquisition enhancements |

---

## 21. Team Roles and Ownership

| Role | Primary Responsibility |
|---|---|
| Product Lead | Scope, requirements, backlog, decision log, stakeholder alignment |
| Engineering Lead | Technical delivery, code quality, architecture adherence |
| Frontend Engineer/Agent | App shell, workspaces, UI states, Copilot panel, UX behavior |
| Backend Engineer/Agent | APIs, data model, services, auth enforcement, audit, integrations |
| AI Engineer/Agent | Prompt contracts, tool registry, RAG, model/provider integration, evaluations |
| Cloud/DevOps Lead | Azure resources, CI/CD, Key Vault, monitoring, deployment, rollback |
| Security Lead | RBAC, tenant isolation, privacy, audit, compliance checks |
| QA Lead | Test plan execution, UAT, regression, release gates |
| Claims SME | Motor claims workflow, documents, statuses, approval rules |
| Legal/Compliance Reviewer | Uganda data/privacy/IRA questions and risk review |
| Delivery/Support Lead | Pilot onboarding, training, support process, issue triage |

---

## 22. Implementation Agent Guardrails

Coding agents working on IIP must follow these rules:

1. Use **Insurance Intelligence Platform (IIP)** naming.
2. Do not reintroduce ACIP as the active product name.
3. Do not implement autonomous claim approval, rejection, settlement, fraud determination, policy binding, or final underwriting decisions.
4. Do not rely on frontend-only permissions.
5. Do not bypass tenant/workspace filters.
6. Do not give AI agents generic database query tools.
7. Do not store secrets in code, docs, or client-side environment variables.
8. Do not use mock data in staging/production paths.
9. Show Not Configured states for unavailable providers.
10. Create loading, empty, error, unauthorized, and degraded states for every workspace.
11. Log audit events for sensitive user, system, AI, document, and integration actions.
12. Keep Graph, WhatsApp, AI, Document Intelligence, and core system integrations behind provider interfaces.
13. Ensure AI outputs show uncertainty and evidence references where applicable.
14. Preserve human approval gates for consequential actions.
15. Update tests and documentation when behavior changes.

---

## 23. Definition of Done

A feature is not done until:

- It satisfies PRD/FSD acceptance criteria.
- It uses correct IIP naming.
- It enforces tenant/workspace context server-side.
- It enforces role permissions server-side.
- It handles loading, empty, error, unauthorized, and Not Configured states.
- It creates required audit events.
- It avoids autonomous regulated decisions.
- It has tests for happy path and key failure paths.
- It works in local mode where relevant.
- It does not rely on fake production data.
- It is observable through logs/telemetry.
- It is documented for users/admins if user-facing.
- It passes QA and release gate checks.

---

## 24. Pilot Readiness Checklist

| Area | Checklist |
|---|---|
| Product | MVP scope confirmed; pilot customer known; success metrics agreed |
| UI | Command Center, Channels, Claims, Documents, Approvals usable |
| Claims | Motor claim workflow tested end-to-end |
| Documents | Synthetic/redacted claim packs tested |
| AI | Summaries/checklists/source grounding tested and safe |
| Security | RBAC, tenant isolation, audit, secrets reviewed |
| Compliance | Legal/data residency/consent questions logged and reviewed |
| Integrations | Graph/email and document upload working; WhatsApp configured or Not Configured |
| Deployment | Staging/pilot environment deployed; rollback tested |
| QA | Critical/high defects closed or accepted |
| Training | User/Admin Guide and demo script ready |
| Support | Incident, escalation, monitoring, and issue logs ready |

---

## 25. Recommended Immediate Next Actions

| # | Action | Owner |
|---|---|---|
| 1 | Convert this roadmap into an implementation backlog with epics and tasks | Product/Engineering |
| 2 | Update AGENTS.md/CODEX.md with IIP naming and guardrails | Engineering |
| 3 | Confirm the current codebase state against the FSD and UI/UX Handoff | Engineering/Antigravity |
| 4 | Lock the Motor Claims field extraction matrix | Product/Claims SME |
| 5 | Build or verify tenant/workspace/auth/audit foundation | Backend/Security |
| 6 | Prioritize Command Center, Channels, Customers, Sales & Leads, Claims Inbox | Frontend/Product |
| 7 | Prepare synthetic/redacted Motor Claim sample packs | Product/QA |
| 8 | Confirm Azure region, AI model quota, and provider availability | Cloud |
| 9 | Confirm WhatsApp provider and Microsoft Graph mailbox plan | Engineering/Client |
| 10 | Create release gates in CI/CD to block mock providers in staging/production | DevOps |
| 11 | Create UAT script for claims officer, supervisor, admin, and compliance reviewer | QA/Product |
| 12 | Schedule Gate 0 decision meeting for naming, MVP, deployment, and pilot assumptions | Leadership |

---

## 26. Roadmap Summary

The IIP implementation should prioritize depth and credibility in one end-to-end insurance workflow before expanding into every promising module. The first win is not building every feature; it is proving that IIP can intelligently receive claim inputs, process documents, prepare a human-reviewable case, preserve evidence, and improve claims operations while remaining safe, auditable, and compliant.

The recommended MVP path is:

```text
Secure foundation
+ Core workspaces
+ Motor Claims workflow
+ Document Intelligence
+ AI-assisted summaries/checklists
+ Human review and audit
+ Email/WhatsApp/channel integration
+ QA/security/deployment hardening
= Pilot-ready Insurance Intelligence Platform
```

After the pilot, IIP can expand into Customer Acquisition, Fraud Intelligence, Underwriting Support, Early Warning/Retention, Advanced Analytics, Broker Workspaces, and Azure Marketplace packaging.

The roadmap should remain disciplined:

> **Build the claims/document intelligence core first. Prove trust. Prove speed. Prove auditability. Then expand the platform.**
