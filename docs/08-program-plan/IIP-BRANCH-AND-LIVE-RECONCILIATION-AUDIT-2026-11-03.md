# IIP branch and live deployment reconciliation audit

Audit date: 3 November 2026, Africa/Nairobi (EAT). Scope: all six IIP-named checkouts under `C:/Users/Administrator/Documents/Projects/Otic/Projects`, their registered worktrees and local branches, current remote heads for the three GitHub repositories, and read-only Azure deployment/artifact inspection. No application code was changed, tested, merged, pushed or deployed during this audit. Local Git objects were fetched for comparison; remote-tracking metadata was refreshed where Git's fetch mapping applied. Working files were preserved.

## Finding

Work is distributed across three Git object stores, six checkouts, and three GitHub repositories. The live API is built with revision `4818bb11c125f723f361c89c9100e6b1f839cb76` from the `iip_saas_insurer_demo` checkout. The live portal's build ID and compiled login page also match that checkout's retained build artifacts. Neither local v2 `main`, remote v2 `main`, nor `build/claims-core` represents all current live and requested work.

The earlier claim that the additional social adapters were absent came from examining `iip-build` alone. They exist as uncommitted changes in `agenticIip-v2`. Findings and completion claims must name the checkout, branch, revision and working-tree status from now on.

## Checkout inventory

Counts below are individual files reported by `git status --porcelain --untracked-files=all`, before adding this report. Untracked files include fixtures, authoring material and diagnostic assets; they are not all application code.

| Folder | Branch and committed HEAD | Modified tracked files | Untracked files | Work to preserve |
|---|---|---:|---:|---|
| `agenticIip-v2` | `feature/bancassurance-acquisition`, `cc8240e` | 114 | 485 | Social adapters/Inbox, identity links and timeline, WhatsApp review mode and requested-follow-up queue, languages, notification routing, policy connector improvements, document extraction fixes, fraud settings, UI changes, plans and Academy authoring material. |
| `iip-build` | `build/claims-core`, `951a3d9` | 1 | 6 | Committed claims/process/document build, policy wording draft workflow, export packs, background processing, billing, signup/invitations, customer memory, multitenant communications, retention and a separate Academy implementation. Uncommitted work is the recent research/prompt/use-case documentation. |
| `iip_saas` | `saas/merge-v2-2026-10-08`, `dff5ab3` | 22 | 10 | SaaS and Academy baseline; uncommitted Academy media, lesson/assessment lifecycle, pricing/payment and related browser/test changes. Its authoring inputs live separately in `agenticIip-v2/docs/iip-academy`. |
| `iip_saas_insurer_demo` | `build/insurer-demo-2026-10-27`, `4818bb1` | 2 | 0 | Current live application lineage: company onboarding, bank events/outreach rules and templates, managed WhatsApp setup, claim identity check, wording review, claim UI and membership/login fixes. Dirty files are `docs/iip-academy/OWNER_SETUP_GOOGLE_MICROSOFT.md` and generated `web/portal/next-env.d.ts`. |
| `iip_saas_release` | `release/2026-10-20-contacts-and-banners`, `d550f0c` | 0 | 0 | Clean historical release snapshot, already an ancestor of the SaaS and demo branches. |
| `agenticIip` | `main`, `90078f3` | 0 | 0 | Older separate repository. Its local main has no common Git ancestor with v2 main; do not attempt an automatic history merge. Its current remote main differs from its local main. |

`agenticIip-v2` and `iip-build` share one Git object store. `iip_saas`, `iip_saas_insurer_demo` and `iip_saas_release` share another. Branches are shared within each group; uncommitted working files are not. `agenticIip` has its own object store. The Codex project inventory points the active v2 project to `agenticIip-v2`, not to either sibling worktree.

Of v2's 485 untracked files, 210 are synthetic PDF-pack fixtures and 145 are Academy authoring files. Preserve the full inventory, but review file type, provenance and size before staging; do not bulk-add generated or sensitive artifacts.

## GitHub repositories and the meaning of main

Live `git ls-remote` reads established these remote heads:

| Repository | Current relevant remote state | Implication |
|---|---|---|
| `OticGroupAz/agenticIip-v2` (`origin` in v2) | Default `main` is `c7ffa30`; local v2 main is `d5e9bed` | They are one commit apart on each side and differ in `.gitignore` and `web/portal/postcss.config.js`. A same-looking commit subject does not establish identical code. |
| `Otic-Group/IIP_Web_App` (`org` in v2) | Default and only advertised branch is `build/claims-core`, at `f901ab2` | There is currently no remote `main`. Local `build/claims-core` is three documentation commits ahead. The earlier migration instruction explicitly pushed only this branch. |
| `OticGroupAz/agenticIip` (older repository) | Remote `main` is `83d35f9`; local main is `90078f3` | The refreshed remote tracking ref reports a forced update. Those tips differ in `.gitignore` and PostCSS configuration. This is not the current live application's source repository. |

The SaaS clone's cached `origin` refs and default-branch pointer were stale. Its remote URL still points to `OticGroupAz/agenticIip-v2`; the live remote inspection above takes precedence over its cached pointers. Its `v2-local` remote points to the sibling v2 folder.

The destination for consolidation must explicitly identify both repository and branch. The prior organization-migration work points toward `Otic-Group/IIP_Web_App`, but creating its canonical `main` and retiring the other release path have not been completed or performed by this audit.

## Local branches and historical work

These counts compare committed ancestry with the clean local v2 main `d5e9bed`. They exclude dirty files and are not counts of unique features; some work was cherry-picked or implemented independently.

| Branch | Committed relation | Treatment |
|---|---|---|
| `feature/bancassurance-acquisition` | 36 commits after local main | Preserve committed changes and its separate dirty working tree. |
| `build/claims-core` | 79 commits after local main | Reconcile its claims build with the live SaaS/demo model. |
| `saas/merge-v2-2026-10-08` | 122 commits after local main | Already an ancestor of the demo branch; preserve the additional dirty Academy changes separately. |
| `build/insurer-demo-2026-10-27` | 153 commits after local main; 31 after SaaS head | Use the verified live behavior/artifacts as the preservation baseline. Review demo-only behavior against current runtime invariants. |
| `release/2026-10-20-contacts-and-banners` | One commit behind SaaS head | Its committed changes are already included in demo ancestry. |
| `saas/foundation` | Ancestor of SaaS head | Its committed changes are already included in demo ancestry. |
| `feature/claim-intake` in v2 | One separate documentation commit, `d347da9` | Preserve `IIP_INSURER_READINESS_GAPS.md`; this is not another unfinished application implementation. |
| `feature/claim-intake` in SaaS clone | Points to `d5e9bed` | Same revision as clean local v2 main. |
| `feature/local-language-replies` | Ancestor of local v2 main | Already included locally. |
| `feature/phase0-1-connections-channels` | Ancestor of local v2 main | Already included locally. |
| `fix/whatsapp-demo-readiness` | Ancestor of local v2 main | Already included locally. |
| `harry/ui-design-language-foundation` | Ancestor of local v2 main | Already included locally. |
| `harry/ui-design-language-screens` | Ancestor of local v2 main | Already included locally. |

Remote-only `origin/gate-0-completion` and `origin/harry/rf-reinstate-merge-governance` are ancestors of remote main. The remote `copilot/fix-backend-build-test-job` tip changes `branch_structure.json` and PostCSS configuration relative to its base; its name is not evidence of a useful backend fix. Several remote feature tips have the same subjects as their local counterparts but different hashes and the same unexpected PostCSS blob. Preserve clean local history and review the remote deltas explicitly.

## Live deployment evidence

Both custom domains map to the existing `rg-iip-dev` App Services in subscription `5f5765ae-985f-42e4-a443-8e2447be33ca`. Times below are EAT, UTC+03:00.

| Component | Current evidence | Source attribution |
|---|---|---|
| Portal: `app.insuranceintelligenceplatform.com` | App `app-portal-dev-h5girbnzrayqe`; active deployment `19c34bd3-6766-4f53-bdac-8d8f14f7f50e`, completed 2 Nov 2026 at 07:19:23 EAT; build ID `K92_UUxWM6BJoOBinGJnJ` appears in live login HTML and Kudu `BUILD_ID` | Matches `iip_saas_insurer_demo/web/portal/.next/BUILD_ID`. Live compiled login page SHA-256 also matches that worktree's compiled page. |
| API: `api.insuranceintelligenceplatform.com` | App `app-api-dev-h5girbnzrayqe`; active deployment `f72d5174-0364-485f-a032-b97de8d5bce6`, completed 2 Nov 2026 at 07:32:31 EAT; active run-from-package file `20261102043206.zip` | API, Application, Domain and Infrastructure assemblies each embed revision `4818bb11c125f723f361c89c9100e6b1f839cb76`; each SHA-256 matches the retained demo-worktree build. |
| Functions worker | App `func-iip-dev-h5girbnzrayqe`; active deployment `2536f867-592b-44e0-bda6-ad8faa89e56a`, completed 14 Sep 2026 at 13:54:15 EAT; package `20260914105347.zip` | Functions, Application and Infrastructure assemblies embed `1872a14560c3250251daa0f68531412b148bfe7c`, an ancestor of clean local v2 main. Worker/API compatibility needs explicit reconciliation. |

The portal login and API health returned HTTP 200. Health reports application environment `Demo`, host environment `Production`, and `Entra` auth. This is runtime evidence, not a validation of all capabilities or current approved release profiles. No customer records were read or changed.

The API uses `WEBSITE_RUN_FROM_PACKAGE=1`. Its ordinary Kudu `site/wwwroot` contains stale July assemblies, so that directory alone is misleading. Attribution above uses `data/SitePackages/packagename.txt` and the active ZIP, not the stale DLL. This distinction must be retained in future deployment audits.

Neither portal nor API has a Git repository/branch configured in App Service deployment source. Azure's deployment entries say `OneDeploy` and do not themselves identify a Git commit. The checked-in `deploy-staging.yml` is manual and guarded to fail pending resource confirmation. Merging to main currently does not establish that main was deployed.

Artifact fingerprints retained for reproducibility:

| Artifact | SHA-256 |
|---|---|
| Active API ZIP | `46be5a1023e3caa59927fe2d0aa2de1813817374cc9f2221ca940b7780ed70eb` |
| Active API assembly | `55bf22047da411a083c23939f309f4c7860a3eee32df7c88a87d06c6d0177840` |
| Live portal compiled login page | `ed56e8610ebb2cf9d26094885795b361aae340d63706c501a15a67c7661da0a0` |
| Active Functions ZIP | `4635f1af8c933562317aa568891bb6cde0554955c744f6f2ebdec77e0176c07d` |

## Reconciliation blockers and overlaps

**Remote source integrity:** remote v2 main's PostCSS file contains 12,970 characters, including a 12,892-character line, versus 80 characters in clean local branches. The unexpected blob is `52e696b4e36749d37172e74ffd3fedb6f653b704`; several remote feature tips share it. `build/claims-core` contains cleanup commit `a6222f6` titled `fix(security): remove obfuscated PostCSS payload`, and its current organization-remote tip is clean. The old repository's remote main also differs from local in PostCSS and ignore entries. These deltas require investigation before any remote snapshot is built or imported. This audit did not execute or decode the obfuscated payload, attribute its origin, or establish whether it executed elsewhere.

**Overlapping implementation:** claims-core and the live demo branch have 52 and 126 commits unique to their respective sides after common base `b211051`. They both changed 81 file paths. V2's dirty tracked changes overlap 50 paths changed by claims-core and 37 changed by demo. These are overlap counts, not a simulated merge-conflict count. Key shared areas include API routes, dependency injection, authentication, WhatsApp handling, document processing, domain permissions, database context/model snapshot, claims UI and tests.

**Duplicate product designs:** claims-core has `WordingDraftService`/`WordingExtraction`, while demo has `ProductWordingExtractionService` and a separate product review flow. Both have claims, Academy, signup/billing, channel and customer-assistant changes. Resolve the desired contracts and source of truth per module rather than copying complete folders or picking a side automatically.

**Previously deployed Academy work:** the October Academy runbook identifies the dirty SaaS checkout as the source of a prior media release, and states those changes remained uncommitted. The current demo source does not contain its `AcademyMediaEndpoints.cs` or the same `MediaUnitId`/paid-release guard additions. This is a concrete reconciliation item and a possible regression to investigate; current authenticated media behavior was not tested in this audit. Keep v2 authoring files and SaaS runtime changes together when assessing Academy completeness.

**Database and release state:** each branch has its own schema additions and EF model snapshot. Active database migration history was not queried here. Before consolidation is deployed, compare the actual migration history with the combined schema and the independently deployed Functions worker. A generated migration or successful local test is not proof it is applied live.

**Claims of completion:** `iip-build/docs/build-loop/BUILD_PLAN.md`, the v2 social plan, and `iip_saas_insurer_demo/docs/insurer-demo/BUILD_LEDGER.md` describe different scopes and revisions. Their DONE labels cannot be added together as proof of one finished application.

## Reconciliation order toward one main

1. Preserve the four sets of current work: v2 social/integration/UI changes, committed claims-core plus its research documents, live demo/SaaS committed history, and dirty SaaS Academy changes. Create reviewable source snapshots before moving branches; retain generated media and deployment artifacts through explicit manifests without committing secrets or customer evidence.
2. Set the canonical GitHub repository and main branch. If using the organization destination from the previous migration, it still needs a main branch and a deliberate transition from the original repository. Resolve the unexpected remote PostCSS deltas before building imported remote source.
3. Preserve the live API/portal behavior using the verified demo revision/build, and record the Functions package and live schema/configuration baseline. Do not assume a branch name, a deploy success message, or a stale Kudu directory proves the running revision.
4. Reconcile claims-core with that baseline in an integration branch, then apply the v2 social and supporting changes in coherent slices. Resolve shared product wording, auth, tenancy, customer identity, WhatsApp, claims, billing and Academy models explicitly. Reconcile migrations as one ordered history and model snapshot.
5. Reconcile the dirty SaaS Academy media/pricing changes and the v2 authoring pipeline. Preserve the single separate readiness document from v2 `feature/claim-intake` and the recent research/prompt documents currently in `iip-build`.
6. Run the detailed remaining-feature audit against this combined, named snapshot. Existing evidence already identifies TikTok inbound as missing; shared social provider proof/media/delivery work, review/Autopilot dispatch, external claim/document sync and other plan gates need assessment against the merged implementation, including the live demo branch's separate outreach system.
7. Run required backend, portal, infrastructure and affected browser checks on that exact revision. Use a PR to the chosen main with the current merge-governance checks and maintainer review. Record commit and artifact hashes for API, portal and worker releases, plus schema version, so future live-to-main comparisons are direct.

This audit establishes the source map and live provenance. It does not authorize deployment or certify that either branch's implementation satisfies every build-plan acceptance criterion. No branch or working changes were merged, deleted, stashed or committed.
