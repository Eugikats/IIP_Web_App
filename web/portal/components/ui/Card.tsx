// Design language v2 §8.8 (card) and §8.9 (stat tile).
import Link from "next/link";
import { type ReactNode } from "react";
import { cn } from "@/lib/utils";

export interface CardProps {
  title?: string;
  eyebrow?: string;
  headerAction?: ReactNode;
  children: ReactNode;
  className?: string;
}

// A card means "a distinct object or a distinct action", do not wrap every group in one.
export function Card({ title, eyebrow, headerAction, children, className }: CardProps) {
  const hasHeader = Boolean(title || headerAction);
  return (
    <section className={cn("rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4 shadow-dls-none", className)}>
      {hasHeader && (
        <div className="mb-3.5 flex items-start justify-between gap-3 border-b border-dls-border-subtle pb-3.5">
          <div className="min-w-0">
            {eyebrow && (
              <p className="mb-0.5 text-[11.5px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">{eyebrow}</p>
            )}
            {title && <h3 className="truncate text-[15px] font-bold text-dls-text-primary">{title}</h3>}
          </div>
          {headerAction && <div className="shrink-0">{headerAction}</div>}
        </div>
      )}
      {children}
    </section>
  );
}

type StatTone = "neutral" | "success" | "warning" | "danger" | "primary";

const statToneClass: Record<StatTone, string> = {
  neutral: "text-dls-text-primary",
  success: "text-dls-success-text",
  warning: "text-dls-warning-text",
  danger: "text-dls-danger-text",
  primary: "text-dls-primary",
};

export interface StatTileProps {
  label: string;
  value: string | number;
  tone?: StatTone;
  /** §8.9, every stat tile must be a link into the filtered list it counts. */
  href?: string;
  className?: string;
}

// A number the user cannot click through to is a decoration. A tile with a zero value renders
// "0", never hidden, never an em dash, so callers must not conditionally omit this component.
export function StatTile({ label, value, tone = "neutral", href, className }: StatTileProps) {
  const body = (
    <>
      <p className="text-[13.5px] text-dls-text-secondary">{label}</p>
      <p className={cn("mt-2 text-[30px] font-bold leading-[1.1] tabular-nums tracking-[-0.5px]", statToneClass[tone])}>{value}</p>
    </>
  );
  const shared = "block rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4 shadow-dls-none";
  if (href) {
    return (
      <Link
        href={href}
        className={cn(
          shared,
          "transition-colors duration-dls-fast ease-dls hover:border-dls-border-strong",
          "dls-focus-outline",
          className,
        )}
      >
        {body}
      </Link>
    );
  }
  return <div className={cn(shared, className)}>{body}</div>;
}
