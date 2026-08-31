# 03 — Immutable workflow version lineage and run pinning

**What to build:** Authors can create successive immutable versions under one stable workflow key (draft → publish audited). Runs that already started keep executing against the exact published definition they started with after a newer version is published.

**Blocked by:** None — can start immediately.

**Status:** ready-for-agent

- [ ] Identity/uniqueness is workflow key + version
- [ ] Draft-new-version and publish transitions are explicit and audited
- [ ] In-flight runs remain pinned to their starting definition
- [ ] Concurrency/migration/API contract tests agree
