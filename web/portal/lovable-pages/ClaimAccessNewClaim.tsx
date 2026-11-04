"use client";

import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { AlertTriangle } from "lucide-react";
import iipLogo from "@/assets/iip-logo";
import {
  type ClientPortalSession,
  type MotorFnolDetails,
  createClaim,
  fetchClaimantClaimTypes,
  loadStoredSession,
  storeSession,
} from "@/lib/clientPortal";
import { fetchClaimantFieldForm, missingRequired, type ClaimFieldForm } from "@/lib/claimFields";
import { ClaimantNewClaimForm } from "@/components/iip/ClaimantNewClaimForm";
import { fetchKnownDetails, type RememberedValue } from "@/lib/customerMemory";

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
  // The claim types this insurer configured for the policy's product. Null means none are
  // configured (or they could not be read), so the built-in list is used.
  const [configuredClaimTypes, setConfiguredClaimTypes] = useState<string[] | null>(null);
  // The insurer's own questions for this claim type. When it has configured them they replace the
  // built-in vehicle questions; otherwise the built-in ones stay.
  const [fieldForm, setFieldForm] = useState<ClaimFieldForm | null>(null);
  const [fieldValues, setFieldValues] = useState<Record<string, string>>({});
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
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

  useEffect(() => {
    if (!session) return;
    let cancelled = false;
    void fetchClaimantClaimTypes(session, productLine).then((options) => {
      if (cancelled) return;
      const names = options?.configured ? options.claimTypes.map((type) => type.name) : null;
      setConfiguredClaimTypes(names);
      if (names && names.length > 0) setClaimType(names[0]);
    });
    return () => { cancelled = true; };
  }, [session, productLine]);

  useEffect(() => {
    if (!session) return;
    let cancelled = false;
    void fetchClaimantFieldForm(session, productLine, claimType).then((result) => {
      if (!cancelled) setFieldForm(result.ok ? result.value : null);
    });
    return () => { cancelled = true; };
  }, [session, productLine, claimType]);

  // Details this customer gave on an earlier claim are put in the form to check, never submitted without them seeing them.
  const [remembered, setRemembered] = useState<RememberedValue[]>([]);
  useEffect(() => {
    if (!session) return;
    let cancelled = false;
    void fetchKnownDetails(session, productLine, claimType).then((found) => {
      if (cancelled) return;
      setRemembered(found);
      setFieldValues((current) => {
        const next = { ...current };
        for (const value of found) if (!next[value.key]) next[value.key] = value.value;
        return next;
      });
      const registration = found.find((value) => value.key === "vehicleRegistration");
      if (registration) setVehicleRegistration((current) => current || registration.value);
    });
    return () => { cancelled = true; };
  }, [session, productLine, claimType]);

  if (!session) return null;

  const useConfiguredFields = Boolean(fieldForm?.configured && fieldForm.fields.length > 0);
  const productLineSupported = SUPPORTED_PRODUCT_LINES.includes(productLine);
  const claimTypeOptions = configuredClaimTypes ?? (productLine === "Medical" ? MEDICAL_CLAIM_TYPES : MOTOR_CLAIM_TYPES);

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    if (!session || !incidentDate || !incidentDescription.trim()) {
      setError("Enter the incident date and a description of what happened.");
      return;
    }

    if (useConfiguredFields && fieldForm) {
      const missing = missingRequired(fieldForm.fields, fieldValues);
      if (missing.length > 0) {
        setError(`Please answer: ${missing.join(", ")}.`);
        return;
      }
    }

    setSubmitting(true);
    setError(null);
    setFieldErrors({});
    const motorFnolDetails: MotorFnolDetails | undefined =
      productLine === "Motor" && !useConfiguredFields
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
      fieldValues: useConfiguredFields && fieldForm
        ? Object.fromEntries(fieldForm.fields.filter((field) => (fieldValues[field.key] ?? "").trim()).map((field) => [field.key, fieldValues[field.key].trim()]))
        : undefined,
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
        <div className="mx-auto flex max-w-2xl items-center justify-between gap-3 px-6 py-4">
          <div className="flex items-center gap-3">
            <span className="grid h-9 w-9 place-items-center rounded-dls-control border border-dls-border-subtle bg-dls-surface">
              <img src={iipLogo.url} alt="IIP" className="h-7 w-7 object-contain" />
            </span>
            <div>
              <div className="text-sm font-semibold">Start your claim</div>
              <div className="text-xs text-dls-text-muted">Policy {session.policyNumber}</div>
            </div>
          </div>
          <a
            href="/access/policy"
            className="dls-focus-outline inline-flex min-h-9 items-center rounded-dls-control border border-dls-border-input bg-dls-surface px-3 py-1.5 text-xs font-semibold text-dls-text-secondary hover:bg-dls-bg-page"
          >
            My policy
          </a>
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
          <>
          {remembered.length > 0 && (
            <p className="mb-3 rounded-dls-card border border-dls-border-subtle bg-dls-bg-page p-3 text-xs leading-5 text-dls-text-secondary" data-testid="remembered-notice">
              We filled in {remembered.map((value) => `${value.label.toLowerCase()} (from claim ${value.fromClaim})`).join(", ")} from your earlier claim. Check them and change anything that is different this time.
            </p>
          )}
          <ClaimantNewClaimForm
            policyNumber={session.policyNumber}
            productLine={productLine}
            claimType={claimType}
            claimTypeOptions={claimTypeOptions}
            onClaimTypeChange={setClaimType}
            incidentDate={incidentDate}
            onIncidentDateChange={setIncidentDate}
            incidentDescription={incidentDescription}
            onIncidentDescriptionChange={setIncidentDescription}
            fieldForm={fieldForm}
            fieldValues={fieldValues}
            fieldErrors={fieldErrors}
            onFieldChange={(key, value) => setFieldValues((current) => ({ ...current, [key]: value }))}
            motor={{ vehicleRegistration, accidentLocation, thirdPartyInvolved, injuryIndicator }}
            onMotorChange={(patch) => {
              if (patch.vehicleRegistration !== undefined) setVehicleRegistration(patch.vehicleRegistration);
              if (patch.accidentLocation !== undefined) setAccidentLocation(patch.accidentLocation);
              if (patch.thirdPartyInvolved !== undefined) setThirdPartyInvolved(patch.thirdPartyInvolved);
              if (patch.injuryIndicator !== undefined) setInjuryIndicator(patch.injuryIndicator);
            }}
            submitting={submitting}
            error={error}
            onSubmit={onSubmit}
          />
          </>
        )}
      </div>
    </main>
  );
}
