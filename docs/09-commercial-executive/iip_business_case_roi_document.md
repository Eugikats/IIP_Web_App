# IIP Business Case and ROI Validation Model

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Version | 2.0 |
| Status | Hypothesis pack; not commercially approved |
| Audience | Insurer sponsors, operations, finance, procurement, product, executive leadership |
| Updated | 2026-08-05 |
| Release focus | Motor own-damage claim evidence review and follow-up |

## Authority and evidence

Use this document with:

- [Product definition and validation charter](../01-strategy-requirements-roadmap/iip_product_definition_and_validation_charter.md)
- [User, business, and market research program](../research/iip-user-business-market-research-program.md)
- [Market, regulatory, and competitive evidence pack](../research/iip-market-regulatory-competitive-evidence-2026-08-05.md)
- [Research evidence register](../research/iip-research-evidence-register.csv)
- [Pricing and packaging hypotheses](iip_pricing_packaging_document.md)

Public evidence establishes that Uganda has an active insurance market and a concentrated non-life segment. It does **not** establish IIP demand, insurer case volumes, achievable savings, willingness to pay, purchase authority, or adoption. Those claims require target-insurer baselines, buyer evidence, and a measured pilot.

The former illustrative claims of USD 16.5 million annual benefit, 33x-66x ROI, and three-to-six-month payback are withdrawn. They had no target-insurer baseline or attributable pilot evidence and must not be used in proposals, forecasts, or product approval.

## Release 1 business case

Release 1 tests one bounded proposition:

> For staff handling non-injury motor own-damage claims, can an evidence-grounded workspace reduce avoidable handling and follow-up effort while preserving human authority, accuracy, auditability, and safe recovery?

Candidate value pools are hypotheses until measured:

| Value pool | Candidate mechanism | Required evidence | Excluded inference |
|---|---|---|---|
| Handling effort | Extract, organize, and summarize claim evidence for review | Observed task time and staff cost before/after | Staff reduction without an approved workforce plan |
| Missing-evidence follow-up | Surface gaps and prepare human-reviewed requests | Follow-up count, delay, and resolution time | Faster settlement unless end-to-end causality is measured |
| Rework | Preserve evidence lineage and route corrections | Repeat work, corrections, reopenings, and defect severity | Zero-error automation |
| Operational control | Show queue, ownership, age, SLA, and pending action | Current reporting effort and intervention outcomes | Leakage or fraud savings without finance-approved attribution |
| Audit preparation | Retain sources, actions, approvals, and overrides | Audit evidence preparation time and completeness | Regulatory compliance certification |

Customer acquisition, retention, underwriting, fraud-loss prevention, and claims leakage are outside the Release 1 ROI unless separately baselined and causally measured.

## Baseline data contract

Before setting any ROI target, the insurer and IIP team must agree the population, observation window, source system, exclusions, owner, and quality limits for:

| Variable | Minimum definition |
|---|---|
| Eligible cases | Non-injury motor own-damage cases matching the approved pilot boundary |
| Case volume | Monthly eligible cases, seasonality, and usable-sample count |
| Handling time | Active staff minutes by task, separated from customer/third-party wait time |
| Loaded labor cost | Finance-approved salary, benefits, overhead, and currency basis |
| Rework | Repeated review, re-keying, correction, reopening, and avoidable follow-up |
| Cycle time | Start/end events, paused states, and external waiting time |
| Quality | Extraction defects, missed evidence, unsafe drafts, overrides, and downstream corrections |
| Control effort | Time spent producing backlog, SLA, audit, and management views |
| Technology cost | Cloud, AI, document processing, storage, integration, support, and security operations |
| Change cost | Configuration, migration, training, governance, support, and dual-running time |

No target is approved until the baseline owner signs off the definition and data extract.

## Calculation model

Use one currency and one time window throughout.

```text
annual_handling_benefit
  = eligible_cases_per_year
  * verified_minutes_saved_per_case
  / 60
  * finance_approved_loaded_hourly_cost

annual_rework_benefit
  = avoidable_rework_events_removed
  * verified_minutes_per_rework_event
  / 60
  * finance_approved_loaded_hourly_cost

annual_control_benefit
  = verified_control_hours_removed
  * finance_approved_loaded_hourly_cost

total_verified_annual_benefit
  = annual_handling_benefit
  + annual_rework_benefit
  + annual_control_benefit
  + other_finance_approved_attributable_benefit

year_one_total_cost
  = discovery_and_pilot
  + implementation_and_integration
  + annual_platform_and_support
  + customer_cloud_and_usage
  + customer_change_and_operating_cost

year_one_roi
  = (total_verified_annual_benefit - year_one_total_cost)
  / year_one_total_cost

payback_months
  = year_one_total_cost
  / (total_verified_annual_benefit / 12)
```

If benefit is zero, negative, unmeasured, or not attributable, report it as such. Do not substitute vendor benchmarks for insurer evidence.

## Scenario sheet

| Input | Conservative | Expected | Upper bound | Evidence ID / owner |
|---|---:|---:|---:|---|
| Eligible cases/year | TBD | TBD | TBD | Insurer operations |
| Minutes saved/case | TBD | TBD | TBD | Timed pilot comparison |
| Loaded hourly cost | TBD | TBD | TBD | Insurer finance |
| Rework events removed | TBD | TBD | TBD | Claims QA |
| Control hours removed | TBD | TBD | TBD | Operations management |
| Other attributable benefit | 0 | TBD | TBD | Finance approval required |
| Year-one total cost | TBD | TBD | TBD | Joint cost model |
| Year-one ROI | Not calculated | Not calculated | Not calculated | Derived only after inputs |
| Payback | Not calculated | Not calculated | Not calculated | Derived only after inputs |

The upper bound is a sensitivity test, not a forecast. Every non-zero input needs a repository evidence ID.

## Pilot measurement design

1. Freeze definitions and baseline window before exposing participants to IIP.
2. Use representative historical or live cases under approved data controls.
3. Compare like-for-like tasks and stratify by case complexity and document quality.
4. Record active handling time, waiting time, correctness, overrides, recovery, and downstream rework separately.
5. Keep human review mandatory; count that effort in the cost and time result.
6. Track adoption and workarounds, not just task completion.
7. Have insurer operations and finance approve attribution before annualizing.
8. Report sample size, missing data, confidence interval or range, and threats to validity.

## Decision scorecard

| Dimension | Advance evidence | Rethink / stop evidence |
|---|---|---|
| Problem | Repeated, recent costly workflow with a named owner | Infrequent problem or already solved adequately |
| Usability | Target roles complete representative tasks safely | Critical tasks need expert rescue or encourage unsafe reliance |
| Quality | Accuracy and recovery meet insurer-approved thresholds | Material misses, hidden uncertainty, or harmful downstream rework |
| Economics | Finance-accepted benefit exceeds full operating cost at credible adoption | Value depends on unsupported revenue, leakage, or headcount claims |
| Adoption | Staff use the workflow with manageable training and change load | Persistent workaround, duplicate entry, or loss of trust |
| Buying | Sponsor, budget path, security/procurement process, and authority identified | Interest without authority, budget, data access, or next commitment |

## Buyer evidence still required

- Last comparable purchase, budget source, approval chain, and economic buyer.
- Procurement, legal, security, data-protection, outsourcing, and vendor-onboarding sequence.
- Required hosting, data location, tenant isolation, support access, and exit conditions.
- Integration owners, allowable system access, implementation capacity, and change calendar.
- Paid-pilot authority, acceptable pilot terms, conversion criteria, and renewal owner.
- Costs of the current workflow and which benefits finance will recognize.

## Evidence limits

- Market premium and license counts are context, not software revenue opportunity.
- Vendor feature pages establish positioning, not independent performance.
- Interview intent is not willingness to pay; stronger evidence is access, time, data, procurement action, and money.
- A local prototype or synthetic test is not insurer production evidence.
- Pilot results do not generalize beyond the sampled organizations, roles, products, geographies, and operating conditions without replication.
- Legal and regulatory interpretations require qualified counsel and insurer compliance approval.

## Commercial approval gate

No ROI claim may enter a proposal or roadmap decision until it has:

- a repository evidence ID;
- a named insurer source and measurement period;
- finance-approved cost and attribution;
- full implementation, cloud, support, human-review, and change costs;
- stated range, exclusions, and uncertainty;
- product and commercial owner approval.
