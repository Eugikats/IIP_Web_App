"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useMemo, useState } from "react";
import { motion } from "framer-motion";
import {
  ArrowRight,
  ArrowUpRight,
  FilePlus2,
  Inbox,
  Mail,
  MessageSquare,
  PenLine,
  RefreshCw,
  UserPlus,
  Users,
} from "lucide-react";
import { StatusBadge } from "@/components/Badges";
import { DataTable } from "@/components/DataTable";
import { GovernedActionDialog } from "@/components/GovernedActionDialog";
import { GovernedButton } from "@/components/GovernedUi";
import { PageHeader } from "@/components/PageHeader";
import { useRole } from "@/components/RoleContext";
import { SectionCard } from "@/components/SectionCard";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { apiBaseUrl, canUseLocalTestDoubles, scopedHeaders } from "@/lib/config";
import { integrationActionAvailability, type IntegrationHealth, type ProviderHealthItem } from "@/lib/providerHealth";

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

type ChannelConversation = {
  id: string;
  customerName: string;
  channel: string;
  detectedIntent: string;
  suggestedRouting: string;
  aiConfidence: number;
  status: string;
  assignedTo?: string | null;
  lastMessage: string;
  lastActivityAt: string;
};

type PendingAction = { message: InboundMessage };

function humanize(value: string): string {
  if (!value) return "—";
  return value.replace(/[_-]+/g, " ").replace(/\b\w/g, (character) => character.toUpperCase());
}

function formatWhen(value: string): string {
  const parsed = Date.parse(value);
  if (Number.isNaN(parsed)) return value || "—";
  return new Date(parsed).toLocaleString(undefined, { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });
}

const channelCards = [
  { type: "Email", title: "Outlook / Email", provider: "graphMail" },
  { type: "Teams", title: "Teams", provider: "notifications" },
  { type: "WhatsApp", title: "WhatsApp", provider: null },
  { type: "SMS", title: "SMS", provider: "communications" },
] as const;

export function ChannelsClient() {
  const { role, userId } = useRole();
  const router = useRouter();
  const [health, setHealth] = useState<IntegrationHealth | null>(null);
  const [messages, setMessages] = useState<InboundMessage[]>([]);
  const [communications, setCommunications] = useState<Communication[]>([]);
  const [conversations, setConversations] = useState<ChannelConversation[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [pendingAction, setPendingAction] = useState<PendingAction | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    const headers = scopedHeaders(role, userId);
    try {
      const [healthResponse, messageResponse, communicationResponse, conversationResponse] = await Promise.all([
        fetch(`${apiBaseUrl}/api/v1/integrations/email/health`, { headers }),
        fetch(`${apiBaseUrl}/api/v1/integrations/email/messages`, { headers }),
        fetch(`${apiBaseUrl}/api/v1/communications/history`, { headers }),
        fetch(`${apiBaseUrl}/api/v1/channels/overview`, { headers }),
      ]);

      if (healthResponse.ok) setHealth((await healthResponse.json()) as IntegrationHealth);
      if (messageResponse.ok) setMessages((await messageResponse.json()) as InboundMessage[]);
      if (communicationResponse.ok) setCommunications((await communicationResponse.json()) as Communication[]);
      if (conversationResponse.ok) setConversations((await conversationResponse.json()) as ChannelConversation[]);
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

  const selected = useMemo(
    () => conversations.find((conversation) => conversation.id === selectedId) ?? null,
    [conversations, selectedId]
  );

  function channelStatus(provider: ProviderHealthItem | null, type: string) {
    if (type === "WhatsApp") return { status: "Pending", detail: "Configure a WhatsApp BSP before enabling this channel." };
    if (!provider) return { status: "Pending", detail: "Provider health is not available." };
    if (provider.status.toLowerCase() === "missing") {
      return { status: "Pending", detail: provider.detail || "Provider is not configured." };
    }
    return { status: "Active", detail: provider.detail || provider.mode };
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
        description="Unified inbox across configured channels. AI triages and routes; a human takes every next action. Unconfigured channels stay pending instead of pretending to work."
        actions={
          <button className="inline-flex items-center gap-2 rounded-full border border-line bg-surface px-3 py-2 text-sm font-semibold text-ink hover-lift" onClick={load}>
            <RefreshCw className="h-4 w-4" /> Refresh
          </button>
        }
      />

      {error ? <div className="mb-4"><ErrorState message={error} /></div> : null}

      {/* Channel health strip */}
      <div className="mb-5 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        {channelCards.map((card) => {
          const provider = card.provider ? providerMap.get(card.provider) ?? null : null;
          const status = channelStatus(provider, card.type);
          return (
            <div key={card.type} className="glass-subtle rounded-xl px-4 py-3 hover-lift">
              <div className="flex items-center justify-between gap-2">
                <p className="text-sm font-semibold text-ink">{card.title}</p>
                <StatusBadge label={status.status} />
              </div>
              <p className="mt-1.5 line-clamp-2 text-xs leading-5 text-muted">{status.detail}</p>
            </div>
          );
        })}
      </div>

      {/* Threaded inbox: conversation list + thread detail */}
      <div className="grid gap-5 lg:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)]">
        <SectionCard
          eyebrow="Inbox"
          title="Conversations"
          description="AI-triaged across configured channels. Nothing is auto-actioned."
        >
          {conversations.length === 0 ? (
            <EmptyState
              title="No channel conversations"
              description="Conversation intake activates when a WhatsApp / web-chat provider is configured and ingesting. Until then this inbox stays empty rather than showing sample conversations."
            />
          ) : (
            <ul className="space-y-2" aria-label="Channel conversations">
              {conversations.map((conversation) => {
                const active = conversation.id === selectedId;
                return (
                  <li key={conversation.id}>
                    <button
                      onClick={() => setSelectedId(conversation.id)}
                      aria-current={active ? "true" : undefined}
                      className={`w-full rounded-xl border px-3 py-3 text-left transition-colors hover-lift ${
                        active ? "border-primary-border bg-primary-soft" : "border-line bg-surface hover:bg-surface-soft"
                      }`}
                    >
                      <div className="flex items-center justify-between gap-2">
                        <p className="truncate text-sm font-semibold text-ink">{conversation.customerName}</p>
                        <span className="shrink-0 text-[11px] text-muted-soft">{formatWhen(conversation.lastActivityAt)}</span>
                      </div>
                      <p className="mt-1 line-clamp-1 text-xs text-muted">{conversation.lastMessage || humanize(conversation.detectedIntent)}</p>
                      <div className="mt-2 flex flex-wrap items-center gap-1.5">
                        <StatusBadge label={humanize(conversation.channel)} />
                        <StatusBadge label={humanize(conversation.status)} />
                      </div>
                    </button>
                  </li>
                );
              })}
            </ul>
          )}
        </SectionCard>

        <SectionCard
          eyebrow="Conversation"
          title={selected ? selected.customerName : "Thread"}
          description={selected ? undefined : "Select a conversation to view its timeline and take a governed next action."}
        >
          {!selected ? (
            <EmptyState
              title="No conversation selected"
              description="Pick a conversation on the left to see its detected intent, AI confidence, suggested routing, and the human actions available."
            />
          ) : (
            <ConversationThread
              conversation={selected}
              onCreateClaim={() => router.push("/cases/new")}
              onDraftResponse={() => router.push("/customer-service")}
            />
          )}
        </SectionCard>
      </div>

      {/* Secondary: inbound email intake (real, governed) */}
      <div className="mt-5 grid gap-5 xl:grid-cols-[1.3fr_0.7fr]">
        <SectionCard eyebrow="Inbound email" title="Messages to process" description="Email intake can create or attach a governed case; processing writes audit evidence server-side.">
          {messages.length === 0 ? (
            <EmptyState title="No inbound messages" description="Run LocalTest ingestion from Integrations or connect a real Graph mailbox." />
          ) : (
            <DataTable label="Inbound channel messages">
              <thead className="bg-mist text-left text-xs uppercase tracking-wider text-muted">
                <tr>
                  <th className="px-4 py-3">Sender</th>
                  <th className="px-4 py-3">Subject</th>
                  <th className="px-4 py-3">Status</th>
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

      <div className="mt-5 grid gap-4 md:grid-cols-4">
        <MiniStat icon={<Users className="h-4 w-4" />} label="Conversations" value={`${conversations.length}`} />
        <MiniStat icon={<Inbox className="h-4 w-4" />} label="Inbound messages" value={`${messages.length}`} />
        <MiniStat icon={<Mail className="h-4 w-4" />} label="Configured mailboxes" value={`${providerMap.size}`} />
        <MiniStat icon={<MessageSquare className="h-4 w-4" />} label="Customer updates" value={`${communications.length}`} />
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

function ConversationThread({
  conversation,
  onCreateClaim,
  onDraftResponse,
}: {
  conversation: ChannelConversation;
  onCreateClaim: () => void;
  onDraftResponse: () => void;
}) {
  const confidencePct = Math.round(conversation.aiConfidence * 100);
  return (
    <div className="space-y-4">
      {/* Metadata */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <Meta label="Channel" value={humanize(conversation.channel)} />
        <Meta label="Detected intent" value={humanize(conversation.detectedIntent)} />
        <Meta label="AI confidence" value={`${confidencePct}%`} />
        <Meta label="Assigned" value={conversation.assignedTo ? conversation.assignedTo : "Unassigned"} />
      </div>

      {/* Suggested routing (advisory) */}
      <div className="rounded-xl border border-ai-border bg-ai-soft p-3">
        <p className="text-[10px] font-bold uppercase tracking-wider text-ai">AI suggested routing — for human review</p>
        <p className="mt-1 text-sm text-ink">{humanize(conversation.suggestedRouting)}</p>
      </div>

      {/* Latest message */}
      <div>
        <p className="mb-2 text-[10px] font-bold uppercase tracking-wider text-muted">Latest message</p>
        <div className="rounded-xl border border-line bg-surface p-3">
          <p className="text-sm leading-6 text-ink">{conversation.lastMessage || "No message content available."}</p>
          <p className="mt-2 text-[11px] text-muted-soft">{formatWhen(conversation.lastActivityAt)}</p>
        </div>
        <p className="mt-2 text-xs text-muted">
          Full conversation timeline appears here once the channel thread API is connected. Until then, only the latest triaged message is shown.
        </p>
      </div>

      {/* Governed action bar */}
      <div>
        <p className="mb-2 text-[10px] font-bold uppercase tracking-wider text-muted">Next action — human decides</p>
        <div className="flex flex-wrap gap-2">
          <GovernedButton variant="primary" icon={<FilePlus2 className="h-4 w-4" />} onClick={onCreateClaim}>
            Create claim
          </GovernedButton>
          <GovernedButton variant="secondary" icon={<PenLine className="h-4 w-4" />} onClick={onDraftResponse}>
            Draft response
          </GovernedButton>
          <GovernedButton
            variant="secondary"
            icon={<UserPlus className="h-4 w-4" />}
            disabledReason="Lead creation from a conversation is not configured yet — the sales-leads write endpoint is pending."
          >
            Create lead
          </GovernedButton>
          <GovernedButton
            variant="secondary"
            icon={<ArrowUpRight className="h-4 w-4" />}
            disabledReason="Conversation escalation is not configured yet — the escalation endpoint is pending."
          >
            Escalate
          </GovernedButton>
        </div>
      </div>
    </div>
  );
}

function Meta({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border border-line bg-surface px-3 py-2">
      <p className="text-[10px] font-medium uppercase tracking-wider text-muted-soft">{label}</p>
      <p className="mt-0.5 truncate text-sm font-semibold text-ink">{value}</p>
    </div>
  );
}

function MiniStat({ icon, label, value }: { icon: React.ReactNode; label: string; value: string }) {
  return (
    <div className="flex items-center gap-3 glass-subtle rounded-xl px-4 py-3 hover-lift">
      <div className="flex h-9 w-9 items-center justify-center rounded-lg border border-ai-border bg-ai-soft text-ai">{icon}</div>
      <div>
        <p className="text-xs font-medium text-muted">{label}</p>
        <p className="text-lg font-semibold text-ink">{value}</p>
      </div>
    </div>
  );
}
