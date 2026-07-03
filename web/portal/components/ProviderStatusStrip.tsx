"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { Bell, Bot, ChevronRight, FileSearch, Globe, Mail, MessageSquare, Search, ShieldCheck, Users } from "lucide-react";
import { useRole } from "./RoleContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { Server, Shield } from "lucide-react";

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

type SystemCapabilities = {
  activeAi: boolean;
  activeDocumentExtraction: boolean;
  activeKnowledgeRetrieval: boolean;
  activeEmailIngestion: boolean;
  activeSmsDelivery: boolean;
  activeTeamsNotification: boolean;
  activeChannels: boolean;
  activeOnboarding: boolean;
  environment: string;
  authMode: string;
};

const displayProviders = [
  { name: "graphMail", label: "Outlook / Graph", icon: Mail },
  { name: "communications", label: "Customer Comms", icon: MessageSquare },
  { name: "notifications", label: "Teams", icon: ShieldCheck },
  { name: "azureOpenAI", label: "Azure OpenAI", icon: Bot },
  { name: "aiSearch", label: "AI Search", icon: Search },
  { name: "documentIntelligence", label: "Document Intelligence", icon: FileSearch }
] as const;

const capabilityLedger = [
  { key: "activeAi" as const, label: "AI Engine", icon: Bot },
  { key: "activeDocumentExtraction" as const, label: "Doc Intel", icon: FileSearch },
  { key: "activeKnowledgeRetrieval" as const, label: "Knowledge RAG", icon: Search },
  { key: "activeEmailIngestion" as const, label: "Email", icon: Mail },
  { key: "activeSmsDelivery" as const, label: "Comms", icon: MessageSquare },
  { key: "activeTeamsNotification" as const, label: "Teams", icon: Bell },
  { key: "activeChannels" as const, label: "Channels", icon: Globe },
  { key: "activeOnboarding" as const, label: "Onboarding", icon: Users },
];

export function ProviderStatusStrip() {
  const { role, userId, can, isLoadingContext } = useRole();
  const [health, setHealth] = useState<ProviderHealth | null>(null);
  const [capabilities, setCapabilities] = useState<SystemCapabilities | null>(null);

  useEffect(() => {
    let active = true;

    if (isLoadingContext || !can("ViewCommandCenter")) {
      setHealth(null);
      setCapabilities(null);
      return () => {
        active = false;
      };
    }

    fetch(`${apiBaseUrl}/api/v1/provider-status`, {
      cache: "no-store",
      headers: scopedHeaders(role, userId)
    })
      .then((response) => (response.ok ? response.json() : null))
      .then((data) => {
        if (active) setHealth(data as ProviderHealth | null);
      })
      .catch(() => {
        if (active) setHealth(null);
      });

    fetch(`${apiBaseUrl}/api/v1/system/capabilities`, {
      cache: "no-store",
      headers: scopedHeaders(role, userId)
    })
      .then((response) => (response.ok ? response.json() : null))
      .then((data) => {
        if (active) setCapabilities(data as SystemCapabilities | null);
      })
      .catch(() => {
        if (active) setCapabilities(null);
      });

    return () => {
      active = false;
    };
  }, [can, isLoadingContext, role, userId]);

  const items = useMemo(() => {
    const byName = new Map(health?.providers.map((provider) => [provider.name, provider]));
    return displayProviders.map((provider) => ({
      ...provider,
      health: byName.get(provider.name)
    }));
  }, [health]);

  if (!can("ViewCommandCenter")) {
    return null;
  }

  if (!health && !capabilities) {
    return null;
  }

  return (
    <section className="border-b border-line bg-surface/90" aria-label="Provider status">
      <div className="mx-auto flex max-w-[1600px] items-center gap-2 overflow-x-auto px-3 py-2 custom-scrollbar sm:px-4 lg:px-6">
        <div className="flex min-w-max flex-1 items-center gap-2">
          {capabilities && (
            <div
              className="inline-flex min-h-9 min-w-max items-center gap-2 rounded-lg border border-line bg-shell px-3 py-1.5 text-xs text-ink"
              title="System Profile"
            >
              <Server className="h-3.5 w-3.5 text-accent" />
              <span className="font-semibold">Env:</span>
              <span className="rounded-md bg-surface-soft px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide text-ink">
                {capabilities.environment}
              </span>
              <Shield className="ml-1 h-3.5 w-3.5 text-accent" />
              <span className="font-semibold">Auth:</span>
              <span className="rounded-md bg-surface-soft px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide text-ink">
                {capabilities.authMode}
              </span>
            </div>
          )}

          {capabilities && (
            <>
              <div className="mx-1 h-6 w-px bg-line" aria-hidden="true" />
              <span className="text-[10px] font-bold uppercase tracking-widest text-muted">Capabilities</span>
              {capabilityLedger.map(({ key, label, icon: Icon }) => {
                const active = capabilities[key];
                return (
                  <div
                    key={key}
                    className="inline-flex min-h-9 min-w-max items-center gap-1.5 rounded-lg border border-line bg-shell px-2.5 py-1.5 text-xs text-ink"
                    aria-label={`${label}: ${active ? "active" : "not configured"}`}
                    title={`${label}: ${active ? "active" : "not configured"}`}
                  >
                    <Icon className={`h-3.5 w-3.5 ${active ? "text-success" : "text-muted"}`} />
                    <span className="font-semibold">{label}</span>
                    <span className={`rounded-md px-1.5 py-0.5 text-[10px] font-bold uppercase tracking-wide ${active ? "bg-success-soft text-success" : "bg-surface-soft text-muted"}`}>
                      {active ? "On" : "Off"}
                    </span>
                  </div>
                );
              })}
            </>
          )}

          {health && items.length > 0 && (
            <>
              <div className="mx-1 h-6 w-px bg-line" aria-hidden="true" />
              <span className="text-[10px] font-bold uppercase tracking-widest text-muted">Providers</span>
              {items.map((item) => {
                const Icon = item.icon;
                const status = item.health?.status ?? "not configured";
                return (
                  <div
                    key={item.name}
                    className="inline-flex min-h-9 min-w-max items-center gap-2 rounded-lg border border-line bg-shell px-3 py-1.5 text-xs text-ink"
                    title={item.health?.detail ?? "Provider status is unavailable."}
                    aria-label={`${item.label}: ${statusLabel(status)}`}
                  >
                    <Icon className="h-3.5 w-3.5 text-accent" />
                    <span className="font-semibold">{item.label}</span>
                    <span className={`rounded-md px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide ${statusClassName(status)}`}>
                      {statusLabel(status)}
                    </span>
                  </div>
                );
              })}
            </>
          )}
        </div>
        <Link className="inline-flex min-w-max items-center gap-1 rounded-lg px-2 py-1 text-xs font-semibold text-muted transition-colors hover:bg-surface-soft hover:text-ink" href="/support-health" aria-label="Open all provider health details">
          All providers
          <ChevronRight className="h-3.5 w-3.5" />
        </Link>
      </div>
    </section>
  );
}

function statusLabel(status: string) {
  if (status === "configured" || status === "healthy") return "Connected";
  if (status === "missing" || status === "not_configured") return "Not configured";
  return status;
}

function statusClassName(status: string) {
  if (status === "configured" || status === "healthy") {
    return "bg-success-soft text-success";
  }

  if (status === "missing" || status === "not_configured") {
    return "bg-danger-soft text-danger";
  }

  return "bg-warning-soft text-warning";
}
