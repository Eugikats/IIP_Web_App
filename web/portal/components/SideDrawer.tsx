"use client";

import { ReactNode, useEffect, useRef, useState } from "react";
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
  const drawerRef = useRef<HTMLElement>(null);

  useEffect(() => setMounted(true), []);

  // Close on Escape and lock body scroll while the drawer is open.
  useEffect(() => {
    if (!open) return;
    const previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const drawer = drawerRef.current;
    const focusable = () => Array.from(drawer?.querySelectorAll<HTMLElement>('button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), a[href], [tabindex]:not([tabindex="-1"])') ?? []);
    window.requestAnimationFrame(() => focusable()[0]?.focus());
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
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
  }, [open, onClose]);

  // Render through a portal on <body> so the fixed positioning resolves against
  // the viewport, not an ancestor that has a CSS transform (e.g. framer-motion
  // wrappers), which previously trapped the drawer inside the content column.
  if (!mounted || !open) return null;

  return createPortal(
    <div>
      <div
        className="fixed inset-0 z-[100] bg-ink/40 opacity-100 backdrop-blur-sm"
        onClick={onClose}
      />
      <aside
        ref={drawerRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="side-drawer-title"
        className="fixed right-0 top-0 z-[101] flex h-full w-full max-w-[min(540px,92vw)] translate-x-0 flex-col rounded-l-2xl border-l border-line bg-surface shadow-drawer"
      >
        <div className="flex items-start justify-between gap-4 border-b border-line px-6 py-5">
          <div className="min-w-0">
            <p className="text-[10px] font-bold uppercase tracking-[0.18em] text-muted-soft">IIP Operations</p>
            <h3 id="side-drawer-title" className="mt-2 break-words text-xl font-semibold text-ink">{title}</h3>
            {subtitle ? <p className="mt-1.5 break-words text-sm leading-6 text-muted">{subtitle}</p> : null}
          </div>
          <button
            className="flex h-11 w-11 flex-shrink-0 items-center justify-center rounded-xl text-muted transition-colors hover:bg-surface-soft hover:text-ink"
            onClick={onClose}
            aria-label="Close drawer"
            title="Close"
          >
            <X className="h-5 w-5" />
          </button>
        </div>
        <div tabIndex={0} aria-label={`${title} details`} className="min-h-0 flex-1 overflow-y-auto px-6 py-5 custom-scrollbar focus-ring">{children}</div>
        {footer ? <div className="border-t border-line px-6 py-4">{footer}</div> : null}
      </aside>
    </div>,
    document.body
  );
}
