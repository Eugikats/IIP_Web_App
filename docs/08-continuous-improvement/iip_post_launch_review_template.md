# Post-Launch Review Template

| Metadata | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Post-Launch Review Template |
| Version | 1.0 |
| Status | Draft |
| Audience | Product, Customer Success, Support, Engineering, Client Operations |
| Date | 2026-06-30 |

## 1. Purpose

This standalone document supports continuous improvement for IIP after demo, UAT, pilot, and production release. It converts field observations, user feedback, AI quality findings, document-extraction errors, support trends, and release outcomes into structured product learning and prioritized improvements.

## 2. IIP Context Used

IIP is an Azure-first, human-reviewed insurance operations platform for insurance teams. The MVP direction is motor claims and document intelligence, supported by staff workspaces for Command Center, Channels, Customers, Sales & Leads, Claims, Documents, Approvals, Compliance & Audit, Reports, Integrations, and Admin. External customers interact through channels such as WhatsApp, web, email, SMS, broker, and call center; they do not log into the internal staff app.

Core governance rule: AI prepares insurance work; humans make consequential decisions. AI may summarize, extract, classify, recommend, flag, draft, and route, but it must not approve or reject claims, bind cover, set final premiums, accuse customers of fraud, promise settlement, release payments, or make final regulated decisions.

## 3. Purpose

The Post-Launch Review Template provides a repeatable structure for reviewing IIP after launch checkpoints: 2 weeks, 30 days, 60 days, and pilot closeout.

## 4. Review Schedule

| Review | Timing | Focus |
|---|---|---|
| 2-Week Review | 2 weeks after go-live | Stabilization, adoption, urgent issues, training gaps. |
| 30-Day Review | 30 days after go-live | Usage patterns, support trends, KPI direction, early value. |
| 60-Day Review | 60 days after go-live | Deeper KPI review, improvement backlog, expansion readiness. |
| Pilot Closeout | End of pilot | Formal outcomes, lessons, next-phase decision. |

## 5. Review Header Template

| Field | Value |
|---|---|
| Review Type | 2-Week / 30-Day / 60-Day / Closeout |
| Client | Requires Confirmation |
| Review Date | YYYY-MM-DD |
| Period Covered | YYYY-MM-DD to YYYY-MM-DD |
| Facilitator | Requires Confirmation |
| Attendees | Requires Confirmation |
| Overall Status | Green / Amber / Red |

## 6. Review Checklist

| Area | Review Question | Status | Evidence | Action Required |
|---|---|---|---|---|
| Adoption | Are target users logging in and using agreed workflows? |  | Usage report |  |
| Claims Workflow | Are FNOL, document review, triage, and updates working? |  | KPI/report |  |
| Document Intelligence | Are extraction and review issues manageable? |  | DEX log |  |
| AI Quality | Are AI outputs useful, grounded, and safe? |  | AIQ log |  |
| Support | Are issues being resolved within expected timelines? |  | Issue log |  |
| Training | Do users need additional role-based training? |  | Feedback |  |
| Security/RBAC | Are access controls working as expected? |  | Audit/access log |  |
| Integrations | Are configured integrations stable? |  | Provider health |  |
| Executive Value | Are leaders seeing useful operational visibility? |  | Dashboard/KPI review |  |

## 7. KPI Snapshot

| KPI | Baseline | Current | Target | Trend | Notes |
|---|---|---|---|---|---|
| Active users | Requires Confirmation |  |  |  |  |
| Claims processed through IIP | Requires Confirmation |  |  |  |  |
| Average document completeness score | Requires Confirmation |  |  |  |  |
| AI questions answered | Requires Confirmation |  |  |  |  |
| SLA-at-risk backlog | Requires Confirmation |  |  |  |  |
| Support tickets open | Requires Confirmation |  |  |  |  |

## 8. Action Review

| Action ID | Source | Action | Owner | Due Date | Status | Notes |
|---|---|---|---|---|---|---|
| ACT-001 | Previous review | To be completed | Owner | Date | Open/In Progress/Done |  |

## 9. Review Decision

| Decision Area | Decision | Notes |
|---|---|---|
| Continue as planned | Yes/No |  |
| Add remediation actions | Yes/No |  |
| Adjust training | Yes/No |  |
| Adjust configuration | Yes/No |  |
| Add engineering fixes | Yes/No |  |
| Prepare expansion | Yes/No |  |

## 10. Review Output

- Updated issue log.
- Updated feedback log.
- Updated improvement backlog.
- Updated AI quality and document extraction logs where relevant.
- Updated release improvement tracker.
- Client communication summary.

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
