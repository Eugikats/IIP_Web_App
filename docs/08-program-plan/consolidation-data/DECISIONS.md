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
