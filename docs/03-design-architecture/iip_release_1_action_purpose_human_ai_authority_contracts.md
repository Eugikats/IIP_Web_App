# IIP Release 1 Action Purpose and Human/AI Authority Contracts

| Field | Value |
|---|---|
| Version | 0.1 |
| Date | 2026-08-07 |
| Status | Source-complete design contract; practitioner, accessibility, and protected-runtime validation remain open |
| Scope | The 20 critical actions registered in `iip_release_1_interaction_authority_and_inventory.md` |
| Behavior owner | Product Design |
| Authority owners | Domain/Application, insurer-authorized humans, Security/Compliance |

## 1. Purpose and conceptual model

IIP is an intelligence layer, not the insurer's decision maker or system of record for policy, payment, accounting, or final claim outcome. Its interaction model is:

> **Prepare → show basis and limits → human reviews or controls intent → execute the exact authorized action → reconcile authoritative outcome → preserve correction and recourse.**

Users should understand five distinct states without learning internal architecture:

1. **Source received:** IIP has retained or referenced an authorized input.
2. **AI/rule prepared:** IIP has produced a derived classification, extraction, summary, checklist, option, or draft.
3. **Human reviewed:** an accountable person accepted, corrected, rejected, returned, or escalated the prepared work.
4. **Action attempted:** IIP submitted one exact authorized external or state-changing action.
5. **Outcome confirmed:** the authoritative owner confirmed success, failure, or another final state; unknown remains unknown.

The interface must not collapse these states into a generic `Done`, `Approved`, or `Sent` label.

### Human context

| Dimension | Release 1 design basis |
|---|---|
| Goal | Complete evidence-backed motor-claim work safely without reconstructing context across systems |
| Knowledge | Users understand insurance work but may not understand model, provider, workflow, or integration internals |
| Environment | Interrupt-driven office/branch/field work; possible low bandwidth; multiple systems and handoffs |
| Frequency | Queue, evidence, and communication actions may be frequent; approvals, overrides, exports, and stop controls are less frequent and higher stakes |
| Stakes | Incorrect evidence, disclosure, approval, communication, or execution can affect rights, money, privacy, trust, and regulatory accountability |
| Input modalities | Keyboard/mouse and touch; assistive technology and zoom/reflow must remain supported; hover and motion are never sole signifiers |

## 2. Purpose, necessity, and non-algorithmic baseline

The legitimate Release 1 purpose of AI/rules is to reduce clerical retrieval, extraction, comparison, checklist, summarization, routing, and drafting effort while preserving evidence lineage and human accountability. The optimization target is **review readiness and safe task completion**, not approval rate, claim cost, fraud yield, rejection rate, staff throughput alone, or reduced human review.

The non-algorithmic baseline is authorized staff manually receiving documents/messages, locating case and policy context, checking required evidence, transcribing fields, preparing referrals or communications, seeking approval, updating an insurer system, and retaining an audit trail. AI/rules should be limited or removed where they do not provide a demonstrable benefit over this safer baseline.

Decision subjects and affected parties include policyholders, insured people, claimants, drivers, third parties, insurer staff, brokers, assessors, and the insurer. The accountable owner of a consequential claim or communication outcome remains the insurer's authorized human role.

## 3. Authority classes

| Class | Meaning | AI/rule authority | Required human authority |
|---|---|---|---|
| `AUT-0 Observe` | Retrieve, display, compare, or monitor without business-state change | May perform bounded retrieval/technical validation using authorized sources | Human chooses task and interprets result |
| `AUT-1 Prepare` | Produce a derived draft, classification, extraction, checklist, summary, match suggestion, or option | May prepare with provenance, freshness, uncertainty, limitations, and version | Human can inspect, correct, reject, return, or ignore |
| `AUT-2 Route` | Create or move non-consequential work under an approved deterministic policy | May route technical work where scope and rule are approved and reversible | Human owns exceptions, reassignment, escalation, and policy |
| `AUT-3 Human commit` | Change reviewed evidence, work ownership, communication approval, or another accountable business state | May validate and propose; cannot commit as the deciding actor | Exact current permission, object/state authority, reason where required |
| `AUT-4 External/consequential` | Communicate externally, execute an approved step, or decide a consequential workflow outcome | May prepare and technically execute only the exact current human authorization | Qualified accountable human decides intent; execution remains separately guarded and reconciled |
| `AUT-5 Safety control` | Pause, kill, quarantine, or fail closed to prevent further harm | May automatically fail closed or trigger an approved protective stop | Authorized operator investigates; controlled human approval is required to restart where risk persists |

No autonomy label, confidence score, provider result, workflow configuration, historical pattern, role label, or human click without evidence and real authority may promote an action to a higher class.

## 4. Attention-demand model

| Level | Use | Required presentation |
|---|---|---|
| `ATT-0 Ambient` | Background receipt, processing, reconciliation, or monitoring | Non-blocking status near the affected object; no interruption unless user action becomes necessary |
| `ATT-1 Routine` | Frequent, reversible navigation, filtering, metadata, or draft work | Clear signifier and immediate feedback; preserve context; no disproportionate confirmation |
| `ATT-2 Focused review` | Evidence correction, classification, completeness, assignment, or exception resolution | Show source/current state, dependencies, uncertainty, and consequences; protect against interruption and stale work |
| `ATT-3 Deliberate decision` | Approval, override, external send, disclosure, export, or execution | Dedicated review region/confirmation; exact target/effect/evidence/authority; reason where required; no bulk shortcut |
| `ATT-4 Incident` | Cross-tenant risk, unsafe evidence, human-gate bypass, repeated provider ambiguity, systematic error, or kill switch | Interrupt only responsible operators; block affected execution; show scope, owner, next safe action, and restart conditions |

Attention must follow consequence and recovery cost, not AI confidence or visual novelty. Do not use urgent styling for routine AI output. Do not hide high-consequence information behind hover, collapsed help, color, or animation.

## 5. Feedback contract

Every action provides:

1. **Before action:** visible label, target, current state, prerequisites, authority, consequence, and whether the result is reversible.
2. **Immediate acknowledgement:** the activated control visibly and programmatically enters pressed/pending/disabled state without claiming business completion.
3. **Progress:** if the authoritative result is not immediate, show the owned stage (`uploading`, `processing`, `awaiting human`, `provider pending`, `reconciling`) and allow safe navigation where possible.
4. **Completion:** announce the exact confirmed state, affected object, timestamp/current version where useful, and next valid action.
5. **Failure:** state what failed, what is preserved, whether any effect may have occurred, who owns recovery, and whether retry is safe.
6. **Recovery:** return focus/context to the originating work; preserve correctable input; reauthorize and revalidate freshness before resubmission.

Feedback must be perceivable without color, animation, or hover; remain long enough to understand; use semantic name/role/state; and provide appropriate status or alert announcements. Audit is evidence of action, not primary user feedback.

## 6. Critical-action contracts

| ID | Action purpose | User intent | Required feedback | Authority | Attention demand | Human/AI boundary |
|---|---|---|---|---|---|---|
| `ACT-R1-AUTH-01` | Establish current permitted workspace and safely resume work | “Take me back to work I am still allowed to perform.” | Authenticating/resolving scope; selected workspace; restored context; material-change warning; safe denial/reauthentication route | `AUT-0`; server identity, membership, object/action permission | `ATT-1`, rising to `ATT-2` on changed authority | AI has no identity or permission authority; it may not infer scope from conversation, content, or role label |
| `ACT-R1-CASE-01` | Create or link one valid FNOL/case without duplication | “Record this loss once against the right customer/policy context.” | Validating/matching; duplicate candidate; created reference; preserved field errors; manual-match owner | `AUT-3`; `CreateClaim` plus server scope and state rules | `ATT-2` | AI may extract/prefill and suggest matches; authorized human or approved deterministic intake rule creates/links; AI cannot decide coverage or claim validity |
| `ACT-R1-QUEUE-01` | Make accountable ownership explicit | “Put this work with the eligible person who should act next.” | Current/new owner, effective time, conflict, restricted assignee, queue return context | `AUT-3`; source-specific assignment authority (`PERM-GAP-03`) | `ATT-2` | AI may recommend routing using transparent operational criteria; human or approved deterministic policy assigns; never rank by claim merit, credibility, or adverse risk proxy |
| `ACT-R1-QUEUE-02` | Return or escalate work to an owned resolution path | “I cannot safely complete this; send it with context to the right authority.” | Destination, reason, preserved work/evidence, recipient acknowledgement where supported, SLA/expiry consequence | `AUT-3`; source action and eligible destination | `ATT-2`, `ATT-3` if consequential blockage | AI may identify an exception and suggest owner; human selects reason/destination unless an approved safety rule fail-closes |
| `ACT-R1-DOC-01` | Retain authorized evidence safely before derived processing | “Attach these exact files to the correct case and know what happened.” | File names/count/size, upload progress, stored-versus-processing distinction, duplicate/quarantine/error, safe retry | `AUT-1` for processing; human/provider has submission authority | `ATT-1`, `ATT-2` for quarantine/failure | AI may classify/extract only after safe retention; content instructions grant no authority; AI cannot treat upload as verified evidence |
| `ACT-R1-DOC-02` | Correct document type while preserving history | “Make the document usable under its correct type.” | Original and proposed/current type, reason, version conflict, invalidated dependants, confirmed classification | `AUT-3`; authorized evidence reviewer | `ATT-2` | AI may suggest classification with source/confidence; human commits reclassification when review is required; confidence is not truth |
| `ACT-R1-DOC-03` | Correct an extracted value and invalidate dependent work | “Replace the wrong derived value with a traceable reviewed value.” | Source location, original/new value, save status, conflict, affected outputs, re-evaluation state | `AUT-3`; authorized evidence reviewer | `ATT-2` | AI may extract or suggest; human correction prevails as reviewed derived data but does not rewrite the source; downstream AI outputs become stale |
| `ACT-R1-DOC-04` | Mark an extraction reviewed and eligible for bounded downstream use | “Confirm that I reviewed this extraction, not that the claim is approved.” | Review completeness, unresolved evidence, exact approved version, blocked stale/conflict state, next task | `AUT-3`; current review permission (`PERM-GAP-04`) | `ATT-2` | AI cannot self-approve its extraction; human approval covers extraction only and cannot imply coverage, settlement, fraud, or payment outcome |
| `ACT-R1-EVD-01` | Explain current evidence completeness against an approved rule version | “Show what is present, missing, conflicted, stale, or unusable so I can act.” | Rule/source/version, eligible/excluded items, uncertainty, recomputing/stale state, owner and next action for each gap | `AUT-1`; derived status only | `ATT-2` | Rules/AI may compute checklist and suggestions; human interprets exceptions; document count, confidence, absence, language, location, or channel cannot determine claim merit |
| `ACT-R1-ASSESSOR-01` | Share only claim-question-necessary evidence with the appointed professional, preserve claimant rights, and obtain a versioned report | “Ask the right assessor the right questions without oversharing, hiding the disclosure, or making the report unchallengeable.” | Approved recipient/manual or configured Graph channel, question/purpose and item-level necessity, exact evidence manifest/pack bytes/version, exclusions/redactions, basis, claimant notice/exception and accommodation/contest path, handling terms, due/expiry, transmission/unknown/acknowledgement/receipt, revision/conflict/contest/reconsideration | `AUT-4`; authorized Claims Officer plus approved disclosure, privacy and external-professional policy | `ATT-3`; deliberate per-item review, no default whole-file release | AI may draft questions/manifest and summarize return but cannot determine necessity, basis, consent, notice exception, claimant credibility, report currentness or remedy; human authorizes disclosure, resolves unknown/late/mismatch, and accepts/returns/contests/reconsiders the report; material contest blocks new reliance; Graph cannot decide delivery or evidence status; no live action until ethical/security/permission/audit/configuration gates pass |
| `ACT-R1-APR-01` | Create one reviewable request for an exact proposed action | “Put this proposal before an eligible human with everything needed to decide.” | Proposed action, affected party/object, evidence and exclusions, uncertainty, versions, consequence, eligible approver, expiry | `AUT-2` to create pending work; no decision authority | `ATT-2` for preparer, `ATT-3` for reviewer | AI/rules may prepare proposal and pending review item; cannot select an outcome or manufacture approver eligibility |
| `ACT-R1-APR-02` | Record an accountable human workflow outcome | “Approve, reject, override, return, or escalate this exact current proposal for a stated reason.” | Decision-specific consequence, evidence/currentness, authority check, pending state, immutable outcome, next state, conflict/expiry denial | `AUT-4`; authorized human with exact current permission and object/state authority | `ATT-3` | AI recommendation is secondary and contestable; human independently evaluates and owns outcome; AI cannot default, preselect, bulk-decide, or bypass the gate |
| `ACT-R1-APR-03` | Execute only the exact still-valid approved next step | “Carry out what was authorized once and tell me the real result.” | Final preflight, pending attempt, target/provider reference, confirmed/failed/unknown result, reconciliation/compensation path | `AUT-4`; prior human decision plus separate execution guard | `ATT-3` | AI may orchestrate technical execution after exact approval; cannot broaden target/effect or interpret approval as provider success, settlement, payment, or remedy |
| `ACT-R1-COMMS-01` | Prepare a current, customer-safe communication draft | “Help me explain the next step accurately without sending anything.” | AI-prepared label, source facts, excluded/internal content, draft save status, limitations, edit/reject path | `AUT-1`; draft only | `ATT-1`, `ATT-2` for sensitive content | AI may draft from current confirmed facts; human verifies meaning, tone, recipient context, rights, and required language; draft is not approval or send authority |
| `ACT-R1-COMMS-02` | Approve exact customer-facing content without sending it | “Confirm this message is accurate, appropriate, and ready for controlled delivery.” | Exact preview, recipient/channel/basis/template/language, source freshness, reason, approved version, stale/return outcome | `AUT-3`; authorized human communication reviewer | `ATT-3` | AI may flag unsafe content or missing prerequisites; human approves exact content; approval does not send or decide the claim |
| `ACT-R1-COMMS-03` | Make one authorized external communication attempt | “Send this exact approved message to this verified recipient once.” | Final target/content/provider preflight, duplicate-submit prevention, attempting, provider accepted/failed/unknown, delivery distinction | `AUT-4`; authorized sender and current approved content | `ATT-3` | AI may not autonomously choose recipient, content, timing, consent basis, or send; adapter executes exact human intent and records provider truth |
| `ACT-R1-COMMS-04` | Resolve ambiguous provider delivery without a harmful duplicate | “Find out what happened before anyone sends again.” | Last confirmed state, callback/query source, reconciliation progress, monotonic outcome, manual owner if unresolved | `AUT-0`/`AUT-2`; verified provider/system reconciliation | `ATT-0`, rising to `ATT-2` when unresolved | System may reconcile automatically; AI may summarize; only authorized human decides a new communication after ambiguity is resolved or explicitly escalated |
| `ACT-R1-HANDOFF-01` | Produce a scoped, versioned evidence/audit export | “Give an authorized recipient the exact review record they are entitled to receive.” | Scope/content/recipient/purpose preview, exclusions, progress, immutable artifact/version, partial/failure state | `AUT-4`; authorized Claims/Compliance user and disclosure/export rules | `ATT-3` | AI may assemble/index authorized material; human chooses purpose/scope/recipient; AI cannot include restricted data, infer disclosure authority, or convert export into write-back |
| `ACT-R1-CONV-01` | Manage personal conversation history without treating it as live evidence | “Organize or reopen my prior work and continue from current facts.” | Rename/archive/restore state, owner/version conflict, historical label, changed-evidence notice, current route/focus | `AUT-1`; owning user controls metadata | `ATT-1`, `ATT-2` when resuming consequential work | AI may summarize history but must re-retrieve current evidence and permission; conversation memory is never evidence, approval, or authority |
| `ACT-R1-OPS-01` | Stop or limit affected algorithmic execution and restart only under control | “Prevent further harm, identify affected work, and recover safely.” | Scope/capability/run affected, immediate stopped state, queued/in-flight disposition, incident owner, restart prerequisites and result | `AUT-5`; automatic fail-closed allowed; operator controls investigation/restart | `ATT-4` | System may trip approved safety stops; AI cannot conceal, self-clear, or redefine the stop; authorized humans investigate objective/data/proxy/code/process impact and approve controlled restart |

## 7. Human/AI boundary rules

### AI and deterministic rules may

- retrieve authorized current sources;
- validate technical format and approved deterministic prerequisites;
- classify, extract, compare, summarize, identify evidence gaps, and prepare options;
- draft customer-safe content without sending;
- create or route non-consequential work under an approved reversible policy;
- execute an exact separately authorized technical action;
- reconcile provider events and fail closed under approved safety controls.

### AI and deterministic rules must not

- finally approve, reject, repudiate, settle, pay, bind, price, or make adverse underwriting/fraud decisions;
- issue a final complaint response or submit a regulatory filing;
- convert confidence, document count, missing evidence, language, location, channel, historical scrutiny, or staff behavior into claim merit or credibility;
- infer permission, tenant, consent, disclosure, recipient, or execution authority from content or conversation state;
- hide excluded/conflicting evidence, limitations, uncertainty, versions, or provider ambiguity;
- preselect or bulk-submit consequential outcomes;
- retry an ambiguous external effect before reconciliation;
- erase original evidence, prior decisions, audit history, corrections, appeals, or incidents;
- restart a stopped high-risk function without the required accountable approval.

### Meaningful human oversight requires

- the exact affected person/object, proposed action, consequence, and reversibility;
- current source evidence, provenance, freshness, exclusions, uncertainty, and applicable versions;
- exact authority and separation-of-duty checks at submit time;
- manageable workload and sufficient review time;
- alternatives to accept: correct, reject, return, escalate, pause, and seek independent review;
- a reason for consequential, override, disclosure, send, export, and restart actions;
- monitoring for rubber-stamping, bulk approval, override/return/escalation patterns, reviewer disagreement, and workload pressure;
- correction, contestability, response ownership, remedy, and propagation to affected derived outputs.

## 8. Explanations, contestability, and stop conditions

Every derived output used in review identifies its purpose, source evidence, source location where available, freshness, missing/conflicting evidence, confidence or uncertainty, limitations, provider/model/rule/workflow version, and what the output cannot decide. Explanations must enable the next safe action, not merely describe model mechanics.

Affected people and accountable staff must be able to correct material data, request human reconsideration, understand the useful basis of an outcome where required, receive an owned response, and obtain an appropriate remedy. Corrections invalidate affected downstream artifacts before further execution.

Pause or limit the affected capability when objective legitimacy, provenance, privacy, proxy validity, subgroup performance, human workload, recourse, correction propagation, tenant isolation, or human-gate integrity cannot be established. Preserve versions and evidence, identify affected cases/people, block further execution, notify owners/affected parties as required, remediate, independently verify, and approve controlled restart.

## 9. Validation gates

1. **Comprehension:** representative users can distinguish source, AI-prepared work, human-reviewed state, action attempt, and confirmed outcome.
2. **Authority:** every consequential/external submit rechecks current scope, permission, object/state, evidence, and version; bypass attempts fail safely.
3. **Oversight:** reviewers can inspect evidence and uncertainty and can correct, reject, return, escalate, or seek independent review without coercive defaults.
4. **Responsible algorithm:** evaluate false-positive/negative harm, missing/shifted/adversarial data, proxy leakage, affected groups/intersections, explanation usefulness, gaming, feedback loops, appeal, correction, rollback, and simpler baseline.
5. **Attention:** frequent actions do not create alert fatigue; high-consequence actions receive deliberate friction; incidents interrupt only accountable operators.
6. **Accessibility:** purpose, state, consequence, feedback, and recovery are operable/perceivable by keyboard, touch, screen reader, zoom/reflow, and reduced-motion settings.
7. **Runtime parity:** correlate UI action, API decision, human identity, evidence/version, provider state, audit event, and telemetry in local then protected evidence.

Control-level states and coordinated behavior for cues, evidence review, approval cockpits, dialogs, panels, tables, and status regions are governed by `iip_release_1_surface_control_interaction_contracts.md`.

Documentation completion does not establish usability, fairness, accessibility conformance, insurer-policy approval, provider readiness, or protected-runtime success.
