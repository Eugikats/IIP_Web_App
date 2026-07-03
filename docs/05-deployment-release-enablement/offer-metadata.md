# Marketplace Offer Metadata Draft

> **Related template:** The full packaging procedure is [iip_azure_managed_application_packaging_guide.md](iip_azure_managed_application_packaging_guide.md). This file is the repo-specific release-candidate offer checklist.

Use this as the release-candidate review checklist before private offer, service catalog, or Azure Marketplace submission. Do not place customer data, tenant secrets, app secrets, connection strings, SAS tokens, screenshots with real claims, or local deployment values in offer artifacts.

## Offer

- Offer name: Insurance Intelligence Platform (IIP)
- Category: AI + Machine Learning; Business Applications
- Deployment model: Azure Managed Application, one insurer per managed application deployment
- Target customers: insurance carriers, brokers, TPAs, and regulated insurance operations teams
- Primary value: document-first claims and customer-service operations with human-supervised AI preparation

## Plans

| Plan | Intended Use | Default Size | Notes |
|---|---|---|---|
| Dev | Internal validation and proof-of-concept | `dev` | Mock-first, low-cost defaults, no customer data |
| Pilot | Controlled insurer pilot | `pilot` | Live providers enabled one at a time after validation |
| Production | Customer-facing regulated use | `production` | Entra, durable storage, live providers, alerts, backups, and cost budgets required |

## Support And Legal

- Support contact: replace with approved support email or queue before submission
- Privacy link: replace with approved public privacy URL before submission
- Terms link: replace with approved public terms URL before submission
- Support access modes: `none`, `read_only`, `delegated`, `jit`
- Support access must be least-privileged, auditable, time bounded, and approved by the customer where required

## Approved Regions

Use only approved regions for the target offer. Confirm data residency before enabling production deployments. Default guidance is to deploy into a single approved Azure region per insurer unless the customer has a reviewed DR design.

## Cost Guidance

Estimated cost depends on provider modes, document volume, retention, AI usage, and selected SKU profile. Do not publish firm pricing from this repository alone.

Baseline guidance:

- Dev: Basic App Service, local/mock-first providers, low retention where allowed.
- Pilot: Standard App Service, live provider validation, budget alerts required.
- Production: Premium App Service, production retention, alerts, backups, budget thresholds, and reviewed live provider capacity.

## Release Candidate Evidence

Attach or record:

- Managed app package name and commit SHA.
- `az bicep build` result.
- createUiDefinition-to-Bicep parameter validation result.
- Package secret/local-file scan result.
- Backend Release restore/build/test result.
- Portal frozen install/lint/build result.
- Clean test deployment or what-if result.
- Smoke test result using synthetic data only.
- Rollback package/version.
