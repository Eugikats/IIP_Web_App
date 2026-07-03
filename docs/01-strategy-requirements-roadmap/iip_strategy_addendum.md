# IIP Strategy Addendum / Product Strategy Update

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Strategy Addendum / Product Strategy Update |
| Version | 1.0 |
| Status | Draft |
| Audience | Executive leadership, product owners, engineering leads, pilot sponsors, solution architects |
| Date | 2026-06-30 |


## 1. Purpose

This addendum updates and sharpens the agreed strategy for the Insurance Intelligence Platform (IIP). It should be read as a control document for naming, MVP direction, pilot readiness, module sequencing, deployment direction, and human-reviewed AI behavior.


This document uses the existing IIP project context and prior documentation pack: Master Product Context, PRD, FSD, Technical Architecture Document, System Integration Strategy, UI/UX Handoff, API Specification, Data Model, Security & Compliance Document, QA/Test Plan, Deployment Plan, User/Admin Guide, and Implementation Roadmap.

Confirmed project baseline:
- Official product name: Insurance Intelligence Platform (IIP).
- IIP is an Azure-native, human-reviewed insurance operations platform, not a replacement for core insurance systems.
- MVP focus: motor claims and document intelligence for a mid-to-large insurer pilot.
- Customer engagement is WhatsApp-first and channel-led; customers do not log into the internal staff application.
- Internal modules include Command Center, Channels, Customers, Sales & Leads, Claims, Documents, Knowledge Base, Approvals, Compliance & Audit, Reports & Analytics, Integrations, Admin Settings, and Support & Health.
- The internal UI is a role-aware staff workspace with persistent navigation, a top bar, global search, operational states, and a contextual Copilot panel.
- The platform uses Next.js/React/TypeScript/Tailwind on the frontend and .NET 8/Azure services on the backend.
- AI assists with extraction, summarization, classification, routing, drafting, risk signals, and evidence preparation; humans make regulated or consequential decisions.


## 2. Confirmed Strategic Position

IIP is an insurance operations intelligence platform. It sits above existing insurance systems and helps staff coordinate customers, claims, documents, workflows, approvals, and AI-assisted analysis. It is not positioned as a core policy administration system, claims payment engine, accounting system, or autonomous claims decision engine.

The strategic emphasis is practical operational intelligence: faster intake, better document completeness, stronger auditability, clearer customer updates, earlier risk signals, and better staff productivity.


## 3. Naming Decision

| Decision Area | Confirmed Position | Implementation Implication |
| --- | --- | --- |
| Official product name | Insurance Intelligence Platform (IIP) | Use IIP in all documents, UI labels, proposals, repository references, navigation labels, and client-facing materials. |
| Superseded naming | ACIP and Agentic Insurance Intelligence Platform are not the official product names | Older references should be treated as legacy context only and should not be introduced into new product surfaces. |
| Brand relationship | IIP may be presented as an Otic solution where commercially appropriate | Do not hardcode Otic as the product name inside internal product architecture unless it is a commercial or attribution field. |

## 4. MVP Direction

| MVP Dimension | Confirmed Direction | Notes |
| --- | --- | --- |
| Primary MVP | Motor claims and document intelligence | Focus first on motor claim intake, document collection, extraction, completeness checks, staff review, and audit. |
| Pilot buyer | Mid-to-large insurer | The first pilot should be sized to produce meaningful operational evidence without overloading integrations. |
| Initial claim type | Motor claims | Open Question: confirm whether the pilot includes comprehensive, third-party, own-damage, theft, windscreen, or all motor claim categories. |
| Primary channel | WhatsApp-first, with email and web-ready expansion | External customers interact through channels; internal users work inside the staff app. |
| Human decision model | Human-in-the-loop | AI prepares, flags, summarizes, and drafts; humans approve, reject, settle, bind, price, or submit regulated actions. |

## 5. Module Direction

| Module | MVP Role | Phase Notes |
| --- | --- | --- |
| Command Center | Operational home for claims, documents, SLA risk, AI activity, and exceptions | Must provide clear entry points into Claims, Documents, Channels, Approvals, and Reports. |
| Claims | Core MVP workspace | Includes claim intake, status, assignment, review, SLA indicators, drawer/detail view, and export pack. |
| Documents | Core MVP workspace | Includes document upload, classification, extraction, confidence, missing item detection, and human review. |
| Channels | MVP-light but important | Must support WhatsApp/email conversation visibility and intake context. |
| Customers | MVP-light | Used to view customer profile, policy references, claim history, and channel interactions. |
| Sales & Leads | Phase 1-lite or Phase 2 depending on pilot | Useful for customer acquisition but should not distract from claims MVP unless required by pilot. |
| Approvals | MVP workflow gate | Needed where claim review, document exception, or escalation requires supervisor action. |
| Compliance & Audit | MVP non-negotiable | Every AI output, document action, review, override, and export must be traceable. |
| Reports & Analytics | MVP operational metrics | Focus on claims/document performance, not advanced BI at first. |
| Integrations | MVP controlled scope | WhatsApp, email/Graph, Blob, Azure AI, SQL, and optional read-only policy lookup are highest priority. |

## 6. Deployment Strategy Update

| Deployment Path | Strategic Position | Open Questions |
| --- | --- | --- |
| Azure-first | IIP should remain Microsoft/Azure-aligned for enterprise trust, security, and insurer IT acceptance. | Confirm final Azure region and data residency expectations with pilot client. |
| Managed Application/private deployment | Preferred for enterprise insurer pilots when customer-tenant deployment is required. | Confirm if pilot client wants customer-tenant deployment or Otic-hosted pilot. |
| Otic-hosted SaaS | Useful for fast demos, smaller clients, and controlled pilots. | Define commercial and support boundaries before external production use. |
| Hybrid/resilient channel buffer | Important for channel resilience, especially WhatsApp and intermittent connectivity. | Confirm whether this is MVP, Phase 1 hardening, or Phase 2. |

## 7. Human-Reviewed AI Policy

| AI May Do | AI Must Not Do |
| --- | --- |
| Summarize claims, documents, and conversations | Approve or reject claims |
| Extract fields from documents and flag missing information | Bind policies or set final premium |
| Draft customer updates for human review | Accuse a customer, provider, broker, or staff member of fraud |
| Identify risk signals and inconsistencies | Release payments or promise settlement |
| Prepare evidence packs and audit notes | Make final underwriting, claims, legal, or regulatory decisions |

## 8. Strategic Priorities

| Priority | Strategic Focus | Success Indicator |
| --- | --- | --- |
| P1 | Claims and document intelligence MVP works end-to-end | A motor claim can move from channel intake to document review to human decision support with audit trail. |
| P2 | No fake production workflows | Staging and production must show real configured state, empty states, or Not configured states. |
| P3 | Integration realism | Every integration has a clear configured/not configured/error state and a test path. |
| P4 | Pilot evidence | Pilot produces measurable improvements in completeness, turnaround, staff workload, and visibility. |
| P5 | Responsible AI trust | Every AI output is source-grounded, auditable, reviewable, and constrained. |

## 9. Open Questions

| ID | Open Question | Owner / Decision Needed |
| --- | --- | --- |
| OQ-STR-001 | Which insurer is the confirmed first pilot client? | Commercial/Product |
| OQ-STR-002 | Which Azure deployment path will the pilot use: customer tenant, Otic-hosted SaaS, or hybrid? | Architecture/Client IT |
| OQ-STR-003 | Which motor claim categories are included in the pilot? | Claims SME/Client |
| OQ-STR-004 | Which external core systems must be connected during MVP? | Integration Lead/Client IT |
| OQ-STR-005 | What commercial packaging will be used for pilot and post-pilot expansion? | Leadership/Commercial |

## 10. Strategic Operating Model

| Operating Principle | Meaning for IIP | Delivery Implication |
| --- | --- | --- |
| Insurance-first, not generic AI | IIP should understand claims, policy, customer, document, approval, SLA, and audit concepts. | Every module should use insurance language and workflow logic, not generic ticketing language. |
| Channel-to-workflow continuity | External customer interactions should become internal staff work items. | WhatsApp, email, and web intake must preserve context, identifiers, customer identity, claim mapping, and audit events. |
| Source-grounded assistance | AI should show what evidence informed its answer. | Use document references, conversation references, policy references, and confidence indicators. |
| Operational visibility by default | Managers need to see backlog, risk, bottlenecks, and exceptions. | Command Center should not be decorative; it should drive action. |
| Configuration before customization | Insurers differ, but many changes should be configurable. | Document checklists, SLAs, roles, templates, and channel rules should become configuration where practical. |

## 11. Module Sequencing Guidance

| Sequence | Module / Capability | Why This Order |
| --- | --- | --- |
| 1 | Foundation shell, auth, tenant context, provider health | All other work depends on safe access and truthful system state. |
| 2 | Claims and documents | This is the MVP proof of value. |
| 3 | AI Copilot with audit | AI value must be introduced after source data and workflow context exist. |
| 4 | Approvals, audit, compliance | Needed before pilot use and before client trust can be earned. |
| 5 | Channels and customer updates | Required for end-to-end service flow. |
| 6 | Reports and pilot metrics | Needed to prove outcomes during the pilot. |
| 7 | Sales, leads, early warning, fraud signal add-ons | Important future modules but should not distract from MVP execution. |

## 12. Strategy Review Checklist

| Question | Expected Answer |
| --- | --- |
| Does the roadmap still prioritize motor claims and document intelligence first? | Yes. |
| Does every AI feature preserve human review? | Yes. |
| Does the product still avoid replacing core systems? | Yes. |
| Are integrations phased and realistic? | Yes. |
| Are customers still treated as channel users, not internal staff app users? | Yes. |
| Are audit and security treated as core product features? | Yes. |
