# Bound Orchestration to Release 1

**Label:** `wayfinder:grilling`
**Status:** Resolved
**Claim:** harry (branch `harry/rf-bound-orchestration-release-1`, GitHub #15)
**Blocked by:** None

## Question

Should the current orchestration control plane be retained only as a governed Release 1 capability, or reduced to a non-executable scaffold until required real providers and earlier application services are proven—and how will Releases 2-5 remain visibly held?

## Resolution criteria

- [x] Decision recorded against `AGENTS.md` build order and the consolidated roadmap.
- [x] Unavailable releases cannot publish or start runs.
- [x] Portal, API, catalog, and capability/status language expose honest availability.
- [x] Any retained orchestration has concrete Release 1 need and an ADR if required.

## Decision

**Retain as governed Release 1 capability** (`AgentWorkflowReleaseOneBound`):

- **Executable:** Release 1 only — canonical graph `document-to-decision`; agents `{document-checker}`; tools `{documents.read, workflow.evidence-quality}`.
- **Held (visible, not runnable):** system Releases 2–5 remain in `AgentWorkflowCatalog` with honest `[Held — …]` description preface and `AvailabilityStatus = Held`. `StartAsync` rejects them.
- **Publish/save:** tenant graphs may only reference allow-listed agents/tools; out-of-scope nodes fail with clear validation errors.
- **Execute/dispatch:** `EnsureExecutable` on start; `EnsureDispatchAllowed` before queue publish so R2+ surfaces cannot sneak into `agent-workflow`.
- Concrete Release 1 need: Document Checker → evidence quality → human extraction review, matching Bevaya-parity blueprint and AGENTS.md document vertical slice. No separate ADR beyond this recorded decision and the allow-list type documentation (control plane already ADR'd for registry/kill-switch in ADR-0002).

## Working notes

- Claimed 2026-08-03 on `harry/rf-bound-orchestration-release-1` from `origin/main`.
- Seams: publish/save validation; start/execute; queue dispatch; catalog held language.
