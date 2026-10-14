"use client";

import { useCallback, useEffect, useState } from "react";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { StatusPill } from "@/components/ui/StatusPill";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { getEntraApiAccessToken } from "@/lib/entraAuth";
import { cn } from "@/lib/utils";

// A legal hold stops retention from deleting anything in this workspace, for a claim in dispute, an investigation or
// litigation. Placing and releasing it needs a reason, and both are written to the audit trail.

type Hold = { active: boolean; reason: string | null; placedBy: string | null; placedAt: string | null; releasedBy: string | null; releasedAt: string | null };

async function call(method: "GET" | "PUT", role: string, body?: unknown): Promise<Hold> {
  const token = await getEntraApiAccessToken();
  const response = await fetch(`${apiBaseUrl}/api/v1/compliance/legal-hold`, {
    method,
    cache: "no-store",
    headers: { ...scopedHeaders(role, ""), ...(body ? { "Content-Type": "application/json" } : {}), ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  });
  if (response.ok) return (await response.json()) as Hold;
  const failure = (await response.json().catch(() => ({}))) as { message?: string; errors?: Record<string, string[]> };
  throw new Error(failure.message ?? Object.values(failure.errors ?? {})[0]?.[0] ?? "That did not work.");
}

export default function LegalHoldPanel({ role }: { role: string }) {
  const [hold, setHold] = useState<Hold | null>(null);
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ tone: "ok" | "bad"; text: string } | null>(null);

  const load = useCallback(async () => {
    try {
      setHold(await call("GET", role));
    } catch (failure) {
      setMessage({ tone: "bad", text: failure instanceof Error ? failure.message : "The legal hold could not be loaded." });
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  if (!hold) return message ? <p role="alert" className="m-0 mt-6 text-[12.5px] text-dls-danger-text">{message.text}</p> : null;

  async function submit(active: boolean) {
    setBusy(true);
    setMessage(null);
    try {
      setHold(await call("PUT", role, { active, reason }));
      setReason("");
      setMessage({ tone: "ok", text: active ? "The legal hold is in place. Nothing will be deleted while it is." : "The legal hold was released. Normal retention resumes at its next run." });
    } catch (failure) {
      setMessage({ tone: "bad", text: failure instanceof Error ? failure.message : "That did not work." });
    } finally {
      setBusy(false);
    }
  }

  return (
    <section aria-label="Legal hold" data-testid="legal-hold" className="mt-8 border-t border-dls-border-subtle pt-6">
      <div className="flex flex-wrap items-center gap-3">
        <h3 className="m-0 text-[16px] font-bold text-dls-text-primary">Legal hold</h3>
        <StatusPill tone={hold.active ? "warning" : "neutral"} label={hold.active ? "In place" : "Not in place"} />
      </div>
      <p className="m-0 mt-1 max-w-2xl text-[13px] text-dls-text-secondary">
        While a hold is in place, retention deletes nothing in this workspace. Use it for a claim in dispute, an investigation or litigation. Placing and releasing it needs a reason, and both are recorded.
      </p>
      {hold.active ? (
        <p className="m-0 mt-2 text-[12.5px] text-dls-text-muted">
          Placed{hold.placedAt ? ` on ${new Date(hold.placedAt).toLocaleDateString()}` : ""}{hold.placedBy ? ` by ${hold.placedBy}` : ""}: {hold.reason}
        </p>
      ) : null}
      <div className="mt-3 flex flex-wrap items-start gap-2">
        <div className="w-96 max-w-full">
          <Input value={reason} onChange={(event) => setReason(event.target.value)} placeholder={hold.active ? "Why it is being released" : "Why a hold is needed"} aria-label="Reason" />
        </div>
        <Button variant={hold.active ? "secondary" : "primary"} disabled={busy || reason.trim().length < 5} onClick={() => void submit(!hold.active)}>
          {busy ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : null}
          {hold.active ? "Release the hold" : "Place a legal hold"}
        </Button>
      </div>
      {message ? <p role="status" className={cn("m-0 mt-2 text-[12.5px]", message.tone === "ok" ? "text-dls-success-text" : "text-dls-danger-text")}>{message.text}</p> : null}
    </section>
  );
}
