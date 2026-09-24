"use client";
import { claimReference, humanizeIdentifier, shortReference } from "@/lib/format";

import { useEffect, useId, useMemo, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { toast } from "sonner";
import { AlertTriangle, Check, ChevronLeft, ChevronRight, FileText, Loader2, Search, ShieldAlert, Upload, UserRound } from "lucide-react";
import { GlassCard, SectionHeader, Badge } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Input, Select, Textarea, Field as UIField } from "@/components/ui/Input";
import { FilterTab } from "@/components/ui/FilterTab";
import { LoadingSkeleton, ErrorStateSurface } from "@/components/ui/StateSurfaces";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

// Six-step FNOL intake (docs/03-design-architecture/iip_fnol_six_step_intake_design.md):
// 1 Identify the claimant, 2 Find the policy, 3 Describe the incident, 4 Product-line specifics,
// 5 Upload documents, 6 Review and submit. Four of the six positions are forced by real
// dependencies, not a style choice, see the design doc §3. Product line is derived from the
// policy lookup in step 2, with a manual fallback only if lookup fails, so an officer is never
// asked a question the system could answer itself.
//
// WO-15 heritage: FNOL was hardcoded to Motor, a fixed productLine/claimType, motor-only fields,
// and a hardcoded seven-item document list. The document checklist is never a client constant
// (A1), it's always fetched from GET /api/v1/document-checklist/preview, the same
// DocumentChecklistRules the rest of the platform uses once a case exists.

type ProductLine = "Motor" | "Medical";

type MotorFields = {
  vehicleRegistration: string;
  accidentTime: string;
  accidentLocation: string;
  driverName: string;
  drivingPermitNumber: string;
  thirdPartyInvolved: string;
  injuryIndicator: string;
  policeReportStatus: string;
  preferredCommunicationChannel: string;
  uploadedDocuments: string;
};

type MedicalFields = {
  patientName: string;
  relationshipToPolicyholder: string;
  treatmentDate: string;
  providerName: string;
  admissionType: string;
  treatingDoctor: string;
  diagnosisSummary: string;
  preferredCommunicationChannel: string;
  uploadedDocuments: string;
};

type FnolForm = {
  customerName: string;
  customerContact: string;
  customerEmail: string;
  policyNumber: string;
  claimType: string;
  accidentDate: string;
  incidentDescription: string;
  motor: MotorFields;
  medical: MedicalFields;
};

type CreateCaseResponse = { caseId?: string };
type ChecklistDocumentPreview = { documentType: string; label: string };
type ChecklistPreviewResponse = { requiredDocuments: ChecklistDocumentPreview[]; optionalDocuments: ChecklistDocumentPreview[] };

type DuplicateClaimWarning = { caseId: string; status: string; incidentDate: string | null; createdAt: string };
type PolicyLookupResponse = {
  policyId: string;
  policyNumber: string;
  productLine: string;
  coverType: string | null;
  customerName: string | null;
  customerEmail: string | null;
  customerPhone: string | null;
  effectiveFrom: string | null;
  effectiveTo: string | null;
  isActive: boolean;
  possibleDuplicateClaims: DuplicateClaimWarning[];
};
type PolicyLookupState =
  | { status: "idle" }
  | { status: "loading" }
  | { status: "found"; result: PolicyLookupResponse }
  | { status: "not-found" }
  | { status: "error"; message: string };

const PRODUCT_LINES: { value: ProductLine; label: string; defaultClaimType: string }[] = [
  { value: "Motor", label: "Motor", defaultClaimType: "Own damage" },
  { value: "Medical", label: "Medical", defaultClaimType: "Medical reimbursement" },
];

const MOTOR_CLAIM_TYPES = ["Own damage", "Third-party property", "Theft", "Bodily injury"];
const MEDICAL_CLAIM_TYPES = ["Medical reimbursement", "Hospitalisation", "Outpatient treatment"];

const STEPS = [
  { n: 1, label: "Claimant" },
  { n: 2, label: "Policy" },
  { n: 3, label: "Incident" },
  { n: 4, label: "Details" },
  { n: 5, label: "Documents" },
  { n: 6, label: "Review" },
] as const;

// A conversation handed off from the Channels Inbox carries a preferred channel that should
// also be this claim's preferred communication channel, if the channel offers one.
const CHANNEL_TO_PREFERRED_COMMUNICATION: Record<string, string> = {
  WhatsApp: "WhatsApp", Email: "Email", SMS: "Phone", Broker: "Broker",
};

function buildInitialForm(searchParams: URLSearchParams): FnolForm {
  const handoffChannel = searchParams.get("channel") ?? "";
  const preferredChannel = CHANNEL_TO_PREFERRED_COMMUNICATION[handoffChannel] ?? "Phone";
  return {
    customerName: searchParams.get("customerName") ?? "",
    customerContact: searchParams.get("customerContact") ?? "",
    customerEmail: searchParams.get("customerEmail") ?? "",
    policyNumber: "",
    claimType: PRODUCT_LINES[0].defaultClaimType,
    accidentDate: "",
    incidentDescription: "",
    motor: {
      vehicleRegistration: "",
      accidentTime: "",
      accidentLocation: "",
      driverName: "",
      drivingPermitNumber: "",
      thirdPartyInvolved: "Unknown",
      injuryIndicator: "Unknown",
      policeReportStatus: "Unknown",
      preferredCommunicationChannel: preferredChannel,
      uploadedDocuments: "",
    },
    medical: {
      patientName: "",
      relationshipToPolicyholder: "Self",
      treatmentDate: "",
      providerName: "",
      admissionType: "Outpatient",
      treatingDoctor: "",
      diagnosisSummary: "",
      preferredCommunicationChannel: preferredChannel,
      uploadedDocuments: "",
    },
  };
}

export default function FNOL() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { roleDef } = useUI();
  const [step, setStep] = useState(1);
  const [productLine, setProductLine] = useState<ProductLine>("Motor");
  const [productLineConfirmed, setProductLineConfirmed] = useState(false);
  const [form, setForm] = useState<FnolForm>(() => buildInitialForm(searchParams));
  const handoffChannel = searchParams.get("channel");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [checklist, setChecklist] = useState<ChecklistPreviewResponse | null>(null);
  const [checklistLoading, setChecklistLoading] = useState(false);
  const [checklistError, setChecklistError] = useState<string | null>(null);
  const [lookup, setLookup] = useState<PolicyLookupState>({ status: "idle" });

  function selectProductLine(next: ProductLine) {
    setProductLine(next);
    setForm((current) => ({
      ...current,
      claimType: PRODUCT_LINES.find((line) => line.value === next)!.defaultClaimType,
    }));
  }

  async function runPolicyLookup() {
    const policyNumber = form.policyNumber.trim();
    if (!policyNumber) return;
    setLookup({ status: "loading" });
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/policy-lookup?policyNumber=${encodeURIComponent(policyNumber)}`, {
        headers: scopedHeaders(roleDef.key, ""),
      });
      if (response.status === 404) {
        setLookup({ status: "not-found" });
        return;
      }
      if (!response.ok) throw new Error(`Policy lookup failed.`);
      const result = (await response.json()) as PolicyLookupResponse;
      setLookup({ status: "found", result });
      if (result.productLine === "Motor" || result.productLine === "Medical") {
        selectProductLine(result.productLine);
        setProductLineConfirmed(true);
      }
    } catch (err) {
      setLookup({ status: "error", message: err instanceof Error ? err.message : "Policy lookup failed." });
    }
  }

  useEffect(() => {
    if (step !== 5) return;
    const controller = new AbortController();
    async function loadChecklist() {
      setChecklistLoading(true);
      setChecklistError(null);
      try {
        const params = new URLSearchParams({ productLine, claimType: form.claimType });
        if (productLine === "Motor" && form.motor.injuryIndicator !== "Unknown") {
          params.set("injuryIndicator", form.motor.injuryIndicator);
        }
        const response = await fetch(`${apiBaseUrl}/api/v1/document-checklist/preview?${params.toString()}`, {
          headers: scopedHeaders(roleDef.key, ""),
          signal: controller.signal,
        });
        if (!response.ok) throw new Error(`Checklist preview failed.`);
        setChecklist((await response.json()) as ChecklistPreviewResponse);
      } catch (err) {
        if (controller.signal.aborted) return;
        setChecklistError(err instanceof Error ? err.message : "Required documents could not be loaded.");
        setChecklist(null);
      } finally {
        if (!controller.signal.aborted) setChecklistLoading(false);
      }
    }
    void loadChecklist();
    return () => controller.abort();
  }, [step, productLine, form.claimType, form.motor.injuryIndicator, roleDef.key]);

  function updateCommon<K extends keyof Omit<FnolForm, "motor" | "medical">>(key: K, value: FnolForm[K]) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  function updateMotor<K extends keyof MotorFields>(key: K, value: string) {
    setForm((current) => ({ ...current, motor: { ...current.motor, [key]: value } }));
  }

  function updateMedical<K extends keyof MedicalFields>(key: K, value: string) {
    setForm((current) => ({ ...current, medical: { ...current.medical, [key]: value } }));
  }

  const step1Missing = useMemo(() => {
    const required: [string, string][] = [
      ["Customer name", form.customerName],
      ["Customer contact", form.customerContact],
    ];
    return required.filter(([, value]) => !value.trim()).map(([label]) => label);
  }, [form.customerName, form.customerContact]);

  const step2Missing = useMemo(() => {
    if (!form.policyNumber.trim()) return ["Policy number"];
    if (lookup.status === "loading" || lookup.status === "idle") return ["Look up the policy"];
    return [];
  }, [form.policyNumber, lookup.status]);

  const step3Missing = useMemo(() => {
    const required: [string, string][] = [
      ["Incident/treatment date", form.accidentDate],
      ["Claim type", form.claimType],
      ["Description", form.incidentDescription],
    ];
    return required.filter(([, value]) => !value.trim()).map(([label]) => label);
  }, [form.accidentDate, form.claimType, form.incidentDescription]);

  const step4Missing = useMemo(() => {
    const required: [string, string][] =
      productLine === "Motor"
        ? [
            ["Vehicle registration", form.motor.vehicleRegistration],
            ["Driver name", form.motor.driverName],
            ["Driving permit number", form.motor.drivingPermitNumber],
            ["Accident time", form.motor.accidentTime],
            ["Accident location", form.motor.accidentLocation],
            ["Preferred communication channel", form.motor.preferredCommunicationChannel],
          ]
        : [
            ["Patient name", form.medical.patientName],
            ["Treatment provider", form.medical.providerName],
            ["Treatment date", form.medical.treatmentDate],
            ["Preferred communication channel", form.medical.preferredCommunicationChannel],
          ];
    return required.filter(([, value]) => !value.trim()).map(([label]) => label);
  }, [productLine, form.motor, form.medical]);

  const stepMissing: Record<number, string[]> = {
    1: step1Missing, 2: step2Missing, 3: step3Missing, 4: step4Missing, 5: [], 6: [],
  };
  const allMissing = [...step1Missing, ...step2Missing, ...step3Missing, ...step4Missing];
  const canSubmit = allMissing.length === 0 && !submitting;
  const canAdvance = stepMissing[step].length === 0;
  // Live completeness against the same required-field counts that gate step advancement, not a
  // separate invented metric. Steps 5-6 have no required fields of their own.
  const totalRequiredFields = 2 + 1 + 3 + (productLine === "Motor" ? 6 : 4);
  const completenessPercent = Math.round(((totalRequiredFields - allMissing.length) / totalRequiredFields) * 100);

  function goTo(next: number) {
    setStep(Math.min(6, Math.max(1, next)));
  }

  async function submit() {
    if (!canSubmit) return;
    setSubmitting(true);
    setError(null);

    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/cases`, {
        method: "POST",
        headers: { ...scopedHeaders(roleDef.key, ""), "Content-Type": "application/json" },
        body: JSON.stringify({
          customerName: form.customerName,
          customerEmail: form.customerEmail || null,
          customerPhone: form.customerContact,
          policyNumber: form.policyNumber,
          productLine,
          claimType: form.claimType,
          channel: "StaffEntry",
          incidentDate: form.accidentDate,
          incidentDescription: form.incidentDescription,
          motorFnolDetails: productLine === "Motor" ? { ...form.motor, uploadedDocuments: form.motor.uploadedDocuments || null } : null,
          medicalFnolDetails: productLine === "Medical" ? { ...form.medical, uploadedDocuments: form.medical.uploadedDocuments || null } : null,
        }),
      });

      if (!response.ok) {
        throw new Error(`Claim intake failed.`);
      }

      const created = (await response.json()) as CreateCaseResponse;
      if (!created.caseId) {
        throw new Error("Claim intake completed without a claim reference.");
      }

      toast.success("Claim created", {
        description: "The claim is now available in the claims queue for review.",
      });
      router.push(`/app/claims/${created.caseId}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Claim intake failed.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <>
      <SectionHeader
        eyebrow="Claims Intelligence"
        title="New Claim · First Notice of Loss"
        sub="Six-step structured intake. Product line, required fields, and required documents follow the policy once it's found."
      />

      {handoffChannel && (form.customerName || form.customerContact || form.customerEmail) ? (
        <div className="mb-3 rounded-dls-control bg-dls-info-tint px-3 py-2 text-[12px] text-dls-info-text">
          Pre-filled from the {handoffChannel} conversation, confirm the details below before continuing.
        </div>
      ) : null}

      <StepRail step={step} onSelect={goTo} completed={(n) => n < step && stepMissing[n].length === 0} />

      <div className="mb-1 mt-3 flex items-center justify-between text-[11.5px] text-dls-text-secondary">
        <span>Claim completeness</span>
        <span className="font-semibold text-dls-text-primary">{completenessPercent}%</span>
      </div>
      {/* Flat fill, not a gradient, design language §4.5 restricts gradients to the Assistant
          orb and chart bars; a form-completion progress bar is neither. */}
      <div className="h-2 w-full overflow-hidden rounded-full bg-dls-bg-page">
        <div className="h-full rounded-full bg-dls-primary transition-[width] duration-300" style={{ width: `${completenessPercent}%` }} />
      </div>

      <div className="mt-4 grid gap-4 xl:grid-cols-[minmax(0,1fr)_340px]">
        <GlassCard>
          {error && (
            <div className="mb-4 flex items-start gap-2 rounded-dls-control bg-dls-danger-tint px-3 py-2 text-[13px] text-dls-danger-text">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
              <span>{error}</span>
            </div>
          )}

          {step === 1 && (
            <StepSection icon={UserRound} title="Who is reporting this claim?" sub="Contact details are what the customer is reached on for updates and, later, self-service links.">
              <div className="grid gap-4 md:grid-cols-2">
                <Field label="Customer name" value={form.customerName} onChange={(value) => updateCommon("customerName", value)} required />
                <Field label="Customer contact (phone)" value={form.customerContact} onChange={(value) => updateCommon("customerContact", value)} required />
                <Field label="Customer email" value={form.customerEmail} onChange={(value) => updateCommon("customerEmail", value)} type="email" />
              </div>
            </StepSection>
          )}

          {step === 2 && (
            <StepSection icon={Search} title="Find the policy" sub="Product line and required fields below are derived from the policy, this is the only lookup step.">
              <div className="flex flex-col items-end gap-2 sm:flex-row">
                <div className="flex-1">
                  <Field
                    label="Policy number"
                    value={form.policyNumber}
                    onChange={(value) => { updateCommon("policyNumber", value); setLookup({ status: "idle" }); setProductLineConfirmed(false); }}
                    required
                  />
                </div>
                <Button
                  variant="primary"
                  onClick={runPolicyLookup}
                  disabled={!form.policyNumber.trim() || lookup.status === "loading"}
                  icon={lookup.status === "loading" ? <Loader2 className="animate-spin" aria-hidden="true" /> : <Search aria-hidden="true" />}
                >
                  Look up
                </Button>
              </div>

              {lookup.status === "found" && (
                <div className="mt-4 rounded-dls-card bg-dls-success-tint p-4">
                  <div className="flex items-center gap-2 text-[13px] font-bold text-dls-text-primary"><Check className="h-4 w-4 text-dls-success-text" aria-hidden="true" /> Policy found</div>
                  <div className="mt-3 grid gap-x-4 gap-y-1.5 text-[12.5px] sm:grid-cols-2">
                    <SummaryRow label="Product line" value={lookup.result.productLine} />
                    <SummaryRow label="Cover type" value={lookup.result.coverType ?? "-"} />
                    <SummaryRow label="Policy holder on file" value={lookup.result.customerName ?? "-"} />
                    <SummaryRow label="Cover status" value={lookup.result.isActive ? "Active" : "Not active for today's date"} warn={!lookup.result.isActive} />
                  </div>
                  {lookup.result.customerName && lookup.result.customerName.trim().toLowerCase() !== form.customerName.trim().toLowerCase() && (
                    <div className="mt-2 text-[11.5px] font-semibold text-dls-warning-text">
                      Note: the name on file (&quot;{lookup.result.customerName}&quot;) differs from the reporter&apos;s name entered in step 1. Confirm this before continuing.
                    </div>
                  )}
                </div>
              )}

              {lookup.status === "found" && lookup.result.possibleDuplicateClaims.length > 0 && (
                <div className="mt-3 rounded-dls-card bg-dls-warning-tint p-4">
                  <div className="flex items-center gap-2 text-[13px] font-bold text-dls-text-primary"><ShieldAlert className="h-4 w-4 text-dls-warning-text" aria-hidden="true" /> Possible duplicate claim{lookup.result.possibleDuplicateClaims.length > 1 ? "s" : ""} on this policy</div>
                  <ul className="mt-2 space-y-1 text-[12px] text-dls-text-secondary">
                    {lookup.result.possibleDuplicateClaims.map((duplicate) => (
                      <li key={duplicate.caseId}>
                        <a href={`/app/claims/${duplicate.caseId}`} className="dls-focus-outline rounded-dls-tight font-semibold text-dls-primary hover:underline">Claim {claimReference(duplicate.caseId)}</a>, {humanizeIdentifier(duplicate.status).toLowerCase()}
                        {duplicate.incidentDate ? `, incident ${duplicate.incidentDate}` : ""}
                      </li>
                    ))}
                  </ul>
                  <div className="mt-1.5 text-[11.5px] text-dls-text-secondary">This does not stop you continuing. Check it is not the same incident first.</div>
                </div>
              )}

              {lookup.status === "not-found" && (
                <div className="mt-4 rounded-dls-card bg-dls-danger-tint p-4 text-[13px] text-dls-danger-text">
                  No policy found for this number in this workspace. Check the number, or continue and select the product line manually below.
                </div>
              )}

              {lookup.status === "error" && (
                <div className="mt-4 rounded-dls-card bg-dls-danger-tint p-4 text-[13px] text-dls-danger-text">{lookup.message}</div>
              )}

              {(lookup.status === "not-found" || (lookup.status === "found" && !productLineConfirmed)) && (
                <div className="mt-4">
                  <div className="mb-1.5 text-[11.5px] font-semibold text-dls-text-secondary">Product line</div>
                  <div className="flex flex-wrap gap-1.5">
                    {PRODUCT_LINES.map((line) => (
                      <FilterTab
                        key={line.value}
                        active={productLine === line.value}
                        onClick={() => { selectProductLine(line.value); setProductLineConfirmed(true); }}
                      >
                        {line.label}
                      </FilterTab>
                    ))}
                  </div>
                </div>
              )}

              {lookup.status === "found" && productLineConfirmed && (
                <div className="mt-3 text-[12px] text-dls-text-secondary">
                  Product line set to <b>{productLine}</b> from the policy.{" "}
                  <button type="button" onClick={() => setProductLineConfirmed(false)} className="dls-focus-outline font-semibold text-dls-primary hover:underline">Change manually</button>
                </div>
              )}
            </StepSection>
          )}

          {step === 3 && (
            <StepSection icon={FileText} title="What happened?" sub="Claim type and a real description of the incident, this is what routing and, for motor, injury detection read.">
              <div className="grid gap-4 md:grid-cols-2">
                <SelectField
                  label="Claim type"
                  value={form.claimType}
                  onChange={(value) => updateCommon("claimType", value)}
                  options={productLine === "Motor" ? MOTOR_CLAIM_TYPES : MEDICAL_CLAIM_TYPES}
                  required
                />
                <Field
                  label={productLine === "Motor" ? "Accident date" : "Treatment date"}
                  value={form.accidentDate}
                  onChange={(value) => updateCommon("accidentDate", value)}
                  type="date"
                  required
                />
                <TextArea
                  label="Incident description"
                  value={form.incidentDescription}
                  onChange={(value) => updateCommon("incidentDescription", value)}
                  required
                  className="md:col-span-2"
                />
              </div>
            </StepSection>
          )}

          {step === 4 && (
            <StepSection icon={FileText} title={`${productLine} details`} sub={`Details specific to ${productLine.toLowerCase()} claims. These are saved with the claim.`}>
              <div className="grid gap-4 md:grid-cols-2">
                {productLine === "Motor" ? (
                  <>
                    <Field label="Vehicle registration" value={form.motor.vehicleRegistration} onChange={(value) => updateMotor("vehicleRegistration", value)} required />
                    <Field label="Driver name" value={form.motor.driverName} onChange={(value) => updateMotor("driverName", value)} required />
                    <Field label="Driving permit number" value={form.motor.drivingPermitNumber} onChange={(value) => updateMotor("drivingPermitNumber", value)} required />
                    <Field label="Accident time" value={form.motor.accidentTime} onChange={(value) => updateMotor("accidentTime", value)} type="time" required />
                    <Field label="Accident location" value={form.motor.accidentLocation} onChange={(value) => updateMotor("accidentLocation", value)} required />
                    <SelectField label="Third party involved" value={form.motor.thirdPartyInvolved} onChange={(value) => updateMotor("thirdPartyInvolved", value)} options={["Unknown", "No", "Yes"]} />
                    <div className="rounded-dls-card bg-dls-info-tint p-3 md:col-span-2">
                      <div className="mb-2 text-[11px] font-bold uppercase tracking-[0.12em] text-dls-info-text">Routing indicators, these change what happens next, not just what&apos;s on file</div>
                      <div className="grid gap-3 md:grid-cols-2">
                        <SelectField label="Injury indicator" value={form.motor.injuryIndicator} onChange={(value) => updateMotor("injuryIndicator", value)} options={["Unknown", "No injuries reported", "Injury reported"]} />
                        <SelectField label="Police report status" value={form.motor.policeReportStatus} onChange={(value) => updateMotor("policeReportStatus", value)} options={["Unknown", "Not available", "Requested", "Received"]} />
                      </div>
                    </div>
                    <SelectField
                      label="Preferred communication channel"
                      value={form.motor.preferredCommunicationChannel}
                      onChange={(value) => updateMotor("preferredCommunicationChannel", value)}
                      options={["Phone", "Email", "WhatsApp", "Broker", "Branch"]}
                      required
                    />
                  </>
                ) : (
                  <>
                    <Field label="Patient name" value={form.medical.patientName} onChange={(value) => updateMedical("patientName", value)} required />
                    <SelectField label="Relationship to policyholder" value={form.medical.relationshipToPolicyholder} onChange={(value) => updateMedical("relationshipToPolicyholder", value)} options={["Self", "Spouse", "Child", "Other dependent"]} />
                    <Field label="Treatment provider (hospital/clinic)" value={form.medical.providerName} onChange={(value) => updateMedical("providerName", value)} required />
                    <Field label="Treating doctor" value={form.medical.treatingDoctor} onChange={(value) => updateMedical("treatingDoctor", value)} />
                    <SelectField label="Admission type" value={form.medical.admissionType} onChange={(value) => updateMedical("admissionType", value)} options={["Outpatient", "Inpatient", "Day case"]} />
                    <SelectField
                      label="Preferred communication channel"
                      value={form.medical.preferredCommunicationChannel}
                      onChange={(value) => updateMedical("preferredCommunicationChannel", value)}
                      options={["Phone", "Email", "WhatsApp", "Broker", "Branch"]}
                      required
                    />
                    <TextArea label="Diagnosis / treatment summary" value={form.medical.diagnosisSummary} onChange={(value) => updateMedical("diagnosisSummary", value)} className="md:col-span-2" />
                  </>
                )}
                <TextArea
                  label="Uploaded documents or document notes"
                  value={productLine === "Motor" ? form.motor.uploadedDocuments : form.medical.uploadedDocuments}
                  onChange={(value) => (productLine === "Motor" ? updateMotor("uploadedDocuments", value) : updateMedical("uploadedDocuments", value))}
                  className="md:col-span-2"
                />
              </div>
            </StepSection>
          )}

          {step === 5 && (
            <StepSection icon={Upload} title="Required documents" sub="Generated from the product line, claim type, and routing indicators already captured.">
              {checklistLoading ? (
                <LoadingSkeleton rows={3} />
              ) : checklistError ? (
                <ErrorStateSurface message={checklistError} />
              ) : (
                <ul className="space-y-1.5">
                  {checklist?.requiredDocuments.map((document) => (
                    <li key={document.documentType} className="flex items-center justify-between gap-3 text-[13px]">
                      <span className="text-dls-text-secondary">{document.label}</span>
                      <Badge tone="warning">Required</Badge>
                    </li>
                  ))}
                  {checklist?.optionalDocuments.map((document) => (
                    <li key={document.documentType} className="flex items-center justify-between gap-3 text-[13px]">
                      <span className="text-dls-text-secondary">{document.label}</span>
                      <Badge tone="neutral">Optional</Badge>
                    </li>
                  ))}
                </ul>
              )}
              <div className="mt-3 flex items-center gap-2 rounded-dls-control border border-dls-border-subtle bg-dls-bg-page px-3 py-2 text-[12.5px] text-dls-text-secondary">
                <Upload className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
                Document upload happens from the claim&apos;s document flow after the claim is created. A missing document does not block intake, it opens with a documented gap.
              </div>
            </StepSection>
          )}

          {step === 6 && (
            <StepSection icon={Check} title="Review and submit" sub="Claim reference, SLA timer, checklist, and audit event are created by the API on submit.">
              <div className="space-y-4">
                <ReviewGroup title="Claimant">
                  <SummaryRow label="Name" value={form.customerName} />
                  <SummaryRow label="Contact" value={form.customerContact} />
                  {form.customerEmail && <SummaryRow label="Email" value={form.customerEmail} />}
                </ReviewGroup>
                <ReviewGroup title="Policy">
                  <SummaryRow label="Policy number" value={form.policyNumber} />
                  <SummaryRow label="Product line" value={productLine} />
                  {lookup.status === "found" && <SummaryRow label="Cover status" value={lookup.result.isActive ? "Active" : "Not active for today's date"} warn={!lookup.result.isActive} />}
                </ReviewGroup>
                <ReviewGroup title="Incident">
                  <SummaryRow label="Claim type" value={form.claimType} />
                  <SummaryRow label={productLine === "Motor" ? "Accident date" : "Treatment date"} value={form.accidentDate} />
                  <SummaryRow label="Description" value={form.incidentDescription} />
                </ReviewGroup>
                <ReviewGroup title={`${productLine} details`}>
                  {productLine === "Motor" ? (
                    <>
                      <SummaryRow label="Vehicle registration" value={form.motor.vehicleRegistration} />
                      <SummaryRow label="Driver" value={form.motor.driverName} />
                      <SummaryRow label="Injury indicator" value={form.motor.injuryIndicator} warn={form.motor.injuryIndicator === "Injury reported"} />
                    </>
                  ) : (
                    <>
                      <SummaryRow label="Patient" value={form.medical.patientName} />
                      <SummaryRow label="Provider" value={form.medical.providerName} />
                    </>
                  )}
                </ReviewGroup>
                {checklist && checklist.requiredDocuments.length > 0 && (
                  <div className="text-[12px] text-dls-text-secondary">
                    {checklist.requiredDocuments.length} required document{checklist.requiredDocuments.length === 1 ? "" : "s"}, collected from the claim after creation.
                  </div>
                )}
              </div>
            </StepSection>
          )}

          <div className="mt-6 flex items-center justify-between border-t border-dls-border-subtle pt-4">
            <Button variant="secondary" onClick={() => goTo(step - 1)} disabled={step === 1} icon={<ChevronLeft aria-hidden="true" />}>
              Back
            </Button>
            {step < 6 ? (
              // Trailing-icon "Next", hand-styled to Button's primary variant (Button's `icon`
              // prop only renders leading; extending its API for this one call site isn't
              // warranted). Same Link-as-button precedent as ClaimsQueue, but a real <button>.
              <button
                type="button"
                onClick={() => goTo(step + 1)}
                disabled={!canAdvance}
                className="dls-focus-outline inline-flex items-center justify-center gap-1.5 rounded-dls-control bg-dls-primary px-4 py-[9px] text-[13px] font-bold text-dls-text-inverse shadow-dls-primary transition-colors duration-dls-fast ease-dls hover:bg-dls-primary-hover disabled:opacity-60"
              >
                Next <ChevronRight className="h-3.5 w-3.5" aria-hidden="true" />
              </button>
            ) : (
              <Button
                variant="primary"
                onClick={submit}
                disabled={!canSubmit}
                icon={submitting ? <Loader2 className="animate-spin" aria-hidden="true" /> : <Check aria-hidden="true" />}
              >
                Create claim
              </Button>
            )}
          </div>
        </GlassCard>

        <div className="space-y-4">
          <GlassCard>
            <div className="mb-2 text-[11px] font-bold uppercase tracking-[0.14em] text-dls-text-muted">This step</div>
            {stepMissing[step].length === 0 ? (
              <div className="text-[13px] text-dls-text-secondary">Ready to continue.</div>
            ) : (
              <ul className="space-y-1.5 text-[13px] text-dls-text-secondary">
                {stepMissing[step].map((field) => (
                  <li key={field}>- {field}</li>
                ))}
              </ul>
            )}
          </GlassCard>
          {step < 6 && allMissing.length > stepMissing[step].length && (
            <GlassCard>
              <div className="mb-2 text-[11px] font-bold uppercase tracking-[0.14em] text-dls-text-muted">Still outstanding</div>
              <div className="text-[12.5px] text-dls-text-secondary">{allMissing.length} field{allMissing.length === 1 ? "" : "s"} across all steps.</div>
            </GlassCard>
          )}
        </div>
      </div>
    </>
  );
}

function StepRail({ step, onSelect, completed }: { step: number; onSelect: (n: number) => void; completed: (n: number) => boolean }) {
  return (
    <div className="flex items-center gap-1 overflow-x-auto pb-1">
      {STEPS.map((s, index) => {
        const active = s.n === step;
        const done = completed(s.n);
        return (
          <div key={s.n} className="flex shrink-0 items-center gap-1">
            <button
              type="button"
              onClick={() => onSelect(s.n)}
              className={
                "dls-focus-outline flex items-center gap-2 rounded-dls-pill border px-3 py-1.5 text-[12.5px] font-semibold transition-colors duration-dls-fast ease-dls " +
                (active
                  ? "border-dls-primary-border bg-dls-primary-soft-alt text-dls-primary-accent"
                  : done
                    ? "border-transparent text-dls-success-text"
                    : "border-transparent text-dls-text-muted hover:text-dls-text-secondary")
              }
            >
              {/* A numbered step badge, not a tab, solid fill here is the conventional wizard-step
                  treatment, distinct from §8.11's tab-background rule this component otherwise
                  follows for its own pill background. */}
              <span
                className={
                  "grid h-5 w-5 place-items-center rounded-full text-[10.5px] font-bold " +
                  (active ? "bg-dls-primary text-dls-text-inverse" : done ? "bg-dls-success-tint text-dls-success-text" : "bg-dls-bg-page text-dls-text-muted")
                }
              >
                {done && !active ? <Check className="h-3 w-3" aria-hidden="true" /> : s.n}
              </span>
              {s.label}
            </button>
            {index < STEPS.length - 1 && <div className="h-px w-4 bg-dls-border-subtle" />}
          </div>
        );
      })}
    </div>
  );
}

function StepSection({ icon: Icon, title, sub, children }: { icon: typeof Search; title: string; sub: string; children: React.ReactNode }) {
  return (
    <div>
      <div className="flex items-start gap-2.5 border-b border-dls-border-subtle pb-4">
        <div className="mt-0.5 grid h-8 w-8 shrink-0 place-items-center rounded-dls-chip bg-dls-bg-page text-dls-text-secondary">
          <Icon className="h-4 w-4" aria-hidden="true" />
        </div>
        <div>
          <div className="text-[14px] font-bold text-dls-text-primary">{title}</div>
          <div className="mt-0.5 text-[12.5px] text-dls-text-secondary">{sub}</div>
        </div>
      </div>
      <div className="mt-5">{children}</div>
    </div>
  );
}

function ReviewGroup({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3.5">
      <div className="mb-2 text-[11px] font-bold uppercase tracking-[0.12em] text-dls-text-muted">{title}</div>
      <div className="space-y-1">{children}</div>
    </div>
  );
}

function SummaryRow({ label, value, warn = false }: { label: string; value: string; warn?: boolean }) {
  return (
    <div className="flex items-baseline justify-between gap-3 text-[12.5px]">
      <span className="shrink-0 text-dls-text-muted">{label}</span>
      <span className={"text-right " + (warn ? "font-semibold text-dls-warning-text" : "text-dls-text-secondary")}>{value || "-"}</span>
    </div>
  );
}

// The three field wrappers below keep every existing call site's API unchanged (label/value/
// onChange/type/required/options/className) but delegate their markup to components/ui/Input.tsx's
// shared Field/Input/Select/Textarea, rather than re-tokenizing hand-rolled markup that duplicates
// what those primitives already do correctly. Single source of truth for control styling; zero
// changes needed at any of this file's ~25 call sites.

function Field({
  label,
  value,
  onChange,
  type = "text",
  required = false,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  type?: string;
  required?: boolean;
}) {
  const id = useId();
  return (
    <UIField label={required ? `${label} *` : label} htmlFor={id}>
      <Input id={id} value={value} onChange={(event) => onChange(event.target.value)} type={type} />
    </UIField>
  );
}

function TextArea({
  label,
  value,
  onChange,
  required = false,
  className = "",
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  required?: boolean;
  className?: string;
}) {
  const id = useId();
  return (
    <div className={className}>
      <UIField label={required ? `${label} *` : label} htmlFor={id}>
        <Textarea id={id} value={value} onChange={(event) => onChange(event.target.value)} />
      </UIField>
    </div>
  );
}

function SelectField({
  label,
  value,
  onChange,
  options,
  required = false,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  options: string[];
  required?: boolean;
}) {
  const id = useId();
  return (
    <UIField label={required ? `${label} *` : label} htmlFor={id}>
      <Select id={id} value={value} onChange={(event) => onChange(event.target.value)} className="w-full">
        {options.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </Select>
    </UIField>
  );
}
