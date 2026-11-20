"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { Building2, LayoutGrid, Plus, Search } from "lucide-react";
import { DashboardCard } from "./ui";
import { DashboardSectionHeader } from "./ui";
import { StatusPill } from "./ui";
import { PlatformEmptyState, PlatformErrorState, PlatformLoadingState } from "./ui";
import { groupBy, workspaceStatus } from "@/lib/platform/format";
import type { PlatformMe, PlatformWorkspace } from "@/lib/platform/types";
import { usePlatformQuery } from "@/lib/platform/use-platform";
import { inputClass, primaryButton } from "./ui";

export function workspaceHref(w: PlatformWorkspace): string {
  return w.isPrimary && w.route ? w.route : `/app/platform/workspaces/${w.id}`;
}

export default function WorkspacesDirectory() {
  const me = usePlatformQuery<PlatformMe>("/me");
  const workspaces = usePlatformQuery<PlatformWorkspace[]>("/workspaces");
  const [query, setQuery] = useState("");
  const [status, setStatus] = useState<string>("");

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    return (workspaces.data ?? []).filter(
      (w) =>
        (!status || w.status === status) &&
        (!q || [w.name, w.department, w.purpose, w.templateName].some((v) => (v ?? "").toLowerCase().includes(q)))
    );
  }, [workspaces.data, query, status]);

  return (
    <div className="space-y-5 pb-10">
      <DashboardSectionHeader
        icon={<LayoutGrid className="h-5 w-5" />}
        title="Workspaces"
        description="Each department works in its own workspace, built from a template. Insurance Intelligence is your live workspace; new ones start with their data connections and suggested automations."
        actions={
          me.data?.isOrgAdmin ? (
            <Link href="/app/platform/workspaces/new" className={primaryButton}>
              <Plus className="h-4 w-4" /> New workspace
            </Link>
          ) : null
        }
      />

      <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
        <div className="relative w-full sm:max-w-xs">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-dls-text-muted" />
          <input className={`${inputClass} pl-9`} placeholder="Search workspaces" value={query} onChange={(e) => setQuery(e.target.value)} aria-label="Search workspaces" />
        </div>
        <div className="flex flex-wrap gap-1.5">
          {[
            ["", "All"],
            ["live", "Live"],
            ["awaiting_data", "Awaiting data"],
            ["draft", "Draft"],
          ].map(([key, label]) => (
            <button
              key={key}
              type="button"
              onClick={() => setStatus(key)}
              className={`rounded-full border px-3 py-1.5 text-[12px] font-medium transition ${status === key ? "border-dls-primary bg-dls-primary-soft text-dls-primary" : "border-dls-border-subtle bg-dls-surface text-dls-text-secondary hover:bg-dls-bg-page"}`}
            >
              {label}
            </button>
          ))}
        </div>
      </div>

      {workspaces.loading ? (
        <PlatformLoadingState title="Loading workspaces…" />
      ) : workspaces.error ? (
        <PlatformErrorState title="Workspaces could not load" description={workspaces.error} />
      ) : filtered.length === 0 ? (
        <PlatformEmptyState title="No workspaces match" description="Try another search or status." />
      ) : (
        groupBy(filtered, (w) => w.department || "Organisation").map(([department, items]) => (
          <section key={department}>
            <p className="mb-2 text-[11px] font-semibold uppercase tracking-[0.12em] text-dls-text-muted">{department}</p>
            <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
              {items.map((w) => {
                const s = workspaceStatus(w.status);
                return (
                  <Link key={w.id} href={workspaceHref(w)} className="block">
                    <DashboardCard interactive padding="md" className="h-full">
                      <div className="flex items-start gap-3">
                        <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-dls-card border border-dls-border-subtle bg-dls-bg-page text-dls-text-secondary">
                          {w.isPrimary ? (
                            // eslint-disable-next-line @next/next/no-img-element
                            <img src="/iip-mark.png" alt="" className="h-6 w-6 object-contain" />
                          ) : (
                            <Building2 className="h-5 w-5" />
                          )}
                        </span>
                        <div className="min-w-0 flex-1">
                          <div className="flex items-start justify-between gap-2">
                            <p className="truncate text-[14px] font-semibold text-dls-text-primary">{w.name}</p>
                            <StatusPill tone={s.tone} dot className="shrink-0">{s.label}</StatusPill>
                          </div>
                          <p className="truncate text-[12px] text-dls-text-secondary">{w.templateName}</p>
                        </div>
                      </div>
                      {w.purpose ? <p className="mt-3 line-clamp-2 text-[13px] text-dls-text-secondary">{w.purpose}</p> : null}
                      <div className="mt-3 flex flex-wrap gap-x-4 gap-y-1 text-[11px] text-dls-text-muted">
                        <span>{w.automations} automation{w.automations === 1 ? "" : "s"}</span>
                        <span>{w.openInboxItems} open signal{w.openInboxItems === 1 ? "" : "s"}</span>
                        <span>{w.connectors.filter((c) => c.status === "connected").length}/{w.connectors.length} sources connected</span>
                      </div>
                    </DashboardCard>
                  </Link>
                );
              })}
            </div>
          </section>
        ))
      )}
    </div>
  );
}
