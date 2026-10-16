"use client";

import { useSyncExternalStore } from "react";
import { academyGet, academyPost, AcademyApiError } from "@/lib/academy/api";

// Zenith's conversation. It lives outside React so it survives moving between Academy pages (the floating panel and the full
// page show the same chat) and is cleared when the browser tab is reloaded. What Zenith says always comes from the server;
// the staged reveal of its steps and its words is only presentation of an answer that has already arrived.

export type ZenithSource = { label: string; href: string; number: number | null };
export type ZenithAction = { label: string; href: string | null; q: string | null };
export type ZenithQuiz = {
  courseId: string; questionId: string; lessonTitle: string; q: string; options: { k: number; text: string }[];
  picked?: number; result?: { right: boolean; correctIndex: number; why: string };
};
export type ZenithStatus = { explanations: boolean; configured: boolean; resting: boolean; dailyLimit: number; usedToday: number; remainingToday: number };

export type UserMsg = { role: "user"; text: string };
export type AgentMsg = {
  role: "agent";
  steps: { label: string; state: "pending" | "run" | "done" }[];
  text: string;
  shown: string;
  done: boolean;
  working: boolean;
  seconds: number | null;
  open: boolean;
  mode: string;
  sources: ZenithSource[];
  actions: ZenithAction[];
  quiz: ZenithQuiz | null;
};
export type Msg = UserMsg | AgentMsg;
export type LogLine = { at: string; text: string };

type State = { msgs: Msg[]; busy: boolean; log: LogLine[]; status: ZenithStatus | null };

let state: State = { msgs: [], busy: false, log: [], status: null };
const listeners = new Set<() => void>();
const emit = () => { listeners.forEach((listener) => listener()); };
const set = (patch: Partial<State>) => { state = { ...state, ...patch }; emit(); };
const clock = () => new Date().toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit" });
const log = (text: string) => set({ log: [...state.log, { at: clock(), text }] });
const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

export function useZenith(): State {
  return useSyncExternalStore(
    (listener) => { listeners.add(listener); return () => { listeners.delete(listener); }; },
    () => state,
    () => state,
  );
}

function replaceLast(update: (message: AgentMsg) => AgentMsg) {
  const msgs = state.msgs.slice();
  const last = msgs[msgs.length - 1];
  if (last && last.role === "agent") {
    msgs[msgs.length - 1] = update(last);
    set({ msgs });
  }
}

export async function loadZenithStatus() {
  try {
    set({ status: await academyGet<ZenithStatus>("/zenith/status") });
  } catch {
    // The status only shapes a notice. Without it the chat still works and says what it can.
  }
}

export function resetZenith() {
  if (state.busy) return;
  set({ msgs: [], log: [] });
}

type Reply = {
  mode: string; steps: string[]; text: string; seconds: number;
  sources: ZenithSource[]; actions: ZenithAction[]; quiz: Omit<ZenithQuiz, "picked" | "result"> | null;
};

export async function askZenith(question: string) {
  const text = question.trim();
  if (!text || state.busy) return;
  const history = state.msgs.slice(-6).map((message) => ({ role: message.role === "user" ? "user" : "assistant", text: message.text }));
  const startedAt = Date.now();
  const pending: AgentMsg = { role: "agent", steps: [], text: "", shown: "", done: false, working: true, seconds: null, open: false, mode: "guide", sources: [], actions: [], quiz: null };
  set({ msgs: [...state.msgs, { role: "user", text }, pending], busy: true });
  log(`Received question: "${text.length > 40 ? `${text.slice(0, 40)}...` : text}"`);

  let reply: Reply;
  try {
    reply = await academyPost<Reply>("/zenith/ask", { message: text, history });
  } catch (failure) {
    const message = failure instanceof AcademyApiError
      ? (failure.status === 429 ? "You are asking faster than I can answer. Wait a moment and try again." : failure.message)
      : "I could not be reached just now. Try again in a moment.";
    replaceLast((current) => ({ ...current, working: false, done: true, text: message, shown: message, mode: "error" }));
    set({ busy: false });
    log("The question could not be answered");
    return;
  }

  replaceLast((current) => ({ ...current, working: false, steps: reply.steps.map((label) => ({ label, state: "pending" as const })) }));
  for (let index = 0; index < reply.steps.length; index++) {
    replaceLast((current) => ({ ...current, steps: current.steps.map((step, i) => (i === index ? { ...step, state: "run" as const } : step)) }));
    await sleep(260);
    replaceLast((current) => ({ ...current, steps: current.steps.map((step, i) => (i === index ? { ...step, state: "done" as const } : step)) }));
    log(reply.steps[index]);
  }

  replaceLast((current) => ({ ...current, seconds: (Date.now() - startedAt) / 1000, mode: reply.mode, text: reply.text }));
  const words = reply.text.split(/(\s+)/);
  let shown = "";
  for (const word of words) {
    shown += word;
    if (word.trim()) {
      const snapshot = shown;
      replaceLast((current) => ({ ...current, shown: snapshot }));
      await sleep(14);
    }
  }
  replaceLast((current) => ({ ...current, shown: reply.text, done: true, sources: reply.sources, actions: reply.actions, quiz: reply.quiz }));
  set({ busy: false });
  log(`Answer delivered${reply.sources.length ? ` with ${reply.sources.length} ${reply.sources.length === 1 ? "source" : "sources"}` : ""}`);
  void loadZenithStatus();
}

export async function pickZenithQuiz(index: number, choice: number) {
  const message = state.msgs[index];
  if (!message || message.role !== "agent" || !message.quiz || message.quiz.picked !== undefined) return;
  const quiz = message.quiz;
  const mark = (patch: Partial<ZenithQuiz>) => {
    const msgs = state.msgs.slice();
    const target = msgs[index];
    if (target.role === "agent" && target.quiz) {
      msgs[index] = { ...target, quiz: { ...target.quiz, ...patch } };
      set({ msgs });
    }
  };
  mark({ picked: choice });
  try {
    const result = await academyPost<{ right: boolean; correctIndex: number; why: string }>("/zenith/quiz/answer", { courseId: quiz.courseId, questionId: quiz.questionId, choice });
    mark({ result });
    log(result.right ? "Quiz answer marked correct" : "Quiz answer marked incorrect, explanation shown");
  } catch {
    mark({ picked: undefined });
  }
}

export function toggleZenithSteps(index: number) {
  const msgs = state.msgs.slice();
  const target = msgs[index];
  if (target?.role === "agent") {
    msgs[index] = { ...target, open: !target.open };
    set({ msgs });
  }
}
