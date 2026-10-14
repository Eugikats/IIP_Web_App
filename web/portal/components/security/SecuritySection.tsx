"use client";

import { useCallback, useEffect, useState } from "react";
import { Loader2, ShieldCheck } from "lucide-react";
import { StatusPill, type PillTone } from "@/components/ui/StatusPill";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { LoadingSkeleton } from "@/components/ui/StateSurfaces";
import { useRuntimeContext } from "@/lib/runtimeContext";
import { cn } from "@/lib/utils";
import {
  disableMfaChannel,
  getMfaMetrics,
  getMfaPolicy,
  getMfaStatus,
  METHOD_LABELS,
  MfaError,
  removeCredential,
  resetMfaUser,
  setMfaPolicy,
  type MfaMethod,
  type MfaMetrics,
  type MfaPolicy,
  type MfaStatus,
} from "@/lib/security/mfa";
import VerifyForm from "./VerifyForm";
import { BackupCodesDialog, CodeSetupDialog, PasskeySetupDialog, TotpSetupDialog } from "./SetupDialogs";
import { passkeysSupported } from "@/lib/security/passkeys";

// Settings, Security: how this person proves it is them after signing in, and (for an administrator) whether the
// organisation requires it. Modelled on CIP's Settings, Security. Off for an organisation until an administrator
// turns it on; anyone can still set a method up voluntarily.

type CardSpec = { method: MfaMethod; logo: string; title: string; text: string };

const CARDS: CardSpec[] = [
  { method: "totp", logo: "/logos/mfa/microsoft-authenticator.svg", title: "Authenticator app", text: "Microsoft Authenticator or Google Authenticator on your phone shows a new code every 30 seconds." },
  { method: "passkey", logo: "/logos/mfa/passkey.svg", title: "Passkey or security key", text: "Use your fingerprint, face or screen lock, or a security key, instead of typing a code." },
  { method: "email_otp", logo: "/logos/mfa/email.svg", title: "Email codes", text: "A six digit code is emailed to the address you sign in with." },
  { method: "sms_otp", logo: "/logos/mfa/sms.svg", title: "SMS codes", text: "A six digit code is texted to your mobile number." },
  { method: "backup_codes", logo: "/logos/mfa/backup-codes.svg", title: "Backup codes", text: "Eight one-time codes for when you cannot use your other methods." },
];

function errorText(failure: unknown, fallback: string) {
  return failure instanceof MfaError ? failure.message : fallback;
}

export default function SecuritySection() {
  const { runtimeContext } = useRuntimeContext();
  const role = runtimeContext?.role;
  const isAdmin = (runtimeContext?.permissions ?? []).some((permission) => String(permission) === "ManageTenantSettings");
  const [status, setStatus] = useState<MfaStatus | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [dialog, setDialog] = useState<"totp" | "passkey" | "email" | "sms" | "backup" | null>(null);
  const [notice, setNotice] = useState<{ tone: "ok" | "bad"; text: string } | null>(null);

  const load = useCallback(async () => {
    try {
      setStatus(await getMfaStatus(role));
      setLoadError(null);
    } catch (failure) {
      setLoadError(errorText(failure, "Your security settings could not be loaded."));
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  if (loadError && !status) return <p role="alert" className="m-0 text-[13px] text-dls-danger-text">{loadError}</p>;
  if (!status) return <LoadingSkeleton rows={4} />;

  const needsVerifyToChange = status.enrolledMethods.length > 0 && !status.verified;
  // Backup codes stand in for another method, so there has to be one first.
  const hasAnotherMethod = status.enrolledMethods.some((method) => method !== "backup_codes");
  const protectedNow = status.satisfied;
  const pill: { tone: PillTone; label: string } = protectedNow
    ? { tone: "success", label: "Protected" }
    : status.policyRequired
      ? { tone: "warning", label: "Action needed" }
      : { tone: "neutral", label: "Optional" };

  async function run(action: () => Promise<unknown>, done: string) {
    setNotice(null);
    try {
      await action();
      setNotice({ tone: "ok", text: done });
      await load();
    } catch (failure) {
      setNotice({ tone: "bad", text: errorText(failure, "That did not work.") });
      await load();
    }
  }

  return (
    <div>
      <div className="flex flex-wrap items-center gap-3">
        <h3 className="m-0 text-[16px] font-bold text-dls-text-primary">Protect your account</h3>
        <StatusPill tone={pill.tone} label={pill.label} />
      </div>
      <p className="m-0 mt-1 max-w-2xl text-[13px] text-dls-text-secondary">
        After you sign in with Microsoft or Google, IIP can ask for a second step. This is separate from your Microsoft or Google account&apos;s own 2-step verification.
        {status.policyRequired
          ? " Your organisation requires it."
          : " Your organisation has not made it compulsory, but you can turn it on for yourself."}
      </p>

      {status.shouldPrompt ? (
        <p role="status" className="m-0 mt-3 rounded-xl border border-dls-warning-dot bg-dls-warning-tint px-4 py-3 text-[13px] text-dls-text-primary">
          Your organisation requires a verification method. Set up at least {status.minMethods === 1 ? "one" : status.minMethods} below.
        </p>
      ) : null}

      {needsVerifyToChange ? (
        <section aria-label="Verify to make changes" className="mt-4 rounded-xl border border-dls-border-subtle bg-dls-surface p-4">
          <h4 className="m-0 text-[14px] font-bold text-dls-text-primary">Verify it is you to make changes</h4>
          <p className="m-0 mt-1 text-[12.5px] text-dls-text-muted">Adding or removing a method needs a recent check.</p>
          <div className="mt-3">
            <VerifyForm status={status} role={role} onVerified={() => void load()} />
          </div>
        </section>
      ) : null}

      {notice ? <p role="status" className={cn("m-0 mt-3 text-[12.5px]", notice.tone === "ok" ? "text-dls-success-text" : "text-dls-danger-text")}>{notice.text}</p> : null}

      <div className="mt-4 grid gap-3 md:grid-cols-2" data-testid="mfa-methods">
        {CARDS.map((card) => {
          const offered = status.offeredMethods.includes(card.method);
          const enrolled = status.enrolledMethods.includes(card.method) || (card.method === "backup_codes" && status.unusedBackupCodes > 0);
          const credentials = status.credentials.filter((credential) => credential.method === card.method);
          const detail =
            card.method === "sms_otp" && enrolled ? status.smsNumberMasked
            : card.method === "backup_codes" && enrolled ? `${status.unusedBackupCodes} unused`
            : credentials.map((credential) => credential.label).join(", ") || null;
          return (
            <section key={card.method} data-testid={`mfa-card-${card.method}`} className="flex flex-col rounded-xl border border-dls-border-subtle bg-dls-surface p-4">
              <div className="flex items-start gap-3">
                <img src={card.logo} alt="" className="h-9 w-9 shrink-0" />
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <h4 className="m-0 text-[14px] font-bold text-dls-text-primary">{card.title}</h4>
                    {!offered ? <StatusPill tone="neutral" label="Switched off" /> : enrolled ? <StatusPill tone="success" label="Active" /> : <StatusPill tone="neutral" label="Not set up" />}
                  </div>
                  <p className="m-0 mt-1 text-[12.5px] leading-relaxed text-dls-text-secondary">{card.text}</p>
                  {detail ? <p className="m-0 mt-1 text-[12px] text-dls-text-muted">{detail}</p> : null}
                  {card.method === "passkey" && !passkeysSupported() && offered ? <p className="m-0 mt-1 text-[12px] text-dls-text-muted">This browser cannot make passkeys.</p> : null}
                  {card.method === "backup_codes" && !hasAnotherMethod && offered ? <p className="m-0 mt-1 text-[12px] text-dls-text-muted">Set up another method first. These are for when you cannot use it.</p> : null}
                </div>
              </div>
              {offered ? (
                <div className="mt-3 flex flex-wrap items-center gap-2">
                  <Button
                    variant={enrolled ? "secondary" : "primary"}
                    disabled={needsVerifyToChange || (card.method === "backup_codes" && !hasAnotherMethod) || (card.method === "passkey" && !passkeysSupported())}
                    onClick={() => setDialog(card.method === "totp" ? "totp" : card.method === "passkey" ? "passkey" : card.method === "email_otp" ? "email" : card.method === "sms_otp" ? "sms" : "backup")}
                  >
                    {card.method === "backup_codes" ? (enrolled ? "Make new codes" : "Make codes") : enrolled ? "Add another" : "Set up"}
                  </Button>
                  {enrolled && (card.method === "totp" || card.method === "passkey")
                    ? credentials.map((credential) => (
                        <Button key={credential.id} variant="destructive" disabled={needsVerifyToChange} onClick={() => void run(() => removeCredential(card.method, credential.id, role), `${card.title} removed.`)}>
                          Remove {credential.label}
                        </Button>
                      ))
                    : null}
                  {enrolled && (card.method === "email_otp" || card.method === "sms_otp") ? (
                    <Button variant="destructive" disabled={needsVerifyToChange} onClick={() => void run(() => disableMfaChannel(card.method === "email_otp" ? "email" : "sms", role), `${card.title} turned off.`)}>
                      Turn off
                    </Button>
                  ) : null}
                </div>
              ) : null}
            </section>
          );
        })}
      </div>

      {status.verified && status.enrolledMethods.length > 0 ? (
        <p className="m-0 mt-3 inline-flex items-center gap-1.5 text-[12px] text-dls-text-muted"><ShieldCheck className="h-3.5 w-3.5" aria-hidden /> You are verified on this device until you sign out{status.verifiedUntil ? ` or until ${new Date(status.verifiedUntil).toLocaleString()}` : ""}.</p>
      ) : null}

      <TotpSetupDialog open={dialog === "totp"} role={role} onClose={() => setDialog(null)} onDone={() => { setNotice({ tone: "ok", text: "Authenticator app turned on." }); void load(); }} />
      <PasskeySetupDialog open={dialog === "passkey"} role={role} onClose={() => setDialog(null)} onDone={() => { setNotice({ tone: "ok", text: "Passkey added." }); void load(); }} />
      <CodeSetupDialog open={dialog === "email"} channel="email" role={role} onClose={() => setDialog(null)} onDone={() => { setNotice({ tone: "ok", text: "Email codes turned on." }); void load(); }} />
      <CodeSetupDialog open={dialog === "sms"} channel="sms" role={role} onClose={() => setDialog(null)} onDone={() => { setNotice({ tone: "ok", text: "SMS codes turned on." }); void load(); }} />
      <BackupCodesDialog open={dialog === "backup"} role={role} onClose={() => setDialog(null)} onDone={() => void load()} />

      {isAdmin ? <PolicyPanel role={role} onChanged={() => void load()} /> : null}
    </div>
  );
}

function PolicyPanel({ role, onChanged }: { role?: string; onChanged: () => void }) {
  const [policy, setPolicy] = useState<MfaPolicy | null>(null);
  const [metrics, setMetrics] = useState<MfaMetrics | null>(null);
  const [draft, setDraft] = useState<MfaPolicy | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ tone: "ok" | "bad"; text: string } | null>(null);
  const [userId, setUserId] = useState("");

  const load = useCallback(async () => {
    try {
      const [p, m] = await Promise.all([getMfaPolicy(role), getMfaMetrics(role)]);
      setPolicy(p);
      setDraft(p);
      setMetrics(m);
    } catch (failure) {
      setMessage({ tone: "bad", text: errorText(failure, "The organisation's policy could not be loaded.") });
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  if (!policy || !draft) return message ? <p role="alert" className="m-0 mt-6 text-[12.5px] text-dls-danger-text">{message.text}</p> : null;

  const changed = JSON.stringify({ ...draft, updatedAt: null }) !== JSON.stringify({ ...policy, updatedAt: null });

  async function save() {
    if (!draft) return;
    setBusy(true);
    setMessage(null);
    try {
      const saved = await setMfaPolicy({ required: draft.required, allowSnooze: draft.allowSnooze, minMethods: draft.minMethods, enabledMethods: draft.enabledMethods }, role);
      setPolicy(saved);
      setDraft(saved);
      setMessage({ tone: "ok", text: "The policy was saved." });
      onChanged();
    } catch (failure) {
      setMessage({ tone: "bad", text: errorText(failure, "The policy could not be saved.") });
    } finally {
      setBusy(false);
    }
  }

  async function reset() {
    setBusy(true);
    setMessage(null);
    try {
      await resetMfaUser(userId.trim(), role);
      setMessage({ tone: "ok", text: "Their methods were cleared. They set up again at their next sign-in." });
      setUserId("");
      void load();
    } catch (failure) {
      setMessage({ tone: "bad", text: errorText(failure, "That person could not be reset.") });
    } finally {
      setBusy(false);
    }
  }

  const toggleMethod = (method: MfaMethod) =>
    setDraft({ ...draft, enabledMethods: draft.enabledMethods.includes(method) ? draft.enabledMethods.filter((item) => item !== method) : [...draft.enabledMethods, method] });

  return (
    <section aria-label="Organisation policy" data-testid="mfa-policy" className="mt-8 border-t border-dls-border-subtle pt-6">
      <h3 className="m-0 text-[16px] font-bold text-dls-text-primary">For your organisation</h3>
      <p className="m-0 mt-1 max-w-2xl text-[13px] text-dls-text-secondary">Off by default. When you turn it on, everyone in this workspace must set up a method, and then verify when they sign in. Set up your own method first.</p>
      {metrics ? <p className="m-0 mt-2 text-[12.5px] text-dls-text-muted">{metrics.peopleWithAMethod} {metrics.peopleWithAMethod === 1 ? "person has" : "people have"} set up a method.</p> : null}

      <div className="mt-4 space-y-3">
        <label className="flex items-start gap-3 rounded-xl border border-dls-border-subtle bg-dls-surface px-4 py-3">
          <input type="checkbox" className="mt-1" checked={draft.required} onChange={(event) => setDraft({ ...draft, required: event.target.checked })} />
          <span>
            <span className="block text-[14px] font-semibold text-dls-text-primary">Require a second step at sign-in</span>
            <span className="block text-[12.5px] text-dls-text-muted">People without a method are asked to set one up. People with one must verify before the platform answers.</span>
          </span>
        </label>
        <label className="flex items-start gap-3 rounded-xl border border-dls-border-subtle bg-dls-surface px-4 py-3">
          <input type="checkbox" className="mt-1" checked={draft.allowSnooze} onChange={(event) => setDraft({ ...draft, allowSnooze: event.target.checked })} />
          <span>
            <span className="block text-[14px] font-semibold text-dls-text-primary">Let people postpone set-up for 24 hours</span>
            <span className="block text-[12.5px] text-dls-text-muted">Up to seven times, then set-up is compulsory.</span>
          </span>
        </label>
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-dls-border-subtle bg-dls-surface px-4 py-3">
          <span className="text-[14px] font-semibold text-dls-text-primary">Methods each person must set up</span>
          <select
            aria-label="Methods each person must set up"
            value={draft.minMethods}
            onChange={(event) => setDraft({ ...draft, minMethods: Number(event.target.value) })}
            className="rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-2 text-[13px]"
          >
            {[1, 2, 3].map((n) => <option key={n} value={n}>{n}</option>)}
          </select>
        </div>
        <fieldset className="rounded-xl border border-dls-border-subtle bg-dls-surface px-4 py-3">
          <legend className="px-1 text-[14px] font-semibold text-dls-text-primary">Methods you offer</legend>
          <div className="mt-1 flex flex-wrap gap-x-5 gap-y-2">
            {(["totp", "passkey", "email_otp", "sms_otp", "backup_codes"] as MfaMethod[]).map((method) => (
              <label key={method} className="inline-flex items-center gap-2 text-[13px] text-dls-text-secondary">
                <input type="checkbox" checked={draft.enabledMethods.includes(method)} onChange={() => toggleMethod(method)} />
                {METHOD_LABELS[method]}
              </label>
            ))}
          </div>
        </fieldset>
      </div>

      <div className="mt-4 flex flex-wrap items-center gap-3">
        <Button variant="primary" disabled={busy || !changed} onClick={() => void save()}>
          {busy ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : null}
          Save policy
        </Button>
        {message ? <span role="status" className={cn("text-[12.5px]", message.tone === "ok" ? "text-dls-success-text" : "text-dls-danger-text")}>{message.text}</span> : null}
      </div>

      <div className="mt-6 rounded-xl border border-dls-border-subtle bg-dls-surface px-4 py-3">
        <h4 className="m-0 text-[14px] font-bold text-dls-text-primary">Someone lost their phone?</h4>
        <p className="m-0 mt-1 text-[12.5px] text-dls-text-muted">Clearing a person&apos;s methods makes them set up again. You cannot reset yourself; ask another administrator.</p>
        <div className="mt-3 flex flex-wrap items-start gap-2">
          <div className="w-72">
            <Input value={userId} onChange={(event) => setUserId(event.target.value)} placeholder="Their user ID" aria-label="User ID to reset" />
          </div>
          <Button variant="secondary" disabled={busy || userId.trim().length < 3} onClick={() => void reset()}>Clear their methods</Button>
        </div>
      </div>
    </section>
  );
}

