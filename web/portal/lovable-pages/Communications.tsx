"use client";
import Link from "next/link";
import { useMemo, useState } from "react";
import { SectionHeader, GlassCard, Badge, StatusBadge } from "@/components/iip/Primitives";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { ExternalLink } from "lucide-react";
import { useCommunications, type CommStatus } from "@/lib/lovable-parity/data/communications";

export default function Communications() {
  const { items, loading, error } = useCommunications();
  const [tab, setTab] = useState<"All" | CommStatus>("Pending Approval");
  const tabs: (CommStatus | "All")[] = ["Pending Approval", "Approved", "Sent", "Send Failed", "Rejected", "All"];
  const filtered = useMemo(() => (tab === "All" ? items : items.filter((c) => c.status === tab)), [items, tab]);

  return (
    <>
      <SectionHeader
        eyebrow="Customer Communications"
        title="Communication drafts"
        sub="AI drafts customer-safe messages. Every send requires named human approval — no sensitive message is dispatched automatically."
      />
      <div className="mb-4"><GuardrailBanner text="Drafts must not promise settlement, coverage, or payment. Approved drafts are dispatched via the configured provider and logged in Audit." /></div>

      <div className="flex flex-wrap gap-1.5 mb-4">
        {tabs.map((t) => (
          <button key={t} onClick={() => setTab(t)} className={`text-[12px] rounded-full px-3 py-1.5 border ${tab === t ? "bg-navy text-white border-navy" : "bg-white text-slate2 border-cloud"}`}>{t}</button>
        ))}
      </div>

      {loading ? (
        <GlassCard><div className="p-6 text-center text-[13px] text-stone">Loading communications…</div></GlassCard>
      ) : items.length === 0 ? (
        <GlassCard>
          <div className="p-10 text-center text-stone">
            <div className="text-navy font-medium">No communication drafts</div>
            <div className="text-[12.5px] mt-1">{error ? error : "Customer-update drafts appear here once created from a claim or Customer Service."}</div>
          </div>
        </GlassCard>
      ) : (
        <GlassCard className="!p-0 overflow-hidden">
          <table className="w-full text-[13px]">
            <thead className="bg-white/60">
              <tr className="text-left text-[11px] uppercase tracking-[0.14em] text-stone">
                <th className="p-3">Draft</th><th className="p-3">Channel</th><th className="p-3">Status</th><th className="p-3">Drafted</th><th className="p-3">By</th><th className="p-3"></th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((c) => (
                <tr key={c.id} className="border-t border-cloud/70 hover:bg-white/50">
                  <td className="p-3">
                    <div className="text-navy font-medium">{c.subject}</div>
                    <div className="text-[11.5px] text-stone truncate max-w-[420px]">{c.preview}</div>
                    {c.claim && c.claim !== "—" && <div className="text-[11px] text-info mt-0.5">{c.claim}</div>}
                  </td>
                  <td className="p-3"><Badge tone="info">{c.channel}</Badge></td>
                  <td className="p-3"><StatusBadge status={c.status} /></td>
                  <td className="p-3 text-stone">{c.drafted}</td>
                  <td className="p-3 text-slate2">{c.reviewer ?? "—"}</td>
                  <td className="p-3">
                    {c.claim && c.claim !== "—" ? <Link href={`/app/claims/${c.claim}`} className="inline-flex items-center gap-1 rounded-lg border border-cloud bg-white px-2 py-1 text-[11.5px] font-medium text-navy focus-ring"><ExternalLink className="h-3 w-3" /> Open claim</Link> : <Badge tone="neutral">No case link</Badge>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {filtered.length === 0 && <div className="p-8 text-center text-stone text-[13px]">No drafts in this state.</div>}
        </GlassCard>
      )}
      <div className="mt-3 text-[11.5px] text-stone">This screen is a read-only communication ledger. Draft approval, rejection, and send remain inside the governed claim review where evidence and recipients are visible.</div>
    </>
  );
}
