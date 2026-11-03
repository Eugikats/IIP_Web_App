"use client";
import { useEffect, useRef, useState } from "react";
import { Sparkles, ShieldCheck, TrendingUp, AlertTriangle, FileText, Plug, BookOpen, MessageSquare, Inbox, UserSquare2, Bot, CreditCard, LineChart, ScrollText, ClipboardList, ExternalLink } from "lucide-react";
import type { ComponentType } from "react";
import { useLocation, useNavigate } from "@/components/iip/NextRouterCompat";
import { useUI } from "@/context/UIStateContext";
import { useProviderHealth } from "@/lib/lovable-parity/data/providerHealth";
import { isProviderHealthy } from "@/lib/providerHealth";
import { presentationMode } from "@/lib/config";
import { useCommandCenterQuery, type CommandCenterAnswer } from "@/lib/lovable-parity/data/commandCenterQuery";
import { AIMessageBubble, UserMessageBubble, Composer } from "@/components/iip/ChatPrimitives";
import { CommandCenterBlocks } from "@/components/iip/CommandCenterAnswer";
import { Button } from "@/components/ui/Button";
import { StatusPill } from "@/components/ui/StatusPill";

type Icon = ComponentType<{ className?: string }>;
type Block = { icon: Icon; label: string; text: string };
interface Ctx { title: string; blocks: Block[]; footer?: string; starters: string[] }

type Msg =
  | { id: string; role: "user"; text: string }
  | { id: string; role: "ai"; answer: CommandCenterAnswer }
  | { id: string; role: "ai-error"; text: string; requestId?: string; question?: string };

// Static, fabrication-free governance guidance per route, plus starter questions that route into
// the same real POST /api/v1/command-center/query path the full Command Center page uses. No
// counts or IDs are invented here, real numbers only ever come back through a live answer.
function blocksFor(pathname: string): Ctx {
  if (pathname.startsWith("/app/claims")) return {
    title: "Claim context",
    blocks: [
      { icon: FileText, label: "What AI does here", text: "Prepares extractions, summaries, drafts, and advisory signals for the claim in view." },
      { icon: ShieldCheck, label: "Recommended actions", text: "Surface from the case, request missing documents, confirm details, prepare the officer summary." },
      { icon: MessageSquare, label: "Customer comms", text: "Customer updates require officer approval before they are sent." },
    ],
    starters: ["Show claims that need attention.", "Which claims are missing documents?"],
    footer: "Settlement, denial, repudiation, payment, and fraud determination remain with the reviewing officer.",
  };
  if (pathname.startsWith("/app/channels")) return {
    title: "Channels snapshot",
    blocks: [
      { icon: Inbox, label: "Inbound", text: "Conversations arrive from WhatsApp, web, email, SMS, and brokers once channels are configured." },
      { icon: AlertTriangle, label: "Approval-gated replies", text: "Outbound drafts require human approval, sensitive messages never dispatch automatically." },
      { icon: Plug, label: "Provider health", text: "Live provider status is shown below." },
    ],
    starters: ["Which channel messages need manual review?", "Show unmatched conversations."],
    footer: "Sensitive outbound messages never dispatch automatically.",
  };
  if (pathname.startsWith("/app/customers")) return {
    title: "Customer context",
    blocks: [
      { icon: UserSquare2, label: "Workspace scope", text: "Customer 360 is staff-only. Customers do not see this view." },
      { icon: ShieldCheck, label: "AI summaries", text: "Summaries cite records or evidence IDs; unknown fields stay unknown." },
      { icon: MessageSquare, label: "Next best action", text: "Draft a customer update; request supervisor review before send." },
    ],
    starters: ["Show customers with open claims."],
  };
  if (pathname.startsWith("/app/documents")) return {
    title: "Document status",
    blocks: [
      { icon: FileText, label: "Flow", text: "Uploaded documents pass through extraction and human review." },
      { icon: ShieldCheck, label: "Threshold", text: "Low-confidence fields route to human review before use." },
      { icon: TrendingUp, label: "Retry", text: "Retries are idempotent, external actions are not duplicated." },
    ],
    starters: ["Which documents need extraction review?"],
  };
  if (pathname.startsWith("/app/communications")) return {
    title: "Communications",
    blocks: [
      { icon: MessageSquare, label: "Pending approval", text: "Drafts await reviewer approval before they are sent." },
      { icon: ShieldCheck, label: "Guardrail", text: "Drafts must not promise settlement, coverage, or payment." },
      { icon: TrendingUp, label: "Delivery", text: "Providers deliver only after a human approves the message." },
    ],
    starters: ["Show pending customer updates."],
    footer: "AI drafts. Humans approve. Providers deliver.",
  };
  if (pathname.startsWith("/app/fraud")) return {
    title: "Fraud signals",
    blocks: [
      { icon: AlertTriangle, label: "Advisory signals", text: "Computed by a deterministic rule engine from real case data." },
      { icon: ShieldCheck, label: "Guardrail", text: "Signals are advisory. IIP does not accuse or determine fraud." },
      { icon: FileText, label: "Evidence-linked", text: "Every signal cites an evidence reference and pattern rationale." },
    ],
    starters: ["Show open fraud signals."],
  };
  if (pathname.startsWith("/app/underwriting")) return {
    title: "Underwriting",
    blocks: [
      { icon: ClipboardList, label: "Queue", text: "Submissions await underwriter review." },
      { icon: ShieldCheck, label: "Guardrail", text: "AI drafts questions and summaries only. Final acceptance and pricing are human." },
      { icon: FileText, label: "Missing info", text: "Missing-information checks surface gaps for the underwriter." },
    ],
    starters: ["Show underwriting submissions awaiting review."],
  };
  if (pathname.startsWith("/app/integrations")) return {
    title: "Integration control plane",
    blocks: [
      { icon: Plug, label: "Provider modes", text: "Configured provider modes and runtime-reported health are shown below." },
      { icon: AlertTriangle, label: "Honest diagnostics", text: "Queue, retry, and correlation telemetry stay Not configured until real endpoints are wired." },
      { icon: MessageSquare, label: "Controlled adapters", text: "Write-back and customer delivery remain human-approved, provider-specific actions." },
    ],
    starters: [],
  };
  if (pathname.startsWith("/app/knowledge")) return {
    title: "Knowledge status",
    blocks: [
      { icon: BookOpen, label: "Approval", text: "Documents pass review before they are retrieval-eligible." },
      { icon: ShieldCheck, label: "Retrieval eligibility", text: "Only approved sources ground the assistant's answers." },
      { icon: FileText, label: "Grounding", text: "Policy answers are grounded in approved sources only." },
    ],
    starters: ["What does our motor policy wording say about windscreen cover?"],
  };
  if (pathname.startsWith("/app/work-items") || pathname.startsWith("/app/workflows")) return {
    title: "Review & approvals",
    blocks: [
      { icon: ShieldCheck, label: "Approvals", text: "Tasks await human decision across your team." },
      { icon: AlertTriangle, label: "Escalations", text: "Escalated items route to a supervisor for review." },
      { icon: TrendingUp, label: "Workload", text: "Reassignment suggestions are advisory, a human decides." },
    ],
    starters: ["Show pending approvals."],
    footer: "Approvals advance workflow only, they do not settle, pay, deny, or accuse fraud.",
  };
  if (pathname.startsWith("/app/executive")) return {
    title: "Executive brief",
    blocks: [
      { icon: LineChart, label: "KPIs", text: "Operational KPIs come from the reports service for this workspace." },
      { icon: TrendingUp, label: "Trends", text: "Trends reflect real claims-by-status and SLA data." },
      { icon: ShieldCheck, label: "Note", text: "Financial and loss metrics require validated data sources before display." },
    ],
    starters: ["Show SLA breaches."],
  };
  if (pathname.startsWith("/app/audit")) return {
    title: "Audit context",
    blocks: [
      { icon: ScrollText, label: "Coverage", text: "AI runs, human overrides, approvals, integrations, communications, and knowledge." },
      { icon: ShieldCheck, label: "Evidence", text: "Every event carries a correlation ID and evidence link." },
      { icon: FileText, label: "Export", text: "CSV export for regulator packs, no secrets, no unnecessary PII." },
    ],
    starters: [],
  };
  if (pathname.startsWith("/app/admin/agents") || pathname.startsWith("/app/admin/tenant-policy")) return {
    title: "Agent governance",
    blocks: [
      { icon: Bot, label: "Registry", text: "The agent registry lists real agents and their kill-switch state." },
      { icon: ShieldCheck, label: "Policy", text: "Human approval is required for regulated actions." },
      { icon: AlertTriangle, label: "Kill switch", text: "The kill switch can only tighten what runs; it never grants authority." },
    ],
    starters: [],
    footer: "Feedback does not auto-retrain production agents; improvements go through testing and approval.",
  };
  if (pathname.startsWith("/app/admin/billing")) return {
    title: "Usage snapshot",
    blocks: [
      { icon: CreditCard, label: "Entitlements", text: "Package entitlements and real usage meters for this workspace." },
      { icon: TrendingUp, label: "Metering", text: "Usage counts are computed from real activity; unmetered dimensions show Not configured." },
      { icon: ShieldCheck, label: "Guardrail", text: "AI is metered; human-in-loop is core value, not optional." },
    ],
    starters: [],
  };
  if (pathname.startsWith("/app/admin")) return {
    title: "Admin",
    blocks: [
      { icon: ShieldCheck, label: "Tenant policy", text: "Human review for regulated decisions is enforced platform-wide." },
      { icon: Bot, label: "Agents", text: "Manage the agent registry, prompt versions, and kill switch." },
      { icon: CreditCard, label: "Billing & Usage", text: "Package, entitlements, seats, and usage meters." },
    ],
    starters: [],
  };
  // Default: Command Center
  return {
    title: "Ask IIP",
    blocks: [
      { icon: Sparkles, label: "What you can ask", text: "Claims, documents, SLA, approvals, and missing information across this workspace." },
      { icon: ShieldCheck, label: "Grounding", text: "Answers show whether they use a current, historical, or synthetic workspace snapshot, with timestamp and limits." },
      { icon: TrendingUp, label: "Evidence", text: "Data answers cite the records and evidence they draw from." },
    ],
    starters: ["Show claims that need attention.", "Show SLA breaches.", "Show pending approvals."],
    footer: "AI prepares work. Regulated decisions remain with the reviewing officer.",
  };
}

export default function CopilotPanel() {
  const loc = useLocation();
  const navigate = useNavigate();
  const { toggleCopilot, roleDef } = useUI();
  const { providers, loading: providersLoading, source, dataAsOf } = useProviderHealth();
  const { ask, loading } = useCommandCenterQuery();
  const ctx = blocksFor(loc.pathname);

  const [messages, setMessages] = useState<Msg[]>([]);
  const [input, setInput] = useState("");
  const [conversationId, setConversationId] = useState<string | undefined>(undefined);
  const scrollRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: "smooth" });
  }, [messages, loading]);

  async function send(question?: string, retryRequestId?: string) {
    const q = (question ?? input).trim();
    if (!q || loading) return;
    const requestId = retryRequestId ?? crypto.randomUUID();
    setMessages((current) => retryRequestId
      ? current.filter((m) => !(m.role === "ai-error" && m.requestId === retryRequestId))
      : [...current, { id: crypto.randomUUID(), role: "user", text: q }]);
    setInput("");
    const result = await ask(q, conversationId, requestId);
    if (!result.ok) {
      setMessages((current) => [...current, {
        id: crypto.randomUUID(),
        role: "ai-error",
        text: result.message,
        requestId: result.kind === "reconcile" ? requestId : undefined,
        question: q,
      }]);
      return;
    }
    if (result.answer.conversationId && !conversationId) setConversationId(result.answer.conversationId);
    setMessages((current) => [...current, { id: crypto.randomUUID(), role: "ai", answer: result.answer }]);
  }

  const hasConversation = messages.length > 0;

  return (
    <aside aria-label="IIP Copilot" className="flex h-full flex-col border-l border-dls-border-subtle bg-dls-surface">
      <div className="flex items-center justify-between border-b border-dls-border-subtle p-5">
        <div className="flex items-center gap-2.5">
          <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-dls-chip bg-dls-primary-soft" aria-hidden="true">
            <Sparkles className="h-4 w-4 text-dls-primary" />
          </div>
          <div>
            <div className="text-[13px] font-bold text-dls-text-primary">{hasConversation ? "IIP Copilot" : ctx.title}</div>
            <div className="text-[11px] text-dls-text-muted">Ask a question, real, grounded answers</div>
          </div>
        </div>
        <div className="flex items-center gap-1">
          {hasConversation && (
            <button
              type="button"
              onClick={() => navigate(conversationId ? `/app/conversations/${conversationId}` : "/app/conversations")}
              className="dls-focus-outline inline-flex items-center gap-1 rounded-dls-tight px-1.5 py-1 text-[11px] font-semibold text-dls-primary hover:text-dls-primary-hover"
              aria-label="Open full conversation"
            >
              Expand <ExternalLink className="h-3 w-3" aria-hidden="true" />
            </button>
          )}
          <button
            type="button"
            onClick={toggleCopilot}
            className="dls-focus-outline rounded-dls-tight px-1.5 py-1 text-[11px] font-semibold text-dls-text-muted hover:text-dls-text-primary"
            aria-label="Collapse Copilot"
          >
            Hide
          </button>
        </div>
      </div>

      <div ref={scrollRef} tabIndex={0} aria-label="Copilot conversation" className="dls-focus-outline flex-1 space-y-3 overflow-y-auto p-4">
        {!hasConversation && (
          <>
            {ctx.blocks.map((b, i) => (
              <div key={i} className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4">
                <div className="mb-1.5 flex items-center gap-2">
                  <b.icon className="h-3.5 w-3.5 text-dls-primary" aria-hidden="true" />
                  <div className="text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">{b.label}</div>
                </div>
                <div className="text-[13px] leading-[19px] text-dls-text-secondary">{b.text}</div>
              </div>
            ))}

            {ctx.starters.length > 0 && (
              <div className="flex flex-wrap gap-1.5 pt-1">
                {ctx.starters.map((s) => (
                  <button
                    key={s}
                    type="button"
                    onClick={() => void send(s)}
                    className="dls-focus-outline rounded-dls-chip border border-dls-border-input px-2.5 py-1.5 text-left text-[11.5px] font-semibold text-dls-text-primary transition-colors duration-dls-fast ease-dls hover:border-dls-primary"
                  >
                    {s}
                  </button>
                ))}
              </div>
            )}

            {!presentationMode && (
            <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4">
              <div className="mb-2 text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Provider health</div>
              {providersLoading ? (
                <div className="text-[12px] text-dls-text-muted">Checking provider health…</div>
              ) : providers.length === 0 ? (
                <StatusPill tone="neutral" label="Not configured" />
              ) : (
                <div className="space-y-1.5">
                  {source && dataAsOf ? (
                    <div className="pb-1 text-[10.5px] text-dls-text-muted">
                      {source === "synthetic" ? "Synthetic snapshot" : "Runtime response"} · {new Date(dataAsOf).toLocaleString()}
                    </div>
                  ) : null}
                  {providers.map((p) => (
                    <div key={p.name} className="flex items-center justify-between gap-2">
                      <span className="truncate text-[12px] text-dls-text-secondary">{p.name}</span>
                      <StatusPill tone={isProviderHealthy(p.status) ? "success" : "warning"} label={p.status} />
                    </div>
                  ))}
                </div>
              )}
            </div>
            )}

            {ctx.footer && (
              <div className="rounded-dls-card border border-dls-primary-border bg-dls-primary-soft-alt p-4">
                <div className="mb-1 text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-secondary">Human review reminder</div>
                <p className="text-[13px] leading-[19px] text-dls-text-secondary">{ctx.footer}</p>
              </div>
            )}
          </>
        )}

        {messages.map((m) =>
          m.role === "user" ? (
            <UserMessageBubble key={m.id} text={m.text} />
          ) : m.role === "ai" ? (
            <AIMessageBubble key={m.id}>
              <CommandCenterBlocks blocks={m.answer.blocks} />
            </AIMessageBubble>
          ) : (
            <AIMessageBubble key={m.id}>
              <p className="text-[13px] text-dls-danger-text">{m.text}</p>
              {m.requestId && m.question ? (
                <Button variant="secondary" className="mt-2" onClick={() => void send(m.question, m.requestId)}>
                  Retry safely
                </Button>
              ) : null}
            </AIMessageBubble>
          )
        )}
        {loading && (
          <div className="flex items-center gap-2 text-[12px] text-dls-primary">
            <span className="h-2 w-2 rounded-full bg-dls-primary" aria-hidden="true" /> Preparing a grounded response&hellip;
          </div>
        )}
      </div>

      <div className="border-t border-dls-border-subtle p-3">
        <Composer
          value={input}
          onChange={setInput}
          onSend={() => void send()}
          disabled={loading}
          ariaLabel="Message IIP Copilot"
          placeholder={ctx.title.startsWith("Ask") ? `${ctx.title}…` : `Ask about ${ctx.title.toLowerCase()}…`}
        />
        <p className="mt-1 text-center text-[10px] text-dls-text-muted">Role: {roleDef.label}</p>
      </div>
    </aside>
  );
}
