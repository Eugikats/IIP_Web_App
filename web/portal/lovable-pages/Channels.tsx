"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import Link from "next/link";
import { ArrowLeft, MessageCircle } from "lucide-react";
import { toast } from "sonner";
import ChatThreadPane from "@/components/cip/inbox/ChatThreadPane";
import ConversationListPane from "@/components/cip/inbox/ConversationListPane";
import CustomerInfoPanel from "@/components/cip/inbox/CustomerInfoPanel";
import ChannelIdentityPanel from "@/components/iip/ChannelIdentityPanel";
import { SideDrawer } from "@/components/ui/SideDrawer";
import { useChannelConversations, useChannelThread, useConversationControl } from "@/lib/lovable-parity/data/channels";
import { useThreadProspect } from "@/lib/lovable-parity/data/salesLeads";
import { useUI } from "@/context/UIStateContext";
import { useWhatsAppHealth, whatsAppHealthLabel } from "@/lib/whatsappHealth";

// The chat screen in CIP's three-pane layout: conversations, the thread, and what is known about the
// customer. Kato answers until a person takes the conversation; the right-hand panel shows the fixed
// set of facts and the reading of the conversation that Kato's insights agent keeps up to date.

const REFRESH_MS = 10_000;

export default function Channels() {
  const { roleDef } = useUI();
  const whatsAppStatus = useWhatsAppHealth(roleDef.key);
  const { conversations, loading, error, reload, parityMode } = useChannelConversations();
  const [activeId, setActiveId] = useState<string | null>(null);
  const [mobileView, setMobileView] = useState<"list" | "thread">("list");
  const [infoOpen, setInfoOpen] = useState(false);

  // The layout follows the room the screen actually has, not the browser width: with the Ask IIP panel
  // open the same window leaves far less. Three panes need about 1,100px, two about 720px.
  const boxRef = useRef<HTMLDivElement | null>(null);
  const [boxWidth, setBoxWidth] = useState(1200);
  useEffect(() => {
    const node = boxRef.current;
    if (!node) return;
    const observer = new ResizeObserver(([entry]) => setBoxWidth(entry.contentRect.width));
    observer.observe(node);
    return () => observer.disconnect();
  }, []);
  const layout: "three" | "two" | "one" = boxWidth >= 1100 ? "three" : boxWidth >= 720 ? "two" : "one";

  // A link from elsewhere (a prospect's row, a cited message) opens that conversation directly.
  useEffect(() => {
    const thread = new URLSearchParams(window.location.search).get("thread");
    if (thread) {
      setActiveId(thread);
      setMobileView("thread");
    }
  }, []);

  const active = useMemo(() => conversations.find((conversation) => conversation.id === activeId) ?? null, [conversations, activeId]);
  const isWhatsApp = active?.channel === "WhatsApp";
  const threadId = !parityMode && isWhatsApp ? active!.id : null;

  const channelThread = useChannelThread(!parityMode ? active?.channel : null, !parityMode ? active?.id : null);
  const control = useConversationControl(threadId);
  const prospect = useThreadProspect(threadId);

  // Keep the screen live: new messages, a person taking over, and fresh insights appear on their own.
  const { reload: reloadThread } = channelThread;
  const { reload: reloadControl } = control;
  const { reload: reloadProspect } = prospect;
  useEffect(() => {
    const timer = window.setInterval(() => {
      if (document.visibilityState !== "visible") return;
      void reload();
      if (!parityMode && active) {
        void reloadThread();
        if (threadId) {
          void reloadControl();
          void reloadProspect();
        }
      }
    }, REFRESH_MS);
    return () => window.clearInterval(timer);
  }, [reload, reloadThread, reloadControl, reloadProspect, threadId, active, parityMode]);

  const select = useCallback((id: string) => {
    setActiveId(id);
    setMobileView("thread");
  }, []);

  const onSend = useCallback(async (input: { body: string; templateName?: string; templateButtonPayload?: string; caseId?: string | null; language: string }) => {
    const recipient = channelThread.recipientId ?? "";
    const result = await channelThread.send({
      caseId: input.caseId ?? null,
      recipientWaId: recipient,
      customerSafeBody: input.body,
      purpose: input.caseId ? "claim_status_update" : "customer_conversation",
      consentBasis: "The customer wrote to us first on this conversation",
      language: input.language,
      templateName: input.templateName,
      templateButtonPayload: input.templateButtonPayload,
      humanApproved: true,
    });
    if (result.deliveryState === "Failed") throw new Error(result.safeError || `${active?.channel ?? "Message"} did not accept the message.`);
    if (result.deliveryState === "Unknown") toast("Not confirmed yet", { description: `${active?.channel ?? "The provider"} did not confirm. Check the conversation before sending again.` });
    if (isWhatsApp) void control.reload();
    void reload();
  }, [active?.channel, channelThread, control, isWhatsApp, reload]);

  return (
    <div className="cip-acq space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h1 className="m-0 text-[20px] font-semibold tracking-tight text-[#0B1640]">Inbox</h1>
          <p className="m-0 mt-0.5 text-[12.5px] text-slate-500">Messages sent to channels connected to this workspace, with what the assistant has learned about each person.</p>
          <span className="mt-2 inline-flex items-center gap-1.5 text-[11px] font-medium text-slate-600" aria-live="polite">
            <span className={`h-1.5 w-1.5 rounded-full ${whatsAppStatus === "ready" ? "bg-emerald-500" : "bg-slate-500"}`} aria-hidden />
            {whatsAppHealthLabel(whatsAppStatus)}
          </span>
        </div>
        <Link href="/app/acquisition" className="inline-flex items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-[12px] font-semibold text-slate-700 hover:bg-slate-50">
          <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
          Acquisition dashboard
        </Link>
      </div>

      <div
        ref={boxRef}
        className="grid h-[calc(100dvh-13rem)] min-h-[34rem] overflow-hidden rounded-2xl border border-[#E6EAF2] bg-white shadow-[0_1px_2px_rgba(15,23,42,0.03)]"
        style={{ gridTemplateColumns: layout === "three" ? "20rem minmax(0,1fr) 22rem" : layout === "two" ? "19rem minmax(0,1fr)" : "minmax(0,1fr)" }}
      >
        <div className={`min-h-0 ${layout === "one" && mobileView === "thread" ? "hidden" : ""}`}>
          <ConversationListPane conversations={conversations} selectedId={activeId} loading={loading} error={error} whatsAppStatus={whatsAppStatus} onSelect={select} />
        </div>

        <div className={`min-h-0 min-w-0 ${layout === "one" && mobileView === "list" ? "hidden" : ""}`}>
          {active ? (
            <ChatThreadPane
              conversation={{ ...active, controlMode: control.control?.mode ?? active.controlMode }}
              messages={channelThread.messages}
              pendingReplyDraft={channelThread.pendingReplyDraft}
              canReplyInInbox={channelThread.canReplyInInbox}
              loading={channelThread.loading}
              error={channelThread.error}
              onRefresh={() => { void reloadThread(); void reloadControl(); void reloadProspect(); }}
              control={control.control}
              controlSaving={control.saving}
              controlError={control.error}
              onTakeOver={control.takeOver}
              onHandBack={control.handBack}
              onSend={onSend}
              onBack={layout === "one" ? () => setMobileView("list") : undefined}
              onOpenInfo={layout !== "three" ? () => setInfoOpen(true) : undefined}
            />
          ) : (
            <div className="flex h-full flex-col items-center justify-center bg-[#F7F9FC] px-6 text-center">
              <span className="mb-3 inline-flex h-12 w-12 items-center justify-center rounded-full bg-white text-slate-300 shadow-sm"><MessageCircle className="h-6 w-6" aria-hidden /></span>
              <p className="m-0 text-[14px] font-bold text-[#0B1640]">Select a conversation</p>
              <p className="m-0 mt-1 max-w-sm text-[12.5px] text-slate-500">View the full message thread with customer messages, Kato&apos;s replies and your own.</p>
            </div>
          )}
        </div>

        <div className={`min-h-0 border-l border-[#E6EAF2] ${layout === "three" ? "" : "hidden"}`}>
          {active ? <>
            {isWhatsApp ? (
              <CustomerInfoPanel
                conversation={active}
                detail={prospect.detail}
                loading={prospect.loading}
                error={prospect.error}
                addNote={prospect.addNote}
                assign={prospect.assign}
                refreshInsights={prospect.refreshInsights}
                capture={prospect.capture}
                onChanged={() => void prospect.reload()}
              />
            ) : <div className="px-4 py-4">
              <p className="m-0 text-[14px] font-bold text-[#0B1640]">Customer information</p>
              <p className="m-0 mt-1 text-[12px] text-slate-600">Not configured: customer details and insights are unavailable for {active.channel}. You can still record a verified identity link to an existing customer.</p>
            </div>}
            <ChannelIdentityPanel channel={active.channel} threadId={active.id} />
          </> : (
            <div className="flex h-full flex-col items-center justify-center px-6 text-center">
              <p className="m-0 text-[14px] font-bold text-[#0B1640]">Customer information</p>
              <p className="m-0 mt-1 text-[12.5px] text-slate-500">Select a conversation to see identity and customer information.</p>
            </div>
          )}
        </div>
      </div>

      {active ? (
        <SideDrawer open={infoOpen} title="Customer information" onClose={() => setInfoOpen(false)}>
          <div className="-mx-[22px] -my-5 h-[calc(100vh-9rem)]">
            {isWhatsApp ? <CustomerInfoPanel
                showTitle={false}
                conversation={active}
                detail={prospect.detail}
                loading={prospect.loading}
                error={prospect.error}
                addNote={prospect.addNote}
                assign={prospect.assign}
                refreshInsights={prospect.refreshInsights}
                capture={prospect.capture}
                onChanged={() => void prospect.reload()}
              /> : null}
            <div className={isWhatsApp ? "" : "pt-3"}>
              <ChannelIdentityPanel channel={active.channel} threadId={active.id} />
            </div>
          </div>
        </SideDrawer>
      ) : null}
    </div>
  );
}
