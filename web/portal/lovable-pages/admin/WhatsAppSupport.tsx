"use client";

import { useCallback, useEffect, useState } from "react";
import { GlassCard } from "@/components/iip/Primitives";
import { ErrorStateSurface, LoadingSkeleton, NoPermissionStateSurface } from "@/components/ui/StateSurfaces";
import { useUI } from "@/context/UIStateContext";
import { getWhatsAppSupportQueue, type WhatsAppSupportQueue } from "@/lib/orgSetup";

export default function WhatsAppSupport() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [items, setItems] = useState<WhatsAppSupportQueue["items"] | null>(null);
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
    setItems(result.value.items);
  }, [role]);
  useEffect(() => { void load(); }, [load]);

  if (loading) return <LoadingSkeleton />;
  if (forbidden) return <NoPermissionStateSurface />;
  if (error) return <ErrorStateSurface title="The WhatsApp support queue could not be loaded" message={error} onRetry={() => void load()} />;

  return <main className="mx-auto w-full max-w-4xl space-y-4 px-4 py-6">
    <header><p className="m-0 text-[12px] text-dls-text-muted">Admin · Support</p><h1 className="m-0 mt-1 text-[22px] font-bold text-dls-text-primary">WhatsApp number requests</h1><p className="mb-0 mt-1 text-[13px] text-dls-text-secondary">Requests from organisations. Meta verification: <strong>Not configured</strong>. Codes and registration PINs are never shown here or stored.</p></header>
    {items === null ? <ErrorStateSurface title="The queue is unavailable" message="The request list could not be loaded." onRetry={() => void load()} /> : items.length === 0 ? <GlassCard><p className="m-0 text-[13px] text-dls-text-secondary">No open WhatsApp setup requests.</p></GlassCard> : <ul className="m-0 space-y-3 p-0" aria-label="WhatsApp setup support queue">{items.map(({ tenantId, workspaceId, request }) => <li key={`${tenantId}:${workspaceId}`} className="list-none"><GlassCard><h2 className="m-0 text-[15px] font-semibold text-dls-text-primary">{request.displayName} · {request.status}</h2><p className="mb-0 mt-1 text-[13px] text-dls-text-secondary">{request.phoneNumber} · {request.contactEmail}</p><p className="mb-0 mt-1 text-[12px] text-dls-text-muted">{tenantId} / {workspaceId}</p></GlassCard></li>)}</ul>}
  </main>;
}
