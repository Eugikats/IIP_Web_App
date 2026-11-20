"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { ArrowLeft, Building2, Check } from "lucide-react";
import { DashboardCard } from "./ui";
import { DashboardSectionHeader } from "./ui";
import { StatusPill } from "./ui";
import { PlatformEmptyState, PlatformLoadingState } from "./ui";
import type { PlatformMe, PlatformTemplatesResponse, PlatformWorkspace, WorkspaceTemplate } from "@/lib/platform/types";
import { usePlatformMutation, usePlatformQuery } from "@/lib/platform/use-platform";
import { Button, InlineError, inputClass, labelClass } from "./ui";

const STEPS = ["Template", "Details", "Data & automations", "Review"] as const;

type Form = { name: string; department: string; purpose: string; kpis: string[]; connectors: string[]; automations: string[] };

function Toggle({ on, children, onClick }: { on: boolean; children: React.ReactNode; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`inline-flex items-center gap-1.5 rounded-full border px-3 py-1.5 text-[12px] font-medium transition ${on ? "border-dls-primary bg-dls-primary-soft text-dls-primary" : "border-dls-border-subtle bg-dls-surface text-dls-text-secondary hover:bg-dls-bg-page"}`}
    >
      {on ? <Check className="h-3.5 w-3.5" /> : null}
      {children}
    </button>
  );
}

const toggle = (list: string[], key: string) => (list.includes(key) ? list.filter((k) => k !== key) : [...list, key]);

export default function WorkspaceWizard() {
  const router = useRouter();
  const me = usePlatformQuery<PlatformMe>("/me");
  const templates = usePlatformQuery<PlatformTemplatesResponse>("/templates");
  const mutate = usePlatformMutation();
  const [step, setStep] = useState(0);
  const [template, setTemplate] = useState<WorkspaceTemplate | null>(null);
  const [form, setForm] = useState<Form | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  if (me.loading || templates.loading) return <PlatformLoadingState title="Loading…" />;
  if (me.data && !me.data.isOrgAdmin) {
    return <PlatformEmptyState title="Only administrators create workspaces" description="Ask your organisation administrator to set up a new workspace." />;
  }

  const automationName = (key: string) => templates.data?.automations.find((a) => a.key === key)?.name ?? key;
  const connectorName = (key: string) => templates.data?.connectors.find((c) => c.key === key)?.name ?? key;

  const choose = (t: WorkspaceTemplate) => {
    setTemplate(t);
    setForm({ name: t.name, department: t.department, purpose: t.purpose, kpis: [...t.kpis], connectors: [...t.connectors], automations: [...t.automations] });
    setStep(1);
  };

  const create = async () => {
    if (!form || !template) return;
    setSaving(true);
    setError(null);
    try {
      const created = await mutate<PlatformWorkspace>("/workspaces", "POST", { templateKey: template.key, ...form });
      router.push(`/app/platform/workspaces/${created.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not create the workspace.");
      setSaving(false);
    }
  };

  return (
    <div className="mx-auto max-w-4xl space-y-5 pb-10">
      <button type="button" onClick={() => router.back()} className="inline-flex items-center gap-1 text-[12px] font-semibold text-dls-text-secondary hover:text-dls-text-primary">
        <ArrowLeft className="h-3.5 w-3.5" /> Back
      </button>
      <DashboardSectionHeader icon={<Building2 className="h-5 w-5" />} title="New workspace" description="Start from a template for the department. You can change everything later." />

      <ol className="flex flex-wrap gap-2">
        {STEPS.map((label, i) => (
          <li key={label} className={`flex items-center gap-1.5 rounded-full px-3 py-1 text-[12px] font-semibold ${i === step ? "bg-dls-primary-soft text-dls-primary" : i < step ? "text-dls-text-secondary" : "text-dls-text-muted"}`}>
            {i < step ? <Check className="h-3.5 w-3.5" /> : <span>{i + 1}.</span>} {label}
          </li>
        ))}
      </ol>

      {step === 0 ? (
        <div className="grid gap-3 md:grid-cols-2">
          {(templates.data?.workspaces ?? []).filter((t) => !t.isLiveTemplate).map((t) => (
            <button key={t.key} type="button" onClick={() => choose(t)} className="text-left">
              <DashboardCard interactive padding="md" className="h-full">
                <div className="flex items-start justify-between gap-2">
                  <p className="text-[14px] font-semibold text-dls-text-primary">{t.name}</p>
                  <StatusPill tone="neutral">{t.department}</StatusPill>
                </div>
                <p className="mt-1 text-[13px] text-dls-text-secondary">{t.purpose}</p>
                <p className="mt-2 text-[11px] text-dls-text-muted">{t.automations.length} suggested automations · {t.kpis.length} KPIs</p>
              </DashboardCard>
            </button>
          ))}
        </div>
      ) : form && template ? (
        <DashboardCard padding="lg">
          {step === 1 ? (
            <div className="grid gap-4 md:grid-cols-2">
              <div>
                <label className={labelClass} htmlFor="ws-name">Workspace name</label>
                <input id="ws-name" className={inputClass} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
              </div>
              <div>
                <label className={labelClass} htmlFor="ws-dept">Department</label>
                <input id="ws-dept" className={inputClass} value={form.department} onChange={(e) => setForm({ ...form, department: e.target.value })} />
              </div>
              <div className="md:col-span-2">
                <label className={labelClass} htmlFor="ws-purpose">Purpose</label>
                <textarea id="ws-purpose" rows={3} className={inputClass} value={form.purpose} onChange={(e) => setForm({ ...form, purpose: e.target.value })} />
              </div>
              <div className="md:col-span-2">
                <p className={labelClass}>KPIs</p>
                <div className="flex flex-wrap gap-2">
                  {template.kpis.map((k) => (
                    <Toggle key={k} on={form.kpis.includes(k)} onClick={() => setForm({ ...form, kpis: toggle(form.kpis, k) })}>{k}</Toggle>
                  ))}
                </div>
              </div>
            </div>
          ) : step === 2 ? (
            <div className="space-y-5">
              <div>
                <p className={labelClass}>Data sources</p>
                <div className="flex flex-wrap gap-2">
                  {(templates.data?.connectors ?? []).map((c) => (
                    <Toggle key={c.key} on={form.connectors.includes(c.key)} onClick={() => setForm({ ...form, connectors: toggle(form.connectors, c.key) })}>{c.name}</Toggle>
                  ))}
                </div>
                <p className="mt-1.5 text-[11px] text-dls-text-muted">Sources are connected in Integrations and Knowledge. The workspace shows “Awaiting data” until they are.</p>
              </div>
              <div>
                <p className={labelClass}>Suggested automations (created as drafts)</p>
                <div className="flex flex-wrap gap-2">
                  {(templates.data?.automations ?? []).map((a) => (
                    <Toggle key={a.key} on={form.automations.includes(a.key)} onClick={() => setForm({ ...form, automations: toggle(form.automations, a.key) })}>{a.name}</Toggle>
                  ))}
                </div>
              </div>
            </div>
          ) : (
            <div className="space-y-3 text-[13px]">
              <p className="text-[15px] font-semibold text-dls-text-primary">{form.name}</p>
              <p className="text-dls-text-secondary">{form.purpose}</p>
              <p><span className="font-semibold text-dls-text-secondary">Department:</span> {form.department}</p>
              <p><span className="font-semibold text-dls-text-secondary">KPIs:</span> {form.kpis.join(", ") || "Not set"}</p>
              <p><span className="font-semibold text-dls-text-secondary">Data sources:</span> {form.connectors.map(connectorName).join(", ") || "Not set"}</p>
              <p><span className="font-semibold text-dls-text-secondary">Automations (drafts):</span> {form.automations.map(automationName).join(", ") || "Not set"}</p>
            </div>
          )}
          <div className="mt-5 flex flex-wrap gap-2">
            <Button variant="secondary" onClick={() => setStep(step - 1)}>Back</Button>
            {step < 3 ? (
              <Button disabled={form.name.trim().length < 2} onClick={() => setStep(step + 1)}>Continue</Button>
            ) : (
              <Button disabled={saving} onClick={() => void create()}>{saving ? "Creating…" : "Create workspace"}</Button>
            )}
          </div>
          <div className="mt-3"><InlineError message={error} /></div>
        </DashboardCard>
      ) : null}
    </div>
  );
}
