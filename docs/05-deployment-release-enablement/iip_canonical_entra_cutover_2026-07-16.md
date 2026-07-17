# IIP canonical multitenant Entra foundation — 2026-07-16

## Outcome

The canonical ADR-0005 Entra application pair now exists in the Otic Group publisher/home tenant. It was created alongside the legacy registrations so the currently deployed dev login remains untouched until the new API and portal artifacts pass cutover validation.

| Item | Confirmed value |
|---|---|
| Publisher/home tenant | `dcb0b132-fb9e-475c-a68e-b0671222e8d8` |
| Subscription | `5f5765ae-985f-42e4-a443-8e2447be33ca` (`Microsoft Azure Sponsorship`) |
| Canonical API registration | `iip-api-platform` |
| API application/client ID | `9ff3410f-12f8-47f9-b249-13bb15c58593` |
| API audience | `api://9ff3410f-12f8-47f9-b249-13bb15c58593` |
| Delegated API scope | `api://9ff3410f-12f8-47f9-b249-13bb15c58593/access_as_user` |
| Canonical portal registration | `iip-operations-portal` |
| Portal application/client ID | `3f44797b-39c8-4d41-aae9-784fbc0aa706` |
| Sign-in audience | `AzureADMultipleOrgs` for both registrations |
| Coarse API roles | `IIP.Access`, `IIP.Admin`, `IIP.ReadOnly` |
| Client secrets | None on either registration |

The portal is pre-authorized for `access_as_user`. Registered dev redirects are:

- `http://localhost:3000`
- `https://app-portal-dev-h5girbnzrayqe.azurewebsites.net`
- `https://app-portal-dev-h5girbnzrayqe.azurewebsites.net/app`

Fine-grained insurance permissions remain in IIP's tenant/workspace membership store. Token `tid`, `oid`, and optional app roles are identity/entry hints only; they do not grant claims, document, approval, agent, or workflow permissions.

## Live cutover state

The current `app-portal-dev-h5girbnzrayqe` settings still reference the legacy `iip-portal-stg` / `iip-api-stg` pair. This is deliberate. The legacy registrations were verified unchanged after creating the canonical pair.

Future Phase 3 dev deployments now default to the canonical IDs in:

- `infra/bicep/phase3-dev-foundation.bicep`
- `infra/bicep/phase3-dev-foundation.dev.bicepparam`

The live development Service Bus namespace `sb-dev-h5girbnzrayqe` now also has an active `agent-workflow` queue. Dead-letter-on-expiry is enabled, maximum delivery count is 10, the lock duration is one minute, and the default TTL is 14 days. The existing API managed identity already has sender/receiver access at namespace scope; the Functions identity has receiver access. Creating the queue did not require a live application restart.

The workflow database migration has **not** been applied to Azure SQL. The current Azure CLI identity is not the configured SQL Microsoft Entra administrator and cannot obtain database access. A temporary exact-IP firewall rule used to verify this was removed after the failed access check. The live application pair and application artifacts therefore remain unchanged.

`20260716123910_AddAgentWorkflowControlPlane` was validated from zero against a disposable LocalDB database and the database was removed afterward. Because the repository's historical model snapshot had not captured several already-pending operational entities, this migration also creates `AgentStates`, `SalesLeads`, `TenantOnboardings`, and `Workspaces`. Duplicate permission/RBAC table creation was removed because those tables belong to `202607020001_AddCorePortPermissionTables`. Compare the migration with the live schema before executing it; do not bypass migration history.

## Cutover gates

Before changing the live App Service settings:

1. Build and deploy the API artifact that contains multitenant issuer validation plus database-backed membership resolution.
2. Apply the database migration that adds the agent workflow control plane.
3. Build the portal with the canonical portal client ID and API scope.
4. Confirm the launch user's `oid` resolves to an active IIP tenant/workspace membership.
5. Complete publisher-tenant sign-in and API token smoke tests.
6. Switch API and portal settings together, then verify `/api/v1/me`, `/api/v1/agent-workflows`, and the portal `/app/flows` route.
7. Keep the previous settings available for immediate rollback until smoke tests and audit evidence pass.

Do not delete the legacy registrations during the cutover window.

Do not enable production workflow execution merely because the queue exists. The queue is transport infrastructure; the tenant-scoped execution worker, tool adapters, completion callback, retry policy, and end-to-end evidence/audit smoke test must pass before run initiation is exposed in a protected runtime.

## Reconciliation command

The app-registration operation is idempotent and creates no secret:

```powershell
./scripts/entra/Initialize-IipPlatformAppRegistrations.ps1 -Apply
```
