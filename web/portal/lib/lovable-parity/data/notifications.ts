"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, canUseLocalTestDoubles, scopedHeaders } from "@/lib/config";
import { getEntraApiAccessToken } from "@/lib/entraAuth";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";

export type NotificationView = {
  notificationId: string;
  type: string;
  message: string;
  caseId: string;
  createdAt: string;
  isRead: boolean;
  readAt: string | null;
};

type FeedDto = {
  unreadCount: number;
  notifications: NotificationView[];
};

async function authorizedHeaders(role: string): Promise<Record<string, string>> {
  const token = canUseLocalTestDoubles ? null : await getEntraApiAccessToken();
  return {
    ...scopedHeaders(role, ""),
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

// WO-14: real client for GET/POST /api/v1/notifications — the bell previously advertised an
// unread state that had no backing domain at all. No hardcoded count or sample rows (A5); an
// empty feed renders as zero, never a fabricated placeholder.
export function useNotifications() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [unreadCount, setUnreadCount] = useState(0);
  const [notifications, setNotifications] = useState<NotificationView[]>([]);
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) {
      setUnreadCount(0);
      setNotifications([]);
      setLoading(false);
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/notifications?limit=20`, {
        headers: await authorizedHeaders(role),
      });
      if (!response.ok) throw new Error(`Notifications failed with HTTP ${response.status}.`);
      const body = (await response.json()) as FeedDto;
      setUnreadCount(body.unreadCount ?? 0);
      setNotifications(body.notifications ?? []);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Notifications failed to load.");
      setUnreadCount(0);
      setNotifications([]);
    } finally {
      setLoading(false);
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  const markRead = useCallback(async (notificationId: string) => {
    const response = await fetch(`${apiBaseUrl}/api/v1/notifications/${encodeURIComponent(notificationId)}/read`, {
      method: "POST",
      headers: await authorizedHeaders(role),
    });
    if (response.ok) await load();
  }, [role, load]);

  const markAllRead = useCallback(async () => {
    const response = await fetch(`${apiBaseUrl}/api/v1/notifications/read-all`, {
      method: "POST",
      headers: await authorizedHeaders(role),
    });
    if (response.ok) await load();
  }, [role, load]);

  return { unreadCount, notifications, loading, error, markRead, markAllRead, reload: load };
}
