"use client";
import { GlassCard, SectionHeader, Badge } from "@/components/iip/Primitives";
import { LoadingSkeleton, ErrorStateSurface } from "@/components/ui/StateSurfaces";
import { useBilling, type UsageMeterView } from "@/lib/lovable-parity/data/billing";
import { BillingPlans } from "@/components/iip/BillingPlans";

/** Cost is counted in whole US cents; show it as dollars. Everything else is shown as the number it is. */
function amount(m: UsageMeterView): string {
  return m.unit === "cents" ? `US$ ${(m.used / 100).toFixed(2)}` : `${m.used.toLocaleString()} ${m.unit}`;
}

function unmeteredReason(m: UsageMeterView): string {
  return m.key === "ai-cost-usd"
    ? "No price is set for the AI model in use, so a cost cannot be shown."
    : "No counter wired for this dimension yet.";
}

function Meter({ m }: { m: UsageMeterView }) {
  if (!m.metered) {
    return (
      <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4">
        <div className="text-[11px] font-bold uppercase tracking-[0.14em] text-dls-text-muted">{m.label}</div>
        <div className="mt-2"><Badge tone="warning">Not configured</Badge></div>
        <div className="mt-2 text-[11px] text-dls-text-muted">{unmeteredReason(m)}</div>
      </div>
    );
  }
  if (m.limit <= 0) {
    return (
      <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4" data-testid={`meter-${m.key}`}>
        <div className="mb-2 text-[11px] font-bold uppercase tracking-[0.14em] text-dls-text-muted">{m.label}</div>
        <div className="text-[20px] font-bold text-dls-text-primary">{amount(m)}</div>
        <div className="mt-2 text-[11px] text-dls-text-muted">No allowance is set, so this is only counted.</div>
      </div>
    );
  }
  const tone = m.overLimit ? "error" : m.percentUsed >= 85 ? "warning" : "info";
  return (
    <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4">
      <div className="mb-2 flex items-center justify-between">
        <div className="text-[11px] font-bold uppercase tracking-[0.14em] text-dls-text-muted">{m.label}</div>
        <Badge tone={tone as any}>{m.percentUsed}%</Badge>
      </div>
      <div className="text-[20px] font-bold text-dls-text-primary">{m.used.toLocaleString()} <span className="text-[12px] font-normal text-dls-text-muted">/ {m.limit.toLocaleString()} {m.unit}</span></div>
      {/* Flat fill, not a gradient, §4.5 restricts gradients to the orb and chart bars. */}
      <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-dls-bg-page"><div className="h-full bg-dls-primary" style={{ width: `${m.percentUsed}%` }} /></div>
    </div>
  );
}

export default function Billing() {
  const { summary, loading, error, reload } = useBilling();

  return (
    <>
      <SectionHeader
        eyebrow="Admin · Billing & Usage"
        title="Package & entitlements"
        sub="Your plan, what this month has used, and how to pay for a bigger plan."
      />

      {loading ? (
        <GlassCard><LoadingSkeleton rows={4} /></GlassCard>
      ) : error ? (
        <GlassCard><ErrorStateSurface title="Billing could not be loaded" message={error} /></GlassCard>
      ) : !summary ? (
        <GlassCard><ErrorStateSurface title="Billing unavailable" message="No billing data for this workspace." /></GlassCard>
      ) : (
        <>
          <BillingPlans
            onChanged={() => void reload()}
            usage={
              <div className="mb-6 grid gap-3 md:grid-cols-2 lg:grid-cols-3">
                {summary.meters.map((m) => <Meter key={m.key} m={m} />)}
              </div>
            }
          />

          <GlassCard>
            <div className="mb-3 text-[14px] font-bold text-dls-text-primary">Module entitlements</div>
            <div className="grid gap-2 text-[13px] sm:grid-cols-2 md:grid-cols-3">
              {summary.modules.map((m) => (
                <div key={m} className="flex items-center justify-between rounded-dls-card border border-dls-border-subtle bg-dls-surface px-3 py-2">
                  <span className="text-dls-text-secondary">{m}</span>
                  <Badge tone="success">Enabled</Badge>
                </div>
              ))}
            </div>
          </GlassCard>
        </>
      )}
    </>
  );
}
