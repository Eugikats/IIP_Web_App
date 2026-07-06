import { ReactNode } from "react";

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
    <section className={`glass rounded-xl ${className}`}>
      {(title || description || actions) && (
        <div className="flex flex-wrap items-start justify-between gap-3 border-b border-line px-5 py-4">
          <div>
            {eyebrow && (
              <p className="mb-1 text-[10px] font-bold uppercase tracking-wider text-muted">
                {eyebrow}
              </p>
            )}
            {title && (
              <h3 className="text-base font-semibold text-ink leading-snug">{title}</h3>
            )}
            {description && (
              <p className="mt-1 text-xs text-muted max-w-xl">{description}</p>
            )}
          </div>
          {actions && (
            <div className="flex flex-wrap items-center gap-2">{actions}</div>
          )}
        </div>
      )}
      <div className="p-5">{children}</div>
    </section>
  );
}
