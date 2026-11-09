"use client";

import { ReactNode, useCallback, useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { AnimatePresence, motion, useReducedMotion } from "framer-motion";
import { X } from "lucide-react";

/**
 * Centred modal, design language v2 §8.14: a record a person reads and acts on without leaving the
 * list behind it. Focus rules match the side drawer: focus moves in on open, is trapped while open,
 * Escape closes, focus returns to the trigger, and the page behind is inert.
 *
 * It fades and rises 8px over 180ms on open and close; with reduced motion it appears without
 * movement. Backdrop is the standard scrim, never a blur.
 */
const FOCUSABLE =
  'button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), a[href], [tabindex]:not([tabindex="-1"])';

export function Modal({
  open,
  title,
  children,
  onClose,
  width = 960,
  bare = false,
}: {
  open: boolean;
  title: string;
  children: ReactNode;
  onClose: () => void;
  width?: number;
  /** The caller draws its own header and close button (the settings window). The dialog is then named by `title`. */
  bare?: boolean;
}) {
  const [host, setHost] = useState<HTMLDivElement | null>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const reduceMotion = useReducedMotion();

  useEffect(() => {
    const node = document.createElement("div");
    node.setAttribute("data-dls-modal-host", "");
    document.body.appendChild(node);
    setHost(node);
    return () => node.remove();
  }, []);

  const close = useCallback(() => onClose(), [onClose]);

  useEffect(() => {
    if (!open || !host) return;
    const previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const focusable = () => Array.from(panelRef.current?.querySelectorAll<HTMLElement>(FOCUSABLE) ?? []);
    window.requestAnimationFrame(() => (focusable()[0] ?? panelRef.current)?.focus());

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

  if (!host) return null;

  const rise = reduceMotion ? 0 : 8;
  return createPortal(
    <AnimatePresence>
      {open ? (
        <div className={`fixed inset-0 z-40 flex items-start justify-center overflow-y-auto ${bare ? "p-0 sm:px-4 sm:py-10" : "px-4 py-6 sm:py-10"}`}>
          <motion.div
            key="scrim"
            className="fixed inset-0 bg-dls-scrim/45"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: reduceMotion ? 0 : 0.18 }}
            onClick={close}
            aria-hidden="true"
          />
          <motion.div
            key="panel"
            ref={panelRef}
            role="dialog"
            aria-modal="true"
            aria-labelledby={bare ? undefined : "dls-modal-title"}
            aria-label={bare ? title : undefined}
            tabIndex={-1}
            className={`relative z-[41] flex w-full flex-col overflow-hidden border border-dls-border-subtle shadow-dls-modal ${bare ? "min-h-[100dvh] rounded-none bg-white sm:min-h-0 sm:rounded-2xl" : "rounded-dls-card bg-dls-bg-page"}`}
            style={{ maxWidth: `min(${width}px, 100%)` }}
            initial={{ opacity: 0, y: rise }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: rise }}
            transition={{ duration: reduceMotion ? 0 : 0.18, ease: [0.2, 0, 0, 1] }}
          >
            {bare ? null : (
              <div className="flex items-center justify-between gap-4 border-b border-dls-border-subtle bg-dls-surface px-6 py-4">
                <h2 id="dls-modal-title" className="m-0 text-[15px] font-bold text-dls-text-primary">{title}</h2>
                <button
                  type="button"
                  onClick={close}
                  aria-label="Close"
                  className="dls-focus-outline inline-flex h-9 w-9 items-center justify-center rounded-dls-control border border-dls-border-input bg-dls-surface text-dls-text-secondary hover:text-dls-text-primary"
                >
                  <X className="h-4 w-4" aria-hidden="true" />
                </button>
              </div>
            )}
            {children}
          </motion.div>
        </div>
      ) : null}
    </AnimatePresence>,
    host,
  );
}
