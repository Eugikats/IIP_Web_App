"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { readEventStream } from "@/lib/lovable-parity/data/commandCenterQuery";
import type { VoiceApi } from "@/lib/voice/types";

/** A refusal from the voice endpoints, with the plain words to show the person. */
export class VoiceApiError extends Error {
  code: string;
  status: number;

  constructor(code: string, message: string, status: number) {
    super(message);
    this.name = "VoiceApiError";
    this.code = code;
    this.status = status;
  }
}

export type VoiceStatus = {
  /** Live realtime voice is set up for this environment. */
  available: boolean;
  /** Voice notes and turn-by-turn conversation (including Luganda and Swahili) are set up for this workspace. */
  turnBasedAvailable: boolean;
  /** Where the speech is processed, said before a person's first call. */
  processedIn: string;
};

async function failure(response: Response): Promise<VoiceApiError> {
  const body = (await response.json().catch(() => null)) as { code?: string; message?: string } | null;
  if (response.status === 403) return new VoiceApiError("forbidden", "You do not have permission to use voice.", 403);
  return new VoiceApiError(body?.code ?? "failed", body?.message ?? "Voice could not start just now. Try again, or type.", response.status);
}

export async function fetchVoiceStatus(role: string): Promise<VoiceStatus | null> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/voice/status`, { headers: scopedHeaders(role, "") });
    if (response.ok) return (await response.json()) as VoiceStatus;
    if (response.status === 404 || response.status === 405) {
      // A server without live voice still offers voice notes and turn-by-turn talking when a speech provider is connected.
      const older = await fetch(`${apiBaseUrl}/api/v1/command-center/voice-note/status`, { headers: scopedHeaders(role, "") });
      if (!older.ok) return null;
      const body = (await older.json()) as { available?: boolean };
      return { available: false, turnBasedAvailable: Boolean(body.available), processedIn: "" };
    }
    return null;
  } catch {
    return null;
  }
}

export type SpokenReply = { text: string; language: string; translated: boolean; audio: string | null; mimeType: string | null };

/** A written answer turned into Luganda or Swahili and, when a voice is available, spoken. Null when the request failed. */
export async function fetchSpokenReply(role: string, text: string, language: "lg" | "sw"): Promise<SpokenReply | null> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/command-center/voice-reply`, {
      method: "POST",
      headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
      body: JSON.stringify({ text, language }),
    });
    return response.ok ? ((await response.json()) as SpokenReply) : null;
  } catch {
    return null;
  }
}

export type Nudge = { id: string; text: string; href: string | null; private: boolean };

/** The next thing worth saying, or null when there is nothing (the server keeps to working hours and a daily allowance). */
export async function fetchNudge(role: string, locked: boolean): Promise<Nudge | null> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/voice/nudge?locked=${locked ? "true" : "false"}`, { headers: scopedHeaders(role, "") });
    if (response.status !== 200) return null;
    const body = (await response.json()) as Nudge;
    return typeof body.text === "string" && body.text ? body : null;
  } catch {
    return null;
  }
}

export function createVoiceApi(role: string): VoiceApi {
  const send = async (path: string, body?: unknown): Promise<Response> => {
    const response = await fetch(`${apiBaseUrl}/api/v1/voice/${path}`, {
      method: "POST",
      headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    if (!response.ok) throw await failure(response);
    return response;
  };

  return {
    async prepare() {
      return (await (await send("session/prepare")).json()) as { prepareId: string };
    },
    async start(request) {
      return (await (await send("session", request)).json()) as { sessionId: string; sdpAnswer: string; maxMinutes: number };
    },
    async lock(sessionId) {
      await send(`session/${sessionId}/lock`);
    },
    async unlock(sessionId) {
      await send(`session/${sessionId}/unlock`);
    },
    async end(sessionId) {
      await send(`session/${sessionId}/end`);
    },
    async metrics(marks) {
      await send("metrics", { marks });
    },
    events(sessionId, onEvent) {
      const controller = new AbortController();
      void (async () => {
        try {
          const response = await fetch(`${apiBaseUrl}/api/v1/voice/session/${sessionId}/events`, { headers: scopedHeaders(role, ""), signal: controller.signal });
          if (!response.ok || !response.body) return;
          for await (const { event, data } of readEventStream(response.body)) onEvent(event, data);
        } catch {
          /* the feed closing is how a call ends; the call itself reports the reason */
        }
      })();
      return () => controller.abort();
    },
  };
}
