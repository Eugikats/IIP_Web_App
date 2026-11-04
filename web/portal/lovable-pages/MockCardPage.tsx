"use client";

import { useState } from "react";
import { useSearchParams } from "next/navigation";
import { AuthFrame, FormError, FormNotice } from "@/components/iip/AuthFrame";
import { Button } from "@/components/ui/Button";
import { settleMockPayment } from "@/lib/billingPlans";
import { canUseLocalTestDoubles } from "@/lib/config";

/** A pretend card page for developer runtimes: it answers the pretend payment the way a card gateway's own page would. No card is read or charged. */
export default function MockCardPage() {
  const reference = useSearchParams().get("reference") ?? "";
  const [done, setDone] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  if (!canUseLocalTestDoubles) {
    return <AuthFrame title="This page is not available" intro="Card payments are made on the card gateway's own page."><span /></AuthFrame>;
  }

  async function answer(outcome: "succeeded" | "failed") {
    setError(null);
    const result = await settleMockPayment("Admin", reference, outcome);
    if (!result.ok) return setError(result.error);
    setDone(outcome === "succeeded" ? "Payment approved." : "Payment declined.");
  }

  return (
    <AuthFrame title="Pretend card page" intro="This stands in for a card gateway in a developer runtime. No card is read and nothing is charged.">
      {done ? (
        <FormNotice message={`${done} Go back to IIP billing to see the result.`} testId="mock-card-done" />
      ) : (
        <div className="space-y-3">
          <FormError message={error} />
          <Button variant="primary" onClick={() => void answer("succeeded")} disabled={!reference}>Approve the payment</Button>
          <Button variant="ghost" onClick={() => void answer("failed")} disabled={!reference}>Decline</Button>
        </div>
      )}
    </AuthFrame>
  );
}
