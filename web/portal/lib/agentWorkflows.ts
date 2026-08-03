"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type WorkflowNodeType = "Trigger" | "Agent" | "Tool" | "Decision" | "HumanReview" | "Complete";
export type WorkflowSideEffect = "ReadOnly" | "DraftOnly" | "InternalWrite" | "ApprovalRequired" | "ExternalSend";

export type WorkflowNode = {
  key: string;
  name: string;
  description: string;
  type: WorkflowNodeType;
  agentKey?: string | null;
  toolKey?: string | null;
  sideEffect: WorkflowSideEffect;
  position: { x: number; y: number };
  requiresHumanReview: boolean;
  configuration: Record<string, string>;
};

export type WorkflowEdge = {
  sourceNodeKey: string;
  targetNodeKey: string;
  outcome?: string | null;
  label?: string | null;
};

export type WorkflowDefinition = {
  definitionId: string;
  key: string;
  name: string;
  description: string;
  release: number;
  version: number;
  status: "Draft" | "Published" | "Archived";
  isSystemDefinition: boolean;
  businessReferenceTypes: string[];
  nodes: WorkflowNode[];
  edges: WorkflowEdge[];
  publishedAt?: string | null;
};

export type EvidenceReference = {
  sourceType: string;
  sourceId: string;
  displayLabel?: string | null;
  pageNumber?: number | null;
  boundingPolygon?: number[] | null;
  confidence?: number | null;
  contentHash?: string | null;
};

export type WorkflowNodeRun = {
  nodeKey: string;
  status: "Pending" | "Queued" | "Running" | "WaitingForHuman" | "Completed" | "Failed" | "Skipped";
  startedAt?: string | null;
  completedAt?: string | null;
  outcome?: string | null;
  outputSummary?: string | null;
  evidence: EvidenceReference[];
  provider?: string | null;
  modelOrTool?: string | null;
  promptVersion?: string | null;
  errorCode?: string | null;
  attemptCount: number;
};

export type WorkflowRun = {
  runId: string;
  definitionId: string;
  workflowKey: string;
  workflowVersion: number;
  businessReferenceType: string;
  businessReferenceId: string;
  status: "Queued" | "Running" | "WaitingForHuman" | "Completed" | "Failed" | "Cancelled";
  nodeRuns: WorkflowNodeRun[];
  createdAt: string;
  updatedAt: string;
  completedAt?: string | null;
  correlationId: string;
  failureCode?: string | null;
};

export type AgentWorkItem = {
  workItemId: string;
  runId: string;
  workflowKey: string;
  nodeKey: string;
  title: string;
  reviewInstructions: string;
  businessReferenceType: string;
  businessReferenceId: string;
  status: "RequiresReview" | "InProgress" | "Completed" | "ReturnedForCorrection" | "Escalated" | "Cancelled";
  evidence: EvidenceReference[];
  assignedToUserId?: string | null;
  createdAt: string;
  updatedAt: string;
  decision?: "AcceptPreparedWork" | "ReturnForCorrection" | "Escalate" | "CancelWorkflow" | null;
  decisionReason?: string | null;
  correlationId: string;
};

type ApiResult<T> = { ok: true; data: T } | { ok: false; message: string };

async function readError(response: Response, fallback: string) {
  try {
    const body = await response.json() as { title?: string; detail?: string; errors?: Record<string, string[]> };
    const validation = body.errors ? Object.values(body.errors).flat()[0] : null;
    return validation ?? body.detail ?? body.title ?? fallback;
  } catch {
    return fallback;
  }
}

async function api<T>(path: string, role: string, init?: RequestInit): Promise<ApiResult<T>> {
  try {
    const response = await fetch(`${apiBaseUrl}${path}`, {
      ...init,
      headers: {
        ...scopedHeaders(role, ""),
        ...(init?.body ? { "Content-Type": "application/json" } : {}),
        ...(init?.headers ?? {}),
      },
    });
    if (!response.ok) {
      return { ok: false, message: await readError(response, `Request failed with HTTP ${response.status}.`) };
    }
    return { ok: true, data: await response.json() as T };
  } catch (error) {
    return { ok: false, message: error instanceof Error ? error.message : "The workflow service is unavailable." };
  }
}

export function useAgentWorkflows() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [definitions, setDefinitions] = useState<WorkflowDefinition[]>([]);
  const [runs, setRuns] = useState<WorkflowRun[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError(null);
    const [definitionsResult, runsResult] = await Promise.all([
      api<WorkflowDefinition[]>("/api/v1/agent-workflows", role),
      api<WorkflowRun[]>("/api/v1/agent-workflow-runs", role),
    ]);
    setDefinitions(definitionsResult.ok ? definitionsResult.data : []);
    setRuns(runsResult.ok ? runsResult.data : []);
    if (!definitionsResult.ok) setError(definitionsResult.message);
    else if (!runsResult.ok) setError(runsResult.message);
    setLoading(false);
  }, [role]);

  useEffect(() => { void refresh(); }, [refresh]);

  const startRun = useCallback(async (workflowKey: string, businessReferenceType: string, businessReferenceId: string) => {
    const result = await api<WorkflowRun>(`/api/v1/agent-workflows/${encodeURIComponent(workflowKey)}/runs`, role, {
      method: "POST",
      body: JSON.stringify({ businessReferenceType, businessReferenceId }),
    });
    if (result.ok) setRuns((current) => [result.data, ...current.filter((run) => run.runId !== result.data.runId)]);
    return result;
  }, [role]);

  const saveDraft = useCallback(async (draft: {
    definitionId?: string | null;
    key: string;
    name: string;
    description: string;
    businessReferenceTypes: string[];
    nodes: WorkflowNode[];
    edges: WorkflowEdge[];
  }) => api<WorkflowDefinition>("/api/v1/agent-workflows", role, { method: "POST", body: JSON.stringify(draft) }), [role]);

  const publish = useCallback(async (workflowKey: string) =>
    api<WorkflowDefinition>(`/api/v1/agent-workflows/${encodeURIComponent(workflowKey)}/publish`, role, { method: "POST" }), [role]);

  return { definitions, runs, loading, error, refresh, startRun, saveDraft, publish };
}

export function useAgentWorkflowRun(runId: string) {
  const { roleDef } = useUI();
  const [run, setRun] = useState<WorkflowRun | null>(null);
  const [definition, setDefinition] = useState<WorkflowDefinition | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    const runResult = await api<WorkflowRun>(`/api/v1/agent-workflow-runs/${encodeURIComponent(runId)}`, roleDef.key);
    if (!runResult.ok) {
      setRun(null); setDefinition(null); setError(runResult.message); setLoading(false); return;
    }
    setRun(runResult.data);
    const definitionResult = await api<WorkflowDefinition>(`/api/v1/agent-workflows/${encodeURIComponent(runResult.data.workflowKey)}`, roleDef.key);
    setDefinition(definitionResult.ok ? definitionResult.data : null);
    setError(definitionResult.ok ? null : definitionResult.message);
    setLoading(false);
  }, [roleDef.key, runId]);

  useEffect(() => { void refresh(); }, [refresh]);
  return { run, definition, loading, error, refresh };
}

export function useAgentWorkItems() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [items, setItems] = useState<AgentWorkItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    const result = await api<AgentWorkItem[]>("/api/v1/agent-work-items", role);
    setItems(result.ok ? result.data : []);
    setError(result.ok ? null : result.message);
    setLoading(false);
  }, [role]);

  useEffect(() => { void refresh(); }, [refresh]);

  const decide = useCallback(async (workItemId: string, decision: AgentWorkItem["decision"], reason: string) => {
    const result = await api<AgentWorkItem>(`/api/v1/agent-work-items/${encodeURIComponent(workItemId)}/decision`, role, {
      method: "POST",
      body: JSON.stringify({ decision, reason }),
    });
    if (result.ok) setItems((current) => current.map((item) => item.workItemId === result.data.workItemId ? result.data : item));
    return result;
  }, [role]);

  return { items, loading, error, refresh, decide };
}

