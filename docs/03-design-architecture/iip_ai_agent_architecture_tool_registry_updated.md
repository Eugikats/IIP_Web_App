# AI Agent Architecture & Tool Registry

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | AI Agent Architecture & Tool Registry |
| Version | 2.0 |
| Status | Updated canonical draft |
| Audience | AI engineers, backend engineers, architects, compliance leads, QA leads, coding agents |
| Date | July 2026 |

---

## July 2026 Canonical Agentic Update

```text
1. Consolidated the earlier Command Center Agent update and the original 8-agent registry into one canonical registry.
2. Set the official IIP AI architecture to 10 canonical agents.
3. Added Underwriting Intelligence Agent as a formal IIP agent.
4. Renamed Risk Signal Agent to Risk & Fraud Signal Agent so fraud capability is explicit but advisory.
5. Kept Command Center Agent as one of the 10 canonical agents because it is the chat-first operational orchestration agent.
6. Expanded the tool registry so underwriting, fraud/risk, reporting, notification drafting, knowledge retrieval, and command workflows are explicitly governed.
7. Clarified that no agent may approve/reject claims, bind policies, set final premiums, accuse fraud, release payment, send sensitive communications, or execute consequential decisions without the approved human workflow.
8. Added a future split note: if Fraud Signal becomes a separate specialist agent later, the official count must be updated from 10 to 11 by a new architecture revision.
```

---

## 1. Purpose

This document defines the canonical AI-agent architecture for the Insurance Intelligence Platform (IIP). It specifies every approved IIP AI agent, the allowed tools each agent may use, permission and tenant boundaries, input/output expectations, grounding rules, failure behavior, human-review gates, and audit requirements.

IIP is intended to be **agentic by architecture**, not merely a traditional insurance application with a chatbot attached. The system uses specialized agents, typed backend tools, evidence grounding, tenant/workspace context, provider health checks, and human-review controls to support insurance operations safely.

This document uses the existing IIP project context and documentation pack: Master Product Context, PRD, Functional Specification, Technical Architecture Document, System Integration Strategy, UI/UX Handoff, API Specification, Data Model, Security & Compliance Document, QA/Test Plan, Deployment Plan, User/Admin Guide, Implementation Roadmap, and Strategy/Requirements/Roadmap Pack.

---

## 2. Confirmed project baseline

```text
- Official product name: Insurance Intelligence Platform (IIP).
- Staff application name: IIP Operations.
- IIP is an Azure-native, human-reviewed insurance operations platform.
- IIP is not a replacement for core insurance systems.
- MVP focus: motor claims and document intelligence for a mid-to-large insurer pilot.
- Customer engagement is WhatsApp-first and channel-led; customers do not log into the internal staff application.
- Internal modules include Command Center, Channels, Customers, Sales & Leads, Claims, Documents, Knowledge Base, Approvals, Compliance & Audit, Reports & Analytics, Integrations, Admin Settings, and Support & Health.
- The internal UI is a role-aware staff workspace with persistent navigation, top bar, global search, operational states, and contextual AI assistance.
- The platform uses Next.js, React, TypeScript, and Tailwind on the frontend and .NET 8 / ASP.NET Core on the backend.
- Azure services include Azure SQL, Blob Storage, Key Vault, Azure AI Search, Azure OpenAI, Azure AI Document Intelligence, Service Bus/Event Grid, Functions/Durable Functions, API Management, Application Insights/Log Analytics, Entra ID/MSAL, and optional Managed Application packaging.
- AI assists with extraction, summarization, classification, routing, drafting, risk/fraud signals, underwriting intelligence, and evidence preparation.
- Humans make regulated, consequential, legal, claims, underwriting, settlement, payment, and customer-communication decisions.
- Staging and Production must not use fake operational data. Empty states, error states, and Not configured states must be real and explicit.
```

---

## 3. Agent architecture decision

### 3.1 Canonical count

The canonical IIP agent architecture contains **10 AI agents**:

```text
1. Command Center Agent
2. Claims Summary Agent
3. Document Checker Agent
4. Policy Intelligence Agent
5. Risk & Fraud Signal Agent
6. Customer Update Drafting Agent
7. Routing Agent
8. Knowledge Assistant
9. Report Insight Agent
10. Underwriting Intelligence Agent
```

### 3.2 Count note

The earlier architecture listed 8 agents and later added a Command Center Agent separately. This updated document resolves that inconsistency by making the Command Center Agent part of the canonical 10-agent model and combining risk and fraud into one advisory agent for the MVP/Phase 1 architecture.

If the fraud capability is later split into a separate specialist **Fraud Signal Agent**, the architecture must be updated from **10 agents** to **11 agents** by a separate approved revision.

### 3.3 Agentic operating model

```text
- Agents do not directly own final authority.
- Agents operate through scoped typed tools, not raw database access.
- Agents receive tenant/workspace context from the backend request pipeline.
- Agents must respect role/permission limits.
- Agents must cite or reference evidence where operational facts are used.
- Agents must create AgentRun and AIInteraction records.
- Sensitive or consequential outputs must create or link to AuditEvent records.
- Agents must fail closed when a tool, provider, permission, or source is unavailable.
```

---

## 4. AI governance boundary

| AI may do | AI must not do |
|---|---|
| Summarize claim records, documents, timelines, conversations, and operational metrics | Approve or reject a claim |
| Extract fields and flag missing information | Deny, repudiate, settle, or release payment |
| Explain relevant policy clauses and underwriting guidelines | Bind a policy or issue final cover |
| Draft customer updates for human review | Set final premium, final underwriting terms, exclusions, or loadings |
| Identify inconsistencies, risk signals, duplicate patterns, and fraud indicators | Accuse any person, customer, provider, repairer, hospital, broker, or organization of fraud |
| Prepare evidence packs and audit notes | Make final legal, regulatory, claims, underwriting, or fraud decisions |
| Recommend routing, escalation, referral, or further checks | Send sensitive customer communications without an approved human send workflow |
| Prepare report summaries and management insights from real data | Fabricate operational metrics, claims, SLA values, provider status, or document evidence |

Required phrasing for fraud/risk outputs:

```text
Allowed: "This claim has fraud-risk indicators requiring human review."
Forbidden: "This claim is fraudulent."
```

Required phrasing for underwriting outputs:

```text
Allowed: "Refer to a human underwriter due to missing inspection and prior claims frequency."
Forbidden: "Policy approved", "Policy declined", "Premium set", or "Cover bound".
```

---

## 5. Canonical agent registry

| # | Agent | Purpose | Allowed inputs | Allowed tools | Output | Human gate |
|---:|---|---|---|---|---|---|
| 1 | Command Center Agent | Primary chat-first operational orchestration agent for briefings, triage, SLA review, metric explanation, action preparation, and report drafting. | User command, tenant/workspace context, user role/permissions, operational records, approved knowledge, provider health. | `command.briefing.read`, `metrics.read`, `claims.attention.read`, `claims.sla.read`, `documents.gaps.read`, `approvals.queue.read`, `knowledge.search`, `customer_update.draft`, `report.draft`, `provider_health.read`, `command.action.prepare`. | Structured command response blocks with evidence, confidence, and proposed actions. | Required for customer communications, consequential workflow changes, claim decisions, exports, and sensitive actions. |
| 2 | Claims Summary Agent | Summarize claim facts, timeline, open issues, documents, conversations, and policy context. | Claim record, documents, conversations, policy reference, audit timeline. | `claim.read`, `document.read`, `conversation.read`, `policy.read`, `knowledge.search`, `audit.read`. | Claim summary with sources, open questions, and confidence. | Required before decision use. |
| 3 | Document Checker Agent | Check required documents, extraction quality, completeness, confidence, and missing document gaps. | Document metadata, extraction fields, claim type checklist, product line rules. | `document.read`, `document.extraction.read`, `documents.gaps.read`, `checklist.read`, `provider_health.read`. | Missing/low-confidence document report. | Human reviewer confirms extracted data and completeness decisions. |
| 4 | Policy Intelligence Agent | Explain relevant policy clauses, coverage context, exclusions, endorsements, limits, and policy wording. | Policy reference, approved policy wording, endorsements, product rules, approved knowledge base. | `policy.read`, `knowledge.search`, `customer.policy.read`. | Clause explanation with citations and uncertainty notes. | Claims officer, underwriter, or supervisor reviews before decision use. |
| 5 | Risk & Fraud Signal Agent | Flag inconsistencies, duplicate patterns, suspicious timing, provider/customer anomalies, and fraud-risk indicators without making accusations. | Claim, documents, customer/channel history, prior claims, provider/repairer patterns, audit history. | `claim.read`, `document.read`, `customer.history.read`, `audit.read`, `risk.rules.read`, `fraud.signals.read`, `knowledge.search`. | Risk/fraud signal memo, scored indicators, and recommended human investigation steps. | Supervisor, SIU/compliance, or claims manager review required. |
| 6 | Customer Update Drafting Agent | Draft customer-safe messages from approved templates and claim context. | Claim status, missing document list, approved communication templates, channel context. | `template.read`, `conversation.read`, `claim.read`, `customer_update.draft`, `approval.create`. | Draft customer message with tone/safety notes. | Human approval and send workflow required. |
| 7 | Routing Agent | Recommend assignment, escalation, referral, workload routing, and SLA-based prioritization. | Claim metadata, SLA status, workload, role rules, complexity, risk signals. | `claim.read`, `claims.sla.read`, `workload.read`, `routing.rules.read`, `task.recommend`, `command.action.prepare`. | Routing or escalation recommendation. | Human or approved automation rule confirms. |
| 8 | Knowledge Assistant | Answer staff questions from approved knowledge, SOPs, policies, underwriting guidelines, and internal documentation. | Approved knowledge base, SOPs, product docs, policy docs, underwriting manuals. | `knowledge.search`, `policy.read`, `sop.read`, `provider_health.read`. | Cited answer with source IDs and confidence. | User reviews before operational reliance. |
| 9 | Report Insight Agent | Summarize operational metrics, report snapshots, trends, partial-period notes, and management insights. | Reports, claims metrics, SLA metrics, audit aggregates, approval turnaround, document completeness. | `report.read`, `metrics.read`, `audit.read`, `report.draft`. | Management summary with real metric sources and partial/final labels. | Manager reviews before export, circulation, or board/executive use. |
| 10 | Underwriting Intelligence Agent | Assist underwriters with proposal review, eligibility checks, missing information, claims-history review, referral notes, and underwriting guideline interpretation. | Proposal/application data, customer profile, claims history, inspection reports, documents, underwriting guidelines, policy/product rules. | `underwriting.case.read`, `customer.profile.read`, `customer.history.read`, `document.read`, `policy.read`, `knowledge.search`, `underwriting.risk.evaluate`, `underwriting.referral.prepare`, `approval.create`. | Underwriting review note, missing-info list, referral recommendation, and evidence summary. | Human underwriter makes final accept/decline/terms/premium/bind decision. |

---

## 6. Agent detail cards

### 6.1 Command Center Agent

```text
Role:
- Conversational operational interface for the Command Center.
- Coordinates read-only operational tools and drafts safe next actions.
- May call specialist tools or prepare task proposals, but must not execute final actions.

Primary outputs:
- Briefing blocks.
- Claims attention tables.
- SLA summaries.
- Evidence lists.
- Action proposals.
- Draft customer updates.
- Draft report summaries.

Hard limits:
- Must not approve, reject, bind, settle, send, pay, or accuse fraud.
- Must not bypass role/tenant/workspace restrictions.
```

### 6.2 Claims Summary Agent

```text
Role:
- Creates grounded claim summaries for staff review.
- Extracts key facts from claim detail, documents, communications, timelines, and policy context.

Primary outputs:
- Claim summary.
- Timeline summary.
- Open issue list.
- Evidence references.
- Low-confidence warnings.
```

### 6.3 Document Checker Agent

```text
Role:
- Reviews documents and extraction outputs.
- Identifies missing required documents and low-confidence fields.

Primary outputs:
- Missing document report.
- Extraction confidence notes.
- Human review checklist.
```

### 6.4 Policy Intelligence Agent

```text
Role:
- Explains policy clauses and coverage context from approved knowledge and policy records.

Primary outputs:
- Clause explanation.
- Coverage context.
- Exclusion/limit notes.
- Source citations.

Hard limits:
- Must not make final coverage decisions.
```

### 6.5 Risk & Fraud Signal Agent

```text
Role:
- Detects advisory risk and fraud signals.
- Surfaces inconsistencies, anomalies, duplicate patterns, unusual provider/customer behavior, and investigation recommendations.

Primary outputs:
- Risk/fraud signal memo.
- Signal list.
- Confidence level.
- Recommended human review steps.

Hard limits:
- Must not accuse fraud.
- Must not reject or deny a claim.
- Must not make SIU/fraud final determinations.
```

Example output wording:

```text
Fraud-risk level: Medium

Signals detected:
- Claim submitted shortly after policy inception.
- Incident date differs between FNOL and supporting document.
- Repair invoice pattern appears similar to prior flagged claims.

Recommended human action:
- Route to supervisor/SIU review.
- Request original repair assessment.
- Compare with prior provider history.

Boundary:
- This is not a fraud determination.
```

### 6.6 Customer Update Drafting Agent

```text
Role:
- Drafts customer-safe communications using approved templates and claim context.

Primary outputs:
- Draft message.
- Required human review note.
- Missing data warning if relevant.

Hard limits:
- Must not send messages directly.
- Must not promise settlement or outcome.
- Must not disclose hidden risk/fraud/internal notes to customers.
```

### 6.7 Routing Agent

```text
Role:
- Recommends assignment, escalation, and routing based on SLA, workload, risk, claim complexity, and role rules.

Primary outputs:
- Assignment recommendation.
- Escalation recommendation.
- Reason codes.

Hard limits:
- Must not override server-side permissions or business rules.
```

### 6.8 Knowledge Assistant

```text
Role:
- Answers staff questions from approved knowledge sources.

Primary outputs:
- Cited answer.
- Relevant source list.
- Confidence level.

Hard limits:
- Must not answer from unapproved or customer-confidential sources.
```

### 6.9 Report Insight Agent

```text
Role:
- Explains operational metrics and report snapshots.

Primary outputs:
- Management summary.
- Metric explanations.
- Partial-period warnings.
- Trend commentary based on real data only.

Hard limits:
- Must not estimate or smooth operational figures and present them as real.
```

### 6.10 Underwriting Intelligence Agent

```text
Role:
- Assists underwriting review without making final underwriting decisions.
- Reviews proposal data, policy/product rules, customer profile, claims history, missing documents, inspection reports, and underwriting guidelines.

Primary outputs:
- Underwriting review note.
- Missing information checklist.
- Referral recommendation.
- Risk factor summary.
- Evidence list.

Hard limits:
- Must not approve or decline risk.
- Must not bind cover.
- Must not set final premium, exclusions, limits, or loading.
```

Example output wording:

```text
Underwriting review result: Refer to human underwriter

Reasons:
- Vehicle age exceeds standard acceptance threshold.
- Inspection report is missing.
- Prior claim frequency is above normal for this product line.

Recommended human action:
- Request inspection report.
- Review claims history.
- Confirm payment before binding.

Boundary:
- This is not an underwriting decision.
```

---

## 7. Tool registry schema

| Field | Description | Required |
|---|---|---|
| tool_name | Stable tool identifier | Yes |
| description | Business purpose and constraints | Yes |
| allowed_agents | Agents that may call the tool | Yes |
| required_permissions | RBAC permissions required | Yes |
| input_schema | Validated request schema | Yes |
| output_schema | Structured response schema | Yes |
| tenant_scope_required | Whether TenantId/WorkspaceId is required | Yes |
| audit_required | Whether tool calls create AuditEvent/AgentRun record | Yes |
| side_effect_level | read, draft, write, external_send, approval_required | Yes |
| provider_health_check | Provider code to check before use, if any | Conditional |
| failure_behavior | How agent should behave on failure | Yes |

---

## 8. Side-effect levels

| Side-effect level | Meaning | Human gate |
|---|---|---|
| read | Reads tenant/workspace-scoped data only | User review |
| draft | Creates a draft/proposal only | Human review before execution |
| write | Writes internal system state | Permission and audit required; human gate if consequential |
| external_send | Sends to customer/external system | Recorded human approval required |
| approval_required | Creates or depends on approval workflow | ApprovalDecisionService / human decision required |

Agents should prefer `read` and `draft` tools. `write`, `external_send`, and `approval_required` paths must be mediated by backend services, permissions, audit, and human decision gates.

---

## 9. Canonical tool registry

| Tool | Side effect level | Allowed agents | Required permissions | Provider health | Guardrail |
|---|---|---|---|---|---|
| `command.briefing.read` | read | Command Center Agent | `command_center.query` | None | Real backend values only; no hardcoded briefing. |
| `metrics.read` | read | Command Center Agent, Report Insight Agent | `reports.read` or module-specific read permission | None | Tenant/workspace scoped; include source labels and generated timestamp. |
| `claim.read` | read | Claims Summary Agent, Risk & Fraud Signal Agent, Routing Agent, Customer Update Drafting Agent | `claims.read` | None | Tenant/workspace filter mandatory. |
| `claims.attention.read` | read | Command Center Agent, Claims Summary Agent, Routing Agent | `claims.read` | None | Applies role, assignment, branch/product filters. |
| `claims.sla.read` | read | Command Center Agent, Routing Agent, Report Insight Agent | `claims.read` | None | SLA values must be backend-calculated. |
| `document.read` | read | Claims Summary Agent, Document Checker Agent, Risk & Fraud Signal Agent, Underwriting Intelligence Agent | `documents.read` or `documents.review` | None | No cross-claim access unless authorized. |
| `document.extraction.read` | read | Document Checker Agent, Claims Summary Agent | `documents.review` | None | Must expose confidence and source document reference. |
| `documents.gaps.read` | read | Command Center Agent, Document Checker Agent | `documents.review` | None | Uses configured claim-type/product checklist only. |
| `policy.read` | read | Policy Intelligence Agent, Claims Summary Agent, Underwriting Intelligence Agent | `claims.read` or underwriting/policy permission when added | None | Do not infer policy terms from memory. |
| `knowledge.search` | read | Command Center Agent, Policy Intelligence Agent, Knowledge Assistant, Claims Summary Agent, Underwriting Intelligence Agent | `command_center.query` or knowledge permission where configured | `azure_ai_search` | Must return citations and source IDs. |
| `customer.profile.read` | read | Underwriting Intelligence Agent, Risk & Fraud Signal Agent, Customer Update Drafting Agent | `customers.read` | None | Customer PII must remain tenant/workspace scoped. |
| `customer.history.read` | read | Risk & Fraud Signal Agent, Underwriting Intelligence Agent | `customers.read`, `claims.read` | None | Use only approved history visible to the user/role. |
| `conversation.read` | read | Claims Summary Agent, Customer Update Drafting Agent | `channels.read` | None | Do not expose internal notes to customers. |
| `audit.read` | read | Risk & Fraud Signal Agent, Report Insight Agent, Claims Summary Agent | `audit.read` | None | Sensitive; scope by tenant/workspace/entity. |
| `approvals.queue.read` | read | Command Center Agent | `approvals.read` | None | Only returns approvals visible to current user/role. |
| `risk.rules.read` | read | Risk & Fraud Signal Agent, Underwriting Intelligence Agent | `claims.read` or future `risk.read` | None | Rules are advisory and must be versioned. |
| `fraud.signals.read` | read | Risk & Fraud Signal Agent | `claims.read` plus future `fraud.signals.read` when added | None | Output fraud-risk indicators only; no accusation. |
| `template.read` | read | Customer Update Drafting Agent | `channels.read` or future `templates.read` | None | Only approved templates. |
| `customer_update.draft` | draft | Command Center Agent, Customer Update Drafting Agent | `command_center.query` or future `communications.draft` | None | Draft only; human approval/send workflow required. |
| `message.draft` | draft | Customer Update Drafting Agent | future `communications.draft` | None | Draft only; no direct send. |
| `task.recommend` | draft | Routing Agent, Command Center Agent | `claims.read` | None | No direct assignment unless approved business rule executes it. |
| `command.action.prepare` | draft | Command Center Agent, Routing Agent | `command_center.query` | None | Prepares action proposal only. |
| `approval.create` | write / approval_required | Customer Update Drafting Agent, Underwriting Intelligence Agent, Command Center Agent | `approvals.review` or module-specific approval permission | None | Creates approval task only; no self-approval. |
| `report.read` | read | Report Insight Agent, Command Center Agent | `reports.read` | None | Real report snapshots and aggregates only. |
| `report.draft` | draft | Command Center Agent, Report Insight Agent | `reports.read` | None | Draft management summary; user reviews before export/send. |
| `underwriting.case.read` | read | Underwriting Intelligence Agent | future `underwriting.read` | None | Proposal and risk data scoped to tenant/workspace. |
| `underwriting.risk.evaluate` | draft | Underwriting Intelligence Agent | future `underwriting.assist` | None | Advisory assessment only; no binding or premium setting. |
| `underwriting.referral.prepare` | draft | Underwriting Intelligence Agent | future `underwriting.assist` | None | Prepares referral note for human underwriter. |
| `provider_health.read` | read | Command Center Agent, Document Checker Agent, Knowledge Assistant, Report Insight Agent | admin/provider visibility as configured | Provider-specific | Shows connected/degraded/not_configured/error; no secrets. |
| `audit.write` | write | All agents via backend only | server-side only | None | Every agent run must be traceable. |

---

## 10. Command Center response contract

The Command Center Agent must return structured blocks, not only free text.

```json
{
  "answerText": "Here are the SLA breaches as of now.",
  "blocks": [
    { "type": "metric_pills", "items": [] },
    { "type": "sla_summary", "items": [] },
    { "type": "claims_table", "rows": [] },
    { "type": "evidence_list", "sources": [] },
    { "type": "action_proposal", "actions": [] }
  ],
  "requiresHumanReview": true,
  "confidence": "high",
  "evidenceIds": [],
  "auditRequired": true
}
```

Required response-block rules:

```text
- Blocks must be typed and renderable by the frontend.
- Evidence-bearing answers must include evidenceIds/source refs.
- Proposed actions must be clearly marked as proposals, not executed outcomes.
- If a tool is unavailable or not configured, the block must show degraded/not_configured state.
- Metrics must include generatedAt/source labels where available.
```

---

## 11. Agent run, interaction, and audit records

| Record | Required fields |
|---|---|
| AgentRun | AgentRunId, TenantId, WorkspaceId, AgentName, TriggerType, InputRefs, Status, StartedAt, CompletedAt, ErrorCategory, CorrelationId |
| AIInteraction | AIInteractionId, AgentRunId, PromptVersion, ModelProvider, ModelName, OutputType, OutputJson, Confidence, SourceRefs, HumanReviewStatus |
| AuditEvent | Actor, Action, EntityType, EntityId, Before/After where appropriate, Timestamp, CorrelationId, Agent/User classification |

Required behavior:

```text
- Every agent execution creates an AgentRun.
- Every model interaction creates an AIInteraction linked to the AgentRun.
- Every sensitive output, draft, proposal, export preparation, customer communication draft, risk/fraud signal, or underwriting recommendation must be auditable.
- Audit summaries must not contain raw prompts, hidden system instructions, raw provider payloads, credentials, secrets, SAS tokens, or unnecessary PII.
```

---

## 12. Failure behavior

| Failure type | Required behavior |
|---|---|
| Missing source data | Say what is missing; do not infer or invent. |
| Low confidence | Mark low confidence and route to human review. |
| Prompt injection attempt | Block or ignore malicious instruction and audit the event. |
| Tool unavailable | Return degraded/not_configured message; do not fabricate output. |
| Provider unavailable | Surface provider_unavailable/not_configured through safe response body. |
| Unsafe decision request | Refuse final decision and provide evidence checklist for human reviewer. |
| Cross-tenant or unauthorized access attempt | Refuse and rely on backend permission/tenant enforcement. |
| Unapproved knowledge source | Do not use the source; state that approved evidence is unavailable. |

---

## 13. Prohibited agent actions

No IIP agent may:

```text
- Approve or reject a claim.
- Deny, repudiate, settle, or release payment.
- Bind a policy.
- Set final premium, loading, exclusion, final underwriting terms, or coverage decision.
- Accuse a person, company, provider, customer, broker, repairer, or hospital of fraud.
- Send sensitive customer communication without a recorded human approval/send workflow.
- Execute external write-back without the approved integration write-back service and permissions.
- Expose hidden prompts or raw tool outputs.
- Bypass tenant, workspace, role, assignment, or sensitivity filters.
- Fabricate metrics, claims, documents, SLA values, provider health, or knowledge citations.
- Use customer-confidential, staff-only, or secret/PII-heavy material for retrieval unless explicitly approved for AI retrieval.
```

---

## 14. Human-review gates by area

| Area | Human gate |
|---|---|
| Claims decision | ApprovalDecisionService and recorded human decision required. |
| Claim approval/rejection/settlement/payment | Always human or approved regulated workflow; no AI-only path. |
| Fraud/SIU action | Human supervisor/SIU/compliance review required. |
| Underwriting accept/decline/terms/premium/bind | Human underwriter or approved rules engine; no AI-only path. |
| Customer communication send | Draft approval and NotificationSendService human-approval verification required. |
| External write-back | Integration Write-Back Execution Service and permissions required. |
| Report export/circulation | reports.export permission and audit required. |
| Knowledge ingestion | Sensitivity gate and knowledge.manage permission required. |

---

## 15. Phase 1-4 agent alignment

| Phase | Agent work |
|---|---|
| Phase 1 | Define agent contracts, response block schemas, core tool registry, AgentRun/AIInteraction records, and UI rendering. Implement Command Center, Claims Summary, Document Checker, Policy Intelligence, Customer Update Drafting, Knowledge Assistant. |
| Phase 2 | Add read-only real tools for briefing, metrics, claims attention, SLA, documents, approvals, provider health, customer profile, claims history, and audit access. Add Risk & Fraud Signal and Underwriting Intelligence in advisory mode. |
| Phase 3 | Add RAG-backed synthesis, customer-update drafts, report drafts, underwriting referral notes, risk/fraud signal memos, and human action proposals. |
| Phase 4 | Add agent evaluations, red-team tests, audit verification, sensitivity-gate testing, no-fake-provider checks, and production-readiness evidence. |

---

## 16. Acceptance criteria

| ID | Acceptance criterion | Evidence |
|---|---|---|
| AC-AI-001 | The canonical registry contains exactly 10 agents. | Architecture review. |
| AC-AI-002 | All agents are registered with allowed tools and side-effect levels. | Tool registry review. |
| AC-AI-003 | Every AI output is tied to AgentRun, AIInteraction, sources where applicable, and audit where required. | Database/API test. |
| AC-AI-004 | Agents cannot approve/reject claims, bind policies, accuse fraud, set final premiums, or release payments. | Red-team test. |
| AC-AI-005 | Customer-facing drafts require human review before sending. | Workflow test. |
| AC-AI-006 | Risk & Fraud Signal Agent outputs advisory signals only and never makes fraud accusations. | Evaluation/red-team test. |
| AC-AI-007 | Underwriting Intelligence Agent provides referral/review recommendations only and never binds cover, declines, or sets final premium. | Evaluation/red-team test. |
| AC-AI-008 | OpenAI/Azure OpenAI outputs are grounded in approved tools and knowledge, not hidden assumptions. | Prompt/tool evaluation. |
| AC-AI-009 | Tool failures produce explicit degraded/not_configured/unavailable states, never fabricated answers. | Failure-mode test. |
| AC-AI-010 | Sensitive knowledge ingestion blocks customer-confidential, staff-only, secret, or unapproved PII-heavy content from AI retrieval. | Knowledge ingestion test. |

---

## 17. Implementation handoff notes

```text
- Do not implement agents as direct database readers.
- Agents must call typed Application-layer tools/services only.
- Use ITenantContextAccessor, IPermissionService, IAuditWriter, and IProviderHealthCheck consistently through backend services.
- Maintain one central tool registry table/configuration so allowed agent-tool relationships are auditable.
- Treat tool registry changes like security-sensitive changes requiring review.
- The frontend should render agent outputs as structured blocks where possible, especially in the Command Center.
- Human-review states must be explicit in DTOs and UI; do not bury them in free text.
```

Recommended initial implementation order:

```text
1. AgentRun and AIInteraction persistence.
2. Tool registry and permission checks.
3. Command Center Agent with read-only tools.
4. Claims Summary Agent and Document Checker Agent.
5. Policy Intelligence Agent and Knowledge Assistant.
6. Customer Update Drafting Agent with human approval boundary.
7. Risk & Fraud Signal Agent in advisory mode.
8. Report Insight Agent.
9. Underwriting Intelligence Agent in advisory/referral mode.
10. Agent evaluations, red-team tests, and audit verification.
```

---

## 18. Open questions and required confirmations

| ID | Open question / confirmation needed | Impact |
|---|---|---|
| OQ-AI-001 | Which model/provider versions are approved for pilot? | Affects evaluation, cost, and Azure OpenAI deployment names. |
| OQ-AI-002 | What confidence thresholds trigger mandatory review by product line and workflow? | Affects UX and workflow routing. |
| OQ-AI-003 | Who approves prompt changes and tool registry changes? | Affects governance and release controls. |
| OQ-AI-004 | Should Fraud Signal become a separate specialist agent later, increasing count from 10 to 11? | Affects agent registry, tooling, evaluation, and roadmap. |
| OQ-AI-005 | What exact underwriting products are in scope first: motor, medical, life, property, travel, or SME/commercial? | Affects Underwriting Intelligence Agent tool design. |
| OQ-AI-006 | Which roles may see risk/fraud signals and underwriting referral notes? | Affects RBAC and UI visibility. |
| OQ-AI-007 | Which knowledge sources are approved for retrieval, and who can approve ingestion? | Affects RAG safety and compliance. |
| OQ-AI-008 | Should agent orchestration be implemented through a central orchestrator service, per-agent services, or both? | Affects backend architecture and operational observability. |

---

## 19. No-fake-data and no-fake-agent confirmation

```text
- This document does not introduce fake operational data.
- This document does not permit simulated provider success in Staging or Production.
- Agents must return explicit empty, not_configured, unavailable, forbidden, low_confidence, or needs_human_review states when applicable.
- Agents must not hallucinate data, claim records, policy clauses, fraud indicators, underwriting rules, provider status, or report metrics.
```

