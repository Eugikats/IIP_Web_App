"use client";
import { useState } from "react";
import { X } from "lucide-react";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { agents, killSwitchLevels, type AgentDef } from "@/lib/lovable-parity/mock";
import { toast } from "sonner";

function StatusPill({ s }: { s: AgentDef["status"] }) {
  const tone = s === "Active" ? "success" : s === "Testing" ? "info" : s === "Disabled" ? "warning" : "neutral";
  return <Badge tone={tone as any}>{s}</Badge>;
}
function RiskPill({ r }: { r: AgentDef["risk"] }) {
  const tone = r === "High" ? "error" : r === "Medium" ? "warning" : "success";
  return <Badge tone={tone as any}>{r}</Badge>;
}

export default function Agents() {
  const [open, setOpen] = useState<AgentDef | null>(null);
  return (
    <>
      <SectionHeader
        eyebrow="Admin · AI & Agent Management"
        title="Agent registry"
        sub="Every agent is versioned, permissioned, and auditable. Human approval remains required for regulated actions."
      />
      <div className="mb-4"><GuardrailBanner text="Disabled agents cannot be invoked. Deprecated agents remain visible for audit history. Sensitive prompt changes require secondary approval." /></div>

      <GlassCard className="!p-0 overflow-hidden">
        <table className="w-full text-[13px]">
          <thead className="bg-white/60">
            <tr className="text-left text-[11px] uppercase tracking-[0.14em] text-stone">
              <th className="p-3">Agent</th><th className="p-3">Purpose</th><th className="p-3">Status</th><th className="p-3">Version</th><th className="p-3">Risk</th><th className="p-3">Human approval</th><th className="p-3">Runs today</th><th className="p-3">Owner</th><th className="p-3"></th>
            </tr>
          </thead>
          <tbody>
            {agents.map((a) => (
              <tr key={a.id} className="border-t border-cloud/70 hover:bg-white/50 cursor-pointer" onClick={() => setOpen(a)}>
                <td className="p-3"><div className="text-navy font-medium">{a.name}</div><div className="text-[10.5px] text-stone">{a.id}</div></td>
                <td className="p-3 text-slate2 max-w-[280px]">{a.purpose}</td>
                <td className="p-3"><StatusPill s={a.status} /></td>
                <td className="p-3 text-slate2">{a.version}</td>
                <td className="p-3"><RiskPill r={a.risk} /></td>
                <td className="p-3 text-slate2">{a.humanApproval}</td>
                <td className="p-3 text-navy">{a.runsToday}</td>
                <td className="p-3 text-slate2">{a.owner}</td>
                <td className="p-3 text-info text-[12px]">Open →</td>
              </tr>
            ))}
          </tbody>
        </table>
      </GlassCard>

      <div className="grid md:grid-cols-2 gap-4 mt-6">
        <GlassCard>
          <div className="text-[14px] font-semibold text-navy mb-2">Kill switch</div>
          <div className="text-[12.5px] text-stone mb-3">Layered controls to safely halt agent activity across scopes.</div>
          <div className="space-y-1.5">
            {killSwitchLevels.map((k) => (
              <div key={k} className="flex items-center justify-between rounded-xl border border-cloud bg-white/70 px-3 py-2 text-[13px]">
                <span className="text-slate2">{k}</span>
                <button onClick={() => toast(`Kill switch armed: ${k}`, { description: "Requires secondary approval to execute." })} className="text-[11.5px] rounded-lg px-2 py-1 border border-cloud text-navy hover:bg-white">Arm</button>
              </div>
            ))}
          </div>
        </GlassCard>

        <GlassCard>
          <div className="text-[14px] font-semibold text-navy mb-2">Human feedback loop</div>
          <div className="text-[12.5px] text-stone mb-3">Reviewers rate every AI run. Feedback does not auto-retrain production agents — improvements go through testing and approval.</div>
          <div className="flex flex-wrap gap-1.5">
            {["Accurate","Partially accurate","Incorrect","Unsupported by evidence","Missing evidence","Unsafe wording","Wrong policy reference","Too vague","Too detailed","Needs escalation","Useful","Not useful"].map((f) => (
              <Badge key={f} tone="neutral">{f}</Badge>
            ))}
          </div>
        </GlassCard>
      </div>

      {open && (
        <div className="fixed inset-0 bg-navy/30 backdrop-blur-sm z-40 flex justify-end animate-fade-in" onClick={() => setOpen(null)}>
          <div onClick={(e) => e.stopPropagation()} className="w-full max-w-lg h-full bg-white/95 backdrop-blur-xl overflow-y-auto p-6 animate-slide-in-right">
            <div className="flex items-start justify-between">
              <div>
                <div className="text-[11px] uppercase tracking-[0.14em] text-stone">{open.id} · {open.version}</div>
                <div className="text-[18px] font-semibold text-navy">{open.name}</div>
                <div className="text-[13px] text-slate2 mt-1">{open.purpose}</div>
              </div>
              <button onClick={() => setOpen(null)} className="text-stone hover:text-navy"><X className="h-4 w-4" /></button>
            </div>
            <div className="mt-4 flex flex-wrap gap-2"><StatusPill s={open.status} /><RiskPill r={open.risk} /><Badge tone="info">Owner · {open.owner}</Badge></div>
            <div className="mt-5 space-y-3 text-[12.5px]">
              {[
                ["Model", open.model], ["Prompt version", open.promptVersion], ["Confidence threshold", String(open.confidenceThreshold)],
                ["Human approval rule", open.humanApproval], ["Allowed modules", open.modules.join(", ")], ["Allowed tools", open.tools.join(", ")],
                ["Allowed knowledge", open.knowledge.join(", ")], ["Last run", open.lastRun], ["Runs today", String(open.runsToday)],
                ["Estimated cost (today)", open.costEstimate], ["Last updated", open.lastUpdated],
              ].map(([k, v]) => (
                <div key={k} className="flex justify-between gap-4"><span className="text-stone">{k}</span><span className="text-navy text-right">{v}</span></div>
              ))}
            </div>
            <div className="mt-6 grid grid-cols-2 gap-2">
              <button onClick={() => toast("Test run queued in playground")} className="rounded-xl border border-cloud bg-white px-3 py-2 text-[12.5px] text-navy">Test in playground</button>
              <button onClick={() => toast("Governance change requested", { description: "Sensitive prompt change requires secondary approval." })} className="rounded-xl border border-cloud bg-white px-3 py-2 text-[12.5px] text-navy">Request prompt change</button>
              <button onClick={() => toast(`${open.name} disabled`)} className="rounded-xl border border-cloud bg-white px-3 py-2 text-[12.5px] text-warning">Disable in this workspace</button>
              <button onClick={() => toast(`${open.name} disabled globally`)} className="rounded-xl border border-cloud bg-white px-3 py-2 text-[12.5px] text-error">Disable everywhere</button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
