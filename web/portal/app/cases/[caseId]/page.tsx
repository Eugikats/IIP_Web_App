import { CaseDetailClient } from "@/app/cases/[caseId]/CaseDetailClient";

export default async function CaseDetailPage({ params }: { params: Promise<{ caseId: string }> }) {
  const { caseId } = await params;

  return (
    <div className="space-y-5">
      <div>
        <h2 className="text-2xl font-semibold text-ink">Case detail</h2>
        <p className="mt-1 text-sm text-muted">Tenant-scoped case lookup with safe audit metadata.</p>
      </div>
      <CaseDetailClient caseId={caseId} />
    </div>
  );
}
