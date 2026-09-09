"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

async function readSafeApiError(response: Response, fallback: string): Promise<string> {
  try {
    const body = (await response.json()) as { message?: unknown };
    if (typeof body.message === "string" && body.message.trim()) return body.message;
  } catch {
    // The fallback remains safe for non-JSON or empty error responses.
  }
  return fallback;
}

export type AdminActionState = { action: string; label: string; requiredPermission: string; reason: string | null };

export type AdminUserView = {
  userId: string;
  name: string;
  email: string;
  phone: string | null;
  role: string;
  department: string | null;
  branch: string | null;
  status: string;
  lastLogin: string | null;
  allowedActions: AdminActionState[];
  blockedActions: AdminActionState[];
};

export type AdminRoleView = {
  role: string;
  permissions: string[];
  approvalLimit: number | null;
  caseVisibilityScope: string;
};

// WO-12: real client for GET/POST /api/v1/admin/users and PUT /api/v1/admin/users/{id}/role —
// these endpoints existed with no UI caller at all before this. Roles come from
// GET /api/v1/admin/roles (server-derived from RolePermissionMap), never a hardcoded client list.
export function useAdminUsers() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [users, setUsers] = useState<AdminUserView[]>([]);
  const [roles, setRoles] = useState<AdminRoleView[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [usersResponse, rolesResponse] = await Promise.all([
        fetch(`${apiBaseUrl}/api/v1/admin/users`, { headers: scopedHeaders(role, "") }),
        fetch(`${apiBaseUrl}/api/v1/admin/roles`, { headers: scopedHeaders(role, "") }),
      ]);
      if (!usersResponse.ok) throw new Error(await readSafeApiError(usersResponse, `Users failed.`));
      if (!rolesResponse.ok) throw new Error(await readSafeApiError(rolesResponse, `Roles failed.`));
      setUsers((await usersResponse.json()) as AdminUserView[]);
      setRoles((await rolesResponse.json()) as AdminRoleView[]);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Users failed to load.");
      setUsers([]);
    } finally {
      setLoading(false);
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  const createUser = useCallback(async (input: {
    userId: string; name: string; email: string; phone?: string; role: string; department?: string; branch?: string;
  }): Promise<{ ok: boolean; message?: string }> => {
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/users`, {
      method: "POST",
      headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
      body: JSON.stringify(input),
    });
    if (!response.ok) return { ok: false, message: await readSafeApiError(response, `Create user failed.`) };
    await load();
    return { ok: true };
  }, [role, load]);

  const updateUserRole = useCallback(async (userId: string, newRole: string, auditReason: string): Promise<{ ok: boolean; message?: string }> => {
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/users/${encodeURIComponent(userId)}/role`, {
      method: "PUT",
      headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json", "X-Audit-Reason": auditReason },
      body: JSON.stringify({ role: newRole }),
    });
    if (!response.ok) return { ok: false, message: await readSafeApiError(response, `Role update failed.`) };
    await load();
    return { ok: true };
  }, [role, load]);

  return { users, roles, loading, error, createUser, updateUserRole, reload: load };
}
