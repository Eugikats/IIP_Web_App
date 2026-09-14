"use client";

import {
  BrowserCacheLocation,
  InteractionRequiredAuthError,
  PublicClientApplication,
  type AuthenticationResult,
} from "@azure/msal-browser";
import {
  canUseEntraBrowserAuth,
  entraApiScope,
  entraClientId,
  entraRedirectUri,
  entraTenantId,
  setApiAccessToken,
} from "@/lib/config";

const postLogoutPath = "/";

let client: PublicClientApplication | null = null;
let initialized: Promise<PublicClientApplication> | null = null;
let redirectResult: Promise<AuthenticationResult | null> | null = null;

export async function getEntraApiAccessToken(): Promise<string | null> {
  if (!canUseEntraBrowserAuth || typeof window === "undefined") {
    setApiAccessToken(null);
    return null;
  }

  const msal = await getClient();
  const redirected = await getRedirectResult(msal);
  const account =
    redirected?.account ??
    msal.getActiveAccount() ??
    msal.getAllAccounts()[0] ??
    null;

  if (!account) {
    await msal.loginRedirect({
      scopes: [entraApiScope],
      redirectUri: redirectUri(),
    });
    return null;
  }

  msal.setActiveAccount(account);

  try {
    const result = await msal.acquireTokenSilent({
      account,
      scopes: [entraApiScope],
    });
    setApiAccessToken(result.accessToken);
    return result.accessToken;
  } catch (error) {
    if (error instanceof InteractionRequiredAuthError) {
      await msal.acquireTokenRedirect({
        account,
        scopes: [entraApiScope],
        redirectUri: redirectUri(),
      });
      return null;
    }

    setApiAccessToken(null);
    throw error;
  }
}

/**
 * Ends the signed-in session. Outside Entra mode (local development / visual parity),
 * identity is header-driven per request, there is no browser session to destroy, so
 * this just clears the cached API token and returns to the sign-in landing page.
 */
export async function signOut(): Promise<void> {
  if (!canUseEntraBrowserAuth || typeof window === "undefined") {
    setApiAccessToken(null);
    window.location.assign(postLogoutPath);
    return;
  }

  const msal = await getClient();
  const account = msal.getActiveAccount() ?? msal.getAllAccounts()[0] ?? null;
  setApiAccessToken(null);
  await msal.logoutRedirect({
    account,
    postLogoutRedirectUri: window.location.origin + postLogoutPath,
  });
}

function getClient(): Promise<PublicClientApplication> {
  if (initialized) {
    return initialized;
  }

  client = new PublicClientApplication({
    auth: {
      clientId: entraClientId,
      authority: `https://login.microsoftonline.com/${entraTenantId}`,
      redirectUri: typeof window === "undefined" ? undefined : redirectUri(),
    },
    cache: {
      cacheLocation: BrowserCacheLocation.SessionStorage,
    },
  });

  initialized = client.initialize().then(() => client!);
  return initialized;
}

function redirectUri() {
  return entraRedirectUri || window.location.origin + "/";
}

function getRedirectResult(msal: PublicClientApplication): Promise<AuthenticationResult | null> {
  redirectResult ??= msal.handleRedirectPromise().then((result) => {
    if (result?.account) {
      msal.setActiveAccount(result.account);
      setApiAccessToken(result.accessToken);
    }

    return result;
  });

  return redirectResult;
}
