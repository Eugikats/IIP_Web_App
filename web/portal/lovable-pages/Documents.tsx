"use client";
import Link from "next/link";
import { GlassCard, SectionHeader, Badge, StatusBadge, ConfidenceBadge } from "@/components/iip/Primitives";
import { ExternalLink, FileText } from "lucide-react";
import { useDocuments } from "@/lib/lovable-parity/data/documents";

export default function Documents() {
  const { documents, loading, error } = useDocuments();

  return (
    <>
      <SectionHeader
        eyebrow="Documents"
        title="Document library"
        sub="All documents ingested across claims, customers, and channels. AI extraction is human-reviewed — low-confidence fields are treated as incomplete."
      />
      {loading ? (
        <GlassCard><div className="p-6 text-center text-[13px] text-stone">Loading document library…</div></GlassCard>
      ) : documents.length === 0 ? (
        <GlassCard>
          <div className="p-10 text-center flex flex-col items-center gap-2">
            <FileText className="h-6 w-6 text-stone" />
            <div className="text-navy font-medium">No documents yet</div>
            <div className="text-[12.5px] text-stone max-w-md">{error ? error : "Only documents uploaded to a tenant-scoped claim appear here."}</div>
          </div>
        </GlassCard>
      ) : (
        <GlassCard className="!p-0 overflow-hidden">
          <table className="w-full text-[13px]">
            <thead className="bg-white/60">
              <tr className="text-left text-[11px] uppercase tracking-[0.14em] text-stone">
                <th className="p-3">Document</th><th className="p-3">Type</th><th className="p-3">Claim</th><th className="p-3">Status</th><th className="p-3">Confidence</th><th className="p-3">Uploaded</th><th className="p-3"></th>
              </tr>
            </thead>
            <tbody>
              {documents.map((d) => (
                <tr key={d.id} className="border-t border-cloud/70 hover:bg-white/50">
                  <td className="p-3"><div className="flex items-center gap-2 text-navy"><FileText className="h-4 w-4 text-info" />{d.name}</div><div className="text-[10.5px] text-stone">{d.id}</div></td>
                  <td className="p-3 text-slate2">{d.type}</td>
                  <td className="p-3 text-info">{d.claim}</td>
                  <td className="p-3"><StatusBadge status={d.status} /></td>
                  <td className="p-3">{d.confidence > 0 ? <ConfidenceBadge value={d.confidence} /> : <Badge tone="neutral">—</Badge>}</td>
                  <td className="p-3 text-stone">{d.uploaded}</td>
                  <td className="p-3">
                    <Link href={`/app/claims/${d.claim}/extraction?documentId=${d.id}`} className="inline-flex items-center gap-1 rounded-lg border border-cloud bg-white px-2 py-1 text-[11.5px] font-medium text-navy focus-ring"><ExternalLink className="h-3 w-3" /> Review</Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </GlassCard>
      )}
      <div className="mt-3 text-[11.5px] text-stone">Allowed file types: PDF, JPEG, PNG · Current upload boundary: 10MB per document.</div>
    </>
  );
}
