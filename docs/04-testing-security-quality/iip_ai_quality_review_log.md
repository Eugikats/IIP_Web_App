# AI Quality Review Log

| Metadata | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | AI Quality Review Log |
| Version | 1.0 |
| Status | Draft |
| Audience | AI Lead, Product, QA, Compliance, Claims Supervisors, Support |
| Date | 2026-06-30 |

## 1. Purpose

This standalone document supports continuous improvement for IIP after demo, UAT, pilot, and production release. It converts field observations, user feedback, AI quality findings, document-extraction errors, support trends, and release outcomes into structured product learning and prioritized improvements.

## 2. IIP Context Used

IIP is an Azure-first, human-reviewed insurance operations platform for insurance teams. The MVP direction is motor claims and document intelligence, supported by staff workspaces for Command Center, Channels, Customers, Sales & Leads, Claims, Documents, Approvals, Compliance & Audit, Reports, Integrations, and Admin. External customers interact through channels such as WhatsApp, web, email, SMS, broker, and call center; they do not log into the internal staff app.

Core governance rule: AI prepares insurance work; humans make consequential decisions. AI may summarize, extract, classify, recommend, flag, draft, and route, but it must not approve or reject claims, bind cover, set final premiums, accuse customers of fraud, promise settlement, release payments, or make final regulated decisions.

## 3. Purpose

The AI Quality Review Log captures poor, unsafe, unsupported, low-confidence, or confusing AI outputs. It helps improve prompts, retrieval, tool use, guardrails, evaluation tests, and user training.

## 4. AI Quality Rules

| Rule | Requirement |
|---|---|
| Evidence required | AI outputs should be grounded in available documents, claim data, policy knowledge, or clearly state uncertainty. |
| Human approval required | AI must not approve/reject claims, bind cover, set final premiums, accuse fraud, promise settlement, or release payment. |
| No unsupported advice | AI must not make unsupported policy, legal, medical, or regulatory statements. |
| Clear confidence | Low confidence or missing evidence must be visible to the user. |
| Auditability | Significant AI outputs should be traceable through AI interaction or agent run logs. |

## 5. AI Quality Review Log Template

| AIQ ID | Date | Module | Agent/Feature | User Role | Scenario | AI Output Issue | Severity | Evidence Available | Human Correction | Root Cause | Action Required | Owner | Target Release | Status |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| AIQ-001 | YYYY-MM-DD | Claims | Claim Summary Agent | Claims Officer | Motor claim review | Summary omitted repair quotation. | High | Yes | Added missing document reference. | Retrieval gap | Improve retrieval and summary test case. | AI Lead | Pilot | Open |
| AIQ-002 | YYYY-MM-DD | Claims | Fraud Signal Agent | Supervisor | Suspicious claim review | Wording sounded like fraud accusation. | Critical | Yes | Reworded as risk signal only. | Prompt wording | Update prompt and red-team tests. | AI Lead/Compliance | Pilot | Triage |

## 6. Issue Categories

| Category | Description | Example |
|---|---|---|
| Hallucination | AI states something not supported by available sources. | Mentions a police report that was not uploaded. |
| Missing Citation | AI gives an answer without source/evidence reference. | Summarizes policy clause without citation. |
| Unsafe Decision | AI implies final regulated decision. | Says claim should be rejected. |
| Fraud Accusation Risk | AI labels a customer or provider as fraudulent. | Says customer committed fraud instead of flagging risk. |
| Low Confidence Hidden | AI gives confident output despite weak evidence. | Extracted plate number from blurry image without warning. |
| Retrieval Failure | Relevant source exists but was not used. | Policy document ignored in answer. |
| Tone/Communication Issue | Customer-facing draft is unclear or inappropriate. | Message promises payment before approval. |
| Tool Misuse | AI uses wrong tool or lacks permission. | Attempts settlement recommendation without human approval. |

## 7. Severity Definitions

| Severity | Definition | Response |
|---|---|---|
| Critical | Could cause unsafe regulated decision, privacy breach, fraud accusation, or serious customer harm. | Immediate containment and fix before release. |
| High | Could mislead staff or materially affect claim handling. | Fix in current release or pilot before wider use. |
| Medium | Causes confusion or operational delay but human review catches it. | Prioritize for next release. |
| Low | Cosmetic, tone, or minor clarity issue. | Backlog or training update. |

## 8. Review Workflow

1. Capture output, prompt context, module, role, and evidence source.
2. Classify severity and category.
3. Confirm whether human approval gate prevented harm.
4. Identify root cause: prompt, retrieval, tool, UI, data, configuration, or training.
5. Define corrective action.
6. Add regression/red-team test if needed.
7. Validate fixed behavior before closure.
8. Communicate learning to product, QA, support, and training teams.

## 9. Corrective Action Types

| Action | When Used |
|---|---|
| Prompt Update | AI wording, refusal, confidence, or reasoning boundary needs adjustment. |
| Retrieval Update | Chunking, indexing, filters, or citations are weak. |
| Tool Permission Change | Agent can access too much or too little. |
| UI Warning | User needs stronger confidence/evidence/human-review display. |
| Data Correction | Source record or document metadata is wrong. |
| Training Update | User misunderstood AI role or output limitations. |
| Red-Team Test | Issue should be permanently tested before releases. |

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
