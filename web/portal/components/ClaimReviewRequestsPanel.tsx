"use client";

import { Loader2 } from "lucide-react";
import { useState } from "react";
import { StatusBadge } from "@/components/iip/Primitives";
import type { ReviewRequestView } from "@/lib/lovable-parity/data/claimReview";

type Props = {
  requests: ReviewRequestView[];
  busyId: string | null;
  onAcknowledge: (requestId: string) => void;
  onResolve: (requestId: string, resolutionNote: string) => void;
};

function formatWhen(value: string | null): string {
  if (!value) return "—";
  const parsed = Date.parse(value);
  if (Number.isNaN(parsed)) return value;
  return new Date(parsed).toLocaleString();
}

export function ClaimReviewRequestsPanel({
  requests,
  busyId,
  onAcknowledge,
  onResolve,
}: Props) {
  const [resolveNotes, setResolveNotes] = useState<Record<string, string>>({});

  if (requests.length === 0) {
    return (
      <div className="rounded-xl border border-line bg-white/70 p-4 text-[12.5px] text-muted">
        No re-review requests for this claim.
      </div>
    );
  }

  return (
    <div className="space-y-3">
      {requests.map((request) => {
        const note = resolveNotes[request.requestId] ?? "";
        const canAcknowledge = request.status === "Open";
        const canResolve = request.status === "Open" || request.status === "Acknowledged";
        const busy = busyId === request.requestId;

        return (
          <div
            key={request.requestId}
            className="rounded-xl border border-line bg-white/80 p-3.5"
          >
            <div className="mb-2 flex flex-wrap items-center gap-2">
              <StatusBadge status={request.status} />
              <span className="text-[11px] uppercase tracking-[0.08em] text-muted">
                {request.origin}
              </span>
              <span className="text-[11px] text-muted">{formatWhen(request.requestedAt)}</span>
            </div>
            <p className="text-[13px] leading-snug text-ink">{request.reason}</p>
            <div className="mt-1.5 text-[11.5px] text-muted">
              Requested by {request.requestedByUserId}
              {request.resolvedByUserId
                ? ` · Resolved by ${request.resolvedByUserId}`
                : null}
            </div>
            {request.resolutionNote ? (
              <div className="mt-2 rounded-lg border border-line bg-[hsl(var(--surface)/0.6)] px-3 py-2 text-[12px] text-ink-soft">
                Resolution note: {request.resolutionNote}
              </div>
            ) : null}

            {canResolve ? (
              <div className="mt-3 space-y-2">
                <label className="block text-[11.5px] font-medium text-ink">
                  Resolution note
                  <textarea
                    value={note}
                    onChange={(event) =>
                      setResolveNotes((current) => ({
                        ...current,
                        [request.requestId]: event.target.value,
                      }))
                    }
                    placeholder="Record what was reviewed and how the re-review request was closed. This does not settle, deny, or pay the claim."
                    className="mt-1.5 min-h-16 w-full rounded-xl border border-line bg-white px-3 py-2.5 text-[12px] font-normal text-ink placeholder:text-muted focus-ring"
                    disabled={busy}
                  />
                </label>
                <div className="flex flex-wrap gap-2">
                  {canAcknowledge ? (
                    <button
                      type="button"
                      onClick={() => onAcknowledge(request.requestId)}
                      disabled={busy}
                      className="inline-flex items-center gap-2 rounded-2xl border border-line bg-white px-3.5 py-2 text-[12.5px] font-medium text-ink hover-lift disabled:opacity-50"
                    >
                      {busy ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : null}
                      Acknowledge
                    </button>
                  ) : null}
                  <button
                    type="button"
                    onClick={() => onResolve(request.requestId, note.trim())}
                    disabled={busy || note.trim().length < 3}
                    className="inline-flex items-center gap-2 rounded-2xl border border-line bg-white px-3.5 py-2 text-[12.5px] font-medium text-ink hover-lift disabled:opacity-50"
                  >
                    {busy ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : null}
                    Resolve re-review
                  </button>
                </div>
              </div>
            ) : null}
          </div>
        );
      })}
    </div>
  );
}
