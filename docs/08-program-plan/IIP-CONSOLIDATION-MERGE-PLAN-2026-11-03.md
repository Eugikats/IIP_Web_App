# IIP consolidation merge plan

Written 2026-11-03 for the executing coding agent (Codex), to be reviewed afterwards by Claude and the owner. It turns the findings of `IIP-BRANCH-AND-LIVE-RECONCILIATION-AUDIT-2026-11-03.md` into ordered, mechanical steps. Read section 1 first: nothing else may start until it is cleared.

Every hash, count and path below was read from the repositories on 2026-11-03. Where a step needs a judgement call, a default rule is given. When a default does not cover the case, record the decision in `docs/08-program-plan/consolidation-data/DECISIONS.md` (what, why, alternative) and carry on; Claude reviews that file after each phase.

## 0. Where things stand

All pending work was committed to snapshot branches and pushed to `https://github.com/Otic-Group/IIP_Web_App.git` (remote name `org`). Offline bundles of both object stores are in `C:\Users\Administrator\Documents\Projects\Otic\repo-backups-2026-11-03\`. No `main` exists on `org` yet. Nothing has been merged or deployed.

| Ref on `org` | Commit | What it is |
|---|---|---|
| `live/insurer-demo-2026-10-27-4818bb1` | `4818bb1` | Source of the live API and portal (verified by assembly hashes and portal build id). **This is the baseline.** |
| `saas/merge-v2-2026-10-08` | `dff5ab3` | SaaS history; already an ancestor of the live branch. |
| `wip/saas-academy-media-pricing-2026-11-03` | `76733b6` | Uncommitted SaaS Academy work (media endpoints, lesson media, assessments, pricing, payments). One commit on top of `dff5ab3`. |
| `wip/demo-owner-setup-doc-2026-11-03` | `8e1024f` | One documentation file. |
| `release/2026-10-20-contacts-and-banners` | `d550f0c` | Old release, already in the live branch. Keep, do not integrate. |
| `build/claims-core` | `3957485` | Claims build: 37 `feat(item-N)` commits plus docs, 52 commits after `e8b2b42`. |
| `wip/v2-social-channels-2026-11-03` | `ebe5ec7` | v2 social channels and related work: 4 snapshot commits on top of `cc8240e` (`feature/bancassurance-acquisition`). |
| `feature/bancassurance-acquisition` | `cc8240e` | Committed part of the v2 line. |
| `base/v2-main-d5e9bed` | `d5e9bed` | Old v2 `main`, for reference. |
| `archive/v2-feature-claim-intake-readiness-doc` | `d347da9` | One document, `IIP_INSURER_READINESS_GAPS.md`. |
| `plan/consolidation-2026-11-03` | this plan | Plan, guard script and appendix data. |

Merge bases: live branch and claims-core meet at `b211051`; live branch and the v2 line meet at `b211051`; claims-core and the v2 line meet at `e8b2b42`.

Already equivalent in the live branch (skip when porting): `e8b2b42` (sign-in says why it failed) and `cc8240e` (deploy refuses demo banner).

## 1. STOP GATE: the repositories were hit by a hidden-code injection

This must be cleared before any install, build, test or dev server runs on any machine that touched these repositories.

**What was found (read from Git history, nothing was executed or decoded):**

- `web/portal/postcss.config.js` on `OticGroupAz/agenticIip-v2` `main` is 12,971 bytes instead of 81. After the normal four-line config there is one line of about 12,900 characters: a long run of tabs followed by obfuscated code ending in an `eval`-style call. PostCSS loads this file on every portal build, dev server and test run, so the code runs on whoever builds.
- It arrived on `main` in commit `c7ffa30` (2026-10-03), whose subject is `docs(deploy)`. A docs commit has no reason to touch a build config. The author field is forgeable and proves nothing about who did it.
- The same injection (a different obfuscated blob, so it is polymorphic) entered `build/claims-core` in commit `a792fda1` on 2026-10-12 16:53, a commit made from this machine. The file was already modified in the working tree before that commit was staged, so something on the machine wrote it. It was removed in `a6222f6` on 2026-11-02.
- The same commits added `branch_structure.json`, `temp_auto_push.bat` and `temp_interactive_push.bat` to `.gitignore`. Those are helper files that script automatic pushing to every branch, hidden from `git status`. The branch list in `branch_structure.json` on the remote `copilot/fix-backend-build-test-job` branch is consistent with that.
- Not found: the helper files themselves are not on disk in the project, Desktop, Downloads, Temp or `.claude` folders now. The live-deployed lineage (`4818bb1`) and every local branch are clean (81-byte file, no long lines in any config).

**What that implies:** treat the Windows machine that these repos live on as possibly compromised, and treat anything those builds could read as exposed. Whether anything ran or leaked cannot be determined from Git.

**Required before Codex runs anything (owner actions):**

1. Run the work on a clean machine, a fresh VM, a dev container or a GitHub Codespace, not on the current machine. If that is impossible, do the steps below first.
2. Run a full antivirus and offline scan on the current machine; look for `temp_auto_push.bat`, scheduled tasks, startup entries and Node processes started from project folders.
3. Rotate every secret the machine could read: GitHub tokens and SSH keys, the Azure CLI/PowerShell sessions (sign out, revoke, sign in again), any service principal secrets, `.env` and `appsettings*.json` values (Azure OpenAI, Document Intelligence, Meta/WhatsApp, email and SMS keys, payment sandbox keys), browser-saved passwords, and the Microsoft Entra accounts used on this machine. Check Azure sign-in logs and GitHub security log for unfamiliar sessions or pushes from 2026-10-03 onwards.
4. Review who can push to `OticGroupAz/agenticIip-v2`, `OticGroupAz/agenticIip` and `Otic-Group/IIP_Web_App`; enable branch protection on `main` once it exists.

**Rules for the agent, all phases:**

- Run `bash scripts/guard/check-config-injection.sh .` before every install, build, test run and commit; stop on any non-zero exit. It flags config files with lines over 400 characters or hidden whitespace runs, any source file with a line over 3,000 characters (except the one known data file), and the three helper file names in the tree or in `.gitignore`. It was tested against the poisoned file and reports all six symptoms.
- Never run `git add -A`, `git add .` or `git commit -a`. Stage explicit paths only. The 2026-10-12 injection was committed by a blanket add.
- Never check out, install, build or execute `origin/main` of `OticGroupAz/agenticIip-v2`, any `copilot/*` branch, or anything from `OticGroupAz/agenticIip`. Read them with `git show` and `git diff` only.
- After a cherry-pick or merge, run the guard again before continuing.
- If the guard fires: stop, do not delete anything, report the file and the commit.

## 2. Operating rules

1. Work in a fresh clone: `git clone https://github.com/Otic-Group/IIP_Web_App.git C:\work\iip-consolidation` (or the VM equivalent), then `git fetch --all`. Do not work inside the existing checkouts under `...\Projects\Otic\Projects\`.
2. Never force-push. Never delete a branch or tag that exists today. Never rewrite history of a pushed branch.
3. Never deploy. Never change anything in `rg-iip-dev` or any Azure resource. Reading the live migration history through the authenticated read-only endpoint in section 6 is the only live access allowed, and only the owner's token may be used for it.
4. Direct pushes are allowed. No pull request is needed unless a push is refused by branch protection. `main` advances only by fast-forward from a phase branch whose gate (section 8) passed.
5. One logical change per commit. Commit messages: no em dashes, no emoji, end with `Co-Authored-By: <the agent> <noreply@...>` as the tool sets it.
6. Never delete, skip or weaken a test. If a test must change, the reason goes in `DECISIONS.md`. When both sides changed the same test file, keep both sides' tests.
7. No em dashes and no emoji in code, comments, UI copy, docs or commits. In C# test assertions write an em dash as `(char)0x2014`.
8. Tenant isolation, human-decision boundaries, grounding and audit rules in `AGENTS.md` may not be weakened to make a merge compile.
9. After every phase write `docs/08-program-plan/consolidation-data/PHASE-<n>-REPORT.md`: commits made, every conflict and how it was resolved (per file), test commands with counts, decisions, deviations. Push the phase branch. Claude reviews from that report and the diff.

## 3. Target model

One repository, `Otic-Group/IIP_Web_App`, two long-lived branches:

- `main`: the **SaaS edition**. Multi-tenant, self-serve, Academy, plans and payments, per-company channels.
- `managed-app`: the **Azure Managed Application edition**. Single insurer per deployment, in the insurer's own Azure subscription, Entra-only, Marketplace packaging. It contains the shared product plus the managed-app-only pieces, and does not contain SaaS-only modules.

`managed-app` is cut from `main` after `main` is complete (phase D). Afterwards, shared fixes are made on `main` and cherry-picked to `managed-app`; section 9 gives the exact procedure and the CI check that stops SaaS-only code reaching `managed-app`.

### 3.1 Which module belongs where (defaults, owner accepted)

| Module | Where | Evidence (paths on the branches) |
|---|---|---|
| Claims, documents, evidence originals, photo to PDF, scanner, recognise and rename, extraction into claim, authenticity checks, review score, status wording, officer status change, notifications, claim link, slow-connection upload, claims pack, pack email, customer memory (claims-core items 1-26) | Shared | `Api/Cases`, `Application/Claims`, `Domain/Documents`, portal claim pages |
| v2 social channels, Inbox, channel identity links, customer follow-ups, notification routing, languages, extraction fixes | Shared | v2 line, 5 new migrations |
| Fraud, policy knowledge, product setup and policy wording (one implementation, section 4) | Shared | `Domain/Products`, `Application/Products` |
| Data retention, legal hold, erasure (claims-core item 34) | Shared | `Domain/DataLifecycle` |
| Indexed lookups (item 35) | Shared | `Domain/Common/LookupKeys.cs` |
| Design direction and no-permission states (item 36) | Shared | portal |
| Bank events, outreach rules and templates (live branch) | Shared (default) | `Application/BankEvents`, `Rules`, `Templates`; confirm with owner after phase B |
| AI usage recording (`AiUsageRecords`, metering of calls) | Shared | `AddAiUsageRecords`, claims-core item 31 recorder |
| Self-serve sign-up, invitations, password sign-in, terms | SaaS only | `/signup`, `/forgot-password`, `Api/Signup*` |
| Organisations, API keys, outbound webhooks, usage counters | SaaS only | `Application/Organisations`, `Webhooks`, `AddOrganisations`, `AddApiKeys`, `AddOutboundWebhooks`, `AddUsageCounters` |
| Plans, allowances, payments (MTN, Airtel, card adapters), billing UI | SaaS only | claims-core item 32, `Domain/Billing/Plans.cs`, `Payments.cs`, `/app/admin/billing` plan card |
| Academy (learners, media, assessments, access codes, certificates, pricing) | SaaS only | `Api/Academy`, `Domain/Academy`, `web/portal/app/academy`, `components/academy` |
| Multi-tenant WhatsApp, email and claimant portal (items 27-29), per-company hostnames | SaaS only | `Application/WhatsApp`, `ClientPortal` hostname resolution |
| Marketing components, logos, insurer demo scenarios, demo seeding | SaaS only | `components/marketing`, `Application/DemoScenarios`, `DemoSeeding` |
| Platform MFA, workspace records | SaaS only (default) | `AddPlatformMfa`, `AddWorkspaceRecords`; confirm after phase B |
| Managed Application packaging (`infra/createUiDefinition.json`, `infra/mainTemplate.bicep`, `infra/bicep/*`, Marketplace metering) | Managed app only | `infra/` |
| Entra-only single-tenant profile and startup validation | Managed app only | `AzureProviderStartupValidator`, `INSURANCEINTELLIGENCE_AUTH_MODE=Entra` |

If something is not in the table, treat it as shared and note it in `DECISIONS.md`.

## 4. Source of truth where two implementations exist

Rule used everywhere: **the implementation the live deployment runs wins, unless the other implementation is a strict superset in tested behaviour.** Prove "superset" with a written capability matrix in `DECISIONS.md` before replacing anything; a matrix that is not clearly a superset means live wins and the other is not ported.

| Area | Live (`4818bb1`) | Claims-core | Default outcome |
|---|---|---|---|
| Policy wording to product setup | `ProductWordingExtractionService`, product review flow | `WordingDraftService`, `WordingExtraction`, `WordingApplication`, cited clauses with page, per-item human accept, preview as customer | Build the matrix at item 7. If claims-core covers citation checking, per-item human decisions and applying through `ProductCommandService`, claims-core replaces live behind the same routes; else live stays and claims-core's `WordingExtraction` citation check is ported as an addition. |
| Academy | Deployed SaaS Academy (plus the 76733b6 media and pricing work) | Learner Academy with access codes and marking (item 33) | SaaS Academy wins. Port only what it lacks: insurer-issued one-time access codes and insurer marking of learner work, if absent. Drop claims-core's own learner store, catalogue export and `/learn` pages. |
| Sign-in and onboarding | Company onboarding, Platform MFA, workspace records, Entra | Self-serve sign-up, passwords, invitations (item 30) | Live onboarding and MFA win. Add claims-core's invitations and forgot-password only if live lacks them; do not run two sign-up flows. |
| Usage and billing | Usage counters, AI usage records, Academy pricing and payments | Metering (item 31), plans with enforced allowances, MTN/Airtel/card adapters (item 32) | Keep claims-core plans, allowances and payment adapters (live has no equivalent); put them on the live Organisation model; reuse live `AiUsageRecords` instead of claims-core's usage row design if both record the same facts. |
| Legal hold and erasure | `AddLegalHold` | Legal hold, retention, erasure (item 34) | Live legal-hold schema wins; add claims-core retention worker and erasure service on top, mapping field names. |
| WhatsApp per-company setup | Managed WhatsApp setup, `WhatsAppConfigurationEndpoints`, receipts | Multi-tenant channel (item 27) | Manual three-way reconcile (30 overlapping files). Live contracts are the base; claims-core's workspace routing is added. All WhatsApp tests from both sides must pass. |
| Customer assistant (Kato) | Live `CustomerAssistantService` and tests | Claims-core `CustomerAssistantService` changes | Manual merge, keep both test sets. |

## 5. Procedure

All commands run in the fresh clone. `G` means `bash scripts/guard/check-config-injection.sh .` and must exit 0 before the step after it.

### Phase 0: prepare

1. `git clone https://github.com/Otic-Group/IIP_Web_App.git C:\work\iip-consolidation && cd C:\work\iip-consolidation && git fetch --all`.
2. `git switch plan/consolidation-2026-11-03` and read this file and `consolidation-data/`.
3. `git switch -c integ/00-baseline origin/live/insurer-demo-2026-10-27-4818bb1`.
4. Run `G`. Confirm `web/portal/postcss.config.js` is 81 bytes.
5. Copy `scripts/guard/check-config-injection.sh` onto this branch (from the plan branch: `git checkout origin/plan/consolidation-2026-11-03 -- scripts/guard docs/08-program-plan/consolidation-data docs/08-program-plan/IIP-CONSOLIDATION-MERGE-PLAN-2026-11-03.md`).
6. Add a CI step that runs the guard (in the existing `.github/workflows` PR check) and a pre-commit hook `.githooks/pre-commit` that runs it; set `git config core.hooksPath .githooks`.
7. Commit: `chore(security): guard against hidden code injection in config files`, stage the explicit paths only.
8. Tag the exact live baseline before the guard commit: `git tag baseline/live-4818bb1 4818bb1`. Push the tag.
9. Push the baseline as the first `main`: `git push origin integ/00-baseline:refs/heads/main` (the remote name in the fresh clone is `origin`). This is the only creation of `main`. From now on `main` moves only by fast-forward.

### Phase B1: SaaS Academy snapshot

1. `git switch -c integ/01-saas-academy main`.
2. `git cherry-pick 76733b6` (single commit on top of `dff5ab3`, an ancestor of the baseline). Overlap with the baseline is 22 files (listed in `data/sw_overlap_demo.txt`); resolve each by: keep the baseline hunk, then re-apply the wip hunk; for `academy-content.json` take the wip file and re-validate JSON; for `Program.cs` union the Academy registrations.
3. Confirm `AcademyMediaEndpoints.cs`, `LessonMedia.tsx`, `MediaUnitId` and the paid-release guard exist. The audit flags their absence from the live branch as a possible live regression; record in the phase report whether the live Academy media behaviour came from this work.
4. Gate B1: the Academy tests in `tests/InsuranceIntelligence.Api.Tests` (`AcademyContentTests`, `AcademyLearnerServiceTests`, `AcademyPaymentTests`, `AcademyPricingServiceTests`, `AcademySittingTests`) and `web/portal/tests/academy/*` pass. Then `G`.
5. Cherry-pick `8e1024f` (one document). Fast-forward `main` to this branch after the full gate in section 8.

### Phase B2: v2 social channels and related work

Ports 11 commits (two of the 13 in the v2 line are already in the baseline). Create `integ/02-v2-social` from `main` and cherry-pick in this order (hashes from `git cherry -v main org/wip/v2-social-channels-2026-11-03`, full list in `data/v2_cherry.txt`):

1. `76bf49b` remove the demo banner and status bar. The baseline already removed them differently (release `d550f0c`); keep the baseline result and only add anything the v2 commit does that the baseline lacks.
2. `cfb3bb2` WhatsApp contact enters acquisition from the first message. The baseline's release branch has the same idea (`d550f0c`); compare behaviour, keep one implementation, and keep both tests.
3. Docs commits `4475ae2`, `da25852`, `9ad47c9`, `6e24a74`, `406081e`.
4. `44d6810` (the big social snapshot: 114 modified files plus 31 new code files; 5 migrations; overlaps 52 baseline paths, listed in `data/v2_overlap_demo.txt`). Do not cherry-pick it in one go. Use `git cherry-pick -n 44d6810`, then resolve file by file using the rules in Appendix B, leaving the migration files and `InsuranceIntelligenceDbContextModelSnapshot.cs` for section 6. Files that are new (no overlap) just come in.
5. `2d9a345` docs, `14bbf54` Academy authoring (docs only), `ebe5ec7` fixtures (about 69 MB of synthetic data; keep, but it is excluded from the `managed-app` branch if the owner prefers).
6. New feature to build afterwards, not part of the merge: TikTok inbound is confirmed missing. Record in `docs/08-program-plan/consolidation-data/BACKLOG.md`.
7. Gate B2: all v2 tests added by `44d6810` (channel identity, channel timeline, customer follow-ups, notification routing, TikTok/X senders, extraction mapping, language catalogue) plus the whole existing suite. Then `G`.

### Phase B3: worker and API compatibility

1. The live worker (`func-iip-dev-h5girbnzrayqe`, deployed 2026-09-14) embeds revision `1872a14`; the live API embeds `4818bb1`. Compare `src/InsuranceIntelligence.Functions` between `1872a14` and `main`; list every Application/Infrastructure type the worker uses and whether its contract changed. `MailboxIngestionTimerFunction.cs` was changed by both the baseline and claims-core.
2. Build the Functions project from `main`, run its tests, and record in the phase report: "worker from `1872a14` is/is not compatible with API `4818bb1`" with evidence, and which worker revision each release needs.
3. Do not deploy. Only the report is required.

### Phase C: claims-core, one item at a time

Create `integ/03-claims-core` from `main` after phases B1-B3 are on `main`. Port the 52 commits in the order of Appendix A (that table gives, for every commit, how many files it touches, how many of them the baseline also changed, and how many the v2 work also changed). Mechanics for each commit:

1. `git cherry-pick -n <sha>`; if there are no conflicts and the guard is clean, run the item's targeted tests, commit with the original message prefixed `port:` (keep the `feat(item-N)` text so history is searchable), and continue.
2. If there are conflicts, resolve using Appendix B (rule per file), then run the targeted tests for the item and for the other side's features in those files, then commit.
3. Skip entirely, with a line in `DECISIONS.md`: `a792fda1` (the commit that carried the injected file; its other changes were documentation and `.gitignore`/`next-env.d.ts` noise), and every `docs(build-loop)` commit; take the build-loop documents at the end in one docs commit instead.
4. Items 30 to 33 and 34 are in the "source of truth" table; do not blindly cherry-pick them. For each: write the capability matrix, then either port the commit, port part of it with `cherry-pick -n` and `git restore --staged` of the discarded files, or skip it, and record which.
5. Item 7 (wording) follows the matrix rule in section 4 before it is ported.
6. After every 5 commits run the full gate (section 8, fast subset: build plus the Domain and Api unit tests), after items 12, 18, 29, 35 and 37 run the full gate.
7. Commit-size warnings from Appendix A: `ba74cb73` item 27 (30 shared files), `72636187` item 12 (153 files, mostly fixtures and tests), `f6f4bad1` item 11 (75 files), `6dd8d886` item 36 (17 files shared with the v2 work), item 2 (13 shared files), items 3, 4, 19 (12 shared files each).

After the last commit: `docs(build-loop)` documents in one commit; run the full gate; fast-forward `main`.

### Phase D: cut the `managed-app` branch

1. `git switch -c managed-app main` only after `main` has passed the full gate and is pushed.
2. Remove the SaaS-only modules in section 3.1 as **one commit per module**, each titled `refactor(managed-app): remove <module>`: sign-up and invitations; organisations, API keys, outbound webhooks, usage counters; plans and payments; Academy (API, domain, portal, tests, media, content); multi-tenant WhatsApp, email and portal hostname resolution (keep the single-tenant WhatsApp setup that belongs to the shared product); marketing and demo scenarios; Platform MFA and workspace records (default; confirm). For each removal also remove the matching registrations in `Program.cs` and `InfrastructureRegistration.cs`, nav entries in `lib/roles.ts` and `Sidebar.tsx`, tests, and the permission entries only if nothing else uses them.
3. Migrations on `managed-app`: do not delete migrations that are part of the shared history; entities for removed modules stay in the model only if their tables exist in every deployment's history. Simplest correct approach: keep the full migration history and model (tables for SaaS-only modules stay unused and empty in a managed-app database) and remove only code that writes to them. Record this in `DECISIONS.md`; Claude and the owner confirm before any attempt to prune migrations.
4. Add or keep `infra/createUiDefinition.json`, `infra/mainTemplate.bicep`, `infra/bicep/*` from the v2 line and set the runtime defaults for the managed app: `INSURANCEINTELLIGENCE_AUTH_MODE=Entra`, no self-serve sign-up route, no plan allowance checks, no marketing pages. Add a startup validation that refuses to run a SaaS-only route set under the `ManagedApplication` profile.
5. CI guard on `managed-app`: a script `scripts/edition/check-no-saas-only.sh` that fails if any path listed in `scripts/edition/saas-only-paths.txt` exists. Populate the list from step 2.
6. Gate D: managed-app profile checks (section 8, last rows), full backend and portal suites, and the Bicep build of `infra/mainTemplate.bicep` (`az bicep build`, no deployment).

## 6. Migrations (the highest-risk part)

Facts read from the branches:

- The live API does not migrate on startup. Migrations are applied by authenticated admin calls: `POST /api/v1/admin/db-migrate` (apply pending), `POST .../reconcile-history`, `POST .../force-reconcile-schema`, and the read-only `GET /api/v1/admin/db-migrate/diagnose` (returns every table and every row of `__EFMigrationsHistory`). `DatabaseMigrationEndpoints.cs` records nine early migrations as "already applied but untracked", so the history table alone does not describe the schema.
- Counts: old v2 main 28 migrations; live branch 42 (14 more, last `20261027073337_AddWorkspaceRecords`); claims-core 45 (17 own, `20261010152545_AddTenantSettings` through `20261011180620_AddLookupColumns`); v2 line 34 (5 own, `20261031184323_AddChannelMessages` through `20261101225108_AddCustomerFollowUps`).
- `20261007072017_AddAcademyPricing` exists in the live, claims-core and v2 lines with the identical blob (`9f702b45e7f4`): no conflict.
- Duplicated purposes with different migrations: legal hold (`AddLegalHold` vs `AddDataLifecycle`), usage (`AddUsageCounters`/`AddAiUsageRecords` vs `AddUsageMetering`), Academy (`AddAcademyRecords` vs `AddAcademyLearners`), sign-in (`AddPlatformMfa`/`AddWorkspaceRecords` vs `AddLocalSignIn`/`AddPortalIdentity`).

**Read-only step the owner must do once (the agent has no database access):** sign in to `https://app.insuranceintelligenceplatform.com` as a workspace administrator, call `GET https://api.insuranceintelligenceplatform.com/api/v1/admin/db-migrate/diagnose` with that session's bearer token, and save the JSON as `docs/08-program-plan/consolidation-data/live_db_history.json` (it contains only table names and migration ids, no customer data). If the endpoint is refused (protected profile) the alternative is to run `SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;` in Azure Data Studio after the owner adds their own IP to the SQL firewall for the session and removes it afterwards. Until this file exists, assume the live history is exactly the 42 migrations of the live branch, and verify later.

**Rules:**

1. Every migration id in the live history is immutable: never edit, rename, delete or reorder its `.cs` or `.Designer.cs`.
2. Do not copy `Designer.cs` files or `InsuranceIntelligenceDbContextModelSnapshot.cs` from claims-core or the v2 line, and never textually merge a snapshot. When a cherry-pick conflicts in the snapshot, take the current `main` version (`git checkout --ours`) and continue.
3. Copy only the entity, configuration and repository code. Leave the 17 claims-core and 5 v2 migrations out of the branch while porting.
4. After phase C is complete and the model compiles, generate new migrations from the combined model on top of the live snapshot: `dotnet ef migrations add Consolidate_ClaimsCore --project src/InsuranceIntelligence.Infrastructure --startup-project src/InsuranceIntelligence.Api` and a second one `Consolidate_V2Channels` if the schema groups cleanly; otherwise one migration. Name them with the current timestamp so they sort after `20261027073337`.
5. Review the generated SQL (`dotnet ef migrations script <last-live-migration> --idempotent`): it must contain only additive operations (`CREATE TABLE`, `ADD COLUMN` nullable or defaulted, `CREATE INDEX`). Any `DROP`, `ALTER COLUMN` narrowing, or rename means stop and report.
6. If `live_db_history.json` shows that any claims-core or v2 migration id is already applied somewhere, keep that migration verbatim and generate only the difference.
7. Tests: run the migration tests (`dotnet test --filter Migration`) and the full suite on a fresh in-memory database; the repository tests for every table that was added.
8. Do not apply anything to a database. Applying is a release step for the owner using `POST /api/v1/admin/db-migrate` after review.

## 7. Per-file rules for the 81 files both sides changed

Appendix B lists each file, the size of the change on each side (`+added -removed`, live then claims-core) and the rule. General rules:

- **`.csproj` and `package.json`:** union of package references and scripts; keep the higher version when both set one; then `dotnet restore` and `pnpm install --frozen-lockfile` must succeed (run the guard first).
- **`Program.cs` (+486/-9 live, +467/-28 claims-core):** keep the live file, then add claims-core's registrations and endpoint maps in the same order they appear there. Check for duplicate route templates and duplicate DI registrations of one interface (the second wins silently). Start the API in the Mock profile and call `/health` and `/api/v1/dev/provider-status`.
- **`InfrastructureRegistration.cs`, `EfRepositories.cs`, `InsuranceIntelligenceDbContext.cs`:** union of registrations, `DbSet` entries and `OnModelCreating` blocks. Same entity configured twice means keep the live configuration and reconcile the property set.
- **Model snapshot:** see section 6, never merged.
- **WhatsApp files (13 files):** manual three-way merge with the live contracts as the base.
- **Customer assistant files:** manual merge; keep both test files' tests.
- **Portal pages (`Sidebar.tsx`, `Login.tsx`, `ClaimReview.tsx`, `ClaimAccessStatus.tsx`, `ProductDetail.tsx`, `globals.css`):** resolve by hand; open the page in a browser at 390, 768 and 1280 px; rerun the visual suite for the touched gates.
- **Academy web files:** take the live version (source of truth).
- **Test files:** union; never drop a test.
- **`AuditEvent.cs`:** claims-core adds about 480 lines of event constants; take the union and keep the live constants' values unchanged.
- **`RolePermissionMap.cs`, `lib/roles.ts`:** union of permissions; then rerun `pnpm check:role-routes` and `pnpm check:action-permission-parity`.

## 8. Gate: what must pass before `main` advances

Run in this order on the phase branch. Use the commands the repository's own workflows use as the source of truth (`.github/workflows/*`); these are the ones used to verify claims-core and must still pass.

| Check | Command | Expected |
|---|---|---|
| Guard | `bash scripts/guard/check-config-injection.sh .` | exit 0 |
| Build | `dotnet build InsuranceIntelligence.sln` | 0 warnings, 0 errors |
| Backend tests | `dotnet test InsuranceIntelligence.sln` | 0 failed; counts never lower than the sum of the baseline suite and claims-core's (claims-core: Domain 1178, Api 1738) minus tests for code intentionally retired, each listed in `DECISIONS.md` |
| Portal static | `cd web/portal && pnpm install --frozen-lockfile && pnpm lint && pnpm test` | pass (`pnpm test` includes the role-route, permission-parity, UI release-gate, runtime-data, trust-truth, authority and scan-math checks) |
| Portal journey | `dotnet build src/InsuranceIntelligence.Api/InsuranceIntelligence.Api.csproj -c Release` then `pnpm test:claims-core` | all pass (claims-core reached 107) |
| Visual | `npx playwright test -c tests/visual/playwright.config.ts` | 0 failed (claims-core reached 150 passed, 51 skipped) |
| Recovery and deployed | `pnpm test:runtime-recovery`, `pnpm test:deployed-dom` | pass |
| Live-branch tests | the baseline's own suites (`web/portal/tests/workspace`, `tests/academy`, `tests/signin`, API tests) | pass |
| Two-company journey | `npx playwright test --config playwright.claims-core.config.ts -g "Item 37"` | pass |
| Migrations | section 6 rule 5 and 7 | additive only; tests pass |
| Managed-app profile (phase D) | `scripts/edition/check-no-saas-only.sh`, boot with `ManagedApplication` profile, `az bicep build` | pass |

Record the exact counts in the phase report. A failing check blocks the fast-forward; fix and rerun, never skip.

## 9. Keeping the two branches in step after the merge

1. Shared fix: make it on `main` (or a branch from `main`), land it, then `git switch managed-app && git cherry-pick <sha>`. If the commit touches a path in `scripts/edition/saas-only-paths.txt`, split it first.
2. SaaS-only change: `main` only. The managed-app CI check proves it did not leak.
3. Managed-app-only change: `managed-app` only.
4. Once a month, `git log --oneline managed-app..main -- . ':!<saas-only paths>'` lists shared commits not yet cherry-picked.

## 10. Releases and traceability

For each release record the commit hash and artifact SHA-256 of API, portal and worker, plus the highest migration id, in `docs/05-deployment-release-enablement/RELEASE_RECORDS.md`. The API uses `WEBSITE_RUN_FROM_PACKAGE=1`; identify the running revision from `data/SitePackages/packagename.txt` and the active zip, not from `site/wwwroot`, which holds stale assemblies. Merging to `main` does not deploy anything; `deploy-staging.yml` is manual and guarded.

## 11. Review by Claude

After each phase report, Claude checks: the guard output; that no commit staged files by wildcard; the per-file conflict resolutions against Appendix B; the capability matrices and `DECISIONS.md`; the test counts against section 8; migrations for any non-additive operation; tenant isolation tests present for every new data type; no em dashes or emoji; that `main` only advanced by fast-forward and nothing was force-pushed.

## 12. Stop conditions

Stop and report (do not improvise) if: the guard fails; a test count drops without a recorded reason; a migration needs a destructive operation; `main` cannot fast-forward; a push is refused; any command would touch Azure or a database; the live history file contradicts the assumed 42 migrations.

## Data in `consolidation-data/`

`cc_commit_map.csv`, `overlap_demo_cc_churn.csv`, `v2_cherry.txt`, `overlap_demo_cc.txt`, `demo_changed.txt`, `cc_changed.txt`, `v2_changed.txt`, `sw_changed.txt`, `sw_overlap_demo.txt`, `v2_overlap_demo.txt`, `migrations_by_branch.txt`.

## Appendix A. Claims-core commits in port order

Columns: files touched, files the live baseline also changed, files the v2 work also changed, migration files touched (never copy these, section 6), how to handle.

| # | Commit | Subject | Files | Shared with live | Shared with v2 | Migrations | Handling |
|---:|---|---|---:|---:|---:|---:|---|
| 1 | `27bdd200` | chore(build-loop): set up build plan | 6 | 0 | 2 | 0 | Docs: take at the end in one commit |
| 2 | `7e7fd5ec` | feat(item-1): deterministic Mock providers for local runs | 33 | 2 | 4 | 0 | Port, resolve per Appendix B |
| 3 | `7a3c9c2a` | feat(item-2): fix known defects | 33 | 13 | 10 | 0 | Port, resolve per Appendix B |
| 4 | `34e8b6db` | feat(item-3): claim types per product | 36 | 12 | 4 | 0 | Port, resolve per Appendix B |
| 5 | `12fe1c47` | feat(item-4): required documents and evidence per claim type | 35 | 12 | 5 | 0 | Port, resolve per Appendix B |
| 6 | `e0cf576c` | feat(item-5): claim fields per claim type | 43 | 14 | 5 | 0 | Port, resolve per Appendix B |
| 7 | `732f01b8` | feat(item-6): operating settings per workspace (IRA rule, reminder and SLA timings, seat l | 40 | 10 | 7 | 3 | Port, resolve per Appendix B |
| 8 | `96995fa6` | feat(item-7): read a policy wording into proposed setup for a person to confirm | 36 | 10 | 5 | 3 | Capability matrix first (section 4, wording) |
| 9 | `d2541d2e` | feat(item-8): preview as customer, sharing the claimant form and documents components | 23 | 3 | 1 | 0 | Port, resolve per Appendix B |
| 10 | `c4eb30a0` | feat(item-9): privacy notice and consent before the first upload, with withdrawal | 43 | 5 | 4 | 0 | Port, resolve per Appendix B |
| 11 | `14896688` | feat(item-10): Take photo and Choose file on every document slot | 25 | 0 | 0 | 0 | Port, resolve per Appendix B |
| 12 | `f6f4bad1` | feat(item-11): evidence kept byte for byte with SHA-256, separate previews, one client com | 75 | 6 | 8 | 0 | Port, resolve per Appendix B |
| 13 | `72636187` | feat(item-12): pictures of paperwork become one ordered PDF per slot, originals kept along | 153 | 9 | 10 | 3 | Port, resolve per Appendix B |
| 14 | `db5cd078` | feat(item-13): in-browser camera document scanner with corner adjustment, straightening, c | 12 | 2 | 0 | 0 | Port, resolve per Appendix B |
| 15 | `d983ffd8` | feat(item-14): recognise every file against the workspace catalogue, name it, file or revi | 25 | 6 | 7 | 0 | Port, resolve per Appendix B |
| 16 | `9ca9462a` | feat(item-15): values read from documents are proposed for the claim's questions, decided  | 31 | 8 | 8 | 3 | Port, resolve per Appendix B |
| 17 | `75fa0dd5` | docs(build-loop): add the final step, moving to the organisation repository | 5 | 0 | 1 | 0 | Docs: take at the end in one commit |
| 18 | `61250494` | feat(item-16): authenticity checks on pictures with advisory findings and a human disposit | 24 | 8 | 7 | 3 | Port, resolve per Appendix B |
| 19 | `6a92260f` | feat(item-17): durable background pipeline for document processing and claim review, with  | 38 | 11 | 8 | 3 | Port, resolve per Appendix B |
| 20 | `0505d44c` | feat(item-18): review score and recommendation for each claim, explained, advisory, behind | 26 | 6 | 7 | 3 | Port, resolve per Appendix B |
| 21 | `157e738f` | feat(item-19): customer wording for every claim status and a timeline drawn from the produ | 28 | 12 | 6 | 0 | Port, resolve per Appendix B |
| 22 | `dd4ba28c` | feat(item-20): one audited Change status action along defined moves, with a reason and the | 17 | 4 | 3 | 0 | Port, resolve per Appendix B |
| 23 | `3df4e4d3` | feat(item-21): tell the customer when their claim's status changes, in the insurer's words | 28 | 4 | 2 | 0 | Port, resolve per Appendix B |
| 24 | `9ce9d952` | feat(item-22): staff can send the claimant a secure link to their claim, respecting consen | 14 | 3 | 3 | 0 | Port, resolve per Appendix B |
| 25 | `5dd03741` | feat(item-23): resumable uploads with progress, retry and backoff, kept across reloads, wi | 25 | 7 | 4 | 3 | Port, resolve per Appendix B |
| 26 | `13ca41fb` | feat(item-24): claims pack zip with a summary PDF, renamed documents, untouched evidence a | 14 | 3 | 3 | 0 | Port, resolve per Appendix B |
| 27 | `7a33134c` | chore: write the dash in test assertions as an escape so the repository holds no literal e | 5 | 0 | 0 | 0 | Port as is (test assertions) |
| 28 | `e9b7dbfd` | feat(item-25): claims pack emailed to named recipients by hand or by a chosen trigger, att | 38 | 9 | 7 | 3 | Port, resolve per Appendix B |
| 29 | `d7f8735f` | feat(item-26): returning customers are offered the details and documents they already gave | 27 | 8 | 7 | 0 | Port, resolve per Appendix B |
| 30 | `ba74cb73` | feat(item-27): several workspaces share one WhatsApp channel, each with its own number, cr | 60 | 30 | 8 | 0 | Manual three-way reconcile; SaaS only; section 4 |
| 31 | `9f39c5c0` | feat(item-28): mail is collected for every insurer, including over IMAP for those without  | 30 | 6 | 2 | 0 | Manual three-way reconcile; SaaS only; section 4 |
| 32 | `fe700cfe` | feat(item-29): each insurer's customers arrive on their own portal, with their own name, a | 39 | 11 | 4 | 3 | Manual three-way reconcile; SaaS only; section 4 |
| 33 | `239d290d` | feat(item-30): self-serve sign-up, email and password sign-in, staff invitations | 41 | 10 | 7 | 3 | Source-of-truth table; matrix first; port part or skip |
| 34 | `8700361f` | feat(item-31): every AI call, document and byte stored is metered per workspace | 33 | 10 | 4 | 3 | Source-of-truth table; matrix first; port part or skip |
| 35 | `c8ac55da` | feat(item-32): plans with enforced allowances, upgrade prompts, mobile money and card paym | 50 | 10 | 9 | 3 | Source-of-truth table; matrix first; port part or skip |
| 36 | `460198c5` | feat(item-33): IIP Academy for learners outside any workspace | 41 | 10 | 7 | 3 | Source-of-truth table; matrix first; port part or skip |
| 37 | `d3935445` | feat(item-34): retention periods, legal holds and erasure of one person's data | 37 | 6 | 7 | 3 | Live legal-hold schema wins; port retention and erasure on top |
| 38 | `e55cac03` | feat(item-35): indexed lookups for claims, customers and policies | 29 | 7 | 6 | 3 | Port, resolve per Appendix B |
| 39 | `6dd8d886` | feat(item-36): design direction across the portal, phone-first rail, no-permission states, | 39 | 7 | 17 | 0 | Manual UI merge; rerun visual gates |
| 40 | `ae4ea6fc` | feat(item-37): final pass, two-company journey, billing no-permission state, build loop su | 6 | 1 | 1 | 0 | Port; includes two-company journey test |
| 41 | `2f089b6d` | docs(build-loop): item 38 blocked on the repository address, owner command recorded | 2 | 0 | 0 | 0 | Docs: take at the end in one commit |
| 42 | `68bf40f9` | feat(academy): browse by category, then course, then its steps | 6 | 2 | 0 | 0 | Skip, Academy source of truth is the SaaS Academy |
| 43 | `6775941b` | fix(portal): Microsoft sign-in returns to the address the person started on | 1 | 1 | 1 | 0 | Check vs live auth; live wins, port only if missing |
| 44 | `5b1bac42` | fix(portal): the sign-in page finishes a Microsoft sign-in on return instead of staying pu | 1 | 1 | 1 | 0 | Check vs live auth; live wins, port only if missing |
| 45 | `a792fda1` | docs(build-loop): item 38 done, build/claims-core pushed to the organisation repository | 6 | 1 | 2 | 0 | Docs: take at the end in one commit |
| 46 | `0344841e` | docs(build-loop): record clean verification rerun | 3 | 0 | 0 | 0 | Docs: take at the end in one commit |
| 47 | `a6222f68` | fix(security): remove obfuscated PostCSS payload | 1 | 0 | 0 | 0 | Skip (already cleaned); keep the guard instead |
| 48 | `f901ab27` | docs(build-loop): record branch cleanup verification | 1 | 0 | 0 | 0 | Docs: take at the end in one commit |
| 49 | `649b733e` | docs(build-loop): record deployed access check | 1 | 0 | 0 | 0 | Docs: take at the end in one commit |
| 50 | `aa640229` | docs(build-loop): record authentication blocker for push | 3 | 0 | 0 | 0 | Docs: take at the end in one commit |
| 51 | `951a3d91` | docs(build-loop): correct final item statuses | 3 | 0 | 0 | 0 | Docs: take at the end in one commit |
| 52 | `39574851` | docs(build-loop): fraud use cases, policy wording test prompt, research notes and reports | 7 | 0 | 0 | 0 | Docs: take at the end in one commit |

## Appendix B. The 81 files changed on both sides

Churn is lines added and removed on each side since the common base `b211051`.

| File | Live | Claims-core | Rule |
|---|---|---|---|
| `src/InsuranceIntelligence.Api/Admin/Billing/BillingEndpoints.cs` | +5 -0 | +33 -17 | Keep live hunk, re-apply claims-core hunk, run the area tests |
| `src/InsuranceIntelligence.Api/Auth/RuntimeHeaderGuard.cs` | +12 -1 | +14 -1 | Keep live hunk, re-apply claims-core hunk, run the area tests |
| `src/InsuranceIntelligence.Api/Azure/AzureDocumentIntelligenceProvider.cs` | +5 -28 | +3 -8 | Keep live hunk, re-apply claims-core hunk, run the area tests |
| `src/InsuranceIntelligence.Api/Azure/AzureOpenAiProviders.cs` | +12 -1 | +23 -6 | Keep live hunk, re-apply claims-core hunk, run the area tests |
| `src/InsuranceIntelligence.Api/Azure/AzureOpenAiToolCallingChatProvider.cs` | +9 -1 | +14 -2 | Keep live hunk, re-apply claims-core hunk, run the area tests |
| `src/InsuranceIntelligence.Api/Cases/CaseEndpoints.cs` | +7 -2 | +13 -2 | Keep live hunk, re-apply claims-core hunk, run the area tests |
| `src/InsuranceIntelligence.Api/ClientPortal/ClaimantAuthenticationEndpoints.cs` | +2 -1 | +28 -14 | Keep live hunk, re-apply claims-core hunk, run the area tests |
| `src/InsuranceIntelligence.Api/ClientPortal/ClientPortalEndpoints.cs` | +19 -5 | +434 -108 | Keep live hunk, re-apply claims-core hunk, run the area tests |
| `src/InsuranceIntelligence.Api/ClientPortal/WhatsAppClaimantAccessLinkIssuer.cs` | +2 -1 | +4 -3 | Manual three-way, live contracts as base; all WhatsApp tests from both sides |
| `src/InsuranceIntelligence.Api/Compliance/DataSubjectRightsEndpoints.cs` | +1 -1 | +1 -1 | Keep live hunk, re-apply claims-core hunk, run the area tests |
| `src/InsuranceIntelligence.Api/Documents/DocumentMapper.cs` | +16 -1 | +48 -2 | Keep live hunk, re-apply claims-core hunk, run the area tests |
| `src/InsuranceIntelligence.Api/Documents/DocumentResponses.cs` | +13 -1 | +48 -3 | Keep live hunk, re-apply claims-core hunk, run the area tests |
| `src/InsuranceIntelligence.Api/InsuranceIntelligence.Api.csproj` | +3 -0 | +1 -0 | Union of references and scripts; restore must succeed |
| `src/InsuranceIntelligence.Api/Integrations/SmsIntegrationEndpoints.cs` | +18 -7 | +25 -5 | Keep live hunk, re-apply claims-core hunk, run the area tests |
| `src/InsuranceIntelligence.Api/Integrations/WhatsAppConfigurationEndpoints.cs` | +52 -0 | +13 -0 | Manual three-way, live contracts as base; all WhatsApp tests from both sides |
| `src/InsuranceIntelligence.Api/Integrations/WhatsAppIntegrationEndpoints.cs` | +121 -30 | +68 -50 | Manual three-way, live contracts as base; all WhatsApp tests from both sides |
| `src/InsuranceIntelligence.Api/Local/StaticDocumentChecklistProvider.cs` | +23 -2 | +8 -81 | Keep live hunk, re-apply claims-core hunk, run the area tests |
| `src/InsuranceIntelligence.Api/Program.cs` | +486 -9 | +467 -28 | Keep live, add claims-core registrations and maps in order; check duplicates; boot test |
| `src/InsuranceIntelligence.Api/Workflow/WorkflowEndpoints.cs` | +1 -1 | +3 -1 | Keep live hunk, re-apply claims-core hunk, run the area tests |
| `src/InsuranceIntelligence.Application/Ai/ToolCallingContracts.cs` | +6 -1 | +1 -1 | Keep live hunk, re-apply claims-core hunk, run the area tests |
| `src/InsuranceIntelligence.Application/CustomerAssistant/CustomerAssistantService.Agent.cs` | +103 -4 | +46 -20 | Manual merge; keep both test sets |
| `src/InsuranceIntelligence.Application/CustomerAssistant/CustomerAssistantService.cs` | +216 -20 | +87 -20 | Manual merge; keep both test sets |
| `src/InsuranceIntelligence.Application/CustomerAssistant/KatoBrand.cs` | +64 -8 | +81 -5 | Manual merge; keep both test sets |
| `src/InsuranceIntelligence.Application/CustomerAssistant/MicroinsuranceCatalog.cs` | +25 -2 | +5 -16 | Manual merge; keep both test sets |
| `src/InsuranceIntelligence.Application/DemoSeeding/DemoSeedService.cs` | +8 -5 | +21 -1 | Keep live hunk, re-apply claims-core hunk, run the area tests |
| `src/InsuranceIntelligence.Application/DemoSeeding/MicroinsuranceDemoSeeder.cs` | +117 -8 | +32 -2 | Keep live hunk, re-apply claims-core hunk, run the area tests |
| `src/InsuranceIntelligence.Application/Products/ProductCommandService.cs` | +56 -2 | +514 -21 | Wording decision first (section 4); then union |
| `src/InsuranceIntelligence.Application/Products/ProductContracts.cs` | +15 -3 | +97 -4 | Wording decision first (section 4); then union |
| `src/InsuranceIntelligence.Application/SalesLeads/ConversationInsightsService.cs` | +9 -7 | +5 -2 | Keep live hunk, re-apply claims-core hunk, run the area tests |
| `src/InsuranceIntelligence.Application/SalesLeads/OutreachService.cs` | +5 -2 | +6 -2 | Keep live hunk, re-apply claims-core hunk, run the area tests |
| `src/InsuranceIntelligence.Application/SalesLeads/ProspectTriageService.cs` | +3 -1 | +4 -1 | Keep live hunk, re-apply claims-core hunk, run the area tests |
| `src/InsuranceIntelligence.Application/WhatsApp/WhatsAppMessagingService.cs` | +5 -1 | +1 -0 | Manual three-way, live contracts as base; all WhatsApp tests from both sides |
| `src/InsuranceIntelligence.Domain/Administration/RolePermissionMap.cs` | +30 -5 | +22 -4 | Union of permissions; rerun role-route and parity checks |
| `src/InsuranceIntelligence.Domain/Audit/AuditEvent.cs` | +23 -0 | +479 -1 | Union of constants; keep live values unchanged |
| `src/InsuranceIntelligence.Domain/Communications/CommunicationInterfaces.cs` | +6 -0 | +8 -1 | Keep live hunk, re-apply claims-core hunk, run the area tests |
| `src/InsuranceIntelligence.Domain/Documents/DocumentCompletenessChecklist.cs` | +25 -2 | +6 -1 | Keep live hunk, re-apply claims-core hunk, run the area tests |
| `src/InsuranceIntelligence.Domain/Integrations/WhatsAppChannelConfiguration.cs` | +5 -0 | +9 -2 | Manual three-way, live contracts as base; all WhatsApp tests from both sides |
| `src/InsuranceIntelligence.Domain/Integrations/WhatsAppInterfaces.cs` | +24 -0 | +10 -0 | Manual three-way, live contracts as base; all WhatsApp tests from both sides |
| `src/InsuranceIntelligence.Domain/Integrations/WhatsAppModels.cs` | +4 -0 | +7 -0 | Manual three-way, live contracts as base; all WhatsApp tests from both sides |
| `src/InsuranceIntelligence.Domain/Products/Product.cs` | +12 -0 | +230 -6 | Wording decision first (section 4); then union |
| `src/InsuranceIntelligence.Functions/MailboxIngestionTimerFunction.cs` | +7 -0 | +11 -35 | Keep live hunk, re-apply claims-core hunk, run the area tests |
| `src/InsuranceIntelligence.Infrastructure/DependencyInjection/InfrastructureRegistration.cs` | +84 -2 | +33 -0 | Union of registrations, DbSets, model config |
| `src/InsuranceIntelligence.Infrastructure/InsuranceIntelligence.Infrastructure.csproj` | +1 -0 | +1 -0 | Union of references and scripts; restore must succeed |
| `src/InsuranceIntelligence.Infrastructure/Migrations/InsuranceIntelligenceDbContextModelSnapshot.cs` | +788 -62 | +1058 -115 | NEVER merge; take main, regenerate (section 6) |
| `src/InsuranceIntelligence.Infrastructure/Persistence/EfRepositories.cs` | +100 -10 | +185 -11 | Union of registrations, DbSets, model config |
| `src/InsuranceIntelligence.Infrastructure/Persistence/InsuranceIntelligenceDbContext.cs` | +176 -0 | +216 -5 | Union of registrations, DbSets, model config |
| `src/InsuranceIntelligence.Infrastructure/Translation/SunbirdVoiceNoteTranscriber.cs` | +4 -2 | +9 -1 | Keep live hunk, re-apply claims-core hunk, run the area tests |
| `src/InsuranceIntelligence.Infrastructure/WhatsApp/EfWhatsAppChannelConfigurationRepository.cs` | +10 -0 | +9 -0 | Manual three-way, live contracts as base; all WhatsApp tests from both sides |
| `src/InsuranceIntelligence.Infrastructure/WhatsApp/WhatsAppAttachmentProcessor.cs` | +2 -2 | +52 -16 | Manual three-way, live contracts as base; all WhatsApp tests from both sides |
| `src/InsuranceIntelligence.Infrastructure/WhatsApp/WhatsAppCloudApiInboundProvider.cs` | +43 -9 | +35 -13 | Manual three-way, live contracts as base; all WhatsApp tests from both sides |
| `src/InsuranceIntelligence.Infrastructure/WhatsApp/WhatsAppCloudApiOutboundProvider.cs` | +17 -17 | +11 -11 | Manual three-way, live contracts as base; all WhatsApp tests from both sides |
| `src/InsuranceIntelligence.Infrastructure/WhatsApp/WhatsAppInboundDocumentIngestionService.cs` | +35 -23 | +100 -33 | Manual three-way, live contracts as base; all WhatsApp tests from both sides |
| `src/InsuranceIntelligence.Infrastructure/WhatsApp/WhatsAppRuntimeSettingsProvider.cs` | +98 -8 | +171 -33 | Manual three-way, live contracts as base; all WhatsApp tests from both sides |
| `src/InsuranceIntelligence.LocalTestDoubles/InMemorySupportRepositories.cs` | +9 -0 | +23 -0 | Keep live hunk, re-apply claims-core hunk, run the area tests |
| `src/InsuranceIntelligence.LocalTestDoubles/InMemoryWhatsAppChannelConfigurationRepository.cs` | +3 -0 | +3 -0 | Manual three-way, live contracts as base; all WhatsApp tests from both sides |
| `src/InsuranceIntelligence.LocalTestDoubles/MockDocumentProcessingProvider.cs` | +1 -37 | +1 -36 | Keep live hunk, re-apply claims-core hunk, run the area tests |
| `tests/InsuranceIntelligence.Api.Tests/CustomerAssistantServiceTests.cs` | +316 -10 | +91 -14 | Manual merge; keep both test sets |
| `tests/InsuranceIntelligence.Domain.Tests/Products/ProductCommandServiceTests.cs` | +57 -2 | +59 -0 | Union; never drop a test |
| `web/portal/app/app/academy/page.tsx` | +0 -5 | +6 -1 | Take live (SaaS Academy source of truth) |
| `web/portal/app/globals.css` | +70 -0 | +1 -1 | Resolve by hand; check at 390, 768, 1280 px; rerun touched visual gates |
| `web/portal/app/signup/page.tsx` | +16 -0 | +10 -0 | Resolve by hand; check at 390, 768, 1280 px; rerun touched visual gates |
| `web/portal/app/terms/page.tsx` | +21 -0 | +10 -0 | Resolve by hand; check at 390, 768, 1280 px; rerun touched visual gates |
| `web/portal/components/academy/AcademyCatalogue.tsx` | +0 -90 | +9 -1 | Take live (SaaS Academy source of truth) |
| `web/portal/components/academy/CoursePlayer.tsx` | +0 -300 | +15 -1 | Take live (SaaS Academy source of truth) |
| `web/portal/components/iip/AssistantBar.tsx` | +4 -3 | +2 -2 | Resolve by hand; check at 390, 768, 1280 px; rerun touched visual gates |
| `web/portal/components/iip/Sidebar.tsx` | +95 -41 | +8 -2 | Resolve by hand; check at 390, 768, 1280 px; rerun touched visual gates |
| `web/portal/lib/academy/certificate.ts` | +0 -94 | +5 -1 | Take live (SaaS Academy source of truth) |
| `web/portal/lib/clientPortal.ts` | +29 -2 | +111 -10 | Resolve by hand; check at 390, 768, 1280 px; rerun touched visual gates |
| `web/portal/lib/config.ts` | +54 -2 | +23 -3 | Resolve by hand; check at 390, 768, 1280 px; rerun touched visual gates |
| `web/portal/lib/entraAuth.ts` | +199 -20 | +87 -1 | Resolve by hand; check at 390, 768, 1280 px; rerun touched visual gates |
| `web/portal/lib/products.ts` | +37 -3 | +57 -0 | Wording decision first (section 4); then union |
| `web/portal/lib/roles.ts` | +15 -6 | +8 -2 | Union of permissions; rerun role-route and parity checks |
| `web/portal/lovable-pages/Admin.tsx` | +8 -2 | +7 -1 | Resolve by hand; check at 390, 768, 1280 px; rerun touched visual gates |
| `web/portal/lovable-pages/ClaimAccessStatus.tsx` | +55 -11 | +199 -116 | Resolve by hand; check at 390, 768, 1280 px; rerun touched visual gates |
| `web/portal/lovable-pages/ClaimReview.tsx` | +236 -128 | +60 -30 | Resolve by hand; check at 390, 768, 1280 px; rerun touched visual gates |
| `web/portal/lovable-pages/Login.tsx` | +73 -30 | +76 -49 | Resolve by hand; check at 390, 768, 1280 px; rerun touched visual gates |
| `web/portal/lovable-pages/admin/ProductDetail.tsx` | +16 -0 | +198 -3 | Wording decision first (section 4); then union |
| `web/portal/package.json` | +5 -1 | +4 -1 | Union of references and scripts; restore must succeed |
| `web/portal/tests/deployed-profile/deployed-profile.dom.spec.ts` | +0 -1 | +1 -1 | Union; never drop a test |
| `web/portal/tests/visual/specs/gate-10-ai-conversations.visual.spec.ts` | +2 -7 | +8 -8 | Union; never drop a test |
| `web/portal/tests/visual/specs/gate-11-command-center.visual.spec.ts` | +0 -2 | +2 -2 | Union; never drop a test |
