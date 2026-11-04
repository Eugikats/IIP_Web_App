"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import type { ClaimFieldForm } from "@/lib/claimFields";
import type { ClaimantDocumentRow } from "@/components/iip/ClaimantDocumentsCard";

export type CustomerPreview = {
  productId: string;
  productName: string;
  productClass: string;
  status: string;
  isPublished: boolean;
  claimTypes: { key: string; name: string; description: string }[];
  claimTypeKey: string | null;
  claimTypeName: string;
  form: ClaimFieldForm;
  documents: ClaimantDocumentRow[];
  samplePolicyNumber: string;
  privacyNoticeVersion: number;
  privacyNoticeText: string;
  privacyNoticeIsDefault: boolean;
};

export type CustomerPreviewResult = { ok: true; value: CustomerPreview } | { ok: false; error: string };

/** Reads what a customer would be asked for a product and claim type. Read only: nothing is saved
 *  or sent, and it works for a draft that is not published yet. */
export async function loadCustomerPreview(role: string, productId: string, claimType?: string): Promise<CustomerPreviewResult> {
  const query = claimType ? `?claimType=${encodeURIComponent(claimType)}` : "";
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/products/${productId}/customer-preview${query}`, {
      headers: scopedHeaders(role, ""),
      cache: "no-store",
    });
    if (!response.ok) {
      try {
        const payload = (await response.json()) as { message?: string; errors?: Record<string, string[]> };
        const first = payload.errors ? Object.values(payload.errors).flat()[0] : null;
        return { ok: false, error: first ?? payload.message ?? "The preview could not be loaded." };
      } catch {
        return { ok: false, error: response.status === 403 ? "You do not have permission to preview this product." : "The preview could not be loaded." };
      }
    }
    return { ok: true, value: (await response.json()) as CustomerPreview };
  } catch {
    return { ok: false, error: "The service could not be reached. Check your connection and try again." };
  }
}
