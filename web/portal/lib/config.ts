export const apiBaseUrl = process.env.NEXT_PUBLIC_API_BASE_URL || "http://127.0.0.1:5167";
export const defaultTenantId = process.env.NEXT_PUBLIC_TENANT_ID || "";
export const defaultWorkspaceId = process.env.NEXT_PUBLIC_WORKSPACE_ID || "";
export const localDevelopmentUserId = process.env.NEXT_PUBLIC_LOCAL_USER_ID || "";
export const authMode = process.env.NEXT_PUBLIC_AUTH_MODE || "Development";
export const runtimeProfile = process.env.NEXT_PUBLIC_RUNTIME_PROFILE || "Development";
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
export function scopedHeaders(role = "Client", userId = ""): Record<string, string> {
  const headers: Record<string, string> = {};

  if (canUseLocalTestDoubles) {
    if (defaultTenantId) {
      headers["X-Tenant-Id"] = defaultTenantId;
    }

    if (defaultWorkspaceId) {
      headers["X-Workspace-Id"] = defaultWorkspaceId;
    }

    headers["x-demo-role"] = role;
    if (userId) {
      headers["x-demo-user-id"] = userId;
      headers["X-User-Id"] = userId;
    }
  } else if (apiAccessToken) {
    headers.Authorization = `Bearer ${apiAccessToken}`;
  }

  return headers;
}
