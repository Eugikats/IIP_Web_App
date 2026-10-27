"use client";

import { useCallback, useEffect, useState } from "react";
import { GlassCard } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Field, Input } from "@/components/ui/Input";
import { ErrorStateSurface, LoadingSkeleton, NoPermissionStateSurface } from "@/components/ui/StateSurfaces";
import { useUI } from "@/context/UIStateContext";
import { assignWhatsAppNumber, getWhatsAppSupportQueue, type WhatsAppSupportQueue } from "@/lib/orgSetup";

type QueueItem = WhatsAppSupportQueue["items"][number];

function SupportRequest({ item, role, available, onAssigned }: { item: QueueItem; role: string; available: boolean; onAssigned: () => void }) {
  const { tenantId, workspaceId, request } = item;
  const [wabaId, setWabaId] = useState("");
  const [phoneNumberId, setPhoneNumberId] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function assign(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    const result = await assignWhatsAppNumber(role, tenantId, workspaceId, wabaId, phoneNumberId);
    setBusy(false);
    if (!result.ok) { setError(result.error); return; }
    onAssigned();
  }

  return <li className="list-none"><GlassCard>
    <h2 className="m-0 text-[15px] font-semibold text-dls-text-primary">{request.displayName} · {request.status}</h2>
    <p className="mb-0 mt-1 text-[13px] text-dls-text-secondary">{request.phoneNumber} · {request.contactEmail}</p>
    <p className="mb-0 mt-1 break-all text-[12px] text-dls-text-muted">{tenantId} / {workspaceId}</p>
    {request.status === "Requested" || request.status === "NeedsAttention" ? (
      available ? <form onSubmit={assign} className="mt-3 grid gap-2 border-t border-dls-border-subtle pt-3 sm:grid-cols-2" aria-label={`Assign Meta number for ${request.displayName}`}>
        <Field label="WhatsApp Business Account ID" htmlFor={`waba-${tenantId}`}><Input id={`waba-${tenantId}`} required value={wabaId} onChange={(event) => setWabaId(event.target.value)} /></Field>
        <Field label="Meta phone number ID" htmlFor={`phone-${tenantId}`}><Input id={`phone-${tenantId}`} required value={phoneNumberId} onChange={(event) => setPhoneNumberId(event.target.value)} /></Field>
        <Button type="submit" variant="primary" disabled={busy}>Confirm number with Meta</Button>
        {error ? <p className="m-0 text-[13px] text-dls-danger-text" role="alert">{error}</p> : null}
      </form> : <p className="mb-0 mt-2 text-[12.5px] text-dls-text-secondary">Managed Meta credentials are Not configured.</p>
    ) : <p className="mb-0 mt-2 text-[12.5px] text-dls-text-secondary">Waiting for the organisation to complete verification.</p>}
  </GlassCard></li>;
}

export default function WhatsAppSupport() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [queue, setQueue] = useState<WhatsAppSupportQueue | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [forbidden, setForbidden] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    const result = await getWhatsAppSupportQueue(role);
    setLoading(false);
    if (!result.ok) { setForbidden(result.forbidden); setError(result.forbidden ? null : result.error); return; }
    setForbidden(false);
    setError(null);
    setQueue(result.value);
  }, [role]);
  useEffect(() => { void load(); }, [load]);

  if (loading) return <LoadingSkeleton />;
  if (forbidden) return <NoPermissionStateSurface />;
  if (error) return <ErrorStateSurface title="The WhatsApp support queue could not be loaded" message={error} onRetry={() => void load()} />;

  return <main className="mx-auto w-full max-w-4xl space-y-4 px-4 py-6">
    <header><p className="m-0 text-[12px] text-dls-text-muted">Admin · Support</p><h1 className="m-0 mt-1 text-[22px] font-bold text-dls-text-primary">WhatsApp number requests</h1><p className="mb-0 mt-1 text-[13px] text-dls-text-secondary">Requests from organisations. Meta verification: <strong>{queue?.metaVerification ?? "Not configured"}</strong>. Codes and registration PINs are never shown here or stored.</p></header>
    {queue === null ? <ErrorStateSurface title="The queue is unavailable" message="The request list could not be loaded." onRetry={() => void load()} /> : queue.items.length === 0 ? <GlassCard><p className="m-0 text-[13px] text-dls-text-secondary">No open WhatsApp setup requests.</p></GlassCard> : <ul className="m-0 space-y-3 p-0" aria-label="WhatsApp setup support queue">{queue.items.map((item) => <SupportRequest key={`${item.tenantId}:${item.workspaceId}`} item={item} role={role} available={queue.metaVerification === "Available"} onAssigned={() => void load()} />)}</ul>}
  </main>;
}
