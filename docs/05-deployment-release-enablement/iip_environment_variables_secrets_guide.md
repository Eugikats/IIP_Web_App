# Environment Variables & Secrets Guide

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Environment Variables & Secrets Guide |
| Version | 1.0 |
| Status | Draft |
| Audience | Developers, DevOps engineers, security leads, support engineers, coding agents |
| Date | 2026-06-30 |


## 1. Purpose

This guide lists required configuration keys without exposing real secrets, including local, staging, pilot, and production environment variables.


This document uses the existing IIP project context and prior documentation pack: Master Product Context, PRD, FSD, Technical Architecture Document, System Integration Strategy, UI/UX Handoff, API Specification, Data Model, Security & Compliance Document, QA/Test Plan, Deployment Plan, User/Admin Guide, Implementation Roadmap, and Strategy/Requirements/Roadmap Pack.

Confirmed project baseline:
- Official product name: Insurance Intelligence Platform (IIP).
- IIP is an Azure-native, human-reviewed insurance operations platform, not a replacement for core insurance systems.
- MVP focus: motor claims and document intelligence for a mid-to-large insurer pilot.
- Customer engagement is WhatsApp-first and channel-led; customers do not log into the internal staff application.
- Internal modules include Command Center, Channels, Customers, Sales & Leads, Claims, Documents, Knowledge Base, Approvals, Compliance & Audit, Reports & Analytics, Integrations, Admin Settings, and Support & Health.
- The internal UI is a role-aware staff workspace with persistent navigation, top bar, global search, operational states, and contextual Copilot panel.
- The platform uses Next.js, React, TypeScript, and Tailwind on the frontend and .NET 8 / ASP.NET Core on the backend.
- Azure services include Azure SQL, Blob Storage, Key Vault, Azure AI Search, Azure OpenAI, Azure AI Document Intelligence, Service Bus/Event Grid, Functions/Durable Functions, API Management, Application Insights/Log Analytics, Entra ID/MSAL, and optional Managed Application packaging.
- AI assists with extraction, summarization, classification, routing, drafting, risk signals, and evidence preparation; humans make regulated or consequential decisions.
- Staging and production must not use fake operational data. Empty states, error states, and Not configured states must be real and explicit.


## Secret Handling Rules

- Never commit real secrets.
- Use Key Vault or secure pipeline variables for staging, pilot, and production.
- Do not print secrets in logs, error messages, screenshots, or AI prompts.
- Store secret references, not secret values, in database configuration where possible.
- Rotate secrets after accidental exposure.

## Backend Configuration Keys

| Key | Purpose | Secret? | Notes |
| --- | --- | --- | --- |
| ASPNETCORE_ENVIRONMENT | Runtime environment | No | Development, Staging, UAT, Pilot, Production. |
| ConnectionStrings__IipSql | Azure SQL connection | Yes | Use managed identity where possible. |
| Azure__TenantId | Azure/Entra tenant | No/Config | Required for identity integration. |
| Azure__KeyVaultUri | Key Vault reference | No | Secrets resolved from Key Vault. |
| Azure__StorageAccountName | Blob storage account | No | Use managed identity. |
| Azure__ServiceBusNamespace | Queue namespace | No | For async workloads. |
| ApplicationInsights__ConnectionString | Telemetry | Yes/Config | Protect if considered sensitive. |
| Auth__Authority | MSAL authority | No | Requires environment-specific value. |
| Auth__Audience | API audience | No | Requires app registration. |
| Iip__RequireRealAuth | Require production auth | No | Must be true outside local/demo. |

## AI and Document Intelligence Keys

| Key | Purpose | Secret? | Notes |
| --- | --- | --- | --- |
| AzureOpenAI__Endpoint | Azure OpenAI endpoint | No/Config | Provider health should validate. |
| AzureOpenAI__ApiKey | Azure OpenAI key if not using identity | Yes | Prefer managed identity where supported. |
| AzureOpenAI__DeploymentName | Model deployment | No | Version control with prompt registry. |
| DocumentIntelligence__Endpoint | OCR/extraction endpoint | No/Config | Provider health required. |
| DocumentIntelligence__ApiKey | Document Intelligence key if used | Yes | Prefer managed identity where supported. |
| Search__Endpoint | Azure AI Search endpoint | No/Config | Tenant-filtered indexes. |
| Search__ApiKey | Search admin/query key if used | Yes | Protect and rotate. |

## Integration Configuration Keys

| Key | Purpose | Secret? | Notes |
| --- | --- | --- | --- |
| WhatsApp__Provider | WhatsApp provider/BSP | No | Requires Confirmation. |
| WhatsApp__WebhookSecret | Webhook validation | Yes | Use Key Vault. |
| WhatsApp__AccessToken | Outbound API token | Yes | Use Key Vault. |
| Graph__ClientId | Microsoft Graph app id | No | Environment-specific. |
| Graph__ClientSecret | Graph app secret if used | Yes | Prefer certificate/managed identity where possible. |
| Graph__TenantId | Graph tenant | No | Client or Otic tenant depending deployment. |
| PolicySystem__BaseUrl | Read-only policy lookup | No/Config | Optional for MVP. |
| PolicySystem__ApiKey | Policy system credential | Yes | Optional; protect strongly. |

## Frontend Configuration Keys

| Key | Purpose | Secret? | Notes |
| --- | --- | --- | --- |
| NEXT_PUBLIC_API_BASE_URL | Backend base URL | No | Public value. |
| NEXT_PUBLIC_ENVIRONMENT_NAME | Environment label | No | Show in top bar. |
| NEXT_PUBLIC_AUTH_CLIENT_ID | MSAL client id | No | Public app registration id. |
| NEXT_PUBLIC_AUTH_AUTHORITY | MSAL authority | No | Public config. |
| NEXT_PUBLIC_ENABLE_DEMO_MODE | Local/demo flag | No | Must be false in production. |

## Environment Matrix

| Environment | Secret Source | Demo Headers | Fake Data | Required Controls |
| --- | --- | --- | --- | --- |
| Local | local user secrets/.env | Allowed | Allowed if labeled | No committed secrets. |
| Demo | secure env store | Controlled | Allowed if labeled | Environment label visible. |
| Staging | Key Vault/pipeline | No production bypass | Only controlled UAT data | Real auth preferred. |
| Pilot | Key Vault/pipeline | No | No fake operational data | Audit, monitoring, support enabled. |
| Production | Key Vault/managed identity | No | No | Full security controls. |

## Acceptance Criteria

| ID | Acceptance Criterion | Evidence |
| --- | --- | --- |
| AC-ENV-001 | All required keys are documented with secret/non-secret classification. | Config review. |
| AC-ENV-002 | Production does not rely on demo headers or fake data flags. | Deployment validation. |
| AC-ENV-003 | Secrets are sourced from secure stores outside local development. | Security review. |

## Open Questions and Required Confirmations

| ID | Open Question / Confirmation Needed | Impact |
| --- | --- | --- |
| OQ-ENV-001 | What exact environment names will be used in pipelines? | Affects naming and scripts. |
| OQ-ENV-002 | Which secrets can use managed identity instead of API keys? | Affects security hardening. |
| OQ-ENV-003 | What WhatsApp and policy-system provider keys are needed? | Affects integration readiness. |
