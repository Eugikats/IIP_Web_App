# IIP Offline Mode — run the platform with no Azure account

Offline mode lets anyone on the team clone the repo and run IIP end-to-end on their own
machine with **no Azure subscription, no credentials, no Entra sign-in and no database
install**. It exists so people can review screens, click through workflows and identify gaps
without us handing out access to the shared Azure environment.

## What you need

- .NET 8 SDK
- Node.js 20 + pnpm (only if you also want the web portal)

That is the whole list. No `az login`, no connection string, no secrets.

## Running the API

```bash
# macOS / Linux / Git Bash
./scripts/local/run-offline.sh
```

```powershell
# Windows PowerShell
./scripts/local/run-offline.ps1
```

The API starts on **http://localhost:5167** and seeds the demo dataset automatically —
50 claims across Motor, Medical, Life, General and Agriculture, with supporting customers,
policies, documents, approvals and audit history.

Confirm it is up:

```bash
curl http://localhost:5167/health/ready
```

## Running the portal as well

In a second terminal:

```bash
cd web/portal
pnpm install
IIP_LOCAL_API_PROXY_TARGET=http://localhost:5167 pnpm dev
```

Then open **http://localhost:3000**.

Keep `NEXT_PUBLIC_API_BASE_URL` unset for local portal runs so browser requests stay same-origin and Next.js proxies `/api/*` to the API. If the API is on another local port, change only `IIP_LOCAL_API_PROXY_TARGET` to that HTTP(S) origin. Do not point the browser directly at a different origin unless that API explicitly allows the portal origin through CORS.

## Signing in

Offline mode uses `AUTH_MODE=Development`, so there is no Microsoft sign-in. Requests carry
demo headers instead:

| Header | Value |
| --- | --- |
| `X-Tenant-Id` | `tenant-demo` |
| `X-Workspace-Id` | `workspace-operations` |
| `x-demo-role` | `Admin` (or `ClaimsOfficer`, `ClaimsSupervisor`, `Compliance`, …) |
| `X-User-Id` | `local-admin` |

Change `x-demo-role` to review the app as a different role.

Example:

```bash
curl http://localhost:5167/api/v1/cases \
  -H "X-Tenant-Id: tenant-demo" \
  -H "X-Workspace-Id: workspace-operations" \
  -H "x-demo-role: Admin" \
  -H "X-User-Id: local-admin"
```

## What works offline, and what does not

| Area | Offline behaviour |
| --- | --- |
| Claims, documents, approvals, customers, policies, audit | **Full** — real domain logic over the seeded dataset |
| Permissions and role behaviour | **Full** — real authorisation, driven by `x-demo-role` |
| Screens, navigation, empty/error states | **Full** |
| AI Copilot answers, document extraction, knowledge search | **Not configured** — these need Azure OpenAI, Document Intelligence and AI Search |
| WhatsApp / SMS / email | **Not configured** — these need third-party accounts |

Anything unavailable shows an explicit **Not configured** state. Nothing is simulated to look
like it is working — that is deliberate, and matches how the product behaves in a real
deployment when a provider is missing.

To review the AI features you need the shared deployed environment, not offline mode.

## Important limits

- **Data is not saved.** Storage is in-memory: every restart gives you a clean, freshly seeded
  dataset. Anything you create is gone when you stop the process. That is intended — it means
  everyone tests from an identical starting point.
- **Offline mode cannot be deployed.** In-memory storage is refused under the `Demo`,
  `Staging`, `Production` and `ManagedApplicationReviewer` profiles, and under any profile name
  that is not explicitly local. Three independent checks enforce this, and
  `OfflineModeStorageGuardTests` pins the behaviour. A deployed environment always requires
  Azure SQL.

## If something fails to start

| Message | Cause |
| --- | --- |
| `cannot run with INSURANCEINTELLIGENCE_STORAGE_PROVIDER=InMemory under the '<x>' runtime profile` | `INSURANCEINTELLIGENCE_RUNTIME_PROFILE` is not a local profile. Use `LocalTest`. |
| `SqlServer storage requires INSURANCEINTELLIGENCE_SQLSERVER_CONNECTION_STRING` | `STORAGE_PROVIDER` is still `SqlServer`. Use the script rather than setting variables by hand. |
| Port already in use | Another instance is running, or set `ASPNETCORE_URLS` to a free port. |
