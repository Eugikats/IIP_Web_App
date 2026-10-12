"use client";

import {
  BrowserCacheLocation,
  InteractionRequiredAuthError,
  PublicClientApplication,
  type AuthenticationResult,
} from "@azure/msal-browser";
import {
  canUseEntraBrowserAuth,
  canUseExternalIdSignIn,
  entraApiScope,
  entraAuthorityTenant,
  entraClientId,
  entraRedirectUri,
  externalIdApiScope,
  externalIdAuthority,
  externalIdClientId,
  setApiAccessToken,
} from "@/lib/config";

/** Which directory a person signs in through: Microsoft work accounts, or External ID (Google, email code). */
export type SignInProvider = "work" | "external";
export type SignInMethod = "microsoft" | "google" | "email";

const PROVIDER_KEY = "iip-sign-in-provider";

function currentProvider(): SignInProvider {
  try {
    return window.localStorage.getItem(PROVIDER_KEY) === "external" && canUseExternalIdSignIn ? "external" : "work";
  } catch {
    return "work";
  }
}

function rememberProvider(provider: SignInProvider) {
  try {
    window.localStorage.setItem(PROVIDER_KEY, provider);
  } catch {
    // Storage can be blocked; work accounts are the default.
  }
}

function scopeFor(provider: SignInProvider) {
  return provider === "external" ? externalIdApiScope : entraApiScope;
}

/** Starts sign-in with the chosen method. Google and email go through External ID, as in CIP. */
export async function startSignIn(method: SignInMethod): Promise<void> {
  const provider: SignInProvider = method === "microsoft" ? "work" : "external";
  if (provider === "external" && !canUseExternalIdSignIn) {
    throw Object.assign(new Error("not_configured"), { errorCode: "not_configured" });
  }

  rememberProvider(provider);
  // Pressing the button is a fresh, deliberate sign-in: an earlier one that was abandoned (back button, closed
  // tab) must not block it.
  clearStaleInteraction();
  const msal = await getClient(provider);
  await msal.loginRedirect({
    scopes: [scopeFor(provider)],
    redirectUri: redirectUri(),
    prompt: method === "email" ? "login" : "select_account",
    // Capital G: External ID rejects a lowercase "google" hint (CIP found this the hard way).
    ...(method === "google" ? { extraQueryParameters: { domain_hint: "Google" } } : {}),
  });
}

const postLogoutPath = "/";

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

    clearStaleInteraction();
    redirectResult = null;
    return getEntraApiAccessTokenOnce();
  }
}

/** Removes MSAL's "interaction in progress" marker, left behind when a sign-in was interrupted. */
function clearStaleInteraction() {
  if (typeof window === "undefined") return;
  for (const store of [window.sessionStorage, window.localStorage]) {
    try {
      for (const key of Object.keys(store)) {
        if (key.includes("interaction.status")) store.removeItem(key);
      }
    } catch {
      // Storage can be blocked; the attempt that follows will report the problem.
    }
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
  if (/not_configured/.test(text)) {
    return "This way of signing in has not been set up yet. Use Continue with Microsoft.";
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

  const provider = currentProvider();
  const msal = await getClient(provider);
  const redirected = await getRedirectResult(msal);
  const account =
    redirected?.account ??
    msal.getActiveAccount() ??
    msal.getAllAccounts()[0] ??
    null;

  if (!account) {
    await msal.loginRedirect({
      scopes: [scopeFor(provider)],
      redirectUri: redirectUri(),
    });
    return null;
  }

  msal.setActiveAccount(account);

  try {
    const result = await msal.acquireTokenSilent({
      account,
      scopes: [scopeFor(provider)],
    });
    setApiAccessToken(result.accessToken);
    return result.accessToken;
  } catch (error) {
    if (error instanceof InteractionRequiredAuthError) {
      await msal.acquireTokenRedirect({
        account,
        scopes: [scopeFor(provider)],
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

  const msal = await getClient(currentProvider());
  const account = msal.getActiveAccount() ?? msal.getAllAccounts()[0] ?? null;
  setApiAccessToken(null);
  await msal.logoutRedirect({
    account,
    postLogoutRedirectUri: window.location.origin + postLogoutPath,
  });
}

const clients = new Map<SignInProvider, Promise<PublicClientApplication>>();

function getClient(provider: SignInProvider): Promise<PublicClientApplication> {
  const existing = clients.get(provider);
  if (existing) {
    return existing;
  }

  const external = provider === "external";
  const instance = new PublicClientApplication({
    auth: {
      clientId: external ? externalIdClientId : entraClientId,
      authority: external ? externalIdAuthority : `https://login.microsoftonline.com/${entraAuthorityTenant}`,
      ...(external ? { knownAuthorities: [new URL(externalIdAuthority).host] } : {}),
      redirectUri: typeof window === "undefined" ? undefined : redirectUri(),
    },
    cache: {
      cacheLocation: BrowserCacheLocation.SessionStorage,
    },
  });

  const ready = instance.initialize().then(() => instance);
  clients.set(provider, ready);
  return ready;
}

function redirectUri() {
  // Come back to the address the person started on. The sign-in's state is kept per address, so returning to a
  // different one (the old and new addresses of the same site) loses it and the sign-in begins again, over and over.
  if (!entraRedirectUri) return window.location.origin + "/app";
  try {
    return window.location.origin + new URL(entraRedirectUri).pathname;
  } catch {
    return entraRedirectUri;
  }
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
