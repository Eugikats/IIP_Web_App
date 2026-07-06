"use client";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import ProviderHealthGrid from "@/components/iip/ProviderHealthGrid";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { Plug } from "lucide-react";

export default function Integrations() {
  return (
    <>
      <SectionHeader
        eyebrow="Integrations & Channels"
        title="Connector health & configuration"
        sub="Channels, AI providers, document intelligence, storage, and messaging infrastructure. Write-back is adapter-based and human-approved."
        action={<button className="rounded-2xl gradient-primary text-white text-[13px] px-4 py-2 shadow-l2 hover-lift"><Plug className="inline h-4 w-4 -mt-0.5 mr-1" /> Test connection</button>}
      />

      <div className="mb-4"><GuardrailBanner text="No generic unrestricted database query tool. Write-back is controlled, adapter-based, human-approved, and idempotent — retries never duplicate external actions." /></div>

      <ProviderHealthGrid />

      <div className="grid md:grid-cols-2 gap-4 mt-6">
        <GlassCard>
          <div className="text-[14px] font-semibold text-navy mb-2">Field mapping</div>
          <div className="text-[12.5px] text-stone mb-3">Map external system fields to IIP entities for controlled ingestion and human-approved write-back.</div>
          <div className="space-y-2 text-[12.5px]">
            {["core.policyNumber → Policy.id", "core.claimId → Claim.id", "core.customerRef → Customer.id", "core.documentType → Document.type", "core.status → Workflow.state"].map((m) => (
              <div key={m} className="rounded-xl border border-cloud bg-white/70 px-3 py-2 text-slate2 font-mono text-[12px]">{m}</div>
            ))}
          </div>
        </GlassCard>
        <GlassCard>
          <div className="text-[14px] font-semibold text-navy mb-2">Ingestion methods (MVP)</div>
          <div className="grid gap-2 text-[12.5px]">
            {["API ingestion", "Microsoft Graph (Outlook)", "Secure file upload", "CSV / Excel import", "SFTP / batch imports", "Read-only policy lookup", "Controlled write-back (human-approved)"].map((m) => (
              <div key={m} className="rounded-xl border border-cloud bg-white/70 px-3 py-2 flex items-center justify-between"><span className="text-slate2">{m}</span><Badge tone="success">Enabled</Badge></div>
            ))}
          </div>
        </GlassCard>
      </div>
    </>
  );
}
