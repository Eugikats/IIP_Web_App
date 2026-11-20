"use client";

/**
 * Automations studio: the automations list, the Intelligence Inbox and Intents. Used for the whole organisation at
 * /app/platform/automations.
 */

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { Bot, Plus } from "lucide-react";
import { DashboardCard } from "./ui";
import { DashboardSectionHeader } from "./ui";
import { StatusPill } from "./ui";
import { PlatformEmptyState, PlatformErrorState, PlatformLoadingState } from "./ui";
import { automationStatus, formatWhen, scheduleLabel } from "@/lib/platform/format";
import type { InboxCounts, PlatformAutomation } from "@/lib/platform/types";
import { usePlatformQuery } from "@/lib/platform/use-platform";
import InboxList from "./InboxList";
import IntentsPanel from "./IntentsPanel";
import { primaryButton, SegmentedTabs } from "./ui";

type Tab = "automations" | "inbox" | "intents";

export default function AutomationsView({
  workspaceId,
  basePath,
  title = "Automations",
  description = "Automations watch your data on a schedule and raise what needs a person in the Intelligence Inbox. Each one is tested and approved before it runs.",
}: {
  workspaceId?: string | null;
  basePath: string;
  title?: string;
  description?: string;
}) {
  const router = useRouter();
  const pathname = usePathname() ?? basePath;
  const searchParams = useSearchParams();
  const tab = ((searchParams?.get("tab") as Tab) || "automations") as Tab;
  const status = searchParams?.get("status") ?? "";
  const ws = workspaceId ? `?workspaceId=${workspaceId}` : "";
  const automations = usePlatformQuery<PlatformAutomation[]>(`/automations${ws}`);
  const counts = usePlatformQuery<InboxCounts>(`/inbox/counts${ws}`);

  const setParams = (next: Record<string, string | null>) => {
    const params = new URLSearchParams(searchParams?.toString() ?? "");
    for (const [k, v] of Object.entries(next)) {
      if (v) params.set(k, v);
      else params.delete(k);
    }
    const q = params.toString();
    router.replace(`${pathname}${q ? `?${q}` : ""}`, { scroll: false });
  };

  const list = automations.data ?? [];
  const newHref = `/app/platform/automations/new${workspaceId ? `?workspaceId=${workspaceId}` : ""}`;

  return (
    <div className="space-y-5 pb-10">
      <DashboardSectionHeader
        icon={<Bot className="h-5 w-5" />}
        title={title}
        description={description}
        actions={
          <Link href={newHref} className={primaryButton}>
            <Plus className="h-4 w-4" /> New automation
          </Link>
        }
      />

      <SegmentedTabs<Tab>
        label="Automation views"
        value={tab}
        onChange={(k) => setParams({ tab: k === "automations" ? null : k, status: null })}
        tabs={[
          { key: "automations", label: "Automations", count: list.length },
          { key: "inbox", label: "Intelligence Inbox", count: counts.data?.total },
          { key: "intents", label: "Intents" },
        ]}
      />

      {tab === "inbox" ? (
        <InboxList workspaceId={workspaceId} status={status} onStatusChange={(s) => setParams({ status: s || null })} />
      ) : tab === "intents" ? (
        <IntentsPanel workspaceId={workspaceId} />
      ) : automations.loading ? (
        <PlatformLoadingState title="Loading automations…" />
      ) : automations.error ? (
        <PlatformErrorState title="Automations could not load" description={automations.error} />
      ) : list.length === 0 ? (
        <PlatformEmptyState
          title="No automations yet"
          description="Start from a template such as “Claims with no movement” or “Claims past their service deadline”. Nothing runs until it has been tested and approved."
          action={<Link href={newHref} className={primaryButton}><Plus className="h-4 w-4" /> New automation</Link>}
        />
      ) : (
        <div className="grid gap-3 lg:grid-cols-2">
          {list.map((a) => {
            const s = automationStatus(a.status);
            return (
              <Link key={a.id} href={`/app/platform/automations/${a.id}`} className="block">
                <DashboardCard interactive padding="md" className="h-full">
                  <div className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <p className="truncate text-[14px] font-semibold text-dls-text-primary">{a.name}</p>
                      <p className="truncate text-[12px] text-dls-text-secondary">
                        {a.templateName} · {a.workspaceName} · v{a.version}
                      </p>
                    </div>
                    <StatusPill tone={s.tone} dot className="shrink-0">{s.label}</StatusPill>
                  </div>
                  {a.objective ? <p className="mt-2 line-clamp-2 text-[13px] text-dls-text-secondary">{a.objective}</p> : null}
                  <div className="mt-3 flex flex-wrap gap-x-4 gap-y-1 text-[11px] text-dls-text-muted">
                    <span>{scheduleLabel(a.trigger)}</span>
                    <span>Last run {formatWhen(a.lastRunAt)}</span>
                    {a.status === "active" && a.nextRunAt ? <span>Next {formatWhen(a.nextRunAt)}</span> : null}
                  </div>
                  {a.lastRunMessage ? <p className="mt-1 truncate text-[12px] text-dls-text-secondary">{a.lastRunMessage}</p> : null}
                </DashboardCard>
              </Link>
            );
          })}
        </div>
      )}
    </div>
  );
}
