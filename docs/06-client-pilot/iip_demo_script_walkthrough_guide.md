# Demo Script / Walkthrough Guide

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Demo Script / Walkthrough Guide |
| Version | 1.0 |
| Status | Draft |
| Audience | Sales team, product demos, pilot trainers, client sponsors |
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


## Demo Objective


---

# July 2026 Update: Chat-First Command Center Demo Flow

## Demo Positioning Change

The demo should now begin with the **chat-first Command Center**, not the Claims Inbox or a static dashboard.

The story should show that IIP is an operational command workspace where a claims manager or operations user can ask natural-language questions and receive real operational blocks, evidence, and safe next actions.

## New Opening Demo Flow: Command Chat Briefing

**Goal:** Show IIP as the user's daily operational command hub.

**Steps:**

1. Open IIP Operations and land on Command Center.
2. Point out the three-panel layout: left navigation, central command chat, right Context & Evidence Rail.
3. Show the daily briefing message in the central chat.
4. Highlight metric pills inside the chat: open claims, SLA breaches, pending approvals, incomplete claims, customer updates, document completeness.
5. Ask: `What needs my attention today?`
6. Show the assistant returning a prioritized list with embedded blocks.
7. Ask: `Show me SLA breaches.`
8. Show the SLA breaches table inside the chat response.
9. Point to the right rail: current focus, why this result, sources, confidence, selected claim, and human responsibility.
10. Click `Open claim details` or `Review` to transition into the claims/case flow.

**Talk track:**

> IIP is not a static dashboard and not a generic chatbot. The Command Center is a conversation-first operations workspace. Metrics, claims, documents, evidence, and next actions appear directly inside the conversation, while the right panel shows why the system reached its answer and what still needs human review.

## Demo Prompts to Use

```text
What needs my attention today?
Show SLA breaches.
Which motor claims are missing police reports?
Prepare customer update drafts for claims waiting on documents.
Generate a daily claims operations summary for management.
Show me claims where AI prepared work but a human has not reviewed it.
```

## Demo Safety Notes

Always explain:

- AI prepares and summarizes work; humans decide.
- Risk signals are not fraud accusations.
- Customer messages remain drafts unless approved or configured as low-risk.
- Evidence and sources are displayed for operational trust.
- If a provider is not configured, IIP shows `Not configured`; it does not fake the integration.

## Updated Demo Order

| Order | Demo Flow | Purpose |
|---:|---|---|
| 1 | Chat-first Command Center briefing | Establish IIP's main product experience. |
| 2 | SLA breach query and embedded results | Show conversational operations intelligence. |
| 3 | Claims Inbox / filtered transition | Show that chat actions link to operational records. |
| 4 | Case Review | Show detailed claim evidence and human workflow. |
| 5 | Document Intelligence | Show extraction, missing documents, and review. |
| 6 | Customer Update Draft | Show AI draft + human approval. |
| 7 | Audit and evidence | Show compliance and trust. |
| 8 | Admin/Health | Show provider states and production readiness. |

## Phase 1-4 Demo Alignment

| Phase | Demo Capability |
|---|---|
| Phase 1 | Show static but production-safe chat-first layout using clearly labeled synthetic/local test data only. |
| Phase 2 | Show real API-backed briefing, metrics, SLA blocks, claims blocks, and provider status. |
| Phase 3 | Show agentic queries, evidence, RAG answers, action proposals, and approval tasks. |
| Phase 4 | Show production-candidate flow with security, audit, no fake data, and release evidence. |
Demonstrate how IIP supports an insurer from customer intake through motor claim document review, AI-assisted case preparation, human decision support, customer update drafting, auditability, and executive visibility.

## Demo Setup Checklist

| Item | Required state |
|---|---|
| Environment | Demo or pilot environment clearly marked, not production unless approved |
| User role | Demo user should have an appropriate role such as Claims Officer or Admin |
| Tenant/workspace | Tenant and workspace context visible in top bar |
| Provider health | Health states visible; Not configured states handled where applicable |
| Sample claim | Use non-sensitive sample claim data |
| Sample documents | Use synthetic or approved sample motor claim documents |
| AI disclaimers | Human-review boundaries must be stated during demo |

## Demo Storyline

A motor insurance customer reports an accident through a supported channel. IIP helps the insurer capture the claim, request and review documents, identify missing information, summarize the case, route work to staff, and maintain an audit trail. The claim is not approved by AI; a human user remains responsible for final decisions.

## Demo Flow 1: WhatsApp FNOL Intake

**Goal:** Show how external customer messages can feed internal insurer workflows.

**Steps:**

1. Start with a customer message such as: "I need to report a motor accident."
2. Show that IIP can structure the intake into key fields: customer, policy number, date, location, vehicle, incident description, contact details.
3. Show the internal staff view receiving the intake in Channels or Claims.
4. Highlight that customers do not log into the internal staff app.

**Talk track:** IIP turns messy channel communication into structured operational work while keeping staff in control.

## Demo Flow 2: Claim Creation and Claims Inbox

**Goal:** Show a claim entering the operational queue.

**Steps:**

1. Open Claims Inbox.
2. Show dense table with filters for status, product line, risk, SLA, channel, and assigned officer.
3. Search by claim ID, customer, or policy number.
4. Open a claim preview or drawer.
5. Show status badges such as incomplete, SLA at risk, pending documents, or review required.

**Expected message:** Managers and officers can see what needs attention instead of chasing claims manually.

## Demo Flow 3: Document Upload and Intelligence

**Goal:** Show OCR, classification, extraction, completeness checks, and human review.

**Steps:**

1. Upload sample documents: claim form, police report, national ID, driving permit, vehicle photos, repair quotation, policy schedule.
2. Show document classification.
3. Show extracted fields: name, ID number, policy number, vehicle registration, date of accident, location, repair amount, document date.
4. Show confidence indicators and low-confidence fields.
5. Show missing documents or mismatched data.
6. Demonstrate human review: accept, correct, reject, or request replacement.

**Safety note:** AI extraction is treated as assistance. Staff confirm or correct before operational reliance.

## Demo Flow 4: AI Case Summary

**Goal:** Show source-grounded case summarization.

**Steps:**

1. Open the case review page.
2. Ask the assistant to summarize the claim.
3. Show the summary broken into incident, policy, documents received, missing documents, risk signals, and recommended next actions.
4. Highlight citations or source references where available.
5. Show that the user can approve, copy, edit, or reject the summary.

**Talk track:** The AI prepares a working summary, but the claims officer is responsible for validating it.

## Demo Flow 5: Fraud Signal / Risk Signal Review

**Goal:** Show risk signal language without overclaiming fraud detection.

**Steps:**

1. Open the risk or signals panel.
2. Show examples such as duplicate document signal, date mismatch, repeated claim pattern, missing assessor report, or high repair estimate.
3. Explain that IIP flags signals for investigation only.
4. Demonstrate escalation to supervisor or investigation queue.

**Required phrasing:** IIP does not accuse the customer of fraud. It identifies review signals for human investigation.

## Demo Flow 6: Customer Update Draft

**Goal:** Show AI-assisted but human-approved customer communication.

**Steps:**

1. Click draft customer update.
2. Generate a message requesting missing documents or confirming receipt.
3. Show editable draft.
4. Human user reviews and sends or rejects.
5. Audit trail records the action.

**Talk track:** AI reduces drafting time but cannot promise settlement or final claim outcome.

## Demo Flow 7: Approval and Escalation

**Goal:** Show human workflow control.

**Steps:**

1. Route the claim to a supervisor or approver.
2. Show pending approvals and escalation required indicators.
3. Show human decision options and required comments.
4. Highlight audit log entry.

## Demo Flow 8: Command Center

**Goal:** Show executive and operational visibility.

**Steps:**

1. Open Command Center.
2. Show cards for open claims, new claims today, incomplete claims, SLA at risk/breached, pending approvals, customer update backlog, AI questions answered, escalations required, average completeness score.
3. Show trend cards and quick links to claims, approvals, documents, and reports.
4. Explain that leadership can monitor bottlenecks and service risk.

## Demo Flow 9: Audit Trail

**Goal:** Show governance and explainability.

**Steps:**

1. Open audit timeline for the case.
2. Show document upload, extraction, AI summary generation, human review, customer message, escalation, and decision events.
3. Show user, timestamp, source, and correlation where available.

## Demo Close

**Closing message:** IIP is designed to make insurance operations faster, clearer, and more auditable without removing human accountability. The pilot proves this with motor claims and document intelligence before expanding into acquisition, fraud signals, early warning, and underwriting support.

## Demo Risks to Avoid

| Risk | Avoidance |
|---|---|
| Saying AI approves claims | Say AI assists; humans decide |
| Saying fraud is detected conclusively | Say risk signals are flagged for review |
| Showing fake production claims | Use synthetic or approved sample data |
| Overpromising integrations | State which integrations are configured and which are future |
| Hiding Not configured states | Use them to show honest readiness and configuration control |

## Open Questions and Required Confirmations

| Item | Status | Notes |
|---|---|---|
| First pilot insurer | Requires Confirmation | ICEA-like mid-to-large insurer is the working assumption, but the named pilot client must be confirmed. |
| Commercial currency | Requires Confirmation | Pricing can be expressed in USD for investor/client modeling, but local contract currency may vary. |
| Final pilot duration | Requires Confirmation | An 8 to 12 week pilot is assumed unless client procurement or integration timelines require adjustment. |
| Production deployment model | Requires Confirmation | Azure Managed Application is preferred for enterprise clients; Otic-hosted SaaS may be used for demos or smaller clients. |
| Regulatory sign-off requirements | Requires Confirmation | Local insurance, privacy, outsourcing, and client legal requirements must be validated before production. |
