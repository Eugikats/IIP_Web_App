"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { getEntraApiAccessToken } from "@/lib/entraAuth";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { ROLES, type Role } from "@/lib/roles";

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
  error: string | null;
  parityMode: boolean;
  refresh: () => Promise<void>;
};

type MeDto = {
  userId?: string;
  displayName?: string | null;
  role?: string;
  roleLabel?: string;
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
  const [error, setError] = useState<string | null>(null);

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
      const response = await fetch(`${apiBaseUrl}/api/v1/me`, {
        headers: {
          ...scopedHeaders(role, ""),
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
      });
      if (!response.ok) {
        throw new Error(`/api/v1/me failed with HTTP ${response.status}.`);
      }

      const next = toRuntimeContext((await response.json()) as MeDto, role);
      setRuntimeContext(next);
      if (next && next.role !== role) {
        setRole(next.role);
      }
    } catch (err) {
      setRuntimeContext(null);
      setError(err instanceof Error ? err.message : "Runtime context unavailable.");
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
