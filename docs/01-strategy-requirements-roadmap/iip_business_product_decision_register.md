# IIP Business & Product Decision Register

**Product:** Insurance Intelligence Platform (IIP)  
**Document type:** Decision-ready business/product register  
**Audience:** Product owner, insurer stakeholders, compliance owner, engineering lead  
**Status:** Consolidated open-decision register  
**Date:** July 2026

---

## Purpose

This register consolidates business and product decisions left as `TODO: confirm`, open questions, or implementation placeholders across the IIP specification set. It is intentionally written for non-engineers. Technical names are included only in the source/reference column so the engineering team can trace each decision back to the source document.

---

## 1. Claim lifecycle

| Decision | Why it matters | Current placeholder/default | Source document | Recommendation | Owner | MVP impact |
|---|---|---|---|---|---|---|
| What claim statuses should exist from first notice through closure? | Statuses drive queues, SLA, approvals, reporting, and customer messages. | Draft states include new, assigned, awaiting documents, pending approval, approved, rejected, closed, escalated. | Claims, Claim Intake/Workflow | Use the drafted state machine for MVP and prevent terminal claims from moving except via admin correction. | Claims SME + Product | Blocking |
| Which statuses are terminal? | Prevents reopening or accidental write-back after final state. | approved, rejected, closed are implied terminal states. | Claim Intake/Workflow | Treat approved, rejected, closed as terminal; allow reopen only through a separate future admin workflow. | Claims SME | Blocking |
| What information is mandatory at first notice of loss? | Determines intake form, email parsing, and required data validation. | FNOL fields stored as nullable JSON until confirmed. | Claim Intake/Workflow, Email Ingestion | MVP minimum: policy number, customer/contact, incident date, location, loss description, product line, channel, attachments. | Claims SME | Blocking |
| How should claim numbers be generated? | Affects integration, audit, customer communication, and insurer preferences. | Claim-number sequence table per tenant/workspace/product/year. | Claim Intake/Workflow | Use tenant + product line + year + sequence; allow insurer-specific display prefix as configuration later. | Product + Engineering | Blocking |
| What does escalation mean operationally? | Determines queues, permissions, and supervisor handoff. | Escalated status exists but semantics open. | Claim Intake/Workflow | Escalation should move the claim to supervisor review without deciding the claim. | Claims Manager | Blocking |

---

## 2. Documents

| Decision | Why it matters | Current placeholder/default | Source document | Recommendation | Owner | MVP impact |
|---|---|---|---|---|---|---|
| What document statuses should exist? | Drives document checker, review queues, and completeness reporting. | Uploaded/extracted/reviewed/corrected variants exist across specs. | Document Upload, Extraction, Review | Use uploaded, extraction_pending, extracted, review_required, reviewed, rejected, failed. | Claims Ops + Compliance | Blocking |
| What confidence threshold forces human review? | Controls automation safety and reviewer workload. | Low-confidence checks exist but threshold not fixed. | Document Extraction, Document Review | Start with 0.80 as human-review threshold for pilot, then tune per document type. | Claims SME + AI Lead | Blocking |
| What file types and sizes are accepted? | Prevents unsafe uploads and controls cost. | MIME/size limits are TODO. | Document Upload, Email Ingestion | Allow PDF, JPEG, PNG initially; set conservative max size per document pending insurer/provider limits. | Security + Product | Blocking |
| What documents are required per product line? | Drives completeness check and customer update drafts. | RequiredDocumentRule table planned. | Completeness Check | For motor MVP: claim form, police report where applicable, photos, repair estimate, assessor report/invoice where applicable. | Claims SME | Blocking |

---

## 3. SLA and approvals

| Decision | Why it matters | Current placeholder/default | Source document | Recommendation | Owner | MVP impact |
|---|---|---|---|---|---|---|
| What SLA warning window applies per product line? | Determines SLA-at-risk logic and alerts. | Configurable warning window implied but not finalized. | SLA Risk | Start with product-line configurable default: at risk when less than 24 business hours remain. | Operations Lead | Blocking |
| What evidence must appear in an approval request? | Prevents approvals without enough context. | Evidence mapping is open. | Approval Query, Approval Decision | Require claim summary, policy context, key documents, missing docs, AI confidence notes, and recommended human action. | Compliance + Claims SME | Blocking |
| Which approvals are visible to which staff? | Prevents cross-role leakage and queue noise. | Role/permission filters implied. | Approval Query | Visibility should follow tenant/workspace, assignment, role, and approval type. | Security + Claims Manager | Blocking |

---

## 4. Channels and communications

| Decision | Why it matters | Current placeholder/default | Source document | Recommendation | Owner | MVP impact |
|---|---|---|---|---|---|---|
| What communication statuses should exist? | Drives customer update workflow and audit. | Draft statuses pending_approval, approved, sent, send_failed, rejected. | Notification Send | Use those five statuses for MVP. | Product | Blocking |
| How does a draft link to approval? | Ensures customer messages are not sent without human approval. | Linked approval ID required. | Notification Send | Every customer-sensitive draft must have a linked approval decision before send. | Compliance | Blocking |
| Should SMS and WhatsApp use the same send service? | Affects provider strategy and roadmap. | SMS/WhatsApp strategy TODO. | Notification Send, Email Ingestion | MVP: email first; WhatsApp/SMS as provider-specific Phase 2 adapters behind the same communication boundary. | Product + Engineering | Non-blocking for email MVP |

---

## 5. Customers and sales leads

| Decision | Why it matters | Current placeholder/default | Source document | Recommendation | Owner | MVP impact |
|---|---|---|---|---|---|---|
| What should Customer 360 contain? | Determines data model and UI. | Dictionary-based sections retained. | Customers | Keep sections flexible for MVP: profile, policies, claims, interactions, alerts, documents. | Product | Blocking |
| What sales/quote statuses should exist? | Drives sales pipeline analytics. | Quote status enum not finalized. | Sales Leads | Use new, contacted, quoted, pending_documents, converted, lost for MVP if Sales is in scope. | Sales Lead | Non-blocking for claims MVP |

---

## 6. Audit and integration write-back

| Decision | Why it matters | Current placeholder/default | Source document | Recommendation | Owner | MVP impact |
|---|---|---|---|---|---|---|
| What target types are valid audit entities? | Enables filtering and regulator review. | Broad entityType string accepted. | Audit Query | Define target set: claim, document, approval, customer, communication, integration, report, knowledge_document, agent_policy. | Compliance + Engineering | Blocking |
| How should unknown audit events be displayed? | Prevents hiding unusual events. | Unknown-event handling not fixed. | Audit Query | Show unknown events safely as “Other system event,” never drop them. | Compliance | Blocking |
| Which policy admin systems are supported first? | Determines write-back adapter work. | Provider port only; concrete adapter deferred. | Integration Write-Back | Pilot with one insurer-selected system; no generic write-back success path. | Customer + Engineering | Blocking for write-back |
| What is the retry/idempotency policy? | Prevents duplicate external updates. | Retry/idempotency TODO. | Integration Write-Back | Require idempotency key before any automatic retry; no retries in MVP unless confirmed. | Engineering | Blocking if retries included |

---

## 7. Knowledge base and reporting

| Decision | Why it matters | Current placeholder/default | Source document | Recommendation | Owner | MVP impact |
|---|---|---|---|---|---|---|
| What sensitivity taxonomy controls AI retrieval? | Prevents confidential material entering AI prompts. | Categories include approved, customer_confidential, staff_only, contains_secrets, contains_unapproved_pii, unknown. | Knowledge Ingestion, Tenant Agent Policy | Use these categories for MVP and block unknown by default. | Compliance + Security | Blocking |
| What chunking parameters should be used? | Affects retrieval quality and cost. | Fixed-size chunking with overlap, exact size open. | Knowledge Ingestion | Start with conservative chunks and tune after evaluation; confirm size with AI lead. | AI Lead | Blocking for KB |
| How often should knowledge be re-indexed? | Keeps citations current. | Re-indexing strategy open. | Knowledge Ingestion | Re-index on document version change and nightly for approved documents. | Operations | Non-blocking if manual re-index exists |
| Which metrics are in management reports? | Prevents reports from becoming vague or incomplete. | Four metrics defined: claims volume, SLA breach rate, approval turnaround, document completeness. | Reporting | Use four metrics for MVP; add financial/loss metrics only after data validation. | Product + Claims Manager | Blocking |
| Which PDF renderer is approved? | Required before PDF export. | PDF renderer not chosen. | Reporting | Defer PDF; ship CSV first unless customer requires PDF. | Engineering + Security | Non-blocking if CSV accepted |
| How long are exports retained? | Affects storage, privacy, and legal hold. | Retention TODO. | Reporting | Start with 90 days unless insurer policy requires longer. | Compliance | Blocking before production |
| Which timezone defines reporting periods? | Prevents partial/final report confusion. | UTC default, tenant-local open. | Reporting | Use tenant-local timezone for business reports; store UTC boundaries. | Product + Customer | Blocking |

---

## 8. Tenant agent policy and email ingestion

| Decision | Why it matters | Current placeholder/default | Source document | Recommendation | Owner | MVP impact |
|---|---|---|---|---|---|---|
| What full set of tenant AI toggles/thresholds is configurable? | Enables tenant control without weakening platform guardrails. | Initial fields defined, exact full list open. | Tenant Agent Policy | Start with drafted fields only; add more after pilot governance review. | Product + Compliance | Blocking for admin UI |
| Should policy changes require secondary approval? | Loosening AI settings can be consequential. | Secondary approval open. | Tenant Agent Policy | Require secondary approval in production for changes that increase autonomy. | Compliance | Non-blocking for read-only MVP |
| Which email providers must be supported at launch? | Determines provider adapter work. | Gmail/Zoho/IMAP auth models open. | Email Ingestion | Launch with Graph if customer uses Microsoft 365; otherwise provider-specific adapter becomes pilot dependency. | Customer + Engineering | Customer-dependent |
| What email attachment limits apply? | Prevents ingestion failures/cost spikes. | Provider limits open. | Email Ingestion | Use platform max lower than all supported provider limits until verified. | Security + Engineering | Blocking for email intake |
| Is IMAP IDLE reliable enough? | Determines near-real-time behavior. | IMAP IDLE open; polling baseline required. | Email Ingestion | Treat IMAP as polling-only for MVP; IDLE may be a latency hint only. | Engineering | Non-blocking |

---

## 9. API versioning

| Decision | Why it matters | Current placeholder/default | Source document | Recommendation | Owner | MVP impact |
|---|---|---|---|---|---|---|
| What is the API contract versioning policy? | Prevents silent frontend/backend breaks. | Semantic OpenAPI versioning specified. | Delivery Tooling | Use semantic OpenAPI artifact versions and require changelog for contract changes. | Engineering Lead | Blocking |

---

## Consolidated decision table

| Decision | Placeholder | Recommended Answer | Owner | Blocking/Non-Blocking for MVP |
|---|---|---|---|---|
| Claim lifecycle statuses | Draft state machine | Use drafted state machine; terminal approved/rejected/closed | Claims SME | Blocking |
| FNOL required fields | JSON placeholder | Use minimum motor FNOL set | Claims SME | Blocking |
| Claim number format | Sequence table | Tenant/product/year sequence with configurable prefix later | Product + Engineering | Blocking |
| Document statuses | Multiple draft values | uploaded/extraction_pending/extracted/review_required/reviewed/rejected/failed | Claims Ops | Blocking |
| Confidence threshold | Not fixed | Start with 0.80 pilot threshold | AI Lead | Blocking |
| MIME/size limits | Not fixed | PDF/JPEG/PNG with conservative max | Security | Blocking |
| Required docs | Rule table | Motor MVP list confirmed by insurer | Claims SME | Blocking |
| SLA warning window | Not fixed | Product-line configurable, default 24 business hours | Operations | Blocking |
| Approval evidence | Open | Claim summary, policy context, docs, confidence, missing docs | Compliance | Blocking |
| Queue visibility | Open | Tenant/workspace + assignment + role + type | Security | Blocking |
| Communication statuses | Drafted | pending_approval/approved/sent/send_failed/rejected | Product | Blocking |
| SMS/WhatsApp strategy | Open | Email first; WhatsApp/SMS Phase 2 | Product | Non-blocking |
| Customer 360 schema | Flexible | Keep dictionary sections | Product | Blocking |
| Sales lead statuses | Open | new/contacted/quoted/pending_documents/converted/lost | Sales | Non-blocking |
| Audit target set | String | Define formal target set | Compliance | Blocking |
| Write-back target systems | Deferred | One pilot system first | Customer + Engineering | Blocking for write-back |
| Retry/idempotency | Open | No auto retry without idempotency key | Engineering | Blocking if retries included |
| Knowledge sensitivity taxonomy | Drafted | Use drafted categories; block unknown | Compliance | Blocking |
| Chunking parameters | Open | Tune after eval | AI Lead | Blocking for KB |
| Report metrics | Four defined | Use four for MVP | Product | Blocking |
| PDF renderer | Open | Ship CSV first, defer PDF | Engineering | Non-blocking if CSV accepted |
| Export retention | Open | 90 days unless customer policy says otherwise | Compliance | Blocking before production |
| Reporting timezone | UTC default | Tenant-local reporting, UTC storage | Product | Blocking |
| Tenant AI policy thresholds | Initial set | Start with drafted set | Compliance | Blocking for admin UI |
| Policy change approval | Open | Require secondary approval for autonomy increases | Compliance | Non-blocking for read-only MVP |
| Email provider auth | Open | Graph first unless pilot needs Gmail/Zoho/IMAP | Customer + Engineering | Customer-dependent |
| API versioning | Drafted | Semantic OpenAPI + changelog | Engineering | Blocking |
