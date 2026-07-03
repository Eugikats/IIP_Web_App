import { ReactNode } from "react";

export function DataTable({ children, label }: { children: ReactNode; label?: string }) {
  return (
    <div className="overflow-x-auto rounded-xl border border-line bg-surface/80">
      <table className="min-w-full divide-y divide-line text-sm" aria-label={label}>
        {children}
      </table>
    </div>
  );
}
