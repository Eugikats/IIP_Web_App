# IIP Pricing and Packaging Hypotheses

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Version | 2.0 |
| Status | Research hypotheses; no approved prices |
| Audience | Commercial, finance, product, insurer buyers, procurement |
| Updated | 2026-08-05 |
| Release focus | Motor own-damage claim evidence review and follow-up |

## Authority and limits

Use this document with the [business case and ROI validation model](iip_business_case_roi_document.md), [research program](../research/iip-user-business-market-research-program.md), and [product definition charter](../01-strategy-requirements-roadmap/iip_product_definition_and_validation_charter.md).

The former broad Standard, Professional, Enterprise, customer-acquisition, fraud, early-warning, and underwriting packages are withdrawn as current offers. They exceed validated Release 1 scope. No price, discount, deployment preference, or willingness-to-pay result is approved yet.

## Packaging principles

- Package the smallest complete customer outcome, not the largest feature list.
- Keep evidence lineage, human review, tenant isolation, audit, safe failure, and recovery in the core offer.
- Separate services, platform rights, customer cloud/provider usage, integration, and support.
- Make variable cost exposure visible and capped; do not advertise unlimited AI or document processing.
- Do not use seats as the only value metric until role concurrency and buyer preference are known.
- Do not assume WhatsApp, a particular core-system integration, customer-tenant deployment, or Marketplace procurement without buyer evidence.
- Time-bound every pilot and state conversion, exit, data-return, and deletion conditions.

## Hypothesis ladder

| Offer hypothesis | Intended decision | Included scope | Explicit exclusions |
|---|---|---|---|
| Readiness and value-baseline engagement | Is there a qualified problem, usable data, buyer path, and measurable baseline? | Workflow observation, baseline definition, data/security review, pilot plan, cost model | Production use, promised ROI, custom product build |
| Controlled paid pilot | Does Release 1 produce safe, attributable value under representative conditions? | Bounded cases/users, configured evidence review, human gates, training, measurement, weekly review, exit report | Autonomous decisions, enterprise-wide rollout, unmeasured modules |
| Release 1 production subscription | Can one approved claims operation run the validated workflow reliably? | Approved Release 1 capability, operations, security, support, usage allowance, change control | Future modules, bespoke integrations not contracted, unsupported geographies/products |

Later modules require their own discovery, validation, cost, safety, and pricing decisions.

## Price architecture to test

| Component | Candidate basis | Why test it | Required buyer evidence |
|---|---|---|---|
| Readiness engagement | Fixed fee for bounded deliverables | Creates a real commitment before custom work | Past spend, signing authority, procurement threshold |
| Pilot | Fixed fee plus explicit usage allowance | Keeps evaluation scope and cost legible | Pilot budget, success criteria, data access, conversion authority |
| Implementation | Fixed scope or milestone fee | Reflects configuration, migration, integration, security, and training | Integration inventory, internal capacity, acceptance owner |
| Platform subscription | Annual fee per approved operation/workspace | Aligns with governed operational use | Budget owner, preferred billing unit, renewal process |
| Usage | Included allowance plus capped overage or customer-paid cloud | Reflects AI, pages, storage, messages, and telemetry cost | Forecast volume, cost predictability needs, invoice constraints |
| Support | Defined service tier | Reflects coverage, response, upgrade, and incident obligations | Criticality, hours, escalation, local support expectations |
| Professional services | Approved change order | Prevents custom work from hiding in subscription | Change authority, day-rate/fixed-fee preference |

## Deployment and channel hypotheses

| Question | Current status | Validation needed |
|---|---|---|
| Customer Azure tenant, vendor-hosted environment, or another model? | Open | Security, data, procurement, operations, and cost interviews |
| Azure Managed Application required? | Open | Buyer deployment policy and operational-support model |
| Azure Marketplace/private plan required? | Open | Actual transacting route and account eligibility; private-plan mechanics do not prove buyer preference |
| CSP procurement required? | Open risk | Microsoft documentation indicates private plans are not supported for CSP-program resellers; verify the buyer's account and route |
| WhatsApp or another external channel required for Release 1? | Open | Observed intake and follow-up workflow; privacy and consent review |
| Deep core-system write-back required? | Open | Workflow necessity, authority, API availability, risk, and integration cost |

## Pricing research sequence

1. Establish the recent problem, current workaround, consequence, and budget owner.
2. Document the last comparable purchase: amount/range, approval chain, timeline, security review, and failure points.
3. Quantify the current operating cost with finance-approved definitions.
4. Test offer comprehension and exclusions before presenting a price.
5. Use several realistic price structures in randomized or counterbalanced research; do not ask only “Would you pay?”
6. Ask what budget would fund it, who can approve it, what it displaces, and what evidence is required.
7. Seek behavioral commitment: named sponsor, data access, technical review, procurement step, letter of intent, or paid pilot.
8. Validate gross margin against full provider, cloud, support, implementation, security, and change costs.

## Pricing hypothesis register

| ID | Hypothesis | Test | Advance signal | Status |
|---|---|---|---|---|
| PRICE-H01 | Buyers will fund a bounded readiness engagement before bespoke implementation | Buyer interviews plus real proposal | Authorized budget and agreed deliverables | Untested |
| PRICE-H02 | A paid pilot is acceptable when success, data use, conversion, and exit are explicit | Real pilot offer | Signed or procurement-approved paid pilot | Untested |
| PRICE-H03 | Annual operation/workspace pricing is easier to approve than pure per-seat pricing | Structured buyer comparison | Consistent preference tied to budget structure | Untested |
| PRICE-H04 | Buyers require predictable usage exposure | Contract and procurement interviews | Preference for allowance/cap or customer-paid cloud | Untested |
| PRICE-H05 | Customer-tenant deployment commands recognized value that covers its operating burden | Security/IT interviews and cost model | Buyer requirement plus accepted incremental price | Untested |
| PRICE-H06 | The measured Release 1 value supports positive buyer ROI and sustainable gross margin | Pilot plus finance review | Finance-accepted value and commercial cost model | Untested |

## Offer worksheet

| Field | Required entry |
|---|---|
| Target organization and operation | Named, qualified segment |
| Job and workflow boundary | Recent evidence with repository IDs |
| Outcome | Measurable and attributable |
| Included users/cases/volume | Explicit ceiling and complexity mix |
| Included capability | Release 1 only, with human responsibilities |
| Data and deployment | Location, controller/processor roles, access, retention, exit |
| Integration | Read/write scope, owners, prerequisites, acceptance |
| Support | Hours, response, escalation, maintenance, upgrade |
| Measurement | Baseline, comparison, quality guardrails, decision date |
| Price and currency | Approved amount, taxes, indexation, payment schedule |
| Variable costs | Allowance, overage/cap, customer-paid items |
| Conversion and exit | Production decision, credit treatment, return/deletion |
| Evidence | Buyer, finance, product, legal, and security approvals |

## Commercial guardrails

- Do not quote placeholder prices as market evidence.
- Do not offer a free pilot when substantial custom work, real data, or operational support is required without an explicit learning investment decision.
- Do not discount without a time limit, decision date, and exchanged commitment.
- Do not bury customer cloud or usage cost in “unlimited” language.
- Do not claim Managed Application, Marketplace, or a private plan removes procurement, security, legal, or deployment work.
- Do not price expected future modules before scope and evidence are approved.
- Do not use a pilot logo, quote, result, or workflow detail without permission.

## Approval gate

A commercial offer becomes approved only when:

- the target segment and buyer roles are evidenced;
- the business-case inputs have named sources and finance review;
- the deployment, data, integration, procurement, and support constraints are known;
- the full cost-to-serve and gross-margin range are reviewed;
- product, commercial, finance, security, and legal owners approve the offer;
- the evidence register records the decision, date, scope, and caveats.
