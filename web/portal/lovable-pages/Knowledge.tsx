"use client";
import { useState } from "react";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import { knowledgeDocs, type KnowledgeSensitivity } from "@/lib/lovable-parity/mock";
import { BookOpen, Search, RefreshCw, CheckCircle2, Upload, ShieldAlert } from "lucide-react";
import { toast } from "sonner";
import GuardrailBanner from "@/components/iip/GuardrailBanner";

const SENS_TONE: Record<KnowledgeSensitivity, "success" | "info" | "warning" | "error" | "violet" | "neutral"> = {
  approved: "success",
  customer_confidential: "violet",
  staff_only: "info",
  contains_secrets: "error",
  contains_unapproved_pii: "error",
  unknown: "warning",
};

export default function Knowledge() {
  const [q, setQ] = useState("");
  const rows = knowledgeDocs.filter((d) => !q || d.title.toLowerCase().includes(q.toLowerCase()));
  return (
    <>
      <SectionHeader
        eyebrow="Knowledge Hub"
        title="Approved sources for grounded AI answers"
        sub="Every knowledge source is classified, versioned, and cited. Coverage answers must be grounded in approved sources — model memory alone is not acceptable."
        action={<button onClick={() => toast.success("Upload dialog opened")} className="inline-flex items-center gap-2 rounded-2xl gradient-primary text-white text-[13px] px-4 py-2 shadow-l2 hover-lift"><Upload className="h-4 w-4" /> Upload document</button>}
      />

      <div className="mb-4"><GuardrailBanner text="Unknown sensitivity is blocked from retrieval by default. Secrets and unapproved PII are never surfaced to general agents." /></div>

      <div className="glass-elevated p-4 mb-4 flex items-center gap-3">
        <Search className="h-4 w-4 text-stone" />
        <input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Ask a policy or SOP question, or search titles…" className="flex-1 bg-transparent outline-none text-[14px] placeholder:text-stone text-navy" />
      </div>

      <GlassCard className="!p-0 overflow-hidden">
        <table className="w-full text-[13px]">
          <thead className="bg-white/60">
            <tr className="text-left text-[11px] uppercase tracking-[0.14em] text-stone">
              <th className="p-3">Document</th><th className="p-3">Category</th><th className="p-3">Sensitivity</th><th className="p-3">Status</th><th className="p-3">Retrieval</th><th className="p-3">Quality</th><th className="p-3">Updated</th><th className="p-3">Owner</th><th className="p-3"></th>
            </tr>
          </thead>
          <tbody>
            {rows.map((d) => (
              <tr key={d.id} className="border-t border-cloud/70 hover:bg-white/50">
                <td className="p-3"><div className="flex items-center gap-2 text-navy"><BookOpen className="h-4 w-4 text-info" />{d.title}</div><div className="text-[10.5px] text-stone">{d.id}</div></td>
                <td className="p-3 text-slate2">{d.category}</td>
                <td className="p-3"><Badge tone={SENS_TONE[d.sensitivity]}>{d.sensitivity}</Badge></td>
                <td className="p-3"><Badge tone={d.status === "Approved" ? "success" : d.status === "Pending" ? "warning" : "info"}>{d.status}</Badge></td>
                <td className="p-3">{d.retrievalEligible ? <Badge tone="success">Eligible</Badge> : <Badge tone="error">Blocked</Badge>}</td>
                <td className="p-3 text-slate2">{d.sourceQuality}</td>
                <td className="p-3 text-stone">{d.updated}</td>
                <td className="p-3 text-slate2">{d.owner}</td>
                <td className="p-3">
                  <div className="flex items-center gap-1">
                    {d.status !== "Approved" && (
                      <button onClick={() => toast.success("Approval request routed")} className="text-[11.5px] px-2 py-1 rounded-lg bg-white border border-cloud text-success inline-flex items-center gap-1"><CheckCircle2 className="h-3 w-3" />Approve</button>
                    )}
                    <button onClick={() => toast("Re-index queued")} className="text-[11.5px] px-2 py-1 rounded-lg bg-white border border-cloud text-navy inline-flex items-center gap-1"><RefreshCw className="h-3 w-3" />Re-index</button>
                    {d.sensitivity === "unknown" && (
                      <button onClick={() => toast("Sensitivity review requested")} className="text-[11.5px] px-2 py-1 rounded-lg bg-white border border-cloud text-warning inline-flex items-center gap-1"><ShieldAlert className="h-3 w-3" />Classify</button>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </GlassCard>
    </>
  );
}
