"use client";
import { useState } from "react";
import { toast } from "sonner";
import { Search, MessageCircle, Mail, Globe, Phone, Users2, MonitorPlay, Plus, Send, ShieldAlert } from "lucide-react";
import { GlassCard, SectionHeader, Badge, StatusBadge } from "@/components/iip/Primitives";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { conversations, type Channel } from "@/lib/lovable-parity/mock";

const ICON: Record<Channel, any> = {
  WhatsApp: MessageCircle, Email: Mail, Web: Globe, SMS: Phone, Broker: Users2, Teams: MonitorPlay,
};

export default function Channels() {
  const [active, setActive] = useState(conversations[0]);
  const [filter, setFilter] = useState<"All" | Channel>("All");
  const list = filter === "All" ? conversations : conversations.filter((c) => c.channel === filter);

  return (
    <>
      <SectionHeader
        eyebrow="Integrations & Channels"
        title="Channels inbox"
        sub="Unified customer, broker, and internal channel traffic routed into the platform. AI drafts responses — humans approve sensitive replies."
        action={<button className="text-[13px] rounded-2xl gradient-primary text-white px-4 py-2 shadow-l2 hover-lift"><Plus className="h-4 w-4 inline -mt-0.5 mr-1" /> Connect new channel</button>}
      />

      <div className="mb-4"><GuardrailBanner text="Sensitive outbound messages route through approval before send. Nothing dispatches automatically." /></div>

      <div className="grid grid-cols-12 gap-4">
        <GlassCard className="col-span-12 lg:col-span-4 !p-0 overflow-hidden">
          <div className="p-3 border-b border-cloud/70">
            <div className="flex items-center gap-2 rounded-xl bg-white/70 border border-cloud px-3 py-2 text-[12.5px] text-stone">
              <Search className="h-3.5 w-3.5" /><span>Search customers, threads…</span>
            </div>
            <div className="mt-2 flex flex-wrap gap-1.5">
              {(["All","WhatsApp","Email","Web","SMS","Broker","Teams"] as const).map((f) => (
                <button key={f} onClick={() => setFilter(f)} className={`text-[11px] rounded-full px-2.5 py-1 border ${filter === f ? "bg-navy text-white border-navy" : "bg-white text-slate2 border-cloud"}`}>{f}</button>
              ))}
            </div>
          </div>
          <div className="max-h-[560px] overflow-y-auto">
            {list.map((c) => {
              const I = ICON[c.channel];
              const isActive = c.id === active.id;
              return (
                <button
                  key={c.id}
                  onClick={() => setActive(c)}
                  className={`w-full text-left p-3 border-b border-cloud/70 hover:bg-white/60 ${isActive ? "bg-white/70" : ""}`}
                >
                  <div className="flex items-center gap-2">
                    <div className="h-8 w-8 rounded-xl bg-white border border-cloud grid place-items-center text-slate2"><I className="h-4 w-4" /></div>
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center justify-between">
                        <div className="text-[13px] font-medium text-navy truncate">{c.customer}</div>
                        <div className="text-[10.5px] text-stone">{c.updated}</div>
                      </div>
                      <div className="text-[12px] text-stone truncate">{c.snippet}</div>
                    </div>
                  </div>
                  <div className="mt-1.5 flex items-center gap-1.5 flex-wrap">
                    <Badge tone="info">{c.channel}</Badge>
                    <Badge tone="neutral">{c.intent}</Badge>
                    {!c.matched && <Badge tone="warning">Unmatched customer</Badge>}
                    {c.needsApproval && <Badge tone="violet">Approval gated</Badge>}
                    {c.unread > 0 && <Badge tone="error">{c.unread} new</Badge>}
                  </div>
                </button>
              );
            })}
          </div>
        </GlassCard>

        <GlassCard className="col-span-12 lg:col-span-8">
          <div className="flex items-center justify-between mb-3">
            <div>
              <div className="text-[11px] uppercase tracking-[0.14em] text-stone">{active.channel} · {active.id}</div>
              <div className="text-[16px] font-semibold text-navy">{active.customer}</div>
            </div>
            <div className="flex items-center gap-2">
              {!active.matched && <Badge tone="warning">Unmatched — link to customer</Badge>}
              <StatusBadge status={active.needsApproval ? "Human Review Required" : "Active"} />
            </div>
          </div>

          <div className="rounded-2xl border border-cloud bg-white/70 p-4 space-y-3 max-h-[360px] overflow-y-auto">
            <div className="text-[12.5px] text-stone">Yesterday 16:41</div>
            <div className="rounded-xl bg-cloud/40 px-3 py-2 text-[13px] text-navy max-w-[80%]">Hello, I had an accident this morning. What documents do I need to send?</div>
            <div className="ml-auto rounded-xl gradient-primary text-white px-3 py-2 text-[13px] max-w-[80%]">Thank you. Please share your driving permit, national ID, vehicle photos, and the police report if available.</div>
            <div className="rounded-xl bg-cloud/40 px-3 py-2 text-[13px] text-navy max-w-[80%]">{active.snippet}</div>
          </div>

          <div className="mt-3 grid sm:grid-cols-4 gap-2">
            <button onClick={() => toast.success("Claim draft created", { description: "Continue in Claims Intelligence." })} className="rounded-2xl bg-white border border-cloud px-3 py-2 text-[12.5px] text-navy hover-lift">Create claim</button>
            <button onClick={() => toast.success("Lead created", { description: "Handed off to Customer Acquisition." })} className="rounded-2xl bg-white border border-cloud px-3 py-2 text-[12.5px] text-navy hover-lift">Create lead</button>
            <button onClick={() => toast("Draft prepared — awaiting approval")} className="rounded-2xl bg-white border border-cloud px-3 py-2 text-[12.5px] text-navy hover-lift inline-flex items-center justify-center gap-1"><Send className="h-3.5 w-3.5" /> Draft response</button>
            <button onClick={() => toast("Escalated to supervisor")} className="rounded-2xl bg-white border border-cloud px-3 py-2 text-[12.5px] text-navy hover-lift inline-flex items-center justify-center gap-1"><ShieldAlert className="h-3.5 w-3.5" /> Escalate</button>
          </div>
        </GlassCard>
      </div>
    </>
  );
}
