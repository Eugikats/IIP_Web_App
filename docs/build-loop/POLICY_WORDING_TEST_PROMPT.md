# Claude prompt: generate synthetic policy wording test documents

Copy this prompt into Claude. Replace the values in brackets. For the first IIP onboarding run, use **Private Motor Comprehensive**.

---

You are creating realistic but wholly fictional insurance policy documents for **software onboarding and extraction QA** in IIP, an insurance operations platform. Generate the actual document text requested below so I can copy, save, and upload it. Do not merely explain how to write it.

## Inputs

- Product: **[CHOOSE ONE: private motor comprehensive / motor third-party / property and fire / individual life / health / personal accident / marine cargo / credit life]**
- Market setting: Uganda
- Currency for non-life and life examples: UGX, unless the chosen fictional product explicitly states otherwise
- Baseline version label: `BASELINE-CLEAN`
- Organization setup: a fictional Ugandan insurer
- Intended IIP flow: insurer onboarding, policy wording extraction, claims-process review
- Optional focus: [e.g. theft claims, beneficiary claims, outpatient benefits; leave blank for a balanced sample]

## Safety and realism requirements

1. Put this visible notice at the top of **every** generated document: **SYNTHETIC TEST DOCUMENT — NOT AN INSURANCE CONTRACT. NOT APPROVED FOR SALE OR CUSTOMER USE.**
2. Invent every insurer, product, person, company, policy number, email, phone number, vehicle identifier, national ID, bank detail, address and claim reference. Do not use real insurer branding, logos, real personal data, real customer evidence, or copied policy clauses. Use obvious placeholders such as `SAMPLE-INSURER`, `TEST-POLICY-001`, `SYNTHETIC-NIN-001`, `TEST-VEHICLE-001`, `claims@example.invalid`, and `+256-TEST-0001`; do not generate valid-looking real identifiers or contact details.
3. Do not claim legal validity, IRA approval, regulatory compliance, market-average pricing, insurer approval, or suitability for issuing insurance. Do not invent legal or regulatory obligations. Use plain fictional contract language and state that figures and clauses are arbitrary QA values.
4. Keep facts internally consistent in the clean baseline. If a field is not stated, leave it blank or label it “Not stated”; never infer it.
5. Treat the output as an extraction fixture, not a real claims decision. Do not state that a claimant is fraudulent or that a claim must be approved, denied or repudiated.
6. Use short, clear clauses with stable numbering such as 1.1 and 2.1, and include literal page markers on separate lines: `Page 1`, `Page 2`, etc. Use native searchable text, not image-only content. Keep the complete baseline wording under 60,000 characters.
7. The IIP wording flow is intended to propose claim types, claim-document requirements, claim questions, covered items and exclusions with source quotes for human review. It does not configure every product detail. Make the claim process and evidence requests explicit and tied to specific claim categories.
8. For document requirements, use only these existing IIP document keys unless I explicitly provide other active workspace keys: `ClaimForm`, `PoliceAbstract`, `Invoice`, `MedicalReport`, `PhotoEvidence`, `CustomerId`, `GarageEstimate`. Show the display label and key, e.g. `Police report [document key: PoliceAbstract]`. Never invent a key or imply a key exists for a document type not listed. If the fictional policy refers to another evidence item, mark it “reference only; no IIP key supplied” and do not make it an extracted keyed requirement.
9. For the baseline, make claim questions answerable as text, date, number, phone, currency or yes/no. Do not require a select/dropdown question: IIP’s parser supports Select, but the current review UI does not expose it. You may mention this as a separate limitation in the answer key only.
10. Make each extractable claim rule, covered item and exclusion traceable to an exact, sufficiently distinctive sentence and clause. Do not put unrelated terms in one clause. Use the exact clause text in the answer key.

## Generate this complete output

### A. Clean baseline bundle

Return four separately titled, copyable documents in Markdown code fences. Put a filename before each fence.

1. `01_schedule_BASELINE-CLEAN.md`: a concise policy schedule / declarations page with synthetic insurer and product, policy number, version, issue date, policyholder/insured fields appropriate to the product, subject insured, cover period, currency, premium and payment status, main limits or benefits, deductibles/excesses where relevant, selected options, and endorsements selected. Add “Not stated” for important fields the sample deliberately omits.
2. `02_wording_BASELINE-CLEAN.md`: a short policy wording with definitions, insuring clauses/benefits, exclusions, conditions, claims notification/process, claim categories, evidence requirements, customer information/questions, renewal/cancellation, complaints contact placeholder, and a short human review note. Include accurate clause numbers and page markers. Tailor the content to the selected product using the guidance below. Do not insert generic clauses that conflict with the schedule.
3. `03_application_BASELINE-CLEAN.md`: a brief fictional application or declaration showing only relevant risk and insured-person details. Clearly distinguish applicant declarations from issued policy terms.
4. `04_endorsement_BASELINE-CLEAN.md`: one concise selected endorsement that changes one clearly named base-wording clause. Give it a unique endorsement number, effective date, exact amended wording and the conditions for applying it. The schedule must list that same endorsement number. Do not make it apply to a different risk or period.

### B. Three separate challenge variants

For each variant, return the exact document(s) to replace in a new file, with a filename and copyable Markdown code fence. Keep the clean baseline untouched; do not silently combine variants. Each variant must contain exactly the specified deliberate issue and otherwise preserve the baseline facts:

- `VARIANT-01-DATE-VERSION-MISMATCH`: change the version/effective-date information so the schedule and wording disagree about one date. Preserve all other values.
- `VARIANT-02-MISSING-ENDORSEMENT`: have the schedule select an endorsement number that is absent from the bundle, or include an endorsement absent from the schedule. Do not also alter the cover terms.
- `VARIANT-03-CONTRADICTORY-OR-MISSING-DETAIL`: create exactly one meaningful contradiction between two documents, or omit one important value from one document while another document states it. Choose a product-relevant field such as the insured asset/member, benefit limit, policy period, or claim evidence requirement. State in the private answer key which documents conflict or where the value is missing.

Do not label the customer-facing variant text as “fraud”, “fraudulent”, or suspicious. Name the issue only in the private answer key.

### C. Private extraction answer key

After all documents, provide a separate `05_PRIVATE_ANSWER_KEY_BASELINE_AND_VARIANTS.md`. This is QA metadata, not a customer policy document. Include:

- The selected product and a short explanation of the fixture scope.
- A JSON object listing the expected claim types, covered items, exclusions, claim-document requirements and claim questions. For each item provide its exact clause, exact verbatim evidence quote, document filename, page marker, expected IIP document key where applicable, applicable claim type, required/conditional status, and the expected normalized value or answer type.
- Use only the permitted IIP document keys above for keyed requirements. If no listed key fits, mark the item `not-configured-in-catalog` and explain that it must not be accepted as a keyed requirement until the workspace catalog has that document type.
- A field-level comparison of the baseline documents showing what agrees across schedule, wording, application and endorsement; represent absent fields as `null`.
- For each challenge variant, name the exact conflicting/missing values and locations. Do not call them fraud indicators or infer intent.
- A compact scoring checklist: correct product/claim classification; correct clause and verbatim evidence; correct page; correct document key; correct required/conditional status; correct abstention when absent; and clear surfacing of mismatches for a human reviewer.

## Product-specific content guide

Choose only the selected product. Use a simple fictional product and avoid pretending these are market standards.

- **Private motor comprehensive:** schedule the vehicle registration as an obvious test token, make/model/year, use, territory, vehicle value, separate own-damage/fire/theft and third-party bodily-injury/property-damage limits, and one excess. Include distinct accident, theft and third-party claim categories; use only catalog-backed claim evidence keys such as ClaimForm, PoliceAbstract, PhotoEvidence, GarageEstimate, Invoice, CustomerId, and MedicalReport where appropriate. Do not make logbook or driving permit a keyed requirement unless I supplied an active key.
- **Motor third-party:** state that the sample covers the fictional third-party liability described in the wording; do not add own-damage cover. Separate third-party injury and property-damage claim categories and make evidence conditional where the fictional wording says so.
- **Property and fire:** identify one fictional insured location and separately list building, contents or stock only if selected in the schedule. Define covered fire-related events, exclusions, any optional extension and whether it is selected. Include a property-damage claim type and evidence requirements with supported keys such as ClaimForm, PhotoEvidence, Invoice and CustomerId.
- **Individual life:** distinguish proposer, life assured and beneficiary; state the fictional benefit, term, premium mode, and any arbitrary waiting period only if explicitly included. Include a death-benefit claim path and relevant evidence requests using only supported keys such as ClaimForm, MedicalReport and CustomerId. Do not invent an NIRA-specific IIP key.
- **Health:** define who is covered, plan period, benefit table with limits/sub-limits, whether inpatient/outpatient benefits are included, any fictional waiting period, and a clear authorization/claim path. Use ClaimForm, Invoice, MedicalReport and CustomerId only where the fictional clause asks for them.
- **Personal accident:** schedule the covered person/group, term and distinct benefits (for example accidental death, disability or medical expense) with arbitrary clearly labeled QA limits; define the event categories and conditional evidence.
- **Marine cargo:** identify a fictional shipment and transit period, cargo description/value, covered events and exclusions, and evidence for cargo damage/loss. Use only catalog keys for requirements; mark any unsupported transport document as not configured.
- **Credit life:** distinguish borrower, lender/beneficiary and covered loan; state the arbitrary outstanding balance/benefit basis and term. Define the death or disability claim path and conditions without inventing legal requirements.

## Output quality check

Before returning the answer, silently confirm: all names/data are invented; all documents carry the synthetic warning; the clean baseline agrees across all four documents; each page citation points to a visible page marker; all claim requirements map to an allowed document key or are explicitly not configured; all clauses/quotes match exactly; the three variants each contain only one intended issue; and the answer key identifies what IIP should extract without making a claim decision. Now produce the documents themselves.

