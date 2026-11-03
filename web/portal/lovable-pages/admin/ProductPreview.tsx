"use client";

import { useCallback, useEffect, useState } from "react";
import type { FormEvent } from "react";
import { useParams } from "next/navigation";
import { GlassCard, SectionHeader } from "@/components/iip/Primitives";
import { ClaimantDocumentsCard } from "@/components/iip/ClaimantDocumentsCard";
import { ClaimantNewClaimForm, type BuiltInMotorAnswers } from "@/components/iip/ClaimantNewClaimForm";
import { Button } from "@/components/ui/Button";
import { Select } from "@/components/ui/Input";
import { StatusPill } from "@/components/ui/StatusPill";
import { ErrorStateSurface, LoadingSkeleton } from "@/components/ui/StateSurfaces";
import { useUI } from "@/context/UIStateContext";
import { loadCustomerPreview, type CustomerPreview } from "@/lib/customerPreview";

type Step = "start" | "documents";
type Width = "phone" | "wide";

const SAMPLE_DESCRIPTION = "Sample: another vehicle hit mine while I was parked outside the shop.";
const SAMPLE_MOTOR: BuiltInMotorAnswers = { vehicleRegistration: "", accidentLocation: "", thirdPartyInvolved: "", injuryIndicator: "" };

/**
 * "Preview as customer": the claimant's own pages for a chosen claim type, filled with sample
 * details and unable to save or send anything. It renders the same components the customer sees, so
 * it cannot drift, and works for a draft that is not published yet.
 */
export default function ProductPreview() {
  const params = useParams<{ productId: string }>();
  const productId = decodeURIComponent(params.productId);
  const { roleDef } = useUI();
  const role = roleDef.key;

  const [preview, setPreview] = useState<CustomerPreview | null>(null);
  const [claimType, setClaimType] = useState<string | undefined>(undefined);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [step, setStep] = useState<Step>("start");
  const [width, setWidth] = useState<Width>("phone");
  const [fieldValues, setFieldValues] = useState<Record<string, string>>({});
  const [incidentDate, setIncidentDate] = useState("");
  const [incidentDescription, setIncidentDescription] = useState(SAMPLE_DESCRIPTION);
  const [motor, setMotor] = useState<BuiltInMotorAnswers>(SAMPLE_MOTOR);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    const result = await loadCustomerPreview(role, productId, claimType);
    setLoading(false);
    if (!result.ok) {
      setError(result.error);
      return;
    }
    setPreview(result.value);
    setFieldValues({});
  }, [role, productId, claimType]);

  useEffect(() => { void load(); }, [load]);

  const noop = () => undefined;
  const claimTypeNames = preview?.claimTypes.map((type) => type.name) ?? [];

  return (
    <>
      <SectionHeader
        eyebrow="Admin · Products"
        title="Preview as customer"
        sub="What a customer sees when they start a claim for this product. The details are samples and nothing here is saved or sent."
        breadcrumb={[
          { label: "Products", href: "/app/admin/products" },
          { label: preview?.productName ?? "Product", href: `/app/admin/products/${productId}` },
          { label: "Preview as customer" },
        ]}
      />

      {loading && !preview && <GlassCard><LoadingSkeleton rows={5} /></GlassCard>}
      {error && <GlassCard><ErrorStateSurface title="The preview could not be loaded" message={error} onRetry={load} /></GlassCard>}

      {preview && (
        <div className="space-y-4" data-testid="customer-preview">
          <GlassCard>
            <div className="flex flex-wrap items-end gap-4">
              <div className="min-w-[12rem]">
                <label htmlFor="preview-claim-type" className="mb-1 block text-[12px] font-semibold text-dls-text-secondary">Claim type</label>
                {claimTypeNames.length > 0 ? (
                  <Select id="preview-claim-type" value={preview.claimTypeName} onChange={(event) => setClaimType(event.target.value)} className="w-full">
                    {claimTypeNames.map((name) => <option key={name} value={name}>{name}</option>)}
                  </Select>
                ) : (
                  <div className="text-[12.5px] text-dls-text-secondary">This product has no claim types yet, so the built-in questions and documents are shown.</div>
                )}
              </div>
              <div role="group" aria-label="Preview width" className="flex gap-2">
                <Button variant={width === "phone" ? "secondary" : "ghost"} onClick={() => setWidth("phone")} aria-pressed={width === "phone"}>Phone</Button>
                <Button variant={width === "wide" ? "secondary" : "ghost"} onClick={() => setWidth("wide")} aria-pressed={width === "wide"}>Wide</Button>
              </div>
              <div role="group" aria-label="Step" className="flex gap-2">
                <Button variant={step === "start" ? "secondary" : "ghost"} onClick={() => setStep("start")} aria-pressed={step === "start"}>1. Start the claim</Button>
                <Button variant={step === "documents" ? "secondary" : "ghost"} onClick={() => setStep("documents")} aria-pressed={step === "documents"}>2. Send documents</Button>
              </div>
              <StatusPill
                tone={preview.isPublished ? "success" : "warning"}
                label={preview.isPublished ? "Published, customers see this" : `${preview.status === "Draft" ? "Draft" : preview.status}, customers do not see this yet`}
              />
            </div>
          </GlassCard>

          <div
            className={`mx-auto overflow-hidden rounded-dls-card border border-dls-border bg-dls-bg-page ${width === "phone" ? "w-[390px] max-w-full" : "w-full max-w-3xl"}`}
            data-testid="customer-preview-frame"
            data-width={width}
          >
            <div className="border-b border-dls-border-subtle bg-dls-surface px-5 py-4">
              <div className="text-sm font-semibold text-dls-text-primary">{step === "start" ? "Start your claim" : "Your claim"}</div>
              <div className="text-xs text-dls-text-muted">Policy {preview.samplePolicyNumber} (sample)</div>
            </div>
            <div className="space-y-5 px-4 py-5">
              {step === "start" ? (
                <ClaimantNewClaimForm
                  policyNumber={preview.samplePolicyNumber}
                  productLine={preview.productClass}
                  claimType={preview.claimTypeName}
                  claimTypeOptions={claimTypeNames.length > 0 ? claimTypeNames : [preview.claimTypeName || "Claim"]}
                  onClaimTypeChange={(value) => setClaimType(value)}
                  incidentDate={incidentDate}
                  onIncidentDateChange={setIncidentDate}
                  incidentDescription={incidentDescription}
                  onIncidentDescriptionChange={setIncidentDescription}
                  fieldForm={preview.form}
                  fieldValues={fieldValues}
                  fieldErrors={{}}
                  onFieldChange={(key, value) => setFieldValues((current) => ({ ...current, [key]: value }))}
                  motor={motor}
                  onMotorChange={(patch) => setMotor((current) => ({ ...current, ...patch }))}
                  submitting={false}
                  error={null}
                  onSubmit={(event: FormEvent) => event.preventDefault()}
                  previewOnly
                />
              ) : preview.documents.length > 0 ? (
                <ClaimantDocumentsCard documents={preview.documents} busyKey={null} notice={null} onUpload={noop} onRemove={noop} onRetry={noop} previewOnly />
              ) : (
                <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-6 text-xs leading-5 text-dls-text-muted">
                  No documents are asked for this claim type. The customer is only offered "Something else".
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </>
  );
}
