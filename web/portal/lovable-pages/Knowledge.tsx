"use client";
import { useMemo, useState } from "react";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import { useKnowledgeDocs, type KnowledgeSensitivity } from "@/lib/lovable-parity/data/knowledge";
import { BookOpen, Search } from "lucide-react";
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
  const { docs, loading, error } = useKnowledgeDocs();
  const [q, setQ] = useState("");
  const rows = useMemo(() => docs.filter((d) => !q || d.title.toLowerCase().includes(q.toLowerCase())), [docs, q]);
  return (
    <>
      <SectionHeader
        eyebrow="Knowledge Hub"
        title="Approved sources for grounded AI answers"
        sub="Every knowledge source is classified, versioned, and cited. Coverage answers must be grounded in approved sources — model memory alone is not acceptable."
      />

      <div className="mb-4"><GuardrailBanner text="Unknown sensitivity is blocked from retrieval by default. Upload and approval controls remain hidden until the API captures an explicit sensitivity label and governance reason." /></div>

      <div className="glass-elevated p-4 mb-4 flex items-center gap-3">
        <Search className="h-4 w-4 text-stone" />
        <input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Search knowledge titles…" className="flex-1 bg-transparent outline-none text-[14px] placeholder:text-stone text-navy" />
      </div>

      {loading ? (
        <GlassCard><div className="p-6 text-center text-[13px] text-stone">Loading knowledge sources…</div></GlassCard>
      ) : rows.length === 0 ? (
        <GlassCard><div className="p-10 text-center text-stone"><div className="text-navy font-medium">No knowledge documents</div><div className="text-[12.5px] mt-1">{error ? error : "Approved policy wordings, SOPs, and playbooks appear here once uploaded and indexed."}</div></div></GlassCard>
      ) : (
        <GlassCard className="!p-0 overflow-hidden">
          <table className="w-full text-[13px]">
            <thead className="bg-white/60">
              <tr className="text-left text-[11px] uppercase tracking-[0.14em] text-stone">
                <th className="p-3">Document</th><th className="p-3">Category</th><th className="p-3">Sensitivity</th><th className="p-3">Status</th><th className="p-3">Retrieval</th><th className="p-3">Updated</th><th className="p-3">Owner</th>
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
                  <td className="p-3 text-stone">{d.updated}</td>
                  <td className="p-3 text-slate2">{d.owner}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </GlassCard>
      )}
      <div className="mt-3 text-[11.5px] text-stone">Knowledge lifecycle actions will return when sensitivity classification is part of the persisted API contract; the portal no longer reports unexecuted upload, approval, or re-index actions as successful.</div>
    </>
  );
}
