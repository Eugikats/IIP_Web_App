"use client";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import { BarChart, Bar, ResponsiveContainer, XAxis, YAxis, Tooltip, CartesianGrid } from "recharts";
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
      ["SLA breached", summary.slaBreached],
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
        action={<button onClick={exportCsv} className="inline-flex items-center gap-2 rounded-2xl gradient-primary text-white text-[13px] px-4 py-2 shadow-l2 hover-lift"><Download className="h-4 w-4" /> Export CSV</button>}
      />

      {loading ? (
        <GlassCard><div className="p-6 text-center text-[13px] text-muted">Loading operations summary…</div></GlassCard>
      ) : !summary ? (
        <GlassCard><div className="p-10 text-center text-muted"><div className="text-ink font-medium">Operations summary unavailable</div><div className="text-[12.5px] mt-1">{error ?? "No operations data for this workspace yet."}</div></div></GlassCard>
      ) : (
        <>
          <DataProvenance source={summary.source} dataAsOf={summary.dataAsOf} snapshotId={summary.snapshotId} limitations={summary.limitations} className="mb-4" />
          <div className="grid md:grid-cols-4 gap-3 mb-6">
            {[
              { l: "Open claims", v: summary.openClaims },
              { l: "Doc completeness", v: `${Math.round(summary.averageCompletenessScore)}%` },
              { l: "AI assist volume", v: summary.assistantQuestionsAnswered.toLocaleString() },
              { l: "SLA at risk", v: summary.slaAtRisk },
            ].map(k => (
              <div key={k.l} className="glass-subtle p-4">
                <div className="text-[11.5px] text-muted">{k.l}</div>
                <div className="mt-1 text-[26px] leading-none font-semibold text-ink">{k.v}</div>
              </div>
            ))}
          </div>

          <div className="glass-elevated p-5 mb-6">
            <div className="flex items-center gap-2 mb-2">
              <div className="h-8 w-8 rounded-xl gradient-primary grid place-items-center shadow-l2"><Sparkles className="h-4 w-4 text-white" /></div>
              <div>
                <div className="text-[11px] uppercase tracking-[0.14em] text-muted">AI executive brief · prepared for human review</div>
                <div className="text-[14px] font-semibold text-ink">Motor Operations</div>
              </div>
            </div>
            {parityMode ? (
              <p className="text-[13.5px] leading-[21px] text-ink-soft">This synthetic snapshot contains {summary.openClaims} open claims, including {summary.slaAtRisk} at risk and {summary.slaBreached} breached. It demonstrates the management layout only; it does not establish trends, provider performance, financial outcomes, or regulatory status.</p>
            ) : (
              <p className="text-[12.5px] leading-[20px] text-ink-soft">A source-cited executive narrative appears here once the summary-generation service is configured. Until then, this view shows only the timestamped operational aggregates returned by the reports service.</p>
            )}
          </div>

          <GlassCard>
            <div className="text-[14px] font-semibold text-ink mb-1">Claims by status</div>
            <div className="text-[11.5px] text-muted mb-4">{summary.source === "synthetic" ? "Illustrative distribution in the labelled development snapshot" : "Distribution at the snapshot time shown above"}</div>
            <div className="h-64">
              <ResponsiveContainer>
                <BarChart data={summary.claimsByStatus}>
                  <CartesianGrid stroke="#EEF2F7" vertical={false} />
                  <XAxis dataKey="label" tick={{ fill: "#6B7280", fontSize: 10 }} axisLine={false} tickLine={false} interval={0} angle={-12} textAnchor="end" height={50} />
                  <YAxis tick={{ fill: "#6B7280", fontSize: 11 }} axisLine={false} tickLine={false} allowDecimals={false} />
                  <Tooltip contentStyle={{ borderRadius: 12, border: "1px solid #E5E7EB" }} />
                  <Bar dataKey="count" fill="#2563FF" radius={[8, 8, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
            <div className="mt-3 flex flex-wrap gap-2">
              <Badge tone="info">{summary.pendingApprovals} approvals pending</Badge>
              <Badge tone="warning">{summary.escalationsRequired} escalations</Badge>
              <Badge tone={summary.slaBreached > 0 ? "error" : "success"}>{summary.slaBreached} SLA breached</Badge>
            </div>
          </GlassCard>
        </>
      )}
    </>
  );
}
