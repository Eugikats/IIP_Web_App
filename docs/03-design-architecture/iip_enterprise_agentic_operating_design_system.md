# Enterprise Agentic Operating Design System

### Generic design language and operating model for enterprise-grade agentic AI platforms

**Version:** 3.0
**Date:** August 2026
**Classification:** Internal design standard
**Scope:** Generic enterprise agentic systems across finance, procurement, insurance, banking, cybersecurity, compliance, government, NGO operations, and future institutional platforms.
**Status:** Revised master standard. Domain-specific systems should extend this document rather than fork it.

---

## Document Purpose

This design system defines the reusable visual, interaction, governance, and operating model for enterprise-grade agentic AI systems. It is intentionally generic. It is not specific to any one platform, industry, client, or product.

The system is designed for applications where institutional users work with high-stakes records, regulated data, approvals, financial values, sensitive documents, and AI agents that can retrieve, reason, draft, recommend, route, or execute workflow actions.

The core idea is simple:

> Enterprise agentic systems are not chatbots attached to dashboards. They are governed operating environments where humans, AI agents, data, tools, policies, and audit trails work together.

This document should be used as the master standard for:

- New enterprise AI products.
- Internal command centers.
- Case management platforms.
- Compliance and risk systems.
- Claims, onboarding, finance, procurement, cybersecurity, credit, banking, and operations platforms.
- Any system where AI agents support enterprise work.

Domain-specific platforms should create an extension layer, for example:

- Insurance Intelligence Extension.
- Procurement Intelligence Extension.
- Cybersecurity Operations Extension.
- Banking and Credit Intelligence Extension.
- Government Case Management Extension.

The generic system remains the foundation.

---

## Reference Standards and Research Basis

This revision is aligned with the following current public standards and design references:

| Source | Relevance to this design system |
|---|---|
| NIST AI Risk Management Framework | Risk governance, mapping, measuring, and managing AI risk across the lifecycle. |
| ISO/IEC 42001:2023 | AI management-system thinking: responsibilities, processes, risk, transparency, and continual improvement. |
| OWASP Top 10 for LLM Applications and OWASP Agentic AI guidance | Prompt injection, excessive agency, data leakage, insecure output handling, and agent/tool risk. |
| WCAG 2.2 | Accessibility requirements including focus visibility, target size, dragging alternatives, and accessible authentication. |
| Microsoft Human-AI Interaction Guidelines | Human-centered AI patterns for uncertainty, correction, user control, and system behavior over time. |
| Microsoft Responsible AI principles | Fairness, reliability and safety, privacy and security, inclusiveness, transparency, and accountability. |

References are listed at the end of this document.

---

## Document Set

This standard has been divided into focused companion documents so each has a single owner, reader, and change cadence. This master document retains the stable foundation (design philosophy, generic applicability, agentic principles) and the governance layer (domain extension model, versioning, readiness checklist, references). All product- and layer-specific detail lives in the companions below and was moved verbatim.

| Document | Owns | Path |
|---|---|---|
| **Foundation and governance** (this document) | Design philosophy, generic platform applicability, enterprise agentic principles, domain extension model, governance and versioning, readiness checklist, references | `iip_enterprise_agentic_operating_design_system.md` |
| **Visual Design System and Design Tokens** | Tokens, color, typography, layout, density, hierarchy, surfaces, components' visual grammar, tables, forms, navigation, data visualization, system states, dark mode, responsive/density, token delivery, visual acceptance | `iip_visual_design_system_and_tokens.md` |
| **Agentic AI Interface and Governance Patterns** | Autonomy levels, agent cards, copilot panel, NL query bar, inline AI, task queue, human review cockpit, action preview/recovery, governance center, agent registry, tool permission matrix, scope language, provenance/evidence, untrusted-content/prompt-injection, monitoring/eval, audit/decision records, security and responsible AI, print/export/evidence packaging | `iip_agentic_ai_interface_and_governance_patterns.md` |
| **IIP Platform UX Behavior Specification** | IIP actors, object model, Command Center, navigation, primary insurance workflows, forms rules, error prevention, screen/state inventory, UX writing, research plan, acceptance criteria, platform-wide UX audit and improved operating model | `iip_platform_ux_behavior_specification.md` |
| **Recent Conversations and Session Continuity Specification** | The full Recent Conversations feature: ontology, IA, flows, lifecycle, permissions, re-grounding, states, microcopy, testable requirements, research plan | `iip_recent_conversations_session_continuity_spec.md` |

# 01. Design Philosophy

## The Enterprise Principle

Enterprise design is not consumer design with a formal color palette. Enterprise users are professionals operating under operational, financial, legal, or institutional responsibility. They are not browsing. They are working.

The interface must help them make accurate decisions, understand risk, trace evidence, and complete work without ambiguity.

## Three Governing Axioms

### Axiom 1: Clarity before beauty

A design that looks impressive but hides evidence, status, source, risk, or next action is wrong. Enterprise users must be able to scan, compare, verify, and act.

### Axiom 2: Density is not clutter

Enterprise work is information-dense. The goal is not to remove information. The goal is to structure information so the right signal appears at the right moment.

### Axiom 3: Governance must be visible but not oppressive

Enterprise AI must be controlled. However, governance should not feel like constant friction. Boundaries should be quiet during normal work and explicit when risk increases.

## Platform Personality

| Attribute | Practical meaning |
|---|---|
| Authoritative | Screens must feel dependable and institutional. |
| Precise | Numbers, dates, states, sources, and permissions are explicit. |
| Calm | No decorative motion, visual noise, or unnecessary emphasis. |
| Evidence-led | AI outputs and human decisions show why they exist. |
| Governable | Roles, audit trails, approvals, and boundaries are first-class UI elements. |
| Operational | Every screen helps work move forward. |

## Golden Rules

- If a status requires inference, it is wrong.
- If an AI output has no visible source, it is wrong.
- If an agent can act without visible permission boundaries, it is wrong.
- If a user cannot tell whether a recommendation is AI-generated, it is wrong.
- If a record cannot be exported for audit, the screen is incomplete.
- If a table cannot be scanned quickly, the design has failed.

---

# 02. Generic Platform Applicability

This design system applies to enterprise platforms such as:

- Finance intelligence platforms.
- Procurement intelligence platforms.
- Insurance intelligence platforms.
- Banking and credit intelligence platforms.
- Cybersecurity operations platforms.
- Legal and compliance platforms.
- Government and NGO operations platforms.
- Customer operations platforms.
- Enterprise knowledge and workflow platforms.

It is deliberately **not** tied to one domain.

## Generic Object Language

Use generic terms in the master design system:

| Generic term | Domain-specific examples |
|---|---|
| Record | Claim, invoice, alert, case, ticket, application, contract |
| Party | Customer, vendor, supplier, patient, applicant, broker, citizen |
| Request | Requisition, claim request, support request, onboarding request |
| Evidence | Document, transaction, photo, policy, email, log, form |
| Decision | Approval, rejection, escalation, payout, closure, exception |
| Agent | Claims agent, finance agent, compliance agent, cyber triage agent |
| Workflow | Review, validation, onboarding, investigation, reconciliation |

## Domain Extension Rule

A domain-specific product may add terminology, workflows, colors, policies, templates, document types, and dashboards, but it must not break the master patterns for:

- Accessibility.
- Agent autonomy levels.
- Human review gates.
- Provenance and evidence.
- Audit trails.
- Tool permissions.
- Decision records.
- Export and print behavior.

---

# 03. Enterprise Agentic Principles

## What Makes a System Agentic

A system is agentic when AI agents can perform one or more of the following within governed boundaries:

- Observe current context.
- Retrieve records and evidence.
- Reason across data and policies.
- Draft structured outputs.
- Recommend decisions.
- Route tasks.
- Trigger workflows.
- Execute approved actions.
- Monitor conditions.
- Escalate exceptions.

A chatbot alone does not make a system agentic. Agentic behavior appears when AI becomes part of the operating layer.

## Five Agentic Design Principles

### Principle 1: The agent is always the assistant, never the authority

The interface must never make users feel that an institutional decision has already been made by AI. AI may recommend, but humans retain accountability for consequential actions unless policy explicitly allows automation.

### Principle 2: Every agent output must have visible provenance

Users must be able to answer:

- Where did this come from?
- What data was used?
- What was not available?
- Which model, prompt, policy, or tool produced it?
- When was the data last refreshed?

Provenance is not a tooltip. It is primary content.

### Principle 3: Transparency over magic

Consumer AI can feel magical. Enterprise AI must feel explainable. A good enterprise agent says what it checked, what it found, what it could not verify, and what it recommends.

### Principle 4: Context persistence must be visible

When an agent is invoked from a record, workflow, table, dashboard, or document, the interface must show the inherited context.

Example:

```text
Context: Record REC-2026-00914 / Customer A / Current workflow: Pending Review
Agent scope: Can summarize, compare, draft, and recommend. Cannot approve or send.
```

### Principle 5: Governed everywhere, restrictive only when needed

The user should be able to ask questions freely. Boundaries become visible when the user asks for restricted data, asks the agent to take a consequential action, or requests something outside their role.

---

# Extracted Specifications

The following specifications were part of this master document and have been moved verbatim to focused companions. Consult them directly; they are authoritative for their scope.

## Visual Design System and Design Tokens

The visual source of truth (tokens, color, typography, layout, density, hierarchy, surfaces, components' visual grammar, tables, forms, navigation, data visualization, system states, dark mode, responsive/density integration, token delivery, and visual acceptance criteria) is maintained in `iip_visual_design_system_and_tokens.md`.

## Agentic AI Interface and Governance Patterns

The reusable governed-AI surface library (autonomy levels, agent cards, copilot panel, natural-language query bar, inline AI entry points, agent task queue, human review cockpit, action preview and recovery, AI governance center, agent registry, tool permission matrix, tenant/role/scope language, provenance and evidence, untrusted-content and prompt-injection states, monitoring and evaluation, audit and decision records, security and responsible-AI guardrails, and print/export/evidence packaging) is maintained in `iip_agentic_ai_interface_and_governance_patterns.md`.

## IIP Platform UX Behavior Specification

The IIP domain behavior specification — formerly sections 03A and 03B — (actors, insurance object model, Command Center experience, navigation model, primary insurance workflows, forms and error rules, screen and state inventory, UX writing, research plan, acceptance criteria, and the platform-wide UX audit and improved operating model) is maintained in `iip_platform_ux_behavior_specification.md`.

## Recent Conversations and Session Continuity Specification

The Recent Conversations feature specification is maintained in `iip_recent_conversations_session_continuity_spec.md`.

# 38. Domain Extension Model

A domain extension adapts the generic system to a specific industry without changing the foundation.

## Extension Template

Each domain extension should define:

1. Domain terminology.
2. Core user roles.
3. Domain-specific workflows.
4. Record types.
5. Evidence/document taxonomy.
6. Policy/rule types.
7. AI agent types.
8. Risk levels.
9. Human review rules.
10. Dashboards and KPIs.
11. Export/audit requirements.
12. Regulatory constraints.

## Example Extension Mapping

| Generic element | Domain extension should define |
|---|---|
| Record | Domain record names and fields. |
| Party | Customer/vendor/member/citizen definitions. |
| Evidence | Accepted evidence types. |
| Decision | Domain decision types. |
| Agent | Domain agent roles. |
| Workflow | Domain lifecycle. |
| Risk | Domain-specific risk model. |
| Export | Required report/evidence formats. |

## Rule

The extension may add. It should not weaken:

- Accessibility.
- Audit.
- Provenance.
- Autonomy display.
- Human review gates.
- Tool permissions.
- Security states.
- Governance center requirements.

---

# 39. Governance and Versioning

## Versioning Rules

| Change type | Version impact |
|---|---|
| Token value change | Minor version |
| New component | Minor version |
| Component behavior change | Minor or major depending on impact |
| Removed token/component | Major version |
| Accessibility requirement change | Minor or major depending on impact |
| Agent autonomy model change | Major version |
| Governance requirement change | Major version if it changes controls |

## Change Request Template

```text
Change title:
Requested by:
Reason:
Affected components:
Affected tokens:
Before:
After:
Risk:
Migration notes:
Reviewers:
Decision:
```

## Review Cadence

- Minor review: monthly during active product build.
- Major review: quarterly.
- Standards review: twice per year.
- Security/governance review: after any major agent capability change.

---

# 40. Readiness Checklist

Use this before declaring an enterprise agentic design ready for implementation.

## Visual Foundation

- [ ] One primitive-semantic-component token hierarchy is authoritative in design and code.
- [ ] Color, typography, spacing, sizing, shape, elevation, motion, and layering tokens are implemented without hard-coded component values.
- [ ] Status, risk, priority, approval, confidence, evidence, provider, and AI-provenance semantics remain visually distinct and never rely on color alone.
- [ ] Operational surfaces are opaque and border-led; glass, blur, glow, hover lift, decorative ambient backgrounds, and gradient operational controls are absent.
- [ ] Numeric tables use tabular numerals; monospace is limited to identifiers and technical values.
- [ ] Light and dark semantic mappings are complete and contrast-tested.
- [ ] Compact, standard, and comfortable density modes preserve control sizes, hierarchy, and accessibility.
- [ ] Component usage matrix and card-versus-flat-section decisions are applied consistently.
- [ ] Storybook covers required component states, densities, themes, roles, and responsive modes.
- [ ] Visual-regression baselines cover shell, Command Center, queues, record workspaces, forms, overlays, charts, AI/evidence patterns, and all critical states.

## Core UX

- [ ] Product shell defined.
- [ ] Navigation patterns defined.
- [ ] Data table component defined.
- [ ] Forms and validation defined.
- [ ] Feedback and loading states defined.
- [ ] Responsive behavior defined.
- [ ] Print/export behavior defined.
- [ ] IIP Command Center chat, evidence rail, and action proposal model defined.
- [ ] IIP insurance object model defined for claims, policies, documents, evidence, tasks, approvals, agent runs, and audit events.
- [ ] Primary insurance workflows defined for FNOL, document review, underwriting support, and customer communications.
- [ ] Screen and state inventory completed for all Release 1 IIP surfaces.
- [ ] UX writing rules defined for evidence, restrictions, fraud signals, missing providers, low confidence, approvals, and write-back.
- [ ] Recent Conversations supports authorized find, reopen, re-ground, and safe continuation.
- [ ] Conversation history retention, archive, deletion, search, export, and access policies approved.
- [ ] Historical responses and stale action proposals are visibly separated from current evidence and current actions.
- [ ] Recent Conversations keyboard, screen-reader, zoom/reflow, narrow-layout, interruption, and low-bandwidth behavior validated.
- [ ] Universal work-item model connects chat, queues, records, approvals, background work, and audit.
- [ ] Role-based My Work and Team Work entry experiences defined with honest empty states.
- [ ] Cross-surface handoff and interruption/resume behavior defined for every critical workflow.
- [ ] Business, work, human-review, evidence, provider, and AI states are visibly distinct.
- [ ] Customer communication preflight covers recipient, channel, policy basis, evidence, approval, provider, and delivery result.
- [ ] Low-bandwidth loading, draft preservation, retry, idempotency, and no-false-success behavior verified.

## Agentic UX

- [ ] Autonomy levels defined.
- [ ] AI output labels defined.
- [ ] Copilot panel defined.
- [ ] Query bar defined.
- [ ] Inline AI entry points defined.
- [ ] Agent task queue defined.
- [ ] Human review cockpit defined.
- [ ] Action preview defined.
- [ ] Undo/recovery rules defined.

## Governance and Safety

- [ ] AI Governance Center defined.
- [ ] Agent Registry defined.
- [ ] Tool Permission Matrix defined.
- [ ] Tenant/role/data scope language defined.
- [ ] Provenance/evidence model defined.
- [ ] Untrusted content states defined.
- [ ] Prompt injection states defined.
- [ ] Monitoring/evaluation dashboards defined.
- [ ] Audit and decision record model defined.
- [ ] Incident log defined.

## Accessibility

- [ ] WCAG 2.2 AA target documented.
- [ ] Keyboard patterns defined.
- [ ] Focus states visible.
- [ ] Non-color status indicators defined.
- [ ] Touch targets meet minimum size.
- [ ] Drag-and-drop alternatives defined.
- [ ] Screen-reader labeling for AI outputs defined.

---

# 41. References

The following public sources informed this revision:

1. NIST AI Risk Management Framework. https://www.nist.gov/itl/ai-risk-management-framework
2. NIST AI RMF Core. https://airc.nist.gov/airmf-resources/airmf/5-sec-core/
3. ISO/IEC 42001:2023 AI management systems. https://www.iso.org/standard/42001
4. OWASP Top 10 for Large Language Model Applications. https://owasp.org/www-project-top-10-for-large-language-model-applications/
5. OWASP GenAI Security Project / LLM Top 10. https://genai.owasp.org/llm-top-10/
6. W3C Web Content Accessibility Guidelines 2.2. https://www.w3.org/TR/WCAG22/
7. W3C What's New in WCAG 2.2. https://www.w3.org/WAI/standards-guidelines/wcag/new-in-22/
8. Microsoft Guidelines for Human-AI Interaction. https://www.microsoft.com/en-us/research/project/guidelines-for-human-ai-interaction/
9. Microsoft HAX Toolkit AI Guidelines. https://www.microsoft.com/en-us/haxtoolkit/ai-guidelines/
10. Microsoft Responsible AI principles. https://www.microsoft.com/en-gb/ai/responsible-ai
11. Bevaya competitive benchmark for Uganda Insurance AI Platform. `docs/research/bevaya-comparative-analysis.md`
12. Salesforce First Notice of Loss documentation. https://help.salesforce.com/s/articleView?id=ind.insurance_claim_fnol.htm&language=en_US&type=5
13. Oracle Siebel Insurance claims management overview. https://docs.oracle.com/cd/G30557_01/books/InsuranceSIA/c-About-Managing-Claims-ahc1016454.html
14. ServiceNow Personal Lines Claims workflows. https://www.servicenow.com/docs/r/xanadu/financial-services-operations/insurance-claims/personal-lines-claims-workflows.html
15. ServiceNow Individual Life Claims workflows. https://www.servicenow.com/docs/r/financial-services-operations/insurance-claims/individual-life-claims-workflows.html
16. Buckhill claims lifecycle overview. https://cdn.buckhill.co.uk/help/article/claims-lifecycle
17. Layerup claims lifecycle and agentic workflow description. https://www.uselayerup.com/solutions/claims
18. Moxo claims processing workflow guide. https://www.moxo.com/blog/claims-processing-guide
19. Regure claims workflow templates and claims automation pages. https://www.getregure.com/resources/claims-workflow-templates/ and https://www.getregure.com/platform/claims-automation/
20. Microsoft Support, How Microsoft 365 Copilot Chat history works. https://support.microsoft.com/en-US/Microsoft-365-Copilot/how-microsoft-365-copilot-chat-history-works
21. Microsoft Learn, Microsoft 365 Copilot architecture and user interaction history. https://learn.microsoft.com/en-us/microsoft-365/copilot/microsoft-365-copilot-architecture
22. Microsoft Support, Delete your Microsoft 365 Copilot activity history. https://support.microsoft.com/en-US/Privacy/delete-your-microsoft-365-copilot-activity-history
23. OpenAI Help Center, How to delete and archive chats in ChatGPT. https://help.openai.com/en/articles/8809935-how-to-delete-and-archive-chats-in-chatgpt
24. Insurance Regulatory Authority of Uganda, A Guide to Insurance Claims. https://ira.go.ug/download/a-guide-to-insurance-claims/
25. Insurance Regulatory Authority of Uganda, Insurance Claims Guidelines. https://ira.go.ug/cp/uploads/claimesguidelines.pdf
26. ServiceNow, Insurance claims landing pages and workspaces. https://www.servicenow.com/docs/r/financial-services-operations/insurance-claims/insurance-claims-flow-workspace.html
27. ServiceNow, Adjuster activities. https://www.servicenow.com/docs/r/financial-services-operations/insurance-claims/adjuster-tasks-workspace-persona.html
28. ServiceNow, Case routing and assignment. https://www.servicenow.com/docs/r/customer-service-management/case-routing-and-asign.html
29. Design Tokens Community Group, Design Tokens specification. https://www.designtokens.org/
30. Design Tokens Community Group, Format Module 2025.10. https://www.designtokens.org/tr/2025.10/format/
31. Microsoft Fluent 2, Design tokens. https://fluent2.microsoft.design/design-tokens

---

## Version Notes

### v3.0 - August 2026

This major revision replaces the conflicting flat and glass-heavy Ambient Intelligence operating styles with one implementation-ready **Operational Intelligence** visual system. It adds a visual audit, governing direction, exact primitive/semantic/component tokens, light and provisional dark modes, density and layout contracts, enterprise containment rules, shell and Command Center specifications, component usage matrix, AI provenance patterns, accessibility and responsive requirements, design-to-development mappings, migration guidance, acceptance criteria, changelog, and unresolved stakeholder decisions. It consolidates the former visual sections 4-16 and removes obsolete package recommendations and protected-runtime Demo/Sandbox guidance. Ambient blue-violet remains only as a restrained brand and AI-provenance signature, not an operational surface effect.

### v2.2 - August 2026

This revision adds the platform-wide IIP UX expert audit and improved insurance operating model. It introduces a universal work spine, role-based My Work entry, adaptive Command Center, grouped navigation, shared record-workspace anatomy, queue and handoff behavior, status hierarchy, communication preflight, interruption recovery, low-bandwidth behavior, platform requirements, remediation priorities, and validation gates. It also recommends extracting the IIP-specific behavioral specification from the generic master design system in a later governed documentation change.

### v2.1 - August 2026

This revision adds the IIP Recent Conversations and Session Continuity experience, including conversation ontology, search and navigation, reopen-and-re-ground flow, lifecycle controls, stale-context safeguards, permissions and privacy rules, error recovery, accessibility, microcopy, testable requirements, research tasks, and explicit implementation dependencies.

### v2.0 - July 2026

This version revises the previous enterprise intelligence design system into a generic **Enterprise Agentic Operating Design System**. The revision keeps the enterprise visual foundation while strengthening the operating model for production-grade agentic AI.

Major additions:

- Generic platform language.
- Domain extension model.
- AI Governance Center.
- Agent Registry.
- Tool Permission Matrix.
- Agent Action Preview.
- Human Review Cockpit.
- Prompt injection and untrusted content states.
- Monitoring and evaluation dashboards.
- Tenant, role, and data scope patterns.
- Stronger responsible AI and security guardrails.

This document is the master generic system. Industry-specific products should build extension documents on top of it.

### v3.1 - August 2026

- Divided the single master document into focused companion documents (Visual Design System and Design Tokens; Agentic AI Interface and Governance Patterns; IIP Platform UX Behavior Specification; Recent Conversations and Session Continuity Specification). Content was moved verbatim; this master retains the foundation and governance layer and now serves as the index. No specification content was changed by the division.
