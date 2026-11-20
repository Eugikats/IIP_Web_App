"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { ArrowLeft, Bot, Check, Pencil } from "lucide-react";
import { DashboardCard } from "./ui";
import { DashboardSectionHeader } from "./ui";
import { StatusPill } from "./ui";
import { PlatformErrorState, PlatformLoadingState } from "./ui";
import { actionLabel, automationStatus, formatWhen, humanize, LIFECYCLE, lifecycleIndex, scheduleLabel, severityTone } from "@/lib/platform/format";
import type { PlatformAutomation, PlatformRun, PlatformTemplatesResponse } from "@/lib/platform/types";
import { usePlatformMutation, usePlatformQuery } from "@/lib/platform/use-platform";
import AutomationEditor, { draftToRequest, type AutomationDraft } from "./AutomationEditor";
import { Button, InlineError, KeyValue, SectionDivider } from "./ui";

type Detail = { automation: PlatformAutomation; runs: PlatformRun[] };

function toDraft(a: PlatformAutomation): AutomationDraft {
  return {
    name: a.name,
    objective: a.objective ?? "",
    logic: { ...a.logic },
    trigger: { type: a.trigger.type ?? "schedule", schedule: a.trigger.schedule ?? "daily", time: a.trigger.time ?? "07:00" },
    maxItemsPerRun: a.policy.maxItemsPerRun ?? 25,
  };
}

export default function AutomationDetail({ id }: { id: string }) {
  const detail = usePlatformQuery<Detail>(`/automations/${id}`);
  const templates = usePlatformQuery<PlatformTemplatesResponse>("/templates");
  const mutate = usePlatformMutation();
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [lastRun, setLastRun] = useState<PlatformRun | null>(null);
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState<AutomationDraft | null>(null);

  const a = detail.data?.automation;
  useEffect(() => {
    if (a && !editing) setDraft(toDraft(a));
  }, [a, editing]);

  if (detail.loading) return <PlatformLoadingState title="Loading automation…" />;
  if (detail.error || !a) return <PlatformErrorState title="Automation not found" description={detail.error ?? undefined} />;

  const template = templates.data?.automations.find((t) => t.key === a.templateKey);
  const status = automationStatus(a.status);
  const step = lifecycleIndex(a.status);

  const act = async (action: string) => {
    setBusy(action);
    setError(null);
    try {
      const result = await mutate<{ run?: PlatformRun }>(`/automations/${a.id}/${action}`, "POST");
      if (result.run) setLastRun(result.run);
    } catch (err) {
      setError(err instanceof Error ? err.message : "That did not work.");
    } finally {
      setBusy(null);
    }
  };

  const save = async () => {
    if (!draft) return;
    setBusy("save");
    setError(null);
    try {
      await mutate(`/automations/${a.id}`, "PATCH", draftToRequest(draft));
      setEditing(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not save.");
    } finally {
      setBusy(null);
    }
  };

  const runs = detail.data?.runs ?? [];
  const shownRun = lastRun ?? runs[0] ?? null;

  return (
    <div className="mx-auto max-w-5xl space-y-5 pb-10">
      <Link href="/app/platform/automations" className="inline-flex items-center gap-1 text-[12px] font-semibold text-dls-text-secondary hover:text-dls-text-primary">
        <ArrowLeft className="h-3.5 w-3.5" /> Automations
      </Link>
      <DashboardSectionHeader
        icon={<Bot className="h-5 w-5" />}
        eyebrow={`${a.workspaceName} · ${a.templateName} · version ${a.version}`}
        title={a.name}
        description={a.objective ?? undefined}
        actions={<StatusPill tone={status.tone} dot>{status.label}</StatusPill>}
      />

      <DashboardCard padding="lg">
        <ol className="grid grid-cols-5 gap-2" aria-label="Lifecycle">
          {LIFECYCLE.map((s, i) => (
            <li key={s} className="min-w-0">
              <div className={`h-1.5 rounded-full ${i <= step ? (a.status === "failed" && i === LIFECYCLE.length - 1 ? "bg-dls-danger-dot" : "bg-dls-primary") : "bg-dls-bg-page"}`} />
              <p className={`mt-1.5 truncate text-[11px] font-semibold ${i <= step ? "text-dls-text-primary" : "text-dls-text-muted"}`}>{automationStatus(s).label}</p>
            </li>
          ))}
        </ol>
        <div className="mt-5 flex flex-wrap items-center gap-2">
          {a.allowedActions.map((action) => (
            <Button
              key={action}
              variant={action === "retire" ? "danger" : ["test", "submit", "approve", "activate", "run", "resume"].includes(action) ? "primary" : "secondary"}
              disabled={busy !== null || editing}
              onClick={() => void act(action)}
            >
              {busy === action ? "Working…" : actionLabel(action)}
            </Button>
          ))}
          {!editing && a.status !== "retired" ? (
            <Button variant="secondary" onClick={() => setEditing(true)}>
              <Pencil className="h-3.5 w-3.5" /> Edit
            </Button>
          ) : null}
        </div>
        {!a.testedCurrentVersion && a.status === "draft" ? (
          <p className="mt-3 text-[12px] text-dls-text-secondary">Run a test to see what it would raise. A test reads your data and changes nothing.</p>
        ) : null}
        {a.status === "awaiting_approval" && !a.allowedActions.includes("approve") ? (
          <p className="mt-3 text-[12px] text-dls-text-secondary">Waiting for a supervisor or administrator to approve it.</p>
        ) : null}
        {!a.hasRunner ? (
          <p className="mt-3 text-[12px] text-dls-text-secondary">This custom automation needs its data and logic set up with your administrator before it can be activated.</p>
        ) : null}
        <div className="mt-3"><InlineError message={error} /></div>
      </DashboardCard>

      {shownRun ? (
        <DashboardCard padding="lg" tone={shownRun.kind === "test" ? "accent" : "default"}>
          <div className="flex flex-wrap items-center gap-2">
            <p className="text-[13px] font-semibold text-dls-text-primary">{shownRun.kind === "test" ? "Test result" : "Latest run"}</p>
            <StatusPill tone={shownRun.status === "succeeded" ? "success" : shownRun.status === "failed" ? "danger" : "warning"}>{humanize(shownRun.status)}</StatusPill>
            <span className="text-[11px] text-dls-text-muted">{formatWhen(shownRun.startedAt)} · v{shownRun.automationVersion}</span>
          </div>
          <p className="mt-2 text-[13px] text-dls-text-secondary">{shownRun.summary}</p>
          {(shownRun.evidence.findings ?? []).length > 0 ? (
            <ul className="mt-3 space-y-1.5">
              {(shownRun.evidence.findings ?? []).slice(0, 10).map((f) => (
                <li key={f.key} className="flex items-center gap-2 text-[13px] text-dls-text-primary">
                  <StatusPill tone={severityTone(f.severity)} dot>{humanize(f.severity)}</StatusPill>
                  <span className="min-w-0 truncate">{f.title}</span>
                </li>
              ))}
            </ul>
          ) : null}
          {shownRun.kind === "test" && shownRun.findings > 0 ? (
            <p className="mt-3 text-[12px] text-dls-text-secondary">When active, these would go to the Intelligence Inbox.</p>
          ) : null}
        </DashboardCard>
      ) : null}

      <DashboardCard padding="lg">
        {editing && draft ? (
          <>
            <AutomationEditor template={template} draft={draft} onChange={setDraft} />
            <p className="mt-4 text-[12px] text-dls-text-secondary">Changing what it watches or when it runs makes a new version that is tested and approved again.</p>
            <div className="mt-3 flex gap-2">
              <Button disabled={busy !== null} onClick={() => void save()}><Check className="h-4 w-4" /> Save</Button>
              <Button variant="secondary" onClick={() => setEditing(false)}>Cancel</Button>
            </div>
          </>
        ) : (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <KeyValue label="Schedule">{scheduleLabel(a.trigger)}</KeyValue>
            <KeyValue label="Next run">{a.status === "active" ? formatWhen(a.nextRunAt) : "Not scheduled"}</KeyValue>
            <KeyValue label="Last run">{formatWhen(a.lastRunAt)}</KeyValue>
            <KeyValue label="Approved">{a.approvedAt ? formatWhen(a.approvedAt) : "Not yet"}</KeyValue>
            {(template?.fields ?? []).map((f) => (
              <KeyValue key={f.key} label={f.label}>{String(a.logic[f.key] ?? "Not set")} {f.unit ?? ""}</KeyValue>
            ))}
            <KeyValue label="Output">Intelligence Inbox</KeyValue>
            <KeyValue label="Most items per run">{a.policy.maxItemsPerRun ?? 25}</KeyValue>
          </div>
        )}
      </DashboardCard>

      <div>
        <SectionDivider>Run history</SectionDivider>
        {runs.length === 0 ? (
          <p className="text-center text-[13px] text-dls-text-muted">No runs yet.</p>
        ) : (
          <DashboardCard padding="none" className="overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full min-w-[560px] text-left text-[13px]">
                <thead className="bg-dls-bg-page text-[11px] uppercase tracking-wide text-dls-text-muted">
                  <tr>
                    <th className="px-4 py-2 font-semibold">When</th>
                    <th className="px-4 py-2 font-semibold">Kind</th>
                    <th className="px-4 py-2 font-semibold">Status</th>
                    <th className="px-4 py-2 font-semibold">Findings</th>
                    <th className="px-4 py-2 font-semibold">Summary</th>
                  </tr>
                </thead>
                <tbody>
                  {runs.map((r) => (
                    <tr key={r.id} className="border-t border-dls-border-subtle">
                      <td className="whitespace-nowrap px-4 py-2 text-dls-text-secondary">{formatWhen(r.startedAt)}</td>
                      <td className="px-4 py-2">{r.kind === "test" ? "Test" : r.triggeredBy === "schedule" ? "Scheduled" : "Manual"} · v{r.automationVersion}</td>
                      <td className="px-4 py-2"><StatusPill tone={r.status === "succeeded" ? "success" : r.status === "failed" ? "danger" : "warning"}>{humanize(r.status)}</StatusPill></td>
                      <td className="px-4 py-2">{r.findings}{r.kind === "run" ? ` (${r.newFindings} new)` : ""}</td>
                      <td className="max-w-[320px] truncate px-4 py-2 text-dls-text-secondary">{r.summary}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </DashboardCard>
        )}
      </div>
    </div>
  );
}
