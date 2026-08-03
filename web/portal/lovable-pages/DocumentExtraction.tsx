"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { AlertTriangle, CheckCircle2, ExternalLink, FileSearch, FileText, Loader2, Save, ShieldCheck } from "lucide-react";
import { toast } from "sonner";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { Badge, ConfidenceBadge, GlassCard, SectionHeader, StatusBadge } from "@/components/iip/Primitives";
import { documentContractLabel, type DocumentItem } from "@/lib/documentContracts";
import { useDocumentExtraction } from "@/lib/documentExtraction";

export default function DocumentExtraction() {
  const params = useParams<{ id: string }>();
  const caseId = String(params.id ?? "");
  const { documents, loading, error, correctField, approve, preview } = useDocumentExtraction(caseId);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [values, setValues] = useState<Record<string, string>>({});
  const [auditReason, setAuditReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [previewState, setPreviewState] = useState<{ url?: string; contentType?: string; error?: string }>({});

  useEffect(() => {
    if (selectedId && documents.some((document) => document.documentId === selectedId)) return;
    const requested = typeof window === "undefined" ? null : new URLSearchParams(window.location.search).get("documentId");
    setSelectedId(documents.find((document) => document.documentId === requested)?.documentId ?? documents[0]?.documentId ?? null);
  }, [documents, selectedId]);

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

  return (
    <>
      <SectionHeader
        eyebrow="Release 1 · Document-to-Decision"
        title={selected ? `Extraction Review · ${documentContractLabel(selected.documentType)}` : "Extraction Review"}
        sub={selected ? `${selected.originalFileName} · Claim ${caseId} · Compare every effective value with the authorized source.` : `Claim ${caseId} · Source-grounded document review.`}
        action={selected ? <div className="flex flex-wrap items-center gap-2"><StatusBadge status={selected.processingStatus} /><ConfidenceBadge value={selected.confidenceScore} />{selected.extractionApproved ? <Badge tone="success">Human approved</Badge> : <Badge tone="warning">Review required</Badge>}</div> : undefined}
      />
      <div className="mb-5"><GuardrailBanner text="This screen reviews extracted document facts only. Approval does not approve a claim, coverage position, settlement, payment, or fraud conclusion." /></div>

      {loading ? <GlassCard><div className="flex items-center justify-center gap-2 py-16 text-[13px] text-stone"><Loader2 className="h-4 w-4 animate-spin" /> Loading case documents…</div></GlassCard> : null}
      {!loading && !selected ? <GlassCard><div className="py-16 text-center"><FileSearch className="mx-auto h-7 w-7 text-stone" /><div className="mt-3 font-medium text-navy">No document available for extraction review</div><div className="mt-1 text-[12.5px] text-stone">{error ?? "Upload a real claim document before opening Release 1 review."}</div><Link href={`/app/claims/${caseId}`} className="mt-4 inline-flex rounded-xl border border-cloud bg-white px-3 py-2 text-[11.5px] font-medium text-navy">Return to claim</Link></div></GlassCard> : null}

      {selected ? <>
        {documents.length > 1 ? <div className="mb-4 flex flex-wrap items-center gap-2"><span className="text-[11px] uppercase tracking-[0.12em] text-stone">Document</span><select value={selected.documentId} onChange={(event) => setSelectedId(event.target.value)} className="rounded-xl border border-cloud bg-white px-3 py-2 text-[12px] text-navy">{documents.map((document) => <option key={document.documentId} value={document.documentId}>{document.originalFileName} · {documentContractLabel(document.documentType)}</option>)}</select></div> : null}

        <div className="grid gap-4 xl:grid-cols-[minmax(0,1.25fr)_minmax(380px,0.75fr)]">
          <GlassCard className="!p-4 overflow-hidden">
            <div className="mb-3 flex flex-wrap items-center justify-between gap-2"><div className="flex items-center gap-2 text-[13px] font-medium text-navy"><FileText className="h-4 w-4 text-info" /> {selected.originalFileName}</div><div className="text-[11px] text-stone">{selected.contentType} · {(selected.fileSizeBytes / 1024).toFixed(1)} KB</div></div>
            <div className="min-h-[560px] overflow-hidden rounded-2xl border border-cloud bg-mist/50">
              {previewState.url && previewState.contentType?.startsWith("image/") ? <img src={previewState.url} alt={`Authorized preview of ${selected.originalFileName}`} className="mx-auto max-h-[760px] w-full object-contain" /> : null}
              {previewState.url && previewState.contentType === "application/pdf" ? <iframe title={`Authorized preview of ${selected.originalFileName}`} src={previewState.url} className="h-[680px] w-full bg-white" /> : null}
              {!previewState.url ? <div className="grid min-h-[560px] place-items-center p-8 text-center"><div><FileText className="mx-auto h-8 w-8 text-stone" /><div className="mt-3 text-[13px] font-medium text-navy">{previewState.error ? "Source preview unavailable" : "Loading authorized source preview…"}</div><div className="mt-1 max-w-md text-[11.5px] leading-5 text-stone">{previewState.error ?? "The preview is loaded through the tenant-scoped document-content endpoint."}</div></div></div> : null}
            </div>
            <div className="mt-3 flex items-start gap-2 rounded-xl border border-cloud bg-white/60 p-3 text-[11.5px] leading-5 text-stone"><ExternalLink className="mt-0.5 h-3.5 w-3.5 shrink-0 text-info" /> Source-coordinate highlighting will appear when the configured extraction provider returns page geometry. IIP does not invent coordinates when they are absent.</div>
          </GlassCard>

          <div className="space-y-4">
            <GlassCard>
              <div className="mb-3 flex items-center justify-between"><div className="text-[11px] uppercase tracking-[0.14em] text-stone">Extracted fields · {selected.fields.length}</div>{dirtyFields.length > 0 ? <Badge tone="warning">{dirtyFields.length} unsaved</Badge> : <Badge tone="success">Values synchronized</Badge>}</div>
              <div className="max-h-[560px] space-y-2.5 overflow-y-auto pr-1">
                {selected.fields.map((field) => <div key={field.name} className={`rounded-xl border px-3 py-2.5 ${values[field.name] !== field.effectiveValue ? "border-warning/40 bg-warning/5" : "border-cloud bg-white/80"}`}>
                  <div className="mb-1 flex items-center justify-between gap-2"><div className="text-[11px] text-stone">{documentContractLabel(field.name)}</div>{typeof field.confidenceScore === "number" ? <ConfidenceBadge value={field.confidenceScore} /> : <Badge tone="neutral">Confidence unavailable</Badge>}</div>
                  <input value={values[field.name] ?? ""} onChange={(event) => setValues((current) => ({ ...current, [field.name]: event.target.value }))} disabled={!allowed("correctDocumentExtraction")} className="w-full bg-transparent text-[13px] text-navy outline-none disabled:text-stone" />
                  {field.warnings.map((warning) => <div key={warning.code} className="mt-1 flex items-start gap-1 text-[10.5px] text-warning-text"><AlertTriangle className="mt-0.5 h-3 w-3 shrink-0" /> {warning.message}</div>)}
                </div>)}
                {selected.fields.length === 0 ? <div className="rounded-xl border border-dashed border-cloud p-8 text-center text-[12px] text-stone">No structured fields were returned by the configured provider.</div> : null}
              </div>
            </GlassCard>

            {selected.validationWarnings.length > 0 ? <GlassCard><div className="text-[11px] uppercase tracking-[0.14em] text-stone">Document warnings</div><div className="mt-3 space-y-2">{selected.validationWarnings.map((warning) => <div key={warning.code} className="flex items-start gap-2 rounded-xl bg-warning/5 p-3 text-[11.5px] text-warning-text"><AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" /> {warning.message}</div>)}</div></GlassCard> : null}

            <GlassCard>
              <label className="text-[11.5px] font-medium text-navy">Review reason<textarea value={auditReason} onChange={(event) => setAuditReason(event.target.value)} placeholder="Record what source evidence you checked and why any correction or approval is appropriate." className="mt-2 min-h-20 w-full rounded-xl border border-cloud bg-white px-3 py-2.5 text-[12px] text-navy placeholder:text-stone focus-ring" /></label>
              <div className="mt-4 grid gap-2 sm:grid-cols-2">
                <button type="button" disabled={busy || dirtyFields.length === 0 || auditReason.trim().length < 3 || !allowed("correctDocumentExtraction")} onClick={() => void saveCorrections()} className="inline-flex items-center justify-center gap-2 rounded-xl border border-cloud bg-white px-3 py-2.5 text-[11.5px] font-medium text-navy disabled:opacity-45"><Save className="h-4 w-4" /> Save corrections</button>
                <button type="button" disabled={busy || dirtyFields.length > 0 || auditReason.trim().length < 3 || !allowed("approveDocumentExtraction")} onClick={() => void approveExtraction()} className="inline-flex items-center justify-center gap-2 rounded-xl gradient-primary px-3 py-2.5 text-[11.5px] font-medium text-white disabled:opacity-45">{busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <CheckCircle2 className="h-4 w-4" />} Approve extraction</button>
              </div>
              {!allowed("correctDocumentExtraction") && blockedReason("correctDocumentExtraction") ? <div className="mt-3 text-[11px] text-stone">Correction unavailable: {blockedReason("correctDocumentExtraction")}</div> : null}
              {!allowed("approveDocumentExtraction") && blockedReason("approveDocumentExtraction") ? <div className="mt-1 text-[11px] text-stone">Approval unavailable: {blockedReason("approveDocumentExtraction")}</div> : null}
              <Link href="/app/work-items" className="mt-3 inline-flex items-center gap-2 text-[11.5px] font-medium text-info"><ShieldCheck className="h-3.5 w-3.5" /> Open Review & Approvals</Link>
            </GlassCard>
          </div>
        </div>
      </> : null}
    </>
  );
}
