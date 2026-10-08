# IIP SaaS: switching `rg-iip-dev` from the Managed App code to the SaaS code

Written 2026-10-08. Nothing in this document has been done in Azure. Everything described as "done" refers to the code merge on this branch.

## 1. The idea, and what it changes

The owner's decision: Otic will pivot to the SaaS product. The Managed App edition stays only for customers who need it in their own subscription. For now, the resources already in `rg-iip-dev` (Azure OpenAI, AfriSLM, Azure SQL, storage, search, Service Bus and the rest) are pointed at the SaaS code, with no new resources and no data lost. Only the architecture changes.

This reverses one line of `IIP_SAAS_GO_LIVE_CHECKLIST.md`, section 1, which said the SaaS environment would be new and separate from the field test. The reasons that line existed still apply, so section 6 lists what we give up and how each risk is reduced.

## 2. Where the code stands (done on this branch)

The SaaS branch (`saas/foundation`) was cut from the Managed App code at `d5e9bed`. Since then the Managed App side gained 27 commits and the SaaS side 15. On 2026-10-08 the Managed App work was merged into a new branch, `saas/merge-v2-2026-10-08`, in a separate working copy so nothing in `agenticIip-saas` was disturbed.

- The merge brought in the bancassurance acquisition work (scoring, outreach, follow-ups, live dashboard), IIP Academy with pricing, the Kato changes (profile questions, no unprompted offer of a person, voice-note retry, language switching) and the one-row-per-person Customers fix.
- Only one file conflicted, `CustomerAssistantService.cs`, in three places. Each was two changes to the same lines, so both were kept: usage limits and per-number voice settings from SaaS, the insights queue and voice retry from the Managed App side.
- All 1,877 backend tests pass (722 domain, 1,155 API).
- The database model and the migrations agree (`dotnet ef migrations has-pending-model-changes` reports no changes).
- Five webhook tests were already failing on the SaaS branch before the merge. They used a fixed date (5 October) while the real code stamps events with the current time, so they expired. The test clock is now relative to the real clock.

Not yet done: the portal was not built or linted on the merged branch, and nothing was run in a browser.

## 3. What each existing resource does in the SaaS product

| Resource in `rg-iip-dev` | Becomes | Change needed |
|---|---|---|
| `sql-dev-h5girbnzrayqe` / `insuranceintelligence` | The pooled database. Every organisation shares it, separated by tenant on every record | Four new tables from additive migrations (organisations, usage counters, webhooks, API keys). No existing table is altered |
| `app-api-dev-h5girbnzrayqe` | The one hosted API for all organisations | New settings (section 4). Deploy the SaaS build |
| `app-portal-dev-h5girbnzrayqe` | The one hosted portal | Rebuilt with the new sign-in and address |
| `func-iip-dev-h5girbnzrayqe` | Timers (follow-ups, mailbox, retention) that now run once per organisation | Deploy the SaaS build |
| `oai-iip-dev-...`, `ca-afrislm-dev`, `di-iip-dev-...`, `srch-dev-...` | Shared by all organisations | None. Usage is counted per organisation by the new usage counters, and a plan limit makes Kato fall back to plain replies instead of running up cost |
| `sb-dev-...` (Service Bus) | Shared queues | None |
| `stiiph5girbnzrayqe` (storage) | Shared containers, tenant-prefixed | One new container for the Data Protection key ring (a container, not a new resource) |
| `kv-iip-dev-zcxolwov` | Holds the key that wraps the key ring | One new key. The identity doing the work needs permission to create it. The account used on 2026-10-08 was refused when listing keys, so this needs an access grant first |
| `appi-dev-...`, alerts | Shared monitoring | Alert rules per organisation are a later item |
| `asp-iip-dev` (B1) | Hosts API and portal | None. B1 has no deployment slots, so there is no instant rollback (section 7) |

## 4. The two-step switch

The SaaS code has a switch, `INSURANCEINTELLIGENCE_MULTI_TENANT`. With it off, the SaaS build behaves like the Managed App build. That gives two small steps instead of one big one.

### Step A: change the code, keep the behaviour (low risk)

1. Export the database to a `.bacpac` in the existing storage account (a new container there is fine). Keep the previous API and portal deployment zips alongside it.
2. Deploy the SaaS build to the API, portal and Functions apps with `MULTI_TENANT` off and every other setting as it is today.
3. Apply the four additive migrations through the existing `db-migrate` route. The database already has `AddAcademyPricing` applied. EF applies any migration missing from its history table, whatever its date order, so the four older SaaS migrations are applied after it. This has been reasoned from how EF works but not tested against a copy of the real database (see section 5).
4. Check the checklist in section 8. Nothing a user sees should change.

Rollback for Step A: redeploy the saved zips. The four new tables are unused by the old code and can stay.

### Step B: turn on SaaS mode (the real switch)

Do this only when every item below is ready, because the API refuses to start in SaaS mode if any is missing.

Settings (names from `IIP_SAAS_GO_LIVE_CHECKLIST.md`, section 2):

| Setting | Value |
|---|---|
| `INSURANCEINTELLIGENCE_MULTI_TENANT` | `true` |
| `INSURANCEINTELLIGENCE_TENANT_ID` and `INSURANCEINTELLIGENCE_WORKSPACE_ID` | **Remove both.** The guard refuses to start while they are set. Today they are `dcb0b132-fb9e-475c-a68e-b0671222e8d8` and `workspace-operations` |
| `INSURANCEINTELLIGENCE_AUTH_MODE` | Entra External ID, with its authority and audience |
| `INSURANCEINTELLIGENCE_DATAPROTECTION_BLOB_URI` and `_KEY_URI` | The key-ring blob and the Key Vault key |
| `INSURANCEINTELLIGENCE_API_PUBLIC_BASE_URL` | `https://api.insuranceintelligenceplatform.com` |
| `INSURANCEINTELLIGENCE_CLIENT_PORTAL_BASE_URL` and `Portal__CorsOrigin` | `https://app.insuranceintelligenceplatform.com` |
| Any deployment-wide WhatsApp settings | Must not be set. Each organisation connects its own number |

Data steps before the flip:

1. **Make the current tenant the first organisation.** The SaaS code finds an organisation through its organisation record, and nothing creates one for a tenant that already exists. The existing data sits under tenant `dcb0b132-...`. Create one `Organisation` row with that id as `OrganisationId`, `workspace-operations` as `DefaultWorkspaceId`, status Active and a plan. Existing user memberships keep working because they are already stored per tenant. This is a small one-off script or admin route that still has to be written and tested. It does not move or copy any data.
2. **Re-save the WhatsApp connection for that organisation** through the portal, because SaaS mode routes inbound messages by Meta phone number id and rejects an unknown number. Until that is saved, Kato would not answer. Save it before switching Meta's callback address.
3. **Re-save the Sunbird (translation and speech) connection** the same way if it is not already stored per tenant.

Rollback for Step B: put the two tenant settings back, switch `MULTI_TENANT` off, and point Meta's callback back. The data is untouched because nothing was moved.

## 5. Rehearsal without creating anything in Azure

1. Restore the `.bacpac` into SQL Server on a laptop (LocalDB or a container).
2. Run the SaaS migrations against it and confirm they apply on top of a database that already has `AddAcademyPricing`.
3. Start the SaaS API locally with `MULTI_TENANT` on and the Development sign-in, create the organisation row, and sign in as an existing user.

This proves the database step and the organisation backfill before touching dev.

## 6. What we give up by sharing the field-test resources, and the mitigation

| Risk | Mitigation |
|---|---|
| One database for the field test and every future insurer. A restore, a bad migration or a slow query hits everyone | Take the `.bacpac` first. Keep paying insurers off the shared dev environment until the isolation script has run against it. The database tier is also a Standard-tier single database, so noisy tenants share its limits |
| The field-test data (real people who texted Kato) becomes one tenant in the pool | It stays under its own tenant id. Run `scripts/tenant-isolation-check.ps1` with two organisations before a second organisation is admitted |
| Rollback is slower on B1 (no slots) | Saved zips plus the bacpac. Do the switch in a quiet hour and expect several minutes of cold start for both apps |
| Pen test, backup drill and legal items in the go-live checklist were written for a clean environment | They still apply to this one and are not done |
| The region is `westus2` | The checklist section 7 asks for a region chosen against Uganda data-residency advice. Moving later would mean new resources and a data move, so settle this before real insurers join |

## 7. Things that are new even though no new Azure resource is created

- **Hostnames and certificates.** Each hostname (`app`, `api`, `www` and the apex) needs a DNS record at Namecheap and a managed certificate bound to the App Service. A managed certificate is a small Azure object in the resource group, so "no new things" is not strictly true here. B1 supports custom domains and managed certificates.
- **Microsoft Entra External ID.** A separate directory, outside the resource group, with Google, Microsoft account and email-code sign-in and an app registration for the portal and API. The portal is rebuilt with it, because those values are fixed at build time.
- **Partner Center and Meta.** Marketplace offer, plans, landing page and webhook; Meta's callback, privacy, terms and data-deletion addresses. All are manual actions by the owner.
- **Entra redirect address and Meta callback** must be changed at the moment of the switch.

## 8. Checks after each step

1. The API health route answers, and the portal home page loads.
2. Sign in as an existing user and open Claims, Acquisition, Outreach and IIP Academy.
3. Send Kato a text from a number he knows and one he does not, and a voice note. Confirm a reply to each.
4. Confirm a second test organisation cannot see the first one's prospects or messages.
5. Look at Application Insights for new errors in the ten minutes after.

## 9. Keeping the Managed App edition alive

The Managed App edition is delivered into a customer's own subscription, so it does not need `rg-iip-dev`. What it needs is a code line. Two ways:

- **One code base, two modes (recommended).** The SaaS build already runs as the Managed App when `MULTI_TENANT` is off. Make the SaaS repository the main line and build the Managed App package from it. Fixes are made once.
- **Two repositories.** Keep merging in both directions. This works, as the 2026-10-08 merge shows, but it costs a merge session every few weeks and migrations written on both sides must be reconciled.

## 10. Decisions needed from the owner

1. One code base with a switch, or two repositories?
2. The data region: stay in `westus2` for now, or settle a Uganda-appropriate region before any real insurer joins?
3. Is the field-test tenant allowed to become the first organisation, or should it be exported and kept apart?
4. Who creates the Entra External ID directory, the Key Vault key and the Namecheap records, and when?
5. Is a short window of reduced service acceptable for Step B, outside working hours?

## 11. Order of work from here

1. Build and lint the portal on the merged branch, and open it in a browser. (This session did not.)
2. Write and test the organisation backfill from section 4.
3. Rehearse on a laptop copy (section 5).
4. Settle the decisions in section 10.
5. Step A on dev. Check. Wait a day.
6. External ID, Key Vault key, DNS and certificates. Then Step B in a quiet hour.
