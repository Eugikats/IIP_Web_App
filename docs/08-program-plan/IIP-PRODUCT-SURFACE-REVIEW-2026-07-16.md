# IIP Product Surface Review

**Date:** 2026-07-16  
**Scope:** Current IIP Operations `/app/*` screens after the Bevaya benchmark, agent-workflow control-plane work, zero-simulation hardening, and Ambient Intelligence UI migration.  
**Decision:** Consolidate overlapping surfaces, keep workflow-critical screens, and make incomplete capabilities visibly honest instead of presenting decorative or simulated actions.

## 1. Review standard

A screen remains a first-class IIP surface only when it has a distinct accountable user job and at least one of the following:

1. a real tenant/workspace-scoped backend contract;
2. a required human-review or evidence boundary;
3. an essential control-plane responsibility; or
4. an honest read-only view that prevents users from confusing an unavailable capability with a working one.

A screen is merged or removed when it duplicates another job, depends on hardcoded business data, exposes buttons without a real governed endpoint, or adds navigation weight without advancing an insurer workflow.

The benchmark-led product flow used for this review is:

```text
Command Center
  -> claim/customer/channel context
  -> document extraction and evidence verification
  -> agent workflow execution
  -> human review of prepared work
  -> separate consequential approval
  -> governed communication/write-back
  -> audit and operational reporting

Control plane: Automation Studio + agent policy + integrations/health + tenant administration
```

## 2. Result at a glance

- The 31 routed page entries now represent **28 canonical routes plus 3 compatibility routes**.
- Three overlapping product surfaces were consolidated:
  - Workflow Approvals -> Review & Approvals.
  - Support & Health -> Integrations & Health.
  - Settings -> Admin console.
- Six obsolete page components were removed: the old `Settings`, `Support`, prototype `Onboarding`, unused `ClientPortal`, unused `Workspaces`, and unused root `Index` pages.
- The critical document-verification surface was rebuilt from static sample content into a real case/document workflow.
- The left navigation was reorganized around insurer work rather than a flat list of every feature.
- Fake operational actions were removed from Communications, Documents, Knowledge, Underwriting, Billing, and the old Support surface.
- A route-authorization defect that allowed `/app` membership to match every nested `/app/*` path was corrected. Root routes now match exactly, while only explicitly assigned modules grant nested access.

## 3. Removed or merged surfaces

| Previous surface | Decision | Canonical destination | Why |
|---|---|---|---|
| `/app/workflows` as a separate Workflow & Approvals screen | Merge | `/app/work-items` Review & Approvals | AI-prepared artifacts and approval requests are both human queues. A single workspace reduces queue fragmentation while its two tabs preserve the crucial boundary between accepting prepared work and authorizing a consequential workflow step. The old route remains as a compatibility entry that opens the approval tab. |
| `/app/support` | Remove as a product screen | `/app/integrations` | It repeated provider health and added unsupported queue depth, parity metrics, connection tests, and ticket-success behavior. Provider/capability health belongs in one control plane. The old route now redirects. |
| `/app/settings` | Remove as a product screen | `/app/admin` | It was a static profile/preferences page with hardcoded identity and non-persisted success feedback. Real tenant settings, users, policies, agents, onboarding, and billing already belong in Admin. The old route now redirects. |
| `lovable-pages/Onboarding.tsx` | Remove | `/app/admin/onboarding` | It was an unused prototype separate from the governed Entra/subscription onboarding implementation. |
| `lovable-pages/ClientPortal.tsx` | Remove | Channels and link-assisted customer workflows | The product requirements exclude a standalone customer login portal from the current scope; the file was not routed and contained prototype content. |
| `lovable-pages/Workspaces.tsx` | Remove | Runtime workspace context and Admin | It was not routed and duplicated server-resolved workspace identity/administration. |
| `lovable-pages/Index.tsx` | Remove | `/app` Command Center | It was an unused root prototype after the Next `/app/*` migration. |

## 4. Canonical screen decisions

| Product area | Canonical route(s) | Decision | Improvement or reason retained |
|---|---|---|---|
| Command Center | `/app` | Keep and improve | It remains the chat-first operating entry point. Visible naming is now Command Center/IIP Operations, identity comes from `/api/v1/me`, metrics come from real reports, and shortcuts lead to Review & Approvals and Automation Studio. |
| Claims Intelligence | `/app/claims`, `/app/claims/new`, `/app/claims/[id]` | Keep | Claims are the primary insurer work object. Queue, FNOL, detail, timeline, documents, recommendations, and governed customer-update context remain distinct workflow steps. |
| Extraction Review | `/app/claims/[id]/extraction` | Keep and rebuild | The old hardcoded claim, customer, police report, fields, and fake success actions were removed. The screen now loads real case documents, authorized source content, provider-returned fields/warnings/confidence, persists corrections with an audit reason, and separately records human extraction approval. |
| Channels Inbox | `/app/channels` | Keep with honest limits | It is required for provider-agnostic email/channel intake. Read state remains real; unimplemented create-claim/create-lead/draft/escalate actions are not presented as working. |
| Customer 360 | `/app/customers`, `/app/customers/[id]` | Keep | Customer context is distinct from claim processing and is required for service and acquisition roles. |
| Review & Approvals | `/app/work-items` | Keep and consolidate | One human workspace now contains Prepared work and Consequential approvals. Required audit reasons, evidence/run references, correction/escalation handoffs, approve/reject/escalate, human override, and approved-next-step execution are backed by real endpoints and remain bounded by platform decision ceilings. |
| Documents library | `/app/documents` | Keep and simplify | It remains the cross-claim document index. Unsupported retry/reject buttons were removed; review links open the real extraction workspace. Upload limits match the API contract. |
| Communications | `/app/communications` | Keep as a ledger | It is useful for operational visibility, but fake approve/reject/send/open actions were removed. Governed actions occur in the claim/channel workflow where evidence and approval context exist. |
| Customer Acquisition | `/app/acquisition` | Keep with honest empty state | Sales/distribution is a distinct insurer capability. It remains read-oriented until a real manual-create or ingestion source is available. |
| Policy Intelligence | `/app/policy` | Keep | Grounded policy explanation is a core specialist-agent capability. It remains provider-gated and must not answer from model memory. |
| Risk & Fraud Signals | `/app/fraud` | Keep and rename | The surface is explicitly advisory. “Signals” avoids presenting AI output as a fraud determination or accusation. |
| Underwriting Intelligence | `/app/underwriting` | Keep as advisory | Underwriting support is strategically important, but unsupported draft/escalate actions were removed. It remains a human decision-support view until governed write endpoints exist. |
| Knowledge Hub | `/app/knowledge` | Keep with write controls gated | Approved knowledge is required for policy and staff Q&A. Fake upload/approve/reindex/classify success actions were removed. Retrieval eligibility is no longer inferred when the list contract lacks sensitivity/governance data. |
| Operations Intelligence | `/app/executive` | Keep and clarify | Real operational aggregates remain useful to management. The screen no longer implies unavailable financial/loss measures or decorative executive KPIs. |
| Automation Studio | `/app/flows`, `/app/flows/new`, `/app/flows/[key]` | Keep | This is the primary benchmark adoption: explicit agent roles, tool grants, evidence boundaries, human-review nodes, versioning, publish/start controls, and observable workflow definitions. |
| Run Debugger | `/app/runs/[id]` | Keep | Bounded step status, evidence references, errors, handoffs, and correlation IDs are necessary to operate and evaluate agent workflows without exposing raw prompts or OCR. |
| Integrations & Health | `/app/integrations` | Keep and consolidate | Provider health, capability state, email adapter seams, identity/admin links, and safe diagnostics now live in one place. No fake connection tests, ticket creation, queue depth, or secret-bearing diagnostics remain. |
| Audit & Compliance | `/app/audit` | Keep | Evidence lineage, human/AI actions, provider events, posture, and export controls are non-negotiable for insurers. |
| Admin console | `/app/admin` | Keep and improve | The console is role-filtered and links only to accessible real administration functions. Fake Settings and Support cards were removed; Automation Studio and Insurer Onboarding were added. |
| AI & Agent Management | `/app/admin/agents` | Keep | Registry status and persisted kill switches are required control-plane capabilities. |
| Tenant Agent Policy | `/app/admin/tenant-policy` | Keep | Structured tenant policy can narrow capability without bypassing platform ceilings. |
| Tenant Onboarding | `/app/admin/onboarding` | Keep | Entra tenant identity, Azure subscription targeting, consent, and membership onboarding are distinct enterprise deployment responsibilities. |
| Billing & Usage | `/app/admin/billing` | Keep as read-only | Real usage meters are useful. The fake “request upgrade” success action was removed until a publisher-backed commercial workflow exists. |

## 5. Cross-screen enterprise improvements

### Navigation

The former flat module list was replaced by five job-oriented groups:

- Command
- Operations
- Intelligence
- Automation
- Governance

Platform administration remains separate and role-filtered. This makes the agent/workflow model visible without making every technical route a top-level product.

### Runtime truth

- User name, initials, role, workspace, and environment now come from the resolved runtime context.
- Hardcoded insurer, employee, and production labels were removed from the shell.
- Missing runtime/provider data renders as resolving, unavailable, empty, or `Not configured`; it is not fabricated.

### Human accountability

- Prepared-work acceptance is visibly different from consequential approval.
- Approval, rejection, escalation, override, extraction correction, and extraction approval require a recorded reason.
- AI remains unable to settle, deny, pay, bind, price, accuse fraud, or make another final regulated decision.

### Compatibility and future cleanup

The old `/app/workflows`, `/app/support`, and `/app/settings` paths are retained only for bookmarks and older documentation links. They are excluded from navigation and role module definitions. They can be deleted in a later breaking release after telemetry confirms no active consumers.

## 6. Verification evidence

All required current-state checks passed on 16 July 2026:

| Verification | Result |
|---|---|
| Portal runtime-data, provider-state, role-route, and UI-release guards | Passed |
| TypeScript compile (`pnpm exec tsc --noEmit`) | Passed |
| Portal lint (`pnpm lint`) | Passed with no warnings or errors |
| Production portal build (`NEXT_TELEMETRY_DISABLED=1 pnpm build`) | Passed; all 31 routes generated |
| Responsive configured visual suite | Passed: desktop 24, tablet 23, mobile 24; 71 total with 31 expected Development-profile skips |
| Deployed-profile DOM guard | Passed: 3 tests; 3 deliberate out-of-scope or mobile-only skips |
| Domain tests | Passed: 106 of 106 |
| API tests | Passed: 277 of 277 |
| Live browser smoke | Passed for the canonical Human Review Workspace, resolved development identity, grouped navigation, and real runtime provider state |

The initial full-solution test command exceeded the execution ceiling because its parallel child processes remained active. Those command-owned processes were stopped, and both test projects were then run serially to completion with `--maxcpucount:1`; the results above are the completed test outcomes, not a partial run.
