# Prompt for Codex: consolidate the IIP repositories

Paste everything below the line into Codex.

---

You are executing a repository consolidation for the Insurance Intelligence Platform (IIP). The full, file-level plan is already written. Your job is to follow it exactly, phase by phase, and to report after each phase so it can be reviewed. Do not redesign the plan.

## Read first, in this order

1. Clone `https://github.com/Otic-Group/IIP_Web_App.git` into a fresh folder outside any existing IIP checkout, then `git fetch --all`.
2. `git switch plan/consolidation-2026-11-03`.
3. Read, completely, before touching anything:
   - `docs/08-program-plan/IIP-CONSOLIDATION-MERGE-PLAN-2026-11-03.md` (the plan, with Appendix A and B)
   - `docs/08-program-plan/IIP-BRANCH-AND-LIVE-RECONCILIATION-AUDIT-2026-11-03.md` (the evidence behind it)
   - `docs/08-program-plan/consolidation-data/` (diff data, `DECISIONS.md`, `BACKLOG.md`)
   - `AGENTS.md` and `CLAUDE.md` in the repository root, and the per-item decisions in `docs/build-loop/DECISIONS.md` on `build/claims-core`.

## What you are building

One repository, two long-lived branches:

- `main` = the **SaaS edition** (multi-tenant, self-serve sign-up, Academy, plans and payments, per-company channels).
- `managed-app` = the **Azure Managed Application edition** (one insurer per deployment, Entra-only, Marketplace packaging, no SaaS-only modules).

`main` is created from the live-deployed code (`live/insurer-demo-2026-10-27-4818bb1`). Then, in order: the SaaS Academy work (`76733b6`), the v2 social channels work (`wip/v2-social-channels-2026-11-03`), a worker/API compatibility report, and the 52 commits of `build/claims-core` one at a time. Then `managed-app` is cut from the finished `main` by removing the SaaS-only modules, one commit per module.

## Hard rules (a violation means stop and report)

1. **Security gate.** The repositories were hit by a hidden-code injection in `web/portal/postcss.config.js` (a normal config followed by a ~12,900 character obfuscated line after a run of tabs), plus `.gitignore` entries hiding `temp_auto_push.bat`, `temp_interactive_push.bat` and `branch_structure.json`. Before every install, build, test run and commit, run `bash scripts/guard/check-config-injection.sh .`. It must exit 0. If it fires, stop and report the file and commit; do not delete anything.
2. Never run `git add -A`, `git add .` or `git commit -a`. Stage explicit paths only.
3. Never check out, install, build or run `origin/main` of `OticGroupAz/agenticIip-v2`, any `copilot/*` branch, or anything from `OticGroupAz/agenticIip`. Read them with `git show` and `git diff` only.
4. Never force-push. Never delete or rewrite any existing branch or tag. `main` advances only by fast-forward from a phase branch whose gate passed.
5. Never deploy. Never change anything in Azure. You have no database access; the owner supplies `consolidation-data/live_db_history.json`. Until it exists, assume the live history is exactly the 42 migrations of the live branch.
6. Never delete, skip or weaken a test. If both sides changed a test file, keep both sides' tests. A test count may not drop without a reason recorded in `DECISIONS.md`.
7. Migrations: every migration in the live history is immutable. Never copy a `Designer.cs` or the model snapshot from claims-core or the v2 line, and never textually merge a snapshot. Port entity and repository code, then generate new additive migrations from the combined model on top of the live snapshot. Any `DROP`, narrowing `ALTER COLUMN` or rename in the generated SQL means stop and report.
8. No em dashes and no emoji anywhere (code, comments, UI copy, docs, commits). In C# test assertions write an em dash as `(char)0x2014`.
9. Do not weaken tenant isolation, human-decision boundaries, grounding or audit rules to make a merge compile.
10. Where two implementations of the same thing exist, the one the live deployment runs wins unless the other is provably a strict superset in tested behaviour. Prove it with a written capability matrix in `DECISIONS.md` before replacing anything (plan section 4).

## How to work

- Follow plan section 5 phase by phase: Phase 0 (prepare, guard, create `main`), B1 (SaaS Academy), B2 (v2 social), B3 (worker compatibility report), C (claims-core, one commit at a time using Appendix A and the per-file rules in Appendix B), D (cut `managed-app`).
- After every phase run the gate in plan section 8 (build 0 warnings and 0 errors, backend tests, `pnpm lint` and `pnpm test`, `pnpm test:claims-core` after building the Release API, the visual suite, runtime-recovery and deployed-dom, the baseline's own suites, the two-company journey, and the migration checks). Use `.github/workflows/*` as the source of truth for commands.
- Work on phase branches named `integ/<nn>-<name>`. Push each phase branch. Fast-forward `main` only after the gate passes. Direct pushes are allowed; open a pull request only if a push is refused.
- After every phase write `docs/08-program-plan/consolidation-data/PHASE-<n>-REPORT.md` containing: the commits made (hash and subject), every conflict and exactly how you resolved it (per file), the commands you ran with their pass/fail counts, decisions taken, and any deviation from the plan. Commit and push the report with the phase.
- Record every judgement call in `consolidation-data/DECISIONS.md` (what, why, the alternative).
- When something is unclear, choose the safer default in the plan, record it, and continue. Do not stop to ask unless a stop condition in plan section 12 applies.

## Stop and report if

The guard fails; a test count drops without a recorded reason; a migration needs a destructive operation; `main` cannot fast-forward; a push is refused; a command would touch Azure or a database; the live history contradicts the assumed 42 migrations.

## Finish

When `main` and `managed-app` are both green and pushed, write `docs/08-program-plan/consolidation-data/FINAL-REPORT.md`: final commit hashes of both branches, the full gate results for each, the list of everything skipped or retired and why, the open items in `BACKLOG.md`, and the release-record entries (commit hash, artifact SHA-256 and highest migration id for API, portal and worker). A reviewer will check the work from your phase reports, `DECISIONS.md` and the diffs, so make those complete and honest. If something failed or was not done, say so plainly.
