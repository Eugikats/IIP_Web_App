"use client";

import { useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { AlertTriangle, Check, FileText, Loader2, Upload } from "lucide-react";
import { GlassCard, SectionHeader, Badge } from "@/components/iip/Primitives";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

const requiredDocuments = [
  "Claim form",
  "Police report",
  "National ID",
  "Driving permit",
  "Vehicle photos",
  "Repair quotation",
  "Policy schedule",
];

type FnolForm = {
  customerName: string;
  customerContact: string;
  customerEmail: string;
  policyNumber: string;
  vehicleRegistration: string;
  accidentDate: string;
  accidentTime: string;
  accidentLocation: string;
  incidentDescription: string;
  driverName: string;
  drivingPermitNumber: string;
  thirdPartyInvolved: string;
  injuryIndicator: string;
  policeReportStatus: string;
  preferredCommunicationChannel: string;
  uploadedDocuments: string;
};

type CreateCaseResponse = {
  caseId?: string;
};

const initialForm: FnolForm = {
  customerName: "",
  customerContact: "",
  customerEmail: "",
  policyNumber: "",
  vehicleRegistration: "",
  accidentDate: "",
  accidentTime: "",
  accidentLocation: "",
  incidentDescription: "",
  driverName: "",
  drivingPermitNumber: "",
  thirdPartyInvolved: "Unknown",
  injuryIndicator: "Unknown",
  policeReportStatus: "Unknown",
  preferredCommunicationChannel: "Phone",
  uploadedDocuments: "",
};

export default function FNOL() {
  const router = useRouter();
  const { roleDef } = useUI();
  const [form, setForm] = useState<FnolForm>(initialForm);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const missingRequired = useMemo(
    () =>
      [
        ["Customer name", form.customerName],
        ["Customer contact", form.customerContact],
        ["Policy number", form.policyNumber],
        ["Vehicle registration", form.vehicleRegistration],
        ["Accident date", form.accidentDate],
        ["Accident time", form.accidentTime],
        ["Accident location", form.accidentLocation],
        ["Incident description", form.incidentDescription],
        ["Driver name", form.driverName],
        ["Driving permit number", form.drivingPermitNumber],
        ["Preferred communication channel", form.preferredCommunicationChannel],
      ]
        .filter(([, value]) => !String(value).trim())
        .map(([label]) => label),
    [form],
  );

  const canSubmit = missingRequired.length === 0 && !submitting;

  function update<K extends keyof FnolForm>(key: K, value: FnolForm[K]) {
    setForm((current) => ({ ...current, [key]: value }));
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
          productLine: "Motor",
          claimType: "Motor Accident",
          channel: "StaffEntry",
          incidentDate: form.accidentDate,
          incidentDescription: form.incidentDescription,
          motorFnolDetails: {
            vehicleRegistration: form.vehicleRegistration,
            accidentTime: form.accidentTime,
            accidentLocation: form.accidentLocation,
            driverName: form.driverName,
            drivingPermitNumber: form.drivingPermitNumber,
            thirdPartyInvolved: form.thirdPartyInvolved,
            injuryIndicator: form.injuryIndicator,
            policeReportStatus: form.policeReportStatus,
            preferredCommunicationChannel: form.preferredCommunicationChannel,
            uploadedDocuments: form.uploadedDocuments || null,
          },
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
        sub="Structured motor FNOL intake. Required fields must be captured before the claim can enter review."
      />

      <div className="grid xl:grid-cols-[minmax(0,1fr)_340px] gap-4">
        <GlassCard>
          <div className="flex items-start justify-between gap-4 border-b border-cloud pb-4">
            <div>
              <div className="text-[14px] font-semibold text-navy">Motor FNOL details</div>
              <div className="mt-1 text-[12.5px] text-slate2">Motor-specific FNOL fields are persisted as structured case data.</div>
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
            <Field label="Customer name" value={form.customerName} onChange={(value) => update("customerName", value)} required />
            <Field label="Customer contact" value={form.customerContact} onChange={(value) => update("customerContact", value)} required />
            <Field label="Customer email" value={form.customerEmail} onChange={(value) => update("customerEmail", value)} type="email" />
            <Field label="Policy number" value={form.policyNumber} onChange={(value) => update("policyNumber", value)} required />
            <Field label="Vehicle registration" value={form.vehicleRegistration} onChange={(value) => update("vehicleRegistration", value)} required />
            <Field label="Driver name" value={form.driverName} onChange={(value) => update("driverName", value)} required />
            <Field label="Driving permit number" value={form.drivingPermitNumber} onChange={(value) => update("drivingPermitNumber", value)} required />
            <Field label="Accident date" value={form.accidentDate} onChange={(value) => update("accidentDate", value)} type="date" required />
            <Field label="Accident time" value={form.accidentTime} onChange={(value) => update("accidentTime", value)} type="time" required />
            <Field label="Accident location" value={form.accidentLocation} onChange={(value) => update("accidentLocation", value)} required />
            <SelectField
              label="Third party involved"
              value={form.thirdPartyInvolved}
              onChange={(value) => update("thirdPartyInvolved", value)}
              options={["Unknown", "No", "Yes"]}
            />
            <SelectField
              label="Injury indicator"
              value={form.injuryIndicator}
              onChange={(value) => update("injuryIndicator", value)}
              options={["Unknown", "No injuries reported", "Injury reported"]}
            />
            <SelectField
              label="Police report status"
              value={form.policeReportStatus}
              onChange={(value) => update("policeReportStatus", value)}
              options={["Unknown", "Not available", "Requested", "Received"]}
            />
            <SelectField
              label="Preferred communication channel"
              value={form.preferredCommunicationChannel}
              onChange={(value) => update("preferredCommunicationChannel", value)}
              options={["Phone", "Email", "WhatsApp", "Broker", "Branch"]}
              required
            />
            <TextArea
              label="Incident description"
              value={form.incidentDescription}
              onChange={(value) => update("incidentDescription", value)}
              required
              className="md:col-span-2"
            />
            <TextArea
              label="Uploaded documents or document notes"
              value={form.uploadedDocuments}
              onChange={(value) => update("uploadedDocuments", value)}
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
            <ul className="space-y-1.5">
              {requiredDocuments.map((document) => (
                <li key={document} className="flex items-center justify-between gap-3 text-[13px]">
                  <span className="text-slate2">{document}</span>
                  <Badge tone="warning">Required</Badge>
                </li>
              ))}
            </ul>
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
