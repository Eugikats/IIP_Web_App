import { Sparkles, ShieldCheck, TrendingUp, AlertTriangle, FileText, Plug, BookOpen, MessageSquare, Inbox, UserSquare2, Bot, CreditCard, LifeBuoy, LineChart, ScrollText, ClipboardList } from "lucide-react";
import { useLocation } from "@/components/iip/NextRouterCompat";
import { useUI } from "@/context/UIStateContext";

type Block = { icon: any; label: string; text: string };
interface Ctx { title: string; blocks: Block[]; footer?: string }

function blocksFor(pathname: string): Ctx {
  if (pathname.startsWith("/app/claims")) return {
    title: "Claim context",
    blocks: [
      { icon: FileText, label: "Claim in view", text: "CLM-1048 · Aisha Nakato · WhatsApp intake · SLA at risk." },
      { icon: ShieldCheck, label: "Recommended actions", text: "Request police report. Confirm repair quotation. Prepare officer summary." },
      { icon: MessageSquare, label: "Customer comms", text: "1 draft update awaiting officer approval before send." },
    ],
    footer: "Regulated decisions remain with the reviewing officer.",
  };
  if (pathname.startsWith("/app/channels")) return {
    title: "Channels snapshot",
    blocks: [
      { icon: Inbox, label: "Today's volume", text: "WhatsApp 214 · Web 88 · Email 41 · SMS 12 · Broker 9." },
      { icon: AlertTriangle, label: "Approval-gated replies", text: "6 outbound drafts pending human approval before send." },
      { icon: Plug, label: "Provider health", text: "WhatsApp healthy · Microsoft Graph degraded (2.4s)." },
    ],
    footer: "Sensitive outbound messages never dispatch automatically.",
  };
  if (pathname.startsWith("/app/customers")) return {
    title: "Customer context",
    blocks: [
      { icon: UserSquare2, label: "Workspace scope", text: "Customer 360 is staff-only. Customers do not see this view." },
      { icon: ShieldCheck, label: "AI summaries", text: "Summaries cite records or evidence IDs; unknown fields stay unknown." },
      { icon: MessageSquare, label: "Next best action", text: "Draft customer update; request supervisor review before send." },
    ],
  };
  if (pathname.startsWith("/app/documents")) return {
    title: "Document status",
    blocks: [
      { icon: FileText, label: "Queue", text: "213 documents processed today · 6 review-required · 1 failed." },
      { icon: ShieldCheck, label: "Threshold", text: "Confidence < 0.80 routes to human review before use." },
      { icon: TrendingUp, label: "Retry", text: "Retries are idempotent — external actions are not duplicated." },
    ],
  };
  if (pathname.startsWith("/app/communications")) return {
    title: "Communications",
    blocks: [
      { icon: MessageSquare, label: "Pending approval", text: "4 drafts awaiting reviewer approval before send." },
      { icon: ShieldCheck, label: "Guardrail", text: "Drafts must not promise settlement, coverage, or payment." },
      { icon: TrendingUp, label: "Send outcomes (24h)", text: "Sent 88 · Failed 3 (retryable) · Rejected 2." },
    ],
    footer: "AI drafts. Humans approve. Providers deliver.",
  };
  if (pathname.startsWith("/app/fraud")) return {
    title: "Fraud signals",
    blocks: [
      { icon: AlertTriangle, label: "Advisory signals (7d)", text: "24 signals raised · 6 require investigation · 4 under review." },
      { icon: ShieldCheck, label: "Guardrail", text: "Signals are advisory. IIP does not accuse or determine fraud." },
      { icon: FileText, label: "Evidence-linked", text: "Every signal cites an evidence ID and pattern rationale." },
    ],
  };
  if (pathname.startsWith("/app/underwriting")) return {
    title: "Underwriting",
    blocks: [
      { icon: ClipboardList, label: "Queue", text: "3 submissions awaiting underwriter review." },
      { icon: ShieldCheck, label: "Guardrail", text: "AI drafts questions and summaries only. Final acceptance is human." },
      { icon: FileText, label: "Missing info", text: "Missing NCD certificate and valuation on SUB-7703." },
    ],
  };
  if (pathname.startsWith("/app/integrations")) return {
    title: "Channel activity",
    blocks: [
      { icon: Plug, label: "Today's volume", text: "WhatsApp 214 · Web 88 · Email 41 · SMS 12." },
      { icon: AlertTriangle, label: "Integration health", text: "WhatsApp Cloud API healthy · Microsoft Graph degraded (2.4s)." },
      { icon: MessageSquare, label: "Messages needing review", text: "6 threads flagged for human handoff." },
    ],
  };
  if (pathname.startsWith("/app/knowledge")) return {
    title: "Knowledge status",
    blocks: [
      { icon: BookOpen, label: "Approved documents", text: "42 approved · 5 pending review · 2 need re-indexing." },
      { icon: ShieldCheck, label: "Retrieval eligibility", text: "Only approved sources ground the assistant's answers." },
      { icon: FileText, label: "Source quality", text: "Average confidence on retrieved chunks this week: 0.89." },
    ],
  };
  if (pathname.startsWith("/app/workflows")) return {
    title: "Workflow snapshot",
    blocks: [
      { icon: ShieldCheck, label: "Approvals pending", text: "9 tasks awaiting human decision across your team." },
      { icon: AlertTriangle, label: "Escalations", text: "3 escalated to Supervisor. Average time-to-decision: 4h 12m." },
      { icon: TrendingUp, label: "Team workload", text: "J. Mugisha over capacity by ~18%. Reassignment suggested." },
    ],
    footer: "Approvals advance workflow only — they do not settle, pay, deny, or accuse fraud.",
  };
  if (pathname.startsWith("/app/executive")) return {
    title: "Executive brief",
    blocks: [
      { icon: LineChart, label: "MVP metrics", text: "Claims volume, SLA breach rate, approval turnaround, doc completeness." },
      { icon: TrendingUp, label: "Trend", text: "Turnaround down to 4.1d (-0.5). AI questions answered +10% MoM." },
      { icon: ShieldCheck, label: "Note", text: "Financial and loss metrics are placeholders pending data validation." },
    ],
  };
  if (pathname.startsWith("/app/support")) return {
    title: "Support & health",
    blocks: [
      { icon: LifeBuoy, label: "Environment", text: "PROD · Priority 24×7 support tier." },
      { icon: AlertTriangle, label: "Degraded", text: "Microsoft Graph elevated latency; drafts may be delayed." },
      { icon: TrendingUp, label: "Queue depth", text: "128 normal · 6 retryable failures · 0 duplicates on retry." },
    ],
  };
  if (pathname.startsWith("/app/audit")) return {
    title: "Audit context",
    blocks: [
      { icon: ScrollText, label: "Coverage", text: "AI runs, human overrides, approvals, integrations, communications, knowledge, agent policy." },
      { icon: ShieldCheck, label: "Evidence", text: "Every event carries a correlation ID and evidence link." },
      { icon: FileText, label: "Export", text: "CSV export for regulator packs — no secrets, no unnecessary PII." },
    ],
  };
  if (pathname.startsWith("/app/admin/agents") || pathname.startsWith("/app/admin/tenant-policy")) return {
    title: "Agent governance",
    blocks: [
      { icon: Bot, label: "Registry", text: "13 agents · 11 active · 2 in testing · 1 disabled." },
      { icon: ShieldCheck, label: "Policy version", text: "Tenant policy v2.3 · human approval required for regulated actions." },
      { icon: AlertTriangle, label: "Kill switch", text: "8 layered controls available. Executing any requires secondary approval." },
    ],
    footer: "Feedback does not auto-retrain production agents; improvements go through testing and approval.",
  };
  if (pathname.startsWith("/app/admin/billing")) return {
    title: "Usage snapshot",
    blocks: [
      { icon: CreditCard, label: "Billing period", text: "Nov 1 – Nov 30, 2026 · Enterprise Insurance package." },
      { icon: TrendingUp, label: "AI usage", text: "12,840 / 25,000 runs used this period." },
      { icon: ShieldCheck, label: "Guardrail", text: "AI is metered; human-in-loop is core value, not optional." },
    ],
  };
  if (pathname.startsWith("/app/admin")) return {
    title: "Admin",
    blocks: [
      { icon: ShieldCheck, label: "Tenant policy", text: "Human review for regulated decisions is enforced platform-wide." },
      { icon: Bot, label: "Agents", text: "Manage agent registry, prompt versions, and kill switch." },
      { icon: CreditCard, label: "Billing & Usage", text: "Package, entitlements, seats, and usage meters." },
    ],
  };
  // Default: AI Conversations
  return {
    title: "Execution snapshot",
    blocks: [
      { icon: AlertTriangle, label: "Top priorities", text: "3 claims approaching SLA · 1 advisory duplicate-claimant signal." },
      { icon: ShieldCheck, label: "Active workflow suggestions", text: "Route CLM-1064 to Supervisor · draft update on CLM-1082." },
      { icon: TrendingUp, label: "AI insight", text: "WhatsApp intake shows 22% higher document-completeness this week." },
    ],
    footer: "AI prepares work. Regulated decisions remain with the reviewing officer.",
  };
}




export default function CopilotPanel() {
  const loc = useLocation();
  const { toggleCopilot } = useUI();
  const ctx = blocksFor(loc.pathname);

  return (
    <aside className="h-full flex flex-col border-l border-cloud/70 bg-white/50 backdrop-blur-xl">
      <div className="p-5 border-b border-cloud/70 flex items-center justify-between">
        <div className="flex items-center gap-2.5">
          <div className="h-8 w-8 rounded-xl gradient-primary grid place-items-center shadow-l2">
            <Sparkles className="h-4 w-4 text-white" />
          </div>
          <div>
            <div className="text-[13px] font-semibold text-navy">{ctx.title}</div>
            <div className="text-[11px] text-stone">Human-reviewed intelligence</div>
          </div>
        </div>
        <button onClick={toggleCopilot} className="text-[11px] text-stone hover:text-navy" aria-label="Collapse Copilot">Hide</button>
      </div>

      <div className="flex-1 overflow-y-auto p-4 space-y-3">
        {ctx.blocks.map((b, i) => {
          const Icon = typeof b.icon === "function" ? b.icon() : b.icon;
          return (
            <div key={i} className="glass-subtle p-4 hover-lift">
              <div className="flex items-center gap-2 mb-1.5">
                <Icon className="h-3.5 w-3.5 text-info" />
                <div className="text-[11px] uppercase tracking-[0.12em] text-stone">{b.label}</div>
              </div>
              <div className="text-[13px] leading-[19px] text-slate2">{b.text}</div>
            </div>
          );
        })}

        <div className="glass-subtle p-4">
          <div className="text-[11px] uppercase tracking-[0.12em] text-stone mb-2">Provider health</div>
          <div className="space-y-1.5 text-[12px]">
            {[
              { n: "Azure OpenAI · GPT-4o", s: "Healthy", lat: "412ms" },
              { n: "Azure Document Intelligence", s: "Healthy", lat: "1.1s" },
              { n: "WhatsApp Cloud API", s: "Healthy", lat: "220ms" },
              { n: "Microsoft Graph", s: "Degraded", lat: "2.4s" },
            ].map(p => (
              <div key={p.n} className="flex items-center justify-between">
                <span className="text-slate2">{p.n}</span>
                <span className={p.s === "Healthy" ? "text-success" : "text-warning"}>{p.s} · {p.lat}</span>
              </div>
            ))}
          </div>
        </div>

        {ctx.footer && (
          <div className="rounded-2xl border border-[hsl(var(--violet)/0.24)] bg-[hsl(var(--violet)/0.06)] p-4">
            <div className="text-[11px] uppercase tracking-[0.12em] text-violet2 mb-1">Human review reminder</div>
            <p className="text-[13px] leading-[19px] text-slate2">{ctx.footer}</p>
          </div>
        )}
      </div>
    </aside>
  );
}
