"use client";

import {
  createContext,
  useCallback,
  useContext,
  useState,
  type ReactNode,
} from "react";
import { useRole } from "@/components/RoleContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import {
  type CommandCenterBlock,
  type CommandCenterQueryResponse,
  parseCommandCenterResponse,
} from "@/lib/commandCenterBlocks";

// ---------------------------------------------------------------------------
// Turn union — mirrors AssistantChat.tsx's ChatTurn but holds blocks[]
// instead of a single text answer.
// ---------------------------------------------------------------------------

export type QueryTurn =
  | { id: string; role: "user"; text: string }
  | { id: string; role: "assistant"; pending: true }
  | {
      id: string;
      role: "assistant";
      pending: false;
      blocks: CommandCenterBlock[];
      response: CommandCenterQueryResponse;
    };

// A completed AI turn — the variant the Context & Evidence Rail reads from.
export type CompletedAssistantTurn = Extract<
  QueryTurn,
  { role: "assistant"; pending: false }
>;

export interface CommandCenterQueryState {
  turns: QueryTurn[];
  busy: boolean;
  error: string | null;
  send: (rawText: string) => Promise<void>;
}

// ---------------------------------------------------------------------------
// Shared context — the chat surface and the right rail are SIBLINGS under
// AppShell (not parent/child), so query state must live in a context both can
// read rather than local hook state. The provider is mounted in AppShell,
// inside RoleProvider (it depends on useRole()).
// ---------------------------------------------------------------------------

const CommandCenterQueryContext = createContext<CommandCenterQueryState | null>(
  null
);

export function CommandCenterQueryProvider({
  children,
}: {
  children: ReactNode;
}) {
  const { role, userId } = useRole();
  const [turns, setTurns] = useState<QueryTurn[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const send = useCallback(
    async (rawText: string) => {
      const text = rawText.trim();
      if (!text || busy) return;

      const userTurn: QueryTurn = { id: crypto.randomUUID(), role: "user", text };
      const pendingId = crypto.randomUUID();

      setTurns((prev) => [
        ...prev,
        userTurn,
        { id: pendingId, role: "assistant", pending: true },
      ]);
      setBusy(true);
      setError(null);

      try {
        const response = await fetch(
          `${apiBaseUrl}/api/v1/command-center/query`,
          {
            method: "POST",
            headers: {
              ...scopedHeaders(role, userId),
              "Content-Type": "application/json",
            },
            body: JSON.stringify({ question: text }),
          }
        );

        if (!response.ok) {
          throw new Error(await readQueryError(response));
        }

        const data = parseCommandCenterResponse(await response.json());
        if (!data) {
          throw new Error(
            "Command Center query response did not include required governed provenance and non-decision state."
          );
        }

        setTurns((prev) =>
          prev.map((turn) =>
            turn.id === pendingId
              ? {
                  id: pendingId,
                  role: "assistant",
                  pending: false,
                  blocks: data.blocks,
                  response: data,
                }
              : turn
          )
        );
      } catch (err) {
        setTurns((prev) => prev.filter((turn) => turn.id !== pendingId));
        setError(err instanceof Error ? err.message : "Command Center query failed.");
      } finally {
        setBusy(false);
      }
    },
    [busy, role, userId]
  );

  return (
    <CommandCenterQueryContext.Provider value={{ turns, busy, error, send }}>
      {children}
    </CommandCenterQueryContext.Provider>
  );
}

// ---------------------------------------------------------------------------
// Consumer hooks
// ---------------------------------------------------------------------------

export function useCommandCenterQuery(): CommandCenterQueryState {
  const ctx = useContext(CommandCenterQueryContext);
  if (!ctx) {
    throw new Error(
      "useCommandCenterQuery must be used within a CommandCenterQueryProvider."
    );
  }
  return ctx;
}

/**
 * The latest completed (non-pending) AI turn, or null if none has finished yet.
 * The Context & Evidence Rail reflects this turn.
 */
export function useCommandCenterLastTurn(): CompletedAssistantTurn | null {
  const { turns } = useCommandCenterQuery();
  for (let i = turns.length - 1; i >= 0; i -= 1) {
    const turn = turns[i];
    if (turn.role === "assistant" && turn.pending === false) {
      return turn;
    }
  }
  return null;
}

// ---------------------------------------------------------------------------
// Error reader — mirrors AssistantChat.tsx's readAssistantError
// ---------------------------------------------------------------------------

async function readQueryError(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as {
      message?: string;
      detail?: string;
      title?: string;
    };
    return (
      body.message ??
      body.detail ??
      body.title ??
      `Command Center query failed with HTTP ${response.status}.`
    );
  } catch {
    return `Command Center query failed with HTTP ${response.status}.`;
  }
}
