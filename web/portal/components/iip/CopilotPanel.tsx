"use client";
import { Sparkles, ShieldCheck, TrendingUp, AlertTriangle, FileText, Plug, BookOpen, MessageSquare, Inbox, UserSquare2, Bot, CreditCard, LineChart, ScrollText, ClipboardList } from "lucide-react";
import type { ComponentType } from "react";
import { useLocation } from "@/components/iip/NextRouterCompat";
import { useUI } from "@/context/UIStateContext";
import { useProviderHealth } from "@/lib/lovable-parity/data/providerHealth";
import { isProviderHealthy } from "@/lib/providerHealth";

type Icon = ComponentType<{ className?: string }>;
type Block = { icon: Icon; label: string; text: string };
interface Ctx { title: string; blocks: Block[]; footer?: string }

// Static, fabrication-free governance guidance per route. No counts or IDs are invented here —
// real numbers live on the screens themselves (grounded in /api/v1 data). This rail carries
// human-review guidance + real provider health only.
function blocksFor(pathname: string): Ctx {
  if (pathname.startsWith("/app/claims")) return {
    title: "Claim context",
    blocks: [
      { icon: FileText, label: "What AI does here", text: "Prepares extractions, summaries, drafts, and advisory signals for the claim in view." },
      { icon: ShieldCheck, label: "Recommended actions", text: "Surface from the case — request missing documents, confirm details, prepare the officer summary." },
      { icon: MessageSquare, label: "Customer comms", text: "Customer updates require officer approval before they are sent." },
    ],
    footer: "Settlement, denial, repudiation, payment, and fraud determination remain with the reviewing officer.",
  };
  if (pathname.startsWith("/app/channels")) return {
    title: "Channels snapshot",
    blocks: [
      { icon: Inbox, label: "Inbound", text: "Conversations arrive from WhatsApp, web, email, SMS, and brokers once channels are configured." },
      { icon: AlertTriangle, label: "Approval-gated replies", text: "Outbound drafts require human approval — sensitive messages never dispatch automatically." },
      { icon: Plug, label: "Provider health", text: "Live provider status is shown below." },
    ],
    footer: "Sensitive outbound messages never dispatch automatically.",
  };
  if (pathname.startsWith("/app/customers")) return {
    title: "Customer context",
    blocks: [
      { icon: UserSquare2, label: "Workspace scope", text: "Customer 360 is staff-only. Customers do not see this view." },
      { icon: ShieldCheck, label: "AI summaries", text: "Summaries cite records or evidence IDs; unknown fields stay unknown." },
      { icon: MessageSquare, label: "Next best action", text: "Draft a customer update; request supervisor review before send." },
    ],
  };
  if (pathname.startsWith("/app/documents")) return {
    title: "Document status",
    blocks: [
      { icon: FileText, label: "Flow", text: "Uploaded documents pass through extraction and human review." },
      { icon: ShieldCheck, label: "Threshold", text: "Low-confidence fields route to human review before use." },
      { icon: TrendingUp, label: "Retry", text: "Retries are idempotent — external actions are not duplicated." },
    ],
  };
  if (pathname.startsWith("/app/communications")) return {
    title: "Communications",
    blocks: [
      { icon: MessageSquare, label: "Pending approval", text: "Drafts await reviewer approval before they are sent." },
      { icon: ShieldCheck, label: "Guardrail", text: "Drafts must not promise settlement, coverage, or payment." },
      { icon: TrendingUp, label: "Delivery", text: "Providers deliver only after a human approves the message." },
    ],
    footer: "AI drafts. Humans approve. Providers deliver.",
  };
  if (pathname.startsWith("/app/fraud")) return {
    title: "Fraud signals",
    blocks: [
      { icon: AlertTriangle, label: "Advisory signals", text: "Computed by a deterministic rule engine from real case data." },
      { icon: ShieldCheck, label: "Guardrail", text: "Signals are advisory. IIP does not accuse or determine fraud." },
      { icon: FileText, label: "Evidence-linked", text: "Every signal cites an evidence reference and pattern rationale." },
    ],
  };
  if (pathname.startsWith("/app/underwriting")) return {
    title: "Underwriting",
    blocks: [
      { icon: ClipboardList, label: "Queue", text: "Submissions await underwriter review." },
      { icon: ShieldCheck, label: "Guardrail", text: "AI drafts questions and summaries only. Final acceptance and pricing are human." },
      { icon: FileText, label: "Missing info", text: "Missing-information checks surface gaps for the underwriter." },
    ],
  };
  if (pathname.startsWith("/app/integrations")) return {
    title: "Integration control plane",
    blocks: [
      { icon: Plug, label: "Provider modes", text: "Configured provider modes and runtime-reported health are shown below." },
      { icon: AlertTriangle, label: "Honest diagnostics", text: "Queue, retry, and correlation telemetry stay Not configured until real endpoints are wired." },
      { icon: MessageSquare, label: "Controlled adapters", text: "Write-back and customer delivery remain human-approved, provider-specific actions." },
    ],
  };
  if (pathname.startsWith("/app/knowledge")) return {
    title: "Knowledge status",
    blocks: [
      { icon: BookOpen, label: "Approval", text: "Documents pass review before they are retrieval-eligible." },
      { icon: ShieldCheck, label: "Retrieval eligibility", text: "Only approved sources ground the assistant's answers." },
      { icon: FileText, label: "Grounding", text: "Policy answers are grounded in approved sources only." },
    ],
  };
  if (pathname.startsWith("/app/work-items") || pathname.startsWith("/app/workflows")) return {
    title: "Review & approvals",
    blocks: [
      { icon: ShieldCheck, label: "Approvals", text: "Tasks await human decision across your team." },
      { icon: AlertTriangle, label: "Escalations", text: "Escalated items route to a supervisor for review." },
      { icon: TrendingUp, label: "Workload", text: "Reassignment suggestions are advisory — a human decides." },
    ],
    footer: "Approvals advance workflow only — they do not settle, pay, deny, or accuse fraud.",
  };
  if (pathname.startsWith("/app/executive")) return {
    title: "Executive brief",
    blocks: [
      { icon: LineChart, label: "KPIs", text: "Operational KPIs come from the reports service for this workspace." },
      { icon: TrendingUp, label: "Trends", text: "Trends reflect real claims-by-status and SLA data." },
      { icon: ShieldCheck, label: "Note", text: "Financial and loss metrics require validated data sources before display." },
    ],
  };
  if (pathname.startsWith("/app/audit")) return {
    title: "Audit context",
    blocks: [
      { icon: ScrollText, label: "Coverage", text: "AI runs, human overrides, approvals, integrations, communications, and knowledge." },
      { icon: ShieldCheck, label: "Evidence", text: "Every event carries a correlation ID and evidence link." },
      { icon: FileText, label: "Export", text: "CSV export for regulator packs — no secrets, no unnecessary PII." },
    ],
  };
  if (pathname.startsWith("/app/admin/agents") || pathname.startsWith("/app/admin/tenant-policy")) return {
    title: "Agent governance",
    blocks: [
      { icon: Bot, label: "Registry", text: "The agent registry lists real agents and their kill-switch state." },
      { icon: ShieldCheck, label: "Policy", text: "Human approval is required for regulated actions." },
      { icon: AlertTriangle, label: "Kill switch", text: "The kill switch can only tighten what runs; it never grants authority." },
    ],
    footer: "Feedback does not auto-retrain production agents; improvements go through testing and approval.",
  };
  if (pathname.startsWith("/app/admin/billing")) return {
    title: "Usage snapshot",
    blocks: [
      { icon: CreditCard, label: "Entitlements", text: "Package entitlements and real usage meters for this workspace." },
      { icon: TrendingUp, label: "Metering", text: "Usage counts are computed from real activity; unmetered dimensions show Not configured." },
      { icon: ShieldCheck, label: "Guardrail", text: "AI is metered; human-in-loop is core value, not optional." },
    ],
  };
  if (pathname.startsWith("/app/admin")) return {
    title: "Admin",
    blocks: [
      { icon: ShieldCheck, label: "Tenant policy", text: "Human review for regulated decisions is enforced platform-wide." },
      { icon: Bot, label: "Agents", text: "Manage the agent registry, prompt versions, and kill switch." },
      { icon: CreditCard, label: "Billing & Usage", text: "Package, entitlements, seats, and usage meters." },
    ],
  };
  // Default: Command Center
  return {
    title: "Execution guidance",
    blocks: [
      { icon: Sparkles, label: "What you can ask", text: "Claims, documents, SLA, approvals, and missing information across this workspace." },
      { icon: ShieldCheck, label: "Grounding", text: "Answers show whether they use a current, historical, or synthetic workspace snapshot, with timestamp and limits." },
      { icon: TrendingUp, label: "Evidence", text: "Data answers cite the records and evidence they draw from." },
    ],
    footer: "AI prepares work. Regulated decisions remain with the reviewing officer.",
  };
}

export default function CopilotPanel() {
  const loc = useLocation();
  const { toggleCopilot } = useUI();
  const { providers, loading, source, dataAsOf } = useProviderHealth();
  const ctx = blocksFor(loc.pathname);

  return (
    <aside aria-label="IIP Copilot" className="h-full flex flex-col border-l border-line/70 bg-white/50 backdrop-blur-xl">
      <div className="p-5 border-b border-line/70 flex items-center justify-between">
        <div className="flex items-center gap-2.5">
          <div className="h-8 w-8 rounded-xl gradient-primary grid place-items-center shadow-l2">
            <Sparkles className="h-4 w-4 text-white" />
          </div>
          <div>
            <div className="text-[13px] font-semibold text-ink">{ctx.title}</div>
            <div className="text-[11px] text-muted">Human-reviewed intelligence</div>
          </div>
        </div>
        <button onClick={toggleCopilot} className="text-[11px] text-muted hover:text-ink" aria-label="Collapse Copilot">Hide</button>
      </div>

      <div tabIndex={0} aria-label="Copilot conversation" className="flex-1 overflow-y-auto p-4 space-y-3 focus-ring">
        {ctx.blocks.map((b, i) => (
          <div key={i} className="glass-subtle p-4 hover-lift">
            <div className="flex items-center gap-2 mb-1.5">
              <b.icon className="h-3.5 w-3.5 text-info" />
              <div className="text-[11px] uppercase tracking-[0.12em] text-muted">{b.label}</div>
            </div>
            <div className="text-[13px] leading-[19px] text-ink-soft">{b.text}</div>
          </div>
        ))}

        <div className="glass-subtle p-4">
          <div className="text-[11px] uppercase tracking-[0.12em] text-muted mb-2">Provider health</div>
          {loading ? (
            <div className="text-[12px] text-muted">Checking provider health…</div>
          ) : providers.length === 0 ? (
            <div className="text-[12px] text-muted">Not configured — no provider health reported for this workspace.</div>
          ) : (
            <div className="space-y-1.5 text-[12px]">
              {source && dataAsOf ? <div className="pb-1 text-[10.5px] text-muted">{source === "synthetic" ? "Synthetic snapshot" : "Runtime response"} · {new Date(dataAsOf).toLocaleString()}</div> : null}
              {providers.map((p) => (
                <div key={p.name} className="flex items-center justify-between gap-2">
                  <span className="text-ink-soft truncate">{p.name}</span>
                  <span className={isProviderHealthy(p.status) ? "text-success shrink-0" : "text-warning shrink-0"}>{p.status}</span>
                </div>
              ))}
            </div>
          )}
        </div>

        {ctx.footer && (
          <div className="rounded-2xl border border-violet/24 bg-violet/6 p-4">
            <div className="text-[11px] uppercase tracking-[0.12em] text-violet mb-1">Human review reminder</div>
            <p className="text-[13px] leading-[19px] text-ink-soft">{ctx.footer}</p>
          </div>
        )}
      </div>
    </aside>
  );
}
