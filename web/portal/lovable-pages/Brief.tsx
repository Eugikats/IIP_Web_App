"use client";

import { Link } from "@/components/iip/NextRouterCompat";
import { CheckCircle2 } from "lucide-react";
import { GlassCard, SectionHeader } from "@/components/iip/Primitives";
import { StatusPill, type PillTone } from "@/components/ui/StatusPill";
import { Button } from "@/components/ui/Button";
import { LoadingSkeleton, ErrorStateSurface, EmptyStateSurface, NoPermissionStateSurface } from "@/components/ui/StateSurfaces";
import { useRuntimeContext } from "@/lib/runtimeContext";
import { useBrief } from "@/lib/lovable-parity/data/brief";

const STATUS_TONE: Record<string, PillTone> = {
  New: "neutral",
  Assigned: "info",
  InReview: "info",
  MissingDocuments: "warning",
  ReadyForReview: "info",
  PendingApproval: "warning",
  Escalated: "danger",
  ReadyForExport: "success",
};

function timeOfDayGreeting(): string {
  const hour = new Date().getHours();
  if (hour < 12) return "Good morning";
  if (hour < 18) return "Good afternoon";
  return "Good evening";
}

export default function Brief() {
  const { runtimeContext } = useRuntimeContext();
  const { items, handledForYou, loading, error, noPermission, reload } = useBrief();
  const givenName = runtimeContext?.displayName?.trim().split(/\s+/)[0];

  return (
    <>
      <SectionHeader
        eyebrow="Your work"
        title={givenName ? `${timeOfDayGreeting()}, ${givenName}` : timeOfDayGreeting()}
        sub="What needs you, ranked. Ask a question any time from the bar below."
      />

      {loading && <GlassCard><LoadingSkeleton rows={4} /></GlassCard>}
      {!loading && error && <GlassCard><ErrorStateSurface title="Your brief could not be loaded" message={error} onRetry={reload} /></GlassCard>}
      {!loading && !error && noPermission && <GlassCard><NoPermissionStateSurface permission="View assigned or all claims" /></GlassCard>}

      {!loading && !error && !noPermission && (
        <div className="space-y-4">
          {items.length === 0 && handledForYou.length === 0 && (
            <GlassCard>
              <EmptyStateSurface title="You have nothing awaiting review" message="Claims assigned to you will appear here, ranked by what needs you most." />
            </GlassCard>
          )}

          {items.map((item) => (
            <GlassCard key={item.caseId}>
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="text-[14px] font-bold text-dls-text-primary">{item.customerName}</span>
                    <StatusPill tone={STATUS_TONE[item.status] ?? "neutral"} label={item.status} />
                  </div>
                  <div className="mt-0.5 text-[12px] text-dls-text-muted">{item.policyNumber} · {item.claimType}</div>
                  <p className="mt-2 text-[13px] text-dls-text-secondary">{item.reason}</p>
                </div>
                <Link to={item.primaryActionHref} className="dls-focus-outline shrink-0 rounded-dls-control">
                  <Button variant="primary">{item.primaryActionLabel}</Button>
                </Link>
              </div>
            </GlassCard>
          ))}

          {handledForYou.length > 0 && (
            <GlassCard>
              <div className="mb-3 flex items-center gap-2">
                <CheckCircle2 className="h-4 w-4 text-dls-success-dot" aria-hidden="true" />
                <div className="text-[14px] font-bold text-dls-text-primary">Handled for you</div>
              </div>
              <div className="space-y-2">
                {handledForYou.map((item, index) => (
                  <div key={`${item.caseId ?? "none"}-${index}`} className="flex items-center justify-between gap-3 text-[12.5px]">
                    <span className="text-dls-text-secondary">{item.summary}</span>
                    <span className="shrink-0 text-dls-text-muted">{new Date(item.handledAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}</span>
                  </div>
                ))}
              </div>
            </GlassCard>
          )}
        </div>
      )}
    </>
  );
}
