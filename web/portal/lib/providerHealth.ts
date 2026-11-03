export type ProviderHealthItem = {
  name: string;
  mode: string;
  status: string;
  detail: string;
};

export type ProviderHealth = {
  overallStatus: string;
  providers: ProviderHealthItem[];
};

export type ActionState = {
  action: string;
  label: string;
  requiredPermission: string;
  reason?: string | null;
};

export type IntegrationHealth = ProviderHealth & {
  integrationId: string;
  displayName: string;
  allowedActions: ActionState[];
  blockedActions: ActionState[];
};

export type ChannelSendAvailability = {
  enabled: boolean;
  reason: string;
  recipientField: "email" | "phone" | null;
};

export type ProviderStatusTone = "success" | "warning" | "error";

const ERROR_PROVIDER_STATUSES = new Set(["error", "failed", "offline", "unhealthy"]);
const WARNING_PROVIDER_STATUSES = new Set([
  "degraded",
  "disabled",
  "missing",
  "mock",
  "not configured",
  "pending",
  "unavailable",
  "unconfigured"
]);
// "configured" is a genuinely good state, not a lesser one, it means the provider's required
// settings are present, whether or not it has an active health ping (database/blobStorage/
// documentIntelligence/aiSearch report "configured" rather than "healthy" precisely because they
// have no such ping). The backend's own overall-status aggregate (AdminEndpoints.
// BuildProviderHealth) agrees: only "missing" and "not_configured" count as degraded there.
// isProviderHealthy previously excluded "configured", so a fully-configured workspace could
// render "9 of 9 providers need attention" next to a panel showing every provider as Configured,
// this was a real, live contradiction, not a hypothetical one.
const HEALTHY_PROVIDER_STATUSES = new Set([
  "healthy",
  "operational",
  "configured"
]);

function normalizeProviderStatus(status: string) {
  return status.trim().toLowerCase().replace(/[_-]+/g, " ").replace(/\s+/g, " ");
}

export function providerStatusTone(status: string): ProviderStatusTone {
  const normalized = normalizeProviderStatus(status);

  if (ERROR_PROVIDER_STATUSES.has(normalized)) return "error";
  if (HEALTHY_PROVIDER_STATUSES.has(normalized)) return "success";
  if (WARNING_PROVIDER_STATUSES.has(normalized)) return "warning";

  return "warning";
}

export function isProviderHealthy(status: string) {
  return HEALTHY_PROVIDER_STATUSES.has(normalizeProviderStatus(status));
}

export function providerByName(health: ProviderHealth | null, name: string) {
  return health?.providers.find((provider) => provider.name === name) ?? null;
}

export function isProviderConfigured(provider: ProviderHealthItem | null) {
  const status = provider?.status.toLowerCase();
  return status === "configured" || status === "healthy";
}

export function formatProviderStatusLabel(status: string) {
  const normalized = normalizeProviderStatus(status);
  if (normalized === "healthy") return "Operational";
  if (normalized === "not configured" || normalized === "unconfigured") {
    return "Not configured";
  }
  return normalized.replace(/\b\w/g, (character) => character.toUpperCase());
}

export function integrationActionAvailability(
  health: IntegrationHealth | null,
  action: string
): ChannelSendAvailability {
  if (health?.allowedActions.some((item) => item.action === action)) {
    return { enabled: true, reason: "", recipientField: null };
  }

  const blockedAction = health?.blockedActions.find((item) => item.action === action);
  return {
    enabled: false,
    reason: blockedAction?.reason || "This integration action is not currently available.",
    recipientField: null
  };
}

export function customerUpdateSendAvailability(
  channel: string,
  health: ProviderHealth | null,
  canUseLocalTestDoubles = false
): ChannelSendAvailability {
  const provider = providerByName(health, "communications");
  const mode = provider?.mode.toLowerCase();
  const normalizedChannel = channel.toLowerCase();
  // "mock" is reported by the API only under a developer runtime, when the deterministic Mock
  // communication adapter is active. It is never "healthy": its output is not real.
  const localMockEnabled = canUseLocalTestDoubles && mode === "mock" && (isProviderConfigured(provider) || provider?.status.toLowerCase() === "mock");

  if (normalizedChannel === "email") {
    if ((isProviderConfigured(provider) && mode === "graphemail") || localMockEnabled) {
      return { enabled: true, reason: "", recipientField: "email" };
    }

    return {
      enabled: false,
      reason: provider?.detail || "GraphEmail customer communication is not configured.",
      recipientField: "email"
    };
  }

  if (normalizedChannel === "sms") {
    if (localMockEnabled) {
      return { enabled: true, reason: "", recipientField: "phone" };
    }

    return {
      enabled: false,
      reason: "SMS sending is not configured for the current communication provider.",
      recipientField: "phone"
    };
  }

  if (normalizedChannel === "portal") {
    return {
      enabled: false,
      reason: "Portal delivery is not configured as an outbound customer-update provider.",
      recipientField: null
    };
  }

  if (normalizedChannel === "whatsapp") {
    return {
      enabled: false,
      reason: "WhatsApp sending is not configured for this workspace.",
      recipientField: "phone"
    };
  }

  return {
    enabled: false,
    reason: `${channel} sending is not configured for this workspace.`,
    recipientField: null
  };
}
