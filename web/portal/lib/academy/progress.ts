"use client";

import { useCallback, useEffect, useState } from "react";
import { useRuntimeContext } from "@/lib/runtimeContext";

// What a person has completed in IIP Academy. Kept in this browser, per signed-in user: it is a
// convenience record, not an official one. The certificate itself is a certificate of completion.

export type Completion = { courseId: string; name: string; completedAt: string; certificateId: string };

const keyFor = (userId: string) => `iip-academy:${userId}`;

function read(userId: string): Completion[] {
  try {
    const raw = window.localStorage.getItem(keyFor(userId));
    const parsed = raw ? (JSON.parse(raw) as Completion[]) : [];
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

export function useAcademyProgress() {
  const { runtimeContext } = useRuntimeContext();
  const userId = runtimeContext?.userId ?? "anonymous";
  const displayName = runtimeContext?.displayName ?? "";
  const [completions, setCompletions] = useState<Completion[]>([]);

  useEffect(() => { setCompletions(read(userId)); }, [userId]);

  const complete = useCallback((courseId: string, name: string): Completion => {
    const existing = read(userId).find((item) => item.courseId === courseId);
    const entry: Completion = {
      courseId,
      name: name.trim(),
      completedAt: existing?.completedAt ?? new Date().toISOString(),
      certificateId: existing?.certificateId ?? `IIP-${courseId.slice(0, 3).toUpperCase()}-${Date.now().toString(36).toUpperCase()}`,
    };
    const next = [...read(userId).filter((item) => item.courseId !== courseId), { ...entry, name: name.trim() }];
    try {
      window.localStorage.setItem(keyFor(userId), JSON.stringify(next));
    } catch {
      // Storage can be blocked; the certificate still downloads this time.
    }
    setCompletions(next);
    return entry;
  }, [userId]);

  return { completions, complete, displayName, userId };
}
