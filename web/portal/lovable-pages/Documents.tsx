"use client";
import { Link } from "@/components/iip/NextRouterCompat";
import { useMemo, useState } from "react";
import { GlassCard, SectionHeader, StatusBadge, ConfidenceBadge } from "@/components/iip/Primitives";
import { Button, IconButton } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { Table, TableHead, TableBody, TableRow, TableHeaderCell, TableCell } from "@/components/ui/Table";
import { LoadingSkeleton, ErrorStateSurface, EmptyStateSurface } from "@/components/ui/StateSurfaces";
import { ExternalLink, FileText, Search, Upload, X } from "lucide-react";
import { useDocuments } from "@/lib/lovable-parity/data/documents";
import { useClaims } from "@/lib/lovable-parity/data/claims";
import { extractionHref } from "@/lib/workItemLinks";
import { DocumentUploadWidget } from "@/components/iip/DocumentUploadWidget";
import { useUI } from "@/context/UIStateContext";
import { canPreviewPermission, hasPermission } from "@/lib/roles";
import { canUseLocalTestDoubles } from "@/lib/config";
import { useRuntimeContext } from "@/lib/runtimeContext";

// WO-18: documents attach to a claim in this domain, there is no unattached-document concept
// (A1), so upload here means picking the claim first, then reusing the exact same
// DocumentUploadWidget ClaimReview.tsx already uses (A2: never a second uploader).
//
// design language v2 §8.24 note (ui-migration-ledger.md screen-6 entry): this screen has no
// filter or search over the document list itself (only the upload panel's claim finder, a
// distinct control), so "empty-after-filter" does not apply here, same accepted, documented
// partial as screen 1's precedent, not an invented feature to force the checklist item green.
// "Not configured" and "no permission" are likewise inapplicable: this hook has no per-provider
// configuration state to report, and the shell's route gate handles no-permission before this
// screen renders (also screen 1's precedent).
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
        sub="Every document received across claims, customers, and channels. A person checks what the AI reads off a document, and anything it is unsure about is treated as still missing."
        action={
          canUploadDocuments ? (
            <Button variant="primary" icon={<Upload aria-hidden="true" />} onClick={() => setPanelOpen((v) => !v)}>
              Upload document
            </Button>
          ) : undefined
        }
      />

      {panelOpen && canUploadDocuments && (
        <GlassCard className="mb-4">
          <div className="mb-3 flex items-center justify-between">
            <div className="text-[15px] font-bold text-dls-text-primary">Upload to a claim</div>
            <IconButton aria-label="Close upload panel" onClick={closePanel}>
              <X className="h-4 w-4" aria-hidden="true" />
            </IconButton>
          </div>
          <div className="mb-3 text-[12.5px] text-dls-text-secondary">Documents attach to a claim. Find the claim first, then upload.</div>

          {!selectedClaim ? (
            <>
              <Input
                value={claimQuery}
                onChange={(e) => setClaimQuery(e.target.value)}
                placeholder="Search by customer name, policy number, or claim ID…"
                leadingIcon={<Search aria-hidden="true" />}
                autoFocus
                className="mb-3"
              />
              {claimsLoading ? (
                <div className="p-4 text-center text-[13px] text-dls-text-secondary">Loading claims…</div>
              ) : matchingClaims.length === 0 ? (
                <div className="p-4 text-center text-[13px] text-dls-text-secondary">No matching claims.</div>
              ) : (
                <div className="max-h-72 space-y-1.5 overflow-y-auto">
                  {matchingClaims.map((c) => (
                    <button
                      key={c.id}
                      type="button"
                      onClick={() => setSelectedClaimId(c.id)}
                      className="dls-focus-outline flex w-full items-center justify-between gap-3 rounded-dls-control border border-dls-border-subtle bg-dls-surface px-3 py-2.5 text-left transition-colors duration-dls-fast ease-dls hover:bg-dls-bg-page"
                    >
                      <div className="min-w-0">
                        <div className="truncate text-[13px] font-semibold text-dls-text-primary">{c.customer} · {c.policy}</div>
                        <div className="truncate text-[11px] text-dls-text-muted">{c.id.slice(0, 8)} · {c.productLine} · {c.claimType}</div>
                      </div>
                      <StatusBadge status={c.status} />
                    </button>
                  ))}
                </div>
              )}
            </>
          ) : (
            <div>
              <div className="mb-3 flex items-center justify-between gap-3 rounded-dls-control border border-dls-border-subtle bg-dls-surface-sunken px-3 py-2.5">
                <div className="min-w-0">
                  <div className="truncate text-[13px] font-semibold text-dls-text-primary">{selectedClaim.customer} · {selectedClaim.policy}</div>
                  <div className="truncate text-[11px] text-dls-text-muted">{selectedClaim.id.slice(0, 8)} · {selectedClaim.productLine}</div>
                </div>
                <button
                  type="button"
                  onClick={() => setSelectedClaimId(null)}
                  className="dls-focus-outline shrink-0 rounded-dls-control px-2 py-1 text-[12.5px] font-bold text-dls-primary hover:bg-dls-bg-page"
                >
                  Change claim
                </button>
              </div>
              <DocumentUploadWidget
                caseId={selectedClaim.id}
                role={roleDef.key}
                onUploaded={async () => {
                  await reload();
                }}
              />
              <Link
                to={`/app/claims/${selectedClaim.id}`}
                className="dls-focus-outline mt-3 inline-flex items-center gap-1 rounded-dls-control text-[11.5px] font-semibold text-dls-primary hover:underline"
              >
                <ExternalLink className="h-3 w-3" aria-hidden="true" /> Open this claim
              </Link>
            </div>
          )}
        </GlassCard>
      )}

      {loading ? (
        <GlassCard>
          <LoadingSkeleton rows={5} />
        </GlassCard>
      ) : error ? (
        <GlassCard>
          <ErrorStateSurface title="Documents could not be loaded" message={error} />
        </GlassCard>
      ) : documents.length === 0 ? (
        <GlassCard>
          <EmptyStateSurface message="Only documents attached to a claim in this workspace appear here." />
        </GlassCard>
      ) : (
        <Table label="Document library">
          <TableHead>
            <TableRow>
              <TableHeaderCell>Document</TableHeaderCell>
              <TableHeaderCell>Type</TableHeaderCell>
              <TableHeaderCell>Claim</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
              <TableHeaderCell>Confidence</TableHeaderCell>
              <TableHeaderCell>Uploaded</TableHeaderCell>
              <TableHeaderCell aria-label="Actions" />
            </TableRow>
          </TableHead>
          <TableBody>
            {documents.map((d) => {
              const reviewHref = extractionHref(d.claim, d.id);
              return (
                <TableRow key={d.id}>
                  <TableCell>
                    <div className="flex items-center gap-2 text-dls-text-primary">
                      <FileText className="h-4 w-4 text-dls-info-text" aria-hidden="true" />
                      {d.name}
                    </div>
                    <div className="mt-0.5 text-[11px] text-dls-text-muted">{d.id}</div>
                  </TableCell>
                  <TableCell className="text-dls-text-secondary">{d.type}</TableCell>
                  <TableCell className="font-semibold text-dls-primary">{d.claim}</TableCell>
                  <TableCell><StatusBadge status={d.status} /></TableCell>
                  <TableCell>{d.confidence > 0 ? <ConfidenceBadge value={d.confidence} /> : <span className="text-dls-text-muted">-</span>}</TableCell>
                  <TableCell className="text-dls-text-muted">{d.uploaded}</TableCell>
                  <TableCell>
                    {reviewHref ? (
                      <Link
                        to={reviewHref}
                        className="dls-focus-outline inline-flex items-center gap-1 rounded-dls-control border border-dls-border-input bg-dls-surface px-2 py-1 text-[11.5px] font-semibold text-dls-text-primary hover:bg-dls-bg-page"
                      >
                        <ExternalLink className="h-3 w-3" aria-hidden="true" /> Review
                      </Link>
                    ) : (
                      <span className="text-[11px] text-dls-text-muted">Review unavailable</span>
                    )}
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      )}
      <div className="mt-3 text-[11.5px] text-dls-text-muted">Allowed file types: PDF, JPEG, PNG · Current upload boundary: 10MB per document.</div>
    </>
  );
}
