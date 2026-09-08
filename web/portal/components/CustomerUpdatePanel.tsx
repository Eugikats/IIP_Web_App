import { StatusBadge } from "./Badges";
import { Button } from "@/components/ui/Button";
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
    return <p className="rounded-dls-card border border-dashed border-dls-border-subtle bg-dls-surface p-4 text-sm text-dls-text-muted">No customer updates drafted yet.</p>;
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
        <article key={update.updateId} className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4">
          <div className="flex flex-wrap items-start justify-between gap-2">
            <div>
              <p className="font-mono text-sm font-semibold text-dls-text-primary">{update.reasonCategory} / {update.channel}</p>
              <p className="mt-2 text-[10px] font-bold uppercase tracking-[0.2em] text-dls-text-muted">Updated {new Date(update.updatedAt).toLocaleString()}</p>
            </div>
            <StatusBadge label={update.status} />
          </div>
          <p className="mt-3 text-sm leading-6 text-dls-text-secondary">{update.messageBody}</p>
          <div className="mt-4 flex flex-wrap gap-2">
            {onApprove && customerUpdateAllows(update, "approveCustomerUpdate") ? (
              <Button variant="primary" onClick={() => onApprove(update)}>Approve update</Button>
            ) : null}
            {onSend && customerUpdateAllows(update, "sendCustomerUpdate") ? (
              <Button variant="secondary" disabled={!sendAvailability.enabled} title={providerSendReason ?? undefined} onClick={() => onSend(update)}>
                Send update
              </Button>
            ) : null}
          </div>
          {backendSendReason || providerSendReason ? (
            <p className="mt-3 rounded-dls-control border border-dls-warning-dot bg-dls-warning-tint px-3 py-2 text-xs font-semibold text-dls-warning-text">{backendSendReason ?? providerSendReason}</p>
          ) : null}
        </article>
        );
      })}
    </div>
  );
}
