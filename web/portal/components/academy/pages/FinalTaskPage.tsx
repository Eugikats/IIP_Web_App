"use client";

import Link from "next/link";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Icon } from "@/components/academy/Icon";
import { useAcademy } from "@/components/academy/AcademyProvider";
import { PageState, SkeletonCards } from "@/components/academy/PageState";
import { SignInCard } from "@/components/academy/pages/shared";
import { academyGet, academyPost, academyPut, AcademyApiError } from "@/lib/academy/api";
import { todayLabel } from "@/lib/academy/format";

type FinalView = {
  id: string;
  courseId: string;
  courseTitle: string;
  title: string;
  brief: string;
  deliverables: string;
  integrity: string;
  requirement: string;
  canvasBoxes: string[];
  minWords: number;
  maxWords: number;
  passMark: number;
  outOf: number;
  rubric: { criterion: string; marks: number; distinction: string }[];
  locked: boolean;
  lockedReason: string | null;
  submission: {
    status: "none" | "draft" | "submitted" | "passed" | "not_passed";
    canvas: string[];
    proposal: string;
    submittedAt: string | null;
    score: number | null;
    scores: { criterion: string; marks: number; outOf: number }[];
    feedback: string | null;
  };
};

const words = (text: string) => text.split(/\s+/).filter(Boolean).length;

// The applied task that closes a course: a ten-box canvas and a written proposal, marked by a person against the rubric.
export default function FinalTaskPage({ courseId }: { courseId: string }) {
  const { me, loadingMe, learner, loadingLearner, isEnrolled, refreshLearner, toast } = useAcademy();
  const [view, setView] = useState<FinalView | null>(null);
  const [problem, setProblem] = useState<{ status: number; message: string } | null>(null);
  const [canvas, setCanvas] = useState<string[]>([]);
  const [proposal, setProposal] = useState("");
  const [busy, setBusy] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const result = await academyGet<FinalView>(`/courses/${courseId}/final`);
      setView(result);
      setCanvas(result.canvasBoxes.map((_, index) => result.submission.canvas[index] ?? ""));
      setProposal(result.submission.proposal);
    } catch (failure) {
      setProblem(failure instanceof AcademyApiError ? { status: failure.status, message: failure.message } : { status: 0, message: "The task could not be loaded." });
    }
  }, [courseId]);

  useEffect(() => { if (me?.signedIn) void load(); }, [me, load]);

  const count = useMemo(() => words(proposal), [proposal]);

  // While this page is open Zenith is switched off. It tells the server so every minute, and the server keeps it off for
  // a few minutes after the last signal.
  const sitting = Boolean(view && !view.locked && view.submission.status !== "submitted" && view.submission.status !== "passed");
  useEffect(() => {
    if (!sitting) return;
    const signal = () => { void academyPost(`/courses/${courseId}/final/open`).catch(() => undefined); };
    signal();
    const timer = setInterval(signal, 60_000);
    return () => {
      clearInterval(timer);
      void academyPost(`/courses/${courseId}/final/leave`).catch(() => undefined);
    };
  }, [sitting, courseId]);

  if (loadingMe) return <SkeletonCards count={1} />;
  if (!me?.signedIn) return <SignInCard title="Sign in to open the final task" text="The final task is for people who have signed in and enrolled." />;
  if (!loadingLearner && learner && !isEnrolled(courseId)) {
    return <PageState kind="forbidden" title="Enrol to open the final task" text="The final task is open to people who have enrolled in the course." action={{ label: "View the course", href: `/academy/courses/${courseId}` }} />;
  }
  if (problem) {
    if (problem.status === 404) return <PageState kind="notfound" title="Page not found" text="This course has no final task." action={{ label: "All courses", href: "/academy/courses" }} />;
    return <PageState kind="error" title="The final task could not be opened" text={problem.message} />;
  }
  if (!view) return <SkeletonCards count={1} />;

  const status = view.submission.status;
  const closed = status === "submitted" || status === "passed";

  const saveDraft = async () => {
    setBusy(true);
    try {
      await academyPut(`/courses/${courseId}/final`, { canvas, proposal });
      toast("Draft saved");
    } catch (failure) {
      toast(failure instanceof AcademyApiError ? failure.message : "The draft could not be saved.", "x");
    } finally {
      setBusy(false);
    }
  };

  const submit = async () => {
    setFormError(null);
    if (canvas.some((value) => !value.trim())) { setFormError(`Fill every box of the canvas (${view.canvasBoxes.length} boxes).`); return; }
    if (view.minWords && count < view.minWords) { setFormError(`The proposal is ${count} words. It needs at least ${view.minWords}.`); return; }
    if (view.maxWords && count > view.maxWords) { setFormError(`The proposal is ${count} words. It can be at most ${view.maxWords}.`); return; }
    setBusy(true);
    try {
      await academyPost(`/courses/${courseId}/final/submit`, { canvas, proposal });
      await refreshLearner();
      await load();
      toast("Submitted. You will be told when it is marked");
      window.scrollTo({ top: 0 });
    } catch (failure) {
      setFormError(failure instanceof AcademyApiError ? failure.message : "The task could not be submitted.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <div className="crumbs">
        <Link href="/academy/courses">All courses</Link><Icon name="chevR" size={14} />
        <Link href={`/academy/courses/${courseId}`}>{view.courseTitle}</Link><Icon name="chevR" size={14} />
        <span>Final task</span>
      </div>

      <section className="card panel">
        <div className="lmeta">Final applied task · pass mark {view.passMark} of {view.outOf}</div>
        <h1 style={{ margin: "4px 0 10px" }}>{view.title}</h1>
        {view.brief ? <p style={{ lineHeight: 1.7 }}>{view.brief}</p> : null}
        {view.deliverables ? <p style={{ lineHeight: 1.7, marginTop: 10 }}><b>What to hand in:</b> {view.deliverables}</p> : null}
        {view.requirement ? <p style={{ lineHeight: 1.7, marginTop: 10 }}><b>Requirement:</b> {view.requirement}</p> : null}
        {view.integrity ? <div className="notice blue" style={{ marginTop: 12 }}><Icon name="shield" size={18} /><span>{view.integrity}</span></div> : null}
      </section>

      {view.locked ? (
        <section className="card panel state" style={{ marginTop: 18 }} data-testid="final-locked">
          <div style={{ width: 56, height: 56, borderRadius: "50%", background: "var(--blue-2)", color: "var(--blue)", display: "grid", placeItems: "center", margin: "0 auto 14px" }}><Icon name="lock" size={26} /></div>
          <h2>The final task opens after the modules</h2>
          <p>{view.lockedReason}</p>
          <Link className="btn primary" href={`/academy/courses/${courseId}`}>Back to the course</Link>
        </section>
      ) : null}

      {status === "submitted" ? (
        <div className="notice" style={{ marginTop: 18 }} data-testid="final-submitted">
          <Icon name="clock" size={18} />
          <span><b>Submitted{view.submission.submittedAt ? ` on ${todayLabel(view.submission.submittedAt)}` : ""}, waiting for review.</b> Your answers are shown below and can no longer be changed.</span>
        </div>
      ) : null}

      {status === "passed" || status === "not_passed" ? (
        <section className="card panel" style={{ marginTop: 18 }} data-testid="final-marked">
          <div className="asc">
            <div className={`ascore ${status === "passed" ? "ok" : "no"}`}>
              <b>{view.submission.score}</b>
              <small>of {view.outOf}</small>
            </div>
            <div>
              <h2 style={{ margin: 0 }}>{status === "passed" ? "You passed the final task" : "Not passed yet"}</h2>
              <p style={{ color: "var(--muted)", marginTop: 6 }}>
                {status === "passed" ? "Your work met the pass mark." : `You need ${view.passMark} of ${view.outOf}. Read the feedback, change your work and submit again.`}
              </p>
            </div>
          </div>
          <div className="tablewrap" style={{ marginTop: 14 }}>
            <table>
              <thead><tr><th>Criterion</th><th>Marks</th></tr></thead>
              <tbody>{view.submission.scores.map((score) => <tr key={score.criterion}><td>{score.criterion}</td><td>{score.marks} of {score.outOf}</td></tr>)}</tbody>
            </table>
          </div>
          {view.submission.feedback ? <div className="qfb" style={{ marginTop: 14 }}><b>Feedback.</b> {view.submission.feedback}</div> : null}
          {status === "passed" ? <div className="asc-actions"><Link className="btn primary" href="/academy/certificates">View certificate <Icon name="award" size={16} /></Link></div> : null}
        </section>
      ) : null}

      <section className="card panel" style={{ marginTop: 18 }}>
        <h2>How it is marked</h2>
        <div className="tablewrap">
          <table>
            <thead><tr><th>Criterion</th><th>Marks</th><th>What full marks looks like</th></tr></thead>
            <tbody>{view.rubric.map((row) => <tr key={row.criterion}><td>{row.criterion}</td><td>{row.marks}</td><td>{row.distinction}</td></tr>)}</tbody>
          </table>
        </div>
      </section>

      {!view.locked ? (
        <>
          <section className="card panel" style={{ marginTop: 18 }}>
            <h2>Your canvas</h2>
            <p style={{ color: "var(--muted)", margin: "4px 0 12px" }}>Answer each box in a few clear sentences.</p>
            {view.canvasBoxes.map((label, index) => (
              <label key={index} className="field">
                {index + 1}. {label}
                <textarea
                  value={canvas[index] ?? ""}
                  disabled={closed}
                  maxLength={1500}
                  onChange={(event) => setCanvas((current) => current.map((value, i) => (i === index ? event.target.value : value)))}
                  aria-label={`Canvas box ${index + 1}: ${label}`}
                />
              </label>
            ))}
          </section>

          <section className="card panel" style={{ marginTop: 18 }}>
            <h2>Your proposal</h2>
            <label className="field">
              Write the proposal in your own words
              <textarea value={proposal} disabled={closed} onChange={(event) => setProposal(event.target.value)} style={{ minHeight: 320 }} aria-label="Proposal" />
              <small data-testid="word-count">{count} words{view.minWords ? `, between ${view.minWords} and ${view.maxWords}` : ""}</small>
            </label>
            {formError ? <div className="notice" role="alert" style={{ marginBottom: 12 }}><Icon name="x" size={18} /><span>{formError}</span></div> : null}
            {!closed ? (
              <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
                <button type="button" className="btn ghost" disabled={busy} onClick={() => void saveDraft()}>Save draft</button>
                <button type="button" className="btn primary" disabled={busy} onClick={() => void submit()} data-testid="submit-final">{busy ? "Working..." : "Submit for marking"} <Icon name="arrowR" size={16} /></button>
              </div>
            ) : null}
          </section>
        </>
      ) : null}
    </>
  );
}
