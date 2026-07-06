# IIP Open Questions & Open Decisions Tracker

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Purpose | Tracking-only view of unresolved business questions and deployment decisions, with the engineering default already in place for each so build work does not block on them |
| Status | Living document |
| Date | 2026-07-03 |
| Related | `docs/08-program-plan/IIP-FULL-PROGRAM-PLAN.md` (W0.2), `docs/01-strategy-requirements-roadmap/iip_assumptions_constraints_open_questions_register.md`, `docs/05-deployment-release-enablement/iip_deployment_plan.md` (§34 Open Decisions) |

## How to read this

This is a **tracking doc, not a decision-making doc.** None of these items is an engineering blocker. Per the source docs' own guidance, engineering proceeds on a documented default (typically a `Not Configured` state or a generic MVP configuration) and a human closes each item in parallel. The **Engineering default in place** column is the whole point of this tracker: it records what is already true in the build so no phase stalls waiting for an answer. When an item is answered, record the outcome in the Product Decision Log (if material) and update any affected PRD/FSD/roadmap/backlog/QA docs, then mark it closed here.

## Business Open Questions (OQ-001–010)

Source: `docs/01-strategy-requirements-roadmap/iip_assumptions_constraints_open_questions_register.md` §4.

| ID | Question | Category | Owner | Status | Engineering default in place |
|---|---|---|---|---|---|
| OQ-001 | Who is the confirmed first pilot insurer? | Commercial | Leadership | Open | Generic MVP config; no hardcoded insurer data (per D-001 mitigation). Build stays client-agnostic. |
| OQ-002 | Which deployment model will be used for the pilot? | Deployment | Architecture | Open | App Service hosting locked for MVP (ADR-0001); Deployment Model B (Enterprise Private Deployment) assumed until confirmed (see D1). |
| OQ-003 | Which motor claim categories and documents are in pilot scope? | Product/Claims | Claims SME | Open | Motor-claims + document-intelligence MVP scope per A-001; 13-state claim status model and generic document types built. |
| OQ-004 | Which integrations are mandatory for pilot launch? | Integration | Integration Lead | Open | All external integrations render `Not Configured` until credentials supplied (per D-004 mitigation); staged-integration roadmap. |
| OQ-005 | What are the final pilot success metrics? | Pilot | Product/Client Sponsor | Open | Reporting service surfaces generic operational metrics; specific success KPIs configurable at pilot launch. |
| OQ-006 | What are support hours and escalation contacts during pilot? | Support | Delivery Lead | Open | No engineering dependency; support model documented at pilot launch. |
| OQ-007 | Which regulatory/client security questionnaire must be completed? | Security | Security Lead | Open | Security controls built to AGENTS.md contract (server-side authz, tenant isolation, audit); questionnaire is pre-production sign-off, not a build input. |
| OQ-008 | What data retention period applies to pilot documents, claims, conversations, and audit logs? | Compliance | Compliance Lead | Open | Retention configurable; no hard-coded purge. Default = retain, tighten at architecture hardening. |
| OQ-009 | Which final user roles will be configured for pilot? | RBAC | Client Admin/Product | Open | Default RBAC roles in use, validated at UAT (per D-005 mitigation). |
| OQ-010 | Will pricing be pilot-only, subscription, usage-based, or private offer? | Commercial | Leadership | Open | No engineering dependency; commercial decision only (see D8 for cost ownership). |

## Deployment Open Decisions (D1–D10)

Source: `docs/05-deployment-release-enablement/iip_deployment_plan.md` §34.

| ID | Decision | Owner | Priority | Status | Engineering default in place |
|---|---|---|---|---|---|
| D1 | Confirm first pilot deployment model: Otic-hosted SaaS, private deployment, or Managed Application path | Leadership / Cloud | Critical | Open | Deployment Model B (Enterprise Private Deployment) assumed for first pilot; App Service hosting per ADR-0001. |
| D2 | Confirm Azure region and required model availability | Cloud / Legal | Critical | Open | Region is a Bicep parameter (`infra/parameters/`); South Africa North targeted at W8.1, not hard-coded. |
| D3 | Confirm WhatsApp provider: direct Meta, Twilio, Infobip, or other | Engineering / Product | Critical | Open | Channel providers behind a provider-agnostic port; render `Not Configured` until a provider is chosen and credentialed. |
| D4 | Confirm pilot customer and real insurer systems | Sales / Leadership | Critical | Open | Same as OQ-001: generic config, integrations `Not Configured`. |
| D5 | Confirm whether Microsoft Graph is required in pilot | Product / Engineering | High | Open | Graph/Outlook email path exists behind a provider setting; disabled/`Not Configured` unless enabled. |
| D6 | Confirm whether core policy system lookup is MVP or Phase 2 | Product / Customer | High | Open | Policy-admin write-back (Phase 5) requires an existing approval; read-only lookup optional and `Not Configured` by default (A-004). |
| D7 | Confirm data residency and cross-border processing position | Legal / Compliance | High | Open | Deployment plan configurable per client (C-006); region parameterized (D2). |
| D8 | Confirm cost ownership for Azure resources | Finance / Leadership | High | Open | No engineering dependency; Managed App deploys into customer subscription by design. |
| D9 | Confirm support access model in customer tenant | Security / Customer | High | Open | No engineering dependency for MVP build; support-access model is a Managed Application packaging input (Phase 9). |
| D10 | Confirm Managed Application timeline | Leadership / Cloud | Medium | Open | Marketplace/Partner Center track deliberately frozen until Phase 9 (W0.3); does not gate Phases 1–8. |

## Recorded decisions affecting scope (not open questions)

These are **confirmed** decisions (recorded in the Product Decision Log), noted here because they change the build's scope and any reader of this tracker should be aware of them. They are not awaiting an answer.

| Ref | Decision | Recorded in |
|---|---|---|
| DEC-011 | Adopt the Lovable-designed "Ambient Intelligence" visual theme, superseding the flat Light Intelligence Workspace palette (skin-only; chat-first architecture unchanged). | `iip_product_decision_log.md`; `iip_design_system_ux_behavior_specification.md` v1.2 |
| DEC-012 | Bring Channels, Customer 360, Communications, Documents, Support, Workspaces to full workflow/logic parity with the Lovable reference — real backend, not mock. | `iip_product_decision_log.md`; program plan Initiative LP |
| DEC-013 | Add AI Agent Registry (+ kill switch), Fraud Intelligence, Underwriting Intelligence, Billing/entitlements as full real features. | `iip_product_decision_log.md`; program plan Initiative LP |
| DEC-014 | Fraud & Underwriting use deterministic rule engines for v1 (not an LLM); revisit Azure OpenAI narration at Gate 3 when real providers activate. | `iip_product_decision_log.md` |

## Closure workflow

1. Human provides an answer for an item.
2. Update this tracker: set Status to Closed and record the decision + date.
3. If material, add the outcome to the Product Decision Log.
4. File any resulting engineering follow-up as a scoped work item in the program plan (e.g., a chosen WhatsApp provider becomes a Phase 5 follow-on) rather than editing this tracker's defaults in place.
5. Items still open remain on their documented defaults — this does not block pilot launch, per the source docs' own guidance.
