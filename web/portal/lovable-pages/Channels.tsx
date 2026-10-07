"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { ArrowLeft, MessageCircle } from "lucide-react";
import { toast } from "sonner";
import ChatThreadPane from "@/components/cip/inbox/ChatThreadPane";
import ConversationListPane from "@/components/cip/inbox/ConversationListPane";
import CustomerInfoPanel from "@/components/cip/inbox/CustomerInfoPanel";
import { SideDrawer } from "@/components/ui/SideDrawer";
import { useChannelConversations, useConversationControl, useWhatsAppThread } from "@/lib/lovable-parity/data/channels";
import { useThreadProspect } from "@/lib/lovable-parity/data/salesLeads";

// The chat screen in CIP's three-pane layout: conversations, the thread, and what is known about the
// customer. Kato answers until a person takes the conversation; the right-hand panel shows the fixed
// set of facts and the reading of the conversation that Kato's insights agent keeps up to date.

const REFRESH_MS = 10_000;

export default function Channels() {
  const { conversations, loading, error, reload, parityMode } = useChannelConversations();
  const [activeId, setActiveId] = useState<string | null>(null);
  const [mobileView, setMobileView] = useState<"list" | "thread">("list");
  const [infoOpen, setInfoOpen] = useState(false);

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

  const whatsapp = useWhatsAppThread(threadId);
  const control = useConversationControl(threadId);
  const prospect = useThreadProspect(threadId);

  // Keep the screen live: new messages, a person taking over, and fresh insights appear on their own.
  const { reload: reloadThread } = whatsapp;
  const { reload: reloadControl } = control;
  const { reload: reloadProspect } = prospect;
  useEffect(() => {
    const timer = window.setInterval(() => {
      if (document.visibilityState !== "visible") return;
      void reload();
      if (threadId) {
        void reloadThread();
        void reloadControl();
        void reloadProspect();
      }
    }, REFRESH_MS);
    return () => window.clearInterval(timer);
  }, [reload, reloadThread, reloadControl, reloadProspect, threadId]);

  const select = useCallback((id: string) => {
    setActiveId(id);
    setMobileView("thread");
  }, []);

  const onSend = useCallback(async (input: { body: string; templateName?: string; caseId?: string | null; language: string }) => {
    const recipient = whatsapp.messages.find((message) => message.direction === "Inbound")?.contactWaId ?? whatsapp.messages[0]?.contactWaId ?? "";
    const result = await whatsapp.send({
      caseId: input.caseId ?? null,
      recipientWaId: recipient,
      customerSafeBody: input.body,
      purpose: input.caseId ? "claim_status_update" : "customer_conversation",
      consentBasis: "The customer wrote to us first on this conversation",
      language: input.language,
      templateName: input.templateName,
      humanApproved: true,
    });
    if (result.deliveryState === "Failed") throw new Error(result.safeError || "WhatsApp did not accept the message.");
    if (result.deliveryState === "Unknown") toast("Not confirmed yet", { description: "WhatsApp did not answer clearly. Check that it arrived before sending again." });
    void control.reload();
    void reload();
  }, [whatsapp, control, reload]);

  return (
    <div className="cip-acq space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h1 className="m-0 text-[20px] font-semibold tracking-tight text-[#0B1640]">Inbox</h1>
          <p className="m-0 mt-0.5 text-[12.5px] text-slate-500">Everyone who writes to you, with what the assistant has learned about them.</p>
        </div>
        <Link href="/app/acquisition" className="inline-flex items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-[12px] font-semibold text-slate-700 hover:bg-slate-50">
          <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
          Acquisition dashboard
        </Link>
      </div>

      <div className="grid h-[calc(100dvh-13rem)] min-h-[34rem] grid-cols-1 overflow-hidden rounded-2xl border border-[#E6EAF2] bg-white shadow-[0_1px_2px_rgba(15,23,42,0.03)] md:grid-cols-[19rem_minmax(0,1fr)] xl:grid-cols-[20rem_minmax(0,1fr)_22rem]">
        <div className={`min-h-0 ${mobileView === "thread" ? "hidden md:block" : ""}`}>
          <ConversationListPane conversations={conversations} selectedId={activeId} loading={loading} error={error} onSelect={select} />
        </div>

        <div className={`min-h-0 ${mobileView === "list" ? "hidden md:block" : ""}`}>
          {active ? (
            <ChatThreadPane
              conversation={{ ...active, controlMode: control.control?.mode ?? active.controlMode }}
              messages={whatsapp.messages}
              loading={whatsapp.loading}
              error={whatsapp.error}
              onRefresh={() => { void reloadThread(); void reloadControl(); void reloadProspect(); }}
              control={control.control}
              controlSaving={control.saving}
              controlError={control.error}
              onTakeOver={control.takeOver}
              onHandBack={control.handBack}
              onSend={onSend}
              onBack={() => setMobileView("list")}
              onOpenInfo={isWhatsApp ? () => setInfoOpen(true) : undefined}
            />
          ) : (
            <div className="flex h-full flex-col items-center justify-center bg-[#F7F9FC] px-6 text-center">
              <span className="mb-3 inline-flex h-12 w-12 items-center justify-center rounded-full bg-white text-slate-300 shadow-sm"><MessageCircle className="h-6 w-6" aria-hidden /></span>
              <p className="m-0 text-[14px] font-bold text-[#0B1640]">Select a conversation</p>
              <p className="m-0 mt-1 max-w-sm text-[12.5px] text-slate-500">View the full message thread with customer messages, Kato&apos;s replies and your own.</p>
            </div>
          )}
        </div>

        <div className="hidden min-h-0 border-l border-[#E6EAF2] xl:block">
          {active && isWhatsApp ? (
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
          ) : (
            <div className="flex h-full flex-col items-center justify-center px-6 text-center">
              <p className="m-0 text-[14px] font-bold text-[#0B1640]">Customer information</p>
              <p className="m-0 mt-1 text-[12.5px] text-slate-500">{active ? `Not configured: customer details and insights are prepared for WhatsApp conversations, not for ${active.channel.toLowerCase()} yet.` : "Select a conversation to see who they are and what they want."}</p>
            </div>
          )}
        </div>
      </div>

      {active && isWhatsApp ? (
        <SideDrawer open={infoOpen} title="Customer information" onClose={() => setInfoOpen(false)}>
          <div className="-mx-[22px] -my-5 h-[calc(100vh-9rem)]">
            <CustomerInfoPanel
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
            />
          </div>
        </SideDrawer>
      ) : null}
    </div>
  );
}
