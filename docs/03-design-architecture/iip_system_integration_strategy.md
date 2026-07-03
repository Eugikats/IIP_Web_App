# Insurance Intelligence Platform (IIP) - System Integration Strategy

**Document Type:** System Integration Strategy  
**Product:** Insurance Intelligence Platform (IIP)  
**Prepared For:** Otic / IIP Product, Engineering, Architecture, Security, and Delivery Teams  
**Version:** 1.0  
**Date:** 30 June 2026  
**Classification:** Internal - Product and Engineering Reference  
**Related Documents:** Master Product Context, PRD, FSD, Technical Architecture Document

---

## 1. Executive Summary


---

# July 2026 Update: Command Center Integration Requirements

## Integration Decision

The new chat-first Command Center depends on a reliable integration layer because operational answers must be grounded in real connected systems, normalized records, and provider health.

The Command Center must not invent operational status. If a data source or provider is unavailable, the response must show `Not configured`, `degraded`, or a safe error state.

## Command Center Data Sources

| Data Source | Used For | MVP Integration Pattern |
|---|---|---|
| Azure SQL canonical records | Claims, customers, policies, tasks, approvals, SLA state, document state | Backend services and scoped query tools. |
| Microsoft Graph / Outlook | Inbound email claims, attachments, approved outbound messages | Tenant-configurable mailbox integration. |
| WhatsApp provider | FNOL, customer updates, missing document responses | Provider-specific webhook adapter. |
| Azure Blob Storage | Source documents, evidence packs, generated reports | Document metadata in SQL, files in Blob. |
| Azure AI Document Intelligence | Extraction and classification state | Backend adapter only. |
| Azure AI Search | Policy, SOP, document, and knowledge evidence | Tenant/workspace-filtered retrieval. |
| Azure Service Bus | Workflow state, ingestion queues, retries, DLQ signals | Queue and event monitoring. |
| Provider Health Services | Connected/degraded/not configured states | Support and Command Center status blocks. |

## Required Integration Behavior for Chat Responses

When the user asks an operational question, the Command Center should be able to retrieve:

- current claims needing attention;
- claims at SLA risk or breached;
- incomplete claims and missing documents;
- pending approvals;
- customer update backlog;
- new channel conversations;
- recent ingestion failures;
- document extraction failures;
- provider health and not-configured states;
- evidence and source references used by AI.

## Integration Guardrails

- Integration tools exposed to the Command Center agent must be read-only or draft-only unless a human approval workflow is explicitly invoked.
- No generic database, mailbox, or file-system tool may be exposed to the agent.
- Every integration result must carry tenant, workspace, source system, external record ID where available, and correlation ID.
- Provider failures must be visible in the chat response and evidence rail.
- Duplicate channel events must be idempotently handled.
- Customer communications prepared by the Command Center must remain drafts until approved or explicitly configured as low-risk.

## Phase 1-4 Integration Alignment

| Phase | Integration Focus |
|---|---|
| Phase 1 | Define command data contracts and UI states for configured, not configured, degraded, and error providers. |
| Phase 2 | Connect Command Center briefing and metric blocks to Azure SQL, provider health, claims, documents, approvals, and channel summaries. |
| Phase 3 | Add scoped command tools for Graph/Outlook, WhatsApp, documents, knowledge retrieval, and draft communications. |
| Phase 4 | Validate end-to-end integration evidence, audit trails, retries, DLQs, no fake success paths, and production readiness. |
This document defines the integration strategy for the **Insurance Intelligence Platform (IIP)**. It consolidates the integration direction discussed across the project context, PRD, FSD, technical architecture, UI/UX notes, client feedback, and implementation-agent findings.

IIP is an Azure-native, human-reviewed insurance operations platform. It does not replace an insurer's policy administration system, claims system, CRM, finance system, document repository, broker portal, or Microsoft 365 environment. Instead, IIP acts as an **intelligent integration and workflow layer** above those systems.

The platform receives information from customer channels, staff workspaces, Microsoft 365, insurer core systems, uploaded documents, and future third-party services. It converts fragmented inputs into structured, auditable insurance work items such as claims, leads, customer records, document checklists, AI summaries, review tasks, and evidence packs.

The integration strategy follows five principles:

1. **Integrate without replacing core systems.** IIP should augment existing insurer systems, not force rip-and-replace.
2. **Prefer read-only access first.** Production write-back must be controlled, explicit, human-approved, and audited.
3. **Normalize data into canonical insurance entities.** Customer, Policy, Claim, Document, Task, Lead, Conversation, WorkflowCase, IntegrationEvent, and AuditEvent are the core integration objects.
4. **Use Azure-native integration patterns.** API Management, Azure Functions, Durable Functions, Service Bus, Event Grid, Blob Storage, Azure SQL, AI Search, Key Vault, Managed Identity, and Application Insights are the default foundation.
5. **Keep humans accountable for regulated insurance decisions.** Integrations may prepare data and workflow actions, but final claims, underwriting, fraud, payment, and customer-adverse decisions remain human-owned.

The MVP should focus on a practical integration scope:

- WhatsApp Business API for customer acquisition and claims initiation.
- Microsoft Graph / Exchange Online for shared mailbox ingestion and approved outbound communication.
- Azure Blob Storage for document/evidence storage.
- Azure AI Document Intelligence for OCR and extraction.
- Azure AI Search for grounded knowledge retrieval.
- Azure SQL for canonical operational records.
- Controlled file/API export for downstream insurer systems.
- Basic integration health and audit logging.

The longer-term roadmap adds policy administration systems, claims management systems, CRM, payment/mobile money, Power BI/Fabric, SharePoint, Teams, SMS, broker portals, regulatory exports, and private deployment integration options.

---

## 2. Purpose of This Document

This document exists to guide engineering, product, architecture, delivery, and implementation agents when designing or building integrations for IIP.

It answers:

- Which systems should IIP integrate with?
- What should be included in MVP versus later phases?
- What data should move between systems?
- Which integrations are read-only, write-back, or event-driven?
- How should external inputs become structured IIP work items?
- How should IIP preserve tenant isolation, auditability, and human approval?
- What fallback approaches should be used where insurer APIs do not exist?
- What backend endpoints, events, and data contracts are required?
- How should integration failures, retries, and health states be handled?

This is not a final implementation specification for every third-party system. Each insurer pilot will still require discovery of the customer's actual core systems, data formats, security policies, and operational workflows.

---

## 3. Product Context for Integration

### 3.1 Product Name and Positioning

The official product name is:

> **Insurance Intelligence Platform (IIP)**

Earlier materials used **ACIP - Agentic Insurance Intelligence Platform**. That naming is superseded. All integration documentation should use **IIP**.

IIP is not a generic chatbot. It is an insurance operations platform that embeds AI into claims, documents, customer acquisition, customer service, knowledge retrieval, audit, and workflow routing.

### 3.2 What IIP Is Integrating

IIP integrates three broad categories of systems:

1. **Customer and channel systems** - WhatsApp, web chat, SMS, email, call center, broker submissions, website forms.
2. **Insurance operating systems** - policy administration, claims management, CRM, underwriting tools, document management, finance/payment systems.
3. **Microsoft/Azure platform services** - Entra ID, Microsoft Graph, Outlook/Exchange, SharePoint/OneDrive, Teams, Power BI/Fabric, Azure AI services, Azure storage and messaging services.

### 3.3 What IIP Should Not Do

IIP must not directly replace insurer systems of record in the MVP. It must not become the ungoverned source of final truth for regulated insurance outcomes. It must not bypass human review for:

- Claim approval.
- Claim rejection.
- Claim repudiation.
- Settlement amount approval.
- Payment release.
- Final underwriting acceptance or rejection.
- Premium setting.
- Fraud accusation.
- Policy binding.
- Complaint final response.
- Regulatory filing submission.

Integrations may prepare, route, export, draft, summarize, retrieve, or validate information. Final regulated actions require human ownership.

---

## 4. Integration North Star

IIP should become the insurer's **intelligent work preparation layer**.

```text
External channels + insurer systems + documents + Microsoft 365
                        |
                        v
              IIP Integration Fabric
                        |
                        v
         Canonical Insurance Data Layer
                        |
                        v
 AI Search + Document Intelligence + AI Agents + Workflows
                        |
                        v
     Human Review + Controlled Export/Write-back + Audit
```

The long-term goal is that an insurer can connect IIP to its existing ecosystem and immediately gain:

- Unified intake from multiple customer channels.
- Structured claims and lead records.
- Document classification, extraction, and validation.
- Policy and knowledge retrieval.
- AI-generated summaries and next actions.
- Work queues for claims, leads, customers, underwriting, and service.
- Approval workflows.
- Audit logs.
- Operational dashboards.
- Export and controlled write-back into systems of record.

---

## 5. Integration Design Principles

### 5.1 Read-First, Write-Later

The first integration posture should be read-first. IIP can read or ingest information, prepare work, and export reviewed outputs. Write-back to insurer systems should come only after:

- The workflow is understood.
- The destination system contract is documented.
- Human approval points are clear.
- Audit logging is implemented.
- Rollback or correction process exists.
- Customer and insurer data boundaries are enforced.

### 5.2 Human-Approved Write-back

Where IIP writes back to an insurer system, it should generally follow this sequence:

```text
AI prepares suggested update
       |
       v
System validates required fields
       |
       v
Human reviews evidence and action
       |
       v
Human approves write-back
       |
       v
IIP writes to external system or exports payload
       |
       v
IntegrationEvent and AuditEvent are stored
```

### 5.3 No Generic Database Access for AI Agents

AI agents must not have direct unrestricted database access. Tools like `query_database(sql)` are prohibited.

Allowed tool pattern:

```text
get_claim(claimId)
get_claim_documents(claimId)
get_policy_summary(policyId)
get_required_documents(claimType, productLine)
create_customer_update_draft(claimId)
queue_writeback_request(caseId, actionType)
```

Each tool must be scoped by tenant, workspace, role, and workflow state.

### 5.4 Tenant and Workspace Isolation

Every integration record must carry tenant and workspace context.

Required fields:

- tenantId
- workspaceId
- sourceSystem
- externalRecordId
- integrationEventId
- correlationId
- createdAt
- createdBy or sourceIdentity
- dataClassification

No cross-tenant lookup, vector search, file access, or write-back is allowed.

### 5.5 Evidence-Linked Data Movement

When extracted values, AI summaries, or workflow decisions are produced from documents, IIP should link outputs to source evidence.

Example:

```json
{
  "fieldName": "incidentDate",
  "value": "2026-06-17",
  "confidence": 0.91,
  "sourceDocumentId": "doc_123",
  "sourcePage": 1,
  "sourceEvidenceId": "ev_456",
  "requiresHumanReview": false
}
```

### 5.6 Failure Is Expected

Insurance integrations often depend on legacy systems, email attachments, spreadsheet exports, slow APIs, and manual workarounds. IIP should assume:

- External systems may be unavailable.
- Webhooks may be duplicated.
- Email attachments may be malformed.
- Documents may be incomplete.
- API schemas may change.
- Customer messages may arrive out of order.
- Users may upload the wrong file.

The integration design must support retries, idempotency, dead-letter queues, manual review, fallback upload, and clear support visibility.

---

## 6. Target Integration Landscape

### 6.1 Customer and Channel Integrations

| Integration | Purpose | Direction | MVP Priority | Notes |
|---|---|---:|---:|---|
| WhatsApp Business API | Customer acquisition, FNOL, missing-document requests, updates | Two-way | Critical | Primary Uganda/Africa customer channel. Provider still to confirm. |
| Web chat | Customer support and lead capture | Two-way | Phase 2 | Can share conversation model with WhatsApp. |
| Email / Outlook / Exchange | Shared mailbox ingestion, attachments, approved outbound messages | Two-way with approval | High | Microsoft Graph preferred. |
| SMS gateway | Fallback notifications | Outbound | Later | Useful for low-bandwidth and non-WhatsApp customers. |
| Call center | Manual staff intake and conversation logging | Inbound/manual | Phase 2 | May integrate via CRM or staff input. |
| Broker submissions | Broker-uploaded leads, claims, documents | Inbound | Phase 2 | Could be portal, email, API, or spreadsheet upload. |
| Website forms | Lead and claim intake from insurer website | Inbound | Phase 2 | API/webhook integration. |
| Teams alerts | Staff notifications and escalation | Outbound | Optional V1/Phase 2 | Useful for internal operations. |

### 6.2 Insurance Core and Operations Integrations

| Integration | Purpose | Direction | MVP Priority | Notes |
|---|---|---:|---:|---|
| Policy administration system | Policy lookup, coverage status, product, limits, insured details | Read-first | High/Phase 2 | MVP can use controlled import if no API. |
| Claims management system | Claim status, claim notes, export reviewed claim packs | Read/write-back with approval | Phase 2 | Avoid direct write-back in earliest MVP unless confirmed. |
| CRM | Customer profile, interactions, leads, opportunities | Read/write-back with approval | Phase 2 | Useful for customer acquisition and retention. |
| Underwriting system | Submission data, risk notes, underwriting status | Read/export | Phase 2 | Underwriting Copilot is not core claims MVP. |
| Finance/payment system | Settlement/payment status, receipts, premium status | Read/write with strict approval | Later | Payment release requires human/finance approval. |
| Document management system | Import policy docs, claim evidence, generated evidence packs | Read/write | Phase 2 | SharePoint may be common. |
| Regulatory/IRA exports | Claims register, complaint records, audit packs | Export | Later | Format must be legally confirmed. |

### 6.3 Microsoft 365 Integrations

| Integration | Purpose | Direction | MVP Priority | Notes |
|---|---|---:|---:|---|
| Microsoft Entra ID | Staff authentication, RBAC, SSO | Identity | Critical | Production auth must be server-enforced. |
| Microsoft Graph | Outlook/Exchange, users, shared mailbox, possible Teams/SharePoint | Read/write per scope | High | Least privilege and admin consent required. |
| Outlook / Exchange Online | Email ingestion and approved replies | Two-way | High | Shared claims mailbox can be a practical first source. |
| SharePoint / OneDrive | Document import/export, policy docs, evidence packs | Read/write | Phase 2 | Useful where insurers store docs in Microsoft 365. |
| Teams | Internal notifications, escalation alerts | Outbound | Optional | Good for operations teams. |
| Power BI | Reports and dashboards | Read/report | Phase 2 | Advanced analytics after core workflows. |
| Microsoft Fabric / Dataverse | Cloud mirror, analytics, enterprise integration | Phase 2/3 | To confirm | Useful but should not be overcommitted in MVP. |

### 6.4 Azure Platform Integrations

| Azure Service | Role in IIP | MVP Priority |
|---|---|---:|
| Azure SQL | Canonical operational store | Critical |
| Azure Blob Storage | Documents, evidence files, exports | Critical |
| Azure AI Document Intelligence | OCR, extraction, classification | Critical |
| Azure AI Search | RAG, policy/SOP/document retrieval | Critical |
| Azure OpenAI | Summaries, explanations, drafting, agent reasoning | Critical |
| Azure Functions | Event handlers, background jobs, connector workers | Critical |
| Durable Functions | Long-running workflows and human-in-loop processes | High |
| Azure Service Bus | Async processing, retries, dead-letter queues | Critical |
| Event Grid | Event notifications and storage triggers | Optional/High |
| Azure API Management | API gateway, policies, rate limits, auth | High |
| Azure Key Vault | Secrets, certificates, credentials | Critical |
| Managed Identity | Secure service-to-service auth | Critical |
| Application Insights | Telemetry and traces | Critical |
| Log Analytics | Central logs | Critical |
| Microsoft Sentinel | Security monitoring | Phase 2/Enterprise |

---

## 7. MVP Integration Scope

### 7.1 MVP Goal

The MVP integration goal is to support an end-to-end motor claims and customer acquisition workflow without requiring deep custom integration into every insurer system.

### 7.2 MVP Included Integrations

| Integration | MVP Use |
|---|---|
| WhatsApp Business API | Customer initiates claim or lead conversation; uploads documents/photos; receives missing-document prompts. |
| Microsoft Graph / Outlook | Shared mailbox ingestion for claims, documents, customer communications, and approved outbound updates. |
| Azure Blob Storage | Store original documents, extracted evidence, and exported claim packs. |
| Azure AI Document Intelligence | Classify and extract fields from motor claim documents. |
| Azure AI Search | Retrieve approved policy, claims, SOP, FAQ, and checklist knowledge. |
| Azure SQL | Store canonical customers, claims, documents, leads, conversations, tasks, audit events, and integration events. |
| Azure Service Bus | Queue document processing, channel events, extraction jobs, and write-back requests. |
| Export interface | Generate PDF/CSV/JSON claim packs for manual or semi-automated downstream ingestion. |

### 7.3 MVP Excluded Integrations

The following should not be mandatory for the first MVP:

- Direct core policy administration write-back.
- Direct claims management system write-back.
- Payment release integration.
- Full mobile money payout integration.
- IRA direct API integration.
- Full Microsoft Fabric analytics.
- Full offline platform replication.
- Voice/call center real-time integration.
- Automatic underwriting rating integration.
- National ID or vehicle registry integration.

### 7.4 MVP Fallback Strategy

If insurer APIs are unavailable, IIP should still operate using:

- Secure upload of policy/customer extracts.
- CSV/Excel import.
- Shared mailbox ingestion.
- Manual policy lookup fields.
- Manual customer verification fields.
- Exportable claim packs.
- Controlled admin configuration.

This allows IIP to prove value before complex system integration.

---

## 8. Canonical Insurance Data Layer

The canonical data layer is the normalized model that allows IIP to work across different insurers even when their source systems differ.

### 8.1 Core Canonical Entities

| Entity | Purpose |
|---|---|
| Tenant | The insurer or deployment boundary. |
| Workspace | Business unit, branch, region, product line, or operational unit. |
| User | Staff or platform user. |
| Role | Permission grouping. |
| Customer | Policyholder, claimant, prospect, beneficiary, broker-managed client. |
| Policy | Policy reference, product, coverage, limits, status. |
| Claim | Claim record, FNOL, status, SLA, assignment, risk, documents. |
| Document | Uploaded or imported file and its processing status. |
| ExtractedField | OCR/extracted data from documents with confidence and evidence. |
| ChannelConversation | External customer/broker conversation. |
| SalesLead | Acquisition opportunity created from channel interaction. |
| WorkflowCase | Structured operational case across claims, leads, service, or underwriting. |
| Task | Action assigned to a user/team. |
| ApprovalRequest | Human approval object for sensitive actions. |
| AuditEvent | Immutable record of actions and decisions. |
| IntegrationEvent | Inbound/outbound integration transaction. |
| WriteBackRequest | Human-approved request to update or export to downstream system. |
| KnowledgeSource | Approved documents indexed for retrieval. |
| ConsentRecord | Purpose-specific consent and processing basis. |

### 8.2 Canonical Entity Requirements

Every canonical entity should include:

- id
- tenantId
- workspaceId
- status
- createdAt
- updatedAt
- createdBy or sourceSystem
- correlationId where applicable
- dataClassification where applicable
- audit references where applicable

### 8.3 Source-to-Canonical Mapping

| Source Object | Canonical Entity |
|---|---|
| WhatsApp message | ChannelConversation, Interaction, Document if media attached |
| Email with claim attachments | ChannelConversation or Interaction, Claim, Document |
| Website lead form | SalesLead, Customer/Prospect |
| Broker spreadsheet | SalesLead, Customer, Policy, Claim depending on file type |
| Policy admin export | Policy, Customer |
| Claims system export | Claim, Task, AuditEvent |
| Uploaded police report | Document, ExtractedField, Evidence |
| AI generated claim summary | ClaimSummary, AuditEvent |
| Human field correction | ExtractedField update, AuditEvent |
| Approved customer update | Interaction, AuditEvent, outbound IntegrationEvent |

---

## 9. Integration Patterns

### 9.1 API Integration Pattern

Use when the external system exposes a stable API.

```text
External System API
       |
       v
Azure API Management / Connector Function
       |
       v
Validation + Tenant Scope + Mapping
       |
       v
Azure SQL Canonical Store
       |
       v
Workflow / AI / UI
```

Required controls:

- OAuth2, managed identity, API key, or certificate auth as appropriate.
- Secrets in Key Vault.
- Per-tenant connector configuration.
- Rate limiting.
- Retries.
- Idempotency keys.
- Audit logging.
- Integration health monitoring.

### 9.2 Webhook Integration Pattern

Use for WhatsApp, website forms, external event notifications, and future broker channels.

```text
Webhook Provider
       |
       v
Webhook Receiver
       |
       v
Verify Signature / Token
       |
       v
Store Raw Event
       |
       v
Queue Event
       |
       v
Process and Normalize
       |
       v
Create Conversation / Lead / Claim / Document
```

Webhook handlers must return quickly and process asynchronously.

### 9.3 Email Ingestion Pattern

Use for shared mailboxes, claims inboxes, broker submissions, and customer documents.

```text
Exchange Online Shared Mailbox
       |
       v
Microsoft Graph Connector
       |
       v
Email Metadata + Attachments
       |
       v
Blob Storage + Canonical Interaction
       |
       v
Document Processing Queue
       |
       v
Claim/Lead/Task Routing
```

Email integration should support:

- Shared mailbox selection.
- Sender allow/block lists.
- Attachment extraction.
- Duplicate detection.
- Conversation threading.
- Manual classification when uncertain.
- Approved outbound replies.

### 9.4 File Import Pattern

Use when APIs do not exist.

```text
CSV/Excel/PDF Upload
       |
       v
Upload Validation
       |
       v
Staging Table / Raw Blob
       |
       v
Mapping Rules
       |
       v
Human Review of Import Summary
       |
       v
Canonical Store
```

File imports are important for African insurance environments where core systems may be old, closed, or inconsistently integrated.

### 9.5 Event Queue Pattern

Use for long-running or unreliable operations.

```text
Application Event
       |
       v
Service Bus Queue
       |
       v
Worker Function
       |
       v
Success -> AuditEvent
       |
       v
Failure -> Retry -> Dead Letter -> Support Queue
```

Use queues for:

- Document processing.
- OCR/extraction jobs.
- AI summary generation.
- Outbound WhatsApp messages.
- Email sends.
- Export pack generation.
- Write-back requests.

### 9.6 Controlled Write-back Pattern

Use only when writing to an insurer system is necessary and approved.

```text
IIP Prepared Payload
       |
       v
Validation and Evidence Check
       |
       v
Human Approval
       |
       v
WriteBackRequest Created
       |
       v
Connector Executes Update
       |
       v
External Response Stored
       |
       v
IntegrationEvent + AuditEvent Stored
```

Write-back examples:

- Add claim note.
- Update missing-document status.
- Create CRM follow-up task.
- Send approved customer update.
- Export reviewed claim pack.
- Create renewal reminder.

Write-back non-goals:

- Automatic claim approval.
- Automatic rejection.
- Automatic payment release.
- Automatic policy binding.
- Automatic premium change.

### 9.7 Hybrid Cloud Mirror / Resilient Channel Buffer Pattern

Use where client connectivity is unreliable.

The MVP should describe this as a **resilient channel buffer**, not a full offline system.

```text
Customer Channel Event
       |
       v
Cloud Webhook Receiver
       |
       v
Queue + Persist Raw Event
       |
       v
Process When Services Available
       |
       v
Sync Operational State
```

For customer offices with local infrastructure requirements, a future pattern may include:

- Local receiver.
- Local Redis or disk-backed queue.
- Secure sync agent.
- Cloud replay and reconciliation.
- Monitoring of unsynced events.

This should be considered Phase 2/enterprise unless explicitly required for the pilot.

---

## 10. Key Integration Workflows

### 10.1 WhatsApp FNOL to Claim Intake

```text
Customer sends WhatsApp message
       |
       v
WhatsApp provider sends webhook to IIP
       |
       v
Webhook validated and raw event stored
       |
       v
Conversation created or updated
       |
       v
FNOL agent identifies claim intent
       |
       v
IIP asks structured questions
       |
       v
Customer uploads documents/photos
       |
       v
Files stored in Blob Storage
       |
       v
Document processing jobs queued
       |
       v
OCR/extraction/classification performed
       |
       v
Claim record and checklist updated
       |
       v
Claims officer reviews case
       |
       v
Approved customer update sent
       |
       v
Audit trail stored
```

### 10.2 Shared Mailbox to Claim or Lead

```text
Email arrives in claims/sales mailbox
       |
       v
Graph connector detects new message
       |
       v
Email metadata and attachments extracted
       |
       v
Sender and subject classified
       |
       v
Conversation/Interaction created
       |
       v
Attachments stored and queued for Document Intelligence
       |
       v
IIP suggests claim, lead, complaint, renewal, or policy inquiry route
       |
       v
Staff confirms route where confidence is low
       |
       v
Task or workflow case created
```

### 10.3 Document Upload to Extraction Review

```text
User uploads claim document
       |
       v
Original file stored in Blob Storage
       |
       v
Document record created
       |
       v
Processing event queued
       |
       v
Document Intelligence extracts text, layout, and fields
       |
       v
Document Classification Agent identifies type
       |
       v
Field Extraction Agent extracts values with confidence
       |
       v
Validation rules check completeness and consistency
       |
       v
Low-confidence fields route to human review
       |
       v
Approved fields update canonical claim record
       |
       v
Audit events stored
```

### 10.4 Policy Knowledge Retrieval

```text
Staff asks policy/coverage question
       |
       v
IIP identifies tenant, workspace, product, policy context
       |
       v
Azure AI Search retrieves approved documents
       |
       v
Policy Intelligence Agent drafts grounded answer
       |
       v
Answer includes sources, confidence, and limitations
       |
       v
Sensitive/ambiguous answer routes to human review
       |
       v
Prompt, sources, output, and user action are logged
```

### 10.5 Lead Creation from Channel Conversation

```text
Prospect asks about insurance on WhatsApp/web/email
       |
       v
ChannelConversation created
       |
       v
Intent classified as quote/product interest
       |
       v
SalesLead created
       |
       v
Product interest and missing information identified
       |
       v
Lead assigned to sales officer/broker team
       |
       v
Customer-safe follow-up drafted
       |
       v
Human sends or approves response
```

### 10.6 Controlled Export to Downstream System

```text
Claim review completed in IIP
       |
       v
Evidence pack generated
       |
       v
Officer selects export or write-back
       |
       v
Payload is validated
       |
       v
Supervisor approval required where configured
       |
       v
Export generated as PDF/CSV/JSON/API payload
       |
       v
IntegrationEvent stored
       |
       v
External system response or manual confirmation logged
```

---

## 11. Required Internal API Surface

### 11.1 Existing/Planned V1 Workspace Endpoints

Implementation notes already identify V1 internal workspace endpoints:

```text
GET /api/v1/channels/overview
GET /api/v1/customers/overview
GET /api/v1/sales-leads/overview
```

These endpoints support the internal insurer workspaces for channel operations, customer 360, and acquisition work.

### 11.2 Recommended Integration API Groups

| API Group | Example Endpoints |
|---|---|
| Channels | `GET /api/v1/channels/overview`, `GET /api/v1/channels/conversations/{id}`, `POST /api/v1/channels/conversations/{id}/assign` |
| Customers | `GET /api/v1/customers/overview`, `GET /api/v1/customers/{id}`, `GET /api/v1/customers/{id}/timeline` |
| Sales & Leads | `GET /api/v1/sales-leads/overview`, `GET /api/v1/sales-leads/{id}`, `POST /api/v1/sales-leads/{id}/assign` |
| Claims | `GET /api/v1/claims`, `POST /api/v1/claims`, `GET /api/v1/claims/{id}`, `POST /api/v1/claims/{id}/assign` |
| Documents | `POST /api/v1/documents/upload`, `GET /api/v1/documents/{id}`, `POST /api/v1/documents/{id}/review` |
| Integrations | `GET /api/v1/integrations/status`, `POST /api/v1/integrations/{id}/test`, `GET /api/v1/integrations/events` |
| Webhooks | `POST /api/v1/webhooks/whatsapp`, `POST /api/v1/webhooks/webchat`, `POST /api/v1/webhooks/external` |
| Write-back | `POST /api/v1/writeback/requests`, `POST /api/v1/writeback/requests/{id}/approve`, `GET /api/v1/writeback/requests/{id}` |
| Audit | `GET /api/v1/audit/events`, `GET /api/v1/audit/cases/{caseId}` |
| Admin | `GET /api/v1/admin/provider-health`, `GET /api/v1/admin/connector-configs`, `POST /api/v1/admin/connector-configs` |

### 11.3 API Requirements

All production APIs must enforce:

- Authentication.
- Server-side authorization.
- Tenant/workspace scoping.
- Correlation ID propagation.
- Structured error responses.
- Pagination for list endpoints.
- Audit logging for sensitive actions.
- No reliance on browser-only permissions.
- No mock bypass in staging/production.

---

## 12. Connector Configuration Model

Each integration connector should be configured per tenant.

### 12.1 ConnectorConfig Fields

| Field | Description |
|---|---|
| connectorId | Unique connector identifier. |
| tenantId | Owning tenant. |
| workspaceId | Optional workspace scope. |
| connectorType | whatsapp, graph, api, sftp, file, sms, core_policy, claims_system, etc. |
| displayName | Human-readable name. |
| status | connected, degraded, disconnected, not_configured. |
| authType | oauth2, api_key, certificate, managed_identity, basic, none. |
| secretRef | Key Vault reference, not raw secret. |
| baseUrl | External endpoint where relevant. |
| scopes | Permission scopes granted. |
| allowedOperations | read, write, send, export, import, webhook. |
| dataClassification | Public, internal, confidential, sensitive, regulated. |
| retryPolicy | Retry/backoff settings. |
| timeoutSeconds | Request timeout. |
| lastSyncAt | Last successful sync. |
| healthStatus | Current health. |
| createdAt / updatedAt | Metadata. |

### 12.2 Connector Health Status

Connector status should be visible in Admin and Support & Health.

| Status | Meaning |
|---|---|
| connected | Last test succeeded and recent operations are healthy. |
| degraded | Some failures or delays are occurring. |
| disconnected | Connector cannot reach or authenticate external service. |
| not_configured | Required settings or credentials are missing. |

---

## 13. Integration Events and Audit Events

### 13.1 IntegrationEvent

IntegrationEvents track system-to-system activity.

Example event types:

- webhook_received
- email_ingested
- document_uploaded
- document_processing_started
- document_processing_completed
- external_api_called
- export_generated
- writeback_requested
- writeback_approved
- writeback_completed
- writeback_failed
- connector_health_changed

Required fields:

- integrationEventId
- tenantId
- workspaceId
- sourceSystem
- eventType
- externalRecordId
- internalRecordType
- internalRecordId
- direction: inbound/outbound/internal
- status: received/processing/succeeded/failed/dead_lettered
- rawPayloadRef where appropriate
- errorCode
- errorMessage
- correlationId
- createdAt

### 13.2 AuditEvent

AuditEvents track user, AI, and business-sensitive actions.

Example event types:

- user_viewed_claim
- ai_summary_generated
- ai_sources_retrieved
- field_extracted
- field_edited
- field_approved
- claim_assigned
- claim_escalated
- customer_update_drafted
- customer_update_approved
- export_generated
- approval_completed
- fraud_signal_reviewed

AuditEvents must not be silently deleted or altered.

---

## 14. Security and Privacy Requirements

### 14.1 Authentication and Authorization

- Staff authentication should use Microsoft Entra ID.
- API authorization must be server-side.
- RBAC must be enforced on every sensitive endpoint.
- Attribute-based checks should include tenant, workspace, role, branch, claim type, and document sensitivity.
- Platform/admin support access must be tightly controlled and logged.

### 14.2 Secrets and Credentials

- No secrets in frontend code.
- No secrets in repository.
- No secrets in logs.
- All secrets should be stored in Azure Key Vault.
- Managed Identity should be preferred for Azure service access.
- Connector credentials should be per-tenant and revocable.

### 14.3 Data Protection

- Store only necessary customer and claim data.
- Use encryption at rest and in transit.
- Mask sensitive fields in UI where appropriate.
- Restrict document access by role.
- Preserve consent and purpose of processing.
- Support retention policy configuration.
- Avoid using real PII in demo/training datasets.

### 14.4 AI and Integration Boundary

AI agents may use scoped tools only. They may not:

- Query arbitrary databases.
- Cross tenant boundaries.
- Send unapproved customer messages.
- Write to external insurer systems without approval.
- Access documents outside the user's role and workflow context.

---

## 15. Error Handling, Retries, and Resilience

### 15.1 Required Failure States

Every integration should support:

- success
- pending
- processing
- retrying
- failed
- dead_lettered
- cancelled
- manually_resolved

### 15.2 Retry Policy

Recommended default:

- Retry transient failures with exponential backoff.
- Do not retry validation failures without correction.
- Dead-letter after configured retry count.
- Notify Support & Health dashboard for dead-lettered events.
- Allow authorized users to replay failed events.

### 15.3 Idempotency

Webhook and API events should be idempotent. Duplicate events must not create duplicate claims, leads, documents, or customer updates.

Recommended idempotency keys:

- provider event ID
- message ID
- file hash
- external record ID
- tenant ID + source system + external ID

### 15.4 Raw Payload Storage

For audit and replay, raw external payloads may be stored securely in Blob Storage with references in IntegrationEvent. Sensitive payloads must be access controlled and retained according to policy.

---

## 16. Integration Health and Observability

### 16.1 Health Dashboard

The Support & Health area should show:

- Connector status.
- Last successful sync.
- Failed events.
- Queue backlog.
- Dead-letter counts.
- Document processing failures.
- AI provider status.
- Graph/Outlook status.
- WhatsApp provider status.
- Average processing time.
- Retry counts.

### 16.2 Monitoring Signals

Use Application Insights and Log Analytics for:

- API request traces.
- Dependency calls.
- Worker failures.
- Queue processing metrics.
- Latency.
- Exceptions.
- Token/cost telemetry where allowed.
- Correlation ID tracing.

### 16.3 Alerts

Recommended alerts:

- WhatsApp webhook failure rate above threshold.
- Shared mailbox sync failure.
- Document processing queue backlog.
- Dead-letter queue non-empty for critical queues.
- AI provider unavailable.
- Graph authentication expired.
- Blob upload failures.
- SQL connection failures.
- Sudden spike in outbound message failures.

---

## 17. UI Implications of Integration Strategy

### 17.1 Channels Workspace

Channels is the unified internal inbox for external interactions.

It should show:

- Customer display name.
- Channel type.
- Intent.
- AI confidence.
- Suggested routing team.
- Status.
- Assigned officer.
- Last message preview.
- Last activity.
- Work item link.
- Channel health.

### 17.2 Customers Workspace

Customers is an internal Customer 360 view, not a customer portal.

It should show:

- Identity details.
- Policies.
- Claims.
- Leads.
- Interactions.
- Documents.
- Consent.
- Complaints.
- Last interaction.
- AI summary.
- Recommended next action.

### 17.3 Sales & Leads Workspace

Sales & Leads manages acquisition work created from external channels.

It should show:

- Lead source.
- Product interest.
- Status.
- AI confidence.
- Product fit summary.
- Missing information count.
- Assigned owner.
- Quote value where available.
- Next action.

### 17.4 Claims Workspace

Claims integration should show:

- Claim source.
- Policy lookup status.
- Document status.
- Extraction status.
- Missing documents.
- SLA status.
- AI summary.
- Fraud signals.
- Assignment.
- Approval requirements.
- Export/write-back status.

### 17.5 Admin and Support

Admin and Support should show:

- Connector configuration.
- Provider health.
- Integration events.
- Failed events.
- Replay controls.
- Credentials status without exposing secrets.
- Last sync.
- Not configured states.

---

## 18. Integration Phasing Roadmap

### 18.1 Phase 0 - Local/Developer Readiness

- Define canonical entities.
- Implement tenant/workspace context.
- Implement integration event model.
- Implement audit event model.
- Add no-mock-production guardrails.
- Provide local mock provider only for development.

### 18.2 Phase 1 - MVP Integration Foundation

- WhatsApp provider integration or sandbox.
- Microsoft Graph shared mailbox ingestion.
- Blob Storage document upload.
- Document Intelligence extraction pipeline.
- AI Search knowledge retrieval.
- Azure SQL canonical store.
- Service Bus processing queues.
- Channels overview endpoint.
- Customers overview endpoint.
- Sales & Leads overview endpoint.
- Claims intake and document workflow.
- Export PDF/CSV/JSON claim pack.
- Integration health dashboard.

### 18.3 Phase 2 - Enterprise Pilot Integrations

- Policy administration read-only connector.
- Claims system export/write-back connector.
- SharePoint import/export.
- Teams escalation alerts.
- Power BI reporting placeholders or embedded reports.
- Underwriting submission intake.
- Early warning/retention data feeds.
- Broker upload workflow.
- Connector configuration UI.

### 18.4 Phase 3 - Advanced and Regulated Integrations

- Mobile money/payment status.
- Claims payout workflow integration with strict approval.
- Regulatory claims/complaints export.
- Microsoft Fabric analytics.
- Dataverse/CRM integration.
- National ID/vehicle registry integration if legally approved.
- Full Azure Managed Application packaging.
- Advanced Cloud Mirror/local buffer options.

### 18.5 Phase 4 - Multi-Country / Multi-Product Scale

- Country-specific regulatory configurations.
- Multi-language channel expansion.
- Multi-product claim types.
- Reinsurance reporting.
- Voice/call center integration.
- Advanced fraud intelligence integration.
- Marketplace metering and billing integration.

---

## 19. Open Decisions

| ID | Decision | Current Recommendation | Owner |
|---|---|---|---|
| OD-001 | WhatsApp provider | Decide between Meta direct, Twilio, Infobip, or another provider. | Engineering / Product |
| OD-002 | First insurer systems | Confirm the pilot customer's policy, claims, CRM, email, and document systems. | Sales / Delivery |
| OD-003 | Deployment model for first pilot | Prefer Azure Managed Application/private deployment for enterprise pilot; SaaS option for fast demo. | Leadership / Architecture |
| OD-004 | Azure region | South Africa North preferred; West Europe fallback if quotas/services unavailable. | Cloud Architect / Legal |
| OD-005 | Write-back timing | Avoid direct write-back in first MVP unless pilot requires and approves it. | Product / Engineering |
| OD-006 | Cloud Mirror scope | Treat as resilient channel buffer first, not full offline platform. | Architecture |
| OD-007 | Mobile money | Decide whether required in MVP or later. | Product / Sales |
| OD-008 | Regulatory export | Confirm claims register and complaints export format. | Legal / Compliance |
| OD-009 | Fabric/Dataverse | Avoid committing as core MVP dependency until architecture is confirmed. | Architecture |
| OD-010 | Customer portal | Keep V1 internal; customer-facing flows through WhatsApp/web channels. | Product |

---

## 20. Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Core insurer systems have no API | Delays deep integration | Support CSV/Excel, file import, email ingestion, and controlled export. |
| WhatsApp provider not selected | Delays channel MVP | Build provider abstraction and start with sandbox. |
| Azure region lacks required model/quota | Deployment risk | Confirm quotas early; define fallback region. |
| Overpromising full offline mode | Scope creep and architecture complexity | Position MVP as resilient channel buffering only. |
| AI agents access too much data | Security/compliance risk | Use scoped tool registry and server-side authorization. |
| Write-back causes incorrect external updates | Operational and regulatory risk | Require human approval and audit for all write-back. |
| Duplicate webhook/email events | Duplicate claims/leads | Implement idempotency keys and duplicate detection. |
| Inconsistent source data | Poor AI summaries and workflow errors | Use confidence, human review, and source evidence. |
| Legal uncertainty around data residency | Compliance risk | Require legal review before final region commitment. |
| Integration failures hidden from staff | Operational failures | Build Support & Health dashboard from MVP. |

---

## 21. Implementation Backlog

### 21.1 Foundation

- Create ConnectorConfig entity.
- Create IntegrationEvent entity.
- Create WriteBackRequest entity.
- Create AuditEvent enhancements for integrations.
- Add correlation ID middleware.
- Add tenant/workspace enforcement middleware.
- Add provider health service.
- Add dead-letter/replay strategy.

### 21.2 Channels

- Implement `GET /api/v1/channels/overview`.
- Implement WhatsApp webhook receiver.
- Implement channel event normalization.
- Implement conversation assignment.
- Implement channel health statuses.
- Implement conversation-to-work-item conversion.

### 21.3 Customers

- Implement `GET /api/v1/customers/overview`.
- Implement Customer 360 aggregation.
- Link customers to policies, claims, leads, conversations, documents, and consent.
- Implement masked identity fields.
- Implement internal-only access rules.

### 21.4 Sales & Leads

- Implement `GET /api/v1/sales-leads/overview`.
- Implement lead creation from channel events.
- Implement product interest classification.
- Implement missing information tracking.
- Implement assignment and next action.

### 21.5 Microsoft Graph

- Define app registration and scopes.
- Implement shared mailbox connector.
- Ingest messages and attachments.
- Store raw email metadata safely.
- Create document processing jobs.
- Draft outbound replies but require approval.

### 21.6 Documents

- Implement Blob upload and metadata records.
- Queue document processing jobs.
- Integrate Document Intelligence.
- Store extracted fields and confidence.
- Link fields to source evidence.
- Route low confidence to review.

### 21.7 Write-back and Export

- Implement export pack generation.
- Implement WriteBackRequest workflow.
- Implement human approval before external update.
- Store external response and audit events.
- Support PDF, CSV, JSON export first.

### 21.8 Monitoring

- Implement integration health endpoint.
- Track queue backlog.
- Track failed events.
- Add retry/dead-letter metrics.
- Display provider health in Support & Health UI.

---

## 22. Acceptance Criteria

The system integration foundation is acceptable when:

1. Each external event is stored as an IntegrationEvent.
2. Every event is tenant-scoped.
3. Duplicate events do not create duplicate business records.
4. Documents are stored in Blob Storage with metadata in Azure SQL.
5. Document processing is asynchronous and visible to staff.
6. Failed integrations have clear error states.
7. Connector health is visible in the UI.
8. AI agents can only call scoped tools.
9. Write-back requires human approval unless explicitly configured as low-risk and permitted.
10. Production environments use no fake endpoints or mock data.
11. Audit logs capture AI outputs, human approvals, integration actions, and external responses.
12. Channels, Customers, and Sales & Leads operate as internal insurer workspaces.
13. Customer-facing interactions are handled through external channels, not direct customer access to the internal app.
14. Exports can be generated for manual downstream processing before deep system integration exists.

---

## 23. Final Recommended Direction

IIP should integrate in layers:

1. **Start with channels and documents.** WhatsApp, email, and document upload create the fastest path to value.
2. **Normalize into canonical insurance entities.** Do not let each external system shape the whole product.
3. **Use read-only core system access first.** Avoid risky write-back until the pilot workflow is proven.
4. **Make integration health visible.** Integration failures should be surfaced, not hidden.
5. **Keep human approval central.** Integration must never create autonomous regulated insurance decisions.
6. **Support practical fallbacks.** CSV, Excel, email, upload, and export are valid MVP integration paths in African insurance environments.
7. **Prepare for enterprise deployment.** The architecture should support Azure Managed Application/private deployment even if the first demo runs in a lighter hosted environment.

This strategy gives IIP a practical path from MVP to enterprise-grade insurer deployment without overbuilding integrations before the first pilot proves value.

---

## 24. Appendix A - Glossary

| Term | Meaning |
|---|---|
| FNOL | First Notice of Loss; the first report of a claim. |
| RAG | Retrieval-Augmented Generation; AI answers grounded in retrieved approved documents. |
| Write-back | Updating an external insurer system from IIP. |
| Canonical model | IIP's normalized data model across insurers and integrations. |
| IntegrationEvent | Record of a system-to-system integration action. |
| AuditEvent | Record of a user, AI, or sensitive business action. |
| Connector | Configured integration adapter for an external system or service. |
| Dead-letter queue | Queue for events that failed after retries. |
| Tenant | Insurer/customer deployment boundary. |
| Workspace | Sub-boundary such as branch, region, product line, or department. |
| Cloud Mirror | Resilient buffering/sync pattern for unreliable connectivity; not full offline mode by default. |

---

## 25. Appendix B - Integration Decision Matrix

| Integration | MVP | Phase 2 | Phase 3+ | Notes |
|---|---:|---:|---:|---|
| WhatsApp | Yes | Yes | Yes | Primary channel; provider TBD. |
| Email/Outlook | Yes | Yes | Yes | Shared mailbox ingestion. |
| Azure Blob | Yes | Yes | Yes | Document storage. |
| Document Intelligence | Yes | Yes | Yes | OCR/extraction. |
| AI Search | Yes | Yes | Yes | RAG and retrieval. |
| Azure SQL | Yes | Yes | Yes | Canonical store. |
| Policy Admin | Optional/import | Yes | Yes | Read-first. |
| Claims System | Export only | Yes | Yes | Write-back later. |
| SharePoint | Optional | Yes | Yes | Document import/export. |
| Teams | Optional | Yes | Yes | Alerts/escalations. |
| Power BI | No | Yes | Yes | Reporting. |
| SMS | No | Optional | Yes | Fallback channel. |
| Mobile Money | No | Optional | Yes | Payment/premium workflows. |
| IRA Export | No | Optional | Yes | Legal format confirmation needed. |
| Fabric/Dataverse | No | Optional | Yes | Confirm architecture first. |
| Voice/Call Center | No | No | Yes | Later module. |
