# Developer Setup Guide

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Developer Setup Guide |
| Version | 1.0 |
| Status | Draft |
| Audience | Developers, QA engineers, DevOps engineers, coding agents |
| Date | 2026-06-30 |


## 1. Purpose

This guide explains local setup, prerequisites, frontend/backend run commands, database setup, local mock mode, Azure configuration, and test commands for IIP development.


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


## Assumed Stack

| Layer | Technology |
| --- | --- |
| Frontend | Next.js, React, TypeScript, Tailwind CSS. |
| Backend | ASP.NET Core / .NET 8, EF Core. |
| Database | Azure SQL / SQL Server-compatible local development. |
| Storage | Azure Blob Storage or local emulator/dev substitute where approved. |
| AI | Azure OpenAI, Azure AI Document Intelligence, Azure AI Search. |
| Identity | Entra ID / MSAL; demo headers may exist only for local/demo contexts. |
| Observability | Application Insights / structured local logs. |

## Local Setup Checklist

| Step | Action | Done |
| --- | --- | --- |
| 1 | Install .NET 8 SDK | [ ] |
| 2 | Install Node.js LTS and package manager used by project | [ ] |
| 3 | Install SQL Server/Azure SQL dev option or configure dev database | [ ] |
| 4 | Clone repository and install frontend dependencies | [ ] |
| 5 | Restore backend packages | [ ] |
| 6 | Create local environment files from sample templates | [ ] |
| 7 | Run EF Core migrations against local database | [ ] |
| 8 | Start backend API and verify /health | [ ] |
| 9 | Start frontend and verify app shell | [ ] |
| 10 | Run tests and lint checks | [ ] |

## Suggested Commands

| Purpose | Command Pattern | Notes |
| --- | --- | --- |
| Restore backend | dotnet restore | Run from solution root. |
| Run backend | dotnet run --project <backend-project> | Exact project path Requires Confirmation. |
| Run migrations | dotnet ef database update | Requires EF tooling and connection string. |
| Install frontend | npm install or pnpm install | Package manager Requires Confirmation. |
| Run frontend | npm run dev or pnpm dev | Exact script Requires Confirmation. |
| Run tests | dotnet test and npm test | Exact test scripts Requires Confirmation. |
| Lint/typecheck | npm run lint and npm run typecheck | Exact scripts Requires Confirmation. |

## Local Demo Headers

| Header | Demo Value | Important Warning |
| --- | --- | --- |
| X-Tenant-Id | tenant-demo | Local/demo only. |
| X-Workspace-Id | workspace-claims | Local/demo only. |
| x-demo-role | Admin | Must not replace production auth. |
| x-demo-user-id | local-admin | Must not be trusted in production. |
| X-User-Id | local-admin | Validate server-side in real auth. |
| X-Correlation-ID | uuid | Use for traceability. |

## Mock and Demo Data Rules

| Environment | Allowed Behavior |
| --- | --- |
| Local development | Seeded demo data allowed if clearly labeled. |
| Demo mirror | Demo data allowed only if it is clearly demo and not mixed with production. |
| Staging | No fake operational data unless explicitly controlled and labeled for UAT. |
| Production | No fake data. Empty/error/Not configured states must be real. |

## Acceptance Criteria

| ID | Acceptance Criterion | Evidence |
| --- | --- | --- |
| AC-DEV-001 | A new developer can run frontend and backend locally using this guide. | Onboarding dry run. |
| AC-DEV-002 | Local health checks and basic case APIs work. | Smoke test. |
| AC-DEV-003 | Secrets are not committed or displayed. | Repo scan. |

## Open Questions and Required Confirmations

| ID | Open Question / Confirmation Needed | Impact |
| --- | --- | --- |
| OQ-DEV-001 | What is the final repository structure and project names? | Affects command accuracy. |
| OQ-DEV-002 | Which package manager is standardized: npm, pnpm, or yarn? | Affects setup. |
| OQ-DEV-003 | Will Docker Compose be used for local dependencies? | Affects setup path. |
