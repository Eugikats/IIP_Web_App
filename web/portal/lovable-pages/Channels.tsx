"use client";
import { useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { Search, MessageCircle, Mail, Globe, Phone, Users2, MonitorPlay, Send, ShieldAlert, Inbox } from "lucide-react";
import { GlassCard, SectionHeader, Badge, StatusBadge } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { FilterTab } from "@/components/ui/FilterTab";
import { LoadingSkeleton, ErrorStateSurface, EmptyStateSurface, EmptyFilteredStateSurface } from "@/components/ui/StateSurfaces";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { type Channel, type ChannelConversation } from "@/lib/lovable-parity/mock";
import { useChannelConversations, useWhatsAppThread } from "@/lib/lovable-parity/data/channels";

const ICON: Record<Channel, any> = {
  WhatsApp: MessageCircle, Email: Mail, Web: Globe, SMS: Phone, Broker: Users2, Teams: MonitorPlay,
};

const CHANNEL_FILTERS = ["All", "WhatsApp", "Email", "Web", "SMS", "Broker", "Teams"] as const;

export default function Channels() {
  const router = useRouter();
  const { conversations, loading, error, source, parityMode } = useChannelConversations();
  const [filter, setFilter] = useState<"All" | Channel>("All");
  const [query, setQuery] = useState("");
  const [activeId, setActiveId] = useState<string | null>(null);
  const [draft, setDraft] = useState("");
  const [purpose, setPurpose] = useState("claim_status_update");
  const [consentBasis, setConsentBasis] = useState("");
  const [language, setLanguage] = useState("en");
  const [templateName, setTemplateName] = useState("");
  const [approved, setApproved] = useState(false);
  const [sending, setSending] = useState(false);

  const list = useMemo(() => {
    const q = query.trim().toLowerCase();
    return conversations.filter((c) => {
      if (filter !== "All" && c.channel !== filter) return false;
      if (q && !`${c.customer} ${c.snippet}`.toLowerCase().includes(q)) return false;
      return true;
    });
  }, [conversations, filter, query]);

  const channelCounts = useMemo(() => {
    const counts = new Map<(typeof CHANNEL_FILTERS)[number], number>();
    for (const f of CHANNEL_FILTERS) counts.set(f, f === "All" ? conversations.length : conversations.filter((c) => c.channel === f).length);
    return counts;
  }, [conversations]);

  const hasActiveFilters = filter !== "All" || query.length > 0;
  function clearFilters() {
    setFilter("All");
    setQuery("");
  }

  const active = useMemo(() => list.find((c) => c.id === activeId) ?? list[0] ?? null, [list, activeId]);
  const whatsapp = useWhatsAppThread(!parityMode && active?.channel === "WhatsApp" ? active.id : null);
  const linkedCaseId = whatsapp.messages.find((message) => message.caseId)?.caseId ?? null;

  async function sendWhatsApp() {
    if (!active || !linkedCaseId) return;
    setSending(true);
    try {
      const result = await whatsapp.send({
        caseId: linkedCaseId,
        recipientWaId: whatsapp.messages[0]?.contactWaId ?? "",
        customerSafeBody: draft,
        purpose,
        consentBasis,
        language,
        templateName,
        humanApproved: approved,
      });
      const copy = result.deliveryState === "ProviderAccepted"
        ? "Accepted by WhatsApp. Delivery is not confirmed yet."
        : result.deliveryState === "Unknown"
          ? "We did not get a clear answer back from WhatsApp. Check whether it arrived before sending again."
          : `WhatsApp state: ${result.deliveryState}.`;
      toast(result.deliveryState === "Failed" ? "WhatsApp did not accept the message" : "WhatsApp attempt recorded", { description: copy });
      if (result.deliveryState !== "Unknown") setDraft("");
      setApproved(false);
    } catch (error) {
      toast.error("WhatsApp message not sent", { description: error instanceof Error ? error.message : "Review the preflight and try again." });
    } finally {
      setSending(false);
    }
  }

  return (
    <>
      <SectionHeader
        eyebrow="Integrations & Channels"
        title="Channels inbox"
        sub="Messages from customers and brokers across every connected channel, in one place. AI drafts replies; a person approves anything sensitive before it is sent."
      />

      <div className="mb-4"><GuardrailBanner text="Sensitive outbound messages route through approval before send. Nothing dispatches automatically." /></div>

      <div className="grid grid-cols-12 gap-4">
        <GlassCard className="col-span-12 !p-0 overflow-hidden lg:col-span-4">
          <div className="border-b border-dls-border-subtle p-3">
            <Input
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="Search conversations…"
              leadingIcon={<Search aria-hidden="true" />}
              aria-label="Search conversations"
            />
            <div className="mt-2 flex flex-wrap gap-1.5">
              {CHANNEL_FILTERS.map((f) => (
                <FilterTab key={f} active={filter === f} onClick={() => setFilter(f)} count={channelCounts.get(f)}>
                  {f}
                </FilterTab>
              ))}
            </div>
          </div>
          <div className="max-h-[560px] overflow-y-auto">
            {loading ? (
              <LoadingSkeleton rows={5} className="p-3" />
            ) : error ? (
              <ErrorStateSurface title="Conversations could not be loaded" message={error} />
            ) : conversations.length === 0 ? (
              <EmptyStateSurface message="Customer conversations appear here once WhatsApp, email or web chat is connected. Until then this stays empty rather than showing made-up examples." />
            ) : list.length === 0 ? (
              <EmptyFilteredStateSurface onClear={clearFilters} />
            ) : (
              list.map((c) => {
                const I = ICON[c.channel];
                const isActive = active ? c.id === active.id : false;
                return (
                  <button
                    key={c.id}
                    type="button"
                    onClick={() => setActiveId(c.id)}
                    className={`dls-focus-outline w-full border-b border-dls-border-subtle p-3 text-left transition-colors duration-dls-fast ease-dls hover:bg-dls-bg-page ${isActive ? "bg-dls-primary-soft-alt" : ""}`}
                  >
                    <div className="flex items-center gap-2">
                      <div className="grid h-8 w-8 place-items-center rounded-dls-chip bg-dls-bg-page text-dls-text-secondary">
                        <I className="h-4 w-4" aria-hidden="true" />
                      </div>
                      <div className="min-w-0 flex-1">
                        <div className="flex items-center justify-between gap-2">
                          <div className="min-w-0 truncate text-[13px] font-semibold text-dls-text-primary">{c.customer}</div>
                          <div className="shrink-0 whitespace-nowrap text-[11px] text-dls-text-muted">{c.updated}</div>
                        </div>
                        <div className="truncate text-[12px] text-dls-text-secondary">{c.snippet}</div>
                      </div>
                    </div>
                    <div className="mt-1.5 flex flex-wrap items-center gap-1.5">
                      <Badge tone="info">{c.channel}</Badge>
                      <Badge tone="neutral">{c.intent}</Badge>
                      {c.language && <Badge tone="info">{c.language}</Badge>}
                      {!c.matched && <Badge tone="warning">Unmatched customer</Badge>}
                      {c.needsApproval && <Badge tone="warning">Needs your approval</Badge>}
                      {c.unread > 0 && <Badge tone="error">{c.unread} new</Badge>}
                    </div>
                  </button>
                );
              })
            )}
          </div>
        </GlassCard>

        <GlassCard className="col-span-12 lg:col-span-8">
          {!active ? (
            <div className="grid h-full min-h-[280px] place-items-center text-center">
              <div>
                <div className="mx-auto mb-3 grid h-11 w-11 place-items-center rounded-dls-chip bg-dls-bg-page text-dls-text-secondary">
                  <Inbox className="h-5 w-5" aria-hidden="true" />
                </div>
                <div className="text-[14px] font-bold text-dls-text-primary">No conversation selected</div>
                <p className="mt-1 text-[12.5px] text-dls-text-secondary">Pick a conversation on the left to view its timeline and take a governed next action.</p>
              </div>
            </div>
          ) : (
            <>
              <div className="mb-3 flex items-center justify-between">
                <div>
                  <div className="text-[11px] font-bold uppercase tracking-[0.14em] text-dls-text-muted">{active.channel}</div>
                  <div className="text-[16px] font-bold text-dls-text-primary">{active.customer}</div>
                </div>
                <div className="flex items-center gap-2">
                  {active.language && <Badge tone="info">{active.language}</Badge>}
                  {!active.matched && <Badge tone="warning">Unmatched, link to customer</Badge>}
                  <StatusBadge status={active.needsApproval ? "Human Review Required" : "Active"} />
                </div>
              </div>

              <div className="max-h-[360px] space-y-3 overflow-y-auto rounded-dls-card border border-dls-border-subtle bg-dls-bg-page p-4">
                {parityMode ? (
                  <>
                    <div className="text-[12.5px] text-dls-text-secondary">Yesterday 16:41</div>
                    <div className="max-w-[80%] rounded-dls-overlay rounded-bl-[4px] bg-dls-surface px-3 py-2 text-[13px] text-dls-text-primary">Hello, I had an accident this morning. What documents do I need to send?</div>
                    <div className="ml-auto max-w-[80%] rounded-dls-overlay rounded-br-[4px] bg-dls-primary px-3 py-2 text-[13px] text-dls-text-inverse">Thank you. Please share your driving permit, national ID, vehicle photos, and the police report if available.</div>
                    <div className="max-w-[80%] rounded-dls-overlay rounded-bl-[4px] bg-dls-surface px-3 py-2 text-[13px] text-dls-text-primary">{active.snippet}</div>
                  </>
                ) : active.channel === "WhatsApp" ? (
                  whatsapp.loading ? (
                    <div className="text-[12.5px] text-dls-text-secondary">Loading WhatsApp thread…</div>
                  ) : whatsapp.error ? (
                    <ErrorStateSurface message={whatsapp.error} />
                  ) : whatsapp.messages.length === 0 ? (
                    <div className="text-[12.5px] text-dls-text-secondary">No retained messages are available for this thread.</div>
                  ) : (
                    whatsapp.messages.map((message) => (
                      <div key={message.messageId} className={message.direction === "Outbound" ? "ml-auto max-w-[80%]" : "max-w-[80%]"}>
                        <div
                          className={
                            message.direction === "Outbound"
                              ? "rounded-dls-overlay rounded-br-[4px] bg-dls-primary px-3 py-2 text-[13px] text-dls-text-inverse"
                              : "rounded-dls-overlay rounded-bl-[4px] bg-dls-surface px-3 py-2 text-[13px] text-dls-text-primary"
                          }
                        >
                          {message.customerSafeBody}
                        </div>
                        <div className="mt-1 text-[11px] text-dls-text-muted">
                          {message.direction === "Outbound" ? deliveryCopy(message.deliveryState) : "Received"} · {new Date(message.createdAt).toLocaleString()}
                        </div>
                      </div>
                    ))
                  )
                ) : (
                  <>
                    <div className="max-w-[80%] rounded-dls-overlay rounded-bl-[4px] bg-dls-surface px-3 py-2 text-[13px] text-dls-text-primary">{active.snippet || "No message content available."}</div>
                    <div className="text-[12px] text-dls-text-muted">{active.updated}</div>
                    <p className="text-[12px] text-dls-text-muted">This channel does not yet expose a retained thread contract. Only the latest permitted message is shown.</p>
                  </>
                )}
              </div>

              {active.languageNote ? (
                <div className="mt-3 rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3">
                  <div className="text-[10.5px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Language</div>
                  <p className="mt-1 text-[12.5px] leading-5 text-dls-text-secondary">{active.languageNote}</p>
                </div>
              ) : null}

              {!parityMode && active.channel === "WhatsApp" ? (
                <div className="mt-3 space-y-3 rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4">
                  <div>
                    <div className="text-[13px] font-bold text-dls-text-primary">Reviewed WhatsApp reply</div>
                    <p className="text-[11.5px] text-dls-text-secondary">Exact content, recipient, purpose, policy basis, language and template are recorded before one provider attempt.</p>
                  </div>
                  {!linkedCaseId ? (
                    <div className="rounded-dls-control bg-dls-warning-tint px-3 py-2 text-[12px] text-dls-warning-text">Link this thread to an authorized case before replying.</div>
                  ) : null}
                  <textarea
                    aria-label="Customer-safe WhatsApp message"
                    value={draft}
                    onChange={(event) => setDraft(event.target.value)}
                    rows={4}
                    maxLength={4096}
                    placeholder="Write only customer-safe content…"
                    className="w-full rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-2 text-[13px] text-dls-text-primary focus:outline-none focus:border-dls-primary focus:ring-[3px] focus:ring-dls-primary/[0.12]"
                  />
                  <div className="grid gap-2 sm:grid-cols-2">
                    <Input aria-label="Communication purpose" value={purpose} onChange={(event) => setPurpose(event.target.value)} placeholder="Purpose" />
                    <Input aria-label="Consent or legal basis" value={consentBasis} onChange={(event) => setConsentBasis(event.target.value)} placeholder="Consent or legal basis reference" />
                    <Input aria-label="Message language" value={language} onChange={(event) => setLanguage(event.target.value)} placeholder="Language code" />
                    <Input aria-label="Approved template name" value={templateName} onChange={(event) => setTemplateName(event.target.value)} placeholder="Approved template (required outside 24h)" />
                  </div>
                  <label className="flex items-start gap-2 text-[12px] text-dls-text-secondary">
                    <input type="checkbox" checked={approved} onChange={(event) => setApproved(event.target.checked)} className="mt-0.5" />
                    I reviewed the exact recipient and customer-safe message and authorize this provider attempt.
                  </label>
                  <Button variant="primary" onClick={() => void sendWhatsApp()} disabled={!linkedCaseId || !draft.trim() || !consentBasis.trim() || !approved || sending}>
                    {sending ? "Requesting provider acceptance…" : "Send reviewed WhatsApp message"}
                  </Button>
                </div>
              ) : null}

              <div className="mt-3 grid gap-2 sm:grid-cols-4">
                <Button variant="secondary" onClick={() => router.push(fnolHandoffUrl(active))}>Create claim</Button>
                <Button
                  variant="secondary"
                  onClick={() => (parityMode ? toast.success("Lead created", { description: "Handed off to Customer Acquisition." }) : toast("Not configured", { description: "Lead creation from a conversation is pending its write endpoint." }))}
                >
                  Create lead
                </Button>
                <Button variant="secondary" icon={<Send aria-hidden="true" />} onClick={() => router.push("/app/communications")}>
                  Draft response
                </Button>
                <Button
                  variant="secondary"
                  icon={<ShieldAlert aria-hidden="true" />}
                  onClick={() => (parityMode ? toast("Escalated to supervisor") : toast("Not configured", { description: "Conversation escalation is pending its endpoint." }))}
                >
                  Escalate
                </Button>
              </div>

              {source === "live" ? (
                <p className="mt-2 text-[11px] text-dls-text-muted">Live data · tenant-scoped. Actions are governed, nothing dispatches automatically.</p>
              ) : null}
            </>
          )}
        </GlassCard>
      </div>
    </>
  );
}

// Carries the conversation's known identity into the FNOL wizard so an officer creating a claim
// from an inbound channel message never has to re-type what the customer already told us.
function fnolHandoffUrl(conversation: ChannelConversation | null): string {
  if (!conversation) return "/app/claims/new";
  const params = new URLSearchParams();
  if (conversation.matched) params.set("customerName", conversation.customer);
  if (conversation.customerContact) {
    if (conversation.channel === "Email") params.set("customerEmail", conversation.customerContact);
    else params.set("customerContact", conversation.customerContact);
  }
  params.set("channel", conversation.channel);
  const query = params.toString();
  return query ? `/app/claims/new?${query}` : "/app/claims/new";
}

function deliveryCopy(state: string) {
  switch (state) {
    case "Queued": return "Queued internally";
    case "ProviderAccepted": return "Accepted by WhatsApp, delivery not confirmed";
    case "Delivered": return "Delivered";
    case "Read": return "Read";
    case "Failed": return "Delivery failed";
    case "Unknown": return "No clear answer back from WhatsApp, check before sending again";
    default: return state;
  }
}
