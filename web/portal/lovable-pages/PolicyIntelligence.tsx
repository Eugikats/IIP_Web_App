"use client";
import { useState } from "react";
import { Loader2, Search, ShieldCheck } from "lucide-react";
import { SectionHeader, GlassCard } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { ErrorStateSurface } from "@/components/ui/StateSurfaces";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { CommandCenterBlocks } from "@/components/iip/CommandCenterAnswer";
import { useCommandCenterQuery } from "@/lib/lovable-parity/data/commandCenterQuery";

// Policy Q&A is a thin, single-question presentation of the same governed path Command Center
// uses: POST /api/v1/command-center/query. There is no separate "Policy Intelligence" model or
// dataset — every answer is grounded, cited, and honestly refused when ungrounded, exactly like
// the Command Center chat. This page must never render clause text, excess figures, or citations
// that did not come back from that endpoint (see AGENTS.md — no hardcoded insurer data).
//
// Placeholder text below is unchanged from the pre-migration file: gate-8-policy-knowledge's
// first test expects a differently-worded textbox name ("...covered under MTR-8821?...") that
// this placeholder doesn't match — a pre-existing content mismatch, confirmed unrelated to this
// migration (present before this screen was touched). Not corrected here — inventing wording to
// pass a test without knowing the intended real copy risks shipping the wrong fix; flagged in the
// ledger instead.
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

      <GlassCard className="mb-6">
        <form
          className="flex items-center gap-3"
          onSubmit={(event) => {
            event.preventDefault();
            void submit();
          }}
        >
          <div className="min-w-0 flex-1">
            <Input
              value={question}
              onChange={(event) => {
                setQuestion(event.target.value);
                if (error) reset();
              }}
              placeholder="Is rear-end collision covered? What is the excess on this policy?"
              disabled={loading}
              leadingIcon={<Search aria-hidden="true" />}
              aria-label="Ask a policy or coverage question"
            />
          </div>
          <Button
            type="submit"
            variant="primary"
            disabled={loading || question.trim().length === 0}
            icon={loading ? <Loader2 className="animate-spin" aria-hidden="true" /> : undefined}
          >
            Ask
          </Button>
        </form>
      </GlassCard>

      {error ? (
        <GlassCard className="mb-4"><ErrorStateSurface message={error} /></GlassCard>
      ) : null}

      {answer ? (
        <GlassCard>
          <div className="mb-3 flex items-center gap-2 text-[11px] font-bold uppercase tracking-[0.14em] text-dls-text-muted">
            <ShieldCheck className="h-3.5 w-3.5 text-dls-info-text" aria-hidden="true" /> Prepared for review · AI does not decide
          </div>
          <div className="mb-3 text-[12.5px] font-semibold text-dls-text-primary">&ldquo;{asked}&rdquo;</div>
          <CommandCenterBlocks blocks={answer.blocks} />
        </GlassCard>
      ) : !loading && !error ? (
        <GlassCard>
          <div className="text-[12.5px] text-dls-text-secondary">
            Ask a coverage or policy-wording question above. Answers cite the approved policy sources they come from; if nothing approved and current supports an answer, IIP will say so rather than guess.
          </div>
        </GlassCard>
      ) : null}
    </>
  );
}
