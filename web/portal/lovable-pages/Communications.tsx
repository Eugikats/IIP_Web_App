"use client";
import { useState } from "react";
import { toast } from "sonner";
import { SectionHeader, GlassCard, Badge, StatusBadge } from "@/components/iip/Primitives";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { communications, type CommStatus } from "@/lib/lovable-parity/mock";
import { CheckCircle2, XCircle, Send, MessageSquareText } from "lucide-react";

export default function Communications() {
  const [tab, setTab] = useState<"All" | CommStatus>("Pending Approval");
  const filtered = tab === "All" ? communications : communications.filter((c) => c.status === tab);
  const tabs: (CommStatus | "All")[] = ["Pending Approval", "Approved", "Sent", "Send Failed", "Rejected", "All"];

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

      <GlassCard className="!p-0 overflow-hidden">
        <table className="w-full text-[13px]">
          <thead className="bg-white/60">
            <tr className="text-left text-[11px] uppercase tracking-[0.14em] text-stone">
              <th className="p-3">Draft</th><th className="p-3">Customer</th><th className="p-3">Channel</th><th className="p-3">Status</th><th className="p-3">Drafted</th><th className="p-3">Reviewer</th><th className="p-3"></th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((c) => (
              <tr key={c.id} className="border-t border-cloud/70 hover:bg-white/50">
                <td className="p-3">
                  <div className="text-navy font-medium">{c.subject}</div>
                  <div className="text-[11.5px] text-stone truncate max-w-[380px]">{c.preview}</div>
                  {c.claim && <div className="text-[11px] text-info mt-0.5">{c.claim}</div>}
                </td>
                <td className="p-3 text-slate2">{c.customer}</td>
                <td className="p-3"><Badge tone="info">{c.channel}</Badge></td>
                <td className="p-3"><StatusBadge status={c.status} /></td>
                <td className="p-3 text-stone">{c.drafted}</td>
                <td className="p-3 text-slate2">{c.reviewer ?? "—"}</td>
                <td className="p-3">
                  <div className="flex items-center gap-1">
                    {c.status === "Pending Approval" && (
                      <>
                        <button onClick={() => toast.success("Approved — dispatched via provider")} className="text-[11.5px] px-2 py-1 rounded-lg bg-white border border-cloud text-success inline-flex items-center gap-1"><CheckCircle2 className="h-3 w-3" />Approve</button>
                        <button onClick={() => toast("Draft rejected")} className="text-[11.5px] px-2 py-1 rounded-lg bg-white border border-cloud text-error inline-flex items-center gap-1"><XCircle className="h-3 w-3" />Reject</button>
                      </>
                    )}
                    {c.status === "Approved" && (
                      <button onClick={() => toast.success("Sent")} className="text-[11.5px] px-2 py-1 rounded-lg bg-white border border-cloud text-navy inline-flex items-center gap-1"><Send className="h-3 w-3" />Send</button>
                    )}
                    <button onClick={() => toast("Opening draft editor")} className="text-[11.5px] px-2 py-1 rounded-lg bg-white border border-cloud text-slate2 inline-flex items-center gap-1"><MessageSquareText className="h-3 w-3" />Open</button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {filtered.length === 0 && <div className="p-8 text-center text-stone text-[13px]">No drafts in this state.</div>}
      </GlassCard>
    </>
  );
}
