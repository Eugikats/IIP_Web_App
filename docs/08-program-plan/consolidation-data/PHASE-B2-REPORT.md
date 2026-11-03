# Phase B2 report: v2 social channels and related work

Date: 2026-11-03. Status: PHASE GATE PASSED; GLOBAL GATE IN PROGRESS. `main` remains at `80155dc` while the remaining consolidation phases and section 8 global gate are unfinished.

## Source and branch

- Repository: `Otic-Group/IIP_Web_App`.
- Execution clone: `C:/work/iip-consolidation-verified`.
- Phase branch: `integ/02-v2-social`, stacked on `integ/01-saas-academy` as recorded in D-015.
- B2 snapshot: `44d6810` (`wip(v2): social channels, Inbox, channel identity, follow-ups, notification routing, languages, extraction fixes, UI changes`).
- Follow-on docs: `2d9a345`.
- Academy authoring material: `14bbf54`.
- Synthetic claim packs and reference images: `ebe5ec7`.
- Reconciled commits on this branch: `526f76a`, `ea30508`, `befaa35`, `f51139e`, `2f5ec6e`.

## Changes and conflict resolution

The v2 snapshot adds shared channel identity and timeline data, customer follow-ups, outbound message senders and webhooks, notification routing, language settings, extraction mapping, and related portal surfaces. It also includes the planned API, unit, and visual coverage for those features. The remaining social platform gap is TikTok inbound; it is recorded in `BACKLOG.md`. Provider setup, external API credentials/approvals, and live inbound/outbound delivery have not been verified in a tenant environment.

The snapshot had 15 conflict paths. They were resolved against the live supported contract: preserve runtime route guards; keep the tested document classifier; preserve WhatsApp tenant routing, authentication, rate limits, durable receipts and review/autopilot gates; union customer assistant dependencies and permission maps; retain the live MFA and organization switch behavior; and combine current database model configuration with channel types. The UI and database decisions are recorded in D-016 and D-017. The five v2 migrations and model snapshot were excluded; they are not represented as applied or production-ready migrations.

Three remaining planned commits were cherry-picked: the v2 documentation and social plan, Academy authoring documents, and synthetic reference fixtures. The 69 MB class of synthetic fixtures was retained under `prototypes/` and `tests/fixtures/` as the plan directs. No production customer data was added.

## Verification

All observed build, test, and commit commands were preceded by `scripts/guard/check-config-injection.sh`; every guard run exited 0.

| Check | Result |
|---|---|
| Backend Release build | Passed, 0 warnings and 0 errors |
| Backend full suite, including B2 tests | 1,833 passed, 0 failed |
| Portal lint | Passed with no errors; 7 existing `<img>` optimization advisories and Next lint deprecation notice |
| Portal `pnpm test` | Passed: runtime hygiene, provider status, role routes, action permission parity, UI release gates, trust/truth 3/3, authority/permissions 4/4 |
| Portal production build | Passed; 77 static pages generated. Existing 7 image advisories and stale Browserslist data warning remain. |
| Full visual matrix | 155 passed, 49 skipped, 0 failed across desktop, tablet, and mobile; 18.5 minutes |
| B2 focused visual rerun | 21 passed, 3 deployment-only skips, 0 failed |

Visual findings were corrected or aligned to the current authority before the final matrix: `/app` now renders the Brief and the Assistant is at `/app/assistant`; malformed connector responses render the safe error state; the claims table's action header is accessible; tests use the current conversation and claims controls; and a tenant workspace is verified not to enter platform-level tenant onboarding. `DESIGN.md` and B2 decisions record these changes.

## Scope limits and remaining work

- TikTok inbound message handling remains missing and is a tracked feature gap. X activity webhook handling and send capability are present in source, but external account approval and live provider delivery were not exercised.
- The onboarding visual gate verified tenant-level denial and redirect. It did not submit a new insurer because the local workspace identity is a tenant administrator, not a platform onboarding operator.
- The imported model changes have no corresponding migrations on this branch. Do not apply a database change from this report.
- B2's phase gate is green. Section 8's global gate remains open for the remaining phases and global deployment/worker checks. No deployed-profile assertion is claimed as passed.

## Main and release status

- `main` and `origin/main` remain at `80155dc`.
- Nothing was deployed, and no cloud resource or database was changed.
- B2 can be reviewed on its phase branch; only the final combined branch may advance `main` after the full global gate passes.

## Next work

Proceed to B3 worker/API compatibility evidence, then Phase C claims-core and Phase D managed-app. Continue to keep `main` frozen until every section 8 gate is green. TikTok inbound remains separate tracked build work and must not be reported as complete.
