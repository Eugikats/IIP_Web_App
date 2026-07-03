import { ReactNode } from "react";

export function PageHeader({
  title,
  description,
  actions,
  eyebrow,
}: {
  title: string;
  description?: string;
  actions?: ReactNode;
  eyebrow?: string;
}) {
  return (
    <section className="mb-6 flex flex-wrap items-start justify-between gap-4">
      <div>
        {eyebrow && (
          <p className="mb-1 text-[10px] font-bold uppercase tracking-wider text-muted">
            {eyebrow}
          </p>
        )}

        <h1 className="text-[28px] font-semibold leading-tight tracking-normal text-ink">
          {title}
        </h1>

        {description && (
          <p className="mt-1.5 max-w-2xl text-sm text-muted">
            {description}
          </p>
        )}
      </div>

      {actions && (
        <div className="flex flex-wrap items-center gap-2">{actions}</div>
      )}
    </section>
  );
}
