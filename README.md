# Insurance Intelligence Platform (IIP)

IIP is an Azure-first, tenant-isolated insurance operations platform. It helps insurer teams retrieve evidence, process documents, prepare operational work, coordinate human review, control external actions, and maintain an auditable record.

AI assists; accountable humans make consequential insurance decisions.

## Repository

```text
src/InsuranceIntelligence.Domain          domain rules and business meaning
src/InsuranceIntelligence.Application     use cases, ports, authorization and audit policy
src/InsuranceIntelligence.Infrastructure  EF, Azure and provider adapters
src/InsuranceIntelligence.Api             HTTP, authentication and composition
src/InsuranceIntelligence.Functions       background and event processing
src/InsuranceIntelligence.LocalTestDoubles isolated test implementations
tests/                                    .NET test projects
web/portal                                Next.js IIP Operations portal
infra/                                    Bicep, scripts and deployment assets
docs/                                     product, architecture, quality and delivery evidence
```

Backend projects target .NET 8. The portal uses Next.js, React, TypeScript and pnpm. Current project membership is defined by `InsuranceIntelligence.sln`; current portal scripts are defined by `web/portal/package.json`.

## Authority and documentation

Coding agents read [`AGENTS.md`](AGENTS.md) first. Its **Authority and current truth** section is the canonical repository authority index.

- Product intent: [`iip_master_product_context.md`](docs/01-strategy-requirements-roadmap/iip_master_product_context.md) and [`iip_product_requirements_document.md`](docs/01-strategy-requirements-roadmap/iip_product_requirements_document.md).
- Current work order: [`IIP-CONSOLIDATED-ROADMAP.md`](docs/08-program-plan/IIP-CONSOLIDATED-ROADMAP.md).
- Documentation index: [`docs/README.md`](docs/README.md).
- Repository map: [`docs/00-start-here/repository-map.md`](docs/00-start-here/repository-map.md).
- Product glossary: [`docs/00-start-here/product-glossary.md`](docs/00-start-here/product-glossary.md).
- Contributor workflow: [`CONTRIBUTING.md`](CONTRIBUTING.md).
- Focused safety reference: [`IIP_IMPLEMENTATION_GUARDRAILS.md`](IIP_IMPLEMENTATION_GUARDRAILS.md).

Supporting files may contain historical context. They do not restore superseded UI, API, workflow, provider, architecture, or release paths.

## Quick validation

```bash
dotnet restore InsuranceIntelligence.sln
dotnet build InsuranceIntelligence.sln --no-restore -c Release
dotnet test InsuranceIntelligence.sln --no-build -c Release --verbosity normal

cd web/portal
pnpm install --frozen-lockfile
pnpm lint
pnpm test
pnpm build
```

Infrastructure compile checks and the full verified command set are maintained in [`AGENTS.md`](AGENTS.md) and [`.github/workflows/ci.yml`](.github/workflows/ci.yml).

## Local development

```bash
# API from repository root; requires valid local configuration
dotnet run --project src/InsuranceIntelligence.Api/InsuranceIntelligence.Api.csproj

# portal from web/portal
pnpm dev
```

Missing real providers must surface as `Not configured`; do not enable protected-runtime mocks to make local or hosted flows appear successful.

## Contribution principles

- Preserve tenant/workspace isolation, server-side authorization, auditability, evidence grounding, privacy, and meaningful human review.
- Implement coherent forward changes. Do not silently fall back to legacy UI, routes, workflows, providers, or delivery processes.
- Keep compatibility paths explicit, observable, tested and time-bounded.
- Update owning docs or ADRs when behavior or architecture changes.
- Report validation and readiness from evidence, not assumption.

See [`CONTRIBUTING.md`](CONTRIBUTING.md) before opening a pull request.
