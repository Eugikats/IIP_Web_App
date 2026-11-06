"use client";

import { useEffect, useState } from "react";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type WhatsAppHealthStatus = "checking" | "ready" | "not_configured" | "unavailable";

/** Connection state for the current workspace only. It never guesses from another workspace. */
export function useWhatsAppHealth(role: string): WhatsAppHealthStatus {
  const [status, setStatus] = useState<WhatsAppHealthStatus>("checking");

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      try {
        const response = await fetch(`${apiBaseUrl}/api/v1/integrations/whatsapp/health`, {
          headers: scopedHeaders(role, ""),
        });
        const next: WhatsAppHealthStatus = response.ok
          ? ((await response.json()) as { configured?: boolean }).configured === true ? "ready" : "not_configured"
          : "unavailable";
        if (!cancelled) setStatus(next);
      } catch {
        if (!cancelled) setStatus("unavailable");
      }
    };
    void load();
    const timer = window.setInterval(() => { void load(); }, 30000);
    return () => { cancelled = true; window.clearInterval(timer); };
  }, [role]);

  return status;
}

export function whatsAppHealthLabel(status: WhatsAppHealthStatus): string {
  switch (status) {
    case "ready": return "WhatsApp configured";
    case "not_configured": return "WhatsApp not configured";
    case "unavailable": return "WhatsApp status unavailable";
    default: return "Checking WhatsApp";
  }
}
