# IIP Bevaya-parity release blueprint

**Decision date:** 2026-07-16  
**Delivery model:** release gates, not calendar estimates  
**Product boundary:** AI prepares insurance work; accountable humans make consequential decisions and approve external actions.

## Product direction

IIP adopts the strongest useful pattern visible in Bevaya's product positioning: a visual automation studio backed by specialist insurance agents, durable runs, evidence, and human handoffs. IIP strengthens that pattern with tenant/workspace enforcement, immutable published workflow versions, permission checks at every service boundary, bounded context, real provider status, and fail-closed protected runtimes.

The target is not an autonomous insurance decision engine. It is an enterprise insurance operations control plane sitting above policy, claims, document, communications, reporting, and knowledge systems.

## Release sequence

| Release | Governed workflow | Agents used | Human gate | Current repository state | Exit gate |
|---:|---|---|---|---|---|
| 1 | Document-to-Decision | Document Checker | Extraction review | Published system definition, durable run/work-item model, API and UI complete in source | Real document upload -> Azure Document Intelligence -> bounded evidence -> reviewed correction path passes in Azure |
| 2 | FNOL & Claim Readiness | Claims Summary, Policy Intelligence, Document Checker, Routing | Readiness review | Published system definition and visual graph complete in source | Real FNOL/case reference produces a grounded readiness pack; routing remains a proposal |
| 3 | Claims Triage & SLA | Claims Summary, Risk & Fraud Signal, Routing | Triage review | Published system definition and visual graph complete in source | Real claim facts and SLA state produce an advisory triage pack without claim decision authority |
| 4 | Policy & Customer Response | Policy Intelligence, Knowledge Assistant, Customer Update Drafting | Communication review | Published system definition and visual graph complete in source | Approved policy/knowledge sources produce a reviewed draft; sending remains a separate approved service |
| 5 | Underwriting & Management Insight | Underwriting Intelligence, Risk & Fraud Signal, Report Insight, Command Center | Insight review | Published system definition and visual graph complete in source | Real underwriting/report inputs produce an advisory evidence pack without binding, pricing, or adverse action |

## Enterprise product surfaces now in source

- **Automation Studio:** release catalog, visual graph, node inspector, tenant workflow builder, publish checks, and start-run control.
- **Agent Work Items:** human review queue with brief, evidence, run trace, accept-prepared-work, return-for-correction, and escalation decisions.
- **Agent Run Debugger:** durable node states, bounded outputs, evidence references, provider/model metadata, prompt version, correlation ID, and failure code.
- **Control-plane API:** versioned definitions, publication, run initiation, node completion, work-item query, and review-decision endpoints.
- **Persistence:** Azure SQL/EF entities and migration for tenant-scoped definitions, runs, node traces, and review items.
- **Governance:** exactly 10 canonical agents, an allow-listed tool catalog, immutable system releases, no external-send nodes, human-review-path validation, permissions, and audit events.

## Azure foundation established

- Canonical multitenant API registration: `iip-api-platform`.
- Canonical multitenant portal registration: `iip-operations-portal`.
- No client secrets; delegated scope and coarse entry roles are configured.
- Active `agent-workflow` Service Bus queue with dead-lettering, retry, lock, and TTL settings.
- Phase 3 and Functions Bicep include the canonical identity and workflow transport configuration.

The live portal deliberately remains on the legacy registration pair. Azure SQL is not migrated because the current operator identity is not the configured SQL Entra administrator. The production agent execution worker is not enabled merely because the queue exists.

## Immediate gate order

1. Obtain an authorized Azure SQL migration path, compare the consolidated pending schema with the live database, and apply `AddAgentWorkflowControlPlane` through EF migration history.
2. Implement and deploy the tenant-scoped Functions execution worker with real tool adapters, bounded completion callbacks, retries, dead-letter handling, and idempotency.
3. Wire Release 1 end-to-end against Blob Storage and Azure AI Document Intelligence; prove correction re-triggers readiness evaluation.
4. Wire the remaining releases to real claims, policy, knowledge, SLA, underwriting, and reporting ports in order.
5. Build and deploy the API and portal with canonical Entra configuration; prove membership resolution and `/api/v1/me` before switching the live pair.
6. Run security, audit, provider-health, load, recovery, and human-review evidence gates before promoting each release.

No release may advance by substituting mock data, fake provider success, model memory, or an autonomous consequential decision.
