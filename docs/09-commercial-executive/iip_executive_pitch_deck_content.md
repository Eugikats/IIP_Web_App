# Executive Pitch Deck Content Document

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Executive Pitch Deck Content Document |
| Version | 1.0 |
| Status | Draft |
| Audience | Executive sponsors, boardroom presenters, sales team, product leadership |
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


## Purpose

This document provides boardroom-ready slide content for an IIP executive pitch. It is not a PowerPoint file; it is the approved content outline to be converted into a presentation when requested.

## Recommended Deck Structure

| Slide | Title | Purpose |
|---:|---|---|
| 1 | Insurance Intelligence Platform (IIP) | Introduce the platform and core promise |
| 2 | The Insurance Operations Problem | Show pain points across acquisition, claims, documents, approvals, and visibility |
| 3 | What IIP Is | Define IIP as an intelligent operations layer, not a core system replacement |
| 4 | Why Now | Explain AI, digital channels, customer expectations, and operational pressure |
| 5 | MVP Focus: Motor Claims and Document Intelligence | Anchor the first pilot scope |
| 6 | How It Works | Show WhatsApp/customer intake to staff workspace to documents to human review |
| 7 | Human-Controlled AI | Explain responsible AI boundaries and governance |
| 8 | Core Modules | Command Center, Claims, Documents, Channels, Customers, Sales/Leads, Audit, Admin |
| 9 | Business Value | Acquisition, claims efficiency, cost reduction, risk control, auditability |
| 10 | Pilot Approach | Scope, timeline, users, success metrics, governance |
| 11 | Architecture and Security | Azure-first, tenant-aware, audit-ready, Microsoft-native architecture |
| 12 | Roadmap | MVP, Phase 2 modules, enterprise scaling, marketplace/deployment path |
| 13 | What We Need From the Client | Sponsor, data, users, process owners, integrations, legal/security review |
| 14 | Decision Request | Approve pilot discovery, configuration, and controlled rollout |

## Slide 1: Insurance Intelligence Platform (IIP)

**Headline:** AI-assisted insurance operations, built for human control.

**Key message:** IIP helps insurers acquire customers, process claims, review documents, manage workflows, and surface risk signals through a secure, auditable, Azure-native platform.

**Speaker notes:** Emphasize that IIP is not replacing the insurer's core system. It is an operational intelligence layer that helps staff work faster and with better visibility.

## Slide 2: The Insurance Operations Problem

**Headline:** Insurance teams are operating across disconnected channels and manual workflows.

**Points:**

- Customer requests arrive through WhatsApp, calls, email, brokers, branches, and websites.
- Claims files are delayed by missing documents and manual review.
- Managers lack real-time visibility into backlog, SLA risk, and escalations.
- AI is useful only if it is controlled, auditable, and grounded in evidence.

## Slide 3: What IIP Is

**Headline:** IIP is the intelligent operating layer above insurance systems.

| IIP does | IIP does not |
|---|---|
| Aggregates work across channels, claims, documents, and workflows | Replace the core policy or claims system |
| Assists staff with summaries, extraction, routing, and risk signals | Make final claim, fraud, pricing, or settlement decisions |
| Provides operational visibility and audit evidence | Operate as an uncontrolled chatbot |

## Slide 4: Why Now

**Headline:** The market is ready for AI-assisted insurance operations, but only with governance.

**Points:**

- Customer expectations are moving toward instant digital service.
- Insurers need better claims turnaround, document control, and SLA visibility.
- AI can reduce manual work, but regulated decisions still require human accountability.
- Microsoft/Azure-native deployment aligns with enterprise security expectations.

## Slide 5: MVP Focus

**Headline:** Start with motor claims and document intelligence.

**MVP scope:**

- Motor FNOL and claim workspace.
- Document upload, classification, extraction, and completeness review.
- Claims inbox and case review.
- Command Center metrics.
- Human-reviewed AI summaries and draft customer updates.
- Audit trail and role-aware access.

## Slide 6: How It Works

**Flow:**

1. Customer initiates claim or query through WhatsApp or supported channel.
2. IIP structures intake and creates or updates internal work item.
3. Documents are uploaded and processed using document intelligence.
4. AI prepares summaries, missing-document checks, and risk signals.
5. Staff review, approve, correct, escalate, or send updates.
6. Managers track backlog, SLA risk, and operational trends.

## Slide 7: Human-Controlled AI

**Headline:** AI prepares the work. Humans make the decisions.

**AI may:** summarize, extract, classify, compare, flag, recommend, draft, route.

**AI must not:** approve/reject claims, bind policies, set final premiums, accuse customers of fraud, promise settlement, release payments, or make final regulated decisions.

## Slide 8: Core Modules

| Module | Executive value |
|---|---|
| Command Center | Visibility into operational health |
| Claims | Faster handling, prioritization, and supervision |
| Documents | Less manual review and fewer incomplete files |
| Channels | Better intake and customer update control |
| Customers | Unified customer context and timeline |
| Sales & Leads | Acquisition and follow-up visibility |
| Audit & Compliance | Evidence, traceability, and accountability |
| Admin | Roles, configuration, templates, and controls |

## Slide 9: Business Value

**Headline:** IIP targets measurable improvements across growth, efficiency, risk, and governance.

| Value area | Example metric |
|---|---|
| Acquisition | Lead-to-quote and quote-to-bind conversion |
| Claims | FNOL-to-case time and document review time |
| Operations | Backlog, SLA risk, escalations, pending approvals |
| Risk | Duplicate/mismatch flags and investigation queues |
| Governance | Audit log completeness and human review traceability |

## Slide 10: Pilot Approach

**Headline:** Prove value in a controlled pilot before scaling.

**Pilot timeline:** 8 to 12 weeks, subject to client readiness.

**Pilot users:** claims officers, supervisors, admins, compliance, customer service, executives.

**Pilot outputs:** configured environment, UAT sign-off, training, weekly metrics, closeout report, next-phase recommendation.

## Slide 11: Architecture and Security

**Headline:** Azure-first, tenant-aware, and auditable.

**Architecture points:**

- Next.js/React frontend and .NET 8 backend.
- Azure SQL, Blob Storage, Key Vault, AI Search, Azure OpenAI, Document Intelligence, Service Bus/Event Grid.
- Entra ID/MSAL, role-aware UI, server-side authorization, audit logging.
- Managed Application path for enterprise customer-tenant deployments.

## Slide 12: Roadmap

| Phase | Focus |
|---|---|
| MVP | Motor claims, documents, command center, audit, AI assistance |
| Phase 2 | Customer acquisition, fraud signals, early warning, underwriting support |
| Phase 3 | Deeper integrations, analytics, marketplace/private deployment, multi-client scaling |

## Slide 13: What We Need From the Client

- Executive sponsor and pilot owner.
- Sample claims and document templates.
- Confirmed workflows, SLAs, roles, and branches.
- Identity/security/integration contacts.
- Legal/compliance review pathway.
- UAT users and pilot participants.

## Slide 14: Decision Request

**Decision requested:** Approve pilot discovery and configuration for a controlled motor claims and document intelligence pilot.

**Immediate next steps:** complete client readiness checklist, agree pilot scope, complete configuration workbook, confirm deployment model, confirm timeline.

## Open Questions and Required Confirmations

| Item | Status | Notes |
|---|---|---|
| First pilot insurer | Requires Confirmation | ICEA-like mid-to-large insurer is the working assumption, but the named pilot client must be confirmed. |
| Commercial currency | Requires Confirmation | Pricing can be expressed in USD for investor/client modeling, but local contract currency may vary. |
| Final pilot duration | Requires Confirmation | An 8 to 12 week pilot is assumed unless client procurement or integration timelines require adjustment. |
| Production deployment model | Requires Confirmation | Azure Managed Application is preferred for enterprise clients; Otic-hosted SaaS may be used for demos or smaller clients. |
| Regulatory sign-off requirements | Requires Confirmation | Local insurance, privacy, outsourcing, and client legal requirements must be validated before production. |

