"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { documentLibrary } from "@/lib/lovable-parity/mock";

export type DocumentView = {
  id: string;
  name: string;
  type: string;
  claim: string;
  status: string;
  confidence: number;
  uploaded: string;
};

type DocumentDto = {
  documentId: string;
  caseId: string;
  fileName: string;
  documentType: string;
  status: string;
  classificationConfidence: number;
  uploadedAt: string;
};

const PARITY: DocumentView[] = documentLibrary.map((d) => ({
  id: d.id, name: d.name, type: d.type, claim: d.claim ?? "—",
  status: d.status, confidence: d.confidence ?? 0,
  uploaded: d.uploaded,
}));

function mapDto(d: DocumentDto): DocumentView {
  return {
    id: d.documentId,
    name: d.fileName,
    type: d.documentType,
    claim: d.caseId,
    status: d.status,
    confidence: d.classificationConfidence,
    uploaded: Number.isNaN(Date.parse(d.uploadedAt)) ? "—" : new Date(d.uploadedAt).toLocaleDateString(),
  };
}

export function useDocuments() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [documents, setDocuments] = useState<DocumentView[]>(LOVABLE_VISUAL_PARITY_MODE ? PARITY : []);
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) return;
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/documents`, { headers: scopedHeaders(role, "") });
      if (!response.ok) throw new Error(`Documents failed with HTTP ${response.status}.`);
      const rows = (await response.json()) as DocumentDto[];
      setDocuments(rows.map(mapDto));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Documents failed to load.");
      setDocuments([]);
    } finally {
      setLoading(false);
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  return { documents, loading, error, parityMode: LOVABLE_VISUAL_PARITY_MODE };
}
