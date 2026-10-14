"use client";

import { useEffect, useState } from "react";
import QRCode from "qrcode";
import { Check, Copy, Loader2 } from "lucide-react";
import { Modal } from "@/components/ui/Modal";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { cn } from "@/lib/utils";
import { beginTotp, confirmMfaCode, confirmTotp, MfaError, rotateBackupCodes, sendMfaCode } from "@/lib/security/mfa";

// The set-up steps for each method. An authenticator app shows its QR code and its secret once; email and SMS
// send a code to type back; backup codes are shown once and never again.

type Done = () => void;

function errorText(failure: unknown, fallback: string) {
  return failure instanceof MfaError ? failure.message : fallback;
}

export function TotpSetupDialog({ open, onClose, onDone, role }: { open: boolean; onClose: () => void; onDone: Done; role?: string }) {
  const [app, setApp] = useState<"microsoft" | "google" | null>(null);
  const [enrolment, setEnrolment] = useState<{ id: string; secret: string; provisioningUri: string } | null>(null);
  const [qr, setQr] = useState<string | null>(null);
  const [code, setCode] = useState("");
  const [busy, setBusy] = useState(false);
  const [copied, setCopied] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) {
      setApp(null);
      setEnrolment(null);
      setQr(null);
      setCode("");
      setError(null);
    }
  }, [open]);

  useEffect(() => {
    if (!enrolment) return;
    let cancelled = false;
    void QRCode.toDataURL(enrolment.provisioningUri, { width: 192, margin: 2 }).then((url) => {
      if (!cancelled) setQr(url);
    });
    return () => {
      cancelled = true;
    };
  }, [enrolment]);

  async function choose(next: "microsoft" | "google") {
    setApp(next);
    setBusy(true);
    setError(null);
    try {
      setEnrolment(await beginTotp(next === "microsoft" ? "Microsoft Authenticator" : "Google Authenticator", role));
    } catch (failure) {
      setError(errorText(failure, "Set-up could not be started."));
      setApp(null);
    } finally {
      setBusy(false);
    }
  }

  async function confirm() {
    if (!enrolment) return;
    setBusy(true);
    setError(null);
    try {
      await confirmTotp(enrolment.id, code, role);
      onDone();
      onClose();
    } catch (failure) {
      setError(errorText(failure, "That code did not work."));
      setBusy(false);
    }
  }

  return (
    <Modal open={open} title="Set up an authenticator app" onClose={onClose} width={560}>
    <div className="p-6">
      {!enrolment ? (
        <div>
          <p className="m-0 text-[13px] text-dls-text-secondary">Choose the app on your phone. Both work the same way: you scan one code, and the app then shows a new six digit number every 30 seconds.</p>
          <div className="mt-4 grid gap-3 sm:grid-cols-2">
            {([
              ["microsoft", "Microsoft Authenticator", "/logos/mfa/microsoft-authenticator.svg"],
              ["google", "Google Authenticator", "/logos/mfa/google-authenticator.svg"],
            ] as const).map(([key, name, logo]) => (
              <button
                key={key}
                type="button"
                disabled={busy}
                onClick={() => void choose(key)}
                className={cn("flex items-center gap-3 rounded-xl border border-dls-border-subtle bg-dls-surface p-4 text-left transition-colors hover:border-dls-primary hover:bg-dls-bg-page disabled:opacity-60", app === key && "border-dls-primary")}
              >
                <img src={logo} alt="" className="h-9 w-9" />
                <span className="text-[14px] font-semibold text-dls-text-primary">{name}</span>
              </button>
            ))}
          </div>
          {busy ? <p className="m-0 mt-3 inline-flex items-center gap-2 text-[12.5px] text-dls-text-muted"><Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden /> Preparing your code</p> : null}
        </div>
      ) : (
        <div>
          <ol className="m-0 list-decimal space-y-1 pl-5 text-[13px] text-dls-text-secondary">
            <li>In {app === "microsoft" ? "Microsoft" : "Google"} Authenticator, add an account and choose to scan a QR code.</li>
            <li>Scan this code, then type the six digit number the app shows.</li>
          </ol>
          <div className="mt-4 flex flex-wrap items-start gap-5">
            <div className="rounded-xl border border-dls-border-subtle bg-white p-2">
              {qr ? <img src={qr} alt="QR code to scan with your authenticator app" width={192} height={192} /> : <div className="h-48 w-48 animate-pulse rounded bg-dls-bg-page" />}
            </div>
            <div className="min-w-0 flex-1">
              <p className="m-0 text-[12px] font-semibold uppercase tracking-wide text-dls-text-muted">Cannot scan? Type this key</p>
              <div className="mt-1 flex items-center gap-2">
                <code data-testid="totp-secret" className="break-all rounded bg-dls-bg-page px-2 py-1 text-[13px] text-dls-text-primary">{enrolment.secret}</code>
                <button
                  type="button"
                  aria-label="Copy the key"
                  className="rounded p-1.5 text-dls-text-muted hover:bg-dls-bg-page"
                  onClick={() => {
                    void navigator.clipboard?.writeText(enrolment.secret);
                    setCopied(true);
                  }}
                >
                  {copied ? <Check className="h-4 w-4" aria-hidden /> : <Copy className="h-4 w-4" aria-hidden />}
                </button>
              </div>
              <p className="m-0 mt-1 text-[12px] text-dls-text-muted">This key is shown once. Anyone who has it can make your codes.</p>
              <form
                className="mt-4 flex flex-wrap items-start gap-2"
                onSubmit={(event) => {
                  event.preventDefault();
                  void confirm();
                }}
              >
                <div className="w-36">
                  <Input value={code} onChange={(event) => setCode(event.target.value)} inputMode="numeric" autoComplete="one-time-code" maxLength={8} placeholder="123456" aria-label="Code from your app" invalid={Boolean(error)} />
                </div>
                <Button variant="primary" type="submit" disabled={busy || code.trim().length < 6}>
                  {busy ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : null}
                  Turn on
                </Button>
              </form>
              {error ? <p role="alert" className="m-0 mt-2 text-[12.5px] text-dls-danger-text">{error}</p> : null}
            </div>
          </div>
        </div>
      )}
      {!enrolment && error ? <p role="alert" className="m-0 mt-3 text-[12.5px] text-dls-danger-text">{error}</p> : null}
    </div>
    </Modal>
  );
}

export function CodeSetupDialog({ open, channel, onClose, onDone, role }: { open: boolean; channel: "email" | "sms"; onClose: () => void; onDone: Done; role?: string }) {
  const [phone, setPhone] = useState("");
  const [sentTo, setSentTo] = useState<string | null>(null);
  const [code, setCode] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) {
      setPhone("");
      setSentTo(null);
      setCode("");
      setError(null);
      setBusy(false);
    }
  }, [open]);

  async function send() {
    setBusy(true);
    setError(null);
    try {
      const result = await sendMfaCode(channel, "enroll", channel === "sms" ? phone : undefined, role);
      setSentTo(result.destination);
    } catch (failure) {
      setError(errorText(failure, "The code could not be sent."));
    } finally {
      setBusy(false);
    }
  }

  async function confirm() {
    setBusy(true);
    setError(null);
    try {
      await confirmMfaCode(channel, code, role);
      onDone();
      onClose();
    } catch (failure) {
      setError(errorText(failure, "That code did not work."));
      setBusy(false);
    }
  }

  const title = channel === "email" ? "Set up email codes" : "Set up SMS codes";
  return (
    <Modal open={open} title={title} onClose={onClose} width={480}>
    <div className="p-6">
      {!sentTo ? (
        <div>
          <p className="m-0 text-[13px] text-dls-text-secondary">
            {channel === "email" ? "We will email a six digit code to the address you sign in with. Type it back to turn this on." : "Enter your mobile number with its country code. We will text you a six digit code to type back."}
          </p>
          {channel === "sms" ? (
            <div className="mt-3 w-64">
              <Input value={phone} onChange={(event) => setPhone(event.target.value)} inputMode="tel" placeholder="+256700000000" aria-label="Mobile number" invalid={Boolean(error)} />
            </div>
          ) : null}
          <Button variant="primary" className="mt-3" disabled={busy || (channel === "sms" && phone.trim().length < 9)} onClick={() => void send()}>
            {busy ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : null}
            Send the code
          </Button>
        </div>
      ) : (
        <form
          onSubmit={(event) => {
            event.preventDefault();
            void confirm();
          }}
        >
          <p className="m-0 text-[13px] text-dls-text-secondary">We sent a code to {sentTo}. It lasts 10 minutes.</p>
          <div className="mt-3 flex flex-wrap items-start gap-2">
            <div className="w-36">
              <Input value={code} onChange={(event) => setCode(event.target.value)} inputMode="numeric" autoComplete="one-time-code" autoFocus maxLength={8} placeholder="123456" aria-label="The code we sent" invalid={Boolean(error)} />
            </div>
            <Button variant="primary" type="submit" disabled={busy || code.trim().length < 6}>
              {busy ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : null}
              Turn on
            </Button>
          </div>
        </form>
      )}
      {error ? <p role="alert" className="m-0 mt-3 text-[12.5px] text-dls-danger-text">{error}</p> : null}
    </div>
    </Modal>
  );
}

export function BackupCodesDialog({ open, onClose, onDone, role }: { open: boolean; onClose: () => void; onDone: Done; role?: string }) {
  const [codes, setCodes] = useState<string[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) {
      setCodes(null);
      setError(null);
    }
  }, [open]);

  async function make() {
    setBusy(true);
    setError(null);
    try {
      setCodes((await rotateBackupCodes(role)).codes);
      onDone();
    } catch (failure) {
      setError(errorText(failure, "The codes could not be made."));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal open={open} title="Backup codes" onClose={onClose} width={480}>
    <div className="p-6">
      {!codes ? (
        <div>
          <p className="m-0 text-[13px] text-dls-text-secondary">Backup codes let you in when you cannot use your other methods. You get eight. Each works once. Making new codes cancels any you had before.</p>
          <Button variant="primary" className="mt-3" disabled={busy} onClick={() => void make()}>
            {busy ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : null}
            Make backup codes
          </Button>
        </div>
      ) : (
        <div>
          <p className="m-0 text-[13px] text-dls-text-secondary">Save these now, somewhere safe and not on this device alone. They are shown only this once.</p>
          <ul data-testid="backup-codes" className="m-0 mt-3 grid list-none grid-cols-2 gap-2 p-0">
            {codes.map((item) => (
              <li key={item} className="rounded bg-dls-bg-page px-3 py-2 text-center font-mono text-[14px] tracking-wider text-dls-text-primary">{item}</li>
            ))}
          </ul>
          <div className="mt-3 flex gap-2">
            <Button variant="secondary" onClick={() => void navigator.clipboard?.writeText(codes.join("\n"))}>Copy all</Button>
            <Button variant="primary" onClick={onClose}>I have saved them</Button>
          </div>
        </div>
      )}
      {error ? <p role="alert" className="m-0 mt-3 text-[12.5px] text-dls-danger-text">{error}</p> : null}
    </div>
    </Modal>
  );
}
