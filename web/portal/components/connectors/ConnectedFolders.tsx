"use client";

import { useCallback, useEffect, useState } from "react";
import { Folder, FileText, Loader2, Plus, RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { StatusPill, type PillTone } from "@/components/ui/StatusPill";
import { LoadingSkeleton } from "@/components/ui/StateSurfaces";
import { useUI } from "@/context/UIStateContext";
import { formatDateTime } from "@/lib/format";
import { cn } from "@/lib/utils";
import { getFileConnectionStatus, type FileConnectionStatus } from "@/lib/files/fileConnections";
import {
  ConnectorError, createConnector, deleteConnector, listConnectors, pauseConnector, replaceConnectorFolders, resumeConnector, syncConnectorNow, toInput,
  type Connector, type ConnectorFolder, type FolderInput,
} from "@/lib/connectors/connectors";
import { OneDriveFolderPicker, pickDocumentsFromGoogleDrive } from "@/components/files/FilePicker";

// Connected folders: files that arrive from a bank's or insurer's own folders, as pending documents. Nothing that arrives
// here answers a customer until a person classifies and approves it in the Data Hub below.

const DOCUMENT_TYPES = ["PolicyWording", "ClaimsChecklist", "CustomerFaq", "ClaimsProcedure", "RegulatoryGuidance", "CompanyProfile", "ProductOverview", "Faq", "Other"];

const OUTCOME_TEXT: Record<string, string> = {
  Succeeded: "The last sync finished cleanly.",
  Partial: "The last sync finished, but something could not be read.",
  Failed: "The last sync did not finish.",
  SignInExpired: "The last sync could not sign in.",
};

function toneOf(connector: Connector): PillTone {
  return connector.status === "Active" ? "success" : connector.status === "Paused" ? "neutral" : "warning";
}

export default function ConnectedFolders() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [connectors, setConnectors] = useState<Connector[] | null>(null);
  const [problem, setProblem] = useState<{ kind: "forbidden" | "error"; text: string } | null>(null);
  const [adding, setAdding] = useState(false);
  const [notice, setNotice] = useState<{ tone: "ok" | "bad"; text: string } | null>(null);

  const load = useCallback(async () => {
    try {
      setConnectors(await listConnectors(role));
      setProblem(null);
    } catch (reason) {
      setConnectors([]);
      setProblem(reason instanceof ConnectorError && reason.status === 403
        ? { kind: "forbidden", text: "Your role cannot manage connected folders." }
        : { kind: "error", text: "Connected folders could not be read just now." });
    }
  }, [role]);

  useEffect(() => { void load(); }, [load]);

  return (
    <section aria-label="Connected folders" data-testid="connected-folders" className="mb-8">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h2 className="m-0 text-[16px] font-bold text-dls-text-primary">Connected folders</h2>
          <p className="m-0 mt-1 max-w-2xl text-[13px] text-dls-text-secondary">
            Keep policy wordings, product sheets and procedures in step with the folders your team already uses. Files arrive below as pending documents. Nothing is used to answer a customer until a person classifies and approves it.
          </p>
        </div>
        {problem?.kind === "forbidden" ? null : (
          <Button variant="secondary" onClick={() => setAdding(true)} icon={<Plus aria-hidden="true" />}>Connect folders</Button>
        )}
      </div>

      {notice ? <p role="status" className={cn("m-0 mt-3 text-[12.5px]", notice.tone === "ok" ? "text-dls-success-text" : "text-dls-danger-text")}>{notice.text}</p> : null}

      <div className="mt-4">
        {connectors === null ? (
          <LoadingSkeleton rows={2} />
        ) : problem ? (
          <p role={problem.kind === "error" ? "alert" : "status"} className="m-0 rounded-xl border border-dls-border-subtle bg-dls-surface p-4 text-[13px] text-dls-text-secondary">{problem.text}</p>
        ) : connectors.length === 0 ? (
          <div className="rounded-xl border border-dls-border-subtle bg-dls-surface p-5 text-[13px] text-dls-text-secondary">
            No folders are connected. Connect a OneDrive or SharePoint folder, or pick files from Google Drive, and they will appear here as pending documents.
          </div>
        ) : (
          <ul className="m-0 space-y-3 p-0">
            {connectors.map((connector) => (
              <ConnectorCard key={connector.id} connector={connector} role={role} onChanged={load} onNotice={setNotice} />
            ))}
          </ul>
        )}
      </div>

      {adding ? <AddConnector role={role} onClose={() => setAdding(false)} onCreated={() => { setAdding(false); setNotice({ tone: "ok", text: "Connected. The first sync runs within a few minutes, or press Sync now." }); void load(); }} /> : null}
    </section>
  );
}

function ConnectorCard({ connector, role, onChanged, onNotice }: { connector: Connector; role: string; onChanged: () => Promise<void>; onNotice: (n: { tone: "ok" | "bad"; text: string } | null) => void }) {
  const [busy, setBusy] = useState<string | null>(null);
  const [confirm, setConfirm] = useState<{ kind: "delete" } | { kind: "folder"; folder: ConnectorFolder } | null>(null);
  const last = connector.recentRuns[0];

  async function act(name: string, work: () => Promise<unknown>, done: (result: unknown) => string) {
    setBusy(name);
    onNotice(null);
    try {
      const result = await work();
      onNotice({ tone: "ok", text: done(result) });
      setConfirm(null);
      await onChanged();
    } catch (reason) {
      onNotice({ tone: "bad", text: reason instanceof Error ? reason.message : "That did not work." });
    } finally {
      setBusy(null);
    }
  }

  const label = connector.provider === "google" ? "file" : "folder";

  return (
    <li className="list-none rounded-xl border border-dls-border-subtle bg-dls-surface p-4" data-testid={`connector-${connector.id}`}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="min-w-0">
          <h3 className="m-0 text-[14.5px] font-bold text-dls-text-primary">{connector.providerName}</h3>
          <p className="m-0 text-[12.5px] text-dls-text-muted">Reads as {connector.account ?? connector.owner}, so it sees only what that person can open.</p>
        </div>
        <StatusPill tone={toneOf(connector)} label={connector.statusLabel} />
      </div>

      {connector.statusNote ? <p role="alert" className="m-0 mt-2 rounded-lg border border-dls-warning-dot bg-dls-warning-tint px-3 py-2 text-[12.5px] text-dls-text-primary">{connector.statusNote}</p> : null}

      <ul className="m-0 mt-3 space-y-1.5 p-0" aria-label={`${connector.providerName} ${label}s`}>
        {connector.folders.map((folder) => (
          <li key={folder.folderId} className="flex list-none flex-wrap items-center justify-between gap-2 text-[13px] text-dls-text-primary">
            <span className="flex min-w-0 items-center gap-2">
              {folder.isFile ? <FileText className="h-4 w-4 shrink-0 text-dls-text-muted" aria-hidden /> : <Folder className="h-4 w-4 shrink-0 text-dls-text-muted" aria-hidden />}
              <span className="truncate font-medium">{folder.name}</span>
              <span className="text-dls-text-muted">{folder.productLine}, {folder.documentType.replace(/([A-Z])/g, " $1").trim().toLowerCase()}</span>
            </span>
            {confirm?.kind === "folder" && confirm.folder.folderId === folder.folderId ? (
              <span className="flex flex-wrap items-center gap-2 text-[12.5px]">
                <span className="text-dls-text-secondary">Its files leave the Data Hub at once.</span>
                <Button variant="secondary" disabled={busy !== null} onClick={() => void act("folder", () => replaceConnectorFolders(role, connector.id, connector.folders.filter((f) => f.folderId !== folder.folderId).map(toInput)), (r) => `Removed. ${(r as { retired?: number }).retired ?? 0} file(s) were taken out of use.`)}>Remove {label}</Button>
                <button type="button" className="text-dls-text-secondary underline" onClick={() => setConfirm(null)}>Keep</button>
              </span>
            ) : connector.folders.length > 1 ? (
              <button type="button" className="text-[12.5px] text-dls-text-secondary underline" onClick={() => setConfirm({ kind: "folder", folder })}>Remove</button>
            ) : null}
          </li>
        ))}
      </ul>

      <p className="m-0 mt-3 text-[12.5px] text-dls-text-secondary">
        {connector.counts.pending} file{connector.counts.pending === 1 ? "" : "s"} waiting for a person to review
        {connector.counts.skipped > 0 ? `, ${connector.counts.skipped} skipped (unsupported or too large)` : ""}
        {connector.counts.failed > 0 ? `, ${connector.counts.failed} could not be read` : ""}.
      </p>
      <p className="m-0 mt-1 text-[12.5px] text-dls-text-muted" data-testid="last-sync">
        {connector.lastRunAt ? `Last synced ${formatDateTime(connector.lastRunAt)}. ${last ? `${last.added} new, ${last.updated} changed, ${last.removed} removed. ` : ""}${connector.lastOutcome ? OUTCOME_TEXT[connector.lastOutcome] ?? "" : ""}` : "Not synced yet."}
        {last?.message ? ` ${last.message}` : ""}
      </p>

      <div className="mt-3 flex flex-wrap items-center gap-2">
        <Button variant="secondary" disabled={busy !== null || connector.status === "NeedsReconnect"} onClick={() => void act("sync", () => syncConnectorNow(role, connector.id), (r) => `Synced: ${(r as { added: number }).added} new, ${(r as { updated: number }).updated} changed, ${(r as { removed: number }).removed} removed.`)} icon={busy === "sync" ? <Loader2 className="animate-spin" aria-hidden="true" /> : <RefreshCw aria-hidden="true" />}>
          Sync now
        </Button>
        {connector.status === "Paused" ? (
          <Button variant="secondary" disabled={busy !== null} onClick={() => void act("resume", () => resumeConnector(role, connector.id), () => "Resumed.")}>Resume</Button>
        ) : (
          <Button variant="secondary" disabled={busy !== null} onClick={() => void act("pause", () => pauseConnector(role, connector.id), () => "Paused. Nothing changes until you resume.")}>Pause</Button>
        )}
        {confirm?.kind === "delete" ? (
          <span className="flex flex-wrap items-center gap-2 text-[12.5px]">
            <span className="text-dls-text-secondary">Everything it brought in leaves the Data Hub at once.</span>
            <Button variant="secondary" disabled={busy !== null} onClick={() => void act("delete", () => deleteConnector(role, connector.id), (r) => `Deleted. ${(r as { retired?: number }).retired ?? 0} file(s) were taken out of use.`)}>Delete connector</Button>
            <button type="button" className="text-dls-text-secondary underline" onClick={() => setConfirm(null)}>Keep</button>
          </span>
        ) : (
          <button type="button" className="text-[13px] text-dls-text-secondary underline" onClick={() => setConfirm({ kind: "delete" })}>Delete</button>
        )}
      </div>
    </li>
  );
}

type Draft = FolderInput & { key: string };

function AddConnector({ role, onClose, onCreated }: { role: string; onClose: () => void; onCreated: () => void }) {
  const [status, setStatus] = useState<FileConnectionStatus[] | null>(null);
  const [provider, setProvider] = useState<"google" | "microsoft" | null>(null);
  const [drafts, setDrafts] = useState<Draft[]>([]);
  const [picking, setPicking] = useState(false);
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);

  useEffect(() => {
    getFileConnectionStatus(role).then(setStatus).catch(() => setStatus([]));
  }, [role]);

  const state = (key: "google" | "microsoft") => status?.find((s) => s.provider === key);
  const usable = (key: "google" | "microsoft") => Boolean(state(key)?.configured && state(key)?.connected);
  const reason = (key: "google" | "microsoft") => {
    const s = state(key);
    if (!s) return "Checking";
    if (!s.configured) return s.reason ?? "Sign-in has not been set up for IIP yet.";
    if (!s.connected) return "Connect your own account first (Settings, Integrations). A connector reads with your sign-in.";
    return null;
  };

  const add = (entries: { id: string; name: string; isFile: boolean }[]) =>
    setDrafts((current) => [
      ...current,
      ...entries.filter((e) => !current.some((d) => d.folderId === e.id)).map((e) => ({ key: e.id, folderId: e.id, displayName: e.name, productLine: "General", documentType: "PolicyWording", isFile: e.isFile })),
    ]);

  async function pickGoogle() {
    setProblem(null);
    try {
      const files = await pickDocumentsFromGoogleDrive(role);
      add(files.map((f) => ({ ...f, isFile: true })));
    } catch (failure) {
      setProblem(failure instanceof Error ? failure.message : "Google's file picker could not be opened.");
    }
  }

  async function connect() {
    if (!provider) return;
    setBusy(true);
    setProblem(null);
    try {
      await createConnector(role, provider, drafts.map(({ key: _key, ...rest }) => rest));
      onCreated();
    } catch (failure) {
      setProblem(failure instanceof Error ? failure.message : "The connector could not be created.");
    } finally {
      setBusy(false);
    }
  }

  const ready = provider !== null && drafts.length > 0 && drafts.every((d) => d.productLine.trim().length > 0);

  return (
    <div className="fixed inset-0 z-40 flex items-center justify-center bg-slate-900/30 p-4" role="presentation" onClick={onClose}>
      <div role="dialog" aria-modal="true" aria-label="Connect folders" className="flex max-h-[min(640px,92dvh)] w-full max-w-xl flex-col overflow-hidden rounded-2xl bg-white shadow-2xl" onClick={(event) => event.stopPropagation()}>
        <div className="border-b border-slate-100 px-5 py-4">
          <h2 className="m-0 text-[15px] font-semibold text-slate-900">Connect folders</h2>
          <p className="m-0 mt-0.5 text-[12px] text-slate-500">Files arrive as pending documents. Nothing is approved by being synced.</p>
        </div>
        <div className="min-h-0 flex-1 space-y-4 overflow-y-auto px-5 py-4">
          {status === null ? <LoadingSkeleton rows={2} /> : (
            <div className="grid gap-2 sm:grid-cols-2" role="radiogroup" aria-label="Where the files are">
              {([["microsoft", "OneDrive and SharePoint", "Sync whole folders."], ["google", "Google Drive", "Pick the files to keep in step."]] as const).map(([key, name, note]) => (
                <button
                  key={key}
                  type="button"
                  role="radio"
                  aria-checked={provider === key}
                  disabled={!usable(key)}
                  onClick={() => { setProvider(key); setDrafts([]); setProblem(null); }}
                  className={cn("rounded-xl border p-3 text-left transition-colors disabled:cursor-not-allowed disabled:opacity-60", provider === key ? "border-dls-primary-accent bg-violet-50" : "border-slate-200 hover:bg-slate-50")}
                >
                  <span className="block text-[13.5px] font-semibold text-slate-900">{name}</span>
                  <span className="mt-0.5 block text-[12px] text-slate-500">{usable(key) ? note : reason(key)}</span>
                </button>
              ))}
            </div>
          )}

          {provider ? (
            <div>
              {provider === "microsoft" ? (
                <Button variant="secondary" onClick={() => setPicking(true)} icon={<Folder aria-hidden="true" />}>Choose a folder</Button>
              ) : (
                <Button variant="secondary" onClick={() => void pickGoogle()} icon={<FileText aria-hidden="true" />}>Pick files</Button>
              )}
              {provider === "google" ? <p className="m-0 mt-1.5 text-[12px] text-slate-500">IIP can only open the files you pick. It cannot browse a Google Drive folder.</p> : null}
            </div>
          ) : null}

          {drafts.length > 0 ? (
            <ul className="m-0 space-y-3 p-0" aria-label="Chosen">
              {drafts.map((draft) => (
                <li key={draft.key} className="list-none rounded-lg border border-slate-200 p-3">
                  <div className="flex items-center justify-between gap-2">
                    <span className="truncate text-[13.5px] font-semibold text-slate-900">{draft.displayName}</span>
                    <button type="button" className="text-[12.5px] text-slate-500 underline" onClick={() => setDrafts((c) => c.filter((d) => d.key !== draft.key))}>Remove</button>
                  </div>
                  <div className="mt-2 grid gap-2 sm:grid-cols-2">
                    <label className="block text-[12px] font-medium text-slate-600">
                      Product line
                      <Input value={draft.productLine} onChange={(e) => setDrafts((c) => c.map((d) => (d.key === draft.key ? { ...d, productLine: e.target.value } : d)))} aria-label={`Product line for ${draft.displayName}`} />
                    </label>
                    <label className="block text-[12px] font-medium text-slate-600">
                      Kind of document
                      <select
                        value={draft.documentType}
                        onChange={(e) => setDrafts((c) => c.map((d) => (d.key === draft.key ? { ...d, documentType: e.target.value } : d)))}
                        aria-label={`Kind of document for ${draft.displayName}`}
                        className="mt-1 block h-10 w-full rounded-lg border border-slate-200 bg-white px-2 text-[13px] text-slate-900"
                      >
                        {DOCUMENT_TYPES.map((type) => <option key={type} value={type}>{type.replace(/([A-Z])/g, " $1").trim()}</option>)}
                      </select>
                    </label>
                  </div>
                </li>
              ))}
            </ul>
          ) : null}
          {problem ? <p role="alert" className="m-0 text-[12.5px] text-dls-danger-text">{problem}</p> : null}
        </div>
        <div className="flex items-center justify-end gap-2 border-t border-slate-100 px-5 py-3">
          <Button variant="secondary" onClick={onClose}>Cancel</Button>
          <Button variant="primary" disabled={!ready || busy} onClick={() => void connect()} icon={busy ? <Loader2 className="animate-spin" aria-hidden="true" /> : undefined}>Connect</Button>
        </div>
      </div>
      {picking ? <OneDriveFolderPicker onClose={() => setPicking(false)} onChosen={(folder) => { add([{ id: folder.id, name: folder.name, isFile: false }]); setPicking(false); }} /> : null}
    </div>
  );
}
