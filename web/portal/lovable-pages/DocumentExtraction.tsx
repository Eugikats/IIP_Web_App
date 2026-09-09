"use client";

import Link from "next/link";
import { useParams, useSearchParams } from "next/navigation";
import { useEffect, useMemo, useRef, useState } from "react";
import { TriangleAlert, CheckCircle2, ExternalLink, FileSearch, FileText, Loader2, Save, ShieldCheck } from "lucide-react";
import { toast } from "sonner";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { Badge, ConfidenceBadge, GlassCard, SectionHeader, StatusBadge } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Field, Select } from "@/components/ui/Input";
import { LoadingSkeleton, EmptyStateSurface } from "@/components/ui/StateSurfaces";
import { documentContractLabel, type DocumentItem } from "@/lib/documentContracts";
import { useDocumentExtraction } from "@/lib/documentExtraction";
import { isValidWorkReference } from "@/lib/workItemLinks";

export default function DocumentExtraction() {
  const params = useParams<{ id: string }>();
  const searchParams = useSearchParams();
  const caseId = String(params.id ?? "");
  const requestedDocumentId = searchParams.get("documentId");
  const validCaseId = isValidWorkReference(caseId, "CLM");
  const validDocumentId = isValidWorkReference(requestedDocumentId, "DOC");
  const { documents, loading, error, errorKind, load, correctField, approve, preview } = useDocumentExtraction(validCaseId ? caseId : "");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [values, setValues] = useState<Record<string, string>>({});
  const [auditReason, setAuditReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [compactPanel, setCompactPanel] = useState<"review" | "source">("review");
  const sourcePanelRef = useRef<HTMLDivElement>(null);
  const reviewPanelRef = useRef<HTMLDivElement>(null);
  const [previewState, setPreviewState] = useState<{ url?: string; contentType?: string; error?: string }>({});

  useEffect(() => {
    if (selectedId && documents.some((document) => document.documentId === selectedId)) return;
    if (!validDocumentId) { setSelectedId(null); return; }
    setSelectedId(documents.find((document) => document.documentId === requestedDocumentId)?.documentId ?? null);
  }, [documents, requestedDocumentId, selectedId, validDocumentId]);

  const selected = documents.find((document) => document.documentId === selectedId) ?? null;

  useEffect(() => {
    setValues(Object.fromEntries((selected?.fields ?? []).map((field) => [field.name, field.effectiveValue])));
    setAuditReason("");
  }, [selected]);

  useEffect(() => {
    let active = true;
    let objectUrl: string | undefined;
    setPreviewState({});
    if (!selected) return () => { active = false; };
    void preview(selected).then((result) => {
      if (!active) { if (result.url) URL.revokeObjectURL(result.url); return; }
      objectUrl = result.url;
      setPreviewState(result.ok ? { url: result.url, contentType: result.contentType } : { error: result.message });
    });
    return () => { active = false; if (objectUrl) URL.revokeObjectURL(objectUrl); };
  }, [preview, selected]);

  const dirtyFields = useMemo(() => selected?.fields.filter((field) => values[field.name] !== field.effectiveValue) ?? [], [selected, values]);
  const allowed = (action: string) => selected?.allowedActions.some((candidate) => candidate.action === action) ?? false;
  const blockedReason = (action: string) => selected?.blockedActions.find((candidate) => candidate.action === action)?.reason;

  async function saveCorrections() {
    if (!selected || dirtyFields.length === 0) return;
    if (auditReason.trim().length < 3) { toast.error("Record why the extraction is being corrected."); return; }
    setBusy(true);
    for (const field of dirtyFields) {
      const result = await correctField(selected.documentId, field.name, values[field.name] ?? "", auditReason.trim());
      if (!result.ok) { toast.error(result.message); setBusy(false); return; }
    }
    setBusy(false);
    toast.success(`${dirtyFields.length} field correction${dirtyFields.length === 1 ? "" : "s"} saved and audited.`);
  }

  async function approveExtraction() {
    if (!selected) return;
    if (dirtyFields.length > 0) { toast.error("Save field corrections before approval."); return; }
    if (auditReason.trim().length < 3) { toast.error("Record the evidence checked before approval."); return; }
    setBusy(true);
    const result = await approve(selected.documentId, auditReason.trim());
    setBusy(false);
    result.ok ? toast.success("Extraction approved with a named-human audit event.") : toast.error(result.message);
  }

  function showCompactPanel(panel: "review" | "source") {
    setCompactPanel(panel);
    window.requestAnimationFrame(() => {
      (panel === "source" ? sourcePanelRef.current : reviewPanelRef.current)?.focus();
    });
  }

  return (
    <>
      <SectionHeader
        eyebrow="Release 1 · Document-to-Decision"
        eyebrowClassName="text-dls-text-secondary"
        title={selected ? `Extraction Review · ${documentContractLabel(selected.documentType)}` : "Extraction Review"}
        sub={selected ? `${selected.originalFileName} · Claim ${caseId} · Compare every effective value with the authorized source.` : `Claim ${caseId} · Source-grounded document review.`}
        action={selected ? <div className="flex flex-wrap items-center gap-2"><StatusBadge status={selected.processingStatus} /><ConfidenceBadge value={selected.confidenceScore} />{selected.extractionApproved ? <Badge tone="success">Human approved</Badge> : <Badge tone="warning">Review required</Badge>}</div> : undefined}
      />
      <div className="mb-5"><GuardrailBanner text="This screen reviews extracted document facts only. Approval does not approve a claim, coverage position, settlement, payment, or fraud conclusion." /></div>

      {loading ? <GlassCard><LoadingSkeleton rows={4} /></GlassCard> : null}
      {!loading && !selected ? (
        <GlassCard>
          <div className="flex flex-col items-center gap-2 py-16 text-center">
            <FileSearch className="h-7 w-7 text-dls-text-muted" aria-hidden="true" />
            <EmptyStateSurface
              message={!validCaseId ? "This claim link is invalid." : !validDocumentId ? "This document link is invalid or incomplete." : error ?? "The document was not found on this claim, or is no longer accessible."}
              action={errorKind === "unavailable" && validCaseId && validDocumentId ? { label: "Try again", onClick: () => void load() } : undefined}
            />
            <p className="text-[13px] font-bold text-dls-text-primary">{errorKind === "unauthorized" ? "Extraction review unavailable" : "Document not available for extraction review"}</p>
            <Link href={validCaseId ? `/app/claims/${caseId}` : "/app/claims"} className="dls-focus-outline mt-2 inline-flex min-h-11 items-center rounded-dls-control border border-dls-border-input bg-dls-surface px-4 py-[9px] text-[13px] font-bold text-dls-text-primary hover:bg-dls-bg-page">
              {validCaseId ? "Return to claim" : "Return to claims"}
            </Link>
          </div>
        </GlassCard>
      ) : null}

      {selected ? <>
        {documents.length > 1 ? (
          <div className="mb-4 max-w-sm">
            <Field label="Document">
              <Select id="extraction-document" value={selected.documentId} onChange={(event) => setSelectedId(event.target.value)}>
                {documents.map((document) => <option key={document.documentId} value={document.documentId}>{document.originalFileName} · {documentContractLabel(document.documentType)}</option>)}
              </Select>
            </Field>
          </div>
        ) : null}

        <div className="mb-4 grid grid-cols-2 rounded-dls-control border border-dls-border-subtle bg-dls-surface p-1 xl:hidden" role="tablist" aria-label="Evidence workspace view">
          <button
            type="button"
            role="tab"
            id="review-fields-tab"
            aria-controls="evidence-review-panel"
            aria-selected={compactPanel === "review"}
            onClick={() => showCompactPanel("review")}
            className={`dls-focus-outline min-h-11 rounded-dls-control px-3 text-[12px] font-bold transition-colors duration-dls-fast ease-dls ${compactPanel === "review" ? "bg-dls-primary text-dls-text-inverse" : "text-dls-text-secondary hover:bg-dls-bg-page"}`}
          >
            Review fields
          </button>
          <button
            type="button"
            role="tab"
            id="source-document-tab"
            aria-controls="evidence-source-panel"
            aria-selected={compactPanel === "source"}
            onClick={() => showCompactPanel("source")}
            className={`dls-focus-outline min-h-11 rounded-dls-control px-3 text-[12px] font-bold transition-colors duration-dls-fast ease-dls ${compactPanel === "source" ? "bg-dls-primary text-dls-text-inverse" : "text-dls-text-secondary hover:bg-dls-bg-page"}`}
          >
            Source document
          </button>
        </div>

        <div className="grid min-w-0 gap-4 xl:grid-cols-[minmax(0,1.25fr)_minmax(380px,0.75fr)]">
          <div id="evidence-source-panel" data-testid="evidence-source-panel" role="tabpanel" aria-labelledby="source-document-tab" ref={sourcePanelRef} tabIndex={-1} className={`${compactPanel === "source" ? "block" : "hidden"} min-w-0 max-h-[calc(100dvh-12rem)] overflow-y-auto overscroll-contain pb-[env(safe-area-inset-bottom)] focus:outline-none xl:block xl:max-h-none xl:overflow-visible xl:pb-0`}>
          <GlassCard className="overflow-hidden !p-4">
            <div className="mb-3 flex flex-wrap items-center justify-between gap-2"><div className="flex items-center gap-2 text-[13px] font-bold text-dls-text-primary"><FileText className="h-4 w-4 text-dls-info-text" aria-hidden="true" /> {selected.originalFileName}</div><div className="text-[11px] text-dls-text-muted">{selected.contentType} · {(selected.fileSizeBytes / 1024).toFixed(1)} KB</div></div>
            <div className="min-h-[560px] overflow-hidden rounded-dls-card border border-dls-border-subtle bg-dls-bg-page">
              {previewState.url && previewState.contentType?.startsWith("image/") ? <img src={previewState.url} alt={`Authorized preview of ${selected.originalFileName}`} className="mx-auto max-h-[760px] w-full object-contain" /> : null}
              {previewState.url && previewState.contentType === "application/pdf" ? <iframe title={`Authorized preview of ${selected.originalFileName}`} src={previewState.url} className="h-[680px] w-full bg-dls-surface" /> : null}
              {!previewState.url ? <div className="grid min-h-[560px] place-items-center p-8 text-center"><div><FileText className="mx-auto h-8 w-8 text-dls-text-muted" aria-hidden="true" /><div className="mt-3 text-[13px] font-bold text-dls-text-primary">{previewState.error ? "Source preview unavailable" : "Loading authorized source preview…"}</div><div className="mt-1 max-w-md text-[11.5px] leading-5 text-dls-text-muted">{previewState.error ?? "The preview is loaded through the tenant-scoped document-content endpoint."}</div></div></div> : null}
            </div>
            <div className="mt-3 flex items-start gap-2 rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3 text-[11.5px] leading-5 text-dls-text-muted"><ExternalLink className="mt-0.5 h-3.5 w-3.5 shrink-0 text-dls-info-text" aria-hidden="true" /> Source-coordinate highlighting will appear when the configured extraction provider returns page geometry. IIP does not invent coordinates when they are absent.</div>
          </GlassCard>
          </div>

          <div id="evidence-review-panel" data-testid="evidence-review-panel" role="tabpanel" aria-labelledby="review-fields-tab" ref={reviewPanelRef} tabIndex={-1} className={`${compactPanel === "review" ? "block" : "hidden"} min-w-0 max-h-[calc(100dvh-12rem)] space-y-4 overflow-y-auto overscroll-contain pb-[env(safe-area-inset-bottom)] focus:outline-none xl:block xl:max-h-none xl:overflow-visible xl:pb-0`}>
            <GlassCard>
              <div className="mb-3 flex items-center justify-between"><div className="text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Extracted fields · {selected.fields.length}</div>{dirtyFields.length > 0 ? <Badge tone="warning">{dirtyFields.length} unsaved</Badge> : <Badge tone="success">Values synchronized</Badge>}</div>
              <div className="max-h-[560px] space-y-2.5 overflow-y-auto pr-1">
                {selected.fields.map((field) => <div key={field.name} className={`rounded-dls-card border px-3 py-2.5 ${values[field.name] !== field.effectiveValue ? "border-dls-warning-dot bg-dls-warning-tint" : "border-dls-border-subtle bg-dls-surface"}`}>
                  <div className="mb-1 flex items-center justify-between gap-2"><div className="text-[11px] text-dls-text-muted">{documentContractLabel(field.name)}</div>{typeof field.confidenceScore === "number" ? <ConfidenceBadge value={field.confidenceScore} /> : <Badge tone="neutral">Confidence unavailable</Badge>}</div>
                  <input aria-label={documentContractLabel(field.name)} value={values[field.name] ?? ""} onChange={(event) => setValues((current) => ({ ...current, [field.name]: event.target.value }))} disabled={!allowed("correctDocumentExtraction")} className="min-h-11 w-full bg-transparent text-[13px] text-dls-text-primary outline-none disabled:text-dls-text-muted" />
                  {field.warnings.map((warning) => <div key={warning.code} className="mt-1 flex items-start gap-1 text-[10.5px] text-dls-warning-text"><TriangleAlert className="mt-0.5 h-3 w-3 shrink-0" aria-hidden="true" /> {warning.message}</div>)}
                </div>)}
                {selected.fields.length === 0 ? <div className="rounded-dls-card border border-dashed border-dls-border-subtle p-8 text-center text-[12px] text-dls-text-muted">No structured fields were returned by the configured provider.</div> : null}
              </div>
            </GlassCard>

            {selected.validationWarnings.length > 0 ? <GlassCard><div className="text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Document warnings</div><div className="mt-3 space-y-2">{selected.validationWarnings.map((warning) => <div key={warning.code} className="flex items-start gap-2 rounded-dls-card bg-dls-warning-tint p-3 text-[11.5px] text-dls-warning-text"><TriangleAlert className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" /><div><div>{warning.fieldName ? <span className="font-bold">{warning.fieldName}: </span> : null}{warning.message}</div>{warning.expectedValue || warning.observedValue ? <div className="mt-0.5 text-[10.5px] text-dls-text-muted">{warning.expectedValue ? <>Expected {warning.expectedValue}</> : null}{warning.expectedValue && warning.observedValue ? " — " : null}{warning.observedValue ? <>Found &ldquo;{warning.observedValue}&rdquo;</> : null}</div> : null}</div></div>)}</div></GlassCard> : null}

            <GlassCard>
              <Field label="Review reason">
                <textarea
                  id="extraction-audit-reason"
                  aria-label="Review reason"
                  value={auditReason}
                  onChange={(event) => setAuditReason(event.target.value)}
                  placeholder="Record what source evidence you checked and why any correction or approval is appropriate."
                  className="mt-2 min-h-20 w-full rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-2.5 text-[12px] text-dls-text-primary placeholder:text-dls-text-muted focus:outline-none focus:border-dls-primary focus:ring-[3px] focus:ring-dls-primary/[0.12]"
                />
              </Field>
              <div className="mt-4 grid gap-2 sm:grid-cols-2">
                <Button variant="secondary" icon={<Save />} disabled={busy || dirtyFields.length === 0 || auditReason.trim().length < 3 || !allowed("correctDocumentExtraction")} onClick={() => void saveCorrections()}>
                  Save corrections
                </Button>
                <Button variant="primary" icon={busy ? <Loader2 className="animate-spin" /> : <CheckCircle2 />} disabled={busy || dirtyFields.length > 0 || auditReason.trim().length < 3 || !allowed("approveDocumentExtraction")} onClick={() => void approveExtraction()}>
                  Approve extraction
                </Button>
              </div>
              {!allowed("correctDocumentExtraction") && blockedReason("correctDocumentExtraction") ? <div className="mt-3 text-[11px] text-dls-text-muted">Correction unavailable: {blockedReason("correctDocumentExtraction")}</div> : null}
              {!allowed("approveDocumentExtraction") && blockedReason("approveDocumentExtraction") ? <div className="mt-1 text-[11px] text-dls-text-muted">Approval unavailable: {blockedReason("approveDocumentExtraction")}</div> : null}
              <Link href="/app/work-items" className="dls-focus-outline mt-3 inline-flex items-center gap-2 rounded-dls-tight text-[11.5px] font-bold text-dls-primary hover:underline"><ShieldCheck className="h-3.5 w-3.5" aria-hidden="true" /> Open Review & Approvals</Link>
            </GlassCard>
          </div>
        </div>
      </> : null}
    </>
  );
}
