# IIP User, Business, and Market Research Program

| Field | Value |
|---|---|
| Version | 0.9 |
| Date | 2026-08-05 |
| Status | Proposed; fieldwork and buyer validation not started |
| Decision owners | Product, Insurance, Operations, Security, Engineering, Commercial |
| Geography | Uganda first; later African-country configuration studies are separate |
| Product boundary | Release 1: non-injury motor-comprehensive own-damage evidence review and approval preparation |

## 1. Decisions this program must inform

| ID | Decision | Owner | What changes |
|---|---|---|---|
| RES-D01 | Is the Release 1 workflow a material, recurrent, software-addressable problem? | Product + Insurance + Operations | Advance, narrow, reframe, or stop the wedge. |
| RES-D02 | Which actors, role labels, responsibilities, authority boundaries, and handoffs are real? | Product + Client Operations | Personas, RBAC vocabulary, navigation, queues, workflow, training. |
| RES-D03 | What device, network, language, literacy, and accessibility conditions must the experience support? | Product + Design + Security | UX requirements, content, performance, draft/resume, test matrix. |
| RES-D04 | Which institutions form the reachable initial segment? | Commercial + Product | Account qualification, positioning, recruitment, sales focus. |
| RES-D05 | What must a buyer approve before a pilot or production purchase? | Commercial + Security + Engineering | Deployment, security, integration, data, support, procurement package. |
| RES-D06 | Is there credible paid-pilot and production demand at a sustainable price? | Commercial + Finance | Pilot offer, pricing metric, packaging, target cost, go/no-go. |
| RES-D07 | Which measured outcomes constitute value and ROI? | Product + Client Sponsor + Finance | Baseline contract, pilot design, ROI model, renewal decision. |
| RES-D08 | Which findings become requirements or roadmap changes? | Product + Engineering | PRD/UX/roadmap updates with provenance and approval. |

Evidence that would contradict the current strategy includes: the problem being rare or mostly external; core-system configuration solving it sufficiently; another work surface increasing duplication; insufficient source data; unacceptable legal/security risk; no accountable buyer or budget; no paid-pilot commitment; or implementation/support cost exceeding validated value.

## 2. Existing evidence and limits

| Evidence | What it supports | What it cannot support |
|---|---|---|
| Uganda regulator, privacy, financial-crime, and industry sources | Market perimeter, reported premiums/claims/complaints, public obligations, licensed-player counts. | Actual insurer process, compliance quality, budget, procurement, causal delay drivers. |
| Official competitor/vendor material | Publicly claimed capability, deployment/category position, visible product emphasis. | Independent performance, Uganda fit, total cost, adoption, customer satisfaction. |
| IIP code, tests, and documents | Implemented capability, intended contracts, feasibility evidence. | User value, market demand, usability, insurer readiness, willingness to pay. |
| Existing product-neutral questionnaire and desk research | Coverage scaffold and fieldwork instrument. | Findings, validated personas, prevalence, ROI, product-market fit. |

No participant corpus currently supports persona, buyer, pricing, pilot-readiness, or adoption claims. Do not synthesize fictional findings to fill that gap.

## 3. Method sequence

### Stage A — Current desk research

Use primary regulator/statistical sources for market and obligations; official vendor sources for capability comparison; public cloud/marketplace documentation for deployment mechanics. Refresh market counts annually and regulatory/cloud facts before any proposal.

Output: cited desk report, evidence-register rows, explicit unknowns.

### Stage B — Practitioner discovery interviews

Purpose: reconstruct actual recent claims/evidence work, exceptions, handoffs, tools, authority, customer communication, cost, and failure.

Target: 12–18 practitioners across at least 4 institutions, selected by recent behavior rather than title. Include claims handlers, authorized approvers, document reviewers, customer service/complaints, supervisors, and assessor/adjuster participants. Stop when new sessions no longer materially change the workflow model and at least two disconfirming/exception cases have been examined; do not claim saturation mechanically.

Use the modular guide in `insurance-operations-discovery-questionnaire.md`. Ask one recent-event question at a time; do not administer all 222 prompts as one interview.

### Stage C — Contextual inquiry and redacted artifact walkthrough

Purpose: compare stated work with actual sequence, tools, interruptions, workarounds, and handoffs.

Target: 6–8 sessions across claims intake, evidence review, approval preparation, supervision, and customer-update work.

Observe or reconstruct one ordinary, delayed, corrected, and reopened case where possible. Never request production credentials or copy live customer evidence. Capture:

```text
time | actor | observable action | artifact/tool | information used
     | interruption | workaround | outcome | direct statement | interpretation
```

Keep observation and interpretation separate. Stop when privacy, safety, consequential work, or bystander consent boundaries are crossed.

### Stage D — Buyer and gatekeeper interviews

Purpose: validate purchase process, authority, deployment, integration, data, change, cost, and commitment.

Target: 8–12 interviews across 4–6 institutions, including Head of Claims/COO, CIO/IT, Security, Compliance/Legal/DPO, Procurement, and Finance. A single executive interview is not buyer validation.

Anchor questions in the last comparable purchase, renewal, integration, audit, or failed transformation:

1. What triggered the last operations or claims-technology purchase?
2. Who first documented the problem and quantified its cost?
3. Which budget paid for evaluation, implementation, cloud, support, and change?
4. Who influenced, blocked, approved, signed, secured, implemented, and owned renewal?
5. Which alternatives were considered, including process change, core configuration, outsourcing, low-code/RPA, and doing nothing?
6. What evidence was required at business-case, security, legal, procurement, architecture, and user-acceptance gates?
7. Which data residency, processor, retention, audit, incident, identity, encryption, and support requirements applied?
8. Which deployment and publisher-access models were acceptable or rejected?
9. Which integrations, data exports, migration, and exit provisions were non-negotiable?
10. What changed for staff, and how were workload, training, incentives, and resistance handled?
11. What did the organization actually spend, and what costs were underestimated?
12. What result determined renewal, expansion, or termination?

After evidence of material pain, ask for proportional commitment: a follow-up with the buying group, redacted baseline data, security review, named pilot owner, written pilot criteria, and finally a paid pilot. Compliments and hypothetical willingness are not commitments.

### Stage E — Concept and task validation

Target: 8–12 representative users across at least two rounds.

Test without coaching:

- find current owned work and reason for priority;
- determine whether a file is evidence-ready;
- inspect source/version and identify a contradiction;
- request or route missing evidence;
- recover after interruption or evidence correction;
- prepare a recommendation and hand it to an authorized human;
- distinguish proposed, approved, executed, failed, and historical state;
- produce or inspect an audit/appeal pack.

Capture task success, critical error, assistance, time, evidence inspection, wrong reliance, correction, and role-language mismatch. Prototype usability does not validate market demand.

### Stage F — Structured survey after qualitative discovery

Use only after interviews establish valid constructs and vocabulary. Attempt broad coverage across the 19 licensed non-life insurers rather than pretending a small convenience sample is representative. Recruit multiple relevant roles per institution and report institution and respondent counts separately.

Candidate constructs: workflow exposure, recent evidence-gap occurrence, observed delay/rework, system fragmentation, handoff failure, baseline availability, deployment constraints, change readiness, and buying-stage commitment. Pretest through cognitive interviews; define missingness, duplicate, speed, exclusion, and subgroup privacy rules before launch.

Survey results may quantify the reached sample. They do not establish national prevalence when the sampling frame, response rate, or nonresponse bias is inadequate.

### Stage G — Paid pilot and outcome validation

Use a concurrent comparison or carefully matched pre/post case set when feasible. Segment case mix and external delay. Record changes in provider, staffing, policy, seasonality, and workload. Do not attribute all observed change to IIP.

## 4. Participant and context validation matrix

| Hypothesis to validate | Required evidence | Product impact if wrong |
|---|---|---|
| “Claims Officer” is the correct primary label | Local titles, actual responsibilities, delegated authority, queue ownership across institutions. | Navigation, permissions, training, research sampling. |
| Document review is a distinct role | Observed assignment and correction behavior, staffing model, volume. | Merge into handler workflow or retain specialist workspace. |
| Authorized approver is distinct from handler | Delegation schedules, recent approval case, separation-of-duty rules. | Approval routing and human gate design. |
| Assessor/adjuster is an external contributor | Contract/handoff, artifact exchange, access constraints. | External submission versus licensed seat. |
| Desktop/laptop is primary | Device used for each critical task and reason. | Responsive priority, mobile scope, test devices. |
| Reliable office connectivity is available | Observed latency/outage/recovery and branch/field differences. | Load order, draft model, upload resume, offline boundary. |
| English staff UI is sufficient initially | Language used in systems, documents, calls, templates, policy clauses, and customer messages. | Content, search, OCR, translation, review requirements. |
| Current accessibility assumptions are sufficient | Keyboard/screen-reader/zoom use, disability/access needs, literacy, environmental constraints. | WCAG target, components, content, accommodations. |
| Handoffs follow documented role boundaries | Recent cases, sender/receiver, payload, wait, bounce-back, authority, system. | Work-item model, SLA, notifications, audit. |

Do not invent demographic personas. Build evidence-based behavioral archetypes only after the corpus contains repeated goals, tasks, constraints, authority, context, and contradictions. Preserve institution-specific variants.

## 5. Business and market validation

### Institution qualification

A Release 1 prospect must be tested against:

- licensed non-life operation with material motor-comprehensive volume;
- recurrent evidence-review and approval-preparation work;
- accountable claims and executive sponsor;
- measurable baseline data or reconstructable cases;
- acceptable read/integration path to policy, claim, document, and audit sources;
- security, privacy, data-transfer, deployment, support, and exit feasibility;
- staff capacity for configuration, UAT, training, and weekly pilot review;
- budget authority and paid-pilot path.

Failure on one criterion does not automatically reject the institution, but it changes pilot scope, evidence strength, cost, or viability.

### Market-validation commitments

| Level | Evidence | Interpretation |
|---|---|---|
| Interest | Meeting, positive comment, questionnaire completion | Learning access only; no demand claim. |
| Time | Follow-up booked; buying group joins; artifact walkthrough allowed | Problem relevance signal. |
| Reputation | Introduction to buyer, security, procurement, or another insurer | Stronger organizational signal. |
| Data/work | Baseline data, workflow owners, security questionnaire, integration workshop | Pilot preparation signal. |
| Money | Paid discovery/pilot, signed order, or procurement action with budget | Commercial demand signal. |
| Renewal/expansion | Continued paid use tied to measured outcomes | Strongest current value signal. |

## 6. ROI and outcome evidence contract

Release 1 ROI must use target-insurer baselines, not industry premium volume.

```text
annual labor value
  = eligible cases × verified minutes saved per case / 60 × loaded hourly cost

annual rework value
  = avoided repeat steps × verified cost per repeat step

annual control/recovery value
  = verified attributable avoided loss or recovered value

total annual benefit
  = labor value + rework value + control/recovery value

total annual cost
  = implementation + subscription + customer Azure resources
    + AI/document/channel usage + integration + security/compliance
    + training/change + internal administration + support

ROI = (total annual benefit - total annual cost) / total annual cost
payback months = total initial cost / verified monthly net benefit
```

Exclude acquisition uplift, retention, leakage, fraud savings, or working-capital benefit unless the pilot measures a defensible causal contribution. Report sensitivity, case mix, missing data, and attribution limits.

## 7. Pricing and packaging validation

Test in this order:

1. Last comparable spend and current cost of the problem.
2. Budget owner, budget source, approval threshold, and procurement calendar.
3. Paid fixed-scope discovery/pilot offer with capped usage and explicit success criteria.
4. Production structure: annual platform/operations fee + implementation/integration + customer Azure/provider usage + support tier.
5. Renewal/expansion decision against measured value and support cost.

Do not ask “How much would you pay?” before establishing past spend, pain, and purchase process. Seat pricing alone is a weak hypothesis for cross-functional workflow value; unlimited AI/document processing creates uncontrolled cost. Private Marketplace plan mechanics do not prove customer willingness or procurement acceptance.

## 8. Analysis and synthesis

Use the repository chain:

```text
evidence ID -> atomic observation -> pattern -> interpretation -> insight
            -> need/opportunity -> implication -> decision
```

For every finding record supporting and disconfirming evidence, roles and institutions represented, method, confidence rationale, minority/access-critical cases, and limitations. Counts describe this corpus only.

Opportunity format:

```text
Enable [actor] to [desired progress] in [context]
despite [evidenced barrier], while preserving [guardrail].
```

Do not calculate opportunity scores from invented importance, reach, satisfaction, or willingness-to-pay data.

## 9. Decision rules

These are proposed thresholds and require owner approval before fieldwork.

### Advance Release 1

- recent cases across multiple institutions show recurrent, consequential, software-addressable evidence/continuity work;
- representative users complete critical tasks without unsafe reliance or unacceptable duplication;
- at least one institution supplies a named sponsor, baseline access, buying-group participation, security/integration path, and paid-pilot commitment;
- modeled target cost fits within a credible share of verified value;
- no unresolved legal, privacy, human-authority, or security blocker.

### Narrow or rethink

- value is concentrated in one sub-step, role, product variant, or non-chat workflow;
- core-system configuration, queue redesign, staffing, or partner management solves most of the problem more cheaply;
- external delay dominates and IIP cannot change or expose it usefully;
- customer-owned Azure deployment or integration makes the initial segment too small or costly;
- users cannot safely inspect/correct evidence under realistic workload or connectivity.

### Stop the current wedge

- no recent cases demonstrate material pain;
- no accountable buyer can fund or procure a pilot;
- required evidence cannot be accessed lawfully or operationally;
- safe human accountability cannot be maintained;
- realistic cost exceeds credible value with no narrower viable outcome.

## 10. Research-to-product change control

No finding becomes a requirement automatically.

1. Add evidence-register rows.
2. Produce redacted synthesis with contradictions and confidence.
3. Propose the exact requirement/roadmap change and affected IDs.
4. Review with Product, Insurance, Operations, Security, Engineering, and Commercial as applicable.
5. Record approval, rejection, or conditional acceptance.
6. Update the owning PRD/UX/security/architecture/commercial/roadmap documents and traceability matrix.
7. Add or change acceptance tests and outcome telemetry.
8. Set review/expiry trigger.

## 11. Fieldwork readiness gaps

- Named Product Research owner and approved secure research store.
- Approved consent, retention, deletion, withdrawal, recording, and incentive terms.
- Named target institutions and behavioral recruitment screeners.
- Participant accessibility and language accommodations.
- Native insurance-domain review of instruments.
- Baseline data dictionary and secure extraction method.
- Buyer, security, procurement, and integration interview access.
- Pre-approved decision thresholds and conflict-resolution owner.
