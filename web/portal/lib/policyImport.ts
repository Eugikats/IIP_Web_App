"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type PolicyImportFieldOption = { field: string; label: string; required: boolean };

export type PolicyImportParseResult = {
  headers: string[];
  rows: string[][];
  truncated: boolean;
  suggestedMapping: Record<string, string>;
  availableFields: PolicyImportFieldOption[];
};

export type PolicyImportRowError = { rowNumber: number; errors: string[] };

export type PolicyImportCommitResult = {
  totalRows: number;
  importedCount: number;
  failedCount: number;
  errors: PolicyImportRowError[];
};

export type PolicyImportResult<T> = { ok: true; value: T } | { ok: false; error: string; field?: string };

async function safeError(response: Response, fallback: string): Promise<{ message: string; field?: string }> {
  try {
    const payload = (await response.json()) as { message?: string; errors?: Record<string, string[]> };
    if (payload.errors) {
      const [field, messages] = Object.entries(payload.errors)[0] ?? [];
      if (messages?.[0]) return { message: messages[0], field };
    }
    if (payload.message) return { message: payload.message };
  } catch {
    // Keep the safe fallback for non-JSON errors.
  }
  return { message: fallback };
}

function networkFailure(reason: unknown, fallback: string): string {
  if (reason instanceof TypeError || (reason instanceof Error && /failed to fetch|networkerror/i.test(reason.message))) {
    return "The policy import service could not be reached. Check your connection and try again.";
  }
  return fallback;
}

export async function parsePolicyImportFile(role: string, file: File): Promise<PolicyImportResult<PolicyImportParseResult>> {
  const form = new FormData();
  form.set("file", file);
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/policy-import/parse`, {
      method: "POST",
      headers: scopedHeaders(role, ""),
      body: form,
    });
    if (!response.ok) {
      const { message, field } = await safeError(response, "This file could not be read.");
      return { ok: false, error: message, field };
    }
    return { ok: true, value: (await response.json()) as PolicyImportParseResult };
  } catch (reason) {
    return { ok: false, error: networkFailure(reason, "This file could not be read.") };
  }
}

export async function commitPolicyImport(
  role: string,
  request: { headers: string[]; columnMapping: Record<string, string>; rows: string[][] }
): Promise<PolicyImportResult<PolicyImportCommitResult>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/policy-import/commit`, {
      method: "POST",
      headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
      body: JSON.stringify(request),
    });
    if (!response.ok) {
      const { message, field } = await safeError(response, "This import could not be committed.");
      return { ok: false, error: message, field };
    }
    return { ok: true, value: (await response.json()) as PolicyImportCommitResult };
  } catch (reason) {
    return { ok: false, error: networkFailure(reason, "This import could not be committed.") };
  }
}
