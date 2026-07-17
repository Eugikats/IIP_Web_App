# Phase 11 Commercial Module Activation Gate

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Phase 11 Commercial Module Activation Gate |
| Version | 1.0 |
| Status | Draft |
| Audience | Product owner, delivery lead, engineering lead, pilot sponsor, commercial lead |
| Date | 2026-07-09 |

This gate prevents the built-ahead commercial modules from pulling the program away from the MVP wedge before Motor Claims and Document Intelligence are piloted. It is a hold-and-activation control, not a request to build more Phase 11 scope now.

## 1. Standing Hold Rule

Fraud Intelligence, Underwriting Intelligence, AI Agent Registry, Billing/entitlements, and future marketplace metering remain in **hold** status until the Phase 10 pilot evidence supports activation.

Do not extend these modules before activation approval except for:

- security fixes,
- tenant-isolation fixes,
- zero-simulation fixes,
- compile/test fixes caused by shared code changes,
- documentation that clarifies the hold boundary,
- read-only audits requested by the maintainer.

## 2. Non-Negotiable Boundaries

- AI must not make final claims, fraud, underwriting, payment, settlement, policy-binding, or pricing decisions.
- Fraud output remains advisory and non-accusatory.
- Underwriting output remains support/referral guidance for a human underwriter.
- Agent Registry and kill switch can only narrow capability; they never bypass `ApprovalDecisionGuard`.
- Billing/entitlement state must use real internal counters or `Not configured`; never fabricated usage.
- Marketplace metering, payment collection, and external billing emission remain inactive until commercial pricing and Azure cost ownership are approved.

## 3. Activation Preconditions

| Gate | Required Evidence | Status |
|---|---|---|
| MVP pilot workflow proven | FNOL → queue → claim review → human approval/customer update → evidence pack succeeds in an approved environment | Blocked until authenticated UAT is complete |
| Pilot daily stabilization in use | Daily stabilization log records no unresolved critical blockers for the agreed observation window | Not started |
| Weekly pilot steering reviewed | Weekly status reports show stable adoption, issue trend, and sponsor review | Not started |
| Provider state accepted | Required providers are either real/configured or explicitly accepted as `Not configured` for the module scope | Not started |
| Security and tenant isolation accepted | No unresolved critical/high tenant-isolation, auth, audit, or data-exposure issue | Not started |
| Commercial owner approves scope | Commercial lead approves whether module is pilot add-on, paid add-on, or deferred | Not started |
| Product owner approves sequencing | Product owner confirms the module will not disrupt Motor Claims MVP stabilization | Not started |

## 4. Module Activation Matrix

| Module | Current Phase 11 Position | Activation Trigger | Extra Evidence Required |
|---|---|---|---|
| Fraud Intelligence | Built-ahead rule-based v1; hold | Pilot sponsor asks for advisory fraud/risk review after MVP workflow is stable | Non-accusatory language review, audit event review, false-positive handling plan |
| Underwriting Intelligence | Built-ahead rule-based v1; hold | Underwriting workflow is added to post-pilot scope | Human-underwriter decision boundary, referral-note review, underwriting SME acceptance |
| AI Agent Registry + kill switch | Built-ahead control plane; hold | Governance team requires tenant-level AI controls beyond current policy settings | Kill-switch audit evidence, disabled-agent fail-closed evidence, no autonomy regression |
| Billing and entitlements | Built-ahead internal counters; hold | Pricing model and cost ownership are approved | Usage-counter evidence, entitlement/authorization separation review, commercial sign-off |
| Marketplace metering | Not active | Azure Marketplace/private-plan monetization decision is approved | Partner Center plan decision, metering API design, legal/commercial approval |

## 5. Allowed Readiness Work While Held

| Work Type | Allowed? | Notes |
|---|---:|---|
| Fixing broken tests caused by shared refactors | Yes | Keep scope to restoring existing behavior. |
| Removing fake data or deployed-runtime mock leakage | Yes | Required by platform contract. |
| Adding new module capabilities | No | Requires activation approval. |
| Adding Azure OpenAI narration on top of rule engines | No | Requires real provider readiness and activation approval. |
| Adding external billing/payment integration | No | Requires pricing, cost ownership, and security review. |
| Auditing current module boundaries | Yes | Report findings without widening scope. |

## 6. Activation Decision Record

| Field | Value |
|---|---|
| Module |  |
| Requested By |  |
| Business Reason |  |
| Pilot Evidence Reviewed |  |
| Required Provider State |  |
| Security/Compliance Review | Approved / Rejected / Deferred |
| Product Decision | Activate / Hold / Defer |
| Commercial Decision | Included / Add-on / Deferred |
| Decision Date |  |
| Conditions |  |

## 7. Current Decision

As of 2026-07-09, Phase 11 remains **held**. The next program focus stays on unblocking authenticated UAT, provider readiness, staging capability, and controlled pilot execution for Motor Claims and Document Intelligence.

