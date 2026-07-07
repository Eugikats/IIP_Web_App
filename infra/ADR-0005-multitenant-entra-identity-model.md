# ADR-0005 — Multitenant Microsoft Entra Identity & Runtime Authorization Model

- **Status:** Accepted
- **Date:** 2026-07-06
- **Deciders:** Product, Architecture, Security, Backend
- **Gate:** Gate 1 (real identity, authorization, config validation, Key Vault/secrets)
- **Supersedes/formalizes:** `docs/02-development-engineering/iip_multi_tenant_identity_onboarding_app_roles_scim_provisioning.md`
- **Related:** AGENTS.md §26 (required ADR: "Multitenant Entra identity model"), ADR-0004 (route versioning)

## Context

IIP is staff-facing B2B SaaS for insurer employees. Insurer staff authenticate through their own
workforce Microsoft Entra tenants; IIP is a single **multitenant** app registration ("accounts in
any organizational directory", authority `organizations`). Entra proves *who a user is in their
directory*; it does not, by itself, establish *what that user may do in IIP's data*.

The Gate 1 audit found the implementation had taken shortcuts that contradict the identity spec:
authorization resolved the **role from the token `roles` claim** into an in-memory map, and tenant
scope was read from the token `tid`/claims. The spec's fixed rules forbid both.

## Decision

1. **Identity provider:** one multitenant Entra app registration; authority `organizations`; JWT
   validated by `Microsoft.Identity.Web` (audience, issuer, lifetime, signing key). No per-insurer
   app registrations except as an explicit exception path.
2. **The token's `tid` is never IIP tenant authority.** It is only a lookup *hint* to locate the
   insurer's external identity namespace.
3. **Runtime authority is IIP's own store.** The internal `TenantContext` and all fine-grained
   permissions (`ViewAllClaims`, `ApproveSettlementWorkflowStep`, …) are resolved server-side from
   IIP's membership/role tables (`UserTenantMemberships` → `RoleAssignments` → `RolePermissions`)
   keyed by the authenticated user's stable id (`oid`), via
   `IPermissionReadRepository` / `AuthenticatedTenantResolver`.
4. **Entra app roles are coarse entry gates only.** An `IIP.Access` app role answers "may this user
   enter IIP at all?" It never conveys fine-grained permissions.
5. **Raw Entra group claims are not an authorization mechanism** (200-group token limit, tenant-
   specific IDs, staleness). Custom claims-mapping is not the permission source of truth.
6. **Fail closed.** An authenticated user with no active IIP membership for the resolved tenant/
   workspace receives no data and no permissions — never a cross-tenant leak, never a default role.
7. **Development mode** retains header-based demo identity (`x-demo-role`/`x-demo-user-id`), rejected
   by `RuntimeHeaderGuard` in every deployed profile.
8. **SCIM** user/group provisioning is phase two; when added it writes membership data into IIP
   tables but never replaces runtime authorization checks.

## Runtime flow (authoritative)

```text
Validated JWT (oid, tid hint, IIP.Access app role)
  -> app-role entry gate: IIP.Access present?              (coarse: may enter IIP)
  -> AuthenticatedTenantResolver(oid, tid hint)
       -> IPermissionReadRepository.GetEffectiveMembershipAsync(oid, tenantHint)
       -> internal TenantContext { TenantId, WorkspaceId, UserId, Permissions }
  -> PermissionEndpointFilter / IPermissionService enforce fine-grained permissions
       from the resolved membership (NOT from token roles)
```

## Consequences

- **Positive:** IIP authorization is defensible and tenant-safe; a forged/over-privileged token
  role claim cannot grant IIP permissions; cross-tenant isolation is enforced by the membership
  store; the model matches the documented spec.
- **Work required (Gate 1):** add `AuthenticatedTenantResolver` + `GetEffectiveMembershipAsync`;
  make the API-layer authorization (`DemoAuthorization`/`PermissionEndpointFilter`) resolve
  permissions from the store for the authenticated user instead of the token role claim; enforce the
  `IIP.Access` entry gate; bootstrap membership on admin-consent onboarding.
- **Manual Azure:** define the app registration, the `IIP.Access` app role, admin-consent, and
  (separately) Key Vault + managed identity. Tracked in the Gate 1 provisioning runbook.
- **Neutral:** Development mode is unchanged; deployed profiles already reject demo headers.
