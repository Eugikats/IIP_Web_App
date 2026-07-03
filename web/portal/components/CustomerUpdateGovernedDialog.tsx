"use client";

import { GovernedActionDialog } from "@/components/GovernedActionDialog";
import type { CustomerUpdate } from "@/components/CustomerUpdatePanel";
import type { ChannelSendAvailability } from "@/lib/providerHealth";

export type PendingCustomerUpdateAction = {
  action: "approve" | "send";
  update: CustomerUpdate;
  recipient?: string | null;
  availability?: ChannelSendAvailability | null;
};

export function CustomerUpdateGovernedDialog({
  pendingAction,
  onClose,
  onConfirm
}: {
  pendingAction: PendingCustomerUpdateAction | null;
  onClose: () => void;
  onConfirm: (reason: string) => Promise<void> | void;
}) {
  if (!pendingAction) {
    return null;
  }

  return (
    <GovernedActionDialog
      open={pendingAction !== null}
      title={customerUpdateActionTitle(pendingAction)}
      target={`${pendingAction.update.reasonCategory} update (${pendingAction.update.updateId})`}
      impact={customerUpdateActionImpact(pendingAction)}
      auditNotice="This is a human-governed customer communication action. It must remain customer-safe, provider-backed when sent, and auditable."
      confirmLabel={pendingAction.action === "approve" ? "Approve update" : "Send update"}
      evidence={<CustomerUpdateActionEvidence pendingAction={pendingAction} />}
      onClose={onClose}
      onConfirm={onConfirm}
    />
  );
}

function CustomerUpdateActionEvidence({ pendingAction }: { pendingAction: PendingCustomerUpdateAction }) {
  const update = pendingAction.update;

  return (
    <div className="rounded-xl border border-line bg-mist p-4 text-sm leading-6 text-muted">
      <p className="text-[10px] font-bold uppercase tracking-[0.2em]">Communication context</p>
      <dl className="mt-3 grid gap-2 sm:grid-cols-2">
        <EvidenceItem label="Channel" value={update.channel} />
        <EvidenceItem label="Status" value={update.status} />
        <EvidenceItem label="Approval required" value={update.requiresApproval ? "Yes" : "No"} />
        <EvidenceItem label="Delivery status" value={update.deliveryStatus ?? "Not sent"} />
        {pendingAction.action === "send" ? (
          <>
            <EvidenceItem label="Recipient" value={pendingAction.recipient ?? "Not configured"} />
            <EvidenceItem label="Provider check" value={pendingAction.availability?.enabled ? "Configured" : pendingAction.availability?.reason ?? "Not checked"} />
          </>
        ) : null}
      </dl>
      <div className="mt-3 rounded-lg border border-line bg-surface/70 p-3">
        <p className="text-[10px] font-bold uppercase tracking-[0.18em] opacity-70">Message preview</p>
        <p className="mt-1 whitespace-pre-wrap text-ink">{update.messageBody}</p>
      </div>
    </div>
  );
}

function EvidenceItem({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-[10px] font-bold uppercase tracking-[0.18em] opacity-70">{label}</dt>
      <dd className="mt-1 break-words font-mono font-semibold text-ink">{value}</dd>
    </div>
  );
}

function customerUpdateActionTitle(pendingAction: PendingCustomerUpdateAction) {
  return pendingAction.action === "approve"
    ? "Approve customer update draft"
    : `Send customer update by ${pendingAction.update.channel}`;
}

function customerUpdateActionImpact(pendingAction: PendingCustomerUpdateAction) {
  if (pendingAction.action === "approve") {
    return "Approves this drafted customer-facing message for workflow use. It does not send the update.";
  }

  return "Sends the customer update through the configured provider and records the communication result. It does not change claim outcome, settlement, or payment state.";
}
