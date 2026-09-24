"use client";
import { useMemo, useState } from "react";
import { SectionHeader, GlassCard, Badge, StatusBadge } from "@/components/iip/Primitives";
import { StatusPill, type PillTone } from "@/components/ui/StatusPill";
import { Link } from "@/components/iip/NextRouterCompat";
import { toast } from "sonner";
import { useApprovals } from "@/lib/lovable-parity/data/approvals";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { Field } from "@/components/ui/Input";
import { LoadingSkeleton, EmptyStateSurface } from "@/components/ui/StateSurfaces";
import { claimReference, formatAgeFromHours, humanizeIdentifier, isGuid } from "@/lib/format";
import { usePeople } from "@/lib/people";

// What each approval is for, in the words of iip_workflow_legibility_contract.md section 10.7. The
// API sends the raw type ("SupervisorReview"); a person should read what is being asked of them.
const APPROVAL_TYPE_LABELS: Record<string, string> = {
  SupervisorReview: "Supervisor review",
  HumanReview: "Human review",
  RequestMissingDocuments: "Request missing documents",
  MarkReadyForReview: "Mark ready for review",
  FollowUpCustomer: "Follow up with customer",
};

function approvalTitle(type: string) {
  if (APPROVAL_TYPE_LABELS[type]) return APPROVAL_TYPE_LABELS[type];
  return /\s/.test(type) ? type : humanizeIdentifier(type);
}

const RISK_TONE: Record<string, PillTone> = { low: "success", medium: "warning", high: "danger" };

export default function Workflows({ embedded = false }: { embedded?: boolean }) {
  const { approvals, loading, error, parityMode, approve, reject, escalate, override, executeNextStep } = useApprovals();
  const { describe } = usePeople();
  const [tab, setTab] = useState<"All approvals" | "High risk" | "Completed">("All approvals");
  const [busyId, setBusyId] = useState<string | null>(null);
  const [reason, setReason] = useState("");
  const reasonReady = reason.trim().length >= 3;

  const filtered = useMemo(() =>
    tab === "High risk" ? approvals.filter((a) => a.risk.toLowerCase() === "high" && !a.executedAt)
    : tab === "Completed" ? approvals.filter((a) => Boolean(a.executedAt) || ["Approved", "Rejected", "Completed"].includes(a.status))
    : approvals, [approvals, tab]);

  async function run(label: string, approvalId: string, action: () => Promise<{ ok: boolean; message?: string }>) {
    if (!reasonReady) { toast.error("Record what you checked and why before taking an approval action."); return; }
    setBusyId(approvalId);
    try {
      const result = await action();
      if (result.ok) { toast.success(label); setReason(""); }
      else toast.error(result.message ?? `${label} failed.`);
    } finally {
      setBusyId(null);
    }
  }

  const can = (actions: { action: string }[], action: string) =>
    parityMode || actions.some((candidate) => candidate.action === action);

  const secondary = "dls-focus-outline rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-1.5 text-[12px] font-semibold text-dls-text-primary hover:bg-dls-bg-page disabled:cursor-not-allowed disabled:opacity-50";

  return (
    <>
      {!embedded ? <>
        <SectionHeader eyebrow="Workflow & Approvals" title="Human review queue" sub="Every step below advances a workflow. Regulated outcomes stay with a named person." />
        <div className="mb-4"><GuardrailBanner text="Approval actions update workflow state only. Final regulated decisions (settle, deny, pay, bind, accuse fraud) require an explicit human owner." /></div>
      </> : null}

      <div className="mb-4 flex flex-wrap gap-1.5">
        {(["All approvals", "High risk", "Completed"] as const).map((t) => (
          <button
            key={t}
            type="button"
            onClick={() => setTab(t)}
            className={`dls-focus-outline rounded-dls-chip border px-3 py-1.5 text-[12px] font-semibold transition-colors duration-dls-fast ease-dls ${tab === t ? "border-dls-primary bg-dls-primary text-dls-text-inverse" : "border-dls-border-input bg-dls-surface text-dls-text-secondary hover:border-dls-primary"}`}
          >
            {t}
          </button>
        ))}
      </div>

      <div className="mb-4">
        <Field label="Decision reason" htmlFor="decision-reason">
          <textarea
            id="decision-reason"
            value={reason}
            onChange={(event) => setReason(event.target.value)}
            placeholder="Note what you checked and why you are approving, rejecting or escalating. This is saved with your decision."
            className="min-h-16 w-full rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-2.5 text-[12.5px] text-dls-text-primary placeholder:text-dls-text-muted focus:outline-none focus:border-dls-primary focus:ring-[3px] focus:ring-dls-primary/[0.12]"
          />
        </Field>
      </div>

      {loading ? (
        <GlassCard><LoadingSkeleton rows={4} /></GlassCard>
      ) : approvals.length === 0 ? (
        <GlassCard><EmptyStateSurface message={error ?? "Approvals appear here when the AI or a colleague sends work for a person to decide."} /></GlassCard>
      ) : filtered.length === 0 ? (
        <GlassCard><EmptyStateSurface message="Nothing matches this filter." /></GlassCard>
      ) : (
        <GlassCard className="overflow-hidden !p-0">
          <div className="hidden grid-cols-12 gap-3 border-b border-dls-border-subtle px-4 py-3 text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted md:grid">
            <div className="col-span-5">What needs deciding</div>
            <div className="col-span-2">Requested by</div>
            <div className="col-span-2">Reviewer</div>
            <div className="col-span-1">Risk</div>
            <div className="col-span-2">Waiting</div>
          </div>
          {filtered.map((q) => {
            const risk = (q.risk || "").trim();
            const busy = busyId === q.id;
            const disabled = busy || !reasonReady;
            const hint = reasonReady ? undefined : "Add a decision reason above first";
            const anyAction = ["approveApproval", "executeApprovedNextStep", "rejectApproval", "escalateApproval", "overrideApproval"].some((a) => can(q.allowedActions, a));
            return (
              <div key={q.id} className="border-b border-dls-border-subtle px-4 py-4 last:border-b-0">
                <div className="grid gap-3 md:grid-cols-12">
                  <div className="min-w-0 md:col-span-5">
                    <div className="text-[13.5px] font-semibold text-dls-text-primary">{approvalTitle(q.title)}</div>
                    {q.claim ? (
                      isGuid(q.claim) ? (
                        <Link to={`/app/claims/${q.claim}`} className="dls-focus-outline mt-0.5 inline-block rounded-dls-tight text-[12px] font-semibold text-dls-primary hover:underline">
                          Claim {claimReference(q.claim)}
                        </Link>
                      ) : (
                        <div className="mt-0.5 text-[12px] text-dls-text-muted">Claim {q.claim}</div>
                      )
                    ) : null}
                    {q.summary ? <div className="mt-1.5 text-[12.5px] leading-relaxed text-dls-text-secondary">{q.summary}</div> : null}
                    <div className="mt-2 flex flex-wrap items-center gap-1.5">
                      <StatusBadge status={q.status} />
                      {q.executedAt ? <Badge tone="success">Next step carried out</Badge> : null}
                    </div>
                  </div>
                  <div className="text-[12.5px] text-dls-text-secondary md:col-span-2">
                    <span className="text-[11px] text-dls-text-muted md:hidden">Requested by </span>
                    {describe(q.requester, "Not recorded")}
                  </div>
                  <div className="text-[12.5px] text-dls-text-secondary md:col-span-2">
                    <span className="text-[11px] text-dls-text-muted md:hidden">Reviewer </span>
                    {describe(q.reviewer, "Any supervisor")}
                  </div>
                  <div className="md:col-span-1">
                    {risk ? <StatusPill tone={RISK_TONE[risk.toLowerCase()] ?? "neutral"} label={humanizeIdentifier(risk)} /> : null}
                  </div>
                  <div className="text-[12.5px] tabular-nums text-dls-text-muted md:col-span-2">{formatAgeFromHours(q.ageHrs)}</div>
                </div>

                {anyAction ? (
                  <div className="mt-3 flex flex-wrap items-center justify-end gap-2">
                    {!reasonReady ? <span className="mr-auto text-[11.5px] text-dls-text-muted">Add a decision reason above to enable these.</span> : null}
                    {can(q.allowedActions, "approveApproval") ? (
                      <button type="button" title={hint} onClick={() => run("Approved", q.id, () => approve(q.id, reason.trim()))} disabled={disabled}
                        className="dls-focus-outline rounded-dls-control bg-dls-primary px-3.5 py-1.5 text-[12px] font-bold text-dls-text-inverse hover:bg-dls-primary-hover disabled:cursor-not-allowed disabled:opacity-50">
                        Approve
                      </button>
                    ) : null}
                    {can(q.allowedActions, "executeApprovedNextStep") ? (
                      <button type="button" title={hint} onClick={() => run("Next step carried out", q.id, () => executeNextStep(q.id, reason.trim()))} disabled={disabled} className={secondary}>
                        Carry out next step
                      </button>
                    ) : null}
                    {can(q.allowedActions, "rejectApproval") ? (
                      <button type="button" title={hint} onClick={() => run("Rejected", q.id, () => reject(q.id, reason.trim()))} disabled={disabled}
                        className="dls-focus-outline rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-1.5 text-[12px] font-semibold text-dls-danger-text hover:bg-dls-bg-page disabled:cursor-not-allowed disabled:opacity-50">
                        Reject
                      </button>
                    ) : null}
                    {can(q.allowedActions, "escalateApproval") ? (
                      <button type="button" title={hint} onClick={() => run("Escalated", q.id, () => escalate(q.id, reason.trim()))} disabled={disabled} className={secondary}>
                        Escalate
                      </button>
                    ) : null}
                    {can(q.allowedActions, "overrideApproval") ? (
                      <button type="button" title={hint} onClick={() => run("Override recorded", q.id, () => override(q.id, reason.trim()))} disabled={disabled} className={secondary}>
                        Override AI suggestion
                      </button>
                    ) : null}
                  </div>
                ) : (
                  <div className="mt-2 text-right text-[11.5px] text-dls-text-muted">No action available to you</div>
                )}
              </div>
            );
          })}
        </GlassCard>
      )}
    </>
  );
}
