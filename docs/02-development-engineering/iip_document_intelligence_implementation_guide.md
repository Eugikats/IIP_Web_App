# Document Intelligence Implementation Guide

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Document Intelligence Implementation Guide |
| Version | 1.0 |
| Status | Draft |
| Audience | AI/document engineers, backend engineers, claims SMEs, QA leads, coding agents |
| Date | 2026-06-30 |


## 1. Purpose

This guide defines OCR, classification, extraction, confidence thresholds, validation rules, human review, and motor claim document processing for IIP.


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


## MVP Document Types

| Document Type | Purpose | Key Fields / Signals |
| --- | --- | --- |
| Claim form | Core claim details | Claimant, incident date, location, vehicle, loss description, signature. |
| Police report | Evidence and incident confirmation | Police reference, date, location, parties, narrative, stamp/signature. |
| National ID | Identity verification support | Name, ID number, date of birth, document image quality. |
| Driving permit | Driver eligibility support | Driver name, permit number, class, expiry date. |
| Vehicle photos | Damage evidence | Vehicle reg, image quality, damage views, timestamps where available. |
| Repair quotation | Repair estimate | Garage, amount, parts/labor, vehicle reg, date. |
| Policy schedule | Coverage reference | Policy number, product, insured vehicle, dates, limits. |
| Assessor report | Assessment support | Assessor, findings, estimate, recommendation, photos. |

## Processing Pipeline

| Step | System Action | Human Review Trigger |
| --- | --- | --- |
| 1 | Receive/upload document and save to Blob | Unsupported file or suspicious file. |
| 2 | Create Document record and audit event | Upload metadata incomplete. |
| 3 | Classify document type | Low classification confidence or unknown type. |
| 4 | Run OCR/extraction | OCR failure or unreadable document. |
| 5 | Validate fields | Missing required fields, expired permit, mismatched policy/vehicle/date. |
| 6 | Compare across documents | Contradictory date, name, vehicle reg, amount. |
| 7 | Generate completeness report | Missing required document or low confidence. |
| 8 | Route to human review | All consequential use of extraction. |
| 9 | Persist corrections and feedback | Human correction creates audit and training signal. |

## Confidence Threshold Guidance

| Confidence Level | System Behavior |
| --- | --- |
| High | Show extracted value with source highlight; still allow correction. |
| Medium | Show review required indicator and ask user to confirm. |
| Low | Do not rely on value; route for manual entry/review. |
| Unknown | Treat as unavailable and request human review or resubmission. |

## Validation Rules

| Rule | Example | Action |
| --- | --- | --- |
| Required document missing | No police report for claim type requiring police report | Flag missing document. |
| Field mismatch | Vehicle reg differs between claim form and quotation | Flag inconsistency, not fraud. |
| Expired permit | Permit expiry before incident date | Flag for human review. |
| Unsupported file | Executable or unsupported image type | Reject or quarantine. |
| Blurry/unreadable | Low OCR quality | Request resubmission or manual review. |
| Duplicate document | Same hash or near-duplicate upload | Flag duplicate, keep audit. |

## Acceptance Criteria

| ID | Acceptance Criterion | Evidence |
| --- | --- | --- |
| AC-DOC-001 | Documents can be uploaded, classified, extracted, reviewed, and linked to claims. | End-to-end document test. |
| AC-DOC-002 | Low-confidence or mismatched fields require human review. | Document QA. |
| AC-DOC-003 | Document corrections are audited. | Audit test. |
| AC-DOC-004 | Missing documents are clearly shown in claim review. | Functional QA. |

## Open Questions and Required Confirmations

| ID | Open Question / Confirmation Needed | Impact |
| --- | --- | --- |
| OQ-DOC-001 | What document checklist applies to each motor claim category? | Affects validation. |
| OQ-DOC-002 | What minimum confidence threshold is acceptable for pilot? | Affects review workload. |
| OQ-DOC-003 | Will sample Ugandan documents be provided for testing? | Affects model tuning and QA. |
