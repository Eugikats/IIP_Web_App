# IIP Release 1 Complex Operational Pattern Contracts

| Field | Value |
|---|---|
| Version | 0.1 |
| Date | 2026-08-10 |
| Status | Source-complete design contract; rendered-wireframe, implementation, accessibility and practitioner verification pending |
| Scope | Queues, evidence review, approval cockpits, tables, panels and dialogs |
| Surface/control authority | `iip_release_1_surface_control_interaction_contracts.md` |
| State/recovery authority | `iip_release_1_state_exception_recovery_contracts.md` |
| Responsive authority | `iip_release_1_responsive_adaptive_reflow_contract.md` |
| Accessibility authority | `iip_release_1_interaction_accessibility_and_runtime_parity.md` |

## 1. Purpose and authority

These patterns solve repeated operational tasks across coordinated components; they are not reusable arrangements detached from business meaning. They compose existing routes, actions, controls, states and permissions. They do not create queue ranking, SLA, bulk-action, approval, disclosure, retry, session or provider policy.

| Pattern | Stable ID | Repeated user task | Primary surfaces/screens |
|---|---|---|---|
| Operational queue | `CP-R1-QUEUE` | Find, understand, open, hand off and return to owned work | `PAT-R1-MYWORK`; `SUR-R1-TABLE`; `IA-SCR-002/003/032/033` |
| Evidence review | `CP-R1-EVIDENCE` | Compare source and derived data, resolve eligibility, correct and propagate change | `PAT-R1-EVIDENCE`; `SUR-R1-EVIDENCE`; `IA-SCR-005/006` |
| Approval cockpit | `CP-R1-APPROVAL` | Independently review one exact proposal and record an accountable human outcome | `PAT-R1-APPROVAL`; `SUR-R1-APPROVAL`; `IA-SCR-002/005` |
| Data table | `CP-R1-TABLE` | Scan and compare homogeneous records while preserving query and return context | `SUR-R1-TABLE`; list/audit/provider screens |
| Contextual panel | `CP-R1-PANEL` | Inspect or perform bounded secondary work without losing the primary task | `SUR-R1-PANEL`; case/evidence/provider surfaces |
| Governed dialog | `CP-R1-DIALOG` | Confirm one short bounded high-consequence action or choice | `SUR-R1-DIALOG`; consequential actions and configuration |

## 2. Task context and state ownership

| Pattern | Actors/frequency/risk | Authoritative objects | UI-owned state | Server/provider-owned state | Scale/environment |
|---|---|---|---|---|---|
| Queue | Claims Officer/Supervisor/Customer Service/Compliance; frequent; wrong-open and delay risk | Source task, approval, document-review, customer-update and agent-work records | Safe filters, sort, page, density, expanded row, return anchor, selection where allowed | Membership/permission, assignment, source status, SLA/priority facts | Empty to high volume; low bandwidth; interruption common |
| Evidence | Evidence reviewer/Claims Officer; frequent focused review; consequential downstream risk | Original source, extracted field, review/correction, evidence eligibility, dependency lineage | Selected document/page/field, preview zoom/position, unsaved correction/reason under approved draft boundary | Source bytes/hash/version, extraction, review state, currentness, invalidation/re-evaluation | Large documents; split/sequential layouts; provider degradation |
| Approval | Authorized approver/supervisor; lower frequency, high consequence | Exact proposal, evidence/version, authority/expiry, human decision, execution/reconciliation | Viewed sections, evidence position, unsaved reason under approved draft boundary | Eligibility, current evidence, approval state, decision, execution/provider state | No bulk consequential decision; deliberate attention required |
| Table | Any authorized list user; frequent | Query results from owning source | Safe filters/sort/page/density/expanded row/selection | Record/count/permission/currentness | Zero to high volume; variable columns and content length |
| Panel | Workforce user; contextual | Owning record/provider/query data | Open/collapsed mode, size, scroll, selected tab/section, safe draft | Data, permission, action state | Wide inline; tablet sheet; mobile sequential/full-height |
| Dialog | Authorized action initiator; episodic, material | Parent action prerequisites and committed result | Open state, local field/reason before submit, invoker/focus return | Revalidated authority/version, commit/result | One bounded task; modal; no background business action |

Browser visibility, selection and local draft state never grant action authority or prove current data.

## 3. Shared seven-state coordination

| State | Queue/table | Evidence/approval | Panel/dialog | Required exit |
|---|---|---|---|---|
| `LOADING` | Keep shell, caption, filters and column/card structure; no fake rows/count | Load source/proposal/evidence/action regions independently; dependent actions disabled | Preserve invoker/parent context; skeleton actual regions | Populated/current, valid empty, partial, permission or exact error |
| `EMPTY` | Valid zero authorized results with retained filters and permitted next action | No evidence/proposal is an owned blocking condition, not a blank review | Empty panel explains purpose; a confirmation dialog must not open without a target | Clear filters/create where authorized, return, await owned work or safe terminal zero |
| `ERROR` | Failed source/scope and preserved query/rows; timeout/unavailable/not-configured remain exact | Preserve source, draft/reason and committed review; state whether external effect may exist | Keep task and inputs; error associated with action/region; no raw provider detail | Correct, reload/compare, reconcile, safe retry, replace, escalate or cancel |
| `PERMISSION` | Filter before count/match; remove restricted row/content/action without leaking existence | View and edit/decide permissions remain distinct; revoke unsafe controls immediately | Whole pattern closes only if no legitimate context; focus moves to safe explanation/return | Permitted route/read-only, reauthenticate or access-owner path |
| `PARTIAL` | Retain successful sources/rows; name unavailable source and count limitation | Retain available source/evidence and label missing/stale/conflicting regions; block unsafe consequence | Region-local failure does not blank parent/pattern | Retry failed safe scope, continue only where safe, or escalate |
| `SUCCESS` | Current query/record open or confirmed source transition; successor/return path shown | Exact reviewed version or immutable human outcome, distinct from execution/provider success | Dialog closes only after parent exposes durable result; panel updates owned region | Resulting record/task, next owned work or stable terminal state |
| `RECOVERY` | Restore filters/position; reconcile source/assignment conflict without replay | Reauthenticate, compare, reapply, re-evaluate or reconsider with exact versions | Preserve invoker, draft and recovery owner/attempt; suppress duplicate activation | Confirmed success, safe failure/manual intervention or deliberate cancel |

`STALE`, `CONFLICT`, `UNAVAILABLE`, `NOT-CONFIGURED`, `TIMEOUT`, `INTERRUPTED` and `REAUTHENTICATED` retain their canonical meanings inside these seven low-fidelity variants.

## 4. `CP-R1-QUEUE` — operational queues

### Purpose and anatomy

Purpose: let an authorized worker find the right owned work, understand why it requires attention, open it without losing queue context, and complete/return/escalate through the source-owned action.

Anatomy in reading order:

1. heading, queue purpose and freshness;
2. source/type tabs or filters, search, saved view where approved;
3. active-filter/sort summary and authorized result count;
4. partial/restriction/stale summary;
5. source-typed rows/cards: object/reference, required outcome, owner/queue, status/currentness, evidence readiness, SLA/priority basis where authoritative, unread/change cue, next action;
6. pagination/load-more and recovery/status region;
7. return-focus anchor after record work.

### Task flow and behavior

1. Entry resolves current tenant/workspace, role and per-source visibility before querying.
2. Initial query loads sources independently; one failed source produces partial, not an empty queue.
3. Filter/sort/search updates a stable URL/session-safe query. Restricted query content is not persisted.
4. Row open rechecks source/object permission and currentness. The queue stores only return context.
5. Assignment, reassignment, return or escalation occurs through the source contract using expected version; queue projection follows the committed source result.
6. Returning restores filter, sort, page, scroll and focus to the logical row. If the row left the queue, focus goes to the next row or result heading with a safe change explanation.

### Filtering, sorting, pagination and selection

- Product/Operations owns ranking and SLA policy; the pattern exposes only approved fields/basis.
- Default sort must be explicit and stable. Equal values use a deterministic non-sensitive tie-breaker.
- Filter options expose only permitted values/counts. “No authorized matches” must not distinguish hidden records.
- Pagination is server-owned for high volume; infinite loading requires equivalent position/count and keyboard behavior. Prefer explicit pages/load-more for operational return context.
- Selection is optional UI state for benign comparison/export only when separately authorized. No generic queue bulk action exists.
- Bulk consequential approval, rejection, disclosure, communication or override is prohibited. A future non-consequential bulk action must state exact scope, exclusions, per-item result, idempotency and partial recovery.

### Adaptation and accessibility

- Desktop uses a priority table; tablet hides secondary fields behind row detail; mobile uses semantic work cards.
- Identity, required outcome, owner/state, currentness/risk basis and next action never disappear.
- One interactive row label/link opens the work; secondary actions are separate controls. The row itself is not a nested click target.
- Keyboard supports filter → results → row action → pagination; result count/status is announced once; loading does not reset focus.
- Touch actions meet target/spacing requirements; hover reveals no unique information.

### Analytics and tests

Goal metrics: time to correct work, direct open rate, wrong-record opens, return-context success, stale/vanished rows, reassignment conflicts, filter reformulation, partial-source frequency and recovery success. Do not rank staff performance from queue clicks or time alone.

| ID | Scenario | Expected result |
|---|---|---|
| `CP-TST-QUEUE-01` | Empty, filtered-empty, restricted-only and one-source-failed queries | Meanings remain distinct without count/existence leak; valid recovery exists |
| `CP-TST-QUEUE-02` | Open row, change source state/permission, then return | Current access rechecked; queue context restored; vanished row explained safely |
| `CP-TST-QUEUE-03` | High volume, long/localized values, mobile cards, keyboard and touch | Comparison, identity, owner/currentness, next action, sort/filter/page and focus remain usable |

## 5. `CP-R1-EVIDENCE` — evidence review

### Purpose and anatomy

Purpose: let an authorized human compare the original source with derived values and limitations, decide evidence eligibility, make a traceable correction, and see downstream consequences.

Anatomy:

1. case/document identity, source/provider, received/current version and eligibility;
2. evidence list and selected source;
3. original preview/download-safe alternative;
4. extracted/derived fields with source reference, confidence/limitation and review status;
5. correction editor with original/new value, reason and expected version;
6. conflicts, excluded/unsafe/unsupported conditions;
7. dependency/invalidation/re-evaluation region;
8. extraction-only review controls and persistent result/recovery status.

### Task flow and behavior

1. Load the immutable source independently from derived fields. Provider failure cannot make the source appear absent when safely stored.
2. Selecting a field links to source position where available and always provides an equivalent textual reference.
3. Reviewer may accept, correct, reject, return or escalate only within current permission/state. AI confidence is not eligibility or truth.
4. Editing creates a local safe draft. Commit sends exact document/field/version, new value and reason where required.
5. Conflict preserves the entered value/reason and presents current source/value for intentional compare/reapply.
6. Successful correction creates a new reviewed version, preserves original/before-after, invalidates all dependent artifacts and exposes re-evaluation progress.
7. Approving extraction means only “reviewed extraction eligible for bounded downstream use”; it never approves the claim or assessor report.

### Selection, editing and persistence

- UI owns selected document/page/field, preview zoom/position and unsaved edit within the approved draft boundary.
- Source/evidence service owns document, versions, confidence/provenance, review and eligibility.
- One field may be edited at a time unless a separately approved batch contract defines per-field validation/conflict/audit. No silent multi-field save.
- Changing document/field with unsaved input requires save/discard/cancel treatment; no route/resize silently discards it.
- Superseded, stale, contested, unauthorized and quarantined evidence remains visibly ineligible and cannot support execution.

### Adaptation and accessibility

- Desktop uses source/fields comparison; tablet uses split only above intrinsic minimum; mobile uses sequential source/value relationship.
- Field/source selection, preview position, draft/reason and conflict persist during transformation.
- Preview coordinates have textual alternatives; preview unavailable state retains metadata and approved download/replacement path.
- Confidence, eligibility, correction and conflict use text/non-color cues. Screen reader receives field status and save/invalidation result once.
- Keyboard can move document → field → source reference → edit/reason → result without pointer or spatial-only interaction.

### Analytics and tests

Goal metrics: source-open rate before correction, field disagreement/correction, unsupported-source frequency, conflict/reapply, downstream invalidation completion, return/escalation, review time distribution and recovery. Never use correction/override rate alone as staff-performance or claimant-risk signal.

| ID | Scenario | Expected result |
|---|---|---|
| `CP-TST-EVIDENCE-01` | Source succeeds while extraction fails/not configured/partial | Source remains available; exact provider state and manual review/retry owner shown; no empty success |
| `CP-TST-EVIDENCE-02` | Concurrent correction after reviewer enters value/reason | Draft preserved; current value/version compared; intentional reapply creates traceable correction |
| `CP-TST-EVIDENCE-03` | Correct field at desktop, reflow to mobile, then dependency invalidation fails partially | Selection/draft/result persist; stale dependants block action; region-local recovery is operable/accessibly announced |

## 6. `CP-R1-APPROVAL` — approval cockpit

### Purpose and anatomy

Purpose: enable an eligible human to independently understand one exact proposal, evidence and consequence, then approve, reject, return or escalate with a reason and valid successor state.

Anatomy follows the deliberate review sequence:

1. exact proposed action and affected person/object;
2. consequence, reversibility and prohibited interpretation;
3. current evidence, exclusions, contradictions, source/version and limitations;
4. AI/rule proposal and uncertainty, visually secondary;
5. authority, assignment, separation-of-duty, expiry and currentness;
6. alternatives and recourse;
7. required reason/comments;
8. distinct decision controls;
9. immutable human outcome;
10. separate execution/provider/reconciliation status.

### Task flow and behavior

1. Opening from queue rechecks eligibility but records no decision.
2. Proposal and evidence regions load independently; partial/stale/conflicting evidence blocks decision and identifies owner.
3. Evidence inspection retains reason and position. AI output never preselects or visually privileges an outcome.
4. Reviewer chooses one explicit action and enters the required reason. Each control names its distinct consequence.
5. Governed confirmation repeats exact action, target, consequence, evidence/workflow version, authority and audit effect; server revalidates all prerequisites.
6. During submit, competing decision controls and duplicate activation are disabled.
7. First valid atomic outcome wins. Concurrent, expired, revoked or mismatched attempts preserve reason and route to current comparison/return/escalation.
8. Recorded approval remains distinct from execution, provider success, settlement, payment, delivery and remedy.

### Decision and queue policy

- No outcome is defaulted, preselected, learned or placed as the visually routine path.
- No bulk consequential decision exists. “Next approval” appears only after a committed outcome or deliberate safe exit.
- Return is not rejection; escalation is not approval; override requires its own current authority/reason/meaning.
- A narrow viewport never removes decision functionality. It changes to sequential deliberate review and makes controls available only after prerequisites are present/current—not after an arbitrary scroll-tracking ritual.

### Adaptation and accessibility

- Desktop may show proposal/evidence/decision together; tablet/mobile follow the same semantic review sequence with decision region last.
- Headings/landmarks expose proposal, evidence, authority, reason and outcome. Evidence remains reachable without opening a focus-losing modal chain.
- Focus enters the cockpit heading, stays on task during updates and moves to immutable outcome/next valid task only after success.
- Stale/permission/error status is announced and associated with disabled controls. Reasons remain after error/reflow/reauthentication.

### Analytics and tests

Goal metrics: evidence inspected, comprehension errors, disagreement/return/escalation, stale/expired attempts, review duration distribution, concurrent conflict, reason completeness, recourse and recovery. Bulk speed/approval rate is not a success metric.

| ID | Scenario | Expected result |
|---|---|---|
| `CP-TST-APPROVAL-01` | AI recommendation conflicts with evidence and reviewer rejects/returns | Evidence remains primary; no preselection; distinct reasoned human outcome is recorded |
| `CP-TST-APPROVAL-02` | Evidence/version/permission changes before confirmation or during submit | Submit blocked/atomically rejected; reason preserved; current comparison and safe next action shown |
| `CP-TST-APPROVAL-03` | Desktop → mobile review with partial evidence, long reason and screen reader | Sequence/authority/limitations remain clear; decision last and operable only when current; no hidden evidence or lost input |

## 7. `CP-R1-TABLE` — high-density data tables

### Purpose and anatomy

Use only for homogeneous records requiring comparison across stable attributes. Anatomy: heading/caption, source/freshness, filter/search, active-filter/sort summary, authorized result count, header controls, rows/cells, separate row navigation/actions, selection scope where allowed, partial summary, pagination and recovery status.

### Behavior rules

- Query source owns data/count/currentness; UI owns safe filter/sort/page/density/expanded-row state.
- Header sort button exposes current direction. Sort is stable and server-aligned for pagination.
- Filters apply deliberately or with clearly announced bounded updates; changing filter resets page but not unrelated safe preferences.
- Row links and action buttons are not nested. Clicking whitespace does not trigger a consequential action.
- Pagination/load-more preserves focus and announces newly available range/count. Virtualization must preserve semantics, search/find expectations and return focus before use.
- Columns hide only by approved priority. Hidden data is available in row detail when permitted.
- Mobile card substitution preserves term/value labels, identity, state, owner/currentness and actions.
- Horizontal scroll is limited to genuinely two-dimensional labeled regions with sticky context and an equivalent detail pattern.
- Selection summary states exact page/query scope. No generic “select all” across unseen records and no generic bulk mutation.

### Analytics and tests

Goal metrics: find/open success, filter reformulation, sort use tied to tasks, wrong opens, row-action error, return position, partial/high-volume latency and recovery—not raw clicks.

| ID | Scenario | Expected result |
|---|---|---|
| `CP-TST-TABLE-01` | Zero/one/10,000 records with long/localized cells and stable equal sort values | Honest counts, deterministic order, usable comparison/pagination and no clipped critical field |
| `CP-TST-TABLE-02` | Permission-restricted records and partial source failure | Filtering precedes count; no leak; successful rows retained with exact limitation/recovery |
| `CP-TST-TABLE-03` | Table → tablet priority columns → mobile cards, keyboard/touch/screen reader | Same objects, labels, sort/filter/page/selection scope and logical focus/return context |

## 8. `CP-R1-PANEL` — panels, rails, drawers and sheets

### Pattern types and rules

| Type | Use | Semantics/focus | Adaptation |
|---|---|---|---|
| Persistent panel | Peer/supporting context visible with primary task | Named region; does not steal focus on refresh | Inline wide; collapsible section intermediate/compact |
| Complementary rail | Evidence/metadata not primary but material | `complementary` only when content qualifies; trigger carries state/count | Inline wide; side sheet or sequential section narrow |
| Nonmodal drawer/sheet | Bounded context while primary remains operable | No focus trap/`aria-modal`; explicit close; focus remains user-controlled | Side/bottom sheet based on content/input/safe area |
| Modal drawer/sheet | Blocking bounded task/choice | Dialog semantics, focus containment, inert background, invoker return | Full-height compact/intermediate; remains modal during active confirmation |

Each panel has a unique heading, purpose, source/freshness, local states/actions and close/collapse behavior. Open/collapsed mode, safe size, scroll, selection and approved draft persist across reflow. A panel never hides the only error, status, unsaved warning, evidence limitation or required action. Independently changing panels own local loading/partial/error/recovery; they never blank the workspace.

### Analytics and tests

Measure task completion/context switching, repeated open/close caused by missing information, lost-state/focus defects and recovery—not panel opens as value.

| ID | Scenario | Expected result |
|---|---|---|
| `CP-TST-PANEL-01` | Supporting panel fails while primary record succeeds | Primary remains usable; local exact error/recovery; no whole-screen failure |
| `CP-TST-PANEL-02` | Inline rail → side sheet → sequential section during resize/orientation | Same heading/object/selection/scroll/state; equivalent focus target; no hidden warning/action |
| `CP-TST-PANEL-03` | Permission revoked while panel open with safe draft | Protected content removed; draft handled by policy; focus moves to safe explanation/parent return |

## 9. `CP-R1-DIALOG` — governed dialogs

### Use and anatomy

Use a dialog for one short bounded blocking confirmation or choice. Long evidence review, multistep correction, queue browsing and complex configuration use a page/panel. Anatomy: heading, exact target/effect, prerequisites/currentness, consequence/reversibility, reason/input if required, error/status, explicit cancel, exact confirm and persistent parent-outcome handoff.

### Behavior rules

1. Opening performs no business action. Save invoker and background scroll.
2. Initial focus goes to heading/summary for consequential confirmation or first required field for simple input; destructive/approve confirm is never initial focus.
3. Focus is contained in both directions; background is inert. Escape/backdrop cancels only before submit and only when no unsaved-loss ambiguity exists.
4. Confirm uses current permission/object/state/version checks. Submit enters busy, suppresses duplicates and prevents unsafe close.
5. Error/timeout/unknown stays in dialog with input/reason preserved and possible-effect/reconciliation boundary explicit.
6. Success closes only after the parent exposes the authoritative result. Focus returns to invoker or resulting-record heading if the invoker no longer exists.
7. Resize may change dialog to full-height sheet but never change modal semantics, target, inputs, confirmation state or focus containment.
8. Reduced motion does not delay focus or state completion.

### Analytics and tests

Measure cancellation before effect, validation errors, duplicate suppression, stale/permission blocks, recovery and focus return. Confirmation rate is not inherently success.

| ID | Scenario | Expected result |
|---|---|---|
| `CP-TST-DIALOG-01` | Keyboard open, forward/reverse containment, Escape/cancel, removed invoker | Correct initial focus, containment, safe cancel and documented fallback return |
| `CP-TST-DIALOG-02` | Double submit, slow response, timeout/unknown and server conflict | At most one attempt/effect; reason preserved; unknown reconciles; no false success |
| `CP-TST-DIALOG-03` | Desktop dialog becomes mobile full-height sheet during confirmation/error | Same modal task/state/input; background inert; no focus loss or hidden cancel/confirm/prerequisite |

## 10. Cross-pattern handoffs

| From → to | Context carried | Receiver recheck | Return/recovery |
|---|---|---|---|
| Queue → record/evidence/approval | Source type/ID, queue query/order/position, required outcome | Current scope, object, permission and version | Restore logical row or safe next result with change explanation |
| Record → evidence panel/review | Case/document/evidence IDs, selected source/field | Content/review permission and current version | Return to evidence section with review/invalidation state |
| Queue/record → approval cockpit | Exact proposal/approval ID, evidence/workflow versions | Eligibility, assignment, SoD, expiry/currentness | Return with immutable decision or safe blocked/returned state |
| Pattern → dialog | Invoker, exact action/target/effect/version, local reason/input | All current server prerequisites | Persistent parent outcome and focus return/fallback |
| Panel/dialog → recovery | Failed scope, preserved work, attempt/correlation, possible effect | Retry/reconciliation eligibility and authority | Confirmed result, manual owner or safe cancel without replay |

## 11. Data/content contract

Every complex pattern exposes only permitted safe fields and keeps these meanings distinct:

- object identity/reference, tenant/workspace-derived scope and current version;
- source vs derived vs human-reviewed data;
- queue/assignment vs evidence/review vs approval/decision vs execution/provider state;
- AI provenance/confidence vs evidence eligibility/business certainty;
- UI selection/draft vs durable committed state;
- request accepted vs provider accepted vs delivered vs final domain outcome;
- current, stale, superseded, contested, restricted and quarantined evidence.

Content states what happened, what is available/preserved, exact limitation/possible effect, current owner and next safe exit. Never show raw provider errors, prompts, secrets, restricted audit/evidence, internal risk/fraud signals or other-tenant existence.

## 12. Accessibility and responsive checklist

- Semantic reading and focus order follow task order through desktop/tablet/mobile transformation.
- Every region/pattern has a unique accessible heading/name and programmatic status/error association.
- Keyboard-only and touch-only completion includes filters, rows/cards, preview/source links, reasons, decision controls, panels, dialogs, pagination and recovery.
- Focus stays visible/unobscured; modal entry/containment/return and nonmodal no-focus-steal behavior are verified.
- High-impact controls meet 44×44 CSS px; no hover-only meaning/action.
- Status, confidence, selection, restriction, conflict and success use text/non-color cues and work in forced colors.
- 200% text and 400%/320px reflow retain evidence, errors, authority, actions and recovery; only labeled genuine two-dimensional regions scroll horizontally.
- Reduced motion retains immediate semantic feedback; localization/RTL and long content do not clip or reorder meaning.

## 13. Gaps and validation order

| ID | Gap | Required treatment |
|---|---|---|
| `CP-GAP-01` | Pattern contracts are not rendered as P0 low-fidelity layouts across seven states/three bands. | Render queue, evidence, approval, table, panel and dialog variants before visual polish. |
| `CP-GAP-02` | Current portal components have not been reconciled comprehensively to these task-level contracts. | Inspect component/API/state ownership; record parity gaps, do not weaken contract to match legacy behavior. |
| `CP-GAP-03` | Queue ranking, SLA, saved views, benign selection/bulk scope and delegation remain insurer-owned decisions. | Annotate decision required; do not invent defaults or generic bulk actions. |
| `CP-GAP-04` | Evidence draft persistence, multi-field editing and mobile complex-correction policy are not approved/runtime-proven. | Default to one-field traceable correction and safe explicit navigation guard until approved. |
| `CP-GAP-05` | Approval workload, SoD/delegation/expiry and exact permissions remain conditional; current permission gaps persist. | Block protected approval claims; run role/negative/concurrent/bypass tests after policy/implementation. |
| `CP-GAP-06` | Dialog/panel focus, table/card semantics, physical touch and screen-reader evidence remains incomplete/failing in current diagnostics. | Fix verified accessibility gaps and retest supported browser/AT/device matrix. |
| `CP-GAP-07` | Practitioner comprehension and operational efficiency evidence is absent. | Scenario-test wrong/stale/partial/permission/recovery paths with representative roles. |

Validation order: specification consistency → rendered low-fidelity review → component/API parity → automated browser/keyboard/reflow → manual screen-reader/touch/device → protected multi-role/provider UAT → practitioner validation. Passing an earlier layer never claims a later one.
