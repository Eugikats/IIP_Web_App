# CI/CD Pipeline Guide

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | CI/CD Pipeline Guide |
| Version | 1.0 |
| Status | Draft |
| Audience | DevOps engineers, engineering leads, QA leads, security leads, coding agents |
| Date | 2026-06-30 |


## 1. Purpose

This guide defines build, test, lint, migration, deploy, rollback, branch rules, release gates, and environment promotion for IIP.


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


## Pipeline Principles

- Every change must be buildable, testable, reviewable, and traceable.
- Security checks and tests are not optional for pilot/production releases.
- Database migrations must be reviewed and reversible where practical.
- Deployments must produce observable release records and post-deployment validation evidence.
- Hotfixes must not bypass security, tenant isolation, or audit controls.

## Branching and Promotion Model

| Stage | Branch / Source | Target Environment | Required Gates |
| --- | --- | --- | --- |
| Feature development | feature/* | Local/dev | Build, unit tests, lint/typecheck. |
| Integration | develop or integration branch | Dev/Staging | Build, unit, integration, API smoke tests. |
| Release candidate | release/* | UAT/Pilot | Security checks, regression, migration dry-run, release readiness. |
| Production release | main tag/version | Production | Approval, backup, deployment window, post-deploy validation. |
| Hotfix | hotfix/* | Production then back-merge | Focused tests, security review, approval, postmortem if incident. |

## Pipeline Stages

| Stage | Actions | Failure Handling |
| --- | --- | --- |
| Validate | Check formatting, lint, typecheck, solution restore | Block merge. |
| Build | Build frontend and backend artifacts | Block merge/release. |
| Test | Unit, integration, API, RBAC, tenant-boundary tests | Block release. |
| Security | Dependency scan, secret scan, static checks, IaC scan | Block pilot/production if high severity. |
| Package | Create deployable artifacts/images/IaC package | Store versioned artifacts. |
| Migrate | Run migration plan/dry-run; apply with approval | Rollback or stop on failure. |
| Deploy | Deploy API, workers, frontend, infra changes | Use staged rollout where possible. |
| Validate | Run health checks, smoke tests, provider health, audit event test | Rollback or incident process. |

## Release Gates

| Gate | Minimum Requirements |
| --- | --- |
| Demo | Build passes, core screens load, no broken navigation, obvious states work. |
| Staging | API tests, auth/RBAC tests, provider health, real state handling, no fake production data. |
| UAT | Functional tests, document workflow, AI review gates, audit trail, client UAT scripts. |
| Pilot | Security validation, release readiness, support runbook, monitoring, backup, rollback plan. |
| Production | Client sign-off, DR/rollback tested, support model active, compliance evidence registered. |

## Rollback Rules

| Area | Rollback Requirement |
| --- | --- |
| Frontend | Previous build can be redeployed quickly. |
| Backend | Versioned artifact rollback; maintain API compatibility where possible. |
| Database | Migration rollback plan or forward-fix plan required before deployment. |
| AI prompts/tools | Prompt/tool registry changes versioned and reversible. |
| Integrations | Provider config changes tracked and reversible. |

## Acceptance Criteria

| ID | Acceptance Criterion | Evidence |
| --- | --- | --- |
| AC-CICD-001 | Pipeline builds frontend and backend from clean checkout. | Pipeline run evidence. |
| AC-CICD-002 | Tests and security checks gate pilot/production deployment. | Pipeline gate config. |
| AC-CICD-003 | Release artifacts are versioned and traceable. | Artifact registry/release logs. |
| AC-CICD-004 | Post-deploy validation confirms health, auth, provider health, and core workflow. | Validation report. |

## Open Questions and Required Confirmations

| ID | Open Question / Confirmation Needed | Impact |
| --- | --- | --- |
| OQ-CICD-001 | Which CI/CD platform is final: GitHub Actions, Azure DevOps, or another? | Affects implementation. |
| OQ-CICD-002 | Will containers be used for API/workers/frontend? | Affects artifact strategy. |
| OQ-CICD-003 | Who approves pilot and production deployments? | Affects release governance. |
