# Protected-Staging Multi-Role Verification — 2026-08-07

| Field | Result |
|---|---|
| Scope | Release 1 protected-staging identity, permission, tenant, route, accessibility and reliability verification |
| Subscription | `5f5765ae-985f-42e4-a443-8e2447be33ca` (`Microsoft Azure Sponsorship`) |
| Intended resource group | `rg-iip-stg` |
| Verdict | **BLOCKED — protected staging runtime absent** |
| Evidence artifact | `artifacts/rf09/protected-staging-smoke-2026-08-07.json` |

## Live evidence

- Azure CLI authenticated successfully to tenant `dcb0b132-fb9e-475c-a68e-b0671222e8d8`.
- `rg-iip-stg` exists in `centralus` with provisioning state `Succeeded`, but its live resource inventory is empty.
- `app-api-stg-cus-vyhhymwydvai2` returns Azure `ResourceNotFound`.
- A subscription-wide resource search found IIP resources only in `rg-iip-dev`; development is not substituted for protected staging.
- The documented staging API and portal smoke failed before authentication because the endpoints are unavailable. The authenticated check also failed because no token was supplied.
- `.azure/deployment-plan.md` is absent, so the `azure-validate` workflow cannot run under its prerequisite contract.
- The Azure MCP server required by the `azure-rbac` skill is not connected. No Azure role definition, assignment or least-privilege conclusion is claimed.

Historical evidence that the staging apps were live on 2026-06-22 is not current runtime proof.

## Supporting local evidence

- Targeted backend authorization, Global Search, My Work, Recent Conversations and WhatsApp tests: **61 passed, 0 failed** in Release configuration.
- Portal runtime-data, provider-status, role-route and UI-release-gate validators: **passed**.

These results support source consistency only. They do not satisfy authenticated protected-staging, real-provider, browser, accessibility, tenant-isolation or latency gates.

## Required role matrix

Use distinct Entra identities backed by active server-side tenant/workspace memberships. Browser role selection, token roles and request headers are not authority.

| Identity | Required positive journey | Required safe-denial journey |
|---|---|---|
| Claims Officer | Assigned claims, documents, extraction review, My Work, authorized search, case-linked WhatsApp context | Unassigned/restricted claim, final approval, tenant administration, other-tenant identifiers |
| Claims Supervisor | Team queue, assignment/escalation, configured approvals, SLA work, authorized search | Tenant secrets/settings not granted; stale or ineligible approval |
| Customer Service Agent | Customer-safe case status, Channels/WhatsApp thread, reviewed outbound draft and handoff | Fraud/internal notes, settlement/final claim decision, unauthorized claim/evidence |
| Compliance Officer | Audit trail, evidence lineage, consent/exception review, permitted export | Mutation/deletion of audit evidence; operational writes not separately granted |
| Tenant Admin | Membership/configuration, provider-health and permitted administration | Bypass of audit/human gates; unauthorized support or other-tenant access |
| Unauthorized/no-membership user | Login or entry-gate handling without protected content | `/api/v1/me`, direct URLs and APIs fail closed without existence or metadata leakage |
| Other-tenant user | Own authorized tenant/workspace only | Exact IIP identifiers from the test tenant return no data, count, snippet or side-channel |

Every role must verify `/api/v1/me` source, tenant, workspace, role and effective permissions; visible navigation; direct-route/API enforcement; record/action scope; audit correlation; session revocation/change; and no restricted data in errors, counts, search results or browser payloads.

## Browser and accessibility evidence required

- Run each critical role journey in a clean authenticated browser context and retain trace, timestamp, route, identity label, tenant/workspace and expected/actual result.
- Test keyboard-only navigation, visible focus, names/roles/values, status announcements, error recovery, session expiry, 200% text and 320 CSS-pixel reflow.
- Treat automated accessibility output as partial evidence; complete manual keyboard and assistive-technology checks for critical tasks.
- Verify loading, empty, partial, timeout, provider unavailable, `Not configured`, unauthorized and permission-changed states without mock fallback.

## Reliability evidence required

Once staging exists, assess its App Service/Functions compute, storage redundancy, health probes and regional failover. No reliability status can be assigned while the scope contains no resources. Any remediation requires separate confirmation because it may change live resources or cost.

## Reopen and closure conditions

1. Approve a protected-staging deployment plan and recreate or nominate the exact non-development runtime.
2. Confirm deployed commit/artifact parity, Entra configuration, database migration state and protected settings with no mocks, seeds or fallback-to-mock.
3. Provision the seven identity conditions above with synthetic/masked records in at least two tenant/workspace scopes.
4. Run `Invoke-IipLiveUatSmoke.ps1` per role with `-RequireAuthenticated`, expected server context, positive checks and at least one denied route.
5. Run Playwright browser journeys and accessibility checks, retaining traces/screenshots without tokens or sensitive customer data.
6. Correlate each journey to API/audit/Application Insights evidence and run representative p95/p99 measurements near the deployment region.
7. Record defects, retests, named Product/Insurance/Operations/Security/Architecture/Delivery decisions, and a dated go/no-go verdict.

Until all conditions have retained evidence, protected-staging multi-role verification remains open and Release 1 staging readiness remains **NO**.
