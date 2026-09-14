"use client";

import { useState } from "react";
import type { FormEvent } from "react";
import { ArrowRight, Check, Loader2, Mail, ShieldCheck } from "lucide-react";
import iipLogo from "@/assets/iip-logo";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { requestMagicLink } from "@/lib/clientPortal";

// Claimant-facing (design language v2, tier 6 — stricter rules: AGENTS.md forbids exposing
// internal notes, reserves, fraud/risk signals, prompts, secrets, raw provider errors, restricted
// audit detail, or other-tenant data here). This screen only ever asks for and echoes back the
// policy number and contact value the claimant themselves typed — nothing staff-only is rendered.
export default function ClaimAccessRequest() {
  const [policyNumber, setPolicyNumber] = useState("");
  const [contactValue, setContactValue] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sent, setSent] = useState<string | null>(null);
  const [sandboxLink, setSandboxLink] = useState<string | null>(null);

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    if (!policyNumber.trim() || !contactValue.trim()) {
      setError("Enter both your policy number and the email or phone number on your claim.");
      return;
    }

    setSubmitting(true);
    setError(null);
    const result = await requestMagicLink(policyNumber.trim(), contactValue.trim());
    setSubmitting(false);
    if (!result.ok) {
      setError(result.error);
      return;
    }
    setSent(result.message);
    setSandboxLink(result.sandboxLink);
  }

  return (
    <main className="grid min-h-screen bg-dls-surface text-dls-text-primary lg:grid-cols-[minmax(0,1.08fr)_minmax(420px,0.92fr)]">
      {/* Brand panel — design language §4.5 notes brand expression is strongest at sign-in.
          Background reuses --color-text-primary's exact value (a deliberate one-off dark brand
          moment, not dark mode — the portal otherwise ships light-only, §9); the checkmark accent
          reuses --color-primary-soft, which reads clearly against near-black and stays on-brand
          rather than introducing a new literal colour. */}
      <section className="relative hidden min-h-screen flex-col justify-between overflow-hidden bg-dls-text-primary px-12 py-10 text-dls-text-inverse lg:flex xl:px-16 xl:py-12">
        <div className="absolute inset-y-0 right-0 w-px bg-dls-text-inverse/15" />
        <div className="relative flex items-center gap-3">
          <span className="grid h-10 w-10 place-items-center rounded-dls-control bg-dls-surface">
            <img src={iipLogo.url} alt="IIP" className="h-8 w-8 object-contain" />
          </span>
          <div>
            <div className="text-sm font-bold">Claim access</div>
            <div className="mt-0.5 text-xs text-dls-text-inverse/65">Insurance Intelligence Platform</div>
          </div>
        </div>

        <div className="relative max-w-2xl">
          <p className="mb-5 text-sm font-semibold text-dls-text-inverse/65">Track your claim, securely</p>
          <h1 className="max-w-xl text-4xl font-bold leading-tight text-dls-text-inverse xl:text-5xl">
            Check what&apos;s outstanding on your claim, any time.
          </h1>
          <p className="mt-6 max-w-lg text-base leading-7 text-dls-text-inverse/70">
            We&apos;ll send a one-time secure link to the email or phone number on your claim — no password to
            remember, and no account to create.
          </p>
          <div className="mt-10 grid max-w-xl gap-4 border-t border-dls-text-inverse/15 pt-7 sm:grid-cols-1">
            {[
              "The link expires in 15 minutes and works once",
              "We never ask for your policy details by phone or email first",
              "Your claim documents and updates stay private to you",
            ].map((item) => (
              <div key={item} className="flex items-start gap-2 text-sm leading-5 text-dls-text-inverse/75">
                <Check className="mt-0.5 h-4 w-4 shrink-0 text-dls-primary-soft" aria-hidden="true" />
                <span>{item}</span>
              </div>
            ))}
          </div>
        </div>

        <div className="relative text-xs text-dls-text-inverse/55">Access is limited to the claimant on file for each claim.</div>
      </section>

      <section className="flex min-h-screen items-center justify-center px-6 py-10 sm:px-10 lg:px-14">
        <div className="w-full max-w-md">
          <div className="mb-12 flex items-center gap-3 lg:hidden">
            <span className="grid h-10 w-10 place-items-center rounded-dls-control border border-dls-border-subtle bg-dls-surface">
              <img src={iipLogo.url} alt="IIP" className="h-8 w-8 object-contain" />
            </span>
            <div>
              <div className="text-sm font-bold">Claim access</div>
              <div className="text-xs text-dls-text-muted">Insurance Intelligence Platform</div>
            </div>
          </div>

          {sent ? (
            <div>
              <div className="mb-8 flex items-start gap-3 border-y border-dls-border-subtle py-4">
                <ShieldCheck className="mt-0.5 h-5 w-5 shrink-0 text-dls-success-text" aria-hidden="true" />
                <div>
                  <div className="text-sm font-semibold text-dls-text-primary">Check your inbox or messages</div>
                  <div className="mt-1 text-xs leading-5 text-dls-text-muted">{sent}</div>
                </div>
              </div>
              {sandboxLink ? (
                <div className="mb-6 rounded-dls-card border border-dls-border-subtle bg-dls-bg-page p-4">
                  <div className="text-sm font-semibold text-dls-text-primary">Demo sandbox: your link is shown here</div>
                  <p className="mt-1 text-xs leading-5 text-dls-text-muted">
                    This environment is not connected to SMS or email, so the secure link that would normally be sent to you
                    is shown here instead. A live environment never shows it on screen.
                  </p>
                  <a
                    href={sandboxLink}
                    className="dls-focus-outline mt-3 inline-flex items-center gap-1.5 rounded-dls-control bg-dls-primary px-4 py-2 text-sm font-bold text-dls-text-inverse hover:bg-dls-primary-hover"
                  >
                    Open my secure link <ArrowRight className="h-4 w-4" aria-hidden="true" />
                  </a>
                </div>
              ) : null}
              <p className="text-sm leading-6 text-dls-text-secondary">
                Didn&apos;t get anything after a few minutes? Check the spam folder, confirm the contact details
                match what is on your claim, or try again below.
              </p>
              <div className="mt-6">
                <Button variant="secondary" onClick={() => { setSent(null); setSandboxLink(null); }}>
                  Try a different claim
                </Button>
              </div>
            </div>
          ) : (
            <form onSubmit={onSubmit}>
              <div className="mb-8 flex items-center justify-between gap-4 border-b border-dls-border-subtle pb-4">
                <span className="text-sm font-semibold text-dls-text-secondary">Claim access</span>
              </div>
              <h2 className="text-3xl font-bold leading-tight text-dls-text-primary">Access your claim</h2>
              <p className="mt-3 text-sm leading-6 text-dls-text-secondary">
                Enter your policy number and the email or phone number on your claim. We&apos;ll send you a secure
                link — no password needed.
              </p>

              <label className="mt-8 block text-sm font-semibold text-dls-text-primary" htmlFor="claim-access-policy">
                Policy number
                <div className="mt-2">
                  <Input
                    id="claim-access-policy"
                    value={policyNumber}
                    onChange={(event) => setPolicyNumber(event.target.value)}
                    placeholder="e.g. NSA/MO/2026/00033"
                    autoComplete="off"
                  />
                </div>
              </label>

              <label className="mt-5 block text-sm font-semibold text-dls-text-primary" htmlFor="claim-access-contact">
                Email or phone number
                <div className="mt-2">
                  <Input
                    id="claim-access-contact"
                    value={contactValue}
                    onChange={(event) => setContactValue(event.target.value)}
                    placeholder="you@example.com or +2567..."
                    autoComplete="off"
                  />
                </div>
              </label>

              {/* Full-width, taller, trailing-icon submit — hand-styled to Button's primary
                  variant. Same reason as FNOL.tsx's "Next": Button's icon prop only renders
                  leading, and this needs both a leading and a trailing icon plus a non-default
                  size, so extending the shared primitive's API isn't warranted for one screen. */}
              <button
                type="submit"
                disabled={submitting}
                className="dls-focus-outline mt-8 inline-flex min-h-12 w-full items-center justify-center gap-3 rounded-dls-control bg-dls-primary px-5 py-3 text-sm font-bold text-dls-text-inverse shadow-dls-primary transition-colors duration-dls-fast ease-dls hover:bg-dls-primary-hover disabled:cursor-not-allowed disabled:opacity-60"
              >
                {submitting ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <Mail className="h-4 w-4" aria-hidden="true" />}
                <span>{submitting ? "Sending your link" : "Send my access link"}</span>
                {!submitting && <ArrowRight className="h-4 w-4" aria-hidden="true" />}
              </button>

              {error && (
                <div role="alert" className="mt-5 flex items-start gap-2 rounded-dls-control bg-dls-danger-tint px-3 py-3 text-xs leading-5 text-dls-danger-text">
                  <span>{error}</span>
                </div>
              )}
              <p className="mt-8 text-xs leading-5 text-dls-text-muted">
                We only send an access link when your details match a claim on file. If nothing arrives, double
                check what you entered.
              </p>
            </form>
          )}
        </div>
      </section>
    </main>
  );
}
