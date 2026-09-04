"use client";

import { useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { AlertTriangle, Check, FileText, Loader2, Upload } from "lucide-react";
import { GlassCard, SectionHeader, Badge } from "@/components/iip/Primitives";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

// WO-15: FNOL was hardcoded to Motor — a fixed productLine/claimType, motor-only fields, and a
// hardcoded seven-item document list. The backend was never motor-limited (CreateCaseCommand.
// ProductLine is a free string and DocumentChecklistRules already branches by product line); this
// generalises intake to a schema per product line and adds Medical as the second real line.
// The document checklist is never a client constant (A1) — it's always fetched from
// GET /api/v1/document-checklist/preview, the same DocumentChecklistRules the rest of the
// platform uses once a case exists.

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

const PRODUCT_LINES: { value: ProductLine; label: string; defaultClaimType: string }[] = [
  { value: "Motor", label: "Motor", defaultClaimType: "Own damage" },
  { value: "Medical", label: "Medical", defaultClaimType: "Medical reimbursement" },
];

const MOTOR_CLAIM_TYPES = ["Own damage", "Third-party property", "Theft", "Bodily injury"];
const MEDICAL_CLAIM_TYPES = ["Medical reimbursement", "Hospitalisation", "Outpatient treatment"];

const initialForm: FnolForm = {
  customerName: "",
  customerContact: "",
  customerEmail: "",
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
    preferredCommunicationChannel: "Phone",
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
    preferredCommunicationChannel: "Phone",
    uploadedDocuments: "",
  },
};

export default function FNOL() {
  const router = useRouter();
  const { roleDef } = useUI();
  const [productLine, setProductLine] = useState<ProductLine>("Motor");
  const [form, setForm] = useState<FnolForm>(initialForm);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [checklist, setChecklist] = useState<ChecklistPreviewResponse | null>(null);
  const [checklistLoading, setChecklistLoading] = useState(true);
  const [checklistError, setChecklistError] = useState<string | null>(null);

  function selectProductLine(next: ProductLine) {
    setProductLine(next);
    setForm((current) => ({
      ...current,
      claimType: PRODUCT_LINES.find((line) => line.value === next)!.defaultClaimType,
    }));
  }

  useEffect(() => {
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
        if (!response.ok) throw new Error(`Checklist preview failed with HTTP ${response.status}.`);
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
  }, [productLine, form.claimType, form.motor.injuryIndicator, roleDef.key]);

  const missingRequired = useMemo(() => {
    const common: [string, string][] = [
      ["Customer name", form.customerName],
      ["Customer contact", form.customerContact],
      ["Policy number", form.policyNumber],
      ["Incident/treatment date", form.accidentDate],
      ["Description", form.incidentDescription],
    ];
    const perLine: [string, string][] =
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
    return [...common, ...perLine].filter(([, value]) => !value.trim()).map(([label]) => label);
  }, [form, productLine]);

  const canSubmit = missingRequired.length === 0 && !submitting;

  function updateCommon<K extends keyof Omit<FnolForm, "motor" | "medical">>(key: K, value: FnolForm[K]) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  function updateMotor<K extends keyof MotorFields>(key: K, value: string) {
    setForm((current) => ({ ...current, motor: { ...current.motor, [key]: value } }));
  }

  function updateMedical<K extends keyof MedicalFields>(key: K, value: string) {
    setForm((current) => ({ ...current, medical: { ...current.medical, [key]: value } }));
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
        throw new Error(`Claim intake failed with HTTP ${response.status}.`);
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
        sub="Structured FNOL intake. Fields and required documents follow the selected product line."
      />

      <div className="mb-4 flex flex-wrap gap-2">
        {PRODUCT_LINES.map((line) => (
          <button
            key={line.value}
            type="button"
            onClick={() => selectProductLine(line.value)}
            className={
              "rounded-2xl border px-4 py-2 text-[13px] font-medium transition-colors " +
              (productLine === line.value
                ? "border-info bg-[hsl(var(--info)/0.08)] text-info"
                : "border-cloud bg-white/70 text-slate2 hover:bg-white")
            }
          >
            {line.label}
          </button>
        ))}
      </div>

      <div className="grid xl:grid-cols-[minmax(0,1fr)_340px] gap-4">
        <GlassCard>
          <div className="flex items-start justify-between gap-4 border-b border-cloud pb-4">
            <div>
              <div className="text-[14px] font-semibold text-navy">{productLine} FNOL details</div>
              <div className="mt-1 text-[12.5px] text-slate2">{productLine}-specific FNOL fields are persisted as structured case data.</div>
            </div>
            <Badge tone={missingRequired.length === 0 ? "success" : "warning"}>
              {missingRequired.length === 0 ? "Ready" : `${missingRequired.length} missing`}
            </Badge>
          </div>

          {error && (
            <div className="mt-4 flex items-start gap-2 rounded-xl border border-danger/20 bg-danger/5 px-3 py-2 text-[13px] text-danger">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
              <span>{error}</span>
            </div>
          )}

          <div className="mt-5 grid md:grid-cols-2 gap-4">
            <Field label="Customer name" value={form.customerName} onChange={(value) => updateCommon("customerName", value)} required />
            <Field label="Customer contact" value={form.customerContact} onChange={(value) => updateCommon("customerContact", value)} required />
            <Field label="Customer email" value={form.customerEmail} onChange={(value) => updateCommon("customerEmail", value)} type="email" />
            <Field label="Policy number" value={form.policyNumber} onChange={(value) => updateCommon("policyNumber", value)} required />
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

            {productLine === "Motor" ? (
              <>
                <Field label="Vehicle registration" value={form.motor.vehicleRegistration} onChange={(value) => updateMotor("vehicleRegistration", value)} required />
                <Field label="Driver name" value={form.motor.driverName} onChange={(value) => updateMotor("driverName", value)} required />
                <Field label="Driving permit number" value={form.motor.drivingPermitNumber} onChange={(value) => updateMotor("drivingPermitNumber", value)} required />
                <Field label="Accident time" value={form.motor.accidentTime} onChange={(value) => updateMotor("accidentTime", value)} type="time" required />
                <Field label="Accident location" value={form.motor.accidentLocation} onChange={(value) => updateMotor("accidentLocation", value)} required />
                <SelectField label="Third party involved" value={form.motor.thirdPartyInvolved} onChange={(value) => updateMotor("thirdPartyInvolved", value)} options={["Unknown", "No", "Yes"]} />
                <SelectField label="Injury indicator" value={form.motor.injuryIndicator} onChange={(value) => updateMotor("injuryIndicator", value)} options={["Unknown", "No injuries reported", "Injury reported"]} />
                <SelectField label="Police report status" value={form.motor.policeReportStatus} onChange={(value) => updateMotor("policeReportStatus", value)} options={["Unknown", "Not available", "Requested", "Received"]} />
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
              label="Incident description"
              value={form.incidentDescription}
              onChange={(value) => updateCommon("incidentDescription", value)}
              required
              className="md:col-span-2"
            />
            <TextArea
              label="Uploaded documents or document notes"
              value={productLine === "Motor" ? form.motor.uploadedDocuments : form.medical.uploadedDocuments}
              onChange={(value) => (productLine === "Motor" ? updateMotor("uploadedDocuments", value) : updateMedical("uploadedDocuments", value))}
              className="md:col-span-2"
            />
          </div>

          <div className="mt-6 flex flex-col gap-3 border-t border-cloud pt-4 sm:flex-row sm:items-center sm:justify-between">
            <div className="text-[12.5px] text-slate2">
              Claim reference, SLA timer, checklist, and audit event are created by the API.
            </div>
            <button
              type="button"
              onClick={submit}
              disabled={!canSubmit}
              className="inline-flex items-center justify-center gap-2 rounded-2xl gradient-primary px-4 py-2.5 text-[13px] font-semibold text-white shadow-l2 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {submitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Check className="h-4 w-4" />}
              Create claim
            </button>
          </div>
        </GlassCard>

        <div className="space-y-4">
          <GlassCard>
            <div className="flex items-center gap-2 mb-3">
              <FileText className="h-3.5 w-3.5 text-info" />
              <div className="text-[11px] uppercase tracking-[0.14em] text-stone">Required documents</div>
            </div>
            {checklistLoading ? (
              <div className="flex items-center gap-2 text-[12.5px] text-stone"><Loader2 className="h-3.5 w-3.5 animate-spin" /> Loading…</div>
            ) : checklistError ? (
              <div className="text-[12.5px] text-stone">{checklistError}</div>
            ) : (
              <ul className="space-y-1.5">
                {checklist?.requiredDocuments.map((document) => (
                  <li key={document.documentType} className="flex items-center justify-between gap-3 text-[13px]">
                    <span className="text-slate2">{document.label}</span>
                    <Badge tone="warning">Required</Badge>
                  </li>
                ))}
                {checklist?.optionalDocuments.map((document) => (
                  <li key={document.documentType} className="flex items-center justify-between gap-3 text-[13px]">
                    <span className="text-slate2">{document.label}</span>
                    <Badge tone="neutral">Optional</Badge>
                  </li>
                ))}
              </ul>
            )}
            <div className="mt-3 flex items-center gap-2 rounded-xl border border-cloud bg-white/70 px-3 py-2 text-[12.5px] text-slate2">
              <Upload className="h-3.5 w-3.5 shrink-0" />
              Document upload remains on the claim review document flow after claim creation.
            </div>
          </GlassCard>

          <GlassCard>
            <div className="text-[11px] uppercase tracking-[0.14em] text-stone mb-2">Missing fields</div>
            {missingRequired.length === 0 ? (
              <div className="text-[13px] text-slate2">All required FNOL fields are captured.</div>
            ) : (
              <ul className="space-y-1.5 text-[13px] text-slate2">
                {missingRequired.map((field) => (
                  <li key={field}>- {field}</li>
                ))}
              </ul>
            )}
          </GlassCard>
        </div>
      </div>
    </>
  );
}

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
  return (
    <label>
      <span className="text-[11.5px] font-medium text-slate2">
        {label}
        {required ? " *" : ""}
      </span>
      <input
        value={value}
        onChange={(event) => onChange(event.target.value)}
        type={type}
        className="mt-1 w-full rounded-2xl border border-cloud bg-white/80 px-4 py-3 text-[13.5px] text-navy outline-none focus:ring-2 focus:ring-info/40"
      />
    </label>
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
  return (
    <label className={className}>
      <span className="text-[11.5px] font-medium text-slate2">
        {label}
        {required ? " *" : ""}
      </span>
      <textarea
        rows={4}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="mt-1 w-full rounded-2xl border border-cloud bg-white/80 px-4 py-3 text-[13.5px] text-navy outline-none focus:ring-2 focus:ring-info/40"
      />
    </label>
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
  return (
    <label>
      <span className="text-[11.5px] font-medium text-slate2">
        {label}
        {required ? " *" : ""}
      </span>
      <select
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="mt-1 w-full rounded-2xl border border-cloud bg-white/80 px-4 py-3 text-[13.5px] text-navy outline-none focus:ring-2 focus:ring-info/40"
      >
        {options.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </select>
    </label>
  );
}
