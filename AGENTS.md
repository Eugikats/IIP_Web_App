# AGENTS.md - Insurance Intelligence Platform (IIP) Production Build and Release Contract

> **Production contract update - July 2026:** This canonical file aligns the repository build contract with the official product name **Insurance Intelligence Platform (IIP)**, the staff application name **IIP Operations**, the zero-simulation deployed-runtime mandate, enterprise UI quality gate, security hardening requirements, real-provider activation plan, Azure Managed Application / Partner Center release gates, and the current IIP documentation library.

## 0. Purpose and authority

You are Codex working inside the repository for **Insurance Intelligence Platform (IIP)**.

This file is the repository contract for how to build the product. Read it before making changes. Follow it unless a human maintainer explicitly changes the instructions.

Before implementing anything, also read `IIP_IMPLEMENTATION_GUARDRAILS.md`. It is the shared implementation guardrail contract for Codex, Claude, Antigravity, and any other coding or automation agent.

Build a commercially credible **Azure Marketplace Managed Application** for insurers. The platform must be **document-first, workflow-first, human-supervised, auditable, tenant-isolated, insurer-grade, and Azure-native**.

The product is not AI that makes insurance decisions. It is **AI that prepares insurance work for humans to decide**.

### 0.1 Naming standard

Use these names consistently:

| Context | Required name |
|---|---|
| Formal product name | **Insurance Intelligence Platform (IIP)** |
| Short product name | **IIP** |
| Staff application name | **IIP Operations** |
| Repository / code identifiers | Keep existing names such as `InsuranceIntelligence.sln`, `InsuranceIntelligence.Api`, and `INSURANCEINTELLIGENCE_*` unless a human maintainer explicitly approves a refactor. |

Do not use `ACIP` as the official product name. Treat any older references to `ACIP` as legacy naming and replace them with **IIP** unless they are part of historical notes.

Do not use placeholder or reference-image brand names.

---

## 0A. Authoritative production release override - zero simulation in deployed environments

**Effective immediately: this section has priority over any older mock-first, MVP-placeholder, developer-demo, or optional-provider language elsewhere in this file.** If any older section conflicts with this override, follow this override and update the older implementation/documentation accordingly.

We are proceeding toward a real production deployment and Azure Managed Application / private-plan-ready release.

### 0A.1 Non-negotiable production rule

The deployed platform must be real. **No mock, simulated, fake, seeded-demo, placeholder, in-memory, SQLite, DevelopmentAuth, demo-header, hardcoded-insurer, hardcoded-user, fake-ingestion, fake-send, fake-dashboard, or fallback-to-mock behavior is permitted in:**

- Azure staging / production-candidate environments;
- production environments;
- customer-deployed Azure Managed Application instances;
- Partner Center reviewer / private-plan deployments;
- any publicly reachable or insurer-facing environment.

Test doubles may remain only:

- inside automated test projects;
- inside an explicitly isolated local-test harness that is not deployed and cannot be enabled in cloud/customer runtime.

Do not remove useful tests. Remove mock/simulation behavior from **deployable backend runtime registrations, deployed configuration, and deployed frontend surfaces**.

### 0A.2 Required real production runtime providers

| Capability | Required staging/production/reviewer implementation | Forbidden deployed alternatives |
|---|---|---|
| Authentication | Microsoft Entra ID only | Development Auth Mode, demo headers, browser-selected role |
| Authorization | Server-validated claims/app roles/groups + tenant/workspace enforcement | UI-only permission checks, arbitrary tenant headers as authority |
| Persistence | Azure SQL Database only | SQLite, in-memory repositories |
| Files/documents | Azure Blob Storage only | local file/mock storage |
| Document extraction | Azure AI Document Intelligence only | mock extraction/placeholder results |
| Knowledge retrieval/indexing | Azure AI Search only | in-memory/mock retrieval |
| AI generation/embeddings | Real Azure OpenAI or approved Microsoft Foundry deployments only | mock assistant, canned responses |
| Workflow queues/events | Azure Service Bus only | mock queue/in-process simulation |
| Outlook/email | Real Microsoft Graph connected to tenant-configurable insurer mailboxes | simulated email, fixed global claims mailbox |
| Secrets | Key Vault references and managed identities wherever possible | secrets in source/frontend/appsettings/docs |
| Monitoring | Real Application Insights and Log Analytics | placeholder-only health/telemetry |
| Customer communications | Real configured provider, or feature disabled as `Not configured` | simulated send |

### 0A.3 Explicit environment profiles and validation gate

Implement and enforce explicit runtime profiles:

| Profile | Purpose | Test doubles allowed? | Deployable/customer-facing? |
|---|---|---:|---:|
| `LocalTest` | Unit/integration tests and isolated local verification only | Yes | No |
| `Development` | Internal development only, visibly marked and not customer-facing | Only if explicitly isolated | No |
| `Staging` | Production-candidate validation using synthetic data | No | Yes, internal validation only |
| `Production` | Live insurer/customer operation | No | Yes |
| `ManagedApplicationReviewer` | Partner Center/private-plan review deployment | No | Yes |

Application startup/readiness must fail safely in `Staging`, `Production`, or `ManagedApplicationReviewer` when any required provider or behavior is set to or resolves to:

```text
Mock
Simulated
Placeholder
InMemory
SQLite
DevelopmentAuth
Demo
SeededDemoBusinessData
FallbackToMock
```

Required behavior:

- No silent fallback from a real provider to a fake provider.
- Missing or unhealthy mandatory live providers return `Not Ready` and block release.
- Production provider-health surfaces report real providers only and expose status without secrets.
- Synthetic data is permitted for pre-release testing; fake service behavior is not.

### 0A.4 Deployed frontend/runtime removals

In deployed staging/production/reviewer builds and runtime, remove or disable:

- demo role switcher;
- mock/simulation mode controls or badges;
- fake-ingestion/test-email buttons that generate invented messages;
- simulated-send controls;
- seed-data or synthetic-demo-data UI actions;
- hardcoded insurer brands, users, mailboxes, cases, metrics, approvals, documents, messages, or dashboards;
- placeholder operational records;
- UI pathways that present incomplete providers as functioning services.

A non-configured real feature must display **`Not configured`** or be unavailable; it must never appear successful through simulation.

### 0A.5 Tenant-configurable real Outlook/email model

There is no platform-wide fixed claims mailbox. Each insurer deployment must connect one or more of that insurer's existing approved Microsoft 365 mailboxes through Microsoft Graph, for example:

```text
claims@insurer.co.ug
complaints@insurer.co.ug
brokers@insurer.co.ug
motorclaims@insurer.co.ug
medicalclaims@insurer.co.ug
```

Support:

- multiple mailboxes per tenant;
- mailbox-to-workspace mapping;
- mailbox-to-product-line/case-type mapping;
- Exchange Online RBAC for Applications or an explicitly documented approved mailbox-scoping alternative;
- inbound email-to-case creation/linking;
- attachment-to-document processing;
- approved outbound customer email;
- idempotency and complete audit trails.

### 0A.6 Required production enforcement tests

Add and preserve automated tests proving that staging/production/reviewer profiles:

- reject demo authentication headers;
- reject Development Auth Mode;
- reject SQLite and in-memory persistence;
- reject mock/simulated/placeholder providers;
- reject seeded demo business data;
- do not render mock UI controls or hardcoded records;
- report `Not Ready` for missing/unhealthy required live providers;
- prevent cross-tenant/cross-workspace access;
- exclude secrets, prompts, system messages, tokens, SAS values, internal notes, fraud flags, reserves, and connection strings from client-facing surfaces.

### 0A.7 Production readiness truthfulness

Never claim the platform is production-ready, deployment-ready, Managed Application-ready, or Partner Center-ready until the required gate evidence in Section 31 passes. The product may be **production-candidate** only after live Azure staging tests with synthetic data prove end-to-end behavior.

---

## 0B. Authoritative documentation library

Before major implementation, release, pilot, support, commercial, or architecture changes, consult the relevant IIP documentation pack. These documents are the project reference set and should be stored under `docs/` using the repository's approved folder structure.

Recommended repository structure:

```text
docs/
  strategy-requirements-roadmap/
  development-engineering/
  design-architecture/
  testing-security-quality/
  deployment-release-enablement/
  client-pilot/
  client-support/
  continuous-improvement/
  commercial-executive/
  ui/
  security/
  production/
  adr/
  runbooks/
```

### 0B.1 Core product and requirements documents

Use these for product direction, scope, acceptance, and traceability:

- Master Product Context
- Product Requirements Document
- Functional Specification Document
- Implementation Roadmap
- Strategy Addendum / Product Strategy Update
- Requirements Traceability Matrix
- MVP Scope Control Register
- Product Decision Log
- Product Risk Register
- Roadmap Execution Backlog
- Requirements Change Control Template
- Assumptions, Constraints & Open Questions Register
- Stakeholder Alignment Brief
- MVP Acceptance Criteria Register

### 0B.2 Development and engineering documents

Use these for actual build execution:

- Engineering Implementation Backlog
- Backend Implementation Guide - Managed Application
- Backend Implementation Guide - SaaS
- Frontend Implementation Guide
- Component Architecture Document
- Database Implementation & Migration Plan
- Integration Implementation Playbook
- AI Agent Architecture & Tool Registry
- AI/RAG Knowledge Architecture
- Document Intelligence Implementation Guide
- RBAC & Permissions Matrix
- Developer Setup Guide
- Environment Variables & Secrets Guide
- CI/CD Pipeline Guide
- Coding Agent Guardrails / `AGENTS.md`

### 0B.3 Design and architecture documents

Use these before changing architecture, infrastructure, UX, or operational flow:

- Technical Architecture Document
- System Integration Strategy
- UI/UX Handoff Document
- Design System & UX Behavior Specification
- Workflow Architecture Document
- C4 Architecture Diagram Pack
- Infrastructure-as-Code Architecture Guide
- Observability & Operations Architecture
- Hybrid Cloud Mirror / Resilient Channel Buffer Architecture
- Managed Application Architecture Guide
- SaaS Tenant Architecture Guide

### 0B.4 Testing, security, and quality documents

Use these to validate readiness and safety:

- Security & Compliance Document
- QA/Test Plan
- Functional Test Cases & Acceptance Criteria Pack
- Security Test Checklist / Security Validation Plan
- API Test Suite Specification
- AI Output Evaluation & Red-Team Test Plan
- Document Intelligence Test Pack Specification
- Performance, Load & Resilience Test Plan
- Release Readiness Checklist
- Compliance Evidence Register
- Incident Response & Breach Handling Runbook
- Regression Test Checklist

### 0B.5 Deployment, release, and client enablement documents

Use these for release readiness and client deployment:

- Deployment Plan
- Release Management Plan
- Environment Setup Guide
- Production Support Runbook
- Rollback & Disaster Recovery Runbook
- Azure Managed Application Packaging Guide
- SaaS Tenant Provisioning Guide
- UAT Plan
- Data Migration / Seed Data Plan
- Cutover Plan
- Post-Deployment Validation Checklist
- Release Notes Template
- Change Control Process

### 0B.6 Client pilot, support, continuous improvement, and commercial documents

Use these for pilot delivery, support operations, feedback, and sales readiness:

- Client Pilot Charter
- Pilot Launch Plan
- Pilot Success Measurement Plan
- Client Configuration Workbook
- Pilot UAT Script
- Pilot Sign-Off Form
- Pilot Risk Register
- Pilot Weekly Status Report Template
- Pilot Closeout Report
- Pilot Governance Meeting Pack
- Pilot Communications Plan
- Support & Escalation Guide
- SLA / Support Response Matrix
- Issue Log Template
- Incident Report Template
- Known Issues Register
- Client FAQ / Troubleshooting Guide
- Support Handover Checklist
- Client Support Operating Model
- Maintenance Window & Downtime Notice Template
- Feedback Log Template
- Improvement Backlog Template
- Feature Prioritization Matrix
- AI Quality Review Log
- Document Extraction Error Log
- Pilot Lessons Learned Report
- Post-Launch Review Template
- Release Improvement Tracker
- Customer Success Health Scorecard
- Product Feedback Review Cadence
- Business Case / ROI Document
- Pilot Proposal Document
- Pricing & Packaging Document
- Executive Pitch Deck Content Document
- Demo Script / Walkthrough Guide
- FAQ / Objection Handling Document
- One-Page Product Brief
- Client Readiness Checklist

---

## 1. Product definition

**Insurance Intelligence Platform (IIP)** is an Azure-native insurance operations platform and Azure Marketplace Managed Application for insurance companies. It helps insurers process insurance work faster by using AI assistance for:

- claims intake;
- policy explanation;
- document checking;
- case routing;
- customer updates;
- human review;
- audit logging;
- operational dashboards;
- future underwriting, fraud-signal triage, compliance, provider-invoice, broker, microinsurance, and agriculture-insurance support.

The platform must support regulated insurance operations. Every important action must preserve evidence, traceability, source references, confidence, and human accountability.

### Product thesis

IIP should feel like a **modern insurance operations command center**, not a generic chatbot UI.

A user should log in and immediately answer:

1. What claims need attention?
2. Which claims are missing documents?
3. Which cases are close to SLA breach?
4. What has AI already prepared?
5. Which decisions require a human?
6. What customer updates need to be sent?
7. Which policy documents did AI use?
8. What is the audit trail?
9. What is the operational impact?
10. What is the AI cost and quality trend?

---

## 2. Release strategy

### V1: IIP Claims & Customer Support Agent

Build this first. This is the impactful MVP.

V1 must include:

- Azure Marketplace/private managed app deployment path;
- tenant onboarding;
- Microsoft Entra ID login;
- role-based dashboard experience;
- Command Center;
- Claims Inbox;
- FNOL/new claim intake;
- Case Review;
- Document Checker;
- real Azure AI Document Intelligence extraction in deployed staging/production; test doubles permitted only in isolated tests/local-test execution;
- completeness checklist;
- Policy Q&A over approved knowledge only;
- AI case summary;
- human approval queue;
- customer update drafting;
- Outlook/Exchange Online shared mailbox ingestion through Microsoft Graph;
- SMS/email notification adapter where configured;
- Knowledge Base upload and approval workflow;
- Customer Service dashboard;
- customer-safe external status surface / optional Client Portal Preview;
- Admin Settings;
- Integrations settings;
- audit logs;
- basic analytics;
- isolated local-test execution may use test doubles, but no deployed staging, production, customer, or Partner Center reviewer runtime may use mock or simulated providers.

Optional but recommended in V1:

- Teams alerts for escalations and approvals;
- Power BI report placeholders or embedded-report-ready design;
- SharePoint import/export shell.

Do not build these as V1 core unless explicitly requested:

- Dynamics/Dataverse integration;
- underwriting copilot;
- provider invoice checker;
- fraud intelligence as a final-decision system;
- regulatory reporting agent;
- advanced Teams approvals app;
- Microsoft Purview governance;
- Microsoft Fabric analytics;
- AKS runtime edition;
- full voice/call agent module.

### V2: Insurance Operations Copilot

Feature-flag and build after V1 is stable:

- underwriting submission checker;
- broker copilot;
- provider invoice checker;
- complaint response agent;
- Teams approvals;
- SharePoint sync;
- Power BI analytics;
- Dynamics/Dataverse integration;
- stronger admin controls;
- Foundry Agent Service observability/evaluations where useful.

### V3: Market Intelligence & Inclusion Platform

Feature-flag and build after V2 foundations:

- fraud triage that flags suspicious patterns but does not accuse or deny;
- regulatory reporting agent;
- microinsurance support;
- agriculture insurance support;
- advanced analytics;
- Microsoft Purview governance;
- multi-agent orchestration;
- voice/call automation where legally and operationally safe.

---

## 3. Non-negotiable product guardrails

### 3.1 Human-supervised insurance decisions

Never implement autonomous final decisions for:

- claim denial;
- claim repudiation;
- settlement approval;
- high-value payment approval;
- policy cancellation;
- premium change;
- underwriting acceptance or rejection;
- adverse underwriting outcome;
- fraud accusation;
- vulnerable-customer handling decision;
- complaint final response;
- regulatory filing submission;
- payment release.

The AI may recommend, summarize, classify, flag, draft, or route. A human approver must make the consequential decision.

### 3.2 Grounding and evidence

All AI answers that affect a customer, claim, policy, payment, complaint, underwriting workflow, compliance workflow, or broker workflow must:

- retrieve from approved tenant knowledge first;
- use tenant/workspace filters;
- cite source documents, sections, or evidence IDs;
- show confidence or uncertainty in staff surfaces where appropriate;
- escalate when evidence is missing, conflicting, expired, or low confidence;
- log prompt version, retrieved evidence IDs, tool calls, model/provider metadata, output, confidence, and user action;
- never fabricate policy terms, exclusions, amounts, limits, dates, claim obligations, or regulatory obligations.

Never answer policy coverage questions from model memory.

### 3.3 Tenant isolation

Default enterprise deployment model is **one insurer per Azure Managed Application deployment** into the customer's Azure subscription.

Inside one insurer deployment, support logical subtenants/workspaces by:

- business unit;
- branch;
- region;
- product line;
- brand;
- broker network;
- country where relevant.

Every query, document, search index item, case, communication, integration event, usage meter, and audit record must carry tenant and workspace identifiers.

### 3.4 Customer-safe data boundaries

Customer-facing or external-channel surfaces must never expose:

- internal risk notes;
- fraud flags;
- claim reserves;
- internal staff comments;
- hidden routing rules;
- internal prompts;
- confidence scores that would confuse or prejudice customers;
- confidential compliance notes;
- other customers' data.

### 3.5 Privacy and security

- Do not log raw secrets, tokens, keys, full national IDs, card numbers, medical details, or unnecessary PII.
- Store secrets in Key Vault or local secret stores, not in source code.
- Use managed identities where possible.
- Use RBAC by role, branch, product line, and assignment.
- Apply least privilege.
- Design for private endpoints in production.
- Make retention and deletion policies explicit.
- Never claim the product is legally compliant by default; describe implemented controls instead.

---

## 4. Main users and role-based dashboard behavior

The dashboard must change based on user role. Do not build one generic experience for everyone.

### 4.1 Insurance company users

| User type | Primary platform use |
|---|---|
| Executive / Operations Manager | Company-wide claims, service, SLA, AI performance, cost, and productivity metrics. |
| Claims Officer | Assigned claims, document checks, AI summaries, next-action review. |
| Claims Supervisor | High-risk claims, workflow approvals, escalations, complaints, overrides. |
| Customer Service Agent | Customer questions, claim status, AI-drafted customer responses. |
| Underwriter | Future module: proposal forms, missing risk information, underwriting referrals. |
| Broker / Agent Manager | Broker submissions, proposal completeness, customer follow-ups, renewals. |
| Compliance / Risk Officer | Audit logs, AI outputs, human overrides, complaints, regulatory reports. |
| Finance / Payments Officer | Future module: invoice validation, payment packs, provider claims, payout readiness. |
| IT / Cloud Admin | Deployment, integrations, identity, security, API keys, monitoring, environments. |
| Knowledge Manager | Policies, FAQs, checklists, templates, approved knowledge for AI use. |

### 4.2 External users

External users mainly interact through controlled customer-safe channels such as WhatsApp, email, web chat, SMS, broker channels, or an optional customer-safe portal/status surface.

| User type | Primary platform use |
|---|---|
| Policyholder / Client | Submit claims, upload documents, ask policy questions, track claim status through approved customer-safe channels. |
| Broker / Sales Agent | Submit client documents, track applications, ask product questions, get checklists. |
| Garage / Hospital / Assessor | Upload estimates, invoices, reports, photos, supporting documents where configured. |
| Regulator / Auditor | Optional controlled access to reports, audit packs, complaints, regulatory evidence. |

### 4.3 Platform-side users

| User type | Primary platform use |
|---|---|
| Publisher admin / support team | Support deployed customer instances, manage versions, monitor health, handle marketplace lifecycle. |
| Implementation consultant | Configure workflows, upload policy documents, define checklists, train insurer users. |

Support access must be controlled carefully. Model support modes as `none`, `delegated`, `read_only`, and `jit` where relevant.

---

## 5. Default technical architecture

Use this default architecture unless a maintainer changes it.

### 5.1 Runtime baseline

- **Portal:** Next.js + React + TypeScript + Tailwind.
- **API:** ASP.NET Core Web API on .NET 8 LTS, hosted on Azure App Service.
- **Workflow/orchestration:** Azure Functions on .NET isolated worker + Durable Functions.
- **Database:** Azure SQL Database for transactional state. Cosmos DB can be added later only when there is a concrete need for event journaling or flexible JSON artifacts.
- **Object storage:** Azure Blob Storage / Data Lake style containers for raw documents, normalized JSON artifacts, extracted text, evidence packs, generated claim packs, and audit exports.
- **Messaging:** Azure Service Bus for async intake, commands, retries, dead-letter queues, and decoupling portal/email/API ingestion from AI processing.
- **Search/RAG:** Azure AI Search with tenant/workspace filters, semantic/hybrid/vector-ready design.
- **AI:** Azure OpenAI through provider interfaces; Microsoft Foundry Agent Service can be added when hosted agent lifecycle, evaluations, and richer tracing justify it.
- **Document extraction:** Azure AI Document Intelligence through provider interfaces.
- **Gateway:** Azure API Management for external, broker, partner, and enterprise APIs.
- **Observability:** Azure Monitor, Application Insights, and Log Analytics.
- **Security:** Microsoft Entra ID, Managed Identity, Key Vault, Azure RBAC, Azure Policy, private endpoints for regulated deployments.
- **Packaging:** Bicep authoring, compiled ARM JSON for Azure Managed Application publication.

### 5.2 Avoid in MVP

- Do not introduce AKS.
- Do not introduce a distributed microservice fleet.
- Do not require Kubernetes, Kafka, Redis, or Cosmos DB unless a concrete MVP need exists.
- Do not create complex multi-agent autonomy when deterministic workflows are enough.
- Do not couple business logic directly to one LLM SDK.
- Do not make paid cloud calls from local unit tests.

---

## 6. Product modules and screens

### 6.1 Marketplace and deployment module

Functions:

- Azure Marketplace listing page;
- plan selection;
- private offer support;
- deployment wizard;
- region selection;
- environment sizing;
- support access selection;
- terms and privacy acceptance;
- managed resource group creation;
- health check after deployment.

Implementation note: this is not only a website. The real product must be packaged as an Azure Managed Application that can be published internally through service catalog or externally through Azure Marketplace.

### 6.2 Tenant onboarding module

Functions:

- add insurer name and logo;
- select business lines;
- add users and roles;
- configure claims workflow;
- configure document checklists;
- configure products and policy types;
- set approval thresholds;
- configure customer communication channels;
- connect email/shared mailboxes;
- connect APIs or file import;
- upload policy documents and rules;
- run initial test case using synthetic data only outside production.

### 6.3 Required IIP Operations screens

The staff application must support these real operations screens:

- Command Center;
- Claims Inbox;
- New Claim Intake;
- Case Review / Case Workspace;
- Document Checker;
- AI Assistant;
- Knowledge Base;
- Approvals;
- Customer Service;
- Compliance & Audit;
- Reports & Analytics;
- Integrations;
- Admin Settings;
- Support & Health;
- Client Portal Preview / customer-safe external status surface where explicitly configured.

### 6.4 Command Center dashboard

Users:

- CEO / COO;
- Head of Claims;
- Head of Operations;
- Customer Experience Manager;
- Digital Transformation Lead;
- Compliance / Risk Officer with relevant filters.

Widgets:

| Widget | Meaning |
|---|---|
| Open claims | Number of active claims in the system. |
| New claims today | Claims received today from portal, email, WhatsApp, broker upload, branch scan, or API. |
| Claims awaiting human approval | Cases where AI has prepared work but a human must decide. |
| Incomplete claims | Claims missing required documents. |
| Average first response time | How quickly the insurer responds after claim notification. |
| SLA breaches | Claims or tasks past deadline. |
| AI summaries generated | Number of claim summaries prepared by AI. |
| Document completeness rate | Percentage of claims submitted with required documents. |
| Human override rate | How often humans disagree with AI recommendation. |
| Customer update backlog | Customers waiting for status updates. |
| Top claim categories | Motor, medical, fire, theft, life, agriculture, etc. |
| Channel breakdown | WhatsApp, email, web portal, broker upload, branch scan, API. |
| AI quality score | Groundedness, response quality, policy citation quality, escalation rate. |
| Cost dashboard | Azure OpenAI token usage, document processing cost, search queries, storage, monitoring. |

### 6.5 Claims Inbox dashboard

Fields:

| Field | Description |
|---|---|
| Claim ID | Unique claim reference. |
| Customer name | Policyholder or claimant. |
| Policy number | Linked policy. |
| Product line | Motor, medical, fire, life, etc. |
| Claim type | Accident, theft, injury, death, medical reimbursement, property damage. |
| Channel | Email, WhatsApp, web portal, broker, branch, API. |
| Status | New, missing documents, ready for review, escalated, settled, closed. |
| Completeness score | AI-assisted document completeness percentage. |
| Risk level | Low, medium, high, critical. Staff-only. |
| SLA timer | Time left before breach. |
| Assigned officer | User responsible. |
| AI confidence | Staff-only confidence in classification/extraction. |

### 6.6 Case Review dashboard

Sections:

| Section | Content |
|---|---|
| Case summary | AI-generated one-page claim summary. |
| Customer details | Name, phone, email, policyholder ID, address, broker. |
| Policy details | Policy number, cover type, start/end date, limits, exclusions. |
| Incident timeline | Reported date, incident date, upload times, officer actions. |
| Documents received | Claim form, photos, police abstract, invoice, medical report, etc. |
| Missing documents | Required but not yet submitted. |
| Extracted fields | AI-extracted document fields with confidence scores. |
| Policy references | Clauses or checklist sections used by AI. |
| Recommended next action | Request document, assign assessor, escalate, prepare review. |
| Risk signals | High value, bodily injury, suspicious duplicate, vulnerable customer. Staff-only and not a fraud accusation. |
| Human decision panel | Approve draft, reject draft, override workflow recommendation, escalate, request more information. |
| Audit history | Every AI and human action. |

Approval decisions update workflow state only. They do not approve settlement, deny, repudiate, pay, or accuse fraud unless a separately authorized human workflow explicitly supports that action.

### 6.7 Document Checker dashboard

Sections:

| Section | Content |
|---|---|
| Upload panel | Drag and drop PDFs, images, scans, email attachments. |
| Document classification | Claim form, police abstract, medical invoice, garage estimate, ID, etc. |
| Extraction results | Extracted fields and confidence. |
| Completeness checklist | Required vs received documents. |
| Missing fields | Missing policy number, date, signature, invoice amount, etc. |
| Validation warnings | Mismatched policy number, expired policy, duplicate invoice, poor scan. |
| Next action | Ask customer, route to officer, accept document, reject unclear document. |

### 6.8 AI Assistant dashboard

Assistant modes:

| Mode | Function |
|---|---|
| Policy Q&A Agent | Answers questions from approved policy documents only. |
| Claims Intake / FNOL Agent | Guides first notice of loss and creates a claim case. |
| Document Checker Agent | Checks document completeness and extracts fields. |
| Routing Agent | Applies assignment and escalation rules. |
| Customer Update Agent | Drafts SMS, email, WhatsApp, or portal updates. |
| Complaint Agent | Summarizes complaint and proposes response route. |
| Underwriting Agent | Future: checks proposal form completeness. |
| Invoice Agent | Future: validates provider invoices and payment packs. |
| Fraud Triage Agent | Future: flags suspicious patterns but does not accuse or reject. |

Guardrails:

- answer only from approved knowledge where policy wording matters;
- cite the source document or section;
- show confidence in staff surfaces;
- escalate uncertainty;
- avoid final claim denial;
- avoid final underwriting rejection;
- avoid fraud accusations;
- avoid giving confidential internal notes to customers;
- log every answer.

### 6.9 Knowledge Base dashboard

Content types:

- policy wordings;
- product brochures;
- claims checklists;
- underwriting rules;
- customer FAQs;
- broker FAQs;
- approved customer message templates;
- escalation rules;
- regulatory guidance;
- claims procedure manuals;
- complaint handling procedures.

### 6.10 Customer Service dashboard

Sections:

| Section | Description |
|---|---|
| Customer search | Search by name, phone, policy number, claim number. |
| Recent interactions | Calls, emails, WhatsApp messages, portal chats. |
| Claim status | Current stage and next action. |
| Policy summary | Simple explanation of cover. |
| AI suggested response | Draft answer grounded in approved documents. |
| Escalation panel | Send to claims, supervisor, complaint team. |
| Communication history | What was sent to the customer and when. |

### 6.11 Client Portal Preview / customer-safe external status surface

This surface is optional and must be treated as customer-facing. It may show only approved customer-safe information.

Sections:

| Section | Description |
|---|---|
| Claim tracker | Simple status timeline. |
| Required documents | What is needed and what has been received. |
| Upload documents | Upload photo, PDF, scan. |
| Policy questions | Ask what is covered, grounded in approved sources. |
| Messages | Updates approved by insurer. |
| Complaint/escalation | Raise complaint or request human help. |

Never expose internal risk signals, fraud flags, reserves, prompts, hidden notes, staff-only audit details, or confidence data here.

### 6.12 Compliance and Audit dashboard

Sections:

| Section | Description |
|---|---|
| AI action log | Every AI action, output, source, and confidence score. |
| Human override log | Where humans changed AI recommendations. |
| Approval history | Who approved what and when. |
| Complaints report | Complaint ageing, category, resolution status. |
| Claim repudiation review | Denied/rejected claims requiring evidence pack. |
| Data access log | Who accessed sensitive records. |
| Bias/fairness indicators | Monitor by region, channel, language, product, gender, etc. |
| Regulatory export | Reports for management or regulator. |

### 6.13 IT / Security / Admin dashboard

Sections:

| Section | Description |
|---|---|
| Tenant settings | Company, environment, region, plan. |
| Users and roles | RBAC permissions. |
| Identity | Entra ID configuration. |
| Integrations | Outlook, Teams, SharePoint, Dynamics, APIs, SMS, WhatsApp. |
| Secrets and keys | Key Vault references. |
| Model settings | Azure OpenAI/Foundry model deployment, token limits, safety settings. |
| Search settings | AI Search indexes, vector configs, reindexing. |
| Document AI settings | Document Intelligence models. |
| Monitoring | App Insights, Log Analytics, health checks. |
| Network security | Private endpoints, firewall, allowed IPs. |
| Data retention | Case retention, logs, documents, audit exports. |

---

## 7. Dashboard menu structure

Build the left navigation around this structure:

```text
IIP Operations
|
|-- Command Center
|-- Claims Inbox
|-- New Claim Intake
|-- Case Review
|-- Document Checker
|-- AI Assistant
|-- Customer Service
|-- Client Portal Preview
|-- Knowledge Base
|-- Approvals
|-- Compliance & Audit
|-- Reports & Analytics
|-- Integrations
|-- Admin Settings
|-- Support & Health
```

The portal must remain role-aware. Hide or restrict screens and actions that a role cannot use, but do not rely on frontend logic as the source of authority.

---

## 8. Core workflows

### Workflow 1: Claim submission

```text
Customer / broker submits claim
-> Platform captures FNOL information
-> Documents uploaded
-> Documents stored in Blob Storage
-> Document Intelligence extracts fields
-> System checks completeness
-> AI Search retrieves policy/checklist rules
-> Azure OpenAI / Foundry creates case summary
-> Routing agent assigns case
-> Claims officer reviews
-> Customer receives approved update
-> Audit log is stored
```

### Workflow 2: Policy question

```text
Customer or staff asks question
-> System identifies product/policy
-> Azure AI Search retrieves approved policy sections
-> AI drafts answer with citations
-> If low-risk and configured, answer may be sent through approved channel
-> If uncertain or disputed, route to human
-> Answer and sources are logged
```

### Workflow 3: Missing document request

```text
Document checker finds missing item
-> AI drafts customer-safe message
-> Officer approves unless explicitly low-risk/configured for auto-send
-> Message goes through configured Outlook, SMS, WhatsApp, or portal channel
-> Customer uploads missing file
-> Claim completeness score updates
```

### Workflow 4: Human approval

```text
AI prepares recommendation
-> System detects consequential action
-> Creates human approval task
-> Supervisor reviews case, evidence, sources
-> Supervisor approves, rejects, or overrides
-> Decision is logged
-> Customer-safe update is sent only when approved/configured
```

### Workflow 5: Compliance review

```text
Compliance officer opens audit dashboard
-> Filters by claim type, officer, AI agent, date, decision type
-> Reviews AI output + source documents + human action
-> Exports audit pack
-> Uses report for internal governance or regulator response
```

---

## 9. AI agents specification

### 9.1 MVP agents

| Agent | Purpose | Human approval needed? |
|---|---|---|
| Policy Q&A Agent | Answers policy questions using approved documents. | Yes for disputed, unclear, sensitive, or high-risk answers. |
| FNOL Intake Agent | Collects first notice of loss details and creates intake record. | No for intake; yes for consequential action. |
| Document Checker Agent | Classifies and extracts uploaded documents; checks completeness. | Human can correct extraction and approve fields. |
| Routing Agent | Assigns cases based on deterministic rules and assisted classification. | Supervisor approval for high-risk routing/escalation. |
| Customer Update Agent | Drafts status and missing-document messages. | Auto-send only for low-risk configured messages; approval for sensitive cases. |

### 9.2 Version 2 agents

| Agent | Purpose |
|---|---|
| Underwriting Submission Agent | Reads proposal forms, checks missing risk data, creates referral notes. |
| Provider Invoice Agent | Validates hospital/garage/provider invoices against claim records. |
| Broker Copilot | Helps brokers explain products and submit complete applications. |
| Complaint Response Agent | Summarizes complaints and drafts regulated responses. |

### 9.3 Version 3 agents

| Agent | Purpose |
|---|---|
| Fraud Triage Agent | Flags suspicious patterns but does not accuse or deny. |
| Regulatory Reporting Agent | Creates compliance and claims reports. |
| Microinsurance Agent | Supports simple mobile-first insurance journeys. |
| Agriculture Insurance Agent | Explains weather-index cover, payout triggers, farmer claim steps. |

---

## 10. Core data model specification

Create or maintain these minimum database objects. Use typed entities, migrations, and DTOs. Field names can be idiomatic for the chosen language, but preserve the meaning.

Required entities:

- `Tenant`
- `Workspace`
- `User`
- `Role`
- `Customer`
- `Policy`
- `Case`
- `CaseParty`
- `CaseEvent`
- `CaseTask`
- `CaseNote`
- `CaseDocument`
- `DocumentVersion`
- `DocumentExtraction`
- `CompletenessCheck`
- `KnowledgeDocument`
- `KnowledgeChunk`
- `KnowledgeQuery`
- `AgentActionLog`
- `AgentRun`
- `AgentRunStep`
- `HumanReview`
- `Approval`
- `Communication`
- `CustomerUpdate`
- `AuditEvent`
- `IntegrationConnection`
- `WebhookEvent`
- `UsageMeter`
- `NotificationDraft`

### Minimum case types

- claim;
- policy question;
- service request;
- complaint;
- broker submission;
- underwriting submission placeholder.

### Minimum case statuses

- new;
- awaiting_documents;
- extraction_in_progress;
- ready_for_review;
- routed;
- pending_approval;
- customer_update_pending;
- closed;
- escalated;
- failed.

---

## 11. Permissions model

Implement RBAC with role, branch, product line, case assignment, and workspace scope.

| Feature | Admin | Claims Officer | Supervisor | Customer Service | Broker | Compliance | Client |
|---|---:|---:|---:|---:|---:|---:|---:|
| View command center | Yes | Limited | Yes | Limited | No | Yes | No |
| Create claim | Yes | Yes | Yes | Yes | Yes | No | Yes |
| Upload document | Yes | Yes | Yes | Yes | Yes | No | Yes |
| View all claims | Yes | No | Yes | No | No | Yes | No |
| View assigned claims | Yes | Yes | Yes | Yes | Own only | Yes | Own only |
| Approve workflow progression | Yes | Limited | Yes | No | No | Review only | No |
| Approve settlement/payment | Separate authorized workflow only | No | Separate authorized workflow only | No | No | Review only | No |
| Deny/repudiate claim | Separate authorized workflow only | No | Separate authorized workflow only | No | No | Review only | No |
| View audit log | Yes | Limited | Yes | No | No | Yes | No |
| Manage knowledge base | Yes | No | Limited | No | No | Yes | No |
| Configure integrations | Yes | No | No | No | No | No | No |
| View AI outputs | Yes | Yes | Yes | Yes | Limited | Yes | Limited |
| Override AI recommendation | Yes | Limited | Yes | No | No | Review only | No |

Authorization tests must cover cross-tenant access, cross-branch access where applicable, unassigned-claim access, and customer-safe boundaries.

---

## 12. Microsoft and Azure integrations

Design integrations around interfaces, adapters, events, and feature flags. Do not hardwire business logic to a vendor SDK.

### 12.1 Must-have for MVP

1. Microsoft Entra ID
2. Azure Managed Applications / Marketplace
3. Azure App Service or Container Apps, default App Service
4. Azure Functions / Durable Functions
5. Azure Blob Storage
6. Azure AI Document Intelligence
7. Azure AI Search
8. Azure OpenAI
9. Azure SQL, default transactional store
10. Azure Service Bus
11. Azure Key Vault
12. Managed Identity
13. Azure Monitor / Application Insights / Log Analytics
14. Outlook / Exchange Online via Microsoft Graph

### 12.2 Strong V1 additions

15. Teams notifications
16. Azure Communication Services SMS/email
17. SharePoint import/export
18. Power BI reports

### 12.3 Version 2 additions

19. Dynamics 365 / Dataverse
20. Power Automate / Logic Apps
21. Microsoft Purview
22. Microsoft Fabric
23. Advanced Teams app with approvals
24. Foundry Agent Service observability/evaluations

### 12.4 Outlook / Exchange Online integration

Build in V1.

Use it for:

- reading claim emails from shared mailboxes;
- extracting email attachments;
- creating cases from emails;
- sending customer updates;
- sending missing-document requests;
- saving email conversation history;
- categorizing claim emails;
- creating draft replies for human approval;
- monitoring inbox changes through webhooks/change notifications.

Implementation rules:

- Use Microsoft Graph Mail API.
- Support tenant-configurable insurer mailboxes.
- Support shared mailboxes using application permissions where appropriate.
- Restrict mailbox access through Exchange Online RBAC for Applications or an explicitly approved mailbox-scoping alternative.
- Store Graph subscription/webhook metadata safely.
- Download attachments into Blob Storage.
- Link email thread IDs to the case.
- Draft replies should require approval unless the message is low-risk and explicitly configured for auto-send.
- Never expose confidential internal notes in outbound email.

---

## 13. Repository structure to create or maintain

Prefer this structure:

```text
repo/
  AGENTS.md
  CLAUDE.md
  README.md
  IIP_IMPLEMENTATION_GUARDRAILS.md
  InsuranceIntelligence.sln

  src/
    InsuranceIntelligence.Api/
    InsuranceIntelligence.Functions/
    InsuranceIntelligence.Domain/
    InsuranceIntelligence.Application/
    InsuranceIntelligence.Infrastructure/
    InsuranceIntelligence.Ai/
    InsuranceIntelligence.Documents/
    InsuranceIntelligence.Integrations/
    InsuranceIntelligence.Telemetry/
    InsuranceIntelligence.Tests/

  web/
    portal/
      app/
      components/
      lib/
      tests/

  infra/
    mainTemplate.bicep
    createUiDefinition.json
    viewDefinition.json
    modules/
    parameters/
    dist/

  docs/
    strategy-requirements-roadmap/
    development-engineering/
    design-architecture/
    testing-security-quality/
    deployment-release-enablement/
    client-pilot/
    client-support/
    continuous-improvement/
    commercial-executive/
    ui/
    architecture/
    api/
    adr/
    runbooks/
    security/
    marketplace/
    production/
    testing/

  scripts/
    dev/
    infra/
    seed/
    smoke-tests/
```

If an existing repository differs, adapt the instructions but preserve the same separation: domain, application, infrastructure adapters, API, functions, portal, infra, docs, tests.

---

## 14. Coding principles

1. Build thin vertical slices, not disconnected scaffolding.
2. Keep domain logic free of Azure SDK dependencies.
3. Put Azure SDK usage behind interfaces in infrastructure/adapters.
4. Retain test doubles only inside automated tests or isolated `LocalTest` execution; deployable staging/production/reviewer code paths must bind only real Azure/Microsoft providers and fail closed when unavailable.
5. Add tests with every non-trivial change.
6. Keep PRs small and reviewable.
7. Prefer explicit names over clever abstractions.
8. Make failure states visible to operators.
9. Make long-running work retryable and idempotent.
10. Log structured events, not ad-hoc strings.
11. Every public endpoint must have validation, authorization, and auditable outcomes.
12. Every AI tool/action must have a typed input/output contract.
13. Do not silently swallow exceptions.
14. Do not make external network calls from tests unless explicitly marked integration tests.
15. Use feature flags for unfinished modules.
16. Do not create a generic chatbot as the center of the UI; the platform is an operations dashboard with embedded AI assistance.

---

## 15. API surface

Implement OpenAPI-documented endpoints. Prefer the current API Specification and backend contract map as source of truth. Start from existing routes and keep backward compatibility unless a maintainer approves a breaking change.

Required endpoint groups:

- Health and metadata
- Tenant onboarding and admin
- Case intake and workspace
- Documents
- Knowledge assistant and knowledge base
- Agent actions
- Approvals
- Communications
- Audit, reports, and telemetry
- Integrations
- Outlook / Graph integration
- Voice/call module - future, feature-flagged only

Every endpoint must enforce:

- authenticated user context;
- server-side authorization;
- tenant/workspace scope;
- input validation;
- safe error output;
- audit logging where it changes state or exposes sensitive workflow information.

---

## 16. Workflow orchestrations

Use Durable Functions or clear equivalent orchestration patterns for long-running work.

### 16.1 MVP orchestrations

- `DocumentIngestionOrchestrator`
- `KnowledgeIndexOrchestrator`
- `CaseSummaryOrchestrator`
- `CaseRoutingOrchestrator`
- `CustomerUpdateDraftOrchestrator`
- `OutlookMailboxIngestionOrchestrator`
- `UsageMeteringOrchestrator`

Each orchestration must be:

- idempotent;
- retry-aware;
- auditable;
- tenant/workspace scoped;
- safe when a provider is missing or unhealthy;
- never silently downgraded to fake provider behavior in deployed runtime.

### 16.2 Future voice/call orchestration

The voice module should use an adapter pattern around Azure Communication Services Call Automation or another approved telephony provider. Voice is future and feature-flagged.

A voice agent may collect intake information and create/update cases. A voice agent must not make final claim decisions, disclose sensitive information without identity verification, or continue when caller asks for a human or confidence is low.

---

## 17. AI implementation rules

### 17.1 Provider interfaces

Create provider interfaces before concrete adapters:

- `IChatCompletionProvider`
- `IEmbeddingProvider`
- `IRetrievalProvider`
- `IDocumentExtractionProvider`
- `IContentSafetyProvider`
- `IAgentToolExecutor`
- `IAgentRunRepository`
- `IAuditWriter`
- `IUsageMeterWriter`
- `IEmailProvider`
- `INotificationProvider`
- `IWorkflowRouter`

Concrete real adapters:

- `AzureOpenAiChatCompletionProvider`
- `AzureAiSearchRetrievalProvider`
- `AzureDocumentIntelligenceProvider`
- `MicrosoftGraphMailProvider`
- `AzureCommunicationServicesProvider`
- `TeamsNotificationProvider`
- `FoundryAgentProvider` if needed later.

Mock/test adapters must remain isolated to automated tests or non-deployable `LocalTest` harnesses.

### 17.2 Prompt rules

Store prompts in versioned files, not scattered string literals.

Each prompt must include:

- task objective;
- allowed inputs;
- forbidden actions;
- required output schema;
- grounding requirements;
- escalation conditions;
- safety instructions;
- prompt version.

### 17.3 RAG rules

The knowledge assistant must:

1. authenticate user and tenant/workspace;
2. apply tenant/workspace search filters;
3. retrieve approved knowledge chunks only;
4. respect effective date and expiry date;
5. pass only relevant snippets to the model;
6. return concise answers with evidence;
7. refuse unsupported answers;
8. log evidence IDs and answer metadata;
9. capture thumbs-up/down, correction, or override feedback.

### 17.4 Structured outputs

Use typed JSON outputs for:

- extraction results;
- completeness checks;
- case summaries;
- recommended next actions;
- customer update drafts;
- routing recommendations;
- fraud/severity signals when implemented later.

Validate model JSON before storing or displaying it.

---

## 18. Document intelligence

### 18.1 MVP supported document families

Start with generic schemas for:

- claim form;
- policy schedule;
- national ID/passport placeholder;
- police report / police abstract;
- medical note;
- invoice/receipt;
- repair quotation / garage estimate;
- broker submission;
- free-form supporting document.

### 18.2 Extraction result must include

- extracted fields;
- confidence by field;
- source page/region if available;
- warnings;
- missing required fields;
- document type classification;
- raw provider metadata stored separately;
- audit event.

### 18.3 Completeness rules

Implement configurable rules by case type and product line. Rules must be tenant-configurable and versioned.

Example:

```yaml
claim_motor:
  required_documents:
    - claim_form
    - policy_schedule
    - police_report
    - photos_or_assessment
  required_fields:
    - claimant_name
    - policy_number
    - incident_date
    - incident_location
```

---

## 19. Portal UX and enterprise corporate interface standard

The portal is a core product surface. It must be a calm, premium, enterprise-grade insurance operations workspace driven by real backend API contracts.

### 19.1 Mandatory design references

Before changing any frontend page, layout, component, styling token, role-aware navigation, AI surface, customer portal surface, integration state, approval workflow, or API-to-UI mapping, read these files in full:

- `docs/ui/IIP_Operations_UI_Design_Specification_MVP.md`
- `docs/ui/reference/IIP_Global_Theme_Approved.png`
- Design System & UX Behavior Specification
- UI/UX Handoff Document
- Frontend Implementation Guide
- Component Architecture Document

These files are the approved authority for the IIP Operations user interface.

### 19.2 Fidelity requirement

The implementation must match the approved global Light Intelligence Workspace Theme:

- bright white and soft-lavender workspace surfaces;
- deep ink-navy typography;
- restrained indigo-to-violet brand and AI accents;
- light sidebar and light header;
- structured right-side execution or AI insight rail;
- compact operational tables;
- subtle borders, shadows and rounded cards;
- no dark sidebar in MVP;
- no unapproved colour palette changes;
- no improvised styling direction.

Do not reinterpret, modernise, simplify, or restyle the approved theme without an explicit user-approved specification update.

### 19.3 Frontend/backend contract rule

Before rebuilding or changing screens:

- inspect actual API routes, DTOs, enums, RBAC, tenant/workspace handling, provider health, and error contracts;
- produce and maintain `docs/ui/backend-contract-map.md`;
- connect each screen to real implemented endpoints;
- never invent operational records, statuses, metrics, mailboxes, customers, approvals, tasks, communications, extraction output, or health state in the deployable UI;
- clearly show `Not available` / `Not configured` when a real capability is not implemented or configured;
- add only minimal backward-compatible, tested backend changes required for accurate UI.

### 19.4 Enterprise visual quality

The production UI must be:

- minimalistic, clean, modern, and restrained;
- suitable for insurance executives, claims managers, compliance teams, and IT administrators;
- operations-first rather than chatbot-first;
- dense enough for real work, but readable on standard laptop screens;
- visually coherent across every route.

Do not use:

- gradients/glassmorphism-heavy dashboards;
- marketing-page or playful consumer design patterns;
- text overflow, overlapping elements, clipped labels, broken responsive layouts, or unreadable tables;
- raw JSON on ordinary screens;
- hardcoded real insurer names/users/mailboxes;
- chatbot framing as the application centre;
- fake dashboard statistics or demo workflow records in deployed environments.

### 19.5 UI quality assurance requirements

Before deployment, test major routes at common desktop and narrower viewport widths and fix:

- overlaps;
- clipped content;
- overflowing text;
- incorrect z-index/modals;
- unreadable table layouts;
- broken navigation;
- inconsistent empty/loading/error states;
- browser console errors;
- accessibility issues in labels/focus/navigation.

Add automated frontend tests where practical for critical security/rendering cases; at minimum provide documented manual evidence.

### 19.6 Customer-safe and sensitive UI boundaries

- Show evidence beside AI output.
- Show confidence/escalation only where appropriate for staff workflows.
- Make human approval explicit.
- Show missing documents and SLA risk clearly.
- Keep destructive actions behind confirmation.
- Do not expose prompts, raw model/system output, provider exception payloads, tokens, secrets, internal notes, fraud flags, reserves, confidential compliance content, other tenants' records, or raw email content to unauthorized roles.
- Render all untrusted content safely; do not use unsafe HTML insertion for email bodies, document text, timeline content, communications, customer-provided text, or AI answers.

---

## 20. Azure Managed Application packaging

### 20.1 Required artifacts

The infra folder must produce:

- `infra/mainTemplate.bicep`
- `infra/dist/mainTemplate.json`
- `infra/createUiDefinition.json`
- `infra/viewDefinition.json`
- `infra/dist/managedapp.zip`

The managed app package must include at minimum:

- `mainTemplate.json`
- `createUiDefinition.json`
- optional `viewDefinition.json`

### 20.2 Authoring rule

Author infrastructure in Bicep, but compile to ARM JSON for the managed application package.

### 20.3 Parameters to expose

`createUiDefinition.json` should collect:

- deployment region;
- environment name;
- insurer/tenant display name;
- SKU size profile: dev, pilot, production;
- business lines enabled;
- support access mode: none, read-only, delegated, JIT;
- private networking enabled/disabled;
- AI provider mode for deployable instances: Azure OpenAI or approved Foundry deployment only;
- data residency mode;
- document retention days;
- audit retention days;
- alert email/group;
- Outlook/shared mailbox integration enabled/disabled;
- real SMS/email module enabled/disabled; disabled means `Not configured`, never simulated;
- optional Teams notifications enabled/disabled;
- optional SharePoint sync enabled/disabled;
- optional voice module enabled/disabled;
- optional WhatsApp module enabled/disabled.

### 20.4 Azure resources to include

MVP managed app deployment should include:

- App Service Plan;
- App Service for API;
- Function App;
- Storage Account;
- Azure SQL Database;
- Service Bus namespace and queues/topics;
- Azure AI Search;
- Azure AI Document Intelligence account or parameterized existing resource;
- Azure OpenAI or Foundry Agent Service account/deployment or parameterized existing resource;
- Key Vault;
- Managed identities;
- Application Insights;
- Log Analytics workspace;
- Azure Monitor alerts;
- API Management optional for dev, enabled for production;
- private endpoints optional but production-ready;
- budget/alert configuration where supported.

---

## 21. Local testing, development, and deployability boundary

### 21.1 Test doubles are not deployed product capability

Test doubles may exist only inside automated test projects or isolated non-deployable `LocalTest` execution. They must not be selectable or active in cloud staging, production, customer, or Partner Center reviewer deployments.

Allowed only for local automated tests:

- fake AI/chat/embedding provider;
- fake document extraction provider;
- fake retrieval/index provider;
- fake Microsoft Graph/email provider;
- fake notification/communication provider;
- in-memory repository or SQLite test database;
- synthetic fixture data.

Forbidden in deployable production runtime and frontend:

- mock/simulated provider registrations;
- fake buttons, fake success messages, role switchers, or seeded-data controls;
- fallback-to-mock on provider errors;
- runtime configuration that allows staging/production/reviewer environments to activate fake providers.

### 21.2 Required verification commands

Maintain these commands or update this section when repository commands change:

```bash
# backend
dotnet restore InsuranceIntelligence.sln
dotnet build InsuranceIntelligence.sln --no-restore
dotnet test InsuranceIntelligence.sln
dotnet list InsuranceIntelligence.sln package --vulnerable --include-transitive

# portal
cd web/portal
pnpm install --frozen-lockfile
pnpm audit
pnpm lint
pnpm build
cd ../..

# infrastructure
az bicep build --file infra/mainTemplate.bicep --outfile infra/dist/mainTemplate.json
az bicep build-params --file infra/parameters/stg.bicepparam --outfile infra/dist/stg.parameters.json
```

Never hardcode machine-specific pnpm store paths in CI/CD or deployment instructions.

---

## 22. Testing expectations

### 22.1 Minimum tests

- Domain unit tests for cases, statuses, routing, approvals, and completeness rules.
- API tests for authorization, validation, and common endpoints.
- AI provider contract tests using isolated test providers.
- Retrieval tests verifying tenant/workspace filters and approved-source-only behavior.
- Document extraction tests using sample JSON fixtures.
- Outlook ingestion tests using synthetic email fixtures.
- Durable orchestration tests where practical.
- Portal component tests for critical workflows.
- Bicep build validation.
- OpenAPI generation/validation.
- Security tests for no secrets in sample config.
- Regression tests for hallucination guardrails: missing evidence must produce refusal/escalation.
- Customer-safe view tests ensuring internal risk notes/fraud flags are not exposed.

### 22.2 Test data

Use synthetic data only. Do not include real customer, medical, claim, policy, payment, broker, or national ID data in the repository.

---

## 23. Telemetry and audit

Every agent-assisted workflow must write an `AuditEvent`.

### 23.1 Minimum audit fields

- event ID;
- tenant/workspace ID;
- case/document/user ID where relevant;
- actor type: user, system, agent, integration;
- action name;
- timestamp;
- before/after status where relevant;
- evidence IDs;
- model/provider metadata if AI was used;
- confidence and validation result;
- approval ID if created;
- correlation ID;
- safe IP/device metadata only if configured.

### 23.2 Minimum telemetry classes

- platform health;
- workflow health;
- AI quality;
- business outcome;
- commercial usage.

### 23.3 Track usage meters

- cases created;
- documents processed;
- pages processed where available;
- knowledge queries;
- AI agent runs;
- AI summaries generated;
- outbound messages;
- Outlook emails processed;
- customer update drafts;
- voice call minutes later;
- premium feature jobs later.

---

## 24. Integration strategy

Design integrations around interfaces and events.

### 24.1 Core integration domains

- CRM;
- policy administration system;
- claims core system;
- billing/payment system;
- messaging providers;
- identity providers;
- Microsoft 365 services.

### 24.2 Integration principles

- API-first, but support file import/export for insurers with legacy systems.
- Use APIM for external API governance.
- Use Service Bus for async handoff and retries.
- Preserve external IDs on cases, policies, documents, communications, and customers.
- Never block MVP on a perfect core-system integration.
- Store integration configuration per tenant.
- Never log OAuth tokens or client secrets.
- If an integration is not configured, show `Not configured`; never simulate success in deployed environments.

---

## 25. Security specifications and production hardening

The production platform must include:

- Microsoft Entra ID authentication and server-side authorization;
- RBAC by role, branch/product scope and case assignment;
- tenant/workspace isolation on every data access path;
- managed identities for Azure-to-Azure access wherever possible;
- Azure Key Vault references for secrets and certificates;
- HTTPS-only access;
- strict non-development CORS allow-lists;
- production-compatible security headers;
- encryption at rest and in transit;
- audit logging for AI and human actions;
- configurable retention and export controls;
- controlled publisher support access, preferring least privilege and JIT where selected;
- no autonomous final adverse insurance decisions;
- zero mock or simulated runtime behavior in staging, production or reviewer deployments.

### 25.1 Mandatory security release blockers

Do not proceed to a production deployment while any of these remain unresolved:

- missing or invalid demo role grants `Admin` or elevated access;
- demo auth headers accepted outside explicit non-deployable test mode;
- unauthorized roles can approve, send, access audit/compliance, configure integrations, manage knowledge, or cross tenant/workspace boundaries;
- secrets, tokens, connection strings, SAS tokens, prompts, system messages, internal notes, fraud flags, or reserves exposed through frontend/API/logs;
- unsafe HTML/XSS rendering path for untrusted email/document/customer/AI content;
- unsafe file upload handling;
- wildcard/permissive production CORS for sensitive endpoints;
- required live provider unhealthy or configured as mock/simulation/fallback;
- customer-facing UI relies on hardcoded records.

### 25.2 UI/backend security audit deliverable

Produce and maintain:

```text
docs/security/ui-backend-security-review.md
```

It must record scope, findings, severity, code evidence, fix, tests, residual risk, and follow-up actions for:

- identity/authentication and demo-header impersonation;
- server-side permissions and IDOR/tenant isolation;
- secrets and sensitive-output exposure;
- XSS/unsafe rendering;
- CORS/security headers;
- file upload validation;
- dependency vulnerabilities;
- Microsoft Graph/mailbox configuration and access isolation;
- provider-health status safety.

### 25.3 Security checklist for every feature

Before declaring a feature deployable:

- enforce tenant/workspace scoping server-side;
- enforce role/permission server-side;
- use real provider or intentionally show `Not configured`;
- record audit events;
- avoid sensitive data in UI/logs/errors;
- add tests for authorization and safe output;
- ensure deployed config does not enable fakes;
- document any manual production setup required.

---

## 26. Marketplace and managed operations notes

The product must be ready for:

- internal service catalog deployment first;
- private customer deployment second;
- Azure Marketplace Managed Application later.

Managed operations must support:

- read-only publisher access by default;
- JIT support access where configured;
- clear customer ownership of data;
- logs and metrics for support;
- reindex-from-source runbook;
- backup/restore runbooks;
- incident runbook;
- metering reconciliation runbook;
- versioned package releases.

Support dashboards must include:

- deployment health;
- API health;
- Function health;
- queue backlog;
- search health;
- document extraction failures;
- AI provider failures;
- token and document processing usage;
- support access status;
- failed integrations.

---

## 27. Architecture decision records

For significant decisions, add ADRs under `docs/adr/`.

Required ADRs:

- `0001-runtime-baseline-appservice-functions.md`
- `0002-human-supervised-decision-boundary.md`
- `0003-tenant-isolation-model.md`
- `0004-rag-grounding-and-evidence.md`
- `0005-managed-application-packaging.md`
- `0006-outlook-graph-ingestion.md`
- `0007-role-based-dashboard-boundaries.md`
- `0008-voice-module-boundaries.md` when voice starts.

Each ADR should include:

- context;
- decision;
- alternatives considered;
- consequences;
- implementation notes.

---

## 28. Environment profiles and configuration rules

Do not hardcode secrets. Do not commit runtime secret values. Use Key Vault references, managed identities, secured pipeline variables, or approved secret stores.

### 28.1 Production/reviewer configuration requirement

`Staging`, `Production`, and `ManagedApplicationReviewer` must set real-provider configuration only:

```bash
INSURANCEINTELLIGENCE_RUNTIME_PROFILE=Staging|Production|ManagedApplicationReviewer
INSURANCEINTELLIGENCE_AUTH_MODE=Entra
INSURANCEINTELLIGENCE_STORAGE_PROVIDER=SqlServer
INSURANCEINTELLIGENCE_BLOB_PROVIDER=AzureBlob
INSURANCEINTELLIGENCE_DOCUMENT_PROVIDER=AzureDocumentIntelligence
INSURANCEINTELLIGENCE_RETRIEVAL_PROVIDER=AzureAiSearch
INSURANCEINTELLIGENCE_AI_PROVIDER=AzureOpenAI
INSURANCEINTELLIGENCE_QUEUE_PROVIDER=AzureServiceBus
INSURANCEINTELLIGENCE_EMAIL_PROVIDER=Graph
INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER=<RealConfiguredProviderOrDisabled>
INSURANCEINTELLIGENCE_SEED_DEMO_DATA=false
INSURANCEINTELLIGENCE_KEYVAULT_URI=<KeyVaultUri>
```

Use managed identity / Key Vault references for required endpoints, certificates, secrets, and connections. No production values or credentials belong in this repository.

### 28.2 LocalTest-only configuration

A `LocalTest` profile may use test doubles solely for automated/local test execution. It must be excluded from deployable production/reviewer package defaults and cannot silently activate under staging/production.

```bash
INSURANCEINTELLIGENCE_RUNTIME_PROFILE=LocalTest
# Test-only provider values may be configured in test harness/local uncommitted settings.
```

### 28.3 Configuration validation

Startup/readiness must:

- reject real deployment profiles using any mock/simulated/placeholder/in-memory/SQLite/development-auth/seeded-demo setting;
- return a safe configuration failure without revealing secret values;
- require real Graph/Outlook configuration for enabled email ingestion/outbound email;
- require configured real customer-notification providers or disable customer-notification functionality.

Local `.env` files must be ignored by Git. `.env.example` may contain placeholders only and must clearly distinguish non-deployable test-only configuration from real deployment requirements.

---

## 29. Definition of done and production truth standard

A normal engineering task is done only when:

1. code compiles;
2. tests and relevant validation pass, or blockers are explicitly documented;
3. tenant isolation is respected;
4. role-based access is enforced server-side;
5. customer-safe boundaries are enforced;
6. AI outputs are grounded, validated and auditable;
7. consequential decisions remain human-approved;
8. OpenAPI/docs/runbooks are updated if behavior changed;
9. telemetry/audit events exist for workflow/provider changes;
10. infrastructure changes compile to ARM JSON where applicable;
11. no secrets or real sensitive data are committed;
12. production UI is backed by real APIs and contains no hardcoded operational records;
13. no text overflow, overlap, broken layout, unsafe rendering or unhandled main-screen error remains.

The platform may be stated as **PRODUCTION-CANDIDATE** only when:

- staging runs only real mandatory providers;
- Entra authentication and authorization are enabled and demo headers rejected;
- real Azure SQL/Blob/Service Bus/AI Search/Document Intelligence/Azure OpenAI/Microsoft Graph are configured, healthy and tested end to end with synthetic data;
- monitoring, alerting, backup/restore and rollback evidence exists;
- security validation has no unresolved critical/high deployment blocker;
- release acceptance report is complete.

The platform may be stated as **Managed Application / Partner Center private-plan ready** only when the managed application ZIP, ARM/UI definitions, test deployment evidence, reviewer instructions, and readiness report have passed the gates in Section 31.

---

## 30. Codex working behavior

When asked to build or release:

1. Read this file, `CLAUDE.md`, `README.md`, `IIP_IMPLEMENTATION_GUARDRAILS.md`, deployment/security/UI documentation and existing test output before editing.
2. Inspect existing files, contracts and deployed configuration before implementation.
3. Preserve a recoverable Git checkpoint before discarding or replacing substantial work.
4. State a short execution plan and identify any manual Azure/Microsoft 365/Partner Center decisions needed.
5. Do not introduce new product features during production hardening unless expressly requested.
6. Prefer modifying existing paths over creating competing architectures or duplicate shells.
7. Remove mock/simulation from deployed backend/frontend runtime, but preserve isolated test doubles needed for automated testing.
8. Never create a fake production success path; if a real provider cannot be configured, mark the capability blocked or `Not configured` and report the manual requirement.
9. Execute gates in order and stop when a manual administrator action, permission, secret value, quota approval, DNS choice, Microsoft 365 consent, or Partner Center action is required.
10. Run relevant build/test/security/infra checks at every gate.
11. Report exactly what changed, what passed, what failed, what is configured live and what remains blocked.
12. Do not declare production readiness without end-to-end live-provider evidence and the release-acceptance report.

---

## 31. Production Release Program - execute gates in order

The foundational local implementation phases are considered complete or superseded. From this point, the required execution model is a gated production release program. Do not create new mock implementation phases. Do not skip gates merely to deploy faster.

### Gate 0 - Secure checkpoint, UI/backend alignment, and release baseline

Objective: produce a clean, secure, real-API-driven release baseline.

Required deliverables:

- Enterprise UI rebuild + security hardening mission complete;
- divergent chatbot-first UI removed where it conflicts with IIP Operations;
- hardcoded insurer/user/mock dashboard data removed from deployable frontend;
- backup branch/commit created before destructive cleanup;
- production interface meets Section 19 quality requirements;
- `docs/ui/backend-contract-map.md`;
- `docs/security/ui-backend-security-review.md`;
- `docs/production/gate-0-secure-baseline.md`;
- no critical/high security deployment blocker remains.

### Gate 1 - Real identity, authorization, configuration validation, and secrets

Objective: make production identity and configuration real.

Required deliverables:

- Microsoft Entra ID authentication for API and portal;
- server-derived production user/tenant/workspace/permission context;
- Development Auth/demo headers disabled for real environments;
- production environment validation from Section 0A;
- Key Vault references and managed identities;
- strict production CORS/HTTPS/security headers;
- `docs/production/authentication-and-rbac.md`;
- `docs/production/secrets-and-key-vault.md`.

Stop and report exact manual Entra/admin-consent values or actions required.

### Gate 2 - Real Azure staging / production-candidate deployment

Objective: deploy a real, synthetic-data-only, production-candidate staging environment.

Use:

```text
environmentName: stg
resourceGroupName: rg-iip-stg
runtimeProfile: Staging
```

No `Mock`, `Simulated`, `Placeholder`, `InMemory`, `SQLite`, `DevelopmentAuth`, or seeded-demo mode is permitted in staging parameters/runtime.

Required deliverables:

- `infra/parameters/stg.bicepparam` with real-provider-only defaults;
- staging deployment scripts/runbook;
- Azure output values template;
- `docs/production/staging-deployment-report.md`.

### Gate 3 - Activate and prove all real providers including Outlook

Objective: exercise real services in staging using synthetic test records only.

Enable and verify, one at a time:

1. Azure SQL persistence and restart survival.
2. Azure Blob document/knowledge storage.
3. Azure Service Bus workflow/event queues.
4. Azure AI Search approved-knowledge indexing/retrieval with tenant/workspace filtering.
5. Azure AI Document Intelligence using stream-capable document pipeline on synthetic PDFs/images.
6. Azure OpenAI / Foundry grounded question answering and customer-safe drafting.
7. Microsoft Graph Outlook/email with restricted mailbox access, inbound case creation/linking, attachment processing, approved outbound response, duplicate prevention, and audit evidence.

Required deliverables:

- `docs/production/live-provider-activation-report.md`;
- `docs/production/outlook-graph-activation.md`;
- provider-by-provider evidence and disable/rollback steps.

Do not proceed while a mandatory real provider is untested or unhealthy.

### Gate 4 - Operational reliability, observability, protection and CI/CD

Objective: ensure the deployment is operable and recoverable.

Required deliverables:

- Application Insights/Log Analytics telemetry and safe PII/secrets redaction;
- correlation IDs and provider/workflow failure visibility;
- production alerts for availability, HTTP failures, queue/DLQ, provider failures, Graph ingestion and cost thresholds;
- Blob protection/versioning/retention;
- Azure SQL backup/PITR and restore drill;
- Key Vault recovery settings;
- audit retention/export procedure;
- disaster recovery/rollback runbook with RPO/RTO;
- managed identity/RBAC/network posture review;
- OWASP-aligned security validation;
- secret-free CI/CD with staging approval/rollback and package artifact versioning;
- `docs/production/monitoring-and-alerts.md`;
- `docs/production/backup-and-disaster-recovery.md`;
- `docs/production/security-review-final.md`;
- `docs/production/cicd-release-runbook.md`.

### Gate 5 - End-to-end production-candidate release acceptance

Objective: prove the full live-service platform using synthetic data before packaging.

Run and document explicit PASS/FAIL acceptance for:

- Entra-authenticated staff login and role-based denial paths;
- case intake;
- real file upload/storage;
- real document extraction and completeness;
- real approved knowledge indexing/retrieval;
- grounded AI answer with citation and no-source escalation;
- workflow task and human approval;
- approved real customer email;
- real inbound Outlook claim email and attachment flow;
- audit pack and compliance view;
- admin/provider health;
- monitoring and alerts;
- database restart persistence;
- backup/restore evidence;
- cross-tenant denial tests;
- no secret/internal-data exposure;
- enterprise UI route/responsive/overflow/accessibility checks.

Required deliverables:

- `docs/production/release-acceptance-report.md`;
- explicit go/no-go checklist.

### Gate 6 - Azure Managed Application package and Partner Center private plan readiness

Objective: package only the validated real-service platform.

Required package artifacts:

```text
infra/dist/mainTemplate.json
infra/dist/createUiDefinition.json
infra/dist/viewDefinition.json
infra/dist/managedapp.zip
infra/dist/package-manifest.json
infra/dist/validation-report.md
```

Required ZIP root entries:

```text
mainTemplate.json
createUiDefinition.json
viewDefinition.json   # when used
```

The package and default deployment UI/parameters must enforce real-provider-only staging/production/reviewer configurations. It must not expose mock provider selection for customer/reviewer deployment.

Required Partner Center readiness docs:

- offer listing copy;
- plan configuration;
- technical configuration;
- support and operations;
- privacy/security notes;
- reviewer test instructions;
- screenshot checklist;
- private plan submission runbook;
- final READY / NOT READY report.

Prepare a private plan first. Do not state submission readiness until package validation and reviewer deployment validation pass.

### Required validation before final release claim

```bash
dotnet restore InsuranceIntelligence.sln
dotnet build InsuranceIntelligence.sln --no-restore
dotnet test InsuranceIntelligence.sln
dotnet list InsuranceIntelligence.sln package --vulnerable --include-transitive

cd web/portal
pnpm install --frozen-lockfile
pnpm audit
pnpm lint
pnpm build
cd ../..

az bicep build --file infra/mainTemplate.bicep --outfile infra/dist/mainTemplate.json
az bicep build-params --file infra/parameters/stg.bicepparam --outfile infra/dist/stg.parameters.json
```

For staging, additionally run real health and end-to-end synthetic workflow tests against Azure URLs.

### Final report format for production work

Report:

1. **Production Release Program Status** - completed gates, blocked gates, manual actions.
2. **Azure Resources and Deployment** - resources, environment, URLs, provider modes, identities/RBAC.
3. **Identity, Security and Compliance** - Entra, demo-header disabled status, Key Vault, isolation, vulnerabilities/residual risks.
4. **Real Integrations** - Azure SQL, Blob, Service Bus, AI Search, Document Intelligence, Azure OpenAI/Foundry, Outlook/Graph and evidence.
5. **Operations and Reliability** - monitoring, alerts, backups/restore, CI/CD, rollback, cost controls.
6. **Managed Application / Partner Center** - ZIP/package readiness, private-plan readiness, manual Partner Center actions.
7. **Release Decision**:
   - `PRODUCTION-CANDIDATE: YES/NO`
   - `MANAGED APPLICATION PACKAGE READY: YES/NO`
   - `PARTNER CENTER PRIVATE PLAN READY: YES/NO`
   - remaining blockers;
   - exact next manual portal/CLI action.

---

## 32. Source links for maintainers

Use official documentation when implementing provider-specific adapters. Important references:

- Codex `AGENTS.md` guidance: https://developers.openai.com/codex/guides/agents-md
- Azure Managed Applications overview: https://learn.microsoft.com/en-us/azure/azure-resource-manager/managed-applications/overview
- Azure Document Intelligence overview: https://learn.microsoft.com/en-us/azure/ai-services/document-intelligence/overview
- Microsoft Purview overview: https://learn.microsoft.com/en-us/purview/purview
- Managed identities overview: https://learn.microsoft.com/en-us/entra/identity/managed-identities-azure-resources/overview
- Microsoft Graph Mail API overview: https://learn.microsoft.com/en-us/graph/api/resources/mail-api-overview
- Teams bots overview: https://learn.microsoft.com/en-us/microsoftteams/platform/bots/what-are-bots
- Microsoft Graph Files API: https://learn.microsoft.com/en-us/graph/api/resources/onedrive
- Power BI embedded analytics overview: https://learn.microsoft.com/en-us/power-bi/developer/embedded/embedded-analytics-power-bi
- Dataverse Web API overview: https://learn.microsoft.com/en-us/power-apps/developer/data-platform/webapi/overview
- Azure Communication Services overview: https://learn.microsoft.com/en-us/azure/communication-services/overview
- Azure App Service Microsoft Entra authentication: https://learn.microsoft.com/en-us/azure/app-service/configure-authentication-provider-aad
- Azure App Service managed identities: https://learn.microsoft.com/en-us/azure/app-service/overview-managed-identity
- Azure App Service Key Vault references: https://learn.microsoft.com/en-us/azure/app-service/app-service-key-vault-references
- Azure Managed Applications JIT access: https://learn.microsoft.com/en-us/azure/azure-resource-manager/managed-applications/request-just-in-time-access
- Partner Center managed application plans: https://learn.microsoft.com/en-us/partner-center/marketplace-offers/azure-app-managed
- Plan an Azure managed application offer: https://learn.microsoft.com/en-us/partner-center/marketplace-offers/plan-azure-app-managed-app

---

## 33. Final reminder

The winning product is not the most autonomous product. It is the product insurers can safely operate in production: real authenticated users, real Azure data and AI services, real tenant-configurable Outlook mailboxes, grounded evidence-backed outputs, human approvals, secure customer communication, auditability, operational monitoring, recoverability, an enterprise-grade UI, and repeatable Azure Managed Application deployment.

For deployed environments: **no simulation, no mock runtime providers, no fake success paths, no hardcoded insurer data, and no production-readiness claims without evidence.**

---

# IIP Operations UI Implementation Authority

## Mandatory design references

Before changing any frontend page, layout, component, styling token, role-aware navigation, AI surface, customer portal surface, integration state, approval workflow or API-to-UI mapping, read these files in full:

- `docs/ui/IIP_Operations_UI_Design_Specification_MVP.md`
- `docs/ui/reference/IIP_Global_Theme_Approved.png`
- Design System & UX Behavior Specification
- UI/UX Handoff Document
- Frontend Implementation Guide
- Component Architecture Document

These files are the approved authority for the IIP Operations user interface.

## Fidelity requirement

The implementation must match the approved global Light Intelligence Workspace Theme:

- bright white and soft-lavender workspace surfaces;
- deep ink-navy typography;
- restrained indigo-to-violet brand and AI accents;
- light sidebar and light header;
- structured right-side execution or AI insight rail;
- compact operational tables;
- subtle borders, shadows and rounded cards;
- no dark sidebar in MVP;
- no unapproved colour palette changes;
- no improvised styling direction.

Do not reinterpret, modernise, simplify or restyle the approved theme without an explicit user-approved specification update.

## Product naming

Use:

- Staff application name: **IIP Operations**
- Formal product name: **Insurance Intelligence Platform (IIP)**
- Short product name: **IIP**

Do not use placeholder or reference-image brand names.

## Non-negotiable governance rules

Never:

- Present an AI recommendation as a human approval or final decision.
- Present simulated, unavailable or unconfigured channels as connected.
- Expose internal AI confidence, fraud/risk indicators, authority limits, prompts, internal notes or detailed audit data to customer-facing views.
- Send customer-facing AI-generated communication without human approval unless explicitly configured as low-risk and auditable.
- Implement consequential actions without confirmation and required audit capture.
- Make frontend logic the sole authority for permission or approval checks.
- Implement mobile final approval, payout authorisation, fraud override, final rejection, policy amendment or disputed-claim closure in MVP.
- Silently reorder operational queues using AI.
- Show policy or coverage answers as grounded when approved sources do not exist.

## Implementation discipline

- Work in small gated batches.
- Do not redesign multiple major modules in one task unless explicitly authorised.
- Reuse existing working routes, API patterns and component architecture where sound.
- Do not replace production-safe functionality merely to make mock visuals easier.
- Do not introduce generic placeholder data into production-facing views.
- Report any conflict between the existing system and the approved specification before resolving it.

## Required checks after every UI gate

Run all available relevant checks, including where configured:

- frontend lint;
- frontend typecheck;
- frontend unit/component tests;
- frontend production build;
- Playwright visual and workflow tests;
- backend build/tests when API contracts change;
- accessibility checks for changed screens.

Every completed gate must report:

- files changed;
- routes affected;
- specification clauses implemented;
- screenshots captured;
- tests run and results;
- remaining deviations or blockers.
