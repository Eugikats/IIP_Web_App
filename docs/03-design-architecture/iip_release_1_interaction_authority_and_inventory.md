# IIP Release 1 Interaction Authority and Inventory

| Field | Value |
|---|---|
| Version | 0.3 |
| Date | 2026-08-10 |
| Status | Governed interaction coordination baseline; detailed contracts and protected verification remain gated |
| Scope | Release 1 non-injury motor own-damage and document-intelligence interactions |
| Accountable owner | Product Design |
| Contributors | Product, Domain/Application, Frontend, Security, Compliance, QA, Operations |

## 1. Objective and authority

This artifact is the Release 1 interaction control plane. It identifies every critical human or externally visible system action, assigns the owning specification and runtime contract, records evidence strength, and exposes design-to-runtime parity without redefining detailed flows, routes, permissions, audit events, or insurer policy.

Authority order for an interaction is:

1. repository invariants in `AGENTS.md` and `IIP_IMPLEMENTATION_GUARDRAILS.md`;
2. current Domain/Application rules, server authorization, API contracts, persistence, audit factories, and tests;
3. approved requirements and decisions;
4. the platform UX behavior specification for reusable observable behavior;
5. the Release 1 task-flow catalog for task transitions, exceptions, and stable state IDs;
6. this inventory for ownership, evidence classification, open decisions, and parity status.

If design and runtime differ, the difference is recorded as a gap. Current code is implementation evidence, not automatic approval of unsafe or accidental behavior. Browser visibility, disabled state, role labels, token claims, and client-supplied scope never grant business authority.

### Stable identifiers

- Interaction: `ACT-R1-{DOMAIN}-{NN}`.
- Interaction parity gap: `INT-GAP-{NN}`.
- Evidence, assumption, constraint, decision: `E-INT-{NN}`, `A-INT-{NN}`, `C-INT-{NN}`, `D-INT-{NN}`.
- Existing flow, state, permission, audit, requirement, acceptance, and UAT IDs remain unchanged.

## 2. Artifact ownership contracts

| Artifact/contract | Sole owning concern | Required inputs | Output/gate | Must not own |
|---|---|---|---|---|
| `iip_platform_ux_behavior_specification.md` | Reusable IIP interaction behavior, feedback, forms, error prevention, recovery, accessibility intent | Product principles, research, platform constraints | Observable behavior is coherent across surfaces | Routes, current enum values, implementation claims |
| This inventory | Critical-action registry, evidence/assumption/decision ledger, ownership, parity status | All owning design/runtime artifacts | Every critical action has one behavior owner and one runtime enforcement seam | Detailed task flow, new route/permission/audit contract |
| `iip_release_1_action_purpose_human_ai_authority_contracts.md` | Action purpose, user intent, feedback, authority class, attention demand, meaningful human oversight, and human/AI boundary | This inventory, AIA, agentic patterns, human-authority invariants | Every critical action communicates why it exists, who controls it, and what AI cannot decide | Control-state styling, routes, permissions, insurer policy, or validation claims |
| `iip_release_1_surface_control_interaction_contracts.md` | Default/hover/focus/pressed/disabled/loading/success/error/retry/reduced-motion control behavior and coordinated surface contracts | Action authority contracts, task states, visual tokens, current components | Every critical action maps to a reusable control and coordinated surface with complete feedback/recovery behavior | Domain authority, routes, permissions, insurer policy, or visual-token values |
| `iip_release_1_interaction_accessibility_and_runtime_parity.md` | Per-action keyboard, focus return, touch, semantics/announcements, contrast, reduced motion, and client/API/provider/concurrency/audit/idempotency reconciliation | All interaction contracts plus current client/runtime source and verification evidence | Accessibility is mandatory on every interaction; source/design/runtime differences remain explicit gaps | New runtime contracts, WCAG conformance claims, or insurer policy |
| `../01-strategy-requirements-roadmap/iip_release_1_interaction_requirements_traceability.md` | One-row-per-action requirement, acceptance, permission, audit, flow/state/screen/API/test/UAT traceability | Interaction contracts, baseline, acceptance register, current runtime enums/actions | No critical interaction is orphaned and missing semantics remain explicit gaps | Duplicate interaction behavior, new runtime permission/audit contracts, or completion claims |
| `iip_release_1_state_exception_recovery_contracts.md` | Independently changing region inventory; loading/empty/partial/error/permission/provider/success/recovery states; confirmation, cancellation, duplicate/idempotency, interruption, stale/conflict, session expiry, and recovery | Task catalog, surface/control contracts, requirements, current runtime evidence | Every region/action has a safe nonterminal exit, preserved-state rule, and recovery owner | New domain states, retry/session values, permissions, or implementation claims |
| `iip_release_1_task_flow_exception_state_catalog.md` | Release 1 transitions, decisions, exceptions, recovery, coordinated patterns, state IDs | Journeys, blueprint, current contracts | Every nonterminal state has safe exit/recovery | Reusable control styling or implementation status |
| `iip_end_to_end_user_flow_architecture.md` | Canonical lifecycle, flow registry, responsibility boundaries | Companion artifacts | One indexed Release 1 lifecycle | Task-level control detail |
| `iip_information_architecture_navigation_and_screen_inventory.md` | Routes, destinations, navigation, screen IDs, route-level states | Route implementation and user tasks | No competing route/screen inventory | Action authority or domain transitions |
| `iip_visual_design_system_and_tokens.md` | Visual tokens, focus/motion grammar, control appearance | Brand and accessibility baseline | Reusable visual rules | Business meaning or permission |
| `iip_component_architecture_document.md` and frontend handoff | Component composition, props, adapters, DTO mapping | Approved behavior plus API contracts | Implementable component boundaries | Product authority or invented fallback |
| `iip_release_1_api_data_contract_map.md` | Current routes, source ownership, write semantics, provider boundary | API/Application/data contracts | Exact current contract and honest readiness | Proposed UI behavior or insurer defaults |
| Domain/Application code | Business state, object/action guards, human gates, idempotency and invalidation rules | Approved requirements and architecture | Server-enforced behavior | Visual feedback |
| `Permission`, `RolePermissionMap`, authorization handlers | Current permission vocabulary and role mapping | Approved security/RBAC decisions | Request-time server authority | UI affordance or inferred insurer authority |
| Audit factories/writers | Current immutable action names and safe event payload | Committed business transition | Correlated audit evidence | User feedback, telemetry, notification |
| Requirements, acceptance register, UAT, RTM | Obligations and verification traceability | Approved interaction deltas | Atomic gates and recorded evidence | Duplicate flow definitions |
| Runtime tests/evidence reports | Actual local/protected results | Versioned build, identity, environment, data | Reproducible expected/actual proof | Design approval or practitioner validation |

Gate: no artifact may silently redefine a route, domain state, permission, role, audit action, source of truth, decision authority, or readiness level owned elsewhere.

## 3. Evidence, assumption, constraint, and decision ledger

| ID | Class | Statement | Source/owner | Confidence and consequence |
|---|---|---|---|---|
| `E-INT-01` | Evidence | AI may prepare work; accountable humans own consequential insurance decisions. | `AGENTS.md`; approval guards/tests | High; invariant |
| `E-INT-02` | Evidence | Release 1 has ten canonical flows and eighteen coordinated state conditions. | Task-flow catalog; documentation validator | High for source completeness, not practitioner validity |
| `E-INT-03` | Evidence | Current action permissions are defined by `Permission` and resolved server-side through role/object/state checks. | Domain authorization source/tests | High for current code; least-privilege approval still open |
| `E-INT-04` | Evidence | Current audit factories cover case, document, workflow, approval, conversation, and WhatsApp actions, with explicit uncovered seams. | Audit source; `AUD-GAP-01`–`05` | High for inspected source |
| `E-INT-05` | Evidence | Governed approval/customer-update dialogs, approval controls, WhatsApp send, and upload controls exist in the portal. | Current frontend source | High for source presence; behavior parity varies |
| `E-INT-06` | Evidence | Selected backend contracts passed 135 tests; portal tests/lint/build and five focused browser checks passed locally. | `../07-qa/release-1-journey-flow-validation-2026-08-07.md` | High for recorded local run only |
| `E-INT-07` | Evidence | A local Chromium diagnostic completed three checks and found failed modal focus containment/return, critical accessible-name/label issues, serious contrast issues, sub-target controls, and incomplete reduced-motion behavior. | `../07-qa/release-1-interaction-accessibility-and-runtime-parity-verification-2026-08-08.md` | High for sampled local fixture pages; not conformance or protected evidence |
| `A-INT-01` | Assumption | The target insurer will use the current role labels and action grouping. | Product/insurer decision pending | Low; cannot authorize protected UAT |
| `A-INT-02` | Assumption | Staff can understand the proposed status, confirmation, and recovery language without additional training. | No participant evidence | Low; requires usability/accessibility study |
| `A-INT-03` | Assumption | Network interruptions are common enough to prioritize preservation and honest retry behavior. | Desk research/design hypothesis | Medium; reversible design priority |
| `C-INT-01` | Constraint | WhatsApp is the mandatory Release 1 customer channel; other inbound/outbound adapters remain conditional. | Approved Release 1 scope | High |
| `C-INT-02` | Constraint | Release 1 stops at preparation/export unless a separately approved core-system write-back exists. | Release 1 scope and blueprint | High |
| `C-INT-03` | Constraint | Missing provider capability is `Not configured`; protected runtimes cannot fall back to mocks. | Repository invariant | High |
| `C-INT-04` | Constraint | No runtime route, enum, schema, permission, or audit-event change is authorized by this documentation pass. | Segment scope | High |
| `D-INT-01` | Decision | The platform UX behavior specification remains the reusable interaction-behavior authority. | Product Design | Approved for this pack |
| `D-INT-02` | Decision | This inventory owns critical-action coverage and parity status, while the task-flow catalog owns detailed transitions and recovery. | Product Design/Architecture | Approved for this pack |
| `D-INT-03` | Decision | Parity is reported by separate design, API, UI, local-test, and protected-test dimensions; no aggregate “implemented” label is allowed. | Product/Engineering/QA | Approved for this pack |
| `D-INT-04` | Decision | A control may be visible only when useful, but every submitted action is reauthorized at the server immediately before its effect. | Security/Product | Repository-aligned invariant |

New evidence must cite a repository path, test result, approved decision, or research record. Uncited statements remain assumptions. One-insurer findings remain institution-specific until replicated.

## 4. Design-to-runtime parity model

Each inventory row uses five independent dimensions:

| Dimension | Values |
|---|---|
| Design | `Aligned`, `Partial`, `Proposed`, `Absent` |
| API/Application | `Current`, `Partial`, `Conditional`, `Absent` |
| UI control | `Current`, `Partial`, `Not found`, `Not applicable` |
| Local evidence | `Passed`, `Partial`, `Not run`, `No test found` |
| Protected evidence | `Passed`, `Failed`, `Blocked`, `Not run` |

`Current` means source exists, not that the behavior is approved, accessible, secure in every path, or production-ready. `Passed` always names its environment and artifact version in the evidence report.

## 5. Critical interaction inventory

| ID | Purpose and actor authority | Prerequisites and transition | Owning flow / runtime seam | Feedback, failure, recovery | Parity: Design / API / UI / Local / Protected |
|---|---|---|---|---|---|
| `ACT-R1-AUTH-01` | Enter or resume authorized work; any workforce user | Valid identity → server-resolved tenant/workspace/permission; recheck current object/version on resume | `FLW-R1-AUTH-01`; auth/tenant resolver | Loading, assigned-workspace choice, safe 401/403, reauthenticate, preserve only safe committed context | Aligned / Current / Partial / Partial / Blocked |
| `ACT-R1-CASE-01` | Create one FNOL/case; authorized intake staff | Valid minimum fields/source; no deterministic duplicate → durable case | `FLW-R1-INTAKE-01`; `POST /api/v1/cases`; `CreateClaim`; `case.created` | Field errors preserve input; duplicate links/manual review; success opens record | Aligned / Current / Partial / Partial / Blocked |
| `ACT-R1-QUEUE-01` | Assign or reassign owned work; authorized source owner/supervisor | Current object, eligible assignee, expected version → changed owner/queue | `FLW-R1-QUEUE-01`; case/workflow assignment routes; `case.assigned`/`workflow.task.assigned` | Pending, conflict/reload, permission denial, explicit new owner; `PERM-GAP-03` | Aligned / Current / Partial / Partial / Blocked |
| `ACT-R1-QUEUE-02` | Return or escalate work with reason; authorized worker/supervisor | Current state, destination, reason → owned next queue/state | `FLW-R1-QUEUE-01`; case escalation/source action; `case.escalated` | Confirm destination, preserve context, conflict/expiry recovery; return audit gap where applicable | Aligned / Partial / Partial / Partial / Blocked |
| `ACT-R1-DOC-01` | Upload evidence; authorized reviewer/intake adapter | Authorized case/thread, supported safe file → source retained before extraction | `FLW-R1-DOC-01`; document POST; `UploadDocument`; `document.uploaded` | Progress, honest offline/error, retry replacement; no false queued success | Aligned / Current / Current / Partial / Blocked |
| `ACT-R1-DOC-02` | Reclassify document; authorized evidence reviewer | Current source/version and reason → new classification/history | `FLW-R1-EVIDENCE-01`; reclassify route; `UploadDocument`; `document.reclassified` | Before/after, reason, conflict/reload, downstream invalidation | Aligned / Current / Partial / Partial / Blocked |
| `ACT-R1-DOC-03` | Correct extracted field; authorized evidence reviewer | Source coordinates/version, new value, reason → versioned correction | `FLW-R1-EVIDENCE-01`; correct-field route; `document.extraction.field_corrected` | Preserve original, show corrected value, conflict and reapply, invalidate descendants | Aligned / Current / Partial / Passed / Blocked |
| `ACT-R1-DOC-04` | Approve extraction for downstream use; authorized reviewer | Complete current review; no unresolved ineligible evidence → approved extraction | `FLW-R1-EVIDENCE-01`; approve-extraction route; `document.extraction.approved` | Block stale/conflict/unauthorized; success exposes next valid task; `PERM-GAP-04` | Aligned / Current / Partial / Partial / Blocked |
| `ACT-R1-EVD-01` | Evaluate completeness; bounded system action viewed by claims staff | Current eligible evidence + approved rule version → derived completeness | `FLW-R1-EVIDENCE-01`; checklist/evaluation; `case.completeness.updated` | Loading/partial/missing/stale; correction triggers rebuild, never claim truth | Aligned / Current / Partial / Passed / Blocked |
| `ACT-R1-ASSESSOR-01` | Release minimal versioned assessor pack and obtain a reviewed report; authorized Claims Officer | Approved recipient/disclosure/manual-or-configured-Graph channel/exact manifest/due-expiry → immutable pack → attempt/acknowledgement → matched return → human review | `FLW-R1-ASSESSOR-01`; DEC-018; no dedicated route; `PERM-GAP-05`; `AUD-GAP-04` | Preview/confirm, export/attestation, Graph accepted/unknown, expiry/late return, duplicate/mismatch, clarification/revision; no delivery or claim-outcome inference | Scope accepted / Conditional configuration / Not implemented / Not run / Blocked |
| `ACT-R1-APR-01` | Create exact approval request; authorized preparer/system | Current evidence/proposal/policy/workflow versions and eligible human role → pending approval | `FLW-R1-DECISION-01`; approval create; `approval.created` | Show purpose/evidence/authority/expiry; stale proposal blocks creation | Aligned / Current / Partial / Passed / Blocked |
| `ACT-R1-APR-02` | Approve, reject, override, or escalate workflow step; authorized human only | Current matched approval, evidence, role, reason → immutable outcome | `FLW-R1-DECISION-01`; approval routes; `ApproveSettlementWorkflowStep`/`OverrideAiRecommendation`; approval audit actions | Busy/disabled, validation, stale/concurrent denial, success and next state; `PERM-GAP-01`/`02` | Aligned / Current / Partial / Passed / Blocked |
| `ACT-R1-APR-03` | Execute an approved next step without changing insurer authority | Current executable approval and target/provider preflight → pending then confirmed/failed effect | `FLW-R1-HANDOFF-01`; execute-next-step; `approval.next_step.executed` | Reauthorize, display pending/unknown, reconcile before retry, compensate explicitly | Aligned / Current / Partial / Passed / Blocked |
| `ACT-R1-COMMS-01` | Prepare customer-safe update; Claims/Customer Service or bounded AI draft | Current confirmed facts, purpose, channel policy → draft only | `FLW-R1-COMMS-01`; draft route; `ManageCustomerCommunications` | Saving/saved/error, preserve draft, distinguish AI-prepared from approved/sent | Aligned / Current / Partial / Partial / Blocked |
| `ACT-R1-COMMS-02` | Approve customer update; authorized human | Current recipient/content/evidence/template/basis → approved draft, not sent | `FLW-R1-COMMS-01`; approve route; governed dialog | Preview exact message and impact; reason required; stale/rejected/permission recovery | Aligned / Current / Current / Partial / Blocked |
| `ACT-R1-COMMS-03` | Send one reviewed WhatsApp/customer update; authorized sender | Exact approved content, recipient/thread, consent/basis, provider configured, idempotency → one attempt | `FLW-R1-COMMS-01`; customer send/WhatsApp message route; `SendCustomerUpdate`; `whatsapp.outbound.attempted` | Disable duplicate submit, pending/provider-accepted/failed/unknown; preserve draft | Aligned / Current / Partial / Passed / Blocked |
| `ACT-R1-COMMS-04` | Reconcile an ambiguous delivery; verified provider/system or authorized operator | Signed callback or provider query and stable message ID → monotonic delivery state | `FLW-R1-COMMS-01`; webhook/service; `whatsapp.delivery.reconciled` | Show current provider truth; no blind resend; manual intervention when unresolved | Aligned / Current / Partial / Passed / Blocked |
| `ACT-R1-HANDOFF-01` | Export evidence/audit pack; authorized Claims/Compliance user | Current permitted scope, purpose, version → immutable export artifact | `FLW-R1-HANDOFF-01`; compliance export; `ViewAuditLog` | Preview/scope, progress, partial/failed export, safe retry; write-back remains conditional | Aligned / Current / Partial / Partial / Blocked |
| `ACT-R1-CONV-01` | Rename, archive, restore, or reopen conversation history; owning user | Current owner/scope/version → metadata lifecycle only; next turn reloads live evidence | `FLW-R1-AUTH-01`; conversation PATCH/subroutes; conversation audit actions | Optimistic conflict, historical label, current evidence warning, focus/route return | Aligned / Current / Current / Passed / Blocked |
| `ACT-R1-OPS-01` | Pause/kill bounded agent execution; authorized admin/operator | Current agent/run, exact management permission and reason → stopped future work | Service blueprint; agent registry/kill switch; `ManageAgents`; kill-switch audit | High-impact confirmation, affected work, durable stopped state, controlled restart | Partial / Current / Partial / Partial / Blocked |

No row marked `Partial`, `Conditional`, `Not found`, `Not run`, or `Blocked` may be cited as end-to-end complete.

## 6. Cross-cutting interaction decisions

| Concern | Governing decision |
|---|---|
| Consequential action | Preview target, effect, evidence, authority, and audit consequence; require a reason; reauthorize on submit; never infer success from UI state. |
| Externally visible action | Persist pending/idempotency/correlation before provider effect; show accepted/delivered/read/failed only when confirmed by the owning system. |
| Duplicate submit | Disable while one request is pending, send stable idempotency where supported, and reconcile ambiguity before another attempt. UI disabling alone is insufficient. |
| Cancellation | Cancel before an irreversible effect; after commitment use an explicit compensating action, never erase audit history. |
| Stale/conflict | Preserve safe input; disclose material change; compare/reload/reapply; stale approvals and derived outputs cannot execute. |
| Offline/interruption | Never claim offline success. Preserve local draft only where approved and safe; reauthenticate and revalidate scope/version before resume. |
| Permission change | Remove restricted content, preserve only authorized committed state, deny stale submit, and provide a safe route back. |
| Accessibility | Keyboard, touch, semantic name/role/state, announcements, focus entry/return, zoom/reflow, and reduced motion require explicit component contracts and browser/manual evidence. Source intent is not conformance. |
| Audit | Audit records committed sensitive reads/actions with safe metadata; telemetry diagnoses operation; notification informs actors. They are not substitutes. |

## 7. Current parity gaps and routing

| Gap | Evidence | Owner and next gate |
|---|---|---|
| `INT-GAP-01` | `GovernedActionDialog` has dialog semantics and Escape handling, but source does not establish initial focus, focus trap, invoker focus return, live outcome announcement, or reduced-motion behavior. | Frontend + Accessibility; component contract then keyboard/screen-reader/manual verification |
| `INT-GAP-02` | `ApprovalDecisionPanel` validates comments and busy state, but allowed decisions default to true and the control itself does not show a full purpose/evidence/authority confirmation or outcome announcement. | Product/Domain/Frontend; exact endpoint-derived eligibility and governed confirmation parity |
| `INT-GAP-03` | `DocumentUploadWidget` shows visual progress/offline/error, but source does not expose progress semantics/live announcements, cancellation, resumability, or duplicate-file outcome. | Frontend/API/Accessibility; upload interaction contract and interruption tests |
| `INT-GAP-04` | WhatsApp send preflight exists, but insurer consent/template/language/provider policy and protected callback evidence remain unapproved. | Product/Client/Integrations/QA; policy decision then protected UAT |
| `INT-GAP-05` | Idempotency/reconciliation exists in selected services, but no platform-wide control-to-request contract proves duplicate-submit behavior for every critical action. | Architecture/API/Frontend; per-action interaction contract |
| `INT-GAP-06` | Autosave, draft ownership, undo/cancel, stale conflict, offline, and expiry behavior are not yet reconciled for every independently changing surface. | Interaction Design; detailed contract/state pass |
| `INT-GAP-07` | Current UI surfaces use mixed feedback mechanisms and do not yet prove one accessible loading/error/success/`Not configured` announcement pattern. | Component System + Accessibility; shared feedback contract and tests |
| `INT-GAP-08` | Current role/permission and audit gaps remain `PERM-GAP-01`–`05` and `AUD-GAP-01`–`05`. | Product/Security/Domain/QA; approved contract decisions and negative/audit tests |
| `INT-GAP-09` | Protected multi-role, real-provider, accessibility, and practitioner evidence is absent or blocked. | Delivery/QA/Research; environment-matched UAT and separate practitioner validation |

## 8. Quality gates and next route

1. **Authority gate:** each action cites one behavior owner, exact runtime seam, permission/gap, audit/gap, and flow/state IDs.
2. **Purpose/authority gate:** `iip_release_1_action_purpose_human_ai_authority_contracts.md` defines purpose, user intent, feedback, authority, attention demand, and human/AI boundary for every inventory row.
3. **Interaction-contract gate:** `microinteraction-design` and `error-prevention-recovery-designer` define control states, prerequisites, feedback timing, cancellation, retry, stale/conflict, interruption, and recovery for every inventory row.
4. **Accessibility gate:** `accessibility-design-auditor` verifies keyboard, touch, semantics, announcements, focus return, reflow, and reduced motion with recorded evidence.
5. **Parity gate:** frontend/API owners reconcile each row; automated tests prove authorized, denied, duplicate, stale, interrupted, provider, success, and recovery paths.
6. **Protected gate:** authenticated role/tenant/provider UAT correlates UI, API, provider, audit, and telemetry. Practitioner validation remains separate.

Next action: remediate `A11Y-GAP-01`–`08` and decide `PAR-GAP-01`–`03`, beginning with the shared modal, status, focus, label, target-size, and reduced-motion primitives used by `ACT-R1-APR-02`, `ACT-R1-COMMS-03`, `ACT-R1-DOC-01`, and `ACT-R1-HANDOFF-01`.
