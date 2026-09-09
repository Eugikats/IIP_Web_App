"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type SourceCitation = {
  knowledgeDocumentId: string;
  chunkId: string;
  documentName: string;
  productLine: string;
  version: string;
  excerpt: string;
  label: string;
};

export type CoverageState = "GroundedAnswerAvailable" | "CoverageUnclear" | "InsufficientApprovedEvidence" | "ConflictingSources";

export type PolicyQuestionAnswer = {
  answer: string;
  confidenceScore: number;
  escalationRequired: boolean;
  guardrailMessage: string;
  riskFlag: string;
  coverageState: CoverageState;
  sources: SourceCitation[];
  agentActionLogId: string;
  modelProvider: string;
  generatedAt: string;
  correlationId: string;
};

export type AskPolicyQuestionInput = {
  question: string;
  caseId?: string;
  productLine?: string;
  audience?: "InternalUser" | "Customer";
};

async function safeError(response: Response, fallback: string): Promise<string> {
  try {
    const payload = (await response.json()) as { message?: string; errors?: Record<string, string[]> };
    if (payload.message) return payload.message;
    const first = payload.errors ? Object.values(payload.errors).flat()[0] : null;
    if (first) return first;
  } catch {
    // Keep the safe fallback for non-JSON errors.
  }
  return fallback;
}

export type PolicyAssistantResult<T> = { ok: true; value: T } | { ok: false; error: string; forbidden?: boolean };

export async function askPolicyQuestion(role: string, input: AskPolicyQuestionInput): Promise<PolicyAssistantResult<PolicyQuestionAnswer>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/assistant/policy-questions`, {
      method: "POST",
      headers: { "Content-Type": "application/json", ...scopedHeaders(role, "") },
      body: JSON.stringify({
        question: input.question,
        caseId: input.caseId,
        productLine: input.productLine,
        audience: input.audience ?? "InternalUser",
      }),
    });
    if (response.status === 403) {
      return { ok: false, error: "You do not have permission to ask policy questions.", forbidden: true };
    }
    if (!response.ok) {
      return { ok: false, error: await safeError(response, `The policy assistant could not answer.`) };
    }
    return { ok: true, value: (await response.json()) as PolicyQuestionAnswer };
  } catch (reason) {
    return { ok: false, error: reason instanceof Error ? reason.message : "The policy assistant could not be reached." };
  }
}
