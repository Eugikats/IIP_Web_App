"use client";

import { useState } from "react";
import { Loader2, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/Button";
import type { PrivacyConsent } from "@/lib/privacyConsent";

type ClaimantConsentCardProps = {
  /** Null while the notice is loading. */
  consent: PrivacyConsent | null;
  busy: boolean;
  error: string | null;
  onAgree: () => void;
  onWithdraw: () => void;
  /** Used by "Preview as customer": the card looks the same but nothing is recorded. */
  previewOnly?: boolean;
};

function formatDate(value: string | null) {
  if (!value) return null;
  try {
    return new Date(value).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
  } catch {
    return value;
  }
}

/**
 * The privacy notice a claimant reads, and agrees to, before sending documents (build-loop item 9).
 * Once agreed it shrinks to one line with the notice one tap away and a way to withdraw. Shared by
 * the claim page and "Preview as customer".
 */
export function ClaimantConsentCard({ consent, busy, error, onAgree, onWithdraw, previewOnly = false }: ClaimantConsentCardProps) {
  const [showNotice, setShowNotice] = useState(false);
  const [confirmWithdraw, setConfirmWithdraw] = useState(false);

  if (!consent) {
    return (
      <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-6 text-xs text-dls-text-muted" data-testid="claimant-consent">
        {error ?? "Loading the privacy notice..."}
      </div>
    );
  }

  const paragraphs = consent.noticeText.split(/\n{2,}/).map((part) => part.trim()).filter(Boolean);
  const notice = (
    <div className="mt-3 space-y-3 text-sm leading-6 text-dls-text-secondary" data-testid="privacy-notice-text">
      {paragraphs.map((paragraph) => {
        const split = paragraph.indexOf(":");
        const heading = split > 0 && split <= 30 ? paragraph.slice(0, split) : null;
        return (
          <p key={paragraph}>
            {heading ? <><span className="font-semibold text-dls-text-primary">{heading}.</span> {paragraph.slice(split + 1).trim()}</> : paragraph}
          </p>
        );
      })}
    </div>
  );

  if (consent.state === "Given") {
    return (
      <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-6" data-testid="claimant-consent" data-state="Given">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="flex items-center gap-2 text-sm text-dls-text-secondary">
            <ShieldCheck className="h-4 w-4 shrink-0 text-dls-success-text" aria-hidden="true" />
            You agreed to our privacy notice{consent.recordedAt ? ` on ${formatDate(consent.recordedAt)}` : ""}.
          </div>
          <div className="flex gap-2">
            <Button variant="ghost" onClick={() => setShowNotice((value) => !value)} aria-expanded={showNotice}>{showNotice ? "Hide the notice" : "Read the notice"}</Button>
            {!confirmWithdraw && <Button variant="ghost" onClick={() => setConfirmWithdraw(true)} disabled={previewOnly}>Withdraw permission</Button>}
          </div>
        </div>
        {showNotice && notice}
        {confirmWithdraw && (
          <div className="mt-3 rounded-dls-control border border-dls-border-subtle bg-dls-bg-page p-3 text-xs leading-5 text-dls-text-secondary" role="group" aria-label="Confirm withdrawing permission">
            If you withdraw, you can no longer send documents until you agree again. What you already sent stays with your claim.
            <div className="mt-2 flex gap-2">
              <Button variant="secondary" onClick={() => { setConfirmWithdraw(false); onWithdraw(); }} disabled={busy}>Withdraw my permission</Button>
              <Button variant="ghost" onClick={() => setConfirmWithdraw(false)}>Keep it</Button>
            </div>
          </div>
        )}
        {error && <p role="alert" className="mt-3 text-xs text-dls-danger-text">{error}</p>}
      </div>
    );
  }

  const intro =
    consent.state === "NoticeChanged"
      ? "Our privacy notice has changed since you last agreed. Please read it again."
      : consent.state === "Withdrawn"
        ? "You withdrew your permission. You can agree again to send documents."
        : "Please read this before you send any documents.";

  return (
    <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-6" data-testid="claimant-consent" data-state={consent.state}>
      <div className="text-xs font-bold uppercase tracking-[0.12em] text-dls-text-muted">How we use your information</div>
      <p className="mt-1 text-xs leading-5 text-dls-text-muted">{intro}</p>
      {notice}
      <Button variant="primary" className="mt-4 w-full" onClick={onAgree} disabled={busy || previewOnly} icon={busy ? <Loader2 className="animate-spin" aria-hidden="true" /> : undefined}>
        I agree
      </Button>
      {previewOnly && <p className="mt-2 text-xs text-dls-text-muted">Preview only. Nothing is recorded.</p>}
      {error && <p role="alert" className="mt-3 text-xs text-dls-danger-text">{error}</p>}
    </div>
  );
}
