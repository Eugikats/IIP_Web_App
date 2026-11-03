"use client";
import { useEffect, useMemo, useState } from "react";
import { useParams, Link } from "@/components/iip/NextRouterCompat";
import { ArrowLeft, ArrowRight, ClipboardList, FileText, MessageSquare, RefreshCw, Shield, ShieldOff, Sparkles } from "lucide-react";
import { GlassCard, SectionHeader, StatusBadge } from "@/components/iip/Primitives";
import { MarkdownText } from "@/components/iip/MarkdownText";
import { Button } from "@/components/ui/Button";
import { StatusPill, type PillTone } from "@/components/ui/StatusPill";
import { LoadingSkeleton, ErrorStateSurface } from "@/components/ui/StateSurfaces";
import { useCustomerProfile } from "@/lib/lovable-parity/data/customers";
import { useCustomerSummary, type CustomerSummarySource } from "@/lib/lovable-parity/data/customerSummary";
import { CONSENT_PURPOSES, useCustomerConsent } from "@/lib/lovable-parity/data/consent";
import { useUI } from "@/context/UIStateContext";
import { useRuntimeContext } from "@/lib/runtimeContext";
import { canPreviewPermission, hasPermission } from "@/lib/roles";
import { canUseLocalTestDoubles } from "@/lib/config";
import { getCustomerChannelTimeline, type CustomerChannelTimeline } from "@/lib/channelIdentities";
import { channelLabel, claimReference, formatDate, formatDateTime, formatPercent, humanizeIdentifier, isGuid, policyStanding } from "@/lib/format";

// Customer 360. Each record type gets its own purpose-built row, never a generic key/value dump:
// the API returns records as string dictionaries keyed by field name, and printing those keys
// ("policyId", "completenessScore") was exactly how internal field names reached the screen.

type Row = Record<string, string>;

const v = (row: Row, key: string) => {
  const value = row[key];
  return value && value.trim() ? value.trim() : null;
};

const STANDING_TONE: Record<string, PillTone> = {
  Active: "success",
  Expired: "warning",
  "Not yet started": "info",
  Unknown: "neutral",
};

function SectionTitle({ icon: Icon, children }: { icon: typeof FileText; children: React.ReactNode }) {
  return (
    <div className="mb-3 inline-flex items-center gap-2 text-[13px] font-bold text-dls-text-primary">
      <Icon className="h-4 w-4 text-dls-text-muted" aria-hidden="true" />
      {children}
    </div>
  );
}

function Empty({ children }: { children: React.ReactNode }) {
  return <div className="text-[12.5px] text-dls-text-muted">{children}</div>;
}

function ClaimRow({ claim }: { claim: Row }) {
  const caseId = v(claim, "caseId");
  const body = (
    <>
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
          <span className="text-[13px] font-semibold tabular-nums text-dls-text-primary">{claimReference(caseId)}</span>
          <span className="text-[12.5px] text-dls-text-secondary">
            {[v(claim, "productLine"), v(claim, "claimType")].filter(Boolean).join(" ")}
          </span>
        </div>
        <div className="mt-1 flex flex-wrap gap-x-3 gap-y-0.5 text-[11.5px] text-dls-text-muted">
          <span>Incident {formatDate(v(claim, "incidentDate"))}</span>
          <span>Reported {formatDate(v(claim, "createdAt"))}</span>
          <span>Documents {formatPercent(v(claim, "completenessScore"))} complete</span>
          {v(claim, "channel") ? <span>Via {channelLabel(v(claim, "channel"))}</span> : null}
          {v(claim, "policyNumber") ? <span>Policy {v(claim, "policyNumber")}</span> : null}
        </div>
      </div>
      <StatusBadge status={v(claim, "status") ?? "Unknown"} />
      {caseId && isGuid(caseId) ? <ArrowRight className="h-3.5 w-3.5 shrink-0 text-dls-text-muted group-hover:text-dls-primary" aria-hidden="true" /> : null}
    </>
  );
  return caseId && isGuid(caseId) ? (
    <Link to={`/app/claims/${caseId}`} className="dls-focus-outline group flex items-center gap-3 rounded-dls-control px-2 py-3 transition-colors duration-dls-fast ease-dls hover:bg-dls-bg-page">
      {body}
    </Link>
  ) : (
    <div className="flex items-center gap-3 px-2 py-3">{body}</div>
  );
}

function PolicyRow({ policy }: { policy: Row }) {
  const standing = policyStanding(v(policy, "effectiveFrom"), v(policy, "effectiveTo"));
  return (
    <div className="py-3 first:pt-0 last:pb-0">
      <div className="flex flex-wrap items-start justify-between gap-x-2 gap-y-1">
        <div className="min-w-0">
          <div className="whitespace-nowrap text-[13px] font-semibold tabular-nums text-dls-text-primary">{v(policy, "policyNumber") ?? "Policy"}</div>
          <div className="mt-0.5 text-[12px] text-dls-text-secondary">
            {[v(policy, "productLine"), v(policy, "coverType")].filter(Boolean).join(" · ") || "Cover not recorded"}
          </div>
        </div>
        {standing !== "Unknown" ? <StatusPill tone={STANDING_TONE[standing]} label={standing} /> : null}
      </div>
      {v(policy, "effectiveFrom") || v(policy, "effectiveTo") ? (
        <div className="mt-1 text-[11.5px] text-dls-text-muted">
          {formatDate(v(policy, "effectiveFrom"))} to {formatDate(v(policy, "effectiveTo"))}
        </div>
      ) : null}
    </div>
  );
}

function InteractionRow({ message }: { message: Row }) {
  const caseId = v(message, "caseId");
  return (
    <div className="py-3 first:pt-0 last:pb-0">
      <div className="flex flex-wrap items-center justify-between gap-2 text-[11.5px] text-dls-text-muted">
        <span>
          {humanizeIdentifier(v(message, "direction")) || "Message"} · {humanizeIdentifier(v(message, "channel")) || "Unknown channel"}
          {caseId && isGuid(caseId) ? (
            <>
              {" · "}
              <Link to={`/app/claims/${caseId}`} className="dls-focus-outline rounded-dls-tight font-semibold text-dls-primary hover:underline">
                {claimReference(caseId)}
              </Link>
            </>
          ) : null}
        </span>
        <span>{formatDateTime(v(message, "createdAt"))}</span>
      </div>
      <div className="mt-1 text-[12.5px] text-dls-text-secondary">{v(message, "messageSnippet") ?? "No message text recorded."}</div>
      {v(message, "deliveryStatus") ? <div className="mt-1 text-[11px] text-dls-text-muted">{humanizeIdentifier(v(message, "deliveryStatus"))}</div> : null}
    </div>
  );
}

function LinkedChannelTimelineCard({ customerId }: { customerId: string }) {
  const { roleDef } = useUI();
  const { runtimeContext } = useRuntimeContext();
  const canView = (canUseLocalTestDoubles || !runtimeContext?.permissions)
    ? canPreviewPermission(roleDef.key, "ViewChannels") && canPreviewPermission(roleDef.key, "ViewCustomers")
    : hasPermission(runtimeContext.permissions, "ViewChannels") && hasPermission(runtimeContext.permissions, "ViewCustomers");
  const [timeline, setTimeline] = useState<CustomerChannelTimeline | null>(null);
  const [loading, setLoading] = useState(canView);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!canView) { setLoading(false); setTimeline(null); return; }
    const controller = new AbortController();
    setLoading(true);
    setError(null);
    void getCustomerChannelTimeline(roleDef.key, customerId, 1, 50, controller.signal)
      .then(setTimeline)
      .catch((reason: unknown) => {
        if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : "Linked channel conversations could not be loaded.");
      })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [canView, customerId, roleDef.key]);

  async function loadOlder() {
    if (!timeline || !timeline.hasMore || loadingMore) return;
    setLoadingMore(true);
    setError(null);
    try {
      const next = await getCustomerChannelTimeline(roleDef.key, customerId, timeline.page + 1, timeline.pageSize);
      setTimeline({ ...next, entries: [...timeline.entries, ...next.entries] });
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Older linked channel messages could not be loaded.");
    } finally {
      setLoadingMore(false);
    }
  }

  return (
    <GlassCard>
      <SectionTitle icon={MessageSquare}>Verified cross-channel timeline</SectionTitle>
      <p className="mb-3 text-[11.5px] text-dls-text-muted">Messages appear here only while the verified identity link, channel connection and customer consent remain active.</p>
      {!canView ? (
        <Empty>You do not have permission to view channel conversations.</Empty>
      ) : loading ? (
        <LoadingSkeleton rows={2} />
      ) : error && !timeline ? (
        <ErrorStateSurface title="Linked conversations unavailable" message={error} onRetry={() => window.location.reload()} />
      ) : !timeline || timeline.entries.length === 0 ? (
        <Empty>No consented linked channel conversations are available.</Empty>
      ) : (
        <div className="divide-y divide-dls-border-subtle">
          {timeline.entries.map((entry) => (
            <div key={entry.messageId} className="py-3 first:pt-0 last:pb-0">
              <div className="flex flex-wrap items-center justify-between gap-2 text-[11.5px] text-dls-text-muted">
                <span>{humanizeIdentifier(entry.direction)} · {channelLabel(entry.channel)}</span>
                <span>{formatDateTime(entry.createdAt)}</span>
              </div>
              <div className="mt-1 whitespace-pre-wrap break-words text-[12.5px] text-dls-text-secondary">{entry.body}</div>
              {entry.caseId ? <div className="mt-1 text-[11px] text-dls-text-muted">Linked claim recorded</div> : null}
            </div>
          ))}
          {error ? <p className="pt-2 text-[11.5px] text-dls-danger-text">{error}</p> : null}
          {timeline.hasMore ? <Button variant="secondary" disabled={loadingMore} onClick={() => void loadOlder()} className="mt-3 !min-h-9 !px-3 !text-[12px]">{loadingMore ? "Loading older messages" : "Load older messages"}</Button> : null}
          {timeline.sourceHistoryCapped ? <p className="pt-2 text-[11px] text-dls-text-muted">Some older messages are outside the timeline history window. Open the channel in Inbox to review its full thread.</p> : null}
        </div>
      )}
    </GlassCard>
  );
}

function DocumentRow({ document }: { document: Row }) {
  const caseId = v(document, "caseId");
  return (
    <div className="py-3 first:pt-0 last:pb-0">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <div className="break-words text-[12.5px] font-semibold text-dls-text-primary">{v(document, "fileName") ?? "Document"}</div>
          <div className="mt-0.5 text-[11.5px] text-dls-text-muted">
            {humanizeIdentifier(v(document, "documentType")) || "Unclassified"} · {formatDate(v(document, "uploadedAt"))}
          </div>
        </div>
        {v(document, "processingStatus") ? <StatusBadge status={humanizeIdentifier(v(document, "processingStatus"))} /> : null}
      </div>
      {caseId && isGuid(caseId) ? (
        <Link to={`/app/claims/${caseId}`} className="dls-focus-outline mt-1 inline-block rounded-dls-tight text-[11.5px] font-semibold text-dls-primary hover:underline">
          On claim {claimReference(caseId)}
        </Link>
      ) : null}
    </div>
  );
}

// The agent cites records as [S1], [S2]. Those become numbered markers that match the source list
// under the briefing, where each source opens the record it points to.
function numberCitations(summary: string, sources: CustomerSummarySource[]) {
  const order = new Map(sources.map((source, index) => [source.tag.toUpperCase(), index + 1]));
  return summary.replace(/\[(S\d+)\]/gi, (match, tag: string) => {
    const n = order.get(tag.toUpperCase());
    return n ? `[${n}]` : "";
  });
}

function CustomerSummaryCard({ customerId }: { customerId: string }) {
  const { state, refresh } = useCustomerSummary(customerId);
  const cited = useMemo(() => {
    if (state.status !== "ready") return [];
    const used = new Set(Array.from(state.summary.summary.matchAll(/\[(S\d+)\]/gi)).map((m) => m[1].toUpperCase()));
    return state.summary.sources.map((source, index) => ({ ...source, n: index + 1, used: used.has(source.tag.toUpperCase()) }));
  }, [state]);

  return (
    <GlassCard>
      <div className="mb-3 flex items-start justify-between gap-3">
        <div className="flex items-center gap-2.5">
          <div className="grid h-8 w-8 shrink-0 place-items-center rounded-dls-chip bg-dls-primary-soft text-dls-primary">
            <Sparkles className="h-4 w-4" aria-hidden="true" />
          </div>
          <div>
            <div className="text-[11px] font-bold uppercase tracking-[0.08em] text-dls-text-muted">Customer summary</div>
            <div className="text-[13.5px] font-bold text-dls-text-primary">Prepared by the customer summary agent</div>
          </div>
        </div>
        {state.status === "ready" || state.status === "unavailable" ? (
          <button
            type="button"
            onClick={() => void refresh()}
            className="dls-focus-outline inline-flex shrink-0 items-center gap-1.5 rounded-dls-control border border-dls-border-input px-2.5 py-1.5 text-[12px] font-semibold text-dls-text-primary hover:bg-dls-bg-page"
          >
            <RefreshCw className="h-3.5 w-3.5" aria-hidden="true" />
            {state.status === "ready" ? "Refresh" : "Try again"}
          </button>
        ) : null}
      </div>

      {state.status === "loading" || state.status === "idle" ? (
        <div>
          <p className="mb-3 text-[12.5px] text-dls-text-muted">Reading this customer&apos;s policies, claims, messages and documents.</p>
          <LoadingSkeleton rows={3} />
        </div>
      ) : state.status === "ready" ? (
        <div>
          <MarkdownText className="text-[13px] leading-relaxed text-dls-text-secondary" content={numberCitations(state.summary.summary, state.summary.sources)} />
          {cited.some((source) => source.used) ? (
            <div className="mt-4 border-t border-dls-border-subtle pt-3">
              <div className="mb-1.5 text-[11px] font-bold uppercase tracking-[0.08em] text-dls-text-muted">Sources</div>
              <ol className="space-y-1">
                {cited.filter((source) => source.used).map((source) => (
                  <li key={source.tag} className="flex gap-2 text-[12px] text-dls-text-secondary">
                    <span className="w-5 shrink-0 tabular-nums text-dls-text-muted">[{source.n}]</span>
                    {source.href ? (
                      <Link to={source.href} className="dls-focus-outline rounded-dls-tight font-semibold text-dls-primary hover:underline">
                        {source.label}
                      </Link>
                    ) : (
                      <span>{source.label}</span>
                    )}
                  </li>
                ))}
              </ol>
            </div>
          ) : null}
          <p className="mt-3 text-[11px] text-dls-text-muted">
            Prepared {formatDateTime(state.summary.generatedAt)} from this customer&apos;s records only. It supports a person&apos;s judgement and makes no decision.
          </p>
        </div>
      ) : state.status === "disabled" ? (
        <p className="text-[12.5px] text-dls-text-muted">{state.message}</p>
      ) : state.status === "unavailable" ? (
        <p className="text-[12.5px] text-dls-warning-text">{state.message}</p>
      ) : (
        <p className="text-[12.5px] text-dls-text-muted">
          Customer summaries are prepared from live records, so none is shown in this preview.
        </p>
      )}
    </GlassCard>
  );
}

// Outcome M: consent as a first class record. What was consented to, which channel, when,
// through which interaction, and how it was withdrawn, each a real ConsentRecord row, not a
// generic profile dictionary. Grouped by purpose so "is WhatsApp currently permitted?" is a
// one-glance answer, with the full row-by-row history underneath for the "how it was withdrawn" part.
function ConsentCard({ customerId }: { customerId: string }) {
  const { roleDef } = useUI();
  const { runtimeContext, parityMode } = useRuntimeContext();
  const canManage = canUseLocalTestDoubles || parityMode
    ? canPreviewPermission(roleDef.key, "ManageCustomerCommunications")
    : hasPermission(runtimeContext?.permissions ?? [], "ManageCustomerCommunications");
  const { records, loading, error, actionError, busyPurpose, recordOptIn, withdraw } = useCustomerConsent(customerId);

  return (
    <GlassCard>
      <SectionTitle icon={Shield}>Consent and privacy</SectionTitle>
      {loading ? (
        <LoadingSkeleton rows={2} />
      ) : error ? (
        <p className="text-[12.5px] text-dls-danger-text">{error}</p>
      ) : (
        <>
          <div className="space-y-2.5">
            {CONSENT_PURPOSES.map(({ purpose, label }) => {
              const history = records.filter((record) => record.purpose === purpose);
              const current = history[0] ?? null;
              const permitted = current?.isCurrentlyPermitted ?? false;
              return (
                <div key={purpose} className="rounded-dls-control border border-dls-border-subtle p-2.5">
                  <div className="flex items-center justify-between gap-2">
                    <div className="flex items-center gap-1.5 text-[12.5px] font-semibold text-dls-text-primary">
                      {permitted ? <Shield className="h-3.5 w-3.5 text-dls-success-text" aria-hidden="true" /> : <ShieldOff className="h-3.5 w-3.5 text-dls-text-muted" aria-hidden="true" />}
                      {label}
                    </div>
                    <span className={`text-[11px] font-semibold ${permitted ? "text-dls-success-text" : "text-dls-text-muted"}`}>
                      {permitted ? "Permitted" : "Not permitted"}
                    </span>
                  </div>
                  {current ? (
                    <div className="mt-1 text-[11px] text-dls-text-muted">
                      {current.withdrawnAt
                        ? `Withdrawn ${formatDateTime(current.withdrawnAt)}`
                        : `${humanizeIdentifier(current.basis)} · ${formatDateTime(current.recordedAt)}`}
                      {current.interactionReference ? ` · ${current.interactionReference}` : ""}
                    </div>
                  ) : (
                    <div className="mt-1 text-[11px] text-dls-text-muted">No consent recorded for this channel yet.</div>
                  )}
                  {canManage && (
                    <div className="mt-2 flex gap-2">
                      <Button
                        variant="secondary"
                        disabled={busyPurpose === purpose || permitted}
                        onClick={() => void recordOptIn(purpose)}
                        className="!min-h-8 !px-2.5 !py-1 !text-[11.5px]"
                      >
                        Record consent
                      </Button>
                      <Button
                        variant="secondary"
                        disabled={busyPurpose === purpose || !permitted}
                        onClick={() => void withdraw(purpose)}
                        className="!min-h-8 !px-2.5 !py-1 !text-[11.5px]"
                      >
                        Withdraw
                      </Button>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
          {actionError && <p className="mt-2 text-[11.5px] text-dls-danger-text">{actionError}</p>}
          {records.length === 0 && (
            <p className="mt-2 text-[12.5px] text-dls-text-muted">
              No consent has been recorded for this customer yet. It is captured when they first contact the insurer on a messaging channel, or recorded here after a confirmed opt-in.
            </p>
          )}
        </>
      )}
    </GlassCard>
  );
}

export default function CustomerDetail() {
  const { id } = useParams();
  const customerId = String(id ?? "");
  const { profile, loading, error } = useCustomerProfile(customerId);

  const name = profile?.identity?.fullName ?? profile?.identity?.name ?? (loading ? "Customer" : "Customer not found");
  const contact = [profile?.identity?.email, profile?.identity?.phone, profile?.identity?.contact].filter(Boolean).join(" · ");
  const claims = profile?.claims ?? [];
  const openClaims = claims.filter((claim) => !["Settled", "Closed"].includes(claim.status ?? ""));

  return (
    <>
      <div className="mb-4 flex items-center gap-2 text-[12px] text-dls-text-secondary">
        <Link to="/app/customers" className="dls-focus-outline inline-flex items-center gap-1 rounded-dls-tight hover:text-dls-text-primary">
          <ArrowLeft className="h-3.5 w-3.5" aria-hidden="true" /> Customers
        </Link>
        <span aria-hidden="true">/</span>
        <span className="text-dls-text-primary">{name}</span>
      </div>

      <SectionHeader
        eyebrow="Customer 360"
        title={name}
        sub={
          profile
            ? `${contact || "No contact details on file"} · ${claims.length} ${claims.length === 1 ? "claim" : "claims"}${openClaims.length ? `, ${openClaims.length} open` : ""}`
            : undefined
        }
      />

      {loading ? (
        <GlassCard><LoadingSkeleton rows={4} /></GlassCard>
      ) : !profile ? (
        <GlassCard>
          <ErrorStateSurface title="Customer not found" message={error ?? "There is no customer with this record in your workspace."} />
        </GlassCard>
      ) : (
        <div className="grid gap-4 lg:grid-cols-3">
          <div className="space-y-4 lg:col-span-2">
            <CustomerSummaryCard customerId={profile.customerId} />

            <GlassCard>
              <SectionTitle icon={FileText}>Claims</SectionTitle>
              {claims.length === 0 ? (
                <Empty>No claims on file for this customer.</Empty>
              ) : (
                <div className="-mx-2 divide-y divide-dls-border-subtle">
                  {claims.map((claim, index) => <ClaimRow key={claim.caseId ?? index} claim={claim} />)}
                </div>
              )}
            </GlassCard>

            <GlassCard>
              <SectionTitle icon={MessageSquare}>Messages</SectionTitle>
              {profile.interactions.length === 0 ? (
                <Empty>No messages with this customer yet.</Empty>
              ) : (
                <div className="divide-y divide-dls-border-subtle">
                  {profile.interactions.map((message, index) => <InteractionRow key={message.communicationId ?? index} message={message} />)}
                </div>
              )}
            </GlassCard>

            <LinkedChannelTimelineCard customerId={profile.customerId} />
          </div>

          <div className="space-y-4">
            <GlassCard>
              <SectionTitle icon={ClipboardList}>Policies</SectionTitle>
              {profile.policies.length === 0 ? (
                <Empty>No policies recorded.</Empty>
              ) : (
                <div className="divide-y divide-dls-border-subtle">
                  {profile.policies.map((policy, index) => <PolicyRow key={policy.policyNumber ?? index} policy={policy} />)}
                </div>
              )}
            </GlassCard>

            <GlassCard>
              <SectionTitle icon={FileText}>Documents on file</SectionTitle>
              {profile.documents.length === 0 ? (
                <Empty>No documents received from this customer yet.</Empty>
              ) : (
                <div className="divide-y divide-dls-border-subtle">
                  {profile.documents.map((document, index) => <DocumentRow key={document.documentId ?? index} document={document} />)}
                </div>
              )}
            </GlassCard>

            <ConsentCard customerId={profile.customerId} />
          </div>
        </div>
      )}
    </>
  );
}
