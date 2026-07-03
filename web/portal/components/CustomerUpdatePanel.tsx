import { StatusBadge } from "./Badges";
import { allowsAction, blockedActionReason, type ActionStateContract } from "@/lib/actionState";
import type { ChannelSendAvailability } from "@/lib/providerHealth";

export type CustomerUpdate = ActionStateContract & {
  updateId: string;
  reasonCategory: string;
  channel: string;
  status: string;
  messageBody: string;
  requiresApproval: boolean;
  deliveryStatus: string | null;
  updatedAt: string;
};

export function customerUpdateAllows(update: CustomerUpdate, action: string) {
  return allowsAction(update, action);
}

export function customerUpdateBlockedReason(update: CustomerUpdate, action: string) {
  return blockedActionReason(update, action);
}

export function CustomerUpdatePanel({
  updates,
  onApprove,
  onSend,
  getSendAvailability
}: {
  updates: CustomerUpdate[];
  onApprove?: (update: CustomerUpdate) => Promise<void> | void;
  onSend?: (update: CustomerUpdate) => Promise<void> | void;
  getSendAvailability?: (update: CustomerUpdate) => ChannelSendAvailability;
}) {
  if (updates.length === 0) {
    return <p className="rounded-xl border border-dashed border-line bg-mist p-4 text-sm text-muted">No customer updates drafted yet.</p>;
  }

  return (
    <div className="space-y-3">
      {updates.map((update) => {
        const sendAvailability = getSendAvailability?.(update) ?? { enabled: true, reason: "", recipientField: null };
        const backendSendReason = customerUpdateBlockedReason(update, "sendCustomerUpdate");
        const providerSendReason = onSend && customerUpdateAllows(update, "sendCustomerUpdate") && !sendAvailability.enabled
          ? sendAvailability.reason
          : null;

        return (
        <article key={update.updateId} className="rounded-xl border border-line bg-mist p-4 transition hover:-translate-y-0.5 hover:border-accent/40 hover:shadow-glow-cyan">
          <div className="flex flex-wrap items-start justify-between gap-2">
            <div>
              <p className="font-semibold text-ink font-mono text-sm">{update.reasonCategory} / {update.channel}</p>
              <p className="mt-2 text-[10px] font-bold uppercase tracking-[0.2em] text-muted">Updated {new Date(update.updatedAt).toLocaleString()}</p>
            </div>
            <StatusBadge label={update.status} />
          </div>
          <p className="mt-3 text-sm leading-6 text-muted">{update.messageBody}</p>
          <div className="mt-4 flex flex-wrap gap-2">
            {onApprove && customerUpdateAllows(update, "approveCustomerUpdate") ? (
              <button className="rounded-full bg-azure border border-azure/40 px-4 py-2 text-sm font-semibold text-white shadow-glow-cyan hover:brightness-110 transition-all" onClick={() => onApprove(update)}>
                Approve update
              </button>
            ) : null}
            {onSend && customerUpdateAllows(update, "sendCustomerUpdate") ? (
              <button
                className="rounded-full border border-line bg-surface px-4 py-2 text-sm font-semibold text-muted hover:text-ink hover:border-accent/50 transition-all disabled:cursor-not-allowed disabled:opacity-50"
                disabled={!sendAvailability.enabled}
                title={providerSendReason ?? undefined}
                onClick={() => onSend(update)}
              >
                Send update
              </button>
            ) : null}
          </div>
          {backendSendReason || providerSendReason ? (
            <p className="mt-3 rounded-xl border border-warning/30 bg-warning/10 px-3 py-2 text-xs font-semibold text-warning">{backendSendReason ?? providerSendReason}</p>
          ) : null}
        </article>
        );
      })}
    </div>
  );
}
