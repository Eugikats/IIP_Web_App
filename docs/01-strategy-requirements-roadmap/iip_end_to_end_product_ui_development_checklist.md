# IIP End-to-End Product and UI Development Checklist

**Status:** Working audit and tracking checklist
**Audit date:** 4 August 2026
**Scope:** The 23-stage workflow in the user-supplied *Complete Workflow for Designing and Building a User Interface*, expanded to cover IIP product, business, market, UX, engineering, release, and continuous-improvement evidence.
**Authority:** This checklist tracks completeness. It does not replace `AGENTS.md`, `IIP_IMPLEMENTATION_GUARDRAILS.md`, or `docs/08-program-plan/IIP-CONSOLIDATED-ROADMAP.md` for product rules or execution order.

## How to use this tracker

- Check an item only when its stated evidence exists and has been reviewed.
- A document proves specification work, not user validation, runtime operation, or release readiness.
- Add a link or dated evidence note beside every newly completed item.
- Reopen checked items when research, testing, regulation, or implementation invalidates an assumption.
- Use `Complete` only when every required checkbox is checked. Use `Blocked` only when an external decision, access, participant, provider, or environment prevents closure.

### Status legend

| Status | Meaning |
|---|---|
| `Complete` | Required deliverables exist, are approved, and have applicable validation evidence. |
| `Partial` | Useful evidence exists, but approval, validation, implementation, or coverage is incomplete. |
| `Gap` | Material required deliverable or proof was not found. |
| `Blocked` | Work cannot close without an external decision, access, participant, provider, or environment. |

## Audit summary

| # | Stage | Current status | Main reason not closed |
|---:|---|---|---|
| 1 | Product definition | Partial | Strong working baseline; stakeholder approval and validated outcome metrics not evidenced. |
| 2 | User, business, and market research | Partial | Desk research and business case exist; primary Uganda practitioner/buyer research and market validation absent. |
| 3 | Requirements and scope | Partial | PRD, scope, acceptance, risks, and traceability exist; open decisions and research-derived changes remain. |
| 4 | Information architecture | Partial | Navigation, screen/state inventory, and platform model exist; validation and final cross-document reconciliation remain. |
| 5 | User journeys and task flows | Partial | Detailed flows exist; representative-user validation and some exception variants remain. |
| 6 | Interaction and behavior design | Partial | Extensive behavior/state rules exist; participant validation and implementation parity remain. |
| 7 | Low-fidelity wireframes | Partial | Canonical 34-screen/seven-state inventory plus a development-only critical-flow prototype exist; direction selection and screen-specific review remain. |
| 8 | Early usability review | Gap | Expert audits exist; no documented participant-based wireframe usability review. |
| 9 | Visual direction | Partial | Ambient Intelligence is authoritative, but newer untracked Operational Intelligence material conflicts and needs a decision. |
| 10 | Design foundations and tokens | Partial | Detailed token specs and implementation exist; authority conflict, accessibility conformance, and token parity need closure. |
| 11 | Components and patterns | Partial | Broad specifications and implementation exist; inventory, variants, Storybook/usage proof, and parity remain incomplete. |
| 12 | High-fidelity interface design | Partial | Canonical `/app/*` surface exists; migration audit records incomplete real-data reconciliation and prototype residue. |
| 13 | Interactive prototyping | Partial | HTML prototype and working portal exist; canonical task-test prototype coverage and annotations are incomplete. |
| 14 | Design validation | Gap | Expert/visual review exists; no representative participant validation or complete accessibility conformance result. |
| 15 | Frontend planning | Partial | Architecture, handoff, implementation guide, and migration backlog exist; reconcile with current design authority. |
| 16 | Frontend implementation | Partial | Real Next.js portal and tests exist; migration and deployed-profile gaps remain. |
| 17 | Backend integration | Partial | Real backend and many `/api/v1` integrations exist; provider/live end-to-end proof remains incomplete. |
| 18 | Functional testing | Partial | Large automated suite and harnesses exist; authenticated staging UAT and provider-path proof remain open. |
| 19 | Accessibility and responsive testing | Partial | Requirements and some automation exist; manual keyboard, screen-reader, zoom/reflow, device, and low-bandwidth proof absent. |
| 20 | Usability testing | Gap | Research plan exists; no completed representative Uganda staff sessions supplied. |
| 21 | Quality assurance | Partial | QA plans, regression suites, CI, and go/no-go evidence exist; human UAT, tail latency, live providers, and final sign-off remain open. |
| 22 | Release | Blocked | Current evidence says production, Managed Application, and Partner Center readiness are all `NO`. |
| 23 | Monitoring and refinement | Partial | Architecture, support, feedback, and post-launch templates exist; production telemetry and operating cadence are unproven. |

## Recommended sequence from the current state

| Horizon | Outcome | Stages |
|---|---|---|
| Now | Close discovery and design-proof gaps before more visual expansion | 1-8, 14, 20 |
| Next | Reconcile design authority, finish design-to-code parity, and complete end-to-end verification | 9-19, 21 |
| Later | Release only after protected-runtime evidence passes; then operate the improvement loop | 22-23 |

Dependencies:

```text
Validated problem, users, market, and outcomes
-> approved scope and information architecture
-> validated journeys, behavior, and wireframes
-> reconciled visual system and components
-> high-fidelity design and prototype validation
-> frontend/backend implementation
-> functional, accessibility, responsive, usability, and QA gates
-> release
-> monitored refinement
```

### Core evidence index

- [Master product context](./iip_master_product_context.md)
- [Product requirements document](./iip_product_requirements_document.md)
- [Requirements traceability matrix](./iip_requirements_traceability_matrix.md)
- [Authoritative consolidated roadmap](../08-program-plan/IIP-CONSOLIDATED-ROADMAP.md)
- [Current-state capability map](../research/iip-current-state-capability-map.md)
- [Pending-work register](../research/iip-pending-work-register.md)
- [Uganda Release 1 desk research](../research/uganda-release-1-claims-workflow-desk-research.md)
- [Platform UX behavior specification](../03-design-architecture/iip_platform_ux_behavior_specification.md)
- [End-to-end user-flow architecture](../03-design-architecture/iip_end_to_end_user_flow_architecture.md)
- [Current authoritative design-system specification](../03-design-architecture/iip_design_system_ux_behavior_specification.md)
- [Lovable parity migration audit](../06-decisions/lovable-parity-migration-audit.md)
- [QA test plan](../04-testing-security-quality/iip_qa_test_plan.md)
- [Release Foundation go/no-go evidence](../07-qa/rf10-release-foundation-go-no-go-evidence.md)

## Skill recommendation policy

`Installed` means available on this system or in this repository. `Add` means a skills.sh candidate worth installing when that stage begins. Do not install duplicate skills merely because they are popular.

### Addable shortlist

| ID | Skill | Why add | Quality check on 4 Aug 2026 | Install command |
|---|---|---|---|---|
| A1 | [`discovery-interview-prep`](https://skills.sh/deanpeters/product-manager-skills/discovery-interview-prep) | Adds a focused discovery-interview preparation workflow to the already-strong local research set. | 1.9K installs; source repository 6.2K stars. | `npx.cmd skills add deanpeters/product-manager-skills@discovery-interview-prep -g -y` |
| A2 | [`usability-testing`](https://skills.sh/refoundai/lenny-skills/usability-testing) | Adds a compact participant-test workflow spanning fidelity choice, recruitment, scenarios, observation, and iteration. | 1.8K installs; 1.2K stars; skills.sh audit badges pass. | `npx.cmd skills add refoundai/lenny-skills@usability-testing -g -y` |
| A3 | [`frontend-design`](https://skills.sh/anthropics/skills/frontend-design) | Adds high-quality visual-direction-to-working-interface guidance for distinctive production UI. Use only inside approved IIP visual authority. | 738K installs; 166K stars; skills.sh audit badges pass. | `npx.cmd skills add anthropics/skills@frontend-design -g -y` |
| A4 | [`azure-reliability`](https://skills.sh/microsoft/azure-skills/azure-reliability) | Adds Microsoft-authored reliability review for Azure architecture, deployment, and operations. | About 180K installs; official Microsoft source; repository 1.3K stars. | `npx.cmd skills add microsoft/azure-skills@azure-reliability -g -y` |

Not recommended now: `web-design-guidelines` is popular, but the skills.sh page showed a Snyk warning during this audit; local accessibility, critique, responsive, and design-consistency skills already cover the immediate need.

---

## 1. Product definition

**Current status:** Partial
**Close when:** Product purpose, target market/users, business outcomes, principles, scope, and measurable success have current stakeholder approval and traceability.

**IIP evidence:** `iip_master_product_context.md`, `iip_product_requirements_document.md`, `iip_strategy_addendum.md`, product/decision registers, one-page brief. The master context and PRD are explicitly working baselines.

**Best skills:** Installed — `product-problem-framing`, `product-vision-experience-principles`, `product-discovery-strategy`, `blue-ocean-product-strategy`, `domain-modeling`, repository `roadmap-planning`. Add — none; current coverage is strong.

- [x] Product purpose and problem statement documented.
- [x] Formal product identity and human-supervised insurance boundary documented.
- [x] Initial target institutions, roles, and Uganda-first/Africa-configurable direction documented.
- [x] MVP wedge and high-level feature/module list documented.
- [ ] Product principles formally approved by product, insurance operations, security, and engineering owners.
- [ ] Business and user outcome metrics have baselines, targets, owners, and measurement methods.
- [ ] Value proposition validated with representative buyers and users.
- [ ] Product definition updated from validated research rather than assumptions alone.

## 2. User, business, and market research

**Current status:** Partial
**Close when:** Secondary evidence and primary research jointly establish the users, workflows, buyer problem, competitive position, market attractiveness, constraints, and willingness to adopt/pay.

**IIP evidence:** Uganda Release 1 desk research, generic conversation-continuity research, Bevaya benchmark/comparative analysis, business case/ROI, pricing and packaging, strategic market context. `iip_platform_ux_behavior_specification.md` explicitly states no participant sessions or production analytics were supplied.

**Best skills:** Installed — `research`, `ux-research-playbook`, `ux-research-planner`, `customer-conversations`, `customer-interview-designer`, `contextual-inquiry-observer`, `survey-quant-research-designer`, `opportunity-needs-analyzer`, `research-synthesis`, `blue-ocean-product-strategy`. Add — A1 `discovery-interview-prep`.

- [x] Secondary Uganda insurance/regulatory desk research exists for Release 1.
- [x] Competitive benchmark and comparative analysis exist.
- [x] Business case, ROI model, pricing, and packaging hypotheses exist.
- [x] Research evidence limits and practitioner-validation caveats are stated.
- [ ] Define a research repository with source, date, geography, participant, method, confidence, and decision affected.
- [ ] Interview claims officers, supervisors/approvers, assessors, customer-service/complaints staff, brokers, IT/security, and insurer executives.
- [ ] Conduct contextual inquiry on real claim intake, document review, approval, communication, and exception work.
- [ ] Validate personas, role labels, device/network context, language needs, accessibility needs, and handoffs.
- [ ] Quantify market size and reachable initial segment using cited, current sources.
- [ ] Validate buyer, procurement, data-residency, deployment, integration, and change-management constraints.
- [ ] Validate willingness to pilot, adoption conditions, pricing, ROI assumptions, and purchase authority.
- [ ] Compare IIP against direct competitors, substitutes, manual workflows, core-system modules, and build-in-house alternatives.
- [ ] Convert findings into approved product/requirements/roadmap changes with provenance.

## 3. Requirements and scope

**Current status:** Partial
**Close when:** Functional, business-rule, data, security, privacy, accessibility, performance, operational, and release requirements are testable, prioritized, traced, and approved.

**IIP evidence:** PRD, MVP scope-control register, acceptance-criteria register, requirements traceability matrix, risk/open-question registers, API specification, RBAC matrix, production guardrails.

**Best skills:** Installed — `ux-requirements-architect`, `product-prioritization-scope-designer`, `to-spec`, `to-tickets`, `domain-modeling`, `responsible-algorithm-engineering`, `architecture-data-systems`. Add — none.

- [x] Functional requirements and high-level workflows documented.
- [x] Human-decision, audit, grounding, tenant/workspace, and zero-simulation rules documented.
- [x] MVP in-scope and out-of-scope boundaries documented.
- [x] Acceptance criteria and traceability artifacts exist.
- [ ] Reconcile requirements affected by August 2026 research and new untracked design artifacts.
- [ ] Resolve or explicitly defer every launch-critical open question and decision.
- [ ] Add measurable performance, resilience, low-bandwidth, accessibility, and usability acceptance thresholds where missing.
- [ ] Confirm multilingual and customer-document requirements; do not treat English-only as final strategy.
- [ ] Trace every Release 1 requirement to design, API/data contract, implementation, test, and release evidence.
- [ ] Obtain product, insurance operations, security/privacy, architecture, and delivery approval.

## 4. Information architecture

**Current status:** Partial
**Close when:** Modules, records, work items, navigation, labels, search, role visibility, and screen/state inventory are internally consistent and validated by users.

**IIP evidence:** Platform UX behavior specification, end-to-end user-flow architecture, frontend architecture/handoff, screen/state inventory, `/app/*` route tree, role/navigation configuration, and an IA-specific approval/research pack. No named approval or participant validation is recorded.

**Best skills:** Installed — `information-architecture-designer`, `navigation-system-designer`, `screen-state-inventory`, `domain-modeling`, `ux-strategy-planes`, `user-flow-architect`. Add — none.

- [x] Core object model, modules, and operational work modes documented.
- [x] Canonical `/app/*` route tree and shell documented.
- [x] Screen and state inventory exists at specification level.
- [x] Role-aware navigation principles exist.
- [x] Reconcile navigation, labels, and screen ownership across current authority documents and code; canonical IA v1.0 created, approval pending.
- [x] Produce one canonical sitemap/module tree linked to real routes, aliases, permissions, owners, scope posture, and implementation evidence.
- [ ] Validate task grouping and terminology with tree testing/card sorting or equivalent practitioner review.
- [x] Verify and update protected role-based visibility to consume server-resolved permissions; browser role selection retained only for explicit local/demo preview.
- [ ] Validate global search, recent work, queues, record/work-item/conversation relationships, and cross-surface handoffs.
- [ ] Obtain named Product, Insurance, Operations, Design/Research, Architecture, Security/Privacy, and Delivery approval for exact IA v1.0; retain conditions and expiry triggers.

## 5. User journeys and task flows

**Current status:** Partial
**Close when:** End-to-end journeys and exact task flows include role, permission, evidence, decision, exception, recovery, and success paths and pass representative-user validation.

**IIP evidence:** `iip_end_to_end_user_flow_architecture.md`, platform UX workflows, workflow architecture, PRD motor-claims lifecycle, UAT scripts.

**Best skills:** Installed — `persona-jobs-journey-modeler`, `user-flow-architect`, `error-prevention-recovery-designer`, `complex-ui-pattern-designer`, `human-centered-interaction-designer`. Add — none.

- [x] Primary Motor Claims and Document Intelligence flow documented.
- [x] Human-review and consequential-decision boundaries included.
- [x] Evidence, audit, tenant, and provider boundaries included in architecture flows.
- [ ] Create journey maps for each Release 1 role, including before/after-system context.
- [ ] Confirm entry/exit points and handoffs across email, WhatsApp, portal, insurer systems, and human queues.
- [ ] Document decision trees for missing, conflicting, stale, superseded, low-confidence, and unauthorized evidence.
- [ ] Cover cancellation, retry, interruption, reauthentication, reassignment, escalation, expiry, rollback, and recovery.
- [ ] Validate critical flows with representative practitioners and update requirements.

## 6. Interaction and behavior design

**Current status:** Partial
**Close when:** Every critical action has purpose, authority, prerequisites, state transitions, feedback, failure/recovery, accessibility behavior, and implementation parity.

**IIP evidence:** Platform UX behavior specification, design-system behavior specification, agentic AI patterns, recent-conversation specification, handoff document.

**Best skills:** Installed — `cognitive-ux-designer`, `microinteraction-design`, `error-prevention-recovery-designer`, `forms-data-entry-designer`, `complex-ui-pattern-designer`, `ux-writing-microcopy`, `accessibility-design-auditor`. Add — none.

- [x] Primary workflow behavior and human/AI action boundaries documented.
- [x] Loading, empty, error, success, disabled, permission, partial, provider-not-configured, and audit concepts documented.
- [ ] Create an interaction contract for each consequential or externally visible action.
- [ ] Define default, hover, focus, pressed, disabled, loading, success, error, retry, and duplicate-submit behavior for interactive controls.
- [ ] Define autosave, undo/cancel, confirmation, idempotency, stale-data, conflict, offline, and session-expiry behavior.
- [ ] Verify keyboard, touch, screen-reader announcement, focus return, and reduced-motion behavior.
- [ ] Reconcile behavior specification against implemented components and APIs.

## 7. Low-fidelity wireframes

**Current status:** Partial
**Close when:** Reviewed low-fidelity layouts cover critical flows, roles, states, and responsive variants before further visual expansion.

**IIP evidence:** `iip_release_1_low_fidelity_wireframe_state_inventory.md` defines the canonical 34-screen boundary and seven required state variants. `iip_release_1_responsive_adaptive_reflow_contract.md` defines intrinsic desktop, tablet and mobile navigation/layout/component transformations, state preservation and test scenarios. `/prototype/release-1-low-fidelity?variant=A|B|C` provides three synthetic, development-only structures across 12 critical surface/mode entries, eight release roles, seven state families and three responsive contexts. Direction selection, exact screen-specific annotation, cross-functional review and participant evidence remain absent.

**Best skills:** Installed — `prototype`, `screen-state-inventory`, `user-flow-architect`, `information-architecture-designer`, `responsive-adaptive-ui-designer`, `forms-data-entry-designer`. Add — none.

- [x] Create low-fidelity prototype layouts for Command Center orientation-to-action.
- [x] Create low-fidelity prototype layouts for FNOL, claim review, document review/correction, approval, customer draft/send, and audit evidence.
- [x] Define loading, empty, error, permission, partial, success and recovery coverage for every canonical screen; preserve exact not-configured, unavailable, timeout, stale/conflict and interruption meanings within those variants.
- [x] Define desktop, tablet and mobile/low-bandwidth reflow concepts, including navigation, tables, evidence, approvals, dialogs, forms, state/focus preservation and input/capability adaptations.
- [x] Annotate prototype hierarchy, content priority, action purpose, state transition, and evidence/human-review boundary.
- [ ] Review with product, insurance operations, engineering, security/accessibility, and representative users.

## 8. Early usability review

**Current status:** Gap
**Close when:** Low-cost tests show users can understand the model and complete critical tasks before high-fidelity work is treated as approved.

**IIP evidence:** `docs/research/iip-release-1-low-fidelity-usability-evaluation.md` records a reproducible heuristic/cognitive/accessibility diagnostic, nine prioritized findings, applied low-fidelity remediations, ten neutral moderated tasks, metrics, observation schema and the existing 90%/zero-consequential-error gate. Browser diagnostics passed after remediation. No representative-user session, task metric, confidence result, quote or participant synthesis exists; status therefore remains Gap and high-fidelity approval remains blocked.

**Best skills:** Installed — `heuristic-ux-auditor`, `cognitive-ux-designer`, `usability-test-planner-moderator`, `research-interview-facilitator`, `research-synthesis`, `accessibility-design-auditor`. Add — A2 `usability-testing`.

- [x] Define research questions and pass/fail criteria per critical wireframe flow.
- [ ] Recruit representative Uganda insurance roles and accessibility participants.
- [ ] Test comprehension of record, work item, conversation, evidence, proposal, approval, and audit states.
- [ ] Test task completion, findability, terminology, error recovery, interruption/resume, and low-bandwidth expectations.
- [ ] Record task success, critical errors, assistance, time, confidence, observed confusion, and qualitative evidence.
- [x] Prioritize expert/browser findings by risk and update flows/wireframes before visual sign-off; repeat after participant sessions.

## 9. Visual direction

**Current status:** Partial
**Close when:** One approved visual direction has clear principles, references, brand alignment, density, and accessibility constraints with no conflicting authority.

**IIP evidence:** `AGENTS.md` and design-system specification declare Ambient Intelligence authoritative. The untracked 4 August `iip_visual_design_system_and_tokens.md` proposes Operational Intelligence and says it replaces conflicting visual guidance; this needs explicit reconciliation, not silent adoption.

**Best skills:** Installed — `design-strategy-builder`, `brand-interface-translator`, `visual-hierarchy-designer`, `visual-design-polish`, `ui-critique`, `design-consistency-auditor`. Add — A3 `frontend-design`, but only after authority is resolved.

- [x] Ambient Intelligence direction, palette family, shell, and chat-first principles documented in current authority.
- [x] Enterprise tone and human/AI/evidence visual distinctions documented.
- [ ] Decide Ambient Intelligence versus the proposed Operational Intelligence changes.
- [ ] Record the decision in the authoritative design specification and ADR/decision log.
- [ ] Create/approve mood board and representative reference screens using real IIP content.
- [ ] Define acceptable density, brand expression, glass/translucency, motion, dark mode, and data-visualization constraints.
- [ ] Validate direction for trust, clarity, cultural fit, accessibility, and operational use—not aesthetic preference alone.

## 10. Design foundations and tokens

**Current status:** Partial
**Close when:** Primitive, semantic, and component tokens are authoritative, implemented, documented, accessible, versioned, and parity-tested.

**IIP evidence:** Design-system specification, `lovable.css`, `globals.css`, Tailwind mapping, and untracked visual-token specification. Current docs disagree on whether two token layers remain deliberate and on the visual direction.

**Best skills:** Installed — `design-token-variable-architect`, `ai-design-system-trainer`, `design-system-ai-skills`, `accessibility-design-auditor`, `responsive-adaptive-ui-designer`. Add — none.

- [x] Color, typography, spacing, sizing, shape, elevation, motion, layering, and responsive foundations documented.
- [x] Semantic status and AI/evidence/human-review concerns are represented.
- [ ] Resolve token-source and visual-authority conflicts.
- [ ] Establish one versioned token source with migration aliases and ownership.
- [ ] Verify code contains no unjustified hard-coded visual values outside approved exceptions.
- [ ] Test semantic color contrast, focus indicators, forced colors, zoom/reflow, and dark-mode mappings.
- [ ] Add automated token parity/drift checks between source, Tailwind/CSS, components, and design tooling.

## 11. Components and patterns

**Current status:** Partial
**Close when:** The component/pattern library covers critical variants and states, is accessible, documented, reusable, and used consistently in production paths.

**IIP evidence:** Component architecture, design-system component rules, surface/control contracts, and `iip_release_1_complex_operational_pattern_contracts.md` now define queue, evidence-review, approval-cockpit, table, panel and dialog purpose, anatomy, state ownership, coordinated behavior, responsive/accessibility rules, analytics and tests. Shared components and visual tests exist; rendering, component/API parity, accessibility and migration audit gaps remain.

**Best skills:** Installed — `component-system-designer`, `ui-pattern-library`, `complex-ui-pattern-designer`, `design-consistency-auditor`, `accessibility-design-auditor`, `screen-state-inventory`. Add — none.

- [ ] Publish a canonical inventory of primitives, composites, workflow patterns, owners, and implementation paths.
- [ ] Cover buttons, links, inputs, selection, upload, tables, tabs, dialogs, drawers, alerts, toasts, tooltips, progress, evidence, approvals, AI proposals, and provider states.
- [ ] Document variants, size/density, content limits, permissions, responsive behavior, and all system states.
- [ ] Verify accessible names, focus order/traps, announcements, keyboard behavior, touch targets, and non-color cues.
- [ ] Add isolated examples/Storybook or equivalent for critical components and states.
- [ ] Remove competing/dead components after canonical paths are proven.
- [ ] Enforce shared components in new screens and visual regression coverage for critical variants.

## 12. High-fidelity interface design

**Current status:** Partial
**Close when:** Critical Release 1 screens and states are visually approved, use real content/contracts, and map directly to the component/token system.

**IIP evidence:** `/app/*` Lovable-parity surface and detailed design documentation exist. Migration audit shows incomplete API reconciliation and prototype/fake-success residue.

**Best skills:** Installed — `ui-ux-product-design-orchestrator`, `visual-hierarchy-designer`, `visual-design-polish`, `design-consistency-auditor`, `ux-writing-microcopy`, `responsive-adaptive-ui-designer`. Add — A3 `frontend-design` after visual authority resolution.

- [ ] Define the canonical Release 1 screen list and every required state/role variant.
- [ ] Replace sample/fake content with representative synthetic records or honest empty/not-configured states.
- [ ] Produce approved desktop and responsive compositions for critical workflows.
- [ ] Confirm visual hierarchy reflects action importance, risk, permission, evidence, and human accountability.
- [ ] Verify design uses canonical tokens/components and exact backend DTO/content constraints.
- [ ] Complete product, domain, accessibility, engineering, and security design review.

## 13. Interactive prototyping

**Current status:** Partial
**Close when:** A task-testable prototype covers the critical flows, branches, feedback, errors, and recovery with clear fidelity limits.

**IIP evidence:** `prototypes/iip-undp-demo.PROTOTYPE.html` and working portal exist; no single canonical participant-test prototype and scenario/evidence pack was found.

**Best skills:** Installed — `prototype`, `microinteraction-design`, `user-flow-architect`, `error-prevention-recovery-designer`, `playwright`. Add — A2 `usability-testing` for test design.

- [x] At least one interactive prototype artifact exists.
- [ ] Select one canonical prototype target/version for each research round.
- [ ] Cover critical end-to-end paths plus missing evidence, provider failure, permission denial, interruption, and recovery.
- [ ] Annotate simulated versus real behavior; never confuse prototype success with deployed capability.
- [ ] Create task scenarios, expected success conditions, observation guide, and data-capture template.
- [ ] Verify prototype behavior matches approved interaction contracts and is safe for participant testing.

## 14. Design validation

**Current status:** Gap
**Close when:** Evidence shows representative users can understand and complete critical tasks and the design passes expert, content, accessibility, and feasibility review.

**IIP evidence:** Expert audits, migration audits, visual regression specs, and acceptance criteria exist. Participant and complete accessibility conformance evidence do not.

**Best skills:** Installed — `heuristic-ux-auditor`, `design-consistency-auditor`, `accessibility-design-auditor`, `usability-test-planner-moderator`, `ux-writing-microcopy`, `research-synthesis`. Add — A2 `usability-testing`.

- [ ] Run heuristic, cognitive, content, design-consistency, security/privacy, and technical-feasibility reviews.
- [ ] Run moderated task tests across critical roles and exception scenarios.
- [ ] Validate evidence comprehension, calibrated reliance, human-review boundaries, and safe contestability.
- [ ] Validate terminology, multilingual strategy, accessibility, responsive behavior, and low-bandwidth recovery.
- [ ] Record severity, evidence, owner, decision, remediation, and retest result.
- [ ] Obtain design approval only after critical findings are closed or explicitly accepted by authorized owners.

## 15. Frontend planning

**Current status:** Partial
**Close when:** Routes, rendering/data strategy, state ownership, component mapping, auth, API contracts, accessibility, performance, tests, rollout, and migration are implementation-ready and current.

**IIP evidence:** Frontend architecture/handoff, frontend implementation guide, UI/UX handoff, component architecture, migration audit, and OpenAPI/API specs.

**Best skills:** Installed — `build-web-apps:react-best-practices`, `build-web-apps:frontend-app-builder`, `architecture-and-design`, `codebase-design`, `design-handoff-documentation-generator`, `prototype-to-production-parity`, `writing-plans`. Add — none.

- [x] Canonical framework, route tree, shell, and API prefix documented.
- [x] Auth, tenant/workspace, no-simulation, and human-review frontend boundaries documented.
- [ ] Reconcile frontend plan with the final visual/token authority decision.
- [ ] Map each Release 1 screen/state to route, component, API DTO, permission, loading/error behavior, and test.
- [ ] Define server/client boundaries, caching/revalidation, state ownership, token refresh, and stale-data handling.
- [ ] Set performance budgets, browser/device support, accessibility target, and low-bandwidth strategy.
- [ ] Sequence migration/removal work without breaking canonical routes or deployed profiles.

## 16. Frontend implementation

**Current status:** Partial
**Close when:** The canonical portal implements approved designs using real protected contracts, passes quality gates, and contains no deployed fake-success or fixture paths.

**IIP evidence:** Real Next.js portal, `/app/*` shell, many per-screen hooks, frontend tests, and CI exist. Current-state and migration audits still identify incomplete reconciliation.

**Best skills:** Installed — `build-web-apps:frontend-app-builder`, `build-web-apps:react-best-practices`, `design-to-code-implementer`, `clean-implementation`, `tdd`, `playwright`, `prototype-to-production-parity`. Add — A3 `frontend-design` only for approved design implementation.

- [ ] Implement every critical Release 1 screen/state from approved contracts.
- [ ] Remove deployed fixture, hardcoded-insurer/user, browser-authority, and fake-success behavior.
- [ ] Use real bearer-token auth, `/api/v1`, server-resolved tenant/workspace, and honest provider states.
- [ ] Implement keyboard, focus, announcements, responsive layouts, preserved input, retry, and reduced-motion behavior.
- [ ] Meet bundle, rendering, interaction, and low-bandwidth budgets.
- [ ] Pass lint, unit/component tests, visual regression, deployed-profile checks, and production build.

## 17. Backend integration

**Current status:** Partial
**Close when:** Every frontend workflow uses real application services/providers or explicit `Not configured`, with tenant scope, permission, validation, audit, idempotency, and safe failure proven end to end.

**IIP evidence:** Layered .NET backend, core ports, EF repositories, many `/api/v1` routes, Azure adapters, automated tests, and dev deployment evidence. Live provider and staging proof remain incomplete.

**Best skills:** Installed — `architecture-data-systems`, `domain-modeling`, `responsible-algorithm-engineering`, `clean-implementation`, `tdd`, `microsoft-foundry`, `data-systems-and-operations`. Add — A4 `azure-reliability`.

- [x] Core architecture, tenant context, permissions, audit, provider health, and `/api/v1` rule implemented.
- [ ] Map every frontend action/read to an existing real endpoint or explicit unavailable state.
- [ ] Prove request/response validation, authorization, tenant/workspace filtering, concurrency, idempotency, and audit.
- [ ] Prove Azure SQL, Blob, Document Intelligence, AI Search, Azure OpenAI/Foundry, Service Bus, and configured communication providers in protected runtime.
- [ ] Verify no deployable project references test doubles or fallback-to-mock behavior.
- [ ] Complete end-to-end error mapping without raw provider errors, secrets, internal notes, or cross-tenant exposure.

## 18. Functional testing

**Current status:** Partial
**Close when:** Automated and manual tests prove normal, negative, permission, provider, concurrency, recovery, and cross-system behavior for critical workflows.

**IIP evidence:** Hundreds of backend tests, portal tests, visual specs, CI, live-dev harnesses, UAT scripts, and current go/no-go evidence. Authenticated multi-role staging UAT and live provider proof remain open.

**Best skills:** Installed — `testing-and-quality`, `tdd`, `playwright`, `build-web-apps:frontend-testing-debugging`, `diagnosing-bugs`, `code-review`, `responsible-algorithm-engineering`. Add — none.

- [x] Automated unit/integration/API tests and CI exist.
- [x] Critical workflow, human-review, lineage, and invalidation contract tests exist.
- [ ] Maintain a requirements-to-test matrix for every Release 1 acceptance criterion.
- [ ] Test happy, negative, validation, permission, tenant isolation, duplicate, concurrency, timeout, retry, and provider-unhealthy paths.
- [ ] Run authenticated end-to-end tests with each critical role in a protected environment.
- [ ] Prove real provider interactions, audit trails, notifications/queues, recovery, and no fabricated results.
- [ ] Record build/test versions, environments, evidence artifacts, defects, and retest results.

## 19. Accessibility and responsive testing

**Current status:** Partial
**Close when:** Automated and manual evidence shows critical workflows meet the approved WCAG target and work across supported viewport, input, zoom, language, and connectivity conditions.

**IIP evidence:** `iip_release_1_interaction_accessibility_and_runtime_parity.md` v0.2 applies WCAG 2.2 AA keyboard, focus, semantics/status, touch/pointer and reduced-motion requirements to 20 interactions, six complex patterns and seven low-fidelity states; responsive, QA and local diagnostic evidence also exist. The diagnostic records modal, naming, target-size, status, motion, focus and contrast failures. Complete conformance, manual AT, physical-device and protected-runtime testing has not occurred.

**Best skills:** Installed — `accessibility-design-auditor`, `responsive-adaptive-ui-designer`, `playwright`, `forms-data-entry-designer`, `visual-hierarchy-designer`, `ux-writing-microcopy`. Add — none; local coverage is stronger than current skills.sh search results.

- [ ] Declare WCAG version/level, supported browsers/devices/viewports, zoom range, and assistive technologies.
- [ ] Run automated accessibility checks on every critical route/state.
- [ ] Complete manual keyboard-only, focus, screen-reader, zoom/reflow, high-contrast/forced-colors, reduced-motion, and touch-target testing.
- [ ] Verify errors, validation, status, charts, AI confidence/provenance, and risk never rely on color alone.
- [ ] Test desktop, tablet/narrow desktop, essential mobile, long text, translated content, and large data tables.
- [ ] Test slow/intermittent connectivity, upload recovery, saved drafts, reconnect, retries, duplicate protection, and honest offline behavior.
- [ ] Block release on unresolved critical accessibility barriers.

## 20. Usability testing

**Current status:** Gap
**Close when:** Representative users complete realistic critical tasks with acceptable success, comprehension, safety, and recovery, and findings have been remediated/retested.

**IIP evidence:** UX research plan and UAT plans exist; the platform UX specification states participant sessions were not supplied.

**Best skills:** Installed — `usability-test-planner-moderator`, `research-interview-facilitator`, `contextual-inquiry-observer`, `ux-metrics-experiment-designer`, `research-synthesis`, `persona-jobs-journey-modeler`. Add — A2 `usability-testing` and A1 `discovery-interview-prep`.

- [ ] Define test questions, critical tasks, safety/comprehension risks, metrics, and success thresholds.
- [ ] Recruit representative Uganda claims, supervision/approval, assessment, customer-service, compliance, IT, and accessibility participants.
- [ ] Test realistic cases including incomplete/conflicting evidence, corrections, approval, communication, interruption, and low bandwidth.
- [ ] Measure completion, critical errors, assists, time, confidence, comprehension, calibrated reliance, recovery, and satisfaction.
- [ ] Separate observed evidence from participant opinion and researcher inference.
- [ ] Prioritize findings, update artifacts/implementation, and retest critical issues.
- [ ] Preserve multilingual/customer-document realities in recruitment and test material.

## 21. Quality assurance

**Current status:** Partial
**Close when:** Functional, regression, visual, security, privacy, AI safety, performance, resilience, accessibility, usability, deployment, recovery, and UAT gates pass with traceable evidence.

**IIP evidence:** QA plan, functional pack, regression checklist, security plan, AI evaluation/red-team plan, performance plan/evidence, CI, and RF-10 go/no-go pack. RF-10 records remaining human-owned blockers.

**Best skills:** Installed — `testing-and-quality`, `code-review`, `ethical-software-review`, `responsible-algorithm-engineering`, `playwright`, `pragmatic-delivery-engineering`, `diagnosing-bugs`. Add — A4 `azure-reliability`.

- [x] QA, security, AI evaluation, performance, regression, and release-test plans exist.
- [x] CI build/test/lint/infrastructure checks exist.
- [ ] Run the full release matrix against the exact candidate commit and protected environment.
- [ ] Complete representative p95/p99, resilience, queue/DLQ, backup/restore, rollback, and incident-response proof.
- [ ] Complete authenticated multi-role UAT and accessibility/usability gates.
- [ ] Resolve all Sev 0/1 defects and document authorized treatment of lower-severity risks.
- [ ] Publish one evidence-backed go/no-go decision with artifact links and no simulated substitutes.

## 22. Release

**Current status:** Blocked
**Close when:** The release candidate passes staging, security, operational, UAT, packaging, reviewer, rollback, support, legal/commercial, and approval gates.

**IIP evidence:** Consolidated roadmap, deployment/release plans, Managed Application packaging guides, runbooks, and RF-10 final evidence. RF-10 states `PRODUCTION-CANDIDATE: NO`, `MANAGED APPLICATION PACKAGE READY: NO`, and `PARTNER CENTER PRIVATE PLAN READY: NO`.

**Best skills:** Installed — `pragmatic-delivery-engineering`, `microsoft-foundry`, `testing-and-quality`, `code-review`, `resolving-merge-conflicts`, `handoff`. Add — A4 `azure-reliability`.

- [ ] Freeze and identify the exact release candidate commit, artifacts, configuration, migrations, and evidence pack.
- [ ] Pass protected-runtime staging deployment and live provider/readiness checks.
- [ ] Pass security/privacy/compliance, tenant isolation, AI human-review, performance, resilience, accessibility, usability, and UAT gates.
- [ ] Validate Managed Application package root, ARM compatibility, createUiDefinition, reviewer profile, RBAC/deny assignment, and zero-simulation parameters.
- [ ] Confirm Partner Center account, offer/private-plan audience, publisher/customer access, support, pricing, legal, and customer onboarding readiness.
- [ ] Rehearse migration, backup, rollback, disaster recovery, incident response, and support handoff.
- [ ] Obtain authorized go/no-go approval and publish release notes/known limitations.
- [ ] Run post-deployment validation before declaring the release operational.

## 23. Monitoring and refinement

**Current status:** Partial
**Close when:** Production telemetry, feedback, support, AI quality, business outcomes, accessibility, reliability, and improvement decisions operate on an owned cadence.

**IIP evidence:** Observability architecture, support model/runbooks, feedback and post-launch templates, improvement tracker, health scorecard, and product-feedback cadence exist. Production operation is not proven.

**Best skills:** Installed — `ux-metrics-experiment-designer`, `product-discovery-strategy`, `customer-conversations`, `research-synthesis`, `pragmatic-delivery-engineering`, `data-systems-and-operations`, `microsoft-foundry`. Add — A4 `azure-reliability`.

- [ ] Define product, workflow, business, reliability, security, accessibility, AI-quality, cost, and support metrics with owners and thresholds.
- [ ] Instrument dashboards/alerts using real telemetry without sensitive payloads or fabricated health.
- [ ] Monitor task success, abandonment, rework, missing-document loops, human overrides/corrections, evidence quality, provider failures, SLA risk, and customer outcomes.
- [ ] Operate incident, problem, support, feedback, research, and model/prompt/workflow review cadences.
- [ ] Segment findings by role, workspace, device/connectivity, language, workflow, and accessibility need where lawful and privacy-safe.
- [ ] Link every improvement to evidence, hypothesis, owner, metric, priority, release, and post-release result.
- [ ] Re-run usability/accessibility/reliability checks after material changes.
- [ ] Keep the roadmap, requirements, design system, known issues, release evidence, and this checklist synchronized.

---

## Cross-stage governance checklist

- [ ] Assign one accountable owner and target review date to every unchecked item selected for the current horizon.
- [ ] Link every checked item to current, reviewable evidence.
- [ ] Mark assumptions and hypotheses; do not present desk research as participant validation.
- [ ] Preserve tenant/workspace isolation, human accountability, approved-source grounding, auditability, and zero simulation in every stage.
- [ ] Treat multilingual support, customer documents, accessibility, responsive behavior, low bandwidth, error recovery, privacy, and security as cross-stage requirements.
- [ ] Reconcile the Ambient Intelligence versus Operational Intelligence visual-authority conflict before approving further design-system or high-fidelity expansion.
- [ ] Review this checklist at product/design/engineering governance cadence and after every material research or release decision.

## Immediate evidence-closing actions

1. Approve a primary research plan and recruit Uganda insurance practitioners/buyers.
2. Validate the product problem, workflows, terminology, personas, buyer constraints, and success measures.
3. Produce and test low-fidelity Release 1 wireframes before treating current high-fidelity work as final.
4. Resolve the visual-direction/token authority conflict and record the decision.
5. Convert research results into requirements, flows, components, implementation, and traceability updates.
6. Complete participant usability, manual accessibility, responsive/low-bandwidth, authenticated UAT, live-provider, latency, and release evidence.

## Risks and dependencies

- Practitioner and buyer recruitment is the largest discovery dependency.
- Visual authority is currently inconsistent across authoritative and newly added untracked documents.
- Azure/provider credentials, capacity, staging environment, and human operational actions block live proof.
- Existing high-fidelity/code investment can bias research; test problems and flows, not only the current solution.
- The repository is a strong source for intended behavior and current implementation, but cannot prove market demand, user comprehension, usability, adoption, or production readiness by itself.
