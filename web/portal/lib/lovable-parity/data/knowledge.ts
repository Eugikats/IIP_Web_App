"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { knowledgeDocs as fixtureDocs, type KnowledgeSensitivity } from "@/lib/lovable-parity/mock";

export type { KnowledgeSensitivity };

export type ActionState = { action: string; label: string; requiredPermission: string; reason: string | null };

export type KnowledgeDocView = {
  id: string;
  title: string;
  category: string;
  sensitivity: KnowledgeSensitivity;
  status: string;
  retrievalEligible: boolean;
  updated: string;
  owner: string;
  governanceReason: string | null;
  allowedActions: ActionState[];
  blockedActions: ActionState[];
};

type KnowledgeDocDto = {
  knowledgeDocumentId: string;
  documentName: string;
  productLine: string;
  documentType: string;
  approvalStatus: string;
  indexedStatus: string;
  sensitivity: string;
  governanceReason: string | null;
  updatedAt: string;
  allowedActions?: ActionState[];
  blockedActions?: ActionState[];
};

// WO-17: real API sensitivity enum values (KnowledgeSensitivity.cs) mapped to the portal's
// existing lower_snake_case vocabulary (lib/lovable-parity/mock.ts) so the badge/tone map the UI
// already had keeps working unchanged.
const SENSITIVITY_FROM_API: Record<string, KnowledgeSensitivity> = {
  Unknown: "unknown",
  Approved: "approved",
  StaffOnly: "staff_only",
  CustomerConfidential: "customer_confidential",
  ContainsUnapprovedPii: "contains_unapproved_pii",
  ContainsSecrets: "contains_secrets",
};

export const SENSITIVITY_TO_API: Record<KnowledgeSensitivity, string> = {
  unknown: "Unknown",
  approved: "Approved",
  staff_only: "StaffOnly",
  customer_confidential: "CustomerConfidential",
  contains_unapproved_pii: "ContainsUnapprovedPii",
  contains_secrets: "ContainsSecrets",
};

export const SELECTABLE_SENSITIVITIES: KnowledgeSensitivity[] = [
  "approved", "staff_only", "customer_confidential", "contains_unapproved_pii", "contains_secrets",
];

function mapStatus(approval: string): string {
  const s = (approval ?? "").toLowerCase();
  if (s.includes("approv")) return "Approved";
  return s.includes("archiv") ? "Archived" : "Pending";
}

const PARITY: KnowledgeDocView[] = fixtureDocs.map((d) => ({
  id: d.id, title: d.title, category: d.category, sensitivity: d.sensitivity,
  status: d.status, retrievalEligible: d.retrievalEligible, updated: d.updated, owner: d.owner,
  governanceReason: null, allowedActions: [], blockedActions: [],
}));

function mapDto(dto: KnowledgeDocDto): KnowledgeDocView {
  const sensitivity = SENSITIVITY_FROM_API[dto.sensitivity] ?? "unknown";
  return {
    id: dto.knowledgeDocumentId,
    title: dto.documentName,
    category: dto.documentType || dto.productLine || "Document",
    sensitivity,
    status: mapStatus(dto.approvalStatus),
    retrievalEligible: dto.approvalStatus === "Approved" && dto.indexedStatus === "Indexed" && sensitivity !== "unknown",
    updated: Number.isNaN(Date.parse(dto.updatedAt)) ? "—" : new Date(dto.updatedAt).toLocaleDateString(),
    owner: "—",
    governanceReason: dto.governanceReason,
    allowedActions: dto.allowedActions ?? [],
    blockedActions: dto.blockedActions ?? [],
  };
}

async function readSafeApiError(response: Response, fallback: string): Promise<string> {
  try {
    const body = (await response.json()) as { message?: unknown };
    if (typeof body.message === "string" && body.message.trim()) return body.message;
  } catch {
    // The fallback remains safe for non-JSON or empty error responses.
  }
  return fallback;
}

export function useKnowledgeDocs() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [docs, setDocs] = useState<KnowledgeDocView[]>(LOVABLE_VISUAL_PARITY_MODE ? PARITY : []);
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) return;
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/knowledge-documents`, { headers: scopedHeaders(role, "") });
      if (!response.ok) throw new Error(`Knowledge documents failed with HTTP ${response.status}.`);
      const rows = (await response.json()) as KnowledgeDocDto[];
      setDocs(rows.map(mapDto));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Knowledge documents failed to load.");
      setDocs([]);
    } finally {
      setLoading(false);
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  const upload = useCallback(async (input: {
    file: File; documentName: string; productLine: string; documentType: string; version: string;
    effectiveDate?: string; expiryDate?: string;
  }): Promise<{ ok: boolean; message?: string }> => {
    const form = new FormData();
    form.set("file", input.file);
    form.set("documentName", input.documentName);
    form.set("productLine", input.productLine);
    form.set("documentType", input.documentType);
    form.set("version", input.version);
    if (input.effectiveDate) form.set("effectiveDate", input.effectiveDate);
    if (input.expiryDate) form.set("expiryDate", input.expiryDate);

    const response = await fetch(`${apiBaseUrl}/api/v1/knowledge-documents`, {
      method: "POST",
      headers: scopedHeaders(role, ""),
      body: form,
    });
    if (!response.ok) return { ok: false, message: await readSafeApiError(response, `Upload failed with HTTP ${response.status}.`) };
    await load();
    return { ok: true };
  }, [role, load]);

  const approve = useCallback(async (
    id: string, sensitivity: KnowledgeSensitivity, governanceReason: string
  ): Promise<{ ok: boolean; message?: string }> => {
    const response = await fetch(`${apiBaseUrl}/api/v1/knowledge-documents/${encodeURIComponent(id)}/approve`, {
      method: "POST",
      headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
      body: JSON.stringify({ governanceReason, sensitivity: SENSITIVITY_TO_API[sensitivity] }),
    });
    if (!response.ok) return { ok: false, message: await readSafeApiError(response, `Approve failed with HTTP ${response.status}.`) };
    await load();
    return { ok: true };
  }, [role, load]);

  const archive = useCallback(async (id: string, auditReason: string): Promise<{ ok: boolean; message?: string }> => {
    const response = await fetch(`${apiBaseUrl}/api/v1/knowledge-documents/${encodeURIComponent(id)}/archive`, {
      method: "POST",
      headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json", "X-Audit-Reason": auditReason },
      body: JSON.stringify({}),
    });
    if (!response.ok) return { ok: false, message: await readSafeApiError(response, `Archive failed with HTTP ${response.status}.`) };
    await load();
    return { ok: true };
  }, [role, load]);

  const reindex = useCallback(async (): Promise<{ ok: boolean; message?: string; indexedCount?: number }> => {
    const response = await fetch(`${apiBaseUrl}/api/v1/knowledge-documents/reindex`, {
      method: "POST",
      headers: scopedHeaders(role, ""),
    });
    if (!response.ok) return { ok: false, message: await readSafeApiError(response, `Re-index failed with HTTP ${response.status}.`) };
    const body = (await response.json()) as { indexedDocumentCount?: number };
    await load();
    return { ok: true, indexedCount: body.indexedDocumentCount };
  }, [role, load]);

  return { docs, loading, error, parityMode: LOVABLE_VISUAL_PARITY_MODE, upload, approve, archive, reindex, reload: load };
}
