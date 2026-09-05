"use client";

import Link from "next/link";
import { Activity, Building2, KeyRound, Mail, MessageCircle, Plug, Search, ShieldCheck } from "lucide-react";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import ProviderHealthGrid from "@/components/iip/ProviderHealthGrid";
import { Badge, GlassCard, SectionHeader } from "@/components/iip/Primitives";
import { useUI } from "@/context/UIStateContext";
import { canAccess, canAccessWithPermissions } from "@/lib/roles";
import { useRuntimeContext } from "@/lib/runtimeContext";
import { canUseLocalTestDoubles } from "@/lib/config";

const PORTS = [
  { name: "Inbound channels", detail: "Graph, Gmail, Zoho, or IMAP/SMTP adapters; polling remains the baseline guarantee." },
  { name: "Document processing", detail: "Blob Storage plus the configured document-intelligence provider, with human correction." },
  { name: "Knowledge retrieval", detail: "Approved tenant-scoped sources through the configured retrieval and embedding providers." },
  { name: "Workflow transport", detail: "Service Bus queues for durable work, retries, dead-lettering, and recovery." },
  { name: "Core-system access", detail: "Insurer-specific read adapters and separately approved, idempotent write-back adapters." },
  { name: "Customer delivery", detail: "Email, SMS, WhatsApp, or other approved providers behind the communication boundary." },
];

export default function Integrations() {
  const { roleDef } = useUI();
  const { runtimeContext, parityMode } = useRuntimeContext();
  const usePreviewRole = canUseLocalTestDoubles || parityMode;
  const canOpen = (path: string) => usePreviewRole
    ? canAccess(roleDef.key, path)
    : canAccessWithPermissions(runtimeContext?.permissions ?? [], path);
  const canOpenOnboarding = canOpen("/app/admin/onboarding");
  const canOpenWhatsApp = canOpen("/app/admin/whatsapp");
  const canOpenEmail = canOpen("/app/admin/email");
  const canOpenAgentGovernance = canOpen("/app/admin/agents");
  const canOpenAdmin = canOpen("/app/admin");

  return (
    <>
      <SectionHeader
        eyebrow="Platform · Integrations & Health"
        title="Integration control plane"
        sub="One operational surface for provider health, adapter boundaries, identity onboarding, and safe diagnostics."
      />

      <div className="mb-5"><GuardrailBanner text={parityMode ? "Provider states below are a labelled development snapshot for UI verification. They do not establish a live connection or provider availability." : "Provider health is read from the runtime. IIP does not show invented connection success, queue depth, webhook delivery, or support-ticket status."} /></div>

      <div className="mb-2 flex items-center justify-between gap-3">
        <div><div className="text-[11px] uppercase tracking-[0.14em] text-muted">{parityMode ? "Synthetic provider-health states" : "Live provider health"}</div><div className="mt-1 text-[12px] text-ink-soft">Protected runtimes fail closed when required providers are missing or simulated.</div></div>
        <Badge tone="info"><Activity className="mr-1 inline h-3 w-3" /> {parityMode ? "Development snapshot" : "Runtime reported"}</Badge>
      </div>
      <ProviderHealthGrid />

      <div className="mt-6 grid gap-4 xl:grid-cols-[minmax(0,1.2fr)_minmax(320px,0.8fr)]">
        <GlassCard>
          <div className="flex items-center gap-2"><Plug className="h-4 w-4 text-info" /><div className="text-[14px] font-semibold text-ink">Adapter boundaries</div></div>
          <div className="mt-1 text-[12px] text-muted">These are supported integration seams, not claims that a customer connection is configured.</div>
          <div className="mt-4 grid gap-2 sm:grid-cols-2">
            {PORTS.map((port) => (
              <div key={port.name} className="rounded-2xl border border-line bg-white/65 p-3">
                <div className="text-[12.5px] font-medium text-ink">{port.name}</div>
                <div className="mt-1 text-[11.5px] leading-5 text-muted">{port.detail}</div>
              </div>
            ))}
          </div>
        </GlassCard>

        <div className="space-y-4">
          <GlassCard>
            <div className="flex items-center gap-2"><Search className="h-4 w-4 text-violet" /><div className="text-[14px] font-semibold text-ink">Safe diagnostics</div></div>
            <div className="mt-3 rounded-xl border border-line bg-mist/60 px-3 py-2.5 text-[12px] text-muted">Correlation and run search appears here when the operations-telemetry query endpoint is configured.</div>
            <div className="mt-3 flex flex-wrap gap-2"><Badge tone="warning">Queue depth not configured</Badge><Badge tone="warning">Retry telemetry not configured</Badge></div>
          </GlassCard>

          <GlassCard>
            <div className="flex items-center gap-2"><KeyRound className="h-4 w-4 text-info" /><div className="text-[14px] font-semibold text-ink">Identity and secrets</div></div>
            <p className="mt-2 text-[12px] leading-5 text-ink-soft">Customer Entra consent and managed identities are configured through controlled deployment and onboarding paths. WhatsApp Business credentials are the one exception — they're entered here, validated against Meta, and encrypted at rest.</p>
            <div className="mt-3 flex flex-wrap gap-2">
              {canOpenWhatsApp ? <Link href="/app/admin/whatsapp" className="inline-flex items-center gap-2 rounded-xl border border-line bg-white px-3 py-2 text-[11.5px] font-medium text-ink focus-ring"><MessageCircle className="h-3.5 w-3.5" /> WhatsApp Business</Link> : null}
              {canOpenEmail ? <Link href="/app/admin/email" className="inline-flex items-center gap-2 rounded-xl border border-line bg-white px-3 py-2 text-[11.5px] font-medium text-ink focus-ring"><Mail className="h-3.5 w-3.5" /> Email Mailboxes</Link> : null}
              {canOpenOnboarding ? <Link href="/app/admin/onboarding" className="inline-flex items-center gap-2 rounded-xl border border-line bg-white px-3 py-2 text-[11.5px] font-medium text-ink focus-ring"><Building2 className="h-3.5 w-3.5" /> Tenant onboarding</Link> : null}
              {canOpenAgentGovernance ? <Link href="/app/admin/agents" className="inline-flex items-center gap-2 rounded-xl border border-line bg-white px-3 py-2 text-[11.5px] font-medium text-ink focus-ring"><ShieldCheck className="h-3.5 w-3.5" /> Agent governance</Link> : null}
              {!canOpenOnboarding && !canOpenAgentGovernance && canOpenAdmin ? <Link href="/app/admin" className="inline-flex items-center gap-2 rounded-xl border border-line bg-white px-3 py-2 text-[11.5px] font-medium text-ink focus-ring"><ShieldCheck className="h-3.5 w-3.5" /> Administration</Link> : null}
            </div>
          </GlassCard>
        </div>
      </div>
    </>
  );
}
