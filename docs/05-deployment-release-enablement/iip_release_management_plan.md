# Release Management Plan

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Release Management Plan |
| Version | 1.0 |
| Status | Draft |
| Audience | Product, Engineering, QA, DevOps, Support, Client Success, Pilot Governance |
| Date | 2026-06-30 |


## 1. Purpose

The Release Management Plan defines how IIP changes move from development into demo, staging, UAT, pilot, and production environments. It creates a repeatable process for planning, approving, testing, releasing, communicating, and rolling back changes.

This document is part of the deployment, release, and client enablement documentation set for Insurance Intelligence Platform (IIP). IIP is an Azure-first, human-reviewed insurance operations platform for claims, documents, channels, customer operations, approvals, auditability, and AI-assisted decision support. Customers interact through external channels such as WhatsApp, email, web forms, SMS, and broker/call-center routes; internal insurer staff use the IIP staff workspace. The MVP is centered on motor claims and document intelligence, with AI assisting through summarization, extraction, classification, routing, and evidence preparation while humans retain authority over consequential insurance decisions.

The expected technical foundation is Next.js/React/TypeScript/Tailwind on the frontend, .NET 8/ASP.NET Core on the backend, Azure SQL, Azure Blob Storage, Azure Key Vault, Azure AI Search, Azure OpenAI, Azure AI Document Intelligence, Azure Service Bus/Event Grid, Azure Functions or Durable Functions, Application Insights/Log Analytics, Microsoft Entra ID/MSAL, and Microsoft Graph where configured. Deployment options include an Otic-hosted SaaS path and a customer-tenant or Azure Managed Application path. Production and pilot environments must not rely on fake endpoints or mock production data; unconfigured providers must show clear Not configured states.

## 2. Release Principles

| Principle | Rule |
|---|---|
| Human-reviewed production changes | No production release is promoted without engineering, QA, security, and business sign-off. |
| No fake production behavior | Staging and production must not depend on mock APIs, fake provider success, or hardcoded demo data. |
| Tenant-safe by default | Tenant context, workspace context, role checks, and audit logging must be validated before release. |
| AI cannot bypass governance | AI features must preserve human approval gates and must not issue final claims, settlement, fraud, or underwriting decisions. |
| Reversible releases | Each release must have rollback steps, database migration review, and known recovery actions. |

## 3. Release Types

| Release Type | Description | Approval Level | Typical Target |
|---|---|---|---|
| Feature release | Adds or changes functionality, screens, workflows, APIs, or AI tools. | Product, Engineering, QA, Security if applicable | Staging -> UAT -> Pilot/Production |
| Configuration release | Changes tenant settings, templates, SLAs, role mappings, channel settings, or provider configuration. | Product Owner and Client Admin | UAT/Pilot/Production |
| Hotfix | Fixes a high-priority production defect with limited scope. | Engineering Lead, QA Lead, Support Lead | Production |
| Security fix | Fixes vulnerability, auth issue, data exposure, or access-control problem. | Security Lead and Engineering Lead | All active environments |
| AI behavior update | Changes prompts, tool registry, RAG rules, confidence handling, or AI output policies. | Product, AI Owner, QA, Compliance | Staging -> UAT -> Production |

## 4. Versioning Model

Recommended version format: `major.minor.patch-build`.

| Version Segment | Meaning | Example |
|---|---|---|
| Major | Significant product or platform change | 1.0.0 |
| Minor | New module, workflow, or major capability | 1.1.0 |
| Patch | Bug fix, security fix, or minor improvement | 1.1.1 |
| Build | CI/CD build identifier | 1.1.1-20260630.4 |

## 5. Branching and Promotion Rules

| Stage | Source | Required Checks | Output |
|---|---|---|---|
| Development | Feature branches | Compile, lint, unit tests, peer/AI-agent review | Merge request |
| Integration | Develop/main integration branch | API tests, component tests, migration checks | Staging candidate |
| Staging | Release branch | Functional tests, regression tests, security checks, AI safety checks | UAT candidate |
| UAT | Approved release candidate | Client UAT, sign-off, release notes | Pilot/production candidate |
| Production | Signed release candidate | Go/no-go meeting and post-deploy validation | Live release |

## 6. Release Approval Gates

| Gate | Required Evidence | Owner |
|---|---|---|
| Build gate | Successful frontend/backend build and test run | Engineering Lead |
| API gate | API test suite passes for auth, tenant context, validation, errors, and audit behavior | QA Lead |
| Security gate | Security validation checklist completed with no unresolved critical findings | Security Lead |
| AI gate | AI red-team checks pass where AI behavior changed | AI Owner / QA |
| Data gate | Migration script reviewed, reversible where possible, and tested on staging data | Backend Lead |
| Client gate | UAT sign-off for client-facing releases | Client Sponsor / Product Owner |
| Operations gate | Monitoring, rollback, support contacts, and release notes are ready | Support Lead |

## 7. Release Checklist

- Confirm release scope and impacted modules.
- Confirm version number and release branch.
- Confirm database migrations and rollback approach.
- Confirm feature flags and configuration changes.
- Confirm no secrets, tokens, or credentials are committed.
- Confirm tenant/workspace and RBAC checks are tested.
- Confirm provider health and Not configured states render properly.
- Confirm AI features preserve human-review gates.
- Confirm release notes are ready.
- Confirm post-deployment validation owner and window.

## 8. Release Notes Structure

Each release note should include: version, date, environment, summary, new features, changed behavior, fixed issues, security notes, AI behavior notes, known issues, rollback notes, client action required, and support contact.

## 9. Rollback Decision Rules

Rollback must be considered when any of the following occur: authentication failure, tenant data exposure risk, critical workflow failure, payment/settlement related defect, AI safety breach, failed database migration, unavailable core APIs, or severe performance degradation.

## 10. Sign-Off Template

| Sign-Off Area | Name | Role | Decision | Date | Notes |
|---|---|---|---|---|---|
| Product |  |  | Approved / Not Approved |  |  |
| Engineering |  |  | Approved / Not Approved |  |  |
| QA |  |  | Approved / Not Approved |  |  |
| Security |  |  | Approved / Not Approved |  |  |
| Support |  |  | Approved / Not Approved |  |  |
| Client/UAT |  |  | Approved / Not Approved |  |  |


## Assumptions and Open Questions

| Type | Item | Action Required |
|---|---|---|
| Assumption | The pilot MVP is focused on motor claims, document intelligence, internal staff workflows, and human-reviewed AI support. | Confirm during pilot kickoff. |
| Assumption | Azure is the preferred hosting foundation, with SaaS and Managed Application deployment paths both preserved until commercial packaging is finalized. | Confirm per client. |
| Open Question | Final Azure region, private networking requirements, and customer data residency constraints may vary by client. | Confirm with the insurer and implementation team. |
| Open Question | Exact client support contacts, escalation contacts, and approval signatories are not yet fixed. | Capture in the Client Configuration Workbook or pilot kickoff pack. |
| Requires Confirmation | Any pricing, SLA, or regulatory commitment must be confirmed by business, legal, security, and delivery leads before client use. | Review before external distribution. |

