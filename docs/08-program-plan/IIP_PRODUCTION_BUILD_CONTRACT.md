# IIP — Production Build Contract

**Type:** Executable build order. This is a document to *run*, not to read.
**Executor:** AI coding agent, end to end, across multiple sessions.
**Baseline:** `main` @ `1baf505`
**Evidence base:** [IIP_SYSTEM_ANALYSIS_AND_PRODUCTION_PLAN.md](IIP_SYSTEM_ANALYSIS_AND_PRODUCTION_PLAN.md) — every claim in this contract is sourced there. Do not re-derive it; read it once at start.
**Authority:** [`AGENTS.md`](../../AGENTS.md) outranks this document. Where they conflict, follow `AGENTS.md` and record the conflict in §9.

---

## 0. How to run this document

Read §1–§3 in full before touching code. Then execute work orders in dependency order, stopping at every checkpoint.

**The loop, per work order:**

1. Read the work order in full.
2. Verify its **Preconditions**. If any fails, stop and record a blocker (§9). Do not proceed on assumption.
3. Read the actual current code at every path listed under **Touch**. The contract states intent; the code is the truth. If they disagree, the code wins and you record the drift (§9).
4. Implement **Required behaviour**.
5. Re-read **Forbidden** and check your own diff against every line of it.
6. Run **Verify** exactly as written. Paste real output into the ledger.
7. Update the **Ledger** (§8). Commit.
8. Move to the next work order. At a checkpoint, stop and run the full gate.

**There are no phases and no schedule in this document.** Work orders are ordered by dependency only. Checkpoints are objective gates, not milestones — a checkpoint either passes or it does not.

---

## 1. Non-negotiable invariants

These bind every work order. A change that violates any of them is wrong even if it makes a test pass.

**I1 — Nothing fabricated.** No hardcoded insurer data, claim figures, policy clauses, citations, confidence scores, metrics, or AI answers anywhere a user can reach. Empty stays empty. Missing capability renders as `Not configured`.

**I2 — No dead controls.** Every interactive element either performs its stated action or is not rendered. A disabled control must state why. A button with no handler is a defect, not a placeholder.

**I3 — Nothing advertised that is not implemented.** If a capability appears in the UI, an admin surface, or a customer-facing document, it exists and runs. If it does not exist, it is not shown.

**I4 — Human authority is real.** AI never finally approves, rejects, repudiates, settles, prices, binds, or issues adverse determinations. Human gates carry evidence and real authority, not a rubber-stamp click.

**I5 — Server-side authority always.** Tenant, workspace, and permission resolve server-side on every request and background action. Client state is never business authority.

**I6 — Grounded outputs only.** Consequential AI output cites current approved tenant-scoped evidence. Model memory is not evidence. When grounding is absent, refuse honestly — never generate.

**I7 — Auditable.** Sensitive reads, state changes, AI/provider actions, approvals, communications, and exports write audit records with safe metadata and correlation IDs.

**I8 — No secrets, ever.** No credentials, tokens, keys, SAS URLs, or real customer evidence in code, logs, tests, or commits.

**I9 — Honest failure.** Raw provider exceptions never reach a user. Failures surface as structured, actionable states. Never substitute a mock for a failed real provider outside explicitly local runtimes.

---

## 2. Anti-cheat rules

**Read this section twice.** The defects this contract exists to fix were produced by an AI taking exactly these shortcuts. Every rule below corresponds to a real defect found in this repository.

**A1 — Never fabricate to fill a surface.** If a page needs data and the endpoint does not exist, build the endpoint or render an empty state. Do not write a plausible-looking constant. *(Origin: `PolicyIntelligence.tsx` — a fake AI answer with a fake excess figure, fake clause citations, and a fake 0.88 confidence badge.)*

**A2 — Never ship an interface with no implementation.** If you define a port, implement it in the same work order or do not define it. *(Origin: `AgentContracts.cs` — six orphaned interfaces, zero implementations.)*

**A3 — Never write state that nothing reads.** A setting, flag, or switch must change runtime behaviour. Persisting it is not implementing it. *(Origin: the agent kill switch — written to the database, checked by nothing.)*

**A4 — Never render a control you have not wired.** *(Origin: notification bell, Help button, profile avatar, Policy "Ask" and "Escalate", seven static Admin tiles.)*

**A5 — Never hardcode a state indicator.** Badges, dots, counts, and status pills read from real state or do not render. *(Origin: the permanently-lit unread notification dot.)*

**A6 — Never leave an endpoint without a caller, or a caller without an endpoint.** Both directions are defects. *(Origin: `admin/users`, `communications/send-*`, and four knowledge-management endpoints — all live, none reachable from the portal.)*

**A7 — Never mark work complete on a partial vertical.** A slice includes: domain, endpoint, permission, audit, UI, empty/error/not-configured states, and tests. Missing any one means the work order is not done.

**A8 — Never weaken a check to make a test pass.** If a test blocks you, the test is probably right. Fix the code. If the test is genuinely wrong, say so explicitly in the ledger with reasoning — never silently relax it.

**A9 — Never report a command you did not run.** Paste real output. "Tests pass" without output is a contract violation.

**A10 — Never expand scope silently.** Out-of-scope work (§7) is not yours to start. Record it in §9 and continue.

**A11 — When blocked, stop and record.** Do not invent a workaround, a mock, or a fallback path to get past a missing credential, absent resource, or unclear requirement. Record the blocker and move to the next independent work order.

---

## 3. Definition of done

The build is complete when all of the following hold. Each is objectively checkable.

| # | Condition | How it is proven |
|---|---|---|
| D1 | Backend builds and all tests pass | `dotnet build` + `dotnet test`, output pasted |
| D2 | Portal lints, tests, and builds | `pnpm lint && pnpm test && pnpm build`, output pasted |
| D3 | Zero fabricated business content | §4 WO-01 audit re-run, returns empty |
| D4 | Zero dead controls | §4 WO-03 inventory re-run, returns empty |
| D5 | Every agent in the admin surface is implemented and gated, or labelled unimplemented | Agent status table matches code |
| D6 | Kill switch demonstrably stops execution | Integration test: disable → call → refused |
| D7 | Every AI execution writes an `AgentRun` record with tool trace | Integration test asserts the record |
| D8 | No endpoint without a UI caller; no caller without an endpoint | §4 WO-06 reconciliation table, both columns empty |
| D9 | Actions render only when the signed-in user holds the permission | Test per gated action |
| D10 | No unconfigured provider surfaces a raw exception | Test per provider port |
| D11 | Docs match code | Agent status table in the architecture doc equals reality |
| D12 | Ledger complete: every work order has real command output | §8 |

---

## 4. Work orders

Dependency graph. `→` means "must complete before".

```
WO-01 ─┬→ WO-02 → WO-03 ─┬→ CP-A ─┬→ WO-06 → WO-07 ─┬→ CP-B ─┬→ WO-08 → WO-09 ─┬→ WO-10 → WO-11 → CP-C
       ├→ WO-04 ─────────┤        │                  │        │                  │
       └→ WO-05 ─────────┘        └→ (independent) ──┘        └→ WO-12 → WO-13 → WO-14 → CP-D
                                                                        │
                                       WO-15, WO-16, WO-17, WO-18, WO-19 ┴→ CP-E → WO-20, WO-21, WO-22 → CP-F
```

WO-15 through WO-19 are mutually independent and may run in any order after CP-B.

---

### WO-01 — Eliminate fabricated business content

**Intent:** No user-reachable surface renders invented insurance data. This is the highest-risk defect in the repository.

**Preconditions:** none. This is the entry point.

**Touch:**
- `web/portal/lovable-pages/PolicyIntelligence.tsx`
- `web/portal/app/app/policy/page.tsx`
- `web/portal/components/iip/Sidebar.tsx`
- plus every file the audit below returns

**Required behaviour:**

1. Run a repository-wide audit for fabricated business content. Search `web/portal/lovable-pages/` and `web/portal/components/` for module-level constants containing: policy numbers, currency amounts, clause identifiers, confidence values, claim references, customer names, document page citations, or metric values. Record every hit in the ledger with a keep/remove decision and a reason. Legitimate configuration (nav entries, filter option lists, form field definitions) is a keep — state why.
2. `PolicyIntelligence.tsx` is the known worst case: a fabricated AI answer, a fabricated excess figure, four fabricated clause citations with page numbers, a fabricated `0.88` confidence badge, a fabricated regulatory reference, and two handler-less buttons. Resolve it one of two ways:
   - **Preferred** — rewire the page to the working grounded path. `CommandCenterQueryService` already answers policy questions with real Azure AI Search grounding, real citations, and honest refusal when ungrounded. Reuse it. The "Ask" button submits a real query; results render real citations; no-grounding renders the honest refusal.
   - **Fallback** — if the rewire cannot be completed cleanly within this work order, delete the page, delete the route, and remove the nav entry. Record which option you took and why.
   - Deleting is strictly better than leaving it. Do not take a third option.
3. Apply the same keep/remove decision to every other audit hit.

**Forbidden:**
- Leaving the page reachable with fabricated content behind any flag, banner, disclaimer, or "demo" label. A disclaimer does not make invented coverage terms safe.
- Replacing fabricated data with differently-fabricated data.
- Adding a "sample data" mode as an escape hatch.

**Verify:**
```bash
cd web/portal && pnpm lint && pnpm build
```
- Re-run the content audit; it returns no un-actioned hits.
- Navigate every route in `Sidebar.tsx`. Confirm no invented business content renders.

**Done when:** the audit is empty and the ledger records a decision and reason for every original hit.

---

### WO-02 — Remove hardcoded state indicators

**Intent:** No badge, dot, count, or status pill asserts a state the system has not computed.

**Preconditions:** WO-01 complete.

**Touch:** `web/portal/components/iip/TopBar.tsx` (known: the unconditional unread dot at the notification bell), plus audit results.

**Required behaviour:**
1. Audit all badge, dot, count, and status-pill renders across `components/iip/` and `lovable-pages/`. For each, identify its data source.
2. Any indicator not derived from real state: remove it, or bind it to real state within this work order. Removal is acceptable; a fabricated indicator is not.
3. The notification unread dot has no backing domain at all (there is no in-app notification model — see WO-14). Remove it now. WO-14 reinstates it against real state.

**Forbidden:** binding an indicator to a placeholder, a constant, a random value, or a client-side guess.

**Verify:** `cd web/portal && pnpm lint && pnpm build`. Ledger lists each indicator, its source, and its disposition.

**Done when:** every rendered indicator traces to real state, or is gone.

---

### WO-03 — Dead control inventory and resolution

**Intent:** Every interactive element does what it appears to do, or is not rendered.

**Preconditions:** WO-02 complete.

**Touch:** `web/portal/components/iip/TopBar.tsx`, `web/portal/lovable-pages/Admin.tsx`, plus audit results.

**Required behaviour:**
1. Build a complete inventory of interactive elements across the portal: every `<button>`, clickable `<div>`, `<a>`, form submit, and menu trigger. For each, record whether it has a working handler that reaches a real endpoint or performs real local state change.
2. Known dead controls to resolve — confirm each against current code before acting, others may exist:
   - Notification bell — no handler (WO-14 wires it; remove or disable with a stated reason until then)
   - Help button — no handler
   - Profile avatar — rendered as `<div role="img">`, not interactive at all (WO-13 wires it)
   - `Admin.tsx` — the seven "Tenant profile" tiles render as static text but read as settings entries. "Users & roles" is the most misleading: `GET/POST/PUT /api/v1/admin/users` all exist with no UI (WO-12 wires it).
3. For each dead control, choose exactly one: **wire it now**, **remove it**, or **defer to a named work order and disable it with a visible reason in the interim**. Deferral requires naming the work order.

**Forbidden:** leaving any control inert with no disposition. "It will be wired later" without a named work order and a visible disabled state is a violation of I2.

**Verify:** `cd web/portal && pnpm lint && pnpm test && pnpm build`. Ledger contains the full inventory table with a disposition per row.

**Done when:** every row has a disposition and no inert control ships.

---

### WO-04 — Honest not-configured states

**Intent:** An unconfigured provider produces a clear, actionable state — never a raw exception, never a silent failure, never a fake success.

**Preconditions:** WO-01 complete. Independent of WO-02/03.

**Touch:**
- `src/InsuranceIntelligence.Api/Local/NotConfiguredProviders.cs` (`ProviderNotConfiguredException` and the eight `NotConfigured*` providers)
- API endpoint error-handling boundary
- portal data modules under `web/portal/lib/lovable-parity/data/`

**Required behaviour:**
1. Enumerate every provider port and its `NotConfigured` implementation. Known ports: AI assistant, communication, customer-update drafting, document processing, document storage, knowledge index, knowledge storage, retrieval. Confirm the current list.
2. At the API boundary, catch `ProviderNotConfiguredException` and return a structured response identifying the capability and that it is not configured. No stack traces, no provider names that leak infrastructure detail, no secrets.
3. In the portal, render that structured response as an honest not-configured state — the capability is unavailable and why, in operator-comprehensible terms.
4. Known live case: document upload fails opaquely when document storage is unconfigured. Fix it as part of this work order.

**Forbidden:**
- Falling back to a mock provider when a real one is unconfigured, outside explicitly local runtimes.
- Rendering not-configured as an empty success.
- Surfacing raw provider errors to any user.

**Verify:** `dotnet test InsuranceIntelligence.sln`. Add a test per port asserting the structured not-configured response. Manually exercise upload with document storage unconfigured; confirm the honest state.

**Done when:** every port has a test proving its not-configured path, and no raw provider exception can reach a user.

---

### WO-05 — Permission-gated action rendering

**Intent:** A user is never offered an action their role cannot perform.

**Preconditions:** WO-01 complete. Independent of WO-02/03/04.

**Touch:** `web/portal/lib/runtimeContext.tsx`, `web/portal/components/iip/DocumentUploadWidget.tsx`, and every action-bearing surface.

**Required behaviour:**
1. `GET /api/v1/me` already returns the signed-in user's real permissions. Establish one consistent way for UI to gate on it. If a helper exists, use it; if not, add one. Do not create a second parallel mechanism.
2. Audit every action control against the permission its endpoint requires (`PermissionEndpointFilter` on the route is the source of truth). Record mismatches.
3. Known live case: the document upload widget renders unconditionally, but `RolePermissionMap` shows `Permission.UploadDocument` is absent from `ITAdmin` and `Compliance` — those roles see a button that returns 403. Fix it, then work through the rest of the audit.
4. Server-side enforcement is already correct and must not be touched. This work order changes only what is *offered*, never what is *allowed*.

**Forbidden:**
- Weakening or removing any server-side permission filter.
- Gating on the preview role switcher. That is a display preview and explicitly not authority — `RoleSwitcher.tsx` announces "Signed-in authority remains unchanged." Gate on `runtimeContext.permissions` only.
- Hiding an action so a permission check becomes unreachable and then removing the check.

**Verify:** `dotnet test` and `pnpm test`. Add a portal test per gated action asserting it is absent without the permission. Ledger contains the action → required permission → gated? table with no mismatches.

**Done when:** the table has no mismatched rows.

---

## ▣ CHECKPOINT A — Truthful

**Gate:** the system contains nothing invented and nothing inert. Every surface is real or honestly labelled unavailable.

| Check | Pass condition |
|---|---|
| A-1 | `dotnet build` and `dotnet test` — clean, output pasted |
| A-2 | `pnpm lint && pnpm test && pnpm build` — clean, output pasted |
| A-3 | Fabricated-content audit returns empty |
| A-4 | Dead-control inventory: every row dispositioned, none inert |
| A-5 | Every state indicator traces to real state |
| A-6 | Every provider port has a not-configured test |
| A-7 | Action → permission table has no mismatches |
| A-8 | Manual walk of every nav route: nothing invented, nothing inert |

**If any check fails, stop. Do not proceed to WO-06.**

*This checkpoint is the single most important gate in the contract. After it, the system is safe to demonstrate — not feature-complete, but truthful. Everything after this adds capability to a trustworthy base.*

---

### WO-06 — Enforce the agent kill switch

**Intent:** Disabling an agent actually stops it. Today the switch is a database write that nothing reads.

**Preconditions:** CP-A passed.

**Touch:**
- `src/InsuranceIntelligence.Domain/Agents/AgentRegistry.cs`
- `src/InsuranceIntelligence.Application/Agents/AgentRegistryService.cs`
- `src/InsuranceIntelligence.Application/CommandCenter/CommandCenterQueryService.cs` (model call sites — approximately lines 296, 556, 685; confirm current line numbers)
- `src/InsuranceIntelligence.Api/Azure/AzureOpenAiProviders.cs` (`AzureOpenAiCustomerUpdateDraftingProvider`, `AzureOpenAiAssistantProvider`)

**Required behaviour:**
1. Introduce a single enforcement seam — an agent gate resolving tenant-scoped enabled state from the existing persisted kill-switch data via `AgentRegistryMerge`.
2. Every path that invokes a model checks the gate for its agent key **before** invocation. Confirm the current call-site inventory yourself; the analysis found three in `CommandCenterQueryService` plus two dedicated providers, but verify against the code you are editing.
3. A disabled agent returns an honest refusal: the agent is disabled for this workspace, who disabled it, and when. The persisted state already carries `UpdatedByUserId`, `UpdatedAt`, and `Reason` — use them.
4. Refusal is not an error state and not an empty result. It is a distinct, rendered outcome.
5. Write an audit record on every gated refusal (I7).

**Forbidden:**
- Checking the gate after the model call.
- Any bypass path, flag, or "internal" caller that skips the gate.
- Degrading to a cached or canned response when an agent is disabled.

**Verify:** `dotnet test InsuranceIntelligence.sln`. Add an integration test per agent key: disable → invoke → assert refusal, assert no model call, assert audit record. Manually disable an agent in `/app/admin/agents` and confirm the Command Center refuses.

**Done when:** every model call site is gated and a test proves each one.

---

### WO-07 — Agent run and tool-trace audit records

**Intent:** Every AI execution is reconstructable: which agent, which tools, what inputs, whose authority, what evidence.

**Preconditions:** WO-06 complete.

**Touch:** `src/InsuranceIntelligence.Domain/Agents/`, `src/InsuranceIntelligence.Domain/Audit/AuditEvent.cs`, `src/InsuranceIntelligence.Infrastructure/Persistence/`, migrations.

**Required behaviour:**
1. Read §11 of `docs/03-design-architecture/iip_ai_agent_architecture_tool_registry_updated.md`. It specifies `AgentRun` and `AIInteraction` records. Neither type currently exists. Implement to that specification.
2. An `AgentRun` records at minimum: agent key, tenant/workspace scope, invoking user, correlation ID, start/end, outcome (completed / refused / gated / failed), evidence references, and the tool trace.
3. Persist tenant-scoped. Most entities here use the `PersistedScopedJson` pattern — follow the existing convention rather than inventing a new one. If a migration is required, generate it against the current model and confirm it does not duplicate existing tables. *(This repository has an existing defect where a migration was generated against a stale model and recreated ~28 tables — do not repeat it.)*
4. Every gated refusal from WO-06 also writes an `AgentRun`.

**Forbidden:**
- Recording prompts, secrets, or raw provider payloads. Safe metadata only (I8).
- A run record that omits the tool trace once WO-08 lands — revisit and complete it there.

**Verify:** `dotnet test`. Integration test: invoke Command Center → assert `AgentRun` written with correct scope, user, correlation, and outcome. Confirm the migration applies cleanly to a database already at head.

**Done when:** every AI execution path writes a retrievable, tenant-scoped run record.

---

## ▣ CHECKPOINT B — Governed

**Gate:** AI execution is controllable and auditable.

| Check | Pass condition |
|---|---|
| B-1 | `dotnet build` + `dotnet test` — clean, output pasted |
| B-2 | Kill switch stops execution — test per agent key |
| B-3 | No ungated model call site exists — inventory proven complete |
| B-4 | Every execution writes an `AgentRun` with scope, user, correlation, outcome |
| B-5 | Gated refusals are audited and render honestly |
| B-6 | No prompts or secrets in any audit record |

**If any check fails, stop.**

---

### WO-08 — Agent tool registry, read tier

**Intent:** Agents act through typed, permission-checked, audited tools — not ad-hoc service calls. This is the capability the product is named for and it currently does not exist.

**Preconditions:** CP-B passed.

**Touch:** `src/InsuranceIntelligence.Domain/Agents/AgentContracts.cs`, new application-layer tool implementations, `src/InsuranceIntelligence.Api/Program.cs` (registration, and the stale note at ~line 361).

**Required behaviour:**
1. Read §7, §8, and §9 of the agent architecture doc. §9 specifies 30 tools with side-effect levels, allowed agents, required permissions, provider-health dependencies, and per-tool guardrails.
2. `AgentContracts.cs` already declares `IAgentTool`, `IAgentToolRegistry`, `IAgentEvidenceBuilder` and related types with **zero implementations**. Implement them — do not redesign the contracts unless the code proves them unworkable, and record the reasoning if so.
3. Implement **only the `read` side-effect tier** in this work order — roughly 15 of the 30 tools. Each tool: declares its descriptor, resolves tenant/workspace server-side, checks its required permission, returns typed results with evidence references, and writes to the tool trace on the current `AgentRun`.
4. A tool whose provider is unhealthy or unconfigured fails closed with an honest state (I9), never a fabricated result.
5. Delete the stale note at `Program.cs` ~line 361 once its claim is no longer true.

**Forbidden:**
- Implementing `write`, `external_send`, or `approval_required` tiers here. They are out of scope for this work order and gated behind human approval design.
- Giving a tool raw database access. Tools call application services.
- A tool that skips its permission check because "the caller already checked."
- Declaring a tool in the registry without implementing it (A2).

**Verify:** `dotnet test`. Per tool: a test for the happy path, a test for permission denial, and a test for tenant isolation (a tool must never return another tenant's data). Ledger contains the implemented-tool table mapped to the doc's §9 rows.

**Done when:** every `read`-tier tool is implemented, permission-checked, tenant-isolated, traced, and tested.

---

### WO-09 — Route Command Center through the tool registry

**Intent:** The one working AI path becomes governed rather than hand-wired.

**Preconditions:** WO-08 complete.

**Touch:** `src/InsuranceIntelligence.Application/CommandCenter/CommandCenterQueryService.cs`, `CommandCenterIntentContracts.cs`.

**Required behaviour:**
1. Command Center currently reaches data through a hand-written deterministic intent classifier and direct service calls. Move data access to the registry from WO-08.
2. **Preserve every existing behaviour**: grounded retrieval, real citations, honest refusal when ungrounded, conversation persistence, structured response blocks, markdown rendering. This is the strongest surface in the product — a regression here is a serious failure.
3. The deterministic classifier may remain as the routing mechanism. This work order changes how the agent *fetches*, not how it *decides*.
4. Tool calls populate the `AgentRun` tool trace from WO-07.

**Forbidden:**
- Regressing grounding or citation behaviour.
- Removing the honest-refusal path. Ungrounded questions must still refuse, never generate.
- Replacing the deterministic classifier with model-based routing in this work order.

**Verify:** `dotnet test`. All existing Command Center tests pass unmodified — if a test needs changing, that is evidence of regression; justify it explicitly in the ledger (A8). Manually confirm: a grounded policy question returns a cited answer; an ungrounded one refuses; the run record shows the tool trace.

**Done when:** Command Center runs through the registry with no behavioural regression and a populated tool trace.

---

### WO-10 — Implement the claims-summary agent

**Intent:** Close the largest genuine agent gap. `claims-summary` is advertised in the admin surface with no implementation of any kind.

**Preconditions:** WO-09 complete.

**Touch:** application layer, `src/InsuranceIntelligence.Domain/Agents/AgentRegistry.cs` if the descriptor needs tool metadata.

**Required behaviour:**
1. Read §6.2 of the architecture doc. Allowed tools: `claim.read`, `document.read`, `conversation.read`, `policy.read`, `knowledge.search`, `audit.read` — all `read` tier, all delivered by WO-08.
2. Output: claim summary with sources, open questions, and confidence. Human gate: required before decision use.
3. Every factual statement traces to a retrieved source (I6). No summarising from model memory.
4. Gated by WO-06. Traced by WO-07. Surfaced where a claims officer actually works — the claim review surface — not a new isolated page.

**Forbidden:**
- Any statement not traceable to a retrieved source.
- Presenting the summary as a decision, recommendation to approve/reject, or determination (I4).
- Implementing `report-insight` here. It remains unimplemented and must be labelled as such in WO-11.

**Verify:** `dotnet test`. Tests: summary cites real sources; ungrounded content is absent; tenant isolation holds; the agent is gateable; a run record with tool trace is written.

**Done when:** the agent produces grounded, cited, human-gated summaries in the claim review surface.

---

### WO-11 — Honest agent status surface

**Intent:** The admin agent surface tells the truth about what each agent actually is.

**Preconditions:** WO-10 complete.

**Touch:** `src/InsuranceIntelligence.Domain/Agents/AgentRegistry.cs`, `web/portal/lovable-pages/admin/Agents.tsx`, `docs/03-design-architecture/iip_ai_agent_architecture_tool_registry_updated.md`.

**Required behaviour:**
1. The catalog lists ten agents as though all are operating AI agents. After WO-10, reality is roughly: several genuinely model-backed; several deterministic rule engines (`routing` → `ClaimRoutingPolicy`, `risk-fraud-signal` → `FraudRuleEngine`, `underwriting-intelligence` → `UnderwritingRuleEngine`); at least one (`report-insight`) unimplemented. **Determine the true current state from the code — do not copy this list.**
2. Extend `AgentDescriptor` with an implementation kind (model-backed / rule-based / not implemented) and the tools it may call.
3. The admin surface renders that truth. A rule-based engine is labelled rule-based. An unimplemented agent is labelled unimplemented and cannot be enabled.
4. Update the architecture doc: add a current-implementation-status table alongside the target-state registry, so the doc no longer implies everything is built.

**Forbidden:**
- Presenting a deterministic rule engine as an AI agent.
- Leaving an unimplemented agent enableable.
- Deleting the target-state registry from the doc. Target state and current state coexist, clearly distinguished.

**Verify:** `dotnet test`, `pnpm test`. A test asserts the descriptor kind matches the actual implementation for every catalog entry. Doc table equals code (D11).

**Done when:** the admin surface and the architecture doc both state the truth (I3).

---

## ▣ CHECKPOINT C — Agentic

**Gate:** the agentic layer exists, is governed, and is honestly represented.

| Check | Pass condition |
|---|---|
| C-1 | `dotnet build` + `dotnet test` — clean, output pasted |
| C-2 | Every `read`-tier tool implemented, permission-checked, tenant-isolated, tested |
| C-3 | No tool declared without an implementation |
| C-4 | Command Center runs through the registry with no regression |
| C-5 | Every agent execution has a populated tool trace |
| C-6 | Agent status surface matches code for all ten entries |
| C-7 | Architecture doc current-status table matches code |
| C-8 | Tenant isolation test passes for every tool |

**If any check fails, stop.**

---

### WO-12 — User management

**Intent:** Provision and manage users in-product. Three endpoints exist today with no UI at all.

**Preconditions:** CP-B passed. Independent of the WO-08→11 chain.

**Touch:** new `web/portal/app/app/admin/users/`, `web/portal/lovable-pages/admin/`, new data module under `web/portal/lib/lovable-parity/data/`, `web/portal/lovable-pages/Admin.tsx`.

**Required behaviour:**
1. `GET /api/v1/admin/users`, `POST /api/v1/admin/users`, `PUT /api/v1/admin/users/{userId}/role` already exist and are permission-gated. Confirm their contracts, then build the UI: list users, invite/create, assign role, deactivate.
2. Roles come from `RolePermissionMap` via the existing roles endpoint — never a hardcoded client list (A1).
3. Convert the static "Users & roles" tile in `Admin.tsx` into a real link. Handle the other six static tiles per their WO-03 disposition.
4. Gate the surface on the permission its endpoints require (WO-05 mechanism).
5. Audit every user-management mutation (I7).

**Forbidden:**
- A client-side role list.
- Any surface implying local role assignment overrides server authority.
- Exposing identity-provider internals or secrets.

**Verify:** `pnpm test`, `pnpm build`, `dotnet test`. Create a second user with a different role, sign in as them, confirm their permissions differ and the UI reflects it. *(This directly answers "there is no place to test different accounts.")*

**Done when:** a second account can be provisioned, assigned, and signed in with correctly differing authority — entirely in-product.

---

### WO-13 — Profile, settings, workspace, sign-out

**Intent:** A signed-in user can see who they are, change what is theirs to change, switch workspace, and sign out.

**Preconditions:** WO-12 complete.

**Touch:** `web/portal/components/iip/TopBar.tsx`, `web/portal/app/app/settings/page.tsx`, `src/InsuranceIntelligence.Api/Auth/RuntimeContextEndpoints.cs`.

**Required behaviour:**
1. The avatar is currently `<div role="img">` — not interactive. Make it a real menu: display name, role, workspace, link to settings, sign out.
2. `GET /api/v1/me` already returns `availableWorkspaces`. Wire a real workspace switcher; switching re-resolves server-side scope. Never client-side scope substitution (I5).
3. Sign out through the existing MSAL path in `web/portal/lib/entraAuth.ts`.
4. `app/app/settings/page.tsx` is a four-line redirect to `/app/admin`. Replace it with a real settings screen showing only what the user can actually change.
5. If self-service profile update is in scope, add the endpoint — no `PATCH /api/v1/me` currently exists. If it is not in scope, render profile fields read-only with a stated reason. Do not render an editable field that cannot save (A4).

**Forbidden:**
- An editable field with no persistence path.
- Client-side workspace switching that does not re-resolve server-side authority.
- A settings screen listing options that do nothing.

**Verify:** `pnpm test`, `pnpm build`, `dotnet test`. Manually: open the menu, switch workspace and confirm scoped data changes, sign out and confirm the session ends.

**Done when:** all four capabilities work, and nothing in the settings screen is inert.

---

### WO-14 — In-app notifications

**Intent:** Build the notification capability the bell has been advertising. There is currently no in-app notification domain — only outbound Teams notifications.

**Preconditions:** WO-13 complete.

**Touch:** new `src/InsuranceIntelligence.Domain/Notifications/` types, repository, endpoint, migration, `web/portal/components/iip/TopBar.tsx`.

**Required behaviour:**
1. `Domain/Notifications/` currently contains only `TeamsNotification` and its queue — those are **outbound to Microsoft Teams**, not user-facing in-app notifications. Do not repurpose them.
2. Model a user-facing notification: recipient, tenant/workspace scope, type, subject reference, created-at, read state.
3. Generate notifications from real events that already exist — approval assigned, claim escalated, SLA breach, document extraction needing review, review request raised. Pick from real event sources only; never synthesise.
4. Add a tenant- and user-scoped feed endpoint with read/unread transitions.
5. Wire the bell: real unread count from real state, opening the panel lists real notifications, each links to its subject. Reinstate the unread indicator removed in WO-02 — now bound to real state.
6. Follow the repository's existing persistence convention. Verify any migration against a database at head.

**Forbidden:**
- Any hardcoded count, dot, or sample notification (A5).
- Cross-tenant or cross-user leakage — scope server-side (I5).
- Notifications carrying restricted content to a user who cannot view the subject.

**Verify:** `dotnet test`, `pnpm test`. Tests: notification created from a real event; feed is user- and tenant-scoped; read state transitions; another user does not see it. Manually: trigger a real approval assignment, confirm the bell count increments, open it, follow the link.

**Done when:** the bell reflects real state end to end.

---

## ▣ CHECKPOINT D — Platform

**Gate:** identity, profile, and notification surfaces are complete and honest.

| Check | Pass condition |
|---|---|
| D-1 | `dotnet build` + `dotnet test` — clean, output pasted |
| D-2 | `pnpm lint && pnpm test && pnpm build` — clean, output pasted |
| D-3 | A second account is provisionable and sign-in-able in-product |
| D-4 | Differing roles demonstrably yield differing authority |
| D-5 | Profile menu, workspace switch, and sign-out all work |
| D-6 | Settings screen contains nothing inert |
| D-7 | Bell reflects real unread state; no hardcoded indicator |
| D-8 | Notification scoping tests pass, including cross-user isolation |

---

### WO-15 — Product-line-driven FNOL

**Intent:** Intake serves the insurer's actual book, not only motor.

**Preconditions:** CP-B passed. Independent of WO-16 through WO-19.

**Touch:** `web/portal/lovable-pages/FNOL.tsx`, `src/InsuranceIntelligence.Domain/Cases/` (`MotorFnolDetails.cs`, `CreateCaseCommand.cs`), `src/InsuranceIntelligence.Domain/Documents/DocumentChecklistRules.cs`.

**Required behaviour:**
1. `FNOL.tsx` hardcodes `productLine: "Motor"` and `claimType: "Motor Accident"`, enforces motor fields as required for every claim, and shows a hardcoded seven-item motor document list. The backend is **not** motor-limited: `CreateCaseCommand.ProductLine` is a free string and `DocumentChecklistRules` already branches by product line and claim-type kind.
2. Introduce a product-line FNOL schema abstraction. `MotorFnolDetails` is currently the only structured detail record — generalise without breaking it. Existing motor claims must continue to read correctly.
3. The form renders fields and required-field validation from the selected product line's schema.
4. The required-documents panel derives from `DocumentChecklistRules`, not a client constant (A1).
5. Add at least one non-motor line end to end — Medical is the natural choice, since `DocumentChecklistRules.ForProductLine` already has a Medical rule set. Confirm the current rule sets before choosing.

**Forbidden:**
- Breaking existing motor claims or their stored details.
- A client-side document checklist.
- Adding a product line to the form without its checklist rules and detail schema (A7).

**Verify:** `dotnet test`, `pnpm test`, `pnpm build`. Create a motor claim and a non-motor claim; confirm each persists correct structured details and gets the correct checklist. Confirm pre-existing seeded motor claims still render.

**Done when:** at least two product lines work end to end and motor is unregressed.

---

### WO-16 — Complete the communication send loop

**Intent:** An approved customer update can actually be sent. Today the loop cannot be completed in the portal.

**Preconditions:** CP-B passed.

**Touch:** `web/portal/lovable-pages/Communications.tsx`, `web/portal/lib/lovable-parity/data/communications.ts`, `src/InsuranceIntelligence.Api/Communications/CommunicationEndpoints.cs`.

**Required behaviour:**
1. The drafting → approve → reject → send chain exists server-side and is correctly human-gated. `POST /api/v1/communications/send-email` and `/send-sms` exist. The Communications data module is **read-only** and never calls them.
2. Wire send. Only an approved draft is sendable — the human gate is enforced server-side and must not be bypassed (I4).
3. Send failures surface honestly (I9). A queued send is shown as queued, never as delivered.
4. Unconfigured communication provider renders the WO-04 not-configured state.
5. Audit every send (I7).

**Forbidden:**
- Sending without recorded human approval.
- Reporting success before the provider confirms acceptance (A5).
- Any client-side path that reaches a send endpoint bypassing approval state.

**Verify:** `dotnet test`, `pnpm test`. Tests: unapproved draft cannot send; approved draft sends; failure renders honestly; audit written. Manually complete draft → approve → send.

**Done when:** the full loop completes in-product with the human gate intact.

---

### WO-17 — Knowledge management UI

**Intent:** Manage the knowledge base that grounds AI answers. Four endpoints exist; the UI is list-only.

**Preconditions:** CP-B passed.

**Touch:** `web/portal/lovable-pages/Knowledge.tsx`, `web/portal/lib/lovable-parity/data/knowledge.ts`.

**Required behaviour:**
1. `POST /api/v1/knowledge-documents`, `/approve`, `/archive`, `/reindex` all exist and are permission-gated. Wire upload, approve, archive, and reindex.
2. The current page carries a banner stating controls remain hidden until the API captures an explicit sensitivity label and governance reason. **Verify whether that is still true.** If the API captures them, surface the fields and remove the banner. If not, this work order includes adding them — unknown sensitivity must stay blocked from retrieval by default.
3. Gate on permission (WO-05 mechanism); `ManageKnowledgeBase` is held by `Compliance` and `KnowledgeManager` — confirm against current code.
4. Approval state is real: an unapproved document is not retrievable by any agent (I6).

**Forbidden:**
- Making a document retrievable before approval.
- Defaulting unknown sensitivity to retrievable.
- Removing the banner without implementing what it describes (I3).

**Verify:** `dotnet test`, `pnpm test`. Tests: unapproved documents are not retrieved; approval makes them retrievable; sensitivity is enforced. Manually upload → approve → confirm the Command Center can now cite it.

**Done when:** the knowledge lifecycle is manageable in-product and grounding respects approval and sensitivity.

---

### WO-18 — Document upload in the Documents module

**Intent:** Upload is discoverable where users look for it, not only inside an individual claim.

**Preconditions:** CP-B passed. Best done after WO-04 and WO-05.

**Touch:** `web/portal/lovable-pages/Documents.tsx`, `web/portal/components/iip/DocumentUploadWidget.tsx`.

**Required behaviour:**
1. `DocumentUploadWidget` is fully working and used only in `ClaimReview.tsx`. `GET /api/v1/documents` is read-only.
2. Add upload to the Documents module. Documents attach to a claim — the flow must therefore establish a claim association. Either require selecting a claim, or state plainly that upload happens within a claim and link there. **Do not invent an unattached-document concept** that the domain does not support (A1).
3. Reuse the existing widget. Do not build a second uploader (A2).
4. Permission-gated (WO-05) and honest when storage is unconfigured (WO-04).

**Forbidden:**
- A second upload implementation.
- Inventing an unattached-document model without domain support.

**Verify:** `pnpm test`, `pnpm build`, `dotnet test`. Manually upload from the Documents module and confirm it appears on the associated claim with extraction run.

**Done when:** upload is discoverable and reaches the same governed path.

---

### WO-19 — Fraud signal disposition workflow

**Intent:** A raised signal can be worked. Today signals are a list with no actions.

**Preconditions:** CP-B passed.

**Touch:** `src/InsuranceIntelligence.Domain/Fraud/FraudRuleEngine.cs` and new disposition types, new endpoints, `web/portal/lovable-pages/FraudIntelligence.tsx`.

**Required behaviour:**
1. `FraudRuleEngine` produces real advisory signals (duplicate contact, claim velocity, duplicate policy, low completeness) with correct non-accusatory severity wording. `GET /api/v1/fraud/signals` is the only route.
2. Add disposition: triage, assign for investigation, record outcome, close — with human accountability on every transition.
3. **Preserve the non-accusatory language contract absolutely.** Permitted: *fraud signal detected, requires investigation, potential anomaly, advisory signal*. Never *fraud confirmed*. AI never makes an adverse fraud determination (I4).
4. Permission-gated. Fully audited (I7).
5. Fraud signals are restricted content — never exposed on policyholder-facing surfaces.

**Forbidden:**
- Any language or state implying confirmed fraud.
- Automated adverse determination.
- Exposing signals to unauthorised roles or customer-facing surfaces.

**Verify:** `dotnet test`, `pnpm test`. Tests: transitions require permission and record the actor; language contract enforced; signals absent from client-portal responses. Manually triage a signal to closure.

**Done when:** signals are workable to closure with human accountability and the language contract intact.

---

## ▣ CHECKPOINT E — Operational

**Gate:** the claims operation is completable in-product.

| Check | Pass condition |
|---|---|
| E-1 | `dotnet build` + `dotnet test` — clean, output pasted |
| E-2 | `pnpm lint && pnpm test && pnpm build` — clean, output pasted |
| E-3 | At least two product lines intake end to end; motor unregressed |
| E-4 | Draft → approve → send completes with the human gate intact |
| E-5 | Knowledge lifecycle manageable; grounding respects approval and sensitivity |
| E-6 | Upload discoverable and governed |
| E-7 | Fraud signals workable to closure; language contract enforced |
| E-8 | Full walkthrough: intake → triage → documents → AI assist → approval → communication → audit, with no dead ends |

---

### WO-20 — Design token consolidation

**Intent:** One token vocabulary.

**Preconditions:** CP-E passed.

**Touch:** `web/portal/tailwind.config.ts`, `web/portal/app/globals.css`, all components.

**Required behaviour:**
1. Two vocabularies coexist, both wired: literal (`navy`, `stone`, `cloud`, `slate2` — ~996 uses) and semantic (`ink`, `muted`, `line`, `danger` — ~195 uses). Confirm current counts before starting.
2. Consolidate on the semantic set, which matches the design system documentation and can express theme state. Literal colour names cannot — this is why `TopBar` reaches for `bg-error` while the CSS layer defines `--color-danger`.
3. Migrate mechanically. **This work order changes no visual output.** Any visual change is a defect.
4. Update the design system doc if the token set changes.

**Forbidden:**
- Altering the visual design under cover of a token migration.
- Leaving both vocabularies live.
- Mixing this with any behavioural change.

**Verify:** `pnpm lint && pnpm test && pnpm build`. Run the visual parity gate if `LovableVisualParityGate` is active. Compare screenshots of every route before and after — they must be identical.

**Done when:** one vocabulary remains and rendering is byte-identical.

---

### WO-21 — Documentation reconciliation

**Intent:** Documentation describes the system that exists. `AGENTS.md` requires it: *"When sources conflict, identify the conflict, follow the latest approved authority, and update the stale source."*

**Preconditions:** CP-E passed.

**Touch:** `docs/03-design-architecture/iip_ai_agent_architecture_tool_registry_updated.md`, `../CLAUDE.md` (parent directory), `IIP_IMPLEMENTATION_GUARDRAILS.md`.

**Required behaviour:**
1. **Remove phased framing from architecture documents.** §15 of the agent architecture doc is titled "Phase 1-4 agent alignment" and sequences agent delivery across phases. Replace with target-state capability plus the current-status table from WO-11. Audit the other architecture documents for the same pattern. The `iip_release_1_*` documents are legitimately release-scoped — leave them.
2. **Resolve the edition conflict.** The parent `Projects/CLAUDE.md` instructs agents to read `BUILD_BRIEF.md` and `IIP_SaaS_Fabric_Edition_Implementation_Guide.md` and describes this repository as the SaaS Fabric Edition. **Neither file exists here**, and `AGENTS.md` describes the Managed Application edition. Any agent following those instructions fails to find its stated authority. Determine which is correct and fix the stale source.
3. Reconcile the tool registry doc with implemented tools: target state and current state both present, clearly distinguished.
4. Clear the one phased reference in `IIP_IMPLEMENTATION_GUARDRAILS.md`.

**Forbidden:**
- Deleting target-state specification to make docs match a smaller implementation. Both coexist, distinguished.
- Claiming implementation status not proven by code (I3).

**Verify:** every agent and tool status in the docs matches code. Ledger records each conflict found and how it was resolved.

**Done when:** no architecture document contradicts the code or carries phased delivery framing.

---

### WO-22 — Repository and deployment hygiene

**Intent:** The repository and its pipelines are trustworthy.

**Preconditions:** CP-E passed.

**Touch:** `.gitignore`, `.github/workflows/deploy-staging.yml`.

**Required behaviour:**

1. **Fix the `.gitignore` typo.** Line 36 reads `"**/artifacts/"` — **with literal quote characters**. Git treats the quotes as part of the filename, so the pattern matches a directory literally named `"artifacts"` and therefore matches nothing. Confirm with:
   ```bash
   git check-ignore -v src/InsuranceIntelligence.Api/artifacts/test-bin/Debug/net8.0/Azure.AI.DocumentIntelligence.dll
   ```
   It currently reports no match. Remove the quotes so the rule works. Verify the same command then reports a match.

2. **Untrack the committed build output — do not rewrite history.** `main` carries ~200 tracked files (~86 MB in the working tree, `.git` is 42 MB total) under `*/artifacts/test-bin/`, introduced by commit `ba8bef6`. These are `dotnet` build outputs — 174 `.dll`, 14 `.pdb`, plus a few support files. Nothing references them as source; `dotnet build` regenerates them.

   Use the non-destructive path:
   ```bash
   git rm -r --cached --quiet -- "*/artifacts/test-bin/*"
   ```
   Then commit normally and push normally. **No force-push. No history rewrite.**

   Teammate impact is effectively nil: collaborators pull as usual, the files leave their working tree, and the next build regenerates them. Commit SHAs are unchanged, so all remote branches remain valid and no one re-clones.

3. `deploy-staging.yml` targets App Service names that no longer exist in any subscription. Either update to live resource names or disable the workflow. A pipeline that cannot succeed is worse than none.

4. Confirm CI still covers backend build/test/vulnerability scan, portal lint/test/build, and bicep compilation.

**Forbidden:**
- **Rewriting history** (`filter-repo`, `filter-branch`, BFG) or force-pushing. There are 8 remote branches; a rewrite invalidates every one and forces every collaborator to re-clone. It would reclaim roughly 35 MB from an already-small 42 MB repository — not a trade worth making. If someone later wants the history purged, that is a separate, explicitly-authorised task with the team offline.
- Deleting the files from disk rather than from the index. `--cached` untracks; it must not destroy a collaborator's local build output.
- Deploying, pushing to cloud, or changing cloud resources — this work order edits repository configuration only. Deployment requires explicit instruction (`AGENTS.md`).

**Verify:**
```bash
git check-ignore -v src/InsuranceIntelligence.Api/artifacts/test-bin/Debug/net8.0/Azure.AI.DocumentIntelligence.dll  # now matches
git ls-files | grep -c "artifacts/test-bin"                                                                          # now 0
dotnet build InsuranceIntelligence.sln && dotnet test InsuranceIntelligence.sln                                       # regenerates cleanly
git status                                                                                                            # clean; regenerated output stays ignored
```
CI green on the resulting commit.

**Done when:** the ignore rule works, no build output is tracked, a clean build regenerates it without reintroducing it to the index, and history is untouched.

---

## ▣ CHECKPOINT F — Release candidate

**Final gate.** All of §3 Definition of Done must hold, plus:

| Check | Pass condition |
|---|---|
| F-1 | All of D1–D12 pass with pasted evidence |
| F-2 | Every checkpoint A–E re-verified against final code |
| F-3 | Full walkthrough as each role: no dead control, no invented data, no unhandled failure |
| F-4 | Tenant isolation verified across every new endpoint and tool |
| F-5 | No secret in code, logs, tests, or history |
| F-6 | Every AI surface: grounded, cited, gated, audited, human-authoritative |
| F-7 | Scope statement (§7) is accurate — nothing claimed that is not built |
| F-8 | Ledger complete; every blocker resolved or explicitly accepted |

---

## 5. Standing verification commands

From `AGENTS.md`. Run the relevant set at every work order; the full set at every checkpoint.

```bash
# backend
dotnet restore InsuranceIntelligence.sln
dotnet build InsuranceIntelligence.sln --no-restore -c Release
dotnet test InsuranceIntelligence.sln --no-build -c Release --verbosity normal
dotnet list InsuranceIntelligence.sln package --vulnerable --include-transitive

# portal (from web/portal)
pnpm install --frozen-lockfile
pnpm lint
pnpm test
pnpm build

# infrastructure
az bicep build --file infra/bicep/phase3-dev-foundation.bicep
az bicep build --file infra/bicep/phase6-functions-runtime.bicep
```

**Local run with no Azure credentials** — use for manual verification without cloud access:
```bash
scripts/local/run-offline.sh
```
Offline mode uses in-memory storage and the synthetic provider path, restricted to local runtimes. If a work order needs the message queue without Azure, set `INSURANCEINTELLIGENCE_QUEUE_PROVIDER=Mock`.

**Never** run a deployment script as part of this contract. Deployment requires explicit human instruction.

---

## 6. Session handoff

This build spans many sessions. Context will be lost. Design for that.

**Starting a session:**
1. Read this document §1, §2, §3.
2. Read the ledger (§8). The first work order with no `DONE` entry is your position.
3. Read the blockers (§9). One may now be unblocked.
4. Verify the last completed work order still passes its `Verify` before building on it.

**Ending a session:**
1. Ledger updated with real command output.
2. Work committed.
3. Any new blocker recorded in §9.
4. Never leave a work order half-implemented without a ledger note stating exactly what remains.

**Never trust a summary of prior work over the code.** If the ledger says something is done and the code disagrees, the code wins — record the discrepancy.

---

## 7. Scope boundary

**In scope:** everything in §4.

**Explicitly out of scope.** Do not start these. If a work order appears to require one, stop and record a blocker (§9).

| Out of scope | Why |
|---|---|
| Underwriting submission → decision → bind lifecycle | Only `GET /underwriting/reviews` exists. No decision endpoint. This is a product build, not a remediation. |
| Policy issuance, endorsement, renewal, cancellation | No policy lifecycle exists. `IPolicyRepository.Upsert` has no HTTP caller. |
| Settlement, reserves, payment instruction | No reserve or payment model exists. `Settled` is a status with nothing behind it. |
| Core insurance system write-back adapters | Requires per-insurer integration design and separate approval. |
| `write` / `external_send` / `approval_required` agent tool tiers | Require human-approval mediation design beyond this contract. |
| Model-based intent routing | The deterministic classifier is sufficient and safer. |

**Accurate scope statement on completion** — use this, and nothing stronger:

> IIP is a claims operations platform with grounded AI assistance and human-accountable approvals. It covers first notice of loss, triage and assignment, document intake and extraction, completeness checking, claims and policy Q&A with cited evidence, customer-update drafting under human approval, approval workflow, fraud signal triage, and audit. Underwriting, policy administration, and settlement are advisory or out of scope.

---

## 8. Ledger

Append one row per work order on completion. Real output only (A9).

| WO | Status | Commit | Verification output | Notes |
|---|---|---|---|---|
| WO-01 | DONE | (pending) | `pnpm lint` → No ESLint warnings or errors. `pnpm build` (NEXT_PUBLIC_API_BASE_URL=http://127.0.0.1:5167 NEXT_PUBLIC_AUTH_MODE=Development NEXT_PUBLIC_RUNTIME_PROFILE=Development) → 35/35 static pages, `/app/policy` 1.78 kB, no errors. | Audited every module-level literal in `lovable-pages/`+`components/iip/`. One real violation found and fixed: `PolicyIntelligence.tsx` — fabricated AI answer, fake excess figure, 4 fake clause citations, fake 0.88 confidence badge, fake regulatory reference, 2 dead buttons. Rewired to the real `POST /command-center/query` path via `useCommandCenterQuery` + `CommandCenterBlocks` (same path `AIConversations.tsx` uses) — real grounding, real citations, honest refusal, no dead buttons. All other literal-data hits classified KEEP as legitimate UI configuration (icon/tone/label maps keyed to real enums, filter/nav lists, empty form defaults, workflow-builder blank-canvas template, integration capability descriptions) or explicitly deferred: `FNOL.tsx` hardcoded 7-item document list is real content not derived from `DocumentChecklistRules` — deferred to WO-15 by design, noted here rather than silently left. `OperationalIntelligencePrototype.tsx` / `Release1LowFidelityPrototype.tsx` keep hardcoded/synthetic content — both self-disclose ("Development-only prototype · synthetic data · no production workflow or decision behavior"), live under an isolated `/prototype/` route tree, and are not linked from any real app navigation. |
| WO-02 | DONE | (pending) | `pnpm lint` → No ESLint warnings or errors. | Audited every badge/dot/count/pill render across `components/iip/` and `lovable-pages/`. Two unconditional indicators found with no backing state, both removed: (1) `TopBar.tsx` — the notification bell's red "unread" dot rendered unconditionally regardless of any actual unread state; left the bell icon itself, comment marks it not-to-be-reinstated without real state, reinstated for real in WO-14. (2) `Sidebar.tsx` — a permanently-lit green "online" dot on the IIP logo with no health/connectivity check behind it; removed outright, no work order currently reinstates it since no system-health indicator is in scope. Every other badge/pill/status render checked and confirmed derived from real props/state (e.g. `c.unread > 0`, `selected.extractionApproved`, `d.retrievalEligible`, `StatusBar.tsx`'s `useProviderHealth()`) — kept unchanged. |
| WO-03 | DONE | (pending) | `pnpm lint` clean. `pnpm test` — runtime-data hygiene, provider-status, role-routes, ui-release-gates checks all passed; trust-truth Playwright 2/2 passed; authority-permissions Playwright 4/4 passed. | Full inventory: (1) `TopBar.tsx` Help button — no handler, no help-content system anywhere, `/app/support` is itself an orphaned redirect stub to `/app/integrations` — **removed**, no work order covers help content. (2) `TopBar.tsx` notification bell — **deferred to WO-14** (named), left as a plain icon with no false affordance (WO-02 already stripped its fake dot). (3) `Channels.tsx` "Connect new channel" — no handler, no backend endpoint, no work order — **removed** rather than invent channel-provisioning scope (A10). (4) `FraudIntelligence.tsx` "Open investigation" — maps directly to **WO-19** (named) — disabled with visible `title` reason instead of left clickable-looking with no handler. (5) `TopBar.tsx` profile avatar — checked for false affordance: it is a plain `<div role="img">` with no hover/cursor-pointer styling, so it does not currently claim interactivity — **deferred to WO-13** (named), no code change needed now. (6) `Admin.tsx` seven "Tenant profile" tiles — checked for false affordance: none are `Link`/`button` wrapped, no hover/cursor-pointer styling, so they read as informational cards, not dead controls — the "Users & roles" tile is the one that will become a real link, **deferred to WO-12** (named); the other six have no work order and stay as plain descriptive cards. |
| CP-A | **PASSED** | 2f3de07..c63389f | Full clean re-run, everything together (not incremental): `dotnet build` 0 warnings/0 errors. `dotnet test` 502+195 = 697/697 passed. `pnpm lint` clean. `pnpm test` 6/6 checks+specs clean. `pnpm build` 35/35 static pages. `git status` clean tree. | A-1 through A-7 verified directly by the CP-A commands above plus the individual WO-01..05 audits they build on. A-8 (manual walk of every nav route) was performed as a systematic code-level review across all 36 `lovable-pages` files during WO-01/02/03 (data-source and interactive-control audits) rather than a live browser click-through — no browser tool is available in this environment. Recommend a human do one real click-through pass before this checkpoint is treated as customer-facing-ready, since a code review cannot catch every rendering/runtime-only issue a live session would. **The system is now truthful: no fabricated business content, no dead controls, no hardcoded state indicators, no raw provider errors, and no action rendered without its real required permission (on the audited page; parity-checked platform-wide for the permission table itself).** Proceeding to CP-B (governed AI execution). |
| WO-04 | DONE | (pending) | `dotnet build` clean, 0 warnings. `dotnet test` → 502/502 passed (was 501/502 before fixing one pre-existing test that asserted the old untyped exception — see notes). `pnpm lint` clean. `pnpm build` clean, 35/35 static pages. | Enumerated all 12 `NotConfigured*`/`NotConfiguredEvidencePackStorageProvider`/`NotConfiguredUnderwritingSubmissionSource` port implementations. 4 already returned honest structured empty/refusal values (knowledge search, retrieval, AI assistant answer, underwriting submissions) — left unchanged. 9 throw sites across 5 classes threw a plain untyped `InvalidOperationException`, invisible to any typed handler — converted all to the typed `ProviderNotConfiguredException`, which now carries a stable machine-readable `Capability` (e.g. `"DocumentStorage"`) alongside the message. Added `ProviderNotConfiguredExceptionHandler : IExceptionHandler`, registered before `AddProblemDetails()`'s fallback, returning a structured RFC7807 body (503, `capability` extension field) with no provider class name or stack trace. Wired the live case from the analysis: `DocumentUploadWidget.tsx` now detects the 503+capability shape and renders an honest "Document storage is not configured — ask a workspace administrator" message, distinct from a generic upload error. New test file `ProviderNotConfiguredHandlingTests.cs`: unit test on the handler proving the response shape and that internals never leak; theory test across all 9 typed throw sites proving each carries its correct capability; test proving the handler does not swallow unrelated exception types. One pre-existing test (`ProtectedRuntimeReconciliationTests.NotConfiguredQueueDispatcher_FailsClosedInsteadOfSimulatingSuccess`) asserted the exact type `InvalidOperationException` — xUnit's `ThrowsAsync<T>` requires an exact match, and `ProviderNotConfiguredException` is a subclass, so the type change broke it. Per A8: fixed the test rather than the code — the test's actual intent (fail closed, never simulate success) is fully preserved and the assertion is now strictly stronger (also checks `Capability == "MessageQueue"`); the sibling test in the same file already asserted the typed exception, confirming this was the stale one. |
| WO-05 | DONE | (pending) | `pnpm lint` clean. `pnpm test` — all 6 checks/specs pass including new `check:action-permission-parity` (9 roles, 29 permissions in sync). `pnpm build` clean, 35/35 pages. `dotnet build`/`dotnet test` unaffected (frontend-only work order; no backend changes). | Established one consistent gating mechanism: `hasPermission(permissions, permission)` (new, `lib/roles.ts`) for real `runtimeContext.permissions`, mirroring the existing `canPreviewPermission(role, permission)` for local-test/parity mode — same `usePreviewRole` branching pattern already used for route-level gating (`Admin.tsx`, `Integrations.tsx`). Discovered en route that the codebase already has a *different*, more granular mechanism (`ActionStateContract`/`allowedActions`/`blockedActions`, backend-computed per record) used by `CustomerUpdatePanel` and document responses — but confirmed by reading `CaseActions.cs` that this contract reflects **workflow state only**, never the caller's permission (`Permission` is carried as display metadata, not checked against the caller), so it does not solve WO-05's problem and the client-side permission gate is still required on top of it. Fixed the flagship case plus every other action-permission mismatch found on the same page (`ClaimReview.tsx`, the richest single page in the app): (1) `UploadDocument` — the reported bug — ITAdmin/Compliance saw the upload button, would 403 server-side. (2) `ManageCustomerCommunications` — "Draft customer update" and "Draft document follow-up" buttons, same mismatch for Compliance. (3) `ExportEvidencePack` — "Export claim pack" button, mismatch for ClaimsOfficer/CustomerService/Broker. (4) `CustomerUpdatePanel`'s Approve/Send buttons — same root cause (workflow-state-only `allowedActions`), fixed at the call site by passing `onApprove`/`onSend` as `undefined` when the permission is absent (the component already treats them as optional), no component change needed; Send is additionally gated on `SendCustomerUpdate`, a narrower permission than Approve's `ManageCustomerCommunications`. Each hidden control renders a plain, honest one-line reason instead of nothing (I2). New regression check `scripts/check-action-permission-parity.mjs`, wired into `pnpm test`: parses both `PREVIEW_PERMISSIONS` (lib/roles.ts) and the backend `RolePermissionMap.cs` + `Permission.cs` enum and fails the build the moment they diverge in either direction, for every role and permission — this is the check that would have caught the original bug before it shipped, and catches the class of bug going forward, not just the one instance fixed here. **Scope note, stated honestly per A9**: this pass covers `ClaimReview.tsx` exhaustively (chosen because it holds the reported defect and is the highest action-density page) plus the parity script that guards the whole permission table going forward; it does not claim to have re-audited all ~28 other action-bearing pages/components button-by-button — that would be a substantially larger pass. The parity script catches the *data* half of future drift (frontend/backend permission tables disagreeing) but not a future page that simply forgets to call `hasPermission` at all. |
| WO-06 | DONE | (pending) | `dotnet build` 0/0 warnings/errors. `dotnet test` 507+196 = 703/703 passed (was 697 before this WO's 6 new tests). Verified the enforcement is real, not cosmetic: temporarily neutered the `report-insight` gate check (`if (false && !reportGate.Enabled)`), confirmed `DisabledReportInsightAgent_RefusesBeforeCallingTheModel` failed with the model-call spy's exception visible in the stack trace, then reverted — real regression caught, not a test that always passes. | Added `IAgentGate`/`AgentGate` (`Application/Agents/AgentGate.cs`) — the single enforcement seam, deliberately separate from `IAgentRegistryService` (which requires `Permission.ManageAgents` and is wrong for a runtime check every authorized caller needs). Reuses `IAgentStateRepository` and the same "absence means enabled" convention as `AgentRegistryMerge`, so admin listing and runtime enforcement can never silently disagree. Gated all 5 real call sites found by inventory (3 in `CommandCenterQueryService` + 2 dedicated endpoint entry points that reach the same agent keys via a different path than Command Center): `BuildPolicyAnswerResponseAsync`→`policy-intelligence`, `BuildCustomerUpdateDraftResponseAsync`→`customer-update-drafting`, `BuildManagementSummaryResponseAsync`→`report-insight` (this path's existence corrects the earlier analysis's claim that `report-insight` was entirely unimplemented — WO-11 must fix that), the deterministic default path→`command-center`, plus `AssistantEndpoints.AskPolicyQuestion`→`policy-intelligence` and `CustomerUpdateEndpoints.DraftCustomerUpdate`→`customer-update-drafting`. Command Center refusals reuse the existing `BuildErrorResponseAsync`/`ErrorBlock` machinery (code `agent_disabled`) rather than adding a new response shape — the frontend already renders it distinguishably with no changes needed. The two endpoint paths construct an honest refusal in the same shape their provider already returns for "not configured" (mirroring `NotConfiguredAiAssistantProvider`'s pattern for the assistant path; a parallel `AgentDisabled`-status JSON for the customer-update path, with an explicit audit write since that path short-circuits before its normal audit code runs). Every refusal identifies who disabled the agent and when (`AgentGateResult.RefusalMessage`), sourced from the same `UpdatedByUserId`/`UpdatedAt`/`Reason` the admin UI already writes. Added `LocalTestAgentGate` to `InsuranceIntelligence.LocalTestDoubles` (defaults to enabled, matching production default) and updated 15 existing test call sites across 4 files that construct these endpoints/service directly. New tests: 3 in `CommandCenterQueryEndpointsTests.cs` (command-center default-path refusal + audit; report-insight refusal with a model-call spy proving the model never runs, plus a negative-control test proving the same setup reaches the model when enabled); 1 each in `KnowledgeAssistantEndpointsTests.cs` and `WorkflowEndpointsTests.cs` for the two endpoint-level paths, same model-call-spy pattern; 1 in `AgentRegistryTests.cs` proving the full loop end-to-end — `AgentRegistryService.SetKillSwitchAsync` (the admin write) against the same repository an `AgentGate` (the runtime read) checks, disable→refuse, re-enable→allow. This is the closest available proof of "disable in Admin > Agents, confirm Command Center refuses" without a browser. Portal untouched this work order — no frontend change needed since refusals flow through response shapes the UI already renders. |
| WO-07 | DONE | (pending) | `dotnet build` 0/0. `dotnet test` 508+196 = 704/704. Migration verified via `dotnet ef migrations script AddCommandCenterConversations AddAgentRuns` (script-verified, not applied against live SQL Server — none available in this environment): exactly one `CREATE TABLE [AgentRuns]` + 3 indexes, no duplicate DDL for any of the ~28 pre-existing tables — same failure mode as the prior `AddCommandCenterConversations` incident, confirmed absent. | Added `AgentRun`/`AgentRunStatus`/`AgentRunToolTraceEntry`/`IAgentRunRepository` (`Domain/Agents/AgentRun.cs`) matching the architecture doc §11 field list (AgentRunId, Scope in place of separate TenantId/WorkspaceId per this codebase's own convention, AgentKey — the real catalog key, not a free-text display name like the pre-existing `AgentActionLog.AgentName` uses — TriggerType, InputRefs, Status, StartedAt/CompletedAt, ErrorCategory, CorrelationId, UserId, CaseId, ToolTrace). Kept deliberately separate from `AgentActionLog`: that type predates this work, captures a different shape (one row per model call with the model's own output), and rewriting it mid-contract to match the doc exactly would have touched every call site across three already-modified files for no behavioral gain — `AgentRun` is the new higher-level record the doc actually asks for. Persistence follows the established `PersistedScopedJson`/`JsonRepositoryBase` pattern exactly (`PersistedAgentRun`, `EfAgentRunRepository`, indexed on tenant+workspace+agentKey and tenant+workspace+caseId) — real EF migration `AddAgentRuns` generated and verified clean. Added `IAgentRunRecorder`/`AgentRunRecorder` as the writing seam (mirrors `IAgentGate`'s separation of concerns) and wired it into every one of WO-06's 6 gated call sites, both outcomes: `CommandCenterQueryService`'s 3 model-calling branches + its deterministic default path (local `RecordRun` closure to avoid repeating scope/correlation/user across 8 call points), plus `AssistantEndpoints.AskPolicyQuestion` and `CustomerUpdateEndpoints.DraftCustomerUpdate`. Every refusal from WO-06 now also produces an `AgentRun` with `Status=Refused` and `ErrorCategory="agent_disabled"`; every completion produces one with `Status=Completed`. Known simplification, stated honestly: the three `CommandCenterQueryService` branch methods can internally return a bounded validation error (e.g. missing claim ID) rather than a real answer, and that inner case is still recorded as `Completed` rather than a more granular status — distinguishing it would require inspecting the returned response's block type, which this pass did not do; `Refused` is reserved specifically for kill-switch refusals and remains unambiguous. `ToolTrace` is structurally present but empty on every real run — no tool registry exists yet (WO-08); populating it honestly, not fabricating entries, is WO-08's job. Tests: `LocalTestAgentRunRecorder` test double added to `InsuranceIntelligence.LocalTestDoubles`; 15 existing call sites across 4 test files updated (mechanical, same locations WO-06 touched); two existing Command Center tests extended with `AgentRun` assertions (completed path: scope/status/timestamps; refused path: status/errorCategory) rather than new tests, since the scenarios already existed; new `EfAgentRunRepository_RoundTripsAndIsolatesByTenantAndCase` in `Phase5InfrastructureTests.cs` proves the persistence layer through real EF Core machinery (not only the in-memory double), including tenant and case isolation. |
| CP-B | **PASSED** | 5ad187f..4c0130e | `dotnet build` 0/0. `dotnet test` 508+196 = 704/704. | B-1 through B-6 all verified. B-2/B-3 precision note: only 4 of the 10 catalogued agents (`command-center`, `policy-intelligence`, `customer-update-drafting`, `report-insight`) have any real model-invocation code path in this codebase today — the other 6 (`claims-summary`, `document-checker`, `risk-fraud-signal`, `routing`, `knowledge-assistant`, `underwriting-intelligence`) either have no implementation or are deterministic rule engines with nothing to gate (WO-11 documents this precisely). "Per agent key" and "no ungated model call site exists" are therefore complete relative to what actually exists — re-confirmed by grep immediately before this gate: exactly 5 call sites in all of `src/` (`chatCompletionProvider.CompleteAsync` ×3, `assistantProvider.AnswerPolicyQuestionAsync` ×1, `draftingProvider.DraftAsync` ×1), all 5 gated. B-6 verified by design: `AgentRun.InputRefs` carries only the existing truncated `SummarizeInput()` helper's output (already used for `AgentActionLog`, same safety convention), never a raw prompt; `ToolTrace` carries only tool name + outcome. **AI execution in this system is now controllable (kill switch actually stops it) and auditable (every execution reconstructable as an AgentRun).** Proceeding to CP-C (agentic layer: tool registry, run it through Command Center, close the claims-summary gap, make the admin surface honest). |
| WO-08 | DONE | (pending) | `dotnet build` 0/0. `dotnet test` 522+196 = 718/718 (up from 704, +14 new). Verified the tenant-isolation test is real, not a tautology: temporarily changed `ClaimSnapshotLoader` to query a hardcoded tenant instead of the invocation's scope, confirmed `ClaimsAttentionReadTool_IsolatesByTenant` failed with tenant A's case leaking into a tenant B query (visible in the assertion failure output), reverted, re-ran clean. | **Redesign decision, recorded per the work order's own instruction to do so when the contracts prove unworkable.** `AgentContracts.cs`'s legacy `IAgentTool`/`IAgentToolRegistry`/`IModuleAgent`/`IAssistantOrchestrator` were evaluated first. Confirmed unworkable for this integration: the file's own header comment states it is a "legacy module-agent request shape retained... for future orchestration work" whose endpoint "the deployed v2 API does not expose"; `Program.cs`'s stale note (flagged for deletion by this same work order) confirms the whole framework "was dropped" during the v2 migration and should be "re-derived to the governing docs... in a later sub-step" — not resurrected verbatim. Building 15 tools against `AgentToolContext`'s message+module+intent shape, which `CommandCenterQueryService` (the only real consumer, per WO-09) does not use, would have produced a second, disconnected implementation satisfying the letter of "implement IAgentToolRegistry" while calling nothing real — exactly what A2/A7 forbid. Deleted the stale `Program.cs` note now that its claim ("no tool registry exists") is no longer true. Left `AgentContracts.cs` itself untouched — flagged, not removed, since deleting legacy domain contracts is a separate decision outside this work order's scope. New registry lives in `Application/Agents/Tools/` (distinct namespace from the legacy `Domain.Agents` types, so nothing collides or is ambiguous): `IAgentTool`/`IAgentToolRegistry`/`AgentToolDescriptor`/`AgentToolInvocation`/`AgentToolResult` (`AgentToolContracts.cs`), shared `AgentToolBase` (permission — any-of, matching this codebase's real `PermissionEndpointFilter`/`RequireClaimsReadPermissionAsync` semantics, not a naive single-permission check that would have regressed `ViewAllClaims`-only roles — and provider-health enforcement, fail-closed, never throws past the registry boundary). Implemented 8 of the doc §9's 21 read-tier tools — every one with a concrete, current consumer: `claims.attention.read`, `claims.sla.read`, `documents.gaps.read` (share a new `ClaimSnapshotLoader` so the case+document+checklist+SLA aggregation exists in one place, not tripled across three tools), `approvals.queue.read`, `knowledge.search`, `audit.read`, `provider_health.read`, `metrics.read` (reuses `CommandCenterMetricsResponse` directly rather than a parallel DTO). The other 13 doc-listed read tools (`claim.read`, `document.read`, `document.extraction.read`, `policy.read`, `customer.profile.read`, `customer.history.read`, `conversation.read`, `risk.rules.read`, `fraud.signals.read`, `template.read`, `report.read`, `underwriting.case.read`, `command.briefing.read`) were **not** built — each belongs to an agent that either doesn't exist yet (`claims-summary`) or is a deterministic rule engine with no tool-calling path (`risk-fraud-signal`, `underwriting-intelligence`, `routing`) — building them now would be a tool with no caller, exactly what AGENTS.md's "no abstractions for fashion, require a concrete need" forbids. They become real work items when WO-10 (`claims-summary`) or a future agent build needs them. Tests: `AgentToolRegistryTests.cs` — registry-level enforcement (unknown tool, agent-not-in-allowed-list) plus per-tool coverage: 4 tools (`claims.attention.read`, `approvals.queue.read`, `knowledge.search`, `metrics.read`, one per distinct repository-dependency shape) get the full happy-path/permission-denial/tenant-isolation set; the remaining 4 get happy-path/permission-denial only, since their isolation guarantee comes from the same proven `TenantScope`-filtered repository pattern, stated honestly rather than padded with redundant tests. |
| WO-09 | DONE | (pending) | `dotnet build` 0 warnings/0 errors (6 pre-existing CS9113 unused-parameter warnings from the now-retired direct repository fields resolved by removing them, not suppressing). `dotnet test` 522+196 = 718/718 — **identical count to before this work order, zero regressions**: every existing grounding/citation/refusal/conversation/tenant-isolation/membership-spoofing/metrics/SLA-token assertion in `CommandCenterQueryEndpointsTests.cs` still passes unmodified after the entire data-fetching layer was rerouted through the tool registry. | Routed 5 of `CommandCenterQueryService`'s data-access call sites through the WO-08 registry, each verified to preserve exact existing behaviour: `LoadQueryDataAsync`'s case aggregation → `claims.attention.read` (required extending `ClaimSnapshot` with `SlaDueAt` — the one field `CommandCenterQueryPlanner.ResolveSla` needed that WO-08 hadn't carried; also changed `ResolveSla`'s signature from taking a full `ClaimCase` to taking `DateTimeOffset? slaDueAt` directly, its only real dependency, verified as the sole caller before changing it), its approvals fetch → `approvals.queue.read`, `LoadMetricsAsync` → `metrics.read` (old body deleted outright as dead code once retired, not renamed-and-kept), `EnsureRequiredProvidersAsync`'s per-provider health check → `provider_health.read`, and `BuildPolicyAnswerResponseAsync`'s retrieval call → `knowledge.search` (verified safe by grep: no `IRetrievalProvider` implementation reads `AssistantQuestion.Audience`, so the tool's fixed `"InternalUser"` value vs. the original's `"Internal"` cannot change retrieval output — only `.Chunks`/`.Citations` are consumed downstream, both preserved exactly). Deliberately **not** routed, stated honestly: `BuildManagementSummaryResponseAsync`'s own separate guidance-retrieval call (structurally similar but a distinct call site outside this pass's verified set) and the single-case lookups in `BuildCustomerUpdateDraftResponseAsync` (`caseRepository.GetAsync`/`caseDocumentRepository.ListForCaseAsync` for one case by ID — no WO-08 tool covers a single-entity lookup, only bulk listing; building one now would be scope creep). Removed 6 now-fully-unused constructor parameters (`knowledgeRepository`, `approvalRepository`, `taskRepository`, `updateRepository`, `slaCalculator`, `providerHealthCheck`) rather than leaving dead injected dependencies. Added a shared `CallToolAsync<T>` helper so every integration point records to a per-request `AgentRunToolTraceEntry` list uniformly and never throws past the registry boundary — a denied/failed tool call returns `null`, and each caller treats that as the honest empty case already established (e.g. zeroed metrics, not fabricated ones), never a crash. Updated the two test harness `CommandCenterQueryService` construction sites to build a real `AgentToolRegistry` backed by the exact same in-memory repositories the tests seed — necessary for the zero-regression result, since a fresh empty registry would have silently broken every data-dependent assertion in the file. New test: extended the existing default-briefing test with tool-trace assertions (`claims.attention.read`/`approvals.queue.read`/`metrics.read` all present, all `"ok"`) rather than adding a duplicate scenario. |
| WO-10 | DONE | (pending) | `dotnet build` 0/0. `dotnet test` 528+196 = 724/724 (up from 718, +6 new claims-summary tests, zero regressions). `pnpm lint` clean, `pnpm build` clean (35/35 pages), `pnpm test` clean (parity check still 9 roles/29 permissions in sync). | Implemented the one genuine agent-catalog gap confirmed by WO-06/07's inventory: `claims-summary` had no implementation of any kind. New `ClaimsSummaryService` (`Application/ClaimsSummary/`) calls 4 tools — `claim.read`, `document.read`, `policy.read`, `knowledge.search` — builds a prompt from ONLY their returned data (never model memory, I6), and calls `chatCompletionProvider`. Proved grounding mechanically, not by inspection: a `CapturingChatCompletionProvider` test double records the exact prompt sent to the model, and the test asserts the real seeded customer name, policy number, and knowledge excerpt all appear in it. Added 3 new tools this agent needed and WO-08 had explicitly deferred until a real consumer existed: `claim.read`, `document.read` (both single-entity, tenant/workspace-scope-checked), `policy.read` (filters the tenant-scoped policy list by `PolicyNumber`, since `IPolicyRepository` has no lookup-by-number method — acceptable at current data volumes, noted as a revisit point if this becomes a hot path). Added `AssistantMode.ClaimsSummary` (additive enum value, no migration — `AgentActionLog` is JSON-payload persisted). Human gate enforced structurally, not by convention: `CanTriggerDecision` is hardcoded `false` on every summary's `AgentActionLog`, the prompt explicitly instructs the model never to approve/deny/settle/repudiate or recommend a decision, and the response is framed as "prepares a human reviewer; it is not itself a decision" (I4) — matching the doc's own human gate for this agent ("Required before decision use"). Endpoint `POST /api/v1/cases/{caseId}/summary` follows the exact established pattern from WO-06's other two direct-agent endpoints: gate check in the endpoint (not the service), `ViewAssignedClaims`/`ViewAllClaims` permission (the same permission already required to view the claim at all — no narrower-than-page-access gap to create, unlike WO-05's cases), honest `AgentDisabled` 503 on refusal, `CaseNotFoundException` caught as a defensive fallback since `WorkflowEndpoints.GetScopedCaseAsync` already checked existence/scope before the service runs. Surfaced where a claims officer actually works, per the work order's explicit instruction — not a new isolated page: a "Claim summary" card on the existing claim-review surface (`ClaimReview.tsx`), reusing `MarkdownText` for the summary body and the same evidence-citation rendering style already established for Command Center answers; no new permission gate needed client-side since the page itself already requires the same permission the endpoint does. New client hook `lib/lovable-parity/data/claimsSummary.ts` follows the established data-hook shape exactly (real fetch, honest 503/error states, no synthetic fallback). Tests: grounding-in-real-facts (the mechanical prompt-capture proof above), full audit/AgentActionLog/AgentRun-with-tool-trace assertion, tenant isolation (`CaseNotFoundException` across tenants), kill-switch refusal at the endpoint with a model-call spy proving the model never runs, enabled-path 200, and tenant/workspace-mismatch 404. |
| WO-11 | DONE | (pending) | `dotnet build` 0/0. `dotnet test` 528+200 = 728/728 (+4 new, zero regressions). `pnpm lint`/`pnpm build`/`pnpm test` all clean. | **Real finding, corrects two earlier claims in this contract's own commit history**: verifying every agent's real call sites for this work order surfaced that `knowledge-assistant` has **no implementation of any kind** — confirmed by grep, only 3 non-execution mentions exist (tool allowed-agent-key lists, one workflow-template node label). WO-06's and WO-08's commit messages both said "only `claims-summary` has no implementation" — that was wrong; it was two agents, and `knowledge-assistant` was never counted because its capability (answering staff knowledge questions) is actually delivered by `policy-intelligence` sharing the same Command Center path, which made the gap easy to miss without re-deriving from call sites, which is exactly what this work order forced. Extended `AgentDescriptor` (`Domain/Agents/AgentRegistry.cs`) with `AgentImplementationKind` (`ModelBacked`/`AiServiceBacked`/`RuleBased`/`NotImplemented`) and `Tools` (cross-referenced against every tool's own `AllowedAgentKeys` from WO-08/WO-10, not asserted independently — the two lists cannot silently drift apart since one is derived from the other by inspection). Classified all 10 honestly: 5 `ModelBacked` (command-center, claims-summary, policy-intelligence, customer-update-drafting, report-insight), 1 `AiServiceBacked` (document-checker — calls Azure Document Intelligence, a real cloud AI service, but never a chat/LLM completion; deliberately not lumped in with the rule engines since output quality genuinely depends on a live AI service) — this AiServiceBacked distinction is itself a WO-11 finding: the original system analysis called document-checker just "not AI-agentic," which undersold what it actually does — 3 `RuleBased` (routing, risk-fraud-signal, underwriting-intelligence — confirmed via grep, no chat/LLM call site for any of the three), 1 `NotImplemented` (knowledge-assistant). Enforcement, not just labeling: `AgentRegistryService.SetKillSwitchAsync` now throws `AgentRegistryValidationException` on any attempt to enable a `NotImplemented` agent (disabling remains always allowed — ADR-0002, the switch may only ever tighten); `ToDto` additionally forces `Enabled=false` for `NotImplemented` regardless of stale persisted state, so a pre-existing `enabled=true` row written before this reclassification can never be reported as running. Admin UI (`Agents.tsx`) renders an honest implementation-kind pill per agent (distinct visual treatment for AI agent / AI service / rule-based / not implemented), lists each agent's real tools inline, and replaces the kill-switch button with a plain "Not implemented" label (no button at all) for `knowledge-assistant` — a control that could never do anything is not rendered, matching I2 exactly. Doc: replaced the architecture doc's §15 "Phase 1-4 agent alignment" (phased delivery framing) with a **current implementation status** table stating plainly that it is generated from `AgentCatalog`/the build contract's own call-site inventory, is current-state (not target-state, which §5/§9 remain), and must be trusted over the target-state registry when they disagree. Tests: `SetKillSwitch_CannotEnableNotImplementedAgent`, `SetKillSwitch_CanStillDisableAnAlreadyDisabledNotImplementedAgent`, `ListAsync_NeverShowsANotImplementedAgentAsEnabled` (proves the `ToDto` override against a deliberately-stale persisted `enabled=true` state), `AgentCatalog_EveryEntry_HasToolsConsistentWithImplementationKind` (a `NotImplemented` agent has zero tools; every other agent has at least one — catches future catalog entries that forget to declare kind/tools consistently). |
| CP-C | | | | |
| WO-12 | | | | |
| WO-13 | | | | |
| WO-14 | | | | |
| CP-D | | | | |
| WO-15 | | | | |
| WO-16 | | | | |
| WO-17 | | | | |
| WO-18 | | | | |
| WO-19 | | | | |
| CP-E | | | | |
| WO-20 | | | | |
| WO-21 | | | | |
| WO-22 | | | | |
| CP-F | | | | |

Status values: `DONE` · `PARTIAL` (note what remains) · `BLOCKED` (link §9) · `SKIPPED` (state why).

---

## 9. Blockers and drift

Record anything that stops work or contradicts this contract. Do not work around it silently (A11).

| # | WO | Type | Description | Needs | Status |
|---|---|---|---|---|---|
| | | | | | |

Types: `credential` · `azure-resource` · `owner-decision` · `contract-drift` (code disagrees with this document — code wins, record it) · `doc-conflict` · `out-of-scope` · `test-conflict`.

**Known likely blockers:**
- Any work order needing live Azure providers requires valid credentials. Offline mode (§5) covers most verification; where it cannot, record the gap rather than mocking past it.
- WO-07 and WO-14 add migrations. This repository has a prior defect where a migration was generated against a stale local model and recreated ~28 existing tables. Generate against the current model and verify against a database at head before applying.

**Resolved, no longer blocking:** WO-22 was previously flagged as requiring owner confirmation for a history rewrite. It no longer does — the work order now uses `git rm --cached`, which leaves history and all remote branches intact.

---

## 10. Contract summary

Twenty-two work orders. Six checkpoints. No phases, no schedule — dependency order only.

The gate that matters most is **Checkpoint A**. After it, the system is truthful: nothing invented, nothing inert. It is not feature-complete there, but it is safe to put in front of an insurer, because every surface either works or honestly says it does not.

Everything after Checkpoint A adds capability to a base that can be trusted. Everything before it removes claims the system could not support.

Build forward: secure, grounded, human-accountable, observable, honest.
