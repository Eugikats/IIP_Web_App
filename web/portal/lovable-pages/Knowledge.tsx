"use client";
import { Fragment, useId, useMemo, useState } from "react";
import { toast } from "sonner";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Input, Select, Field as UIField } from "@/components/ui/Input";
import { Table, TableHead, TableBody, TableRow, TableHeaderCell, TableCell } from "@/components/ui/Table";
import { LoadingSkeleton, ErrorStateSurface, EmptyStateSurface, EmptyFilteredStateSurface } from "@/components/ui/StateSurfaces";
import {
  useKnowledgeDocs, SELECTABLE_SENSITIVITIES, type KnowledgeDocView, type KnowledgeSensitivity, type KnowledgeChunkView,
} from "@/lib/lovable-parity/data/knowledge";
import { BookOpen, Search, Upload, RefreshCw, Loader2, AlertTriangle, ChevronDown, ChevronRight } from "lucide-react";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { GovernedActionDialog } from "@/components/GovernedActionDialog";
import PolicyQuestionPanel from "@/components/PolicyQuestionPanel";

// "violet" is Badge's legacy tone alias — it degrades to "info" internally (design language §8.7:
// violet marks actions, not status), so customer_confidential uses "info" directly in new code.
const SENS_TONE: Record<KnowledgeSensitivity, "success" | "info" | "warning" | "error" | "neutral"> = {
  approved: "success",
  customer_confidential: "info",
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

// CompanyProfile/ProductOverview/Faq are acquisition-facing (blueprint section 8.4) — content
// meant for a prospect, not an existing policyholder. Only classifying a document `approved`
// makes it eligible for customer-channel retrieval; DocumentType alone grants nothing.
const DOCUMENT_TYPES = ["PolicyWording", "ClaimsChecklist", "CustomerFaq", "ClaimsProcedure", "RegulatoryGuidance", "CompanyProfile", "ProductOverview", "Faq", "Other"];

type PendingAction =
  | { kind: "approve"; doc: KnowledgeDocView; sensitivity: KnowledgeSensitivity }
  | { kind: "classify"; doc: KnowledgeDocView; sensitivity: KnowledgeSensitivity }
  | { kind: "archive"; doc: KnowledgeDocView };

export default function Knowledge() {
  const { docs, loading, error, upload, approve, classify, archive, reindex, loadChunks } = useKnowledgeDocs();
  const [q, setQ] = useState("");
  const [showUpload, setShowUpload] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [reindexing, setReindexing] = useState(false);
  const [pending, setPending] = useState<PendingAction | null>(null);
  // WO 7.4: per-document chunk preview — which chunks this document was actually split into for
  // retrieval, fetched on demand when a row is expanded rather than with the document list.
  const [expandedDocId, setExpandedDocId] = useState<string | null>(null);
  const [chunksByDoc, setChunksByDoc] = useState<Record<string, KnowledgeChunkView[]>>({});
  const [chunksLoadingId, setChunksLoadingId] = useState<string | null>(null);
  const [chunksErrorByDoc, setChunksErrorByDoc] = useState<Record<string, string>>({});

  async function toggleChunkPreview(docId: string) {
    if (expandedDocId === docId) {
      setExpandedDocId(null);
      return;
    }
    setExpandedDocId(docId);
    if (chunksByDoc[docId] || chunksLoadingId === docId) return;
    setChunksLoadingId(docId);
    const result = await loadChunks(docId);
    setChunksLoadingId(null);
    if (result.ok) {
      setChunksByDoc((current) => ({ ...current, [docId]: result.chunks ?? [] }));
    } else {
      setChunksErrorByDoc((current) => ({ ...current, [docId]: result.message ?? "Could not load the chunk preview." }));
    }
  }
  const [pendingSensitivity, setPendingSensitivity] = useState<Record<string, KnowledgeSensitivity>>({});
  const [form, setForm] = useState({ documentName: "", productLine: "", documentType: "PolicyWording", version: "1.0", effectiveDate: "", expiryDate: "" });
  const [file, setFile] = useState<File | null>(null);
  const fileFieldId = useId();

  const rows = useMemo(() => docs.filter((d) => !q || d.title.toLowerCase().includes(q.toLowerCase())), [docs, q]);
  const stuckCount = useMemo(() => docs.filter((d) => d.status === "Approved" && !d.retrievalEligible).length, [docs]);

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
      : pending.kind === "classify"
        ? await classify(pending.doc.id, pending.sensitivity, reason)
        : await archive(pending.doc.id, reason);
    if (result.ok) {
      toast.success(
        pending.kind === "approve" ? "Document approved and indexed."
        : pending.kind === "classify" ? "Sensitivity set. Document re-indexed and eligible for retrieval."
        : "Document archived."
      );
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
          // flex-wrap, not a rigid single row: at widths around 768px, SectionHeader's own
          // sm:flex-row breakpoint (640px) puts the title block and this two-button action side
          // by side, and "Re-index approved" + "Upload" together are wider than the space left —
          // without wrap, "Upload" rendered past the viewport edge, silently clipped by the page's
          // own overflow-x guard rather than visibly scrolling, so half the button was simply
          // unclickable. Wrapping lets Upload drop to its own line instead of running off-screen.
          <div className="flex flex-wrap justify-end gap-2">
            <Button variant="secondary" onClick={handleReindex} disabled={reindexing} icon={reindexing ? <Loader2 className="animate-spin" aria-hidden="true" /> : <RefreshCw aria-hidden="true" />}>
              Re-index approved
            </Button>
            <Button variant="primary" onClick={() => setShowUpload((v) => !v)} icon={<Upload aria-hidden="true" />}>
              Upload
            </Button>
          </div>
        }
      />

      <div className="mb-4"><GuardrailBanner text="Unknown sensitivity is blocked from retrieval by default. A document cannot be approved for AI grounding without an explicit sensitivity classification and governance reason." /></div>

      {stuckCount > 0 && (
        <div className="mb-4 flex items-center gap-2.5 rounded-dls-card bg-dls-warning-tint px-4 py-3 text-[12.5px] text-dls-warning-text">
          <AlertTriangle className="h-4 w-4 shrink-0" aria-hidden="true" />
          <span>
            {stuckCount} approved document{stuckCount === 1 ? " is" : "s are"} marked <b>Approved</b> but blocked from retrieval — approved before a sensitivity classification was required, so nothing has ever been able to answer from them.
            Set a classification below to fix each one.
          </span>
        </div>
      )}

      <div className="mb-4"><PolicyQuestionPanel /></div>

      {showUpload && (
        <GlassCard className="mb-4">
          <div className="mb-3 text-[15px] font-bold text-dls-text-primary">Upload knowledge document</div>
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="sm:col-span-2">
              <UIField label="File (TXT, MD, PDF, or DOCX) *" htmlFor={fileFieldId}>
                <input
                  id={fileFieldId}
                  type="file"
                  accept=".txt,.md,.pdf,.docx"
                  onChange={(e) => setFile(e.target.files?.[0] ?? null)}
                  className="w-full rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-[9px] text-sm text-dls-text-primary file:mr-3 file:rounded-dls-control file:border-0 file:bg-dls-bg-page file:px-3 file:py-1.5 file:text-[12.5px] file:font-semibold file:text-dls-text-secondary focus:outline-none focus:border-dls-primary focus:ring-[3px] focus:ring-dls-primary/[0.12]"
                />
              </UIField>
            </div>
            <Field label="Document name" value={form.documentName} onChange={(v) => setForm((f) => ({ ...f, documentName: v }))} required />
            <Field label="Product line" value={form.productLine} onChange={(v) => setForm((f) => ({ ...f, productLine: v }))} required />
            <Field label="Document type" as="select" value={form.documentType} onChange={(v) => setForm((f) => ({ ...f, documentType: v }))} options={DOCUMENT_TYPES} />
            <Field label="Version" value={form.version} onChange={(v) => setForm((f) => ({ ...f, version: v }))} required />
            <Field label="Effective date" value={form.effectiveDate} onChange={(v) => setForm((f) => ({ ...f, effectiveDate: v }))} type="date" />
            <Field label="Expiry date" value={form.expiryDate} onChange={(v) => setForm((f) => ({ ...f, expiryDate: v }))} type="date" />
          </div>
          <div className="mt-4 flex justify-end gap-2">
            <Button variant="ghost" onClick={() => setShowUpload(false)}>Cancel</Button>
            <Button variant="primary" onClick={submitUpload} disabled={uploading}>
              {uploading ? "Uploading…" : "Upload as draft"}
            </Button>
          </div>
        </GlassCard>
      )}

      <div className="mb-4 max-w-md">
        <Input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Search knowledge titles…" leadingIcon={<Search aria-hidden="true" />} aria-label="Search knowledge titles…" />
      </div>

      {loading ? (
        <GlassCard><LoadingSkeleton rows={5} /></GlassCard>
      ) : error ? (
        <GlassCard><ErrorStateSurface title="Knowledge sources could not be loaded" message={error} /></GlassCard>
      ) : docs.length === 0 ? (
        <GlassCard><EmptyStateSurface title="No knowledge documents" message="Approved policy wordings, SOPs, and playbooks appear here once uploaded and indexed." /></GlassCard>
      ) : rows.length === 0 ? (
        <GlassCard><EmptyFilteredStateSurface onClear={() => setQ("")} /></GlassCard>
      ) : (
        <Table label="Knowledge sources">
          <TableHead>
            <TableRow>
              <TableHeaderCell>Document</TableHeaderCell>
              <TableHeaderCell>Category</TableHeaderCell>
              <TableHeaderCell>Sensitivity</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
              <TableHeaderCell>Retrieval</TableHeaderCell>
              <TableHeaderCell>Updated</TableHeaderCell>
              <TableHeaderCell numeric>Actions</TableHeaderCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((d) => {
              const canApprove = allows(d, "approveKnowledgeDocument");
              const canClassify = allows(d, "classifyKnowledgeDocument");
              const canArchive = allows(d, "archiveKnowledgeDocument");
              const approveBlocked = blockedReason(d, "approveKnowledgeDocument");
              const archiveBlocked = blockedReason(d, "archiveKnowledgeDocument");
              const stuck = d.status === "Approved" && !d.retrievalEligible;
              const selectedSensitivity = pendingSensitivity[d.id];
              return (
                <Fragment key={d.id}>
                <TableRow className={"align-top" + (stuck ? " bg-dls-warning-tint" : "")}>
                  <TableCell>
                    <button
                      type="button"
                      onClick={() => toggleChunkPreview(d.id)}
                      className="dls-focus-outline flex items-center gap-2 text-dls-text-primary hover:text-dls-info-text"
                      aria-expanded={expandedDocId === d.id}
                      title="Preview the chunks this document was split into for retrieval"
                    >
                      {expandedDocId === d.id ? <ChevronDown className="h-4 w-4 shrink-0 text-dls-text-muted" aria-hidden="true" /> : <ChevronRight className="h-4 w-4 shrink-0 text-dls-text-muted" aria-hidden="true" />}
                      <BookOpen className="h-4 w-4 text-dls-info-text" aria-hidden="true" />
                      {d.title}
                    </button>
                    <div className="text-[11px] text-dls-text-muted">{d.id}</div>
                  </TableCell>
                  <TableCell className="text-dls-text-secondary">{d.category}</TableCell>
                  <TableCell><Badge tone={SENS_TONE[d.sensitivity]}>{SENS_LABEL[d.sensitivity]}</Badge></TableCell>
                  <TableCell><Badge tone={d.status === "Approved" ? "success" : d.status === "Archived" ? "neutral" : "warning"}>{d.status}</Badge></TableCell>
                  <TableCell>{d.retrievalEligible ? <Badge tone="success">Eligible</Badge> : <Badge tone="error">Blocked</Badge>}</TableCell>
                  <TableCell className="text-dls-text-muted">{d.updated}</TableCell>
                  <TableCell numeric>
                    <div className="flex flex-col items-end gap-1.5">
                      {canApprove && (
                        <div className="flex items-center gap-1.5">
                          <Select
                            aria-label={`Sensitivity for ${d.title}`}
                            value={selectedSensitivity ?? ""}
                            onChange={(e) => setPendingSensitivity((current) => ({ ...current, [d.id]: e.target.value as KnowledgeSensitivity }))}
                          >
                            <option value="">Sensitivity…</option>
                            {SELECTABLE_SENSITIVITIES.map((s) => <option key={s} value={s}>{SENS_LABEL[s]}</option>)}
                          </Select>
                          <Button
                            variant="secondary"
                            disabled={!selectedSensitivity}
                            onClick={() => selectedSensitivity && setPending({ kind: "approve", doc: d, sensitivity: selectedSensitivity })}
                          >
                            Approve
                          </Button>
                        </div>
                      )}
                      {canClassify && (
                        <div className="flex items-center gap-1.5">
                          <Select
                            aria-label={`Sensitivity for ${d.title}`}
                            value={selectedSensitivity ?? ""}
                            onChange={(e) => setPendingSensitivity((current) => ({ ...current, [d.id]: e.target.value as KnowledgeSensitivity }))}
                          >
                            <option value="">Sensitivity…</option>
                            {SELECTABLE_SENSITIVITIES.map((s) => <option key={s} value={s}>{SENS_LABEL[s]}</option>)}
                          </Select>
                          <Button
                            variant="secondary"
                            disabled={!selectedSensitivity}
                            onClick={() => selectedSensitivity && setPending({ kind: "classify", doc: d, sensitivity: selectedSensitivity })}
                            title={stuck ? "This document was approved before sensitivity classification was required and cannot be retrieved until classified." : "Correct this document's sensitivity classification."}
                          >
                            {stuck ? "Classify to restore retrieval" : "Reclassify"}
                          </Button>
                        </div>
                      )}
                      {canArchive && (
                        // Same hand-sized destructive treatment as Communications.tsx's Reject —
                        // Button's destructive variant is zero-padding by design (§8.4) and would
                        // misalign here.
                        <button
                          type="button"
                          onClick={() => setPending({ kind: "archive", doc: d })}
                          className="dls-focus-outline inline-flex min-h-11 items-center justify-center rounded-dls-control px-4 py-[9px] text-[13px] font-bold text-dls-danger-text transition-colors duration-dls-fast ease-dls hover:underline"
                        >
                          Archive
                        </button>
                      )}
                      {!canApprove && !canClassify && !canArchive && (
                        <span className="text-right text-[11px] text-dls-text-muted" title={approveBlocked ?? archiveBlocked ?? undefined}>{approveBlocked ?? archiveBlocked ?? "No action available"}</span>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
                {expandedDocId === d.id ? (
                  <TableRow>
                    <TableCell colSpan={7}>
                      <ChunkPreview
                        loading={chunksLoadingId === d.id}
                        error={chunksErrorByDoc[d.id] ?? null}
                        chunks={chunksByDoc[d.id]}
                      />
                    </TableCell>
                  </TableRow>
                ) : null}
                </Fragment>
              );
            })}
          </TableBody>
        </Table>
      )}

      <GovernedActionDialog
        open={!!pending}
        title={
          pending?.kind === "approve" ? "Approve knowledge document"
          : pending?.kind === "classify" ? "Set sensitivity classification"
          : "Archive knowledge document"
        }
        target={pending?.doc.title ?? ""}
        impact={
          pending?.kind === "approve"
            ? `This makes the document eligible for AI grounding, classified as "${SENS_LABEL[pending.sensitivity]}". It will be indexed immediately.`
            : pending?.kind === "classify"
              ? `This sets the document's sensitivity to "${SENS_LABEL[pending.sensitivity]}" without changing its approval. The document is re-indexed immediately and becomes eligible for AI grounding.`
              : "This removes the document from AI grounding retrieval immediately."
        }
        auditNotice="This action is written to the audit trail with your reason and cannot be undone silently."
        confirmLabel={pending?.kind === "approve" ? "Approve" : pending?.kind === "classify" ? "Set classification" : "Archive"}
        tone={pending?.kind === "archive" ? "danger" : "primary"}
        onClose={() => setPending(null)}
        onConfirm={handleConfirm}
      />
    </>
  );
}

// WO 7.4: per-document chunk preview — the actual chunks a document was split into for
// retrieval, with the same excerpt + source label an AI answer's citation already shows (see
// SourceCitationList), so an officer can see exactly what the assistant could ground an answer
// in, not just that "some chunk somewhere" exists.
function ChunkPreview({ loading, error, chunks }: { loading: boolean; error: string | null; chunks?: KnowledgeChunkView[] }) {
  if (loading) {
    return <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4 text-[12.5px] text-dls-text-muted">Loading chunk preview…</div>;
  }
  if (error) {
    return <div className="rounded-dls-card bg-dls-danger-tint p-4 text-[12.5px] text-dls-danger-text">{error}</div>;
  }
  if (!chunks || chunks.length === 0) {
    return <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4 text-[12.5px] text-dls-text-muted">No chunks yet — this document has not been indexed.</div>;
  }
  return (
    <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4">
      <div className="mb-2 text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">
        {chunks.length} chunk{chunks.length === 1 ? "" : "s"} — what retrieval can ground an answer in
      </div>
      <ul className="space-y-2">
        {chunks.map((chunk) => (
          <li key={chunk.chunkId} className="rounded-dls-control border border-dls-border-subtle bg-dls-surface p-3">
            <p className="font-mono text-[11.5px] font-semibold text-dls-text-primary">{chunk.sourceLabel}</p>
            <p className="mt-1 text-[12.5px] leading-6 text-dls-text-secondary">{chunk.excerpt}</p>
          </li>
        ))}
      </ul>
    </div>
  );
}

// Delegates to components/ui/Input.tsx's Field/Input/Select — same precedent as FNOL.tsx's local
// wrapper, kept local here since only this screen's upload form needs it and it isn't used at
// FNOL.tsx's ~25-call-site scale.
function Field({
  label, value, onChange, type = "text", required = false, as, options,
}: {
  label: string; value: string; onChange: (v: string) => void; type?: string; required?: boolean;
  as?: "select"; options?: string[];
}) {
  const id = useId();
  return (
    <UIField label={required ? `${label} *` : label} htmlFor={id}>
      {as === "select" && options ? (
        <Select id={id} value={value} onChange={(e) => onChange(e.target.value)} className="w-full">
          {options.map((option) => <option key={option} value={option}>{option}</option>)}
        </Select>
      ) : (
        <Input id={id} value={value} onChange={(e) => onChange(e.target.value)} type={type} />
      )}
    </UIField>
  );
}
