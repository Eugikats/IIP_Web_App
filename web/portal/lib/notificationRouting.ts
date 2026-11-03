import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type NotificationEventType =
  | "ApprovalAssigned"
  | "ClaimEscalated"
  | "SlaBreach"
  | "DocumentReviewNeeded"
  | "ReviewRequested"
  | "ClaimAssigned"
  | "SlaAtRisk";

export type NotificationDestination = "InApp" | "MicrosoftTeams";

export type NotificationRoutingRule = {
  type: NotificationEventType;
  additionalRecipientUserIds: string[];
  additionalDestinations: NotificationDestination[];
};

export type NotificationRoutingResponse = {
  configuration: { rules: NotificationRoutingRule[] };
  users: { userId: string; name: string; role: string }[];
  availableDestinations: NotificationDestination[];
};

export type NotificationRoutingResult<T> = { ok: true; value: T } | { ok: false; error: string };

async function safeError(response: Response): Promise<string> {
  try {
    const payload = (await response.json()) as { message?: string; errors?: Record<string, string[]> };
    if (payload.message) return payload.message;
    const first = payload.errors ? Object.values(payload.errors).flat()[0] : null;
    if (first) return first;
  } catch {
    // Return a plain fallback for non-JSON errors.
  }
  return "Notification routing could not be saved. Check your connection and try again.";
}

export async function getNotificationRouting(role: string): Promise<NotificationRoutingResult<NotificationRoutingResponse>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/notification-routing`, { headers: scopedHeaders(role, "") });
    if (!response.ok) return { ok: false, error: await safeError(response) };
    return { ok: true, value: (await response.json()) as NotificationRoutingResponse };
  } catch {
    return { ok: false, error: "Notification routing could not be loaded. Check your connection and try again." };
  }
}

export async function saveNotificationRouting(
  role: string,
  rules: NotificationRoutingRule[],
  auditReason: string,
): Promise<NotificationRoutingResult<NotificationRoutingResponse>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/notification-routing`, {
      method: "PUT",
      headers: { "Content-Type": "application/json", "X-Audit-Reason": auditReason, ...scopedHeaders(role, "") },
      body: JSON.stringify({ rules }),
    });
    if (!response.ok) return { ok: false, error: await safeError(response) };
    return { ok: true, value: (await response.json()) as NotificationRoutingResponse };
  } catch {
    return { ok: false, error: "Notification routing could not be saved. Check your connection and try again." };
  }
}
