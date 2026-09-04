"use client";
import { Link } from "@/components/iip/NextRouterCompat";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import { Building2, Users, FileText, Workflow, Sparkles, Plug, ShieldCheck, ScrollText, Bot, CreditCard, Gavel, ArrowRight } from "lucide-react";
import { useUI } from "@/context/UIStateContext";
import { canAccess, canAccessWithPermissions } from "@/lib/roles";
import { useRuntimeContext } from "@/lib/runtimeContext";
import { canUseLocalTestDoubles } from "@/lib/config";

const cards = [
  { to: "/app/admin/users", icon: Users, title: "Users & roles", desc: "Add users, resolved from server-side membership and permission data. Role changes are audited." },
  { to: "/app/admin/agents", icon: Bot, title: "Agents", desc: "Registry · versions · permissions · kill switch · run logs · feedback." },
  { to: "/app/admin/tenant-policy", icon: Gavel, title: "Agent Policy", desc: "Baseline guardrails inherited by all agents in this workspace." },
  { to: "/app/flows", icon: Workflow, title: "Flows", desc: "Governed Releases 1–5, tenant workflow drafts, publication, and run inspection." },
  { to: "/app/admin/onboarding", icon: Building2, title: "Tenant Onboarding", desc: "Customer Entra tenant identity, Azure subscription targeting, consent, and workspaces." },
  { to: "/app/admin/billing", icon: CreditCard, title: "Billing & Usage", desc: "Plan, entitlement, seat, AI run, document, message, and storage controls." },
  { to: "/app/integrations", icon: Plug, title: "Integrations & Health", desc: "Live provider health, adapter boundaries, identity paths, and safe diagnostics." },
];

// Informational only — deliberately not clickable. None of these has a management surface yet
// (see the build contract's scope boundary); they describe where the underlying state comes from
// rather than implying a page exists.
const inline = [
  { icon: FileText, title: "Document templates", desc: "Managed by tenant configuration; absent templates remain empty." },
  { icon: Workflow, title: "Workflow rules", desc: "Configured workflow rules apply human-review boundaries." },
  { icon: Sparkles, title: "AI providers", desc: "Provider state is reported by runtime health checks." },
  { icon: ShieldCheck, title: "Security & access", desc: "Authentication and access posture come from live runtime configuration." },
  { icon: ScrollText, title: "Audit & retention", desc: "Audit and retention policy must be confirmed by tenant settings." },
];

export default function Admin() {
  const { roleDef } = useUI();
  const { runtimeContext, parityMode } = useRuntimeContext();
  const usePreviewRole = canUseLocalTestDoubles || parityMode;
  const visibleCards = cards.filter((card) =>
    usePreviewRole
      ? canAccess(roleDef.key, card.to)
      : canAccessWithPermissions(runtimeContext?.permissions ?? [], card.to)
  );

  return (
    <>
      <SectionHeader eyebrow="Admin" title="Admin Console" sub="Tenant, users, workflows, documents, channels, AI providers, agent governance, and integrations." />

      <div className="text-[11px] uppercase tracking-[0.14em] text-muted mb-2">Governance & operations</div>
      <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-3 mb-6">
        {visibleCards.map(c => (
          <Link key={c.title} to={c.to} className="block">
            <GlassCard className="hover-lift h-full">
              <div className="flex items-start gap-3">
                <div className="h-10 w-10 rounded-xl bg-info/8 text-info grid place-items-center"><c.icon className="h-4 w-4" /></div>
                <div>
                  <div className="text-[14px] font-semibold text-ink">{c.title}</div>
                  <div className="text-[12.5px] text-muted">{c.desc}</div>
                </div>
              </div>
              <div className="mt-3 flex items-center justify-between">
                <Badge tone="neutral">Open</Badge>
                <span className="text-[12px] text-info inline-flex items-center gap-1">Manage <ArrowRight className="h-3 w-3" /></span>
              </div>
            </GlassCard>
          </Link>
        ))}
      </div>

      <div className="text-[11px] uppercase tracking-[0.14em] text-muted mb-2">Tenant profile</div>
      <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-3">
        <GlassCard>
          <div className="flex items-start gap-3">
            <div className="h-10 w-10 rounded-xl bg-info/8 text-info grid place-items-center"><Building2 className="h-4 w-4" /></div>
            <div><div className="text-[14px] font-semibold text-ink">Tenant profile</div><div className="text-[12.5px] text-muted">Resolved from the authenticated tenant and workspace context.</div></div>
          </div>
        </GlassCard>
        {inline.map(c => (
          <GlassCard key={c.title}>
            <div className="flex items-start gap-3">
              <div className="h-10 w-10 rounded-xl bg-info/8 text-info grid place-items-center"><c.icon className="h-4 w-4" /></div>
              <div><div className="text-[14px] font-semibold text-ink">{c.title}</div><div className="text-[12.5px] text-muted">{c.desc}</div></div>
            </div>
          </GlassCard>
        ))}
      </div>
    </>
  );
}
