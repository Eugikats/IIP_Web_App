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

/**
 * A sign-in that was interrupted (a closed tab, the back button) leaves MSAL's "interaction in progress"
 * marker behind, and every later attempt then fails until it is cleared. Clear it and try once more.
 */
export async function getEntraApiAccessToken(): Promise<string | null> {
  try {
    return await getEntraApiAccessTokenOnce();
  } catch (error) {
    if (errorCode(error) !== "interaction_in_progress" || typeof window === "undefined") {
      throw error;
    }

    for (const store of [window.sessionStorage, window.localStorage]) {
      try {
        for (const key of Object.keys(store)) {
          if (key.includes("interaction.status")) store.removeItem(key);
        }
      } catch {
        // Storage can be blocked; the retry below will report the problem.
      }
    }

    redirectResult = null;
    return getEntraApiAccessTokenOnce();
  }
}

/** A plain reason for a failed sign-in, for the person signing in. Never the raw provider message. */
export function describeSignInError(error: unknown): string {
  const code = errorCode(error);
  const text = `${code} ${error instanceof Error ? error.message : ""}`;
  if (/AADSTS50020|AADSTS50177|AADSTS500200/.test(text)) {
    return "This Microsoft account is not part of the organisation that runs IIP. Sign in with your work account, or ask your IIP administrator to invite this one.";
  }
  if (/AADSTS50105|AADSTS53003/.test(text)) {
    return "Your account has not been given access to IIP. Ask your IIP administrator to add you.";
  }
  if (/AADSTS65001|consent_required/.test(text)) {
    return "IIP has not been approved for your organisation yet. Your Microsoft administrator needs to approve it.";
  }
  if (/AADSTS50011|redirect_uri/.test(text)) {
    return "This IIP address is not registered for sign-in. Tell your IIP administrator which address you used.";
  }
  if (/user_cancelled|access_denied/.test(text)) {
    return "Sign-in was cancelled. Try again when you are ready.";
  }
  if (/interaction_in_progress/.test(text)) {
    return "An earlier sign-in is still open in another tab. Close the other IIP tabs and try again.";
  }
  if (/network|timed_out|Failed to fetch/i.test(text)) {
    return "Could not reach Microsoft sign-in. Check your connection and try again.";
  }
  return `Sign-in could not be completed. Try again, or contact your IIP administrator${code ? ` and quote "${code}"` : ""}.`;
}

function errorCode(error: unknown): string {
  if (error && typeof error === "object") {
    const record = error as { errorCode?: unknown; message?: unknown };
    if (typeof record.errorCode === "string" && record.errorCode) return record.errorCode;
    const found = typeof record.message === "string" ? /AADSTS\d+/.exec(record.message) : null;
    if (found) return found[0];
  }
  return "";
}

async function getEntraApiAccessTokenOnce(): Promise<string | null> {
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
  // Sign-in returns to the workspace, which finishes it; "/" is the public landing page.
  return entraRedirectUri || window.location.origin + "/app";
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
