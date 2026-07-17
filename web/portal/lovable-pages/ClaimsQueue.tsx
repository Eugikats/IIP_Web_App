"use client";
import { useState, useMemo } from "react";
import { Link } from "@/components/iip/NextRouterCompat";
import { toast } from "sonner";
import { AlertTriangle, ArrowRight, ClipboardList, Filter, Loader2, Search, UserCheck, X } from "lucide-react";
import { GlassCard, SectionHeader, Badge, StatusBadge, SLABadge, RiskBadge, ConfidenceBadge } from "@/components/iip/Primitives";
import { useClaims } from "@/lib/lovable-parity/data/claims";
import { useRuntimeContext } from "@/lib/runtimeContext";

const quickFilters = [
  "All",
  "My queue",
  "Unassigned",
  "Missing documents",
  "Pending approval",
  "Escalated",
  "SLA attention",
] as const;
type QuickFilter = (typeof quickFilters)[number];

export default function ClaimsQueue() {
  const { claims, loading, error, assignCase, escalateCase } = useClaims();
  const { runtimeContext } = useRuntimeContext();
  const [f, setF] = useState<QuickFilter>("All");
  const [q, setQ] = useState("");
  const [productFilter, setProductFilter] = useState("All");
  const [riskFilter, setRiskFilter] = useState("All");
  const [channelFilter, setChannelFilter] = useState("All");
  const [busyCaseId, setBusyCaseId] = useState<string | null>(null);

  const productOptions = useMemo(() => uniqueOptions(claims.map((claim) => claim.productLine)), [claims]);
  const riskOptions = useMemo(() => uniqueOptions(claims.map((claim) => claim.risk)), [claims]);
  const channelOptions = useMemo(() => uniqueOptions(claims.map((claim) => claim.channel)), [claims]);

  const quickCounts = useMemo(() => {
    const counts = new Map<QuickFilter, number>();
    for (const filter of quickFilters) {
      counts.set(filter, claims.filter((claim) => matchesQuickFilter(claim, filter, runtimeContext?.userId ?? null)).length);
    }
    return counts;
  }, [claims, runtimeContext?.userId]);

  const rows = useMemo(() => claims.filter(c => {
    if (!matchesQuickFilter(c, f, runtimeContext?.userId ?? null)) return false;
    if (productFilter !== "All" && c.productLine !== productFilter) return false;
    if (riskFilter !== "All" && c.risk !== riskFilter) return false;
    if (channelFilter !== "All" && c.channel !== channelFilter) return false;
    if (q && !`${c.id} ${c.customer} ${c.policy} ${c.productLine} ${c.claimType} ${c.status} ${c.assignedUserId ?? ""} ${c.assignedTeam ?? ""}`.toLowerCase().includes(q.toLowerCase())) return false;
    return true;
  }), [channelFilter, claims, f, productFilter, q, riskFilter, runtimeContext?.userId]);

  const activeStructuredFilters = [productFilter, riskFilter, channelFilter].filter((value) => value !== "All").length;

  function clearFilters() {
    setF("All");
    setQ("");
    setProductFilter("All");
    setRiskFilter("All");
    setChannelFilter("All");
  }

  async function assignToMe(caseId: string) {
    if (!runtimeContext?.userId) {
      toast.error("User context is not available.");
      return;
    }

    setBusyCaseId(caseId);
    try {
      const result = await assignCase(caseId, runtimeContext.userId);
      if (result.ok) toast.success("Case assigned", { description: "The work queue was refreshed from the backend." });
      else toast.error(result.message ?? "Assignment failed.");
    } finally {
      setBusyCaseId(null);
    }
  }

  async function escalate(caseId: string) {
    setBusyCaseId(caseId);
    try {
      const result = await escalateCase(caseId, "Escalated from claims work queue for supervisor review.");
      if (result.ok) toast.success("Case escalated", { description: "The case is now assigned to Claims Supervision." });
      else toast.error(result.message ?? "Escalation failed.");
    } finally {
      setBusyCaseId(null);
    }
  }

  return (
    <>
      <SectionHeader
        eyebrow="Claims Intelligence"
        title="Work Queue"
        sub="Claims requiring officer attention across Motor Operations. AI has pre-processed extractions, evidence, and risk signals — officers decide."
        action={<Link to="/app/claims/new" className="rounded-2xl gradient-primary text-white text-[13px] font-medium px-4 py-2.5 shadow-l2 hover-lift">+ New Claim (FNOL)</Link>}
      />

      <div className="glass-subtle p-3 mb-4 space-y-3">
        <div className="flex flex-wrap items-center gap-2">
        <div className="flex items-center gap-2 rounded-xl bg-white/80 border border-cloud px-3 py-2 min-w-[280px] flex-1 max-w-md">
          <Search className="h-3.5 w-3.5 text-stone" />
          <input value={q} onChange={e => setQ(e.target.value)} placeholder="Search claim, customer, policy, assignment..." className="bg-transparent outline-none text-[13px] flex-1 placeholder:text-stone text-navy" />
        </div>
        <div className="flex flex-wrap gap-1.5">
          {quickFilters.map(x => (
            <button key={x} onClick={() => setF(x)} className={`text-[12px] rounded-full px-3 py-1.5 border transition ${f === x ? "border-info/30 bg-[hsl(var(--info)/0.08)] text-info" : "border-cloud bg-white/70 text-slate2 hover:bg-white"}`}>
              {x} <span className="ml-1 text-[10px] opacity-70">{quickCounts.get(x) ?? 0}</span>
            </button>
          ))}
        </div>
        <button
          type="button"
          onClick={clearFilters}
          disabled={f === "All" && q.length === 0 && activeStructuredFilters === 0}
          className="ml-auto inline-flex items-center gap-1.5 rounded-xl border border-cloud bg-white/70 px-3 py-2 text-[12px] text-slate2 disabled:cursor-not-allowed disabled:opacity-45"
        >
          <X className="h-3.5 w-3.5" />
          Clear
        </button>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Filter className="h-3.5 w-3.5 text-stone" />
          <QueueSelect label="Product" value={productFilter} options={productOptions} onChange={setProductFilter} />
          <QueueSelect label="Risk" value={riskFilter} options={riskOptions} onChange={setRiskFilter} />
          <QueueSelect label="Channel" value={channelFilter} options={channelOptions} onChange={setChannelFilter} />
          <div className="ml-auto text-[12px] text-stone">Assignment and escalation execute through audited API actions.</div>
        </div>
      </div>

      {loading ? (
        <GlassCard><div className="p-6 text-center text-[13px] text-stone">Loading claims…</div></GlassCard>
      ) : claims.length === 0 ? (
        <GlassCard>
          <div className="p-10 text-center flex flex-col items-center gap-2">
            <ClipboardList className="h-6 w-6 text-stone" />
            <div className="text-navy font-medium">No claims in this workspace</div>
            <div className="text-[12.5px] text-stone max-w-md">{error ? error : "Claims appear here once created from FNOL intake or channel conversations."}</div>
          </div>
        </GlassCard>
      ) : (
        <GlassCard className="p-0 overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-[13px]">
              <thead>
                <tr className="text-left text-[11px] uppercase tracking-[0.12em] text-stone border-b border-cloud">
                  {["Claim","Customer","Product","Status","Docs","SLA","Risk","Confidence","Assigned","Updated","Actions"].map(h => (
                    <th key={h} className="px-4 py-3 font-medium">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {rows.map(c => (
                  <tr key={c.id} className="border-b border-cloud/60 hover:bg-white/70 transition cursor-pointer group">
                    <td className="px-4 py-3">
                      <div className="font-medium text-navy">{c.id}</div>
                      <div className="text-[11px] text-stone">{c.policy}</div>
                    </td>
                    <td className="px-4 py-3">
                      <div className="text-navy">{c.customer}</div>
                      <div className="text-[11px] text-stone">{c.channel}</div>
                    </td>
                    <td className="px-4 py-3 text-slate2">{c.productLine} · {c.claimType}</td>
                    <td className="px-4 py-3"><StatusBadge status={c.status} /></td>
                    <td className="px-4 py-3">{c.complete ? <Badge tone="success">Complete</Badge> : <Badge tone="warning">Incomplete</Badge>}</td>
                    <td className="px-4 py-3"><SLABadge sla={c.sla} /></td>
                    <td className="px-4 py-3"><RiskBadge risk={c.risk} /></td>
                    <td className="px-4 py-3"><ConfidenceBadge value={c.confidence} /></td>
                    <td className="px-4 py-3 text-slate2 whitespace-nowrap">
                      {c.assignedUserId ? c.assignedUserId : c.assignedTeam ?? "Unassigned"}
                    </td>
                    <td className="px-4 py-3 text-stone whitespace-nowrap">{c.updated}</td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-1.5">
                        <Link to={`/app/claims/${c.id}`} className="inline-flex items-center gap-1 rounded-lg border border-cloud bg-white px-2 py-1.5 text-[12px] text-info">
                          Review <ArrowRight className="h-3 w-3" />
                        </Link>
                        <button
                          type="button"
                          onClick={() => assignToMe(c.id)}
                          disabled={busyCaseId === c.id}
                          className="inline-flex items-center gap-1 rounded-lg border border-cloud bg-white px-2 py-1.5 text-[12px] text-navy disabled:opacity-50"
                        >
                          {busyCaseId === c.id ? <Loader2 className="h-3 w-3 animate-spin" /> : <UserCheck className="h-3 w-3" />}
                          Assign
                        </button>
                        <button
                          type="button"
                          onClick={() => escalate(c.id)}
                          disabled={busyCaseId === c.id || c.status === "Escalated"}
                          className="inline-flex items-center gap-1 rounded-lg border border-[hsl(var(--warning)/0.32)] bg-[hsl(var(--warning)/0.08)] px-2 py-1.5 text-[12px] text-[hsl(38_92%_36%)] disabled:opacity-50"
                        >
                          <AlertTriangle className="h-3 w-3" />
                          Escalate
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="flex items-center justify-between px-4 py-3 text-[12px] text-stone border-t border-cloud">
            <div>Showing {rows.length} of {claims.length} claims</div>
          </div>
        </GlassCard>
      )}
    </>
  );
}

function uniqueOptions(values: string[]) {
  return Array.from(new Set(values.filter(Boolean))).sort((a, b) => a.localeCompare(b));
}

function normalized(value: string) {
  return value.replace(/[\s_-]/g, "").toLowerCase();
}

function matchesQuickFilter(claim: { status: string; sla: string; complete: boolean; assignedUserId: string | null; assignedTeam: string | null }, filter: QuickFilter, userId: string | null) {
  const status = normalized(claim.status);
  const sla = normalized(claim.sla);
  switch (filter) {
    case "All":
      return true;
    case "My queue":
      return Boolean(userId && claim.assignedUserId === userId);
    case "Unassigned":
      return !claim.assignedUserId && !claim.assignedTeam;
    case "Missing documents":
      return !claim.complete || status.includes("missingdocuments");
    case "Pending approval":
      return status.includes("pendingapproval");
    case "Escalated":
      return status.includes("escalated");
    case "SLA attention":
      return sla !== "normal";
  }
}

function QueueSelect({
  label,
  value,
  options,
  onChange,
}: {
  label: string;
  value: string;
  options: string[];
  onChange: (value: string) => void;
}) {
  return (
    <label className="inline-flex items-center gap-2 rounded-xl border border-cloud bg-white/70 px-3 py-2 text-[12px] text-slate2">
      <span className="text-stone">{label}</span>
      <select
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="bg-transparent text-navy outline-none"
      >
        <option value="All">All</option>
        {options.map((option) => (
          <option key={option} value={option}>{option}</option>
        ))}
      </select>
    </label>
  );
}
