"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";

export type DocumentTypeAccuracy = {
  documentType: string;
  sampleCount: number;
  correctCount: number;
  accuracy: number | null;
  minimumSampleCount: number;
};

const PARITY: DocumentTypeAccuracy[] = [
  { documentType: "ClaimForm", sampleCount: 18, correctCount: 17, accuracy: 0.9444, minimumSampleCount: 5 },
  { documentType: "GarageEstimate", sampleCount: 12, correctCount: 9, accuracy: 0.75, minimumSampleCount: 5 },
  { documentType: "PoliceAbstract", sampleCount: 3, correctCount: 3, accuracy: null, minimumSampleCount: 5 },
];

export function useDocumentAccuracy() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [rows, setRows] = useState<DocumentTypeAccuracy[]>(LOVABLE_VISUAL_PARITY_MODE ? PARITY : []);
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) return;
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/documents/quality/accuracy`, { headers: scopedHeaders(role, "") });
      if (response.status === 403) {
        setRows([]);
        return;
      }
      if (!response.ok) throw new Error("Document classification accuracy failed to load.");
      setRows((await response.json()) as DocumentTypeAccuracy[]);
    } catch {
      setError("Document classification accuracy failed to load.");
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  return { rows, loading, error, reload: load };
}
