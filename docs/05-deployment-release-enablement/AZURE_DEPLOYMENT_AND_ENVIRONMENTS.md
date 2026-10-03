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
| Container Apps environment | `cae-iip-dev` | Hosts the translation engine (added 2026-09-27) |
| Container app | `ca-afrislm-dev` | The AfriSLM model served by llama.cpp, for local-language WhatsApp replies |
| File share | `afrislm-models` in `stiiph5girbnzrayqe` | Holds the model file; mounted read-only into the container |

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

### Applying database migrations — a required, separate step after deploying the API

**State as of 2026-09-27:** `dev` has all 27 migrations applied, the latest being
`20260925151034_AddCustomerLanguagePreferences`. The earlier note here that outcomes N through Q were
undeployed and their migration pending is out of date: that work is live, and its migration was
already applied. Always run `diagnose` and compare the count before trusting any written status.

**The API does not migrate on startup.** Nothing in `Program.cs` calls `Database.Migrate()`, by
design. Deploying code that reads a new table therefore succeeds, starts healthy, and then returns
`500 Invalid object name '<Table>'` on every request that touches it — `/health` will not catch
this, because it does not query those tables. This bit us on 2026-09-14: the API deployed and
reported healthy while `/cases/{id}/case-review`, `/cases/{id}/summary` and
`/integrations/whatsapp/configuration` all 500'd, because the live database was **nine migrations
behind** (since 2026-09-04, long before that day's deploy).

After deploying the API, apply migrations through the app's own admin endpoints — they use the API's
managed-identity SQL connection, so nobody needs direct database access. They are additive and
idempotent (EF skips already-applied migrations) and are blocked under protected runtime profiles
(`Staging`/`Production`/`ManagedApplicationReviewer`); `dev` runs `Demo`, which is permitted.

```bash
# Bearer token for the API's own scope, as a signed-in Entra user with ManageTenantSettings:
az account get-access-token --resource api://d55fe46b-1fba-4c5b-81e1-4ee1918ecf73 --query accessToken -o tsv

GET  /api/v1/admin/db-migrate/diagnose   # table list + applied-migration history — always run first
POST /api/v1/admin/db-migrate            # applies pending migrations; returns wasPending/appliedAfter
```

Compare `diagnose`'s `appliedMigrationHistory` count against
`ls src/InsuranceIntelligence.Infrastructure/Migrations/*.cs | grep -v Designer | grep -v Snapshot | wc -l`.
They should match. If they don't, migrations are pending regardless of what `/health` says.

**A migration EF cannot see will not appear as pending, and `diagnose` will not flag it.** EF
discovers migrations via the `[DbContext]`/`[Migration]` attributes in each migration's
`.Designer.cs`. `20260906150000_AddCaseReviews` was hand-written on 2026-09-08 without one, so it
was invisible to `dotnet ef migrations list`, to `GetPendingMigrations()`, and to the db-migrate
endpoint — the `CaseReviews` table simply never got created in any environment. Fixed on
2026-09-14 by adding the missing Designer. **Never hand-write a migration file; always use
`dotnet ef migrations add`**, and if you must edit one, confirm afterwards that
`dotnet ef migrations list` still shows it.

### Function App

**This section did not exist until 2026-09-14, and the consequence was real:** `RetentionEnforcement`
had been in the codebase since Phase 6 but was never live, because nothing here told anyone the
Functions app needed its own deploy. Deploying the API does not deploy the functions. If you add or
change anything in `InsuranceIntelligence.Functions`, this step is required or it simply never runs.

The app is `func-iip-dev-h5girbnzrayqe`, Linux, `DOTNET-ISOLATED|8.0`, with
`WEBSITE_RUN_FROM_PACKAGE=1` — so it takes a zip through `config-zip`, not `az webapp deploy`.

```bash
dotnet publish src/InsuranceIntelligence.Functions/InsuranceIntelligence.Functions.csproj   -c Release -o /tmp/func-publish
# zip it (PowerShell: [System.IO.Compression.ZipFile]::CreateFromDirectory) -- about 12 MB
az functionapp deployment source config-zip   --name func-iip-dev-h5girbnzrayqe --resource-group rg-iip-dev   --subscription 5f5765ae-985f-42e4-a443-8e2447be33ca   --src /tmp/func-publish.zip
```

**Check the publish output before deploying, and the live app after.** `functions.metadata` in the
publish folder lists exactly what the build found; `az functionapp function list` lists what the
platform actually registered. If a function is in the first and not the second, the deploy did not
take effect — registration lags the deploy by up to a minute, so poll rather than concluding on the
first empty result.

```bash
az functionapp function list --name func-iip-dev-h5girbnzrayqe --resource-group rg-iip-dev   --subscription 5f5765ae-985f-42e4-a443-8e2447be33ca   --query "[].{name:name,disabled:isDisabled}" -o table
```

As of 2026-09-14 that returns four, all enabled: `AgentWorkflowNodeExecute` (Service Bus),
`TeamsNotificationDispatch` (Service Bus), `RetentionEnforcement` (timer, 02:00 UTC daily),
`MailboxIngestion` (timer, every 5 minutes).

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

### Local-language translation engine (added 2026-09-27)

WhatsApp customers can write in Luganda or Kiswahili and be answered in the same language. The
translation is done by the AfriSLM model (`afrislm-0.8b-q4_k_m.gguf`, a 0.8B model in 4-bit, 672 MB),
served by `llama.cpp` in its own container so it can never starve the API. The definition is
`infra/bicep/afrislm-translation.bicep`; what exists in `dev` was created by CLI on 2026-09-27.

| Item | Value |
|---|---|
| Container app | `ca-afrislm-dev` in `cae-iip-dev` (West US 2), 2 vCPU and 4 GiB, exactly one replica, always on |
| Public address | `https://ca-afrislm-dev.happywave-c069e02e.westus2.azurecontainerapps.io` (unauthenticated `/health` only; everything else needs the key) |
| Image | `ghcr.io/ggml-org/llama.cpp` pinned by digest `sha256:6257697a7f5d034b8fb499ddb07af3e250506352f94102054252a23f3b85e0af`. The moving `server` tag would change the engine under a running deployment. |
| Model file | SHA-256 `4af8ee1df3ec9008f763ebe95e6f21df3acd8d42c541feeb13314ca22e560afc`, 672,329,792 bytes. Verified by hashing the copy in Azure after upload. Never committed to git (`languages/` is ignored). |
| Access key | Key Vault secret `Translation--ApiKey`, read by the API as `Translation:ApiKey`. Also held as a container secret. Never in an app setting or in git. |
| Model architecture | `qwen35`. Older llama.cpp builds cannot load it, so the image must not be downgraded. |
| Cost | A new always-on container (about 2 vCPU and 4 GiB) is billed continuously. It is the only new billable resource: the file share is in the existing storage account and the environment reuses the existing Log Analytics workspace. Check the Azure cost view before leaving it running long term. |

API app settings added (none existed before; an unset provider means Disabled, English only):

```
INSURANCEINTELLIGENCE_TRANSLATION_PROVIDER=AfriSlm
Translation__Endpoint=https://ca-afrislm-dev.happywave-c069e02e.westus2.azurecontainerapps.io
Translation__EnabledLanguages=lg,sw
Translation__AllowUnreviewedTranslations=true
Translation__RequestTimeoutSeconds=90
```

`AllowUnreviewedTranslations=true` is set for `dev` only, so real output can be reviewed by native
speakers. Leave it off in any other environment until translations have been reviewed. With it off,
customers are still understood in their own language but replies go out in English.

Check it: `GET /api/v1/agents/customer-assistant/translation` (needs `ManageAgents`) returns
`Healthy`, `Failing` or `NotConfigured`, measured from the API to the container, not from settings.
`POST .../translation/test` with `{"text","from","to"}` shows what a customer would get.

Failure behaviour, by design: if the container is down, customers who write in another language are
handed to a person with their own words, and replies go out in English. Nothing is guessed.

**Measured quality (2026-09-27, native-speaker review still pending):** customer-to-English is
accurate in both languages. English-to-Kiswahili is good. English-to-Luganda makes mistakes with
insurance vocabulary, so Luganda replies keep "claim", "policy" and "claims officer" in English. Every
translated reply is labelled as machine translated.

To replace the model: upload the new file to the `afrislm-models` share, verify its SHA-256, update the
file name in the container arguments and `AfriSlmTranslationProvider.ModelTag` (so cached translations
from the old model are never reused), and record the new hash here.

**Sunbird AI (per tenant, no redeploy).** A tenant admin can connect Sunbird AI under
`/app/admin/connections` → Translation (API key, optional endpoint, optional model, default
`sunflower-9b`). The connection is saved only after a real sample translation succeeds, the key is
encrypted at rest and never returned to the browser, and a connected tenant uses Sunbird instead of
the AfriSLM container above; tenants without one keep the AfriSLM fallback unchanged. The same card
has a "Try a translation" box (`POST /api/v1/integrations/connections/translation/try`, needs
`ConfigureIntegrations`) that runs text through the same checks a customer message gets. Sunbird is
called on its chat endpoint with a verbatim-only instruction, not on `/tasks/translate`, which
accepts no instruction. Switching engines changes the cache key, so earlier translations are never
reused. Quality against real Sunbird output has not yet been measured or reviewed by a native speaker.

### Inbound email is deployed but switched off in `dev` (verified 2026-09-14)

The collection path exists and is live — `MailboxIngestion` runs every five minutes and
`POST /api/v1/integrations/email/ingest` is registered — but **both the API and the Function App
carry `INSURANCEINTELLIGENCE_EMAIL_PROVIDER=Disabled`**, so neither collects anything. That is
deliberate honest behaviour, not a bug: the endpoint returns

```json
{"message":"Inbound email collection requires a configured Microsoft Graph mailbox connection.","provider":"Disabled"}
```

with HTTP 400, and the timer returns without acting. Nothing pretends to work.

**To actually receive email in `dev`, three things are needed, in this order:**

1. A Microsoft Graph app registration with `Mail.Read` (and `Mail.ReadWrite` if processed messages
   should be moved), admin-consented in tenant `dcb0b132-fb9e-475c-a68e-b0671222e8d8`, plus the
   `GraphMail__*` settings it needs.
2. `INSURANCEINTELLIGENCE_EMAIL_PROVIDER=Graph` on **both** `app-api-dev-h5girbnzrayqe` and
   `func-iip-dev-h5girbnzrayqe`. Setting it on only one gives a half-working channel: the admin
   "collect now" button works and the schedule does not, or the reverse.
3. A mailbox configured in the app at `/app/admin/email` with collection switched on.

Until step 2, the Channels inbox will show WhatsApp traffic and no email, which is the truthful
state and not something to "fix" by changing code.

### Claimant portal and WhatsApp under Entra auth (fixed 2026-09-15)

Until 2026-09-15 neither the claimant portal nor the WhatsApp webhook could work in `dev`, and the
code would have failed the same way in any deployed environment:

- `RuntimeHeaderGuard` rejected `X-Tenant-Id` and `X-Workspace-Id` on every route outside
  Development, while the claimant portal relies on exactly those headers to name its insurer
  (403 on every link request).
- The unauthenticated-API guard exempted only the Graph webhook, so claimant routes and Meta's
  `GET`/`POST /api/v1/integrations/whatsapp/webhook` were answered with 401. Neither caller can
  ever hold an Entra token.

Both are fixed in code: client-portal routes and the WhatsApp webhook are exempt from the sign-in
guard (they are protected by the magic-link session and by Meta's `X-Hub-Signature-256`
respectively), and `ClientPortalScopeReader` pins claimant scope to the deployment's own
`INSURANCEINTELLIGENCE_TENANT_ID`/`INSURANCEINTELLIGENCE_WORKSPACE_ID`, refusing any header that
names another insurer.

Two pieces of configuration the claimant portal needs in `dev`, both now in place:

1. `INSURANCEINTELLIGENCE_CLIENT_PORTAL_BASE_URL` on the API, set to the portal URL. Without it the
   API refuses to issue links at all.
2. A way for the claimant to receive the link. `dev` has no SMS or email provider, so on the Demo
   runtime profile only, when a real match is found and delivery did not happen, the link is shown
   on the claimant's screen with a label saying so. Staging, Production and
   ManagedApplicationReviewer never return it.

To demonstrate a claimant filing a first claim, the policyholder must have no existing claim on
that policy; the main demo seed gives every customer one. `POST /api/v1/admin/demo-seed/policyholders?count=3`
(Demo profile only, `ManageTenantSettings`) adds policyholders with an active policy and no claim,
and returns their names, emails and policy numbers. It is safe to call again: existing ones are
returned rather than duplicated.

### Portal start-up time and Always On (set 2026-09-15)

The portal container needs a long time to start on the B1 plan: the first start after the
2026-09-15 deploy failed Azure's startup probe at the default 230 second limit, and the retry only
passed after 210 seconds. Two settings now guard against that:

- WEBSITES_CONTAINER_START_TIME_LIMIT is 600 on app-portal-dev-h5girbnzrayqe, so a slow start is
  given time instead of being killed and retried.
- Always On is enabled on both app-portal-dev-h5girbnzrayqe and app-api-dev-h5girbnzrayqe. With it
  off, Azure unloads an idle app after about twenty minutes, and the next visitor waits for a full
  cold start: several minutes for the portal. Always On is included in the Basic tier at no extra cost.

After any portal deploy or setting change, expect the portal to be unavailable for up to about four
minutes while it restarts, and check the container log before concluding a deploy has failed.

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
- **`az webapp deploy` can report `RuntimeSuccessful` while the old code keeps running (seen
  2026-09-27).** The new endpoint kept returning 404 for eight minutes after a "successful" deploy, and
  the site's last-modified time had not moved. Confirm with a request only the new code can answer, and
  run `az webapp restart` if it does not.
- **The API can take 4 to 8 minutes to serve new code after a deploy or restart**, during which
  `/api/v1/me` may take 14 seconds or fail. The portal shows "Sign-in could not be completed" for
  that window. It is not a permissions problem: wait and retry in a new tab.
- **The `C:` drive on this VM was at 0 bytes free on 2026-09-27**, which stopped a zip mid-write.
  Clearing the npm download cache (`npm cache clean --force`, 2.4 GB) and one stale
  `%LOCALAPPDATA%\Temp\portal-deploy` folder restored working room. Check free space before a deploy.
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
