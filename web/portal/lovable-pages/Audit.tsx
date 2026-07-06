"use client";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import { Download, Sparkles, User, Cog, FileClock } from "lucide-react";
import { toast } from "sonner";
import { useAuditEvents, toCsv } from "@/lib/lovable-parity/data/audit";

const icon = { "AI Copilot": Sparkles, "System": Cog } as Record<string, any>;

const TYPE_TONE: Record<string, "info" | "violet" | "warning" | "success" | "neutral" | "error"> = {
  claim: "info", document: "info", approval: "success", customer: "info",
  communication: "violet", integration: "warning", report: "neutral",
  knowledge_document: "success", agent_policy: "violet",
};

export default function Audit() {
  const { rows, loading, error } = useAuditEvents();

  function exportCsv() {
    if (rows.length === 0) {
      toast("Nothing to export");
      return;
    }
    const blob = new Blob([toCsv(rows)], { type: "text/csv;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `iip-audit-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
    toast.success("Audit CSV exported");
  }

  return (
    <>
      <SectionHeader
        eyebrow="Reports & Audit"
        title="Audit trail"
        sub="Every AI action, human decision, integration event, and communication — evidence-linked and regulator-ready."
        action={
          <button onClick={exportCsv} className="inline-flex items-center gap-2 rounded-2xl gradient-primary text-white text-[13px] font-medium px-4 py-2.5 shadow-l2">
            <Download className="h-4 w-4" /> Export CSV
          </button>
        }
      />

      {loading ? (
        <GlassCard><div className="p-6 text-center text-[13px] text-stone">Loading audit trail…</div></GlassCard>
      ) : rows.length === 0 ? (
        <GlassCard>
          <div className="p-10 text-center flex flex-col items-center gap-2">
            <FileClock className="h-6 w-6 text-stone" />
            <div className="text-navy font-medium">No audit events</div>
            <div className="text-[12.5px] text-stone max-w-md">
              {error ? error : "Audit events appear here as AI actions, human decisions, and integration events are recorded for this workspace."}
            </div>
          </div>
        </GlassCard>
      ) : (
        <GlassCard className="p-0 overflow-hidden">
          <div className="grid grid-cols-12 text-[11px] uppercase tracking-[0.12em] text-stone px-4 py-3 border-b border-cloud">
            <div className="col-span-2">Time</div>
            <div className="col-span-2">Actor</div>
            <div className="col-span-3">Action</div>
            <div className="col-span-3">Target</div>
            <div className="col-span-1">Type</div>
            <div className="col-span-1">Correlation</div>
          </div>
          {rows.map((e) => {
            const Icon = icon[e.actor] ?? User;
            const knownType = TYPE_TONE[e.targetType] !== undefined;
            const tone = TYPE_TONE[e.targetType] ?? "neutral";
            return (
              <div key={e.id} className="grid grid-cols-12 items-center px-4 py-3 border-b border-cloud/60 hover:bg-white/70 text-[13px]">
                <div className="col-span-2 text-stone">{e.time}</div>
                <div className="col-span-2 inline-flex items-center gap-2 text-navy"><Icon className="h-3.5 w-3.5 text-info" />{e.actor}</div>
                <div className="col-span-3 text-slate2 truncate">{e.action}</div>
                <div className="col-span-3 text-slate2 truncate">{e.target}</div>
                <div className="col-span-1"><Badge tone={tone}>{knownType ? e.targetType : "Other"}</Badge></div>
                <div className="col-span-1 text-[11.5px] text-stone truncate">{e.correlation}</div>
              </div>
            );
          })}
        </GlassCard>
      )}
      <div className="mt-3 text-[11.5px] text-stone">Model / prompt metadata is recorded per AI run. Logs never contain secrets or unnecessary PII.</div>
    </>
  );
}
