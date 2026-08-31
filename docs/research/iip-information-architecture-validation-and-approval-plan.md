# IIP Information Architecture Validation and Approval Plan

| Field | Value |
|---|---|
| Date | 2026-08-07 |
| Status | Ready for recruitment and named-owner assignment; no sessions conducted |
| Decision | Retain, revise or reject IA v1.0 grouping, labels, role visibility and handoffs; inform named stakeholder approval without manufacturing consensus |
| Approval artifact | `../03-design-architecture/iip_information_architecture_stakeholder_approval_pack.md` |

## Objective and decision use

Evaluate whether representative insurance practitioners can predict where work lives, distinguish records/work items/conversations, understand permission-dependent visibility, and complete critical findability/handoff tasks. Findings may retain labels/grouping, change them, split role landings, revise routes/handoffs, or reject the proposed structure.

Internal approval and practitioner validation are separate. Stakeholders own risk and scope decisions; participants supply evidence about work, terminology and usability.

## Existing evidence and uncertainty

| Type | Statement |
|---|---|
| Evidence | IA v1.0 is reconciled to current routes, local contracts and screen ownership. |
| Evidence | Repository tests can support source consistency but do not establish practitioner fit. |
| Assumption | Work-centered grouping is more predictable than capability or organization-chart grouping. |
| Assumption | Canonical labels transfer across insurers, roles and device/network contexts. |
| Unknown | Role landing priorities, local terminology, queue ownership, delegation and cross-surface recovery. |
| Unknown | Whether permission-filtered omission is understood as access control rather than data loss. |
| Constraint | No real customer/claim data, secrets, credentials or identifiable raw notes enter Git. |

The current corpus is insufficient for research synthesis: no participant card-sort, tree-test, terminology, accessibility or authenticated walkthrough evidence exists. No participant finding, quote, count or consensus is claimed.

## Research questions

1. How do practitioners group representative records, work, communications, oversight and platform tasks without seeing the proposed hierarchy?
2. Which labels do they use for the same concepts, and where do identical words carry different authority or state meanings?
3. Where do they first look for each critical task, and what reasoning produces wrong turns?
4. How do roles differ in landing priorities, queue ownership, handoffs and escalation?
5. How do users interpret missing navigation, restricted states, changed access and no-authorized-results messages?
6. Can users distinguish Search retrieval, Command Center analysis, staff conversations, WhatsApp threads and customer communications?
7. What context must survive Search/My Work/WhatsApp/conversation handoffs and interruption?
8. What device, network, language or accessibility conditions materially change grouping or navigation needs?

## Methods and sequence

| Stage | Method | Decision contribution |
|---|---|---|
| 1 | Semi-structured terminology and recent-event interview | Actual language, artifacts, handoffs and authority boundaries |
| 2 | Open card sort using representative objects/tasks | Participant-created categories and labels |
| 3 | Closed card sort against IA v1.0 | Fit, ambiguity, disagreements and role variance |
| 4 | Text-only tree test using the canonical sitemap | First choice, destination, directness, time, confidence and failure path |
| 5 | Moderated scenario walkthrough | Permission visibility, Search/recent-work relationships, interruption and handoffs |
| 6 | Named stakeholder decision review | Accept evidence, record conditions/conflicts, approve/reject exact IA version |

Card sorting informs a hierarchy; it does not prove final navigation quality. Interviews explain behavior but do not establish prevalence. The tree test evaluates findability, not market demand or implementation correctness.

## Participants and sampling

Recruit practitioners with recent direct experience as claims handler, claims supervisor/authorized approver, document reviewer/assessor, customer service or complaints officer, compliance/audit reviewer, and integration/tenant administrator. Include materially different institutions where access permits; capture role behavior, authority, institution type, geography, workflow frequency, device/network and accessibility/language context without naming the person or employer in Git.

Use iterative purposive sampling: begin with enough diversity to expose role/institution differences, then continue until new sessions no longer materially change the decision map or a practical recruitment limit is reached. Record the limit. Do not claim statistical representativeness or convert qualitative mention counts into market prevalence.

Exclude participants whose only knowledge is vendor sales material or hypothetical future responsibility. A stakeholder may also be a practitioner only when both capacities are recorded separately.

## Recruitment and ethics

- Recruit through approved insurer, broker, assessor and professional contacts; avoid managerial pressure on staff participation.
- Explain purpose, voluntary participation, withdrawal, recording choice, privacy limits, use of anonymized findings and retention/deletion before starting.
- Recording is opt-in. A refusal to record does not exclude participation when secure notes are feasible.
- Use synthetic scenarios and de-identified cards. Stop participants from sharing claim/customer identifiers, credentials, internal fraud details or protected commercial information.
- Store recordings/raw notes only in an approved restricted repository. Git receives anonymized metadata, redacted atomic observations and synthesis.
- Offer accessibility accommodations, extra time, remote/in-person choice and low-bandwidth alternatives.

## Neutral interview and facilitation guide

### Opening

“We are studying how insurance operational work is organized and found. We are testing the structure and words, not you. There are no correct answers. Participation is voluntary; you may skip anything or stop. Please avoid identifying customers, employees or confidential cases. May we take notes? May we record?”

### Recent-event questions

1. Walk me through the last time you had to find an assigned claim or case that needed action.
2. What did you open first, and what told you where to go next?
3. Tell me about the last handoff that crossed claims, documents, customer service, approval or compliance work.
4. What information was lost, repeated or misunderstood?
5. When access changed or something disappeared, what did you think had happened?
6. Show or describe the non-sensitive labels your workplace uses for these objects and queues.

Do not ask whether participants “like” IIP labels or would use the product. Probe past behavior, sequence, artifacts, exceptions, cost and existing workaround.

### Adaptive probes

- “What happened immediately before and after that?”
- “What exact word did your team use?”
- “Who had authority at that point?”
- “How did you know the work was waiting, complete or delivered?”
- “What changed on a slow connection, mobile device or interrupted shift?”
- “Can you remember an exception where this worked differently?”

Ask one question at a time, allow silence, reuse participant language and distinguish observation from inference. Do not praise an answer, reveal the desired placement or turn the session into a product pitch.

## Tasks and capture

Use the twelve canonical tasks in IA v1.0 §12, including assigned evidence work, WhatsApp intake/thread reopening, extraction correction, approval return, customer lookup, integration health, audit pack, workflow/run distinction, tenant configuration, recent work/change recognition and Search return context.

Capture per task: first choice, final destination, direct/indirect/fail, time, backtracks, confidence, label interpretation, permission expectation, consequence of error and participant explanation. Capture card placements individually before aggregating.

Use note tags: `OBS` observed behavior, `SAY` participant statement, `ART` artifact reference, `TERM` exact term, `AUTH` authority boundary, `HANDOFF`, `ACCESS`, `A11Y`, `NETWORK`, `CONTRA`, `MOD` moderator influence and `FOLLOWUP`.

## Risk and contingency protocol

- Pause and redirect if confidential or personal information appears; remove it from notes.
- Stop recording on request without argument.
- If the prototype/runtime fails, continue only with the text tree/card materials and label the method change.
- If a manager observes, ensure the participant can decline and do not report individual performance.
- If distress or employment risk emerges, stop the task and protect participant agency.

## Analysis and synthesis

Maintain: `source → atomic observation → pattern → interpretation → insight → implication → decision`.

Each evidence row records source/session ID, date, geography, anonymized participant role/context, method, observation, confidence rationale, limitation and decisions affected. Preserve contradictions, minority safety/accessibility cases, role variance and moderator influence.

| Finding ID | Sources | Observation/pattern | Contradicting evidence | Interpretation | Confidence/rationale | IA implication | Decision owner |
|---|---|---|---|---|---|---|---|
| — | — | No sessions yet | — | Synthesis prohibited until evidence exists | — | — | — |

## Decision rules

- Critical Release 1 tasks use the existing IA gate: at least 90% unassisted completion and zero unrecovered consequential error. Report role/institution results separately; do not average away a severe failure.
- A label/grouping change is required when evidence shows repeated wrong destination or unsafe authority interpretation and no role-safe clarification resolves it.
- A high-consequence minority accessibility, privacy or permission failure can block approval even when aggregate task success is high.
- Conflicting evidence produces a condition, targeted follow-up or role-specific design—not invented consensus.
- Stakeholder approval records the exact evidence reviewed. A named approver may approve, approve with conditions or reject; unresolved blocking objections remain visible.

## Stop criteria and limitations

Stop a wave when all planned role contexts have evidence and additional sessions no longer materially change the decision map, or when recruitment/time limits are reached and documented. Re-run affected tasks after material hierarchy, label, permission, Search, queue or handoff changes.

Findings remain context-bound. They cannot establish market demand, adoption, pricing, production authorization correctness or accessibility conformance by themselves.

## Immediate next actions

1. Name Product Research owner, moderator, note-taker and seven functional approvers.
2. Approve consent/data-handling protocol and restricted raw-data location.
3. Recruit the first diverse practitioner wave and schedule sessions.
4. Pilot the cards/tasks internally to remove ambiguity without counting pilot behavior as practitioner evidence.
5. Conduct sessions, register evidence IDs, synthesize with provenance and convene the approval review.

No research session or named stakeholder decision was performed while creating this plan.
