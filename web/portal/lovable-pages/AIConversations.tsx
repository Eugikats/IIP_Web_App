"use client";
import { useMemo, useRef, useState, useEffect } from "react";
import type { ReactNode } from "react";
import { Sparkles, ArrowUpRight, ArrowDownRight, MessagesSquare, ShieldCheck, TrendingUp, AlertTriangle } from "lucide-react";
import AIContextWindow, { type ContextStep } from "@/components/iip/AIContextWindow";
import { AIMessageBubble, UserMessageBubble, InsightCard, ApprovalCard, Composer } from "@/components/iip/ChatPrimitives";
import { useUI } from "@/context/UIStateContext";

type Msg =
  | { id: string; role: "user"; text: string }
  | { id: string; role: "ai"; render: () => ReactNode; sources?: string[] };

const STARTERS = [
  "Show high-risk claims today.",
  "Which policy documents are missing?",
  "Explain why fraud alerts increased.",
  "Draft customer update for claim CLM-2025-00928.",
  "Route this to supervisor approval.",
  "Summarize CLM-1048 with evidence.",
];

const HERO_METRICS = [
  { label: "Open claims", value: 148, delta: "+6", up: true, icon: MessagesSquare },
  { label: "SLA at risk", value: 12, delta: "+1", up: true, icon: AlertTriangle, tone: "warning" as const },
  { label: "Approvals pending", value: 9, delta: "0", up: false, icon: ShieldCheck, tone: "violet" as const },
  { label: "Fraud signals", value: 4, delta: "+1", up: true, icon: TrendingUp, tone: "error" as const },
];

function classifyIntent(q: string): "claims" | "policy" | "fraud" | "draft" | "route" | "generic" {
  const s = q.toLowerCase();
  if (s.includes("draft") || s.includes("customer update")) return "draft";
  if (s.includes("route") || s.includes("supervisor") || s.includes("approval")) return "route";
  if (s.includes("fraud")) return "fraud";
  if (s.includes("policy") || s.includes("document")) return "policy";
  if (s.includes("claim") || s.includes("clm-") || s.includes("high-risk")) return "claims";
  return "generic";
}

function stepsForIntent(intent: ReturnType<typeof classifyIntent>): { steps: ContextStep[]; sources: string[]; workflow?: string } {
  switch (intent) {
    case "claims":
      return {
        steps: [
          { label: "Reading open claims work queue", state: "done" },
          { label: "Reviewing claim CLM-2025-00928", state: "done" },
          { label: "Ranking by risk and SLA proximity", state: "active" },
          { label: "Preparing summary for reviewer", state: "pending" },
        ],
        sources: ["Claims Work Queue", "SLA Ruleset v1.4", "Risk Signals · advisory"],
        workflow: "Officer triage",
      };
    case "policy":
      return {
        steps: [
          { label: "Checking approved policy documents", state: "done" },
          { label: "Comparing required document checklist", state: "active" },
          { label: "Grounding response in approved sources only", state: "pending" },
        ],
        sources: ["Policy Schedule MTR-COMP v3.2", "Claims SOP §4.2", "Document Checklist"],
      };
    case "fraud":
      return {
        steps: [
          { label: "Reviewing advisory fraud signals · last 7 days", state: "done" },
          { label: "Comparing pattern clusters", state: "done" },
          { label: "Preparing evidence summary for reviewer", state: "active" },
        ],
        sources: ["Advisory Signal Log", "Claims Work Queue"],
        workflow: "Human investigation required",
      };
    case "draft":
      return {
        steps: [
          { label: "Reviewing claim CLM-2025-00928", state: "done" },
          { label: "Checking approved communication templates", state: "done" },
          { label: "Preparing customer-safe draft", state: "active" },
          { label: "Awaiting officer approval before send", state: "pending" },
        ],
        sources: ["Customer Comms Template Library", "Claim CLM-2025-00928"],
        workflow: "Officer approval",
      };
    case "route":
      return {
        steps: [
          { label: "Reading current workflow state", state: "done" },
          { label: "Preparing supervisor approval task", state: "active" },
        ],
        sources: ["Workflow Ruleset", "Approval Matrix"],
        workflow: "Supervisor review",
      };
    default:
      return {
        steps: [
          { label: "Interpreting request", state: "done" },
          { label: "Assembling operational context", state: "active" },
        ],
        sources: ["Workspace context"],
      };
  }
}

function makeAIResponse(intent: ReturnType<typeof classifyIntent>, q: string): { render: () => ReactNode; sources: string[] } {
  const sources = stepsForIntent(intent).sources;
  switch (intent) {
    case "claims":
      return {
        sources,
        render: () => (
          <>
            <p>Here are today's higher-risk claims ranked by SLA proximity and advisory risk signals. Final triage remains with the reviewing officer.</p>
            <InsightCard
              title="High-risk claims · today"
              tone="warning"
              items={[
                { label: "CLM-1064 · Samuel Otieno · third-party bodily injury", meta: "SLA breached · High" },
                { label: "CLM-1048 · Aisha Nakato · rear-end collision", meta: "SLA at risk · Medium" },
                { label: "CLM-1071 · Fatima Suleiman · total loss, fire", meta: "Advisory signal" },
              ]}
            />
          </>
        ),
      };
    case "policy":
      return {
        sources,
        render: () => (
          <>
            <p>Across active claims, the most frequently missing documents this week are:</p>
            <InsightCard
              title="Missing documents · rolling 7 days"
              items={[
                { label: "Police report", meta: "9 claims" },
                { label: "Repair quotation", meta: "6 claims" },
                { label: "Driving permit copy", meta: "4 claims" },
              ]}
            />
            <p className="text-[12.5px] text-stone">Grounded in approved Claims SOP §4.2 and the current Document Checklist. I will not answer coverage questions without an approved source.</p>
          </>
        ),
      };
    case "fraud":
      return {
        sources,
        render: () => (
          <>
            <p>Advisory fraud signals increased by <b>3</b> this week, driven mainly by duplicate-claimant patterns across WhatsApp intake. These are potential anomalies — human review is required before any action.</p>
            <InsightCard
              title="Signal categories"
              tone="violet"
              items={[
                { label: "Duplicate claimant pattern", meta: "2 signals" },
                { label: "Unusual damage geometry vs. narrative", meta: "1 signal" },
                { label: "Repeated repair vendor", meta: "1 signal" },
              ]}
            />
          </>
        ),
      };
    case "draft":
      return {
        sources,
        render: () => (
          <>
            <p>Prepared a customer-safe update for claim <b>CLM-2025-00928</b>. Review before it is sent — nothing has been dispatched.</p>
            <div className="rounded-2xl border border-cloud bg-white/80 p-4 text-[13px] leading-[20px] text-slate2">
              <div className="text-[11px] uppercase tracking-[0.14em] text-stone mb-1">Draft · pending approval</div>
              Dear customer, thank you for your patience on claim CLM-2025-00928. Our review is underway and we currently require the police report to complete assessment. Kindly share it via WhatsApp at your convenience. — Global Insurance Co.
            </div>
            <ApprovalCard
              title="Send this customer update"
              summary="Message will only be dispatched after an officer approves. No reserves, internal notes, or fraud indicators are included."
            />
          </>
        ),
      };
    case "route":
      return {
        sources,
        render: () => (
          <>
            <p>Ready to route this to a supervisor. This only advances the workflow — it does not settle, deny, repudiate, or pay the claim.</p>
            <ApprovalCard
              title="Route to Supervisor review"
              summary="Requesting supervisor approval on next workflow stage. Evidence bundle and source references are attached."
            />
          </>
        ),
      };
    default:
      return {
        sources,
        render: () => (
          <>
            <p>Understood. I'll help with claims, documents, customers, policies, workflows, and approvals across this workspace. Ask me something specific — for example, <em>"Show high-risk claims today"</em> or <em>"Draft a customer update for CLM-2025-00928"</em>.</p>
            <p className="text-[12.5px] text-stone">I only ground policy answers in approved sources, and I never make regulated decisions on your behalf.</p>
          </>
        ),
      };
  }
}

export default function AIConversations() {
  const { roleDef } = useUI();
  const [messages, setMessages] = useState<Msg[]>([]);
  const [input, setInput] = useState("");
  const [thinking, setThinking] = useState<null | { intent: ReturnType<typeof classifyIntent>; startedAt: number }>(null);
  const [progress, setProgress] = useState(0);
  const scrollRef = useRef<HTMLDivElement>(null);

  const hasContent = messages.length > 0 || input.length > 0 || thinking !== null;

  useEffect(() => {
    if (!thinking) return;
    setProgress(0);
    const total = stepsForIntent(thinking.intent).steps.length;
    let i = 0;
    const iv = setInterval(() => {
      i += 1;
      setProgress(i);
      if (i >= total) clearInterval(iv);
    }, 550);
    return () => clearInterval(iv);
  }, [thinking]);

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: "smooth" });
  }, [messages, thinking]);

  const contextSteps = useMemo<{ steps: ContextStep[]; sources: string[]; workflow?: string } | null>(() => {
    if (!thinking) return null;
    const base = stepsForIntent(thinking.intent);
    const steps = base.steps.map((s, i) => ({
      ...s,
      state: (i < progress ? "done" : i === progress ? "active" : "pending") as ContextStep["state"],
    }));
    return { ...base, steps };
  }, [thinking, progress]);

  function send() {
    const q = input.trim();
    if (!q) return;
    const uid = crypto.randomUUID();
    setMessages((m) => [...m, { id: uid, role: "user", text: q }]);
    setInput("");
    const intent = classifyIntent(q);
    setThinking({ intent, startedAt: Date.now() });
    const total = stepsForIntent(intent).steps.length;
    const delay = total * 550 + 200;
    setTimeout(() => {
      const resp = makeAIResponse(intent, q);
      setMessages((m) => [...m, { id: crypto.randomUUID(), role: "ai", render: resp.render, sources: resp.sources }]);
      setThinking(null);
      setProgress(0);
    }, delay);
  }

  return (
    <div className="min-h-[calc(100vh-8rem)] flex flex-col -mt-2">
      {/* Header greeting only when empty */}
      {!hasContent && (
        <div className="max-w-[780px] mx-auto w-full text-center py-10 animate-fade-in">
          <div className="inline-flex items-center gap-2 rounded-full bg-[hsl(var(--violet)/0.08)] px-3 py-1 text-[11px] uppercase tracking-[0.14em] text-violet2 mb-4">
            <Sparkles className="h-3 w-3" /> AI Conversations
          </div>
          <h1 className="text-[32px] leading-[40px] font-semibold text-navy tracking-tight">
            Good morning, {roleDef.name.split(" ")[0]}.
          </h1>
          <p className="text-[14px] text-stone mt-2">
            Ask about claims, policies, fraud signals, customer updates, or workflows. Every regulated decision remains with a human.
          </p>
        </div>
      )}

      {/* Starter metrics + prompts — fade out when composing */}
      <div
        className={
          "max-w-[780px] mx-auto w-full transition-all duration-300 " +
          (hasContent ? "opacity-0 -translate-y-2 pointer-events-none h-0 overflow-hidden" : "opacity-100 translate-y-0 mb-8")
        }
      >
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3 mb-5">
          {HERO_METRICS.map((m) => {
            const up = m.delta.startsWith("+");
            const flat = m.delta === "0";
            return (
              <div key={m.label} className="glass-subtle p-4 hover-lift">
                <div className="flex items-center gap-1.5 text-[11.5px] text-stone">
                  <m.icon className="h-3.5 w-3.5 text-info" />
                  {m.label}
                </div>
                <div className="mt-1.5 flex items-end justify-between">
                  <div className="text-[24px] leading-none font-semibold text-navy tracking-tight">{m.value}</div>
                  {!flat && (
                    <div className={`text-[11px] inline-flex items-center gap-0.5 ${up ? "text-warning" : "text-success"}`}>
                      {up ? <ArrowUpRight className="h-3 w-3" /> : <ArrowDownRight className="h-3 w-3" />} {m.delta}
                    </div>
                  )}
                </div>
              </div>
            );
          })}
        </div>
        <div className="flex flex-wrap gap-2 justify-center">
          {STARTERS.map((s) => (
            <button
              key={s}
              onClick={() => { setInput(s); setTimeout(send, 0); }}
              className="text-[12.5px] text-slate2 rounded-full border border-cloud bg-white/80 px-3 py-1.5 hover:bg-white hover-lift transition"
            >
              {s}
            </button>
          ))}
        </div>
      </div>

      {/* Conversation */}
      <div ref={scrollRef} className="flex-1 overflow-y-auto">
        <div className="max-w-[780px] mx-auto w-full space-y-6 pb-6">
          {messages.map((m) =>
            m.role === "user" ? (
              <UserMessageBubble key={m.id} text={m.text} />
            ) : (
              <AIMessageBubble key={m.id} sources={m.sources}>{m.render()}</AIMessageBubble>
            )
          )}
          {thinking && (
            <div className="flex items-center gap-2 text-[12.5px] text-violet2 animate-fade-in">
              <span className="h-2 w-2 rounded-full bg-violet2 animate-pulse" />
              Copilot is working…
            </div>
          )}
        </div>
      </div>

      {/* Context + composer */}
      <div className="max-w-[780px] mx-auto w-full space-y-2 pt-4 pb-2 sticky bottom-0 bg-transparent">
        {contextSteps && (
          <AIContextWindow steps={contextSteps.steps} sources={contextSteps.sources} workflow={contextSteps.workflow} />
        )}
        <Composer value={input} onChange={setInput} onSend={send} disabled={!!thinking} />
        <div className="text-[10.5px] text-stone text-center">
          IIP Copilot assists — it never settles, denies, repudiates, pays, or accuses fraud. Human reviewers approve regulated decisions.
        </div>
      </div>
    </div>
  );
}
