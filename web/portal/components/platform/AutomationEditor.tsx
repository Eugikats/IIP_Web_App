"use client";

/** The fields of an automation: its logic (template fields), schedule and limits. Used by the wizard and detail page. */

import type { AutomationTemplate } from "@/lib/platform/types";
import { inputClass, labelClass } from "./ui";

export type AutomationDraft = {
  name: string;
  objective: string;
  logic: Record<string, unknown>;
  trigger: { type: string; schedule: string; time: string };
  maxItemsPerRun: number;
};

export function draftFromTemplate(template: AutomationTemplate): AutomationDraft {
  return {
    name: template.name,
    objective: template.objective,
    logic: Object.fromEntries(template.fields.map((f) => [f.key, f.default])),
    trigger: { type: template.defaultTrigger, schedule: template.defaultSchedule, time: "07:00" },
    maxItemsPerRun: 25,
  };
}

export function draftToRequest(draft: AutomationDraft) {
  return {
    name: draft.name,
    objective: draft.objective,
    logic: draft.logic,
    trigger: draft.trigger,
    policy: { requireApproval: true, maxItemsPerRun: draft.maxItemsPerRun },
  };
}

export default function AutomationEditor({
  template,
  draft,
  onChange,
  disabled,
}: {
  template: AutomationTemplate | undefined;
  draft: AutomationDraft;
  onChange: (draft: AutomationDraft) => void;
  disabled?: boolean;
}) {
  const setLogic = (key: string, value: unknown) => onChange({ ...draft, logic: { ...draft.logic, [key]: value } });
  return (
    <div className="grid gap-4 md:grid-cols-2">
      <div className="md:col-span-2">
        <label className={labelClass} htmlFor="auto-name">Name</label>
        <input id="auto-name" className={inputClass} disabled={disabled} value={draft.name} onChange={(e) => onChange({ ...draft, name: e.target.value })} />
      </div>
      <div className="md:col-span-2">
        <label className={labelClass} htmlFor="auto-objective">Objective</label>
        <textarea id="auto-objective" rows={2} className={inputClass} disabled={disabled} value={draft.objective} onChange={(e) => onChange({ ...draft, objective: e.target.value })} />
      </div>

      {(template?.fields ?? []).map((field) => (
        <div key={field.key} className={field.kind === "text" || field.kind === "textarea" ? "md:col-span-2" : ""}>
          <label className={labelClass} htmlFor={`f-${field.key}`}>
            {field.label}
            {field.unit ? <span className="font-normal text-dls-text-muted"> ({field.unit})</span> : null}
          </label>
          {field.kind === "number" ? (
            <input
              id={`f-${field.key}`}
              type="number"
              className={inputClass}
              disabled={disabled}
              value={String(draft.logic[field.key] ?? "")}
              onChange={(e) => setLogic(field.key, e.target.value === "" ? "" : Number(e.target.value))}
            />
          ) : field.kind === "time" ? (
            <input id={`f-${field.key}`} type="time" className={inputClass} disabled={disabled} value={String(draft.logic[field.key] ?? "")} onChange={(e) => setLogic(field.key, e.target.value)} />
          ) : (
            <textarea id={`f-${field.key}`} rows={3} className={inputClass} disabled={disabled} value={String(draft.logic[field.key] ?? "")} onChange={(e) => setLogic(field.key, e.target.value)} />
          )}
          {field.help ? <p className="mt-1 text-[11px] text-dls-text-muted">{field.help}</p> : null}
        </div>
      ))}

      <div>
        <label className={labelClass} htmlFor="auto-schedule">Runs</label>
        <select
          id="auto-schedule"
          className={inputClass}
          disabled={disabled}
          value={draft.trigger.type === "manual" ? "manual" : draft.trigger.schedule}
          onChange={(e) =>
            onChange({
              ...draft,
              trigger: e.target.value === "manual" ? { ...draft.trigger, type: "manual" } : { ...draft.trigger, type: "schedule", schedule: e.target.value },
            })
          }
        >
          <option value="hourly">Every hour</option>
          <option value="daily">Daily</option>
          <option value="weekly">Weekly (Mondays)</option>
          <option value="manual">Only when run by hand</option>
        </select>
      </div>
      {draft.trigger.type !== "manual" && draft.trigger.schedule !== "hourly" ? (
        <div>
          <label className={labelClass} htmlFor="auto-time">At (UTC)</label>
          <input id="auto-time" type="time" className={inputClass} disabled={disabled} value={draft.trigger.time} onChange={(e) => onChange({ ...draft, trigger: { ...draft.trigger, time: e.target.value } })} />
        </div>
      ) : (
        <div />
      )}
      <div>
        <label className={labelClass} htmlFor="auto-max">Most items raised per run</label>
        <input
          id="auto-max"
          type="number"
          min={1}
          max={200}
          className={inputClass}
          disabled={disabled}
          value={draft.maxItemsPerRun}
          onChange={(e) => onChange({ ...draft, maxItemsPerRun: Math.max(1, Math.min(200, Number(e.target.value) || 1)) })}
        />
      </div>
    </div>
  );
}
