"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type AgentBehaviourVersion = {
  versionId: string;
  agentKey: string;
  version: number;
  systemPromptOverride: string | null;
  persona: string | null;
  language: string;
  allowedTopics: string[];
  refusalTopics: string[];
  escalationMessage: string;
  maxResponseCharacters: number | null;
  requireCitation: boolean;
  updatedByUserId: string;
  updatedAt: string;
  changeReason: string;
};

export type AgentBehaviourStatus = {
  agentKey: string;
  currentVersion: AgentBehaviourVersion | null;
  effectivePreamble: string;
  effectiveTenantSection: string;
  isCustomized: boolean;
};

export type SaveAgentBehaviourInput = {
  systemPromptOverride: string | null;
  persona: string | null;
  language: string;
  allowedTopics: string[];
  refusalTopics: string[];
  escalationMessage: string | null;
  maxResponseCharacters: number | null;
  requireCitation: boolean;
};

export type AgentBehaviourTestResult = {
  tested: boolean;
  message?: string;
  answer?: string;
  citations?: { title: string; documentId?: string }[];
  requiresHumanReview?: boolean;
  guardrailMessage?: string;
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

function safeNetworkFailure(reason: unknown, fallback: string): string {
  if (reason instanceof TypeError || (reason instanceof Error && /failed to fetch|networkerror/i.test(reason.message))) {
    return "The agent behaviour service could not be reached. Check your connection and try again.";
  }
  return fallback;
}

export type BehaviourResult<T> = { ok: true; value: T } | { ok: false; error: string };

export async function getAgentBehaviour(role: string, agentKey: string): Promise<BehaviourResult<AgentBehaviourStatus>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/agents/${encodeURIComponent(agentKey)}/behaviour`, {
      headers: scopedHeaders(role, ""),
    });
    if (!response.ok) {
      return { ok: false, error: await safeError(response, "This agent's behaviour could not be loaded.") };
    }
    return { ok: true, value: (await response.json()) as AgentBehaviourStatus };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "This agent's behaviour could not be loaded.") };
  }
}

export async function getAgentBehaviourVersions(role: string, agentKey: string): Promise<BehaviourResult<AgentBehaviourVersion[]>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/agents/${encodeURIComponent(agentKey)}/behaviour/versions`, {
      headers: scopedHeaders(role, ""),
    });
    if (!response.ok) {
      return { ok: false, error: await safeError(response, "Version history could not be loaded.") };
    }
    const payload = (await response.json()) as { versions: AgentBehaviourVersion[] };
    return { ok: true, value: payload.versions };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "Version history could not be loaded.") };
  }
}

export async function saveAgentBehaviour(
  role: string,
  agentKey: string,
  input: SaveAgentBehaviourInput,
  auditReason: string
): Promise<BehaviourResult<AgentBehaviourStatus>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/agents/${encodeURIComponent(agentKey)}/behaviour`, {
      method: "POST",
      headers: { "Content-Type": "application/json", "X-Audit-Reason": auditReason, ...scopedHeaders(role, "") },
      body: JSON.stringify(input),
    });
    if (!response.ok) {
      return { ok: false, error: await safeError(response, "This change could not be saved.") };
    }
    return { ok: true, value: (await response.json()) as AgentBehaviourStatus };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "This change could not be saved.") };
  }
}

export async function testAgentBehaviour(role: string, agentKey: string, question: string): Promise<BehaviourResult<AgentBehaviourTestResult>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/agents/${encodeURIComponent(agentKey)}/behaviour/test`, {
      method: "POST",
      headers: { "Content-Type": "application/json", ...scopedHeaders(role, "") },
      body: JSON.stringify({ question }),
    });
    if (!response.ok) {
      return { ok: false, error: await safeError(response, "The test could not be run.") };
    }
    return { ok: true, value: (await response.json()) as AgentBehaviourTestResult };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "The test could not be run.") };
  }
}
