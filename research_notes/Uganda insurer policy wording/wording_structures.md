# Uganda insurer policy wording structures for IIP onboarding tests

## 1. What information should an IIP onboarding extractor expect in a policy wording and schedule?

### Takeaway

Treat the policy as a linked document set: policy wording, schedule/certificate, proposal or application, declarations, and endorsements may each contain different parts of the contract. Build extraction around the actual text and evidence location, and represent absent or ambiguous values as unknown rather than filling them from a generic template.

### Cited Findings

- Uganda's Insurance Act provides that insurers must not issue policy or proposal-form text/format unless approved by the Authority, and approved text is not to be amended without prior written approval. This means generated wording must be clearly marked synthetic and must not be presented as an insurer-approved product. [Insurance Act, sections 64–65](https://ira.go.ug/wp-content/uploads/2025/01/Final-Insurance-Act-1.pdf)
- An Uganda Insurers Association traders' booklet says the policy document issued upon confirmation of cover contains a schedule of benefits, terms, warranties and conditions, and claims procedure. It also notes that the listed solutions are not exhaustive and insurers may customize cover. [UIA, Traders’ Insurance Solutions Booklet](https://www.uia.co.ug/sites/default/files/2024-12/Traders%E2%80%99%20Insurance%20Solutions%20Booklet%20ENGLISH.pdf)
- The published Jubilee Allianz General fire wording says cover responds to property described in the schedule for sums specified there and is subject to the wording's terms, definitions, exclusions and conditions; it also incorporates proposal particulars. [Jubilee Allianz General, Fire Insurance Policy](https://www.beanafrica.com/Allianz/ug/PolicyDocs/Fire%20Policy.pdf)
- The fire wording makes endorsements conditional on their number appearing in the schedule, showing why an extractor should link schedule selections to the corresponding endorsement text rather than assume every printed clause applies. [Jubilee Allianz General, Fire Insurance Policy](https://www.beanafrica.com/Allianz/ug/PolicyDocs/Fire%20Policy.pdf)
- A Ugandan insurance tribunal describes policy particulars as including parties, subject matter, period, commencement date, insured peril, and exemptions; it treats the policy as the contract governing the parties' rights. [Insurance Appeals Tribunal, UAP Old Mutual v African Queen Ltd](https://iat.go.ug/wp-content/uploads/2023/02/Application-01-of-2022-UAP-Old-Mutual-Insurance-Uganda-Ltd-vs-African-Queen-Limited.pdf)
- The Insurance Act says premiums are payable in full on or before policy inception or renewal, subject to any alternative method provided by regulation. [Insurance Act, section 62](https://ira.go.ug/wp-content/uploads/2025/01/Final-Insurance-Act-1.pdf)
- The IRA's published approval form for index insurance products asks for company, product name/status, covered risks, target market, supporting policy/proposal/key-features documents and premium-rating structure. This is a product-specific approval form, not a universal policy schedule, but it demonstrates that product identity, risk, target market and supporting-document links matter. [Insurance (Index Contracts) Regulations, 2020](https://ira.go.ug/cp/uploads/THE%20INSURANCE%20%28%20INDEX%20COTRACTS%29%20REGULATIONS%202020.pdf)
- A public Uganda private-motor proposal form asks where the vehicle is normally kept, its address/territory and storage arrangement, driver health/conviction details, and past/current motor-insurance history. These are proposal/underwriting inputs, not necessarily fields printed in every issued schedule. [Statewide Insurance, Motor Commercial Proposal Form](https://www.swico.co.ug/wp-content/uploads/2023/07/Motor-Commercial-Proposal-Form.pdf)
- The CIC Family Life application asks for life-assured identity/contact/occupation, selected cover option and premium, declared family members and relationships, main/contingent beneficiaries, health questionnaire answers, and declaration/consent. [CIC Uganda, Family Life Plan document](https://ug.cicinsurancegroup.com/wp-content/uploads/2022/07/CIC-Uganda-Family-Life-Plan.pdf)

### Inferences

- A practical extraction schema should separate: insurer/product/version; policy and proposal identifiers; policyholder/insured/beneficiary/other interested party; insured subject and location; period and inception/expiry; premium, currency and payment frequency/status; benefit/cover sections; sums insured, sublimits, deductible/excess and basis of settlement; covered perils; exclusions; conditions/warranties; claims-notice steps and supporting documents; territory/use; definitions; renewal/cancellation; endorsements and their applicability; complaints contacts; and source-page/paragraph evidence. These are a recommended data model inferred from the sources above, not a regulator-prescribed universal field list.
- Store each extracted clause with the source document, page/section, verbatim excerpt, confidence, and whether it comes from schedule, base wording, proposal, or endorsement. This allows the onboarding reviewer to resolve conflicts across the policy bundle.
- Keep insurer/product-specific values configurable. Do not infer a standard deductible, exclusion, waiting period, territorial area, or required claim document from another insurer's policy.

### Gaps

- Public sources do not establish one mandatory Uganda-wide schedule layout or a complete universal list of fields for every insurance class; each insurer/product wording must be treated as authoritative for its own policy.
- Some published PDFs are inconsistent or may be stale. The CIC-hosted Family Life PDF is branded for Uganda and lists UGX sums/contacts, but its policy clause says premiums are payable in Kenya's currency. Verify the current insurer-approved version before using any clause/value as a golden reference. [CIC Uganda, Family Life Plan document](https://ug.cicinsurancegroup.com/wp-content/uploads/2022/07/CIC-Uganda-Family-Life-Plan.pdf)

## 2. How does policy structure and required claim evidence vary across common Ugandan insurance products?

### Takeaway

The extractor should first classify the product and the insured event, then apply a product-specific checklist. Motor, property/fire, life/funeral, and health policies use different insured subjects, benefit structures, conditions, and claim evidence; the insurer wording and schedule control the exact requirements.

### Cited Findings

- IRA distinguishes motor third-party, accident/fire/theft, and comprehensive covers. Comprehensive combines own-vehicle loss/damage and third-party injury/property liability, while third-party cover responds to third-party losses. [IRA FAQs](https://ira.go.ug/frequently-asked-questions/)
- CIC describes its comprehensive motor cover in two parts: own damage/fire/theft and legal liability to third parties for bodily injury/death or property damage, with exclusions for some people/property relationships stated on its product page. [CIC Uganda, Motor Comprehensive Insurance](https://ug.cicinsurancegroup.com/corporates-solutions/motor-comprehensive-insurance/)
- IRA motor-accident guidance recommends police reporting promptly, notifying the insurer as soon as possible, and sending available police abstract, driving licence and driver's statement; it also advises collecting involved-party, vehicle and witness details. [IRA FAQs](https://ira.go.ug/frequently-asked-questions/)
- The IRA claims guidelines list a completed claim form, copy of driving permit, police report/sketch plan, logbook, third-party demand letter (if any) and subrogation deed (where a third party caused the accident) for motor third-party/COMESA claims. The guideline calls for timely reporting and documentation; it does not make this list universal for every product or every motor claim. [IRA Insurance Claims Guidelines](https://ira.go.ug/cp/uploads/claimesguidelines.pdf)
- Jubilee Allianz's fire wording insures scheduled buildings/property against fire/lightning subject to exclusions and conditions; special-peril extensions and other clauses apply only when referenced in the schedule. [Jubilee Allianz General, Fire Insurance Policy](https://www.beanafrica.com/Allianz/ug/PolicyDocs/Fire%20Policy.pdf)
- Mayfair Uganda describes fire cover, exclusions and optional extensions, including that some perils may be added subject to insurer endorsement and extra premium; it lists examples such as riot/strike, explosion, flood/storm and earthquake extensions. This is Mayfair's product explanation, not a universal market clause set. [Mayfair Uganda, Fire and Allied Perils](https://ug.mayfairinsurance.africa/solutions/property/fire-and-allied-perils/)
- IRA says life death-claim documents may include a death claim form, NIRA death certificate, original policy document, nominee's bank details and nominee's national identity card; an accident may call for police/post-mortem reports, while health-related death may require a physician's statement. [IRA, Endowment Policy Claims](https://ira.go.ug/when-to-place-a-claim-on-that-endowment-insurance-policy/)
- The CIC Family Life example has product-specific waiting periods and first-death shared benefits, declared family-member categories, age/eligibility conditions, premium options and exclusions. Its proposal collects named beneficiaries and a health questionnaire. [CIC Uganda, Family Life Plan document](https://ug.cicinsurancegroup.com/wp-content/uploads/2022/07/CIC-Uganda-Family-Life-Plan.pdf)
- UIA explains that life products differ: term assurance pays on death within a term; endowment can combine protection and investment/maturity features; funeral insurance may be cash or service-based. [UIA Life FAQs](https://uia.co.ug/life-faqs)
- UIA says medical insurance can cover inpatient, outpatient, or both; extensions such as dental, optical, maternity or pre-existing-condition cover vary, and policyholders should confirm what their specific wording covers and excludes. [UIA Medical FAQs](https://mail.uia.co.ug/medical-faqs)
- Liberty Uganda publishes separate medical membership guides, policy conditions, benefit tables, hospital/scan pre-authorisation forms, chronic-medicine applications, dependant amendment forms and provider forms. This demonstrates that health coverage commonly uses linked benefit/administration documents in addition to the core policy. [Liberty Uganda, Forms & Documents](https://www.liberty.co.ug/forms-documents)
- Prudential Uganda's group-health page describes employer/group cover and lists principal-member application evidence such as National ID and enrolment form, with entry-age and reinstatement waiting-period conditions for that product. [Prudential Uganda, Group Health Plans](https://www.prudential.ug/group-health-plans-2/)

### Inferences

- Build separate onboarding extraction profiles for at least motor, property/fire, life/funeral, health, and group policies; each profile should capture product-specific concepts and allow optional fields instead of forcing one universal form.
- For motor: test registration/chassis/engine, vehicle make/model/use, drivers, territory, cover type, insured value, excess, third-party limits, extensions/endorsements, and claim-reporting steps. For property: locations, occupancy, building/contents/stock items, sums insured, insured perils, valuation basis, warranties, and selected extensions. For life: life assured, proposer, beneficiary/nominee, sum assured, term, premium mode, waiting period, exclusions, and evidence at death/maturity. For health: member/dependants, benefit table, limits/sub-limits, provider/network, pre-authorisation and waiting periods.
- A claim-evidence checklist should be generated from the policy/product and loss type; missing evidence should be labelled “required by this policy/checklist” only when supported by the actual wording or an insurer-confirmed rule.
- To test extraction, include consistent and deliberately conflicting document bundles: schedule versus wording dates, an endorsement selected in the schedule, a vehicle identifier mismatch, declared beneficiary versus claim form, and a health benefit-table limit conflict. These are synthetic QA scenarios, not fraud determinations.

### Gaps

- There is no reliable public evidence for a single “average insurer” wording or fixed market-wide claim-document list. Product type, insurer, benefit choice and endorsements change requirements.
- Some insurer sites provide product summaries rather than the complete approved wording; public summaries are useful for taxonomy but should not be treated as contractual text.
- Claims requirements may change by insurer, product version, loss cause and circumstances; obtain current approved policy bundles and claims checklists directly from participating insurers before using them as production rules.

## 3. What should the synthetic policy wording prompt and onboarding test pack contain?

### Takeaway

Generate a clearly fictional, internally consistent policy bundle for extraction testing, not a purported real or regulator-approved contract. Use a motor comprehensive sample first because schedules, vehicle identifiers, dates, limits, excesses, cover sections and endorsements produce varied, testable fields; then add separate life, property and health packs.

### Cited Findings

- Uganda's Insurance Act requires approval of policy/proposal text and format before issue, and amendments to approved policy text require prior written approval. [Insurance Act, section 64](https://ira.go.ug/wp-content/uploads/2025/01/Final-Insurance-Act-1.pdf)
- Uganda's IRA provides distinct types of motor cover and identifies different own-damage and third-party elements; this supports making the synthetic policy explicit about the selected cover rather than using an ambiguous “motor insurance” label. [IRA FAQs](https://ira.go.ug/frequently-asked-questions/)
- CIC's motor product distinguishes own damage/fire/theft from third-party liability and describes limits/perils separately, which supports separate schedule fields per coverage section. [CIC Uganda, Motor Comprehensive Insurance](https://ug.cicinsurancegroup.com/corporates-solutions/motor-comprehensive-insurance/)
- The IRA claims guidelines provide a Uganda-specific motor-third-party evidence example, while IRA life guidance gives a distinct death/maturity document example; these can anchor different synthetic claim packs. [IRA Insurance Claims Guidelines](https://ira.go.ug/cp/uploads/claimesguidelines.pdf); [IRA, Endowment Policy Claims](https://ira.go.ug/when-to-place-a-claim-on-that-endowment-insurance-policy/)
- The Uganda Insurance Act states premiums are payable by inception/renewal unless another allowed payment method applies, so a generated example should make premium, payment mode and effective inception internally consistent. [Insurance Act, section 62](https://ira.go.ug/wp-content/uploads/2025/01/Final-Insurance-Act-1.pdf)

### Inferences

- Suggested prompt for Claude to generate a first onboarding test bundle:

  > Create a **fictional synthetic policy bundle for software QA only**, set in Uganda and denominated in UGX. Do not use any real insurer's name, logo, policy number, customer data, copied clauses, or claim decision. Do not claim the wording is legally valid, insurer-approved, or suitable for sale. Create four separate, text-searchable documents: (1) a one-page policy schedule, (2) a short policy wording, (3) a proposal/application form, and (4) one selected endorsement. The product is fictional private motor comprehensive cover. Keep every value consistent across documents and include a visible label “SYNTHETIC TEST DOCUMENT — NOT AN INSURANCE CONTRACT.”
  >
  > Include fictional insurer/product/version, policy/proposal number, policyholder and insured contact fields, vehicle registration/make/model/year/chassis/engine, use, territory, inception and expiry timestamps, premium/currency/payment frequency and receipt status, insured value and valuation basis, separate own-damage/fire/theft and third-party bodily-injury/property-damage limits, excess/deductible (including any different excess by loss type), selected extensions, exclusions, conditions/warranties, definitions, claims notification instructions, a product-specific list of evidence requested for accident and theft, renewal/cancellation terms, complaint contact placeholders, and endorsement number/effective date. Make the schedule explicitly list the endorsement by number and ensure the endorsement modifies only the stated clause.
  >
  > Add one separate QA variant with exactly three intentional discrepancies: the schedule expiry date differs from the wording period by one day; the vehicle registration differs between proposal and schedule; and the selected endorsement number is absent from the attached endorsement. Mark these discrepancies only in a private answer key, not in the customer-facing documents. Provide a JSON extraction answer key mapping every expected field to document name, page/section and verbatim evidence, with null for any field not stated. Do not infer missing values. Also provide a list of expected cross-document mismatch flags, without calling them fraud.

- To make the test robust, generate separate variants for a clean motor claim, theft with inconsistent ownership evidence, property fire with a selected special-peril endorsement, a life death claim with a named beneficiary and conditional supporting documents, and a health claim with an explicit benefit table. Do not combine products or share clauses across them unless the generated wording explicitly does so.
- Mark each policy version and issuer as synthetic, retain source/page evidence in the extraction answer key, and have a human reviewer compare extracted rules with source text before making them available to an agent.

### Gaps

- A synthetic template cannot establish the legal sufficiency, regulatory approval, insurer-specific market practice, actual tariff, or claims acceptance criteria for any Ugandan product; an insurer's current approved documentation and legal/compliance review are required for those purposes. [Insurance Act, section 64](https://ira.go.ug/wp-content/uploads/2025/01/Final-Insurance-Act-1.pdf)
- Public material does not establish a defensible national average premium, sum insured, deductible, or product-wide standard clause. Do not invent market averages for synthetic examples; label test numbers as arbitrary QA values.
