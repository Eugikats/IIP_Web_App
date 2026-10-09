// A person's own Google Drive and OneDrive connection. Every call goes to the API as the signed-in person;
// the access tokens never reach the browser.

import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { getEntraApiAccessToken } from "@/lib/entraAuth";

export type FileProviderKey = "google" | "microsoft";

export type FileConnectionStatus = {
  provider: FileProviderKey;
  configured: boolean;
  connected: boolean;
  accountEmail: string | null;
  reason: string | null;
};

export type FileItem = {
  id: string;
  name: string;
  isFolder: boolean;
  size: number | null;
  mimeType: string | null;
  modifiedAt: string | null;
};

export type GooglePickerSession = { accessToken: string; appId: string; developerKey: string | null; accountEmail: string | null };

export class FileConnectionError extends Error {
  constructor(public code: string, message: string) {
    super(message);
  }
}

const PENDING_KEY = "iip.fileConnect.pending";

async function call(path: string, init: RequestInit = {}, role = "ClaimsOfficer"): Promise<Response> {
  const token = await getEntraApiAccessToken();
  return fetch(`${apiBaseUrl}/api/v1/me/file-connections${path}`, {
    ...init,
    cache: "no-store",
    headers: {
      ...scopedHeaders(role, ""),
      ...(init.body ? { "Content-Type": "application/json" } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(init.headers ?? {}),
    },
  });
}

async function failure(response: Response): Promise<never> {
  const body = (await response.json().catch(() => ({}))) as { code?: string; message?: string };
  throw new FileConnectionError(body.code ?? "error", body.message ?? "The request could not be completed.");
}

export async function getFileConnectionStatus(role?: string): Promise<FileConnectionStatus[]> {
  const response = await call("", {}, role);
  if (!response.ok) return failure(response);
  return (await response.json()) as FileConnectionStatus[];
}

/** Sends the person to the provider's own sign-in page. They come back to /app/connect/callback. */
export async function startFileConnection(provider: FileProviderKey, returnTo: string, role?: string, sharePoint = false): Promise<void> {
  const response = await call(`/${provider}/start`, { method: "POST", body: JSON.stringify({ returnTo, sharePoint }) }, role);
  if (!response.ok) return failure(response);
  const { authorizeUrl } = (await response.json()) as { authorizeUrl: string };
  try {
    window.sessionStorage.setItem(PENDING_KEY, provider);
  } catch {
    // Without storage the callback cannot tell which provider this was; it says so and asks to start again.
  }
  window.location.assign(authorizeUrl);
}

export function pendingFileProvider(): FileProviderKey | null {
  try {
    const value = window.sessionStorage.getItem(PENDING_KEY);
    return value === "google" || value === "microsoft" ? value : null;
  } catch {
    return null;
  }
}

export function clearPendingFileProvider(): void {
  try {
    window.sessionStorage.removeItem(PENDING_KEY);
  } catch {
    // Nothing to clear.
  }
}

export async function finishFileConnection(provider: FileProviderKey, code: string, state: string, role?: string) {
  const response = await call(`/${provider}/exchange`, { method: "POST", body: JSON.stringify({ code, state }) }, role);
  if (!response.ok) return failure(response);
  return (await response.json()) as { provider: FileProviderKey; accountEmail: string | null; returnTo: string | null };
}

export async function disconnectFileProvider(provider: FileProviderKey, role?: string): Promise<void> {
  const response = await call(`/${provider}`, { method: "DELETE" }, role);
  if (!response.ok) return failure(response);
}

export async function browseOneDrive(folderId: string | null, role?: string): Promise<FileItem[]> {
  const response = await call(`/microsoft/files${folderId ? `?folderId=${encodeURIComponent(folderId)}` : ""}`, {}, role);
  if (!response.ok) return failure(response);
  return ((await response.json()) as { items: FileItem[] }).items;
}

/** Reads one chosen file as a File object, ready for the same reader a computer upload uses. */
export async function downloadFile(provider: FileProviderKey, fileId: string, role?: string): Promise<File> {
  const response = await call(`/${provider}/files/${encodeURIComponent(fileId)}/content`, {}, role);
  if (!response.ok) return failure(response);
  const name = decodeURIComponent(response.headers.get("X-File-Name") ?? "list");
  return new File([await response.blob()], name, { type: response.headers.get("Content-Type") ?? undefined });
}

export async function getGooglePickerSession(role?: string): Promise<GooglePickerSession> {
  const response = await call("/google/picker", {}, role);
  if (!response.ok) return failure(response);
  return (await response.json()) as GooglePickerSession;
}
