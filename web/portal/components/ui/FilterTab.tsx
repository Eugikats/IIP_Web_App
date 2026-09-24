// Design language v2 §8.11, filter tabs / segmented pills.
//
// Found while migrating /app/channels (ui-migration-ledger.md screen-7 entry): five existing
// hand-rolled tab implementations across four already-Done screens (AgentWorkItems, ClaimsQueue,
// DocumentExtraction, ReviewWorkspace, Workflows) render their *active* tab as a solid violet fill
// with white text, §8.11 specifies a light tint background with violet text and border, never a
// solid fill (solid violet is §8.1's primary-button treatment, not a tab's). This primitive is the
// spec-correct version, for this screen and to migrate those five call sites onto later, see the
// ledger for why that migration is a separate, dedicated pass rather than bundled into this screen.
"use client";

import { cn } from "@/lib/utils";

export interface FilterTabProps {
  active: boolean;
  onClick: () => void;
  children: React.ReactNode;
  /** Shown in a lighter weight after the label. Every tab shows its count per §8.11. */
  count?: number | string;
  disabled?: boolean;
  className?: string;
}

export function FilterTab({ active, onClick, children, count, disabled, className }: FilterTabProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      aria-pressed={active}
      className={cn(
        "dls-focus-outline inline-flex min-h-11 items-center gap-1.5 rounded-dls-pill border px-3.5 py-2 text-[13px] font-semibold transition-colors duration-dls-fast ease-dls",
        "disabled:cursor-not-allowed disabled:opacity-55",
        active
          ? "border-dls-primary-border bg-dls-primary-soft-alt text-dls-primary-accent"
          : "border-dls-border-input bg-dls-surface text-dls-text-primary hover:border-dls-primary",
        className,
      )}
    >
      {children}
      {count !== undefined && (
        <span className={cn("text-[12px] font-bold", active ? "text-dls-primary-accent" : "text-dls-text-muted")}>{count}</span>
      )}
    </button>
  );
}
