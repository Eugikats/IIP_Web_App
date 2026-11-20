"use client";

import { useMemo, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { ArrowLeft, Bot, Check } from "lucide-react";
import { DashboardCard } from "./ui";
import { DashboardSectionHeader } from "./ui";
import { StatusPill } from "./ui";
import { PlatformLoadingState } from "./ui";
import { groupBy } from "@/lib/platform/format";
import type { PlatformAutomation, PlatformTemplatesResponse, PlatformWorkspace } from "@/lib/platform/types";
import { usePlatformMutation, usePlatformQuery } from "@/lib/platform/use-platform";
import AutomationEditor, { draftFromTemplate, draftToRequest, type AutomationDraft } from "./AutomationEditor";
import { Button, InlineError, inputClass, labelClass } from "./ui";

const STEPS = ["Template", "Configure", "Review"] as const;

export default function AutomationWizard() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const templates = usePlatformQuery<PlatformTemplatesResponse>("/templates");
  const workspaces = usePlatformQuery<PlatformWorkspace[]>("/workspaces");
  const mutate = usePlatformMutation();
  const [step, setStep] = useState(0);
  const [templateKey, setTemplateKey] = useState<string | null>(null);
  const [workspaceId, setWorkspaceId] = useState<string>(searchParams?.get("workspaceId") ?? "");
  const [draft, setDraft] = useState<AutomationDraft | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const template = templates.data?.automations.find((t) => t.key === templateKey);
  const groups = useMemo(() => groupBy(templates.data?.automations ?? [], (t) => t.category), [templates.data]);
  const wsId = workspaceId || workspaces.data?.find((w) => w.isPrimary)?.id || "";

  const choose = (key: string) => {
    const t = templates.data?.automations.find((x) => x.key === key);
    if (!t) return;
    setTemplateKey(key);
    setDraft(draftFromTemplate(t));
    setStep(1);
  };

  const create = async () => {
    if (!draft || !templateKey) return;
    setSaving(true);
    setError(null);
    try {
      const created = await mutate<PlatformAutomation>("/automations", "POST", { ...draftToRequest(draft), templateKey, workspaceId: wsId || null });
      router.push(`/app/platform/automations/${created.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not create the automation.");
      setSaving(false);
    }
  };

  return (
    <div className="mx-auto max-w-4xl space-y-5 pb-10">
      <button type="button" onClick={() => router.back()} className="inline-flex items-center gap-1 text-[12px] font-semibold text-dls-text-secondary hover:text-dls-text-primary">
        <ArrowLeft className="h-3.5 w-3.5" /> Back
      </button>
      <DashboardSectionHeader icon={<Bot className="h-5 w-5" />} title="New automation" description="Pick what to watch, set the thresholds and schedule, then test it. It runs only after an approver approves it." />

      <ol className="flex flex-wrap gap-2">
        {STEPS.map((label, i) => (
          <li key={label} className={`flex items-center gap-1.5 rounded-full px-3 py-1 text-[12px] font-semibold ${i === step ? "bg-dls-primary-soft text-dls-primary" : i < step ? "text-dls-text-secondary" : "text-dls-text-muted"}`}>
            {i < step ? <Check className="h-3.5 w-3.5" /> : <span>{i + 1}.</span>} {label}
          </li>
        ))}
      </ol>

      {templates.loading ? (
        <PlatformLoadingState title="Loading templates…" />
      ) : step === 0 ? (
        <div className="space-y-5">
          {groups.map(([category, items]) => (
            <div key={category}>
              <p className="mb-2 text-[11px] font-semibold uppercase tracking-[0.12em] text-dls-text-muted">{category}</p>
              <div className="grid gap-3 md:grid-cols-2">
                {items.map((t) => (
                  <button key={t.key} type="button" onClick={() => choose(t.key)} className="text-left">
                    <DashboardCard interactive padding="md" className="h-full">
                      <div className="flex items-start justify-between gap-2">
                        <p className="text-[14px] font-semibold text-dls-text-primary">{t.name}</p>
                        {!t.hasRunner ? <StatusPill tone="neutral">Needs setup</StatusPill> : null}
                      </div>
                      <p className="mt-1 text-[13px] text-dls-text-secondary">{t.description}</p>
                      {t.dataUsed.length > 0 ? <p className="mt-2 text-[11px] text-dls-text-muted">Reads: {t.dataUsed.join(", ")}</p> : null}
                    </DashboardCard>
                  </button>
                ))}
              </div>
            </div>
          ))}
        </div>
      ) : step === 1 && draft ? (
        <DashboardCard padding="lg">
          <div className="mb-4">
            <label className={labelClass} htmlFor="auto-ws">Workspace</label>
            <select id="auto-ws" className={inputClass} value={wsId} onChange={(e) => setWorkspaceId(e.target.value)}>
              {(workspaces.data ?? []).map((w) => (
                <option key={w.id} value={w.id}>{w.name}</option>
              ))}
            </select>
          </div>
          <AutomationEditor template={template} draft={draft} onChange={setDraft} />
          <div className="mt-5 flex flex-wrap gap-2">
            <Button variant="secondary" onClick={() => setStep(0)}>Change template</Button>
            <Button disabled={!draft.name.trim()} onClick={() => setStep(2)}>Review</Button>
          </div>
        </DashboardCard>
      ) : draft && template ? (
        <DashboardCard padding="lg">
          <p className="text-[15px] font-semibold text-dls-text-primary">{draft.name}</p>
          <p className="mt-1 text-[13px] text-dls-text-secondary">{draft.objective}</p>
          <dl className="mt-4 grid gap-3 text-[13px] sm:grid-cols-2">
            <div><dt className="text-[11px] font-semibold uppercase tracking-wide text-dls-text-muted">Template</dt><dd className="text-dls-text-primary">{template.name}</dd></div>
            <div><dt className="text-[11px] font-semibold uppercase tracking-wide text-dls-text-muted">Workspace</dt><dd className="text-dls-text-primary">{workspaces.data?.find((w) => w.id === wsId)?.name ?? "Not set"}</dd></div>
            {template.fields.map((f) => (
              <div key={f.key}><dt className="text-[11px] font-semibold uppercase tracking-wide text-dls-text-muted">{f.label}</dt><dd className="text-dls-text-primary">{String(draft.logic[f.key] ?? "Not set")} {f.unit ?? ""}</dd></div>
            ))}
            <div><dt className="text-[11px] font-semibold uppercase tracking-wide text-dls-text-muted">Output</dt><dd className="text-dls-text-primary">Intelligence Inbox</dd></div>
          </dl>
          <p className="mt-4 rounded-dls-control bg-dls-bg-page px-3 py-2 text-[12px] text-dls-text-secondary">
            It is saved as a draft. Next: run a test (a dry run that changes nothing), submit it, and an approver activates it.
          </p>
          <div className="mt-4 flex flex-wrap gap-2">
            <Button variant="secondary" onClick={() => setStep(1)}>Edit</Button>
            <Button disabled={saving} onClick={() => void create()}>{saving ? "Saving…" : "Save draft"}</Button>
          </div>
          <div className="mt-3"><InlineError message={error} /></div>
        </DashboardCard>
      ) : null}
    </div>
  );
}
