# Environment Setup Guide

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Environment Setup Guide |
| Version | 1.0 |
| Status | Draft |
| Audience | Engineering, DevOps, Security, Client IT, Support |
| Date | 2026-06-30 |


## 1. Purpose

This guide defines the expected environments for building, testing, piloting, and operating IIP. It clarifies environment intent, ownership, configuration, data rules, secrets handling, and promotion flow.

This document is part of the deployment, release, and client enablement documentation set for Insurance Intelligence Platform (IIP). IIP is an Azure-first, human-reviewed insurance operations platform for claims, documents, channels, customer operations, approvals, auditability, and AI-assisted decision support. Customers interact through external channels such as WhatsApp, email, web forms, SMS, and broker/call-center routes; internal insurer staff use the IIP staff workspace. The MVP is centered on motor claims and document intelligence, with AI assisting through summarization, extraction, classification, routing, and evidence preparation while humans retain authority over consequential insurance decisions.

The expected technical foundation is Next.js/React/TypeScript/Tailwind on the frontend, .NET 8/ASP.NET Core on the backend, Azure SQL, Azure Blob Storage, Azure Key Vault, Azure AI Search, Azure OpenAI, Azure AI Document Intelligence, Azure Service Bus/Event Grid, Azure Functions or Durable Functions, Application Insights/Log Analytics, Microsoft Entra ID/MSAL, and Microsoft Graph where configured. Deployment options include an Otic-hosted SaaS path and a customer-tenant or Azure Managed Application path. Production and pilot environments must not rely on fake endpoints or mock production data; unconfigured providers must show clear Not configured states.

## 2. Environment Model

| Environment | Purpose | Data Type | Access |
|---|---|---|---|
| Local Development | Developer and AI-agent assisted coding | Synthetic or developer seed data only | Developers |
| Dev / Integration | Shared integration and build verification | Synthetic data | Engineering and QA |
| Staging | Production-like validation | Synthetic or approved masked data | Engineering, QA, Product, Security |
| UAT | Client acceptance testing | Client-approved test data or masked data | Client testers and delivery team |
| Pilot | Controlled live pilot workflow | Client-approved pilot data | Pilot users and support team |
| Production | Full operational use | Production data | Authorized client/Otic support according to contract |

## 3. Azure Resource Baseline

| Resource | Purpose | Notes |
|---|---|---|
| Azure Container Apps / App Service | Host .NET backend and/or frontend services | Deployment target depends on architecture selection. |
| Azure SQL | Transactional tenant, claim, document, workflow, audit, and configuration data | Tenant isolation rules required. |
| Azure Blob Storage | Store documents, exports, claim packs, and evidence files | Use private access and SAS carefully. |
| Azure Key Vault | Store secrets and certificates | Prefer managed identity over raw secrets. |
| Azure AI Search | Knowledge retrieval and document search | Index approval and re-indexing rules required. |
| Azure OpenAI | AI assistance | Must enforce human-review boundaries. |
| Azure AI Document Intelligence | OCR/classification/extraction | Low-confidence fields require review. |
| Service Bus / Event Grid | Async processing and integration events | Use idempotency and retry policies. |
| Application Insights / Log Analytics | Observability | Correlation IDs required. |
| Entra ID / MSAL | Authentication and identity | Avoid frontend-only authorization. |

## 4. Environment Configuration Checklist

| Area | Required Configuration |
|---|---|
| Tenant context | Tenant ID, workspace ID, allowed domains, support contacts |
| Identity | Entra app registration, redirect URIs, scopes, group/role mapping |
| API | Base URL, CORS policy, API version, health endpoints |
| Storage | Blob containers, retention policy, access policy, lifecycle policy |
| Database | Connection string via Key Vault, migration baseline, backup policy |
| AI | Azure OpenAI deployment name, content filters, tool registry, logging policy |
| Search | Index names, data source, indexer schedule, schema version |
| Documents | Document Intelligence endpoint, models, confidence thresholds |
| Channels | WhatsApp/email/SMS/provider credentials if configured |
| Monitoring | App Insights connection, alert rules, dashboards, health checks |

## 5. Promotion Flow

| From | To | Required Gate |
|---|---|---|
| Local | Dev | Build and unit checks pass |
| Dev | Staging | Integration tests and migrations pass |
| Staging | UAT | QA, security, and AI safety gates pass |
| UAT | Pilot | Client UAT sign-off and deployment approval |
| Pilot | Production | Pilot closeout or executive release approval |

## 6. Data Rules

- Local and dev must use synthetic or approved seed data only.
- UAT must use either synthetic, masked, or formally approved test data.
- Production data must not be copied into local machines.
- Document samples used for AI or OCR testing must be approved and tracked.
- Any data export must be audited and access-controlled.

## 7. Secrets Rules

- Store secrets in Azure Key Vault, not source code or `.env` committed files.
- Prefer managed identities for Azure resource access.
- Rotate credentials after pilot setup, after staff changes, and after incidents.
- Do not expose provider keys in browser code or logs.
- Redact secrets from screenshots, logs, and support tickets.

## 8. Environment Readiness Checklist

| Check | Dev | Staging | UAT | Pilot | Production |
|---|---|---|---|---|---|
| Health endpoints pass |  |  |  |  |  |
| Auth configured |  |  |  |  |  |
| Tenant/workspace configured |  |  |  |  |  |
| Database migrated |  |  |  |  |  |
| Blob storage private |  |  |  |  |  |
| AI provider configured or Not configured shown |  |  |  |  |  |
| Document Intelligence configured or Not configured shown |  |  |  |  |  |
| Monitoring and alerts enabled |  |  |  |  |  |
| Support contacts assigned |  |  |  |  |  |


## Assumptions and Open Questions

| Type | Item | Action Required |
|---|---|---|
| Assumption | The pilot MVP is focused on motor claims, document intelligence, internal staff workflows, and human-reviewed AI support. | Confirm during pilot kickoff. |
| Assumption | Azure is the preferred hosting foundation, with SaaS and Managed Application deployment paths both preserved until commercial packaging is finalized. | Confirm per client. |
| Open Question | Final Azure region, private networking requirements, and customer data residency constraints may vary by client. | Confirm with the insurer and implementation team. |
| Open Question | Exact client support contacts, escalation contacts, and approval signatories are not yet fixed. | Capture in the Client Configuration Workbook or pilot kickoff pack. |
| Requires Confirmation | Any pricing, SLA, or regulatory commitment must be confirmed by business, legal, security, and delivery leads before client use. | Review before external distribution. |

