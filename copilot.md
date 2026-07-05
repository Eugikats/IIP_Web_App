# Insurance Intelligence Platform (IIP) — GitHub Copilot Execution Instructions

> Note: For repository-wide Copilot custom instructions, this file may also be placed at `.github/copilot-instructions.md`. This root `copilot.md` is the human- and tooling-readable authority pointer. It mirrors `CLAUDE.md` and is subordinate to `AGENTS.md`.

## Authoritative contract

Read `AGENTS.md` and `IIP_IMPLEMENTATION_GUARDRAILS.md` fully before suggesting or generating any change.

- `AGENTS.md` is the authoritative product, architecture, security, deployment, UI, and production release contract for this repository.
- `IIP_IMPLEMENTATION_GUARDRAILS.md` is the shared implementation guardrail contract for Claude, Codex, GitHub Copilot, and any other coding or automation agent.
- `README.md`, `CONTRIBUTING.md`, `CLAUDE.md`, and relevant documentation under `docs/` provide supporting context, but they do not override `AGENTS.md`.

If any prior plan, prior prompt, old UI instruction, development-mode instruction, mock-first instruction, generated suggestion, or documentation conflicts with `AGENTS.md`, follow `AGENTS.md`.

## Product naming

Use the current official naming consistently:

- Formal/exported product name: **Insurance Intelligence Platform (IIP)**
- Short product name: **IIP**
- Staff application name: **IIP Operations**

Do not use `ACIP` as the official product name. Treat older `ACIP` references as legacy naming unless a historical note explicitly requires them.

Keep implementation identifiers unchanged unless a human maintainer explicitly requests a codebase refactor. Examples that may remain unchanged include:

- `InsuranceIntelligence.sln`
- `src/InsuranceIntelligence.Api`
- `InsuranceIntelligence.Infrastructure`
- `INSURANCEINTELLIGENCE_*` environment variables

## Documentation library awareness

When a task touches strategy, requirements, roadmap, architecture, engineering execution, testing, deployment, pilot delivery, support, continuous improvement, or commercial enablement, consult the relevant IIP documentation pack before generating changes.

Relevant documentation packs include:

- Strategy, Requirements & Roadmap Pack
- Development & Engineering Execution Pack
- Design & Architecture Deep-Dive Pack
- Testing, Security & Quality Execution Pack
- Deployment, Release & Client Enablement Pack
- Client Pilot Pack
- Client Support Pack
- Continuous Improvement Pack
- Commercial & Executive Pack

Use these documents to preserve product direction, implementation sequencing, acceptance criteria, release gates, security controls, pilot readiness, and support/improvement practices. Do not invent new requirements when the documentation already defines the intended behavior.

## Current mission

The platform is moving to real production deployment.

Execute the **Production Release Program** defined in `AGENTS.md` Section 31, beginning at the first incomplete gate. The foundational local implementation phases are complete or superseded. Do not generate new mock implementation phases and do not skip gates to deploy faster.

## Zero-simulation rule — AGENTS.md Section 0A, highest priority

Test doubles may exist **only** inside automated tests or isolated `LocalTest` execution.

No mock, simulated, fake, placeholder, in-memory, SQLite, DevelopmentAuth, demo-header, fake-ingestion, fake-send, fake-dashboard, seeded-demo, hardcoded-insurer, or fallback-to-mock behavior may run in deployed **Staging**, **Production**, **customer**, or **ManagedApplicationReviewer / Partner Center** environments.

When Copilot is asked to add a capability that lacks a real provider, do not generate a fake success path. Mark the capability `Not configured` / unavailable and surface the manual setup requirement.

### Required real production runtime providers

| Capability | Required deployed implementation | Forbidden in deployed runtime |
|---|---|---|
| Authentication | Microsoft Entra ID | Development Auth, demo headers, browser-selected role |
| Authorization | Server-validated claims/app roles + tenant/workspace enforcement | UI-only checks, arbitrary tenant headers as authority |
| Persistence | Azure SQL Database | SQLite, in-memory repositories |
| Files/documents | Azure Blob Storage | local/mock storage |
| Document extraction | Azure AI Document Intelligence | mock/placeholder extraction |
| Knowledge retrieval | Azure AI Search | in-memory/mock retrieval |
| AI generation/embeddings | Azure OpenAI / approved Foundry | mock assistant, canned responses |
| Workflow queues | Azure Service Bus | mock/in-process queue |
| Outlook/email | Microsoft Graph + tenant-configurable mailboxes | simulated email, fixed global mailbox |
| Secrets | Key Vault references + managed identities | secrets in source/frontend/appsettings/docs |
| Monitoring | Application Insights + Log Analytics | placeholder-only health/telemetry |

Startup/readiness must fail closed in `Staging`, `Production`, and `ManagedApplicationReviewer` if any provider resolves to `Mock`, `Simulated`, `Placeholder`, `InMemory`, `SQLite`, `DevelopmentAuth`, `Demo`, `SeededDemoBusinessData`, or `FallbackToMock`.

No silent fallback from a real provider to a fake one is allowed.

## Non-negotiable product guardrails

Never generate code that:

- Implements an autonomous **final** decision for claim denial, repudiation, settlement, high-value payment, policy cancellation, premium change, underwriting acceptance/rejection, fraud accusation, complaint final response, or regulatory filing.
- Presents AI recommendations as human approvals or final decisions.
- Answers policy or coverage questions from model memory. Such answers must retrieve approved tenant knowledge first, apply tenant/workspace filters, cite sources, show confidence where appropriate, and escalate on missing or low-confidence evidence.
- Omits tenant and workspace identifiers from any query, document, index item, case, communication, integration event, usage meter, or audit record.
- Exposes internal risk notes, fraud flags, reserves, internal staff comments, hidden routing rules, prompts, system messages, tokens, secrets, connection strings, or other customers' data to client-facing surfaces.
- Makes the frontend the sole authority for permission or approval checks.
- Sends customer-facing AI-generated communication without human approval.
- Uses unsafe HTML insertion for email bodies, document text, timeline content, communications, customer text, or AI answers.
- Logs raw secrets, tokens, keys, full national IDs, card numbers, medical details, or unnecessary PII.

AI may recommend, summarize, classify, flag, draft, route, and prepare evidence. Humans make consequential insurance decisions.

## Coding principles

Follow the coding principles in `AGENTS.md` Section 14.

- Keep domain logic free of Azure SDK dependencies; put Azure SDK usage behind interfaces in infrastructure/adapters.
- Build thin vertical slices, not disconnected scaffolding.
- Prefer modifying existing paths over creating competing architectures or duplicate shells.
- Add tests with every non-trivial change.
- Do not make external network calls from tests unless explicitly marked integration tests.
- Every public endpoint must have validation, authorization, tenant/workspace enforcement, and auditable outcomes.
- Every AI tool/action must have a typed input/output contract.
- Do not silently swallow exceptions.
- Make failure states visible to operators.
- Make long-running work retryable and idempotent.
- Use feature flags for unfinished modules.
- Build IIP Operations as an operations workspace with embedded AI assistance, not as a generic chatbot.

## UI fidelity and IIP Operations authority

Before changing any frontend page, layout, component, styling token, navigation, AI surface, customer portal surface, integration state, approval workflow, or API-to-UI mapping, read in full (the `docs/ui/*` paths in earlier drafts were never created — use these real docs):

- `docs/03-design-architecture/iip_design_system_ux_behavior_specification.md` (Ambient Intelligence theme, v1.2)
- `docs/02-development-engineering/iip_ui_ux_handoff_document.md`
- `docs/06-decisions/ADR-2026-07-04-lovable-visual-parity-mode.md`
- `docs/06-decisions/lovable-parity-migration-audit.md`

The operator app is the `/app/*` route tree (`web/portal/lovable-pages/*`); `/` is Login. Screens reconcile off dev-only `LovableVisualParity` fixtures onto real `/api/v1` data via `web/portal/lib/lovable-parity/data/*` hooks.

Match the approved **Ambient Intelligence theme** (DEC-011):

- blue→violet gradient family; base ink navy `#0B0F3B`, primary `#2563FF`, AI accent violet `#7C4DFF`;
- soft translucent glass surfaces over an ambient gradient background;
- deep ink-navy typography;
- structured right-side execution or AI insight rail;
- compact operational tables;
- glass/depth shadows and generous rounded cards;
- no unapproved color palette changes outside the Ambient Intelligence tokens;
- no improvised styling direction.

Do not reinterpret, modernize, simplify, or restyle the approved theme without an explicit user-approved specification update.

Connect each screen to real implemented endpoints. Never invent operational records, statuses, metrics, mailboxes, customers, approvals, documents, messages, extraction output, or health state. Show `Not available` or `Not configured` when a capability is not implemented or configured.

## Immediate execution rule

Before generating changes:

1. Inspect `git status`.
2. Read `AGENTS.md`, `IIP_IMPLEMENTATION_GUARDRAILS.md`, `README.md`, and all relevant deployment, security, UI, and pack documents for the task.
3. Identify the first incomplete Production Release Program gate in `AGENTS.md` Section 31.
4. Produce a short execution plan and identify any manual Azure, Microsoft Entra, Microsoft 365, DNS, quota, secret, or Partner Center decisions needed.
5. Implement only the work required for that gate.
6. Do not introduce new product features during production hardening unless expressly requested.
7. Run all required validation.
8. Report blockers requiring manual administrator action and stop at the gate.

## Required validation commands

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

If a command cannot be run, state exactly why and do not claim it passed.

## Production readiness truthfulness

Do not claim the platform is production-ready, deployment-ready, Managed Application-ready, or Partner Center-ready until the live-provider, security, operational, acceptance, and packaging gate evidence in `AGENTS.md` Sections 29 and 31 has passed.

The product may be called **production-candidate** only after live Azure staging tests with synthetic data prove end-to-end behavior.

For deployed environments: **no simulation, no mock runtime providers, no fake success paths, no hardcoded insurer data, and no production-readiness claims without evidence.**
