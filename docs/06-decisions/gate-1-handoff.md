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
| G1.3 | Reconcile API-layer authz to the store; `IIP.Access` entry gate | ⏳ | Security-critical — claim before editing. Stop trusting token `tid`/`roles` in deployed mode. |
| G1.5 | Tests (resolver, DB-authoritative authz, entry gate, fail-closed) | ⏳ | |
| G1.6 | Key Vault config provider (deployed-only) + config/identity readiness validator | ⏳ | `Azure__KeyVaultUri` + `DefaultAzureCredential`. |
| G1.7 | Full onboarding portal + admin-consent flow (model, endpoints, bootstrap membership, UI) | ⏳ | Per identity spec §4. |
| G1.8 | Azure/Entra provisioning runbook | ⏳ | So owner can provision + we prove live. |

## Commit discipline
Every implementation is committed separately with a descriptive message. Handoff state lives in this
doc — update the table above as items land so the other clone can pick up without collision.

## Validation bar (unchanged)
`dotnet build` 0 errors · `dotnet test` (237 API + 96 Domain baseline) · `pnpm build`/`lint` green ·
no fabricated success paths · fail closed on missing membership.
