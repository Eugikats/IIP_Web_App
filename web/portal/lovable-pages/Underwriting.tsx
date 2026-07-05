"use client";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import { Sparkles, ClipboardCheck } from "lucide-react";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { toast } from "sonner";
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
        <GlassCard><div className="p-6 text-center text-[13px] text-stone">Loading submission reviews…</div></GlassCard>
      ) : reviews.length === 0 ? (
        <GlassCard>
          <div className="p-10 text-center flex flex-col items-center gap-2">
            <ClipboardCheck className="h-6 w-6 text-stone" />
            <div className="text-navy font-medium">No submissions in review</div>
            <div className="text-[12.5px] text-stone max-w-md">
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
                  <div className="text-[15px] font-semibold text-navy">{r.submissionId} · {r.applicant}</div>
                  <div className="mt-2 space-y-1.5">
                    {r.checks.map((c) => (
                      <div key={c.checkId} className="flex items-start gap-2">
                        <Badge tone={checkTone[c.state]}>{checkLabel[c.state]}</Badge>
                        <div className="text-[12.5px] text-slate2"><span className="text-navy font-medium">{c.label}:</span> {c.detail}</div>
                      </div>
                    ))}
                  </div>
                </div>
                <div className="flex flex-col gap-2 shrink-0">
                  <button onClick={() => toast("Draft prepared — send only after underwriter approval")} className="text-[12.5px] rounded-xl gradient-primary text-white px-3 py-1.5">Draft questions</button>
                  <button onClick={() => toast("Escalated for underwriter review")} className="text-[12.5px] rounded-xl border border-cloud bg-white px-3 py-1.5 text-navy">Escalate for review</button>
                </div>
              </div>

              {r.draftedQuestions.length > 0 ? (
                <div className="mt-3 rounded-xl border border-cloud bg-white/80 p-3">
                  <div className="flex items-center gap-2 mb-1.5"><Sparkles className="h-3.5 w-3.5 text-info" /><div className="text-[11px] uppercase tracking-[0.14em] text-stone">Drafted clarifying questions — send only after approval</div></div>
                  <ul className="text-[12.5px] text-slate2 space-y-1 list-disc pl-4">
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
