# IIP Merge Governance (RF-07)

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document | Merge Governance — go/no-go evidence |
| Status | In force (plan-limited compensating control) |
| Date | 2026-08-03 |
| Ticket | RF-07 / GitHub #16 |
| Authority | `AGENTS.md` §24–§25; `.github/workflows/ci.yml` |

## 1. Purpose

Prevent broken `main` from shipping silently. Merges into the default branch must require green backend, portal, and infrastructure CI plus human review before release-foundation work is treated as reachable from `main`.

## 2. Authoritative CI gate set

`.github/workflows/ci.yml` remains the authoritative gate set. Do not weaken, skip, or replace these jobs with softer substitutes.

| Required check context (job `name`) | What it proves |
|---|---|
| `Backend (build + test)` | `dotnet restore` / build / test for `InsuranceIntelligence.sln` |
| `Portal (lint + checks + build)` | Portal lint, `pnpm test` hygiene gates, production build |
| `Infra (bicep compile)` | Bicep compile of checked-in infra templates |

Observed on `main` (commit check-runs, 2026-08-03): all three contexts present and successful on recent CI runs (example workflow run `30849334243`).

Vulnerable package scan in the backend job remains **report-only** (`continue-on-error: true`) by design in `ci.yml`. That does not authorize merging on failed backend build/test, portal, or infra jobs.

## 3. Repository plan constraint (verified)

Repository: `OticGroupAz/agenticIip-v2` — **private**.

| Mechanism | API result (2026-08-03) | Interpretation |
|---|---|---|
| Classic branch protection on `main` | `GET/PUT .../branches/main/protection` → **HTTP 404** | No protection configured; private Free/org plan does not expose enforceable required-check branch protection for this repository. |
| Repository rulesets | `GET/POST .../rulesets` → **HTTP 403** with message: *Upgrade to GitHub Pro or make this repository public to enable this feature.* | Native rulesets (required status checks + pull-request reviews) are **plan-blocked**. |

**Honest conclusion:** native required checks and ruleset enforcement cannot be enabled on this private repository under the current GitHub plan. Claiming they are configured would be false. Do not invent green required-check UI.

## 4. Compensating merge process (in force)

Until GitHub Pro/Team (or equivalent org plan) enables branch protection/rulesets on this private repo—or the repo is deliberately made public—the following process is **mandatory** for any merge into `main`:

1. **PR required.** Do not push commits directly to `main`. Open a pull request from a `harry/*` (or agreed team) feature branch.
2. **CI green required.** All three authoritative check contexts above must be `success` on the PR head commit. Any failure or pending state → **no-go**.
3. **Human review required.** At least one maintainer approval. Security / tenant / identity / infra / release-gate changes may require senior maintainer review (`CONTRIBUTING.md` §16.3).
4. **No merge on red.** Maintainers and agents must not squash/merge when CI is red, cancelled, or incomplete. "Looks fine locally" is not a substitute.
5. **Evidence in the PR.** Use the Merge Governance Gate checklist in `.github/pull_request_template.md` and link the green CI run.
6. **Do not weaken CI.** Changes that remove or soften the three gate jobs are out of policy unless a maintainer updates this document and `AGENTS.md` in the same change with an explicit replacement gate.

### 4.1 Operator merge checklist (go / no-go)

| Check | Go | No-go |
|---|---|---|
| PR targets `main` | Yes | Direct push to `main` |
| `Backend (build + test)` | success | failure / cancelled / missing |
| `Portal (lint + checks + build)` | success | failure / cancelled / missing |
| `Infra (bicep compile)` | success | failure / cancelled / missing |
| Maintainer approval | >=1 approve | 0 approvals |
| CI workflow file | `.github/workflows/ci.yml` unchanged in a weakening way | Jobs removed, skipped, or made always-green |

## 5. Upgrade path (human-owned)

When the organization upgrades to a plan that supports private-repo branch protection or rulesets, a maintainer with admin rights should:

1. Create a ruleset (preferred) or classic branch protection on `main`.
2. Require status checks exactly:
   - `Backend (build + test)`
   - `Portal (lint + checks + build)`
   - `Infra (bicep compile)`
3. Require at least one approving pull-request review; dismiss stale reviews on new pushes.
4. Block direct pushes to `main`; include administrators if policy requires.
5. Open a proof PR that fails one check and confirm the merge button stays blocked; then close without merging.
6. Update §3–§4 of this document to **Native enforcement enabled** and record the ruleset/protection id and proof PR URL.

Until that upgrade completes, this compensating process remains the go/no-go control for RF-07.

## 6. Go / no-go evidence summary

| Item | Result |
|---|---|
| CI workflow authoritative | Yes — `.github/workflows/ci.yml` |
| Required check names confirmed live | Yes — three job names above |
| Native required checks configured | **No** — plan-limited (404/403) |
| Compensating process documented and in force | **Yes** — this doc + `CONTRIBUTING.md` §16.4 + PR template gate |
| Test of native "red CI blocks merge" | **Not runnable** on current plan; deferred to upgrade path §5 |

```text
RF-07 MERGE GOVERNANCE: COMPENSATING CONTROL IN FORCE
NATIVE REQUIRED CHECKS: BLOCKED BY GITHUB PLAN (PRIVATE REPO)
CI WEAKENED: NO
```
