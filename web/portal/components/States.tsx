// Migrated onto design language v2 §8.24 (six states, none a spinner alone). External API of the
// four existing exports is unchanged; ErrorState gains two optional, additive props
// (onRetry/correlationId) and two new exports close the six-state set this file was missing:
// EmptyFilteredState and NotConfiguredState.
import { Inbox, Loader2, AlertCircle, ShieldAlert } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { StatusPill } from "@/components/ui/StatusPill";

export function EmptyState({ title, description }: { title: string; description: string }) {
  return (
    <div className="flex flex-col items-center justify-center rounded-dls-card border border-dashed border-dls-border-input bg-dls-bg-page px-6 py-10 text-center">
      <div className="mb-4 flex h-11 w-11 items-center justify-center rounded-dls-chip bg-dls-primary-soft">
        <Inbox className="h-4 w-4 text-dls-primary" aria-hidden="true" />
      </div>
      <p className="text-[13.5px] font-bold text-dls-text-primary">{title}</p>
      <p className="mt-1.5 max-w-xs text-[13.5px] leading-relaxed text-dls-text-secondary">{description}</p>
    </div>
  );
}

// Distinct from EmptyState — never show the same message for both (§8.24).
export function EmptyFilteredState({ onClear }: { onClear: () => void }) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 rounded-dls-card border border-dashed border-dls-border-input bg-dls-bg-page px-6 py-10 text-center">
      <p className="text-[13.5px] text-dls-text-secondary">No items match these filters.</p>
      <Button variant="ghost" onClick={onClear}>
        Clear filters
      </Button>
    </div>
  );
}

export function LoadingState({ label = "Loading…" }: { label?: string }) {
  return (
    <div className="flex items-center gap-3 rounded-dls-card border border-dls-border-subtle bg-dls-surface px-4 py-3.5" role="status">
      <Loader2 className="h-4 w-4 shrink-0 animate-spin text-dls-primary" aria-hidden="true" />
      <p className="text-[13.5px] font-medium text-dls-text-secondary">{label}</p>
    </div>
  );
}

export function ErrorState({
  message,
  onRetry,
  correlationId,
}: {
  message: string;
  onRetry?: () => void;
  correlationId?: string;
}) {
  return (
    <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface px-4 py-3.5">
      <div className="flex items-start gap-2.5">
        <AlertCircle className="mt-0.5 h-4 w-4 shrink-0 text-dls-danger-text" aria-hidden="true" />
        <p className="text-[13.5px] font-medium text-dls-danger-text">{message}</p>
      </div>
      {(onRetry || correlationId) && (
        <div className="mt-2.5 flex items-center gap-3 pl-[26px]">
          {onRetry && (
            <Button variant="secondary" onClick={onRetry}>
              Try again
            </Button>
          )}
          {correlationId && <span className="text-xs text-dls-text-muted">Reference: {correlationId}</span>}
        </div>
      )}
    </div>
  );
}

export function NotConfiguredState({ what }: { what: string }) {
  return (
    <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface px-4 py-3.5">
      <div className="flex flex-col items-start gap-2">
        <StatusPill tone="neutral" label="Not configured" />
        <p className="text-[13.5px] text-dls-text-secondary">{what}</p>
      </div>
    </div>
  );
}

export function UnauthorizedState({ permission }: { permission?: string }) {
  return (
    <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface px-5 py-6">
      <div className="flex items-start gap-3.5">
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-dls-chip bg-dls-bg-page">
          <ShieldAlert className="h-4 w-4 text-dls-text-secondary" aria-hidden="true" />
        </div>
        <div>
          <p className="text-[13.5px] font-bold text-dls-text-primary">Access not available</p>
          <p className="mt-2 max-w-xl text-[13.5px] leading-6 text-dls-text-secondary">
            This workspace view is hidden unless the backend runtime context grants the required permission
            {permission ? ` (${permission})` : ""}. Direct links do not bypass server-side authorization.
          </p>
        </div>
      </div>
    </div>
  );
}
