"use client";

import { useCallback, useEffect, useState } from "react";
import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { AlertTriangle, FileText, Loader2, LogOut, MessageCircleQuestion, ShieldCheck } from "lucide-react";
import iipLogo from "@/assets/iip-logo";
import { MarkdownText } from "@/components/iip/MarkdownText";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import {
  type ClientPortalPolicy,
  type ClientPortalSession,
  type PolicyQuestionSource,
  askPolicyQuestion,
  clearStoredSession,
  getPolicy,
  loadStoredSession,
  requestContactDetailChange,
} from "@/lib/clientPortal";

function formatDate(value: string | null) {
  if (!value) return null;
  try {
    return new Date(value).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
  } catch {
    return value;
  }
}

const COVER_STATUS_LABEL: Record<string, { label: string; tone: string }> = {
  Active: { label: "Active", tone: "text-dls-success-text" },
  Expired: { label: "Expired", tone: "text-dls-danger-text" },
  NotYetStarted: { label: "Not yet started", tone: "text-dls-warning-text" },
  Unknown: { label: "Not recorded", tone: "text-dls-text-muted" },
};

const POLICY_SOURCE_LABEL: Record<string, string> = {
  Manual: "Entered manually",
  FileImport: "Loaded from file",
  PartnerApi: "Partner API",
  ExternalLookup: "External lookup",
};

// Outcome L: "the customer sees their policy," "asks a question about the wording," and "requests
// a change to their contact details." Premium status is rendered honestly as "Not configured":
// no billing/payment-status subsystem exists in this codebase to say paid, due, or lapsed, and
// showing a made-up value would be exactly the fabricated-data failure AGENTS.md forbids.
export default function ClaimAccessPolicy() {
  const router = useRouter();
  const [session, setSession] = useState<ClientPortalSession | null>(null);
  const [policy, setPolicy] = useState<ClientPortalPolicy | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [question, setQuestion] = useState("");
  const [askBusy, setAskBusy] = useState(false);
  const [answer, setAnswer] = useState<{ text: string; escalationRequired: boolean; sources: PolicyQuestionSource[] } | null>(null);
  const [askError, setAskError] = useState<string | null>(null);

  const [contactEmail, setContactEmail] = useState("");
  const [contactPhone, setContactPhone] = useState("");
  const [contactNote, setContactNote] = useState("");
  const [contactBusy, setContactBusy] = useState(false);
  const [contactNotice, setContactNotice] = useState<string | null>(null);

  const load = useCallback(async (activeSession: ClientPortalSession) => {
    setLoading(true);
    setError(null);
    const result = await getPolicy(activeSession);
    setLoading(false);
    if (!result.ok) {
      setError(result.error);
      return;
    }
    setPolicy(result.policy);
  }, []);

  useEffect(() => {
    const stored = loadStoredSession();
    if (!stored) {
      router.replace("/access");
      return;
    }
    setSession(stored);
    void load(stored);
  }, [load, router]);

  async function onAsk(event: FormEvent) {
    event.preventDefault();
    if (!session || !question.trim()) return;

    setAskBusy(true);
    setAskError(null);
    setAnswer(null);
    const result = await askPolicyQuestion(session, question.trim());
    setAskBusy(false);
    if (!result.ok) {
      setAskError(result.error);
      return;
    }
    setAnswer({ text: result.answerText, escalationRequired: result.escalationRequired, sources: result.sources });
  }

  async function onRequestContactChange(event: FormEvent) {
    event.preventDefault();
    if (!session || (!contactEmail.trim() && !contactPhone.trim())) return;

    setContactBusy(true);
    setContactNotice(null);
    const result = await requestContactDetailChange(
      session,
      contactEmail.trim() || null,
      contactPhone.trim() || null,
      contactNote.trim() || null,
    );
    setContactBusy(false);
    if (!result.ok) {
      setContactNotice(result.error);
      return;
    }
    setContactEmail("");
    setContactPhone("");
    setContactNote("");
    setContactNotice(result.message);
  }

  function signOut() {
    clearStoredSession();
    router.replace("/access");
  }

  if (!session) return null;

  const standing = policy ? COVER_STATUS_LABEL[policy.coverStatus] ?? COVER_STATUS_LABEL.Unknown : null;

  return (
    <main className="min-h-screen bg-dls-bg-page text-dls-text-primary">
      <header className="border-b border-dls-border-subtle bg-dls-surface">
        <div className="mx-auto flex max-w-3xl items-center justify-between px-6 py-4">
          <div className="flex items-center gap-3">
            <span className="grid h-9 w-9 place-items-center rounded-dls-control border border-dls-border-subtle bg-dls-surface">
              <img src={iipLogo.url} alt="IIP" className="h-7 w-7 object-contain" />
            </span>
            <div>
              <div className="text-sm font-semibold">Your policy</div>
              <div className="text-xs text-dls-text-muted">Insurance Intelligence Platform</div>
            </div>
          </div>
          <div className="flex items-center gap-2">
            {session.caseId && (
              <a
                href="/access/claim"
                className="dls-focus-outline inline-flex min-h-9 items-center rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-1.5 text-xs font-semibold text-dls-text-secondary hover:bg-dls-bg-page"
              >
                My claim
              </a>
            )}
            <Button variant="secondary" onClick={signOut} icon={<LogOut aria-hidden="true" />}>
              Done
            </Button>
          </div>
        </div>
      </header>

      <div className="mx-auto max-w-3xl px-6 py-8">
        {loading && (
          <div className="flex items-center justify-center gap-2 py-24 text-sm text-dls-text-secondary">
            <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> Loading your policy…
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

        {!loading && policy && (
          <div className="space-y-5">
            <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-6">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <div className="text-xs font-bold uppercase tracking-[0.12em] text-dls-text-muted">{policy.productLine}{policy.coverType ? ` · ${policy.coverType}` : ""}</div>
                  <div className="mt-1 text-2xl font-bold text-dls-text-primary">Policy {policy.policyNumber}</div>
                </div>
                <div className={`text-sm font-bold ${standing?.tone ?? "text-dls-text-muted"}`}>{standing?.label}</div>
              </div>
              <dl className="mt-5 grid grid-cols-2 gap-4 border-t border-dls-border-subtle pt-5 text-sm sm:grid-cols-4">
                <div>
                  <dt className="text-xs text-dls-text-muted">Cover from</dt>
                  <dd className="mt-0.5 font-semibold">{formatDate(policy.effectiveFrom) ?? "Not recorded"}</dd>
                </div>
                <div>
                  <dt className="text-xs text-dls-text-muted">Cover to</dt>
                  <dd className="mt-0.5 font-semibold">{formatDate(policy.effectiveTo) ?? "Not recorded"}</dd>
                </div>
                <div>
                  <dt className="text-xs text-dls-text-muted">Annual premium</dt>
                  <dd className="mt-0.5 font-semibold">{policy.annualPremium != null ? `${policy.premiumCurrency ?? ""} ${policy.annualPremium.toLocaleString()}`.trim() : "Not recorded"}</dd>
                </div>
                <div>
                  <dt className="text-xs text-dls-text-muted">Premium status</dt>
                  <dd className="mt-0.5 font-semibold text-dls-text-muted">Not configured</dd>
                </div>
              </dl>
              <div className="mt-4 border-t border-dls-border-subtle pt-3 text-xs text-dls-text-muted">
                {POLICY_SOURCE_LABEL[policy.recordSource] ?? policy.recordSource}
                {policy.lastRefreshedAt ? ` · refreshed ${formatDate(policy.lastRefreshedAt)}` : ""}
              </div>
            </div>

            <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-6">
              <div className="text-xs font-bold uppercase tracking-[0.12em] text-dls-text-muted">What is covered</div>
              {policy.coverages.length === 0 ? (
                <p className="mt-3 text-sm text-dls-text-muted">No coverage lines are recorded for this policy yet.</p>
              ) : (
                <ul className="mt-3 divide-y divide-dls-border-subtle">
                  {policy.coverages.map((coverage) => (
                    <li key={coverage.name} className="flex items-start gap-2 py-2.5 text-sm">
                      <FileText className="mt-0.5 h-3.5 w-3.5 shrink-0 text-dls-text-muted" aria-hidden="true" />
                      <div>
                        <div className="font-semibold text-dls-text-primary">{coverage.name}</div>
                        <div className="text-xs text-dls-text-muted">Limit {coverage.limit} · Deductible {coverage.deductible}</div>
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </div>

            <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-6">
              <div className="text-xs font-bold uppercase tracking-[0.12em] text-dls-text-muted">Ask about your policy wording</div>
              <p className="mt-1 text-xs leading-5 text-dls-text-muted">Answers are grounded in your approved policy documents, with a source you can check.</p>
              <form onSubmit={onAsk} className="mt-3 flex flex-col gap-2 sm:flex-row">
                <div className="flex-1">
                  <Input
                    value={question}
                    onChange={(event) => setQuestion(event.target.value)}
                    placeholder="e.g. Is windscreen damage covered?"
                    aria-label="Policy question"
                  />
                </div>
                <Button
                  type="submit"
                  variant="primary"
                  disabled={askBusy || !question.trim()}
                  icon={askBusy ? <Loader2 className="animate-spin" aria-hidden="true" /> : <MessageCircleQuestion aria-hidden="true" />}
                >
                  {askBusy ? "" : "Ask"}
                </Button>
              </form>
              {askError && <p className="mt-3 text-xs leading-5 text-dls-danger-text">{askError}</p>}
              {answer && (
                <div className="mt-4 rounded-dls-control border border-dls-border-subtle bg-dls-bg-page p-4">
                  <MarkdownText className="text-sm leading-6 text-dls-text-primary" content={answer.text} />
                  {answer.sources.length > 0 && (
                    <ul className="mt-3 space-y-1">
                      {answer.sources.map((source) => (
                        <li key={source.chunkId} className="flex items-center gap-1.5 text-[11px] text-dls-text-muted">
                          <FileText className="h-3 w-3 shrink-0" aria-hidden="true" /> {source.documentName}
                        </li>
                      ))}
                    </ul>
                  )}
                  {answer.escalationRequired && (
                    <p className="mt-3 text-[11px] leading-5 text-dls-warning-text">
                      This needs a person to confirm. A member of our team will follow up with you.
                    </p>
                  )}
                </div>
              )}
            </div>

            <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-6">
              <div className="text-xs font-bold uppercase tracking-[0.12em] text-dls-text-muted">Update your contact details</div>
              <p className="mt-1 text-xs leading-5 text-dls-text-muted">
                <ShieldCheck className="mr-1 inline h-3 w-3" aria-hidden="true" />
                We never change your details automatically. A member of staff reviews and confirms every request.
              </p>
              <form onSubmit={onRequestContactChange} className="mt-3 grid gap-2 sm:grid-cols-2">
                <Input
                  value={contactEmail}
                  onChange={(event) => setContactEmail(event.target.value)}
                  placeholder="New email address"
                  type="email"
                  aria-label="New email address"
                />
                <Input
                  value={contactPhone}
                  onChange={(event) => setContactPhone(event.target.value)}
                  placeholder="New phone number"
                  aria-label="New phone number"
                />
                <div className="sm:col-span-2">
                  <Input
                    value={contactNote}
                    onChange={(event) => setContactNote(event.target.value)}
                    placeholder="Note (optional)"
                    aria-label="Note"
                  />
                </div>
                <div className="sm:col-span-2">
                  <Button
                    type="submit"
                    variant="secondary"
                    disabled={contactBusy || (!contactEmail.trim() && !contactPhone.trim())}
                    icon={contactBusy ? <Loader2 className="animate-spin" aria-hidden="true" /> : undefined}
                  >
                    {contactBusy ? "" : "Request change"}
                  </Button>
                </div>
              </form>
              {contactNotice && <p className="mt-3 text-xs leading-5 text-dls-text-muted">{contactNotice}</p>}
            </div>
          </div>
        )}
      </div>
    </main>
  );
}
