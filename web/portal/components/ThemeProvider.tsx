"use client";

import { ReactNode, useEffect } from "react";

const STORAGE_KEY = "iip-theme";

export function ThemeProvider({ children }: { children: ReactNode }) {
  // MVP delivery is light-only. Keep older stored dark/system preferences from
  // leaking into visual acceptance runs while retaining a future provider seam.
  useEffect(() => {
    document.documentElement.classList.remove("dark");
    window.localStorage.setItem(STORAGE_KEY, "light");
  }, []);

  return <>{children}</>;
}

export function ThemeToggle() {
  return null;
}
