# IIP Release 1 Stakeholder Approval Pack

| Field | Value |
|---|---|
| Date | 2026-08-05 |
| Baseline | `iip_release_1_requirements_baseline.md` v0.10 |
| Scope | `iip_mvp_scope_control_register.md` v2.1 |
| Acceptance | `iip_mvp_acceptance_criteria_register.md` v2.1 |
| Status | Approval requested; no signatures recorded |

## Decision requested

Each function must choose `Approve`, `Approve with conditions`, or `Reject` for the exact versions above. Silence, attendance, a chat response, prior document approval or continued implementation is not approval.

## Review packet

- Product definition/validation charter and August 2026 research program/evidence limits.
- Requirements baseline and scope register.
- Acceptance and traceability registers.
- API/data contract map and current route/OpenAPI evidence.
- Algorithmic impact assessment and risk/decision registers.
- End-to-end flow and UX behavior/design artifacts.
- IA v1.0 and `../03-design-architecture/iip_information_architecture_stakeholder_approval_pack.md`; IA approval and practitioner validation remain separate gates.
- RF-10 go/no-go evidence and current blockers.

## Function-specific questions

| Function | Must decide |
|---|---|
| Product | Problem/outcome, Release 1 boundary, mandatory WhatsApp outcome, priorities, evidence status, success/kill rules and excluded scope |
| Insurance | Claim terminology, human authority, categories/exclusions, evidence/completeness, approvals, recourse and severe-error tolerance |
| Operations | Roles, queues, handoffs, WhatsApp intake/outbound ownership, workload, SLAs, interruption/recovery, support/escalation and change management |
| Security/Privacy | Tenant/permission model, WhatsApp consent/legal basis/provider data flow, data inventory/lifecycle, cross-border/deployment/support access, untrusted input, audit, incident and residual risk |
| Architecture | Boundaries, source of truth, WhatsApp webhook/outbound/status contracts, API/data contracts, state models, idempotency/reconciliation, performance, recovery, migration and operability |
| Delivery | Dependencies, estimation readiness, environments, evidence collection, UAT/pilot/support plan, release gates and accountable owners |

## Approval criteria

- No unresolved conflict with `AGENTS.md` or product authority.
- Every P0 requirement is understandable, atomic, testable and mapped.
- Conditional insurer values have a named owner and blocking gate.
- Deferred scope and consequences are accepted.
- High/Critical risks and algorithmic residual risk have named owners and conditions.
- Performance, resilience, accessibility, usability and multilingual thresholds are accepted as targets or explicitly changed with rationale/evidence.
- No approval relies on simulated protected-runtime behavior or unexecuted evidence.

## Formal record

| Function | Named person/title | Decision | Conditions/required changes | Evidence reviewed | Date | Expiry/review trigger |
|---|---|---|---|---|---|---|
| Product | — | Pending | — | — | — | — |
| Insurance | — | Pending | — | — | — | — |
| Operations | — | Pending | — | — | — | — |
| Security/Privacy | — | Pending | — | — | — | — |
| Architecture | — | Pending | — | — | — | — |
| Delivery | — | Pending | — | — | — | — |

## Decision outcomes

- All approve: baseline may be marked approved/frozen for the stated decision horizon; conditional client gates remain.
- Conditional approval: record owner/date and keep affected gate blocked until verified.
- Any rejection: baseline remains proposed; log conflict, evidence and required revision.
- Material evidence or scope change: increment versions and reapprove affected functions.

No formal approval has been obtained in this repository update.
