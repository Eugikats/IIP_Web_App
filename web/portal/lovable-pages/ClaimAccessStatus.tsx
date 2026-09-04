"use client";

import { useCallback, useEffect, useState } from "react";
import type { ChangeEvent, FormEvent } from "react";
import { useRouter } from "next/navigation";
import { AlertTriangle, CheckCircle2, Circle, Loader2, LogOut, MessageSquare, Paperclip, ShieldCheck, UploadCloud } from "lucide-react";
import iipLogo from "@/assets/iip-logo";
import {
  type ClientPortalClaim,
  type ClientPortalSession,
  clearStoredSession,
  getClaim,
  loadStoredSession,
  requestClaimReview,
  uploadClaimDocuments,
} from "@/lib/clientPortal";

function formatDate(value: string | null) {
  if (!value) return null;
  try {
    return new Date(value).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
  } catch {
    return value;
  }
}

export default function ClaimAccessStatus() {
  const router = useRouter();
  const [session, setSession] = useState<ClientPortalSession | null>(null);
  const [claim, setClaim] = useState<ClientPortalClaim | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);
  const [uploadNotice, setUploadNotice] = useState<string | null>(null);
  const [reviewReason, setReviewReason] = useState("");
  const [reviewBusy, setReviewBusy] = useState(false);
  const [reviewNotice, setReviewNotice] = useState<string | null>(null);

  const load = useCallback(async (activeSession: ClientPortalSession) => {
    setLoading(true);
    setError(null);
    const result = await getClaim(activeSession);
    setLoading(false);
    if (!result.ok) {
      setError(result.error);
      if (result.errorKind === "unauthorized") {
        clearStoredSession();
      }
      return;
    }
    setClaim(result.claim);
  }, []);

  useEffect(() => {
    const stored = loadStoredSession();
    if (!stored) {
      router.replace("/access");
      return;
    }
    if (!stored.caseId) {
      router.replace("/access/new-claim");
      return;
    }
    setSession(stored);
    void load(stored);
  }, [load, router]);

  async function onUpload(event: ChangeEvent<HTMLInputElement>) {
    const files = event.target.files ? Array.from(event.target.files) : [];
    event.target.value = "";
    if (!session || files.length === 0) return;

    setUploading(true);
    setUploadNotice(null);
    const result = await uploadClaimDocuments(session, files);
    setUploading(false);
    if (!result.ok) {
      setUploadNotice(result.error);
      return;
    }
    setClaim(result.claim);
    setUploadNotice(`${files.length === 1 ? "Document" : `${files.length} documents`} uploaded.`);
  }

  async function onRequestReview(event: FormEvent) {
    event.preventDefault();
    if (!session || !reviewReason.trim()) return;

    setReviewBusy(true);
    setReviewNotice(null);
    const result = await requestClaimReview(session, reviewReason.trim());
    setReviewBusy(false);
    if (!result.ok) {
      setReviewNotice(result.error);
      return;
    }
    setReviewReason("");
    setReviewNotice("Your request has been sent to our claims team.");
    void load(session);
  }

  function signOut() {
    clearStoredSession();
    router.replace("/access");
  }

  if (!session) return null;

  return (
    <main className="min-h-screen bg-mist/40 text-ink">
      <header className="border-b border-line bg-white">
        <div className="mx-auto flex max-w-3xl items-center justify-between px-6 py-4">
          <div className="flex items-center gap-3">
            <span className="grid h-9 w-9 place-items-center rounded-md border border-line bg-white">
              <img src={iipLogo.url} alt="IIP" className="h-7 w-7 object-contain" />
            </span>
            <div>
              <div className="text-sm font-semibold">Your claim</div>
              <div className="text-xs text-muted">Insurance Intelligence Platform</div>
            </div>
          </div>
          <button
            type="button"
            onClick={signOut}
            className="inline-flex min-h-9 items-center gap-1.5 rounded-md border border-line bg-white px-3 py-1.5 text-xs font-medium text-ink-soft focus-ring"
          >
            <LogOut className="h-3.5 w-3.5" /> Done
          </button>
        </div>
      </header>

      <div className="mx-auto max-w-3xl px-6 py-8">
        {loading && (
          <div className="flex items-center justify-center gap-2 py-24 text-sm text-muted">
            <Loader2 className="h-4 w-4 animate-spin" /> Loading your claim…
          </div>
        )}

        {!loading && error && (
          <div className="rounded-xl border border-danger/25 bg-danger/7 p-6 text-center">
            <AlertTriangle className="mx-auto h-6 w-6 text-danger" />
            <p className="mt-3 text-sm leading-6 text-danger">{error}</p>
            <a
              href="/access"
              className="mt-5 inline-flex min-h-11 items-center justify-center rounded-md bg-primary px-5 py-2.5 text-sm font-semibold text-white shadow-l2 hover:bg-primary/90 focus-ring"
            >
              Request a new access link
            </a>
          </div>
        )}

        {!loading && claim && (
          <div className="space-y-5">
            <div className="rounded-xl border border-line bg-white p-6">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <div className="text-xs uppercase tracking-[0.12em] text-muted">{claim.productLine} · {claim.claimType}</div>
                  <div className="mt-1 text-2xl font-semibold text-ink">{claim.status.replace(/([a-z])([A-Z])/g, "$1 $2")}</div>
                </div>
                <div className="text-right text-xs text-muted">
                  <div>Policy {claim.policyNumber}</div>
                  <div>Reported {formatDate(claim.createdAt)}</div>
                </div>
              </div>
              <div className="mt-5">
                <div className="flex items-center justify-between text-xs text-muted">
                  <span>Completeness</span>
                  <span>{Math.round(claim.completenessScore)}%</span>
                </div>
                <div className="mt-1.5 h-2 overflow-hidden rounded-full bg-line/60">
                  <div className="h-full rounded-full bg-primary" style={{ width: `${Math.min(100, Math.max(0, claim.completenessScore))}%` }} />
                </div>
              </div>
            </div>

            <div className="rounded-xl border border-line bg-white p-6">
              <div className="text-xs uppercase tracking-[0.12em] text-muted">What happens next</div>
              <ul className="mt-3 space-y-2">
                {claim.nextSteps.map((step) => (
                  <li key={step} className="flex items-start gap-2 text-sm leading-5 text-ink-soft">
                    <Circle className="mt-1 h-1.5 w-1.5 shrink-0 fill-current text-muted" />
                    {step}
                  </li>
                ))}
              </ul>
              <div className="mt-5 border-t border-line pt-5">
                <div className="mb-3 flex items-center gap-2">
                  {claim.timeline.map((step, index) => (
                    <div key={step.label} className="flex flex-1 items-center gap-2">
                      <div className={`grid h-6 w-6 shrink-0 place-items-center rounded-full text-[10px] font-semibold ${step.complete ? "bg-success/15 text-success" : "bg-line/60 text-muted"}`}>
                        {step.complete ? <CheckCircle2 className="h-3.5 w-3.5" /> : index + 1}
                      </div>
                      {index < claim.timeline.length - 1 && <div className={`h-px flex-1 ${step.complete ? "bg-success/40" : "bg-line"}`} />}
                    </div>
                  ))}
                </div>
                <div className="grid grid-cols-5 gap-1 text-center text-[10.5px] leading-4 text-muted">
                  {claim.timeline.map((step) => (
                    <div key={step.label}>{step.label}</div>
                  ))}
                </div>
              </div>
            </div>

            <div className="rounded-xl border border-line bg-white p-6">
              <div className="flex items-center justify-between">
                <div className="text-xs uppercase tracking-[0.12em] text-muted">Documents</div>
                <label className="inline-flex min-h-9 cursor-pointer items-center gap-1.5 rounded-md border border-line bg-white px-3 py-1.5 text-xs font-medium text-ink-soft focus-ring">
                  {uploading ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <UploadCloud className="h-3.5 w-3.5" />}
                  Upload
                  <input type="file" multiple className="hidden" onChange={onUpload} disabled={uploading} />
                </label>
              </div>
              <ul className="mt-3 divide-y divide-line">
                {claim.requiredDocuments.map((doc) => (
                  <li key={doc.documentType} className="flex items-center justify-between gap-3 py-2.5 text-sm">
                    <span className="flex items-center gap-2 text-ink-soft">
                      <Paperclip className="h-3.5 w-3.5 text-muted" /> {doc.label}
                    </span>
                    <span className={`text-xs font-medium ${doc.uploaded ? "text-success" : "text-warning"}`}>{doc.status}</span>
                  </li>
                ))}
              </ul>
              {uploadNotice && <p className="mt-3 text-xs leading-5 text-muted">{uploadNotice}</p>}
            </div>

            {claim.messages.length > 0 && (
              <div className="rounded-xl border border-line bg-white p-6">
                <div className="text-xs uppercase tracking-[0.12em] text-muted">Updates</div>
                <ul className="mt-3 space-y-3">
                  {claim.messages.map((message) => (
                    <li key={message.communicationId} className="flex items-start gap-2 text-sm leading-5 text-ink-soft">
                      <MessageSquare className="mt-0.5 h-3.5 w-3.5 shrink-0 text-muted" />
                      <div>
                        <div>{message.messageBody}</div>
                        <div className="mt-0.5 text-[11px] text-muted">{formatDate(message.createdAt)}</div>
                      </div>
                    </li>
                  ))}
                </ul>
              </div>
            )}

            <div className="rounded-xl border border-line bg-white p-6">
              <div className="text-xs uppercase tracking-[0.12em] text-muted">Ask for a human review</div>
              <p className="mt-1 text-xs leading-5 text-muted">
                If something about your claim doesn&apos;t look right, tell us why and our team will take another look.
              </p>
              {claim.reviewRequests.length > 0 && (
                <ul className="mt-3 space-y-1.5">
                  {claim.reviewRequests.map((request) => (
                    <li key={request.requestId} className="flex items-center gap-2 text-xs text-muted">
                      <ShieldCheck className="h-3.5 w-3.5 shrink-0" />
                      {request.status} — {request.reason}
                    </li>
                  ))}
                </ul>
              )}
              <form onSubmit={onRequestReview} className="mt-3 flex flex-col gap-2 sm:flex-row">
                <input
                  value={reviewReason}
                  onChange={(event) => setReviewReason(event.target.value)}
                  placeholder="Briefly explain why you'd like a review"
                  className="min-h-11 flex-1 rounded-md border border-line bg-white px-3 py-2.5 text-sm text-ink placeholder:text-muted focus-ring"
                />
                <button
                  type="submit"
                  disabled={reviewBusy || !reviewReason.trim()}
                  className="inline-flex min-h-11 items-center justify-center gap-2 rounded-md bg-primary px-4 py-2.5 text-sm font-semibold text-white shadow-l2 hover:bg-primary/90 focus-ring disabled:cursor-not-allowed disabled:opacity-55"
                >
                  {reviewBusy ? <Loader2 className="h-4 w-4 animate-spin" /> : "Request review"}
                </button>
              </form>
              {reviewNotice && <p className="mt-3 text-xs leading-5 text-muted">{reviewNotice}</p>}
            </div>
          </div>
        )}
      </div>
    </main>
  );
}
