# IIP_IMPLEMENTATION_GUARDRAILS.md — Supporting Engineering Guardrails

> **Updated:** July 2026  
> **Product:** Insurance Intelligence Platform (IIP)  
> **Staff application name:** IIP Operations  
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

1. `AGENTS.md` — production build and release contract.
2. `README.md` — repository setup and current implementation status.
3. `IIP_IMPLEMENTATION_GUARDRAILS.md` — this supporting guardrail document.
4. `CLAUDE.md` or other agent-specific instructions, if present.
5. Relevant files in the IIP documentation library.

### 0.1 Documentation library alignment

The repository documentation should align with the generated IIP documentation packs:

- Strategy, Requirements & Roadmap Pack.
- Development & Engineering Execution Pack.
- Design & Architecture Deep-Dive Pack.
- Testing, Security & Quality Execution Pack.
- Deployment, Release & Client Enablement Pack.
- Client Pilot Pack.
- Client Support Pack.
- Continuous Improvement Pack.
- Commercial & Executive Pack.

Use those documents as implementation context. Do not create architecture, workflows, permissions, UI behavior, or release practices that contradict them.

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
- `INSURANCEINTELLIGENCE_*` environment variables
- existing namespaces and package identifiers

---

## 1. Core Product Principles

Always preserve these principles:

1. IIP is Azure-first and Microsoft-native where appropriate.
2. IIP is document-first, workflow-first, and audit-first.
3. IIP is tenant-aware and workspace-aware from the beginning.
4. IIP must be secure by default.
5. IIP must support human review and human approval for consequential decisions.
6. AI must use approved, normalized, indexed, tenant-filtered data.
7. AI must not directly query production databases.
8. High-risk insurance decisions must not be automated.
9. Every write-back action must be controlled, reviewable, and auditable.
10. Deployed environments must not contain mock, simulated, fake, fallback, or placeholder operational behavior.
11. A feature that is not configured must show `Not configured` or be unavailable; it must not appear to work through simulation.
12. IIP Operations must feel like an enterprise insurance operations workspace with embedded AI assistance, not a generic chatbot-first app.

---

## 2. Production Zero-Simulation Guardrail

This section is mandatory and must stay aligned with `AGENTS.md`.

### 2.1 Deployed runtime rule

No mock, simulated, fake, seeded-demo, placeholder, in-memory, SQLite, DevelopmentAuth, demo-header, hardcoded-insurer, hardcoded-user, fake-ingestion, fake-send, fake-dashboard, or fallback-to-mock behavior is permitted in:

- Azure staging environments.
- Production-candidate environments.
- Production environments.
- Customer-deployed Azure Managed Application instances.
- Partner Center reviewer or private-plan deployments.
- Any publicly reachable or insurer-facing environment.

Test doubles may exist only inside:

- automated test projects;
- isolated local test harnesses;
- clearly non-deployable `LocalTest` execution.

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
| Authorization | Server-validated roles/claims/groups and tenant/workspace enforcement | UI-only checks |
| Persistence | Azure SQL Database | SQLite, in-memory repositories |
| Files/documents | Azure Blob Storage | Local/mock storage |
| Document extraction | Azure AI Document Intelligence | Mock extraction, placeholder results |
| Knowledge retrieval/indexing | Azure AI Search | In-memory/mock retrieval |
| AI generation/embeddings | Azure OpenAI or approved Microsoft Foundry deployment | Mock assistant, canned responses |
| Workflow queues/events | Azure Service Bus | In-process/mock queues |
| Outlook/email | Microsoft Graph with tenant-configurable insurer mailboxes | Simulated email, fixed global mailbox |
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
- Azure App Service or Azure Container Apps for APIs and backend services.
- Azure Functions or Container Apps Jobs for ingestion and background work.
- Azure API Management for external integration APIs.
- Azure Service Bus for asynchronous events, workflow messages, retries, and DLQs.
- Azure Blob Storage or Data Lake-style containers for documents and file landing zones.
- Azure SQL Database for normalized application data.
- Azure AI Document Intelligence for document extraction.
- Azure AI Search for retrieval and RAG grounding.
- Azure OpenAI or approved Azure AI Foundry deployments for AI capabilities.
- Application Insights and Log Analytics for monitoring.

Do not hardcode Azure secrets, connection strings, tenant IDs, client secrets, API keys, storage keys, SAS tokens, or model credentials into source code.

Use environment variables, managed identity, Key Vault references, secure app configuration, or secure CI/CD variables.

---

## 4. Authentication and Authorization Guardrails

All authenticated application access must use Microsoft Entra ID unless explicitly isolated to `LocalTest` or internal non-customer development.

Rules:

- Do not bypass authentication in staging, production, or reviewer environments.
- Do not add fake users, fake roles, mock JWTs, mock claims, or demo headers to deployed runtime paths.
- Do not silently fall back to anonymous access.
- Do not disable JWT validation to make a screen load.
- Do not weaken issuer, audience, tenant, expiry, or signature validation.
- Keep frontend MSAL configuration aligned with backend JWT validation.
- Validate the `/api/v1/me` or current authenticated identity endpoint before relying on authenticated UI behavior.
- Enforce RBAC server-side, not only in the browser.
- UI role checks may hide navigation, but APIs must enforce permissions.
- Every tenant-scoped API request must validate tenant access on the server.
- Demo-role headers must never grant access outside isolated local/test execution.

If authentication fails, diagnose the real cause. Do not patch around it.

---

## 5. Tenant and Workspace Isolation Guardrails

Treat tenant and workspace isolation as a foundational requirement.

Every tenant-scoped table, document, index record, workflow, integration event, audit event, communication, AI action, and usage meter must include tenant/workspace context.

Rules:

- Never return data without tenant filtering.
- Never trust tenant IDs from the client without validating authenticated access.
- Never allow cross-tenant search results.
- Never allow cross-tenant document access.
- Never allow cross-tenant workflow actions.
- Never allow one tenant's configuration, connectors, documents, prompts, mailboxes, audit logs, or AI outputs to leak into another tenant.
- Enforce tenant/workspace context in repositories, services, queries, policies, search filters, blob paths, and audit events.
- When uncertain, fail closed.

For Azure Managed Application deployments, the default commercial model is one insurer per managed application instance, with logical workspaces inside the insurer deployment where needed.

---

## 6. Data Architecture Guardrails

IIP must use a clean canonical data model.

Initial canonical entities include:

- Tenant
- Workspace
- User
- Role
- Customer
- Policy
- Claim / Case
- CaseParty
- Document
- DocumentExtraction
- CompletenessCheck
- Interaction / Communication
- Task
- Approval / HumanReview
- ConsentRecord
- AuditEvent
- IntegrationEvent
- AgentRun / AgentActionLog
- KnowledgeDocument
- KnowledgeChunk
- WriteBackRequest
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
- Do not let every customer integration create custom chaos.
- Map external fields into canonical IIP entities.
- Use additive migrations where possible.
- Do not drop production data without explicit approval.
- Store large files in Blob Storage and store references in relational records.

---

## 7. Integration Fabric Guardrails

The MVP should be integration-complete without depending on custom deep connectors for every vendor on day one.

Preferred connector types:

- Microsoft Graph mailbox connector.
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

## 8. Microsoft Graph / Outlook Guardrails

IIP must not assume a fixed platform-wide claims mailbox.

Each insurer deployment must connect one or more of that insurer's approved Microsoft 365 mailboxes through Microsoft Graph, for example:

```text
claims@insurer.co.ug
complaints@insurer.co.ug
brokers@insurer.co.ug
motorclaims@insurer.co.ug
medicalclaims@insurer.co.ug
```

Support:

- multiple mailboxes per tenant;
- mailbox-to-workspace mapping;
- mailbox-to-product-line or case-type mapping;
- restricted mailbox access using Exchange Online RBAC for Applications or an approved mailbox-scoping alternative;
- inbound email-to-case creation or linking;
- attachment-to-document processing;
- approved outbound customer email;
- idempotency using Graph message IDs and attachment IDs;
- complete audit trails.

Rules:

- Do not process real mail unless Graph configuration and mailbox scoping are confirmed.
- Do not expose internal notes in outbound replies.
- Do not simulate email send in deployed environments.
- If Graph is not configured, show `Not configured`.

---

## 9. AI and Agent Guardrails

AI agents must be grounded, constrained, and auditable.

Rules:

- AI should use approved, normalized, indexed data.
- AI should retrieve from Azure AI Search or approved context stores.
- AI must not directly query production databases.
- AI must not fabricate claim facts, policy details, customer data, payments, limits, exclusions, obligations, or compliance conclusions.
- AI must distinguish between source-backed facts, assumptions, and recommendations.
- AI responses that affect insurance operations must include traceability for review.
- AI-generated summaries, recommendations, drafts, and actions must be logged.
- AI should assist claims handlers, underwriters, service agents, compliance users, and operations teams, not replace required human judgment.

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

## 10. Grounding, RAG, and Policy Q&A Guardrails

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

## 11. Human Approval Guardrails

For high-risk insurance workflows, use this pattern:

```text
AI suggests or prepares action
-> human reviews evidence and source references
-> human approves, rejects, edits, or escalates
-> IIP creates a controlled action or write-back request
-> external system or communication provider performs the action where configured
-> audit log records everything
```

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

---

## 12. Audit and Compliance Guardrails

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
- permission failures;
- security-sensitive configuration changes;
- provider configuration and health status changes;
- customer communication approvals and sends.

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

## 13. Privacy and Sensitive Data Guardrails

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

## 14. Secrets and Configuration Guardrails

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

## 15. API and Backend Guardrails

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

Do not implement business-critical checks only in the frontend.

---

## 16. Frontend and IIP Operations UI Guardrails

IIP Operations is an enterprise insurance operations workspace with embedded AI assistance. It is not a generic chatbot-first app.

Core UI principles:

- Modern enterprise look.
- Operations-first workflows.
- Embedded AI assistance where useful.
- Clear operational dashboards.
- Role-aware navigation.
- Consistent left navigation, top bar, work area, and contextual insight/action rail where applicable.
- Clear loading, empty, error, unauthorized, and `Not configured` states.
- Real backend API contracts.
- Customer-safe boundaries for customer-facing surfaces.

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

### 16.1 Approved UI authority

Before changing major frontend page structure, styling tokens, role-aware navigation, AI surfaces, customer-safe surfaces, integration states, approval workflows, or API-to-UI mapping, read the approved UI documentation where present:

- `docs/ui/IIP_Operations_UI_Design_Specification_MVP.md`
- `docs/ui/reference/IIP_Global_Theme_Approved.png`
- UI/UX Handoff Document
- Design System & UX Behavior Specification
- Component Architecture Document
- Workflow Architecture Document

If the approved UI spec conflicts with implementation reality, report the conflict before inventing a new design direction.

---

## 17. Testing Guardrails

Every meaningful implementation should include tests or a clear reason why tests were not added.

Prioritize tests for:

- authentication;
- authorization;
- tenant/workspace isolation;
- cross-tenant denial;
- data validation;
- integration ingestion;
- field mapping;
- document processing;
- AI grounding boundaries;
- prompt-injection resistance where applicable;
- human approval workflows;
- write-back requests;
- audit logging;
- error states;
- API contract stability;
- production profile rejection of mock/simulated providers;
- frontend no-fake-data and no-unsafe-rendering checks.

Before completing work, run relevant test suites, linters, type checkers, builds, security scans, and infrastructure validation commands where available.

If tests cannot be run, state exactly why.

---

## 18. Migration and Database Guardrails

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

---

## 19. Observability Guardrails

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
- Graph mailbox ingestion failures;
- provider health;
- latency for critical workflows;
- background job status;
- cost and usage signals;
- customer communication failures.

Do not swallow exceptions silently.

Do not replace real error handling with console logs only.

---

## 20. Development Workflow Guardrails

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

## 21. Dependency Guardrails

Before adding a dependency:

- Check if the project already has an approved library.
- Confirm the package is maintained.
- Avoid unnecessary dependencies.
- Avoid packages with known security issues.
- Prefer official Azure SDKs for Azure integrations.
- Keep dependency changes minimal.

Do not add packages just to avoid writing simple logic.

---

## 22. Environment Guardrails

Keep local test, development, staging, production, and reviewer environments clearly separated.

Rules:

- Do not point local development accidentally at production resources.
- Do not use production secrets in local development.
- Do not use mock auth in staging, production, or reviewer environments.
- Do not mix staging and production storage, databases, queues, Key Vaults, AI indexes, or mailboxes.
- Environment-specific configuration must be explicit.
- Dangerous operations must require explicit confirmation or separate permissions.
- Non-configured live features must show `Not configured`; they must not simulate success.

---

## 23. Staging Debugging Guardrails

When diagnosing staging issues:

- Validate authenticated identity with a real Entra token.
- Confirm API audience and issuer.
- Confirm frontend MSAL configuration matches backend JWT validation.
- Separate authentication failures from API route failures, CORS failures, and backend exceptions.
- Check all failing HTTP endpoints, not only the visible screen.
- Configure Graph/Outlook only after core authentication is stable.
- Do not introduce mock authentication bypasses.
- Do not enable mock providers to make staging appear healthy.

---

## 24. Insurance Domain Guardrails

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

## 25. Release and Azure Managed Application Guardrails

Release work must follow `AGENTS.md` production gates.

Do not claim that IIP is:

- production-ready;
- deployment-ready;
- Managed Application-ready;
- Partner Center-ready;
- private-plan-ready;

unless the required gate evidence exists.

Azure Managed Application readiness must include:

- validated `mainTemplate.json`;
- validated `createUiDefinition.json`;
- `viewDefinition.json` where used;
- reproducible managed app ZIP package;
- real-provider-only staging/production/reviewer defaults;
- no mock provider selection for deployed customer/reviewer runtime;
- reviewer instructions;
- support/operations documentation;
- readiness report.

---

## 26. Required Completion Report

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

## 27. Absolute Do-Not-Do List

Do not:

- hardcode secrets;
- disable authentication;
- bypass authorization;
- trust browser-only permissions;
- remove tenant/workspace filtering;
- let AI directly query production databases;
- allow automatic claim approval, rejection, denial, repudiation, settlement, or payment release;
- allow unapproved write-back;
- skip audit logging for important actions;
- mix tenant data;
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

## 28. Default Implementation Standard

When in doubt, choose the safer option:

- Secure over convenient.
- Explicit over implicit.
- Auditable over hidden.
- Tenant-isolated over shared.
- Human-approved over automated.
- Source-grounded over model-memory.
- Azure-native over improvised.
- Incremental over large rewrite.
- Tested over assumed.
- `Not configured` over simulated success.
- Production-conscious over demo-only.

IIP must be built as a serious insurance operations platform, not a fragile AI demo.
