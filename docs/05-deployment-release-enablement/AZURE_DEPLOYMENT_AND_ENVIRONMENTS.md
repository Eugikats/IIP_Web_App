# Azure environments and how to deploy

Audience: any agent or person working in this repo who needs to push code to a live Azure
environment. This is the concrete "what actually exists, how do I get to it, how do I deploy"
reference — `infra/AZURE_TARGET_ARCHITECTURE.md` is the target-shape *design* doc; this one is the
current, verified *reality*. If the two disagree, this file (kept current against live `az` output)
wins for "what exists today"; `AZURE_TARGET_ARCHITECTURE.md` still wins for "what the eventual
per-insurer shape should be."

**Every identifier below is a resource name or ID, not a secret.** Never add an actual secret value
(SQL password, API key, client secret, publish-profile XML) to this file or any committed file — see
"Secrets" at the end.

## Tenant and subscriptions

- **Entra tenant**: Otic Group — `dcb0b132-fb9e-475c-a68e-b0671222e8d8` (default domain
  `oticgroupug.onmicrosoft.com`)
- **The only subscription with any IIP resources**: **"Microsoft Azure Sponsorship"** —
  `5f5765ae-985f-42e4-a443-8e2447be33ca`. This is a Sponsored subscription (spending-limit tier),
  which matters — see "Why staging is empty" below.

The tenant also has two other subscriptions visible to some accounts
(`88c8b4ab-3510-4a8d-832c-73bf72088b5a`, another "Microsoft Azure Sponsorship"; and
`2f3dc107-3c11-42ed-ae94-99e1a05c20e4`, "Azure subscription 1"). **Neither has any IIP resource.**
They host unrelated Otic projects (CIP credit-intelligence, Diaspora AI, PDIP, ERA, etc.) — don't
search them for IIP resources, and don't deploy IIP into them without a deliberate decision to do so.

### Who has access (subscription-level roles, `5f5765ae-...`)

| Principal | Role |
|---|---|
| `OticGroup@oticgroupug.onmicrosoft.com` | Owner |
| `harrygafa0_gmail.com#EXT#@oticgroupug.onmicrosoft.com` (Harry) | Owner |
| `mujuzifil_gmail.com#EXT#@oticgroupug.onmicrosoft.com` | Contributor |
| `elvistwinomugisha123_gmail.com#EXT#@oticgroupug.onmicrosoft.com` | Contributor |

The SQL Server's Entra admin is Harry's account (see below) — SQL access outside the App Services'
managed identities goes through Harry.

### How to authenticate

```bash
az login                      # interactive browser login, must be one of the accounts above
az account set --subscription 5f5765ae-985f-42e4-a443-8e2447be33ca
az account show               # confirm you landed on the right tenant/subscription
```

If `az` reports you're logged in but commands 404/403, you're almost certainly on the wrong
subscription — re-run `az account set` explicitly rather than trusting the CLI default.

## Environments

### `dev` — real, running, this is what "push to Azure" means today

Resource group **`rg-iip-dev`**, region **West US 2** (except OpenAI, which is **East US** —
Azure OpenAI wasn't available in West US 2 for this resource type at provisioning time).

| Resource | Name | Purpose |
|---|---|---|
| API App Service | `app-api-dev-h5girbnzrayqe` | `InsuranceIntelligence.Api` — `.NET 8` on Linux |
| Portal App Service | `app-portal-dev-h5girbnzrayqe` | `web/portal` — Next.js standalone on Node 20 |
| App Service Plan | `asp-iip-dev` | Hosts both App Services above |
| Function App | `func-iip-dev-h5girbnzrayqe` | `InsuranceIntelligence.Functions` |
| Azure SQL Server / DB | `sql-dev-h5girbnzrayqe` / db `insuranceintelligence` | Transactional state |
| Storage Account | `stiiph5girbnzrayqe` | Documents, knowledge files, exports |
| Service Bus | `sb-dev-h5girbnzrayqe` | Async processing queues |
| AI Search | `srch-dev-h5girbnzrayqe` (SKU basic) | Knowledge retrieval |
| Azure OpenAI | `oai-iip-dev-h5girbnzrayqe` (SKU S0, region **eastus**) | Chat + embeddings |
| Document Intelligence | `di-iip-dev-h5girbnzrayqe` | Document extraction |
| Key Vault | `kv-iip-dev-zcxolwov` (RBAC-mode, not access policies) | Secrets, referenced by app settings |
| Log Analytics | `log-dev-h5girbnzrayqe` | Log sink |
| Application Insights | `appi-dev-h5girbnzrayqe` | Telemetry |

- Portal URL: `https://app-portal-dev-h5girbnzrayqe.azurewebsites.net`
- API URL: `https://app-api-dev-h5girbnzrayqe.azurewebsites.net` (health check: `/health`)
- Both App Services already carry full, live app settings (Entra, Key Vault URI, all provider
  endpoints, `INSURANCEINTELLIGENCE_RUNTIME_PROFILE=Demo`) — **do not overwrite app settings**
  without reading the current values first (`az webapp config appsettings list`). Deploying code
  does not touch app settings.
- Auth: real Entra (`NEXT_PUBLIC_AUTH_MODE=Entra`), portal Entra app registration
  `3b3522e4-13fd-4bd9-8f08-a1cedf428d0d`, API app registration
  `d55fe46b-1fba-4c5b-81e1-4ee1918ecf73` (scope `api://d55fe46b-1fba-4c5b-81e1-4ee1918ecf73/access_as_user`).
- SQL Entra admin: `harrygafa0_gmail.com#EXT#@oticgroupug.onmicrosoft.com`. Everything else
  (Storage, AI Search, OpenAI, Document Intelligence, Service Bus, Key Vault) is accessed by the App
  Services via their **system-assigned managed identities** — no keys/connection strings in app
  settings for those.

### `stg` — empty on purpose, do not assume it exists

Resource group `rg-iip-stg` (same subscription) is **empty** — confirmed live, zero resources.
Full history is in
[`iip_gate2_staging_deploy_reuse_existing_resources.md`](iip_gate2_staging_deploy_reuse_existing_resources.md):
the original staging App Services (`app-api-stg-cus-vyhhymwydvai2`,
`app-portal-stg-cus-vyhhymwydvai2`, etc.) were deleted out-of-band around 2026-07-03, and as of that
investigation this Sponsored subscription **could not create a new App Service Plan
(`ServerFarmCreationNotAllowed`) or a new Azure OpenAI resource
(`SpecialFeatureOrQuotaIdRequired`)** in any region. `rg-iip-stg` was left empty as a placeholder
rather than deleted, on the account owner's instruction.

That quota finding is from 2026-07-07 and has **not been re-tested since** — the `dev` resource
group above does have both an App Service Plan and Azure OpenAI in this same subscription, which is
odd against a blanket "cannot create" finding and may mean the restriction has since loosened, was
resource-type/region-specific, or applied only at a particular point in time. **Re-test with a
throwaway `az appservice plan create --sku F1` before assuming staging can or can't be provisioned.**

`.github/workflows/deploy-staging.yml` still targets the deleted staging App Service names and is
deliberately guarded to fail immediately (`::error::` + `exit 1`) rather than deploy into a void or
silently succeed against a name that happens to exist by coincidence. **Do not delete that guard**
without independently re-confirming the target names via `az webapp show` first, the same way this
document was produced.

## How to deploy to `dev`

There is currently **no working CI/CD path** — `deploy-staging.yml` points at the deleted staging
resources (see above), and no equivalent dev workflow exists yet. Until one is added (needs someone
with GitHub Actions secrets/variables access to configure `AZURE_API_PUBLISH_PROFILE`,
`AZURE_PORTAL_PUBLISH_PROFILE`, and the `NEXT_PUBLIC_*` build-time variables — not something `az`
CLI can do), **deploy directly with `az webapp deploy`**, verified live on 2026-09-09:

### API

```bash
dotnet publish src/InsuranceIntelligence.Api/InsuranceIntelligence.Api.csproj -c Release -o /tmp/api-publish
# zip it (PowerShell: Compress-Archive; bash has no `zip` in this environment — use PowerShell)
az webapp deploy \
  --name app-api-dev-h5girbnzrayqe --resource-group rg-iip-dev \
  --subscription 5f5765ae-985f-42e4-a443-8e2447be33ca \
  --src-path /tmp/api-publish.zip --type zip
```

Verify: `curl https://app-api-dev-h5girbnzrayqe.azurewebsites.net/health` should return
`{"status":"healthy",...}`.

### Portal

The Next.js standalone bundle needs careful assembly — see the detailed comments in
`.github/workflows/deploy-staging.yml`'s `deploy-portal` job for the exact reasoning (a wrong
`find`-based approach previously shipped a broken bundle; pnpm's symlinked `node_modules` doesn't
survive zipping). Steps, using **dev's real values** for the build-time env vars (fetch current
values first — they may have changed — via
`az webapp config appsettings list --name app-portal-dev-h5girbnzrayqe --resource-group rg-iip-dev`):

```bash
cd web/portal
NEXT_PUBLIC_API_BASE_URL=https://app-api-dev-h5girbnzrayqe.azurewebsites.net \
NEXT_PUBLIC_AUTH_MODE=Entra \
NEXT_PUBLIC_RUNTIME_PROFILE=Demo \
NEXT_PUBLIC_ENTRA_TENANT_ID=dcb0b132-fb9e-475c-a68e-b0671222e8d8 \
NEXT_PUBLIC_ENTRA_CLIENT_ID=3b3522e4-13fd-4bd9-8f08-a1cedf428d0d \
NEXT_PUBLIC_ENTRA_API_SCOPE=api://d55fe46b-1fba-4c5b-81e1-4ee1918ecf73/access_as_user \
NEXT_PUBLIC_ENTRA_REDIRECT_URI=https://app-portal-dev-h5girbnzrayqe.azurewebsites.net/app \
pnpm build

rm -rf deploy && mkdir -p deploy
cp -r .next/standalone/. deploy/
rm -rf deploy/node_modules
mkdir -p deploy/.next
cp -r .next/static deploy/.next/static
[ -d public ] && cp -r public deploy/public
( cd deploy && npm install --omit=dev --no-audit --no-fund --ignore-scripts )
# Verify before deploying: deploy/server.js exists, deploy/.next/BUILD_ID exists,
# deploy/.next/server and deploy/.next/static exist, no symlinks in deploy/ (they don't survive zip).

# zip `deploy/` (PowerShell Compress-Archive), then:
az webapp deploy \
  --name app-portal-dev-h5girbnzrayqe --resource-group rg-iip-dev \
  --subscription 5f5765ae-985f-42e4-a443-8e2447be33ca \
  --src-path deploy.zip --type zip
```

The portal's startup command is already set to `node server.js` — don't need to touch
`az webapp config set` unless it's drifted (`az webapp config show ... --query appCommandLine`).

### Known gotchas hit while doing this live

- **`pnpm build` can crash with a Windows access violation (exit code `0xC0000005` /
  `3221226505`)** under memory pressure — e.g. running a concurrent `az webapp deploy` upload at the
  same time. If the build worker crashes with no real error message, retry it alone, not alongside
  another heavy process.
- **`npm install --silent` swallows real errors.** An `ENOSPC: no space left on device` failure
  during bundle assembly produced *zero* output under `--silent`; only visible without that flag.
  Drop `--silent` if a deploy step fails with no explanation.
- **Check disk space before a deploy session.** This environment's `C:` drive was found completely
  full (0 bytes free) mid-deployment, from accumulated build artifacts across multiple projects, not
  just this repo. `bin`/`obj`/`.next`/npm and pnpm caches are all safely regenerable — clear them
  first if a build fails mysteriously.
- **This dev VM is small (15 GB RAM total) and typically shared** — other Claude Code sessions, VS
  Code windows, etc. all compete for the same memory. If a build/zip/copy step is inexplicably slow
  or crashes, check `Get-Process | Sort WorkingSet64 -Descending` and free RAM
  (`(Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory`) before assuming something is broken.
  Don't kill node/pwsh processes indiscriminately while a build might still be flushing output —
  that corrupted a `.next/standalone` output mid-write once already.
- **`Compress-Archive` is very slow on this box for a directory with thousands of small files**
  (a `node_modules` tree) and can effectively stall for 10+ minutes. Prefer
  `[System.IO.Compression.ZipFile]::CreateFromDirectory(...)` (`Add-Type -AssemblyName
  System.IO.Compression.FileSystem` first) — noticeably faster.
- **`az webapp deploy --type zip` can report `HTTP_504 GatewayTimeout` for a large package
  (the portal's zip was ~156 MB) while the deployment actually succeeds on the server side.**
  The CLI's HTTP client gives up waiting for Kudu's response before extraction finishes; Kudu keeps
  going regardless. **Always verify with `az webapp log deployment list -n <app> -g rg-iip-dev`**
  (look for the newest entry with `"status": 4` and `"active": true`) and a live `curl` before
  concluding a deploy failed — don't just retry blindly on a reported failure, which risks stacking
  redundant deployments.

## Secrets

Never commit an actual secret to this file, `CLAUDE.md`, or anywhere else in the repo. What's above
is identifiers only. Real secrets live in:

- **Key Vault** `kv-iip-dev-zcxolwov`, referenced by app settings (`Azure__KeyVaultUri`) — the API
  reads from it directly via its managed identity, nothing is copied into app settings as plaintext.
- **Managed identities** for Storage, AI Search, OpenAI, Document Intelligence, Service Bus — no
  keys stored anywhere; RBAC role assignments on each resource grant the App Service's identity
  access.
- **GitHub Actions publish profiles** (`AZURE_API_PUBLISH_PROFILE`, `AZURE_PORTAL_PUBLISH_PROFILE`)
  are repo secrets, not present in this repo's files. Fetch fresh if setting up CI/CD:
  `az webapp deployment list-publishing-profiles -g rg-iip-dev -n <app-name> --xml`.

## Related docs

- [`../../infra/AZURE_TARGET_ARCHITECTURE.md`](../../infra/AZURE_TARGET_ARCHITECTURE.md) — the
  eventual per-insurer Managed Application target shape (design, not current state).
- [`iip_gate2_staging_deploy_reuse_existing_resources.md`](iip_gate2_staging_deploy_reuse_existing_resources.md) —
  full history of the staging deletion and the quota investigation.
- `infra/ADR-0001-hosting-model.md` — why App Service over Container Apps/AKS.
