"use client";

import { FormEvent, useState } from "react";
import { StatusBadge } from "@/components/Badges";
import { CustomerUpdatePanel, customerUpdateAllows, type CustomerUpdate } from "@/components/CustomerUpdatePanel";
import { CustomerUpdateGovernedDialog, type PendingCustomerUpdateAction } from "@/components/CustomerUpdateGovernedDialog";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { SideDrawer } from "@/components/SideDrawer";
import { EmptyState, ErrorState } from "@/components/States";
import { useRole } from "@/components/RoleContext";
import { apiBaseUrl, canUseLocalTestDoubles, scopedHeaders } from "@/lib/config";
import { hasActionState } from "@/lib/actionState";
import { customerUpdateSendAvailability, type ProviderHealth } from "@/lib/providerHealth";
import { motion } from "framer-motion";

type CaseDetail = {
  caseId: string;
  customerName: string;
  customerEmail: string | null;
  customerPhone: string | null;
  policyNumber: string;
  productLine: string;
  status: string;
};

type Checklist = {
  missingRequiredDocuments: Array<{ documentType: string; label: string }>;
};

type Communication = {
  communicationId: string;
  channel: string;
  direction?: string;
  messageBody: string;
  deliveryStatus: string;
  createdAt: string;
};

export function CustomerServiceClient() {
  const { role, userId, can } = useRole();
  const [caseId, setCaseId] = useState("");
  const [caseDetail, setCaseDetail] = useState<CaseDetail | null>(null);
  const [checklist, setChecklist] = useState<Checklist | null>(null);
  const [updates, setUpdates] = useState<CustomerUpdate[]>([]);
  const [communications, setCommunications] = useState<Communication[]>([]);
  const [providerHealth, setProviderHealth] = useState<ProviderHealth | null>(null);
  const [sendChannel, setSendChannel] = useState("Email");
  const [error, setError] = useState<string | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const [selectedUpdate, setSelectedUpdate] = useState<CustomerUpdate | null>(null);
  const [selectedCommunication, setSelectedCommunication] = useState<Communication | null>(null);
  const [pendingCustomerUpdateAction, setPendingCustomerUpdateAction] = useState<PendingCustomerUpdateAction | null>(null);
  const canManageCustomerCommunications = can("ManageCustomerCommunications");
  const canSendCustomerUpdates = can("SendCustomerUpdate");

  async function openCase(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setStatus("Loading customer-safe case view...");
    const headers = scopedHeaders(role, userId);
    const [caseResponse, checklistResponse, updatesResponse, providerResponse] = await Promise.all([
      fetch(`${apiBaseUrl}/api/v1/cases/${caseId}`, { headers }),
      fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/document-checklist`, { headers }),
      fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/customer-updates`, { headers }),
      fetch(`${apiBaseUrl}/api/v1/provider-status`, { headers })
    ]);
    if (!caseResponse.ok) {
      setError(`Case lookup failed with HTTP ${caseResponse.status}.`);
      setStatus(null);
      return;
    }
    if (!updatesResponse.ok) {
      setError(`Customer updates failed with HTTP ${updatesResponse.status}.`);
      setStatus(null);
      return;
    }

    const updatesPayload: unknown = await updatesResponse.json();
    if (!Array.isArray(updatesPayload) || !updatesPayload.every(hasActionState)) {
      setError("Customer update response did not include required backend action authorization state.");
      setStatus(null);
      return;
    }

    setCaseDetail((await caseResponse.json()) as CaseDetail);
    setChecklist(checklistResponse.ok ? ((await checklistResponse.json()) as Checklist) : null);
    setUpdates(updatesPayload as CustomerUpdate[]);
    setProviderHealth(providerResponse.ok ? ((await providerResponse.json()) as ProviderHealth) : null);
    await loadCommunications(caseId);
    setStatus(null);
  }

  async function post(path: string, body: object = {}, auditReason?: string) {
    setStatus("Submitting customer service action...");
    const response = await fetch(`${apiBaseUrl}${path}`, {
      method: "POST",
      headers: {
        ...scopedHeaders(role, userId),
        "Content-Type": "application/json",
        ...(auditReason ? { "X-Audit-Reason": auditReason } : {})
      },
      body: JSON.stringify(body)
    });
    if (!response.ok) {
      setError(`Action failed with HTTP ${response.status}.`);
      setStatus(null);
      return;
    }
    const updatedPayload: unknown = await response.json();
    if (!hasActionState(updatedPayload)) {
      setUpdates([]);
      setSelectedUpdate(null);
      setError("Customer update action completed, but the response omitted required backend authorization state.");
      setStatus(null);
      return;
    }

    if (caseDetail) {
      const updatesResponse = await fetch(`${apiBaseUrl}/api/v1/cases/${caseDetail.caseId}/customer-updates`, { headers: scopedHeaders(role, userId) });
      if (!updatesResponse.ok) {
        setUpdates([]);
        setSelectedUpdate(null);
        setError(`Customer update action completed, but refresh failed with HTTP ${updatesResponse.status}.`);
        setStatus(null);
        return;
      }

      const updatesPayload: unknown = await updatesResponse.json();
      if (!Array.isArray(updatesPayload) || !updatesPayload.every(hasActionState)) {
        setUpdates([]);
        setSelectedUpdate(null);
        setError("Customer update action completed, but refreshed authorization state was missing.");
        setStatus(null);
        return;
      }

      setUpdates(updatesPayload as CustomerUpdate[]);
      await loadCommunications(caseDetail.caseId);
    }
    setStatus("Customer service action completed.");
  }

  async function loadCommunications(targetCaseId: string) {
    const response = await fetch(`${apiBaseUrl}/api/v1/cases/${targetCaseId}/communications`, { headers: scopedHeaders(role, userId) });
    if (response.ok) {
      setCommunications((await response.json()) as Communication[]);
    }
  }

  function resolveCustomerUpdateSendContext(update: CustomerUpdate) {
    if (!caseDetail) return;

    const availability = customerUpdateSendAvailability(update.channel, providerHealth, canUseLocalTestDoubles);
    if (!availability.enabled) {
      setError(availability.reason);
      return;
    }

    const recipient = availability.recipientField === "phone"
      ? caseDetail.customerPhone
      : availability.recipientField === "email"
        ? caseDetail.customerEmail
        : null;
    if (availability.recipientField && !recipient) {
      setError("Cannot send: the selected customer contact channel is not configured.");
      return;
    }

    return { availability, recipient };
  }

  function openCustomerUpdateConfirmation(action: "approve" | "send", update: CustomerUpdate) {
    setError(null);

    if (action === "approve") {
      if (!canManageCustomerCommunications || !customerUpdateAllows(update, "approveCustomerUpdate")) {
        setError("You do not have permission to approve this customer update.");
        return;
      }

      setPendingCustomerUpdateAction({ action, update });
      return;
    }

    if (!canSendCustomerUpdates || !customerUpdateAllows(update, "sendCustomerUpdate")) {
      setError("You do not have permission to send this customer update.");
      return;
    }

    const sendContext = resolveCustomerUpdateSendContext(update);
    if (!sendContext) {
      return;
    }

    setPendingCustomerUpdateAction({
      action,
      update,
      availability: sendContext.availability,
      recipient: sendContext.recipient
    });
  }

  async function approveCustomerUpdate(update: CustomerUpdate, auditReason: string) {
    if (!canManageCustomerCommunications || !customerUpdateAllows(update, "approveCustomerUpdate")) {
      setError("You do not have permission to approve this customer update.");
      return;
    }

    await post(`/api/v1/customer-updates/${update.updateId}/approve`, {}, auditReason);
    setPendingCustomerUpdateAction(null);
  }

  async function sendApprovedUpdate(update: CustomerUpdate, auditReason: string) {
    if (!canSendCustomerUpdates || !customerUpdateAllows(update, "sendCustomerUpdate")) {
      setError("You do not have permission to send this customer update.");
      return;
    }

    const sendContext = resolveCustomerUpdateSendContext(update);
    if (!sendContext) {
      return;
    }

    await post(`/api/v1/customer-updates/${update.updateId}/send`, {
      recipient: sendContext.recipient,
    }, auditReason);
    setPendingCustomerUpdateAction(null);
  }

  const emailAvailability = customerUpdateSendAvailability("Email", providerHealth, canUseLocalTestDoubles);
  const smsAvailability = customerUpdateSendAvailability("SMS", providerHealth, canUseLocalTestDoubles);
  const portalAvailability = customerUpdateSendAvailability("Portal", providerHealth, canUseLocalTestDoubles);
  const selectedChannelAvailability = customerUpdateSendAvailability(sendChannel, providerHealth, canUseLocalTestDoubles);
  const selectedUpdateSendAvailability = selectedUpdate
    ? customerUpdateSendAvailability(selectedUpdate.channel, providerHealth, canUseLocalTestDoubles)
    : null;

  return (
    <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.4 }} className="space-y-6">
      <PageHeader
        eyebrow="Customer Service"
        title="Customer-safe communication desk"
        description="Claim status, missing-document prompts, and update approval stay clearly separated from internal-only notes. Sending requires a configured real provider in deployed runtimes."
      />
      {error ? <ErrorState message={error} /> : null}
      {status ? <div className="rounded-xl border border-accent/30 bg-accent/10 px-4 py-3 text-sm text-accent shadow-glow-cyan">{status}</div> : null}
      <div className="grid gap-6 xl:grid-cols-[340px_minmax(0,1fr)]">
        <SectionCard eyebrow="Search" title="Open case" description="Search by case ID to load the customer-safe communication view.">
          <form className="space-y-4" onSubmit={openCase}>
            <label className="block text-sm font-medium text-muted">
              Case ID
              <input className="mt-1 w-full rounded-xl border border-line bg-shell px-3 py-2 text-sm text-ink outline-none focus:border-accent focus:ring-1 focus:ring-accent transition-all font-mono" value={caseId} onChange={(event) => setCaseId(event.target.value)} />
            </label>
            <button className="w-full rounded-full bg-azure border border-azure/40 px-4 py-2.5 text-sm font-semibold text-white shadow-glow-cyan hover:brightness-110 transition-all">Open customer view</button>
          </form>
        </SectionCard>

        <main className="space-y-6">
          {!caseDetail ? (
            <EmptyState title="No case selected" description="Search by case ID to view customer-safe status, missing documents, and communication history." />
          ) : (
            <motion.div initial={{ opacity: 0, scale: 0.98 }} animate={{ opacity: 1, scale: 1 }} transition={{ duration: 0.3 }} className="space-y-6">
              <SectionCard eyebrow="Overview" title="Customer-safe claim status" description="This view intentionally excludes internal-only risk and routing notes." actions={<StatusBadge label={caseDetail.status} />}>
                <div className="grid gap-4 md:grid-cols-3">
                  <Detail label="Customer" value={caseDetail.customerName} />
                  <Detail label="Policy" value={caseDetail.policyNumber} />
                  <Detail label="Product" value={caseDetail.productLine} />
                  <Detail label="Email" value={caseDetail.customerEmail ?? "Not provided"} />
                  <Detail label="Phone" value={caseDetail.customerPhone ?? "Not provided"} />
                  <Detail label="View boundary" value="Customer-safe only" />
                </div>
              </SectionCard>

              <SectionCard eyebrow="Checklist" title="Required documents" description="Missing-document prompts can be drafted from this workspace, but still remain human reviewed.">
                {checklist?.missingRequiredDocuments.length ? (
                  <div className="grid gap-3 md:grid-cols-2">
                    {checklist.missingRequiredDocuments.map((item) => (
                      <div key={item.documentType} className="rounded-xl border border-warning/30 bg-warning/10 p-4 text-sm text-warning shadow-glow-warning font-mono">
                        {item.label}
                      </div>
                    ))}
                  </div>
                ) : (
                  <EmptyState title="No missing required documents" description="The customer does not need to provide additional required documents right now." />
                )}
              </SectionCard>

              <SectionCard
                eyebrow="Updates"
                title="Communication history"
                description="Customer updates can be drafted here. Delivery requires a configured real communication provider."
                actions={canManageCustomerCommunications ? <button className="rounded-full bg-accent border border-accent/40 px-4 py-2 text-sm font-semibold text-white shadow-glow-cyan hover:brightness-110 transition-all" onClick={() => post(`/api/v1/cases/${caseDetail.caseId}/customer-updates/draft`, { reasonCategory: checklist?.missingRequiredDocuments.length ? "MissingDocuments" : "StatusUpdate", channel: sendChannel })}>Draft update</button> : null}
              >
                <div className="mb-4 flex flex-wrap items-center gap-3 rounded-xl border border-line bg-mist p-4">
                  <span className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted">Send channel</span>
                  <select className="rounded-xl border border-line bg-shell px-3 py-2 text-sm text-ink outline-none focus:border-accent focus:ring-1 focus:ring-accent transition-all cursor-pointer" value={sendChannel} onChange={(event) => setSendChannel(event.target.value)}>
                    <option className="bg-shell text-muted" value="Portal" disabled={!portalAvailability.enabled}>
                      Portal not configured
                    </option>
                    <option className="bg-shell text-ink" value="Email" disabled={!emailAvailability.enabled}>
                      {emailAvailability.enabled ? "Email" : "Email not configured"}
                    </option>
                    <option className="bg-shell text-muted" value="SMS" disabled={!smsAvailability.enabled}>
                      SMS not configured
                    </option>
                    <option className="bg-shell text-muted" value="WhatsApp" disabled>
                      WhatsApp not configured
                    </option>
                  </select>
                  <span
                    className={`rounded-full border px-3 py-1.5 text-[10px] font-bold uppercase tracking-[0.2em] shadow-sm ${
                      selectedChannelAvailability.enabled
                        ? "border-success/30 bg-success/10 text-success shadow-glow-success"
                        : "border-warning/30 bg-warning/10 text-warning shadow-glow-warning"
                    }`}
                    title={selectedChannelAvailability.reason || undefined}
                  >
                    {selectedChannelAvailability.enabled ? "Delivery configured" : "Draft only"}
                  </span>
                </div>
                <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,0.9fr)]">
                  <div className="space-y-4">
                    <CustomerUpdatePanel
                      updates={updates}
                      onApprove={canManageCustomerCommunications ? (update) => openCustomerUpdateConfirmation("approve", update) : undefined}
                      onSend={canSendCustomerUpdates ? (update) => openCustomerUpdateConfirmation("send", update) : undefined}
                      getSendAvailability={(update) => customerUpdateSendAvailability(update.channel, providerHealth, canUseLocalTestDoubles)}
                    />
                  </div>
                  <div className="space-y-3">
                    {communications.length === 0 ? (
                      <EmptyState title="No communications yet" description="Case communications will appear after an approved update is sent through a configured provider." />
                    ) : (
                      communications.map((communication) => (
                        <button key={communication.communicationId} className="w-full rounded-xl border border-line bg-mist p-4 text-left transition hover:-translate-y-0.5 hover:border-accent hover:shadow-glow-cyan focus:outline-none" onClick={() => setSelectedCommunication(communication)}>
                          <div className="flex items-center justify-between gap-3">
                            <p className="text-sm font-semibold text-ink font-mono">
                              {communication.channel} / {communication.deliveryStatus}
                            </p>
                            <p className="text-xs text-muted font-mono">{new Date(communication.createdAt).toLocaleString()}</p>
                          </div>
                          <p className="mt-2 line-clamp-3 text-sm text-muted">{communication.messageBody}</p>
                        </button>
                      ))
                    )}
                  </div>
                </div>
                <p className="mt-4 text-[10px] uppercase tracking-[0.2em] font-bold text-muted">Sending requires a configured provider; unavailable providers are rejected by the API.</p>
              </SectionCard>
            </motion.div>
          )}
        </main>
      </div>

      <SideDrawer
        open={selectedUpdate !== null || selectedCommunication !== null}
        title={selectedUpdate ? `${selectedUpdate.reasonCategory} update` : selectedCommunication ? `${selectedCommunication.channel} communication` : ""}
        subtitle={selectedUpdate ? "Customer update preview with explicit human approval." : "Communication history preview."}
        onClose={() => {
          setSelectedUpdate(null);
          setSelectedCommunication(null);
          setPendingCustomerUpdateAction(null);
        }}
        footer={
          selectedUpdate ? (
            <div className="flex flex-wrap gap-2">
              {canManageCustomerCommunications && customerUpdateAllows(selectedUpdate, "approveCustomerUpdate") ? (
                <button className="rounded-full bg-azure border border-azure/40 px-5 py-2.5 text-sm font-semibold text-white shadow-glow-cyan hover:brightness-110 transition-all" onClick={() => openCustomerUpdateConfirmation("approve", selectedUpdate)}>
                  Approve update
                </button>
              ) : null}
              {canSendCustomerUpdates && customerUpdateAllows(selectedUpdate, "sendCustomerUpdate") ? (
                <button
                  className="rounded-full border border-line bg-surface px-5 py-2.5 text-sm font-semibold text-muted hover:text-ink hover:border-accent/50 transition-all disabled:cursor-not-allowed disabled:opacity-50"
                  disabled={!selectedUpdateSendAvailability?.enabled}
                  title={selectedUpdateSendAvailability?.reason || undefined}
                  onClick={() => openCustomerUpdateConfirmation("send", selectedUpdate)}
                >
                  Send through configured provider
                </button>
              ) : null}
              {selectedUpdateSendAvailability && !selectedUpdateSendAvailability.enabled ? (
                <p className="basis-full rounded-xl border border-warning/30 bg-warning/10 px-3 py-2 text-xs font-semibold text-warning">
                  {selectedUpdateSendAvailability.reason}
                </p>
              ) : null}
            </div>
          ) : null
        }
      >
        {selectedUpdate ? (
          <div className="space-y-5">
            <div className="grid gap-4 md:grid-cols-2">
              <Detail label="Channel" value={selectedUpdate.channel} />
              <Detail label="Status" value={selectedUpdate.status} />
              <Detail label="Approval required" value={selectedUpdate.requiresApproval ? "Yes" : "No"} />
            </div>
            <div className="rounded-xl border border-line bg-mist p-5 text-sm leading-7 text-ink font-mono whitespace-pre-wrap">{selectedUpdate.messageBody}</div>
          </div>
        ) : selectedCommunication ? (
          <div className="space-y-5">
            <div className="grid gap-4 md:grid-cols-2">
              <Detail label="Channel" value={selectedCommunication.channel} />
              <Detail label="Direction" value={selectedCommunication.direction ?? "Outbound"} />
              <Detail label="Delivery status" value={selectedCommunication.deliveryStatus} />
              <Detail label="Created" value={new Date(selectedCommunication.createdAt).toLocaleString()} />
            </div>
            <div className="rounded-xl border border-line bg-mist p-5 text-sm leading-7 text-ink font-mono whitespace-pre-wrap">{selectedCommunication.messageBody}</div>
          </div>
        ) : null}
      </SideDrawer>

      <CustomerUpdateGovernedDialog
        pendingAction={pendingCustomerUpdateAction}
        onClose={() => setPendingCustomerUpdateAction(null)}
        onConfirm={async (auditReason) => {
          if (!pendingCustomerUpdateAction) {
            return;
          }

          if (pendingCustomerUpdateAction.action === "approve") {
            await approveCustomerUpdate(pendingCustomerUpdateAction.update, auditReason);
            return;
          }

          await sendApprovedUpdate(pendingCustomerUpdateAction.update, auditReason);
        }}
      />
    </motion.div>
  );
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border border-line bg-mist p-4">
      <dt className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted">{label}</dt>
      <dd className="mt-2 text-sm font-semibold text-ink font-mono">{value}</dd>
    </div>
  );
}
