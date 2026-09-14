const configuredApiBaseUrl = process.env.NEXT_PUBLIC_API_BASE_URL?.trim();
// Same-origin is the safe default: Next proxies /api to the local API in
// development, while deployed runtimes must provide their approved API origin.
export const apiBaseUrl = configuredApiBaseUrl ? configuredApiBaseUrl.replace(/\/$/, "") : "";
export const defaultTenantId = process.env.NEXT_PUBLIC_TENANT_ID || "";
export const defaultWorkspaceId = process.env.NEXT_PUBLIC_WORKSPACE_ID || "";
export const localDevelopmentUserId = process.env.NEXT_PUBLIC_LOCAL_USER_ID || "";
const developmentBuild = process.env.NODE_ENV === "development";
export const authMode = process.env.NEXT_PUBLIC_AUTH_MODE || (developmentBuild ? "Development" : "NotConfigured");
export const runtimeProfile = process.env.NEXT_PUBLIC_RUNTIME_PROFILE || (developmentBuild ? "Development" : "Unknown");
export const entraTenantId = process.env.NEXT_PUBLIC_ENTRA_TENANT_ID || "";
export const entraClientId =
  process.env.NEXT_PUBLIC_ENTRA_CLIENT_ID || process.env.NEXT_PUBLIC_ENTRA_PORTAL_CLIENT_ID || "";
export const entraApiScope = process.env.NEXT_PUBLIC_ENTRA_API_SCOPE || "";
export const entraRedirectUri = process.env.NEXT_PUBLIC_ENTRA_REDIRECT_URI || "";

export const isDevelopmentAuth = authMode.toLowerCase() === "development";
export const isDeployedRuntime = ["staging", "production", "managedapplicationreviewer"].includes(
  runtimeProfile.toLowerCase()
);
export const canUseLocalTestDoubles = !isDeployedRuntime && isDevelopmentAuth;
export const canUseEntraBrowserAuth =
  !isDevelopmentAuth && Boolean(entraTenantId && entraClientId && entraApiScope);

let apiAccessToken = "";

export function setApiAccessToken(token: string | null) {
  apiAccessToken = token ?? "";
}

const ACTIVE_WORKSPACE_STORAGE_KEY = "iip.active-workspace-id";
// The header the backend re-validates against the user's real membership before honoring
// (see RuntimeClaimTypes.RequestedWorkspaceHeaderName), never trusted client-side.
export const requestedWorkspaceHeaderName = "X-Requested-Workspace-Id";

/** The workspace the user last chose via the settings workspace switcher, if any. */
export function getActiveWorkspaceOverride(): string | null {
  if (typeof window === "undefined") return null;
  try {
    return window.localStorage.getItem(ACTIVE_WORKSPACE_STORAGE_KEY);
  } catch {
    return null;
  }
}

export function setActiveWorkspaceOverride(workspaceId: string | null) {
  if (typeof window === "undefined") return;
  try {
    if (workspaceId) {
      window.localStorage.setItem(ACTIVE_WORKSPACE_STORAGE_KEY, workspaceId);
    } else {
      window.localStorage.removeItem(ACTIVE_WORKSPACE_STORAGE_KEY);
    }
  } catch {
    // Best effort, falling back to the server's default workspace is safe.
  }
}

/**
 * Builds local-development scope and identity headers for an API call.
 *
 * Security: browser-supplied tenant/workspace/user/role headers are an explicit
 * local development affordance only. They are attached ONLY when the portal is
 * running with local test doubles enabled. In any deployed runtime, production
 * identity, permissions, tenant, and workspace scope must be derived server-side
 * from authenticated claims; the API rejects demo identity headers outside
 * Development mode.
 */
export function scopedHeaders(role = "ClaimsOfficer", userId = ""): Record<string, string> {
  const headers: Record<string, string> = {};
  const workspaceOverride = getActiveWorkspaceOverride();

  if (canUseLocalTestDoubles) {
    if (defaultTenantId) {
      headers["X-Tenant-Id"] = defaultTenantId;
    }

    // A workspace chosen via the settings switcher overrides the configured default,
    // trusted here only because local development headers are already fully trusted scope.
    const effectiveWorkspaceId = workspaceOverride || defaultWorkspaceId;
    if (effectiveWorkspaceId) {
      headers["X-Workspace-Id"] = effectiveWorkspaceId;
    }

    headers["x-demo-role"] = role;
    // Fall back to the configured local development user so a caller that omits
    // the userId (e.g. the /app parity-data hooks) still supplies an identity,
    // the API's tenant-context middleware needs one. No effect in deployed
    // runtimes, where localDevelopmentUserId is empty and bearer auth is used.
    const effectiveUserId = userId || localDevelopmentUserId;
    if (effectiveUserId) {
      headers["x-demo-user-id"] = effectiveUserId;
      headers["X-User-Id"] = effectiveUserId;
    }
  } else {
    if (apiAccessToken) {
      headers.Authorization = `Bearer ${apiAccessToken}`;
    }

    // A hint only, the API re-validates it against the signed-in user's real membership
    // and fails closed if they don't belong to it (never client-side scope substitution).
    if (workspaceOverride) {
      headers[requestedWorkspaceHeaderName] = workspaceOverride;
    }
  }

  return headers;
}
