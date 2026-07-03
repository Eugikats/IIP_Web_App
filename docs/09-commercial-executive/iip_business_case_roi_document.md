# Business Case / ROI Document

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Business Case / ROI Document |
| Version | 1.0 |
| Status | Draft |
| Audience | Executive leadership, insurer sponsors, finance, commercial, product |
| Date | 2026-06-30 |

## Core Principles Used Across This Pack

- IIP is the official product name. Older ACIP references are not used as official naming.
- The MVP is centered on motor claims and document intelligence for an insurer pilot.
- IIP assists insurance work; humans make consequential insurance decisions.
- AI may summarize, classify, extract, flag, draft, recommend, and route, but it must not approve or reject claims, bind policies, set final premiums, accuse customers of fraud, promise settlement, or release payment.
- The target architecture is Azure-first with Next.js/React/TypeScript frontend, .NET 8 backend, Azure SQL, Blob Storage, Key Vault, AI Search, Azure OpenAI, Azure AI Document Intelligence, Service Bus/Event Grid, Application Insights, Entra ID/MSAL, and Microsoft Graph where appropriate.
- Customer interaction is WhatsApp-first but can extend to web chat, SMS, email, broker, call center, and Microsoft channels.
- Internal users work through role-aware staff workspaces; customers do not log into the internal app.
- Staging and production must not rely on fake data or mock endpoints. Empty, error, unauthorized, loading, and Not configured states must be handled clearly.


## Executive Summary

The Insurance Intelligence Platform (IIP) is positioned as an AI-assisted insurance operations layer for African insurers. It focuses on improving customer acquisition, motor claims handling, document review, workflow visibility, operational control, auditability, and staff productivity without replacing the insurer's core policy or claims systems.

The business case is built around five value pools:

1. Faster and more reliable customer acquisition through WhatsApp-first engagement and guided intake.
2. Faster claims processing through document intelligence, completeness scoring, triage, and workflow orchestration.
3. Lower operational cost through reduced manual chasing, re-keying, summarization, and status update work.
4. Better leakage and risk control through fraud signals, duplicate checks, SLA alerts, and early warning dashboards.
5. Stronger executive visibility through command-center metrics, audit logs, and source-grounded AI summaries.

All financial benefits in this document are **illustrative** and must be validated with the pilot insurer's baseline data.

## Business Problem

Insurers commonly face operational friction across customer acquisition, claims, document handling, customer communication, approvals, and audit evidence. Staff often work across disconnected channels, email inboxes, claims systems, document repositories, spreadsheets, and manual follow-ups. This creates delays, inconsistent service, incomplete files, hidden backlog, weak audit trails, and difficulty seeing risk early.

IIP addresses these issues by becoming an intelligent operations layer that sits above existing systems. It aggregates information, coordinates workflow, assists staff with AI, and keeps humans responsible for final insurance decisions.

## Value Pool 1: Customer Acquisition and Conversion

| Value lever | IIP contribution | Measurement method |
|---|---|---|
| Faster lead intake | WhatsApp-first guided conversations and structured lead capture | Time from inquiry to complete lead record |
| Higher conversion | Reduced friction between quote inquiry, document capture, follow-up, and handoff | Lead-to-quote and quote-to-bind conversion |
| Better follow-up | AI-assisted reminders and backlog visibility | Percentage of leads followed up within SLA |
| Channel visibility | Channel dashboard for WhatsApp, email, web, call center, and broker traffic | New leads by channel and drop-off stage |

## Value Pool 2: Claims Efficiency

| Value lever | IIP contribution | Measurement method |
|---|---|---|
| Faster FNOL handling | Structured intake and claim creation workflow | FNOL-to-case creation time |
| Faster document review | OCR, classification, extraction, completeness checks, and human review queues | Time from upload to reviewed document |
| Reduced missing-document delays | Missing document detection and customer update drafting | Missing-document cycle time |
| Better claim prioritization | SLA/risk/completeness indicators and escalation flags | Percentage of SLA-at-risk claims addressed before breach |

## Value Pool 3: Operational Cost Reduction

IIP can reduce manual work in areas such as document reading, data extraction, status updates, claim summaries, customer follow-up drafting, management reporting, audit evidence preparation, and issue triage.

| Cost area | Current manual burden | IIP-assisted improvement |
|---|---|---|
| Document handling | Staff manually inspect and re-key documents | AI extracts fields and routes low-confidence items for review |
| Customer updates | Staff manually draft and track updates | AI drafts messages for human approval |
| Case summaries | Supervisors read full files manually | AI prepares source-grounded summaries |
| Reporting | Teams build operational views manually | Command Center and reports surface backlog, SLA, and risk |

## Value Pool 4: Leakage, Fraud Signals, and Risk Control

IIP should not accuse a customer of fraud or make a final fraud determination. It can flag signals for investigation and human review.

| Signal area | Example IIP assistance | Human responsibility |
|---|---|---|
| Duplicate documents | Identify similar files or repeated references | Confirm duplication and action |
| Mismatch detection | Compare names, policy numbers, dates, vehicle details, and incident details | Decide whether mismatch is material |
| Unusual patterns | Highlight repeat visits, repeat claims, high-risk entities, or suspicious timing | Investigate and determine outcome |
| Early warning | Surface operational and claim risk trends | Decide escalation or intervention |

## Value Pool 5: Trust, Auditability, and Governance

IIP strengthens governance by keeping an auditable record of claims work, AI suggestions, human approvals, document evidence, workflow transitions, and customer communications.

| Governance need | IIP support |
|---|---|
| Explainability | AI outputs should be tied to sources, citations, and confidence indicators |
| Accountability | Human user actions, approvals, overrides, and decisions are logged |
| Compliance support | Audit and compliance workspaces surface evidence and history |
| Executive control | Dashboards show open claims, SLA risk, pending approvals, escalations, backlog, and AI usage |

## Illustrative ROI Scenario

The following scenario is an illustrative working model based on prior IIP planning discussions. It must not be treated as a guaranteed forecast.

| Benefit driver | Illustrative annual benefit | Assumption |
|---|---:|---|
| Customer acquisition uplift | USD 12.0M | Faster WhatsApp quote and onboarding flow improves conversion and policy volume |
| Claims savings and retention | USD 3.5M | Reduced operational cost, faster settlement cycles, better retention, and lower leakage |
| Operational reporting and admin efficiency | USD 1.0M | Reduced manual reporting, follow-up, and management preparation |
| Total illustrative gross annual benefit | USD 16.5M | Requires validation with insurer baseline data |
| Illustrative annual investment | USD 250k to USD 500k | Depends on deployment, modules, usage, support, and integration scope |
| Indicative ROI range | 33x to 66x | Based on gross benefit divided by annual investment |
| Indicative payback | 3 to 6 months | Requires client-specific validation |

## Pilot Value Hypothesis

For the first pilot, value should be proven through measurable operational metrics rather than broad enterprise claims. Recommended pilot value targets:

- Reduce average document review time.
- Reduce claims with missing documents beyond SLA.
- Improve visibility of open claims, incomplete claims, and SLA-at-risk claims.
- Improve customer update turnaround for document requests and claim status updates.
- Improve management visibility into operational trends and approval bottlenecks.
- Demonstrate safe, human-reviewed AI use in claims and document workflows.

## Executive KPI Dashboard

| KPI | Definition | Baseline required | Pilot target |
|---|---|---|---|
| FNOL-to-case time | Time between first notice and internal case creation | Yes | Requires Confirmation |
| Document review time | Upload to reviewed/accepted/rejected document | Yes | Requires Confirmation |
| Missing-document resolution time | Time from missing-document detection to complete file | Yes | Requires Confirmation |
| SLA-at-risk count | Claims approaching breach | Yes | Requires Confirmation |
| AI-assisted summaries reviewed | Number of AI summaries used by staff | No | Requires Confirmation |
| Human override rate | Frequency of staff correction of AI outputs | No | Monitor for quality |
| Customer update backlog | Pending customer updates | Yes | Requires Confirmation |

## Investment Areas

| Investment area | Description |
|---|---|
| Platform implementation | Frontend, backend, database, security, monitoring, and deployment |
| AI/document intelligence | OCR, extraction, classification, RAG, AI agents, evaluation, guardrails |
| Integrations | WhatsApp, Graph/email, Blob, policy/claims systems, exports |
| Client onboarding | Configuration, UAT, training, support, pilot management |
| Support and operations | Monitoring, incident handling, improvements, releases |

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Unrealistic ROI expectations | Loss of trust | Mark all ROI numbers as illustrative until validated with client baseline data |
| Weak data quality | Low automation value | Use human review, confidence thresholds, and client data readiness checks |
| Integration delays | Pilot slippage | Start with controlled exports and read-only integrations before deep write-back |
| AI safety issues | Operational or reputational harm | Enforce human review, citations, red-team tests, and prohibited actions |
| User adoption resistance | Low value realization | Role-based training, guided workflows, and visible quick wins |

## Open Questions and Required Confirmations

| Item | Status | Notes |
|---|---|---|
| First pilot insurer | Requires Confirmation | ICEA-like mid-to-large insurer is the working assumption, but the named pilot client must be confirmed. |
| Commercial currency | Requires Confirmation | Pricing can be expressed in USD for investor/client modeling, but local contract currency may vary. |
| Final pilot duration | Requires Confirmation | An 8 to 12 week pilot is assumed unless client procurement or integration timelines require adjustment. |
| Production deployment model | Requires Confirmation | Azure Managed Application is preferred for enterprise clients; Otic-hosted SaaS may be used for demos or smaller clients. |
| Regulatory sign-off requirements | Requires Confirmation | Local insurance, privacy, outsourcing, and client legal requirements must be validated before production. |

