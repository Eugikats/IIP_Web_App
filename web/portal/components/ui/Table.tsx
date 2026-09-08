// Design language v2 §8.10. No zebra striping, no vertical rules, no outer border beyond the
// containing card.
import { type ReactNode, type ThHTMLAttributes, type TdHTMLAttributes } from "react";
import { ChevronDown } from "lucide-react";
import { cn } from "@/lib/utils";

export function Table({ children, label, className }: { children: ReactNode; label?: string; className?: string }) {
  return (
    <div className={cn("overflow-x-auto rounded-dls-card border border-dls-border-subtle bg-dls-surface", className)}>
      <table className="min-w-full text-[13.5px]" aria-label={label}>
        {children}
      </table>
    </div>
  );
}

export function TableHead({ children }: { children: ReactNode }) {
  return <thead>{children}</thead>;
}

export function TableBody({ children }: { children: ReactNode }) {
  return <tbody>{children}</tbody>;
}

export function TableRow({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <tr className={cn("border-b border-dls-border-subtle last:border-0 hover:bg-dls-bg-page", className)}>{children}</tr>
  );
}

export interface TableHeaderCellProps extends ThHTMLAttributes<HTMLTableCellElement> {
  numeric?: boolean;
  sorted?: "asc" | "desc" | false;
}

export function TableHeaderCell({ children, numeric, sorted, className, ...props }: TableHeaderCellProps) {
  return (
    <th
      scope="col"
      className={cn(
        "border-b border-dls-border-subtle px-2 py-2.5 text-[11.5px] font-bold uppercase tracking-[0.04em] text-dls-text-muted",
        numeric ? "text-right" : "text-left",
        className,
      )}
      {...props}
    >
      <span className="inline-flex items-center gap-1">
        {children}
        {sorted && <ChevronDown className={cn("h-3.5 w-3.5", sorted === "asc" && "rotate-180")} aria-hidden="true" />}
      </span>
    </th>
  );
}

export interface TableCellProps extends TdHTMLAttributes<HTMLTableCellElement> {
  numeric?: boolean;
}

export function TableCell({ children, numeric, className, ...props }: TableCellProps) {
  return (
    <td
      className={cn(
        "px-2 py-3 align-middle text-dls-text-primary",
        numeric ? "text-right tabular-nums" : undefined,
        className,
      )}
      {...props}
    >
      {children}
    </td>
  );
}
