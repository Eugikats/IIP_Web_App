"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import type { DocumentItem } from "@/lib/documentContracts";

type ActionResult = { ok: boolean; message?: string };
type PreviewResult = ActionResult & { url?: string; contentType?: string };

async function safeError(response: Response, fallback: string) {
  try {
    const payload = await response.json() as { message?: string; errors?: Record<string, string[]> };
    if (payload.message) return payload.message;
    const first = payload.errors ? Object.values(payload.errors).flat()[0] : null;
    if (first) return first;
  } catch {
    // Keep the safe fallback for non-JSON errors.
  }
  return fallback;
}

export function useDocumentExtraction(caseId: string) {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [documents, setDocuments] = useState<DocumentItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!caseId) { setDocuments([]); setLoading(false); return; }
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/documents`, { headers: scopedHeaders(role, "") });
      if (response.status === 404) { setDocuments([]); setError("This claim was not found in the current workspace."); return; }
      if (!response.ok) throw new Error(await safeError(response, `Documents failed with HTTP ${response.status}.`));
      setDocuments((await response.json()) as DocumentItem[]);
    } catch (reason) {
      setDocuments([]);
      setError(reason instanceof Error ? reason.message : "Documents failed to load.");
    } finally {
      setLoading(false);
    }
  }, [caseId, role]);

  useEffect(() => { void load(); }, [load]);

  const correctField = useCallback(async (documentId: string, fieldName: string, correctedValue: string, auditReason: string): Promise<ActionResult> => {
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/documents/${documentId}/correct-field`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json", "X-Audit-Reason": auditReason },
        body: JSON.stringify({ fieldName, correctedValue }),
      });
      if (!response.ok) return { ok: false, message: await safeError(response, `Correction failed with HTTP ${response.status}.`) };
      await load();
      return { ok: true };
    } catch (reason) {
      return { ok: false, message: reason instanceof Error ? reason.message : "Correction failed." };
    }
  }, [caseId, load, role]);

  const approve = useCallback(async (documentId: string, auditReason: string): Promise<ActionResult> => {
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/documents/${documentId}/approve-extraction`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json", "X-Audit-Reason": auditReason },
        body: JSON.stringify({ approvedByUserId: null }),
      });
      if (!response.ok) return { ok: false, message: await safeError(response, `Approval failed with HTTP ${response.status}.`) };
      await load();
      return { ok: true };
    } catch (reason) {
      return { ok: false, message: reason instanceof Error ? reason.message : "Approval failed." };
    }
  }, [caseId, load, role]);

  const preview = useCallback(async (document: DocumentItem): Promise<PreviewResult> => {
    try {
      const response = await fetch(`${apiBaseUrl}${document.previewUrl}`, { headers: scopedHeaders(role, "") });
      if (!response.ok) return { ok: false, message: await safeError(response, `Preview failed with HTTP ${response.status}.`) };
      const blob = await response.blob();
      return { ok: true, url: URL.createObjectURL(blob), contentType: blob.type || document.contentType };
    } catch (reason) {
      return { ok: false, message: reason instanceof Error ? reason.message : "Preview failed." };
    }
  }, [role]);

  return { documents, loading, error, load, correctField, approve, preview };
}
