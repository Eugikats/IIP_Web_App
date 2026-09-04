"use client";

import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { AlertTriangle, ArrowRight, Check, Loader2, ShieldCheck } from "lucide-react";
import iipLogo from "@/assets/iip-logo";
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
    <main className="min-h-screen bg-mist/40 text-ink">
      <header className="border-b border-line bg-white">
        <div className="mx-auto flex max-w-2xl items-center gap-3 px-6 py-4">
          <span className="grid h-9 w-9 place-items-center rounded-md border border-line bg-white">
            <img src={iipLogo.url} alt="IIP" className="h-7 w-7 object-contain" />
          </span>
          <div>
            <div className="text-sm font-semibold">Start your claim</div>
            <div className="text-xs text-muted">Policy {session.policyNumber}</div>
          </div>
        </div>
      </header>

      <div className="mx-auto max-w-2xl px-6 py-8">
        {!productLineSupported ? (
          <div className="rounded-xl border border-warning/30 bg-warning/8 p-6 text-center">
            <AlertTriangle className="mx-auto h-6 w-6 text-warning" />
            <p className="mt-3 text-sm leading-6 text-ink-soft">
              Online claim submission isn&apos;t available yet for this policy&apos;s product line. Please contact
              us directly and we&apos;ll help you file this claim.
            </p>
          </div>
        ) : (
          <form onSubmit={onSubmit} className="space-y-5">
            <div className="rounded-xl border border-line bg-white p-6">
              <div className="mb-4 flex items-start gap-3 border-b border-line pb-4">
                <ShieldCheck className="mt-0.5 h-5 w-5 shrink-0 text-success" />
                <div>
                  <div className="text-sm font-medium text-ink">Verified policyholder</div>
                  <div className="mt-1 text-xs leading-5 text-muted">
                    We&apos;ve matched your access link to policy {session.policyNumber} ({productLine}). Tell us
                    what happened and we&apos;ll open your claim.
                  </div>
                </div>
              </div>

              <label className="block text-sm font-medium text-ink">
                Claim type
                <select
                  value={claimType}
                  onChange={(event) => setClaimType(event.target.value)}
                  className="mt-2 min-h-11 w-full rounded-md border border-line bg-white px-3 py-2.5 text-sm text-ink focus-ring"
                >
                  {claimTypeOptions.map((option) => (
                    <option key={option} value={option}>{option}</option>
                  ))}
                </select>
              </label>

              <label className="mt-5 block text-sm font-medium text-ink">
                Date of incident
                <input
                  type="date"
                  value={incidentDate}
                  onChange={(event) => setIncidentDate(event.target.value)}
                  max={new Date().toISOString().slice(0, 10)}
                  className="mt-2 min-h-11 w-full rounded-md border border-line bg-white px-3 py-2.5 text-sm text-ink focus-ring"
                />
              </label>

              <label className="mt-5 block text-sm font-medium text-ink">
                What happened?
                <textarea
                  value={incidentDescription}
                  onChange={(event) => setIncidentDescription(event.target.value)}
                  rows={4}
                  placeholder="Describe the incident in your own words."
                  className="mt-2 w-full resize-none rounded-md border border-line bg-white px-3 py-2.5 text-sm text-ink placeholder:text-muted focus-ring"
                />
              </label>
            </div>

            {productLine === "Motor" && (
              <div className="rounded-xl border border-line bg-white p-6">
                <div className="text-xs uppercase tracking-[0.12em] text-muted mb-4">Vehicle details</div>

                <label className="block text-sm font-medium text-ink">
                  Vehicle registration
                  <input
                    value={vehicleRegistration}
                    onChange={(event) => setVehicleRegistration(event.target.value)}
                    placeholder="e.g. UBA 123X"
                    className="mt-2 min-h-11 w-full rounded-md border border-line bg-white px-3 py-2.5 text-sm text-ink placeholder:text-muted focus-ring"
                  />
                </label>

                <label className="mt-5 block text-sm font-medium text-ink">
                  Where did it happen?
                  <input
                    value={accidentLocation}
                    onChange={(event) => setAccidentLocation(event.target.value)}
                    placeholder="e.g. Jinja Road, near the roundabout"
                    className="mt-2 min-h-11 w-full rounded-md border border-line bg-white px-3 py-2.5 text-sm text-ink placeholder:text-muted focus-ring"
                  />
                </label>

                <div className="mt-5 grid grid-cols-2 gap-4">
                  <label className="block text-sm font-medium text-ink">
                    Was a third party involved?
                    <select
                      value={thirdPartyInvolved}
                      onChange={(event) => setThirdPartyInvolved(event.target.value as "Yes" | "No" | "")}
                      className="mt-2 min-h-11 w-full rounded-md border border-line bg-white px-3 py-2.5 text-sm text-ink focus-ring"
                    >
                      <option value="">Not sure</option>
                      <option value="Yes">Yes</option>
                      <option value="No">No</option>
                    </select>
                  </label>
                  <label className="block text-sm font-medium text-ink">
                    Was anyone injured?
                    <select
                      value={injuryIndicator}
                      onChange={(event) => setInjuryIndicator(event.target.value as "Yes" | "No" | "")}
                      className="mt-2 min-h-11 w-full rounded-md border border-line bg-white px-3 py-2.5 text-sm text-ink focus-ring"
                    >
                      <option value="">Not sure</option>
                      <option value="Yes">Yes</option>
                      <option value="No">No</option>
                    </select>
                  </label>
                </div>
              </div>
            )}

            <button
              type="submit"
              disabled={submitting}
              className="inline-flex min-h-12 w-full items-center justify-center gap-3 rounded-md bg-primary px-5 py-3 text-sm font-semibold text-white shadow-l2 transition-colors hover:bg-primary/90 focus-ring disabled:cursor-not-allowed disabled:opacity-55"
            >
              {submitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Check className="h-4 w-4" />}
              <span>{submitting ? "Submitting your claim" : "Submit claim"}</span>
              {!submitting && <ArrowRight className="h-4 w-4" />}
            </button>

            {error && (
              <div role="alert" className="rounded-md border border-danger/25 bg-danger/7 px-3 py-3 text-xs leading-5 text-danger">
                {error}
              </div>
            )}
            <p className="text-xs leading-5 text-muted text-center">
              After you submit, you can upload supporting documents and track progress from the same link.
            </p>
          </form>
        )}
      </div>
    </main>
  );
}
