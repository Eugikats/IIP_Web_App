"use client";

import { useId } from "react";
import type { FormEvent } from "react";
import { ArrowRight, Check, Loader2, ShieldCheck } from "lucide-react";
import { Input, Select, Textarea, Field as UIField } from "@/components/ui/Input";
import { DynamicFieldsForm } from "@/components/iip/DynamicFieldsForm";
import type { ClaimFieldForm } from "@/lib/claimFields";

type YesNoBlank = "Yes" | "No" | "";

export type BuiltInMotorAnswers = {
  vehicleRegistration: string;
  accidentLocation: string;
  thirdPartyInvolved: YesNoBlank;
  injuryIndicator: YesNoBlank;
};

type ClaimantNewClaimFormProps = {
  policyNumber: string;
  productLine: string;
  claimType: string;
  claimTypeOptions: string[];
  onClaimTypeChange: (value: string) => void;
  incidentDate: string;
  onIncidentDateChange: (value: string) => void;
  incidentDescription: string;
  onIncidentDescriptionChange: (value: string) => void;
  /** The insurer's own questions, when the product has configured them for this claim type. */
  fieldForm: ClaimFieldForm | null;
  fieldValues: Record<string, string>;
  fieldErrors: Record<string, string>;
  onFieldChange: (key: string, value: string) => void;
  motor: BuiltInMotorAnswers;
  onMotorChange: (patch: Partial<BuiltInMotorAnswers>) => void;
  submitting: boolean;
  error: string | null;
  onSubmit: (event: FormEvent) => void;
  /** Used by "Preview as customer": the form looks the same but cannot be submitted. */
  previewOnly?: boolean;
};

/**
 * The claimant's "start a claim" form. Shared by the claim page and by "Preview as customer", so
 * what an administrator previews is the same component the customer sees.
 */
export function ClaimantNewClaimForm({
  policyNumber, productLine, claimType, claimTypeOptions, onClaimTypeChange,
  incidentDate, onIncidentDateChange, incidentDescription, onIncidentDescriptionChange,
  fieldForm, fieldValues, fieldErrors, onFieldChange, motor, onMotorChange,
  submitting, error, onSubmit, previewOnly = false,
}: ClaimantNewClaimFormProps) {
  const claimTypeId = useId();
  const incidentDateId = useId();
  const incidentDescriptionId = useId();
  const vehicleRegistrationId = useId();
  const accidentLocationId = useId();
  const thirdPartyId = useId();
  const injuryId = useId();
  const useConfiguredFields = Boolean(fieldForm?.configured && fieldForm.fields.length > 0);

  return (
    <form onSubmit={onSubmit} className="space-y-5" data-testid="claimant-new-claim-form">
      <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-6">
        <div className="mb-4 flex items-start gap-3 border-b border-dls-border-subtle pb-4">
          <ShieldCheck className="mt-0.5 h-5 w-5 shrink-0 text-dls-success-text" aria-hidden="true" />
          <div>
            <div className="text-sm font-semibold text-dls-text-primary">Verified policyholder</div>
            <div className="mt-1 text-xs leading-5 text-dls-text-muted">
              We&apos;ve matched your access link to policy {policyNumber} ({productLine}). Tell us
              what happened and we&apos;ll open your claim.
            </div>
          </div>
        </div>

        <UIField label="Claim type" htmlFor={claimTypeId}>
          <Select id={claimTypeId} value={claimType} onChange={(event) => onClaimTypeChange(event.target.value)} className="w-full">
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
              onChange={(event) => onIncidentDateChange(event.target.value)}
              max={new Date().toISOString().slice(0, 10)}
            />
          </UIField>
        </div>

        <div className="mt-5">
          <UIField label="What happened?" htmlFor={incidentDescriptionId}>
            <Textarea
              id={incidentDescriptionId}
              value={incidentDescription}
              onChange={(event) => onIncidentDescriptionChange(event.target.value)}
              rows={4}
              placeholder="Describe the incident in your own words."
            />
          </UIField>
        </div>
      </div>

      {useConfiguredFields && fieldForm && (
        <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-6">
          <div className="mb-4 text-xs font-bold uppercase tracking-[0.12em] text-dls-text-muted">A few details</div>
          <DynamicFieldsForm fields={fieldForm.fields} values={fieldValues} errors={fieldErrors} onChange={onFieldChange} />
        </div>
      )}

      {productLine === "Motor" && !useConfiguredFields && (
        <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-6">
          <div className="mb-4 text-xs font-bold uppercase tracking-[0.12em] text-dls-text-muted">Vehicle details</div>

          <UIField label="Vehicle registration" htmlFor={vehicleRegistrationId}>
            <Input
              id={vehicleRegistrationId}
              value={motor.vehicleRegistration}
              onChange={(event) => onMotorChange({ vehicleRegistration: event.target.value })}
              placeholder="e.g. UBA 123X"
            />
          </UIField>

          <div className="mt-5">
            <UIField label="Where did it happen?" htmlFor={accidentLocationId}>
              <Input
                id={accidentLocationId}
                value={motor.accidentLocation}
                onChange={(event) => onMotorChange({ accidentLocation: event.target.value })}
                placeholder="e.g. Jinja Road, near the roundabout"
              />
            </UIField>
          </div>

          <div className="mt-5 grid grid-cols-2 gap-4">
            <UIField label="Was a third party involved?" htmlFor={thirdPartyId}>
              <Select id={thirdPartyId} value={motor.thirdPartyInvolved} onChange={(event) => onMotorChange({ thirdPartyInvolved: event.target.value as YesNoBlank })} className="w-full">
                <option value="">Not sure</option>
                <option value="Yes">Yes</option>
                <option value="No">No</option>
              </Select>
            </UIField>
            <UIField label="Was anyone injured?" htmlFor={injuryId}>
              <Select id={injuryId} value={motor.injuryIndicator} onChange={(event) => onMotorChange({ injuryIndicator: event.target.value as YesNoBlank })} className="w-full">
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
        disabled={submitting || previewOnly}
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
        {previewOnly ? "Preview only. Nothing is saved or sent. " : ""}After you submit, you can upload supporting documents and track progress from the same link.
      </p>
    </form>
  );
}
