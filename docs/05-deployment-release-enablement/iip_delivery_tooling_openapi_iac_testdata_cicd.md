# IIP Delivery Tooling — OpenAPI, IaC, Test Data, and CI/CD

**Product:** Insurance Intelligence Platform (IIP)  
**Staff application:** IIP Operations  
**Document type:** Implementation-ready delivery/tooling specification  
**Area:** OpenAPI contract, infrastructure as code, test data, and CI/CD  
**Status:** New-repository implementation specification  
**Date:** July 2026

## Changelog

```text
- Fix 4: Removed the stray Infrastructure:AzureAI:* configuration mapping and kept only Infrastructure:AzureOpenAI:*, because AzureAI is not a real InfrastructureOptions section and AzureOpenAI is the canonical option name.
```

---

## Purpose

This document defines the delivery tooling needed to make the IIP backend implementable, testable, deployable, and governable in a new repository.

It extends the approved `docs/design-architecture/iip_infrastructure_as_code_architecture_guide.md` direction. It does not replace that architecture guide and does not redefine the route map already specified in **IIP Backend Wiring — Controllers, Composition Root, and Request Pipeline**.

Fixed inputs used by this document:

```text
- Backend route map from the Backend Wiring document.
- RuntimeProfile enum, RuntimeProfileParser, and IsProtected() behavior from the Core Ports / Wiring documents.
- AzureProviderStartupValidator and protected-runtime fail-closed behavior.
- InfrastructureOptions defaulting to managed identity for Azure providers.
- LocalTest-only doubles isolated in InsuranceIntelligence.LocalTestDoubles.
```

This document is not a service specification. It is structured as delivery tooling guidance and ends with a single setup checklist rather than service-level tests.

Non-negotiable delivery rules:

```text
1. Do not invent or silently add new API routes in delivery tooling.
2. OpenAPI must describe the routes already defined by the backend wiring document.
3. Protected runtime profiles must never deploy mock, in-memory, simulated, placeholder, demo, or seeded-demo behavior.
4. IaC must prefer managed identity over keys and secrets.
5. Seed/test data is allowed only in LocalTest/test-only assemblies.
6. CI/CD must fail closed before deployment if provider/runtime configuration is unsafe.
7. Production deploys require a manual approval gate.
8. Post-deploy smoke tests must verify both health and the resolved runtime profile.
```

---

## 1. OpenAPI / Swagger contract

### 1.1 Decision

OpenAPI generation is required for IIP.

The setup already specified in the backend wiring document remains the governing API behavior:

```text
- OpenAPI JSON generation is enabled.
- Swagger UI is available only in non-protected runtime profiles.
- Swagger UI is not exposed in Staging, Production, or ManagedApplicationReviewer.
- In protected profiles, OpenAPI JSON may be exposed only behind internal network/auth controls.
```

This document adds contract versioning, baseline comparison, and response-code documentation enforcement.

### 1.2 Files to create

```text
contracts/openapi/iip-api.openapi.json
contracts/openapi/iip-api.openapi.v1.0.0.json
contracts/openapi/CHANGELOG.md
contracts/openapi/README.md
scripts/openapi/generate-openapi.ps1
scripts/openapi/generate-openapi.sh
scripts/openapi/normalize-openapi.ps1
scripts/openapi/normalize-openapi.sh
scripts/openapi/check-openapi-diff.ps1
scripts/openapi/check-openapi-diff.sh
src/InsuranceIntelligence.Api/Extensions/OpenApiRegistration.cs
tests/InsuranceIntelligence.Tests/Api/OpenApiContractTests.cs
```

### 1.3 Contract versioning policy

IIP must version the OpenAPI contract as an artifact, not rely only on ASP.NET Core's default Swagger document name.

Versioning rules:

```text
- Every release has a semantic OpenAPI contract version: MAJOR.MINOR.PATCH.
- The current checked-in baseline lives at contracts/openapi/iip-api.openapi.json.
- Every release snapshot also lives at contracts/openapi/iip-api.openapi.v{MAJOR}.{MINOR}.{PATCH}.json.
- Contract changes must include an entry in contracts/openapi/CHANGELOG.md.
- Breaking changes require a MAJOR version increment.
- Backward-compatible route/field additions require a MINOR version increment.
- Documentation-only or description-only changes may use PATCH.
```

Example changelog entry:

```markdown
## 1.1.0 - 2026-07-15

- Added documented 503 response for document extraction provider outages.
- Added documented 409 response for approval workflow conflicts.
```

No endpoint may be introduced, removed, renamed, or have its request/response shape changed without the generated OpenAPI baseline changing in the same pull request.

### 1.4 OpenAPI registration

Create a single API extension so `Program.cs` stays small.

```csharp
using Microsoft.OpenApi.Models;

namespace InsuranceIntelligence.Api.Extensions;

public static class OpenApiRegistration
{
    public static IServiceCollection AddIipOpenApi(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Insurance Intelligence Platform API",
                Version = "v1",
                Description = "Internal IIP Operations API contract. Route map is governed by the backend wiring specification."
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Microsoft Entra ID JWT bearer token."
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }

    public static WebApplication UseIipOpenApi(this WebApplication app, RuntimeProfile runtimeProfile)
    {
        if (!runtimeProfile.IsProtected())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
            return app;
        }

        app.UseSwagger(options =>
        {
            options.RouteTemplate = "internal/openapi/{documentName}.json";
        });

        return app;
    }
}
```

`Program.cs` should call:

```csharp
builder.Services.AddIipOpenApi();
...
app.UseIipOpenApi(runtimeProfile);
```

### 1.5 Required response-type annotations

Every controller action must declare explicit response metadata. No endpoint may ship without `[ProducesResponseType]` entries for every success and error response it can produce.

Minimum rule:

```text
- 200 or 201 for success where applicable.
- 400 when ValidationException or model validation can occur.
- 401 for missing/invalid bearer token.
- 403 for ForbiddenException / missing permission.
- 404 for record-specific routes that can return NotFoundOrNotVisibleException or null detail.
- 409 for workflow routes that can throw WorkflowConflictException.
- 503 for ProviderNotConfiguredException or ProviderUnavailableException.
- 500 for unhandled errors mapped by SafeExceptionHandlingMiddleware.
```

Example controller action annotation:

```csharp
[HttpPost("documents/{documentId}/extraction")]
[ProducesResponseType(typeof(IReadOnlyList<ExtractedFieldDto>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
public async Task<ActionResult<IReadOnlyList<ExtractedFieldDto>>> ExtractAsync(
    [FromRoute] string documentId,
    CancellationToken cancellationToken)
{
    await _permissionService.EnsureAsync(Permissions.DocumentsReview, cancellationToken);
    return Ok(await _extractionService.ProcessExtractionAsync(documentId, cancellationToken));
}
```

Workflow examples such as `POST /api/v1/approvals/{approvalId}/decisions` must also include `409`.

### 1.6 OpenAPI generation scripts

The generator must run against the real API assembly and the real controller route map.

Example PowerShell:

```powershell
# scripts/openapi/generate-openapi.ps1
param(
    [string]$Output = "contracts/openapi/iip-api.openapi.generated.json"
)

$ErrorActionPreference = "Stop"

dotnet restore

dotnet build src/InsuranceIntelligence.Api/InsuranceIntelligence.Api.csproj --configuration Release --no-restore

dotnet tool restore

dotnet swagger tofile `
    --output $Output `
    src/InsuranceIntelligence.Api/bin/Release/net8.0/InsuranceIntelligence.Api.dll `
    v1
```

Example Bash:

```bash
#!/usr/bin/env bash
set -euo pipefail

OUTPUT="${1:-contracts/openapi/iip-api.openapi.generated.json}"

dotnet restore

dotnet build src/InsuranceIntelligence.Api/InsuranceIntelligence.Api.csproj --configuration Release --no-restore

dotnet tool restore

dotnet swagger tofile \
  --output "$OUTPUT" \
  src/InsuranceIntelligence.Api/bin/Release/net8.0/InsuranceIntelligence.Api.dll \
  v1
```

### 1.7 OpenAPI normalization and diff check

Raw OpenAPI JSON may vary by ordering. Normalize before diffing.

Implementation rule:

```text
- Generate JSON.
- Normalize JSON deterministically.
- Compare normalized generated file to normalized baseline.
- If changed, require a matching changelog entry in contracts/openapi/CHANGELOG.md.
- Fail the CI job if either condition is not met.
```

Example diff script behavior:

```text
Input:
- contracts/openapi/iip-api.openapi.json
- contracts/openapi/iip-api.openapi.generated.json
- contracts/openapi/CHANGELOG.md

Failure conditions:
- Generated contract differs from baseline and no changelog entry was modified in the PR.
- Generated contract cannot be parsed.
- Baseline contract cannot be parsed.
- Contract version file for the target release is missing.
```

### 1.8 OpenAPI contract tests

Add tests that inspect the generated OpenAPI document.

```text
OpenApiContractTests
- EveryControllerActionHasProducesResponseTypeForSuccess
- EveryProtectedControllerActionDocuments401And403
- ProviderTouchingEndpointsDocument503
- WorkflowEndpointsDocument409
- RecordDetailEndpointsDocument404
- OpenApiBaselineExists
- OpenApiChangelogExists
```

These are tooling contract tests, not Application service tests.

---

## 2. Bicep / IaC templates

### 2.1 Decision

The IaC implementation extends the approved `docs/design-architecture/iip_infrastructure_as_code_architecture_guide.md`.

This document does not re-derive the high-level Azure architecture. It only specifies the concrete Bicep module list, composition layout, and environment parameter structure required for the new repository.

The default API hosting target for this pass is **Azure Container Apps**. App Service can be added later by ADR if the deployment target changes.

### 2.2 Files to create

```text
infra/bicep/main.bicep
infra/bicep/main.dev.bicepparam
infra/bicep/main.staging.bicepparam
infra/bicep/main.production.bicepparam

infra/bicep/modules/managed-identity.bicep
infra/bicep/modules/log-analytics.bicep
infra/bicep/modules/application-insights.bicep
infra/bicep/modules/key-vault.bicep
infra/bicep/modules/storage-account.bicep
infra/bicep/modules/sql-server.bicep
infra/bicep/modules/sql-database.bicep
infra/bicep/modules/ai-search.bicep
infra/bicep/modules/azure-openai.bicep
infra/bicep/modules/document-intelligence.bicep
infra/bicep/modules/service-bus.bicep
infra/bicep/modules/container-app-environment.bicep
infra/bicep/modules/container-app-api.bicep
infra/bicep/modules/role-assignments.bicep
infra/bicep/modules/diagnostic-settings.bicep

infra/bicep/README.md
infra/bicep/parameters/README.md
scripts/entra/create-api-app-registration.ps1
scripts/entra/create-api-app-registration.sh
scripts/deploy/what-if.ps1
scripts/deploy/what-if.sh
scripts/deploy/deploy-bicep.ps1
scripts/deploy/deploy-bicep.sh
```

### 2.3 Required Azure resources

The program requires these Azure resources per environment unless an approved environment-specific exception exists:

```text
- User-assigned managed identity for the API/runtime workloads.
- Azure SQL Server.
- Azure SQL Database.
- Azure Blob Storage account and containers.
- Azure Key Vault.
- Azure AI Search service and index configuration target.
- Azure OpenAI account/deployments.
- Azure AI Document Intelligence account.
- Azure Service Bus namespace and queues/topics.
- Log Analytics workspace.
- Application Insights resource.
- Azure Container Apps environment.
- Azure Container App for the API.
- Diagnostic settings wired to Log Analytics.
- Role assignments granting managed identity access to required services.
- Microsoft Entra App Registration for the API.
```

Important note:

```text
Microsoft Entra App Registration usually requires Microsoft Graph / az ad / portal automation and may not be fully represented as pure Bicep in all tenants. Treat it as a separate deployment step with scripts and manual approval where required.
```

### 2.4 Module responsibilities

#### `managed-identity.bicep`

Creates a user-assigned managed identity.

Outputs:

```text
managedIdentityResourceId
managedIdentityClientId
managedIdentityPrincipalId
```

#### `log-analytics.bicep`

Creates Log Analytics workspace.

Outputs:

```text
workspaceId
workspaceResourceId
```

#### `application-insights.bicep`

Creates Application Insights connected to Log Analytics.

Outputs:

```text
applicationInsightsConnectionString
applicationInsightsResourceId
```

The connection string should be stored in Key Vault or injected through app configuration, not committed to source.

#### `key-vault.bicep`

Creates Key Vault with RBAC enabled.

Required behavior:

```text
- Disable public network access where environment policy requires it.
- Enable purge protection in staging/production.
- Grant the API managed identity read access to required secrets/certificates.
```

Outputs:

```text
keyVaultName
keyVaultUri
keyVaultResourceId
```

#### `storage-account.bicep`

Creates Azure Storage account and Blob containers.

Initial containers:

```text
- documents
- exports
- knowledge-source
- knowledge-index-staging
```

Required behavior:

```text
- HTTPS only.
- Minimum TLS 1.2 or higher.
- No shared-key usage for protected runtimes unless separately approved.
- Managed identity access for Blob Data Contributor where needed.
```

Outputs:

```text
storageAccountName
blobEndpoint
documentsContainerName
exportsContainerName
knowledgeSourceContainerName
```

#### `sql-server.bicep` and `sql-database.bicep`

Create Azure SQL Server and database.

Required behavior:

```text
- Azure AD / Entra admin configured where tenant policy permits.
- Connection configuration supports managed identity first.
- Transparent Data Encryption enabled.
- Auditing/diagnostics wired through diagnostic-settings module where supported.
```

Outputs:

```text
sqlServerName
sqlServerFullyQualifiedDomainName
sqlDatabaseName
sqlConnectionStringForManagedIdentityPattern
```

Do not create SQLite infrastructure. SQLite is rejected by the startup validator in all environments.

#### `ai-search.bicep`

Creates Azure AI Search.

Required behavior:

```text
- Parameterize SKU per environment.
- Create or output the target index name from AzureAiSearchOptions.IndexName.
- Grant the managed identity Search Index Data Contributor / Search Service Contributor as required by the adapter design.
```

Outputs:

```text
searchServiceName
searchEndpoint
searchIndexName
```

#### `azure-openai.bicep`

Creates or references Azure OpenAI account and deployments.

Required parameters:

```text
chatDeploymentName
embeddingDeploymentName
location
```

Required behavior:

```text
- Embedding deployment name must map to AzureOpenAiOptions.EmbeddingDeployment.
- Chat deployment name must map to the configured chat model/deployment option.
- Managed identity access must be preferred over API keys.
```

Outputs:

```text
azureOpenAiEndpoint
chatDeploymentName
embeddingDeploymentName
```

#### `document-intelligence.bicep`

Creates Azure AI Document Intelligence account.

Outputs:

```text
documentIntelligenceEndpoint
documentIntelligenceResourceId
```

#### `service-bus.bicep`

Creates Service Bus namespace and required queues/topics.

Initial queues/topics:

```text
- document-extraction-requests
- knowledge-ingestion-requests
- notification-send-requests
- integration-writeback-requests
```

Outputs:

```text
serviceBusNamespaceName
serviceBusFullyQualifiedNamespace
documentExtractionQueueName
knowledgeIngestionQueueName
notificationSendQueueName
integrationWriteBackQueueName
```

#### `container-app-environment.bicep`

Creates Container Apps managed environment and connects diagnostics.

Outputs:

```text
containerAppEnvironmentId
```

#### `container-app-api.bicep`

Creates the API container app.

Required behavior:

```text
- Assign the user-managed identity.
- Set InfrastructureOptions values as environment variables/configuration.
- Set RuntimeProfile to the target environment value.
- Set AuthMode=Entra in staging/production/reviewer.
- Do not set any Mock/InMemory provider mode in protected runtimes.
- Configure Application Insights connection.
```

Outputs:

```text
apiUrl
apiResourceId
```

#### `role-assignments.bicep`

Assigns managed identity permissions.

Minimum role assignment categories:

```text
- Storage Blob Data Contributor for document/export/knowledge containers.
- Key Vault Secrets User or equivalent least-privilege role.
- Azure AI Search roles needed by the indexing/search adapters.
- Cognitive Services User for Azure OpenAI and Document Intelligence where supported.
- Azure Service Bus Data Sender/Receiver for queues/topics.
- SQL managed identity access must be completed through SQL/AAD configuration and migrations where Bicep cannot fully configure the database user.
```

#### `diagnostic-settings.bicep`

Configures diagnostic settings for supported resources.

Target:

```text
- Log Analytics workspace.
```

### 2.5 `main.bicep` composition

`main.bicep` must compose modules in dependency order.

Required order:

```text
1. Log Analytics.
2. Managed identity.
3. Application Insights.
4. Key Vault.
5. Storage Account.
6. Azure SQL Server / Database.
7. Azure AI Search.
8. Azure OpenAI.
9. Azure AI Document Intelligence.
10. Service Bus.
11. Role assignments.
12. Container Apps environment.
13. API Container App.
14. Diagnostic settings.
```

Example composition outline:

```bicep
param environmentName string
param location string
param workloadName string = 'iip'
param runtimeProfile string
param authMode string

module logAnalytics 'modules/log-analytics.bicep' = {
  name: 'log-${environmentName}'
  params: {
    location: location
    environmentName: environmentName
    workloadName: workloadName
  }
}

module identity 'modules/managed-identity.bicep' = {
  name: 'id-${environmentName}'
  params: {
    location: location
    environmentName: environmentName
    workloadName: workloadName
  }
}

module appInsights 'modules/application-insights.bicep' = {
  name: 'appi-${environmentName}'
  params: {
    location: location
    environmentName: environmentName
    workloadName: workloadName
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceResourceId
  }
}

// Continue with Key Vault, Storage, SQL, AI Search, Azure OpenAI,
// Document Intelligence, Service Bus, role assignments, Container Apps,
// and diagnostic settings.
```

### 2.6 Environment parameter files

Required parameter files:

```text
infra/bicep/main.dev.bicepparam
infra/bicep/main.staging.bicepparam
infra/bicep/main.production.bicepparam
```

Required parameter categories:

```text
- environmentName
- runtimeProfile
- location
- workloadName
- Azure SQL SKU / sizing
- Storage SKU
- AI Search SKU
- Azure OpenAI deployment names
- Document Intelligence SKU
- Service Bus SKU
- Container Apps CPU/memory/scale settings
- Entra tenant/client/audience/issuer values or references
- Public network/private endpoint controls where applicable
```

Protected-runtime parameter rules:

```text
- staging: runtimeProfile = 'Staging', authMode = 'Entra'
- production: runtimeProfile = 'Production', authMode = 'Entra'
- ManagedApplicationReviewer: runtimeProfile = 'ManagedApplicationReviewer', authMode = 'Entra'
- No protected parameter file may set Mock, InMemory, SQLite, DevelopmentAuth, Demo, SeededDemoBusinessData, or FallbackToMock provider modes.
```

Example protected parameter values:

```bicep
using './main.bicep'

param environmentName = 'staging'
param runtimeProfile = 'Staging'
param authMode = 'Entra'
param location = 'eastus'
param workloadName = 'iip'
```

### 2.7 Configuration output to InfrastructureOptions

IaC must produce or document the values that map into `InfrastructureOptions`.

Minimum mapping:

```text
Infrastructure:RuntimeProfile -> runtimeProfile
Infrastructure:AuthMode -> authMode
Infrastructure:StorageProvider -> SqlServer
Infrastructure:BlobProvider -> AzureBlob
Infrastructure:DocumentProvider -> AzureDocumentIntelligence
Infrastructure:RetrievalProvider -> AzureAiSearch
Infrastructure:AiProvider -> AzureOpenAI
Infrastructure:QueueProvider -> AzureServiceBus
Infrastructure:EmailProvider -> Graph
Infrastructure:CommunicationProvider -> GraphEmail or configured real provider
Infrastructure:NotificationProvider -> IncomingWebhook or configured real provider when actually used
Infrastructure:UseManagedIdentity -> true
Infrastructure:SqlServerConnectionString -> managed identity compatible SQL connection pattern
Infrastructure:EntraTenantId -> Entra tenant id
Infrastructure:EntraClientId -> API app/client id
Infrastructure:EntraAudience -> API audience/application ID URI
Infrastructure:EntraIssuer -> expected issuer
Infrastructure:AzureBlob:* -> storage account/blob settings
Infrastructure:AzureOpenAI:* -> Azure OpenAI endpoint/deployment settings
Infrastructure:AzureAiSearch:* -> search endpoint/index settings
Infrastructure:DocumentIntelligence:* -> document intelligence endpoint/settings
Infrastructure:ServiceBus:* -> namespace and queue/topic settings
```

Use the exact property names established in `InfrastructureOptions`; do not introduce nested option names that do not exist in the core ports document.

### 2.8 IaC validation commands

Example what-if command:

```bash
az deployment group what-if \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/main.staging.bicepparam
```

Example validate command:

```bash
az deployment group validate \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/main.staging.bicepparam
```

The CI/CD pipeline must run Bicep validation and what-if before any deployment stage.

---

## 3. Seed / test data

### 3.1 Decision

Synthetic test data is allowed only for LocalTest-profile automated tests and local isolated execution.

Synthetic data must be realistic enough to exercise claims, policies, customers, documents, approvals, channels, sales leads, audit, reports, write-back history, and knowledge ingestion behavior, but it must be fabricated and must never be deployed as customer/demo data to protected environments.

### 3.2 Allowed location

All seed/test data generation code must live in test-only or LocalTest-only projects.

Allowed:

```text
src/InsuranceIntelligence.LocalTestDoubles/SeedData/SyntheticIipDataSeeder.cs
src/InsuranceIntelligence.LocalTestDoubles/SeedData/SyntheticClaimFactory.cs
src/InsuranceIntelligence.LocalTestDoubles/SeedData/SyntheticPolicyFactory.cs
src/InsuranceIntelligence.LocalTestDoubles/SeedData/SyntheticCustomerFactory.cs
tests/InsuranceIntelligence.Tests/TestData/*
tests/InsuranceIntelligence.Tests.Integration/TestData/*
```

Forbidden:

```text
src/InsuranceIntelligence.Api/SeedData/*
src/InsuranceIntelligence.Infrastructure/SeedData/*
src/InsuranceIntelligence.Application/SeedData/*
src/InsuranceIntelligence.Functions/SeedData/*
```

Deployable projects must not reference `InsuranceIntelligence.LocalTestDoubles`.

### 3.3 Runtime guard

The seeding utility must refuse to run unless the resolved runtime profile is `LocalTest`.

```csharp
namespace InsuranceIntelligence.LocalTestDoubles.SeedData;

public sealed class SyntheticIipDataSeeder
{
    public async Task SeedAsync(
        RuntimeProfile runtimeProfile,
        InfrastructureOptions options,
        CancellationToken cancellationToken)
    {
        if (runtimeProfile != RuntimeProfile.LocalTest)
        {
            throw new InvalidOperationException("Synthetic seed data can only run in LocalTest runtime profile.");
        }

        if (!options.SeedDemoData)
        {
            return;
        }

        // Insert fabricated records only into the LocalTest data store used by tests.
        await Task.CompletedTask;
    }
}
```

Protected runtime protection already exists through `AzureProviderStartupValidator`. Do not invent a second production seeding flag.

The only accepted flag is:

```text
InfrastructureOptions.SeedDemoData
```

Required behavior:

```text
- If SeedDemoData == true and RuntimeProfile is Staging, Production, or ManagedApplicationReviewer, AzureProviderStartupValidator must reject startup.
- If runtimeProfile != LocalTest, the seeder must throw even if someone accidentally calls it.
- No seed data execution path may exist in the production API host.
```

### 3.4 Allowed synthetic data categories

Allowed for LocalTest only:

```text
- Fabricated tenants/workspaces.
- Fabricated users, roles, permissions.
- Fabricated customers.
- Fabricated policies.
- Fabricated motor claims and claim timelines.
- Fabricated FNOL records.
- Fabricated document metadata and extraction fields.
- Fabricated approvals and human approval decisions.
- Fabricated channel conversations.
- Fabricated sales leads.
- Fabricated audit rows.
- Fabricated report aggregates.
- Fabricated write-back history rows.
- Fabricated knowledge documents approved for retrieval.
```

Forbidden in any environment:

```text
- Real customer PII copied into test fixtures.
- Real policy numbers or claim numbers from a customer insurer.
- Real medical/claims documents.
- Real external-system credentials.
- Raw provider payloads captured from production.
- Customer-confidential documents marked as AI-retrievable for convenience.
```

### 3.5 Test data determinism

Synthetic data must be deterministic by seed value.

Rules:

```text
- Factories must accept a seed or deterministic fixture ID.
- Tests must not rely on wall-clock dates unless IClock is injected/frozen.
- Claim numbers generated by synthetic factories must be clearly fake, for example CLM-TEST-000001.
- Synthetic customer names must not resemble real customer records.
```

### 3.6 CI behavior

CI may use LocalTest synthetic data only during automated tests.

CI must not run seed commands during deployment jobs.

Required CI check:

```text
- Verify production deploy artifact does not contain InsuranceIntelligence.LocalTestDoubles.dll.
- Verify deployable projects do not reference InsuranceIntelligence.LocalTestDoubles.
- Verify InfrastructureOptions.SeedDemoData is false for staging/production/reviewer parameter sets.
```

---

## 4. CI/CD pipeline

### 4.1 Decision

Use **GitHub Actions** for the new repository CI/CD pipeline.

Azure DevOps can be introduced later if the client's tenant requires it, but this specification standardizes on GitHub Actions for consistency.

### 4.2 Required workflow files

```text
.github/workflows/iip-ci.yml
.github/workflows/iip-cd-dev.yml
.github/workflows/iip-cd-staging.yml
.github/workflows/iip-cd-production.yml
.github/workflows/iip-openapi-contract.yml
.github/workflows/iip-iac-validate.yml
```

A single combined workflow is also acceptable if the job gates are preserved exactly. The required order must not change.

### 4.3 Pipeline stages

Required gated order:

```text
1. Restore.
2. Build.
3. Unit tests.
4. AzureProviderStartupValidator configuration tests.
5. OpenAPI contract-diff check.
6. Bicep validate and what-if for target environment.
7. Deploy dev/staging automatically after passing gates.
8. Deploy production only after manual approval.
9. Post-deploy smoke test against real health endpoint.
10. Confirm resolved runtime profile matches target environment.
```

No deploy stage may run if stages 1-6 fail.

### 4.4 CI workflow

Example `.github/workflows/iip-ci.yml`:

```yaml
name: iip-ci

on:
  pull_request:
    branches: [ main ]
  push:
    branches: [ main ]

permissions:
  contents: read

jobs:
  restore-build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --configuration Release --no-restore

      - name: Unit tests
        run: dotnet test --configuration Release --no-build --filter "Category!=Integration"

  startup-validator-tests:
    runs-on: ubuntu-latest
    needs: restore-build-test
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Run AzureProviderStartupValidator tests
        run: dotnet test tests/InsuranceIntelligence.Tests/InsuranceIntelligence.Tests.csproj --configuration Release --filter "FullyQualifiedName~AzureProviderStartupValidator"

  openapi-contract-diff:
    runs-on: ubuntu-latest
    needs: startup-validator-tests
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore tools
        run: dotnet tool restore

      - name: Generate OpenAPI
        run: bash scripts/openapi/generate-openapi.sh contracts/openapi/iip-api.openapi.generated.json

      - name: Check OpenAPI diff
        run: bash scripts/openapi/check-openapi-diff.sh

  bicep-validate-dev:
    runs-on: ubuntu-latest
    needs: openapi-contract-diff
    steps:
      - uses: actions/checkout@v4

      - name: Azure login
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Bicep validate dev
        run: |
          az deployment group validate \
            --resource-group "${{ vars.AZURE_DEV_RESOURCE_GROUP }}" \
            --template-file infra/bicep/main.bicep \
            --parameters infra/bicep/main.dev.bicepparam

      - name: Bicep what-if dev
        run: |
          az deployment group what-if \
            --resource-group "${{ vars.AZURE_DEV_RESOURCE_GROUP }}" \
            --template-file infra/bicep/main.bicep \
            --parameters infra/bicep/main.dev.bicepparam
```

### 4.5 Deployment workflow

Example staging deployment job:

```yaml
name: iip-cd-staging

on:
  workflow_dispatch:
  push:
    branches: [ main ]

permissions:
  id-token: write
  contents: read

jobs:
  deploy-staging:
    runs-on: ubuntu-latest
    environment: staging
    steps:
      - uses: actions/checkout@v4

      - name: Azure login
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Verify startup validator tests before deploy
        run: dotnet test tests/InsuranceIntelligence.Tests/InsuranceIntelligence.Tests.csproj --configuration Release --filter "FullyQualifiedName~AzureProviderStartupValidator"

      - name: Bicep validate staging
        run: |
          az deployment group validate \
            --resource-group "${{ vars.AZURE_STAGING_RESOURCE_GROUP }}" \
            --template-file infra/bicep/main.bicep \
            --parameters infra/bicep/main.staging.bicepparam

      - name: Bicep what-if staging
        run: |
          az deployment group what-if \
            --resource-group "${{ vars.AZURE_STAGING_RESOURCE_GROUP }}" \
            --template-file infra/bicep/main.bicep \
            --parameters infra/bicep/main.staging.bicepparam

      - name: Deploy staging infrastructure
        run: |
          az deployment group create \
            --resource-group "${{ vars.AZURE_STAGING_RESOURCE_GROUP }}" \
            --template-file infra/bicep/main.bicep \
            --parameters infra/bicep/main.staging.bicepparam

      - name: Deploy API container image
        run: |
          echo "Deploy API image through the approved Container Apps deployment command or reusable workflow."

      - name: Smoke test staging
        run: bash scripts/smoke/smoke-test.sh "${{ vars.IIP_STAGING_API_BASE_URL }}" "Staging"
```

Production must use a GitHub Environment named `production` with required reviewers enabled.

Example production job declaration:

```yaml
jobs:
  deploy-production:
    runs-on: ubuntu-latest
    environment: production
    steps:
      # same gates as staging, but using production parameter file and production smoke profile
```

Manual production approval is configured in GitHub repository settings, not in YAML alone.

### 4.6 Post-deploy smoke test

Smoke test script:

```text
scripts/smoke/smoke-test.sh
scripts/smoke/smoke-test.ps1
```

Required checks:

```text
- Call the real approved health endpoint, for example /health/ready from the backend baseline.
- Confirm HTTP 200.
- Confirm response contains or can be paired with a diagnostics endpoint that reports runtimeProfile.
- Confirm runtimeProfile equals the target environment: Development, Staging, Production, or ManagedApplicationReviewer.
- Fail deployment if production resolves to anything other than Production.
- Fail deployment if staging resolves to anything other than Staging.
```

Example Bash:

```bash
#!/usr/bin/env bash
set -euo pipefail

BASE_URL="$1"
EXPECTED_PROFILE="$2"

READY_JSON="$(curl -fsS "$BASE_URL/health/ready")"

echo "$READY_JSON" | jq -e '.status == "Healthy" or .status == "healthy"' >/dev/null

aCTUAL_PROFILE="$(echo "$READY_JSON" | jq -r '.runtimeProfile // empty')"

if [ -z "$aCTUAL_PROFILE" ]; then
  echo "Health response did not include runtimeProfile. Add runtimeProfile to the approved health response or expose it through the approved internal health contract."
  exit 1
fi

if [ "$aCTUAL_PROFILE" != "$EXPECTED_PROFILE" ]; then
  echo "Runtime profile mismatch. Expected $EXPECTED_PROFILE but got $aCTUAL_PROFILE."
  exit 1
fi
```

Implementation note:

```text
If the current health endpoint does not yet include runtimeProfile, extend the existing health response shape. Do not invent a new public route solely for this check.
```

### 4.7 Required deployment gates

CI/CD must block deployment when any of these are true:

```text
- dotnet build fails.
- unit tests fail.
- AzureProviderStartupValidator tests fail.
- Generated OpenAPI differs from baseline without changelog update.
- Bicep validation fails.
- Bicep what-if fails due to policy/permission/template errors.
- Protected environment parameter file uses mock/in-memory/development provider mode.
- SeedDemoData is true for a protected profile.
- Production deployment lacks manual approval.
- Smoke test fails.
- Runtime profile returned by health endpoint does not match target environment.
```

### 4.8 Artifact retention

Pipeline artifacts to retain:

```text
- Build output.
- Test results.
- Generated OpenAPI JSON.
- Bicep what-if output.
- Deployment logs.
- Smoke test output.
```

Recommended retention:

```text
- Pull request artifacts: 14 days.
- Staging deployment artifacts: 30 days.
- Production deployment artifacts: 180 days or according to client compliance policy.
```

---

## Setup checklist

### OpenAPI / Swagger contract

```text
[ ] `src/InsuranceIntelligence.Api/Extensions/OpenApiRegistration.cs` exists.
[ ] `Program.cs` calls `AddIipOpenApi()` and `UseIipOpenApi(runtimeProfile)`.
[ ] Swagger JSON generation is enabled.
[ ] Swagger UI is available only in non-protected profiles.
[ ] Swagger UI is disabled in Staging, Production, and ManagedApplicationReviewer.
[ ] `contracts/openapi/iip-api.openapi.json` baseline is committed.
[ ] Versioned contract snapshot such as `contracts/openapi/iip-api.openapi.v1.0.0.json` is committed for the release.
[ ] `contracts/openapi/CHANGELOG.md` exists.
[ ] OpenAPI generation scripts exist for Bash and/or PowerShell.
[ ] OpenAPI diff check fails CI if generated JSON differs from baseline without changelog entry.
[ ] Controller actions include `[ProducesResponseType]` for applicable 200/400/401/403/404/409/503/500 responses.
```

### Bicep / IaC

```text
[ ] `infra/bicep/main.bicep` exists.
[ ] `infra/bicep/main.dev.bicepparam` exists.
[ ] `infra/bicep/main.staging.bicepparam` exists.
[ ] `infra/bicep/main.production.bicepparam` exists.
[ ] `modules/managed-identity.bicep` exists.
[ ] `modules/log-analytics.bicep` exists.
[ ] `modules/application-insights.bicep` exists.
[ ] `modules/key-vault.bicep` exists.
[ ] `modules/storage-account.bicep` exists.
[ ] `modules/sql-server.bicep` exists.
[ ] `modules/sql-database.bicep` exists.
[ ] `modules/ai-search.bicep` exists.
[ ] `modules/azure-openai.bicep` exists.
[ ] `modules/document-intelligence.bicep` exists.
[ ] `modules/service-bus.bicep` exists.
[ ] `modules/container-app-environment.bicep` exists.
[ ] `modules/container-app-api.bicep` exists.
[ ] `modules/role-assignments.bicep` exists.
[ ] `modules/diagnostic-settings.bicep` exists.
[ ] Managed identity is wired to Key Vault, Blob Storage, SQL access pattern, AI Search, Azure OpenAI, Document Intelligence, and Service Bus.
[ ] Protected parameter files use `AuthMode = Entra`.
[ ] Protected parameter files do not use Mock, InMemory, SQLite, DevelopmentAuth, Demo, SeededDemoBusinessData, or FallbackToMock provider modes.
[ ] Entra App Registration script exists, with manual tenant-admin consent step documented.
```

### Seed / test data

```text
[ ] Synthetic data seeding utility lives only in `InsuranceIntelligence.LocalTestDoubles` or test projects.
[ ] No seeding code exists under `InsuranceIntelligence.Api`, `InsuranceIntelligence.Infrastructure`, `InsuranceIntelligence.Application`, or `InsuranceIntelligence.Functions`.
[ ] Seeder refuses to run unless `RuntimeProfile == LocalTest`.
[ ] Seeder reuses `InfrastructureOptions.SeedDemoData`; no second seed flag exists.
[ ] AzureProviderStartupValidator rejects `SeedDemoData == true` in protected runtimes.
[ ] Test data is fabricated and deterministic.
[ ] Test data contains no real customer PII, real documents, real claim numbers, real policy numbers, or real provider payloads.
[ ] CI verifies deployable projects do not reference `InsuranceIntelligence.LocalTestDoubles`.
[ ] CI verifies protected publish output does not contain `InsuranceIntelligence.LocalTestDoubles.dll`.
```

### CI/CD

```text
[ ] GitHub Actions is selected as the pipeline system.
[ ] Restore/build/unit-test stage exists.
[ ] AzureProviderStartupValidator configuration tests run before any deploy job.
[ ] OpenAPI contract-diff check runs before any deploy job.
[ ] Bicep validate runs for the target environment before deploy.
[ ] Bicep what-if runs for the target environment before deploy.
[ ] Dev deployment is automated after gates pass.
[ ] Staging deployment is automated after gates pass, subject to branch/environment policy.
[ ] Production deployment uses GitHub Environment `production` with required manual reviewers.
[ ] Post-deploy smoke test calls the approved health endpoint.
[ ] Smoke test verifies the resolved runtime profile matches the target environment.
[ ] Deployment fails if production does not resolve to `Production`.
[ ] Deployment fails if staging does not resolve to `Staging`.
[ ] Build/test/OpenAPI/IaC/deployment/smoke artifacts are retained according to policy.
```

### Manual portal actions versus code-committed actions

Code-committed actions:

```text
[ ] OpenAPI registration, baseline, scripts, and diff checks.
[ ] Bicep modules and parameter files.
[ ] Synthetic seed utilities in LocalTest-only/test-only projects.
[ ] GitHub Actions workflow files.
[ ] Smoke test scripts.
[ ] Startup validator tests.
[ ] LocalTest isolation checks.
```

Manual Azure / GitHub / tenant-admin actions:

```text
[ ] Create or approve Microsoft Entra API App Registration where tenant policy does not allow full automation.
[ ] Grant admin consent for required API permissions, especially Microsoft Graph where applicable.
[ ] Configure GitHub OIDC federation to Azure.
[ ] Create GitHub repository environments: dev, staging, production.
[ ] Configure required reviewers for the production environment.
[ ] Configure Azure subscription/resource group permissions for the deployment identity.
[ ] Approve Azure OpenAI access/deployments where tenant/subscription policy requires manual approval.
[ ] Confirm private networking/firewall settings where required by customer tenant policy.
[ ] Confirm retention policy for pipeline artifacts and report/export artifacts.
```
