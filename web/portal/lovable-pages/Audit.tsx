"use client";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import { auditEvents } from "@/lib/lovable-parity/mock";
import { Download, Filter, Sparkles, User, Cog } from "lucide-react";
import { toast } from "sonner";

const icon = { "AI Copilot": Sparkles, "System": Cog } as Record<string, any>;

const TYPE_TONE: Record<string, "info" | "violet" | "warning" | "success" | "neutral" | "error"> = {
  claim: "info", document: "info", approval: "success", customer: "info",
  communication: "violet", integration: "warning", report: "neutral",
  knowledge_document: "success", agent_policy: "violet",
};

export default function Audit() {
  return (
    <>
      <SectionHeader
        eyebrow="Reports & Audit"
        title="Audit trail"
        sub="Every AI action, human decision, integration event, and communication — evidence-linked and regulator-ready."
        action={
          <button onClick={() => toast.success("CSV export prepared")} className="inline-flex items-center gap-2 rounded-2xl gradient-primary text-white text-[13px] font-medium px-4 py-2.5 shadow-l2">
            <Download className="h-4 w-4" /> Export CSV
          </button>
        }
      />

      <div className="glass-subtle p-3 mb-4 flex flex-wrap gap-2">
        {["All events", "AI actions", "Human actions", "Human overrides", "Approvals", "Communications", "Integrations", "Knowledge", "Agent policy"].map(f => (
          <button key={f} className={`text-[12px] rounded-full px-3 py-1.5 border transition ${f === "All events" ? "border-info/30 bg-[hsl(var(--info)/0.08)] text-info" : "border-cloud bg-white/70 text-slate2 hover:bg-white"}`}>{f}</button>
        ))}
        <button className="ml-auto inline-flex items-center gap-1.5 text-[12px] text-slate2 rounded-xl border border-cloud bg-white/70 px-3 py-1.5"><Filter className="h-3.5 w-3.5" /> Date · severity · user · correlation ID</button>
      </div>

      <GlassCard className="p-0 overflow-hidden">
        <div className="grid grid-cols-12 text-[11px] uppercase tracking-[0.12em] text-stone px-4 py-3 border-b border-cloud">
          <div className="col-span-1">Time</div>
          <div className="col-span-2">Actor</div>
          <div className="col-span-2">Action</div>
          <div className="col-span-3">Target</div>
          <div className="col-span-1">Type</div>
          <div className="col-span-1">Evidence</div>
          <div className="col-span-1">Run</div>
          <div className="col-span-1">Correlation</div>
        </div>
        {auditEvents.map((e, i) => {
          const Icon = icon[e.user] ?? User;
          const targetType = (e as any).targetType ?? "other";
          const tone = TYPE_TONE[targetType] ?? "neutral";
          const knownType = TYPE_TONE[targetType] !== undefined;
          return (
            <div key={i} className="grid grid-cols-12 items-center px-4 py-3 border-b border-cloud/60 hover:bg-white/70 text-[13px]">
              <div className="col-span-1 text-stone">{e.time}</div>
              <div className="col-span-2 inline-flex items-center gap-2 text-navy"><Icon className="h-3.5 w-3.5 text-info" />{e.user}</div>
              <div className="col-span-2 text-slate2">{e.action}</div>
              <div className="col-span-3 text-slate2 truncate">{e.target}</div>
              <div className="col-span-1"><Badge tone={tone}>{knownType ? targetType : "Other system event"}</Badge></div>
              <div className="col-span-1 text-[11.5px] text-stone">{e.evidence}</div>
              <div className="col-span-1 text-[11.5px] text-stone">{e.run}</div>
              <div className="col-span-1 text-[11.5px] text-stone truncate">{(e as any).corr ?? "—"}</div>
            </div>
          );
        })}
      </GlassCard>
      <div className="mt-3 text-[11.5px] text-stone">Model / prompt metadata is recorded per AI run (e.g. <span className="text-slate2">gpt-4o · prompt v3.2</span>). Logs never contain secrets or unnecessary PII.</div>
    </>
  );
}
