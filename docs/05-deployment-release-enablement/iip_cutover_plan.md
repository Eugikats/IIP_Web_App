# Cutover Plan

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Cutover Plan |
| Version | 1.0 |
| Status | Draft |
| Audience | Product, Client Operations, Client IT, Engineering, QA, Support, Client Success |
| Date | 2026-06-30 |


## 1. Purpose

The Cutover Plan defines how a client moves from current manual or legacy workflows into IIP-assisted operations for pilot or production. It covers readiness, communications, freeze windows, configuration, validation, support, and fallback.

This document is part of the deployment, release, and client enablement documentation set for Insurance Intelligence Platform (IIP). IIP is an Azure-first, human-reviewed insurance operations platform for claims, documents, channels, customer operations, approvals, auditability, and AI-assisted decision support. Customers interact through external channels such as WhatsApp, email, web forms, SMS, and broker/call-center routes; internal insurer staff use the IIP staff workspace. The MVP is centered on motor claims and document intelligence, with AI assisting through summarization, extraction, classification, routing, and evidence preparation while humans retain authority over consequential insurance decisions.

The expected technical foundation is Next.js/React/TypeScript/Tailwind on the frontend, .NET 8/ASP.NET Core on the backend, Azure SQL, Azure Blob Storage, Azure Key Vault, Azure AI Search, Azure OpenAI, Azure AI Document Intelligence, Azure Service Bus/Event Grid, Azure Functions or Durable Functions, Application Insights/Log Analytics, Microsoft Entra ID/MSAL, and Microsoft Graph where configured. Deployment options include an Otic-hosted SaaS path and a customer-tenant or Azure Managed Application path. Production and pilot environments must not rely on fake endpoints or mock production data; unconfigured providers must show clear Not configured states.

## 2. Cutover Strategy

For IIP, a phased cutover is preferred over a big-bang transition. The recommended approach is to begin with a controlled motor-claims pilot involving selected users, selected claim types, and defined channels before expanding to broader operations.

## 3. Cutover Options

| Option | Description | Recommended Use |
|---|---|---|
| Soft launch | IIP runs alongside current process for selected users | First pilot |
| Parallel run | Same claims tracked in current process and IIP | Validation period |
| Phased rollout | Expand by branch, claim type, or team | Post-pilot scale |
| Full cutover | IIP becomes primary workflow for agreed scope | After proven pilot |

## 4. Cutover Entry Criteria

| Criterion | Required Evidence |
|---|---|
| UAT complete | UAT sign-off |
| Pilot users trained | Training attendance or completion record |
| Support ready | Support and escalation guide shared |
| Known issues accepted | Known Issues Register reviewed |
| Integrations configured | Provider health checks pass or Not configured accepted |
| Data loaded | Seed/migration reconciliation complete |
| Rollback/fallback approved | Fallback process documented |
| Client sponsor approval | Go-live approval recorded |

## 5. Cutover Timeline Template

| Timeframe | Activity | Owner |
|---|---|---|
| T-10 to T-5 days | Confirm pilot scope, users, data, channels, and support model | Product/Client Success |
| T-5 to T-2 days | Complete UAT fixes, training, and final configuration | Engineering/QA/Client |
| T-1 day | Freeze non-critical changes and confirm go/no-go | Release Lead |
| T day | Enable pilot users and monitor first workflows | Support/Engineering |
| T+1 day | Review first-day issues and metrics | Pilot Governance |
| T+5 days | Review adoption, defects, support load, and improvement backlog | Product/Client Success |

## 6. Go-Live Day Checklist

- Confirm release version and environment.
- Confirm users and roles are active.
- Confirm claims workflow is available.
- Confirm document upload and review are available.
- Confirm AI assistance is configured or shows Not configured.
- Confirm audit trail is logging actions.
- Confirm support contacts are online.
- Confirm fallback workflow is known.
- Confirm client communications have been sent.

## 7. Fallback Plan

| Failure Scenario | Fallback |
|---|---|
| Login fails for pilot users | Use existing/manual process while auth issue is resolved. |
| Claims cannot be created | Record claims through existing process and import/replay later where safe. |
| Documents fail to process | Store documents and conduct manual review until OCR/AI restored. |
| AI unavailable | Continue manual workflow; AI is assistive, not mandatory. |
| Channel integration fails | Use alternate intake channel such as email/manual entry. |
| Severe data/security issue | Pause IIP workflow and escalate under incident response runbook. |

## 8. Cutover Exit Criteria

| Criterion | Target |
|---|---|
| Pilot users active | All planned pilot users can access IIP |
| Critical workflows | Claim intake, documents, review, audit, and support validated |
| Critical defects | Zero open or accepted fallback |
| Support process | Active and tested |
| Client decision | Continue pilot / pause / remediate / expand |


## Assumptions and Open Questions

| Type | Item | Action Required |
|---|---|---|
| Assumption | The pilot MVP is focused on motor claims, document intelligence, internal staff workflows, and human-reviewed AI support. | Confirm during pilot kickoff. |
| Assumption | Azure is the preferred hosting foundation, with SaaS and Managed Application deployment paths both preserved until commercial packaging is finalized. | Confirm per client. |
| Open Question | Final Azure region, private networking requirements, and customer data residency constraints may vary by client. | Confirm with the insurer and implementation team. |
| Open Question | Exact client support contacts, escalation contacts, and approval signatories are not yet fixed. | Capture in the Client Configuration Workbook or pilot kickoff pack. |
| Requires Confirmation | Any pricing, SLA, or regulatory commitment must be confirmed by business, legal, security, and delivery leads before client use. | Review before external distribution. |

