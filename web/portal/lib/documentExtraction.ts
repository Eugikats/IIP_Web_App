"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import type { DocumentItem } from "@/lib/documentContracts";

type ActionResult = { ok: boolean; message?: string };
type PreviewResult = ActionResult & { url?: string; contentType?: string };

function safeNetworkFailure(reason: unknown, fallback: string) {
  if (reason instanceof TypeError || (reason instanceof Error && /failed to fetch|networkerror/i.test(reason.message))) {
    return "Document service could not be reached. Check the connection.";
  }
  return fallback;
}

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
  const [errorKind, setErrorKind] = useState<"not_found" | "unauthorized" | "unavailable" | null>(null);

  const load = useCallback(async () => {
    if (!caseId) { setDocuments([]); setErrorKind("not_found"); setLoading(false); return; }
    setLoading(true);
    setError(null);
    setErrorKind(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/documents`, { headers: scopedHeaders(role, "") });
      if (response.status === 404) { setDocuments([]); setErrorKind("not_found"); setError("This claim was not found in the current workspace."); return; }
      if (response.status === 401 || response.status === 403) { setDocuments([]); setErrorKind("unauthorized"); setError("You do not have permission to review documents for this claim."); return; }
      if (!response.ok) {
        setDocuments([]);
        setErrorKind("unavailable");
        setError(await safeError(response, `Documents failed.`));
        return;
      }
      setDocuments((await response.json()) as DocumentItem[]);
    } catch (reason) {
      setDocuments([]);
      setErrorKind("unavailable");
      setError(safeNetworkFailure(reason, "Documents failed to load."));
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
      if (!response.ok) return { ok: false, message: await safeError(response, `Correction failed.`) };
      await load();
      return { ok: true };
    } catch (reason) {
      return { ok: false, message: safeNetworkFailure(reason, "Correction failed.") };
    }
  }, [caseId, load, role]);

  const approve = useCallback(async (documentId: string, auditReason: string): Promise<ActionResult> => {
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/documents/${documentId}/approve-extraction`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json", "X-Audit-Reason": auditReason },
        body: JSON.stringify({ approvedByUserId: null }),
      });
      if (!response.ok) return { ok: false, message: await safeError(response, `Approval failed.`) };
      await load();
      return { ok: true };
    } catch (reason) {
      return { ok: false, message: safeNetworkFailure(reason, "Approval failed.") };
    }
  }, [caseId, load, role]);

  const preview = useCallback(async (document: DocumentItem): Promise<PreviewResult> => {
    try {
      const response = await fetch(`${apiBaseUrl}${document.previewUrl}`, { headers: scopedHeaders(role, "") });
      if (!response.ok) return { ok: false, message: await safeError(response, `Preview failed.`) };
      const blob = await response.blob();
      return { ok: true, url: URL.createObjectURL(blob), contentType: blob.type || document.contentType };
    } catch (reason) {
      return { ok: false, message: safeNetworkFailure(reason, "Preview failed.") };
    }
  }, [role]);

  return { documents, loading, error, errorKind, load, correctField, approve, preview };
}
