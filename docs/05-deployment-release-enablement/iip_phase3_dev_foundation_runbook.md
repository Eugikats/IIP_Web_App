# IIP Phase 3 Dev Foundation Runbook

**Scope:** Phase 3.5 and the infrastructure half of Phase 3.6 from `docs/08-program-plan/IIP-CONSOLIDATED-ROADMAP.md`.

This runbook provisions the shared **dev-only** Azure foundation in `rg-iip-dev` on the Sponsored subscription while PAYG is deferred. It does not attempt staging, production, Azure OpenAI, or Azure Document Intelligence.

## Boundaries

- Target resource group: `rg-iip-dev`.
- Target region: `westus2`.
- Existing compute plan: `asp-iip-dev` Basic Linux.
- Runtime profile: `Development`.
- Real providers enabled: Entra auth, Azure SQL, Azure Blob Storage, Azure AI Search, Azure Service Bus, Key Vault, Application Insights.
- Intentionally not configured: Azure OpenAI, Azure Document Intelligence, Graph/email, outbound communications, notifications.
- No mock provider is configured for the API deploy target; missing providers are represented as `NotConfigured` or `Disabled`.

## Prerequisites

1. Azure CLI is logged into the Sponsored subscription:

   ```powershell
   az account set --subscription 5f5765ae-985f-42e4-a443-8e2447be33ca
   ```

2. `rg-iip-dev` exists in `westus2`.
3. `asp-iip-dev` exists in `rg-iip-dev`.
4. A strong one-time SQL administrator password is available locally as an environment variable. Do not commit it. The deployment also configures the signed-in operator as the Azure SQL Entra administrator, so day-to-day access should use Entra/managed identity rather than this SQL password.

   ```powershell
   $env:IIP_DEV_SQL_ADMIN_PASSWORD = '<secure-password>'
   ```

5. If a different Azure operator runs the deployment, override the SQL Entra admin values:

   ```powershell
   $env:IIP_DEV_SQL_ENTRA_ADMIN_LOGIN = '<user-principal-name>'
   $env:IIP_DEV_SQL_ENTRA_ADMIN_OBJECT_ID = '<entra-object-id>'
   ```

## Validate the template

```powershell
az bicep build --file infra/bicep/phase3-dev-foundation.bicep
```

## Preview

```powershell
az deployment group what-if `
  --resource-group rg-iip-dev `
  --template-file infra/bicep/phase3-dev-foundation.bicep `
  --parameters infra/bicep/phase3-dev-foundation.dev.bicepparam
```

## Provision

```powershell
az deployment group create `
  --resource-group rg-iip-dev `
  --template-file infra/bicep/phase3-dev-foundation.bicep `
  --parameters infra/bicep/phase3-dev-foundation.dev.bicepparam
```

Capture the deployment outputs. The outputs include the API/portal URLs, managed identity principal IDs, and the manual grants required before the API can pass readiness.

## Manual grants after provisioning

The Bicep template intentionally does not create IAM role assignments. A human operator must grant least-privilege access:

1. API managed identity:
   - Key Vault Secrets User on the dev Key Vault.
   - Storage Blob Data Contributor on the dev Storage Account.
   - Search Index Data Contributor and any required Search service role on the dev Search service.
   - Azure Service Bus Data Sender and Azure Service Bus Data Receiver on the dev Service Bus namespace.

2. Azure SQL:
   - Confirm the deployment configured the intended Microsoft Entra administrator on the SQL logical server.
   - Create the API managed identity as a contained database user using Entra admin access.
   - Grant `db_datareader`, `db_datawriter`, and for the current `Development` runtime only, `db_ddladmin`.
   - `db_ddladmin` is required because the current infrastructure startup path calls EF `EnsureCreatedAsync` when SQL storage is enabled. Remove or narrow this grant when schema management moves to migrations/deployment automation.

   ```sql
   CREATE USER [app-api-dev-h5girbnzrayqe] FROM EXTERNAL PROVIDER;
   ALTER ROLE db_datareader ADD MEMBER [app-api-dev-h5girbnzrayqe];
   ALTER ROLE db_datawriter ADD MEMBER [app-api-dev-h5girbnzrayqe];
   ALTER ROLE db_ddladmin ADD MEMBER [app-api-dev-h5girbnzrayqe];
   ```

3. Microsoft Entra app registration:
   - Add the emitted portal URL to the portal app redirect URIs.
   - Confirm the portal SPA still requests `api://d55fe46b-1fba-4c5b-81e1-4ee1918ecf73/access_as_user`.

## Current dev deployment output (2026-07-07)

Deployment `phase3-dev-foundation` succeeded in `rg-iip-dev`.

| Output | Value |
|---|---|
| API app | `app-api-dev-h5girbnzrayqe` |
| API URL | `https://app-api-dev-h5girbnzrayqe.azurewebsites.net` |
| API managed identity principal ID | `549d4a78-d4b2-48d1-94e3-4586b08853a9` |
| Portal app | `app-portal-dev-h5girbnzrayqe` |
| Portal URL | `https://app-portal-dev-h5girbnzrayqe.azurewebsites.net` |
| Portal managed identity principal ID | `9a13612d-e35f-41dd-b40a-a50f28c21844` |
| Portal Entra app | `iip-portal-stg` |
| Portal Entra app client ID | `3b3522e4-13fd-4bd9-8f08-a1cedf428d0d` |
| Key Vault | `kv-iip-dev-zcxolwov` |
| Storage Account | `stiiph5girbnzrayqe` |
| SQL Server | `sql-dev-h5girbnzrayqe` |
| SQL Database | `insuranceintelligence` |
| AI Search | `srch-dev-h5girbnzrayqe` |
| AI Search managed identity principal ID | `ce3bc36a-4873-47cd-911d-5cd2a230bd38` |
| Service Bus namespace | `sb-dev-h5girbnzrayqe` |

Completed after provisioning:

- API managed identity granted `Key Vault Secrets User` on `kv-iip-dev-zcxolwov`.
- API managed identity granted `Storage Blob Data Contributor` on `stiiph5girbnzrayqe`.
- API managed identity granted `Search Index Data Contributor` and `Search Service Contributor` on `srch-dev-h5girbnzrayqe`.
- API managed identity granted `Azure Service Bus Data Sender` and `Azure Service Bus Data Receiver` on `sb-dev-h5girbnzrayqe`.
- API managed identity created as SQL contained user `app-api-dev-h5girbnzrayqe` and granted `db_datareader` / `db_datawriter` / `db_ddladmin`.
- Tenant-owner/admin account `OticGroup@oticgroupug.onmicrosoft.com` added `harrygafa0_gmail.com#EXT#@oticgroupug.onmicrosoft.com` as an owner of the original `iip-portal-stg` app registration.
- The original `iip-portal-stg` app registration has SPA redirect URIs for:
  - `https://app-portal-dev-h5girbnzrayqe.azurewebsites.net`
  - `https://app-portal-dev-h5girbnzrayqe.azurewebsites.net/`
- The dev portal App Service settings were updated to use the original `iip-portal-stg` client ID `3b3522e4-13fd-4bd9-8f08-a1cedf428d0d`.

## Phase 3.6 app deployment output (2026-07-07)

Backend validation and deployment:

- `dotnet build InsuranceIntelligence.sln` passed with existing obsolete-provider warnings in local/test paths.
- API artifact: `C:\tmp\iip-phase3-deploy-20260707-182700\api.zip` (`20,522,058` bytes).
- API zip deployed to `app-api-dev-h5girbnzrayqe`.
- Initial API startup failed with `CREATE TABLE permission denied in database 'insuranceintelligence'`.
- After granting the API managed identity `db_ddladmin`, API startup and readiness passed.

API readiness evidence:

```json
{"status":"ready","environment":"Development","hostEnvironment":"Development","storageProvider":"SqlServer","blobProvider":"AzureBlob","aiProvider":"NotConfigured","documentProvider":"NotConfigured","retrievalProvider":"AzureAiSearch","queueProvider":"AzureServiceBus","emailProvider":"Disabled","communicationProvider":"Disabled","notificationProvider":"Disabled","seededBusinessData":false,"errors":[]}
```

Portal validation and deployment:

- Portal build produced a Next standalone output under `web/portal/.next/standalone`.
- Direct standalone zip packages failed on Azure App Service because pnpm symlink-style `node_modules` did not survive App Service's node_modules relocation.
- Final deployed portal artifact was staged in `C:\tmp\iip-portal-hoisted-20260707` with production dependencies installed using `pnpm install --prod --frozen-lockfile --config.node-linker=hoisted`.
- Portal artifact: `C:\tmp\iip-phase3-deploy-20260707-182700\portal-hoisted.zip` (`120,766,236` bytes).
- Startup log evidence showed `Next.js 15.5.18`, `Ready in 1224ms`, and `Site startup probe succeeded`.
- `https://app-portal-dev-h5girbnzrayqe.azurewebsites.net/app` returned HTTP `200`.

## Phase 3.7 smoke target

After app artifacts are deployed to the created web apps and manual grants are complete:

1. Portal signs in through Entra.
2. API validates the bearer token.
3. `/api/v1/me` resolves tenant/workspace from the membership store.
4. `/health/ready` proves SQL connectivity and provider readiness.
5. Provider status shows OpenAI, Document Intelligence, Graph/email, communications, and notifications as not configured or disabled, not simulated.

## Phase 3.7 auth smoke progress (2026-07-08)

Completed:

- `web/portal/lib/lovable-parity/data/environment.ts` now obtains an Entra API access token before calling `/api/v1/me`. If token acquisition has not completed, the portal keeps the environment banner unverified instead of issuing an unauthenticated `/me` request.
- The portal app setting `NEXT_PUBLIC_ENTRA_REDIRECT_URI` is now `https://app-portal-dev-h5girbnzrayqe.azurewebsites.net/app`.
- The original `iip-portal-stg` SPA redirect URI list now includes `https://app-portal-dev-h5girbnzrayqe.azurewebsites.net/app` while preserving the prior dev and staging URIs.
- The corrected portal bundle was rebuilt with `NEXT_PUBLIC_ENTRA_REDIRECT_URI=https://app-portal-dev-h5girbnzrayqe.azurewebsites.net/app`.
- Final corrected portal artifact: `C:\tmp\iip-phase3-deploy-20260707-182700\portal-redirectapp-hoisted.zip` (`120,885,349` bytes).
- Deployment to `app-portal-dev-h5girbnzrayqe` completed with deployment ID `8cceac93-d7ad-4d0f-a86b-6290ef1dfefe` and status `RuntimeSuccessful`.
- The deployed client artifact contains:
  - API base URL `https://app-api-dev-h5girbnzrayqe.azurewebsites.net`.
  - API scope `api://d55fe46b-1fba-4c5b-81e1-4ee1918ecf73/access_as_user`.
  - Redirect URI `https://app-portal-dev-h5girbnzrayqe.azurewebsites.net/app`.
- API CORS remains restricted to `https://app-portal-dev-h5girbnzrayqe.azurewebsites.net`.
- `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/health/ready` returned HTTP `200` with `environment=Development`, SQL Server storage, Azure Blob, Azure AI Search, Azure Service Bus, `AzureOpenAI=NotConfigured`, `DocumentIntelligence=NotConfigured`, email/communications/notifications disabled, and `seededBusinessData=false`.
- `GET https://app-portal-dev-h5girbnzrayqe.azurewebsites.net/app?smoke=http-20260708` returned HTTP `200`.
- Unauthenticated `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/api/v1/me` returned HTTP `401`, confirming `/me` remains bearer-token protected.
- Dev SQL membership was seeded for the signed-in external user:
  - User object ID `5b303954-53fe-45de-b094-c8f70e4aef1c`.
  - Tenant ID `dcb0b132-fb9e-475c-a68e-b0671222e8d8`.
  - Workspace ID `workspace-operations`.
  - Role `Admin`.
- The temporary local membership smoke helper under `tools/tmp-iip-membership-smoke` was removed after seeding and is not part of the repo deliverable.

Remaining smoke gap:

- A browser-mediated Entra sign-in previously reached Microsoft login with the correct portal client ID and API scope, but returned to `/` because the redirect URI was root. The `/app` redirect blocker is now fixed in Entra, App Service settings, and the rebuilt client bundle.
- Post-fix Chrome automation timed out during redirect handling before a live post-login `/api/v1/me` response could be captured. The remaining manual/live verification item is to open `https://app-portal-dev-h5girbnzrayqe.azurewebsites.net/app`, sign in as `harrygafa0@gmail.com`, and confirm `/api/v1/me` resolves `Source=MembershipStore`, tenant `dcb0b132-fb9e-475c-a68e-b0671222e8d8`, workspace `workspace-operations`, and role `Admin`.

## Phase 3.7a auth shell cleanup deployment (2026-07-08)

Completed:

- Added a shared portal runtime-context provider backed by `GET /api/v1/me`.
- Root login now starts the real Entra/MSAL token flow directly instead of linking blindly to `/app`.
- Root login no longer renders deployed-mode prototype tenant/workspace/domain labels:
  - `Global Insurance Co.`
  - `Motor Ops · Production`
  - `global-ins.onmicrosoft.com`
- Shell chrome no longer renders deployed-mode prototype identity labels:
  - `Otic Intelligence`
  - `Global Insurance Co.`
  - frontend role-fixture user names in sidebar/topbar once `/me` resolves
- Sidebar/topbar now render workspace, user, role, environment, and available workspace state from `/api/v1/me`; before runtime confirmation they show cautious resolving/unverified states.
- The preview role switcher is hidden outside local test doubles and `LovableVisualParity` mode.

Validation and deployment:

- `pnpm lint` passed with the existing `Sidebar.tsx` `<img>` warning.
- `pnpm build` passed with the same existing warning.
- `pnpm run check:runtime-data` passed.
- Deployed artifact: `C:\tmp\iip-phase3-deploy-20260707-182700\portal-authshell-hoisted.zip` (`120,882,762` bytes).
- Deployment to `app-portal-dev-h5girbnzrayqe` completed with deployment ID `54ea6277-a345-42b1-9ccb-d72ce7355c01` and status `RuntimeSuccessful`.
- `GET https://app-portal-dev-h5girbnzrayqe.azurewebsites.net/?smoke=authshell-20260708` returned HTTP `200`.
- The deployed login HTML does not contain `Global Insurance Co.` or `Motor Ops` and does contain `Workspace resolved server-side`.
- `GET https://app-portal-dev-h5girbnzrayqe.azurewebsites.net/app?smoke=authshell-20260708` returned HTTP `200`.

Remaining acceptance proof:

- Complete a browser sign-in as `harrygafa0@gmail.com` and capture `GET /api/v1/me` returning `Source=MembershipStore`, tenant `dcb0b132-fb9e-475c-a68e-b0671222e8d8`, workspace `workspace-operations`, and role `Admin`.
- Non-shell prototype labels still exist in screen-specific backlog areas such as Admin/Workspaces/ClientPortal; those remain part of the screen-by-screen Lovable reconciliation backlog, not the auth-shell gate.

## Phase 3.7 auth blocker remediation (2026-07-08)

Observed during live browser proof:

- The deployed login screen reached Microsoft sign-in with the expected portal client ID `3b3522e4-13fd-4bd9-8f08-a1cedf428d0d`, API scope `api://d55fe46b-1fba-4c5b-81e1-4ee1918ecf73/access_as_user`, and redirect URI `https://app-portal-dev-h5girbnzrayqe.azurewebsites.net/app`.
- After redirect to `/app`, the shell remained fail-closed with `/api/v1/me failed with HTTP 401`.

Remediations deployed:

- Portal runtime context now attaches the freshly acquired MSAL access token explicitly to the first `GET /api/v1/me` call.
- API JWT audience validation now accepts only the configured API Application ID URI and the same API app's bare client ID:
  - `api://d55fe46b-1fba-4c5b-81e1-4ee1918ecf73`
  - `d55fe46b-1fba-4c5b-81e1-4ee1918ecf73`
- API JWT issuer validation is explicit and still fail-closed: accepted issuers must match `https://login.microsoftonline.com/{tenant-guid}/v2.0`.

Validation:

- `pnpm lint` passed with the existing `Sidebar.tsx` `<img>` warning.
- `pnpm build` passed after clearing a stale local `.next` artifact.
- `dotnet build InsuranceIntelligence.sln` passed with existing obsolete-provider warnings in test/local paths.
- `dotnet test tests\InsuranceIntelligence.Api.Tests\InsuranceIntelligence.Api.Tests.csproj --no-restore` passed: `238` passed, `0` failed.
- Portal auth-fix artifact deployed to `app-portal-dev-h5girbnzrayqe` with deployment ID `9a7552e0-03e9-4f96-bf33-6a9921e8903e` and status `RuntimeSuccessful`.
- API auth audience-only artifact deployed with deployment ID `f5f8aa28-fb7a-421c-9be5-40a979f1af12` and status `RuntimeSuccessful`.
- API auth issuer+audience artifact deployed with deployment ID `03224a57-ef27-42c2-8f5f-02152d73b74a` and status `RuntimeSuccessful`.
- `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/health/ready` returned HTTP `200` after the final API deployment.

Final browser acceptance proof:

- Fresh Chrome tab opened `https://app-portal-dev-h5girbnzrayqe.azurewebsites.net/app?proof=phase37-20260708`.
- Microsoft sign-in used the expected authorize URL:
  - Portal client ID `3b3522e4-13fd-4bd9-8f08-a1cedf428d0d`.
  - API scope `api://d55fe46b-1fba-4c5b-81e1-4ee1918ecf73/access_as_user`.
  - Redirect URI `https://app-portal-dev-h5girbnzrayqe.azurewebsites.net/app`.
- Selected signed-in account `harrygafa0@gmail.com`.
- Browser returned to the live portal `/app`.
- Settled shell state showed:
  - `workspace-operations`.
  - `MembershipStore`.
  - `harrygafa0`.
  - `Tenant Admin`.
  - No `Runtime context error`.
  - No `Workspace not configured`.
  - No `User unavailable`.
  - Provider health remained honestly `Not configured` for disabled/unconfigured providers.

Phase 3.7 Gate-1 live proof is accepted for the dev environment.

## Phase 4 dev redeploy smoke progress (2026-07-08)

Prepared artifacts:

- API artifact: `C:\tmp\iip-phase4-deploy-20260708\api.zip` (`20,535,764` bytes).
- Portal artifact: `C:\tmp\iip-phase4-deploy-20260708\portal-hoisted.zip` (`120,901,229` bytes).
- Portal archive validation: `tar.exe -tf` exited `0`, listed `24,799` entries, and contained `./server.js`, `./package.json`, `./.next/static/`, and `./node_modules/next/package.json`.

Deployment:

- API deployed to `app-api-dev-h5girbnzrayqe` with deployment ID `2c65b925-c2be-4a76-8a4e-8281639bf941`; status `RuntimeSuccessful`; `0` failed instances; `1` successful instance.
- Portal deployed to `app-portal-dev-h5girbnzrayqe` with deployment ID `3317b78a-7108-40cc-82ae-d70c26d01a8f`; status `RuntimeSuccessful`; `0` failed instances; `1` successful instance.

Unauthenticated live smoke:

- `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/health/ready` returned HTTP `200`.
- Unauthenticated `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/api/v1/me` returned HTTP `401`.
- `GET https://app-portal-dev-h5girbnzrayqe.azurewebsites.net/app?smoke=phase4-20260708` returned HTTP `200`.

Remaining Phase 4 functional smoke blocker:

- `az account get-access-token --scope api://d55fe46b-1fba-4c5b-81e1-4ee1918ecf73/access_as_user` fails with `AADSTS65001 consent_required` for the Microsoft Azure CLI app.
- An attempted interactive `az login --tenant dcb0b132-fb9e-475c-a68e-b0671222e8d8 --scope api://d55fe46b-1fba-4c5b-81e1-4ee1918ecf73/access_as_user` did not complete before timeout.
- Chrome reached Microsoft sign-in for the deployed portal rather than an authenticated `/app` session.
- Next unblock action: complete real Entra sign-in/consent, then run the authenticated FNOL -> queue -> case review -> approval/customer update -> evidence-pack path against the live dev API, Azure SQL, and Blob-backed evidence storage.

## Phase 5 document tenant-scope hardening deployment (2026-07-08)

Code hardening:

- Case-scoped document endpoints now prefer the server-resolved `ITenantContextAccessor` tenant/workspace/user before any local-test header fallback.
- `ClaimAccessGuard` now evaluates resolved membership user/role codes before demo identity headers when a tenant context is present.
- Regression coverage proves spoofed browser `X-Tenant-Id`, `X-Workspace-Id`, `X-User-Id`, and `x-demo-user-id` headers do not override membership context during document upload.

Validation:

- `dotnet test tests\InsuranceIntelligence.Api.Tests\InsuranceIntelligence.Api.Tests.csproj --no-restore` passed: `246` passed, `0` failed.

Deployment:

- API artifact: `C:\tmp\iip-phase5-docscope-20260708\api.zip` (`20,536,552` bytes).
- API deployed to `app-api-dev-h5girbnzrayqe` with deployment ID `d85b3ba3-2cdb-44ce-ae6f-90f47e29f6b5`; status `RuntimeSuccessful`; `0` failed instances; `1` successful instance.

Post-deploy smoke:

- `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/health/ready` returned HTTP `200`.
- Unauthenticated `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/api/v1/me` returned HTTP `401`.

## Phase 7 legacy UI-context cleanup deployment (2026-07-08)

Code cleanup:

- Removed the unmapped legacy `UiContextEndpoints` route handler residue after confirming `Program.cs` maps `/api/v1/me` to `RuntimeContextEndpoints.GetMe` and the portal runtime context client calls `/api/v1/me`.
- Moved the shared `UiEnvironmentContext` DTO into its own file so active runtime-context responses keep their API shape.
- Removed obsolete endpoint-only tests for the deleted handler; runtime-context tests remain authoritative for `/api/v1/me`.

Validation:

- `dotnet test tests\InsuranceIntelligence.Api.Tests\InsuranceIntelligence.Api.Tests.csproj --no-restore` passed: `254` passed, `0` failed.

Deployment:

- API artifact: `C:\tmp\iip-phase7-uicontext-cleanup-20260708\api.zip` (`20,528,805` bytes).
- API deployed to `app-api-dev-h5girbnzrayqe` with deployment ID `e71aaeaf-7ecb-4f2a-95ec-60099a09bdaa`; status `RuntimeSuccessful`; `0` failed instances; `1` successful instance.

Post-deploy smoke:

- `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/health/ready` returned HTTP `200`.
- Unauthenticated `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/api/v1/me` returned HTTP `401`.

## Phase 7 runtime context tenant-scope hardening deployment (2026-07-08)

Code hardening:

- `/api/v1/me` now prefers an already server-resolved `ITenantContextAccessor` tenant/workspace/user before any Development-mode header fallback.
- Protected runtime membership-store behavior remains fail-closed and unchanged.
- Regression coverage proves spoofed browser `X-Tenant-Id`, `X-Workspace-Id`, `x-demo-role`, and `x-demo-user-id` headers do not override resolved runtime context.

Validation:

- `dotnet test tests\InsuranceIntelligence.Api.Tests\InsuranceIntelligence.Api.Tests.csproj --no-restore` passed: `257` passed, `0` failed.

Deployment:

- API artifact: `C:\tmp\iip-phase7-runtimecontext-20260708\api.zip` (`20,537,264` bytes).
- API deployed to `app-api-dev-h5girbnzrayqe` with deployment ID `0373fc2e-2bb6-4acb-9ca8-f20ef3fbfbfd`; status `RuntimeSuccessful`; `0` failed instances; `1` successful instance.

Post-deploy smoke:

- `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/health/ready` returned HTTP `200`.
- Unauthenticated `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/api/v1/me` returned HTTP `401`.

## Phase 7 Command Center tenant-scope hardening deployment (2026-07-08)

Code hardening:

- `POST /api/v1/command-center/query` now preserves an already server-resolved `ITenantContextAccessor` tenant/workspace/user before any Development-mode header fallback.
- Protected runtimes continue to fail closed when no middleware-resolved membership context is present.
- Regression coverage proves spoofed browser `X-Tenant-Id`, `X-Workspace-Id`, `X-User-Id`, and `x-demo-user-id` headers do not override membership context or action-log scope during command execution.

Validation:

- `dotnet test tests\InsuranceIntelligence.Api.Tests\InsuranceIntelligence.Api.Tests.csproj --no-restore` passed: `256` passed, `0` failed.

Deployment:

- API artifact: `C:\tmp\iip-phase7-commandcenterscope-20260708\api.zip` (`20,537,065` bytes).
- API deployed to `app-api-dev-h5girbnzrayqe` with deployment ID `94d9be46-4e90-4b01-a0f4-7c2d9f3b02b1`; status `RuntimeSuccessful`; `0` failed instances; `1` successful instance.

Post-deploy smoke:

- `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/health/ready` returned HTTP `200`.
- Unauthenticated `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/api/v1/me` returned HTTP `401`.

## Phase 7 admin tenant-scope hardening deployment (2026-07-08)

Code hardening:

- Tenant configuration read/update, user list/create, and user role update endpoints now prefer the server-resolved `ITenantContextAccessor` tenant/workspace/user before any local-test header/query fallback.
- Admin mutation audit actor attribution now prefers the resolved membership user before demo identity headers.
- Regression coverage proves spoofed browser `X-Tenant-Id`, `X-Workspace-Id`, `X-User-Id`, and `x-demo-user-id` headers do not override membership context during tenant configuration update.

Validation:

- `dotnet test tests\InsuranceIntelligence.Api.Tests\InsuranceIntelligence.Api.Tests.csproj --no-restore` passed: `255` passed, `0` failed.

Deployment:

- API artifact: `C:\tmp\iip-phase7-adminscope-20260708\api.zip` (`20,537,069` bytes).
- API deployed to `app-api-dev-h5girbnzrayqe` with deployment ID `5b6fc663-e88a-4680-9a18-80f1429c8084`; status `RuntimeSuccessful`; `0` failed instances; `1` successful instance.

Post-deploy smoke:

- `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/health/ready` returned HTTP `200`.
- Unauthenticated `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/api/v1/me` returned HTTP `401`.

## Phase 7 email integration tenant-scope hardening deployment (2026-07-08)

Code hardening:

- Mailbox list/create, test ingestion, process-message, inbound-message list, and inbound-message detail endpoints now prefer the server-resolved `ITenantContextAccessor` tenant/workspace/user before any local-test header/query fallback.
- Integration mutation audit actor attribution now prefers the resolved membership user before demo identity headers.
- Regression coverage proves spoofed browser `X-Tenant-Id`, `X-Workspace-Id`, `X-User-Id`, and `x-demo-user-id` headers do not override membership context during mailbox configuration.

Validation:

- `dotnet test tests\InsuranceIntelligence.Api.Tests\InsuranceIntelligence.Api.Tests.csproj --no-restore` passed: `254` passed, `0` failed.

Deployment:

- API artifact: `C:\tmp\iip-phase7-integrationscope-20260708\api.zip` (`20,536,895` bytes).
- API deployed to `app-api-dev-h5girbnzrayqe` with deployment ID `36258345-bf69-4d49-9620-37e8bd75fe39`; status `RuntimeSuccessful`; `0` failed instances; `1` successful instance.

Post-deploy smoke:

- `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/health/ready` returned HTTP `200`.
- Unauthenticated `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/api/v1/me` returned HTTP `401`.

## Phase 7 claims tenant-scope hardening deployment (2026-07-08)

Code hardening:

- Claims list, detail, attention, daily-summary, and draft-context endpoints now preserve an already server-resolved `ITenantContextAccessor` tenant/workspace/user before any Development-mode header/query fallback.
- Protected runtimes continue to fail closed when no middleware-resolved membership context is present.
- Regression coverage proves spoofed browser `X-Tenant-Id`, `X-Workspace-Id`, and `x-demo-user-id` headers do not override membership context during claims list execution.

Validation:

- `dotnet test tests\InsuranceIntelligence.Api.Tests\InsuranceIntelligence.Api.Tests.csproj --no-restore` passed: `253` passed, `0` failed.

Deployment:

- API artifact: `C:\tmp\iip-phase7-claimsscope-20260708\api.zip` (`20,536,781` bytes).
- API deployed to `app-api-dev-h5girbnzrayqe` with deployment ID `8f90c02a-ccbb-47d8-a374-172478544514`; status `RuntimeSuccessful`; `0` failed instances; `1` successful instance.

Post-deploy smoke:

- `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/health/ready` returned HTTP `200`.
- Unauthenticated `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/api/v1/me` returned HTTP `401`.

## Phase 7 client-portal tenant-scope hardening deployment (2026-07-08)

Code hardening:

- Customer claim view and customer document upload endpoints now prefer the server-resolved `ITenantContextAccessor` tenant/workspace/user before any local-test header/query fallback.
- Client-portal customer ownership checks now use resolved membership/claims identity before demo identity headers.
- Client-portal uploaded-document attribution now uses the resolved membership actor when tenant context is present.
- Regression coverage proves spoofed browser `X-Tenant-Id`, `X-Workspace-Id`, `X-User-Id`, and `x-demo-user-id` headers do not override membership context during client document upload.

Validation:

- `dotnet test tests\InsuranceIntelligence.Api.Tests\InsuranceIntelligence.Api.Tests.csproj --no-restore` passed: `252` passed, `0` failed.

Deployment:

- API artifact: `C:\tmp\iip-phase7-clientportal-communicationsscope-20260708\api.zip` (`20,536,792` bytes).
- API deployed to `app-api-dev-h5girbnzrayqe` with deployment ID `62eba1ea-32fb-4cf7-86b1-46c534b21699`; status `RuntimeSuccessful`; `0` failed instances; `1` successful instance.
- This deployment supersedes the earlier same-day communications-only Kudu deployment `9be0c53e-28b7-44f5-aad3-15422dfb3f19` and includes both communications and client-portal hardening.

Post-deploy smoke:

- `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/health/ready` returned HTTP `200`.
- Unauthenticated `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/api/v1/me` returned HTTP `401`.

## Phase 7 communications tenant-scope hardening deployment (2026-07-08)

Code hardening:

- Email/SMS send endpoints now prefer the server-resolved `ITenantContextAccessor` tenant/workspace/user before any local-test header fallback.
- Teams notification audit now uses the same server-resolved scope path and resolved actor fallback.
- Communication history and case communication history now prefer server-resolved scope before any local-test header/query fallback.
- Customer communication records and audit events now attribute sends to the resolved membership actor rather than browser-supplied request body or demo user headers when tenant context is present.
- Regression coverage proves spoofed browser `X-Tenant-Id`, `X-Workspace-Id`, `X-User-Id`, and `x-demo-user-id` headers do not override membership context or actor attribution during customer communication send.

Validation:

- `dotnet test tests\InsuranceIntelligence.Api.Tests\InsuranceIntelligence.Api.Tests.csproj --no-restore` passed: `251` passed, `0` failed.

Deployment:

- API artifact: `C:\tmp\iip-phase7-communicationsscope-20260708\api.zip` (`20,536,829` bytes).
- API deployed to `app-api-dev-h5girbnzrayqe` with Kudu deployment ID `9be0c53e-28b7-44f5-aad3-15422dfb3f19`; status `4`; complete `True`.
- Local `az webapp deploy` timed out before returning a CLI result, but Kudu reported the OneDeploy deployment complete.

Post-deploy smoke:

- `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/health/ready` returned HTTP `200`.
- Unauthenticated `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/api/v1/me` returned HTTP `401`.

## Phase 7 reporting and metrics tenant-scope hardening deployment (2026-07-08)

Code hardening:

- Command Center metrics and operations-summary report endpoints now prefer the server-resolved `ITenantContextAccessor` tenant/workspace/user before any local-test header/query fallback.
- Regression coverage proves spoofed browser `X-Tenant-Id`, `X-Workspace-Id`, `X-User-Id`, and `x-demo-user-id` headers do not affect management metrics or reporting aggregates.

Validation:

- `dotnet test tests\InsuranceIntelligence.Api.Tests\InsuranceIntelligence.Api.Tests.csproj --no-restore` passed: `250` passed, `0` failed.

Deployment:

- API artifact: `C:\tmp\iip-phase7-metricsscope-20260708\api.zip` (`20,536,748` bytes).
- API deployed to `app-api-dev-h5girbnzrayqe` with deployment ID `7b6200b9-b74b-46dc-a5cd-18ba56e6e0f8`; status `RuntimeSuccessful`; `0` failed instances; `1` successful instance.

Post-deploy smoke:

- `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/health/ready` returned HTTP `200`.
- Unauthenticated `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/api/v1/me` returned HTTP `401`.

Remaining Phase 5 provider gate:

- Real Azure Document Intelligence extraction and Azure OpenAI review remain blocked by the Phase 3.4 subscription/provider capability decision. Keep them `Not configured` until a capable subscription and provider access are approved.

## Phase 7 workflow tenant-scope hardening deployment (2026-07-08)

Code hardening:

- Workflow timeline, recommended-actions, evaluation, task list/get/assign/complete endpoints now prefer the server-resolved `ITenantContextAccessor` tenant/workspace/user before any local-test header fallback.
- Approval list/get/create/decision/execute-next-step endpoints now use the same server-resolved workflow scope path.
- Customer-update draft/list/approve/reject/send endpoints now use the same server-resolved workflow scope path.
- Workflow actor resolution now prefers the resolved membership user before demo/browser headers when a tenant context is present.
- Regression coverage proves spoofed browser `X-Tenant-Id`, `X-Workspace-Id`, `X-User-Id`, and `x-demo-user-id` headers do not override membership context during workflow evaluation.

Validation:

- `dotnet test tests\InsuranceIntelligence.Api.Tests\InsuranceIntelligence.Api.Tests.csproj --no-restore` passed: `247` passed, `0` failed.

Deployment:

- API artifact: `C:\tmp\iip-phase7-workflowscope-20260708\api.zip` (`20,536,705` bytes).
- API deployed to `app-api-dev-h5girbnzrayqe` with deployment ID `7e6eed56-3a24-4884-9ac1-60c4bc9fbbf1`; status `RuntimeSuccessful`; `0` failed instances; `1` successful instance.

Post-deploy smoke:

- `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/health/ready` returned HTTP `200`.
- Unauthenticated `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/api/v1/me` returned HTTP `401`.

Remaining live proof gap:

- Authenticated Phase 4 functional smoke still requires real Entra sign-in/consent for the IIP API scope.

## Phase 7 compliance tenant-scope hardening deployment (2026-07-08)

Code hardening:

- Compliance audit-event list, audit-pack read, and evidence-pack export endpoints now prefer the server-resolved `ITenantContextAccessor` tenant/workspace/user before any local-test header/query fallback.
- Regression coverage proves spoofed browser `X-Tenant-Id`, `X-Workspace-Id`, `X-User-Id`, and `x-demo-user-id` headers do not override membership context during evidence-pack export.
- Evidence-pack storage requests and `evidence_pack.exported` audit events use the resolved membership scope/user.

Validation:

- `dotnet test tests\InsuranceIntelligence.Api.Tests\InsuranceIntelligence.Api.Tests.csproj --no-restore` passed: `248` passed, `0` failed.

Deployment:

- API artifact: `C:\tmp\iip-phase7-compliancescope-20260708\api.zip` (`20,536,744` bytes).
- API deployed to `app-api-dev-h5girbnzrayqe` with deployment ID `5d2564d7-dc02-4280-a070-26c95dfb16b9`; status `RuntimeSuccessful`; `0` failed instances; `1` successful instance.

Post-deploy smoke:

- `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/health/ready` returned HTTP `200`.
- Unauthenticated `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/api/v1/me` returned HTTP `401`.

## Phase 6 Functions host deployment readiness (2026-07-10)

Current state:

- `infra/bicep/phase3-dev-foundation.bicep` now defines `func-iip-dev-h5girbnzrayqe` on the existing `asp-iip-dev` plan.
- The trigger uses `%ServiceBus:ApprovalNotificationsQueue%`; Linux app settings provide that hierarchical value as `ServiceBus__ApprovalNotificationsQueue`. The identity-based connection prefix resolves through `ServiceBusConnection__fullyQualifiedNamespace`.
- IaC grants the API managed identity Azure Service Bus Data Sender, the Functions managed identity Azure Service Bus Data Receiver, and the Functions identity Storage Blob Data Owner for secretless host storage.
- Optional alerts cover Function App 5xx responses and Service Bus dead-lettered messages when `alertEmailOrGroup` is configured.
- `func-iip-dev-h5girbnzrayqe` is deployed on `asp-iip-dev` and Azure lists `TeamsNotificationDispatch` as enabled.

Validated locally:

```powershell
az bicep build --file infra\bicep\phase3-dev-foundation.bicep --outfile C:\tmp\iip-phase3-dev-foundation.json
dotnet test tests\InsuranceIntelligence.Api.Tests\InsuranceIntelligence.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~FunctionsWorkflowRuntimeTests"
dotnet publish src\InsuranceIntelligence.Functions\InsuranceIntelligence.Functions.csproj --no-restore -c Release -o C:\tmp\iip-functions-publish
```

Results:

- Focused tests passed `17/17`, including protected-runtime fail-closed coverage.
- Bicep compilation passed.
- Release publish passed and generated `functions.metadata` with queue `%ServiceBus:ApprovalNotificationsQueue%` and connection `ServiceBusConnection`.
- Azure what-if `iip-phase6-functions-preview-20260710` succeeded against `rg-iip-dev`, showing the expected Function App and three RBAC creates and no deletes.

Live deployment evidence (2026-07-13):

1. Narrow deployment `iip-phase6-functions-runtime-20260713` created the Function App, managed identity, least-privilege RBAC, and Functions 5xx alert without requiring or resetting the existing SQL administrator password.
2. Azure SQL contains identity `func-iip-dev-h5girbnzrayqe` with only `db_datareader` and `db_datawriter`.
3. Run-from-package was removed after Azure reported `BadRunFromPackageConfig`; the precompiled ZIP now deploys in extraction mode with Oryx builds disabled.
4. Corrected artifact deployment `f5556fd2-4d60-4327-bc24-2703139f4723` uses `%ServiceBus:ApprovalNotificationsQueue%` and loads `TeamsNotificationDispatch`.
5. Application Insights recorded two post-fix host starts and two function-discovery events, with zero indexing errors and zero exceptions. The host endpoint returned `200`.
6. The `approval-notifications` queue is `Active` with zero active, scheduled, and dead-letter messages.

Remaining provider-gated smoke:

1. With a real Incoming Webhook configured, enqueue a synthetic approval notification and confirm the durable notification record reaches `Delivered`.
2. Exercise one controlled failing delivery and confirm the record reaches `Failed`, the Service Bus delivery is retried, and the message dead-letters after the configured maximum.

Do not deploy a mock notification provider. If no real Teams webhook is configured, keep notifications `Disabled` and do not enqueue customer notifications.

## Dev portal login and auth-boundary deployment (2026-07-13)

Deployment:

- Portal artifact: `C:\tmp\iip-portal-login-auth-boundary-20260713.zip` (`120,783,182` bytes; `24,799` entries).
- Archive validation confirmed `server.js`, `package.json`, `.next/static`, and `node_modules/next/package.json` at the package root.
- Portal deployed to `app-portal-dev-h5girbnzrayqe` with deployment ID `c7e89821-6513-4334-afce-ef3c588d8138`; status `RuntimeSuccessful`; `0` failed instances; `1` successful instance.

Post-deploy smoke:

- `GET https://app-portal-dev-h5girbnzrayqe.azurewebsites.net/` returned HTTP `200` and rendered the revised `Sign in to IIP Operations` Entra access surface.
- The live login HTML no longer contains the prototype `Authority tenant` or `Enter your workspace` copy.
- A fresh browser visit to `/app` redirected to Microsoft sign-in with portal client ID `3b3522e4-13fd-4bd9-8f08-a1cedf428d0d`, API scope `api://d55fe46b-1fba-4c5b-81e1-4ee1918ecf73/access_as_user`, and redirect URI `https://app-portal-dev-h5girbnzrayqe.azurewebsites.net/app`.
- The unauthenticated browser did not render the Command Center shell before redirect.
- `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/health/ready` returned HTTP `200`.
- Unauthenticated `GET https://app-api-dev-h5girbnzrayqe.azurewebsites.net/api/v1/me` returned HTTP `401`.
- Browser console showed only a missing `favicon.ico` HTTP `404`; no auth or portal runtime error was observed.
## Dev alert recipient and metric alerts (2026-07-13)

- Created action group `ag-iip-dev-h5girbnzrayqe` in `rg-iip-dev` with short name `iipdev` and `cloud@oticgroup.net` as the enabled `operations` receiver using the common alert schema.
- Created severity-2 API alert `ma-api-5xx-dev-h5girbnzrayqe`: total `Http5xx > 5`, evaluated every five minutes over a five-minute window.
- Created severity-2 Service Bus alert `ma-sb-dlq-dev-h5girbnzrayqe`: average `DeadletteredMessages > 0`, evaluated every five minutes over a fifteen-minute window.
- Both alerts are enabled, target the existing dev resources, and reference the same action group.
- Azure completed the explicit metric-static-threshold test notification with email mechanism `operations` status `Succeeded`; send time was `2026-07-13T10:55:19Z`.
- The Function 5xx alert was not created because `func-iip-dev-h5girbnzrayqe` does not yet exist.
- Future foundation deployments must set `IIP_DEV_ALERT_EMAIL_OR_GROUP` to the approved operations mailbox or owner email. Leaving it empty would cause the conditional action group and metric alerts to be omitted from the deployment.

## API live telemetry proof (2026-07-13)

Validation and deployment:

- Focused `ObservabilityWiringTests` passed `3/3`.
- `dotnet build InsuranceIntelligence.sln --no-restore` passed with `0` warnings and `0` errors.
- `dotnet test InsuranceIntelligence.sln --no-restore` passed `371/371` (`102` Domain, `269` API).
- Release API publish contained the Application Insights assemblies and no `InsuranceIntelligence.LocalTestDoubles` dependency.
- API artifact: `C:\tmp\iip-api-telemetry-proof-20260713\api.zip` (`20,067,005` bytes; `131` entries).
- Deployment to `app-api-dev-h5girbnzrayqe` completed with ID `d8b503af-ed29-445e-9649-2d89e4a6c0a2`; status `RuntimeSuccessful`; `0` failed instances; `1` successful instance.

Application Insights evidence:

- Pre-deployment `requests` query over 24 hours returned `0` rows.
- Proof marker `iip-live-telemetry-20260713-1115z` produced five ingested request rows: `/health/ready = 200`, `/health/live = 200`, unauthenticated `/api/v1/me = 401`, unauthenticated `/api/v1/claims = 401`, and `/telemetry-proof/... = 404`.
- Each row included timestamp, route name, URL, result code, success state, duration, operation ID, and cloud role `app-api-dev-h5girbnzrayqe`.
- Correlated dependency rows were empty because these proof calls did not invoke an external provider. Safe `IIP telemetry event` traces were empty because no authenticated audited action was executed.
- Removed the obsolete App Service setting `APPINSIGHTS_INSTRUMENTATIONKEY`; `APPLICATIONINSIGHTS_CONNECTION_STRING` remains configured.
- After removal, readiness remained HTTP `200` and proof marker `iip-connection-string-only-20260713-1122z` was ingested with result `200`, duration `107.4261 ms`, operation ID `2c334815a2b9d8e1ba09536dbab832a5`, and the expected API cloud role.
- The post-deployment window contained `23` requests (`18` successful, `5` expected unsuccessful auth/not-found outcomes) and `0` exceptions.

Remaining telemetry proof:

- Deploy the Functions host before claiming Functions request/dependency/trace telemetry.
- Execute one controlled authenticated audited action to capture a safe `IIP telemetry event` row without raw prompts, tokens, provider payloads, or customer content.
