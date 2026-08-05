# Contributing to Insurance Intelligence Platform (IIP)

Thank you for contributing to IIP. This guide covers the human contribution workflow. Repository invariants and coding-agent behavior live in [`AGENTS.md`](AGENTS.md); detailed safety checks live in [`IIP_IMPLEMENTATION_GUARDRAILS.md`](IIP_IMPLEMENTATION_GUARDRAILS.md).

## Before starting

1. Read [`AGENTS.md`](AGENTS.md).
2. Read the current task/spec and the owning product, architecture, UI, security, data or deployment document.
3. Inspect `git status` and preserve unrelated work.
4. Confirm the requested outcome, affected module, migration needs and verification path.
5. Check [`IIP-CONSOLIDATED-ROADMAP.md`](docs/08-program-plan/IIP-CONSOLIDATED-ROADMAP.md) only when sequencing or programme scope matters.

Do not use an older implementation or document as an automatic fallback. If sources conflict, resolve the authority conflict before coding and update the stale source with the change.

## Branches and commits

- Branch from the maintainer-approved base. New branch names use `harry/<short-description>` unless instructed otherwise.
- Keep commits focused and concise. Prefer Conventional Commit types when useful: `feat`, `fix`, `refactor`, `docs`, `test`, `security`, `ci`, `chore`.
- Do not mix unrelated cleanup, generated artifacts, local packages, secrets or user-owned working-tree changes into a commit.
- Do not rewrite shared history or push directly to `main`.

Examples:

```text
feat(claims): add reviewed intake path
fix(auth): reject unmapped workspace
refactor(portal): remove legacy route fallback
```

## Implementing changes

- Work through the owning Domain/Application module; keep frameworks, transport, EF and provider SDK details in adapters.
- Preserve tenant/workspace enforcement, permissions, audit, evidence grounding, privacy and meaningful human review.
- Build the smallest coherent forward slice. Update callers, contracts, migrations, UI, tests, telemetry and docs needed for that outcome.
- Do not repair a new UI, API, workflow, provider, release or delivery path by silently invoking the old one.
- Characterize unclear legacy behavior before changing it, but do not preserve accidental behavior without an active requirement.
- Any temporary compatibility path needs an owner, tests, telemetry, expiry/removal condition and tracked removal work.
- Keep schemas, routes, messages, events, statuses and configuration versioned through the supported migration window.
- Add dependencies or infrastructure only for a concrete requirement. Document material architectural trade-offs in an ADR.
- Do not alter or suppress tests, startup validation, security controls or evidence gates merely to obtain a green result.

## Testing

Run targeted checks during development. Before requesting review, run the full affected-area checks from [`AGENTS.md`](AGENTS.md) and [`.github/workflows/ci.yml`](.github/workflows/ci.yml) when feasible.

At minimum, changed behavior should be tested for the relevant cases:

- success and invalid input;
- authentication, permission and tenant/workspace isolation;
- human-decision boundaries and audit writing;
- provider absence/unavailability and honest `Not configured` behavior;
- retries, duplicates, concurrency, reconciliation or migration;
- frontend loading, empty, error, unauthorized, stale/conflict and accessibility states.

If a check cannot run, record the command, reason and remaining risk. Never report an unrun check as passed.

## Documentation and ADRs

Update the owning document when a contract, workflow, data model, route, UI behavior, runtime profile, provider, deployment process or operational procedure changes.

Use an ADR for a material decision that changes boundaries, dependencies, source of truth, security posture, human authority, deployment model or migration strategy. Mark superseded guidance explicitly; do not leave two active authorities.

Do not copy volatile inventories into `AGENTS.md` or this guide. Route catalogs, agent/provider definitions, UI tokens, delivery status and release evidence belong in code or their owning documents.

## Pull requests

Keep the PR description concise and evidence-based. Include:

- outcome and scope;
- files/modules/contracts affected;
- legacy path removed, retained or time-bounded;
- security, tenant, data/migration, AI/human-review, UI and operational impact;
- tests and commands with actual results;
- screenshots or recordings for material UI changes;
- manual cloud, identity, mailbox, migration or marketplace actions;
- blockers, known limitations and removal follow-ups.

Before review:

- [ ] Supported path works end to end for the requested scope.
- [ ] No secret, real customer evidence or unrelated file is included.
- [ ] No mock/simulation/legacy fallback can activate in a protected runtime.
- [ ] Authorization, tenant scope, audit and human gates are enforced server-side.
- [ ] Tests and docs match the new behavior.
- [ ] Superseded paths are removed or have explicit compatibility expiry.
- [ ] Readiness claims match available evidence.

## Review and merge

- Follow [`iip_merge_governance.md`](docs/05-deployment-release-enablement/iip_merge_governance.md) for current required checks and approvals.
- Address review findings at their root cause. Do not resolve a security or design concern by adding another hidden fallback.
- Security-, identity-, tenant-, data-, infrastructure- and consequential-workflow changes may require specialist review.
- Do not merge, deploy, apply production migrations or publish artifacts without the required human authorization.

## Reporting readiness

Use precise states: **source-complete**, **locally verified**, **deployed**, **staging-validated**, **production-candidate**, **Managed Application-ready**, or **Partner Center-ready**. State only what the evidence proves.
