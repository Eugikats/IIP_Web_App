"use client";

// Ported from the CIP web app (WhatsApp inbox, LeadContextPanel): the customer's header, then rows of
// icon tile, small label and value, then notes. The rows are a fixed list; the assistant fills only
// these fields, only with what the customer said, and every point of its reading cites a message.

import { useState } from "react";
import {
  Briefcase, Building2, Check, FileText, Loader2, Lock, MapPin, MessageSquare, RefreshCw, Shield, Sparkles, Target, UserPlus, Users, Wallet,
  type LucideIcon,
} from "lucide-react";
import { toast } from "sonner";
import type { ChannelConversation } from "@/lib/lovable-parity/mock";
import type { CreateLeadInput, LeadResult, SalesLeadDetail } from "@/lib/lovable-parity/data/salesLeads";
import { AssignControl, Citations, PointList, PointText } from "@/components/acquisition/LeadCustomerModal";
import { formatAgeSince, formatDateTime } from "@/lib/format";
import { conversationInitials } from "./ConversationListPane";

const FACT_ICON: Record<string, LucideIcon> = {
  occupation: Briefcase,
  business: Briefcase,
  area: MapPin,
  income_band: Wallet,
  dependants: Users,
  existing_cover: Shield,
  worries: MessageSquare,
  monthly_budget: Wallet,
  timing: Target,
  bank_products: Building2,
  branch: Building2,
};

function DetailRow({ icon: Icon, label, value, note }: { icon: LucideIcon; label: string; value: React.ReactNode; note?: string }) {
  return (
    <div className="flex items-start gap-3 border-b border-slate-100 py-3 last:border-b-0">
      <span className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-[#F7F9FC] text-slate-500">
        <Icon className="h-4 w-4" aria-hidden />
      </span>
      <div className="min-w-0 flex-1">
        <p className="m-0 text-[10px] font-semibold uppercase tracking-wide text-slate-400">{label}</p>
        <div className="mt-0.5 text-[13px] font-semibold text-[#0B1640]">{value}</div>
        {note ? <p className="m-0 mt-0.5 text-[10.5px] text-slate-400">{note}</p> : null}
      </div>
    </div>
  );
}

export default function CustomerInfoPanel({
  conversation,
  detail,
  loading,
  error,
  addNote,
  assign,
  refreshInsights,
  capture,
  onChanged,
  showTitle = true,
}: {
  showTitle?: boolean;
  conversation: ChannelConversation;
  detail: SalesLeadDetail | null;
  loading: boolean;
  error: string | null;
  addNote: (leadId: string, text: string) => Promise<LeadResult>;
  assign: (leadId: string, userId: string | null) => Promise<LeadResult>;
  refreshInsights: (leadId: string) => Promise<LeadResult>;
  capture: (input: CreateLeadInput) => Promise<{ ok: boolean; message?: string }>;
  onChanged: (detail: SalesLeadDetail) => void;
}) {
  const [note, setNote] = useState("");
  const [savingNote, setSavingNote] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [capturing, setCapturing] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);

  const name = detail?.customerName ?? conversation.customer;

  async function saveNote() {
    if (!detail) return;
    setSavingNote(true);
    const result = await addNote(detail.leadId, note);
    setSavingNote(false);
    if (result.ok) {
      setNote("");
      toast.success("Note saved");
    } else toast.error(result.message);
  }

  async function refresh() {
    if (!detail) return;
    setRefreshing(true);
    setProblem(null);
    const result = await refreshInsights(detail.leadId);
    setRefreshing(false);
    if (!result.ok) setProblem(result.message);
  }

  async function captureLead() {
    setCapturing(true);
    const result = await capture({
      customerName: name.startsWith("WhatsApp contact") ? "WhatsApp contact" : name,
      leadSource: "WhatsApp conversation",
      customerNeed: conversation.snippet || "Wrote in on WhatsApp",
      contactInfo: conversation.customerContact ?? undefined,
    });
    setCapturing(false);
    if (result.ok) toast.success("Added to prospects");
    else toast.error(result.message ?? "The lead could not be saved.");
  }

  const insights = detail?.insights ?? null;
  const conversationHref = `/app/channels?thread=${encodeURIComponent(conversation.id)}`;

  return (
    <div className="flex h-full min-h-0 flex-col bg-white">
      {showTitle ? (
        <div className="border-b border-[#E6EAF2] px-4 py-3">
          <h2 className="m-0 text-[14px] font-bold text-[#0B1640]">Customer information</h2>
        </div>
      ) : null}
      <div className="min-h-0 flex-1 space-y-4 overflow-y-auto px-4 py-4">
        <div className="flex items-center gap-3">
          <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-full bg-[#EEF4FF] text-[16px] font-bold text-[#1769FF]">{conversationInitials(name)}</div>
          <div className="min-w-0">
            <p className="m-0 truncate text-[16px] font-bold text-[#0B1640]">{name}</p>
            <p className="m-0 truncate text-[12px] text-slate-500">
              {detail ? `${detail.maskedContact} · ${detail.primaryChannel ?? conversation.channel}` : conversation.customerContact ? conversation.customerContact.replace(/\D/g, "").replace(/^(\d{2}).*(\d{2})$/, "$1***$2") : conversation.channel}
            </p>
            {detail ? <p className="m-0 mt-0.5 text-[12px] font-semibold text-[#1769FF]">{detail.relationshipLabel}</p> : null}
          </div>
        </div>

        {loading && !detail ? (
          <div className="space-y-2">{[...Array(4)].map((_, index) => <div key={index} className="h-12 animate-pulse rounded-xl bg-[#F7F9FC]" />)}</div>
        ) : error ? (
          <p className="text-[12px] text-rose-600">{error}</p>
        ) : !detail ? (
          <div className="rounded-xl border border-[#E6EAF2] bg-[#F7F9FC]/60 p-3">
            <p className="m-0 text-[12.5px] font-bold text-[#0B1640]">Not a prospect yet</p>
            <p className="m-0 mt-1 text-[11.5px] leading-relaxed text-slate-500">
              They have written in but have not asked about a cover. They become a prospect on their own once they do, or you can add them now.
            </p>
            <button
              type="button"
              onClick={() => void captureLead()}
              disabled={capturing}
              className="mt-3 inline-flex items-center gap-1.5 rounded-lg bg-[#1769FF] px-3 py-1.5 text-[11px] font-semibold text-white hover:bg-[#1258d6] disabled:opacity-50"
            >
              {capturing ? <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden /> : <UserPlus className="h-3.5 w-3.5" aria-hidden />}
              Add to prospects
            </button>
          </div>
        ) : (
          <>
            <div className="rounded-xl border border-[#E6EAF2] px-3">
              <DetailRow icon={Target} label="Stage" value={detail.stageLabel} />
              <DetailRow icon={FileText} label="Product interest" value={detail.productInterest ?? "Not yet known"} note={detail.interests[0]?.reason || undefined} />
              {detail.interests.length > 1 ? (
                <DetailRow
                  icon={FileText}
                  label="Also interested in"
                  value={<span className="block space-y-0.5">{detail.interests.slice(1).map((interest) => <span key={interest.product} className="block">{interest.product} <span className="font-normal text-slate-500">({interest.strengthLabel})</span></span>)}</span>}
                />
              ) : null}
              <DetailRow icon={Building2} label="Bank customer" value={detail.bankCustomerLabel} />
              <DetailRow icon={Shield} label="Agreed to follow-up" value={detail.consent.statusLabel} note={detail.consent.at ? formatDateTime(detail.consent.at) : undefined} />
              <DetailRow icon={UserPlus} label="Assigned officer" value={<AssignControl detail={detail} onAssigned={onChanged} assign={assign} />} />
              {detail.facts.map((fact) => (
                <DetailRow key={fact.key} icon={FACT_ICON[fact.key] ?? FileText} label={fact.label} value={fact.value} note={fact.source} />
              ))}
            </div>

            {detail.missing.length > 0 ? (
              <div>
                <p className="m-0 mb-1.5 text-[10px] font-semibold uppercase tracking-wide text-slate-400">Still unknown</p>
                <div className="flex flex-wrap gap-1.5">
                  {detail.missing.map((item) => <span key={item} className="rounded-full border border-slate-200 bg-white px-2 py-0.5 text-[11px] text-slate-500">{item}</span>)}
                </div>
              </div>
            ) : null}

            <div className="rounded-xl border border-[#1769FF]/20 bg-[#F7FAFF] p-3">
              <div className="flex items-start justify-between gap-2">
                <div className="min-w-0">
                  <p className="m-0 inline-flex items-center gap-1.5 text-[12px] font-bold text-[#0B1640]"><Sparkles className="h-3.5 w-3.5 text-[#1769FF]" aria-hidden /> What the conversation shows</p>
                  <p className="m-0 mt-0.5 text-[10.5px] text-slate-500">
                    {insights ? `Updated ${formatAgeSince(insights.generatedAt)} ago, from ${insights.messageCount} messages` : "Not prepared yet. It runs on its own shortly after the customer writes."}
                  </p>
                </div>
                <button type="button" onClick={() => void refresh()} disabled={refreshing} aria-label="Refresh insights" className="rounded-lg p-1.5 text-slate-500 hover:bg-white disabled:opacity-50">
                  <RefreshCw className={`h-3.5 w-3.5 ${refreshing ? "animate-spin" : ""}`} />
                </button>
              </div>
              {detail.insightsStale && insights ? <p className="m-0 mt-2 text-[11px] text-amber-700">The conversation has moved on since this was prepared.</p> : null}
              {problem ? <p className="m-0 mt-2 text-[11px] text-rose-600" role="alert">{problem}</p> : null}
              {insights && !insights.enoughConversation ? (
                <p className="m-0 mt-2 text-[11.5px] text-slate-500">Not enough conversation yet. Notes are prepared once the customer has written at least twice.</p>
              ) : insights ? (
                <div className="mt-3 space-y-3 text-[12.5px]">
                  {insights.summary ? <PointText point={insights.summary} insights={insights} conversationHref={conversationHref} lead /> : null}
                  {insights.problems.length > 0 ? <PointList title="Worries" points={insights.problems} insights={insights} conversationHref={conversationHref} empty="None described." /> : null}
                  {insights.objections.length > 0 ? <PointList title="Doubts" points={insights.objections} insights={insights} conversationHref={conversationHref} empty="None raised." /> : null}
                  {insights.interests.length > 0 ? (
                    <ul className="m-0 space-y-1 p-0">
                      {insights.interests.map((interest) => (
                        <li key={interest.product} className="list-none text-[12.5px] text-[#0B1640]">
                          <span className="font-semibold">{interest.product}</span> <Citations refs={interest.refs} insights={insights} conversationHref={conversationHref} />
                        </li>
                      ))}
                    </ul>
                  ) : null}
                  {insights.nextStep ? (
                    <div className="rounded-lg border border-[#E6EAF2] bg-white p-2.5">
                      <p className="m-0 text-[10px] font-semibold uppercase tracking-wide text-slate-400">Suggested next step</p>
                      <div className="mt-1"><PointText point={insights.nextStep} insights={insights} conversationHref={conversationHref} /></div>
                      <p className="m-0 mt-1 text-[10px] text-slate-400">A suggestion only. You decide.</p>
                    </div>
                  ) : null}
                </div>
              ) : null}
            </div>

            <p className="m-0 text-[10px] text-slate-400">AI-assisted insight · Advisory only · A person decides</p>

            <div className="rounded-xl border border-[#E6EAF2] bg-[#F7F9FC]/60 p-3">
              <div className="mb-2 flex items-center justify-between">
                <p className="m-0 text-[12px] font-bold text-[#0B1640]">Notes</p>
                <Lock className="h-3 w-3 text-slate-400" aria-hidden />
              </div>
              <textarea
                value={note}
                onChange={(event) => setNote(event.target.value.slice(0, 500))}
                rows={3}
                placeholder="Add an officer note…"
                aria-label="Officer note"
                className="w-full resize-none rounded-xl border border-[#E6EAF2] bg-white px-3 py-2 text-[12px] text-[#0B1640] outline-none focus:border-[#1769FF]"
              />
              <div className="mt-1.5 flex items-center justify-between gap-2">
                <p className="m-0 text-[10px] text-slate-400">{note.length}/500</p>
                <button
                  type="button"
                  disabled={!note.trim() || savingNote}
                  onClick={() => void saveNote()}
                  className="inline-flex items-center gap-1.5 rounded-lg bg-[#1769FF] px-3 py-1.5 text-[11px] font-semibold text-white disabled:bg-slate-300"
                >
                  {savingNote ? <Loader2 className="h-3 w-3 animate-spin" aria-hidden /> : <Check className="h-3 w-3" aria-hidden />}
                  Save note
                </button>
              </div>
              {detail.notes.length === 0 ? (
                <p className="m-0 mt-3 text-[11px] leading-relaxed text-slate-500">No notes yet. Notes are visible to authorised team members only.</p>
              ) : (
                <ul className="m-0 mt-3 space-y-2 p-0">
                  {detail.notes.map((item) => (
                    <li key={item.noteId} className="list-none rounded-lg border border-[#E6EAF2] bg-white px-2.5 py-2">
                      <p className="m-0 text-[12px] leading-relaxed text-[#0B1640]">{item.text}</p>
                      <p className="m-0 mt-1 text-[10px] text-slate-400">{item.authorName} · {formatDateTime(item.at)}</p>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </>
        )}
      </div>
    </div>
  );
}
