# ADR-0002: AI Agent Registry, Kill Switch, and Multi-Agent Orchestration Boundary

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-07-04 |
| Deciders | Architecture (per IIP Full-Program Plan, Initiative LP; Product Decision Log DEC-013) |
| Supersedes | — |
| Related | `AGENTS.md` §4 (Human-supervised decision boundary), §26 (ADR-required areas: multi-agent orchestration boundary), `docs/03-design-architecture/iip_ai_agent_architecture_tool_registry_updated.md`, `docs/03-design-architecture/iip_conversational_session_context_multi_agent_architecture.md`, `src/InsuranceIntelligence.Domain/Agents/AgentContracts.cs`, `docs/01-strategy-requirements-roadmap/iip_product_decision_log.md` (DEC-013) |

## Context

The approved Lovable reference introduces an **AI Agent Registry** with per-agent configuration (prompt versions, tool/knowledge policies, confidence thresholds, human-approval rules), per-agent run logs, and a **kill switch** (per-agent and tenant-wide). `AGENTS.md` §26 explicitly names the "multi-agent orchestration boundary" as an area that requires an ADR before implementation. The repo already contains a legacy, currently-unwired agent contract set (`Domain/Agents/AgentContracts.cs`, dropped during the v2 migration per a `Program.cs` comment) and design-doc coverage of an agent/tool architecture. This ADR fixes the boundary and control model before the registry is built under Initiative LP (LP.3).

## Decision

1. **The Agent Registry is a control-plane, not an autonomy-plane.** It stores and enforces *constraints* on what AI agents may do (which tools/knowledge sources an agent may use, its confidence threshold, whether a human approval is required for a given action class). It never grants an agent authority to take a consequential insurance decision. The `AGENTS.md` §4 human-supervised decision boundary is unchanged and unconditional — no registry configuration, confidence threshold, or approval rule can enable an agent to approve/reject/settle/pay/bind/underwrite/accuse, and `ApprovalDecisionGuard.EnsureHumanForConsequentialDecision` remains the backstop regardless of registry state.

2. **The kill switch is a governed, audited human admin action** — not an autonomous or AI-triggered one. It is enforced at the point an agent is invoked: a disabled agent (per-agent switch) or a tenant-wide halt causes the invocation path to fail closed (the capability shows `Not configured` / `Disabled by administrator`), never to silently degrade to a fabricated or fallback result. Every kill-switch state change is written to the audit trail with actor, reason, and correlation ID, using the same `GovernedActionDialog` reason-capture pattern used elsewhere.

3. **Run logs are real and factual.** An agent run-log entry is written only when an agent action actually executes; entries are never fabricated to populate the UI. Absent runs render as an empty state.

4. **Tenant isolation applies to all registry state.** Agent definitions, policies, run logs, and kill-switch states are tenant/workspace-scoped and enforced server-side via the existing `ITenantContextAccessor` + read-repository `TenantScope` pattern; no cross-tenant visibility of agent config or run history.

5. **Orchestration scope for v1 is deliberately narrow.** v1 delivers the registry, per-agent config, run logging, and kill switch as real state and enforcement. It does **not** re-introduce the dropped multi-agent orchestrator/tool-runtime as a live execution engine; agents remain the existing block-producing, human-reviewed Command Center capability, now governed by registry constraints. Reviving a full orchestrator runtime is a separate, later decision.

## Reasoning

Insurance decisions are consequential and regulated; the winning posture (per `AGENTS.md`) is auditability and human accountability, not autonomy. Modeling the registry as a constraint/control plane means adding agent configurability can only ever *tighten* what AI may do, never loosen the §4 boundary — the safe direction by construction. Making the kill switch a fail-closed, audited human control gives operators a real, provable "stop" that satisfies enterprise and regulatory expectations without depending on any live external provider (so it is fully functional inside Gate 0). Keeping the orchestrator runtime out of v1 scope avoids reintroducing a large, dropped subsystem before there is a concrete need, consistent with the guardrail against introducing new architectural patterns without a clear reason.

## Consequences

- LP.3 builds real tenant-scoped agent-definition/policy/run-log/kill-switch entities, query/command services, endpoints, and a governed `/admin/agents` UI; kill-switch enforcement is unit-tested (a disabled agent must fail closed).
- No registry configuration path may weaken `ApprovalDecisionGuard`; tests assert consequential-decision actions remain human-gated regardless of agent confidence/threshold/approval-rule settings.
- If/when a live Azure OpenAI–backed narration or a revived orchestrator runtime is considered (Gate 3+), this ADR is revisited to extend — not relax — the boundary.
