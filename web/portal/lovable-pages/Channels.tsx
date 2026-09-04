"use client";
import { useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { Search, MessageCircle, Mail, Globe, Phone, Users2, MonitorPlay, Send, ShieldAlert, Inbox } from "lucide-react";
import { GlassCard, SectionHeader, Badge, StatusBadge } from "@/components/iip/Primitives";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { type Channel } from "@/lib/lovable-parity/mock";
import { useChannelConversations, useWhatsAppThread } from "@/lib/lovable-parity/data/channels";

const ICON: Record<Channel, any> = {
  WhatsApp: MessageCircle, Email: Mail, Web: Globe, SMS: Phone, Broker: Users2, Teams: MonitorPlay,
};

export default function Channels() {
  const router = useRouter();
  const { conversations, loading, error, source, parityMode } = useChannelConversations();
  const [filter, setFilter] = useState<"All" | Channel>("All");
  const [activeId, setActiveId] = useState<string | null>(null);
  const [draft, setDraft] = useState("");
  const [purpose, setPurpose] = useState("claim_status_update");
  const [consentBasis, setConsentBasis] = useState("");
  const [language, setLanguage] = useState("en");
  const [templateName, setTemplateName] = useState("");
  const [approved, setApproved] = useState(false);
  const [sending, setSending] = useState(false);

  const list = useMemo(
    () => (filter === "All" ? conversations : conversations.filter((c) => c.channel === filter)),
    [conversations, filter]
  );
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
          ? "Provider response unknown. Reconcile before retrying."
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
        sub="Unified customer, broker, and internal channel traffic routed into the platform. AI drafts responses — humans approve sensitive replies."
      />

      <div className="mb-4"><GuardrailBanner text="Sensitive outbound messages route through approval before send. Nothing dispatches automatically." /></div>

      <div className="grid grid-cols-12 gap-4">
        <GlassCard className="col-span-12 lg:col-span-4 !p-0 overflow-hidden">
          <div className="p-3 border-b border-line/70">
            <div className="flex items-center gap-2 rounded-xl bg-white/70 border border-line px-3 py-2 text-[12.5px] text-muted">
              <Search className="h-3.5 w-3.5" /><span>Search customers, threads…</span>
            </div>
            <div className="mt-2 flex flex-wrap gap-1.5">
              {(["All","WhatsApp","Email","Web","SMS","Broker","Teams"] as const).map((f) => (
                <button key={f} onClick={() => setFilter(f)} className={`text-[11px] rounded-full px-2.5 py-1 border ${filter === f ? "bg-ink text-white border-ink" : "bg-white text-ink-soft border-line"}`}>{f}</button>
              ))}
            </div>
          </div>
          <div className="max-h-[560px] overflow-y-auto">
            {loading ? (
              <div className="p-6 text-center text-[12.5px] text-muted">Loading conversations…</div>
            ) : list.length === 0 ? (
              <div className="p-8 text-center">
                <div className="mx-auto mb-3 grid h-10 w-10 place-items-center rounded-2xl bg-white border border-line text-ink-soft"><Inbox className="h-5 w-5" /></div>
                <div className="text-[13px] font-medium text-ink">No channel conversations</div>
                <p className="mt-1 text-[12px] text-muted">
                  {error
                    ? error
                    : "Conversation intake activates when a WhatsApp / web-chat provider is configured and ingesting. Until then this inbox stays empty rather than showing sample conversations."}
                </p>
              </div>
            ) : (
              list.map((c) => {
                const I = ICON[c.channel];
                const isActive = active ? c.id === active.id : false;
                return (
                  <button
                    key={c.id}
                    onClick={() => setActiveId(c.id)}
                    className={`w-full text-left p-3 border-b border-line/70 hover:bg-white/60 ${isActive ? "bg-white/70" : ""}`}
                  >
                    <div className="flex items-center gap-2">
                      <div className="h-8 w-8 rounded-xl bg-white border border-line grid place-items-center text-ink-soft"><I className="h-4 w-4" /></div>
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center justify-between">
                          <div className="text-[13px] font-medium text-ink truncate">{c.customer}</div>
                          <div className="text-[10.5px] text-muted">{c.updated}</div>
                        </div>
                        <div className="text-[12px] text-muted truncate">{c.snippet}</div>
                      </div>
                    </div>
                    <div className="mt-1.5 flex items-center gap-1.5 flex-wrap">
                      <Badge tone="info">{c.channel}</Badge>
                      <Badge tone="neutral">{c.intent}</Badge>
                      {!c.matched && <Badge tone="warning">Unmatched customer</Badge>}
                      {c.needsApproval && <Badge tone="violet">Approval gated</Badge>}
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
                <div className="mx-auto mb-3 grid h-11 w-11 place-items-center rounded-2xl bg-white border border-line text-ink-soft"><Inbox className="h-5 w-5" /></div>
                <div className="text-[14px] font-semibold text-ink">No conversation selected</div>
                <p className="mt-1 text-[12.5px] text-muted">Pick a conversation on the left to view its timeline and take a governed next action.</p>
              </div>
            </div>
          ) : (
            <>
              <div className="flex items-center justify-between mb-3">
                <div>
                  <div className="text-[11px] uppercase tracking-[0.14em] text-muted">{active.channel} · {active.id}</div>
                  <div className="text-[16px] font-semibold text-ink">{active.customer}</div>
                </div>
                <div className="flex items-center gap-2">
                  {!active.matched && <Badge tone="warning">Unmatched — link to customer</Badge>}
                  <StatusBadge status={active.needsApproval ? "Human Review Required" : "Active"} />
                </div>
              </div>

              <div className="rounded-2xl border border-line bg-white/70 p-4 space-y-3 max-h-[360px] overflow-y-auto">
                {parityMode ? (
                  <>
                    <div className="text-[12.5px] text-muted">Yesterday 16:41</div>
                    <div className="rounded-xl bg-line/40 px-3 py-2 text-[13px] text-ink max-w-[80%]">Hello, I had an accident this morning. What documents do I need to send?</div>
                    <div className="ml-auto rounded-xl gradient-primary text-white px-3 py-2 text-[13px] max-w-[80%]">Thank you. Please share your driving permit, national ID, vehicle photos, and the police report if available.</div>
                    <div className="rounded-xl bg-line/40 px-3 py-2 text-[13px] text-ink max-w-[80%]">{active.snippet}</div>
                  </>
                ) : active.channel === "WhatsApp" ? (
                  whatsapp.loading ? <div className="text-[12.5px] text-muted">Loading WhatsApp thread…</div>
                  : whatsapp.error ? <div className="text-[12.5px] text-red-700">{whatsapp.error}</div>
                  : whatsapp.messages.length === 0 ? <div className="text-[12.5px] text-muted">No retained messages are available for this thread.</div>
                  : whatsapp.messages.map((message) => (
                    <div key={message.messageId} className={message.direction === "Outbound" ? "ml-auto max-w-[80%]" : "max-w-[80%]"}>
                      <div className={`rounded-xl px-3 py-2 text-[13px] ${message.direction === "Outbound" ? "gradient-primary text-white" : "bg-line/40 text-ink"}`}>
                        {message.customerSafeBody}
                      </div>
                      <div className="mt-1 text-[10.5px] text-muted">
                        {message.direction === "Outbound" ? deliveryCopy(message.deliveryState) : "Received"} · {new Date(message.createdAt).toLocaleString()}
                      </div>
                    </div>
                  ))
                ) : (
                  <>
                    <div className="rounded-xl bg-line/40 px-3 py-2 text-[13px] text-ink max-w-[80%]">{active.snippet || "No message content available."}</div>
                    <div className="text-[12px] text-muted">{active.updated}</div>
                    <p className="text-[12px] text-muted">This channel does not yet expose a retained thread contract. Only the latest permitted message is shown.</p>
                  </>
                )}
              </div>

              {!parityMode && active.channel === "WhatsApp" ? (
                <div className="mt-3 rounded-2xl border border-line bg-white/70 p-4 space-y-3">
                  <div>
                    <div className="text-[13px] font-semibold text-ink">Reviewed WhatsApp reply</div>
                    <p className="text-[11.5px] text-muted">Exact content, recipient, purpose, policy basis, language and template are recorded before one provider attempt.</p>
                  </div>
                  {!linkedCaseId ? <div className="rounded-xl bg-amber-50 px-3 py-2 text-[12px] text-amber-900">Link this thread to an authorized case before replying.</div> : null}
                  <textarea aria-label="Customer-safe WhatsApp message" value={draft} onChange={(event) => setDraft(event.target.value)} rows={4} maxLength={4096} placeholder="Write only customer-safe content…" className="w-full rounded-xl border border-line bg-white px-3 py-2 text-[13px] text-ink" />
                  <div className="grid sm:grid-cols-2 gap-2">
                    <input aria-label="Communication purpose" value={purpose} onChange={(event) => setPurpose(event.target.value)} placeholder="Purpose" className="rounded-xl border border-line bg-white px-3 py-2 text-[12.5px]" />
                    <input aria-label="Consent or legal basis" value={consentBasis} onChange={(event) => setConsentBasis(event.target.value)} placeholder="Consent or legal basis reference" className="rounded-xl border border-line bg-white px-3 py-2 text-[12.5px]" />
                    <input aria-label="Message language" value={language} onChange={(event) => setLanguage(event.target.value)} placeholder="Language code" className="rounded-xl border border-line bg-white px-3 py-2 text-[12.5px]" />
                    <input aria-label="Approved template name" value={templateName} onChange={(event) => setTemplateName(event.target.value)} placeholder="Approved template (required outside 24h)" className="rounded-xl border border-line bg-white px-3 py-2 text-[12.5px]" />
                  </div>
                  <label className="flex items-start gap-2 text-[12px] text-ink-soft">
                    <input type="checkbox" checked={approved} onChange={(event) => setApproved(event.target.checked)} className="mt-0.5" />
                    I reviewed the exact recipient and customer-safe message and authorize this provider attempt.
                  </label>
                  <button onClick={() => void sendWhatsApp()} disabled={!linkedCaseId || !draft.trim() || !consentBasis.trim() || !approved || sending} className="rounded-2xl gradient-primary text-white px-4 py-2 text-[12.5px] disabled:opacity-50">
                    {sending ? "Requesting provider acceptance…" : "Send reviewed WhatsApp message"}
                  </button>
                </div>
              ) : null}

              <div className="mt-3 grid sm:grid-cols-4 gap-2">
                <button onClick={() => router.push("/app/claims/new")} className="rounded-2xl bg-white border border-line px-3 py-2 text-[12.5px] text-ink hover-lift">Create claim</button>
                <button
                  onClick={() => (parityMode ? toast.success("Lead created", { description: "Handed off to Customer Acquisition." }) : toast("Not configured", { description: "Lead creation from a conversation is pending its write endpoint." }))}
                  className="rounded-2xl bg-white border border-line px-3 py-2 text-[12.5px] text-ink hover-lift"
                >
                  Create lead
                </button>
                <button onClick={() => router.push("/app/communications")} className="rounded-2xl bg-white border border-line px-3 py-2 text-[12.5px] text-ink hover-lift inline-flex items-center justify-center gap-1"><Send className="h-3.5 w-3.5" /> Draft response</button>
                <button
                  onClick={() => (parityMode ? toast("Escalated to supervisor") : toast("Not configured", { description: "Conversation escalation is pending its endpoint." }))}
                  className="rounded-2xl bg-white border border-line px-3 py-2 text-[12.5px] text-ink hover-lift inline-flex items-center justify-center gap-1"
                >
                  <ShieldAlert className="h-3.5 w-3.5" /> Escalate
                </button>
              </div>

              {source === "live" ? (
                <p className="mt-2 text-[11px] text-muted">Live data · tenant-scoped. Actions are governed — nothing dispatches automatically.</p>
              ) : null}
            </>
          )}
        </GlassCard>
      </div>
    </>
  );
}

function deliveryCopy(state: string) {
  switch (state) {
    case "Queued": return "Queued internally";
    case "ProviderAccepted": return "Accepted by WhatsApp — delivery not confirmed";
    case "Delivered": return "Delivered";
    case "Read": return "Read";
    case "Failed": return "Delivery failed";
    case "Unknown": return "Provider response unknown — reconcile before retrying";
    default: return state;
  }
}
