"use client";

import Link from "next/link";
import { ArrowLeft, ArrowRight, Building2, Plus } from "lucide-react";
import { DashboardCard } from "./ui";
import { DashboardSectionHeader } from "./ui";
import { StatusPill } from "./ui";
import { PlatformErrorState, PlatformLoadingState } from "./ui";
import { automationStatus, formatWhen, workspaceStatus } from "@/lib/platform/format";
import type { PlatformAutomation, PlatformNotifications, PlatformWorkspace } from "@/lib/platform/types";
import { usePlatformQuery } from "@/lib/platform/use-platform";
import IntelligenceCards from "./IntelligenceCards";
import IntentStrip from "./IntentStrip";
import { primaryButton, secondaryButton, SectionDivider } from "./ui";

export default function WorkspaceDetail({ id }: { id: string }) {
  const workspace = usePlatformQuery<PlatformWorkspace>(`/workspaces/${id}`);
  const automations = usePlatformQuery<PlatformAutomation[]>(`/automations?workspaceId=${id}`);
  const notifications = usePlatformQuery<PlatformNotifications>(`/notifications?workspaceId=${id}`);

  if (workspace.loading) return <PlatformLoadingState title="Loading workspace…" />;
  if (workspace.error || !workspace.data) return <PlatformErrorState title="Workspace not found" description={workspace.error ?? undefined} />;
  const w = workspace.data;
  const s = workspaceStatus(w.status);
  const connected = w.connectors.filter((c) => c.status === "connected").length;

  return (
    <div className="mx-auto max-w-6xl space-y-6 pb-10">
      <Link href="/app/platform/workspaces" className="inline-flex items-center gap-1 text-[12px] font-semibold text-dls-text-secondary hover:text-dls-text-primary">
        <ArrowLeft className="h-3.5 w-3.5" /> Workspaces
      </Link>
      <DashboardSectionHeader
        icon={<Building2 className="h-5 w-5" />}
        eyebrow={`${w.department} · ${w.templateName}`}
        title={w.name}
        description={w.purpose ?? undefined}
        actions={
          <>
            <StatusPill tone={s.tone} dot>{s.label}</StatusPill>
            {w.isPrimary && w.route ? (
              <Link href={w.route} className={primaryButton}>Open workspace <ArrowRight className="h-4 w-4" /></Link>
            ) : null}
          </>
        }
      />

      {w.status === "awaiting_data" ? (
        <DashboardCard padding="md" tone="accent">
          <p className="text-[13px] font-semibold text-dls-text-primary">Connect data to bring this workspace to life</p>
          <p className="mt-0.5 text-[12px] text-dls-text-secondary">
            {connected} of {w.connectors.length} sources are connected. Its automations can be tested now against the data IIP already holds.
          </p>
          <div className="mt-3 flex flex-wrap gap-2">
            <Link href="/app/integrations" className={secondaryButton}>Integrations</Link>
            <Link href="/app/knowledge" className={secondaryButton}>Knowledge</Link>
          </div>
        </DashboardCard>
      ) : null}

      {notifications.data && notifications.data.cards.length > 0 ? (
        <div>
          <SectionDivider>Intelligence</SectionDivider>
          <IntelligenceCards cards={notifications.data.cards} />
        </div>
      ) : null}
      {notifications.data ? <IntentStrip intents={notifications.data.intents} /> : null}

      <div className="grid gap-4 lg:grid-cols-3">
        <DashboardCard padding="lg">
          <p className="text-[13px] font-semibold text-dls-text-primary">KPIs</p>
          {w.kpis.length === 0 ? (
            <p className="mt-2 text-[12px] text-dls-text-muted">None set.</p>
          ) : (
            <ul className="mt-2 space-y-1.5">
              {w.kpis.map((k) => (
                <li key={k} className="text-[13px] text-dls-text-secondary">{k}</li>
              ))}
            </ul>
          )}
        </DashboardCard>
        <DashboardCard padding="lg">
          <p className="text-[13px] font-semibold text-dls-text-primary">Data sources</p>
          <ul className="mt-2 space-y-1.5">
            {w.connectors.map((c) => (
              <li key={c.key} className="flex items-center justify-between gap-2 text-[13px] text-dls-text-secondary">
                <span className="truncate">{c.name}</span>
                <StatusPill tone={c.status === "connected" ? "success" : "neutral"} dot>{c.status === "connected" ? "Connected" : "Not connected"}</StatusPill>
              </li>
            ))}
          </ul>
        </DashboardCard>
        <DashboardCard padding="lg">
          <div className="flex items-center justify-between gap-2">
            <p className="text-[13px] font-semibold text-dls-text-primary">Automations</p>
            <Link href={`/app/platform/automations/new?workspaceId=${w.id}`} className="inline-flex items-center gap-1 text-[12px] font-semibold text-dls-primary hover:underline">
              <Plus className="h-3.5 w-3.5" /> Add
            </Link>
          </div>
          {(automations.data ?? []).length === 0 ? (
            <p className="mt-2 text-[12px] text-dls-text-muted">No automations yet.</p>
          ) : (
            <ul className="mt-2 space-y-2">
              {(automations.data ?? []).map((a) => {
                const st = automationStatus(a.status);
                return (
                  <li key={a.id}>
                    <Link href={`/app/platform/automations/${a.id}`} className="flex items-center justify-between gap-2 rounded-dls-control px-1 py-1 text-[13px] hover:bg-dls-bg-page">
                      <span className="min-w-0">
                        <span className="block truncate font-medium text-dls-text-primary">{a.name}</span>
                        <span className="block truncate text-[11px] text-dls-text-muted">Last run {formatWhen(a.lastRunAt)}</span>
                      </span>
                      <StatusPill tone={st.tone} dot className="shrink-0">{st.label}</StatusPill>
                    </Link>
                  </li>
                );
              })}
            </ul>
          )}
        </DashboardCard>
      </div>
    </div>
  );
}
