"use client";

import { useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { AlertTriangle, BarChart3, CheckCircle2, ChevronRight, ClipboardCheck, Download, FileSpreadsheet, FolderOpen, Loader2, Users, X, type LucideIcon } from "lucide-react";
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { Button } from "@/components/ui/Button";
import { Link } from "@/components/iip/NextRouterCompat";
import { CommandCenterBlocks } from "@/components/iip/CommandCenterAnswer";
import CcComposer from "@/components/command-center/CcComposer";
import { StatusPill, type PillTone } from "@/components/ui/StatusPill";
import { useUI } from "@/context/UIStateContext";
import { canUseLocalTestDoubles } from "@/lib/config";
import { useRuntimeContext } from "@/lib/runtimeContext";
import { useCommandCenterQuery, type CommandCenterAnswer } from "@/lib/lovable-parity/data/commandCenterQuery";
import {
  analyseWorkbook,
  exportAnalysis,
  getChart,
  routeInstruction,
  type AnalysedProspect,
  type ChartSeries,
  type WorkbookAnalysis,
} from "@/lib/workspace";

// The staff workspace for Kato. One conversation: attach a customer workbook and Kato analyses it, ask
// for a chart and Kato draws it from real records, ask for an export and Kato builds the workbook.
// Every step shown is a result the service returned. Nothing here sends a message to a customer.

type Message =
  | { id: number; from: "user"; text: string; fileName?: string }
  | { id: number; from: "kato"; kind: "text"; text: string; error?: boolean }
  | { id: number; from: "kato"; kind: "analysis"; analysis: WorkbookAnalysis }
  | { id: number; from: "kato"; kind: "chart"; series: ChartSeries }
  | { id: number; from: "kato"; kind: "answer"; answer: CommandCenterAnswer };

// Omit does not distribute over a union, so a message without its id is spelled out per member.
type NewMessage = Message extends infer M ? (M extends { id: number } ? Omit<M, "id"> : never) : never;

// SVG fills cannot read the design-token variables, so charts use the same literal blue as the other IIP charts.
const CHART_BLUE = "#1769FF";

const MAX_FILE_BYTES = 25 * 1024 * 1024;

const SUGGESTIONS = [
  "Plot new prospects for the past three days",
  "Show prospects by stage",
  "Compare product interest",
];

function bandTone(band: string): PillTone {
  if (band === "High priority" || band === "Qualified") return "success";
  if (band === "Warm") return "info";
  if (band === "Early interest") return "warning";
  return "neutral";
}

function reviewTone(status: string): PillTone {
  if (status === "Ready for campaign review") return "success";
  if (status === "Needs human review") return "warning";
  return "neutral";
}

function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

function Figure({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-dls-control border border-dls-border-subtle bg-dls-surface px-3 py-2.5">
      <p className="m-0 text-[20px] font-semibold tabular-nums text-dls-text-primary">{value}</p>
      <p className="m-0 text-xs text-dls-text-secondary">{label}</p>
    </div>
  );
}

function Bubble({ children, from }: { children: ReactNode; from: "user" | "kato" }) {
  return (
    <div className={from === "user" ? "flex justify-end" : "flex justify-start"}>
      <div
        className={
          from === "user"
            ? "max-w-[85%] rounded-2xl rounded-tr-md bg-[#1769FF] px-4 py-3 text-[14px] leading-relaxed text-white sm:max-w-[75%]"
            : "w-full max-w-full text-[13.5px] text-dls-text-primary"
        }
      >
        {children}
      </div>
    </div>
  );
}

function AnalysisCard({ analysis, role, canExport }: { analysis: WorkbookAnalysis; role: string; canExport: boolean }) {
  const [reviewing, setReviewing] = useState(false);
  const [eligibleOnly, setEligibleOnly] = useState(true);
  const [showExclusions, setShowExclusions] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);
  const [exported, setExported] = useState(false);
  const s = analysis.summary;

  const reviewRows = useMemo(
    () => analysis.prospects.filter((p) => !eligibleOnly || p.contactable),
    [analysis.prospects, eligibleOnly],
  );
  const top = useMemo(
    () => [...analysis.prospects].sort((a, b) => b.score - a.score).slice(0, 6),
    [analysis.prospects],
  );

  async function runExport() {
    setExporting(true);
    setExportError(null);
    const result = await exportAnalysis(role, analysis);
    setExporting(false);
    if (!result.ok) {
      setExportError(result.error);
      return;
    }
    downloadBlob(result.value.blob, result.value.fileName);
    setExported(true);
  }

  return (
    <section className="rounded-dls-card border border-dls-border-subtle bg-dls-surface" aria-label="Prospect analysis">
      <header className="border-b border-dls-border-subtle px-4 py-3">
        <h3 className="m-0 text-[14px] font-semibold text-dls-text-primary">Prospect analysis</h3>
        <p className="m-0 mt-0.5 text-xs text-dls-text-secondary">
          {analysis.fileName} · analysis {analysis.analysisId} · {analysis.bankName}
        </p>
      </header>

      <ol className="m-0 list-none space-y-1.5 border-b border-dls-border-subtle px-4 py-3">
        {analysis.steps.map((step) => (
          <li key={step.label} className="flex items-start gap-2 text-[13px]">
            {step.status === "Warning" ? (
              <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0 text-dls-warning-text" aria-label="Needs attention" />
            ) : (
              <CheckCircle2 className="mt-0.5 h-3.5 w-3.5 shrink-0 text-dls-success-text" aria-label="Done" />
            )}
            <span className="min-w-0">
              <span className="font-medium text-dls-text-primary">{step.label}.</span>{" "}
              <span className="text-dls-text-secondary">{step.detail}</span>
            </span>
          </li>
        ))}
      </ol>

      <div className="grid grid-cols-2 gap-2 px-4 py-3 sm:grid-cols-4">
        <Figure label="Records read" value={s.totalRecords} />
        <Figure label="Eligible to review" value={s.eligible} />
        <Figure label="Excluded" value={s.excluded} />
        <Figure label="Need human review" value={s.needsReview} />
      </div>

      {s.productRecommendations.length > 0 && (
        <div className="px-4 pb-3">
          <p className="m-0 mb-1.5 text-xs font-medium text-dls-text-secondary">Suggested covers</p>
          <ul className="m-0 flex list-none flex-wrap gap-1.5 p-0">
            {s.productRecommendations.map((item) => (
              <li key={item.label}>
                <StatusPill tone="info" label={`${item.label}: ${item.count}`} />
              </li>
            ))}
          </ul>
        </div>
      )}

      <div className="overflow-x-auto px-4 pb-3">
        <p className="m-0 mb-1.5 text-xs font-medium text-dls-text-secondary">Highest scores, with the reasons</p>
        <table className="w-full min-w-[640px] border-collapse text-left text-[13px]">
          <thead>
            <tr className="border-b border-dls-border-subtle text-xs text-dls-text-secondary">
              <th className="py-1.5 pr-3 font-medium">Customer</th>
              <th className="py-1.5 pr-3 font-medium">Suggested cover</th>
              <th className="py-1.5 pr-3 font-medium">Score</th>
              <th className="py-1.5 font-medium">Why</th>
            </tr>
          </thead>
          <tbody>
            {top.map((p) => (
              <tr key={p.rowNumber} className="border-b border-dls-border-subtle align-top last:border-0">
                <td className="py-2 pr-3">
                  <span className="font-medium text-dls-text-primary">{p.name}</span>
                  <span className="block text-xs text-dls-text-secondary">{p.contactMasked}</span>
                </td>
                <td className="py-2 pr-3 text-dls-text-primary">{p.suggestedProduct}</td>
                <td className="py-2 pr-3">
                  <span className="font-semibold tabular-nums text-dls-text-primary">{p.score}</span>
                  <span className="text-xs text-dls-text-secondary"> / 100</span>
                  <span className="mt-1 block"><StatusPill tone={bandTone(p.band)} label={p.band} /></span>
                </td>
                <td className="py-2 text-dls-text-secondary">
                  {p.reasons.length > 0 ? p.reasons.join("; ") : "Nothing in the file points to a specific cover."}
                  {p.missing.length > 0 && <span className="block text-xs">Missing: {p.missing.slice(0, 3).join(", ")}</span>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        <p className="m-0 mt-2 text-xs text-dls-text-secondary">
          Scores follow fixed rules (need, fit, capacity, relationship, contactability). They order follow-up and are not probabilities or underwriting decisions.
        </p>
      </div>

      <div className="flex flex-wrap items-center gap-2 border-t border-dls-border-subtle px-4 py-3">
        <Button variant="primary" onClick={() => setReviewing((v) => !v)} aria-expanded={reviewing}>
          {reviewing ? "Hide campaign review" : "Review campaign"}
        </Button>
        <Button
          variant="secondary"
          onClick={runExport}
          disabled={!canExport || exporting}
          icon={exporting ? <Loader2 className="animate-spin" /> : <Download />}
        >
          {exporting ? "Building workbook" : "Export recommendations"}
        </Button>
        {s.excluded > 0 && (
          <Button variant="ghost" onClick={() => setShowExclusions((v) => !v)} aria-expanded={showExclusions}>
            {showExclusions ? "Hide exclusions" : `View ${s.excluded} exclusions`}
          </Button>
        )}
        {exported && <span className="text-xs text-dls-text-secondary">Workbook downloaded. The export is audited.</span>}
        {exportError && <span role="alert" className="text-xs text-dls-danger-text">{exportError}</span>}
      </div>

      {showExclusions && (
        <div className="overflow-x-auto border-t border-dls-border-subtle px-4 py-3">
          <table className="w-full min-w-[520px] border-collapse text-left text-[13px]">
            <thead>
              <tr className="border-b border-dls-border-subtle text-xs text-dls-text-secondary">
                <th className="py-1.5 pr-3 font-medium">Customer</th>
                <th className="py-1.5 pr-3 font-medium">Reason</th>
                <th className="py-1.5 font-medium">Fix</th>
              </tr>
            </thead>
            <tbody>
              {analysis.exclusions.slice(0, 100).map((e) => (
                <tr key={`${e.rowNumber}-${e.reason}`} className="border-b border-dls-border-subtle last:border-0">
                  <td className="py-1.5 pr-3 text-dls-text-primary">{e.customerRef}</td>
                  <td className="py-1.5 pr-3 text-dls-text-primary">{e.reason}</td>
                  <td className="py-1.5 text-dls-text-secondary">{e.remediation}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {analysis.exclusions.length > 100 && (
            <p className="m-0 mt-2 text-xs text-dls-text-secondary">Showing 100 of {analysis.exclusions.length}. The exported workbook lists all of them.</p>
          )}
        </div>
      )}

      {reviewing && (
        <div className="border-t border-dls-border-subtle px-4 py-3">
          <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
            <p className="m-0 text-[13px] font-medium text-dls-text-primary">
              Message previews. Template {analysis.prospects[0]?.templateName ?? "iip_bancassurance_product_intro"}
            </p>
            <label className="inline-flex items-center gap-1.5 text-xs text-dls-text-secondary">
              <input type="checkbox" checked={eligibleOnly} onChange={(e) => setEligibleOnly(e.target.checked)} />
              Eligible recipients only
            </label>
          </div>
          <ul className="m-0 max-h-[360px] list-none space-y-2 overflow-y-auto p-0">
            {reviewRows.slice(0, 200).map((p: AnalysedProspect) => (
              <li key={p.rowNumber} className="rounded-dls-control border border-dls-border-subtle p-2.5">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span className="text-[13px] font-medium text-dls-text-primary">
                    {p.name} <span className="font-normal text-dls-text-secondary">{p.contactMasked}</span>
                  </span>
                  <StatusPill tone={reviewTone(p.reviewStatus)} label={p.reviewStatus} />
                </div>
                <p className="m-0 mt-1.5 text-[13px] text-dls-text-secondary">{p.messagePreview}</p>
                <p className="m-0 mt-1 text-xs text-dls-text-secondary">Consent: {p.consent}. Next: {p.nextAction}</p>
              </li>
            ))}
            {reviewRows.length === 0 && <li className="text-[13px] text-dls-text-secondary">No recipients match this filter.</li>}
          </ul>
          <div className="mt-3 rounded-dls-control border border-dls-border-subtle bg-dls-surface-sunken p-3">
            <p className="m-0 flex flex-wrap items-center gap-2 text-[13px] font-medium text-dls-text-primary">
              Approve and send <StatusPill tone="neutral" label="Not configured" />
            </p>
            <p className="m-0 mt-1 text-xs text-dls-text-secondary">
              Sending from a workbook analysis is not connected. Nothing has been sent and the template shows as unverified until Meta approval is confirmed on the WhatsApp connection.
              Approved sending runs through the named-approval outreach on the Acquisition page, which rechecks consent evidence and opt-outs for each person.
            </p>
          </div>
        </div>
      )}

      <ul className="m-0 list-none space-y-1 border-t border-dls-border-subtle px-4 py-3 text-xs text-dls-text-secondary">
        {analysis.notices.map((notice) => (
          <li key={notice}>{notice}</li>
        ))}
      </ul>
    </section>
  );
}

function ChartCard({ series }: { series: ChartSeries }) {
  const [showData, setShowData] = useState(false);

  function downloadCsv() {
    const escape = (value: string | number) => `"${String(value).replace(/"/g, '""')}"`;
    const lines = [[series.xAxis, series.yAxis].map(escape).join(",")].concat(series.points.map((p) => [p.label, p.value].map(escape).join(",")));
    downloadBlob(new Blob([lines.join("\n")], { type: "text/csv;charset=utf-8" }), `iip-${series.metric}.csv`);
  }

  return (
    <section className="rounded-dls-card border border-dls-border-subtle bg-dls-surface" aria-label={series.title}>
      <header className="border-b border-dls-border-subtle px-4 py-3">
        <h3 className="m-0 text-[14px] font-semibold text-dls-text-primary">{series.title}</h3>
        <p className="m-0 mt-0.5 text-xs text-dls-text-secondary">
          {series.period} · {series.timeZone} · Total {series.total}
        </p>
      </header>
      <div className="px-4 py-3">
        {series.isEmpty ? (
          <p className="m-0 rounded-dls-control border border-dashed border-dls-border-input px-3 py-8 text-center text-[13px] text-dls-text-secondary">
            No records exist for this view yet, so there is nothing to draw.
          </p>
        ) : (
          <div className="h-56 w-full" role="img" aria-label={`${series.title}. ${series.insight}`}>
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={series.points} margin={{ top: 4, right: 8, bottom: 4, left: -16 }}>
                <CartesianGrid vertical={false} stroke="#EEF2F7" />
                <XAxis dataKey="label" tick={{ fontSize: 11, fill: "#64748B" }} interval={0} tickLine={false} />
                <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: "#64748B" }} tickLine={false} axisLine={false} />
                <Tooltip cursor={{ fill: "rgba(23,105,255,0.06)" }} />
                <Bar dataKey="value" name={series.yAxis} fill={CHART_BLUE} radius={[3, 3, 0, 0]} maxBarSize={48} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        )}
        <p className="m-0 mt-2 text-[13px] text-dls-text-primary">{series.insight}</p>
        <p className="m-0 mt-1 text-xs text-dls-text-secondary">Source: {series.source}</p>
      </div>
      <div className="flex flex-wrap items-center gap-2 border-t border-dls-border-subtle px-4 py-3">
        <Button variant="secondary" onClick={() => setShowData((v) => !v)} aria-expanded={showData}>
          {showData ? "Hide data" : "View data"}
        </Button>
        <Button variant="secondary" onClick={downloadCsv} disabled={series.isEmpty} icon={<Download />}>
          Export data
        </Button>
      </div>
      {showData && (
        <div className="overflow-x-auto border-t border-dls-border-subtle px-4 py-3">
          <table className="w-full max-w-md border-collapse text-left text-[13px]">
            <thead>
              <tr className="border-b border-dls-border-subtle text-xs text-dls-text-secondary">
                <th className="py-1.5 pr-3 font-medium">{series.xAxis}</th>
                <th className="py-1.5 font-medium">{series.yAxis}</th>
              </tr>
            </thead>
            <tbody>
              {series.points.map((p) => (
                <tr key={p.label} className="border-b border-dls-border-subtle last:border-0">
                  <td className="py-1.5 pr-3 text-dls-text-primary">{p.label}</td>
                  <td className="py-1.5 tabular-nums text-dls-text-primary">{p.value}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

export default function KatoWorkspace() {
  const { roleDef } = useUI();
  const { runtimeContext, parityMode } = useRuntimeContext();
  const { ask } = useCommandCenterQuery();
  const permissions = runtimeContext?.permissions ?? [];
  const canView = canUseLocalTestDoubles || parityMode || permissions.includes("ViewSalesLeads");
  const canManage = canUseLocalTestDoubles || parityMode || permissions.includes("ManageSalesLeads");
  const canAssistant = canUseLocalTestDoubles || parityMode || permissions.includes("ViewCommandCenter");
  const personName = runtimeContext?.displayName?.trim().split(/\s+/)[0] || null;
  const [localTime, setLocalTime] = useState<{ greeting: string; date: string } | null>(null);
  const workspace = runtimeContext?.availableWorkspaces.find((item) => item.workspaceId === runtimeContext.workspaceId);
  const canClaims = canUseLocalTestDoubles || parityMode || permissions.some((permission) => ["ViewAllClaims", "ViewAssignedClaims"].includes(permission));
  const canWork = canUseLocalTestDoubles || parityMode || permissions.includes("ReviewAgentWorkItems") || permissions.includes("ApproveSettlementWorkflowStep");

  const [messages, setMessages] = useState<Message[]>([]);
  const [input, setInput] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const latestAnalysis = useRef<WorkbookAnalysis | null>(null);
  const conversationId = useRef<string | undefined>(undefined);
  const nextId = useRef(1);
  const fileInput = useRef<HTMLInputElement>(null);
  const endRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const hour = new Date().getHours();
    const greeting = hour < 5 ? "Hello" : hour < 12 ? "Good morning" : hour < 17 ? "Good afternoon" : "Good evening";
    setLocalTime({ greeting, date: new Intl.DateTimeFormat(undefined, { weekday: "long", month: "long", day: "numeric" }).format(new Date()) });
  }, []);

  useEffect(() => {
    endRef.current?.scrollIntoView({ block: "end" });
  }, [messages, busy]);

  function push(message: NewMessage) {
    setMessages((current) => [...current, { ...message, id: nextId.current++ } as Message]);
  }

  function chooseFile(candidate: File | undefined) {
    setFileError(null);
    if (!candidate) return;
    if (!/\.(xlsx|csv)$/i.test(candidate.name)) {
      setFileError("Attach an .xlsx or .csv file.");
      return;
    }
    if (candidate.size > MAX_FILE_BYTES) {
      setFileError("That file is larger than 25 MB. Split it and attach the parts.");
      return;
    }
    setFile(candidate);
  }

  async function submit(override?: string) {
    const text = (override ?? input).trim();
    const attached = file;
    if ((!text && !attached) || busy) return;

    push({ from: "user", text: text || "Analyse this workbook", fileName: attached?.name });
    setInput("");
    setFile(null);

    if (attached) {
      setBusy(`Analysing ${attached.name}`);
      const result = await analyseWorkbook(roleDef.key, attached);
      setBusy(null);
      if (!result.ok) {
        push({ from: "kato", kind: "text", text: result.error, error: true });
        return;
      }
      latestAnalysis.current = result.value;
      push({ from: "kato", kind: "analysis", analysis: result.value });
      return;
    }

    const intent = canView ? routeInstruction(text, latestAnalysis.current !== null) : { kind: "unsupported" as const, reply: "" };
    if (intent.kind === "chart") {
      setBusy("Reading prospect records");
      const result = await getChart(roleDef.key, intent.metric, intent.days);
      setBusy(null);
      if (!result.ok) {
        push({ from: "kato", kind: "text", text: result.error, error: true });
        return;
      }
      push({ from: "kato", kind: "chart", series: result.value });
    } else if (intent.kind === "export" && latestAnalysis.current) {
      setBusy("Building the workbook");
      const result = await exportAnalysis(roleDef.key, latestAnalysis.current);
      setBusy(null);
      if (!result.ok) {
        push({ from: "kato", kind: "text", text: result.error, error: true });
        return;
      }
      downloadBlob(result.value.blob, result.value.fileName);
      push({ from: "kato", kind: "text", text: `The workbook ${result.value.fileName} has been downloaded. It has four sheets: Prospect Analysis, Campaign Recommendations, Exclusions and Summary. The export is audited.` });
    } else if (intent.kind === "unsupported") {
      push({ from: "kato", kind: "text", text: intent.reply });
    } else {
      setBusy("Checking workspace records");
      const result = await ask(text, conversationId.current, crypto.randomUUID());
      setBusy(null);
      if (!result.ok) {
        push({ from: "kato", kind: "text", text: result.message, error: true });
        return;
      }
      conversationId.current = result.answer.conversationId ?? conversationId.current;
      push({ from: "kato", kind: "answer", answer: result.answer });
      if (result.answer.conversationId) window.dispatchEvent(new CustomEvent("iip:conversations-changed"));
    }
  }

  const hasThread = messages.length > 0;
  const hasComposer = canAssistant || canView;
  const queues: QueueLink[] = [];
  if (canView) queues.push({ to: "/app/acquisition", title: "Leads needing follow-up", detail: "Open prospects and follow-up activity.", icon: Users });
  if (canWork) queues.push({ to: "/app/work-items", title: "Approvals and assigned work", detail: "Open items assigned to you.", icon: ClipboardCheck });
  if (canClaims) queues.push({ to: "/app/claims", title: "Claims needing attention", detail: "Open the claims queue.", icon: FolderOpen });

  const composer = (
    <div className="w-full shrink-0">
      {file ? (
        <p className="mx-auto mb-2 flex w-full max-w-3xl items-center gap-2 px-2 text-xs text-slate-600">
          <span className="inline-flex items-center gap-2 rounded-full border border-slate-200 bg-white px-2.5 py-1">
            <FileSpreadsheet className="h-3.5 w-3.5 text-[#1769FF]" aria-hidden />
            {file.name}
            <button type="button" aria-label="Remove attachment" onClick={() => setFile(null)} className="text-slate-400 hover:text-slate-700">
              <X className="h-3.5 w-3.5" />
            </button>
          </span>
        </p>
      ) : null}
      {fileError ? <p role="alert" className="mx-auto mb-2 max-w-3xl px-2 text-center text-xs text-dls-danger-text">{fileError}</p> : null}
      <input
        ref={fileInput}
        type="file"
        accept=".xlsx,.csv"
        className="sr-only"
        aria-label="Attach a customer workbook"
        tabIndex={-1}
        onChange={(event) => {
          chooseFile(event.target.files?.[0]);
          event.target.value = "";
        }}
      />
      <CcComposer
        value={input}
        onChange={setInput}
        onSend={() => void submit()}
        disabled={!!busy}
        canSend={!!input.trim() || !!file}
        placeholder={canView ? "Ask anything or tell me what to do..." : "Ask about claims, customers or your assigned work"}
        ariaLabel="Message Kato"
        onAttach={() => fileInput.current?.click()}
        attachDisabled={!canManage}
        attachLabel="Attach a customer workbook"
        attachTitle={canManage ? "Attach an .xlsx or .csv workbook" : "Your role cannot analyse customer files"}
      />
      {canView && !hasThread ? (
        <ul className="m-0 mx-auto mt-3 flex max-w-3xl list-none flex-wrap justify-center gap-1.5 p-0">
          {SUGGESTIONS.map((suggestion) => (
            <li key={suggestion}>
              <button
                type="button"
                disabled={!!busy}
                onClick={() => void submit(suggestion)}
                className="inline-flex min-h-8 items-center gap-1.5 rounded-full border border-slate-200/90 bg-white/80 px-3 py-1 text-xs text-slate-600 transition hover:border-[#1769FF]/40 hover:text-[#0B1640] disabled:opacity-60"
              >
                <BarChart3 className="h-3 w-3 text-[#1769FF]" aria-hidden />
                {suggestion}
              </button>
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );

  if (!hasThread) {
    return (
      <div className="cip-cc -mx-3 -mb-[60px] -mt-7 flex min-h-[100dvh] flex-col px-4 pb-4 pt-7 sm:-mx-6 sm:px-6">
        <div className="mx-auto flex w-full max-w-3xl flex-1 flex-col">
          <div className="flex-1" aria-hidden="true" />
          <header className="shrink-0 text-center">
            <p className="m-0 mb-3 text-[11px] font-semibold uppercase tracking-[0.14em] text-slate-400">
              {workspace?.name ?? "Insurance Intelligence Platform"}
              {localTime ? <span className="font-normal normal-case tracking-normal"> · {localTime.date}</span> : null}
            </p>
            <h1 className="cip-cc-serif m-0 text-balance text-[1.85rem] font-semibold leading-tight tracking-tight text-[#0B1640] sm:text-[2.35rem]">
              {localTime?.greeting ?? "Welcome"}
              {personName ? <>, <span className="text-[#1769FF]">{personName}</span></> : " to your workspace"}
            </h1>
            <p className="cip-cc-serif mx-auto mt-3 max-w-lg text-[15px] leading-relaxed text-slate-500 sm:text-base">
              Ask about claims, customers or acquisition, or analyse a customer workbook if your role allows it.
            </p>
          </header>
          <div className="flex-1" aria-hidden="true" />
          {hasComposer ? (
            composer
          ) : (
            <p className="mx-auto m-0 max-w-xl rounded-2xl border border-slate-100 bg-white px-4 py-4 text-center text-[13px] text-slate-500">
              Your workspace is ready. Ask your administrator to assign the work queues you need.
            </p>
          )}
          {queues.length > 0 ? (
            <section className="mx-auto mt-10 w-full max-w-4xl pb-2" aria-label="Your work queues">
              <div className="mb-5 flex items-center gap-3">
                <div className="h-px flex-1 bg-slate-200/80" />
                <h2 className="m-0 shrink-0 text-[11px] font-semibold uppercase tracking-[0.14em] text-slate-400">Your work queues</h2>
                <div className="h-px flex-1 bg-slate-200/80" />
              </div>
              <div className="grid gap-2 lg:grid-cols-3">
                {queues.map((queue) => (
                  <QueueRow key={queue.to} {...queue} />
                ))}
              </div>
            </section>
          ) : null}
          <div className="h-[min(8vh,4rem)] shrink-0" aria-hidden="true" />
        </div>
      </div>
    );
  }

  return (
    <div className="cip-cc -mx-3 -mb-[60px] -mt-7 flex min-h-[100dvh] flex-col px-4 pt-7 sm:-mx-6 sm:px-6">
      <div className="mx-auto w-full max-w-4xl flex-1 space-y-4 pb-6 pt-3 sm:space-y-5 sm:pt-5" aria-live="polite">
        {messages.map((message) => {
          if (message.from === "user") {
            return (
              <Bubble key={message.id} from="user">
                {message.fileName && (
                  <span className="mb-1 flex items-center gap-1.5 text-xs text-white/80">
                    <FileSpreadsheet className="h-3.5 w-3.5" aria-hidden /> {message.fileName}
                  </span>
                )}
                {message.text}
              </Bubble>
            );
          }
          return (
            <Bubble key={message.id} from="kato">
              {message.kind === "text" && (
                <div className="w-fit max-w-full rounded-2xl rounded-tl-md border border-slate-100 bg-white px-4 py-3">
                  <p className={`m-0 ${message.error ? "text-dls-danger-text" : ""}`} role={message.error ? "alert" : undefined}>
                    {message.text}
                  </p>
                </div>
              )}
              {message.kind === "analysis" && <AnalysisCard analysis={message.analysis} role={roleDef.key} canExport={canManage} />}
              {message.kind === "chart" && <ChartCard series={message.series} />}
              {message.kind === "answer" && <CommandCenterBlocks blocks={message.answer.blocks} />}
            </Bubble>
          );
        })}
        {busy && (
          <Bubble from="kato">
            <p className="m-0 inline-flex items-center gap-2 text-slate-500" role="status">
              <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden /> {busy}
            </p>
          </Bubble>
        )}
        <div ref={endRef} />
      </div>

      {hasComposer ? (
        <div className="sticky bottom-0 -mx-4 bg-gradient-to-t from-white via-white/95 to-transparent px-4 pb-[max(0.75rem,env(safe-area-inset-bottom))] pt-8 sm:-mx-6 sm:px-6">
          {composer}
        </div>
      ) : null}
    </div>
  );
}

type QueueLink = { to: string; title: string; detail: string; icon: LucideIcon };

function QueueRow({ to, title, detail, icon: Icon }: QueueLink) {
  return (
    <Link
      to={to}
      className="flex w-full items-start gap-3 rounded-xl px-2 py-2 text-left transition hover:bg-slate-50/80 dls-focus-outline"
    >
      <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-blue-50">
        <Icon className="h-5 w-5 text-[#1769FF]" aria-hidden />
      </span>
      <span className="min-w-0 flex-1">
        <span className="block text-[15px] font-semibold text-[#0B1640]">{title}</span>
        <span className="mt-0.5 block text-[13px] leading-snug text-slate-500">{detail}</span>
      </span>
      <ChevronRight className="h-4 w-4 shrink-0 text-slate-300 lg:hidden" aria-hidden />
    </Link>
  );
}
