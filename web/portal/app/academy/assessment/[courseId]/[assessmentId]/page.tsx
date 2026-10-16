import AssessmentPage from "@/components/academy/pages/AssessmentPage";

export default async function Page({ params }: { params: Promise<{ courseId: string; assessmentId: string }> }) {
  const { courseId, assessmentId } = await params;
  return <AssessmentPage courseId={courseId} assessmentId={assessmentId} />;
}
