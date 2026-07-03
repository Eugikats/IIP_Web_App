# Feedback Log Template

| Metadata | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Feedback Log Template |
| Version | 1.0 |
| Status | Draft |
| Audience | Product, Customer Success, Pilot Team, Support, Engineering |
| Date | 2026-06-30 |

## 1. Purpose

This standalone document supports continuous improvement for IIP after demo, UAT, pilot, and production release. It converts field observations, user feedback, AI quality findings, document-extraction errors, support trends, and release outcomes into structured product learning and prioritized improvements.

## 2. IIP Context Used

IIP is an Azure-first, human-reviewed insurance operations platform for insurance teams. The MVP direction is motor claims and document intelligence, supported by staff workspaces for Command Center, Channels, Customers, Sales & Leads, Claims, Documents, Approvals, Compliance & Audit, Reports, Integrations, and Admin. External customers interact through channels such as WhatsApp, web, email, SMS, broker, and call center; they do not log into the internal staff app.

Core governance rule: AI prepares insurance work; humans make consequential decisions. AI may summarize, extract, classify, recommend, flag, draft, and route, but it must not approve or reject claims, bind cover, set final premiums, accuse customers of fraud, promise settlement, release payments, or make final regulated decisions.

## 3. When to Use This Template

Use the Feedback Log whenever a user, client stakeholder, support agent, implementation team member, or pilot observer reports a pain point, improvement suggestion, confusion, workflow delay, or usability issue.

## 4. Feedback Intake Principles

| Principle | Rule |
|---|---|
| Capture context | Record who gave the feedback, their role, module, workflow stage, and scenario. |
| Separate fact from opinion | Keep observed behavior separate from interpretation. |
| Link to evidence | Attach screenshots, claim references, error IDs, audit events, or support ticket IDs where allowed. |
| Protect sensitive information | Do not paste policyholder personal data into general feedback tools unless approved. |
| Convert to action | Every accepted item should become either a support action, configuration change, backlog item, training note, or product decision. |

## 5. Feedback Log Template

| Feedback ID | Date | Source | Role | Module | Workflow Stage | Feedback Type | Description | Evidence Link | Severity | Frequency | Owner | Proposed Action | Status | Follow-Up Date |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| FB-001 | YYYY-MM-DD | User interview | Claims Officer | Claims Inbox | Document review | Pain point | User cannot quickly see missing police report. | Link | Medium | Repeated | Product | Improve missing-doc badge visibility. | Open | YYYY-MM-DD |
| FB-002 | YYYY-MM-DD | Support ticket | Admin | Admin Settings | User setup | Question | Admin unsure how role permissions affect screens. | Ticket ID | Low | Once | Customer Success | Add quick guide note. | Triage | YYYY-MM-DD |

## 6. Feedback Classification

| Type | Definition | Example | Likely Destination |
|---|---|---|---|
| Bug | Product behaves incorrectly or fails. | Claim drawer fails to load. | Issue log / engineering backlog |
| Usability | Product works but is confusing or slow. | Search filters are hard to discover. | UX backlog |
| Configuration | Client-specific setup needs adjustment. | SLA threshold should be 48 hours, not 24. | Admin / configuration change |
| Training | User needs explanation or practice. | Users do not understand AI confidence labels. | Training plan / FAQ |
| AI Quality | AI output is incomplete, unsupported, unsafe, or unclear. | AI summary omits key document. | AI Quality Review Log |
| Document Extraction | OCR, classification, validation, or confidence issue. | Driving permit expiry date misread. | Document Extraction Error Log |
| Feature Request | New capability or workflow. | Add repairer quotation comparison. | Improvement backlog |

## 7. Triage Checklist

- Is this feedback tied to the MVP scope or future phase?
- Is this a bug, support issue, configuration issue, training need, or product improvement?
- Does it affect human decision making, compliance, auditability, or customer communication?
- Is there evidence or a reproducible scenario?
- Does the feedback require client approval before change?
- Does the issue create risk if left unresolved?

## 8. Status Values

| Status | Meaning |
|---|---|
| New | Captured but not reviewed. |
| Triage | Under review by product/support. |
| Accepted | Valid and approved for action. |
| Deferred | Valid but not current priority. |
| Rejected | Not aligned, duplicate, or invalid. |
| Converted | Converted into backlog, support, training, or configuration item. |
| Closed | Completed and communicated. |

## 9. Acceptance Criteria for a Good Feedback Item

| Criterion | Expected Standard |
|---|---|
| Clear description | The issue or suggestion can be understood without a meeting. |
| Context provided | Role, module, and workflow stage are known. |
| Evidence captured | Screenshot, ticket, event ID, or user quote is attached where possible. |
| Owner assigned | Someone is accountable for next action. |
| Next step defined | It is clear whether this becomes support, training, configuration, or product work. |

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
