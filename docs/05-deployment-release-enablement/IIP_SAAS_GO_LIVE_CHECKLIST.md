# IIP SaaS: go-live checklist

Everything the code can do without a cloud account is built and tested locally on branch `saas/foundation`. What follows needs a person with the right account. Nothing here has been done.

## 1. Environment (new, separate from the field test `rg-iip-dev`)

- [ ] New Azure subscription or resource group, region chosen against Uganda data-residency advice (section 7).
- [ ] Azure SQL (pooled, one database for all organisations), App Service or Container App for the API, hosting for the portal, Functions app for timers, Application Insights.
- [ ] Run the migrations on the new database (`dotnet ef database update` from the Api project, or the generated script reviewed first).

## 2. Settings the API needs (multi-tenant mode)

| Setting | Purpose |
|---|---|
| MultiTenant = true | Turns on organisations, per-insurer WhatsApp routing, workspace iteration, API keys. |
| AuthMode = EntraExternalId | Sign-in through Microsoft Entra External ID. |
| ExternalIdAuthority and audience | From the External ID tenant app registration. |
| DataProtectionBlobUri, DataProtectionKeyUri | Blob holding the key ring and the Key Vault key that wraps it. Startup refuses to run multi-tenant without them. |
| SQL connection string | Pooled database. |
| Marketplace tenant id, client id, secret | App registration allowed to call the SaaS Fulfillment API. Backend only. |
| Sunbird and model keys | Entered per organisation in the portal, never in settings. |

The startup guard lists every missing or unsafe value by name and refuses to start rather than run quietly insecure.

## 3. Manual actions by system

- [ ] **Entra External ID**: create the tenant, user flows (Google, Microsoft account, email code), app registration for the portal and API.
- [ ] **Partner Center**: publisher account, SaaS offer, plans matching `PlanCatalog`, landing page URL (the portal activation page) and connection webhook URL (`/api/v1/marketplace/webhook`), technical configuration with the app registration.
- [ ] **Key Vault and blob** for the Data Protection key ring; grant the API identity wrap/unwrap and blob access.
- [ ] **Meta**: apply as Tech Provider and set up embedded signup so each insurer connects its own WhatsApp Business number; until then each insurer connects its number by hand in the portal.
- [ ] **DNS and certificates** for the portal and API hostnames.
- [ ] **Alerting**: per-organisation error rate, webhook failure rate, assistant recovery backlog.

## 4. Before the first paying insurer

- [ ] Penetration test (scope below).
- [ ] Backup and restore drill, timed, written down.
- [ ] Tenant-isolation script (`scripts/tenant-isolation-check.ps1`) run against the live environment with two real test organisations.
- [ ] Rate-limit values reviewed against real traffic.
- [ ] Privacy notice and data processing terms reviewed by counsel.

## 5. Pen test scope

Cross-organisation reads and writes through every endpoint (tampering with the organisation header, id guessing, broker link abuse); API key handling (brute force, revoked key reuse, key in logs); webhook SSRF (redirects, DNS rebinding, private ranges); WhatsApp and Marketplace webhook signature bypass; invitation and sign-up abuse; file upload; prompt injection through customer messages reaching decisions; rate-limit bypass.

## 6. Backup and restore drill

Enable point-in-time restore on the SQL database and geo-backup for the blob holding the key ring. Drill: restore to a new database, point a staging API at it, confirm one organisation's claims, documents and WhatsApp connection decrypt and load, and record the time taken. The key ring and Key Vault key must be restorable too, or restored secrets will not decrypt.

## 7. Uganda Data Protection and Privacy Act

- [ ] Register with the Personal Data Protection Office as a data collector or processor as required.
- [ ] Decide hosting region; cross-border transfer needs a lawful basis and notice to insurers.
- [ ] Each insurer is the controller of its customers' data; Otic is processor. Data processing agreement per insurer.
- [ ] Export and purge (built) cover access and deletion requests; define the retention period per organisation.
