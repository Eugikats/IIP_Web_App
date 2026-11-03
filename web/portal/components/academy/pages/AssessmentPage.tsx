"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";
import { Icon } from "@/components/academy/Icon";
import { useAcademy } from "@/components/academy/AcademyProvider";
import { PageState, SkeletonCards } from "@/components/academy/PageState";
import { SignInCard, useCourse } from "@/components/academy/pages/shared";
import { academyGet, academyPost, academyLeave, AcademyApiError } from "@/lib/academy/api";
import { buildSequence } from "@/lib/academy/sequence";

type Started = {
  id: string;
  sittingId: string;
  courseId: string;
  title: string;
  kind: "quiz" | "module" | "check";
  lessonId: string | null;
  passMark: number;
  maxAttempts: number;
  attemptsUsed: number;
  attemptsLeft: number | null;
  best: number;
  passed: boolean;
  questions: { id: string; q: string; options: { k: number; text: string }[] }[];
};

type Result = {
  score: number;
  correct: number;
  total: number;
  passed: boolean;
  passMark: number;
  attempts: number;
  attemptsLeft: number | null;
  best: number;
  lessonCompleted: boolean;
  certificateIssued: boolean;
  review: { id: string; q: string; options: string[]; chosen: number; right: boolean; correctIndex: number | null; why: string | null }[];
};

const LETTERS = "ABCDEFGH";

// A unit quiz, a module assessment or a course check. Questions arrive without answers, in a shuffled order; the server
// grades the submission and says what was right. The look is the design's: cards, the same option buttons Zenith's quiz uses.
export default function AssessmentPage({ courseId, assessmentId }: { courseId: string; assessmentId: string }) {
  const { me, loadingMe, learner, loadingLearner, isEnrolled, refreshLearner, toast } = useAcademy();
  const { course } = useCourse(courseId);
  const [started, setStarted] = useState<Started | null>(null);
  const [problem, setProblem] = useState<{ status: number; code?: string; message: string } | null>(null);
  const [answers, setAnswers] = useState<Record<string, number>>({});
  const [result, setResult] = useState<Result | null>(null);
  const [busy, setBusy] = useState(false);
  const submittedRef = useRef(false);
  const sittingRef = useRef<string | null>(null);
  const generationRef = useRef(0);

  const load = useCallback(async () => {
    const generation = ++generationRef.current;
    submittedRef.current = false;
    const previous = sittingRef.current;
    sittingRef.current = null;
    if (previous) void academyLeave(`/courses/${courseId}/assessments/${assessmentId}/leave`, { sittingId: previous }).catch(() => undefined);
    setStarted(null);
    setResult(null);
    setAnswers({});
    setProblem(null);
    try {
      const opened = await academyGet<Started>(`/courses/${courseId}/assessments/${assessmentId}`);
      if (generation !== generationRef.current) {
        void academyLeave(`/courses/${courseId}/assessments/${assessmentId}/leave`, { sittingId: opened.sittingId }).catch(() => undefined);
        return;
      }
      sittingRef.current = opened.sittingId;
      setStarted(opened);
    } catch (failure) {
      setProblem(failure instanceof AcademyApiError
        ? { status: failure.status, code: failure.code, message: failure.message }
        : { status: 0, message: "The questions could not be loaded." });
    }
  }, [courseId, assessmentId]);

  useEffect(() => {
    if (me?.signedIn) void load();
  }, [me, load]);

  // Leaving without submitting hands Zenith back. (A submission hands it back on the server.)
  useEffect(() => {
    const leave = () => {
      const sittingId = sittingRef.current;
      if (sittingId && !submittedRef.current) void academyLeave(`/courses/${courseId}/assessments/${assessmentId}/leave`, { sittingId }).catch(() => undefined);
    };
    window.addEventListener("pagehide", leave);
    return () => {
      generationRef.current += 1;
      window.removeEventListener("pagehide", leave);
      leave();
      sittingRef.current = null;
    };
  }, [courseId, assessmentId]);

  const submit = async () => {
    if (!started) return;
    setBusy(true);
    try {
      const outcome = await academyPost<Result>(`/courses/${courseId}/assessments/${assessmentId}/submit`, { answers });
      submittedRef.current = true;
      setResult(outcome);
      await refreshLearner();
      if (outcome.certificateIssued) toast("Course complete. Your certificate is ready", "award");
      window.scrollTo({ top: 0 });
    } catch (failure) {
      toast(failure instanceof AcademyApiError ? failure.message : "Your answers could not be submitted.", "x");
    } finally {
      setBusy(false);
    }
  };

  if (loadingMe) return <SkeletonCards count={1} />;
  if (!me?.signedIn) return <SignInCard title="Sign in to take this assessment" text="Assessments are for people who have signed in and enrolled." />;
  if (!loadingLearner && learner && !isEnrolled(courseId)) {
    return <PageState kind="forbidden" title="Enrol to take this assessment" text="Assessments are open to people who have enrolled in the course." action={{ label: "View the course", href: `/academy/courses/${courseId}` }} />;
  }
  if (problem) {
    if (problem.status === 404) return <PageState kind="notfound" title="Page not found" text="That assessment does not exist." action={{ label: "All courses", href: "/academy/courses" }} />;
    if (problem.code === "no_attempts") {
      return <PageState kind="forbidden" title="No attempts left" text={problem.message} action={{ label: "Back to the course", href: `/academy/courses/${courseId}` }} />;
    }
    return <PageState kind={problem.status === 403 ? "forbidden" : "error"} title="The assessment could not be opened" text={problem.message} action={{ label: "Back to the course", href: `/academy/courses/${courseId}` }} />;
  }
  if (!started) return <SkeletonCards count={1} />;

  const sequence = course ? buildSequence(course) : [];
  const anchor = started.kind === "quiz" ? started.lessonId : started.id;
  const at = sequence.findIndex((item) => item.id === anchor && (started.kind === "quiz" ? item.kind === "lesson" : item.kind !== "lesson"));
  const next = at >= 0 ? sequence[at + 1] : undefined;
  const continueHref = next?.href ?? `/academy/courses/${courseId}`;
  const continueLabel = next ? (next.kind === "lesson" ? "Next lesson" : next.kind === "final" ? "Final task" : `Next: ${next.title}`) : "Back to course";
  const backHref = started.kind === "quiz" && started.lessonId ? `/academy/learn/${courseId}/${started.lessonId}` : `/academy/courses/${courseId}`;

  const answered = Object.keys(answers).length;
  const crumbs = (
    <div className="crumbs">
      <Link href="/academy/courses">All courses</Link><Icon name="chevR" size={14} />
      <Link href={`/academy/courses/${courseId}`}>{course?.title ?? "Course"}</Link><Icon name="chevR" size={14} />
      <span>{started.title}</span>
    </div>
  );

  if (result) {
    const lowCredit = !result.passed;
    return (
      <>
        {crumbs}
        <section className="card panel" data-testid="assessment-result">
          <div className="asc">
            <div className={`ascore ${result.passed ? "ok" : "no"}`}>
              <b>{result.score}%</b>
              <small>{result.correct} of {result.total}</small>
            </div>
            <div>
              <h1>{result.passed ? "You passed" : "Not passed yet"}</h1>
              <p>
                You scored {result.score}%. The pass mark is {result.passMark}%.
                {result.attemptsLeft !== null ? ` Attempts left: ${result.attemptsLeft}.` : ""}
                {result.lessonCompleted ? " This lesson is now complete." : ""}
              </p>
              {result.certificateIssued ? <p><b>Your certificate is ready.</b></p> : null}
            </div>
          </div>
          <div className="asc-actions">
            {result.passed ? (
              <Link className="btn primary" href={result.certificateIssued ? "/academy/certificates" : continueHref} data-testid="assessment-continue">
                {result.certificateIssued ? "View certificate" : continueLabel} <Icon name="arrowR" size={16} />
              </Link>
            ) : null}
            {lowCredit && (result.attemptsLeft === null || result.attemptsLeft > 0) ? (
              <button type="button" className="btn primary" onClick={() => void load()} data-testid="assessment-retake">Try again <Icon name="refresh" size={16} /></button>
            ) : null}
            <Link className="btn ghost" href={backHref}>{started.kind === "quiz" ? "Back to the lesson" : "Back to the course"}</Link>
          </div>
          {lowCredit && result.attemptsLeft === 0 ? (
            <div className="notice" style={{ marginTop: 14 }}><Icon name="lock" size={18} /><span>You have used every attempt. Ask the Academy team to reset this assessment.</span></div>
          ) : null}
        </section>

        <section className="card panel" style={{ marginTop: 18 }}>
          <h2>Your answers</h2>
          {result.correct < result.total && result.review.every((item) => item.correctIndex === null) ? (
            <p style={{ color: "var(--muted)", margin: "6px 0 0" }}>The right answers are shown once you pass or use your last attempt, so a retake tests what you know.</p>
          ) : null}
          {result.review.map((item, index) => (
            <div key={item.id} className="aq">
              <div className="qq"><span className="qn">{index + 1}</span>{item.q}</div>
              <div className="quiz">
                {item.options.map((option, k) => {
                  const state = item.correctIndex !== null && k === item.correctIndex ? "right" : k === item.chosen && !item.right ? "wrong" : k === item.chosen && item.right ? "right" : "";
                  return <div key={k} className={`qopt ${state}`}>{LETTERS[k]}. {option}{k === item.chosen ? <i className="you">Your answer</i> : null}</div>;
                })}
                {item.why ? <div className="qfb"><b>{item.right ? "Correct." : "Not quite."}</b> {item.why}</div> : <div className="qfb"><b>{item.right ? "Correct." : "Not quite."}</b></div>}
              </div>
            </div>
          ))}
        </section>
      </>
    );
  }

  return (
    <>
      {crumbs}
      <section className="card panel">
        <div className="lmeta">{started.kind === "quiz" ? "Unit quiz" : started.kind === "module" ? "Module assessment" : "Course check"}</div>
        <h1 style={{ margin: "4px 0 6px" }}>{started.title}</h1>
        <p style={{ color: "var(--muted)" }}>
          {started.questions.length} questions. You need {started.passMark}% to pass.
          {started.attemptsLeft !== null ? ` You have ${started.attemptsLeft} of ${started.maxAttempts} attempts left.` : " You can try again as often as you like."}
          {started.best ? ` Your best score so far is ${started.best}%.` : ""}
        </p>
        <div className="bar" style={{ marginTop: 12 }}><i style={{ width: `${started.questions.length ? (answered / started.questions.length) * 100 : 0}%` }} /></div>
        <div className="pc" style={{ fontSize: 13 }}>{answered} of {started.questions.length} answered</div>
      </section>

      {started.questions.map((question, index) => (
        <section key={question.id} className="card panel aq" data-testid="question">
          <div className="qq"><span className="qn">{index + 1}</span>{question.q}</div>
          <div className="quiz" role="radiogroup" aria-label={`Question ${index + 1}`}>
            {question.options.map((option, position) => (
              <button
                key={option.k}
                type="button"
                role="radio"
                aria-checked={answers[question.id] === option.k}
                className={`qopt${answers[question.id] === option.k ? " sel" : ""}`}
                onClick={() => setAnswers((current) => ({ ...current, [question.id]: option.k }))}
              >
                {LETTERS[position]}. {option.text}
              </button>
            ))}
          </div>
        </section>
      ))}

      <div className="asub">
        <Link className="btn ghost" href={backHref}>Cancel</Link>
        <button type="button" className="btn primary" disabled={busy || answered < started.questions.length} onClick={() => void submit()} data-testid="submit-assessment">
          {busy ? "Marking..." : answered < started.questions.length ? `Answer all ${started.questions.length} questions` : "Submit answers"}
        </button>
      </div>
    </>
  );
}
