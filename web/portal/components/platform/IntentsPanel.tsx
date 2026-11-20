"use client";

import { useState } from "react";
import { Plus } from "lucide-react";
import { DashboardCard } from "./ui";
import { StatusPill } from "./ui";
import { PlatformEmptyState, PlatformLoadingState } from "./ui";
import { formatWhen, intentHealth } from "@/lib/platform/format";
import type { PlatformAutomation, PlatformIntent } from "@/lib/platform/types";
import { usePlatformMutation, usePlatformQuery } from "@/lib/platform/use-platform";
import { Button, InlineError, inputClass, labelClass } from "./ui";

export default function IntentsPanel({ workspaceId }: { workspaceId?: string | null }) {
  const intents = usePlatformQuery<PlatformIntent[]>("/intents");
  const automations = usePlatformQuery<PlatformAutomation[]>(`/automations${workspaceId ? `?workspaceId=${workspaceId}` : ""}`);
  const mutate = usePlatformMutation();
  const [form, setForm] = useState<{ name: string; measure: string; target: string; description: string; automationIds: string[] } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const list = (intents.data ?? []).filter((i) => !workspaceId || !i.workspaceId || i.workspaceId === workspaceId);
  const names = new Map((automations.data ?? []).map((a) => [a.id, a.name]));

  const save = async () => {
    if (!form) return;
    setSaving(true);
    setError(null);
    try {
      await mutate("/intents", "POST", { ...form, workspaceId: workspaceId ?? null });
      setForm(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not save the intent.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="max-w-2xl text-[13px] text-dls-text-secondary">
          An intent is a goal the organisation is working towards. Link the automations that watch it and IIP shows
          whether it is on track from their open signals.
        </p>
        {!form ? (
          <Button onClick={() => setForm({ name: "", measure: "", target: "", description: "", automationIds: [] })}>
            <Plus className="h-4 w-4" /> New intent
          </Button>
        ) : null}
      </div>

      {form ? (
        <DashboardCard padding="lg" tone="accent">
          <div className="grid gap-3 md:grid-cols-2">
            <div className="md:col-span-2">
              <label className={labelClass} htmlFor="intent-name">Intent</label>
              <input id="intent-name" className={inputClass} placeholder="e.g. Keep PAR30 below 5%" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
            </div>
            <div>
              <label className={labelClass} htmlFor="intent-measure">Measure</label>
              <input id="intent-measure" className={inputClass} placeholder="e.g. PAR30" value={form.measure} onChange={(e) => setForm({ ...form, measure: e.target.value })} />
            </div>
            <div>
              <label className={labelClass} htmlFor="intent-target">Target</label>
              <input id="intent-target" className={inputClass} placeholder="e.g. Below 5% by Q1" value={form.target} onChange={(e) => setForm({ ...form, target: e.target.value })} />
            </div>
            <div className="md:col-span-2">
              <p className={labelClass}>Automations that watch it</p>
              {(automations.data ?? []).length === 0 ? (
                <p className="text-[12px] text-dls-text-secondary">No automations yet. You can link them later.</p>
              ) : (
                <div className="flex flex-wrap gap-2">
                  {(automations.data ?? []).map((a) => {
                    const on = form.automationIds.includes(a.id);
                    return (
                      <button
                        key={a.id}
                        type="button"
                        onClick={() => setForm({ ...form, automationIds: on ? form.automationIds.filter((x) => x !== a.id) : [...form.automationIds, a.id] })}
                        className={`rounded-full border px-3 py-1.5 text-[12px] font-medium transition ${on ? "border-dls-primary bg-dls-primary-soft text-dls-primary" : "border-dls-border-subtle bg-dls-surface text-dls-text-secondary hover:bg-dls-bg-page"}`}
                      >
                        {a.name}
                      </button>
                    );
                  })}
                </div>
              )}
            </div>
          </div>
          <div className="mt-4 flex flex-wrap items-center gap-2">
            <Button disabled={saving || !form.name.trim()} onClick={() => void save()}>{saving ? "Saving…" : "Save intent"}</Button>
            <Button variant="secondary" onClick={() => setForm(null)}>Cancel</Button>
          </div>
          <div className="mt-3"><InlineError message={error} /></div>
        </DashboardCard>
      ) : null}

      {intents.loading ? (
        <PlatformLoadingState title="Loading intents…" />
      ) : list.length === 0 ? (
        <PlatformEmptyState title="No intents yet" description="Set a goal such as “Decide every case within 5 days” and link the automations that track it." />
      ) : (
        <div className="grid gap-3 md:grid-cols-2">
          {list.map((intent) => {
            const health = intentHealth(intent.health);
            return (
              <DashboardCard key={intent.id} padding="md">
                <div className="flex items-start justify-between gap-2">
                  <p className="text-[14px] font-semibold text-dls-text-primary">{intent.name}</p>
                  <StatusPill tone={health.tone} dot>{health.label}</StatusPill>
                </div>
                {intent.measure || intent.target ? (
                  <p className="mt-0.5 text-[12px] text-dls-text-secondary">{[intent.measure, intent.target].filter(Boolean).join(" · ")}</p>
                ) : null}
                <div className="mt-3 h-1.5 w-full overflow-hidden rounded-full bg-dls-bg-page">
                  <div className={`h-full rounded-full ${health.bar}`} style={{ width: intent.health === "on_track" ? "100%" : intent.health === "at_risk" ? "60%" : "25%" }} />
                </div>
                <p className="mt-2 text-[12px] text-dls-text-secondary">
                  {intent.openItems === 0
                    ? "No open signals."
                    : `${intent.openItems} open signal${intent.openItems === 1 ? "" : "s"}${intent.highItems ? `, ${intent.highItems} high` : ""}. Latest: ${intent.latestSignal ?? "Not set"} (${formatWhen(intent.latestSignalAt)})`}
                </p>
                {intent.automationIds.length > 0 ? (
                  <p className="mt-1 truncate text-[11px] text-dls-text-muted">Watched by {intent.automationIds.map((id) => names.get(id) ?? "an automation").join(", ")}</p>
                ) : null}
              </DashboardCard>
            );
          })}
        </div>
      )}
    </div>
  );
}
