"use client";

/** Organisation-wide Reports repository, previous conversations, and platform settings. */

import { useMemo, useState } from "react";
import Link from "next/link";
import { FileText, MessagesSquare, Search, Settings } from "lucide-react";
import { DashboardCard } from "./ui";
import { DashboardSectionHeader } from "./ui";
import { StatusPill } from "./ui";
import { PlatformEmptyState, PlatformErrorState, PlatformLoadingState } from "./ui";
import { formatWhen, humanize } from "@/lib/platform/format";
import type { PlatformConversation, PlatformMe, PlatformReport, PlatformWorkspace } from "@/lib/platform/types";
import { usePlatformMutation, usePlatformQuery } from "@/lib/platform/use-platform";
import { InlineError, inputClass, secondaryButton } from "./ui";

function SearchBox({ value, onChange, label }: { value: string; onChange: (v: string) => void; label: string }) {
  return (
    <div className="relative w-full sm:max-w-xs">
      <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-dls-text-muted" />
      <input className={`${inputClass} pl-9`} placeholder={label} aria-label={label} value={value} onChange={(e) => onChange(e.target.value)} />
    </div>
  );
}

export function PlatformReportsView() {
  const reports = usePlatformQuery<PlatformReport[]>("/reports");
  const [query, setQuery] = useState("");
  const [workspace, setWorkspace] = useState("");
  const rows = useMemo(() => reports.data ?? [], [reports.data]);
  const workspaces = useMemo(() => [...new Set(rows.map((r) => r.workspaceName).filter(Boolean))], [rows]);
  const filtered = rows.filter(
    (r) => (!workspace || r.workspaceName === workspace) && (!query.trim() || `${r.title} ${r.type} ${r.origin}`.toLowerCase().includes(query.trim().toLowerCase()))
  );

  return (
    <div className="space-y-5 pb-10">
      <DashboardSectionHeader
        icon={<FileText className="h-5 w-5" />}
        title="Reports"
        description="Every report across the organisation, with where it came from: the report library, scheduled reports and automation outputs."
      />
      <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
        <SearchBox value={query} onChange={setQuery} label="Search reports" />
        {workspaces.length > 1 ? (
          <select className={`${inputClass} sm:max-w-[220px]`} value={workspace} onChange={(e) => setWorkspace(e.target.value)} aria-label="Workspace">
            <option value="">All workspaces</option>
            {workspaces.map((w) => (
              <option key={w} value={w}>{w}</option>
            ))}
          </select>
        ) : null}
      </div>
      {reports.loading ? (
        <PlatformLoadingState title="Loading reports…" />
      ) : reports.error ? (
        <PlatformErrorState title="Reports could not load" description={reports.error} />
      ) : filtered.length === 0 ? (
        <PlatformEmptyState title="No reports yet" description="Reports from your automations collect here, each with the findings it raised." />
      ) : (
        <DashboardCard padding="none" className="overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full min-w-[640px] text-left text-[13px]">
              <thead className="bg-dls-bg-page text-[11px] uppercase tracking-wide text-dls-text-muted">
                <tr>
                  <th className="px-4 py-2.5 font-semibold">Report</th>
                  <th className="px-4 py-2.5 font-semibold">Workspace</th>
                  <th className="px-4 py-2.5 font-semibold">Source</th>
                  <th className="px-4 py-2.5 font-semibold">Status</th>
                  <th className="px-4 py-2.5 font-semibold">Updated</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((r) => (
                  <tr key={r.id} className="border-t border-dls-border-subtle hover:bg-dls-bg-page">
                    <td className="px-4 py-2.5">
                      {r.href ? (
                        <Link href={r.href} className="font-medium text-dls-text-primary hover:text-dls-primary">{r.title}</Link>
                      ) : (
                        <span className="font-medium text-dls-text-primary">{r.title}</span>
                      )}
                      <span className="block text-[11px] text-dls-text-muted">{humanize(r.type)}</span>
                    </td>
                    <td className="px-4 py-2.5 text-dls-text-secondary">{r.workspaceName}</td>
                    <td className="px-4 py-2.5 text-dls-text-secondary">{r.origin}{r.createdBy ? ` · ${r.createdBy}` : ""}</td>
                    <td className="px-4 py-2.5">
                      <StatusPill tone={["ready", "generated", "succeeded", "completed"].includes(r.status) ? "success" : r.status === "failed" ? "danger" : "neutral"}>{humanize(r.status)}</StatusPill>
                    </td>
                    <td className="whitespace-nowrap px-4 py-2.5 text-dls-text-secondary">{formatWhen(r.generatedAt ?? r.updatedAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </DashboardCard>
      )}
    </div>
  );
}

export function PlatformConversationsView() {
  const conversations = usePlatformQuery<PlatformConversation[]>("/conversations");
  const [query, setQuery] = useState("");
  const rows = (conversations.data ?? []).filter((c) => !query.trim() || c.title.toLowerCase().includes(query.trim().toLowerCase()));

  return (
    <div className="space-y-5 pb-10">
      <DashboardSectionHeader
        icon={<MessagesSquare className="h-5 w-5" />}
        title="Conversations"
        description="Your previous conversations across every workspace. Only you can see your conversations."
      />
      <SearchBox value={query} onChange={setQuery} label="Search conversations" />
      {conversations.loading ? (
        <PlatformLoadingState title="Loading conversations…" />
      ) : conversations.error ? (
        <PlatformErrorState title="Conversations could not load" description={conversations.error} />
      ) : rows.length === 0 ? (
        <PlatformEmptyState title="No conversations yet" description="Ask the Command Center anything and the conversation is kept here." />
      ) : (
        <DashboardCard padding="none" className="divide-y divide-dls-border-subtle overflow-hidden">
          {rows.map((c) => (
            <Link key={c.id} href={c.href} className="flex items-center gap-3 px-4 py-3 transition hover:bg-dls-bg-page">
              <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-dls-bg-page text-dls-text-secondary">
                <MessagesSquare className="h-4 w-4" />
              </span>
              <span className="min-w-0 flex-1">
                <span className="block truncate text-[13px] font-semibold text-dls-text-primary">{c.title}</span>
                <span className="block truncate text-[11px] text-dls-text-muted">{c.workspaceName}</span>
              </span>
              <span className="shrink-0 text-[11px] text-dls-text-muted">{formatWhen(c.lastMessageAt)}</span>
            </Link>
          ))}
        </DashboardCard>
      )}
    </div>
  );
}

export function PlatformSettingsView() {
  const me = usePlatformQuery<PlatformMe>("/me");
  const workspaces = usePlatformQuery<PlatformWorkspace[]>("/workspaces");
  const mutate = usePlatformMutation();
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  if (me.loading) return <PlatformLoadingState title="Loading settings…" />;
  if (!me.data) return <PlatformErrorState title="Settings could not load" description={me.error ?? undefined} />;
  const admin = me.data.isOrgAdmin;

  const setMode = async (mode: "open" | "strict") => {
    setSaving(true);
    setError(null);
    try {
      await mutate("/settings/access", "PUT", { mode });
      await me.reload();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not save.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="mx-auto max-w-4xl space-y-5 pb-10">
      <DashboardSectionHeader icon={<Settings className="h-5 w-5" />} title="Platform settings" description={`How ${me.data.organisationName || "your organisation"} is structured and who can see each workspace.`} />

      <DashboardCard padding="lg">
        <p className="text-[14px] font-semibold text-dls-text-primary">Workspace access</p>
        <p className="mt-1 text-[13px] text-dls-text-secondary">
          Open: everyone in the organisation sees every workspace. Restricted: people see the Insurance Intelligence workspace
          and the workspaces they own or are members of. Administrators always see everything.
        </p>
        <div className="mt-4 grid gap-3 sm:grid-cols-2">
          {(["open", "strict"] as const).map((mode) => {
            const on = me.data!.accessMode === mode;
            return (
              <button
                key={mode}
                type="button"
                disabled={!admin || saving}
                onClick={() => void setMode(mode)}
                className={`rounded-dls-card border p-4 text-left transition disabled:cursor-default ${on ? "border-dls-primary bg-dls-primary-soft" : "border-dls-border-subtle bg-dls-surface hover:bg-dls-bg-page"}`}
              >
                <p className="text-[13px] font-semibold text-dls-text-primary">{mode === "open" ? "Open" : "Restricted"}</p>
                <p className="mt-0.5 text-[12px] text-dls-text-secondary">{mode === "open" ? "Everyone sees all workspaces." : "Members only, per workspace."}</p>
              </button>
            );
          })}
        </div>
        {!admin ? <p className="mt-3 text-[12px] text-dls-text-muted">Only organisation administrators can change this.</p> : null}
        <div className="mt-3"><InlineError message={error} /></div>
      </DashboardCard>

      <DashboardCard padding="lg">
        <p className="text-[14px] font-semibold text-dls-text-primary">Structure</p>
        <p className="mt-1 text-[13px] text-dls-text-secondary">{(workspaces.data ?? []).length} workspaces.</p>
        <ul className="mt-3 divide-y divide-dls-border-subtle">
          {(workspaces.data ?? []).map((w) => (
            <li key={w.id} className="flex items-center justify-between gap-2 py-2 text-[13px]">
              <span className="min-w-0 truncate"><span className="font-medium text-dls-text-primary">{w.name}</span> <span className="text-dls-text-muted">· {w.department}</span></span>
              <span className="shrink-0 text-[11px] text-dls-text-muted">{w.automations} automations</span>
            </li>
          ))}
        </ul>
      </DashboardCard>

      <DashboardCard padding="lg">
        <p className="text-[14px] font-semibold text-dls-text-primary">People and approvals</p>
        <p className="mt-1 text-[13px] text-dls-text-secondary">
          People, roles and seats are managed in People. Claims supervisors and administrators approve automations.
          {me.data.canApproveAutomations ? " You can approve automations." : ""}
        </p>
        <div className="mt-3 flex flex-wrap gap-2">
          <Link href="/app/admin/users" className={secondaryButton}>People</Link>
          <Link href="/app/admin/settings" className={secondaryButton}>Organisation settings</Link>
        </div>
      </DashboardCard>
    </div>
  );
}
