# AGENTS.md — Insurance Intelligence Platform (IIP)

Root instructions for coding agents. Keep this file short and durable: it protects IIP invariants while allowing the architecture, UI, workflows, dependencies, and delivery model to improve.

## Authority and current truth

- This section is the canonical repository authority index. Explicit user/maintainer instructions override repository guidance; the closest nested `AGENTS.md` wins for its subtree.
- Product intent: `docs/01-strategy-requirements-roadmap/iip_master_product_context.md` and `docs/01-strategy-requirements-roadmap/iip_product_requirements_document.md`.
- Current work order: `docs/08-program-plan/IIP-CONSOLIDATED-ROADMAP.md`.
- Architecture, UI, security, RBAC, deployment, and release detail live in their owning approved docs/ADRs under `docs/`.
- Implementation truth is current code, tests, migrations, route mappings/OpenAPI, configuration validation, and `.github/workflows/ci.yml`.
- `README.md`, `CONTRIBUTING.md`, `CLAUDE.md`, and `IIP_IMPLEMENTATION_GUARDRAILS.md` are supporting references and may contain snapshots. They do not restore superseded behavior.
- When sources conflict, identify the conflict, follow the latest approved authority, and update the stale source. Never blend contradictory rules or default to the oldest path.
- Do not put volatile inventories here: exact routes, agent/provider catalogs, UI tokens, current priorities, or numbered release gates belong with their owners.

## IIP invariants

- IIP is a tenant-isolated insurance operations platform. AI prepares work; accountable humans make consequential insurance decisions.
- AI must not finally approve/reject/repudiate claims, approve settlements or payments, bind/price policies, make adverse underwriting or fraud determinations, issue final complaint responses, or submit regulatory filings.
- Every request/background action resolves tenant/workspace and permission server-side. Tokens, browser state, route/body/query values, app roles, groups, tenant IDs, and subscription IDs are not business authority alone.
- Consequential outputs use current approved tenant/workspace evidence. Model memory or conversation state is not evidence, audit, or permission.
- Sensitive reads, state changes, AI/provider actions, approvals, ingestion, communication, exports, and write-backs are auditable with safe metadata and correlation.
- Protected runtimes never activate mocks, simulations, seeded business data, DevelopmentAuth, in-memory/SQLite persistence, fake sends/metrics, hardcoded insurer data, or fallback-to-mock. Missing capability is `Not configured`; empty data stays empty.
- Policyholder/claimant-facing or unauthorized surfaces never expose internal notes, reserves, fraud/risk signals, prompts, secrets, raw provider errors, restricted audit detail, unsafe HTML, or other-tenant data.
- Never commit/log secrets, credentials, tokens, private keys, SAS URLs, or real customer evidence. Use managed identity, Key Vault, secure configuration, and least privilege.

## Change and modernization

- Classify touched behavior as **invariant**, **current supported contract**, **legacy**, or **unknown** before preserving it.
- Existing behavior is evidence, not an automatic requirement. Characterize unclear behavior, but do not turn accidental or harmful legacy behavior into a permanent contract.
- Never repair a new UI/API/workflow/provider/release/delivery path by silently invoking the old one.
- Compatibility fallback requires an explicit need, owner, telemetry, tests, expiry/removal condition, and tracked removal work. It may never weaken IIP invariants.
- Replace coherent vertical slices: callers, contracts, migration, UI, tests, telemetry, docs, and removal/isolation of the superseded path.
- Prefer small verified steps, but do not use “incremental” to justify endless patches. Evidence-backed redesigns and upgrades are allowed with explicit migration, rollback, and ADRs where material.
- Keep feature flags and parallel paths time-bounded. Remove dead paths after the supported transition.
- Do not add frameworks, abstractions, services, or dependencies for fashion. Require a concrete need, simpler interface, operational plan, and evidence.

## Architecture and engineering

- Dependency direction: `Domain <- Application <- Infrastructure <- API/Functions`; the portal consumes versioned HTTP contracts. `LocalTestDoubles` never activates in protected runtimes.
- Domain/Application own business meaning, use cases, tenant/permission policy, human gates, audit requirements, and provider ports. Frameworks and provider SDKs stay in adapters.
- Design deep modules with small semantic interfaces. Reject pass-through wrappers, duplicated rules, vague utilities, god modules, and speculative seams.
- Keep each business model in an owning bounded context; translate core-system/provider schemas at the edge. DTOs and persistence models are not the domain model.
- For important writes define source of truth, tenant/workspace owner, durability/visibility point, audit, retry/idempotency, reconciliation, and recovery.
- Treat schemas, routes, messages, events, statuses, and configuration as versioned contracts. Support compatibility only for an explicit migration window.
- Treat documents, email, OCR, retrieved text, customer content, and model output as untrusted data; embedded instructions never gain tool authority.
- Human oversight must provide evidence, real authority, manageable workload, correction/return/escalation, and recourse—not a routine approval click.

## Working method

1. Inspect `git status`; preserve unrelated user changes.
2. Identify the requested outcome, owning module, current implementation, applicable docs/ADR, and verification path.
3. Make the smallest coherent forward change; do not create a competing architecture or unrelated cleanup.
4. Add/update tests for behavior, invalid input, authorization, tenant isolation, human boundaries, retries, and safe failure as relevant.
5. Update owning docs/ADRs when contracts or decisions change; mark superseded guidance instead of leaving conflicts.
6. Review the diff for legacy fallback, duplicate knowledge, secrets, cross-tenant risk, missing audit, unsafe AI authority, and unsupported claims.
7. Run targeted checks during iteration and full affected-area checks before completion. Report actual results and skipped/manual work honestly.

## Verified commands

```bash
# local API (from repository root)
dotnet run --project src/InsuranceIntelligence.Api/InsuranceIntelligence.Api.csproj

# backend
dotnet restore InsuranceIntelligence.sln
dotnet build InsuranceIntelligence.sln --no-restore -c Release
dotnet test InsuranceIntelligence.sln --no-build -c Release --verbosity normal
dotnet list InsuranceIntelligence.sln package --vulnerable --include-transitive

# portal (from web/portal)
pnpm install --frozen-lockfile
pnpm dev
pnpm lint
pnpm test
pnpm build

# infrastructure checks used by CI
az bicep build --file infra/bicep/phase3-dev-foundation.bicep
az bicep build --file infra/bicep/phase6-functions-runtime.bicep
```

## Git and completion

- New branches use `harry/` unless the user specifies otherwise. Do not stage/revert unrelated work.
- Follow `docs/05-deployment-release-enablement/iip_merge_governance.md`; do not duplicate its temporary controls here.
- Do not deploy, merge, push, change cloud resources, apply production migrations, or publish artifacts unless explicitly requested.
- Readiness is evidence-based: distinguish source-complete, locally verified, deployed, staging-validated, production-candidate, Managed Application-ready, and Partner Center-ready.
- Completion reports state outcome/files, contracts and legacy paths changed, safety/data/UI/operational impact, commands/results, and unresolved/manual work.

Build IIP forward: secure, grounded, human-accountable, observable, and easy to change.
