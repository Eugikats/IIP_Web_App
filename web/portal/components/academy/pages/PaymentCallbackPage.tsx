"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { Icon } from "@/components/academy/Icon";
import { useAcademy } from "@/components/academy/AcademyProvider";
import { SkeletonCards } from "@/components/academy/PageState";
import { SignInCard } from "@/components/academy/pages/shared";
import { academyPost, AcademyApiError } from "@/lib/academy/api";

type Result = { status: "paid" | "pending" | "failed" | "mismatch" | "refunded"; courseId: string | null; courseTitle: string | null; message: string };

// Where the payment provider sends a person back to. The page never decides whether a payment worked: it asks the server,
// which asks the provider, and shows the answer. A payment that is still being confirmed is checked again for a little while.
export default function PaymentCallbackPage() {
  const params = useSearchParams();
  const { me, loadingMe, refreshLearner } = useAcademy();
  const [result, setResult] = useState<Result | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const started = useRef(false);
  const reference = params.get("tx_ref");
  const transactionId = params.get("transaction_id");
  const returned = params.get("status");

  useEffect(() => {
    if (!me?.signedIn || started.current || !reference) return;
    started.current = true;
    let cancelled = false;
    const check = async (attempt: number) => {
      try {
        const answer = await academyPost<Result>("/payments/confirm", { reference, transactionId });
        if (cancelled) return;
        setResult(answer);
        if (answer.status === "paid") await refreshLearner();
        if (answer.status === "pending" && attempt < 12) setTimeout(() => void check(attempt + 1), 3000);
      } catch (failure) {
        if (!cancelled) setProblem(failure instanceof AcademyApiError ? failure.message : "The payment could not be checked. Try again in a moment.");
      }
    };
    void check(0);
    return () => { cancelled = true; };
  }, [me, reference, transactionId, refreshLearner]);

  if (loadingMe) return <SkeletonCards count={1} />;
  if (!me?.signedIn) return <SignInCard title="Sign in to finish your payment" text="Sign in with the same account you paid with and your course will open." />;

  if (!reference || returned === "cancelled") {
    return (
      <section className="card panel state" data-testid="payment-cancelled">
        <h2>Payment cancelled</h2>
        <p>You were not charged. You can try again whenever you like.</p>
        <Link className="btn primary" href="/academy/courses">Back to courses</Link>
      </section>
    );
  }

  if (problem) {
    return (
      <section className="card panel state" role="alert" data-testid="payment-error">
        <h2>The payment could not be checked</h2>
        <p>{problem}</p>
        <Link className="btn primary" href="/academy/courses">Back to courses</Link>
      </section>
    );
  }

  if (!result || result.status === "pending") {
    return (
      <section className="card panel state" aria-busy="true" data-testid="payment-pending">
        <div className="skel" style={{ height: 56, width: 56, borderRadius: "50%", margin: "0 auto 14px" }} />
        <h2>Confirming your payment</h2>
        <p>This usually takes a few seconds. If you paid by mobile money, approve the request on your phone. You can leave this page: your course opens as soon as the payment is confirmed.</p>
      </section>
    );
  }

  return (
    <section className="card panel state" data-testid={`payment-${result.status}`}>
      <div style={{ width: 56, height: 56, borderRadius: "50%", background: result.status === "paid" ? "#E8F7EE" : "var(--blue-2)", color: result.status === "paid" ? "#0F7A3E" : "var(--blue)", display: "grid", placeItems: "center", margin: "0 auto 14px" }}>
        <Icon name={result.status === "paid" ? "check" : "x"} size={26} />
      </div>
      <h2>{result.status === "paid" ? "You're enrolled" : result.status === "mismatch" ? "We need to check your payment" : "Payment not completed"}</h2>
      <p>{result.message}</p>
      {result.status === "paid" && result.courseId ? <Link className="btn primary" href={`/academy/learn/${result.courseId}`} data-testid="payment-start">Start learning <Icon name="arrowR" size={16} /></Link> : <Link className="btn primary" href="/academy/courses">Back to courses</Link>}
    </section>
  );
}
