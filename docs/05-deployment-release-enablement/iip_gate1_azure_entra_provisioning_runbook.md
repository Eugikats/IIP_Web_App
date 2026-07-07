# Gate 1 — Azure / Entra Provisioning Runbook (identity, app roles, Key Vault, managed identity)

**Purpose:** the exact manual Azure/Microsoft Entra steps to make IIP's real identity + secrets work,
so the Gate 1 code (ADR-0005) can be proven live. IIP is a **single multitenant** Entra app
registration; insurer staff sign in from their own workforce tenants. Runtime authorization stays in
IIP's membership store — Entra only proves identity + coarse entry (`IIP.Access`).

> Owner performs these; the app itself never self-provisions. Capture the emitted IDs into the env
> vars in §7. Nothing here runs from the dev machine — it is Azure portal / `az` CLI work.

---

## 1. App registration (IIP publisher/home tenant)

1. Entra admin center → **App registrations → New registration**.
   - Name: `IIP Operations API`.
   - Supported account types: **Accounts in any organizational directory (multitenant)**.
   - Redirect URI: leave blank for the API; add the onboarding portal's callback later (§5).
2. Record **Application (client) ID** and the **Directory (tenant) ID** (this is IIP's *home* tenant, not a customer's).

## 2. Expose the API (audience)

1. App → **Expose an API** → set **Application ID URI** = `api://{client-id}` (default).
2. Add a scope (e.g. `access_as_user`, admin+user consent) if the portal/SPA calls the API on behalf of a user.
3. The API validates audience = `api://{client-id}` (see `Program.cs`; overridable via `INSURANCEINTELLIGENCE_ENTRA_AUDIENCE`).

## 3. App roles — the coarse entry gate (ADR-0005 rule 8)

1. App → **App roles → Create app role**:
   - Display name: `IIP Access`
   - Value: **`IIP.Access`**  ← emitted in the token `roles` claim
   - Allowed member types: **Users/Groups**
   - Description: "May sign in to IIP. Fine-grained permissions are assigned inside IIP."
2. **Do NOT** create per-permission app roles (claims.read, approvals.review, …). Fine-grained
   permissions live in IIP's DB store, not in Entra. `IIP.Access` answers only "may enter IIP at all?"
3. (Optional, later) additional coarse roles only if a real access-tier need appears.

## 4. Token configuration

- Ensure the `roles` claim is emitted (default for app roles). IIP maps `IIP.Access` as the entry gate
  and reads the stable user id from `oid` (object id) — **not** `tid`, which is only a lookup hint.
- Keep `preferred_username` as the name claim (already configured in `Program.cs`).

## 5. Multitenant admin consent (per insurer tenant)

For each insurer onboarding (see identity spec §4.2 and the Gate 1 onboarding flow, G1.7):

```
https://login.microsoftonline.com/{insurer-entra-tenant-id}/adminconsent
  ?client_id={client-id}
  &redirect_uri={iip-onboarding-callback}
  &state={onboarding-session-state}
```

- Use the **insurer's tenant id**, never `common`, for admin consent.
- On success Microsoft redirects with `admin_consent=True&tenant={insurer-tenant}&state=…`.
- IIP validates `state`, stores the insurer tenant as `ExternalEntraTenantId` (a lookup key, not
  authority), and creates a bootstrap membership for the consenting admin.

## 6. Key Vault + managed identity (secrets)

1. Create a **Key Vault** (RBAC-authorization recommended).
2. Give the IIP compute (App Service / Container App) a **system-assigned managed identity**.
3. Grant that identity **Key Vault Secrets User** on the vault.
4. Store secrets (SQL connection string, WhatsApp tokens, any client secret) as vault secrets.
5. Set `Azure__KeyVaultUri = https://{vault}.vault.azure.net/`. The app loads secrets via
   `DefaultAzureCredential` (managed identity in Azure; developer creds locally) — implemented in G1.6.
6. Prefer **managed identity** for Azure SQL / Blob / OpenAI / Search / Service Bus over connection
   strings/keys wherever supported (`*:UseManagedIdentity=true`, already the default).

## 7. Environment variables (deployed profile)

| Variable | Value | Notes |
|---|---|---|
| `INSURANCEINTELLIGENCE_AUTH_MODE` | `Entra` | Enables JWT validation; rejects demo headers. |
| `INSURANCEINTELLIGENCE_RUNTIME_PROFILE` | `Staging` / `Production` (or `Demo`) | Governs the zero-simulation guardrails. |
| `INSURANCEINTELLIGENCE_ENTRA_TENANT_ID` | IIP home tenant id | Required; multitenant issuer stays dynamic. |
| `INSURANCEINTELLIGENCE_ENTRA_CLIENT_ID` | app (client) id | Required. |
| `INSURANCEINTELLIGENCE_ENTRA_AUDIENCE` | `api://{client-id}` | Optional; defaults from client id. |
| `INSURANCEINTELLIGENCE_STORAGE_PROVIDER` | `SqlServer` | Azure SQL. |
| `Azure__KeyVaultUri` | `https://{vault}.vault.azure.net/` | Secrets source (G1.6). |

## 8. Proving it live (acceptance)

1. Deploy with the vars above (Gate 2 does the real deploy; a dev-tenant smoke test can be earlier).
2. Acquire a user token for a member of `IIP.Access` in a consented insurer tenant.
3. `GET /api/v1/me` with `Authorization: Bearer {token}` → returns the user's **resolved** tenant/
   workspace/permissions from IIP's store (not the token's tid/roles).
4. A user **without** `IIP.Access` → 403 at the entry gate.
5. A user with `IIP.Access` but **no IIP membership** → authenticated but no data / no permissions
   (fail closed) — never a cross-tenant leak.
6. Confirm no demo headers are accepted (send `x-demo-role` → 403).

## 9. What IIP does NOT rely on (ADR-0005)

- The token `tid` as tenant authority. It is a hint only.
- Raw Entra group claims for authorization.
- Per-permission Entra app roles.
- Any single hardcoded `ValidIssuer` (multitenant issuer validation is dynamic).
