"use client";

import { useId } from "react";
import { Input, Select, Textarea } from "@/components/ui/Input";
import type { ClaimFieldValue } from "@/lib/claimFields";

type DynamicFieldsFormProps = {
  fields: ClaimFieldValue[];
  values: Record<string, string>;
  onChange: (key: string, value: string) => void;
  errors?: Record<string, string>;
  disabled?: boolean;
  /** Marks staff-only fields so a person editing a claim can tell them apart. */
  showStaffOnlyBadge?: boolean;
};

/**
 * One renderer for every question a product asks about a claim, used by the staff new-claim form,
 * the claimant new-claim page and the claim details editor. It knows nothing about any product: it
 * draws what the definitions say (label, help text, type, choices, whether it is required).
 */
export function DynamicFieldsForm({ fields, values, onChange, errors = {}, disabled = false, showStaffOnlyBadge = false }: DynamicFieldsFormProps) {
  const baseId = useId();
  return (
    <div className="grid gap-4 md:grid-cols-2" data-testid="dynamic-fields-form">
      {fields.map((field) => {
        const id = `${baseId}-${field.key}`;
        const error = errors[field.key];
        const value = values[field.key] ?? "";
        const describedBy = [field.helpText ? `${id}-help` : null, error ? `${id}-error` : null].filter(Boolean).join(" ") || undefined;
        return (
          <div key={field.key} className={field.type === "Text" && field.label.length > 40 ? "md:col-span-2" : undefined}>
            <label htmlFor={id} className="mb-1 block text-[12.5px] font-semibold text-dls-text-primary">
              {field.label}
              {field.required && <span className="ml-1 text-dls-danger-text" aria-hidden="true">*</span>}
              {showStaffOnlyBadge && !field.customerVisible && (
                <span className="ml-2 rounded-dls-pill border border-dls-border-subtle px-1.5 py-0.5 text-[10px] font-semibold text-dls-text-muted">Staff only</span>
              )}
            </label>
            {field.type === "Select" ? (
              <Select id={id} value={value} onChange={(event) => onChange(field.key, event.target.value)} disabled={disabled} aria-describedby={describedBy} aria-required={field.required} className="w-full">
                <option value="">Choose one</option>
                {field.options.map((option) => (
                  <option key={option} value={option}>{option}</option>
                ))}
              </Select>
            ) : field.type === "YesNo" ? (
              <Select id={id} value={value} onChange={(event) => onChange(field.key, event.target.value)} disabled={disabled} aria-describedby={describedBy} aria-required={field.required} className="w-full">
                <option value="">Choose one</option>
                <option value="Yes">Yes</option>
                <option value="No">No</option>
              </Select>
            ) : field.type === "Text" && field.label.length > 40 ? (
              <Textarea id={id} value={value} onChange={(event) => onChange(field.key, event.target.value)} disabled={disabled} aria-describedby={describedBy} aria-required={field.required} rows={3} />
            ) : (
              <Input
                id={id}
                type={field.type === "Date" ? "date" : field.type === "Phone" ? "tel" : "text"}
                inputMode={field.type === "Number" || field.type === "Currency" ? "decimal" : field.type === "Phone" ? "tel" : undefined}
                value={value}
                onChange={(event) => onChange(field.key, event.target.value)}
                disabled={disabled}
                aria-describedby={describedBy}
                aria-required={field.required}
                aria-invalid={error ? true : undefined}
              />
            )}
            {field.helpText && <p id={`${id}-help`} className="mt-1 text-[11.5px] leading-4 text-dls-text-muted">{field.helpText}</p>}
            {error && <p id={`${id}-error`} role="alert" className="mt-1 text-[11.5px] leading-4 text-dls-danger-text">{error}</p>}
          </div>
        );
      })}
    </div>
  );
}
