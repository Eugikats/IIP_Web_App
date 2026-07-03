# Insurance Intelligence Platform (IIP) — v2

Clean v2 repository for the **Insurance Intelligence Platform (IIP)**. This repository
holds the authoritative **documentation pack** that defines the product, architecture,
backend/API, security, deployment, and UI direction. Implementation code is added against
these documents as the source of truth.

## Current implementation status

- Domain, Application, Infrastructure, API, LocalTestDoubles, Functions, Domain.Tests, and
  Api.Tests projects are present and target .NET 8.
- API and Functions build successfully in the v2 repository.
- Domain.Tests and Api.Tests pass locally. Api.Tests were migrated to v2 contracts:
  `/api/v1` routes, multitenant/runtime guardrails, LocalTestDoubles namespace usage, and
  no restoration of the dropped code-only agent framework or superseded prose Command Center
  assistant path.
- Command Center `/api/v1/command-center/query` delegates to an Application-layer
  `CommandCenterQueryService` for deterministic, tenant-scoped orchestration, permission
  checks, and audit/action-log writing. The API endpoint remains a thin request adapter.
- The obsolete-provider warnings in test/build output are intentional guard warnings for
  isolated test doubles and legacy simulation classes; do not silence them without replacing
  the underlying production provider path.

## Read first (authority order)

1. [`AGENTS.md`](AGENTS.md) — top-level product, architecture, security, deployment, UI, and
   production-release contract for the repository.
2. [`IIP_IMPLEMENTATION_GUARDRAILS.md`](IIP_IMPLEMENTATION_GUARDRAILS.md) — shared
   implementation guardrails for every coding/automation agent.
3. [`CLAUDE.md`](CLAUDE.md) — Claude execution instructions.
4. [`copilot.md`](copilot.md) — Copilot execution instructions.
5. [`CONTRIBUTING.md`](CONTRIBUTING.md) — contribution workflow.

If any older plan, prompt, or document conflicts with `AGENTS.md`, follow `AGENTS.md`.

## Documentation packs

All product documentation lives under [`docs/`](docs/), grouped into the canonical packs:

| Pack | Folder | Focus |
| --- | --- | --- |
| Strategy, Requirements & Roadmap | [`docs/01-strategy-requirements-roadmap/`](docs/01-strategy-requirements-roadmap/) | Product context, PRD, MVP scope, roadmap, traceability |
| Development & Engineering | [`docs/02-development-engineering/`](docs/02-development-engineering/) | Backend/API implementation specs, service outputs, frontend handoff |
| Design & Architecture | [`docs/03-design-architecture/`](docs/03-design-architecture/) | Technical/component architecture, data model, RAG & agent architecture |
| Testing, Security & Quality | [`docs/04-testing-security-quality/`](docs/04-testing-security-quality/) | QA/test plans, security & compliance, RBAC, red-team evaluation |
| Deployment, Release & Enablement | [`docs/05-deployment-release-enablement/`](docs/05-deployment-release-enablement/) | Deployment plan, managed-app packaging, CI/CD, environment setup |
| Client Pilot | [`docs/06-client-pilot/`](docs/06-client-pilot/) | Pilot charter, launch, UAT, governance, sign-off |
| Client Support | [`docs/07-client-support/`](docs/07-client-support/) | Support operating model, runbooks, SLAs, escalation |
| Continuous Improvement | [`docs/08-continuous-improvement/`](docs/08-continuous-improvement/) | Feedback, improvement backlog, post-launch review |
| Commercial & Executive | [`docs/09-commercial-executive/`](docs/09-commercial-executive/) | Business case, pricing, pitch, objection handling |

See [`docs/README.md`](docs/README.md) for the full per-pack file index.

## Provenance

This documentation pack was imported and de-duplicated from the source authoring folder:
browser re-download duplicates (`… (1).md` / `… (2).md`) were collapsed to a single copy,
keeping the newest content in every case (notably `iip_data_model_document.md`, where the
newer 78KB revision was kept over the stale original). Authority files were restored to their
canonical names (`AGENTS.md`, `CLAUDE.md`).
