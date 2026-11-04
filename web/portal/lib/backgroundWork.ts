"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type BackgroundJob = {
  jobId: string;
  queue: string;
  type: string;
  what: string;
  status: "Pending" | "Running" | "Succeeded" | "DeadLettered";
  attempts: number;
  maxAttempts: number;
  nextAttemptAt: string | null;
  lastError: string | null;
  createdAt: string;
  completedAt: string | null;
  caseId: string | null;
  claimReference: string | null;
};

export type BackgroundWork = {
  pending: number;
  running: number;
  succeeded: number;
  deadLettered: number;
  jobs: BackgroundJob[];
};

export type BackgroundWorkResult<T> = { ok: true; value: T } | { ok: false; error: string };

async function failure(response: Response, fallback: string): Promise<string> {
  try {
    const payload = (await response.json()) as { message?: string };
    if (payload.message) return payload.message;
  } catch {
    // keep the fallback
  }
  return response.status === 403 ? "You do not have permission to see this." : fallback;
}

export async function loadBackgroundWork(role: string, status?: string): Promise<BackgroundWorkResult<BackgroundWork>> {
  try {
    const query = status ? `?status=${encodeURIComponent(status)}` : "";
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/background-jobs${query}`, { headers: scopedHeaders(role, ""), cache: "no-store" });
    if (!response.ok) return { ok: false, error: await failure(response, "The background work could not be loaded.") };
    return { ok: true, value: (await response.json()) as BackgroundWork };
  } catch {
    return { ok: false, error: "The service could not be reached. Check your connection and try again." };
  }
}

export async function retryBackgroundJob(role: string, jobId: string): Promise<BackgroundWorkResult<BackgroundJob>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/background-jobs/${jobId}/retry`, { method: "POST", headers: scopedHeaders(role, "") });
    if (!response.ok) return { ok: false, error: await failure(response, "It could not be sent back.") };
    return { ok: true, value: (await response.json()) as BackgroundJob };
  } catch {
    return { ok: false, error: "The service could not be reached. Check your connection and try again." };
  }
}
