// Display formatting shared by every staff screen. The rule these helpers exist to enforce: a
// person never reads a raw identifier, a camelCase field name, a PascalCase enum name or an ISO
// timestamp. Each one has a human form, and this file is the single place that decides it.

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function isGuid(value: string | null | undefined): boolean {
  return Boolean(value && GUID.test(value.trim()));
}

/**
 * Short claim reference, CLM plus the first eight hex characters of the id, upper case. Mirrors
 * InsuranceIntelligence.Domain.Cases.ClaimReference exactly, so the assistant, the API and every
 * screen agree on what a claim is called. Anything that is not a GUID (a fixture reference such
 * as CLM-1064) is returned unchanged.
 */
export function claimReference(caseId: string | null | undefined): string {
  const value = (caseId ?? "").trim();
  if (!value) return "Claim";
  if (!isGuid(value)) return value;
  return `CLM-${value.replace(/-/g, "").slice(0, 8).toUpperCase()}`;
}

/**
 * Free text from the backend can still carry a full id ("Claim e0ab43ad-1835-..."). Any id in the text becomes the
 * short reference people use, so a 36-character GUID never reaches the screen, including in records written before
 * the backend stopped printing ids.
 */
export function withReferences(text: string | null | undefined): string {
  return (text ?? "").replace(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi, (id) => claimReference(id));
}

/** Short reference for any other record (approval, document, customer). Never the full id. */
export function shortReference(prefix: string, id: string | null | undefined): string {
  const value = (id ?? "").trim();
  if (!isGuid(value)) return value;
  return `${prefix}-${value.replace(/-/g, "").slice(0, 8).toUpperCase()}`;
}

/** "SupervisorReview" / "completenessScore" / "claim_fnol" / "IN_REVIEW" become "Supervisor review". */
export function humanizeIdentifier(value: string | null | undefined): string {
  const raw = (value ?? "").trim();
  if (!raw) return "";
  const spaced = raw
    .replace(/[_-]+/g, " ")
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
    .replace(/([A-Z]+)([A-Z][a-z])/g, "$1 $2")
    .replace(/\s+/g, " ")
    .trim()
    .toLowerCase();
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

function parseDate(value: string): Date | null {
  // A bare date (2026-01-22) is a calendar date, not an instant. Parsing it as UTC midnight would
  // show the previous day for anyone west of Greenwich, so it is built as a local date instead.
  const bare = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value.trim());
  if (bare) return new Date(Number(bare[1]), Number(bare[2]) - 1, Number(bare[3]));
  const t = Date.parse(value);
  return Number.isNaN(t) ? null : new Date(t);
}

/** "22 Jan 2026". Returns the fallback for missing or unparseable input rather than echoing it. */
export function formatDate(value: string | null | undefined, fallback = "Not recorded"): string {
  if (!value) return fallback;
  const date = parseDate(value);
  return date ? date.toLocaleDateString("en-GB", { day: "numeric", month: "short", year: "numeric" }) : fallback;
}

/** "28 Aug 2026, 12:10". */
export function formatDateTime(value: string | null | undefined, fallback = "Not recorded"): string {
  if (!value) return fallback;
  const date = parseDate(value);
  if (!date) return fallback;
  return `${date.toLocaleDateString("en-GB", { day: "numeric", month: "short", year: "numeric" })}, ${date.toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit" })}`;
}

/** How long something has been waiting, in the unit a person would use: "35 min", "5 hours", "15 days". */
export function formatAgeFromHours(hours: number): string {
  if (!Number.isFinite(hours) || hours < 0) return "Unknown";
  if (hours < 1 / 60) return "Just now";
  if (hours < 1) return `${Math.round(hours * 60)} min`;
  if (hours < 48) {
    const h = Math.round(hours);
    return `${h} ${h === 1 ? "hour" : "hours"}`;
  }
  const days = Math.round(hours / 24);
  if (days < 60) return `${days} days`;
  const months = Math.round(days / 30);
  return `${months} ${months === 1 ? "month" : "months"}`;
}

export function formatAgeSince(value: string | null | undefined): string {
  if (!value) return "Unknown";
  const date = parseDate(value);
  if (!date) return "Unknown";
  return formatAgeFromHours((Date.now() - date.getTime()) / 3_600_000);
}

/** Completeness arrives as 0 to 100 from most endpoints and occasionally as 0 to 1; either way, "40%". */
export function formatPercent(value: string | number | null | undefined): string {
  const n = typeof value === "string" ? Number(value) : value;
  if (n === null || n === undefined || !Number.isFinite(n)) return "Not recorded";
  const pct = n > 0 && n <= 1 ? n * 100 : n;
  return `${Math.round(pct)}%`;
}

/** Whether a policy is in force today, from its effective dates. */
export function policyStanding(effectiveFrom?: string | null, effectiveTo?: string | null): "Active" | "Expired" | "Not yet started" | "Unknown" {
  const now = new Date();
  const from = effectiveFrom ? parseDate(effectiveFrom) : null;
  const to = effectiveTo ? parseDate(effectiveTo) : null;
  if (!from && !to) return "Unknown";
  if (from && now < from) return "Not yet started";
  if (to && now > new Date(to.getFullYear(), to.getMonth(), to.getDate(), 23, 59, 59)) return "Expired";
  return "Active";
}

// How a claim or message arrived, in words. The backend stores Channel as free text (see
// ChannelBadge in components/iip/Primitives.tsx), so unknown values are humanised, never hidden.
const CHANNEL_LABELS: Record<string, string> = {
  StaffEntry: "Manual entry",
  ClientPortal: "Claimant portal",
  PortalSubmission: "Claimant portal",
  Email: "Email",
  WhatsApp: "WhatsApp",
  Sms: "SMS",
  SMS: "SMS",
  Branch: "Branch",
  Broker: "Broker",
  CallCentre: "Call centre",
  CallCenter: "Call centre",
};

export function channelLabel(channel: string | null | undefined): string {
  const value = (channel ?? "").trim();
  if (!value) return "Not recorded";
  return CHANNEL_LABELS[value] ?? humanizeIdentifier(value);
}

// Document types as a person would say them. "Unknown" is shown as "Unclassified": an unclassified
// document is something a person should notice, not a blank.
const DOCUMENT_TYPE_LABELS: Record<string, string> = {
  ClaimForm: "Claim form",
  PoliceAbstract: "Police abstract",
  MedicalReport: "Medical report",
  PhotoEvidence: "Photo evidence",
  CustomerId: "Customer ID",
  GarageEstimate: "Garage estimate",
  Unknown: "Unclassified",
};

export function documentTypeLabel(type: string | null | undefined): string {
  const value = (type ?? "").trim();
  if (!value) return "Unclassified";
  return DOCUMENT_TYPE_LABELS[value] ?? humanizeIdentifier(value);
}

/** What an agent run or work item is about: "CLM-1A2B3C4D" for a claim, a short reference otherwise. */
export function businessReference(type: string | null | undefined, id: string | null | undefined): string {
  const kind = (type ?? "").trim().toLowerCase();
  if (kind === "case" || kind === "claim") return claimReference(id);
  return shortReference("REF", id);
}
