# Consolidation decisions

One entry per judgement call made while executing `IIP-CONSOLIDATION-MERGE-PLAN-2026-11-03.md`: what was decided, why, and the alternative. Claude reviews this file after each phase.

## Decisions already made by the owner (2026-11-03)

- D-001 Two editions in one repository: `main` is the SaaS edition, `managed-app` is the Azure Managed Application edition, cut from `main` after it is complete.
- D-002 Repository: `Otic-Group/IIP_Web_App`. `main` is created from the live lineage `4818bb1`. `OticGroupAz/agenticIip-v2` is not reused because its `main` carries the hidden-code injection.
- D-003 Direct pushes are allowed; pull requests only if a push is refused. No force-push, no deployment, no change to Azure resources.
- D-004 Defaults in plan sections 3.1 and 4 apply (module placement and source of truth), with the capability-matrix rule for the policy wording, sign-in, billing and Academy overlaps.
- D-005 The agent has no database access. The owner supplies `live_db_history.json` from the read-only diagnose endpoint (plan section 6).

## Decisions made during execution

### D-006 Owner security prerequisite confirmed

On 2026-11-03 the owner confirmed in this chat: "The listed checks and credential rotation are complete on this machine". This satisfies the plan's prerequisite to continue here. The agent did not independently certify the scans or rotation. Alternative: move execution to a clean environment.

### D-007 Independent clone with locally available Git objects

Execution uses `C:/work/iip-consolidation-verified`, outside all existing IIP checkouts. An ordinary remote clone failed with a connection reset, and a retry stalled and was cancelled. The successful remote clone used the two existing object stores as `--reference-if-able` sources with `--dissociate`. GitHub supplied the refs; `git fetch --all` passed. The new clone has no `.git/objects/info/alternates`, so it is independent. No existing checkout, branch or tag was changed. Alternative: repeatedly download the same objects over the failing connection.

### D-008 Bootstrap the guard before installing it on the baseline

Phase 0 asks to run the guard on the live baseline before copying it there, but the baseline has no guard. The reviewed plan-branch script was copied outside the checkout and run against the baseline first, then installed from the plan branch. Both plan and baseline checks passed. The baseline PostCSS Git blob is exactly 81 bytes. Alternative: install the script before checking, which reverses the listed step order.

### D-009 Preserve the plan and evidence beside phase reports

In addition to the explicitly listed plan, data and guard paths, Phase 0 copies the handover and branch/live audit from the plan branch. They are the instructions and evidence named by the handover. No application files are imported from the plan branch. Alternative: keep these two documents reachable only through the plan branch.

### D-010 Repair baseline build warnings without weakening checks

The first Release build of the live baseline succeeded with 17 warnings and 0 errors, which fails the plan's zero-warning gate. The XML package pin moves from 8.0.4 to Microsoft's patched 8.0.5. Sources: https://github.com/dotnet/runtime/security/advisories/GHSA-7hqp-7c5m-4vq6 and https://github.com/dotnet/runtime/security/advisories/GHSA-vrv5-2jg9-4mmr. Both identify 8.0.5 as patched. The other warnings are addressed with an explicit null check in the existing outcome path, an explicit `new` modifier on the existing authentication scheme constant, equivalent xUnit collection assertions and awaited tasks in seven document tests. No test, assertion or expected outcome is removed. These are current supported contracts; the null branch already returns when no reason exists. Alternative: suppress warnings or advance main with a red gate, both rejected.

The copied migration inventory's trailing blank line was removed to pass `git diff --cached --check`; no migration entry or migration source changed.

### D-011 Consolidation authorization overrides the older PR-only process

The owner's handover explicitly permits direct phase pushes and fast-forward main updates after gates pass. That task-specific authorization supersedes the older repository's PR-only process for this consolidation. All three CI jobs and the broader phase checks remain in force; nothing is deployed. A refused push triggers the plan's stop-and-report rule.

### D-012 Phase 0 validates the tests present in the live baseline

The live baseline has 735 Domain and 1,741 API tests. It does not contain `playwright.claims-core.config.ts`, the `test:claims-core` package script, or the Item 37 journey; those are scheduled for Phase C. Phase 0 runs the baseline's existing suites and records the absent future suites as not yet applicable, never as passed. The tests present at baseline are retained and their counts must not fall. Phase C and final gates must include the imported tests and journeys. Alternative: silently claim future suites ran, or port test-dependent features out of the prescribed order, both rejected.

### D-013 Push reviewable phase work without moving main

The phase branch and baseline tag may be pushed while local browser checks are still running, so the unchanged CI jobs can also validate the phase. The phase report remains explicitly IN PROGRESS. Main is not created or advanced until the applicable phase gate is green. This follows the handover's distinction between pushing a phase branch and advancing main.
