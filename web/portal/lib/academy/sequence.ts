import type { CourseDetail } from "@/lib/academy/types";

export type SequenceItem = { kind: "lesson" | "assessment" | "check" | "final"; id: string; href: string; title: string; moduleId: string | null };

/** The order a learner works through a course: each module's lessons, then its assessment, then the course check or final task. */
export function buildSequence(course: CourseDetail): SequenceItem[] {
  const items: SequenceItem[] = [];
  for (const part of course.modules) {
    for (const lesson of part.lessons) {
      items.push({ kind: "lesson", id: lesson.id, href: `/academy/learn/${course.id}/${lesson.id}`, title: lesson.title, moduleId: part.id });
    }
    if (part.assessment) {
      items.push({ kind: "assessment", id: part.assessment.id, href: `/academy/assessment/${course.id}/${part.assessment.id}`, title: part.assessment.title, moduleId: part.id });
    }
  }
  if (course.check) items.push({ kind: "check", id: course.check.id, href: `/academy/assessment/${course.id}/${course.check.id}`, title: course.check.title, moduleId: null });
  if (course.finalTask) items.push({ kind: "final", id: course.finalTask.id, href: `/academy/final/${course.id}`, title: course.finalTask.title, moduleId: null });
  return items;
}
