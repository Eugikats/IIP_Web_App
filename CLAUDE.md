# Insurance Intelligence Platform (IIP) — Claude Execution Instructions

## Authoritative contract

You are Claude working inside the repository for **Insurance Intelligence Platform (IIP)**.

Staff application name: **IIP Operations**.  
Formal product name: **Insurance Intelligence Platform (IIP)**.  
Short product name: **IIP**.

**Start here:** `docs/CONTEXT.md` — a thin start-here index that points to current-state, backlog, pending-work, and research artifacts. Read it first to orient, then open the deep artifact you need.

Read the following files fully before making any change:

1. `docs/CONTEXT.md` (orientation index)
2. `AGENTS.md`
3. `IIP_IMPLEMENTATION_GUARDRAILS.md`
4. `README.md`
5. Any relevant deployment, security, UI, architecture, API, testing, or production documentation for the task being executed.

`AGENTS.md` is the authoritative product, architecture, security, deployment, UI, and production release contract for this repository.

`IIP_IMPLEMENTATION_GUARDRAILS.md` is the shared implementation guardrail contract for Claude, Codex, and any other coding or automation agent.

If any prior plan, prior prompt, old UI instruction, development-mode instruction, mock-first instruction, or documentation conflicts with `AGENTS.md`, follow `AGENTS.md`.

Do not treat older references to **InsuranceIntelligence Platform** or **ACIP** as the official product name. Use **Insurance Intelligence Platform (IIP)** in product-facing documentation and user-facing language. Keep code-level identifiers unchanged unless a human maintainer explicitly requests a codebase refactor.

---

## Current mission

The platform is moving toward real production deployment and Azure Managed Application/private-plan readiness.

Execute the **Production Release Program** defined in `AGENTS.md`, beginning at the first incomplete gate.

Key rule:

- Test doubles may exist only inside automated tests or isolated `LocalTest` execution.
- No mock, simulated, fake, placeholder, in-memory, SQLite, DevelopmentAuth, demo-header, fake-ingestion, fake-send, fake-dashboard, seeded-demo, fallback-to-mock, or hardcoded-insurer behavior may run in deployed staging, production, customer, or Partner Center reviewer environments.

A capability that is not configured in a deployed environment must show **`Not configured`**, fail safely, or be unavailable. It must never appear successful through simulation.

---

## Immediate execution rule

Before making changes:

1. Inspect `git status`.
2. Read `AGENTS.md`, `IIP_IMPLEMENTATION_GUARDRAILS.md`, `README.md`, and all relevant deployment/security/UI/architecture documents.
3. Identify the first incomplete Production Release Program gate.
4. Produce a short execution plan.
5. Implement only work required for that gate.
6. Run all required validation.
7. Report blockers requiring manual Azure, Microsoft Entra, Microsoft 365, DNS, quota, secret, security, support-access, or Partner Center action.

Do not claim production readiness unless the live-provider, security, operational, acceptance, and packaging evidence required in `AGENTS.md` has passed.

---

## UI implementation authority

Before changing any frontend page, layout, component, style token, navigation behavior, AI surface, approval workflow, customer-safe surface, integration state, or API-to-UI mapping, read the authoritative IIP Operations UI docs in full (the `docs/ui/*` and `docs/security/ui-backend-security-review.md` paths in earlier drafts were never created — use these real docs; security guidance lives in `AGENTS.md` §22 and guardrails §§4/18–19):

- `docs/03-design-architecture/iip_design_system_ux_behavior_specification.md` (Ambient Intelligence theme, v1.2 — supersedes the flat theme)
- `docs/02-development-engineering/iip_ui_ux_handoff_document.md`
- `docs/02-development-engineering/iip_frontend_implementation_guide.md`
- `docs/02-development-engineering/IIP_FRONTEND_ARCHITECTURE_AND_AGENT_HANDOFF.md`
- `docs/06-decisions/ADR-2026-07-04-lovable-visual-parity-mode.md`
- `docs/06-decisions/lovable-parity-migration-audit.md`

Current app structure: the operator app is the `/app/*` route tree (`web/portal/lovable-pages/*` in the `components/iip/AppShell` shell); `/` is Login. Screens are being reconciled off `LovableVisualParity` dev-only fixtures (`NEXT_PUBLIC_VISUAL_PARITY_MODE=true`) onto real `/api/v1` data via `web/portal/lib/lovable-parity/data/*` hooks — see the migration audit for the backlog and the old route tree slated for removal.

The IIP Operations UI must be a premium, calm, enterprise insurance operations workspace with embedded AI assistance. It must not become a generic chatbot-first app.

Do not reinterpret, modernize, restyle, simplify, or replace the approved UI direction without an explicit user-approved specification update.

For deployed environments, do not render:

- fake dashboard statistics;
- hardcoded insurer data;
- demo users;
- mock role switchers;
- simulated send controls;
- fake-ingestion controls;
- unavailable integrations as connected;
- unconfigured services as working;
- AI outputs as final human decisions.

---

## Production runtime rule

In `Staging`, `Production`, and `ManagedApplicationReviewer` profiles, the platform must use real configured providers only.

Required production/reviewer expectations include:

- Microsoft Entra ID authentication;
- server-side authorization and tenant/workspace enforcement;
- Azure SQL for persistence;
- Azure Blob Storage for files/documents;
- Azure AI Document Intelligence for extraction;
- Azure AI Search for retrieval/indexing;
- Azure OpenAI or approved Microsoft Foundry deployments for AI generation/embeddings;
- Azure Service Bus for workflow queues/events;
- Microsoft Graph for Outlook/email where enabled;
- Key Vault and Managed Identity wherever possible;
- Application Insights and Log Analytics for monitoring.

If any required live provider is missing, unhealthy, incorrectly configured, or blocked by manual setup, stop and report the exact blocker. Do not create a fake success path.

---

## Human-supervised insurance rule

IIP uses AI to prepare insurance work for humans to decide.

AI may:

- summarize claims;
- classify documents;
- extract document fields;
- identify missing documents;
- draft customer-safe updates;
- retrieve policy knowledge from approved sources;
- highlight risk signals;
- recommend workflow next steps;
- prepare audit evidence.

AI must not:

- approve or reject claims;
- deny, repudiate, settle, or release payments;
- bind policies;
- set final premiums;
- make final underwriting decisions;
- accuse customers, providers, brokers, or staff of fraud;
- send final regulated communications without required human approval;
- answer policy questions without approved source grounding;
- expose prompts, hidden system messages, internal notes, secrets, confidential staff-only content, or raw provider error payloads.

All consequential insurance decisions must remain human-reviewed, auditable, and traceable.

---

## Documentation awareness

When implementing or changing product behavior, consult the relevant IIP documentation pack where available:

- Strategy, Requirements & Roadmap Pack
- Development & Engineering Execution Pack
- Design & Architecture Deep-Dive Pack
- Testing, Security & Quality Execution Pack
- Deployment, Release & Client Enablement Pack
- Client Pilot Pack
- Client Support Pack
- Continuous Improvement Pack
- Commercial & Executive Pack

Do not create behavior that contradicts the current documentation. If implementation reality and documentation differ, report the mismatch and propose a safe update path.

---

## Working discipline

When executing a task:

1. Work in small gated batches.
2. Prefer modifying existing implementation paths over creating competing architectures.
3. Keep domain logic separate from Azure SDK adapters.
4. Preserve test doubles only in automated tests or isolated `LocalTest` paths.
5. Do not introduce new frameworks, cloud services, packages, or architectural patterns without a clear reason.
6. Add or update tests for every meaningful behavior change.
7. Maintain tenant/workspace isolation.
8. Maintain server-side authorization.
9. Maintain audit logging for important actions.
10. Keep frontend UI backed by real API contracts.
11. Use `Not configured` for missing real services.
12. Never hide real errors with fake success states.
13. Never commit secrets or real customer data.
14. Never claim validation passed unless it actually ran.

---

## Required validation

Run all relevant checks available for the affected area.

Common checks include:

```bash
# backend
dotnet restore InsuranceIntelligence.sln
dotnet build InsuranceIntelligence.sln --no-restore
dotnet test InsuranceIntelligence.sln
dotnet list InsuranceIntelligence.sln package --vulnerable --include-transitive

# portal
cd web/portal
pnpm install --frozen-lockfile
pnpm audit
pnpm lint
pnpm build
cd ../..

# infrastructure
az bicep build --file infra/mainTemplate.bicep --outfile infra/dist/mainTemplate.json
az bicep build-params --file infra/parameters/stg.bicepparam --outfile infra/dist/stg.parameters.json
```

If a command is unavailable, fails because of missing local tooling, or requires manual cloud credentials, report that clearly and do not pretend it passed.

---

## Completion report format

Every completed task must report:

1. Files changed.
2. Routes, APIs, modules, or infrastructure affected.
3. Production Release Program gate addressed.
4. Security impact.
5. Tenant isolation impact.
6. Authentication/authorization impact.
7. Data model or migration impact.
8. AI/human-review impact.
9. UI/UX impact, where relevant.
10. Tests and validation commands run.
11. Results: passed, failed, or not run with reason.
12. Manual actions required.
13. Remaining blockers or deviations.
14. Release readiness status, using only evidence-backed claims.

Release readiness must be stated conservatively:

```text
PRODUCTION-CANDIDATE: YES/NO
MANAGED APPLICATION PACKAGE READY: YES/NO
PARTNER CENTER PRIVATE PLAN READY: YES/NO
```

Do not answer `YES` unless the evidence required in `AGENTS.md` exists and has passed.

---

## Final reminder

The winning IIP product is not the most autonomous product. It is the platform insurers can safely operate in production: real authenticated users, real Azure data and AI services, real tenant-configurable Outlook mailboxes, grounded evidence-backed outputs, human approvals, secure customer communication, auditability, operational monitoring, recoverability, enterprise-grade UI, and repeatable Azure Managed Application deployment.

For deployed environments: **no simulation, no mock runtime providers, no fake success paths, no hardcoded insurer data, and no production-readiness claims without evidence.**
