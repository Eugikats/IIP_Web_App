# IIP_IMPLEMENTATION_GUARDRAILS.md — Supporting Engineering Guardrails

> **Updated:** July 2026  
> **Product:** Insurance Intelligence Platform (IIP)  
> **Staff application:** IIP Operations  
> **Status:** Supporting engineering guardrail document  
> **Higher-authority production contract:** `AGENTS.md`

You are working on **Insurance Intelligence Platform (IIP)**.

IIP is an Azure-first, AI-assisted insurance operations platform for insurers, brokers, bancassurance teams, claims teams, underwriters, customer service teams, compliance teams, IT administrators, and operations leaders. It is not a generic chatbot. It is an operational insurance platform that connects AI assistance to approved enterprise data, documents, workflows, human approvals, audit logs, and external systems.

This file applies to Codex, Claude, Antigravity, and any other coding or automation agent working in this repository.

This file must be read before implementing anything. However, for production build, release, Azure Managed Application packaging, Partner Center/private-plan readiness, zero-simulation runtime enforcement, and release gates, **`AGENTS.md` is the higher-authority repository contract**. If this file conflicts with `AGENTS.md`, follow `AGENTS.md` and update this file.

The product principle is simple:

> **AI prepares insurance work. Humans make consequential insurance decisions.**

---

## 0. Authority, Scope, and Required Reading

Before making implementation changes, read the following documents where relevant:

1. `AGENTS.md` — production build and release contract, highest repository authority.
2. `README.md` — repository setup and current implementation status.
3. `IIP_IMPLEMENTATION_GUARDRAILS.md` — this supporting guardrail document.
4. `CLAUDE.md` or other agent-specific instructions, if present.
5. The relevant IIP specification documents under `docs/`.

### 0.1 Documentation library alignment

The repository documentation must align with the generated IIP documentation packs and implementation-ready specs:

- Strategy, Requirements & Roadmap Pack.
- Development & Engineering Execution Pack.
- Design & Architecture Deep-Dive Pack.
- Testing, Security & Quality Execution Pack.
- Deployment, Release & Client Enablement Pack.
- Client Pilot Pack.
- Client Support Pack.
- Continuous Improvement Pack.
- Commercial & Executive Pack.
- Core Ports implementation spec.
- Backend Wiring / Controllers / Composition Root / Request Pipeline spec.
- Application-layer service specs for Command Center, Claims, SLA, Approvals, Documents, Channels, Customers, Sales Leads, Audit, Claim Intake, Write-Back, Notifications, Knowledge, Reporting, Email Ingestion, Tenant Agent Policy, Session/Context, and Multi-Agent Orchestration.

Use those documents as implementation context. Do not create architecture, workflows, permissions, UI behavior, service contracts, data models, or release practices that contradict them.

### 0.2 Naming standard

Use the following naming consistently:

| Usage | Correct name |
|---|---|
| Formal product name | Insurance Intelligence Platform (IIP) |
| Short product name | IIP |
| Staff application name | IIP Operations |
| Legacy/superseded name | ACIP, only when referring to historical drafts |

Do not use `ACIP` as the official product name.

Keep code-level identifiers unchanged unless there is an explicit refactor task. For example, do not rename these merely for branding alignment:

- `InsuranceIntelligence.sln`
- `src/InsuranceIntelligence.Api`
- `src/InsuranceIntelligence.Application`
- `src/InsuranceIntelligence.Domain`
- `src/InsuranceIntelligence.Infrastructure`
- `INSURANCEINTELLIGENCE_*` environment variables
- existing namespaces and package identifiers

---

## 1. Core Product Principles

Always preserve these principles:

1. IIP is Azure-first and Microsoft-native where appropriate.
2. IIP is document-first, workflow-first, and audit-first.
3. IIP is tenant-aware and workspace-aware from the beginning.
4. IIP is secure by default.
5. IIP supports human review and human approval for consequential decisions.
6. AI uses approved, normalized, indexed, tenant-filtered data.
7. AI does not directly query production databases.
8. High-risk insurance decisions are not automated.
9. Every write-back action is controlled, reviewable, and auditable.
10. Deployed environments must not contain mock, simulated, fake, fallback, or placeholder operational behavior.
11. A feature that is not configured must show `Not configured` or be unavailable; it must not appear to work through simulation.
12. IIP Operations must feel like an enterprise insurance operations workspace with embedded AI assistance, not a generic chatbot app.
13. The Command Center is chat-first and structured, but every answer must be grounded, evidence-backed, and rendered as operational blocks, not free-form unverified chat.
14. Foundry, Azure OpenAI, Claude, or any model provider is an inference/tooling layer only; IIP remains the source of truth for tenant context, evidence, approvals, audit, and authorization.

---

## 2. Production Zero-Simulation Guardrail

This section is mandatory and must stay aligned with `AGENTS.md`.

### 2.1 Deployed runtime rule

No mock, simulated, fake, seeded-demo, placeholder, in-memory, SQLite, DevelopmentAuth, demo-header, hardcoded-insurer, hardcoded-user, fake-ingestion, fake-send, fake-dashboard, or fallback-to-mock behavior is permitted in:

- Azure staging environments;
- production-candidate environments;
- production environments;
- customer-deployed Azure Managed Application instances;
- Partner Center reviewer or private-plan deployments;
- any publicly reachable or insurer-facing environment.

Test doubles may exist only inside:

- automated test projects;
- isolated local test harnesses;
- clearly non-deployable `LocalTest` execution;
- `InsuranceIntelligence.LocalTestDoubles`, which must never be referenced by deployable API, Infrastructure, Application, Functions, or portal runtime projects.

### 2.2 Runtime profiles

The implementation must maintain explicit runtime profiles:

| Profile | Purpose | Test doubles allowed? | Customer-facing? |
|---|---|---:|---:|
| `LocalTest` | Automated tests and isolated local verification | Yes | No |
| `Development` | Internal development only | Limited, visibly isolated | No |
| `Staging` | Production-candidate validation with synthetic data | No | Internal validation only |
| `Production` | Live insurer/customer operation | No | Yes |
| `ManagedApplicationReviewer` | Partner Center/private-plan review deployment | No | Yes |

Startup and readiness must fail safely in `Staging`, `Production`, and `ManagedApplicationReviewer` if any required provider or behavior resolves to:

```text
Mock
Simulated
Placeholder
InMemory
SQLite
DevelopmentAuth
Demo
SeededDemoBusinessData
FallbackToMock
```

### 2.3 Required real providers for deployed environments

| Capability | Required deployed implementation | Forbidden deployed alternatives |
|---|---|---|
| Authentication | Microsoft Entra ID | Development Auth, demo headers, browser-selected roles |
| Authorization | Server-side IIP membership and permission resolution | UI-only checks, token-only tenant authority |
| Persistence | Azure SQL Database | SQLite, in-memory repositories |
| Files/documents | Azure Blob Storage | Local/mock storage |
| Document extraction | Azure AI Document Intelligence | Mock extraction, placeholder results |
| Knowledge retrieval/indexing | Azure AI Search | In-memory/mock retrieval |
| AI generation/embeddings | Azure OpenAI, Azure AI Foundry, or approved Foundry-hosted model deployment | Mock assistant, canned responses |
| Workflow queues/events | Azure Service Bus | In-process/mock queues |
| Email ingestion/outbound | Real configured provider through provider-agnostic email ports | Simulated email, fixed global mailbox |
| Secrets | Key Vault references and managed identities where possible | Secrets in source, frontend, docs, or appsettings |
| Monitoring | Application Insights and Log Analytics | Placeholder-only telemetry |
| Customer communications | Real configured provider or `Not configured` | Simulated send |

---

## 3. Azure Architecture Guardrails

Use Azure-native services where appropriate.

Preferred Azure services:

- Microsoft Entra ID for authentication.
- Managed Identity for service-to-service access.
- Azure Key Vault for secrets.
- Azure Container Apps or Azure App Service for APIs and backend services.
- Azure Functions or Container Apps Jobs for ingestion and background work.
- Azure API Management for external integration APIs where needed.
- Azure Service Bus for asynchronous events, workflow messages, retries, and DLQs.
- Azure Blob Storage or Data Lake-style containers for documents and file landing zones.
- Azure SQL Database for normalized application data.
- Azure AI Document Intelligence for document extraction.
- Azure AI Search for retrieval and RAG grounding.
- Azure OpenAI or approved Azure AI Foundry deployments for AI capabilities.
- Application Insights and Log Analytics for monitoring.

Do not hardcode Azure secrets, connection strings, tenant IDs, client secrets, API keys, storage keys, SAS tokens, model credentials, or provider keys into source code.

Use environment variables, managed identity, Key Vault references, secure app configuration, or secure CI/CD variables.

---

## 4. Authentication, Identity, and Authorization Guardrails

All authenticated application access must use Microsoft Entra ID unless explicitly isolated to `LocalTest` or internal non-customer development.

### 4.1 Multitenant Entra model

IIP's default identity model is:

```text
One multitenant Microsoft Entra app registration
Supported account type: Accounts in any organizational directory
Authority mode: organizations
Runtime authorization: AuthenticatedTenantResolver + IPermissionReadRepository
```

Rules:

- `InfrastructureOptions.EntraTenantId` represents IIP's publisher/home tenant for the app registration, not an insurer/customer tenant.
- `EntraClientId` and `EntraAudience` represent IIP's app/API registration and remain stable across insurer tenants.
- JWT validation must accept valid organizational tokens from insurer workforce tenants through the approved multitenant configuration.
- The token `tid` claim is an identity namespace hint only, never IIP business authority.
- Prefer `tid + oid` as the immutable external identity key for Entra workforce users, for example `entra:{tid}:{oid}`.
- Use `sub` only as a last-resort compatibility fallback where `tid + oid` is not available.
- Do not use `email`, `preferred_username`, `unique_name`, or `upn` as authorization keys.

### 4.2 AuthenticatedTenantResolver remains authoritative

Do not weaken the existing server-side authorization design:

```text
Validated JWT identity
-> AuthenticatedTenantResolver
-> IPermissionReadRepository.GetEffectiveMembershipAsync(userId, entraTenantHint, requestedWorkspaceId, ct)
-> internal TenantContext
-> controller/service permission checks
```

The internal `TenantContext` is IIP's authority for:

- `TenantId`
- `WorkspaceId`
- `UserId`
- roles
- permissions
- correlation context

Never treat the Entra token's `tid`, app role, group claim, browser-selected workspace, route parameter, query string, request body, or client header as final business authority.

### 4.3 App roles and groups

App roles may be used only as coarse entry gates, for example:

```text
IIP.Access
IIP.Admin
IIP.ReadOnly
```

They answer: **who may enter IIP at all?**

They do not answer: **what can the user do inside IIP?**

Fine-grained permissions such as `claims.read`, `documents.review`, `approvals.review`, `agent_policy.manage`, or `writeback.execute` remain owned by IIP's role/permission store and resolved through `IPermissionReadRepository`.

Do not use raw Entra group claims as the primary permission mechanism. Enterprise group overage and cross-tenant group portability make raw group claims unreliable for IIP's fine-grained authorization model.

### 4.4 SCIM provisioning

SCIM is a phase-two identity lifecycle capability unless explicitly prioritized for a launch customer.

SCIM may provision or deprovision users/groups into IIP's own membership tables, but runtime authorization still always goes through:

```text
AuthenticatedTenantResolver -> IPermissionReadRepository -> TenantContext
```

SCIM data is not a request-time authorization shortcut.

### 4.5 Authentication anti-patterns

Do not:

- bypass authentication in staging, production, or reviewer environments;
- add fake users, fake roles, mock JWTs, mock claims, or demo headers to deployed runtime paths;
- silently fall back to anonymous access;
- disable JWT validation to make a screen load;
- weaken issuer, audience, expiry, or signature validation;
- make frontend MSAL or role checks the source of truth;
- grant access because a tenant ID appears in a token;
- grant access because an Entra group claim appears in a token;
- grant access because a browser-selected role says the user is Admin.

If authentication fails, diagnose the real cause. Do not patch around it.

---

## 5. Tenant and Workspace Isolation Guardrails

Tenant and workspace isolation is a foundational requirement.

Every tenant-scoped table, document, index record, workflow, integration event, audit event, communication, AI action, session-state record, mailbox mapping, report snapshot, and usage meter must include tenant/workspace context where applicable.

Rules:

- Never return data without tenant filtering.
- Never trust tenant IDs from the client without validating authenticated access.
- Never allow cross-tenant search results.
- Never allow cross-tenant document access.
- Never allow cross-tenant workflow actions.
- Never allow one tenant's configuration, connectors, documents, prompts, mailboxes, audit logs, reports, or AI outputs to leak into another tenant.
- Enforce tenant/workspace context in repositories, services, queries, policies, search filters, blob paths, session-state records, and audit events.
- When uncertain, fail closed.

For Azure Managed Application deployments, the default commercial model is one insurer per managed application instance, with logical workspaces inside the insurer deployment where needed.

---

## 6. Solution and Layering Guardrails

The repository should preserve the agreed layered structure:

```text
src/
  InsuranceIntelligence.Api/
  InsuranceIntelligence.Application/
  InsuranceIntelligence.Domain/
  InsuranceIntelligence.Infrastructure/
  InsuranceIntelligence.Functions/
  InsuranceIntelligence.LocalTestDoubles/
  InsuranceIntelligence.Api.LocalTestHost/   # optional isolated host

tests/
  InsuranceIntelligence.Tests/
```

Project-reference rules:

```text
Api -> Application, Infrastructure, Domain
Infrastructure -> Application, Domain
Application -> Domain
Domain -> no Application/API/Infrastructure dependency
LocalTestDoubles -> Application, Domain only where needed
Api.LocalTestHost -> Api, Application, Infrastructure, LocalTestDoubles
```

Deployable projects must not reference `InsuranceIntelligence.LocalTestDoubles`.

Keep Azure SDK usage in Infrastructure/adapters. Application services depend on ports and contracts, not Azure SDK concrete types.

---

## 7. Data Architecture and Database Guardrails

IIP must use a clean canonical data model.

Initial canonical entities include:

- Tenant
- Workspace
- User / external identity mapping
- Role
- Permission
- UserTenantMembership
- RoleAssignment
- Customer
- Policy
- Claim / Case
- Claim intake record
- Claim workflow transition history
- Claim number sequence
- Document
- Document upload record
- Document extraction record
- Extracted field correction/status history
- Required document rule
- CompletenessCheck
- ChannelConversation / Interaction / Communication
- SalesLead
- Approval / ApprovalDecision
- NotificationDraft / NotificationSend
- AuditEvent
- IntegrationEvent
- WriteBackRequest / WriteBackHistory
- KnowledgeDocument / KnowledgeChunk / KnowledgeIndexStatus
- ReportSnapshot / ReportExportRecord
- MailboxToTenantMapping / EmailCheckpoint
- TenantAgentCapabilityPolicy
- CommandCenterSessionState
- AgentRun / AIInteraction where implemented
- UsageMeter

Incoming source data should move through this pattern:

```text
Source systems / channels
-> raw landing zone
-> validation and mapping
-> canonical application store
-> approved knowledge/search indexes
-> AI-assisted workflows and human review
```

Rules:

- Do not let AI agents operate directly on uncontrolled raw data.
- Do not let every customer integration create custom schema chaos.
- Map external fields into canonical IIP entities.
- Use additive migrations where possible.
- Do not drop production data without explicit approval.
- Store large files in Blob Storage and store references in relational records.
- SQL Server / Azure SQL is the deployable persistence target. SQLite is not allowed in deployable runtimes.
- Tenant-scoped entities must include `TenantId` and `WorkspaceId` where applicable, with indexes.
- Command Center session state must store operational continuity only, never raw retrieved evidence, document text, claim text, OCR text, policy wording, or other large evidence payloads.

---

## 8. Integration Fabric Guardrails

The MVP should be integration-complete without depending on custom deep connectors for every vendor on day one.

Preferred connector types:

- Provider-agnostic email connector.
- Microsoft Graph mailbox adapter.
- Gmail adapter, deferred until provider-specific implementation.
- Zoho Mail adapter, deferred until provider-specific implementation.
- Generic IMAP/SMTP adapter, polling-baseline and deferred until provider-specific implementation.
- Generic API connector.
- Webhook/event connector.
- File/SFTP connector.
- Database read connector.
- Document upload connector.
- Third-party REST connector.
- Manual upload connector.

Rules:

- Avoid direct write access to customer production systems unless explicitly implemented through approved write-back flows.
- Prefer APIs, events, files, or read-only database views.
- Validate all inbound data.
- Log all integration events.
- Store raw payloads separately from normalized records where appropriate.
- Make mapping rules explicit and reviewable.
- Do not allow integrations to bypass tenant boundaries.
- Do not expose secrets in connector configuration screens or logs.
- Integration failures must be visible to operators.

---

## 9. Email Ingestion and Dispatch Guardrails

IIP must not assume a fixed platform-wide claims mailbox.

Each insurer deployment may connect one or more approved mailboxes through the provider-agnostic email ports:

```text
IEmailInboundProvider
IEmailOutboundProvider
```

Microsoft Graph is a real adapter, not the only possible implementation. Future adapters may include Gmail, Zoho Mail, and generic IMAP/SMTP.

### 9.1 Delivery model

Design email ingestion around polling as the baseline guarantee.

- Microsoft Graph and Gmail APIs may support webhook/push-based notification.
- Generic IMAP typically supports polling, with IMAP IDLE treated only as an optimization where reliable.
- Dispatcher correctness must not depend on push availability.

### 9.2 Mailbox-to-tenant mapping

Every inbound mailbox must map to:

- internal tenant;
- workspace;
- product line or case type where configured;
- processing status;
- provider type;
- checkpoint/cursor.

Inbound asynchronous email processing must still resolve tenant/workspace server-side and write audit events. It must not bypass `IPermissionService`, `IAuditWriter`, tenant context, or repository scoping merely because the request came from a mailbox rather than an authenticated browser session.

### 9.3 Attachment handling

Attachments that become claim documents must go through the existing document upload pipeline, especially `IDocumentUploadService`. Do not invent a parallel upload path for email attachments.

### 9.4 Email anti-patterns

Do not:

- process real mail unless provider configuration and mailbox scoping are confirmed;
- expose internal notes in outbound replies;
- simulate email send in deployed environments;
- assume Microsoft Graph-specific message IDs or delta tokens are Application-layer concepts;
- use a fixed global claims mailbox;
- treat provider webhooks as required for correctness.

If email is not configured, show `Not configured`.

---

## 10. AI, Agent, and Grounding Guardrails

AI agents must be grounded, constrained, and auditable.

Rules:

- AI uses approved, normalized, indexed data.
- AI retrieves from Azure AI Search or approved context stores.
- AI does not directly query production databases.
- AI must not fabricate claim facts, policy details, customer data, payments, limits, exclusions, obligations, or compliance conclusions.
- AI must distinguish between source-backed facts, assumptions, and recommendations.
- AI responses that affect insurance operations must include traceability for review.
- AI-generated summaries, recommendations, drafts, and actions must be logged.
- AI assists claims handlers, underwriters, service agents, compliance users, and operations teams; it does not replace required human judgment.

For MVP and production workflows, AI must not automatically:

- approve claims;
- reject claims;
- deny or repudiate claims;
- make binding underwriting decisions;
- release payments;
- set final premiums;
- accuse customers, providers, brokers, or staff of fraud;
- trigger irreversible customer-impacting actions;
- send sensitive customer communications without approval;
- modify source systems without approved write-back.

---

## 11. Canonical Agent Architecture Guardrails

The current canonical IIP agent model is 10 agents:

| # | Agent | Boundary |
|---:|---|---|
| 1 | Command Center Agent | Conversational operational interface; never final decision-maker. |
| 2 | Claims Summary Agent | Summarizes claim facts, timeline, open issues, and evidence. |
| 3 | Document Checker Agent | Checks required documents and extraction quality. |
| 4 | Policy Intelligence Agent | Explains clauses and coverage context from approved knowledge. |
| 5 | Risk & Fraud Signal Agent | Flags risk/fraud indicators for review; never accuses or decides. |
| 6 | Customer Update Drafting Agent | Drafts customer-safe messages; human approval where required. |
| 7 | Routing Agent | Recommends assignment/escalation; deterministic rules or human confirmation decide. |
| 8 | Knowledge Assistant | Answers staff questions from approved KB with citations. |
| 9 | Report Insight Agent | Summarizes operational metrics and management reports. |
| 10 | Underwriting Intelligence Agent | Assists underwriting review; never binds, prices, accepts, or declines cover. |

This agent model is advisory and tool-scoped. Agents must operate through typed, audited tools and service ports. Do not create an agent that bypasses the existing Application service permissions, repositories, approval guards, or audit trail.

If fraud becomes a separate standalone specialist agent later, that requires an explicit architecture revision. Until then, keep fraud signals under **Risk & Fraud Signal Agent**.

---

## 12. Conversational Context and Session Guardrails

IIP uses retrieval-on-demand as the standing rule for grounded Command Center turns.

Rules:

- Every consequential conversational turn re-retrieves authoritative data through existing repositories, Application services, or `IRetrievalProvider`.
- Session state may remember operational continuity, such as current claim, active filters, unresolved questions, or pending task references.
- Session state must not store raw retrieved evidence, claim text, document content, OCR text, policy wording, or large model context payloads.
- Foundry native conversations, `previous_response_id`, or context compaction may be used only as short-horizon model continuity conveniences.
- Foundry compacted state is not an audit record and not an evidence source.
- Audit integrity comes from `IAuditWriter`, `AgentRun`/`AIInteraction` records where implemented, and live source references, not from opaque model memory.

When a user says “the claim we were discussing,” resolve that reference through `ICommandCenterSessionStateService`, then re-retrieve the claim fresh. Do not depend on model memory.

---

## 13. Grounding, RAG, and Policy Q&A Guardrails

All AI answers that affect claims, policies, customers, complaints, underwriting, compliance, or broker workflows must:

1. Authenticate the user and tenant/workspace.
2. Retrieve from approved tenant knowledge first.
3. Apply tenant/workspace/product-line/effective-date filters.
4. Use only approved and indexed knowledge sources.
5. Cite source documents, sections, clauses, or evidence IDs.
6. Show confidence or uncertainty where appropriate for staff users.
7. Escalate when evidence is missing, expired, conflicting, or low confidence.
8. Refuse to answer from model memory where policy wording matters.
9. Log retrieved evidence IDs, prompt version, model/provider metadata, confidence, output, and user action.

Never answer policy coverage questions from model memory.

---

## 14. Human Approval and Decision Boundary Guardrails

For high-risk insurance workflows, use this pattern:

```text
AI suggests or prepares action
-> human reviews evidence and source references
-> human approves, rejects, edits, or escalates
-> IIP creates a controlled action or write-back request
-> external system or communication provider performs the action where configured
-> audit log records everything
```

`ApprovalDecisionGuard.EnsureHumanForConsequentialDecision` is a platform ceiling. Do not bypass it.

No tenant configuration, feature flag, app role, workflow setting, agent policy, model output, or admin UI may allow AI to become the final actor for:

- claim approval;
- claim rejection;
- denial or repudiation;
- settlement approval;
- payment release;
- policy binding;
- premium setting;
- underwriting acceptance/rejection;
- final fraud determination;
- complaint final response;
- regulatory filing submission.

---

## 15. Tenant Agent Capability Policy Guardrails

Tenants may configure structured AI capability policies, but only to narrow capability, never to expand beyond platform rules.

Examples of allowed tenant-policy restrictions:

- disable automatic customer-update drafting;
- require approval above lower thresholds;
- require two-person approval above a configured amount;
- restrict knowledge retrieval by sensitivity label;
- disable document auto-extraction;
- disable management summary generation.

Rules:

- Tenant policy must be structured fields, not free-text AI-interpreted policy.
- Every policy write must validate through a platform-ceiling guard such as `TenantAgentCapabilityPolicyValidator.EnsureDoesNotExceedPlatformCeiling`.
- Tenant policy cannot disable audit writing.
- Tenant policy cannot bypass `ApprovalDecisionGuard`.
- Tenant policy cannot authorize final AI decisions.
- Policy reads/writes must be tenant/workspace scoped and audited.
- Policy update requires `Permissions.AgentPolicyManage` or the current canonical equivalent.

---

## 16. Write-Back, Notifications, and Communications Guardrails

Approved write-back examples:

- Create claim note.
- Update missing-document status.
- Send customer communication after approval.
- Create CRM task.
- Create renewal reminder.
- Export a case pack.

Every write-back request must record:

- tenant/workspace;
- user;
- role;
- source system;
- entity affected;
- proposed action;
- AI-generated content, if any;
- human approval status;
- timestamp;
- result;
- failure reason, if any.

Notification and communication rules:

- `INotificationDraftService` drafts.
- `INotificationSendService` sends only approved drafts or explicitly allowed low-risk configurations.
- `ICommunicationSendingProvider` / outbound provider adapters perform real sends.
- Graph/ACS/other providers must be real or `Not configured` in deployed environments.
- No simulated send path is allowed in staging, production, reviewer, or customer environments.

---

## 17. Audit and Compliance Guardrails

Every important action must be auditable.

Log at minimum:

- user login-relevant events where appropriate;
- user actions;
- AI actions and agent runs;
- AI recommendations and drafts;
- integration events;
- document uploads;
- document extraction results;
- workflow transitions;
- human approvals;
- write-back requests;
- notification drafts and sends;
- permission failures;
- security-sensitive configuration changes;
- provider configuration and health status changes;
- customer communication approvals and sends;
- tenant agent policy changes;
- email ingestion processing;
- report export requests.

Do not log:

- secrets;
- raw access tokens;
- refresh tokens;
- full credentials;
- SAS tokens;
- raw prompts or hidden system messages in user-facing logs;
- unnecessary sensitive personal data;
- full national IDs unless explicitly required and protected;
- medical details beyond what is necessary for the workflow and role.

Audit logs should be append-oriented. Do not casually update or delete audit history.

---

## 18. Privacy and Sensitive Data Guardrails

IIP handles customer data, insurance documents, claims, medical details, payment information, police reports, legal information, and potentially regulatory evidence.

Rules:

- Minimize data exposure.
- Do not show sensitive fields unless required for the user's role.
- Mask or redact sensitive data where appropriate.
- Avoid unnecessary copying of customer data into prompts, logs, telemetry, errors, or screenshots.
- Do not send sensitive data to unapproved external services.
- Treat documents, claim evidence, medical reports, police reports, provider invoices, and payment data as sensitive.
- Preserve consent records where customer communication, recordings, transcripts, or AI calling are involved.
- Customer-facing surfaces must never expose internal risk notes, fraud signals, reserves, internal staff comments, prompts, system messages, or confidential compliance notes.

---

## 19. Secrets and Configuration Guardrails

Never commit secrets.

Never create files such as `.env`, config JSON, YAML, Bicep parameter files, scripts, or docs containing live secrets unless they are explicitly ignored, protected, and meant only for local development.

Use:

- Azure Key Vault.
- Managed Identity.
- Environment variables.
- Key Vault references.
- Secure CI/CD variables.
- Approved local secret stores.

Before making changes, check existing configuration patterns and follow them.

Do not print secrets in logs, tests, CLI output, error messages, screenshots, docs, or comments.

---

## 20. API and Backend Guardrails

Backend APIs must be secure, validated, tenant-aware, and auditable.

Rules:

- Validate all inputs.
- Enforce authorization server-side.
- Enforce tenant/workspace access server-side.
- Use consistent error handling.
- Avoid leaking stack traces or sensitive details to clients.
- Return clear operational errors for the UI.
- Use pagination for large lists.
- Use idempotency where duplicate integration events or write-back requests are possible.
- Use background queues for long-running work.
- Use structured logging and correlation IDs.
- Add tests for authorization, tenant filtering, validation, and safe error output.
- Maintain OpenAPI documentation for externally relevant endpoints.
- Keep controller logic thin; business rules belong in Application services.
- Every repository query must be tenant/workspace scoped where applicable.

Do not implement business-critical checks only in the frontend.

---

## 21. Frontend and IIP Operations UI Guardrails

IIP Operations is an enterprise insurance operations workspace with embedded AI assistance. It is not a generic chatbot-first app.

### 21.1 Command Center UI direction

The Command Center is the primary interface of IIP Operations. Post the Lovable-parity migration the operator app is served under the `/app/*` route tree (the Command Center screen is `/app`), with `/` as the Login screen. Earlier drafts referenced `/command-center`; that is superseded by the `/app` structure. See `docs/06-decisions/ADR-2026-07-04-lovable-visual-parity-mode.md` and `docs/06-decisions/lovable-parity-migration-audit.md`.

Command Center behavior:

- chat-first but structured;
- grounded in real API responses;
- renders typed blocks such as text, metric pills, SLA summary, evidence list, errors, and `Not configured` states;
- uses the real `POST /api/v1/command-center/query` endpoint;
- never renders fake dashboard metrics or hardcoded insurer data;
- uses bearer-token/MSAL authentication, not hardcoded fake tokens;
- shows loading, empty, error, unauthorized, and `Not configured` states clearly.

### 21.2 UI principles

Core UI principles:

- modern enterprise look;
- operations-first workflows;
- embedded AI assistance where useful;
- clear operational dashboards;
- role-aware navigation;
- consistent left navigation, top bar, work area, and contextual insight/action rail;
- clear loading, empty, error, unauthorized, and `Not configured` states;
- real backend API contracts;
- customer-safe boundaries for customer-facing surfaces.

Rules:

- Do not hide backend failures behind fake data.
- Do not show mock data in staging, production, reviewer, or insurer-facing environments.
- Do not expose features that are not configured.
- For unavailable integrations, show `Not configured`.
- For unauthorized users, show a proper unauthorized state.
- Fix layout, stacking, overflow, and responsive issues rather than hiding errors.
- UI may adapt by role, but permissions must still be enforced by the API.
- Do not present AI recommendations as final decisions.
- Do not expose internal AI confidence, fraud/risk indicators, reserves, prompts, internal notes, or detailed audit data to customer-facing views.
- Do not use unsafe HTML insertion for email bodies, document text, communications, customer text, or AI answers.

### 21.3 Approved UI authority

Before changing major frontend page structure, styling tokens, role-aware navigation, AI surfaces, customer-safe surfaces, integration states, approval workflows, or API-to-UI mapping, read the authoritative UI documentation (the `docs/ui/*` paths in earlier drafts were never created — use these real docs):

- `docs/03-design-architecture/iip_design_system_ux_behavior_specification.md` (Ambient Intelligence theme, v1.2)
- `docs/02-development-engineering/iip_ui_ux_handoff_document.md`
- `docs/02-development-engineering/iip_frontend_implementation_guide.md`
- `docs/02-development-engineering/IIP_FRONTEND_ARCHITECTURE_AND_AGENT_HANDOFF.md`
- `docs/03-design-architecture/iip_component_architecture_document.md`
- `docs/03-design-architecture/iip_workflow_architecture_document.md`
- `docs/06-decisions/ADR-2026-07-04-lovable-visual-parity-mode.md`
- `docs/06-decisions/lovable-parity-migration-audit.md`

If the approved UI spec conflicts with implementation reality, report the conflict before inventing a new design direction.

---

## 22. Testing Guardrails

Every meaningful implementation should include tests or a clear reason why tests were not added.

Prioritize tests for:

- authentication;
- multitenant token acceptance and rejection paths;
- authorization;
- tenant/workspace isolation;
- cross-tenant denial;
- data validation;
- integration ingestion;
- field mapping;
- document processing;
- AI grounding boundaries;
- prompt-injection resistance where applicable;
- session-state no-raw-evidence constraints;
- human approval workflows;
- write-back requests;
- audit logging;
- error states;
- API contract stability;
- production profile rejection of mock/simulated providers;
- frontend no-fake-data and no-unsafe-rendering checks;
- ManagedApplicationReviewer fail-closed startup;
- mailbox-to-tenant mapping;
- tenant agent policy cannot widen platform capability.

Before completing work, run relevant test suites, linters, type checkers, builds, security scans, and infrastructure validation commands where available.

If tests cannot be run, state exactly why.

---

## 23. Migration and Database Guardrails

Be careful with database changes.

Rules:

- Do not drop tables or columns without explicit approval.
- Do not destroy data.
- Do not create breaking schema changes without migrations.
- Prefer additive migrations.
- Include tenant/workspace IDs where required.
- Include audit fields where required.
- Use indexes for tenant-scoped queries and common lookups.
- Avoid storing large documents directly in relational tables.
- Production mode must use explicit migration/deployment steps.
- Do not auto-migrate production databases unless the release process explicitly allows it.
- SQL Server/Azure SQL is the deployable database target.
- Do not introduce SQLite into deployable runtime paths.

Where business decisions are still open, model fields conservatively and mark them as TODOs. Do not invent permanent enums or FKs for unresolved product decisions.

---

## 24. Observability Guardrails

Production-quality IIP must be diagnosable.

Use Application Insights, Log Analytics, structured logs, and correlation IDs where appropriate.

Track:

- request failures;
- integration failures;
- queue failures and DLQ counts;
- AI operation failures;
- document extraction failures;
- authentication and authorization failures;
- write-back failures;
- email ingestion failures;
- provider health;
- latency for critical workflows;
- background job status;
- cost and usage signals;
- customer communication failures;
- managed application deployment health where applicable.

Do not swallow exceptions silently.

Do not replace real error handling with console logs only.

---

## 25. Azure Managed Application and Partner Center Guardrails

Release work must follow `AGENTS.md` production gates.

### 25.1 Packaging model

For Azure Managed Application / Partner Center private-plan packaging:

- Bicep is the authoring language.
- Partner Center package submission uses compiled ARM JSON, not raw Bicep.
- The managed app package must include `mainTemplate.json` and `createUiDefinition.json` at the root of the ZIP.
- `createUiDefinition.json` is a first-class JSON artifact; there is no Bicep equivalent.
- Validate the compiled ARM template compatibility required by the current Managed Application toolchain, including the current `languageVersion` constraint at implementation time.
- Use `ManagedApplicationReviewer` runtime profile for reviewer deployment.
- No managed app parameter or UI option may expose mock provider choices for reviewer/customer deployment.

### 25.2 Managed application access and data-plane rule

Managed Application RBAC and deny assignments are control-plane controls. They do not automatically secure data-plane access to:

- Azure SQL;
- Blob Storage;
- Key Vault;
- Azure AI Search;
- Azure OpenAI / Foundry;
- Document Intelligence;
- Service Bus.

Data-plane access must still use managed identity, least-privilege roles, and provider-specific configuration.

### 25.3 Publisher and customer access

Managed Application publisher/customer access posture must be explicit:

- publisher management access parameters;
- publisher tenant and principal IDs;
- customer access restrictions/deny assignments;
- JIT access settings;
- immutable managed-application plan settings documented before go-live.

Do not assume a setting can be changed later after the plan is live unless Microsoft documentation and the current Partner Center workflow confirm it.

### 25.4 Onboarding identifiers

The onboarding flow must capture both:

| Identifier | Purpose |
|---|---|
| Entra tenant ID | Authentication/sign-in and external identity namespace. |
| Azure subscription ID(s) | Partner Center private-plan targeting and Managed Application deployment audience. |

Do not overload one identifier for the other.

If a supplied subscription is a CSP-reseller subscription and the current marketplace private-plan rules do not support it, surface this as a blocker before deployment.

### 25.5 Readiness claims

Do not claim that IIP is:

- production-ready;
- deployment-ready;
- Managed Application-ready;
- Partner Center-ready;
- private-plan-ready;

unless the required gate evidence exists.

---

## 26. Staging Debugging Guardrails

When diagnosing staging issues:

- Validate authenticated identity with a real Entra token.
- Confirm API audience and issuer/multitenant authority configuration.
- Confirm frontend MSAL configuration matches backend JWT validation.
- Separate authentication failures from API route failures, CORS failures, and backend exceptions.
- Check all failing HTTP endpoints, not only the visible screen.
- Configure Graph/email only after core authentication is stable.
- Do not introduce mock authentication bypasses.
- Do not enable mock providers to make staging appear healthy.
- Do not patch over tenant resolution failures by trusting browser tenant/workspace values.

---

## 27. Insurance Domain Guardrails

Treat insurance workflows as high-impact business processes.

Do not implement features that casually:

- approve claims;
- reject claims;
- deny or repudiate claims;
- change policy coverage;
- make binding underwriting decisions;
- send legal or sensitive customer communications without human approval;
- alter payment or settlement records without controlled approval;
- delete claim evidence;
- hide audit history;
- accuse anyone of fraud.

When building claims intelligence, focus on:

- intake;
- evidence review;
- missing document detection;
- claim summaries;
- policy/customer lookup;
- workflow tasks;
- human review;
- approved communication or write-back;
- audit trail.

---

## 28. Recommended Build Order

When building implementation passes, follow the established dependency order unless a maintainer explicitly changes it:

```text
1. Foundation: Application project, InfrastructureOptions, startup validator, DbContext, core ports.
2. Claims query service and EF claims repository.
3. SLA risk service.
4. Approvals query and decision services.
5. Document upload, extraction, and review services.
6. Completeness check service.
7. Channels, Customers, and Sales Leads query services.
8. Audit query service.
9. Claim Intake and Workflow Transition services.
10. Integration Write-Back service.
11. Notification Draft/Send pipeline.
12. Knowledge ingestion/indexing service.
13. Reporting service.
14. Provider-agnostic Email Ingestion and Dispatch.
15. Tenant Agent Capability Policy.
16. Command Center Session State.
17. Multi-Agent orchestration scaffold only when cross-agent handoff is truly needed.
```

Do not jump to advanced multi-agent orchestration before the core services, repositories, permissions, audit, provider health, and Command Center endpoint are real and tested.

---

## 29. Development Workflow Guardrails

Work incrementally.

Before changing code:

1. Inspect the existing architecture.
2. Identify relevant files.
3. Understand existing patterns.
4. Make the smallest safe change.
5. Preserve existing behavior unless changing it is required.
6. Add or update tests.
7. Run validation commands.
8. Summarize exactly what changed.

Do not rewrite large areas unnecessarily.

Do not introduce new frameworks, libraries, cloud services, or architectural patterns without a clear reason.

Do not make speculative changes unrelated to the task.

---

## 30. Dependency Guardrails

Before adding a dependency:

- Check if the project already has an approved library.
- Confirm the package is maintained.
- Avoid unnecessary dependencies.
- Avoid packages with known security issues.
- Prefer official Azure SDKs for Azure integrations.
- Keep dependency changes minimal.

Do not add packages just to avoid writing simple logic.

PDF/report rendering library selection is an explicit open decision. Do not silently add a PDF renderer without approval.

---

## 31. Required Completion Report

At the end of every implementation task, provide a concise report with:

1. What changed.
2. Files changed.
3. Security impact.
4. Tenant/workspace isolation impact.
5. Authentication/authorization impact.
6. Data model or migration impact.
7. AI/human-approval impact.
8. UI impact, if any.
9. Tests added or updated.
10. Commands run.
11. Anything not completed.
12. Risks, blockers, or follow-up actions.

Do not claim success unless validation was actually run or the limitation is clearly stated.

---

## 32. Absolute Do-Not-Do List

Do not:

- hardcode secrets;
- disable authentication;
- bypass authorization;
- trust browser-only permissions;
- remove tenant/workspace filtering;
- trust Entra `tid` as business authority;
- use raw group claims as the fine-grained permission source;
- let AI directly query production databases;
- allow automatic claim approval, rejection, denial, repudiation, settlement, or payment release;
- allow unapproved write-back;
- skip audit logging for important actions;
- mix tenant data;
- store raw evidence in Command Center session state;
- treat Foundry compaction as evidence or audit trail;
- hide real errors with fake success states;
- introduce mock auth into staging, production, or reviewer runtime;
- deploy mock/simulated/fallback providers to staging, production, or reviewer runtime;
- log access tokens, secrets, SAS tokens, prompts, hidden system messages, or confidential internal notes;
- send sensitive data to unapproved external services;
- drop production data;
- rewrite unrelated systems without need;
- add unnecessary dependencies;
- ignore failed tests;
- pretend validation passed when it was not run;
- claim production readiness without evidence.

---

## 33. Default Implementation Standard

When in doubt, choose the safer option:

- Secure over convenient.
- Explicit over implicit.
- Auditable over hidden.
- Tenant-isolated over shared.
- Human-approved over automated.
- Source-grounded over model-memory.
- Retrieval-on-demand over accumulated stale context.
- Structured policy over free-text policy.
- Provider-agnostic ports over hardwired vendor logic.
- Azure-native over improvised.
- Incremental over large rewrite.
- Tested over assumed.
- `Not configured` over simulated success.
- Production-conscious over demo-only.

IIP must be built as a serious insurance operations platform, not a fragile AI demo.
