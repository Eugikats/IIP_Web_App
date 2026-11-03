"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";

/** What a product offers for one product line. `configured` is false when no published product has
 *  claim types yet; callers then keep their built-in list. */
export type ClaimTypeOptions = {
  productLine: string;
  productId: string | null;
  productName: string | null;
  configured: boolean;
  claimTypes: { key: string; name: string; description: string }[];
};

export type ClaimTypeSuggestion = { key: string | null; name: string | null; matched: boolean };

async function readOptions(response: Response): Promise<ClaimTypeOptions | null> {
  if (!response.ok) return null;
  try {
    return (await response.json()) as ClaimTypeOptions;
  } catch {
    return null;
  }
}

/** For the staff app. Returns null when the list could not be read, which callers treat as "not
 *  configured" and fall back to their built-in list rather than blocking the person. */
export async function fetchClaimTypes(role: string, productLine: string): Promise<ClaimTypeOptions | null> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/claim-types?productLine=${encodeURIComponent(productLine)}`, {
      headers: scopedHeaders(role, ""),
      cache: "no-store",
    });
    return await readOptions(response);
  } catch {
    return null;
  }
}

export async function suggestClaimType(role: string, productLine: string, text: string): Promise<ClaimTypeSuggestion | null> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/claim-types/suggest`, {
      method: "POST",
      headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
      body: JSON.stringify({ productLine, text }),
    });
    return response.ok ? ((await response.json()) as ClaimTypeSuggestion) : null;
  } catch {
    return null;
  }
}
