# Pricing & Packaging Document

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Pricing & Packaging Document |
| Version | 1.0 |
| Status | Draft |
| Audience | Commercial leadership, sales, finance, executives, client procurement |
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

This document defines a pricing and packaging structure for IIP that can support pilots, enterprise deployments, Azure Managed Application deployments, and Otic-hosted SaaS deployments. All prices are placeholders until commercially approved.

## Packaging Principles

- Keep the first pilot simple and focused on motor claims and document intelligence.
- Separate platform access, implementation services, support, usage, and optional modules.
- Price enterprise deployments differently from lightweight SaaS/demo deployments because Managed Application deployments carry additional setup, security, support, and governance work.
- Do not price AI as unlimited if provider costs are usage-based.
- Keep human-in-loop and auditability as core product value, not optional compliance add-ons.

## Recommended Package Structure

| Package | Target client | Description |
|---|---|---|
| Pilot Package | First insurer pilot | Controlled pilot for motor claims, documents, dashboards, AI assistance, training, and weekly support |
| Core Claims Intelligence | Insurer operations team | Claims inbox, case review, document intelligence, command center, audit trail, user/admin setup |
| Customer Acquisition Add-on | Distribution and digital teams | WhatsApp-first lead capture, quote handoff, customer channels, follow-up backlog |
| Fraud Signal Add-on | Claims/risk teams | Risk signals, duplicate/mismatch flags, suspicious pattern queues, investigation support |
| Early Warning Add-on | Executives and operations | Trend alerts, SLA risk, claim volume shifts, operational bottleneck signals |
| Underwriting Support Add-on | Underwriting teams | Policy/quote support, document review, risk notes, human-reviewed recommendations |
| Enterprise Managed Application | Large regulated clients | Customer-tenant deployment, Azure Managed Application packaging, support access controls, premium governance |

## Pricing Components

| Component | Description | Pricing basis | Status |
|---|---|---|---|
| Platform subscription | Access to core IIP workspaces and dashboards | Monthly or annual | Requires Confirmation |
| User seats | Staff users by role or bundle | Per user/month or tier bundle | Requires Confirmation |
| AI usage | LLM calls, summaries, RAG, evaluation, red-team monitoring | Usage-based or included allowance | Requires Confirmation |
| Document processing | OCR/classification/extraction | Per document/page or included allowance | Requires Confirmation |
| WhatsApp/channel usage | Customer messages and channel fees | Usage-based | Requires Confirmation |
| Implementation services | Setup, configuration, integration, training, UAT | One-time fee | Requires Confirmation |
| Managed Application premium | Extra security, packaging, client tenant deployment, support model | One-time and/or annual premium | Requires Confirmation |
| Support tier | Support response targets and coverage | Monthly or annual | Requires Confirmation |

## Illustrative Package Matrix

The following is a commercial structure, not approved pricing.

| Tier | Intended use | Included modules | Notes |
|---|---|---|---|
| Pilot | 8-12 week validation | Motor claims, document intelligence, command center, limited users, support | Best for first insurer proof of value |
| Standard | Single business unit | Core claims intelligence, documents, audit, reports, admin | Suitable for claims department rollout |
| Professional | Multi-team operations | Standard plus channels, customer workspace, sales/leads, advanced reporting | Suitable for insurer-wide operations |
| Enterprise | Regulated enterprise | Professional plus Managed Application deployment, premium support, custom integrations | Suitable for large insurers and strict security environments |

## Module Definitions

| Module | Included capabilities | Commercial note |
|---|---|---|
| Core Staff App | Navigation, top bar, global search, role-aware workspace, status states | Core |
| Command Center | Claims metrics, trends, SLA risk, approvals, escalations, AI usage | Core |
| Claims | Claims inbox, filters, case review, summaries, approval routing | Core |
| Documents | Upload, classification, extraction, completeness, review, evidence | Core for MVP |
| Channels | WhatsApp/email/web intake, backlog, customer updates | Add-on or Professional+ |
| Customers | Customer timeline, policies, documents, interactions | Add-on or Professional+ |
| Sales & Leads | Lead capture, quote handoff, follow-up backlog | Add-on |
| AI/RAG Knowledge | Approved knowledge base, citations, Q&A, safe responses | Core with usage controls |
| Fraud Signals | Duplicate/mismatch/pattern flags, investigation support | Add-on |
| Early Warning | Trend/risk/volume alerts and operational forecasting | Add-on |

## Pilot Pricing Model Template

| Item | Placeholder | Notes |
|---|---|---|
| Pilot setup fee | Requires Confirmation | Covers configuration, training, UAT, pilot management |
| Pilot subscription | Requires Confirmation | Could be discounted or credited into annual contract |
| Included users | Requires Confirmation | Suggested: claims officers, supervisors, admin, compliance, executive viewers |
| Included documents/pages | Requires Confirmation | Should match pilot volume |
| Included AI usage | Requires Confirmation | Must include safe limits and monitoring |
| Support coverage | Requires Confirmation | Pilot support hours and escalation targets must be agreed |
| Optional integrations | Requires Confirmation | Deeper core system integration may be priced separately |

## Annual Contract Model Template

| Commercial element | Recommended approach |
|---|---|
| Annual platform fee | Based on tier and deployment model |
| Usage allowances | AI usage, document pages, messages, storage |
| Overage fees | Defined per usage category |
| Implementation services | One-time project fee by integration complexity |
| Support | Tiered support levels by coverage and response targets |
| Professional services | Change requests, new modules, custom workflows, custom reports |
| Managed Application | Premium for private deployment, marketplace packaging, and support operations |

## Discount and Commercial Control Rules

- Pilot discounts should be time-limited and tied to a next-phase decision.
- Do not include unlimited AI/document processing unless cost exposure is capped.
- Deep core system integrations should be scoped and priced separately.
- Any client-specific compliance or hosting requirement should be assessed before pricing.
- Pricing should align with measured value, not only software seats.

## Open Questions and Required Confirmations

| Item | Status | Notes |
|---|---|---|
| First pilot insurer | Requires Confirmation | ICEA-like mid-to-large insurer is the working assumption, but the named pilot client must be confirmed. |
| Commercial currency | Requires Confirmation | Pricing can be expressed in USD for investor/client modeling, but local contract currency may vary. |
| Final pilot duration | Requires Confirmation | An 8 to 12 week pilot is assumed unless client procurement or integration timelines require adjustment. |
| Production deployment model | Requires Confirmation | Azure Managed Application is preferred for enterprise clients; Otic-hosted SaaS may be used for demos or smaller clients. |
| Regulatory sign-off requirements | Requires Confirmation | Local insurance, privacy, outsourcing, and client legal requirements must be validated before production. |

