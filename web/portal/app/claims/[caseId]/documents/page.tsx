import { DocumentCheckerClient } from "@/app/document-checker/DocumentCheckerClient";

export default async function CaseDocumentsPage({ params }: { params: Promise<{ caseId: string }> }) {
  const { caseId } = await params;

  return <DocumentCheckerClient initialCaseId={caseId} lockedCaseId />;
}
