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

/**
 * Presentation mode hides the environment banner and the provider-health message so a demonstration
 * is not interrupted by status chrome. Both are honest indicators, so this is honoured only in the
 * profiles that are demonstrations by definition (Demo, LocalTest, Development) and ignored in
 * Staging, Production and any unknown profile, where they must always show.
 */
export const presentationMode =
  process.env.NEXT_PUBLIC_PRESENTATION_MODE === "true" && ["Demo", "LocalTest", "Development"].includes(runtimeProfile);
export const entraTenantId = process.env.NEXT_PUBLIC_ENTRA_TENANT_ID || "";
export const entraClientId =
  process.env.NEXT_PUBLIC_ENTRA_CLIENT_ID || process.env.NEXT_PUBLIC_ENTRA_PORTAL_CLIENT_ID || "";
export const entraApiScope = process.env.NEXT_PUBLIC_ENTRA_API_SCOPE || "";
export const entraRedirectUri = process.env.NEXT_PUBLIC_ENTRA_REDIRECT_URI || "";

/** "organizations" lets work accounts from any company sign in (the SaaS product); otherwise only IIP's own
 *  directory. Needs the app registration set to accept accounts from any organisation. */
export const entraAuthorityTenant = process.env.NEXT_PUBLIC_ENTRA_AUTHORITY_TENANT || entraTenantId;

/** Entra External ID, for Google and email-code sign-in (as CIP). For example
 *  https://contoso.ciamlogin.com/{tenant-id}. Absent: only Microsoft work accounts can sign in. */
export const externalIdAuthority = (process.env.NEXT_PUBLIC_EXTERNAL_ID_AUTHORITY || "").replace(/\/$/, "");
export const externalIdClientId = process.env.NEXT_PUBLIC_EXTERNAL_ID_CLIENT_ID || "";
export const externalIdApiScope = process.env.NEXT_PUBLIC_EXTERNAL_ID_API_SCOPE || "";

export const isDevelopmentAuth = authMode.toLowerCase() === "development";
export const isDeployedRuntime = ["staging", "production", "managedapplicationreviewer"].includes(
  runtimeProfile.toLowerCase()
);
export const isDemonstrationRuntime = runtimeProfile.toLowerCase() === "demo";
export const canUseLocalTestDoubles = !isDeployedRuntime && isDevelopmentAuth;
export const canUseEntraBrowserAuth =
  !isDevelopmentAuth && Boolean(entraTenantId && entraClientId && entraApiScope);
export const canUseExternalIdSignIn =
  !isDevelopmentAuth && Boolean(externalIdAuthority && externalIdClientId && externalIdApiScope);

let apiAccessToken = "";

export function setApiAccessToken(token: string | null) {
  apiAccessToken = token ?? "";
}

/** Where the platform's own sign-in keeps its session for this tab (see lib/platformAuth). */
export const platformSessionKey = "iip.platform.session";

/** The token held in memory, or else the one a platform sign-in stored for this tab, so a page reload stays signed in. */
function currentAccessToken(): string {
  if (apiAccessToken) return apiAccessToken;
  if (typeof window === "undefined") return "";
  try {
    const raw = window.sessionStorage.getItem(platformSessionKey);
    if (!raw) return "";
    const session = JSON.parse(raw) as { accessToken?: string; expiresAt?: string };
    return session.accessToken && session.expiresAt && new Date(session.expiresAt).getTime() > Date.now() ? session.accessToken : "";
  } catch {
    return "";
  }
}

const ACTIVE_WORKSPACE_STORAGE_KEY = "iip.active-workspace-id";
const ACTIVE_ORGANISATION_STORAGE_KEY = "iip.active-organisation-id";
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

/** A remembered demonstration selection for display and recovery only. It is never request authority. */
export function getActiveOrganisationOverride(): string | null {
  if (typeof window === "undefined") return null;
  try {
    return window.localStorage.getItem(ACTIVE_ORGANISATION_STORAGE_KEY);
  } catch {
    return null;
  }
}

export function setActiveOrganisationOverride(organisationId: string | null, workspaceId: string | null = null) {
  if (typeof window === "undefined") return;
  try {
    if (organisationId) window.localStorage.setItem(ACTIVE_ORGANISATION_STORAGE_KEY, organisationId);
    else window.localStorage.removeItem(ACTIVE_ORGANISATION_STORAGE_KEY);
  } catch {
    // The server remains the authority if browser storage is unavailable.
  }
  if (workspaceId !== null || organisationId === null) setActiveWorkspaceOverride(workspaceId);
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

  // A person who has signed in with the platform's own sign-in is a real identity, even on a developer machine:
  // their token, not the developer headers, says who they are.
  const accessToken = currentAccessToken();
  if (canUseLocalTestDoubles && !accessToken) {
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
    if (accessToken) {
      headers.Authorization = `Bearer ${accessToken}`;
    }

    // A hint only, the API re-validates it against the signed-in user's real membership
    // and fails closed if they don't belong to it (never client-side scope substitution).
    if (workspaceOverride) {
      headers[requestedWorkspaceHeaderName] = workspaceOverride;
    }

    // The organisation (a demonstration world, or one of several the person belongs to) is a hint too: the
    // membership store confirms the person belongs to it, and the API fails closed if they do not.
    // Without it the world switch changes the screen but every request still reads the home organisation.
    const organisationOverride = getActiveOrganisationOverride();
    if (organisationOverride) {
      headers["X-Organisation-Id"] = organisationOverride;
    }
  }

  return headers;
}
