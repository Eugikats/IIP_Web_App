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

export function providerByName(health: ProviderHealth | null, name: string) {
  return health?.providers.find((provider) => provider.name === name) ?? null;
}

export function isProviderConfigured(provider: ProviderHealthItem | null) {
  const status = provider?.status.toLowerCase();
  return status === "configured" || status === "healthy";
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
  const localMockEnabled = canUseLocalTestDoubles && isProviderConfigured(provider) && mode === "mock";

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
