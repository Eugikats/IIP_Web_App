"use client";

import { useEffect, useState } from "react";
import { StatusPill, type PillTone } from "@/components/ui/StatusPill";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

type Consent = { state: "NotGiven" | "Given" | "Withdrawn" | "NoticeChanged"; recordedAt: string | null; consentedVersion: string | null };

const PRESENTATION: Record<Consent["state"], { tone: PillTone; label: string; note: string }> = {
  Given: { tone: "success", label: "Agreed", note: "The customer agreed to the privacy notice and can send documents." },
  NotGiven: { tone: "warning", label: "Not yet agreed", note: "The customer has not agreed to the privacy notice, so documents cannot be sent from the customer page yet." },
  Withdrawn: { tone: "danger", label: "Withdrawn", note: "The customer withdrew their permission. New documents are not accepted from the customer page until they agree again." },
  NoticeChanged: { tone: "warning", label: "Notice changed", note: "The privacy notice changed after the customer agreed. They are asked to agree again before sending more documents." },
};

/**
 * One line for staff on a claim: has the customer agreed to the privacy notice that lets them send
 * documents (build-loop item 9)? Shown as a status pill with a plain sentence, never colour alone.
 */
export function PrivacyConsentLine({ caseId, role }: { caseId: string; role: string }) {
  const [consent, setConsent] = useState<Consent | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let cancelled = false;
    fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/privacy-consent`, { headers: scopedHeaders(role, ""), cache: "no-store" })
      .then(async (response) => {
        if (cancelled) return;
        if (!response.ok) return setFailed(true);
        setConsent((await response.json()) as Consent);
      })
      .catch(() => { if (!cancelled) setFailed(true); });
    return () => { cancelled = true; };
  }, [caseId, role]);

  if (failed) return null;
  if (!consent) return null;
  const view = PRESENTATION[consent.state];
  return (
    <div className="flex flex-wrap items-center gap-2 text-[12.5px] text-dls-text-secondary" data-testid="privacy-consent-line">
      <span className="font-semibold text-dls-text-primary">Privacy notice</span>
      <StatusPill tone={view.tone} label={view.label} />
      <span>{view.note}</span>
    </div>
  );
}
