"use client";

import { useCallback, useEffect, useState } from "react";
import { GlassCard, SectionHeader } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Select, Textarea } from "@/components/ui/Input";
import { ErrorStateSurface, LoadingSkeleton } from "@/components/ui/StateSurfaces";
import { useUI } from "@/context/UIStateContext";
import { loadDelivery, saveDelivery, type ClaimsPackDelivery } from "@/lib/claimsPackDelivery";

const STATUSES: { value: string; label: string }[] = [
  { value: "Assigned", label: "Assigned" },
  { value: "InReview", label: "Under review" },
  { value: "ReadyForReview", label: "Ready for review" },
  { value: "PendingApproval", label: "Awaiting approval" },
  { value: "ReadyForExport", label: "Ready for export" },
  { value: "Escalated", label: "Escalated" },
  { value: "Settled", label: "Settled" },
  { value: "Closed", label: "Closed" },
];

/**
 * Who a claims pack is emailed to, and when it goes by itself (build-loop item 25). A small pack is attached; a larger one goes
 * as a secure link that expires. Staff can always send one by hand from the claim page.
 */
export default function ClaimsPackEmail() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [settings, setSettings] = useState<ClaimsPackDelivery | null>(null);
  const [recipients, setRecipients] = useState("");
  const [trigger, setTrigger] = useState("Manual");
  const [status, setStatus] = useState("Closed");
  const [loadError, setLoadError] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoadError(null);
    const result = await loadDelivery(role);
    if (!result.ok) return setLoadError(result.error);
    setSettings(result.value);
    setRecipients(result.value.recipients.join("\n"));
    setTrigger(result.value.trigger);
    setStatus(result.value.triggerStatus ?? "Closed");
  }, [role]);

  useEffect(() => { void load(); }, [load]);

  async function save() {
    setSaving(true);
    setSaveError(null);
    setSaved(false);
    const list = recipients.split(/[\n,;]+/).map((item) => item.trim()).filter(Boolean);
    const result = await saveDelivery(role, { recipients: list, trigger, triggerStatus: trigger === "Status" ? status : null });
    setSaving(false);
    if (!result.ok) return setSaveError(result.error);
    setSettings(result.value);
    setRecipients(result.value.recipients.join("\n"));
    setSaved(true);
  }

  return (
    <>
      <SectionHeader eyebrow="Admin" title="Claims pack email" sub="Send a claim's pack (summary, documents, evidence and file fingerprints) to the people who need it, such as a reinsurer or an assessor." />
      {!settings && !loadError && <GlassCard><LoadingSkeleton rows={4} /></GlassCard>}
      {loadError && <GlassCard><ErrorStateSurface title="The settings could not be loaded" message={loadError} onRetry={load} /></GlassCard>}
      {settings && (
        <div className="space-y-4" data-testid="claims-pack-email">
          <GlassCard>
            <label htmlFor="pack-recipients" className="block text-[13px] font-bold text-dls-text-primary">Who receives it</label>
            <p className="mt-1 text-[12px] text-dls-text-secondary">One email address per line, up to {settings.maxRecipients}. The pack contains personal information, so only list people who may hold it.</p>
            <Textarea id="pack-recipients" rows={4} value={recipients} onChange={(event) => setRecipients(event.target.value)} className="mt-2 w-full" placeholder="reinsurance@example.com" />

            <label htmlFor="pack-trigger" className="mt-4 block text-[13px] font-bold text-dls-text-primary">When it is sent</label>
            <Select id="pack-trigger" value={trigger} onChange={(event) => setTrigger(event.target.value)} className="mt-2">
              <option value="Manual">Only when staff press "Send pack now"</option>
              <option value="Complete">When a claim's documents are complete</option>
              <option value="Status">When a claim reaches a status</option>
            </Select>
            {trigger === "Status" && (
              <>
                <label htmlFor="pack-status" className="mt-3 block text-[12.5px] font-semibold text-dls-text-primary">The status that sends it</label>
                <Select id="pack-status" value={status} onChange={(event) => setStatus(event.target.value)} className="mt-1">
                  {STATUSES.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                </Select>
              </>
            )}
            <p className="mt-3 text-[12px] text-dls-text-secondary">
              A pack up to {(settings.maxAttachmentBytes / (1024 * 1024)).toFixed(0)} MB is attached to the email. A larger one is sent as a secure link that works for {settings.linkDays} days and a few downloads. Each reason sends the pack once per claim.
            </p>
            <div className="mt-4 flex flex-wrap items-center gap-3">
              <Button onClick={() => void save()} disabled={saving}>{saving ? "Saving" : "Save"}</Button>
              {saved && <span role="status" className="text-[12.5px] text-dls-text-secondary">Saved.</span>}
              {saveError && <span role="alert" className="text-[12.5px] text-dls-danger-text">{saveError}</span>}
            </div>
          </GlassCard>
        </div>
      )}
    </>
  );
}
