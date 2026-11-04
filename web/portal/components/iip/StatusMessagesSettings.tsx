"use client";

import { useCallback, useEffect, useState } from "react";
import { GlassCard } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { ErrorStateSurface, LoadingSkeleton } from "@/components/ui/StateSurfaces";
import { loadStatusNotifications, saveStatusNotifications, type StatusNotificationSettings } from "@/lib/statusNotifications";

type Draft = { notify: boolean; autoSend: boolean; templates: Record<string, { name: string; language: string }> };

function toDraft(settings: StatusNotificationSettings): Draft {
  return {
    notify: settings.notifyCustomerOnStatusChange,
    autoSend: settings.autoSendStatusMessages,
    templates: Object.fromEntries(settings.statuses.map((row) => [row.status, { name: row.whatsAppTemplateName ?? "", language: row.whatsAppTemplateLanguage ?? "en" }])),
  };
}

/**
 * Whether the customer is told when their claim's status changes (build-loop item 21), whether those
 * messages may go without a person approving them, and the WhatsApp templates Meta approved for each
 * status. Messages that announce an outcome or an escalation always wait for a person, whatever is chosen here.
 */
export function StatusMessagesSettings({ role }: { role: string }) {
  const [settings, setSettings] = useState<StatusNotificationSettings | null>(null);
  const [draft, setDraft] = useState<Draft | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoadError(null);
    const result = await loadStatusNotifications(role);
    if (!result.ok) return setLoadError(result.error);
    setSettings(result.value);
    setDraft(toDraft(result.value));
  }, [role]);

  useEffect(() => { void load(); }, [load]);

  async function save() {
    if (!draft) return;
    setSaving(true);
    setSaveError(null);
    setSaved(false);
    const templates = Object.fromEntries(Object.entries(draft.templates).filter(([, template]) => template.name.trim().length > 0));
    const result = await saveStatusNotifications(role, { notifyCustomerOnStatusChange: draft.notify, autoSendStatusMessages: draft.autoSend, whatsAppTemplates: templates });
    setSaving(false);
    if (!result.ok) return setSaveError(result.error);
    setSettings(result.value);
    setDraft(toDraft(result.value));
    setSaved(true);
  }

  if (loadError) return <GlassCard><ErrorStateSurface title="The message settings could not be loaded" message={loadError} onRetry={load} /></GlassCard>;
  if (!settings || !draft) return <GlassCard><LoadingSkeleton rows={3} /></GlassCard>;

  return (
    <GlassCard>
      <div data-testid="status-messages-settings">
        <div className="text-[13px] font-bold text-dls-text-primary">Telling customers when the status changes</div>
        <p className="mt-1 text-[12.5px] text-dls-text-secondary">
          When a claim moves to a new status, the customer is sent the wording below on the channel they reached you by: WhatsApp if the claim came in on WhatsApp, otherwise a text message, otherwise email.
        </p>
        <label className="mt-3 flex items-start gap-2 text-[13px] text-dls-text-primary">
          <input type="checkbox" className="mt-1 h-4 w-4" checked={draft.notify} onChange={(event) => setDraft({ ...draft, notify: event.target.checked })} />
          <span>Tell customers when their claim&apos;s status changes</span>
        </label>
        <label className="mt-2 flex items-start gap-2 text-[13px] text-dls-text-primary">
          <input type="checkbox" className="mt-1 h-4 w-4" checked={draft.autoSend} disabled={!draft.notify} onChange={(event) => setDraft({ ...draft, autoSend: event.target.checked })} />
          <span>
            Send them without a person approving each one
            <span className="mt-0.5 block text-[12px] text-dls-text-secondary">
              Messages about a decision, an escalation or a claim being ready for review always wait for a person, whatever you choose. Everything else waits for approval unless this is on.
            </span>
          </span>
        </label>

        <div className="mt-4 text-[12.5px] font-semibold text-dls-text-primary">WhatsApp templates</div>
        <p className="mt-1 text-[12px] text-dls-text-secondary">
          WhatsApp only lets you message a customer after more than 24 hours of silence with a template Meta has approved. Enter the approved name for each status. The template can use the named values claim_reference and status. Without one, that message waits for a person.
        </p>
        <ul className="mt-2 space-y-2">
          {settings.statuses.filter((row) => row.sendsMessage).map((row) => (
            <li key={row.status} className="grid grid-cols-1 items-center gap-2 sm:grid-cols-[10rem_1fr_6rem]">
              <span className="text-[12.5px] text-dls-text-primary">
                {row.staffLabel}
                {row.needsApproval && <span className="block text-[11px] text-dls-text-muted">Always needs a person</span>}
              </span>
              <Input
                aria-label={`WhatsApp template name for ${row.staffLabel}`}
                placeholder="approved_template_name"
                value={draft.templates[row.status]?.name ?? ""}
                onChange={(event) => setDraft({ ...draft, templates: { ...draft.templates, [row.status]: { ...draft.templates[row.status], name: event.target.value } } })}
              />
              <Input
                aria-label={`WhatsApp template language for ${row.staffLabel}`}
                value={draft.templates[row.status]?.language ?? "en"}
                onChange={(event) => setDraft({ ...draft, templates: { ...draft.templates, [row.status]: { ...draft.templates[row.status], language: event.target.value } } })}
              />
            </li>
          ))}
        </ul>

        <div className="mt-4 flex flex-wrap items-center gap-3">
          <Button onClick={() => void save()} disabled={saving}>{saving ? "Saving" : "Save message settings"}</Button>
          {saved && <span role="status" className="text-[12.5px] text-dls-text-secondary">Saved.</span>}
          {saveError && <span role="alert" className="text-[12.5px] text-dls-danger-text">{saveError}</span>}
        </div>
      </div>
    </GlassCard>
  );
}
