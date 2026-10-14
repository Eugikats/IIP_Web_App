"use client";

import { useCallback, useEffect, useState } from "react";
import { ArrowLeft, FileSpreadsheet, Folder, Loader2, X } from "lucide-react";
import { browseOneDrive, downloadFile, FileConnectionError, getGooglePickerSession, type FileItem, type FileProviderKey } from "@/lib/files/fileConnections";
import { useUI } from "@/context/UIStateContext";

type GoogleWindow = Window & {
  gapi?: { load: (name: string, options: { callback: () => void }) => void };
  google?: {
    picker: {
      PickerBuilder: new () => PickerBuilder;
      ViewId: { SPREADSHEETS: string; DOCS: string };
      DocsView: new (viewId?: string) => { setMimeTypes: (types: string) => unknown };
      Action: { PICKED: string; CANCEL: string };
      Feature: { NAV_HIDDEN: string; MULTISELECT_ENABLED: string };
      Response: { ACTION: string; DOCUMENTS: string };
      Document: { ID: string; NAME: string };
    };
  };
};

type PickerBuilder = {
  addView: (view: unknown) => PickerBuilder;
  enableFeature: (feature: string) => PickerBuilder;
  setOAuthToken: (token: string) => PickerBuilder;
  setDeveloperKey: (key: string) => PickerBuilder;
  setAppId: (id: string) => PickerBuilder;
  setCallback: (callback: (data: Record<string, unknown>) => void) => PickerBuilder;
  build: () => { setVisible: (visible: boolean) => void };
};

function loadGooglePicker(): Promise<void> {
  const win = window as GoogleWindow;
  return new Promise((resolve, reject) => {
    const ready = () => win.gapi!.load("picker", { callback: () => resolve() });
    if (win.gapi) return ready();
    const script = document.createElement("script");
    script.src = "https://apis.google.com/js/api.js";
    script.async = true;
    script.onload = ready;
    script.onerror = () => reject(new Error("Google's file picker could not be loaded."));
    document.head.appendChild(script);
  });
}

/** Opens Google's own picker; resolves with the chosen file's id, or null if the person closed it. */
export async function pickFromGoogleDrive(role: string): Promise<string | null> {
  const session = await getGooglePickerSession(role);
  await loadGooglePicker();
  const picker = (window as GoogleWindow).google!.picker;
  return new Promise((resolve) => {
    const view = new picker.DocsView(picker.ViewId.DOCS) as unknown as { setMimeTypes: (types: string) => unknown };
    view.setMimeTypes("text/csv,application/vnd.google-apps.spreadsheet,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    const builder = new picker.PickerBuilder()
      .addView(view)
      .setOAuthToken(session.accessToken)
      .setAppId(session.appId)
      .setCallback((data) => {
        const action = data[picker.Response.ACTION];
        if (action === picker.Action.PICKED) {
          const documents = data[picker.Response.DOCUMENTS] as Record<string, string>[];
          resolve(documents?.[0]?.[picker.Document.ID] ?? null);
        } else if (action === picker.Action.CANCEL) {
          resolve(null);
        }
      });
    if (session.developerKey) builder.setDeveloperKey(session.developerKey);
    builder.build().setVisible(true);
  });
}

/** Opens Google's own picker for documents, several at a time. Only the files chosen become visible to IIP. */
export async function pickDocumentsFromGoogleDrive(role: string): Promise<{ id: string; name: string }[]> {
  const session = await getGooglePickerSession(role);
  await loadGooglePicker();
  const picker = (window as GoogleWindow).google!.picker;
  return new Promise((resolve) => {
    const view = new picker.DocsView(picker.ViewId.DOCS) as unknown as { setMimeTypes: (types: string) => unknown };
    view.setMimeTypes("application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document,text/plain,text/markdown");
    const builder = new picker.PickerBuilder()
      .addView(view)
      .enableFeature(picker.Feature.MULTISELECT_ENABLED)
      .setOAuthToken(session.accessToken)
      .setAppId(session.appId)
      .setCallback((data) => {
        const action = data[picker.Response.ACTION];
        if (action === picker.Action.PICKED) {
          const documents = (data[picker.Response.DOCUMENTS] as Record<string, string>[]) ?? [];
          resolve(documents.map((doc) => ({ id: doc[picker.Document.ID], name: doc[picker.Document.NAME] ?? doc[picker.Document.ID] })));
        } else if (action === picker.Action.CANCEL) {
          resolve([]);
        }
      });
    if (session.developerKey) builder.setDeveloperKey(session.developerKey);
    builder.build().setVisible(true);
  });
}

/** A folder browser over the person's own OneDrive that returns the folder they stand in. Files are shown for context only. */
export function OneDriveFolderPicker({ onChosen, onClose }: { onChosen: (folder: { id: string; name: string }) => void; onClose: () => void }) {
  const { roleDef } = useUI();
  const [trail, setTrail] = useState<{ id: string | null; name: string }[]>([{ id: null, name: "OneDrive" }]);
  const [items, setItems] = useState<FileItem[] | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const current = trail[trail.length - 1];

  const load = useCallback(async (folderId: string | null) => {
    setItems(null);
    setProblem(null);
    try {
      setItems(await browseOneDrive(folderId, roleDef.key));
    } catch (reason) {
      setItems([]);
      setProblem(reason instanceof FileConnectionError ? reason.message : "Your OneDrive could not be read just now.");
    }
  }, [roleDef.key]);

  useEffect(() => { void load(current.id); }, [load, current.id]);

  const folders = (items ?? []).filter((item) => item.isFolder);
  const fileCount = (items ?? []).length - folders.length;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/30 p-4" role="presentation" onClick={onClose}>
      <div role="dialog" aria-modal="true" aria-label="Choose a folder from OneDrive" className="flex max-h-[min(560px,90dvh)] w-full max-w-lg flex-col overflow-hidden rounded-2xl bg-white shadow-2xl" onClick={(event) => event.stopPropagation()}>
        <div className="flex items-center justify-between gap-3 border-b border-slate-100 px-5 py-4">
          <div className="min-w-0">
            <h2 className="m-0 text-[15px] font-semibold text-slate-900">Choose a folder to sync</h2>
            <p className="m-0 mt-0.5 truncate text-[12px] text-slate-500">{trail.map((step) => step.name).join(" / ")}</p>
          </div>
          <button type="button" onClick={onClose} aria-label="Close" className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-slate-100 text-slate-500 transition-colors hover:bg-slate-200">
            <X className="h-4 w-4" aria-hidden />
          </button>
        </div>
        <div className="min-h-0 flex-1 overflow-y-auto p-2">
          {trail.length > 1 ? (
            <button type="button" onClick={() => setTrail((steps) => steps.slice(0, -1))} className="mb-1 flex w-full items-center gap-2 rounded-lg px-3 py-2 text-left text-[13px] font-semibold text-blue-700 transition-colors hover:bg-blue-50">
              <ArrowLeft className="h-4 w-4" aria-hidden /> Back
            </button>
          ) : null}
          {items === null ? (
            <p className="m-0 flex items-center gap-2 px-3 py-6 text-[13px] text-slate-500"><Loader2 className="h-4 w-4 animate-spin" aria-hidden /> Reading your OneDrive</p>
          ) : folders.length === 0 && !problem ? (
            <p className="m-0 px-3 py-6 text-[13px] text-slate-500">No folders here{fileCount > 0 ? `, only ${fileCount} file${fileCount === 1 ? "" : "s"}` : ""}. You can choose this folder.</p>
          ) : (
            <ul className="m-0 list-none p-0">
              {folders.map((item) => (
                <li key={item.id}>
                  <button type="button" onClick={() => setTrail((steps) => [...steps, { id: item.id, name: item.name }])} className="flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-left transition-colors hover:bg-slate-50">
                    <Folder className="h-4 w-4 shrink-0 text-slate-400" aria-hidden />
                    <span className="min-w-0 flex-1 truncate text-[13.5px] text-slate-800">{item.name}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
        {problem ? <p role="alert" className="m-0 border-t border-red-100 bg-red-50 px-5 py-3 text-[12.5px] text-red-700">{problem}</p> : null}
        <div className="flex items-center justify-between gap-3 border-t border-slate-100 px-5 py-3">
          <p className="m-0 text-[11.5px] text-slate-400">PDF, Word and text files in the folder and its subfolders are read.</p>
          <button
            type="button"
            disabled={current.id === null}
            onClick={() => current.id && onChosen({ id: current.id, name: current.name })}
            className="inline-flex h-9 shrink-0 items-center rounded-lg bg-dls-primary-accent px-3 text-[13px] font-semibold text-white disabled:cursor-not-allowed disabled:opacity-50"
          >
            Choose this folder
          </button>
        </div>
      </div>
    </div>
  );
}

const LIST_TYPE = /\.(csv|xlsx)$/i;

/** A small folder browser over the person's own OneDrive. Choosing a CSV or Excel file reads it. */
export function OneDrivePicker({ onPicked, onClose }: { onPicked: (file: File) => void; onClose: () => void }) {
  const { roleDef } = useUI();
  const [trail, setTrail] = useState<{ id: string | null; name: string }[]>([{ id: null, name: "OneDrive" }]);
  const [items, setItems] = useState<FileItem[] | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const [reading, setReading] = useState<string | null>(null);

  const current = trail[trail.length - 1];

  const load = useCallback(async (folderId: string | null) => {
    setItems(null);
    setProblem(null);
    try {
      setItems(await browseOneDrive(folderId, roleDef.key));
    } catch (reason) {
      setItems([]);
      setProblem(reason instanceof FileConnectionError ? reason.message : "Your OneDrive could not be read just now.");
    }
  }, [roleDef.key]);

  useEffect(() => { void load(current.id); }, [load, current.id]);

  const choose = async (item: FileItem) => {
    setReading(item.id);
    setProblem(null);
    try {
      onPicked(await downloadFile("microsoft", item.id, roleDef.key));
    } catch (reason) {
      setProblem(reason instanceof FileConnectionError ? reason.message : "That file could not be read.");
    } finally {
      setReading(null);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/30 p-4" role="presentation" onClick={onClose}>
      <div role="dialog" aria-modal="true" aria-label="Choose a file from OneDrive" className="flex max-h-[min(560px,90dvh)] w-full max-w-lg flex-col overflow-hidden rounded-2xl bg-white shadow-2xl" onClick={(event) => event.stopPropagation()}>
        <div className="flex items-center justify-between gap-3 border-b border-slate-100 px-5 py-4">
          <div className="min-w-0">
            <h2 className="m-0 text-[15px] font-semibold text-slate-900">Choose a list from OneDrive</h2>
            <p className="m-0 mt-0.5 truncate text-[12px] text-slate-500">{trail.map((step) => step.name).join(" / ")}</p>
          </div>
          <button type="button" onClick={onClose} aria-label="Close" className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-slate-100 text-slate-500 transition-colors hover:bg-slate-200">
            <X className="h-4 w-4" aria-hidden />
          </button>
        </div>
        <div className="min-h-0 flex-1 overflow-y-auto p-2">
          {trail.length > 1 ? (
            <button type="button" onClick={() => setTrail((steps) => steps.slice(0, -1))} className="mb-1 flex w-full items-center gap-2 rounded-lg px-3 py-2 text-left text-[13px] font-semibold text-blue-700 transition-colors hover:bg-blue-50">
              <ArrowLeft className="h-4 w-4" aria-hidden /> Back
            </button>
          ) : null}
          {items === null ? (
            <p className="m-0 flex items-center gap-2 px-3 py-6 text-[13px] text-slate-500"><Loader2 className="h-4 w-4 animate-spin" aria-hidden /> Reading your OneDrive</p>
          ) : items.length === 0 && !problem ? (
            <p className="m-0 px-3 py-6 text-[13px] text-slate-500">This folder is empty.</p>
          ) : (
            <ul className="m-0 list-none p-0">
              {items.map((item) => {
                const usable = item.isFolder || LIST_TYPE.test(item.name);
                return (
                  <li key={item.id}>
                    <button
                      type="button"
                      disabled={!usable || reading !== null}
                      onClick={() => (item.isFolder ? setTrail((steps) => [...steps, { id: item.id, name: item.name }]) : void choose(item))}
                      className="flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-left transition-colors hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-45"
                    >
                      {item.isFolder ? <Folder className="h-4 w-4 shrink-0 text-slate-400" aria-hidden /> : <FileSpreadsheet className="h-4 w-4 shrink-0 text-emerald-600" aria-hidden />}
                      <span className="min-w-0 flex-1 truncate text-[13.5px] text-slate-800">{item.name}</span>
                      {reading === item.id ? <Loader2 className="h-4 w-4 animate-spin text-slate-400" aria-hidden /> : null}
                    </button>
                  </li>
                );
              })}
            </ul>
          )}
        </div>
        {problem ? <p role="alert" className="m-0 border-t border-red-100 bg-red-50 px-5 py-3 text-[12.5px] text-red-700">{problem}</p> : null}
        <p className="m-0 border-t border-slate-100 px-5 py-2.5 text-[11.5px] text-slate-400">Only CSV and Excel files can be chosen. IIP reads the file you pick and nothing else.</p>
      </div>
    </div>
  );
}

export type { FileProviderKey };
