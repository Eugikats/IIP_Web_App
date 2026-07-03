"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useMemo, useState } from "react";
import { motion } from "framer-motion";
import { ArrowRight, Mail, MessageSquare, RefreshCw, Send, Users } from "lucide-react";
import { StatusBadge } from "@/components/Badges";
import { DataTable } from "@/components/DataTable";
import { GovernedActionDialog } from "@/components/GovernedActionDialog";
import { PageHeader } from "@/components/PageHeader";
import { useRole } from "@/components/RoleContext";
import { SectionCard } from "@/components/SectionCard";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { apiBaseUrl, canUseLocalTestDoubles, scopedHeaders } from "@/lib/config";
import { integrationActionAvailability, type IntegrationHealth, type ProviderHealthItem } from "@/lib/providerHealth";

type Mailbox = {
  mailboxConnectionId: string;
  displayName: string;
  mailboxAddress: string;
  ingestionEnabled: boolean;
  defaultChannel: string;
  productLine?: string | null;
  createdAt?: string;
};

type InboundMessage = {
  messageId: string;
  sender: string;
  subject: string;
  bodyPreview?: string;
  receivedAt: string;
  processingStatus: string | number;
  linkedCaseId?: string | null;
  attachments?: { fileName: string; sizeBytes: number }[];
};

type Communication = {
  communicationId: string;
  caseId?: string | null;
  channel: string;
  direction?: string;
  messageBody: string;
  sentBy?: string;
  deliveryStatus?: string;
  createdAt: string;
};

type PendingAction = { message: InboundMessage };

const channelCards = [
  { type: "Email", title: "Outlook / Email", provider: "graphMail", detail: "Inbound claims mailbox and attachment intake." },
  { type: "Teams", title: "Teams", provider: "notifications", detail: "Operational notifications and approval alerts." },
  { type: "WhatsApp", title: "WhatsApp", provider: null, detail: "Pending BSP selection and tenant configuration." },
  { type: "SMS", title: "SMS", provider: "communications", detail: "Customer-update delivery when configured." },
] as const;

export function ChannelsClient() {
  const { role, userId } = useRole();
  const router = useRouter();
  const [health, setHealth] = useState<IntegrationHealth | null>(null);
  const [mailboxes, setMailboxes] = useState<Mailbox[]>([]);
  const [messages, setMessages] = useState<InboundMessage[]>([]);
  const [communications, setCommunications] = useState<Communication[]>([]);
  const [pendingAction, setPendingAction] = useState<PendingAction | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    const headers = scopedHeaders(role, userId);
    try {
      const [healthResponse, mailboxResponse, messageResponse, communicationResponse] = await Promise.all([
        fetch(`${apiBaseUrl}/api/v1/integrations/email/health`, { headers }),
        fetch(`${apiBaseUrl}/api/v1/integrations/email/mailboxes`, { headers }),
        fetch(`${apiBaseUrl}/api/v1/integrations/email/messages`, { headers }),
        fetch(`${apiBaseUrl}/api/v1/communications/history`, { headers }),
      ]);

      if (healthResponse.ok) setHealth((await healthResponse.json()) as IntegrationHealth);
      if (mailboxResponse.ok) setMailboxes((await mailboxResponse.json()) as Mailbox[]);
      if (messageResponse.ok) setMessages((await messageResponse.json()) as InboundMessage[]);
      if (communicationResponse.ok) setCommunications((await communicationResponse.json()) as Communication[]);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Channels failed to load.");
    } finally {
      setLoading(false);
    }
  }, [role, userId]);

  useEffect(() => {
    load();
  }, [load]);

  const providerMap = useMemo(
    () => new Map((health?.providers ?? []).map((provider) => [provider.name, provider])),
    [health]
  );

  function channelStatus(provider: ProviderHealthItem | null, type: string) {
    if (type === "WhatsApp") return { status: "Pending", health: "Warning", detail: "Configure a WhatsApp BSP before enabling this channel." };
    if (!provider) return { status: "Pending", health: "Warning", detail: "Provider health is not available." };
    if (provider.status.toLowerCase() === "missing") {
      return { status: "Pending", health: "Warning", detail: provider.detail || "Provider is not configured." };
    }
    return { status: "Active", health: "Operational", detail: provider.detail || provider.mode };
  }

  function openProcess(message: InboundMessage) {
    const availability = integrationActionAvailability(health, "processInboundEmail");
    if (!availability.enabled && !canUseLocalTestDoubles) {
      setError(availability.reason);
      return;
    }
    setError(null);
    setPendingAction({ message });
  }

  async function confirmProcess(auditReason: string) {
    if (!pendingAction) return;
    const response = await fetch(`${apiBaseUrl}/api/v1/integrations/email/process-message`, {
      method: "POST",
      headers: { ...scopedHeaders(role, userId), "Content-Type": "application/json", "X-Audit-Reason": auditReason },
      body: JSON.stringify({ messageId: pendingAction.message.messageId }),
    });
    if (!response.ok) {
      setError(`Message processing failed with HTTP ${response.status}.`);
      return;
    }
    const result = (await response.json()) as { caseId?: string | null };
    setPendingAction(null);
    await load();
    if (result.caseId) {
      router.push(`/cases/${result.caseId}`);
    }
  }

  if (loading) return <LoadingState label="Loading channel operations..." />;

  return (
    <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.35 }}>
      <PageHeader
        eyebrow="Channel-led intake"
        title="Channels"
        description="Operational view of how insurer work enters and leaves IIP. Unconfigured channels stay pending instead of pretending to work."
        actions={
          <button className="inline-flex items-center gap-2 rounded-full border border-line bg-surface px-3 py-2 text-sm font-semibold text-ink hover:bg-surface-soft" onClick={load}>
            <RefreshCw className="h-4 w-4" /> Refresh
          </button>
        }
      />

      {error ? <div className="mb-4"><ErrorState message={error} /></div> : null}

      <div className="grid gap-4 xl:grid-cols-4">
        {channelCards.map((card) => {
          const provider = card.provider ? providerMap.get(card.provider) ?? null : null;
          const status = channelStatus(provider, card.type);
          return (
            <SectionCard key={card.type} title={card.title} description={card.detail}>
              <div className="space-y-3">
                <StatusBadge label={status.status} />
                <p className="text-xs leading-5 text-muted">{status.detail}</p>
                <p className="font-mono text-xs text-muted-soft">{status.health}</p>
              </div>
            </SectionCard>
          );
        })}
      </div>

      <div className="mt-5 grid gap-5 xl:grid-cols-[1.3fr_0.7fr]">
        <SectionCard eyebrow="Inbound" title="Messages to process" description="Email intake can create or attach a governed case; processing writes audit evidence server-side.">
          {messages.length === 0 ? (
            <EmptyState title="No inbound messages" description="Run LocalTest ingestion from Integrations or connect a real Graph mailbox." />
          ) : (
            <DataTable label="Inbound channel messages">
              <thead className="bg-mist text-left text-xs uppercase tracking-wider text-muted">
                <tr>
                  <th className="px-4 py-3">Sender</th>
                  <th className="px-4 py-3">Subject</th>
                  <th className="px-4 py-3">Status</th>
                  <th className="px-4 py-3">Attachments</th>
                  <th className="px-4 py-3">Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {messages.map((message) => (
                  <tr key={message.messageId}>
                    <td className="px-4 py-3 font-mono text-xs text-ink">{message.sender}</td>
                    <td className="px-4 py-3 text-sm text-ink">
                      <p className="font-medium">{message.subject}</p>
                      {message.bodyPreview ? <p className="mt-1 line-clamp-1 text-xs text-muted">{message.bodyPreview}</p> : null}
                    </td>
                    <td className="px-4 py-3"><StatusBadge label={String(message.processingStatus)} /></td>
                    <td className="px-4 py-3 text-sm text-muted">{message.attachments?.length ?? 0}</td>
                    <td className="px-4 py-3">
                      {message.linkedCaseId ? (
                        <Link className="inline-flex items-center gap-1 text-sm font-semibold text-ai" href={`/cases/${message.linkedCaseId}`}>
                          Open case <ArrowRight className="h-3.5 w-3.5" />
                        </Link>
                      ) : (
                        <button className="inline-flex items-center gap-1 rounded-full border border-ai-border bg-ai-soft px-3 py-1.5 text-xs font-semibold text-ai hover:bg-violet/15" onClick={() => openProcess(message)}>
                          Create / attach case
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </DataTable>
          )}
        </SectionCard>

        <SectionCard eyebrow="Outbound" title="Communication history" description="Outbound customer communication remains governed by draft, approval, and provider configuration.">
          {communications.length === 0 ? (
            <EmptyState title="No communications yet" description="Draft customer updates from a case or Customer Service; do not send directly from Channels." />
          ) : (
            <div className="space-y-3">
              {communications.slice(0, 6).map((item) => (
                <div key={item.communicationId} className="rounded-xl border border-line bg-mist/40 p-3">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <p className="text-sm font-semibold text-ink">{item.channel}</p>
                      <p className="mt-1 line-clamp-2 text-xs leading-5 text-muted">{item.messageBody}</p>
                    </div>
                    <StatusBadge label={item.deliveryStatus ?? "Draft"} />
                  </div>
                  {item.caseId ? <Link className="mt-2 inline-flex items-center gap-1 text-xs font-semibold text-ai" href={`/cases/${item.caseId}`}>Case workspace <ArrowRight className="h-3 w-3" /></Link> : null}
                </div>
              ))}
            </div>
          )}
        </SectionCard>
      </div>

      <div className="mt-5 grid gap-4 md:grid-cols-3">
        <MiniStat icon={<Mail className="h-4 w-4" />} label="Configured mailboxes" value={`${mailboxes.length}`} />
        <MiniStat icon={<MessageSquare className="h-4 w-4" />} label="Inbound messages" value={`${messages.length}`} />
        <MiniStat icon={<Send className="h-4 w-4" />} label="Customer updates" value={`${communications.length}`} />
      </div>

      <GovernedActionDialog
        open={pendingAction !== null}
        title="Process inbound message"
        target={pendingAction?.message.subject ?? "Inbound message"}
        impact="Create or attach a case from this email message. Processing is audited and idempotent by message id."
        auditNotice="This action records the business reason, actor, provider, and resulting case link in the audit trail."
        confirmLabel="Process message"
        onClose={() => setPendingAction(null)}
        onConfirm={confirmProcess}
        evidence={
          <div className="rounded-xl border border-line bg-mist/60 p-3 text-sm text-muted">
            <p className="font-semibold text-ink">{pendingAction?.message.subject}</p>
            <p className="mt-1 font-mono text-xs">{pendingAction?.message.sender}</p>
          </div>
        }
      />
    </motion.div>
  );
}

function MiniStat({ icon, label, value }: { icon: React.ReactNode; label: string; value: string }) {
  return (
    <div className="flex items-center gap-3 rounded-xl border border-line bg-surface px-4 py-3 shadow-panel">
      <div className="flex h-9 w-9 items-center justify-center rounded-lg border border-ai-border bg-ai-soft text-ai">{icon}</div>
      <div>
        <p className="text-xs font-medium text-muted">{label}</p>
        <p className="text-lg font-semibold text-ink">{value}</p>
      </div>
    </div>
  );
}
