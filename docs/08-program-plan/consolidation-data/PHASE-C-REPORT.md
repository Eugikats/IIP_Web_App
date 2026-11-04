# Phase C report: claims-core consolidation

Date: 2026-11-04
Branch: `integ/03-claims-core`

## Outcome

Items 1-32 were ported in the preceding commits on this branch. Items 34-37 are implemented in the current change set. Item 33's duplicate `/learn` learner application was omitted because the live SaaS Academy is the source of truth; the capability comparison and the missing insurer-sponsored access-code follow-up are recorded in `DECISIONS.md` D-032 and `BACKLOG.md`.

Claims-core source commits represented by this phase include `d3935445` (item 34), `e55cac03` (item 35), `6dd8d886` (item 36), and `ae4ea6fc` (item 37). The item 33 source commit `460198c5` was reviewed and not copied. Microsoft sign-in callback completion from `5b1bac42` was ported; the origin-host redirect change from `6775941b` was not adopted because it would replace the configured Entra redirect authority with the current request origin.

## Conflict resolutions

- Academy: retained the existing `/academy` identity, enrollment, billing, assessment, marking, certificate and admin paths. Removed the parallel learner store and `/learn` routes. One-time insurer-sponsored access codes remain a tracked follow-up.
- Runtime access: removed the obsolete public `/api/v1/learn` identity exemption and added it to the authorization regression test. Restored narrow unauthenticated access for the live Academy public course/certificate routes and exact learner session route, while retaining authentication for the rest of the Academy API.
- Data lifecycle and lookup: added tenant-scoped retention, legal hold, erasure records and indexed case/customer/policy lookups. Existing persistence and domain boundaries remain in place.
- UI: manually reconciled the assistant bar, claims assessment card, copilot panel and sidebar with the current design authority. Preserved keyboard focus restoration, tablet rail behavior and visible workflow labels. Updated visual expectations to match the reconciled screens.
- Login and claimant portal compatibility: retained configured Entra authority while completing MSAL callback handling; added display-only organization recovery state and claimant scope routing hints without treating browser values as authorization. Added required-document counts to the API response and matched portal completion to the canonical uploaded state.
- Migration: restored the live model snapshot before generating a new additive migration. No claims-core or v2 migration designer/snapshot was copied.

## Verification

| Check | Result |
|---|---|
| Config injection guard | Clean before checks; rerun before commit |
| Release solution build | Passed, 0 warnings and 0 errors |
| Portal production build | Passed, 89/89 pages generated |
| Portal lint | Passed; existing `next lint` deprecation and `<img>` optimization warnings |
| Portal `pnpm test` | Passed; hygiene, provider status, role routes, permission parity (9 roles / 31 permissions), UI release gates, trust (3/3), authority (4/4), scan math (21/21) |
| Domain lifecycle tests | Passed, 13/13 |
| API lifecycle, indexed lookup and client portal tests | Passed, 71/71 |
| Item 34/36/37 browser journey | Passed, 12/12; included 390, 768 and 1280 pixel layouts, no-permission state, legal hold/erasure and two-company isolation |
| Migration script from assumed live migration `20261027073337_AddWorkspaceRecords` | Additive only: 23 tables, 24 columns and 66 indexes; no drops, renames or column alterations |

The production database was not inspected or changed. `live_db_history.json` was unavailable, so the plan's assumed 42-migration live history remains unverified by the database owner. The generated migration was not applied.

## Deviation and remaining work

To reduce turnaround, items 34-37 are being reviewed and committed as one coherent slice rather than four individual commits. Previously passed builds and browser journeys were not repeated. The full backend suite, complete visual suite, runtime-recovery suite and deployed-DOM suite were not repeated in this closing pass. No push, fast-forward of `main`, deployment or database operation is included in this phase report.

External or separately tracked work remains in `BACKLOG.md`, including insurer-sponsored Academy codes, TikTok inbound messages, real-provider validation, production migration-history confirmation, and the live worker/API redeployment decision.
