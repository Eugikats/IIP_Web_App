# Client Readiness Checklist

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Client Readiness Checklist |
| Version | 1.0 |
| Status | Draft |
| Audience | Client sponsor, pilot manager, implementation team, security/legal, operations |
| Date | 2026-06-30 |

## Core Principles Used Across This Pack

- IIP is the official product name. Older ACIP references are not used as official naming.
- The MVP is centered on motor claims and document intelligence for an insurer pilot.
- IIP assists insurance work; humans make consequential insurance decisions.
- AI may summarize, classify, extract, flag, draft, recommend, and route, but it must not approve or reject claims, bind policies, set final premiums, accuse customers of fraud, promise settlement, or release payment.
- The target architecture is Azure-first with Next.js/React/TypeScript frontend, .NET 8 backend, Azure SQL, Blob Storage, Key Vault, AI Search, Azure OpenAI, Azure AI Document Intelligence, Service Bus/Event Grid, Application Insights, Entra ID/MSAL, and Microsoft Graph where appropriate.
- Customer interaction is WhatsApp-first but can extend to web chat, SMS, email, broker, call center, and Microsoft channels.
- Internal users work through role-aware staff workspaces; customers do not log into the internal app.
- Staging and production must not rely on fake data or mock endpoints. Empty, error, unauthorized, loading, and Not configured states must be handled clearly.


## Purpose

This checklist confirms whether a client is ready to begin an IIP pilot or implementation. It should be completed before pilot kickoff and reviewed again before UAT and go-live.

## Readiness Scoring

| Score | Meaning |
|---|---|
| Green | Ready or low-risk |
| Amber | Partially ready; action required |
| Red | Not ready; blocks pilot or go-live |
| N/A | Not applicable to current pilot scope |

## 1. Sponsorship and Governance

| Check | Status | Owner | Notes |
|---|---|---|---|
| Executive sponsor confirmed |  |  |  |
| Business process owner confirmed |  |  |  |
| Technical contact confirmed |  |  |  |
| Compliance/legal reviewer confirmed |  |  |  |
| Pilot steering cadence agreed |  |  |  |
| Decision and escalation pathway agreed |  |  |  |

## 2. Pilot Scope

| Check | Status | Owner | Notes |
|---|---|---|---|
| Motor claims pilot scope confirmed |  |  |  |
| Included claim types confirmed |  |  |  |
| Excluded workflows confirmed |  |  |  |
| AI prohibited actions acknowledged |  |  |  |
| Human review gates confirmed |  |  |  |
| Pilot start and end dates agreed |  |  |  |

## 3. Users and Roles

| Check | Status | Owner | Notes |
|---|---|---|---|
| Claims officer users identified |  |  |  |
| Claims supervisor users identified |  |  |  |
| Admin users identified |  |  |  |
| Compliance/audit users identified |  |  |  |
| Customer service users identified |  |  |  |
| Executive/viewer users identified |  |  |  |
| Role permissions reviewed |  |  |  |

## 4. Process and Workflow Readiness

| Check | Status | Owner | Notes |
|---|---|---|---|
| Current motor claims workflow documented |  |  |  |
| FNOL process documented |  |  |  |
| Document review process documented |  |  |  |
| Approval process documented |  |  |  |
| Escalation rules documented |  |  |  |
| Customer update process documented |  |  |  |
| SLA definitions confirmed |  |  |  |

## 5. Document Readiness

| Document type | Required? | Sample provided? | Template/format confirmed? | Notes |
|---|---|---|---|---|
| Claim form |  |  |  |  |
| Police report |  |  |  |  |
| National ID |  |  |  |  |
| Driving permit |  |  |  |  |
| Vehicle photos |  |  |  |  |
| Repair quotation |  |  |  |  |
| Policy schedule |  |  |  |  |
| Assessor report |  |  |  |  |
| Other client-specific documents |  |  |  |  |

## 6. Data Readiness

| Check | Status | Owner | Notes |
|---|---|---|---|
| Sample claims provided |  |  |  |
| Sample customer records provided |  |  |  |
| Sample policy records provided |  |  |  |
| Sample document set approved for testing |  |  |  |
| Synthetic/demo data approved where needed |  |  |  |
| Data privacy rules confirmed |  |  |  |
| Data retention requirements confirmed |  |  |  |

## 7. Integration Readiness

| Integration | Required for pilot? | Contact | Status | Notes |
|---|---|---|---|---|
| WhatsApp Business API |  |  |  |  |
| Email / Microsoft Graph |  |  |  |  |
| Policy lookup |  |  |  |  |
| Claims system lookup/export |  |  |  |  |
| Blob/document storage |  |  |  |  |
| Azure AI services |  |  |  |  |
| Reporting/Power BI |  |  |  |  |

## 8. Security and Legal Readiness

| Check | Status | Owner | Notes |
|---|---|---|---|
| Identity approach confirmed |  |  |  |
| Entra ID/MSAL requirements confirmed |  |  |  |
| Data residency requirements confirmed |  |  |  |
| Security review pathway confirmed |  |  |  |
| Legal/compliance review pathway confirmed |  |  |  |
| Customer communication approval rules confirmed |  |  |  |
| Support access model confirmed |  |  |  |
| Incident response contacts confirmed |  |  |  |

## 9. Environment Readiness

| Check | Status | Owner | Notes |
|---|---|---|---|
| Deployment model selected |  |  | Managed Application / SaaS / demo |
| Pilot environment provisioned |  |  |  |
| Provider health checked |  |  |  |
| Required environment variables configured |  |  |  |
| Key Vault/secrets configured |  |  |  |
| Monitoring configured |  |  |  |
| Backup and rollback approach confirmed |  |  |  |

## 10. Training and Adoption Readiness

| Check | Status | Owner | Notes |
|---|---|---|---|
| Training dates agreed |  |  |  |
| Role-based training groups confirmed |  |  |  |
| UAT participants confirmed |  |  |  |
| Support channels communicated |  |  |  |
| FAQ/troubleshooting guide shared |  |  |  |
| Pilot communication plan approved |  |  |  |

## Readiness Summary

| Area | Green | Amber | Red | Decision |
|---|---:|---:|---:|---|
| Sponsorship and governance |  |  |  |  |
| Scope |  |  |  |  |
| Users and roles |  |  |  |  |
| Workflows |  |  |  |  |
| Documents |  |  |  |  |
| Data |  |  |  |  |
| Integrations |  |  |  |  |
| Security/legal |  |  |  |  |
| Environment |  |  |  |  |
| Training/adoption |  |  |  |  |

## Go / No-Go Recommendation

| Decision | Criteria |
|---|---|
| Go | No red blockers; amber items have owners and dates |
| Conditional Go | Non-critical red items accepted by sponsor with mitigation |
| No-Go | Critical blockers remain in security, data, scope, users, or environment |

## Open Questions and Required Confirmations

| Item | Status | Notes |
|---|---|---|
| First pilot insurer | Requires Confirmation | ICEA-like mid-to-large insurer is the working assumption, but the named pilot client must be confirmed. |
| Commercial currency | Requires Confirmation | Pricing can be expressed in USD for investor/client modeling, but local contract currency may vary. |
| Final pilot duration | Requires Confirmation | An 8 to 12 week pilot is assumed unless client procurement or integration timelines require adjustment. |
| Production deployment model | Requires Confirmation | Azure Managed Application is preferred for enterprise clients; Otic-hosted SaaS may be used for demos or smaller clients. |
| Regulatory sign-off requirements | Requires Confirmation | Local insurance, privacy, outsourcing, and client legal requirements must be validated before production. |

