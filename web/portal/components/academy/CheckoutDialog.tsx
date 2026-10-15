"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Icon } from "@/components/academy/Icon";
import { useAcademy } from "@/components/academy/AcademyProvider";
import { academyPost, AcademyApiError } from "@/lib/academy/api";
import { formatPrice } from "@/lib/academy/format";
import type { CourseSummary } from "@/lib/academy/types";

/**
 * The enrol dialog, in the design's look. Payment is not connected, so this does not pretend to take any: it says so, and
 * enrolment is open while that is true. When a payment provider is connected, the choices of how to pay appear here.
 */
export function CheckoutDialog({ course }: { course: Pick<CourseSummary, "id" | "title" | "hue" | "icon" | "lessons" | "price"> }) {
  const { closeModal, showModal, refreshLearner, toast } = useAcademy();
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);

  async function enrol() {
    setBusy(true);
    setProblem(null);
    try {
      await academyPost(`/courses/${course.id}/enrol`);
      await refreshLearner();
      showModal(
        <>
          <div className="seal ok"><Icon name="check" size={40} /></div>
          <h2>You&apos;re enrolled!</h2>
          <p><b>{course.title}</b> is now unlocked.</p>
          <div className="btns">
            <button type="button" className="btn primary" onClick={() => { closeModal(); router.push(`/academy/learn/${course.id}`); }}>Start learning <Icon name="arrowR" size={16} /></button>
            <button type="button" className="btn ghost" onClick={closeModal}>Later</button>
          </div>
        </>,
      );
    } catch (failure) {
      setProblem(failure instanceof AcademyApiError ? failure.message : "You could not be enrolled. Try again in a moment.");
      if (failure instanceof AcademyApiError && failure.status === 401) toast("Sign in to enrol in a course", "user");
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <h2>Enrol in this course</h2>
      <div className="ckc">
        <div className="mini" style={{ background: `linear-gradient(135deg,${course.hue[0]},${course.hue[1]})` }}><Icon name={course.icon} size={22} /></div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <b>{course.title}</b>
          <small>{course.lessons} lessons · Certificate on completion</small>
        </div>
      </div>
      <div className="notice blue" style={{ marginBottom: 14 }}>
        <Icon name="card" size={18} />
        <span>Payment is not connected yet, so no payment is taken and nothing is charged. Enrolment is open while that is true. The listed price is UGX {formatPrice(course.price)}.</span>
      </div>
      <div className="cktotal"><span>Due today</span><b>UGX 0</b></div>
      {problem ? <p role="alert" style={{ color: "var(--red)", fontSize: 13.5, margin: "8px 0" }}>{problem}</p> : null}
      <button type="button" className="btn primary block" onClick={() => void enrol()} disabled={busy} data-testid="confirm-enrol">
        {busy ? "Enrolling" : "Enrol now"}
      </button>
      <button type="button" className="btn ghost block" style={{ marginTop: 10 }} onClick={closeModal}>Not now</button>
    </>
  );
}
