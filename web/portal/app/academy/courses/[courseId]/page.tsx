import CoursePage from "@/components/academy/pages/CoursePage";

export default async function Page({ params }: { params: Promise<{ courseId: string }> }) {
  const { courseId } = await params;
  return <CoursePage courseId={courseId} />;
}
