# Insurance Intelligence Platform (IIP): the handbook

> Status: Active
> Owner: Platform Engineering
> Last reviewed: 2026-11-07
> Purpose: the one file a new developer reads. It states what IIP is, how it is built, how to run, test and deploy it, the rules that are never waived, and where the code and the evidence stand today. It condenses the ~430 files under `docs/`; when a detail matters beyond this file, section 17 says which document owns it.

How to use it: read sections 1 to 4 (including the agent and orchestration architecture in 4.4 to 4.6) on day one, run the system with section 7, and keep sections 9 to 12 open while you work. Everything here was checked against the repository and its branches on 2026-11-03. Where a statement describes a branch that has not been merged yet, it says so.

---

## 1. What IIP is

IIP is a tenant-isolated insurance operations platform for African insurers, brokers, TPAs, HMOs and bancassurance partners. It receives fragmented work (WhatsApp messages, emails, web forms, uploaded documents), turns it into structured claim files, prepares evidence-grounded summaries and recommendations, and routes the work to accountable staff.

The staff application is **IIP Operations**. The first product wedge is **motor claims and document intelligence**; the same core also carries customer acquisition, fraud signals, policy knowledge and underwriting support.

The rule that outranks every other rule:

> AI prepares insurance work. Accountable humans make consequential insurance decisions.

AI may summarise, classify, extract, flag, draft, route and recommend. It must not approve or reject claims, release payments, bind policies, set final premiums, make adverse underwriting or fraud determinations, or send final regulated communications. Review scores, authenticity checks and recommendations are advice. Every status change, approval and customer message is a person's audited action. The platform preamble that states this boundary is not editable by a tenant: a company may configure an agent's voice and scope, never its authority.

## 2. Two editions, one codebase

| | **SaaS edition** (`main`) | **Azure Managed Application edition** (`managed-app`) |
|---|---|---|
| For | Insurers, brokers and agents who sign up and start with no infrastructure | Larger regulated insurers who require data to stay in their own Azure subscription |
| Runs | Otic-hosted Azure, many companies on one deployment | Packaged into the customer's subscription, one insurer per deployment |
| Tenant model | Logical isolation: every request scoped by tenant and workspace on the server | Customer subscription is the outer boundary; tenant and workspace scoping still applies inside |
| Sign-in | Self-serve sign-up, password sign-in, invitations; Microsoft Entra also supported. A person who belongs to several organisations is asked to choose one (the API refuses to guess) | Microsoft Entra only (`INSURANCEINTELLIGENCE_AUTH_MODE=Entra`), single tenant |
| Commercial | Plans, allowances, payments (MTN MoMo, Airtel Money, card), usage metering | Azure Marketplace packaging and publisher metering; no in-app plans |
| Channels | Each company brings its own WhatsApp number, mailbox, optional hostname | The insurer configures its own providers once, in its own subscription |
| SaaS-only modules | Academy, organisations, API keys, outbound webhooks, marketing pages, demo scenarios, platform MFA | Removed by the `managed-app` cut; a CI check keeps them out |
| Support access | Otic operates the service | Publisher access is explicit, limited, auditable and revocable |

Sections 3, 4, 8 to 14 are shared by both editions. The module-by-module split lives in the consolidation plan (section 16). An earlier, separate direction, the "SaaS Fabric Edition" (customer data in the customer's Microsoft Fabric workspace, Marketplace SaaS offer), is documented at the top of the `Projects/` folder (`IIP_SaaS_Fabric_Edition_Implementation_Guide.md`, `LOCAL_DEVELOPMENT.md`, `SAAS_STATUS_AND_PLAN.md`). It is not part of either edition and its local-development instructions do not apply here.

## 3. What the platform does (shared core)

**Claims.** Claim types, required documents and claim questions per product; setup from a policy wording (proposals shown with the quoted clause, accepted item by item); a claim link a customer opens on a phone; a privacy notice and consent step; a photo or file per document slot; in-browser scanning and photo-to-PDF; original evidence stored with hashes; automatic recognising and renaming of documents; extracted values offered to the officer; authenticity checks; a review score with its reasons; claimant-facing status wording and timeline; officer status changes with customer notifications; a claims pack downloaded or emailed automatically; "preview as customer" for product setup.

**Channels.** WhatsApp, email and a claimant portal, with per-customer memory so the assistant knows what was said before. Social and messaging channels feed one inbox, with channel identity links and customer follow-ups. Local-language replies use a translation engine (AfriSLM served by llama.cpp on Azure Container Apps). The assistant always tells the customer that a person will decide.

**Knowledge and agents** (full architecture in sections 4.4 to 4.6). Policy questions are answered only from approved, indexed sources, with a citation that opens the source. Agents run through a registry with a kill switch; each run is recorded as an `AgentRun` with audit events. No generic SQL tool is ever exposed to an agent, and no agent tool may approve, reject, release payment, bind, accuse of fraud or send a final regulated communication.

**Governance.** Server-side permissions, an audit trail with correlation ids, retention periods per record kind, legal hold, and erasure of a person (the claim record is kept for audit; the person's name, phone, email, text, documents, chats, consents and portal access are removed). Token use is metered per call with a cost estimate.

**Honest providers.** A provider that is not configured shows `Not configured`. Protected runtimes (Staging, Production, ManagedApplicationReviewer, and deployed Demo) never use mocks, seeded business data or fallback-to-mock. Mocks exist only for local development and automated tests.

## 4. Architecture

```text
Channels:  WhatsApp | Email | Web forms | Claim link | Staff uploads
              |
InsuranceIntelligence.Api (.NET 8)  authentication, tenant/workspace resolution, permissions, audit
              |
Application   use cases, ports, authorization and audit policy
Domain        business rules and meaning
              |
Infrastructure   EF Core / Azure SQL, Blob, Search, OpenAI, Document Intelligence, Graph, provider adapters
InsuranceIntelligence.Functions   background and event processing
              |
web/portal (Next.js, React, TypeScript, pnpm)   IIP Operations and the claimant pages
```

### 4.1 Projects and dependency direction

`Domain <- Application <- Infrastructure <- API / Functions`. The portal consumes versioned HTTP contracts only and is never business authority.

| Project | Responsibility | May depend on |
|---|---|---|
| `src/InsuranceIntelligence.Domain` | Business meaning, entities, value objects, invariants | nothing above it |
| `src/InsuranceIntelligence.Application` | Use cases, ports, permissions, tenant policy, human gates, audit policy | Domain |
| `src/InsuranceIntelligence.Infrastructure` | EF Core, Azure, email, communication and provider adapters, migrations | Application, Domain |
| `src/InsuranceIntelligence.Api` | HTTP transport, authentication middleware, composition | Application, Infrastructure, Domain |
| `src/InsuranceIntelligence.Functions` | Timers and queue/event triggers (retention, mailbox ingestion, agent workflow nodes, Teams notifications) | as required |
| `src/InsuranceIntelligence.LocalTestDoubles` | Isolated test adapters, never active in a protected runtime | Application, Domain |
| `web/portal` | Next.js IIP Operations portal | API contracts |
| `tests/InsuranceIntelligence.Domain.Tests`, `...Api.Tests` | .NET tests | |
| `infra/` | Bicep, Marketplace packaging, ADRs, deployment scripts | |

Domain areas present in the live code include: Academy, Acquisition, Administration, AgentWorkflows, Agents, Approvals, Audit, Billing, CaseReview, Cases, Channels, ClientPortal, Communications, Compliance, Contestability, CustomerAssistant, Customers, Distribution, Documents, Fraud, Integrations, Knowledge, Notifications, Policies, Products, Queue, Retention, Scheduling, Storage, Tenants, Translation, Underwriting, Users, Workflow. The claims-core build added DataLifecycle, Identity, Jobs and Uploads, and the SaaS lineage added Marketplace, Organisations, Security, Usage and Webhooks; all of them are in `main`.

### 4.2 Technology

| Concern | Technology |
|---|---|
| Transactional data | Azure SQL through EF Core; additive migrations only |
| Documents and evidence | Azure Blob Storage |
| Knowledge retrieval | Azure AI Search |
| AI | Azure OpenAI (chat and embeddings), Azure AI Document Intelligence |
| Async work | Azure Functions, plus a durable database queue (Service Bus is optional) |
| Secrets and identity | Key Vault (RBAC mode), managed identities, Microsoft Entra |
| Operations | Application Insights, Log Analytics |
| Infrastructure as code | Bicep under `infra/` |
| Portal | Next.js, React, TypeScript, Tailwind, pnpm, Node 20 |
| Backend | .NET 8, ASP.NET Core |

### 4.3 How tenancy, identity and providers work

- **Tenant and workspace** are resolved on the server for every request and every background action. A token, a route value, a body value, a browser-selected workspace, an app role, a group or a subscription id is never business authority on its own. Membership is a server-held record; identity claims are only a hint used to look it up.
- **Providers are swapped by environment variable**, each with a `Mock` (development and tests only), and a real value. For example `INSURANCEINTELLIGENCE_AI_PROVIDER`, `..._DOCUMENT_PROVIDER`, `..._RETRIEVAL_PROVIDER`, `..._BLOB_PROVIDER`, `..._QUEUE_PROVIDER`, `..._EMAIL_PROVIDER`, `..._COMMUNICATION_PROVIDER`, `..._NOTIFICATION_PROVIDER`, `..._SMS_PROVIDER`, `..._PAYMENT_PROVIDER`, `..._STORAGE_PROVIDER`. `AzureProviderStartupValidator` refuses to start a protected runtime with a mock, simulated or placeholder value, with seeded demo data, or with a missing required provider.
- **Runtime profiles** (`INSURANCEINTELLIGENCE_RUNTIME_PROFILE`): `Development`/`LocalTest` (mocks allowed), `Demo` (deployed synthetic sandbox: real providers and Entra, synthetic data in the isolated `tenant-demo` tenant, a mandatory banner), `Staging`, `Production`, `ManagedApplicationReviewer` (all protected). The review mode is classified server-side and returned on `GET /api/v1/me` under `environment`, so the browser cannot spoof it.
- **Authentication modes**: `Entra` (real), `Development` (header identity, local only). Local demo headers are `X-Tenant-Id`, `X-Workspace-Id`, `x-demo-role`, `x-demo-user-id`, `X-User-Id`, `X-Correlation-ID`; they must never be trusted outside local development.
- **API versioning**: all routes live under `/api/v1` (ADR-0004). Health is `/health`; provider health is `/api/v1/provider-status`.

### 4.4 Agent architecture

**Model.** An agent is a named, code-defined capability in `AgentCatalog` (`src/InsuranceIntelligence.Domain/Agents/AgentRegistry.cs`). Each entry declares its key, purpose, risk level, whether human approval is required, how it is implemented, and the typed tools it may call. Agents never touch the database directly and never hold final authority. They:
- receive tenant, workspace and role from the backend request pipeline (or, for a webhook with no signed-in user, an explicit scope and a system actor);
- act only through scoped, typed tools that check tenant, permission and sensitivity themselves;
- cite or reference evidence wherever they state an operational fact;
- record an `AgentRun` and an `AIInteraction` for every run, and an `AuditEvent` for every sensitive output;
- fail closed when a tool, provider, permission or source is unavailable (they say what is missing, they do not invent).

**Registry as control plane (ADR-0002).** The registry stores constraints, never grants. A tenant can disable an agent (the kill switch, a governed and audited human admin action, per agent or tenant-wide), and a disabled agent fails closed (`Not configured` or `Disabled by administrator`), never a silent fallback. No registry setting, confidence threshold or approval rule can enable an agent to approve, reject, settle, pay, bind, underwrite or accuse; `ApprovalDecisionGuard.EnsureHumanForConsequentialDecision` stays the backstop regardless. Adding or removing an agent from the catalog needs an ADR. The kill switch is enforced at invocation through `IAgentGate`, including a scope-explicit overload for webhook-driven agents. Run logs are written only when an agent really executes.

**The catalog today.** 16 agents on `main`; the sixteenth, `claim-recommendation`, came with the claims-core build. The first ten staff agents below are the canonical ten of the architecture; the rest were added later.

| Key | Kind | What it does (and its limit) |
|---|---|---|
| `command-center` | model-backed | Chat-first operational agent: briefings, triage, SLA review, metric explanation, action preparation. Returns typed response blocks, never executes a final action |
| `claims-summary` | model-backed | Summarises a claim from retrieved tool results; runs automatically on every new claim as `system:claim-triage` and on demand |
| `customer-summary` | model-backed | Briefs staff on one customer from that customer's own records only, every point citing its record; no knowledge base, contact details withheld |
| `document-checker` | AI-service-backed | Azure Document Intelligence OCR and field extraction plus checklist rules; no chat model |
| `policy-intelligence` | model-backed | Explains policy and coverage from approved, tenant-scoped sources with citations; also answers the customer assistant's general questions |
| `risk-fraud-signal` | rule-based | `FraudRuleEngine`: duplicate contact, claim velocity, duplicate policy, low completeness. Advisory indicators only; wording is "fraud-risk indicators requiring human review", never "fraudulent" |
| `claim-recommendation` (claims-core) | model over rules | Rules decide the advice and every reason; the model only writes a short explanation, dropped if it contains decision or accusation wording (`AdvisoryLanguage`) |
| `customer-update-drafting` | model-backed | Drafts customer-safe messages; sending needs a recorded human approval |
| `routing` | rule-based | `ClaimRoutingPolicy`: assignment, priority and escalation proposals |
| `knowledge-assistant` | not implemented | In the catalog, cannot be enabled; staff policy questions go to `policy-intelligence` |
| `report-insight` | model-backed | Management summaries from real aggregates, labelled partial or final |
| `underwriting-intelligence` | rule-based | `UnderwritingRuleEngine`: referral and missing-information notes only; no intake or decision endpoint exists |
| `conversation-insights` | model-backed | Reads a prospect's WhatsApp conversation after each reply and records what they said, interests and a suggested next step |
| `prospect-triage` | rule-based | Scores prospects and names the two best-fit covers; never contacts anyone |
| `outreach` | rule-based | Drafts personal messages and follow-ups; a named person approves every message |
| `customer-assistant` | rule-based classification | Customer-facing WhatsApp assistant, see 4.6 |

Kinds are read from `AgentCatalog` on 2026-11-03. Trust the catalog and the implementation table in the agent architecture document over any written count.

**Tools and side-effect levels.** Every tool is declared with its allowed agents, required permissions, input and output schemas, tenant-scope requirement, audit requirement, side-effect level, provider health check and failure behaviour. Side-effect levels: `read` (tenant-scoped reads), `draft` (a proposal only), `write` (internal state, permission and audit, human gate if consequential), `external_send` (recorded human approval required), `approval_required` (creates or depends on an approval workflow). Agents prefer `read` and `draft`; the others are mediated by backend services. Examples: `knowledge.search` (returns citations and source ids, uses Azure AI Search), `claim.read`, `document.extraction.read` (exposes confidence and source), `customer_update.draft`, `command.action.prepare`, `approval.create` (creates an approval task only, never self-approves), `provider_health.read`. `audit.write` is server-side only. There is no generic SQL tool.

**Prompt composition and tenant configuration.** `AgentBehaviourComposer.Compose` builds the real system prompt in a fixed order: an immutable `PlatformPreamble` (the human-decision boundary in imperative form, never tenant-editable, always first), then the tenant's configured prompt or the agent's default, then tone, refusal topics with an exact escalation message, a response-length limit for SMS and WhatsApp, and a citation requirement (on by default). A tenant's `AgentBehaviourProfile` has no field that can reach the preamble. Rule: a tenant configures an agent's voice and scope, never its authority.

**What agents may and must not do.** The table in `iip_ai_agent_architecture_tool_registry_updated.md` section 4 is the boundary. Prohibited for every agent: approve or reject claims; deny, repudiate, settle or release payment; bind a policy or set final premium, loading, exclusion or terms; accuse anyone of fraud; send a sensitive customer communication without a recorded human approval; execute an external write-back outside the approved write-back service; expose hidden prompts or raw tool output; bypass tenant, workspace, role, assignment or sensitivity filters; fabricate metrics, claims, documents, SLA values, provider health or citations; use staff-only or secret material for retrieval unless approved for AI retrieval. Underwriting output reads "Refer to a human underwriter because ...", never "approved", "declined", "premium set" or "cover bound".

**Failure behaviour.** Missing source data: say what is missing. Low confidence: mark it and route to a human. Prompt injection: block or ignore and audit. Tool or provider unavailable: return a degraded or `not_configured` block. Unsafe decision request: refuse and give the reviewer an evidence checklist. Cross-tenant or unauthorised access: refuse. Unapproved knowledge source: say approved evidence is unavailable. Documents, email, OCR text and model output are untrusted data and never gain tool authority.

### 4.5 Orchestration architecture

There are four orchestration mechanisms. None lets an agent decide a consequential outcome.

1. **Command Center (synchronous, deterministic first).** `CommandCenterQueryService` classifies each user turn with a deterministic intent classifier (an AI fallback only when needed), then dispatches to Application services and read repositories, never to a free-roaming model. Intents include policy questions, management summaries and customer update drafts, and, through `CommandCenterActionService`, three instructions: draft a message to a customer (saved for approval, never sent), assign a claim to a named person (audited with the words typed), and run the Claims Summary Agent on a claim. A target is resolved from a claim reference (`CLM-` plus the first eight hex characters of the id) or a customer name; an ambiguous name returns the candidates and asks which was meant. The answer is a structured response, not free text: typed blocks (`metric_pills`, `sla_summary`, `claims_table`, `evidence_list`, `action_proposal`) plus `requiresHumanReview`, `confidence`, `evidenceIds` and `auditRequired`. Proposed actions are labelled proposals, never executed outcomes.
2. **Session and context.** IIP owns the conversational state (hybrid decision: Azure AI Foundry provides model hosting and optional short-horizon continuity, and the .NET Application layer owns state, grounding, permissions, audit, approval gates and supervision). **Built:** `CommandCenterConversationService` keeps per-user, per-workspace conversations (list, open, rename, archive, restore) with their turns, and `ConversationGroundingService` captures, with each answer, a focus reference (the claim the answer was about) and an evidence snapshot (a version hash of the claim's status, completeness, documents, extractions and field corrections). When a person reopens a conversation it compares that snapshot with the current records and reports whether the old answer is still current, so a stale answer is flagged, not trusted. **Specified but not built:** a bounded `ICommandCenterSessionStateService` (operational continuity only: focus references, active filters, current task intent, unresolved clarifying questions, pending action proposal references; never raw evidence, claim records, policy wording, OCR text, message bodies, provider payloads, prompts or embeddings) and its compaction policy, in `iip_conversational_session_context_multi_agent_architecture.md`. Standing rule either way: **every consequential turn re-retrieves its grounding fresh** by id or filter. The prompt is built from bounded parts (static instructions, current tenant, workspace and role, the user's request, a small continuity block, freshly retrieved evidence with tool metadata and citations, and the response schema); then `IChatCompletionProvider` generates, `IAiOutputValidator` checks schema, citations and review flags, and the audit writer records the activity. Foundry-side conversation history and compaction are convenience, never evidence.
3. **Workflow engine (graph orchestration, Release 1 bounded).** Tenants can run and publish graphs of node types `Trigger`, `Agent`, `Tool`, `Decision`, `HumanReview` and `Complete`; each node has a side-effect level (`ReadOnly`, `DraftOnly`, `InternalWrite`, `ApprovalRequired`, `ExternalSend`). The code is `AgentWorkflowService`, `AgentWorkflowCatalog` and the Functions worker (`AgentWorkflowNodeExecute`, a Service Bus trigger on the `agent-workflow` queue), so each node runs as its own queued message. Run states: `Queued`, `Running`, `WaitingForHuman`, `Completed`, `Failed`, `Cancelled`; work items awaiting people are `RequiresReview`, `InProgress`, `Completed`, `ReturnedForCorrection`, `Escalated`, `Cancelled`. A publish-time invariant (`AgentWorkflowHumanReviewInvariant`) rejects any graph in which a path from a consequential or external-send node to `Complete` bypasses a `HumanReview` node, or in which such a node is unreachable. The catalog defines five system workflows by release (`document-to-decision`, `fnol-claim-readiness`, `claims-triage-sla`, a customer-response workflow that drafts but cannot send, and an underwriting and portfolio insight workflow). Only Release 1 is executable: the canonical graph `document-to-decision` (Document Checker, then an evidence quality gate, then human extraction review), limited by `AgentWorkflowReleaseOneBound` to the `document-checker` agent and the tools `documents.read` and `workflow.evidence-quality`. Releases 2 to 5 are visible but held until their providers and earlier services are proven. ADR-0002 deliberately did not revive a free multi-agent runtime.
4. **Background pipeline (durable queue).** On `main` (from the claims-core build), document work and claim review run off the request path through one durable database queue (`BackgroundJobs`: lease, retry, dead letter, visibility, optimistic lease token across API instances), used in every environment (D-024). Uploading a document returns at once; a handler per document reads, names, extracts, checks and offers values, then the claim review job runs the claims summary, the fraud signals and the advisory recommendation. A persistent failure ends as "Could not be read" with a safe reason and a send-back action; a transient one retries invisibly; a dead worker's job is taken over; job payloads carry identifiers only, never customer words. The WhatsApp assistant uses its own durable `customer-assistant` queue (item 27) in place of the old in-memory queue. A Service Bus consumer remains a possible swap behind the same handlers. Timers in `InsuranceIntelligence.Functions` handle retention enforcement, mailbox ingestion and case follow-through.

**Future multi-agent shape (not built).** The architecture reserves explicit `AgentRole` handoffs (`AgentHandoffRequest` and `AgentHandoffResult` carrying intent, related entity, evidence reference ids, required permission, `RequiresHumanReview` and a correlation id), orchestrated in the Application layer and, after live SDK verification, with Microsoft Agent Framework sequential and handoff patterns. Core routing, approval gates and grounding are not moved into a visual workflow designer, and neither the older Connected Agents capability nor A2A replaces IIP's permissioning, approval and audit semantics. Do not start a multi-agent migration without a concrete need.

### 4.6 The customer-facing agent and channels

The Customer Assistant is the one agent that runs with no staff user. It runs on an inbound WhatsApp message (HMAC-signature verified; on the multi-tenant build routed by business number with that workspace's secret) and, on the claims-core and SaaS builds, in the background rather than inside the webhook request. It classifies every message into one of six intents (`GeneralQuestion`, `NewClaim`, `ExistingClaimStatus`, `ProductInterest`, `HumanRequested`, `Unknown`) with a deterministic keyword classifier, never a live model call.

- `GeneralQuestion`: answered by the Policy Intelligence path with grounded retrieval. It may auto-send only if all five conditions of `CustomerAutoReplyGate` hold: the intent is `GeneralQuestion`, the answer carries an approved citation, it carries no customer-specific assertion, it fits the tenant's length limit, and the tenant has opted into auto-reply (default off). Otherwise it is logged for human review and not sent.
- `NewClaim`: opens a provisional case (`RequiresConfirmation = true`, no SLA clock) only once a policy number matches a real policy; a bare WhatsApp number is never enough.
- `ExistingClaimStatus`: discloses only a coarse status label, and only after a policy-number match.
- `ProductInterest` and `HumanRequested`: create a real sales lead through the same path staff use and broadcast it to a licensed person; the reply says a human will follow up.
- Governance: kill switch via `IAgentGate` with the scope-explicit overload (disabling it escalates every message to a human and audits `customer_assistant.agent_disabled`); every turn records a `CustomerConversationTurn` and an audit event `customer_assistant.<outcome>` with actor `system:customer-assistant`; a failure or timeout escalates to a person (`customer_assistant.processing_failed`). On the multi-tenant build the assistant's name and offered covers come from each workspace's own settings, bound per request.
- Local languages: where a workspace enables Luganda or Kiswahili, the inbound message is translated to English at the door, the existing English flow runs unchanged (every gate, consent check, identity check and classification runs on English only), and the reply is translated on the way out. The engine is AfriSLM behind `ITranslationProvider`; its output is untrusted, passes `TranslationQualityJudge`, protected values (policy numbers, links, claim references, names) must survive, and any failure falls back to English or escalates to a person with the customer's own words. A translated reply carries a note that a computer translated it and is sent only where unreviewed translations are allowed. No translation has been reviewed by a native speaker.

Staff-facing context sits beside this: a claim's review score and recommendation are computed from facts already on the claim (rules decide, a model only explains, weights are uncalibrated starting values), are visible only to roles with `ViewFraudSignals`, and a person's Agree or Disagree response needs `OverrideAiRecommendation` and is tied to the facts it was given on.

## 5. Domain language (the terms that cause mistakes)

| Term | Meaning | Not |
|---|---|---|
| Tenant | The insurance organisation that owns a data and authority boundary | customer, account, Azure subscription |
| Workspace | A governed subdivision of a tenant: membership, permissions, queues, configuration | tenant, a browser-selected context |
| Membership | The server-held relationship granting a person roles within a tenant and workspace | token claim, app role, group |
| Claim | A request for indemnity or benefit under a policy | case, task |
| Case | IIP's tenant-scoped work container for intake, evidence, tasks, reviews, approvals, communication and audit | a claim decision |
| Work item | A queue-addressable view of the next owned outcome | a universal backend aggregate |
| Claim file | The governed evidence and records of a claim | chat history |
| Complete claim file | Satisfies the current document and information rules for a review stage | an approved claim |
| Claim decision | An authorised human determination of the outcome | a recommendation or workflow status |
| Settlement offer | An authorised proposal after the claim decision process | a payment |
| Accountable human | A person with the evidence, authority, capacity and recourse for a consequential decision | a routine approval click |

The full glossary (245 lines) is `docs/00-start-here/product-glossary.md`; the domain language file is `CONTEXT.md`.

## 6. Roles and permissions

Roles: Admin, Claims Officer, Claims Supervisor, Underwriter, Compliance Officer, Customer Service Agent, Executive, Otic Admin, Support Engineer. Principles: backend authorization is the enforcement point; role-aware navigation is a convenience, not security; least privilege by default; every privileged action is audited; Otic and support access is controlled, justified and traceable.

Highlights of the matrix: only Admin configures checklists, providers and roles; a Claims Officer creates and edits assigned claims and submits for approval; a Claims Supervisor approves within authority; a Customer Service Agent can draft and see customer-facing status but never gives final approval; an Executive sees metrics only; a Compliance Officer has the full compliance audit view. Action classes (Read, Create, Update, Approve, Export, Configure) each require module permission, tenant and workspace scope, and, for all but Read, an audit record; Approve also requires a reason. Full matrix: `docs/04-testing-security-quality/iip_rbac_permissions_matrix.md`. The portal's role routes and action permissions are checked for parity with the API by `pnpm test`.

## 7. Getting started

### 7.1 Prerequisites

.NET 8 SDK; Node 20 and pnpm (check `web/portal/package.json` for the pinned version); Git; Git Bash or WSL for the launcher script; PowerShell 7 for the docs scripts. No Azure subscription is needed to develop.

### 7.2 Run everything locally on mock providers

From the repository root:

```bash
scripts/run-local-mock.sh            # API on 5167 and portal on 3000
scripts/run-local-mock.sh --api-only # API only
```

It sets every provider to `Mock`, `INSURANCEINTELLIGENCE_STORAGE_PROVIDER=InMemory`, `INSURANCEINTELLIGENCE_AUTH_MODE=Development` and `INSURANCEINTELLIGENCE_SEED_DEMO_DATA=true`, and writes logs to `.local/run-local-mock/`. Seeded scope: tenant `tenant-demo`, workspace `workspace-operations`, user `local-admin`. Open http://localhost:3000. Nothing needs a credential and nothing leaves the machine. Mock AI output is labelled "MOCK AI OUTPUT, not produced by a real model."

Without that script, run the two processes yourself:

```bash
# API, from the repository root
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5167 \
INSURANCEINTELLIGENCE_AUTH_MODE=Development INSURANCEINTELLIGENCE_STORAGE_PROVIDER=InMemory \
INSURANCEINTELLIGENCE_SEED_DEMO_DATA=true \
dotnet run --no-launch-profile --project src/InsuranceIntelligence.Api/InsuranceIntelligence.Api.csproj

# portal, from web/portal
NEXT_PUBLIC_API_BASE_URL=http://localhost:5167 NEXT_PUBLIC_TENANT_ID=tenant-demo \
NEXT_PUBLIC_WORKSPACE_ID=workspace-operations NEXT_PUBLIC_LOCAL_USER_ID=local-admin \
NEXT_PUBLIC_AUTH_MODE=Development pnpm dev
```

Omitting `NEXT_PUBLIC_LOCAL_USER_ID` is the commonest mistake: every API call then fails with HTTP 400 "Authenticated user context is required" although the API is healthy. In mock mode there is no real email; read confirmation and claim links from the mock outbox (`/api/v1/dev/mock-outbox`, with the dev headers) or the API log. Test files for uploads are in `web/portal/tests/fixtures/claims-core/` (for example `police_letter.jpg`, which is recognised as a police abstract with report number `POL/KLA/2026/00471`).

### 7.3 Configuration reference

Never commit a secret. Outside local development, secrets come from Key Vault or pipeline secrets, and Azure resources are reached through managed identity.

| Key | Purpose |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | Runtime environment |
| `INSURANCEINTELLIGENCE_RUNTIME_PROFILE` | `Demo`, `Staging`, `Production`, `ManagedApplicationReviewer` |
| `INSURANCEINTELLIGENCE_AUTH_MODE` | `Entra` or `Development` |
| `INSURANCEINTELLIGENCE_SEED_DEMO_DATA` | Seeds the synthetic tenant; allowed only in `Demo` and local, blocked by the validator elsewhere |
| `INSURANCEINTELLIGENCE_ALLOW_PRODUCTION_MOCKS` | Forbidden in every protected runtime; the app refuses to boot |
| `INSURANCEINTELLIGENCE_SELF_SERVE_SIGNUP` | SaaS: on by default only in developer runtimes; set `true` in a deployed environment |
| `INSURANCEINTELLIGENCE_LOCAL_AUTH_SIGNING_KEY` | SaaS: signs platform tokens (HS256, 8 hours); a deployment secret |
| `INSURANCEINTELLIGENCE_PLAN_CATALOGUE` | SaaS: replaces the placeholder plans and allowances without a release |
| `INSURANCEINTELLIGENCE_AI_PRICING` | Price table for the token cost estimate |
| `Azure__KeyVaultUri`, `Azure__TenantId` | Key Vault and Entra tenant |
| `NEXT_PUBLIC_API_BASE_URL`, `NEXT_PUBLIC_AUTH_MODE`, `NEXT_PUBLIC_TENANT_ID`, `NEXT_PUBLIC_WORKSPACE_ID`, `NEXT_PUBLIC_LOCAL_USER_ID` | Portal build-time values; the first two plus the Entra ones are baked into the build |
| `NEXT_PUBLIC_ENTRA_TENANT_ID`, `..._CLIENT_ID`, `..._API_SCOPE`, `..._REDIRECT_URI` | Portal Entra sign-in |
| `NEXT_PUBLIC_EXTERNAL_ID_AUTHORITY`, `..._CLIENT_ID`, `..._API_SCOPE` | Optional. Entra External ID for "Continue with Google" and "Sign in with email". Absent, the two buttons show but are disabled with a note (not configured on dev; needs an External ID app registration). The API needs `INSURANCEINTELLIGENCE_EXTERNAL_ID_AUTHORITY` and `..._CLIENT_ID` (optionally `..._AUDIENCE`) to accept those tokens next to work-account tokens |
| `NEXT_PUBLIC_ENTRA_AUTHORITY_TENANT` | Optional. `organizations` lets work accounts from any company sign in |
| `NEXT_PUBLIC_VISUAL_PARITY_MODE` | Development fixtures only; must be unset or `false` in every deployed build |

Two environment guides in `docs/05-deployment-release-enablement/` are still Draft and list generic keys (`ConnectionStrings__IipSql`, `AzureOpenAI__*`, `WhatsApp__*`); trust the validator and the code over them.

## 8. Repository layout and where changes go

```text
src/            .NET modules             tests/        .NET tests and fixtures
web/portal/     Next.js portal           infra/        Bicep, Marketplace packaging, ADRs
docs/           documentation            scripts/      setup, docs catalog, guard, tenant-isolation check
tools/          operator and QA utilities   prototypes/   non-production experiments only
.github/        CI workflows             .scratch/     temporary work, never authoritative
```

| Change | Start here |
|---|---|
| Business rule or domain language | `src/InsuranceIntelligence.Domain/` and the owning document |
| Use case, permission, tenant rule, approval, audit | `src/InsuranceIntelligence.Application/` |
| Database, Azure, provider, email, integration | `src/InsuranceIntelligence.Infrastructure/` |
| HTTP route, authentication, composition | `src/InsuranceIntelligence.Api/` |
| Background or event processing | `src/InsuranceIntelligence.Functions/` |
| Operator or claimant interface | `web/portal/` and `DESIGN.md` |
| Azure resources, packaging | `infra/` |
| A material decision | an ADR under `docs/06-decisions/` or `infra/ADR-*.md` |

## 9. Working rules

**Invariants (never weakened to make a check pass).**
- Tenant, workspace and permission are resolved server-side for every request and background action.
- Consequential outputs use current approved tenant evidence; model memory and conversation state are not evidence, audit or permission.
- Sensitive reads, state changes, provider actions, approvals, ingestion, communication, exports and write-backs are audited with safe metadata and a correlation id.
- Protected runtimes never use mocks, simulations, seeded data, in-memory or SQLite storage, fake sends, hardcoded insurer data, or fallback-to-mock. Missing capability is `Not configured`; empty data stays empty.
- Claimant-facing or unauthorised surfaces never expose internal notes, reserves, fraud or risk signals, prompts, secrets, raw provider errors, restricted audit detail or other tenants' data.
- Documents, email, OCR text, customer content and model output are untrusted data; embedded instructions never gain tool authority.
- No secrets, tokens, private keys, SAS URLs or real customer evidence in the repository, logs or commits.

**Method.** Inspect `git status` and preserve unrelated changes. Read the task, the code and the owning document. Make the smallest coherent forward change: do not add a competing architecture, a silent fallback to an older path, or unrelated cleanup. Business rules, tenant enforcement, human gates and audit live in Domain and Application; framework and provider details stay in adapters. Treat schemas, routes, messages, statuses and configuration as versioned contracts. Add a dependency or a service only for a concrete need. Compatibility paths need an owner, tests, telemetry and a removal condition.

**Branches and commits.** Branch from the maintainer-approved base (`harry/<description>` unless told otherwise). Focused commits, Conventional Commit types (`feat`, `fix`, `refactor`, `docs`, `test`, `security`, `ci`, `chore`). Stage explicit paths; never `git add -A`, `git add .` or `git commit -a`. Do not rewrite shared history or force-push.

**Text rules.** No emoji anywhere (UI, code, comments, model output, commit messages, docs). No em dashes anywhere; use a comma, colon, full stop or parentheses. In C# test assertions write an em dash as `(char)0x2014`. Model completions are cleaned at the provider boundary by `DisplayText.WithoutEmDashes`.

**Documentation before every commit of a major build.** Update the owning documents and this handbook in the same commit, regenerate the catalog (`pwsh scripts/docs/Update-IipDocsCatalog.ps1`), and name the files in the completion report (`Documentation updated:` or `No documentation impact:` with a reason). This is also in both `CLAUDE.md` files.

**Completion reports** state: outcome and files; contracts and legacy paths changed; security, tenant, data, AI and UI impact; commands run with real results; blockers and manual actions. Readiness is stated only as: source-complete, locally verified, deployed, staging-validated, production-candidate, Managed Application-ready, Partner Center-ready.

## 10. UI and UX rules

Read `DESIGN.md` before changing anything a user can see. Direction: a calm, border-led, light-first insurance operations console. One blue accent (`#1769FF`, changed from violet on 2026-10-07 so IIP matches its sister product CIP) marking only the next action; Inter throughout; hairline borders instead of shadows; colour rationed to 6px status dots and single figures; a 64px icon rail with a contextual panel and a left-aligned content column. A precise instrument, not a product tour. The tokens are in `web/portal/app/globals.css` (`--color-primary` and tints); the muted text token is `#646C7A` for AA contrast on the page background.

Rules never waived:
1. No emoji. 2. One primary action per screen region, in the accent. 3. Gradients in exactly two places (the Assistant orb and chart bars). 4. Borders, not shadows. 5. Status is a neutral pill plus a 6px coloured dot plus a word, never colour alone. 6. Every screen answers: where am I, what is this, what needs me, what next, how do I get back. 7. **Six states or it is not finished**: loading, empty, empty-after-filter, error, not configured, no permission. 8. No fabricated data, placeholder names, dead buttons or "coming soon"; missing capability renders `Not configured`. 9. AI preparation and human decision authority live in separate containers; the decision bar has an explicit verb and a consequence preview. 10. Every actionable AI assertion carries a citation that opens its source; no citation, no render. 11. No em dashes.

Landing is the Brief at `/app` (at most five ranked items from real fields, each with a one-sentence reason and one primary action, plus a "Handled for you" list, honestly empty when nothing needs the person). The Assistant is at `/app/assistant`, and chat is a persistent bar on every screen. Toasts are rare: off-screen, this user's work, actionable now, not already shown. Workflow state and its label come from the vocabulary in the workflow legibility contract, derived from the Domain enums. On a phone the sidebar starts as the 64px rail and opens on keyboard focus or hover, not on tap.

Before reporting a UI change complete, clear the 27-point checklist in `docs/03-design-architecture/iip_ui_craft_guardrails.md` section 8 and open the screen in a browser at 1280px and 768px (and 390px for phone). Reference: `docs/03-design-architecture/reference/bevaya-ui-replica.html`; copy its visual grammar, not its scope or emoji. The older visual-system documents that describe the blue `#1D4ED8` palette or violet are superseded.

## 11. Testing and CI

CI (`.github/workflows/ci.yml`) is the authoritative gate set; do not weaken, skip or replace its jobs.

| CI job | Commands |
|---|---|
| Backend (build + test) | `dotnet restore InsuranceIntelligence.sln`, `dotnet build InsuranceIntelligence.sln --no-restore -c Release`, `dotnet test InsuranceIntelligence.sln --no-build -c Release --verbosity normal`; a vulnerable-package scan is report-only |
| Portal (lint + checks + build) | in `web/portal`: `pnpm install --frozen-lockfile`, `pnpm lint`, `pnpm test`, `pnpm build`, then `pnpm test:release-gates` |
| Infra (bicep compile) | `az bicep build --file infra/bicep/phase3-dev-foundation.bicep` and `phase6-functions-runtime.bicep` |

`pnpm test` runs the Academy catalogue, runtime-data, provider-status, role-route, action-permission-parity and UI release-gate checks, plus the trust-truth, authority-permissions and scan-math suites. Other portal suites: `pnpm test:claims-core` (full claims journey on mock providers, including the two-company journey; build the Release API first with `dotnet build InsuranceIntelligence.sln -c Release`), `pnpm test:visual` (three projects: desktop 1440, tablet 1024, mobile 390), `pnpm test:runtime-recovery`, `pnpm test:deployed-dom`, `pnpm test:visual:update` (update snapshots on purpose only). Run the claims-core suite with `CI=1` and explicit `IIP_E2E_API_PORT` and `IIP_E2E_PORTAL_PORT` so Playwright never reuses a stale server.

Last full run on `main` (2026-11-06, commit `0e670b1a`): build 0 warnings and 0 errors; Domain 1266 and Api 2423 tests passed, 0 failed. The claims-core journey (110 passed) and the visual suite (150 passed, 51 skipped by their own conditions) were last run on 2026-11-02 against the claims-core branch before it was merged; run them again before the next release. `EndpointParityTests` fail if any route the live build served on 2026-11-02 stops being registered, if a route is registered twice, or if an endpoint asks for a service nothing registers. They exist because the consolidation once lost about 150 routes and nothing failed.

What a change must be tested for: success and invalid input; authentication, permission and tenant isolation; human-decision boundaries and audit writing; provider absence and honest `Not configured`; retries, duplicates, concurrency and migration; and the six UI states with accessibility. Never delete, skip or weaken a test to get a green result, and never report an unrun check as passed.

Merge governance: native branch protection is unavailable on the old private repository's plan, so the compensating rule is a pull request, all three CI jobs green, and one maintainer approval, with no merge on red. On `Otic-Group/IIP_Web_App` direct pushes are currently allowed for the consolidation work (decision D-003).

## 12. Data and migrations

- EF Core migrations live in `src/InsuranceIntelligence.Infrastructure/Migrations`. **Never hand-write a migration**; use `dotnet ef migrations add <Name> --project src/InsuranceIntelligence.Infrastructure --startup-project src/InsuranceIntelligence.Api` so the `Designer.cs` attributes exist (a hand-written migration without them was invisible to EF and its table was never created anywhere).
- Migrations are **additive**: new tables, nullable or defaulted columns, indexes. A `DROP`, a narrowing `ALTER COLUMN` or a rename needs a plan and an ADR, not a quiet merge.
- **A migration that has been applied anywhere is immutable.** Never edit, rename or reorder it, never copy a model snapshot between branches, and never merge a snapshot textually.
- **The API does not migrate on startup.** Deploying code that reads a new table starts healthy and then returns `500 Invalid object name` on every request that touches it, while `/health` stays green. After a deploy, call the admin endpoints (blocked under protected profiles): `GET /api/v1/admin/db-migrate/diagnose` (tables and applied history, run first), `POST /api/v1/admin/db-migrate` (apply pending), with `reconcile-history` and `force-reconcile-schema` for repair. They use the API's managed identity, so no one needs direct database access. Compare the diagnose count with the number of migration files; the written count in any document may be out of date.
- Data boundary: real customer insurance data lives only in the customer's own tenant data stores. Local and Demo data are synthetic and never migrate into a real account.
- Retention: nothing is deleted by default; a retention period is an explicit per-record-kind choice, and the legal basis is for the insurer's legal team to confirm. A legal hold on a claim blocks both retention and erasure for it. Erasure does not reach copies outside the platform (a provider's message copy, a pack already sent, backups, soft-deleted blobs for 7 days), so a restore procedure must replay erasures.
- Scale: everyday lookups are indexed; dashboards, reports, global search and the brief still read a whole workspace and must move to queries or a search index before a workspace reaches tens of thousands of claims.

## 13. Environments and deployment

### 13.1 What exists

Only one subscription holds IIP resources: "Microsoft Azure Sponsorship", `5f5765ae-985f-42e4-a443-8e2447be33ca` (Entra tenant Otic Group, `dcb0b132-fb9e-475c-a68e-b0671222e8d8`). Two other subscriptions in the tenant host unrelated projects; do not deploy IIP into them without a deliberate decision. Resource group `rg-iip-dev` (West US 2; Azure OpenAI in East US) is the only live environment and runs the `Demo` profile.

| Resource | Name |
|---|---|
| API App Service | `app-api-dev-h5girbnzrayqe` (`/health`) |
| Portal App Service | `app-portal-dev-h5girbnzrayqe` (Next.js standalone, Node 20) |
| Function App | `func-iip-dev-h5girbnzrayqe` |
| Plan | `asp-iip-dev` (B1 is too small for the API, the portal and the Functions app together: at 100% CPU the API answered in minutes. Raised to P1v3 on 2026-11-07 for testing and recording; reset to B1 afterwards only once the per-message database scan is fixed and the Functions app is off the shared plan) |
| SQL | `sql-dev-h5girbnzrayqe`, database `insuranceintelligence` |
| Storage, Service Bus, AI Search, OpenAI, Document Intelligence | `stiiph5girbnzrayqe`, `sb-dev-...`, `srch-dev-...`, `oai-iip-dev-...`, `di-iip-dev-...` |
| Key Vault | `kv-iip-dev-zcxolwov` (RBAC mode) |
| Telemetry | `log-dev-...`, `appi-dev-...` |
| Translation | Sunbird, configured per workspace as a saved connection. The AfriSLM container (`ca-afrislm-dev`) was deleted on 2026-11-07 and `INSURANCEINTELLIGENCE_TRANSLATION_PROVIDER` is `Disabled`, so a workspace without a Sunbird connection has no translation and fails fast. The empty Container Apps environment `cae-iip-dev` remains |

`rg-iip-stg` is empty on purpose: the staging resources were deleted out of band around 2026-07-03, and the Sponsored subscription could not create an App Service Plan or an Azure OpenAI resource when last tried. Confirm live with `az` before deploying; resources here have been deleted or renamed out of band before.

### 13.2 Deploying to dev

There is no working CI/CD path to dev (`deploy-staging.yml` points at the deleted staging resources). Deployment is by `az` and happens only when explicitly requested.

1. `az login`, then `az account set --subscription 5f5765ae-985f-42e4-a443-8e2447be33ca`.
2. **API**: `dotnet publish src/InsuranceIntelligence.Api/InsuranceIntelligence.Api.csproj -c Release -o <dir>`, zip, `az webapp deploy --type zip`. The API runs from a package (`WEBSITE_RUN_FROM_PACKAGE=1`); identify the running revision from `data/SitePackages/packagename.txt`, not from a stale `wwwroot`.
3. **Migrations**: the separate step in section 12. Run `diagnose` first.
4. **Function App**: deploying the API does not deploy the functions. Publish `InsuranceIntelligence.Functions`, zip, `az functionapp deployment source config-zip`, then compare `functions.metadata` in the publish folder with `az functionapp function list` (registration lags up to a minute). The worker currently running is older than the API (revision `1872a14`, 2026-09-14); the compatibility check is open work (section 16).
5. **Portal**: build with the real `NEXT_PUBLIC_*` values (read them first with `az webapp config appsettings list`), assemble the standalone bundle (copy `.next/standalone`, `.next/static`, `public`, reinstall production dependencies, no symlinks), zip, `az webapp deploy`. The start command is `node server.js`.
6. Do not overwrite app settings without reading the current values; a code deploy does not touch them.
7. **Restart, then prove it.** Both the API and the portal kept serving the old build after a "successful" upload until the app was restarted (`az webapp restart`). Never trust the deploy output. Check a route or page that exists only in the new build: for the API `GET /api/v1/auth/capabilities` (200 on the new build, 401 on the 2 November one), for the portal `/signin` (200 on the new build, 404 on the old).
8. **Start time.** On the B1 plan the API takes from 4 to about 24 minutes to start listening. `WEBSITES_CONTAINER_START_TIME_LIMIT` must be `1800` (the platform maximum); at `600` Azure killed the container in a loop on 2026-11-06 and even the previously good build would not come up. The CLI often prints "Deployment failed" while the server is still starting, so check `az webapp log deployment list` and the marker route, and use `az webapp log startup show` for the container log. A larger plan starts faster.
9. **Keep a rollback.** Before replacing a build, build and keep a zip of the one being replaced (the previous commit published and zipped). On 2026-11-06 it brought dev back twice.

Gotchas that cost time: the CLI can report `HTTP_504` for a large zip while the deploy succeeds (check `az webapp log deployment list`); `RuntimeSuccessful` can appear while old code keeps serving (confirm with a request only the new code can answer, then `az webapp restart`); the API can take 4 to 8 minutes to serve new code, during which the portal says "Sign-in could not be completed"; `pnpm build` can crash with an access violation under memory pressure; `npm install --silent` hides `ENOSPC`; the build machine runs out of disk, so check free space first; `Compress-Archive` stalls on thousands of small files, so use `ZipFile.CreateFromDirectory`; Git Bash on Windows converts `ref:path` arguments unless you set `MSYS_NO_PATHCONV=1`.

### 13.3 Managed Application packaging

`infra/createUiDefinition.json`, `infra/mainTemplate.bicep`, `infra/viewDefinition.json` and `infra/bicep/*` package IIP for a customer's subscription. A customer-managed-key option is written but has never been run in a real deployment. Publisher support access must be explicit, limited, auditable and revocable. Nothing in the repository is Partner Center-ready.

### 13.4 Release records

Every release records, per artifact (API, portal, worker): the commit, the artifact SHA-256 and the highest migration id. This is how the live code was traced to `4818bb1` after the repositories drifted.

## 14. Security

- Authorization is server-side; the portal hides what a role cannot use, but the API enforces it.
- Platform tokens are HS256 with an 8-hour life, no refresh and no revocation; five wrong passwords pause an account for 15 minutes; the sign-up rate limit counts by connection address, so behind a proxy the real address must be forwarded.
- Channel credentials (WhatsApp, mailbox) are stored encrypted per company, write-only from the browser, never shown again. Provider credentials belong in the per-tenant, validated-before-save connection model, not in startup environment variables; `WhatsAppConfigurationEndpoints` is the reference implementation.
- Custom hostnames need DNS and TLS you control; nothing in the product issues certificates. There is no database uniqueness rule yet on link names, hostnames or code redemption.
- Payment adapters (MTN MoMo, Airtel Money, card) are written to each operator's published API and only tested against stand-ins. Prices and allowances are placeholders for the business to set.
- **Hidden-code injection (2026-11).** `OticGroupAz/agenticIip-v2` `main` carries an obfuscated payload of about 12,900 characters hidden after a run of tabs in `web/portal/postcss.config.js`, plus `.gitignore` entries hiding `temp_auto_push.bat`, `temp_interactive_push.bat` and `branch_structure.json`; it spreads through blanket `git add -A`. Never build, install or run that `main`, any `copilot/*` branch, or the old `OticGroupAz/agenticIip` repository; read them with `git show` only. Before every install, build, test run and commit, run `bash scripts/guard/check-config-injection.sh .` (exit 0 is clean). Machines that built the poisoned branches need rotating secrets and a clean environment; that is the owner's task and is not done by the consolidation.

## 15. Current state (2026-11-03)

| Item | State |
|---|---|
| Live | API and portal run `main` (`0e670b1a`) since 2026-11-06, with 43 migrations applied, the last `20261104033637_Consolidate_ClaimsCore`; the Functions worker is still the 14 September build |
| Staging | none; `rg-iip-stg` is empty |
| Claims-core build | 37 items, merged into `main` (the build notes are in `docs/build-loop/`) |
| Social channels | merged into `main` |
| SaaS Academy | the live Academy at `/academy`, with media, pricing and payments, is in `main`; the duplicate learner pages claims-core built at `/learn` were deliberately not ported (decision D-032) |
| Consolidation | `main` (the SaaS edition) is complete, deployed to dev and verified; the `managed-app` branch is not cut yet (phase D of the plan) |

**Not proven against real services** (each needs its first run with real access): Azure OpenAI quality, grounding, refusal behaviour and cost accuracy; Document Intelligence accuracy on Ugandan police abstracts, IDs, repair estimates and phone photos; WhatsApp template approval, the 24-hour window, signatures and media download; email sending with attachments and IMAP reading; all three payment providers; Service Bus; customer-managed-key deployment; backups, soft delete and replay of erasures after a restore; Entra sign-in alongside platform sign-in.

**Known gaps:** TikTok inbound is missing; social provider proof, media and delivery status are open; erasure of Academy learner records, whole-workspace erasure and personal data export are not built; the Functions worker and API may be out of step; the terms page is draft wording pending legal review; sign-up and plan numbers are placeholders. The live dev environment is not staging-validated and nothing is Partner Center-ready.

**Decisions a human owns** (from the claims-core build): payment prices and provider accounts; retention periods and legal basis; whether sign-up is open in a deployed environment; the data migration that adds uniqueness rules; whether to use Service Bus instead of the durable database queue (the queue is database-backed in every environment, decision D-024). There is no automatic monthly charge because mobile money cannot be charged unattended.

## 16. Repository consolidation

The separate checkouts and branches (live lineage, SaaS Academy snapshot, v2 social channels, claims-core) were unified in `https://github.com/Otic-Group/IIP_Web_App.git`. `main` is the SaaS edition, created from the live lineage `4818bb1` with everything else ported on top. The `managed-app` branch, the Azure Managed Application edition, is still to be cut from `main` by removing the SaaS-only modules, one commit per module, guarded by a CI check.

Rules the work followed: the live implementation wins where two implementations exist unless the other is provably a superset in tested behaviour (written capability matrix); live migrations are immutable, and the claims-core and v2 migrations were replaced by one additive migration, `Consolidate_ClaimsCore`; no test count drops without a recorded reason; no force-push.

**What went wrong and how it was found (2026-11-06).** Two ports (items 27 and 28, and item 29) rewrote whole files and silently removed about 150 routes and their wiring, so the first deploy of `main` lost the demo worlds, rules, bank events, templates, the Academy API and WhatsApp receipts. It was rolled back, repaired by a three-way merge of the damaged files, and is now guarded by `EndpointParityTests`. The evidence and the decisions are in `docs/08-program-plan/consolidation-data/` (`MAIN-PARITY-GAP-MAP-2026-11-06.md`, `DECISIONS.md` D-040, the phase reports).

The plan and its appendices: `docs/08-program-plan/IIP-CONSOLIDATION-MERGE-PLAN-2026-11-03.md`, `IIP-BRANCH-AND-LIVE-RECONCILIATION-AUDIT-2026-11-03.md` and `consolidation-data/`.

## 17. Documentation map and its health

Authority order: explicit instructions from the owner or maintainer, then `AGENTS.md`, then current code, tests, migrations, CI and configuration validation, then the owning approved document or ADR. If sources conflict, find the latest approved authority and fix the stale source; never blend them.

| Need | Owning document |
|---|---|
| Repository rules | `AGENTS.md`, `CLAUDE.md`, `CONTRIBUTING.md`, `IIP_IMPLEMENTATION_GUARDRAILS.md` |
| Product intent | `docs/01-strategy-requirements-roadmap/iip_master_product_context.md`, `iip_product_requirements_document.md`, `iip_product_definition_and_validation_charter.md` |
| Programme order | `docs/08-program-plan/IIP-CONSOLIDATED-ROADMAP.md` |
| Architecture | `docs/03-design-architecture/iip_technical_architecture_document.md` |
| Agent architecture and tool registry | `docs/03-design-architecture/iip_ai_agent_architecture_tool_registry_updated.md` (target registry in sections 5 to 9, code-verified status in section 15), `infra/ADR-0002-agent-registry-kill-switch-boundary.md`, `src/InsuranceIntelligence.Domain/Agents/AgentRegistry.cs` |
| Orchestration, session and context | `iip_conversational_session_context_multi_agent_architecture.md`, `iip_workflow_architecture_document.md`, `iip_ai_rag_knowledge_architecture.md`, `iip_agentic_ai_interface_and_governance_patterns.md` (all under `docs/03-design-architecture/`) |
| UI and UX | `DESIGN.md` and the four documents it links |
| Security and permissions | `docs/04-testing-security-quality/iip_security_compliance_document.md`, `iip_rbac_permissions_matrix.md` |
| Deployment and Azure reality | `docs/05-deployment-release-enablement/AZURE_DEPLOYMENT_AND_ENVIRONMENTS.md` |
| Managed Application | `iip_managed_application_architecture_guide.md`, `iip_azure_managed_application_packaging_guide.md`, `infra/ADR-0001-hosting-model.md` |
| SaaS tenancy and billing | `iip_saas_tenant_provisioning_guide.md`, `infra/ADR-0003-billing-entitlements-metering.md`, `docs/iip-academy/` |
| Gap register and honesty rules | `docs/01-strategy-requirements-roadmap/IIP_HACKATHON_READINESS_BLUEPRINT.md` |
| Claims-core build notes | `docs/build-loop/` (SUMMARY, DECISIONS, HOW_TO_TEST with a step-by-step walkthrough per item) |
| Everything else | `docs/README.md` and the generated `docs/CATALOG.md` |

Known documentation drift on 2026-11-03: the agent architecture document's implementation table lists 12 agents and its acceptance criterion says exactly 10, while `AgentCatalog` holds 15 (16 on claims-core); the architecture, security and test documents are dated June to September 2026 and do not cover the claims-core or SaaS work (this handbook does); the developer setup guide and environment-variables guide are Draft with "Requires Confirmation" placeholders; the Managed Application guide and the SaaS tenant provisioning guide are Draft from 2026-06-30; `AZURE_DEPLOYMENT_AND_ENVIRONMENTS.md` quotes 27 applied migrations while live has 42; several visual documents still describe violet and are superseded by `DESIGN.md`. When you touch an area, correct the document that owns it in the same commit.
