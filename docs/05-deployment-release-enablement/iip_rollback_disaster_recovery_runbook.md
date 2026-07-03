# Rollback & Disaster Recovery Runbook

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Rollback & Disaster Recovery Runbook |
| Version | 1.0 |
| Status | Draft |
| Audience | DevOps, Engineering, Support, Security, Client IT |
| Date | 2026-06-30 |


## 1. Purpose

This runbook defines how IIP teams respond when a deployment, migration, integration, AI provider, database, or infrastructure event requires rollback, recovery, or disaster response.

This document is part of the deployment, release, and client enablement documentation set for Insurance Intelligence Platform (IIP). IIP is an Azure-first, human-reviewed insurance operations platform for claims, documents, channels, customer operations, approvals, auditability, and AI-assisted decision support. Customers interact through external channels such as WhatsApp, email, web forms, SMS, and broker/call-center routes; internal insurer staff use the IIP staff workspace. The MVP is centered on motor claims and document intelligence, with AI assisting through summarization, extraction, classification, routing, and evidence preparation while humans retain authority over consequential insurance decisions.

The expected technical foundation is Next.js/React/TypeScript/Tailwind on the frontend, .NET 8/ASP.NET Core on the backend, Azure SQL, Azure Blob Storage, Azure Key Vault, Azure AI Search, Azure OpenAI, Azure AI Document Intelligence, Azure Service Bus/Event Grid, Azure Functions or Durable Functions, Application Insights/Log Analytics, Microsoft Entra ID/MSAL, and Microsoft Graph where configured. Deployment options include an Otic-hosted SaaS path and a customer-tenant or Azure Managed Application path. Production and pilot environments must not rely on fake endpoints or mock production data; unconfigured providers must show clear Not configured states.

## 2. Recovery Principles

| Principle | Rule |
|---|---|
| Preserve data first | Do not destroy documents, claims, audit logs, messages, or integration events while recovering. |
| Prefer reversible changes | Feature flags and configuration rollback are preferred before code rollback. |
| Separate code and data rollback | Database rollback must be explicitly reviewed; not all migrations are safely reversible. |
| Communicate early | Client-facing impact requires clear status and next-update timing. |
| Audit the recovery | Recovery actions must be logged and attached to incident records. |

## 3. Rollback Triggers

- Authentication or RBAC failure affecting secure access.
- Tenant isolation failure or possible data exposure.
- Critical workflow outage after deployment.
- Failed or partially applied database migration.
- AI behavior violates human-review or safety boundaries.
- Document processing stops for the pilot/production tenant.
- Severe performance degradation after release.
- Integration messages are duplicated, lost, or blocked.

## 4. Rollback Options

| Option | When to Use | Key Risk |
|---|---|---|
| Feature flag rollback | New feature can be disabled safely | Feature flag must already exist |
| Configuration rollback | Provider/template/SLA/role config caused issue | Need known-good config backup |
| Code rollback | New deployment introduced defect | Migrations may not match old code |
| Database migration rollback | Schema/data change failed | Data loss risk if not designed safely |
| Queue replay | Messages failed or were delayed | Duplicate handling/idempotency required |
| Provider fallback | AI/channel/search provider degraded | Fallback must be pre-approved |

## 5. Pre-Rollback Checklist

| Check | Owner | Complete |
|---|---|---|
| Incident severity confirmed | Support Lead |  |
| Current version and previous version identified | DevOps |  |
| Database migration status reviewed | Backend Lead |  |
| Data exposure risk assessed | Security Lead |  |
| Queue state and dead-letter count captured | DevOps |  |
| Client impact confirmed | Client Success |  |
| Communication owner assigned | Client Success |  |

## 6. Recovery Procedures

### 6.1 Failed Frontend or API Deployment

1. Pause further promotion.
2. Confirm issue is release-related using monitoring and release logs.
3. Revert to previous container/app version or deployment slot.
4. Validate health endpoints.
5. Run smoke tests for login, dashboard, claims, documents, AI status, and audit.
6. Send client/internal update if pilot or production was impacted.

### 6.2 Failed Database Migration

1. Stop write-heavy jobs if necessary.
2. Confirm migration step and database state.
3. Restore from backup only with explicit approval if data risk exists.
4. Apply forward-fix migration where safer than rollback.
5. Verify data integrity and affected entities.
6. Record recovery actions in incident report.

### 6.3 Queue or Integration Recovery

1. Freeze duplicate processors if needed.
2. Capture active, scheduled, deferred, and dead-letter counts.
3. Confirm idempotency keys for messages.
4. Replay selected messages only after duplicate-risk review.
5. Validate claim/document/channel records after replay.

### 6.4 AI Provider Degradation

1. Confirm provider health and error category.
2. Show Not configured or degraded-state UI where provider is unavailable.
3. Disable affected AI actions if outputs cannot be trusted.
4. Preserve manual workflow capability.
5. Review AI logs for unsafe or partial outputs.

## 7. Disaster Recovery Planning Checklist

| Area | Requirement | Status |
|---|---|---|
| Backups | Azure SQL backup policy defined and tested | Open Question |
| Blob recovery | Document retention and restore process defined | Open Question |
| Key Vault | Secret backup/rotation process defined | Open Question |
| Infrastructure | IaC or deployment scripts can recreate environment | Requires Confirmation |
| Monitoring | Alerts cover API, DB, queues, providers, and storage | Requires Confirmation |
| DR test | Restore test performed before production | Open Question |

## 8. Post-Recovery Review

Every Sev 1/2 rollback or disaster event requires a post-incident review covering cause, impact, timeline, customer communication, remediation, preventive controls, backlog items, and owner/date.


## Assumptions and Open Questions

| Type | Item | Action Required |
|---|---|---|
| Assumption | The pilot MVP is focused on motor claims, document intelligence, internal staff workflows, and human-reviewed AI support. | Confirm during pilot kickoff. |
| Assumption | Azure is the preferred hosting foundation, with SaaS and Managed Application deployment paths both preserved until commercial packaging is finalized. | Confirm per client. |
| Open Question | Final Azure region, private networking requirements, and customer data residency constraints may vary by client. | Confirm with the insurer and implementation team. |
| Open Question | Exact client support contacts, escalation contacts, and approval signatories are not yet fixed. | Capture in the Client Configuration Workbook or pilot kickoff pack. |
| Requires Confirmation | Any pricing, SLA, or regulatory commitment must be confirmed by business, legal, security, and delivery leads before client use. | Review before external distribution. |

