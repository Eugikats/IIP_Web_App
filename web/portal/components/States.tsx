import { AlertCircle, Inbox, Loader2, ShieldAlert } from "lucide-react";

export function EmptyState({ title, description }: { title: string; description: string }) {
  return (
    <div className="flex flex-col items-center justify-center rounded-3xl border border-dashed border-line bg-mist/60 px-6 py-10 text-center">
      <div className="mb-4 flex h-12 w-12 items-center justify-center rounded-2xl border border-violet/20 bg-violet/10">
        <Inbox className="h-5 w-5 text-violet" />
      </div>
      <p className="text-sm font-semibold text-ink">{title}</p>
      <p className="mt-1.5 max-w-xs text-xs leading-relaxed text-muted">{description}</p>
    </div>
  );
}

export function LoadingState({ label = "Loading..." }: { label?: string }) {
  return (
    <div className="flex items-center gap-3 rounded-3xl border border-line bg-mist/60 px-5 py-4">
      <Loader2 className="h-4 w-4 flex-shrink-0 animate-spin text-violet" />
      <p className="text-sm font-medium text-muted">{label}</p>
    </div>
  );
}

export function ErrorState({ message }: { message: string }) {
  return (
    <div className="flex items-start gap-3 rounded-3xl border border-danger/30 bg-danger/[0.06] px-5 py-4 shadow-glow-danger">
      <AlertCircle className="mt-0.5 h-4 w-4 flex-shrink-0 text-danger" />
      <p className="text-sm font-medium text-danger">{message}</p>
    </div>
  );
}

export function UnauthorizedState({ permission }: { permission?: string }) {
  return (
    <div className="rounded-3xl border border-warning/30 bg-warning/[0.08] px-6 py-8 shadow-glow-warning">
      <div className="flex items-start gap-4">
        <div className="flex h-11 w-11 flex-shrink-0 items-center justify-center rounded-2xl border border-warning/30 bg-warning/10">
          <ShieldAlert className="h-5 w-5 text-warning" />
        </div>
        <div>
          <p className="text-sm font-semibold text-ink">Access not available</p>
          <p className="mt-2 max-w-xl text-sm leading-6 text-muted">
            This workspace view is hidden unless the backend runtime context grants the required permission
            {permission ? ` (${permission})` : ""}. Direct links do not bypass server-side authorization.
          </p>
        </div>
      </div>
    </div>
  );
}
