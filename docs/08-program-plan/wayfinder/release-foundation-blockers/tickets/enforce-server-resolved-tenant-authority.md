# Enforce Server-Resolved Tenant Authority in Compliance

**Label:** `wayfinder:prototype`  
**Status:** Done  
**Claim:** Auto session - RF-01 Compliance tenant authority  
**Blocked by:** None

## Question

What is the smallest Application-layer compliance posture/export design that removes all header/query tenant authority, fails closed outside isolated LocalTest, uses tenant-scoped repository methods, and keeps API endpoints thin?

## Resolution criteria

- [x] Exact service and repository contracts decided and implemented (`IComplianceQueryService` / `ComplianceQueryService`).
- [x] Request-derived tenant/workspace fallback removed from Compliance deployable paths (`TryRequireTenantContext` only).
- [x] Cross-tenant and missing-context tests prove fail-closed behavior (`ComplianceTenantAuthorityTests`).
- [x] Compliance aggregation moves out of endpoint code.
- [x] Full backend validation passes (`InsuranceIntelligence.Api.Tests`).

## Answer / Resolution

Compliance uses `IComplianceQueryService` with membership `TenantContext` only; header/query fallback removed; Aggregation in Application; API thin; HTTP seam tests in `ComplianceTenantAuthorityTests` green.
