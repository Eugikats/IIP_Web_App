# IIP Product Domain

> Status: Proposed
> Owner: Unassigned
> Last reviewed: 2026-08-05

This glossary defines the canonical product language for Insurance Intelligence Platform (IIP). It complements the start-here index at `docs/CONTEXT.md`; it does not replace product, legal, workflow, or implementation specifications.

## Product and operating scope

**Insurance Intelligence Platform (IIP)**:
The formal product: a human-supervised insurance operations platform that prepares evidence-linked work for accountable people. IIP is not a core insurance system or autonomous decision maker.
_Avoid_: ACIP, AIIP, generic chatbot, autonomous insurer

**IIP Operations**:
The internal staff application through which authorized insurance personnel review and progress work.
_Avoid_: customer portal, chatbot app

**Insurance Institution**:
An insurer, broker, TPA, HMO, bancassurance operator, or other regulated insurance participant that may buy or operate IIP. Release 1 targets a general insurer; the broader term does not imply equal launch priority.
_Avoid_: customer when institutional type matters

**Insurer**:
The licensed insurance institution that assumes covered risk, issues the policy, handles the claim, and remains accountable for its decisions and obligations.
_Avoid_: broker, agent, TPA

**Tenant**:
One insurance institution's isolated IIP business boundary. A tenant owns its configuration, memberships, workspaces, records, and audit scope.
_Avoid_: Entra tenant, account

**Workspace**:
A governed subdivision within a tenant for an authorized operational scope such as a branch, department, product line, or country.
_Avoid_: tenant, project

**Uganda First**:
The product strategy of validating Release 1 against Uganda's insurance practices, institutions, regulation, channels, language realities, and infrastructure constraints.
_Avoid_: Uganda-only, Uganda-hardcoded

**Africa Configurable**:
The design obligation to express country, institution, workflow, document, language, currency, authority, retention, and channel differences through governed configuration and adapters.
_Avoid_: one Africa workflow, pan-African by default

**Release 1**:
The first outcome-validation release: non-injury motor comprehensive own-damage evidence review, with bounded test cases and exception routing. It is narrower than the long-term platform and commercial module portfolio.
_Avoid_: full platform, every motor claim, generic MVP

**MVP Wedge**:
The minimum product scope used to validate the Release 1 outcome and commercial hypothesis. For IIP, the MVP wedge and Release 1 refer to the same bounded motor-claims evidence-review strategy.
_Avoid_: all implemented modules, feature checklist

## Claims and evidence

**Claim Case**:
The tenant-scoped operational record that brings together a reported loss, parties, policy references, documents, communications, workflow state, recommendations, decisions, and audit history.
_Avoid_: ticket, chat, folder

**Claim Notification (Intimation)**:
The report that a loss may have occurred.
_Avoid_: approved claim, admission

**Claim Acknowledgement**:
The insurer's recorded confirmation that it received a claim notification and has begun the applicable intake process. It is not an admission of liability, coverage, or payment.
_Avoid_: claim approval, acceptance of liability

**Claim File**:
The versioned collection of source evidence, derived artifacts, communications, and reviewed records used to handle a claim case.
_Avoid_: attachments, AI context

**Supporting Document**:
A source artifact supplied for a claim, such as a claim form, police evidence, permit, photographs, quotation, policy schedule, or assessor report.
_Avoid_: attachment when evidentiary purpose matters

**Source Evidence**:
The original or authoritative record from which a claim fact is supported, contradicted, or left unresolved. Derived AI output is not source evidence.
_Avoid_: model answer, chat history

**Derived Artifact**:
An extraction, translation, classification, summary, chronology, or recommendation produced from source evidence with recorded provenance, version, and review state.
_Avoid_: evidence

**Complete Claim File**:
A claim file containing the required evidence for the current workflow state. Completeness does not mean coverage, liability, admissibility, or payment is approved.
_Avoid_: payable claim, approved claim

**Recommendation**:
A non-binding, evidence-linked proposal prepared for an authorized human. It must expose material gaps, contradictions, assumptions, and limits.
_Avoid_: decision, verdict

**Consequential Insurance Decision**:
A claim, settlement, payment, binding, pricing, underwriting, fraud, complaint, regulatory, or write-back decision reserved for an authorized human under platform rules.
_Avoid_: AI decision, auto-approval

**Human Approval**:
An attributable decision or authorization performed by a permitted person after reviewing the required evidence and limits.
_Avoid_: button click without accountable identity

## Insurance and claim lifecycle

**Insurance Product Lifecycle**:
The wider continuum from market research and product design through distribution, application, underwriting, policy administration, servicing, claims, reporting, and improvement. It is broader than the Release 1 claim lifecycle.
_Avoid_: claim lifecycle

**Policy Lifecycle**:
The progression of an insurance contract through quotation, application, underwriting, issue, premium collection, servicing, endorsement, renewal, lapse, cancellation, or expiry.
_Avoid_: claim lifecycle, customer journey

**Claim Lifecycle**:
The progression of a reported loss through notification, acknowledgement, evidence acquisition, investigation or assessment, human decision, settlement or repudiation, payment where applicable, recovery, complaint or appeal, and closure. Institution-specific states may vary without collapsing these distinct meanings.
_Avoid_: one universal status list, policy lifecycle

**Claim Investigation / Assessment**:
The evidence-gathering and professional review used to establish facts, damage, causation, liability questions, and loss amount for a human decision. It does not itself approve or decline the claim.
_Avoid_: AI verdict, claim decision

**Claim Decision**:
The attributable human determination of the claim outcome within delegated authority, such as approval in whole or part, referral, or repudiation. It is distinct from a recommendation, settlement offer, discharge, payment, and closure.
_Avoid_: AI recommendation, workflow approval without decision authority

**Settlement Offer**:
A human-authorized proposal stating the amount and terms on which the insurer is prepared to resolve an approved claim.
_Avoid_: AI offer, payment, settled status

**Claim Decline (Repudiation)**:
The human-authorized refusal of a claim, supported by the applicable policy basis, evidence, written reason, and available complaint or appeal path.
_Avoid_: fraud accusation, unexplained rejection

**Discharge Voucher**:
A settlement-stage document by which the claimant records acceptance of stated settlement terms, subject to applicable law and institution process. It must be explained before signature and is not proof that payment has occurred.
_Avoid_: payment receipt, claim approval

**Claim Payment**:
The executed transfer of an authorized claim amount to the verified payee. Payment authorization and payment completion are separate states.
_Avoid_: approval, settlement recommendation

**Claim Settlement**:
The financially resolved state supported by the required agreement and payment records. IIP must not mark a claim settled merely because an offer or approval exists.
_Avoid_: approved, ready for payment

**Recovery / Subrogation**:
Post-decision action through which an insurer seeks to recover an amount from a responsible third party, salvage, or another legally available source after meeting applicable claim obligations.
_Avoid_: customer debt collection

**Complaint**:
A customer's or claimant's formal expression of dissatisfaction handled through the insurer's complaint process, with its own owner, response, evidence, and audit trail.
_Avoid_: claim appeal, support message

**Appeal**:
A request for an authorized body to review a disputed decision after or outside the applicable complaint path. Preserve the reviewed file, decision basis, communications, and version history.
_Avoid_: complaint when formal external review is meant

**Claim Closure**:
The institution-authorized administrative state reached after required claim actions are complete or otherwise concluded. Closure is distinct from payment and may be reversed by a governed reopening.
_Avoid_: deletion, automatic archive

## People and roles

**Policyholder**:
The person or organization that holds the policy. The policyholder may differ from the insured and claimant.
_Avoid_: customer when legal identity matters

**Insured**:
The person, property, or interest covered under the policy.
_Avoid_: policyholder when they differ

**Claimant**:
The person or organization seeking a claim benefit or remedy. A claimant may be a policyholder, insured, or third party.
_Avoid_: customer when claim standing matters

**Third-Party Claimant**:
A claimant seeking a remedy in connection with another person's or organization's policy or alleged liability. Keep this role separate from policyholder and insured.
_Avoid_: policyholder, insured

**Vehicle Owner**:
The person or organization recorded as owning the vehicle relevant to a motor claim. Vehicle owner, policyholder, insured, driver, and claimant may be different parties.
_Avoid_: driver, policyholder

**Driver**:
The person operating the vehicle at the relevant incident time. Driver identity and permit evidence do not by themselves establish coverage, liability, or claim entitlement.
_Avoid_: vehicle owner, insured

**Witness**:
A person who supplies an attributable account of relevant events without becoming the insurer's investigator or decision-maker.
_Avoid_: claimant, claims investigator

**Insurance Broker**:
An intermediary acting for or representing the insurance buyer under the applicable legal and contractual relationship. A broker may submit and follow up a claim but does not become the claimant or insurer decision-maker.
_Avoid_: insurance agent, claims approver

**Insurance Agent**:
An intermediary appointed or authorized to solicit or service insurance business within the applicable legal relationship. Keep agent and broker distinct unless institution and jurisdiction evidence explicitly maps them.
_Avoid_: insurance broker, loss adjuster

**Underwriter**:
The insurer-side role accountable for assessing a proposed risk and making or referring risk-acceptance, coverage-condition, and pricing decisions within delegated authority. Do not use this title as a generic synonym for claims approver.
_Avoid_: claims officer, claims approver

**Claims Officer**:
The insurer-side user who assembles and reviews the file, resolves evidence gaps, prepares a recommendation, and coordinates handoffs. An institution may split these capabilities across intake officer, administrator, handler, or examiner titles; map authority and tasks rather than assuming one universal title.
_Avoid_: adjuster unless that is the institution's validated title

**Authorized Approver**:
The person or formally constituted committee whose role and delegated authority permit the relevant consequential decision. The local title, quorum where applicable, and authority matrix must be configured and validated per institution.
_Avoid_: supervisor as a universal title

**Loss Assessor / Loss Adjuster**:
Licensed or recognized external roles that investigate or assess loss and provide a report. Preserve the institution's and jurisdiction's precise role distinction.
_Avoid_: agent, AI assessor

**Claims Investigator**:
A human specialist assigned to establish disputed or unusual claim facts. Investigation may identify concerns but does not itself determine fraud, approve, or repudiate the claim.
_Avoid_: AI investigator, fraud verdict

**Customer Service / Complaints Officer**:
The insurer-side role responsible for approved customer-safe updates, service continuity, complaint handling, and escalation without exposure of restricted claim information.
_Avoid_: claims decision-maker

**Claims Settlement / Payment Officer**:
The insurer-side role responsible for verifying and executing authorized settlement or payment steps. This role does not inherit claim-decision authority merely by controlling payment operations.
_Avoid_: claims approver

**Garage / Repairer**:
An external service provider that supplies quotations, inspection access, repair evidence, invoices, or repair work. Its evidence and commercial interest must remain attributable.
_Avoid_: loss assessor, insurer approver

**Police / Traffic Authority**:
The public authority that may supply incident records, abstracts, reports, or sketch plans relevant to a motor claim. Its record is source evidence, not an insurance coverage decision.
_Avoid_: claims investigator, insurer approver

**Insurance Regulator / Appeals Body**:
The legally established public authority or tribunal that supervises insurance conduct or reviews matters within its mandate. It is external to the insurer's internal approval chain.
_Avoid_: insurer compliance team, claims committee

## Product behavior

**Command Center**:
The chat-first, structured operational workspace that retrieves current tenant-scoped facts and renders work blocks linked to evidence and actions.
_Avoid_: free-form chatbot, source of truth

**Human-Supervised Assurance Boundary**:
The non-bypassable product ceiling under which AI may prepare, retrieve, summarize, draft, recommend, and route work but may not make consequential insurance decisions.
_Avoid_: configurable autonomy level

**Not Configured**:
The truthful product state shown when a required real provider or institution configuration is absent. It is not an error to conceal or a reason to simulate success.
_Avoid_: mock success, fallback data
