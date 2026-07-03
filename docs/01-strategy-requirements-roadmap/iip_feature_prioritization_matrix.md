# Feature Prioritization Matrix

| Metadata | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Feature Prioritization Matrix |
| Version | 1.0 |
| Status | Draft |
| Audience | Product Leadership, Engineering Leads, Client Steering Committee, Customer Success |
| Date | 2026-06-30 |

## 1. Purpose

This standalone document supports continuous improvement for IIP after demo, UAT, pilot, and production release. It converts field observations, user feedback, AI quality findings, document-extraction errors, support trends, and release outcomes into structured product learning and prioritized improvements.

## 2. IIP Context Used

IIP is an Azure-first, human-reviewed insurance operations platform for insurance teams. The MVP direction is motor claims and document intelligence, supported by staff workspaces for Command Center, Channels, Customers, Sales & Leads, Claims, Documents, Approvals, Compliance & Audit, Reports, Integrations, and Admin. External customers interact through channels such as WhatsApp, web, email, SMS, broker, and call center; they do not log into the internal staff app.

Core governance rule: AI prepares insurance work; humans make consequential decisions. AI may summarize, extract, classify, recommend, flag, draft, and route, but it must not approve or reject claims, bind cover, set final premiums, accuse customers of fraud, promise settlement, release payments, or make final regulated decisions.

## 3. Purpose

The Feature Prioritization Matrix helps decide which requested IIP capabilities should be built now, later, or not at all. It is especially useful during pilot feedback review, roadmap planning, and commercial discussions.

## 4. Feature Prioritization Principles

| Principle | Description |
|---|---|
| Protect MVP focus | Prioritize motor claims and document intelligence before broader expansion. |
| Human review is non-negotiable | Do not prioritize features that imply autonomous claims, pricing, fraud, or payment decisions. |
| Client value must be explicit | Every feature should link to a measurable workflow, KPI, compliance need, or revenue opportunity. |
| Implementation risk matters | Avoid high-risk work without architecture and security review. |
| Reuse platform capabilities | Prefer features that strengthen shared modules rather than isolated one-off custom work. |

## 5. Feature Matrix Template

| Feature ID | Feature Name | Request Source | Module | User Role | Client Value | User Impact | Revenue Impact | Compliance Value | Strategic Fit | Effort | Risk | Urgency | Score | Decision | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| FEAT-001 | Repairer quotation comparison | Pilot feedback | Claims | Claims Officer | 4 | 4 | 3 | 2 | 4 | 3 | 3 | 4 | TBD | Evaluate | Requires document extraction and rules confirmation. |
| FEAT-002 | Customer portal login | Stakeholder idea | Customer | External Customer | 2 | 2 | 2 | 2 | 1 | 5 | 4 | 1 | TBD | Defer | Customers currently use channels; internal app is staff-only. |

## 6. Scoring Guidance

| Criterion | 1 | 3 | 5 |
|---|---|---|---|
| Client Value | Nice to have | Supports workflow KPI | Critical to client value realization |
| User Impact | Small convenience | Saves time or reduces confusion | Removes major operational bottleneck |
| Revenue Impact | No clear commercial impact | Supports retention or upsell | Strong sales/expansion driver |
| Compliance Value | None | Improves evidence/control | Reduces serious compliance risk |
| Strategic Fit | Peripheral | Adjacent to roadmap | Directly supports IIP core direction |
| Effort | Very high | Medium | Low |
| Risk | High | Medium | Low |
| Urgency | Later | Next release | Current pilot/release |

## 7. Decision Categories

| Decision | Meaning |
|---|---|
| Build Now | Needed for MVP, pilot success, security, compliance, or serious adoption blocker. |
| Build Next | Valuable but not required for immediate pilot completion. |
| Defer | Valid but outside current phase. |
| Configure | Can be solved through settings, templates, roles, or workflow configuration. |
| Train | Solved through enablement rather than software change. |
| Reject | Not aligned with IIP direction, unsafe, or unsupported. |
| Requires Discovery | Needs client, legal, technical, or integration confirmation. |

## 8. Review Meeting Agenda

| Agenda Item | Time | Output |
|---|---|---|
| Review new feature requests | 15 min | Clarified request list |
| Confirm problem statements | 15 min | Validated user/client problem |
| Score features | 20 min | Draft priority score |
| Identify risks/dependencies | 15 min | Open questions and owners |
| Decide disposition | 20 min | Build/defer/configure/train/reject decisions |
| Communicate outcomes | 10 min | Update plan and client messaging |

## 9. Acceptance Criteria for Prioritization

- Each feature has a clear problem statement.
- Each feature has a target user role and module.
- MVP and human-review boundaries are respected.
- Effort and risk are reviewed by engineering.
- Compliance or security-sensitive features are reviewed before approval.
- Decision and rationale are recorded.

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
