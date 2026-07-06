"use client";
import { Link } from "@/components/iip/NextRouterCompat";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import { Building2, Users, FileText, Workflow, Sparkles, Plug, ShieldCheck, ScrollText, Bot, CreditCard, Gavel, Cog, ArrowRight, LifeBuoy } from "lucide-react";

const cards = [
  { to: "/app/admin/agents", icon: Bot, title: "AI & Agent Management", desc: "Registry · versions · permissions · kill switch · run logs · feedback." },
  { to: "/app/admin/tenant-policy", icon: Gavel, title: "Tenant Agent Policy", desc: "Baseline guardrails inherited by all agents in this workspace." },
  { to: "/app/admin/billing", icon: CreditCard, title: "Billing, Packaging & Usage", desc: "Enterprise Insurance · seats, AI runs, docs, messages, storage." },
  { to: "/app/settings", icon: Cog, title: "Tenant & workspace settings", desc: "Regions, defaults, SLAs, document checklists, retention." },
  { to: "/app/integrations", icon: Plug, title: "Integrations & Channels", desc: "WhatsApp · Graph · SMS · Web · Broker · Azure providers." },
  { to: "/app/support", icon: LifeBuoy, title: "Support & Health", desc: "Provider health, queue depth, failed jobs, safe diagnostics." },
];

const inline = [
  { icon: Users, title: "Users & roles", desc: "142 users · 10 roles · SCIM enabled" },
  { icon: FileText, title: "Document templates", desc: "12 templates · Motor claim, FNOL, Assessor report" },
  { icon: Workflow, title: "Workflow rules", desc: "24 rules across Claims, Underwriting, Approvals" },
  { icon: Sparkles, title: "AI providers", desc: "Azure OpenAI (primary), Azure Document Intelligence" },
  { icon: ShieldCheck, title: "Security & access", desc: "SSO enforced · Conditional Access · MFA" },
  { icon: ScrollText, title: "Audit & retention", desc: "7-year retention · regulator-ready export" },
];

export default function Admin() {
  return (
    <>
      <SectionHeader eyebrow="Admin" title="Admin Console" sub="Tenant, users, workflows, documents, channels, AI providers, agent governance, and integrations." />

      <div className="text-[11px] uppercase tracking-[0.14em] text-stone mb-2">Governance & operations</div>
      <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-3 mb-6">
        {cards.map(c => (
          <Link key={c.title} to={c.to} className="block">
            <GlassCard className="hover-lift h-full">
              <div className="flex items-start gap-3">
                <div className="h-10 w-10 rounded-xl bg-[hsl(var(--info)/0.08)] text-info grid place-items-center"><c.icon className="h-4 w-4" /></div>
                <div>
                  <div className="text-[14px] font-semibold text-navy">{c.title}</div>
                  <div className="text-[12.5px] text-stone">{c.desc}</div>
                </div>
              </div>
              <div className="mt-3 flex items-center justify-between">
                <Badge tone="success">Configured</Badge>
                <span className="text-[12px] text-info inline-flex items-center gap-1">Manage <ArrowRight className="h-3 w-3" /></span>
              </div>
            </GlassCard>
          </Link>
        ))}
      </div>

      <div className="text-[11px] uppercase tracking-[0.14em] text-stone mb-2">Tenant profile</div>
      <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-3">
        <GlassCard>
          <div className="flex items-start gap-3">
            <div className="h-10 w-10 rounded-xl bg-[hsl(var(--info)/0.08)] text-info grid place-items-center"><Building2 className="h-4 w-4" /></div>
            <div><div className="text-[14px] font-semibold text-navy">Tenant profile</div><div className="text-[12.5px] text-stone">Global Insurance Co. · Enterprise · Region EA-North</div></div>
          </div>
        </GlassCard>
        {inline.map(c => (
          <GlassCard key={c.title}>
            <div className="flex items-start gap-3">
              <div className="h-10 w-10 rounded-xl bg-[hsl(var(--info)/0.08)] text-info grid place-items-center"><c.icon className="h-4 w-4" /></div>
              <div><div className="text-[14px] font-semibold text-navy">{c.title}</div><div className="text-[12.5px] text-stone">{c.desc}</div></div>
            </div>
          </GlassCard>
        ))}
      </div>
    </>
  );
}
