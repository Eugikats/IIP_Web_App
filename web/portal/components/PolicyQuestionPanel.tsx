"use client";

import { useState } from "react";
import { AlertTriangle, FileText, Loader2, ShieldQuestion, Sparkles } from "lucide-react";
import { Badge, GlassCard } from "@/components/iip/Primitives";
import { useUI } from "@/context/UIStateContext";
import { askPolicyQuestion, type CoverageState, type PolicyQuestionAnswer } from "@/lib/policyAssistant";

const COVERAGE_TONE: Record<CoverageState, "success" | "warning" | "error"> = {
  GroundedAnswerAvailable: "success",
  CoverageUnclear: "warning",
  ConflictingSources: "warning",
  InsufficientApprovedEvidence: "error",
};

const COVERAGE_LABEL: Record<CoverageState, string> = {
  GroundedAnswerAvailable: "Grounded answer",
  CoverageUnclear: "Coverage unclear — escalate",
  ConflictingSources: "Conflicting approved sources",
  InsufficientApprovedEvidence: "No approved evidence found",
};

export default function PolicyQuestionPanel({
  caseId,
  productLine,
  title = "Ask a policy question",
  sub = "Answers are grounded in approved knowledge sources only — never model memory alone. Every answer is logged with its sources and confidence.",
}: {
  caseId?: string;
  productLine?: string;
  title?: string;
  sub?: string;
}) {
  const { roleDef } = useUI();
  const [question, setQuestion] = useState("");
  const [asking, setAsking] = useState(false);
  const [answer, setAnswer] = useState<PolicyQuestionAnswer | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [forbidden, setForbidden] = useState(false);

  async function onAsk() {
    if (!question.trim()) return;
    setAsking(true);
    setError(null);
    const result = await askPolicyQuestion(roleDef.key, { question: question.trim(), caseId, productLine });
    setAsking(false);
    if (!result.ok) {
      setError(result.error);
      setForbidden(Boolean(result.forbidden));
      setAnswer(null);
      return;
    }
    setForbidden(false);
    setAnswer(result.value);
  }

  if (forbidden) {
    return null;
  }

  return (
    <GlassCard>
      <div className="mb-1 flex items-center gap-2">
        <ShieldQuestion className="h-4 w-4 text-info" />
        <div className="text-[14px] font-semibold text-ink">{title}</div>
      </div>
      <p className="mb-3 text-[11.5px] leading-5 text-muted">{sub}</p>

      <div className="flex flex-wrap gap-2">
        <input
          value={question}
          onChange={(event) => setQuestion(event.target.value)}
          onKeyDown={(event) => { if (event.key === "Enter" && !asking) void onAsk(); }}
          placeholder={productLine ? `Ask about this ${productLine} policy's coverage…` : "e.g. Is windscreen damage covered under comprehensive Motor?"}
          className="min-h-11 flex-1 rounded-xl border border-line bg-white px-3 py-2.5 text-[13px] text-ink outline-none placeholder:text-muted focus-ring"
        />
        <button
          type="button"
          onClick={() => void onAsk()}
          disabled={asking || !question.trim()}
          className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-primary px-4 py-2.5 text-[13px] font-semibold text-white shadow-l2 hover:bg-primary/90 focus-ring disabled:cursor-not-allowed disabled:opacity-50"
        >
          {asking ? <Loader2 className="h-4 w-4 animate-spin" /> : <Sparkles className="h-4 w-4" />} Ask
        </button>
      </div>

      {error ? (
        <div className="mt-3 flex items-start gap-2 rounded-xl border border-danger/25 bg-danger/7 p-3 text-[12px] text-danger">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" /> {error}
        </div>
      ) : null}

      {answer ? (
        <div className="mt-4 space-y-3 rounded-xl border border-line bg-white/80 p-4">
          <div className="flex flex-wrap items-center gap-2">
            <Badge tone={COVERAGE_TONE[answer.coverageState]}>{COVERAGE_LABEL[answer.coverageState]}</Badge>
            <Badge tone="neutral">{Math.round(answer.confidenceScore * 100)}% confidence</Badge>
            {answer.escalationRequired ? <Badge tone="warning">Escalation recommended</Badge> : null}
          </div>
          <p className="text-[13px] leading-relaxed text-ink">{answer.answer}</p>
          {answer.guardrailMessage && answer.guardrailMessage !== answer.answer ? (
            <p className="text-[11.5px] italic text-muted">{answer.guardrailMessage}</p>
          ) : null}
          {answer.sources.length > 0 ? (
            <div>
              <div className="mb-1.5 text-[10.5px] font-semibold uppercase tracking-[0.14em] text-muted">Sources</div>
              <div className="space-y-1.5">
                {answer.sources.map((source) => (
                  <div key={source.chunkId} className="flex items-start gap-2 rounded-lg border border-line bg-mist/50 p-2 text-[11.5px] text-ink-soft">
                    <FileText className="mt-0.5 h-3.5 w-3.5 shrink-0 text-info" />
                    <div>
                      <div className="font-medium text-ink">{source.documentName} <span className="font-normal text-muted">· v{source.version}</span></div>
                      <div className="mt-0.5 text-muted">{source.excerpt}</div>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          ) : null}
          <div className="text-[10.5px] text-muted">Logged as action {answer.agentActionLogId.slice(0, 8)} · {answer.modelProvider}</div>
        </div>
      ) : null}
    </GlassCard>
  );
}
