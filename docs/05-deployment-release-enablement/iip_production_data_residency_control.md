# IIP Production Data Residency Control

**Status:** Technical enforcement implemented; production jurisdiction decision pending legal, security, and customer approval  
**Date:** 2026-07-13  
**Applies to:** `Production` and `ManagedApplicationReviewer`

## 1. Control Objective

IIP must not infer production data residency from an insurer's business address or from a development deployment. Before a protected production runtime starts, an approved decision record must identify:

- the residency country or jurisdiction;
- the Azure geography or data boundary;
- every allowed Azure deployment region, including any disaster-recovery or storage-replica region;
- the processing mode: `regional`, `data_zone`, or `global`;
- a non-secret legal/security approval reference.

The code does not choose a jurisdiction. Legal, security, the customer, and the Azure subscription owner must approve the values for each production deployment.

## 2. Enforced Settings

| Setting | Purpose |
|---|---|
| `INSURANCEINTELLIGENCE_AZURE_REGION` | Actual region of the deployed IIP runtime. |
| `INSURANCEINTELLIGENCE_DATA_RESIDENCY_COUNTRY` | Approved country or jurisdiction label. |
| `INSURANCEINTELLIGENCE_DATA_RESIDENCY_GEOGRAPHY` | Approved Azure geography or regulatory boundary. |
| `INSURANCEINTELLIGENCE_DATA_RESIDENCY_MODE` | `regional`, `data_zone`, or `global`. |
| `INSURANCEINTELLIGENCE_DATA_RESIDENCY_ALLOWED_AZURE_REGIONS` | Comma-separated region allowlist. |
| `INSURANCEINTELLIGENCE_DATA_RESIDENCY_APPROVAL_REFERENCE` | Non-secret decision, ticket, or contract reference. |
| `AzureOpenAI:DeploymentType` | Actual model deployment type: `Standard`, `DataZoneStandard`, or `GlobalStandard`. |

`Production` and `ManagedApplicationReviewer` fail startup when required values are missing, the actual Azure region is outside the allowlist, or the Azure OpenAI deployment type does not match the residency mode. The compliance posture API reports `Compliant`, `Mismatch`, or `Not configured`. It separately reports the tenant's business country/region and the Azure residency decision.

## 3. Azure Service Rules

| Service | Residency rule |
|---|---|
| Azure SQL | Deploy in an approved region. Do not configure active geo-replication until the secondary region is separately approved and allowlisted. |
| Blob Storage | `Standard_LRS` is the template default. `ZRS` requires zone support in the approved region. `GRS/GZRS` require the paired secondary region to be approved and recorded before deployment. |
| Azure AI Search | Treat each service as single-region. A multi-region design requires separate services and application routing. Keep Search with the model/provider region where the approved service availability permits. |
| Azure OpenAI / Foundry | Match deployment type to the decision: regional `Standard` for regional processing, `DataZoneStandard` only for an approved supported data zone, and `GlobalStandard` only when global processing is explicitly approved. The current template creates the account but not model deployments, so this must be verified when the deployment is attached. |
| Document Intelligence | Deploy the account in an approved region and confirm model availability there. |
| Service Bus, Key Vault, App Service, Functions, Log Analytics, Application Insights | Deploy in approved regions and review any service-specific replication, backup, retention, and disaster-recovery configuration. |
| Microsoft Entra ID and Microsoft 365/Graph | Treat as external platform boundaries with their own tenant and Microsoft 365 data-location commitments; do not represent the IIP Azure-region control as proof of their residency. |

Microsoft documents that Global model deployments may process inference in any Azure region, Data Zone deployments restrict processing to a supported zone, and Standard deployments process in the deployment region: [Foundry deployment types](https://learn.microsoft.com/en-us/azure/ai-foundry/foundry-models/concepts/deployment-types?view=foundry-classic).

Azure Storage documents that GRS/GZRS copy data to a secondary region while LRS keeps copies in the primary region: [Azure Storage redundancy](https://learn.microsoft.com/en-us/azure/storage/common/storage-redundancy).

Azure AI Search is a regional service unless the customer builds a multi-region topology: [Azure AI Search reliability](https://learn.microsoft.com/en-us/azure/reliability/reliability-ai-search). Azure geographies are designed around residency and compliance boundaries: [Azure data-store geography guidance](https://learn.microsoft.com/en-us/azure/architecture/guide/technology-choices/data-stores-getting-started).

## 4. Production Decision Record

Complete this outside source control if it contains customer-sensitive terms. Commit only the non-secret approval reference.

| Field | Approved value |
|---|---|
| Customer/insurer | Pending |
| Residency country/jurisdiction | Pending |
| Azure geography/data boundary | Pending |
| Primary Azure region | Pending |
| Approved secondary/DR regions | Pending or none |
| Residency mode | Pending |
| Storage SKU | `Standard_LRS` unless another mode is approved |
| Azure OpenAI deployment type | Pending |
| Approval reference | Pending |
| Legal approver/date | Pending |
| Security approver/date | Pending |
| Customer approver/date | Pending |

## 5. Go/No-Go Procedure

1. Confirm required Azure services and model capacity exist in the proposed region.
2. Obtain legal, security, and customer approval for the complete primary/secondary region set.
3. Set the residency runtime values, `AzureOpenAI:DeploymentType`, and the matching Bicep parameters.
4. Keep `storageAccountSkuName = 'Standard_LRS'` unless replication regions are explicitly approved.
5. Verify every Azure OpenAI model deployment type matches `dataResidencyMode`.
6. Run Bicep build, deployment validation, and what-if against the intended production resource group.
7. Start the protected runtime and verify `/health/ready` succeeds.
8. Verify `GET /api/v1/compliance/posture` reports `Compliant` for the authenticated tenant.
9. Record resource locations, storage redundancy, SQL replica state, model deployment type, and the approval reference in release evidence.
10. Stop the release on any missing value, region mismatch, unapproved replica, or global model deployment under a regional decision.

## 6. Current Position

The technical enforcement is implemented. No production jurisdiction or Azure region has been selected by this change, no production resources were created, and the development deployment is not production residency evidence. Production remains gated until the decision record is approved and its values are supplied to the deployment.

