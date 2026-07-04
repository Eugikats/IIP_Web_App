# IIP Full-Program Implementation Plan (Docs-Only, From Scratch)

## Context

The Insurance Intelligence Platform v2 repo (`C:\Users\Nic Kings\OneDrive\Desktop\IIP V2`) contains a mature, ~150-document specification pack covering frontend/UX, backend architecture, Azure infrastructure, and a 10-phase roadmap — but only a fraction of it is built. This plan sequences the **remaining** work (as if starting fresh, grounded solely in this repo's docs) into phases that two AI coding agents — **Claude Code** and **OpenAI Codex** — can execute **in parallel sessions on the same repo without colliding**.

**Why this matters now:** two agents have already worked on this repo in the same session window and produced two near-miss conflicts already found and fixed (an `AGENTS.md` §20 route-prefix conflict, a §12 block-naming conflict). Without an explicit file-ownership plan, that pattern will repeat at much larger scale once 16 backend services and ~19 frontend screens are being built concurrently.

**Ground truth used to build this plan** (verified by reading the actual repo, not assumed):
- **Built today:** Domain/Application/Infrastructure/Api/Functions/LocalTestDoubles projects; only `Application/Core` (4 port *interfaces*, no EF impl), `Application/Claims`, `Application/CommandCenter` are real. Auth is multitenant Entra (correct, don't touch). All routes are `/api/v1` (correct, don't touch). Command Center emits 12 typed blocks, portal parser matches exactly (verified). 217 API tests + 30 Domain tests pass. Portal has ~19 screens; **Channels and Sales & Leads are UI-only — no backend exists for them yet.**
- **Spec-only (documented, not built):** 16 backend services, each with its own `iip_*_service_implementation_output.md` doc in `docs/02-development-engineering/`. EF implementations of the 4 Core ports. The multi-agent orchestrator. Several frontend screens' real-data wiring.
- **A live, unresolved doc conflict:** `infra/AZURE_TARGET_ARCHITECTURE.md` (matches the actual working `infra/mainTemplate.bicep`, App Service-based) vs `docs/05-deployment-release-enablement/iip_delivery_tooling_openapi_iac_testdata_cicd.md` (specifies a different, newer Container Apps-based IaC layout). Not reconciled — this plan resolves it once, up front (Phase 0), rather than letting different phases pick different answers.
- **A missing doc set:** `AGENTS.md` §19 points to `docs/ui/IIP_Operations_UI_Design_Specification_MVP.md`, `docs/ui/reference/IIP_Global_Theme_Approved.png`, `docs/ui/backend-contract-map.md` — **none exist**. This plan uses the real authoritative set instead: `IIP_FRONTEND_ARCHITECTURE_AND_AGENT_HANDOFF.md`, `iip_ui_ux_handoff_document.md`, `iip_frontend_implementation_guide.md`, `iip_design_system_ux_behavior_specification.md`, `iip_component_architecture_document.md`.
- **10 business Open Questions (OQ-001–010)** and the Deployment Plan's 10 Open Decisions (D1–D10) are **not engineering blockers** — the docs' own guidance is to default to "Not Configured" / generic MVP config and let a human close them in parallel. This plan treats them exactly that way.

**Intended outcome:** a phase-by-phase build (Phase 0 → Phase 9, following the roadmap's own shape) where every work item names its owning agent, its exact file/folder lane, what it must NOT touch, and a ready-to-hand-off prompt — so you can literally copy a prompt into a Claude or Codex session and know it won't collide with whatever the other agent is doing.

---

## How to use this plan (read once, applies to every phase below)

1. **One folder, one agent, one active task at a time.** Never run two agents against the same `Application/<Area>/` or `Api/<Area>/` folder concurrently, even for "different files in the same folder" — sequence them instead.
2. **Composition-root/DI-registration and route-table edits are always solo tasks.** These touch shared files (`Program.cs`, `Infrastructure/DependencyInjection/*`). Run them alone — no other task active on either agent at that moment. Each phase has exactly one such task, always assigned to Claude, always last in the phase.
3. **`infra/` is Claude-only for this entire plan.** Codex never touches Bicep/`createUiDefinition.json`/`viewDefinition.json` unless a future phase explicitly reassigns it.
4. **Tests are additive-only.** New test files per feature, never edits to existing test files — this alone lets Claude and Codex both write tests in parallel with zero merge risk.
5. **One frontend screen folder, one agent, one phase.** No two agents editing `web/portal/app/<screen>/*` at the same time.
6. **Doc-reconciliation/naming passes are batched at the end of a phase, solo, Claude** — never interleaved with active service-building (an in-flight Codex task reading a doc mid-rename will build against the wrong name).
7. **If a phase's work finishes faster than expected, move directly to the next phase** — phases are ordered by dependency, not by calendar time.
8. **Every UI/screen work item requires a visual approval checkpoint.** Before marking any frontend task complete, the implementing agent must start the app locally, navigate to the screen(s) it built or changed, capture a live screenshot, and present it to you for explicit approval or change requests. Do NOT proceed to a dependent screen or the next phase's frontend work until you approve. This applies to every work item whose "Owns" includes a `web/portal/app/*` folder — each such prompt below includes this instruction directly (so it survives being copy-pasted into a fresh session on its own).
9. Every work item below states: **Agent**, **Governing doc(s)**, **Owns (files/folders)**, **Must not touch**, **Depends on**, and a **Prompt** ready to hand off verbatim.

---

## Parallel execution, continuity, and usage-limit handling

**Can Claude and Codex genuinely work at the same time?** Yes, for any two work items whose "Owns" folders don't overlap (e.g., W2.1 Channels + W2.2 Customers + W2.3 SalesLeads + W2.4 Audit are four disjoint folders and could, in principle, run concurrently). But running two agents inside the **same** checked-out working directory at the same literal moment is risky — a race between two agents' file writes, or one agent's uncommitted work being clobbered by the other's git operations. **Recommended setup:** use two separate working copies of the same repo/branch — either two plain `git clone`s of `agenticIip-v2` on disk, or two `git worktree`s of the same clone — one that you drive Claude Code from, one that you drive Codex from. They only ever interact through git (push/pull), never through the filesystem directly, so true simultaneous work is safe as long as:
- Each agent works only within its assigned work item's "Owns" folders (as specified per item below).
- Each agent commits and pushes as soon as its work item is done — don't batch multiple work items into one uncommitted pile.
- Before starting a new work item, `git pull` in that working copy first, so it has the other agent's finished work (needed when a task `Depends on` an earlier one).

**What if you hit a usage limit mid-task and need to switch tools (e.g., Claude Code → Codex/Code.exe, or vice versa)?** This is safe specifically *because* every work item is scoped to its own folder and ends with a commit:
- If the interrupted task's work is **already committed**, there's nothing to hand off — just give the next work item's prompt to whichever agent/tool is available.
- If the interrupted task's work is **uncommitted** (mid-way through), run `git status` before switching. If it's a small, coherent chunk, commit it with a message prefixed `WIP: <work-item-id> partial — not yet verified`, then hand the *same* prompt to the next agent with an added note: "Work is partially done in commit <hash> — read the diff first, finish it or redo it, then run the verification gate before committing again." If the partial work looks messy or you're unsure, it's simpler to `git checkout -- <files>` to discard it and start the same prompt fresh in the new tool — since each work item is small (one folder, one service), redoing it costs little.
- Because every prompt in this plan is self-contained (names its own governing doc, files, and constraints), **any agent in any tool can pick up any not-yet-done work item cold** — there's no hidden context trapped in one tool's conversation history that the plan depends on.

---

## PHASE 0 — Decision Lock (one blocking decision + parallel tracks)

### W0.0 — Commit this plan into the repo [Claude, solo, first action]
**Owns:** new file `docs/08-program-plan/IIP-FULL-PROGRAM-PLAN.md`
**Prompt:**
> Copy the full contents of this approved plan into a new file `docs/08-program-plan/IIP-FULL-PROGRAM-PLAN.md` in the repo, commit it ("docs: add full-program implementation plan"), and push to `main`. This makes the plan itself a versioned artifact in the repo that every future work item's prompt can reference (`docs/08-program-plan/IIP-FULL-PROGRAM-PLAN.md`) rather than depending on an external plan file.

### W0.1 — Resolve the infra hosting-model conflict [Claude, BLOCKING for all infra work in Phases 5+]
**Governing docs:** `infra/AZURE_TARGET_ARCHITECTURE.md`, `docs/05-deployment-release-enablement/iip_delivery_tooling_openapi_iac_testdata_cicd.md`
**Owns:** new file `infra/ADR-0001-hosting-model.md`
**Must not touch:** `infra/mainTemplate.bicep`, any other infra file (decision-only, no code change)
**Depends on:** nothing
**Prompt:**
> Read `infra/AZURE_TARGET_ARCHITECTURE.md` and `docs/05-deployment-release-enablement/iip_delivery_tooling_openapi_iac_testdata_cicd.md` in full. They specify two different hosting models for the API (App Service vs. Azure Container Apps). The working `infra/mainTemplate.bicep` already implements the App Service model. Write `infra/ADR-0001-hosting-model.md` recording the decision: keep the App Service model for the MVP pilot (it's already built and working); treat the Container Apps spec in the Delivery Tooling doc as a Phase-9+ candidate to revisit only if post-pilot usage data justifies it. State the reasoning in 4-6 sentences. Do not modify any other file.

### W0.2 — Track the business Open Questions [Claude, parallel, non-blocking]
**Governing docs:** `docs/01-strategy-requirements-roadmap/iip_assumptions_constraints_open_questions_register.md`, `docs/05-deployment-release-enablement/iip_deployment_plan.md` (Open Decisions D1–D10)
**Owns:** new file `docs/06-decisions/open-questions-tracker.md`
**Must not touch:** any src/ or web/ file
**Depends on:** nothing
**Prompt:**
> Read `docs/01-strategy-requirements-roadmap/iip_assumptions_constraints_open_questions_register.md` (OQ-001–010) and the Open Decisions section (D1–D10) of `docs/05-deployment-release-enablement/iip_deployment_plan.md`. Create `docs/06-decisions/open-questions-tracker.md` listing all 20 items with owner, status (open), and — critically — which engineering default is already in place so engineering does not block on them (e.g., "OQ-009 final roles: default RBAC roles in use, validate at UAT per D-005 mitigation"). This is a tracking doc only, not a decision-making doc.

### W0.3 — Freeze Partner Center / Marketplace track [no action, deferred]
**Owns:** nothing yet. Do not assign `offer-metadata.md`, `createUiDefinition.json`, `viewDefinition.json` to anyone until Phase 9.

**Gate:** `infra/ADR-0001-hosting-model.md` and `docs/06-decisions/open-questions-tracker.md` committed. `git status` clean apart from these two new files. No build/test changes expected — run `dotnet build` once to confirm nothing broke.

---

## PHASE 1 — Platform Foundation Completion

Only the 4 Core port **interfaces** exist (`Application/Core/`); no EF-backed implementation.

> **CORRECTION (applied during W1.3, commit prefixed `W1.3:`):** The premise above was inaccurate. Only **1** of the 4 Core ports (`IProviderHealthCheck`) actually lacked an implementation. The other 3 were already implemented via the codebase's hexagonal pattern: `PermissionService`, `AuditWriter`, and `TenantContextAccessor` (all in `Application/Core/`) contain the domain logic and delegate persistence to repository ports (`IPermissionReadRepository`, `IAuditEventRepository`) whose EF adapters (`EfPermissionReadRepository`, `EfAuditEventRepository`) were **already implemented and already registered** in DI. W1.1 (built before this was noticed) produced `PermissionServiceEf`/`AuditWriterEf`/`TenantContextAccessorEf` that duplicated that logic and collapsed the service/repository layer separation (`TenantContextAccessorEf` used no DbContext at all — a pure duplicate). Those three files were **removed** during W1.3; only `ProviderHealthCheckEf` (a genuine addition — it wraps `AzureProviderHealthCheck` and adds a real Azure SQL connectivity probe) was kept and registered. **Lesson for future port work: before "implementing an unimplemented port", grep for an existing concrete `class Foo : IFoo` and check whether the port is already satisfied through a service+repository split — don't add a second implementation of a port that already has one.**

### W1.1 — EF implementations of the 4 Core ports [Codex]
**Governing docs:** `docs/02-development-engineering/iip_infrastructure_layer_core_ports_implementation.md`, `iip_concrete_implementation_roadmap_infrastructure_layer_repository_buildout.md`
**Owns:** new files `src/InsuranceIntelligence.Infrastructure/Persistence/{TenantContextAccessorEf,PermissionServiceEf,AuditWriterEf,ProviderHealthCheckEf}.cs` (match existing naming style found in `Infrastructure/Persistence/`)
**Must not touch:** `src/InsuranceIntelligence.Application/Core/*Contracts.cs` (interfaces are frozen), `Infrastructure/DependencyInjection/*`
**Depends on:** nothing
**Prompt:**
> Read `docs/02-development-engineering/iip_infrastructure_layer_core_ports_implementation.md` in full. It defines 4 Application-layer ports: `ITenantContextAccessor`, `IPermissionService`, `IAuditWriter`, `IProviderHealthCheck` (find the interfaces in `src/InsuranceIntelligence.Application/Core/`). Only interface stubs exist today — no EF-backed implementation. Implement each against Azure SQL via EF Core, following the persistence patterns already used in `src/InsuranceIntelligence.Infrastructure/Persistence/` (look at how existing repositories there are structured for the naming/DI-friendly-constructor convention). Create one new file per port. Do NOT edit the interface files in `Application/Core/`, and do NOT touch `Infrastructure/DependencyInjection/` — DI registration is a separate, later task. Build the Infrastructure project when done to confirm no compile errors.

### W1.2 — EF migrations for Phase-1 entities [Codex, after W1.1]
**Governing docs:** `docs/03-design-architecture/iip_data_model_document.md` (Tenant/Workspace, Customer 360 sections only)
**Owns:** new migration files only, in `Infrastructure/Migrations/`
**Must not touch:** existing migration files
**Depends on:** W1.1
**Prompt:**
> Read the Tenant/Workspace and Customer 360 entity sections of `docs/03-design-architecture/iip_data_model_document.md`. Confirm which of these tables are needed to back the 4 Core port EF implementations just added in `src/InsuranceIntelligence.Infrastructure/Persistence/`. Add a new EF Core migration only for tables not already present (check `Infrastructure/Migrations/` first — some tables may already exist from earlier migrations). Do not hand-edit any existing migration file; add a new one via `dotnet ef migrations add`.

### W1.3 — Composition-root wiring pass #1 [Claude, SOLO, after W1.1+W1.2]
**Governing docs:** `docs/02-development-engineering/iip_backend_wiring_controllers_composition_root_request_pipeline.md`
**Owns:** `Infrastructure/DependencyInjection/*ServiceCollectionExtensions.cs` (or equivalent existing DI file), `Program.cs` if needed
**Must not touch:** anything else
**Depends on:** W1.1, W1.2. **Must be the only active task across both agents when run.**
**Prompt:**
> Read `docs/02-development-engineering/iip_backend_wiring_controllers_composition_root_request_pipeline.md` for the composition-root registration order. Register the 4 new EF port implementations (added in `Infrastructure/Persistence/`) into DI, following the existing registration pattern in `Infrastructure/DependencyInjection/`. This is a solo task — confirm no other work is in flight before starting, since this touches a shared file. Build and run the existing test suite (`dotnet test`) to confirm nothing regressed.

### W1.4 — New tests for the EF port implementations [Codex, parallel-safe with W1.1]
**Owns:** new file `tests/InsuranceIntelligence.Domain.Tests/CorePortsEfImplTests.cs`
**Must not touch:** `CorePortsAndClaimsQueryTests.cs` (existing file)
**Prompt:**
> Write `tests/InsuranceIntelligence.Domain.Tests/CorePortsEfImplTests.cs` (new file) covering the 4 EF port implementations added in `src/InsuranceIntelligence.Infrastructure/Persistence/`. Do not edit `CorePortsAndClaimsQueryTests.cs` or any other existing test file — add only the new file.

**Gate:** `dotnet build` (whole solution) 0 errors; `dotnet test` — existing 217+30 tests plus new W1.4 tests all pass.

---

## PHASE 2 — Core Workspaces Backend (query-only services, batch 1)

Four independent read-only services. Each owns its own top-level folder — fully parallelizable, but sequence 2-at-a-time to keep the DI-merge small.

### W2.1 — Channels query service [Codex]
**Governing doc:** `docs/02-development-engineering/iip_channels_query_service_implementation_output.md`
**Owns:** new folder `Application/Channels/`, new folder `Api/Channels/` (endpoint file, follow the `Api/Cases/CaseEndpoints.cs`-style static-class pattern used elsewhere in `Api/`)
**Must not touch:** `web/portal/app/channels/*` (read-only reference for shape, do not edit)
**Prompt:**
> Read `docs/02-development-engineering/iip_channels_query_service_implementation_output.md`. It defines `IChannelQueryService` and its read repository. There is no backend for Channels today — the portal's `web/portal/app/channels/*` UI is currently reading placeholder/UI-only data; you may open it read-only to see what shape the frontend expects, but do not edit any file under `web/portal/`. Implement the service in a new `src/InsuranceIntelligence.Application/Channels/` folder (contracts file + service file, following the pattern in `Application/Claims/ClaimsQueryContracts.cs` + `ClaimsQueryService.cs`). Add a new `src/InsuranceIntelligence.Api/Channels/` folder with an endpoint file matching the existing minimal-API static-class convention (see `Api/Cases/CaseEndpoints.cs` for the pattern). Do NOT register routes in `Program.cs` yet — that's a separate composition-root task. Add a new test file only.

### W2.2 — Customer query service [Codex]
**Governing doc:** `iip_customers_query_service_implementation_output.md`
**Owns:** new folder `Application/Customers/`, new folder `Api/Customer/` (note: `Api/Customer/` singular folder already exists with `CustomerSafeDtos.cs` — add alongside, do not restructure)
**Prompt:**
> Read `docs/02-development-engineering/iip_customers_query_service_implementation_output.md`. Implement `ICustomerQueryService` in a new `src/InsuranceIntelligence.Application/Customers/` folder (contracts + service files, same pattern as `Application/Claims/`). Add endpoint(s) in the existing `src/InsuranceIntelligence.Api/Customer/` folder alongside the existing `CustomerSafeDtos.cs` — do not modify that existing file, only add new ones. No route registration in `Program.cs` yet. Add a new test file only.

### W2.3 — Sales & Leads query service [Codex]
**Governing doc:** `iip_sales_leads_query_service_implementation_output.md`
**Owns:** new folder `Application/SalesLeads/`, new folder `Api/SalesLeads/`
**Prompt:**
> Read `docs/02-development-engineering/iip_sales_leads_query_service_implementation_output.md`. There is no backend for Sales & Leads today — `web/portal/app/sales-leads/*` is UI-only (read-only reference, do not edit). Implement `ISalesLeadQueryService` in a new `src/InsuranceIntelligence.Application/SalesLeads/` folder and a new `src/InsuranceIntelligence.Api/SalesLeads/` endpoint folder, following the `Application/Claims/` file-pair pattern. No route registration in `Program.cs` yet. Add a new test file only.

### W2.4 — Audit query service [Codex]
**Governing doc:** `iip_audit_query_service_implementation_output.md`
**Owns:** new folder `Application/Audit/` (note: `Api/Compliance/ComplianceEndpoints.cs` already exposes some audit-events reads — check for overlap before creating new routes, but the Application-layer service itself is net-new)
**Prompt:**
> Read `docs/02-development-engineering/iip_audit_query_service_implementation_output.md`. First check `src/InsuranceIntelligence.Api/Compliance/ComplianceEndpoints.cs` — it already exposes `GET /api/v1/compliance/audit-events`. Determine whether this new `IAuditQueryService` should back that existing endpoint (refactor the endpoint's implementation only, not its route) or is a genuinely separate concern per the doc. Implement in a new `src/InsuranceIntelligence.Application/Audit/` folder (contracts + service). Do not change the existing route path. Add a new test file only.

### W2.5 — Composition-root wiring pass #2 [Claude, SOLO, after W2.1–W2.4 land]
**Owns:** shared DI file, `Program.cs` route registrations for the 4 new services
**Depends on:** W2.1, W2.2, W2.3, W2.4
**Prompt:**
> Register the 4 new query services (Channels, Customers, SalesLeads, Audit) into DI and map their routes in `Program.cs`, following the existing `/api/v1/...` pattern and the registration style already used for other services. Cross-check the new routes against `docs/02-development-engineering/iip_api_specification.md`'s route table for naming consistency. This is a solo task — confirm nothing else is active first. Run `dotnet build` + `dotnet test` to confirm green.

### W2.6 — Wire Channels & Sales-Leads screens to real data [Claude]
**Governing docs:** `IIP_FRONTEND_ARCHITECTURE_AND_AGENT_HANDOFF.md`, `iip_frontend_implementation_guide.md`
**Owns:** `web/portal/app/channels/*`, `web/portal/app/sales-leads/*` only
**Depends on:** W2.5 (routes must exist)
**Prompt:**
> Read `docs/02-development-engineering/IIP_FRONTEND_ARCHITECTURE_AND_AGENT_HANDOFF.md` and `iip_frontend_implementation_guide.md` for the Channels and Sales & Leads screen specs. The backend now has real `/api/v1/channels/...` and `/api/v1/sales-leads/...` endpoints (just added). Update `web/portal/app/channels/*` and `web/portal/app/sales-leads/*` to fetch from these real endpoints instead of any placeholder data, matching the actual response DTO shapes (check the new `Application/Channels/` and `Application/SalesLeads/` contracts files for exact field names — do not guess). Touch only these two screen folders. Run `pnpm build && pnpm lint` in `web/portal/` to confirm green. **Before considering this task done: start the app locally, navigate to both the Channels and Sales & Leads screens, capture a live screenshot of each, and present them to the user for approval. Do not proceed to any other screen's work until approved or told what to change.**

### W2.7 — Doc reconciliation pass (naming) [Claude, solo, end of phase]
**Governing doc:** `docs/02-development-engineering/iip_cross_document_consistency_reconciliation_pass.md`
**Owns:** doc files only
**Prompt:**
> Read `docs/02-development-engineering/iip_cross_document_consistency_reconciliation_pass.md`. Apply the still-open corrections it lists (canonical constants like `ApprovalStatusValues`, `ProviderCodes`, `KnowledgeApprovalStatusValues`, `EmailProviderValues` — check whether the services that need them exist yet; if not, note in the doc that the fix applies "when built" rather than forcing premature scaffolding). This is a documentation-only pass — do not create speculative code for services not yet built. Run at the end of the phase, solo, so no in-flight Codex task is confused by a mid-stream rename.

**Gate:** Full regression (`dotnet test` + `pnpm build`/`lint`) green. Manual/Playwright smoke: Channels and Sales-Leads screens visibly load real (not placeholder) data.

---

## PHASE 3 — Claims Workflow MVP

### W3.1 — Claim intake + workflow transition services [Codex]
**Governing doc:** `docs/02-development-engineering/iip_claim_intake_workflow_transition_service.md`
**Owns:** **new files only** in the existing `Application/Claims/` folder: `ClaimIntakeContracts.cs`, `ClaimIntakeService.cs`, `ClaimWorkflowTransitionContracts.cs`, `ClaimWorkflowTransitionService.cs`
**Must not touch:** the existing `ClaimsQueryContracts.cs` / `ClaimsQueryService.cs` in the same folder
**Prompt:**
> Read `docs/02-development-engineering/iip_claim_intake_workflow_transition_service.md`. It defines `IClaimIntakeService` and `IClaimWorkflowTransitionService`. The `Application/Claims/` folder already contains `ClaimsQueryContracts.cs` and `ClaimsQueryService.cs` — do NOT edit those two files. Add four new files in the same folder: `ClaimIntakeContracts.cs`, `ClaimIntakeService.cs`, `ClaimWorkflowTransitionContracts.cs`, `ClaimWorkflowTransitionService.cs`. Follow the FNOL intake and claim-status-transition rules exactly as specified (13-state status model per the data model doc if referenced). Add new test files only.

### W3.2 — SLA risk service [Codex, parallel-safe with W3.1]
**Governing doc:** `iip_sla_risk_service_implementation_output.md`
**Owns:** new folder `Application/Sla/`
**Prompt:**
> Read `docs/02-development-engineering/iip_sla_risk_service_implementation_output.md`. Implement `ISlaRiskService` in a new `src/InsuranceIntelligence.Application/Sla/` folder (contracts + service files, `Application/Claims/` pattern). This is independent of any other in-flight Claims work — do not touch `Application/Claims/` at all. Add a new test file only.

### W3.3 — Composition-root wiring pass #3 + Claims controller additions [Claude, SOLO, after W3.1+W3.2]
**Owns:** shared DI file, `Api/Claims/ClaimsController.cs` (existing file — additive edit only)
**Prompt:**
> Register `IClaimIntakeService`, `IClaimWorkflowTransitionService`, and `ISlaRiskService` in DI. Add new endpoint methods to the existing `src/InsuranceIntelligence.Api/Claims/ClaimsController.cs` for intake/transition/SLA-risk (additive only — do not restructure the existing file). Register new `/api/v1/...` routes in `Program.cs` per `docs/02-development-engineering/iip_api_specification.md`. Solo task. Run `dotnet build` + `dotnet test`.

### W3.4 — Claims screens build-out [Claude]
**Governing docs:** `iip_frontend_implementation_guide.md`, `iip_ui_ux_handoff_document.md` (§ Claims Work Queue, FNOL Intake, Case Review)
**Owns:** `web/portal/app/claims/*`, `web/portal/app/claims/[caseId]/*`
**Depends on:** W3.3
**Prompt:**
> Read the Claims Work Queue, FNOL Intake, and Case Review screen specs in `docs/02-development-engineering/iip_ui_ux_handoff_document.md` and the MVP build order in `iip_frontend_implementation_guide.md`. Wire `web/portal/app/claims/*` and `web/portal/app/claims/[caseId]/*` to the new intake/transition/SLA-risk endpoints (just registered). Touch only these folders. Run `pnpm build && pnpm lint`. **Before considering this task done: start the app locally, navigate to the Claims Work Queue and a Case Review screen, capture a live screenshot of each, and present them to the user for approval. Do not proceed to other screens until approved or told what to change.**

**Gate:** Full regression green. Manual smoke: create claim (intake) → transition status → SLA-risk flag appears correctly.

---

## PHASE 4 — Document Intelligence / AI Review

### W4.1 — Document upload service [Codex]
**Governing doc:** `iip_document_upload_service_implementation_output.md`
**Owns:** new folder `Application/Documents/` — create `DocumentUploadContracts.cs`, `DocumentUploadService.cs` first
**Prompt:**
> Read `docs/02-development-engineering/iip_document_upload_service_implementation_output.md`. Implement `IDocumentUploadService` in a new `src/InsuranceIntelligence.Application/Documents/` folder — create only `DocumentUploadContracts.cs` and `DocumentUploadService.cs` in this task (a follow-on task adds the extraction workflow service to this same folder afterward — do not start that work). Add a new test file only.

### W4.2 — Document extraction workflow service [Codex, sequenced strictly after W4.1]
**Governing doc:** `iip_document_extraction_workflow_service_implementation_output.md`
**Owns:** same `Application/Documents/` folder — add `DocumentExtractionWorkflowContracts.cs`, `DocumentExtractionWorkflowService.cs`
**Depends on:** W4.1 must be fully merged first (same folder, sequenced not parallel)
**Prompt:**
> Confirm `DocumentUploadContracts.cs` and `DocumentUploadService.cs` already exist in `src/InsuranceIntelligence.Application/Documents/` before starting (if not, wait). Read `docs/02-development-engineering/iip_document_extraction_workflow_service_implementation_output.md` and add `DocumentExtractionWorkflowContracts.cs` + `DocumentExtractionWorkflowService.cs` to the same folder. Do not edit the upload-service files. Add a new test file only.

### W4.3 — Completeness check service [Codex, parallel-safe, separate folder]
**Governing doc:** `iip_completeness_check_service_implementation_output.md`
**Owns:** new folder `Application/Completeness/`
**Prompt:**
> Read `docs/02-development-engineering/iip_completeness_check_service_implementation_output.md`. Implement `ICompletenessCheckService` in a new `src/InsuranceIntelligence.Application/Completeness/` folder. Fully independent of the Documents-folder work — do not touch `Application/Documents/`. Add a new test file only.

### W4.4 — Document review service [Codex, parallel-safe, separate folder]
**Governing doc:** `iip_document_review_service_implementation_output.md`
**Owns:** new folder `Application/DocumentReview/`
**Prompt:**
> Read `docs/02-development-engineering/iip_document_review_service_implementation_output.md`. Implement `IDocumentReviewService` in a new `src/InsuranceIntelligence.Application/DocumentReview/` folder. Do not touch `Application/Documents/` or `Application/Completeness/`. Add a new test file only.

### W4.5 — Approval decision + query services [Codex, parallel-safe, separate folder, bundled]
**Governing docs:** `iip_approval_decision_service_implementation_output.md`, `iip_approval_query_service_implementation_output.md`
**Owns:** new folder `Application/Approvals/` (both services bundled into one task/session to avoid a second agent touching this folder mid-stream)
**Prompt:**
> Read both `docs/02-development-engineering/iip_approval_decision_service_implementation_output.md` and `iip_approval_query_service_implementation_output.md`. Implement `IApprovalDecisionService` and `IApprovalQueryService` together in a new `src/InsuranceIntelligence.Application/Approvals/` folder (they share the same bounded context — build both in this one task so no other session needs to touch this folder). `ApprovalStatusValues` canonical constants belong here per the reconciliation-pass doc — define them now since this is the service that needs them. Add new test files only.

### W4.6 — Document-to-decision orchestrator [Claude, after W4.1–W4.5 land]
**Governing doc:** `docs/02-development-engineering/iip_multi_agent_orchestration_document_to_decision_pipeline_implementation.md`
**Owns:** new folder `Application/Orchestration/`
**Depends on:** W4.1–W4.5
**Prompt:**
> Read `docs/02-development-engineering/iip_multi_agent_orchestration_document_to_decision_pipeline_implementation.md`. Build the orchestrator in a new `src/InsuranceIntelligence.Application/Orchestration/` folder, wrapping the existing services from Documents/Completeness/DocumentReview/Approvals (Extraction → Completeness → Triage → Draft, sequential). Hard constraint from the doc: the orchestrator must NEVER call `DecideAsync`, `SendApprovedDraftAsync`, or `ExecuteAsync` directly on any downstream service — it only sequences read/prepare steps and hands off to human-gated approval. Every step must write its own `AgentRun` + `AuditEvent` per `docs/03-design-architecture/iip_ai_agent_architecture_tool_registry_updated.md`'s governance table. This requires architectural judgment about pipeline sequencing, not mechanical scaffolding — read all 5 upstream services' contracts fully before writing the orchestrator.

### W4.7 — Composition-root wiring pass #4 [Claude, SOLO, after W4.6]
**Prompt:**
> Register all Phase 4 services (Documents, Completeness, DocumentReview, Approvals, Orchestration) in DI and map their `/api/v1/...` routes in `Program.cs`. Solo task. Run `dotnet build` + `dotnet test`.

### W4.8 — Documents/Compliance/Approvals screens [Claude]
**Owns:** `web/portal/app/document-checker/*`, `app/compliance/*`, `app/approvals/*`
**Depends on:** W4.7
**Prompt:**
> Read the Document Checker, Compliance & Audit, and Approvals screen specs in `docs/02-development-engineering/iip_ui_ux_handoff_document.md`. Wire `web/portal/app/document-checker/*`, `app/compliance/*`, `app/approvals/*` to the new real endpoints. Touch only these three folders. Run `pnpm build && pnpm lint`. **Before considering this task done: start the app locally, navigate to all three screens, capture a live screenshot of each, and present them to the user for approval. Do not proceed to other screens until approved or told what to change.**

**Gate:** Full regression green. Manual smoke: upload document → completeness check runs → approval queue populated → decision recorded → confirm `AgentRun`/`AuditEvent` rows created for every AI step (fail-closed check).

---

## PHASE 5 — Integrations & Resilience

### W5.1 — Policy admin write-back service [Codex]
**Governing doc:** `iip_integration_write_back_execution_service.md`
**Owns:** new folder `Application/PolicyAdmin/`
**Prompt:**
> Read `docs/02-development-engineering/iip_integration_write_back_execution_service.md`. Implement `IPolicyAdminWriteBackService` in a new `src/InsuranceIntelligence.Application/PolicyAdmin/` folder. Write-back must require an existing approval per the doc — do not build an autonomous write path. Add a new test file only.

### W5.2 — Notification send service [Codex]
**Governing doc:** `iip_notification_send_execution_service.md`
**Owns:** new folder `Application/Notifications/`
**Must not touch:** `Infrastructure/Notifications/` (existing provider plumbing — read only; if it needs edits, that's a separate follow-on task, not bundled here)
**Prompt:**
> Read `docs/02-development-engineering/iip_notification_send_execution_service.md`. Implement `INotificationSendService` in a new `src/InsuranceIntelligence.Application/Notifications/` folder. It extends the single `NotificationDrafts` aggregate table per the reconciliation-pass doc's resolved ownership note — do not create a second drafts table. You may read `src/InsuranceIntelligence.Infrastructure/Notifications/` for existing provider wiring, but do not edit it; if a change there turns out to be required, stop and report rather than editing. Add `EmailProviderValues` canonical constants here per the reconciliation-pass doc. Add a new test file only.

### W5.3 — Knowledge base ingestion service [Codex]
**Governing doc:** `iip_knowledge_base_indexing_ingestion_service.md`
**Owns:** new folder `Application/Knowledge/`
**Prompt:**
> Read `docs/02-development-engineering/iip_knowledge_base_indexing_ingestion_service.md`. Implement `IKnowledgeIngestionService` in a new `src/InsuranceIntelligence.Application/Knowledge/` folder, including the sensitivity classifier and approval-before-index rule. Add `KnowledgeApprovalStatusValues` canonical constants here per the reconciliation-pass doc. Add a new test file only.

### W5.4 — Composition-root wiring pass #5 [Claude, SOLO]
**Prompt:**
> Register the Phase 5 services (PolicyAdmin, Notifications, Knowledge) in DI and map routes in `Program.cs`. Solo task. Run `dotnet build` + `dotnet test`.

### W5.5 — Infra evolution step (only after W0.1 ADR locked) [Claude, infra-only]
**Governing doc:** `infra/AZURE_TARGET_ARCHITECTURE.md` (its 9-step evolution order)
**Owns:** `infra/mainTemplate.bicep`, `infra/modules/*` — extend, don't restructure
**Depends on:** W0.1
**Prompt:**
> Confirm `infra/ADR-0001-hosting-model.md` exists and locks the App Service model before starting. Read `infra/AZURE_TARGET_ARCHITECTURE.md`'s 9-step evolution order and extend `infra/mainTemplate.bicep`/`infra/modules/` for the Service Bus queues needed by the Phase 5 services just built (`document-extraction-requests`, `knowledge-ingestion-requests`, `notification-send-requests`, `integration-writeback-requests` — check which already exist in the current Bicep before adding). Do not restructure existing modules — extend additively. Run `az bicep build infra/mainTemplate.bicep` to validate syntax (not a live deploy).

### W5.6 — Integrations screen [Claude]
**Owns:** `web/portal/app/integrations/*`
**Prompt:**
> Wire `web/portal/app/integrations/*` to the existing `IProviderHealthCheck`-backed status endpoints (already built) plus any new integration endpoints from Phase 5. Touch only this folder. Run `pnpm build && pnpm lint`. **Before considering this task done: start the app locally, navigate to the Integrations screen, capture a live screenshot, and present it to the user for approval. Do not proceed to other work until approved or told what to change.**

**Gate:** Full regression green. `az bicep build` succeeds.

---

## PHASE 6 — Security, Compliance, Observability

### W6.1 — Reporting service [Codex]
**Governing doc:** `iip_reporting_service.md`
**Owns:** new folder `Application/Reporting/`, `Api/Reports/` additions
**Prompt:**
> Read `docs/02-development-engineering/iip_reporting_service.md`. Implement `IReportingService` in a new `src/InsuranceIntelligence.Application/Reporting/` folder, plus endpoint additions in `src/InsuranceIntelligence.Api/Reports/` (check if a `ReportsController.cs`/`ReportsEndpoints.cs` already exists — add to it if so, create if not). Add a new test file only.

### W6.2 — Cross-cutting authz/tenant-isolation review [Claude, review task]
**Governing doc:** `docs/02-development-engineering/iip_multi_tenant_identity_onboarding_app_roles_scim_provisioning.md`
**Owns:** read-only review across Phases 2–5's new code; small targeted fixes only, no new folders
**Prompt:**
> Read `docs/02-development-engineering/iip_multi_tenant_identity_onboarding_app_roles_scim_provisioning.md`, especially the rule that Entra tenant ID and Azure subscription ID must never be conflated, and that runtime authorization always flows through `AuthenticatedTenantResolver` + `IPermissionReadRepository`, never Entra app roles directly. Review every service added in Phases 2–5 (`Application/Channels`, `Customers`, `SalesLeads`, `Audit`, `Sla`, `Documents`, `Completeness`, `DocumentReview`, `Approvals`, `PolicyAdmin`, `Notifications`, `Knowledge`) for compliance with this rule. Make small, targeted fixes directly where violations are found; do not create new folders or restructure services. Report every fix made.

### W6.3 — Observability wiring [Claude]
**Governing doc:** `docs/03-design-architecture/iip_observability_operations_architecture.md`
**Owns:** new telemetry helper files in `Infrastructure/`
**Prompt:**
> Read `docs/03-design-architecture/iip_observability_operations_architecture.md`. Add App Insights custom-event telemetry for `AgentRun`/`AuditEvent` creation (Log Analytics/App Insights are already provisioned in `infra/mainTemplate.bicep`). Add new files under `src/InsuranceIntelligence.Infrastructure/` for the telemetry helpers — do not modify existing provider files, only add and call into them where AgentRun/AuditEvent are written.

### W6.4 — Reports/Admin/Compliance screen polish [Claude]
**Governing doc:** `docs/03-design-architecture/iip_design_system_ux_behavior_specification.md`
**Owns:** `web/portal/app/reports/*`, `app/admin-settings/*`, `app/compliance/*` (final polish only, already wired in earlier phases)
**Prompt:**
> Read `docs/03-design-architecture/iip_design_system_ux_behavior_specification.md` for the design tokens and density rules. Apply final visual polish to `web/portal/app/reports/*`, `app/admin-settings/*`, `app/compliance/*` — these were functionally wired in earlier phases; this pass is tokens/spacing/accessibility only, no new data-fetching logic. Run `pnpm build && pnpm lint`. **Before considering this task done: start the app locally, navigate to all three screens, capture a live screenshot of each, and present them to the user for approval. Do not proceed to other work until approved or told what to change.**

**Gate:** New security test suite green; manual cross-tenant-isolation smoke test (confirm access denied across tenants); accessibility check on polished screens.

---

## PHASE 7 — QA / UAT / Pilot Readiness

### W7.1 — Bulk test-coverage fill-in [Codex]
**Owns:** new test files only, one per Phase 2–6 service not yet covered
**Prompt:**
> Audit `tests/InsuranceIntelligence.Api.Tests/` and `tests/InsuranceIntelligence.Domain.Tests/` against every service built in Phases 2–6. For any service with thin or missing test coverage, add a new test file (never edit an existing one) covering its core contract behavior, permission checks, and audit-event creation.

### W7.2 — Command Center end-to-end scenario walkthroughs [Claude]
**Governing doc:** `docs/02-development-engineering/iip_command_center_query_service_updated_logic.md`
**Prompt:**
> Read `docs/02-development-engineering/iip_command_center_query_service_updated_logic.md`. Manually or via Playwright, walk through representative chat-first Command Center queries covering all 12 block types (text, metric_pills, claims_table, sla_summary, document_gap_summary, approval_queue, draft_communication, management_summary, action_proposal, evidence_list, not_configured, error) now that Phases 2–6's real services back them. Report any block that doesn't render correctly or returns stale/placeholder data.

### W7.3 — UAT script authoring [Claude]
**Governing docs:** `docs/02-development-engineering/iip_user_admin_guide.md`, the role/UX matrix in `iip_ui_ux_handoff_document.md`
**Owns:** new file, e.g. `docs/07-qa/uat-script.md`
**Prompt:**
> Read `docs/02-development-engineering/iip_user_admin_guide.md` and the 8-role UX matrix in `iip_ui_ux_handoff_document.md`. Write a UAT script (`docs/07-qa/uat-script.md`) covering each of the 8 roles against each of the now-built screens, with pass/fail criteria per the acceptance criteria already defined in the per-screen specs.

**Gate:** Full `dotnet test` + `pnpm build`/Playwright suites green; UAT checklist executed against a locally hosted build; zero P0/P1 defects open.

---

## PHASE 8 — Pilot Launch

### W8.1 — Finalize pilot deployment [Claude, infra, solo]
**Governing doc:** `docs/05-deployment-release-enablement/iip_deployment_plan.md`
**Prompt:**
> Read `docs/05-deployment-release-enablement/iip_deployment_plan.md`. Finalize deployment per Deployment Model B (Enterprise Private Deployment — recommended for first pilot). Set the South Africa North region parameter in `infra/parameters/`. Walk through the plan's 5 release gates (Demo, Staging, UAT, Pilot, Production) and confirm each is satisfied or explicitly flagged as an open item for `docs/06-decisions/open-questions-tracker.md`.

### W8.2 — Close ready Open Questions [Human + Claude]
**Prompt:**
> Review `docs/06-decisions/open-questions-tracker.md`. For any of the 20 tracked items (OQ-001–010, D1–D10) that now have a human-provided answer, update the tracker with the decision and note any resulting engineering follow-up (e.g., if a specific WhatsApp provider is now chosen, file it as a Phase 5 follow-on task). Items still open remain on "Not Configured" defaults — this does not block pilot launch per the docs' own guidance.

**Gate:** Staging deploy succeeds via the 8-stage CI/CD flow; smoke tests green in staging; all 5 release-gate sign-offs complete.

---

## PHASE 9 — Post-Pilot Expansion / Marketplace

### W9.1 — Reassess hosting-model ADR [Claude]
**Prompt:**
> Now that pilot usage data exists, re-read `infra/ADR-0001-hosting-model.md` and `docs/05-deployment-release-enablement/iip_delivery_tooling_openapi_iac_testdata_cicd.md`'s Container Apps spec. Decide whether to migrate, and only then act — this was deliberately deferred in Phase 0.

### W9.2 — Marketplace/Partner Center packaging finalization [Claude, then Codex for mechanical validation]
**Governing docs:** `offer-metadata.md`, `createUiDefinition.json`, `viewDefinition.json`
**Prompt:**
> Read `offer-metadata.md`, `createUiDefinition.json`, `viewDefinition.json` — these are already shaped for Azure Managed Application submission. Validate `createUiDefinition.json` against the current `infra/mainTemplate.bicep` parameters (they may have drifted since Phase 0–8 added new params). Complete the "Release Candidate Evidence" checklist in `offer-metadata.md` (package name+commit SHA, `az bicep build` result, parameter-validation result, secret scan, build/test results, clean test-deployment result, smoke test, rollback package). This is the only phase where these three files are in scope for editing.

**Gate:** Partner Center submission checklist fully green; `az bicep build` + createUiDefinition validation both pass.

---

## INITIATIVE LP — Lovable Design & Workflow Parity

> **Numbering note:** this is a **cross-cutting initiative**, not a sequential phase — its work items use the `LP.*` prefix (not `W#.#`) precisely so it does not collide with the existing PHASE 0–9 numbering (in particular the pre-existing PHASE 3 = Claims Workflow MVP). It can run against the current codebase state and interleaves with the phases above by the same file-ownership rules.

**Context.** The product owner designed a full reference prototype in Lovable (`Intelligent Operations Hub`) that is the desired UI/UX, workflow, and logic direction for IIP Operations. This initiative brings the real repo to full parity with that reference on IIP's own stack (Next.js/.NET, not Vite/shadcn — port the pattern, not the library). Governing decisions: `docs/01-strategy-requirements-roadmap/iip_product_decision_log.md` DEC-011…DEC-014. Nothing from the reference is mocked-and-left — where a feature was mock in Lovable, the real backend logic is implemented so it is genuinely functional. Full plan of record: the approved plan file for this initiative (`twinkly-hatching-hippo`).

**Gate mapping (AGENTS.md Production Release Program).** All LP work sits inside **Gate 0** (secure baseline, UI/backend alignment, real API contract map). Everything built is real and needs no live external provider to function; the only provider-dependent seams (live WhatsApp/SMS ingestion, any future Azure OpenAI narration for Fraud/Underwriting) are built up to the provider boundary and gated behind the existing `Not configured` honesty pattern until **Gate 3** activates real providers. New config/secret needs are forward-referenced into the Gate-1/Gate-3 env-vars and packaging guides as they arise.

### LP.0 — Branch + documentation foundation [Claude, solo, first]
**Governing docs:** `iip_design_system_ux_behavior_specification.md`, `iip_ui_ux_handoff_document.md`, `iip_product_decision_log.md`, `docs/06-decisions/open-questions-tracker.md`
**Owns:** those doc files + new ADR(s) under `infra/` or `docs/06-decisions/`; branch `feature/lovable-design-parity`
**Prompt:**
> On branch `feature/lovable-design-parity`: bump the design-system spec to the Ambient Intelligence theme (Lovable-derived tokens), add new Section-9 screen specs to the UI/UX handoff doc for Channels Inbox (threaded + actions), Customer 360 detail, Communications hub, Documents hub, Fraud Intelligence, Underwriting Intelligence, AI Agent Registry, Billing, and Workspaces, log DEC-011…DEC-014 in the product decision log, and add an ADR for the multi-agent orchestration / kill-switch boundary. Docs land before code.

### LP.1 — Design tokens & shell skin [Claude]
**Owns:** `web/portal/app/globals.css`, `web/portal/tailwind.config.ts`, `web/portal/components/{AppShell,SidebarNav,TopBar,RightCopilotPanel}.tsx`, shared card primitives (`States`, `Badges`, `SectionCard`, `PageHeader`)
**Prompt:**
> Port the Ambient Intelligence tokens/utilities (`.glass*`, `.ambient-bg`, `.hover-lift`, gradient/shadow scales) into `globals.css`/`tailwind.config.ts` additively (extend, don't rename existing tokens), then apply the glass/ambient treatment to shell chrome and shared card primitives keeping all prop APIs and permission/collapse logic unchanged. Visual-approval checkpoint required before proceeding.

### LP.2 — Existing-page parity [Claude, one screen folder at a time]
**Owns (sequence, never concurrent within this list):** `web/portal/app/channels/*`, `web/portal/app/customers/*` (+ new `customers/[customerId]/`), `web/portal/app/customer-service/*` (Communications), new `web/portal/app/documents/*`, `web/portal/app/support-health/*`, plus `TopBar` workspace switcher
**Backend it depends on:** LP.6 status-enum/endpoint additions (Channels actions, Documents status + retry, Communications delivery-status enum, Customer 360 summary field)
**Prompt (per screen):**
> Bring `<screen>` to workflow+logic parity with the Lovable reference, reusing the existing governance primitives (`GovernedActionDialog`, `ActionStateContract`, `States`). For Channels, first fix the tenant-context gap (route group never calls `ITenantContextSetter.SetCurrent`) before adding create-claim/create-lead/draft-response/escalate actions. Visual-approval checkpoint per screen.

### LP.3 — AI Agent Registry + kill switch [Codex backend, Claude UI + DI]
**Owns:** new `Application/Agents/`, `Api/Admin/Agents/`, EF read/write repos; new `web/portal/app/admin/agents/*`
**Prompt:**
> Investigate reusing `Domain/Agents/AgentContracts.cs` (legacy, unwired) first. Build real agent-definition/run-log/kill-switch entities + query/command services following the Phase-2 pattern; kill switch is a governed, audited admin control enforced at agent-invocation time — never an autonomous AI action. New `/admin/agents` UI (registry table + detail drawer + kill-switch panel).

### LP.4 — Fraud Intelligence (rule-based v1) [Codex backend, Claude UI + DI]
**Owns:** new `Domain/Fraud/`, `Application/Fraud/`, `Api/Fraud/`; new `web/portal/app/fraud-intelligence/*`
**Prompt:**
> Build a deterministic rule engine over real case/customer data (duplicate-claimant/policy patterns, claim velocity, missing-doc-to-payout ratio) producing `AdvisorySignal` records with linked evidence — never a "confirmed fraud" verdict (AGENTS.md AI-behavior boundary). Unit-test each rule (fixed input → expected signal). UI reuses Command Center signal-card patterns. Note the rule-based-v1 / LLM-narration-later decision (DEC-014) in the doc.

### LP.5 — Underwriting Intelligence (rule-based v1) [Codex backend, Claude UI + DI]
**Owns:** new `Domain/Underwriting/`, `Application/Underwriting/`, `Api/Underwriting/`; new `web/portal/app/underwriting/*`
**Prompt:**
> Build a deterministic rule engine (product-line/coverage/limit checks, missing-information detection, drafts clarifying questions) — never approves/rejects/binds. Same pattern, tests, and UI approach as LP.4.

### LP.6 — Billing & entitlements + supporting backend deltas [Codex backend, Claude DI]
**Owns:** new `Domain/Billing/`, `Application/Billing/`, `Api/Billing/`; status-enum/endpoint deltas for Documents (`ReviewRequired`/`Reviewed`/`Rejected` + retry-extraction), Communications (`DeliveryStatus` enum), Channels (write actions), Customer 360 (AI-summary field); new `web/portal/app/admin/billing/*`
**Prompt:**
> Build real entitlement/seat/usage-meter domain wired to actual internal counters the app already emits (AI calls, docs processed, channels active, storage) — computed, never fabricated; overage = real usage vs. real limit. Any external metering provider is gated behind `Not configured`. Add the Documents/Communications/Channels/Customer-360 backend deltas that LP.2 depends on, following the Phase-2 query-service pattern.

### LP.7 — Composition-root wiring + validation [Claude, SOLO, last]
**Prompt:**
> Register all new LP services/repos in DI + map routes in `Program.cs` (solo task, nothing else active). Run `dotnet build`/`dotnet test` (new fixed-input rule-engine + kill-switch-enforcement tests) and `pnpm lint`/`build`; update Playwright visual baselines (`test:visual:update`) as an intentional diff. Completion report per CLAUDE.md; release-readiness stays NO on all three flags (this initiative doesn't touch identity/staging/packaging).

**Gate:** `dotnet test` + `pnpm build`/`lint` green; every new `Not configured` seam fails safely; Fraud/Underwriting language never reads as a verdict across a two-role manual walkthrough.

---

## Verification (applies throughout)

- **Backend gate, every phase:** `dotnet build` (whole solution, 0 errors) + `dotnet test` (all projects — the count grows every phase, never shrinks).
- **Frontend gate, every phase touching web/portal:** `pnpm build && pnpm lint` in `web/portal/`.
- **Infra gate, whenever infra/ changes:** `az bicep build infra/mainTemplate.bicep` (syntax/param validation — not a live deploy) as the standing check; a live deploy only happens in Phase 8.
- **Never skip a phase gate to start the next phase** — even if moving quickly per the user's "if initial phases are completed quickly, move directly to next" instruction, the gate itself must still pass first.
