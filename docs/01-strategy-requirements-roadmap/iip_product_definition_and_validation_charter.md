# IIP Product Definition and Validation Charter

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Staff application | IIP Operations |
| Version | 0.10 |
| Date | 2026-08-05 |
| Status | Proposed for cross-functional approval; value proposition not yet participant-validated |
| Product owner | Product Lead — named person required |
| Decision owners | Product, Insurance, Operations, Security, Engineering |
| Review cadence | Monthly during discovery; at every pilot gate; after material regulatory or market evidence |

## 1. Authority and completion rule

`AGENTS.md` remains the highest repository authority. Once approved, this charter is the canonical product-definition layer beneath it. Older documents remain useful for detailed requirements and history, but their broad claims, stale runtime profiles, and unvalidated assumptions do not override this charter or `AGENTS.md`.

Product definition is complete only when all gates below are evidenced. Document presence alone is not completion.

The governed research layer is [the research repository](../research/README.md), [the user, business, and market research program](../research/iip-user-business-market-research-program.md), and [the evidence register](../research/iip-research-evidence-register.csv). Desk research may change hypotheses, but only approved decision records change product authority.

| Gate | Current state | Completion evidence |
|---|---|---|
| Purpose and problem | **Proposed** | Owners accept the evidence-aware problem frame. |
| Identity and assurance boundary | **Defined** | Formal name and platform ceiling match `AGENTS.md`; five-owner sign-off recorded. |
| Uganda First / Africa Configurable | **Defined, partly researched** | Uganda desk evidence plus target-institution participant validation; configuration boundaries accepted. |
| Release 1 wedge and portfolio | **Defined** | Scope signed by Product, Insurance, Operations, Security, Engineering. |
| Product principles | **Proposed** | All five functions approve or record a dated objection and resolution. |
| Outcome metrics | **Instrument plan proposed; baselines missing** | Pre-pilot baseline dataset accepted; targets and owners signed. |
| Value proposition | **Not validated** | Representative buyers and users demonstrate demand and workflow value. |
| Research-driven revision | **Process defined** | Research log shows decisions retained, changed, or killed from evidence. |

### Current market boundary

The 2026 Uganda register lists 144 authorized insurance-market players, including 19 non-life insurers. Those figures describe a regulated ecosystem and an eligible-account ceiling; they are not IIP TAM, SAM, SOM, reachable revenue, demand, or willingness to pay. The initial reachable segment remains **unquantified** until a named account list is filtered by Release 1 fit, Azure/deployment compatibility, data readiness, procurement access, sponsor access, and willingness to run a measured paid pilot.

## 2. Product purpose and problem frame

### Decision

Define and validate the smallest commercially credible IIP product that improves Uganda motor-claims evidence review while preserving human accountability and a path to configurable African deployments.

### Purpose

IIP exists to help insurance institutions turn fragmented operational inputs into evidence-linked, reviewable work so authorized people can make faster, better-supported, auditable decisions.

### Evidence-aware problem statement

Claims officers handling non-injury motor claims in document-heavy, multi-party workflows struggle to assemble a complete and current claim file, locate decisive evidence, resolve contradictions, coordinate assessors and customers, and prepare an approval-ready recommendation. This creates avoidable delay, rework, weak communication, and difficult audit or appeal reconstruction. Uganda regulator evidence establishes claims complaints, delay, communication, and motor relevance; the actual frequency, cost, and causes inside a target insurer remain unmeasured.

### Desired user progress

A claims officer can see what is known, missing, conflicting, stale, or unverified; inspect every decisive source; coordinate the next evidence action; and hand an approval-ready recommendation to the correct authorized human without reconstructing the case across disconnected tools.

### Business outcome

A pilot insurer reduces active handling and evidence-gap delay while improving traceability, customer-update timeliness, and safe human oversight—without replacing its core claims system.

### Constraints and non-goals

- IIP is an operations and intelligence layer, not a policy administration, claims payment, accounting, ERP, or general CRM replacement.
- AI never approves, repudiates, settles, pays, binds, prices, accuses fraud, submits a regulatory filing, or bypasses required approval.
- Release 1 does not include bodily injury, fatality, contested liability, medical, life, workers' compensation, reinsurance, or autonomous write-back happy paths.
- Customer channels feed staff work; Release 1 is not a general customer self-service portal.
- No deployed mock, seeded-demo, fallback, hardcoded-insurer, or fake-success behavior.
- English may be the initial staff-interface language, but sources, derived artifacts, and customer communications must not be architected as English-only.

### Facts, hypotheses, and alternative explanations

| Type | Statement | Status / test |
|---|---|---|
| Fact | Uganda's regulator reports material motor activity and complaints involving unpaid/repudiated claims, delayed settlement, and poor communication. | Primary-source desk evidence. |
| Fact | Published Uganda guidance and insurer forms show a multi-document, multi-party motor claims process. | Primary-source desk evidence; institution variation expected. |
| Fact | The repository implements real claims, documents, approvals, audit, tenant scope, and provider-state foundations. | Code/current-state evidence; not proof of user value. |
| Hypothesis | Evidence assembly and handoff are important delay/rework drivers at a target insurer. | Observe recent cases and baseline event data. |
| Hypothesis | Chat plus structured work blocks and an evidence rail improves task completion. | Moderated prototype and pilot comparison. |
| Hypothesis | A mid-to-large general insurer has budget and urgency for the wedge. | Buyer interviews, procurement mapping, paid-design-partner test. |
| Alternative | Delay is mainly caused by external assessor capacity, payment liquidity, authority limits, or core-system policy rather than evidence handling. | Case-timeline decomposition; do not over-credit IIP. |
| Alternative | A workflow queue or core-system configuration change creates most value without a chat surface. | Compare low-fidelity workflow-only and Command Center prototypes. |

## 3. Formal product identity and assurance boundary

### Identity

| Item | Canonical definition |
|---|---|
| Formal name | **Insurance Intelligence Platform (IIP)** |
| Short name | **IIP** |
| Staff application | **IIP Operations** |
| Category | Human-supervised insurance operations platform |
| Market direction | Uganda First; Africa Configurable |
| Release 1 wedge | Non-injury motor comprehensive own-damage evidence review and approval preparation |
| Commercial deployment priority | Azure Managed Application / Partner Center private plan into the insurer's Azure subscription |
| One-line story | For Uganda general insurers whose motor-claims teams reconstruct evidence across fragmented channels and documents, IIP prepares a complete, source-traceable claim review for an accountable human—without replacing the core claims system or automating the decision. |

### Human-supervised assurance boundary

| AI may prepare | Only an authorized human may decide or execute |
|---|---|
| Extract and classify documents; identify gaps and contradictions; retrieve approved evidence; build chronology; summarize; draft; recommend; route; prepare an approval or evidence pack. | Claim approval or repudiation; settlement approval; payment release; policy binding; premium setting; underwriting outcome; fraud determination; final complaint response; regulatory filing; consequential write-back. |

The boundary is a platform ceiling, not a tenant preference. Tenant policy may narrow capability but never widen it. Every material AI or workflow action must be attributable, tenant/workspace-scoped, evidence-linked where applicable, reviewable, and auditable.

## 4. Strategy kernel

### Diagnosis

The critical obstacle is not lack of another claims system or chatbot. It is the weak continuity between fragmented evidence, the current claim state, the next accountable action, and the human decision record—under local workflow, trust, integration, and infrastructure constraints.

### Guiding policy

Win a narrow Uganda motor-claims evidence-review wedge by integrating beside existing systems, making source and limits visible, and proving measurable work improvement under a non-bypassable human decision ceiling. Generalize only through validated configuration boundaries.

### Coherent actions

1. Validate the workflow with recent redacted motor-comprehensive cases and representative staff.
2. Baseline handling, waiting, rework, evidence gaps, communication, and audit reconstruction before pilot claims.
3. Deliver evidence-complete review, assessor/customer handoffs, recommendation, approval, and audit as one bounded outcome.
4. Keep channels, policy lookup, integrations, and reporting only where they serve that outcome.
5. Run a controlled pilot with target metrics and disconfirming cases.
6. Expand by validated adjacent workflow and country configuration—not by module accumulation.

### Explicitly not doing now

- Competing head-on as a full core claims suite.
- Selling “autonomous insurance agents” as the value proposition.
- Activating built-ahead Fraud, Underwriting, Billing, or Agent Registry modules as Release 1 value claims.
- Assuming WhatsApp-first, English-only, a universal document checklist, or universal role titles without institution evidence.
- Promising ROI from the old illustrative USD 16.5M model; it has no client baseline.

## 5. Target institutions, buyers, users, and noncustomers

### Institution priority

| Priority | Institution | Release rationale | Status |
|---|---|---|---|
| P1 | Mid-to-large Uganda general insurer with meaningful motor-comprehensive volume | Bounded workflow, multiple operational roles, measurable volume, stronger budget ownership. | Target hypothesis; named design partner missing. |
| P2 | Smaller general insurer or insurer group sharing similar workflow | Tests configurability and lower-cost packaging after wedge proof. | Later validation. |
| P3 | Broker, TPA, bancassurance operator | Valuable handoffs, but different authority, data, and commercial model. | Adjacent, not Release 1 buyer. |
| P4 | Health, life, microinsurance, reinsurer | Different evidence, sensitivity, actors, and regulation. | Later discovery; not a motor variant. |

### Buyer and gatekeeper roles

| Role | Job / concern | Evidence required |
|---|---|---|
| Executive sponsor / COO / Head of Claims | Faster, more controlled operations and visible bottlenecks. | Baseline-to-pilot outcome change and adoption. |
| Claims Manager | Complete files, manageable queues, clear authority and escalations. | Workflow fit, exception coverage, no hidden work. |
| CIO / IT / Cloud | Integration, security, identity, supportability, deployment truth. | Architecture, provider, tenancy, recovery, and operational evidence. |
| Compliance / Legal / DPO | Accountability, lawful processing, retention, contestability, audit. | Control mapping and reviewed evidence. |
| Procurement / Finance | Affordable acquisition and operating cost with credible benefit. | Target-cost and commercial model using real volume. |

### Release 1 users and participants

| Type | Role | Release 1 relationship |
|---|---|---|
| Primary user | Claims Officer | Owns evidence review, chronology, gaps, handoffs, and recommendation. |
| Decision user | Authorized Approver | Makes the consequential decision within delegated authority. |
| Supporting user | Customer Service / Complaints Officer | Coordinates approved, customer-safe status and complaint continuity. |
| External contributor | Loss Assessor / Loss Adjuster | Supplies assessment/investigation evidence through controlled handoff. |
| Represented party | Policyholder, insured, claimant, broker/agent, police, witness, garage/repairer | Appears in evidence and communications; not automatically an IIP seat. |

### Noncustomer map

| Tier | Why current options are rejected or endured | What could change behavior | Cheapest test |
|---|---|---|---|
| Soon-to-be | Teams tolerate email, shared drives, spreadsheets, and core screens but face rising volume or audit pressure. | Show less reconstruction and faster approval-ready files without system replacement. | Reconstruct five recent cases with a concierge evidence pack. |
| Refusing | Institutions distrust AI, lack clean data, fear integration or regulatory exposure, or cannot fund enterprise transformation. | Human ceiling, local deployment, phased read-only integration, measurable pilot, truthful `Not Configured`. | Security/operations walkthrough plus bounded paid pilot offer. |
| Unexplored | Smaller institutions and intermediaries have never considered an intelligence layer or lack Azure maturity. | Lower-cost managed service or shared patterns after P1 economics are proven. | Problem interviews only; do not build for them yet. |

## 6. Release 1 wedge and product portfolio

### Release 1 outcome

> A claims officer can assemble and verify a non-injury motor-comprehensive claim file, see missing and conflicting evidence, coordinate the assessor and customer evidence handoffs, prepare an evidence-linked recommendation, and hand it to the correct authorized human with an appeal-ready audit trail.

### Primary and exception scope

| Scope | Decision |
|---|---|
| Primary | Motor comprehensive own accidental damage. |
| Bounded test cases | Windscreen/glass and minor accidental damage; same workflow, not separate products. |
| Secondary | Third-party property damage only after primary workflow validation. |
| Exception route | Bodily injury, fatality, contested liability, suspected fraud requiring investigation, litigation, COMESA, or unsupported evidence/language. |

### Release 1 capability slices

| Slice | Required outcome | Not a standalone value claim |
|---|---|---|
| Intake and continuity | Preserve notification, parties, loss facts, timestamps, channel provenance, and current owner. | Omnichannel breadth. |
| Claim file and document intelligence | Preserve originals; classify/extract with provenance; show missing, stale, unreadable, conflicting, and corrected state. | OCR accuracy in isolation. |
| Evidence review workspace | Link every decisive fact to source page/field/version; expose limits and contrary evidence. | Free-form chat. |
| Handoffs and SLA | Assign missing evidence, assessor referral, customer update, due date, receipt, and escalation. | Generic task management. |
| Recommendation and approval | Prepare non-binding rationale and route to authorized human; enforce the ceiling. | Automated adjudication. |
| Audit and evidence pack | Reconstruct source, derived artifact, corrections, communications, actors, decisions, and versions. | Log volume. |
| Minimum integration | Entra, Azure SQL/Blob, real configured document/AI providers, controlled email/channel and core reference/export as pilot requires. | Integration catalog size. |
| Outcome instrumentation | Capture baseline and pilot event definitions without fabricated dashboard data. | Decorative analytics. |

### Portfolio beyond Release 1

Command Center, Channels, Customers, Sales & Leads, Claims, Documents, Knowledge, Approvals, Audit, Reports, Integrations, Admin, and Support form the platform surface. Underwriting Intelligence, Fraud Signals, Policy Servicing, Customer Acquisition, Early Warning/Retention, Billing/Entitlements, Agent Registry, advanced analytics, and other lines are later portfolio capabilities. Built code does not make a capability part of the released wedge; activation requires evidence, readiness, and an explicit roadmap decision.

## 7. Blue-ocean diagnostic

This is a strategy hypothesis, not proof of uncontested space.

### Alternatives and value factors

Scale: 1 low investment / utility, 5 high. Scores are directional hypotheses for validation.

| Buyer value factor | Manual/shared tools | Core claims suite | Generic AI/RPA | Vertical insurance AI | IIP proposed | Evidence / caveat |
|---|---:|---:|---:|---:|---:|---|
| Full core-transaction depth | 1 | 5 | 1 | 2 | 1 | IIP deliberately integrates beside core systems. |
| Deployment/integration burden | 2 | 1 | 3 | 3 | 3 | Managed deployment helps control; real target cost unknown. |
| Uganda workflow and terminology fit | 3 | 2 | 1 | 1 | 5 | IIP score is an ambition until participant validation. |
| Evidence-level traceability | 2 | 3 | 2 | 5 | 5 | Vertical AI competitors visibly invest here; not unique alone. |
| Non-bypassable human assurance | 3 | 3 | 2 | 3 | 5 | Platform ceiling is the intended divergence. |
| Chat-to-work continuity | 1 | 2 | 4 | 3 | 5 | Usability and value are unproven. |
| Country/institution configurability | 2 | 4 | 3 | 3 | 5 | Must be proven with a second configuration. |
| Low-bandwidth/customer-channel resilience | 2 | 3 | 3 | 2 | 4 | Requirements exist; live proof missing. |
| Audit/appeal reconstruction | 1 | 4 | 2 | 4 | 5 | Must be tested on reopened/contested cases. |

### Four-actions grid

| Action | Choice | User effect | Cost / operating implication | Evidence needed |
|---|---|---|---|---|
| Eliminate | Autonomous-decision positioning; replacement-suite ambition; fake demo success; one-size-fits-all Africa claims. | Reduces fear and ambiguity. | Avoids broad transactional build and unsafe operations. | Buyer objections and procurement/security response. |
| Reduce | Module breadth, dashboard decoration, generic chatbot conversation, upfront write-back depth. | Focuses work on the claim file and next action. | Concentrates delivery and integration cost. | Task observation and pilot adoption. |
| Raise | Evidence provenance, contradictions, human authority, audit/appeal continuity, local terminology, honest provider states. | Improves calibrated reliance and control. | Requires versioning, instrumentation, governance, and training. | Correct acceptance/rejection and audit reconstruction. |
| Create | Configurable Uganda-first evidence-review layer; chat plus structured evidence work; explicit supported/unsupported boundaries. | Provides one place to prepare approval-ready work without replacing core. | Requires configuration model and integration adapters. | Paid-design-partner demand and measured workflow improvement. |

### Buyer-utility barriers

| Journey | Main barrier | Product response / validation |
|---|---|---|
| Discovery / purchase | AI distrust, unclear ROI, crowded automation claims. | Show the bounded workflow, human ceiling, baseline plan, and disconfirming test results. |
| Onboarding | Security review, Azure/procurement, data access, vocabulary and authority mapping. | Read-only/controlled start; configuration workbook; named owners; deployment evidence. |
| Use | Extra tool, evidence inspection burden, poor extraction, unclear next action. | Task-centered workspace, source links, correction, exception routing; usability benchmark. |
| Complements / support | Core system, assessor, email/WhatsApp, document provider and human handoff dependencies. | Honest provider state, durable handoff, integration scope control, support runbook. |
| Repeat use | Model/workflow drift, stale evidence, weak adoption. | Versioning, evaluation set, weekly adoption and override review. |
| Exit / switching | Data lock-in and audit-retention concerns. | Exportable evidence packs, retention/deletion controls, contract-tested exit plan. |

### Strategic sequence verdict

| Test | Verdict | Reason |
|---|---|---|
| Buyer utility | **Test first** | Desk evidence supports the problem; task and purchase value are not participant-validated. |
| Strategic price | **Unknown** | No buyer willingness-to-pay or procurement evidence. |
| Target cost | **Requires redesign evidence** | Cloud, implementation, support, verification, and integration costs are not modeled from a real pilot. |
| Adoption | **Material barriers remain** | Trust, authority, workflow disruption, data, procurement, security, language, and integration remain. |
| Overall | **Test first** | Advance to structured discovery and a bounded design-partner test, not broad commercial build. |

### Positioning and promise

**Positioning:** IIP is the Uganda-first, human-supervised evidence-review layer for insurance operations—not a replacement claims system and not an autonomous adjuster.

**One-line promise:** Turn a fragmented motor claim file into source-traceable, approval-ready work for the accountable human.

## 8. Product vision, experience promise, and principles

### Vision and horizon

Over five years, African insurance operations teams can progress complex work across channels, documents, institutions, and countries without losing evidence, accountability, or local context—and can introduce AI assistance without surrendering consequential decisions.

### Experience promise

At every material step, the user can tell what is known, where it came from, what is missing or disputed, what the system did, what the person must decide, and what happens next.

### Product personality

Calm, precise, locally literate, evidence-forward, candid about limits, and operationally accountable. Never theatrical, omniscient, accusatory, or falsely reassuring.

### Decision principles

| Principle | Prefer | Avoid | Tradeoff test | Evidence | Approval |
|---|---|---|---|---|---|
| Prepare; humans decide | Non-binding work products and attributable approval. | Automation that crosses the consequential boundary. | If speed conflicts with accountable decision, preserve accountable decision. | Repository authority; legal/insurance validation still required. | Proposed |
| Evidence before fluency | Source, version, contradiction, gap, and review state beside the assertion. | Persuasive prose or confidence as a substitute for proof. | If a concise answer hides decisive limits, show the limits. | NIST and calibrated-reliance desk research. | Proposed |
| Current work, recoverable history | Exact current state plus preserved superseded/corrected history and pending action. | Chat history as source of truth or silent overwrite. | If simplicity conflicts with recovery/audit, preserve recoverability. | Continuity desk research; user study pending. | Proposed |
| Configure local truth | Institution-validated roles, workflow, documents, language, authority, currency, retention, and channels. | “African” defaults or Uganda hardcoding. | If consistency conflicts with lawful/local operation, keep the invariant and configure the local rule. | Uganda desk research; second-country proof pending. | Proposed |
| Truthful operational state | Empty, unavailable, degraded, or `Not Configured` states. | Simulated success or decorative metrics. | If a demo looks better by hiding reality, show reality. | Repository authority. | Proposed |
| One outcome, minimum surface | Capabilities that directly improve the Release 1 outcome. | Feature parity or module count as strategy. | If a feature cannot move a target outcome or safety guardrail, defer it. | Product strategy analysis; pilot validation pending. | Proposed |
| Correctability over automation | Inspect, correct, rerun, request evidence, contest, and escalate. | One-way AI execution or invisible recomputation. | If automation makes an error harder to detect or reverse, require review. | Human-factors desk research; usability evidence pending. | Proposed |

### Anti-principles

- “Maximum automation.”
- “One workflow for Africa.”
- “Chat is the product.”
- “Confidence equals correctness.”
- “Built means valuable.”
- “A positive pilot story outweighs disconfirming evidence.”

### Formal approval register

No function is marked approved until a named accountable person records a date and decision.

| Function | Required approval question | Named owner | Decision | Date / evidence |
|---|---|---|---|---|
| Product | Is this the focused market/problem/outcome strategy, including what is excluded? | TBD | Pending | — |
| Insurance | Are actors, terminology, claim scope, authority, evidence, and exceptions operationally correct? | TBD | Pending | — |
| Operations | Can target teams adopt and operate the workflow with explicit handoffs and support? | TBD | Pending | — |
| Security | Are assurance, data, identity, tenancy, provider, audit, retention, and failure boundaries acceptable? | TBD | Pending | — |
| Engineering | Is the scope feasible, observable, supportable, configurable, and coherently sequenced? | TBD | Pending | — |

Approval requires either `Approved`, `Approved with conditions`, or `Rejected`, with conditions/objections linked. Silence and a meeting attendance list are not approval.

## 9. Outcome metric contract

### Measurement rules

- `B0` is the pre-pilot baseline from the same institution, scope, definitions, and comparable case cohort.
- Business targets below are **proposed validation thresholds**, not achieved results or forecasts.
- Report median and 80th/90th percentile where waiting-time outliers matter; do not rely on averages alone.
- Separate active staff time, external waiting time, system time, and end-to-end elapsed time.
- Never optimize approval or settlement speed by weakening evidence, authority, customer fairness, or safety.
- Product owns definitions; institution operations owns source validity; data/engineering owns instrumentation; an independent pilot reviewer signs the analysis.

### Release 1 scorecard

| ID | Outcome metric | Baseline | Proposed pilot target | Accountable owner | Measurement method |
|---|---|---|---|---|---|
| U1 | Active staff minutes to produce approval-ready recommendation | Missing; time study on ≥30 comparable cases | ≥30% median reduction vs B0, no safety regression | Claims Operations Owner | Event timestamps plus sampled observation/time diary. |
| U2 | First-pass evidence completeness | Missing; % cases needing another avoidable evidence loop after review | ≥25% relative improvement vs B0 | Claims Manager | Versioned checklist and follow-up events by reason/owner. |
| U3 | Missing-evidence resolution elapsed time | Missing; notification to required-item acceptance | ≥20% median reduction vs B0 | Claims Operations Owner | Claim/document/communication timestamps; split external wait. |
| U4 | Approved customer-status update time | Missing; trigger to delivered approved update | ≥30% median reduction vs B0 | Customer Service Owner | Draft, approval, provider delivery, and failure timestamps. |
| U5 | Approval-ready file returned for avoidable rework | Missing; return reasons on comparable cases | ≥25% relative reduction vs B0 | Authorized Approver | Approval return/defect taxonomy and case audit. |
| Q1 | Decisive fact traceability | Missing evaluation-set score | 100% of sampled decisive facts link to source version/page/field or explicit absence | Product Quality Owner | Blinded evaluation of ≥50 representative scenarios. |
| Q2 | Unsupported consequential-decision rate | Missing; establish on evaluation set | 0 | Insurance + Security Owners | Adversarial scenarios and production audit query. |
| Q3 | Correct acceptance / incorrect rejection | Missing | Directional improvement vs unaided B0; threshold set after formative test | Product Research Owner | Balanced correct, incorrect, incomplete, conflicting, unavailable tasks. |
| Q4 | Material contradiction detection | Missing | ≥95% on agreed high-risk evaluation set | Claims Quality Owner | Seeded and historic reconstructed cases. |
| Q5 | Audit reconstruction completeness | Missing | 100% sampled cases reconstruct sources, versions, actors, actions, correction, recommendation, approval | Compliance Owner | Independent audit-pack review. |
| S1 | Cross-tenant/workspace exposure | Current automated coverage exists; pilot baseline 0 incidents | 0 | Security Owner | Automated isolation tests, telemetry, incident review. |
| S2 | Human-boundary bypass | Current guard exists; pilot baseline 0 | 0 | Security + Insurance Owners | Guard tests, approval audit, red-team attempt. |
| A1 | Weekly active use among trained Release 1 claims users | 0 before pilot | ≥70% by week 4 and sustained for four weeks | Product Owner | Authenticated activity tied to target tasks; exclude passive page views. |
| A2 | Release 1 task completion without moderator/help | Missing formative baseline | ≥80% after iteration; 100% respect the decision boundary | Design/Research Owner | Moderated then unmoderated representative tasks. |
| B1 | Buyer progression | 0 validated buyers | ≥2 qualified institutions complete problem/solution review; ≥1 accepts paid or contractually committed design-partner step | Commercial Owner | CRM evidence, signed LOI/pilot agreement, procurement next action. |
| B2 | Unit economics | Missing real volume/cost inputs | Target cost supports approved price and gross-margin floor | Finance Owner | Activity-based cloud, implementation, support, integration, and verification cost model. |

### Guardrail interpretation

AI correction/override rate is diagnostic, not a “lower is always better” KPI. A lower rate can mean better output, missed errors, or automation bias. Review it with correctness, evidence inspection, contradiction detection, and escalation behavior.

## 10. Value proposition validation and discovery plan

### Current verdict

The problem and wedge are supported by primary-source desk research and repository strategy. The value proposition has **not** been validated with representative buyers or users. No document may claim otherwise.

### Four-risk plan

| Risk | Riskiest question | Cheapest credible test | Pass signal |
|---|---|---|---|
| Value | Will claims leaders and officers change behavior or pay to improve this workflow? | Problem interviews, recent-case reconstruction, concierge evidence pack, buyer pilot offer. | Repeated high-intensity problem, voluntary data/time, and concrete procurement/design-partner action. |
| Usability | Can staff complete review and handoff from the proposed workspace without unsafe reliance? | Clickable prototype with correct, incorrect, incomplete, conflicting, and unavailable cases. | Task completion, source inspection, contradiction detection, correct escalation, boundary comprehension. |
| Feasibility | Can IIP support representative documents, integrations, configuration, performance, and recovery? | Thin live-data spike using redacted/synthetic-equivalent case packs and real providers. | Agreed document/field coverage, traceability, retry/recovery, cost/latency, no unsafe fallback. |
| Viability | Can security, compliance, IT, procurement, support, and finance accept the model? | Cross-functional buyer walkthrough and activity-based target-cost model. | No unowned blocker; acceptable deployment, contract, support, price, margin, and data posture. |

### Participant plan

- 6–8 motor claims officers.
- 2–3 claims supervisors / authorized approvers.
- 2–3 loss assessors or adjusters.
- 2–3 customer service or complaints officers.
- 2–3 recent motor policyholders/claimants.
- 2 brokers/agents and one garage/repair-network representative.
- 5–8 buyer/gatekeeper interviews across claims leadership, operations, IT/security, compliance/legal, finance/procurement.
- At least two institutions before treating configuration claims as supported; a second African jurisdiction before claiming cross-country fit.

### Required cases

Use recent redacted cases or faithful reconstructions: complete simple damage; missing police evidence; conflicting dates; disputed quotation/assessment; late notification; third-party property demand; low-quality image; non-English narrative; unsupported document/language; complaint/reopen; and a case that must be escalated without recommendation.

### Interview and observation rules

- Start with current behavior, a recent case, artifacts, workarounds, handoffs, authority, and costs—not reactions to the IIP pitch.
- Separate buyer, user, influencer, and gatekeeper evidence.
- Record evidence strength, sample, institution, date, moderator, artifact, and limitations.
- Ask for behavior or commitment: data access, staff time, security review, procurement step, paid pilot—not enthusiasm alone.
- Test a workflow-only alternative so the chat surface earns its place.

### Kill / rethink criteria

Rethink or stop the Release 1 strategy if any of these occurs:

- Evidence handling is not a top-three controllable cause of delay/rework in at least two target institutions.
- Claims officers cannot complete critical tasks safely after two prototype iterations.
- Source visibility does not improve incorrect rejection/contradiction detection or creates unacceptable review burden.
- A core-system configuration or simpler workflow tool provides comparable value at materially lower total cost and disruption.
- No qualified buyer commits data, staff time, security review, or a paid/contracted next step after the planned discovery sample.
- Required target price cannot cover cloud, implementation, support, integration, verification, and compliance cost at the approved margin.
- Security, legal, regulator, or institution authority requirements cannot be met without weakening the human-supervised ceiling or tenant isolation.

## 11. Research-to-decision governance

### Evidence hierarchy

1. Applicable law, regulation, regulator guidance, and binding institution policy.
2. Observed recent work and artifacts from representative institutions/users.
3. Measured prototype, pilot, security, quality, and operational results.
4. Corroborated market/customer evidence.
5. Primary-source desk research.
6. Vendor claims, stakeholder opinion, repository assumptions, and illustrative ROI.

Higher-ranked current evidence overrides lower-ranked assumptions. Code proves implementation, not desirability or viability.

### Evidence and decision log fields

Every material product update must record: evidence ID; question; source/sample; date; jurisdiction/institution; method; finding; limitations; affected assumption/decision/metric; disposition (`Retain`, `Change`, `Kill`, `Need more evidence`); owner; approver; linked artifact/version.

### Current evidence ledger

| Evidence | What it supports | What it does not prove |
|---|---|---|
| `docs/research/iip-market-regulatory-competitive-evidence-2026-08-05.md` | Current Uganda market context, regulatory and deployment constraints, competitor/substitute taxonomy, and explicit evidence gaps. | IIP demand, reachable revenue, participant workflow, buyer authority, ROI, pricing, or adoption. |
| `docs/research/iip-user-business-market-research-program.md` | Decision-led primary research, sampling, contextual inquiry, buyer validation, ROI measurement, pricing tests, synthesis, and advance/rethink/stop rules. | Findings; no sessions or pilots have occurred under the program. |
| `docs/research/iip-research-evidence-register.csv` | Structured provenance linking sources and methods to affected decisions. | Approval merely because a row exists. |
| `docs/research/uganda-release-1-claims-workflow-desk-research.md` | Motor-comprehensive own-damage wedge, roles, terminology, evidence set, handoff hypotheses, language posture. | Institution frequency/cost, usability, willingness to buy, universal workflow. |
| `docs/research/generic-conversation-continuity-ux-research.md` | Recoverable work state, evidence/version continuity, low-bandwidth principles. | IIP user behavior or Release 1 usability. |
| `docs/research/bevaya-comparative-analysis.md` | Vertical insurance AI alternatives and observable workflow/review patterns. | Vendor production truth, Uganda fit, IIP differentiation or demand. |
| Repository code/current-state map | Feasibility evidence and implementation constraints. | Product-market fit, adoption, ROI, production readiness. |
| June product pack | Historical decisions, requirements, hypotheses, and scope candidates. | Participant validation or current authority when conflicting with `AGENTS.md`. |

### External baseline sources

- [IRA Uganda 2025 market performance release](https://ira.go.ug/insurance-premiums-cross-ugx-2-trillion-as-life-business-sparks-historic-growth/)
- [IRA Uganda 2026 authorized-player release](https://ira.go.ug/144-players-set-to-drive-insurance-business-in-2026/)
- [IRA Uganda 2024 Annual Insurance Industry Market Report](https://ira.go.ug/wp-content/uploads/2025/10/2024-Insurance-Industry-market-report-2.pdf)
- [IRA Insurance Claims Guidelines](https://ira.go.ug/cp/uploads/claimesguidelines.pdf)
- [Insurance Act 2017](https://ulii.org/en/akn/ug/act/2017/6/eng%402017-06-08)
- [Bevaya platform](https://www.bevaya.ai/platform)
- [Guidewire ClaimCenter](https://www.guidewire.com/en/products/core-products/insurancesuite/claimcenter-claims-management-software)
- [Duck Creek Claims](https://www.duckcreek.com/product/claims-management-software/)

## 12. Product domain and boundaries

The canonical ubiquitous language is maintained in the repository-root `CONTEXT.md`. The critical distinctions are:

- tenant is the insurance business boundary; workspace is a governed subdivision;
- policyholder, insured, and claimant are not interchangeable;
- complete claim file does not mean payable claim;
- source evidence is distinct from derived AI artifacts;
- recommendation is distinct from decision;
- claim acknowledgement is not admission of liability;
- claim decision, settlement offer, discharge voucher, payment, settlement, and closure are distinct;
- complaint and appeal are distinct review paths;
- underwriter is not a generic synonym for claims approver;
- built module is distinct from activated Release 1 scope;
- Uganda First is distinct from Uganda-hardcoded; Africa Configurable is a validation obligation, not a market claim.

**Implementation reconciliation required:** the current coarse `CaseStatus.Settled` / `CaseStatus.Closed` model and client timeline can imply decision and settlement from terminal status alone. Before payment, dispute, recovery, or closure workflows are activated, the implementation must represent the distinct lifecycle meanings above and prove their transitions with human-authority and audit evidence.

## 13. Outcome roadmap

| Stage | Initiative | Outcome | Exit evidence |
|---|---|---|---|
| Now | Approve the frame, wedge, principles, glossary, metrics, and research protocol | One coherent product definition with named owners and explicit assumptions | Five-function decisions recorded; objections resolved or conditioned. |
| Now | Secure Uganda design-partner access and baseline pack | Real current workflow, roles, artifacts, cost/delay and authority evidence | Target institution; redacted cases; baseline dataset; owner-approved definitions. |
| Now | Run problem/value discovery and comparative task prototypes | Validate problem intensity, workflow alternative, Command Center utility, assurance comprehension | Research report changes/retains/kills assumptions; pass/kill criteria applied. |
| Next | Execute bounded Release 1 pilot with real providers and synthetic-safe staging before controlled institution data | Prove task, quality, adoption, operational, security, and economic outcomes | Scorecard, UAT, provider/security evidence, support and incident results. |
| Next | Decide wedge continuation, redesign, or stop | Evidence-backed investment decision | Signed decision with metric and qualitative evidence. |
| Later | Validate second institution and configuration model | Separate product invariants from institution configuration | Configuration delta, cost, outcome, no code fork. |
| Later | Validate one adjacent workflow or second African jurisdiction | Earn broader “Africa Configurable” claim | New research, legal/operational mapping, controlled pilot. |

Dependencies: named target institution; representative participants; redacted cases and policies; authority matrix; document checklist; service-level definitions; baseline data access; provider and environment readiness; legal/security participation; commercial owner and target-cost inputs.

## 14. Legacy-document reconciliation

| Legacy statement | Disposition |
|---|---|
| Broad June “MVP” includes Channels, Sales & Leads, Customer 360, fraud flags, dashboards, and more. | **Narrow:** these may support the platform, but Release 1 value scope is the evidence-review outcome in §6. |
| “WhatsApp-first” is confirmed. | **Hypothesis:** validate actual institution/channel behavior; preserve channel configurability. |
| Mid-to-large insurer is the confirmed pilot buyer. | **Target hypothesis:** institution class is prioritized; named buyer and demand are missing. |
| DEC-013 makes Fraud, Underwriting, Billing, and Agent Registry full features. | **Hold:** code may exist, but activation is post-wedge and cannot broaden Release 1 without evidence and approval. |
| DEC-016 creates a deployed `Demo` runtime profile. | **Superseded:** `AGENTS.md` permits only the listed runtime profiles and forbids seeded demo business data in protected/customer-facing runtimes. |
| Illustrative USD 16.5M benefit and 33x–66x ROI. | **Do not use as forecast:** replace with target-institution baseline and activity-based economics. |
| “Africa-first” or “Africa-configurable” implies market fit. | **Constrain:** it is a direction and configuration hypothesis until multi-institution/jurisdiction evidence exists. |
| Phase 0 product definition is complete. | **Correct:** identity/wedge/boundary are defined; validation, baselines, value proof, named approvals, and research-driven revision remain open. |

## 15. Approval conditions and unresolved questions

1. Who are the named Product, Insurance, Operations, Security, and Engineering approvers?
2. Which Uganda general insurer will supply discovery access, recent redacted cases, workflow documents, authority matrix, and baseline data?
3. Is the recommended Release 1 claim scope accepted: motor-comprehensive own-damage first, third-party property damage secondary, injury/fatality/contested liability exception-routed?
4. What target gross-margin floor and commercial currency constrain strategic price and target cost?
5. Which core system, channel, document, and policy integrations are essential to validate the outcome rather than optional breadth?
6. What institution-approved retention, residency, privacy, complaint, appeal, and customer-communication rules apply?
7. Which language and service-area evidence will determine the first customer-communication localization after English staff UI?

Until these are resolved, the product definition is coherent and actionable but not formally approved, baseline-complete, buyer-validated, or market-validated.
