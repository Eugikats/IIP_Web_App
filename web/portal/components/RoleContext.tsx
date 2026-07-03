"use client";

import { createContext, ReactNode, useContext, useEffect, useMemo, useState } from "react";
import { apiBaseUrl, canUseLocalTestDoubles, localDevelopmentUserId, scopedHeaders } from "@/lib/config";
import { getEntraApiAccessToken } from "@/lib/entraAuth";
import { hasPermission, isRole, roleLabels, rolePermissions, roles, type Role } from "@/lib/roleUiConfig";
export { roles };
export type { Role };

type RoleContextValue = {
  role: Role;
  userId: string;
  displayName: string;
  roleLabel: string;
  tenantId: string;
  workspaceId: string;
  permissions: string[];
  contextSource: string;
  isLoadingContext: boolean;
  setRole: (role: Role) => void;
  can: (permission: string) => boolean;
};

type RuntimeContextResponse = {
  userId: string;
  displayName: string | null;
  role: string;
  roleLabel: string;
  permissions: string[];
  tenantId: string;
  workspaceId: string;
  authMode: string;
  source: string;
};

type RuntimeContext = Omit<RuntimeContextResponse, "role" | "displayName"> & {
  role: Role;
  displayName: string;
};

const RoleContext = createContext<RoleContextValue | null>(null);

export function RoleProvider({ children }: { children: ReactNode }) {
  const [role, setRoleState] = useState<Role>(canUseLocalTestDoubles ? "Admin" : "Client");
  const [runtimeContext, setRuntimeContext] = useState<RuntimeContext | null>(null);
  const [isLoadingContext, setIsLoadingContext] = useState(true);
  const [hasLoadedStoredRole, setHasLoadedStoredRole] = useState(!canUseLocalTestDoubles);

  useEffect(() => {
    if (!canUseLocalTestDoubles) {
      setRoleState("Client");
      setHasLoadedStoredRole(true);
      return;
    }

    const stored = window.localStorage.getItem("iip-demo-role");
    if (stored && roles.includes(stored as Role)) {
      setRoleState(stored as Role);
    }

    setHasLoadedStoredRole(true);
  }, []);

  useEffect(() => {
    let cancelled = false;

    async function loadRuntimeContext() {
      if (!hasLoadedStoredRole) {
        return;
      }

      setIsLoadingContext(true);

      try {
        await getEntraApiAccessToken();
        const developmentUserId = canUseLocalTestDoubles ? localDevelopmentUserId : "";
        const response = await fetch(`${apiBaseUrl}/api/v1/me`, {
          cache: "no-store",
          credentials: "include",
          headers: scopedHeaders(role, developmentUserId)
        });

        if (!response.ok) {
          throw new Error(`Runtime context failed with HTTP ${response.status}.`);
        }

        const data = (await response.json()) as RuntimeContextResponse;
        const nextRole = isRole(data.role) ? data.role : "Client";

        if (cancelled) return;

        setRuntimeContext({
          ...data,
          role: nextRole,
          displayName: data.displayName || data.userId || roleLabels[nextRole]
        });

        if (nextRole !== role) {
          setRoleState(nextRole);
        }
      } catch {
        if (cancelled) return;

        setRuntimeContext(null);
        if (!canUseLocalTestDoubles) {
          setRoleState("Client");
        }
      } finally {
        if (!cancelled) {
          setIsLoadingContext(false);
        }
      }
    }

    void loadRuntimeContext();

    return () => {
      cancelled = true;
    };
  }, [hasLoadedStoredRole, role]);

  const value = useMemo<RoleContextValue>(() => {
    const setRole = (nextRole: Role) => {
      if (!canUseLocalTestDoubles) {
        setRoleState("Client");
        return;
      }

      window.localStorage.setItem("iip-demo-role", nextRole);
      setRoleState(nextRole);
    };

    const activePermissions = runtimeContext?.permissions ?? (canUseLocalTestDoubles ? rolePermissions[role] : []);

    return {
      role,
      userId: runtimeContext?.userId ?? (canUseLocalTestDoubles ? localDevelopmentUserId : ""),
      displayName: runtimeContext?.displayName ?? (canUseLocalTestDoubles ? localDevelopmentUserId || roleLabels[role] : ""),
      roleLabel: runtimeContext?.roleLabel ?? roleLabels[role],
      tenantId: runtimeContext?.tenantId ?? "",
      workspaceId: runtimeContext?.workspaceId ?? "",
      permissions: activePermissions,
      contextSource: runtimeContext?.source ?? "Unavailable",
      isLoadingContext,
      setRole,
      can: (permission: string) => hasPermission(activePermissions, permission)
    };
  }, [isLoadingContext, role, runtimeContext]);

  return <RoleContext.Provider value={value}>{children}</RoleContext.Provider>;
}

export function useRole() {
  const context = useContext(RoleContext);
  if (!context) {
    throw new Error("useRole must be used inside RoleProvider.");
  }

  return context;
}
