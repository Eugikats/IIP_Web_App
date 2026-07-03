"use client";

import Link from "next/link";
import { ReactNode, useCallback, useEffect, useMemo, useState } from "react";
import { motion } from "framer-motion";
import {
  ArrowDownUp,
  ClipboardCheck,
  FilePlus2,
  FileText,
  FolderOpen,
  RefreshCw,
  Search,
  SlidersHorizontal,
  Sparkles
} from "lucide-react";
import { RiskBadge, SlaBadge, StatusBadge } from "@/components/Badges";
import { CompletenessIndicator } from "@/components/CompletenessIndicator";
import { DataTable } from "@/components/DataTable";
import { BlockedAction, BlockedActionPanel, GovernedButton, StatusChip } from "@/components/GovernedUi";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { SideDrawer } from "@/components/SideDrawer";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useRole } from "@/components/RoleContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { allowsAction, blockedActionReason, hasActionState, type ActionStateContract } from "@/lib/actionState";

type ClaimInboxItem = ActionStateContract & {
  caseId: string;
  customerName: string;
  policyNumber: string;
  productLine: string;
  claimType: string;
  channel: string;
  status: string;
  completenessScore: number;
  documentStatus: string;
  riskLevel: string;
  priority: string;
  slaStatus: string;
  assignedUserId: string | null;
  assignedTeam: string | null;
  updatedAt: string;
  audit: {
    lastActionName: string | null;
    lastEventAt?: string | null;
  };
  allowedActions?: string[];
  blockedActions?: BlockedAction[];
};

type QueueView = "All Claims" | "My Queue" | "SLA Risk" | "AI Recommended Review";
type SortMode = "SLA Risk" | "Last Updated" | "Completeness" | "Priority";

type RecommendedAction = {
  actionId: string;
  title: string;
  rationale: string;
  confidenceScore: number;
};

type ChecklistDocumentResponse = {
  documentType: string;
  label: string;
  received: boolean;
};

type DocumentChecklist = {
  missingRequiredDocuments: ChecklistDocumentResponse[];
  receivedDocuments: ChecklistDocumentResponse[];
  completenessScore: number;
};

const sortModes: SortMode[] = ["SLA Risk", "Last Updated", "Completeness", "Priority"];
const statusOptions = ["All", "New", "Missing Documents", "Ready For Review", "UnderReview"];
const completenessOptions = ["All", "Incomplete", "Complete"];
const riskOptions = ["All", "Low", "Medium", "High", "Critical"];

export function ClaimsInboxClient() {
  const { role, userId, can } = useRole();
  const [claims, setClaims] = useState<ClaimInboxItem[]>([]);
  const [selected, setSelected] = useState<ClaimInboxItem | null>(null);
  const [recommendedActions, setRecommendedActions] = useState<RecommendedAction[]>([]);
  const [checklist, setChecklist] = useState<DocumentChecklist | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [lastRefreshedAt, setLastRefreshedAt] = useState<Date | null>(null);
  const [activeView, setActiveView] = useState<QueueView>("My Queue");
  const [statusFilter, setStatusFilter] = useState("All");
  const [productFilter, setProductFilter] = useState("All");
  const [riskFilter, setRiskFilter] = useState("All");
  const [completenessFilter, setCompletenessFilter] = useState("All");
  const [sortMode, setSortMode] = useState<SortMode>("SLA Risk");
  const [searchQuery, setSearchQuery] = useState("");

  const queueViews = useMemo<QueueView[]>(() => {
    const views: QueueView[] = can("ViewAllClaims") ? ["All Claims", "My Queue", "SLA Risk", "AI Recommended Review"] : ["My Queue", "SLA Risk", "AI Recommended Review"];
    return views;
  }, [can]);

  const loadClaims = useCallback(async () => {
    setRefreshing(true);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/cases`, {
        headers: scopedHeaders(role, userId)
      });

      const payload: unknown = await response.json();
      if (!Array.isArray(payload) || !payload.every(hasActionState)) {
        throw new Error("Claims response did not include required backend action authorization state.");
      }

      const data = payload as ClaimInboxItem[];
      setClaims(data);
      setSelected((current) => data.find((claim) => claim.caseId === current?.caseId) ?? data[0] ?? null);
      setLastRefreshedAt(new Date());
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Claims intelligence queue failed.");
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [role, userId]);

  useEffect(() => {
    loadClaims();
  }, [loadClaims]);

  useEffect(() => {
    if (!queueViews.includes(activeView)) {
      setActiveView(queueViews[0]);
    }
  }, [activeView, queueViews]);

  useEffect(() => {
    if (!selected) {
      setRecommendedActions([]);
      setChecklist(null);
      return;
    }

    let cancelled = false;
    const selectedCaseId = selected.caseId;
    async function loadPreviewContext() {
      const headers = scopedHeaders(role, userId);
      const [actionsResponse, checklistResponse] = await Promise.all([
        fetch(`${apiBaseUrl}/api/v1/cases/${selectedCaseId}/recommended-actions`, { headers }),
        fetch(`${apiBaseUrl}/api/v1/cases/${selectedCaseId}/document-checklist`, { headers })
      ]);

      if (cancelled) return;
      setRecommendedActions(actionsResponse.ok ? ((await actionsResponse.json()) as RecommendedAction[]) : []);
      setChecklist(checklistResponse.ok ? ((await checklistResponse.json()) as DocumentChecklist) : null);
    }

    loadPreviewContext().catch(() => {
      if (!cancelled) {
        setRecommendedActions([]);
        setChecklist(null);
      }
    });

    return () => {
      cancelled = true;
    };
  }, [role, selected, userId]);

  const products = useMemo(() => ["All", ...Array.from(new Set(claims.map((claim) => claim.productLine)))], [claims]);
  const filtered = useMemo(() => {
    const normalizedQuery = searchQuery.trim().toLowerCase();

    return claims
      .filter((claim) => {
        const statusOk = statusFilter === "All" || claim.documentStatus === statusFilter || claim.status === statusFilter;
        const productOk = productFilter === "All" || claim.productLine === productFilter;
        const riskOk = riskFilter === "All" || claim.riskLevel === riskFilter;
        const completenessOk =
          completenessFilter === "All"
          || (completenessFilter === "Incomplete" && claim.completenessScore < 100)
          || (completenessFilter === "Complete" && claim.completenessScore >= 100);
        const viewOk = claimMatchesView(claim, activeView, userId);
        const searchOk =
          normalizedQuery.length === 0
          || [claim.caseId, claim.customerName, claim.policyNumber, claim.productLine, claim.claimType, claim.assignedTeam ?? ""]
            .some((value) => value.toLowerCase().includes(normalizedQuery));

        return statusOk && productOk && riskOk && completenessOk && viewOk && searchOk;
      })
      .sort((left, right) => compareClaims(left, right, sortMode));
  }, [activeView, claims, completenessFilter, productFilter, riskFilter, searchQuery, sortMode, statusFilter, userId]);

  const queueSummary = useMemo(() => {
    return {
      total: claims.length,
      slaRisk: claims.filter(isSlaRisk).length,
      incomplete: claims.filter((claim) => claim.completenessScore < 100).length,
      blocked: claims.filter((claim) => (claim.blockedActions?.length ?? 0) > 0).length
    };
  }, [claims]);

  if (error) return <ErrorState message={error} />;
  if (loading) return <LoadingState label="Loading claims intelligence..." />;

  return (
    <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.4 }}>
      <PageHeader
        eyebrow="Insurance Operations / Claims"
        title="Claims Intelligence"
        description="Monitor, prioritise and review active claims requiring action. Queue state, action availability and blocked actions come from backend contracts."
        actions={
          <div className="flex flex-wrap gap-2">
            <GovernedButton variant="secondary" icon={<RefreshCw className={`h-4 w-4 ${refreshing ? "animate-spin" : ""}`} />} onClick={loadClaims}>
              Refresh
            </GovernedButton>
            <Link className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-accent bg-accent px-4 py-2 text-sm font-semibold text-white shadow-panel transition-colors hover:bg-brand-600" href="/cases/new">
              <FilePlus2 className="h-4 w-4" aria-hidden="true" />
              Create claim
            </Link>
          </div>
        }
      />

      <div className="mb-4 flex flex-wrap items-center gap-2 text-xs text-muted">
        <StatusChip label={`Visible claims: ${filtered.length}`} tone="info" />
        <StatusChip label={`SLA risk: ${queueSummary.slaRisk}`} tone={queueSummary.slaRisk > 0 ? "warning" : "success"} />
        <StatusChip label={`Incomplete: ${queueSummary.incomplete}`} tone={queueSummary.incomplete > 0 ? "warning" : "success"} />
        <StatusChip label={`Blocked actions: ${queueSummary.blocked}`} tone={queueSummary.blocked > 0 ? "danger" : "success"} />
        <span className="font-mono">{lastRefreshedAt ? `Last refreshed ${lastRefreshedAt.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}` : "Refresh pending"}</span>
      </div>

      <div className="mb-4 flex flex-wrap gap-2" role="tablist" aria-label="Claims queue views">
        {queueViews.map((view) => (
          <button
            key={view}
            type="button"
            role="tab"
            aria-selected={activeView === view}
            className={`min-h-11 rounded-xl border px-4 py-2 text-sm font-semibold transition-colors ${activeView === view ? "border-accent bg-accent text-white" : "border-line bg-surface text-muted hover:border-accent/50 hover:text-ink"}`}
            onClick={() => setActiveView(view)}
          >
            {view}
          </button>
        ))}
      </div>

      <SectionCard eyebrow="Queue controls" title="Search, filters and sort" description="Filters change only this view. Server-side tenant and workspace scope still owns access.">
        <div className="grid gap-3 xl:grid-cols-[minmax(260px,1.5fr)_repeat(5,minmax(140px,1fr))]">
          <label className="block text-sm font-medium text-muted">
            Search
            <span className="mt-1 flex min-h-11 items-center gap-2 rounded-xl border border-line bg-shell px-3 text-ink focus-within:border-accent focus-within:ring-1 focus-within:ring-accent">
              <Search className="h-4 w-4 text-muted" aria-hidden="true" />
              <input
                className="w-full bg-transparent py-2 text-sm outline-none"
                value={searchQuery}
                onChange={(event) => setSearchQuery(event.target.value)}
                placeholder="Claim, policy, customer..."
              />
            </span>
          </label>
          <Filter label="Status" value={statusFilter} onChange={setStatusFilter} options={statusOptions} />
          <Filter label="Product line" value={productFilter} onChange={setProductFilter} options={products} />
          <Filter label="Completeness" value={completenessFilter} onChange={setCompletenessFilter} options={completenessOptions} />
          <Filter label="Risk level" value={riskFilter} onChange={setRiskFilter} options={riskOptions} />
          <Filter label="Sort" value={sortMode} onChange={(value) => setSortMode(value as SortMode)} options={sortModes} icon={<ArrowDownUp className="h-4 w-4" />} />
        </div>
      </SectionCard>

      <div className="mt-5">
        {filtered.length === 0 ? (
          <EmptyState title="No matching claims" description="Create a claim or adjust filters to see the current queue." />
        ) : (
          <DataTable label="Claims Intelligence work queue">
            <thead className="bg-mist text-left text-[10px] font-bold uppercase tracking-[0.12em] text-muted">
              <tr>
                <th className="border-b border-line px-4 py-3">Claim</th>
                <th className="border-b border-line px-4 py-3">Product</th>
                <th className="border-b border-line px-4 py-3">Status</th>
                <th className="border-b border-line px-4 py-3">Completeness</th>
                <th className="border-b border-line px-4 py-3">Risk / SLA</th>
                <th className="border-b border-line px-4 py-3">Assignment</th>
                <th className="border-b border-line px-4 py-3">Action state</th>
                <th className="border-b border-line px-4 py-3">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line bg-surface/80">
              {filtered.map((claim) => (
                <tr key={claim.caseId} className="align-top transition-colors hover:bg-mist/70">
                  <td className="px-4 py-4">
                    <button className="text-left font-semibold text-ink hover:text-accent" onClick={() => setSelected(claim)}>
                      {claim.customerName}
                    </button>
                    <div className="mt-1 font-mono text-xs text-muted">{compactId(claim.caseId)}</div>
                    <div className="mt-1 text-xs text-muted">{claim.policyNumber}</div>
                  </td>
                  <td className="px-4 py-4 text-sm text-muted">
                    {claim.productLine}
                    <div className="mt-1 text-xs text-muted">{claim.claimType} / {claim.channel}</div>
                  </td>
                  <td className="px-4 py-4">
                    <StatusBadge label={claim.documentStatus || claim.status} />
                    <div className="mt-2 text-xs text-muted">{claim.status}</div>
                  </td>
                  <td className="w-44 px-4 py-4">
                    <CompletenessIndicator value={claim.completenessScore} />
                  </td>
                  <td className="space-y-2 px-4 py-4">
                    <RiskBadge label={claim.riskLevel} />
                    <SlaBadge label={claim.slaStatus} />
                  </td>
                  <td className="px-4 py-4 text-sm text-muted">
                    {claim.assignedUserId ?? "Unassigned"}
                    <div className="mt-1 text-xs text-muted">{claim.assignedTeam ?? "Claims team"}</div>
                  </td>
                  <td className="max-w-[220px] px-4 py-4">
                    <ActionStateSummary claim={claim} />
                  </td>
                  <td className="px-4 py-4">
                    <div className="flex flex-col gap-2">
                      <button
                        className="rounded-full border border-accent/40 bg-accent/10 px-3 py-1.5 text-xs font-semibold text-accent hover:bg-accent hover:text-white transition-colors disabled:cursor-not-allowed disabled:opacity-50"
                        disabled={!allowsAction(claim, "openCaseReview")}
                        title={blockedActionReason(claim, "openCaseReview") ?? undefined}
                        onClick={() => setSelected(claim)}
                      >
                        Review
                      </button>
                      <Link className="rounded-full border border-line bg-surface px-3 py-1.5 text-xs font-semibold text-muted hover:text-ink hover:border-accent/50 transition-colors text-center" href={`/claims/${claim.caseId}/documents`}>
                        Documents
                      </Link>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </DataTable>
        )}
      </div>

      <SideDrawer
        open={selected !== null}
        title={selected ? selected.customerName : "Claim preview"}
        subtitle={selected ? `${selected.productLine} / ${selected.claimType}` : undefined}
        onClose={() => setSelected(null)}
        footer={
          selected ? (
            <div className="flex flex-wrap gap-2">
              {allowsAction(selected, "openCaseReview") ? (
                <Link className="rounded-full bg-azure border border-azure/40 px-5 py-2.5 text-sm font-semibold text-white shadow-glow-cyan hover:brightness-110 transition-all" href={`/cases/${selected.caseId}`}>
                  Open full case
                </Link>
              ) : (
                <span className="rounded-full border border-warning/30 bg-warning/10 px-5 py-2.5 text-sm font-semibold text-warning">
                  {blockedActionReason(selected, "openCaseReview") ?? "Case review is not available."}
                </span>
              )}
              <Link className="rounded-full border border-line bg-surface px-5 py-2.5 text-sm font-semibold text-muted hover:text-ink hover:border-accent/50 transition-all" href={`/claims/${selected.caseId}/documents`}>
                Open documents
              </Link>
            </div>
          ) : null
        }
      >
        {selected ? (
          <div className="space-y-5">
            <div className="grid gap-3 md:grid-cols-2">
              <Detail label="Policy" value={selected.policyNumber} />
              <Detail label="Claim amount" value="Not captured" />
              <Detail label="Channel" value={selected.channel} />
              <Detail label="Assigned team" value={selected.assignedTeam ?? "Claims team"} />
              <Detail label="Assigned user" value={selected.assignedUserId ?? "Unassigned"} />
              <Detail label="Last updated" value={formatDateTime(selected.updatedAt)} />
            </div>

            <div className="flex flex-wrap gap-2">
              <StatusBadge label={selected.documentStatus || selected.status} />
              <RiskBadge label={selected.riskLevel} />
              <SlaBadge label={selected.slaStatus} />
            </div>

            <SectionCard eyebrow="Document completeness" title="Evidence state" className="shadow-none !border-0 !bg-transparent !p-0">
              <div className="rounded-xl border border-line bg-mist p-4">
                <CompletenessIndicator value={checklist?.completenessScore ?? selected.completenessScore} />
                <EvidenceList title="Missing evidence" items={checklist?.missingRequiredDocuments ?? []} emptyLabel="No missing required documents reported." />
                <EvidenceList title="Received evidence" items={checklist?.receivedDocuments ?? []} emptyLabel="No received documents reported." />
              </div>
            </SectionCard>

            <BlockedActionPanel actions={selected.blockedActions ?? []} title="Backend-blocked actions" />

            <SectionCard eyebrow="AI recommendation" title="Next recommended action" className="shadow-none !border-0 !bg-transparent !p-0">
              <div className="space-y-3 rounded-xl border border-line bg-mist p-4">
                {recommendedActions.length > 0 ? recommendedActions.map((action) => (
                  <article key={action.actionId} className="rounded-xl border border-violet/20 bg-surface-ai p-3">
                    <div className="flex items-start gap-2">
                      <Sparkles className="mt-0.5 h-4 w-4 flex-shrink-0 text-violet" aria-hidden="true" />
                      <div>
                        <h3 className="text-sm font-semibold text-ink">{action.title}</h3>
                        <p className="mt-1 text-sm leading-6 text-muted">{action.rationale}</p>
                        <p className="mt-2 font-mono text-xs text-muted">Confidence {Math.round(action.confidenceScore * 100)}%</p>
                      </div>
                    </div>
                  </article>
                )) : (
                  <p className="text-sm leading-6 text-muted">No AI recommendation returned for this claim.</p>
                )}
              </div>
            </SectionCard>

            <SectionCard eyebrow="Audit" title="Latest workflow note" className="shadow-none !border-0 !bg-transparent !p-0">
              <div className="rounded-xl border border-line bg-mist p-4">
                <p className="text-sm leading-6 text-muted">{selected.audit.lastActionName ?? "No workflow note recorded yet."}</p>
              </div>
            </SectionCard>
          </div>
        ) : null}
      </SideDrawer>
    </motion.div>
  );
}

function Filter({
  label,
  value,
  onChange,
  options,
  icon
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  options: string[];
  icon?: ReactNode;
}) {
  return (
    <label className="block text-sm font-medium text-muted">
      {label}
      <span className="mt-1 flex min-h-11 items-center gap-2 rounded-xl border border-line bg-shell px-3 focus-within:border-accent focus-within:ring-1 focus-within:ring-accent">
        {icon ?? <SlidersHorizontal className="h-4 w-4 text-muted" aria-hidden="true" />}
        <select className="w-full bg-transparent py-2 text-sm text-ink outline-none cursor-pointer" value={value} onChange={(event) => onChange(event.target.value)}>
          {options.map((option) => (
            <option key={option} value={option} className="bg-shell text-ink">{option}</option>
          ))}
        </select>
      </span>
    </label>
  );
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border border-line bg-mist p-4">
      <p className="text-[10px] font-bold uppercase tracking-[0.12em] text-muted">{label}</p>
      <p className="mt-2 break-words font-mono text-sm font-semibold text-ink">{value}</p>
    </div>
  );
}

function ActionStateSummary({ claim }: { claim: ClaimInboxItem }) {
  const blockedCount = claim.blockedActions?.length ?? 0;
  const allowedCount = claim.allowedActions?.length ?? 0;

  if (blockedCount > 0) {
    return (
      <div className="space-y-2">
        <StatusChip label={`${blockedCount} blocked`} tone="danger" />
        <p className="text-xs leading-5 text-muted">{claim.blockedActions?.[0]?.message}</p>
      </div>
    );
  }

  return (
    <div className="space-y-2">
      <StatusChip label={allowedCount > 0 ? `${allowedCount} available` : "Read only"} tone={allowedCount > 0 ? "success" : "neutral"} />
      <p className="text-xs leading-5 text-muted">{claim.audit.lastActionName ?? "No recent workflow action."}</p>
    </div>
  );
}

function EvidenceList({ title, items, emptyLabel }: { title: string; items: ChecklistDocumentResponse[]; emptyLabel: string }) {
  return (
    <div className="mt-4">
      <h3 className="text-xs font-semibold uppercase tracking-[0.12em] text-muted">{title}</h3>
      {items.length > 0 ? (
        <ul className="mt-2 flex flex-wrap gap-2">
          {items.map((item) => (
            <li key={item.documentType}>
              <StatusChip label={item.label} tone={title.startsWith("Missing") ? "warning" : "success"} />
            </li>
          ))}
        </ul>
      ) : (
        <p className="mt-2 text-sm text-muted">{emptyLabel}</p>
      )}
    </div>
  );
}

function claimMatchesView(claim: ClaimInboxItem, activeView: QueueView, userId: string) {
  if (activeView === "All Claims") return true;
  if (activeView === "My Queue") return claim.assignedUserId === userId || claim.assignedUserId === null;
  if (activeView === "SLA Risk") return isSlaRisk(claim);
  return claim.riskLevel === "High" || claim.riskLevel === "Critical" || claim.completenessScore < 100 || (claim.blockedActions?.length ?? 0) > 0;
}

function isSlaRisk(claim: ClaimInboxItem) {
  return ["DueToday", "AtRisk", "Breached", "Overdue"].some((state) => claim.slaStatus.toLowerCase().includes(state.toLowerCase()));
}

function compareClaims(left: ClaimInboxItem, right: ClaimInboxItem, sortMode: SortMode) {
  if (sortMode === "Last Updated") return new Date(right.updatedAt).getTime() - new Date(left.updatedAt).getTime();
  if (sortMode === "Completeness") return left.completenessScore - right.completenessScore;
  if (sortMode === "Priority") return priorityRank(right.priority) - priorityRank(left.priority);
  return slaRank(right.slaStatus) - slaRank(left.slaStatus) || priorityRank(right.priority) - priorityRank(left.priority);
}

function priorityRank(value: string) {
  const normalized = value.toLowerCase();
  if (normalized.includes("critical")) return 4;
  if (normalized.includes("high")) return 3;
  if (normalized.includes("medium")) return 2;
  return 1;
}

function slaRank(value: string) {
  const normalized = value.toLowerCase();
  if (normalized.includes("breach") || normalized.includes("overdue")) return 4;
  if (normalized.includes("due")) return 3;
  if (normalized.includes("risk")) return 2;
  return 1;
}

function compactId(value: string) {
  return value.length > 12 ? value.slice(0, 8) : value;
}

function formatDateTime(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString([], { dateStyle: "medium", timeStyle: "short" });
}
