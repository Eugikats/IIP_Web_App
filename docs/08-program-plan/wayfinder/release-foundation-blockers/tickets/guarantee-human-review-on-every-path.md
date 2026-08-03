# Guarantee Human Review on Every Consequential Workflow Path

**Label:** `wayfinder:prototype`  
**Status:** Resolved  
**Claim:** harry (branch `harry/rf-human-review-every-path`, GitHub #13)  
**Blocked by:** None

## Question

Which graph invariant and validation algorithm will prove that every executable path containing a consequential or external action reaches the required human-review gate before completion, including branches, cycles, invalid outcomes, and unreachable nodes?

## Resolution criteria

- [x] Invariant documented against the platform human-decision ceiling.
- [x] Validator checks all applicable paths, not existence of one safe path.
- [x] Unsafe alternate completion, branching, cycle, and unreachable-node tests fail publication.
- [x] Canonical Release 1 graph still publishes and passes.

## Decision

**All-paths human-review invariant** (`AgentWorkflowHumanReviewInvariant`): for every non-`HumanReview` node with consequential/external side effect (`ApprovalRequired`, `InternalWrite`, `ExternalSend`, `DraftOnly`), the node must be reachable from Trigger, and every executable path from that node to `Complete` must pass through a `HumanReview` node. Cycles that can reach Complete without review fail. Existence of one safe path is insufficient.

**Platform ceiling** (`ApprovalDecisionGuard.EnsureHumanForConsequentialDecision`): Agent/System/Integration actors cannot finalize consequential Decide/Send/Execute. Wired at approval decide/execute, customer-update decide/send, and agent work-item decide. Actor kind resolved from prefixed user ids (`agent:`, `system:`, `integration:`).

## Working notes

- Claimed 2026-08-03 on `harry/rf-human-review-every-path` from `origin/main`.
- Seams used: publish/save validation; ApprovalDecisionGuard Decide/Send/Execute; Release 1 catalog publish.
