# IIP AI Cost Model — Pilot & Scale Guide

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document | AI Cost Model — Pilot & Scale |
| Version | 1.0 |
| Status | Draft |
| Audience | Commercial leads, engineering, finance, pilot customers |

---

## 1. Overview

IIP records per-request AI token consumption on every `AgentActionLog` row:

| Column | Type | Source |
|---|---|---|
| `PromptTokens` | `int?` | Parsed from `usage.prompt_tokens` in Azure OpenAI response |
| `CompletionTokens` | `int?` | Parsed from `usage.completion_tokens` in Azure OpenAI response |
| `EstimatedCostUsd` | `decimal?` | Computed by `AiCostEstimator` at request time |
| `ModelVersion` | `string?` | Parsed from `model` field in Azure OpenAI response |

Token counts are `null` when the provider does not return usage (e.g. `NotConfigured`, `Mock`). The billing screen shows `Not configured` for those meters rather than fabricated numbers.

---

## 2. List price basis (gpt-4o family, 2024-08 vintage)

| Token type | Price per 1 000 tokens |
|---|---|
| Prompt (input) | $0.0025 |
| Completion (output) | $0.010 |

**These are list prices only.** Actual billing comes from Azure Cost Management. Negotiate reserved capacity pricing at scale.

---

## 3. Pilot cost envelope (indicative)

Assumptions: 1 insurer, 50 staff, 200 claims/month, 10 assistant queries per claim.

| Item | Tokens/request | Requests/month | Est. cost/month |
|---|---|---|---|
| Policy Q&A (prompt + 3 chunks) | ~1 200 prompt + 400 completion | 2 000 | ~$7 |
| Command Center query | ~800 prompt + 300 completion | 1 500 | ~$4.50 |
| Customer update drafting | ~600 prompt + 200 completion | 400 | ~$1.20 |
| **Total pilot estimate** | | | **~$13/month** |

Pilot AI cost is negligible at single-insurer scale. Azure OpenAI is billed pay-as-you-go until reserved capacity is warranted.

---

## 4. Scale model (100 insurers, SaaS)

At 100 tenants × pilot volume = ~$1 300/month AI cost. Azure reserved capacity (Provisioned Throughput Units) reduces per-token cost significantly at this scale. Review PTU pricing via Azure Portal before committing commercial pricing to customers.

---

## 5. Cost controls

- `AiCostEstimator.Estimate()` runs at request time and records `EstimatedCostUsd` per log row — no polling required.
- `RecordBackedBillingUsageSource` aggregates real token counts from `AgentActionLog` for the billing dashboard.
- Custom Azure Monitor alert rules on the `IIP.AiTokenUsage` custom event can fire when per-tenant spend exceeds a threshold.
- Tenant agent capability policy can disable specific agents to cap cost.

---

## 6. AppInsights event: `IIP.AiTokenUsage`

Emitted per-request by `AssistantEndpoints`. Properties:

| Property | Value |
|---|---|
| `TenantId` | Tenant identifier |
| `WorkspaceId` | Workspace identifier |
| `AgentName` | e.g. `PolicyQnAAgent` |
| `ModelVersion` | e.g. `gpt-4o-2024-08-06` |
| `PromptTokens` | Integer |
| `CompletionTokens` | Integer |
| `EstimatedCostUsd` | Decimal |

Use this event in Azure Monitor Workbooks to build per-tenant cost dashboards.
