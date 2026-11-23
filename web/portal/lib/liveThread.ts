"use client";

import type { CommandCenterAnswer, CommandCenterBlock } from "@/lib/lovable-parity/data/commandCenterQuery";

/**
 * What was said and shown in live conversations, kept for this visit so the written conversation can show it
 * wherever the person is when the call ends. The live call lives in the app shell and carries on across
 * screens; the workspace reads this and adds the exchanges to its thread, whenever it is open.
 */

export type LiveThreadItem =
  | { id: number; kind: "exchange"; question: string; answer: CommandCenterAnswer }
  | { id: number; kind: "said"; who: "you" | "iip"; text: string }
  | { id: number; kind: "shown"; blocks: CommandCenterBlock[] };

type Listener = () => void;

let items: LiveThreadItem[] = [];
let nextId = 1;
let conversationId: string | undefined;
const listeners = new Set<Listener>();

function emit() {
  items = [...items];
  listeners.forEach((listener) => listener());
}

export const liveThread = {
  subscribe(listener: Listener) {
    listeners.add(listener);
    return () => { listeners.delete(listener); };
  },
  getSnapshot(): LiveThreadItem[] {
    return items;
  },
  publish(item: DistributiveOmit<LiveThreadItem, "id">) {
    items.push({ ...item, id: nextId++ } as LiveThreadItem);
    emit();
  },
  /** The written conversation the live call adds to, shared with the workspace so both continue the same chat. */
  get conversationId() {
    return conversationId;
  },
  setConversationId(id: string | undefined) {
    if (id && id !== conversationId) {
      conversationId = id;
      if (typeof window !== "undefined") window.dispatchEvent(new CustomEvent("iip:conversations-changed"));
    }
  },
  /** A fresh chat: forget what was said and which conversation it belonged to. */
  clear() {
    items = [];
    conversationId = undefined;
    emit();
  },
};

type DistributiveOmit<T, K extends keyof never> = T extends unknown ? Omit<T, K> : never;

export const EMPTY_THREAD: LiveThreadItem[] = [];
