"use client";

/** Small shared pieces for the organisation platform screens, in IIP's design language (borders, one blue accent, neutral status pills). */

import type { ButtonHTMLAttributes, ComponentPropsWithoutRef, ElementType, ReactNode } from "react";
import { AlertCircle, Loader2, SearchX } from "lucide-react";
import { cn } from "@/lib/utils";
import { StatusPill as IipStatusPill, type PillTone } from "@/components/ui/StatusPill";

export const primaryButton =
  "inline-flex items-center justify-center gap-1.5 whitespace-nowrap rounded-dls-control bg-dls-primary px-4 py-2 text-[13px] font-bold text-dls-text-inverse transition-colors hover:bg-dls-primary-hover disabled:cursor-not-allowed disabled:opacity-60";
export const secondaryButton =
  "inline-flex items-center justify-center gap-1.5 whitespace-nowrap rounded-dls-control border border-dls-border-input bg-dls-surface px-4 py-2 text-[13px] font-bold text-dls-text-primary transition-colors hover:bg-dls-bg-page disabled:cursor-not-allowed disabled:opacity-60";
export const dangerButton =
  "inline-flex items-center justify-center gap-1.5 whitespace-nowrap rounded-dls-control border border-dls-border-input bg-dls-surface px-4 py-2 text-[13px] font-bold text-dls-danger-text transition-colors hover:bg-dls-bg-page disabled:cursor-not-allowed disabled:opacity-60";
export const inputClass =
  "w-full rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-2 text-[13px] text-dls-text-primary outline-none transition-colors placeholder:text-dls-text-faint focus:border-dls-primary";
export const labelClass = "mb-1 block text-[12px] font-semibold text-dls-text-secondary";

export function Button({ variant = "primary", className, ...props }: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: "primary" | "secondary" | "danger" }) {
  const base = variant === "primary" ? primaryButton : variant === "danger" ? dangerButton : secondaryButton;
  return <button type="button" className={cn(base, className)} {...props} />;
}

type Tone = "success" | "warning" | "danger" | "info" | "neutral" | "violet";

/**
 * Status is a neutral pill, a 6px dot and a word (DESIGN.md rule 5), never a tinted pill. The tone names map onto IIP's pill tones.
 * `dot` is accepted for source compatibility; the dot is always drawn.
 */
export function StatusPill({ children, tone = "neutral" }: { children: ReactNode; tone?: Tone; dot?: boolean; className?: string }) {
  const mapped: PillTone = tone === "violet" ? "info" : tone;
  return <IipStatusPill tone={mapped} label={typeof children === "string" ? children : String(children ?? "")} />;
}

/** The segmented tabs used on Acquisition. */
export function SegmentedTabs<T extends string>({
  tabs,
  value,
  onChange,
  label,
}: {
  tabs: readonly { key: T; label: string; count?: number }[];
  value: T;
  onChange: (key: T) => void;
  label: string;
}) {
  return (
    <div className="max-w-full overflow-x-auto">
      <div className="inline-flex rounded-dls-control border border-dls-border-subtle bg-dls-surface p-0.5" role="tablist" aria-label={label}>
        {tabs.map((t) => (
          <button
            key={t.key}
            type="button"
            role="tab"
            aria-selected={value === t.key}
            onClick={() => onChange(t.key)}
            className={cn(
              "inline-flex items-center gap-1.5 whitespace-nowrap rounded-dls-chip px-3 py-1.5 text-[12px] font-semibold transition-colors",
              value === t.key ? "bg-dls-primary-soft text-dls-primary" : "text-dls-text-secondary hover:bg-dls-bg-page hover:text-dls-text-primary"
            )}
          >
            {t.label}
            {typeof t.count === "number" && t.count > 0 ? (
              <span className={cn("rounded-full px-1.5 text-[10px] font-bold", value === t.key ? "bg-dls-primary text-dls-text-inverse" : "bg-dls-bg-page text-dls-text-secondary")}>
                {t.count}
              </span>
            ) : null}
          </button>
        ))}
      </div>
    </div>
  );
}

/** Uppercase divider label, as on the Command Center. */
export function SectionDivider({ children }: { children: ReactNode }) {
  return (
    <div className="mb-4 flex items-center gap-3">
      <div className="h-px flex-1 bg-dls-border-subtle" />
      <p className="shrink-0 text-[11px] font-semibold uppercase tracking-[0.14em] text-dls-text-muted">{children}</p>
      <div className="h-px flex-1 bg-dls-border-subtle" />
    </div>
  );
}

export function KeyValue({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="min-w-0">
      <p className="text-[11px] font-semibold uppercase tracking-wide text-dls-text-muted">{label}</p>
      <div className="mt-0.5 truncate text-[13px] font-medium text-dls-text-primary">{children}</div>
    </div>
  );
}

export function InlineError({ message }: { message: string | null }) {
  if (!message) return null;
  return (
    <p role="alert" className="rounded-dls-control border border-dls-border-subtle bg-dls-surface px-3 py-2 text-[12px] font-medium text-dls-danger-text">
      {message}
    </p>
  );
}

export function DashboardSectionHeader({
  title,
  description,
  eyebrow,
  icon,
  actions,
  className,
}: {
  title: ReactNode;
  description?: ReactNode;
  eyebrow?: ReactNode;
  icon?: ReactNode;
  actions?: ReactNode;
  className?: string;
}) {
  return (
    <div className={cn("flex flex-wrap items-start justify-between gap-3", className)}>
      <div className="flex min-w-0 items-start gap-3">
        {icon ? (
          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-dls-control border border-dls-border-subtle bg-dls-surface text-dls-text-secondary">{icon}</div>
        ) : null}
        <div className="min-w-0">
          {eyebrow ? <p className="mb-1 text-[11px] font-semibold uppercase tracking-wide text-dls-text-muted">{eyebrow}</p> : null}
          <h1 className="text-[22px] font-bold leading-tight text-dls-text-primary">{title}</h1>
          {description ? <p className="mt-1 max-w-3xl text-[13px] leading-relaxed text-dls-text-secondary">{description}</p> : null}
        </div>
      </div>
      {actions ? <div className="flex shrink-0 flex-wrap items-center gap-2">{actions}</div> : null}
    </div>
  );
}

type StateProps = { title: ReactNode; description?: ReactNode; action?: ReactNode; className?: string };

function StateShell({ title, description, action, className, icon, role }: StateProps & { icon: ReactNode; role?: "status" | "alert" }) {
  return (
    <div role={role} className={cn("flex flex-col items-center justify-center rounded-dls-card border border-dls-border-subtle bg-dls-surface p-6 text-center", className)}>
      <div className="mb-3 flex h-10 w-10 items-center justify-center rounded-dls-control border border-dls-border-subtle bg-dls-bg-page text-dls-text-secondary">{icon}</div>
      <p className="text-[14px] font-bold text-dls-text-primary">{title}</p>
      {description ? <p className="mt-1 max-w-md text-[12px] leading-relaxed text-dls-text-secondary">{description}</p> : null}
      {action ? <div className="mt-4">{action}</div> : null}
    </div>
  );
}

export function PlatformLoadingState(props: StateProps) {
  return <StateShell {...props} role="status" icon={<Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />} />;
}

export function PlatformErrorState(props: StateProps) {
  return <StateShell {...props} role="alert" icon={<AlertCircle className="h-4 w-4 text-dls-danger-text" aria-hidden="true" />} />;
}

export function PlatformEmptyState(props: StateProps) {
  return <StateShell {...props} icon={<SearchX className="h-4 w-4" aria-hidden="true" />} />;
}

type CardProps<T extends ElementType = "section"> = {
  as?: T;
  children: ReactNode;
  className?: string;
  interactive?: boolean;
  padding?: "none" | "sm" | "md" | "lg";
  tone?: "default" | "subtle" | "accent";
} & Omit<ComponentPropsWithoutRef<T>, "as" | "children" | "className">;

const cardPadding = { none: "", sm: "p-3", md: "p-4", lg: "p-5" };

/** A bordered static card: border, no shadow, no lift on hover (DESIGN.md rule 4). */
export function DashboardCard<T extends ElementType = "section">({ as, children, className, interactive = false, padding = "md", tone: _tone, ...props }: CardProps<T>) {
  const Component = (as ?? "section") as ElementType;
  return (
    <Component
      className={cn("rounded-dls-card border border-dls-border-subtle bg-dls-surface text-dls-text-primary", cardPadding[padding], interactive && "transition-colors hover:border-dls-border-strong", className)}
      {...props}
    >
      {children}
    </Component>
  );
}
