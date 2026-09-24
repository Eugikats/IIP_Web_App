const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const FIXTURE_REFERENCE = /^(CLM|DOC)-[A-Z0-9-]+$/i;

export function isValidWorkReference(value: string | null | undefined, prefix: "CLM" | "DOC") {
  const normalized = value?.trim() ?? "";
  if (!normalized || normalized === "-" || normalized.toLowerCase() === "unknown") return false;
  return UUID.test(normalized) || (FIXTURE_REFERENCE.test(normalized) && normalized.toUpperCase().startsWith(`${prefix}-`));
}

export function extractionHref(claimId: string | null | undefined, documentId: string | null | undefined) {
  if (!isValidWorkReference(claimId, "CLM") || !isValidWorkReference(documentId, "DOC")) return null;
  return `/app/claims/${encodeURIComponent(claimId!.trim())}/extraction?documentId=${encodeURIComponent(documentId!.trim())}`;
}

export function safeInternalWorkHref(value: string | null | undefined, fallback: string) {
  const href = value?.trim() ?? "";
  if (!href.startsWith("/app/")) return fallback;
  const extraction = href.match(/^\/app\/claims\/([^/?#]+)\/extraction\?documentId=([^&#]+)$/);
  if (!extraction) return href;
  try {
    return extractionHref(decodeURIComponent(extraction[1]), decodeURIComponent(extraction[2])) ?? fallback;
  } catch {
    return fallback;
  }
}
