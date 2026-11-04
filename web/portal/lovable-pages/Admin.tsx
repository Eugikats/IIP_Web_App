"use client";
import { Link } from "@/components/iip/NextRouterCompat";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import { Building2, Users, FileText, Workflow, Sparkles, Plug, ShieldCheck, ScrollText, Bot, CreditCard, Gavel, ArrowRight, MessageCircle, Mail, Radio, PackageCheck, UploadCloud, Presentation, MessageSquareText, ClipboardCheck, Bell, SlidersHorizontal } from "lucide-react";
import { useUI } from "@/context/UIStateContext";
import { canAccess, canAccessWithPermissions } from "@/lib/roles";
import { useRuntimeContext } from "@/lib/runtimeContext";
import { canUseLocalTestDoubles, isDemonstrationRuntime } from "@/lib/config";

const cards = [
  { to: "/app/admin/setup", icon: ClipboardCheck, title: "Set up your organisation", desc: "Your profile and licence, the terms, and the steps before the assistant speaks to your customers." },
  { to: "/app/admin/products", icon: PackageCheck, title: "Products", desc: "Configure a product's claim process, document matrix and IRA approval. Every change is versioned and audited." },
  { to: "/app/admin/policy-import", icon: UploadCloud, title: "Policy Import", desc: "Load policies from a CSV or Excel export, map columns, review a validation report, then commit. Every record is marked with where it came from." },
  { to: "/app/admin/claims-pack", icon: PackageCheck, title: "Claims pack email", desc: "Who receives a claim's pack by email and when it is sent by itself." },
  { to: "/app/admin/status-wording", icon: MessageSquareText, title: "Claim status wording", desc: "What customers read about where their claim stands, status by status." },
  { to: "/app/admin/background-work", icon: Radio, title: "Background work", desc: "Documents being read and claims being reviewed, what is waiting, and what could not be done." },
  { to: "/app/admin/settings", icon: SlidersHorizontal, title: "Operating settings", desc: "Reminder and SLA warning timings, the IRA publishing rule, seat limit and claims allowance." },
  { to: "/app/admin/users", icon: Users, title: "Users & roles", desc: "Add users, resolved from server-side membership and permission data. Role changes are audited." },
  { to: "/app/admin/agents", icon: Bot, title: "Agents", desc: "Registry · versions · permissions · kill switch · run logs · feedback." },
  { to: "/app/admin/tenant-policy", icon: Gavel, title: "Agent Policy", desc: "Baseline guardrails inherited by all agents in this workspace." },
  { to: "/app/flows", icon: Workflow, title: "Flows", desc: "Governed Releases 1–5, tenant workflow drafts, publication, and run inspection." },
  { to: "/app/admin/onboarding", icon: Building2, title: "Tenant Onboarding", desc: "Customer Entra tenant identity, Azure subscription targeting, consent, and workspaces." },
  { to: "/app/admin/billing", icon: CreditCard, title: "Billing & Usage", desc: "Plan, entitlement, seat, AI run, document, message, and storage controls." },
  { to: "/app/integrations", icon: Plug, title: "Integrations & Health", desc: "Live provider health, adapter boundaries, identity paths, and safe diagnostics." },
  { to: "/app/admin/whatsapp", icon: MessageCircle, title: "WhatsApp Business", desc: "Connect your WhatsApp Business number, credentials are validated against Meta and encrypted at rest." },
  { to: "/app/admin/whatsapp-support", icon: MessageCircle, title: "WhatsApp setup requests", desc: "Publisher support queue for organisations requesting managed number setup." },
  { to: "/app/admin/templates", icon: MessageSquareText, title: "Message templates", desc: "The opening messages WhatsApp must approve before you can write first to someone. Edit drafts, submit them and record the decision." },
  { to: "/app/admin/email", icon: Mail, title: "Email Mailboxes", desc: "Configure claim mailboxes and process inbound email into verified, real claims." },
  ...(isDemonstrationRuntime
    ? [{ to: "/app/admin/demonstrations", icon: Presentation, title: "Demonstrations", desc: "Switch between the ready-made worlds: Mavuno Bank, Nile Life and Nile General. Invented people only." }]
    : []),
  { to: "/app/admin/connections", icon: Radio, title: "Connections", desc: "Connect SMS and other outbound channels, enter credentials, validated live, no redeploy needed." },
  { to: "/app/admin/notifications", icon: Bell, title: "Team notifications", desc: "Route claim events to additional workspace members and a configured Teams channel." },
];

// Informational only, deliberately not clickable. None of these has a management surface yet
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

      <div className="mb-2 text-[11px] font-bold uppercase tracking-[0.14em] text-dls-text-muted">Governance & operations</div>
      <div className="mb-6 grid gap-3 md:grid-cols-2 lg:grid-cols-3">
        {visibleCards.map((c) => (
          <Link key={c.title} to={c.to} className="dls-focus-outline block rounded-dls-card">
            <GlassCard className="h-full transition-colors duration-dls-fast ease-dls hover:border-dls-border-strong">
              <div className="flex items-start gap-3">
                <div className="grid h-10 w-10 shrink-0 place-items-center rounded-dls-chip bg-dls-bg-page text-dls-text-secondary"><c.icon className="h-4 w-4" aria-hidden="true" /></div>
                <div>
                  <div className="text-[14px] font-bold text-dls-text-primary">{c.title}</div>
                  <div className="text-[12.5px] text-dls-text-secondary">{c.desc}</div>
                </div>
              </div>
              <div className="mt-3 flex items-center justify-between">
                <Badge tone="neutral">Open</Badge>
                <span className="inline-flex items-center gap-1 text-[12px] font-semibold text-dls-primary">Manage <ArrowRight className="h-3 w-3" aria-hidden="true" /></span>
              </div>
            </GlassCard>
          </Link>
        ))}
      </div>

      <div className="mb-2 text-[11px] font-bold uppercase tracking-[0.14em] text-dls-text-muted">Tenant profile</div>
      <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-3">
        <GlassCard>
          <div className="flex items-start gap-3">
            <div className="grid h-10 w-10 shrink-0 place-items-center rounded-dls-chip bg-dls-bg-page text-dls-text-secondary"><Building2 className="h-4 w-4" aria-hidden="true" /></div>
            <div>
              <div className="text-[14px] font-bold text-dls-text-primary">Tenant profile</div>
              <div className="text-[12.5px] text-dls-text-secondary">Resolved from the authenticated tenant and workspace context.</div>
            </div>
          </div>
        </GlassCard>
        {inline.map((c) => (
          <GlassCard key={c.title}>
            <div className="flex items-start gap-3">
              <div className="grid h-10 w-10 shrink-0 place-items-center rounded-dls-chip bg-dls-bg-page text-dls-text-secondary"><c.icon className="h-4 w-4" aria-hidden="true" /></div>
              <div>
                <div className="text-[14px] font-bold text-dls-text-primary">{c.title}</div>
                <div className="text-[12.5px] text-dls-text-secondary">{c.desc}</div>
              </div>
            </div>
          </GlassCard>
        ))}
      </div>
    </>
  );
}
