"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type ValueTierServiceStandard = { upToValueUgx: number | null; workingDays: number };

export type ClaimStage = {
  stageKey: string;
  name: string;
  ownerRole: string;
  isAutomatic: boolean;
  sortOrder: number;
  serviceStandardWorkingDays: number | null;
  valueTierStandards: ValueTierServiceStandard[] | null;
};

export type DocumentMatrixEntry = {
  documentKey: string;
  customerLabel: string;
  reason: string;
  isRequired: boolean;
  requiredAtStageKey: string;
  isRegulatoryOrWordingMandated: boolean;
  /** Claim type keys this applies to; empty means every claim type of the product. */
  claimTypeKeys?: string[];
  /** "Document" is paperwork (can be scanned to PDF); "Evidence" is kept exactly as taken. */
  kind?: "Document" | "Evidence";
  allowsMultiplePages?: boolean;
  maxFiles?: number;
  acceptedFormats?: string[];
};

export type ProductWordingFact = {
  category: string;
  label: string;
  value: string;
  sourceKnowledgeDocumentId: string;
  sourceChunkId: string;
  sourceQuote: string;
  confidence: number;
};

export type ProductWordingProposal = {
  proposalId: string;
  knowledgeDocumentId: string;
  sourceName: string;
  sourceVersion: string;
  provider: string;
  modelVersion: string | null;
  preparedAt: string;
  preparedByUserId: string;
  items: { category: string; label: string; value: string; sourceChunkId: string; sourceQuote: string; confidence: number }[];
};

export type ClaimTypeConfig = {
  key: string | null;
  name: string;
  description: string | null;
  active: boolean;
  sortOrder: number;
  aliases: string[] | null;
};

export type ClaimFieldConfig = {
  key: string | null;
  label: string;
  type: "Text" | "Number" | "Date" | "Select" | "Phone" | "Currency" | "YesNo";
  required: boolean;
  options: string[] | null;
  helpText: string | null;
  sortOrder: number;
  customerVisible: boolean;
  claimTypeKeys: string[] | null;
};

export type ProductStatus = "Draft" | "PendingPublishApproval" | "Published" | "Retired";

export type Product = {
  productId: string;
  version: number;
  name: string;
  productClass: string;
  iraApprovalReference: string | null;
  iraApprovalEffectiveDate: string | null;
  approvedWordingKnowledgeDocumentId: string | null;
  status: ProductStatus;
  canPublish: boolean;
  stages: ClaimStage[];
  documentMatrix: DocumentMatrixEntry[];
  wordingFacts: ProductWordingFact[];
  claimTypes: ClaimTypeConfig[];
  claimFields: ClaimFieldConfig[];
  updatedByUserId: string;
  updatedAt: string;
  changeReason: string;
  publishSubmittedByUserId: string | null;
  publishSubmittedAt: string | null;
  publishApprovedByUserId: string | null;
  publishApprovedAt: string | null;
  publishRejectedByUserId: string | null;
  publishRejectedAt: string | null;
  publishRejectionReason: string | null;
};

export type ProductVersionSummary = {
  version: number;
  status: ProductStatus;
  updatedByUserId: string;
  updatedAt: string;
  changeReason: string;
};

export type ProductListItem = {
  productId: string;
  name: string;
  productClass: string;
  status: ProductStatus;
  version: number;
  updatedAt: string;
};

export type UpdateProductInput = {
  name?: string | null;
  productClass?: string | null;
  iraApprovalReference?: string | null;
  iraApprovalEffectiveDate?: string | null;
  approvedWordingKnowledgeDocumentId?: string | null;
  stages?: ClaimStage[] | null;
  documentMatrix?: DocumentMatrixEntry[] | null;
  claimTypes?: ClaimTypeConfig[] | null;
  claimFields?: ClaimFieldConfig[] | null;
  changeReason: string;
};

export type ClaimTypeImportResult = {
  productId: string;
  productVersion: number;
  addedClaimTypes: string[];
  claimsUpdated: number;
};

export type ProductsResult<T> = { ok: true; value: T } | { ok: false; error: string; field?: string };

async function safeError(response: Response, fallback: string): Promise<{ message: string; field?: string }> {
  try {
    const payload = (await response.json()) as { message?: string; detail?: string; title?: string; errors?: Record<string, string[]> };
    if (payload.errors) {
      const [field, messages] = Object.entries(payload.errors)[0] ?? [];
      if (messages?.[0]) return { message: messages[0], field };
    }
    if (payload.message || payload.detail) return { message: payload.message ?? payload.detail ?? payload.title ?? fallback };
  } catch {
    // Keep the safe fallback for non-JSON errors.
  }
  return { message: fallback };
}

function networkFailure(reason: unknown, fallback: string): string {
  if (reason instanceof TypeError || (reason instanceof Error && /failed to fetch|networkerror/i.test(reason.message))) {
    return "The product service could not be reached. Check your connection and try again.";
  }
  return fallback;
}

async function call<T>(role: string, input: RequestInfo, init: RequestInit, fallback: string): Promise<ProductsResult<T>> {
  try {
    const response = await fetch(input, {
      ...init,
      headers: { ...scopedHeaders(role, ""), ...(init.headers ?? {}) },
    });
    if (!response.ok) {
      const { message, field } = await safeError(response, fallback);
      return { ok: false, error: message, field };
    }
    if (response.status === 204) return { ok: true, value: undefined as T };
    return { ok: true, value: (await response.json()) as T };
  } catch (reason) {
    return { ok: false, error: networkFailure(reason, fallback) };
  }
}

export function listProducts(role: string) {
  return call<ProductListItem[]>(role, `${apiBaseUrl}/api/v1/products`, {}, "Products could not be loaded.");
}

export function getDefaultStageLibrary(role: string) {
  return call<ClaimStage[]>(role, `${apiBaseUrl}/api/v1/products/default-stage-library`, {}, "The default claim process could not be loaded.");
}

export function getProduct(role: string, productId: string) {
  return call<Product>(role, `${apiBaseUrl}/api/v1/products/${productId}`, {}, "This product could not be loaded.");
}

export function listProductVersions(role: string, productId: string) {
  return call<ProductVersionSummary[]>(role, `${apiBaseUrl}/api/v1/products/${productId}/versions`, {}, "Version history could not be loaded.");
}

export function createProduct(role: string, input: { name: string; productClass: string; changeReason: string; useDefaultStageLibrary: boolean; wordingFacts?: ProductWordingFact[] }) {
  return call<Product>(role, `${apiBaseUrl}/api/v1/products`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(input),
  }, "This product could not be created.");
}

export function extractProductWording(role: string, knowledgeDocumentId: string) {
  return call<ProductWordingProposal>(role, `${apiBaseUrl}/api/v1/products/wording-extractions/${encodeURIComponent(knowledgeDocumentId)}`, {
    method: "POST",
  }, "The wording could not be read.");
}

export function listProductWordingProposals(role: string, knowledgeDocumentId?: string) {
  const query = knowledgeDocumentId ? `?knowledgeDocumentId=${encodeURIComponent(knowledgeDocumentId)}` : "";
  return call<ProductWordingProposal[]>(role, `${apiBaseUrl}/api/v1/products/wording-extractions${query}`, {}, "Wording drafts could not be loaded.");
}

export function updateProductDraft(role: string, productId: string, input: UpdateProductInput) {
  return call<Product>(role, `${apiBaseUrl}/api/v1/products/${productId}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(input),
  }, "This change could not be saved.");
}

export function importExistingClaimTypes(role: string, productId: string) {
  return call<ClaimTypeImportResult>(role, `${apiBaseUrl}/api/v1/products/${productId}/claim-types/import-existing`, {
    method: "POST",
  }, "Existing claim types could not be imported.");
}

export function submitProductForPublishApproval(role: string, productId: string, changeReason: string) {
  return call<Product>(role, `${apiBaseUrl}/api/v1/products/${productId}/submit`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ changeReason }),
  }, "This product could not be submitted for publish approval.");
}

export function approveProductPublish(role: string, productId: string, changeReason: string) {
  return call<Product>(role, `${apiBaseUrl}/api/v1/products/${productId}/approve`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ changeReason }),
  }, "This product could not be published.");
}

export function rejectProductPublish(role: string, productId: string, reason: string) {
  return call<Product>(role, `${apiBaseUrl}/api/v1/products/${productId}/reject`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ reason }),
  }, "This publish request could not be rejected.");
}

export function retireProduct(role: string, productId: string, changeReason: string) {
  return call<Product>(role, `${apiBaseUrl}/api/v1/products/${productId}/retire`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ changeReason }),
  }, "This product could not be retired.");
}
