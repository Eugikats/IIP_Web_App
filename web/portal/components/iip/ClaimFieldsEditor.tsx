"use client";

import { ArrowDown, ArrowUp, Plus, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import type { ClaimFieldConfig, ClaimTypeConfig } from "@/lib/products";

const FIELD_TYPES: { value: ClaimFieldConfig["type"]; label: string }[] = [
  { value: "Text", label: "Short text" },
  { value: "Number", label: "Number" },
  { value: "Currency", label: "Amount of money" },
  { value: "Date", label: "Date" },
  { value: "Phone", label: "Phone number" },
  { value: "YesNo", label: "Yes or no" },
  { value: "Select", label: "Choose from a list" },
];

type ClaimFieldsEditorProps = {
  value: ClaimFieldConfig[];
  onChange: (next: ClaimFieldConfig[]) => void;
  claimTypes: ClaimTypeConfig[];
  disabled?: boolean;
};

// The list order is the order the questions are asked in; the server numbers the list as it
// receives it, so moving a row is the only way to reorder.
function renumber(items: ClaimFieldConfig[]): ClaimFieldConfig[] {
  return items.map((item, index) => ({ ...item, sortOrder: index }));
}

export function ClaimFieldsEditor({ value, onChange, claimTypes, disabled = false }: ClaimFieldsEditorProps) {
  const savedTypes = claimTypes.filter((type): type is ClaimTypeConfig & { key: string } => Boolean(type.key));

  function update(index: number, patch: Partial<ClaimFieldConfig>) {
    onChange(value.map((item, i) => (i === index ? { ...item, ...patch } : item)));
  }

  function move(index: number, delta: -1 | 1) {
    const target = index + delta;
    if (target < 0 || target >= value.length) return;
    const next = [...value];
    [next[index], next[target]] = [next[target], next[index]];
    onChange(renumber(next));
  }

  return (
    <div data-testid="claim-fields-editor">
      <div className="mb-1 flex items-center justify-between">
        <div className="text-[14px] font-bold text-dls-text-primary">Claim questions</div>
        {!disabled && (
          <Button
            variant="secondary"
            icon={<Plus aria-hidden="true" />}
            onClick={() => onChange(renumber([...value, { key: null, label: "", type: "Text", required: false, options: [], helpText: null, sortOrder: value.length, customerVisible: true, claimTypeKeys: [] }]))}
          >
            Add question
          </Button>
        )}
      </div>
      <p className="mb-3 text-[12px] text-dls-text-secondary">
        What is asked about a claim, in the order it is asked. A question can apply to every claim type or only to some. Questions marked for staff only are never shown to the customer.
      </p>
      <div className="space-y-3">
        {value.length === 0 && (
          <p className="text-[12.5px] text-dls-text-secondary">No questions yet. Until some are added, the built-in questions for the product line are used.</p>
        )}
        {value.map((field, index) => (
          <div key={field.key ?? `new-${index}`} className="rounded-dls-card border border-dls-border-subtle bg-dls-bg-page p-3">
            <div className="grid gap-2 sm:grid-cols-6">
              <Input className="sm:col-span-3" value={field.label} onChange={(e) => update(index, { label: e.target.value })} disabled={disabled} aria-label={`Question ${index + 1} label`} placeholder="e.g. Police report number" />
              <select
                className="rounded-dls-control border border-dls-border-input bg-dls-surface px-2 py-[9px] text-sm text-dls-text-primary sm:col-span-2"
                value={field.type}
                disabled={disabled}
                onChange={(e) => update(index, { type: e.target.value as ClaimFieldConfig["type"] })}
                aria-label={`Question ${index + 1} type`}
              >
                {FIELD_TYPES.map((type) => <option key={type.value} value={type.value}>{type.label}</option>)}
              </select>
              <div className="text-[11px] text-dls-text-muted sm:col-span-1 sm:self-center">{field.key ? `Key: ${field.key}` : ""}</div>
              <Input className="sm:col-span-6" value={field.helpText ?? ""} onChange={(e) => update(index, { helpText: e.target.value || null })} disabled={disabled} aria-label={`Question ${index + 1} help text`} placeholder="Help text shown under the question (optional)" />
              {field.type === "Select" && (
                <Input
                  className="sm:col-span-6"
                  value={(field.options ?? []).join(", ")}
                  onChange={(e) => update(index, { options: e.target.value.split(",").map((option) => option.trim()).filter(Boolean) })}
                  disabled={disabled}
                  aria-label={`Question ${index + 1} choices`}
                  placeholder="The choices, separated by commas"
                />
              )}
            </div>
            {savedTypes.length > 0 && (
              <fieldset className="mt-2" disabled={disabled}>
                <legend className="text-[11.5px] font-semibold text-dls-text-secondary">Asked for</legend>
                <div className="mt-1 flex flex-wrap gap-x-4 gap-y-1">
                  <label className="flex items-center gap-2 text-[12px] text-dls-text-secondary">
                    <input type="radio" name={`field-applies-${index}`} checked={!field.claimTypeKeys?.length} onChange={() => update(index, { claimTypeKeys: [] })} />
                    Every claim type
                  </label>
                  {savedTypes.map((type) => (
                    <label key={type.key} className="flex items-center gap-2 text-[12px] text-dls-text-secondary">
                      <input
                        type="checkbox"
                        className="h-3.5 w-3.5"
                        checked={field.claimTypeKeys?.includes(type.key) ?? false}
                        onChange={(e) => {
                          const current = field.claimTypeKeys ?? [];
                          update(index, { claimTypeKeys: e.target.checked ? [...current, type.key] : current.filter((key) => key !== type.key) });
                        }}
                      />
                      {type.name}
                    </label>
                  ))}
                </div>
              </fieldset>
            )}
            <div className="mt-2 flex flex-wrap items-center justify-between gap-2">
              <div className="flex flex-wrap gap-4">
                <label className="flex items-center gap-2 text-[12px] text-dls-text-secondary">
                  <input type="checkbox" className="h-3.5 w-3.5" checked={field.required} disabled={disabled} onChange={(e) => update(index, { required: e.target.checked })} />
                  Required
                </label>
                <label className="flex items-center gap-2 text-[12px] text-dls-text-secondary">
                  <input type="checkbox" className="h-3.5 w-3.5" checked={!field.customerVisible} disabled={disabled} onChange={(e) => update(index, { customerVisible: !e.target.checked })} />
                  Staff only
                </label>
              </div>
              {!disabled && (
                <div className="flex items-center gap-1">
                  <Button variant="ghost" icon={<ArrowUp aria-hidden="true" />} onClick={() => move(index, -1)} disabled={index === 0} aria-label={`Move ${field.label || index + 1} up`}>Up</Button>
                  <Button variant="ghost" icon={<ArrowDown aria-hidden="true" />} onClick={() => move(index, 1)} disabled={index === value.length - 1} aria-label={`Move ${field.label || index + 1} down`}>Down</Button>
                  <Button variant="ghost" icon={<Trash2 aria-hidden="true" />} onClick={() => onChange(renumber(value.filter((_, i) => i !== index)))} aria-label={`Remove question ${field.label || index + 1}`}>Remove</Button>
                </div>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
