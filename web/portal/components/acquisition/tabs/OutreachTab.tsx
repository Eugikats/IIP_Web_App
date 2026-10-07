"use client";

import { Fragment, useMemo, useState } from "react";
import { Clock, Loader2, PenLine, Send, Sparkles } from "lucide-react";
import { toast } from "sonner";
import CipStatusPill from "@/components/cip/CipStatusPill";
import type { OutreachDraft, OutreachSendResult, SalesLeadView } from "@/lib/lovable-parity/data/salesLeads";
import { KpiRow, ReportState, TableCard, th } from "./ReportParts";

type Tone = "success" | "warning" | "danger" | "info" | "neutral" | "violet";
type Result<T> = Promise<{ ok: true; data: T } | { ok: false; message: string }>;

const WAITING = new Set(["Draft", "Edited"]);

function statusTone(status: string): Tone {
  return status === "Sent" ? "success" : status === "DemoSent" ? "info" : status === "Blocked" ? "danger" : status === "Edited" ? "warning" : "neutral";
}

/** Messages the drafting agent has prepared, for a person to read, change and approve. Nothing here is sent
 *  until someone presses the send button, and each send is checked again on the server. */
export default function OutreachTab({
  leads,
  loading,
  error,
  forbidden,
  reload,
  draftOutreach,
  draftFollowUps,
  editOutreach,
  sendOutreach,
  sendOutreachMany,
  dismissOutreach,
}: {
  leads: SalesLeadView[];
  loading: boolean;
  error: string | null;
  forbidden: boolean;
  reload: () => Promise<void>;
  draftOutreach: (leadIds?: string[]) => Result<{ drafted: number; skipped: number }>;
  draftFollowUps: () => Result<{ quiet: number; ending: number; skipped: number }>;
  editOutreach: (leadId: string, draftId: string, text: string) => Result<OutreachDraft>;
  sendOutreach: (leadId: string, draftId: string) => Result<OutreachSendResult>;
  sendOutreachMany: (drafts: { leadId: string; draftId: string }[]) => Result<OutreachSendResult[]>;
  dismissOutreach: (leadId: string, draftId: string) => Result<null>;
}) {
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [editing, setEditing] = useState<string | null>(null);
  const [text, setText] = useState("");
  const [busy, setBusy] = useState<string | null>(null);
  const [confirming, setConfirming] = useState(false);

  const rows = useMemo(
    () => leads.filter((lead) => lead.latestOutreach).sort((a, b) => (b.latestOutreach!.createdAt).localeCompare(a.latestOutreach!.createdAt)),
    [leads],
  );
  const waiting = rows.filter((lead) => WAITING.has(lead.latestOutreach!.status));
  const count = (status: string) => rows.filter((lead) => lead.latestOutreach!.status === status).length;
  const chosen = waiting.filter((lead) => selected.has(lead.latestOutreach!.draftId));

  const toggle = (id: string) => setSelected((current) => {
    const next = new Set(current);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    return next;
  });

  const report = (result: OutreachSendResult) => {
    if (result.status === "Sent") toast.success("Sent on WhatsApp");
    else if (result.status === "DemoSent") toast("Demo send recorded", { description: "Shown in the inbox; not delivered to a phone." });
    else toast.error("Could not send", { description: result.reason ?? result.statusLabel });
  };

  const draftAll = async () => {
    setBusy("draft");
    const result = await draftOutreach();
    setBusy(null);
    if (result.ok) toast.success(`Drafted ${result.data.drafted} messages`, { description: result.data.skipped ? `${result.data.skipped} skipped: already drafted, no agreement to be contacted, or no recommended cover.` : undefined });
    else toast.error(result.message);
  };

  const followUps = async () => {
    setBusy("followups");
    const result = await draftFollowUps();
    setBusy(null);
    if (!result.ok) return toast.error(result.message);
    const total = result.data.quiet + result.data.ending;
    toast(total > 0 ? `Drafted ${total} follow-ups` : "Nobody needs a follow-up right now", {
      description: total > 0 ? `${result.data.quiet} quiet clients and ${result.data.ending} policies ending soon.` : "People who wrote recently, or have not agreed to be contacted, are left alone.",
    });
  };

  const sendOne = async (lead: SalesLeadView) => {
    setBusy(lead.latestOutreach!.draftId);
    const result = await sendOutreach(lead.leadId, lead.latestOutreach!.draftId);
    setBusy(null);
    if (result.ok) report(result.data);
    else toast.error(result.message);
  };

  const sendChosen = async () => {
    setBusy("bulk");
    setConfirming(false);
    const result = await sendOutreachMany(chosen.map((lead) => ({ leadId: lead.leadId, draftId: lead.latestOutreach!.draftId })));
    setBusy(null);
    setSelected(new Set());
    if (!result.ok) return toast.error(result.message);
    const sent = result.data.filter((item) => item.status === "Sent" || item.status === "DemoSent").length;
    const stopped = result.data.length - sent;
    toast(sent > 0 ? `${sent} sent` : "Nothing was sent", { description: stopped > 0 ? `${stopped} could not go. The reason is on each row.` : undefined });
  };

  const saveEdit = async (lead: SalesLeadView) => {
    setBusy(lead.latestOutreach!.draftId);
    const result = await editOutreach(lead.leadId, lead.latestOutreach!.draftId, text);
    setBusy(null);
    if (result.ok) setEditing(null);
    else toast.error(result.message);
  };

  return (
    <div className="space-y-4">
      <KpiRow
        items={[
          { label: "Waiting for review", value: String(waiting.length), helper: "Drafted or edited" },
          { label: "Sent", value: String(count("Sent")), helper: "On WhatsApp" },
          { label: "Demo sends", value: String(count("DemoSent")), helper: "Not delivered to a phone" },
          { label: "Could not send", value: String(count("Blocked")), helper: "Reason on each row" },
        ]}
      />
      <TableCard
        title="Messages"
        subtitle="Drafted by the outreach agent from what is recorded about each person, including follow-ups for quiet clients and policies ending. You read, change and approve; nothing goes out on its own."
        actions={
          <>
            <button type="button" onClick={() => void draftAll()} disabled={busy !== null || leads.length === 0} className="inline-flex h-8 items-center gap-1.5 rounded-md border border-slate-200 bg-white px-3 text-[12px] font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-50">
              {busy === "draft" ? <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden /> : <Sparkles className="h-3.5 w-3.5 text-[#1769FF]" aria-hidden />}
              Draft messages
            </button>
            <button type="button" onClick={() => void followUps()} disabled={busy !== null || leads.length === 0} className="inline-flex h-8 items-center gap-1.5 rounded-md border border-slate-200 bg-white px-3 text-[12px] font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-50">
              {busy === "followups" ? <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden /> : <Clock className="h-3.5 w-3.5 text-[#1769FF]" aria-hidden />}
              Find follow-ups
            </button>
            <button type="button" onClick={() => setConfirming(true)} disabled={chosen.length === 0 || busy !== null} className="inline-flex h-8 items-center gap-1.5 rounded-md bg-[#1769FF] px-3 text-[12px] font-semibold text-white hover:bg-[#1258d6] disabled:cursor-not-allowed disabled:opacity-50">
              <Send className="h-3.5 w-3.5" aria-hidden />
              Send selected{chosen.length > 0 ? ` (${chosen.length})` : ""}
            </button>
          </>
        }
      >
        {confirming ? (
          <div role="alertdialog" aria-label="Confirm sending" className="flex flex-wrap items-center justify-between gap-3 border-b border-amber-100 bg-amber-50 px-5 py-3">
            <p className="m-0 max-w-2xl text-[12.5px] text-amber-900">
              Send {chosen.length} {chosen.length === 1 ? "message" : "messages"} exactly as written? Each one is checked first for agreement to be contacted, sending hours (07:00 to 20:00) and WhatsApp&apos;s rules. Any that fail are kept with the reason.
            </p>
            <div className="flex gap-2">
              <button type="button" onClick={() => setConfirming(false)} className="h-8 rounded-md px-3 text-[12px] font-medium text-slate-600 hover:bg-white">Cancel</button>
              <button type="button" onClick={() => void sendChosen()} className="h-8 rounded-md bg-[#1769FF] px-3 text-[12px] font-semibold text-white hover:bg-[#1258d6]">Approve and send</button>
            </div>
          </div>
        ) : null}
        <ReportState
          loading={loading}
          error={error}
          forbidden={forbidden}
          onRetry={() => void reload()}
          permission="Manage sales leads"
          empty={rows.length === 0 ? { title: "No messages drafted yet", message: leads.length === 0 ? "Prospects appear when someone asks Kato about a cover, or when a bank list is imported." : "Choose Draft messages to write a personal introduction for each person who has a recommended cover and has not declined contact." } : null}
        >
          <div className="overflow-x-auto">
            <table className="w-full min-w-[560px] text-left">
              <thead>
                <tr className="border-b border-slate-100 text-[11px] font-semibold uppercase tracking-wide text-slate-400">
                  <th className="w-10 px-5 py-3 font-semibold"><span className="sr-only">Select</span></th>
                  <th className={th}>Customer</th>
                  <th className={th}>Message</th>
                  <th className={th}>Status</th>
                  <th className={`${th} pr-4`}>Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-50">
                {rows.map((lead) => {
                  const draft = lead.latestOutreach!;
                  const open = WAITING.has(draft.status);
                  return (
                    <Fragment key={lead.leadId}>
                      <tr className="align-top hover:bg-slate-50/70">
                        <td className="px-5 py-3.5">
                          <input type="checkbox" disabled={!open} checked={selected.has(draft.draftId)} onChange={() => toggle(draft.draftId)} aria-label={`Select the message for ${lead.customerName}`} className="h-3.5 w-3.5 accent-[#1769FF]" />
                        </td>
                        <td className="min-w-[8rem] px-3 py-3.5">
                          <div className="text-[13px] font-semibold text-[#0B1640]">{lead.customerName}</div>
                          <div className="mt-0.5 text-[11px] text-slate-500">{draft.product?.replace(/^Nile Shield |^Mpingo |^Kifaru /, "") ?? "Cover not named"}</div>
                        </td>
                        <td className="max-w-[26rem] px-3 py-3.5 text-[12px] leading-snug text-slate-600">
                          <p className="m-0 line-clamp-3">{draft.text}</p>
                          {draft.status === "Blocked" && draft.blockedReason ? <p className="m-0 mt-1 text-[11.5px] font-medium text-red-700">{draft.blockedReason}</p> : null}
                        </td>
                        <td className="whitespace-nowrap px-3 py-3.5"><CipStatusPill tone={statusTone(draft.status)} dot>{draft.statusLabel}</CipStatusPill></td>
                        <td className="px-3 py-3.5 pr-4">
                          {open || draft.status === "Blocked" ? (
                            <div className="flex flex-wrap items-center gap-1.5">
                              <button type="button" onClick={() => { setEditing(draft.draftId); setText(draft.text); }} className="inline-flex h-8 items-center gap-1 rounded-lg border border-slate-200 bg-white px-2.5 text-[12px] font-semibold text-slate-700 hover:bg-slate-50">
                                <PenLine className="h-3.5 w-3.5" aria-hidden />
                                Edit
                              </button>
                              <button type="button" onClick={() => void sendOne(lead)} disabled={busy !== null} className="inline-flex h-8 items-center gap-1 rounded-lg bg-[#1769FF] px-2.5 text-[12px] font-semibold text-white hover:bg-[#1258d6] disabled:opacity-50">
                                {busy === draft.draftId ? <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden /> : null}
                                Approve and send
                              </button>
                              <button type="button" onClick={() => void dismissOutreach(lead.leadId, draft.draftId)} className="inline-flex h-8 items-center rounded-lg px-2 text-[12px] font-medium text-slate-500 hover:bg-slate-50">Dismiss</button>
                            </div>
                          ) : (
                            <span className="text-[12px] text-slate-400">No action needed</span>
                          )}
                        </td>
                      </tr>
                      {editing === draft.draftId ? (
                        <tr className="bg-[#F7F9FC]">
                          <td />
                          <td colSpan={4} className="px-3 py-3">
                            <label className="block">
                              <span className="mb-1 block text-[10px] font-semibold uppercase tracking-wide text-slate-400">Message to {lead.customerName.split(" ")[0]}</span>
                              <textarea value={text} onChange={(event) => setText(event.target.value)} rows={5} maxLength={1000} className="w-full rounded-lg border border-slate-200 bg-white p-2.5 text-[13px] text-[#0B1640] outline-none focus:border-[#1769FF]/50" />
                            </label>
                            <div className="mt-2 flex items-center justify-between">
                              <span className="text-[11px] text-slate-400">{text.length} of 1,000 characters. The opt-out line should stay.</span>
                              <div className="flex gap-2">
                                <button type="button" onClick={() => setEditing(null)} className="h-8 rounded-md px-3 text-[12px] font-medium text-slate-600 hover:bg-white">Cancel</button>
                                <button type="button" onClick={() => void saveEdit(lead)} disabled={!text.trim() || busy !== null} className="h-8 rounded-md bg-[#1769FF] px-3 text-[12px] font-semibold text-white hover:bg-[#1258d6] disabled:opacity-50">Save wording</button>
                              </div>
                            </div>
                          </td>
                        </tr>
                      ) : null}
                    </Fragment>
                  );
                })}
              </tbody>
            </table>
          </div>
        </ReportState>
      </TableCard>

      <WhatsAppTemplates />
    </div>
  );
}

// WhatsApp only lets a business start a conversation with a message template that Meta has approved.
// IIP is not connected to Meta's template service yet, so each template below is shown as what it is:
// proposed wording, not submitted. People who wrote in the last 24 hours can be answered without one.
const PROPOSED_TEMPLATES = [
  { name: "cover_introduction", use: "First message to a bank customer about a cover that suits them", body: "Hello {{1}}, this is {{2}}'s insurance team. {{3}} from {{4}} may suit you. Would you like me to explain how it works? Reply STOP at any time and we will not message you again." },
  { name: "quiet_follow_up", use: "A gentle nudge after a few quiet days", body: "Hello {{1}}, this is {{2}}'s insurance team. We spoke recently about {{3}}. Would you like to carry on? Reply STOP at any time and we will not message you again." },
  { name: "policy_renewal", use: "A policy is about to end", body: "Hello {{1}}, your {{2}} cover runs until {{3}}. Would you like to renew it? Reply STOP at any time and we will not message you again." },
];

function WhatsAppTemplates() {
  return (
    <TableCard
      title="WhatsApp templates"
      subtitle="Needed to message someone who has not written in the last 24 hours."
      actions={<CipStatusPill tone="neutral">Not connected to Meta</CipStatusPill>}
    >
      <div className="space-y-3 p-4 sm:p-5">
        <p className="m-0 max-w-3xl text-[12.5px] leading-relaxed text-slate-600">
          A business can start a WhatsApp conversation only with a template that Meta has approved. None has been submitted from IIP yet, so messages to people who have not written in recently are held with the reason shown. People who wrote in the last 24 hours can be answered with an ordinary message.
        </p>
        <ul className="m-0 grid list-none gap-3 p-0 md:grid-cols-3">
          {PROPOSED_TEMPLATES.map((template) => (
            <li key={template.name} className="rounded-xl border border-slate-200 bg-white p-3.5">
              <div className="flex items-start justify-between gap-2">
                <span className="font-mono text-[12px] font-semibold text-[#0B1640]">{template.name}</span>
                <CipStatusPill tone="warning">Not submitted</CipStatusPill>
              </div>
              <p className="m-0 mt-1 text-[11.5px] text-slate-500">{template.use}</p>
              <p className="m-0 mt-2 rounded-lg bg-[#F7F9FC] p-2.5 text-[12px] leading-snug text-slate-700">{template.body}</p>
            </li>
          ))}
        </ul>
        <p className="m-0 text-[11.5px] text-slate-500">Proposed wording only. Meta decides whether each template is approved, and that decision cannot be made from here.</p>
      </div>
    </TableCard>
  );
}
