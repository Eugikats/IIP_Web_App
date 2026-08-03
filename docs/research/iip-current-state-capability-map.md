# IIP Current-State Capability Map (Ground Truth — Phase 1)

**Audit date:** 3 August 2026
**Branch:** `harry/undp-demo-prototype`
**Purpose:** Establish what IIP actually *is today* — real / partial / concept-only — so that every UNDP application answer can be reconciled to reality before submission.
**Method:** Direct codebase inspection, build, and test execution. Evidence labels below.

## Evidence labels

| Label | Meaning |
|---|---|
| **BUILT** | Real implementation present in the codebase; compiles; exercised by tests or wired into the running app. |
| **PARTIAL** | Real scaffolding/interface/adapter exists, but incomplete, unproven end-to-end, or not yet run against live infrastructure. |
| **CONCEPT** | Exists only as an enum value, config flag, doc, fixture, or design intent — no working implementation. |
| **ABSENT** | Named in contract/answers but not found in the codebase at all. |
| **UNVERIFIED** | Cannot be determined from the repository alone (e.g. whether a live Azure environment is currently running). |

---

## 0. Headline verdict

IIP is a **real, well-architected, compiling, tested backend + frontend** — far more substantial than a concept deck. But it is at approximately **Production Release Program Gate 1–2** (identity + tenant context done; real-Azure staging and provider proving not evidenced), **not** a live, deployed, insurer-operating platform. The Uganda-specific inclusion and distribution layer that dominates several UNDP answers is **almost entirely CONCEPT** — it exists as fixtures and enum values, not working integrations.

**One-line honesty rule for the application:** IIP *is* a genuine software platform under active construction. It is *not yet* a deployed, provider-proven, Uganda-integrated production system. Answers must be written in that register.

---

## 1. Build & test reality (BUILT)

- `dotnet build InsuranceIntelligence.sln` → **Build succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **424 passing, 0 failing** (106 Domain + 318 API tests, as of 2026-08-03; grows with each slice).
- 336+ C# files, 111+ `.tsx`, real EF Core migrations, real Next.js portal.
- **CI/CD present and green** — `.github/workflows/ci.yml` (backend build+test+vuln-scan, portal lint+checks+build, infra bicep compile); first run 30807368448 green on all jobs.

**Reconciliation impact:** We can legitimately say the platform "builds cleanly, passes its automated test suite, and is gated by CI on every push/PR."

---

## 2. Backend core architecture (BUILT)

| Capability | Status | Evidence |
|---|---|---|
| Clean layering (Api/Application/Domain/Infrastructure/Functions) | **BUILT** | Matches AGENTS.md §6.2 |
| Four core ports (TenantContext, Permission, Audit, ProviderHealth) | **BUILT** | `EfCorePorts.cs`, `PermissionService`, `AuditWriter`, middleware |
| Server-side tenant/workspace resolution | **BUILT** | `AuthenticatedTenantResolver`, `TenantWorkspaceContextMiddleware`, Gate 1 commits |
| Multitenant Entra ID identity | **BUILT** | `/me` resolves tenant/role from membership store (ADR-0005), oid-first key |
| EF Core repositories on Azure SQL | **BUILT** | `EfRepositories.cs`, 6 migrations incl. agent workflow control plane |
| Human-supervised decision guard | **BUILT** | `ApprovalDecisionGuard`, system prompts enforce it, contract §4 |
| Audit writing on state changes | **BUILT** | `EfAuditWriter`, audit query service |
| ~40 `/api/v1` routes | **BUILT** | Program.cs endpoint map; openapi.json (94 paths) |

**This is the genuinely strong core.** Tenant isolation, server-side authz, audit, and the human-review ceiling are real and tested.

---

## 3. Azure provider adapters (BUILT as code / UNVERIFIED as live-proven)

All named Azure adapters **exist as real REST/SDK implementations** in `src/InsuranceIntelligence.Api/Azure/`, each with an honest `NotConfigured`/`LocalMock` fallback gated by config and a fail-closed startup validator:

| Provider | Adapter | Fallback | Status |
|---|---|---|---|
| Azure OpenAI (chat, embeddings, assistant, customer-draft) | `AzureOpenAi*Provider` | `NotConfigured*` | **BUILT** (real REST + managed identity) |
| Azure Blob (documents, knowledge, evidence packs) | `AzureBlob*StorageProvider` | `LocalMock*` | **BUILT** |
| Azure Document Intelligence | `AzureDocumentIntelligenceProvider` | `NotConfiguredDocumentProcessingProvider` | **BUILT** |
| Azure AI Search (retrieval + knowledge index) | `AzureAiSearch*Provider` | `NotConfigured*` | **BUILT** |
| Azure Service Bus | `AzureServiceBusQueueDispatcher` | `MockMessageQueueDispatcher` | **BUILT** |
| Microsoft Graph email (in/out) | `GraphMailProvider`, `GraphCommunicationSendingProvider` | `Disabled*` | **BUILT** |

**Critical nuance:** These adapters are *written* and unit-tested, but the repository provides **no evidence that a live Azure environment (real SQL, Blob, OpenAI, Doc Intelligence, Search) is currently provisioned and has processed a real end-to-end claim.** Production Release Program Gate 3 ("activate and prove all real providers") is **UNVERIFIED**.

**Architecture deviation (minor):** these adapters live in `Api/Azure/` rather than `Infrastructure/` as AGENTS.md §11.2 mandates. Real, but misplaced — technical debt, not a capability gap.

**Reconciliation impact:** We can say "IIP has real Azure AI/Blob/Search/Document Intelligence/OpenAI/Service Bus adapters implemented." We **cannot** say "IIP is running on live Azure infrastructure" or "has processed real claims" without confirming a live deployment.

---

## 4. AI agents & document intelligence (PARTIAL)

- 10 canonical agents defined in contract (§13); AI providers real; `AgentRegistryService` (101 lines) + `AgentWorkflowService` (729 lines) = a real **agent workflow control plane** with a persisted kill-switch (ADR-0002) and a July 2026 migration.
- Document extraction pipeline (upload → Doc Intelligence → completeness → review → correct field) is **BUILT** as services + endpoints.
- Multi-agent orchestration (Bevaya-style typed flow graph across agents) is **deliberately CONCEPT** per AGENTS.md §14 ("do not introduce prematurely").

**Reconciliation impact:** "AI extracts, classifies, drafts, retrieves, and routes for human review" = defensible as *implemented capability*. "Autonomous multi-agent workflows" = not built, and intentionally so.

---

## 5. Frontend portal (BUILT, mid-migration)

- Real Next.js app; operator app under `/app/*` in `AppShell`; login at `/`.
- **Per-screen data hooks** (`lib/lovable-parity/data/*`) fetch **real `/api/v1`** endpoints, map DTOs, and render **honest empty states** on error/empty.
- `LovableVisualParity` fixtures (the Uganda-flavoured demo data — Nakato, boda-boda, UGX) are gated behind dev-only `NEXT_PUBLIC_VISUAL_PARITY_MODE=true` and **never run deployed**.
- Migration off fixtures onto real data is **in progress** (see `lovable-parity-migration-audit`).

**Reconciliation impact:** The UI is real and API-wired. But the polished screens a viewer might see in parity mode are **demo fixtures**, not live data. Do not present screenshots of parity-mode data as "the system in production."

---

## 6. Deployment & packaging (PARTIAL / ABSENT)

| Item | Status | Evidence |
|---|---|---|
| Bicep IaC (dev foundation, functions runtime) | **PARTIAL** | `infra/bicep/phase3-dev-foundation.bicep`, `phase6-functions-runtime.bicep` |
| Staging parameters | **PARTIAL** | `infra/parameters/stg.bicepparam`, `staging-sales-mirror.bicepparam` |
| **Azure Managed Application marketplace packaging** | **ABSENT** | No `infra/bicep/marketplace/` dir; AGENTS.md §21 files not created |
| Partner Center private plan | **ABSENT** | No package, no submission artifacts |
| Live deployed environment | **UNVERIFIED** | No azd config, no CI/CD, no deployment state in repo |

**Reconciliation impact:** The Feasibility Q2 claim that "the Azure Managed Application packaging model is established" is **NOT accurate** — the packaging track has not been built. Soften to "the deployment architecture targets Azure Managed Application; packaging is planned."

---

## 7. Uganda inclusion & distribution layer (CONCEPT / ABSENT — the biggest gap)

This is the layer that dominates UNDP Sections 3 (Inclusion) and 5 (Distribution). Reality:

| Claimed capability (in our answers) | Status | Evidence |
|---|---|---|
| USSD access (feature phones) | **ABSENT** | No USSD gateway / session flow (Africa's Talking SMS adapter exists separately) |
| SMS notifications | **PARTIAL** | `AfricasTalkingSmsProvider` behind `ISmsProvider` when `Infrastructure:SmsProvider=AfricasTalking`; Uganda MSISDN normalize; tiered `OutboundSmsGuard` (human approval OR low-risk + `AutoSendTransactionalSmsEnabled`, default OFF). Live credentials / staging prove still pending. |
| WhatsApp document submission | **PARTIAL** | `IWhatsAppInboundProvider` + `WhatsAppCloudApiInboundProvider` when `ChannelProvider=WhatsAppCloudApi`; webhook verify + `X-Hub-Signature-256`; media download → case document via existing storage/processing spine (email attachment pattern). Live Meta credentials / staging prove pending. |
| Mobile money (MTN/Airtel) premium collection | **ABSENT** | No integration, no interface |
| Offline-capable field agent app | **ABSENT** | Not in codebase |
| Local languages (Luganda, etc.) | **ABSENT** | No i18n/localization in code |
| Agent-assisted intermediary workflows | **CONCEPT** | Design intent only; not a built role/flow |
| Contestability (human re-review request) | **PARTIAL** | `ClaimReviewRequest` domain + EF `ClaimReviewRequests` + client-portal `POST .../request-review` (owner-scoped) + audit `claim.review_requested`. Staff acknowledge/resolve UI and SMS/USSD entry deferred (PEND-CONTEST-01/02). |
| SACCO/MFI partner integration | **CONCEPT** | Generic multi-tenant/API scaffolding exists; no partner-specific integration |

The Uganda specifics (customer names, UGX amounts, Kampala streets, boda-boda claims) live entirely in `web/portal/lib/lovable-parity/mock.ts` — **dev-only fixtures**, never deployed.

**Reconciliation impact (most important in the whole audit):** Nearly every concrete inclusion/distribution mechanism in Sections 3 and 5 is **not built**. These must be rewritten from present-tense ("IIP incorporates USSD, WhatsApp, offline sync…") to **planned/roadmap tense** ("IIP is designed to incorporate…", "the roadmap includes…"). This aligns with the Bevaya research §11.3, which independently concluded these Uganda integrations must not be assumed.

---

## 8. Reconciliation summary — how each answer section stands

| Section | Verdict | Action |
|---|---|---|
| **Relevance & Alignment (Q1–Q3)** | Mostly SAFE — describes the problem and the *intended* mechanism; problem evidence is external and real | Keep; ensure mechanism verbs read as "designed to" where about Uganda channels |
| **Originality (Q1–Q2)** | SAFE on architecture (real), CONCEPT on Uganda-specific disruption | Soften Uganda-distribution claims; the operations-layer + human-supervised AI claims are real |
| **Inclusion (Q1–Q2)** | **HIGH RISK** — most mechanisms CONCEPT/ABSENT | Rewrite to roadmap tense; do not claim USSD/WhatsApp/offline/local-language as present |
| **Feasibility (Q1a technical)** | SAFE — adapters real; **but** "Managed App packaging established" is false | Correct packaging claim; keep provider stack (real adapters) |
| **Feasibility (Q1b/c, Q2, Q3)** | PARTIAL — capacity answer over-claims live infra & packaging | Reframe to "adapters built, staging IaC exists, live provisioning pending" |
| **Scalability (Q1 traction)** | Needs honesty — no live customers, no processed claims | Frame as "pre-pilot; platform builds & passes tests; no production claims processed yet" |
| **Scalability (Q2–Q4)** | Market data SAFE (external, verified); distribution mechanisms CONCEPT | Keep market figures; soften distribution-integration claims |
| **Sustainability (Q1–Q2)** | Revenue model is projection (fine); risks are real | Keep as forward-looking; no code claims embedded |

---

## 9. What is genuinely strong (lead with these — they're real)

1. Clean, tested, compiling .NET 8 + Next.js platform (383 tests green).
2. Real tenant isolation, server-side authorization, audit trail, human-review ceiling.
3. Real Azure adapters for the full AI/document/retrieval/storage/queue stack (with honest Not-configured fallbacks — the zero-simulation discipline is actually enforced in code).
4. Multitenant Entra identity resolved server-side.
5. A real agent workflow control plane with kill-switch governance.
6. Honest empty states and `Not configured` surfaces — the platform does not fake success.

## 10. What must not be claimed as present

1. Live Azure deployment / processed real claims (UNVERIFIED — confirm with user).
2. Azure Managed Application packaging / Partner Center readiness (ABSENT).
3. USSD, mobile money, offline agent app, local languages (CONCEPT/ABSENT). SMS outbound and WhatsApp inbound adapters are PARTIAL (config-gated; live prove pending).
4. Any live insurer, SACCO, or MFI integration or pilot (CONCEPT).
5. Autonomous / multi-agent orchestration (deliberately not built).
6. CI/CD gate enforcement (no workflows present).

---

## 11. Confirmed ground truth (from founder, 3 Aug 2026)

- [x] **Live Azure environment:** None running. IaC-only at present.
- [x] **Real claim processed end-to-end:** Never. No non-fixture claim has been processed.
- [x] **Commitments:** No signed/verbal insurer, SACCO, or MFI commitments — **except one company that has requested a demo** to see how the system works. That is the extent of external traction.
- [x] **Legal entity:** Formally established. Broader legal frameworks/implementations not yet in place (deferred).
- [x] **Overall stance:** Still funding and building. Substantial progress but **not production-ready**. Known refinement areas: UI/UX, colour palettes, visual design, design system.

## 12. Direction for reconciliation (founder intent)

- Application answers that describe unbuilt features reflect **genuine intent**, not deception. The reconciliation goal is a truthful **current-state vs. roadmap** register — not deletion of the vision.
- **Bevaya:** adopt UX practices, system workflows, and integration methods. **Do not** adopt its visual design / colour palette (founder dislikes it). IIP's visual identity will diverge.
- Design system + visual design are explicitly **works in progress** slated for refinement.
- The one honest traction fact available: **one insurer has requested a demo** — the clickable prototype at `prototypes/iip-undp-demo.PROTOTYPE.html` directly serves this.
