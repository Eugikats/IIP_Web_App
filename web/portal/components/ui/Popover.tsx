// Design language v2 §8.15. Anchored, transparent full-screen backdrop closes on click/Escape.
"use client";

import { useEffect, useRef, type ReactNode } from "react";
import { cn } from "@/lib/utils";

export interface PopoverProps {
  open: boolean;
  onClose: () => void;
  anchorRef: React.RefObject<HTMLElement | null>;
  children: ReactNode;
  label: string;
}

export function Popover({ open, onClose, anchorRef, children, label }: PopoverProps) {
  const panelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const trigger = anchorRef.current;
    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        event.preventDefault();
        onClose();
      }
    }
    document.addEventListener("keydown", onKeyDown);
    panelRef.current?.querySelector<HTMLElement>("input, button, [tabindex]")?.focus();
    return () => {
      document.removeEventListener("keydown", onKeyDown);
      trigger?.focus();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  if (!open) return null;

  return (
    <>
      <button
        type="button"
        aria-label="Close"
        tabIndex={-1}
        onClick={onClose}
        className="fixed inset-0 z-40 bg-transparent"
      />
      <div
        ref={panelRef}
        role="dialog"
        aria-label={label}
        className={cn(
          "fixed left-1/2 top-24 z-[41] w-[320px] -translate-x-1/2 rounded-dls-overlay border border-dls-border-subtle bg-dls-surface p-2 shadow-dls-popover",
          "sm:absolute sm:left-16 sm:top-4 sm:translate-x-0",
        )}
      >
        {children}
      </div>
    </>
  );
}

export function PopoverGroupLabel({ children }: { children: ReactNode }) {
  return <p className="px-4 pb-1 pt-2 text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">{children}</p>;
}
