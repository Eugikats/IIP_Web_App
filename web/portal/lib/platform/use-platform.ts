"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, canUseLocalTestDoubles, scopedHeaders } from "@/lib/config";
import { getEntraApiAccessToken } from "@/lib/entraAuth";

export const PLATFORM_API = "/api/v1/org-platform";

/** Fired after any platform change so the bell, cards and lists refresh together. */
export const PLATFORM_CHANGED_EVENT = "iip:platform-changed";

export function notifyPlatformChanged() {
  if (typeof window !== "undefined") window.dispatchEvent(new Event(PLATFORM_CHANGED_EVENT));
}

async function authorizedHeaders(role: string): Promise<Record<string, string>> {
  const token = canUseLocalTestDoubles ? null : await getEntraApiAccessToken();
  return {
    ...scopedHeaders(role, ""),
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

/** The API answers a refusal with ProblemDetails; show its own sentence rather than a status code. */
async function failureMessage(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as { detail?: unknown; title?: unknown };
    for (const value of [body.detail, body.title]) {
      if (typeof value === "string" && value.trim()) return value.trim();
    }
  } catch {
    // not JSON
  }
  if (response.status === 403) return "You do not have permission to do this.";
  if (response.status === 404) return "That could not be found.";
  return "The request could not be completed.";
}

/** GET a platform resource. `path` null skips the call. Refreshes on platform changes. */
export function usePlatformQuery<T>(path: string | null, options: { pollMs?: number } = {}) {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(Boolean(path));
  const seq = useRef(0);

  const load = useCallback(async () => {
    if (!path) return;
    const mine = ++seq.current;
    try {
      const response = await fetch(`${apiBaseUrl}${PLATFORM_API}${path}`, { headers: await authorizedHeaders(role) });
      if (!response.ok) throw new Error(await failureMessage(response));
      const body = (await response.json()) as T;
      if (mine !== seq.current) return;
      setData(body);
      setError(null);
    } catch (err) {
      if (mine !== seq.current) return;
      setError(err instanceof Error ? err.message : "Could not load.");
    } finally {
      if (mine === seq.current) setLoading(false);
    }
  }, [path, role]);

  useEffect(() => {
    setLoading(Boolean(path));
    void load();
  }, [load, path]);

  useEffect(() => {
    const onChange = () => void load();
    window.addEventListener(PLATFORM_CHANGED_EVENT, onChange);
    const timer = options.pollMs ? window.setInterval(onChange, options.pollMs) : null;
    return () => {
      window.removeEventListener(PLATFORM_CHANGED_EVENT, onChange);
      if (timer) window.clearInterval(timer);
    };
  }, [load, options.pollMs]);

  return { data, error, loading, reload: load };
}

/** POST / PATCH / PUT to the platform API. Returns the data or throws with the API's own message. */
export function usePlatformMutation() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  return useCallback(
    async <T,>(path: string, method: "POST" | "PATCH" | "PUT", body?: unknown): Promise<T> => {
      const response = await fetch(`${apiBaseUrl}${PLATFORM_API}${path}`, {
        method,
        headers: { ...(await authorizedHeaders(role)), "Content-Type": "application/json" },
        body: JSON.stringify(body ?? {}),
      });
      if (!response.ok) throw new Error(await failureMessage(response));
      notifyPlatformChanged();
      return (await response.json()) as T;
    },
    [role]
  );
}
