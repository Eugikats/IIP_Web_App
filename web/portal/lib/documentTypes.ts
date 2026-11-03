"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type DocumentTypeDefinition = {
  definitionId: string;
  key: string;
  version: number;
  customerLabel: string;
  description: string;
  builtInType: string | null;
  active: boolean;
  updatedByUserId: string;
  updatedAt: string;
  changeReason: string;
};

export type DocumentTypeUploadOption = {
  key: string;
  customerLabel: string;
  description: string;
};

export type UnresolvedProductDocumentKeys = {
  productId: string;
  productName: string;
  version: number;
  unresolvedDocumentKeys: string[];
};

export type DocumentTypesResult<T> = { ok: true; value: T } | { ok: false; error: string };

async function call<T>(role: string, input: RequestInfo, init: RequestInit, fallback: string): Promise<DocumentTypesResult<T>> {
  try {
    const response = await fetch(input, {
      ...init,
      headers: { ...scopedHeaders(role, ""), ...(init.headers ?? {}) },
    });
    if (!response.ok) {
      try {
        const payload = (await response.json()) as { message?: string };
        return { ok: false, error: payload.message ?? fallback };
      } catch {
        return { ok: false, error: fallback };
      }
    }
    return { ok: true, value: (await response.json()) as T };
  } catch (reason) {
    if (reason instanceof TypeError || (reason instanceof Error && /failed to fetch|networkerror/i.test(reason.message))) {
      return { ok: false, error: "The document type catalog could not be reached. Check your connection and try again." };
    }
    return { ok: false, error: fallback };
  }
}

export function listDocumentTypes(role: string) {
  return call<DocumentTypeDefinition[]>(role, `${apiBaseUrl}/api/v1/document-types`, {}, "The document type catalog could not be loaded.");
}

export function listDocumentUploadOptions(role: string) {
  return call<DocumentTypeUploadOption[]>(role, `${apiBaseUrl}/api/v1/document-types/upload-options`, {}, "The document type choices could not be loaded.");
}

export function seedDefaultDocumentTypes(role: string) {
  return call<{ seeded: number }>(role, `${apiBaseUrl}/api/v1/document-types/seed-defaults`, { method: "POST" }, "The default document types could not be created.");
}

export function saveDocumentType(role: string, key: string, input: { customerLabel: string; description: string; active: boolean }, changeReason: string) {
  return call<DocumentTypeDefinition>(role, `${apiBaseUrl}/api/v1/document-types/${encodeURIComponent(key)}`, {
    method: "POST",
    headers: { "Content-Type": "application/json", "X-Audit-Reason": changeReason },
    body: JSON.stringify(input),
  }, "This document type could not be saved.");
}

export function listUnresolvedProductDocumentKeys(role: string) {
  return call<UnresolvedProductDocumentKeys[]>(role, `${apiBaseUrl}/api/v1/document-types/unresolved-product-keys`, {}, "Could not check products for unrecognised document keys.");
}

// Ensures the tenant's catalog has at least the built-in types before the admin document matrix
// dropdown is shown, since a brand-new tenant workspace has never called seed-defaults yet.
export async function ensureDocumentTypeCatalog(role: string): Promise<DocumentTypesResult<DocumentTypeDefinition[]>> {
  const first = await listDocumentTypes(role);
  if (!first.ok || first.value.length > 0) return first;
  const seeded = await seedDefaultDocumentTypes(role);
  if (!seeded.ok) return first;
  return listDocumentTypes(role);
}
