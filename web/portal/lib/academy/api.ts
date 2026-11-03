"use client";

import { apiBaseUrl, canUseLocalTestDoubles, scopedHeaders } from "@/lib/config";
import { tryGetEntraApiAccessToken } from "@/lib/entraAuth";

// The Academy's calls to the API. Public pages work without signing in, so a token is attached only when the person is
// already signed in, and nothing here ever starts a sign-in on its own.

export class AcademyApiError extends Error {
  constructor(public status: number, message: string, public code?: string) {
    super(message);
  }
}

let lastSessionHeaders: Record<string, string> = {};

export function academyLeave(path: string, json: unknown): Promise<Response> {
  return fetch(`${apiBaseUrl}/api/v1/academy${path}`, {
    method: "POST",
    keepalive: true,
    headers: { ...lastSessionHeaders, "Content-Type": "application/json" },
    body: JSON.stringify(json),
  });
}

export async function academyFetch<T = unknown>(path: string, init: RequestInit & { json?: unknown } = {}): Promise<T> {
  const token = await tryGetEntraApiAccessToken();
  const headers: Record<string, string> = {
    ...(canUseLocalTestDoubles ? scopedHeaders("Admin", "") : {}),
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...((init.headers as Record<string, string> | undefined) ?? {}),
  };
  if (init.json !== undefined) headers["Content-Type"] = "application/json";
  lastSessionHeaders = headers;
  const response = await fetch(`${apiBaseUrl}/api/v1/academy${path}`, {
    ...init,
    cache: "no-store",
    headers,
    body: init.json !== undefined ? JSON.stringify(init.json) : init.body,
  });
  if (response.status === 204) return undefined as T;
  const payload = (await response.json().catch(() => ({}))) as { message?: string; code?: string; errors?: Record<string, string[]> };
  if (!response.ok) {
    throw new AcademyApiError(
      response.status,
      payload.message ?? Object.values(payload.errors ?? {})[0]?.[0] ?? (response.status === 429 ? "Too many requests. Try again in a moment." : "That did not work."),
      payload.code,
    );
  }
  return payload as T;
}

export const academyGet = <T,>(path: string) => academyFetch<T>(path);
export async function academyMedia(path: string, signal?: AbortSignal): Promise<Blob> {
  const token = await tryGetEntraApiAccessToken();
  const headers = {
    ...(canUseLocalTestDoubles ? scopedHeaders("Admin", "") : {}),
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
  const response = await fetch(`${apiBaseUrl}/api/v1/academy${path}`, { headers, signal, cache: "no-store" });
  if (!response.ok) throw new AcademyApiError(response.status, response.status === 403 ? "Enrol in this course to open its media." : "Lesson media could not be loaded. Try again.");
  return response.blob();
}
export const academyPost = <T,>(path: string, json?: unknown) => academyFetch<T>(path, { method: "POST", json: json ?? {} });
export const academyPut = <T,>(path: string, json: unknown) => academyFetch<T>(path, { method: "PUT", json });
export const academyDelete = <T,>(path: string) => academyFetch<T>(path, { method: "DELETE" });
