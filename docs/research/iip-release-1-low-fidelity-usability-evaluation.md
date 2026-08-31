# IIP Release 1 Low-Fidelity Usability Evaluation

| Field | Value |
|---|---|
| Date | 2026-08-10 |
| Artifact | `/prototype/release-1-low-fidelity?variant=A|B|C` |
| Status | Expert/cognitive/accessibility diagnostic completed and first remediation applied; participant sessions not conducted |
| Decision gate | High-fidelity work is not approved until representative-user evidence meets the stated gate |
| Standards baseline | WCAG 2.2 AA design and implementation review; no conformance claim |
| Data boundary | Synthetic identifiers and scenarios only; no customer, employee, credential or insurer-confidential data |

## 1. Decision and evidence boundary

This evaluation asks whether representative Release 1 users can understand IIP's object model and complete critical tasks before visual expansion is approved. It separates:

1. **Expert evidence:** reproducible heuristic and cognitive walkthrough observations.
2. **Implementation evidence:** browser, keyboard, reflow and automated accessibility diagnostics.
3. **Participant evidence:** observed behavior, task outcomes, time, assistance, confidence and statements from recruited users.

Only the first two layers exist today. No participant, quote, success rate, confidence result, consensus or usability validation is claimed. Automated accessibility checks cover only a subset of barriers and cannot establish conformance.

## 2. Target users and conditions

The first wave must cover practitioners with recent direct responsibility as:

- Claims Officer;
- Claims Supervisor / Authorized Approver;
- Customer Service / Complaints Officer;
- Compliance / Audit reviewer;
- IT / Security / Integration administrator;
- loss assessor, broker or claim party for the constrained mediated-handoff tasks only.

Recruit across materially different insurer contexts where possible. Capture role, authority, task frequency, institution type, device, network, language and accessibility context without identifying the person or employer in Git. Include keyboard-only and screen-reader users and people affected by low vision, motor, cognitive, language or low-bandwidth constraints where the related risk is material. A stakeholder counts as a practitioner only when both capacities are recorded separately.

Exclude vendor-only familiarity and hypothetical future responsibility. Do not place a manager in a session unless the participant can freely decline.

## 3. Research questions

1. Can users distinguish a case **record**, owned **work item**, staff/customer **conversation**, versioned **evidence**, advisory **proposal**, accountable human **approval**, later execution state and immutable **audit event**?
2. Can each role predict where to start, identify the current owner and authority, and reach the correct successor without unsafe shortcuts?
3. Do labels match insurance-operational language without requiring knowledge of IIP IDs or architecture terms?
4. Can users recover from missing, partial, permission-restricted, failed and ambiguous-provider states without inferring success?
5. After interruption, can users recognize the preserved object, draft, evidence version, pending action and changes that require re-review?
6. Under compact/mobile and low-bandwidth conditions, can users still identify priority, state, authority, source and next safe action?
7. Does any participant mistake AI preparation for a final decision, approval for execution, conversation history for evidence, or audit history for current business state?

## 4. Measures and coding

Record each task independently.

| Measure | Coding |
|---|---|
| Task outcome | `S` unassisted success; `AS` assisted success; `F` failure; `AB` abandonment |
| Critical error | Action or interpretation that could create an unauthorized/consequential outcome, disclosure, wrong-record action, duplicate external effect, false success or loss of evidence/currentness |
| Noncritical error | Wrong turn or reversible mistake that does not create material harm |
| Assistance | `A0` none; `A1` neutral reread; `A2` directional clue; `A3` path/step instruction. `A1`–`A3` are not unassisted success |
| Time | Scenario handoff to stated/observable intended outcome; pause separately for technical failure or safeguarding |
| Confidence | Participant-selected 1–5 after the task, followed by “What made you choose that rating?” |
| Observed confusion | Exact behavior, hesitation, backtrack, comparison or incorrect inference; interpretation recorded separately |
| Qualitative evidence | Verbatim short statement where consent permits, tagged to session/task; never reconstructed from memory |

Time is descriptive until an internal pilot establishes realistic task-specific anchors. Faster completion never compensates for an authority, evidence, privacy or accessibility error.

## 5. Moderated tasks

Scenarios state outcomes, not UI labels or paths. Rotate variants and task order where practical to reduce order effects. Do not expose the model key before the first comprehension probe when testing unaided understanding; reveal it only in the assigned condition.

| ID | Role/context | Scenario and intended outcome | Comprehension probe | Success and critical-error rule |
|---|---|---|---|---|
| `UT-LFW-01` | Claims Officer | A claim needs evidence review. Find the owned activity for synthetic case `MOTOR-011`, open the correct place, then explain what completing the activity changes. | “What is the difference between the claim and the item waiting for you?” | Opens `WI-011` linked to `MOTOR-011`; does not claim task completion rewrites unrelated claim facts. Wrong-record action is critical. |
| `UT-LFW-02` | Claims Officer | Find `MOTOR-011` using cross-object search and choose among the case, work item, conversation and evidence results. | “How did you know which result type to choose?” | Chooses the requested object and preserves return context. Treating all result types as equivalent is a critical comprehension failure. |
| `UT-LFW-03` | Claims Officer / reviewer | Review a low-confidence registration value, compare it with the source, correct it with a reason, then identify what must be re-evaluated. | “Which item is evidence? What is derived? What became stale?” | Uses current source/version, records correction reason, recognizes dependent proposal invalidation. Accepting confidence as truth is critical. |
| `UT-LFW-04` | Authorized Approver | Review proposal `PR-07`, determine whether it is current, and record an accountable outcome. | “What has happened now, and what has not happened yet?” | Verifies evidence/authority and distinguishes proposal, human decision and execution. Treating proposal or button activation as final execution is critical. |
| `UT-LFW-05` | Compliance | Reconstruct who corrected evidence and who recorded the human outcome; identify the current case state separately. | “Is the audit event the claim state? Why?” | Follows correlation and before/after evidence without treating audit history as mutable business state. Restricted-detail disclosure is critical. |
| `UT-LFW-06` | Customer Service | Review a proposed customer update, minimize disclosure, attempt the send, then handle an unknown delivery result. | “What can you safely tell the customer now?” | Distinguishes draft, authorized attempt, accepted and delivered; reconciles before retry. Duplicate send or false delivered state is critical. |
| `UT-LFW-07` | Any internal role | Encounter empty, partial, failed and permission-restricted variants for a role-critical task. Recover or leave safely. | “What do you think exists, what is hidden, and what can you do next?” | Uses exact safe exit, preserves successful scope and does not infer hidden record existence. Unauthorized workaround is critical. |
| `UT-LFW-08` | Claims / approval | Begin a task, simulate interruption, then resume after the evidence or permission context changes. | “What did you need to recheck before continuing?” | Recognizes object, draft, evidence version, authority and change summary. Blind continuation is critical. |
| `UT-LFW-09` | IT / Security Admin | Investigate an ambiguous provider outcome under low bandwidth, then choose reconciliation, retry or manual intervention. | “Why is retry safe or unsafe?” | Identifies attempt/correlation and reconciles before retry. Replaying an unknown external effect is critical. |
| `UT-LFW-10` | Relevant role; compact/mobile; keyboard or AT condition | Complete one assigned task at 390/320 CSS px or equivalent zoom/reflow without hover and under low bandwidth. | “What information or action became harder to find?” | No loss/obscuring of identity, state, authority, source, action or recovery; no page-level horizontal overflow. |

## 6. Neutral moderator script

Opening:

> We are testing the structure and words, not you. Some parts are deliberately schematic and no real claim action will occur. Please work as you normally would, say what you are looking for, and avoid sharing real customer, staff or insurer-confidential information. You may pause, skip or stop at any time.

Before each task: read only the scenario. Do not name the target control, route, object type or intended path.

Allowed neutral probes:

- “What are you looking for now?”
- “What do you think this represents?”
- “What tells you that?”
- “What do you expect to happen next?”
- “What, if anything, would you need before continuing?”
- “What changed after you resumed?”

Do not teach the model, praise a path, correct terminology during the task or use “Would you click…?” If assistance becomes necessary, record the level before giving it.

Debrief:

1. Ask the participant to explain record, work item, conversation, evidence, proposal, approval and audit in their own words.
2. Ask which labels match or conflict with their workplace language.
3. Ask what was most difficult under interruption, restricted access or low bandwidth.
4. Ask what could cause a harmful mistake.
5. Reconfirm withdrawal/recording choices and do not solicit real case examples containing identifiers.

## 7. Observation record

| Field | Entry |
|---|---|
| Session/task | `SES-___ / UT-LFW-__` |
| Role/context | — |
| Variant/state/device/network/input | — |
| Start/end/pause-adjusted time | — |
| Outcome/assistance | `S/AS/F/AB`; `A0–A3` |
| Critical/noncritical errors | — |
| First action/backtracks | — |
| Observation (`OBS`) | — |
| Participant statement (`SAY`) | — |
| Terminology (`TERM`) | — |
| Authority/evidence/access (`AUTH/EVID/ACCESS`) | — |
| Accessibility/network (`A11Y/NETWORK`) | — |
| Confidence + reason | — |
| Moderator influence (`MOD`) | — |
| Follow-up | — |

Observation and participant language must be written before interpretation. Raw recordings/notes stay in the approved restricted repository; Git receives anonymized, redacted atomic observations only.

## 8. Approval gate and analysis

Use the existing IA decision gate: at least 90% unassisted completion for critical Release 1 tasks and zero unrecovered consequential error. Also require:

- every critical role/context represented in the evidence reviewed;
- zero participant interpretation that grants AI final authority or conflates approval with execution;
- zero unresolved High/Blocker accessibility barrier on a critical task;
- each role-critical concept distinguished accurately in the participant's own words;
- no severe minority privacy, permission or accessibility failure averaged away;
- all material contradictions and moderator interventions visible.

Report numerator/denominator and role/context separately. Counts describe only the studied corpus. Confidence ratings support interpretation but do not override behavior.

Maintain the synthesis chain:

`session source → atomic observation → pattern → interpretation → insight → implication → decision`

Assign confidence from source quality, relevance, context diversity, consistency, disconfirming evidence and researcher influence. Do not synthesize participant patterns until sessions exist.

## 9. Expert and implementation findings

Severity uses `1 Minor`, `2 Moderate`, `3 Major`, `4 Critical`. “Fixed” means corrected in the low-fidelity prototype and rechecked locally; it does not mean participant-validated or production-implemented.

| ID | Location/trigger | Observation and principle | Consequence / affected users | Severity rationale | Remediation and status | Validation |
|---|---|---|---|---|---|---|
| `HUX-LFW-01` | Variant B; loading/empty/error/permission | Populated rows and inspector data remained visible while the state said data was unresolved, absent or restricted. Status/consistency/privacy failure. | Users could infer false success or protected object existence. | `4` consequential and disclosure risk across roles. | State-aware content now removes or limits values and counts. **Fixed locally.** | Repeat all seven states; participant `UT-LFW-07`. |
| `HUX-LFW-02` | All surfaces; successful state | Generic “Review consequential action” appeared outside approval and for ineligible roles. Prevention/authority failure. | Could teach unsafe human/AI authority and action boundaries. | `4` critical insurance-decision risk. | Human-decision control exists only in approval; enabled only for Authorized Approver/current/success/online. **Fixed locally.** | Negative role/state matrix; `UT-LFW-04`. |
| `HUX-LFW-03` | Prototype-wide comprehension | Record, work item, conversation, evidence, proposal, approval and audit relationships were not visible; generic boxes demanded inference. Recognition/mental-model failure. | First-time and infrequent users could choose the wrong object or attribute authority incorrectly. | `3` blocks reliable critical-task comprehension. | Added persistent model key, per-surface concept and task-specific synthetic examples. **Fixed locally; comprehension unvalidated.** | Unaided/aided comparison across `UT-LFW-01`–`05`. |
| `HUX-LFW-04` | Variant A; non-evidence surfaces | Every surface used the same evidence/decision stages, contradicting task purpose. Real-world match/consistency failure. | Users could learn a false workflow and miss actual handoffs. | `3` repeated across all task families. | Added surface-specific four-stage sequences. **Fixed locally.** | Cognitive walkthrough and participant path explanation. |
| `HUX-LFW-05` | External roles paired with staff screens | Assessor, claimant and broker selections exposed internal workspace structures and controls. Privacy/authority/real-world mismatch. | Suggests unauthorized portal access and excess disclosure. | `4` cross-boundary privacy/tenant risk. | External roles now receive a mediated-handoff boundary with no staff data/action. **Fixed locally.** | Role/surface matrix and mediated task session. |
| `A11Y-LFW-01` | Variant B at 390px | Four-column table caused page-level horizontal overflow. WCAG 2.2 1.4.10/reflow risk. | Compact, zoomed and low-vision users lose context or must pan. | `3` blocks efficient critical comparison. | Compact Variant B substitutes labeled cards; wide table uses fixed layout/breaking. **Fixed locally; manual zoom/AT pending.** | 320/390/400% browser and physical-device checks. |
| `HUX-LFW-06` | Prototype-wide | Interruption/resume and low-bandwidth/offline expectations were descriptive only, not exercisable. Recovery/visibility failure. | Study could not observe resumption or safe degraded behavior. | `3` common operational condition with consequential stale-state risk. | Added interruption/resume simulation and explicit normal/low-bandwidth/offline modes with action blocking. **Fixed locally.** | `UT-LFW-08`–`10`. |
| `HUX-LFW-07` | State selector/header | Internal uppercase codes were the primary state vocabulary. Recognition/terminology risk. | Practitioners may understand system codes differently or not at all. | `2` meaningful friction; workaround exists. | Plain-language labels are primary; stable codes secondary. **Fixed locally; terminology unvalidated.** | Terminology probe in every session. |
| `HUX-LFW-08` | Human-decision dialog | Required reason was prefilled and submit bypassed native validation. Error-prevention failure. | Prototype could teach a routine approval click without accountable reasoning. | `3` consequential attention and audit risk. | Removed default reason; native required validation now gates simulated submission. **Fixed locally.** | Empty/reason keyboard and screen-reader probe; `UT-LFW-04`. |
| `A11Y-LFW-02` | Six variants in automated scan | Axe WCAG A/AA scan found zero detectable violations; keyboard semantics, focus, status and reduced motion were present in sampled states. | Positive implementation signal only. | `0`; automated coverage is incomplete. | Preserve native controls/status/focus behavior. **Positive pattern.** | Manual screen-reader, 200/400% zoom, forced colors, touch and disabled-user testing remain required. |

## 10. Cognitive task map

| Task family | Must notice | Must compare/decide | External memory and recovery support |
|---|---|---|---|
| Find/open work | Object type, owner, state, currentness | Record versus task; correct destination | Stable IDs, return context, type labels, queue state |
| Evidence review | Source/version, derived value, confidence, eligibility | Source truth versus extraction; correction consequence | Side-by-side/text reference, before/after, invalidation list |
| Human approval | Proposal, evidence version, authority, expiry, consequence | Whether evidence and authority are current; outcome and reason | Deliberate sequence, action guard, immutable outcome, execution separate |
| Communication | Conversation, disclosure scope, authorized attempt, delivery | What may be disclosed; whether retry is safe | Draft, attempt ID, channel state, reconciliation owner |
| Audit | Event, actor, object, before/after, correlation | Historical event versus current business state | Correlation chain, scoped filters, immutable export |
| Resume/recover | Preserved object/draft, changed evidence/authority, possible external effect | Continue, reapply, reconcile, return or escalate | Explicit resume point, change summary, safe next action |

Intrinsic insurance complexity remains visible; the interface should remove memory burden without hiding evidence, uncertainty, authority or consequences.

## 11. Corpus and synthesis status

| Source | Method | Coverage | Confidence/use |
|---|---|---|---|
| `EXP-2026-08-10` | Heuristic and cognitive walkthrough | Three variants, 12 surface/modes, seven states, eight roles | High for directly reproducible design contradictions; no frequency inference |
| `BROWSER-2026-08-10` | Playwright keyboard/state/reflow diagnostics | Variant switching, state controls, decision dialog, focus return, 390px overflow | High for tested implementation behavior only |
| `AXE-2026-08-10` | Axe WCAG A/AA automated scan | A/B/C at 1280px and 390px | Medium for absence of detectable automated violations; low for total accessibility |
| Participant corpus | None | No sessions | Insufficient; participant synthesis prohibited |

Contradictions/outliers: none can be reported without participant evidence. Expert remediation may improve internal consistency but could introduce unfamiliar terminology or excess explanation; representative sessions must test that tradeoff.

## 12. Remaining blockers and next actions

1. Pilot the script and technical setup internally; do not count pilot behavior as practitioner evidence.
2. Name research owner, moderator, note-taker and approved restricted raw-data location.
3. Recruit all role contexts plus relevant accessibility/device/network diversity.
4. Run sessions without teaching the model; capture outcomes and observations separately.
5. Synthesize with provenance, retain contradictory/minority evidence, and update the wireframes.
6. Re-test changed tasks, then convene named Product, Insurance Operations, Engineering, Security/Privacy and Accessibility review.

High-fidelity sign-off remains blocked until the participant gate is met and blocking findings are resolved.
