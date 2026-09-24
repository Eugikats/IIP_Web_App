"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { useRuntimeContext } from "@/lib/runtimeContext";
import { humanizeIdentifier, isGuid } from "@/lib/format";

// Who someone is, by name, everywhere a record names a person. Records store directory object ids,
// which are right for audit and unreadable on screen; GET /api/v1/directory/people returns the
// workspace's people so any screen can turn an id back into a name.

export type Person = { userId: string; name: string; role: string };

// Actors that are the platform itself rather than a person. Named for what they did, so an audit
// row or an approval reads "Claims agent (automatic)" instead of "system:claim-triage".
const SYSTEM_ACTORS: Record<string, string> = {
  "system:customer-assistant": "WhatsApp assistant (automatic)",
  "system:claimant-magic-link": "Claimant portal (automatic)",
  "system:mailbox-ingestion": "Email collection (automatic)",
  "system:missing-document-request": "Document request (automatic)",
  "system:claim-triage": "Claims agent (automatic)",
  "system:retention": "Retention policy (automatic)",
};

// One fetch per page load, shared by every component that asks. The list is small and changes only
// when someone new signs in, so there is no reason for each row on a screen to request it again.
let shared: Promise<Person[]> | null = null;

async function fetchPeople(role: string): Promise<Person[]> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/directory/people`, { headers: scopedHeaders(role, ""), cache: "no-store" });
    if (!response.ok) return [];
    return (await response.json()) as Person[];
  } catch {
    return [];
  }
}

export function usePeople() {
  const { roleDef } = useUI();
  const { runtimeContext } = useRuntimeContext();
  const [people, setPeople] = useState<Person[]>([]);
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const currentUserId = runtimeContext?.userId ?? null;

  useEffect(() => {
    if (LOVABLE_VISUAL_PARITY_MODE) return;
    let active = true;
    shared ??= fetchPeople(roleDef.key);
    void shared.then((list) => {
      if (!active) return;
      setPeople(list);
      setLoading(false);
    });
    return () => {
      active = false;
    };
  }, [roleDef.key]);

  const describe = useCallback(
    (id: string | null | undefined, fallback = "Unassigned") => describePerson(id, people, currentUserId, fallback),
    [people, currentUserId],
  );

  return { people, loading, describe, currentUserId };
}

export function describePerson(
  id: string | null | undefined,
  people: Person[],
  currentUserId: string | null,
  fallback = "Unassigned",
): string {
  const value = (id ?? "").trim();
  if (!value || value === "-" || value.toLowerCase() === "unknown") return fallback;
  if (currentUserId && value === currentUserId) return "You";
  if (value.toLowerCase().startsWith("system:")) {
    return SYSTEM_ACTORS[value.toLowerCase()] ?? `${humanizeIdentifier(value.slice(7))} (automatic)`;
  }
  const match = people.find((person) => person.userId === value);
  if (match) return match.name;
  // An id with no directory entry belongs to someone who has not signed in since names started
  // being recorded. Saying so plainly is better than printing their object id.
  if (isGuid(value)) return "Staff member";
  // Anything else is already a readable label: a team ("Motor Claims Review") or a fixture name.
  return value;
}

/** Test seam: forget the shared fetch so a later render requests the directory again. */
export function resetPeopleCache() {
  shared = null;
}
