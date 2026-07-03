"use client";

import { ChangeEvent, FormEvent, useEffect, useMemo, useState } from "react";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { SideDrawer } from "@/components/SideDrawer";
import { EmptyState, ErrorState } from "@/components/States";
import { StatusBadge } from "@/components/Badges";
import { GovernedActionDialog } from "@/components/GovernedActionDialog";
import { useRole } from "@/components/RoleContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { allowsAction, blockedActionReason, hasActionState, type ActionStateContract } from "@/lib/actionState";
import { motion } from "framer-motion";

type KnowledgeDocument = ActionStateContract & {
  knowledgeDocumentId: string;
  documentName: string;
  productLine: string;
  documentType: string;
  version: string;
  approvalStatus: string;
  indexedStatus: string;
  chunkCount: number;
  updatedAt: string;
};

type PendingKnowledgeAction = {
  action: "approve" | "archive";
  document: KnowledgeDocument;
};

export function KnowledgeBaseClient() {
  const { role, userId } = useRole();
  const [documents, setDocuments] = useState<KnowledgeDocument[]>([]);
  const [file, setFile] = useState<File | null>(null);
  const [form, setForm] = useState({
    documentName: "",
    productLine: "",
    documentType: "PolicyWording",
    version: "",
    effectiveDate: "",
    expiryDate: ""
  });
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [selectedDocumentId, setSelectedDocumentId] = useState<string | null>(null);
  const [pendingKnowledgeAction, setPendingKnowledgeAction] = useState<PendingKnowledgeAction | null>(null);
  const headers = useMemo(
    () => scopedHeaders(role, userId),
    [role, userId]
  );

  async function load() {
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/knowledge-documents`, { headers });
      if (!response.ok) {
        throw new Error(`Knowledge list failed with HTTP ${response.status}.`);
      }

      const payload: unknown = await response.json();
      if (!Array.isArray(payload) || !payload.every(hasActionState)) {
        throw new Error("Knowledge response did not include required backend action authorization state.");
      }

      setDocuments(payload as KnowledgeDocument[]);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Knowledge list failed.");
    }
  }

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function upload(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!file) {
      setError("Choose a knowledge document file.");
      return;
    }

    const body = new FormData();
    body.append("file", file);
    Object.entries(form).forEach(([key, value]) => body.append(key, value));

    setError(null);
    setStatus("Uploading knowledge document...");
    const response = await fetch(`${apiBaseUrl}/api/v1/knowledge-documents`, {
      method: "POST",
      headers,
      body
    });

    if (!response.ok) {
      setStatus(null);
      setError(`Upload failed with HTTP ${response.status}.`);
      return;
    }

    setStatus("Knowledge document uploaded as Draft.");
    setFile(null);
    await load();
  }

  async function postAction(path: string, message: string, auditReason?: string) {
    setError(null);
    setStatus(message);
    const response = await fetch(`${apiBaseUrl}${path}`, {
      method: "POST",
      headers: auditReason ? { ...headers, "X-Audit-Reason": auditReason } : headers
    });

    if (!response.ok) {
      const failure = await readFailure(response);
      setStatus(null);
      setError(failure ?? `Action failed with HTTP ${response.status}.`);
      return false;
    }

    await load();
    setStatus("Knowledge Base updated.");
    return true;
  }

  async function confirmKnowledgeAction(reason: string) {
    if (!pendingKnowledgeAction) return;

    const { action, document } = pendingKnowledgeAction;
    const success = action === "approve"
      ? await postAction(
          `/api/v1/knowledge-documents/${document.knowledgeDocumentId}/approve`,
          "Approving and indexing...",
          reason)
      : await postAction(
          `/api/v1/knowledge-documents/${document.knowledgeDocumentId}/archive`,
          "Archiving document...",
          reason);

    if (success) {
      setPendingKnowledgeAction(null);
    }
  }

  function setField(event: ChangeEvent<HTMLInputElement | HTMLSelectElement>) {
    setForm((current) => ({ ...current, [event.target.name]: event.target.value }));
  }

  const selectedDocument = documents.find((document) => document.knowledgeDocumentId === selectedDocumentId) ?? null;

  return (
    <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.4 }} className="space-y-6">
      <PageHeader
        eyebrow="Knowledge Base"
        title="Knowledge operations workbench"
        description="Upload, approve, archive, and reindex insurer knowledge used for grounded answers. Only approved and indexed documents should influence assistant output."
      />

      <div className="grid gap-6 xl:grid-cols-[360px_minmax(0,1fr)]">
        <aside className="space-y-6">
          <SectionCard eyebrow="Ingestion" title="Upload draft" description="Upload policy, claims, FAQ, and procedure documents for approval before AI use.">
            <form className="space-y-4" onSubmit={upload}>
              <Input label="Document name" name="documentName" value={form.documentName} onChange={setField} />
              <Input label="Product line" name="productLine" value={form.productLine} onChange={setField} />
              <label className="block text-sm font-medium text-muted">
                Type
                <select className="mt-1 w-full rounded-xl border border-line bg-shell px-3 py-2 text-sm text-ink outline-none focus:border-accent focus:ring-1 focus:ring-accent transition-all cursor-pointer" name="documentType" value={form.documentType} onChange={setField}>
                  <option className="bg-shell">PolicyWording</option>
                  <option className="bg-shell">ClaimsChecklist</option>
                  <option className="bg-shell">CustomerFaq</option>
                  <option className="bg-shell">ClaimsProcedure</option>
                  <option className="bg-shell">RegulatoryGuidance</option>
                  <option className="bg-shell">Other</option>
                </select>
              </label>
              <Input label="Version" name="version" value={form.version} onChange={setField} />
              <Input label="Effective date" name="effectiveDate" type="date" value={form.effectiveDate} onChange={setField} />
              <Input label="Expiry date" name="expiryDate" type="date" value={form.expiryDate} onChange={setField} />
              <input className="w-full rounded-xl border border-line bg-shell px-3 py-2 text-sm text-ink cursor-pointer file:mr-4 file:py-2 file:px-4 file:rounded-full file:border-0 file:text-xs file:font-semibold file:bg-accent/10 file:text-accent hover:file:bg-accent/20 transition-all" type="file" accept=".txt,.md,.pdf,.docx" onChange={(event) => setFile(event.target.files?.[0] ?? null)} />
              <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted">Supported uploads: TXT, MD, PDF, DOCX up to the configured platform limit.</p>
              <button className="w-full rounded-full bg-azure border border-azure/40 px-4 py-2.5 text-sm font-semibold text-white shadow-glow-cyan hover:brightness-110 transition-all">Upload draft</button>
            </form>
          </SectionCard>

          <SectionCard eyebrow="Compliance" title="Knowledge guardrails" description="Keep grounded retrieval honest and tenant-safe.">
            <div className="space-y-3 text-sm leading-6 text-muted">
              <Boundary label="Retrieval rule" value="Approved and indexed knowledge only" />
              <Boundary label="Provider honesty" value="Unavailable retrieval shows Not configured until backend health confirms readiness" />
              <Boundary label="Usage boundary" value="Grounding support only, not autonomous claim decisions" />
              <Boundary label="Workspace safety" value="Tenant and workspace scope comes from runtime context" />
            </div>
          </SectionCard>
        </aside>

        <main className="space-y-6">
          {error ? <ErrorState message={error} /> : null}
          {status ? <div className="rounded-xl border border-accent/30 bg-accent/10 px-4 py-3 text-sm text-accent shadow-glow-cyan">{status}</div> : null}

          <SectionCard
            eyebrow="Library"
            title="Knowledge documents"
            description="Click a document to inspect approval and retrieval posture in the drawer."
            actions={<button className="rounded-full border border-line bg-surface px-4 py-2 text-sm font-semibold text-muted hover:text-ink hover:border-accent/50 transition-all shadow-sm" onClick={() => postAction("/api/v1/knowledge-documents/reindex", "Reindexing approved documents...")}>Reindex approved knowledge</button>}
          >
            {documents.length === 0 ? (
              <EmptyState title="No knowledge documents uploaded" description="Upload a policy wording, claims checklist, FAQ, or procedure document to begin grounding assistant answers." />
            ) : (
              <div className="space-y-3">
                {documents.map((document) => (
                  <button
                    key={document.knowledgeDocumentId}
                    className="w-full rounded-xl border border-line bg-mist p-4 text-left transition hover:-translate-y-0.5 hover:border-accent hover:shadow-glow-cyan focus:outline-none"
                    onClick={() => setSelectedDocumentId(document.knowledgeDocumentId)}
                  >
                    <div className="flex flex-wrap items-start justify-between gap-4">
                      <div>
                        <h4 className="font-semibold text-ink">{document.documentName}</h4>
                        <p className="mt-1 text-sm text-muted font-mono">
                          {document.productLine} • {document.documentType} • v{document.version} • {document.chunkCount} chunks
                        </p>
                      </div>
                      <div className="flex flex-wrap gap-2">
                        <StatusBadge label={document.approvalStatus} />
                        <StatusBadge label={document.indexedStatus} />
                      </div>
                    </div>
                  </button>
                ))}
              </div>
            )}
          </SectionCard>

          <div className="grid gap-6 lg:grid-cols-3">
            <StatCard label="Total documents" value={`${documents.length}`} />
            <StatCard label="Approved" value={`${documents.filter((document) => document.approvalStatus.toLowerCase().includes("approved")).length}`} />
            <StatCard label="Indexed" value={`${documents.filter((document) => document.indexedStatus.toLowerCase().includes("indexed")).length}`} />
          </div>

          <SectionCard title="Usage analytics" description="Retrieval counts, answer quality, and citation feedback appear when supported by backend reporting.">
            <div className="rounded-xl border border-dashed border-line bg-mist p-5 text-sm leading-7 text-muted">
              Draft and archived sources are excluded from retrieval. If no approved source is found, policy answers must refuse or escalate rather than rely on model memory.
            </div>
          </SectionCard>
        </main>
      </div>

      <SideDrawer
        open={selectedDocument !== null}
        title={selectedDocument?.documentName ?? ""}
        subtitle="Knowledge document preview, approval posture, and retrieval readiness."
        onClose={() => setSelectedDocumentId(null)}
        footer={
          selectedDocument ? (
            <div className="flex flex-wrap gap-3">
              <button
                className={`flex-1 rounded-full px-4 py-2.5 text-sm font-semibold transition-all ${allowsAction(selectedDocument, "approveKnowledgeDocument") ? "bg-azure border border-azure/40 text-white shadow-glow-cyan hover:brightness-110" : "border border-line bg-surface text-muted opacity-70 cursor-not-allowed"}`}
                disabled={!allowsAction(selectedDocument, "approveKnowledgeDocument")}
                onClick={() => setPendingKnowledgeAction({ action: "approve", document: selectedDocument })}
              >
                Approve
              </button>
              <button
                className={`flex-1 rounded-full border px-4 py-2.5 text-sm font-semibold transition-all ${allowsAction(selectedDocument, "archiveKnowledgeDocument") ? "border-line bg-surface text-muted hover:text-ink hover:border-danger/50 hover:bg-danger/10 hover:shadow-glow-danger" : "border-line bg-surface text-muted opacity-70 cursor-not-allowed"}`}
                disabled={!allowsAction(selectedDocument, "archiveKnowledgeDocument")}
                onClick={() => setPendingKnowledgeAction({ action: "archive", document: selectedDocument })}
              >
                Archive
              </button>
            </div>
          ) : null
        }
      >
        {selectedDocument ? (
          <div className="space-y-5">
            <Boundary label="Product line" value={selectedDocument.productLine} />
            <Boundary label="Type" value={selectedDocument.documentType} />
            <Boundary label="Version" value={selectedDocument.version} />
            <Boundary label="Chunks" value={`${selectedDocument.chunkCount}`} />
            <Boundary label="Approval status" value={selectedDocument.approvalStatus} />
            <Boundary label="Indexed status" value={selectedDocument.indexedStatus} />
            <Boundary label="Updated" value={new Date(selectedDocument.updatedAt).toLocaleString()} />
            <ActionStateNotice document={selectedDocument} />
            <div className="rounded-xl border border-warning/30 bg-warning/10 shadow-glow-warning p-4 text-sm leading-7 text-warning">
              Only approved and indexed documents should be used for grounded assistant answers. Archived or draft content should not be treated as active insurer guidance.
            </div>
          </div>
        ) : null}
      </SideDrawer>
      <GovernedActionDialog
        open={pendingKnowledgeAction !== null}
        title={pendingKnowledgeAction?.action === "approve" ? "Approve knowledge for AI grounding" : "Archive knowledge document"}
        target={pendingKnowledgeAction?.document.documentName ?? ""}
        impact={
          pendingKnowledgeAction?.action === "approve"
            ? "This document becomes eligible for approved knowledge retrieval after indexing succeeds."
            : "This document is removed from active approved retrieval and should no longer guide assistant answers."
        }
        auditNotice="This lifecycle action is server-authorized and writes a knowledge audit event. The reason you enter here is required before submission and is sent with the action request for audit-supporting context."
        confirmLabel={pendingKnowledgeAction?.action === "approve" ? "Approve document" : "Archive document"}
        tone={pendingKnowledgeAction?.action === "archive" ? "danger" : "primary"}
        evidence={pendingKnowledgeAction ? (
          <div className="grid gap-3 sm:grid-cols-2">
            <Boundary label="Product line" value={pendingKnowledgeAction.document.productLine} />
            <Boundary label="Version" value={pendingKnowledgeAction.document.version} />
            <Boundary label="Current approval" value={pendingKnowledgeAction.document.approvalStatus} />
            <Boundary label="Current index" value={pendingKnowledgeAction.document.indexedStatus} />
          </div>
        ) : null}
        onClose={() => setPendingKnowledgeAction(null)}
        onConfirm={confirmKnowledgeAction}
      />
    </motion.div>
  );
}

function Input({ label, name, value, onChange, type = "text" }: { label: string; name: string; value: string; onChange: (event: ChangeEvent<HTMLInputElement>) => void; type?: string }) {
  return (
    <label className="block text-sm font-medium text-muted">
      {label}
      <input className="mt-1 w-full rounded-xl border border-line bg-shell px-3 py-2 text-sm text-ink outline-none focus:border-accent focus:ring-1 focus:ring-accent transition-all font-mono" name={name} type={type} value={value} onChange={onChange} />
    </label>
  );
}

function Boundary({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border border-line bg-mist p-4">
      <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted">{label}</p>
      <p className="mt-2 text-sm font-semibold text-ink font-mono">{value}</p>
    </div>
  );
}

function StatCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border border-line bg-mist p-5 shadow-panel flex flex-col justify-between">
      <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted">{label}</p>
      <p className="mt-4 text-2xl font-semibold text-ink font-mono">{value}</p>
    </div>
  );
}

function ActionStateNotice({ document }: { document: KnowledgeDocument }) {
  const blockedReasons = ["approveKnowledgeDocument", "archiveKnowledgeDocument"]
    .map((action) => blockedActionReason(document, action))
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

async function readFailure(response: Response) {
  try {
    const body = (await response.json()) as { message?: string };
    return body.message ?? null;
  } catch {
    return null;
  }
}
