"use client";

import type { ReactNode } from "react";
import { AlertTriangle, CheckCircle2 } from "lucide-react";
import iipLogo from "@/assets/iip-logo";
import { Input, Field as UIField } from "@/components/ui/Input";

/** The plain page around sign-up, sign-in, reset and invitation screens: one calm column, the platform's mark, nothing else. */
export function AuthFrame({ title, intro, children }: { title: string; intro?: string; children: ReactNode }) {
  return (
    <main className="grid min-h-screen place-items-center bg-dls-bg-page px-6 py-10 text-dls-text-primary">
      <div className="w-full max-w-md">
        <div className="mb-8 flex items-center gap-3">
          <span className="grid h-10 w-10 place-items-center rounded-dls-control border border-dls-border-subtle bg-dls-surface">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src={iipLogo.url} alt="IIP" className="h-8 w-8 object-contain" />
          </span>
          <div>
            <div className="text-sm font-semibold">IIP Operations</div>
            <div className="text-xs text-dls-text-muted">Insurance Intelligence Platform</div>
          </div>
        </div>
        <h1 className="text-2xl font-semibold leading-tight">{title}</h1>
        {intro ? <p className="mt-2 text-sm leading-6 text-dls-text-secondary">{intro}</p> : null}
        <div className="mt-6">{children}</div>
      </div>
    </main>
  );
}

export function FormError({ message }: { message: string | null }) {
  if (!message) return null;
  return (
    <div role="alert" className="mt-4 flex items-start gap-2 rounded-dls-control border border-dls-danger-border bg-dls-danger-tint px-3 py-3 text-xs leading-5 text-dls-danger-text">
      <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
      <span>{message}</span>
    </div>
  );
}

export function FormNotice({ message, testId }: { message: string | null; testId?: string }) {
  if (!message) return null;
  return (
    <div role="status" data-testid={testId} className="mt-4 flex items-start gap-2 rounded-dls-control border border-dls-border-subtle bg-dls-surface px-3 py-3 text-sm leading-5 text-dls-text-primary">
      <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-dls-success-text" aria-hidden="true" />
      <span>{message}</span>
    </div>
  );
}

export function TextField({
  id, label, value, onChange, type = "text", autoComplete, hint, error, required = true,
}: {
  id: string; label: string; value: string; onChange: (value: string) => void; type?: string; autoComplete?: string; hint?: string; error?: string; required?: boolean;
}) {
  return (
    <UIField label={label} htmlFor={id}>
      <Input id={id} type={type} value={value} autoComplete={autoComplete} required={required} onChange={(event) => onChange(event.target.value)} aria-invalid={error ? true : undefined} />
      {hint && !error ? <p className="mt-1 text-[11.5px] text-dls-text-muted">{hint}</p> : null}
      {error ? <p className="mt-1 text-[11.5px] text-dls-danger-text">{error}</p> : null}
    </UIField>
  );
}
