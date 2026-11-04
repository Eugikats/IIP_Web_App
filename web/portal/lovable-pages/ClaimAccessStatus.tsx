"use client";

import { useCallback, useEffect, useState } from "react";
import type { ChangeEvent, FormEvent } from "react";
import { useRouter } from "next/navigation";
import { AlertTriangle, CheckCircle2, Circle, Loader2, LogOut, MessageSquare, ShieldCheck } from "lucide-react";
import iipLogo from "@/assets/iip-logo";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import {
  type ClientPortalClaim,
  type ClientPortalSession,
  clearStoredSession,
  getClaim,
  loadStoredSession,
  removeClaimDocument,
  reorderClaimPages,
  requestClaimReview,
  retryClaimDocument,
  uploadClaimDocuments,
} from "@/lib/clientPortal";
import { prepareFileForSlot } from "@/lib/photoDownscale";
import { unfinishedUploads, type UnfinishedUpload, type UploadProgress } from "@/lib/resumableUpload";
import { ClaimantDocumentsCard, SOMETHING_ELSE_KEY } from "@/components/iip/ClaimantDocumentsCard";
import { ClaimantConsentCard } from "@/components/iip/ClaimantConsentCard";
import { agreeToPrivacyNotice, fetchPrivacyConsent, withdrawPrivacyConsent, type PrivacyConsent } from "@/lib/privacyConsent";

// "LastExpenseClaim" is a stored name; a person reads "Last expense claim".
function plainClaimType(value: string): string {
  const spaced = value.replace(/([a-z])([A-Z])/g, "$1 $2").replace(/_/g, " ").trim();
  return spaced.length > 0 ? spaced.charAt(0).toUpperCase() + spaced.slice(1).toLowerCase() : spaced;
}

// What the person sees as the state of their claim. Plain words for a customer: the staff vocabulary and the
// raw state name stay inside the platform, and nothing here suggests a decision before one is made.
function customerStatus(status: string): string {
  switch (status) {
    case "New": return "We have your claim";
    case "Assigned": return "Someone is looking at it";
    case "InReview": return "Being reviewed";
    case "MissingDocuments": return "We need more from you";
    case "ReadyForReview": return "Ready for our team to review";
    case "PendingApproval": return "With our team";
    case "ReadyForExport": return "With our team";
    case "Escalated": return "With a senior team member";
    case "Settled": return "Settled";
    case "Closed": return "Closed";
    default: return status.replace(/([a-z])([A-Z])/g, "$1 $2");
  }
}

function formatDate(value: string | null) {
  if (!value) return null;
  try {
    return new Date(value).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
  } catch {
    return value;
  }
}

// Claimant-facing (tier 6, stricter rules). Confirmed while migrating: no internal notes,
// reserves, fraud/risk signals, or other-tenant data render here, claim.status, however, is the
// raw CaseStatus enum name run through a camelCase-to-spaces regex (e.g. "Escalated",
// "ReadyForExport"), not a claimant-appropriate translation. Left unchanged here, inventing
// customer-facing wording for a regulated insurer communication isn't a restyling call to make
// unilaterally; flagged in the ledger for whoever owns claimant-communication copy.
export default function ClaimAccessStatus() {
  const router = useRouter();
  const [session, setSession] = useState<ClientPortalSession | null>(null);
  const [claim, setClaim] = useState<ClientPortalClaim | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busyKey, setBusyKey] = useState<string | null>(null);
  const [uploadNotice, setUploadNotice] = useState<string | null>(null);
  const [uploadFailed, setUploadFailed] = useState(false);
  const [progress, setProgress] = useState<UploadProgress | null>(null);
  const [unfinished, setUnfinished] = useState<UnfinishedUpload[]>([]);
  const claimCaseId = claim?.caseId ?? null;
  // Uploads left half done before a reload are remembered in this browser, and shown against their slot.
  useEffect(() => {
    if (claimCaseId) setUnfinished(unfinishedUploads(claimCaseId));
  }, [claimCaseId]);
  const [reviewReason, setReviewReason] = useState("");
  const [reviewBusy, setReviewBusy] = useState(false);
  const [reviewNotice, setReviewNotice] = useState<string | null>(null);
  const [consent, setConsent] = useState<PrivacyConsent | null>(null);
  const [consentBusy, setConsentBusy] = useState(false);
  const [consentError, setConsentError] = useState<string | null>(null);

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

  // Documents are read in the background after they are sent. While any is "Being checked", look
  // again every couple of seconds (quietly, without replacing the page), for up to three minutes.
  const stillBeingChecked = claim?.requiredDocuments.some((row) => row.status === "Being checked") ?? false;
  useEffect(() => {
    if (!session || !stillBeingChecked) return;
    let attempts = 0;
    const timer = window.setInterval(async () => {
      attempts += 1;
      if (attempts > 90) {
        window.clearInterval(timer);
        return;
      }
      const result = await getClaim(session);
      if (result.ok) setClaim(result.claim);
    }, 2000);
    return () => window.clearInterval(timer);
  }, [session, stillBeingChecked]);

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
    void fetchPrivacyConsent(stored).then((result) => {
      if (result.ok) setConsent(result.consent);
      else setConsentError(result.error);
    });
  }, [load, router]);

  async function onAgree() {
    if (!session || !consent) return;
    setConsentBusy(true);
    setConsentError(null);
    const result = await agreeToPrivacyNotice(session, consent.noticeVersion);
    setConsentBusy(false);
    if (result.ok) {
      setConsent(result.consent);
      return;
    }
    setConsentError(result.error);
    // The notice may have changed while it was open: show the current one.
    const fresh = await fetchPrivacyConsent(session);
    if (fresh.ok) setConsent(fresh.consent);
  }

  async function onWithdrawConsent() {
    if (!session) return;
    setConsentBusy(true);
    setConsentError(null);
    const result = await withdrawPrivacyConsent(session);
    setConsentBusy(false);
    if (result.ok) setConsent(result.consent);
    else setConsentError(result.error);
  }

  async function onUpload(rawFiles: File[], documentTypeKey: string, label: string) {
    if (!session || rawFiles.length === 0) return;

    setBusyKey(documentTypeKey);
    setUploadNotice(null);
    // Only a document slot may have its pictures scaled down; evidence is sent exactly as taken.
    const slotKind = claim?.requiredDocuments.find((row) => row.documentTypeKey === documentTypeKey)?.kind === "Evidence" ? "Evidence" : "Document";
    const files = await Promise.all(rawFiles.map((file) => prepareFileForSlot(file, slotKind)));
    setProgress(null);
    const result = await uploadClaimDocuments(session, files, documentTypeKey === SOMETHING_ELSE_KEY ? undefined : documentTypeKey, { onProgress: setProgress });
    setBusyKey(null);
    setProgress(null);
    if (session.caseId) setUnfinished(unfinishedUploads(session.caseId));
    if (!result.ok) {
      setUploadFailed(true);
      setUploadNotice(result.error);
      // The file may have arrived even though the answer did not: show what is really on the claim.
      const fresh = await getClaim(session);
      if (fresh.ok) setClaim(fresh.claim);
      return;
    }
    setUploadFailed(false);
    setClaim(result.claim);
    setUploadNotice(`${label} sent. Thank you.`);
  }

  async function onRemove(documentTypeKey: string, label: string) {
    if (!session) return;
    setBusyKey(documentTypeKey);
    setUploadNotice(null);
    const result = await removeClaimDocument(session, documentTypeKey);
    setBusyKey(null);
    if (!result.ok) {
      setUploadFailed(true);
      setUploadNotice(result.error);
      return;
    }
    setUploadFailed(false);
    setClaim(result.claim);
    setUploadNotice(`${label} removed.`);
  }

  async function onRemovePage(documentTypeKey: string, documentId: string, label: string) {
    if (!session) return;
    setBusyKey(documentTypeKey);
    setUploadNotice(null);
    const result = await removeClaimDocument(session, documentTypeKey, documentId);
    setBusyKey(null);
    if (!result.ok) {
      setUploadNotice(result.error);
      return;
    }
    setClaim(result.claim);
    setUploadNotice(`A page of ${label} was removed.`);
  }

  async function onMovePage(documentTypeKey: string, documentId: string, delta: -1 | 1) {
    if (!session || !claim) return;
    const files = claim.requiredDocuments.find((row) => row.documentTypeKey === documentTypeKey)?.files ?? [];
    const from = files.findIndex((file) => file.documentId === documentId);
    const to = from + delta;
    if (from < 0 || to < 0 || to >= files.length) return;
    const ids = files.map((file) => file.documentId);
    [ids[from], ids[to]] = [ids[to], ids[from]];
    setBusyKey(documentTypeKey);
    setUploadNotice(null);
    const result = await reorderClaimPages(session, documentTypeKey, ids);
    setBusyKey(null);
    if (!result.ok) {
      setUploadNotice(result.error);
      return;
    }
    setClaim(result.claim);
  }

  async function onRetry(documentTypeKey: string, label: string) {
    if (!session) return;
    setBusyKey(documentTypeKey);
    setUploadNotice(null);
    const result = await retryClaimDocument(session, documentTypeKey);
    setBusyKey(null);
    if (!result.ok) {
      setUploadNotice(result.error);
      return;
    }
    setClaim(result.claim);
    const row = result.claim.requiredDocuments.find((item) => item.documentTypeKey === documentTypeKey);
    setUploadNotice(row?.canRetry ? `${label} still could not be read. You can try again or send it again.` : `${label} checked.`);
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

  const requiredList = claim?.requiredDocuments.filter((doc) => doc.required) ?? [];
  const requiredTotal = claim?.requiredDocumentsTotal ?? 0;
  const requiredSent = claim?.requiredDocumentsReceived ?? 0;
  const stillNeeded = requiredList.filter((doc) => !doc.received).map((doc) => doc.label);

  return (
    <main className="min-h-screen bg-dls-bg-page text-dls-text-primary">
      <header className="border-b border-dls-border-subtle bg-dls-surface">
        <div className="mx-auto flex max-w-3xl items-center justify-between px-6 py-4">
          <div className="flex items-center gap-3">
            <span className="grid h-9 w-9 place-items-center rounded-dls-control border border-dls-border-subtle bg-dls-surface">
              <img src={iipLogo.url} alt="IIP" className="h-7 w-7 object-contain" />
            </span>
            <div>
              <div className="text-sm font-semibold">Your claim</div>
              <div className="text-xs text-dls-text-muted">Insurance Intelligence Platform</div>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <a
              href="/access/policy"
              className="dls-focus-outline inline-flex min-h-9 items-center rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-1.5 text-xs font-semibold text-dls-text-secondary hover:bg-dls-bg-page"
            >
              My policy
            </a>
            <Button variant="secondary" onClick={signOut} icon={<LogOut aria-hidden="true" />}>
              Done
            </Button>
          </div>
        </div>
      </header>

      <div className="mx-auto max-w-3xl px-6 py-8">
        {loading && (
          <div className="flex items-center justify-center gap-2 py-24 text-sm text-dls-text-secondary">
            <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> Loading your claim…
          </div>
        )}

        {!loading && error && (
          <div className="rounded-dls-card bg-dls-danger-tint p-6 text-center">
            <AlertTriangle className="mx-auto h-6 w-6 text-dls-danger-text" aria-hidden="true" />
            <p className="mt-3 text-sm leading-6 text-dls-danger-text">{error}</p>
            <a
              href="/access"
              className="dls-focus-outline mt-5 inline-flex min-h-11 items-center justify-center rounded-dls-control bg-dls-primary px-5 py-2.5 text-sm font-bold text-dls-text-inverse shadow-dls-primary transition-colors duration-dls-fast ease-dls hover:bg-dls-primary-hover"
            >
              Request a new access link
            </a>
          </div>
        )}

        {!loading && claim && (
          <div className="space-y-5">
            <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-6">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <div className="text-xs font-bold uppercase tracking-[0.12em] text-dls-text-muted">{claim.productLine} · {plainClaimType(claim.claimType)}</div>
                  <h1 className="mt-1 text-2xl font-bold text-dls-text-primary" data-testid="claim-status-headline">{claim.statusHeadline || claim.status.replace(/([a-z])([A-Z])/g, "$1 $2")}</h1>
                  {claim.statusDetail && <p className="mt-2 max-w-xl text-sm leading-5 text-dls-text-secondary" data-testid="claim-status-detail">{claim.statusDetail}</p>}
                </div>
                <div className="text-right text-xs text-dls-text-muted">
                  <div>Policy {claim.policyNumber}</div>
                  <div>Reported {formatDate(claim.createdAt)}</div>
                </div>
              </div>
              <div className="mt-5">
                <div className="flex items-center justify-between text-xs text-dls-text-muted">
                  <span>Documents we asked for</span>
                  <span>{requiredTotal > 0 ? `${requiredSent} of ${requiredTotal} sent` : `${Math.round(claim.completenessScore)}%`}</span>
                </div>
                {/* Flat fill, not a gradient, §4.5 restricts gradients to the orb and chart bars. */}
                <div className="mt-1.5 h-2 overflow-hidden rounded-full bg-dls-bg-page">
                  <div className="h-full rounded-full bg-dls-primary" style={{ width: `${requiredTotal > 0 ? Math.round((requiredSent / requiredTotal) * 100) : Math.min(100, Math.max(0, claim.completenessScore))}%` }} />
                </div>
              </div>
            </div>

            <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-6">
              <div className="text-xs font-bold uppercase tracking-[0.12em] text-dls-text-muted">What happens next</div>
              <ul className="mt-3 space-y-2">
                {claim.nextSteps.map((step) => (
                  <li key={step} className="flex items-start gap-2 text-sm leading-5 text-dls-text-secondary">
                    <Circle className="mt-1 h-1.5 w-1.5 shrink-0 fill-current text-dls-text-muted" aria-hidden="true" />
                    {step}
                  </li>
                ))}
              </ul>
              <div className="mt-5 border-t border-dls-border-subtle pt-5">
                <ol className="space-y-3" aria-label="Where your claim is" data-testid="claim-timeline">
                  {claim.timeline.map((step, index) => {
                    const state = step.state ?? (step.complete ? "Done" : "Upcoming");
                    return (
                      <li key={step.label} className="flex items-center gap-3" data-state={state} aria-current={state === "Current" ? "step" : undefined}>
                        <div className={`grid h-6 w-6 shrink-0 place-items-center rounded-full text-[10px] font-bold ${state === "Done" ? "bg-dls-success-tint text-dls-success-text" : state === "Current" ? "bg-dls-primary text-white" : "bg-dls-bg-page text-dls-text-muted"}`}>
                          {state === "Done" ? <CheckCircle2 className="h-3.5 w-3.5" aria-hidden="true" /> : index + 1}
                        </div>
                        <div className="min-w-0 flex-1">
                          <div className={`text-sm ${state === "Current" ? "font-semibold text-dls-text-primary" : state === "Done" ? "text-dls-text-secondary" : "text-dls-text-muted"}`}>{step.label}</div>
                          {step.date && state !== "Upcoming" && <div className="text-[11px] text-dls-text-muted">{formatDate(step.date)}</div>}
                        </div>
                        <span className="shrink-0 text-[11px] text-dls-text-muted">{state === "Done" ? "Done" : state === "Current" ? "Now" : "Next"}</span>
                      </li>
                    );
                  })}
                </ol>
              </div>
            </div>

            <ClaimantConsentCard consent={consent} busy={consentBusy} error={consentError} onAgree={() => void onAgree()} onWithdraw={() => void onWithdrawConsent()} />

            <ClaimantDocumentsCard
              uploadsBlockedReason={consent && consent.state !== "Given" ? "Read and agree to the privacy notice above to send documents." : null}
              documents={claim.requiredDocuments}
              busyKey={busyKey}
              progress={progress}
              unfinished={unfinished}
              notice={uploadNotice}
              noticeFailed={uploadFailed}
              requiredSummary={requiredTotal > 0 && claim.status !== "Settled" && claim.status !== "Closed" ? (stillNeeded.length === 0 ? "Everything we asked for has been sent. Our team will let you know if they need anything else." : `Still needed: ${stillNeeded.join(", ")}.`) : null}
              requiredComplete={requiredTotal > 0 && stillNeeded.length === 0}
              onUpload={onUpload}
              onRemove={onRemove}
              onRetry={onRetry}
              onRemovePage={onRemovePage}
              onMovePage={onMovePage}
            />

            {claim.messages.length > 0 && (
              <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-6">
                <div className="text-xs font-bold uppercase tracking-[0.12em] text-dls-text-muted">Updates</div>
                <ul className="mt-3 space-y-3">
                  {claim.messages.map((message) => (
                    <li key={message.communicationId} className="flex items-start gap-2 text-sm leading-5 text-dls-text-secondary">
                      <MessageSquare className="mt-0.5 h-3.5 w-3.5 shrink-0 text-dls-text-muted" aria-hidden="true" />
                      <div>
                        <div>{message.messageBody}</div>
                        <div className="mt-0.5 text-[11px] text-dls-text-muted">{formatDate(message.createdAt)}</div>
                      </div>
                    </li>
                  ))}
                </ul>
              </div>
            )}

            <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-6">
              <div className="text-xs font-bold uppercase tracking-[0.12em] text-dls-text-muted">Want to ask about your claim?</div>
              <p className="mt-1 text-xs leading-5 text-dls-text-muted">
                Type your requirements below.
              </p>
              {claim.reviewRequests.length > 0 && (
                <ul className="mt-3 space-y-1.5">
                  {claim.reviewRequests.map((request) => (
                    <li key={request.requestId} className="flex items-center gap-2 text-xs text-dls-text-muted">
                      <ShieldCheck className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
                      {request.status}, {request.reason}
                    </li>
                  ))}
                </ul>
              )}
              <form onSubmit={onRequestReview} className="mt-3 flex flex-col gap-2 sm:flex-row">
                <div className="flex-1">
                  <Input
                    value={reviewReason}
                    onChange={(event) => setReviewReason(event.target.value)}
                    placeholder="Type your question or request"
                    aria-label="Your question or request about this claim"
                  />
                </div>
                <Button
                  type="submit"
                  variant="primary"
                  disabled={reviewBusy || !reviewReason.trim()}
                  icon={reviewBusy ? <Loader2 className="animate-spin" aria-hidden="true" /> : undefined}
                  aria-label="Send to the claims team"
                >
                  {reviewBusy ? "" : "Send"}
                </Button>
              </form>
              {reviewNotice && <p className="mt-3 text-xs leading-5 text-dls-text-muted">{reviewNotice}</p>}
            </div>
          </div>
        )}
      </div>
    </main>
  );
}
