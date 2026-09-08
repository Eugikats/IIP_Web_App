"use client";

import { ReactNode, useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { X } from "lucide-react";
import { Button } from "@/components/ui/Button";

// Design language v2 §8.14 — modal. Shared by 8 screens across tiers 1/2/3/5 (admin
// EmailInbox/Users/WhatsAppSettings, ClaimReview, Communications, FraudIntelligence, Knowledge,
// plus CustomerUpdateGovernedDialog), so this is migrated once here rather than per screen.

type GovernedActionDialogProps = {
  open: boolean;
  title: string;
  target: string;
  impact: string;
  auditNotice: string;
  confirmLabel: string;
  tone?: "primary" | "danger";
  evidence?: ReactNode;
  onClose: () => void;
  onConfirm: (reason: string) => Promise<void> | void;
};

export function GovernedActionDialog({
  open,
  title,
  target,
  impact,
  auditNotice,
  confirmLabel,
  tone = "primary",
  evidence,
  onClose,
  onConfirm
}: GovernedActionDialogProps) {
  const [mounted, setMounted] = useState(false);
  const [reason, setReason] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const dialogRef = useRef<HTMLElement>(null);

  useEffect(() => setMounted(true), []);

  useEffect(() => {
    if (open) {
      setReason("");
      setIsSubmitting(false);
    }
  }, [open]);

  // Captured only on the false->true transition, in its own effect keyed solely on `open` — the
  // trap effect below re-runs whenever `onClose`/`isSubmitting` change (a new `onClose` closure
  // on every parent render is normal), and re-deriving "previousFocus" from document.activeElement
  // on one of those re-runs would capture an element already inside the dialog, not the real
  // trigger, silently breaking focus-return to whatever the dialog itself last focused.
  const previousFocusRef = useRef<HTMLElement | null>(null);
  useEffect(() => {
    if (open) {
      previousFocusRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    }
  }, [open]);

  useEffect(() => {
    if (!open) return;
    const dialog = dialogRef.current;
    const focusable = () => Array.from(dialog?.querySelectorAll<HTMLElement>('button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), a[href], [tabindex]:not([tabindex="-1"])') ?? []);
    window.requestAnimationFrame(() => focusable()[0]?.focus());

    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape" && !isSubmitting) onClose();
      if (event.key !== "Tab") return;
      const items = focusable();
      if (items.length === 0) { event.preventDefault(); return; }
      const first = items[0];
      const last = items[items.length - 1];
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
      if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
    };
    document.addEventListener("keydown", onKey);
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", onKey);
      document.body.style.overflow = previousOverflow;
      window.requestAnimationFrame(() => previousFocusRef.current?.focus());
    };
  }, [isSubmitting, onClose, open]);

  if (!mounted || !open) return null;

  const trimmedReason = reason.trim();
  const canConfirm = trimmedReason.length >= 8 && !isSubmitting;

  return createPortal(
    <div>
      <div
        className="fixed inset-0 z-[120] bg-dls-scrim/45"
        onClick={isSubmitting ? undefined : onClose}
      />
      <section
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="governed-action-title"
        className="fixed left-1/2 top-1/2 z-[121] flex max-h-[calc(100dvh-24px)] w-[min(460px,92vw)] -translate-x-1/2 -translate-y-1/2 flex-col overflow-hidden rounded-dls-overlay border border-dls-border-subtle bg-dls-surface shadow-dls-modal"
      >
        <div className="flex items-start justify-between gap-4 border-b border-dls-border-subtle px-[22px] py-5">
          <h3 id="governed-action-title" className="min-w-0 break-words text-[17px] font-extrabold text-dls-text-primary">{title}</h3>
          <button
            type="button"
            className="dls-focus-outline flex h-9 w-9 shrink-0 items-center justify-center rounded-dls-control text-dls-text-muted hover:bg-dls-bg-page hover:text-dls-text-primary disabled:cursor-not-allowed disabled:opacity-50"
            onClick={onClose}
            disabled={isSubmitting}
            aria-label="Close confirmation"
          >
            <X className="h-5 w-5" aria-hidden="true" />
          </button>
        </div>

        <div className="min-h-0 flex-1 space-y-4 overflow-y-auto px-[22px] py-5">
          <InfoBlock label="Affected object" value={target} />
          <InfoBlock label="Impact" value={impact} />
          {evidence ? <div>{evidence}</div> : null}
          <div className="rounded-dls-card border border-dls-warning-dot bg-dls-warning-tint p-4 text-sm leading-6 text-dls-warning-text">
            {auditNotice}
          </div>
          <label className="block text-sm font-semibold text-dls-text-primary">
            Reason for audit trail
            <textarea
              aria-label="Reason for audit trail"
              className="mt-2 min-h-28 w-full resize-y rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-2 text-sm text-dls-text-primary outline-none placeholder:text-dls-text-muted focus:border-dls-primary focus:ring-[3px] focus:ring-dls-primary/[0.12]"
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              placeholder="Enter the business reason for this action…"
            />
          </label>
          <p className="text-xs leading-5 text-dls-text-muted">
            A reason of at least 8 characters is required before this action can be submitted.
          </p>
        </div>

        <div className="flex flex-wrap justify-end gap-3 border-t border-dls-border-subtle px-[22px] py-4">
          <Button variant="ghost" onClick={onClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button
            variant={tone === "danger" ? "destructive" : "primary"}
            disabled={!canConfirm}
            onClick={async () => {
              setIsSubmitting(true);
              try {
                await onConfirm(trimmedReason);
              } finally {
                setIsSubmitting(false);
              }
            }}
          >
            {isSubmitting ? "Submitting…" : confirmLabel}
          </Button>
        </div>
      </section>
    </div>,
    document.body
  );
}

function InfoBlock({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4">
      <p className="text-[10px] font-bold uppercase tracking-[0.18em] text-dls-text-muted">{label}</p>
      <p className="mt-2 break-words text-sm font-bold text-dls-text-primary">{value}</p>
    </div>
  );
}
