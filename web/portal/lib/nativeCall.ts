"use client";

/**
 * What the Android app is told about a live conversation so it can keep the call going with the phone in a
 * pocket (a foreground service with Mute and Hang up in its notification), and what it can tell the page back.
 * In a browser none of this exists and every function here does nothing.
 */

type Bridge = {
  callStarted?: () => void;
  callEnded?: () => void;
  callMuted?: (muted: boolean) => void;
  isLocked?: () => boolean;
};

function bridge(): Bridge | undefined {
  return (window as unknown as { IipNative?: Bridge }).IipNative;
}

function tell(action: (native: Bridge) => void) {
  try {
    const native = bridge();
    if (native) action(native);
  } catch {
    /* an older app without the call bridge: the call simply is not held by a service */
  }
}

export const nativeCall = {
  started: () => tell((native) => native.callStarted?.()),
  ended: () => tell((native) => native.callEnded?.()),
  muted: (muted: boolean) => tell((native) => native.callMuted?.(muted)),
};

/** Mute or Hang up pressed in the phone's notification. Returns a function that stops listening. */
export function onNativeCallAction(handlers: { mute: () => void; hangUp: () => void }): () => void {
  const mute = () => handlers.mute();
  const hangUp = () => handlers.hangUp();
  window.addEventListener("iip:native-mute", mute);
  window.addEventListener("iip:native-hangup", hangUp);
  return () => {
    window.removeEventListener("iip:native-mute", mute);
    window.removeEventListener("iip:native-hangup", hangUp);
  };
}
