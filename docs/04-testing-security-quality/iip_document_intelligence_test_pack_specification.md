---
Product: Insurance Intelligence Platform (IIP)
Document Name: Document Intelligence Test Pack Specification
Version: 1.0
Status: Draft
Audience: Document AI Engineering, QA, Claims Operations, Product
Date: 2026-06-30
---

# Document Intelligence Test Pack Specification

## Common IIP Context Used

The Insurance Intelligence Platform (IIP) is an Azure-native, human-reviewed insurance operations platform for African insurers and related insurance organizations. It acts as an intelligent operational layer across staff, customers, core insurance systems, documents, channels, knowledge sources, and AI agents.

The current MVP direction is Motor Claims & Document Intelligence for a mid-to-large insurer pilot. Customers do not log into the internal staff app. Customer interactions arrive through WhatsApp-first channels and other supported channels, then feed internal insurer workspaces.

Confirmed implementation direction:

- Frontend: Next.js, React, TypeScript, Tailwind CSS.
- Backend: .NET 8 / ASP.NET Core, EF Core, Azure SQL.
- Azure services: Azure SQL, Blob Storage, Key Vault, Azure AI Search, Azure OpenAI, Azure AI Document Intelligence, Service Bus or Event Grid, Functions or Durable Functions, API Management, Application Insights, and Log Analytics.
- Identity: Microsoft Entra ID / MSAL / OAuth 2.0.
- Primary internal modules: Command Center, Claims, Documents, Channels, Customers, Sales & Leads, Knowledge Base, Approvals, Compliance & Audit, Reports & Analytics, Integrations, Admin Settings, Support & Health.
- Governance rule: AI prepares insurance work; humans make consequential decisions.

Non-negotiable AI boundary:

- AI may summarize, classify, extract, flag, draft, route, and recommend.
- AI must not approve or reject claims, bind policies, set final premiums, accuse customers of fraud, release payments, promise settlement, or make final regulated decisions.

Known local/demo request headers:

| Header | Known Demo Value | Purpose |
|---|---|---|
| X-Tenant-Id | tenant-demo | Tenant context. |
| X-Workspace-Id | workspace-claims | Workspace context. |
| x-demo-role | Admin | Demo role context only. |
| x-demo-user-id | local-admin | Demo user context only. |
| X-User-Id | local-admin | User context. |
| X-Correlation-ID | UUID | Traceability across logs and requests. |

Known initial endpoints include health checks, admin provider health, and cases APIs. Additional overview endpoints discussed include `/api/v1/channels/overview`, `/api/v1/customers/overview`, and `/api/v1/sales-leads/overview`.

## Purpose

This document defines tests for the IIP document intelligence pipeline, including OCR, classification, extraction, confidence thresholds, validation, human review, duplicate handling, mismatch detection, missing documents, and auditability.

## MVP Document Types

| Document Type | MVP Relevance | Typical Validation Need |
|---|---|---|
| Claim form | Required | Claimant, incident, vehicle, policy, signature/date where applicable. |
| Police report | Required or strongly required by insurer rules | Incident date, location, involved parties, report reference. |
| National ID | Often required | Name, ID number, expiry/validity where applicable. |
| Driving permit | Often required | Driver name, permit number, class, expiry. |
| Vehicle photos | Required or supporting | Damage visibility, file quality, link to claim. |
| Repair quotation | Required for repair path | Amount, garage, vehicle, date, line items. |
| Policy schedule | Required or core-system lookup | Policy number, coverage, insured, vehicle, dates. |
| Assessor report | Optional or later-stage | Assessment findings, estimate, recommendation. |

## Test Matrix

| Test ID | Area | Scenario | Test Input | Expected Result | Priority | Severity |
|---|---|---|---|---|---|---|
| DI-CLS-001 | Classification | Correctly classify clear claim form. | Clean claim form PDF/image. | Document type = claim form with confidence. | High | Major |
| DI-CLS-002 | Classification | Unknown document uploaded. | Unrelated file. | Mark unknown/needs review; do not force incorrect type. | High | Major |
| DI-OCR-001 | OCR | Extract readable text from clear document. | High-quality scan. | Text extracted and linked to document. | High | Major |
| DI-OCR-002 | OCR | Blurry document. | Low-quality image. | Low confidence or quality warning; human review required. | High | Major |
| DI-EXT-001 | Extraction | Extract claim form fields. | Claim form. | Required fields extracted with confidence per field. | High | Critical |
| DI-EXT-002 | Extraction | Extract policy number from policy schedule. | Policy schedule. | Policy number captured or marked uncertain. | High | Critical |
| DI-EXT-003 | Extraction | Extract repair amount from quotation. | Repair quote. | Amount, currency, garage, and date captured where visible. | High | Major |
| DI-VAL-001 | Validation | Mismatch between claim form and policy schedule. | Different policy number. | Mismatch flagged for human review. | High | Critical |
| DI-VAL-002 | Validation | Driving permit expired. | Expired permit. | Expiry risk flagged, no final decision made. | High | Major |
| DI-MISS-001 | Missing docs | Required doc absent. | Claim lacking police report. | Missing document flagged with next action. | High | Major |
| DI-DUP-001 | Duplicate | Same document uploaded twice. | Duplicate file. | Duplicate warning or linked duplicate state. | Medium | Major |
| DI-REV-001 | Human review | Reviewer corrects extracted field. | Extracted field with error. | Corrected value saved; original retained in audit. | High | Critical |
| DI-AUD-001 | Audit | Document processing creates audit trail. | Any processed doc. | Upload, extraction, correction, review events logged. | High | Critical |
| DI-SEC-001 | Security | User cannot access another tenant's document. | Cross-tenant document ID. | Access denied or no data leak. | High | Critical |

## Confidence Threshold Guidance

| Confidence Level | Suggested Behavior |
|---|---|
| 90% and above | May prefill field, still editable and auditable. |
| 70% to 89% | Prefill with review indicator. |
| 50% to 69% | Show as uncertain and require human confirmation. |
| Below 50% | Do not rely on value; show needs review or extraction failed. |

## Required Test Pack Assets

| Asset Category | Minimum Recommended Set | Notes |
|---|---:|---|
| Clean documents | 5 per document type | Clear expected baseline. |
| Blurry/low-quality scans | 3 per document type | Quality warning tests. |
| Mismatched documents | 10 mixed cases | Cross-document validation. |
| Duplicate documents | 10 samples | Duplicate detection. |
| Missing-document cases | 10 claim packs | Checklist logic. |
| Edge cases | 10 samples | Handwritten, cropped, rotated, dark, multi-page. |

## Acceptance Criteria

- Clear baseline documents classify correctly or fall to review without unsafe assumptions.
- Low-confidence fields are not treated as final facts.
- Human correction preserves original extracted value and audit trail.
- Required document checklist works per configured claim type.
- Cross-document mismatches create review flags, not automatic rejection.

## Quality Gates Used Across This Pack

| Gate | Required Outcome | Evidence |
|---|---|---|
| G1 - Requirements coverage | MVP modules, workflows, roles, APIs, data entities, AI actions, and document flows have mapped tests. | Traceability matrix, test cases, review sign-off. |
| G2 - Tenant and role boundary | Users cannot access another tenant, workspace, or unauthorized action. | RBAC tests, API tests, audit logs. |
| G3 - Human decision control | AI never performs final claims, underwriting, fraud, premium, settlement, or payment decisions. | AI red-team results, UI checks, audit evidence. |
| G4 - Evidence and auditability | Critical actions are logged with actor, tenant, workspace, timestamp, source, and correlation ID. | Audit event records, log samples. |
| G5 - No fake production behavior | Staging and production do not silently use fake data, fake endpoints, or hidden auth bypasses. | Config review, environment checks, release checklist. |
| G6 - Failure transparency | Loading, empty, error, unauthorized, and Not configured states are visible and useful. | Functional test results, screenshots. |

## Open Questions

| ID | Open Question | Owner | Impact if Unresolved |
|---|---|---|---|
| OQ-01 | Which insurer will be the first signed pilot customer and what exact internal processes will be included? | Product / Commercial | Final test scripts, UAT users, and success thresholds may change. |
| OQ-02 | Which claims/core system will be integrated first, and will MVP require read-only lookup, export, or direct write-back? | Product / Engineering / Client IT | Impacts API, integration, resilience, and regression testing. |
| OQ-03 | Which Azure deployment path is selected first: customer-tenant Managed Application or Otic-hosted SaaS pilot? | Leadership / Engineering | Impacts release gates, security validation, support access, and incident response. |
| OQ-04 | Which WhatsApp Business provider will be used for the first pilot? | Product / Integrations | Impacts webhook tests, retry tests, idempotency checks, and channel support runbooks. |
| OQ-05 | What client-specific SLAs, roles, branches, claim categories, and document checklists must be configured? | Client Operations | Impacts functional tests, RBAC tests, document intelligence tests, and acceptance criteria. |
| OQ-06 | What final regulatory, legal, data protection, and breach notification requirements must be signed off for the first pilot? | Legal / Compliance | Impacts compliance evidence, security validation, and incident response steps. |
