"use client";

import { useEffect, useMemo, useState } from "react";
import { Bell, Check, Loader2, MessageSquare, Users } from "lucide-react";
import { GovernedActionDialog } from "@/components/GovernedActionDialog";
import { Badge, GlassCard, SectionHeader } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { EmptyStateSurface, ErrorStateSurface, LoadingSkeleton, NoPermissionStateSurface, NotConfiguredStateSurface } from "@/components/ui/StateSurfaces";
import { useUI } from "@/context/UIStateContext";
import { getNotificationRouting, saveNotificationRouting, type NotificationDestination, type NotificationEventType, type NotificationRoutingResponse, type NotificationRoutingRule } from "@/lib/notificationRouting";
import { useRuntimeContext } from "@/lib/runtimeContext";
import { canUseLocalTestDoubles } from "@/lib/config";

const EVENTS: { type: NotificationEventType; label: string; detail: string }[] = [
  { type: "ApprovalAssigned", label: "Approval assigned", detail: "A work item is waiting for an authorized reviewer." },
  { type: "ClaimAssigned", label: "Claim assigned", detail: "A claim has a new owner." },
  { type: "ClaimEscalated", label: "Claim escalated", detail: "A claim needs supervisor attention." },
  { type: "DocumentReviewNeeded", label: "Document review needed", detail: "Extracted information needs a person to check it." },
  { type: "ReviewRequested", label: "Review requested", detail: "A customer or staff member requested a review." },
  { type: "SlaAtRisk", label: "Service standard at risk", detail: "A claim is approaching its configured service standard." },
  { type: "SlaBreach", label: "Service standard breached", detail: "A claim has passed its configured service standard." },
];

const emptyRule = (type: NotificationEventType): NotificationRoutingRule => ({
  type,
  additionalRecipientUserIds: [],
  additionalDestinations: [],
});

export default function NotificationRoutingSettings() {
  const { roleDef } = useUI();
  const { runtimeContext, parityMode } = useRuntimeContext();
  const canConfigure = canUseLocalTestDoubles || parityMode || runtimeContext?.permissions.includes("ConfigureIntegrations") === true;
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [state, setState] = useState<NotificationRoutingResponse | null>(null);
  const [draft, setDraft] = useState<NotificationRoutingRule[]>([]);
  const [saveDialogOpen, setSaveDialogOpen] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function load() {
    setLoading(true);
    setLoadError(null);
    const result = await getNotificationRouting(roleDef.key);
    setLoading(false);
    if (!result.ok) {
      setLoadError(result.error);
      return;
    }
    setState(result.value);
    setDraft(result.value.configuration.rules);
  }

  useEffect(() => { void load(); }, [roleDef.key]);

  const available = useMemo(() => new Set(state?.availableDestinations ?? []), [state?.availableDestinations]);
  const dirty = JSON.stringify(draft) !== JSON.stringify(state?.configuration.rules ?? []);
  const validDraft = draft.every((rule) => rule.additionalDestinations.length > 0
    && (!rule.additionalDestinations.includes("InApp") || rule.additionalRecipientUserIds.length > 0));

  function updateRule(type: NotificationEventType, update: (rule: NotificationRoutingRule) => NotificationRoutingRule) {
    setDraft((current) => {
      const found = current.find((rule) => rule.type === type) ?? emptyRule(type);
      const changed = update(found);
      const isEmpty = changed.additionalRecipientUserIds.length === 0 && changed.additionalDestinations.length === 0;
      return isEmpty
        ? current.filter((rule) => rule.type !== type)
        : [...current.filter((rule) => rule.type !== type), changed].sort((a, b) => EVENTS.findIndex((event) => event.type === a.type) - EVENTS.findIndex((event) => event.type === b.type));
    });
  }

  function toggleDestination(type: NotificationEventType, destination: NotificationDestination) {
    updateRule(type, (rule) => {
      const selected = rule.additionalDestinations.includes(destination);
      const destinations = selected
        ? rule.additionalDestinations.filter((item) => item !== destination)
        : [...rule.additionalDestinations, destination];
      return {
        ...rule,
        additionalDestinations: destinations,
        additionalRecipientUserIds: destination === "InApp" && selected ? [] : rule.additionalRecipientUserIds,
      };
    });
  }

  function toggleRecipient(type: NotificationEventType, userId: string) {
    updateRule(type, (rule) => ({
      ...rule,
      additionalRecipientUserIds: rule.additionalRecipientUserIds.includes(userId)
        ? rule.additionalRecipientUserIds.filter((item) => item !== userId)
        : [...rule.additionalRecipientUserIds, userId],
    }));
  }

  async function save(reason: string) {
    setSaving(true);
    setSaveError(null);
    const result = await saveNotificationRouting(roleDef.key, draft, reason);
    setSaving(false);
    if (!result.ok) {
      setSaveError(result.error);
      return;
    }
    setState(result.value);
    setDraft(result.value.configuration.rules);
    setSaveDialogOpen(false);
  }

  if (!canConfigure) {
    return <NoPermissionStateSurface permission="ConfigureIntegrations" />;
  }

  return (
    <>
      <SectionHeader eyebrow="Settings · Integrations" title="Team notifications" sub="Choose who receives extra alerts and which configured destinations receive them." />
      {loading && !state ? <GlassCard><LoadingSkeleton rows={5} /></GlassCard> : null}
      {loadError && !state ? <GlassCard><ErrorStateSurface message={loadError} onRetry={() => void load()} /></GlassCard> : null}
      {state ? (
        <div className="space-y-4">
          <GlassCard>
            <div className="flex items-start gap-3">
              <div className="grid h-9 w-9 shrink-0 place-items-center rounded-dls-chip bg-dls-bg-page text-dls-text-secondary"><Bell className="h-4 w-4" aria-hidden="true" /></div>
              <div>
                <h2 className="text-[14px] font-bold text-dls-text-primary">How routing works</h2>
                <p className="mt-1 text-[12.5px] leading-5 text-dls-text-secondary">The assigned staff member keeps receiving the existing in-app alert. Rules here add recipients. Teams alerts go to the workspace channel configured for this deployment.</p>
                <p className="mt-2 text-[12px] text-dls-text-muted">Alerts contain a case reference and event type. They do not include documents, customer contact details, reserves, or fraud signals.</p>
                <div className="mt-3 space-y-2">
                  {!available.has("MicrosoftTeams") ? <NotConfiguredStateSurface what="Microsoft Teams requires an HTTPS workspace webhook configured by the deployment." /> : null}
                  <NotConfiguredStateSurface what="Email and WhatsApp delivery are not yet available for internal team alerts. They require verified staff destinations and channel-specific send controls." />
                </div>
              </div>
            </div>
          </GlassCard>

          {state.users.length === 0 ? (
            <GlassCard><EmptyStateSurface title="No active team members" message="Add an active user to this workspace before setting notification recipients." /></GlassCard>
          ) : (
            EVENTS.map((event) => {
              const rule = draft.find((item) => item.type === event.type) ?? emptyRule(event.type);
              const inApp = rule.additionalDestinations.includes("InApp");
              const teams = rule.additionalDestinations.includes("MicrosoftTeams");
              return (
                <GlassCard key={event.type}>
                  <div className="flex flex-col gap-4">
                    <div>
                      <h2 className="text-[14px] font-bold text-dls-text-primary">{event.label}</h2>
                      <p className="mt-1 text-[12px] text-dls-text-secondary">{event.detail}</p>
                    </div>
                    <fieldset className="space-y-3">
                      <legend className="text-[11px] font-bold uppercase tracking-[0.12em] text-dls-text-muted">Destinations</legend>
                      <label className="flex items-start gap-2.5 text-[12.5px] text-dls-text-primary">
                        <input type="checkbox" className="mt-0.5 accent-dls-primary" checked={inApp} onChange={() => toggleDestination(event.type, "InApp")} />
                        <span>In-app alert for additional recipients</span>
                      </label>
                      {available.has("MicrosoftTeams") ? (
                        <label className="flex items-start gap-2.5 text-[12.5px] text-dls-text-primary">
                          <input type="checkbox" className="mt-0.5 accent-dls-primary" checked={teams} onChange={() => toggleDestination(event.type, "MicrosoftTeams")} />
                          <span className="inline-flex items-center gap-1.5"><MessageSquare className="h-3.5 w-3.5 text-dls-text-muted" aria-hidden="true" /> Microsoft Teams workspace channel</span>
                        </label>
                      ) : null}
                    </fieldset>
                    <fieldset className="space-y-2" disabled={!inApp}>
                      <legend className="mb-2 inline-flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-[0.12em] text-dls-text-muted"><Users className="h-3.5 w-3.5" aria-hidden="true" /> Additional in-app recipients</legend>
                      <div className="space-y-2">
                        {state.users.map((user) => (
                          <label key={user.userId} className={`flex items-start gap-2.5 text-[12.5px] ${inApp ? "text-dls-text-primary" : "text-dls-text-muted"}`}>
                            <input type="checkbox" className="mt-0.5 accent-dls-primary" checked={rule.additionalRecipientUserIds.includes(user.userId)} disabled={!inApp} onChange={() => toggleRecipient(event.type, user.userId)} />
                            <span>{user.name}<span className="ml-1.5 text-dls-text-muted">{user.role}</span></span>
                          </label>
                        ))}
                      </div>
                      {inApp && rule.additionalRecipientUserIds.length === 0 ? <p className="text-[12px] text-dls-warning-text">Select at least one active recipient for in-app alerts.</p> : null}
                    </fieldset>
                  </div>
                </GlassCard>
              );
            })
          )}

          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex items-center gap-2 text-[12px] text-dls-text-secondary" role="status" aria-live="polite">
              {dirty ? <Badge tone="warning">Unsaved changes</Badge> : <Badge tone="success"><Check className="mr-1 inline h-3 w-3" aria-hidden="true" /> Saved</Badge>}
              {saving ? <span className="inline-flex items-center gap-1.5"><Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden="true" /> Saving</span> : null}
            </div>
            <Button variant="primary" disabled={!dirty || !validDraft || saving || state.users.length === 0} onClick={() => { setSaveError(null); setSaveDialogOpen(true); }}>Save notification rules</Button>
          </div>
        </div>
      ) : null}
      <GovernedActionDialog
        open={saveDialogOpen}
        title="Save notification rules"
        target="Notification routing in this workspace"
        impact="The selected active team members and configured Teams channel will receive additional event alerts."
        auditNotice="This change is recorded in the workspace audit trail."
        confirmLabel="Save rules"
        onClose={() => setSaveDialogOpen(false)}
        onConfirm={save}
        evidence={saveError ? <p className="text-[12.5px] text-dls-danger-text" role="alert">{saveError}</p> : undefined}
      />
    </>
  );
}
