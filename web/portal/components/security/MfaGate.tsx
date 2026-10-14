"use client";

import { useCallback, useEffect, useState } from "react";
import { Modal } from "@/components/ui/Modal";
import { Button } from "@/components/ui/Button";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { useRuntimeContext } from "@/lib/runtimeContext";
import { getMfaStatus, snoozeMfa, type MfaStatus } from "@/lib/security/mfa";

// Mounted once inside the app. When the organisation requires a second step it sends a person who has set one up
// but not verified to the verify page, and asks a person who has not set one up to do so (or to postpone for a day).
// It does nothing when the policy is off, and never blocks the screen if the status cannot be read: the API itself
// refuses protected requests, so this is the helpful half, not the enforcing half.

const RECHECK_MS = 5 * 60 * 1000;

export default function MfaGate() {
  const { runtimeContext } = useRuntimeContext();
  const role = runtimeContext?.role;
  const [status, setStatus] = useState<MfaStatus | null>(null);
  const [dismissed, setDismissed] = useState(false);
  const [busy, setBusy] = useState(false);

  const check = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE || !runtimeContext) return;
    try {
      const next = await getMfaStatus(role);
      setStatus(next);
      if (next.mustVerify && !window.location.pathname.startsWith("/security/verify")) {
        window.location.assign(`/security/verify?next=${encodeURIComponent(window.location.pathname + window.location.search)}`);
      }
    } catch {
      // The status could not be read. The API still enforces; do not block the screen over it.
    }
  }, [role, runtimeContext]);

  useEffect(() => {
    void check();
    const timer = window.setInterval(() => void check(), RECHECK_MS);
    const onFocus = () => void check();
    window.addEventListener("focus", onFocus);
    return () => {
      window.clearInterval(timer);
      window.removeEventListener("focus", onFocus);
    };
  }, [check]);

  const open = Boolean(status?.shouldPrompt) && !dismissed;
  if (!open || !status) return null;

  return (
    <Modal open title="Set up your second step" onClose={() => setDismissed(true)} width={480}>
      <p className="m-0 text-[13.5px] text-dls-text-secondary">
        Your organisation asks everyone to prove it is them after signing in. It takes about two minutes with an authenticator app on your phone.
      </p>
      <div className="mt-4 flex flex-wrap gap-2">
        <Button
          variant="primary"
          onClick={() => {
            window.dispatchEvent(new CustomEvent("iip:open-settings", { detail: { section: "security" } }));
            setDismissed(true);
          }}
        >
          Set it up now
        </Button>
        {status.canSnooze ? (
          <Button
            variant="secondary"
            disabled={busy}
            onClick={() => {
              setBusy(true);
              void snoozeMfa(role).finally(() => {
                setBusy(false);
                setDismissed(true);
                void check();
              });
            }}
          >
            Remind me tomorrow
          </Button>
        ) : (
          <p className="m-0 self-center text-[12.5px] text-dls-text-muted">It cannot be postponed any longer.</p>
        )}
      </div>
    </Modal>
  );
}
