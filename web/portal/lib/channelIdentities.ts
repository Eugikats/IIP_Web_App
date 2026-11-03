import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type ChannelIdentityLink = {
  linkId: string;
  customerId: string;
  customerName: string;
  channel: string;
  verificationMethod: string;
  consentPurpose: string;
  linkedAt: string;
  isActive: boolean;
  hasActiveConsent: boolean;
  revokedAt: string | null;
};

export type ChannelIdentityCustomer = { customerId: string; fullName: string };
export type CustomerChannelTimelineEntry = {
  messageId: string;
  channel: string;
  direction: string;
  body: string;
  contentType: string;
  deliveryState: string;
  createdAt: string;
  caseId: string | null;
};
export type CustomerChannelTimeline = {
  customerId: string;
  entries: CustomerChannelTimelineEntry[];
  page: number;
  pageSize: number;
  hasMore: boolean;
  sourceHistoryCapped: boolean;
};

async function readError(response: Response, fallback: string): Promise<string> {
  const body = await response.json().catch(() => ({})) as { message?: string; title?: string };
  return body.message || body.title || fallback;
}

export async function getChannelIdentityLink(role: string, channel: string, threadId: string): Promise<ChannelIdentityLink | null> {
  const response = await fetch(`${apiBaseUrl}/api/v1/channels/conversations/${encodeURIComponent(channel.toLowerCase())}/${encodeURIComponent(threadId)}/identity-link`, {
    headers: scopedHeaders(role, ""), cache: "no-store",
  });
  if (!response.ok) throw new Error(await readError(response, "Identity link failed to load."));
  return await response.json() as ChannelIdentityLink | null;
}

export async function listIdentityLinkCustomers(role: string): Promise<ChannelIdentityCustomer[]> {
  const response = await fetch(`${apiBaseUrl}/api/v1/customers`, { headers: scopedHeaders(role, ""), cache: "no-store" });
  if (!response.ok) throw new Error(await readError(response, "Customers could not be loaded."));
  return await response.json() as ChannelIdentityCustomer[];
}

export async function linkChannelIdentity(
  role: string, channel: string, threadId: string,
  input: { customerId: string; customerConsentedToContext: boolean; evidenceReference: string; auditReason: string },
): Promise<ChannelIdentityLink> {
  const response = await fetch(`${apiBaseUrl}/api/v1/channels/conversations/${encodeURIComponent(channel.toLowerCase())}/${encodeURIComponent(threadId)}/identity-link`, {
    method: "POST", headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
    body: JSON.stringify(input),
  });
  if (!response.ok) throw new Error(await readError(response, "Customer identity could not be linked."));
  return await response.json() as ChannelIdentityLink;
}

export async function revokeChannelIdentity(role: string, linkId: string, auditReason: string): Promise<ChannelIdentityLink> {
  const response = await fetch(`${apiBaseUrl}/api/v1/channel-identities/${encodeURIComponent(linkId)}/revoke`, {
    method: "POST", headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
    body: JSON.stringify({ auditReason }),
  });
  if (!response.ok) throw new Error(await readError(response, "Customer identity link could not be removed."));
  return await response.json() as ChannelIdentityLink;
}

export async function getCustomerChannelTimeline(
  role: string, customerId: string, page = 1, pageSize = 50, signal?: AbortSignal,
): Promise<CustomerChannelTimeline> {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  const response = await fetch(`${apiBaseUrl}/api/v1/customers/${encodeURIComponent(customerId)}/channel-timeline?${query}`, {
    headers: scopedHeaders(role, ""), cache: "no-store", signal,
  });
  if (!response.ok) throw new Error(await readError(response, "Linked channel conversations could not be loaded."));
  return await response.json() as CustomerChannelTimeline;
}
