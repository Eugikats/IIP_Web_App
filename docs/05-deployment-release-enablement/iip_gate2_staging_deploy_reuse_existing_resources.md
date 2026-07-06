# Gate 2 — Staging deploy to the EXISTING Azure resources (reuse, do not recreate)

v2 owns deployment to the same staging resources originally provisioned for the old repo. This
deploys **application code only** to the existing App Services. It never creates/modifies Azure
infrastructure, never touches Partner Center, and never republishes the Marketplace managed-app
package (`infra/mainTemplate.bicep` remains build-only, for the already-published offer).

## Existing resources reused (identifiers, not secrets)

| Resource | Name |
|---|---|
| Subscription | `5f5765ae-985f-42e4-a443-8e2447be33ca` (Microsoft Azure Sponsorship) |
| Entra tenant | `dcb0b132-fb9e-475c-a68e-b0671222e8d8` |
| Resource group | `rg-iip-stg` |
| API App Service | `app-api-stg-cus-vyhhymwydvai2` |
| Portal App Service | `app-portal-stg-cus-vyhhymwydvai2` |
| Function App | `func-stg-cus-vyhhymwydvai2` |
| Key Vault | `kv-stg-vyhhymwydvai2` |
| Storage | `stvyhhymwydvai2` |
| Azure SQL | `sql-stg-cus-vyhhymwydvai2` / db `insuranceintelligence` |
| Document Intelligence | `di-stg-vyhhymwydvai2` |
| Azure OpenAI | `oai-stg-vyhhymwydvai2-a5524` |
| AI Search | `srch-stg-vyhhymwydvai2` (index `insuranceintelligence-knowledge`) |
| Service Bus | `sb-stg-vyhhymwydvai2` |
| App Insights / Logs | `appi-stg-vyhhymwydvai2` / `log-stg-vyhhymwydvai2` |
| Portal URL | https://app-portal-stg-cus-vyhhymwydvai2.azurewebsites.net |

## What v2 already had vs. what was added

| Area | Status |
|---|---|
| Infra Bicep (`infra/mainTemplate.bicep`, `modules/*`) | Already present — Managed-App template, **build-only**, not run for this reuse deploy |
| Parameterized `infra/parameters/stg.bicepparam` (`readEnvironmentVariable('IIP_*')`) | Already present — no change needed |
| Hosting model | Azure App Service (ADR-0001) — matches the existing resources |
| **Deploy CI/CD workflow** | **Added** → `.github/workflows/deploy-staging.yml` (was missing) |
| **App Service runtime settings scripts** | **Added** → `verify-` (read-only), `add-v2-` (safe additive), `set-` (full/fresh) under `infra/scripts/` |
| This runbook | **Added** |

> **The existing App Services already carry live runtime settings** from the prior deployment
> (Azure Resource Inventory: *"No action needed unless you rotate a secret"*). Because the App
> Service URLs are unchanged, the Entra app registration — client id, redirect URIs, API scope, and
> already-consented Graph mailbox permissions — is reused as-is with **no redirect-URI change**. So
> for this reuse deploy you only **add the v2-new settings** and verify the rest; you do not
> reconfigure everything from scratch.

## Manual steps you must perform

### 1. GitHub Actions **secrets** (publish profiles — never committed)
Download each App Service's publish profile and add it as a repo secret:
```
az webapp deployment list-publishing-profiles -g rg-iip-stg -n app-api-stg-cus-vyhhymwydvai2 --xml    > api.publishsettings
az webapp deployment list-publishing-profiles -g rg-iip-stg -n app-portal-stg-cus-vyhhymwydvai2 --xml > portal.publishsettings
```
| Secret | Value |
|---|---|
| `AZURE_API_PUBLISH_PROFILE` | contents of `api.publishsettings` |
| `AZURE_PORTAL_PUBLISH_PROFILE` | contents of `portal.publishsettings` |
Delete the local `*.publishsettings` files afterwards. (Alternatively use OIDC federated
credentials — the workflow can be switched to `azure/login` if you prefer no publish profiles.)

### 2. GitHub Actions **variables** (non-secret; baked into the portal build)
| Variable | Value |
|---|---|
| `IIP_PORTAL_API_BASE_URL` | `https://app-api-stg-cus-vyhhymwydvai2.azurewebsites.net` |
| `IIP_ENTRA_TENANT_ID` | `dcb0b132-fb9e-475c-a68e-b0671222e8d8` |
| `IIP_PORTAL_ENTRA_CLIENT_ID` | client id of the **existing** app registration |
| `IIP_ENTRA_API_SCOPE` | `api://<client-id>/access_as_user` (existing) |
| `IIP_ENTRA_REDIRECT_URI` | existing portal callback, e.g. `https://app-portal-stg-cus-vyhhymwydvai2.azurewebsites.net/auth/callback` |

### 3. API/Portal app settings — reuse-safe path (verify → add only what's new)
The existing App Services are already configured. Do **not** blindly overwrite them. Run, in order:
```
# a) see what's already live and which v2-required keys (if any) are missing — changes nothing
bash infra/scripts/verify-staging-appsettings.sh

# b) add ONLY the v2-new keys (Key Vault config URI + onboarding consent redirect); leaves the
#    existing provider/Entra/SQL settings untouched
bash infra/scripts/add-v2-appsettings.sh

# c) ensure the portal startup command is the Next.js standalone entrypoint (idempotent)
az webapp config set -g rg-iip-stg -n app-portal-stg-cus-vyhhymwydvai2 --startup-file "node server.js"
```
Only if `verify` shows the API app is **not** configured (a fresh App Service, or major drift) do a
full reconcile with `set-staging-appsettings.sh` (read its header warning first — it overwrites):
```
export IIP_PORTAL_ENTRA_CLIENT_ID=<existing app registration client id>
export IIP_AZURE_OPENAI_CHAT_DEPLOYMENT=<e.g. gpt-4o>
export IIP_AZURE_OPENAI_EMBEDDING_DEPLOYMENT=<e.g. text-embedding-3-large>
export APPINSIGHTS_CONNECTION_STRING=<from appi-stg-vyhhymwydvai2>   # optional
bash infra/scripts/set-staging-appsettings.sh
```

### 4. Managed-identity RBAC + SQL user (one-time)
The script prints the exact RBAC roles to grant each App Service's system-assigned identity
(Key Vault, Storage, OpenAI, Document Intelligence, AI Search, Service Bus), and the SQL contained-user
step. Grant those so the managed-identity connections work (no secrets/keys needed).

### 5. Run the deploy
GitHub → Actions → **Deploy Staging (existing Azure resources)** → Run workflow (branch of your choice).

## "Not configured" boundaries (honest, by design)
- **Email (Graph/Outlook):** shipped `Disabled` → shows *Not configured*. Enable only after setting
  `INSURANCEINTELLIGENCE_EMAIL_PROVIDER=Graph` + `GraphMail__*` credentials + `GraphMail__MailboxAccessScoped=true`
  (the API refuses Graph mode otherwise — no fake send path). Mailbox Graph consent already exists on the app registration.
- **Communication services & Teams notifications:** `Disabled` → *Not configured* (matches `stg.bicepparam`).
- Any provider whose managed-identity RBAC/endpoint is missing fails safely at startup with a clear
  message (Staging profile rejects mock/simulated providers) rather than faking success.

## Guardrails honored
- No Azure resources created or modified by this change (app code + app settings only).
- Partner Center and the Marketplace managed-app package are untouched (`mainTemplate.bicep` unchanged).
- No secrets committed: publish profiles are GitHub secrets; SQL/other secrets are Key Vault references
  or managed identity; the script reads secrets from your shell/Key Vault at run time.
- Deploy is manual (`workflow_dispatch`) on a feature branch — no protected branch advanced.
