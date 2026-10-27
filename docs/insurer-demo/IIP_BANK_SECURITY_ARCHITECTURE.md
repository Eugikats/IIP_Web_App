# IIP security architecture overview

**Status: discussion diagram.** It describes the application boundaries visible in the current source and does not certify a deployment configuration.

```mermaid
flowchart LR
    U[Organisation user] -->|Identity provider token| P[Portal]
    P -->|HTTPS and versioned API| A[API authorization and tenant scope]
    A -->|Tenant and workspace filters| D[(Application data stores)]
    A -->|Permission checked request| W[Workflow and domain services]
    W --> D
    W -->|Approved provider connection| X[External providers]
    X --> M[Meta WhatsApp]
    X --> S[Configured SMS or email provider]
    W -->|Audited safe metadata| L[(Audit records)]
    W -->|Draft with source citations| H[Human review and decision]
    H -->|Explicit authorized action| W
```

## Boundaries and controls

1. The identity provider authenticates the person. IIP resolves organisation membership and workspace context server-side.
2. API authorization checks permissions on each protected request. Browser-supplied tenant identifiers are not sufficient authority.
3. Repositories scope customer records by tenant and workspace. Current database-level row security is not configured.
4. Provider credentials are encrypted before persistence and are write-only in the portal. Secrets and verification codes are not included in audit metadata.
5. WhatsApp traffic is processed through Meta. The message path is not described as end-to-end encrypted to IIP.
6. AI produces preparatory work. A person reviews source evidence and performs consequential actions.
7. Audit records capture important actors and actions without copying document or message content. SIEM export is not available.

## Deployment facts to verify

The diagram does not establish the target deployment's region, network topology, storage encryption settings, key location, availability, backup configuration or recovery objectives. Confirm these directly for each environment before making a deployment-specific statement.
