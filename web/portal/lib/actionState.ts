export type ActionState = {
  action: string;
  label: string;
  requiredPermission: string;
  reason: string | null;
};

export type ActionStateContract = {
  allowedActions: ActionState[];
  blockedActions: ActionState[];
};

export function hasActionState(value: unknown): value is ActionStateContract {
  if (typeof value !== "object" || value === null) return false;

  const contract = value as Partial<ActionStateContract>;
  return Array.isArray(contract.allowedActions)
    && Array.isArray(contract.blockedActions)
    && contract.allowedActions.every(isActionState)
    && contract.blockedActions.every(isActionState);
}

export function allowsAction(contract: ActionStateContract, action: string) {
  return contract.allowedActions.some((item) => item.action === action);
}

export function blockedActionReason(contract: ActionStateContract, action: string) {
  return contract.blockedActions.find((item) => item.action === action)?.reason ?? null;
}

function isActionState(value: unknown): value is ActionState {
  if (typeof value !== "object" || value === null) return false;

  const action = value as Partial<ActionState>;
  return typeof action.action === "string"
    && typeof action.label === "string"
    && typeof action.requiredPermission === "string"
    && (action.reason === null || typeof action.reason === "string");
}
