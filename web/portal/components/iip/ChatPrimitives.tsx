import { Sparkles, ArrowUp, ShieldCheck, FileText, User } from "lucide-react";
import { cn } from "@/lib/utils";
import { Badge } from "./Primitives";
import type { ReactNode } from "react";

export function UserMessageBubble({ text, name = "You" }: { text: string; name?: string }) {
  return (
    <div className="flex items-start gap-3 animate-fade-in">
      <div className="h-8 w-8 rounded-full bg-gradient-secondary grid place-items-center text-white text-[11px] font-semibold shrink-0">
        <User className="h-4 w-4" />
      </div>
      <div className="flex-1 min-w-0">
        <div className="text-[11.5px] text-muted mb-1">{name}</div>
        <div className="text-[14px] leading-[22px] text-ink whitespace-pre-wrap">{text}</div>
      </div>
    </div>
  );
}

export function AIMessageBubble({ children, sources = [] }: { children: ReactNode; sources?: string[] }) {
  return (
    <div className="flex items-start gap-3 animate-fade-in">
      <div className="h-8 w-8 rounded-xl gradient-primary grid place-items-center shrink-0 shadow-l1">
        <Sparkles className="h-4 w-4 text-white" />
      </div>
      <div className="flex-1 min-w-0">
        <div className="text-[11.5px] text-violet mb-1 flex items-center gap-1.5">
          IIP Operations <span className="text-muted">· human review required for regulated decisions</span>
        </div>
        <div className="text-[14px] leading-[22px] text-ink-soft space-y-3">{children}</div>
        {sources.length > 0 && (
          <div className="mt-3 flex flex-wrap gap-1.5">
            {sources.map((s) => (
              <span key={s} className="inline-flex items-center gap-1 text-[11.5px] rounded-full bg-white/70 border border-line px-2 py-0.5 text-ink-soft">
                <FileText className="h-3 w-3 text-info" /> {s}
              </span>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

export function InsightCard({ title, items, tone = "info" }: { title: string; items: { label: string; meta?: string }[]; tone?: "info" | "violet" | "warning" }) {
  const toneMap = { info: "border-info/24", violet: "border-violet/24", warning: "border-warning/28" };
  return (
    <div className={cn("rounded-2xl border bg-white/70 p-4", toneMap[tone])}>
      <div className="text-[11px] uppercase tracking-[0.14em] text-muted mb-2">{title}</div>
      <div className="space-y-2">
        {items.map((it, i) => (
          <div key={i} className="flex items-center justify-between gap-3 rounded-xl border border-line/70 bg-white/70 px-3 py-2">
            <div className="text-[13px] text-ink truncate">{it.label}</div>
            {it.meta && <div className="text-[11.5px] text-muted shrink-0">{it.meta}</div>}
          </div>
        ))}
      </div>
    </div>
  );
}

export function ApprovalCard({ title, summary, onApprove, onReject, onEscalate }: {
  title: string; summary: string;
  onApprove?: () => void; onReject?: () => void; onEscalate?: () => void;
}) {
  return (
    <div className="rounded-2xl border border-violet/24 bg-violet/5 p-4">
      <div className="flex items-center gap-2 mb-1">
        <ShieldCheck className="h-3.5 w-3.5 text-violet" />
        <div className="text-[11px] uppercase tracking-[0.14em] text-violet">Human approval requested</div>
      </div>
      <div className="text-[14px] font-medium text-ink">{title}</div>
      <p className="text-[13px] text-ink-soft mt-1 leading-snug">{summary}</p>
      <div className="mt-3 flex flex-wrap gap-2">
        <button onClick={onApprove} className="text-[12.5px] rounded-xl gradient-primary text-white px-3 py-1.5">Approve step</button>
        <button onClick={onReject} className="text-[12.5px] rounded-xl border border-line bg-white text-ink px-3 py-1.5">Reject</button>
        <button onClick={onEscalate} className="text-[12.5px] rounded-xl border border-line bg-white text-ink px-3 py-1.5">Escalate</button>
      </div>
      <div className="mt-2 text-[10.5px] text-muted">Actions update workflow state only · they do not settle, deny, repudiate, pay, or accuse fraud.</div>
    </div>
  );
}

export function Composer({ value, onChange, onSend, disabled, placeholder, ariaLabel }: {
  value: string;
  onChange: (v: string) => void;
  onSend: () => void;
  disabled?: boolean;
  placeholder?: string;
  ariaLabel?: string;
}) {
  // Derive the send button's accessible name from the composer's own label so
  // multiple Composer instances on the same page (e.g. AIConversations + CopilotPanel
  // at desktop width) each expose a unique "Send to <surface>" label.  Playwright
  // strict mode and axe both fail when two buttons share the same name.
  const composerLabel = ariaLabel ?? "Message IIP Operations";
  const sendLabel = `Send to ${composerLabel.replace(/^Message /, "")}`;
  return (
    <div className="glass-elevated p-3 flex items-end gap-2">
      <textarea
        aria-label={composerLabel}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === "Enter" && !e.shiftKey) {
            e.preventDefault();
            onSend();
          }
        }}
        rows={1}
        placeholder={placeholder ?? "Ask IIP anything — claims, documents, customers, policies, workflows…"}
        className="min-h-11 flex-1 resize-none bg-transparent outline-none text-[14.5px] placeholder:text-muted text-ink py-2.5 px-2 max-h-40"
      />
      <button
        onClick={onSend}
        disabled={disabled || !value.trim()}
        className="h-11 w-11 shrink-0 grid place-items-center rounded-2xl gradient-primary text-white shadow-l2 disabled:opacity-40 disabled:cursor-not-allowed focus-ring"
        aria-label={sendLabel}
      >
        <ArrowUp className="h-4 w-4" />
      </button>
    </div>
  );
}

export function ChannelBadge({ channel }: { channel: string }) {
  const map: Record<string, { bg: string; text: string; ring: string }> = {
    WhatsApp: { bg: "bg-success/10", text: "text-success", ring: "ring-success/24" },
    Website:  { bg: "bg-violet/10",  text: "text-violet", ring: "ring-violet/24" },
    Web:      { bg: "bg-violet/10",  text: "text-violet", ring: "ring-violet/24" },
    Mobile:   { bg: "bg-info/10",    text: "text-info",    ring: "ring-info/24" },
    Email:    { bg: "bg-info/10",    text: "text-info",    ring: "ring-info/24" },
    SMS:      { bg: "bg-warning/12", text: "text-[hsl(38_92%_38%)]", ring: "ring-warning/28" },
    Teams:    { bg: "bg-violet/10",  text: "text-violet", ring: "ring-violet/24" },
    Broker:   { bg: "bg-line/60", text: "text-ink-soft", ring: "ring-line" },
    Branch:   { bg: "bg-line/60", text: "text-ink-soft", ring: "ring-line" },
  };
  const s = map[channel] ?? map.Broker;
  return <span className={cn("inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-[11px] font-medium ring-1 ring-inset", s.bg, s.text, s.ring)}>{channel}</span>;
}

export function PriorityBadge({ level }: { level: "High" | "Medium" | "Low" }) {
  const map = {
    High: "error" as const,
    Medium: "warning" as const,
    Low: "info" as const,
  };
  return <Badge tone={map[level]}>Priority · {level}</Badge>;
}
