// Migrated onto design language v2 §8.10. External API unchanged, callers still supply their own
// <thead>/<tbody> children; this only governs the outer surface.
import { type ReactNode } from "react";

export function DataTable({ children, label }: { children: ReactNode; label?: string }) {
  return (
    <div className="overflow-x-auto rounded-dls-card border border-dls-border-subtle bg-dls-surface">
      <table className="min-w-full text-[13.5px]" aria-label={label}>
        {children}
      </table>
    </div>
  );
}
