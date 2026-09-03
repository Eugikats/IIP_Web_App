"use client";
import { useState } from "react";
import { AlertTriangle, Loader2, Search, ShieldCheck } from "lucide-react";
import { SectionHeader, GlassCard } from "@/components/iip/Primitives";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { CommandCenterBlocks } from "@/components/iip/CommandCenterAnswer";
import { useCommandCenterQuery } from "@/lib/lovable-parity/data/commandCenterQuery";

// Policy Q&A is a thin, single-question presentation of the same governed path Command Center
// uses: POST /api/v1/command-center/query. There is no separate "Policy Intelligence" model or
// dataset — every answer is grounded, cited, and honestly refused when ungrounded, exactly like
// the Command Center chat. This page must never render clause text, excess figures, or citations
// that did not come back from that endpoint (see AGENTS.md — no hardcoded insurer data).
export default function PolicyIntelligence() {
  const { ask, answer, loading, error, reset } = useCommandCenterQuery();
  const [question, setQuestion] = useState("");
  const [asked, setAsked] = useState<string | null>(null);

  async function submit() {
    const q = question.trim();
    if (!q || loading) return;
    setAsked(q);
    await ask(q);
  }

  return (
    <>
      <SectionHeader
        eyebrow="Policy Intelligence"
        title="Approved policy lookup & coverage Q&A"
        sub="Source-grounded answers from your approved policy library. Every response cites the underlying evidence, or says plainly when it cannot answer."
      />

      <div className="mb-4"><GuardrailBanner text="Answers are grounded in approved, tenant-scoped sources only. Policy Intelligence provides guidance to prepare human review — it is not a binding legal or claims interpretation." /></div>

      <div className="glass-elevated p-4 mb-6">
        <form
          className="flex items-center gap-3"
          onSubmit={(event) => {
            event.preventDefault();
            void submit();
          }}
        >
          <Search className="h-4 w-4 text-stone shrink-0" />
          <input
            value={question}
            onChange={(event) => {
              setQuestion(event.target.value);
              if (error) reset();
            }}
            placeholder="Is rear-end collision covered? What is the excess on this policy?"
            disabled={loading}
            className="flex-1 bg-transparent outline-none text-[14px] placeholder:text-stone text-navy disabled:opacity-60"
          />
          <button
            type="submit"
            disabled={loading || question.trim().length === 0}
            className="rounded-2xl gradient-primary text-white text-[13px] font-medium px-4 py-2 shadow-l2 disabled:cursor-not-allowed disabled:opacity-50 inline-flex items-center gap-2"
          >
            {loading ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : null}
            Ask
          </button>
        </form>
      </div>

      {error ? (
        <GlassCard className="mb-4">
          <div className="flex items-start gap-2 text-[13px] text-danger">
            <AlertTriangle className="h-4 w-4 shrink-0 mt-0.5" />
            <span>{error}</span>
          </div>
        </GlassCard>
      ) : null}

      {answer ? (
        <div className="grid lg:grid-cols-3 gap-4">
          <div className="lg:col-span-3 space-y-4">
            <GlassCard level={3}>
              <div className="flex items-center gap-2 mb-3 text-[11px] uppercase tracking-[0.14em] text-stone">
                <ShieldCheck className="h-3.5 w-3.5 text-info" /> Prepared for review · AI does not decide
              </div>
              <div className="text-[12.5px] text-navy font-medium mb-3">&ldquo;{asked}&rdquo;</div>
              <CommandCenterBlocks blocks={answer.blocks} />
            </GlassCard>
          </div>
        </div>
      ) : !loading && !error ? (
        <GlassCard>
          <div className="text-[12.5px] text-stone">
            Ask a coverage or policy-wording question above. Answers cite the approved policy sources they come from; if nothing approved and current supports an answer, IIP will say so rather than guess.
          </div>
        </GlassCard>
      ) : null}
    </>
  );
}
