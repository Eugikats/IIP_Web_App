# Implement Immutable Workflow Version Lineage

**Label:** `wayfinder:prototype`  
**Status:** In progress (PR open)  
**Claim:** HarryGafa  
**Blocked by:** None

## Question

What persistence, API, and publication model provides immutable successive versions of one tenant workflow key while ensuring every run remains pinned to the exact published definition it started with?

## Resolution criteria

- Identity and uniqueness include stable workflow key plus version.
- Draft-new-version and publish transitions are explicit and audited.
- Existing runs remain pinned to immutable historical definitions.
- Migration, repositories, DTOs, portal behavior, and concurrency tests agree.

## Implementation notes (RF-03)

- Uniqueness: `(TenantId, WorkspaceId, WorkflowKey, Version)`.
- `POST .../draft-new-version` and `POST .../publish` are audited; published rows are immutable.
- Runs pin `DefinitionId` + `WorkflowVersion`; Complete/Decide resolve by id (not latest-by-key).
- Portal run view loads `/versions/{workflowVersion}`.
