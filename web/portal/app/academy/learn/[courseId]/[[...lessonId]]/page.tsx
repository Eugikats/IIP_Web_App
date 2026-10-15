import LessonPage from "@/components/academy/pages/LessonPage";

export default async function Page({ params }: { params: Promise<{ courseId: string; lessonId?: string[] }> }) {
  const { courseId, lessonId } = await params;
  return <LessonPage courseId={courseId} lessonId={lessonId?.[0] ?? null} />;
}
