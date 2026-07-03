# FAQ / Objection Handling Document

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | FAQ / Objection Handling Document |
| Version | 1.0 |
| Status | Draft |
| Audience | Sales, executives, client stakeholders, security reviewers, pilot sponsors |
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

This document prepares IIP presenters and client-facing teams to answer common buyer, executive, security, operations, and claims questions clearly and responsibly.

## Core Positioning Answer

IIP is an AI-assisted insurance operations platform. It helps insurers coordinate claims, documents, customer channels, knowledge, approvals, and operational visibility. It does not replace the insurer's core systems and does not remove human decision-making from regulated insurance processes.

## Buyer FAQ

| Question | Recommended answer |
|---|---|
| Is IIP replacing our core insurance system? | No. IIP sits above existing systems as an operational intelligence and workflow layer. Core policy, claims, finance, and regulatory systems remain systems of record unless a future integration is approved. |
| Can AI approve or reject claims? | No. AI can summarize, classify, extract, flag, recommend, and draft. Human users approve, reject, settle, escalate, or communicate final decisions. |
| Does IIP detect fraud? | IIP flags risk signals and suspicious patterns for human investigation. It should not accuse customers of fraud or make final fraud determinations. |
| Why start with motor claims? | Motor claims provide a practical MVP because they involve repeatable workflows, common document types, customer updates, and clear operational metrics. |
| Why WhatsApp-first? | Many customer interactions already happen through mobile messaging. WhatsApp-first intake can reduce friction while feeding structured internal workflows. |
| Can we use email or web forms too? | Yes. WhatsApp is a primary channel, but email, web, SMS, call center, broker, and Teams workflows can be added or integrated. |
| Can customers log into IIP? | The current scope is an internal staff workspace. Customers interact through external channels, not the internal app. |

## Security and Compliance FAQ

| Question | Recommended answer |
|---|---|
| Where is data hosted? | The target architecture is Azure-first. Final region and tenancy must be confirmed per client requirements. |
| Can IIP run in our tenant? | Yes, the preferred enterprise path is Azure Managed Application/private deployment into the customer tenant, subject to packaging and commercial readiness. |
| How are secrets handled? | Secrets should be stored in Azure Key Vault and accessed through managed identities where possible. They should not be hardcoded. |
| How is access controlled? | IIP uses role-aware UI and server-side authorization. Frontend hiding alone is not security. |
| Is every AI action logged? | AI interactions, tool calls, generated outputs, human approvals, overrides, and source references should be logged for auditability. |
| What happens if an AI output is wrong? | Users can reject or correct AI output. The correction should be logged and used in quality review. |

## Operations FAQ

| Question | Recommended answer |
|---|---|
| What if a document is blurry? | IIP should mark low confidence, route it for human review, and allow staff to request a replacement document. |
| What if a provider is not configured? | The UI should show a clear Not configured state instead of fake behavior. |
| What if WhatsApp is down? | IIP should use queueing, retry, and resilient channel patterns where configured. Local or hybrid buffering is an architecture option that requires confirmation. |
| Can staff override AI? | Yes. Human review and correction are core requirements. |
| How do managers see backlog? | The Command Center shows claims, incomplete files, SLA risk, pending approvals, escalations, customer update backlog, and trend cards. |

## Commercial FAQ

| Question | Recommended answer |
|---|---|
| How is IIP priced? | Pricing can include platform subscription, users, AI usage, document processing, channels, implementation, support, and deployment model. Final pricing requires commercial approval. |
| Is there a pilot option? | Yes. A controlled motor claims and document intelligence pilot is recommended before wider rollout. |
| Can pilot fees be credited to production? | This is a commercial decision and should be marked Requires Confirmation. |
| What determines implementation cost? | Scope, deployment model, integrations, data readiness, security review, training, and custom workflows. |

## Objection Handling

| Objection | Response |
|---|---|
| We already have a claims system. | IIP is not trying to replace it. It improves intake, documents, visibility, AI assistance, and workflow around the system. |
| AI is risky in insurance. | Correct, which is why IIP uses human review, prohibited-action guardrails, citations, audit logs, and quality testing. |
| Integrations will take too long. | The pilot can begin with controlled intake, upload, exports, and read-only lookup before deeper write-back integrations. |
| Our data is messy. | IIP is designed for messy operational inputs, but value depends on data readiness, document templates, and human review. |
| Staff may not adopt it. | The pilot includes role-based training, UAT, weekly support, and workflow design around real staff tasks. |
| We need executive visibility. | Command Center and reporting are designed specifically for visibility across claims, SLA risk, backlog, approvals, escalations, and trends. |

## Words to Avoid

| Avoid saying | Say instead |
|---|---|
| AI approves claims | AI assists claims staff; humans approve claims |
| Fraud detected | Risk signal flagged for review |
| Fully automated insurance decisions | Human-reviewed decision support |
| Replaces your systems | Integrates with and sits above existing systems |
| Guaranteed ROI | Illustrative ROI subject to baseline validation |

## Open Questions and Required Confirmations

| Item | Status | Notes |
|---|---|---|
| First pilot insurer | Requires Confirmation | ICEA-like mid-to-large insurer is the working assumption, but the named pilot client must be confirmed. |
| Commercial currency | Requires Confirmation | Pricing can be expressed in USD for investor/client modeling, but local contract currency may vary. |
| Final pilot duration | Requires Confirmation | An 8 to 12 week pilot is assumed unless client procurement or integration timelines require adjustment. |
| Production deployment model | Requires Confirmation | Azure Managed Application is preferred for enterprise clients; Otic-hosted SaaS may be used for demos or smaller clients. |
| Regulatory sign-off requirements | Requires Confirmation | Local insurance, privacy, outsourcing, and client legal requirements must be validated before production. |

