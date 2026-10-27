"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Upload } from "lucide-react";
import { GlassCard, SectionHeader } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Field, Input, Select, Textarea } from "@/components/ui/Input";
import { SideDrawer } from "@/components/ui/SideDrawer";
import { StatusPill, type PillTone } from "@/components/ui/StatusPill";
import {
  EmptyFilteredStateSurface,
  ErrorStateSurface,
  LoadingSkeleton,
  StateSurfaceShell,
} from "@/components/ui/StateSurfaces";
import { useUI } from "@/context/UIStateContext";
import {
  decideTemplate,
  importTemplates,
  listTemplates,
  previewOf,
  saveTemplateDraft,
  submitTemplate,
  type MessageTemplate,
  type TemplateCategory,
  type TemplateEdit,
  type TemplateStatus,
} from "@/lib/templates";

const STATUS: Record<TemplateStatus, { label: string; tone: PillTone }> = {
  Draft: { label: "Draft", tone: "neutral" },
  AwaitingApproval: { label: "Waiting for WhatsApp", tone: "warning" },
  Approved: { label: "Approved", tone: "success" },
  Rejected: { label: "Rejected", tone: "danger" },
  Paused: { label: "Paused", tone: "warning" },
};

const keyOf = (template: Pick<MessageTemplate, "name" | "language">) => `${template.name}:${template.language}`;

export default function Templates() {
  const { roleDef } = useUI();
  const role = roleDef.key;

  const [templates, setTemplates] = useState<MessageTemplate[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [filter, setFilter] = useState<"All" | TemplateStatus>("All");
  const [openKey, setOpenKey] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const fileInput = useRef<HTMLInputElement>(null);

  const load = useCallback(async () => {
    setLoadError(null);
    const result = await listTemplates(role);
    if (!result.ok) {
      setTemplates(null);
      setLoadError(result.error);
      return;
    }
    setTemplates(result.value);
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  const visible = useMemo(() => (templates ?? []).filter((template) => filter === "All" || template.status === filter), [templates, filter]);
  const open = templates?.find((template) => keyOf(template) === openKey) ?? null;
  const approved = (templates ?? []).filter((template) => template.isSendable).length;

  function replace(updated: MessageTemplate) {
    setTemplates((current) => (current ?? []).map((item) => (keyOf(item) === keyOf(updated) ? updated : item)));
  }

  async function onImport(file: File | undefined) {
    if (!file) return;
    setNotice(null);
    try {
      const parsed = JSON.parse(await file.text()) as unknown;
      const list = Array.isArray(parsed) ? parsed : (parsed as { templates?: unknown[] }).templates;
      if (!Array.isArray(list)) throw new Error("shape");
      const result = await importTemplates(role, list);
      if (!result.ok) {
        setNotice(result.error);
        return;
      }
      setNotice(
        `${result.value.stored} template(s) imported.` +
          (result.value.refused.length > 0 ? ` Not imported: ${result.value.refused.join(" ")}` : ""),
      );
      await load();
    } catch {
      setNotice("That file could not be read. It should be a JSON list of templates, each with name, language, category, status, purpose, body, parameters and buttons.");
    } finally {
      if (fileInput.current) fileInput.current.value = "";
    }
  }

  return (
    <>
      <SectionHeader
        eyebrow="Admin · Channels"
        title="Message templates"
        sub="The opening words WhatsApp lets you send to someone who has not written in the last 24 hours. Only WhatsApp can approve a template: this page records where each one stands, and a message that needs a template waits until one is approved."
      />

      {templates && approved === 0 && (
        <div className="mb-3">
          <GlassCard>
            <p className="px-1 text-[13.5px] text-dls-text-secondary" role="status">
              No template is approved yet. Until WhatsApp approves one, nothing can be sent to someone who has not written in during the last 24 hours.
            </p>
          </GlassCard>
        </div>
      )}

      {notice && (
        <div className="mb-3">
          <GlassCard>
            <p className="px-1 text-[13.5px] text-dls-text-secondary" role="status">{notice}</p>
          </GlassCard>
        </div>
      )}

      <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
        <label className="flex items-center gap-2 text-[13px] text-dls-text-secondary">
          Show
          <Select aria-label="Filter by status" value={filter} onChange={(event) => setFilter(event.target.value as "All" | TemplateStatus)}>
            <option value="All">All templates</option>
            {(Object.keys(STATUS) as TemplateStatus[]).map((status) => (
              <option key={status} value={status}>{STATUS[status].label}</option>
            ))}
          </Select>
        </label>
        <div>
          <input ref={fileInput} type="file" accept="application/json,.json" className="sr-only" aria-label="Import templates from a file" onChange={(event) => void onImport(event.target.files?.[0])} />
          <Button variant="secondary" icon={<Upload className="h-4 w-4" aria-hidden="true" />} onClick={() => fileInput.current?.click()}>
            Import templates
          </Button>
        </div>
      </div>

      {!templates && !loadError && (
        <GlassCard>
          <LoadingSkeleton rows={5} />
        </GlassCard>
      )}

      {loadError && (
        <GlassCard>
          <ErrorStateSurface title="The templates could not be loaded" message={loadError} onRetry={() => void load()} />
        </GlassCard>
      )}

      {templates && visible.length === 0 && (
        <StateSurfaceShell>
          <EmptyFilteredStateSurface onClear={() => setFilter("All")} />
        </StateSurfaceShell>
      )}

      {templates && visible.length > 0 && (
        <StateSurfaceShell>
          <ul className="divide-y divide-dls-border-subtle" aria-label="Message templates">
            {visible.map((template) => (
              <li key={keyOf(template)}>
                <button
                  type="button"
                  onClick={() => setOpenKey(keyOf(template))}
                  className="dls-focus-outline flex w-full flex-wrap items-center justify-between gap-3 px-4 py-3 text-left hover:bg-dls-bg-page"
                >
                  <span className="min-w-0">
                    <span className="block font-mono text-[13px] font-semibold text-dls-text-primary">{template.name}</span>
                    <span className="block text-[12.5px] text-dls-text-secondary">{template.purpose}</span>
                  </span>
                  <span className="flex items-center gap-2">
                    <span className="text-[12px] text-dls-text-muted">{template.category} · {template.language}</span>
                    <StatusPill tone={STATUS[template.status].tone} label={STATUS[template.status].label} />
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </StateSurfaceShell>
      )}

      <SideDrawer open={open !== null} title={open?.name ?? ""} description={open?.purpose} onClose={() => setOpenKey(null)}>
        {open && <TemplateDetail key={keyOf(open)} template={open} role={role} onChanged={replace} />}
      </SideDrawer>
    </>
  );
}

function TemplateDetail({ template, role, onChanged }: { template: MessageTemplate; role: string; onChanged: (updated: MessageTemplate) => void }) {
  const [editing, setEditing] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [rejecting, setRejecting] = useState(false);
  const [reason, setReason] = useState("");
  const [draft, setDraft] = useState<TemplateEdit>({
    category: template.category,
    purpose: template.purpose,
    body: template.body,
    parameters: template.parameters,
    buttons: template.buttons,
  });

  async function run(action: () => Promise<{ ok: true; value: MessageTemplate } | { ok: false; error: string }>) {
    setBusy(true);
    setError(null);
    const result = await action();
    setBusy(false);
    if (!result.ok) {
      setError(result.error);
      return false;
    }
    onChanged(result.value);
    return true;
  }

  const shown = editing ? { body: draft.body, parameters: draft.parameters } : template;

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2">
        <StatusPill tone={STATUS[template.status].tone} label={STATUS[template.status].label} />
        <span className="text-[12.5px] text-dls-text-muted">{template.category} · {template.language}</span>
      </div>

      <div>
        <p className="mb-1.5 text-[12px] font-bold uppercase tracking-[0.12em] text-dls-text-muted">What the customer will read</p>
        <div className="rounded-dls-card border border-dls-border-subtle bg-dls-bg-page px-4 py-3 text-[13.5px] leading-relaxed text-dls-text-primary">
          {previewOf(shown)}
        </div>
        <p className="mt-1.5 text-[12px] text-dls-text-muted">Example values only. Nothing is sent from this page.</p>
      </div>

      {template.buttons.length > 0 && !editing && (
        <div className="flex flex-wrap gap-2" aria-label="Reply buttons">
          {template.buttons.map((label) => (
            <span key={label} className="rounded-dls-pill border border-dls-border-pill px-3 py-1 text-[12.5px] text-dls-text-secondary">{label}</span>
          ))}
        </div>
      )}

      {!editing && (
        <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-[13px]">
          {template.parameters.map((name, index) => (
            <div key={name} className="contents">
              <dt className="font-mono text-dls-text-muted">{`{{${index + 1}}}`}</dt>
              <dd className="text-dls-text-secondary">{name}</dd>
            </div>
          ))}
        </dl>
      )}

      {template.decisionNote && (
        <p className="text-[13px] text-dls-text-secondary">
          <span className="font-semibold text-dls-text-primary">Note on the decision: </span>{template.decisionNote}
        </p>
      )}

      {template.problems.length > 0 && !editing && template.isEditable && (
        <p className="text-[13px] text-dls-danger-text" role="alert">{template.problems[0]}</p>
      )}

      {editing && (
        <form
          className="flex flex-col gap-3"
          onSubmit={(event) => {
            event.preventDefault();
            void run(() => saveTemplateDraft(role, template, draft)).then((saved) => saved && setEditing(false));
          }}
        >
          <Field label="Purpose" htmlFor="tpl-purpose">
            <Input id="tpl-purpose" value={draft.purpose} onChange={(event) => setDraft({ ...draft, purpose: event.target.value })} />
          </Field>
          <Field label="Kind" hint="WhatsApp decides which kind it accepts." htmlFor="tpl-category">
            <Select id="tpl-category" value={draft.category} onChange={(event) => setDraft({ ...draft, category: event.target.value as TemplateCategory })}>
              <option value="Utility">Utility (about something they already have)</option>
              <option value="Marketing">Marketing (an offer)</option>
              <option value="Authentication">Authentication (a code)</option>
            </Select>
          </Field>
          <Field label="Words" hint="Use {{1}}, {{2}} where a value goes. It cannot start or end with one." htmlFor="tpl-body">
            <Textarea id="tpl-body" rows={5} value={draft.body} onChange={(event) => setDraft({ ...draft, body: event.target.value })} />
          </Field>
          <Field label="What each value is" hint="Names in order, separated by commas." htmlFor="tpl-parameters">
            <Input
              id="tpl-parameters"
              value={draft.parameters.join(", ")}
              onChange={(event) => setDraft({ ...draft, parameters: event.target.value.split(",").map((part) => part.trim()).filter(Boolean) })}
            />
          </Field>
          <Field label="Reply buttons" hint="Up to three, separated by commas." htmlFor="tpl-buttons">
            <Input
              id="tpl-buttons"
              value={draft.buttons.join(", ")}
              onChange={(event) => setDraft({ ...draft, buttons: event.target.value.split(",").map((part) => part.trim()).filter(Boolean) })}
            />
          </Field>
          <div className="flex gap-2">
            <Button type="submit" variant="primary" disabled={busy}>Save draft</Button>
            <Button variant="ghost" disabled={busy} onClick={() => setEditing(false)}>Cancel</Button>
          </div>
        </form>
      )}

      {rejecting && (
        <div className="flex flex-col gap-2">
          <Field label="Why did WhatsApp reject it?" htmlFor="tpl-reason">
            <Textarea id="tpl-reason" rows={3} value={reason} onChange={(event) => setReason(event.target.value)} />
          </Field>
          <div className="flex gap-2">
            <Button variant="secondary" disabled={busy || reason.trim().length === 0} onClick={() => void run(() => decideTemplate(role, template, "Rejected", reason)).then((done) => done && setRejecting(false))}>
              Record the rejection
            </Button>
            <Button variant="ghost" onClick={() => setRejecting(false)}>Cancel</Button>
          </div>
        </div>
      )}

      {error && <p className="text-[13px] text-dls-danger-text" role="alert">{error}</p>}

      {!editing && !rejecting && (
        <div className="flex flex-wrap gap-2 border-t border-dls-border-subtle pt-3">
          {template.isEditable && (
            <>
              <Button variant="secondary" disabled={busy} onClick={() => setEditing(true)}>Edit the draft</Button>
              <Button variant="primary" disabled={busy || template.problems.length > 0} onClick={() => void run(() => submitTemplate(role, template))}>
                Mark as submitted to WhatsApp
              </Button>
            </>
          )}
          {template.status === "AwaitingApproval" && (
            <>
              <Button variant="primary" disabled={busy} onClick={() => void run(() => decideTemplate(role, template, "Approved", ""))}>
                WhatsApp approved it
              </Button>
              <Button variant="secondary" disabled={busy} onClick={() => setRejecting(true)}>WhatsApp rejected it</Button>
            </>
          )}
          {template.status === "Approved" && (
            <Button variant="secondary" disabled={busy} onClick={() => void run(() => decideTemplate(role, template, "Paused", "Paused by an administrator."))}>
              Pause this template
            </Button>
          )}
          {template.status === "Paused" && (
            <Button variant="secondary" disabled={busy} onClick={() => void run(() => decideTemplate(role, template, "Approved", "Resumed by an administrator."))}>
              Resume this template
            </Button>
          )}
        </div>
      )}
    </div>
  );
}
