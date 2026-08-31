# Uganda Release 1 claims workflow desk research

**Date:** 2026-08-03
**Purpose:** Recommend the first Release 1 claims workflow, claim types, roles, Uganda-facing labels, handoff hypotheses, and language posture.
**Evidence level:** Desk research from primary sources. No participant research or product usability test has yet been conducted.

## Executive recommendation

Release 1 should optimize **evidence-complete review of non-injury motor claims**, starting with **motor comprehensive own-damage** and a deliberately narrow extension to **third-party property damage**. Within comprehensive claims, use windscreen/glass and minor accidental-damage cases as low-complexity test cases, but do not build a separate product workflow for each.

The staff outcome should be:

> A claims officer can assemble and verify the claim file, see missing or conflicting evidence, obtain or review an assessor's report, prepare a reasoned recommendation, and hand the case to an authorized human approver with a complete audit trail.

Release 1 must not let AI approve, repudiate, or settle a claim. It should support evidence review and recommendation only.

Recommended Release 1 staff roles:

1. **Claims Officer** — owns intake follow-up, evidence review, chronology, questions, and recommendation.
2. **Claims Supervisor / Authorized Approver** — reviews exceptions and makes the human decision. The exact local title needs insurer validation.
3. **Loss Assessor / Loss Adjuster** — supplies the independent assessment/investigation report through a constrained external handoff or controlled upload.
4. **Customer Service / Complaints Officer** — sees status, reasons, promised next action, and approved customer-safe communication.

External participants represented in the workflow, but not necessarily full staff seats in Release 1: **policyholder/insured, claimant, broker or agent, police, witness, garage/repairer, and medical provider when an injury is present**. Injury cases should route out of the first-release happy path.

## Why motor comprehensive first

### Primary-source evidence

- Uganda's 2024 non-life insurance figures show motor insurance at UGX 189.7 billion insurance revenue and UGX 102.1 billion incurred claims and attributable expenses. Health and medical was larger by incurred claims, but its workflow is provider-network and clinically specialized. Motor is therefore both materially important and more bounded for an evidence-review release. [IRA 2024 Annual Insurance Industry Market Report, pp. 17–18](https://ira.go.ug/wp-content/uploads/2025/10/2024-Insurance-Industry-market-report-2.pdf)
- The IRA reported that motor, medical, and life were particularly affected by claim-handling weaknesses and fraud risk. It recorded 109 complaints for repudiation/failure to honour/unpaid claims, 46 for delayed settlement or dissatisfaction with payment, and 20 for unfair treatment including poor communication in 2024. [IRA 2024 Annual Insurance Industry Market Report, pp. 16, 51–52](https://ira.go.ug/wp-content/uploads/2025/10/2024-Insurance-Industry-market-report-2.pdf)
- In 2025, the Insurance Appeals Tribunal chair said most appeals concerned group life, motor comprehensive, and medical claims. The same IRA account linked distrust to complex policy terms, lengthy processes, inadequate support, vague claim procedures, and weak investigation or loss-adjustment reports. [IRA: Insurers urged to simplify policies and improve claims](https://ira.go.ug/insurers-urged-to-simplify-policies-improve-claims-process-to-enhance-public-trust/)
- Uganda Police recorded 25,808 road-crash casualties in 2024, including 5,144 deaths and 17,013 serious injuries. That reinforces motor's relevance but also supports excluding bodily injury and fatality from the initial happy path because those cases carry materially greater human, evidentiary, medical, and legal complexity. [Uganda Police Force Annual Crime Report 2024, p. 94](https://upf.go.ug/wp-content/uploads/2025/02/ACR2024-Web.pdf)
- The IRA claims guideline gives motor claims a concrete evidence set: claim form, driving permit, police report/sketch plan, logbook, third-party demand, third-party details, and proof of damage/injury. Its process names notification, acknowledgement and document acquisition, site visit or assessor appointment, settlement offer or repudiation, discharge voucher, and settlement. [IRA Insurance Claims Guidelines](https://ira.go.ug/cp/uploads/claimesguidelines.pdf)
- A first-party Uganda insurer's motor intake mirrors that pattern: incident report, Police Abstract, driver's licence, third-party contact and registration data, plus a motor claim form. Its process document adds claimant, LC1, medical, death, and estate evidence for third-party injury/fatality claims. [Britam Uganda: file a motor claim](https://ug.britam.com/help/file-a-claim), [Britam Uganda claims process](https://ug.britam.com/images/FormsAndDownloads/Claims-Process-UG.pdf)

### Product judgment

Motor comprehensive own-damage is the best first slice because it combines high operational relevance with a reviewable artifact set: policy, form, identity and vehicle records, police evidence, photographs, quotations, repair evidence, assessor report, correspondence, and decision rationale. It can exercise document completeness, chronology, inconsistency detection, evidence citation, SLA tracking, assessor handoff, supervisor review, and customer communication without making clinical or bereavement workflows the first implementation dependency.

Third-party property damage is a useful second scenario inside Release 1 because it tests multi-party evidence and correspondence. Third-party bodily injury, fatality, and contested liability should be exception-routed until explicitly designed and validated.

## Claim-type scope

| Claim type | Release 1 decision | Reason |
|---|---|---|
| Motor comprehensive: own accidental damage | **Primary** | Material claim line; bounded documentary review; assessor/garage handoffs; strong fit for evidence rail. |
| Windscreen/glass and minor vehicle-part damage | **Primary test cases** | Lower-complexity variants suitable for learning completeness and straight-through preparation; still human-approved. Britam publishes distinct forms and asks for logbook, quotations, and driver's licence for glass claims. [Britam windscreen claim form](https://ug.britam.com/images/MotorClaimForms/Windscreen_Glass_Claim_Form.pdf) |
| Motor third-party property damage | **Secondary** | Exercises claimant, insured, police, assessor, broker, and correspondence handoffs without the full severity of injury/fatality. |
| Motor bodily injury/fatality and COMESA | **Exception route / later release** | Requires medical, identity/estate, LC1, cross-border, and potentially legal evidence. |
| Medical | **Later release** | Largest non-life incurred-claims line in the 2024 class table, but clinically and operationally distinct; the IRA issued dedicated Medical Insurance Claims Directives effective 30 January 2025. [IRA Medical Insurance Claims Directives](https://ira.go.ug/download/circular-medical-insurance-claims-directives/) |
| Group life/death | **Later release** | Appeals relevance is high, but beneficiary, death, and estate evidence require a separate sensitive workflow. |
| Workers' compensation/personal accident | **Later release** | Requires employer, labour office, medical-practitioner, incapacity, and sometimes police handoffs; not a minor motor variant. [Britam Uganda claims process](https://ug.britam.com/images/FormsAndDownloads/Claims-Process-UG.pdf) |
| Fire, agriculture, marine, engineering | **Later release** | Valuable but require peril-specific survey, valuation, aggregation, or specialist evidence models not needed to validate the first workflow. |

## Can staff complete review from chat plus evidence rail?

**Not yet known.** There is no working interface and no usability evidence. Desk research can establish the required review content, not usability.

The first prototype should permit a claims officer to complete these tasks without leaving the central workspace:

1. Confirm policy, insured, vehicle, driver, loss date, and notification date.
2. See required, received, missing, expired, unreadable, and conflicting evidence.
3. Open every cited source in the evidence rail and locate the supporting passage or field.
4. Build or verify the event chronology.
5. Request missing evidence from the correct party.
6. Appoint or hand off to a loss assessor/adjuster and track the report.
7. Compare the assessment, quotations, policy terms, and claim facts.
8. Prepare a recommendation with supporting and contradictory evidence.
9. Escalate to a named authorized human with a due date and complete context.
10. Produce an approved, plain-language status update without exposing internal notes.

The evidence rail should show source type, issuer, date, version, extraction status, relevant page/field, confidence where machine extraction is used, contradictions, missing items, and whether the source has been human-verified. Chat must not become a second source of truth: it should operate over the claim file and link back to it.

Acceptance criteria for a later usability study: representative claims officers complete the review and handoff without moderator help; no unsupported decision is treated as final; all decisive facts are traceable to source evidence; missing/conflicting evidence is noticed; and the correct human approval boundary is respected.

## Will visible evidence and limits create trust?

**They can improve calibrated reliance, but they do not prove or guarantee trust.** The desired outcome is not maximum trust. Staff should accept well-supported output and reject, correct, or escalate incorrect or incomplete output.

A preregistered controlled experiment with 308 participants found that explanations increased reliance on both correct and incorrect LLM answers. Sources and visible inconsistencies reduced reliance on incorrect answers. This means a fluent explanation or confidence label can increase automation bias unless users can inspect sources and see contradictions. [Kim et al., CHI 2025: Fostering Appropriate Reliance on Large Language Models](https://www.microsoft.com/en-us/research/publication/fostering-appropriate-reliance-on-large-language-models-the-role-of-explanations-sources-and-inconsistencies/)

NIST treats valid and reliable performance as foundational and distinguishes transparency, explainability, and interpretability. It also says knowledge limits, intended use, human oversight, and safe failure beyond those limits should be documented and evaluated in context. Transparency by itself does not make a system accurate or fair. [NIST AI RMF trustworthiness characteristics](https://airc.nist.gov/airmf-resources/airmf/3-sec-characteristics/), [NIST AI RMF Core](https://airc.nist.gov/airmf-resources/airmf/5-sec-core/)

Release 1 should therefore:

- cite evidence at the claim, statement, field, page, and document-version level;
- surface missing, contradictory, stale, unreadable, and unverified evidence beside the recommendation;
- show what the AI did, what it did not check, and which human remains responsible;
- avoid a single global confidence score that can be mistaken for correctness;
- make dismiss, correct, re-run, request evidence, and escalate actions easy;
- fail safely when a provider, language, document, or evidence source is unsupported;
- evaluate appropriate reliance with deliberately correct, incorrect, incomplete, contradictory, and unavailable scenarios.

Trust-study measures should be: correct acceptance, incorrect rejection, evidence inspection before action, contradiction detection, correction/escalation behavior, comprehension of the AI boundary, and unsupported-decision rate. A satisfaction or self-reported trust score is secondary.

## Where handoffs appear to fail

The following are **evidence-backed risk hypotheses**, not observed workflow findings:

| Handoff | Primary-source signal | Release 1 control to test |
|---|---|---|
| Policyholder/broker → insurer | IRA requires prompt notification, document acquisition, documented communication, and copying brokers/clients on assessor correspondence. Complaints include poor communication and delayed/unpaid claims. | One visible checklist, receipt timestamp, missing-item owner, promised next action, and communication log. |
| Claims officer → assessor/adjuster | IRA expects written appointment and prompt delivery of received evidence. The Appeals Tribunal account criticizes weak investigation and loss-adjustment reports. | Structured referral pack, acknowledged receipt, questions to answer, due date, report-quality checklist, and revision loop. |
| Assessor/adjuster → insurer decision maker | IRA links report quality to the insurer's ability to decide whether a claim is payable. | Report findings mapped to evidence, unresolved contradictions, exclusions cited, and human reviewer sign-off. |
| Claim file → settlement offer/repudiation | Repudiation/failure to honour/unpaid claim was the top 2024 complaint category. | Decision rationale with policy clause and evidence links; separate supporting and contrary evidence; mandatory plain-language customer reason; human approval. |
| Insurer → customer/broker | IRA requires documented communication and written reasons; poor communication appears in complaint data. | Customer-safe status, next step, owner, due date, delivery/receipt proof, and escalation path. |
| Approved amount → settlement | IRA ties settlement timing to a signed discharge voucher and, for larger claims, reinsurer cash calls. | Explicit voucher status, bank-detail verification, payment owner, timer, exception reason, and reinsurance dependency. |
| Dissatisfied customer → complaint/appeal | IRA and the Insurance Appeals Tribunal report claim disputes and appeals. | Preserve the reviewed file, reason, evidence, communications, and decision history as an appeal-ready pack. |

Only observation of live or reconstructed claims can establish where these handoffs actually fail within a target insurer.

## Uganda insurance terminology

Use regulator and practitioner terms as the initial vocabulary, paired with plain-language explanations where a client could misunderstand them.

| Preferred label | Usage note |
|---|---|
| **Claim notification (intimation)** | Show “Claim notification” first; retain “intimation” as recognized industry terminology. |
| **Claim acknowledgement** | Receipt is not admission of liability. Make that boundary explicit. |
| **Supporting documents / claim documents** | Prefer over a generic “attachments” label. |
| **Complete claim file** | Means required evidence received, not that the claim is payable. |
| **Claims Officer** | Used by IRA guidance for insurer-side handling. Validate each insurer's local title. |
| **Loss Assessor / Loss Adjuster** | Preserve the licensed role labels; do not collapse them into “agent.” Uganda's Insurance Act separately recognizes these intermediary licence categories. [Insurance Act 2017, ss. 2, 83, 94](https://ulii.org/en/akn/ug/act/2017/6/eng%402017-06-08) |
| **Policyholder**, **insured**, **claimant** | Keep separate. They can be different people in third-party claims. Avoid using “customer” when the legal/operational identity matters. |
| **Broker** / **insurance agent** | Keep distinct; their statutory relationships differ. A policyholder or beneficiary may communicate directly with the insurer despite an agent appointment. [Insurance Act 2017, ss. 2, 93](https://ulii.org/en/akn/ug/act/2017/6/eng%402017-06-08) |
| **Assessment / investigation report** | Show author, role, date, evidence base, findings, and unresolved questions. |
| **Settlement offer** | Human-approved offer; not an AI action. |
| **Claim declined (repudiated)** | Staff surfaces may show both terms; client surfaces should lead with the plain-language label and full reason. |
| **Discharge voucher** | Retain because it is an explicit settlement-stage artifact in IRA guidance. Explain its consequence before signature. |
| **Claim settlement / payment** | Do not label a recommendation or approval as “settled” until payment state supports it. |
| **Complaint** / **appeal** | Separate internal complaint handling from escalation to the IRA Complaints Bureau or Insurance Appeals Tribunal. |

Terms needing direct validation: **Claims Supervisor**, **Claims Manager**, **Approver**, **Underwriter**, **assessor versus adjuster**, **authority to repair**, **quantum**, **admissibility**, **repudiation**, and insurer-specific status names. The IRA article uses “underwriters (insurers)” in a broad sense; the product should not assume every target insurer calls a claims decision maker an underwriter.

## Language posture

Release 1 may use **English for the staff interface and initial prototype**, but the client/document boundary must not be architected as English-only.

Uganda's Constitution names English as the official language and Swahili as the second official language, while allowing other languages for prescribed educational, administrative, or judicial purposes. This establishes a multilingual national context; it does **not** prove which language a target insurer's clients prefer. [Constitution of Uganda, Article 6](https://ulii.org/akn/ug/act/statute/1995/constitution/eng%402015-08-24/provision/chp_Two__sec_6)

Required architectural posture from Release 1:

- retain the original uploaded document and original-language text;
- record declared and detected language separately;
- treat OCR and translation as derived artifacts with provider, version, confidence, and review status;
- allow a human to correct extracted or translated content without overwriting the source;
- never translate names, policy numbers, registration numbers, dates, currency, or legal clauses silently;
- allow client guidance and missing-document requests to be localized independently of staff UI;
- make low-confidence or unsupported-language extraction visibly `Not configured` or “human review required,” never fake success.

Do not select Luganda, Swahili, or another language as the first localization solely from constitutional status or intuition. Choose the next language from launch-insurer service-area data and participant research. Recruit clients who submit handwritten, photographed, police, LC1, medical, and other third-party documents; ask which language they use to describe the event, receive instructions, and resolve document questions.

## Recommended Release 1 research participants

Prioritize behavior and workflow exposure, not seniority alone:

- 6–8 claims officers handling motor own-damage files;
- 2–3 claims supervisors or authorized approvers;
- 2–3 licensed loss assessors/adjusters used by the target insurers;
- 2–3 customer service or complaints officers;
- 2–3 motor policyholders who recently submitted a claim;
- 2 brokers/agents who actively follow up claims;
- one garage/repair-network representative for the authority-to-repair handoff.

Use recent, redacted real cases or faithful reconstructions. Include: complete simple damage, missing police evidence, conflicting dates, disputed quotation/assessment, late notification, third-party property demand, low-quality image, non-English customer narrative, and a case that must be escalated rather than recommended.

## Limits and validation gates

- This note does not identify the actual frequency, delay, rework, or failure points inside any insurer.
- National market values do not equal claim counts or workflow suitability; the recommendation combines market materiality with product-scope judgment.
- One insurer's published forms demonstrate a live Uganda pattern but do not establish universal practice.
- The IRA claims guideline is older and uses earlier Act references; it remains useful for process terminology and published service expectations, but current insurer compliance interpretation should be confirmed with IRA/legal counsel.
- No evidence yet establishes that chat plus evidence rail is usable or that visible evidence calibrates trust.
- No current primary source located in this pass establishes client language preference by claim context or insurer.
- Medical, life, worker injury, bodily injury, fatality, fraud referral, litigation, reinsurance, and final payment controls require dedicated research before inclusion in the happy path.

Proceed from desk research to participant validation only after a target insurer provides: anonymized motor workflow documents, status vocabulary, authority matrix, service-level rules, sample assessor reports, customer communication templates, and five to ten recent cases spanning paid, delayed, declined, and reopened outcomes.
