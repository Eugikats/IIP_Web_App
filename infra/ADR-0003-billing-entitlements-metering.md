# ADR-0003: Billing, Entitlements, and Usage Metering Model

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-07-04 |
| Deciders | Architecture (per IIP Full-Program Plan, Initiative LP; Product Decision Log DEC-013) |
| Supersedes | — |
| Related | `AGENTS.md` §2 (no-mock / `Not configured`), tenant isolation guardrails (`IIP_IMPLEMENTATION_GUARDRAILS.md` §5), `docs/03-design-architecture/iip_saas_tenant_architecture_guide.md`, `docs/01-strategy-requirements-roadmap/iip_product_decision_log.md` (DEC-013), OQ-010 / D8 in `docs/06-decisions/open-questions-tracker.md` |

## Context

The Lovable reference includes a Billing / entitlements surface (module entitlements, seat counts, usage meters for AI/documents/channels/storage, overage indicators, upgrade request). IIP has no billing domain today. Commercial pricing model (OQ-010) and Azure cost ownership (D8) are open business questions, and the commercial default is one insurer per Managed Application instance with logical workspaces inside. This ADR fixes what is built now versus deferred, and how billing state respects tenant isolation and the no-mock rule.

## Decision

1. **Entitlement + usage tracking is real and built now; external billing/payment integration is deferred.** The domain models entitlements (which modules/seats a tenant is licensed for) and computes usage meters from **actual internal counters the platform already emits** (AI generation calls, documents processed, active channels, storage consumed). Overage is a real comparison of measured usage against the configured entitlement limit. No usage number is fabricated for display — an unmetered dimension renders as `Not configured`, not as a zero or a sample value.

2. **No external metering/billing/payment provider is integrated in v1.** Invoicing, payment capture, and marketplace metered-billing emission are **Gate-3+ / Phase-9 concerns** gated behind the existing `Not configured` pattern. "Upgrade request" is a governed, audited internal request record — it does not transact.

3. **All billing state is tenant/workspace-scoped and server-enforced.** Entitlements, seat assignments, usage records, and overage state carry tenant context and are read/written only through the existing `ITenantContextAccessor` + `TenantScope` repository pattern. A tenant can never see another tenant's entitlements or usage. Because the commercial default is one insurer per instance, the primary scope is the tenant; workspace-level sub-metering is modeled but may aggregate to the tenant for v1.

4. **Entitlement checks do not replace authorization.** Entitlements gate *feature availability* (is this module licensed for this tenant); they never replace the per-user role/permission checks (`PermissionService`, `RolePermissionMap`). Both apply.

## Reasoning

Basing usage meters on counters the app genuinely emits keeps the feature fully functional inside Gate 0 with zero external dependency, while honoring `AGENTS.md`'s prohibition on fabricated dashboard statistics. Deferring the external billing/payment integration matches the open commercial decisions (OQ-010, D8) — building a transacting billing engine before pricing and cost ownership are decided would be speculative and likely rework. Scoping all billing state to the tenant via the established isolation pattern avoids inventing a parallel isolation mechanism. Keeping entitlement checks orthogonal to authorization prevents a licensing state from ever silently widening a user's permissions.

## Consequences

- LP.6 builds real tenant-scoped entitlement/seat/usage-meter domain + query services + `/admin/billing` UI, wired to existing internal counters; usage tests assert computed-not-fabricated values and tenant isolation.
- External billing/metered-marketplace emission is explicitly out of scope until a pricing decision (OQ-010) and the Marketplace track (Phase 9) are active; any such need is forward-referenced in the env-vars/packaging guides when it arises.
- Revisited if the commercial model (OQ-010) or deployment/cost model (D1/D8) is decided in a way that requires transacting billing before Phase 9.
