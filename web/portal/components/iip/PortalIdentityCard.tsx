"use client";

import { useCallback, useEffect, useState } from "react";
import { GlassCard } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { LoadingSkeleton } from "@/components/ui/StateSurfaces";
import { useUI } from "@/context/UIStateContext";
import {
  loadPortalIdentity, rotateSmsWebhook, savePortalIdentity, verifyPortalHostname,
  type PortalIdentity, type PortalIdentityInput,
} from "@/lib/portalIdentity";

const toDraft = (identity: PortalIdentity): PortalIdentityInput => ({
  slug: identity.slug ?? "",
  displayName: identity.displayName ?? "",
  accentColour: identity.accentColour ?? "",
  logoUrl: identity.logoUrl ?? "",
  supportPhone: identity.supportPhone ?? "",
  supportEmail: identity.supportEmail ?? "",
  hostname: identity.hostname ?? "",
});

/**
 * How this insurer's customers reach it and what their pages show (build-loop item 29): a short name in their links, optionally the
 * insurer's own web address, the name, logo and colour on the customer pages, who to call, and the secret address the SMS gateway reports
 * delivery to. Saved on its own button, separate from the operating settings.
 */
export function PortalIdentityCard() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [identity, setIdentity] = useState<PortalIdentity | null>(null);
  const [draft, setDraft] = useState<PortalIdentityInput | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [message, setMessage] = useState<{ tone: "ok" | "error"; text: string } | null>(null);
  const [busy, setBusy] = useState(false);
  const [callbackUrl, setCallbackUrl] = useState<string | null>(null);

  const load = useCallback(async () => {
    const result = await loadPortalIdentity(role);
    if (!result.ok) {
      setLoadError(result.error);
      return;
    }
    setIdentity(result.value);
    setDraft(toDraft(result.value));
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  if (loadError) {
    return (
      <GlassCard>
        <div className="text-[14px] font-bold text-dls-text-primary">Customer portal</div>
        <p role="alert" className="mt-2 text-[12.5px] text-dls-danger-text">{loadError}</p>
      </GlassCard>
    );
  }

  if (!identity || !draft) {
    return <LoadingSkeleton />;
  }

  const set = (patch: Partial<PortalIdentityInput>) => setDraft({ ...draft, ...patch });
  const saved = toDraft(identity);
  const dirty = (Object.keys(saved) as (keyof PortalIdentityInput)[]).some((key) => saved[key] !== draft[key]);

  async function onSave() {
    setBusy(true);
    setMessage(null);
    const result = await savePortalIdentity(role, draft!);
    setBusy(false);
    if (!result.ok) return setMessage({ tone: "error", text: result.error });
    setIdentity(result.value);
    setDraft(toDraft(result.value));
    setMessage({ tone: "ok", text: "Customer portal details saved." });
  }

  async function onVerify() {
    setBusy(true);
    setMessage(null);
    const result = await verifyPortalHostname(role);
    setBusy(false);
    if (!result.ok) return setMessage({ tone: "error", text: result.error });
    setIdentity(result.value);
    setMessage({ tone: "ok", text: "The web address is verified. Customers are now sent there." });
  }

  async function onRotate() {
    setBusy(true);
    setMessage(null);
    const result = await rotateSmsWebhook(role);
    setBusy(false);
    if (!result.ok) return setMessage({ tone: "error", text: result.error });
    setCallbackUrl(result.value.callbackUrl);
    void load();
  }

  return (
    <GlassCard>
      <div className="text-[14px] font-bold text-dls-text-primary" data-testid="portal-identity-card">Customer portal</div>
      <p className="mt-1 text-[12px] text-dls-text-secondary">
        How your customers reach their claim pages, and what those pages show. Customer links carry your link name, so the page knows it is yours.
      </p>

      <label htmlFor="portal-slug" className="mb-1 mt-3 block text-[12.5px] font-semibold text-dls-text-primary">Link name</label>
      <Input id="portal-slug" value={draft.slug} maxLength={40} placeholder="for example acme-insurance" onChange={(e) => set({ slug: e.target.value.toLowerCase() })} />
      <p className="mt-1 text-[11.5px] text-dls-text-muted">Lower-case letters, digits and hyphens. It appears in the links customers receive.</p>

      <label htmlFor="portal-name" className="mb-1 mt-3 block text-[12.5px] font-semibold text-dls-text-primary">Name customers know you by</label>
      <Input id="portal-name" value={draft.displayName} maxLength={80} onChange={(e) => set({ displayName: e.target.value })} />

      <div className="mt-3 grid gap-3 sm:grid-cols-2">
        <div>
          <label htmlFor="portal-colour" className="mb-1 block text-[12.5px] font-semibold text-dls-text-primary">Button colour</label>
          <Input id="portal-colour" value={draft.accentColour} maxLength={7} placeholder="#1F5EFF" onChange={(e) => set({ accentColour: e.target.value })} />
        </div>
        <div>
          <label htmlFor="portal-logo" className="mb-1 block text-[12.5px] font-semibold text-dls-text-primary">Logo address</label>
          <Input id="portal-logo" value={draft.logoUrl} maxLength={500} placeholder="https://..." onChange={(e) => set({ logoUrl: e.target.value })} />
        </div>
        <div>
          <label htmlFor="portal-phone" className="mb-1 block text-[12.5px] font-semibold text-dls-text-primary">Support phone</label>
          <Input id="portal-phone" value={draft.supportPhone} maxLength={40} onChange={(e) => set({ supportPhone: e.target.value })} />
        </div>
        <div>
          <label htmlFor="portal-email" className="mb-1 block text-[12.5px] font-semibold text-dls-text-primary">Support email</label>
          <Input id="portal-email" value={draft.supportEmail} maxLength={120} onChange={(e) => set({ supportEmail: e.target.value })} />
        </div>
      </div>

      <label htmlFor="portal-host" className="mb-1 mt-3 block text-[12.5px] font-semibold text-dls-text-primary">Your own web address (optional)</label>
      <Input id="portal-host" value={draft.hostname} maxLength={253} placeholder="claims.yourcompany.com" onChange={(e) => set({ hostname: e.target.value.toLowerCase() })} />
      {identity.hostname && !identity.hostnameVerified && identity.hostnameChallenge && (
        <div className="mt-2 rounded-dls-control border border-dls-border-subtle bg-dls-surface p-3 text-[12px] text-dls-text-secondary" data-testid="portal-hostname-steps">
          <p>Not verified yet, so customers are still sent to the shared portal. To prove the address is yours:</p>
          <ol className="mt-1 list-decimal pl-5">
            <li>Point the address at this platform (ask whoever manages your domain).</li>
            <li>Make <span className="font-mono">https://{identity.hostname}{identity.challengePath}</span> show exactly this text: <span className="font-mono font-semibold">{identity.hostnameChallenge}</span></li>
            <li>Press Verify.</li>
          </ol>
          <div className="mt-2">
            <Button variant="secondary" onClick={() => void onVerify()} disabled={busy || dirty}>Verify the address</Button>
          </div>
        </div>
      )}
      {identity.hostname && identity.hostnameVerified && <p className="mt-1 text-[12px] text-dls-success-text">Verified. Customers are sent to https://{identity.hostname}.</p>}

      {identity.exampleLink && (
        <p className="mt-3 break-all text-[11.5px] text-dls-text-muted" data-testid="portal-example-link">A customer link looks like: {identity.exampleLink}</p>
      )}

      <div className="mt-3 flex flex-wrap items-center gap-3">
        <Button variant="primary" onClick={() => void onSave()} disabled={!dirty || busy}>{busy ? "Working..." : "Save customer portal"}</Button>
        {message && <span role={message.tone === "error" ? "alert" : "status"} className={`text-[12.5px] ${message.tone === "error" ? "text-dls-danger-text" : "text-dls-success-text"}`}>{message.text}</span>}
      </div>

      <div className="mt-5 border-t border-dls-border-subtle pt-3">
        <div className="text-[13px] font-semibold text-dls-text-primary">SMS delivery reports</div>
        <p className="mt-1 text-[12px] text-dls-text-secondary">
          Give your SMS gateway a secret callback address so its delivery reports reach only your workspace. The address is shown once; making a new one stops the old one working.
        </p>
        <div className="mt-2">
          <Button variant="secondary" onClick={() => void onRotate()} disabled={busy || !identity.slug}>
            {identity.smsWebhookConfigured ? "Make a new callback address" : "Make a callback address"}
          </Button>
        </div>
        {callbackUrl && (
          <p className="mt-2 break-all rounded-dls-control border border-dls-border-subtle bg-dls-surface p-2 font-mono text-[11.5px]" data-testid="portal-sms-callback">{callbackUrl}</p>
        )}
      </div>
    </GlassCard>
  );
}
