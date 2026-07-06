"use client";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import { leads } from "@/lib/lovable-parity/mock";
import { MessageSquare, Mail, Globe, Building2, Sparkles, Inbox } from "lucide-react";
import { toast } from "sonner";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";

const sourceIcon = { WhatsApp: MessageSquare, Email: Mail, Web: Globe, Broker: Building2 } as const;
const parity = LOVABLE_VISUAL_PARITY_MODE;
const rows = parity ? leads : [];

export default function CustomerAcquisition() {
  return (
    <>
      <SectionHeader eyebrow="Customer Acquisition" title="Leads Inbox" sub="Prospects from WhatsApp, web, email, brokers, and campaigns. AI qualifies and drafts customer-safe product education." />

      <div className="grid md:grid-cols-4 gap-3 mb-6">
        {[
          { l: "New leads today", t: "info" as const, v: parity ? 34 : "—" },
          { l: "In conversation", t: "violet" as const, v: parity ? 27 : "—" },
          { l: "Awaiting KYC", t: "warning" as const, v: parity ? 9 : "—" },
          { l: "Converted (7d)", t: "success" as const, v: parity ? 41 : "—" },
        ].map(k => (
          <div key={k.l} className="glass-subtle p-4">
            <div className="text-[11.5px] text-stone">{k.l}</div>
            <div className="mt-1 text-[26px] leading-none font-semibold text-navy">{k.v}</div>
            <Badge tone={k.t} className="mt-2">Pipeline</Badge>
          </div>
        ))}
      </div>

      {rows.length === 0 ? (
        <GlassCard>
          <div className="p-10 text-center flex flex-col items-center gap-2">
            <Inbox className="h-6 w-6 text-stone" />
            <div className="text-navy font-medium">Lead intake not configured</div>
            <div className="text-[12.5px] text-stone max-w-md">Qualified leads appear here once a channel/campaign lead source is connected. No sample leads are shown.</div>
          </div>
        </GlassCard>
      ) : (
        <div className="space-y-3">
          {rows.map(l => {
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
      )}
    </>
  );
}
