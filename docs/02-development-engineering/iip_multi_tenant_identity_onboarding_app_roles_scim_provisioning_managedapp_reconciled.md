# IIP Multi-Tenant Identity Onboarding, App Roles, and SCIM Provisioning

**Product:** Insurance Intelligence Platform (IIP)  
**Staff application:** IIP Operations  
**Document type:** Identity onboarding and tenant authorization companion document  
**Layer:** API / identity onboarding / tenant administration / provisioning  
**Status:** Companion update to backend wiring authentication model  
**Date:** July 2026

---

## 1. Purpose

This document defines the tenant identity onboarding model for IIP after reconciling the backend wiring specification with the completed Entra ID identity-model research.

The correction is targeted:

```text
IIP uses one multitenant Microsoft Entra app registration for workforce users from many insurer tenants.
The API validates tokens through organizational multitenant authority mode.
The JWT tid claim is an identity hint only.
IIP's own AuthenticatedTenantResolver and IPermissionReadRepository remain the business authorization authority.
```

This document does not redesign IIP's tenant/workspace model, permission model, core ports, routing, controller behavior, or human-review rules.

---

## 2. Fixed architectural rules

```text
1. IIP is a staff-facing B2B SaaS for insurer employees, not a consumer CIAM application.
2. Insurer staff authenticate through their own workforce Microsoft Entra tenant.
3. IIP uses a single multitenant app registration configured for Accounts in any organizational directory.
4. Entra validates identity; IIP validates business membership and permissions.
5. The token's tid claim is never accepted as IIP tenant authority.
6. The internal TenantContext comes only from AuthenticatedTenantResolver + IPermissionReadRepository.
7. Fine-grained permissions such as claims.read, documents.review, and approvals.review stay in IIP's role/permission store.
8. App roles are optional coarse entry gates only.
9. Raw group claims are not the primary authorization mechanism.
10. SCIM provisioning, when added, writes membership/user lifecycle data into IIP tables but never replaces runtime authorization checks.
11. Entra tenant ID and Azure subscription ID are different onboarding identifiers; both must be captured when the customer will deploy IIP through a Managed Application private plan.
```

---

## 3. Identity model decision

### Decision

IIP should use:

```text
Default model: One multitenant Microsoft Entra app registration.
Supported account type: Accounts in any organizational directory.
Authority mode: organizations.
Runtime authorization: AuthenticatedTenantResolver + IPermissionReadRepository.
```

### Not the default

```text
Per-insurer app registrations: Exception path only when an insurer's security team requires it.
Microsoft Entra External ID / CIAM: Not used for insurer staff workforce authentication.
Raw Entra group claims: Not used as the primary authorization mechanism.
Custom claims-mapping policy: Not used as the IIP permission source of truth.
```

### Reason

IIP's customers are insurers whose employees already belong to workforce directories. The standard SaaS pattern is for those users to sign in through their own Entra tenants while the SaaS application keeps application-specific authorization in its own data model.

IIP's current server-side resolver pattern is correct and must be preserved:

```text
Validated JWT identity
-> tid/oid identity hint
-> IPermissionReadRepository.GetEffectiveMembershipAsync(...)
-> internal TenantContext
-> controller/service permission enforcement
```

The Entra tenant ID returned by a token or consent flow is a lookup key for locating the right external identity namespace. It is not sufficient to grant access to insurer data.

---

## 4. Tenant onboarding flow

### 4.1 Required onboarding portal

IIP should provide a dedicated **IIP tenant onboarding portal** for insurer administrators.

The portal should support this sequence:

```text
1. IIP operator creates an internal pending Tenant record for the insurer.
2. IIP captures the insurer's legal/customer name, expected Entra tenant ID or verified domain, initial admin contact, target workspaces, and target Azure subscription ID(s) for Managed Application private-plan deployment.
3. Insurer admin opens the IIP onboarding portal.
4. Insurer admin signs in with a work or school Microsoft account from the insurer's own Entra tenant.
5. IIP redirects the admin to the Microsoft admin-consent endpoint.
6. Admin completes tenant-wide consent for the requested permission set.
7. Microsoft redirects back to IIP with admin_consent=True, tenant, scope, and state.
8. IIP validates state against the onboarding session.
9. IIP stores the returned external tenant identifier against the internal Tenant record as an external identity lookup key.
10. The Managed-Application-deploying admin, who may or may not be the same person as the Entra consent admin, supplies target Azure subscription ID(s) for the customer environment.
11. IIP stores the Azure subscription ID(s) against the same internal Tenant record as a separate field from ExternalEntraTenantId.
12. IIP flags CSP-reseller subscriptions as private-plan blockers before Partner Center targeting.
13. IIP creates a bootstrap membership for the consenting admin user in its own membership tables.
14. Runtime access still goes through AuthenticatedTenantResolver and IPermissionReadRepository.
```

### 4.1.1 Entra tenant ID versus Azure subscription ID

The onboarding portal must capture two separate identifiers when the customer will receive IIP through a Managed Application private plan.

| Identifier | Used for | Stored in IIP? | Used by Partner Center? | Runtime authority? |
|---|---|---:|---:|---:|
| Entra tenant ID / directory ID | Workforce authentication, admin consent, and external identity namespace. | Yes, as `ExternalEntraTenantId`. | Not for Azure Application private-plan audience targeting. | No. Runtime authority remains `AuthenticatedTenantResolver` + `IPermissionReadRepository`. |
| Azure subscription ID | Partner Center private-plan audience targeting and Managed Application deployment into the customer's Azure subscription. | Yes, as separate deployment-target metadata. | Yes, for Azure Application / Managed Application private-plan audience lists. | No. Subscription targeting does not grant application data access. |

Do not infer one from the other. A customer may have multiple subscriptions under one Entra tenant, and the person who completes Entra admin consent may be different from the person who owns Marketplace deployment into the target subscription.

### 4.1.2 Azure subscription capture for Managed Application private plans

The onboarding intake must include a Managed Application deployment-target step.

Rules:

```text
- Capture up to 10 Azure subscription IDs manually.
- Support CSV import for larger cases where the Partner Center audience list will exceed 10 subscriptions.
- Store the subscription IDs against the internal Tenant record as deployment-target metadata, not as identity authority.
- Validate GUID shape and normalize letters to lowercase before use in Partner Center configuration.
- Flag known CSP-reseller subscriptions as blockers because private plans are not supported for subscriptions established through a CSP reseller.
- Surface CSP/private-plan targeting blockers during onboarding, not at deployment time.
```

This step feeds the Managed Application private-plan audience list in Partner Center. It is a business/platform-operations configuration action that runs in parallel with, not inside, the application's request-time authorization pipeline.

### 4.1.3 Parallel dependency model

The onboarding portal should present the Entra tenant ID and Azure subscription ID(s) together in the same customer intake flow:

```text
ExternalEntraTenantId -> IIP authentication and membership resolution.
AzureSubscriptionIds -> Partner Center private-plan targeting and Managed Application deployment.
```

They are parallel dependencies:

```text
- Entra consent can succeed even before the private plan is targeted to the right subscription.
- Private-plan targeting can be configured for a subscription before staff users are fully mapped in IIP.
- Neither dependency bypasses AuthenticatedTenantResolver or IPermissionReadRepository.
```

### 4.2 Admin consent endpoint

The consent endpoint shape is:

```text
https://login.microsoftonline.com/{tenant}/v2.0/adminconsent
    ?client_id={iipClientId}
    &scope={requestedScopes}
    &redirect_uri={iipRegisteredRedirectUri}
    &state={signedOnboardingState}
```

Rules:

```text
- Use the specific tenant ID when known.
- organizations is acceptable for a generic organizational flow.
- Do not use common for admin consent.
- Always validate state after redirect.
- Never use the returned tenant parameter by itself to authenticate or authorize users.
```

### 4.3 What IIP stores after consent

Store:

```text
InternalTenantId
ExternalEntraTenantId
ConsentCompletedAtUtc
ConsentedScopes
ConsentedByExternalUserId = entra:{tid}:{oid} when tid and oid are available
ConsentCorrelationId
PublisherAppClientId
AzureSubscriptionIdsForManagedApplicationTargeting
ManagedApplicationPrivatePlanAudienceStatus
CspResellerSubscriptionBlockers
BootstrapMembershipCreatedBy
```

Do not store:

```text
Access tokens
Refresh tokens
Raw ID/access token payloads
Admin passwords
Tenant-wide secrets
Fine-grained IIP permissions from Entra claims
Partner Center credentials or publisher secrets
Azure subscription IDs overloaded into ExternalEntraTenantId
```

### 4.4 Bootstrap membership

The consenting admin should receive a minimal bootstrap role in IIP, such as:

```text
IIP Tenant Bootstrap Admin
```

This role allows:

```text
- Completing onboarding.
- Configuring initial workspaces.
- Inviting or mapping initial staff users.
- Assigning IIP roles/permissions inside IIP.
- Viewing identity setup status.
```

It must not automatically grant all operational permissions such as claim approval, payment release, or policy binding. Consequential operational permissions still require explicit IIP role/permission assignment.

---

## 5. Optional Entra-side coarse gating through app roles

### 5.1 Decision

IIP may expose optional app roles on its Entra app registration as coarse access gates.

Recommended initial app roles:

```text
IIP.Access     Baseline access gate for users allowed to enter IIP.
IIP.Admin      Coarse admin signal for onboarding/admin screens.
IIP.ReadOnly   Coarse read-only entry signal, if a tenant wants Entra-side coarse separation.
```

These are not IIP's fine-grained permission model.

### 5.2 Correct use

```text
Entra app roles answer: Who may enter IIP at all?
IIP role/permission store answers: What can this user do inside this tenant/workspace?
```

App roles may be used to:

```text
- Require assignment before sign-in.
- Let insurer admins assign users or groups to IIP.Access.
- Provide a coarse tenant-side security gate.
- Bootstrap local IIP membership workflows.
```

App roles must not be used to directly grant:

```text
claims.read
documents.review
approvals.review
payments.release
claims.approve
writeback.execute
agent_policy.manage
```

Those remain IIP-owned permissions resolved through `IPermissionReadRepository`.

### 5.3 Why raw group claims are rejected as the primary mechanism

Raw Entra group claims are not reliable enough as the primary IIP authorization mechanism at enterprise scale.

Reasons:

```text
- JWT group claims have a documented 200-group limit.
- Large enterprise users can hit group overage conditions.
- Group IDs and group names are tenant-specific and do not move safely across customer tenants.
- Token group membership reflects the time of token issuance, not necessarily the latest app authorization state.
- Group claims do not model IIP workspace, branch, queue, product-line, or approval-limit semantics.
```

Preferred enterprise pattern:

```text
Assign insurer groups to IIP app roles for coarse entry gating.
Provision users/groups into IIP through SCIM later if lifecycle automation is needed.
Keep runtime fine-grained authorization in IIP's own role/permission store.
```

---

## 6. SCIM provisioning as phase two

### 6.1 Decision

SCIM provisioning is a phase-two capability, not part of the first production wave unless a launch customer requires it contractually.

SCIM should be added after:

```text
- Multitenant Entra sign-in works.
- Admin consent onboarding works.
- IIP internal Tenant/User/Workspace/Role/Permission tables are stable.
- Bootstrap admin flows are implemented.
```

### 6.2 Purpose of future SCIM support

A future SCIM endpoint allows an insurer's identity administrator to automate:

```text
- User creation in IIP.
- User activation/deactivation.
- Group creation or mapping.
- Group membership updates.
- Deprovisioning when a staff member leaves the insurer.
```

### 6.3 Proposed SCIM endpoint shape

Future endpoints:

```text
GET    /scim/v2/ServiceProviderConfig
GET    /scim/v2/Schemas
GET    /scim/v2/ResourceTypes
POST   /scim/v2/Users
GET    /scim/v2/Users/{id}
GET    /scim/v2/Users?filter=userName eq "..."
PATCH  /scim/v2/Users/{id}
DELETE /scim/v2/Users/{id}
POST   /scim/v2/Groups
GET    /scim/v2/Groups/{id}
PATCH  /scim/v2/Groups/{id}
DELETE /scim/v2/Groups/{id}
```

Minimum user mapping:

```text
SCIM id                 IIP external provisioning ID
SCIM externalId         Entra object ID or customer's stable HR/identity ID
SCIM userName           Work UPN/email for display and onboarding lookup only
SCIM active             Enables/disables IIP login eligibility
SCIM name.givenName     Display metadata
SCIM name.familyName    Display metadata
SCIM emails             Display/contact metadata
SCIM groups             Optional group-to-role/workspace mapping input
```

### 6.4 SCIM does not replace runtime authorization

SCIM affects who exists in IIP's tables. It does not answer whether a request may access data.

Runtime still works as:

```text
JWT token
-> AuthenticatedTenantResolver
-> IPermissionReadRepository.GetEffectiveMembershipAsync(...)
-> TenantContext
-> permission checks
```

SCIM writes or updates the records that `IPermissionReadRepository` later reads. It never bypasses the repository and never grants permissions directly at request time.

---

## 7. Publisher verification

### Decision

Publisher verification is required before broad insurer rollout of the multitenant app registration.

Reason:

```text
- IIP is a multitenant OAuth/OIDC application.
- Insurer admins will see the publisher identity during consent and enterprise-app review.
- Verified publisher status increases trust in consent prompts.
- It aligns with Partner Center / Managed Application readiness work.
```

Implementation notes:

```text
- Complete publisher verification in the IIP publisher/home tenant.
- Ensure the app registration is owned by the verified publisher tenant.
- Confirm branding, privacy URL, terms URL, and support contact before customer consent testing.
- Treat publisher verification as a rollout blocker, not a nice-to-have.
```

---

## 8. Per-insurer app registration exception path

The default remains one multitenant app registration.

A per-insurer app registration is allowed only as an exception when:

```text
- A specific insurer's security team refuses the standard multitenant app model.
- The commercial agreement explicitly requires a customer-owned registration.
- Engineering accepts the added operational cost of customer-specific auth config.
```

Risks of exception path:

```text
- Multiple client IDs and audiences.
- Multiple issuer/authority configurations.
- More complex support and deployment validation.
- Higher chance of configuration drift.
- More difficult Managed Application packaging story.
```

If used, the exception path must still preserve:

```text
- AuthenticatedTenantResolver.
- IPermissionReadRepository.
- Server-side tenant/workspace resolution.
- No trust in token tenant claims as final business authority.
```

---

## 9. Non-goals

This document does not implement:

```text
- Microsoft Entra External ID / CIAM for insurer staff.
- Consumer-facing policyholder identity.
- Per-insurer app registration as the default model.
- Fine-grained authorization through raw group claims.
- Fine-grained authorization through custom claims-mapping policies.
- SCIM in the first wave unless separately prioritized.
- Any change to AI/human-review decision boundaries.
```

---

## 10. Open questions requiring live verification before implementation

| ID | Question | Why it matters | Owner |
|---|---|---|---|
| OQ-ID-001 | Exact .NET 8 / Microsoft.Identity.Web multitenant configuration syntax for the package version IIP will ship. | The architectural direction is fixed, but overloads/options can differ by package version. | Backend lead |
| OQ-ID-002 | Current Entra admin-center UX for admin consent and enterprise-app assignment. | Portal labels and navigation move over time; onboarding guide must match current UI. | Implementation/Azure lead |
| OQ-ID-003 | Should IIP require enterprise-app assignment from day one? | This is a product/security decision: stronger entry control but more insurer admin setup. | Product + security |
| OQ-ID-004 | Should SCIM be first production wave or phase two? | SCIM helps enterprise lifecycle management but adds a separate standards-compliance workstream. | Product + customer success |
| OQ-ID-005 | Which exact app roles should be exposed in the app registration at launch? | App roles are visible to insurer admins and should be stable. | Product + security |
| OQ-ID-006 | Whether each Managed Application deployment needs customer-specific identity callback/redirect URI handling. | Managed Application packaging and multitenant app registration are separate concerns, but deployment-specific URLs may affect app registration configuration. | Platform lead |
| OQ-ID-007 | How the onboarding portal will verify whether supplied Azure subscription IDs are CSP-reseller subscriptions. | Private plans are not supported for CSP-reseller subscriptions, so this must be surfaced before Partner Center targeting. | Platform + business operations |
| OQ-ID-008 | Whether the first customer requires more than 10 subscription IDs and therefore CSV-based private-plan audience import. | Partner Center supports manual entry only up to the documented limit; larger audiences need CSV handling. | Business operations |

---

## 11. Tests to add

### 11.1 Multitenant token acceptance

```text
Given a token from an organizational tenant whose issuer is not IIP's publisher tenant
When audience and signature are valid
Then JWT middleware accepts the token
And AuthenticatedTenantResolver performs membership lookup before controller execution.
```

### 11.2 Publisher tenant not treated as customer tenant

```text
Given InfrastructureOptions.EntraTenantId is the IIP publisher/home tenant
When a user from an insurer tenant signs in
Then the API does not require token tid to equal InfrastructureOptions.EntraTenantId.
```

### 11.3 tid is not authority

```text
Given a valid token with tid for Insurer A
And no matching IIP membership exists
When the user calls /api/claims
Then the request fails before any tenant data is returned.
```

### 11.4 tid+oid external identity key

```text
Given a token with tid and oid
When AuthenticatedTenantResolver constructs the external user key
Then it uses entra:{tid}:{oid}
And does not use email, preferred_username, unique_name, or upn.
```

### 11.5 App roles do not grant fine-grained permission

```text
Given a token with roles = [IIP.Access]
And IIP membership lacks claims.read
When the user calls GET /api/claims
Then the request returns 403.
```

### 11.6 Group overage does not break IIP permissions

```text
Given a token without groups due to group overage
And IIP membership grants claims.read
When the user calls GET /api/claims
Then access is based on IPermissionReadRepository, not group claims.
```

### 11.7 Managed Application subscription ID is not tenant authority

```text
Given an internal Tenant record has Azure subscription IDs for Managed Application targeting
When a user signs in with a valid Entra token
Then request-time TenantContext is still resolved only by AuthenticatedTenantResolver and IPermissionReadRepository
And Azure subscription IDs are not used as authorization authority.
```

### 11.8 CSP-reseller subscription blocker is surfaced

```text
Given onboarding receives an Azure subscription ID identified as CSP-reseller-backed
When the customer attempts to proceed to private-plan targeting
Then the portal surfaces a blocking status before deployment
And does not mark the Partner Center private-plan audience step as ready.
```

### 11.9 SCIM provisioning is not runtime authorization

```text
Given SCIM has created a user record but no active role assignment exists in IIP
When that user signs in and calls an API
Then AuthenticatedTenantResolver denies membership/permission as configured.
```

---

## 12. Reference notes

This document is based on the completed Entra identity-model research and should be rechecked against current Microsoft Learn before implementation.

Reference areas to verify live:

```text
- Protected web API multitenant configuration with Microsoft.Identity.Web.
- Microsoft identity platform admin-consent protocol.
- Claims validation guidance, especially tid + oid as immutable application key.
- App roles versus groups guidance.
- Group overage behavior and 200-group JWT limit.
- SCIM endpoint requirements for Microsoft Entra provisioning.
- Publisher verification requirements.
- Microsoft Entra External ID product boundaries.
- Partner Center private-plan Azure subscription ID targeting and CSP-reseller limitation.
- Azure Managed Application publisher/customer access, deny assignment, and JIT behavior.
```

---

## 13. Completion report

```text
Document produced:
- IIP Multi-Tenant Identity Onboarding, App Roles, and SCIM Provisioning.

Companion wiring document produced:
- IIP Backend Wiring — Controllers, Composition Root, and Request Pipeline — Multitenant Identity Reconciled.

Files/sections changed in the wiring document:
- Reconciliation changelog: added Item 6 for multitenant Entra authentication correction.
- Section 6.2 Entra JWT registration: replaced fixed-issuer/single-tenant AddJwtBearer sample with organizations-mode Microsoft.Identity.Web pattern.
- Section 6.2 Entra field interpretation: clarified EntraTenantId is IIP publisher/home tenant, not insurer tenant.
- Section 6.3.1 External identity key strategy: added tid+oid preferred external identity key guidance with sub-only fallback.
- Section 12 Manual Azure / Entra actions: updated for multitenant app registration, publisher verification, admin consent, and optional app roles.
- Section 13 Completion report template: updated authentication/authorization impact, manual actions, blockers, and companion document path.

Security impact:
- Strengthens authentication configuration by accepting valid organizational tokens through multitenant authority mode instead of incorrectly binding the API to one insurer issuer.
- Preserves server-side tenant/workspace authorization through AuthenticatedTenantResolver and IPermissionReadRepository.
- Reduces risk of using mutable or unsafe claims by requiring tid+oid for external identity keys where available.
- Rejects raw group claims as primary authorization because of group overage and cross-tenant portability issues.

Tenant isolation impact:
- Tenant isolation guarantees are strengthened, not changed.
- JWT tid is a lookup hint only.
- Internal TenantContext remains the sole request-time authority for TenantId, WorkspaceId, Roles, and Permissions.
- No route, controller, token claim, browser value, or onboarding callback grants direct business access without repository membership validation.

AI/human-review impact:
- No change to AI/human-review boundaries.
- AI remains unable to approve/reject claims, bind policies, set premium, release payment, or determine fraud.

TODOs:
- Verify exact Microsoft.Identity.Web configuration syntax against the package version used by the .NET 8 API.
- Verify current Microsoft Entra admin-center screens for consent and enterprise-app assignment before writing customer onboarding instructions.
- Decide whether enterprise-app assignment is required for all insurer tenants from day one.
- Decide SCIM timing: first production wave or phase two.
- Confirm final launch app roles and role display names before publisher verification/customer consent testing.

Managed Application onboarding extension added:
- Section 2 Fixed architectural rules: added explicit separation of Entra tenant ID and Azure subscription ID.
- Section 4.1 Required onboarding portal: added Azure subscription ID capture, CSP-reseller blocker, and separation from identity consent.
- Section 4.1.1: added Entra tenant ID versus Azure subscription ID comparison.
- Section 4.1.2: added Managed Application private-plan subscription capture rules, manual/CSV handling, and CSP-reseller blocker.
- Section 4.1.3: clarified Entra consent and Partner Center private-plan targeting as parallel dependencies.
- Section 4.3: added AzureSubscriptionIdsForManagedApplicationTargeting, ManagedApplicationPrivatePlanAudienceStatus, and CspResellerSubscriptionBlockers to stored onboarding metadata.
- Section 10: added live-verification questions for CSP-reseller detection and private-plan audience import scale.
- Section 11: added tests proving Azure subscription IDs do not become runtime tenant authority and that CSP blockers surface before deployment.

Managed Application research source basis:
- Completed Azure Managed Application / Partner Center research.
- Microsoft Learn private-plan targeting guidance.
- Microsoft Learn Azure managed application plan guidance.

Managed Application security impact:
- Strengthens onboarding by preventing Entra tenant ID and Azure subscription ID from being conflated.
- Preserves AuthenticatedTenantResolver and IPermissionReadRepository as the only request-time tenant/workspace authorization authority.
- Prevents Marketplace private-plan targeting from being mistaken for application authorization.

Managed Application tenant isolation impact:
- No weakening of tenant/workspace isolation.
- Azure subscription IDs are deployment-target metadata only.
- Internal TenantContext remains controlled by IIP membership lookup.

Recommended next step:
- Update the real AuthenticationRegistration.cs implementation to organizations-mode Microsoft.Identity.Web configuration, then add tests proving tokens from non-publisher organizational tenants are accepted by JWT validation but still denied without a matching IPermissionReadRepository membership.
- Extend the onboarding portal backlog with fields for ExternalEntraTenantId and AzureSubscriptionIdsForManagedApplicationTargeting, including CSP/private-plan targeting validation.
```
