# 04 — Bound orchestration to Release 1

**What to build:** Only Release 1 allow-listed tools and graphs can be published into or executed on the Release 1 orchestration path. Release 2–5 / post-MVP surfaces cannot sneak into the worker queue or publish contract.

**Blocked by:** None — can start immediately (must land before the worker).

**Status:** implementing on `harry/rf-bound-orchestration-release-1` (#15)

- [x] Release 1 allow-list documented and enforced at publish/execute boundaries
- [x] Out-of-scope tools/graphs rejected with honest errors
- [x] Canonical Release 1 path remains executable
- [x] Contract tests prove R2+ surfaces are rejected
