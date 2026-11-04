"use client";

import { useCallback, useEffect, useState } from "react";
import { Pencil } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { DynamicFieldsForm } from "@/components/iip/DynamicFieldsForm";
import { fetchClaimFields, saveClaimFieldValues, type ClaimFieldForm } from "@/lib/claimFields";

type ClaimDetailsCardProps = {
  caseId: string;
  role: string;
  canEdit: boolean;
  /** Called after a save, so the page can refresh anything derived from the claim. */
  onSaved?: () => void;
};

/**
 * The answers to the questions the claim's product asks (build-loop item 5): read as tiles, edited
 * with the same renderer used to file the claim. Works for every product line, including claims
 * filed before fields existed (their original answers are read through the built-in questions).
 */
export function ClaimDetailsCard({ caseId, role, canEdit, onSaved }: ClaimDetailsCardProps) {
  const [form, setForm] = useState<ClaimFieldForm | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState<Record<string, string>>({});
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoadError(null);
    const result = await fetchClaimFields(role, caseId);
    if (!result.ok) {
      setLoadError(result.error);
      return;
    }
    setForm(result.value);
  }, [role, caseId]);

  useEffect(() => { void load(); }, [load]);

  function startEditing() {
    if (!form) return;
    setDraft(Object.fromEntries(form.fields.map((field) => [field.key, field.value ?? ""])));
    setErrors({});
    setSaveError(null);
    setEditing(true);
  }

  async function save() {
    if (!form) return;
    setSaving(true);
    setSaveError(null);
    const changed = Object.fromEntries(
      form.fields
        .filter((field) => (draft[field.key] ?? "").trim() !== (field.value ?? ""))
        .filter((field) => (draft[field.key] ?? "").trim() !== "")
        .map((field) => [field.key, draft[field.key].trim()]),
    );
    if (Object.keys(changed).length === 0) {
      setSaving(false);
      setEditing(false);
      return;
    }

    const result = await saveClaimFieldValues(role, caseId, changed);
    setSaving(false);
    if (!result.ok) {
      setErrors(result.fieldErrors);
      setSaveError(result.error);
      return;
    }
    setForm(result.value);
    setEditing(false);
    onSaved?.();
  }

  return (
    <div data-testid="claim-details-card">
      <div className="mb-3 flex items-center justify-between">
        <div className="text-[14px] font-bold text-dls-text-primary">Claim details</div>
        {canEdit && form && form.fields.length > 0 && !editing && (
          <Button variant="secondary" icon={<Pencil aria-hidden="true" />} onClick={startEditing}>Edit details</Button>
        )}
      </div>
      {loadError && (
        <div role="alert" className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4 text-[12.5px] text-dls-danger-text">
          {loadError} <button type="button" className="font-semibold underline" onClick={() => void load()}>Try again</button>
        </div>
      )}
      {!loadError && !form && <div className="text-[12.5px] text-dls-text-muted">Loading the claim details...</div>}
      {form && form.fields.length === 0 && (
        <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4 text-[12.5px] text-dls-text-muted">
          This product does not ask any further questions about a claim of this type.
        </div>
      )}
      {form && form.fields.length > 0 && !editing && (
        <dl className="grid gap-2 sm:grid-cols-2">
          {form.fields.map((field) => (
            <div key={field.key} className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3">
              <dt className="text-[11px] font-semibold uppercase tracking-[0.08em] text-dls-text-muted">
                {field.label}
                {!field.customerVisible && <span className="ml-2 normal-case tracking-normal">(staff only)</span>}
              </dt>
              <dd className="mt-1 text-[13px] text-dls-text-primary">
                {field.value ?? <span className="text-dls-text-muted">{field.required ? "Needed, not given yet" : "Not given"}</span>}
              </dd>
              {field.value && field.source && (
                <p className="mt-1 text-[11px] text-dls-text-muted" data-testid="field-source">
                  {field.source === "Document"
                    ? `Read from a document${field.sourcePage ? `, page ${field.sourcePage}` : ""}${typeof field.sourceConfidence === "number" ? `, ${Math.round(field.sourceConfidence * 100)}% sure` : ""}, confirmed by ${field.confirmedBy}`
                    : field.source === "Customer" ? "Given by the customer" : `Entered by ${field.confirmedBy}`}
                </p>
              )}
            </div>
          ))}
        </dl>
      )}
      {form && editing && (
        <div>
          <DynamicFieldsForm
            fields={form.fields}
            values={draft}
            errors={errors}
            onChange={(key, value) => setDraft((current) => ({ ...current, [key]: value }))}
            disabled={saving}
            showStaffOnlyBadge
          />
          {saveError && <p role="alert" className="mt-3 text-[12px] text-dls-danger-text">{saveError}</p>}
          <div className="mt-3 flex gap-2">
            <Button variant="primary" onClick={() => void save()} disabled={saving}>{saving ? "Saving..." : "Save details"}</Button>
            <Button variant="ghost" onClick={() => setEditing(false)} disabled={saving}>Cancel</Button>
          </div>
        </div>
      )}
    </div>
  );
}
