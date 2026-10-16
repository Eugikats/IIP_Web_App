"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Icon } from "@/components/academy/Icon";
import { useAcademy } from "@/components/academy/AcademyProvider";
import { academyGet, academyPost, AcademyApiError } from "@/lib/academy/api";
import { formatMoney } from "@/lib/academy/format";
import type { CourseSummary } from "@/lib/academy/types";

type Offer = {
  courseId: string;
  priceUsd: number;
  free: boolean;
  configured: boolean;
  enrolled: boolean;
  taxRatePercent: number;
  choices: { method: "card" | "mobile_money"; currency: string; amount: number; label: string }[];
};

/**
 * The enrol dialog, in the design's look. With a payment provider set up, the person chooses card or mobile money and is
 * sent to the provider's own page to pay; the amount comes from the server. With none set up, it says so plainly and
 * enrolment is open (nothing is charged).
 */
export function CheckoutDialog({ course }: { course: Pick<CourseSummary, "id" | "title" | "hue" | "icon" | "lessons" | "price" | "currency"> }) {
  const { closeModal, showModal, refreshLearner, toast } = useAcademy();
  const router = useRouter();
  const [offer, setOffer] = useState<Offer | null>(null);
  const [method, setMethod] = useState<"card" | "mobile_money">("mobile_money");
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    academyGet<Offer>(`/courses/${course.id}/offer`)
      .then((result) => { if (!cancelled) setOffer(result); })
      .catch((failure) => { if (!cancelled) setProblem(failure instanceof AcademyApiError ? failure.message : "The price could not be loaded."); });
    return () => { cancelled = true; };
  }, [course.id]);

  const enrolled = () => {
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
  };

  async function enrolOpen() {
    setBusy(true);
    setProblem(null);
    try {
      await academyPost(`/courses/${course.id}/enrol`);
      await refreshLearner();
      enrolled();
    } catch (failure) {
      setProblem(failure instanceof AcademyApiError ? failure.message : "You could not be enrolled. Try again in a moment.");
      if (failure instanceof AcademyApiError && failure.status === 401) toast("Sign in to enrol in a course", "user");
    } finally {
      setBusy(false);
    }
  }

  async function pay() {
    setBusy(true);
    setProblem(null);
    try {
      const result = await academyPost<{ url: string }>(`/courses/${course.id}/checkout`, { method });
      window.location.assign(result.url);
    } catch (failure) {
      setProblem(failure instanceof AcademyApiError ? failure.message : "The payment page could not be opened. Try again in a moment.");
      setBusy(false);
    }
  }

  const header = (
    <>
      <h2>Enrol in this course</h2>
      <div className="ckc">
        <div className="mini" style={{ background: `linear-gradient(135deg,${course.hue[0]},${course.hue[1]})` }}><Icon name={course.icon} size={22} /></div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <b>{course.title}</b>
          <small>{course.lessons} lessons · Certificate on completion</small>
        </div>
      </div>
    </>
  );

  if (!offer) {
    return (
      <>
        {header}
        {problem ? <p role="alert" style={{ color: "var(--red)", fontSize: 13.5, margin: "8px 0" }}>{problem}</p> : <div className="skel" style={{ height: 120 }} aria-busy="true" aria-label="Loading the price" />}
        <button type="button" className="btn ghost block" style={{ marginTop: 10 }} onClick={closeModal}>Close</button>
      </>
    );
  }

  // Free, or no payment provider yet: enrolment is open.
  if (offer.free || !offer.configured) {
    return (
      <>
        {header}
        <div className="notice blue" style={{ marginBottom: 14 }}>
          <Icon name="card" size={18} />
          <span>
            {offer.free
              ? "This course is free."
              : `Payment is not connected yet, so no payment is taken and nothing is charged. Enrolment is open while that is true. The listed price is ${formatMoney(offer.priceUsd, course.currency)}.`}
          </span>
        </div>
        <div className="cktotal"><span>Due today</span><b>{formatMoney(0, course.currency)}</b></div>
        {problem ? <p role="alert" style={{ color: "var(--red)", fontSize: 13.5, margin: "8px 0" }}>{problem}</p> : null}
        <button type="button" className="btn primary block" onClick={() => void enrolOpen()} disabled={busy} data-testid="confirm-enrol">
          {busy ? "Enrolling" : "Enrol now"}
        </button>
        <button type="button" className="btn ghost block" style={{ marginTop: 10 }} onClick={closeModal}>Not now</button>
      </>
    );
  }

  const choice = offer.choices.find((item) => item.method === method) ?? offer.choices[0];
  return (
    <>
      {header}
      <div className="payopts" role="radiogroup" aria-label="How to pay">
        {offer.choices.map((item) => (
          <button
            key={item.method}
            type="button"
            role="radio"
            aria-checked={method === item.method}
            className={`payopt${method === item.method ? " on" : ""}`}
            onClick={() => setMethod(item.method)}
            data-testid={`pay-${item.method}`}
          >
            <span className="pl" style={item.method === "card" ? { background: "var(--navy)", color: "#fff" } : { background: "#FFCB05", color: "#1a1a1a" }}>
              {item.method === "card" ? <Icon name="card" size={16} /> : "MoMo"}
            </span>
            <span>
              <b>{item.method === "card" ? "Debit or credit card" : "Mobile money"}</b>
              <small>{item.method === "card" ? "Visa or Mastercard" : "MTN or Airtel, in shillings"}</small>
            </span>
            <span className="rd" />
          </button>
        ))}
      </div>
      {method === "mobile_money" ? <p className="cknote" style={{ textAlign: "left", marginTop: 0 }}>Mobile money is charged in shillings: {formatMoney(offer.priceUsd, "USD")} is {formatMoney(choice.amount, "UGX")} at today&apos;s rate.</p> : null}
      <div className="cktotal"><span>Total{offer.taxRatePercent > 0 ? ` (includes ${offer.taxRatePercent}% tax)` : ""}</span><b>{formatMoney(choice.amount, choice.currency)}</b></div>
      {problem ? <p role="alert" style={{ color: "var(--red)", fontSize: 13.5, margin: "8px 0" }}>{problem}</p> : null}
      <button type="button" className="btn primary block" onClick={() => void pay()} disabled={busy} data-testid="pay-now">
        {busy ? "Opening the payment page" : `Pay ${formatMoney(choice.amount, choice.currency)}`}
      </button>
      <p className="cknote">You pay on the payment provider&apos;s own page. IIP never sees your card or phone details.</p>
      <button type="button" className="btn ghost block" style={{ marginTop: 6 }} onClick={closeModal}>Not now</button>
    </>
  );
}
