# IIP SaaS: activation checklist

Written 2026-10-09. What has to happen, and who does it, for `app.insuranceintelligenceplatform.com` to take sign-ups. The code side is on branch `saas/merge-v2-2026-10-08` in `Projects/iip_saas`. Detail on the switch itself is in `IIP_SAAS_CUTOVER_ON_EXISTING_RESOURCES.md`.

## Done in code

- Landing page at `/`, product choice at `/signup` (IIP for Insurance or IIP for Bancassurance), sign-in at `/login`.
- Sign-in with Microsoft work accounts, Google, or an email code. Google and email go through Microsoft Entra External ID, as CIP does. Buttons that are not configured say "Not set up yet".
- A first-time person is sent to `/welcome` to name their organisation; the product they chose is kept on it.
- The API accepts work-account and External ID tokens side by side.
- `POST /api/v1/admin/organisations/adopt-current-tenant` turns today's field-test tenant into the first organisation without moving data.
- Everything the Managed App edition has (merged 2026-10-08). 1,890 backend tests pass.

## What the owner needs to do

### 1. Choose the sign-in directory for Google and email (decision)

- **Recommended: a new External ID directory for IIP** (for example `iipcustomers`). Its sign-in pages carry IIP's name and logo. The free tier covers 50,000 monthly users.
- **Alternative: reuse CIP's** (`cipcustomers.ciamlogin.com`). Faster, but its sign-in pages show CIP's branding to IIP customers, because branding belongs to the directory.

Either way, in that directory: add Google as a sign-in option, turn on email one-time code, and create an app registration for IIP with:

- Single-page application redirect URIs: `https://app.insuranceintelligenceplatform.com/app` (and the current dev portal address for testing).
- An exposed API scope (for example `api://<client-id>/access_as_user`).

Send me the directory's sign-in address (`https://<name>.ciamlogin.com/<tenant-id>`), the client id and the scope.

### 2. Let any company's work account sign in

In Otic's directory, set the IIP portal and API app registrations to "Accounts in any organizational directory" (today they accept Otic accounts only), and add the new redirect address above. CIP already does this (`organizations`).

### 3. Key Vault permission

Give the account doing the switch the **Key Vault Crypto Officer** role on `kv-iip-dev-zcxolwov` (or create the key-ring key yourself). SaaS mode will not start without the key ring.

### 4. Decide: replace or side by side

Recommended: replace. The SaaS build runs on the existing API and portal web apps.

### 5. DNS at Namecheap

When the apps are ready I will give the exact records: CNAME and TXT verification for `app` and `api`, and A and TXT for the root domain. Then I bind the hostnames and free managed certificates.

### 6. Later, before paying customers

Privacy, terms, support and data-deletion pages (reviewed by counsel), Partner Center offer, Meta tech-provider setup, pen test and backup drill (see `IIP_SAAS_GO_LIVE_CHECKLIST.md`).

## What I do once 1 to 4 are in place

1. Rehearse on a laptop copy of the database.
2. Step A: deploy the SaaS build with multi-tenant off; apply the migrations; check nothing changed.
3. Adopt today's tenant as the first organisation.
4. Set the SaaS settings (multi-tenant on, sign-in directories, key ring, addresses) and rebuild the portal with them.
5. Re-save the WhatsApp and Sunbird connections for the organisation.
6. Step B: switch on, and check sign-up with each method, sign-in, the workspace, and Kato.
7. Give you the DNS records; bind hostnames and certificates; update sign-in redirects and Meta's callback.

## Settings used

| Where | Setting | Value |
|---|---|---|
| API | `INSURANCEINTELLIGENCE_MULTI_TENANT` | `true` |
| API | `INSURANCEINTELLIGENCE_AUTH_MODE` | `Entra` |
| API | `INSURANCEINTELLIGENCE_EXTERNAL_ID_AUTHORITY` | `https://<name>.ciamlogin.com/<tenant-id>/v2.0` |
| API | `INSURANCEINTELLIGENCE_EXTERNAL_ID_CLIENT_ID` | the IIP app's client id in that directory |
| API | `INSURANCEINTELLIGENCE_TENANT_ID`, `_WORKSPACE_ID` | removed |
| API | `INSURANCEINTELLIGENCE_DATAPROTECTION_BLOB_URI`, `_KEY_URI` | key-ring blob and Key Vault key |
| Portal | `NEXT_PUBLIC_ENTRA_AUTHORITY_TENANT` | `organizations` |
| Portal | `NEXT_PUBLIC_EXTERNAL_ID_AUTHORITY` | `https://<name>.ciamlogin.com/<tenant-id>` |
| Portal | `NEXT_PUBLIC_EXTERNAL_ID_CLIENT_ID`, `_API_SCOPE` | from the External ID app registration |
| Portal | `NEXT_PUBLIC_ENTRA_REDIRECT_URI` | `https://app.insuranceintelligenceplatform.com/app` |
