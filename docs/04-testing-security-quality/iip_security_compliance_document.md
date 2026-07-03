# Insurance Intelligence Platform (IIP) - Security, Privacy and Compliance Requirements Document

**Document type:** Security, Privacy and Compliance Requirements Document  
**Product name:** Insurance Intelligence Platform  
**Acronym:** IIP  
**Prepared for:** Otic Group / IIP Product, Engineering, Cloud, Security, Compliance, Legal, AI, QA, and Delivery Teams  
**Date:** 30 June 2026  
**Version:** 1.0 - Consolidated MVP Security and Compliance Baseline  
**Status:** Working baseline for MVP implementation and legal review  
**Classification:** Internal - Security, Product, Engineering, Architecture, Compliance, Legal, Delivery, and Leadership Reference

---

## Document Control

| Field | Value |
|---|---|
| Owner | Security / Compliance / Architecture Lead |
| Primary audience | Security, compliance, legal, engineering, architecture, cloud, product, QA, delivery, and leadership |
| Source baseline | IIP Master Product Context, PRD, FSD, Technical Architecture, System Integration Strategy, API Specification, Data Model, UI/UX Handoff, meeting notes, Antigravity/codebase findings, Uganda-focused insurance benchmark notes |
| Current product name | **Insurance Intelligence Platform (IIP)** |
| Superseded name | ACIP / Agentic Insurance Intelligence Platform |
| MVP focus | Motor Claims and Document Intelligence for an insurance company pilot |
| Security stance | Azure-first, human-reviewed, tenant-aware, audit-first, privacy-by-design, evidence-linked, least-privilege |
| Legal status | This document is not legal advice. Uganda insurance and data-protection items require qualified legal and regulatory review. |

---

## 1. Executive Summary


---

# July 2026 Update: Security Controls for Chat-First Command Center

## Security Decision

The chat-first Command Center is a high-trust operational interface. Because it can summarize claims, retrieve policy knowledge, display SLA risk, propose workflow actions, and draft customer communications, it must follow strict security, privacy, compliance, and human-review controls.

## New Security Requirements

| ID | Requirement | Control |
|---|---|---|
| SEC-CMDCHAT-001 | Command queries must be authenticated and tenant/workspace scoped. | Resolve user, tenant, workspace, role, and permissions server-side. |
| SEC-CMDCHAT-002 | Command responses must not expose unauthorized records. | Apply RBAC/ABAC filters to every tool result and evidence reference. |
| SEC-CMDCHAT-003 | Command Center must not answer policy/coverage questions from model memory. | Require approved knowledge retrieval and cite sources. |
| SEC-CMDCHAT-004 | Command Center must not expose internal prompts, system messages, secrets, tokens, or provider payloads. | Redact logs, responses, and evidence details. |
| SEC-CMDCHAT-005 | Command Center must not expose fraud flags, reserves, internal notes, or confidential audit details to unauthorized roles. | Sensitivity labels and role-based rendering. |
| SEC-CMDCHAT-006 | AI-proposed actions must not execute consequential decisions. | Require Approval/Task workflow and human actor. |
| SEC-CMDCHAT-007 | Customer communication drafts must remain drafts unless approved or explicitly configured as low-risk. | Human approval gate and audit event. |
| SEC-CMDCHAT-008 | Every AI-assisted command response must be auditable. | Log AgentRun, tool calls, evidence IDs, output metadata, and user action. |
| SEC-CMDCHAT-009 | Not configured providers must not appear connected. | Provider validation and safe UI states. |
| SEC-CMDCHAT-010 | Prompt injection from documents, emails, or customer text must not override system/tool policy. | Treat retrieved text as evidence, not instruction. |

## Sensitive Output Boundaries

The Command Center must not reveal to unauthorized users:

- cross-tenant or cross-workspace records;
- internal risk notes;
- fraud investigation details beyond role permission;
- reserves or payment authority limits;
- raw email headers containing sensitive routing data;
- unmasked national IDs or medical details where not required;
- prompts, hidden system messages, model configuration, tokens, keys, SAS values, connection strings, or raw provider exceptions.

## Audit Requirements

A command session must log:

- user ID;
- tenant and workspace;
- command text or safe summarized command;
- intent classification;
- tools called;
- evidence IDs used;
- AI/model metadata where applicable;
- response block types produced;
- proposed actions;
- human approvals, edits, sends, exports, or overrides;
- correlation ID and timestamp.

## Phase 1-4 Security Alignment

| Phase | Security Focus |
|---|---|
| Phase 1 | UI boundaries, no hardcoded operational data, no unsafe rendering, role-aware display only as convenience. |
| Phase 2 | Server-side authorization for metrics, claims, SLA, approvals, evidence, and provider health blocks. |
| Phase 3 | Agent tool policy, RAG grounding, prompt-injection defense, human approval gates, audit logs. |
| Phase 4 | Security tests, red-team prompts, cross-tenant denial, no secrets exposure, production no-simulation checks. |
The **Insurance Intelligence Platform (IIP)** is an Azure-native insurance operations platform that helps insurers acquire customers, process claims, review documents, support underwriting, manage customer interactions, and maintain auditable AI-assisted workflows.

IIP will handle sensitive insurance data, identity data, claims evidence, policyholder information, customer communications, policy records, uploaded documents, AI outputs, workflow actions, and audit trails. Therefore, security, privacy, compliance, and governance must be treated as core product requirements, not optional technical features.

The MVP focuses on **Motor Claims and Document Intelligence** for a mid-to-large insurance company pilot. The platform must support WhatsApp/email/channel-based FNOL, document upload, OCR and extraction, missing-document detection, claim summary generation, advisory fraud-signal flagging, human review, audit logging, and claim pack export.

The security model is built around one non-negotiable operating rule:

> **AI prepares insurance work. Humans make regulated insurance decisions.**

IIP must not autonomously approve claims, reject claims, settle claims, release payments, bind policies, set final premiums, accuse customers of fraud, make final underwriting decisions, or issue final adverse customer outcomes. The platform may assist, summarize, classify, extract, flag, route, draft, and recommend, but consequential insurance decisions require accountable human review.

This document defines the security, privacy, compliance, identity, access control, tenant isolation, data protection, AI governance, audit logging, incident response, regulatory, and operational controls required for IIP.

---

## 2. Purpose of this Document

This document converts the IIP product, technical, and UI direction into a security and compliance baseline that engineering and delivery teams can implement.

It answers the following questions:

1. What security and compliance principles must govern IIP?
2. What regulatory and legal themes must be considered for Uganda-first insurance deployment?
3. How should identity, roles, tenant isolation, and access control work?
4. How should IIP protect customer, policy, claim, document, communication, and AI data?
5. How should AI outputs be grounded, reviewed, logged, and restricted?
6. What actions must always require human approval?
7. How should audit logs and evidence trails be captured?
8. How should integrations with WhatsApp, Microsoft Graph, email, files, APIs, and insurer systems be secured?
9. How should IIP manage incidents, breaches, provider failures, and disaster recovery?
10. What security acceptance criteria must be met before pilot deployment?

This document is not a replacement for formal legal advice, insurer security review, penetration testing, cloud architecture review, or a Data Protection Impact Assessment. It is a working baseline for implementation and stakeholder alignment.

---

## 3. Scope

### 3.1 In Scope

This document covers security and compliance requirements for:

- Staff authentication and authorization.
- Microsoft Entra ID integration.
- Role-based and attribute-based access control.
- Tenant and workspace isolation.
- Claims, customers, policies, documents, tasks, leads, conversations, approvals, audit events, integration events, and AI runs.
- WhatsApp, email, web, broker, call center, and future channel ingestion.
- Document upload, classification, OCR, extraction, validation, review, and storage.
- AI summary generation, policy Q&A, customer-safe drafting, and fraud-signal flagging.
- Human review and approval workflows.
- Claims SLA tracking and regulator-ready claims records.
- Consent capture and privacy controls.
- Audit logging and evidence retention.
- API security and integration security.
- Azure infrastructure security.
- Monitoring, alerting, incident response, and operational resilience.
- MVP and near-term roadmap modules.

### 3.2 Out of Scope

The following are outside this document unless separately added:

- Final legal opinion on Uganda insurance law or data-protection law.
- Formal regulator filing package.
- Full ISO 27001 policy set.
- Full SOC 2 readiness package.
- Full penetration test report.
- Full disaster recovery exercise report.
- Full Microsoft Marketplace certification package.
- Customer-specific security questionnaire responses.
- Full payment security design for automated payouts.
- Full cross-country legal mapping outside Uganda.

---

## 4. Core Security and Compliance Principles

| Principle | Requirement |
|---|---|
| Human decision ownership | AI must not make final regulated insurance decisions. |
| Least privilege | Users, services, integrations, and AI tools receive only the permissions required. |
| Tenant isolation | No insurer must access another insurer's data, documents, vectors, prompts, logs, or outputs. |
| Workspace awareness | Every case, document, interaction, lead, event, and AI run must carry workspace context where applicable. |
| Server-side authorization | UI restrictions are not enough. Every sensitive backend endpoint must enforce authorization. |
| Evidence-linked AI | AI outputs affecting insurance work must cite source documents, evidence IDs, records, or retrieval references. |
| Privacy by design | Collect minimum necessary data, explain purposes, capture consent, and protect PII throughout the lifecycle. |
| Audit by default | Important user, AI, workflow, document, and integration actions must be logged. |
| No uncontrolled AI tools | Agents must not receive generic database, file-system, tenant-crossing, or unbounded internet tools. |
| No production mocks | Staging and production must not silently use fake data or fake endpoints. Show Not Configured or error states. |
| Clear customer communication | AI-generated customer messages must be customer-safe and human-approved where consequential. |
| Secure integration | External channels and insurer systems must use authenticated, scoped, logged connectors. |
| Legal uncertainty handling | Regulatory and legal assumptions must be flagged for confirmation. |

---

## 5. Regulatory and Compliance Context

### 5.1 Uganda-First Compliance Context

IIP is designed for Uganda-first deployment, with future Africa-configurable expansion. The chat materials identify several Uganda-specific regulatory and governance themes that should influence product and architecture design.

| Source Area | Relevance to IIP |
|---|---|
| Uganda Insurance Act, 2017 | Insurer governance, policyholder protection, regulated insurance activities, qualified control functions, accountability. |
| Uganda Insurance Regulatory Authority expectations | Claims handling, claims register, fair customer treatment, auditability, complaints, market conduct. |
| Uganda Insurance Mobile Insurance Regulations | Mobile/WhatsApp-first insurance journeys, consent, disclosure, confidentiality, mobile data handling. |
| Uganda Data Protection and Privacy Act, 2019 | Lawful processing, consent, purpose limitation, data minimization, security, retention, customer rights. |
| Data Protection and Privacy Regulations, 2021 | Operationalization of data-protection responsibilities. |
| Insurance Complaints Bureau guidance | Complaint tracking, escalation, evidence, regulator-ready exports. |
| Bancassurance regulations | Future broker/bank channel compliance, licensing, product disclosure, customer protection. |

### 5.2 Legal Review Disclaimer

The product team may use this document to guide design and implementation, but legal counsel must confirm:

- Whether the first pilot requires IRA notification, approval, sandbox participation, or special governance disclosure.
- Whether Azure region selection satisfies Uganda data-protection and insurer requirements.
- Whether processing through Azure OpenAI, Azure AI Document Intelligence, or other cloud services creates cross-border processing concerns.
- Required consent wording for WhatsApp, document upload, claims processing, marketing, and customer support.
- Required claims register fields and retention periods.
- Complaint handling timelines and evidence requirements.
- Breach notification obligations and timelines.
- Customer rights request handling obligations.

---

## 6. Compliance Requirements Register

| ID | Requirement | Product Implication | Backend Implication | UI Implication | MVP Critical |
|---|---|---|---|---|---|
| CR-001 | Human ownership of regulated decisions | Approval workflows for consequential actions | Approval state, actor, timestamp, reason | Human approval banners and decision panels | Yes |
| CR-002 | Claims register support | Claims records must be exportable | Claims register fields in data model | Claims register view/export | Yes |
| CR-003 | Claims SLA tracking | Monitor acknowledgement, assessment, offer/repudiation, settlement timings | SLA clocks, escalation jobs | SLA badges, at-risk/breached indicators | Yes |
| CR-004 | Claims communication evidence | Store customer updates and officer approvals | Communication events and audit records | Communication history and approval status | Yes |
| CR-005 | Explicit consent | Capture purpose-specific consent | Consent records linked to customer/channel | Consent prompts and status chips | Yes |
| CR-006 | Data minimization | Collect only necessary fields | Required-field validation and purpose mapping | Field labels and optional/required distinction | Yes |
| CR-007 | PII protection | Protect sensitive identity/claim/document data | Encryption, masking, access control | Masked IDs, restricted views | Yes |
| CR-008 | Tenant isolation | Prevent cross-insurer data access | Tenant filters and DB constraints | Workspace/tenant context visible | Yes |
| CR-009 | Role-based access | Role-specific permissions | RBAC/ABAC middleware | Role-aware navigation and actions | Yes |
| CR-010 | AI source grounding | AI outputs must cite approved evidence | Evidence IDs and AI run metadata | Source citations and confidence display | Yes |
| CR-011 | AI decision limits | AI must not perform prohibited actions | Tool registry and policy enforcement | Disabled/blocked autonomous actions | Yes |
| CR-012 | Audit logs | Log user, AI, workflow, document, and integration actions | Immutable audit event store or append-only tables | Audit viewer and export | Yes |
| CR-013 | Complaint tracking | Track complaints and escalations | Complaint entity and workflow | Complaint queue and SLA indicators | Phase 2 unless pilot requires |
| CR-014 | Data subject rights | Support access, correction, deletion requests | Request workflow and data inventory | Customer request admin workflow | Phase 2 |
| CR-015 | Retention/deletion | Configurable retention policies | Retention rules and deletion jobs | Admin retention settings | Phase 2 |
| CR-016 | Cross-border processing review | Region and service use must be reviewed | Region metadata and provider documentation | Admin compliance notes | Yes for pilot signoff |
| CR-017 | Mobile disclosure | WhatsApp flows must provide clear disclosures | Message templates and consent records | Customer-safe message templates | Yes for WhatsApp |
| CR-018 | Integration accountability | External system actions must be logged | IntegrationEvent and WriteBackRequest records | Integration health and export status | Yes |

---

## 7. Human-in-the-Loop Decision Boundary

### 7.1 Prohibited Autonomous AI Actions

IIP must not allow AI agents to autonomously perform the following actions:

| Prohibited Action | Required Control |
|---|---|
| Approve a claim | Human claims officer or supervisor approval required. |
| Reject or repudiate a claim | Human decision and customer-safe communication approval required. |
| Approve settlement amount | Human claims authority approval required. |
| Release payment | Finance/payment approval required. |
| Bind a policy | Licensed/authorized human or approved rating/binding workflow required. |
| Set final premium | Underwriter/rating engine approval required. |
| Accept or reject underwriting risk | Qualified underwriter approval required. |
| Accuse customer of fraud | Fraud signal must be advisory and require investigation. |
| Issue final complaint response | Human compliance/customer-experience approval required. |
| Submit regulatory filing | Authorized compliance/legal owner required. |
| Delete evidence or audit records | Restricted admin/legal process required. |

### 7.2 Allowed AI Assistance

AI may assist with:

- Claim intake guidance.
- Document classification.
- OCR-supported extraction.
- Missing-document checklist generation.
- Claim summary drafting.
- Policy/SOP retrieval.
- Customer-safe update drafting.
- Risk/fraud-signal advisory flagging.
- Routing recommendations.
- Underwriting submission summaries.
- Early warning/retention scoring, where configured.
- Report drafting.
- Search and explanation of approved knowledge.

### 7.3 Required UI Language

The UI must distinguish:

- **AI suggestion**
- **System rule**
- **Human decision**
- **Customer action**
- **Integration action**
- **Admin configuration**

Avoid wording such as:

- "AI approved this claim"
- "AI rejected this submission"
- "Fraud confirmed"
- "Settlement authorized by AI"
- "Policy bound by assistant"

Use wording such as:

- "AI-prepared summary"
- "Advisory fraud signal"
- "Suggested next action"
- "Requires human review"
- "Human-approved customer update"
- "Evidence-linked recommendation"

---

## 8. Identity and Authentication Requirements

### 8.1 Staff Authentication

IIP should use Microsoft Entra ID for internal insurer staff and platform users.

| Requirement | Description |
|---|---|
| SSO | Staff should authenticate through Microsoft Entra ID where available. |
| MFA-ready | Privileged and sensitive roles should support or require MFA. |
| Token validation | Backend must validate issuer, audience, expiry, tenant, and signing keys. |
| Conditional access | Enterprise deployments should support customer conditional access policies where applicable. |
| Session management | Sessions should expire and be revocable. |
| Privileged access | Admin roles should be restricted and auditable. |

### 8.2 External Customer Authentication

The MVP does not require customers to log into the internal application. Customer interactions should enter through WhatsApp, email, web forms, broker, or call-center workflows.

For customer-specific claim status or sensitive information, the system should require verification before disclosure.

Verification options may include:

- Policy number plus phone number match.
- OTP through configured channel.
- Claim reference plus identity check.
- Human handoff for sensitive cases.

### 8.3 Service Authentication

Internal services should use managed identity where possible.

| Service Area | Preferred Authentication |
|---|---|
| Azure SQL | Managed Identity / Azure AD auth where feasible. |
| Blob Storage | Managed Identity and scoped RBAC. |
| Key Vault | Managed Identity and least-privilege access policies/RBAC. |
| Azure AI Search | Managed Identity or restricted admin/query keys as appropriate. |
| Azure OpenAI | Managed Identity where supported or Key Vault-managed keys. |
| Document Intelligence | Managed Identity where supported or Key Vault-managed keys. |
| Microsoft Graph | App registration with least-privilege delegated/application permissions. |
| WhatsApp provider | Webhook signature validation and stored provider secrets in Key Vault. |

---

## 9. Authorization Model

### 9.1 RBAC Roles

The MVP should support a role-aware platform. Roles may evolve by client, but the baseline should include:

| Role | Typical Access |
|---|---|
| Claims Officer | Assigned claims, documents, summaries, customer updates, notes, document requests. |
| Claims Supervisor | Team claims, escalations, approvals, SLA management, assignment. |
| Customer Service Agent | Customer interactions, policy Q&A, customer-safe drafts, channel conversations. |
| Underwriter | Underwriting support workflows, submission summaries, missing information, notes. |
| Compliance Officer | Audit logs, decision trails, complaints, evidence packs, exports. |
| Insurer Admin | Users, roles, tenant settings, workflows, document templates, retention settings. |
| IT / Security Admin | Provider health, integrations, identity settings, security logs, support access. |
| Executive / Manager | Dashboards, operational reports, SLA, productivity, trend summaries. |
| Otic Platform Admin | Platform operations, tenant setup, deployment support, monitoring. |
| Implementation Consultant | Configured support access during implementation, time-limited and audited. |

### 9.2 Attribute-Based Controls

RBAC should be extended with attributes where needed.

| Attribute | Examples |
|---|---|
| Tenant | Insurer/customer deployment boundary. |
| Workspace | Branch, region, product line, department, broker network. |
| Claim assignment | Assigned officer, team, supervisor. |
| Claim type | Motor, health, life, property, microinsurance. |
| Claim value | Approval thresholds and escalation rules. |
| Risk level | Fraud signal, SLA breach, complaint, VIP customer. |
| Document sensitivity | ID, medical, financial, legal, claim evidence. |
| Channel | WhatsApp, email, broker, call center, web. |
| Workflow stage | Intake, review, approval, payment, closed. |

### 9.3 Server-Side Enforcement

All sensitive actions must enforce authorization on the backend:

- Viewing a customer profile.
- Viewing a claim.
- Viewing or downloading documents.
- Approving a field extraction.
- Editing extracted fields.
- Assigning or escalating a claim.
- Sending or approving customer communication.
- Marking fraud signals.
- Exporting claim packs.
- Accessing audit logs.
- Changing tenant/workspace settings.
- Creating or updating integrations.
- Viewing AI run details.
- Triggering reprocessing.

Frontend role-aware navigation is useful for UX but must not be the source of truth.

---

## 10. Tenant and Workspace Isolation

### 10.1 Tenant Model

The default enterprise target is one insurer per deployment or strong logical tenant isolation where hosted centrally.

Every major record must include tenant context:

- Customer.
- Policy.
- Claim.
- Document.
- Lead.
- Channel conversation.
- Task.
- Approval.
- Audit event.
- AI run.
- Integration event.
- Workflow case.
- Search index item.
- Export.
- Configuration record.

### 10.2 Workspace Model

Within a tenant, IIP should support workspaces or subtenants by:

- Branch.
- Region.
- Product line.
- Business unit.
- Brand.
- Broker network.
- Country where relevant.

### 10.3 Isolation Requirements

| Area | Requirement |
|---|---|
| Database | Tenant/workspace filters must be mandatory. Consider row-level security or equivalent guardrails. |
| Blob Storage | Documents must be partitioned by tenant and restricted by identity. |
| AI Search | Indexes must not leak across tenants. Use tenant-scoped filters or tenant-specific indexes where required. |
| Prompts and AI runs | AI context must be tenant-scoped and logged. |
| Logs | Operational logs must not expose tenant PII unnecessarily. |
| Exports | Export links must be scoped, time-bound, and auditable. |
| Admin support | Cross-tenant support access must be explicit, restricted, and logged. |

### 10.4 Cross-Tenant Access Testing

Security testing must include attempts to:

- Access another tenant's claim by ID.
- Download another tenant's document.
- Query search results from another tenant.
- Use a valid token with modified tenant headers.
- Access another workspace's records.
- Reuse export links across users/tenants.
- View audit logs outside permitted scope.

---

## 11. Data Classification

IIP should classify data to drive access, retention, masking, encryption, and logging behavior.

| Classification | Examples | Controls |
|---|---|---|
| Public | Public product information, general help text | Standard integrity controls. |
| Internal | Non-sensitive workflow metadata, UI configuration | Authenticated access. |
| Confidential | Claim records, policy records, lead records, internal notes | RBAC, tenant isolation, audit logging. |
| Sensitive PII | National ID, phone number, address, DOB, identity documents | Masking, encryption, restricted access, purpose limitation. |
| Highly Sensitive | Medical reports, injury details, financial records, fraud investigation notes | Strong restrictions, enhanced audit, careful disclosure. |
| Secrets | API keys, client secrets, webhook secrets, connection strings | Key Vault only, no logs, no source control. |
| Audit/Legal Evidence | Audit logs, claim evidence, approvals, regulatory exports | Append-only, retention, restricted deletion. |

---

## 12. Privacy Requirements

### 12.1 Privacy-by-Design Requirements

| Requirement | Implementation Direction |
|---|---|
| Purpose limitation | Capture why data is collected: claims processing, customer service, marketing, support, compliance. |
| Data minimization | Do not collect fields not needed for the workflow. |
| Transparency | Explain what the customer is submitting and why. |
| Consent capture | Store consent status, channel, timestamp, text/version, purpose. |
| Revocation | Support withdrawal/updates where legally applicable. |
| Access and correction | Provide internal process for customer access/correction requests. |
| Retention | Configurable retention periods by document/data type. |
| Deletion | Controlled deletion/anonymization workflows where legally allowed. |
| Cross-border review | Document service regions and processors. |
| Processor accountability | Maintain integration/provider inventory. |

### 12.2 Consent Model

Consent records should include:

| Field | Purpose |
|---|---|
| consentId | Unique consent record. |
| tenantId | Insurer context. |
| customerId | Linked customer where known. |
| channel | WhatsApp, web, email, call center, broker. |
| purpose | Claims processing, support, marketing, document review, policy lookup. |
| consentTextVersion | Version of message/privacy wording. |
| status | Granted, denied, withdrawn, partial, not required. |
| capturedAt | Timestamp. |
| capturedBy | User/system/channel. |
| evidence | Message ID, form submission ID, document reference, call note. |
| expiry/renewal | Optional where required. |

### 12.3 Sensitive Customer Communication

Before disclosing claim or policy-specific data through WhatsApp or another external channel, the system should verify identity and scope the response.

Do not disclose:

- Internal fraud signals.
- Internal adjuster comments.
- Supervisor notes.
- Other policyholders' information.
- Full national ID or sensitive identity data.
- Medical or financial information without proper verification.
- Claim decision before human approval.

---

## 13. Data Protection and Encryption

### 13.1 Encryption at Rest

| Data Store | Requirement |
|---|---|
| Azure SQL | Encryption at rest enabled. Consider ledger/temporal/audit features where useful. |
| Blob Storage | Encryption at rest enabled. Sensitive containers restricted by identity/RBAC. |
| AI Search | Ensure search index security and tenant filtering. |
| Logs | Avoid logging secrets/PII. Use restricted workspace access. |
| Backups | Encrypted, access-controlled, retention-defined. |

### 13.2 Encryption in Transit

- Require HTTPS/TLS for all external and internal APIs.
- Use secure webhook endpoints.
- Use certificate validation for external services.
- Do not send sensitive data over insecure channels.
- Prefer private endpoints for regulated enterprise deployments.

### 13.3 Masking and Redaction

The UI and API should support masking for:

- National ID.
- Phone numbers where not needed in full.
- Email addresses in broad dashboards.
- Bank/payment details.
- Medical details.
- Fraud investigation notes.

Documents used for demos or synthetic testing must not contain real PII unless properly authorized and governed.

---

## 14. AI Governance Requirements

### 14.1 AI Operating Rules

Every AI feature must comply with the following:

| Rule | Requirement |
|---|---|
| Approved knowledge only | Policy coverage questions must retrieve from approved tenant knowledge. |
| Tenant scope | AI context must never cross tenant boundaries. |
| Workspace scope | AI context must respect workspace restrictions. |
| Evidence required | Claim/policy/customer-sensitive outputs must cite evidence or state missing evidence. |
| Confidence/uncertainty | Low-confidence outputs must be flagged for human review. |
| No hallucinated obligations | AI must not invent policy terms, limits, exclusions, dates, or regulatory obligations. |
| Human review | Consequential outputs require human approval. |
| Audit logging | Prompts, tools, sources, output, model metadata, and user action must be logged where appropriate. |
| Tool restriction | Agents may call only approved scoped tools. |
| Prompt injection defense | Uploaded documents and customer messages must not be treated as instructions to the system. |

### 14.2 AI Run Metadata

AI run logs should include:

| Field | Purpose |
|---|---|
| aiRunId | Unique AI execution record. |
| tenantId / workspaceId | Scope. |
| userId / initiatedBy | User or system trigger. |
| agentName | Claims Summary, Policy Q&A, Document Checker, etc. |
| taskType | Summary, extraction review, draft, classification, route, flag. |
| modelProvider | Azure OpenAI or configured provider. |
| modelDeployment | Deployment name/version. |
| promptTemplateVersion | Governance and reproducibility. |
| retrievedEvidenceIds | Documents/search chunks/records used. |
| toolCalls | Scoped tools called. |
| confidence | Where applicable. |
| output | Stored safely with PII controls. |
| requiresHumanReview | Boolean and reason. |
| userAction | Approved, edited, rejected, ignored, escalated. |
| createdAt | Timestamp. |
| correlationId | Troubleshooting and audit. |

### 14.3 AI Agent Tool Registry

Agents must use a formal tool registry. Each tool should define:

- Tool name.
- Purpose.
- Input schema.
- Output schema.
- Authorized agents.
- Authorized user roles.
- Tenant/workspace scope.
- Data classification.
- Human review requirements.
- Timeout and retry policy.
- Failure behavior.
- Audit logging requirement.

Prohibited tool patterns:

- `query_database(sql)`
- `download_any_file(path)`
- `send_any_message(to, body)` without approval controls
- `approve_claim(claimId)` as an AI-callable autonomous tool
- `reject_claim(claimId)` as an AI-callable autonomous tool
- Cross-tenant search without tenant filtering

### 14.4 Prompt Injection Controls

Controls must assume that customer messages and uploaded documents can contain malicious or misleading instructions.

Required controls:

- Treat document content as data, not system instructions.
- Separate system prompts from retrieved content.
- Validate all tool inputs server-side.
- Restrict tool permissions by role and workflow state.
- Log suspicious prompt-injection attempts where identifiable.
- Refuse requests to reveal internal prompts, secrets, or hidden policy logic.
- Do not let extracted document text override governance rules.

---

## 15. RAG, Knowledge, and Evidence Controls

### 15.1 Approved Knowledge Sources

The Knowledge Base should support approved sources such as:

- Policy wordings.
- Policy schedules.
- Claims procedures.
- Underwriting guidelines.
- SOPs.
- FAQs.
- Document checklists.
- Communication templates.
- Approved regulatory guidance summaries.

### 15.2 Knowledge Approval Workflow

Knowledge should not become AI-usable until approved.

```text
Upload knowledge document
-> classify and store document
-> review metadata and access scope
-> approve for indexing
-> index in tenant/workspace-scoped AI Search
-> test retrieval
-> mark active
-> log approval and version
```

### 15.3 Source Citations

Policy or claims guidance responses should show:

- Document title.
- Section/page/chunk reference where available.
- Evidence ID.
- Confidence or completeness indicator.
- Warning where evidence is missing, conflicting, expired, or ambiguous.

### 15.4 Expired and Superseded Knowledge

The system should support:

- Effective date.
- Expiry date.
- Version.
- Superseded-by reference.
- Active/inactive status.
- Re-indexing after approval.

AI should not rely on expired or unapproved documents unless the user is explicitly reviewing historic context and the answer is labelled accordingly.

---

## 16. Document Security

### 16.1 Document Intake Controls

Documents may arrive through:

- WhatsApp.
- Email.
- Web upload.
- Broker upload.
- Internal staff upload.
- Call center-assisted upload.
- Future SFTP/API imports.

Required controls:

- File type restrictions.
- File size limits.
- Malware scanning where available.
- Tenant/workspace tagging at upload.
- Customer/claim linkage.
- Upload source and user/channel capture.
- Original file preservation.
- Processing status tracking.
- Document hash for duplicate detection.
- Evidence ID generation.

### 16.2 Document Processing Controls

Document processing should be asynchronous and auditable.

```text
Document uploaded
-> stored in Blob Storage
-> metadata record created
-> processing event queued
-> document classified
-> OCR/extraction performed
-> fields stored with confidence and evidence references
-> low-confidence items routed to human review
-> audit events written
```

### 16.3 Document Access Controls

| Action | Control |
|---|---|
| View document | Role, tenant, workspace, claim assignment/scope. |
| Download document | Restricted roles, audit event. |
| Delete document | Restricted admin/legal process, retention rules. |
| Reprocess document | Authorized user or system process, audit event. |
| Approve extracted field | Claims officer/reviewer permission. |
| Edit extracted field | Reason required and audit event. |
| Export document pack | Permission, purpose, audit event, expiry where link-based. |

---

## 17. Claims Workflow Security

### 17.1 Motor Claims MVP Controls

For the MVP Motor Claims workflow, security must protect:

- FNOL intake records.
- Customer identity.
- Policy number and coverage context.
- Police reports.
- Vehicle photos.
- Repair quotations.
- Assessor reports.
- AI-generated claim summaries.
- Fraud-signal notes.
- Human decisions.
- Customer communications.
- Claim pack exports.

### 17.2 Claims Approval Controls

Any action that moves a claim toward a consequential outcome should require explicit human ownership.

| Action | Required Control |
|---|---|
| Mark documents complete | Claims officer review. |
| Accept extracted field | User action logged. |
| Confirm coverage | Human review. |
| Escalate fraud signal | Human investigation flag. |
| Recommend settlement | Claims officer note, not final approval. |
| Approve settlement | Authorized user and threshold check. |
| Send final customer update | Human approval for sensitive outcomes. |
| Close claim | Authorized workflow state transition. |

### 17.3 Fraud Signal Safeguards

Fraud-related output must be carefully worded.

Use:

- "Advisory fraud signal."
- "Potential inconsistency."
- "Requires human investigation."
- "Evidence observed."
- "False positive can be marked by reviewer."

Avoid:

- "Fraud confirmed."
- "Customer is fraudulent."
- "Reject this claim for fraud."

---

## 18. Channel, WhatsApp, and Communications Security

### 18.1 Channel Inbox Controls

The channel inbox handles customer-originated interactions from WhatsApp, email, webchat, SMS, broker, call center, and website forms. The current codebase direction confirms that Channels, Customers, and Sales & Leads are internal insurer workspaces fed by external channels, not customer login areas.

Controls:

- Webhook signature validation.
- Message source verification.
- Message ID deduplication.
- PII minimization in previews.
- Conversation-to-customer matching with confidence.
- Manual review for uncertain matches.
- Routing based on intent and role.
- Audit events for assignment, conversion to claim/lead/task, and closure.

### 18.2 Customer-Safe Communications

Customer-facing drafts must be:

- Plain language.
- Non-binding unless approved.
- Free from internal risk/fraud labels.
- Based on approved templates where possible.
- Approved by human where consequential.
- Logged with sender, approver, channel, timestamp, and message version.

### 18.3 Resilient Channel Buffer

Where connectivity is a concern, the platform may support a resilient channel buffer rather than full offline platform operation.

Controls:

- Acknowledge webhook safely where possible.
- Queue messages for processing.
- Retry failed processing with backoff.
- Preserve message ordering where required.
- Dead-letter failed messages.
- Show channel health and queue backlog.
- Avoid duplicate customer messages.
- Log delivery and processing outcomes.

---

## 19. Integration Security

### 19.1 Integration Inventory

IIP integration security must cover:

| Integration | Security Requirements |
|---|---|
| WhatsApp Business API | Webhook validation, provider secret protection, customer identity verification. |
| Microsoft Graph / Outlook | Least-privilege scopes, mailbox scoping, token security, audit ingestion. |
| Email ingestion | Attachment scanning, sender checks, duplicate detection. |
| Blob Storage | Container RBAC, tenant paths, encryption, access logging. |
| Policy admin system | Prefer read-only access in MVP, API scoping, request logging. |
| Claims system | Controlled export/write-back, human approval, idempotency. |
| Power BI/Fabric | Data minimization, role-aware reporting, no PII leakage. |
| SMS gateway | Template controls, sender verification, message logs. |
| Mobile money/payment | Stronger controls, maker-checker, no MVP autonomous payment release. |
| SFTP/file imports | Key management, malware scanning, schema validation, import logs. |

### 19.2 Integration Event Logging

Every integration event should include:

- integrationEventId.
- tenantId/workspaceId.
- connector name.
- external system.
- direction: inbound/outbound.
- entity type and ID.
- status.
- retry count.
- failure reason.
- correlation ID.
- timestamp.
- user/system initiator.

### 19.3 Write-Back Controls

Controlled write-back should use a `WriteBackRequest` pattern:

```text
AI/system prepares payload
-> human reviews
-> user approves
-> write-back request created
-> connector sends payload
-> response stored
-> audit event written
-> UI shows success/failure
```

Write-back should be idempotent and auditable.

---

## 20. API Security

### 20.1 Required API Controls

| Control | Requirement |
|---|---|
| Authentication | All protected endpoints require valid token/session. |
| Authorization | Server-side RBAC/ABAC checks. |
| Tenant validation | Tenant/workspace context must be validated, not blindly trusted from headers. |
| Correlation IDs | Every request should carry or generate a correlation ID. |
| Input validation | Validate request bodies, query params, file uploads, and IDs. |
| Output filtering | Return only fields the user may see. |
| Pagination | Required for list endpoints. |
| Rate limiting | Required for external/public endpoints and sensitive flows. |
| Idempotency | Required for create/write-back/payment-like operations. |
| Error handling | No secrets or stack traces in user-facing errors. |
| Audit events | Required for sensitive actions. |

### 20.2 Sensitive Endpoint Categories

Sensitive endpoints include:

- Claims details and actions.
- Customer profiles.
- Document downloads.
- Document extraction review.
- AI run details.
- Approval actions.
- Audit log access.
- Integration configuration.
- Admin settings.
- User and role management.
- Export generation.
- Provider secrets and health details.

### 20.3 API Error Requirements

Error responses should include:

- Error code.
- User-safe message.
- Correlation ID.
- Optional validation details.

They should not include:

- Secrets.
- Raw tokens.
- Full connection strings.
- Stack traces.
- Sensitive document text.
- Cross-tenant record existence signals.

---

## 21. Infrastructure and Azure Security

### 21.1 Azure Security Baseline

| Area | Requirement |
|---|---|
| Identity | Use Entra ID and Managed Identity where possible. |
| Secrets | Store secrets in Azure Key Vault. |
| Network | Use private endpoints for regulated/private deployments where feasible. |
| Storage | Restrict blob containers, disable public access unless explicitly justified. |
| Database | Enforce encryption, firewall rules/private connectivity, least-privilege access. |
| App hosting | Use environment separation and secure app settings. |
| Monitoring | Application Insights and Log Analytics required. |
| Security posture | Defender for Cloud and Sentinel considered for enterprise deployments. |
| IaC | Use Bicep/Terraform with reviewed parameters. |
| Backups | Encrypted backups with defined retention. |

### 21.2 Environment Separation

Recommended environments:

| Environment | Security Requirements |
|---|---|
| Local | Mock providers allowed; no real PII. |
| Development | Limited test data; secrets in dev Key Vault; no production access. |
| Staging | Real configured services or Not Configured states; sanitized data preferred. |
| Production | No mocks, production identity, monitoring, backup, support controls. |
| Demo | Synthetic or redacted data only. |

### 21.3 No Production Mock Rule

The platform must not silently use mock data or fake endpoints in staging or production.

Allowed:

- Local mock mode.
- Demo mode with clearly labelled synthetic data.
- Not Configured state.
- Provider health warnings.

Not allowed:

- Fake claims in production dashboards.
- Mock AI responses in staging presented as real.
- Simulated integration success in production.
- Hidden fallback to local JSON data.

---

## 22. Secrets and Key Management

### 22.1 Secrets Storage

Secrets must not be committed to source control. Store secrets in Key Vault or platform-approved secret storage.

Secret categories:

- Database connection strings.
- Azure OpenAI keys where managed identity is unavailable.
- Document Intelligence keys where required.
- Search admin/query keys where required.
- WhatsApp provider secrets.
- Microsoft Graph client secrets/certificates.
- Webhook signing secrets.
- SMS/email provider secrets.
- Storage account keys if managed identity is unavailable.

### 22.2 Secret Handling Rules

- Do not log secrets.
- Do not expose secrets in UI.
- Do not paste secrets into agent prompts.
- Rotate secrets periodically and after incidents.
- Use separate secrets per environment.
- Restrict Key Vault access by role and environment.
- Prefer certificates/managed identity over static secrets where feasible.

---

## 23. Logging and Audit Requirements

### 23.1 Audit Event Categories

Audit logs should capture:

| Category | Examples |
|---|---|
| Authentication | Login, logout, failed login, token/session issues. |
| Authorization | Access denied, privileged access, role changes. |
| Claims | Claim created, assigned, escalated, reviewed, closed, exported. |
| Documents | Uploaded, classified, extracted, reviewed, edited, downloaded. |
| AI | Summary generated, policy answer generated, evidence used, user action on AI output. |
| Approvals | Approval requested, approved, rejected, overridden, escalated. |
| Communications | Draft generated, message approved, sent, failed, received. |
| Integrations | Webhook received, Graph email ingested, export sent, write-back completed/failed. |
| Admin | Settings changed, users/roles updated, retention rules changed. |
| Security | Suspicious activity, cross-tenant access attempt, webhook validation failure. |

### 23.2 Audit Log Fields

Minimum fields:

- auditEventId.
- tenantId.
- workspaceId.
- actorType: user, system, AI, integration.
- actorId.
- action.
- entityType.
- entityId.
- before/after summary where safe.
- reason/comment where required.
- source IP/device where applicable.
- timestamp.
- correlationId.
- severity.
- data classification.

### 23.3 Audit Integrity

Audit logs should be:

- Append-only where feasible.
- Restricted from ordinary editing.
- Searchable by authorized compliance/security users.
- Exportable for regulator/client review.
- Retained according to policy.
- Protected from cross-tenant access.

---

## 24. Monitoring, Alerting, and Security Operations

### 24.1 Operational Monitoring

Monitor:

- API errors.
- Latency.
- Authentication failures.
- Authorization failures.
- Queue backlogs.
- Dead-letter messages.
- AI provider errors.
- Document Intelligence errors.
- Search indexing failures.
- Blob upload/download failures.
- Database performance.
- Integration failures.
- WhatsApp webhook failures.
- Graph ingestion failures.

### 24.2 Security Monitoring

Alert on:

- Repeated failed logins.
- Privileged role changes.
- Cross-tenant access attempts.
- Large document downloads.
- Unusual export volume.
- Failed webhook signature validations.
- Secret access anomalies.
- Admin setting changes.
- AI tool policy violations.
- Unauthorized document access.
- Excessive API rate errors.

### 24.3 Provider Health UI

The Support & Health area should display statuses such as:

- Connected.
- Degraded.
- Disconnected.
- Not Configured.
- Queue backlog.
- Last successful sync.
- Last failed event.
- Correlation ID for incidents.

---

## 25. Incident Response

### 25.1 Incident Categories

| Category | Examples |
|---|---|
| Security incident | Unauthorized access, credential exposure, cross-tenant leak. |
| Privacy incident | PII disclosure, wrong recipient, unauthorized document access. |
| AI governance incident | AI generated unsupported claim decision, hallucinated policy term, unapproved customer message. |
| Integration incident | Failed Graph ingestion, WhatsApp outage, duplicated messages, write-back failure. |
| Availability incident | API outage, database outage, provider outage. |
| Data integrity incident | Incorrect extraction, duplicate claim, corrupted import. |

### 25.2 Incident Response Steps

```text
Detect incident
-> classify severity and category
-> assign incident owner
-> preserve logs and evidence
-> contain impact
-> notify internal stakeholders
-> assess customer/regulatory notification obligations
-> remediate root cause
-> verify fix
-> document timeline and lessons learned
-> update controls/tests/runbooks
```

### 25.3 Breach and Regulatory Notification

Notification obligations require legal review. The platform must support evidence collection by preserving:

- Affected tenant.
- Affected users/customers.
- Affected data types.
- Time window.
- Access logs.
- Export/download logs.
- User actions.
- AI actions.
- Integration events.
- Remediation actions.

---

## 26. Business Continuity and Disaster Recovery

### 26.1 Availability Goals

Availability goals should be confirmed by client tier. Baseline MVP should aim for practical resilience rather than complex enterprise DR on day one.

Required capabilities:

- Health checks.
- Backups.
- Queue retry.
- Dead-letter review.
- Provider degradation states.
- Manual fallback for claims processing.
- Export/recovery of claim data.
- Operational runbooks.

### 26.2 Backup and Recovery

| Component | Recovery Requirement |
|---|---|
| Azure SQL | Automated backups, point-in-time recovery where configured. |
| Blob Storage | Soft delete/versioning considered for documents. |
| Search Index | Rebuildable from source documents/metadata. |
| Audit Logs | Strong retention and restricted deletion. |
| App Configuration | IaC and environment config backup. |
| Key Vault | Recovery/soft-delete enabled where available. |

### 26.3 Degraded Operations

When a provider fails:

- Show degraded provider health.
- Queue safe background work.
- Avoid data loss.
- Avoid duplicate actions.
- Prevent unsupported AI outputs.
- Allow manual claims review where possible.
- Preserve audit logs.

---

## 27. Data Residency and Cross-Border Processing

### 27.1 Recommended Region Strategy

The chat materials recommend **South Africa North** as the preferred starting Azure region for Africa proximity and data-residency comfort, with **West Europe** as a fallback if required AI services, quotas, or models are unavailable.

### 27.2 Region Confirmation Required

Before production or client pilot signoff, engineering and legal must confirm:

- Azure OpenAI availability and model deployment in selected region.
- Azure AI Document Intelligence availability.
- Azure AI Search availability.
- Data storage region.
- Whether model processing data leaves the selected region.
- Whether logs/telemetry are stored in the same or acceptable region.
- Customer contract/data-processing requirements.
- Uganda cross-border data transfer implications.

### 27.3 Data Residency Documentation

The architecture pack should include:

- Azure region map.
- Services used per region.
- Data stored per service.
- Data processed per service.
- Third-party processors.
- Subprocessor list where applicable.
- Retention and deletion policy.
- Data export process.

---

## 28. Secure Development Lifecycle

### 28.1 Engineering Guardrails

Implementation agents and developers must follow these rules:

- Do not commit secrets.
- Do not bypass authentication to make screens work.
- Do not add frontend-only permission checks for sensitive actions.
- Do not use mock data in staging/production.
- Do not give AI generic database access.
- Do not hardcode tenant IDs in production logic.
- Do not expose internal fraud labels to customers.
- Do not log PII unnecessarily.
- Do not send customer messages without configured approval rules.
- Do not implement autonomous claim approval/rejection/payment.

### 28.2 Code Review Requirements

Security review should be required for changes to:

- Authentication.
- Authorization.
- Tenant/workspace filtering.
- Document upload/download.
- AI tool registry.
- AI prompts affecting customers/claims/policies.
- Integration credentials.
- Admin settings.
- Audit logging.
- Export generation.
- Payment-like flows.
- Database migrations involving PII.

### 28.3 Dependency and Supply Chain Controls

- Use dependency scanning.
- Pin or review critical dependencies.
- Avoid unmaintained libraries for auth/crypto/file parsing.
- Review PDF/image/document parsing libraries.
- Use official Azure SDKs where practical.
- Restrict CI/CD secrets.
- Protect main branch and deployment workflows.

---

## 29. Security Testing Requirements

### 29.1 MVP Security Test Scenarios

| Test | Expected Result |
|---|---|
| User accesses another tenant's claim ID | Denied and logged. |
| User modifies tenant header | Backend rejects or ignores unauthorized scope. |
| Customer service user opens fraud notes | Denied unless permitted. |
| Claims officer downloads unassigned sensitive document | Denied unless role/workspace permits. |
| AI asked to approve claim | Refuses or routes to human review. |
| AI asked policy question without source | States insufficient evidence or routes to human. |
| Uploaded document contains prompt injection | Treated as document data, not instruction. |
| Expired knowledge document retrieved | Not used unless explicitly historic and labelled. |
| Webhook signature invalid | Rejected and logged. |
| Export link reused by unauthorized user | Denied and logged. |
| Production provider missing | UI shows Not Configured, not mock result. |

### 29.2 Privacy Tests

- Verify masking of national ID fields.
- Verify role-specific customer profile fields.
- Verify customer communication does not include internal notes.
- Verify audit exports do not overexpose unrelated PII.
- Verify demo data is synthetic/redacted.
- Verify retention/delete jobs do not delete audit/legal records improperly.

### 29.3 AI Governance Tests

- Claim approval prompt test.
- Claim rejection prompt test.
- Fraud accusation prompt test.
- Unsupported policy coverage prompt test.
- Cross-tenant retrieval test.
- Prompt injection in document test.
- Low-confidence extraction test.
- Missing evidence test.
- Customer-safe tone test.
- Human approval workflow test.

---

## 30. Security Acceptance Criteria

IIP security and compliance baseline is acceptable for pilot when:

1. Staff authentication works through the approved identity path.
2. Backend validates tokens and enforces authorization server-side.
3. Tenant and workspace context is stored on all major records.
4. Cross-tenant access tests fail safely and are logged.
5. Role-aware navigation is implemented but not relied on as the only control.
6. Claims, documents, customers, leads, conversations, approvals, and audit logs have access checks.
7. Documents are stored securely and are not publicly accessible.
8. Document processing is auditable.
9. AI outputs are evidence-linked where required.
10. AI cannot autonomously approve/reject/settle/bind/price/accuse/pay.
11. Human approval workflows exist for sensitive actions.
12. Audit events are captured for user, AI, document, workflow, and integration actions.
13. No staging/production environment silently uses mock data.
14. Provider health and Not Configured states are visible.
15. Secrets are in Key Vault or approved secret storage.
16. Logs do not expose secrets or unnecessary PII.
17. Incident response process is documented.
18. Legal/compliance open questions are recorded.
19. Synthetic/redacted data is used for demos unless client data is formally authorized.
20. Production release has passed agreed security and privacy tests.

---

## 31. Roles and Responsibilities

| Role | Security/Compliance Responsibility |
|---|---|
| Product Owner | Ensure requirements preserve human review and regulatory safety. |
| Engineering Lead | Implement secure backend, tenant isolation, audit, and provider controls. |
| Cloud Architect | Design Azure security, networking, identity, monitoring, backups. |
| Security Lead | Review threats, access, tests, secrets, incident processes. |
| Compliance/Legal Lead | Review Uganda regulatory, privacy, consent, and client obligations. |
| AI Lead | Enforce AI tool registry, prompt governance, RAG controls, evaluation. |
| QA Lead | Test security, role behavior, AI guardrails, privacy, audit flows. |
| Delivery Lead | Ensure client-specific deployment and compliance assumptions are documented. |
| Insurer Admin | Manage users, roles, workflows, and client-side approvals. |
| Otic Platform Admin | Provide restricted, audited platform support. |

---

## 32. Risk Register

| ID | Risk | Impact | Mitigation | Owner |
|---|---|---|---|---|
| R-001 | Product accidentally implies AI makes claim decisions | Regulatory/customer trust risk | Strict copy, UI labels, AI refusals, human approval flows | Product/AI |
| R-002 | Cross-tenant data leakage | Critical security incident | Tenant filters, tests, RBAC/ABAC, isolated storage/search | Engineering/Security |
| R-003 | PII exposed through logs | Privacy breach | Logging policy, redaction, review, restricted log access | Engineering/Security |
| R-004 | AI hallucinates policy terms | Wrong customer guidance | RAG-only policy answers, citations, uncertainty handling | AI/Product |
| R-005 | Fraud signal exposed to customer | Legal/reputational risk | Customer-safe comms rules and role restrictions | Product/Engineering |
| R-006 | WhatsApp messages duplicated/lost | Customer trust issue | Idempotency, message IDs, queues, retries, health dashboard | Engineering |
| R-007 | Azure region/model availability mismatch | Deployment delay | Confirm service availability and fallback region | Cloud Architect |
| R-008 | Legal uncertainty on cloud processing | Pilot delay/risk | Legal review and DPA preparation | Legal/Leadership |
| R-009 | Admin support access too broad | Data exposure | Just-in-time, least privilege, audit logs | Security/Ops |
| R-010 | Production mock fallback | Misleading demos/operations | Environment gates, provider health, CI checks | Engineering/QA |

---

## 33. MVP Security Implementation Backlog

### Phase 1 - Foundation

- Confirm official IIP naming in all security documentation.
- Define roles and permission matrix.
- Implement Entra ID authentication path.
- Add backend authorization middleware.
- Add tenant/workspace context validation.
- Set up Key Vault and secret naming standards.
- Define audit event schema.

### Phase 2 - Core Data and Claims Security

- Add tenant/workspace fields to core entities.
- Implement claims/document/customer access checks.
- Secure Blob Storage containers.
- Implement document upload validation.
- Add audit events for claim/document actions.
- Add field extraction review audit logs.

### Phase 3 - AI Governance

- Define AI tool registry.
- Add AI run logging.
- Enforce prohibited autonomous decision rules.
- Add source citation requirements for policy/claims outputs.
- Add prompt injection handling guidelines.
- Add low-confidence human review routing.

### Phase 4 - Integration Security

- Secure WhatsApp webhooks.
- Secure Microsoft Graph ingestion.
- Add integration event logging.
- Add provider health states.
- Add dead-letter queue review.
- Add write-back request pattern.

### Phase 5 - Compliance and Pilot Readiness

- Prepare consent text and privacy review pack.
- Create claims register export baseline.
- Add SLA tracking controls.
- Run cross-tenant security tests.
- Run AI governance tests.
- Prepare incident response runbook.
- Prepare legal open-questions register.

---

## 34. Open Compliance Questions

| ID | Question | Who Should Confirm | Priority |
|---|---|---|---|
| Q-001 | Does the first pilot require IRA notification, sandbox participation, or formal approval? | Legal/Leadership | High |
| Q-002 | Is South Africa North acceptable for Uganda insurer PII and claims processing? | Legal/Client | High |
| Q-003 | Is West Europe acceptable as a fallback for AI services if required? | Legal/Client | High |
| Q-004 | What exact consent language should WhatsApp claim intake use? | Legal/Product | High |
| Q-005 | What exact claims register fields and retention period are required? | Compliance/Claims SME | High |
| Q-006 | What are the client-specific document retention requirements? | Client/Legal | Medium |
| Q-007 | What level of AI explainability does the pilot customer require? | Client/Compliance | Medium |
| Q-008 | Are payment/payout workflows included in the pilot? | Product/Client | High |
| Q-009 | Which Graph scopes will be approved by the customer? | Cloud/Security | Medium |
| Q-010 | Which WhatsApp provider will be used and what data terms apply? | Engineering/Legal | High |

---

## 35. Appendices

### Appendix A - Security Terms

| Term | Meaning |
|---|---|
| RBAC | Role-Based Access Control. |
| ABAC | Attribute-Based Access Control. |
| PII | Personally Identifiable Information. |
| RAG | Retrieval-Augmented Generation. |
| HITL | Human-in-the-loop. |
| FNOL | First Notice of Loss. |
| Tenant | Insurer/customer boundary. |
| Workspace | Branch/region/product/department context within a tenant. |
| Audit Event | Record of an important user/system/AI/integration action. |
| AI Run | Logged AI execution with model, prompt, source, output, and user action metadata. |
| WriteBackRequest | Controlled request to update or export data to a downstream system after approval. |

### Appendix B - Minimum Security Artifacts to Maintain

| Artifact | Purpose |
|---|---|
| Role and Permissions Matrix | Defines who can see/do what. |
| Data Classification Matrix | Defines sensitivity and controls per data type. |
| Integration Security Register | Tracks systems, credentials, scopes, risks. |
| AI Tool Registry | Defines allowed AI tools and governance. |
| Audit Event Catalog | Defines auditable events and fields. |
| Consent Register | Tracks purpose-specific consent. |
| Incident Response Runbook | Guides security/privacy incident handling. |
| Legal Open Questions Register | Tracks compliance assumptions requiring review. |
| Data Retention Schedule | Defines retention and deletion rules. |
| Security Test Plan | Defines required security and AI governance tests. |

### Appendix C - Do Not Build List

For MVP and until explicitly approved, do not build:

- Autonomous claim approval.
- Autonomous claim rejection.
- Automatic settlement.
- Payment release automation.
- Final underwriting acceptance/rejection.
- Final premium setting.
- Policy binding.
- Fraud accusation automation.
- Customer-visible internal fraud labels.
- Unbounded AI database query tools.
- Cross-tenant AI search.
- Production mock providers.
- Unapproved external data sharing.

---

## 36. Closing Security Position

IIP should be implemented as a controlled, evidence-linked, human-reviewed insurance intelligence platform. Its security and compliance credibility depends on making governance visible in the product, enforceable in the backend, and provable through audit logs.

The safest implementation posture is:

> **Protect tenant data, ground AI in approved evidence, require humans for consequential insurance decisions, log every important action, and treat privacy and compliance as core product features.**