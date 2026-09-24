// Migrated onto design language v2 §8.8 (card): border, radius-card, box-shadow none, no more
// .glass glassmorphism. External API unchanged.
import { type ReactNode } from "react";
import { cn } from "@/lib/utils";

export function SectionCard({
  title,
  description,
  actions,
  children,
  className = "",
  eyebrow,
}: {
  title?: string;
  description?: string;
  actions?: ReactNode;
  children: ReactNode;
  className?: string;
  eyebrow?: string;
}) {
  return (
    <section className={cn("rounded-dls-card border border-dls-border-subtle bg-dls-surface shadow-dls-none", className)}>
      {(title || description || actions) && (
        <div className="flex flex-wrap items-start justify-between gap-3 border-b border-dls-border-subtle px-5 py-4">
          <div>
            {eyebrow && (
              <p className="mb-1 text-[11.5px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">{eyebrow}</p>
            )}
            {title && <h3 className="text-[15px] font-bold leading-snug text-dls-text-primary">{title}</h3>}
            {description && <p className="mt-1 max-w-xl text-xs text-dls-text-secondary">{description}</p>}
          </div>
          {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
        </div>
      )}
      <div className="p-5">{children}</div>
    </section>
  );
}
