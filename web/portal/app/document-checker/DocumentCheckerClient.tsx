"use client";

import Link from "next/link";
import { ChangeEvent, FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import { BlockedActionPanel, ConfirmationAuditNotice, GovernedButton, StatusChip, type BlockedAction } from "@/components/GovernedUi";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { SideDrawer } from "@/components/SideDrawer";
import { EmptyState, ErrorState } from "@/components/States";
import { StatusBadge } from "@/components/Badges";
import { GovernedActionDialog } from "@/components/GovernedActionDialog";
import { useRole } from "@/components/RoleContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { documentContractLabel, type DocumentActionState, type DocumentFieldVerification, type DocumentItem } from "@/lib/documentContracts";
import { motion } from "framer-motion";
import { FileText, CheckCircle, UploadCloud, AlertTriangle, Eye, ShieldCheck } from "lucide-react";

type CaseDetail = {
  caseId: string;
  customerName: string;
  policyNumber: string;
  productLine: string;
  claimType: string;
  completenessScore: number;
  audit: {
    lastActionName: string | null;
  };
};

type Checklist = {
  completenessScore: number;
  requiredReceived: number;
  requiredTotal: number;
  requiredDocuments: ChecklistItem[];
  optionalDocuments: ChecklistItem[];
  receivedDocuments: ChecklistItem[];
  missingRequiredDocuments: ChecklistItem[];
};

type ChecklistItem = {
  documentType: string;
  label: string;
  received: boolean;
};

type PendingDocumentAction =
  | { type: "approveExtraction"; document: DocumentItem }
  | { type: "reclassify"; document: DocumentItem; documentType: string }
  | { type: "correctField"; document: DocumentItem; field: DocumentFieldVerification; correctedValue: string };

const documentTypeOptions = ["ClaimForm", "PoliceAbstract", "Invoice", "MedicalReport", "PhotoEvidence", "CustomerId", "GarageEstimate", "Unknown"];

export function DocumentCheckerClient({
  initialCaseId = "",
  lockedCaseId = false
}: {
  initialCaseId?: string;
  lockedCaseId?: boolean;
}) {
  const { role, userId } = useRole();
  const [caseId, setCaseId] = useState(initialCaseId);
  const [caseDetail, setCaseDetail] = useState<CaseDetail | null>(null);
  const [documents, setDocuments] = useState<DocumentItem[]>([]);
  const [checklist, setChecklist] = useState<Checklist | null>(null);
  const [files, setFiles] = useState<FileList | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [selectedDocument, setSelectedDocument] = useState<DocumentItem | null>(null);
  const [drawerDocument, setDrawerDocument] = useState<DocumentItem | null>(null);
  const [pendingDocumentType, setPendingDocumentType] = useState("Unknown");
  const [fieldDrafts, setFieldDrafts] = useState<Record<string, string>>({});
  const [pendingDocumentAction, setPendingDocumentAction] = useState<PendingDocumentAction | null>(null);
  const [verificationReason, setVerificationReason] = useState("");
  const headers = useMemo(
    () => scopedHeaders(role, userId),
    [role, userId]
  );

  const load = useCallback(
    async (targetCaseId = caseId) => {
      if (!targetCaseId.trim()) {
        setError("Enter a case ID to open the document checker.");
        return;
      }

      setError(null);
      setStatus("Loading document checker...");

      try {
        const [caseResponse, documentsResponse, checklistResponse] = await Promise.all([
          fetch(`${apiBaseUrl}/api/v1/cases/${targetCaseId}`, { headers }),
          fetch(`${apiBaseUrl}/api/v1/cases/${targetCaseId}/documents`, { headers }),
          fetch(`${apiBaseUrl}/api/v1/cases/${targetCaseId}/document-checklist`, { headers })
        ]);

        if (!caseResponse.ok) {
          throw new Error(`Case lookup failed with HTTP ${caseResponse.status}.`);
        }

        if (!documentsResponse.ok) {
          throw new Error(`Document lookup failed with HTTP ${documentsResponse.status}.`);
        }

        if (!checklistResponse.ok) {
          throw new Error(`Checklist lookup failed with HTTP ${checklistResponse.status}.`);
        }

        setCaseDetail((await caseResponse.json()) as CaseDetail);
        const loadedDocuments = (await documentsResponse.json()) as DocumentItem[];
        setDocuments(loadedDocuments);
        setSelectedDocument((current) => loadedDocuments.find((document) => document.documentId === current?.documentId) ?? loadedDocuments[0] ?? null);
        setChecklist((await checklistResponse.json()) as Checklist);
        setStatus(null);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Document checker failed.");
        setStatus(null);
      }
    },
    [caseId, headers]
  );

  useEffect(() => {
    if (initialCaseId) {
      void load(initialCaseId);
    }
  }, [initialCaseId, load]);

  useEffect(() => {
    setPendingDocumentType(selectedDocument?.documentType ?? "Unknown");
    setFieldDrafts(Object.fromEntries((selectedDocument?.fields ?? []).map((field) => [field.name, field.effectiveValue])));
  }, [selectedDocument]);

  useEffect(() => {
    if (pendingDocumentAction && selectedDocument?.documentId !== pendingDocumentAction.document.documentId) {
      setPendingDocumentAction(null);
    }
  }, [pendingDocumentAction, selectedDocument?.documentId]);

  async function handleOpen(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await load();
  }

  async function handleUpload(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!caseDetail) {
      setError("Open a case before uploading documents.");
      return;
    }

    if (!files || files.length === 0) {
      setError("Choose at least one document to upload.");
      return;
    }

    const formData = new FormData();
    Array.from(files).forEach((file) => formData.append("files", file));
    formData.append("uploadedByUserId", userId);
    setError(null);
    setStatus("Uploading and processing documents...");

    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/cases/${caseDetail.caseId}/documents`, {
        method: "POST",
        headers,
        body: formData
      });

      if (!response.ok) {
        throw new Error(`Upload failed with HTTP ${response.status}.`);
      }

      await load(caseDetail.caseId);
      setFiles(null);
      setStatus("Documents submitted for configured document processing.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Upload failed.");
      setStatus(null);
    }
  }

  async function approveExtraction(document: DocumentItem, explicitReason?: string) {
    if (!caseDetail) {
      return;
    }

    const trimmedReason = (explicitReason ?? verificationReason).trim();
    const verificationRequired = documentRequiresHumanVerification(document);
    if (verificationRequired && !trimmedReason) {
      setStatus("A human verification reason is required before approving this extraction.");
      return;
    }

    setError(null);
    setStatus("Approving extracted fields...");

    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/cases/${caseDetail.caseId}/documents/${document.documentId}/approve-extraction`, {
        method: "POST",
        headers: {
          ...headers,
          "Content-Type": "application/json",
          ...(trimmedReason ? { "X-Audit-Reason": trimmedReason } : {})
        },
        body: JSON.stringify({ approvedByUserId: userId })
      });

      if (!response.ok) {
        throw new Error(`Approve extraction failed with HTTP ${response.status}.`);
      }

      await load(caseDetail.caseId);
      setSelectedDocument(null);
      setPendingDocumentAction(null);
      setVerificationReason("");
      setStatus("Extraction approved.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Approve extraction failed.");
      setStatus(null);
    }
  }

  async function reclassifyDocument(documentId: string, documentType: string, auditReason: string) {
    if (!caseDetail) {
      return;
    }

    setError(null);
    setStatus("Reclassifying document...");

    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/cases/${caseDetail.caseId}/documents/${documentId}/reclassify`, {
        method: "POST",
        headers: {
          ...headers,
          "Content-Type": "application/json",
          "X-Audit-Reason": auditReason
        },
        body: JSON.stringify({ documentType, updatedByUserId: userId })
      });

      if (!response.ok) {
        throw new Error(`Reclassify document failed with HTTP ${response.status}.`);
      }

      await load(caseDetail.caseId);
      setSelectedDocument(null);
      setPendingDocumentAction(null);
      setStatus("Document reclassified.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Reclassify document failed.");
      setStatus(null);
    }
  }

  async function correctExtractionField(
    documentId: string,
    fieldName: string,
    correctedValue: string,
    auditReason: string
  ) {
    if (!caseDetail) {
      return;
    }

    setError(null);
    setStatus("Saving verified field correction...");

    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/cases/${caseDetail.caseId}/documents/${documentId}/correct-field`, {
        method: "POST",
        headers: {
          ...headers,
          "Content-Type": "application/json",
          "X-Audit-Reason": auditReason
        },
        body: JSON.stringify({ fieldName, correctedValue })
      });

      if (!response.ok) {
        throw new Error(`Field correction failed with HTTP ${response.status}.`);
      }

      await load(caseDetail.caseId);
      setSelectedDocument(null);
      setPendingDocumentAction(null);
      setStatus("Field correction saved with audit context.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Field correction failed.");
      setStatus(null);
    }
  }

  async function openDocumentPreview(document: DocumentItem) {
    setError(null);

    try {
      const response = await fetch(`${apiBaseUrl}${document.previewUrl}`, { headers });
      if (!response.ok) {
        throw new Error(`Document preview failed with HTTP ${response.status}.`);
      }

      const objectUrl = URL.createObjectURL(await response.blob());
      const previewLink = window.document.createElement("a");
      previewLink.href = objectUrl;
      previewLink.target = "_blank";
      previewLink.rel = "noopener noreferrer";
      previewLink.click();
      window.setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Document preview failed.");
    }
  }

  function handleFilesChanged(event: ChangeEvent<HTMLInputElement>) {
    setFiles(event.target.files);
  }

  return (
    <>
      <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.4 }} className="space-y-6">
        <PageHeader
          eyebrow="Document Checker"
          title="Claim document workspace"
          description="Classification, extraction approval, completeness checks, and upload flow backed by the configured document provider."
          actions={
            caseDetail ? (
              <Link className="rounded-full bg-surface border border-line hover:border-accent hover:text-accent transition-all px-5 py-2.5 text-sm font-semibold text-ink shadow-glow-cyan" href={`/cases/${caseDetail.caseId}`}>
                Open full case
              </Link>
            ) : null
          }
        />

        <div className="grid gap-6 xl:grid-cols-[360px_minmax(0,1fr)]">
          <aside className="space-y-6">
            <SectionCard eyebrow="Selection" title="Open case" description="Use a case ID to review uploaded evidence and the current checklist.">
              <form className="space-y-4 mt-2" onSubmit={handleOpen}>
                <input
                  id="caseId"
                  className="w-full rounded-xl border border-line bg-shell px-4 py-3 text-sm text-ink outline-none focus:border-accent focus:ring-1 focus:ring-accent transition-all font-mono"
                  value={caseId}
                  disabled={lockedCaseId}
                  onChange={(event) => setCaseId(event.target.value)}
                  placeholder="Paste claim case ID"
                />
                {!lockedCaseId ? (
                  <button className="w-full rounded-full bg-azure border border-azure/40 px-5 py-2.5 text-sm font-semibold text-white shadow-glow-cyan transition-all hover:brightness-110">Open case</button>
                ) : null}
              </form>
            </SectionCard>

            <SectionCard eyebrow="Input" title="Upload documents" description="Documents stay tenant scoped and are processed by the configured document pipeline.">
              <form className="space-y-4 mt-2" onSubmit={handleUpload}>
                <div className="relative group">
                  <input className="absolute inset-0 w-full h-full opacity-0 cursor-pointer" type="file" multiple accept=".pdf,.png,.jpg,.jpeg,.txt" onChange={handleFilesChanged} />
                  <div className="w-full rounded-xl border-2 border-dashed border-line bg-surface/50 px-4 py-8 flex flex-col items-center justify-center text-center group-hover:border-accent/50 transition-colors">
                    <UploadCloud className="w-8 h-8 text-muted mb-2 group-hover:text-accent transition-colors" />
                    <p className="text-sm font-semibold text-ink">{files?.length ? `${files.length} file(s) selected` : "Drag and drop or click to upload"}</p>
                    <p className="mt-1 text-xs text-muted">PDF, PNG, JPG up to 10MB</p>
                  </div>
                </div>
                <button className="w-full rounded-full bg-[#6f4cff] px-5 py-2.5 text-sm font-semibold text-white shadow-glow-cyan hover:brightness-110 transition-all">Upload documents</button>
              </form>
            </SectionCard>

            {caseDetail ? (
              <SectionCard eyebrow="Context" title="Case overview" description="Claim-level context used for document validation.">
                <dl className="space-y-3 text-sm">
                  <Detail label="Customer" value={caseDetail.customerName} />
                  <Detail label="Policy" value={caseDetail.policyNumber} />
                  <Detail label="Product" value={caseDetail.productLine} />
                  <Detail label="Completeness" value={`${caseDetail.completenessScore}%`} />
                  <Detail label="Latest audit" value={caseDetail.audit.lastActionName ?? "None"} />
                </dl>
              </SectionCard>
            ) : null}
          </aside>

          <main className="space-y-6">
            {error ? <ErrorState message={error} /> : null}
            {status ? <div className="rounded-xl border border-line bg-surface/50 px-4 py-3 text-sm text-accent font-mono shadow-glow-cyan">{status}</div> : null}

            {checklist ? (
              <SectionCard eyebrow="Requirements" title="Completeness checklist" description={`Required received: ${checklist.requiredReceived} of ${checklist.requiredTotal}`}>
                <div className="grid gap-4 md:grid-cols-4">
                  <ChecklistStat label="Completeness" value={`${checklist.completenessScore}%`} />
                  <ChecklistStat label="Required received" value={`${checklist.requiredReceived}`} />
                  <ChecklistStat label="Required total" value={`${checklist.requiredTotal}`} />
                  <ChecklistStat label="Missing required" value={`${checklist.missingRequiredDocuments.length}`} highlight={checklist.missingRequiredDocuments.length > 0} />
                </div>
                <div className="mt-6 grid gap-4 md:grid-cols-3">
                  <ChecklistGroup title="Required" items={checklist.requiredDocuments} />
                  <ChecklistGroup title="Optional" items={checklist.optionalDocuments} />
                  <ChecklistGroup title="Missing" items={checklist.missingRequiredDocuments} empty="No required documents missing." />
                </div>
              </SectionCard>
            ) : null}

            <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_380px]">
            <div className="rounded-2xl bg-[#E2E8F0] p-1 shadow-inner min-h-[400px]">
              <div className="rounded-xl bg-shell h-full overflow-hidden flex flex-col">
                <div className="px-6 py-4 border-b border-line bg-surface/50 backdrop-blur flex justify-between items-center">
                  <div>
                    <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-accent">Evidence</p>
                    <h3 className="font-bold text-ink mt-1">Documents received</h3>
                  </div>
                  <span className="text-xs text-muted">Click to inspect</span>
                </div>
                <div className="p-6 overflow-auto">
                  {documents.length === 0 ? (
                    <EmptyState title="No documents received" description="Upload or ingest a file to populate the document workbench for this claim." />
                  ) : (
                    <div className="space-y-3">
                      {documents.map((document) => (
                        <button key={document.documentId} className="w-full rounded-xl border border-line bg-mist p-4 text-left transition hover:border-accent/50 group" onClick={() => setSelectedDocument(document)}>
                          <div className="flex flex-wrap items-start justify-between gap-3">
                            <div className="flex items-center gap-3">
                              <FileText className="w-5 h-5 text-accent group-hover:text-azure transition-colors" />
                              <div>
                                <p className="font-semibold text-ink group-hover:text-accent transition-colors">{document.originalFileName}</p>
                                <p className="mt-1 text-sm text-muted">
                                  {document.contentType} • {formatBytes(document.fileSizeBytes)}
                                </p>
                                <div className="mt-2 flex flex-wrap gap-2">
                                  <StatusChip label={document.extractionApproved ? "Verified" : documentRequiresHumanVerification(document) ? "Needs Human Verification" : "Ready for Approval"} tone={document.extractionApproved ? "success" : documentRequiresHumanVerification(document) ? "warning" : "info"} />
                                  <StatusChip label={`${document.allowedActions?.length ?? 0} actions`} tone="neutral" />
                                </div>
                              </div>
                            </div>
                            <div className="flex flex-wrap gap-2">
                              <StatusBadge label={document.documentType} />
                              <span className={`font-mono text-[10px] px-2 py-1 rounded border ${document.confidenceScore < 0.8 ? 'bg-danger/10 border-danger/20 text-danger shadow-glow-danger' : 'bg-accent/10 border-accent/20 text-accent shadow-glow-cyan'}`}>
                                {Math.round(document.confidenceScore * 100)}% conf
                              </span>
                              <StatusBadge label={document.processingStatus} />
                            </div>
                          </div>
                        </button>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            </div>
            <SectionCard
              eyebrow="Verification"
              title="Extraction review"
              description="Low-confidence or warned extraction requires human verification before approval."
              actions={selectedDocument ? <StatusChip label={selectedDocument.extractionApproved ? "Approved" : documentRequiresHumanVerification(selectedDocument) ? "Verification required" : "Ready"} tone={selectedDocument.extractionApproved ? "success" : documentRequiresHumanVerification(selectedDocument) ? "warning" : "info"} /> : null}
            >
              {selectedDocument ? (
                <div className="space-y-4">
                  <div className="rounded-xl border border-line bg-mist p-4">
                    <p className="font-semibold text-ink">{selectedDocument.originalFileName}</p>
                    <p className="mt-1 text-sm text-muted">{selectedDocument.documentType} / {selectedDocument.processingStatus}</p>
                    <div className="mt-3 grid gap-2 sm:grid-cols-2 lg:grid-cols-1">
                      <Detail label="Confidence" value={`${Math.round(selectedDocument.confidenceScore * 100)}%`} highlight={selectedDocument.confidenceScore < 0.8} />
                      <Detail label="Approved" value={selectedDocument.extractionApproved ? "Yes" : "No"} />
                    </div>
                  </div>
                  <BlockedActionPanel actions={toBlockedActions(selectedDocument.blockedActions)} title="Extraction blocked actions" />
                  <ConfirmationAuditNotice
                    action="Approve extraction"
                    affectedObject={selectedDocument.originalFileName}
                    impact="Approving extraction accepts the current extracted fields into the case evidence workflow. It does not approve, deny or settle the claim."
                  >
                    <label className="block text-sm font-medium text-muted">
                      Human verification reason
                      <textarea
                        className="mt-2 min-h-28 w-full rounded-xl border border-line bg-shell px-3 py-2 text-sm text-ink outline-none transition-colors focus:border-accent focus:ring-1 focus:ring-accent"
                        value={verificationReason}
                        onChange={(event) => setVerificationReason(event.target.value)}
                        placeholder="Describe what was checked before approval."
                      />
                    </label>
                    <div className="mt-4">
                      <GovernedButton
                        variant="authority"
                        icon={<ShieldCheck className="h-4 w-4" />}
                        disabled={selectedDocument.extractionApproved}
                        disabledReason={
                          selectedDocument.extractionApproved
                            ? "Extraction is already approved."
                            : documentRequiresHumanVerification(selectedDocument) && !verificationReason.trim()
                              ? "Human verification reason is required."
                              : undefined
                        }
                        onClick={() => approveExtraction(selectedDocument)}
                      >
                        Approve extraction
                      </GovernedButton>
                    </div>
                  </ConfirmationAuditNotice>
                </div>
              ) : (
                <EmptyState title="No document selected" description="Select a document to review extraction and action state." />
              )}
            </SectionCard>
            </div>
          </main>
        </div>
      </motion.div>

      <SideDrawer
        open={drawerDocument !== null}
        title={drawerDocument?.originalFileName ?? ""}
        subtitle="Document classification, extraction, completeness impact, and provider context."
        onClose={() => {
          setDrawerDocument(null);
          setPendingDocumentAction(null);
        }}
        footer={
          drawerDocument ? (
            <div className="flex flex-wrap gap-2">
              <button
                className="inline-flex items-center gap-2 rounded-full border border-line bg-surface px-5 py-2.5 text-sm font-semibold text-ink transition-all hover:border-accent hover:text-accent"
                onClick={() => void openDocumentPreview(drawerDocument)}
              >
                <Eye className="h-4 w-4" />
                Open preview
              </button>
              <select
                className="rounded-full border border-line bg-surface px-4 py-2.5 text-sm font-semibold text-ink outline-none transition-all disabled:cursor-not-allowed disabled:opacity-50"
                value={pendingDocumentType}
                disabled={!documentAllows(drawerDocument, "reclassifyDocument")}
                onChange={(event) => setPendingDocumentType(event.target.value)}
                aria-label="Document type"
              >
                {documentTypeOptions.map((documentType) => (
                  <option key={documentType} className="bg-shell" value={documentType}>
                    {documentType}
                  </option>
                ))}
              </select>
              <button
                className="rounded-full border border-line bg-surface px-5 py-2.5 text-sm font-semibold text-ink transition-all hover:border-accent hover:text-accent disabled:cursor-not-allowed disabled:opacity-50 disabled:hover:border-line disabled:hover:text-ink"
                disabled={!documentAllows(drawerDocument, "reclassifyDocument") || pendingDocumentType === drawerDocument.documentType}
                onClick={() => setPendingDocumentAction({ type: "reclassify", document: drawerDocument, documentType: pendingDocumentType })}
              >
                Reclassify
              </button>
              <button
                className={`rounded-full px-5 py-2.5 text-sm font-semibold transition-all ${drawerDocument.extractionApproved ? 'bg-success/20 text-success border border-success/30 cursor-not-allowed' : !documentAllows(drawerDocument, "approveDocumentExtraction") ? 'bg-surface text-muted border border-line cursor-not-allowed opacity-70' : 'bg-azure border border-azure/40 text-white shadow-glow-cyan hover:brightness-110'}`}
                disabled={!documentAllows(drawerDocument, "approveDocumentExtraction")}
                onClick={() => setPendingDocumentAction({ type: "approveExtraction", document: drawerDocument })}
              >
                {drawerDocument.extractionApproved ? "Extraction approved" : "Approve extraction"}
              </button>
              {caseDetail ? (
                <Link className="rounded-full border border-line bg-surface px-5 py-2.5 text-sm font-semibold text-ink hover:text-accent hover:border-accent transition-all" href={`/cases/${caseDetail.caseId}`}>
                  Open full case
                </Link>
              ) : null}
            </div>
          ) : null
        }
      >
        {drawerDocument ? (
          <div className="space-y-6">
            <div className="grid gap-3 sm:grid-cols-2">
              <Detail label="Type" value={drawerDocument.documentType} />
              <Detail label="Processing" value={drawerDocument.processingStatus} />
              <Detail label="Confidence" value={`${Math.round(drawerDocument.confidenceScore * 100)}%`} highlight={drawerDocument.confidenceScore < 0.8} />
              <Detail label="Uploaded" value={new Date(drawerDocument.uploadedAt).toLocaleString()} />
            </div>
            <ActionStateNotice document={drawerDocument} />
            <div>
              <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted">Extracted fields</p>
              <div className="mt-3 space-y-3 rounded-xl border border-line bg-mist p-4 text-sm">
                {drawerDocument.fields.length === 0 ? (
                  <p className="text-muted">No extracted fields returned yet.</p>
                ) : (
                  drawerDocument.fields.map((field) => {
                    const draftValue = fieldDrafts[field.name] ?? field.effectiveValue;
                    const canCorrect = documentAllows(drawerDocument, "correctDocumentExtraction");
                    return (
                    <div key={field.name} className="rounded-lg border border-line/70 bg-surface/70 p-3">
                      <div className="flex flex-wrap items-start justify-between gap-2">
                        <div>
                          <p className="font-semibold text-ink">{field.name}</p>
                          <p className="mt-1 text-xs text-muted">
                            Original: <span className="font-mono">{field.extractedValue}</span>
                          </p>
                        </div>
                        <div className="flex flex-wrap gap-2">
                          <StatusBadge label={documentContractLabel(field.verificationStatus)} />
                          <StatusBadge label={field.sensitivity} />
                          <span className="rounded border border-line px-2 py-1 font-mono text-[10px] text-muted">
                            {field.confidenceScore == null ? "No confidence" : `${Math.round(field.confidenceScore * 100)}%`}
                          </span>
                        </div>
                      </div>
                      <div className="mt-3 flex flex-col gap-2 sm:flex-row">
                        <input
                          className="min-w-0 flex-1 rounded-lg border border-line bg-shell px-3 py-2 font-mono text-sm text-ink outline-none focus:border-accent disabled:cursor-not-allowed disabled:opacity-60"
                          value={draftValue}
                          disabled={!canCorrect}
                          aria-label={`Verified value for ${field.name}`}
                          onChange={(event) => setFieldDrafts((current) => ({ ...current, [field.name]: event.target.value }))}
                        />
                        <button
                          className="rounded-lg border border-line bg-surface px-4 py-2 text-xs font-semibold text-ink transition-colors hover:border-accent hover:text-accent disabled:cursor-not-allowed disabled:opacity-50"
                          disabled={!canCorrect || !draftValue.trim() || draftValue.trim() === field.effectiveValue}
                          onClick={() => setPendingDocumentAction({
                            type: "correctField",
                            document: drawerDocument,
                            field,
                            correctedValue: draftValue.trim()
                          })}
                        >
                          Save correction
                        </button>
                      </div>
                      {field.correctedAt ? (
                        <p className="mt-2 text-xs text-muted">
                          Corrected {new Date(field.correctedAt).toLocaleString()} • reference {field.correctionReference ?? "Unavailable"}
                        </p>
                      ) : null}
                      {field.warnings.map((warning) => (
                        <p key={warning.code} className="mt-2 text-xs text-warning">{warning.message}</p>
                      ))}
                    </div>
                    );
                  })
                )}
              </div>
            </div>
            <div>
              <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted">Validation warnings</p>
              {drawerDocument.validationWarnings.length === 0 ? (
                <p className="mt-3 rounded-xl border border-success/30 bg-success/10 p-4 text-sm text-success shadow-glow-success flex items-center gap-2"><CheckCircle className="w-4 h-4" /> No validation warnings.</p>
              ) : (
                <ul className="mt-3 space-y-2">
                  {drawerDocument.validationWarnings.map((warning) => (
                    <li key={warning.code} className="rounded-xl border border-danger/30 bg-danger/10 p-4 text-sm text-danger shadow-glow-danger flex items-start gap-3">
                      <AlertTriangle className="w-4 h-4 mt-0.5 shrink-0" />
                      <div>
                        <span className="font-bold">{warning.code}</span>
                        <p className="mt-1 text-danger/80">{warning.message}</p>
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </div>
            <div className="rounded-xl border border-dashed border-line p-4 text-sm leading-6 text-muted">
              Extraction is available only when the configured document provider is healthy. Deployed runtimes must use Azure Document Intelligence.
            </div>
            <BlockedActionPanel actions={toBlockedActions(drawerDocument.blockedActions)} title="Extraction blocked actions" />
          </div>
        ) : null}
      </SideDrawer>

      {pendingDocumentAction ? (
        <GovernedActionDialog
          open={pendingDocumentAction !== null}
          title={documentActionTitle(pendingDocumentAction)}
          target={`${pendingDocumentAction.document.originalFileName} (${pendingDocumentAction.document.documentId})`}
          impact={documentActionImpact(pendingDocumentAction)}
          auditNotice="This is a human-governed document action. The reason is required as audit context and the backend will still enforce tenant scope, document state, and action permissions."
          confirmLabel={documentActionConfirmLabel(pendingDocumentAction)}
          evidence={<DocumentActionEvidence action={pendingDocumentAction} />}
          onClose={() => setPendingDocumentAction(null)}
          onConfirm={async (auditReason) => {
            if (pendingDocumentAction.type === "approveExtraction") {
              await approveExtraction(pendingDocumentAction.document, auditReason);
              return;
            }

            if (pendingDocumentAction.type === "correctField") {
              await correctExtractionField(
                pendingDocumentAction.document.documentId,
                pendingDocumentAction.field.name,
                pendingDocumentAction.correctedValue,
                auditReason
              );
              return;
            }

            await reclassifyDocument(
              pendingDocumentAction.document.documentId,
              pendingDocumentAction.documentType,
              auditReason
            );
          }}
        />
      ) : null}
    </>
  );
}

function ChecklistGroup({ title, items, empty = "None" }: { title: string; items: ChecklistItem[]; empty?: string }) {
  return (
    <div className="rounded-xl border border-line bg-mist p-4">
      <h4 className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted mb-4">{title}</h4>
      {items.length === 0 ? (
        <p className="text-sm text-muted/70">{empty}</p>
      ) : (
        <ul className="space-y-2">
          {items.map((item) => (
            <li key={`${title}-${item.documentType}`} className="flex items-center justify-between rounded-lg bg-surface/80 px-3 py-2 text-sm border border-line/50">
              <span className="font-medium text-ink">{item.label}</span>
              <span className={item.received ? "text-success font-mono text-[10px] uppercase tracking-wider" : "text-danger font-mono text-[10px] uppercase tracking-wider shadow-glow-danger"}>{item.received ? "Received" : "Missing"}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function ChecklistStat({ label, value, highlight = false }: { label: string; value: string; highlight?: boolean }) {
  return (
    <div className={`rounded-xl border p-4 shadow-panel flex flex-col justify-between ${highlight ? 'border-danger/30 bg-danger/5 shadow-glow-danger' : 'border-line bg-mist'}`}>
      <p className={`text-[10px] font-bold uppercase tracking-[0.2em] ${highlight ? 'text-danger' : 'text-muted'}`}>{label}</p>
      <p className={`mt-3 text-lg font-bold font-mono ${highlight ? 'text-danger' : 'text-ink'}`}>{value}</p>
    </div>
  );
}

function Detail({ label, value, highlight = false }: { label: string; value: string; highlight?: boolean }) {
  return (
    <div className="rounded-xl border border-line bg-surface/50 p-3">
      <dt className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted">{label}</dt>
      <dd className={`mt-2 break-words font-mono font-medium ${highlight ? 'text-danger' : 'text-ink'}`}>{value}</dd>
    </div>
  );
}

function DocumentActionEvidence({ action }: { action: PendingDocumentAction }) {
  const document = action.document;
  const lowConfidenceFields = document.fields
    .filter((field) => field.confidenceScore != null && field.confidenceScore < 0.8)
    .sort((left, right) => (left.confidenceScore ?? 1) - (right.confidenceScore ?? 1));
  const warnings = document.validationWarnings ?? [];
  const isLowConfidence = document.confidenceScore < 0.8 || lowConfidenceFields.length > 0;

  return (
    <div className={`rounded-xl border p-4 text-sm leading-6 ${isLowConfidence ? "border-warning/30 bg-warning/10 text-warning" : "border-line bg-mist text-muted"}`}>
      <p className="text-[10px] font-bold uppercase tracking-[0.2em]">Evidence context</p>
      <dl className="mt-3 grid gap-2 sm:grid-cols-2">
        <EvidenceItem label="Current type" value={document.documentType} />
        {action.type === "reclassify" ? <EvidenceItem label="Requested type" value={action.documentType} /> : null}
        {action.type === "correctField" ? <EvidenceItem label="Field" value={action.field.name} /> : null}
        {action.type === "correctField" ? <EvidenceItem label="Current value" value={action.field.effectiveValue} /> : null}
        {action.type === "correctField" ? <EvidenceItem label="Corrected value" value={action.correctedValue} /> : null}
        <EvidenceItem label="Processing" value={document.processingStatus} />
        <EvidenceItem label="Overall confidence" value={`${Math.round(document.confidenceScore * 100)}%`} />
        <EvidenceItem label="Extracted fields" value={`${document.fields.length}`} />
        <EvidenceItem label="Validation warnings" value={`${warnings.length}`} />
      </dl>
      {lowConfidenceFields.length > 0 ? (
        <div className="mt-3 rounded-lg border border-warning/30 bg-surface/70 p-3">
          <p className="font-semibold">Low-confidence fields need human review before this action is submitted.</p>
          <ul className="mt-2 space-y-1">
            {lowConfidenceFields.slice(0, 5).map((field) => (
              <li key={field.name} className="flex justify-between gap-4">
                <span className="break-words">{field.name}</span>
                <span className="font-mono">{Math.round((field.confidenceScore ?? 0) * 100)}%</span>
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </div>
  );
}

function EvidenceItem({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-[10px] font-bold uppercase tracking-[0.18em] opacity-70">{label}</dt>
      <dd className="mt-1 break-words font-mono font-semibold text-ink">{value}</dd>
    </div>
  );
}

function documentActionTitle(action: PendingDocumentAction) {
  if (action.type === "approveExtraction") {
    return "Approve extracted document fields";
  }

  if (action.type === "correctField") {
    return `Correct extracted field: ${action.field.name}`;
  }

  return `Reclassify document to ${action.documentType}`;
}

function documentActionImpact(action: PendingDocumentAction) {
  if (action.type === "approveExtraction") {
    return "Marks the extracted document fields as human approved and prevents further extraction edits through the current document action rules.";
  }

  if (action.type === "correctField") {
    return "Preserves the provider extraction, stores the verified value as a human correction, and writes an audit event without copying the corrected value into audit logs.";
  }

  return "Updates the document classification, may recalculate claim completeness, and affects the checklist evidence used by claims workflows.";
}

function documentActionConfirmLabel(action: PendingDocumentAction) {
  if (action.type === "approveExtraction") {
    return "Approve extraction";
  }

  return action.type === "correctField" ? "Save correction" : "Confirm reclassification";
}

function ActionStateNotice({ document }: { document: DocumentItem }) {
  const blockedReasons = ["reclassifyDocument", "approveDocumentExtraction", "correctDocumentExtraction"]
    .map((action) => documentBlockedReason(document, action))
    .filter((reason): reason is string => Boolean(reason));

  if (blockedReasons.length === 0) {
    return null;
  }

  return (
    <div className="rounded-xl border border-warning/30 bg-warning/10 p-4 text-sm leading-6 text-warning shadow-glow-warning">
      {blockedReasons.map((reason) => (
        <p key={reason}>{reason}</p>
      ))}
    </div>
  );
}

function documentAllows(document: DocumentItem, action: string) {
  return document.allowedActions.some((item) => item.action === action);
}

function toBlockedActions(actions: DocumentActionState[] | undefined): BlockedAction[] {
  return (actions ?? []).map((action) => ({
    action: action.action,
    reasonCode: action.requiredPermission,
    message: action.reason ?? action.label
  }));
}

function documentBlockedReason(document: DocumentItem, action: string) {
  return document.blockedActions.find((item) => item.action === action)?.reason ?? null;
}

function formatBytes(bytes: number) {
  if (bytes < 1024) {
    return `${bytes} B`;
  }

  if (bytes < 1024 * 1024) {
    return `${Math.round(bytes / 1024)} KB`;
  }

  return `${Math.round((bytes / (1024 * 1024)) * 10) / 10} MB`;
}

function documentRequiresHumanVerification(document: DocumentItem) {
  return document.confidenceScore < 0.8 || document.validationWarnings.length > 0 || (document.blockedActions?.length ?? 0) > 0;
}
