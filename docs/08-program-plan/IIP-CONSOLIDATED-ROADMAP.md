# IIP — Consolidated Implementation Roadmap (Authoritative)

**Status:** Authoritative execution sequence · **Owner:** Product/Engineering · **Created:** 2026-07-07
**Implemented by:** Codex (primary) · **Supersedes for sequencing:** this document is the single source of truth for *what order to build and deploy in*. It reconciles the three prior plans:
- `docs/01-strategy-requirements-roadmap/iip_implementation_roadmap.md` (strategic Phases 0–9 — still valid for scope/requirements detail)
- `docs/08-program-plan/IIP-FULL-PROGRAM-PLAN.md` (build lanes — still valid for file-ownership detail)
- AGENTS.md "Production Release Program" Gates 0–6 (still the security/release contract)

Where any of those conflict on **order**, follow this document. Where they add **detail** (scope, acceptance, file lanes, security gates), defer to them.

---

## Why this document exists (the correction)

We built application code deeply (Phases 0–2 + several commercial modules) **before establishing any Azure foundation**, then attempted a **staging** deploy into an environment that didn't exist — skipping resource groups, a dev environment, and subscription-capability validation. That inversion caused a live incident (deleted resources discovered mid-deploy; Sponsored subscription unable to host).

This roadmap fixes the order with three rules:

1. **One source of truth.** This file. No more steering by three competing plans.
2. **Cloud Foundation is an early, explicit phase (Phase 3) — nothing deploys before it.** Subscription capability → resource groups → dev environment → live proof, *before* any staging.
3. **Ship the first module before commercial expansion.** Motor Claims + Document Intelligence (the MVP wedge) reach a piloted, deployed state before further work on Fraud/Underwriting/Billing/etc.

Status keys: ✅ done · 🔄 partial/in-progress · ⬜ not started · 🔒 blocked on a decision or external access.

---

## Phase overview

| Phase | Name | Status |
|---|---|---|
| 0 | Decision Lock & Docs Baseline | ✅ Done |
| 1 | App Foundation (secure, tenant-aware, local-runnable) | ✅ Done |
| 2 | Core Workspaces & Shell | ✅ Done |
| **3** | **Cloud Foundation** *(the previously-missing phase)* | 🔄 In progress |
| 4 | Motor Claims Workflow MVP *(first module)* | 🔄 Partial |
| 5 | Document Intelligence & AI Review | 🔒 Provider-gated |
| 6 | Integrations & Resilience | 🔒 Provider-gated |
| 7 | Security, Compliance & Observability hardening | 🔄 Partial |
| 8 | QA, UAT & Pilot Readiness | ⬜ |
| 9 | Staging & Managed-App / Marketplace packaging | 🔒 Needs capable subscription |
| 10 | Pilot Launch & Stabilization | ⬜ |
| 11 | Commercial Modules (post-MVP) | 🔄 Built-ahead, to activate |

Track tags per item: **[APP]** app/module code · **[CLOUD]** Azure infra · **[GOV]** security/compliance/QA.

---

## Phase 0 — Decision Lock & Docs Baseline ✅
- ✅ Naming = **Insurance Intelligence Platform (IIP)**; ACIP superseded.
- ✅ MVP wedge = **Motor Claims + Document Intelligence**.
- ✅ AI boundary = *AI assists; humans make regulated decisions*.
- ✅ Customer portal **out of scope** (internal insurer workspace only, per PRD).
- ✅ UI = three-panel command center (Ambient Intelligence design system v1.2).
- ✅ Repo guardrails (AGENTS.md / CLAUDE.md / IIP_IMPLEMENTATION_GUARDRAILS.md).

## Phase 1 — App Foundation ✅ *(AGENTS Gate 0 + Gate 1 code)*
- ✅ .NET 8 API + Next.js portal (standalone); layered Domain/Application/Infrastructure/Api.
- ✅ Microsoft Entra identity model (**ADR-0005**): store-authoritative authorization, `oid`-keyed membership, app roles as coarse `IIP.Access` gate, fail-closed on no membership.
- ✅ Tenant/workspace enforcement server-side; ✅ EF Core data model; ✅ audit pipeline.
- ✅ Provider abstraction + honest `Not Configured`; ✅ health endpoints; ✅ Key Vault config provider; ✅ tenant onboarding + Entra admin-consent flow.
- ✅ **338 tests pass** (238 API + 100 Domain). ✅ portal lint/build green.
- 🔄 Open: **PR #3** (`/me` resolves scope from membership store) awaiting merge. Full live-proof deferred to Phase 3.7.

## Phase 2 — Core Workspaces & Shell ✅
- ✅ Command Center, Channels (threaded two-pane inbox), Sales & Leads, Customer 360 (`customers/[id]`), Claims shell, Documents hub, Approvals, Audit, Admin, Support.
- ✅ Every `/app/*` screen reconciled to real `/api/v1` with honest empty/error/Not-configured states; **no fabricated data in any deployed surface**.
- ✅ Design tokens (glass/ambient) + governed-action dialogs + evidence/citation primitives.

## Phase 3 — Cloud Foundation 🔄  *(NEW — must complete before any deploy)*
**Goal:** a real, running **dev** environment proving identity + data + config end-to-end, on validated Azure infrastructure.

- ✅ **3.1 Subscription capability validated.** Sponsored sub `5f5765ae-985f-42e4-a443-8e2447be33ca` (tenant `dcb0b132-fb9e-475c-a68e-b0671222e8d8`, Otic Group): **Basic (B1) App Service allowed; Standard (S1)+ blocked (`ServerFarmCreationNotAllowed`); Azure OpenAI blocked (`SpecialFeatureOrQuotaIdRequired`); Document Intelligence likely blocked (same Cognitive family).**
- ✅ **3.2 Resource groups:** `rg-iip-dev` (westus2), `rg-iip-stg` (centralus, empty placeholder — kept intentionally).
- ✅ **3.3 Dev compute:** `asp-iip-dev` App Service Plan (B1 Basic, Linux) created. Entra apps intact & reusable: `iip-portal-stg` = `3b3522e4-13fd-4bd9-8f08-a1cedf428d0d`, `iip-api-stg` = `d55fe46b-1fba-4c5b-81e1-4ee1918ecf73` (scope `api://d55fe46b-…/access_as_user`).
- 🔒 **3.4 DECISION GATE — deployment subscription.** The Sponsored sub can host a **dev** env (Basic) only. **Staging/prod (Standard SKU) and all Azure AI require a pay-as-you-go subscription + an Azure OpenAI access request (`aka.ms/oai/access`).**
  - **Decision status (2026-07-07):** Pay-as-you-go is **DEFERRED** — pending sign-off from the owner's higher-ups; not available yet. **Proceed dev-only on the Sponsored sub (Basic) in the meantime.** Do **not** attempt staging/prod or provision Azure OpenAI until PAYG is confirmed.
  - **Near-term executable path while deferred:** Phases 3.5 → 3.7 (dev backing services + deploy + Gate-1 live proof on Basic) and Phase 4 (Motor Claims on the dev env). **Parked until PAYG:** Phase 5 real AI (OpenAI/DocIntel), Phase 9 staging/packaging — keep these honestly `Not Configured`.
- ⬜ **3.5 Provision dev backing services** into `rg-iip-dev` (allowed types): Azure SQL (Basic), Storage (LRS), Key Vault, AI Search (Basic/Free), Service Bus (Basic). Verify Document Intelligence; gate as Not-configured if blocked.
- ⬜ **3.6 Deploy** API + portal to `asp-iip-dev`; wire real app settings + managed-identity RBAC + SQL contained user. Deploy path: `.github/workflows/deploy-staging.yml` (generalize to dev) or `az webapp deploy`.
- ⬜ **3.7 Gate-1 live proof** on dev: real Entra login → `/me` from membership store → Key Vault secret load → Azure SQL read/write. OpenAI/DocIntel = Not configured.
- **Exit gate:** dev env serves the app with real identity, SQL, Key Vault, Storage, Search; Gate-1 acceptance evidence captured.
- **Naming note for infra:** template names resources `{type}-${environmentName}-${uniqueSuffix}`, `uniqueSuffix = uniqueString(resourceGroup().id, environmentName)` — **no region token** (the old deleted names carried `cus` from a *different* template). Names are deterministic per (subscription, RG, env); a different subscription changes them → update deploy targets + Entra redirect URIs accordingly.

## Phase 4 — Motor Claims Workflow MVP 🔄  *(first module to ship)*
- 🔄 FNOL / New Claim Intake · Work Queue (filter/assign/escalate) · Case Review workspace (exists) · full Motor status model · SLA tracking · tasks · human approvals · customer-safe update drafting · evidence-pack export.
- **Action:** verify each screen/endpoint against the FSD + `iip_implementation_roadmap.md` §8; complete gaps; all sensitive actions audited; no AI finalizes a claim.
- **Exit gate:** FNOL → Case Review → human-approved next step → export, end-to-end on the dev env.

## Phase 5 — Document Intelligence & AI Review 🔒
- 🔄 Upload/Blob, classification, extraction-review UI, confidence, validation rules, missing-doc checklist, evidence linking (UI/status models exist).
- 🔒 Real **Azure Document Intelligence** extraction + **Azure OpenAI** (claim summary, policy Q&A/RAG over approved sources, customer-safe draft) — gated on the Phase 3.4 subscription + OpenAI access. Honest Not-configured until then.
- **Guardrails:** low-confidence → human review; AI shows uncertainty + evidence; fraud output advisory only; no autonomous decisions.

## Phase 6 — Integrations & Resilience 🔒
- 🔄 Provider abstractions + health exist. 🔒 Activate: Microsoft Graph/Outlook ingestion, WhatsApp (or Not configured), export API, durable Service Bus queues + retry/backoff. No customer message silently lost.

## Phase 7 — Security, Compliance & Observability 🔄
- ✅ RBAC, tenant isolation, Key Vault, audit (code-level, tested). ⬜ App Insights + Log Analytics + alerts (need real env), PII masking/minimization, AI-governance tests (prompt/tool/model logging), consent/retention/SLA compliance views, data-residency confirmation.

## Phase 8 — QA, UAT & Pilot Readiness ⬜
- Functional/UI-state/API/document/AI/integration/security/perf tests; synthetic Motor-Claim packs (clean, missing docs, expired permit, mismatches, high-value quote, duplicate invoice, blurry ID, fraud signal, escalated, complaint); UAT scripts (claims officer/supervisor/admin/compliance); go/no-go checklist.

## Phase 9 — Staging & Packaging 🔒
- 🔒 Staging deploy (Standard SKU → **needs pay-as-you-go**, Phase 3.4). Deploy wiring **ready** (PR #4, `feature/gate2-staging-deploy-reuse`) — finalize app names + Entra redirect URIs to the chosen environment.
- ⚠️ **Managed-App package / Marketplace: do not touch** — the `IIP Starter Managed` offer is already published; `infra/mainTemplate.bicep` stays build-only; never republish without explicit instruction.

## Phase 10 — Pilot Launch & Stabilization ⬜
- Controlled insurer pilot on Motor Claims; daily monitoring; issue log; weekly steering; metrics per `iip_implementation_roadmap.md` §13; retrospective → expansion decision.

## Phase 11 — Commercial Modules (post-MVP) 🔄
- ⚠️ **Built ahead of sequence (real rule-based v1 code, honestly gated), to be HELD — not extended — until the MVP is piloted:** Fraud Intelligence, Underwriting Support, AI Agent Registry (+ kill switch), Billing & entitlements.
- **Post-pilot activation:** add Azure OpenAI narration atop the Fraud/Underwriting rule engines (needs Phase 3.4 OpenAI); then Early Warning/Retention, Customer Acquisition, Advanced Analytics, Broker Workspace, Policy Servicing, Marketplace metering.

---

## Current Azure state snapshot (2026-07-07, ground truth)

| Item | State |
|---|---|
| Subscription (dev-capable only) | `5f5765ae…` Sponsored, tenant `dcb0b132…` (Otic Group) — Basic SKU only, no OpenAI/DocIntel |
| `rg-iip-dev` (westus2) | Contains `asp-iip-dev` (B1 Basic Linux). Backing services not yet provisioned. |
| `rg-iip-stg` (centralus) | Empty placeholder (kept). Staging blocked pending capable subscription. |
| Entra apps | `iip-portal-stg` (`3b3522e4…`), `iip-api-stg` (`d55fe46b…`) — intact, reusable. |
| Key Vault | Old `kv-stg-vyhhymwydvai2` was recovered, inspected (only unused `graph-client-secret`), then purged. Regenerate Graph secret when Graph is enabled. |
| Prior staging RG | Deleted out-of-band ~2026-07-03 (not by any repo). Do not assume "existing resources" exist. |

## Execution notes for Codex
- **Never deploy before its Phase-3 prerequisite exists** (subscription validated → resource group → env). Verify Azure state with `az` before acting; do not trust inventory docs.
- Work on branches; do not advance protected branches without approval. Coordinate with the Claude clone via git per `docs/06-decisions/gate-1-handoff.md`.
- Keep the MVP wedge (Phases 4–5) ahead of commercial-module work (Phase 11).
- Honor all AGENTS.md guardrails: real providers or `Not Configured` in deployed envs; no mocks/fake success outside LocalTest/automated tests; human-in-the-loop; audit; tenant isolation; no secrets committed.
