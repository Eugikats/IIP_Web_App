"use client";
import { useState, useMemo } from "react";
import { Link } from "@/components/iip/NextRouterCompat";
import { Filter, Search, ArrowRight, Users2 } from "lucide-react";
import { GlassCard, SectionHeader, Badge, StatusBadge, SLABadge, RiskBadge, ConfidenceBadge } from "@/components/iip/Primitives";
import { claims } from "@/lib/lovable-parity/mock";

const filters = ["All", "Human Review Required", "Missing Documents", "Escalated", "SLA at risk"];

export default function ClaimsQueue() {
  const [f, setF] = useState("All");
  const [q, setQ] = useState("");

  const rows = useMemo(() => claims.filter(c => {
    if (f === "SLA at risk" && c.sla === "Normal") return false;
    if (f !== "All" && f !== "SLA at risk" && c.status !== f) return false;
    if (q && !`${c.id} ${c.customer} ${c.policy}`.toLowerCase().includes(q.toLowerCase())) return false;
    return true;
  }), [f, q]);

  return (
    <>
      <SectionHeader
        eyebrow="Claims Intelligence"
        title="Work Queue"
        sub="Claims requiring officer attention across Motor Operations. AI has pre-processed extractions, evidence, and risk signals."
        action={<button className="rounded-2xl gradient-primary text-white text-[13px] font-medium px-4 py-2.5 shadow-l2 hover-lift">+ New Claim (FNOL)</button>}
      />

      {/* Filter bar */}
      <div className="glass-subtle p-3 mb-4 flex flex-wrap items-center gap-2">
        <div className="flex items-center gap-2 rounded-xl bg-white/80 border border-cloud px-3 py-2 min-w-[280px] flex-1 max-w-md">
          <Search className="h-3.5 w-3.5 text-stone" />
          <input value={q} onChange={e => setQ(e.target.value)} placeholder="Search claim, customer, policy…" className="bg-transparent outline-none text-[13px] flex-1 placeholder:text-stone text-navy" />
        </div>
        <div className="flex flex-wrap gap-1.5">
          {filters.map(x => (
            <button key={x} onClick={() => setF(x)} className={`text-[12px] rounded-full px-3 py-1.5 border transition ${f === x ? "border-info/30 bg-[hsl(var(--info)/0.08)] text-info" : "border-cloud bg-white/70 text-slate2 hover:bg-white"}`}>{x}</button>
          ))}
        </div>
        <div className="ml-auto flex items-center gap-1.5">
          <button className="inline-flex items-center gap-1.5 text-[12px] text-slate2 rounded-xl border border-cloud bg-white/70 px-3 py-1.5 hover-lift"><Filter className="h-3.5 w-3.5" /> More filters</button>
          <button className="inline-flex items-center gap-1.5 text-[12px] text-slate2 rounded-xl border border-cloud bg-white/70 px-3 py-1.5 hover-lift"><Users2 className="h-3.5 w-3.5" /> Bulk assign</button>
        </div>
      </div>

      {/* Table */}
      <GlassCard className="p-0 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-[13px]">
            <thead>
              <tr className="text-left text-[11px] uppercase tracking-[0.12em] text-stone border-b border-cloud">
                {["Claim","Customer","Incident","Status","Missing","SLA","Risk","Officer","Confidence","Updated",""].map(h => (
                  <th key={h} className="px-4 py-3 font-medium">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rows.map(c => (
                <tr key={c.id} className="border-b border-cloud/60 hover:bg-white/70 transition cursor-pointer group">
                  <td className="px-4 py-3">
                    <div className="font-medium text-navy">{c.id}</div>
                    <div className="text-[11px] text-stone">{c.policy}</div>
                  </td>
                  <td className="px-4 py-3">
                    <div className="text-navy">{c.customer}</div>
                    <div className="text-[11px] text-stone">{c.channel} · {c.region}</div>
                  </td>
                  <td className="px-4 py-3 max-w-[240px]"><div className="text-slate2 truncate">{c.incident}</div><div className="text-[11px] text-stone">{c.amount}</div></td>
                  <td className="px-4 py-3"><StatusBadge status={c.status} /></td>
                  <td className="px-4 py-3">
                    {c.missingDocs === 0
                      ? <Badge tone="success">Complete</Badge>
                      : <Badge tone="warning">{c.missingDocs} missing</Badge>}
                  </td>
                  <td className="px-4 py-3"><SLABadge sla={c.sla} /></td>
                  <td className="px-4 py-3"><RiskBadge risk={c.risk} /></td>
                  <td className="px-4 py-3 text-slate2">{c.officer}</td>
                  <td className="px-4 py-3"><ConfidenceBadge value={c.confidence} /></td>
                  <td className="px-4 py-3 text-stone whitespace-nowrap">{c.updated}</td>
                  <td className="px-4 py-3">
                    <Link to={`/app/claims/${c.id}`} className="inline-flex items-center gap-1 text-info text-[12px] opacity-60 group-hover:opacity-100">
                      Review <ArrowRight className="h-3 w-3" />
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="flex items-center justify-between px-4 py-3 text-[12px] text-stone border-t border-cloud">
          <div>Showing {rows.length} of {claims.length} claims</div>
          <div className="flex items-center gap-2">
            <button className="rounded-lg border border-cloud px-2.5 py-1 hover:bg-white">Previous</button>
            <button className="rounded-lg border border-cloud px-2.5 py-1 hover:bg-white">Next</button>
          </div>
        </div>
      </GlassCard>
    </>
  );
}
