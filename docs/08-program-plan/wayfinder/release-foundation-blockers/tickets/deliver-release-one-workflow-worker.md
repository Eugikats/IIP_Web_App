# Deliver the Release 1 Workflow Execution Worker

**Label:** `wayfinder:prototype`  
**Status:** In progress  
**Claim:** harry/rf-invalidate-downstream-after-corrections  
**Blocked by:** Guarantee Human Review on Every Consequential Workflow Path; Invalidate and Re-evaluate Downstream Workflow State; Implement Immutable Workflow Version Lineage; Bound Orchestration to Release 1

## Question

What tenant-scoped Functions or Durable Functions worker contract will consume `agent-workflow` messages, re-resolve authority, invoke only allow-listed real Release 1 tools, write bounded results and audit, handle retries/dead-letter/idempotency, and stop safely at human review?

## Resolution criteria

- Worker and message contract implemented with managed identity and server-resolved scope.
- Duplicate delivery, stale version, malformed envelope, unavailable provider, retry, dead-letter, and human-review-stop behavior tested.
- No raw document/OCR payload crosses the extraction boundary.
- Dev deployment and telemetry prove one real bounded execution path.

## Progress notes

- Added `AgentWorkflowNodeExecute` Azure Function on `ServiceBus:AgentWorkflowQueue` and worker contract `agent-workflow.node.execute.v1`.
- Worker re-resolves tenant authority per message using `IPermissionReadRepository.GetEffectiveMembershipAsync` and configured runtime principal `ServiceBus:AgentWorkflowWorkerUserId`.
- Added duplicate/stale/human-review-stop guards and strict malformed-envelope validation.
- Added bounded executor for Release 1 allow-list only (`document-checker`, `workflow.evidence-quality`) with metadata-only evidence (no raw OCR/bytes).
- Added contract tests in `AgentWorkflowExecutionWorkerContractTests` for malformed, duplicate, stale version, provider-unavailable (retry), and human-review-stop.
- Manual blocker remains: dev/staging deployed telemetry evidence has not been produced in this coding session.
