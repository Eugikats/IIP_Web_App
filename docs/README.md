# IIP documentation

Start here for product, architecture, delivery, research, and operational knowledge. This page is a task-oriented map; use [`CATALOG.md`](CATALOG.md) when you need the exhaustive file inventory.

## Read first

| Need | Source |
|---|---|
| Repository rules and authority | [`../AGENTS.md`](../AGENTS.md) |
| Product intent | [`iip_master_product_context.md`](01-strategy-requirements-roadmap/iip_master_product_context.md) and [`iip_product_requirements_document.md`](01-strategy-requirements-roadmap/iip_product_requirements_document.md) |
| Current programme order | [`IIP-CONSOLIDATED-ROADMAP.md`](08-program-plan/IIP-CONSOLIDATED-ROADMAP.md) |
| System architecture | [`iip_technical_architecture_document.md`](03-design-architecture/iip_technical_architecture_document.md) |
| Security and permissions | [`iip_security_compliance_document.md`](04-testing-security-quality/iip_security_compliance_document.md) and [`iip_rbac_permissions_matrix.md`](04-testing-security-quality/iip_rbac_permissions_matrix.md) |
| Developer setup | [`iip_developer_setup_guide.md`](05-deployment-release-enablement/iip_developer_setup_guide.md) |
| Repository orientation | [`00-start-here/repository-map.md`](00-start-here/repository-map.md) |
| Product language | [`00-start-here/product-glossary.md`](00-start-here/product-glossary.md) |

## Find information by task

| Area | What belongs there |
|---|---|
| [`01-strategy-requirements-roadmap/`](01-strategy-requirements-roadmap/) | Product intent, scope, requirements, assumptions, risks, and prioritization |
| [`02-development-engineering/`](02-development-engineering/) | Implementation guides, service contracts, handoffs, and engineering reports |
| [`03-design-architecture/`](03-design-architecture/) | System, data, workflow, AI, integration, UI, and observability design |
| [`04-testing-security-quality/`](04-testing-security-quality/) | Test strategy, security, RBAC, compliance, resilience, and quality controls |
| [`05-deployment-release-enablement/`](05-deployment-release-enablement/) | Environments, IaC, CI/CD, release, migration, recovery, and marketplace packaging |
| [`06-client-pilot/`](06-client-pilot/) | Pilot preparation, execution, UAT, measurement, and closeout |
| [`06-decisions/`](06-decisions/) | ADRs, approved decisions, decision handoffs, and open decision questions |
| [`07-client-support/`](07-client-support/) | Support operating model, runbooks, incidents, SLAs, and customer guidance |
| [`07-qa/`](07-qa/) | Dated QA evidence and live-test outputs; evidence is not a standing specification |
| [`08-continuous-improvement/`](08-continuous-improvement/) | Feedback, improvement, post-launch, and learning templates |
| [`08-program-plan/`](08-program-plan/) | Current programme order, work maps, release blueprints, and remediation tickets |
| [`09-commercial-executive/`](09-commercial-executive/) | Business case, commercial packaging, proposals, and executive material |
| [`api/`](api/) | OpenAPI and API contract artifacts |
| [`research/`](research/) | Research evidence, synthesis, capability maps, and pending-work registers |
| [`agents/`](agents/) | Agent work coordination and specifications; not product runtime configuration |

## Documentation rules

- Put each document with its owning concern; do not create another catch-all folder.
- Link to an authority instead of copying volatile catalogs or status into multiple files.
- Label time-bound evidence and snapshots with a date and lifecycle state.
- Mark superseded guidance explicitly and link to its replacement.
- Keep generated inventories generated. Run `pwsh scripts/docs/Update-IipDocsCatalog.ps1` from the repository root after adding, moving, or removing documentation.
- Follow [`00-start-here/documentation-governance.md`](00-start-here/documentation-governance.md) for naming, lifecycle, ownership, and archival rules.

## Full inventory

[`CATALOG.md`](CATALOG.md) is generated from the files currently under `docs/`. Do not hand-edit it.
