"use client";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { LoadingSkeleton, ErrorStateSurface } from "@/components/ui/StateSurfaces";
import { BarChart, Bar, ResponsiveContainer, XAxis, YAxis, Tooltip } from "recharts";
import { Download, Sparkles } from "lucide-react";
import { toast } from "sonner";
import { useOperationsSummary } from "@/lib/lovable-parity/data/reports";
import DataProvenance from "@/components/iip/DataProvenance";

export default function Executive() {
  const { summary, loading, error, parityMode } = useOperationsSummary();

  function exportCsv() {
    if (!summary) { toast("Nothing to export"); return; }
    const rows = [
      ["Source", summary.source],
      ["Data as of", summary.dataAsOf],
      ["Snapshot ID", summary.snapshotId],
      ["Limitations", summary.limitations],
      [],
      ["Metric", "Value"],
      ["Open claims", summary.openClaims],
      ["New claims today", summary.newClaimsToday],
      ["Incomplete claims", summary.incompleteClaims],
      ["Avg completeness %", summary.averageCompletenessScore],
      ["Pending approvals", summary.pendingApprovals],
      ["Customer updates pending", summary.customerUpdatesPending],
      ["SLA at risk", summary.slaAtRisk],
      ["SLA breached (open claims)", summary.slaBreached],
      ["Closed claims that breached SLA before closing", summary.closedClaimsWithSlaBreach],
      ["Escalations required", summary.escalationsRequired],
    ];
    const csv = rows.map((r) => r.map((v) => `"${String(v).replace(/"/g, '""')}"`).join(",")).join("\r\n");
    const url = URL.createObjectURL(new Blob([csv], { type: "text/csv;charset=utf-8" }));
    const a = document.createElement("a");
    a.href = url; a.download = `iip-operations-${new Date().toISOString().slice(0, 10)}.csv`; a.click();
    URL.revokeObjectURL(url);
    toast.success("Operations summary exported");
  }

  return (
    <>
      <SectionHeader
        eyebrow="Operations Intelligence"
        title="Management operations view"
        sub="Claims volume, SLA state, approvals, and document completeness from timestamped workspace aggregates or a labelled development snapshot. Unavailable financial or loss measures are omitted rather than estimated."
        action={
          <Button variant="primary" onClick={exportCsv} icon={<Download aria-hidden="true" />}>
            Export CSV
          </Button>
        }
      />

      {loading ? (
        <GlassCard><LoadingSkeleton rows={5} /></GlassCard>
      ) : error ? (
        <GlassCard><ErrorStateSurface title="Operations summary could not be loaded" message={error} /></GlassCard>
      ) : !summary ? (
        <GlassCard><ErrorStateSurface title="Operations summary unavailable" message="No operations data for this workspace yet." /></GlassCard>
      ) : (
        <>
          <DataProvenance source={summary.source} dataAsOf={summary.dataAsOf} snapshotId={summary.snapshotId} limitations={summary.limitations} className="mb-4" />
          <div className="mb-6 grid gap-3 md:grid-cols-4">
            {[
              { l: "Open claims", v: summary.openClaims },
              { l: "Doc completeness", v: `${Math.round(summary.averageCompletenessScore)}%` },
              { l: "AI assist volume", v: summary.assistantQuestionsAnswered.toLocaleString() },
              { l: "SLA at risk", v: summary.slaAtRisk },
            ].map((k) => (
              <div key={k.l} className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4">
                <div className="text-[13.5px] text-dls-text-secondary">{k.l}</div>
                <div className="mt-2 text-[30px] font-bold leading-[1.1] tabular-nums tracking-[-0.5px] text-dls-text-primary">{k.v}</div>
              </div>
            ))}
          </div>

          <GlassCard className="mb-6">
            <div className="mb-2 flex items-center gap-2">
              <div className="grid h-8 w-8 place-items-center rounded-dls-chip bg-dls-primary-soft text-dls-primary"><Sparkles className="h-4 w-4" aria-hidden="true" /></div>
              <div>
                <div className="text-[11px] font-bold uppercase tracking-[0.14em] text-dls-text-muted">AI executive brief · prepared for human review</div>
                <div className="text-[14px] font-bold text-dls-text-primary">Motor Operations</div>
              </div>
            </div>
            {parityMode ? (
              <p className="text-[13.5px] leading-[21px] text-dls-text-secondary">This synthetic snapshot contains {summary.openClaims} open claims, including {summary.slaAtRisk} at risk and {summary.slaBreached} breached. It demonstrates the management layout only; it does not establish trends, provider performance, financial outcomes, or regulatory status.</p>
            ) : (
              <p className="text-[12.5px] leading-[20px] text-dls-text-secondary">A source-cited executive narrative appears here once the summary-generation service is configured. Until then, this view shows only the timestamped operational aggregates returned by the reports service.</p>
            )}
          </GlassCard>

          <GlassCard>
            <div className="mb-1 text-[15px] font-bold text-dls-text-primary">Claims by status</div>
            <div className="mb-4 text-[11.5px] text-dls-text-muted">{summary.source === "synthetic" ? "Illustrative distribution in the labelled development snapshot" : "Distribution at the snapshot time shown above"}</div>
            {/* §8.25, vertical bars only, the chart-bar gradient (one of the two permitted uses),
                8px top radius, no gridlines/axis lines, muted short labels. */}
            <div className="h-[180px]">
              <ResponsiveContainer>
                <BarChart data={summary.claimsByStatus}>
                  <defs>
                    <linearGradient id="executive-claims-bar" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="0%" stopColor="#5B95FF" />
                      <stop offset="100%" stopColor="#1769FF" />
                    </linearGradient>
                  </defs>
                  <XAxis dataKey="label" tick={{ fill: "#6B7280", fontSize: 12, fontWeight: 600 }} axisLine={false} tickLine={false} interval={0} />
                  <YAxis hide />
                  <Tooltip
                    cursor={{ fill: "rgb(var(--color-bg-page))" }}
                    contentStyle={{ borderRadius: 12, border: "1px solid #ECEEF2", fontSize: 12 }}
                  />
                  <Bar dataKey="count" fill="url(#executive-claims-bar)" radius={[8, 8, 4, 4]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
            {/* Table alternative to the chart, per §8.25, "every chart is accompanied by an
                accessible table alternative and direct value labels". */}
            <table className="mt-4 w-full text-[12.5px]">
              <caption className="sr-only">Claims by status, exact counts</caption>
              <thead>
                <tr className="border-b border-dls-border-subtle text-left text-[11px] font-bold uppercase tracking-[0.04em] text-dls-text-muted">
                  <th scope="col" className="py-2">Status</th>
                  <th scope="col" className="py-2 text-right">Count</th>
                </tr>
              </thead>
              <tbody>
                {summary.claimsByStatus.map((row) => (
                  <tr key={row.label} className="border-b border-dls-border-subtle last:border-0">
                    <td className="py-2 text-dls-text-secondary">{row.label}</td>
                    <td className="py-2 text-right font-semibold tabular-nums text-dls-text-primary">{row.count}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            <div className="mt-4 flex flex-wrap gap-2">
              <Badge tone="info">{summary.pendingApprovals} approvals pending</Badge>
              <Badge tone="warning">{summary.escalationsRequired} escalations</Badge>
              <Badge tone={summary.slaBreached > 0 ? "error" : "success"}>{summary.slaBreached} SLA breached</Badge>
              {summary.closedClaimsWithSlaBreach > 0 && (
                <Badge tone="neutral">
                  {summary.closedClaimsWithSlaBreach} closed claim{summary.closedClaimsWithSlaBreach === 1 ? "" : "s"} breached SLA before closing
                </Badge>
              )}
            </div>
          </GlassCard>
        </>
      )}
    </>
  );
}
