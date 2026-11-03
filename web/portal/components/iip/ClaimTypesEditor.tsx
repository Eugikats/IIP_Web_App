"use client";

import { ArrowDown, ArrowUp, Plus, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import type { ClaimTypeConfig } from "@/lib/products";

type ClaimTypesEditorProps = {
  value: ClaimTypeConfig[];
  onChange: (next: ClaimTypeConfig[]) => void;
  disabled?: boolean;
  /** Shown beside Add; used for "Import from existing claims". */
  extraAction?: React.ReactNode;
};

// Order in the list is the order shown to the person filing a claim, so moving a row is the only
// way to reorder: the server numbers the list in the order it receives it.
function renumber(items: ClaimTypeConfig[]): ClaimTypeConfig[] {
  return items.map((item, index) => ({ ...item, sortOrder: index }));
}

export function ClaimTypesEditor({ value, onChange, disabled = false, extraAction }: ClaimTypesEditorProps) {
  function update(index: number, patch: Partial<ClaimTypeConfig>) {
    onChange(value.map((item, i) => (i === index ? { ...item, ...patch } : item)));
  }

  function move(index: number, delta: -1 | 1) {
    const target = index + delta;
    if (target < 0 || target >= value.length) return;
    const next = [...value];
    [next[index], next[target]] = [next[target], next[index]];
    onChange(renumber(next));
  }

  function remove(index: number) {
    onChange(renumber(value.filter((_, i) => i !== index)));
  }

  function add() {
    onChange(renumber([...value, { key: null, name: "", description: "", active: true, sortOrder: value.length, aliases: [] }]));
  }

  return (
    <div data-testid="claim-types-editor">
      <div className="mb-1 flex items-center justify-between">
        <div className="text-[14px] font-bold text-dls-text-primary">Claim types</div>
        <div className="flex items-center gap-2">
          {extraAction}
          {!disabled && (
            <Button variant="secondary" icon={<Plus aria-hidden="true" />} onClick={add}>Add claim type</Button>
          )}
        </div>
      </div>
      <p className="mb-3 text-[12px] text-dls-text-secondary">
        The kinds of claim this product handles, in the order people see them. Aliases are other words people use (for example &quot;stolen&quot; for theft) and help match what customers write.
        A claim type that is switched off stays on claims already filed but cannot be chosen for new ones.
      </p>
      <div className="space-y-3">
        {value.length === 0 && (
          <p className="text-[12.5px] text-dls-text-secondary">
            No claim types yet. Until some are added, claims for this product keep a free-text claim type.
          </p>
        )}
        {value.map((type, index) => (
          <div key={type.key ?? `new-${index}`} className="rounded-dls-card border border-dls-border-subtle bg-dls-bg-page p-3">
            <div className="grid gap-2 sm:grid-cols-6">
              <Input
                className="sm:col-span-2"
                value={type.name}
                onChange={(e) => update(index, { name: e.target.value })}
                disabled={disabled}
                aria-label={`Claim type ${index + 1} name`}
                placeholder="e.g. Own damage"
              />
              <Input
                className="sm:col-span-4"
                value={type.description ?? ""}
                onChange={(e) => update(index, { description: e.target.value })}
                disabled={disabled}
                aria-label={`Claim type ${index + 1} description`}
                placeholder="A short description, shown to the person filing the claim"
              />
              <Input
                className="sm:col-span-6"
                value={(type.aliases ?? []).join(", ")}
                onChange={(e) => update(index, { aliases: e.target.value.split(",").map((alias) => alias.trim()).filter(Boolean) })}
                disabled={disabled}
                aria-label={`Claim type ${index + 1} aliases`}
                placeholder="Other words people use, separated by commas"
              />
            </div>
            <div className="mt-2 flex flex-wrap items-center justify-between gap-2">
              <label className="flex items-center gap-2 text-[12px] text-dls-text-secondary">
                <input
                  type="checkbox"
                  checked={type.active}
                  disabled={disabled}
                  onChange={(e) => update(index, { active: e.target.checked })}
                  className="h-3.5 w-3.5"
                />
                Available for new claims
              </label>
              {type.key && <span className="text-[11px] text-dls-text-muted">Key: {type.key}</span>}
              {!disabled && (
                <div className="flex items-center gap-1">
                  <Button variant="ghost" icon={<ArrowUp aria-hidden="true" />} onClick={() => move(index, -1)} disabled={index === 0} aria-label={`Move ${type.name || index + 1} up`}>Up</Button>
                  <Button variant="ghost" icon={<ArrowDown aria-hidden="true" />} onClick={() => move(index, 1)} disabled={index === value.length - 1} aria-label={`Move ${type.name || index + 1} down`}>Down</Button>
                  <Button variant="ghost" icon={<Trash2 aria-hidden="true" />} onClick={() => remove(index)} aria-label={`Remove claim type ${type.name || index + 1}`}>Remove</Button>
                </div>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
