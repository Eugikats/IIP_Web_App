# Azure Managed Application Packaging Guide

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Azure Managed Application Packaging Guide |
| Version | 1.0 |
| Status | Draft |
| Audience | Product, Engineering, DevOps, Azure Marketplace Owner, Security, Client IT |
| Date | 2026-06-30 |


## 1. Purpose

This guide defines the packaging readiness path for delivering IIP as an Azure Managed Application or private Marketplace offer for insurers that require customer-tenant deployment, clearer data boundaries, and enterprise procurement controls.

This document is part of the deployment, release, and client enablement documentation set for Insurance Intelligence Platform (IIP). IIP is an Azure-first, human-reviewed insurance operations platform for claims, documents, channels, customer operations, approvals, auditability, and AI-assisted decision support. Customers interact through external channels such as WhatsApp, email, web forms, SMS, and broker/call-center routes; internal insurer staff use the IIP staff workspace. The MVP is centered on motor claims and document intelligence, with AI assisting through summarization, extraction, classification, routing, and evidence preparation while humans retain authority over consequential insurance decisions.

The expected technical foundation is Next.js/React/TypeScript/Tailwind on the frontend, .NET 8/ASP.NET Core on the backend, Azure SQL, Azure Blob Storage, Azure Key Vault, Azure AI Search, Azure OpenAI, Azure AI Document Intelligence, Azure Service Bus/Event Grid, Azure Functions or Durable Functions, Application Insights/Log Analytics, Microsoft Entra ID/MSAL, and Microsoft Graph where configured. Deployment options include an Otic-hosted SaaS path and a customer-tenant or Azure Managed Application path. Production and pilot environments must not rely on fake endpoints or mock production data; unconfigured providers must show clear Not configured states.

## 2. Packaging Objectives

| Objective | Description |
|---|---|
| Customer tenant deployment | Deploy IIP resources into the client-controlled Azure subscription or resource group where required. |
| Controlled publisher access | Define what Otic can access for support and what remains client-owned. |
| Repeatable deployment | Use templates and scripts instead of manual resource creation. |
| Secure defaults | Enforce Key Vault, managed identities, private storage, diagnostic settings, and role boundaries. |
| Marketplace readiness | Prepare the artifacts needed for private offer validation when commercially ready. |

## 3. Candidate Managed Application Components

| Component | Include in Package? | Notes |
|---|---|---|
| Backend service | Yes | .NET 8 API hosting. |
| Frontend app | Yes or separate | Depends on hosting topology. |
| Azure SQL | Yes | Customer-tenant data store if Managed App path is selected. |
| Blob Storage | Yes | Documents and claim evidence. |
| Key Vault | Yes | Secrets and certificates. |
| AI Search | Yes | Knowledge and retrieval indexes. |
| Service Bus/Event Grid | Yes | Async integration events. |
| Application Insights/Log Analytics | Yes | Diagnostics and operational visibility. |
| Azure OpenAI | Requires confirmation | Availability, quota, region, and customer policy may vary. |
| Document Intelligence | Requires confirmation | Region and model provisioning may vary. |
| WhatsApp provider | Usually external config | Provider account ownership must be confirmed. |

## 4. Required Package Artifacts

| Artifact | Purpose | Status |
|---|---|---|
| ARM/Bicep templates | Deploy Azure resources | Required |
| Create UI definition | Collect deployment parameters | Required for Marketplace-style deployment |
| Parameter files | Environment-specific values | Required |
| Role definition plan | Define publisher/client access | Required |
| Deployment scripts | Post-provision setup/migrations | Required |
| Security documentation | Describe controls and boundaries | Required |
| Support documentation | Describe support model and escalation | Required |
| Pricing/private offer terms | Commercial packaging | Requires Confirmation |
| Legal/privacy terms | Client-facing contractual terms | Requires Confirmation |

## 5. Parameter Checklist

- Tenant name and workspace name.
- Azure region and resource group.
- Entra ID tenant and app registration details.
- Custom domain and redirect URI settings if used.
- SQL admin/identity settings.
- Storage retention and lifecycle policy.
- AI provider deployment names and quotas.
- Document Intelligence model configuration.
- Support access and managed identity settings.
- Diagnostic workspace and alert routing.

## 6. Security Boundary Decisions

| Decision Area | Default Direction | Requires Confirmation |
|---|---|---|
| Data ownership | Client owns data in customer tenant | Yes, by contract |
| Publisher access | Least privilege support access | Yes |
| Logs | Operational logs should not leak sensitive document data | Yes |
| Secrets | Stored in Key Vault | No |
| External channels | Provider credentials handled by agreed owner | Yes |
| AI service location | Must match customer policy and Azure availability | Yes |

## 7. Packaging Readiness Checklist

| Check | Status |
|---|---|
| IaC templates compile and deploy cleanly |  |
| Deployment parameters documented |  |
| Managed identities configured |  |
| Key Vault integration verified |  |
| SQL migrations automated or documented |  |
| Provider Not configured states verified |  |
| Diagnostics enabled by default |  |
| Role assignments least-privilege reviewed |  |
| Client uninstall/data retention process defined |  |
| Support access approved by client/legal |  |

## 8. Open Questions

| Question | Owner |
|---|---|
| Will the first pilot require Managed Application packaging or Otic-hosted SaaS? | Product / Client Sponsor |
| Which Azure region will be used for client data and AI services? | Client IT / Security |
| What level of publisher support access will clients allow? | Legal / Client IT |
| Will Azure Marketplace private offer be required before first pilot or after pilot? | Commercial Lead |


## Assumptions and Open Questions

| Type | Item | Action Required |
|---|---|---|
| Assumption | The pilot MVP is focused on motor claims, document intelligence, internal staff workflows, and human-reviewed AI support. | Confirm during pilot kickoff. |
| Assumption | Azure is the preferred hosting foundation, with SaaS and Managed Application deployment paths both preserved until commercial packaging is finalized. | Confirm per client. |
| Open Question | Final Azure region, private networking requirements, and customer data residency constraints may vary by client. | Confirm with the insurer and implementation team. |
| Open Question | Exact client support contacts, escalation contacts, and approval signatories are not yet fixed. | Capture in the Client Configuration Workbook or pilot kickoff pack. |
| Requires Confirmation | Any pricing, SLA, or regulatory commitment must be confirmed by business, legal, security, and delivery leads before client use. | Review before external distribution. |

