"use client";

import { useEffect, useRef, useState } from "react";
import { CheckCircle2, Circle, MoreHorizontal } from "lucide-react";
import { Button } from "@/components/ui/Button";
import type { ClaimDetailView } from "@/lib/lovable-parity/data/claimReview";

export type DecisionAction = {
  key: string;
  label: string;
  onClick: () => void;
  disabled?: boolean;
  visible: boolean;
};

/// Outcome H: "the claim page answers what do I need to decide, and is it ready?" A readiness
/// checklist and completeness meter first, then one primary action, with everything else
/// collapsed behind a More menu rather than a wall of equally-weighted buttons.
export default function ClaimDecisionCard({
  detail,
  openReviewRequestCount,
  documentsReceivedCount,
  documentsRequiredCount,
  actions,
}: {
  detail: ClaimDetailView;
  openReviewRequestCount: number;
  documentsReceivedCount: number;
  documentsRequiredCount: number;
  actions: DecisionAction[];
}) {
  const visibleActions = actions.filter((action) => action.visible);
  const [primary, ...rest] = visibleActions;
  const [moreOpen, setMoreOpen] = useState(false);
  const moreRef = useRef<HTMLDivElement>(null);
  const moreTriggerRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (!moreOpen) return;
    function onClick(event: MouseEvent) {
      if (moreRef.current && !moreRef.current.contains(event.target as Node)) {
        setMoreOpen(false);
      }
    }
    function onKey(event: KeyboardEvent) {
      if (event.key === "Escape") {
        moreTriggerRef.current?.focus();
        setMoreOpen(false);
      }
    }
    document.addEventListener("mousedown", onClick);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onClick);
      document.removeEventListener("keydown", onKey);
    };
  }, [moreOpen]);

  const slaBreached = detail.slaDueAt ? new Date(detail.slaDueAt).getTime() < Date.now() : false;
  const hasMissingDocuments = documentsReceivedCount < documentsRequiredCount;
  const hasRiskSignals = detail.advisoryRiskSignals.length > 0;

  return (
    <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-5">
      <div className="mb-3 text-[14px] font-bold text-dls-text-primary">What needs a decision</div>

      <ul className="mb-4 space-y-1.5 text-[13px]">
        <ReadinessRow
          ok={!hasMissingDocuments}
          label={documentsRequiredCount > 0
            ? `${documentsReceivedCount} of ${documentsRequiredCount} required documents received`
            : "No documents required for this claim type"}
        />
        <ReadinessRow ok={!slaBreached} label={slaBreached ? "Service standard has been breached" : "Within its service standard"} />
        <ReadinessRow ok={!hasRiskSignals} label={hasRiskSignals ? `${detail.advisoryRiskSignals.length} advisory risk signal(s) to review` : "No advisory risk signals"} />
        <ReadinessRow ok={openReviewRequestCount === 0} label={openReviewRequestCount > 0 ? `${openReviewRequestCount} open re-review request(s)` : "No open re-review requests"} />
      </ul>

      {visibleActions.length === 0 ? (
        <p className="text-[12.5px] text-dls-text-muted">Your role does not include any action on this claim.</p>
      ) : (
        <div className="flex items-center gap-2">
          <Button variant="primary" disabled={primary.disabled} onClick={primary.onClick}>
            {primary.label}
          </Button>
          {rest.length > 0 && (
            <div ref={moreRef} className="relative">
              <Button ref={moreTriggerRef} variant="secondary" icon={<MoreHorizontal aria-hidden="true" />} onClick={() => setMoreOpen((open) => !open)} aria-haspopup="menu" aria-expanded={moreOpen}>
                More actions
              </Button>
              {moreOpen && (
                <div role="menu" className="absolute left-0 top-full z-20 mt-1.5 w-64 rounded-dls-overlay border border-dls-border-subtle bg-dls-surface p-1.5 shadow-dls-popover">
                  {rest.map((action) => (
                    <button
                      key={action.key}
                      type="button"
                      role="menuitem"
                      disabled={action.disabled}
                      onClick={() => {
                        // The menu item is removed as its dialog opens. Return focus to the
                        // persistent menu trigger first so the dialog captures a live target.
                        moreTriggerRef.current?.focus();
                        setMoreOpen(false);
                        action.onClick();
                      }}
                      className="dls-focus-outline block w-full rounded-dls-tight px-3 py-2 text-left text-[13px] font-semibold text-dls-text-primary hover:bg-dls-bg-page disabled:cursor-not-allowed disabled:opacity-50"
                    >
                      {action.label}
                    </button>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function ReadinessRow({ ok, label }: { ok: boolean; label: string }) {
  return (
    <li className="flex items-center gap-2 text-dls-text-secondary">
      {ok ? (
        <CheckCircle2 className="h-3.5 w-3.5 shrink-0 text-dls-success-dot" aria-hidden="true" />
      ) : (
        <Circle className="h-3.5 w-3.5 shrink-0 text-dls-text-muted" aria-hidden="true" />
      )}
      <span>{label}</span>
    </li>
  );
}
