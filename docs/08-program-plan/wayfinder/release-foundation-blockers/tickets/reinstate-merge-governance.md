# Reinstate Merge Governance

**Label:** `wayfinder:task`  
**Status:** Resolved  
**Claim:** harry (branch `harry/rf-reinstate-merge-governance`, GitHub #16)  
**Blocked by:** None

## Question

Given the current private-repository plan and unprotected `main`, what enforceable merge mechanism will require green backend, portal, and infrastructure checks plus human review before release-foundation changes become reachable from `main`?

## Resolution criteria

- [x] Repository-plan constraint verified.
- [x] Native branch protection/ruleset enabled, or an explicit compensating merge process implemented and documented.
- [x] Required CI checks are blocking rather than report-only where risk demands it.
- [x] Test PR proves failed checks and missing review prevent merge — *native proof deferred: plan blocks required checks; compensating process records the honest alternative.*

## Decision

**Plan-limited compensating merge governance** is in force for `OticGroupAz/agenticIip-v2` (private).

- Native rulesets return HTTP 403 (*Upgrade to GitHub Pro or make this repository public*). Classic branch protection GET/PUT on `main` returns HTTP 404 with no enforceable required checks.
- Authoritative gate set remains `.github/workflows/ci.yml` with required contexts: `Backend (build + test)`, `Portal (lint + checks + build)`, `Infra (bicep compile)`.
- Until Pro/Team (or public repo) enables native required checks, merges to `main` must follow the mandatory process in `docs/05-deployment-release-enablement/iip_merge_governance.md`: PR-only, all three CI jobs green, >=1 maintainer approval, no merge on red, no CI weakening.
- Process is wired into `CONTRIBUTING.md` §16.4 and `.github/pull_request_template.md` (Merge Governance Gate).
- Human-owned upgrade: enable ruleset/protection with the three contexts, then run a failing-check proof PR and update the governance doc.

## Working notes

- Claimed 2026-08-03 on `harry/rf-reinstate-merge-governance` from `origin/main`.
- Evidence capture via `gh api` branch protection + rulesets; live check-run names from `main`.
- Did not weaken `ci.yml`. Did not close parent #11.
