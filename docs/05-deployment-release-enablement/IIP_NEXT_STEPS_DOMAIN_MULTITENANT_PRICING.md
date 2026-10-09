# IIP SaaS: what is left for the domain, multi-tenant mode and pricing

Written 2026-10-09 after studying the live state in Azure and the code. Each section says what is true now, what has to happen, who does it, and in what order.

## 1. The bare domain does not load (`insuranceintelligenceplatform.com`)

**What is true.** Only `app.` and `api.` have DNS records. The bare domain and `www.` have none, so a browser says "This site can't be reached" (DNS_PROBE_FINISHED_NXDOMAIN). `app.insuranceintelligenceplatform.com` and `api.insuranceintelligenceplatform.com` work, with certificates.

**What has to happen.** Add these records at Namecheap (Advanced DNS) for `insuranceintelligenceplatform.com`. Delete any default Namecheap "parking" or "URL redirect" record on `@` and `www` first.

| Type | Host | Value |
|---|---|---|
| A | `@` | `20.115.232.11` (the portal app's public address today) |
| TXT | `asuid` | `F84C7C73D84CDF456110812DFFAD13CC632259C1B49A6AAD461A86FA1156F350` |
| CNAME | `www` | `app-portal-dev-h5girbnzrayqe.azurewebsites.net` |
| TXT | `asuid.www` | the same value as above |

Then I bind both names to the portal and issue the free certificates (about 10 minutes after DNS spreads). The landing page, sign-up and sign-in are the same app, so the bare domain shows the landing page. Entra already lists the bare domain as a sign-in return address.

**Watch for.** The public address belongs to the current server plan. If the plan is ever recreated or moved, the A record must be updated; a CNAME-style alias is not allowed on a bare domain. Moving to Azure Front Door later removes that fragility.

**Who.** You add the records; I bind and verify.

## 2. Turning on multi-tenant mode (Step B)

**What is true now.** The code is complete: organisations, memberships, per-organisation WhatsApp and connections, usage limits, sign-up gate, and a startup guard that refuses to run unsafely. Work accounts from any company can already sign in at the API (it accepts tokens from any Microsoft directory). The live site still runs as your single workspace, with `INSURANCEINTELLIGENCE_MULTI_TENANT` off, so sign-up is closed ("Sign-up for IIP is not open yet").

**Checked on 2026-10-09 (so the earlier blockers are smaller than thought):**

- The signed-in Azure account is **Owner on the subscription**, so it can grant itself and the API the Key Vault access that was refused before. The vault uses role-based access. Today only a secrets role exists on it; no key can be created or used until a crypto role is granted.
- The 3 existing staff accounts already have memberships in the current tenant, so once the organisation record exists they keep working. Adoption creates only the organisation record; no data moves.
- The storage account has no container for the encryption key ring yet.
- The portal deploy script did not pass the "any work account" sign-in setting; it does now (`IIP_ENTRA_AUTHORITY_TENANT=organizations`).

**The order, with the settings:**

1. **Azure access** (needs your yes, because it changes who can do what in Azure):
   - Give the account doing the work **Key Vault Crypto Officer** on `kv-iip-dev-zcxolwov`, and create a key `iip-dataprotection`.
   - Give the API's identity (`549d4a78-d4b2-48d1-94e3-4586b08853a9`) **Key Vault Crypto User** on that key and **Storage Blob Data Contributor** on a new container `dataprotection`.
2. **Rehearse first** on a copy of the database, as in `IIP_SAAS_CUTOVER_ON_EXISTING_RESOURCES.md` section 5. Export a `.bacpac` of the database before anything changes (the restore point is the rollback).
3. **Register the current workspace as the first organisation** with `POST /api/v1/admin/organisations/adopt-current-tenant` (name and product). No data moves.
4. **Re-save the WhatsApp and Sunbird connections** per organisation through the portal. In multi-tenant mode inbound messages are matched by Meta's phone-number id, so until that is saved Kato goes silent. Do this before the flip, and move Meta's callback only at the flip.
5. **Flip the API settings:** `INSURANCEINTELLIGENCE_MULTI_TENANT=true`; remove `INSURANCEINTELLIGENCE_TENANT_ID` and `INSURANCEINTELLIGENCE_WORKSPACE_ID` (the guard refuses to start while they are set); add `INSURANCEINTELLIGENCE_DATAPROTECTION_BLOB_URI` and `_KEY_URI`; set the public base URLs to the new domain.
6. **Rebuild the portal** with `IIP_ENTRA_AUTHORITY_TENANT=organizations`.
7. **Check:** sign in as an existing staff member, Kato answers on WhatsApp, a brand-new work account can sign up and gets its own organisation and sees none of the others' data.

**Two ways to launch:**

- **Phase 1, recommended: Microsoft work accounts only.** Everything above, nothing else. Insurers and banks all have work accounts, so this is enough for the first customers. It can go live as soon as step 1 is approved.
- **Phase 2: Google and email-code sign-in.** Needs a new Entra External ID directory with Google added, an IIP app registration in it, and three portal and API settings. Branding on its sign-in pages belongs to the directory, so create a new one for IIP instead of reusing CIP's.

**Before real customers (not blockers for Phase 1 testing):** narrow the `CustomerService` role, which currently holds every permission; the privacy, terms, support and deletion pages checked by counsel; a backup restore drill; a penetration test.

## 3. Pricing and credits (`IIP_PRICING_AND_CREDITS_PLAN.md`)

**What is true.** The plan is written and decisions are listed. Nothing is built yet. The platform already counts assistant turns and inbound messages per organisation and stops AI work at a plan limit (`IUsageMeter`), which is the part to build on.

**Decisions only you can make:** prices and credit allowances per plan, the markup (3x proposed), the trial (30 days, 2,000 credits proposed), the payment provider for direct sign-ups, and whether to enrol in ISV Success now (recommended; it gives Azure credits and Marketplace benefits).

**Build order (each step ships on its own):**

1. Record every AI call (user, task, model, token counts, credits). One recording hook in the AI provider adapters so no call can skip it, plus a model price table in settings.
2. Credit balance per organisation and period, the plan catalogue, and the trial allowance given when an organisation is created. Replace the turn counter with a credit check that falls back to plain replies when credits run out.
3. Settings, Usage and billing: the credit meter, breakdown by feature and person, the plan card. (It currently says "Not connected" for credits.)
4. Out-of-credits states on every AI surface, with the "Buy credits" action for administrators only.
5. Direct billing: top-ups and plan changes (needs the payment provider decision).
6. Marketplace: plans and the two metered dimensions in Partner Center, the hourly usage report from the backend, and a reconciliation report. Needs the Partner Center offer to exist.

**Dependency.** Steps 5 and 6 need multi-tenant mode on, because plans and credits are per organisation.

## 4. The order I would do it in

1. Add the four DNS records (you), then I bind the domain.
2. Approve the Azure access in 2.1; I do the rehearsal, adoption and the flip for Phase 1.
3. Decide the pricing numbers and payment provider; I build pricing steps 1 to 4.
4. Create the Google and Microsoft app registrations from `IIP_FILE_CONNECTIONS_SETUP.md`; Drive and OneDrive then go live without more code.
5. Phase 2 sign-in (External ID) when you want Google and email sign-in.
