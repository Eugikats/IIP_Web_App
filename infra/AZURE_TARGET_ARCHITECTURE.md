# Azure Target Architecture

This document defines the recommended Azure hosting shape for this repository as it moves from local/mock mode to a commercially credible Azure Marketplace Managed Application.

## Recommendation

Host InsuranceIntelligence Platform as:

- one Azure Managed Application deployment per insurer
- deployed into the customer's Azure subscription
- primarily on Azure PaaS services
- with logical workspace isolation inside each insurer deployment

This matches the repository contract in `AGENTS.md`, the current code split, and the existing infra direction in `infra/mainTemplate.bicep`.

## Current repo fit

Current code and infra already point to this shape:

- portal: `web/portal` is a Next.js app with `output: "standalone"`
- API: `src/InsuranceIntelligence.Api` is an ASP.NET Core .NET 8 web app
- workflows: `src/InsuranceIntelligence.Functions` is the right home for Azure Functions/Durable Functions expansion
- persistence: `src/InsuranceIntelligence.Infrastructure` supports isolated local in-memory mode and explicit SQL Server/Azure SQL configuration for durable environments
- infra: `infra/mainTemplate.bicep` already assumes Managed Application packaging and future Azure SQL, AI Search, OpenAI, Service Bus, Graph, and communications providers

## Target Azure resources

For each insurer deployment, provision:

### Core application layer

- `portal-web`: Azure App Service for the Next.js portal
- `api-web`: Azure App Service for the .NET 8 API
- `workflow-func`: Azure Functions Premium plan app for Durable Functions and async workers

### Data and storage

- `sql-server` + `sql-db`: Azure SQL Database for transactional state
- `storage`: Azure Storage account for raw documents, extracted artifacts, knowledge files, exports, and Durable storage
- `servicebus`: Azure Service Bus namespace for async processing and decoupling

### AI and search

- `search`: Azure AI Search
- `openai`: Azure OpenAI
- `docintel`: Azure AI Document Intelligence

### Security and operations

- `keyvault`: Azure Key Vault
- `appi-api`: Application Insights for API/portal/functions telemetry
- `loganalytics`: Log Analytics workspace
- `managed identities`: system-assigned identities on App Service and Functions

### Optional but recommended for production

- `apim`: Azure API Management, if external broker/partner APIs need a governed edge
- `appgw`: Application Gateway with WAF, if the insurer requires controlled HTTP ingress, WAF, or private frontend patterns
- private endpoints for SQL, Storage, Key Vault, Search, OpenAI, Service Bus, and optionally App Service

## Recommended hosting decisions

### Portal

Use Azure App Service for the portal.

Reason:

- this repo uses Next.js App Router and a server-capable standalone build
- App Service is the simplest stable fit for insurer-grade hosting
- it avoids the current feature and topology caveats of hybrid Next.js on Static Web Apps

Deployment artifact:

- standalone Next.js server build from `web/portal`

### API

Use Azure App Service for the API.

Reason:

- the API is already a standard ASP.NET Core .NET 8 web app
- App Service gives managed identity, VNet integration, diagnostics, deployment slots, and straightforward ops

### Workflows

Use Azure Functions Premium with Durable Functions.

Reason:

- the product needs long-running, auditable, human-supervised workflows
- Premium avoids Consumption-plan cold-start characteristics and supports more controlled enterprise networking
- the repo already has a `Functions` project placeholder that should absorb orchestration work instead of overloading the API

### Database

Use Azure SQL Database.

Reason:

- the repo and product contract are transaction-heavy and relational
- current EF repository design maps to Azure SQL through explicit `SqlServer` provider configuration
- this is a better V1 fit than Cosmos DB

### Documents and artifacts

Use Azure Blob Storage containers in one account, separated by purpose:

- `raw-documents`
- `normalized-artifacts`
- `knowledge-documents`
- `knowledge-index-artifacts`
- `audit-exports`
- `claim-packs`

### Queueing

Use one Service Bus namespace with queues such as:

- `email-ingestion`
- `document-processing`
- `workflow-evaluation`
- `approval-notifications`
- `customer-updates`
- `outbound-communications`

### AI services

Use:

- Azure OpenAI for summarization, drafting, and grounded answer generation
- Azure AI Search for retrieval over approved knowledge
- Azure AI Document Intelligence for extraction

All AI calls must remain behind provider interfaces already implied by the repo.

## Network posture by environment

### Dev

Use:

- public App Service endpoints
- no private endpoints by default
- Azure SQL firewall limited to admin/dev access
- mock providers still available

This environment is for developer productivity, not insurer-grade isolation.

### Test / Pilot

Use:

- public App Service endpoints or controlled ingress through Application Gateway if customer requires it
- VNet integration for App Service and Functions
- private endpoints at least for SQL, Key Vault, and Storage
- managed identities everywhere possible

This is the minimum credible insurer pilot posture.

### Production

Use:

- Managed Application in customer subscription
- App Service and Functions with VNet integration
- private endpoints for SQL, Storage, Key Vault, Search, OpenAI, Service Bus, and Document Intelligence where available and required
- disable public network access on back-end PaaS services where feasible
- optionally private App Service access behind Application Gateway/WAF
- Log Analytics retention and alerting configured centrally

## Suggested Bicep module breakdown

The current single-file `infra/mainTemplate.bicep` should become an orchestrator over modules like these:

### Managed application packaging

- `infra/mainTemplate.bicep`
- `infra/createUiDefinition.json`
- `infra/viewDefinition.json`

Responsibilities:

- top-level parameters
- environment selection
- feature flags
- module composition
- outputs required for Marketplace/managed application packaging

### Modules

- `infra/modules/monitoring.bicep`
  - Log Analytics
  - Application Insights
  - diagnostic settings

- `infra/modules/network.bicep`
  - VNet
  - subnets
  - private DNS zones
  - private endpoint wiring

- `infra/modules/appservice-plan.bicep`
  - shared App Service plan for portal and API where appropriate

- `infra/modules/portal-web.bicep`
  - App Service for Next.js portal
  - app settings
  - identity
  - optional private endpoint

- `infra/modules/api-web.bicep`
  - App Service for API
  - app settings
  - identity
  - health check path
  - deployment slots if enabled

- `infra/modules/functions-workflow.bicep`
  - Function App
  - Premium plan
  - storage linkage
  - Durable settings
  - identity

- `infra/modules/sql.bicep`
  - SQL server
  - database
  - firewall/private endpoint
  - Entra admin

- `infra/modules/storage.bicep`
  - storage account
  - blob containers
  - lifecycle rules
  - private endpoint

- `infra/modules/servicebus.bicep`
  - namespace
  - queues
  - private endpoint

- `infra/modules/search.bicep`
  - Azure AI Search
  - private endpoint

- `infra/modules/openai.bicep`
  - Azure OpenAI account
  - model deployment parameters
  - private endpoint

- `infra/modules/document-intelligence.bicep`
  - Document Intelligence resource
  - private endpoint if used

- `infra/modules/keyvault.bicep`
  - Key Vault
  - access policies or RBAC-ready configuration
  - secret placeholders
  - private endpoint

- `infra/modules/security-edge.bicep`
  - optional Application Gateway/WAF
  - optional APIM

## App configuration target

These settings should move from local placeholders to Azure app settings and Key Vault references:

### API app settings

- `INSURANCEINTELLIGENCE_STORAGE_PROVIDER=SqlServer`
- `INSURANCEINTELLIGENCE_AUTH_MODE=Entra`
- `INSURANCEINTELLIGENCE_SQLSERVER_CONNECTION_STRING`
- `INSURANCEINTELLIGENCE_AI_PROVIDER=AzureOpenAI`
- `INSURANCEINTELLIGENCE_RETRIEVAL_PROVIDER=AzureAiSearch`
- `INSURANCEINTELLIGENCE_DOCUMENT_PROVIDER=DocumentIntelligence`
- `INSURANCEINTELLIGENCE_EMAIL_PROVIDER=GraphEmail`
- `INSURANCEINTELLIGENCE_COMMUNICATION_PROVIDER=AzureCommunicationServicesEmail`
- `INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER=Teams`
- `Portal__CorsOrigin`

### Function app settings

- shared database connection setting
- Service Bus connection
- storage settings
- Durable Functions settings
- provider settings for Graph, Search, OpenAI, and Document Intelligence

### Portal app settings

- `NEXT_PUBLIC_API_BASE_URL`
- `NEXT_PUBLIC_TENANT_ID` only for demo or internal environments
- `NEXT_PUBLIC_WORKSPACE_ID` only for demo or internal environments
- `NEXT_PUBLIC_AUTH_MODE`

Production portal auth should move away from fixed demo values and rely on real login/session flow.

## Environment topology

### Dev topology

One shared internal subscription:

- 1 resource group per environment
- 1 App Service plan hosting portal and API
- 1 Functions Premium app
- 1 small SQL database
- 1 small storage account
- mock providers still allowed

Purpose:

- team development
- CI validation
- integration checks

### Test topology

One internal preproduction deployment:

- separate resource group
- same service shape as production
- lower SKUs
- realistic Entra, SQL, Key Vault, and messaging wiring

Purpose:

- release verification
- UAT
- managed app packaging validation

### Production topology

Per insurer:

- dedicated Managed Application deployment
- dedicated resource group(s) in customer subscription
- dedicated SQL database
- dedicated storage account
- dedicated Key Vault
- dedicated Service Bus
- dedicated AI Search index/service according to scale and isolation requirements
- dedicated OpenAI resource or controlled shared model estate, depending commercial and data-boundary policy

Purpose:

- hard tenant boundary between insurers
- insurer-specific networking and residency controls

## Cheapest credible pilot SKU set

This is the lowest-cost Azure shape that is still commercially credible for a pilot.

### Pilot baseline

- Portal App Service: Standard `S1`
- API App Service: Standard `S1`
- Functions: Premium `EP1`
- Azure SQL Database: General Purpose, small vCore tier
- Storage: Standard `LRS`
- Service Bus: `Standard`
- Azure AI Search: smallest tier that supports your expected index/query load and required features
- Azure OpenAI: pay-per-use model deployments, region permitting
- Document Intelligence: pay-per-use
- Key Vault: standard
- Log Analytics + App Insights: low retention defaults initially, increase for regulated deployments

### Why not cheaper than this

- Functions Consumption is weaker for enterprise workflow responsiveness and networking control
- Free/shared web tiers are not credible for insurer pilots
- Basic Service Bus is usually too limiting once async workflows matter
- In-memory local/test storage is not a pilot production store

## Production baseline

For a real insurer production starting point:

- Portal App Service: Premium v3
- API App Service: Premium v3
- Functions: Premium EP1 or EP2
- Azure SQL Database: General Purpose or Business Critical based on SLA and IO needs
- Storage: Standard GRS or ZRS based on resilience needs
- Service Bus: Standard or Premium depending volume and isolation requirements
- AI Search: tier sized for corpus and query concurrency
- Application Gateway/WAF if customer requires controlled ingress

Do not pre-commit higher-cost SKUs before real workload numbers exist.

## Concrete implementation order

1. Expand `infra/mainTemplate.bicep` into module orchestration.
2. Add App Service resources for portal and API.
3. Add Azure SQL and move production storage provider to SQL-backed mode.
4. Add Key Vault and managed identity wiring.
5. Add Function App + Service Bus for workflow offload.
6. Add Storage containers for documents and exports.
7. Add AI Search, OpenAI, and Document Intelligence resources behind provider settings.
8. Add private endpoints and DNS modules.
9. Add Managed Application packaging refinements and customer-facing deployment inputs.

## Non-recommendations for this repo

Do not make these the primary V1 hosting model:

- AKS
- broad microservice decomposition
- Cosmos DB-first transactional design
- Static Web Apps as the main insurer-grade portal host
- autonomous agent runtime replacing deterministic workflow orchestration

## Decision summary

If you want the shortest path from this repo to a real Azure offering, the target should be:

- Azure Managed Application
- App Service for portal
- App Service for API
- Functions Premium for workflow
- Azure SQL
- Storage
- Service Bus
- Key Vault
- Azure AI Search
- Azure OpenAI
- Document Intelligence
- private networking added by environment maturity and customer requirement

That is the best fit for the code you have now and the product contract you have written.
