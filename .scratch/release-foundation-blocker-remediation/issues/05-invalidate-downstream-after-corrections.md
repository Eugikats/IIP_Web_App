# 05 — Invalidate and re-evaluate downstream state after corrections

**What to build:** When a human corrects extracted document fields, any stale downstream workflow work is invalidated and re-evaluated before triage/draft continues. Corrections never silently proceed on obsolete extraction; the action is auditable.

**Blocked by:** 02 — Human review on every consequential workflow path.

**Status:** ready-for-agent

- [ ] Correction invalidates affected downstream workflow state
- [ ] Re-evaluation runs before triage/draft proceed
- [ ] Audit trail records invalidation and re-evaluation
- [ ] API/contract tests prove no silent proceed on stale extraction
