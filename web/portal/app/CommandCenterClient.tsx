"use client";

import { BlockRenderer } from "@/components/blocks";
import { ErrorState, LoadingState } from "@/components/States";
import { useRole } from "@/components/RoleContext";
import { PromptBar, type ChatContextItem } from "@/components/PromptBar";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { commandCenterSuggestions, defaultCommandCenterSuggestions } from "@/lib/roleUiConfig";
import { useCommandCenterQuery } from "@/hooks/useCommandCenterQuery";
import Link from "next/link";
import { AlertTriangle, ArrowRight, Bot, ChevronDown, Clock, FileText, MessageSquareText, ShieldCheck, Sparkles, UserCheck } from "lucide-react";
import { type ReactNode, useEffect, useMemo, useRef, useState } from "react";

type Metrics = {
  totalCases: number;
  totalDocuments: number;
  averageCompletenessScore: number;
  knowledgeDocumentsApproved: number;
  assistantQuestionsAnswered: number;
  escalationsRequired: number;
  openClaims: number;
  newClaimsToday: number;
  incompleteClaims: number;
  pendingApprovals: number;
  openTasks: number;
  customerUpdatesPending: number;
  slaAtRisk: number;
  slaBreached: number;
};

export function CommandCenterClient() {
  const { role, userId, displayName, roleLabel, isLoadingContext } = useRole();
  const [metrics, setMetrics] = useState<Metrics | null>(null);
  const [metricsError, setMetricsError] = useState<string | null>(null);
  const [contextOpen, setContextOpen] = useState(false);
  const [question, setQuestion] = useState("");
  const endRef = useRef<HTMLDivElement>(null);

  const { turns, busy, error: queryError, send } = useCommandCenterQuery();

  useEffect(() => {
    let active = true;
    setMetricsError(null);
    setMetrics(null);

    if (isLoadingContext) {
      return () => {
        active = false;
      };
    }

    fetch(`${apiBaseUrl}/api/v1/command-center/metrics`, { headers: scopedHeaders(role, userId) })
      .then((response) => {
        if (!response.ok) {
          throw new Error(`Command Center metrics failed with HTTP ${response.status}.`);
        }
        return response.json();
      })
      .then((data) => {
        if (active) setMetrics(data as Metrics);
      })
      .catch((err) => {
        if (active) setMetricsError(err instanceof Error ? err.message : "Command Center metrics failed.");
      });

    return () => {
      active = false;
    };
  }, [isLoadingContext, role, userId]);

  // Auto-scroll to latest turn
  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: "smooth", block: "nearest" });
  }, [turns]);

  const suggestions = useMemo(() => commandCenterSuggestions[role] ?? defaultCommandCenterSuggestions, [role]);
  const firstName = displayName.split(" ")[0] || roleLabel;

  const promptContext: ChatContextItem[] = useMemo(() => [
    { label: "Scope", value: "Active tenant and workspace" },
    { label: "Role", value: roleLabel },
    { label: "Grounding", value: "Tenant/workspace operational metrics" },
  ], [roleLabel]);

  function handleSend() {
    const text = question.trim();
    if (!text) return;
    setQuestion("");
    send(text);
  }

  return (
    <div className="flex flex-col gap-5 pb-4">
      {/* Header and Operational Context popover */}
      <div className="flex flex-wrap items-center justify-between gap-3 relative">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-ink">Command Center</h1>
          <p className="mt-1 text-sm text-muted">Chat-first operational workspace for {roleLabel}.</p>
        </div>
        <div className="relative">
          <button
            onClick={() => setContextOpen(!contextOpen)}
            className={`flex items-center gap-2 rounded-xl border px-3 py-2 text-sm font-semibold transition-colors ${
              contextOpen ? "border-primary-border bg-primary-soft text-primary" : "border-line bg-surface text-ink hover:bg-surface-soft"
            }`}
          >
            Operational context <ChevronDown className={`h-4 w-4 transition-transform ${contextOpen ? "rotate-180" : ""}`} />
          </button>

          {contextOpen && metrics && (
            <div className="absolute right-0 top-full z-50 mt-2 w-[520px] max-w-[calc(100vw-2rem)] rounded-2xl border border-line bg-surface p-4 shadow-popover">
              <div className="mb-3 flex items-center justify-between">
                <h2 className="text-sm font-bold text-ink">Backend context</h2>
                <span className="rounded-full border border-line bg-mist px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider text-muted">
                  Backend-owned
                </span>
              </div>
              <div className="grid gap-2 sm:grid-cols-2">
                <ContextMetric label="Open claims" value={metrics.openClaims} />
                <ContextMetric label="New today" value={metrics.newClaimsToday} />
                <ContextMetric label="Incomplete claims" value={metrics.incompleteClaims} tone={metrics.incompleteClaims > 0 ? "warning" : "default"} />
                <ContextMetric label="SLA at risk" value={metrics.slaAtRisk} tone={metrics.slaAtRisk > 0 ? "warning" : "default"} />
                <ContextMetric label="Pending approvals" value={metrics.pendingApprovals} tone={metrics.pendingApprovals > 0 ? "warning" : "default"} />
                <ContextMetric label="Customer updates" value={metrics.customerUpdatesPending} />
              </div>
              <p className="mt-3 text-xs leading-5 text-muted">
                These metrics frame the chat briefing. Human decisions and governed workflows remain in their owner screens.
              </p>
            </div>
          )}
        </div>
      </div>

      {metricsError ? <ErrorState message={metricsError} /> : null}
      {!metrics && !metricsError && <LoadingState label="Loading Command Center metrics..." />}

      {/* ── Chat Canvas ─────────────────────────────────────────── */}
      <section className="flex min-h-[calc(100vh-220px)] flex-col rounded-2xl border border-line bg-surface shadow-panel">
        {/* Transcript */}
        <div className="min-h-[520px] flex-1 space-y-5 overflow-y-auto px-5 py-6">
          {turns.length === 0 ? (
            /* Empty / welcome state */
            <div className="flex min-h-[500px] flex-col items-center justify-center text-center">
              <WelcomeCard firstName={firstName} metrics={metrics} />
              {/* Suggestion chips */}
              {suggestions.length > 0 && (
                <div className="mt-6 flex max-w-3xl flex-wrap justify-center gap-2">
                  {suggestions.map((s) => (
                    <button
                      key={s}
                      onClick={() => send(s)}
                      className="inline-flex items-center gap-1.5 rounded-full border border-line bg-surface px-4 py-2 text-xs font-semibold text-ink transition-all hover:-translate-y-0.5 hover:border-primary-border hover:shadow-sm"
                    >
                      <Sparkles className="h-3.5 w-3.5 text-primary" />
                      {s}
                    </button>
                  ))}
                </div>
              )}
            </div>
          ) : (
            turns.map((turn) =>
              turn.role === "user" ? (
                /* User bubble — right-aligned */
                <div key={turn.id} className="flex justify-end">
                  <div className="max-w-[80%] rounded-2xl rounded-tr-md bg-primary px-4 py-2.5 text-sm leading-relaxed text-inverse">
                    {turn.text}
                  </div>
                </div>
              ) : (
                /* Assistant turn — left-aligned block stream */
                <div key={turn.id} className="flex gap-2.5">
                  <div className="mt-0.5 flex h-7 w-7 flex-shrink-0 items-center justify-center rounded-full border border-ai-border bg-ai-soft">
                    <Bot className="h-3.5 w-3.5 text-ai" />
                  </div>
                  <div className="min-w-0 flex-1 space-y-3">
                    {turn.pending ? (
                      <div className="inline-flex items-center gap-2 rounded-2xl rounded-tl-md border border-ai-border bg-ai-surface px-4 py-2.5 text-sm text-muted">
                        <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-ai" />
                        IIP is preparing your command center...
                      </div>
                    ) : (
                      /* Each block gets its own renderer — no single text bubble */
                      turn.blocks.map((block, idx) => (
                        <BlockRenderer key={`${turn.id}-block-${idx}`} block={block} />
                      ))
                    )}
                  </div>
                </div>
              )
            )
          )}
          <div ref={endRef} />
        </div>

        {/* Query error */}
        {queryError ? (
          <div className="px-4 pb-2">
            <ErrorState message={queryError} />
          </div>
        ) : null}

        {/* Pinned input */}
        <PromptBar
          value={question}
          onChange={setQuestion}
          onSubmit={handleSend}
          busy={busy}
          placeholder="Ask about claims, workflow pressure, approvals, or customer updates..."
          context={promptContext}
        />
      </section>
    </div>
  );
}

function WelcomeCard({ firstName, metrics }: { firstName: string; metrics: Metrics | null }) {
  return (
    <div className="flex w-full max-w-4xl flex-col items-center justify-center rounded-2xl border border-ai-border bg-ai-soft px-6 py-8 text-center">
      <div className="mb-4 flex h-14 w-14 items-center justify-center rounded-2xl bg-primary text-white shadow-sm">
        <Sparkles className="h-7 w-7" />
      </div>
      <h2 className="text-2xl font-bold tracking-tight text-ink">Good morning, {firstName}</h2>
      <p className="mt-3 text-sm leading-relaxed text-muted">
        I&apos;m monitoring your claims operations and can help you review SLA risk, incomplete claims, approvals, customer updates, and workflow pressure.
      </p>
      {metrics ? (
        <>
          <div className="mt-6 grid w-full gap-2 sm:grid-cols-2 lg:grid-cols-3">
            <BriefingChip icon={<FileText className="h-4 w-4" />} label="Open claims" value={metrics.openClaims} />
            <BriefingChip icon={<Clock className="h-4 w-4" />} label="New today" value={metrics.newClaimsToday} />
            <BriefingChip icon={<AlertTriangle className="h-4 w-4" />} label="SLA at risk" value={metrics.slaAtRisk} tone={metrics.slaAtRisk > 0 ? "warning" : "default"} />
            <BriefingChip icon={<FileText className="h-4 w-4" />} label="Incomplete" value={metrics.incompleteClaims} tone={metrics.incompleteClaims > 0 ? "warning" : "default"} />
            <BriefingChip icon={<UserCheck className="h-4 w-4" />} label="Approvals" value={metrics.pendingApprovals} tone={metrics.pendingApprovals > 0 ? "warning" : "default"} />
            <BriefingChip icon={<MessageSquareText className="h-4 w-4" />} label="Customer updates" value={metrics.customerUpdatesPending} />
          </div>
          <div className="mt-6 w-full rounded-2xl border border-line bg-surface p-4 text-left">
            <div className="mb-3 flex items-center gap-2">
              <Bot className="h-4 w-4 text-ai" />
              <p className="text-sm font-semibold text-ink">Daily operational briefing</p>
            </div>
            <div className="space-y-2">
              <BriefingLink href="/claims" title="SLA pressure" detail={`${metrics.slaAtRisk} at risk, ${metrics.slaBreached} breached`} />
              <BriefingLink href="/document-checker" title="Missing evidence" detail={`${metrics.incompleteClaims} incomplete claims need document follow-up`} />
              <BriefingLink href="/approvals" title="Human review" detail={`${metrics.pendingApprovals} items are waiting for governed approval`} />
            </div>
          </div>
          <div className="mt-4 flex w-full items-start gap-2 rounded-2xl border border-line bg-surface px-4 py-3 text-left">
            <ShieldCheck className="mt-0.5 h-4 w-4 flex-shrink-0 text-primary" />
            <p className="text-xs leading-5 text-muted">
              Command Center prepares operational work. Final coverage, payment, denial, settlement, fraud, and complaint decisions stay with authorized humans.
            </p>
          </div>
        </>
      ) : null}
    </div>
  );
}

function ContextMetric({ label, value, tone = "default" }: { label: string; value: number; tone?: "default" | "warning" }) {
  return (
    <div className="flex items-center justify-between gap-3 rounded-xl border border-line bg-surface px-3 py-2">
      <span className="text-xs font-medium text-muted">{label}</span>
      <span className={`font-mono text-sm font-bold tabular-nums ${tone === "warning" ? "text-warning-text" : "text-ink"}`}>
        {value.toLocaleString()}
      </span>
    </div>
  );
}

function BriefingChip({ icon, label, value, tone = "default" }: { icon: ReactNode; label: string; value: number; tone?: "default" | "warning" }) {
  return (
    <div className="flex items-center justify-between gap-3 rounded-xl border border-line bg-surface px-3 py-2 text-left">
      <div className={`flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-lg border ${tone === "warning" ? "border-warning-border bg-warning-soft text-warning-text" : "border-primary-border bg-primary-soft text-primary"}`}>
        {icon}
      </div>
      <div className="min-w-0 flex-1">
        <p className="truncate text-xs font-medium text-muted">{label}</p>
        <p className="font-mono text-lg font-bold leading-tight text-ink">{value.toLocaleString()}</p>
      </div>
    </div>
  );
}

function BriefingLink({ href, title, detail }: { href: string; title: string; detail: string }) {
  return (
    <Link href={href} className="group flex items-center gap-3 rounded-xl border border-line bg-surface-soft px-3 py-2 transition-colors hover:bg-surface-muted">
      <div className="min-w-0 flex-1">
        <p className="text-sm font-semibold text-ink">{title}</p>
        <p className="truncate text-xs text-muted">{detail}</p>
      </div>
      <ArrowRight className="h-4 w-4 flex-shrink-0 text-muted transition-transform group-hover:translate-x-0.5" />
    </Link>
  );
}
