"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { Icon } from "@/components/academy/Icon";
import { Thumb } from "@/components/academy/Thumb";
import { useAcademy } from "@/components/academy/AcademyProvider";
import { categoryTitle } from "@/lib/academy/categoryTitles";
import type { CourseSummary } from "@/lib/academy/types";

/** One course card, exactly as the design draws it, with the button the person's progress calls for. */
export function CourseCard({ course }: { course: CourseSummary }) {
  const router = useRouter();
  const { isEnrolled, percentOf, statusOf, nextLessonId, learner } = useAcademy();
  const enrolled = isEnrolled(course.id);
  const percent = percentOf(course);
  const status = statusOf(course);
  const next = nextLessonId(course);
  const last = learner?.progress[course.id]?.lastLessonId;
  const soon = course.status === "coming_soon";
  const arrow = <Icon name="arrowR" size={17} />;
  const stop = (event: React.MouseEvent) => event.stopPropagation();

  let button: React.ReactNode;
  if (soon) {
    button = <button type="button" className="btn soft block" disabled>Coming soon</button>;
  } else if (!enrolled) {
    button = <Link className="btn soft block" href={`/academy/courses/${course.id}`} onClick={stop}>View course {arrow}</Link>;
  } else if (status === "Completed") {
    button = <Link className="btn soft block" href="/academy/certificates" onClick={stop}>View certificate {arrow}</Link>;
  } else if (percent === 0) {
    button = <Link className="btn primary block" href={`/academy/learn/${course.id}/${next ?? ""}`} onClick={stop}>Start course {arrow}</Link>;
  } else if (last && next === last) {
    button = <Link className="btn primary block" href={`/academy/learn/${course.id}/${next}`} onClick={stop}>Continue learning {arrow}</Link>;
  } else {
    button = <Link className="btn soft block" href={`/academy/learn/${course.id}/${next ?? ""}`} onClick={stop}>Resume {arrow}</Link>;
  }

  return (
    <article className="ccard card" onClick={() => router.push(`/academy/courses/${course.id}`)} data-testid={`course-card-${course.id}`}>
      <Thumb course={course} badge={soon ? "Coming soon" : status} />
      <div className="cbody">
        <div className="cat">{categoryTitle(course.category)}</div>
        <h3><Link href={`/academy/courses/${course.id}`} onClick={stop}>{course.title}</Link></h3>
        {enrolled ? (
          <>
            <div className="pc">{percent}% complete</div>
            <div className="bar"><i style={{ width: `${percent}%` }} /></div>
          </>
        ) : (
          <>
            <div className="pc">{soon ? "Lessons are being prepared" : `${course.level ? `${course.level}, ` : ""}${course.lessons} lessons`}</div>
            <div className="bar ghost" />
          </>
        )}
        <div className="meta">
          {soon ? (
            <span><Icon name="clock" size={16} /> Coming soon</span>
          ) : (
            <>
              <span><Icon name="layers" size={16} /> {course.modules} modules</span>
              <span className="sep" />
              <span><Icon name="video" size={16} /> {course.lessons} lessons</span>
            </>
          )}
        </div>
        {button}
      </div>
    </article>
  );
}
