# ADR-0001: API/Portal Hosting Model for the MVP Pilot

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-07-03 |
| Deciders | Architecture (per IIP Full-Program Plan, W0.1) |
| Supersedes | — |
| Related | `infra/AZURE_TARGET_ARCHITECTURE.md`, `docs/05-deployment-release-enablement/iip_delivery_tooling_openapi_iac_testdata_cicd.md`, `docs/08-program-plan/IIP-FULL-PROGRAM-PLAN.md` (W0.1) |

## Context

Two documents in the repository specify different Azure hosting models for the IIP API. `infra/AZURE_TARGET_ARCHITECTURE.md` recommends **Azure App Service** for both the Next.js portal and the .NET 8 API (with Azure Functions Premium for Durable workflows), and the working `infra/mainTemplate.bicep` already implements this App Service model. In contrast, `docs/05-deployment-release-enablement/iip_delivery_tooling_openapi_iac_testdata_cicd.md` (§ default API hosting target) specifies **Azure Container Apps** as the API host, with a corresponding Container Apps managed-environment IaC layout. Left unreconciled, different program phases could build against different hosting assumptions, producing infra drift and rework.

## Decision

For the MVP pilot, IIP keeps the **Azure App Service** hosting model for the portal and API, as already implemented in `infra/mainTemplate.bicep` and described in `infra/AZURE_TARGET_ARCHITECTURE.md`. Azure Functions Premium remains the home for Durable/async workflow offload. The Azure Container Apps layout described in the Delivery Tooling document is treated as a **Phase-9+ candidate** to revisit only if post-pilot usage, scaling, or cost data justifies a migration. Until then, no Container Apps infrastructure is introduced, and all Phase 5+ infra work extends the existing App Service–based Bicep additively.

## Reasoning

The App Service model is already built, validated by `az bicep build`, and matches the current code split (a standard ASP.NET Core web app and a `standalone` Next.js build), so it is the shortest credible path to an insurer pilot with the least delivery risk. App Service natively provides managed identity, VNet integration, deployment slots, and health checks that the pilot posture requires, without introducing container-image build/registry/runtime concerns that Container Apps would add before they deliver measurable benefit. Choosing one model now removes the live cross-document conflict and prevents two agents from independently picking divergent answers during concurrent infra work. Container Apps offers finer-grained autoscaling and per-service scaling that may become worthwhile at higher, more variable production load — but that is a data-driven optimization best made after real pilot workload numbers exist, consistent with the target-architecture guidance to "not pre-commit higher-cost SKUs before real workload numbers exist." This ADR therefore defers, rather than rejects, the Container Apps option.

## Consequences

- `infra/mainTemplate.bicep` and `infra/modules/*` remain the single source of truth for hosting; Phase 5 (W5.5) and later infra tasks extend them additively and do not restructure toward Container Apps.
- The Container Apps sections of the Delivery Tooling document are non-authoritative for the pilot and should be read as a future-option reference only.
- Revisiting this decision is explicitly scheduled as W9.1 (post-pilot), gated on real usage data.
