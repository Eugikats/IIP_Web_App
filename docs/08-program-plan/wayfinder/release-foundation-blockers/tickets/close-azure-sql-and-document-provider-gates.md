# Close Azure SQL and Document Provider Gates

**Label:** `wayfinder:task`  
**Status:** In progress  
**Claim:** harry/rf-invalidate-downstream-after-corrections  
**Blocked by:** Deliver the Release 1 Workflow Execution Worker

## Question

Which authorized migration path, deployment region, provider capacity, identity grants, and rollback evidence are required to apply workflow schema and prove real Blob Storage plus Azure AI Document Intelligence for Release 1?

## Resolution criteria

- Live schema compared and EF migration applied through authorized history.
- Managed identities receive least-privilege data-plane access.
- Real upload, extraction, bounded evidence, correction, and re-evaluation pass in dev or approved staging.
- Migration/deployment evidence and rollback steps are recorded without secrets.

## Progress notes

- Added provider-health status hardening so explicitly disabled/unconfigured providers surface as `not_configured` rather than ambiguous missing states.
- Added API test coverage proving Document Intelligence / AI Search / OpenAI emit `not_configured` with safe degraded posture when disabled by environment policy.
- Operator-gated evidence remains pending:
  - authorized Azure SQL migration window and applied-history capture;
  - Document Intelligence deployment/region/capacity verification;
  - staged live proof for upload -> extraction -> correction/re-evaluation path.
