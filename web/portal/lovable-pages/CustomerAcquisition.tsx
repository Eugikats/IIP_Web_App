"use client";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import { leads } from "@/lib/lovable-parity/mock";
import { MessageSquare, Mail, Globe, Building2, Sparkles } from "lucide-react";
import { toast } from "sonner";

const sourceIcon = { WhatsApp: MessageSquare, Email: Mail, Web: Globe, Broker: Building2 } as const;

export default function CustomerAcquisition() {
  return (
    <>
      <SectionHeader eyebrow="Customer Acquisition" title="Leads Inbox" sub="Prospects from WhatsApp, web, email, brokers, and campaigns. AI qualifies and drafts customer-safe product education." />

      <div className="grid md:grid-cols-4 gap-3 mb-6">
        {[
          { l: "New leads today", v: 34, t: "info" as const },
          { l: "In conversation", v: 27, t: "violet" as const },
          { l: "Awaiting KYC", v: 9, t: "warning" as const },
          { l: "Converted (7d)", v: 41, t: "success" as const },
        ].map(k => (
          <div key={k.l} className="glass-subtle p-4">
            <div className="text-[11.5px] text-stone">{k.l}</div>
            <div className="mt-1 text-[26px] leading-none font-semibold text-navy">{k.v}</div>
            <Badge tone={k.t} className="mt-2">Pipeline</Badge>
          </div>
        ))}
      </div>

      <div className="grid lg:grid-cols-3 gap-4">
        <div className="lg:col-span-2 space-y-3">
          {leads.map(l => {
            const Icon = sourceIcon[l.source as keyof typeof sourceIcon] ?? Globe;
            return (
              <GlassCard key={l.id} className="hover-lift">
                <div className="flex items-start gap-4">
                  <div className="h-11 w-11 rounded-2xl bg-gradient-secondary grid place-items-center text-white text-[13px] font-semibold">{l.name.split(" ").map(x => x[0]).slice(0,2).join("")}</div>
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <div className="text-[14px] font-semibold text-navy">{l.name}</div>
                      <div className="text-[11.5px] text-stone">· {l.id}</div>
                    </div>
                    <div className="text-[12.5px] text-slate2 mt-0.5 inline-flex items-center gap-1.5">
                      <Icon className="h-3.5 w-3.5 text-stone" /> {l.source} · {l.product}
                    </div>
                    <div className="mt-3 flex flex-wrap gap-2 items-center">
                      <Badge tone={l.score >= 0.75 ? "success" : l.score >= 0.6 ? "info" : "warning"}>AI qualification · {Math.round(l.score * 100)}%</Badge>
                      <Badge tone={l.status === "Needs KYC" ? "warning" : "violet"}>{l.status}</Badge>
                      <span className="text-[11.5px] text-stone">Assigned · {l.officer}</span>
                    </div>
                  </div>
                  <div className="flex flex-col gap-2 shrink-0">
                    <button onClick={() => toast.success("Converted to customer draft")} className="text-[12.5px] rounded-xl gradient-primary text-white px-3 py-1.5">Convert</button>
                    <button onClick={() => toast("Follow-up task created")} className="text-[12.5px] rounded-xl border border-cloud bg-white px-3 py-1.5 text-navy">Follow up</button>
                  </div>
                </div>
              </GlassCard>
            );
          })}
        </div>

        <GlassCard>
          <div className="flex items-center gap-2 mb-3"><Sparkles className="h-3.5 w-3.5 text-info" /><div className="text-[11px] uppercase tracking-[0.14em] text-stone">Copilot suggested draft</div></div>
          <div className="text-[12.5px] text-slate2 mb-2">To: Brian Ssemakula · WhatsApp</div>
          <div className="rounded-2xl border border-cloud bg-white/80 p-3 text-[13px] text-slate2 leading-relaxed">
            Hello Brian, thank you for your interest in <b>Motor Comprehensive cover</b>. Based on the vehicle details you shared, an indicative annual premium is <b>UGX 1,850,000</b>.
            To move forward, please share a copy of your <b>National ID</b> and <b>logbook</b>. Standard cover includes third-party liability, own damage, and theft.
            <div className="mt-2 text-[11px] text-stone">Draft — human review required before sending.</div>
          </div>
          <div className="mt-3 flex gap-2">
            <button className="flex-1 text-[12.5px] rounded-xl gradient-primary text-white py-2">Approve & send</button>
            <button className="text-[12.5px] rounded-xl border border-cloud bg-white px-3 py-2 text-navy">Edit</button>
          </div>
        </GlassCard>
      </div>
    </>
  );
}
