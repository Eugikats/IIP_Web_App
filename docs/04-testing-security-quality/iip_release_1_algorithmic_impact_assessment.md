# IIP Release 1 Algorithmic Impact Assessment

| Field | Value |
|---|---|
| Version | 1.2 |
| Date | 2026-08-10 |
| Status | Proposed; residual-risk approval and target-insurer evidence pending |
| Deployment ceiling | Advisory preparation with mandatory human authority |

## System and decision

Release 1 may classify documents, extract fields, identify configured evidence gaps, summarize current sources, prioritize review, and prepare a next action or WhatsApp customer-update draft. WhatsApp customer content is untrusted input, and any outbound draft remains separate from authorized human review and provider execution. It does not decide claim validity, coverage, repudiation, settlement, payment, fraud, complaint outcome, or regulatory filing.

The affected parties include policyholders, insured people, claimants, drivers, third parties, insurer staff, brokers/assessors, and the insurer. The accountable owner of every claim outcome remains the insurer's authorized human decision-maker.

The observable purpose, user intent, feedback, authority class, attention demand, and human/AI boundary for every registered critical action are governed by `../03-design-architecture/iip_release_1_action_purpose_human_ai_authority_contracts.md`. That companion operationalizes this assessment but cannot raise the deployment ceiling, invent insurer authority, or replace residual-risk approval.

The focused assessor rights-and-power assessment is `iip_release_1_assessor_handoff_ethical_review.md`. It finds serious opacity and feedback-loop flags and blocks external activation until minimum disclosure, claimant notice, external-professional terms, contestability, monitoring and validation are in place.

## Purpose, necessity, and baseline

Purpose: reduce avoidable evidence-assembly and follow-up burden while improving provenance, correction, and audit reconstruction.

Non-algorithmic baseline: the insurer's current case, document, email/channel, spreadsheet, shared-drive, and core-system workflow. IIP must demonstrate benefit against that baseline; model use is not justified merely because it is available.

## Consequence and risk rating

| Lens | Rating | Reason |
|---|---|---|
| Opacity | High | Model/rule outputs can look authoritative unless sources, limits, and versions are exposed. |
| Scale | Medium initially | Pilot is bounded, but repeated workflow reuse can propagate a systematic error. |
| Damage | High | Missing or misleading evidence can delay, burden, or influence claim treatment and customer communication. |
| Reversibility | Mixed | Drafts/corrections are reversible; sent messages and insurer/external actions may require remedy rather than undo. |

No broader deployment is permitted while any dimension remains unmeasured for the target population and workflow.

## Data inventory and provenance

| Input | Source/authority | Meaning | Sensitive/proxy risk | Quality risk | Correction/deletion |
|---|---|---|---|---|---|
| Claim/FNOL fields | Insurer-approved intake/core record | Reported event and parties | Identity, contact, location, financial and incident data | Misreporting, transcription, stale policy link | Authorized correction; propagate invalidation |
| Claim documents/images | Policyholder, claimant, insurer, assessor, police/third party as approved | Source evidence | Identity, vehicle, location, signature, financial and possibly health data | Poor image, fraud/manipulation, missing page, language/script | Preserve source; reviewed correction; retention/deletion policy |
| Extracted fields | Approved document provider output | Derived representation of source | Can infer sensitive facts or encode OCR bias | Language, handwriting, scan quality, template shift | Human review; retain provenance and before/after |
| Required-document rules | Approved tenant configuration | Evidence expected at a review stage | May unfairly burden uncommon cases if treated rigidly | Stale/wrong product or branch rule | Versioned owner change and exception route |
| Policy/SOP knowledge | Insurer-approved versioned source | Applicable operational guidance | Restricted/internal content | Superseded or wrong jurisdiction/product | Archive/reindex; invalidate dependent output |
| Workflow history | IIP authoritative records | Actions, tasks, approvals, execution | Staff behavior and performance inference | Missing events, duplicate/reordered processing | Append correction/repair with audit |
| Model/rule output | Derived artifact | Classification, gap, summary, proposal, draft | Automation bias and proxy-based prioritization | Hallucination, false positive/negative, drift | Reject/correct/appeal; invalidate downstream |
| WhatsApp message/document/status | Customer or approved WhatsApp provider; provider status is external delivery truth | Intake evidence, conversation context and delivery state | Identity/contact/content, wrong-recipient, cross-tenant and consent risk | Spoofing, duplicate/replay, wrong case match, unsafe draft, ambiguous delivery | Verified webhook; server-side mapping; manual review; correction/retention policy; reconciliation |
| Global-search metadata | Current tenant-scoped case, document, workflow-task and approval repositories | Direct retrieval of authorized work | Identity and operational metadata; restricted-object and count leakage | Stale permission, unsafe snippets, ranking-induced delay | Permission/assignment filtering before matching/counting; no raw query audit; omit sensitive fields; recheck target access |

Raw evidence and prompts are not conversational memory. Telemetry and audit use safe metadata and references.

## Objective and proxy analysis

The permitted objective is accurate, source-linked work preparation with safe recovery—not claim denial rate, fraud yield, settlement reduction, staff surveillance, or maximum automation.

Potential invalid proxies include document count as claim merit; missing police evidence as fraud; language/grammar as credibility; location/branch/channel as risk; staff override rate as performance; confidence as truth; and historical scrutiny as future fraud likelihood. None may drive an adverse outcome in Release 1.

Assessor referral, report, staff reliance, contest outcome and downstream claim outcome are not approved training labels, retrieval authority, assessor rankings, credibility/risk/fraud scores or automated-referral targets. Any future reuse requires a separately approved purpose, data authority, impact assessment, fairness definition, recourse and monitoring/stop plan.

Global-search ranking is retrieval, not claim prioritization or decision authority. Exact authorized identity matches rank first, then operational urgency, then recency. The search contract excludes free-text evidence, task summaries, approval evidence summaries, internal notes, national IDs, payment details, fraud/risk signals and raw conversation text. Search ranking must not be reused as a claim-risk, merit, fraud or staff-performance score.

## Error and harm analysis

| Error | Potential harm | Required control |
|---|---|---|
| False missing-evidence flag | Delay and repeated claimant burden | Show rule/source; reviewer exception/correction; measure repeat requests |
| Missed evidence gap | Incomplete human review | Checklist/version visibility; representative severe-error tests |
| Wrong extraction | Incorrect summary or follow-up | Field source view, confidence, correction, downstream invalidation |
| Unsupported summary | Automation bias and bad decision preparation | Claim-level citations; unsupported-claim test; stale-output block |
| Wrong prioritization | Unequal delay or workload | No adverse outcome; review distribution and subgroup/document-quality analysis |
| Unsafe customer draft | Misleading disclosure or rights harm | Approved templates, customer-safe filter, human review, send separation |
| Wrong WhatsApp case/recipient or false delivery state | Confidentiality breach, missed evidence, repeated contact or false assurance | Server-side business-number mapping, manual review for uncertain match, recipient/consent/template preflight, idempotency and provider reconciliation |
| Cross-tenant grounding | Confidentiality and decision contamination | Server scope, repository isolation, adversarial tenant tests |
| Prompt/tool injection | Unauthorized action or disclosure | Treat content as data, tool allow-list, scope/approval checks, incident trail |

## Fairness and evaluation definition

The primary fairness question is whether evidence-review errors or delays disproportionately burden people whose documents, language, script, device/channel, location, disability/access need, or case complexity differ from the evaluation corpus. Target groups and lawful sensitive-attribute processing must be agreed with privacy/legal and participant stakeholders; one generic parity score is not approved.

Report severe-error, false-gap, missed-gap, correction, return, escalation, task time, and unresolved-outcome distributions across relevant intersections. Where lawful group labels are unavailable, evaluate controlled document/language/accessibility strata and disclose the limit. Accuracy/fairness trade-offs require accountable stakeholder decision, not engineering convenience.

For assessor handoff, the approved objective is procedural parity and equal opportunity to participate, correct and obtain reconsideration—not demographic parity of referral rates. Measure disclosed-item count/sensitivity, notice comprehension and exception use, additional-evidence burden, assessor delay, report disagreement/contest rate, reconsideration time and remedy across justified claim/document/language/location/channel/accessibility strata and lawful intersections. Assessor/provider delay or missing accommodation must never become an adverse claimant inference. Differential privacy may protect published aggregate monitoring where appropriate; it may never perturb an individual case, notice, evidence or remedy record.

## Explanation and human oversight

Reviewers receive the source, source location, freshness, missing/conflicting evidence, derived status, provider/model/rule/workflow versions, proposed action, consequence, and permitted alternatives. Human review must support accept, correct, reject, return, escalate, and seek independent review. Queue design and staffing must prevent routine rubber-stamping; monitor time-on-review, bulk approval, override, return, escalation, and reviewer disagreement.

`FLW-R1-DECISION-01` is the required consequential path. Unknown action classification defaults to that human gate. Approval is separate from external execution, provider success, settlement, payment, delivery, and remedy.

Evidence eligibility follows the decision tree in `../03-design-architecture/iip_release_1_task_flow_exception_state_catalog.md`: missing, unverified, low-confidence, conflicting, stale, superseded, unauthorized, unsupported-language, duplicate, corrupt, and unsafe evidence each has an owned non-success path. Only eligible current reviewed evidence may support execution; excluded evidence remains traceable where lawful.

Role label alone is not meaningful oversight. Current permission reconciliation found that `Compliance` includes settlement-approval/override permissions, some Claims Officer/Customer Service workflow permissions are broader than the narrow role journeys, and no approved assessor disclosure/action permission exists. `PERM-GAP-01`–`05` therefore block any separation-of-duty or least-privilege claim until target role mapping, delegation/expiry, object/state guards, external-recipient/disclosure authority, and negative tests are approved.

## Contestability and remedy

For consequential use, the insurer must provide useful notice/reason as required, access to material data, correction, a human review capable of reconsideration, a response target, remedy, and propagation of corrections to derived artifacts. IIP's review-request and audit functions support the process but do not replace the insurer's legal complaint/appeal obligations.

For assessor evidence, contestability is mediated through WhatsApp, Customer Service/Complaints or another approved channel and requires no internal-app account or proof of system defect. A material dispute links the exact disclosure/referral/report versions, preserves original and claimant context, sets the report to `Contested`, and blocks new reliance/execution until an authorized human—independent where required—records reconsideration, reason, remedy/escalation and correction propagation.

## Pre-deployment tests

- Non-algorithmic baseline comparison and necessity test.
- Representative document/language/quality and intersectional error analysis.
- Severe false-positive/false-negative scenario review.
- Missing, unverified, low-confidence, conflicting, stale, superseded, unauthorized, unsupported-language, duplicate, corrupt, unsafe, adversarial, and shifted-data tests.
- Prompt injection, tool escalation, data leakage, proxy and gaming tests.
- Reviewer comprehension, wrong-output correction, recourse, and workload tests.
- Shadow/limited-pilot outcomes with rollback and affected-work identification.
- Version lineage and downstream invalidation after correction.
- WhatsApp signature/spoof, duplicate/replay, wrong-number/mapping, uncertain case match, restricted-content, recipient/consent/template, outbound approval, callback duplication and unknown-delivery tests using a real provider before activation.
- Global-search cross-tenant, revoked-permission, assigned-only, approval-permission, restricted-field/count leakage, exact-ranking, target-recheck, empty/partial/error and return-state tests.
- Role/action route inventory plus negative, revoked, delegated, expired and separation-of-duty tests, including the current Compliance and coarse document-review permission gaps.
- Assessor handoff tests for minimum disclosure, denied/unapproved access, acknowledgement, expiry, report provenance, clarification and superseding revision before any live external path.

## Monitoring and incidents

Monitor errors by severity/strata; evidence/citation coverage; corrections; returns/escalations; complaints/review requests; review time and bulk approval; drift; prompt/tool attacks; provider/rule/model/workflow versions; incidents; affected cases; and whether the function still outperforms a simpler safer process.

Incident response: pause/limit the function; preserve versions/evidence; identify affected cases/people; prevent further execution; correct records and derived artifacts; notify owners/affected parties as required; analyze data, objective, proxy, code, process, and incentives; verify remediation before restart.

## Stop conditions

- No legitimate accountable objective or safer baseline is comparable.
- Unacceptable data authority, provenance, privacy, proxy, or cross-tenant risk.
- Severe systematic error cannot be measured for affected populations/documents.
- Review workload makes oversight nominal.
- Consequential action lacks correction, independent reconsideration, or remedy.
- One correction cannot reliably invalidate downstream preparation.
- An alternate workflow path bypasses human authority.
- A simpler configured/manual process gives comparable value with materially lower risk/cost.

## Residual-risk approval

| Owner | Named person | Decision | Conditions | Date/review |
|---|---|---|---|---|
| Insurance/claims | Required | Pending | Target workflow and harm thresholds | — |
| Product | Required | Pending | Necessity and scope | — |
| Security/privacy | Required | Pending | Data/proxy/privacy threat model | — |
| Responsible AI/quality | Required | Pending | Evaluation and monitoring | — |
| Operations | Required | Pending | Review workload and incident process | — |

Pending rows block pilot activation for algorithm-assisted work.
