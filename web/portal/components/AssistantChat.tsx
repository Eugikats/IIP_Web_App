"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { PromptBar, ChatContextItem } from "./PromptBar";
import { StatusBadge } from "./Badges";
import { ErrorState } from "./States";
import { BlockedAction, BlockedActionPanel, StatusChip } from "./GovernedUi";
import { useRole } from "./RoleContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { AlertTriangle, Bot, Sparkles } from "lucide-react";

type AssistantSource = {
  knowledgeDocumentId: string;
  chunkId: string;
  documentName: string;
  productLine: string;
  version: string;
  excerpt: string;
  label: string;
};

type AssistantResponse = {
  answer: string;
  confidenceScore: number;
  escalationRequired: boolean;
  guardrailMessage: string;
  riskFlag: string;
  coverageState?: string;
  citations?: AssistantSource[];
  sources?: AssistantSource[]; // legacy
  agentActionLogId: string;
  insightType?: string;
  modelProvider: string;
  modelDeployment?: string | null;
  promptVersion: string;
  visibility: string;
  canTriggerDecision: false;
  generatedAt: string;
  correlationId: string;
};

// Grounded policy-QA is the only assistant endpoint. The prose Command Center
// assistant (/command-center/assistant) was dropped — the chat-first
// /api/v1/command-center/query path replaces it and is not routed through here.
type AssistantEndpointPath = "/api/v1/assistant/policy-questions";

type ChatTurn =
  | { id: string; role: "user"; text: string }
  | { id: string; role: "assistant"; pending: true }
  | { id: string; role: "assistant"; pending: false; response: AssistantResponse };

/**
 * Shared governed chat surface for policy Q&A and Command Center operational
 * summaries. Renders a scrolling transcript with a pinned composer and a
 * transparency context window; the caller selects the backend endpoint.
 *
 * Render inside a flex-column parent that defines the height; this component
 * fills it (`h-full`) so the transcript scrolls and the composer stays pinned.
 */
export function AssistantChat({
  className = "",
  productLine = null,
  caseId = null,
  suggestions = [],
  emptyTitle = "Ask a grounded policy question",
  emptyHint = "The assistant answers only from approved tenant knowledge and cites its sources.",
  customEmptyState = null,
  showOpenFullLink = false,
  endpointPath = "/api/v1/assistant/policy-questions",
  groundingLabel = "Approved tenant knowledge only",
  placeholder = "Ask IIP AI a policy question...",
}: {
  className?: string;
  productLine?: string | null;
  caseId?: string | null;
  suggestions?: string[];
  emptyTitle?: string;
  emptyHint?: string;
  customEmptyState?: React.ReactNode;
  showOpenFullLink?: boolean;
  endpointPath?: AssistantEndpointPath;
  groundingLabel?: string;
  placeholder?: string;
}) {
  const { role, userId } = useRole();
  const [turns, setTurns] = useState<ChatTurn[]>([]);
  const [question, setQuestion] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const endRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: "smooth", block: "nearest" });
  }, [turns]);

  async function send(rawText: string) {
    const text = rawText.trim();
    if (!text || busy) return;

    const userTurn: ChatTurn = { id: crypto.randomUUID(), role: "user", text };
    const pendingId = crypto.randomUUID();
    setTurns((prev) => [...prev, userTurn, { id: pendingId, role: "assistant", pending: true }]);
    setQuestion("");
    setBusy(true);
    setError(null);

    try {
      const payload = { caseId, question: text, productLine };

      const response = await fetch(`${apiBaseUrl}${endpointPath}`, {
        method: "POST",
        headers: {
          ...scopedHeaders(role, userId),
          "Content-Type": "application/json",
        },
        body: JSON.stringify(payload),
      });
      if (!response.ok) throw new Error(await readAssistantError(response));
      const data = parseAssistantResponse(await response.json());
      if (!data) {
        throw new Error("Assistant response did not include required governed provenance and non-decision state.");
      }
      setTurns((prev) =>
        prev.map((turn) =>
          turn.id === pendingId ? { id: pendingId, role: "assistant", pending: false, response: data } : turn
        )
      );
    } catch (err) {
      setTurns((prev) => prev.filter((turn) => turn.id !== pendingId));
      setError(err instanceof Error ? err.message : "Assistant request failed.");
    } finally {
      setBusy(false);
    }
  }

  const context: ChatContextItem[] = [
    { label: "Scope", value: "Active tenant and workspace" },
    { label: "Role", value: role },
    ...(productLine ? [{ label: "Product line", value: productLine }] : []),
    ...(caseId ? [{ label: "Case", value: caseId }] : []),
    { label: "Grounding", value: groundingLabel },
  ];

  return (
    <div className={`flex flex-col ${className}`}>
      <div className="flex-1 space-y-4 py-5">
        {turns.length === 0 ? (
          <div className="flex h-full flex-col items-center justify-center py-8 text-center">
            {customEmptyState || (
              <>
                <div className="mb-3 flex h-12 w-12 items-center justify-center rounded-2xl border border-ai-border bg-ai-soft">
                  <Bot className="h-6 w-6 text-ai" />
                </div>
                <p className="text-base font-semibold text-ink">{emptyTitle}</p>
                <p className="mt-1.5 max-w-md text-xs leading-relaxed text-muted">{emptyHint}</p>
              </>
            )}
            {suggestions.length > 0 && (
              <div className="mt-8 flex max-w-lg flex-wrap justify-center gap-2">
                {suggestions.map((s) => (
                  <button
                    key={s}
                    onClick={() => send(s)}
                    className="inline-flex items-center gap-1.5 rounded-full border border-line bg-surface px-4 py-2 text-xs font-semibold text-ink transition-all hover:-translate-y-0.5 hover:border-ai/40 hover:shadow-sm"
                  >
                    <Sparkles className="h-3.5 w-3.5 text-ai" />
                    {s}
                  </button>
                ))}
              </div>
            )}
          </div>
        ) : (
          turns.map((turn) =>
            turn.role === "user" ? (
              <div key={turn.id} className="flex justify-end">
                <div className="max-w-[80%] rounded-3xl rounded-tr-md bg-primary px-4 py-2.5 text-sm leading-relaxed text-inverse">
                  {turn.text}
                </div>
              </div>
            ) : (
              <div key={turn.id} className="flex gap-2.5">
                <div className="mt-0.5 flex h-7 w-7 flex-shrink-0 items-center justify-center rounded-full border border-ai-border bg-ai-soft">
                  <Bot className="h-3.5 w-3.5 text-ai" />
                </div>
                <div className="min-w-0 flex-1">
                  {turn.pending ? (
                    <div className="inline-flex items-center gap-2 rounded-3xl rounded-tl-md border border-ai-border bg-ai-surface px-4 py-2.5 text-sm text-muted">
                      <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-ai" />
                      IIP is checking approved knowledge sources…
                    </div>
                  ) : (
                    <AssistantBubble response={turn.response} showOpenFullLink={showOpenFullLink} />
                  )}
                </div>
              </div>
            )
          )
        )}
        <div ref={endRef} />
      </div>

      {error ? (
        <div className="px-5 pb-2">
          <ErrorState message={error} />
        </div>
      ) : null}

      <PromptBar
        value={question}
        onChange={setQuestion}
        onSubmit={() => send(question)}
        busy={busy}
        placeholder={placeholder}
        context={context}
      />
    </div>
  );
}

function AssistantBubble({ response, showOpenFullLink }: { response: AssistantResponse; showOpenFullLink: boolean }) {
  const lowConfidence = response.confidenceScore < 0.5 || response.escalationRequired;
  const approvedSources = response.citations ?? response.sources ?? [];
  return (
    <div className="space-y-2">
      <div className="rounded-3xl rounded-tl-md border border-ai-border bg-ai-surface px-4 py-3 text-sm leading-relaxed text-ink whitespace-pre-wrap">
        {response.answer}
      </div>
      <div className="flex flex-wrap items-center gap-2">
        <StatusBadge label={`${Math.round(response.confidenceScore * 100)}% confidence`} />
        {response.escalationRequired ? <StatusBadge label="Escalation required" /> : null}
        {response.riskFlag ? <StatusBadge label={response.riskFlag} /> : null}
        {response.coverageState ? <StatusBadge label={response.coverageState} /> : null}
        {response.visibility ? <StatusBadge label={response.visibility} /> : null}
      </div>
      {response.modelProvider ? (
        <p className="text-[11px] leading-relaxed text-muted">
          Prepared by {response.modelProvider}
          {response.canTriggerDecision === false ? " · Preparation only" : ""}
          {response.promptVersion ? ` · Prompt ${response.promptVersion}` : ""}
          {response.agentActionLogId ? ` · Audit ${response.agentActionLogId.slice(0, 8)}` : ""}
        </p>
      ) : null}
      {response.guardrailMessage ? (
        <p className="rounded-xl border border-line bg-surface px-3 py-2 text-[11px] leading-relaxed text-muted">
          {response.guardrailMessage}
        </p>
      ) : null}
      {lowConfidence ? (
        <div className="flex items-start gap-2 rounded-xl border border-warning/30 bg-warning/5 px-3 py-2 text-xs leading-relaxed text-warning">
          <AlertTriangle className="mt-0.5 h-3.5 w-3.5 flex-shrink-0" />
          Low confidence — human verification required before acting on this answer.
        </div>
      ) : null}
      {approvedSources.length > 0 ? (
        <div role="region" aria-label="Approved sources" className="mt-2 space-y-2 rounded-2xl border border-line bg-surface p-3">
          <p className="text-[10px] font-bold uppercase tracking-[0.18em] text-muted">
            {approvedSources.length} approved source{approvedSources.length === 1 ? "" : "s"} cited
          </p>
          {approvedSources.map((source) => (
            <div key={`${source.knowledgeDocumentId}:${source.chunkId}`} className="rounded-xl border border-line bg-mist px-3 py-2.5">
              <p className="text-xs font-semibold text-ink">{source.label}</p>
              <p className="mt-1 text-[11px] text-muted">{source.documentName} · Version {source.version}</p>
              <p className="mt-2 text-xs leading-relaxed text-ink">{source.excerpt}</p>
            </div>
          ))}
          {showOpenFullLink ? (
            <Link href="/ai-assistant" className="inline-flex text-[11px] font-semibold text-accent hover:underline">
              Open full assistant
            </Link>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}

function parseAssistantResponse(value: unknown): AssistantResponse | null {
  if (!isRecord(value)
      || typeof value.answer !== "string"
      || typeof value.confidenceScore !== "number"
      || !Number.isFinite(value.confidenceScore)
      || value.confidenceScore < 0
      || value.confidenceScore > 1
      || typeof value.escalationRequired !== "boolean"
      || typeof value.guardrailMessage !== "string"
      || typeof value.riskFlag !== "string"
      || typeof value.agentActionLogId !== "string"
      || typeof value.modelProvider !== "string"
      || !(typeof value.modelDeployment === "string" || value.modelDeployment === null)
      || typeof value.promptVersion !== "string"
      || typeof value.visibility !== "string"
      || value.canTriggerDecision !== false
      || typeof value.generatedAt !== "string"
      || typeof value.correlationId !== "string") {
    return null;
  }

  const sourceValue = Array.isArray(value.citations)
    ? value.citations
    : Array.isArray(value.sources)
      ? value.sources
      : [];
  if (!sourceValue.every(isAssistantSource)) {
    return null;
  }

  return {
    answer: value.answer,
    confidenceScore: value.confidenceScore,
    escalationRequired: value.escalationRequired,
    guardrailMessage: value.guardrailMessage,
    riskFlag: value.riskFlag,
    coverageState: typeof value.coverageState === "string" ? value.coverageState : undefined,
    sources: sourceValue,
    agentActionLogId: value.agentActionLogId,
    insightType: typeof value.insightType === "string" ? value.insightType : undefined,
    modelProvider: value.modelProvider,
    modelDeployment: value.modelDeployment,
    promptVersion: value.promptVersion,
    visibility: value.visibility,
    canTriggerDecision: false,
    generatedAt: value.generatedAt,
    correlationId: value.correlationId
  };
}

function isAssistantSource(value: unknown): value is AssistantSource {
  return isRecord(value)
    && typeof value.knowledgeDocumentId === "string"
    && typeof value.chunkId === "string"
    && typeof value.documentName === "string"
    && typeof value.productLine === "string"
    && typeof value.version === "string"
    && typeof value.excerpt === "string"
    && typeof value.label === "string";
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

async function readAssistantError(response: Response) {
  try {
    const body = (await response.json()) as { message?: string; detail?: string; title?: string };
    return body.message ?? body.detail ?? body.title ?? `Assistant request failed with HTTP ${response.status}.`;
  } catch {
    return `Assistant request failed with HTTP ${response.status}.`;
  }
}
