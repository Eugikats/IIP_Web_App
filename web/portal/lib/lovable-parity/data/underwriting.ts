"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";

export type UnderwritingCheckState = "Pass" | "Attention" | "MissingInformation";

export type UnderwritingCheckView = {
  checkId: string;
  label: string;
  state: UnderwritingCheckState;
  detail: string;
};

export type UnderwritingReviewView = {
  submissionId: string;
  applicant: string;
  checks: UnderwritingCheckView[];
  draftedQuestions: string[];
};

type UnderwritingReviewDto = {
  submissionId: string;
  applicant: string;
  checks: { checkId: string; label: string; state: string; detail: string }[];
  draftedQuestions: string[];
};

const PARITY_FIXTURES: UnderwritingReviewView[] = [
  {
    submissionId: "SUB-7701",
    applicant: "Katonga Logistics Ltd",
    checks: [
      { checkId: "UW-COMPLETENESS", label: "Submission completeness", state: "MissingInformation", detail: "2 required items missing: fleet schedule, loss history." },
      { checkId: "UW-LIMIT", label: "Sum-insured limit", state: "Attention", detail: "Sum insured UGX 420,000,000 exceeds the senior-referral threshold. Refer to a senior underwriter." },
      { checkId: "UW-PRODUCT", label: "Product-line risk", state: "Attention", detail: "Fleet Comprehensive is a higher-risk line — confirm survey/inspection requirements before quoting." },
    ],
    draftedQuestions: ["Could you please provide the fleet schedule for submission SUB-7701?", "Could you please provide the loss history for submission SUB-7701?"],
  },
  {
    submissionId: "SUB-7702",
    applicant: "Amina Yusuf",
    checks: [
      { checkId: "UW-COMPLETENESS", label: "Submission completeness", state: "Pass", detail: "All required intake items are present." },
      { checkId: "UW-LIMIT", label: "Sum-insured limit", state: "Pass", detail: "Sum insured UGX 65,000,000 is within standard authority." },
    ],
    draftedQuestions: [],
  },
];

function mapDto(dto: UnderwritingReviewDto): UnderwritingReviewView {
  return {
    submissionId: dto.submissionId,
    applicant: dto.applicant,
    checks: dto.checks.map((c) => ({
      checkId: c.checkId,
      label: c.label,
      state: (c.state as UnderwritingCheckState) ?? "Pass",
      detail: c.detail,
    })),
    draftedQuestions: dto.draftedQuestions,
  };
}

export function useUnderwritingReviews() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [reviews, setReviews] = useState<UnderwritingReviewView[]>(LOVABLE_VISUAL_PARITY_MODE ? PARITY_FIXTURES : []);
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) return;
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/underwriting/reviews`, { headers: scopedHeaders(role, "") });
      if (!response.ok) throw new Error(`Underwriting reviews failed with HTTP ${response.status}.`);
      const rows = (await response.json()) as UnderwritingReviewDto[];
      setReviews(rows.map(mapDto));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Underwriting reviews failed to load.");
      setReviews([]);
    } finally {
      setLoading(false);
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  return { reviews, loading, error, parityMode: LOVABLE_VISUAL_PARITY_MODE };
}
