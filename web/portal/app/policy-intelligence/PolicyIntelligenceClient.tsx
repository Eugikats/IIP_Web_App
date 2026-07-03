"use client";

import { FormEvent, useMemo, useState } from "react";
import { AlertTriangle, BookOpenCheck, FileText, Search, ShieldCheck } from "lucide-react";
import { StatusBadge } from "@/components/Badges";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useRole } from "@/components/RoleContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

type SourceCitation = {
  knowledgeDocumentId: string;
  chunkId: string;
  documentName: string;
  productLine: string;
  version: string;
  excerpt: string;
  label: string;
};

type CoverageState =
  | "GroundedAnswerAvailable"
  | "InsufficientApprovedEvidence"
  | "CoverageUnclear"
  | "ConflictingSources";

type PolicyQuestionResponse = {
  answer: string;
  confidenceScore: number;
  escalationRequired: boolean;
  guardrailMessage: string;
  riskFlag: string;
  coverageState: CoverageState;
  sources: SourceCitation[];
  agentActionLogId: string;
  modelProvider: string;
  modelDeployment?: string | null;
  promptVersion: string;
  visibility: string;
  canTriggerDecision: boolean;
  generatedAt: string;
  correlationId: string;
};

const coverageLabels: Record<CoverageState, string> = {
  GroundedAnswerAvailable: "Grounded answer available",
  InsufficientApprovedEvidence: "Insufficient approved evidence",
  CoverageUnclear: "Coverage unclear",
  ConflictingSources: "Conflicting sources"
};

export function PolicyIntelligenceClient() {
  const { role, userId } = useRole();
  const headers = useMemo(() => scopedHeaders(role, userId), [role, userId]);
  const [question, setQuestion] = useState("");
  const [productLine, setProductLine] = useState("");
  const [caseId, setCaseId] = useState("");
  const [audience, setAudience] = useState("InternalUser");
  const [response, setResponse] = useState<PolicyQuestionResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submitQuestion(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!question.trim()) {
      setError("Enter a policy or coverage question.");
      return;
    }

    setLoading(true);
    setError(null);
    setResponse(null);

    try {
      const apiResponse = await fetch(`${apiBaseUrl}/api/v1/assistant/policy-questions`, {
        method: "POST",
        headers: { ...headers, "Content-Type": "application/json" },
        body: JSON.stringify({
          question: question.trim(),
          productLine: productLine.trim() || null,
          caseId: caseId.trim() || null,
          audience
        })
      });

      if (!apiResponse.ok) {
        throw new Error(await readFailure(apiResponse));
      }

      const payload: unknown = await apiResponse.json();
      if (!isPolicyQuestionResponse(payload)) {
        throw new Error("Policy response did not include the required grounding and coverage state.");
      }

      setResponse(payload);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Policy Intelligence could not prepare an answer.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Policy Intelligence"
        title="Policy coverage workbench"
        description="Interrogate approved insurer knowledge, inspect the exact supporting clauses, and escalate uncertainty for human review."
      />

      <div className="grid gap-6 xl:grid-cols-[360px_minmax(0,1fr)]">
        <div className="space-y-6">
          <SectionCard
            eyebrow="Grounded query"
            title="Ask approved knowledge"
            description="The backend searches approved, active sources within your tenant and workspace before preparing an answer."
          >
            <form className="space-y-4" onSubmit={submitQuestion}>
              <label className="block text-sm font-medium text-ink">
                Policy question
                <textarea
                  aria-label="Policy question"
                  className="mt-1 min-h-32 w-full resize-y rounded-lg border border-line bg-surface px-3 py-2 text-sm text-ink outline-none transition focus:border-accent focus:ring-2 focus:ring-accent/15"
                  value={question}
                  onChange={(event) => setQuestion(event.target.value)}
                  placeholder="What policy conditions apply to this coverage question?"
                />
              </label>

              <label className="block text-sm font-medium text-ink">
                Product line
                <input
                  aria-label="Product line"
                  className="mt-1 w-full rounded-lg border border-line bg-surface px-3 py-2 text-sm text-ink outline-none transition focus:border-accent focus:ring-2 focus:ring-accent/15"
                  value={productLine}
                  onChange={(event) => setProductLine(event.target.value)}
                  placeholder="Optional"
                />
              </label>

              <label className="block text-sm font-medium text-ink">
                Case ID
                <input
                  aria-label="Case ID"
                  className="mt-1 w-full rounded-lg border border-line bg-surface px-3 py-2 text-sm text-ink outline-none transition focus:border-accent focus:ring-2 focus:ring-accent/15"
                  value={caseId}
                  onChange={(event) => setCaseId(event.target.value)}
                  placeholder="Optional case context"
                />
              </label>

              <label className="block text-sm font-medium text-ink">
                Output audience
                <select
                  aria-label="Output audience"
                  className="mt-1 w-full rounded-lg border border-line bg-surface px-3 py-2 text-sm text-ink outline-none transition focus:border-accent focus:ring-2 focus:ring-accent/15"
                  value={audience}
                  onChange={(event) => setAudience(event.target.value)}
                >
                  <option value="InternalUser">Internal review</option>
                  <option value="Customer">Customer-safe draft</option>
                </select>
              </label>

              <button
                className="inline-flex w-full items-center justify-center gap-2 rounded-lg bg-accent px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-brand-hover disabled:cursor-not-allowed disabled:opacity-60"
                type="submit"
                disabled={loading}
              >
                <Search className="h-4 w-4" />
                {loading ? "Checking approved sources..." : "Check policy evidence"}
              </button>
            </form>
          </SectionCard>

          <div className="flex gap-3 rounded-lg border border-info/25 bg-info/5 p-4">
            <ShieldCheck className="mt-0.5 h-5 w-5 flex-none text-info" />
            <p className="text-xs leading-5 text-muted">
              Answers are preparation for human review. This workspace cannot approve, deny, or change coverage.
            </p>
          </div>
        </div>

        <SectionCard
          eyebrow="Evidence review"
          title="Coverage result"
          description="Coverage state, uncertainty, citations, and model provenance are supplied by the backend."
        >
          {loading ? <LoadingState label="Retrieving approved policy evidence..." /> : null}
          {!loading && error ? <ErrorState message={error} /> : null}
          {!loading && !error && !response ? (
            <EmptyState
              title="No policy question submitted"
              description="Ask a question to retrieve approved sources and prepare a reviewable answer."
            />
          ) : null}
          {!loading && !error && response ? <PolicyResult response={response} /> : null}
        </SectionCard>
      </div>
    </div>
  );
}

function PolicyResult({ response }: { response: PolicyQuestionResponse }) {
  const coverageLabel = coverageLabels[response.coverageState];

  return (
    <div className="space-y-5" aria-live="polite">
      <div className="flex flex-wrap items-center gap-2">
        <StatusBadge label={coverageLabel} />
        <StatusBadge label={`${Math.round(response.confidenceScore * 100)}% confidence`} />
        <StatusBadge label={response.visibility} />
      </div>

      <div className="rounded-lg border border-line bg-surface-soft p-5">
        <div className="mb-3 flex items-center gap-2 text-sm font-semibold text-ink">
          <BookOpenCheck className="h-4 w-4 text-accent" />
          Prepared answer
        </div>
        <p className="whitespace-pre-wrap text-sm leading-6 text-ink">{response.answer}</p>
      </div>

      {response.escalationRequired ? (
        <div className="flex gap-3 rounded-lg border border-warning/30 bg-warning/5 p-4">
          <AlertTriangle className="mt-0.5 h-5 w-5 flex-none text-warning" />
          <div>
            <p className="text-sm font-semibold text-ink">Human review required</p>
            <p className="mt-1 text-xs leading-5 text-muted">{response.guardrailMessage}</p>
          </div>
        </div>
      ) : (
        <p className="rounded-lg border border-line bg-surface px-4 py-3 text-xs leading-5 text-muted">
          {response.guardrailMessage}
        </p>
      )}

      <div>
        <div className="mb-3 flex items-center justify-between gap-3">
          <h3 className="text-sm font-semibold text-ink">Approved source citations</h3>
          <span className="text-xs text-muted">{response.sources.length} source{response.sources.length === 1 ? "" : "s"}</span>
        </div>
        {response.sources.length === 0 ? (
          <div className="rounded-lg border border-dashed border-warning/40 bg-warning/5 p-4 text-sm text-muted">
            No approved source supports an answer. Do not use this output as a coverage conclusion.
          </div>
        ) : (
          <div className="space-y-3">
            {response.sources.map((source) => (
              <article className="rounded-lg border border-line bg-surface p-4" key={`${source.knowledgeDocumentId}-${source.chunkId}`}>
                <div className="flex items-start gap-3">
                  <FileText className="mt-0.5 h-4 w-4 flex-none text-accent" />
                  <div className="min-w-0">
                    <p className="text-sm font-semibold text-ink">{source.label || source.documentName}</p>
                    <p className="mt-1 text-xs text-muted">
                      {source.documentName} | {source.productLine} | Version {source.version}
                    </p>
                    <p className="mt-3 text-sm leading-6 text-ink">{source.excerpt}</p>
                  </div>
                </div>
              </article>
            ))}
          </div>
        )}
      </div>

      <dl className="grid gap-3 border-t border-line pt-4 text-xs sm:grid-cols-2">
        <Provenance label="Preparation boundary" value={response.canTriggerDecision ? "Invalid decision authority" : "Cannot trigger decisions"} />
        <Provenance label="Risk signal" value={response.riskFlag} />
        <Provenance label="Prepared by" value={`${response.modelProvider}${response.modelDeployment ? ` / ${response.modelDeployment}` : ""}`} />
        <Provenance label="Prompt version" value={response.promptVersion} />
        <Provenance label="Audit reference" value={response.agentActionLogId} />
        <Provenance label="Generated" value={formatGeneratedAt(response.generatedAt)} />
      </dl>
    </div>
  );
}

function Provenance({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="font-medium text-muted">{label}</dt>
      <dd className="mt-1 break-words text-ink">{value}</dd>
    </div>
  );
}

function isPolicyQuestionResponse(value: unknown): value is PolicyQuestionResponse {
  if (!value || typeof value !== "object") return false;
  const response = value as Partial<PolicyQuestionResponse>;
  return typeof response.answer === "string"
    && typeof response.confidenceScore === "number"
    && typeof response.escalationRequired === "boolean"
    && typeof response.guardrailMessage === "string"
    && typeof response.riskFlag === "string"
    && typeof response.coverageState === "string"
    && Object.prototype.hasOwnProperty.call(coverageLabels, response.coverageState)
    && Array.isArray(response.sources)
    && response.sources.every(isSourceCitation)
    && typeof response.canTriggerDecision === "boolean"
    && response.canTriggerDecision === false
    && typeof response.agentActionLogId === "string"
    && typeof response.modelProvider === "string"
    && typeof response.promptVersion === "string"
    && typeof response.visibility === "string"
    && typeof response.generatedAt === "string"
    && typeof response.correlationId === "string";
}

function isSourceCitation(value: unknown): value is SourceCitation {
  if (!value || typeof value !== "object") return false;
  const source = value as Partial<SourceCitation>;
  return typeof source.knowledgeDocumentId === "string"
    && typeof source.chunkId === "string"
    && typeof source.documentName === "string"
    && typeof source.productLine === "string"
    && typeof source.version === "string"
    && typeof source.excerpt === "string"
    && typeof source.label === "string";
}

function formatGeneratedAt(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "Unavailable";
  return new Intl.DateTimeFormat("en-GB", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: "UTC"
  }).format(date);
}

async function readFailure(response: Response) {
  try {
    const body = await response.json() as { detail?: string; message?: string; errors?: Record<string, string[]> };
    const validation = body.errors ? Object.values(body.errors).flat()[0] : null;
    return validation ?? body.detail ?? body.message ?? `Policy request failed with HTTP ${response.status}.`;
  } catch {
    return `Policy request failed with HTTP ${response.status}.`;
  }
}
