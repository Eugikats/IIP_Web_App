"use client";

import { Loader2 } from "lucide-react";
import { useState } from "react";
import { StatusBadge } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
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
      <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4 text-[12.5px] text-dls-text-muted">
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
            className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3.5"
          >
            <div className="mb-2 flex flex-wrap items-center gap-2">
              <StatusBadge status={request.status} />
              <span className="text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">
                {request.origin}
              </span>
              <span className="text-[11px] text-dls-text-muted">{formatWhen(request.requestedAt)}</span>
            </div>
            <p className="text-[13px] leading-snug text-dls-text-primary">{request.reason}</p>
            <div className="mt-1.5 text-[11.5px] text-dls-text-muted">
              Requested by {request.requestedByUserId}
              {request.resolvedByUserId
                ? ` · Resolved by ${request.resolvedByUserId}`
                : null}
            </div>
            {request.resolutionNote ? (
              <div className="mt-2 rounded-dls-control border border-dls-border-subtle bg-dls-bg-page px-3 py-2 text-[12px] text-dls-text-secondary">
                Resolution note: {request.resolutionNote}
              </div>
            ) : null}

            {canResolve ? (
              <div className="mt-3 space-y-2">
                <label className="block text-[11.5px] font-bold text-dls-text-primary">
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
                    className="mt-1.5 min-h-16 w-full rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-2.5 text-[12px] font-normal text-dls-text-primary placeholder:text-dls-text-muted focus:outline-none focus:border-dls-primary focus:ring-[3px] focus:ring-dls-primary/[0.12]"
                    disabled={busy}
                  />
                </label>
                <div className="flex flex-wrap gap-2">
                  {canAcknowledge ? (
                    <Button variant="secondary" icon={busy ? <Loader2 className="animate-spin" /> : undefined} disabled={busy} onClick={() => onAcknowledge(request.requestId)}>
                      Acknowledge
                    </Button>
                  ) : null}
                  <Button variant="secondary" icon={busy ? <Loader2 className="animate-spin" /> : undefined} disabled={busy || note.trim().length < 3} onClick={() => onResolve(request.requestId, note.trim())}>
                    Resolve re-review
                  </Button>
                </div>
              </div>
            ) : null}
          </div>
        );
      })}
    </div>
  );
}
