---
Product: Insurance Intelligence Platform (IIP)
Document Name: Infrastructure-as-Code Architecture Guide
Version: 1.0
Status: Draft
Audience: Cloud Engineering, DevOps, Security, Technical Leadership, Implementation Agents
Date: 2026-06-30
---

# Infrastructure-as-Code Architecture Guide

## Common IIP Context Used

The Insurance Intelligence Platform (IIP) is an Azure-native, human-reviewed insurance operations platform for African insurers and related insurance organizations. It acts as an intelligent operational layer across staff, customers, core insurance systems, documents, channels, knowledge sources, and AI agents. The current MVP direction is Motor Claims & Document Intelligence for a mid-to-large insurer pilot.

Confirmed implementation direction:

- Frontend: Next.js, React, TypeScript, Tailwind CSS.
- Backend: .NET 8 / ASP.NET Core, EF Core, Azure SQL.
- Azure services: Azure SQL, Blob Storage, Key Vault, Azure AI Search, Azure OpenAI, Azure AI Document Intelligence, Service Bus or Event Grid, Functions or Durable Functions, API Management, Application Insights and Log Analytics.
- Identity: Microsoft Entra ID / MSAL / OAuth 2.0.
- Primary internal modules: Command Center, Claims, Documents, Channels, Customers, Sales & Leads, Knowledge Base, Approvals, Compliance & Audit, Reports & Analytics, Integrations, Admin Settings, Support & Health.
- Customer entry points: WhatsApp-first, with web chat, SMS, email, broker, call center, Teams, and website forms as planned or future channels.
- Governance rule: AI prepares insurance work; humans make consequential decisions.

Non-negotiable AI boundary:

- AI may summarize, classify, extract, flag, draft, route, and recommend.
- AI must not approve or reject claims, bind policies, set final premiums, accuse customers of fraud, release payments, promise settlement, or make final regulated decisions.

## Purpose

This document defines the target Infrastructure-as-Code architecture for IIP. It is intended to guide Bicep or Terraform implementation for development, staging, UAT, pilot, and production environments.

## IaC Principles

| Principle | Requirement |
|---|---|
| Azure-first | All infrastructure definitions should target Azure services already selected for IIP. |
| Repeatable environments | Dev, staging, UAT, pilot, and production should be reproducible from source-controlled IaC. |
| No secrets in code | Secrets must be stored in Key Vault or secure pipeline variables. |
| Managed identity first | Services should use managed identities where possible. |
| Least privilege | Each app component should receive only the permissions needed. |
| Observable by default | Monitoring, logs, diagnostics, and alerts must be created with the environment. |

## Recommended Repository Structure

```text
infra/
  bicep/ or terraform/
    modules/
      app-hosting/
      api-management/
      azure-sql/
      blob-storage/
      key-vault/
      ai-search/
      azure-openai/
      document-intelligence/
      service-bus/
      application-insights/
      networking/
      managed-identities/
    environments/
      dev/
      staging/
      uat/
      pilot/
      prod/
    parameters/
      dev.parameters.json
      staging.parameters.json
      uat.parameters.json
      pilot.parameters.json
      prod.parameters.json
```

## Resource Naming Convention

| Resource Type | Pattern | Example |
|---|---|---|
| Resource group | `rg-iip-{env}-{region}-{suffix}` | `rg-iip-pilot-eus-001` |
| API app | `app-iip-api-{env}-{suffix}` | `app-iip-api-pilot-001` |
| Frontend app | `app-iip-web-{env}-{suffix}` | `app-iip-web-pilot-001` |
| SQL server | `sql-iip-{env}-{suffix}` | `sql-iip-pilot-001` |
| SQL database | `sqldb-iip-{env}` | `sqldb-iip-pilot` |
| Storage | `stiip{env}{suffix}` | `stiippilot001` |
| Key Vault | `kv-iip-{env}-{suffix}` | `kv-iip-pilot-001` |
| Service Bus | `sb-iip-{env}-{suffix}` | `sb-iip-pilot-001` |
| AI Search | `srch-iip-{env}-{suffix}` | `srch-iip-pilot-001` |

## Environment Separation

| Environment | Purpose | Data Type | Deployment Control |
|---|---|---|---|
| Dev | Engineering iteration. | Synthetic data only. | Frequent automated deployments. |
| Staging | Integration validation. | Synthetic or approved test data. | Controlled deployments after tests. |
| UAT | Client acceptance. | Client-approved test data. | Change-controlled deployments. |
| Pilot | Controlled live/pilot operation. | Client-approved pilot data. | Release-gated deployments. |
| Production | Full live operation. | Production data. | Strict release and rollback controls. |

## Core Azure Resources

| Capability | Target Azure Resource |
|---|---|
| Web app hosting | Azure Container Apps, App Service, or equivalent agreed hosting. |
| Backend API | .NET 8 service hosted in Azure Container Apps/App Service. |
| Database | Azure SQL. |
| Object storage | Azure Blob Storage. |
| Secrets | Azure Key Vault. |
| Search/RAG | Azure AI Search. |
| Language model | Azure OpenAI. |
| OCR/extraction | Azure AI Document Intelligence. |
| Async processing | Azure Service Bus/Event Grid. |
| Background workflows | Azure Functions/Durable Functions. |
| Monitoring | Application Insights and Log Analytics. |
| API boundary | Azure API Management where required. |

## Deployment Order

1. Resource group and shared diagnostics.
2. Managed identities.
3. Key Vault and access policies/RBAC.
4. Storage, SQL, Service Bus, AI Search.
5. AI services and provider configuration.
6. App hosting resources.
7. Application settings and references to Key Vault.
8. Database migration execution.
9. Monitoring, alerts, and dashboards.
10. Post-deployment validation.

## Security Baseline

- Use HTTPS-only endpoints.
- Use private endpoints where required by client/security review.
- Disable public access to storage where possible.
- Store connection strings and provider keys in Key Vault.
- Use managed identities for app-to-resource access.
- Enable diagnostic logs on critical resources.
- Enforce tenant/workspace context in application layer and database access patterns.

## Managed Application Considerations

For Managed Application deployments, IaC must separate publisher-managed package logic from customer-owned resource groups. Support access must be explicit, auditable, least-privilege, and aligned with client contracts.

## SaaS Considerations

For SaaS deployment, IaC must support multi-tenant provisioning, shared services, tenant-isolated data patterns, and repeatable onboarding of new tenants and workspaces.

## IaC Acceptance Criteria

- A fresh environment can be deployed from source-controlled parameters.
- No secrets are committed to source control.
- Application can retrieve configuration securely.
- Logs and health checks are active immediately after deployment.
- Deployment can be repeated without destructive side effects.
- Environment naming and tagging support cost tracking and support triage.


## Open Questions

| ID | Open Question | Owner | Impact if Unresolved |
|---|---|---|---|
| OQ-01 | Which insurer will be the first signed pilot customer and what exact internal processes will be included? | Product / Commercial | Final configuration, training, and pilot timelines may change. |
| OQ-02 | Which claims/core system will be integrated first, and will MVP require read-only lookup, export, or direct write-back? | Product / Engineering / Client IT | Impacts integration complexity and deployment schedule. |
| OQ-03 | Which Azure deployment path is selected first: customer-tenant Managed Application or Otic-hosted SaaS pilot? | Leadership / Engineering | Impacts tenancy, support access, IaC, billing, and security review. |
| OQ-04 | Which WhatsApp Business provider will be used for the first pilot? | Product / Integrations | Impacts webhook schemas, retry behavior, and channel onboarding. |
| OQ-05 | What client-specific SLAs, roles, branches, claim categories, and document checklists must be configured? | Client Operations | Impacts workflow rules and acceptance testing. |
| OQ-06 | What final regulatory, legal, and data residency requirements must be signed off for the first pilot? | Legal / Compliance | Impacts production readiness and contractual commitments. |
