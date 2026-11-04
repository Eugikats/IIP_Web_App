"use client";

import { apiBaseUrl } from "@/lib/config";

/**
 * Sending files over a slow or unreliable connection (build-loop item 23). A file larger than one small
 * request is cut into pieces and sent a piece at a time, each piece retried with a growing wait if the
 * connection drops, and the upload can be picked up again after the page is reloaded. Everything here is
 * about getting the file across; what the server then does with it (consent, slot rules, keeping the
 * original untouched) is exactly what it does for any other upload.
 */

/** Must match ResumableUploadPolicy on the server. */
export const SINGLE_REQUEST_LIMIT = 256 * 1024;

export const MAX_ATTEMPTS = 8;

/** Slows or speeds up the waits between tries. 1 in real use; a test sets it low so retries are quick. */
const BACKOFF_SCALE = (() => {
  const raw = Number(process.env.NEXT_PUBLIC_UPLOAD_BACKOFF_SCALE);
  return Number.isFinite(raw) && raw > 0 ? raw : 1;
})();

export type UploadPhase = "sending" | "retrying" | "waiting" | "finishing";

export type UploadProgress = {
  fileName: string;
  fileIndex: number;
  fileCount: number;
  /** 0 to 100 for this file. */
  percent: number;
  phase: UploadPhase;
  attempt?: number;
  retryInSeconds?: number;
  /** True when this upload carried on from pieces that had already arrived. */
  resumed?: boolean;
};

export type UploadHandlers = {
  onProgress?: (progress: UploadProgress) => void;
};

/** The wait before try number <paramref name="attempt"/> (1 is the first retry): 1 s, 2 s, 4 s ... up to 30 s, plus or minus a fifth. */
export function backoffDelayMs(attempt: number, random: () => number = Math.random): number {
  const base = Math.min(30_000, 1000 * 2 ** Math.max(0, attempt - 1));
  const jitter = 1 + (random() * 0.4 - 0.2);
  return Math.round(base * jitter);
}

/** Server trouble and a lost connection are worth another try; a refusal the customer must act on is not. */
export function isRetryableStatus(status: number): boolean {
  return status === 0 || status === 408 || status === 425 || status === 429 || status >= 500;
}

export function percentOf(sentBytes: number, totalBytes: number): number {
  if (totalBytes <= 0) return 0;
  return Math.max(0, Math.min(100, Math.floor((sentBytes / totalBytes) * 100)));
}

export function chunkCount(size: number, chunkSize: number): number {
  return size <= 0 ? 0 : Math.ceil(size / chunkSize);
}

export function fingerprintFor(file: { name: string; size: number; lastModified: number }, slotKey: string | undefined): string {
  return `${file.name}:${file.size}:${file.lastModified}:${slotKey ?? "other"}`;
}

type HttpResult = { status: number; text: string };

class NetworkError extends Error {}

function xhrRequest(
  method: string,
  url: string,
  headers: Record<string, string>,
  body: Blob | FormData | string | null,
  onUploadBytes?: (loaded: number) => void,
): Promise<HttpResult> {
  return new Promise<HttpResult>((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open(method, url);
    for (const [key, value] of Object.entries(headers)) xhr.setRequestHeader(key, value);
    xhr.timeout = 90_000;
    if (onUploadBytes) {
      xhr.upload.onprogress = (event) => {
        if (event.lengthComputable) onUploadBytes(event.loaded);
      };
    }
    xhr.onload = () => resolve({ status: xhr.status, text: xhr.responseText });
    xhr.onerror = () => reject(new NetworkError("network"));
    xhr.ontimeout = () => reject(new NetworkError("timeout"));
    xhr.onabort = () => reject(new NetworkError("aborted"));
    xhr.send(body);
  });
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

/** Resolves when the browser says it is online again, or after a minute so a wrong report cannot hold an upload forever. */
function waitForOnline(): Promise<void> {
  if (typeof navigator === "undefined" || navigator.onLine) return Promise.resolve();
  return new Promise((resolve) => {
    const done = () => {
      window.removeEventListener("online", done);
      resolve();
    };
    window.addEventListener("online", done);
    setTimeout(done, 60_000);
  });
}

/**
 * Runs one request, trying again with a growing wait while the failure is the connection's or the server's.
 * Reports "retrying" (with how long until the next try) or "waiting" (offline) so the screen can say so.
 */
async function withRetry(
  attemptRequest: () => Promise<HttpResult>,
  onWaiting: (phase: UploadPhase, attempt: number, retryInSeconds: number) => void,
): Promise<HttpResult> {
  let last: HttpResult = { status: 0, text: "" };
  for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt += 1) {
    try {
      last = await attemptRequest();
      if (!isRetryableStatus(last.status)) return last;
    } catch (reason) {
      if (!(reason instanceof NetworkError)) throw reason;
      last = { status: 0, text: "" };
    }

    if (attempt === MAX_ATTEMPTS) break;
    if (typeof navigator !== "undefined" && !navigator.onLine) {
      onWaiting("waiting", attempt, 0);
      await waitForOnline();
    } else {
      const delay = Math.round(backoffDelayMs(attempt) * BACKOFF_SCALE);
      onWaiting("retrying", attempt, Math.max(1, Math.ceil(delay / 1000)));
      await sleep(delay);
    }
  }

  return last;
}

function messageFrom(result: HttpResult, fallback: string): string {
  try {
    const payload = JSON.parse(result.text) as { message?: string; errors?: Record<string, string[]> };
    if (payload.message) return payload.message;
    const first = payload.errors ? Object.values(payload.errors).flat()[0] : null;
    if (first) return first;
  } catch {
    // keep the fallback
  }
  return result.status === 0 ? "The connection was lost and did not come back. Your file was not lost: choose it again to carry on." : fallback;
}

// ---- what is remembered across a reload ----

export type UnfinishedUpload = { fingerprint: string; fileName: string; size: number; slotKey: string | null; uploadId: string; startedAt: number };

const storageKey = (caseId: string) => `iip.uploads.v1.${caseId}`;

function readMap(caseId: string): Record<string, UnfinishedUpload> {
  try {
    const raw = window.localStorage.getItem(storageKey(caseId));
    return raw ? (JSON.parse(raw) as Record<string, UnfinishedUpload>) : {};
  } catch {
    return {};
  }
}

function writeMap(caseId: string, map: Record<string, UnfinishedUpload>) {
  try {
    window.localStorage.setItem(storageKey(caseId), JSON.stringify(map));
  } catch {
    // Storage may be blocked; the upload still works, it just cannot be picked up after a reload.
  }
}

/** Uploads that were started and not finished, still within the time the server keeps them. */
export function unfinishedUploads(caseId: string): UnfinishedUpload[] {
  if (typeof window === "undefined") return [];
  const dayAgo = Date.now() - 23 * 60 * 60 * 1000;
  return Object.values(readMap(caseId)).filter((entry) => entry.startedAt >= dayAgo);
}

function remember(caseId: string, entry: UnfinishedUpload) {
  const map = readMap(caseId);
  map[entry.fingerprint] = entry;
  writeMap(caseId, map);
}

export function forgetUpload(caseId: string, fingerprint: string) {
  const map = readMap(caseId);
  delete map[fingerprint];
  writeMap(caseId, map);
}

// ---- sending ----

export type SendResult<T> = { ok: true; value: T } | { ok: false; error: string };

type Context = {
  caseId: string;
  headers: Record<string, string>;
  handlers: UploadHandlers;
  fileName: string;
  fileIndex: number;
  fileCount: number;
};

function report(context: Context, partial: Partial<UploadProgress> & Pick<UploadProgress, "percent" | "phase">) {
  context.handlers.onProgress?.({ fileName: context.fileName, fileIndex: context.fileIndex, fileCount: context.fileCount, ...partial });
}

async function sha256Hex(file: Blob): Promise<string | undefined> {
  try {
    if (typeof crypto === "undefined" || !crypto.subtle) return undefined;
    const digest = await crypto.subtle.digest("SHA-256", await file.arrayBuffer());
    return Array.from(new Uint8Array(digest)).map((byte) => byte.toString(16).padStart(2, "0")).join("");
  } catch {
    return undefined;
  }
}

/** A small file goes in one request, with progress and the same retry. */
export async function sendSmallFile<T>(
  file: File,
  slotKey: string | undefined,
  sessionToken: string,
  caseId: string,
  baseHeaders: Record<string, string>,
  handlers: UploadHandlers,
  position: { fileIndex: number; fileCount: number },
): Promise<SendResult<T>> {
  const context: Context = { caseId, headers: { ...baseHeaders, "X-Client-Portal-Session": sessionToken }, handlers, fileName: file.name, ...position };
  report(context, { percent: 0, phase: "sending" });
  const form = new FormData();
  form.append("files", file);
  if (slotKey) form.append("documentTypeKey", slotKey);
  const result = await withRetry(
    () => xhrRequest("POST", `${apiBaseUrl}/api/v1/client-portal/claims/${caseId}/documents`, context.headers, form, (loaded) => report(context, { percent: percentOf(loaded, file.size), phase: "sending" })),
    (phase, attempt, retryInSeconds) => report(context, { percent: 0, phase, attempt, retryInSeconds }),
  );
  if (result.status >= 200 && result.status < 300) {
    report(context, { percent: 100, phase: "finishing" });
    return { ok: true, value: JSON.parse(result.text) as T };
  }

  return { ok: false, error: messageFrom(result, "We could not upload your document. Try again shortly.") };
}

/**
 * A larger file goes up a piece at a time. If the same file was started before (the connection dropped, or the
 * page was reloaded) it carries on after the last piece that arrived. Nothing here changes the file.
 */
export async function sendFileInPieces<T>(
  file: File,
  slotKey: string | undefined,
  sessionToken: string,
  caseId: string,
  baseHeaders: Record<string, string>,
  handlers: UploadHandlers,
  position: { fileIndex: number; fileCount: number },
  restarted = false,
): Promise<SendResult<T>> {
  const headers = { ...baseHeaders, "X-Client-Portal-Session": sessionToken };
  const context: Context = { caseId, headers, handlers, fileName: file.name, ...position };
  const base = `${apiBaseUrl}/api/v1/client-portal/claims/${caseId}/uploads`;
  const waiting = (phase: UploadPhase, attempt: number, retryInSeconds: number) => report(context, { percent: 0, phase, attempt, retryInSeconds });
  const json = { ...headers, "Content-Type": "application/json" };
  const fingerprint = fingerprintFor(file, slotKey);

  report(context, { percent: 0, phase: "sending" });
  const started = await withRetry(
    () => xhrRequest("POST", base, json, JSON.stringify({ fileName: file.name, contentType: file.type || "application/octet-stream", size: file.size, documentTypeKey: slotKey ?? null, fingerprint })),
    waiting,
  );
  if (started.status !== 200) return { ok: false, error: messageFrom(started, "We could not start sending your file. Try again shortly.") };
  const session = JSON.parse(started.text) as { uploadId: string; chunkSize: number; totalChunks: number; received: number[] };
  remember(caseId, { fingerprint, fileName: file.name, size: file.size, slotKey: slotKey ?? null, uploadId: session.uploadId, startedAt: Date.now() });

  const arrived = new Set(session.received);
  let confirmedBytes = 0;
  for (const index of arrived) confirmedBytes += Math.min(session.chunkSize, file.size - index * session.chunkSize);
  const resumed = arrived.size > 0;
  report(context, { percent: percentOf(confirmedBytes, file.size), phase: "sending", resumed });

  for (let index = 0; index < session.totalChunks; index += 1) {
    if (arrived.has(index)) continue;
    const start = index * session.chunkSize;
    const piece = file.slice(start, Math.min(file.size, start + session.chunkSize));
    const result = await withRetry(
      () => xhrRequest("PUT", `${base}/${session.uploadId}/chunks/${index}`, { ...headers, "Content-Type": "application/octet-stream" }, piece, (loaded) =>
        report(context, { percent: percentOf(confirmedBytes + loaded, file.size), phase: "sending", resumed })),
      (phase, attempt, retryInSeconds) => report(context, { percent: percentOf(confirmedBytes, file.size), phase, attempt, retryInSeconds, resumed }),
    );
    if (result.status === 410 && !restarted) {
      // The server let this upload lapse; start the file again from the beginning, once.
      forgetUpload(caseId, fingerprint);
      return sendFileInPieces<T>(file, slotKey, sessionToken, caseId, baseHeaders, handlers, position, true);
    }

    if (result.status !== 200) return { ok: false, error: messageFrom(result, "We could not send your file. Try again shortly.") };
    confirmedBytes += piece.size;
    arrived.add(index);
    report(context, { percent: percentOf(confirmedBytes, file.size), phase: "sending", resumed });
  }

  report(context, { percent: 100, phase: "finishing", resumed });
  const sha256 = await sha256Hex(file);
  const finished = await withRetry(() => xhrRequest("POST", `${base}/${session.uploadId}/complete`, json, JSON.stringify({ sha256 })), waiting);
  if (finished.status >= 200 && finished.status < 300) {
    forgetUpload(caseId, fingerprint);
    return { ok: true, value: JSON.parse(finished.text) as T };
  }

  if (finished.status === 409 && !restarted) {
    // A piece did not arrive after all, or the file arrived damaged: start it again once, clean.
    forgetUpload(caseId, fingerprint);
    await xhrRequest("DELETE", `${base}/${session.uploadId}`, headers, null).catch(() => undefined);
    return sendFileInPieces<T>(file, slotKey, sessionToken, caseId, baseHeaders, handlers, position, true);
  }

  return { ok: false, error: messageFrom(finished, "We could not finish sending your file. Try again shortly.") };
}
