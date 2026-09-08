"use client";

import { useState } from "react";
import { AlertTriangle, FileText, Loader2, ShieldQuestion, Sparkles } from "lucide-react";
import { Badge, GlassCard } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
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
        <ShieldQuestion className="h-4 w-4 text-dls-primary" aria-hidden="true" />
        <div className="text-[14px] font-bold text-dls-text-primary">{title}</div>
      </div>
      <p className="mb-3 text-[11.5px] leading-5 text-dls-text-secondary">{sub}</p>

      <div className="flex flex-wrap gap-2">
        <input
          value={question}
          onChange={(event) => setQuestion(event.target.value)}
          onKeyDown={(event) => { if (event.key === "Enter" && !asking) void onAsk(); }}
          placeholder={productLine ? `Ask about this ${productLine} policy's coverage…` : "e.g. Is windscreen damage covered under comprehensive Motor?"}
          className="min-h-11 flex-1 rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-2.5 text-[13px] text-dls-text-primary outline-none placeholder:text-dls-text-muted focus:border-dls-primary focus:ring-[3px] focus:ring-dls-primary/[0.12]"
        />
        <Button variant="primary" icon={asking ? <Loader2 className="animate-spin" /> : <Sparkles />} disabled={asking || !question.trim()} onClick={() => void onAsk()}>
          Ask
        </Button>
      </div>

      {error ? (
        <div className="mt-3 flex items-start gap-2 rounded-dls-card border border-dls-danger-dot bg-dls-danger-tint p-3 text-[12px] text-dls-danger-text">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" /> {error}
        </div>
      ) : null}

      {answer ? (
        <div className="mt-4 space-y-3 rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4">
          <div className="flex flex-wrap items-center gap-2">
            <Badge tone={COVERAGE_TONE[answer.coverageState]}>{COVERAGE_LABEL[answer.coverageState]}</Badge>
            <Badge tone="neutral">{Math.round(answer.confidenceScore * 100)}% confidence</Badge>
            {answer.escalationRequired ? <Badge tone="warning">Escalation recommended</Badge> : null}
          </div>
          <p className="text-[13px] leading-relaxed text-dls-text-primary">{answer.answer}</p>
          {answer.guardrailMessage && answer.guardrailMessage !== answer.answer ? (
            <p className="text-[11.5px] italic text-dls-text-muted">{answer.guardrailMessage}</p>
          ) : null}
          {answer.sources.length > 0 ? (
            <div>
              <div className="mb-1.5 text-[10.5px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Sources</div>
              <div className="space-y-1.5">
                {answer.sources.map((source) => (
                  <div key={source.chunkId} className="flex items-start gap-2 rounded-dls-control border border-dls-border-subtle bg-dls-bg-page p-2 text-[11.5px] text-dls-text-secondary">
                    <FileText className="mt-0.5 h-3.5 w-3.5 shrink-0 text-dls-text-muted" aria-hidden="true" />
                    <div>
                      <div className="font-semibold text-dls-text-primary">{source.documentName} <span className="font-normal text-dls-text-muted">· v{source.version}</span></div>
                      <div className="mt-0.5 text-dls-text-muted">{source.excerpt}</div>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          ) : null}
          <div className="text-[10.5px] text-dls-text-muted">Logged as action {answer.agentActionLogId.slice(0, 8)} · {answer.modelProvider}</div>
        </div>
      ) : null}
    </GlassCard>
  );
}
