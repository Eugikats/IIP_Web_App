"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { communications as fixtureComms, type CommStatus } from "@/lib/lovable-parity/mock";

export type { CommStatus };

export type CommunicationView = {
  id: string;
  claim: string;
  customer: string;
  channel: string;
  subject: string;
  preview: string;
  status: CommStatus;
  drafted: string;
  reviewer?: string;
};

type CommunicationDto = {
  communicationId: string;
  caseId?: string | null;
  channel: string;
  messageBody: string;
  sentBy?: string | null;
  deliveryStatus?: string | null;
  createdAt: string;
};

function mapStatus(raw?: string | null): CommStatus {
  const s = (raw ?? "").toLowerCase().replace(/[^a-z]/g, "");
  if (s.includes("approv") && !s.includes("pending")) return "Approved";
  if (s.includes("sendfail") || s.includes("failed")) return "Send Failed";
  if (s === "sent" || s.includes("delivered")) return "Sent";
  if (s.includes("reject")) return "Rejected";
  return "Pending Approval";
}

function relTime(iso: string): string {
  const t = Date.parse(iso);
  if (Number.isNaN(t)) return iso || "—";
  return new Date(t).toLocaleString(undefined, { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });
}

const PARITY: CommunicationView[] = fixtureComms.map((c) => ({
  id: c.id, claim: c.claim ?? "—", customer: c.customer, channel: String(c.channel),
  subject: c.subject, preview: c.preview, status: c.status, drafted: c.drafted, reviewer: c.reviewer,
}));

function mapDto(dto: CommunicationDto): CommunicationView {
  const body = dto.messageBody ?? "";
  return {
    id: dto.communicationId,
    claim: dto.caseId ?? "—",
    customer: "—",
    channel: dto.channel,
    subject: body.split("\n")[0]?.slice(0, 60) || "Customer update",
    preview: body,
    status: mapStatus(dto.deliveryStatus),
    drafted: relTime(dto.createdAt),
    reviewer: dto.sentBy ?? undefined,
  };
}

export function useCommunications() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [items, setItems] = useState<CommunicationView[]>(LOVABLE_VISUAL_PARITY_MODE ? PARITY : []);
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) return;
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/communications/history`, { headers: scopedHeaders(role, "") });
      if (!response.ok) throw new Error(`Communications failed with HTTP ${response.status}.`);
      const rows = (await response.json()) as CommunicationDto[];
      setItems(rows.map(mapDto));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Communications failed to load.");
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  return { items, loading, error, parityMode: LOVABLE_VISUAL_PARITY_MODE };
}
