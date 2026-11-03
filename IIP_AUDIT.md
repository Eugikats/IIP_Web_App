# IIP Audit

Audited: 2026-10-10. Branch `feature/bancassurance-acquisition`, HEAD `e8b2b42`. No code, config or data was changed. The working tree has the same four pre-existing changes it had before the audit (`docs/CONTEXT.md` modified; `iic_prompt.md`, `iip.png` and `docs/01-strategy-requirements-roadmap/iip_consolidated_stakeholder_overview.md` untracked). Build output (`bin/`, `obj/`, `.next/`) was written to git-ignored folders.

## How to read this report

Statuses: BUILT (exists and works end to end), PARTIAL (exists but incomplete, hard-coded, or one case only), BROKEN (exists but errors), MISSING (not in the codebase).

Every row also carries a verification level, because a large part of IIP depends on Azure services that were not reachable in this audit:

- **LIVE**: I ran it locally and observed the result.
- **TESTS**: covered by automated tests that pass (see Section 5), not exercised by me.
- **CODE**: established by reading the source only.
- **NOT VERIFIABLE**: needs live Azure OpenAI, Document Intelligence, Blob, WhatsApp, Graph mail or SMS credentials, which were not used.

Where I could not tell, the word "unclear" is used and the reason is given.

What I actually ran:

| Check | Result |
|---|---|
| `dotnet build InsuranceIntelligence.sln -c Release` | Passed, 0 warnings, 0 errors |
| `dotnet test InsuranceIntelligence.sln -c Release` | Passed: 681 (Domain) + 1083 (Api) = 1764 tests, 0 failed, 0 skipped |
| API started locally (`dotnet run`, Development auth, in-memory EF, mock queue/notification providers forced by env vars) | Started, `/health` healthy, seeded 50 claims |
| Portal started locally (`pnpm dev`, Next.js 15.5.18) | Started, all pages below rendered |
| `pnpm lint` | Passed, no warnings or errors |
| `pnpm check:runtime-data`, `check:provider-status`, `check:role-routes`, `check:ui-release-gates` | Passed |
| `pnpm check:action-permission-parity` | **Failed**: 17 role/permission mismatches between `lib/roles.ts` and `RolePermissionMap.cs` |
| Playwright suites (23 spec files under `web/portal/tests`) | Not run (they start their own servers) |
| Click-through (Playwright, headless Chromium) | Staff pages at 1280px, claimant pages at 390px, magic-link flow end to end, one document upload attempt |

---

## Section 1: System overview

### Tech stack

| Layer | What | Evidence |
|---|---|---|
| Backend | .NET 8, ASP.NET Core minimal APIs, ~672 `.cs` files in `src/`, ~176 in `tests/` | `src/InsuranceIntelligence.Api/Program.cs` (1230 lines, ~260 routes mapped) |
| Layering | Domain, Application, Infrastructure, Api, Functions, LocalTestDoubles | `src/*`, `InsuranceIntelligence.sln` |
| Frontend | Next.js 15.5 (App Router), React 19, TypeScript, Tailwind, recharts, framer-motion, jspdf, msal-browser, pnpm | `web/portal/package.json` |
| Database | Azure SQL (deployed) via EF Core 8; EF InMemory provider locally | `InsuranceIntelligenceDbContext.cs`, `InfrastructureOptions.StorageProvider` |
| File storage | Azure Blob Storage, private containers `raw-documents`, `knowledge-documents`, `audit-exports`; local disk mock in dev | `Api/Azure/AzureBlobStorageProviders.cs`, `Api/Local/LocalMockDocumentStorageProvider.cs` |
| AI | Azure OpenAI (chat, embeddings), Azure Document Intelligence, Azure AI Search | `Api/Azure/*` |
| Local-language | AfriSLM (llama.cpp in Azure Container Apps) and Sunbird API for translation; Sunbird for voice-note transcription | `Infrastructure/Translation/*` |
| Queues and background | Azure Service Bus (config), Azure Functions app (5 functions), two in-process `Channel<T>` queues in the API | `src/InsuranceIntelligence.Functions/*.cs`, `Api/Agents/ClaimTriageQueue.cs`, `Api/Integrations/WhatsAppAssistantQueue.cs` |
| Functions | `AgentWorkflowNodeExecute` (Service Bus), `CaseFollowThrough`, `MailboxIngestion` (every 5 min), `RetentionEnforcement`, `TeamsNotificationDispatch` | `src/InsuranceIntelligence.Functions/` |
| Hosting | Azure App Service (API, portal), Function App, Container Apps, Key Vault, App Insights, in resource group `rg-iip-dev`, West US 2 (OpenAI in East US) | `docs/05-deployment-release-enablement/AZURE_DEPLOYMENT_AND_ENVIRONMENTS.md` (not re-verified with `az`) |
| CI/CD | `.github/workflows/ci.yml`; `deploy-staging.yml` is deliberately guarded to fail; no working deploy workflow, deploys are manual `az webapp deploy` | same doc, `.github/workflows/` |
| IaC | Bicep (`infra/bicep`, `infra/mainTemplate.bicep`), Marketplace createUiDefinition | `infra/` |

### Repo structure

| Folder | Purpose |
|---|---|
| `src/InsuranceIntelligence.Domain` | Business records and rules (cases, documents, products, fraud rules, roles/permissions, agent catalog) |
| `src/InsuranceIntelligence.Application` | Use-case services (claims summary, command centre, customer assistant "Kato", compliance, onboarding, billing, demo seeding) |
| `src/InsuranceIntelligence.Infrastructure` | EF persistence, WhatsApp/Graph/SMS/SMTP adapters, translation, DI registration, 29 migrations |
| `src/InsuranceIntelligence.Api` | HTTP endpoints, auth, Azure provider adapters (`Azure/`), not-configured providers (`Local/`) |
| `src/InsuranceIntelligence.Functions` | Timers and Service Bus workers |
| `src/InsuranceIntelligence.LocalTestDoubles` | Mocks used by tests only |
| `tests/` | `InsuranceIntelligence.Domain.Tests`, `InsuranceIntelligence.Api.Tests`, fixtures |
| `web/portal` | One Next.js app: staff app under `/app/*`, claimant pages under `/access/*`, Academy under `/app/academy` |
| `web/portal/lovable-pages` | The page implementations the thin `app/*/page.tsx` routes delegate to |
| `infra/` | Bicep, ADRs, Marketplace packaging |
| `docs/` | Product, architecture, QA, deployment documents |
| `prototypes/`, `languages/`, `tools/`, `scripts/`, `artifacts/` | Prototype HTML, language assets, helper scripts (not examined in depth) |

### How to run locally, and whether it worked

Yes, it ran. Steps that worked (from repo root; env vars override the Azure defaults in `appsettings.Development.json`):

```
ASPNETCORE_ENVIRONMENT=Development INSURANCEINTELLIGENCE_QUEUE_PROVIDER=Mock \
INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER=Mock INSURANCEINTELLIGENCE_SEED_DEMO_DATA=true \
ASPNETCORE_URLS=http://localhost:5167 dotnet run --project src/InsuranceIntelligence.Api/InsuranceIntelligence.Api.csproj

cd web/portal
NEXT_PUBLIC_TENANT_ID=tenant-demo NEXT_PUBLIC_WORKSPACE_ID=workspace-operations \
NEXT_PUBLIC_LOCAL_USER_ID=local-admin NEXT_PUBLIC_AUTH_MODE=Development pnpm dev
```

Caveats observed:

- `appsettings.Development.json` sets `INSURANCEINTELLIGENCE_QUEUE_PROVIDER=AzureServiceBus` and a real Service Bus namespace name. I overrode both to avoid contacting Azure. Without the override the API would try the real namespace (not tested).
- Seed data is created only when `INSURANCEINTELLIGENCE_SEED_DEMO_DATA=true`, under tenant `tenant-demo`, workspace `workspace-operations` (`Program.cs:~505`). With the headers `X-Tenant-Id: tenant-demo`, `X-Workspace-Id: workspace-operations` the API returned 50 claims (Motor 18, Medical 13, General 8, Life 6, Agriculture 5) and 5 published products.
- **No mock AI or document processing exists in the runnable API.** `Program.cs:248-252` and `:299-308` register only the Azure provider or a "NotConfigured" provider. The mocks live in `LocalTestDoubles` and are used by tests. Locally, every AI and extraction feature therefore returns "not configured" (`/api/v1/system/capabilities` returned `activeAi:false, activeDocumentExtraction:false`). Consequently the AI features in Sections 3C and 3E could only be assessed from code and tests.

### Multi-tenancy

- **Model**: one shared database. Every table has `TenantId` and `WorkspaceId` columns; 52 of the 57 `DbSet`s derive from `PersistedScopedJson` (`InsuranceIntelligenceDbContext.cs:357-361`) and carry `Id, TenantId, WorkspaceId, PayloadJson, UpdatedAt`. The 4 scoped permission tables (memberships, role assignments, roles, role permissions) carry the same scope columns; the global `Permissions` catalogue does not.
- **Enforcement**: repository-level `Where(TenantId == scope.TenantId && WorkspaceId == scope.WorkspaceId)` (`Infrastructure/Persistence/JsonRepositoryBase.cs:26`). There is no EF global query filter (`HasQueryFilter` has zero hits), so isolation depends on every repository going through that base.
- **Blob storage**: keys are prefixed `{tenantId}/{workspaceId}/...` and reads check the prefix (`AzureBlobDocumentStorageProvider.EnsureScopedReference`).
- **Runtime authority** (ADR-0005): the user's Entra `tid` is only a lookup hint. Tenant, workspace and permissions come from IIP's own tables (`UserTenantMemberships`, `RoleAssignments`, `RolePermissions`). A user with no active membership gets nothing.
- **Important limit**: the tenant model is correct in the data layer, but several runtime paths are bound to ONE tenant per deployment. See Section 5, "Second insurer".

### Authentication and roles

- **Staff**: Microsoft Entra only (multitenant app registration, `Microsoft.Identity.Web`, JWT bearer). The insurer must have an Entra/Microsoft 365 tenant. There is no email-and-password or other identity provider (CODE: `RuntimeContextEndpoints.cs`, ADR-0005).
- **Development mode**: header identity (`x-demo-role`, `x-demo-user-id`, `X-Tenant-Id`, `X-Workspace-Id`). `RuntimeHeaderGuard` rejects these in deployed profiles (CODE and TESTS: `ProtectedRuntimeReconciliationTests`, `SecurityAuthorizationTests`).
- **Claimants (customers)**: no accounts. Magic link by policy number plus email or phone, single-use token (hash stored), 15-minute link lifetime from the web form, 24 hours when issued over WhatsApp, 24-hour session (`ClaimantAuthenticationEndpoints.cs`, `WhatsAppClaimantAccessLinkIssuer.cs`). LIVE.
- **Roles** (`PlatformRole`): Admin, ClaimsOfficer, ClaimsSupervisor, CustomerService, Broker, Compliance, Client, KnowledgeManager, ITAdmin, PublisherSupport (10).
- **Permissions** (`Permission` enum, 30 values) mapped by `RolePermissionMap.cs`, enforced with `PermissionEndpointFilter` per route. Examples: `ViewAllClaims`, `ApproveSettlementWorkflowStep`, `ExportEvidencePack`, `ManageProducts`, `ManageTenantOnboarding`.
- `UserRole` in `Domain/Users/UserRole.cs` is a second, different role enum (ClaimsOfficer, ClaimsSupervisor, CustomerServiceAgent, ComplianceOfficer, TenantAdmin, CloudAdmin, KnowledgeManager). Which one is authoritative at runtime is unclear; `PlatformRole` is the one used by the permission map.

### Environment variables and external services (names only)

Backend, read in code:

`INSURANCEINTELLIGENCE_` + `RUNTIME_PROFILE, AUTH_MODE, STORAGE_PROVIDER, BLOB_PROVIDER, AI_PROVIDER, DOCUMENT_PROVIDER, RETRIEVAL_PROVIDER, QUEUE_PROVIDER, EMAIL_PROVIDER, COMMUNICATION_PROVIDER, SMS_PROVIDER, NOTIFICATION_PROVIDER, CHANNEL_PROVIDER, PROVISIONER_PROVIDER, TRANSLATION_PROVIDER, TENANT_ID, WORKSPACE_ID, SQLSERVER_CONNECTION_STRING, ENTRA_TENANT_ID, ENTRA_CLIENT_ID, ENTRA_AUDIENCE, ENTRA_ISSUER, CLIENT_PORTAL_BASE_URL, API_PUBLIC_BASE_URL, SMS_WEBHOOK_SHARED_SECRET, ALLOW_PRODUCTION_MOCKS, SEED_DEMO_DATA, AZURE_REGION, DATA_RESIDENCY_COUNTRY, DATA_RESIDENCY_GEOGRAPHY, DATA_RESIDENCY_MODE, DATA_RESIDENCY_ALLOWED_AZURE_REGIONS, DATA_RESIDENCY_APPROVAL_REFERENCE, LOCAL_DOCUMENT_STORAGE_PATH, LOCAL_DOCUMENT_MAX_BYTES, LOCAL_KNOWLEDGE_STORAGE_PATH, LOCAL_KNOWLEDGE_MAX_BYTES, DEMO_PHONE_NUMBER, DEMO_POLICYHOLDER_NAME`.

Configuration sections: `AzureOpenAI:` (Endpoint, ChatDeployment, EmbeddingDeployment, ApiVersion, MaxTokens, Temperature, RequestTimeoutSeconds, PolicyAnswerPromptTemplatePath, UseManagedIdentity, DeploymentType), `DocumentIntelligence:` (Endpoint, DefaultModelId, ModelMode, UseManagedIdentity, ApiKey), `AzureAiSearch:`, `AzureBlob:`, `ServiceBus:`, `GraphMail:` (TenantId, ClientId, ClientSecret, DefaultMailboxAddress, ...), `AfricasTalking:` (ApiKey, Username, SenderId, ...), `WhatsApp:` (AccessToken, AppSecret, VerifyToken, PhoneNumberId, TenantId, WorkspaceId, AssistantAutoReplyEnabled, ...), `Translation:` (Endpoint, ApiKey, EnabledLanguages, ...), `Assistant:` (Organisation, ShortName, PartnerInsurers), `Onboarding:AdminConsentRedirectUri`, `Portal:CorsOrigin`, `Azure:KeyVaultUri`, `ProviderHealth:TimeoutSeconds`.

Portal (build time, `NEXT_PUBLIC_`): `API_BASE_URL, TENANT_ID, WORKSPACE_ID, LOCAL_USER_ID, AUTH_MODE, RUNTIME_PROFILE, PRESENTATION_MODE, ENTRA_TENANT_ID, ENTRA_CLIENT_ID, ENTRA_PORTAL_CLIENT_ID, ENTRA_API_SCOPE, ENTRA_REDIRECT_URI`.

External services: Azure SQL, Blob, OpenAI, Document Intelligence, AI Search, Service Bus, Key Vault, Application Insights, Container Apps; Microsoft Entra; Microsoft Graph (mailbox read, mail send); Meta WhatsApp Cloud API; EgoSMS and Africa's Talking (SMS); SMTP; Microsoft Teams incoming webhook; Sunbird (translation and transcription); self-hosted AfriSLM.

---

## Section 2: Data model

### Storage pattern

Almost every table is a **JSON document store on SQL**: columns `Id, TenantId, WorkspaceId, PayloadJson, UpdatedAt` plus one or two indexed lookup columns (`CaseId`, `Status`, `RecipientUserId` and similar). The business fields live inside `PayloadJson`. Consequence: the database cannot enforce relationships or required fields, and SQL queries over claim fields are not possible without deserializing in application code (CODE: `InsuranceIntelligenceDbContext.cs` `ConfigureScopedJson`).

### Tables (57 `DbSet`s)

| Table | Purpose | Key fields (outside `PayloadJson` unless noted) |
|---|---|---|
| Cases | Claim cases (`ClaimCase`) | Id=CaseId; payload: customer name/email/phone, PolicyNumber, ProductLine, ProductId+ProductVersion, ClaimType (string), Channel, IncidentDate, MotorFnolDetails, MedicalFnolDetails, Status, CompletenessScore, SlaDueAt, AssignedUserId |
| CaseDocuments | Uploaded files and their processing result | CaseId; payload: OriginalFileName, ObjectKey, DocumentType (enum), DocumentTypeKey (customer's slot), Classification, Extraction (fields, confidences, bounding boxes, approval), ValidationWarnings, IsSuperseded |
| KnowledgeDocuments | Policy wordings, SOPs, playbooks | payload: type, status, index state |
| AuditEvents | Audit trail | CaseId |
| AgentActionLogs | One row per AI action (prompt summary, output, sources, tokens fields) | CaseId |
| AgentRuns | Agent run records with tool trace | AgentKey, CaseId |
| AgentStates | Per-tenant agent kill-switch state | |
| AgentBehaviourProfiles | Versioned per-tenant agent voice and scope prompts | AgentKey |
| AgentWorkflowDefinitions / AgentWorkflowRuns / AgentWorkItems | Configurable agent workflow engine | WorkflowKey, Status, Version |
| WorkflowTasks, Approvals | Human tasks and approval requests | CaseId |
| CustomerUpdates | Drafted/approved/sent customer messages | CaseId |
| Communications | Sent/received communication records | CaseId |
| CommunicationTemplates | Versioned message templates | TemplateKey, Version |
| ClaimReviewRequests, CaseReviews | Customer "ask about my claim" requests; AI case-review output | CaseId |
| ContactDetailChangeRequests | Customer requests to change contact details | |
| SatisfactionResponses | Post-claim satisfaction | CaseId |
| Customers | Customer master (name, email, phone) | |
| Policies, Coverages, Branches, Producers | Policy master data | |
| Products / ProductVersions | Product configuration (see below) | ProductId, Version |
| DocumentTypeDefinitions | Per-tenant document type catalogue, versioned | Key, Version |
| DocumentFieldValidationRules | Rules checking extracted fields | DocumentType |
| DocumentLabelledExamples | Staff reclassification examples (accuracy tracking) | CaseId |
| TenantConfigurations, Workspaces, TenantOnboardings | Tenant settings, workspaces, onboarding records | |
| UserAccounts + UserTenantMemberships, RoleAssignments, Roles, RolePermissions, Permissions | Users and RBAC | TenantId, WorkspaceId, UserId, RoleCode |
| MailboxConnections, InboundEmails | Connected mailboxes and ingested mail | |
| IntegrationConnections | Per-tenant encrypted provider credentials (SMS, SMTP, translation) | |
| WhatsAppChannelConfigurations, WhatsAppMessages, CustomerLanguagePreferences | WhatsApp config (encrypted), message log, language | ThreadId, ProviderMessageId |
| CustomerConversationTurns | Kato conversation memory | ThreadId |
| CommandCenterConversations | Officer assistant conversations | OwnerUserId, Status |
| ClaimantMagicLinkTokens, ClaimantPortalSessions | Claimant auth (hashes only) | TokenHash, SessionTokenHash |
| ConsentRecords | Contact consent | CustomerIdentifier |
| RetentionPolicies, RetentionReminders | Retention config; policy-renewal reminders | PolicyId |
| FraudSignalDispositions | Human handling of fraud signals | Status |
| SalesLeads | Prospects | |
| Notifications, TeamsNotifications | In-app and Teams notifications | RecipientUserId |
| AcademyPricings | Academy course prices | |

### How the requested concepts are modelled

| Concept | Modelled? | Where | Configurable data or hard-coded |
|---|---|---|---|
| Company/tenant | Yes | `TenantId`/`WorkspaceId` columns; `Tenant`, `Workspace` records (`Domain/Tenants`); `TenantOnboardings`, `TenantConfigurations` tables | Data |
| Product / policy type | Yes | `Product` record, tables `ProductVersions` (append-only versions, `Domain/Products/Product.cs`) | **Data**, per tenant, created through UI/API (`/api/v1/products`) |
| Claim type | **Only as a free string** | `ClaimCase.ClaimType` (string). No `ClaimType` entity, no list per product | **Hard-coded** classification: `ClaimTypeClassifier` keyword-matches Motor claim text into 4 kinds (OwnDamage, ThirdPartyProperty, TheftTotalLoss, InjuryFatality, plus Unknown) and returns Unknown for every non-Motor product (`Domain/Cases/ClaimTypeClassifier.cs`) |
| Required documents per claim type | **Partially, per product, not per claim type** | `Product.DocumentMatrix` (`ProductDocumentRequirement`: DocumentKey, CustomerLabel, Reason, IsRequired, RequiredAtStageKey, IsRegulatoryOrWordingMandated). LIVE: Motor matrix has 6 entries, same for every Motor claim type | Data when the claim is pinned to a published product. Otherwise **hard-coded** fallback `DocumentChecklistRules.Resolve` (Motor/Medical/default, plus 4 Motor claim-type variants) |
| Claim fields | **Hard-coded** | `MotorFnolDetails` (10 fixed fields) and `MedicalFnolDetails` (9 fixed fields) records on `ClaimCase`. No other product has a field set. No field-definition table | Hard-coded in C# |
| Claim | Yes | `ClaimCase`, table Cases | |
| Customer | Yes | `Customer` record, table Customers; `Customer360` view | |
| Uploaded file | Yes | `CaseDocument`, table CaseDocuments; blob in `raw-documents` | |
| Document type / label | Yes, hybrid | `DocumentType` enum (8 values: Unknown, ClaimForm, PoliceAbstract, Invoice, MedicalReport, PhotoEvidence, CustomerId, GarageEstimate) **plus** tenant catalogue `DocumentTypeDefinition` (custom keys allowed, versioned). Custom types are tracked by key but the classifier cannot recognise them (`Domain/Documents/DocumentTypeDefinition.cs`) | Catalogue is data; recognition is limited to the 8-value enum |
| Claim status | Yes | `CaseStatus` enum: New, Assigned, InReview, MissingDocuments, ReadyForReview, PendingApproval, ReadyForExport, Escalated, Settled, Closed (10) | Hard-coded enum. Product `Stages` (8 default IRA stages) are a separate, configurable process definition |
| User | Yes | `UserAccounts`, `UserTenantMemberships`, `RoleAssignments`; `PlatformRole` enum | Roles are a code enum; role-to-permission map is code (`RolePermissionMap`) although `Roles`/`RolePermissions` tables exist |

### Configuration as data vs hard-coded, summary

Configurable per company as data (LIVE for products; CODE for the rest): products, product claim stages (with IRA service-standard working days per value tier), product document matrix, document type catalogue, document field validation rules, communication templates, agent behaviour (voice and scope), agent kill switches, retention days for one data class, users and role assignments, integration connections, Academy prices.

Hard-coded: claim types and their taxonomy, claim field sets (Motor and Medical only), the 8 recognised document types and their classifier, Motor-only claim-type routing, the default claim process (Uganda IRA stages), built-in document labels shown to customers (`ClientPortalEndpoints.Label()`), role-permission map, the microinsurance product catalogue used by the WhatsApp assistant (`MicroinsuranceCatalog.cs`), the entitlement package and seat limit (`DefaultEntitlements`, SeatLimit 50), SLA and reminder timings (3 days, 24 hours, `CaseFollowThroughService`).

---

## Section 3: Feature checklist against the target

### A. Self-serve company setup

| # | Item | Status | Verification | Evidence | What is missing |
|---|---|---|---|---|---|
| A1 | New company signs up without a developer | PARTIAL | CODE, TESTS | `POST /api/v1/onboarding`, `TenantOnboardingService.CreatePendingAsync` requires `ManageTenantOnboarding` (Admin role only). Flow: an IIP operator creates a record, the insurer's Entra admin completes Microsoft admin consent, IIP bootstraps the admin membership (`Application/Onboarding`). Local `/api/v1/system/capabilities` shows `activeOnboarding:false` | No public sign-up. An IIP operator must start it, and the insurer must have an Entra tenant. Consent URL reports "Not configured" without Entra settings |
| A2 | Add products (Motor, Medical) | BUILT | LIVE (read), CODE (write) | `ProductEndpoints.cs`, `Domain/Products/Product.cs`, UI `/app/admin/products` (5 seeded products listed, v1 Published). Versioned, submit then second-person approve | Publishing requires an IRA approval reference and effective date, and at least one stage (`Product.CanPublish`), so it is Uganda-regulator specific |
| A3 | Per product: define claim types | MISSING | CODE | No claim-type collection on `Product`. `ClaimCase.ClaimType` is free text; the Motor-only keyword classifier is hard-coded | Claim type entity per product, UI, intake dropdown |
| A4 | Per claim type: define required documents | PARTIAL | LIVE, CODE | The matrix is per product, not per claim type (Motor Theft and Motor Own Damage get the same 6 entries). `StaticDocumentChecklistProvider` uses the pinned product's matrix, else hard-coded `DocumentChecklistRules`. Custom document keys supported | Matrix keyed by claim type. Custom types cannot be auto-recognised |
| A5 | Per claim type: define claim fields | MISSING | CODE | Only `MotorFnolDetails` and `MedicalFnolDetails`. `DocumentFieldValidationRule` validates extracted document fields, it does not define claim fields | Field-definition model, form renderer, storage |
| A6 | Upload wordings, AI drafts covered items, exclusions, required documents for human confirmation | MISSING | CODE | Wordings upload to Knowledge (`/api/v1/knowledge-documents`), indexed for search; `.../classify` is a manual type label. A product can reference a wording id (`ApprovedWordingKnowledgeDocumentId`) and publish is blocked without one. No code derives structured items from wordings | LLM extraction of cover, exclusions and documents, review UI |
| A7 | Guided onboarding wizard or conversational agent | MISSING | CODE | `FirstRunOrientation.tsx` is an optional UI tour, off by default. Admin onboarding page is a form for IIP operators | Setup wizard or agent that configures products as it asks questions |
| A8 | Test/preview step before going live | PARTIAL | CODE | `GET /api/v1/document-checklist/preview` (checklist before a case exists); `POST /api/v1/agents/{key}/behaviour/test` (tests an agent prompt); products need a second-person approval before Published | No "see what the customer will see" preview of the claim link |
| A9 | Connect an email inbox; connect a WhatsApp number | PARTIAL | CODE, TESTS; NOT VERIFIABLE live | UI `/app/admin/email`, `/app/admin/whatsapp`; `WhatsAppConfigurationEndpoints` (encrypted, write-only secrets, validated against Meta before save); `POST /api/v1/integrations/email/mailboxes`. Ingestion timer and WhatsApp webhook use the deployment's single `TenantId`/`WorkspaceId` (`MailboxIngestionTimerFunction.cs`, `WhatsAppRuntimeSettingsProvider.cs`) | Works for one tenant per deployment only. Email is Microsoft Graph (Microsoft 365) only; no IMAP |

### B. Customer claim link (mobile web)

| # | Item | Status | Verification | Evidence | What is missing |
|---|---|---|---|---|---|
| B1 | Unique, secure claim link, no login | BUILT | LIVE (issue, redeem, view); delivery NOT VERIFIABLE | `POST /api/v1/client-portal/magic-links` then `/access/verify?token=...` returned a working session and claim page. Token stored as hash, single-use, 15 min (web) or 24 h (WhatsApp). Constant-shape response to avoid enumeration. In Development the link is returned in the response because no SMS/email provider is connected | The customer must first ask for a link by typing policy number plus contact, or get one via Kato on WhatsApp. I found no staff action that pushes a link to a customer from the claim page. Link delivery over SMS/email not verified |
| B2 | Page shows required documents as separate slots | BUILT | LIVE | At 390px the Motor Theft claim showed: Completed claim form, Police report, Photos of the damage (required) and Repair estimate or invoice, Garage estimate, Customer identification (optional), plus "Something else" | Labels for built-in types are hard-coded (`Label()`), so tenant-edited `CustomerLabel` for built-in documents is not shown. LIVE: portal said "Police report", the product matrix says "Police abstract" |
| B3 | Upload per slot as file or phone photo | PARTIAL | LIVE (UI, request), NOT VERIFIABLE (processing) | `<input type=file accept="image/*,application/pdf">`, one file per slot (replace supersedes the earlier file, never deletes), several for "Something else" (`ClaimAccessStatus.tsx`). No `capture` attribute on the claimant inputs (0 found), the browser decides whether to offer the camera. LIVE: the upload request returned **503** locally because Document Intelligence is not configured, and the customer page showed a safe error | End-to-end store, extract, status update only provable with Azure providers |
| B4 | In-browser camera document scanning (edge detection, crop, perspective, clean-up) | MISSING | CODE | No `getUserMedia`, OpenCV, jscanify or similar anywhere in `web/portal` | Entire scanner |
| B5 | Document photos converted to PDF in background | MISSING | CODE | `jspdf` is used only for the Academy certificate (`lib/academy/certificate.ts`). Photos upload as JPEG; no server-side PDF conversion; no zip/PDF libraries in backend packages | Client or server conversion |
| B6 | Evidence photos kept as originals with metadata | PARTIAL | CODE | `lib/photoDownscale.ts` re-encodes any image wider than 1600px to JPEG at 0.85 quality, which drops EXIF and discards the original. Images up to 1600px upload unchanged. No evidence-versus-document distinction exists. HEIC passes through undecoded | Keep the original (or upload both), preserve EXIF, tag evidence vs document |
| B7 | Unslotted uploads auto-classified by AI and renamed | PARTIAL | CODE | Classification: Azure Document Intelligence document type mapped for invoice, receipt, medical, identity only; otherwise a **filename keyword** match (`AzureDocumentIntelligenceProvider.Classify`). It is not an LLM. No code renames files (zero hits for rename logic). Original filename is kept (`CaseDocument.OriginalFileName`) | A real classifier; the renaming rule `Police_Letter_<ClaimRef>.pdf` |
| B8 | Completeness tracking, customer sees what is missing | BUILT | LIVE | Completeness bar (67%), per-slot status Received/Being checked/Not uploaded, "What happens next" list | See Section 5: completeness score and slot status are computed from different keys for built-in types |
| B9 | Claim tracking/status page | BUILT | LIVE | `/access/claim` shows status, 5-step timeline, messages, claim question box | Status text is the raw enum name split into words ("Missing Documents"). The file's own comment says so. Timeline is fixed |
| B10 | Works on cheap Android, slow connection (size limits, compression, retry) | PARTIAL | CODE | 25 MB per file cap (`DocumentUploadValidation`), client downscale above 1600px. Claimant upload uses plain `fetch` (`lib/clientPortal.ts:215-240`): no progress bar, no retry, no resume, no offline queue (the code comments say offline queueing is deliberately not invented). Staff widget has XHR progress. Page is the full Next.js app; load time on a slow link not measured | Retry/resume, progress, smaller targets, measured low-end test |

### C. Insurer side

| # | Item | Status | Verification | Evidence | What is missing |
|---|---|---|---|---|---|
| C1 | Claims list and single-claim view | BUILT | LIVE (list), CODE (detail) | `/app/claims` showed 50 claims with queue tabs (My queue 42, Missing documents 24, Pending approval 6, Escalated 3), filters, search. Detail page `ClaimReview.tsx` (1062 lines), `/app/claims/[id]/extraction` | Detail page not opened in this audit |
| C2 | AI extraction of fields from documents into the claim | PARTIAL | CODE, TESTS (`ExtractionRegressionTests`, 2 golden fixtures); NOT VERIFIABLE live | Azure DI (`prebuilt-document` default) puts key/value pairs, confidences and bounding boxes on the document. Staff can approve or correct fields. No code path writes extracted values into the claim record (grep found none) | Mapping extracted fields into claim fields; per-document-type models |
| C3 | AI claim summary | BUILT | CODE, TESTS (`ClaimsSummaryTests`); NOT VERIFIABLE live | `POST /api/v1/cases/{id}/summary`, `ClaimsSummaryService` (reads case, documents, policy, knowledge via governed tools, returns summary, open questions, risk signals with citations). Also queued automatically at intake from staff, claimant portal, email and WhatsApp (`ClaimTriageWorker`) | Needs Azure OpenAI deployment; not run |
| C4 | Claims pack export: summary PDF, renamed documents, zip | PARTIAL | CODE | `POST /api/v1/compliance/cases/{id}/audit-pack/export` writes one **JSON** evidence pack (audit events, document metadata, AI action log, approvals, customer updates) to blob (`ComplianceQueryService.cs:159-227`). No PDF, no zip, no documents, no renaming | PDF summary, document bundling, naming, zip |
| C5 | Pack emailed to insurer automatically | MISSING | CODE | Generic `POST /api/v1/communications/send-email` exists; nothing connects export to email | Trigger, recipient config, attachment sending |
| C6 | Officer can change claim status | PARTIAL | CODE, TESTS | No generic "set status". Transitions are indirect: assign (New to Assigned), escalate, approval workflow (to ReadyForExport), resolve (ReadyForExport or Escalated to Settled/Closed, human-only, audit reason required). I found no code that writes InReview, MissingDocuments or ReadyForReview other than seeding and creation; unclear how those are reached in production | A direct, audited status change and a defined transition table |
| C7 | Status change notifies customer (channel?) | PARTIAL | CODE | A status change does not notify by itself. Resolving a claim auto-drafts a satisfaction question and (if Settled) a referral invitation as **drafts** that need human approve then send. Send works for **Email and SMS** only; WhatsApp, Portal and Teams return `ConfigurationRequired` in `ConfiguredCustomerUpdateCommunicationProvider`. A separate endpoint sends WhatsApp messages. Missing-document reminders go out on a timer after 3 days (`CaseFollowThroughService`) | Event-driven notification on status change with per-tenant channel choice; WhatsApp in the same send path |
| C8 | Officer chat over a claim | BUILT | CODE, TESTS; NOT VERIFIABLE live | Right-hand "Claim context" panel (`CopilotPanel.tsx`) posts to `POST /api/v1/command-center/query`; claim-scoped actions in `CommandCenterActionService`; answers cite sources | Needs Azure OpenAI |

### D. Intake channels

| # | Item | Status | Verification | Evidence | What is missing |
|---|---|---|---|---|---|
| D1 | WhatsApp intake | PARTIAL | CODE, TESTS; NOT VERIFIABLE live | Provider: **Meta WhatsApp Cloud API**, direct (`WhatsAppCloudApiInboundProvider/OutboundProvider`), webhook `/api/v1/integrations/whatsapp/webhook` with HMAC signature check. "Kato" is an LLM tool-calling agent (`CustomerAssistantService.Agent.cs`, tools: get_my_account, list_products, search_policy_information, register_customer, apply_for_cover, file_claim, send_claim_link, record_contact_consent, hand_to_person). It opens a claim and sends the claimant link; it is told never to ask for papers in chat. Inbound media is stored and matched to a case or flagged for manual review (`WhatsAppInboundDocumentIngestionService`). Voice notes are transcribed (Sunbird); replies can be translated (AfriSLM, Sunbird) | Single tenant per deployment. Kato's products are the hard-coded microinsurance catalogue, not the tenant's configured products |
| D2 | Email intake | PARTIAL | CODE, TESTS; NOT VERIFIABLE live | Microsoft Graph mailbox polled every 5 minutes by `MailboxIngestionTimerFunction`; creates a case with `RequiresConfirmation=true`, `ClaimType="Email Intake"`, policy "UNKNOWN" when the sender is unmatched; attachments stored and sent to Document Intelligence (`GraphEmailAttachmentProcessor`) | Single tenant per deployment (reads deployment `TenantId`/`WorkspaceId`). Graph only. No claims-pack parsing beyond attachments |
| D3 | Customer memory (does not re-ask) | PARTIAL | CODE | Kato receives "already recorded" facts and `get_my_account`; conversation turns are stored (`CustomerConversationTurns`, retention 365 days); Customer 360 aggregates policies, claims, messages, documents, consent. Claimant portal does not reuse documents from earlier claims; I found no such logic | Cross-claim document reuse; memory outside the WhatsApp agent |

### E. AI and agents

**E1. Every AI call site**

| Where (file) | Service and model | What it does | Prompt location |
|---|---|---|---|
| `Api/Azure/AzureOpenAiProviders.cs` `AzureOpenAiChatCompletionProvider` (line 16) | Azure OpenAI chat deployment named by `AzureOpenAI:ChatDeployment` (model family not recorded in the repo; `AiCostEstimator` assumes gpt-4o prices), API version 2024-10-21 | Generic completion used by `ClaimsSummaryService`, others | Default system prompt at line 24, composed with the tenant's agent behaviour profile (`AgentBehaviourComposer`); claims-summary prompt built in `ClaimsSummaryService.BuildPrompt`, version string `claims-summary-v2` |
| same file `AzureOpenAiAssistantProvider` (line 140) | same | Grounded policy answers (staff command centre, claimant "policy questions") | Default prompt line 149 and `Api/Prompts/azure-openai-policy-answer.txt` |
| same file `AzureOpenAiCustomerUpdateDraftingProvider` (line 396) | same | Drafts customer updates | Default prompt line 405 |
| same file `AzureOpenAiEmbeddingProvider` (line 350) | `AzureOpenAI:EmbeddingDeployment` | Embeddings for knowledge indexing | none |
| `Api/Azure/AzureOpenAiToolCallingChatProvider.cs` | same chat deployment, with tools | Kato's multi-step WhatsApp agent loop | System prompt built in `Application/CustomerAssistant/CustomerAssistantService.Agent.cs` (~line 120-200) |
| `Api/Azure/AzureDocumentIntelligenceProvider.cs` | Azure Document Intelligence, model `DocumentIntelligence:DefaultModelId` (default `prebuilt-document`) | OCR, key-value extraction, field bounding boxes | none |
| `Api/Azure/AzureAiSearchProviders.cs` | Azure AI Search | Knowledge retrieval for grounding | none |
| `Application/SalesLeads/ConversationInsightsService.cs` | chat provider | Reads a prospect's WhatsApp conversation, records facts and next step | in service |
| `Application/Compliance/ComplianceQueryService.cs`, `Application/CustomerSummary`, `Application/Brief` | chat provider | Customer summary, daily brief, report insight | in services |
| `Infrastructure/Translation/AfriSlmTranslationProvider.cs`, `SunbirdTranslationProvider.cs`, `SunbirdVoiceNoteTranscriber.cs` | AfriSLM on llama.cpp; Sunbird API | Translation and voice-note transcription | none |

Agent catalogue (`Domain/Agents/AgentRegistry.cs`): 15 entries. ModelBacked 7 (command-center, claims-summary, customer-summary, policy-intelligence, customer-update-drafting, report-insight, conversation-insights), AiServiceBacked 1 (document-checker), RuleBased 6 (risk-fraud-signal, routing, underwriting-intelligence, prospect-triage, outreach, customer-assistant), NotImplemented 1 (knowledge-assistant). The code comment says "ten canonical agents"; the list has 15. `customer-assistant` is labelled RuleBased although Kato is an LLM tool-calling agent, so the registry label is stale.

**E2. Specialist agents**

| Agent | Status | Evidence |
|---|---|---|
| Document classifier | PARTIAL | Filename keywords plus 4 Azure DI type mappings, not a model (`Classify`). Contains a defect, see Section 5 |
| Field extractor | PARTIAL | Azure DI generic extraction; no schema per document type; not written into claim fields |
| Authenticity / photo checker | MISSING | No EXIF, tamper or duplicate-image logic anywhere (searched) |
| Completeness checker | BUILT | Rule-based `CompletenessScoringService` plus `StaticDocumentChecklistProvider`, TESTS (`DocumentCompletenessTests`) |
| Fraud scorer | PARTIAL | `Domain/Fraud/FraudRuleEngine.cs`: deterministic advisory signals (duplicate contact, claim velocity, duplicate policy, low completeness, early claim). Human disposition workflow exists. No model scoring, no numeric fraud score |
| Recommendation writer | PARTIAL | `GET /api/v1/cases/{id}/recommended-actions` and claims-summary open questions/risk signals; `ClaimRoutingPolicy` for routing advice (rule-based) |

**E3. Parallel/background or on demand?**

- Document extraction runs **synchronously inside the upload request**, one file after another (`ClientPortalEndpoints.UploadDocuments`). The `document-processing` Service Bus queue name is configured but no consumer exists (the only references are config and a health listing in `WorkflowShell.cs`).
- Claims summary and risk signals run in the **background** after intake via an in-process bounded `Channel` (capacity 500, drops oldest, lost on restart, by design per its comment). It is triggered at intake, not when a document lands; a later document does not re-trigger it.
- Kato replies run in a second in-process queue.
- Timers: missing-document reminders and SLA warnings (`CaseFollowThrough`), mailbox ingestion (5 min), retention.
- A configurable agent-workflow engine exists (`AgentWorkflows`, Service Bus-driven `AgentWorkflowNodeExecute`) with human-review gates, not wired to document arrival in anything I found.
- Everything else (summary refresh, extraction approval, chat) is on demand.

**E4. Token and usage metering per company**

PARTIAL, effectively not working. `AgentActionLog` has `PromptTokens`, `CompletionTokens` fields and the Azure providers return usage numbers, but no code assigns them into `AgentActionLog` (grep for any setter found none; only readers in `BillingEndpoints` and `AssistantMapper`). `RecordBackedBillingUsageSource` therefore reports `ai-tokens` as `Metered=false` (CODE). The claims meter counts claims with a hard-coded limit of 5000; documents and storage meters are `Metered=false`. Scope is per tenant/workspace because the logs are scoped. `AiCostEstimator` uses fixed gpt-4o 2024-08 list prices.

### F. IIP Academy

| # | Item | Answer |
|---|---|---|
| F1 | What exists | In `web/portal`: catalogue page `/app/academy`, course page `/app/academy/[courseId]`, `CoursePlayer`. 4 courses (claims, underwriting, fraud-identity, governance) x 5 modules, content as TypeScript data in `lib/academy/courses.ts` (406 lines) with screenshots in `public/academy/`. Per-module goal, outcomes, worked example, exercise, takeaways, then check questions, then a certificate PDF generated in the browser with jspdf. Prices per course stored per workspace (`AcademyPricings`, `GET/PUT /api/v1/academy/pricing`), default USD 50. **No enrolment, no payment, no video** (0 hits for video). Quizzes: check questions in the player; exercise marking is not implemented (stated in `docs/iip-academy-build-notes.md`). Completion is stored in the learner's browser `localStorage` (`lib/academy/progress.ts`) |
| F2 | In this repo or separate? Deployed? | In this repo, inside the staff portal, linked from the sidebar under "Inbox". Deployed: the dev portal App Service is described as running this app, but I did not verify the live site (`az` not used). Academy pages rendered locally |
| F3 | What stops a real learner using it | Needs a staff sign-in with a workspace membership (it lives under `/app`, no public or learner route); no account type for learners; no enrolment record; no payment (the pricing page says nobody is charged); progress and certificates are browser-local, so not durable, not portable across devices and not verifiable; no video; exercises unmarked; legal and regulatory statements left as "to be confirmed" per the build notes |

### G. Billing and plans

PARTIAL for plans and usage, MISSING for payments. `Domain/Billing/BillingModels.cs`: a code-defined `EntitlementPackage` ("IIP Operations, Standard", 15 modules, seat limit 50) and `UsageMeter` records; `GET /api/v1/admin/billing` shows them. ADR-0003 states "no external billing/payment integration". No card or mobile money integration exists: grep for Stripe, Flutterwave, Pesapal, MTN or Airtel payment code finds only text in Kato's microinsurance scripts that say customers pay by mobile money (the integration is not built). There is no per-tenant plan assignment, no subscription state, and no usage limit enforcement except the displayed 5000-claim meter.

### H. Data protection and security

| # | Item | Status | Evidence |
|---|---|---|---|
| H1 | Files: where stored, encryption, retention/deletion | PARTIAL | Azure Blob, container `raw-documents`, `PublicAccessType.None`, keys prefixed by tenant and workspace. No application-level or customer-managed-key encryption in code; at-rest encryption would be Azure's platform default, which I did not verify in Bicep or the portal. Integration secrets (WhatsApp, SMS, SMTP, translation) are encrypted with ASP.NET Data Protection and are write-only from the browser. Retention: only one data class exists (`RetentionDataClass.CustomerConversationTurns`, default 365 days) enforced by a timer; **claim documents and cases have no retention or deletion job**. Data-subject erasure anonymises the customer record's name, email and phone only (`DataSubjectRightsService.cs:153-170`); case rows, documents and blobs are not erased |
| H2 | Consent and privacy notice in the customer flow | PARTIAL | Contact consent exists (`ConsentRecords`, `/api/v1/customers/{id}/consent`, Kato `record_contact_consent`, sending is gated on it). The claimant portal pages contain no privacy notice and no consent capture (grep for "privacy" and "consent" in `ClaimAccess*.tsx` returned nothing) |
| H3 | Audit logs | BUILT | TESTS, LIVE (counts on seeded data) | `AuditEvents` table; typed `AuditEvent` factories for uploads, processing, completeness, status, claimant sessions, magic-link requests, sends; `GET /api/v1/audit/events`, per-case timeline, `/app/audit`, evidence pack. Each case response carries audit count and last action |

---

## Section 4: How a claim actually moves today

Walked through for the path with the most code behind it (WhatsApp, Kato), then the other entry points. Steps marked "manual", "missing" or "not verifiable".

1. **Customer reports a loss.** Three ways in:
   - *WhatsApp*: Kato (LLM agent) takes the details, reads them back, customer says YES, `file_claim` creates a `ClaimCase` (Channel WhatsApp). Requires the customer's number to match the policyholder on record. NOT VERIFIABLE live (needs Meta, Azure OpenAI). Tenant-bound to the deployment.
   - *Claimant web*: customer enters policy number and email/phone at `/access`, receives a link, opens `/access/new-claim` and files (`CreateClaim`). LIVE up to the link; delivery by SMS/email not verified.
   - *Email*: mailbox polled every 5 minutes, a case is created with `RequiresConfirmation=true`, policy "UNKNOWN" if the sender is unmatched. **Manual**: an officer must confirm the case (`POST /cases/{id}/confirm`) before an SLA clock starts.
   - *Staff*: officer creates it on `/app/claims/new`.
2. **Product pinning.** At intake `CaseIntakeService` matches `ProductLine` text to a published product by name and pins `ProductId` and `ProductVersion`. If no product matches, the hard-coded checklist is used. (CODE)
3. **Claim link to the customer.** Kato sends it in chat (24 h, single use). For other channels the customer must request it. **Missing**: no staff "send link" action found.
4. **Customer uploads documents** into slots. Each upload is stored in blob, then **synchronously** sent to Document Intelligence, classified by filename and DI type, validated by tenant field rules, and the completeness score is recomputed. LIVE: with Azure not configured the request returns 503, the staff-route variant left the file saved in state `Uploaded` and never processed. **Missing**: scanning, PDF conversion, renaming, evidence-original preservation, retry.
5. **AI work.** On intake an in-process queue runs the claims-summary agent (summary, open questions, risk signals). **Manual**: officer presses "Generate summary" to refresh after new documents. Rule-based fraud signals are produced on demand. **Missing**: authenticity check, extracted fields flowing into the claim.
6. **Missing documents.** Completeness drives "Missing documents" tabs. Reminder to the customer goes out automatically after 3 days of a missing document (timer), or an officer drafts a request. Sending needs human approval and works over Email/SMS only.
7. **Officer review.** Officer opens the claim, reads the summary, checks extractions (approve or correct fields), asks questions in the copilot panel, assigns or escalates. **Manual**: all of it. There is no "set status" control; status moves only via assign, escalate, approval, resolve.
8. **Approval.** Consequential steps use the approvals queue with human decision and audit reason; AI cannot decide (enforced by `ApprovalDecisionGuard`, TESTS).
9. **Resolution.** `POST /cases/{id}/resolve` sets Settled or Closed (from ReadyForExport or Escalated), drafts a satisfaction question and a referral invitation for human approval.
10. **"Insurer has what they need."** The only export is a JSON evidence pack. **Missing**: summary PDF, renamed documents, zip, automatic email to the insurer. **Manual**: the officer downloads the JSON and collects the files from the claim screen.
11. **Customer visibility.** Customer sees status words, completeness and customer-safe messages on `/access/claim`. Status changes do not push a message by themselves.

---

## Section 5: Quality and risk

### Tests

- Backend: 1764 tests passed (Domain 681, Api 1083), 0 failed, 0 skipped, Release configuration. Coverage includes tenant authority, human-review contracts on every path, upload validation, magic-link auth, WhatsApp, SMS providers, extraction golden fixtures, retention, DSR. Note these tests use the mocks in `LocalTestDoubles`; none call Azure.
- Portal: lint clean. Static check scripts: 4 pass, **`check:action-permission-parity` fails with 17 role/permission mismatches** (for example `CustomerService` holds `ManageProducts` and `ManageTenantOnboarding` server-side but not in `PREVIEW_PERMISSIONS`; `Broker` has `ViewAssignedClaims` in the preview map but not on the server). 23 Playwright spec files exist (visual, trust-truth, authority-permissions, runtime-recovery, deployed-profile, marketing); not run here. `tests/saas-e2e/` is an empty folder.
- No end-to-end test against live Azure providers was run.
- TODO/FIXME/HACK markers: none found in `src`, `tests`, `web/portal` app, components, lib and pages.

### Defects and risks found (each from reading the code unless marked LIVE)

1. **Document classifier substring bug.** `AzureDocumentIntelligenceProvider.Classify` falls back to `fileName.Contains("id")` which maps any filename containing "id" (for example "accident_report.pdf", "valid.pdf", "paid_receipt.pdf") to `CustomerId`, after the more specific checks. Also, only 4 of 8 types can ever be recognised from the DI document type; the rest depend on the filename (`IMG_2031.jpg` is `Unknown`).
2. **Completeness versus slot status.** `StaticDocumentChecklistProvider` marks a built-in required document received only if the **classifier's** `DocumentType` matches, while the customer page marks the slot Received by the **customer's chosen** `DocumentTypeKey`. A customer who uploads a correctly chosen but unrecognised file sees "Received" while the score does not count it. Custom types use the customer key, so the two rules differ by type. Not reproduced live (needs processing).
3. **Orphaned upload on failure.** When processing is unavailable the file is already saved with status `Uploaded` and stays that way; the request returns 503. LIVE (staff upload route). The customer slot would read "Being checked" indefinitely.
4. **Local "healthy" indicators are misleading.** In Development the portal footer says "All 10 providers healthy" and Provider health shows documentIntelligence and azureOpenAI "Operational", while `/api/v1/system/capabilities` reports `activeAi:false` and uploads return 503 (LIVE). Mock-mode status is presented as healthy; Development only.
5. **Photo downscale loses originals** (B6) and there are two near-duplicate compressors (`compressImageForUpload.ts` 1800px/0.75, used by the staff widget; `photoDownscale.ts` 1600px/0.85, used by the claimant page).
6. **Scale risks.** `caseRepository.ListAsync(scope)` loads every case for the tenant on each magic-link request, each follow-through run and in billing (`Take: 5000`). Unauthenticated magic-link requests therefore trigger a full scan per request. JSON-in-SQL storage means no relational constraints.
7. **In-process queues lose work** on restart or deploy (documented in code); extraction is not queued.
8. **Registry drift.** Agent catalogue comment "ten canonical agents" versus 15 entries; `customer-assistant` labelled RuleBased though LLM-driven. `UserRole` and `PlatformRole` coexist.
9. **Token metering never populated** (E4).
10. **Retention and erasure are narrow** (H1).
11. **No privacy notice** in the claimant flow (H2).
12. **Claimant portal tenant comes from the browser.** The claimant client sends `X-Tenant-Id`/`X-Workspace-Id` from build-time `NEXT_PUBLIC_TENANT_ID`/`NEXT_PUBLIC_WORKSPACE_ID` (`lib/clientPortal.ts:78`). The code comment says knowing the tenant id alone grants nothing and the session is the gate; the practical effect is below.
13. **UI direction not applied.** `DESIGN.md` describes a violet-accent, icon-rail, border-led design that replaces the current portal. At 1280px the running portal uses a blue accent and a full labelled sidebar, so the new direction is not in the app yet (LIVE screenshots).
14. **`Program.cs` is 1230 lines** with ~260 route registrations in one file.
15. `appsettings.Development.json` points Development at the real Service Bus namespace.

### Hard-coded insurer- or product-specific logic

- `Application/CustomerAssistant/MicroinsuranceCatalog.cs`: insurer names "Nile Shield Assurance", "Mpingo Life Assurance", "Kifaru General Insurance", policy prefix `NSA`, 10 named products with prices in UGX, waiting periods, IRA microinsurance ceiling. `KatoBrand.Current` is a process-wide static defaulting to Nile Shield.
- `MicroinsuranceDemoSeeder`, `DemoSeedService`, `BankCustomerDemoSeeder`: demo content for those insurers.
- Uganda and IRA: `DefaultClaimStageLibrary` (8 stages, IRA service-standard days), publish gate requiring an IRA approval reference, SMS auto-send policy for Uganda DPPA/UCC, UGX throughout, EgoSMS and Africa's Talking.
- Motor and Medical: `ClaimTypeClassifier`, `DocumentChecklistRules`, `MotorFnolDetails`, `MedicalFnolDetails`.
- `ClientPortalEndpoints.Label()/Description()` for built-in document labels.
- `AiCostEstimator` gpt-4o prices; `DefaultEntitlements` (50 seats); follow-through timings.

### What breaks if a second insurer onboards tomorrow

Data separation itself holds (tenant columns, scoped repositories, tenant-prefixed blobs, membership-based authorization). These do not work for a second tenant in the same deployment:

1. **WhatsApp**: one `PhoneNumberId` and one tenant per deployment; the webhook resolves a single `Current` settings object (`WhatsAppRuntimeSettingsProvider`), and filters inbound messages to that one number.
2. **Email ingestion**: `MailboxIngestionTimerFunction` and `RetentionEnforcementTimerFunction` and `CaseFollowThroughTimerFunction` run only for `options.TenantId/WorkspaceId`, with no tenant enumeration.
3. **SMS delivery webhook**: single path secret and "the one deployment-configured tenant scope".
4. **Claimant portal**: tenant fixed at portal build time by env var; claimant links carry only a token, no tenant. Two insurers need two portal builds, or code changes.
5. **Kato branding and catalogue**: process-wide static, set once from config.
6. **CORS** is a single `Portal:CorsOrigin`; `ClientPortalBaseUrl` is a single origin.
7. **Onboarding** requires the insurer to have an Entra tenant and an IIP operator to run the consent flow; no self-serve path.
8. **Product publishing** requires an IRA approval reference (Uganda).
9. **Claim types, claim fields, document recognition and claim-type rules** are Motor/Medical-centric code; a new insurer's other products fall back to generic rules.
10. **Cost**: AI usage is not metered per tenant, so nothing can be billed back.
11. Seed/demo helpers and the "tenant-demo" scope exist in code; the demo seeder must not run in a shared deployment.

---

## Section 6: Gap list

Effort is the estimated hours of build and test for one engineer, with the existing code as the base, excluding Azure account or credential setup and third-party approvals (Meta, SMS sender registration).

| Item | Status | What is needed | Rough effort (hours) | Dependencies |
|---|---|---|---|---|
| A1 Self-serve company signup | PARTIAL | Public sign-up page, tenant and workspace creation without operator, non-Entra identity option for small insurers, email verification, admin bootstrap | 60 | Identity decision (Entra External ID or similar); A9 per-tenant channel binding |
| A2 Add products (non-IRA gate) | BUILT | Make the IRA reference requirement a per-tenant setting | 6 | None |
| A3 Claim types per product | MISSING | `ClaimType` model under `Product`, admin UI, intake dropdown, replace keyword classifier, migrate existing free-text claim types | 30 | A4, A5 |
| A4 Required documents per claim type | PARTIAL | Key the document matrix by claim type, checklist resolution by claim type, UI | 20 | A3 |
| A5 Claim fields per claim type | MISSING | Field-definition model (type, required, options), dynamic form renderer for staff and claimant, storage replacing the two FNOL records | 50 | A3 |
| A6 Wording upload, AI draft for human confirmation | MISSING | Extraction pipeline from wording to covered items, exclusions, required documents with citations; confirm UI that writes into the product | 45 | Azure OpenAI; A3, A4 |
| A7 Guided onboarding wizard or agent | MISSING | Step flow or conversational agent that creates products, claim types, documents and fields through the same APIs | 45 | A3, A4, A5, A6 |
| A8 Preview as customer | PARTIAL | Render the claimant page from a draft product with sample claim | 14 | A3, A4 |
| A9 Per-tenant email and WhatsApp | PARTIAL | Webhook routing by `PhoneNumberId` to tenant, iterate tenants in timers, per-tenant Graph mailbox ingestion, IMAP option if required | 40 | Meta and Graph credentials per tenant |
| B1 Claim link delivery and staff-sent link | BUILT | Staff action to send a link from the claim, verify SMS/email delivery with real providers | 10 | SMS/email provider |
| B2 Tenant label for built-in documents | BUILT | Use the tenant catalogue `CustomerLabel` and matrix labels in the claimant page | 4 | None |
| B3 Slot upload end to end | PARTIAL | Run against Azure Blob and Document Intelligence, fix failure state (no orphaned `Uploaded`), add `capture` option | 12 | Azure providers |
| B4 In-browser document scanning | MISSING | Camera capture, edge detection, crop, perspective correction, contrast clean-up (for example OpenCV.js or jscanify), fallback for low-end devices | 40 | None |
| B5 Document photo to PDF | MISSING | Client or server conversion, multi-page assembly per slot | 16 | B4 |
| B6 Evidence originals and metadata | PARTIAL | Distinguish evidence from document, upload original with EXIF, store both | 14 | B3 |
| B7 Auto classification and renaming | PARTIAL | Model-based classifier for unslotted files, fix the "id" substring defect, filename rule `<Type>_<ClaimRef>` applied to stored name, show to staff | 30 | Azure OpenAI or Document Intelligence custom model |
| B8 Completeness and slot status consistency | BUILT | One rule for built-in types using the customer key plus classifier | 8 | None |
| B9 Customer-friendly status wording | BUILT | Mapping from `CaseStatus` to customer copy, configurable timeline | 8 | Copy sign-off |
| B10 Slow-network upload | PARTIAL | Progress, retry with backoff, chunked or resumable upload, size targets, test on a throttled low-end Android profile | 24 | None |
| C2 Extraction into claim fields | PARTIAL | Per-document-type extraction models or LLM extraction, map to claim fields, human confirm | 40 | A5, Azure |
| C3, C8 AI summary and officer chat | BUILT | Run and tune against live Azure OpenAI, re-run summary when documents land | 12 | Azure OpenAI |
| C4 Claims pack (PDF, renamed docs, zip) | PARTIAL | PDF summary generation, bundle documents with new names, zip, download endpoint | 30 | B7 for naming |
| C5 Pack emailed automatically | MISSING | Trigger on ready state, recipient config per tenant, attachment send, audit | 14 | C4, email provider |
| C6 Officer status change | PARTIAL | Audited status change action with allowed transition table and reason | 14 | None |
| C7 Status-change notifications | PARTIAL | Event on status change, per-tenant channel and template mapping, WhatsApp in the send path, still behind human approval policy | 24 | A9 |
| D1 WhatsApp intake (multi-tenant, tenant products) | PARTIAL | Per-tenant routing, Kato reads tenant products instead of the hard-coded catalogue, document intake by slot | 40 | A9, A2 |
| D2 Email intake (multi-tenant, pack parsing) | PARTIAL | Per-tenant ingestion, match attachments to slots, classify | 24 | A9, B7 |
| D3 Customer memory | PARTIAL | Reuse documents and details across claims, prefill claimant forms | 20 | A5 |
| E2 Authenticity and photo checker | MISSING | EXIF and tamper checks, duplicate-image detection, advisory flag with human review | 40 | B6 |
| E2 Fraud scorer | PARTIAL | Optional model-assisted scoring behind human disposition | 24 | Data |
| E2 Recommendation writer | PARTIAL | Dedicated recommendation agent with citations | 16 | Azure OpenAI |
| E3 Document-triggered background agents | PARTIAL | Queue document processing (consume `document-processing`), fan out classifier, extractor, completeness, authenticity when a document lands, durable queue instead of in-process | 40 | Service Bus, B3 |
| E4 Token metering per company | PARTIAL | Persist token usage on every AI call, per-tenant aggregation, limits, cost model by actual deployment | 16 | None |
| F Academy usable by real learners | PARTIAL | Learner accounts and access outside staff roles, enrolment records, server-side progress and certificates with verification, payment, video, exercise marking | 80 | G payments, A1 identity |
| G Billing and plans | PARTIAL / MISSING | Plan model per tenant, subscription state, usage limits enforced, card and mobile money integration (MTN, Airtel) with reconciliation | 80 | E4, payment provider contracts |
| H1 Retention and erasure | PARTIAL | Retention classes for documents, cases and communications, deletion of blobs, full erasure workflow, evidence of deletion | 30 | Legal retention periods |
| H1 Encryption evidence | PARTIAL | Confirm storage and SQL encryption in Bicep, optional customer-managed keys | 8 | Infra access |
| H2 Privacy notice and consent in claimant flow | PARTIAL | Notice text, consent capture and record before upload, link to withdrawal | 10 | Legal copy |
| Multi-tenant claimant portal | PARTIAL | Tenant resolved from link or hostname instead of build-time env, CORS and base URL per tenant | 16 | A1 |
| Portal parity check failures | BROKEN | Fix the 17 mismatches between `lib/roles.ts` and `RolePermissionMap.cs` | 4 | None |
| Local provider-health honesty | PARTIAL | Mock-mode status should report Not configured for providers that return 503 | 4 | None |
| Scale: full-scan queries | PARTIAL | Indexed lookups by policy number and status instead of `ListAsync` scans | 16 | None |
| New UI direction (`DESIGN.md`) | PARTIAL | Apply violet accent, icon rail and six-state coverage across screens | 120 | Design sign-off |

Items marked BUILT in Section 3 but listed above are BUILT with a specific refinement noted; all other BUILT items (A2 core, B8 core, C1, H3) need no work beyond what is listed.
