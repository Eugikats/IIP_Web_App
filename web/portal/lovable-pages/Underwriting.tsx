"use client";
import Link from "next/link";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import { Sparkles } from "lucide-react";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { LoadingSkeleton, ErrorStateSurface, EmptyStateSurface } from "@/components/ui/StateSurfaces";
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
        <GlassCard><LoadingSkeleton rows={4} /></GlassCard>
      ) : error ? (
        <GlassCard><ErrorStateSurface title="Submission reviews could not be loaded" message={error} /></GlassCard>
      ) : reviews.length === 0 ? (
        <GlassCard>
          <EmptyStateSurface
            title="No submissions in review"
            message="Submission intake is not configured yet. When a broker/direct submission source is connected, advisory rule-engine reviews appear here — no sample submissions are shown."
          />
        </GlassCard>
      ) : (
        <div className="space-y-3">
          {reviews.map((r) => (
            <GlassCard key={r.submissionId}>
              <div className="flex items-start justify-between gap-4">
                <div className="min-w-0">
                  <div className="text-[15px] font-bold text-dls-text-primary">{r.submissionId} · {r.applicant}</div>
                  <div className="mt-2 space-y-1.5">
                    {r.checks.map((c) => (
                      <div key={c.checkId} className="flex items-start gap-2">
                        <Badge tone={checkTone[c.state]}>{checkLabel[c.state]}</Badge>
                        <div className="text-[12.5px] text-dls-text-secondary"><span className="font-semibold text-dls-text-primary">{c.label}:</span> {c.detail}</div>
                      </div>
                    ))}
                  </div>
                </div>
                <div className="flex shrink-0 flex-col items-end gap-2">
                  <Badge tone="info">Advisory only</Badge>
                  <Link href="/app/work-items" className="dls-focus-outline rounded-dls-control text-[11.5px] font-semibold text-dls-primary hover:underline">Open Review &amp; Approvals</Link>
                </div>
              </div>

              {r.draftedQuestions.length > 0 ? (
                <div className="mt-3 rounded-dls-card bg-dls-info-tint p-3">
                  <div className="mb-1.5 flex items-center gap-2"><Sparkles className="h-3.5 w-3.5 text-dls-info-text" aria-hidden="true" /><div className="text-[11px] font-bold uppercase tracking-[0.14em] text-dls-info-text">Drafted clarifying questions — send only after approval</div></div>
                  <ul className="list-disc space-y-1 pl-4 text-[12.5px] text-dls-text-secondary">
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
