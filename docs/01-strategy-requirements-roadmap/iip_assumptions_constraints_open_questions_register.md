# IIP Release 1 Assumptions, Constraints and Decision Register

| Field | Value |
|---|---|
| Version | 2.0 |
| Date | 2026-08-05 |
| Status | Every launch-critical item resolved or explicitly deferred; deferred gates remain blocked |
| Requirements | `iip_release_1_requirements_baseline.md` |

## Status meanings

| Status | Meaning |
|---|---|
| Resolved — invariant | Repository authority decides the matter; implementation/release evidence still required |
| Resolved — Release 1 scope | The proposed baseline selects the approach; six-function approval pending |
| Deferred — target insurer | No universal answer is safe; obtain the named insurer's decision before the listed gate |
| Deferred — evidence | Decision waits for specified research/test evidence; listed gate remains blocked |
| Deferred — commercial | Does not block source work but blocks a proposal/pilot contract |

## Constraints

| ID | Constraint | Source | Effect |
|---|---|---|---|
| C-001 | AI/system never finally decides consequential insurance outcomes. | `AGENTS.md`; R1-AI-001 | No tenant configuration or workflow may widen authority. |
| C-002 | Protected runtimes use no simulation, fallback, fake success, seeded business data, DevelopmentAuth or LocalTest persistence/provider. | `AGENTS.md`; R1-SEC-004 | Missing capability is `Not configured`; invalid configuration fails closed. |
| C-003 | Tenant/workspace membership and permission resolve server-side for HTTP and background work. | `AGENTS.md`; R1-FUN-001 | Browser/token/request identifiers cannot grant authority. |
| C-004 | Consequential work uses current approved evidence and exact versioned human approval. | `AGENTS.md`; R1-FUN-010–013 | Stale/missing/conflicting evidence blocks execution. |
| C-005 | IIP is not the policy, claims payment, accounting or ERP system of record. | Charter; R1-BUS-003 | External truth has an explicit owner and reconciliation path. |
| C-006 | English-only is not a final product architecture. | Research; R1-L10N-* | Preserve original multilingual sources and unsupported-language review. |
| C-007 | Production readiness requires evidence and named approvals, not document presence. | `AGENTS.md`; R1-REL-* | Gate remains blocked when evidence/approval is absent. |
| C-008 | WhatsApp intake and reviewed follow-up are mandatory Release 1 capabilities. | Owner decision 2026-08-05; DEC-017; R1-WA-* | Provider/legal/configuration gaps block activation; they do not convert WhatsApp to optional or permit simulated success. |

## Assumptions requiring validation

| ID | Assumption | Evidence needed | Failure response | Status |
|---|---|---|---|---|
| A-001 | Evidence assembly/correction is a material controllable problem in target motor own-damage work. | Recent-case interviews, observation, artifacts and baseline | Reframe/narrow/stop Release 1 | Deferred — evidence |
| A-002 | The selected staff roles can use one evidence-review operating loop. | Role/authority/handoff research in target institutions | Configure distinct workflows or narrow role | Deferred — target insurer |
| A-003 | A real document provider can meet target documents/languages/quality/cost. | Representative corpus and live provider evaluation | Keep `Not configured`, change provider/scope or stop | Deferred — evidence |
| A-004 | A qualified buyer can support data, staff, security review and a paid pilot. | Behavioral commitment/procurement evidence | Stop bespoke pilot preparation | Deferred — commercial |
| A-005 | Initial engineering performance/recovery targets fit business impact and pilot load. | Load model, client impact analysis and staging tests | Reapprove target/architecture; never relax after failure silently | Deferred — target insurer |
| A-006 | Reviewer workload permits meaningful oversight. | Timed tasks, queue volume, staffing and override behavior | Reduce automation/volume, redesign or stop | Deferred — evidence |

## Launch-critical decisions

| ID | Decision/question | Disposition/default | Owner | Required by | Gate effect |
|---|---|---|---|---|---|
| OQ-001 | Confirm first pilot insurer/design partner. | Deferred — no named account; generic/synthetic work only | Leadership/Commercial/Product | UAT planning | Blocks G3–G5 |
| OQ-002 | Select exact deployment model, subscription, region, network and support-access posture. | Deferred; parameterized design, no production promise | Architecture/Security/Client IT/Legal | Protected staging design | Blocks G2 activation for target client and G4–G5 |
| OQ-003 | Confirm included motor own-damage categories, exclusions and required documents. | Release 1 category bounded; institution rules deferred | Insurance/Product/Client Claims | UAT corpus/config | Blocks G3–G5 |
| OQ-004 | Select mandatory integrations and read/export/write-back depth beyond WhatsApp. | WhatsApp is required; every other integration is unassumed and absent stays `Not configured` | Product/Architecture/Client IT | UAT configuration | WhatsApp gaps block its G2–G5 journey; others block only approved dependent journeys |
| OQ-005 | Approve pilot outcomes, baseline definitions and severe-error thresholds. | Deferred to baseline/research/pilot charter | Product/Insurance/Operations/Client Sponsor | Pilot contract | Blocks G4–G5 |
| OQ-006 | Approve support hours, incident contacts, escalation and recovery ownership. | Deferred; no unsupported service commitment | Delivery/Operations/Client | Protected staging handoff | Blocks G2 operational acceptance and G4–G5 |
| OQ-007 | Approve security/privacy/legal questionnaires and residual risks. | Deferred to exact client/deployment | Security/Privacy/Legal/Client | Pilot activation | Blocks G4–G5 |
| OQ-008 | Approve retention, deletion, legal hold, audit, backup and exit schedules. | Deferred; retain safely without claiming compliant final policy; no real pilot data until decided | Privacy/Legal/Client Records | Real-data onboarding | Blocks G4–G5 |
| OQ-009 | Confirm role labels, memberships, permissions, delegation, separation of duty and two-person controls. | Platform ceiling resolved; institution mapping deferred | Product/Insurance/Client Admin | UAT role setup | Blocks G3–G5 |
| OQ-010 | Approve price, packaging, currency, cost owner and paid-pilot terms. | Deferred — commercial hypothesis only | Commercial/Finance/Leadership | Proposal | Blocks paid pilot contract, not G0–G2 source work |
| OQ-011 | Confirm device, network, language, literacy, accessibility and handoff conditions. | Provisional thresholds in baseline; participant validation deferred | Research/Product/Design/Client Ops | UAT plan | Blocks final G3 acceptance and G4–G5 |
| OQ-012 | Qualify reachable initial accounts. | Current validated count 0; ceiling 19 non-life insurers | Commercial/Product | Market-validation review | Blocks market/revenue claim, not G0–G2 source work |
| OQ-013 | Confirm procurement, data residency, outsourcing, integration and change-management path. | Deferred to named buyer; public sources are not buyer proof | Buyer/Procurement/IT/Legal | Pilot proposal | Blocks G4–G5 |
| OQ-014 | Select document languages/scripts, translation policy and qualified human fallback. | Preserve original + `undetermined`; unsupported routes to human | Insurance/Operations/Privacy/Client | Corpus/configuration | Blocks document journey G3–G5 |
| OQ-015 | Select document provider, models, region, capacity and acceptance corpus. | `Not configured` until live-verified | Architecture/AI Quality/Client | Protected staging | Blocks extraction-dependent G2–G5 |
| OQ-016 | Approve approval expiry, delegation, concurrency, return, escalation and compensation policy. | Exact approval invariant resolved; institution values deferred | Insurance/Operations/Security/Client | UAT configuration | Blocks approval/execution G3–G5 |
| OQ-017 | Select WhatsApp provider/business account, templates, languages, consent/legal basis, sender authority and callback/reconciliation policy. | Resolved — WhatsApp capability required; activation details deferred and no outbound send until approved | Operations/Privacy/Client Communications/Architecture | Protected staging and communication UAT | Blocks WhatsApp G2 activation and send journey G3–G5, not source design/implementation |
| OQ-018 | Approve idempotency keys, retry/backoff, DLQ retention, replay and reconciliation authority per provider/action. | Bounded/idempotent invariant resolved; values deferred | Architecture/Operations/Security | Provider activation | Blocks affected G2–G5 journey |
| OQ-019 | Approve RPO/RTO, availability, load and supported browser/device/assistive-tech matrix. | Provisional baseline thresholds; no SLA promise | Operations/Architecture/Product/Client | G2/G3 test plan | Blocks G4–G5 |
| OQ-020 | Decide claim decision/settlement/payment/closure/reopen domain semantics before activating those workflows. | Release 1 stops at approval preparation/export; current coarse terminal statuses are not authority | Insurance/Product/Architecture | Any terminal workflow | Deferred outside Release 1; blocks such activation |

## Closure rule

An item closes only with a named decider, exact scope/version, decision/date, evidence, conditions, expiry/review trigger, and synchronized requirement/API/data/test/roadmap updates. An engineering default may support safe source work but never silently satisfy a client/legal/release decision.
