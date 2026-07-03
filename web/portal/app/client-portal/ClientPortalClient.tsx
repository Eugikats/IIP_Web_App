"use client";

import { ChangeEvent, FormEvent, useMemo, useRef, useState } from "react";
import type { ReactNode } from "react";
import { CheckCircle2, Circle, FileText, MessageSquareText, Paperclip, Send, UploadCloud } from "lucide-react";
import { StatusBadge } from "@/components/Badges";
import { EmptyState, ErrorState } from "@/components/States";
import { useRole } from "@/components/RoleContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

type ClientPortalClaim = {
  caseId: string;
  productLine: string;
  claimType: string;
  status: string;
  policyNumber: string;
  incidentDate: string;
  createdAt: string;
  updatedAt: string;
  completenessScore: number;
  requiredDocuments: Array<{
    documentType: string;
    label: string;
    uploaded: boolean;
    status: string;
  }>;
  timeline: Array<{
    label: string;
    complete: boolean;
    date: string | null;
  }>;
  messages: Array<{
    communicationId: string;
    channel: string;
    direction: string;
    messageBody: string;
    createdAt: string;
    deliveryStatus: string;
  }>;
  nextSteps: string[];
};

export function ClientPortalClient() {
  const { displayName, role, userId } = useRole();
  const [claimId, setClaimId] = useState("");
  const [claim, setClaim] = useState<ClientPortalClaim | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  const firstName = useMemo(() => displayName.split(" ")[0] || "there", [displayName]);
  const missingCount = claim?.requiredDocuments.filter((document) => !document.uploaded).length ?? 0;

  async function openClaim(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const trimmedClaimId = claimId.trim();
    if (!trimmedClaimId) {
      setError("Enter a claim reference to open your customer-safe claim view.");
      return;
    }

    setClaim(null);
    setError(null);
    setStatus("Loading your claim...");

    const response = await fetch(`${apiBaseUrl}/api/v1/client-portal/claims/${trimmedClaimId}`, {
      cache: "no-store",
      headers: scopedHeaders(role, userId)
    });

    if (!response.ok) {
      setStatus(null);
      setError(response.status === 404
        ? "We could not find a claim for this signed-in customer."
        : `Claim lookup failed with HTTP ${response.status}.`);
      return;
    }

    setClaim((await response.json()) as ClientPortalClaim);
    setStatus(null);
  }

  async function uploadDocuments(event: ChangeEvent<HTMLInputElement>) {
    if (!claim || !event.target.files?.length) {
      return;
    }

    const formData = new FormData();
    for (const file of Array.from(event.target.files)) {
      formData.append("files", file);
    }

    setError(null);
    setStatus("Uploading documents...");
    const response = await fetch(`${apiBaseUrl}/api/v1/client-portal/claims/${claim.caseId}/documents`, {
      method: "POST",
      headers: scopedHeaders(role, userId),
      body: formData
    });

    event.target.value = "";

    if (!response.ok) {
      setStatus(null);
      setError(`Document upload failed with HTTP ${response.status}.`);
      return;
    }

    setClaim((await response.json()) as ClientPortalClaim);
    setStatus("Documents uploaded and sent for review.");
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <p className="text-sm font-medium text-muted">Welcome back, {firstName}</p>
          <h1 className="mt-1 text-2xl font-bold tracking-tight text-ink">My Claims</h1>
        </div>
        <form onSubmit={openClaim} className="flex w-full flex-col gap-2 sm:flex-row lg:max-w-xl" aria-label="Open customer claim">
          <label className="sr-only" htmlFor="client-claim-reference">Claim reference</label>
          <input
            id="client-claim-reference"
            name="claimReference"
            className="min-h-11 flex-1 rounded-xl border border-line bg-surface px-4 text-sm text-ink outline-none transition focus:border-accent focus:ring-1 focus:ring-accent"
            value={claimId}
            onChange={(event) => setClaimId(event.target.value)}
            placeholder="Enter your claim reference..."
            autoComplete="off"
          />
          <button type="submit" className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-azure px-4 text-sm font-semibold text-white transition hover:brightness-110">
            <FileText className="h-4 w-4" />
            Open claim
          </button>
        </form>
      </div>

      {error ? <ErrorState message={error} /> : null}
      {status ? <div className="rounded-xl border border-accent/30 bg-accent/10 px-4 py-3 text-sm text-accent" role="status" aria-live="polite">{status}</div> : null}

      {!claim ? (
        <EmptyState title="No claim selected" description="Open a claim by reference to view customer-safe status, required documents, messages, and next steps." />
      ) : (
        <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_380px]">
          <section className="space-y-6" aria-label="Claim status and assistant">
            <section className="rounded-xl border border-line bg-surface p-5 shadow-card">
              <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <div className="flex flex-wrap items-center gap-2">
                    <h2 className="text-lg font-semibold text-ink">{claim.productLine} Claim</h2>
                    <span className="text-sm font-semibold text-muted">/</span>
                    <span className="font-mono text-sm font-semibold text-ink">{claim.caseId}</span>
                    <StatusBadge label={claim.status} />
                  </div>
                  <p className="mt-2 text-sm text-muted">Claim reported on {formatDate(claim.createdAt)}</p>
                </div>
                <div className="rounded-xl border border-line bg-mist px-4 py-3 text-right">
                  <p className="text-[10px] font-bold uppercase tracking-wider text-muted">Completeness</p>
                  <p className="mt-1 text-2xl font-bold text-ink">{Math.round(claim.completenessScore)}%</p>
                </div>
              </div>

              <div className="mt-6 grid gap-3 md:grid-cols-5">
                {claim.timeline.map((step, index) => (
                  <div key={step.label} className="rounded-xl border border-line bg-shell p-4">
                    <div className="flex items-center gap-2">
                      {step.complete ? <CheckCircle2 className="h-5 w-5 text-success" /> : <Circle className="h-5 w-5 text-muted-soft" />}
                      <span className="text-sm font-semibold text-ink">{step.label}</span>
                    </div>
                    <p className="mt-2 text-xs text-muted">{step.date ? formatDate(step.date) : index === 0 ? "Recorded" : "Pending"}</p>
                  </div>
                ))}
              </div>
            </section>

            <section className="grid min-h-[520px] overflow-hidden rounded-xl border border-line bg-surface shadow-card lg:grid-cols-[280px_minmax(0,1fr)]">
              <section aria-label="Customer-safe assistant" className="border-b border-line bg-mist p-5 lg:border-b-0 lg:border-r">
                <div className="flex items-center gap-2">
                  <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-brand-gradient text-white">
                    <MessageSquareText className="h-4 w-4" />
                  </div>
                  <div>
                    <h2 className="text-sm font-semibold text-ink">IIP Assistant</h2>
                    <p className="text-xs text-muted">Customer-safe claim help</p>
                  </div>
                </div>
                <div className="mt-5 space-y-3">
                  {["What documents are needed?", "What is my claim status?", "What happens next?"].map((question) => (
                    <div key={question} className="rounded-xl border border-line bg-surface px-3 py-3 text-sm text-ink">
                      {question}
                    </div>
                  ))}
                </div>
                <p className="mt-6 text-xs leading-6 text-muted">Assistant answers must come from approved customer-safe claim and policy data. This page does not fabricate answers when an endpoint is unavailable.</p>
              </section>

              <div className="flex min-h-0 flex-col p-5">
                <div className="flex-1 space-y-5 overflow-y-auto pr-1 custom-scrollbar">
                  <ChatBubble side="assistant">
                    Your claim status is <strong>{claim.status}</strong>. {missingCount > 0 ? `There ${missingCount === 1 ? "is" : "are"} ${missingCount} required document${missingCount === 1 ? "" : "s"} still needed.` : "No required documents are missing right now."}
                  </ChatBubble>
                  {claim.messages.map((message) => (
                    <ChatBubble key={message.communicationId} side={message.direction === "Inbound" ? "user" : "assistant"} meta={`${message.channel} / ${formatDateTime(message.createdAt)}`}>
                      {message.messageBody}
                    </ChatBubble>
                  ))}
                </div>

                <div className="mt-4 rounded-xl border border-line bg-shell p-3">
                  <div className="flex items-center gap-2">
                    <label className="sr-only" htmlFor="customer-assistant-input">Customer assistant question</label>
                    <input id="customer-assistant-input" className="min-h-10 flex-1 bg-transparent px-2 text-sm text-muted outline-none" disabled placeholder="Customer assistant endpoint not configured yet" />
                    <button type="button" className="flex h-10 w-10 items-center justify-center rounded-lg text-muted" disabled title="Customer assistant endpoint not configured" aria-label="Attach file to customer assistant">
                      <Paperclip className="h-4 w-4" />
                    </button>
                    <button type="button" className="flex h-10 w-10 items-center justify-center rounded-lg text-muted" disabled title="Customer assistant endpoint not configured" aria-label="Send customer assistant question">
                      <Send className="h-4 w-4" />
                    </button>
                  </div>
                </div>
              </div>
            </section>
          </section>

          <section aria-label="Customer claim details" className="space-y-5">
            <Panel title="Required Documents" action={`${missingCount} pending`}>
              <div className="space-y-2">
                {claim.requiredDocuments.map((document) => (
                  <div key={document.documentType} className="flex items-center justify-between gap-3 rounded-xl border border-line bg-mist px-3 py-3">
                    <div className="flex min-w-0 items-center gap-3">
                      <span className="flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-lg bg-surface text-azure">
                        <FileText className="h-4 w-4" />
                      </span>
                      <span className="truncate text-sm font-medium text-ink">{document.label}</span>
                    </div>
                    <StatusBadge label={document.status} />
                  </div>
                ))}
              </div>
              <input
                ref={fileInputRef}
                className="hidden"
                type="file"
                multiple
                accept=".pdf,.png,.jpg,.jpeg,.txt"
                aria-label="Upload claim documents"
                onChange={uploadDocuments}
              />
              <button type="button" className="mt-4 inline-flex items-center gap-2 text-sm font-semibold text-azure" onClick={() => fileInputRef.current?.click()}>
                <UploadCloud className="h-4 w-4" />
                Upload Documents
              </button>
            </Panel>

            <Panel title="Recent Messages" action={claim.messages.length ? "View all" : undefined}>
              {claim.messages.length === 0 ? (
                <EmptyState title="No messages yet" description="Customer-safe messages will appear here after they are sent through a configured provider." />
              ) : (
                <div className="space-y-3">
                  {claim.messages.slice(0, 3).map((message) => (
                    <div key={message.communicationId} className="rounded-xl border border-line bg-mist p-3">
                      <p className="text-sm font-semibold text-ink">{message.channel} update</p>
                      <p className="mt-1 line-clamp-2 text-xs text-muted">{message.messageBody}</p>
                      <p className="mt-2 text-xs text-muted">{formatDate(message.createdAt)}</p>
                    </div>
                  ))}
                </div>
              )}
            </Panel>

            <Panel title="Next Steps">
              <div className="space-y-3">
                {claim.nextSteps.map((step, index) => (
                  <div key={step} className="flex gap-3">
                    {index === 0 ? <CheckCircle2 className="mt-0.5 h-5 w-5 flex-shrink-0 text-success" /> : <Circle className="mt-0.5 h-5 w-5 flex-shrink-0 text-muted-soft" />}
                    <p className="text-sm text-ink">{step}</p>
                  </div>
                ))}
              </div>
            </Panel>
          </section>
        </div>
      )}
    </div>
  );
}

function Panel({ title, action, children }: { title: string; action?: string; children: ReactNode }) {
  return (
    <section className="rounded-xl border border-line bg-surface p-5 shadow-card">
      <div className="mb-4 flex items-center justify-between gap-3">
        <h2 className="text-base font-semibold text-ink">{title}</h2>
        {action ? <span className="rounded-full border border-line bg-mist px-3 py-1 text-xs font-semibold text-muted">{action}</span> : null}
      </div>
      {children}
    </section>
  );
}

function ChatBubble({ side, meta, children }: { side: "assistant" | "user"; meta?: string; children: ReactNode }) {
  const isUser = side === "user";

  return (
    <div className={`flex ${isUser ? "justify-end" : "justify-start"}`}>
      <div className={`max-w-[min(620px,85%)] rounded-2xl px-4 py-3 text-sm leading-6 ${isUser ? "bg-azure/10 text-ink" : "border border-line bg-mist text-ink"}`}>
        <div>{children}</div>
        {meta ? <p className="mt-2 text-right text-xs text-muted">{meta}</p> : null}
      </div>
    </div>
  );
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, { day: "2-digit", month: "short", year: "numeric" }).format(new Date(value));
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat(undefined, { day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" }).format(new Date(value));
}
