# IIP SaaS: build plan

Status: active. Workspace: `Projects/agenticIip-saas`, branch `saas/foundation`, cloned from `agenticIip-v2` at commit `d5e9bed`. The Managed App edition in `agenticIip-v2` keeps running the field test unchanged; finished SaaS work merges back through git, not by copying files.

## What we are building

One hosted IIP that many insurers and brokers sign up to, run by Otic. Same code as the Managed App edition (.NET 8 API, Next.js portal, Azure SQL), made multi-tenant.

## Decisions (confirmed by the product owner, 2026-10-03)

1. **Who signs up:** insurance companies and brokers. An insurer creates its organisation, then invites its own staff, brokers and agents, who sign in under that insurer.
2. **Brokers work with several insurers from one login,** and see one dashboard across all the insurers they work with.
3. **Data:** Otic-hosted pooled Azure SQL. Every insurer shares the database, separated by tenant on every record. Microsoft Fabric stays the Enterprise option (customer's own Fabric, customer's own capacity), parked in `iip_saas/parked/fabric` until a confirmed Enterprise customer needs it. Fabric mirroring of the SQL database into a Lakehouse can later serve analytics without the app depending on Fabric.
4. **Sign-in:** Microsoft Entra External ID, so people can sign in with Google, a Microsoft account or an email one-time code.
5. **Payment:** Microsoft commercial marketplace, as a transactable SaaS offer published through Partner Center. (The Managed App edition stays a separate Azure Application offer from the same Partner Center account.)

## What the code already gives us

Verified in source on 2026-10-03:

- Every stored record carries `TenantId` and `WorkspaceId`, and repositories filter by `TenantScope`.
- Signed-in users are resolved to an insurer through the membership store (`UserTenantMemberships`, `RoleAssignments`), not through anything the browser sends (`Program.cs`, tenant context middleware; `EfCorePorts.GetEffectiveMembershipAsync`).
- A user can hold memberships in several tenants. When more than one matches and nothing says which, the request is refused, never guessed. A workspace switcher header already exists and is re-validated against membership.
- Provider credentials (WhatsApp, SMS, email, translation) are saved per tenant, encrypted, validated before save.

## What is single-tenant today

These read the one tenant the deployment is configured with (`INSURANCEINTELLIGENCE_TENANT_ID` / `_WORKSPACE_ID`) and must become per tenant:

| Place | File |
|---|---|
| WhatsApp credentials loaded for one tenant only; webhook routes every message to it | `Infrastructure/WhatsApp/WhatsAppRuntimeSettingsProvider.cs`, `Api/Integrations/WhatsAppIntegrationEndpoints.cs`, `Infrastructure/WhatsApp/WhatsAppInboundDocumentIngestionService.cs`, and every `settingsProvider.Current` caller |
| SMS delivery webhook scope | `Api/Integrations/SmsIntegrationEndpoints.cs` |
| Claimant portal scope | `Api/ClientPortal/ClientPortalScopeReader.cs` |
| Fallback scope from configuration | `Api/Common/TenantScopeReader.cs` |
| Scheduled jobs (follow-ups, mailbox, retention) run for one tenant | `Functions/*TimerFunction.cs` |
| Demo seeding and the demo phone commands | `Program.cs`, `DemoSeedEndpoints.cs`, `DemoSessionCommands` |

## Build order

Each step leaves the system working and tested. Do not start a step before the one it depends on.

### 1. WhatsApp per insurer

Each insurer connects its own WhatsApp Business number. One webhook URL serves all of them.

- Resolve credentials by Meta `phone_number_id` for inbound messages and by `TenantScope` for outbound ones; no deployment-wide tenant.
- Inbound: read only the `phone_number_id` from the payload, look up that insurer's connection, then verify the signature with that insurer's app secret before anything else is processed. Unknown number: reject, never route to a default.
- Webhook verification (`hub.verify_token`) matched against every active connection's verify token.
- Keep the environment-variable connection as the Managed App path, used only when SaaS mode is off.
- Tests: two insurers, two numbers, messages never cross; a forged signature for one number using another insurer's secret is rejected.

### 2. Remove the remaining single-tenant paths

- SMS webhook: resolve the tenant from the provider account the callback belongs to.
- Claimant portal: the insurer comes from the magic-link token (already tenant-bound), not configuration.
- Scheduled jobs: iterate over active tenants, each run in its own scope, one tenant's failure never stopping the others.
- Demo seeding per organisation (sandbox), demo phone per organisation.
- A `SaaS` runtime profile that refuses to start if any code path still falls back to a configured tenant.

### 3. Organisations, sign-up and invitations

- Entra External ID tenant with Google, Microsoft account and email one-time code. The API already validates Entra tokens; add the External ID authority and audience.
- `Organisation` (type: Insurer or Broker) owning a tenant and a default workspace.
- Sign-up flow: create account, create organisation, become its Owner. Invitations by email with a role; accepting creates the membership.
- The insurer chosen for a request comes from a requested-tenant header validated against membership, exactly like the workspace switcher; the token's own tenant claim is the External ID directory and is never an insurer.

### 4. Brokers across insurers

- A broker organisation links to an insurer only when the insurer invites it or approves its request. The link grants the broker's people a scoped membership (broker roles) in that insurer's tenant.
- Tenant switcher in the portal for anyone with more than one insurer.
- "All my insurers" dashboard: one read per membership, each in that insurer's own scope, merged in the application layer. No query ever runs without a tenant filter; no cross-tenant SQL.
- Tests: a broker sees exactly the insurers that linked them; removing the link removes access on the next request.

### 5. Shared resources made tenant-safe

- Knowledge search: tenant filter enforced in the retrieval provider, tested.
- Usage limits and metering per tenant: model calls, WhatsApp conversations, translations, voice minutes. Hard caps so one tenant cannot run up the bill.
- Per-tenant data export and deletion (Data Protection and Privacy Act, 2019).

### 6. Microsoft Marketplace

- Port the landing page and SaaS Fulfillment API integration from `iip_saas` (`InsuranceIntelligence.Marketplace`, `SaasControlPlane`, `SaasIdentity`).
- Activation maps a Marketplace subscription to an organisation; change and cancel events update its plan and access.
- Plans: flat monthly tiers first; usage dimensions through the Marketplace metering API once step 5 counts reliably.
- The buyer activates with a Microsoft identity once; their people then sign in however they like.
- Before listing: confirm Uganda and the other target countries are supported buyer markets for transactable offers. Where they are not, offer direct invoicing.

### 7. Onboarding

- Guided setup: connect WhatsApp, choose products, upload approved policy wordings, invite the team.
- A sandbox with demo data for every new organisation, clearly marked, separate from live data.

### 8. Hardening and go-live

- Automated tenant-isolation suite over every endpoint (extend `scripts/tenant-isolation-check.ps1`).
- Penetration test; backups and restore drill; monitoring and alerting per tenant.
- A separate Azure environment for SaaS, never the field-test `rg-iip-dev`.

## Open questions

1. **WhatsApp numbers:** does every insurer bring its own WhatsApp Business number (recommended: their brand, their number, their Meta account, connected through Meta's embedded sign-up with Otic as a Tech Provider), or does Otic run one shared number that asks which insurer?
2. **Broker approval:** must an insurer approve every broker, or can a broker request access and be approved later?
3. **Pricing:** plan tiers and what is metered.

## Status (2026-10-05)

Steps 1 to 8 are built and tested locally (Domain 655, Api 1113 tests passing). Nothing is deployed or pushed.

- Built: per-insurer WhatsApp routing; single-tenant paths removed; organisations, sign-up, invitations; brokers across insurers; tenant-safe shared resources including cross-tenant overwrite and read fixes; Marketplace fulfillment (backend only); onboarding and sandbox; plan limits, export and purge; Key Vault key ring; rate limits; durable recovery of lost WhatsApp messages.
- Integration: import header synonyms, claims register CSV, signed outbound webhooks, API keys. See docs/05-deployment-release-enablement/IIP_INTEGRATION_GUIDE.md.
- Needs people and accounts: see docs/05-deployment-release-enablement/IIP_SAAS_GO_LIVE_CHECKLIST.md.
