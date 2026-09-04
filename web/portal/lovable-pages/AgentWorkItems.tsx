"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { AlertTriangle, CheckCircle2, FileSearch, History, Loader2, RotateCcw, ShieldCheck } from "lucide-react";
import { toast } from "sonner";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { Badge, ConfidenceBadge, GlassCard, SectionHeader, StatusBadge } from "@/components/iip/Primitives";
import { useAgentWorkItems, type AgentWorkItem } from "@/lib/agentWorkflows";

type QueueTab = "Requires review" | "Escalated" | "Completed";
type DetailTab = "Brief" | "Evidence" | "Run trace";

export default function AgentWorkItems({ embedded = false }: { embedded?: boolean }) {
  const { items, loading, error, decide } = useAgentWorkItems();
  const [queueTab, setQueueTab] = useState<QueueTab>("Requires review");
  const [detailTab, setDetailTab] = useState<DetailTab>("Brief");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);

  const filtered = useMemo(() => items.filter((item) => {
    if (queueTab === "Escalated") return item.status === "Escalated";
    if (queueTab === "Completed") return ["Completed", "ReturnedForCorrection", "Cancelled"].includes(item.status);
    return ["RequiresReview", "InProgress"].includes(item.status);
  }), [items, queueTab]);

  useEffect(() => {
    if (!selectedId || !filtered.some((item) => item.workItemId === selectedId)) setSelectedId(filtered[0]?.workItemId ?? null);
  }, [filtered, selectedId]);

  const selected = items.find((item) => item.workItemId === selectedId) ?? null;

  async function takeDecision(decision: AgentWorkItem["decision"], success: string) {
    if (!selected || !decision) return;
    if (reason.trim().length < 3) { toast.error("Record a review reason for the audit trail."); return; }
    setBusy(true);
    const result = await decide(selected.workItemId, decision, reason.trim());
    setBusy(false);
    if (result.ok) { toast.success(success); setReason(""); }
    else toast.error(result.message);
  }

  return (
    <>
      {!embedded ? <>
        <SectionHeader eyebrow="Agent Operations · Work Items" title="Human review workspace" sub="Inspect prepared work, verify evidence, and record explicit human handoffs without crossing regulated decision boundaries." />
        <div className="mb-5"><GuardrailBanner text="Accepting a work item accepts the prepared artifact only. It does not approve a claim, settlement, payment, policy, premium, underwriting outcome, complaint response, fraud verdict, or regulatory filing." /></div>
      </> : null}

      <div className="mb-4 flex flex-wrap gap-2">
        {(["Requires review", "Escalated", "Completed"] as QueueTab[]).map((tab) => (
          <button key={tab} type="button" onClick={() => setQueueTab(tab)} className={`rounded-full border px-3 py-1.5 text-[11.5px] font-medium focus-ring ${queueTab === tab ? "border-ink bg-ink text-white" : "border-line bg-white/70 text-ink-soft"}`}>
            {tab} <span className="ml-1 opacity-70">{tab === "Escalated" ? items.filter((item) => item.status === "Escalated").length : tab === "Completed" ? items.filter((item) => ["Completed", "ReturnedForCorrection", "Cancelled"].includes(item.status)).length : items.filter((item) => ["RequiresReview", "InProgress"].includes(item.status)).length}</span>
          </button>
        ))}
      </div>

      {loading ? (
        <GlassCard><div className="flex items-center justify-center gap-2 py-14 text-sm text-muted"><Loader2 className="h-4 w-4 animate-spin" /> Loading review queue…</div></GlassCard>
      ) : filtered.length === 0 ? (
        <GlassCard><div className="py-16 text-center"><ShieldCheck className="mx-auto h-7 w-7 text-success" /><div className="mt-3 font-medium text-ink">No work items in this queue</div><div className="mt-1 text-[12.5px] text-muted">{error ?? "Items appear here only when a real workflow reaches a human-review node."}</div></div></GlassCard>
      ) : (
        <div className="grid min-h-[650px] gap-4 xl:grid-cols-[360px_minmax(0,1fr)]">
          <GlassCard className="!p-2 overflow-hidden">
            <div className="px-3 py-2 text-[10.5px] uppercase tracking-[0.14em] text-muted">Queue · {filtered.length}</div>
            <div className="max-h-[610px] space-y-1 overflow-y-auto">
              {filtered.map((item) => (
                <button key={item.workItemId} type="button" onClick={() => setSelectedId(item.workItemId)} className={`w-full rounded-2xl border p-3 text-left transition-all focus-ring ${selectedId === item.workItemId ? "border-violet/30 bg-violet/10" : "border-transparent hover:border-line hover:bg-white/70"}`}>
                  <div className="flex items-start justify-between gap-2"><span className="text-[12.5px] font-semibold leading-4 text-ink">{item.title}</span><StatusBadge status={item.status} /></div>
                  <div className="mt-2 text-[11.5px] text-ink-soft">{item.businessReferenceType} · <span className="font-medium text-ink">{item.businessReferenceId}</span></div>
                  <div className="mt-2 flex items-center justify-between gap-2 text-[10.5px] text-muted"><span className="truncate">{item.workflowKey}</span><span>{new Date(item.createdAt).toLocaleString()}</span></div>
                </button>
              ))}
            </div>
          </GlassCard>

          {selected ? (
            <div className="min-w-0 space-y-4">
              <GlassCard className="!p-5">
                <div className="flex flex-wrap items-start justify-between gap-4">
                  <div>
                    <div className="flex flex-wrap items-center gap-2"><StatusBadge status={selected.status} /><Badge tone="violet">Human accountable</Badge></div>
                    <h2 className="mt-3 text-xl font-semibold text-ink">{selected.title}</h2>
                    <div className="mt-1 text-[12px] text-muted">{selected.businessReferenceType} · {selected.businessReferenceId} · {selected.nodeKey}</div>
                  </div>
                  <Link href={`/app/runs/${selected.runId}`} className="inline-flex items-center gap-2 rounded-xl border border-line bg-white px-3 py-2 text-[11.5px] font-medium text-ink focus-ring"><History className="h-3.5 w-3.5" /> Open run debugger</Link>
                </div>
                <div className="mt-5 flex gap-1 border-b border-line">
                  {(["Brief", "Evidence", "Run trace"] as DetailTab[]).map((tab) => <button key={tab} type="button" onClick={() => setDetailTab(tab)} className={`border-b-2 px-3 py-2 text-[11.5px] font-medium ${detailTab === tab ? "border-violet text-ink" : "border-transparent text-muted"}`}>{tab}</button>)}
                </div>

                {detailTab === "Brief" ? (
                  <div className="py-5">
                    <div className="rounded-2xl border border-violet/20 bg-violet/5 p-4">
                      <div className="text-[10.5px] uppercase tracking-[0.14em] text-violet">Review instructions</div>
                      <p className="mt-2 text-[13px] leading-6 text-ink">{selected.reviewInstructions}</p>
                    </div>
                    <div className="mt-4 grid gap-3 sm:grid-cols-2">
                      <div className="rounded-2xl border border-line bg-white/60 p-4"><div className="text-[10.5px] uppercase tracking-[0.12em] text-muted">Workflow</div><div className="mt-2 text-[12.5px] font-medium text-ink">{selected.workflowKey}</div></div>
                      <div className="rounded-2xl border border-line bg-white/60 p-4"><div className="text-[10.5px] uppercase tracking-[0.12em] text-muted">Evidence references</div><div className="mt-2 text-[12.5px] font-medium text-ink">{selected.evidence.length}</div></div>
                    </div>
                  </div>
                ) : null}

                {detailTab === "Evidence" ? (
                  <div className="space-y-2 py-5">
                    {selected.evidence.map((evidence, index) => (
                      <div key={`${evidence.sourceType}-${evidence.sourceId}-${index}`} className="flex items-start gap-3 rounded-2xl border border-line bg-white/65 p-4">
                        <span className="grid h-9 w-9 shrink-0 place-items-center rounded-xl bg-info/10 text-info"><FileSearch className="h-4 w-4" /></span>
                        <div className="min-w-0 flex-1"><div className="text-[12.5px] font-medium text-ink">{evidence.displayLabel ?? evidence.sourceId}</div><div className="mt-1 break-all font-mono text-[10.5px] text-muted">{evidence.sourceType} · {evidence.sourceId}{evidence.pageNumber ? ` · page ${evidence.pageNumber}` : ""}</div></div>
                        {typeof evidence.confidence === "number" ? <ConfidenceBadge value={evidence.confidence} /> : <Badge tone="neutral">Confidence unavailable</Badge>}
                      </div>
                    ))}
                    {selected.evidence.length === 0 ? <div className="rounded-2xl border border-dashed border-line p-8 text-center text-[12px] text-muted">No evidence references were attached. Return this prepared work if grounding is required.</div> : null}
                  </div>
                ) : null}

                {detailTab === "Run trace" ? (
                  <div className="grid gap-3 py-5 sm:grid-cols-2">
                    <div className="rounded-2xl border border-line bg-white/65 p-4"><div className="text-[10.5px] uppercase tracking-[0.12em] text-muted">Run ID</div><div className="mt-2 break-all font-mono text-[11px] text-ink">{selected.runId}</div></div>
                    <div className="rounded-2xl border border-line bg-white/65 p-4"><div className="text-[10.5px] uppercase tracking-[0.12em] text-muted">Correlation ID</div><div className="mt-2 break-all font-mono text-[11px] text-ink">{selected.correlationId}</div></div>
                    <div className="rounded-2xl border border-line bg-white/65 p-4 sm:col-span-2"><div className="text-[10.5px] uppercase tracking-[0.12em] text-muted">Human boundary</div><div className="mt-2 text-[12px] leading-5 text-ink-soft">This review records acceptance or correction of an AI-prepared artifact only. Consequential action remains in the dedicated approval and execution services.</div></div>
                  </div>
                ) : null}
              </GlassCard>

              {selected.status === "RequiresReview" || selected.status === "InProgress" || selected.status === "Escalated" ? (
                <GlassCard className="!p-5">
                  <label className="text-[11.5px] font-medium text-ink">Audit reason<textarea value={reason} onChange={(event) => setReason(event.target.value)} className="mt-2 min-h-20 w-full rounded-xl border border-line bg-white px-3 py-2.5 text-[12.5px] text-ink placeholder:text-muted focus-ring" placeholder="Record what you checked and why you are taking this handoff." /></label>
                  <div className="mt-4 flex flex-wrap justify-end gap-2">
                    <button type="button" disabled={busy} onClick={() => void takeDecision("Escalate", "Work item escalated for human review.")} className="inline-flex items-center gap-2 rounded-xl border border-warning/30 bg-white px-3 py-2.5 text-[11.5px] font-medium text-warning-text disabled:opacity-50 focus-ring"><AlertTriangle className="h-4 w-4" /> Escalate</button>
                    <button type="button" disabled={busy} onClick={() => void takeDecision("ReturnForCorrection", "Prepared work returned for correction.")} className="inline-flex items-center gap-2 rounded-xl border border-line bg-white px-3 py-2.5 text-[11.5px] font-medium text-ink disabled:opacity-50 focus-ring"><RotateCcw className="h-4 w-4" /> Return for correction</button>
                    <button type="button" disabled={busy} onClick={() => void takeDecision("AcceptPreparedWork", "Prepared work accepted by the reviewer.")} className="inline-flex items-center gap-2 rounded-xl gradient-primary px-4 py-2.5 text-[11.5px] font-medium text-white disabled:opacity-50 focus-ring">{busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <CheckCircle2 className="h-4 w-4" />} Accept prepared work</button>
                  </div>
                </GlassCard>
              ) : null}
            </div>
          ) : null}
        </div>
      )}
    </>
  );
}
