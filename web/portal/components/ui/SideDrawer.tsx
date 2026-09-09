"use client";

import { ReactNode, useCallback, useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { X } from "lucide-react";

/**
 * Side drawer — design language v2 §8.16.
 *
 * Right-anchored, 440px (92vw max), full height, surface background, modal
 * shadow, hairline left border. Used for evidence, document preview and audit
 * detail: anything the user must consult *without leaving the decision they are
 * making*. The underlying page stays visible and inert (§8.16), which is the
 * whole point — a full navigation would lose the decision context.
 *
 * Focus rules match the modal (§8.14): focus moves in on open, is trapped while
 * open, Escape closes, and focus returns to the trigger on close.
 */
const FOCUSABLE =
  'button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), a[href], [tabindex]:not([tabindex="-1"])';

export function SideDrawer({
  open,
  title,
  description,
  children,
  footer,
  onClose,
}: {
  open: boolean;
  title: string;
  description?: string;
  children: ReactNode;
  footer?: ReactNode;
  onClose: () => void;
}) {
  const [host, setHost] = useState<HTMLDivElement | null>(null);
  const panelRef = useRef<HTMLElement>(null);

  // Own portal host so the background can be made inert without also
  // disabling the drawer itself.
  useEffect(() => {
    const node = document.createElement("div");
    node.setAttribute("data-dls-drawer-host", "");
    document.body.appendChild(node);
    setHost(node);
    return () => {
      node.remove();
    };
  }, []);

  const close = useCallback(() => onClose(), [onClose]);

  useEffect(() => {
    if (!open || !host) return;

    const previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const panel = panelRef.current;
    const focusable = () => Array.from(panel?.querySelectorAll<HTMLElement>(FOCUSABLE) ?? []);

    window.requestAnimationFrame(() => (focusable()[0] ?? panel)?.focus());

    // §8.16 — the page behind stays visible but inert, so neither pointer nor
    // assistive technology can reach it while a decision is being supported.
    const background = Array.from(document.body.children).filter(
      (child): child is HTMLElement => child instanceof HTMLElement && child !== host,
    );
    const previousInert = background.map((el) => el.inert);
    background.forEach((el) => (el.inert = true));

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        event.preventDefault();
        close();
        return;
      }
      if (event.key !== "Tab") return;
      const items = focusable();
      if (items.length === 0) {
        event.preventDefault();
        return;
      }
      const first = items[0];
      const last = items[items.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      }
      if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    }

    document.addEventListener("keydown", onKeyDown);
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    return () => {
      document.removeEventListener("keydown", onKeyDown);
      document.body.style.overflow = previousOverflow;
      background.forEach((el, i) => (el.inert = previousInert[i]));
      window.requestAnimationFrame(() => previousFocus?.focus());
    };
  }, [open, host, close]);

  if (!host || !open) return null;

  return createPortal(
    <>
      {/* Scrim — §8.14 backdrop value. No blur: glassmorphism is banned. */}
      <div
        className="fixed inset-0 z-30 bg-dls-scrim/45 transition-opacity duration-dls-base ease-dls motion-reduce:transition-none"
        onClick={close}
        aria-hidden="true"
      />
      <aside
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="dls-drawer-title"
        aria-describedby={description ? "dls-drawer-description" : undefined}
        tabIndex={-1}
        className="fixed right-0 top-0 z-[31] flex h-full w-full max-w-[min(440px,92vw)] flex-col border-l border-dls-border-subtle bg-dls-surface shadow-dls-modal transition-opacity duration-dls-base ease-dls motion-reduce:transition-none"
      >
        <div className="flex items-start justify-between gap-4 border-b border-dls-border-subtle px-[22px] py-5">
          <div className="min-w-0">
            <h2 id="dls-drawer-title" className="m-0 break-words text-[17px] font-extrabold leading-[1.3] tracking-[-0.2px] text-dls-text-primary">
              {title}
            </h2>
            {description ? (
              <p id="dls-drawer-description" className="mt-1 break-words text-[13.5px] leading-[1.5] text-dls-text-secondary">
                {description}
              </p>
            ) : null}
          </div>
          <button
            type="button"
            onClick={close}
            aria-label="Close"
            className="dls-focus-outline -mr-1 flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-dls-control text-dls-text-muted transition-colors duration-dls-fast ease-dls hover:bg-dls-bg-page hover:text-dls-text-primary"
          >
            <X className="h-4 w-4" aria-hidden="true" />
          </button>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto px-[22px] py-5">{children}</div>

        {footer ? <div className="border-t border-dls-border-subtle px-[22px] py-4">{footer}</div> : null}
      </aside>
    </>,
    host,
  );
}

export default SideDrawer;
