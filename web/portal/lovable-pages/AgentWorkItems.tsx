"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { CheckCircle2, FileSearch, History, RotateCcw, ShieldCheck, TriangleAlert } from "lucide-react";
import { toast } from "sonner";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { Badge, ConfidenceBadge, GlassCard, SectionHeader, StatusBadge } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Field } from "@/components/ui/Input";
import { LoadingSkeleton, EmptyStateSurface } from "@/components/ui/StateSurfaces";
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
          <button
            key={tab}
            type="button"
            onClick={() => setQueueTab(tab)}
            className={`dls-focus-outline rounded-dls-chip border px-3 py-1.5 text-[11.5px] font-semibold transition-colors duration-dls-fast ease-dls ${queueTab === tab ? "border-dls-primary bg-dls-primary text-dls-text-inverse" : "border-dls-border-input bg-dls-surface text-dls-text-secondary hover:border-dls-primary"}`}
          >
            {tab} <span className="ml-1 opacity-80">{tab === "Escalated" ? items.filter((item) => item.status === "Escalated").length : tab === "Completed" ? items.filter((item) => ["Completed", "ReturnedForCorrection", "Cancelled"].includes(item.status)).length : items.filter((item) => ["RequiresReview", "InProgress"].includes(item.status)).length}</span>
          </button>
        ))}
      </div>

      {loading ? (
        <GlassCard><LoadingSkeleton rows={4} /></GlassCard>
      ) : filtered.length === 0 ? (
        <GlassCard>
          <div className="flex flex-col items-center gap-2 px-5 py-10 text-center">
            <ShieldCheck className="h-7 w-7 text-dls-success-dot" aria-hidden="true" />
            <EmptyStateSurface message={error ?? "Items appear here only when a real workflow reaches a human-review node."} />
          </div>
        </GlassCard>
      ) : (
        <div className="grid min-h-[650px] gap-4 xl:grid-cols-[360px_minmax(0,1fr)]">
          <GlassCard className="overflow-hidden !p-2">
            <div className="px-3 py-2 text-[10.5px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Queue · {filtered.length}</div>
            <div className="max-h-[610px] space-y-1 overflow-y-auto">
              {filtered.map((item) => (
                <button
                  key={item.workItemId}
                  type="button"
                  onClick={() => setSelectedId(item.workItemId)}
                  className={`dls-focus-outline w-full rounded-dls-card border p-3 text-left transition-colors duration-dls-fast ease-dls ${selectedId === item.workItemId ? "border-dls-primary-border bg-dls-primary-soft-alt" : "border-transparent hover:border-dls-border-subtle hover:bg-dls-bg-page"}`}
                >
                  <div className="flex items-start justify-between gap-2"><span className="text-[12.5px] font-bold leading-4 text-dls-text-primary">{item.title}</span><StatusBadge status={item.status} /></div>
                  <div className="mt-2 text-[11.5px] text-dls-text-secondary">{item.businessReferenceType} · <span className="font-semibold text-dls-text-primary">{item.businessReferenceId}</span></div>
                  <div className="mt-2 flex items-center justify-between gap-2 text-[10.5px] text-dls-text-muted"><span className="truncate">{item.workflowKey}</span><span>{new Date(item.createdAt).toLocaleString()}</span></div>
                </button>
              ))}
            </div>
          </GlassCard>

          {selected ? (
            <div className="min-w-0 space-y-4">
              <GlassCard className="!p-5">
                <div className="flex flex-wrap items-start justify-between gap-4">
                  <div>
                    <div className="flex flex-wrap items-center gap-2"><StatusBadge status={selected.status} /><Badge tone="neutral">Human accountable</Badge></div>
                    <h2 className="mt-3 text-xl font-bold text-dls-text-primary">{selected.title}</h2>
                    <div className="mt-1 text-[12px] text-dls-text-muted">{selected.businessReferenceType} · {selected.businessReferenceId} · {selected.nodeKey}</div>
                  </div>
                  <Link href={`/app/runs/${selected.runId}`} className="dls-focus-outline inline-flex items-center gap-2 rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-2 text-[11.5px] font-bold text-dls-text-primary hover:bg-dls-bg-page"><History className="h-3.5 w-3.5" aria-hidden="true" /> Open run debugger</Link>
                </div>
                <div className="mt-5 flex gap-1 border-b border-dls-border-subtle">
                  {(["Brief", "Evidence", "Run trace"] as DetailTab[]).map((tab) => (
                    <button
                      key={tab}
                      type="button"
                      onClick={() => setDetailTab(tab)}
                      className={`dls-focus-outline border-b-2 px-3 py-2 text-[11.5px] font-semibold ${detailTab === tab ? "border-dls-primary text-dls-text-primary" : "border-transparent text-dls-text-muted"}`}
                    >
                      {tab}
                    </button>
                  ))}
                </div>

                {detailTab === "Brief" ? (
                  <div className="py-5">
                    <div className="rounded-dls-card border border-dls-primary-border bg-dls-primary-soft-alt p-4">
                      <div className="text-[10.5px] font-bold uppercase tracking-[0.05em] text-dls-primary">Review instructions</div>
                      <p className="mt-2 text-[13px] leading-6 text-dls-text-primary">{selected.reviewInstructions}</p>
                    </div>
                    <div className="mt-4 grid gap-3 sm:grid-cols-2">
                      <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4"><div className="text-[10.5px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Workflow</div><div className="mt-2 text-[12.5px] font-semibold text-dls-text-primary">{selected.workflowKey}</div></div>
                      <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4"><div className="text-[10.5px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Evidence references</div><div className="mt-2 text-[12.5px] font-semibold text-dls-text-primary">{selected.evidence.length}</div></div>
                    </div>
                  </div>
                ) : null}

                {detailTab === "Evidence" ? (
                  <div className="space-y-2 py-5">
                    {selected.evidence.map((evidence, index) => (
                      <div key={`${evidence.sourceType}-${evidence.sourceId}-${index}`} className="flex items-start gap-3 rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4">
                        <span className="grid h-9 w-9 shrink-0 place-items-center rounded-dls-control bg-dls-info-tint text-dls-info-text"><FileSearch className="h-4 w-4" aria-hidden="true" /></span>
                        <div className="min-w-0 flex-1"><div className="text-[12.5px] font-semibold text-dls-text-primary">{evidence.displayLabel ?? evidence.sourceId}</div><div className="mt-1 break-all font-mono text-[10.5px] text-dls-text-muted">{evidence.sourceType} · {evidence.sourceId}{evidence.pageNumber ? ` · page ${evidence.pageNumber}` : ""}</div></div>
                        {typeof evidence.confidence === "number" ? <ConfidenceBadge value={evidence.confidence} /> : <Badge tone="neutral">Confidence unavailable</Badge>}
                      </div>
                    ))}
                    {selected.evidence.length === 0 ? <div className="rounded-dls-card border border-dashed border-dls-border-subtle p-8 text-center text-[12px] text-dls-text-muted">No sources were attached to this. Send it back if you need to see what it was based on.</div> : null}
                  </div>
                ) : null}

                {detailTab === "Run trace" ? (
                  <div className="grid gap-3 py-5 sm:grid-cols-2">
                    <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4"><div className="text-[10.5px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Run ID</div><div className="mt-2 break-all font-mono text-[11px] text-dls-text-primary">{selected.runId}</div></div>
                    <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4"><div className="text-[10.5px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Correlation ID</div><div className="mt-2 break-all font-mono text-[11px] text-dls-text-primary">{selected.correlationId}</div></div>
                    <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4 sm:col-span-2"><div className="text-[10.5px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Human boundary</div><div className="mt-2 text-[12px] leading-5 text-dls-text-secondary">This review records acceptance or correction of an AI-prepared artifact only. Consequential action remains in the dedicated approval and execution services.</div></div>
                  </div>
                ) : null}
              </GlassCard>

              {selected.status === "RequiresReview" || selected.status === "InProgress" || selected.status === "Escalated" ? (
                <GlassCard className="!p-5">
                  <Field label="Audit reason">
                    <textarea
                      value={reason}
                      onChange={(event) => setReason(event.target.value)}
                      className="min-h-20 w-full rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-2.5 text-[12.5px] text-dls-text-primary placeholder:text-dls-text-muted focus:outline-none focus:border-dls-primary focus:ring-[3px] focus:ring-dls-primary/[0.12]"
                      placeholder="Record what you checked and why you are taking this handoff."
                    />
                  </Field>
                  <div className="mt-4 flex flex-wrap justify-end gap-2">
                    <Button variant="secondary" icon={<TriangleAlert />} disabled={busy} onClick={() => void takeDecision("Escalate", "Work item escalated for human review.")} className="border-dls-warning-dot text-dls-warning-text">
                      Escalate
                    </Button>
                    <Button variant="secondary" icon={<RotateCcw />} disabled={busy} onClick={() => void takeDecision("ReturnForCorrection", "Prepared work returned for correction.")}>
                      Return for correction
                    </Button>
                    <Button variant="primary" icon={<CheckCircle2 />} disabled={busy} onClick={() => void takeDecision("AcceptPreparedWork", "Prepared work accepted by the reviewer.")}>
                      Accept prepared work
                    </Button>
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
