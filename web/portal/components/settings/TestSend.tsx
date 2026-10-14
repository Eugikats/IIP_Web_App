"use client";

import { useState } from "react";
import { Loader2 } from "lucide-react";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { getEntraApiAccessToken } from "@/lib/entraAuth";
import { cn } from "@/lib/utils";

// Sends one fixed test message through a saved connection, so the only proof that counts, a message arriving, is
// available from the card. The text is set by the server and says it is a test. A few sends an hour are allowed.

type Channel = "email" | "sms";

const COPY: Record<Channel, { label: string; placeholder: string; type: string; inputLabel: string }> = {
  email: { label: "Send a test email", placeholder: "you@yourcompany.com", type: "email", inputLabel: "Send the test email to" },
  sms: { label: "Send a test message", placeholder: "+256700000000", type: "tel", inputLabel: "Send the test message to" },
};

export default function TestSend({ channel, role, name }: { channel: Channel; role: string; name: string }) {
  const copy = COPY[channel];
  const [open, setOpen] = useState(false);
  const [recipient, setRecipient] = useState("");
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<{ tone: "ok" | "bad"; text: string } | null>(null);

  async function send() {
    setBusy(true);
    setResult(null);
    try {
      const token = await getEntraApiAccessToken();
      const response = await fetch(`${apiBaseUrl}/api/v1/integrations/${channel}/test-send`, {
        method: "POST",
        cache: "no-store",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}) },
        body: JSON.stringify({ recipient }),
      });
      const body = (await response.json().catch(() => ({}))) as { accepted?: boolean; message?: string; errors?: Record<string, string[]> };
      if (response.ok) {
        setResult({ tone: body.accepted ? "ok" : "bad", text: body.message ?? (body.accepted ? "Sent." : "It was not sent.") });
      } else {
        setResult({ tone: "bad", text: body.message ?? Object.values(body.errors ?? {})[0]?.[0] ?? "The test could not be sent." });
      }
    } catch {
      setResult({ tone: "bad", text: "The test could not be sent. Check your connection and try again." });
    } finally {
      setBusy(false);
    }
  }

  if (!open) {
    return (
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="mt-3 ml-2 inline-flex min-h-9 items-center rounded-lg border border-gray-200 bg-white px-3 text-[13px] font-semibold text-slate-700 transition-colors hover:bg-slate-50"
      >
        {copy.label}
      </button>
    );
  }

  return (
    <div className="mt-3 rounded-lg border border-gray-200 bg-slate-50 p-3" data-testid={`test-send-${channel}`}>
      <label className="block text-[12.5px] font-medium text-slate-700">
        {copy.inputLabel}
        <input
          type={copy.type}
          value={recipient}
          onChange={(event) => { setRecipient(event.target.value); setResult(null); }}
          placeholder={copy.placeholder}
          aria-label={`${name}: ${copy.inputLabel}`}
          className="mt-1 block h-9 w-full rounded-lg border border-gray-200 bg-white px-3 text-[13px] text-slate-900"
        />
      </label>
      <p className="m-0 mt-1.5 text-[12px] text-slate-500">A short message that says it is a test is sent through this connection. Up to three tests an hour.</p>
      <div className="mt-2 flex flex-wrap items-center gap-2">
        <button
          type="button"
          disabled={busy || recipient.trim().length < 5}
          onClick={() => void send()}
          className="inline-flex h-9 items-center gap-2 rounded-lg bg-dls-primary-accent px-3 text-[13px] font-semibold text-white disabled:cursor-not-allowed disabled:opacity-50"
        >
          {busy ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : null}
          Send test
        </button>
        <button type="button" onClick={() => { setOpen(false); setResult(null); }} className="inline-flex h-9 items-center rounded-lg px-2 text-[13px] font-semibold text-slate-600 hover:bg-white">
          Cancel
        </button>
      </div>
      {result ? <p role="status" className={cn("m-0 mt-2 text-[12.5px]", result.tone === "ok" ? "text-emerald-700" : "text-red-700")}>{result.text}</p> : null}
    </div>
  );
}
