// Design language v2 §8.24, every data surface implements six states, and none of them is a
// spinner alone.
import { type ReactNode } from "react";
import { AlertCircle, ShieldAlert } from "lucide-react";
import { Button } from "./Button";
import { StatusPill } from "./StatusPill";

export function LoadingSkeleton({ rows = 3, className }: { rows?: number; className?: string }) {
  return (
    <div className={className} role="status" aria-label="Loading">
      <div className="flex flex-col gap-2">
        {Array.from({ length: rows }).map((_, index) => (
          <div key={index} className="h-11 rounded-dls-control bg-dls-surface-sunken" />
        ))}
      </div>
    </div>
  );
}

// `title` is optional per §8.24 ("one sentence saying what will appear here" is the minimum
// requirement, not a ceiling), added while migrating screen 10's checkpoint
// (ui-migration-ledger.md) after consolidating RecentConversations.tsx onto this primitive
// dropped a distinct headline ("No conversations yet" / "No archived conversations") three
// release gates asserted on. Omit it for the plain single-sentence case most screens use.
export function EmptyStateSurface({ title, message, action }: { title?: string; message: string; action?: { label: string; onClick: () => void } }) {
  return (
    <div className="flex flex-col items-center gap-2 px-5 py-10 text-center">
      {title && <p className="text-[13.5px] font-bold text-dls-text-primary">{title}</p>}
      <p className="max-w-sm text-[13.5px] text-dls-text-secondary">{message}</p>
      {action && (
        <Button variant="secondary" onClick={action.onClick} className="mt-1">
          {action.label}
        </Button>
      )}
    </div>
  );
}

// Distinct from EmptyStateSurface, never show the same message for both.
export function EmptyFilteredStateSurface({ title, onClear }: { title?: string; onClear: () => void }) {
  return (
    <div className="flex flex-col items-center gap-2 px-5 py-10 text-center">
      {title && <p className="text-[13.5px] font-bold text-dls-text-primary">{title}</p>}
      <p className="text-[13.5px] text-dls-text-secondary">No items match these filters.</p>
      <Button variant="ghost" onClick={onClear} className="mt-1">
        Clear filters
      </Button>
    </div>
  );
}

export function ErrorStateSurface({
  title = "Something went wrong",
  message,
  correlationId,
  onRetry,
}: {
  title?: string;
  message: string;
  correlationId?: string;
  onRetry?: () => void;
}) {
  return (
    <div className="flex flex-col gap-2 px-5 py-4">
      <div className="flex items-start gap-2.5">
        <AlertCircle className="mt-0.5 h-4 w-4 shrink-0 text-dls-danger-text" aria-hidden="true" />
        <div>
          <p className="text-[13.5px] font-bold text-dls-danger-text">{title}</p>
          <p className="mt-0.5 text-[13.5px] text-dls-text-secondary">{message}</p>
        </div>
      </div>
      <div className="flex items-center gap-3 pl-6">
        {onRetry && (
          <Button variant="secondary" onClick={onRetry}>
            Try again
          </Button>
        )}
        {correlationId && <span className="font-mono text-xs text-dls-text-muted">Reference {correlationId}</span>}
      </div>
    </div>
  );
}

export function NotConfiguredStateSurface({ what }: { what: string }) {
  return (
    <div className="flex flex-col items-start gap-2 px-5 py-4">
      <StatusPill tone="neutral" label="Not configured" />
      <p className="text-[13.5px] text-dls-text-secondary">{what}</p>
    </div>
  );
}

export function NoPermissionStateSurface({ permission, contact }: { permission?: string; contact?: string }) {
  return (
    <div className="flex items-start gap-3 px-5 py-6">
      <ShieldAlert className="mt-0.5 h-5 w-5 shrink-0 text-dls-text-muted" aria-hidden="true" />
      <div>
        <p className="text-[13.5px] font-bold text-dls-text-primary">Access restricted</p>
        <p className="mt-1 max-w-md text-[13.5px] text-dls-text-secondary">
          This view is hidden because your account does not have the required permission
          {permission ? ` (${permission})` : ""}.{" "}
          {contact ? `Contact ${contact} for access.` : "Contact a workspace administrator for access."}
        </p>
      </div>
    </div>
  );
}

export function StateSurfaceShell({ children }: { children: ReactNode }) {
  return <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface shadow-dls-none">{children}</div>;
}
