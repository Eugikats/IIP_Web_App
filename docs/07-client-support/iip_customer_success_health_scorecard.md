# Customer Success Health Scorecard

| Metadata | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Customer Success Health Scorecard |
| Version | 1.0 |
| Status | Draft |
| Audience | Customer Success, Leadership, Pilot Lead, Support, Product |
| Date | 2026-06-30 |

## 1. Purpose

This standalone document supports continuous improvement for IIP after demo, UAT, pilot, and production release. It converts field observations, user feedback, AI quality findings, document-extraction errors, support trends, and release outcomes into structured product learning and prioritized improvements.

## 2. IIP Context Used

IIP is an Azure-first, human-reviewed insurance operations platform for insurance teams. The MVP direction is motor claims and document intelligence, supported by staff workspaces for Command Center, Channels, Customers, Sales & Leads, Claims, Documents, Approvals, Compliance & Audit, Reports, Integrations, and Admin. External customers interact through channels such as WhatsApp, web, email, SMS, broker, and call center; they do not log into the internal staff app.

Core governance rule: AI prepares insurance work; humans make consequential decisions. AI may summarize, extract, classify, recommend, flag, draft, and route, but it must not approve or reject claims, bind cover, set final premiums, accuse customers of fraud, promise settlement, release payments, or make final regulated decisions.

## 3. Purpose

The Customer Success Health Scorecard tracks whether the client is adopting IIP, realizing value, staying supported, and becoming ready for expansion from pilot to broader rollout.

## 4. Health Categories

| Category | Weight | Meaning |
|---|---|---|
| Adoption | 20% | Are target users actively using agreed workflows? |
| Workflow Value | 20% | Are claims/document workflows improving? |
| AI & Document Quality | 15% | Are AI outputs and extraction quality acceptable? |
| Support Health | 15% | Are support issues controlled and resolved? |
| Executive Sentiment | 10% | Do sponsors believe IIP is valuable? |
| Compliance & Trust | 10% | Are audit, RBAC, and governance controls trusted? |
| Expansion Readiness | 10% | Is client ready to extend modules, teams, or volume? |

## 5. Scorecard Template

| Category | Metric | Target | Current | Score 1-5 | Trend | Evidence | Owner | Action |
|---|---|---|---|---|---|---|---|---|
| Adoption | Active users per week | Requires Confirmation |  |  |  | Usage report | Customer Success |  |
| Workflow Value | Claims reviewed in IIP | Requires Confirmation |  |  |  | Claims dashboard | Pilot Lead |  |
| AI Quality | Critical AI issues open | 0 |  |  |  | AIQ log | AI Lead |  |
| Document Quality | High severity extraction errors | Requires Confirmation |  |  |  | DEX log | Doc AI Lead |  |
| Support | Sev 1/2 open items | 0 or agreed threshold |  |  |  | Issue log | Support Lead |  |
| Executive Sentiment | Sponsor rating | Requires Confirmation |  |  |  | Review notes | Account Lead |  |

## 6. Health Rating

| Rating | Score Range | Meaning | Action |
|---|---|---|---|
| Green | 4.0-5.0 | Healthy, adoption/value on track. | Continue and prepare expansion. |
| Amber | 2.8-3.9 | Some risks or adoption issues. | Create remediation plan. |
| Red | Below 2.8 | Value, adoption, or support risk. | Escalate and stabilize before expansion. |

## 7. Health Review Cadence

| Cadence | Participants | Output |
|---|---|---|
| Weekly during pilot | Pilot Lead, Client Ops Lead, Support, Product | Updated issues, risks, feedback, KPI snapshot. |
| Biweekly product review | Product, Engineering, AI, Support | Improvement decisions and release tracker updates. |
| Monthly executive review | Client sponsor, Otic leadership, Product | Health rating, value realization, next-phase decisions. |

## 8. Expansion Readiness Checklist

- Pilot scope is stable and users are active.
- Critical security, RBAC, AI safety, and document extraction issues are closed or controlled.
- Client has named owners for rollout, training, support, and data/integration decisions.
- Success metrics show positive trend or agreed value signal.
- Support load is manageable.
- Roadmap expansion scope is approved through change control.

## 9. Action Plan Template

| Action ID | Health Category | Issue | Action | Owner | Due Date | Expected Impact | Status |
|---|---|---|---|---|---|---|---|
| HACT-001 | Adoption | Low supervisor usage | Schedule role-based refresher training. | Customer Success | Date | Higher supervisor review activity. | Open |

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
