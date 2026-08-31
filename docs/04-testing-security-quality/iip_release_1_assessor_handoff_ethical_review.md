# Ethical review: IIP Release 1 assessor handoff

| Field | Value |
|---|---|
| Version | 0.1 |
| Date | 2026-08-10 |
| Status | Design review complete; insurer policy, claimant/practitioner validation, implementation and residual-risk approval pending |
| Scope | Minimum disclosure, claimant privacy, external-professional access, fairness, feedback effects and contestability |
| Not | Legal advice, formal regulatory opinion, participant finding or runtime certification |

## What decision it makes, about whom, at what scale

IIP does not appoint an assessor or decide a claim. It prepares a proposed minimum referral pack, records an authorized insurer human's external disclosure, receives a report, and lets another authorized human decide whether that report is current evidence. These steps still affect people: the choice to refer, the evidence disclosed, the questions asked, the credibility given to the report, and any delay or repeated evidence burden may influence a policyholder, insured person, claimant, driver, third party, or household member.

Release 1 volume is bounded by a controlled manual baseline, with Graph email optional. The domain contract is reusable across insurers and can scale quickly once automated. Ethical controls therefore apply before activation, not only after high volume appears.

The legitimate purpose is narrow: enable an insurer-appointed external professional to answer documented claim-specific questions using only evidence necessary for that assignment. It does not include general investigation, claimant scoring, fraud inference, marketing, model training, assessor ranking, or unrelated reuse.

## WMD screen: opacity, scale and feedback loop

| Warning sign | Verdict | Reason and required response |
|---|---|---|
| Opacity | **Serious flag** | Current design does not yet guarantee that the claimant learns an assessor is involved, what was shared, why, how the report may be used, or how to correct/challenge it. Staff audit visibility is not claimant transparency. Require useful notice, accessible explanation and a mediated contestability path. |
| Scale | **Conditional flag** | Manual exchange limits initial throughput, but the workflow, Graph adapter and reusable templates can replicate a bad disclosure or referral rule across tenants. Tenant-specific rules, versioning, monitoring and stop conditions must precede automation. |
| Damage/feedback loops | **Serious flag** | An assessor report can acquire unjustified authority, generate repeated evidence requests/delay, shape a claim proposal, and later be treated as a training label or proof that the original referral criterion was correct. Disputed/limited reports must remain contestable; no training/score reuse without separate approval. |

**WMD verdict:** opacity plus plausible damage/feedback creates a serious ethical flag even though IIP does not make the final claim decision. Human approval alone is insufficient if the human sees an over-disclosed, uncontestable report framed as authoritative. Activation requires `ETH-R1-ASR-01`–`08` and `AC-ETH-01`.

## Fairness: applicable definition and disparities

Demographic parity of assessor referrals is not the approved fairness objective: legitimate claim type, damage complexity, location, evidence quality and insurer policy may change whether assessment is necessary. The Release 1 fairness definition is **procedural parity and equal opportunity to participate, correct and obtain reconsideration**:

- similarly situated claims use the same approved referral criteria and minimum-disclosure rules;
- document language, script, disability/access need, channel, device, location, income proxy, name or broker representation does not create a higher evidence burden without justified claim-specific reason;
- people receive comparable notice, response opportunity, correction route, review quality and remedy;
- assessor delay, report disagreement or system/provider failure is not converted into an adverse inference about the claimant;
- no protected or vulnerability proxy ranks credibility, urgency, fraud, merit or assessor need outside a separately approved lawful purpose.

Measure referral rate, documents/pages/fields disclosed, repeat evidence requests, unverified/unsupported report rate, clarification/revision rate, staff disagreement/override, elapsed time, expiry/late return, contested-report rate, reconsideration outcome and unresolved burden across approved claim-complexity strata and, only where lawful and justified, relevant demographic/vulnerability intersections. Where sensitive labels are unavailable, use controlled language/script, document quality, accessibility, channel, connectivity, geography and case-complexity test strata and disclose the limitation.

Do not add noise to individual evidence, referral, review or remedy. For externally published aggregate fairness/operations statistics, apply minimum cohort thresholds, suppression and, where justified, calibrated differential privacy so one person's participation cannot be inferred. Internal individual-level access remains purpose-limited and auditable.

## Privacy: personal data, minimum disclosure and external access

Potential data includes claimant/policyholder/driver/third-party identity and contact data; policy, vehicle, incident date/location and damage evidence; photographs, statements, signatures, police/repair records and identifiers; financial or special-category material accidentally present in a document; and referral, acknowledgement, report, limitation and revision data.

### Minimum-disclosure test

Every proposed item must pass all questions below. Failure removes/redacts it or blocks referral pending authorized resolution.

1. **Purpose:** Which approved referral question requires this item?
2. **Necessity:** Could a redacted field, page, excerpt, lower-resolution copy or structured fact answer it with less exposure?
3. **Authority:** What approved legal/contractual basis and disclosure permission apply? Consent is not assumed or forced where not freely given.
4. **Recipient:** Is the current appointed professional the intended recipient, and are delegates/subcontractors allowed?
5. **Sensitivity:** Does it contain unrelated people, internal notes, reserves, fraud/risk signals, prompts, credentials, health/financial data or metadata to exclude?
6. **Time:** Is it current for this referral version and necessary before due/expiry?
7. **Handling:** Are channel, forwarding, retention, deletion, location, confidentiality, incident and return/destruction obligations approved?
8. **Accountability:** Can the insurer reconstruct the disclosure and provide a useful claimant explanation without exposing restricted material?

“Entire claim file,” convenience, assessor preference, speed, or previous practice is not a necessity reason. A minimum pack is an explicit allow-list, not an export followed by attempted redaction.

### Claimant notice and choice

Before disclosure, or at the earliest approved time where advance notice would lawfully frustrate a necessary process, the insurer process must provide accessible notice of:

- assessor identity or meaningful recipient category;
- purpose and questions;
- information categories shared and material report use;
- channel and the fact transmitted email attachments may be copied/forwarded and cannot be technically recalled by IIP;
- relevant retention/handling owner;
- correction, context, accommodation and challenge routes;
- the fact that the assessor/AI does not make the final claim decision.

Notice is not consent. The insurer must record the appropriate basis and consequence of refusal or inability to provide information. A claimant must not be coerced into unrelated/excessive disclosure to progress a claim. Any notice exception needs a named owner, reason, scope, expiry and later-notice/review rule.

### External-professional access

The assessor receives no Release 1 IIP staff seat, tenant browse access, search, queue, internal notes, reserves, fraud/risk signals, audit detail, prompts or unrelated claims. Access is limited to the immutable minimum pack delivered through the approved existing channel or configured Graph mailbox. Recipient delegates, onward sharing, storage location, confidentiality, retention, deletion/return, breach notification and AI/subprocessor use remain insurer-owned contractual/configuration gates.

Email/manual disclosure is not technically revocable after bytes leave IIP. Expiry prevents future workflow reliance/reuse; it cannot guarantee deletion from the assessor's mailbox, device, backup or forwarded copy.

## Gaming and feedback risks

| Risk | Harm | Guardrail/monitoring |
|---|---|---|
| Staff attach whole file to save time | Systematic privacy loss; irrelevant facts bias assessor | Per-item necessity reason, default-deny manifest, redaction preview, sample audits, no speed KPI rewarding oversharing |
| Assessor writes to expected template/threshold | Nuance/uncertainty disappear; reports converge on expectations | Require sources/limits/uncertainty, measure revision/disagreement, prohibit ranking by agreement rate |
| SLA/completeness becomes target | Premature currentness; late report harms claimant | Separate SLA from merit/currentness; audit late handling; no adverse inference from provider/assessor delay |
| Claimant withholds context from fear | Lower-quality evidence and unequal outcomes | Plain notice, minimum disclosure, accommodation, correction and explanation |
| Disputed report becomes training truth | Past error becomes future referral/scoring bias | No training label, retrieval authority, assessor score or ground truth without separate approved purpose/AIA/data authority |
| Frequent referral justifies referral rule | Self-confirming process and unnecessary cost/delay | Compare simpler baseline; monitor necessity, value, disagreement, turnaround and burden; pause low-value rule |

## Accountability: explanation and contestability

Contestability is mediated through WhatsApp, Customer Service/Complaints, another approved channel or staff-assisted intake; it never requires claimant access to the internal application.

An effective path must:

1. accept correction, context, accommodation, identity/recipient concern, privacy complaint, report dispute or human-review request without requiring proof of a system defect;
2. acknowledge receipt, name an accountable owner and provide an approved response target;
3. link exact source/referral/report versions while preserving originals and claimant context;
4. mark materially disputed assessor evidence `Contested` and block new reliance/execution until an authorized human decides continued use is safe;
5. provide review by a person able to disagree, seek clarification/another assessment, correct evidence, invalidate derived work and remedy; where independence is required, not the report author or original relying decision-maker;
6. communicate a useful outcome reason, what changed/remained, limitations, next action and escalation;
7. propagate accepted corrections to summaries, completeness, proposals, approvals, communications and exports without rewriting history;
8. audit and monitor delay, abandonment, repeat contact, disagreement and remedy—not merely closure count.

Restricted internal notes/provider detail remain protected, but secrecy cannot deny the substance needed to challenge material reliance. “A human reviewed it” is not an explanation.

## Recommendations

### P0 — before activation

| ID | Recommendation | Owner/evidence |
|---|---|---|
| `ETH-R1-ASR-01` | Approve purpose taxonomy and per-question/per-item minimum-disclosure allow-list with redaction/exclusion rules. | Claims Operations, Privacy/Legal, Insurance; representative pack review |
| `ETH-R1-ASR-02` | Add immutable disclosure manifest: necessity, recipient/appointment, basis, categories/items, exclusions/redactions, version/hash, channel, handling and actor. | Product/Architecture/Security; contract/audit tests |
| `ETH-R1-ASR-03` | Approve accessible claimant notice, timing/exception policy, language/accommodation path and useful explanation. | Privacy/Legal, Customer Service, Accessibility; claimant comprehension test |
| `ETH-R1-ASR-04` | Contract purpose, delegates, onward sharing, AI/subprocessors, storage/region, confidentiality, retention/deletion/return, incident and audit terms. | Procurement/Legal/Privacy/Security; signed terms |
| `ETH-R1-ASR-05` | Implement contested-evidence state and reconsideration able to pause reliance, correct, obtain another assessment, invalidate and remedy. | Insurance/Product/Architecture; end-to-end tests |
| `ETH-R1-ASR-06` | Prohibit report/referral/outcome reuse as training labels, assessor ranking or risk/fraud/credibility score without separate approval/AIA. | Responsible AI/Data Governance; source/config/model inventory |
| `ETH-R1-ASR-07` | Define procedural-parity metrics and privacy-safe reporting across justified strata. | Responsible AI/Operations/Privacy; baseline/dashboard specification |
| `ETH-R1-ASR-08` | Give named owners authority to pause a rule/channel for oversharing, disparate burden, ineffective contestability or low value. | Product/Insurance/Privacy; stop/restart rehearsal |

### P1 — before controlled pilot

- Test minimum-pack decisions with Claims Officers, assessors, Privacy and representative claimant scenarios.
- Test notice, explanation and contestability across language, literacy, disability, channel and low connectivity.
- Sample manifests against packs; measure unnecessary items/pages/fields and prohibited categories.
- Exercise wrong report, disagreement, privacy complaint, late report, repeat request and independent reassessment.
- Obtain named residual-risk acceptance for non-revocable email forwarding and external retention limits.

### P2 — later expansion

- Reassess identity, power, accessibility, surveillance and exclusion before any external portal, assessor scoring, automated referral, core write-back or report-based model training.
- Prefer a simpler/safer process when it provides comparable value with less transfer, delay or burden.

## Readiness verdict

The manual/optional-Graph boundary can be ethically defensible only as a narrow, purpose-bound, human-accountable exchange. It is **not ready for external activation**: minimum-disclosure rules, claimant notice, external-professional terms, contested-evidence behavior, fairness monitoring, implementation, practitioner/claimant validation and named approvals are absent. Documentation completion cannot substitute for these decisions and tests.

This is an ethical/product-systems review, not legal advice. Applicable privacy, insurance, evidence, professional and complaint obligations require qualified target-jurisdiction and insurer review.
