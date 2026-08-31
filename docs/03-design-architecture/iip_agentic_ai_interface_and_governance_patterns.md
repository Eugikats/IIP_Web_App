# IIP Agentic AI Interface and Governance Patterns

**Status:** Generic pattern library extracted on 2026-08-04; reconciled with IIP Release 1 authority on 2026-08-05.

> **IIP ceiling:** Generic autonomy levels below do not authorize IIP behavior. Release 1 may automate bounded technical preparation, but every consequential insurance outcome and sensitive external action remains subject to the exact permissions, evidence, human-authority, audit and recourse requirements in `../01-strategy-requirements-roadmap/iip_release_1_requirements_baseline.md`. L5 is not approved for IIP Release 1, and no generic pre-authorization may bypass `AGENTS.md`.

**This document owns:** the reusable "governed AI surface" pattern library — autonomy levels, agent cards and agent identity, the copilot panel, the natural-language query bar, inline AI entry points, the agent task queue, the human review cockpit, agent action preview and recovery, the AI governance center, the agent registry, the tool permission matrix, tenant/role/data-scope language, provenance/citations/evidence, untrusted-content and prompt-injection states, monitoring/evaluation/quality dashboards, audit/compliance/decision records, security and responsible-AI guardrails, and print/export/evidence packaging.

**This document must not own:** the visual token system (see the Visual Design System and Design Tokens); IIP-specific routes, roles, and insurance workflows (see the IIP Platform UX Behavior Specification).

**Companion documents:**
- Foundation and governance — `iip_enterprise_agentic_operating_design_system.md`
- Visual design system and tokens — `iip_visual_design_system_and_tokens.md`
- IIP platform UX behavior — `iip_platform_ux_behavior_specification.md`
- Recent Conversations feature — `iip_recent_conversations_session_continuity_spec.md`

---

# 17. Autonomy Levels

Enterprise systems need a shared autonomy language. This prevents confusion about what an agent can and cannot do.

| Level | Name | Agent behavior | Human control | Visual style |
|---|---|---|---|---|
| L0 | Passive | Surfaces information only | None required | Neutral label + text |
| L1 | Advisory | Recommends; human decides | Pre-action | Neutral label + AI provenance icon/text |
| L2 | Assisted | Drafts or prepares action | Human reviews and triggers | Review-required label + AI provenance |
| L3 | Supervised | Executes while human can intervene | During action | Warning label + supervised text |
| L4 | Autonomous | Executes within policy | Post-action audit | Restricted label + audit marker |
| L5 | Restricted autonomous | Executes only whitelisted high-control workflows | Pre-authorization | Critical gate + explicit text |

## Autonomy Display Rule

Every agent action must show its autonomy level.

Example:

```text
L2 Assisted - Agent drafted a customer response. Human approval required before sending.
```

## L5 Rule

L5 is not a default capability. It must require:

- Explicit policy whitelist.
- Senior approval.
- Audit logging.
- Rollback or recovery plan where possible.
- Monitoring.
- Periodic review.

---

# 18. Agent Cards and Agent Identity

Agents should be role-based, not personality-based.

## Agent Card Anatomy

```text
+------------------------------------------------+
| Agent Name                                     |
| Purpose                                        |
| Autonomy level                                 |
| Data scope                                     |
| Tool permissions                               |
| Current status                                 |
| Last run                                       |
| Owner                                          |
+------------------------------------------------+
```

## Agent Identity Rules

- Use functional names: `Document Review Agent`, `Risk Triage Agent`, `Customer Response Agent`.
- Avoid human-like names in high-stakes enterprise contexts.
- Show owner/team responsible for the agent.
- Show if the agent is active, paused, degraded, experimental, or disabled.

## Agent Statuses

| Status | Meaning |
|---|---|
| Active | Agent is available and healthy. |
| Running | Agent is currently processing. |
| Waiting for human | Agent needs review or approval. |
| Paused | Temporarily stopped. |
| Degraded | Running with limitations. |
| Disabled | Unavailable by policy or admin action. |
| Experimental | Limited use; not production-default. |

---

# 19. Copilot Panel

The copilot panel is a contextual enterprise assistant, not a full-screen consumer chat.

## Placement

- Right side of operational screens.
- Width: 360px default; user-resizable from 320px to 440px on wide screens.
- Can be hidden, pinned, or expanded depending on product maturity.
- Should preserve main workspace visibility.

## Header Requirements

The panel header must show:

- Agent name.
- Current context.
- Autonomy level.
- Permission/scope indicator.
- New task or conversation control.

Example:

```text
Operations Copilot | Context: REC-2026-00914 | L1 Advisory | Scope: Current record
```

## Panel States

| State | Meaning |
|---|---|
| Idle | Ready to help in current context. |
| Thinking | Processing. |
| Retrieving | Searching records/evidence. |
| Drafting | Preparing an output. |
| Waiting for approval | Human review required. |
| Restricted | User asked for something outside scope. |
| Error | Task failed. |
| Degraded | Some tools or data unavailable. |

## Copilot Modes

| Mode | Use case |
|---|---|
| Ask | User asks a question. |
| Explain | Agent explains a field, status, or decision. |
| Summarize | Agent summarizes a record or set of records. |
| Compare | Agent compares records, policies, values, or versions. |
| Draft | Agent prepares message, report, or decision note. |
| Recommend | Agent suggests next action. |
| Act | Agent prepares or executes a governed action. |

---

# 20. Natural Language Query Bar

The query bar lets users ask cross-system questions without leaving the workspace.

## Two Invocation Modes

| Mode | Description |
|---|---|
| Persistent bar | Always visible in the top bar or dashboard. |
| Command palette | Opened by keyboard shortcut or button. |

## Query Result Types

| Result type | Display pattern |
|---|---|
| Direct answer | Structured answer with evidence. |
| Record result | Link to matching records. |
| Table result | Small preview with filters. |
| Chart result | Chart with source and period. |
| Action result | Proposed action preview. |
| Restricted result | Boundary explanation. |
| No result | Clear explanation and next options. |

## Query Rules

- Query results must never silently perform consequential actions.
- Cross-module results must show which modules were searched.
- Results must show stale or missing data conditions.
- Search scope must be visible.

Example:

```text
Searched: Records, Evidence, Tasks, Policies
Not searched: Finance data - restricted by your role
```

---

# 21. Inline AI Entry Points

AI should be available where work happens, but it must not dominate the UI.

## Entry Point Types

| Entry point | Location | Use case |
|---|---|---|
| AI button | Page header | Summarize page or create briefing. |
| Row AI marker | Table row | Explain risk or priority. |
| Field suggestion | Form field | Suggest extracted value. |
| Document AI marker | Document viewer | Extract, classify, compare. |
| Context card | Detail view | Summarize record. |
| Bulk AI action | Table toolbar | Analyze selected records. |

## Inline AI Rules

- AI entry points must be secondary to primary human workflow actions.
- AI markers must not replace normal status indicators.
- Consequential AI actions must open action preview or human review cockpit.

---

# 22. Agent Task Queue

Agents often perform background work. The system needs a visible task queue.

## Task Queue Card

```text
Task: Review 47 records for missing evidence
Agent: Document Review Agent
Status: Running
Progress: 21 of 47 checked
Autonomy: L1 Advisory
Started: 08 Jul 2026, 15:10
Actions: View details | Pause | Cancel
```

## Task States

| State | Meaning |
|---|---|
| Queued | Waiting to start. |
| Running | Currently processing. |
| Waiting for human | Human input required. |
| Completed | Finished successfully. |
| Completed with issues | Finished but with warnings. |
| Failed | Could not complete. |
| Cancelled | Stopped by user/system. |
| Paused | Temporarily stopped. |

## Background Completion

When a task finishes, notify the user through:

- Task indicator.
- Notification center.
- Optional toast for low-risk completion.
- Review queue item for consequential outputs.

---

# 23. Human Review Cockpit

The human review cockpit is the primary pattern for regulated or high-impact decisions.

## Cockpit Layout

```text
+-----------------------------+-----------------------------+
| Record Summary              | AI Recommendation            |
| Key fields                  | Confidence / limitations     |
+-----------------------------+-----------------------------+
| Evidence                    | Policy / Rules               |
| Documents, logs, sources    | Applicable checks            |
+-----------------------------+-----------------------------+
| Timeline                    | Human Decision               |
| Events and actors           | Approve / reject / escalate  |
+-----------------------------+-----------------------------+
```

## Required Sections

Every consequential review screen must include:

- Record summary.
- AI recommendation if available.
- Evidence panel.
- Policy/rule checks.
- Timeline.
- Human decision area.
- Reason capture.
- Audit record preview.

## Decision Reason Rule

For consequential decisions, a reason is required when:

- Approving a high-risk item.
- Rejecting a request.
- Overriding AI.
- Overriding policy.
- Escalating.
- Sending customer-facing communication.
- Changing sensitive data.

---

# 24. Agent Action Preview and Recovery

Before any L2-L5 action, the system must show what the agent intends to do.

## Action Preview Anatomy

```text
Agent wants to:
- Create 4 tasks
- Change record status to Needs Review
- Draft a message
- Notify a supervisor

Data affected:
- Record status
- Task queue
- Communication log

Human approval required:
- Yes

Recovery:
- Status can be reverted
- Tasks can be cancelled
- Sent message cannot be unsent
```

## Action Risk Levels

| Risk | Examples | Required control |
|---|---|---|
| Low | Summarize, draft, classify as tentative | Normal confirmation |
| Medium | Create tasks, route records, change non-final status | Action preview |
| High | Approve, reject, send, pay, lock, delete | Human review cockpit |
| Critical | Autonomous high-impact execution | Senior approval + policy whitelist |

## Undo and Recovery

For every agent action, define:

- Can it be undone?
- Who can undo it?
- How long is undo available?
- What audit record remains?
- What downstream systems were affected?

If an action cannot be undone, the preview must state this explicitly.

---

# 25. AI Governance Center

The AI Governance Center is a top-level workspace for administrators, compliance teams, risk teams, and platform owners.

## Purpose

The Governance Center makes the AI operating model visible and manageable.

## Core Screens

| Screen | Purpose |
|---|---|
| Agent overview | View all agents, status, owners, autonomy levels. |
| Risk register | Track known AI risks, mitigations, owners, severity. |
| Policy controls | Configure what agents can and cannot do. |
| Evaluation dashboard | Review accuracy, quality, drift, and regression. |
| Incident log | Track prompt injection, unsafe output, tool failures. |
| Override analytics | See how often humans change AI outputs. |
| Model and prompt versions | Track deployed versions and change history. |
| Access and scope | Review data/tool permissions. |
| Audit exports | Generate governance evidence packs. |

## Governance Center Metrics

| Metric | Why it matters |
|---|---|
| Human override rate | Shows whether AI recommendations are trusted. |
| Low-confidence output rate | Indicates quality/data problems. |
| Citation coverage | Shows grounding quality. |
| Tool-call failure rate | Measures operational reliability. |
| Restricted request attempts | Reveals training, permission, or threat issues. |
| Prompt injection detections | Tracks adversarial or untrusted inputs. |
| Evaluation pass rate | Shows release readiness. |
| Model/prompt regression | Prevents quality degradation. |

---

# 26. Agent Registry

The Agent Registry is the system of record for agent configuration and accountability.

## Agent Registry Fields

| Field | Description |
|---|---|
| Agent name | Functional name. |
| Purpose | What the agent is designed to do. |
| Owner | Business or technical owner. |
| Status | Active, paused, disabled, experimental. |
| Autonomy level | Highest permitted autonomy. |
| Data sources | Approved data sources. |
| Tools | Approved tools/actions. |
| Output types | Summary, draft, classification, recommendation, action. |
| Human review policy | When review is required. |
| Evaluation suite | Tests required before release. |
| Version | Agent, model, prompt, retrieval, policy version. |
| Last reviewed | Governance review date. |

## Agent Registry UI Rules

- No production agent should be ownerless.
- No agent should have tool access without a documented purpose.
- Experimental agents must be visually marked.
- Disabled agents must show reason and date.
- Registry changes must be auditable.

---

# 27. Tool Permission Matrix

Agentic systems become risky when agents can use tools without clear limits. The permission matrix is mandatory.

## Matrix Structure

| Agent | Read data | Write data | External message | Workflow trigger | Approval required |
|---|---|---|---|---|---|
| Summary Agent | Current record | No | No | No | No |
| Document Agent | Evidence docs | Extracted draft fields | No | Create checklist | For medium confidence |
| Response Agent | Record + party profile | Draft only | After approval | No | Always |
| Routing Agent | Queue metadata | Status changes | No | Route record | Medium/high risk |
| Governance Agent | Logs + evaluations | Governance notes | No | Create incident | Admin approval |

## Permission Levels

| Level | Meaning |
|---|---|
| None | Agent cannot access/use. |
| Read | Agent can view. |
| Draft | Agent can prepare but not commit. |
| Write with approval | Agent can write after human approval. |
| Write within policy | Agent can write under predefined policy. |
| Restricted | Requires elevated approval and logging. |

## Tool Call Display

When an agent uses a tool, show:

```text
Tool used: Search Records
Scope: Current tenant / Operations module
Result: 38 records returned
Permission: Read-only
```

---

# 28. Tenant, Role, and Data Scope Language

Enterprise users must always know what boundary they are operating inside.

## Scope Banner

Use scope banners in sensitive or multi-tenant systems.

```text
Tenant: Organization A
Role: Review Officer
Data scope: Operations records only
Environment: Production
Agent scope: Advisory only
```

## Environment Indicator

Non-production environments must be visually obvious.

| Environment | Visual treatment |
|---|---|
| Production | Minimal text indicator. |
| Staging | Amber top-bar label. |
| Development | Blue-grey top-bar label. |
| LocalTest | Violet-outline label; never customer-facing or deployable. |
| ManagedApplicationReviewer | Amber reviewer label plus deployment identity. |

IIP uses only `LocalTest`, `Development`, `Staging`, `Production`, and `ManagedApplicationReviewer`. Do not expose `Sandbox`, `Demo`, or simulated runtime choices in deployed or customer-facing environments. A domain extension may define other environments only when its runtime contract explicitly permits them.

## Restricted Access Response

When access is restricted, explain clearly:

```text
You do not have access to this data.
Reason: Restricted to Finance Admin role.
What you can do: Request access or ask a scoped question using the current record only.
```

Never expose hidden data through error messages or AI summaries.

---

# 29. Provenance, Citations, and Evidence

Provenance is the backbone of enterprise AI trust.

## Evidence Requirements

Every consequential AI output must show:

- Source records.
- Source documents.
- Data period.
- Retrieval time.
- Version of policy/rules used.
- Model and prompt version where relevant.
- Missing data or excluded sources.

## Evidence Card

```text
Evidence
- Record REC-2026-00914, updated 08 Jul 2026
- Document DOC-4412, page 2
- Policy POL-118, version 3.2
- Transaction set: 847 records, Apr-Jun 2026
```

## Source Strength Labels

| Label | Meaning |
|---|---|
| Direct source | Primary record or document. |
| Derived | Calculated from underlying sources. |
| Inferred | AI inferred from available evidence. |
| Unverified | Evidence incomplete or source not confirmed. |
| Stale | Source data older than freshness threshold. |

## Citation Placement

- Do not hide all citations at the bottom.
- Place evidence near the claim it supports.
- Allow expansion into full source view.
- Support export of sources with decision record.

---

# 30. Untrusted Content and Prompt Injection States

Enterprise agents often process documents, emails, chat messages, web pages, forms, and files. These may contain malicious or manipulative instructions.

## Untrusted Content Warning

Show this when AI reads content that may contain instructions not authored by the enterprise system.

```text
Untrusted content detected
This document may contain instructions intended to manipulate AI behavior. The agent will treat the document as data only and ignore embedded instructions.
```

## Prompt Injection Risk States

| State | Meaning | UI action |
|---|---|---|
| None detected | No suspicious instruction pattern found. | Continue normally. |
| Possible injection | Suspicious embedded instruction found. | Warn user; restrict tool use. |
| Confirmed injection | Malicious instruction detected. | Block action; create incident. |
| Tool-risk escalation | Prompt asks agent to use tools unsafely. | Require review; disable tool call. |

## Design Rules

- Treat external content as data, not instruction.
- Show when the agent has ignored embedded instructions.
- Restrict tool use when untrusted content is involved.
- Log confirmed attacks.
- Allow security teams to review examples.

---

# 31. Monitoring, Evaluation, and Quality Dashboards

Confidence indicators are not enough. Enterprise AI needs continuous measurement.

## Evaluation Dashboard

Track:

| Metric | Purpose |
|---|---|
| Accuracy | Correctness against labeled data. |
| Extraction accuracy | Correct field extraction from documents. |
| Classification precision/recall | Quality of category/status/risk classification. |
| Hallucination rate | Unsupported claims. |
| Citation coverage | Outputs with usable sources. |
| Human correction rate | How often users fix AI. |
| Human rejection rate | How often users reject AI. |
| Low-confidence rate | Output quality issues. |
| Latency | Operational speed. |
| Cost per workflow | Financial sustainability. |
| Tool failure rate | Reliability of integrated actions. |
| Drift indicators | Degradation over time. |

## Release Gate

Before an agent is promoted to production:

- Evaluation suite must pass.
- High-risk failure cases must be reviewed.
- Prompt/model/retrieval versions must be documented.
- Rollback plan must exist.
- Owner must approve.
- Governance log must be updated.

---

# 32. Audit, Compliance, and Decision Records

## Activity Timeline

Every important record should have a timeline showing:

- User actions.
- Agent actions.
- Status changes.
- Evidence uploads.
- Comments.
- Approvals/rejections.
- External system syncs.
- Errors and retries.

## Decision Record

For consequential decisions, create a structured decision record.

```text
Decision: Approved
Actor: Jane Doe
Role: Senior Reviewer
AI recommendation: Approve with conditions
Human action: Approved
Reason: Evidence complete and policy checks passed
Sources: DOC-4412, REC-00914, POL-118
Timestamp: 2026-07-08T15:42:31+03:00
```

## Audit Export

Audit export must include:

- Record details.
- Decision history.
- Evidence list.
- AI outputs.
- Human overrides.
- Agent/tool logs.
- Timestamps.
- User identities/roles.
- Filters applied.
- Export generator and timestamp.

---

# 33. Security and Responsible AI Guardrails

## Security Design Requirements

Agentic systems must design for:

- Least privilege.
- Tool-call authorization.
- Data minimization.
- Output validation.
- Sensitive data protection.
- Rate limiting and abuse prevention.
- Prompt injection handling.
- Secure audit logs.
- Incident response.
- Human approval for high-impact actions.

## Responsible AI Requirements

The interface must support:

| Principle | UI/design implication |
|---|---|
| Fairness | Show data limitations and bias-sensitive fields. |
| Reliability and safety | Show confidence, validation, and review gates. |
| Privacy and security | Respect role/data scope and redact sensitive data. |
| Inclusiveness | Meet accessibility standards and plain-language guidance. |
| Transparency | Show provenance, agent identity, and limitations. |
| Accountability | Capture decision records, owners, and audit trails. |

## Sensitive Output Handling

When AI outputs sensitive information:

- Mask where appropriate.
- Require role permission to reveal.
- Log reveal actions.
- Prevent inclusion in exports unless allowed.
- Avoid sending sensitive content externally without approval.

---

# 35. Print, Export, and Evidence Packaging

Enterprise systems generate evidence, not just screens.

## Print Rules

- Print on white background.
- Hide navigation and interactive controls.
- Include title, timestamp, user, role, tenant, and filter criteria.
- Keep tables readable.
- Use page breaks for major sections.
- Include source/evidence appendix where relevant.

## Export Types

| Export | Requirements |
|---|---|
| PDF | White background, headers, evidence appendix. |
| CSV | UTF-8, ISO dates, raw data, no hidden formatting. |
| Excel | Right-aligned numerics, date formats, filters. |
| Audit pack | Decision record + evidence + logs. |
| Board pack | Executive summary + charts + narrative. |

## Export Metadata

Every export must include:

- System name.
- Generated by.
- Generated at.
- Tenant/organization.
- Filters applied.
- Data freshness.
- Source modules.
- Confidentiality label.

---
