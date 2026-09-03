# IIP — Full System Analysis & Production Readiness Plan

**Date:** 2026-09-04
**Branch analysed:** `main` @ `1baf505`
**Method:** direct source inspection of `src/` (7 projects, 126 HTTP routes), `web/portal/` (36 pages, 22 data modules), `docs/` (39 architecture documents), `tests/` (67 backend test files, 23 Playwright specs).
**Scope:** every navigable module, every workflow, the agentic layer, the documentation authority set, and the design system.

---

## 0. Headline verdict

The instinct that this is "vibe-coded scaffold" is **half right, and the half that is right is the dangerous half.**

What is actually true:

- **The backend domain is not scaffold.** `InsuranceIntelligence.Domain` is a genuinely well-built, tenant-isolated insurance domain: 10-state claim lifecycle, deterministic routing policy, claim-type classifier, SLA calculator, document checklist rules, fraud rule engine, underwriting rule engine, approval authority policy, audit event model. 126 real HTTP routes with server-side permission filters on essentially all of them. Real Entra/MSAL authentication. 67 backend test files.
- **The read layer of the portal is not scaffold.** All 22 data modules under `web/portal/lib/lovable-parity/data/` make real HTTP calls to the API. Claims, audit, approvals, channels, customers, reports, provider health — these render live backend data.
- **The write/action layer is roughly half missing.** 13 of 22 data modules are read-only. Several modules have working backend endpoints with no UI to call them.
- **The agentic layer — the thing the product is named for — does not exist in code.** This is the single most serious finding and is detailed in §3.
- **At least one shipped, navigable page renders entirely fabricated insurance content**, including a fake AI answer with a fake confidence score and fake policy clause citations. This is a direct violation of the platform's own governing invariants.

So: the foundation is sound, the domain is real, and the problem is not that everything is fake. The problem is that **the gap between what the system advertises and what it does is large, and it is not signposted.** A demo user clicking through cannot tell which surfaces are real. That is what makes it feel vibe-coded, and that is precisely the failure mode `AGENTS.md` was written to prevent.

**Production readiness: not in one week. One month is achievable for a scoped, honest production release** — see §7.

---

## 1. The five reported issues — verified

Every one is real. Three are worse than reported, one is narrower than reported.

### 1.1 FNOL is Motor-only — **CONFIRMED, UI-layer only**

`web/portal/lovable-pages/FNOL.tsx` hardcodes the product line:

```
line 110:  productLine: "Motor",
line 111:  claimType: "Motor Accident",
```

The entire form is motor-shaped: vehicle registration, driving permit number, accident location/time, police report status. Required-field validation (lines 70–88) enforces motor fields on every claim regardless of type. The right-hand "Required documents" panel is a hardcoded 7-item motor list (lines 11–19).

**The good news: the backend is not motor-only.**
- `CreateCaseCommand` takes `ProductLine` as a free string.
- `DocumentChecklistRules.ForProductLine()` already branches Motor / Medical / default.
- `DocumentChecklistRules.ForCase()` already discriminates by claim-type kind (OwnDamage, ThirdPartyProperty, TheftTotalLoss, InjuryFatality).
- The demo seed generates claims across 5 product lines, and `ClaimsQueue` filters on product line — so non-motor claims already display correctly.

**Gap:** `MotorFnolDetails` is the only structured FNOL detail record in the domain (`src/InsuranceIntelligence.Domain/Cases/MotorFnolDetails.cs`). There is no Medical/Property/Marine/Liability equivalent, and no product-line-driven form schema.

**Verdict:** UI is hardcoded; domain needs one new abstraction (a product-line FNOL schema), not a rewrite.

---

### 1.2 Cannot upload documents in Claim Intelligence — **CONFIRMED, but the cause is not a missing feature**

The upload path is fully built end to end:
- `POST /api/v1/cases/{caseId}/documents` — `Program.cs:537`
- `DocumentEndpoints.UploadDocuments` — stores to blob, classifies, extracts, updates completeness, writes two audit events per file
- `components/iip/DocumentUploadWidget.tsx` — real XHR upload with progress, image compression, honest offline/error states
- Wired into `ClaimReview.tsx:383`

So *the feature exists*. It fails for one of three reasons, and all three are real defects:

**(a) No permission gating on the UI.** The upload widget renders unconditionally. But `RolePermissionMap` shows `Permission.UploadDocument` is **absent** from `ITAdmin` and `Compliance`. A user in either role sees an Upload button that returns HTTP 403. The button should not be there.

**(b) Storage provider may be `NotConfigured`.** `Program.cs:216–228` selects `AzureBlob` / `LocalMock` / `NotConfigured`. If document storage is not configured in the running environment, `NotConfiguredDocumentStorageProvider` throws `ProviderNotConfiguredException` — the upload fails with a raw provider error rather than an honest "Document storage is not configured" state.

**(c) The Documents module has no upload at all.** `lovable-pages/Documents.tsx` (59 lines) and `GET /api/v1/documents` are read-only. A user looking for "upload a document" in the Documents module will not find it — upload only exists inside an individual claim.

**Verdict:** Not a missing feature. A permission-gating bug + a provider-configuration honesty bug + a discoverability gap.

---

### 1.3 No place to test different accounts — **CONFIRMED, and this is a genuine product gap**

Three separate problems stacked:

**(a) The role switcher is preview-only and hidden in production.**
`components/iip/RoleSwitcher.tsx:29` returns `null` unless `canUseLocalTestDoubles || LOVABLE_VISUAL_PARITY_MODE`. Even when visible, it only changes *what you see* — line 42 explicitly announces "Signed-in authority remains unchanged." Server-side permissions are unaffected. This is correct security behaviour but it is **not** account testing.

**(b) User management has a backend and no UI.**
These endpoints exist and are permission-gated:
- `GET /api/v1/admin/users`
- `POST /api/v1/admin/users` (CreateUser)
- `PUT /api/v1/admin/users/{userId}/role` (UpdateUserRole)

There is **no portal page that calls any of them.** `app/app/admin/` contains only `agents`, `billing`, `onboarding`, `tenant-policy`. The Admin console lists "Users & roles" as a card (`Admin.tsx:20`) — but it is in the `inline` array, which renders as a **static, non-clickable description tile**. It looks like a settings entry and does nothing.

**(c) No invite / provisioning flow.** No way to add a colleague, assign them a role, and have them sign in.

**Verdict:** To test a second account today you must create the user directly in Entra ID and call the admin API by hand. There is no product surface for it.

---

### 1.4 Notification icon does not work — **CONFIRMED, and worse: it displays a fake unread badge**

`components/iip/TopBar.tsx:99–102`:

```tsx
<button type="button" aria-label="Notifications" className="...">
  <Bell className="h-4 w-4" />
  <span className="absolute top-1.5 right-1.5 h-1.5 w-1.5 rounded-full bg-error" />
</button>
```

No `onClick`. No handler. And the red dot is **unconditional** — it is a hardcoded element that always claims unread notifications exist.

**The deeper finding: there is no in-app notification system at all.** Searching the domain for notifications returns only `Domain/Notifications/TeamsNotification.cs` and `ITeamsNotificationQueue` — these are **outbound** notifications pushed to Microsoft Teams. There is no notification entity for a user, no read/unread state, no notification feed endpoint, no repository.

The bell is not a broken button. It is a picture of a feature that was never built, with a permanently-lit unread indicator.

This violates `AGENTS.md`: *"Protected runtimes never activate … fake sends/metrics, hardcoded insurer data."*

**Also dead in the same component:** the Help button (`TopBar.tsx:98`) has no `onClick` either.

---

### 1.5 Profile icon does nothing — **CONFIRMED; it is not even a button**

`TopBar.tsx:117`:

```tsx
<div role="img" aria-label={`${displayName}. Signed in as ${roleLabel}`} ...>{userInitials}</div>
```

It is a `<div role="img">`. There is no menu, no profile page, no sign-out control, no preferences.

Backend state:
- `GET /api/v1/me` exists and returns display name, role label, permissions, workspace list.
- There is **no** `PATCH /api/v1/me` or any self-service profile update endpoint.
- `app/app/settings/page.tsx` is a 4-line redirect to `/app/admin`. There is no settings screen.

**Consequence:** a signed-in user cannot change any personal detail, cannot switch workspace from the header (despite `availableWorkspaces` being returned by `/api/v1/me`), and cannot sign out from the UI.

---

## 2. Module-by-module analysis

Legend: **Live** = real API read + working actions · **Read-only** = real data, no actions · **Static** = no backend call · **Fabricated** = hardcoded fake business content

| # | Module | Route | State | Detail |
|---|---|---|---|---|
| 1 | Command Center | `/app` | **Live** | Real `/command-center/query` + metrics. The strongest surface in the product. |
| 2 | Claims Queue | `/app/claims` | **Live** | Real claims, filters, assign + escalate actions wired. |
| 3 | Claim Review | `/app/claims/[id]` | **Live** | 13 API calls — richest page. Documents, timeline, review requests, upload, approvals. |
| 4 | FNOL / New Claim | `/app/claims/new` | **Live, Motor-only** | Creates real claims. Product line hardcoded (§1.1). |
| 5 | Document Extraction | `/app/claims/[id]/extraction` | **Live** | Approve-extraction and correct-field wired to real endpoints. |
| 6 | AI Conversations | `/app/conversations` | **Live** | Real conversation persistence, grounded answers, citations, markdown rendering. |
| 7 | Approvals / Work Items | `/app/work-items` | **Live** | Real approve/reject/escalate/override. |
| 8 | Agent Flows | `/app/flows` | **Live** | Real workflow CRUD, versioning, publish, run inspection. |
| 9 | Audit | `/app/audit` | **Read-only** | Correct — audit should be read-only. Export exists at API level. |
| 10 | Executive | `/app/executive` | **Read-only** | Real report aggregates + CSV export. |
| 11 | Channels | `/app/channels` | **Live** | WhatsApp thread read + outbound message POST. |
| 12 | Customers | `/app/customers` | **Read-only** | No create/edit. No `POST /api/v1/customers` exists. |
| 13 | Customer Detail | `/app/customers/[id]` | **Read-only** | Display only. |
| 14 | Documents | `/app/documents` | **Read-only** | No upload in this module (§1.2c). |
| 15 | Communications | `/app/communications` | **Read-only UI** | **Gap:** `POST /communications/send-email` and `send-sms` exist in the API but the UI never calls them. |
| 16 | Knowledge | `/app/knowledge` | **Read-only UI** | **Gap:** upload / approve / archive / reindex all exist as endpoints; UI is list + search only. Banner admits controls are "hidden". |
| 17 | Underwriting | `/app/underwriting` | **Read-only** | `GET /underwriting/reviews` only. **No decision endpoint exists at all.** |
| 18 | Fraud Intelligence | `/app/fraud` | **Read-only** | `GET /fraud/signals` only. Real `FraudRuleEngine` behind it. No triage/disposition workflow. |
| 19 | Customer Acquisition | `/app/acquisition` | **Live** | Sales leads with POST. |
| 20 | Global Search | `/app/search` | **Read-only** | Real search endpoint. |
| 21 | Integrations & Health | `/app/integrations` | **Live** | Real provider health grid. Honest. |
| 22 | Admin Console | `/app/admin` | **Partly static** | 6 governance cards route correctly. 7 "Tenant profile" tiles are **non-clickable static text** (§1.3b). |
| 23 | Admin · Agents | `/app/admin/agents` | **Live but hollow** | Real kill-switch API — but the switch gates nothing (§3). |
| 24 | Admin · Billing | `/app/admin/billing` | **Read-only** | Real usage aggregates. |
| 25 | Admin · Onboarding | `/app/admin/onboarding` | **Live** | Real tenant onboarding + consent URL. |
| 26 | Admin · Tenant Policy | `/app/admin/tenant-policy` | **Read-only** | 41 lines. Display only. |
| 27 | **Policy Intelligence** | `/app/policy` | **🔴 FABRICATED** | See below. |
| 28 | Settings | `/app/settings` | **Redirect stub** | 4-line redirect to `/app/admin`. |

### 2.1 🔴 Policy Intelligence is entirely fabricated — P0

`lovable-pages/PolicyIntelligence.tsx` (83 lines) is in the **main navigation** and contains **zero** API calls. Everything on it is hardcoded:

- **A fake AI answer** (lines 36–40): *"Under Motor Comprehensive Policy **MTR-8821**… A standard excess of **UGX 250,000** applies (clause 6.1)… Coverage is void if the driver at the time of the incident is unlicensed (exclusion 9.3)."*
- **A fake confidence badge** — `<ConfidenceBadge value={0.88} />` (line 34)
- **Four fake clause citations** with fake page numbers (lines 5–10): *"Motor Comprehensive Policy Wording v3.2 · p.12"*, confidences 0.94 / 0.91 / 0.88 / 0.96
- **Three fake related policies** including a fabricated regulatory reference: *"REG · IRA Motor Guidelines 2024"* (line 74)
- **A dead "Ask" button** (line 21) — no handler
- **A dead "Escalate to human policy expert" button** (line 78) — no handler

This breaks four separate governing rules at once:

> `AGENTS.md`: *"Protected runtimes never activate mocks, simulations, seeded business data … fake sends/metrics, **hardcoded insurer data**."*
> `AGENTS.md`: *"Consequential outputs use current approved tenant/workspace evidence."*
> `CLAUDE.md`: *"do not render: fake dashboard statistics or hardcoded insurer data … AI outputs presented as final human decisions."*
> Agent architecture doc §4: Policy Intelligence output must carry *"clause explanation with citations and uncertainty notes"* from approved sources.

The bitter irony: **the real capability already works.** `CommandCenterQueryService` answers policy questions with genuine Azure AI Search grounding, real citations, and an honest refusal when no grounded source is found. This page shows a counterfeit of a feature that exists two modules away.

**This page must be either deleted or wired to the real endpoint before any customer sees the system.** It is the highest-risk item in the repository — a regulated-industry demo showing an insurer a fabricated coverage determination with a fabricated excess figure and a fabricated confidence score.

---

## 3. The agentic layer — the biggest finding

The product is positioned as agentic. The architecture document `docs/03-design-architecture/iip_ai_agent_architecture_tool_registry_updated.md` (606 lines) is thorough and good: it defines 10 canonical agents, 30 named tools, a tool-registry schema, side-effect levels (`read` / `draft` / `write` / `external_send` / `approval_required`), per-tool permission requirements, provider-health dependencies, failure behaviour, and audit requirements.

**Almost none of it is implemented.**

### 3.1 What exists

`src/InsuranceIntelligence.Domain/Agents/AgentRegistry.cs` defines the 10 agents as a static catalog — key, name, purpose, risk level, human-approval flag. `AgentRegistryMerge` combines it with a tenant-scoped kill-switch state, surfaced at `/app/admin/agents`.

That is the whole of it. `AgentDescriptor` has **no tools field, no side-effect level, no permission mapping, no provider dependency.**

### 3.2 What was removed

`src/InsuranceIntelligence.Api/Program.cs:361–366` — an in-repo note:

> ```
> // NOTE (v2 migration): the code-only agent framework (IAgentTool/AgentToolRegistry/
> // AgentIntentRouter/AssistantOrchestrator/IModuleAgent + per-module agents) was dropped.
> // It will be re-derived to the governing docs (AgentToolRegistry + AgentRun audit +
> // IIntentClassifier ...) in a later sub-step. The deterministic /query planner remains
> // functional without it.
> ```

The contracts survive as **orphaned interfaces** in `Domain/Agents/AgentContracts.cs` — `IAgentTool`, `IAgentToolRegistry`, `IAgentIntentRouter`, `IAssistantOrchestrator`, `IModuleAgent`, `IAgentEvidenceBuilder`. **None has a single implementation anywhere in `src/`.** Verified by search.

### 3.3 What actually runs

One service calls a model. `CommandCenterQueryService` invokes `IChatCompletionProvider.CompleteAsync` at exactly three call sites (lines 296, 556, 685), reached through a hand-written deterministic intent classifier. Plus two dedicated providers: `AzureOpenAiCustomerUpdateDraftingProvider` and `AzureOpenAiAssistantProvider`.

Mapping the 10 catalogued agents to reality:

| Agent | Advertised in Admin | Actually implemented | How |
|---|---|---|---|
| `command-center` | ✅ | ✅ **Real** | `CommandCenterQueryService` + Azure OpenAI |
| `policy-intelligence` | ✅ | ⚠️ **Partial** | Real via Command Center Q&A; the *page* named after it is fabricated (§2.1) |
| `knowledge-assistant` | ✅ | ⚠️ **Partial** | `AzureOpenAiAssistantProvider` + AI Search retrieval exists; no dedicated UI |
| `document-checker` | ✅ | ✅ **Real, not AI-agentic** | `AzureDocumentIntelligenceProvider` + `DocumentChecklistRules` |
| `customer-update-drafting` | ✅ | ✅ **Real** | `AzureOpenAiCustomerUpdateDraftingProvider`, human approval enforced |
| `routing` | ✅ | ⚠️ **Deterministic** | `ClaimRoutingPolicy` — rules, no model |
| `risk-fraud-signal` | ✅ | ⚠️ **Deterministic** | `FraudRuleEngine` — rules, no model |
| `underwriting-intelligence` | ✅ | ⚠️ **Deterministic** | `UnderwritingRuleEngine` — rules, no model |
| `claims-summary` | ✅ | ❌ **Nothing** | No provider, no service, no call site |
| `report-insight` | ✅ | ✅ **Real** *(correction, see below)* | `CommandCenterQueryService.BuildManagementSummaryResponseAsync` calls `chatCompletionProvider.CompleteAsync` for `GenerateManagementSummary` intent |

**Correction (found during WO-06 of the build contract, which gates every model call site):** the original pass through this file missed the third `chatCompletionProvider.CompleteAsync` call site and concluded `report-insight` had no implementation at all. It does — `BuildManagementSummaryResponseAsync` is a real, model-backed path, reached by questions like "Prepare a management summary for today's claims." **Only one of ten agents (`claims-summary`) has no implementation whatsoever.** Three are deterministic rule engines presented as AI agents.

### 3.4 The governance hole

The kill switch is **not enforced**. `AgentCatalog` is referenced in only two places outside the registry itself:
- `AgentRegistryService` — writes/reads the switch
- `AgentWorkflowService:945` — validates that a workflow node's `AgentKey` names a real catalog entry

**No code path checks `Enabled` before invoking a model.** Disabling `command-center` in the Admin UI does not stop the Command Center from answering. The kill switch is a database write with no effect on runtime behaviour.

For a platform whose safety story rests on human authority over AI, an advertised-but-inert kill switch is a serious governance defect.

### 3.5 Missing audit primitives

The architecture doc §11 requires `AgentRun` and `AIInteraction` records for every agent execution, with tool traces. Neither type exists in the codebase. Agent activity is recorded only as generic `AuditEvent`s where a service happens to write one. There is no way to answer "which tools did the agent call, with what inputs, and on whose authority" — which is exactly what an insurance regulator asks.

---

## 4. Workflow-by-workflow analysis

### 4.1 Claim intake (FNOL) — **works, motor-only**
Create → validate → persist → route → SLA start → audit. `CaseIntakeService` is solid. Blocked only by the hardcoded product line and the absence of non-motor FNOL schemas.

### 4.2 Triage, assignment, escalation — **works**
`ClaimRoutingPolicy` (deterministic), assign and escalate endpoints wired to the queue UI, SLA calculation server-side. Sound.

### 4.3 Document intake & extraction — **works, with two honesty bugs**
Upload → blob → classify → extract → completeness recalculation → 2 audit events per file. Human correction and approval endpoints exist and are wired. Defects: no permission gating on the upload button; `NotConfigured` storage surfaces a raw provider exception instead of an honest state (§1.2).

### 4.4 Completeness & checklist — **works, shallow**
`DocumentChecklistRules` branches by claim-type kind and falls back to product line. Only Motor and Medical have real rule sets; everything else gets a one-item default (`ClaimForm`). Adequate for motor, thin for a multi-line insurer.

### 4.5 AI assistance / Command Center — **works, and is the best part of the product**
Real grounding via Azure AI Search, real citations, honest refusal when ungrounded, conversation persistence, markdown rendering. This is production-quality. It is also the *only* place a model actually runs.

### 4.6 Policy & knowledge Q&A — **backend real, UI fabricated or absent**
The capability works through Command Center. The Knowledge module is list-only despite four working management endpoints. The Policy Intelligence page is counterfeit (§2.1).

### 4.7 Customer communication — **drafting works, sending is orphaned**
`customer-updates/draft` → `approve` / `reject` → `send` is a genuinely correct human-gated chain with real AI drafting. But `POST /communications/send-email` and `/send-sms` exist with **no UI caller**, and the Communications page is read-only. The approval → send loop cannot be completed from the portal.

### 4.8 Approvals & settlement authority — **works**
`ApprovalRequest` / `ApprovalStatus` / `ApprovalDecision`, `ClaimAuthorityPolicy`, threshold config per product line, approve / reject / escalate / override / execute-next-step all wired. Human authority is properly enforced. Note: the Teams notification on decision requires Service Bus; for local testing set `INSURANCEINTELLIGENCE_QUEUE_PROVIDER=Mock`.

### 4.9 Fraud & risk — **signals only, no workflow**
`FraudRuleEngine` produces real advisory signals (duplicate contact, claim velocity, duplicate policy, low completeness) with correct non-accusatory severity wording. But there is **no disposition workflow**: no way to triage a signal, assign an investigation, record an outcome, or close it. It is a list you can look at.

### 4.10 Underwriting — **thinnest module in the product**
`UnderwritingRuleEngine` exists and is reasonable. But the only route is `GET /underwriting/reviews`. There is **no submission intake, no decision endpoint, no referral workflow, no quote, no bind**. The UI honestly says *"Submission intake is not configured yet"* — which is the correct behaviour, but it means underwriting is a stub.

### 4.11 Policy lifecycle — **read-only, no issuance**
`IPolicyRepository` has `Upsert`, but no HTTP route creates or modifies a policy. Policies exist only as demo-seeded reference data for claims to point at. There is no quote → bind → issue → endorse → renew → cancel lifecycle.

### 4.12 Settlement & payment — **absent**
`CaseStatus` includes `Settled` and `ReadyForExport`, but there is no reserve model, no payment instruction, no core-system write-back adapter. A claim can be marked settled; nothing settles.

### 4.13 Audit & compliance — **strong**
Audit events on all sensitive actions, correlation IDs, tenant-scoped queries, compliance posture endpoint, per-case audit pack with export to blob. This is the second-strongest area after Command Center.

### 4.14 Identity, profile, notifications — **weakest platform layer**
Real Entra/MSAL auth and real server-side permission resolution — good. But no user management UI, no profile screen, no settings screen, no sign-out control, no workspace switcher, no in-app notifications (§1.3–1.5).

---

## 5. Documentation audit

You asked me to confirm two things.

### 5.1 "Ensure AGENTS.md and CLAUDE.md are not phased, and are complete on agents/operation/tools"

**Phasing: already clean.** Zero phase/release/sprint references in `AGENTS.md`, `CLAUDE.md`, `CONTEXT.md`, `README.md`, `copilot.md`. One incidental hit in `IIP_IMPLEMENTATION_GUARDRAILS.md`. `AGENTS.md` is invariant-based and durable — it is a good document and I would not restructure it.

**Completeness on agents: deliberately delegated, and that is defensible.** `AGENTS.md:14` says explicitly:

> *"Do not put volatile inventories here: exact routes, agent/provider catalogs, UI tokens, current priorities, or numbered release gates belong with their owners."*

So the agent catalog correctly lives in `iip_ai_agent_architecture_tool_registry_updated.md` — and that document **is** complete to the level you asked for: every agent, its purpose, allowed inputs, allowed tools, output shape, and human gate; plus a 30-row tool registry with side-effect levels, required permissions, provider-health dependencies, and per-tool guardrails.

**The real problem is not that the docs are incomplete. It is that the docs are correct and the code does not implement them (§3).** `AGENTS.md:13` demands: *"When sources conflict, identify the conflict, follow the latest approved authority, and update the stale source."* That conflict is currently unreconciled and undeclared.

**Two documentation defects to fix:**

1. **Phased framing inside the agent architecture doc.** §15 is titled *"Phase 1-4 agent alignment"* and sequences agent delivery across phases. Per your instruction this should be replaced with a **target-state capability matrix** plus a separate, honest **current implementation status** table (the one in §3.3 above). 18 design documents carry `Phase N` / `Release N` headings — the `iip_release_1_*` files are legitimately release-scoped and should stay, but the core architecture documents should describe the complete system.

2. **Edition confusion.** The parent `Projects/CLAUDE.md` instructs agents to read `BUILD_BRIEF.md` and `IIP_SaaS_Fabric_Edition_Implementation_Guide.md` and describes this as the SaaS Fabric Edition. **Neither file exists in this repository**, and `AGENTS.md` here describes the Managed Application edition. Any agent following the parent instructions will fail to find its stated authority. This needs resolving — it is a live source conflict.

### 5.2 "Ensure the design markdown file is in check"

`iip_visual_design_system_and_tokens.md` and `iip_design_system_ux_behavior_specification.md` are present and detailed. The token system in `globals.css` is real and substantial (~45 semantic CSS custom properties).

**But the implementation has drifted into two parallel token vocabularies**, both wired in `tailwind.config.ts`:

| Vocabulary | Tokens | Usages in `components/` + `lovable-pages/` |
|---|---|---|
| Legacy/literal | `navy`, `stone`, `cloud`, `slate2` | **996** |
| Semantic | `ink`, `muted`, `line`, `danger` | **195** |

Nothing is broken — both resolve. But a designer reading the design system doc sees the semantic set, while 84% of the codebase uses the literal set. Colour names like `navy` and `cloud` also can't express theme states, which is why `TopBar` reaches for `bg-error` while the CSS layer defines `--color-danger`.

**Verdict:** the design docs are in reasonable shape; the *implementation* has drifted from them. This needs a token-consolidation pass, not a doc rewrite.

---

## 6. Cross-cutting defects

| # | Defect | Severity | Evidence |
|---|---|---|---|
| C1 | Fabricated insurance content on a navigable page | **P0** | `PolicyIntelligence.tsx` — fake answer, fake excess, fake citations, fake confidence |
| C2 | Agent kill switch is inert | **P0** | No `Enabled` check at any model call site |
| C3 | No `AgentRun` / `AIInteraction` audit records | **P0** | Required by architecture §11; type does not exist |
| C4 | Fake permanent "unread" notification badge | **P1** | `TopBar.tsx:101`, hardcoded |
| C5 | Actions rendered without permission gating | **P1** | Upload widget shown to roles lacking `UploadDocument` |
| C6 | Working endpoints with no UI caller | **P1** | admin/users, communications send, knowledge manage |
| C7 | Dead controls shipped | **P1** | Notification bell, Help, profile avatar, Policy "Ask", Policy "Escalate", 7 Admin tiles |
| C8 | Orphaned agent interfaces with zero implementations | **P1** | `AgentContracts.cs` — 6 interfaces, 0 implementations |
| C9 | Design token vocabulary drift | **P2** | 996 legacy vs 195 semantic usages |
| C10 | Docs describe a system the code does not implement | **P1** | 30 tools specified, 0 implemented |
| C11 | Edition/authority document conflict | **P1** | Parent `CLAUDE.md` cites files absent from this repo |
| C12 | ~200 tracked build-output files (86 MB working tree) on `main` | **P2** | `.gitignore` line 36 is `"**/artifacts/"` — literal quotes, so the rule matches nothing. `.git` is 42 MB; fix is `git rm --cached`, no history rewrite needed. |
| C13 | `deploy-staging.yml` targets dead App Service names | **P2** | Resources no longer exist in any subscription |

---

## 7. What it takes to reach production

### 7.1 One week — **not production. Achievable: an honest, demonstrable system.**

The goal in one week is to close the gap between what the system claims and what it does. No new capability.

| Priority | Work | Est. |
|---|---|---|
| P0 | **Delete or rewire Policy Intelligence.** Fastest safe fix: point the page at the working `/command-center/query` policy-answer path. If that is more than a day, delete the route and remove it from nav. | 1 d |
| P0 | **Enforce the agent kill switch.** Add an `IAgentGate` check at all three `CompleteAsync` call sites + both dedicated providers. Return an honest "agent disabled by workspace administrator" state. | 1 d |
| P0 | **Remove all fake state indicators.** The permanent notification dot first. | 0.5 d |
| P1 | **Hide or wire every dead control.** Bell, Help, avatar, Policy buttons, 7 Admin tiles. Anything that cannot be wired this week gets removed, not left inert. | 1 d |
| P1 | **Permission-gate action UI.** Render actions from `runtimeContext.permissions`, not unconditionally. Start with the upload widget. | 1 d |
| P1 | **Honest `NotConfigured` states.** Catch `ProviderNotConfiguredException` at the endpoint boundary and return a structured "not configured" response the UI renders as such. | 0.5 d |
| P1 | **Declare the doc/code conflict.** Add a "Current implementation status" section to the agent architecture doc with the §3.3 table. Resolve the parent-`CLAUDE.md` edition conflict. | 0.5 d |

**End state after one week:** every surface is either real or honestly labelled unavailable. Nothing fabricated. This is what makes the system safe to show an insurer — not feature-complete, but truthful.

### 7.2 One month — **a scoped production release for motor claims**

Weeks 2–4, assuming the week-1 work lands.

**Week 2 — Identity & platform shell**
- User management UI on the existing `admin/users` endpoints — list, invite, assign role, deactivate
- Profile menu on the avatar: display name, workspace switcher (data already in `/api/v1/me`), sign out
- Real Settings screen replacing the redirect stub
- In-app notification domain: entity, repository, feed endpoint, read/unread, `GET /api/v1/notifications` — then wire the bell

**Week 3 — Close the workflow loops**
- Product-line-driven FNOL: schema abstraction + Medical and Property forms alongside Motor
- Wire Communications send (the approval→send loop currently cannot be completed in the portal)
- Wire Knowledge management UI to the four existing endpoints
- Document upload in the Documents module, not only inside a claim
- Fraud signal disposition workflow: triage, assign, outcome, close

**Week 4 — Agentic layer, minimum credible version**
- Implement `IAgentToolRegistry` with the `read`-tier tools only (roughly 15 of the 30 — everything with side-effect level `read`)
- Add `AgentRun` + tool-trace audit records
- Route Command Center through the registry rather than hand-written intent branching
- Implement `claims-summary` (real gap, high demo value, low risk — read tools only)
- Leave `report-insight`, and the deterministic routing/fraud/underwriting engines, honestly labelled as rule-based rather than agentic

**Explicitly out of scope for one month** — these are quarters, not weeks, and should be stated as such to any prospect:
- Underwriting submission → decision → bind lifecycle
- Policy issuance / endorsement / renewal
- Settlement, reserves, payment instruction
- Core insurance-system write-back adapters
- `write` / `external_send` tier agent tools

### 7.3 Recommended production scope statement

> IIP v1 is a **motor claims operations platform** with grounded AI assistance and human-accountable approvals. It covers FNOL, triage, document intake and extraction, completeness, claims Q&A with cited evidence, customer-update drafting under human approval, approval workflow, and audit. Underwriting, policy administration, and settlement are advisory or out of scope in v1.

That is a defensible, honest, sellable product. Claiming the full agentic insurance platform today is not.

---

## 8. Priority order

1. **C1** — Policy Intelligence fabricated content (regulated-industry risk; do this first)
2. **C2** — Kill switch enforcement (safety story depends on it)
3. **C4, C7** — Fake indicators and dead controls (this is what reads as "vibe-coded")
4. **C5** — Permission-gated action UI
5. **C10, C11** — Reconcile docs to code; resolve the edition conflict
6. **C3, C8** — `AgentRun` audit and the tool registry
7. **C6** — Wire the orphaned endpoints
8. **C9, C12, C13** — Token consolidation, repo hygiene, deploy workflow

---

## 9. What is genuinely good — worth protecting

Because the tone above is corrective, this should be said plainly. The following are real assets and should not be disturbed while fixing the above:

- The domain model. Tenant isolation, permission resolution, audit event coverage, and human-authority policy are properly designed and enforced server-side.
- The Command Center AI path — grounded, cited, honest when it cannot answer.
- The approvals chain — human authority is real, not a rubber-stamp click.
- The provider-port architecture — every cloud dependency sits behind an interface with Azure / Mock / NotConfigured implementations. Offline mode works because of this.
- The audit and compliance layer.
- `AGENTS.md` itself — a genuinely good, durable, invariant-based contract.
- 67 backend test files and 23 Playwright specs.

The problem is not the foundation. The problem is a presentation layer that promises more than the foundation delivers, and an agentic layer that was removed and never rebuilt while the documentation and admin UI continued to advertise it.
