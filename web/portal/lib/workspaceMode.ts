"use client";

import { useEffect, useState } from "react";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type WorkspaceMode = {
  /** True once the answer is known (or known to be unavailable to this person). */
  loaded: boolean;
  /** Whether this workspace is a bank selling partner insurers' covers. Null when it could not be read. */
  isBancassurance: boolean | null;
  organisation: string | null;
  lineFocus: "Both" | "Life" | "General" | null;
};

const UNKNOWN: WorkspaceMode = { loaded: true, isBancassurance: null, organisation: null, lineFocus: null };

// Reads whose assistant this workspace is (GET /api/v1/workspace/brand), so a screen can show what fits the mode: a
// bank sees bank customers and bank events, an insurer does not. When the answer cannot be read (no permission, the
// service is down) the screen shows everything rather than hiding something that might be needed.
export function useWorkspaceMode(role: string): WorkspaceMode {
  const [mode, setMode] = useState<WorkspaceMode>({ loaded: false, isBancassurance: null, organisation: null, lineFocus: null });

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const response = await fetch(`${apiBaseUrl}/api/v1/workspace/brand`, { headers: scopedHeaders(role, "") });
        if (!response.ok) {
          if (!cancelled) setMode(UNKNOWN);
          return;
        }
        const body = (await response.json()) as { organisation?: string; isBancassurance?: boolean; lineFocus?: WorkspaceMode["lineFocus"] };
        if (!cancelled) {
          setMode({
            loaded: true,
            isBancassurance: typeof body.isBancassurance === "boolean" ? body.isBancassurance : null,
            organisation: body.organisation ?? null,
            lineFocus: body.lineFocus ?? null,
          });
        }
      } catch {
        if (!cancelled) setMode(UNKNOWN);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [role]);

  return mode;
}
