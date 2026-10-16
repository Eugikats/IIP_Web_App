import FinalTaskPage from "@/components/academy/pages/FinalTaskPage";

export default async function Page({ params }: { params: Promise<{ courseId: string }> }) {
  const { courseId } = await params;
  return <FinalTaskPage courseId={courseId} />;
}
