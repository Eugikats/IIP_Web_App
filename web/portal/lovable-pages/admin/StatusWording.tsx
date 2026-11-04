"use client";

import { useCallback, useEffect, useState } from "react";
import { GlassCard, SectionHeader } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Input, Textarea } from "@/components/ui/Input";
import { ErrorStateSurface, LoadingSkeleton } from "@/components/ui/StateSurfaces";
import { StatusMessagesSettings } from "@/components/iip/StatusMessagesSettings";
import { useUI } from "@/context/UIStateContext";
import { loadStatusWording, saveStatusWording, type StatusWordingRow } from "@/lib/statusWording";

type Draft = Record<string, { headline: string; detail: string }>;

function toDraft(rows: StatusWordingRow[]): Draft {
  return Object.fromEntries(rows.map((row) => [row.status, { headline: row.headline, detail: row.detail }]));
}

/**
 * What a customer reads about where their claim stands, one entry per claim status (build-loop item
 * 19). Each shows the staff name for the status, the default wording and the workspace's own. The
 * defaults are a starting point: the insurer (and its legal adviser) should confirm them before use.
 */
export default function StatusWording() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [rows, setRows] = useState<StatusWordingRow[] | null>(null);
  const [draft, setDraft] = useState<Draft>({});
  const [loadError, setLoadError] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoadError(null);
    const result = await loadStatusWording(role);
    if (!result.ok) return setLoadError(result.error);
    setRows(result.value);
    setDraft(toDraft(result.value));
  }, [role]);

  useEffect(() => { void load(); }, [load]);

  async function save() {
    if (!rows) return;
    setSaving(true);
    setSaveError(null);
    setSaved(false);
    // Only what differs from the default is sent; the rest goes back to the default.
    const wording: Draft = {};
    for (const row of rows) {
      const entry = draft[row.status];
      if (entry.headline.trim() !== row.defaultHeadline || entry.detail.trim() !== row.defaultDetail) wording[row.status] = entry;
    }
    const result = await saveStatusWording(role, wording);
    setSaving(false);
    if (!result.ok) return setSaveError(result.error);
    setRows(result.value);
    setDraft(toDraft(result.value));
    setSaved(true);
  }

  return (
    <>
      <SectionHeader eyebrow="Admin" title="Claim status wording" sub="What customers read about where their claim stands. Staff keep their own status names; customers see these words on their claim page and in messages about it." />
      <div className="mb-4"><StatusMessagesSettings role={role} /></div>
      {!rows && !loadError && <GlassCard><LoadingSkeleton rows={6} /></GlassCard>}
      {loadError && <GlassCard><ErrorStateSurface title="The wording could not be loaded" message={loadError} onRetry={load} /></GlassCard>}
      {rows && (
        <div className="space-y-4" data-testid="status-wording">
          <p className="text-[12.5px] text-dls-text-secondary">The default wording is a starting point to be confirmed by your insurer and legal adviser before customers rely on it.</p>
          {rows.map((row) => {
            const entry = draft[row.status];
            return (
              <GlassCard key={row.status}>
                <div className="flex flex-wrap items-baseline justify-between gap-2">
                  <div className="text-[13px] font-bold text-dls-text-primary">{row.staffLabel}</div>
                  <div className="text-[11.5px] text-dls-text-muted">{row.customised ? "Your own wording" : "Default wording"}</div>
                </div>
                <label htmlFor={`headline-${row.status}`} className="mb-1 mt-3 block text-[12px] font-semibold text-dls-text-primary">What the customer sees as the title ({entry.headline.length} of 60)</label>
                <Input id={`headline-${row.status}`} value={entry.headline} maxLength={60} onChange={(event) => setDraft({ ...draft, [row.status]: { ...entry, headline: event.target.value } })} className="w-full" />
                <label htmlFor={`detail-${row.status}`} className="mb-1 mt-3 block text-[12px] font-semibold text-dls-text-primary">What it means for them ({entry.detail.length} of 400)</label>
                <Textarea id={`detail-${row.status}`} rows={2} value={entry.detail} maxLength={400} onChange={(event) => setDraft({ ...draft, [row.status]: { ...entry, detail: event.target.value } })} className="w-full" />
                <div className="mt-2">
                  <Button
                    variant="ghost"
                    disabled={entry.headline === row.defaultHeadline && entry.detail === row.defaultDetail}
                    onClick={() => setDraft({ ...draft, [row.status]: { headline: row.defaultHeadline, detail: row.defaultDetail } })}
                    aria-label={`Use the default wording for ${row.staffLabel}`}
                  >
                    Use the default
                  </Button>
                </div>
              </GlassCard>
            );
          })}
          <div className="flex flex-wrap items-center gap-3">
            <Button onClick={() => void save()} disabled={saving}>{saving ? "Saving" : "Save wording"}</Button>
            {saved && <span role="status" className="text-[12.5px] text-dls-text-secondary">Saved. Customers see this the next time they open their claim.</span>}
            {saveError && <span role="alert" className="text-[12.5px] text-dls-danger-text">{saveError}</span>}
          </div>
        </div>
      )}
    </>
  );
}
