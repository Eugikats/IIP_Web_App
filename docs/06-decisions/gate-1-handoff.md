# Gate 1 — Handoff & Progress Notes (for coordinating agents)

| Field | Value |
|---|---|
| Gate | 1 — Real identity, authorization, config validation, Key Vault/secrets (AGENTS.md §25) |
| Branch | `feature/lovable-design-parity` (also mirrored to `gate-0-completion` for PR #2) |
| Started | 2026-07-06 |
| Coordination | Two-clone workflow (this clone + Codex). Coordinate only via git push/pull. **Do not both edit the auth pipeline at once** — G1.2/G1.3 touch security-critical code; claim it before starting. |

## Owner decisions (locked)
- **Azure provisioning:** owner will provision Azure/Entra **as we go** (app registration, app roles, admin consent, Key Vault, managed identity). Build must be live-provable, with an exact runbook.
- **First slice:** auth-model reconciliation (DB-authoritative authorization; Entra app roles = coarse `IIP.Access` entry gate; stop trusting token `tid`/`roles`).
- **Onboarding:** build the **full onboarding portal + admin-consent flow** this gate.

## Authoritative model
See **ADR-0005** (`infra/ADR-0005-multitenant-entra-identity-model.md`) and
`docs/02-development-engineering/iip_multi_tenant_identity_onboarding_app_roles_scim_provisioning.md`.
Key rules: token `tid` is a hint only; runtime authority = IIP membership store via
`IPermissionReadRepository`; app roles are coarse entry gates; fail closed on no membership.

## Existing building blocks (already in the repo — reuse, don't rebuild)
- `IPermissionReadRepository.GetPermissionsAsync(tenant, workspace, userId)` — **DB-authoritative** permission resolution over `UserTenantMemberships` → `RoleAssignments` → `RolePermissions` (`EfCorePorts.cs`). Keyed by userId (= Entra `oid`).
- `PermissionService` (Application) already merges DB permissions + the in-memory `RolePermissionMap` fallback for the context role.
- `HttpContextTenantContext` (`Api/Common/ApiTenantContext.cs`) — resolves scope from headers (Dev) or claims (Entra); already fails closed on unmapped tenant; comment names the `AuthenticatedTenantResolver` follow-on.
- `DemoAuthorization` (`Api/Auth`) — **the shortcut to fix**: in Entra mode it reads the role from the token `roles` claim → in-memory map. Must resolve permissions from the store instead.
- Membership entities: `UserTenantMembershipEntity`, `RoleAssignmentEntity`, `RoleEntity`, `RolePermissionEntity`, `PermissionEntity` (`InsuranceIntelligenceDbContext.cs`).

## Gate 1 work items
| ID | Item | Status | Notes |
|---|---|---|---|
| G1.4 | ADR-0005 multitenant Entra identity model | ✅ done | Formalizes the spec decision. |
| G1.2 | `GetEffectiveMembershipAsync` (DB-authoritative membership resolver) | ✅ done | `IPermissionReadRepository.GetEffectiveMembershipAsync(oid, tenantHint, workspaceHint)` + EF impl + `EffectiveMembership` record. Fails closed on no/ambiguous membership; hint disambiguates. 2 tests. AuthenticatedTenantResolver (HttpContext wiring) folds into G1.3. |
| G1.3 | Reconcile API-layer authz to the store | ✅ done | Per-request middleware resolves `GetEffectiveMembershipAsync` in deployed mode, sets TenantContext from the store (not token `tid`) + stashes permissions in `HttpContext.Items`; `DemoAuthorization` deployed-mode checks the stashed store permissions, never the token `roles`; fail-closed on no membership. Covers the filter + all ~20 `DemoAuthorization.Require` sites at once. Tests updated + added (238 API pass). **G1.3b follow-on:** `ClaimsController.TrySetTenantContext` + `HttpContextTenantContext` still self-resolve scope from claims — make them defer to the middleware-resolved context in deployed mode. `IIP.Access` coarse entry gate is handled Entra-side via app assignment (runbook §3), optional in code per spec §5.1. |
| G1.5 | Tests (resolver, DB-authoritative authz, fail-closed) | ✅ done (core) | Resolver + deployed-mode authz allow/deny/fail-closed + token-role-ignored tests. Cross-tenant HTTP-level isolation test is a nice add for G1.3b. |
| G1.6 | Key Vault config provider | ✅ done (core) | `builder.Configuration.AddAzureKeyVault(Azure:KeyVaultUri, DefaultAzureCredential())` — activates only when the URI is set (local dev unaffected); secrets via managed identity, none in files/env. Identity settings already validated at startup (RequireSetting for Entra tenant/client; ThrowIfProductionInvalid for providers/seed/mocks). Optional enhancement: a single unified readiness report. |
| G1.7 | Full onboarding portal + admin-consent flow (model, endpoints, bootstrap membership, UI) | ⏳ | Per identity spec §4. |
| G1.8 | Azure/Entra provisioning runbook | ✅ done | `docs/05-deployment-release-enablement/iip_gate1_azure_entra_provisioning_runbook.md`. Owner provisions app registration + `IIP.Access` app role + admin consent + Key Vault + managed identity from this. |

## Commit discipline
Every implementation is committed separately with a descriptive message. Handoff state lives in this
doc — update the table above as items land so the other clone can pick up without collision.

## Validation bar (unchanged)
`dotnet build` 0 errors · `dotnet test` (237 API + 96 Domain baseline) · `pnpm build`/`lint` green ·
no fabricated success paths · fail closed on missing membership.
