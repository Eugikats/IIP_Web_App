"use client";
import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import { X } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * First-run orientation, implements docs/03-design-architecture/iip_first_run_orientation_spec.md.
 *
 * The governing constraint from that spec: the orientation is a guest. It arrives once, it can be
 * refused in one action, it never blocks work, and after it leaves there is no trace of it in the
 * interface. Every decision here answers to that sentence.
 *
 * The load-bearing requirement is §3: a step only renders if the signed-in user can actually open
 * the destination it describes. Pointing someone at a screen their role cannot reach is a promise
 * the product breaks the moment they click, and teaches them the interface says untrue things
 * about their own account. That is why this is a hand-written component rather than intro.js or
 * driver.js, none of them can filter steps by permission, and all of them ship a stylesheet that
 * would then have to be fought to remove shadows and accents DESIGN.md forbids (spec §7).
 */

const STORAGE_KEY = "iip.orientation.seen.v1";

/** Cleared by the profile menu's "Show me around", which is the only way to re-run this. */
export function resetFirstRunOrientation() {
  try {
    window.localStorage.removeItem(STORAGE_KEY);
  } catch {
    // Blocked storage, nothing to clear, and the tour will not have run anyway.
  }
}

type Step = {
  /** Matches a data-tour attribute in the rail. Null for the closing step, which anchors to nothing. */
  anchor: string | null;
  title: string;
  body: string;
};

// Copy per spec §4: two sentences at most, the language of the job rather than the system, and
// nothing promised that is not built.
const STEPS: Step[] = [
  {
    anchor: "/app",
    title: "Start here",
    body: "Ask for what you need in plain words, “what needs my attention today?” or “what does the policy say about flood damage?”. Answers about cover come only from your approved documents, with a link to the source.",
  },
  {
    anchor: "/app/work-items",
    title: "What is waiting for you",
    body: "Everything assigned to you, in one list, claims to review, approvals to give, documents to check. This is the one place that answers “what should I do next?”.",
  },
  {
    anchor: "/app/claims",
    title: "Every claim",
    body: "Search, filter and open any claim in your workspace. Each one shows its full history: what happened, who did it, and what the AI prepared along the way.",
  },
  {
    anchor: "/app/channels",
    title: "Messages from customers",
    body: "WhatsApp, email and web chat in one place, sorted by what each person is asking about. AI drafts replies; nothing reaches a customer until you approve it.",
  },
  {
    anchor: "/app/documents",
    title: "Everything received",
    body: "Every document that has arrived across claims and channels. The AI reads what it can, and flags anything it is unsure about so you can check it.",
  },
  {
    anchor: "profile",
    title: "Settings and everything else",
    body: "Your account, search, connected channels, the knowledge base and the admin console live behind here.",
  },
  {
    anchor: null,
    title: "The AI prepares, you decide",
    body: "It can summarise, draft and flag, but it never approves, rejects or settles a claim, and nothing goes to a customer without your say-so.",
  },
];

type AnchorRect = { top: number; left: number; width: number; height: number };

function readAnchor(selectorValue: string): AnchorRect | null {
  const element = document.querySelector<HTMLElement>(`[data-tour="${CSS.escape(selectorValue)}"]`);
  if (!element) return null;
  const rect = element.getBoundingClientRect();
  if (rect.width === 0 && rect.height === 0) return null;
  return { top: rect.top, left: rect.left, width: rect.width, height: rect.height };
}

export default function FirstRunOrientation({
  userId,
  ready,
  pathname,
}: {
  userId: string | null;
  ready: boolean;
  pathname: string;
}) {
  const [open, setOpen] = useState(false);
  const [index, setIndex] = useState(0);
  const cardRef = useRef<HTMLDivElement>(null);
  const returnFocusRef = useRef<HTMLElement | null>(null);

  // Spec §6 "Error": a step whose anchor is not in the DOM is dropped rather than rendering a card
  // pointing at nothing. Recomputed only when the tour opens, by which time the rail has rendered.
  const steps = useMemo(() => {
    if (!open) return STEPS;
    return STEPS.filter((step) => step.anchor === null || readAnchor(step.anchor) !== null);
  }, [open]);

  useEffect(() => {
    // Spec §2 and §6 "Loading": waits for permissions to resolve, so a step can never be shown or
    // dropped based on access that was still unknown.
    if (!ready || !userId) return;
    // Spec section 2: first arrival at /app, the landing screen. This component is mounted in the
    // shell, which wraps every /app route -- without this check a user following a deep link into
    // a claim would be shown "Start here" pointing at the Assistant while looking at something
    // else entirely. An orientation that describes a screen you are not on is worse than none.
    if (pathname !== "/app") return;
    let seen: string | null = null;
    try {
      seen = window.localStorage.getItem(STORAGE_KEY);
    } catch {
      // Spec §2: storage unavailable means the tour does not run. A private window must not
      // produce a tour on every single page load, which is worse than never showing it.
      return;
    }
    if (seen === userId) return;

    // Spec §3: fewer than two reachable steps is a popup, not an orientation.
    const reachable = STEPS.filter((step) => step.anchor === null || readAnchor(step.anchor) !== null);
    if (reachable.length < 2) return;

    returnFocusRef.current = document.activeElement as HTMLElement | null;
    setOpen(true);
  }, [ready, userId, pathname]);

  const dismiss = useCallback(() => {
    setOpen(false);
    setIndex(0);
    try {
      if (userId) window.localStorage.setItem(STORAGE_KEY, userId);
    } catch {
      // Not persisted, it will offer itself again next time. Better than failing to close.
    }
    returnFocusRef.current?.focus?.();
  }, [userId]);

  const step = steps[Math.min(index, steps.length - 1)];
  const isLast = index >= steps.length - 1;

  const advance = useCallback(() => {
    if (isLast) dismiss();
    else setIndex((current) => current + 1);
  }, [dismiss, isLast]);

  useEffect(() => {
    if (!open) return;
    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        event.preventDefault();
        dismiss();
      }
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [open, dismiss]);

  useEffect(() => {
    if (open) cardRef.current?.focus();
  }, [open, index]);

  // Position is read from the live element rather than stored, so a collapsed rail, a resized
  // window or a scrolled nav cannot leave the card pointing at where something used to be.
  const [rect, setRect] = useState<AnchorRect | null>(null);
  useLayoutEffect(() => {
    if (!open || !step?.anchor) {
      setRect(null);
      return;
    }
    function update() {
      setRect(step.anchor ? readAnchor(step.anchor) : null);
    }
    update();
    window.addEventListener("resize", update);
    window.addEventListener("scroll", update, true);
    return () => {
      window.removeEventListener("resize", update);
      window.removeEventListener("scroll", update, true);
    };
  }, [open, step?.anchor]);

  if (!open || !step) return null;

  const CARD_WIDTH = 340;
  const GAP = 14;
  // Anchored beside the rail where there is one, centred otherwise (the closing step). Clamped so
  // the card can never sit partly off-screen on a short viewport.
  const cardStyle: React.CSSProperties = rect
    ? {
        position: "fixed",
        left: Math.min(rect.left + rect.width + GAP, window.innerWidth - CARD_WIDTH - 16),
        top: Math.max(16, Math.min(rect.top - 8, window.innerHeight - 260)),
        width: CARD_WIDTH,
      }
    : {
        position: "fixed",
        left: "50%",
        top: "50%",
        width: CARD_WIDTH,
        transform: "translate(-50%, -50%)",
      };

  return (
    <>
      {/* Plain scrim, no spotlight cut-out and no blur (spec §5). Clicking it dismisses, and it
          covers everything behind so no click half-lands on the app underneath. */}
      <div
        className="fixed inset-0 z-[70] bg-dls-scrim/45 animate-fade-in"
        onClick={dismiss}
        aria-hidden="true"
      />

      {/* The ring marks what is being talked about, the one sanctioned use of the accent. */}
      {rect && (
        <div
          className="pointer-events-none fixed z-[71] rounded-dls-control ring-2 ring-dls-primary transition-all duration-dls-panel ease-dls"
          style={{ top: rect.top - 2, left: rect.left - 2, width: rect.width + 4, height: rect.height + 4 }}
          aria-hidden="true"
        />
      )}

      <div
        ref={cardRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="orientation-title"
        tabIndex={-1}
        style={cardStyle}
        className={cn(
          "z-[72] rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4 shadow-l3",
          "dls-focus-outline animate-fade-in transition-[top,left] duration-dls-panel ease-dls",
        )}
      >
        <div className="mb-2 flex items-start justify-between gap-3">
          <div>
            <div className="text-[10.5px] font-bold uppercase tracking-[0.08em] text-dls-text-muted">
              Step {index + 1} of {steps.length}
            </div>
            <h2 id="orientation-title" className="mt-0.5 text-[15px] font-bold text-dls-text-primary">
              {step.title}
            </h2>
          </div>
          <button
            type="button"
            onClick={dismiss}
            aria-label="Close the walkthrough"
            className="dls-focus-outline -mr-1 -mt-1 flex h-7 w-7 shrink-0 items-center justify-center rounded-dls-control text-dls-text-muted hover:bg-dls-bg-page hover:text-dls-text-secondary"
          >
            <X className="h-4 w-4" aria-hidden="true" />
          </button>
        </div>

        <p className="text-[13px] leading-relaxed text-dls-text-secondary">{step.body}</p>

        <div className="mt-4 flex items-center justify-between gap-3">
          <button
            type="button"
            onClick={dismiss}
            className="dls-focus-outline rounded-dls-control px-1 text-[12.5px] font-semibold text-dls-text-muted hover:text-dls-text-secondary"
          >
            Skip
          </button>
          <div className="flex items-center gap-2">
            {index > 0 && (
              <button
                type="button"
                onClick={() => setIndex((current) => current - 1)}
                className="dls-focus-outline rounded-dls-control border border-dls-border-input px-3 py-[7px] text-[12.5px] font-semibold text-dls-text-primary hover:bg-dls-bg-page"
              >
                Back
              </button>
            )}
            <button
              type="button"
              onClick={advance}
              className="dls-focus-outline rounded-dls-control bg-dls-primary px-4 py-[7px] text-[12.5px] font-bold text-dls-text-inverse transition-colors duration-dls-fast ease-dls hover:bg-dls-primary-hover"
            >
              {isLast ? "Done" : "Next"}
            </button>
          </div>
        </div>
      </div>
    </>
  );
}
