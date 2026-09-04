"use client";
import { useMemo, useState } from "react";
import { toast } from "sonner";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import {
  useKnowledgeDocs, SELECTABLE_SENSITIVITIES, type KnowledgeDocView, type KnowledgeSensitivity,
} from "@/lib/lovable-parity/data/knowledge";
import { BookOpen, Search, Upload, RefreshCw, Loader2 } from "lucide-react";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { GovernedActionDialog } from "@/components/GovernedActionDialog";

const SENS_TONE: Record<KnowledgeSensitivity, "success" | "info" | "warning" | "error" | "violet" | "neutral"> = {
  approved: "success",
  customer_confidential: "violet",
  staff_only: "info",
  contains_secrets: "error",
  contains_unapproved_pii: "error",
  unknown: "warning",
};

const SENS_LABEL: Record<KnowledgeSensitivity, string> = {
  approved: "Approved for AI grounding",
  customer_confidential: "Customer confidential",
  staff_only: "Staff only",
  contains_secrets: "Contains secrets",
  contains_unapproved_pii: "Contains unapproved PII",
  unknown: "Unknown",
};

const DOCUMENT_TYPES = ["PolicyWording", "ClaimsChecklist", "CustomerFaq", "ClaimsProcedure", "RegulatoryGuidance", "Other"];

type PendingAction = { kind: "approve"; doc: KnowledgeDocView; sensitivity: KnowledgeSensitivity } | { kind: "archive"; doc: KnowledgeDocView };

export default function Knowledge() {
  const { docs, loading, error, upload, approve, archive, reindex } = useKnowledgeDocs();
  const [q, setQ] = useState("");
  const [showUpload, setShowUpload] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [reindexing, setReindexing] = useState(false);
  const [pending, setPending] = useState<PendingAction | null>(null);
  const [pendingSensitivity, setPendingSensitivity] = useState<Record<string, KnowledgeSensitivity>>({});
  const [form, setForm] = useState({ documentName: "", productLine: "", documentType: "PolicyWording", version: "1.0", effectiveDate: "", expiryDate: "" });
  const [file, setFile] = useState<File | null>(null);

  const rows = useMemo(() => docs.filter((d) => !q || d.title.toLowerCase().includes(q.toLowerCase())), [docs, q]);

  function allows(doc: KnowledgeDocView, action: string) {
    return doc.allowedActions.some((a) => a.action === action);
  }

  function blockedReason(doc: KnowledgeDocView, action: string) {
    return doc.blockedActions.find((a) => a.action === action)?.reason ?? null;
  }

  async function submitUpload() {
    if (!file || !form.documentName.trim() || !form.productLine.trim() || !form.version.trim()) {
      toast.error("File, document name, product line, and version are required.");
      return;
    }
    setUploading(true);
    const result = await upload({ file, ...form });
    setUploading(false);
    if (result.ok) {
      toast.success("Knowledge document uploaded as a draft.");
      setShowUpload(false);
      setForm({ documentName: "", productLine: "", documentType: "PolicyWording", version: "1.0", effectiveDate: "", expiryDate: "" });
      setFile(null);
    } else {
      toast.error(result.message ?? "Upload failed.");
    }
  }

  async function handleReindex() {
    setReindexing(true);
    const result = await reindex();
    setReindexing(false);
    if (result.ok) {
      toast.success(`Re-index complete. ${result.indexedCount ?? 0} approved document(s) indexed.`);
    } else {
      toast.error(result.message ?? "Re-index failed.");
    }
  }

  async function handleConfirm(reason: string) {
    if (!pending) return;
    const result = pending.kind === "approve"
      ? await approve(pending.doc.id, pending.sensitivity, reason)
      : await archive(pending.doc.id, reason);
    if (result.ok) {
      toast.success(pending.kind === "approve" ? "Document approved and indexed." : "Document archived.");
    } else {
      toast.error(result.message ?? `${pending.kind} failed.`);
    }
    setPending(null);
  }

  return (
    <>
      <SectionHeader
        eyebrow="Knowledge Hub"
        title="Approved sources for grounded AI answers"
        sub="Every knowledge source is classified, versioned, and cited. Coverage answers must be grounded in approved sources — model memory alone is not acceptable."
        action={
          <div className="flex gap-2">
            <button type="button" onClick={handleReindex} disabled={reindexing} className="inline-flex items-center gap-2 rounded-2xl border border-line bg-white px-4 py-2.5 text-[13px] font-medium text-ink hover-lift disabled:opacity-50">
              {reindexing ? <Loader2 className="h-4 w-4 animate-spin" /> : <RefreshCw className="h-4 w-4" />} Re-index approved
            </button>
            <button type="button" onClick={() => setShowUpload((v) => !v)} className="inline-flex items-center gap-2 rounded-2xl gradient-primary px-4 py-2.5 text-[13px] font-semibold text-white shadow-l2">
              <Upload className="h-4 w-4" /> Upload
            </button>
          </div>
        }
      />

      <div className="mb-4"><GuardrailBanner text="Unknown sensitivity is blocked from retrieval by default. A document cannot be approved for AI grounding without an explicit sensitivity classification and governance reason." /></div>

      {showUpload && (
        <GlassCard className="mb-4">
          <div className="mb-3 text-[14px] font-semibold text-ink">Upload knowledge document</div>
          <div className="grid gap-3 sm:grid-cols-2">
            <label className="sm:col-span-2">
              <span className="text-[11.5px] font-medium text-ink-soft">File (TXT, MD, PDF, or DOCX) *</span>
              <input type="file" accept=".txt,.md,.pdf,.docx" onChange={(e) => setFile(e.target.files?.[0] ?? null)} className="mt-1 w-full rounded-2xl border border-line bg-white/80 px-4 py-3 text-[13.5px] text-ink outline-none focus:ring-2 focus:ring-info/40" />
            </label>
            <Field label="Document name" value={form.documentName} onChange={(v) => setForm((f) => ({ ...f, documentName: v }))} required />
            <Field label="Product line" value={form.productLine} onChange={(v) => setForm((f) => ({ ...f, productLine: v }))} required />
            <label>
              <span className="text-[11.5px] font-medium text-ink-soft">Document type</span>
              <select value={form.documentType} onChange={(e) => setForm((f) => ({ ...f, documentType: e.target.value }))} className="mt-1 w-full rounded-2xl border border-line bg-white/80 px-4 py-3 text-[13.5px] text-ink outline-none focus:ring-2 focus:ring-info/40">
                {DOCUMENT_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
              </select>
            </label>
            <Field label="Version" value={form.version} onChange={(v) => setForm((f) => ({ ...f, version: v }))} required />
            <Field label="Effective date" value={form.effectiveDate} onChange={(v) => setForm((f) => ({ ...f, effectiveDate: v }))} type="date" />
            <Field label="Expiry date" value={form.expiryDate} onChange={(v) => setForm((f) => ({ ...f, expiryDate: v }))} type="date" />
          </div>
          <div className="mt-4 flex justify-end gap-2">
            <button type="button" onClick={() => setShowUpload(false)} className="rounded-2xl border border-line bg-white px-4 py-2.5 text-[13px] font-medium text-ink">Cancel</button>
            <button type="button" onClick={submitUpload} disabled={uploading} className="rounded-2xl gradient-primary px-4 py-2.5 text-[13px] font-semibold text-white shadow-l2 disabled:opacity-50">
              {uploading ? "Uploading…" : "Upload as draft"}
            </button>
          </div>
        </GlassCard>
      )}

      <div className="glass-elevated p-4 mb-4 flex items-center gap-3">
        <Search className="h-4 w-4 text-muted" />
        <input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Search knowledge titles…" className="flex-1 bg-transparent outline-none text-[14px] placeholder:text-muted text-ink" />
      </div>

      {loading ? (
        <GlassCard><div className="p-6 text-center text-[13px] text-muted">Loading knowledge sources…</div></GlassCard>
      ) : rows.length === 0 ? (
        <GlassCard><div className="p-10 text-center text-muted"><div className="text-ink font-medium">No knowledge documents</div><div className="text-[12.5px] mt-1">{error ? error : "Approved policy wordings, SOPs, and playbooks appear here once uploaded and indexed."}</div></div></GlassCard>
      ) : (
        <GlassCard className="!p-0 overflow-hidden">
          <table className="w-full text-[13px]">
            <thead className="bg-white/60">
              <tr className="text-left text-[11px] uppercase tracking-[0.14em] text-muted">
                <th className="p-3">Document</th><th className="p-3">Category</th><th className="p-3">Sensitivity</th><th className="p-3">Status</th><th className="p-3">Retrieval</th><th className="p-3">Updated</th><th className="p-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((d) => {
                const canApprove = allows(d, "approveKnowledgeDocument");
                const canArchive = allows(d, "archiveKnowledgeDocument");
                const approveBlocked = blockedReason(d, "approveKnowledgeDocument");
                const archiveBlocked = blockedReason(d, "archiveKnowledgeDocument");
                const selectedSensitivity = pendingSensitivity[d.id];
                return (
                  <tr key={d.id} className="border-t border-line/70 hover:bg-white/50 align-top">
                    <td className="p-3"><div className="flex items-center gap-2 text-ink"><BookOpen className="h-4 w-4 text-info" />{d.title}</div><div className="text-[10.5px] text-muted">{d.id}</div></td>
                    <td className="p-3 text-ink-soft">{d.category}</td>
                    <td className="p-3"><Badge tone={SENS_TONE[d.sensitivity]}>{SENS_LABEL[d.sensitivity]}</Badge></td>
                    <td className="p-3"><Badge tone={d.status === "Approved" ? "success" : d.status === "Archived" ? "neutral" : "warning"}>{d.status}</Badge></td>
                    <td className="p-3">{d.retrievalEligible ? <Badge tone="success">Eligible</Badge> : <Badge tone="error">Blocked</Badge>}</td>
                    <td className="p-3 text-muted">{d.updated}</td>
                    <td className="p-3">
                      <div className="flex flex-col items-end gap-1.5">
                        {canApprove && (
                          <div className="flex items-center gap-1.5">
                            <select
                              aria-label={`Sensitivity for ${d.title}`}
                              value={selectedSensitivity ?? ""}
                              onChange={(e) => setPendingSensitivity((current) => ({ ...current, [d.id]: e.target.value as KnowledgeSensitivity }))}
                              className="rounded-lg border border-line bg-white px-2 py-1.5 text-[11.5px] text-ink"
                            >
                              <option value="">Sensitivity…</option>
                              {SELECTABLE_SENSITIVITIES.map((s) => <option key={s} value={s}>{SENS_LABEL[s]}</option>)}
                            </select>
                            <button
                              type="button"
                              disabled={!selectedSensitivity}
                              onClick={() => selectedSensitivity && setPending({ kind: "approve", doc: d, sensitivity: selectedSensitivity })}
                              className="rounded-lg border border-line bg-white px-2.5 py-1.5 text-[11.5px] font-medium text-ink hover:bg-line/40 disabled:opacity-50"
                            >
                              Approve
                            </button>
                          </div>
                        )}
                        {canArchive && (
                          <button type="button" onClick={() => setPending({ kind: "archive", doc: d })} className="rounded-lg border border-line bg-white px-2.5 py-1.5 text-[11.5px] font-medium text-danger hover:bg-danger/5">Archive</button>
                        )}
                        {!canApprove && !canArchive && (
                          <span className="text-[11px] text-muted text-right" title={approveBlocked ?? archiveBlocked ?? undefined}>{approveBlocked ?? archiveBlocked ?? "No action available"}</span>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </GlassCard>
      )}

      <GovernedActionDialog
        open={!!pending}
        title={pending?.kind === "approve" ? "Approve knowledge document" : "Archive knowledge document"}
        target={pending?.doc.title ?? ""}
        impact={
          pending?.kind === "approve"
            ? `This makes the document eligible for AI grounding, classified as "${SENS_LABEL[pending.sensitivity]}". It will be indexed immediately.`
            : "This removes the document from AI grounding retrieval immediately."
        }
        auditNotice="This action is written to the audit trail with your reason and cannot be undone silently."
        confirmLabel={pending?.kind === "approve" ? "Approve" : "Archive"}
        tone={pending?.kind === "archive" ? "danger" : "primary"}
        onClose={() => setPending(null)}
        onConfirm={handleConfirm}
      />
    </>
  );
}

function Field({
  label, value, onChange, type = "text", required = false,
}: { label: string; value: string; onChange: (v: string) => void; type?: string; required?: boolean }) {
  return (
    <label>
      <span className="text-[11.5px] font-medium text-ink-soft">{label}{required ? " *" : ""}</span>
      <input
        value={value}
        onChange={(e) => onChange(e.target.value)}
        type={type}
        className="mt-1 w-full rounded-2xl border border-line bg-white/80 px-4 py-3 text-[13.5px] text-ink outline-none focus:ring-2 focus:ring-info/40"
      />
    </label>
  );
}
