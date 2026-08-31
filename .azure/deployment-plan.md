# IIP Protected-Staging Deployment Plan

| Field | Value |
|---|---|
| Status | Planning — Azure context confirmation required |
| Date | 2026-08-07 |
| Mode | Modify existing Azure deployment architecture |
| Requested outcome | Recreate or nominate a protected staging runtime, then complete multi-role verification |

## 1. Project overview

**Goal:** recreate a cost-controlled, non-production `Staging` runtime for authenticated multi-role verification without mocks, seeded business data, browser-selected authority or fallback-to-mock.

**Path:** modify and harden the existing Bicep deployment architecture. Do not create a parallel architecture or reuse `Development` as staging.

## 2. Requirements

| Attribute | Value |
|---|---|
| Classification | Protected non-production staging/UAT |
| Scale | Small; controlled internal role-matrix cohort |
| Budget | Cost-optimized; no production HA claim |
| Data | Synthetic or masked insurance records only |
| Compliance | Tenant/workspace isolation, Entra authentication, least privilege, auditability, human authority, protected-runtime zero-simulation |
| Subscription | Awaiting explicit confirmation; candidate `Microsoft Azure Sponsorship` (`5f5765ae-985f-42e4-a443-8e2447be33ca`) |
| Location | Awaiting explicit confirmation; `westus2` is the current IIP dev region and initial recommendation |
| Resource group | `rg-iip-stg` (exists and is empty) |

No Azure Policy assignments were returned for the candidate subscription. Service quotas and deployment what-if remain blocked until subscription and location are confirmed.

## 3. Components detected

| Component | Type | Technology | Path |
|---|---|---|---|
| Staff portal | SSR web application | Next.js 15 / React 19 / TypeScript | `web/portal` |
| API | Web API | ASP.NET Core / .NET 8 | `src/InsuranceIntelligence.Api` |
| Workflow worker | Event processor | Azure Functions isolated / .NET 8 | `src/InsuranceIntelligence.Functions` |
| Domain/Application/Infrastructure | Business and provider layers | .NET 8 / EF Core | `src/InsuranceIntelligence.*` |
| Infrastructure | Resource-group Bicep | Bicep modules and parameter files | `infra/mainTemplate.bicep`, `infra/modules`, `infra/parameters/stg.bicepparam` |

## 4. Recipe selection

**Selected:** direct Bicep deployment with existing repository build/package procedures.

**Rationale:** the repository already owns a Bicep resource-group template and staging parameters; no `azure.yaml` or Terraform exists. Creating an AZD or parallel template would duplicate deployment knowledge. The stale code-only GitHub workflow cannot recreate deleted resources and must not be treated as the infrastructure path.

## 5. Architecture

**Stack:** App Service for API and portal; Azure Functions for background work.

| Component | Azure service | Planned staging posture |
|---|---|---|
| API and portal | Linux App Service, shared plan | Small non-production SKU; HTTPS-only; system-assigned identity |
| Worker | Linux Function App | Same plan where supported; system-assigned identity |
| Operational data | Azure SQL | Entra-only administration and managed-identity application access |
| Documents/function state | Storage accounts | Private containers; managed identity; no public blob access |
| Messaging | Service Bus | Entra data-plane access; local auth disabled |
| Retrieval | Azure AI Search | Basic staging SKU; managed identity |
| Document extraction | Azure AI Document Intelligence | S0 only if confirmed available and required for the UAT corpus |
| AI assistance | Existing or new Azure OpenAI | Must be explicitly available and approved; otherwise `Not configured`, never mock |
| Secrets/configuration | Key Vault | Managed identity and least privilege; no committed secrets |
| Observability | Log Analytics and Application Insights | Correlation, auth failures, provider health and UAT evidence |

### Existing IaC blockers requiring remediation before validation

- `mainTemplate.bicep` and staging parameters still accept SQL administrator login/password; protected staging must use unconditional Entra-only SQL administration.
- The Function module builds `AzureWebJobsStorage` from storage account keys and enables shared-key access; managed-identity/identity-based host storage must replace this before protected deployment.
- Staging parameters set Azure OpenAI mode while `deployAzureOpenAi=false`; endpoint/deployment provenance must be reconciled or the provider must remain `Not configured`.
- WhatsApp is disabled in the current staging parameters. This deployment can enable the protected shell and role matrix, but live WhatsApp UAT remains blocked until provider/business account, consent, templates, callbacks and credentials are separately approved.
- The GitHub staging workflow points to deleted App Service names and is code-only; it must not run until outputs and Entra redirects are updated.

## 6. Provisioning inventory

| ARM resource type | Number to deploy | Capacity status |
|---|---:|---|
| `Microsoft.Web/serverfarms` | 1 | Awaiting confirmed region quota/preflight |
| `Microsoft.Web/sites` | 3 | Awaiting confirmed region quota/preflight |
| `Microsoft.Storage/storageAccounts` | 2 | Awaiting confirmed region limit/count check |
| `Microsoft.Sql/servers` | 1 | Awaiting confirmed region limit/count check |
| `Microsoft.Sql/servers/databases` | 1 | Awaiting confirmed region capacity check |
| `Microsoft.ServiceBus/namespaces` | 1 | Awaiting confirmed region quota/preflight |
| `Microsoft.Search/searchServices` | 1 | Awaiting confirmed region quota/preflight |
| `Microsoft.CognitiveServices/accounts` — Document Intelligence | 1 | Awaiting confirmed region availability/quota |
| `Microsoft.CognitiveServices/accounts` — Azure OpenAI | 0 or 1 | Decision depends on approved existing account, residency and model quota |
| `Microsoft.KeyVault/vaults` | 1 | Awaiting confirmed region limit/count check |
| `Microsoft.OperationalInsights/workspaces` | 1 | Awaiting confirmed region limit/count check |
| `Microsoft.Insights/components` | 1 | Awaiting confirmed region limit/count check |
| `Microsoft.Insights/actionGroups` | 1 | Awaiting confirmed subscription limit check |
| `Microsoft.Insights/metricAlerts` | up to 5 | Awaiting confirmed subscription limit check |

## 7. Deployment sequence

1. Confirm subscription, region, cost profile and whether Azure OpenAI is required for this staging gate.
2. Check policies, providers, quotas, regional availability, current counts and Bicep what-if.
3. Patch the existing IaC for Entra-only SQL, identity-based Function storage, explicit provider state and current staging outputs.
4. Build Bicep, run repository tests and complete `azure-validate`; do not deploy on validation failure.
5. Use the Azure deployment workflow only after separate deployment confirmation, recording outputs and rollback evidence.
6. Publish matching API, portal and Function artifacts; update Entra redirects together; apply migrations through an approved migration window.
7. Provision server-authoritative UAT memberships for the role matrix, then run API, browser, accessibility, audit and p95/p99 evidence collection.

## 8. Validation and evidence

- Bicep compilation and what-if contain no destructive replacement or secret material.
- Protected settings prove `Staging`, Entra authentication, durable providers, no seed/mock/fallback.
- `/health/ready`, anonymous denial and server-resolved `/api/v1/me` pass.
- Claims Officer, Claims Supervisor, Customer Service, Compliance, Tenant Admin, no-membership and other-tenant conditions pass positive and safe-denial journeys.
- Playwright traces, accessibility/manual checks, audit correlation, provider health and representative latency evidence retained under `artifacts/rf09`.

## 9. Cost and reliability considerations

This is a new billable environment. Exact SKUs and estimated monthly cost will be recorded after subscription/location confirmation. Single-region, small-SKU staging does not establish production zone redundancy or disaster recovery. Reliability will be assessed after resources exist; remediation that increases cost requires separate approval.

## 10. Rollback

- Do not delete or overwrite `rg-iip-dev` or any unrelated subscription resource.
- Retain previous Entra redirect/config values until authenticated staging smoke passes.
- Deploy infrastructure and application artifacts as versioned, reproducible changes; record deployment operation IDs.
- On application failure, redeploy the last known package/config. On migration failure, stop and execute the approved database rollback/forward-fix plan.
- Resource deletion is outside this authorization and requires separate explicit confirmation.

## 11. Decisions and approvals

- [x] User authorized preparation/recreation of protected staging on 2026-08-07.
- [ ] Subscription and location confirmed.
- [ ] Azure OpenAI reuse/new deployment/`Not configured` choice confirmed.
- [ ] Quota, availability, cost and what-if checks complete with no unresolved capacity entry.
- [ ] IaC hardening changes reviewed and locally validated.
- [ ] Final deployment plan approved before Azure mutation.
- [ ] Plan marked `Ready for Validation`, then `azure-validate` completed.
- [ ] Separate deployment confirmation recorded before invoking Azure deployment.
