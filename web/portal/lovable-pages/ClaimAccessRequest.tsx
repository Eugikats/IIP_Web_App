"use client";

import { useState } from "react";
import type { FormEvent } from "react";
import { ArrowRight, Check, Loader2, Mail, ShieldCheck } from "lucide-react";
import iipLogo from "@/assets/iip-logo";
import { requestMagicLink } from "@/lib/clientPortal";

export default function ClaimAccessRequest() {
  const [policyNumber, setPolicyNumber] = useState("");
  const [contactValue, setContactValue] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sent, setSent] = useState<string | null>(null);

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
  }

  return (
    <main className="grid min-h-screen bg-white text-ink lg:grid-cols-[minmax(0,1.08fr)_minmax(420px,0.92fr)]">
      <section className="relative hidden min-h-screen flex-col justify-between overflow-hidden bg-ink px-12 py-10 text-white lg:flex xl:px-16 xl:py-12">
        <div className="absolute inset-y-0 right-0 w-px bg-white/15" />
        <div className="relative flex items-center gap-3">
          <span className="grid h-10 w-10 place-items-center rounded-md bg-white">
            <img src={iipLogo.url} alt="IIP" className="h-8 w-8 object-contain" />
          </span>
          <div>
            <div className="text-sm font-semibold">Claim access</div>
            <div className="mt-0.5 text-xs text-white/65">Insurance Intelligence Platform</div>
          </div>
        </div>

        <div className="relative max-w-2xl">
          <p className="mb-5 text-sm font-medium text-white/65">Track your claim, securely</p>
          <h1 className="max-w-xl text-4xl font-semibold leading-tight text-white xl:text-5xl">
            Check what&apos;s outstanding on your claim, any time.
          </h1>
          <p className="mt-6 max-w-lg text-base leading-7 text-white/70">
            We&apos;ll send a one-time secure link to the email or phone number on your claim — no password to
            remember, and no account to create.
          </p>
          <div className="mt-10 grid max-w-xl gap-4 border-t border-white/15 pt-7 sm:grid-cols-1">
            {[
              "The link expires in 15 minutes and works once",
              "We never ask for your policy details by phone or email first",
              "Your claim documents and updates stay private to you",
            ].map((item) => (
              <div key={item} className="flex items-start gap-2 text-sm leading-5 text-white/75">
                <Check className="mt-0.5 h-4 w-4 shrink-0 text-[#7dd3fc]" />
                <span>{item}</span>
              </div>
            ))}
          </div>
        </div>

        <div className="relative text-xs text-white/55">Access is limited to the claimant on file for each claim.</div>
      </section>

      <section className="flex min-h-screen items-center justify-center px-6 py-10 sm:px-10 lg:px-14">
        <div className="w-full max-w-md">
          <div className="mb-12 flex items-center gap-3 lg:hidden">
            <span className="grid h-10 w-10 place-items-center rounded-md border border-line bg-white">
              <img src={iipLogo.url} alt="IIP" className="h-8 w-8 object-contain" />
            </span>
            <div>
              <div className="text-sm font-semibold">Claim access</div>
              <div className="text-xs text-muted">Insurance Intelligence Platform</div>
            </div>
          </div>

          {sent ? (
            <div>
              <div className="mb-8 flex items-start gap-3 border-y border-line py-4">
                <ShieldCheck className="mt-0.5 h-5 w-5 shrink-0 text-success" />
                <div>
                  <div className="text-sm font-medium text-ink">Check your inbox or messages</div>
                  <div className="mt-1 text-xs leading-5 text-muted">{sent}</div>
                </div>
              </div>
              <p className="text-sm leading-6 text-muted">
                Didn&apos;t get anything after a few minutes? Check the spam folder, confirm the contact details
                match what is on your claim, or try again below.
              </p>
              <button
                type="button"
                onClick={() => setSent(null)}
                className="mt-6 inline-flex min-h-11 items-center gap-2 rounded-md border border-line bg-white px-4 py-2.5 text-sm font-medium text-ink focus-ring"
              >
                Try a different claim
              </button>
            </div>
          ) : (
            <form onSubmit={onSubmit}>
              <div className="mb-8 flex items-center justify-between gap-4 border-b border-line pb-4">
                <span className="text-sm font-medium text-ink-soft">Claim access</span>
              </div>
              <h2 className="text-3xl font-semibold leading-tight text-ink">Access your claim</h2>
              <p className="mt-3 text-sm leading-6 text-muted">
                Enter your policy number and the email or phone number on your claim. We&apos;ll send you a secure
                link — no password needed.
              </p>

              <label className="mt-8 block text-sm font-medium text-ink">
                Policy number
                <input
                  value={policyNumber}
                  onChange={(event) => setPolicyNumber(event.target.value)}
                  placeholder="e.g. NSA/MO/2026/00033"
                  autoComplete="off"
                  className="mt-2 min-h-11 w-full rounded-md border border-line bg-white px-3 py-2.5 text-sm text-ink placeholder:text-muted focus-ring"
                />
              </label>

              <label className="mt-5 block text-sm font-medium text-ink">
                Email or phone number
                <input
                  value={contactValue}
                  onChange={(event) => setContactValue(event.target.value)}
                  placeholder="you@example.com or +2567..."
                  autoComplete="off"
                  className="mt-2 min-h-11 w-full rounded-md border border-line bg-white px-3 py-2.5 text-sm text-ink placeholder:text-muted focus-ring"
                />
              </label>

              <button
                type="submit"
                disabled={submitting}
                className="mt-8 inline-flex min-h-12 w-full items-center justify-center gap-3 rounded-md bg-primary px-5 py-3 text-sm font-semibold text-white shadow-l2 transition-colors hover:bg-primary/90 focus-ring disabled:cursor-not-allowed disabled:opacity-55"
              >
                {submitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Mail className="h-4 w-4" />}
                <span>{submitting ? "Sending your link" : "Send my access link"}</span>
                {!submitting && <ArrowRight className="h-4 w-4" />}
              </button>

              {error && (
                <div role="alert" className="mt-5 flex items-start gap-2 rounded-md border border-danger/25 bg-danger/7 px-3 py-3 text-xs leading-5 text-danger">
                  <span>{error}</span>
                </div>
              )}
              <p className="mt-8 text-xs leading-5 text-muted">
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
