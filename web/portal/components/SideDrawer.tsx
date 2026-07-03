"use client";

import { ReactNode, useEffect, useState } from "react";
import { createPortal } from "react-dom";
import { X } from "lucide-react";

export function SideDrawer({
  open,
  title,
  subtitle,
  children,
  footer,
  onClose
}: {
  open: boolean;
  title: string;
  subtitle?: string;
  children: ReactNode;
  footer?: ReactNode;
  onClose: () => void;
}) {
  const [mounted, setMounted] = useState(false);

  useEffect(() => setMounted(true), []);

  // Close on Escape and lock body scroll while the drawer is open.
  useEffect(() => {
    if (!open) return;
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    document.addEventListener("keydown", onKey);
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", onKey);
      document.body.style.overflow = previousOverflow;
    };
  }, [open, onClose]);

  // Render through a portal on <body> so the fixed positioning resolves against
  // the viewport, not an ancestor that has a CSS transform (e.g. framer-motion
  // wrappers), which previously trapped the drawer inside the content column.
  if (!mounted) return null;

  return createPortal(
    <div aria-hidden={!open}>
      <div
        className={`fixed inset-0 z-[100] bg-ink/40 backdrop-blur-sm transition-opacity duration-200 ${
          open ? "opacity-100" : "pointer-events-none opacity-0"
        }`}
        onClick={onClose}
      />
      <aside
        role="dialog"
        aria-modal="true"
        aria-hidden={!open}
        className={`fixed right-0 top-0 z-[101] flex h-full w-full max-w-[min(540px,92vw)] flex-col rounded-l-2xl border-l border-line bg-surface shadow-drawer transition-transform duration-200 ease-out ${
          open ? "translate-x-0" : "translate-x-full"
        }`}
      >
        <div className="flex items-start justify-between gap-4 border-b border-line px-6 py-5">
          <div className="min-w-0">
            <p className="text-[10px] font-bold uppercase tracking-[0.18em] text-muted-soft">IIP Operations</p>
            <h3 className="mt-2 break-words text-xl font-semibold text-ink">{title}</h3>
            {subtitle ? <p className="mt-1.5 break-words text-sm leading-6 text-muted">{subtitle}</p> : null}
          </div>
          <button
            className="flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-xl text-muted transition-colors hover:bg-surface-soft hover:text-ink"
            onClick={onClose}
            aria-label="Close drawer"
            title="Close"
          >
            <X className="h-5 w-5" />
          </button>
        </div>
        <div className="min-h-0 flex-1 overflow-y-auto px-6 py-5 custom-scrollbar">{children}</div>
        {footer ? <div className="border-t border-line px-6 py-4">{footer}</div> : null}
      </aside>
    </div>,
    document.body
  );
}
