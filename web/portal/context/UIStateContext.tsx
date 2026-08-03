"use client";
import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { ROLES, type Role } from "@/lib/roles";

interface UIState {
  sidebarOpen: boolean;
  copilotOpen: boolean;
  role: Role;
  setSidebarOpen: (v: boolean) => void;
  setCopilotOpen: (v: boolean) => void;
  toggleSidebar: () => void;
  toggleCopilot: () => void;
  setRole: (r: Role) => void;
  roleDef: (typeof ROLES)[Role];
}

const Ctx = createContext<UIState | null>(null);

function readLS<T>(k: string, fallback: T): T {
  try {
    const v = localStorage.getItem(k);
    return v ? (JSON.parse(v) as T) : fallback;
  } catch {
    return fallback;
  }
}

export function UIStateProvider({ children }: { children: ReactNode }) {
  const [sidebarOpen, setSidebarOpen] = useState(true);
  const [copilotOpen, setCopilotOpen] = useState(true);
  const [role, setRole] = useState<Role>("Admin");
  const [hydrated, setHydrated] = useState(false);

  useEffect(() => {
    const mobileViewport = window.matchMedia("(max-width: 767px)").matches;
    setSidebarOpen(mobileViewport ? false : readLS("iip.sidebarOpen", true));
    setCopilotOpen(readLS("iip.copilotOpen", true));
    setRole(readLS<Role>("iip.role", "Admin"));
    setHydrated(true);
  }, []);

  useEffect(() => {
    if (hydrated) localStorage.setItem("iip.sidebarOpen", JSON.stringify(sidebarOpen));
  }, [hydrated, sidebarOpen]);
  useEffect(() => {
    if (hydrated) localStorage.setItem("iip.copilotOpen", JSON.stringify(copilotOpen));
  }, [copilotOpen, hydrated]);
  useEffect(() => {
    if (hydrated) localStorage.setItem("iip.role", JSON.stringify(role));
  }, [hydrated, role]);

  const value = useMemo<UIState>(() => ({
    sidebarOpen, copilotOpen, role,
    setSidebarOpen, setCopilotOpen,
    toggleSidebar: () => setSidebarOpen((v) => !v),
    toggleCopilot: () => setCopilotOpen((v) => !v),
    setRole,
    roleDef: ROLES[role],
  }), [sidebarOpen, copilotOpen, role]);

  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export function useUI() {
  const v = useContext(Ctx);
  if (!v) throw new Error("useUI must be used within UIStateProvider");
  return v;
}
