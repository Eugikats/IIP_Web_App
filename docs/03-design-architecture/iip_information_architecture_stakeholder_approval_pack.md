# IIP Information Architecture Stakeholder Approval Pack

| Field | Value |
|---|---|
| Date issued | 2026-08-07 |
| Decision artifact | `iip_information_architecture_navigation_and_screen_inventory.md` v1.0 |
| Scope | Authenticated staff portal `/app/*` information architecture |
| Status | Approval requested; no named approver or decision recorded |
| Research companion | `../research/iip-information-architecture-validation-and-approval-plan.md` |

## Decision requested

Each named function must choose `Approve`, `Approve with conditions`, or `Reject` for IA v1.0. The decision covers the proposed object/findability model, module grouping, route/label authority, role-aware visibility principles, screen/state inventory, Global Search, My Work, Recent Conversations and cross-surface handoffs.

Attendance, silence, implementation progress, repository access or informal chat is not approval. One person may sign for more than one function only when their authority is explicit.

## Approval boundary

Approval means the function accepts IA v1.0 as the controlled baseline for implementation and practitioner validation, subject to recorded conditions. It does **not** establish:

- practitioner-validated terminology, grouping or task findability;
- protected-staging permission correctness;
- accessibility conformance or disabled-user validation;
- production readiness, provider readiness or release approval;
- approval of unimplemented advanced Search, My Work or Recent Conversations behavior.

Those claims require their own evidence. A stakeholder may approve conditionally while keeping IA-OQ-001–006 and protected-runtime gates open.

## Evidence, assumptions, constraints and proposed decisions

| Class | Statement | Approval implication |
|---|---|---|
| Evidence | Canonical routes, route ownership, server permission contracts and current code were reconciled into IA v1.0. | Supports internal consistency, not user validation. |
| Evidence | Global Search, five-source My Work, owner-scoped Recent Conversations and WhatsApp messaging baselines have local source/test evidence. | Protected staging, scale, accessibility and practitioner evidence remain open. |
| Assumption | Work/Records/Specialist work/Automation/Oversight/Platform/Administration matches practitioner mental models. | Must be tested through card sorting/tree testing. |
| Assumption | Canonical labels such as `My Work`, `Claims`, `Channels` and `Policy Guidance` transfer across insurers. | Must be tested across roles and institutions. |
| Constraint | Server-resolved tenant/workspace membership and effective permissions are the authority. | No approval may authorize browser-selected roles as security. |
| Constraint | AI prepares work; accountable humans make consequential insurance decisions. | Labels and flows must never imply autonomous authority. |
| Proposed decision | Adopt IA v1.0 as the controlled baseline for the next validation cycle. | Requires every named function below to decide. |

## Pre-read

1. `iip_information_architecture_navigation_and_screen_inventory.md` v1.0.
2. `iip_platform_ux_behavior_specification.md`.
3. `iip_end_to_end_user_flow_architecture.md`.
4. `iip_recent_conversations_session_continuity_spec.md`.
5. `iip_universal_my_work_aggregation_spec.md`.
6. `iip_whatsapp_thread_outbound_delivery_spec.md`.
7. Release 1 requirements, acceptance criteria, traceability and current protected-staging evidence.

## Function-specific decision questions

| Function | Named decision owner must decide |
|---|---|
| Product | Does the hierarchy support Release 1 outcomes without presenting mounted out-of-scope modules as release commitments? Are labels stable enough for controlled validation? |
| Insurance | Are Claim, Case, Evidence, Work item, Approval, Communication and Conversation meanings operationally safe? Could any label imply the wrong insurance authority or state? |
| Operations | Do My Work, queue ownership, waiting/action states, WhatsApp handoffs, resume behavior and cross-surface return paths fit accountable work management? Which conditions must block adoption? |
| Design/Research | Is the IA testable, internally coherent and ready for neutral card sort/tree test/terminology evaluation? Are current evidence limits visible? |
| Architecture | Do object owners, routes, APIs, data contracts, aliases, source state machines and handoffs have one coherent authority and migration path? |
| Security/Privacy | Do navigation omission, direct-route denial, Search fields/snippets/audit, conversation retention, permission-change recovery and no-existence-leak rules meet the protected boundary? |
| Delivery | Are owners, dependencies, alias removal, validation, defect handling, evidence retention and rollout/reversal gates executable without overstating readiness? |

## Approval criteria

- The exact IA version and linked conditions are stated.
- No known conflict with product scope, domain meaning, server authorization or human authority remains unrecorded.
- Every high-consequence objection is accepted, assigned, or explicitly blocks approval.
- Assumptions remain labelled and have an owner, method and decision rule.
- Search, My Work, Recent Conversations and WhatsApp gaps are approved as conditions or remain blocking.
- Practitioner validation, accessibility and protected-staging gates are not waived by document approval.
- Superseded labels/routes have migration ownership and removal conditions.

## Review meeting

**Recommended duration:** 75 minutes.

1. Five minutes — decision, scope and non-claims.
2. Ten minutes — object model, hierarchy and Release 1 posture.
3. Fifteen minutes — scenario walkthroughs: assigned claim, WhatsApp thread, approval, Search, Recent Conversations and return context.
4. Fifteen minutes — permission, tenant, privacy and adverse-state review.
5. Ten minutes — evidence limits and practitioner-validation plan.
6. Fifteen minutes — objections, conditions, owners and dates.
7. Five minutes — each function states its decision; no group vote substitutes for individual accountability.

## Formal approval record

| Function | Named person and title | Decision | Conditions or required changes | Evidence reviewed | Decision date | Expiry/review trigger |
|---|---|---|---|---|---|---|
| Product | — | Pending | — | — | — | Material scope/evidence change |
| Insurance | — | Pending | — | — | — | Material domain/authority change |
| Operations | — | Pending | — | — | — | Material queue/handoff change |
| Design/Research | — | Pending | — | — | — | Practitioner findings or material IA change |
| Architecture | — | Pending | — | — | — | Route/API/data-contract change |
| Security/Privacy | — | Pending | — | — | — | Permission/search/retention/privacy change |
| Delivery | — | Pending | — | — | — | Deployment/release-plan change |

## Conditions and conflict log

| ID | Raised by | Condition, objection or conflict | Evidence | Severity | Owner | Due/gate | Status |
|---|---|---|---|---|---|---|---|
| IA-APR-001 | — | Named approvers have not been supplied. | Formal record above | Blocking | Product sponsor | Before approval session | Open |
| IA-APR-002 | Research | No practitioner card sort, tree test or terminology corpus exists. | IA v1.0 §12 | Blocking for user-validation claim; condition permitted for internal baseline approval | Product Research/Operations | Before IA-OQ-001 closure | Open |
| IA-APR-003 | Security/Delivery | Protected-staging multi-role verification is blocked because the staging runtime is absent. | `../07-qa/protected-staging-multi-role-verification-2026-08-07.md` | Blocking for release/staging claim | Architecture/Delivery/Security | Before protected IA closure | Open |

## Decision outcome

- **Approved baseline:** all seven named functions approve; conditions remain separately gated. Record the decision in the product decision log and update IA status to `Approved for controlled validation`.
- **Conditional baseline:** no function rejects, every condition has owner/date/gate, and no condition is misreported as closed.
- **Rejected:** any function rejects; IA remains proposed and the conflict log governs revision.
- **Expired:** material scope, route, permission, evidence, retention or workflow change triggers reapproval by affected functions.

No named IA stakeholder approval has been obtained in this repository update.
