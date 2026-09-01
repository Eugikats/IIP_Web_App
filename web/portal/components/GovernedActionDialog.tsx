"use client";

import { ReactNode, useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { AlertTriangle, X } from "lucide-react";

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

  useEffect(() => {
    if (!open) return;
    const previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
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
      window.requestAnimationFrame(() => previousFocus?.focus());
    };
  }, [isSubmitting, onClose, open]);

  if (!mounted || !open) return null;

  const trimmedReason = reason.trim();
  const canConfirm = trimmedReason.length >= 8 && !isSubmitting;
  const confirmClass = tone === "danger"
    ? "border-danger/40 bg-danger text-white hover:brightness-110"
    : "border-azure/40 bg-azure text-white hover:brightness-110";

  return createPortal(
    <div>
      <div
        className="fixed inset-0 z-[120] bg-ink/45 opacity-100 backdrop-blur-sm"
        onClick={isSubmitting ? undefined : onClose}
      />
      <section
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="governed-action-title"
        className="fixed left-1/2 top-1/2 z-[121] flex max-h-[calc(100dvh-24px)] w-[min(560px,92vw)] -translate-x-1/2 -translate-y-1/2 flex-col overflow-hidden rounded-2xl border border-line bg-surface opacity-100 shadow-float"
      >
        <div className="flex items-start justify-between gap-4 border-b border-line px-6 py-5">
          <div className="min-w-0">
            <div className="flex items-center gap-2 text-[10px] font-bold uppercase tracking-[0.18em] text-warning">
              <AlertTriangle className="h-4 w-4" />
              Governed action
            </div>
            <h3 id="governed-action-title" className="mt-2 break-words text-xl font-semibold text-ink">{title}</h3>
          </div>
          <button
            className="flex h-11 w-11 flex-shrink-0 items-center justify-center rounded-xl text-muted transition-colors hover:bg-mist hover:text-ink disabled:cursor-not-allowed disabled:opacity-50"
            onClick={onClose}
            disabled={isSubmitting}
            aria-label="Close confirmation"
            title="Close"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="min-h-0 flex-1 space-y-4 overflow-y-auto px-6 py-5">
          <InfoBlock label="Affected object" value={target} />
          <InfoBlock label="Impact" value={impact} />
          {evidence ? <div>{evidence}</div> : null}
          <div className="rounded-xl border border-warning/30 bg-warning/10 p-4 text-sm leading-6 text-warning">
            {auditNotice}
          </div>
          <label className="block text-sm font-medium text-muted">
            Reason for audit trail
            <textarea
              aria-label="Reason for audit trail"
              className="mt-2 min-h-28 w-full resize-y rounded-xl border border-line bg-shell px-3 py-2 text-sm text-ink outline-none transition-all placeholder:text-muted-soft focus:border-accent focus:ring-1 focus:ring-accent"
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              placeholder="Enter the business reason for this action..."
            />
          </label>
          <p className="text-xs leading-5 text-muted">
            A reason of at least 8 characters is required before this action can be submitted.
          </p>
        </div>

        <div className="flex flex-wrap justify-end gap-3 border-t border-line bg-surface px-6 py-4">
          <button
            className="min-h-11 rounded-full border border-line bg-surface px-4 py-2.5 text-sm font-semibold text-muted transition-colors hover:border-accent/50 hover:text-ink disabled:cursor-not-allowed disabled:opacity-50"
            onClick={onClose}
            disabled={isSubmitting}
          >
            Cancel
          </button>
          <button
            className={`min-h-11 rounded-full border px-4 py-2.5 text-sm font-semibold shadow-sm transition-colors disabled:cursor-not-allowed disabled:opacity-50 ${confirmClass}`}
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
            {isSubmitting ? "Submitting..." : confirmLabel}
          </button>
        </div>
      </section>
    </div>,
    document.body
  );
}

function InfoBlock({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border border-line bg-mist p-4">
      <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted">{label}</p>
      <p className="mt-2 break-words text-sm font-semibold text-ink">{value}</p>
    </div>
  );
}
