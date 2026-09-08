// Migrated onto design language v2 §3.1–3.2. External API unchanged except for the new optional
// `breadcrumb` prop (additive — existing callers that omit it are unaffected).
import { type ReactNode } from "react";

export function PageHeader({
  title,
  description,
  actions,
  eyebrow,
  breadcrumb,
}: {
  title: string;
  description?: string;
  actions?: ReactNode;
  eyebrow?: string;
  /** §3.1 — "Section › Object › Current page", current page not a link. */
  breadcrumb?: { label: string; href?: string }[];
}) {
  return (
    <section className="mb-6">
      {breadcrumb && breadcrumb.length > 0 && (
        <nav aria-label="Breadcrumb" className="mb-2 flex items-center gap-1.5 text-[13px] text-dls-text-muted">
          {breadcrumb.map((crumb, index) => {
            const isLast = index === breadcrumb.length - 1;
            return (
              <span key={`${crumb.label}-${index}`} className="flex items-center gap-1.5">
                {index > 0 && <span aria-hidden="true">&rsaquo;</span>}
                {crumb.href && !isLast ? (
                  <a href={crumb.href} className="hover:text-dls-text-primary hover:underline">
                    {crumb.label}
                  </a>
                ) : (
                  <span className={isLast ? "font-semibold text-dls-text-primary" : undefined}>{crumb.label}</span>
                )}
              </span>
            );
          })}
        </nav>
      )}
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          {eyebrow && (
            <p className="mb-1 text-[11.5px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">{eyebrow}</p>
          )}
          <h1 className="text-[23px] font-bold leading-[1.25] tracking-[-0.41px] text-dls-text-primary">{title}</h1>
          {description && <p className="mt-1.5 max-w-2xl text-sm text-dls-text-secondary">{description}</p>}
        </div>
        {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
      </div>
    </section>
  );
}
