"use client";

/**
 * What the Android app offers beyond the live call: listening for "Hey Kato" while the screen is off, a short check for
 * things that need the person while the app is closed, and the narrow credential those two checks use. In a browser none of
 * this exists, `available()` is false and every other function here does nothing.
 *
 * The credential is not the sign-in token. It can only ask the server for the next nudge and whether a short clip said
 * "Hey Kato"; it lives in the app's private storage, never in the page, and is revoked on sign-out.
 */

type DeviceBridge = {
  handsFreeAvailable?: () => boolean;
  wakeOn?: () => boolean;
  nudgesOn?: () => boolean;
  setWake?: (wanted: boolean) => void;
  setNudges?: (wanted: boolean) => void;
  hasDeviceCheck?: () => boolean;
  saveDeviceCheck?: (token: string, apiBase: string) => boolean;
  clearDeviceCheck?: () => string;
  takeTalk?: () => boolean;
  wakeState?: () => string;
};

function bridge(): DeviceBridge | undefined {
  if (typeof window === "undefined") return undefined;
  return (window as unknown as { IipNative?: DeviceBridge }).IipNative;
}

function ask<T>(fallback: T, action: (native: DeviceBridge) => T | undefined): T {
  try {
    const native = bridge();
    if (!native) return fallback;
    const result = action(native);
    return result === undefined ? fallback : result;
  } catch {
    /* an older app without these methods: the feature is simply not there */
    return fallback;
  }
}

export type WakeVerdict = "pending" | "yes" | "no";

export const nativeDevice = {
  /** This is the Android app and it is new enough to listen for the wake phrase and check for nudges. */
  available: () => ask(false, (native) => (native.setWake && native.setNudges && native.handsFreeAvailable ? native.handsFreeAvailable() : false)),
  wakeOn: () => ask(false, (native) => native.wakeOn?.()),
  nudgesOn: () => ask(false, (native) => native.nudgesOn?.()),
  setWake: (wanted: boolean) => ask(undefined, (native) => { native.setWake?.(wanted); return undefined; }),
  setNudges: (wanted: boolean) => ask(undefined, (native) => { native.setNudges?.(wanted); return undefined; }),
  hasCredential: () => ask(false, (native) => native.hasDeviceCheck?.()),
  saveCredential: (token: string, apiBase: string) => ask(false, (native) => native.saveDeviceCheck?.(token, apiBase)),
  /** Forgets the credential on the phone and returns what it held (empty when none) so the server can revoke it. */
  clearCredential: () => ask("", (native) => native.clearDeviceCheck?.()),
  /** True once after "Talk now" or the wake phrase asked for a conversation. */
  takeTalk: () => ask(false, (native) => native.takeTalk?.()),
  /** The server's verdict on the last wake phrase. An app that cannot say counts as yes. */
  wakeVerdict: (): WakeVerdict =>
    ask<WakeVerdict>("yes", (native) => {
      const state = native.wakeState?.();
      return state === "pending" || state === "no" ? state : "yes";
    }),
};

/** The phone asked for a conversation (the wake phrase or the nudge's Talk now). Returns a function that stops listening. */
export function onNativeTalk(handler: () => void): () => void {
  window.addEventListener("iip:native-talk", handler);
  return () => window.removeEventListener("iip:native-talk", handler);
}

/** The person changed Hey Kato or nudges, or the microphone was allowed. Returns a function that stops listening. */
export function onNativeSettings(handler: () => void): () => void {
  window.addEventListener("iip:native-settings", handler);
  return () => window.removeEventListener("iip:native-settings", handler);
}
