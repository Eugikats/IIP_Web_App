"use client";

import { useEffect, useState } from "react";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { cn } from "@/lib/utils";
import { METHOD_LABELS, MfaError, sendMfaCode, verifyMfa, type MfaMethod, type MfaStatus } from "@/lib/security/mfa";
import { passkeyCancelled, passkeysSupported, verifyWithPasskey } from "@/lib/security/passkeys";

// Prove it is you with a method you set up: an authenticator code, a code sent to you, or a backup code. Used on
// the verify page and inside Settings when a change needs a recent check. Passkeys verify through the browser's own
// prompt and are not offered here.

const LOGOS: Partial<Record<MfaMethod, string>> = {
  totp: "/logos/mfa/microsoft-authenticator.svg",
  email_otp: "/logos/mfa/email.svg",
  sms_otp: "/logos/mfa/sms.svg",
  backup_codes: "/logos/mfa/backup-codes.svg",
  passkey: "/logos/mfa/passkey.svg",
};

const HINTS: Partial<Record<MfaMethod, string>> = {
  totp: "Open your authenticator app and type the six digit code for IIP.",
  email_otp: "We will email a six digit code to your sign-in address.",
  sms_otp: "We will text a six digit code to the number you saved.",
  backup_codes: "Type one of the backup codes you saved. Each works once.",
  passkey: "Use the passkey or security key you set up. Your device will ask you to confirm.",
};

export function usableMethods(status: MfaStatus): MfaMethod[] {
  return status.enrolledMethods.filter((method) => status.offeredMethods.includes(method) && (method !== "passkey" || passkeysSupported()));
}

export default function VerifyForm({ status, onVerified, role }: { status: MfaStatus; onVerified: () => void; role?: string }) {
  const methods = usableMethods(status);
  const [method, setMethod] = useState<MfaMethod>(methods[0] ?? "totp");
  const [code, setCode] = useState("");
  const [busy, setBusy] = useState(false);
  const [sent, setSent] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setCode("");
    setSent(null);
    setError(null);
  }, [method]);

  if (methods.length === 0) {
    return <p className="m-0 text-[13px] text-dls-text-secondary">There is no method here that can verify you. Ask an administrator to reset your methods.</p>;
  }

  const needsSend = method === "email_otp" || method === "sms_otp";

  async function send() {
    setBusy(true);
    setError(null);
    try {
      const result = await sendMfaCode(method === "email_otp" ? "email" : "sms", "login", undefined, role);
      setSent(`Code sent to ${result.destination}.`);
    } catch (failure) {
      setError(failure instanceof MfaError ? failure.message : "The code could not be sent.");
    } finally {
      setBusy(false);
    }
  }

  async function usePasskey() {
    setBusy(true);
    setError(null);
    try {
      await verifyWithPasskey(role);
      onVerified();
    } catch (failure) {
      setError(passkeyCancelled(failure) ? "The passkey prompt was closed. Try again." : failure instanceof MfaError ? failure.message : "That did not work. Try again.");
      setBusy(false);
    }
  }

  async function submit() {
    setBusy(true);
    setError(null);
    try {
      await verifyMfa(method, code, role);
      onVerified();
    } catch (failure) {
      setError(failure instanceof MfaError ? failure.message : "That did not work. Try again.");
      setBusy(false);
    }
  }

  return (
    <div>
      {methods.length > 1 ? (
        <div role="tablist" aria-label="How to verify" className="mb-4 flex flex-wrap gap-2">
          {methods.map((item) => (
            <button
              key={item}
              type="button"
              role="tab"
              aria-selected={method === item}
              onClick={() => setMethod(item)}
              className={cn(
                "inline-flex items-center gap-2 rounded-dls-control border px-3 py-2 text-[13px] font-semibold transition-colors",
                method === item ? "border-dls-primary bg-dls-primary-soft text-dls-primary" : "border-dls-border-input bg-dls-surface text-dls-text-secondary hover:bg-dls-bg-page",
              )}
            >
              {LOGOS[item] ? <img src={LOGOS[item]} alt="" className="h-4 w-4" /> : null}
              {METHOD_LABELS[item]}
            </button>
          ))}
        </div>
      ) : null}

      <p className="m-0 text-[13px] text-dls-text-secondary">{HINTS[method]}</p>

      {method === "passkey" ? (
        <Button variant="primary" className="mt-3" disabled={busy} onClick={() => void usePasskey()}>
          {busy ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : null}
          Use my passkey
        </Button>
      ) : needsSend && !sent ? (
        <Button variant="secondary" className="mt-3" disabled={busy || status.lockedOut} onClick={() => void send()}>
          {busy ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : null}
          Send me a code
        </Button>
      ) : (
        <form
          className="mt-3 flex flex-wrap items-start gap-2"
          onSubmit={(event) => {
            event.preventDefault();
            void submit();
          }}
        >
          <div className="w-44">
            <Input
              value={code}
              onChange={(event) => setCode(event.target.value)}
              inputMode={method === "backup_codes" ? "text" : "numeric"}
              autoComplete="one-time-code"
              autoFocus
              maxLength={method === "backup_codes" ? 12 : 8}
              placeholder={method === "backup_codes" ? "XXXXX-XXXXX" : "123456"}
              aria-label="Verification code"
              invalid={Boolean(error)}
            />
          </div>
          <Button variant="primary" type="submit" disabled={busy || code.trim().length < 6 || status.lockedOut}>
            {busy ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : null}
            Verify
          </Button>
        </form>
      )}

      {sent ? <p role="status" className="m-0 mt-2 text-[12.5px] text-dls-text-muted">{sent}</p> : null}
      {status.lockedOut ? (
        <p role="alert" className="m-0 mt-2 text-[12.5px] text-dls-danger-text">Too many wrong codes. Try again in a few minutes.</p>
      ) : error ? (
        <p role="alert" className="m-0 mt-2 text-[12.5px] text-dls-danger-text">{error}</p>
      ) : null}
    </div>
  );
}
