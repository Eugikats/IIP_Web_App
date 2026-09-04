"use client";
import { Link } from "@/components/iip/NextRouterCompat";
import { useMemo, useState } from "react";
import { GlassCard, SectionHeader, Badge, StatusBadge, ConfidenceBadge } from "@/components/iip/Primitives";
import { ExternalLink, FileText, Search, Upload, X } from "lucide-react";
import { useDocuments } from "@/lib/lovable-parity/data/documents";
import { useClaims } from "@/lib/lovable-parity/data/claims";
import { extractionHref } from "@/lib/workItemLinks";
import { DocumentUploadWidget } from "@/components/iip/DocumentUploadWidget";
import { useUI } from "@/context/UIStateContext";
import { canPreviewPermission, hasPermission } from "@/lib/roles";
import { canUseLocalTestDoubles } from "@/lib/config";
import { useRuntimeContext } from "@/lib/runtimeContext";

// WO-18: documents attach to a claim in this domain — there is no unattached-document concept
// (A1) — so upload here means picking the claim first, then reusing the exact same
// DocumentUploadWidget ClaimReview.tsx already uses (A2: never a second uploader).
export default function Documents() {
  const { documents, loading, error, reload } = useDocuments();
  const { claims, loading: claimsLoading } = useClaims();
  const { roleDef } = useUI();
  const { runtimeContext, parityMode } = useRuntimeContext();
  const canUploadDocuments = canUseLocalTestDoubles || parityMode
    ? canPreviewPermission(roleDef.key, "UploadDocument")
    : hasPermission(runtimeContext?.permissions ?? [], "UploadDocument");

  const [panelOpen, setPanelOpen] = useState(false);
  const [claimQuery, setClaimQuery] = useState("");
  const [selectedClaimId, setSelectedClaimId] = useState<string | null>(null);

  const matchingClaims = useMemo(() => {
    const q = claimQuery.trim().toLowerCase();
    if (!q) return claims.slice(0, 8);
    return claims
      .filter((c) => c.customer.toLowerCase().includes(q) || c.policy.toLowerCase().includes(q) || c.id.toLowerCase().includes(q))
      .slice(0, 8);
  }, [claims, claimQuery]);

  const selectedClaim = claims.find((c) => c.id === selectedClaimId) ?? null;

  function closePanel() {
    setPanelOpen(false);
    setSelectedClaimId(null);
    setClaimQuery("");
  }

  return (
    <>
      <SectionHeader
        eyebrow="Documents"
        title="Document library"
        sub="All documents ingested across claims, customers, and channels. AI extraction is human-reviewed — low-confidence fields are treated as incomplete."
        action={
          canUploadDocuments ? (
            <button
              type="button"
              onClick={() => setPanelOpen((v) => !v)}
              className="inline-flex items-center gap-2 rounded-2xl gradient-primary px-4 py-2.5 text-[13px] font-semibold text-white shadow-l2"
            >
              <Upload className="h-4 w-4" /> Upload document
            </button>
          ) : undefined
        }
      />

      {panelOpen && canUploadDocuments && (
        <GlassCard className="mb-4">
          <div className="flex items-center justify-between mb-3">
            <div className="text-[14px] font-semibold text-ink">Upload to a claim</div>
            <button type="button" onClick={closePanel} aria-label="Close upload panel" className="rounded-lg p-1.5 text-muted hover:bg-line/40 hover:text-ink"><X className="h-4 w-4" /></button>
          </div>
          <div className="text-[12.5px] text-ink-soft mb-3">Documents attach to a claim. Find the claim first, then upload.</div>

          {!selectedClaim ? (
            <>
              <div className="glass-subtle p-3 mb-3 flex items-center gap-2">
                <Search className="h-4 w-4 text-muted" />
                <input
                  value={claimQuery}
                  onChange={(e) => setClaimQuery(e.target.value)}
                  placeholder="Search by customer name, policy number, or claim ID…"
                  className="flex-1 bg-transparent outline-none text-[13.5px] placeholder:text-muted text-ink"
                  autoFocus
                />
              </div>
              {claimsLoading ? (
                <div className="p-4 text-center text-[13px] text-muted">Loading claims…</div>
              ) : matchingClaims.length === 0 ? (
                <div className="p-4 text-center text-[13px] text-muted">No matching claims.</div>
              ) : (
                <div className="space-y-1.5 max-h-72 overflow-y-auto">
                  {matchingClaims.map((c) => (
                    <button
                      key={c.id}
                      type="button"
                      onClick={() => setSelectedClaimId(c.id)}
                      className="w-full flex items-center justify-between gap-3 rounded-xl border border-line bg-white/70 px-3 py-2.5 text-left hover:bg-white"
                    >
                      <div className="min-w-0">
                        <div className="text-[13px] font-medium text-ink truncate">{c.customer} · {c.policy}</div>
                        <div className="text-[10.5px] text-muted truncate">{c.id.slice(0, 8)} · {c.productLine} · {c.claimType}</div>
                      </div>
                      <StatusBadge status={c.status} />
                    </button>
                  ))}
                </div>
              )}
            </>
          ) : (
            <div>
              <div className="flex items-center justify-between gap-3 rounded-xl border border-info/30 bg-info/6 px-3 py-2.5 mb-3">
                <div className="min-w-0">
                  <div className="text-[13px] font-medium text-ink truncate">{selectedClaim.customer} · {selectedClaim.policy}</div>
                  <div className="text-[10.5px] text-muted truncate">{selectedClaim.id.slice(0, 8)} · {selectedClaim.productLine}</div>
                </div>
                <button type="button" onClick={() => setSelectedClaimId(null)} className="text-[11.5px] text-info shrink-0">Change claim</button>
              </div>
              <DocumentUploadWidget
                caseId={selectedClaim.id}
                role={roleDef.key}
                onUploaded={async () => {
                  await reload();
                }}
              />
              <Link to={`/app/claims/${selectedClaim.id}`} className="mt-3 inline-flex items-center gap-1 text-[11.5px] text-info"><ExternalLink className="h-3 w-3" /> Open this claim</Link>
            </div>
          )}
        </GlassCard>
      )}

      {loading ? (
        <GlassCard><div className="p-6 text-center text-[13px] text-muted">Loading document library…</div></GlassCard>
      ) : documents.length === 0 ? (
        <GlassCard>
          <div className="p-10 text-center flex flex-col items-center gap-2">
            <FileText className="h-6 w-6 text-muted" />
            <div className="text-ink font-medium">No documents yet</div>
            <div className="text-[12.5px] text-muted max-w-md">{error ? error : "Only documents uploaded to a tenant-scoped claim appear here."}</div>
          </div>
        </GlassCard>
      ) : (
        <GlassCard className="!p-0 overflow-hidden">
          <table className="w-full text-[13px]">
            <thead className="bg-white/60">
              <tr className="text-left text-[11px] uppercase tracking-[0.14em] text-muted">
                <th className="p-3">Document</th><th className="p-3">Type</th><th className="p-3">Claim</th><th className="p-3">Status</th><th className="p-3">Confidence</th><th className="p-3">Uploaded</th><th className="p-3"></th>
              </tr>
            </thead>
            <tbody>
              {documents.map((d) => {
                const reviewHref = extractionHref(d.claim, d.id);
                return (
                <tr key={d.id} className="border-t border-line/70 hover:bg-white/50">
                  <td className="p-3"><div className="flex items-center gap-2 text-ink"><FileText className="h-4 w-4 text-info" />{d.name}</div><div className="text-[10.5px] text-muted">{d.id}</div></td>
                  <td className="p-3 text-ink-soft">{d.type}</td>
                  <td className="p-3 text-info">{d.claim}</td>
                  <td className="p-3"><StatusBadge status={d.status} /></td>
                  <td className="p-3">{d.confidence > 0 ? <ConfidenceBadge value={d.confidence} /> : <Badge tone="neutral">—</Badge>}</td>
                  <td className="p-3 text-muted">{d.uploaded}</td>
                  <td className="p-3">
                    {reviewHref ? <Link to={reviewHref} className="inline-flex items-center gap-1 rounded-lg border border-line bg-white px-2 py-1 text-[11.5px] font-medium text-ink focus-ring"><ExternalLink className="h-3 w-3" /> Review</Link> : <span className="text-[11px] text-muted">Review unavailable</span>}
                  </td>
                </tr>
              );})}
            </tbody>
          </table>
        </GlassCard>
      )}
      <div className="mt-3 text-[11.5px] text-muted">Allowed file types: PDF, JPEG, PNG · Current upload boundary: 10MB per document.</div>
    </>
  );
}
