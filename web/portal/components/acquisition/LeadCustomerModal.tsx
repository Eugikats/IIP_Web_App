"use client";

import Link from "next/link";
import { useCallback, useEffect, useId, useRef, useState } from "react";
import { AnimatePresence, motion, useReducedMotion } from "framer-motion";
import { Check, ChevronDown, FileText, Loader2, MessageCircle, RefreshCw, ShieldCheck, Sparkles, UserPlus, Users } from "lucide-react";
import { toast } from "sonner";
import { Modal } from "@/components/ui/Modal";
import { Button } from "@/components/ui/Button";
import { StatusPill, type PillTone } from "@/components/ui/StatusPill";
import { Avatar } from "@/components/ui/Avatar";
import { LoadingSkeleton, ErrorStateSurface } from "@/components/ui/StateSurfaces";
import { usePeople } from "@/lib/people";
import { formatAgeSince, formatDateTime, humanizeIdentifier } from "@/lib/format";
import { cn } from "@/lib/utils";
import type { InsightPoint, LeadResult, SalesLeadDetail } from "@/lib/lovable-parity/data/salesLeads";

// The Lead Customer record, opened from the prospect queue. Everything shown is recorded or derived
// by the API; the insights tab is prepared by the Conversation Insights Agent and every point in it
// cites the customer message it came from.

type Tab = "customer" | "insights" | "interests" | "notes" | "activity";

const TABS: { key: Tab; label: string }[] = [
  { key: "customer", label: "Customer" },
  { key: "insights", label: "Insights" },
  { key: "interests", label: "Interests" },
  { key: "notes", label: "Notes" },
  { key: "activity", label: "Activity" },
];

export function stageTone(stage: string): PillTone {
  switch (stage) {
    case "ReadyToApply":
    case "Applied":
      return "success";
    case "WithAPerson":
      return "warning";
    case "Interested":
    case "Considering":
    case "GettingToKnow":
      return "info";
    default:
      return "neutral";
  }
}

export function relationshipTone(relationship: string): PillTone {
  return relationship === "ExistingCustomer" ? "success" : relationship === "Registered" ? "info" : "neutral";
}

export function initialsOf(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  return (parts.length === 0 ? "?" : parts.slice(0, 2).map((part) => part[0]!.toUpperCase()).join(""));
}

export function LeadCustomerModal({
  leadId,
  onClose,
  load,
  addNote,
  assign,
  refreshInsights,
}: {
  leadId: string | null;
  onClose: () => void;
  load: (leadId: string) => Promise<LeadResult>;
  addNote: (leadId: string, text: string) => Promise<LeadResult>;
  assign: (leadId: string, userId: string | null) => Promise<LeadResult>;
  refreshInsights: (leadId: string) => Promise<LeadResult>;
}) {
  const [detail, setDetail] = useState<SalesLeadDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [tab, setTab] = useState<Tab>("customer");

  useEffect(() => {
    if (!leadId) return;
    let active = true;
    setDetail(null);
    setError(null);
    setTab("customer");
    void load(leadId).then((result) => {
      if (!active) return;
      if (result.ok) setDetail(result.detail);
      else setError(result.message);
    });
    return () => {
      active = false;
    };
  }, [leadId, load]);

  return (
    <Modal open={leadId !== null} title="Lead customer" onClose={onClose}>
      <div className="max-h-[calc(100vh-9rem)] overflow-y-auto px-4 py-5 sm:px-6">
        {error ? (
          <ErrorStateSurface title="This prospect could not be opened" message={error} />
        ) : !detail ? (
          <LoadingSkeleton rows={6} />
        ) : (
          <LeadBody detail={detail} setDetail={setDetail} tab={tab} setTab={setTab} addNote={addNote} assign={assign} refreshInsights={refreshInsights} />
        )}
      </div>
    </Modal>
  );
}

function LeadBody({
  detail,
  setDetail,
  tab,
  setTab,
  addNote,
  assign,
  refreshInsights,
}: {
  detail: SalesLeadDetail;
  setDetail: (detail: SalesLeadDetail) => void;
  tab: Tab;
  setTab: (tab: Tab) => void;
  addNote: (leadId: string, text: string) => Promise<LeadResult>;
  assign: (leadId: string, userId: string | null) => Promise<LeadResult>;
  refreshInsights: (leadId: string) => Promise<LeadResult>;
}) {
  const reduceMotion = useReducedMotion();
  const conversationHref = detail.conversationId ? `/app/channels?thread=${encodeURIComponent(detail.conversationId)}` : null;

  return (
    <>
      <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div className="flex min-w-0 items-center gap-4">
          <span className="inline-flex h-16 w-16 shrink-0 items-center justify-center rounded-full bg-dls-primary text-[22px] font-bold text-dls-text-inverse sm:h-20 sm:w-20 sm:text-[26px]" aria-hidden="true">{initialsOf(detail.customerName)}</span>
          <div className="min-w-0">
            <h3 className="m-0 truncate text-[22px] font-bold tracking-[-0.3px] text-dls-text-primary">{detail.customerName}</h3>
            <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-[13px] text-dls-text-secondary">
              <span className="tabular-nums">{detail.maskedContact}</span>
              {detail.primaryChannel ? (
                <span className="inline-flex items-center gap-1">
                  {detail.primaryChannel === "WhatsApp" ? <MessageCircle className="h-3.5 w-3.5 text-dls-text-muted" aria-hidden="true" /> : null}
                  {detail.primaryChannel}
                </span>
              ) : null}
              <span>Last seen {formatDateTime(detail.lastInteractionAt, "not recorded")}</span>
            </div>
          </div>
        </div>
        <div className="flex shrink-0 flex-wrap items-center gap-2">
          <AssignControl detail={detail} onAssigned={setDetail} assign={assign} />
          {conversationHref ? (
            <Link
              href={conversationHref}
              className="dls-focus-outline inline-flex min-h-10 items-center gap-1.5 rounded-dls-control bg-dls-primary px-3.5 text-[13px] font-semibold text-dls-text-inverse hover:bg-dls-primary-hover"
            >
              <MessageCircle className="h-4 w-4" aria-hidden="true" />
              Open conversation
            </Link>
          ) : null}
        </div>
      </header>

      <div className="mt-5 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <StatTile icon={Users} label="Relationship" value={detail.relationshipLabel} />
        <StatTile icon={Sparkles} label="Stage" value={detail.stageLabel} />
        <StatTile icon={ShieldCheck} label="Agreed to follow-up" value={detail.consent.statusLabel} />
        <StatTile icon={FileText} label="Product interest" value={detail.productInterest ?? "Not yet known"} />
      </div>

      <nav className="mt-6 flex gap-1 overflow-x-auto border-b border-dls-border-subtle" aria-label="Prospect sections">
        {TABS.map((item) => (
          <button
            key={item.key}
            type="button"
            onClick={() => setTab(item.key)}
            aria-current={tab === item.key ? "page" : undefined}
            className={cn(
              "dls-focus-outline relative shrink-0 px-3 pb-3 pt-1 text-[14px] font-semibold transition-colors duration-dls-fast",
              tab === item.key ? "text-dls-primary-accent" : "text-dls-text-secondary hover:text-dls-text-primary",
            )}
          >
            {item.label}
            {item.key === "insights" && detail.insightsStale ? <span className="ml-1.5 inline-block h-1.5 w-1.5 rounded-full bg-dls-warning-dot align-middle" aria-label="has new messages" /> : null}
            {tab === item.key ? (
              <motion.span layoutId="lead-tab-underline" className="absolute inset-x-0 -bottom-px h-0.5 bg-dls-primary" transition={{ duration: reduceMotion ? 0 : 0.2 }} />
            ) : null}
          </button>
        ))}
      </nav>

      <AnimatePresence mode="wait">
        <motion.section
          key={tab}
          className="mt-5"
          initial={{ opacity: 0, y: reduceMotion ? 0 : 4 }}
          animate={{ opacity: 1, y: 0 }}
          exit={{ opacity: 0 }}
          transition={{ duration: reduceMotion ? 0 : 0.15 }}
        >
          {tab === "customer" ? <CustomerTab detail={detail} /> : null}
          {tab === "insights" ? <InsightsTab detail={detail} setDetail={setDetail} refreshInsights={refreshInsights} conversationHref={conversationHref} /> : null}
          {tab === "interests" ? <InterestsTab detail={detail} /> : null}
          {tab === "notes" ? <NotesTab detail={detail} setDetail={setDetail} addNote={addNote} /> : null}
          {tab === "activity" ? <ActivityTab detail={detail} /> : null}
        </motion.section>
      </AnimatePresence>
    </>
  );
}

function StatTile({ icon: Icon, label, value }: { icon: typeof Users; label: string; value: string }) {
  return (
    <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4">
      <span className="inline-flex h-9 w-9 items-center justify-center rounded-full bg-dls-primary-soft text-dls-primary-accent">
        <Icon className="h-4 w-4" aria-hidden="true" />
      </span>
      <div className="mt-3 text-[11px] font-semibold uppercase tracking-[0.08em] text-dls-text-muted">{label}</div>
      <div className="mt-1 text-[17px] font-bold text-dls-text-primary">{value}</div>
    </div>
  );
}

function Card({ title, icon: Icon, children, aside }: { title: string; icon?: typeof Users; children: React.ReactNode; aside?: React.ReactNode }) {
  return (
    <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-5">
      <div className="mb-3 flex items-center justify-between gap-3">
        <h4 className="m-0 inline-flex items-center gap-2 text-[15px] font-bold text-dls-text-primary">
          {Icon ? <Icon className="h-4 w-4 text-dls-primary-accent" aria-hidden="true" /> : null}
          {title}
        </h4>
        {aside}
      </div>
      {children}
    </div>
  );
}

function Row({ label, value, note }: { label: string; value: React.ReactNode; note?: string }) {
  return (
    <div className="grid grid-cols-1 gap-1 border-b border-dls-border-subtle py-3 last:border-b-0 sm:grid-cols-[minmax(0,2fr)_minmax(0,3fr)] sm:gap-4">
      <div className="text-[13px] text-dls-text-secondary">{label}</div>
      <div className="min-w-0 text-[14px] text-dls-text-primary">
        {value}
        {note ? <div className="mt-0.5 text-[12px] text-dls-text-muted">{note}</div> : null}
      </div>
    </div>
  );
}

function CustomerTab({ detail }: { detail: SalesLeadDetail }) {
  return (
    <div className="space-y-4">
      <Card title="Customer profile" icon={Users}>
        <Row label="Full name" value={detail.customerName} />
        <Row label="Contact" value={<span className="tabular-nums">{detail.maskedContact}</span>} />
        <Row label="Channel" value={detail.primaryChannel ?? "Not recorded"} />
        <Row label="Bank customer" value={<StatusPill tone={detail.bankCustomer === true ? "success" : "neutral"} label={detail.bankCustomerLabel} />} />
        <Row label="Relationship" value={<StatusPill tone={relationshipTone(detail.relationship)} label={detail.relationshipLabel} />} />
        {detail.facts.map((fact) => (
          <Row key={fact.key} label={fact.label} value={fact.value} note={`${fact.source}, ${formatDateTime(fact.recordedAt)}`} />
        ))}
        <Row label="Became a prospect" value={formatDateTime(detail.createdAt)} note={detail.leadSource} />
      </Card>

      <Card title="Still unknown">
        {detail.missing.length === 0 ? (
          <p className="m-0 text-[13.5px] text-dls-text-secondary">Everything Kato asks about has been answered.</p>
        ) : (
          <ul className="m-0 flex flex-wrap gap-2 p-0">
            {detail.missing.map((item) => (
              <li key={item} className="list-none rounded-dls-pill border border-dls-border-pill px-2.5 py-1 text-[12.5px] text-dls-text-secondary">{item}</li>
            ))}
          </ul>
        )}
      </Card>

      {detail.policies.length > 0 ? (
        <Card title="Cover held with the insurer" icon={ShieldCheck}>
          {detail.policies.map((policy) => (
            <Row key={policy.policyNumber} label={policy.productLine} value={policy.policyNumber} note={policy.effectiveTo ? `Runs to ${policy.effectiveTo}` : undefined} />
          ))}
        </Card>
      ) : null}
    </div>
  );
}

function InsightsTab({
  detail,
  setDetail,
  refreshInsights,
  conversationHref,
}: {
  detail: SalesLeadDetail;
  setDetail: (detail: SalesLeadDetail) => void;
  refreshInsights: (leadId: string) => Promise<LeadResult>;
  conversationHref: string | null;
}) {
  const [refreshing, setRefreshing] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const insights = detail.insights;

  const refresh = useCallback(async () => {
    setRefreshing(true);
    setProblem(null);
    const result = await refreshInsights(detail.leadId);
    setRefreshing(false);
    if (result.ok) setDetail(result.detail);
    else setProblem(result.message);
  }, [detail.leadId, refreshInsights, setDetail]);

  if (!detail.hasConversation) {
    return (
      <Card title="Conversation insights" icon={Sparkles}>
        <p className="m-0 text-[13.5px] text-dls-text-secondary">This prospect has no WhatsApp conversation, so there is nothing for the Conversation Insights Agent to read.</p>
      </Card>
    );
  }

  const refreshButton = (
    <Button variant="secondary" onClick={() => void refresh()} disabled={refreshing} icon={refreshing ? <Loader2 className="animate-spin" aria-hidden="true" /> : <RefreshCw aria-hidden="true" />}>
      {refreshing ? "Reading the conversation" : "Refresh"}
    </Button>
  );

  return (
    <div className="space-y-4">
      <div className="rounded-dls-card border border-ai-border bg-dls-surface p-5">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <div className="inline-flex items-center gap-2 text-[15px] font-bold text-dls-text-primary">
              <Sparkles className="h-4 w-4 text-dls-primary-accent" aria-hidden="true" />
              Prepared by the Conversation Insights Agent
            </div>
            <p className="m-0 mt-1 text-[12.5px] text-dls-text-muted">
              {insights
                ? `Updated ${formatAgeSince(insights.generatedAt)} ago, from ${insights.messageCount} customer ${insights.messageCount === 1 ? "message" : "messages"}. Each point cites the message it came from.`
                : "Not prepared yet. It runs on its own a little after the customer's last message."}
            </p>
          </div>
          {refreshButton}
        </div>
        {detail.insightsStale && insights ? (
          <p className="m-0 mt-3 inline-flex items-center gap-2 text-[13px] text-dls-text-secondary">
            <span className="h-1.5 w-1.5 rounded-full bg-dls-warning-dot" aria-hidden="true" />
            The conversation has moved on since these notes were prepared.
          </p>
        ) : null}
        {problem ? <p className="m-0 mt-3 text-[13px] text-dls-danger-text" role="alert">{problem}</p> : null}

        {!insights ? null : !insights.enoughConversation ? (
          <p className="m-0 mt-4 text-[13.5px] text-dls-text-secondary">Not enough conversation yet. Notes are prepared once the customer has written at least twice.</p>
        ) : (
          <div className="mt-4 space-y-5">
            {insights.summary ? <PointText point={insights.summary} insights={insights} conversationHref={conversationHref} lead /> : <p className="m-0 text-[13.5px] text-dls-text-secondary">No summary could be supported by the messages.</p>}
            <PointList title="Problems and worries they described" points={insights.problems} insights={insights} conversationHref={conversationHref} empty="None described." />
            <PointList title="Doubts and objections" points={insights.objections} insights={insights} conversationHref={conversationHref} empty="None raised." />
            {insights.interests.length > 0 ? (
              <div>
                <div className="text-[11px] font-semibold uppercase tracking-[0.08em] text-dls-text-muted">Covers they are interested in</div>
                <ul className="m-0 mt-2 space-y-2 p-0">
                  {insights.interests.map((interest) => (
                    <li key={interest.product} className="list-none text-[14px] text-dls-text-primary">
                      <span className="font-semibold">{interest.product}</span>
                      {interest.text ? <span className="text-dls-text-secondary">: {interest.text}</span> : null}{" "}
                      <Citations refs={interest.refs} insights={insights} conversationHref={conversationHref} />
                    </li>
                  ))}
                </ul>
              </div>
            ) : null}
            {insights.tone ? (
              <div className="text-[13.5px] text-dls-text-secondary">
                <span className="text-[11px] font-semibold uppercase tracking-[0.08em] text-dls-text-muted">Tone</span>
                <span className="ml-2 text-dls-text-primary">{insights.tone}</span>
              </div>
            ) : null}
          </div>
        )}
      </div>

      {insights?.nextStep ? (
        <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-5">
          <div className="text-[11px] font-semibold uppercase tracking-[0.08em] text-dls-text-muted">Suggested next step for you</div>
          <div className="mt-2">
            <PointText point={insights.nextStep} insights={insights} conversationHref={conversationHref} />
          </div>
          <p className="m-0 mt-2 text-[12px] text-dls-text-muted">A suggestion only. You decide what happens next.</p>
        </div>
      ) : null}
    </div>
  );
}

export function PointList({
  title,
  points,
  insights,
  conversationHref,
  empty,
}: {
  title: string;
  points: InsightPoint[];
  insights: NonNullable<SalesLeadDetail["insights"]>;
  conversationHref: string | null;
  empty: string;
}) {
  return (
    <div>
      <div className="text-[11px] font-semibold uppercase tracking-[0.08em] text-dls-text-muted">{title}</div>
      {points.length === 0 ? (
        <p className="m-0 mt-2 text-[13.5px] text-dls-text-secondary">{empty}</p>
      ) : (
        <ul className="m-0 mt-2 space-y-2 p-0">
          {points.map((point, index) => (
            <li key={index} className="list-none">
              <PointText point={point} insights={insights} conversationHref={conversationHref} />
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

export function PointText({
  point,
  insights,
  conversationHref,
  lead,
}: {
  point: InsightPoint;
  insights: NonNullable<SalesLeadDetail["insights"]>;
  conversationHref: string | null;
  lead?: boolean;
}) {
  return (
    <p className={cn("m-0 text-dls-text-primary", lead ? "text-[15px] leading-[1.55]" : "text-[14px] leading-[1.5]")}>
      {point.text} <Citations refs={point.refs} insights={insights} conversationHref={conversationHref} />
    </p>
  );
}

/** Each cited message opens in place, with its time and the words the customer used, and from there
 *  in the conversation itself. */
export function Citations({ refs, insights, conversationHref }: { refs: string[]; insights: NonNullable<SalesLeadDetail["insights"]>; conversationHref: string | null }) {
  const [open, setOpen] = useState<string | null>(null);
  const source = open ? insights.sources.find((item) => item.tag === open) : null;
  return (
    <span className="inline">
      {refs.map((tag) => (
        <button
          key={tag}
          type="button"
          onClick={() => setOpen(open === tag ? null : tag)}
          aria-expanded={open === tag}
          aria-label={`Customer message ${tag.slice(1)}`}
          className={cn(
            "dls-focus-outline mx-0.5 inline-flex items-center rounded-dls-chip border px-1.5 text-[11px] font-semibold align-[1px]",
            open === tag ? "border-dls-primary-border bg-dls-primary-soft-alt text-dls-primary-accent" : "border-dls-border-pill text-dls-text-secondary hover:border-dls-primary",
          )}
        >
          {tag}
        </button>
      ))}
      {source ? (
        <span className="mt-2 block rounded-dls-control border border-dls-border-subtle bg-dls-surface-sunken px-3 py-2 text-[13px] text-dls-text-secondary">
          <span className="block text-[11.5px] text-dls-text-muted">Customer, {formatDateTime(source.at)}</span>
          <span className="block text-dls-text-primary">&ldquo;{source.excerpt}&rdquo;</span>
          {conversationHref ? (
            <Link href={conversationHref} className="dls-focus-outline mt-1 inline-block text-[12.5px] font-semibold text-dls-primary-accent hover:underline">
              Open in the conversation
            </Link>
          ) : null}
        </span>
      ) : null}
    </span>
  );
}

function InterestsTab({ detail }: { detail: SalesLeadDetail }) {
  return (
    <Card title="Covers they are interested in" icon={FileText}>
      {detail.appliedFor ? <Row label="Applied for" value={detail.appliedFor} /> : null}
      {detail.interests.length === 0 && !detail.appliedFor ? (
        <p className="m-0 text-[13.5px] text-dls-text-secondary">No interest in a particular cover recorded yet.</p>
      ) : (
        detail.interests.map((interest) => (
          <Row
            key={interest.product}
            label={interest.product}
            value={<StatusPill tone={interest.strength === "NotNow" ? "neutral" : interest.strength === "WantsIt" ? "success" : "info"} label={interest.strengthLabel} />}
            note={interest.reason || undefined}
          />
        ))
      )}
    </Card>
  );
}

function NotesTab({
  detail,
  setDetail,
  addNote,
}: {
  detail: SalesLeadDetail;
  setDetail: (detail: SalesLeadDetail) => void;
  addNote: (leadId: string, text: string) => Promise<LeadResult>;
}) {
  const [text, setText] = useState("");
  const [saving, setSaving] = useState(false);

  async function save() {
    setSaving(true);
    const result = await addNote(detail.leadId, text);
    setSaving(false);
    if (result.ok) {
      setDetail(result.detail);
      setText("");
      toast.success("Note saved");
    } else {
      toast.error(result.message);
    }
  }

  return (
    <div className="space-y-4">
      <Card title="Add a note">
        <label className="block">
          <span className="sr-only">Note</span>
          <textarea
            value={text}
            onChange={(event) => setText(event.target.value)}
            rows={3}
            maxLength={2000}
            placeholder="Only your colleagues see notes. The customer never does."
            className="dls-focus-outline block w-full resize-y rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-2 text-[14px] text-dls-text-primary placeholder:text-dls-text-faint"
          />
        </label>
        <div className="mt-3 flex justify-end">
          <Button variant="primary" onClick={() => void save()} disabled={saving || text.trim().length === 0} icon={saving ? <Loader2 className="animate-spin" aria-hidden="true" /> : undefined}>
            Save note
          </Button>
        </div>
      </Card>
      <Card title="Notes">
        {detail.notes.length === 0 ? (
          <p className="m-0 text-[13.5px] text-dls-text-secondary">No notes yet.</p>
        ) : (
          <ul className="m-0 space-y-3 p-0">
            {detail.notes.map((note) => (
              <li key={note.noteId} className="list-none border-b border-dls-border-subtle pb-3 last:border-b-0 last:pb-0">
                <div className="text-[12px] text-dls-text-muted">{note.authorName}, {formatDateTime(note.at)}</div>
                <p className="m-0 mt-1 whitespace-pre-wrap text-[14px] text-dls-text-primary">{note.text}</p>
              </li>
            ))}
          </ul>
        )}
      </Card>
    </div>
  );
}

function ActivityTab({ detail }: { detail: SalesLeadDetail }) {
  return (
    <Card title="Activity">
      {detail.activity.length === 0 ? (
        <p className="m-0 text-[13.5px] text-dls-text-secondary">Nothing recorded yet.</p>
      ) : (
        <ol className="m-0 space-y-3 p-0">
          {detail.activity.map((entry, index) => (
            <li key={`${entry.at}-${index}`} className="flex list-none gap-3">
              <span className="mt-1.5 h-1.5 w-1.5 shrink-0 rounded-full bg-dls-text-faint" aria-hidden="true" />
              <div>
                <div className="text-[14px] text-dls-text-primary">{entry.text}</div>
                <div className="text-[12px] text-dls-text-muted">{entry.actor}, {formatDateTime(entry.at)}</div>
              </div>
            </li>
          ))}
        </ol>
      )}
    </Card>
  );
}

export function AssignControl({
  detail,
  onAssigned,
  assign,
}: {
  detail: SalesLeadDetail;
  onAssigned: (detail: SalesLeadDetail) => void;
  assign: (leadId: string, userId: string | null) => Promise<LeadResult>;
}) {
  const { people, loading } = usePeople();
  const [saving, setSaving] = useState(false);
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);
  const rootRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const listId = useId();

  const staff = people.filter((person) => !person.userId.toLowerCase().startsWith("system:"));
  // The unassigned choice first, then the people; the current owner is kept even when the directory
  // has not listed them, so the picker never hides who owns the lead.
  const options: { userId: string | null; name: string; role: string | null }[] = [
    { userId: null, name: "Unassigned", role: null },
    ...(detail.assignedSalesOfficer && !staff.some((person) => person.userId === detail.assignedSalesOfficer)
      ? [{ userId: detail.assignedSalesOfficer, name: detail.assignedOfficerName ?? "Current owner", role: null }]
      : []),
    ...staff.map((person) => ({ userId: person.userId, name: person.name, role: humanizeIdentifier(person.role) })),
  ];
  const selectedIndex = Math.max(0, options.findIndex((option) => option.userId === (detail.assignedSalesOfficer ?? null)));
  const ownerName = detail.assignedOfficerName ?? "Unassigned";

  useEffect(() => {
    if (!open) return;
    setActive(selectedIndex);
    function onPointerDown(event: PointerEvent) {
      if (rootRef.current && !rootRef.current.contains(event.target as Node)) setOpen(false);
    }
    document.addEventListener("pointerdown", onPointerDown);
    return () => document.removeEventListener("pointerdown", onPointerDown);
  }, [open, selectedIndex]);

  async function choose(index: number) {
    const option = options[index];
    setOpen(false);
    buttonRef.current?.focus();
    if (!option || option.userId === (detail.assignedSalesOfficer ?? null)) return;
    setSaving(true);
    const result = await assign(detail.leadId, option.userId);
    setSaving(false);
    if (result.ok) {
      onAssigned(result.detail);
      toast.success(option.userId === null ? "Unassigned" : `Assigned to ${option.name}`);
    } else {
      toast.error(result.message);
    }
  }

  function onKeyDown(event: React.KeyboardEvent) {
    if (!open) {
      if (event.key === "ArrowDown" || event.key === "ArrowUp") {
        event.preventDefault();
        setOpen(true);
      }
      return;
    }
    if (event.key === "Escape") {
      // The modal closes on Escape too; this one only closes the list.
      event.preventDefault();
      event.stopPropagation();
      setOpen(false);
    } else if (event.key === "ArrowDown") {
      event.preventDefault();
      setActive((index) => Math.min(options.length - 1, index + 1));
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      setActive((index) => Math.max(0, index - 1));
    } else if (event.key === "Home") {
      event.preventDefault();
      setActive(0);
    } else if (event.key === "End") {
      event.preventDefault();
      setActive(options.length - 1);
    } else if (event.key === "Enter") {
      event.preventDefault();
      void choose(active);
    }
  }

  return (
    <div ref={rootRef} className="relative" onKeyDown={onKeyDown}>
      <button
        ref={buttonRef}
        type="button"
        onClick={() => setOpen((value) => !value)}
        disabled={saving || loading}
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={open ? listId : undefined}
        aria-label={`Assigned officer: ${ownerName}. Change`}
        className="dls-focus-outline inline-flex min-h-10 min-w-[11rem] items-center gap-2 rounded-dls-control border border-dls-border-input bg-dls-surface py-1 pl-2 pr-2.5 text-[13px] font-semibold text-dls-text-primary hover:bg-dls-bg-page disabled:opacity-70"
      >
        {saving ? (
          <Loader2 className="h-4 w-4 animate-spin text-dls-text-muted" aria-hidden="true" />
        ) : detail.assignedSalesOfficer ? (
          <Avatar id={detail.assignedSalesOfficer} initials={initialsOf(ownerName)} size={26} />
        ) : (
          <span className="inline-flex h-[26px] w-[26px] items-center justify-center rounded-full border border-dashed border-dls-border-strong text-dls-text-muted">
            <UserPlus className="h-3.5 w-3.5" aria-hidden="true" />
          </span>
        )}
        <span className="min-w-0 flex-1 truncate text-left">{loading ? "Loading people" : ownerName}</span>
        <ChevronDown className={cn("h-4 w-4 shrink-0 text-dls-text-muted transition-transform duration-dls-fast", open && "rotate-180")} aria-hidden="true" />
      </button>

      {open ? (
        <ul
          id={listId}
          role="listbox"
          aria-label="Assign to"
          aria-activedescendant={`${listId}-${active}`}
          tabIndex={-1}
          ref={(node) => node?.focus({ preventScroll: true })}
          className="absolute right-0 z-20 mt-1.5 max-h-72 w-72 max-w-[calc(100vw-3rem)] overflow-y-auto rounded-dls-overlay border border-dls-border-subtle bg-dls-surface p-1.5 shadow-dls-popover outline-none"
        >
          <li className="list-none px-2.5 pb-1 pt-1.5 text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted" role="presentation">Assign to</li>
          {options.map((option, index) => {
            const selected = index === selectedIndex;
            return (
              <li
                key={option.userId ?? "unassigned"}
                id={`${listId}-${index}`}
                role="option"
                aria-selected={selected}
                onPointerEnter={() => setActive(index)}
                onClick={() => void choose(index)}
                className={cn(
                  "flex cursor-pointer list-none items-center gap-2.5 rounded-dls-control px-2.5 py-2",
                  index === active ? "bg-dls-bg-page" : "",
                )}
              >
                {option.userId ? (
                  <Avatar id={option.userId} initials={initialsOf(option.name)} size={28} />
                ) : (
                  <span className="inline-flex h-7 w-7 items-center justify-center rounded-full border border-dashed border-dls-border-strong text-dls-text-muted">
                    <UserPlus className="h-3.5 w-3.5" aria-hidden="true" />
                  </span>
                )}
                <span className="min-w-0 flex-1">
                  <span className={cn("block truncate text-[13.5px]", selected ? "font-bold text-dls-text-primary" : "font-semibold text-dls-text-primary")}>{option.name}</span>
                  {option.role ? <span className="block truncate text-[12px] text-dls-text-muted">{option.role}</span> : null}
                </span>
                {selected ? <Check className="h-4 w-4 shrink-0 text-dls-primary-accent" aria-hidden="true" /> : null}
              </li>
            );
          })}
        </ul>
      ) : null}
    </div>
  );
}
