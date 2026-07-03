# Contributing to Insurance Intelligence Platform (IIP)

Welcome to the team. This repository contains the source code and deployment assets for **Insurance Intelligence Platform (IIP)**, an Azure-first insurance operations platform designed for regulated enterprise insurance environments.

IIP is not a generic chatbot and must not be treated as a demo-only AI application. It is a document-first, workflow-first, human-supervised, auditable, tenant-isolated, insurer-grade platform intended to support production insurance operations and Azure Managed Application deployment.

This contributor guide explains how to work safely in the repository. It is a companion to the repository authority files and does not replace them.

---

## 0. Authority and Required Reading

Before opening a branch, implementing a task, or submitting a pull request, read these files:

1. `AGENTS.md`
2. `IIP_IMPLEMENTATION_GUARDRAILS.md`
3. `CLAUDE.md`
4. `README.md`
5. Any relevant architecture, deployment, security, UI, testing, or runbook document under `docs/`

`AGENTS.md` is the highest-authority repository contract for product scope, architecture, security, deployment, UI rules, Azure Managed Application readiness, Partner Center readiness, and production release gates.

`IIP_IMPLEMENTATION_GUARDRAILS.md` is the shared implementation guardrail contract for Codex, Claude, and any other coding or automation agent.

If this guide conflicts with `AGENTS.md`, follow `AGENTS.md` and update this guide later.

---

## 1. Product Naming Standard

Use the official product naming consistently:

| Context | Name to use |
|---|---|
| Formal product name | **Insurance Intelligence Platform (IIP)** |
| Short product name | **IIP** |
| Staff application name | **IIP Operations** |
| Codebase identifiers | Keep existing code identifiers such as `InsuranceIntelligence.sln`, `InsuranceIntelligence.Api`, and `INSURANCEINTELLIGENCE_*` |

Do not use `ACIP` as the official product name.

Do not rename code namespaces, solution files, environment variable prefixes, or infrastructure identifiers unless a maintainer explicitly approves a refactor.

---

## 2. Commit and Branch Etiquette

### 2.1 Conventional Commits

We use the [Conventional Commits specification](https://www.conventionalcommits.org/) to support changelog quality and release discipline.

Every commit message should follow this format:

```text
<type>(<scope>): <description>

[optional body]

[optional footer(s)]
```

Common types:

| Type | Use |
|---|---|
| `feat` | New feature or product capability |
| `fix` | Bug fix |
| `docs` | Documentation-only change |
| `style` | Formatting or styling change that does not alter behavior |
| `refactor` | Code change that neither fixes a bug nor adds a feature |
| `perf` | Performance improvement |
| `test` | Test addition or correction |
| `ci` | CI/CD configuration or script change |
| `chore` | Maintenance work that does not affect runtime behavior |
| `security` | Security hardening, vulnerability fix, or control improvement |

Examples:

```text
feat(auth): add Entra ID tenant registration flow
fix(claims): resolve SLA breach boundary check
security(api): reject demo headers outside LocalTest
```

### 2.2 Branch Naming Conventions

Create branches from `main` unless a maintainer instructs otherwise.

Use these patterns:

```text
feature/issue-<num>-<short-description>
bugfix/issue-<num>-<short-description>
security/issue-<num>-<short-description>
refactor/<short-description>
docs/<short-description>
release/<version-or-gate>
```

Examples:

```text
feature/issue-102-azure-search-rag
security/issue-118-disable-demo-auth-staging
docs/update-managed-app-runbook
```

### 2.3 Linear Git History

- Do not create unnecessary merge commits in feature branches.
- Prefer rebasing on `main` where appropriate.
- Pull requests may be squash-merged into `main` to keep history clean.
- Do not rewrite shared branch history unless the team has agreed.

---

## 3. Runtime Profiles and Zero-Simulation Rule

IIP distinguishes local test execution from deployed runtime. Contributors must preserve this boundary.

| Runtime profile | Purpose | Test doubles allowed? | Customer-facing / deployable? |
|---|---|---:|---:|
| `LocalTest` | Automated tests and isolated local verification | Yes | No |
| `Development` | Internal development only | Limited and explicit | No |
| `Staging` | Production-candidate validation using synthetic data | No | Yes, internal validation only |
| `Production` | Live insurer/customer operation | No | Yes |
| `ManagedApplicationReviewer` | Partner Center/private-plan review deployment | No | Yes |

In `Staging`, `Production`, `ManagedApplicationReviewer`, customer-facing deployments, insurer-facing deployments, or publicly reachable environments, the following are forbidden:

- mock providers;
- simulated providers;
- fake send behavior;
- fake ingestion behavior;
- fake dashboards or hardcoded operational metrics;
- seeded demo business data;
- in-memory persistence;
- SQLite persistence;
- Development Auth Mode;
- demo headers as authority;
- hardcoded insurer, mailbox, user, case, claim, document, approval, or dashboard records;
- fallback from a real provider to a mock provider;
- UI pathways that make an unconfigured capability appear connected or successful.

If a real capability is missing or not configured, display `Not configured`, return a safe readiness failure, or block the release gate. Do not fake success.

---

## 4. Mock, Simulated, and Test Double Policy

### 4.1 Allowed Uses

Test doubles may exist only for:

- automated unit tests;
- integration tests using synthetic fixtures;
- isolated `LocalTest` execution;
- local developer verification that is not deployed and cannot be enabled in customer/runtime profiles.

### 4.2 Forbidden Uses

Mocks, simulations, fake providers, placeholder adapters, seeded demo behavior, or development-only shortcuts must not be registered or selectable in deployed runtime profiles.

Do not reference test-double classes from production dependency injection pipelines.

Do not make production behavior depend on fake data, fallback mocks, local file storage, or hardcoded dashboard values.

### 4.3 Compilation and Warning Discipline

If simulated classes are retained, keep them clearly isolated by namespace, folder, profile, and dependency injection boundaries.

Expected warnings related to obsolete simulated classes are acceptable only in test or explicitly isolated local-test targets. Production builds and release pipelines should not rely on obsolete simulation paths.

---

## 5. Product Guardrails

Every contribution must preserve these non-negotiable product rules:

1. **Human-supervised decisions:** AI may draft, summarize, recommend, classify, route, or flag. Humans must make consequential insurance decisions.
2. **Tenant isolation:** Every query, service invocation, document, index item, audit event, integration event, and write must respect tenant and workspace boundaries.
3. **Grounding and evidence:** Policy and operational AI answers must use approved sources, evidence references, citations, and confidence/uncertainty handling.
4. **Auditability:** Important human, AI, integration, workflow, and security actions must produce audit evidence.
5. **Customer-safe boundaries:** Customer-facing surfaces must not expose internal notes, fraud flags, reserves, prompts, system messages, secrets, or confidential staff-only content.
6. **No autonomous adverse action:** The platform must not autonomously deny claims, repudiate claims, approve settlements, release payments, bind policies, change premiums, accuse fraud, or issue final regulated communications.
7. **No AI direct database access:** AI agents must operate through approved services, retrieval layers, typed tools, and audited workflows, not direct production database queries.

---

## 6. Architecture Contribution Rules

IIP is Azure-first and Microsoft-native.

Preferred services include:

- Microsoft Entra ID for authentication;
- Managed Identity for service-to-service access;
- Azure Key Vault for secrets;
- Azure App Service or Azure Container Apps for APIs and backend services;
- Azure Functions or Container Apps Jobs for ingestion/background work;
- Azure API Management for external integration APIs;
- Azure Service Bus for asynchronous workflow messages;
- Azure Blob Storage or Data Lake Storage for documents and file landing zones;
- Azure SQL Database for normalized application state;
- Azure AI Document Intelligence for document extraction;
- Azure AI Search for retrieval and RAG grounding;
- Azure OpenAI or approved Microsoft Foundry deployments for AI features;
- Application Insights and Log Analytics for monitoring.

Do not introduce new infrastructure, frameworks, cloud services, queues, databases, orchestration systems, or AI runtimes without a clear task requirement and maintainer approval.

Keep domain logic free of Azure SDK dependencies. Use interfaces and infrastructure adapters for Azure services.

---

## 7. Azure Managed Application and Partner Center Rules

IIP is intended for Azure Managed Application and private-plan readiness.

Infrastructure changes must preserve:

- Bicep authoring;
- ARM JSON output;
- `createUiDefinition.json`;
- `viewDefinition.json` where used;
- `managedapp.zip` packaging requirements;
- real-provider-only staging/production/reviewer deployment defaults;
- customer-owned data boundaries;
- controlled publisher support access;
- least-privilege managed identity and Azure RBAC posture.

Do not expose mock provider selection or demo behavior in Managed Application deployment UI or customer/reviewer parameters.

When changing infrastructure, update the relevant deployment, release, packaging, or validation documentation.

Where applicable, validate:

```bash
az bicep build --file infra/mainTemplate.bicep --outfile infra/dist/mainTemplate.json
```

If Azure CLI is unavailable, document that clearly in the PR notes.

---

## 8. Authentication and Authorization Rules

All authenticated application access must use Microsoft Entra ID unless explicitly isolated to local test/development.

Rules:

- Do not bypass authentication in staging, production, reviewer, or insurer-facing environments.
- Do not add fake users, fake roles, mock JWTs, or mock claims to deployed runtime code.
- Do not silently fall back to anonymous access.
- Do not disable JWT validation to make a screen load.
- Do not weaken issuer, audience, tenant, expiry, or signature validation.
- Keep frontend MSAL configuration aligned with backend JWT validation.
- Validate `/api/v1/me` or the current authenticated-user endpoint before relying on authenticated UI behavior.
- Enforce RBAC server-side, not only in the browser.
- UI role checks may hide navigation, but APIs must still enforce permissions.
- Every tenant-scoped API request must validate tenant access on the server.

If authentication fails, diagnose and fix the real cause. Do not patch around it.

---

## 9. Tenant Isolation Rules

Every tenant-scoped table, document, index record, workflow, integration event, audit event, AI action, and communication must include tenant context.

Rules:

- Never return data without tenant and workspace filtering.
- Never trust tenant IDs from the client without validating the authenticated user's access.
- Never allow cross-tenant search results.
- Never allow cross-tenant document access.
- Never allow cross-tenant workflow actions.
- Never allow one tenant's configuration, connectors, documents, prompts, or audit logs to leak into another tenant.
- Prefer explicit `TenantScope` / tenant-workspace enforcement in repositories, services, queries, policies, and AI tools.

When uncertain, fail closed.

---

## 10. Data and Migration Rules

Use a clean canonical data model. Important entities include:

- Tenant;
- Workspace;
- User;
- Role;
- Customer;
- Policy;
- Claim / Case;
- Document;
- DocumentExtraction;
- Interaction / Communication;
- Task;
- Approval;
- ConsentRecord;
- AuditEvent;
- IntegrationEvent;
- WriteBackRequest;
- AgentRun / AIInteraction;
- KnowledgeDocument / KnowledgeItem.

Database rules:

- Do not drop tables or columns without explicit approval.
- Do not destroy data.
- Prefer additive migrations.
- Include tenant/workspace IDs where required.
- Include audit fields where required.
- Use indexes for tenant-scoped queries and common lookups.
- Avoid storing large documents directly in relational tables; use Blob Storage and store references.
- Production mode must use explicit migration/deployment steps; do not rely on automatic production database migration at app startup unless explicitly approved.

---

## 11. AI and Agent Contribution Rules

AI agents must be grounded, constrained, and auditable.

Rules:

- AI must retrieve from approved tenant/workspace-scoped knowledge where policy wording or claim guidance matters.
- AI must not answer policy coverage questions from model memory.
- AI must not fabricate claim facts, policy details, customer data, payments, obligations, limits, dates, or compliance conclusions.
- AI must distinguish between source-backed facts, assumptions, uncertainty, and recommendations.
- AI-generated summaries, recommendations, and actions must be logged.
- AI tool calls must have typed input/output contracts.
- Structured model outputs must be validated before storage or display.
- Low-confidence, missing-evidence, expired-evidence, or conflicting-evidence cases must escalate to humans.

For MVP and production workflows, AI must not automatically:

- approve claims;
- reject claims;
- make binding underwriting decisions;
- trigger irreversible customer-impacting actions;
- send sensitive customer communications without approval;
- modify source systems without approved write-back;
- accuse customers, providers, brokers, or staff of fraud.

---

## 12. Frontend and IIP Operations UI Rules

IIP Operations is an enterprise insurance operations workspace with embedded AI assistance. It is not a generic chatbot-first application.

Before changing frontend pages, routes, components, layout, styling, role-aware navigation, AI surfaces, customer-safe surfaces, integration states, approvals, or API-to-UI mappings, read:

- `docs/ui/IIP_Operations_UI_Design_Specification_MVP.md`
- `docs/ui/reference/IIP_Global_Theme_Approved.png`

Frontend rules:

- Follow the approved Light Intelligence Workspace Theme.
- Use bright white and soft-lavender workspace surfaces.
- Use deep ink-navy typography.
- Use restrained indigo-to-violet brand and AI accents.
- Use a light sidebar and light header for MVP.
- Use structured right-side execution or AI insight rails.
- Use compact operational tables.
- Use subtle borders, shadows, and rounded cards.
- Do not introduce a dark sidebar in MVP unless an approved spec update allows it.
- Do not improvise a new color palette.
- Do not hide backend failures behind fake data.
- Do not show unavailable integrations as connected.
- Use `Not configured` for unconfigured real capabilities.
- Use proper loading, empty, error, unauthorized, and `Not configured` states.
- Enforce permissions on the backend; frontend role checks are only a usability layer.
- Render untrusted email, document, customer, communication, and AI content safely.

Before declaring frontend work complete, check for:

- text overflow;
- clipped labels;
- overlapping cards or controls;
- broken responsive layout;
- incorrect z-index or modal behavior;
- unreadable tables;
- inconsistent loading/empty/error states;
- browser console errors;
- accessibility issues in labels, focus, keyboard navigation, and contrast.

---

## 13. Integration Contribution Rules

Design integrations around interfaces, adapters, events, and feature flags.

Preferred connector types:

- API connector;
- webhook/event connector;
- SFTP/file connector;
- database read connector;
- document connector;
- third-party REST connector;
- manual upload connector.

Rules:

- Avoid direct write access to customer production systems unless explicitly implemented through approved write-back flows.
- Prefer APIs, events, files, or read-only database views.
- Validate all inbound data.
- Log all integration events.
- Store raw payloads separately from normalized records where appropriate.
- Make mapping rules explicit and reviewable.
- Do not allow integrations to bypass tenant boundaries.
- Do not expose secrets in connector configuration screens or logs.
- Use idempotency for duplicate webhooks, mailbox ingestion, and file-processing events.

---

## 14. Security and Secrets Rules

Never commit secrets.

Never commit:

- client secrets;
- API keys;
- connection strings;
- SAS tokens;
- storage keys;
- tenant secrets;
- access tokens;
- refresh tokens;
- certificates/private keys;
- live customer data;
- real policyholder data;
- real medical, police, payment, or claim evidence;
- real national IDs or other sensitive personal data.

Use:

- Azure Key Vault;
- Managed Identity;
- environment variables;
- secure app configuration;
- local user secrets for development only;
- secured pipeline variables.

If a secret is accidentally committed, notify the team immediately. The credential must be rotated, logs reviewed, and repository history handled according to the team incident process.

---

## 15. Testing and Validation Rules

Every meaningful implementation should include tests or a clear reason why tests were not added.

Prioritize tests for:

- authentication;
- authorization;
- tenant isolation;
- data validation;
- integration ingestion;
- field mapping;
- document processing;
- AI grounding boundaries;
- human approval workflows;
- write-back requests;
- audit logging;
- UI rendering safety;
- error states;
- API contract stability;
- zero-simulation enforcement in deployed profiles.

Before completing work, run the relevant commands where available:

```bash
# backend
dotnet restore InsuranceIntelligence.sln
dotnet build InsuranceIntelligence.sln --no-restore
dotnet test InsuranceIntelligence.sln
dotnet list InsuranceIntelligence.sln package --vulnerable --include-transitive

# portal
cd web/portal
pnpm install --frozen-lockfile
pnpm audit
pnpm lint
pnpm build
cd ../..

# infrastructure
az bicep build --file infra/mainTemplate.bicep --outfile infra/dist/mainTemplate.json
```

If a command cannot be run, state exactly why in the PR or completion report.

Do not claim validation passed when it was not run.

---

## 16. Pull Request Process

### 16.1 Before Opening a PR

Before requesting review:

1. Read the relevant authority documents.
2. Confirm the task scope and release gate.
3. Build and test the changed areas.
4. Check security, tenant isolation, and customer-safe boundaries.
5. Update docs/runbooks if behavior changed.
6. Confirm no secrets or real sensitive data are included.
7. Confirm deployed runtime paths do not activate mock/simulation/fake behavior.
8. Capture screenshots for UI changes where required.

### 16.2 PR Requirements

Each PR should include:

- summary of changes;
- files/routes/modules affected;
- security impact;
- tenant isolation impact;
- authentication/authorization impact;
- data model or migration impact;
- AI/agent impact;
- UI impact, if applicable;
- tests added or updated;
- commands run and results;
- known limitations or blockers;
- manual Azure/Microsoft 365/Partner Center actions required.

### 16.3 Review Expectations

- Every PR requires at least one approved review from a maintainer.
- Security-sensitive, tenant-isolation, identity, infrastructure, or release-gate changes may require senior maintainer review.
- Address all comment threads.
- Do not resolve critical design/security threads yourself unless the reviewer agrees.
- Keep communication professional, specific, and evidence-based.

---

## 17. Documentation Requirements

Update documentation when behavior changes.

Depending on the change, update relevant documents under:

```text
docs/
  strategy-requirements-roadmap/
  development-engineering/
  design-architecture/
  testing-security-quality/
  deployment-release-enablement/
  client-pilot/
  client-support/
  continuous-improvement/
  commercial-executive/
  ui/
  security/
  deployment/
  production/
  runbooks/
  adr/
```

For significant architecture decisions, add or update ADRs under `docs/adr/`.

Do not leave README, AGENTS, guardrails, UI specs, API specs, deployment docs, or runbooks inconsistent with implemented behavior.

---

## 18. Absolute Do-Not-Do List

Do not:

- hardcode secrets;
- disable authentication;
- bypass authorization;
- trust browser-only permissions;
- remove tenant filtering;
- allow AI to directly query production databases;
- allow automatic claim approval or rejection;
- allow automatic fraud accusation;
- allow unapproved write-back;
- skip audit logging for important actions;
- mix tenant data;
- hide real errors with fake success states;
- introduce mock auth into staging, production, reviewer, or customer-facing runtime;
- log access tokens or secrets;
- send sensitive data to unapproved external services;
- drop production data;
- rewrite unrelated systems unnecessarily;
- add unnecessary dependencies;
- ignore failed tests;
- claim validation passed when it did not run;
- claim production readiness without evidence;
- expose unconfigured integrations as functioning;
- ship a UI that relies on hardcoded operational records.

---

## 19. Default Implementation Standard

When in doubt, choose the safer option:

- Secure over convenient.
- Explicit over implicit.
- Auditable over hidden.
- Tenant-isolated over shared.
- Human-approved over automated.
- Azure-native over improvised.
- Real provider or `Not configured` over simulation.
- Incremental over large rewrite.
- Tested over assumed.
- Production-conscious over demo-only.

IIP must be built as a serious insurance operations platform, not a fragile AI demo.
