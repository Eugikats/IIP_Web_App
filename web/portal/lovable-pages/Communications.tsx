"use client";
import { Link } from "@/components/iip/NextRouterCompat";
import { useMemo, useState } from "react";
import { SectionHeader, GlassCard, Badge, StatusBadge } from "@/components/iip/Primitives";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { GovernedActionDialog } from "@/components/GovernedActionDialog";
import { ExternalLink } from "lucide-react";
import { toast } from "sonner";
import { useCustomerUpdateQueue, type CustomerUpdateQueueItem } from "@/lib/lovable-parity/data/communications";

type PendingAction = { kind: "approve" | "reject" | "send"; item: CustomerUpdateQueueItem };

const TABS = ["Pending Approval", "Approved", "Sent", "Rejected", "All"] as const;

export default function Communications() {
  const { items, loading, error, parityMode, approve, reject, send } = useCustomerUpdateQueue();
  const [tab, setTab] = useState<(typeof TABS)[number]>("Pending Approval");
  const [pending, setPending] = useState<PendingAction | null>(null);
  const filtered = useMemo(
    () => (tab === "All" ? items : items.filter((item) => item.status === tab || (tab === "Sent" && item.status === "Send Failed"))),
    [items, tab],
  );

  function allows(item: CustomerUpdateQueueItem, action: string) {
    return item.allowedActions.some((a) => a.action === action);
  }

  function blockedReason(item: CustomerUpdateQueueItem, action: string) {
    return item.blockedActions.find((a) => a.action === action)?.reason ?? null;
  }

  async function handleConfirm(auditReason: string) {
    if (!pending) return;
    const { kind, item } = pending;
    const result = kind === "approve" ? await approve(item.updateId, auditReason)
      : kind === "reject" ? await reject(item.updateId, auditReason)
      : await send(item, auditReason);
    if (result.ok) {
      toast.success(kind === "approve" ? "Update approved." : kind === "reject" ? "Update rejected." : "Update sent.");
    } else {
      toast.error(result.message ?? `${kind} failed.`);
    }
    setPending(null);
  }

  return (
    <>
      <SectionHeader
        eyebrow="Customer Communications"
        title="Customer update queue"
        sub="AI drafts customer-safe messages. Every send requires named human approval — no sensitive message is dispatched automatically."
      />
      <div className="mb-4"><GuardrailBanner text="Drafts must not promise settlement, coverage, or payment. Approved drafts are dispatched via the configured provider and logged in Audit." /></div>

      <div className="flex flex-wrap gap-1.5 mb-4">
        {TABS.map((t) => (
          <button key={t} onClick={() => setTab(t)} className={`text-[12px] rounded-full px-3 py-1.5 border ${tab === t ? "bg-navy text-white border-navy" : "bg-white text-slate2 border-cloud"}`}>{t}</button>
        ))}
      </div>

      {loading ? (
        <GlassCard><div className="p-6 text-center text-[13px] text-stone">Loading customer updates…</div></GlassCard>
      ) : items.length === 0 ? (
        <GlassCard>
          <div className="p-10 text-center text-stone">
            <div className="text-navy font-medium">No customer updates</div>
            <div className="text-[12.5px] mt-1">{error ? error : "Customer-update drafts appear here once created from a claim."}</div>
          </div>
        </GlassCard>
      ) : (
        <GlassCard className="!p-0 overflow-hidden">
          <table className="w-full text-[13px]">
            <thead className="bg-white/60">
              <tr className="text-left text-[11px] uppercase tracking-[0.14em] text-stone">
                <th className="p-3">Draft</th><th className="p-3">Channel</th><th className="p-3">Status</th><th className="p-3">Updated</th><th className="p-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((item) => {
                const approveBlocked = blockedReason(item, "approveCustomerUpdate");
                const sendBlocked = blockedReason(item, "sendCustomerUpdate");
                return (
                  <tr key={item.updateId} className="border-t border-cloud/70 hover:bg-white/50 align-top">
                    <td className="p-3">
                      <div className="text-navy font-medium">{item.reasonCategory} · {item.customerName}</div>
                      <div className="text-[11.5px] text-stone truncate max-w-[420px]">{item.messageBody}</div>
                      <Link to={`/app/claims/${item.caseId}`} className="mt-0.5 inline-flex items-center gap-1 text-[11px] text-info"><ExternalLink className="h-3 w-3" /> {item.caseId.slice(0, 8)}</Link>
                    </td>
                    <td className="p-3"><Badge tone="info">{item.channel}</Badge></td>
                    <td className="p-3"><StatusBadge status={item.status} /></td>
                    <td className="p-3 text-stone">{parityMode ? item.updatedAt : new Date(item.updatedAt).toLocaleString(undefined, { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" })}</td>
                    <td className="p-3">
                      <div className="flex justify-end gap-1.5 flex-wrap">
                        {allows(item, "approveCustomerUpdate") && (
                          <button onClick={() => setPending({ kind: "approve", item })} className="rounded-lg border border-cloud bg-white px-2.5 py-1.5 text-[11.5px] font-medium text-navy hover:bg-cloud/40">Approve</button>
                        )}
                        {allows(item, "approveCustomerUpdate") && (
                          <button onClick={() => setPending({ kind: "reject", item })} className="rounded-lg border border-cloud bg-white px-2.5 py-1.5 text-[11.5px] font-medium text-danger hover:bg-danger/5">Reject</button>
                        )}
                        {allows(item, "sendCustomerUpdate") && (
                          <button onClick={() => setPending({ kind: "send", item })} className="rounded-lg gradient-primary px-2.5 py-1.5 text-[11.5px] font-semibold text-white">Send</button>
                        )}
                        {!allows(item, "approveCustomerUpdate") && !allows(item, "sendCustomerUpdate") && (
                          <span className="text-[11px] text-stone" title={approveBlocked ?? sendBlocked ?? undefined}>{approveBlocked ?? sendBlocked ?? "No action available"}</span>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          {filtered.length === 0 && <div className="p-8 text-center text-stone text-[13px]">No drafts in this state.</div>}
        </GlassCard>
      )}

      <GovernedActionDialog
        open={!!pending}
        title={pending?.kind === "approve" ? "Approve customer update" : pending?.kind === "reject" ? "Reject customer update" : "Send customer update"}
        target={pending ? `${pending.item.customerName} · ${pending.item.channel} · ${pending.item.reasonCategory}` : ""}
        impact={
          pending?.kind === "send"
            ? "This dispatches the message to the customer through the configured provider immediately. This cannot be undone."
            : pending?.kind === "approve"
              ? "This marks the draft as approved and makes it eligible to send."
              : "This marks the draft as rejected. It will not be sendable."
        }
        auditNotice="This action is written to the audit trail with your reason and cannot be undone silently."
        confirmLabel={pending?.kind === "approve" ? "Approve" : pending?.kind === "reject" ? "Reject" : "Send"}
        tone={pending?.kind === "reject" ? "danger" : "primary"}
        onClose={() => setPending(null)}
        onConfirm={handleConfirm}
      />
    </>
  );
}
