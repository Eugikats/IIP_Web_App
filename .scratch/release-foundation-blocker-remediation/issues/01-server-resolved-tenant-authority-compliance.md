# 01 — Server-resolved tenant authority for Compliance

**What to build:** Compliance posture, audit listing, audit-pack, and export only reflect the signed-in membership’s tenant/workspace. Spoofed headers or query scope cannot redirect results. Missing membership fails closed outside LocalTest. Cross-tenant case access returns empty/not-found. Operators can demo a safe compliance path without browser-owned authority.

**Blocked by:** None — can start immediately.

**Status:** done

- [x] Compliance surfaces resolve scope only from server membership / TenantContext
- [x] Missing context and cross-tenant access fail closed (proven at HTTP API seam)
- [x] Aggregation lives in Application layer; API stays thin
- [x] Backend validation for this slice is green
