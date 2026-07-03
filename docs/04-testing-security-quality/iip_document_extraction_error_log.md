# Document Extraction Error Log

| Metadata | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Document Extraction Error Log |
| Version | 1.0 |
| Status | Draft |
| Audience | Document Intelligence Team, QA, Claims Operations, Support |
| Date | 2026-06-30 |

## 1. Purpose

This standalone document supports continuous improvement for IIP after demo, UAT, pilot, and production release. It converts field observations, user feedback, AI quality findings, document-extraction errors, support trends, and release outcomes into structured product learning and prioritized improvements.

## 2. IIP Context Used

IIP is an Azure-first, human-reviewed insurance operations platform for insurance teams. The MVP direction is motor claims and document intelligence, supported by staff workspaces for Command Center, Channels, Customers, Sales & Leads, Claims, Documents, Approvals, Compliance & Audit, Reports, Integrations, and Admin. External customers interact through channels such as WhatsApp, web, email, SMS, broker, and call center; they do not log into the internal staff app.

Core governance rule: AI prepares insurance work; humans make consequential decisions. AI may summarize, extract, classify, recommend, flag, draft, and route, but it must not approve or reject claims, bind cover, set final premiums, accuse customers of fraud, promise settlement, release payments, or make final regulated decisions.

## 3. Purpose

The Document Extraction Error Log captures OCR, classification, extraction, validation, and confidence failures for motor claims documents. It helps improve document templates, Azure AI Document Intelligence models, validation rules, user review screens, and QA test packs.

## 4. Target Document Types

| Document Type | MVP Role |
|---|---|
| Claim form | Captures claim details and incident basics. |
| Police report | Confirms incident reporting and official incident details. |
| National ID | Helps verify claimant identity. |
| Driving permit | Helps verify driver eligibility and expiry. |
| Vehicle photos | Supports damage assessment workflow. |
| Repair quotation | Supports repair cost review. |
| Policy schedule | Confirms cover details and policy status. |
| Assessor report | Supports evaluation where available. |

## 5. Error Log Template

| Error ID | Date | Claim Ref | Document Type | File Quality | Error Category | Field/Area | Expected Value | Extracted Value | Confidence | Human Correction | Severity | Root Cause | Corrective Action | Owner | Status |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| DEX-001 | YYYY-MM-DD | CLM-XXX | Driving permit | Blurry | Extraction | Expiry date | 2027-05-10 | 2021-05-10 | 0.54 | Corrected expiry date. | High | OCR ambiguity | Add validation and low-confidence warning. | Doc AI Lead | Open |
| DEX-002 | YYYY-MM-DD | CLM-XXX | Police report | Good | Classification | Document type | Police report | Claim form | 0.62 | Reclassified manually. | Medium | Template variation | Add sample to test set. | QA | Triage |

## 6. Error Categories

| Category | Description | Example |
|---|---|---|
| OCR Error | Text read incorrectly. | Plate number B instead of 8. |
| Classification Error | Wrong document type. | Assessor report classified as repair quotation. |
| Extraction Error | Wrong field value extracted. | Incident date extracted from print date. |
| Missing Field | Required field not extracted. | Policy number missing from schedule. |
| Low Confidence | Field extracted but confidence below threshold. | ID number low confidence. |
| Validation Error | Extracted field fails business rule. | Claim date after submission date. |
| Cross-Document Mismatch | Values do not match across documents. | Vehicle registration differs between police report and policy schedule. |
| Duplicate Detection Error | Duplicate document not detected or false duplicate detected. | Same quotation uploaded twice. |
| Review UI Issue | Human reviewer cannot easily correct/confirm extraction. | Field correction not saved. |

## 7. Severity Guidance

| Severity | Definition |
|---|---|
| Critical | Error may lead to wrong identity, policy, claim, payment, or compliance outcome if not caught. |
| High | Error materially slows review or could mislead claims staff. |
| Medium | Error requires correction but does not block workflow. |
| Low | Cosmetic, formatting, or minor extraction display issue. |

## 8. Field Validation Rules to Track

| Field | Example Validation |
|---|---|
| Claim date | Must not be future date; should be within policy period or flagged. |
| Policy number | Must match known policy format or policy lookup. |
| Vehicle registration | Should match policy schedule where available. |
| Driver permit expiry | Expired permit should be flagged, not decided by AI. |
| National ID | Format and completeness check where applicable. |
| Repair amount | Must parse currency and numeric value; high values should be review flagged. |
| Document date | Must be captured separately from print/upload date. |

## 9. Review Workflow

1. Human reviewer identifies extraction/classification issue.
2. Capture source document type, field, extracted value, expected value, and confidence.
3. Assign severity and root cause.
4. Decide corrective path: validation rule, sample expansion, model improvement, UI improvement, or training.
5. Add test case to Document Intelligence Test Pack where needed.
6. Validate fix against similar documents.
7. Close only after reviewer confirms expected behavior.

## Open Questions and Required Confirmations

| ID | Open Question | Owner | Needed By | Notes |
|---|---|---|---|---|
| OQ-01 | Confirm the first pilot insurer and named pilot departments. | Product / Leadership | Before pilot kickoff | Use placeholders until client is confirmed. |
| OQ-02 | Confirm target KPI baselines from the client: current FNOL time, document completion time, claim triage time, and claim update backlog. | Pilot Lead | Before baseline measurement | Do not invent client baselines. |
| OQ-03 | Confirm support tooling to be used for issue, feedback, and improvement tracking. | Delivery / Support | Before go-live | Options may include Planner, Jira, DevOps, or spreadsheet. |
| OQ-04 | Confirm data retention, log retention, and evidence retention periods for pilot and production. | Compliance / Client | Before production | Must align with client policy and applicable law. |
| OQ-05 | Confirm release cadence and improvement review cadence. | Product / Engineering | Before first release cycle | Suggested cadence is provided as an assumption only. |

## Usage Rules

- Use this document as an operational template, not as a substitute for legal, compliance, or client-specific policy review.
- Treat all examples as starting points requiring client confirmation.
- Record sensitive client data only in approved systems.
- Keep AI quality and document-extraction issues linked to traceable evidence where possible.
- Never use continuous improvement findings to bypass human approval gates.
