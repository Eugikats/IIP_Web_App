# IIP Release 1 State, Exception, and Recovery Contracts

| Field | Value |
|---|---|
| Version | 0.1 |
| Date | 2026-08-07 |
| Status | Source-complete design contract; implementation, accessibility, and protected-runtime verification remain open |
| Scope | Release 1 independently changing regions, exceptions, prevention, and recovery |
| Behavior owner | Product Design |
| Runtime state owner | Owning Domain/Application/provider contract |

## 1. Authority, terminology, and identifiers

This artifact completes the region-level state inventory and recovery behavior for the existing Release 1 flows and actions. It does not create domain states, retry values, session duration, permissions, audit events, or provider policy.

- Region: `REG-R1-{SURFACE}-{REGION}`.
- Existing UI state: `ST-R1-{SURFACE}-{CODE}` from `iip_release_1_task_flow_exception_state_catalog.md`.
- Existing exception: `EX-R1-{FLOW-CODE}-{NN}`.
- Recovery parity gap: `REC-GAP-{NN}`.

“Succession” in this segment is interpreted as **confirmed success followed by the next valid task or terminal state**. `SUCCESS` and `RECOVERY` remain separate. A request accepted, button pressed, provider call started, or approval recorded is not automatically the final successor outcome.

## 2. Shared state semantics and exits

Every state answers: what happened, what is available, what is preserved, who owns the next action, and how the user exits safely.

| Requested state | Canonical code(s) | Entry condition | Required presentation and preservation | Valid exits |
|---|---|---|---|---|
| Loading | `LOADING` | Required initial data or authoritative state is unresolved | Structural skeleton or named stage; no fake rows/actions; preserve surrounding context; affected region `aria-busy` | `POPULATED`, `EMPTY`, `PARTIAL`, permission state, provider state, `TIMEOUT`, or `FAILED` |
| Empty | `EMPTY` | Authoritative query succeeded with zero permitted items | Explain what is empty and why when known; retain filters; show valid next action; never imply restricted records do not exist | Create/import where authorized, clear filters, return, or remain terminal valid zero state |
| Partial | `PARTIAL` | Some independently owned data/operations succeeded and others did not | Show available and missing regions/items, source/owner, freshness, preserved work, and scoped recovery; no whole-screen blanking | Retry missing scope, continue only if safe, escalate, or full current state |
| Error | `FAILED` plus `TIMEOUT` where applicable | Operation rejected, failed, or exhausted; timeout remains distinct if effect is unknown | Specific safe explanation, failed scope, preserved input/state, possible-effect disclosure, recovery owner, correlation reference where useful | Correct, safe retry, reconcile, reload, escalate, replace, compensate, or terminal exit |
| Permission restricted | `UNAUTHORIZED`, `RESTRICTED`, or read-only presentation | Current server authority denies all access, limits contextual disclosure, or prevents action | Remove protected content/actions; do not leak existence/counts; explain only where legitimate context exists; preserve authorized committed work | Return to permitted route, reauthenticate, request access, or continue read-only |
| Unavailable | `UNAVAILABLE` | Capability is configured but dependency/runtime is currently unhealthy or degraded | Name affected capability and safe alternative; retain current data/draft; distinguish from configuration absence and terminal failure | Automatic/manual retry under policy, recovery, alternate path, escalation, or outage resolution |
| Not configured | `NOT-CONFIGURED` | Required provider/capability/policy is absent or intentionally inactive | Exact missing capability, configuration owner, blocked action, preserved work; never simulate or offer futile retry | Authorized configuration/change approval, alternate approved path, or terminal blocked exit |
| Success | `SUCCESS` | Authoritative domain/provider owner confirms the exact outcome | Exact object/outcome, time/version/reference where material, what happens next, and route to resulting record/work; no generic “Done” | Successor task/state, stable terminal completion, or later separately initiated correction/compensation |
| Recovery | `RECOVERY`, with `RETRYING` where active | Authorized retry, repair, replay, reconciliation, compensation, re-evaluation, or escalation has begun | Recovery type/scope/owner, attempt/stage, preserved state, cancellation boundary, and expected terminal meanings | Confirmed success, failed/manual intervention, safe cancellation, or controlled blocked state |

### Supporting states

| Code | Contract |
|---|---|
| `POPULATED` | Current permitted data is available; actions still require current prerequisites and server authority. |
| `STALE` | Displayed/source/derived version is no longer current or freshness is unacceptable; block unsafe action and identify refresh/review impact. |
| `CONFLICT` | A concurrent or contradictory current state prevents safe overwrite; preserve input and present compare/reload/reapply. |
| `TIMEOUT` | Completion was not confirmed in time. State is failed only when the owner proves no effect; otherwise it is unknown/reconciliation. |
| `RETRYING` | One bounded retry/replay is in progress using the required stable identity; duplicate user action remains disabled. |
| `INTERRUPTED` | User, network, device, or session left before task completion; committed/durable state survives and unsafe local assumptions do not. |
| `REAUTHENTICATED` | Identity and authority were re-resolved; current evidence/version is compared before resume. |
| `HIGH-VOLUME` | Pagination/virtualization/filtering preserves count meaning, selection scope, comparison, focus, and return context. |

No nonterminal state may be a dead end. Each has at least one user action, system action, owner escalation, or explicit terminal blocked exit.

## 3. Independently changing region inventory

| ID | Task/flow and surface | Actor/entry point | Data/system states | Permission and variants | Requirement/dependency | Priority/status |
|---|---|---|---|---|---|---|
| `REG-R1-SHELL-AUTH` | `FLW-R1-AUTH-01`; shell/workspace entry | Workforce user; sign-in/deep link/resume | Loading, permitted, no membership, wrong workspace, expired, reauthenticated, unavailable | Server identity/membership; wide/narrow, keyboard/touch | R1-FUN-001, R1-FUN-016, R1-SEC-001 | P0 / Partial |
| `REG-R1-MYWORK-FILTERS` | `FLW-R1-QUEUE-01`; My Work filters | Staff; queue entry/return | Default, selected, loading, stale saved filter, restricted options, high volume | Source permissions; URL/session-safe persistence | R1-FUN-003, R1-DAT-008 | P0 / Proposed |
| `REG-R1-MYWORK-RESULTS` | `FLW-R1-QUEUE-01`; work results | Claims/support roles; filtered query | Loading, empty, populated, partial, stale row, restricted, timeout, error, high volume | Source/object access; table/list/narrow | R1-FUN-003, R1-SEC-002, R1-UX-001 | P0 / Partial |
| `REG-R1-CASE-HEADER` | Case workspace; identity/state/next action | Claims worker; queue/search/deep link | Loading, populated, stale, conflict, restricted, unavailable | Case read/action permission; returning/resumed | R1-FUN-004, R1-DAT-004 | P0 / Proposed |
| `REG-R1-CASE-TASKS` | Queue/decision flows; tasks/approvals | Claims worker/supervisor | Empty, populated, partial, stale, conflict, unauthorized, success/recovery | Source action permission; high volume | R1-FUN-003, R1-FUN-011–013 | P0 / Partial |
| `REG-R1-CASE-TIMELINE` | Case history/audit projection | Claims/Compliance; case open | Loading, empty, populated, partial, restricted, high volume | Case/audit scope; print/zoom/narrow | R1-FUN-004, R1-FUN-015 | P1 / Proposed |
| `REG-R1-EVIDENCE-LIST` | `FLW-R1-EVIDENCE-01`; evidence inventory | Reviewer; case/document entry | Loading, empty, populated, partial, stale, restricted, unavailable, high volume | Document/case access | R1-FUN-005–009, R1-DAT-009 | P0 / Proposed |
| `REG-R1-EVIDENCE-PREVIEW` | `SUR-R1-EVIDENCE`; source preview | Reviewer; select evidence | Loading, populated, partial, corrupt/unsafe, restricted, unavailable, error | Content stream authority; wide/split/narrow route | R1-FUN-005–007, R1-PRI-001 | P0 / Partial |
| `REG-R1-EVIDENCE-FIELDS` | Extraction/correction editor | Reviewer; processed document | Loading, empty extraction, partial, low confidence, stale, conflict, error, success/recovery | `UploadDocument` current coarse guard; keyboard/zoom/localized | R1-FUN-006–009, R1-A11Y-001–004 | P0 / Partial |
| `REG-R1-EVIDENCE-DEPENDENCIES` | Invalidation/re-evaluation status | Reviewer/system; correction commit | Empty, populated, stale, retrying, partial, failed, success | System invariant; authorized visibility | R1-FUN-009, R1-DAT-003 | P0 / Partial |
| `REG-R1-APPROVAL-PROPOSAL` | `FLW-R1-DECISION-01`; proposed action | Approver; review item | Loading, populated, partial evidence, stale, expired, restricted | Approval read/object access; no narrow final decision | R1-FUN-010–012, R1-UX-002 | P0 / Partial |
| `REG-R1-APPROVAL-EVIDENCE` | Approval evidence/limits | Approver; proposal review | Loading, populated, partial, stale, conflicting, restricted, unavailable | Evidence permissions; split/sequential | R1-FUN-010–013, R1-DAT-009 | P0 / Partial |
| `REG-R1-APPROVAL-DECISION` | Human reason/action/outcome | Authorized approver; after review | Default, disabled, loading, stale, conflict, expired, unauthorized, success, recovery | Exact action permission/SoD; desktop high-risk | R1-FUN-011–013, R1-SEC-005 | P0 / Partial |
| `REG-R1-CHANNEL-THREAD` | `FLW-R1-COMMS-01`; linked conversation | Customer Service/Claims; channel entry | Loading, empty, populated, partial, stale link, restricted, unavailable, high volume | `ViewChannels` plus case scope; low bandwidth | R1-FUN-014, R1-WA-001–004 | P0 / Partial |
| `REG-R1-CHANNEL-DRAFT` | Draft/preflight/review | Authorized communicator; message need | Empty, drafting, saved, partial, stale, conflict, permission, not configured, error, approved | Communication permissions; keyboard/touch/localized | R1-FUN-014, R1-UX-001 | P0 / Partial |
| `REG-R1-CHANNEL-DELIVERY` | Attempt/provider reconciliation | Sender/system; approved send | Loading/attempting, accepted, delivered, read, failed, timeout/unknown, retrying, recovery | `SendCustomerUpdate`; provider callback | R1-FUN-013–014, R1-DAT-006 | P0 / Partial |
| `REG-R1-PROVIDER-HEALTH` | `SUR-R1-STATUS`; capability health | Authorized staff/operator; contextual status | Loading, not configured, ready, unavailable, degraded, failed, recovery | Role-safe diagnostics; no raw errors | R1-SEC-004, R1-RES-004 | P0 / Partial |
| `REG-R1-DIALOG-ACTION` | Confirmation/cancellation overlay | Authorized actor; high-consequence trigger | Default, disabled, loading, stale, conflict, permission, provider, error, success handoff | Parent action authority; focus/touch/reduced motion | R1-FUN-012–015, R1-A11Y-002/004 | P0 / Partial |
| `REG-R1-CONVERSATION-HISTORY` | Reopen/archive/restore work context | Conversation owner; sidebar/all conversations | Loading, empty, populated, stale history, conflict, restricted, interrupted/resumed | Owner/scope/version; narrow/list/search | R1-FUN-016, R1-DAT-005 | P1 / Partial |
| `REG-R1-GLOBAL-STATUS` | Environment/outage/session status | All permitted users; system trigger | Offline, degraded, unavailable, session expiring/expired, recovery, restored | No data leakage; keyboard/screen reader/reduced motion | R1-SEC-004, R1-RES-001–004 | P0 / Proposed |

`Partial` here means some source/components/tests exist but the complete region-state contract is not locally and protected-runtime verified. It is not an implementation percentage.

## 4. State transition and succession rules

1. Initial unresolved region enters `LOADING`, then exactly one of populated, empty, partial, permission, provider, timeout, or failed state.
2. A populated region may become `STALE` or `CONFLICT` without clearing visible prior context; unsafe actions disable immediately.
3. A user correction or authorized retry enters `RECOVERY`/`RETRYING`; the original failure and attempt history remain traceable.
4. `SUCCESS` requires authoritative confirmation. It names the successor task/state and preserves a route to the resulting record.
5. External operations transition pending → confirmed success/failure or unknown. Unknown never transitions directly to retry; reconciliation intervenes.
6. Session recovery transitions interrupted/expired → reauthenticated → current authority/evidence comparison → resumed, restricted, stale, or conflict state.
7. Terminal rejection, quarantine, absent configuration, or denied authority still provides an owner or safe route out; it is not presented as generic error.

## 5. Confirmation policy

| Risk/reversibility | Pattern | Required content |
|---|---|---|
| Frequent and reversible | Direct action with immediate feedback; optional undo if domain supports it | Action/object and changed state |
| Material but correctable before external effect | Inline review or short confirmation | Target, effect, current value/version, preserved draft, cancel |
| Rare, consequential, external, disclosure, destructive, override, export, execution, or restart | Dedicated governed dialog/embedded confirmation | Exact action, affected person/object, evidence/limits, authority, consequence/reversibility, target/provider, reason, audit notice, explicit confirm/cancel |
| Ambiguous or stale prerequisite | No confirmation; block and route to compare/review/reconciliation | What changed/unknown, preserved work, next safe action |

- Confirmation must not compensate for a preventable design error.
- Consequential outcome is never preselected, defaulted from AI, or grouped into a generic “Confirm.”
- Repeated confirmation is avoided when one exact current confirmation safely covers one action; a changed target/evidence/version invalidates it.
- Type-to-confirm is not a Release 1 default and requires a separately justified extreme-risk case.

## 6. Cancellation, undo, rollback, and compensation

| Boundary | Behavior |
|---|---|
| Before submit/commit | Cancel closes the action, preserves approved draft policy, changes no business state, and returns focus/context. |
| Local preparation/upload before server durability | Cancel stops supported client work/request; selected files/draft remain or clear according to explicit copy; never claim server cancellation without acknowledgement. |
| Durable internal work queued/running | Cancel requires an approved cancellable state and records cancellation; already committed source/audit remains. If unsupported, show `Cannot cancel after processing started`. |
| External effect pending/unknown | Do not offer generic cancel or resend. Reconcile provider/target; user may leave while status remains owned. |
| Confirmed reversible metadata action | Offer undo only within the domain's real reversal contract; record both transitions where auditable. |
| Confirmed consequential/external action | No destructive undo. Create an explicit authorized compensating/corrective action linked to the original. |
| System/config rollback | Restore approved code/config/rule/model version while preserving incident, affected-work, and decision history; re-evaluate affected artifacts. |

Closing a dialog, browser tab, or device never implies cancellation of a durable or external operation.

## 7. Duplicate prevention and idempotence

Prevention is layered:

1. **Control:** disable the same submit while pending and retain a stable local attempt identity across safe retry/resume.
2. **Request:** send a server-recognized idempotency/correlation key for important writes where the contract supports it.
3. **Domain:** enforce uniqueness/current-state invariants and return the prior deterministic result for the same logical action where applicable.
4. **Queue/provider:** stable message/provider identity, monotonic event handling, deduplication, bounded retry, and poison-work routing.
5. **Reconciliation:** if effect is unknown, query/observe the authoritative target before another attempt.

Duplicate detection outcomes are:

- **Exact duplicate:** link/no-op and show the existing result/reference.
- **Replayed same intent:** return the existing result using the same idempotency identity.
- **Near duplicate/ambiguous match:** hold for human comparison; do not merge or create silently.
- **Distinct justified item:** create with a new identity and recorded reason/source relationship.

UI disabling alone is never idempotence. A generated key is scoped to one logical intent; editing target, content, evidence, or consequence requires a new reviewed intent and key.

## 8. Interruption, session expiry, and resume

| Trigger | Preserved | Discarded/invalidated | Resume contract |
|---|---|---|---|
| Navigation/panel close | Committed state, approved safe draft, filters/selection/scroll where safe | Ephemeral hover, unsafe uncommitted secrets/content | Restore context; re-fetch current data before action |
| Browser/device/network interruption | Durable source/work/attempt IDs and approved draft contract | Optimistic success, expired upload selection, unpersisted restricted content | Show interrupted/current operation state; resume/restart only as supported; no automatic submit |
| Session expiry before submit | Safe draft and work pointer | Prior permission assumption and sensitive rendered content | Reauthenticate; resolve tenant/workspace/permission; reload latest; compare; user intentionally submits |
| Session expiry during internal pending work | Durable operation continues under its authorized server context where valid | Browser ownership of completion | Reauthenticate; query operation by safe pointer; show confirmed current state |
| Session expiry during external ambiguity | Pending/idempotency/provider reference and approved intent | Any assumption of failure or success | Reauthenticate; reconcile first; never resend automatically |
| Permission revoked | Authorized committed work and audit | Restricted content/actions, stale client permission | Remove restricted region, deny submit, return to permitted route or admin request |

Session warning thresholds and draft retention duration are insurer/security decisions required; this specification does not invent minutes or retention periods.

## 9. Stale data and conflict

| Condition | Prevention/detection | Presentation | Recovery |
|---|---|---|---|
| Display freshness exceeded | Source timestamp/rule/provider freshness policy | `STALE` cue with affected action disabled where material | Refresh/retrieve current source; retain reading position |
| Expected version mismatch | Optimistic concurrency token/current-state guard | Compare current server value with preserved user draft | Accept current, reapply intentionally, or abandon draft |
| Evidence changed after derived work | Dependency lineage/invalidation | Proposal/task/approval marked stale; affected dependencies listed | Re-evaluate, re-review, and create new approval as required |
| Concurrent assignment/approval | Atomic expected state | Winning actor/outcome, time, and current owner/state; no overwrite | Reload; choose another valid action/escalation |
| Conflicting sources | Evidence relationship/status | Show both sources, provenance, freshness, and restriction; no silent winner | Qualified human resolves, requests evidence, or escalates |
| Provider callbacks reordered | Stable provider ID and monotonic state machine | Keep highest confirmed valid state; log ignored duplicate safely | Reconcile if sequence remains ambiguous |

Refresh never silently discards a draft. “Last write wins” is prohibited for evidence correction, approval, assignment, communication approval/send, and provider outcome.

## 10. Error and recovery matrix

| Trigger/cause | Consequence | Prevention/detection | Required message and preserved state | User/system recovery | Retry/rollback/escalation/audit |
|---|---|---|---|---|---|
| Invalid input | Action cannot safely start | Field/schema/client+server constraints | Identify field/rule; preserve all valid input | Correct and resubmit | New attempt; validation telemetry, audit only if required |
| Unauthorized/cross-tenant | Protected action/data denied | Per-request scope/object/action checks | Safe denial without existence/content leak; retain only permitted context | Return/reauth/request access | No automatic retry; security telemetry/audit as policy |
| Provider not configured | Capability absent | Startup/health/preflight | `Not configured`, blocked action, owner; preserve source/draft | Configure under authority or approved alternate path | No futile retry; configuration audit |
| Provider unavailable | Configured capability unhealthy | Health/timeout/circuit/bounded retry | Affected operation, preserved source/draft, retry ownership | Wait, bounded retry, alternate manual path, escalate | Same run identity; DLQ/manual intervention |
| Timeout before proven effect | No confirmed outcome | Pending record before call | State unknown unless target proves no effect | Reconcile target/provider | Retry only after no-effect proof; correlation/audit |
| Duplicate/replay | Possible duplicate record/effect | Idempotency/domain uniqueness/provider ID | Existing result/reference or ambiguous comparison; preserve submitted intent | Accept existing, compare, or create distinct justified item | No repeat effect; dedup audit/telemetry |
| Upload corrupt/unsafe | Evidence cannot enter processing | Format/size/safety validation/quarantine | Safe file reason; other files/results preserved | Replace or security process | No extraction; quarantine/audit gap tracked |
| Stale evidence/version | Derived/decision action unsafe | Freshness/version/dependency checks | Changed source/version and blocked effect; preserve draft/reason | Refresh, compare, re-evaluate/reapprove | Old artifact remains immutable/audited |
| Concurrent conflict | Overwrite/duplicate decision risk | Expected version/atomic state | Current winner/state plus preserved input | Reload/reapply or choose alternate action | New deliberate attempt; conflict telemetry/audit |
| Partial batch/region failure | Some work succeeded | Item/region ownership and response detail | Succeeded/failed scope and preserved selection/filter | Retry failed safe subset or continue if allowed | Same per-item identities; no successful-item replay |
| Session expiry/interruption | Client loses authority/context | Session/network detection and durable pointers | What is committed/pending/draft and sign-in/resume path | Reauthenticate, reload, compare, intentional resume | Never replay submit; security/session telemetry |
| Retry exhausted/poison work | Automated progress stops | Bounded attempts and failure classification | Work/run/failure owner; customer-safe surface remains nontechnical | Operator fixes cause and replays under authority | DLQ/manual replay/audit |
| Confirmed effect needs correction | Original cannot be erased | Review and immutable history | Original result, correction need, consequence | New corrective/compensating action and approval | Linked audit; rollback only for approved technical config |
| Human-gate bypass/systemic harm | Unsafe continued scale | Invariant/monitoring/incident trigger | Affected capability stopped, scope/owner/restart conditions | Investigate, identify affected work/people, remedy | Controlled restart only; incident/audit/notification |

## 11. Action-specific prevention and recovery obligations

| Action | Confirmation | Cancellation boundary | Duplicate/idempotence | Interruption/stale/conflict | Recovery/successor |
|---|---|---|---|---|---|
| `ACT-R1-AUTH-01` | None for sign-in; explicit workspace choice when multiple | Leave before scope established | No business submit replay | Reauthenticate and resolve current scope | Permitted destination or safe denial/admin route |
| `ACT-R1-CASE-01` | Review minimum data/ambiguous match | Before case commit | Stable intake identity; exact duplicate links/no-op | Preserve input; reload match/current records | Open created/existing case or manual-match queue |
| `ACT-R1-QUEUE-01` | Confirm target for reassignment where material | Before assignment commit | Expected version prevents repeat/overwrite | Preserve queue context; show winning owner | Current assignee/queue or deliberate reassign/escalate |
| `ACT-R1-QUEUE-02` | Destination/reason/effect | Before return/escalation commit | Expected state and one transition | Preserve reason/context; expired destination reload | Recipient-owned work or safe blocked queue |
| `ACT-R1-DOC-01` | File/target review; no modal for routine upload | Before durable upload/approved processing boundary | Hash/source/provider identity; per-file outcome | Non-resumable upload restarts honestly; retained source not duplicated | Stored source → processing/review; replace/quarantine/manual path |
| `ACT-R1-DOC-02` | Type/reason/downstream impact | Before reclassification commit | Expected version | Preserve choice/reason; compare changed document | Current classification and re-evaluation |
| `ACT-R1-DOC-03` | Original/new/source/reason/dependants | Before correction commit | Expected version; idempotent identical correction | Preserve entered value; compare/reapply | Reviewed value; descendants stale then re-evaluated |
| `ACT-R1-DOC-04` | Exact extraction version/scope | Before approval commit | Atomic current review state | Stale/conflict blocks; comments retained | Approved extraction or return/escalation |
| `ACT-R1-EVD-01` | None for read-only evaluation | Cancel queued retry only where supported | Stable run/rule/evidence version | Correction invalidates result; retry bounded | Current checklist or owned gap/manual review |
| `ACT-R1-ASSESSOR-01` | Recipient/disclosure/channel/manifest-version/due-expiry | Before external disclosure; cancellation cannot revoke an already transmitted email attachment | Logical referral plus immutable pack version/hash and linked attempt identity | Preserve pack/attempt; Graph unknown reconciles before retry; expired/late/mismatched return requires owned review or reissue | Exact-version acknowledgement, matched reviewed report/revision, or explicit expired/failed/unknown escalation |
| `ACT-R1-APR-01` | Exact proposed action/evidence/authority | Before request commit | Proposal/evidence/workflow version | Stale proposal blocks; rebuild request | Pending eligible-human review |
| `ACT-R1-APR-02` | Required distinct governed confirmation | Before immutable decision commit | Atomic expected approval state | Preserve reason; show winning/expired/revoked state | Immutable outcome and valid next task/recourse |
| `ACT-R1-APR-03` | Exact approved target/effect/provider | Before attempt; none while ambiguous | Stable execution identity | Reauthenticate/reconcile; stale approval blocks | Confirmed effect, failure/manual intervention, or compensation |
| `ACT-R1-COMMS-01` | No high-friction confirm for draft | Cancel/edit draft per approved storage contract | Draft version | Preserve safe draft; current facts invalidate/refresh | Review-ready draft or discarded draft |
| `ACT-R1-COMMS-02` | Exact recipient/content/basis/version | Before approval commit | Atomic draft/version | Preserve reason; stale content returns to review | Approved exact content, not sent |
| `ACT-R1-COMMS-03` | Exact recipient/content/provider/consent | Before provider attempt; not after unknown | Stable idempotency key and pending record | Preserve intent; reauthenticate then reconcile | Accepted/delivered/read/failed/unknown; no blind resend |
| `ACT-R1-COMMS-04` | None for automatic reconciliation | Stop polling only, not provider effect | Provider message/event ID and monotonic state | Resume from durable attempt/reference | Confirmed delivery state or owned manual intervention |
| `ACT-R1-HANDOFF-01` | Scope/recipient/purpose/version | Before export generation/disclosure | Export request/version identity | Preserve scope; stale evidence requires refreshed export | Immutable export/download or partial/failure retry scope |
| `ACT-R1-CONV-01` | Archive confirmation only when recovery/retention warrants | Before metadata commit | Expected conversation version | Preserve title/filter; compare conflict; reauthorize reopen | Renamed/archived/restored history with current-evidence warning |
| `ACT-R1-OPS-01` | Exact capability/scope/affected work/restart consequence | Stop is immediate/fail-closed; restart cancellable before activation | Stable incident/control identity | Session loss does not clear stop; conflict shows current state | Durable stopped state → investigation/remedy → controlled restart |

## 12. Message, audit, telemetry, and notification rules

Error and recovery copy states:

1. what happened in user language;
2. affected object/action;
3. what was preserved;
4. whether an external effect may have occurred;
5. what the user can do now;
6. who owns resolution if self-recovery is unavailable;
7. a safe correlation/reference when useful.

Never expose secrets, prompts, raw provider errors, restricted evidence, internal fraud/risk signals, another tenant's existence, or blame-oriented language.

Audit records committed sensitive reads/actions, reasons, before/after, evidence/version, human outcome, and correlation. Telemetry records safe operational classification, latency, retries, deduplication, conflicts, interruption, recovery, and failure owner. Notifications direct attention to owned work but never replace domain state, audit, or recovery status.

## 13. Current recovery parity gaps

| Gap | Evidence | Required resolution |
|---|---|---|
| `REC-GAP-01` | Shared `States` components do not yet cover partial, unavailable, `Not configured`, timeout/unknown, success succession, and recovery with consistent semantics/actions. | Compose the shared state contract and screen-reader/browser tests. |
| `REC-GAP-02` | Upload source handles offline/progress/error but not supported cancellation boundary, per-file duplicate outcome, resumability, or explicit safe retry control. | Reconcile `ACT-R1-DOC-01` with API/provider behavior and R1-RES-001/002. |
| `REC-GAP-03` | Dialog/drawer source does not establish draft-loss prevention, safe-close boundary, focus recovery, or durable-operation-on-close messaging. | Implement cancellation/interruption contract before relying on overlays for governed work. |
| `REC-GAP-04` | Idempotency exists for selected queue/WhatsApp paths, but every critical write is not yet proven against duplicate/reordered/resumed submit. | Complete action-level request/domain/provider idempotency matrix and tests. |
| `REC-GAP-05` | Conversation optimistic conflict exists, but consistent compare/reapply behavior is not established for evidence, assignment, approval, drafts, and exports in the portal. | Implement shared stale/conflict recovery pattern backed by expected-version contracts. |
| `REC-GAP-06` | Session storage/auth source exists, but protected session-expiry recovery with permission revocation, pending effects, focus/context, and no replay is unverified. | Run authenticated local/protected interruption and reauthentication journeys. |
| `REC-GAP-07` | Provider `Not configured` and unavailable states appear across surfaces with mixed components/copy and recovery affordances. | Centralize capability-state mapping while retaining provider-specific ownership. |
| `REC-GAP-08` | Success feedback sometimes uses transient toast/labels without proving authoritative confirmation, persistent successor state, and accessible announcement. | Adopt `SUCCESS`/succession contract and correlate UI, API, provider, audit, and resulting record. |

## 14. Verification scenarios

- Each region independently exercises loading, empty, populated, partial, error, permission/restricted, unavailable, `Not configured`, success/succession, and recovery where applicable.
- Confirmation friction matches consequence and changed evidence/version invalidates prior confirmation.
- Cancellation before/after each durability/external-effect boundary preserves correct state and never lies about cancellation.
- Duplicate click, browser retry, reordered message, lost response, worker replay, and resumed session create at most one confirmed effect.
- Offline/30-second interruption, tab close, route change, and session expiry preserve committed work and never replay a submit.
- Stale evidence, optimistic version mismatch, concurrent assignment, and concurrent approval preserve input and expose compare/reapply or safe alternate action.
- Provider absence, outage, timeout-before-effect, timeout-after-effect, retry exhaustion, and poison work reach distinct owned states.
- Recovery status is keyboard/touch operable and announced once with useful context; focus returns to the task-appropriate region.
- Protected tests record identity, tenant/workspace, versions, provider state, correlation, audit/telemetry, expected/actual result, defect, and retest.

Documentation completeness does not establish accessibility conformance, practitioner success, provider readiness, or protected-runtime completion.
