# IIP Product Decision Log

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Product Decision Log |
| Version | 1.0 |
| Status | Draft |
| Audience | Leadership, product owners, architects, delivery managers, engineering leads |
| Date | 2026-06-30 |


## 1. Purpose

This decision log records key product decisions that should guide future planning, implementation, QA, deployment, and client communication. It is intended to reduce repeated debate and prevent coding agents or implementation teams from reintroducing superseded assumptions.


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


## 2. Decision Log

| Decision ID | Decision | Status | Rationale | Implementation Impact |
| --- | --- | --- | --- | --- |
| DEC-001 | Use Insurance Intelligence Platform (IIP) as the official product name | Confirmed | The name is clearer, broader, and more client-ready. | Use IIP consistently across documents, UI, repositories, proposals, and architecture references. |
| DEC-002 | Treat ACIP/Agentic naming as superseded legacy context | Confirmed | Older drafts used ACIP, but the official name is IIP. | Do not introduce ACIP in new documents unless explaining legacy references. |
| DEC-003 | MVP focuses on motor claims and document intelligence | Confirmed | A claims/document MVP is practical, demonstrable, and high-value. | Prioritize Claims, Documents, Channels context, Audit, and Command Center. |
| DEC-004 | Customers do not log into the internal staff application | Confirmed | IIP staff app is for insurer/Otic users; customers use external channels. | Design customer workflows through WhatsApp/email/web/SMS and internal staff views. |
| DEC-005 | AI assists, humans decide | Confirmed | Insurance decisions are consequential and require accountability. | Every AI output must be reviewable, auditable, and non-binding. |
| DEC-006 | Azure-first architecture | Confirmed | Enterprise insurers require security, identity, data, and operational trust. | Use Azure SQL, Blob, Key Vault, AI Search, Azure OpenAI, Document Intelligence, Service Bus, App Insights, Entra ID. |
| DEC-007 | Support both Managed Application and SaaS paths | Confirmed direction | Enterprise customers may need customer-tenant deployment while demos/pilots may need SaaS. | Keep deployment abstraction and tenant isolation clean. |
| DEC-008 | No mock data in staging/production | Confirmed | Trustworthy implementation requires truthful states. | Use real data, empty states, error states, or Not configured states. |
| DEC-009 | Role-aware navigation must not be frontend-only security | Confirmed | Browser-owned permission checks are insufficient. | Backend authorization and audit logging must enforce permissions. |
| DEC-010 | Start with controlled integrations | Confirmed direction | Integration risk can derail MVP if uncontrolled. | Prioritize WhatsApp/email/Graph, Blob, AI, SQL, and optional read-only policy lookup. |
| DEC-011 | Adopt the Lovable-designed "Ambient Intelligence" visual theme, superseding the flat Light Intelligence Workspace palette | Confirmed | Owner-designed reference prototype (Lovable) is the desired visual direction; skin-only change, chat-first architecture unchanged. | Update `iip_design_system_ux_behavior_specification.md` tokens/patterns; re-skin shell and shared card primitives in `web/portal`; no IA/route changes required by this decision alone. |
| DEC-012 | Bring Channels, Customer 360, Communications, Documents, Support, and Workspaces to full workflow/logic parity with the Lovable reference — real backend logic, not mock, wherever the current implementation is partial | Confirmed | Owner wants everything designed in Lovable to be genuinely functional in IIP, not a demo; where the current stack differs from Lovable's, the pattern is ported, the library is not. | New/extended backend endpoints (Channels actions, Documents/Communications status enums, Customer 360 summary), new Customer 360 detail route, new cross-case Documents/Communications views, Workspace switcher. |
| DEC-013 | Add four previously out-of-scope modules — AI Agent Registry (+ kill switch), Fraud Intelligence, Underwriting Intelligence, Billing/entitlements — as full, real features, not deferred | Confirmed | These exist in the Lovable reference with no backend/doc/UI equivalent in IIP today; owner wants full parity, not a trimmed MVP. | New Domain entities, query/command services, endpoints, and UI routes for each, following the existing Phase 2 query-service pattern; new doc screen specs; ADR for the multi-agent orchestration/kill-switch boundary. |
| DEC-014 | Fraud Intelligence and Underwriting Intelligence use deterministic rule engines for v1, not an LLM | Confirmed | Rule output is auditable/defensible if disputed, requires no live Azure OpenAI dependency (pre-Gate-1 at time of decision), is unit-testable, and structurally keeps AI out of the fraud/underwriting decision seat. | Build rule engines against real case/customer data now; revisit layering Azure OpenAI narration on top once Gate 3 activates real providers (tracked separately for follow-up, do not silently upgrade). |
| DEC-015 | The Lovable-parity implementation is delivered as the canonical `/app/*` route tree (LovableVisualParity mode), superseding DEC-012's incremental real-route rework | Confirmed | The migration (`47ed7a8`, ADR-2026-07-04) mounted the full Lovable prototype under `/app/*` with dev-only fixtures, which the owner adopted as canonical; reconciling each screen to real `/api/v1` data is cleaner than reworking each legacy route in place. | Reconcile `/app/*` screens off fixtures onto real APIs (per-screen `lib/lovable-parity/data/*` hooks); the 20 pre-migration top-level routes + the IIP `AppShell`/`roleUiConfig`/`RoleContext` become dead and are removed once nav fully covers `/app/*`; consolidate the `globals.css`+`lovable.css` token systems; resolve the prod parity-gate. Backlog: `docs/06-decisions/lovable-parity-migration-audit.md`. |
| DEC-016 | Provide two deployed review modes — a `Demo` synthetic-data sandbox and live `Production` — as distinct runtime profiles rather than a toggle inside Production | Confirmed | Owner wants to review the system both fully-populated (for demos/pilots) and against real data, without ever letting synthetic data masquerade as live. Making `Demo` a first-class profile keeps the existing guardrail intact: a `Production`/`Staging`/`ManagedApplicationReviewer` boot with seeded data is impossible by construction. | `Demo` profile validated with Production-grade strictness (real Entra/SQL/providers, no mock escape hatch) except `SEED_DEMO_DATA` is permitted into the isolated `tenant-demo`; server-side review-mode classification on `GET /api/v1/me`; mandatory `EnvironmentBanner` (amber for synthetic, silent for live, fail-closed "unverified"). Both modes run identical real code paths — no mock runtime, no fake success. Designed to map onto two deployed Azure environments once Gate 2 lands. See `iip_environment_variables_secrets_guide.md` §"Review modes". |



## 3. Decision Categories

| Category | Key Decisions |
| --- | --- |
| Product identity | DEC-001, DEC-002 |
| MVP scope | DEC-003, DEC-004 |
| AI governance | DEC-005 |
| Architecture/deployment | DEC-006, DEC-007, DEC-016 |
| Engineering guardrails | DEC-008, DEC-009 |
| Integration sequencing | DEC-010 |
| Visual/UX parity (Lovable reference) | DEC-011, DEC-012, DEC-015 |
| Feature scope expansion (Agent Registry, Fraud, Underwriting, Billing) | DEC-013, DEC-014 |



## 4. Decision Review Triggers

A decision should be reviewed only when one of the following occurs:

- Pilot client requirements materially change the MVP scope.
- Regulatory/legal review requires a change in AI or audit behavior.
- Client IT requires a different deployment model.
- A confirmed integration is unavailable or unsafe for MVP.
- Commercial packaging changes the module sequence.

## 5. Open Questions

| ID | Open Question | Affected Decisions |
| --- | --- | --- |
| OQ-DEC-001 | Which deployment path will the first client pilot use? | DEC-007 |
| OQ-DEC-002 | Which integrations are mandatory for pilot launch rather than Phase 2? | DEC-010 |
| OQ-DEC-003 | What support model will be offered during the pilot? | DEC-007, DEC-010 |

## 6. Decision Record Template

| Field | Guidance |
| --- | --- |
| Decision ID | Sequential identifier, e.g., DEC-011. |
| Decision Title | Short decision label. |
| Decision Statement | Clear statement of what has been decided. |
| Options Considered | List alternatives considered. |
| Reason for Decision | Explain business, technical, compliance, or delivery rationale. |
| Decision Owner | Person or group accountable for decision. |
| Date | Date decision was made. |
| Review Trigger | When decision should be revisited. |
| Implementation Impact | What must change in documents, backlog, code, tests, or communications. |

## 7. Additional Decisions to Capture Next

| Decision Needed | Why It Matters |
| --- | --- |
| Final pilot client | Determines configuration, governance, and commercial terms. |
| Final deployment model | Determines architecture, security review, Azure setup, and cost. |
| Final document checklist | Determines document intelligence configuration and test pack. |
| Final role list | Determines RBAC matrix and training materials. |
| Final support model | Determines pilot confidence and escalation flow. |
| Final success KPIs | Determines pilot value proof. |

## 8. Decision Governance

| Practice | Rule |
| --- | --- |
| Document before build | Major decisions should be captured before coding agents implement them. |
| Update linked artifacts | A decision may require updates to PRD, FSD, API spec, backlog, QA plan, or training material. |
| Separate decision from opinion | Only confirmed decisions enter the log; suggestions become open questions. |
| Use review triggers | Do not reopen decisions unless a trigger is met. |
