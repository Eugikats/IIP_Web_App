# IIP Research Repository

| Field | Value |
|---|---|
| Status | Active proposed governance; seed register created, owner approval pending |
| Owner | Product Research — named person required |
| Date | 2026-08-05 |
| Scope | User, business, market, regulatory, competitive, commercial, and evaluative evidence |

## Purpose

This directory is IIP's research provenance layer. It records what evidence exists, where it came from, what it can support, what it cannot support, and which product or commercial decisions it affected.

It is not a store for raw confidential participant material or real insurer/customer evidence.

## Repository structure

```text
docs/research/
  README.md                                  # governance and schema
  iip-research-evidence-register.csv         # source/claim/decision index
  iip-user-business-market-research-program.md
  *-desk-research.md                         # cited public-source research
  *-questionnaire.md / *-form.gs             # approved instruments
  *-usability-evaluation.md                   # protocols, redacted observations and evidence-bounded findings
  *-synthesis.md                              # redacted, traceable findings only
  *-decision-log.md                           # evidence-to-decision records when needed
```

Raw recordings, transcripts, screenshots, production artifacts, customer records, credentials, and identifiable participant notes must live only in an approved access-controlled research store with retention and deletion controls. Git contains only anonymized metadata, redacted observations, approved excerpts, and aggregated synthesis.

## Evidence-register schema

Every substantive source or participant session receives one stable row in `iip-research-evidence-register.csv`.

| Field | Required | Meaning |
|---|---:|---|
| `evidence_id` | Yes | Stable ID such as `PUB-UG-IRA-2025-MARKET`, `INT-PRACT-001`, or `ART-CLAIM-001`. |
| `source` | Yes | Short source or session label. |
| `source_type` | Yes | `public-primary`, `vendor-primary`, `participant`, `artifact`, `observation`, `survey`, `analytics`, `repository`, or `secondary`. |
| `source_owner` | Yes | Authority, vendor, anonymized participant ID, system, or document owner. |
| `source_date` | Yes | Publication, event, or session date. |
| `collected_date` | Yes | Date entered or refreshed. |
| `geography` | Yes | Country/region and institution context where relevant. |
| `participant` | Conditional | Anonymized ID plus behavior/role context; `not-applicable` for desk evidence. |
| `method` | Yes | Interview, contextual inquiry, survey, regulatory review, vendor-document review, telemetry query, code inspection, etc. |
| `claim_or_observation` | Yes | Atomic statement supported by the source. One row must not combine unrelated claims. |
| `confidence` | Yes | `high`, `medium`, or `low`. |
| `confidence_rationale` | Yes | Source quality, relevance, context diversity, consistency, contradiction, and missingness. |
| `limitations` | Yes | What the evidence cannot establish. |
| `decisions_affected` | Yes | Stable decision/requirement/roadmap IDs or `none-yet`. |
| `status` | Yes | `planned`, `collected`, `verified`, `superseded`, `expired`, or `excluded`. |
| `review_or_expiry_date` | Yes | Refresh trigger or date. |
| `location` | Yes | Public URL, repository path, or approved secure-store reference; never a secret URL. |
| `privacy_class` | Yes | `public`, `internal`, `confidential-research`, or `restricted`. |
| `researcher` | Yes | Accountable researcher or team. |

## Identifier rules

| Prefix | Use |
|---|---|
| `PUB` | Public primary authority or official statistical source |
| `VND` | Vendor-owned product evidence |
| `SEC` | Secondary research |
| `INT` | Interview session |
| `OBS` | Contextual observation |
| `ART` | Redacted artifact walkthrough |
| `SUR` | Survey dataset or wave |
| `ANA` | Product/operational analytics |
| `REP` | Repository code/test/document evidence |

Participant IDs must not encode a person's name, employer, email, phone number, branch, or other direct identifier.

## Confidence rules

Confidence is claim-specific, not source-wide.

| Level | Use when |
|---|---|
| High | The owning primary source directly supports the claim, or multiple relevant methods/contexts converge with no material unresolved contradiction. |
| Medium | The source is credible but indirect, dated, vendor-authored, context-limited, or supported by a small/diverse-but-incomplete participant corpus. |
| Low | The claim is a hypothesis, single-context report, unverified repository assumption, marketing assertion, or materially contradicted. |

Counts describe the analyzed corpus, not the market. High consequence minority, accessibility, legal, safety, or exclusion evidence remains visible even when rare.

## Research chain

Maintain this chain without skipping levels:

```text
source -> atomic observation -> pattern -> interpretation -> insight
       -> need/opportunity -> implication -> approved decision/requirement
```

Personas, jobs, journeys, market segments, ROI, price, and roadmap changes are interpretations or decisions. They must link back to evidence IDs and retain disconfirming evidence.

## Decision update contract

An approved research-driven change records:

- decision or requirement ID;
- prior statement;
- new statement;
- evidence IDs supporting and contradicting it;
- confidence and limitation;
- accountable approver and approval date;
- affected product, UX, architecture, security, commercial, test, and roadmap documents;
- follow-up measurement or expiry trigger.

Desk research may define obligations, market boundaries, and hypotheses. It does not validate an insurer's workflow, user need, willingness to pay, procurement route, usability, adoption, or ROI.

## Intake and quality checklist

- [ ] Research decision and owner named.
- [ ] Existing evidence and uncertainty inventoried.
- [ ] Method matches the question.
- [ ] Participant recruitment is behavior-based.
- [ ] Consent, recording, access, retention, deletion, and withdrawal defined.
- [ ] Sensitive data minimized; raw evidence kept outside Git.
- [ ] Analysis plan and decision rule written before collection.
- [ ] Contradictions, exclusions, missingness, and researcher influence retained.
- [ ] Evidence register updated.
- [ ] Approved decisions propagated to owning documents and tests.
