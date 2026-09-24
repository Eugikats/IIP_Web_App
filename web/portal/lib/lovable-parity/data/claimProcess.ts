"use client";

import { useCallback, useEffect, useState } from "react";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";

export type ClaimProcessStage = {
  stageKey: string;
  name: string;
  ownerRole: string;
  isAutomatic: boolean;
  sortOrder: number;
  serviceStandardWorkingDays: number | null;
};

export type ClaimProcess = {
  configured: boolean;
  productName: string | null;
  productVersion: number | null;
  stages: ClaimProcessStage[];
};

const EMPTY: ClaimProcess = { configured: false, productName: null, productVersion: null, stages: [] };

// Mirrors DefaultClaimStageLibrary.Default() (Domain/Products) so the visual-parity fixture matches
// what a real Motor product looks like once outcome A's stage editor has been used to publish one.
const PARITY_MOTOR_PROCESS: ClaimProcess = {
  configured: true,
  productName: "Motor Comprehensive",
  productVersion: 3,
  stages: [
    { stageKey: "intimation_and_reference", name: "Claim intimation and reference", ownerRole: "Claims Officer", isAutomatic: true, sortOrder: 1, serviceStandardWorkingDays: null },
    { stageKey: "acknowledgement_and_document_request", name: "Acknowledgement and document request", ownerRole: "Claims Officer", isAutomatic: true, sortOrder: 2, serviceStandardWorkingDays: 3 },
    { stageKey: "assessor_or_site_visit", name: "Assessor or site visit appointment", ownerRole: "Assessor", isAutomatic: false, sortOrder: 3, serviceStandardWorkingDays: 3 },
    { stageKey: "adjustment_or_investigation_report", name: "Adjustment or investigation report", ownerRole: "Adjuster", isAutomatic: false, sortOrder: 4, serviceStandardWorkingDays: null },
    { stageKey: "offer_or_repudiation", name: "Settlement offer or repudiation", ownerRole: "Claims Officer", isAutomatic: false, sortOrder: 5, serviceStandardWorkingDays: 5 },
    { stageKey: "discharge_voucher", name: "Discharge voucher", ownerRole: "Claims Officer", isAutomatic: false, sortOrder: 6, serviceStandardWorkingDays: null },
    { stageKey: "payment", name: "Payment", ownerRole: "Finance", isAutomatic: false, sortOrder: 7, serviceStandardWorkingDays: 10 },
    { stageKey: "complaint_escalation", name: "Complaint escalation", ownerRole: "Claims Supervisor", isAutomatic: false, sortOrder: 8, serviceStandardWorkingDays: null },
  ],
};

export function useClaimProcess(claimId: string, role: string) {
  const [process, setProcess] = useState<ClaimProcess>(EMPTY);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!claimId) return;
    setLoading(true);
    setError(null);
    if (LOVABLE_VISUAL_PARITY_MODE) {
      setProcess(claimId === "CLM-1071" ? EMPTY : PARITY_MOTOR_PROCESS);
      setLoading(false);
      return;
    }
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/cases/${claimId}/product-stages`, { headers: scopedHeaders(role, "") });
      if (!response.ok) throw new Error("This claim's process could not be loaded.");
      setProcess((await response.json()) as ClaimProcess);
    } catch (err) {
      setError(err instanceof Error ? err.message : "This claim's process could not be loaded.");
      setProcess(EMPTY);
    } finally {
      setLoading(false);
    }
  }, [claimId, role]);

  useEffect(() => { void load(); }, [load]);

  return { process, loading, error, retry: load };
}
