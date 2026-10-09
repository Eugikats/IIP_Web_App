"use client";

import { useEffect, useRef, useState } from "react";
import { useSearchParams } from "next/navigation";
import { Loader2 } from "lucide-react";
import { useUI } from "@/context/UIStateContext";
import { clearPendingFileProvider, FileConnectionError, finishFileConnection, pendingFileProvider } from "@/lib/files/fileConnections";

/** Where Google or Microsoft sends the person back after they allow IIP to read their files. */
export default function ConnectCallback() {
  const params = useSearchParams();
  const { roleDef } = useUI();
  const started = useRef(false);
  const [problem, setProblem] = useState<string | null>(null);

  useEffect(() => {
    if (started.current) return;
    started.current = true;

    const code = params.get("code");
    const state = params.get("state");
    const providerError = params.get("error");
    const provider = pendingFileProvider();

    if (providerError) {
      clearPendingFileProvider();
      setProblem(providerError === "access_denied" ? "You chose not to allow access, so nothing was connected." : "The sign-in was not completed, so nothing was connected.");
      return;
    }
    if (!code || !state || !provider) {
      setProblem("This page was opened without a sign-in in progress. Start the connection again from Settings, Integrations.");
      return;
    }

    void finishFileConnection(provider, code, state, roleDef.key)
      .then((result) => {
        clearPendingFileProvider();
        window.location.replace(result.returnTo && result.returnTo.startsWith("/app") ? result.returnTo : "/app");
      })
      .catch((reason: unknown) => {
        clearPendingFileProvider();
        setProblem(reason instanceof FileConnectionError ? reason.message : "The connection could not be completed. Try again.");
      });
  }, [params, roleDef.key]);

  return (
    <div className="mx-auto flex min-h-[50dvh] max-w-md flex-col items-center justify-center gap-3 text-center">
      {problem ? (
        <>
          <h1 className="m-0 text-[18px] font-semibold text-slate-900">Not connected</h1>
          <p role="alert" className="m-0 text-[13.5px] text-slate-600">{problem}</p>
          <a href="/app" className="mt-2 inline-flex h-9 items-center rounded-lg bg-[#1769FF] px-4 text-[13px] font-semibold text-white transition-colors hover:bg-[#1258d6]">Back to IIP</a>
        </>
      ) : (
        <>
          <Loader2 className="h-6 w-6 animate-spin text-[#1769FF]" aria-hidden />
          <p className="m-0 text-[14px] text-slate-600">Finishing the connection</p>
        </>
      )}
    </div>
  );
}
