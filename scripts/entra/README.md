# IIP canonical Entra registrations

`Initialize-IipPlatformAppRegistrations.ps1` creates or reconciles the canonical multitenant application pair required by ADR-0005:

- `iip-api-platform`: multitenant API, v2 access tokens, `access_as_user`, and only the coarse `IIP.Access`, `IIP.Admin`, and `IIP.ReadOnly` entry roles.
- `iip-operations-portal`: multitenant SPA client pre-authorized for the API delegated scope.

The script creates no client secret and does not mutate the legacy `iip-api-stg`, `iip-portal-stg`, or `iip-portal-dev-codex` registrations. Fine-grained permissions remain in IIP's tenant/workspace membership store.

The default dev redirects include both the portal origin and `/app`, matching the current MSAL redirect setting. Add staging/production redirects explicitly when those hosts exist; do not add wildcards.

Preview:

```powershell
./scripts/entra/Initialize-IipPlatformAppRegistrations.ps1
```

Apply idempotently:

```powershell
./scripts/entra/Initialize-IipPlatformAppRegistrations.ps1 -Apply
```

Do not switch a live App Service to the new IDs until the API and portal builds carrying ADR-0005 tenant membership resolution have been deployed and a launch tenant has completed admin consent.
