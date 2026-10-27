"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type TemplateStatus = "Draft" | "AwaitingApproval" | "Approved" | "Rejected" | "Paused";
export type TemplateCategory = "Utility" | "Marketing" | "Authentication";

export type MessageTemplate = {
  name: string;
  language: string;
  category: TemplateCategory;
  status: TemplateStatus;
  purpose: string;
  body: string;
  parameters: string[];
  buttons: string[];
  decisionNote: string | null;
  decidedAt: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
  isSendable: boolean;
  isEditable: boolean;
  isStandardDraft: boolean;
  problems: string[];
};

export type TemplateEdit = {
  category: TemplateCategory;
  purpose: string;
  body: string;
  parameters: string[];
  buttons: string[];
};

export type TemplateResult<T> = { ok: true; value: T } | { ok: false; error: string };

async function failure(response: Response, fallback: string): Promise<TemplateResult<never>> {
  try {
    const payload = (await response.json()) as { detail?: string; message?: string; errors?: Record<string, string[]> };
    const first = payload.errors ? Object.values(payload.errors).flat()[0] : null;
    return { ok: false, error: payload.detail || payload.message || first || fallback };
  } catch {
    return { ok: false, error: fallback };
  }
}

const NETWORK: TemplateResult<never> = { ok: false, error: "The service could not be reached. Check your connection and try again." };

const base = (role: string) => ({ "Content-Type": "application/json", ...scopedHeaders(role, "") });
const path = (template: Pick<MessageTemplate, "name" | "language">) =>
  `${apiBaseUrl}/api/v1/templates/${encodeURIComponent(template.name)}/${encodeURIComponent(template.language)}`;

export async function listTemplates(role: string): Promise<TemplateResult<MessageTemplate[]>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/templates`, { headers: base(role) });
    return response.ok ? { ok: true, value: (await response.json()) as MessageTemplate[] } : failure(response, "The templates could not be loaded.");
  } catch {
    return NETWORK;
  }
}

export async function saveTemplateDraft(role: string, template: Pick<MessageTemplate, "name" | "language">, edit: TemplateEdit) {
  try {
    const response = await fetch(path(template), { method: "PUT", headers: base(role), body: JSON.stringify(edit) });
    return response.ok ? ({ ok: true, value: (await response.json()) as MessageTemplate } as const) : failure(response, "The draft could not be saved.");
  } catch {
    return NETWORK;
  }
}

export async function submitTemplate(role: string, template: Pick<MessageTemplate, "name" | "language">) {
  try {
    const response = await fetch(`${path(template)}/submit`, { method: "POST", headers: base(role) });
    return response.ok ? ({ ok: true, value: (await response.json()) as MessageTemplate } as const) : failure(response, "The template could not be submitted.");
  } catch {
    return NETWORK;
  }
}

export async function decideTemplate(
  role: string,
  template: Pick<MessageTemplate, "name" | "language">,
  decision: "Approved" | "Rejected" | "Paused",
  note: string,
) {
  try {
    const response = await fetch(`${path(template)}/decision`, { method: "POST", headers: base(role), body: JSON.stringify({ decision, note }) });
    return response.ok ? ({ ok: true, value: (await response.json()) as MessageTemplate } as const) : failure(response, "The decision could not be recorded.");
  } catch {
    return NETWORK;
  }
}

export async function importTemplates(role: string, templates: unknown[]) {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/templates/import`, { method: "POST", headers: base(role), body: JSON.stringify({ templates }) });
    return response.ok
      ? ({ ok: true, value: (await response.json()) as { stored: number; refused: string[] } } as const)
      : failure(response, "The templates could not be imported.");
  } catch {
    return NETWORK;
  }
}

/** What each placeholder might hold, for a preview. Never sent anywhere. */
const EXAMPLES: Record<string, string> = {
  customer_name: "Eugene",
  organisation: "Mavuno Bank",
  product: "Family Funeral Cover",
  policy_number: "NLA/FF/2026/00012",
  end_date: "30 November 2026",
  due_date: "5 November 2026",
  amount: "UGX 8,000",
  claim_reference: "CLM-4F2A9C",
  documents_needed: "the police report",
  status: "approved and being paid",
  bank_relationship: "a salary account and a loan",
  cover_name: "Loan Protector",
  code: "482913",
};

export function previewOf(template: Pick<MessageTemplate, "body" | "parameters">): string {
  return template.body.replace(/\{\{(\d+)\}\}/g, (_, number: string) => {
    const name = template.parameters[Number(number) - 1] ?? "";
    return EXAMPLES[name] ?? `[${name || number}]`;
  });
}
