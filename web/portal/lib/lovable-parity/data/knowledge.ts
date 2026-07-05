"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { knowledgeDocs as fixtureDocs, type KnowledgeSensitivity } from "@/lib/lovable-parity/mock";

export type { KnowledgeSensitivity };

export type KnowledgeDocView = {
  id: string;
  title: string;
  category: string;
  sensitivity: KnowledgeSensitivity;
  status: string;
  retrievalEligible: boolean;
  updated: string;
  owner: string;
};

type KnowledgeDocDto = {
  knowledgeDocumentId: string;
  documentName: string;
  productLine: string;
  documentType: string;
  approvalStatus: string;
  indexedStatus: string;
  updatedAt: string;
};

function mapStatus(approval: string): string {
  const s = (approval ?? "").toLowerCase();
  if (s.includes("approv")) return "Approved";
  if (s.includes("reindex") || s.includes("re-index")) return "Needs re-index";
  return "Pending";
}

const PARITY: KnowledgeDocView[] = fixtureDocs.map((d) => ({
  id: d.id, title: d.title, category: d.category, sensitivity: d.sensitivity,
  status: d.status, retrievalEligible: d.retrievalEligible, updated: d.updated, owner: d.owner,
}));

function mapDto(dto: KnowledgeDocDto): KnowledgeDocView {
  const status = mapStatus(dto.approvalStatus);
  const indexed = (dto.indexedStatus ?? "").toLowerCase().includes("index");
  return {
    id: dto.knowledgeDocumentId,
    title: dto.documentName,
    category: dto.documentType || dto.productLine || "Document",
    sensitivity: "unknown",
    status,
    retrievalEligible: status === "Approved" && indexed,
    updated: Number.isNaN(Date.parse(dto.updatedAt)) ? "—" : new Date(dto.updatedAt).toLocaleDateString(),
    owner: "—",
  };
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

  return { docs, loading, error, parityMode: LOVABLE_VISUAL_PARITY_MODE };
}
