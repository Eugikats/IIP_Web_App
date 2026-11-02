"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, canUseLocalTestDoubles, getActiveOrganisationOverride, getActiveWorkspaceOverride, scopedHeaders, setActiveOrganisationOverride } from "@/lib/config";
import { getEntraApiAccessToken } from "@/lib/entraAuth";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { ROLES, type Permission, type Role } from "@/lib/roles";
import { UiServiceError } from "@/lib/uiLoadState";

export type ReviewMode = "Production" | "Demo" | "LocalDevelopment";

export type EnvironmentView = {
  name: string;
  reviewMode: ReviewMode;
  syntheticData: boolean;
  dataClassification: string;
};

export type WorkspaceView = {
  workspaceId: string;
  name: string;
  kind?: string;
};

export type RuntimeContextView = {
  userId: string;
  displayName: string;
  role: Role;
  roleLabel: string;
  permissions: Permission[];
  tenantId: string;
  workspaceId: string;
  source: string;
  environment: EnvironmentView;
  availableWorkspaces: WorkspaceView[];
};

type RuntimeContextState = {
  runtimeContext: RuntimeContextView | null;
  environment: EnvironmentView | null;
  loading: boolean;
  error: UiServiceError | null;
  parityMode: boolean;
  refresh: () => Promise<void>;
};

type MeDto = {
  userId?: string;
  displayName?: string | null;
  role?: string;
  roleLabel?: string;
  permissions?: string[];
  tenantId?: string;
  workspaceId?: string;
  source?: string;
  environment?: {
    name?: string;
    reviewMode?: string;
    syntheticData?: boolean;
    dataClassification?: string;
  };
  availableWorkspaces?: Array<{
    workspaceId?: string;
    name?: string;
    kind?: string;
  }>;
};

const PARITY_ENVIRONMENT: EnvironmentView = {
  name: "Demo / Review sandbox",
  reviewMode: "Demo",
  syntheticData: true,
  dataClassification: "Synthetic - not real customer data",
};

const PARITY_CONTEXT: RuntimeContextView = {
  userId: "visual-parity-user",
  displayName: "Visual parity user",
  role: "Admin",
  roleLabel: "Workspace Administrator",
  permissions: [
    "ViewCommandCenter", "CreateClaim", "UploadDocument", "ViewAllClaims", "ViewAssignedClaims",
    "ApproveSettlementWorkflowStep", "ViewAuditLog", "ExportEvidencePack", "ManageKnowledgeBase", "ConfigureIntegrations",
    "ViewAiOutputs", "OverrideAiRecommendation", "ManageCustomerCommunications", "SendCustomerUpdate",
    "ManageTenantSettings", "ViewClientPortal", "ViewChannels", "ViewCustomers", "ViewSalesLeads",
    "ManageSalesLeads", "ViewFraudSignals", "ViewUnderwriting", "ManageAgents", "ViewAgentWorkflows",
    "RunAgentWorkflows", "ManageAgentWorkflows", "ReviewAgentWorkItems", "ViewBilling", "ManageTenantOnboarding",
  ],
  tenantId: "visual-parity-tenant",
  workspaceId: "visual-parity-workspace",
  source: "LovableVisualParity",
  environment: PARITY_ENVIRONMENT,
  availableWorkspaces: [
    { workspaceId: "visual-parity-workspace", name: "Visual parity workspace", kind: "demo" },
  ],
};

const RuntimeContext = createContext<RuntimeContextState | null>(null);

function normalizeReviewMode(value: string | undefined): ReviewMode {
  if (value === "Production" || value === "Demo") return value;
  return "LocalDevelopment";
}

function normalizeRole(value: string | undefined, fallback: Role): Role {
  return value && value in ROLES ? (value as Role) : fallback;
}

function initials(displayName: string): string {
  return displayName
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join("") || "IIP";
}

export function runtimeInitials(displayName: string): string {
  return initials(displayName);
}

function toRuntimeContext(body: MeDto, fallbackRole: Role): RuntimeContextView | null {
  if (!body.environment || !body.tenantId || !body.workspaceId) {
    return null;
  }

  const role = normalizeRole(body.role, fallbackRole);
  const displayName = body.displayName || body.userId || "Authenticated user";
  const availableWorkspaces =
    body.availableWorkspaces
      ?.filter((workspace) => workspace.workspaceId)
      .map((workspace) => ({
        workspaceId: workspace.workspaceId!,
        name: workspace.name || workspace.workspaceId!,
        kind: workspace.kind,
      })) ?? [];

  return {
    userId: body.userId || "",
    displayName,
    role,
    roleLabel: body.roleLabel || ROLES[role].title,
    permissions: (body.permissions ?? []).filter(Boolean) as Permission[],
    tenantId: body.tenantId,
    workspaceId: body.workspaceId,
    source: body.source || "Unknown",
    environment: {
      name: body.environment.name || "Environment",
      reviewMode: normalizeReviewMode(body.environment.reviewMode),
      syntheticData: Boolean(body.environment.syntheticData),
      dataClassification: body.environment.dataClassification || "",
    },
    availableWorkspaces:
      availableWorkspaces.length > 0
        ? availableWorkspaces
        : [{ workspaceId: body.workspaceId, name: body.workspaceId, kind: "default" }],
  };
}

export function RuntimeContextProvider({ children }: { children: ReactNode }) {
  const { role, setRole } = useUI();
  const [runtimeContext, setRuntimeContext] = useState<RuntimeContextView | null>(
    LOVABLE_VISUAL_PARITY_MODE ? PARITY_CONTEXT : null,
  );
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<UiServiceError | null>(null);

  const refresh = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) {
      setRuntimeContext(PARITY_CONTEXT);
      setLoading(false);
      setError(null);
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const token = await getEntraApiAccessToken();
      const load = () =>
        fetch(`${apiBaseUrl}/api/v1/me`, {
          headers: {
            ...scopedHeaders(canUseLocalTestDoubles ? "Admin" : role, ""),
            ...(token ? { Authorization: `Bearer ${token}` } : {}),
          },
        });
      let response = await load();
      // A remembered organisation or workspace (from switching demonstrations) that this account does not belong to
      // makes the API refuse everything, and the browser would send it again forever. Forget it and try once more as
      // the account's own workspace, so a stale choice can never lock someone out.
      if (response.status === 403 && !canUseLocalTestDoubles && (getActiveOrganisationOverride() || getActiveWorkspaceOverride())) {
        setActiveOrganisationOverride(null, null);
        response = await load();
      }
      if (!response.ok) {
        throw await UiServiceError.fromResponse(response, `Workspace access is unavailable.`);
      }

      const authorityRole = canUseLocalTestDoubles ? "Admin" : role;
      const next = toRuntimeContext((await response.json()) as MeDto, authorityRole);
      setRuntimeContext(next);
      if (!canUseLocalTestDoubles && next && next.role !== role) {
        setRole(next.role);
      }
    } catch (err) {
      setRuntimeContext(null);
      setError(UiServiceError.fromUnknown(err, "Workspace access service could not be reached."));
    } finally {
      setLoading(false);
    }
  }, [role, setRole]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const value = useMemo<RuntimeContextState>(
    () => ({
      runtimeContext,
      environment: runtimeContext?.environment ?? null,
      loading,
      error,
      parityMode: LOVABLE_VISUAL_PARITY_MODE,
      refresh,
    }),
    [error, loading, refresh, runtimeContext],
  );

  return <RuntimeContext.Provider value={value}>{children}</RuntimeContext.Provider>;
}

export function useRuntimeContext() {
  const value = useContext(RuntimeContext);
  if (!value) {
    throw new Error("useRuntimeContext must be used within RuntimeContextProvider");
  }

  return value;
}
