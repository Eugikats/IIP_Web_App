# Insurance Intelligence Platform (IIP) - Deployment Plan

**Document type:** Deployment Plan / Release and Environment Strategy  
**Product name:** Insurance Intelligence Platform  
**Acronym:** IIP  
**Prepared for:** Otic Group / IIP Product, Engineering, Cloud, Security, QA, Delivery, and Leadership Teams  
**Date:** 30 June 2026  
**Version:** 1.0 - Consolidated MVP Deployment Baseline  
**Status:** Working deployment baseline for MVP implementation and pilot readiness  
**Classification:** Internal - Product, Engineering, Cloud, Security, QA, Delivery, Support, and Leadership Reference

---

## Document Control

| Field | Value |
|---|---|
| Owner | Cloud / DevOps / Engineering Lead |
| Primary audience | Engineering, cloud, DevOps, security, QA, product, delivery, support, and leadership |
| Source baseline | IIP Master Product Context, PRD, FSD, Technical Architecture, System Integration Strategy, API Specification, Data Model, Security and Compliance Document, QA/Test Plan, UI/UX Handoff, chat decisions, Antigravity/codebase findings |
| Current product name | **Insurance Intelligence Platform (IIP)** |
| Superseded name | ACIP / Agentic Insurance Intelligence Platform |
| MVP focus | Motor Claims and Document Intelligence for an insurance company pilot |
| Deployment stance | Azure-first, secure-by-default, environment-controlled, observable, auditable, human-reviewed, tenant-aware |
| Decision model | AI assists. Humans make regulated insurance decisions. |

---

## 1. Executive Summary

The **Insurance Intelligence Platform (IIP)** is an Azure-native insurance operations platform that helps insurers acquire customers, process claims, review documents, manage customer interactions, support underwriting workflows, and maintain auditable AI-assisted operations.

The MVP focuses on **Motor Claims and Document Intelligence** for a mid-to-large insurance company pilot. The deployment plan must support the operational path from local development to staging, pilot production, and future enterprise deployment through either an Otic-hosted SaaS model or an Azure Managed Application / private customer tenant model.

This document defines how IIP should be deployed, validated, secured, observed, rolled back, handed over, and operated.

The deployment strategy is guided by five non-negotiable principles:

1. **No mock production** - staging and production must not depend on fake endpoints or fake data paths.
2. **Human-controlled insurance decisions** - deployment must preserve human review and approval gates.
3. **Tenant and workspace isolation** - every deployed environment must enforce tenant/workspace context server-side.
4. **Observable by default** - every environment must have health, telemetry, audit, and failure visibility.
5. **Azure-first enterprise readiness** - deployment should align with Microsoft-native identity, secrets, monitoring, and managed deployment patterns.

The immediate recommended deployment path is:

```text
Local development with mocked providers
↓
Shared cloud development environment
↓
Controlled staging environment with real provider configuration
↓
Pilot production environment for the first insurer
↓
Enterprise private deployment / Azure Managed Application path
```

---

## 2. Purpose of this Document

This Deployment Plan converts the IIP architecture, security, data, API, UI, and QA decisions into a practical implementation and release strategy.

It answers the following questions:

1. What deployment models should IIP support?
2. What environments are needed before pilot deployment?
3. What Azure resources must be provisioned?
4. How should configuration, secrets, and tenant settings be managed?
5. How should CI/CD work for frontend, backend, workers, and infrastructure?
6. What checks are required before release?
7. How should deployments be verified and rolled back?
8. How should monitoring, alerts, audit logs, and support handover work?
9. How should integration providers be configured across environments?
10. What remains unresolved before production readiness?

This document does not replace IaC templates, GitHub Actions workflows, Azure DevOps pipelines, release notes, runbooks, or client-specific implementation plans. Those should be created as follow-on artifacts.

---

## 3. Deployment Scope

### 3.1 In Scope

This deployment plan covers the MVP deployment of:

- Next.js / React / TypeScript frontend.
- .NET 8 backend APIs.
- Azure SQL transactional database.
- Azure Blob Storage for documents and evidence files.
- Azure AI Document Intelligence for OCR and extraction.
- Azure AI Search for approved knowledge retrieval.
- Azure OpenAI for summaries, explanations, and assistant outputs.
- Azure Functions / Durable Functions for background workflows.
- Azure Service Bus / event queues for async processing.
- Microsoft Entra ID authentication.
- Microsoft Graph / Outlook integration where configured.
- WhatsApp Business integration where configured.
- Admin configuration, provider health, and operational dashboards.
- Audit logging and human review workflows.
- Environment setup, CI/CD, monitoring, rollback, and release gates.

### 3.2 Out of Scope for MVP Deployment

The following should not be treated as mandatory for the first MVP deployment unless the pilot contract specifically requires them:

- Fully offline claims processing.
- Direct automatic settlement or payment release.
- Autonomous claim approval or rejection.
- Full underwriting rating engine.
- Full Microsoft Marketplace transactable SaaS billing.
- Cross-country regulatory automation.
- Advanced Microsoft Fabric analytics.
- Full voice/TTS/call-agent production deployment.
- Direct write access into insurer core systems without human approval and explicit integration control.
- Production use of real customer PII in test/demo documents without legal and client authorization.

---

## 4. Deployment Principles

### 4.1 Azure-First

IIP should use Microsoft Azure as the primary deployment platform. Azure services should be selected because they support enterprise identity, governance, auditability, AI services, document processing, search, observability, and future Managed Application deployment.

### 4.2 Secure by Default

Every deployed environment must use least-privilege access, secure secrets management, encryption in transit, encryption at rest, tenant isolation, audit logging, and role-aware access controls.

### 4.3 No Frontend-Only Security

Frontend role-aware navigation is useful for user experience, but it must never be treated as the security boundary. Backend APIs must enforce tenant, workspace, role, permission, and object-level access.

### 4.4 No Mock Production

Local development may use mocked providers. Staging and production must use real provider configuration, real service health checks, and real failure handling.

### 4.5 Human Review Preserved

Deployment must not bypass human approval workflows. Any release that enables autonomous claim approval, claim rejection, settlement approval, fraud determination, payment release, final underwriting decision, or policy binding is not acceptable.

### 4.6 Observable by Default

Every deployed workload must expose health, readiness, logging, metrics, traces, queue depth, integration status, AI provider status, document processing status, and audit event status.

### 4.7 Integration-Safe Rollout

IIP should begin with controlled ingestion and export paths, then expand toward deeper integrations. Read-only integrations should come before write-back. Human-approved write-back should come before any automated integration action.

---

## 5. Target Deployment Models

IIP should support three deployment models over time.

### 5.1 Model A - Otic-Hosted SaaS

In this model, IIP runs in Otic-controlled Azure infrastructure and serves insurers as logical tenants.

| Attribute | Direction |
|---|---|
| Best for | Demos, proof-of-concepts, small insurers, brokers, microinsurance providers, early pilots |
| Data isolation | Logical tenant isolation in shared infrastructure |
| Cost | Lower startup cost |
| Onboarding speed | Fastest |
| Security challenge | Tenant isolation must be extremely strong |
| Marketplace readiness | Can later become SaaS offer |

This is useful for quick pilots, but it may not satisfy larger insurers that require stronger control over data, networking, and secrets.

### 5.2 Model B - Enterprise Private Deployment

In this model, IIP is deployed into a dedicated Azure subscription or the customer's Azure tenant.

| Attribute | Direction |
|---|---|
| Best for | Large insurers, banks, HMOs, regulated institutions, enterprise pilots |
| Data isolation | Dedicated infrastructure or customer tenant boundary |
| Cost | Higher |
| Onboarding speed | Slower than SaaS |
| Security advantage | Stronger customer trust and data control |
| Marketplace readiness | Can evolve into Azure Managed Application |

This is the recommended direction for serious insurer pilots where data control and governance are major buying concerns.

### 5.3 Model C - Azure Managed Application

In this model, IIP is packaged so the customer deploys it through Azure Marketplace into their subscription, while Otic retains controlled publisher/support access where permitted.

| Attribute | Direction |
|---|---|
| Best for | Enterprise productization and Microsoft Marketplace path |
| Data control | Customer subscription / managed resource group |
| Otic support | Controlled access through managed application permissions |
| Complexity | High |
| MVP requirement | Not mandatory for first pilot unless leadership decides it is required |
| Long-term value | Strong enterprise SaaS distribution model |

The deployment plan should keep Managed Application readiness in mind, but the first pilot may be manually deployed into a customer or dedicated Azure environment to move faster.

### 5.4 Recommended MVP Deployment Path

For the first serious insurer pilot:

```text
Preferred: Enterprise Private Deployment or dedicated pilot Azure environment
Fallback: Otic-hosted SaaS pilot if customer accepts hosted model
Future: Azure Managed Application packaging for repeatable enterprise deployment
```

---

## 6. Environment Strategy

IIP should use clearly separated environments.

| Environment | Purpose | Data Type | Provider Mode | Access |
|---|---|---|---|---|
| Local | Developer build and fast iteration | Synthetic/local only | Mock or local adapters allowed | Individual developers |
| Development | Shared integration development | Synthetic or seeded test data | Mixed, controlled test providers | Engineering |
| Staging | Release validation and UAT | Synthetic or client-approved test data | Real providers where possible | Engineering, QA, product, pilot stakeholders |
| Pilot Production | First customer deployment | Real customer data if approved | Real providers only | Authorized pilot users and support |
| Production | Scaled customer operations | Real customer data | Real providers only | Authorized customer and support teams |
| Disaster Recovery | Recovery validation | Replicated or backed-up data where allowed | Real recovery configuration | Cloud/security/support |

### 6.1 Local Environment

Local mode must allow developers to run the app without Azure credentials.

Local mode may include:

- Mock AI provider.
- Mock document extraction.
- Local database or SQLite where appropriate.
- Seeded synthetic claims, customers, leads, and documents.
- Local auth simulation for developer testing.

Local mode must not be confused with staging or production readiness.

### 6.2 Development Environment

The shared development environment is used for feature integration.

It should include:

- Dev frontend deployment.
- Dev API deployment.
- Dev Azure SQL database.
- Dev Blob containers.
- Dev queues.
- Test AI provider configuration where available.
- Test document intelligence configuration.
- Test Entra ID app registration.
- Test tenant/workspace seed data.

### 6.3 Staging Environment

Staging should behave as close to production as possible.

Staging must include:

- Real authentication path.
- Real API deployment.
- Real database migrations.
- Real Blob Storage.
- Real Service Bus queues.
- Real health checks.
- Real App Insights telemetry.
- Real provider status checks.
- No hardcoded browser-owned permissions.
- No fake production endpoints.

Staging data should be synthetic, redacted, or client-approved. Real PII should not be used unless legally approved.

### 6.4 Pilot Production Environment

Pilot production is the first live customer environment.

It must include:

- Production-grade identity.
- Production secrets in Key Vault.
- Tenant/workspace configuration.
- Real provider connections.
- Audit logging.
- Backup and restore configuration.
- Monitoring alerts.
- Support access controls.
- Incident response contacts.
- Release notes.
- Rollback plan.

---

## 7. Azure Resource Baseline

The MVP deployment should provision the following Azure resources.

| Layer | Azure Resource | MVP Role |
|---|---|---|
| Frontend | Azure App Service, Static Web Apps, or Container Apps | Host Next.js frontend |
| Backend API | Azure App Service or Container Apps | Host .NET 8 API |
| Workflow workers | Azure Functions / Durable Functions | Document processing, async workflows, escalation jobs |
| Database | Azure SQL Database | Claims, customers, policies, leads, tasks, audit metadata |
| Storage | Azure Blob Storage | Documents, evidence files, exports |
| Queues | Azure Service Bus | Async document processing, integration events, retries |
| Search | Azure AI Search | Approved knowledge retrieval and RAG grounding |
| Document AI | Azure AI Document Intelligence | OCR, extraction, classification |
| LLM | Azure OpenAI | Summaries, assistant responses, drafting |
| Secrets | Azure Key Vault | API keys, connection strings, certificates, provider secrets |
| Identity | Microsoft Entra ID | Staff login, RBAC, app registrations |
| API gateway | Azure API Management | Future external API governance and throttling |
| Monitoring | Application Insights, Azure Monitor, Log Analytics | Logs, traces, metrics, alerts |
| Security | Defender for Cloud, Microsoft Sentinel | Security posture and SIEM path |
| Analytics | Power BI / Fabric later | Executive and operational dashboards |

### 7.1 MVP Minimal Azure Stack

For a practical first deployment, the minimum viable Azure stack is:

- Frontend hosting.
- Backend API hosting.
- Azure SQL.
- Blob Storage.
- Key Vault.
- Application Insights / Log Analytics.
- Entra ID app registration.
- Azure AI Document Intelligence.
- Azure AI Search.
- Azure OpenAI.
- Service Bus.

### 7.2 Phase 2 Azure Additions

Phase 2 may add:

- Azure API Management.
- Microsoft Sentinel.
- Defender for Cloud hardening.
- Power BI/Fabric analytics.
- Private endpoints.
- Customer-managed keys.
- Azure Managed Application packaging.
- Advanced networking and hub-spoke architecture.

---

## 8. Azure Region Strategy

The recommended starting region is **South Africa North**, because it is geographically closer to Uganda and provides an Africa-based deployment posture.

A fallback region such as **West Europe** may be required if specific Azure OpenAI models, quotas, or service capabilities are not available in South Africa North.

### 8.1 Region Selection Criteria

| Criterion | Requirement |
|---|---|
| Service availability | Azure OpenAI, AI Search, Document Intelligence, App Service/Container Apps, Functions, SQL, Blob, Key Vault, Monitor |
| Model availability | Required GPT and embedding models must be deployable with enough quota |
| Data residency | Must satisfy customer and legal review requirements |
| Latency | Acceptable user and provider latency for Uganda operations |
| Cost | Sustainable for pilot and future SaaS/enterprise deployments |
| Compliance | Must support customer security and privacy review |
| Disaster recovery | Must support backup and restore strategy |

### 8.2 Required Confirmation Before Production

Engineering must confirm:

- Azure OpenAI model availability.
- Embedding model availability.
- Azure AI Document Intelligence availability.
- Azure AI Search availability.
- Azure SQL availability and backup options.
- App Service or Container Apps availability.
- Log Analytics and Application Insights availability.
- Quotas and rate limits.
- Customer/legal approval for selected processing region.

---

## 9. Infrastructure as Code Strategy

IIP should use Infrastructure as Code for repeatable deployments.

Recommended options:

| Option | Use |
|---|---|
| Bicep | Strong Microsoft-native option for Azure resources |
| Terraform | Strong cross-cloud/team-standard option if already adopted |
| ARM JSON | Required output path for Azure Managed Application packaging |
| GitHub Actions / Azure DevOps | CI/CD execution |

### 9.1 IaC Goals

IaC should provision:

- Resource groups.
- App hosting resources.
- Azure SQL server/database.
- Blob Storage accounts and containers.
- Service Bus namespaces and queues.
- Key Vault and access policies/RBAC.
- AI Search service and indexes where feasible.
- Document Intelligence resource.
- Azure OpenAI resource and deployments where permitted.
- Application Insights / Log Analytics.
- Managed identities.
- Environment variables/app settings.
- Role assignments.

### 9.2 IaC Rules

- No secrets should be stored in IaC files.
- Secrets must be referenced from Key Vault.
- Environment-specific values must be parameterized.
- Resource names should be deterministic and environment-scoped.
- Tags should be applied for product, environment, owner, cost center, customer, and data classification.
- Production IaC changes must be reviewed before apply.

---

## 10. CI/CD Strategy

IIP should use automated pipelines for consistent releases.

### 10.1 Pipeline Categories

| Pipeline | Purpose |
|---|---|
| Frontend pipeline | Build, lint, test, package, deploy Next.js app |
| Backend pipeline | Build, test, package, deploy .NET 8 API |
| Worker pipeline | Build and deploy Functions/Durable Functions |
| Database pipeline | Validate and apply EF Core or SQL migrations |
| IaC pipeline | Provision and update Azure infrastructure |
| Documentation pipeline | Publish release notes and deployment docs |

### 10.2 Recommended Pipeline Flow

```text
Pull request opened
↓
Static checks run
↓
Unit tests run
↓
Build artifacts produced
↓
Security/dependency checks run
↓
Review and approval
↓
Merge to main
↓
Deploy to development
↓
Integration tests
↓
Promote to staging
↓
UAT and release gate
↓
Manual approval for pilot production
↓
Deploy to pilot production
↓
Smoke tests and monitoring verification
```

### 10.3 Frontend Checks

Frontend deployment must run:

- TypeScript type checking.
- Linting.
- Unit/component tests where available.
- Route build validation.
- Environment variable validation.
- Accessibility checks for critical screens where possible.
- Bundle sanity check.

### 10.4 Backend Checks

Backend deployment must run:

- .NET build.
- Unit tests.
- Integration tests.
- API contract tests.
- Auth/tenant guard tests.
- Database migration validation.
- Static analysis where available.
- Dependency vulnerability checks.

### 10.5 Database Checks

Database release must validate:

- Migration order.
- Roll-forward path.
- Rollback/backout path or restoration plan.
- Required indexes.
- Non-null/default constraints.
- Tenant/workspace columns.
- Audit columns.
- Seed data safety.

---

## 11. Configuration and Secrets Management

### 11.1 Configuration Principles

- Environment configuration must be explicit.
- Secrets must not be committed to source control.
- Production secrets must be stored in Azure Key Vault.
- Local `.env` files should never contain production secrets.
- Provider configuration must support `Not configured` states in the UI.
- Missing provider config must fail safely.

### 11.2 Required Configuration Areas

| Area | Examples |
|---|---|
| API base URLs | Frontend-to-backend API URL |
| Tenant defaults | Default tenant/workspace for local/demo only |
| Entra ID | Tenant ID, client ID, authority, audience |
| Azure SQL | Connection string or managed identity config |
| Blob Storage | Account/container names and identity permissions |
| Service Bus | Namespace, queues, identity permissions |
| Azure OpenAI | Endpoint, deployment names, API version, model mappings |
| AI Search | Endpoint, index names, identity/API config |
| Document Intelligence | Endpoint, model IDs, extraction mode |
| Microsoft Graph | App registration, scopes, mailbox settings |
| WhatsApp | Provider, webhook secret, phone number ID, access token |
| Observability | App Insights connection string or managed config |

### 11.3 Key Vault Usage

Key Vault should store:

- Provider API keys.
- WhatsApp tokens.
- Graph client secrets if used.
- Storage/account keys only if managed identity is not available.
- Database secrets only if managed identity is not available.
- Certificate material.
- External integration credentials.

Managed Identity should be preferred wherever possible.

---

## 12. Identity and Access Deployment

### 12.1 Microsoft Entra ID

IIP should use Microsoft Entra ID for staff authentication.

Deployment must configure:

- App registration for frontend/API.
- Redirect URIs per environment.
- API scopes or application roles.
- Token audience validation.
- Authority/issuer validation.
- MFA/Conditional Access where the customer requires it.
- Group/role mapping where appropriate.

### 12.2 Role and Tenant Enforcement

Every API call must enforce:

- Authenticated user.
- Tenant ID.
- Workspace ID.
- Role/permission.
- Object ownership or scope.
- Audit correlation ID.

Frontend role-aware navigation is not enough.

### 12.3 Known Auth Risk

Prior context mentioned staging authentication issues around issuer/authority and v1/v2 token mismatch. Deployment must include authentication smoke tests for:

- `/api/v1/me`.
- Command Center authenticated load.
- Role-aware navigation.
- Unauthorized state.
- Expired token handling.
- Wrong tenant/workspace handling.

---

## 13. Data Deployment and Migration Plan

### 13.1 Database Provisioning

The database deployment must create the canonical MVP data structures for:

- Tenants.
- Workspaces.
- Users.
- Roles and permissions.
- Customers.
- Policies.
- Claims.
- Documents.
- Document extraction results.
- Channel conversations.
- Sales leads.
- Tasks/work items.
- Approvals.
- Audit events.
- AI runs.
- Integration events.
- Provider health events.

### 13.2 Database Migration Strategy

Recommended approach:

```text
Create migration in development
↓
Review schema changes
↓
Apply to development
↓
Run integration tests
↓
Apply to staging
↓
Run migration validation and smoke tests
↓
Backup production
↓
Apply to pilot production during controlled window
↓
Run post-migration checks
```

### 13.3 Seed Data

Local and development seed data may include synthetic:

- Tenants.
- Workspaces.
- Users.
- Motor claims.
- Customers.
- Documents.
- Leads.
- Channel conversations.

Staging should use synthetic or client-approved test data.

Production should not include fake demo records unless clearly marked and approved.

### 13.4 PII and Test Data Rules

- Do not use real customer PII in local development.
- Do not use real customer PII in staging unless approved.
- Demo documents must be synthetic or redacted.
- Test data should include edge cases for document processing.
- Data retention rules should be documented before pilot.

---

## 14. Document Storage Deployment

Blob Storage must be configured for document storage.

### 14.1 Container Strategy

Recommended containers:

| Container | Purpose |
|---|---|
| `raw-uploads` | Original uploaded documents |
| `processed-documents` | Normalized/processed documents |
| `extraction-results` | Extraction outputs where file-based storage is needed |
| `exports` | Claim packs, PDF/CSV/JSON exports |
| `knowledge-base` | Approved policy/SOP/reference documents |
| `quarantine` | Suspicious, failed, or blocked uploads |

### 14.2 Storage Rules

- Store originals immutably where practical.
- Store metadata in Azure SQL.
- Use tenant/workspace prefixes or containers where appropriate.
- Encrypt at rest.
- Restrict access through managed identities.
- Avoid exposing direct public blob URLs.
- Use short-lived SAS URLs only where absolutely required.
- Log every document access.

---

## 15. AI and Document Provider Deployment

### 15.1 Azure AI Document Intelligence

Deployment must configure:

- Resource endpoint.
- Supported model IDs.
- Document type mappings.
- Confidence thresholds.
- Failure behavior.
- Retry settings.
- Human review routing for low confidence.

### 15.2 Azure AI Search

Deployment must configure:

- Search service.
- Indexes for approved knowledge.
- Indexes for claim/document evidence where allowed.
- Tenant/workspace filters.
- Access controls.
- Index refresh jobs.
- Rebuild process.

### 15.3 Azure OpenAI

Deployment must configure:

- Resource endpoint.
- Model deployments.
- Embeddings deployment.
- API version.
- Rate limits/quotas.
- Prompt templates.
- Provider health checks.
- Content safety/failure handling.
- Logging of prompt version, evidence IDs, model metadata, and output metadata.

### 15.4 AI Safety Deployment Rules

- AI must not answer policy/coverage questions from model memory.
- AI must retrieve approved tenant knowledge first.
- AI must show uncertainty where evidence is missing.
- AI must not approve/reject claims.
- AI must not accuse customers of fraud.
- AI must not promise settlement.
- AI outputs affecting claims/customers must be logged.

---

## 16. Integration Deployment

### 16.1 MVP Integration Order

Recommended integration deployment order:

1. Internal API and database.
2. Blob Storage document upload.
3. Azure AI Document Intelligence.
4. Azure AI Search and knowledge indexing.
5. Azure OpenAI summary and assistant providers.
6. Microsoft Graph/Outlook ingestion where configured.
7. WhatsApp Business channel provider.
8. Controlled exports/API payloads for downstream systems.
9. Future policy/claims core system connectors.

### 16.2 Microsoft Graph / Outlook

Deployment must configure:

- App registration.
- Mailbox access model.
- Admin consent if required.
- Least-privilege scopes.
- Subscription/webhook lifecycle if used.
- Email ingestion rules.
- Attachment handling.
- Audit logging.
- Failure and retry behavior.

### 16.3 WhatsApp Business

Deployment must configure:

- Provider selection: direct Meta, Twilio, Infobip, or other.
- Webhook endpoint.
- Signature validation.
- Access tokens/secrets.
- Phone number ID.
- Message templates.
- Consent and disclosure text.
- Retry behavior.
- Human handoff route.
- Channel health status.

### 16.4 Core System Integration

For MVP, core system integration should be conservative.

Preferred initial patterns:

- Read-only API lookup.
- Secure CSV/Excel import.
- SFTP import.
- Manual upload.
- Controlled export.
- Human-approved write-back only.

Do not provide unrestricted generic database access to AI agents.

---

## 17. Hybrid Cloud Mirror / Resilient Channel Buffer

Client discussions highlighted a concern around internet outages affecting WhatsApp and customer-facing channels.

The MVP should not promise full offline platform capability. Instead, document and deploy a **resilient channel buffer** where required.

### 17.1 MVP Resilience Pattern

```text
External channel event received
↓
Webhook validates and acknowledges event
↓
Event is written to durable queue
↓
Processing worker handles event asynchronously
↓
Failures retry with backoff
↓
Failed events move to dead-letter queue
↓
Support/admin sees failed provider status
```

### 17.2 Optional Local/Branch Buffer

For a customer that requires local resilience:

- Local webhook receiver may acknowledge events.
- Local queue may persist messages temporarily.
- Cloud sync resumes when connectivity returns.
- Message expiry and retry policy must be defined.
- Security and key management must be reviewed.

This should be treated as an advanced deployment option, not the default MVP requirement.

---

## 18. Release Management

### 18.1 Release Types

| Release Type | Description | Approval |
|---|---|---|
| Development release | Feature integration | Engineering |
| Staging release | Candidate build for QA/UAT | Engineering + QA |
| Pilot release | Customer-facing pilot deployment | Product + QA + Security + Delivery |
| Hotfix | Urgent fix | Engineering Lead + Product + Support |
| Infrastructure release | Azure resource or config change | Cloud + Security |
| Data migration release | Schema/data change | Engineering + DBA/Cloud + QA |

### 18.2 Release Artifacts

Each pilot/production release should include:

- Version number.
- Release notes.
- Deployment checklist.
- Migration summary.
- Changed endpoints/features.
- Known issues.
- Rollback plan.
- Smoke test results.
- Security review notes where relevant.
- Support handover notes.

### 18.3 Release Naming

Recommended format:

```text
iip-mvp-v{major}.{minor}.{patch}-{environment}
```

Examples:

```text
iip-mvp-v1.0.0-staging
iip-mvp-v1.0.0-pilot
iip-mvp-v1.0.1-hotfix
```

---

## 19. Pre-Deployment Checklist

Before any staging or production deployment, confirm:

### 19.1 Product and Scope

- [ ] Product name is **Insurance Intelligence Platform (IIP)**.
- [ ] MVP scope is Motor Claims and Document Intelligence.
- [ ] Phase 2 features are not accidentally exposed as production-ready.
- [ ] Human approval gates are enabled.
- [ ] No autonomous regulated decisions are enabled.

### 19.2 Environment

- [ ] Correct environment selected.
- [ ] Resource group exists.
- [ ] Region selected and approved.
- [ ] Required Azure resources provisioned.
- [ ] Tags applied.
- [ ] Cost alerts configured.

### 19.3 Security

- [ ] Entra ID configuration verified.
- [ ] API auth enforced.
- [ ] Tenant/workspace checks enforced.
- [ ] Key Vault configured.
- [ ] No secrets in repo or pipeline logs.
- [ ] Managed identities configured.
- [ ] HTTPS enforced.
- [ ] CORS restricted.
- [ ] Audit logging enabled.

### 19.4 Data

- [ ] Database migrations reviewed.
- [ ] Backup completed where needed.
- [ ] Tenant seed data verified.
- [ ] Workspace seed data verified.
- [ ] No real PII in non-approved environments.
- [ ] Document containers configured.

### 19.5 Providers

- [ ] Azure OpenAI configured or marked Not configured.
- [ ] AI Search configured or marked Not configured.
- [ ] Document Intelligence configured or marked Not configured.
- [ ] Microsoft Graph configured or marked Not configured.
- [ ] WhatsApp configured or marked Not configured.
- [ ] Provider health page works.

### 19.6 QA

- [ ] Unit tests pass.
- [ ] Integration tests pass.
- [ ] Smoke tests pass.
- [ ] Auth tests pass.
- [ ] Document upload tests pass.
- [ ] AI guardrail tests pass.
- [ ] Audit logging tests pass.
- [ ] UI loading/empty/error/unauthorized states verified.

---

## 20. Deployment Execution Plan

### 20.1 Standard Deployment Sequence

```text
1. Freeze release candidate
2. Run automated tests
3. Build frontend artifact
4. Build backend artifact
5. Build worker artifacts
6. Validate IaC plan
7. Apply infrastructure changes if required
8. Run database migrations
9. Deploy backend API
10. Deploy workers/functions
11. Deploy frontend
12. Update environment configuration
13. Restart services if needed
14. Run smoke tests
15. Verify provider health
16. Verify audit logging
17. Verify key user journeys
18. Monitor errors and performance
19. Approve release
20. Notify stakeholders
```

### 20.2 First Pilot Production Deployment Sequence

For first pilot production:

1. Confirm pilot customer and deployment model.
2. Confirm Azure tenant/subscription and region.
3. Confirm legal/security approval for deployment.
4. Provision resource group and base Azure resources.
5. Configure identity and app registrations.
6. Configure database and storage.
7. Configure Key Vault and managed identities.
8. Deploy backend APIs.
9. Deploy workers/functions.
10. Deploy frontend.
11. Configure tenant/workspace/roles.
12. Configure providers in disabled or test mode first.
13. Enable provider integrations one by one.
14. Load approved knowledge base.
15. Load synthetic or approved test claim packs.
16. Run QA smoke tests.
17. Run UAT with pilot users.
18. Fix critical issues.
19. Enable production pilot mode.
20. Monitor closely during first operating week.

---

## 21. Post-Deployment Validation

Post-deployment validation must confirm that the system works end to end.

### 21.1 Smoke Test Checklist

- [ ] User can sign in.
- [ ] `/api/v1/me` returns correct user and roles.
- [ ] Command Center loads.
- [ ] Channels Inbox loads.
- [ ] Customers workspace loads.
- [ ] Sales and Leads workspace loads.
- [ ] Claims queue loads.
- [ ] New claim can be created.
- [ ] Document can be uploaded.
- [ ] Document processing event is queued.
- [ ] Extraction result is saved.
- [ ] AI claim summary can be generated when provider is configured.
- [ ] Low-confidence extraction routes to review.
- [ ] Audit event is recorded.
- [ ] Unauthorized user is blocked.
- [ ] Provider health page shows configured/not configured states.

### 21.2 User Journey Validation

Validate the MVP journey:

```text
Customer/channel sends motor claim input
↓
IIP creates channel conversation or FNOL record
↓
Claim work item is created
↓
Documents are uploaded
↓
Document Intelligence extracts fields
↓
Missing-document checklist is generated
↓
Claims officer reviews extraction
↓
AI summary is generated with evidence
↓
Fraud signal is advisory only
↓
Human approves next workflow action
↓
Customer-safe update is drafted/approved
↓
Audit trail is visible
↓
Claim pack can be exported
```

---

## 22. Rollback and Recovery Plan

### 22.1 Rollback Principles

- Rollback must be planned before deployment.
- Database migrations require special care.
- Rollback should prefer restoring previous app version where schema remains compatible.
- Destructive migrations must be avoided in early pilot releases.
- All production releases must have backup or restore points.

### 22.2 Rollback Scenarios

| Scenario | Response |
|---|---|
| Frontend deployment breaks UI | Revert frontend artifact to previous version |
| Backend API fails after deploy | Revert API artifact if database compatible |
| Database migration fails | Stop deployment, restore backup or apply corrective migration |
| AI provider misconfigured | Disable provider and show Not configured/provider error state |
| Document processing fails | Pause queue workers, fix provider/config, replay messages |
| WhatsApp webhook fails | Disable webhook or route to fallback, inspect dead-letter queue |
| Auth failure | Roll back app registration/config, use break-glass admin procedure if approved |
| Tenant isolation bug | Disable affected environment immediately and investigate |

### 22.3 Backup Requirements

Before production changes:

- Azure SQL backup/restore point confirmed.
- Blob Storage retention/versioning reviewed.
- Key Vault recovery enabled.
- IaC state backed up where applicable.
- Release artifact retained.
- Config snapshot retained.

---

## 23. Monitoring and Observability

### 23.1 Required Telemetry

IIP must collect:

- API request metrics.
- API error rates.
- Latency.
- Authentication failures.
- Tenant/workspace access denials.
- Document upload counts.
- Document processing success/failure.
- Queue depth.
- Dead-letter counts.
- AI provider calls.
- AI provider errors.
- Token/cost estimates where available.
- Search query errors.
- Graph/WhatsApp integration failures.
- Audit event write failures.
- Database performance.

### 23.2 Dashboards

Deployment should provide dashboards for:

- Platform health.
- Provider health.
- Queue health.
- API health.
- Document processing health.
- AI provider health.
- Integration health.
- Security events.
- Cost and usage.
- User activity.

### 23.3 Alerts

Minimum alerts:

- API error rate above threshold.
- API unavailable.
- Database unavailable.
- Queue backlog too high.
- Dead-letter queue non-zero beyond threshold.
- AI provider failures.
- Document Intelligence failures.
- Blob Storage failures.
- Audit logging failures.
- Unusual authentication failures.
- Cost budget threshold exceeded.

---

## 24. Security Deployment Gates

A release cannot go to pilot production unless these gates pass:

| Gate | Requirement |
|---|---|
| Authentication | Entra ID login and token validation work |
| Authorization | Backend enforces roles, tenant, workspace, object scope |
| Secrets | No secrets in repo, logs, frontend bundle, or pipeline output |
| HTTPS | HTTPS enforced for all endpoints |
| Audit | Audit events written for sensitive actions |
| Human review | Regulated decisions require human approval |
| Tenant isolation | Cross-tenant access blocked |
| Provider config | Missing providers fail safely |
| Logging | No unnecessary PII in logs |
| Backup | Production restore path confirmed |
| Incident contacts | Support and escalation contacts defined |

---

## 25. Compliance Deployment Gates

Before pilot production:

- [ ] Data Protection and Privacy Act implications reviewed.
- [ ] Customer consent path reviewed.
- [ ] WhatsApp disclosure wording reviewed.
- [ ] Claims audit trail reviewed.
- [ ] Claims register/export needs reviewed.
- [ ] Cross-border processing reviewed.
- [ ] Data retention assumptions documented.
- [ ] Legal review gaps listed.
- [ ] AI limitation wording included.
- [ ] Human owner for every regulated decision defined.

This deployment plan is not legal advice. Legal and regulatory review remains required before formal production usage with real customer data.

---

## 26. Cost Management

### 26.1 Cost Drivers

Primary cost drivers:

- Azure OpenAI tokens.
- Document Intelligence pages processed.
- AI Search service tier.
- Azure SQL tier.
- Blob Storage volume.
- App hosting tier.
- Service Bus usage.
- Log Analytics ingestion.
- WhatsApp provider charges.
- Data transfer.

### 26.2 Cost Controls

- Configure Azure budgets.
- Configure cost alerts.
- Track per-tenant usage.
- Track per-module usage.
- Track documents processed.
- Track AI calls/tokens.
- Limit non-production provider usage.
- Use quotas and throttles.
- Archive old logs according to retention policy.

### 26.3 Pilot Cost Policy

Pilot deployment should define:

- Included claims volume.
- Included document volume.
- Included AI usage.
- WhatsApp cost handling.
- Overage handling.
- Support coverage.
- Azure resource ownership.

---

## 27. Tenant Onboarding Plan

### 27.1 Onboarding Steps

For each insurer tenant:

1. Create tenant record.
2. Create workspace(s).
3. Configure country, currency, and timezone.
4. Configure product lines.
5. Configure claim types.
6. Configure motor claim document checklist.
7. Configure roles and permissions.
8. Invite staff users.
9. Configure channels.
10. Configure document storage.
11. Upload approved knowledge documents.
12. Build/search indexes.
13. Configure provider settings.
14. Validate sample claim journey.
15. Enable pilot users.

### 27.2 Required Tenant Defaults

- Tenant name.
- Tenant ID.
- Primary workspace.
- Country: Uganda for first pilot unless otherwise specified.
- Currency: UGX unless customer requires another.
- Timezone: Africa/Kampala.
- Claim type: Motor.
- Roles: Admin, Claims Officer, Claims Supervisor, Customer Service Agent, Compliance Reviewer, Underwriter, Otic Support.
- Channels: WhatsApp, Email, Broker, Call Center, Web Form as configured.

---

## 28. Support Handover

Before go-live, support teams must receive:

- Environment URLs.
- Support contacts.
- Escalation matrix.
- Known issues.
- Runbook links.
- Provider health page location.
- Log Analytics workspace access or dashboards.
- Incident severity definitions.
- Rollback contact list.
- Customer admin contact list.
- Azure resource inventory.
- Release notes.

### 28.1 Support Tiers

| Tier | Responsibility |
|---|---|
| Level 1 | Basic user support, access issues, known workaround guidance |
| Level 2 | Product support, workflow issues, provider configuration, data checks |
| Level 3 | Engineering support, API failures, deployments, data repair |
| Cloud/Security | Azure incidents, secrets, identity, networking, security events |

---

## 29. Incident Response During Deployment

### 29.1 Incident Severity

| Severity | Example | Response |
|---|---|---|
| Sev 1 | Platform unavailable, tenant data exposure, auth failure, audit logging down | Immediate escalation, possible rollback/disablement |
| Sev 2 | Claims workflow blocked, document processing down, provider outage | Urgent fix or fallback |
| Sev 3 | Non-critical UI bug, slow provider, limited feature degradation | Scheduled fix |
| Sev 4 | Cosmetic issue, documentation issue | Backlog |

### 29.2 Incident Workflow

```text
Detect incident
↓
Classify severity
↓
Notify owner and stakeholders
↓
Stabilize environment
↓
Apply rollback/fix/fallback
↓
Capture timeline and evidence
↓
Communicate status
↓
Perform root cause analysis
↓
Create prevention actions
```

---

## 30. Disaster Recovery and Business Continuity

### 30.1 MVP DR Requirements

MVP should define:

- Azure SQL backup and restore process.
- Blob Storage recovery/retention.
- Key Vault soft delete and purge protection.
- IaC re-deployment path.
- Release artifact retention.
- Provider fallback behavior.
- Queue replay process.
- Manual operating procedure if key providers fail.

### 30.2 Recovery Objectives

Initial targets should be defined by leadership and customer expectations. Until confirmed, use working planning assumptions:

| Metric | Planning Assumption |
|---|---|
| RTO | Same business day for MVP pilot |
| RPO | Less than 24 hours for database restore; lower if customer requires |
| Critical data | Claims, documents, audit logs, users, tasks, provider events |
| Manual fallback | Customer communication and claims handling can be performed manually during outage |

These are planning assumptions, not final SLA commitments.

---

## 31. Performance and Scale Gates

Before pilot production, validate:

- Command Center loads within acceptable time.
- Claims queue can handle expected pilot volume.
- Document upload handles expected file sizes.
- Document processing queue drains under expected volume.
- AI responses fail gracefully under provider latency.
- Search results return within acceptable time.
- Audit logging does not block core actions excessively.
- Pagination exists for large lists.
- Background jobs are idempotent where possible.

### 31.1 Initial Pilot Scale Assumptions

| Area | Planning Assumption |
|---|---|
| Users | 25-100 internal staff for pilot |
| Claims | 50-500 claims/month for controlled pilot |
| Documents | 5-10 documents per motor claim |
| Channels | WhatsApp/email/web/broker/call-center inputs |
| AI volume | Summary/extraction/retrieval on demand and workflow events |

Final volumes must be confirmed with the pilot insurer.

---

## 32. Deployment RACI

| Activity | Product | Engineering | Cloud/DevOps | Security | QA | Delivery | Customer |
|---|---|---|---|---|---|---|---|
| Scope confirmation | A | C | C | C | C | R | C |
| Infrastructure provisioning | C | C | A/R | C | I | I | C |
| App deployment | C | A/R | R | C | C | I | I |
| Database migration | I | A/R | R | C | C | I | I |
| Provider configuration | C | R | A/R | C | C | C | C |
| Security review | C | C | C | A/R | C | I | C |
| QA validation | C | C | C | C | A/R | C | C |
| UAT coordination | R | C | I | I | C | A/R | A/R |
| Go-live approval | A | C | C | C | C | R | A/R |
| Incident response | C | R | R | A/R | C | C | C |

Legend: A = Accountable, R = Responsible, C = Consulted, I = Informed.

---

## 33. Deployment Timeline - Recommended Sequence

### Phase 0 - Readiness

- Confirm product name and MVP scope.
- Confirm pilot customer or target pilot assumptions.
- Confirm deployment model.
- Confirm Azure region.
- Confirm WhatsApp provider.
- Confirm legal review path.

### Phase 1 - Foundation Deployment

- Provision base Azure resources.
- Configure Entra ID.
- Configure Key Vault.
- Deploy frontend and backend skeleton.
- Deploy database.
- Deploy monitoring.
- Configure health endpoints.

### Phase 2 - Core MVP Deployment

- Deploy Command Center.
- Deploy Channels, Customers, Sales & Leads.
- Deploy Claims Work Queue.
- Deploy FNOL and Case Review.
- Deploy Document upload and processing.
- Deploy audit logging.
- Configure provider health.

### Phase 3 - AI and Integration Enablement

- Configure Document Intelligence.
- Configure Azure AI Search.
- Configure Azure OpenAI.
- Load approved knowledge base.
- Configure Microsoft Graph.
- Configure WhatsApp sandbox/test.
- Validate end-to-end motor claim journey.

### Phase 4 - Pilot Hardening

- Run QA test plan.
- Run security gates.
- Run compliance checklist.
- Run UAT.
- Fix critical issues.
- Prepare support handover.
- Prepare release notes.

### Phase 5 - Pilot Go-Live

- Enable pilot users.
- Enable production provider configuration.
- Monitor first claims.
- Track incidents.
- Run daily deployment/ops review for initial pilot period.
- Capture pilot metrics.

---

## 34. Open Decisions

| ID | Decision | Owner | Priority |
|---|---|---|---|
| D1 | Confirm first pilot deployment model: Otic-hosted SaaS, private deployment, or Managed Application path | Leadership / Cloud | Critical |
| D2 | Confirm Azure region and required model availability | Cloud / Legal | Critical |
| D3 | Confirm WhatsApp provider: direct Meta, Twilio, Infobip, or other | Engineering / Product | Critical |
| D4 | Confirm pilot customer and real insurer systems | Sales / Leadership | Critical |
| D5 | Confirm whether Microsoft Graph is required in pilot | Product / Engineering | High |
| D6 | Confirm whether core policy system lookup is MVP or Phase 2 | Product / Customer | High |
| D7 | Confirm data residency and cross-border processing position | Legal / Compliance | High |
| D8 | Confirm cost ownership for Azure resources | Finance / Leadership | High |
| D9 | Confirm support access model in customer tenant | Security / Customer | High |
| D10 | Confirm Managed Application timeline | Leadership / Cloud | Medium |

---

## 35. Deployment Acceptance Criteria

A deployment is acceptable for pilot when:

1. The app deploys successfully to the target environment.
2. Users can authenticate through the approved identity path.
3. Tenant/workspace/role enforcement works server-side.
4. Command Center loads with real API responses.
5. Channels, Customers, Sales & Leads, and Claims modules load.
6. New motor claim intake can be created.
7. Documents can be uploaded and stored securely.
8. Document processing can run or fail safely with visible status.
9. AI summaries use configured providers or display safe Not configured states.
10. Human review and approval gates are active.
11. Audit events are captured.
12. Provider health is visible.
13. Monitoring dashboards and alerts work.
14. Rollback plan is documented.
15. Security/compliance gates are passed or explicitly waived by leadership.
16. Support handover is complete.
17. Known issues are documented.

---

## 36. Appendix A - Environment Variable Categories

Do not treat this as a final key list. It is a category checklist for engineering.

| Category | Example Values |
|---|---|
| App environment | `APP_ENV`, `APP_VERSION`, `PUBLIC_APP_NAME` |
| API endpoints | `NEXT_PUBLIC_IIP_API_BASE_URL` |
| Auth | Entra tenant ID, client ID, authority, audience |
| Tenant defaults | Local/demo tenant and workspace IDs only |
| SQL | Connection string or managed identity config |
| Blob | Storage account and container settings |
| Service Bus | Namespace and queue names |
| AI Search | Endpoint, index names |
| Azure OpenAI | Endpoint, deployment names, API version |
| Document Intelligence | Endpoint, model IDs |
| Graph | Client ID/scopes/mailbox config |
| WhatsApp | Provider, webhook secret, phone number ID |
| Monitoring | App Insights / Log Analytics config |

---

## 37. Appendix B - MVP Deployment Checklist Summary

```text
Product scope confirmed
Environment selected
Azure region approved
Azure resources provisioned
Identity configured
Secrets in Key Vault
Database migrated
Blob containers configured
Service Bus queues configured
Frontend deployed
Backend deployed
Workers deployed
Providers configured or safely marked Not configured
Tenant/workspace seeded
Roles and permissions seeded
Knowledge base loaded
Test claim data loaded
Smoke tests passed
QA tests passed
Security gates passed
Compliance review gaps documented
Monitoring alerts enabled
Rollback plan ready
Support handover complete
Go-live approved
```

---

## 38. Final Deployment Position

The IIP deployment approach should be practical, controlled, and enterprise-ready.

For the MVP, the deployment should prove that IIP can securely support a motor claims and document intelligence workflow for an insurer without overcommitting to full marketplace packaging, full offline capability, or deep core-system automation before the product has validated its first operating workflow.

The recommended deployment position is:

> **Deploy IIP first as a controlled Azure-native pilot for Motor Claims and Document Intelligence, with real provider configuration, strong tenant/workspace security, human review gates, full auditability, and a clear path to enterprise private deployment and Azure Managed Application packaging.**

This keeps the platform credible for insurers, safe for regulated workflows, practical for engineering, and ready for future commercial scaling.
