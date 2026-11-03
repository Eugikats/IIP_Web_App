"use client";

import { useCallback, useEffect, useState } from "react";
import { GlassCard, SectionHeader } from "@/components/iip/Primitives";
import { LoadingSkeleton, ErrorStateSurface } from "@/components/ui/StateSurfaces";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { useUI } from "@/context/UIStateContext";
import { loadTenantSettings, saveTenantSettings, type TenantSettings } from "@/lib/tenantSettings";

type Draft = {
  requireIraApprovalForPublish: boolean;
  missingDocumentReminderDays: string;
  slaWarningLeadHours: string;
  seatLimit: string;
  claimsMeterLimit: string;
};

function toDraft(settings: TenantSettings): Draft {
  return {
    requireIraApprovalForPublish: settings.requireIraApprovalForPublish,
    missingDocumentReminderDays: String(settings.missingDocumentReminderDays),
    slaWarningLeadHours: String(settings.slaWarningLeadHours),
    seatLimit: String(settings.seatLimit),
    claimsMeterLimit: String(settings.claimsMeterLimit),
  };
}

const digits = (value: string) => value.replace(/[^0-9]/g, "");

/**
 * The operating values a workspace administrator can change without a developer: the publishing
 * rule, reminder and SLA warning timings, the seat limit and the claims allowance. Each shows what
 * it is when nobody has changed it, so a change can be undone.
 */
export default function OperatingSettings() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [settings, setSettings] = useState<TenantSettings | null>(null);
  const [draft, setDraft] = useState<Draft | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoadError(null);
    const result = await loadTenantSettings(role);
    if (!result.ok) {
      setLoadError(result.error);
      return;
    }
    setSettings(result.value);
    setDraft(toDraft(result.value));
  }, [role]);

  useEffect(() => { void load(); }, [load]);

  const dirty = settings && draft ? JSON.stringify(draft) !== JSON.stringify(toDraft(settings)) : false;

  async function onSave() {
    if (!draft) return;
    setSaving(true);
    setSaveError(null);
    setSaved(false);
    const result = await saveTenantSettings(role, {
      requireIraApprovalForPublish: draft.requireIraApprovalForPublish,
      missingDocumentReminderDays: Number(draft.missingDocumentReminderDays) || 0,
      slaWarningLeadHours: Number(draft.slaWarningLeadHours) || 0,
      seatLimit: Number(draft.seatLimit) || 0,
      claimsMeterLimit: Number(draft.claimsMeterLimit) || 0,
    });
    setSaving(false);
    if (!result.ok) {
      setSaveError(result.error);
      return;
    }
    setSettings(result.value);
    setDraft(toDraft(result.value));
    setSaved(true);
  }

  return (
    <>
      <SectionHeader eyebrow="Admin" title="Operating settings" sub="The values that decide when customers are reminded, when officers are warned, who may publish a product and how much the workspace may use." />
      {!settings && !loadError && <GlassCard><LoadingSkeleton rows={5} /></GlassCard>}
      {loadError && <GlassCard><ErrorStateSurface title="The settings could not be loaded" message={loadError} onRetry={load} /></GlassCard>}
      {settings && draft && (
        <div className="space-y-4" data-testid="operating-settings">
          <GlassCard>
            <div className="text-[14px] font-bold text-dls-text-primary">Publishing a product</div>
            <label className="mt-3 flex items-start gap-3 text-[13px] text-dls-text-primary">
              <input
                type="checkbox"
                className="mt-1 h-4 w-4"
                checked={draft.requireIraApprovalForPublish}
                onChange={(e) => setDraft({ ...draft, requireIraApprovalForPublish: e.target.checked })}
              />
              <span>
                Require an IRA approval reference and effective date before a product can be published
                <span className="mt-0.5 block text-[12px] text-dls-text-secondary">
                  Keep this on if the insurer is regulated by the IRA. A second person must approve every publish either way.
                </span>
              </span>
            </label>
          </GlassCard>

          <GlassCard>
            <div className="text-[14px] font-bold text-dls-text-primary">Follow-through</div>
            <div className="mt-3 grid gap-4 sm:grid-cols-2">
              <div>
                <label htmlFor="reminder-days" className="mb-1 block text-[12.5px] font-semibold text-dls-text-primary">Remind the customer about missing documents after (days)</label>
                <Input id="reminder-days" inputMode="numeric" value={draft.missingDocumentReminderDays} onChange={(e) => setDraft({ ...draft, missingDocumentReminderDays: digits(e.target.value) })} />
                <p className="mt-1 text-[11.5px] text-dls-text-muted">Usually {settings.defaults.missingDocumentReminderDays}. Between 1 and 30.</p>
              </div>
              <div>
                <label htmlFor="sla-lead" className="mb-1 block text-[12.5px] font-semibold text-dls-text-primary">Warn the officer before a claim is late (hours)</label>
                <Input id="sla-lead" inputMode="numeric" value={draft.slaWarningLeadHours} onChange={(e) => setDraft({ ...draft, slaWarningLeadHours: digits(e.target.value) })} />
                <p className="mt-1 text-[11.5px] text-dls-text-muted">Usually {settings.defaults.slaWarningLeadHours}. Between 1 and 168.</p>
              </div>
            </div>
          </GlassCard>

          <GlassCard>
            <div className="text-[14px] font-bold text-dls-text-primary">Capacity</div>
            <div className="mt-3 grid gap-4 sm:grid-cols-2">
              <div>
                <label htmlFor="seat-limit" className="mb-1 block text-[12.5px] font-semibold text-dls-text-primary">Staff seats</label>
                <Input id="seat-limit" inputMode="numeric" value={draft.seatLimit} onChange={(e) => setDraft({ ...draft, seatLimit: digits(e.target.value) })} />
                <p className="mt-1 text-[11.5px] text-dls-text-muted">Usually {settings.defaults.seatLimit}.</p>
              </div>
              <div>
                <label htmlFor="claims-limit" className="mb-1 block text-[12.5px] font-semibold text-dls-text-primary">Claims allowance</label>
                <Input id="claims-limit" inputMode="numeric" value={draft.claimsMeterLimit} onChange={(e) => setDraft({ ...draft, claimsMeterLimit: digits(e.target.value) })} />
                <p className="mt-1 text-[11.5px] text-dls-text-muted">Usually {settings.defaults.claimsMeterLimit}. Shown against usage on the billing page.</p>
              </div>
            </div>
          </GlassCard>

          {saveError && <p role="alert" className="text-[12.5px] text-dls-danger-text">{saveError}</p>}
          {saved && !dirty && <p role="status" className="text-[12.5px] text-dls-success-text">Settings saved.</p>}
          <div className="flex items-center gap-3">
            <Button variant="primary" onClick={() => void onSave()} disabled={!dirty || saving}>{saving ? "Saving..." : "Save settings"}</Button>
            {settings.updatedAt && (
              <span className="text-[11.5px] text-dls-text-muted">Last changed {new Date(settings.updatedAt).toLocaleString()} by {settings.updatedByUserId}</span>
            )}
          </div>
        </div>
      )}
    </>
  );
}
