# IIP Release 1 Surface and Control Interaction Contracts

| Field | Value |
|---|---|
| Version | 0.2 |
| Date | 2026-08-07 |
| Status | Source-complete design contract; implementation reconciliation and accessibility verification remain open |
| Scope | Release 1 operational controls and coordinated surfaces |
| Behavior owner | Product Design |
| Visual-token owner | `iip_visual_design_system_and_tokens.md` |
| Domain/runtime authority | Current Domain/Application/API contracts |
| Complex-pattern companion | `iip_release_1_complex_operational_pattern_contracts.md` |

## 1. Authority and identifiers

This artifact owns the Release 1 task-level contract across controls and coordinated surface regions. It composes existing patterns and tokens; it does not create routes, permissions, audit actions, insurer policy, or domain states.

- Surface contract: `SUR-R1-{SURFACE}`.
- Reusable control contract: `CTL-R1-{CONTROL}`.
- Surface/control parity gap: `SUR-GAP-{NN}`.
- Action purpose and authority remain owned by `iip_release_1_action_purpose_human_ai_authority_contracts.md`.
- Flow/exception/state IDs remain owned by `iip_release_1_task_flow_exception_state_catalog.md`.
- Visual values remain owned by `iip_visual_design_system_and_tokens.md`.

If this contract conflicts with current runtime behavior, record a parity gap. Do not weaken a human gate, permission, evidence rule, or safe failure to match the UI.

## 2. Universal control-state contract

Every microinteraction performs one task stated as a verb–noun sentence, such as “Upload document,” “Approve update,” or “Retry extraction.” A control that performs two effects must be split or use an explicit review step.

| State | Trigger and rule | Visual/text feedback | Semantics, focus, and input | Transition/exit |
|---|---|---|---|---|
| `default` | Action is available and prerequisites currently appear satisfied | Persistent verb-first label; consequence or current value where useful; icon is secondary | Native semantic control; 44px target for primary touch workflows; no hover-only information | Hover/focus/pressed or submit |
| `hover` | Fine pointer enters an enabled control | Static background/border/text change using canonical hover tokens; cursor where conventional | Must not reveal required instructions or alter layout; touch has no hover dependency | Default on leave; pressed on activation |
| `focus` | Keyboard/programmatic focus reaches control | Canonical `2px color.focus.inner` + `2px color.focus.outer`, `1px` offset; never obscured | `:focus-visible`; logical order; label and description programmatically associated | Pressed/submit or next focus target |
| `pressed` | Pointer/key activation begins | `color.action.primary.active` or equivalent state; no scale/translation; acknowledge in under `0.1s` | Native pressed behavior; `aria-pressed` only for a persistent toggle, never a momentary button | Loading for async action; success for confirmed sync action; default if cancelled |
| `disabled` | Action cannot be attempted safely or is unavailable | Stable disabled appearance plus persistent nearby reason and valid prerequisite/owner | Native `disabled` when it must not receive focus; use `aria-disabled` only when focus is needed to explain availability; tooltip alone is insufficient | Re-evaluate when prerequisite/permission/provider/state changes |
| `loading` | One request is pending or a controlled action is running | Preserve control width; progressive verb (`Uploading…`, `Submitting…`); compact spinner only where motion is allowed; named stage/progress nearby | Disable duplicate activation; `aria-busy` on affected region; one polite status announcement, not repeated spinner announcements | Confirmed success, explicit error/unknown, safe cancel, or interruption |
| `success` | Authoritative domain/provider owner confirms the outcome | Exact outcome, affected object, and next action near the region; low-risk transient toast only when no later reference is needed | Polite status announcement; do not move focus unless the task advances to a new view; never rely on green/icon alone | Stable resulting state; control returns only if action remains valid |
| `error` | Request is rejected, fails, conflicts, times out, or returns unknown | What happened, what was preserved, whether an effect may have occurred, owner, correlation reference where useful, and next safe action | Error linked to control/field/region; alert only when immediate attention is necessary; preserve input and focus context | Correct, reload/compare, retry if safe, reconcile, escalate, or terminal exit |
| `retry` | Failure is classified retryable and retry scope is known | Explicit `Retry {task}` label, attempt/scope, preserved state, and whether same idempotency identity is reused | Available only to an authorized actor/system policy; never offered for ambiguous external effect before reconciliation | Loading with bounded attempt; success/error/manual intervention |
| `reduced-motion` | `prefers-reduced-motion: reduce` or equivalent user setting | Instant state replacement or opacity only; no translation, scaling, lift, parallax, pulsing, or decorative spin | Text and semantic progress remain complete; static progress icon may replace spinner | Same domain transition and timing thresholds as other modes |

### Additional states

| State | Rule |
|---|---|
| `selected` | Use position/control plus text/icon and `aria-selected`, `aria-current`, or checked state as appropriate. Hover is never selection. |
| `read-only` | Display readable current value and reason; do not present disabled form controls as static data. |
| `stale/conflict` | Block unsafe submit, preserve draft/selection, identify changed version, and offer compare/reload/reapply. |
| `permission/restricted` | Reveal only what policy permits; explain unavailable action when the user has legitimate context; browser state is not authority. |
| `not-configured` | Name absent capability and configuration owner; never offer a retry that cannot succeed until configuration changes. |

## 3. Timing, motion, sound, and loops

| Concern | Contract |
|---|---|
| Activation acknowledgement | Under `0.1s`; pressed/pending state uses the existing control rather than an unrelated toast. |
| Busy indication | If work exceeds `1s`, show a labelled busy state or named stage. |
| Long operation | If work exceeds `10s`, show progress when measurable and a safe cancel/background-navigation option when the runtime supports it. |
| Motion tokens | `instant 0ms`, `fast 80ms`, `standard 140ms`, `deliberate 200ms`, `slow 240ms` maximum; standard easing `cubic-bezier(.2,0,0,1)`, exit `cubic-bezier(.4,0,1,1)`. |
| Motion purpose | Communicate causality, spatial entry/exit, or hierarchy only; motion is interruptible and never signals evidence quality, urgency, or success alone. |
| Sound/haptic | None by default for Release 1. If later introduced, optional and never the sole feedback channel. |
| Repeated use | Preserve safe filters, density, panel size, and draft context; never learn/preselect consequential decisions, recipients, consent, disclosure, or overrides. |
| Modes | Avoid persistent invisible modes. Editing, selection, and comparison modes require visible label, scope, exit, and Escape/cancel behavior. |

## 4. Reusable control contracts

| ID | Single task | Rules and variants | Feedback/recovery | Accessibility/adaptation |
|---|---|---|---|---|
| `CTL-R1-ACTION` | Activate one named action | Primary once per region; secondary/tertiary for alternatives; destructive/authority styling follows consequence, never AI provenance | In-control pending then regional authoritative outcome; disabled reason persistent | Native button; visible focus; 44px touch where primary; no icon-only consequence |
| `CTL-R1-ICON` | Activate one familiar utility action | Use only for conventional close, reveal, copy, expand, or navigation; accessible name required | Tooltip may supplement but not replace name; pressed/selected state persistent where applicable | 44px touch target; title alone insufficient; forced-colors boundary |
| `CTL-R1-LINK` | Navigate to one resource or record | Link navigates; button changes state. Row link and row actions remain separate | Preserve origin/filter/selection return context; visited treatment must not imply business state | Native link, descriptive destination, keyboard and screen-reader context |
| `CTL-R1-FIELD` | Enter or correct one value | Persistent label/instructions; forgiving format; source/provenance/read-only distinction; expected-version save | Inline validation preserves value; save/saved/error near field group; conflict supports compare/reapply | Associated label, description/error IDs, autocomplete/input mode, zoom/reflow |
| `CTL-R1-CHOICE` | Select one or more explicit options | Radio for one, checkbox for many, switch only for immediate binary setting; no switch for consequential submission | Current selection and downstream consequence visible before commit | Native grouping/legend; checked state non-color; adequate target |
| `CTL-R1-MENU` | Choose one secondary action from a bounded list | Trigger shows menu purpose; destructive/consequential items remain explicit and may open review | Opening does not perform action; close on Escape/outside; return focus to trigger | Menu semantics only for application-style commands; ordinary action list otherwise |
| `CTL-R1-UPLOAD` | Submit selected files to one authorized target | Show target, file rules, selected names/count/size; validate client and server; source retention precedes extraction | Per-file progress/result; duplicate/quarantine/error; cancel only before supported commit boundary | File input remains operable; progress semantics/live updates; touch/camera alternative |
| `CTL-R1-RETRY` | Re-attempt one classified safe operation | Names task and scope; bounded; same idempotency/run identity when required; hidden for terminal/unknown effect | Attempt count and stage; success/error/manual owner | Button label contains retry target; status announcement does not repeat excessively |
| `CTL-R1-CANCEL` | Stop one reversible pending/draft action | Names what stops and what remains; disabled after irreversible boundary; compensation is a separate action | Confirm cancelled state and preserved work; return focus/context | Escape maps to cancel only where safe; never silently discard user input |

## 5. Coordinated surface registry

| ID | Repeated task | Primary components | State owner | Narrow/touch adaptation |
|---|---|---|---|---|
| `SUR-R1-CUES` | Understand provenance, status, priority, restriction, and required attention | Text label, icon/shape, value, source/freshness, optional action | Owning domain/provider state; cue is presentation only | Wrap without truncating meaning; no hover dependency |
| `SUR-R1-EVIDENCE` | Compare source and derived values, correct, and establish review state | Source preview, evidence list, field grid, version/status, correction editor, dependencies, action bar | Source/blob + document/evidence review contract; draft local until commit | Sequential source/value relationship retaining position, source link, field, draft, reason and currentness; equivalent traceable correction remains available |
| `SUR-R1-APPROVAL` | Independently review and record one exact human outcome | Proposal, affected party/object, consequence, evidence/limits, authority/expiry, reason, decision controls, audit/outcome | Approval service; UI selection is never outcome | Sequential full-page review with decision region last; equivalent actions become available only after current prerequisites, never removed merely by viewport |
| `SUR-R1-DIALOG` | Confirm one short high-consequence action or bounded choice | Heading, target/effect, evidence summary, reason/input, cancel, exact confirm, outcome handoff | Parent surface draft + domain action on submit | Near-full-screen on narrow; remains one task; long review becomes route/panel |
| `SUR-R1-PANEL` | Preserve supporting context or short secondary work beside the primary task | Named heading, status, content, local actions, close/collapse, optional footer | Source owner for data; UI owns open/size/scroll only | Reflow, drawer, sheet, or route; state and focus preserved |
| `SUR-R1-TABLE` | Scan, compare, filter, sort, select, and open high-volume work | Caption/heading, filters, result count, column headers, rows, selection, row actions, pagination, partial-state summary | Query source owns data; URL/session may own safe filters; selection is UI state | Priority table or alternate semantic list; essential horizontal scroll only |
| `SUR-R1-STATUS` | Understand progress, completion, failure, provider state, and recovery ownership | State label, icon, concise explanation, object/stage, timestamp/freshness, next action/correlation | Domain/provider owner; status region renders, never invents | Full-width inline region; concise announcements; details progressively disclosed |

## 6. Surface behavior contracts

### `SUR-R1-CUES` — cues and attention indicators

- Distinguish workflow state, approval state, evidence eligibility, provider state, risk/priority, confidence, and AI provenance; never reuse one color/shape family as if meanings were interchangeable.
- Every cue uses readable text plus icon/shape/position where helpful. Color is reinforcement only.
- AI provenance is visually secondary to evidence and human-reviewed status. Confidence always includes reason/source quality and cannot appear as business certainty.
- A cue is not a control unless it has an explicit action label and keyboard semantics. Tooltip content must also be available on focus and in persistent detail where required.
- `ATT-0/1` cues remain inline. `ATT-2` cues identify owner/next action. `ATT-3` cues sit beside the decision control. `ATT-4` uses a persistent blocking status region for affected operators.
- Repeated cues collapse only when the summary preserves count, highest consequence, and access to every item.

### `SUR-R1-EVIDENCE` — evidence review

1. Opening a document selects one source and updates preview/fields without moving focus unexpectedly.
2. Preview and extracted value remain simultaneously comparable on wide layouts; narrow layouts use a source/fields switch that retains page, zoom, selected field, draft, and reading position.
3. Selecting a field highlights the corresponding source location where available; an equivalent textual source reference is always present.
4. Each value exposes source, extraction/reviewer status, version/freshness, confidence/limitation, and eligibility without relying on highlight color.
5. Edit creates a local draft. Save uses expected version and reason where required. Success confirms the reviewed value; conflict preserves draft and offers compare/reapply.
6. Correction shows affected downstream artifacts and moves them to stale/re-evaluating before any further execution.
7. Approve extraction is a separate `ATT-2` action; its label states that it approves extraction only.
8. Loading/partial/provider failure affects only the owned region; the source remains available when safely retained.

Selection owner: UI for current document/field; source system for document/review status. Filters and current field may persist for the session; unsaved sensitive values do not persist outside an approved draft contract.

### `SUR-R1-APPROVAL` — approval cockpit

1. Scan order is exact action → affected party/object → consequence/reversibility → current evidence/exclusions → AI/rule proposal and uncertainty → authority/expiry → alternatives → reason → decision controls.
2. AI recommendation is visually and semantically secondary. No decision is preselected, learned, or emphasized because it matches AI output.
3. Approve, reject, override, return, and escalate are distinct controls with distinct consequence text. Unavailable decisions remain absent or disabled with a legitimate-context reason derived from the endpoint contract.
4. Evidence can be inspected without losing the reason draft or current place. Material evidence/version change blocks controls and presents compare/reload.
5. Submission opens a proportionate `SUR-R1-DIALOG` or embedded confirmation that repeats exact target/effect and current version; confirmation reauthorizes server-side.
6. During submission all competing decision controls are disabled. Success is the immutable recorded outcome; API acceptance is not external execution success.
7. Concurrent, expired, mismatched, or revoked-authority outcomes preserve comments and return the reviewer to a current comparison state.
8. Bulk consequential approval is prohibited. Queue navigation appears only after a recorded or safe-exit outcome.

### `SUR-R1-DIALOG` — dialogs

- Use only for one short blocking confirmation/choice. Evidence browsing, long forms, and complex correction use a panel or route.
- On open, save invoker and background scroll. Initial focus goes to a programmatically focusable heading/summary for consequential review, otherwise the first required field; destructive confirm is never initial focus.
- Trap focus within the dialog; Escape/backdrop closes only before submission and only when it cannot discard unsaved work. Close/cancel labels remain explicit.
- Heading, description, field errors, target/effect, audit notice, and disabled reason are programmatically associated.
- Submit enters `aria-busy`, disables duplicate submit and unsafe close, and keeps the control label width stable. If safe cancellation is supported, expose it as a distinct control.
- Error remains in the dialog with reason/input preserved. Success closes only after the parent surface has a confirmed persistent outcome; focus returns to invoker or moves to the resulting record heading when the invoker no longer exists.
- Reduced motion replaces scale/translation with instant/opacity transition; focus timing does not depend on animation completion.

### `SUR-R1-PANEL` — panels, rails, and drawers

- A persistent panel is a named region; an evidence rail is complementary; a modal drawer uses dialog semantics. Do not apply `aria-modal` to non-modal persistent context.
- Open/collapse state, width, scroll position, selection, and safe drafts persist across responsive transformation. Source data is revalidated independently.
- User-initiated modal drawer moves focus to its heading/content and traps it; closing returns focus to trigger. A non-modal panel does not steal focus when data refreshes.
- Collapse never hides the only status, required action, error, or unsaved-state warning. The collapsed trigger carries count/state when useful.
- Independently changing regions own their loading/partial/error/retry state. One failed panel does not blank the whole workspace.
- Narrow layouts promote supporting panels to full-height sheet/route with the same heading, state, and return context.

### `SUR-R1-TABLE` — tables and work queues

- Use a table for repeated comparable records; heterogeneous narrative work uses a list. Provide visible heading/caption and result count.
- Filters/sort/pagination update the query and announce result count once. Preserve filters in URL/session when safe; never persist restricted query content.
- Sort controls are buttons in headers with current direction. Selection uses explicit checkboxes and a scope summary (`3 selected on this page`); row hover is not selection.
- Row primary navigation and row action controls are separate. Opening a record preserves queue order, filters, page, and return focus.
- Bulk actions state exact scope and exclusions. Bulk consequential decisions are prohibited. Partial bulk failure reports succeeded/failed items and retry scope.
- Loading uses structural skeletons without fake data. Empty preserves filters and offers valid next action. Partial identifies missing source. Stale row blocks unsafe action. Unauthorized rows do not leak existence.
- High volume uses server pagination or approved virtualization while maintaining headers, focus, selection, and position. Infinite scroll alone is insufficient for operational queues.
- Narrow view uses a priority table or semantic list preserving identifier, state, owner, next action, freshness, and SLA/attention cue.

### `SUR-R1-STATUS` — status regions

- Place status beside the affected object/action. Use a global banner only for environment-wide or cross-surface impact.
- Routine progress/completion uses `role="status"`/polite live behavior. Blocking validation stays associated with fields. Immediate material failure may use `role="alert"`; do not announce every polling update.
- Deduplicate repeated provider/workflow updates and announce only meaningful state transitions. Maintain a visible current state after any toast disappears.
- `Not configured`, unavailable, timeout, failed, unknown, and permission denied are separate states with different owners and recovery.
- Success means confirmed by the authoritative domain/provider. Provider accepted, delivered, and read remain distinct.
- Status contains state text, affected object/stage, what is preserved, timestamp/freshness where material, and next action/owner. Raw provider errors, secrets, and restricted data are excluded.
- Reduced motion replaces spinning/pulsing with static icon/text or native progress semantics; live updates continue.

## 7. Coordinated state matrix

| Surface | Required state coordination |
|---|---|
| `SUR-R1-CUES` | `POPULATED`, `STALE`, `CONFLICT`, `RESTRICTED`, `NOT-CONFIGURED`, `UNAVAILABLE`, `SUCCESS`; cue never becomes sole state explanation |
| `SUR-R1-EVIDENCE` | All state-registry codes; preview, fields, correction, and dependencies may be independently loading/partial/error while source authority remains explicit |
| `SUR-R1-APPROVAL` | `LOADING`, `POPULATED`, `PARTIAL`, `STALE`, `CONFLICT`, `UNAUTHORIZED`, `RESTRICTED`, `TIMEOUT`, `FAILED`, `SUCCESS`, `INTERRUPTED`, `REAUTHENTICATED`, `RECOVERY` |
| `SUR-R1-DIALOG` | `POPULATED`, `LOADING`, `STALE`, `CONFLICT`, `UNAUTHORIZED`, `NOT-CONFIGURED`, `UNAVAILABLE`, `TIMEOUT`, `FAILED`, `SUCCESS`, `INTERRUPTED`; parent owns durable outcome |
| `SUR-R1-PANEL` | Every independently changing panel has `LOADING`, `EMPTY`, `POPULATED`, `PARTIAL`, `STALE`, permission, provider, error, success, interruption, and recovery behavior |
| `SUR-R1-TABLE` | `LOADING`, `EMPTY`, `POPULATED`, `PARTIAL`, `STALE`, `CONFLICT`, `UNAUTHORIZED`, `RESTRICTED`, `UNAVAILABLE`, `TIMEOUT`, `FAILED`, `SUCCESS`, `HIGH-VOLUME` |
| `SUR-R1-STATUS` | Renders all state-registry codes but never owns business state; maps each to correct persistence, announcement priority, owner, and next action |

## 8. Action-to-surface/control coverage

| Action | Primary surface | Trigger/control | Confirmation and outcome region |
|---|---|---|---|
| `ACT-R1-AUTH-01` | `SUR-R1-PANEL`/`STATUS` | `CTL-R1-ACTION` | Reauthentication route; current-work status |
| `ACT-R1-CASE-01` | `SUR-R1-PANEL` | `CTL-R1-ACTION` + fields | Duplicate/creation region; dialog only if ambiguity requires choice |
| `ACT-R1-QUEUE-01` | `SUR-R1-TABLE` | row action/menu | Assignment dialog or inline editor; row/queue outcome |
| `ACT-R1-QUEUE-02` | `SUR-R1-TABLE`/`PANEL` | `CTL-R1-ACTION` | Reason/destination dialog; queue status |
| `ACT-R1-DOC-01` | `SUR-R1-EVIDENCE` | `CTL-R1-UPLOAD` | Per-file/status region |
| `ACT-R1-DOC-02` | `SUR-R1-EVIDENCE` | `CTL-R1-CHOICE` + action | Review confirmation; evidence status |
| `ACT-R1-DOC-03` | `SUR-R1-EVIDENCE` | `CTL-R1-FIELD` + action | Correction confirmation; field/dependency status |
| `ACT-R1-DOC-04` | `SUR-R1-EVIDENCE` | `CTL-R1-ACTION` | Exact-scope dialog; review status |
| `ACT-R1-EVD-01` | `SUR-R1-EVIDENCE`/`CUES` | system trigger + retry where safe | Completeness/status region |
| `ACT-R1-ASSESSOR-01` | `SUR-R1-PANEL` | `CTL-R1-ACTION` | Disclosure dialog; handoff status |
| `ACT-R1-APR-01` | `SUR-R1-APPROVAL` | `CTL-R1-ACTION` | Proposal status/queue |
| `ACT-R1-APR-02` | `SUR-R1-APPROVAL` | distinct decision controls | `SUR-R1-DIALOG`; immutable outcome region |
| `ACT-R1-APR-03` | `SUR-R1-APPROVAL`/`STATUS` | `CTL-R1-ACTION` | Execution dialog; provider/domain outcome |
| `ACT-R1-COMMS-01` | `SUR-R1-PANEL` | fields + draft action | Save status; no send confirmation |
| `ACT-R1-COMMS-02` | `SUR-R1-PANEL` | `CTL-R1-ACTION` | `SUR-R1-DIALOG`; approved-content status |
| `ACT-R1-COMMS-03` | `SUR-R1-PANEL` | `CTL-R1-ACTION` | `SUR-R1-DIALOG`; provider attempt/status |
| `ACT-R1-COMMS-04` | `SUR-R1-STATUS` | system trigger/`CTL-R1-RETRY` after reconciliation | Persistent delivery/reconciliation status |
| `ACT-R1-HANDOFF-01` | `SUR-R1-PANEL` | `CTL-R1-ACTION` | Scope dialog; export status/download link |
| `ACT-R1-CONV-01` | `SUR-R1-PANEL`/`TABLE` | row/menu action | Inline lifecycle status; destructive archive confirmation as required |
| `ACT-R1-OPS-01` | `SUR-R1-PANEL`/`STATUS` | authority/destructive action | `SUR-R1-DIALOG`; incident/stopped status |

## 9. Current implementation parity gaps

| Gap | Current source evidence | Required reconciliation |
|---|---|---|
| `SUR-GAP-01` | `GovernedButton` uses gradient/glow primary treatment and does not itself demonstrate the canonical focus-ring contract. | Move to semantic solid action tokens and prove focus-visible/forced-colors states. |
| `SUR-GAP-02` | `SourceCitationList` and `CustomerUpdatePanel` translate/lift on hover despite the canonical static-hover rule. | Remove translation and preserve state/affordance with border/background only. |
| `SUR-GAP-03` | `LoadingState`, `ErrorState`, and upload feedback do not consistently expose status/alert/progress semantics or deduplicated announcements. | Implement shared `SUR-R1-STATUS` semantics and browser/screen-reader tests. |
| `SUR-GAP-04` | `DataTable` currently supplies only a wrapper/table label; sort, selection, filter, pagination, partial-state, and high-volume coordination are surface-specific. | Introduce composed table contracts without inventing a new data authority. |
| `SUR-GAP-05` | `GovernedActionDialog` and `SideDrawer` handle Escape/scroll lock but do not establish initial focus, focus trap, invoker return, safe-close boundary, or reduced-motion behavior. | Reconcile overlay primitives with `SUR-R1-DIALOG`/`PANEL` and verify keyboard/screen reader. |
| `SUR-GAP-06` | `ApprovalDecisionPanel` has busy/disabled states but no complete governed confirmation, error association/outcome announcement, and decision availability defaults permissive when omitted. | Derive exact actions from current endpoint state and compose the approval/dialog/status contracts. |
| `SUR-GAP-07` | `DocumentUploadWidget` shows visual progress but lacks progress semantics, per-file results, supported cancel boundary, and reduced-motion alternative. | Implement `CTL-R1-UPLOAD` and provider/duplicate/retry tests. |
| `SUR-GAP-08` | Control styling/state logic is duplicated across legacy and Lovable-parity components. | Migrate coherently to shared contracts with visual regression; do not mass-replace or preserve harmful legacy behavior. |

## 10. Analytics and verification

Measure task outcomes, not decorative interaction counts: completion/return/escalation, preserved-draft recovery, correction/conflict rate, duplicate prevention, provider ambiguity/reconciliation, time in focused review, decision disagreement, abandoned dialog with reason, and accessibility failure. Never score staff performance from override, dwell time, correction, or assistive-technology use.

Minimum tests:

- every control state with mouse, keyboard, touch, zoom, forced colors, and reduced motion;
- focus entry/trap/return for dialogs and modal drawers;
- evidence selection, correction conflict, invalidation, and narrow source/field switching;
- approval stale/concurrent/permission/expiry paths and absence of preselected/bulk outcomes;
- tables with empty/loading/partial/high-volume, sort/filter/pagination, selection scope, row navigation, and return focus;
- status announcement deduplication and distinction among `Not configured`, unavailable, timeout, failed, unknown, provider accepted, delivered, and read;
- duplicate submit, retry eligibility, safe cancellation boundary, interruption, and reauthentication;
- visual regression against canonical tokens at approved viewports.

No accessibility conformance, practitioner preference, protected-runtime completion, or component implementation parity is claimed by this documentation artifact.

The complete independently changing region inventory and exception/recovery behavior are governed by `iip_release_1_state_exception_recovery_contracts.md`.
