# AI/RAG Knowledge Architecture

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | AI/RAG Knowledge Architecture |
| Version | 1.0 |
| Status | Draft |
| Audience | AI engineers, knowledge managers, backend engineers, compliance leads, QA leads, coding agents |
| Date | 2026-06-30 |


## 1. Purpose


---

# July 2026 Update: RAG Rules for Chat-First Command Center

## Knowledge Architecture Decision

The chat-first Command Center may answer operational questions and summarize insurance work, but any answer that depends on policy wording, SOPs, document evidence, claim records, SLA rules, or compliance guidance must be grounded in approved tenant-scoped sources.

The Command Center must not answer policy, coverage, claims, compliance, or operational questions from model memory.

## Command Center Retrieval Scope

| Query Type | Required Retrieval / Source |
|---|---|
| Policy or coverage question | Approved policy wording, schedule, product guide, or clause source. |
| Claims process question | Approved claims SOP or workflow configuration. |
| Missing document question | Versioned document checklist by claim type/product line. |
| SLA question | SLA configuration, claim/task timestamps, workflow state. |
| Compliance question | Approved compliance guidance or audit policy. |
| Customer update draft | Approved templates, claim state, missing documents, communication rules. |
| Management summary | Backend metrics, claims records, audit records, provider health, and source labels. |

## Evidence Rail Requirements

For Command Center responses, the RAG layer should return enough metadata for the UI to show:

- source title;
- source type;
- source version/effective date;
- page, section, or evidence ID;
- tenant/workspace scope;
- confidence or retrieval score where appropriate;
- whether the answer requires human review;
- whether evidence is missing, conflicting, stale, or low confidence.

## No-Source Behavior

If no approved source exists, the assistant must respond with a safe limitation such as:

```text
I cannot confirm this from approved IIP knowledge or connected operational data. Please review manually or ask an administrator to configure the required source.
```

It must not improvise policy explanations, claim obligations, settlement expectations, legal conclusions, or regulatory statements.

## Prompt Injection Defense

Customer messages, emails, uploaded documents, claim forms, and retrieved knowledge may contain malicious or misleading instructions. The system must treat retrieved content as evidence only. Retrieved content must never override system policy, tool policy, tenant boundaries, or human-approval requirements.

## Phase 1-4 RAG Alignment

| Phase | RAG Work |
|---|---|
| Phase 1 | Define evidence rail payloads and no-source UX states. |
| Phase 2 | Connect command responses to approved knowledge and operational evidence with tenant/workspace filters. |
| Phase 3 | Add grounded command answers, citations, uncertainty handling, and action drafting from approved templates. |
| Phase 4 | Add RAG evaluation tests, hallucination checks, prompt-injection tests, source freshness checks, and audit review. |
This document defines knowledge upload, approval, chunking, indexing, embeddings, retrieval, citations, confidence handling, and re-indexing rules for IIP.


This document uses the existing IIP project context and prior documentation pack: Master Product Context, PRD, FSD, Technical Architecture Document, System Integration Strategy, UI/UX Handoff, API Specification, Data Model, Security & Compliance Document, QA/Test Plan, Deployment Plan, User/Admin Guide, Implementation Roadmap, and Strategy/Requirements/Roadmap Pack.

Confirmed project baseline:
- Official product name: Insurance Intelligence Platform (IIP).
- IIP is an Azure-native, human-reviewed insurance operations platform, not a replacement for core insurance systems.
- MVP focus: motor claims and document intelligence for a mid-to-large insurer pilot.
- Customer engagement is WhatsApp-first and channel-led; customers do not log into the internal staff application.
- Internal modules include Command Center, Channels, Customers, Sales & Leads, Claims, Documents, Knowledge Base, Approvals, Compliance & Audit, Reports & Analytics, Integrations, Admin Settings, and Support & Health.
- The internal UI is a role-aware staff workspace with persistent navigation, top bar, global search, operational states, and contextual Copilot panel.
- The platform uses Next.js, React, TypeScript, and Tailwind on the frontend and .NET 8 / ASP.NET Core on the backend.
- Azure services include Azure SQL, Blob Storage, Key Vault, Azure AI Search, Azure OpenAI, Azure AI Document Intelligence, Service Bus/Event Grid, Functions/Durable Functions, API Management, Application Insights/Log Analytics, Entra ID/MSAL, and optional Managed Application packaging.
- AI assists with extraction, summarization, classification, routing, drafting, risk signals, and evidence preparation; humans make regulated or consequential decisions.
- Staging and production must not use fake operational data. Empty states, error states, and Not configured states must be real and explicit.


## Knowledge Architecture Principles

- Only approved knowledge should be available for staff answers and policy/SOP explanations.
- Retrieval must be tenant/workspace-aware.
- Every generated answer that depends on knowledge must include source references.
- The system must distinguish client-specific knowledge from generic IIP/product knowledge.
- Stale or superseded documents must not remain active without review.

## Knowledge Sources

| Source Type | Examples | MVP Handling |
| --- | --- | --- |
| Insurance SOPs | Claims handling SOP, motor claim checklist, escalation procedure | Approved upload and indexed with version metadata. |
| Policy wording/reference | Policy clauses, exclusions, coverage rules | Read-only reference with citation. |
| Regulatory/internal compliance guidance | Complaint handling, audit requirements, retention rules | Requires compliance approval before indexing. |
| Templates | Customer update templates, missing document templates | Used by drafting agent with human review. |
| Product documentation | IIP user/admin guide, workflow guidance | Useful for staff support and training. |

## Ingestion Pipeline

| Step | Action | Control |
| --- | --- | --- |
| 1 | Upload knowledge item | Capture uploader, tenant, workspace, document type, version. |
| 2 | Review/approve | Knowledge manager or admin approves before activation. |
| 3 | Extract text | Use OCR if needed; store extraction metadata. |
| 4 | Chunk content | Chunk by headings/semantic sections, retain source page/section. |
| 5 | Embed/index | Index into Azure AI Search with tenant/workspace filters. |
| 6 | Quality check | Sample retrieval and citation validation. |
| 7 | Activate | Only active approved versions are available to agents. |
| 8 | Re-index or retire | On document update, supersede old version and audit change. |

## Retrieval Rules

| Rule | Requirement |
| --- | --- |
| Tenant filter | Every query must include TenantId and WorkspaceId where applicable. |
| Source type filter | Agents should search only approved source types relevant to the task. |
| Citation requirement | Answers must include source references for claims about policy/SOP content. |
| No source, no assertion | If no relevant source is found, AI must say it cannot confirm from available sources. |
| Freshness | Output should show source version/date where available. |
| Prompt injection defense | Retrieved content is evidence, not instruction overriding system/tool policy. |

## Index Metadata Fields

| Field | Purpose |
| --- | --- |
| TenantId | Tenant isolation. |
| WorkspaceId | Workspace scoping. |
| KnowledgeItemId | Trace source. |
| DocumentVersion | Version control. |
| SourceType | Filter by SOP/policy/template/product doc. |
| ApprovalStatus | Ensure only approved content is searchable. |
| EffectiveDate | Freshness and policy interpretation. |
| PageOrSection | Citation display. |
| SensitivityLabel | Access control and output policy. |

## Acceptance Criteria

| ID | Acceptance Criterion | Evidence |
| --- | --- | --- |
| AC-RAG-001 | Only approved active content is retrievable by agents. | Retrieval test. |
| AC-RAG-002 | Search queries are tenant/workspace filtered. | Security test. |
| AC-RAG-003 | AI answers cite source IDs/sections where knowledge is used. | AI QA. |
| AC-RAG-004 | Superseded knowledge is retired or clearly versioned. | Knowledge management review. |

## Open Questions and Required Confirmations

| ID | Open Question / Confirmation Needed | Impact |
| --- | --- | --- |
| OQ-RAG-001 | Who owns knowledge approval in pilot? | Affects workflow. |
| OQ-RAG-002 | What source documents will pilot client provide first? | Affects setup. |
| OQ-RAG-003 | What chunking and confidence thresholds should be used for legal/policy text? | Affects answer quality. |