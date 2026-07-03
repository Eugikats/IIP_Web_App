"use client";

import { useEffect, useState } from "react";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { StatusBadge } from "@/components/Badges";
import { ErrorState, LoadingState } from "@/components/States";
import { useRole } from "@/components/RoleContext";
import { apiBaseUrl, authMode, canUseLocalTestDoubles, scopedHeaders } from "@/lib/config";
import { motion } from "framer-motion";

type Readiness = {
  status: string;
  environment: string;
  storageProvider: string;
  blobProvider: string;
  aiProvider: string;
  documentProvider: string;
  retrievalProvider: string;
  queueProvider: string;
  emailProvider: string;
  communicationProvider: string;
  notificationProvider: string;
  seededBusinessData: boolean;
  errors: string[];
};

type ProviderHealthItem = {
  name: string;
  mode: string;
  status: string;
  detail: string;
};

type ProviderHealth = {
  overallStatus: string;
  providers: ProviderHealthItem[];
};

export default function SupportHealthPage() {
  const { role, userId } = useRole();
  const [readiness, setReadiness] = useState<Readiness | null>(null);
  const [providerHealth, setProviderHealth] = useState<ProviderHealth | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function load() {
      try {
        const [readinessResponse, providerHealthResponse] = await Promise.all([
          fetch(`${apiBaseUrl}/health/ready`),
          fetch(`${apiBaseUrl}/api/v1/admin/provider-health`, { headers: scopedHeaders(role, userId) })
        ]);
        setReadiness((await readinessResponse.json()) as Readiness);
        setProviderHealth(providerHealthResponse.ok ? ((await providerHealthResponse.json()) as ProviderHealth) : null);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Support health failed.");
      }
    }

    load();
  }, [role, userId]);

  if (error) return <ErrorState message={error} />;
  if (!readiness) return <LoadingState label="Loading support and health diagnostics..." />;

  const diagnostics = [
    ["Environment", readiness.environment],
    ["Database provider", readiness.storageProvider],
    ["Blob provider", readiness.blobProvider],
    ["AI provider", readiness.aiProvider],
    ["Document provider", readiness.documentProvider],
    ["Retrieval provider", readiness.retrievalProvider],
    ["Queue provider", readiness.queueProvider],
    ["Email provider", readiness.emailProvider],
    ["Communication provider", readiness.communicationProvider],
    ["Notification provider", readiness.notificationProvider],
    ["Seeded business data loader", readiness.seededBusinessData ? "Blocked" : "Off"]
  ];

  return (
    <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.4 }}>
      <PageHeader eyebrow="Diagnostics" title="Support & Health" description="Health, provider mode, and deployment diagnostics for the IIP portal." />
      <div className="grid gap-4 md:grid-cols-3">
        <SectionCard eyebrow="System" title="API health">
          <StatusBadge label={readiness.status} />
        </SectionCard>
        <SectionCard eyebrow="Identity" title="Auth mode">
          <StatusBadge label={authMode} />
        </SectionCard>
        <SectionCard eyebrow="Platform" title="Support posture">
          <StatusBadge label={providerHealth ? providerHealth.overallStatus : "Not configured"} />
        </SectionCard>
      </div>
      <div className="mt-5 grid gap-5 lg:grid-cols-2">
        <SectionCard eyebrow="Providers" title="Provider health" description="Safe status only. Secrets, connection strings, and internal stack traces are never shown.">
          {providerHealth ? (
            <div className="space-y-3">
              {providerHealth.providers.map((provider) => (
                <div key={provider.name} className="rounded-xl border border-line bg-mist p-4 transition hover:-translate-y-0.5 hover:border-accent hover:shadow-glow-cyan">
                  <div className="flex items-center justify-between gap-3">
                    <div>
                      <p className="text-sm font-semibold text-ink font-mono">{provider.name}</p>
                      <p className="mt-2 text-[10px] font-bold uppercase tracking-[0.2em] text-muted">{provider.mode}</p>
                    </div>
                    <StatusBadge label={provider.status} />
                  </div>
                  <p className="mt-3 text-sm leading-6 text-muted">{provider.detail}</p>
                </div>
              ))}
            </div>
          ) : (
            <p className="text-sm text-muted">Provider health details require Admin or ITAdmin access.</p>
          )}
        </SectionCard>
        <SectionCard eyebrow="Configuration" title="Readiness diagnostics" description="Current readiness snapshot from the backend production gate.">
          <div className="space-y-3">
            {diagnostics.map(([label, value]) => (
              <div key={label} className="flex items-center justify-between gap-3 rounded-xl border border-line bg-mist px-4 py-3 transition-colors hover:border-accent/40 hover:bg-surface/50">
                <span className="text-sm font-semibold text-ink font-mono">{label}</span>
                <StatusBadge label={String(value)} />
              </div>
            ))}
          </div>
          {readiness.errors.length > 0 ? (
            <div className="mt-4 rounded-xl border border-warning/30 bg-warning/10 p-4">
              <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-warning">Release blockers</p>
              <ul className="mt-3 space-y-2 text-xs leading-6 text-warning font-mono">
                {readiness.errors.map((item) => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
            </div>
          ) : null}
        </SectionCard>
        <SectionCard eyebrow="Boundaries" title="Operational limits" description="Honest current-state boundaries for this repository.">
          <ul className="space-y-2 text-sm text-muted">
            <li className="rounded-xl border border-line bg-mist px-4 py-3 font-mono text-xs text-ink">Azure providers are required for deployed staging, production, customer, and reviewer runtimes.</li>
            {canUseLocalTestDoubles ? <li className="rounded-xl border border-line bg-mist px-4 py-3 font-mono text-xs text-ink">Local test doubles are available only in isolated development execution.</li> : null}
            <li className="rounded-xl border border-line bg-mist px-4 py-3 font-mono text-xs text-ink">Missing live provider configuration is reported safely without secrets.</li>
            <li className="rounded-xl border border-line bg-mist px-4 py-3 font-mono text-xs text-ink">Microsoft Graph and Outlook require tenant admin consent, mailbox scoping, and Key Vault-backed configuration.</li>
            <li className="rounded-xl border border-line bg-mist px-4 py-3 font-mono text-xs text-ink">SMS, WhatsApp, and Teams remain unavailable until a real provider is configured.</li>
          </ul>
        </SectionCard>
      </div>
    </motion.div>
  );
}
