"use client";

import { useCallback, useEffect, useId, useMemo, useState } from "react";
import { useParams } from "next/navigation";
import { AlertTriangle, History, Plus, Trash2 } from "lucide-react";
import { GovernedActionDialog } from "@/components/GovernedActionDialog";
import { GlassCard, SectionHeader } from "@/components/iip/Primitives";
import { StatusPill, type PillTone } from "@/components/ui/StatusPill";
import { Button } from "@/components/ui/Button";
import { Input, Field } from "@/components/ui/Input";
import { LoadingSkeleton, ErrorStateSurface } from "@/components/ui/StateSurfaces";
import { useUI } from "@/context/UIStateContext";
import {
  approveProductPublish,
  getProduct,
  listProductVersions,
  rejectProductPublish,
  retireProduct,
  submitProductForPublishApproval,
  updateProductDraft,
  type ClaimStage,
  type DocumentMatrixEntry,
  type Product,
  type ProductStatus,
  type ProductVersionSummary,
} from "@/lib/products";

const STATUS_TONE: Record<ProductStatus, PillTone> = {
  Draft: "neutral",
  PendingPublishApproval: "warning",
  Published: "success",
  Retired: "danger",
};

const STATUS_LABEL: Record<ProductStatus, string> = {
  Draft: "Draft",
  PendingPublishApproval: "Pending publish approval",
  Published: "Published",
  Retired: "Retired",
};

type GovernedAction = "save" | "submit" | "approve" | "reject" | "retire" | null;

export default function ProductDetail() {
  const params = useParams<{ productId: string }>();
  const productId = decodeURIComponent(params.productId);
  const { roleDef } = useUI();
  const role = roleDef.key;

  const [product, setProduct] = useState<Product | null>(null);
  const [versions, setVersions] = useState<ProductVersionSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [showHistory, setShowHistory] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [governedAction, setGovernedAction] = useState<GovernedAction>(null);

  const [name, setName] = useState("");
  const [productClass, setProductClass] = useState("");
  const [iraApprovalReference, setIraApprovalReference] = useState("");
  const [iraApprovalEffectiveDate, setIraApprovalEffectiveDate] = useState("");
  const [stages, setStages] = useState<ClaimStage[]>([]);
  const [documentMatrix, setDocumentMatrix] = useState<DocumentMatrixEntry[]>([]);

  function applyProduct(loaded: Product) {
    setProduct(loaded);
    setName(loaded.name);
    setProductClass(loaded.productClass);
    setIraApprovalReference(loaded.iraApprovalReference ?? "");
    setIraApprovalEffectiveDate(loaded.iraApprovalEffectiveDate ?? "");
    setStages(loaded.stages);
    setDocumentMatrix(loaded.documentMatrix);
  }

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    const [productResult, versionsResult] = await Promise.all([
      getProduct(role, productId),
      listProductVersions(role, productId),
    ]);
    setLoading(false);
    if (!productResult.ok) {
      setLoadError(productResult.error);
      return;
    }
    applyProduct(productResult.value);
    if (versionsResult.ok) setVersions(versionsResult.value);
  }, [role, productId]);

  useEffect(() => { void load(); }, [load]);

  const stageKeys = useMemo(() => stages.map((stage) => stage.stageKey).filter(Boolean), [stages]);
  const canEdit = product?.status !== "Retired";
  const isDirty = product
    ? name !== product.name
      || productClass !== product.productClass
      || iraApprovalReference !== (product.iraApprovalReference ?? "")
      || iraApprovalEffectiveDate !== (product.iraApprovalEffectiveDate ?? "")
      || JSON.stringify(stages) !== JSON.stringify(product.stages)
      || JSON.stringify(documentMatrix) !== JSON.stringify(product.documentMatrix)
    : false;

  async function onConfirmGoverned(reason: string) {
    setActionError(null);
    let result: Awaited<ReturnType<typeof getProduct>>;
    switch (governedAction) {
      case "save":
        result = await updateProductDraft(role, productId, {
          name, productClass, iraApprovalReference: iraApprovalReference || null,
          iraApprovalEffectiveDate: iraApprovalEffectiveDate || null, stages, documentMatrix, changeReason: reason,
        });
        break;
      case "submit":
        result = await submitProductForPublishApproval(role, productId, reason);
        break;
      case "approve":
        result = await approveProductPublish(role, productId, reason);
        break;
      case "reject":
        result = await rejectProductPublish(role, productId, reason);
        break;
      case "retire":
        result = await retireProduct(role, productId, reason);
        break;
      default:
        return;
    }
    if (!result.ok) {
      setActionError(result.error);
      throw new Error(result.error);
    }
    applyProduct(result.value);
    setGovernedAction(null);
    void load();
  }

  function addStage() {
    setStages((prev) => [...prev, {
      stageKey: "", name: "", ownerRole: "", isAutomatic: false, sortOrder: prev.length + 1,
      serviceStandardWorkingDays: null, valueTierStandards: null,
    }]);
  }

  function updateStage(index: number, patch: Partial<ClaimStage>) {
    setStages((prev) => prev.map((stage, i) => (i === index ? { ...stage, ...patch } : stage)));
  }

  function removeStage(index: number) {
    setStages((prev) => prev.filter((_, i) => i !== index));
  }

  function addDocument() {
    setDocumentMatrix((prev) => [...prev, {
      documentKey: "", customerLabel: "", reason: "", isRequired: true,
      requiredAtStageKey: stageKeys[0] ?? "", isRegulatoryOrWordingMandated: false,
    }]);
  }

  function updateDocument(index: number, patch: Partial<DocumentMatrixEntry>) {
    setDocumentMatrix((prev) => prev.map((entry, i) => (i === index ? { ...entry, ...patch } : entry)));
  }

  function removeDocument(index: number) {
    setDocumentMatrix((prev) => prev.filter((_, i) => i !== index));
  }

  const dialogFor: Record<Exclude<GovernedAction, null>, { title: string; impact: string; confirmLabel: string; tone?: "primary" | "danger" }> = {
    save: { title: "Save product configuration", impact: "This creates a new version of this product. An open claim already filed keeps the version that was in force when it was filed.", confirmLabel: "Save" },
    submit: { title: "Submit for publish approval", impact: "This sends the product for a second person to approve before it becomes active for new claims.", confirmLabel: "Submit" },
    approve: { title: "Approve and publish", impact: "This makes the product active for new claims. You cannot approve a product you submitted yourself.", confirmLabel: "Approve and publish" },
    reject: { title: "Reject publish request", impact: "This returns the product to draft so it can be corrected and resubmitted.", confirmLabel: "Reject", tone: "danger" },
    retire: { title: "Retire product", impact: "This stops the product being offered for new claims. Claims already open are not affected.", confirmLabel: "Retire", tone: "danger" },
  };

  return (
    <>
      <SectionHeader
        eyebrow="Admin · Products"
        title={product ? product.name : "Product"}
        sub="A product's claim process, document matrix and IRA approval. Every save is a new version; nothing is overwritten."
        breadcrumb={[{ label: "Products", href: "/app/admin/products" }, { label: product?.name ?? "Product" }]}
      />

      {loading && <GlassCard><LoadingSkeleton rows={6} /></GlassCard>}
      {!loading && loadError && <GlassCard><ErrorStateSurface title="This product could not be loaded" message={loadError} onRetry={load} /></GlassCard>}

      {!loading && !loadError && product && (
        <div className="space-y-4">
          <GlassCard>
            <div className="mb-3 flex flex-wrap items-center gap-2">
              <StatusPill tone={STATUS_TONE[product.status]} label={STATUS_LABEL[product.status]} />
              <span className="text-[12px] text-dls-text-muted">v{product.version} · last updated by {product.updatedByUserId} on {new Date(product.updatedAt).toLocaleString()}</span>
            </div>
            {!product.canPublish && (
              <div className="mb-4 flex items-start gap-2 rounded-dls-control border border-dls-warning-dot bg-dls-warning-tint p-3 text-[12.5px] text-dls-text-primary">
                <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-dls-warning-text" aria-hidden="true" />
                A product cannot be activated without an IRA approval reference, its effective date, and at least one claim stage.
              </div>
            )}
            {product.status === "PendingPublishApproval" && (
              <div className="mb-4 rounded-dls-control border border-dls-border-subtle bg-dls-bg-page p-3 text-[12.5px] text-dls-text-secondary">
                Submitted by {product.publishSubmittedByUserId} on {product.publishSubmittedAt ? new Date(product.publishSubmittedAt).toLocaleString() : ""}. A different person must approve it.
              </div>
            )}
            {product.status === "Draft" && product.publishRejectionReason && (
              <div className="mb-4 rounded-dls-control border border-dls-danger-dot bg-dls-danger-tint p-3 text-[12.5px] text-dls-danger-text">
                Publish request rejected by {product.publishRejectedByUserId}: {product.publishRejectionReason}
              </div>
            )}

            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="Product name">
                <Input value={name} onChange={(e) => setName(e.target.value)} disabled={!canEdit} />
              </Field>
              <Field label="Product class">
                <Input value={productClass} onChange={(e) => setProductClass(e.target.value)} disabled={!canEdit} />
              </Field>
              <Field label="IRA approval reference" hint="Required before this product can be published.">
                <Input value={iraApprovalReference} onChange={(e) => setIraApprovalReference(e.target.value)} disabled={!canEdit} placeholder="e.g. IRA/2026/0042" />
              </Field>
              <Field label="IRA approval effective date">
                <Input type="date" value={iraApprovalEffectiveDate} onChange={(e) => setIraApprovalEffectiveDate(e.target.value)} disabled={!canEdit} />
              </Field>
            </div>
          </GlassCard>

          <GlassCard>
            <div className="mb-1 flex items-center justify-between">
              <div className="text-[14px] font-bold text-dls-text-primary">Claim process</div>
              {canEdit && <Button variant="secondary" icon={<Plus aria-hidden="true" />} onClick={addStage}>Add stage</Button>}
            </div>
            <p className="mb-3 text-[12px] text-dls-text-secondary">Ordered stages, each with an owner role and a service standard in working days.</p>
            <div className="space-y-3">
              {stages.length === 0 && <p className="text-[12.5px] text-dls-text-secondary">No stages configured. A product cannot be published without at least one.</p>}
              {stages.map((stage, index) => (
                <div key={index} className="rounded-dls-card border border-dls-border-subtle bg-dls-bg-page p-3">
                  <div className="grid gap-2 sm:grid-cols-6">
                    <Input className="sm:col-span-1" value={String(stage.sortOrder)} onChange={(e) => updateStage(index, { sortOrder: Number(e.target.value.replace(/[^0-9]/g, "")) || 0 })} disabled={!canEdit} aria-label="Order" placeholder="Order" />
                    <Input className="sm:col-span-2" value={stage.name} onChange={(e) => updateStage(index, { name: e.target.value })} disabled={!canEdit} aria-label="Stage name" placeholder="Stage name" />
                    <Input className="sm:col-span-1" value={stage.stageKey} onChange={(e) => updateStage(index, { stageKey: e.target.value })} disabled={!canEdit} aria-label="Stage key" placeholder="stage_key" />
                    <Input className="sm:col-span-1" value={stage.ownerRole} onChange={(e) => updateStage(index, { ownerRole: e.target.value })} disabled={!canEdit} aria-label="Owner role" placeholder="Owner role" />
                    <Input
                      className="sm:col-span-1"
                      value={stage.serviceStandardWorkingDays === null ? "" : String(stage.serviceStandardWorkingDays)}
                      onChange={(e) => updateStage(index, { serviceStandardWorkingDays: e.target.value.trim() === "" ? null : Number(e.target.value.replace(/[^0-9]/g, "")) })}
                      disabled={!canEdit}
                      aria-label="Service standard, working days"
                      placeholder="Working days"
                    />
                  </div>
                  <div className="mt-2 flex items-center justify-between">
                    <label className="flex items-center gap-2 text-[12px] text-dls-text-secondary">
                      <input type="checkbox" checked={stage.isAutomatic} disabled={!canEdit} onChange={(e) => updateStage(index, { isAutomatic: e.target.checked })} className="h-3.5 w-3.5" />
                      Runs automatically, no person needed
                    </label>
                    {canEdit && (
                      <Button variant="ghost" icon={<Trash2 aria-hidden="true" />} onClick={() => removeStage(index)} aria-label={`Remove stage ${stage.name || index + 1}`}>Remove</Button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </GlassCard>

          <GlassCard>
            <div className="mb-1 flex items-center justify-between">
              <div className="text-[14px] font-bold text-dls-text-primary">Document matrix</div>
              {canEdit && <Button variant="secondary" icon={<Plus aria-hidden="true" />} onClick={addDocument} disabled={stageKeys.length === 0}>Add document</Button>}
            </div>
            <p className="mb-3 text-[12px] text-dls-text-secondary">What the customer needs to provide, in the customer&apos;s own words, and at which stage.</p>
            <div className="space-y-3">
              {documentMatrix.length === 0 && <p className="text-[12.5px] text-dls-text-secondary">No documents configured yet.</p>}
              {documentMatrix.map((entry, index) => (
                <div key={index} className="rounded-dls-card border border-dls-border-subtle bg-dls-bg-page p-3">
                  <div className="grid gap-2 sm:grid-cols-6">
                    <Input className="sm:col-span-1" value={entry.documentKey} onChange={(e) => updateDocument(index, { documentKey: e.target.value })} disabled={!canEdit} aria-label="Document key" placeholder="document_key" />
                    <Input className="sm:col-span-2" value={entry.customerLabel} onChange={(e) => updateDocument(index, { customerLabel: e.target.value })} disabled={!canEdit} aria-label="What the customer sees" placeholder="e.g. Police abstract" />
                    <Input className="sm:col-span-2" value={entry.reason} onChange={(e) => updateDocument(index, { reason: e.target.value })} disabled={!canEdit} aria-label="Why it is needed" placeholder="Why it is needed" />
                    <select
                      className="rounded-dls-control border border-dls-border-input bg-dls-surface px-2 py-[9px] text-sm text-dls-text-primary sm:col-span-1"
                      value={entry.requiredAtStageKey}
                      disabled={!canEdit}
                      onChange={(e) => updateDocument(index, { requiredAtStageKey: e.target.value })}
                      aria-label="Required at stage"
                    >
                      {stageKeys.map((key) => <option key={key} value={key}>{key}</option>)}
                    </select>
                  </div>
                  <div className="mt-2 flex flex-wrap items-center justify-between gap-2">
                    <div className="flex flex-wrap gap-4">
                      <label className="flex items-center gap-2 text-[12px] text-dls-text-secondary">
                        <input type="checkbox" checked={entry.isRequired} disabled={!canEdit} onChange={(e) => updateDocument(index, { isRequired: e.target.checked })} className="h-3.5 w-3.5" />
                        Required, not optional
                      </label>
                      <label className="flex items-center gap-2 text-[12px] text-dls-text-secondary">
                        <input type="checkbox" checked={entry.isRegulatoryOrWordingMandated} disabled={!canEdit} onChange={(e) => updateDocument(index, { isRegulatoryOrWordingMandated: e.target.checked })} className="h-3.5 w-3.5" />
                        Required by regulator or wording
                      </label>
                    </div>
                    {canEdit && (
                      <Button variant="ghost" icon={<Trash2 aria-hidden="true" />} onClick={() => removeDocument(index)} aria-label={`Remove document ${entry.customerLabel || index + 1}`}>Remove</Button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </GlassCard>

          {actionError && (
            <div className="flex items-start gap-2 rounded-dls-control bg-dls-danger-tint p-3 text-[12px] text-dls-danger-text">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" /> {actionError}
            </div>
          )}

          <GlassCard>
            <div className="flex flex-wrap items-center gap-2">
              {canEdit && (
                <Button variant="primary" onClick={() => setGovernedAction("save")} disabled={!isDirty}>Save changes</Button>
              )}
              {product.status === "Draft" && (
                <Button variant="secondary" onClick={() => setGovernedAction("submit")} disabled={isDirty || !product.canPublish}>Submit for publish approval</Button>
              )}
              {product.status === "PendingPublishApproval" && (
                <>
                  <Button variant="primary" onClick={() => setGovernedAction("approve")}>Approve and publish</Button>
                  <Button variant="destructive" onClick={() => setGovernedAction("reject")}>Reject</Button>
                </>
              )}
              {product.status === "Published" && (
                <Button variant="destructive" onClick={() => setGovernedAction("retire")}>Retire product</Button>
              )}
              <Button variant="ghost" icon={<History aria-hidden="true" />} onClick={() => setShowHistory((v) => !v)}>
                {showHistory ? "Hide" : "Show"} version history ({versions.length})
              </Button>
            </div>
            {isDirty && product.status === "Draft" && (
              <p className="mt-2 text-[12px] text-dls-text-muted">Save your changes before submitting for publish approval.</p>
            )}
          </GlassCard>

          {showHistory && (
            <GlassCard>
              <div className="mb-3 text-[14px] font-bold text-dls-text-primary">Version history</div>
              <div className="space-y-2">
                {versions.map((v) => (
                  <div key={v.version} className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <span className="text-[12.5px] font-bold text-dls-text-primary">v{v.version}</span>
                      <StatusPill tone={STATUS_TONE[v.status]} label={STATUS_LABEL[v.status]} />
                      <span className="text-[11px] text-dls-text-muted">{v.updatedByUserId} · {new Date(v.updatedAt).toLocaleString()}</span>
                    </div>
                    <p className="mt-1 text-[12px] text-dls-text-secondary">{v.changeReason}</p>
                  </div>
                ))}
              </div>
            </GlassCard>
          )}
        </div>
      )}

      {governedAction && (
        <GovernedActionDialog
          open={Boolean(governedAction)}
          title={dialogFor[governedAction].title}
          target={product?.name ?? ""}
          impact={dialogFor[governedAction].impact}
          auditNotice="This change is recorded in the audit trail with your name against it, note why you are making it."
          confirmLabel={dialogFor[governedAction].confirmLabel}
          tone={dialogFor[governedAction].tone}
          onClose={() => setGovernedAction(null)}
          onConfirm={onConfirmGoverned}
        />
      )}
    </>
  );
}
