# Insurance Intelligence Platform (IIP) - API Specification

**Document Type:** API Specification  
**Product:** Insurance Intelligence Platform (IIP)  
**Prepared For:** Otic / IIP Product, Engineering, Architecture, Security, and Delivery Teams  
**Version:** 1.1 - Chat-First Command Center Update  
**Date:** 30 June 2026  
**Classification:** Internal - Product and Engineering Reference  
**Related Documents:** Master Product Context, PRD, FSD, Technical Architecture Document, System Integration Strategy, UI/UX Handoff Document

---

---

## July 2026 Update - Chat-First Command Center Direction

This version updates the document to align with the approved **chat-first Command Center** direction for **IIP Operations**.

Authoritative design decision:

> The Command Center is not a dashboard with chat added to it. The Command Center is a **chat-first operational command workspace** where metrics, claims, documents, evidence, approvals, actions, and reports appear as structured blocks inside the conversation.

The implementation must preserve the IIP production rules:

- no fake operational records, metrics, mailboxes, users, or provider states in deployed environments;
- all operational answers must be tenant-scoped, role-aware, source-grounded, and auditable;
- the AI may prepare, summarize, classify, recommend, route, and draft, but humans remain responsible for consequential insurance decisions;
- unavailable or unconfigured capabilities must show **`Not configured`** rather than simulated success;
- the approved Light Intelligence Workspace visual system remains in force.

---

## Chat-First Command Center API Update

### Purpose

The Command Center requires backend-owned, tenant-scoped, role-aware APIs that support daily briefing, metric pills, conversational operational queries, structured response blocks, evidence, and auditable actions.

The frontend must not compute or invent operational metrics. All deployed Command Center data must come from real APIs or explicit `Not configured` states.

### Endpoint group: Command Center

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/api/v1/command-center/briefing` | Returns the initial daily operational briefing as typed chat blocks. |
| `GET` | `/api/v1/command-center/metrics` | Returns compact metric pills for the current user's tenant/workspace/role scope. |
| `POST` | `/api/v1/command-center/query` | Accepts a natural-language command and returns structured operational response blocks. |
| `GET` | `/api/v1/command-center/conversations/{conversationId}` | Returns a command conversation history where enabled and authorized. |
| `POST` | `/api/v1/command-center/actions` | Executes a safe command action such as opening a filtered queue, preparing a draft, or creating an approval task. |
| `GET` | `/api/v1/command-center/evidence/{messageId}` | Returns evidence sources and audit references for a command response. |

### Request: command query

```json
{
  "conversationId": "optional-existing-conversation-id",
  "query": "Show me SLA breaches",
  "context": {
    "currentRoute": "/command-center",
    "selectedEntityType": "claim",
    "selectedEntityId": "CLM-2025-005678"
  }
}
```

### Response: command center response

```json
{
  "conversationId": "cmd-123",
  "messageId": "msg-456",
  "createdAt": "2026-07-01T08:46:00Z",
  "tenantId": "tenant-demo",
  "workspaceId": "workspace-claims",
  "blocks": [
    {
      "type": "text",
      "markdown": "Here are the SLA breaches as of now."
    },
    {
      "type": "metric_pills",
      "items": []
    },
    {
      "type": "claims_table",
      "rows": []
    }
  ],
  "evidence": [],
  "confidence": "high",
  "humanReviewRequired": true,
  "auditEventId": "audit-789"
}
```

### Supported block types

| Type | Description |
|---|---|
| `text` | Markdown-safe narrative response. |
| `metric_pills` | Compact operational metrics. |
| `claims_table` | Filtered claims result table. |
| `sla_summary` | SLA risk and breach summary. |
| `document_gap_list` | Missing/incomplete document list. |
| `approval_queue` | Human approval items. |
| `draft_communication` | Customer-safe draft communication requiring approval where applicable. |
| `management_summary` | Executive/manager summary. |
| `evidence_list` | Sources, timestamps, confidence, audit IDs. |
| `action_buttons` | Safe actions exposed to authorized users. |
| `not_configured` | Provider/module unavailable or not configured. |
| `error` | Safe operational error with correlation ID. |

### Security and authorization rules

- Every endpoint must require authentication.
- Every result must be scoped by tenant, workspace, role, assignment, product line, and branch where applicable.
- The API must not trust browser-selected role or workspace values as authority.
- The API must not return internal risk notes, fraud flags, reserves, prompts, system messages, tokens, secrets, or other tenants' data to unauthorized users.
- Every consequential action must create or route to a human approval workflow.
- Command queries and responses that affect operations must be auditable.

## 1. Executive Summary

This document defines the initial API specification for the **Insurance Intelligence Platform (IIP)**. It consolidates the API direction discussed across the project context, PRD, FSD, Technical Architecture Document, System Integration Strategy, UI/UX Handoff Document, current implementation notes, and coding-agent requirements.

IIP is an Azure-native, human-reviewed insurance operations platform. Its APIs must support internal insurer workspaces, customer/channel ingestion, claims intake, document intelligence, AI-assisted summaries, human review, audit logging, role-aware dashboards, integration health, and controlled export/write-back into downstream insurer systems.

The API design must preserve IIP's most important governance principle:

> **AI prepares insurance work. Humans make consequential insurance decisions.**

Therefore, APIs may support AI extraction, AI summary generation, missing-document detection, customer-safe draft creation, fraud-signal flagging, routing suggestions, and evidence-pack preparation. APIs must not directly approve claims, reject claims, bind policies, set final premiums, accuse customers of fraud, release payments, or make final underwriting decisions without explicit human-owned workflow actions.

The MVP API scope should prioritize:

- Authentication context and current user/tenant profile.
- Command Center metrics and suggested actions.
- Channels unified inbox.
- Customers internal Customer 360.
- Sales & Leads internal acquisition workspace.
- Claims work queue and motor claim case review.
- FNOL/new claim intake.
- Document upload, classification, extraction, and review.
- Knowledge Base and policy Q&A over approved sources.
- Human approvals.
- Compliance and audit logs.
- Integration health and provider configuration.
- Support and system health.

This document uses REST-style endpoints under `/api/v1`. It is not a complete OpenAPI YAML file yet, but it is structured so engineering can convert it into an OpenAPI 3.1 specification.

---

## 2. API Design Principles

### 2.1 Core API Principles

| Principle | Requirement |
|---|---|
| Versioned APIs | All product APIs should use `/api/v1/...` for the MVP. |
| Tenant-aware | Every request must resolve tenant and workspace context server-side. |
| Server-side authorization | No sensitive permission check may rely only on frontend logic. |
| No mock production data | Staging and production must not use fake endpoints or hardcoded mock payloads. |
| Human approval boundary | APIs must separate AI suggestions from human decisions. |
| Audit first | Important reads, writes, AI outputs, exports, and approvals must create audit events. |
| RAG/source grounding | AI responses that affect policies, claims, or customers must cite approved sources or evidence IDs. |
| Controlled write-back | Downstream writes must be explicit, permissioned, idempotent, and auditable. |
| Least privilege | API permissions should be role-scoped and action-specific. |
| Safe failure | Failed integrations should create visible error states, retry records, and support diagnostics. |

### 2.2 Data Handling Principles

IIP APIs must treat insurance information as sensitive by default. Customer identity data, claims evidence, policy records, documents, communications, AI outputs, and audit records require careful handling.

Required controls:

- Do not return data from another tenant or workspace.
- Do not expose internal fraud-signal details to customer-facing channels.
- Do not expose insurer-only comments to customers, brokers, or unauthorized staff.
- Do not return raw secrets, access tokens, connection strings, or provider keys.
- Mask sensitive identifiers where full value is not needed.
- Log access to sensitive records.
- Store file metadata separately from file content.
- Preserve source evidence IDs for AI outputs.

---

## 3. API Base URLs and Versioning

### 3.1 Base URL Pattern

```text
https://{environment-host}/api/v1
```

Example environment patterns:

| Environment | Example Base URL |
|---|---|
| Local development | `https://localhost:5001/api/v1` or proxied Next.js route |
| Staging | `https://iip-staging.{domain}/api/v1` |
| Production SaaS | `https://iip.{domain}/api/v1` |
| Private deployment | Customer-specific hostname or private endpoint |

### 3.2 Versioning Rules

- MVP endpoints use `/api/v1`.
- Breaking changes require `/api/v2`.
- Non-breaking additions may be added to v1.
- Deprecated fields should remain for at least one release cycle.
- API responses should include stable IDs and not depend on UI labels as identifiers.

---

## 4. Authentication and Authorization

### 4.1 Authentication Model

The primary staff authentication model is **Microsoft Entra ID**.

Supported identity modes:

| Mode | Purpose | MVP Status |
|---|---|---|
| Entra ID staff login | Internal insurer staff authentication | Required |
| Otic/platform admin access | Support and deployment administration | Required |
| Customer verification via OTP or channel verification | WhatsApp/customer-safe claim status | Later / limited MVP |
| Broker/agent access | External partner access | Later unless pilot requires |

### 4.2 Required Context Headers

The backend must resolve tenant/workspace context from authenticated claims and server-side membership. During local/demo development, the following headers may exist, but production must not rely on browser-owned trust alone.

```http
X-Tenant-Id: tenant-demo
X-Workspace-Id: workspace-claims
X-User-Id: local-admin
X-Correlation-ID: 7f4d2e7b-5c1b-45d4-93af-49e0873a4f21
```

Known development/demo headers from earlier implementation context:

```http
x-demo-role: Admin
x-demo-user-id: local-admin
```

Production rule:

> Demo role headers are not an authorization mechanism in staging or production. Role and tenant membership must be resolved from authenticated identity, server-side claims, database membership, and policy checks.

### 4.3 Authorization Model

Permissions must be evaluated using:

- Tenant membership.
- Workspace membership.
- Role.
- Case ownership/assignment.
- Workflow stage.
- Action type.
- Data sensitivity.
- Approval limit where applicable.

Representative roles:

| Role | API Access Pattern |
|---|---|
| Claims Officer | Assigned claims, document review, notes, customer update drafts, request missing documents. |
| Claims Supervisor | Team queue, escalation review, approvals, reassignment, SLA management. |
| Customer Service Agent | Customer-safe information, channels inbox, customer update drafting, policy Q&A. |
| Underwriter | Underwriting submissions and future underwriting support modules. |
| Compliance Officer | Audit logs, AI outputs, human decisions, exports, complaint records. |
| Insurer Admin | Users, roles, workflows, provider settings, document templates. |
| Executive / Manager | Dashboards, reports, operational summaries; limited case detail depending on role. |
| Otic Platform Admin | Tenant setup, support, provider health; restricted production data access. |
| IT / Security Admin | Identity, integration, health, secrets metadata, logs, provider status. |

---

## 5. Common Request and Response Conventions

### 5.1 Content Types

```http
Content-Type: application/json
Accept: application/json
```

For file upload:

```http
Content-Type: multipart/form-data
```

For file download/export:

```http
Accept: application/pdf
Accept: text/csv
Accept: application/json
```

### 5.2 Common Response Envelope

For most JSON endpoints, use this pattern:

```json
{
  "data": {},
  "meta": {
    "requestId": "req_01H...",
    "correlationId": "7f4d2e7b-5c1b-45d4-93af-49e0873a4f21",
    "tenantId": "tenant-demo",
    "workspaceId": "workspace-claims",
    "generatedAt": "2026-06-30T13:00:00Z"
  }
}
```

For lists:

```json
{
  "data": [],
  "pagination": {
    "page": 1,
    "pageSize": 25,
    "totalCount": 102,
    "totalPages": 5
  },
  "meta": {
    "requestId": "req_01H...",
    "correlationId": "..."
  }
}
```

### 5.3 Error Response Format

All API errors should use a consistent shape.

```json
{
  "error": {
    "code": "CLAIM_NOT_FOUND",
    "message": "The requested claim could not be found or is not available in this workspace.",
    "details": null,
    "target": "claimId",
    "traceId": "00-...",
    "correlationId": "7f4d2e7b-5c1b-45d4-93af-49e0873a4f21"
  }
}
```

### 5.4 Standard HTTP Status Codes

| Status | Meaning |
|---:|---|
| 200 | Successful read or action. |
| 201 | Resource created. |
| 202 | Accepted for asynchronous processing. |
| 204 | Successful action with no response body. |
| 400 | Invalid input or business-rule violation. |
| 401 | Authentication required or token invalid. |
| 403 | Authenticated but not authorized. |
| 404 | Resource not found or not visible to current tenant/workspace. |
| 409 | Conflict, duplicate idempotency key, invalid workflow transition. |
| 422 | Validation passed syntactically but failed domain validation. |
| 429 | Rate limited. |
| 500 | Internal server error. |
| 502 | Upstream provider/integration failure. |
| 503 | Provider unavailable or not configured. |

### 5.5 Idempotency

Mutating operations that may be retried must support idempotency.

```http
Idempotency-Key: fnol_20260630_0001
```

Required for:

- Claim creation from channels.
- Document upload registration.
- Customer communication sends.
- Export/write-back requests.
- Payment-related future workflows.
- Integration event processing.

---

## 6. Core Data Types

### 6.1 Canonical Entity IDs

| Entity | Example ID Pattern |
|---|---|
| Tenant | `tenant_01J...` |
| Workspace | `workspace_claims` |
| Customer | `cus_01J...` |
| Policy | `pol_2026_000123` |
| Claim | `clm_2026_000123` |
| Document | `doc_01J...` |
| Channel Conversation | `conv_01J...` |
| Lead | `lead_01J...` |
| Task | `task_01J...` |
| Approval | `appr_01J...` |
| Audit Event | `audit_01J...` |
| Integration Event | `intev_01J...` |
| AI Run | `airun_01J...` |

### 6.2 Common Status Enumerations

Claim statuses:

```text
new
intake_started
documents_pending
documents_uploaded
ai_processing
needs_review
missing_information
fraud_signal_detected
escalated
approved_for_next_workflow_step_by_human
returned_to_customer
exported
closed
```

Document statuses:

```text
required
uploaded
classifying
classified
extracting
extracted
needs_review
accepted
rejected
unreadable
expired
mismatched
duplicate
archived
```

Channel statuses:

```text
new
assigned
in_review
waiting_customer
waiting_officer
converted_to_work_item
closed
escalated
```

Approval statuses:

```text
pending
approved
rejected
overridden
escalated
cancelled
expired
```

Provider health statuses:

```text
connected
degraded
disconnected
not_configured
unknown
```

---

## 7. Health and Provider Status APIs

These endpoints support local, staging, and production monitoring.

### 7.1 Platform Health

```http
GET /health
GET /health/live
GET /health/ready
```

Response example:

```json
{
  "status": "healthy",
  "service": "iip-api",
  "version": "1.0.0",
  "environment": "staging",
  "checkedAt": "2026-06-30T13:00:00Z"
}
```

### 7.2 Provider Health

```http
GET /api/admin/provider-health
GET /api/v1/support/health/providers
```

Purpose:

- Check Azure OpenAI status.
- Check Azure AI Search status.
- Check Azure AI Document Intelligence status.
- Check Blob Storage status.
- Check Azure SQL connectivity.
- Check Microsoft Graph configuration.
- Check WhatsApp provider configuration.
- Check Service Bus queue health.

Response example:

```json
{
  "data": {
    "overallStatus": "degraded",
    "providers": [
      {
        "provider": "azure_openai",
        "displayName": "Azure OpenAI",
        "status": "connected",
        "lastCheckedAt": "2026-06-30T13:00:00Z",
        "latencyMs": 220,
        "message": "Connected"
      },
      {
        "provider": "microsoft_graph",
        "displayName": "Microsoft Graph",
        "status": "not_configured",
        "lastCheckedAt": null,
        "latencyMs": null,
        "message": "Provider not configured for this tenant"
      }
    ]
  }
}
```

---

## 8. Current User and Tenant APIs

### 8.1 Get Current User Profile

```http
GET /api/v1/me
```

Purpose:

Return the authenticated user's tenant, workspace, roles, permissions, and feature flags.

Response example:

```json
{
  "data": {
    "userId": "usr_01J...",
    "displayName": "Jane Claims Officer",
    "email": "jane@example.com",
    "tenantId": "tenant-demo",
    "workspaceId": "workspace-claims",
    "roles": ["claims_officer"],
    "permissions": [
      "claims.read",
      "claims.update_assigned",
      "documents.review",
      "customer_updates.draft"
    ],
    "featureFlags": {
      "channels": true,
      "salesLeads": true,
      "documentIntelligence": true,
      "underwritingSupport": false,
      "earlyWarning": false
    }
  }
}
```

### 8.2 List Available Workspaces

```http
GET /api/v1/me/workspaces
```

Response example:

```json
{
  "data": [
    {
      "workspaceId": "workspace-claims",
      "displayName": "Claims Operations",
      "role": "claims_officer",
      "isDefault": true
    }
  ]
}
```

---

## 9. Command Center APIs

The Command Center is the role-aware operational landing page. It supports metrics, priorities, recent activity, suggested actions, AI activity, and queue signals.

### 9.1 Get Command Center Summary

```http
GET /api/v1/command-center/summary
```

Query parameters:

| Name | Type | Required | Description |
|---|---|---:|---|
| roleView | string | No | Optional role dashboard view. |
| from | date | No | Start date for metrics. |
| to | date | No | End date for metrics. |

Response example:

```json
{
  "data": {
    "headline": "Good morning. Claims intake is stable, but 8 claims are at SLA risk.",
    "metrics": [
      { "key": "openClaims", "label": "Open Claims", "value": 124, "severity": "neutral" },
      { "key": "slaAtRisk", "label": "SLA At Risk", "value": 8, "severity": "warning" },
      { "key": "pendingApprovals", "label": "Pending Approvals", "value": 11, "severity": "warning" },
      { "key": "aiQuestionsAnswered", "label": "AI Questions Answered", "value": 326, "severity": "good" }
    ],
    "priorities": [
      {
        "id": "prio_001",
        "type": "claim",
        "title": "Review motor claim CLM-2026-000123",
        "reason": "SLA breach risk and missing police report",
        "severity": "warning",
        "targetUrl": "/claims/clm_2026_000123"
      }
    ]
  }
}
```

### 9.2 Get Recent Activity

```http
GET /api/v1/command-center/recent-activity?page=1&pageSize=25
```

### 9.3 Get Suggested Actions

```http
GET /api/v1/command-center/suggested-actions
```

Suggested actions must be permission-aware. A user should never receive an action they cannot perform.

---

## 10. Channels APIs

Channels is the internal unified inbox for customer-originated conversations entering through WhatsApp, WebChat, SMS, email, Teams, broker, call center, and website form channels.

### 10.1 Get Channels Overview

```http
GET /api/v1/channels/overview
```

Query parameters:

| Name | Type | Required | Description |
|---|---|---:|---|
| channel | string | No | `whatsapp`, `webchat`, `email`, `sms`, `teams`, `broker`, `call_center`, `website_form`. |
| status | string | No | Conversation status. |
| intent | string | No | Claim, quote, renewal, complaint, policy inquiry, etc. |
| assignedTo | string | No | User ID or team ID. |
| search | string | No | Customer, message, route search. |
| page | number | No | Default `1`. |
| pageSize | number | No | Default `25`. |

Response example:

```json
{
  "data": {
    "metrics": [
      { "label": "New Conversations", "value": 18, "changePercent": 12, "severity": "neutral" },
      { "label": "Escalated", "value": 3, "changePercent": -4, "severity": "warning" }
    ],
    "sourceStatuses": [
      {
        "channel": "whatsapp",
        "displayName": "WhatsApp",
        "healthStatus": "connected",
        "activeConversations": 42,
        "failedEvents": 0,
        "lastSyncAt": "2026-06-30T12:50:00Z"
      }
    ],
    "conversations": [
      {
        "id": "conv_01J...",
        "customerId": "cus_01J...",
        "customerDisplayName": "John Mukasa",
        "customerPrimaryContact": "+256...",
        "channel": "whatsapp",
        "intent": "claim",
        "aiConfidence": 91,
        "suggestedRoutingTeam": "Motor Claims",
        "status": "new",
        "assignedTo": null,
        "lastMessagePreview": "I have had an accident on Jinja Road...",
        "lastActivityAt": "2026-06-30T12:48:00Z",
        "workItemId": null
      }
    ],
    "totalCount": 18,
    "page": 1,
    "pageSize": 25
  }
}
```

### 10.2 Get Conversation Detail

```http
GET /api/v1/channels/conversations/{conversationId}
```

### 10.3 Assign Conversation

```http
POST /api/v1/channels/conversations/{conversationId}/assign
```

Request:

```json
{
  "assignedTo": "usr_01J...",
  "note": "Assigning to motor claims intake team."
}
```

### 10.4 Convert Conversation to Work Item

```http
POST /api/v1/channels/conversations/{conversationId}/convert
```

Request:

```json
{
  "targetType": "claim",
  "claimType": "motor_comprehensive",
  "customerId": "cus_01J...",
  "idempotencyKey": "conv_01J_to_claim_001"
}
```

Response:

```json
{
  "data": {
    "workItemId": "claim_clm_2026_000123",
    "claimId": "clm_2026_000123",
    "status": "created"
  }
}
```

---

## 11. Customers APIs

Customers is an internal Customer 360 workspace. Customers do not log into this internal workspace.

### 11.1 Get Customers Overview

```http
GET /api/v1/customers/overview
```

Query parameters:

| Name | Type | Required | Description |
|---|---|---:|---|
| search | string | No | Name, phone, email, masked ID, policy, claim. |
| segment | string | No | `retail`, `sme`, `corporate`, `broker_managed`, `bancassurance`, `microinsurance`. |
| status | string | No | `active`, `prospect`, `lapsed`, `dormant`, `restricted`. |
| page | number | No | Default `1`. |
| pageSize | number | No | Default `25`. |

Response example:

```json
{
  "data": {
    "metrics": [
      { "label": "Active Customers", "value": 4821, "severity": "good" },
      { "label": "Open Claims", "value": 124, "severity": "warning" }
    ],
    "customers": [
      {
        "id": "cus_01J...",
        "displayName": "John Mukasa",
        "primaryPhone": "+256...",
        "email": "john@example.com",
        "nationalIdMasked": "CM****789",
        "segment": "retail",
        "status": "active",
        "activePolicies": 2,
        "openClaims": 1,
        "openLeads": 0,
        "unresolvedComplaints": 0,
        "consentStatus": "complete",
        "lastInteractionAt": "2026-06-30T12:48:00Z"
      }
    ],
    "totalCount": 4821,
    "page": 1,
    "pageSize": 25
  }
}
```

### 11.2 Get Customer Profile

```http
GET /api/v1/customers/{customerId}
```

Response sections:

- Identity details.
- Contact information.
- Consent status.
- Active policies.
- Claims.
- Leads/opportunities.
- Documents.
- Communication history.
- Tasks.
- Complaints.
- Audit summary.
- AI customer summary.

### 11.3 Update Customer Consent

```http
POST /api/v1/customers/{customerId}/consents
```

Request:

```json
{
  "purpose": "claims_processing",
  "status": "granted",
  "channel": "whatsapp",
  "capturedText": "I agree that my information may be used to process this claim.",
  "capturedAt": "2026-06-30T12:48:00Z"
}
```

---

## 12. Sales & Leads APIs

Sales & Leads captures acquisition work created by WhatsApp, WebChat, broker, call-center, email, website, agent, and bancassurance channels.

### 12.1 Get Sales Leads Overview

```http
GET /api/v1/sales-leads/overview
```

Query parameters:

| Name | Type | Required | Description |
|---|---|---:|---|
| search | string | No | Customer, lead reference, product. |
| source | string | No | `whatsapp`, `webchat`, `broker`, `agent`, `call_center`, `email`, `website_form`, `bancassurance_partner`. |
| status | string | No | Lead status. |
| productInterest | string | No | `motor`, `life`, `health`, `business`, `agriculture`, `travel`, `property`, `unknown`. |
| assignedTo | string | No | User ID or team ID. |
| page | number | No | Default `1`. |
| pageSize | number | No | Default `25`. |

Response example:

```json
{
  "data": {
    "metrics": [
      { "label": "New Leads", "value": 32, "changePercent": 18, "severity": "good" },
      { "label": "Missing Information", "value": 7, "changePercent": -2, "severity": "warning" }
    ],
    "leads": [
      {
        "id": "lead_01J...",
        "reference": "LEAD-2026-00045",
        "customerDisplayName": "M. Nantongo",
        "customerPrimaryContact": "+256...",
        "source": "whatsapp",
        "productInterest": "motor",
        "status": "qualifying",
        "aiConfidence": 88,
        "productFitSummary": "Customer is seeking comprehensive motor cover for a private vehicle.",
        "requiredInformationCount": 2,
        "nextAction": "Request vehicle registration and current logbook photo.",
        "assignedTo": "usr_01J...",
        "quoteValue": null,
        "currencyCode": "UGX",
        "lastActivityAt": "2026-06-30T12:49:00Z"
      }
    ],
    "totalCount": 32,
    "page": 1,
    "pageSize": 25
  }
}
```

### 12.2 Create Lead

```http
POST /api/v1/sales-leads
```

Request:

```json
{
  "source": "whatsapp",
  "customerId": "cus_01J...",
  "productInterest": "motor",
  "notes": "Customer asked for comprehensive motor insurance.",
  "conversationId": "conv_01J..."
}
```

### 12.3 Update Lead Status

```http
POST /api/v1/sales-leads/{leadId}/status
```

Request:

```json
{
  "status": "quote_sent",
  "reason": "Quote prepared and sent after human review.",
  "customerSafeMessageId": "msg_01J..."
}
```

---

## 13. Claims APIs

Claims is the core MVP module for IIP. The first MVP workflow is motor claims and document intelligence.

### 13.1 Create Claim

```http
POST /api/cases
POST /api/v1/claims
```

Legacy/current endpoint `/api/cases` may exist. New product API should use `/api/v1/claims` while preserving backward compatibility during migration.

Request:

```json
{
  "claimType": "motor_comprehensive",
  "sourceChannel": "whatsapp",
  "customerId": "cus_01J...",
  "policyNumber": "POL-2026-000123",
  "incident": {
    "dateOfLoss": "2026-06-29",
    "timeOfLoss": "18:30",
    "location": "Jinja Road, Kampala",
    "description": "Vehicle collision involving third party."
  },
  "vehicle": {
    "registrationNumber": "UAX 123A",
    "make": "Toyota",
    "model": "Hilux",
    "year": 2020
  },
  "idempotencyKey": "fnol_20260630_0001"
}
```

Response:

```json
{
  "data": {
    "claimId": "clm_2026_000123",
    "claimReference": "CLM-2026-000123",
    "status": "intake_started",
    "requiredDocuments": [
      "claim_form",
      "police_report",
      "national_id",
      "driving_permit",
      "vehicle_photos",
      "repair_quotation",
      "policy_schedule"
    ]
  }
}
```

### 13.2 List Claims / Work Queue

```http
GET /api/cases
GET /api/v1/claims
```

Query parameters:

| Name | Type | Required | Description |
|---|---|---:|---|
| status | string | No | Claim status filter. |
| claimType | string | No | Claim type. |
| productLine | string | No | Motor, health, life, property, etc. |
| risk | string | No | Risk severity. |
| sla | string | No | `on_track`, `at_risk`, `breached`. |
| assignedTo | string | No | User ID. |
| search | string | No | Claim ID, customer, policy number. |
| page | number | No | Default `1`. |
| pageSize | number | No | Default `25`. |

### 13.3 Get Claim Detail

```http
GET /api/cases/{caseId}
GET /api/v1/claims/{claimId}
```

Response example:

```json
{
  "data": {
    "claimId": "clm_2026_000123",
    "claimReference": "CLM-2026-000123",
    "claimType": "motor_comprehensive",
    "status": "needs_review",
    "customer": {
      "customerId": "cus_01J...",
      "displayName": "John Mukasa",
      "primaryPhone": "+256..."
    },
    "policy": {
      "policyId": "pol_2026_000123",
      "policyNumber": "POL-2026-000123",
      "productLine": "motor",
      "coverageStatus": "unknown_pending_review"
    },
    "incident": {
      "dateOfLoss": "2026-06-29",
      "location": "Jinja Road, Kampala",
      "description": "Vehicle collision involving third party."
    },
    "documentsSummary": {
      "requiredCount": 7,
      "uploadedCount": 5,
      "missingCount": 2,
      "lowConfidenceCount": 1
    },
    "aiSummary": {
      "summaryId": "sum_01J...",
      "status": "generated",
      "text": "The claimant reported a motor accident on Jinja Road...",
      "confidence": 82,
      "sourceEvidenceIds": ["doc_01J#page1", "doc_02J#field_date"],
      "limitations": ["Police report missing officer stamp verification"]
    },
    "sla": {
      "status": "at_risk",
      "dueAt": "2026-07-03T17:00:00Z"
    },
    "assignedTo": "usr_01J..."
  }
}
```

### 13.4 Assign Claim

```http
POST /api/v1/claims/{claimId}/assign
```

Request:

```json
{
  "assignedTo": "usr_01J...",
  "priority": "high",
  "note": "Assign to motor claims officer due to SLA risk."
}
```

### 13.5 Escalate Claim

```http
POST /api/v1/claims/{claimId}/escalate
```

Request:

```json
{
  "reason": "sla_at_risk",
  "escalateTo": "claims_supervisor",
  "note": "Police report still missing and claim is approaching SLA breach."
}
```

### 13.6 Request Missing Documents

```http
POST /api/v1/claims/{claimId}/document-requests
```

Request:

```json
{
  "documentTypes": ["police_report", "repair_quotation"],
  "messageMode": "draft_for_human_review",
  "channel": "whatsapp"
}
```

Response:

```json
{
  "data": {
    "requestId": "docreq_01J...",
    "draftMessage": "Thank you for submitting your motor claim. Please share the police report and repair quotation so the claims team can continue the review.",
    "requiresHumanApproval": true
  }
}
```

### 13.7 Update Claim Workflow Status

```http
POST /api/v1/claims/{claimId}/status
```

Request:

```json
{
  "status": "needs_review",
  "reason": "All required documents uploaded. AI extraction ready for human review."
}
```

Business rule:

- Status changes involving final claim outcome must require approval endpoints, not direct status updates.

---

## 14. FNOL APIs

FNOL APIs support first notice of loss intake from WhatsApp, staff, email, broker, or web forms.

### 14.1 Start FNOL Session

```http
POST /api/v1/fnol/sessions
```

Request:

```json
{
  "channel": "whatsapp",
  "customerContact": "+256...",
  "claimTypeHint": "motor",
  "language": "en",
  "conversationId": "conv_01J..."
}
```

### 14.2 Update FNOL Session

```http
PATCH /api/v1/fnol/sessions/{sessionId}
```

Request:

```json
{
  "incidentDate": "2026-06-29",
  "incidentLocation": "Jinja Road, Kampala",
  "description": "Vehicle accident",
  "policyNumber": "POL-2026-000123"
}
```

### 14.3 Convert FNOL to Claim

```http
POST /api/v1/fnol/sessions/{sessionId}/convert-to-claim
```

Request:

```json
{
  "idempotencyKey": "fnol_session_01J_convert_001"
}
```

---

## 15. Document Intelligence APIs

Document APIs support upload, classification, OCR/extraction, evidence mapping, missing-document checks, and human review.

### 15.1 Upload Claim Document

```http
POST /api/v1/claims/{claimId}/documents
Content-Type: multipart/form-data
```

Form fields:

| Field | Type | Required | Description |
|---|---|---:|---|
| file | binary | Yes | PDF, image, document file. |
| documentTypeHint | string | No | User-selected type if known. |
| sourceChannel | string | No | WhatsApp, email, staff upload, broker. |
| uploadedByCustomer | boolean | No | Whether uploaded externally. |

Response:

```json
{
  "data": {
    "documentId": "doc_01J...",
    "claimId": "clm_2026_000123",
    "fileName": "police-report.pdf",
    "status": "uploaded",
    "storageUri": "redacted://blob/claim-documents/doc_01J",
    "classificationStatus": "queued"
  }
}
```

### 15.2 List Claim Documents

```http
GET /api/v1/claims/{claimId}/documents
```

### 15.3 Get Document Detail

```http
GET /api/v1/documents/{documentId}
```

Response includes:

- Metadata.
- File preview URL if authorized.
- Classification result.
- Extraction result.
- Confidence scores.
- Review status.
- Source evidence anchors.
- Audit summary.

### 15.4 Start Classification

```http
POST /api/v1/documents/{documentId}/classify
```

Response:

```json
{
  "data": {
    "jobId": "job_01J...",
    "status": "queued"
  }
}
```

### 15.5 Start Extraction

```http
POST /api/v1/documents/{documentId}/extract
```

Response:

```json
{
  "data": {
    "jobId": "job_01J...",
    "status": "queued"
  }
}
```

### 15.6 Get Extraction Result

```http
GET /api/v1/documents/{documentId}/extraction
```

Response example:

```json
{
  "data": {
    "documentId": "doc_01J...",
    "documentType": "police_report",
    "overallConfidence": 86,
    "fields": [
      {
        "fieldKey": "incidentDate",
        "label": "Incident Date",
        "value": "2026-06-29",
        "confidence": 91,
        "sourceEvidenceId": "doc_01J#page1:y120-x55-w210-h20",
        "reviewStatus": "pending"
      },
      {
        "fieldKey": "vehicleRegistration",
        "label": "Vehicle Registration",
        "value": "UAX 123A",
        "confidence": 74,
        "sourceEvidenceId": "doc_01J#page1:y210-x60-w160-h20",
        "reviewStatus": "needs_review"
      }
    ],
    "warnings": [
      {
        "code": "LOW_CONFIDENCE_FIELD",
        "message": "Vehicle registration confidence is below the review threshold."
      }
    ]
  }
}
```

### 15.7 Review Extracted Field

```http
POST /api/v1/documents/{documentId}/fields/{fieldKey}/review
```

Request:

```json
{
  "reviewStatus": "approved",
  "correctedValue": "UAX 123A",
  "note": "Confirmed from police report image."
}
```

### 15.8 Check Claim Document Completeness

```http
POST /api/v1/claims/{claimId}/document-completeness/check
```

Response:

```json
{
  "data": {
    "claimId": "clm_2026_000123",
    "claimType": "motor_comprehensive",
    "completenessScore": 71,
    "requiredDocuments": [
      { "documentType": "claim_form", "status": "uploaded" },
      { "documentType": "police_report", "status": "missing" },
      { "documentType": "repair_quotation", "status": "missing" }
    ],
    "recommendedNextAction": "Request police report and repair quotation from customer."
  }
}
```

---

## 16. AI Agent and Copilot APIs

AI APIs must be tightly governed. AI outputs must be saved, traceable, source-grounded where relevant, and never treated as final regulated decisions.

### 16.1 Ask Contextual Assistant

```http
POST /api/v1/ai/assistant/query
```

Request:

```json
{
  "scope": "claim",
  "scopeId": "clm_2026_000123",
  "question": "What documents are still missing and what should I do next?",
  "responseMode": "staff_internal"
}
```

Response:

```json
{
  "data": {
    "aiRunId": "airun_01J...",
    "answer": "The police report and repair quotation are still missing. The next recommended action is to send a customer-safe missing-document request for those two items.",
    "confidence": 88,
    "sources": [
      { "sourceType": "claim", "sourceId": "clm_2026_000123", "label": "Claim Document Checklist" }
    ],
    "limitations": [],
    "requiresHumanReview": false,
    "suggestedActions": [
      {
        "actionType": "draft_missing_document_request",
        "label": "Draft missing-document request",
        "targetEndpoint": "/api/v1/claims/clm_2026_000123/document-requests"
      }
    ]
  }
}
```

### 16.2 Generate Claim Summary

```http
POST /api/v1/claims/{claimId}/ai-summary
```

Request:

```json
{
  "summaryType": "human_review",
  "includeSources": true
}
```

Response:

```json
{
  "data": {
    "summaryId": "sum_01J...",
    "aiRunId": "airun_01J...",
    "text": "The claimant reported a motor accident on 29 June 2026 on Jinja Road...",
    "confidence": 82,
    "sources": [
      { "documentId": "doc_01J...", "evidenceId": "doc_01J#page1", "label": "Claim Form" }
    ],
    "limitations": [
      "Police report not yet uploaded. Coverage cannot be determined from available evidence."
    ],
    "guardrailsApplied": [
      "no_claim_approval",
      "no_settlement_promise",
      "source_grounding_required"
    ]
  }
}
```

### 16.3 Detect Fraud Signals

```http
POST /api/v1/claims/{claimId}/fraud-signals/detect
```

Response:

```json
{
  "data": {
    "signalRunId": "fsr_01J...",
    "overallSignalLevel": "medium",
    "signals": [
      {
        "signalType": "date_mismatch",
        "severity": "warning",
        "description": "Incident date on claim form differs from police report date.",
        "evidenceIds": ["doc_01J#field_incidentDate", "doc_02J#field_reportDate"],
        "humanReviewRequired": true,
        "disclaimer": "This is an advisory signal only and is not a fraud accusation."
      }
    ]
  }
}
```

Business rule:

> The Fraud Signal API must never return language accusing a customer of fraud. It returns signals requiring human investigation.

### 16.4 Draft Customer Update

```http
POST /api/v1/customer-updates/draft
```

Request:

```json
{
  "claimId": "clm_2026_000123",
  "channel": "whatsapp",
  "purpose": "missing_documents",
  "tone": "clear_professional",
  "language": "en"
}
```

Response:

```json
{
  "data": {
    "draftId": "draft_01J...",
    "message": "Thank you for submitting your claim. To continue the review, please send the police report and repair quotation.",
    "requiresHumanApproval": true,
    "sources": [
      { "sourceType": "document_checklist", "sourceId": "chk_01J..." }
    ]
  }
}
```

---

## 17. Knowledge Base and Policy Q&A APIs

Knowledge APIs support approved policy documents, claims procedures, FAQs, SOPs, templates, and retrieval-grounded AI.

### 17.1 List Knowledge Sources

```http
GET /api/v1/knowledge/sources
```

### 17.2 Upload Knowledge Document

```http
POST /api/v1/knowledge/sources
Content-Type: multipart/form-data
```

Form fields:

| Field | Type | Required | Description |
|---|---|---:|---|
| file | binary | Yes | Policy wording, SOP, FAQ, checklist. |
| sourceType | string | Yes | `policy_wording`, `claims_sop`, `faq`, `checklist`, `template`. |
| productLine | string | No | Motor, health, life, property, etc. |
| effectiveFrom | date | No | Start date. |
| effectiveTo | date | No | End date if known. |

### 17.3 Approve Knowledge Source

```http
POST /api/v1/knowledge/sources/{sourceId}/approve
```

Business rule:

- Only approved sources may be used for customer-impacting policy Q&A.

### 17.4 Ask Policy Question

```http
POST /api/v1/knowledge/query
```

Request:

```json
{
  "question": "What documents are needed for a comprehensive motor claim?",
  "productLine": "motor",
  "audience": "staff_internal",
  "claimId": "clm_2026_000123"
}
```

Response:

```json
{
  "data": {
    "aiRunId": "airun_01J...",
    "answer": "For a comprehensive motor claim, the required documents include the claim form, police report, driving permit, national ID, vehicle photos, repair quotation, and policy schedule.",
    "confidence": 91,
    "sources": [
      {
        "sourceId": "ks_01J...",
        "title": "Motor Claims Checklist",
        "section": "Required Documents",
        "page": 2
      }
    ],
    "limitations": []
  }
}
```

---

## 18. Human Approvals APIs

Approvals are required for consequential actions, sensitive outputs, high-risk workflows, customer-adverse decisions, and regulated insurance decisions.

### 18.1 List Approval Queue

```http
GET /api/v1/approvals
```

Query parameters:

| Name | Type | Required | Description |
|---|---|---:|---|
| status | string | No | Pending, approved, rejected, escalated. |
| type | string | No | Claim decision, customer update, export, override, etc. |
| assignedTo | string | No | User ID. |
| claimId | string | No | Filter by claim. |

### 18.2 Create Approval Request

```http
POST /api/v1/approvals
```

Request:

```json
{
  "approvalType": "customer_update_send",
  "targetType": "customer_update_draft",
  "targetId": "draft_01J...",
  "reason": "Customer-facing message requires review before sending.",
  "claimId": "clm_2026_000123",
  "assignedToRole": "claims_supervisor"
}
```

### 18.3 Approve Request

```http
POST /api/v1/approvals/{approvalId}/approve
```

Request:

```json
{
  "decisionNote": "Message is accurate and customer-safe. Approved to send.",
  "applyImmediately": true
}
```

### 18.4 Reject Request

```http
POST /api/v1/approvals/{approvalId}/reject
```

Request:

```json
{
  "decisionNote": "Message implies settlement timeline. Revise and resubmit."
}
```

### 18.5 Override AI Output

```http
POST /api/v1/approvals/{approvalId}/override
```

Request:

```json
{
  "overrideReason": "AI summary omitted key detail from assessor report.",
  "correctedOutput": "Updated human-approved summary text."
}
```

---

## 19. Customer Communication APIs

Customer communications must be customer-safe, channel-aware, and auditable.

### 19.1 Send Approved Customer Update

```http
POST /api/v1/customer-updates/{draftId}/send
```

Request:

```json
{
  "approvalId": "appr_01J...",
  "channel": "whatsapp"
}
```

Business rule:

- Customer-facing updates should generally require human approval unless explicitly configured as low-risk auto-send.

### 19.2 List Communication History

```http
GET /api/v1/customers/{customerId}/communications
GET /api/v1/claims/{claimId}/communications
```

---

## 20. Tasks and Workflow APIs

### 20.1 List Tasks

```http
GET /api/v1/tasks
```

Query parameters:

| Name | Type | Required | Description |
|---|---|---:|---|
| status | string | No | Open, in progress, completed, overdue. |
| assignedTo | string | No | User ID. |
| claimId | string | No | Linked claim. |
| priority | string | No | Low, normal, high, urgent. |

### 20.2 Create Task

```http
POST /api/v1/tasks
```

Request:

```json
{
  "title": "Review low-confidence vehicle registration extraction",
  "description": "The extraction confidence is below threshold and needs review.",
  "linkedEntityType": "document",
  "linkedEntityId": "doc_01J...",
  "claimId": "clm_2026_000123",
  "assignedTo": "usr_01J...",
  "priority": "high",
  "dueAt": "2026-07-01T17:00:00Z"
}
```

### 20.3 Complete Task

```http
POST /api/v1/tasks/{taskId}/complete
```

Request:

```json
{
  "completionNote": "Field reviewed and corrected.",
  "evidenceIds": ["doc_01J#field_vehicleRegistration"]
}
```

---

## 21. Compliance and Audit APIs

Audit APIs support internal compliance, regulator-ready evidence, debugging, AI governance, and incident review.

### 21.1 Search Audit Events

```http
GET /api/v1/audit/events
```

Query parameters:

| Name | Type | Required | Description |
|---|---|---:|---|
| entityType | string | No | Claim, document, customer, approval, AI run. |
| entityId | string | No | Entity ID. |
| actorId | string | No | User or system actor. |
| eventType | string | No | Event type. |
| from | datetime | No | Start time. |
| to | datetime | No | End time. |
| page | number | No | Default `1`. |
| pageSize | number | No | Default `50`. |

Response example:

```json
{
  "data": [
    {
      "auditEventId": "audit_01J...",
      "eventType": "document.field_approved",
      "entityType": "document",
      "entityId": "doc_01J...",
      "actorType": "user",
      "actorId": "usr_01J...",
      "tenantId": "tenant-demo",
      "workspaceId": "workspace-claims",
      "timestamp": "2026-06-30T13:00:00Z",
      "summary": "User approved extracted field vehicleRegistration.",
      "metadata": {
        "fieldKey": "vehicleRegistration",
        "previousStatus": "needs_review",
        "newStatus": "approved"
      }
    }
  ]
}
```

### 21.2 Get AI Run Audit Detail

```http
GET /api/v1/audit/ai-runs/{aiRunId}
```

Response includes:

- Agent name.
- Prompt version.
- Model/provider metadata.
- Tool calls.
- Retrieved evidence IDs.
- Output.
- Confidence.
- Limitations.
- Guardrails applied.
- User action after output.

### 21.3 Export Audit Pack

```http
POST /api/v1/audit/export
```

Request:

```json
{
  "entityType": "claim",
  "entityId": "clm_2026_000123",
  "format": "pdf",
  "includeDocuments": false,
  "includeAiRuns": true,
  "includeApprovals": true
}
```

Response:

```json
{
  "data": {
    "exportId": "exp_01J...",
    "status": "queued"
  }
}
```

---

## 22. Integration APIs

Integration APIs manage provider state, incoming events, export jobs, and controlled downstream write-back.

### 22.1 List Integration Providers

```http
GET /api/v1/integrations/providers
```

Response example:

```json
{
  "data": [
    {
      "providerKey": "whatsapp_meta",
      "displayName": "Meta WhatsApp Business API",
      "status": "connected",
      "isConfigured": true,
      "lastSuccessAt": "2026-06-30T12:55:00Z",
      "lastFailureAt": null
    },
    {
      "providerKey": "microsoft_graph",
      "displayName": "Microsoft Graph",
      "status": "not_configured",
      "isConfigured": false,
      "lastSuccessAt": null,
      "lastFailureAt": null
    }
  ]
}
```

### 22.2 Get Integration Events

```http
GET /api/v1/integrations/events
```

### 22.3 Retry Failed Integration Event

```http
POST /api/v1/integrations/events/{eventId}/retry
```

### 22.4 Create Export Job

```http
POST /api/v1/integrations/exports
```

Request:

```json
{
  "targetSystem": "claims_management_system",
  "entityType": "claim",
  "entityId": "clm_2026_000123",
  "format": "json",
  "requiresApproval": true
}
```

### 22.5 Execute Approved Write-Back

```http
POST /api/v1/integrations/writebacks/{writeBackRequestId}/execute
```

Business rules:

- Write-back must be approved where it affects insurer systems of record.
- Write-back must be idempotent.
- Write-back must log request payload, response metadata, actor, timestamp, target system, and correlation ID.
- Secrets must never be returned to the frontend.

---

## 23. Admin Settings APIs

Admin APIs configure tenant behavior, workflow rules, document templates, users, roles, provider modes, and AI settings.

### 23.1 Get Tenant Settings

```http
GET /api/v1/admin/tenant-settings
```

### 23.2 Update Tenant Settings

```http
PATCH /api/v1/admin/tenant-settings
```

Request:

```json
{
  "countryCode": "UG",
  "currencyCode": "UGX",
  "defaultLanguage": "en",
  "enabledChannels": ["whatsapp", "email", "webchat"],
  "humanReviewRequiredForCustomerMessages": true
}
```

### 23.3 Configure Document Requirements

```http
GET /api/v1/admin/document-requirements
POST /api/v1/admin/document-requirements
PATCH /api/v1/admin/document-requirements/{requirementId}
```

Example requirement:

```json
{
  "claimType": "motor_comprehensive",
  "requiredDocuments": [
    "claim_form",
    "police_report",
    "national_id",
    "driving_permit",
    "vehicle_photos",
    "repair_quotation",
    "policy_schedule"
  ],
  "effectiveFrom": "2026-07-01"
}
```

### 23.4 User and Role Management

```http
GET /api/v1/admin/users
POST /api/v1/admin/users
PATCH /api/v1/admin/users/{userId}
GET /api/v1/admin/roles
PATCH /api/v1/admin/users/{userId}/roles
```

### 23.5 AI Provider Settings

```http
GET /api/v1/admin/ai-settings
PATCH /api/v1/admin/ai-settings
```

Sensitive values must be written only through secure secret configuration flows and never returned in response bodies.

---

## 24. Reports and Analytics APIs

### 24.1 Get Operational Metrics

```http
GET /api/v1/reports/operations-summary
```

Query parameters:

| Name | Type | Required | Description |
|---|---|---:|---|
| from | date | No | Start date. |
| to | date | No | End date. |
| productLine | string | No | Product line filter. |
| workspaceId | string | No | Workspace filter. |

Metrics:

- Claims volume.
- Claims by status.
- Average intake time.
- Average document review time.
- Missing-document rate.
- SLA at risk/breached.
- AI summaries generated.
- Human review completion rate.
- Fraud signals raised.
- Customer update backlog.

### 24.2 Get AI Quality Metrics

```http
GET /api/v1/reports/ai-quality
```

Metrics:

- Extraction accuracy after human review.
- Low-confidence field rate.
- AI summary acceptance rate.
- Human override rate.
- Source citation coverage.
- Guardrail violation count.
- Model/provider latency.
- Token/cost trend.

---

## 25. Support and Health APIs

### 25.1 Get Queue Backlog

```http
GET /api/v1/support/queues
```

Response example:

```json
{
  "data": [
    {
      "queueName": "document-extraction",
      "activeMessages": 12,
      "deadLetterMessages": 1,
      "oldestMessageAgeSeconds": 340,
      "status": "degraded"
    }
  ]
}
```

### 25.2 Get Recent Failures

```http
GET /api/v1/support/failures
```

### 25.3 Reprocess Failed Job

```http
POST /api/v1/support/jobs/{jobId}/reprocess
```

---

## 26. Webhook APIs

Webhook endpoints receive external provider events. They must validate signatures, return quickly, and process asynchronously.

### 26.1 WhatsApp Webhook

```http
POST /api/v1/webhooks/whatsapp
```

Rules:

- Validate provider signature.
- Return `200` quickly after durable enqueue.
- Store raw event metadata for troubleshooting.
- Do not process customer messages synchronously in the request thread.
- Create or update ChannelConversation records.
- Deduplicate events by provider message ID.

### 26.2 Microsoft Graph Mail Webhook

```http
POST /api/v1/webhooks/microsoft-graph/mail
```

Rules:

- Validate subscription and lifecycle notifications.
- Enqueue processing event.
- Fetch message via Graph using configured app permissions/delegated permissions.
- Apply tenant mailbox routing rules.

### 26.3 Generic Integration Webhook

```http
POST /api/v1/webhooks/integrations/{providerKey}
```

Use for future insurer systems that support webhooks.

---

## 27. Event Contracts

IIP should use internal events for asynchronous processing. These events may be carried over Azure Service Bus or Event Grid.

### 27.1 Event Envelope

```json
{
  "eventId": "evt_01J...",
  "eventType": "DocumentUploaded",
  "tenantId": "tenant-demo",
  "workspaceId": "workspace-claims",
  "correlationId": "7f4d2e7b-5c1b-45d4-93af-49e0873a4f21",
  "occurredAt": "2026-06-30T13:00:00Z",
  "schemaVersion": "1.0",
  "data": {}
}
```

### 27.2 Core MVP Events

| Event | Trigger |
|---|---|
| `ChannelMessageReceived` | WhatsApp/email/webchat message received. |
| `ConversationClassified` | Channel intent identified. |
| `FNOLSessionStarted` | New FNOL flow started. |
| `ClaimCreated` | Claim record created. |
| `DocumentUploaded` | Document stored. |
| `DocumentClassified` | Document type detected. |
| `DocumentExtractionCompleted` | OCR/extraction complete. |
| `DocumentFieldReviewed` | Human reviewed extracted field. |
| `ClaimCompletenessChecked` | Required document checklist evaluated. |
| `ClaimSummaryGenerated` | AI summary generated. |
| `FraudSignalsDetected` | Advisory signals created. |
| `ApprovalRequested` | Human approval task created. |
| `ApprovalCompleted` | Approval/rejection/override completed. |
| `CustomerUpdateDrafted` | Customer-safe message drafted. |
| `CustomerUpdateSent` | Approved message sent. |
| `ExportRequested` | Export/write-back requested. |
| `ExportCompleted` | Export/write-back completed. |
| `AuditEventRecorded` | Audit event persisted. |

---

## 28. OpenAPI Conversion Notes

Engineering should convert this document into an OpenAPI 3.1 specification.

Recommended file:

```text
/openapi/iip-api-v1.yaml
```

Required OpenAPI sections:

- Info.
- Servers.
- Security schemes.
- Tags.
- Paths.
- Components/schemas.
- Components/responses.
- Components/parameters.
- Components/examples.
- Error schemas.
- Pagination schemas.

Suggested tags:

```text
AuthContext
CommandCenter
Channels
Customers
SalesLeads
Claims
FNOL
Documents
AI
Knowledge
Approvals
Communications
Tasks
Audit
Integrations
Admin
Reports
Support
Webhooks
```

---

## 29. Security Requirements for APIs

### 29.1 Required Security Controls

- Enforce authentication on all internal APIs.
- Enforce server-side RBAC/ABAC.
- Validate tenant/workspace scope on every query.
- Use anti-forgery protections where applicable for browser sessions.
- Validate all file uploads.
- Scan uploads where configured.
- Restrict file types.
- Store files in tenant-scoped containers or paths.
- Never expose connection strings or secrets.
- Use Managed Identity where possible.
- Use Key Vault for secrets.
- Log privileged actions.
- Rate-limit external webhook endpoints.
- Validate webhook signatures.
- Use idempotency keys for retried operations.
- Mask PII in logs.

### 29.2 Sensitive Action Categories

These actions require additional checks and audit logging:

- View sensitive claim documents.
- Export claim pack.
- Send customer-facing communication.
- Change claim status.
- Approve or reject workflow action.
- Override AI output.
- Execute integration write-back.
- Change role/permission settings.
- Change AI provider settings.
- Access support diagnostics in production.

---

## 30. Non-Functional API Requirements

| Requirement | Target |
|---|---|
| Availability | MVP target 99.5% for core APIs; higher for production enterprise deployments. |
| Latency | Common dashboard reads under 1.5s where possible; AI/document operations may be async. |
| Async processing | OCR, AI summary, fraud signals, export, and write-back should be queue-based. |
| Observability | All requests should emit structured logs, metrics, traces, and correlation IDs. |
| Pagination | Required for all potentially large lists. |
| Filtering | Claims, channels, customers, leads, audit, and tasks must support filters. |
| Retention | Audit and claim data retention must be configurable by tenant and legal requirements. |
| Localization | Customer-facing responses should support English first, with Luganda and Swahili as planned languages. |
| Accessibility | API should support UI states for loading, empty, error, unauthorized, and not configured. |
| Resilience | Integration failures should not crash user workflows; show degraded/not configured states. |

---

## 31. API Implementation Priorities

### 31.1 Phase 1 - MVP Foundations

| Priority | API Area |
|---:|---|
| 1 | `/api/v1/me` and workspace context |
| 2 | Health/provider health |
| 3 | Command Center summary |
| 4 | Channels overview |
| 5 | Customers overview |
| 6 | Sales & Leads overview |
| 7 | Claims create/list/detail |
| 8 | Document upload/list/detail |
| 9 | Document classification/extraction jobs |
| 10 | Claim completeness check |
| 11 | Claim AI summary |
| 12 | Human approvals |
| 13 | Audit events |
| 14 | Integration provider health |

### 31.2 Phase 2 - Operational Depth

- Policy lookup integration.
- Claims system export/write-back.
- Microsoft Graph mailbox ingestion.
- Customer update sending.
- Knowledge Base upload/approval/query.
- Fraud Signal module.
- SLA and task automation.
- Reports and AI quality metrics.

### 31.3 Phase 3 - Enterprise Expansion

- Underwriting support APIs.
- Early Warning/Retention APIs.
- Payment/mobile money APIs.
- Broker/agent portal APIs.
- Regulatory export APIs.
- Marketplace provisioning APIs.
- Advanced admin workflow builder APIs.

---

## 32. Known Open Questions

| ID | Question | Owner |
|---|---|---|
| API-Q1 | Should `/api/cases` remain as a compatibility alias for `/api/v1/claims`? | Engineering |
| API-Q2 | Which WhatsApp provider will be used first: Meta direct, Twilio, Infobip, or another provider? | Engineering / Product |
| API-Q3 | Which policy administration system will the pilot insurer use? | Sales / Solutions |
| API-Q4 | Which claims management system will the pilot insurer use? | Sales / Solutions |
| API-Q5 | Should customer-safe claim status be exposed through a customer portal in V1, or only through WhatsApp/channel workflows? | Product |
| API-Q6 | What Azure region and model quotas will be approved for production? | Cloud Architect |
| API-Q7 | What fields are mandatory in the regulator-ready claims register? | Legal / Compliance |
| API-Q8 | Which exact roles and approval limits will the pilot insurer use? | Product / Pilot Customer |
| API-Q9 | What retention periods apply to claim files, audit logs, and customer communications? | Legal / Compliance |
| API-Q10 | What API gateway policy should be enforced in Azure API Management for each client deployment? | Architecture / Security |

---

## 33. Coding Agent Guardrails

When implementing this API, coding agents must follow these rules:

1. Use **Insurance Intelligence Platform (IIP)** naming. Do not rename the product to ACIP.
2. Do not implement autonomous claim approval, rejection, settlement, payment release, policy binding, fraud accusation, final underwriting, or final customer-adverse decisions.
3. Do not rely on frontend-only RBAC.
4. Do not use mock data in staging or production.
5. Do not create generic `query_database(sql)` tools for AI agents.
6. Do not expose secrets, connection strings, provider keys, or raw tokens.
7. Every sensitive mutation must create an audit event.
8. Every AI output affecting claims, policies, customers, complaints, underwriting, payments, or regulatory workflows must preserve evidence IDs, confidence, limitations, and model/provider metadata.
9. File upload APIs must validate file type, size, tenant scope, and authorization.
10. All integration write-backs must be idempotent, approved where required, and auditable.
11. Keep local/demo headers out of production authorization logic.
12. Prefer async jobs for AI, OCR, extraction, export, and downstream integration calls.
13. Implement loading, empty, error, unauthorized, and not-configured states for UI consumers.
14. Keep API responses stable, typed, paginated, and documented.

---

## 34. Appendix A - Endpoint Inventory

| Area | Endpoint | Method | MVP |
|---|---|---:|---:|
| Health | `/health` | GET | Yes |
| Health | `/health/live` | GET | Yes |
| Health | `/health/ready` | GET | Yes |
| Provider Health | `/api/admin/provider-health` | GET | Yes |
| User Context | `/api/v1/me` | GET | Yes |
| User Context | `/api/v1/me/workspaces` | GET | Yes |
| Command Center | `/api/v1/command-center/summary` | GET | Yes |
| Command Center | `/api/v1/command-center/recent-activity` | GET | Yes |
| Command Center | `/api/v1/command-center/suggested-actions` | GET | Yes |
| Channels | `/api/v1/channels/overview` | GET | Yes |
| Channels | `/api/v1/channels/conversations/{conversationId}` | GET | Yes |
| Channels | `/api/v1/channels/conversations/{conversationId}/assign` | POST | Yes |
| Channels | `/api/v1/channels/conversations/{conversationId}/convert` | POST | Yes |
| Customers | `/api/v1/customers/overview` | GET | Yes |
| Customers | `/api/v1/customers/{customerId}` | GET | Yes |
| Customers | `/api/v1/customers/{customerId}/consents` | POST | Yes |
| Sales Leads | `/api/v1/sales-leads/overview` | GET | Yes |
| Sales Leads | `/api/v1/sales-leads` | POST | Yes |
| Sales Leads | `/api/v1/sales-leads/{leadId}/status` | POST | Yes |
| Claims | `/api/v1/claims` | GET | Yes |
| Claims | `/api/v1/claims` | POST | Yes |
| Claims | `/api/v1/claims/{claimId}` | GET | Yes |
| Claims | `/api/v1/claims/{claimId}/assign` | POST | Yes |
| Claims | `/api/v1/claims/{claimId}/escalate` | POST | Yes |
| Claims | `/api/v1/claims/{claimId}/document-requests` | POST | Yes |
| Claims | `/api/v1/claims/{claimId}/status` | POST | Yes |
| FNOL | `/api/v1/fnol/sessions` | POST | Yes |
| FNOL | `/api/v1/fnol/sessions/{sessionId}` | PATCH | Yes |
| FNOL | `/api/v1/fnol/sessions/{sessionId}/convert-to-claim` | POST | Yes |
| Documents | `/api/v1/claims/{claimId}/documents` | POST | Yes |
| Documents | `/api/v1/claims/{claimId}/documents` | GET | Yes |
| Documents | `/api/v1/documents/{documentId}` | GET | Yes |
| Documents | `/api/v1/documents/{documentId}/classify` | POST | Yes |
| Documents | `/api/v1/documents/{documentId}/extract` | POST | Yes |
| Documents | `/api/v1/documents/{documentId}/extraction` | GET | Yes |
| Documents | `/api/v1/documents/{documentId}/fields/{fieldKey}/review` | POST | Yes |
| AI | `/api/v1/ai/assistant/query` | POST | Yes |
| AI | `/api/v1/claims/{claimId}/ai-summary` | POST | Yes |
| AI | `/api/v1/claims/{claimId}/fraud-signals/detect` | POST | Phase 2 |
| Knowledge | `/api/v1/knowledge/sources` | GET | Yes |
| Knowledge | `/api/v1/knowledge/sources` | POST | Yes |
| Knowledge | `/api/v1/knowledge/sources/{sourceId}/approve` | POST | Yes |
| Knowledge | `/api/v1/knowledge/query` | POST | Yes |
| Approvals | `/api/v1/approvals` | GET | Yes |
| Approvals | `/api/v1/approvals` | POST | Yes |
| Approvals | `/api/v1/approvals/{approvalId}/approve` | POST | Yes |
| Approvals | `/api/v1/approvals/{approvalId}/reject` | POST | Yes |
| Audit | `/api/v1/audit/events` | GET | Yes |
| Audit | `/api/v1/audit/ai-runs/{aiRunId}` | GET | Yes |
| Audit | `/api/v1/audit/export` | POST | Yes |
| Integrations | `/api/v1/integrations/providers` | GET | Yes |
| Integrations | `/api/v1/integrations/events` | GET | Yes |
| Integrations | `/api/v1/integrations/events/{eventId}/retry` | POST | Yes |
| Admin | `/api/v1/admin/tenant-settings` | GET | Yes |
| Admin | `/api/v1/admin/tenant-settings` | PATCH | Yes |
| Reports | `/api/v1/reports/operations-summary` | GET | Phase 2 |
| Support | `/api/v1/support/queues` | GET | Yes |
| Webhooks | `/api/v1/webhooks/whatsapp` | POST | Yes |
| Webhooks | `/api/v1/webhooks/microsoft-graph/mail` | POST | Phase 2 |

---

## 35. Appendix B - Minimal TypeScript API Client Pattern

The frontend should call real APIs and render empty/error states if data is unavailable.

```ts
export type ApiQueryParams = Record<string, string | number | boolean | null | undefined>;

export class ApiError extends Error {
  public readonly status: number;
  public readonly details: string;

  constructor(message: string, status: number, details: string) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.details = details;
  }
}

export async function apiGet<T>(path: string, params?: ApiQueryParams, signal?: AbortSignal): Promise<T> {
  const url = new URL(path, window.location.origin);

  if (params) {
    Object.entries(params).forEach(([key, value]) => {
      if (value !== null && value !== undefined && value !== "") {
        url.searchParams.set(key, String(value));
      }
    });
  }

  const response = await fetch(url.toString(), {
    method: "GET",
    credentials: "include",
    signal,
    headers: { Accept: "application/json" }
  });

  if (!response.ok) {
    const details = await response.text();
    throw new ApiError(`Request failed with status ${response.status}`, response.status, details);
  }

  return (await response.json()) as T;
}
```

---

## 36. Final Implementation Note

This API specification should be treated as the bridge between the IIP PRD/FSD and the actual backend implementation. It should be converted into an OpenAPI file before large-scale implementation begins, then used by frontend, backend, QA, and AI coding agents as the shared API contract.

