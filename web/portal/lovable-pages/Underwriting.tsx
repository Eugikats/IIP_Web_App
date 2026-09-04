"use client";
import Link from "next/link";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import { Sparkles, ClipboardCheck } from "lucide-react";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { useUnderwritingReviews, type UnderwritingCheckState } from "@/lib/lovable-parity/data/underwriting";

const checkTone: Record<UnderwritingCheckState, "success" | "warning" | "info"> = {
  Pass: "success",
  Attention: "warning",
  MissingInformation: "info",
};

const checkLabel: Record<UnderwritingCheckState, string> = {
  Pass: "Pass",
  Attention: "Attention",
  MissingInformation: "Missing info",
};

export default function Underwriting() {
  const { reviews, loading, error } = useUnderwritingReviews();

  return (
    <>
      <SectionHeader eyebrow="Underwriting Intelligence" title="Submission reviews" sub="Broker and direct submissions summarised for underwriters. AI drafts questions and highlights gaps — underwriters decide acceptance, rejection, and premium." />
      <div className="mb-4"><GuardrailBanner text="AI does not make final underwriting acceptance or rejection. It summarises, recommends, drafts questions, and escalates." /></div>

      {loading ? (
        <GlassCard><div className="p-6 text-center text-[13px] text-muted">Loading submission reviews…</div></GlassCard>
      ) : reviews.length === 0 ? (
        <GlassCard>
          <div className="p-10 text-center flex flex-col items-center gap-2">
            <ClipboardCheck className="h-6 w-6 text-muted" />
            <div className="text-ink font-medium">No submissions in review</div>
            <div className="text-[12.5px] text-muted max-w-md">
              {error
                ? error
                : "Submission intake is not configured yet. When a broker/direct submission source is connected, advisory rule-engine reviews appear here — no sample submissions are shown."}
            </div>
          </div>
        </GlassCard>
      ) : (
        <div className="space-y-3">
          {reviews.map((r) => (
            <GlassCard key={r.submissionId} className="hover-lift">
              <div className="flex items-start justify-between gap-4">
                <div className="min-w-0">
                  <div className="text-[15px] font-semibold text-ink">{r.submissionId} · {r.applicant}</div>
                  <div className="mt-2 space-y-1.5">
                    {r.checks.map((c) => (
                      <div key={c.checkId} className="flex items-start gap-2">
                        <Badge tone={checkTone[c.state]}>{checkLabel[c.state]}</Badge>
                        <div className="text-[12.5px] text-ink-soft"><span className="text-ink font-medium">{c.label}:</span> {c.detail}</div>
                      </div>
                    ))}
                  </div>
                </div>
                <div className="flex shrink-0 flex-col items-end gap-2"><Badge tone="violet">Advisory only</Badge><Link href="/app/work-items" className="text-[11.5px] font-medium text-info">Open Review & Approvals</Link></div>
              </div>

              {r.draftedQuestions.length > 0 ? (
                <div className="mt-3 rounded-xl border border-line bg-white/80 p-3">
                  <div className="flex items-center gap-2 mb-1.5"><Sparkles className="h-3.5 w-3.5 text-info" /><div className="text-[11px] uppercase tracking-[0.14em] text-muted">Drafted clarifying questions — send only after approval</div></div>
                  <ul className="text-[12.5px] text-ink-soft space-y-1 list-disc pl-4">
                    {r.draftedQuestions.map((q, i) => <li key={i}>{q}</li>)}
                  </ul>
                </div>
              ) : null}
            </GlassCard>
          ))}
        </div>
      )}
    </>
  );
}
