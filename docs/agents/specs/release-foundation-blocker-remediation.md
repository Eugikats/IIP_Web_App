# Release Foundation Blocker Remediation

**Tracker ID:** IIP-RF-001
**Triage:** `ready-for-agent`
**Wayfinder map:** `docs/08-program-plan/wayfinder/release-foundation-blockers/MAP.md`
**Implementation branch (first ticket):** `harry/rf-enforce-server-tenant-authority`
**Authority:** `AGENTS.md` (highest), then `IIP_IMPLEMENTATION_GUARDRAILS.md`, `CLAUDE.md`, consolidated roadmap, Bevaya-parity release blueprint

---

## Problem Statement

Release-foundation work is now reachable from `main`, but it is not yet demonstrably merge-safe or Release 1 dev-gate-ready. Compliance and related surfaces can still fall back to request-derived or deployment-configured tenant scope instead of membership-resolved TenantContext. Human review is only partially enforced on workflow graphs, so consequential or external actions can still complete without an unavoidable human gate. Workflow versions are mutable in practice and runs are not reliably pinned to the definition they started with. There is no real tenant-scoped execution worker for agent-workflow messages. Merge governance, Azure SQL / Document Intelligence prove, authenticated UAT, and tail-latency evidence are incomplete or blocked on external facts. Operators cannot trust that CI green means Release 1 readiness, and external blockers risk being mislabeled as done.

## Solution

Remediate blockers in Wayfinder ticket order until the destination is met: server-resolved tenant authority everywhere deployable, unavoidable human review on every consequential path, correct correction replay, immutable workflow version lineage, a real tenant-scoped execution worker limited to Release 1, enforced release sequencing and merge governance, passing CI, and truthful Azure/UAT/performance evidence. External dependencies are closed with live proof or recorded as human-owned blockers—never disguised as completed. Work one non-research ticket per session; claim before coding; record resolution on the ticket.

**Confirmed primary test seam:** HTTP API tenant-isolation / contract tests in the existing API test project (membership-resolved scope, fail-closed missing context, cross-tenant denial). Later tickets extend the same outer seam (API and worker message contract) rather than inventing new low-level seams.

## User Stories

1. As a claims officer, I want compliance and audit views to show only my insurer workspace, so that I never see another tenant’s evidence.
2. As a compliance reviewer, I want audit-pack export to use my signed-in membership, so that exports cannot be redirected by forged headers.
3. As a security engineer, I want missing TenantContext to fail closed outside LocalTest, so that deployable environments never invent scope.
4. As a platform operator, I want LocalTest-only doubles isolated from Staging/Production, so that zero-simulation rules hold.
5. As an auditor, I want every compliance aggregation decision traced to server-resolved TenantId and WorkspaceId, so that evidence packs are defensible.
6. As a developer, I want thin compliance API endpoints, so that business rules live in Application services.
7. As a tenant admin, I want spoofed `X-Tenant-Id` ignored when membership is present, so that browser state is never authority.
8. As a penetration tester, I want cross-tenant case IDs to return not-found or empty, so that enumeration does not leak existence across tenants.
9. As a claims supervisor, I want every consequential workflow path to stop for human review, so that AI cannot silently approve, deny, settle, or send regulated communications.
10. As a workflow author, I want unsafe graphs rejected at publish, so that alternate branches cannot bypass HumanReview.
11. As a workflow author, I want cycles and unreachable consequential nodes to fail validation, so that incomplete graphs cannot ship.
12. As an AI agent runtime, I want ApprovalDecisionGuard (or equivalent ceiling) to block agent actors from consequential Decide/Send/Execute, so that humans remain accountable.
13. As a document reviewer, I want field corrections to invalidate stale downstream workflow state, so that triage and drafts do not proceed on obsolete extraction.
14. As a document reviewer, I want re-evaluation to re-run only the affected downstream steps, so that corrections are efficient and auditable.
15. As a workflow author, I want successive immutable versions under one workflow key, so that I can revise without rewriting identity.
16. As a workflow author, I want draft-new-version and publish transitions audited, so that lineage is reconstructible.
17. As a claims officer, I want in-flight runs pinned to the definition version they started on, so that mid-run edits do not change behavior underfoot.
18. As a platform engineer, I want uniqueness on workflow key plus version, so that concurrent publishes cannot collide.
19. As a Release 1 product owner, I want orchestration bounded to approved Release 1 tools and graphs, so that R2–R5 surfaces cannot sneak into the worker.
20. As an operations engineer, I want a real Functions/Durable worker consuming agent-workflow messages, so that execution is not a fake success path.
21. As an operations engineer, I want the worker to re-resolve tenant authority per message, so that queued work cannot carry stale spoofed scope.
22. As an operations engineer, I want idempotent retries and dead-letter handling, so that duplicate deliveries do not double-send or double-write.
23. As a compliance officer, I want the worker to stop safely at human review, so that automation never crosses the decision boundary.
24. As a document privacy officer, I want raw OCR/bytes to stop at extraction, so that later agents never see raw document content.
25. As an engineering lead, I want merge governance with required checks, so that broken main cannot ship silently.
26. As an engineering lead, I want an honest alternative if GitHub required checks are plan-limited, so that governance is real rather than aspirational.
27. As a DBA/operator, I want consolidated Azure SQL migrations applied in a recorded maintenance window, so that schema matches Release 1 code.
28. As a platform operator, I want Document Intelligence capacity and region verified, so that extraction is real or explicitly Not configured.
29. As a QA lead, I want authenticated UAT scripts against real staging providers, so that acceptance is evidence-backed.
30. As a performance engineer, I want tail-latency gates measured on representative paths, so that sponsored-subscription limits are visible.
31. As an executive sponsor, I want a final go/no-go pack that separates code-complete from human-owned blockers, so that launch decisions are honest.
32. As a founder, I want PAYG / quota / legal blockers listed without fake green, so that commercial risk is clear.
33. As a CI consumer, I want backend, portal, and infra jobs to stay green after each ticket, so that remediation does not regress T1 CI.
34. As a portal operator, I want UI release gates to keep rejecting browser-owned tenant/actor authority in request bodies, so that frontend cannot reintroduce spoofing.
35. As a support engineer, I want provider gaps to render Not configured, so that demos never simulate success.
36. As a claims officer, I want customer-safe communications to remain draft-then-approve, so that regulated sends stay human-gated.
37. As a workflow publisher, I want Canonical Release 1 graphs to still publish after stricter validation, so that remediation does not break the known-good path.
38. As a security reviewer, I want audit events for publish, decide, export, and worker stops, so that investigations have a trail.
39. As a multi-workspace user, I want effective membership to choose workspace, so that branch/department isolation holds.
40. As a Partner Center reviewer (future), I want Managed Application republication explicitly out of scope here, so that this program does not pretend packaging is done.
41. As an implementing agent, I want one claimed Wayfinder ticket per session, so that work stays gated and reviewable.
42. As an implementing agent, I want resolution criteria written back to the ticket, so that the map’s Decisions so far stays current.
43. As a release manager, I want blocked tickets to stay blocked until prerequisites close, so that worker and UAT do not start early.
44. As a developer, I want new branches named `harry/…` from current `origin/main`, so that stack hygiene matches repo convention.
45. As a maintainer, I want unrelated untracked user files left alone, so that local research zips and drafts are not force-committed.

## Implementation Decisions

- Follow the Wayfinder map ticket order; first claimed implementation ticket is **Enforce Server-Resolved Tenant Authority in Compliance**.
- Compliance aggregation moves into an Application-layer compliance/query service; API endpoints remain thin adapters.
- Deployable paths resolve scope only via `ITenantContextAccessor` / membership middleware; remove header/query and unprotected deployment-scope fallback from Compliance (and align sibling endpoints that share the same anti-pattern when in scope of a ticket).
- Repository methods used by compliance remain tenant/workspace-scoped; cross-tenant access returns not-found or empty, never another tenant’s rows.
- Introduce or restore a platform **ApprovalDecisionGuard** (or equivalent named ceiling) and wire it at Application decision/send/execute boundaries; tenant policy may narrow, never widen.
- Workflow publish validation must prove **every executable path** that includes consequential or external side effects reaches HumanReview before Complete—not merely that some path exists.
- Correction replay invalidates and re-evaluates downstream workflow state before triage/draft proceeds.
- Workflow identity is stable **workflow key + version**; publish creates immutable lineage; runs pin to `DefinitionId` / version for the whole run lifetime (no key-only re-resolve to a newer definition).
- Release 1 worker consumes allow-listed `agent-workflow` messages with managed identity, server-resolved scope, idempotency, retry/dead-letter, and hard stop at human review; no raw OCR beyond extraction.
- Bound orchestration and tool allow-list to Release 1 only.
- Reinstate merge governance with required CI checks or a documented equivalent if GitHub plan limits block native branch protection.
- Azure SQL migration and Document Intelligence prove are operator-gated; record blockers honestly in evidence packs.
- Do not republish Managed Application / Partner Center offers in this program.
- Protected runtimes remain zero-simulation and fail closed.

## Testing Decisions

- Good tests assert **external behavior** only: status codes, safe bodies, tenant isolation, publish rejection reasons, run pinning, worker stop conditions—not private helpers or EF internals.
- **Primary seam (confirmed):** HTTP API contract tests in `InsuranceIntelligence.Api.Tests`, extending existing membership-spoof and Compliance posture patterns (`SecurityAuthorizationTests`, case/claims membership tests, Compliance scope tests).
- Compliance ticket: missing context fail-closed; attacker headers ignored when membership set; cross-tenant case audit-pack denied; LocalTest-only paths explicitly isolated.
- Human-review ticket: publish attempts for bypass/branch/cycle/unreachable consequential graphs fail; canonical Release 1 graph still publishes.
- Version lineage: concurrent version uniqueness; historical run continues on pinned definition after a newer publish.
- Worker: duplicate delivery, stale version, malformed envelope, provider unavailable, dead-letter, and human-review-stop at the message/API boundary.
- Prefer extending existing API test hosts and LocalTest doubles over new frameworks.
- Full backend `dotnet test` (relevant projects) and portal hygiene/release-gate scripts must pass before closing a ticket; remote CI must be green on the PR.

## Out of Scope

- Partner Center or Managed Application republication.
- Production launch or insurer onboarding before staging and pilot gates pass.
- Releases 2–5, Fraud specialist modules, Underwriting deep modules, Billing, Marketplace, and other post-MVP surfaces.
- Visual redesign unrelated to blocker remediation.
- Disguising sponsored-subscription, PAYG, quota, legal, or operator-window gaps as completed evidence.

## Further Notes

- Map status and ticket files live under `docs/08-program-plan/wayfinder/release-foundation-blockers/`.
- Human-owned unknowns remain: sponsored subscription latency capability, GitHub protected-branch plan limits, Azure SQL migration operator/window, Document Intelligence deployment/region/capacity.
- After each closed ticket, append a one-line gist to the map’s **Decisions so far**.
- Next agent session: claim `tickets/enforce-server-resolved-tenant-authority.md`, implement on `harry/rf-enforce-server-tenant-authority`, keep validation evidence on the ticket, then open a PR.
