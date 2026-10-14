"use client";

import { useCallback, useEffect, useState } from "react";
import { AlertTriangle, CheckCircle2, Info, Loader2, SearchCheck } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { LoadingSkeleton } from "@/components/ui/StateSurfaces";
import { cn } from "@/lib/utils";
import { checkWhatsAppWithMeta, getWhatsAppReceipts, type DeliveryFinding, type WhatsAppReceipts } from "@/lib/whatsappSettings";

// Is Meta actually sending messages to IIP? A saved, validated connection says the credentials work; it cannot say
// that messages arrive. This shows the calls IIP has received and lets an administrator ask Meta where it sends them.

const OUTCOME: Record<string, string> = {
  accepted: "Message saved",
  duplicate_only: "Already had it",
  refused_signature: "Refused: signature did not match",
  no_messages: "Status update only",
};

function ago(value: string | null): string {
  if (!value) return "never";
  const minutes = Math.max(0, Math.round((Date.now() - new Date(value).getTime()) / 60000));
  if (minutes < 1) return "just now";
  if (minutes < 60) return `${minutes} minute${minutes === 1 ? "" : "s"} ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 48) return `${hours} hour${hours === 1 ? "" : "s"} ago`;
  return `${Math.round(hours / 24)} days ago`;
}

export default function DeliveryPanel({ role }: { role: string }) {
  const [receipts, setReceipts] = useState<WhatsAppReceipts | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [checking, setChecking] = useState(false);
  const [findings, setFindings] = useState<DeliveryFinding[] | null>(null);
  const [checkError, setCheckError] = useState<string | null>(null);

  const load = useCallback(async () => {
    const result = await getWhatsAppReceipts(role);
    if (result.ok) {
      setReceipts(result.value);
      setError(null);
    } else {
      setError(result.error);
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  async function check() {
    setChecking(true);
    setCheckError(null);
    const result = await checkWhatsAppWithMeta(role);
    setChecking(false);
    if (result.ok) setFindings(result.value.findings);
    else setCheckError(result.error);
  }

  const refused = receipts?.receipts.some((item) => item.outcome === "refused_signature");

  return (
    <section aria-label="Messages from Meta" data-testid="whatsapp-delivery" className="mt-4 rounded-dls-control border border-dls-border-subtle p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h3 className="m-0 text-[13px] font-bold text-dls-text-primary">Are messages reaching IIP?</h3>
          {receipts ? (
            <p className="m-0 mt-1 text-[12px] text-dls-text-secondary" data-testid="whatsapp-last-message">
              Last message received: <strong>{ago(receipts.lastMessageAt)}</strong>. Last call from Meta: {ago(receipts.lastCallAt)}.
            </p>
          ) : null}
        </div>
        <Button variant="secondary" onClick={() => void check()} disabled={checking} icon={checking ? <Loader2 className="animate-spin" aria-hidden="true" /> : <SearchCheck aria-hidden="true" />}>
          Check with Meta
        </Button>
      </div>

      {!receipts && !error ? <div className="mt-3"><LoadingSkeleton rows={2} /></div> : null}
      {error ? <p role="alert" className="m-0 mt-3 text-[12px] text-dls-danger-text">{error}</p> : null}
      {receipts && receipts.receipts.length === 0 ? (
        <p className="m-0 mt-3 text-[12px] text-dls-text-muted">No call from Meta has been recorded yet. If customers have written to this number, Meta is not sending those messages to IIP. Use the check to see why.</p>
      ) : null}
      {refused ? (
        <p role="alert" className="m-0 mt-3 text-[12px] text-dls-danger-text">Some calls were refused because their signature did not match. The app secret saved here may not be the one for the Meta app that sends the messages.</p>
      ) : null}

      {receipts && receipts.receipts.length > 0 ? (
        <table className="mt-3 w-full text-left text-[12px]">
          <thead>
            <tr className="text-dls-text-muted">
              <th className="py-1 font-semibold">When</th>
              <th className="py-1 font-semibold">Result</th>
              <th className="py-1 text-right font-semibold">Messages</th>
            </tr>
          </thead>
          <tbody>
            {receipts.receipts.slice(0, 8).map((item) => (
              <tr key={`${item.receivedAt}-${item.outcome}`} className="border-t border-dls-border-subtle">
                <td className="py-1.5 text-dls-text-secondary">{new Date(item.receivedAt).toLocaleString()}</td>
                <td className={cn("py-1.5", item.outcome === "refused_signature" ? "text-dls-danger-text" : "text-dls-text-primary")}>{OUTCOME[item.outcome] ?? item.outcome}</td>
                <td className="py-1.5 text-right text-dls-text-secondary">{item.messagesInCall}</td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}

      {checkError ? <p role="alert" className="m-0 mt-3 text-[12px] text-dls-danger-text">{checkError}</p> : null}
      {findings ? (
        <ul data-testid="whatsapp-findings" className="m-0 mt-3 list-none space-y-2 p-0">
          {findings.map((finding, index) => (
            <li key={index} className={cn("flex items-start gap-2 rounded-dls-control p-2.5 text-[12px]", finding.level === "problem" ? "bg-dls-danger-tint text-dls-danger-text" : finding.level === "warning" ? "bg-dls-warning-tint text-dls-warning-text" : "bg-dls-success-tint text-dls-success-text")}>
              {finding.level === "ok" ? <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" /> : finding.level === "warning" ? <Info className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" /> : <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />}
              <span>{finding.text}</span>
            </li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}
