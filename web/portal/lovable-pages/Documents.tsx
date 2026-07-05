"use client";
import { toast } from "sonner";
import { GlassCard, SectionHeader, Badge, StatusBadge, ConfidenceBadge } from "@/components/iip/Primitives";
import { documentLibrary } from "@/lib/lovable-parity/mock";
import { FileText, RefreshCw, XCircle } from "lucide-react";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";

const parity = LOVABLE_VISUAL_PARITY_MODE;
const rows = parity ? documentLibrary : [];

export default function Documents() {
  const gov = (label: string) => (parity ? toast.success(label) : toast("Recorded — governed document action is audited."));
  return (
    <>
      <SectionHeader
        eyebrow="Documents"
        title="Document library"
        sub="All documents ingested across claims, customers, and channels. AI extraction is human-reviewed — low-confidence fields are treated as incomplete."
      />
      {rows.length === 0 ? (
        <GlassCard>
          <div className="p-10 text-center flex flex-col items-center gap-2">
            <FileText className="h-6 w-6 text-stone" />
            <div className="text-navy font-medium">Cross-case document library not configured</div>
            <div className="text-[12.5px] text-stone max-w-md">Documents are currently viewed per claim (open a claim → Documents). The cross-case library appears here once the documents list endpoint is wired — no sample documents are shown.</div>
          </div>
        </GlassCard>
      ) : (
        <GlassCard className="!p-0 overflow-hidden">
          <table className="w-full text-[13px]">
            <thead className="bg-white/60">
              <tr className="text-left text-[11px] uppercase tracking-[0.14em] text-stone">
                <th className="p-3">Document</th><th className="p-3">Type</th><th className="p-3">Claim</th><th className="p-3">Customer</th><th className="p-3">Status</th><th className="p-3">Confidence</th><th className="p-3">Evidence</th><th className="p-3">Uploaded</th><th className="p-3"></th>
              </tr>
            </thead>
            <tbody>
              {rows.map((d) => (
                <tr key={d.id} className="border-t border-cloud/70 hover:bg-white/50">
                  <td className="p-3"><div className="flex items-center gap-2 text-navy"><FileText className="h-4 w-4 text-info" />{d.name}</div><div className="text-[10.5px] text-stone">{d.id}</div></td>
                  <td className="p-3 text-slate2">{d.type}</td>
                  <td className="p-3 text-info">{d.claim ?? "—"}</td>
                  <td className="p-3 text-slate2">{d.customer ?? "—"}</td>
                  <td className="p-3"><StatusBadge status={d.status} /></td>
                  <td className="p-3">{d.confidence ? <ConfidenceBadge value={d.confidence} /> : <Badge tone="neutral">—</Badge>}</td>
                  <td className="p-3 text-stone">{d.evidence}</td>
                  <td className="p-3 text-stone">{d.uploaded}</td>
                  <td className="p-3">
                    <div className="flex items-center gap-1">
                      {(d.status === "Failed" || d.status === "Review Required") && (
                        <button onClick={() => gov("Extraction re-queued")} className="text-[11.5px] px-2 py-1 rounded-lg bg-white border border-cloud text-navy inline-flex items-center gap-1"><RefreshCw className="h-3 w-3" />Retry</button>
                      )}
                      <button onClick={() => gov("Document rejected")} className="text-[11.5px] px-2 py-1 rounded-lg bg-white border border-cloud text-error inline-flex items-center gap-1"><XCircle className="h-3 w-3" />Reject</button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </GlassCard>
      )}
      <div className="mt-3 text-[11.5px] text-stone">Allowed file types: PDF, JPEG, PNG · Max 20MB per document.</div>
    </>
  );
}
