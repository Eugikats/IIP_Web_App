"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Icon } from "@/components/academy/Icon";
import { LessonBlocks } from "@/components/academy/LessonBlocks";
import { useAcademy } from "@/components/academy/AcademyProvider";
import { PageState, SkeletonCards } from "@/components/academy/PageState";
import { academyGet, academyPost, academyPut, AcademyApiError } from "@/lib/academy/api";
import { buildSequence } from "@/lib/academy/sequence";
import { ZENITH_READY } from "@/lib/academy/flags";
import type { CourseDetail, LessonView } from "@/lib/academy/types";

// A lesson, as designed: the player area on top, the lesson beside the course outline, a place for the learner's own
// note, and the way to the next step. There are no recordings yet, so the player area says so and the lesson is read.
export default function LessonPage({ courseId, lessonId }: { courseId: string; lessonId: string | null }) {
  const router = useRouter();
  const { me, loadingMe, learner, loadingLearner, isEnrolled, progressOf, refreshLearner, toast, signIn, setZenithOpen } = useAcademy();
  const [course, setCourse] = useState<CourseDetail | null>(null);
  const [lesson, setLesson] = useState<LessonView | null>(null);
  const [problem, setProblem] = useState<{ status: number; message: string } | null>(null);
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let cancelled = false;
    academyGet<CourseDetail>(`/public/courses/${courseId}`)
      .then((result) => { if (!cancelled) setCourse(result); })
      .catch((failure) => { if (!cancelled) setProblem({ status: failure instanceof AcademyApiError ? failure.status : 0, message: "The course could not be loaded." }); });
    return () => { cancelled = true; };
  }, [courseId]);

  const progress = progressOf(courseId);
  const sequence = useMemo(() => (course ? buildSequence(course) : []), [course]);

  // No lesson named: go to the one the learner is up to.
  useEffect(() => {
    if (lessonId || !course || loadingLearner) return;
    const done = new Set(progress.doneLessons);
    const first = sequence.find((item) => item.kind === "lesson" && !done.has(item.id)) ?? sequence.find((item) => item.kind === "lesson");
    if (first) router.replace(first.href);
  }, [lessonId, course, loadingLearner, progress.doneLessons, sequence, router]);

  useEffect(() => {
    if (!lessonId || !me?.signedIn) return;
    let cancelled = false;
    setLesson(null);
    academyGet<LessonView>(`/courses/${courseId}/lessons/${lessonId}`)
      .then((result) => { if (!cancelled) { setLesson(result); setNote(result.note); } })
      .catch((failure) => { if (!cancelled) setProblem({ status: failure instanceof AcademyApiError ? failure.status : 0, message: failure instanceof AcademyApiError ? failure.message : "The lesson could not be loaded." }); });
    return () => { cancelled = true; };
  }, [courseId, lessonId, me]);

  const complete = useCallback(async () => {
    if (!lesson) return;
    setBusy(true);
    try {
      const result = await academyPost<{ newlyCompleted: boolean; minutes: number; certificateIssued: boolean }>(`/courses/${courseId}/lessons/${lesson.id}/complete`);
      await refreshLearner();
      if (result.newlyCompleted) toast(`Lesson complete. +${result.minutes} minutes today`);
      const index = sequence.findIndex((item) => item.kind === "lesson" && item.id === lesson.id);
      router.push(sequence[index + 1]?.href ?? `/academy/courses/${courseId}`);
    } catch (failure) {
      toast(failure instanceof AcademyApiError ? failure.message : "The lesson could not be marked complete.", "x");
    } finally {
      setBusy(false);
    }
  }, [lesson, courseId, sequence, refreshLearner, toast, router]);

  const saveNote = async () => {
    if (!lesson) return;
    try {
      await academyPut(`/courses/${courseId}/lessons/${lesson.id}/note`, { text: note });
      toast("Note saved");
    } catch {
      toast("The note could not be saved", "x");
    }
  };

  if (problem) {
    if (problem.status === 404) return <PageState kind="notfound" title="Page not found" text="That lesson does not exist." action={{ label: "All courses", href: "/academy/courses" }} />;
    if (problem.status === 403 || problem.status === 409) {
      return <PageState kind="forbidden" title="Enrol to open this lesson" text="Lessons are open to people who have enrolled in the course." action={{ label: "View the course", href: `/academy/courses/${courseId}` }} />;
    }
    return <PageState kind="error" title="The lesson could not be loaded" text={problem.message} />;
  }
  if (loadingMe || !course) return <SkeletonCards count={1} />;
  if (!me?.signedIn) {
    return (
      <section className="card panel state">
        <h2>Sign in to take this course</h2>
        <p>Lessons are for people who have signed in and enrolled.</p>
        <button type="button" className="btn primary" onClick={signIn}>Sign in</button>
      </section>
    );
  }
  if (!loadingLearner && learner && !isEnrolled(courseId)) {
    return <PageState kind="forbidden" title="Enrol to open this lesson" text="Lessons are open to people who have enrolled in the course." action={{ label: "View the course", href: `/academy/courses/${courseId}` }} />;
  }
  if (!lessonId || !lesson) return <SkeletonCards count={1} />;

  const done = new Set(progress.doneLessons);
  const isDone = done.has(lesson.id);
  const index = sequence.findIndex((item) => item.kind === "lesson" && item.id === lesson.id);
  const previous = sequence.slice(0, index).reverse().find((item) => item.kind === "lesson");
  const next = sequence[index + 1];
  const percent = course.modules.reduce((sum, module) => sum + module.lessons.length, 0)
    ? Math.round((done.size / course.modules.reduce((sum, module) => sum + module.lessons.length, 0)) * 100)
    : 0;
  const quizPassed = progress.passedQuizzes.includes(lesson.id);

  let primary: React.ReactNode;
  if (lesson.hasQuiz && !isDone) {
    primary = <Link className="btn primary" href={`/academy/assessment/${courseId}/${lesson.id}-quiz`} data-testid="take-quiz">Take the unit quiz <Icon name="arrowR" size={16} /></Link>;
  } else if (isDone) {
    primary = <Link className="btn primary" href={next?.href ?? `/academy/courses/${courseId}`} data-testid="next-step">{next ? (next.kind === "lesson" ? "Next lesson" : next.kind === "final" ? "Final task" : "Next: " + next.title) : "Back to course"} <Icon name="arrowR" size={16} /></Link>;
  } else {
    primary = <button type="button" className="btn primary" onClick={() => void complete()} disabled={busy} data-testid="complete-lesson">{next ? "Mark complete and continue" : "Complete course"} <Icon name="arrowR" size={16} /></button>;
  }

  const moduleLessons = course.modules.map((module, moduleIndex) => (
    <div key={module.id}>
      <div className="mt">Module {moduleIndex + 1}: {module.title}</div>
      {module.lessons.map((entry) => (
        <Link key={entry.id} className={`lrow${entry.id === lesson.id ? " on" : ""}`} href={`/academy/learn/${courseId}/${entry.id}`}>
          <span className={`st${done.has(entry.id) ? " done" : ""}`}>{done.has(entry.id) ? <Icon name="check" size={12} /> : null}</span>
          {entry.title}
          <span className="dur">{entry.minutes}m</span>
        </Link>
      ))}
      {module.assessment ? (
        <Link className="lrow" href={`/academy/assessment/${courseId}/${module.assessment.id}`}>
          <span className={`st${progress.assessments[module.assessment.id]?.passed ? " done" : ""}`}>{progress.assessments[module.assessment.id]?.passed ? <Icon name="check" size={12} /> : null}</span>
          {module.assessment.title}
          <span className="dur">{module.assessment.questions} q</span>
        </Link>
      ) : null}
    </div>
  ));

  return (
    <>
      <div className="crumbs">
        <Link href="/academy/courses">All courses</Link><Icon name="chevR" size={14} />
        <Link href={`/academy/courses/${courseId}`}>{course.title}</Link><Icon name="chevR" size={14} />
        <span>Lesson {index + 1 - sequence.slice(0, index).filter((item) => item.kind !== "lesson").length}</span>
      </div>

      <div className="player" id="player" style={{ background: `linear-gradient(135deg,${course.hue[0]},${course.hue[1]})` }}>
        <div className="shade" />
        <div className="ptitle"><Icon name="video" size={17} /><span>Module {lesson.moduleNumber}, Lesson {lesson.indexInModule}: {lesson.title}</span></div>
        {lesson.video ? (
          <video src={lesson.video} controls style={{ position: "relative", width: "100%", height: "100%", objectFit: "contain" }} />
        ) : (
          <div className="noreel" data-testid="no-recording">
            <Icon name="video" size={34} />
            <b>No recording for this lesson yet.</b>
            <div>Read the lesson below. It has everything the lesson covers.</div>
          </div>
        )}
      </div>

      <div className="learn">
        <div>
          <section className="card lcontent">
            <div className="lmeta">Module {lesson.moduleNumber} · Lesson {lesson.indexInModule} of {lesson.lessonsInModule} · about {lesson.minutes} min read</div>
            <h1>{lesson.title}</h1>
            {lesson.outcome ? <p><b>By the end of this lesson you can:</b> {lesson.outcome}</p> : null}
            <LessonBlocks blocks={lesson.blocks} />

            {lesson.sources.length && course.sources.length ? (
              <>
                <h3>Sources for this lesson</h3>
                <div className="srcs lesson">
                  {lesson.sources.map((id) => {
                    const source = course.sources.find((entry) => entry.id === id);
                    return <span key={id} className="src" title={source?.title}><Icon name="book" size={13} />{id}{source ? `: ${source.title.length > 60 ? source.title.slice(0, 58) + "..." : source.title}` : ""}</span>;
                  })}
                </div>
              </>
            ) : null}

            <div className="tryit">
              <b><Icon name="pen" size={17} />{lesson.tryIt ? "Try it" : "Your notes"}</b>
              <p>{lesson.tryIt ?? "Write down what you want to remember or apply at work."}</p>
              <textarea id="note" value={note} onChange={(event) => setNote(event.target.value)} placeholder="Write your reflection..." aria-label="Your note for this lesson" />
              <div style={{ display: "flex", gap: 8, marginTop: 10, flexWrap: "wrap" }}>
                <button type="button" className="btn soft sm" onClick={() => void saveNote()}>Save note</button>
                {ZENITH_READY ? <button type="button" className="btn ghost sm" onClick={() => setZenithOpen(true)}><Icon name="sparkle" size={15} />Ask Zenith about this lesson</button> : null}
              </div>
            </div>

            {lesson.hasQuiz ? (
              <p className="pnote" style={{ marginTop: 16, color: "var(--muted)", fontSize: 14 }}>
                {quizPassed ? "You have passed this unit's quiz." : "Finish the unit with its 10-question quiz. You need 70% to pass, and you can try again."}
              </p>
            ) : null}

            <div className="lnav">
              {previous ? <Link className="btn ghost" href={previous.href}><Icon name="chevL" size={16} />Previous</Link> : <span />}
              {primary}
            </div>
          </section>
        </div>
        <aside className="card lside">
          <h3>{course.title}</h3>
          <div className="pc" style={{ fontSize: 13 }}>{percent}% complete</div>
          <div className="bar"><i style={{ width: `${percent}%` }} /></div>
          {moduleLessons}
        </aside>
      </div>
    </>
  );
}
