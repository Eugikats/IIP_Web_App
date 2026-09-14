"use client";

import { useEffect, useId, useState } from "react";
import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { AlertTriangle, ArrowRight, Check, Loader2, ShieldCheck } from "lucide-react";
import iipLogo from "@/assets/iip-logo";
import { Input, Select, Textarea, Field as UIField } from "@/components/ui/Input";
import {
  type ClientPortalSession,
  type MotorFnolDetails,
  createClaim,
  loadStoredSession,
  storeSession,
} from "@/lib/clientPortal";

const MOTOR_CLAIM_TYPES = ["Own damage", "Third-party property", "Theft", "Bodily injury"];
const MEDICAL_CLAIM_TYPES = ["Medical reimbursement", "Hospitalisation", "Outpatient treatment"];
const SUPPORTED_PRODUCT_LINES = ["Motor", "Medical"];

// Claimant-facing (tier 6, stricter rules). Confirmed: this screen only ever shows the
// claimant's own policy number and what they themselves type; no staff-only content is in scope.
export default function ClaimAccessNewClaim() {
  const router = useRouter();
  const [session, setSession] = useState<ClientPortalSession | null>(null);
  const [productLine, setProductLine] = useState<string>("Motor");
  const [claimType, setClaimType] = useState(MOTOR_CLAIM_TYPES[0]);
  const [incidentDate, setIncidentDate] = useState("");
  const [incidentDescription, setIncidentDescription] = useState("");
  const [vehicleRegistration, setVehicleRegistration] = useState("");
  const [accidentLocation, setAccidentLocation] = useState("");
  const [thirdPartyInvolved, setThirdPartyInvolved] = useState<"Yes" | "No" | "">("");
  const [injuryIndicator, setInjuryIndicator] = useState<"Yes" | "No" | "">("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const claimTypeId = useId();
  const incidentDateId = useId();
  const incidentDescriptionId = useId();
  const vehicleRegistrationId = useId();
  const accidentLocationId = useId();
  const thirdPartyId = useId();
  const injuryId = useId();

  useEffect(() => {
    const stored = loadStoredSession();
    if (!stored) {
      router.replace("/access");
      return;
    }
    if (stored.caseId) {
      router.replace("/access/claim");
      return;
    }
    setSession(stored);
    if (stored.productLine) {
      setProductLine(stored.productLine);
      setClaimType(stored.productLine === "Medical" ? MEDICAL_CLAIM_TYPES[0] : MOTOR_CLAIM_TYPES[0]);
    }
  }, [router]);

  if (!session) return null;

  const productLineSupported = SUPPORTED_PRODUCT_LINES.includes(productLine);
  const claimTypeOptions = productLine === "Medical" ? MEDICAL_CLAIM_TYPES : MOTOR_CLAIM_TYPES;

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    if (!session || !incidentDate || !incidentDescription.trim()) {
      setError("Enter the incident date and a description of what happened.");
      return;
    }

    setSubmitting(true);
    setError(null);
    const motorFnolDetails: MotorFnolDetails | undefined =
      productLine === "Motor"
        ? {
            vehicleRegistration: vehicleRegistration.trim() || undefined,
            accidentLocation: accidentLocation.trim() || undefined,
            thirdPartyInvolved: thirdPartyInvolved || undefined,
            injuryIndicator: injuryIndicator || undefined,
          }
        : undefined;

    const result = await createClaim(session, {
      productLine,
      claimType,
      incidentDate,
      incidentDescription: incidentDescription.trim(),
      motorFnolDetails,
    });
    setSubmitting(false);
    if (!result.ok) {
      setError(result.error);
      return;
    }

    storeSession({ ...session, caseId: result.caseId });
    router.replace("/access/claim");
  }

  return (
    <main className="min-h-screen bg-dls-bg-page text-dls-text-primary">
      <header className="border-b border-dls-border-subtle bg-dls-surface">
        <div className="mx-auto flex max-w-2xl items-center gap-3 px-6 py-4">
          <span className="grid h-9 w-9 place-items-center rounded-dls-control border border-dls-border-subtle bg-dls-surface">
            <img src={iipLogo.url} alt="IIP" className="h-7 w-7 object-contain" />
          </span>
          <div>
            <div className="text-sm font-semibold">Start your claim</div>
            <div className="text-xs text-dls-text-muted">Policy {session.policyNumber}</div>
          </div>
        </div>
      </header>

      <div className="mx-auto max-w-2xl px-6 py-8">
        {!productLineSupported ? (
          <div className="rounded-dls-card bg-dls-warning-tint p-6 text-center">
            <AlertTriangle className="mx-auto h-6 w-6 text-dls-warning-text" aria-hidden="true" />
            <p className="mt-3 text-sm leading-6 text-dls-text-secondary">
              Online claim submission isn&apos;t available yet for this policy&apos;s product line. Please contact
              us directly and we&apos;ll help you file this claim.
            </p>
          </div>
        ) : (
          <form onSubmit={onSubmit} className="space-y-5">
            <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-6">
              <div className="mb-4 flex items-start gap-3 border-b border-dls-border-subtle pb-4">
                <ShieldCheck className="mt-0.5 h-5 w-5 shrink-0 text-dls-success-text" aria-hidden="true" />
                <div>
                  <div className="text-sm font-semibold text-dls-text-primary">Verified policyholder</div>
                  <div className="mt-1 text-xs leading-5 text-dls-text-muted">
                    We&apos;ve matched your access link to policy {session.policyNumber} ({productLine}). Tell us
                    what happened and we&apos;ll open your claim.
                  </div>
                </div>
              </div>

              <UIField label="Claim type" htmlFor={claimTypeId}>
                <Select id={claimTypeId} value={claimType} onChange={(event) => setClaimType(event.target.value)} className="w-full">
                  {claimTypeOptions.map((option) => (
                    <option key={option} value={option}>{option}</option>
                  ))}
                </Select>
              </UIField>

              <div className="mt-5">
                <UIField label="Date of incident" htmlFor={incidentDateId}>
                  <Input
                    id={incidentDateId}
                    type="date"
                    value={incidentDate}
                    onChange={(event) => setIncidentDate(event.target.value)}
                    max={new Date().toISOString().slice(0, 10)}
                  />
                </UIField>
              </div>

              <div className="mt-5">
                <UIField label="What happened?" htmlFor={incidentDescriptionId}>
                  <Textarea
                    id={incidentDescriptionId}
                    value={incidentDescription}
                    onChange={(event) => setIncidentDescription(event.target.value)}
                    rows={4}
                    placeholder="Describe the incident in your own words."
                  />
                </UIField>
              </div>
            </div>

            {productLine === "Motor" && (
              <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-6">
                <div className="mb-4 text-xs font-bold uppercase tracking-[0.12em] text-dls-text-muted">Vehicle details</div>

                <UIField label="Vehicle registration" htmlFor={vehicleRegistrationId}>
                  <Input
                    id={vehicleRegistrationId}
                    value={vehicleRegistration}
                    onChange={(event) => setVehicleRegistration(event.target.value)}
                    placeholder="e.g. UBA 123X"
                  />
                </UIField>

                <div className="mt-5">
                  <UIField label="Where did it happen?" htmlFor={accidentLocationId}>
                    <Input
                      id={accidentLocationId}
                      value={accidentLocation}
                      onChange={(event) => setAccidentLocation(event.target.value)}
                      placeholder="e.g. Jinja Road, near the roundabout"
                    />
                  </UIField>
                </div>

                <div className="mt-5 grid grid-cols-2 gap-4">
                  <UIField label="Was a third party involved?" htmlFor={thirdPartyId}>
                    <Select id={thirdPartyId} value={thirdPartyInvolved} onChange={(event) => setThirdPartyInvolved(event.target.value as "Yes" | "No" | "")} className="w-full">
                      <option value="">Not sure</option>
                      <option value="Yes">Yes</option>
                      <option value="No">No</option>
                    </Select>
                  </UIField>
                  <UIField label="Was anyone injured?" htmlFor={injuryId}>
                    <Select id={injuryId} value={injuryIndicator} onChange={(event) => setInjuryIndicator(event.target.value as "Yes" | "No" | "")} className="w-full">
                      <option value="">Not sure</option>
                      <option value="Yes">Yes</option>
                      <option value="No">No</option>
                    </Select>
                  </UIField>
                </div>
              </div>
            )}

            {/* Full-width, taller, trailing-icon submit, hand-styled to Button's primary
                variant, same reason as FNOL.tsx's "Next" and /access's submit button. */}
            <button
              type="submit"
              disabled={submitting}
              className="dls-focus-outline inline-flex min-h-12 w-full items-center justify-center gap-3 rounded-dls-control bg-dls-primary px-5 py-3 text-sm font-bold text-dls-text-inverse shadow-dls-primary transition-colors duration-dls-fast ease-dls hover:bg-dls-primary-hover disabled:cursor-not-allowed disabled:opacity-60"
            >
              {submitting ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <Check className="h-4 w-4" aria-hidden="true" />}
              <span>{submitting ? "Submitting your claim" : "Submit claim"}</span>
              {!submitting && <ArrowRight className="h-4 w-4" aria-hidden="true" />}
            </button>

            {error && (
              <div role="alert" className="rounded-dls-control bg-dls-danger-tint px-3 py-3 text-xs leading-5 text-dls-danger-text">
                {error}
              </div>
            )}
            <p className="text-center text-xs leading-5 text-dls-text-muted">
              After you submit, you can upload supporting documents and track progress from the same link.
            </p>
          </form>
        )}
      </div>
    </main>
  );
}
