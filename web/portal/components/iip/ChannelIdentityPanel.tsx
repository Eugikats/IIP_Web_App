"use client";

import { useCallback, useEffect, useState } from "react";
import { Link2, Loader2, Unlink, UserRound } from "lucide-react";
import { useUI } from "@/context/UIStateContext";
import { PREVIEW_PERMISSIONS } from "@/lib/roles";
import {
  getChannelIdentityLink, linkChannelIdentity, listIdentityLinkCustomers, revokeChannelIdentity,
  type ChannelIdentityCustomer, type ChannelIdentityLink,
} from "@/lib/channelIdentities";

export default function ChannelIdentityPanel({ channel, threadId }: { channel: string; threadId: string }) {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const canManage = PREVIEW_PERMISSIONS[role].includes("ManageCustomerCommunications")
    && PREVIEW_PERMISSIONS[role].includes("ViewCustomers");
  const [link, setLink] = useState<ChannelIdentityLink | null>(null);
  const [customers, setCustomers] = useState<ChannelIdentityCustomer[]>([]);
  const [customerId, setCustomerId] = useState("");
  const [evidenceReference, setEvidenceReference] = useState("");
  const [auditReason, setAuditReason] = useState("");
  const [open, setOpen] = useState(false);
  const [consented, setConsented] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setLink(await getChannelIdentityLink(role, channel, threadId));
    } catch (loadError) {
      setLink(null);
      setError(loadError instanceof Error ? loadError.message : "Identity link failed to load.");
    } finally {
      setLoading(false);
    }
  }, [role, channel, threadId]);

  useEffect(() => { void load(); }, [load]);

  const beginLink = async () => {
    setOpen(true);
    setError(null);
    if (customers.length) return;
    try {
      setCustomers(await listIdentityLinkCustomers(role));
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "Customers could not be loaded.");
    }
  };

  const saveLink = async () => {
    if (!customerId || !consented || !evidenceReference.trim() || auditReason.trim().length < 8) return;
    setSaving(true);
    setError(null);
    try {
      const saved = await linkChannelIdentity(role, channel, threadId, {
        customerId, customerConsentedToContext: consented,
        evidenceReference: evidenceReference.trim(), auditReason: auditReason.trim(),
      });
      setLink(saved);
      setOpen(false);
      setConsented(false);
      setEvidenceReference("");
      setAuditReason("");
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : "Customer identity could not be linked.");
    } finally {
      setSaving(false);
    }
  };

  const revoke = async () => {
    if (!link || auditReason.trim().length < 8) return;
    setSaving(true);
    setError(null);
    try {
      await revokeChannelIdentity(role, link.linkId, auditReason.trim());
      setLink(null);
      setOpen(false);
      setAuditReason("");
    } catch (revokeError) {
      setError(revokeError instanceof Error ? revokeError.message : "Customer identity link could not be removed.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <section className="border-t border-[#E6EAF2] px-4 py-4" aria-labelledby="channel-identity-heading">
      <div className="flex items-center gap-2">
        <Link2 className="h-4 w-4 text-slate-500" aria-hidden />
        <h2 id="channel-identity-heading" className="m-0 text-[13px] font-semibold text-[#0B1640]">Customer identity</h2>
      </div>
      <p className="mb-0 mt-1 text-[11.5px] leading-relaxed text-slate-600">
        A name or channel handle is never enough. This records a staff-verified identity and explicit consent, without granting claim access.
      </p>

      {loading ? (
        <p className="mb-0 mt-3 flex items-center gap-2 text-[12px] text-slate-600"><Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden />Checking this conversation</p>
      ) : error && !open ? (
        <p className="mb-0 mt-3 text-[12px] text-red-700" role="alert">{error}</p>
      ) : link ? (
        <div className="mt-3 rounded-lg border border-[#E6EAF2] px-3 py-3">
          <p className="mb-0 flex items-center gap-2 text-[12px] font-semibold text-[#0B1640]"><UserRound className="h-3.5 w-3.5 text-slate-500" aria-hidden />{link.customerName}</p>
          <p className="mb-0 mt-1 text-[11px] text-slate-600">{link.hasActiveConsent ? `Verified through a staff-confirmed customer challenge. Consent is active for this ${channel} identity link.` : "Consent is not active for this identity link."}</p>
          {canManage ? <button type="button" onClick={() => { setOpen(true); setError(null); }} className="mt-3 inline-flex min-h-9 items-center gap-2 rounded-md border border-[#DDE1E8] px-3 text-[12px] font-semibold text-slate-700 hover:bg-slate-50"><Unlink className="h-3.5 w-3.5" aria-hidden />{link.hasActiveConsent ? "Unlink customer" : "Remove inactive link"}</button> : null}
        </div>
      ) : (
        <div className="mt-3">
          <p className="mb-0 text-[12px] text-slate-600">No customer is linked to this channel identity.</p>
          {canManage ? <button type="button" onClick={() => void beginLink()} className="mt-3 inline-flex min-h-9 items-center gap-2 rounded-md bg-[#1769FF] px-3 text-[12px] font-semibold text-white hover:bg-[#1258D8]"><Link2 className="h-3.5 w-3.5" aria-hidden />Link customer</button> : null}
        </div>
      )}

      {open ? (
        <div className="mt-3 space-y-3 rounded-lg border border-[#DDE1E8] p-3">
          {link ? (
            <>
              <p className="m-0 text-[12px] font-semibold text-[#0B1640]">Unlink {link.customerName}</p>
              <p className="m-0 text-[11.5px] leading-relaxed text-slate-600">This revokes this channel&apos;s cross-channel context consent. Other channel links remain unchanged.</p>
              <label className="block text-[11px] font-semibold text-slate-700" htmlFor="identity-unlink-reason">Reason for unlinking
                <input id="identity-unlink-reason" value={auditReason} onChange={(event) => setAuditReason(event.target.value)} maxLength={240} className="mt-1 block min-h-9 w-full rounded-md border border-[#DDE1E8] px-2.5 text-[12px] font-normal" />
              </label>
              <div className="flex flex-wrap gap-2">
                <button type="button" disabled={saving || auditReason.trim().length < 8} onClick={() => void revoke()} className="min-h-9 rounded-md bg-[#1769FF] px-3 text-[12px] font-semibold text-white disabled:cursor-not-allowed disabled:opacity-50">{saving ? "Unlinking…" : "Unlink and withdraw consent"}</button>
                <button type="button" disabled={saving} onClick={() => { setOpen(false); setAuditReason(""); setError(null); }} className="min-h-9 rounded-md border border-[#DDE1E8] px-3 text-[12px] font-semibold text-slate-700">Cancel</button>
              </div>
            </>
          ) : (
            <>
              <p className="m-0 text-[12px] font-semibold text-[#0B1640]">Verify and link the customer</p>
              <label className="block text-[11px] font-semibold text-slate-700" htmlFor="identity-customer">Customer
                <select id="identity-customer" value={customerId} onChange={(event) => setCustomerId(event.target.value)} className="mt-1 block min-h-9 w-full rounded-md border border-[#DDE1E8] bg-white px-2.5 text-[12px] font-normal">
                  <option value="">Choose a customer</option>
                  {customers.map((customer) => <option key={customer.customerId} value={customer.customerId}>{customer.fullName}</option>)}
                </select>
              </label>
              <label className="block text-[11px] font-semibold text-slate-700" htmlFor="identity-evidence">Verification evidence reference
                <input id="identity-evidence" value={evidenceReference} onChange={(event) => setEvidenceReference(event.target.value)} maxLength={180} placeholder="Reference to the completed identity challenge" className="mt-1 block min-h-9 w-full rounded-md border border-[#DDE1E8] px-2.5 text-[12px] font-normal" />
              </label>
              <label className="flex items-start gap-2 text-[11.5px] leading-relaxed text-slate-700">
                <input type="checkbox" checked={consented} onChange={(event) => setConsented(event.target.checked)} className="mt-0.5" />
                <span>The customer explicitly agreed to link this conversation with their other IIP conversations.</span>
              </label>
              <label className="block text-[11px] font-semibold text-slate-700" htmlFor="identity-link-reason">Audit reason
                <input id="identity-link-reason" value={auditReason} onChange={(event) => setAuditReason(event.target.value)} maxLength={240} className="mt-1 block min-h-9 w-full rounded-md border border-[#DDE1E8] px-2.5 text-[12px] font-normal" />
              </label>
              {customers.length === 0 && !error ? <p className="m-0 text-[11.5px] text-slate-600">No customers are available in this workspace.</p> : null}
              <div className="flex flex-wrap gap-2">
                <button type="button" disabled={saving || !customerId || !consented || evidenceReference.trim().length < 3 || auditReason.trim().length < 8} onClick={() => void saveLink()} className="min-h-9 rounded-md bg-[#1769FF] px-3 text-[12px] font-semibold text-white disabled:cursor-not-allowed disabled:opacity-50">{saving ? "Saving link…" : "Link verified customer"}</button>
                <button type="button" disabled={saving} onClick={() => { setOpen(false); setError(null); }} className="min-h-9 rounded-md border border-[#DDE1E8] px-3 text-[12px] font-semibold text-slate-700">Cancel</button>
              </div>
            </>
          )}
          {error ? <p className="m-0 text-[12px] text-red-700" role="alert">{error}</p> : null}
        </div>
      ) : null}
    </section>
  );
}
