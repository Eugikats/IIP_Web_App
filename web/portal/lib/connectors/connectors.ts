// Folder connectors: a standing link from folders in a person's Google Drive, OneDrive or SharePoint to the Data Hub.
// Everything a connector brings in arrives as a pending document; nothing is approved by being synced.

import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { getEntraApiAccessToken } from "@/lib/entraAuth";

export type ConnectorFolder = {
  folderId: string;
  name: string;
  driveId: string | null;
  productLine: string;
  documentType: string;
  isFile: boolean;
};

export type ConnectorRun = {
  startedAt: string;
  finishedAt: string | null;
  outcome: "Succeeded" | "Partial" | "Failed" | "SignInExpired" | null;
  found: number;
  added: number;
  updated: number;
  removed: number;
  skipped: number;
  failed: number;
  message: string | null;
};

export type Connector = {
  id: string;
  provider: "google" | "microsoft";
  providerName: string;
  owner: string;
  account: string | null;
  status: "Active" | "Paused" | "NeedsReconnect" | "Error";
  statusLabel: string;
  statusNote: string | null;
  folders: ConnectorFolder[];
  lastRunAt: string | null;
  lastOutcome: string | null;
  counts: { pending: number; skipped: number; failed: number; removed: number };
  recentRuns: ConnectorRun[];
};

export type FolderInput = { folderId: string; displayName: string; driveId?: string | null; productLine: string; documentType: string; isFile: boolean };

export class ConnectorError extends Error {
  constructor(public status: number, message: string) {
    super(message);
  }
}

async function call(path: string, role: string, method = "GET", body?: unknown): Promise<unknown> {
  const token = await getEntraApiAccessToken();
  const response = await fetch(`${apiBaseUrl}/api/v1/connectors${path}`, {
    method,
    cache: "no-store",
    headers: { ...scopedHeaders(role, ""), ...(body ? { "Content-Type": "application/json" } : {}), ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  });
  const payload = (await response.json().catch(() => ({}))) as { message?: string; errors?: Record<string, string[]> };
  if (!response.ok) {
    throw new ConnectorError(response.status, payload.message ?? Object.values(payload.errors ?? {})[0]?.[0] ?? "That did not work.");
  }
  return payload;
}

export async function listConnectors(role: string): Promise<Connector[]> {
  return ((await call("", role)) as { connectors: Connector[] }).connectors;
}

export const createConnector = (role: string, provider: "google" | "microsoft", folders: FolderInput[]) => call("", role, "POST", { provider, folders });

export const replaceConnectorFolders = (role: string, id: string, folders: FolderInput[]) => call(`/${id}/folders`, role, "PUT", { folders });

export const syncConnectorNow = (role: string, id: string) => call(`/${id}/sync`, role, "POST") as Promise<ConnectorRun>;

export const pauseConnector = (role: string, id: string) => call(`/${id}/pause`, role, "POST");

export const resumeConnector = (role: string, id: string) => call(`/${id}/resume`, role, "POST");

export const deleteConnector = (role: string, id: string) => call(`/${id}`, role, "DELETE");

export function toInput(folder: ConnectorFolder): FolderInput {
  return { folderId: folder.folderId, displayName: folder.name, driveId: folder.driveId, productLine: folder.productLine, documentType: folder.documentType, isFile: folder.isFile };
}
