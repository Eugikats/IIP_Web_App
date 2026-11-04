"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Select } from "@/components/ui/Input";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

type SendClaimLinkProps = { caseId: string; role: string };

/**
 * "Send claim link" (build-loop item 22): sends the customer a single use link to this claim, valid for 24
 * hours, on the channel the claim came in on or the one chosen here. It respects the customer's consent,
 * says plainly when it could not send and why, and shows only a masked address, never the link.
 */
export function SendClaimLink({ caseId, role }: SendClaimLinkProps) {
  const [channel, setChannel] = useState("");
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<{ ok: boolean; text: string } | null>(null);

  async function send() {
    setBusy(true);
    setResult(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/claim-link`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
        body: JSON.stringify({ channel: channel || null }),
      });
      const payload = (await response.json().catch(() => ({}))) as { message?: string; sentTo?: string; channel?: string; errors?: Record<string, string[]> };
      if (!response.ok) {
        setResult({ ok: false, text: payload.message ?? Object.values(payload.errors ?? {}).flat()[0] ?? "The link could not be sent." });
        return;
      }
      setResult({ ok: true, text: `Link sent by ${payload.channel === "SMS" ? "text message" : payload.channel === "Email" ? "email" : "WhatsApp"} to ${payload.sentTo}. It works once and for 24 hours.` });
    } catch {
      setResult({ ok: false, text: "The service could not be reached. Check your connection and try again." });
    } finally {
      setBusy(false);
    }
  }

  return (
    <div data-testid="send-claim-link">
      <div className="text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Send claim link</div>
      <p className="mt-1 text-[12.5px] text-dls-text-secondary">Sends the customer a secure link to this claim, to see what is needed and send documents.</p>
      <div className="mt-2 flex flex-wrap items-center gap-2">
        <Select aria-label="Send the link by" value={channel} onChange={(event) => setChannel(event.target.value)}>
          <option value="">The way the claim came in</option>
          <option value="SMS">Text message</option>
          <option value="Email">Email</option>
          <option value="WhatsApp">WhatsApp</option>
        </Select>
        <Button variant="secondary" disabled={busy} onClick={() => void send()}>Send claim link</Button>
      </div>
      {result && (
        <p role={result.ok ? "status" : "alert"} className={`mt-2 text-[12.5px] ${result.ok ? "text-dls-text-secondary" : "text-dls-danger-text"}`}>{result.text}</p>
      )}
    </div>
  );
}
