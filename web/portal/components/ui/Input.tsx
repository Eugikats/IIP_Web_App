// Design language v2 §8.5–8.6.
"use client";

import { forwardRef, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from "react";
import { Lock } from "lucide-react";
import { cn } from "@/lib/utils";

export interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  leadingIcon?: ReactNode;
  invalid?: boolean;
  managed?: boolean;
  managedCaption?: string;
}

export const Input = forwardRef<HTMLInputElement, InputProps>(function Input(
  { leadingIcon, invalid, managed, managedCaption, className, disabled, ...props },
  ref,
) {
  const isManaged = managed || disabled;
  return (
    <div>
      <div className="relative">
        {leadingIcon && (
          <span className="pointer-events-none absolute left-3 top-1/2 h-[15px] w-[15px] -translate-y-1/2 text-dls-text-muted [&>svg]:h-full [&>svg]:w-full">
            {leadingIcon}
          </span>
        )}
        <input
          ref={ref}
          disabled={isManaged}
          aria-invalid={invalid || undefined}
          className={cn(
            "w-full rounded-dls-control border bg-dls-surface px-3 py-[9px] text-sm text-dls-text-primary",
            "placeholder:text-dls-text-muted",
            "focus:outline-none focus:border-dls-primary focus:ring-[3px] focus:ring-dls-primary/[0.12]",
            leadingIcon ? "pl-9" : undefined,
            invalid ? "border-dls-danger-text" : "border-dls-border-input",
            isManaged ? "bg-dls-surface-sunken text-dls-text-muted cursor-not-allowed" : undefined,
            className,
          )}
          {...props}
        />
        {isManaged && (
          <Lock className="pointer-events-none absolute right-3 top-1/2 h-3 w-3 -translate-y-1/2 text-dls-text-muted" aria-hidden="true" />
        )}
      </div>
      {isManaged && managedCaption && (
        <p className="mt-1.5 text-xs text-dls-text-muted">{managedCaption}</p>
      )}
    </div>
  );
});

// design language v2's radius token table lists selects under --radius-control alongside
// buttons and inputs, same 8px radius, border, and focus ring as Input; no separate spec.
export const Select = forwardRef<HTMLSelectElement, SelectHTMLAttributes<HTMLSelectElement>>(function Select(
  { className, ...props },
  ref,
) {
  return (
    <select
      ref={ref}
      className={cn(
        "rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-[9px] text-sm text-dls-text-primary",
        "focus:outline-none focus:border-dls-primary focus:ring-[3px] focus:ring-dls-primary/[0.12]",
        className,
      )}
      {...props}
    />
  );
});

// Found missing while migrating FNOL.tsx (ui-migration-ledger.md screen-9 entry), design
// language v2's radius/border/focus contract in §8.5 doesn't name textareas separately from
// inputs, and every multi-line control in the app so far has been hand-styled per screen. Same
// contract as Input, just a taller, resizable-off element; same extension precedent as Select.
export const Textarea = forwardRef<HTMLTextAreaElement, TextareaHTMLAttributes<HTMLTextAreaElement>>(function Textarea(
  { className, rows = 4, ...props },
  ref,
) {
  return (
    <textarea
      ref={ref}
      rows={rows}
      className={cn(
        "w-full resize-none rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-[9px] text-sm text-dls-text-primary",
        "placeholder:text-dls-text-muted",
        "focus:outline-none focus:border-dls-primary focus:ring-[3px] focus:ring-dls-primary/[0.12]",
        className,
      )}
      {...props}
    />
  );
});

export interface FieldProps {
  label: string;
  hint?: string;
  error?: string;
  htmlFor?: string;
  children: ReactNode;
}

// §8.6, label -> optional hint -> control -> optional error/helper. 16px stack gap is the
// caller's responsibility (a parent flex/grid with gap-4); Field itself only orders its own parts.
export function Field({ label, hint, error, htmlFor, children }: FieldProps) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={htmlFor} className="text-[13.5px] font-bold text-dls-text-primary">
        {label}
      </label>
      {hint && <p className="text-[12.5px] text-dls-text-muted">{hint}</p>}
      {children}
      {error && (
        <p className="text-[12.5px] text-dls-danger-text" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
